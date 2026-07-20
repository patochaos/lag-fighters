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

        readonly List<int>[] _plans = { new List<int>(), new List<int>() };
        readonly List<(List<int> q0, List<int> q1, int w0, int w1)> _turnLog = new List<(List<int>, List<int>, int, int)>();
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
        public ReplayViewMode ReplayMode { get; private set; } = ReplayViewMode.Lag;

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
        float _roundTimer;
        float _hitstop;
        float _koTimer; // KO en cámara lenta (cosmético: timeScale, la sim ya terminó)
        bool _autoReplay; // el replay del round que corre SIEMPRE después del KO
        int _lastProjCount;

        // velocidad de playback (solo presentación, la sim no cambia)
        public float PlaybackSpeed { get; private set; } = 1f;
        public void SetPlaybackSpeed(float s) => PlaybackSpeed = s;

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
            "TIP: pasá el mouse por las cartas para ver qué hacen · click las agrega al turno (probá DASH + y después GOLPE A)",
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
            _menu.Close();
            _ghost.Clear();
            _modeMenu.Open();
            _hud.SetPrompt("");
        }

        public void StartMatch(GameMode mode, bool lagMode, int localSide = 0,
            AIProfile aiProfile = AIProfile.Random, AIDifficulty aiDifficulty = AIDifficulty.Normal)
        {
            Mode = mode;
            LagMode = lagMode;
            LocalSide = localSide;
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
            if (Mode == GameMode.Practice) Sim.Fighters[1].BlockEnabled = false; // el dummy no bloquea
            Time.timeScale = 1f;
            _koTimer = 0f;
            _autoReplay = false;
            _acc = 0f;
            _hitstop = 0f;
            _lastProjCount = 0;
            TurnNumber = 0;
            _prevLagLevel = 0;
            _turnLog.Clear(); // el replay cubre el round en curso
            _hasTurnSummary = false;
            _hud.ClearTurnLog();
            _ghost.Clear();
            _menu.Close();
            _views[0].OnMatchReset();
            _views[1].OnMatchReset();
            _hud.OnMatchReset();
            if (Mode != GameMode.Practice)
                _hud.ShowBigMessage($"ROUND {_wins[0] + _wins[1] + 1}\n<size=18>¡PELEA!</size>", new Color(1f, 0.9f, 0.4f));
            StartPlanning();
        }

        public int GetWins(int i) => _wins[i];

        // ---------- planning ----------

        void StartPlanning()
        {
            TurnNumber++;
            if (LagMode && LagLevel != _prevLagLevel)
            {
                _prevLagLevel = LagLevel;
                _hud.ShowLagMessage(LagMessage(LagLevel));
                SfxLib.Play(SfxLib.Kind.Ko, 0.5f);
            }
            CaptureTurnStartStun();
            _wakeQuick[0] = _wakeQuick[1] = true;
            _plans[0].Clear();
            _plans[1].Clear();
            _awaitingCode = false;
            _myCode = "";
            if (Mode == GameMode.Practice || Mode == GameMode.VsAI)
            {
                if (Mode == GameMode.VsAI && WakeupAvailable(1)) _wakeQuick[1] = _ai.QuickRise();
                _plans[1] = Mode == GameMode.Practice ? new List<int>() : _ai.Plan(Sim, 1, CurrentTurnFrames - WakeDelta(1));
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
                _hud.SetTurnSummary(
                    $"último turno: pegaste {_turnHitCount[p]} (−{_turnDmg[p]:0} HP) · " +
                    $"recibiste {_turnHitCount[o]} (−{_turnDmg[o]:0} HP) · perdiste {_turnLost[p]} órdenes");
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
        public int WakeDelta(int i) => WakeupAvailable(i) ? (_wakeQuick[i] ? WakeQuickDelta : WakeStayDelta) : 0;
        public int EffectiveStartStun(int i) => Mathf.Max(0, TurnStartStun[i] + WakeDelta(i));

        public void ToggleWakeup()
        {
            _wakeQuick[Picker] = !_wakeQuick[Picker];
            // si el plan ya no entra con menos frames, se recorta desde el final
            while (_plans[Picker].Count > 0 && PlanFramesUsed(Picker) > PlanFramesAvailable(Picker))
                _plans[Picker].RemoveAt(_plans[Picker].Count - 1);
            UpdateGhost();
        }

        // la timeline muestra el stun base (info pública); tu propia fila, tu elección
        public int DisplayStun(int i) => State == Flow.Planning && i == Picker ? EffectiveStartStun(i) : TurnStartStun[i];

        // el stun arrastrado te come frames del turno: solo se planifica lo que entra
        public int PlanFramesAvailable(int i) => CurrentTurnFrames - EffectiveStartStun(i);
        public bool PlanFits(int moveIndex) =>
            Sim.MoveAllowed(Picker, moveIndex) &&
            PlanFramesUsed(Picker) + MoveCatalog.All[moveIndex].Total <= PlanFramesAvailable(Picker);

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
            string myCode = TurnCode.Encode(LocalSide, TurnNumber, _wakeQuick[LocalSide], _plans[LocalSide]);
            NetLobby.I.PushTurn(_netSeq, LocalSide, myCode);
            int seq = _netSeq;
            NetLobby.I.PollTurn(seq, 1 - LocalSide, payload => OnRemoteTurn(seq, payload));
            _hud.SetBanner($"SALA {NetLobby.I.Room}\n<size=26>esperando el turno del rival…</size>\n<size=22>M abandona la pelea</size>");
            _hud.SetPrompt($"TURNO {TurnNumber}/{TurnsPerRound} — online");
        }

        void OnRemoteTurn(int seq, string payload)
        {
            if (Mode != GameMode.Online || State != Flow.Planning || !_awaitingCode || seq != _netSeq) return;
            if (!TurnCode.TryDecode(payload, out int side, out int turn, out bool quick, out var moves)) return;
            if (side != 1 - LocalSide || turn != (TurnNumber & 0xFF)) return;
            int remote = 1 - LocalSide;
            _plans[remote] = moves;
            _wakeQuick[remote] = quick;
            _awaitingCode = false;
            _hud.SetBanner("");
            BeginExecution();
        }

        // ---------- online asincrónico por código ----------

        void BeginAwaitCode()
        {
            _awaitingCode = true;
            _guardFrame = Time.frameCount;
            _myCode = TurnCode.Encode(LocalSide, TurnNumber, _wakeQuick[LocalSide], _plans[LocalSide]);
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
            if (!TurnCode.TryDecode(clip, out int side, out int turn, out bool quick, out var moves))
            {
                _hud.Feedback(LocalSide, "CÓDIGO INVÁLIDO (¿copiaste el del rival?)", new Color(1f, 0.5f, 0.4f));
                return;
            }
            if (side != remote) { _hud.Feedback(LocalSide, "ESE CÓDIGO ES TUYO, falta el del rival", new Color(1f, 0.5f, 0.4f)); return; }
            if (turn != (TurnNumber & 0xFF)) { _hud.Feedback(LocalSide, $"CÓDIGO DEL TURNO {turn}, va el {TurnNumber}", new Color(1f, 0.5f, 0.4f)); return; }
            _plans[remote] = moves;
            _wakeQuick[remote] = quick;
            _awaitingCode = false;
            _hud.SetBanner("");
            BeginExecution();
        }

        void UpdateGhost()
        {
            var basis = Sim;
            if (WakeDelta(Picker) != 0)
            {
                basis = Sim.Clone(); // el preview arranca con el wakeup elegido
                basis.AdjustKnockdown(Picker, WakeDelta(Picker));
            }
            _ghost.Show(basis, Picker, _plans[Picker], CurrentTurnFrames);
            var g = PlanPreview.Build(basis, Picker, _plans[Picker], CurrentTurnFrames);
            _menu.SetPrediction(g, PlanFramesUsed(Picker), PlanFramesAvailable(Picker));
        }

        // ---------- execution ----------

        void BeginExecution()
        {
            if (Mode == GameMode.VsAI) _ai.ObserveOpponentPlan(_plans[0]);
            Sim.AdjustKnockdown(0, WakeDelta(0));
            Sim.AdjustKnockdown(1, WakeDelta(1));
            CaptureTurnStartStun(); // las timelines muestran el wakeup real al ejecutar
            Sim.SetQueue(0, _plans[0]);
            Sim.SetQueue(1, _plans[1]);
            _turnLog.Add((new List<int>(_plans[0]), new List<int>(_plans[1]), WakeDelta(0), WakeDelta(1)));
            TurnStartTick = Sim.Tick;
            _acc = 0f;
            _ghost.Clear();
            _menu.Close();
            State = Flow.Executing;
            _turnDmg[0] = _turnDmg[1] = 0f;
            _turnHitCount[0] = _turnHitCount[1] = 0;
            _hud.SetTurnSummary("");
            _hud.SetPrompt($"TURNO {TurnNumber} — ¡EJECUTANDO!");
            SfxLib.Play(SfxLib.Kind.TurnStart, 0.6f);
        }

        static string ProfileName(AIProfile profile) => profile.ToString().ToUpperInvariant();
        static string DifficultyName(AIDifficulty difficulty) => difficulty == AIDifficulty.Easy ? "FÁCIL" :
            difficulty == AIDifficulty.Hard ? "DIFÍCIL" : "NORMAL";

        void EndTurn()
        {
            for (int i = 0; i < 2; i++)
            {
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

        // Ganador efectivo del round: KO manda; sin KO decide la vida (TIME OVER).
        public int EffectiveWinner()
        {
            if (Sim.Over) return Sim.Winner;
            float h0 = Sim.Fighters[0].Hp, h1 = Sim.Fighters[1].Hp;
            return h0 > h1 ? 0 : h1 > h0 ? 1 : -1;
        }

        void AddTurnLogLine()
        {
            if (_turnLog.Count == 0) return;
            var t = _turnLog[_turnLog.Count - 1];
            string q0 = ChipString(t.q0), q1 = ChipString(t.q1);
            _hud.AddTurnLog($"T{TurnNumber}  <color=#8cc8ff>{q0}</color> −{_turnDmg[1]:0}  ·  <color=#ffa080>{q1}</color> −{_turnDmg[0]:0}");
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

            // KO slow-mo: cámara lenta cosmética antes de cerrar el round
            if (_koTimer > 0f)
            {
                _koTimer -= Time.unscaledDeltaTime;
                if (_koTimer <= 0f)
                {
                    Time.timeScale = 1f;
                    BeginRoundReplay();
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

            if (State == Flow.Executing) TickExecuting();
            else if (State == Flow.Replay) TickReplay();
        }

        void TickExecuting()
        {
            if (_hitstop > 0f) { _hitstop -= Time.deltaTime * PlaybackSpeed; return; } // pausa cosmética, la sim no avanza

            _acc += Time.deltaTime * PlaybackSpeed;
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
                        // KO en cámara lenta: el caído cae despacio, después el banner
                        _koTimer = 1.5f;
                        Time.timeScale = 0.3f;
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
            Sim.AdjustKnockdown(0, t.w0);
            Sim.AdjustKnockdown(1, t.w1);
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

            _acc += dt * PlaybackSpeed * lagMult * (ReplayMode == ReplayViewMode.Fast ? 2f : 1f);
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
            foreach (var ev in Sim.LastEvents)
            {
                int def = 1 - ev.Attacker;
                switch (ev.Kind)
                {
                    case EvKind.Hit:
                        _turnDmg[ev.Attacker] += ev.Damage;
                        _turnHitCount[ev.Attacker]++;
                        _views[def].FlashHit(ev.Counter);
                        SparkFX.Burst(ContactPos(def), ev.Counter ? new Color(1f, 0.55f, 0.1f) : Color.white, ev.Counter ? 14 : 9);
                        SfxLib.Play(ev.Counter ? SfxLib.Kind.Counter : SfxLib.Kind.Hit);
                        _hitstop = Mathf.Max(_hitstop, ev.Counter ? 0.13f : 0.075f);
                        CamFx()?.Shake(ev.Counter ? 0.1f : 0.05f);
                        if (ev.Counter) _hud.ShowBigMessage("¡COUNTER!", new Color(1f, 0.55f, 0.15f));
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
                _hud.OnSimEvent(ev);
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
