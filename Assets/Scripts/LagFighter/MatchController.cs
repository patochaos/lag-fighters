using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LagFighter
{
    public enum GameMode { Practice, VsAI, PvP, Async, Online }

    // Cómo se VE la repetición (botones en el HUD, cambiables en vivo):
    // LAG = con el lag teatral · NORMAL = limpia · FAST = limpia a ×2.
    public enum ReplayViewMode { Lag, Normal, Fast }

    // Flags del lag TEATRAL del replay (solo presentación, la re-simulación es
    // idéntica). Apagar acá lo que no convenza — cada efecto es independiente.
    public static class ReplayLagFX
    {
        public static bool Enabled = true;      // master: false = replay limpio
        public static bool Stutter = true;      // congelar + rubber-banding ×2.6
        public static bool Choppy = true;       // ratos a ~5 fps (a los saltos) en vez de congelar
        public static bool PingSpike = true;    // ping falso en rojo + wifi en pánico durante el tirón
        public static bool Rewind = true;       // mini-rewind (~hasta 6f) al descongelar: el teleport de netplay
        public static bool AudioDrop = true;    // el audio se ahoga mientras está trabado
        public static bool ScaleWithLag = true; // en Lag Mode, más tirones según el nivel alcanzado en el round
    }

    // Flujo por turnos programados:
    //  - Planning: en pausa, cada jugador arma su cola de hasta 240 frames
    //    (la IA planifica en secreto). Ghost de preview del plan propio.
    //  - Executing: ambas colas corren simultáneas 4 segundos en tiempo real.
    //  - Al final, V reproduce la pelea completa de corrido (replay determinista).
    public class MatchController : MonoBehaviour
    {
        public enum Flow { ModeSelect, Planning, Executing, RoundOver, GameOver, Replay }

        public const int RoundsToWin = 2; // al mejor de 3
        public const int TurnsPerRound = SimConfig.TurnsPerRound; // TIME OVER: gana el que tiene más vida

        public MatchSim Sim { get; private set; }

        // ---- Modo YOMI v2 (discreto): el estado real vive acá; Sim pasa a
        // ser el escenario donde se ACTÚA el resultado de cada turno.
        public YomiSim Yomi { get; private set; }
        YomiTurnResult _yomiResult;
        readonly int[] _yomiHpBefore = new int[2];
        readonly int[] _yomiApBefore = new int[2];
        const int YomiTheaterFrames = 78;              // duración fija del teatro
        // Distancias BIEN distintas: cerca casi pegados, lejos media pantalla.
        // Los moves de entrada usan los clones *Far del catálogo para llegar.
        const float YomiCloseSlot = 0.5f, YomiFarSlot = 1.7f;
        const float YomiRevealSeconds = 2.4f;          // cartas gigantes antes de actuar
        const float YomiTheaterSpeed = 0.6f;           // la acción corre lenta: que se LEA
        float _yomiRevealTimer;
        bool _yomiShowLiveAp = true; // los AP en HUD se actualizan AL CERRAR el turno, no al resolver
        GameObject _yomiFloor;       // franja de piso que pinta la distancia actual

        // Lo que muestran los circulitos: durante la revelación/acción, los AP
        // con los que ARRANCÓ el turno (el resultado se cobra cuando se ve).
        public int YomiDisplayAp(int i) =>
            Yomi == null ? 0 : _yomiShowLiveAp ? Yomi.Ap[i] : _yomiApBefore[i];
        // ---- Modo CARTAS v2 (copia completa de Yomi 2): el estado real vive
        // en CardSim (mazos, meter, combos); la sim de frames actúa el
        // resultado, igual que en YOMI. La mano vive en CardHandUI.
        public CardSim Cards { get; private set; }
        CardTurnResult _cardsResult;
        readonly int[] _cardsHpBefore = new int[2];
        int _cardsRound;                 // alterna quién empieza cada partida
        int _cardsPlayerChar, _cardsAiChar;
        CardHandUI _hand;
        bool _cardsCombatOn;             // openers revelados: followup en curso
        const float CardsSlot = 0.62f;   // marcas de los peleadores en el teatro

        // ---- Modo DUELO (2026-07-25): EL núcleo casual. Misma arquitectura
        // que YOMI y CARTAS — DuelSim manda, la sim de frames actúa. Ver
        // DUELO.md. La mano vive en DuelHandUI y todo lo público en DuelHudUI.
        public DuelSim Duel { get; private set; }
        DuelTurnResult _duelResult;
        readonly int[] _duelHpBefore = new int[2];
        int _duelPlayerChar, _duelAiChar;
        DuelHandUI _duelHand;
        DuelHudUI _duelHud;
        bool _duelCombatOn;
        float _duelBeat;                       // reloj de los tres tiempos
        int _duelStage;                        // 0 coil · 1 impacto · 2 consecuencia · 3 leer
        readonly bool[] _duelWasDown = new bool[2];
        // lo que se cuenta para la pantalla de resultados
        readonly int[] _duelDmgDealt = new int[2];
        readonly int[] _duelGuardsHit = new int[2];
        readonly int[] _duelKdsDealt = new int[2];
        float _duelPostReveal;
        bool _duelAwaitChoice;   // la ceremonia termina abriendo una decisión
        // A 0.62 (1.24 de separación) los dos blockmen se interpenetraban los
        // brazos y ocupaban el 15% del alto. A 1.7 se leen como dos siluetas
        // enfrentadas y quedan por dentro de los paneles del HUD.
        const float DuelSlot = 1.7f;

        public float TickFloat => Sim == null ? 0f : Sim.Tick + _acc / SimConfig.TickDuration;
        public GameMode Mode { get; private set; }
        public bool LagMode { get; private set; }
        public AIProfile SelectedAIProfile { get; private set; } = AIProfile.Random;
        public AIDifficulty SelectedAIDifficulty { get; private set; } = AIDifficulty.Normal;
        public Flow State { get; private set; } = Flow.ModeSelect;
        public int Picker { get; private set; }
        public int LocalSide { get; private set; } // en Async: qué lado soy yo

        // 1v1 local con picks secretos: pantalla "pasá el teclado" entre pickers
        bool _handoff;
        int _pendingPicker;
        int _guardFrame; // evita que el mismo Espacio confirme dos cosas

        // Async: después de planificar, espero el código del rival
        bool _awaitingCode;
        string _myCode = "";

        // Online (sala Supabase): contador global de intercambios (cruza rounds)
        int _netSeq;

        // Timer de planificación (ONLINE y 1v1 local): al agotarse se manda lo
        // que haya; sin órdenes = quieto bloqueando. Que nadie eternice el turno.
        public const float PlanSeconds = 30f;
        float _planTimer;
        bool TimedPlanning => State == Flow.Planning && !_handoff && !_awaitingCode &&
                              (Mode == GameMode.PvP || Mode == GameMode.Online);
        public int TurnNumber { get; private set; }
        public int TurnStartTick { get; private set; }

        // Lag Mode: cada 3 turnos el lag sube 50%. IT GETS LAGGIER (despacio).
        static readonly int[] LagFrames = { 60, 90, 135, 202, 303 };
        // LA fórmula del lag vive solo acá: +50% cada 3 turnos, con cap.
        public int LagLevelForTurn(int turn) => LagMode ? Mathf.Min((Mathf.Max(turn, 1) - 1) / 3, LagFrames.Length - 1) : 0;
        public int FramesForLevel(int level) => LagFrames[Mathf.Clamp(level, 0, LagFrames.Length - 1)];
        public int LagLevel => LagLevelForTurn(TurnNumber);
        public int CurrentTurnFrames => LagFrames[LagLevel];
        int _prevLagLevel;

        // Wakeup options: al planificar derribado elegís levantarte rápido
        // (menos knockdown) o quedarte (más, para que el meaty whiffee).
        // Es información secreta hasta que el turno se ejecuta.
        public const int WakeQuickDelta = -16, WakeStayDelta = 16;
        readonly bool[] _wakeQuick = { true, true };
        // Reversal (válvula anti-vortex): tercera opción de wakeup — 1 por
        // round, cuesta AP del stock, te levanta YA y separa.
        readonly bool[] _wakeReversal = new bool[2];
        // Economía de AP del modo clásico (regla pura en ApEconomy, Sim.cs):
        // el stock solo limita la planificación, jamás toca la sim → replay y
        // online quedan deterministas sin viajar en el protocolo.
        readonly ApEconomy _eco = new ApEconomy();

        readonly List<int>[] _plans = { new List<int>(), new List<int>() };
        // r0/r1: ese lado abrió el turno con REVERSAL (el replay lo re-aplica)
        readonly List<(List<int> q0, List<int> q1, int w0, int w1, bool r0, bool r1)> _turnLog =
            new List<(List<int>, List<int>, int, int, bool, bool)>();
        int _replayTurn;

        // Lag TEATRAL del replay (solo presentación): cada tanto la repetición
        // se traba como un stream con mala conexión, acumula "deuda" de tiempo
        // y después corre acelerada hasta alcanzarse. La sim re-simula idéntico;
        // acá solo se maquilla el reloj de playback.
        float _replayGlitchIn;  // segundos hasta el próximo tirón
        float _replayFreeze;    // lo que queda congelado
        float _replayDebt;      // tiempo congelado a recuperar en fast-forward
        float _choppyTimer;     // rato "a los saltos": suelta el tiempo en tandas de ~0.18s
        float _choppyAcc;
        float _replayIntensity = 1f;   // >1 en Lag Mode según el nivel alcanzado
        MatchSim _rewindSnap;          // foto reciente de la sim para el mini-rewind
        int _suppressEventsUntil = -1; // re-step tras rewind: no re-disparar sfx/sparks ya vistos

        // modo de visualización del replay, cambiable EN VIVO desde el HUD
        // (se recuerda dentro de la sesión)
        // Lag teatral DORMIDO (2026-07-20, pedido): el replay arranca NORMAL
        // y solo se ofrece RÁPIDO. Todo el código de ReplayLagFX y el modo
        // Lag quedan intactos para cuando vuelva la temática.
        // RÁPIDO por defecto: la acción ya se vio en vivo, el replay es repaso
        public ReplayViewMode ReplayMode { get; private set; } = ReplayViewMode.Fast;

        public void SetReplayMode(ReplayViewMode m)
        {
            if (m == ReplayMode) return;
            ReplayMode = m;
            // cortar en seco cualquier tirón en curso al salir de LAG
            // (_suppressEventsUntil queda: evita re-juice si venimos de un rewind)
            _replayFreeze = 0f;
            _replayDebt = 0f;
            _choppyTimer = 0f;
            _choppyAcc = 0f;
            _hud.SetReplayStalled(false);
            if (AudioListener.volume < 1f) AudioListener.volume = 1f;
        }
        readonly int[] _wins = new int[2];
        public readonly int[] TurnStartStun = new int[2];        // stun arrastrado al arrancar el turno
        public readonly StunKind[] TurnStartStunKind = new StunKind[2];
        public readonly int[] TurnStartCommitted = new int[2];   // frames de move que cruzaron el turno (turno fluido)
        float _roundTimer;
        float _hitstop;
        float _koTimer; // KO en cámara lenta (cosmético: timeScale, la sim ya terminó)
        bool _autoReplay; // el replay del round que corre SIEMPRE después del KO
        int _lastProjCount;

        // velocidad de playback (solo presentación, la sim no cambia)
        public float PlaybackSpeed { get; private set; } = 1f;
        public void SetPlaybackSpeed(float s) => PlaybackSpeed = s;
        // La acción del modo clásico corre al 50% (2026-07-20: a velocidad
        // real no se entendía qué pasó). Afecta ejecución Y replay — RÁPIDO
        // (×2) del replay queda en tiempo real. El teatro YOMI tiene su
        // propio ritmo (YomiTheaterSpeed) y no pasa por acá.
        const float ClassicPace = 0.5f;

        // stats del turno para el resumen post-turno y el log
        readonly float[] _turnDmg = new float[2];
        readonly int[] _turnHitCount = new int[2];
        readonly int[] _turnLost = new int[2];
        bool _hasTurnSummary;

        readonly FighterView[] _views = new FighterView[2];
        HudUI _hud;
        PlanMenuUI _menu;
        ModeMenuUI _modeMenu;
        LiveViz _viz;
        GhostViz _ghost;
        SimpleAI _ai;
        float _acc;
        int _seed = 1234;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindAnyObjectByType<MatchController>() != null) return;
            new GameObject("LagFighter.Match").AddComponent<MatchController>();
        }

        // Tips de la primera vez, solo en Práctica. Avanzan con lo que hacés
        // (primera orden, cada turno confirmado) y no vuelven (PlayerPrefs).
        static readonly string[] Tips =
        {
            "TIP: pasá el mouse por las cartas para ver qué hacen · click las agrega al turno (probá DASH + y después JAB)",
            "TIP: el fantasma es tu plan · arrastrá tu barra de abajo para verlo cuadro a cuadro · click derecho en una ficha la borra",
            "TIP: H muestra las cajas de golpe · el panel derecho da startup / activo / recovery de cada movimiento",
            "TIP: \"pegaría N si no se mueve\" es contra un rival quieto — en VS IA va a reaccionar · M vuelve al menú",
        };
        int _tipStage = -1;

        void Start()
        {
            CursorFX.Apply();
            ArenaBuilder.Build();
            Sim = new MatchSim();
            _views[0] = FighterView.Create(0, this);
            _views[1] = FighterView.Create(1, this);
            _viz = LiveViz.Create(this);
            _ghost = GhostViz.Create();
            _hud = HudUI.Create(this);
            _menu = PlanMenuUI.Create(this);
            _hand = CardHandUI.Create(this);
            _duelHand = DuelHandUI.Create(this);
            _duelHud = DuelHudUI.Create(this);
            _modeMenu = ModeMenuUI.Create(this);
            GoToModeSelect();
        }

        // ---------- navegación ----------

        public void GoToModeSelect()
        {
            Time.timeScale = 1f;
            _koTimer = 0f;
            _autoReplay = false;
            _handoff = false;
            _awaitingCode = false;
            if (Mode == GameMode.Online) NetLobby.I.Leave(); // suelta la sala y corta polls
            State = Flow.ModeSelect;
            _tipStage = -1;
            _hud.SetTip("");
            _hud.HideYomiCards();
            UpdateYomiFloor();
            _menu.Close();
            _hand.Close();
            _ghost.Clear();
            _modeMenu.Open();
            _hud.SetPrompt("");
        }

        public void StartMatch(GameMode mode, bool lagMode, int localSide = 0,
            AIProfile aiProfile = AIProfile.Random, AIDifficulty aiDifficulty = AIDifficulty.Normal,
            bool yomi = false, bool cards = false, int cardsChar = 0,
            bool duel = false, int duelChar = 0)
        {
            Mode = mode;
            LagMode = lagMode;
            LocalSide = localSide;
            // Modo YOMI v2 (discreto): la lógica vive en YomiSim; la sim de
            // frames queda de teatro. ModeMenuUI.Open() apaga el flag al volver.
            SimConfig.YomiEnabled = yomi;
            // Modo CARTAS (copia de Yomi 2): ídem con CardSim. El personaje
            // rival lo sortea la casa — con dos mazos, que se vean los dos.
            SimConfig.CardsEnabled = cards;
            // Modo DUELO: el rival sortea personaje entre los tres mazos.
            SimConfig.DuelEnabled = duel;
            _duelPlayerChar = Mathf.Clamp(duelChar, 0, DuelCatalog.Chars.Length - 1);
            _duelAiChar = Random.Range(0, DuelCatalog.Chars.Length);
            _duelHud.SetVisible(duel);
            _hud.SetDuelChrome(duel);
            ArenaBuilder.SetDuelStage(duel);   // el cuarto oscuro (DUELO-LOOK §2)
            // Sin teatro los peleadores no tienen nada que actuar: se ocultan
            // y el escenario queda de fondo. La ceremonia son las cartas.
            for (int i = 0; i < 2; i++)
                if (_views[i] != null)
                    _views[i].gameObject.SetActive(!duel || SimConfig.DuelTheaterEnabled);
            _cardsRound = 0;
            _cardsPlayerChar = cardsChar;
            _cardsAiChar = Random.Range(0, CardCatalog.Chars.Length);
            // el toggle no viaja en el protocolo online: forzarlo OFF evita desyncs
            if (mode == GameMode.Online || mode == GameMode.Async) SimConfig.CarryoverEnabled = false;
            SelectedAIProfile = aiProfile;
            SelectedAIDifficulty = aiDifficulty;
            _modeMenu.Close();
            _tipStage = mode == GameMode.Practice && PlayerPrefs.GetInt("lf_tips", 0) == 0 ? 0 : -1;
            _hud.SetTip(_tipStage >= 0 ? Tips[0] : "");
            ResetMatch();
        }

        void AdvanceTip()
        {
            if (_tipStage < 0) return;
            _tipStage++;
            if (_tipStage >= Tips.Length)
            {
                _tipStage = -1;
                PlayerPrefs.SetInt("lf_tips", 1); // no molestar nunca más
            }
            _hud.SetTip(_tipStage >= 0 ? Tips[_tipStage] : "");
        }

        void ResetMatch()
        {
            _wins[0] = _wins[1] = 0;
            _netSeq = 0;
            _ai = new SimpleAI(_seed++, SelectedAIProfile, SelectedAIDifficulty);
            StartRound();
        }

        void StartRound()
        {
            Sim = new MatchSim();
            if (SimConfig.CardsEnabled)
            {
                // el que empieza alterna por partida (el activo gana los empates)
                Cards = new CardSim(seed: _seed++, firstPlayer: _cardsRound++ % 2,
                    _cardsPlayerChar, _cardsAiChar);
                _cardsCombatOn = false;
                for (int i = 0; i < 2; i++)
                {
                    var f = Sim.Fighters[i];
                    f.BlockEnabled = false; // la tabla decide, no el auto-bloqueo
                    f.X = i == 0 ? -CardsSlot : CardsSlot;
                    f.PrevX = f.X;
                }
            }
            if (SimConfig.DuelEnabled)
            {
                Duel = new DuelSim(seed: _seed++, _duelPlayerChar, _duelAiChar);
                _duelCombatOn = false;
                for (int i = 0; i < 2; i++)
                {
                    _duelDmgDealt[i] = _duelGuardsHit[i] = _duelKdsDealt[i] = 0;
                    _duelWasDown[i] = false;
                }
                _duelHud.HideResults();
                for (int i = 0; i < 2; i++)
                {
                    var f = Sim.Fighters[i];
                    f.BlockEnabled = false; // la tabla decide, no el auto-bloqueo
                    f.X = i == 0 ? -DuelSlot : DuelSlot;
                    f.PrevX = f.X;
                }
            }
            if (SimConfig.YomiEnabled)
            {
                Yomi = new YomiSim();
                _yomiShowLiveAp = true;
                float slot = Yomi.Close ? YomiCloseSlot : YomiFarSlot;
                for (int i = 0; i < 2; i++)
                {
                    var f = Sim.Fighters[i];
                    f.BlockEnabled = false; // la tabla decide, no el auto-bloqueo
                    f.X = i == 0 ? -slot : slot;
                    f.PrevX = f.X;
                }
                UpdateYomiFloor();
            }
            if (Mode == GameMode.Practice) Sim.Fighters[1].BlockEnabled = false; // el dummy no bloquea
            Time.timeScale = 1f;
            _koTimer = 0f;
            _autoReplay = false;
            _acc = 0f;
            _hitstop = 0f;
            _lastProjCount = 0;
            TurnNumber = 0;
            _prevLagLevel = 0;
            _eco.ResetRound(ApPerTurn); // stock lleno y reversal disponible por round
            _turnLog.Clear(); // el replay cubre el round en curso
            _hasTurnSummary = false;
            _hud.ClearTurnLog();
            _ghost.Clear();
            _menu.Close();
            _views[0].OnMatchReset();
            _views[1].OnMatchReset();
            _hud.OnMatchReset();
            // En DUELO no hay rounds (una partida ES el match) y el cartel
            // tapaba justo el centro, que es donde vive la revelación.
            if (Mode != GameMode.Practice && !SimConfig.DuelEnabled)
                _hud.ShowBigMessage($"ROUND {_wins[0] + _wins[1] + 1}\n<size=18>¡PELEA!</size>", new Color(1f, 0.9f, 0.4f));
            StartPlanning();
        }

        public int GetWins(int i) => _wins[i];

        // ---------- planning ----------

        void StartPlanning()
        {
            if (SimConfig.DuelEnabled) { StartDuelPlanning(); return; }
            if (SimConfig.CardsEnabled) { StartCardsPlanning(); return; }
            if (SimConfig.YomiEnabled) { StartYomiPlanning(); return; }
            TurnNumber++;
            if (LagMode && LagLevel != _prevLagLevel)
            {
                _prevLagLevel = LagLevel;
                _hud.ShowLagMessage(LagMessage(LagLevel));
                SfxLib.Play(SfxLib.Kind.Ko, 0.5f);
            }
            CaptureTurnStartStun();
            _wakeQuick[0] = _wakeQuick[1] = true;
            _wakeReversal[0] = _wakeReversal[1] = false;
            _plans[0].Clear();
            _plans[1].Clear();
            _awaitingCode = false;
            _myCode = "";
            if (Mode == GameMode.Practice || Mode == GameMode.VsAI)
            {
                if (Mode == GameMode.VsAI && WakeupAvailable(1))
                {
                    _wakeQuick[1] = _ai.QuickRise();
                    _wakeReversal[1] = ReversalSelectable(1) && _ai.UseReversal();
                }
                _plans[1] = Mode == GameMode.Practice ? new List<int>()
                    : _ai.Plan(Sim, 1, CurrentTurnFrames - WakeDelta(1),
                        _eco.Stock[1] - (_wakeReversal[1] ? ApEconomy.ReversalCost : 0));
            }
            Picker = (Mode == GameMode.Async || Mode == GameMode.Online) ? LocalSide : 0;
            State = Flow.Planning;
            _planTimer = PlanSeconds;
            if (Mode == GameMode.PvP)
            {
                BeginHandoff(0); // nadie planifica hasta que el jugador tome el teclado
                return;
            }
            _menu.Open(Picker);
            UpdatePlanPrompt();
            UpdateGhost();
        }

        void BeginHandoff(int who)
        {
            _handoff = true;
            _pendingPicker = who;
            _guardFrame = Time.frameCount;
            _menu.Close();
            _ghost.Clear();
            _hud.SetBanner($"PASÁ EL TECLADO\n<size=30>JUGADOR {who + 1}: ESPACIO cuando lo tengas (que el otro no mire)</size>");
            _hud.SetPrompt($"TURNO {TurnNumber} — picks secretos");
        }

        void UpdatePlanPrompt()
        {
            string who = Mode == GameMode.PvP ? $"PLANIFICA JUGADOR {Picker + 1}" :
                         Mode == GameMode.Practice ? "PRÁCTICA — el dummy no hace nada" :
                         Mode == GameMode.VsAI ? $"ARMÁ TU TURNO · IA {ProfileName(_ai.ResolvedProfile)} {DifficultyName(SelectedAIDifficulty)}" :
                         "ARMÁ TU TURNO";
            int myStun = Sim.StunRemaining(Picker);
            int oppStun = Sim.StunRemaining(1 - Picker);
            string adv = "";
            if (myStun > 0) adv = $"  ·  arrancás −{myStun}f ({StunName(Picker)})";
            else if (oppStun > 0) adv = $"  ·  VENTAJA +{oppStun}f (rival en {StunName(1 - Picker)})";
            // turno fluido: los moves que cruzaron el límite son info pública
            if (TurnStartCommitted[Picker] > 0)
                adv += $"  ·  seguís en {MoveCatalog.All[Sim.Fighters[Picker].MoveIndex].Name} ({TurnStartCommitted[Picker]}f)";
            if (TurnStartCommitted[1 - Picker] > 0)
                adv += $"  ·  RIVAL comprometido: {MoveCatalog.All[Sim.Fighters[1 - Picker].MoveIndex].Name} ({TurnStartCommitted[1 - Picker]}f)";
            string lag = "";
            if (LagMode && LagLevel > LagLevelForTurn(TurnNumber - 1))
                lag = $"  ·  ¡AHORA {CurrentTurnFrames}F POR TURNO!";
            string turnLabel = Mode == GameMode.Practice ? $"TURNO {TurnNumber}" : $"TURNO {TurnNumber}/{TurnsPerRound}";
            if (Mode != GameMode.Practice && TurnNumber >= TurnsPerRound - 2) turnLabel += " · ¡SE ACABA!";
            _hud.SetPrompt($"{turnLabel} — {who}{adv}{lag}");

            // resumen de lo que pasó en el turno anterior, desde la silla del picker
            if (_hasTurnSummary && Mode != GameMode.Practice)
            {
                int p = Picker, o = 1 - Picker;
                // rich text con el color = significado de la paleta: lo bueno
                // verde, lo malo rojo, lo perdido ámbar — se escanea de reojo
                string pega = _turnHitCount[p] > 0
                    ? $"<color=#59d96b>pegaste {_turnHitCount[p]} (−{_turnDmg[p]:0} HP)</color>"
                    : "pegaste 0";
                string recibe = _turnHitCount[o] > 0
                    ? $"<color=#f25549>recibiste {_turnHitCount[o]} (−{_turnDmg[o]:0} HP)</color>"
                    : "recibiste 0";
                string perdio = _turnLost[p] > 0
                    ? $"<color=#ffd94d>perdiste {_turnLost[p]} órdenes</color>"
                    : "no perdiste órdenes";
                _hud.SetTurnSummary($"último turno: {pega} · {recibe} · {perdio}");
            }
            else _hud.SetTurnSummary("");
        }

        static string LagMessage(int level)
        {
            switch (level)
            {
                case 1: return "IT GETS LAGGIER…\n<size=14>90 frames por turno</size>";
                case 2: return "EL WIFI ESTÁ LLORANDO\n<size=14>135 frames por turno</size>";
                case 3: return "MODO DIAL-UP\n<size=14>202 frames por turno</size>";
                default: return "PALOMA MENSAJERA\n<size=14>303 frames por turno</size>";
            }
        }

        string StunName(int i)
        {
            switch (Sim.Fighters[i].Stun)
            {
                case StunKind.Knockdown: return "KNOCKDOWN";
                case StunKind.Blockstun: return "BLOCKSTUN";
                default: return "HITSTUN";
            }
        }

        public List<int> GetPlan(int i) => State == Flow.Planning ? _plans[i] : Sim.Fighters[i].Queue;

        public bool RowRevealed(int i)
        {
            if (Mode == GameMode.Practice || State == Flow.Executing || State == Flow.Replay || State == Flow.GameOver) return true;
            if (_handoff) return false; // pantalla "pasá el teclado": no se ve nada
            return State == Flow.Planning && i == Picker;
        }

        public int PlanFramesUsed(int i)
        {
            int f = 0;
            foreach (var m in _plans[i]) f += MoveCatalog.All[m].Total;
            return f;
        }

        // ---------- wakeup options ----------

        public bool WakeupAvailable(int i) => TurnStartStunKind[i] == StunKind.Knockdown && TurnStartStun[i] > 0;
        public bool WakeQuickChoice(int i) => _wakeQuick[i];
        // Reversal: derribado + no usado este round + AP en el stock
        public bool ReversalSelectable(int i) =>
            SimConfig.ApActive && WakeupAvailable(i) && !_eco.ReversalUsed[i] && _eco.Stock[i] >= ApEconomy.ReversalCost;
        public bool WakeReversalChoice(int i) => _wakeReversal[i];
        public int WakeDelta(int i) => _wakeReversal[i] ? 0 : WakeupAvailable(i) ? (_wakeQuick[i] ? WakeQuickDelta : WakeStayDelta) : 0;
        // con reversal te levantás YA: el stun arrastrado desaparece
        public int EffectiveStartStun(int i) => _wakeReversal[i] ? 0 : Mathf.Max(0, TurnStartStun[i] + WakeDelta(i));

        public void ToggleWakeup()
        {
            // ciclo RÁPIDO → QUEDARSE → REVERSAL (si está disponible) → RÁPIDO
            if (_wakeReversal[Picker]) { _wakeReversal[Picker] = false; _wakeQuick[Picker] = true; }
            else if (_wakeQuick[Picker]) _wakeQuick[Picker] = false;
            else if (ReversalSelectable(Picker)) _wakeReversal[Picker] = true;
            else _wakeQuick[Picker] = true;
            // si el plan ya no entra con menos presupuesto, se recorta desde el final
            // (en turno fluido un move es válido mientras ARRANQUE dentro del turno)
            while (_plans[Picker].Count > 0 && (SimConfig.ApActive
                ? (SimConfig.ApOverflowEnabled
                    ? PlanApUsed(Picker) - MoveCatalog.All[_plans[Picker][_plans[Picker].Count - 1]].ApCost >= PlanApAvailable(Picker)
                    : PlanApUsed(Picker) > PlanApAvailable(Picker))
                : SimConfig.CarryoverEnabled
                    ? PlanFramesUsed(Picker) - MoveCatalog.All[_plans[Picker][_plans[Picker].Count - 1]].Total >= PlanFramesAvailable(Picker)
                    : PlanFramesUsed(Picker) > PlanFramesAvailable(Picker)))
                _plans[Picker].RemoveAt(_plans[Picker].Count - 1);
            UpdateGhost();
        }

        // la timeline muestra el stun base (info pública); tu propia fila, tu elección
        public int DisplayStun(int i) => State == Flow.Planning && i == Picker ? EffectiveStartStun(i) : TurnStartStun[i];

        // offset total de la timeline: stun arrastrado + move comprometido
        // (turno fluido); nunca coexisten porque un golpe cancela el move
        public int TimelineOffset(int i) => DisplayStun(i) + TurnStartCommitted[i];

        // frames del move en curso que cruzan el fin del turno (badge OVERFLOW):
        // planificando = lo ya comprometido; ejecutando = lo que va a cruzar
        public int OverflowFrames(int i)
        {
            if (State == Flow.Planning) return TurnStartCommitted[i];
            var f = Sim.Fighters[i];
            if (f.MoveIndex < 0) return 0;
            int endTick = f.MoveStartTick + MoveCatalog.All[f.MoveIndex].Total;
            return Mathf.Max(0, endTick - (TurnStartTick + CurrentTurnFrames));
        }

        // hover de carta: el ghost muestra el plan actual + la carta bajo el
        // cursor, como si ya estuviera agregada (sin comprometerla)
        public void PreviewHover(int moveIndex)
        {
            if (State != Flow.Planning) return;
            var preview = _plans[Picker];
            if (moveIndex >= 0 && PlanFits(moveIndex))
                preview = new List<int>(preview) { moveIndex };
            var basis = Sim;
            if (WakeDelta(Picker) != 0 || _wakeReversal[Picker])
            {
                basis = Sim.Clone();
                if (_wakeReversal[Picker]) basis.Reversal(Picker);
                else basis.AdjustKnockdown(Picker, WakeDelta(Picker));
            }
            _ghost.Show(basis, Picker, preview, CurrentTurnFrames);
        }

        // el stun arrastrado te come frames del turno: solo se planifica lo que entra
        public int PlanFramesAvailable(int i) =>
            Mathf.Max(0, CurrentTurnFrames - EffectiveStartStun(i) - TurnStartCommitted[i]);

        // ---- modo AP: el mismo presupuesto, contado en action points ----
        // Capacidad del turno (60f → 5 AP; en Lag Mode crece con los frames).
        public int ApPerTurn => CurrentTurnFrames / SimConfig.FramesPerAp;
        public int PlanApUsed(int i)
        {
            int ap = 0;
            foreach (var m in _plans[i]) ap += MoveCatalog.All[m].ApCost;
            return ap;
        }
        // AP disponibles = lo que dé menos entre tu STOCK (economía: ingreso
        // +4/turno, lo no gastado se guarda hasta 7, bloquear banca +1) y los
        // slots FÍSICOS que quedan del turno (el stun arrastrado los come).
        public int PlanApAvailable(int i) =>
            Mathf.Max(0, Mathf.Min(
                _eco.Stock[i] - (_wakeReversal[i] ? ApEconomy.ReversalCost : 0),
                PlanFramesAvailable(i) / SimConfig.FramesPerAp));
        // AP que este plan le pide prestados al turno que viene (overflow —
        // desactivado por ahora: con ApOverflowEnabled=false esto siempre da 0)
        public int PlanApBorrowed(int i) => Mathf.Max(0, PlanApUsed(i) - PlanApAvailable(i));
        // stock crudo (para las bolitas del HUD y la IA)
        public int ApStock(int i) => _eco.Stock[i];
        public int ApStockCap => ApEconomy.Cap(ApPerTurn);
        // Lo que muestran las bolitas: tu lado descuenta EN VIVO lo que vas
        // planificando; el rival muestra su stock público de arranque (su
        // plan es secreto — el cobro se ve recién al cerrar el turno).
        public int ApStockShown(int i) =>
            State == Flow.Planning && i == Picker
                ? Mathf.Max(0, _eco.Stock[i] - PlanApUsed(i) - (_wakeReversal[i] ? ApEconomy.ReversalCost : 0))
                : _eco.Stock[i];
        // Turno fluido: alcanza con que el move ARRANQUE dentro del turno
        // (el último puede cruzar el límite). Estricto: tiene que entrar entero.
        public bool PlanFits(int moveIndex) =>
            Sim.MoveAllowed(Picker, moveIndex) &&
            // la barra se gasta al ejecutar: una sola super por plan
            !(moveIndex == MoveCatalog.Super && _plans[Picker].Contains(MoveCatalog.Super)) &&
            (SimConfig.ApActive
                // modo AP estricto: el move entra si te alcanzan los AP.
                // (Con overflow habilitado — hoy no — alcanza con 1 AP libre
                // y el último move puede pasarse pidiendo prestado.)
                ? (SimConfig.ApOverflowEnabled
                    ? PlanApUsed(Picker) < PlanApAvailable(Picker)
                    : PlanApUsed(Picker) + MoveCatalog.All[moveIndex].ApCost <= PlanApAvailable(Picker))
                : SimConfig.CarryoverEnabled
                    ? PlanFramesUsed(Picker) < PlanFramesAvailable(Picker)
                    : PlanFramesUsed(Picker) + MoveCatalog.All[moveIndex].Total <= PlanFramesAvailable(Picker));

        public void PlanAdd(int moveIndex)
        {
            if (!PlanFits(moveIndex)) return;
            _plans[Picker].Add(moveIndex);
            if (_tipStage == 0) AdvanceTip(); // primera orden puesta: siguiente tip
            UpdateGhost();
        }

        // borrar una orden puntual (click derecho en su ficha de la timeline)
        public void PlanRemoveAt(int fighter, int index)
        {
            if (State != Flow.Planning || fighter != Picker) return;
            if (index < 0 || index >= _plans[Picker].Count) return;
            _plans[Picker].RemoveAt(index);
            UpdateGhost();
        }

        // scrub del ghost desde la timeline (−1 = volver al loop automático)
        public void GhostScrub(float frame) => _ghost.SetScrub(frame);

        // wrappers para los botones de fin de partida
        public void RequestRematch() { if (State == Flow.GameOver && Mode != GameMode.Online) ResetMatch(); }
        public void RequestReplay() { if (State == Flow.GameOver && HasReplay) StartReplay(); }
        public bool HasReplay => _turnLog.Count > 0;

        public void PlanUndo()
        {
            if (_plans[Picker].Count == 0) return;
            _plans[Picker].RemoveAt(_plans[Picker].Count - 1);
            UpdateGhost();
        }

        public void PlanConfirm()
        {
            if (SimConfig.YomiEnabled) return; // en YOMI el turno se cierra con YomiPick
            if (_tipStage >= 1) AdvanceTip(); // cada turno confirmado avanza el tutorial
            if (Mode == GameMode.PvP && Picker == 0)
            {
                BeginHandoff(1);
                return;
            }
            if (Mode == GameMode.Async)
            {
                BeginAwaitCode();
                return;
            }
            if (Mode == GameMode.Online)
            {
                BeginAwaitOnline();
                return;
            }
            BeginExecution();
        }

        // ---------- online por sala (Supabase relay) ----------

        void BeginAwaitOnline()
        {
            _awaitingCode = true;
            _guardFrame = Time.frameCount;
            _menu.Close();
            _ghost.Clear();
            _netSeq++;
            string myCode = TurnCode.Encode(LocalSide, TurnNumber, WakeCode(LocalSide), _plans[LocalSide]);
            NetLobby.I.PushTurn(_netSeq, LocalSide, myCode);
            int seq = _netSeq;
            NetLobby.I.PollTurn(seq, 1 - LocalSide, payload => OnRemoteTurn(seq, payload));
            _hud.SetBanner($"SALA {NetLobby.I.Room}\n<size=26>esperando el turno del rival…</size>\n<size=22>M abandona la pelea</size>");
            _hud.SetPrompt($"TURNO {TurnNumber}/{TurnsPerRound} — online");
        }

        // wake como trit del protocolo: 0 quedarse · 1 rápido · 2 reversal
        int WakeCode(int i) => _wakeReversal[i] ? TurnCode.WakeReversal : _wakeQuick[i] ? TurnCode.WakeQuick : TurnCode.WakeStay;
        void ApplyWakeCode(int i, int wake)
        {
            _wakeReversal[i] = wake == TurnCode.WakeReversal;
            _wakeQuick[i] = wake == TurnCode.WakeQuick;
        }

        void OnRemoteTurn(int seq, string payload)
        {
            if (Mode != GameMode.Online || State != Flow.Planning || !_awaitingCode || seq != _netSeq) return;
            if (!TurnCode.TryDecode(payload, out int side, out int turn, out int wake, out var moves)) return;
            if (side != 1 - LocalSide || turn != (TurnNumber & 0xFF)) return;
            int remote = 1 - LocalSide;
            _plans[remote] = moves;
            ApplyWakeCode(remote, wake);
            _awaitingCode = false;
            _hud.SetBanner("");
            BeginExecution();
        }

        // ---------- online asincrónico por código ----------

        void BeginAwaitCode()
        {
            _awaitingCode = true;
            _guardFrame = Time.frameCount;
            _myCode = TurnCode.Encode(LocalSide, TurnNumber, WakeCode(LocalSide), _plans[LocalSide]);
            GUIUtility.systemCopyBuffer = _myCode;
            _menu.Close();
            _ghost.Clear();
            _hud.SetBanner($"TU CÓDIGO (ya copiado al portapapeles):\n<size=26>{_myCode}</size>\n<size=24>mandáselo al rival · copiá el suyo del chat · ESPACIO lo pega y ejecuta</size>");
            _hud.SetPrompt($"TURNO {TurnNumber} — esperando el código rival");
        }

        void TryPasteRivalCode()
        {
            string clip = (GUIUtility.systemCopyBuffer ?? "").Trim();
            int remote = 1 - LocalSide;
            if (!TurnCode.TryDecode(clip, out int side, out int turn, out int wake, out var moves))
            {
                _hud.Feedback(LocalSide, "CÓDIGO INVÁLIDO (¿copiaste el del rival?)", new Color(1f, 0.5f, 0.4f));
                return;
            }
            if (side != remote) { _hud.Feedback(LocalSide, "ESE CÓDIGO ES TUYO, falta el del rival", new Color(1f, 0.5f, 0.4f)); return; }
            if (turn != (TurnNumber & 0xFF)) { _hud.Feedback(LocalSide, $"CÓDIGO DEL TURNO {turn}, va el {TurnNumber}", new Color(1f, 0.5f, 0.4f)); return; }
            _plans[remote] = moves;
            ApplyWakeCode(remote, wake);
            _awaitingCode = false;
            _hud.SetBanner("");
            BeginExecution();
        }

        void UpdateGhost()
        {
            var basis = Sim;
            if (WakeDelta(Picker) != 0 || _wakeReversal[Picker])
            {
                basis = Sim.Clone(); // el preview arranca con el wakeup elegido
                if (_wakeReversal[Picker]) basis.Reversal(Picker);
                else basis.AdjustKnockdown(Picker, WakeDelta(Picker));
            }
            _ghost.Show(basis, Picker, _plans[Picker], CurrentTurnFrames);
            var g = PlanPreview.Build(basis, Picker, _plans[Picker], CurrentTurnFrames);
            _menu.SetPrediction(g, PlanFramesUsed(Picker), PlanFramesAvailable(Picker));
        }

        // ---------- modo YOMI v2: una acción por turno, resolución por tabla ----------

        // Franja de piso: pinta la banda de distancia actual (fría = LEJOS,
        // cálida = CERCA) entre las dos marcas — la distancia se ve en el piso.
        void UpdateYomiFloor()
        {
            if (!SimConfig.YomiEnabled || State == Flow.ModeSelect)
            {
                if (_yomiFloor != null) _yomiFloor.SetActive(false);
                return;
            }
            if (_yomiFloor == null)
            {
                _yomiFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _yomiFloor.name = "LagFighter.YomiDistanceStrip";
            }
            _yomiFloor.SetActive(true);
            bool close = Yomi != null && Yomi.Close;
            float half = close ? YomiCloseSlot : YomiFarSlot;
            _yomiFloor.transform.position = new Vector3(0f, 0.026f, 0f);
            _yomiFloor.transform.localScale = new Vector3(half * 2f + 0.7f, 0.02f, 1.62f);
            ArenaBuilder.Tint(_yomiFloor, close
                ? new Color(0.5f, 0.22f, 0.12f)    // cerca: piso caliente
                : new Color(0.12f, 0.26f, 0.42f)); // lejos: piso frío
        }

        void StartYomiPlanning()
        {
            TurnNumber++;
            for (int i = 0; i < 2; i++)
            {
                TurnStartStun[i] = 0;
                TurnStartStunKind[i] = StunKind.None;
                TurnStartCommitted[i] = 0;
                _plans[i].Clear();
            }
            Picker = 0;
            State = Flow.Planning;
            _hud.HideYomiCards(); // las cartas del turno pasado ya contaron lo suyo
            if (Yomi.Recovery[0])
            {
                // whiffeaste el Shoryu: este turno no se elige, se sufre
                _hud.Feedback(0, "RECOVERY: perdés el turno", new Color(1f, 0.5f, 0.4f));
                YomiPick(YomiAction.Recovery);
                return;
            }
            _menu.Open(0);
            UpdateYomiPrompt();
        }

        void UpdateYomiPrompt()
        {
            string dist = Yomi.Close ? "CERCA" : "LEJOS";
            string rec = Yomi.Recovery[1] ? "  ·  ¡RIVAL EN RECOVERY: golpe gratis!" : "";
            string turnLabel = $"TURNO {TurnNumber}/{TurnsPerRound}";
            if (TurnNumber >= TurnsPerRound - 2) turnLabel += " · ¡SE ACABA!";
            _hud.SetPrompt($"{turnLabel} — DISTANCIA: {dist}  ·  AP: vos {Yomi.Ap[0]} · rival {Yomi.Ap[1]}{rec}");
        }

        // El pick del jugador cierra el turno: la IA elige en secreto, la tabla
        // resuelve y el teatro lo actúa. Una acción, una lectura, un resultado.
        public void YomiPick(YomiAction act)
        {
            if (!SimConfig.YomiEnabled || State != Flow.Planning) return;
            if (!Yomi.Legal(0, act)) return;
            var aiAct = _ai.PickYomi(Yomi, 1);
            bool closeBefore = Yomi.Close;
            for (int i = 0; i < 2; i++)
            {
                _yomiHpBefore[i] = Yomi.Hp[i];
                _yomiApBefore[i] = Yomi.Ap[i];
            }
            _yomiResult = Yomi.Resolve(act, aiAct);
            _ai.ObserveYomi(act, closeBefore); // la IA aprende del pick ya revelado
            BeginYomiTheater();
        }

        // Teatro: sim de frames nueva por turno, posicionada según la
        // distancia, con los moves que actúan cada acción y retardos de
        // coreografía para que la sim reproduzca el resultado de la tabla
        // (los golpes del ganador conectan, los del perdedor se interrumpen,
        // los whiffs whiffean). El HP final lo dicta YomiSim, no la sim.
        // El FALLO del turno en una frase: qué regla de la matriz aplicó.
        // Se muestra en la revelación, ANTES de actuar — reglas primero,
        // ejecución después (pedido 2026-07-20: que el juego cante la regla).
        static string YomiRuling(YomiTurnResult r)
        {
            string N(YomiAction x) => YomiConfig.Name(x).ToUpperInvariant();
            var a = r.A0;
            var b = r.A1;
            if (r.Tech) return "AGARRE vs AGARRE: ¡TECH! nadie come";
            if (r.Parry0 || r.Parry1)
            {
                int p = r.Parry0 ? 0 : 1;
                return $"{(p == 0 ? "TU" : "SU")} PARRY bloquea el {N(r.Act(1 - p))} y devuelve 1";
            }
            if (r.Dmg0 > 0 && r.Dmg1 > 0) return $"{N(a)} y {N(b)} a la vez: ¡TRADE!";
            if (r.Dmg0 == 0 && r.Dmg1 == 0)
            {
                if (r.Rec0Next && r.Rec1Next) return "doble SHORYU al aire: los dos en RECOVERY";
                if (r.Rec0Next || r.Rec1Next) return $"¡SHORYU al aire! {(r.Rec0Next ? "quedás" : "queda")} en RECOVERY";
                if (a == YomiAction.Charge && b == YomiAction.Charge) return "los dos CARGAN: +2 AP cada uno";
                if (r.CloseBefore != r.CloseAfter) return r.CloseAfter ? "nadie conecta → CERCA" : "nadie conecta → LEJOS";
                return "nadie conecta";
            }
            int w = r.Dmg0 > 0 ? 1 : 0; // el que pegó
            var wa = r.Act(w);
            var la = r.Act(1 - w);
            string who = w == 0 ? "GANÁS VOS" : "GANA EL RIVAL";
            string reason;
            if (la == YomiAction.Charge) reason = $"{N(wa)} lo caza CARGANDO: counter";
            else if (la == YomiAction.Recovery) reason = $"{N(wa)} castiga el RECOVERY: counter";
            else if (wa == YomiAction.Shoryu) reason = r.CloseBefore ? "SHORYU le gana a TODO de cerca" : "¡lectura antiaérea! SHORYU baja al salto";
            else if (wa == YomiAction.Jab && la == YomiAction.Kick) reason = "JAB es más rápido que KICK";
            else if (wa == YomiAction.Jab && la == YomiAction.Grab) reason = "el golpe interrumpe al AGARRE";
            else if (wa == YomiAction.Jab && la == YomiAction.Jump) reason = "JAB lo baja en el despegue";
            else if (wa == YomiAction.Kick && la == YomiAction.Grab) reason = "el golpe interrumpe al AGARRE";
            else if (wa == YomiAction.Kick && la == YomiAction.Dash) reason = r.CloseBefore ? "KICK caza la retirada" : "KICK frena la entrada";
            else if (wa == YomiAction.Kick && la == YomiAction.Shoryu) reason = "KICK castiga el SHORYU al aire";
            else if (wa == YomiAction.Jump && la == YomiAction.Kick) reason = "SALTO pasa por ARRIBA del kick";
            else if (wa == YomiAction.Jump && la == YomiAction.Dash) reason = "la patada de entrada caza al DASH";
            else if (wa == YomiAction.Grab && la == YomiAction.Parry) reason = "AGARRE rompe el PARRY y tira";
            else reason = $"{N(wa)} le gana a {N(la)}";
            return $"{who}: {reason}";
        }

        void BeginYomiTheater()
        {
            var r = _yomiResult;
            _menu.Close();
            _ghost.Clear();
            _yomiShowLiveAp = false; // los circulitos muestran el AP de arranque hasta cerrar el turno
            float keepX0 = Sim.Fighters[0].X, keepX1 = Sim.Fighters[1].X; // continuidad: sin teleports
            Sim = new MatchSim();
            for (int i = 0; i < 2; i++)
            {
                var f = Sim.Fighters[i];
                f.BlockEnabled = false;
                f.X = i == 0 ? keepX0 : keepX1; // durante la revelación caminan a su marca
                f.PrevX = f.X;
                f.Hp = _yomiHpBefore[i];
            }
            for (int i = 0; i < 2; i++)
            {
                int mv = TheaterMove(r, i);
                if (mv >= 0) Sim.SetQueue(i, new List<int> { mv });
                // retardo de coreografía INVISIBLE: espera en neutral, sin stun falso
                Sim.Fighters[i].QueueDelayTick = TheaterDelay(r, i);
            }
            TurnStartTick = Sim.Tick;
            _acc = 0f;
            _turnDmg[0] = _turnDmg[1] = 0f;
            _turnHitCount[0] = _turnHitCount[1] = 0;
            State = Flow.Executing;
            // fase de REVELACIÓN: las dos cartas gigantes, 2.4s de "esto eligió
            // cada uno" antes de que pase nada (espacio o click la apuran)
            _yomiRevealTimer = YomiRevealSeconds;
            _hud.ShowYomiReveal(r.A0, r.A1, r.CloseBefore, YomiRuling(r));
            _hud.SetPrompt($"TURNO {TurnNumber} — {YomiConfig.Name(r.A0).ToUpperInvariant()} vs {YomiConfig.Name(r.A1).ToUpperInvariant()}");
            SfxLib.Play(SfxLib.Kind.TurnStart, 0.6f);
        }

        static int TheaterMove(YomiTurnResult r, int i)
        {
            bool close = r.CloseBefore;
            switch (r.Act(i))
            {
                case YomiAction.Jab: return MoveCatalog.AttackA;
                case YomiAction.Kick: return close ? MoveCatalog.Strong : MoveCatalog.StrongFar;
                case YomiAction.Grab: return MoveCatalog.YomiGrab;
                case YomiAction.Parry: return MoveCatalog.Parry;
                case YomiAction.Shoryu: return MoveCatalog.Shoryuken;
                case YomiAction.Dash: return close ? MoveCatalog.DashB : MoveCatalog.DashInFar;
                case YomiAction.Jump: return close ? MoveCatalog.JumpB : MoveCatalog.JumpInFar;
                default: return -1; // Cargar / Recovery: quieto
            }
        }

        // primer frame de contacto del move teatral (−1 = no pega)
        static int TheaterContact(YomiAction a, bool close)
        {
            switch (a)
            {
                case YomiAction.Jab: return 6;
                case YomiAction.Kick: return 14;
                case YomiAction.Grab: return 9;
                case YomiAction.Shoryu: return 4;
                case YomiAction.Jump: return close ? -1 : 26; // la patada de entrada llega tarde (cruza 3.4)
                default: return -1;
            }
        }

        int TheaterDelay(YomiTurnResult r, int i)
        {
            int o = 1 - i;
            var mine = r.Act(i);
            var theirs = r.Act(o);
            bool close = r.CloseBefore;
            int myC = TheaterContact(mine, close), theirC = TheaterContact(theirs, close);
            // parry exitoso: alinear la ventana f3-7 con el contacto del atacante
            if (r.Parried(i) && theirC >= 0) return Mathf.Max(0, theirC - 5);
            // shoryu antiaéreo de lejos: esperar a que el salto llegue encima
            if (mine == YomiAction.Shoryu && !close && theirs == YomiAction.Jump) return 22;
            // me pegaron y yo también tiraba: que el golpe rival entre primero
            if (r.Dmg(i) > 0 && myC >= 0 && theirC >= 0) return Mathf.Max(0, theirC + 8 - myC);
            // me cazaron moviéndome (kick al dash, jab al salto): frená un toque
            if (r.Dmg(i) > 0 && (mine == YomiAction.Dash || mine == YomiAction.Jump)) return 6;
            // mi golpe whiffeó porque el rival se fue: tirarlo tarde, que se VEA el whiff
            if (myC >= 0 && r.Dmg(o) == 0 && !r.Parried(o) &&
                (theirs == YomiAction.Dash || theirs == YomiAction.Jump)) return 18;
            return 0;
        }

        void TickYomiTheater()
        {
            // revelación: las cartas al frente; los peleadores CAMINAN a su
            // marca de distancia (así el cambio cerca/lejos se ve, sin teleport)
            if (_yomiRevealTimer > 0f)
            {
                _yomiRevealTimer -= Time.deltaTime;
                float slot = _yomiResult.CloseBefore ? YomiCloseSlot : YomiFarSlot;
                for (int i = 0; i < 2; i++)
                {
                    var f = Sim.Fighters[i];
                    f.X = Mathf.MoveTowards(f.X, i == 0 ? -slot : slot, 3.2f * Time.deltaTime);
                    f.PrevX = f.X;
                }
                // apurarla con espacio/click (no el mismo click que eligió la carta)
                if (_yomiRevealTimer < YomiRevealSeconds - 0.35f &&
                    (GameInput.EndTurnPressed() || GameInput.ClickPressed()))
                    _yomiRevealTimer = 0f;
                if (_yomiRevealTimer <= 0f)
                {
                    // que la acción arranque desde las marcas exactas
                    for (int i = 0; i < 2; i++)
                    {
                        var f = Sim.Fighters[i];
                        f.X = i == 0 ? -slot : slot;
                        f.PrevX = f.X;
                    }
                    _hud.DockYomiCards();
                }
                return;
            }
            if (_hitstop > 0f) { _hitstop -= Time.deltaTime * PlaybackSpeed; return; }
            _acc += Time.deltaTime * PlaybackSpeed * YomiTheaterSpeed;
            while (_acc >= SimConfig.TickDuration)
            {
                _acc -= SimConfig.TickDuration;
                Sim.Step();
                DispatchEvents();
                if (Sim.Over || Sim.Tick - TurnStartTick >= YomiTheaterFrames)
                {
                    _acc = 0f;
                    EndYomiTurn();
                    return;
                }
                if (_hitstop > 0f) return;
            }
        }

        void EndYomiTurn()
        {
            var r = _yomiResult;
            // el HP y los AP reales los dicta la tabla; recién ahora se muestran
            for (int i = 0; i < 2; i++) Sim.Fighters[i].Hp = Yomi.Hp[i];
            _yomiShowLiveAp = true;
            if (!Yomi.Over) { Sim.Over = false; Sim.Winner = -1; } // el teatro no decide KOs
            for (int i = 0; i < 2; i++)
            {
                if (r.Dmg(i) > 0) _hud.Feedback(i, $"−{r.Dmg(i)} HP", new Color(1f, 0.42f, 0.35f));
                if (r.Charged(i)) _hud.Feedback(i, "+2 AP", new Color(1f, 0.85f, 0.3f));
                if (r.Act(i) == YomiAction.Charge && r.Dmg(i) > 0)
                    _hud.Feedback(i, "CARGA CANCELADA", new Color(0.8f, 0.8f, 0.85f));
                if (r.Parried(i)) _hud.Feedback(i, "¡PARRY! +1 AP y devuelve 1", new Color(0.3f, 0.95f, 1f));
                if (r.RecNext(i)) _hud.Feedback(i, "¡WHIFF! Recovery: pierde el turno", new Color(1f, 0.5f, 0.3f));
                if (i == 0 ? r.Counter0 : r.Counter1) _hud.Feedback(i, "¡COUNTER!", new Color(1f, 0.55f, 0.15f));
            }
            // cambio de distancia: cantarlo y repintar la franja del piso
            if (r.CloseBefore != r.CloseAfter)
                _hud.ShowBigMessage(r.CloseAfter ? "→ CERCA" : "→ LEJOS", new Color(0.5f, 0.85f, 1f), 1f);
            UpdateYomiFloor();
            _hud.AddTurnLog($"T{TurnNumber}  <color=#8cc8ff>{YomiConfig.Chip(r.A0)}</color> −{r.Dmg1}  ·  <color=#ffa080>{YomiConfig.Chip(r.A1)}</color> −{r.Dmg0}");
            _hud.SetTurnSummary($"último turno: {YomiConfig.Name(r.A0)} vs {YomiConfig.Name(r.A1)} — vos −{r.Dmg0} · rival −{r.Dmg1}");

            if (Yomi.Over)
            {
                Sim.Over = true;
                Sim.Winner = Yomi.Winner;
                _koTimer = 1.4f;
                Time.timeScale = 0.25f; // el golpe del KO, bien en cámara lenta
                _hud.ShowBigMessage("K.O.", new Color(1f, 0.3f, 0.25f));
                Announcer.Play();
                return; // _koTimer → BeginRoundReplay → (log frame vacío) → OnRoundEnd
            }
            if (TurnNumber >= TurnsPerRound)
            {
                _hud.ShowBigMessage("TIME OVER", new Color(1f, 0.85f, 0.3f));
                SfxLib.Play(SfxLib.Kind.Ko, 0.6f);
                OnRoundEnd();
                return;
            }
            StartPlanning();
        }

        // ---------- modo DUELO (el núcleo casual) ----------

        // Con el teatro apagado el escenario tiene que quedar LIMPIO: sin
        // peleadores y sin fantasma. Se refuerza por frame a propósito —
        // apagarlos una sola vez al entrar al modo dejaba pasar cualquier
        // camino que los vuelva a encender, y un blockman suelto detrás de
        // las cartas es exactamente el ruido que había que sacar.
        void EnforceDuelStage()
        {
            if (!SimConfig.DuelEnabled || SimConfig.DuelTheaterEnabled) return;
            for (int i = 0; i < 2; i++)
                if (_views[i] != null && _views[i].gameObject.activeSelf)
                    _views[i].gameObject.SetActive(false);
            if (_ghost != null) _ghost.Clear();
        }


        // Los pips del HUD son 6 y acá la vida es 46-54: proporcionales.
        // Los números exactos viven en el panel de DuelHudUI.
        float DuelTheaterHp(int i) => Duel.Hp[i] * (float)SimConfig.MaxHp / Duel.MaxHpOf(i);

        void StartDuelPlanning()
        {
            TurnNumber++;
            for (int i = 0; i < 2; i++)
            {
                TurnStartStun[i] = 0;
                TurnStartStunKind[i] = StunKind.None;
                TurnStartCommitted[i] = 0;
                _plans[i].Clear();
            }
            Duel.StartTurn();
            if (Duel.Over) { DuelTimeOver(); return; }
            _duelCombatOn = false;
            Picker = 0;
            State = Flow.Planning;
            _duelHud.HideReveal();
            if (SimConfig.DuelTheaterEnabled) PoseDuelPlanning();
            _duelHand.Open(DuelHandUI.Mode.Pick);
            UpdateDuelPrompt();
        }

        // El derribo se VE: mientras elegís carta, el derribado sigue en el
        // piso. Y el que perdió el intercambio sigue tambaleado, y el que ganó
        // sigue con el brazo afuera.
        //
        // Eso es EL cambio del teatro nuevo: acá NO se resetea nada a idle. La
        // consecuencia del turno anterior es la escenografía del turno que
        // estás pensando (DUELO-LOOK §5.3, el tercer tiempo).
        void PoseDuelPlanning()
        {
            for (int i = 0; i < 2; i++)
            {
                if (_views[i] == null) continue;
                _views[i].PlaceDuel(i == 0 ? -DuelSlot : DuelSlot);
                _views[i].SetDuelBuild(Duel.CharIdx[i]);
                _views[i].ClearDuelMarks();
                if (Duel.KnockedDown[i]) _views[i].SetDuelPose(DuelPose.Down, restart: false);
            }
        }

        // La pose que le toca a una carta. La regla de oro sigue siendo la
        // misma: la ALTURA de la carta y la altura del golpe en pantalla tienen
        // que coincidir — el mixup se aprende mirando, no leyendo.
        DuelPose DuelPoseOf(int side, int card)
        {
            if (card < 0) return DuelPose.Idle;
            var d = Duel.Def(side, card);
            switch (d.Kind)
            {
                case DuelKind.Guard:
                    return d.Height == DuelHeight.High ? DuelPose.GuardHigh : DuelPose.GuardLow;
                case DuelKind.Grab: return DuelPose.Grab;
                case DuelKind.Escape: return DuelPose.Escape;
                default:
                    return d.Height == DuelHeight.High ? DuelPose.StrikeHigh : DuelPose.StrikeLow;
            }
        }

        bool DuelCardHigh(int side, int card) =>
            card >= 0 && Duel.Def(side, card).Height == DuelHeight.High;

        void UpdateDuelPrompt()
        {
            string kd = Duel.KnockedDown[0] ? "  ·  ESTÁS DERRIBADO: tu guardia no bloquea"
                : Duel.KnockedDown[1] ? "  ·  RIVAL DERRIBADO: su guardia no bloquea" : "";
            _hud.SetPrompt($"TURNO {TurnNumber} — elegí tu carta{kd}");
        }

        // El pick del humano cierra el turno: la IA ya eligió en secreto, la
        // tabla resuelve y recién después se actúa.
        public void DuelPick(int handIdx)
        {
            if (!SimConfig.DuelEnabled || State != Flow.Planning || _duelCombatOn) return;
            if (handIdx < 0 || handIdx >= Duel.Hand[0].Count) return;
            for (int i = 0; i < 2; i++) _duelHpBefore[i] = Duel.Hp[i];
            int aiIdx = _ai.PickDuelCard(Duel, 1);
            _duelResult = Duel.Resolve(handIdx, aiIdx);
            _duelCombatOn = true;
            ContinueDuelTurn();
        }

        void ContinueDuelTurn()
        {
            if (Duel.AwaitingChoice && Duel.PendingSide == 1) _ai.DoDuelChoice(Duel);
            if (Duel.AwaitingChoice && Duel.PendingSide == 0)
            {
                // La ceremonia va IGUAL: primero ves por qué ganaste, después
                // elegís el premio. Saltearla acá era regalarse el momento.
                StartDuelReveal(Duel.LastResult, awaitChoice: true);
                return;
            }
            _duelResult = Duel.LastResult;
            BeginDuelTheater();
        }

        public void DuelChoosePrize(DuelPrize prize, int handIdx)
        {
            if (!_duelCombatOn || !Duel.AwaitingChoice || Duel.PendingIsPunish) return;
            if (!Duel.ChoosePrize(prize, handIdx)) { SfxLib.Play(SfxLib.Kind.UiCancel, 0.4f); return; }
            _duelResult = Duel.LastResult;
            BeginDuelTheater();
        }

        public void DuelPunish(int handIdx)
        {
            if (!_duelCombatOn || !Duel.AwaitingChoice || !Duel.PendingIsPunish) return;
            if (!Duel.Punish(handIdx)) { SfxLib.Play(SfxLib.Kind.UiCancel, 0.4f); return; }
            _duelResult = Duel.LastResult;
            BeginDuelTheater();
        }

        // ---- teatro: que se VEA lo que dijo la tabla ----
        //
        // TRES TIEMPOS, no noventa frames (DUELO-LOOK §5.3):
        //   1. ANTICIPACIÓN — los dos se cargan A LA VEZ (el reveal es
        //      simultáneo: acá no hay jugador activo ni turnos de iniciativa).
        //   2. IMPACTO — hitstop, flash, y la ZONA golpeada se enciende.
        //   3. CONSECUENCIA — la pose queda hasta la próxima revelación.
        //
        // El tiempo 3 es todo el cambio conceptual: el teatro viejo era un
        // video que se reproducía y se olvidaba; esto es una foto que se
        // actualiza.
        const float DuelBeatCoil = 0.34f;    // dura la anticipación
        const float DuelBeatHold = 0.30f;    // impacto + hitstop
        const float DuelBeatRead = 1.30f;    // tiempo para leer antes de seguir

        // Arranca la ceremonia de revelación. awaitChoice: al terminar no se
        // cierra el turno — se abre la decisión del premio o del castigo.
        void StartDuelReveal(DuelTurnResult r, bool awaitChoice)
        {
            _duelHand.SetDimmed(true);   // se apaga, no desaparece
            _menu.Close();
            _ghost.Clear();
            TurnStartTick = Sim.Tick;
            _acc = 0f;
            _turnDmg[0] = _turnDmg[1] = 0f;
            _turnHitCount[0] = _turnHitCount[1] = 0;
            State = Flow.Executing;
            _duelPostReveal = 0f;
            _duelAwaitChoice = awaitChoice;
            _duelHud.ShowReveal(r);
            SfxLib.Play(SfxLib.Kind.TurnStart, 0.6f);
        }

        void BeginDuelTheater()
        {
            StartDuelReveal(_duelResult, awaitChoice: false);
            _duelBeat = 0f;
            _duelStage = 0;
        }

        // tiempo 1: los dos cargados, sin decir todavía qué tiraron
        void DuelBeatCoiled()
        {
            for (int i = 0; i < 2; i++)
            {
                if (_views[i] == null) continue;
                _views[i].PlaceDuel(i == 0 ? -DuelSlot : DuelSlot);
                _views[i].ClearDuelMarks();
                // el derribado no se carga: está en el piso
                if (!Duel.KnockedDown[i] || !_duelWasDown[i])
                    _views[i].SetDuelPose(DuelPose.Coiled);
            }
        }

        // tiempo 2: se actúa el verbo y PEGA — flash, zona encendida, hitstop
        void DuelBeatImpact()
        {
            var r = _duelResult;
            for (int i = 0; i < 2; i++)
            {
                if (_views[i] == null) continue;
                if (_duelWasDown[i] && r.Card(i) >= 0 && Duel.Def(i, r.Card(i)).Kind == DuelKind.Guard)
                    continue;   // derribado con guardia: ni se levanta a cubrirse
                _views[i].SetDuelPose(DuelPoseOf(i, r.Card(i)));
            }

            for (int i = 0; i < 2; i++)
            {
                if (_views[i] == null) continue;
                bool attackerHigh = DuelCardHigh(1 - i, r.Card(1 - i));

                // la guardia es una PLACA sobre la mitad que cubrió; si erró,
                // se parte y el golpe pasa por el otro lado
                if (r.Card(i) >= 0 && Duel.Def(i, r.Card(i)).Kind == DuelKind.Guard)
                {
                    bool guardHigh = Duel.Def(i, r.Card(i)).Height == DuelHeight.High;
                    _views[i].ShowGuardPlate(true, guardHigh, broken: r.WrongGuard(i) || _duelWasDown[i]);
                }

                if (r.Guarded(i)) _views[i].FlashZone(attackerHigh, blocked: true);

                if (r.Dmg(i) > 0)
                {
                    _views[i].FlashZone(attackerHigh);
                    _views[i].FlashHit(counter: r.Dmg(i) >= 8);
                    SparkFX.Burst(new Vector3((i == 0 ? -DuelSlot : DuelSlot) + (i == 0 ? 0.4f : -0.4f),
                        attackerHigh ? 1.45f : 0.6f, -0.3f),
                        new Color(1f, 0.6f, 0.3f), Mathf.Clamp(r.Dmg(i) * 2, 6, 20), 3.2f);
                }
            }

            if (r.Dmg(0) > 0 || r.Dmg(1) > 0)
            {
                _hitstop = 0.14f;
                SfxLib.Play(SfxLib.Kind.Hit, 0.9f);
                CamFx()?.Shake(0.14f);
            }
            if (r.Tech || r.Trade) SfxLib.Play(SfxLib.Kind.Block, 0.8f);
        }

        // tiempo 3: la CONSECUENCIA, que se queda hasta la próxima revelación
        void DuelBeatConsequence()
        {
            var r = _duelResult;
            for (int i = 0; i < 2; i++)
            {
                if (_views[i] == null) continue;
                DuelPose p;
                if (Duel.KnockedDown[i]) p = DuelPose.Down;
                else if (r.WrongGuard(i)) p = DuelPose.GuardBroken;
                else if (r.Dmg(i) > 0) p = DuelPose.Hurt;
                else if (r.Guarded(i)) p = DuelPoseOf(i, r.Card(i));   // sostiene la guardia buena
                else if (r.Winner == i) p = DuelPose.Win;
                else p = DuelPose.Idle;
                _views[i].SetDuelPose(p);
            }
        }

        string DuelName(int side, int card) =>
            card < 0 ? "SIN CARTAS" : Duel.Def(side, card).Name.ToUpperInvariant();

        void TickDuelTheater()
        {
            // La ceremonia de las cartas ES el turno. Se puede apurar.
            if (!_duelHud.RevealFinished)
            {
                if (GameInput.EndTurnPressed() || GameInput.ClickPressed()) _duelHud.SkipReveal();
                return;
            }
            if (_duelAwaitChoice)
            {
                // ya viste el fallo: ahora sí, elegí
                _duelAwaitChoice = false;
                State = Flow.Planning;
                _duelHand.Open(Duel.PendingIsPunish ? DuelHandUI.Mode.Punish : DuelHandUI.Mode.Prize);
                return;
            }
            if (!SimConfig.DuelTheaterEnabled)
            {
                // un respiro para leer el veredicto y al turno siguiente
                _duelPostReveal += Time.deltaTime;
                if (_duelPostReveal >= 0.7f || GameInput.EndTurnPressed() || GameInput.ClickPressed())
                {
                    _duelPostReveal = 0f;
                    EndDuelTurn();
                }
                return;
            }

            // ---- los tres tiempos ----
            if (_hitstop > 0f) { _hitstop -= Time.deltaTime; return; }
            _duelBeat += Time.deltaTime;
            bool skip = GameInput.EndTurnPressed() || GameInput.ClickPressed();

            if (_duelStage == 0) { DuelBeatCoiled(); _duelStage = 1; return; }
            if (_duelStage == 1 && _duelBeat >= DuelBeatCoil)
            {
                DuelBeatImpact();
                _duelStage = 2;
                return;
            }
            if (_duelStage == 2 && _duelBeat >= DuelBeatCoil + DuelBeatHold)
            {
                DuelBeatConsequence();
                _duelStage = 3;
                return;
            }
            // el tiempo 3 NO se desarma al terminar: la pose sigue en pantalla
            // durante toda la planificación del turno siguiente.
            if (_duelStage == 3 && (_duelBeat >= DuelBeatCoil + DuelBeatHold + DuelBeatRead || skip))
                EndDuelTurn();
        }

        void EndDuelTurn()
        {
            var r = _duelResult;
            if (SimConfig.DuelTheaterEnabled)
            {
                for (int i = 0; i < 2; i++) Sim.Fighters[i].Hp = DuelTheaterHp(i);
                if (!Duel.Over) { Sim.Over = false; Sim.Winner = -1; }
                // quién ESTABA en el piso, para que el próximo teatro sepa que
                // este no se levanta a cubrirse
                for (int i = 0; i < 2; i++) _duelWasDown[i] = Duel.KnockedDown[i];
            }

            for (int i = 0; i < 2; i++)
            {
                int real = r.Dmg(i) - r.Chip(i);
                if (real > 0) _hud.Feedback(i, $"−{real} HP", new Color(1f, 0.42f, 0.35f));
                if (r.Chip(i) > 0) _hud.Feedback(i, $"−{r.Chip(i)} CHIP", new Color(1f, 0.75f, 0.4f));
                if (r.Guarded(i)) _hud.Feedback(i, $"¡DEFENDIDO! +{r.Drew(i)} cartas", new Color(0.35f, 0.85f, 1f));
                if (r.WrongGuard(i)) _hud.Feedback(i, "¡ALTURA EQUIVOCADA!", new Color(1f, 0.55f, 0.15f));
                if (r.KdNext(i)) _hud.Feedback(i, "¡DERRIBADO!", new Color(1f, 0.6f, 0.4f));
                if (r.Returned(i)) _hud.Feedback(i, "la guardia VUELVE a tu mano", new Color(0.7f, 0.95f, 0.7f));

                // lo que le pasó a i se lo hizo el otro: es la estadística de
                // la pantalla de resultados
                _duelDmgDealt[1 - i] += r.Dmg(i);
                if (r.Guarded(i)) _duelGuardsHit[i]++;
                if (r.KdNext(i)) _duelKdsDealt[1 - i]++;
            }
            if (r.Armor) _hud.Feedback(0, "¡AGUANTE! el agarre entra igual", new Color(0.95f, 0.7f, 1f));
            else if (r.Trade) _hud.Feedback(0, "TRADE: misma velocidad", new Color(1f, 0.85f, 0.4f));
            if (r.Tech) _hud.Feedback(0, "TECH", new Color(0.8f, 0.7f, 1f));
            if (r.PrizeSide >= 0 && r.Prize == DuelPrize.Damage)
                _hud.Feedback(r.PrizeSide, $"+DAÑO {r.PrizeDamage}", new Color(1f, 0.8f, 0.35f));
            if (r.PunishSide >= 0)
                _hud.Feedback(r.PunishSide, $"CASTIGO: {DuelName(r.PunishSide, r.PunishCard)}", new Color(1f, 0.55f, 0.9f));

            // el derribo queda EN PANTALLA hasta que el derribado juegue: lo
            // sostiene la pose Down, que no se resetea al planificar

            string s0 = r.Card0 >= 0 ? Duel.Def(0, r.Card0).Short : "—";
            string s1 = r.Card1 >= 0 ? Duel.Def(1, r.Card1).Short : "—";
            _hud.AddTurnLog($"T{TurnNumber}  <color=#8cc8ff>{s0}</color> −{r.Dmg1}  ·  <color=#ffa080>{s1}</color> −{r.Dmg0}");
            // (nada de "último turno: X vs Y": en DUELO ese recap es la
            // REVELACIÓN, con las dos cartas dockeadas y el veredicto cantado.
            // La línea del clásico decía lo mismo peor y a veces se contradecía
            // con el veredicto, que es el peor error que puede cometer un HUD.)

            if (Duel.Over)
            {
                Sim.Over = true;
                Sim.Winner = Duel.Winner;
                if (Duel.Winner >= 0) _wins[Duel.Winner] = RoundsToWin - 1;
                if (Duel.Hp[0] <= 0 || Duel.Hp[1] <= 0)
                {
                    _koTimer = 1.4f;
                    Time.timeScale = 0.25f;
                    _hud.ShowBigMessage("K.O.", new Color(1f, 0.3f, 0.25f));
                    Announcer.Play();
                    return;
                }
                DuelTimeOver();
                return;
            }
            StartPlanning();
        }

        // ---- la ceremonia de cierre (DUELO-LOOK §7) ----
        // Una partida de DUELO ES el match: no hay replay de round que valga.
        // Se cierra con la pantalla de resultados, que además es donde el
        // jugador ve por primera vez cómo peleó, no solo si ganó.
        void ShowDuelResults()
        {
            State = Flow.GameOver;
            _duelHand.Close();
            _duelHud.HideReveal();
            _duelHud.ShowResults(Duel.Winner, TurnNumber, _duelDmgDealt, _duelGuardsHit, _duelKdsDealt);
        }

        public void DuelRematch()
        {
            _duelHud.HideResults();
            ResetMatch();
        }

        public void DuelToMenu()
        {
            _duelHud.HideResults();
            GoToModeSelect();
        }

        void DuelTimeOver()
        {
            State = Flow.GameOver;
            Sim.Over = true;
            Sim.Winner = Duel.Winner;
            if (Duel.Winner >= 0) _wins[Duel.Winner] = RoundsToWin - 1;
            _duelHand.Close();
            _duelHud.ShowResults(Duel.Winner, TurnNumber, _duelDmgDealt, _duelGuardsHit, _duelKdsDealt,
                timeOver: true);
        }

        // ---------- modo CARTAS v2 (copia completa de Yomi 2) ----------

        // HP del teatro: los pips del HUD son 6 y acá el HP es 85-90 —
        // proporcionales; los números exactos van en los paneles del HUD.
        float CardsTheaterHp(int i) => Cards.Hp[i] * (float)SimConfig.MaxHp / Cards.Chr[i].MaxHp;

        void StartCardsPlanning()
        {
            TurnNumber++;
            for (int i = 0; i < 2; i++)
            {
                TurnStartStun[i] = 0;
                TurnStartStunKind[i] = StunKind.None;
                TurnStartCommitted[i] = 0;
                _plans[i].Clear();
            }
            Cards.StartTurn(); // el activo roba 2 (1 el primer turno)
            if (Cards.Over) { CardsTimeOver(); return; }
            if (Cards.Active == 1) _ai.DoCardMainPhase(Cards); // su main phase entera
            _cardsCombatOn = false;
            Picker = 0;
            State = Flow.Planning;
            _hud.HideYomiCards();
            if (Cards.Hand[0].Count == 0)
            {
                _hud.Feedback(0, "SIN CARTAS: wild swing", new Color(1f, 0.8f, 0.3f));
                CardsPick(-1);
                return;
            }
            _hand.Open(CardHandUI.Mode.Opener);
            UpdateCardsPrompt();
        }

        public void UpdateCardsPrompt()
        {
            bool mine = Cards.Active == 0;
            string kd = Cards.KnockedDown[0] ? "  ·  ¡DERRIBADO! (sin esquive, sus golpes a speed 10)"
                : Cards.KnockedDown[1] ? "  ·  RIVAL DERRIBADO (derribado no esquiva y tus golpes suben a 10)" : "";
            _hud.SetPrompt($"TURNO {TurnNumber} — {(mine ? "TU TURNO (main phase + opener)" : "TURNO RIVAL (gana los empates)")}{kd}");
        }

        // ---- acciones de la MAIN PHASE del humano (vía CardHandUI) ----

        public void CardsPlayAbility(int handIdx)
        {
            // solo en TU main phase: la sim opera sobre la mano del ACTIVO
            if (!CardsPlanningOpen() || Cards.Active != 0 || !Cards.PlayAbility(handIdx)) return;
            _hud.Feedback(0, Cards.Chr[0].Cards[CardCatalog.Ability].Name.ToUpperInvariant() + " ACTIVA (2 combates)",
                new Color(0.6f, 0.95f, 1f));
            SfxLib.Play(SfxLib.Kind.TurnStart, 0.5f);
            _hand.Open(CardHandUI.Mode.Opener);
        }

        public bool CardsExchange(int handIdx, int discardIdx)
        {
            if (!CardsPlanningOpen() || Cards.Active != 0) return false;
            if (!Cards.Exchange(handIdx, discardIdx)) return false;
            SfxLib.Play(SfxLib.Kind.UiClick, 0.7f);
            _hand.Open(CardHandUI.Mode.Opener);
            return true;
        }

        public bool CardsPowerUp(int handIdxA, int handIdxB, bool fetchSuper, int superCard)
        {
            if (!CardsPlanningOpen() || Cards.Active != 0) return false;
            if (!Cards.PowerUp(handIdxA, handIdxB, fetchSuper, superCard)) return false;
            _hud.Feedback(0, fetchSuper ? "PODER: super a la mano · +1 ★" : "PODER: +2 ★",
                new Color(1f, 0.85f, 0.3f));
            SfxLib.Play(SfxLib.Kind.UiClick, 0.9f);
            _hand.Open(CardHandUI.Mode.Opener);
            return true;
        }

        bool CardsPlanningOpen() =>
            SimConfig.CardsEnabled && State == Flow.Planning && !_cardsCombatOn && !Cards.Over;

        // ---- combate ----

        // El opener del humano cierra la elección: la IA ya eligió en secreto,
        // la tabla resuelve y el followup (combo/castigo) se decide ANTES de
        // que el teatro lo actúe todo junto.
        public void CardsPick(int handIdx)
        {
            if (!SimConfig.CardsEnabled || State != Flow.Planning || _cardsCombatOn) return;
            if (handIdx >= Cards.Hand[0].Count) return;
            if (handIdx < 0 && Cards.Hand[0].Count > 0) return;
            if (handIdx >= 0 && !Cards.LegalOpener(0, handIdx) && Cards.HasLegalOpener(0)) return;
            for (int i = 0; i < 2; i++) _cardsHpBefore[i] = Cards.Hp[i];
            int aiIdx = _ai.PickCardOpener(Cards, 1);
            _cardsResult = Cards.Resolve(handIdx, aiIdx);
            _cardsCombatOn = true;
            ContinueCardsCombat();
        }

        // Rutea el followup pendiente hasta que el combate cierra del todo.
        void ContinueCardsCombat()
        {
            if (Cards.AwaitingFollowup && Cards.FollowSide == 1)
            {
                _ai.DoCardFollowup(Cards); // la IA decide combo/pump/castigo
            }
            if (Cards.AwaitingFollowup && Cards.FollowSide == 0)
            {
                // decisión del humano: las cartas reveladas quedan a la vista
                var r = Cards.LastResult;
                string hint = Cards.FollowIsHitBack
                    ? "te dieron la ventana: ¡CASTIGO!"
                    : "¡conectaste! armá el combo";
                _hud.ShowCardsReveal(Cards, r.Card0, r.Card1, hint);
                _hud.DockYomiCards();
                _hand.Open(Cards.FollowIsHitBack ? CardHandUI.Mode.Punish : CardHandUI.Mode.Combo);
                UpdateCardsPrompt();
                return;
            }
            _cardsResult = Cards.LastResult;
            FinishCardsTurn();
        }

        // ---- decisiones del followup humano (vía CardHandUI) ----

        public void CardsComboAdd(int handIdx)
        {
            if (!_cardsCombatOn || !Cards.AwaitingFollowup) return;
            if (Cards.ComboAdd(handIdx)) SfxLib.Play(SfxLib.Kind.Hit, 0.5f);
            AfterHumanFollow();
        }

        public void CardsPump()
        {
            if (!_cardsCombatOn || !Cards.AwaitingFollowup) return;
            if (Cards.PumpLast(9)) SfxLib.Play(SfxLib.Kind.Counter, 0.6f);
            AfterHumanFollow();
        }

        public void CardsComboEnd()
        {
            if (!_cardsCombatOn) return;
            if (Cards.AwaitingFollowup) Cards.FollowupEnd();
            AfterHumanFollow();
        }

        public void CardsPunish(int handIdx)
        {
            if (!_cardsCombatOn || !Cards.AwaitingFollowup) return;
            Cards.HitBack(handIdx);
            AfterHumanFollow();
        }

        void AfterHumanFollow()
        {
            if (Cards.AwaitingFollowup)
            {
                // sigue abierta la decisión (más combo, o el pump del castigo)
                _hand.Open(Cards.FollowIsHitBack ? CardHandUI.Mode.Punish : CardHandUI.Mode.Combo);
                return;
            }
            _cardsResult = Cards.LastResult;
            FinishCardsTurn();
        }

        void FinishCardsTurn()
        {
            _ai.ObserveCard(Cards.Def(0, _cardsResult.Card0).Kind);
            BeginCardsTheater();
        }

        // El FALLO del combate en una frase (con el combo y el castigo).
        string CardsRuling(CardTurnResult r)
        {
            string N(int side, int c) => Cards.Def(side, c).Name.ToUpperInvariant();
            var d0 = Cards.Def(0, r.Card0);
            var d1 = Cards.Def(1, r.Card1);
            string tail = "";
            for (int i = 0; i < 2; i++)
                if (r.Combo(i).Count > 0)
                    tail += $" · COMBO ×{r.Combo(i).Count + 1}";
            if (r.PumpExtra0 + r.PumpExtra1 > 0) tail += " · PUMP";
            if (r.SuperCounter >= 0)
                return (r.SuperCounter == 0 ? "TU" : "SU") + $" SUPER ESQUIVE devuelve 40{tail}";
            if (r.ProjCancel) return "proyectiles del MISMO NIVEL: se anulan";
            if (r.Blocked0 || r.Blocked1)
            {
                int b = r.Blocked0 ? 0 : 1;
                var atk = Cards.Def(1 - b, r.Card(1 - b));
                string who = b == 0 ? "BLOQUEÁS" : "BLOQUEA";
                string extra = atk.BlockDamage > 0 ? $" (chip)" : "";
                if (r.HitBackCard >= 0) extra += $" · ¡UNSAFE! castiga con {N(r.HitBackSide, r.HitBackCard)}";
                else if (atk.UnsafeOnBlock) extra += " · UNSAFE, sin castigo";
                else if (atk.Lockdown) extra += " · lockdown: sin robo";
                else extra += " · roba 1";
                return $"{who} bien la altura de {N(1 - b, r.Card(1 - b))}{extra}{tail}";
            }
            if (r.WrongBlock0 || r.WrongBlock1)
            {
                int b = r.WrongBlock0 ? 0 : 1;
                var atk = Cards.Def(1 - b, r.Card(1 - b));
                string h = atk.Height == CardHeight.High ? "ALTO" : atk.Height == CardHeight.Low ? "BAJO" : "MID";
                return $"¡bloqueo EQUIVOCADO! {N(1 - b, r.Card(1 - b))} pega {h} y entra{tail}";
            }
            if (r.Dodged0 || r.Dodged1)
            {
                int dd = r.Dodged0 ? 0 : 1;
                var atk = Cards.Def(1 - dd, r.Card(1 - dd));
                if (atk.Projectile) return "ESQUIVE al proyectil: lo evita, sin castigo";
                return r.HitBackCard >= 0
                    ? $"¡ESQUIVE! evita {N(1 - dd, r.Card(1 - dd))} y devuelve {N(dd, r.HitBackCard)}{tail}"
                    : $"¡ESQUIVE! evita {N(1 - dd, r.Card(1 - dd))} (sin castigo)";
            }
            if (r.Thrown0 || r.Thrown1)
            {
                int v = r.Thrown0 ? 0 : 1;
                var vic = Cards.Def(v, r.Card(v));
                string why = vic.Kind == CardKind.Block ? "AGARRE le gana al bloqueo"
                    : vic.Kind == CardKind.Dodge ? "AGARRE le gana al esquive"
                    : "agarre vs agarre: el más rápido tira";
                return $"{why}{(r.KdNext(v) ? " y DERRIBA" : "")}{tail}";
            }
            if (r.Dmg0 == 0 && r.Dmg1 == 0) return r.Arc0 || r.Arc1 ? "el ARCO cobró igual" : "nadie conecta";
            int w = r.Dmg1 > 0 && !r.Thrown0 ? 0 : 1;
            var wd = Cards.Def(w, r.Card(w));
            var ld = Cards.Def(1 - w, r.Card(1 - w));
            string winner = w == 0 ? "GANÁS VOS" : "GANA EL RIVAL";
            string reason;
            if (ld.Kind == CardKind.Throw) reason = "el GOLPE siempre interrumpe al AGARRE";
            else if (wd.Projectile && ld.Projectile) reason = "proyectil de nivel más alto";
            else if (wd.Speed == ld.Speed) reason = $"empate de speed {wd.Speed}: gana el ACTIVO";
            else reason = $"más rápido (speed {wd.Speed} vs {ld.Speed})";
            return $"{winner}: {reason}{tail}";
        }

        // Cada carta actúa con el move clásico MÁS PARECIDO a su golpe real;
        // el combo entero se actúa en secuencia. La tabla ya decidió todo.
        static int CardsTheaterMove(int chrIdx, int card)
        {
            bool jaina = chrIdx == CardCatalog.JainaIdx;
            switch (card)
            {
                case CardCatalog.AttackA:
                case CardCatalog.AttackB: return MoveCatalog.AttackA;   // jabs rápidos
                case CardCatalog.AttackC:
                case CardCatalog.AttackD:
                case CardCatalog.AttackE: return MoveCatalog.Strong;    // golpes con el cuerpo
                case CardCatalog.Throw: return MoveCatalog.YomiGrab;
                case CardCatalog.Dodge: return MoveCatalog.DashB;       // se hace atrás
                case CardCatalog.SpecialX: return MoveCatalog.Hadouken; // proyectil (nube / flecha)
                case CardCatalog.SpecialY: return MoveCatalog.Shoryuken; // el reversal que sube
                case CardCatalog.SpecialZ: return MoveCatalog.Tatsu;    // giro (torbellino / patada cruzada)
                case CardCatalog.Super1: return jaina ? MoveCatalog.Super : MoveCatalog.Shoryuken;
                case CardCatalog.Super2: return jaina ? MoveCatalog.Super : MoveCatalog.DashB; // aliento / super esquive
                default: return -1; // blocks: quieto bloqueando
            }
        }

        static int CardsTheaterContact(int chrIdx, int card)
        {
            switch (CardsTheaterMove(chrIdx, card))
            {
                case MoveCatalog.AttackA: return 6;
                case MoveCatalog.Strong: return 14;
                case MoveCatalog.YomiGrab: return 9;
                case MoveCatalog.Hadouken: return 16;
                case MoveCatalog.Shoryuken: return 4;
                case MoveCatalog.Tatsu: return 14;
                case MoveCatalog.Super: return 16;
                default: return -1;
            }
        }

        int CardsTheaterDelay(CardTurnResult r, int i)
        {
            int o = 1 - i;
            int myC = CardsTheaterContact(Cards.CharIdx[i], r.Card(i));
            int theirC = CardsTheaterContact(Cards.CharIdx[o], r.Card(o));
            bool hit = i == 0 ? r.Hit0 : r.Hit1;
            if (hit && myC >= 0 && theirC >= 0) return Mathf.Max(0, theirC + 8 - myC);
            if (r.HitBackSide == i && r.HitBackCard >= 0 && theirC >= 0) return theirC + 10;
            if (r.SuperCounter == i && theirC >= 0) return theirC + 8;
            return 0;
        }

        void BeginCardsTheater()
        {
            var r = _cardsResult;
            _hand.Close();
            _menu.Close();
            _ghost.Clear();
            Sim = new MatchSim();
            int extras = r.Combo0.Count + r.Combo1.Count +
                (r.HitBackCard >= 0 ? 1 : 0) + (r.SuperCounter >= 0 ? 1 : 0);
            _cardsTheaterFrames = 70 + 26 * extras;
            for (int i = 0; i < 2; i++)
            {
                var f = Sim.Fighters[i];
                var def = Cards.Def(i, r.Card(i));
                f.BlockEnabled = (def.Kind == CardKind.Block && r.Blocked(i)) || r.ProjCancel;
                f.X = i == 0 ? -CardsSlot : CardsSlot;
                f.PrevX = f.X;
                f.Hp = _cardsHpBefore[i] * (float)SimConfig.MaxHp / Cards.Chr[i].MaxHp;
            }
            for (int i = 0; i < 2; i++)
            {
                var q = new List<int>();
                int mv = CardsTheaterMove(Cards.CharIdx[i], r.Card(i));
                if (mv >= 0) q.Add(mv);
                foreach (int c in r.Combo(i))
                {
                    int cm = CardsTheaterMove(Cards.CharIdx[i], c);
                    if (cm >= 0) q.Add(cm);
                }
                if (r.HitBackSide == i && r.HitBackCard >= 0)
                {
                    if (Cards.Def(i, r.Card(i)).Kind == CardKind.Dodge) q.Add(MoveCatalog.DashF);
                    int pm = CardsTheaterMove(Cards.CharIdx[i], r.HitBackCard);
                    if (pm >= 0) q.Add(pm);
                }
                if (r.SuperCounter == i) { q.Add(MoveCatalog.DashF); q.Add(MoveCatalog.Strong); }
                if (q.Count > 0) Sim.SetQueue(i, q);
                Sim.Fighters[i].QueueDelayTick = CardsTheaterDelay(r, i);
            }
            TurnStartTick = Sim.Tick;
            _acc = 0f;
            _turnDmg[0] = _turnDmg[1] = 0f;
            _turnHitCount[0] = _turnHitCount[1] = 0;
            State = Flow.Executing;
            _yomiRevealTimer = YomiRevealSeconds;
            _hud.ShowCardsReveal(Cards, r.Card0, r.Card1, CardsRuling(r));
            // la cadena del combo/castigo, visible durante TODA la ejecución
            _hud.ShowCardsComboChain(Cards, r);
            _hud.SetPrompt($"TURNO {TurnNumber} — {Cards.Def(0, r.Card0).Name.ToUpperInvariant()} vs {Cards.Def(1, r.Card1).Name.ToUpperInvariant()}");
            SfxLib.Play(SfxLib.Kind.TurnStart, 0.6f);
        }
        int _cardsTheaterFrames = 70;

        void TickCardsTheater()
        {
            if (_yomiRevealTimer > 0f)
            {
                _yomiRevealTimer -= Time.deltaTime;
                if (_yomiRevealTimer < YomiRevealSeconds - 0.35f &&
                    (GameInput.EndTurnPressed() || GameInput.ClickPressed()))
                    _yomiRevealTimer = 0f;
                if (_yomiRevealTimer <= 0f) _hud.DockYomiCards();
                return;
            }
            if (_hitstop > 0f) { _hitstop -= Time.deltaTime * PlaybackSpeed; return; }
            _acc += Time.deltaTime * PlaybackSpeed * YomiTheaterSpeed;
            while (_acc >= SimConfig.TickDuration)
            {
                _acc -= SimConfig.TickDuration;
                Sim.Step();
                DispatchEvents();
                if (Sim.Over || Sim.Tick - TurnStartTick >= _cardsTheaterFrames)
                {
                    _acc = 0f;
                    EndCardsTurn();
                    return;
                }
                if (_hitstop > 0f) return;
            }
        }

        void EndCardsTurn()
        {
            var r = _cardsResult;
            for (int i = 0; i < 2; i++) Sim.Fighters[i].Hp = CardsTheaterHp(i);
            if (!Cards.Over) { Sim.Over = false; Sim.Winner = -1; }
            for (int i = 0; i < 2; i++)
            {
                int real = r.Dmg(i) - r.Chip(i);
                if (real > 0) _hud.Feedback(i, $"−{real} HP", new Color(1f, 0.42f, 0.35f));
                if (r.Chip(i) > 0) _hud.Feedback(i, $"−{r.Chip(i)} CHIP", new Color(1f, 0.75f, 0.4f));
                if (r.Self(i) > 0) _hud.Feedback(i, $"−{r.Self(i)} PROPIO", new Color(1f, 0.5f, 0.6f));
                if (i == 0 ? r.Arc0 : r.Arc1) _hud.Feedback(i, "¡EL ARCO COBRA!", new Color(1f, 0.6f, 0.2f));
                if (r.Blocked(i))
                    _hud.Feedback(i, (i == 0 ? r.Drew0 : r.Drew1) > 0 ? "BLOQUEO: +1 carta" : "BLOQUEO (sin robo)",
                        new Color(0.35f, 0.85f, 1f));
                if (i == 0 ? r.WrongBlock0 : r.WrongBlock1)
                    _hud.Feedback(i, "¡ALTURA EQUIVOCADA!", new Color(1f, 0.55f, 0.15f));
                if (r.Dodged(i)) _hud.Feedback(i, "¡ESQUIVADO!", new Color(0.3f, 0.95f, 1f));
                if (r.KdNext(i)) _hud.Feedback(i, "¡DERRIBADO!", new Color(1f, 0.6f, 0.4f));
                if (r.MeterGain(i) > 0) _hud.Feedback(i, $"+{r.MeterGain(i)} ★ (chain)", new Color(1f, 0.85f, 0.3f));
                if (r.Combo(i).Count > 0) _hud.Feedback(i, $"COMBO ×{r.Combo(i).Count + 1}", new Color(0.7f, 1f, 0.5f));
                if ((i == 0 ? r.PumpExtra0 : r.PumpExtra1) > 0)
                    _hud.Feedback(i, $"PUMP +{(i == 0 ? r.PumpExtra0 : r.PumpExtra1)}", new Color(1f, 0.85f, 0.3f));
                if (i == 0 ? r.Returned0 : r.Returned1)
                    _hud.Feedback(i, "la carta VUELVE a la mano", new Color(0.7f, 0.95f, 0.7f));
                int wild = i == 0 ? r.Wild0 : r.Wild1;
                if (wild > 0) _hud.Feedback(i, $"WILD SWING ×{wild}", new Color(1f, 0.8f, 0.3f));
            }
            if (r.Reckless) _hud.Feedback(r.Active, "IMPRUDENCIA: −2 y roba", new Color(1f, 0.5f, 0.6f));
            if (r.HitBackSide >= 0 && r.HitBackCard >= 0)
                _hud.Feedback(r.HitBackSide, $"CASTIGO: {Cards.Def(r.HitBackSide, r.HitBackCard).Name}", new Color(1f, 0.55f, 0.9f));
            _hud.AddTurnLog($"T{TurnNumber}  <color=#8cc8ff>{Cards.Def(0, r.Card0).Short}</color> −{r.Dmg1}  ·  <color=#ffa080>{Cards.Def(1, r.Card1).Short}</color> −{r.Dmg0}");
            _hud.SetTurnSummary($"último turno: {Cards.Def(0, r.Card0).Name} vs {Cards.Def(1, r.Card1).Name} — vos −{r.Dmg0 + r.Self0} · rival −{r.Dmg1 + r.Self1}");

            if (Cards.Over)
            {
                Sim.Over = true;
                Sim.Winner = Cards.Winner;
                // una partida de cartas ES el match (Yomi 2 no tiene rounds)
                if (Cards.Winner >= 0) _wins[Cards.Winner] = RoundsToWin - 1;
                if (Cards.Hp[0] <= 0 || Cards.Hp[1] <= 0)
                {
                    _koTimer = 1.4f;
                    Time.timeScale = 0.25f;
                    _hud.ShowBigMessage("K.O.", new Color(1f, 0.3f, 0.25f));
                    Announcer.Play();
                    return;
                }
                CardsTimeOver();
                return;
            }
            StartPlanning();
        }

        // Mazos agotados dos veces: TIME OVER, decide la vida (regla real).
        void CardsTimeOver()
        {
            Sim.Over = true;
            Sim.Winner = Cards.Winner;
            if (Cards.Winner >= 0) _wins[Cards.Winner] = RoundsToWin - 1; // partida única
            for (int i = 0; i < 2; i++) Sim.Fighters[i].Hp = CardsTheaterHp(i);
            _hand.Close();
            _menu.Close();
            _hud.ShowBigMessage("TIME OVER\n<size=18>mazos agotados: decide la vida</size>", new Color(1f, 0.85f, 0.3f));
            SfxLib.Play(SfxLib.Kind.Ko, 0.6f);
            OnRoundEnd();
        }

        // ---------- execution ----------

        void BeginExecution()
        {
            if (Mode == GameMode.VsAI) _ai.ObserveOpponentPlan(_plans[0]);
            for (int i = 0; i < 2; i++)
            {
                if (_wakeReversal[i])
                {
                    // REVERSAL: levanta ya + separa; el costo en AP se cobra en EndTurn
                    Sim.Reversal(i);
                    _eco.ReversalUsed[i] = true;
                    _hud.Feedback(i, "¡REVERSAL!", new Color(1f, 0.55f, 0.9f));
                }
                else Sim.AdjustKnockdown(i, WakeDelta(i));
            }
            CaptureTurnStartStun(); // las timelines muestran el wakeup real al ejecutar
            Sim.SetQueue(0, _plans[0]);
            Sim.SetQueue(1, _plans[1]);
            _turnLog.Add((new List<int>(_plans[0]), new List<int>(_plans[1]), WakeDelta(0), WakeDelta(1),
                _wakeReversal[0], _wakeReversal[1]));
            TurnStartTick = Sim.Tick;
            _acc = 0f;
            _ghost.Clear();
            _menu.Close();
            State = Flow.Executing;
            _turnDmg[0] = _turnDmg[1] = 0f;
            _turnHitCount[0] = _turnHitCount[1] = 0;
            _hud.SetTurnSummary("");
            _hud.SetPrompt($"TURNO {TurnNumber} — ¡EJECUTANDO!");
            // mini-reveal: el plan de cada lado (la primera carta grande y la
            // secuencia completa abajo) — responde "¿qué juega?" antes del caos
            _hud.ShowOpeners(_plans[0], _plans[1]);
            SfxLib.Play(SfxLib.Kind.TurnStart, 0.6f);
        }

        static string ProfileName(AIProfile profile) => profile.ToString().ToUpperInvariant();
        static string DifficultyName(AIDifficulty difficulty) => difficulty == AIDifficulty.Easy ? "FÁCIL" :
            difficulty == AIDifficulty.Hard ? "DIFÍCIL" : "NORMAL";

        void EndTurn()
        {
            for (int i = 0; i < 2; i++)
            {
                // economía: cobrar el plan (+ reversal) y pagar ingreso/bancado,
                // ANTES de OnTurnEnd (que limpia el flag de bloqueo bancado)
                if (SimConfig.ApActive)
                {
                    bool banked = Sim.Fighters[i].BankedBlock;
                    int spent = PlanApUsed(i) + (_wakeReversal[i] ? ApEconomy.ReversalCost : 0);
                    _eco.EndTurn(i, ApPerTurn, spent, banked);
                    if (banked && Mode != GameMode.Practice)
                        _hud.Feedback(i, "+1 AP (bloqueo bancado)", new Color(0.35f, 0.85f, 1f));
                }
                int lost = Sim.OnTurnEnd(i);
                _turnLost[i] = lost;
                if (lost > 0 && Mode != GameMode.Practice)
                    _hud.Feedback(i, $"PERDIÓ {lost} ÓRDENES (lo interrumpieron)", new Color(1f, 0.6f, 0.4f));
            }
            _hasTurnSummary = true;
            AddTurnLogLine();

            // TIME OVER: se acabaron los turnos del round, decide la vida
            if (Mode != GameMode.Practice && TurnNumber >= TurnsPerRound)
            {
                _hud.ShowBigMessage("TIME OVER", new Color(1f, 0.85f, 0.3f));
                SfxLib.Play(SfxLib.Kind.Ko, 0.6f);
                BeginRoundReplay();
                return;
            }
            StartPlanning();
        }

        // Ganador efectivo del round: KO manda; sin KO decide la vida (TIME
        // OVER) y, con vida igual, la GUARDIA restante — premia al que atacó
        // (la guardia solo regenera ejecutando moves que no bloquean): la
        // tortuga que empata a vida pierde el juicio. En YOMI las guardias
        // quedan intactas e iguales → sigue siendo empate, no cambia nada.
        public int EffectiveWinner()
        {
            if (Sim.Over) return Sim.Winner;
            float h0 = Sim.Fighters[0].Hp, h1 = Sim.Fighters[1].Hp;
            if (h0 != h1) return h0 > h1 ? 0 : 1;
            float g0 = Sim.Fighters[0].Guard, g1 = Sim.Fighters[1].Guard;
            return g0 > g1 + 0.01f ? 0 : g1 > g0 + 0.01f ? 1 : -1;
        }

        void AddTurnLogLine()
        {
            if (_turnLog.Count == 0) return;
            var t = _turnLog[_turnLog.Count - 1];
            string q0 = ChipString(t.q0), q1 = ChipString(t.q1);
            _hud.AddTurnLog($"T{TurnNumber}  <color=#8cc8ff>{q0}</color> −{_turnDmg[1]:0}  ·  <color=#ffa080>{q1}</color> −{_turnDmg[0]:0}");
        }

        // El plan completo que ese lado ejecutó el turno PASADO (ya es
        // público): durante la planificación, la fila rival de la timeline
        // lo muestra como recap — "qué acaba de pasar", congelado y legible.
        public List<int> LastTurnQueue(int side) =>
            _turnLog.Count == 0 ? null : side == 0 ? _turnLog[_turnLog.Count - 1].q0 : _turnLog[_turnLog.Count - 1].q1;

        // Log de aperturas (Ley 5 de la biblia: info pública para LEER, no
        // adivinar): las primeras cartas de los últimos planes YA REVELADOS
        // de ese lado, la más reciente al final. "—" = pasó el turno.
        public string RecentOpenings(int side, int count = 3)
        {
            if (_turnLog.Count == 0) return "";
            var sb = new System.Text.StringBuilder();
            int from = Mathf.Max(0, _turnLog.Count - count);
            for (int t = from; t < _turnLog.Count; t++)
            {
                var q = side == 0 ? _turnLog[t].q0 : _turnLog[t].q1;
                if (sb.Length > 0) sb.Append(" · ");
                sb.Append(q.Count == 0 ? "—" : HudUI.ChipLabel(q[0]));
            }
            return sb.ToString();
        }

        static string ChipString(List<int> q)
        {
            if (q.Count == 0) return "—";
            var sb = new System.Text.StringBuilder();
            foreach (var mi in q) { if (sb.Length > 0) sb.Append(' '); sb.Append(HudUI.ChipLabel(mi)); }
            return sb.ToString();
        }

        void Update()
        {
            // que el audio nunca quede ahogado si el replay se cortó a mitad de un tirón
            if (State != Flow.Replay && AudioListener.volume < 1f)
                AudioListener.volume = 1f;

            if (State == Flow.ModeSelect) return;
            EnforceDuelStage();

            // framing suave según la distancia entre los peleadores
            if (Sim != null) CamFx()?.SetDistance(Mathf.Abs(Sim.Fighters[1].X - Sim.Fighters[0].X));

            // KO slow-mo: cámara lenta cosmética antes de cerrar el round
            if (_koTimer > 0f)
            {
                _koTimer -= Time.unscaledDeltaTime;
                if (_koTimer <= 0f)
                {
                    Time.timeScale = 1f;
                    // DUELO cierra con su propia pantalla: una partida ES el
                    // match, así que el replay del round no tiene sentido.
                    if (SimConfig.DuelEnabled) ShowDuelResults();
                    else BeginRoundReplay();
                }
                return;
            }

            if (GameInput.MenuPressed()) { GoToModeSelect(); return; }
            if (State == Flow.GameOver && GameInput.ReplayPressed()) { StartReplay(); return; }
            if (State == Flow.Replay && GameInput.EndTurnPressed()) { SkipReplay(); return; }
            if (GameInput.RestartPressed())
            {
                // online no hay revancha local: desincronizaría la sala
                if (Mode == GameMode.Online) _hud.Feedback(LocalSide, "ONLINE: no hay revancha — M para salir", new Color(1f, 0.7f, 0.4f));
                else { ResetMatch(); return; }
            }

            // timer de planificación: al agotarse se manda lo que haya
            if (TimedPlanning)
            {
                _planTimer -= Time.deltaTime;
                _hud.SetPlanTimer(Mathf.CeilToInt(Mathf.Max(0f, _planTimer)));
                if (_planTimer <= 0f)
                {
                    _hud.SetPlanTimer(-1);
                    PlanConfirm();
                    return;
                }
            }
            else _hud.SetPlanTimer(-1);

            // pantalla "pasá el teclado" (1v1) y espera de código (async)
            if (State == Flow.Planning && Time.frameCount > _guardFrame)
            {
                if (_handoff && GameInput.ConfirmPressed())
                {
                    _handoff = false;
                    Picker = _pendingPicker;
                    _planTimer = PlanSeconds; // el timer arranca cuando tomás el teclado
                    _hud.SetBanner("");
                    _menu.Open(Picker);
                    UpdatePlanPrompt();
                    UpdateGhost();
                    return;
                }
                if (_awaitingCode && Mode == GameMode.Async && GameInput.EndTurnPressed())
                {
                    TryPasteRivalCode();
                    return;
                }
            }

            if (State == Flow.RoundOver)
            {
                _roundTimer -= Time.deltaTime;
                if (_roundTimer <= 0f)
                {
                    _hud.SetBanner("");
                    StartRound();
                }
                return;
            }

            if (State == Flow.Executing)
            {
                if (SimConfig.DuelEnabled) TickDuelTheater();
                else if (SimConfig.CardsEnabled) TickCardsTheater();
                else if (SimConfig.YomiEnabled) TickYomiTheater();
                else TickExecuting();
            }
            else if (State == Flow.Replay) TickReplay();
        }

        void TickExecuting()
        {
            if (_hitstop > 0f) { _hitstop -= Time.deltaTime * PlaybackSpeed; return; } // pausa cosmética, la sim no avanza

            _acc += Time.deltaTime * PlaybackSpeed * ClassicPace; // la acción al 50%: que se LEA
            while (_acc >= SimConfig.TickDuration)
            {
                _acc -= SimConfig.TickDuration;
                Sim.Step();
                DispatchEvents();

                if (Sim.Over)
                {
                    if (Mode == GameMode.Practice && Sim.Winner == 0)
                    {
                        Sim.Fighters[1].Hp = SimConfig.MaxHp;
                        Sim.Over = false;
                        Sim.Winner = -1;
                        _hud.Feedback(1, "DUMMY REINICIADO", new Color(0.7f, 0.7f, 0.7f));
                    }
                    else
                    {
                        // KO en cámara lenta (25%): el golpe final se saborea
                        _koTimer = 1.5f;
                        Time.timeScale = 0.25f;
                        _hud.ShowBigMessage("K.O.", new Color(1f, 0.3f, 0.25f));
                        Announcer.Play();
                        return;
                    }
                }

                if (Sim.Tick - TurnStartTick >= CurrentTurnFrames)
                {
                    _acc = 0f;
                    EndTurn();
                    return;
                }
                if (_hitstop > 0f) return;
            }
        }

        void OnRoundEnd()
        {
            _acc = 0f;
            _hud.SetPrompt("");
            int winner = EffectiveWinner(); // KO, o la vida si fue TIME OVER
            if (winner >= 0) _wins[winner]++;

            if (winner >= 0 && _wins[winner] >= RoundsToWin)
            {
                State = Flow.GameOver;
                return;
            }

            State = Flow.RoundOver;
            _roundTimer = 2.6f;
            string how = Sim.Over ? "" : " (por vida)";
            string txt = winner == 0 ? $"ROUND PARA VOS{how}" : winner == 1 ? $"ROUND PARA EL RIVAL{how}" : "ROUND EMPATADO";
            _hud.SetBanner($"{txt}\n<size=30>{_wins[0]} — {_wins[1]}</size>");
        }

        // ---------- replay ----------

        // Terminó el round: SIEMPRE se repite la pelea entera de corrido antes
        // del banner, gane quien gane. No es opcional: acá se lee qué pasó.
        void BeginRoundReplay()
        {
            if (_turnLog.Count == 0) { OnRoundEnd(); return; }
            _autoReplay = true;
            StartReplay();
            _hud.SetPrompt("REPETICIÓN DEL ROUND — la pelea entera, de corrido");
        }

        // SKIP del replay: adelanta la re-simulación hasta el final, sin juice.
        // Determinista, así que el resultado es idéntico al que viste en vivo.
        public void SkipReplay()
        {
            if (State != Flow.Replay) return;
            int safety = 1000000;
            while (safety-- > 0)
            {
                Sim.Step();
                if (Sim.Over) break;
                if (Sim.Tick - TurnStartTick >= CurrentTurnFrames)
                {
                    Sim.OnTurnEnd(0);
                    Sim.OnTurnEnd(1);
                    _replayTurn++;
                    if (_replayTurn >= _turnLog.Count) break;
                    LoadReplayTurn();
                }
            }
            _acc = 0f;
            _hitstop = 0f;
            if (_autoReplay) { _autoReplay = false; OnRoundEnd(); }
            else { State = Flow.GameOver; _hud.SetPrompt(""); }
        }

        void StartReplay()
        {
            Sim = new MatchSim();
            if (Mode == GameMode.Practice) Sim.Fighters[1].BlockEnabled = false;
            _replayTurn = 0;
            // en Lag Mode el replay hereda lo rota que terminó la conexión
            int maxLvl = LagLevelForTurn(_turnLog.Count);
            _replayIntensity = ReplayLagFX.ScaleWithLag && LagMode ? 1f + 0.6f * maxLvl : 1f;
            _replayGlitchIn = Random.Range(1.5f, 3f) / _replayIntensity;
            _replayFreeze = 0f;
            _replayDebt = 0f;
            _choppyTimer = 0f;
            _choppyAcc = 0f;
            _rewindSnap = null;
            _suppressEventsUntil = -1;
            _acc = 0f;
            TurnNumber = 0;
            _views[0].OnMatchReset();
            _views[1].OnMatchReset();
            _hud.OnMatchReset();
            _hud.SetPrompt("REPETICIÓN — la pelea entera, de corrido");
            LoadReplayTurn();
            State = Flow.Replay;
        }

        void LoadReplayTurn()
        {
            var t = _turnLog[_replayTurn];
            if (t.r0) Sim.Reversal(0); else Sim.AdjustKnockdown(0, t.w0);
            if (t.r1) Sim.Reversal(1); else Sim.AdjustKnockdown(1, t.w1);
            Sim.SetQueue(0, t.q0);
            Sim.SetQueue(1, t.q1);
            TurnStartTick = Sim.Tick;
            TurnNumber = _replayTurn + 1;
            CaptureTurnStartStun();
        }

        // Fin del tirón: si hay foto reciente del MISMO turno, retrocede unos
        // frames y los re-simula — el "teleport" clásico de netplay. Los eventos
        // re-simulados no re-disparan juice, y la deuda cubre el tiempo perdido.
        void EndStall()
        {
            if (!ReplayLagFX.Rewind || _rewindSnap == null) return;
            if (_rewindSnap.Tick < TurnStartTick || _rewindSnap.Tick >= Sim.Tick) return;
            _suppressEventsUntil = Sim.Tick;
            _replayDebt += (Sim.Tick - _rewindSnap.Tick) * SimConfig.TickDuration;
            Sim = _rewindSnap;
            _rewindSnap = null;
        }

        void CaptureTurnStartStun()
        {
            for (int i = 0; i < 2; i++)
            {
                TurnStartStun[i] = Sim.StunRemaining(i);
                TurnStartStunKind[i] = Sim.Fighters[i].Stun;
                TurnStartCommitted[i] = Sim.CommittedRemaining(i);
            }
        }

        void TickReplay()
        {
            if (_hitstop > 0f) { _hitstop -= Time.deltaTime * PlaybackSpeed; return; }

            // ---- lag teatral (flags en ReplayLagFX): congelar/saltos → deuda →
            // fast-forward hasta alcanzarse, con rewind y ping falso opcionales ----
            float dt = Time.deltaTime;
            float lagMult = 1f;
            if (ReplayMode == ReplayViewMode.Lag && ReplayLagFX.Enabled)
            {
                if (_replayFreeze > 0f)
                {
                    _replayFreeze -= dt;
                    _replayDebt += dt;
                    _hud.SetReplayStalled(true);
                    if (ReplayLagFX.AudioDrop) AudioListener.volume = 0.3f; // el audio se ahoga
                    if (_replayFreeze <= 0f) EndStall();
                    return; // trabado: el playhead no avanza, la sim tampoco
                }
                _hud.SetReplayStalled(false);
                if (ReplayLagFX.AudioDrop && AudioListener.volume < 1f)
                    AudioListener.volume = Mathf.MoveTowards(AudioListener.volume, 1f, 3f * dt);

                if (_choppyTimer > 0f)
                {
                    // "a los saltos": el tiempo se junta y se suelta en tandas (~5 fps)
                    _choppyTimer -= dt;
                    _choppyAcc += dt;
                    if (_choppyAcc < 0.18f && _choppyTimer > 0f) return;
                    dt = _choppyAcc;
                    _choppyAcc = 0f;
                }
                else if (ReplayLagFX.Stutter || ReplayLagFX.Choppy)
                {
                    _replayGlitchIn -= dt;
                    if (_replayGlitchIn <= 0f)
                    {
                        _replayGlitchIn = Random.Range(1.6f, 4f) / _replayIntensity;
                        bool freeze = ReplayLagFX.Stutter &&
                                      (!ReplayLagFX.Choppy || Random.value < 0.55f);
                        if (freeze)
                        {
                            _replayFreeze = Random.Range(0.15f, 0.55f) * Mathf.Min(_replayIntensity, 1.6f);
                            _hud.GlitchBurst(_replayFreeze + 0.25f);
                            SfxLib.Play(SfxLib.Kind.Glitch, 0.35f);
                            return;
                        }
                        _choppyTimer = Random.Range(0.7f, 1.4f) * Mathf.Min(_replayIntensity, 1.5f);
                        _hud.GlitchBurst(0.3f);
                        SfxLib.Play(SfxLib.Kind.Glitch, 0.25f);
                    }
                }
                if (_replayDebt > 0f)
                {
                    lagMult = 2.6f; // rubber-banding: corre a recuperar lo congelado
                    _replayDebt = Mathf.Max(0f, _replayDebt - dt * (lagMult - 1f));
                }
            }

            // mismo ritmo 50% que la ejecución; RÁPIDO (×2) = tiempo real
            _acc += dt * PlaybackSpeed * lagMult * (ReplayMode == ReplayViewMode.Fast ? 2f : 1f) * ClassicPace;
            while (_acc >= SimConfig.TickDuration)
            {
                _acc -= SimConfig.TickDuration;
                Sim.Step();
                // foto periódica para el mini-rewind (solo en modo LAG)
                if (ReplayMode == ReplayViewMode.Lag && ReplayLagFX.Rewind && Sim.Tick % 6 == 0)
                    _rewindSnap = Sim.Clone();
                // tras un rewind, lo re-simulado ya se vio: sin sfx/sparks dobles
                if (Sim.Tick > _suppressEventsUntil) DispatchEvents();
                else _lastProjCount = Sim.Projectiles.Count;

                if (Sim.Over)
                {
                    _acc = 0f;
                    if (_autoReplay) { _autoReplay = false; OnRoundEnd(); }
                    else { State = Flow.GameOver; _hud.SetPrompt(""); }
                    return;
                }

                if (Sim.Tick - TurnStartTick >= CurrentTurnFrames)
                {
                    Sim.OnTurnEnd(0);
                    Sim.OnTurnEnd(1);
                    _replayTurn++;
                    if (_replayTurn >= _turnLog.Count)
                    {
                        _acc = 0f;
                        if (_autoReplay) { _autoReplay = false; OnRoundEnd(); }
                        else { State = Flow.GameOver; _hud.SetPrompt(""); }
                        return;
                    }
                    LoadReplayTurn();
                }
            }
        }

        CameraFX _camFx;

        Vector3 ContactPos(int defender) => _views[defender].transform.position + new Vector3(0f, 1.25f, 0f);

        void DispatchEvents()
        {
            // Teatro YOMI/CARTAS: la sim es un títere MUDO — pone sparks, sfx y
            // poses, pero los carteles y números los dicta la tabla
            // (EndYomiTurn / EndCardsTurn). Sin esto, sus whiffs/counters
            // internos contradicen el resultado.
            bool yomiTheater = (SimConfig.YomiEnabled || SimConfig.CardsEnabled) && State == Flow.Executing;
            foreach (var ev in Sim.LastEvents)
            {
                int def = 1 - ev.Attacker;
                switch (ev.Kind)
                {
                    case EvKind.Hit:
                        _turnDmg[ev.Attacker] += ev.Damage;
                        _turnHitCount[ev.Attacker]++;
                        _views[def].FlashHit(ev.Counter);
                        // sparks PROPORCIONALES al daño: un jab no puede pesar
                        // visualmente lo mismo que un shoryuken
                        int shards = Mathf.RoundToInt(4f + ev.Damage * 5f) + (ev.Counter ? 5 : 0);
                        SparkFX.Burst(ContactPos(def), ev.Counter ? new Color(1f, 0.55f, 0.1f) : Color.white,
                            shards, 2.8f + ev.Damage * 0.5f);
                        SfxLib.Play(ev.Counter ? SfxLib.Kind.Counter : SfxLib.Kind.Hit);
                        _hitstop = Mathf.Max(_hitstop, ev.Counter ? 0.13f : 0.075f);
                        CamFx()?.Shake(ev.Counter ? 0.1f : 0.05f);
                        if (ev.Damage >= 2f || ev.Counter) CamFx()?.Punch(0.22f);
                        if (ev.Counter && !yomiTheater)
                        {
                            _hud.ShowBigMessage("¡COUNTER!", new Color(1f, 0.55f, 0.15f));
                            _hud.ScreenFlash(0.4f); // impact frame de anime
                        }
                        // la tabla ya decidió: al golpeado no le queda nada por hacer
                        if (yomiTheater) { Sim.Fighters[def].Queue.Clear(); Sim.Fighters[def].QueueIndex = 0; }
                        break;
                    case EvKind.Blocked:
                        _views[def].FlashBlock();
                        SparkFX.Burst(ContactPos(def), new Color(0.45f, 0.75f, 1f), 6, 2.2f);
                        SfxLib.Play(SfxLib.Kind.Block, 0.8f);
                        _hitstop = Mathf.Max(_hitstop, 0.04f);
                        break;
                    case EvKind.Parry:
                        _views[ev.Attacker].FlashParry();
                        SparkFX.Burst(ContactPos(ev.Attacker), new Color(0.3f, 0.95f, 1f), 12, 3.2f);
                        SfxLib.Play(SfxLib.Kind.Block, 1.15f);
                        _hitstop = Mathf.Max(_hitstop, 0.09f);
                        CamFx()?.Shake(0.06f);
                        _hud.ShowBigMessage("¡PARRY!", new Color(0.3f, 0.95f, 1f));
                        break;
                    case EvKind.Tech:
                        SfxLib.Play(SfxLib.Kind.Block, 1f);
                        _hitstop = Mathf.Max(_hitstop, 0.08f);
                        break;
                    case EvKind.GuardCrush:
                        _views[def].FlashHit();
                        SparkFX.Burst(ContactPos(def), new Color(1f, 0.85f, 0.2f), 18, 4f);
                        SfxLib.Play(SfxLib.Kind.Counter);
                        _hitstop = Mathf.Max(_hitstop, 0.14f);
                        CamFx()?.Shake(0.11f);
                        CamFx()?.Punch(0.3f);
                        _hud.ScreenFlash(0.45f);
                        _hud.ShowBigMessage("¡GUARDIA ROTA!", new Color(1f, 0.85f, 0.2f));
                        Announcer.Play(0.7f);
                        break;
                    case EvKind.LimbLost:
                        SparkFX.Burst(ContactPos(def) + new Vector3(0f, ev.Limb == Limb.Leg ? -0.6f : 0.1f, 0f),
                            new Color(0.95f, 0.25f, 0.2f), 20, 4.2f);
                        SfxLib.Play(SfxLib.Kind.Ko, 0.7f);
                        _hitstop = Mathf.Max(_hitstop, 0.16f);
                        CamFx()?.Shake(0.13f);
                        _hud.ShowBigMessage(ev.Limb == Limb.Arm ? "¡BRAZO FUERA!" : "¡PIERNA FUERA!", new Color(1f, 0.35f, 0.3f));
                        break;
                }
                if (!yomiTheater) _hud.OnSimEvent(ev); // los popups en yomi los pone la tabla
            }

            // TRADE: los dos conectaron en el mismo frame → énfasis extra
            bool hit0 = false, hit1 = false;
            foreach (var ev in Sim.LastEvents)
                if (ev.Kind == EvKind.Hit) { if (ev.Attacker == 0) hit0 = true; else hit1 = true; }
            if (hit0 && hit1)
            {
                _hitstop = Mathf.Max(_hitstop, 0.15f);
                CamFx()?.Shake(0.11f);
                _hud.Feedback(0, "¡TRADE!", new Color(1f, 0.85f, 0.3f));
                _hud.Feedback(1, "¡TRADE!", new Color(1f, 0.85f, 0.3f));
            }

            if (Sim.Projectiles.Count > _lastProjCount)
                SfxLib.Play(SfxLib.Kind.Fireball, 0.9f);
            _lastProjCount = Sim.Projectiles.Count;

            if (Sim.Over)
            {
                SfxLib.Play(SfxLib.Kind.Ko);
                CamFx()?.Shake(0.14f);
                CamFx()?.Punch(0.45f);
                _hud.ScreenFlash(0.65f); // el KO es EL evento: que la pantalla lo grite
            }
        }

        CameraFX CamFx()
        {
            if (_camFx == null && Camera.main != null)
            {
                _camFx = Camera.main.GetComponent<CameraFX>();
                if (_camFx == null) _camFx = Camera.main.gameObject.AddComponent<CameraFX>();
            }
            return _camFx;
        }
    }

    // Input centralizado (Input System o legacy, según el proyecto).
    public static class GameInput
    {
#if ENABLE_INPUT_SYSTEM
        static Keyboard K => Keyboard.current;
        public static bool LeftPressed() => K != null && (K.leftArrowKey.wasPressedThisFrame || K.aKey.wasPressedThisFrame);
        public static bool RightPressed() => K != null && (K.rightArrowKey.wasPressedThisFrame || K.dKey.wasPressedThisFrame);
        public static bool UpPressed() => K != null && (K.upArrowKey.wasPressedThisFrame || K.wKey.wasPressedThisFrame);
        public static bool DownPressed() => K != null && (K.downArrowKey.wasPressedThisFrame || K.sKey.wasPressedThisFrame);
        public static bool AddPressed() => K != null && (K.enterKey.wasPressedThisFrame || K.jKey.wasPressedThisFrame);
        public static bool UndoPressed() => K != null && K.backspaceKey.wasPressedThisFrame;
        public static bool EndTurnPressed() => K != null && (K.spaceKey.wasPressedThisFrame || K.fKey.wasPressedThisFrame);
        public static bool ConfirmPressed() => K != null && (K.enterKey.wasPressedThisFrame || K.spaceKey.wasPressedThisFrame || K.jKey.wasPressedThisFrame);
        public static bool RestartPressed() => K != null && K.rKey.wasPressedThisFrame;
        public static bool MenuPressed() => K != null && (K.mKey.wasPressedThisFrame || K.escapeKey.wasPressedThisFrame);
        public static bool ReplayPressed() => K != null && K.vKey.wasPressedThisFrame;
        public static bool ClickPressed() => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        public static bool ClickHeld() => Mouse.current != null && Mouse.current.leftButton.isPressed;
        public static bool RightClickPressed() => Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
        public static Vector2 MousePos() => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        public static bool BoxesPressed() => K != null && K.hKey.wasPressedThisFrame;
        public static bool LogPressed() => K != null && K.lKey.wasPressedThisFrame;
        public static char LetterPressed()
        {
            if (K == null) return '\0';
            for (int k = (int)Key.A; k <= (int)Key.Z; k++)
                if (K[(Key)k].wasPressedThisFrame) return (char)('A' + k - (int)Key.A);
            return '\0';
        }
        public static bool CancelPressed() => K != null && K.escapeKey.wasPressedThisFrame;
        public static int NumberPressed()
        {
            if (K == null) return -1;
            for (int n = 1; n <= 9; n++)
                if (K[(Key)((int)Key.Digit1 + (n - 1))].wasPressedThisFrame) return n;
            if (K.digit0Key.wasPressedThisFrame) return 10;
            return -1;
        }
#else
        public static bool LeftPressed() => Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A);
        public static bool RightPressed() => Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D);
        public static bool UpPressed() => Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
        public static bool DownPressed() => Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
        public static bool AddPressed() => Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.J);
        public static bool UndoPressed() => Input.GetKeyDown(KeyCode.Backspace);
        public static bool EndTurnPressed() => Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.F);
        public static bool ConfirmPressed() => Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.J);
        public static bool RestartPressed() => Input.GetKeyDown(KeyCode.R);
        public static bool MenuPressed() => Input.GetKeyDown(KeyCode.M) || Input.GetKeyDown(KeyCode.Escape);
        public static bool ReplayPressed() => Input.GetKeyDown(KeyCode.V);
        public static bool ClickPressed() => Input.GetMouseButtonDown(0);
        public static bool ClickHeld() => Input.GetMouseButton(0);
        public static bool RightClickPressed() => Input.GetMouseButtonDown(1);
        public static Vector2 MousePos() => Input.mousePosition;
        public static bool BoxesPressed() => Input.GetKeyDown(KeyCode.H);
        public static bool LogPressed() => Input.GetKeyDown(KeyCode.L);
        public static char LetterPressed()
        {
            for (var k = KeyCode.A; k <= KeyCode.Z; k++)
                if (Input.GetKeyDown(k)) return (char)('A' + k - KeyCode.A);
            return '\0';
        }
        public static bool CancelPressed() => Input.GetKeyDown(KeyCode.Escape);
        public static int NumberPressed()
        {
            for (int n = 1; n <= 9; n++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + (n - 1))) return n;
            if (Input.GetKeyDown(KeyCode.Alpha0)) return 10;
            return -1;
        }
#endif
    }
}
