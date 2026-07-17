using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LagFighter
{
    public enum GameMode { Practice, VsAI, PvP }

    // Flujo por turnos programados:
    //  - Planning: en pausa, cada jugador arma su cola de hasta 240 frames
    //    (la IA planifica en secreto). Ghost de preview del plan propio.
    //  - Executing: ambas colas corren simultáneas 4 segundos en tiempo real.
    //  - Al final, V reproduce la pelea completa de corrido (replay determinista).
    public class MatchController : MonoBehaviour
    {
        public enum Flow { ModeSelect, Planning, Executing, RoundOver, GameOver, Replay }

        public const int RoundsToWin = 2; // al mejor de 3

        public MatchSim Sim { get; private set; }
        public float TickFloat => Sim == null ? 0f : Sim.Tick + _acc / SimConfig.TickDuration;
        public GameMode Mode { get; private set; }
        public Flow State { get; private set; } = Flow.ModeSelect;
        public int Picker { get; private set; }
        public int TurnNumber { get; private set; }
        public int TurnStartTick { get; private set; }

        readonly List<int>[] _plans = { new List<int>(), new List<int>() };
        readonly List<(List<int> q0, List<int> q1)> _turnLog = new List<(List<int>, List<int>)>();
        int _replayTurn;
        readonly int[] _wins = new int[2];
        float _roundTimer;
        float _hitstop;
        int _lastProjCount;

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

        void Start()
        {
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
            State = Flow.ModeSelect;
            _menu.Close();
            _ghost.Clear();
            _modeMenu.Open();
            _hud.SetPrompt("");
        }

        public void StartMatch(GameMode mode)
        {
            Mode = mode;
            _modeMenu.Close();
            ResetMatch();
        }

        void ResetMatch()
        {
            _wins[0] = _wins[1] = 0;
            _ai = new SimpleAI(_seed++);
            StartRound();
        }

        void StartRound()
        {
            Sim = new MatchSim();
            if (Mode == GameMode.Practice) Sim.Fighters[1].BlockEnabled = false; // el dummy no bloquea
            _acc = 0f;
            _hitstop = 0f;
            _lastProjCount = 0;
            TurnNumber = 0;
            _turnLog.Clear(); // el replay cubre el round en curso
            _ghost.Clear();
            _menu.Close();
            _views[0].OnMatchReset();
            _views[1].OnMatchReset();
            _hud.OnMatchReset();
            StartPlanning();
        }

        public int GetWins(int i) => _wins[i];

        // ---------- planning ----------

        void StartPlanning()
        {
            TurnNumber++;
            _plans[0].Clear();
            _plans[1].Clear();
            if (Mode != GameMode.PvP)
                _plans[1] = Mode == GameMode.Practice ? new List<int>() : _ai.Plan(Sim, 1);
            Picker = 0;
            State = Flow.Planning;
            _menu.Open(Picker);
            UpdatePlanPrompt();
            UpdateGhost();
        }

        void UpdatePlanPrompt()
        {
            string who = Mode == GameMode.PvP ? $"PLANIFICA JUGADOR {Picker + 1}" :
                         Mode == GameMode.Practice ? "PRÁCTICA — el dummy no hace nada" : "ARMÁ TU TURNO";
            int myStun = Sim.StunRemaining(Picker);
            int oppStun = Sim.StunRemaining(1 - Picker);
            string adv = "";
            if (myStun > 0) adv = $"  ·  arrancás −{myStun}f ({StunName(Picker)})";
            else if (oppStun > 0) adv = $"  ·  VENTAJA +{oppStun}f (rival en {StunName(1 - Picker)})";
            _hud.SetPrompt($"TURNO {TurnNumber} — {who}{adv}");
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
            return State == Flow.Planning && i == Picker;
        }

        public int PlanFramesUsed(int i)
        {
            int f = 0;
            foreach (var m in _plans[i]) f += MoveCatalog.All[m].Total;
            return f;
        }

        public bool PlanFits(int moveIndex) => PlanFramesUsed(Picker) + MoveCatalog.All[moveIndex].Total <= SimConfig.TurnFrames;

        public void PlanAdd(int moveIndex)
        {
            if (!PlanFits(moveIndex)) return;
            _plans[Picker].Add(moveIndex);
            UpdateGhost();
        }

        public void PlanUndo()
        {
            if (_plans[Picker].Count == 0) return;
            _plans[Picker].RemoveAt(_plans[Picker].Count - 1);
            UpdateGhost();
        }

        public void PlanConfirm()
        {
            if (Mode == GameMode.PvP && Picker == 0)
            {
                Picker = 1;
                _menu.Open(Picker);
                UpdatePlanPrompt();
                UpdateGhost();
                return;
            }
            BeginExecution();
        }

        void UpdateGhost()
        {
            var g = PlanPreview.Build(Sim, Picker, _plans[Picker]);
            _ghost.Show(g, Picker);
            _menu.SetPrediction(g, PlanFramesUsed(Picker));
        }

        // ---------- execution ----------

        void BeginExecution()
        {
            Sim.SetQueue(0, _plans[0]);
            Sim.SetQueue(1, _plans[1]);
            _turnLog.Add((new List<int>(_plans[0]), new List<int>(_plans[1])));
            TurnStartTick = Sim.Tick;
            _acc = 0f;
            _ghost.Clear();
            _menu.Close();
            State = Flow.Executing;
            _hud.SetPrompt($"TURNO {TurnNumber} — ¡EJECUTANDO!");
            SfxLib.Play(SfxLib.Kind.TurnStart, 0.6f);
        }

        void EndTurn()
        {
            for (int i = 0; i < 2; i++)
            {
                int lost = Sim.OnTurnEnd(i);
                if (lost > 0 && Mode != GameMode.Practice)
                    _hud.Feedback(i, $"PERDIÓ {lost} ÓRDENES (lo interrumpieron)", new Color(1f, 0.6f, 0.4f));
            }
            StartPlanning();
        }

        void Update()
        {
            if (State == Flow.ModeSelect) return;

            if (GameInput.MenuPressed()) { GoToModeSelect(); return; }
            if (State == Flow.GameOver && GameInput.ReplayPressed()) { StartReplay(); return; }
            if (GameInput.RestartPressed()) { ResetMatch(); return; }

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
            if (_hitstop > 0f) { _hitstop -= Time.deltaTime; return; } // pausa cosmética, la sim no avanza

            _acc += Time.deltaTime;
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
                        OnRoundEnd();
                        return;
                    }
                }

                if (Sim.Tick - TurnStartTick >= SimConfig.TurnFrames)
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
            if (Sim.Winner >= 0) _wins[Sim.Winner]++;

            if (Sim.Winner >= 0 && _wins[Sim.Winner] >= RoundsToWin)
            {
                State = Flow.GameOver;
                return;
            }

            State = Flow.RoundOver;
            _roundTimer = 2.6f;
            string txt = Sim.Winner == 0 ? "ROUND PARA VOS" : Sim.Winner == 1 ? "ROUND PARA EL RIVAL" : "ROUND EMPATADO";
            _hud.SetBanner($"{txt}\n<size=30>{_wins[0]} — {_wins[1]}</size>");
        }

        // ---------- replay ----------

        void StartReplay()
        {
            Sim = new MatchSim();
            if (Mode == GameMode.Practice) Sim.Fighters[1].BlockEnabled = false;
            _replayTurn = 0;
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
            Sim.SetQueue(0, t.q0);
            Sim.SetQueue(1, t.q1);
            TurnStartTick = Sim.Tick;
            TurnNumber = _replayTurn + 1;
        }

        void TickReplay()
        {
            if (_hitstop > 0f) { _hitstop -= Time.deltaTime; return; }

            _acc += Time.deltaTime;
            while (_acc >= SimConfig.TickDuration)
            {
                _acc -= SimConfig.TickDuration;
                Sim.Step();
                DispatchEvents();

                if (Sim.Over)
                {
                    State = Flow.GameOver;
                    _acc = 0f;
                    _hud.SetPrompt("");
                    return;
                }

                if (Sim.Tick - TurnStartTick >= SimConfig.TurnFrames)
                {
                    Sim.OnTurnEnd(0);
                    Sim.OnTurnEnd(1);
                    _replayTurn++;
                    if (_replayTurn >= _turnLog.Count)
                    {
                        State = Flow.GameOver;
                        _acc = 0f;
                        _hud.SetPrompt("");
                        return;
                    }
                    LoadReplayTurn();
                }
            }
        }

        CameraFX _camFx;

        void DispatchEvents()
        {
            foreach (var ev in Sim.LastEvents)
            {
                switch (ev.Kind)
                {
                    case EvKind.Hit:
                        _views[1 - ev.Attacker].FlashHit();
                        SfxLib.Play(ev.Counter ? SfxLib.Kind.Counter : SfxLib.Kind.Hit);
                        _hitstop = Mathf.Max(_hitstop, ev.Counter ? 0.11f : 0.075f);
                        CamFx()?.Shake(ev.Counter ? 0.09f : 0.05f);
                        break;
                    case EvKind.Blocked:
                        _views[1 - ev.Attacker].FlashBlock();
                        SfxLib.Play(SfxLib.Kind.Block, 0.8f);
                        _hitstop = Mathf.Max(_hitstop, 0.04f);
                        break;
                }
                _hud.OnSimEvent(ev);
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
        public static bool AddPressed() => K != null && (K.enterKey.wasPressedThisFrame || K.jKey.wasPressedThisFrame);
        public static bool UndoPressed() => K != null && K.backspaceKey.wasPressedThisFrame;
        public static bool EndTurnPressed() => K != null && (K.spaceKey.wasPressedThisFrame || K.fKey.wasPressedThisFrame);
        public static bool ConfirmPressed() => K != null && (K.enterKey.wasPressedThisFrame || K.spaceKey.wasPressedThisFrame || K.jKey.wasPressedThisFrame);
        public static bool RestartPressed() => K != null && K.rKey.wasPressedThisFrame;
        public static bool MenuPressed() => K != null && (K.mKey.wasPressedThisFrame || K.escapeKey.wasPressedThisFrame);
        public static bool ReplayPressed() => K != null && K.vKey.wasPressedThisFrame;
        public static bool ClickPressed() => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        public static Vector2 MousePos() => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        public static bool BoxesPressed() => K != null && K.hKey.wasPressedThisFrame;
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
        public static bool AddPressed() => Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.J);
        public static bool UndoPressed() => Input.GetKeyDown(KeyCode.Backspace);
        public static bool EndTurnPressed() => Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.F);
        public static bool ConfirmPressed() => Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.J);
        public static bool RestartPressed() => Input.GetKeyDown(KeyCode.R);
        public static bool MenuPressed() => Input.GetKeyDown(KeyCode.M) || Input.GetKeyDown(KeyCode.Escape);
        public static bool ReplayPressed() => Input.GetKeyDown(KeyCode.V);
        public static bool ClickPressed() => Input.GetMouseButtonDown(0);
        public static Vector2 MousePos() => Input.mousePosition;
        public static bool BoxesPressed() => Input.GetKeyDown(KeyCode.H);
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
