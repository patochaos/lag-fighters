using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LagFighter
{
    // HUD: vida en pips, prompt de turno, feedback de eventos, banner y las
    // dos timelines del turno (240 frames). Durante la planificación solo se
    // ve tu fila; el plan rival se revela recién al ejecutar.
    public class HudUI : MonoBehaviour
    {
        public const float RowW = 1440f;
        float PxPerFrame => RowW / _mc.CurrentTurnFrames; // en Lag Mode la escala cambia por turno

        MatchController _mc;
        Font _font;
        RectTransform _canvasRt;
        readonly Image[][] _pips = new Image[2][];
        readonly Image[][] _winPips = new Image[2][];
        readonly Image[] _guardFill = new Image[2];
        const float GuardBarW = SimConfig.MaxHp * 46f - 8f;
        Text _banner, _prompt, _dist, _turnSummary;
        string _bannerOverride = "";
        Image _boxBtn, _voiceBtn;
        Text _boxBtnLabel, _voiceBtnLabel;

        // velocidad de playback (x0.5 / x1 / x2) — solo presentación
        static readonly float[] Speeds = { 0.5f, 1f, 2f };
        readonly Image[] _speedBtns = new Image[3];
        readonly Text[] _speedLabels = new Text[3];

        // log lateral de turnos, colapsable con L
        Image _logBtn, _logPanel;
        Text _logBtnLabel, _logText;
        bool _logOpen;
        readonly List<string> _logLines = new List<string>();
        Text _lagMsg;
        float _lagMsgTimer;
        readonly Image[] _wifiBars = new Image[4];
        Text _wifiLabel;
        readonly Text[] _feedback = new Text[2];
        readonly Text[] _stateLabel = new Text[2];
        readonly Text[] _limbLabel = new Text[2];
        readonly float[] _fbTimer = new float[2];
        TimelineRow _row0, _row1;

        public static HudUI Create(MatchController mc)
        {
            var go = new GameObject("LagFighter.HUD");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var hud = go.AddComponent<HudUI>();
            hud._mc = mc;
            hud._canvasRt = go.GetComponent<RectTransform>();
            hud._font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hud.BuildAll();
            return hud;
        }

        void BuildAll()
        {
            BuildSide(0, left: true, "VOS", new Color(0.25f, 0.7f, 0.95f));
            BuildSide(1, left: false, "RIVAL", new Color(0.95f, 0.45f, 0.25f));

            _prompt = MakeText(_canvasRt, "Prompt", "", new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(1400f, 40f),
                28, new Color(1f, 0.9f, 0.4f), TextAnchor.MiddleCenter);
            _prompt.fontStyle = FontStyle.Bold;

            _dist = MakeText(_canvasRt, "Dist", "", new Vector2(0.5f, 1f), new Vector2(0f, -82f), new Vector2(300f, 22f),
                15, new Color(1f, 1f, 1f, 0.45f), TextAnchor.MiddleCenter);

            _turnSummary = MakeText(_canvasRt, "TurnSummary", "", new Vector2(0.5f, 1f), new Vector2(0f, -102f), new Vector2(1400f, 22f),
                17, new Color(0.85f, 0.9f, 1f, 0.85f), TextAnchor.MiddleCenter);

            _boxBtn = MakeImage(_canvasRt, "BoxBtn", new Vector2(0.5f, 1f), new Vector2(-80f, -128f), new Vector2(150f, 32f), new Color(0.12f, 0.14f, 0.18f, 0.9f));
            _boxBtnLabel = MakeText(_boxBtn.rectTransform, "T", "CAJAS: ON", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(146f, 24f),
                14, new Color(0.5f, 1f, 0.6f), TextAnchor.MiddleCenter);

            _voiceBtn = MakeImage(_canvasRt, "VoiceBtn", new Vector2(0.5f, 1f), new Vector2(80f, -128f), new Vector2(150f, 32f), new Color(0.12f, 0.14f, 0.18f, 0.9f));
            _voiceBtnLabel = MakeText(_voiceBtn.rectTransform, "T", "VOZ: ON", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(146f, 24f),
                14, new Color(0.5f, 1f, 0.6f), TextAnchor.MiddleCenter);

            // timelines del turno (fila propia abajo, rival arriba)
            _row1 = new TimelineRow(this, "Row1", y: 262f, height: 40f, dim: true);
            _row0 = new TimelineRow(this, "Row0", y: 316f, height: 40f, dim: false);
            MakeText(_canvasRt, "Row0Label", "VOS", new Vector2(0.5f, 0f), new Vector2(-RowW / 2f - 46f, 316f), new Vector2(80f, 24f),
                15, new Color(0.55f, 0.8f, 1f), TextAnchor.MiddleRight);
            MakeText(_canvasRt, "Row1Label", "RIVAL", new Vector2(0.5f, 0f), new Vector2(-RowW / 2f - 46f, 262f), new Vector2(80f, 24f),
                15, new Color(1f, 0.6f, 0.5f), TextAnchor.MiddleRight);

            _banner = MakeText(_canvasRt, "Banner", "", new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(1200f, 160f),
                64, Color.white, TextAnchor.MiddleCenter);
            _banner.fontStyle = FontStyle.Bold;

            // cartel de "IT GETS LAGGIER"
            _lagMsg = MakeText(_canvasRt, "LagMsg", "", new Vector2(0.5f, 0.5f), new Vector2(0f, 270f), new Vector2(1400f, 120f),
                54, new Color(1f, 0.35f, 0.3f), TextAnchor.MiddleCenter);
            _lagMsg.fontStyle = FontStyle.Bold;

            // indicador de wifi agonizante (solo Lag Mode): 4 barras que se van muriendo
            float wx = 220f;
            for (int b = 0; b < 4; b++)
            {
                _wifiBars[b] = MakeImage(_canvasRt, "Wifi" + b, new Vector2(0.5f, 1f),
                    new Vector2(wx + b * 13f, -128f), new Vector2(9f, 9f + b * 8f), Color.green);
                _wifiBars[b].rectTransform.pivot = new Vector2(0.5f, 0f);
            }
            _wifiLabel = MakeText(_canvasRt, "WifiLabel", "", new Vector2(0.5f, 1f), new Vector2(wx + 20f, -142f), new Vector2(200f, 16f),
                12, new Color(1f, 1f, 1f, 0.6f), TextAnchor.MiddleCenter);

            // velocidad de playback, arriba de la timeline propia
            for (int s = 0; s < Speeds.Length; s++)
            {
                _speedBtns[s] = MakeImage(_canvasRt, "Speed" + s, new Vector2(0.5f, 0f),
                    new Vector2(RowW / 2f - 140f + s * 52f, 372f), new Vector2(48f, 26f), new Color(0.12f, 0.14f, 0.18f, 0.9f));
                _speedLabels[s] = MakeText(_speedBtns[s].rectTransform, "T", Speeds[s] == 0.5f ? "×½" : $"×{Speeds[s]:0}",
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44f, 22f), 14, Color.white, TextAnchor.MiddleCenter);
            }
            MakeText(_canvasRt, "SpeedTag", "VELOCIDAD", new Vector2(0.5f, 0f), new Vector2(RowW / 2f - 230f, 372f),
                new Vector2(110f, 22f), 12, new Color(1f, 1f, 1f, 0.4f), TextAnchor.MiddleRight);

            // log de turnos: botón + panel lateral derecho
            _logBtn = MakeImage(_canvasRt, "LogBtn", new Vector2(1f, 1f), new Vector2(-40f, -180f), new Vector2(110f, 28f), new Color(0.12f, 0.14f, 0.18f, 0.9f));
            _logBtn.rectTransform.pivot = new Vector2(1f, 1f);
            _logBtnLabel = MakeText(_logBtn.rectTransform, "T", "LOG (L)", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(106f, 22f),
                13, new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleCenter);
            _logPanel = MakeImage(_canvasRt, "LogPanel", new Vector2(1f, 1f), new Vector2(-40f, -214f), new Vector2(400f, 460f), new Color(0.03f, 0.04f, 0.06f, 0.82f));
            _logPanel.rectTransform.pivot = new Vector2(1f, 1f);
            _logText = MakeText(_logPanel.rectTransform, "T", "", new Vector2(0f, 1f), new Vector2(12f, -10f), new Vector2(380f, 440f),
                14, new Color(0.9f, 0.92f, 0.95f), TextAnchor.UpperLeft);
            _logText.rectTransform.pivot = new Vector2(0f, 1f);
            _logPanel.gameObject.SetActive(false);
        }

        public void SetTurnSummary(string s) => _turnSummary.text = s;

        public void AddTurnLog(string line)
        {
            _logLines.Insert(0, line);
            if (_logLines.Count > 26) _logLines.RemoveAt(_logLines.Count - 1);
            _logText.text = string.Join("\n", _logLines);
        }

        public void ClearTurnLog()
        {
            _logLines.Clear();
            _logText.text = "";
        }

        public void ShowLagMessage(string msg) => ShowBigMessage(msg, new Color(1f, 0.35f, 0.3f));

        public void ShowBigMessage(string msg, Color c)
        {
            _lagMsg.text = msg;
            _lagMsg.color = c;
            _lagMsgTimer = 2.6f;
        }

        void BuildSide(int i, bool left, string label, Color color)
        {
            float sign = left ? 1f : -1f;
            var anchor = left ? new Vector2(0f, 1f) : new Vector2(1f, 1f);

            MakeText(_canvasRt, label + "Name", label, anchor, new Vector2(sign * 40f, -84f), new Vector2(200f, 24f),
                17, new Color(1f, 1f, 1f, 0.7f), left ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight)
                .rectTransform.pivot = anchor;

            _pips[i] = new Image[SimConfig.MaxHp];
            for (int p = 0; p < SimConfig.MaxHp; p++)
            {
                var pip = MakeImage(_canvasRt, $"{label}Pip{p}", anchor,
                    new Vector2(sign * (40f + p * 46f), -44f), new Vector2(38f, 26f), color);
                pip.rectTransform.pivot = anchor;
                _pips[i][p] = pip;
            }

            // barra de guardia (amarilla) bajo los pips de vida
            var gbg = MakeImage(_canvasRt, label + "GuardBg", anchor,
                new Vector2(sign * 40f, -74f), new Vector2(GuardBarW, 7f), new Color(0f, 0f, 0f, 0.5f));
            gbg.rectTransform.pivot = anchor;
            _guardFill[i] = MakeImage(_canvasRt, label + "Guard", anchor,
                new Vector2(sign * 40f, -74f), new Vector2(GuardBarW, 7f), new Color(1f, 0.85f, 0.25f));
            _guardFill[i].rectTransform.pivot = anchor;

            // rounds ganados (al mejor de 3)
            _winPips[i] = new Image[MatchController.RoundsToWin];
            for (int w = 0; w < MatchController.RoundsToWin; w++)
            {
                var wp = MakeImage(_canvasRt, $"{label}Win{w}", anchor,
                    new Vector2(sign * (300f + w * 26f), -84f), new Vector2(18f, 18f), new Color(1f, 0.85f, 0.3f));
                wp.rectTransform.pivot = anchor;
                _winPips[i][w] = wp;
            }

            _feedback[i] = MakeText(_canvasRt, label + "Feedback", "", anchor, new Vector2(sign * 40f, -116f), new Vector2(640f, 34f),
                24, Color.white, left ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight);
            _feedback[i].rectTransform.pivot = anchor;
            _feedback[i].fontStyle = FontStyle.Bold;

            _stateLabel[i] = MakeText(_canvasRt, label + "State", "", anchor, new Vector2(sign * 40f, -148f), new Vector2(400f, 26f),
                18, Color.white, left ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight);
            _stateLabel[i].rectTransform.pivot = anchor;
            _stateLabel[i].fontStyle = FontStyle.Bold;

            _limbLabel[i] = MakeText(_canvasRt, label + "Limbs", "", anchor, new Vector2(sign * 40f, -176f), new Vector2(400f, 22f),
                15, new Color(1f, 0.45f, 0.35f), left ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight);
            _limbLabel[i].rectTransform.pivot = anchor;
            _limbLabel[i].fontStyle = FontStyle.Bold;
        }

        public void SetPrompt(string s) => _prompt.text = s;
        public void SetBanner(string s) => _bannerOverride = s;

        public void OnMatchReset()
        {
            _fbTimer[0] = _fbTimer[1] = 0f;
            _feedback[0].text = _feedback[1].text = "";
            _banner.text = "";
            _bannerOverride = "";
        }

        public void OnSimEvent(SimEvent ev)
        {
            // los saltos son movimiento: no avisar cuando la patada aérea no conecta
            if (ev.Kind == EvKind.Whiff && (ev.MoveIndex == MoveCatalog.JumpF || ev.MoveIndex == MoveCatalog.JumpN)) return;
            if (ev.Kind == EvKind.Tech)
            {
                Feedback(0, "¡TECH!", new Color(0.5f, 0.95f, 1f));
                Feedback(1, "¡TECH!", new Color(0.5f, 0.95f, 1f));
                return;
            }
            if (ev.Kind == EvKind.LimbLost)
            {
                string limb = ev.Limb == Limb.Arm ? "EL BRAZO" : "LA PIERNA";
                Feedback(1 - ev.Attacker, $"¡PERDISTE {limb}!", new Color(1f, 0.35f, 0.3f));
                Feedback(ev.Attacker, $"¡LE ARRANCASTE {limb}!", new Color(1f, 0.6f, 0.2f));
                return;
            }
            int atk = ev.Attacker;
            string mv = MoveCatalog.All[ev.MoveIndex].Name.ToUpperInvariant();
            string adv = ev.FrameAdv >= 0 ? $"+{ev.FrameAdv}f" : $"−{-ev.FrameAdv}f";
            switch (ev.Kind)
            {
                case EvKind.Hit:
                    Feedback(atk, (ev.Counter ? $"¡COUNTER! {mv}" : mv) + $" −{ev.Damage:0} · {adv}",
                        ev.Counter ? new Color(1f, 0.55f, 0.15f) : Color.white);
                    break;
                case EvKind.Blocked:
                    Feedback(atk, $"{mv}: BLOQUEADO · {adv}", new Color(0.5f, 0.75f, 1f));
                    break;
                case EvKind.Whiff:
                    Feedback(atk, $"{mv}: AL AIRE", new Color(1f, 1f, 1f, 0.55f));
                    break;
                case EvKind.GuardCrush:
                    Feedback(atk, $"{mv}: ¡ROMPIÓ LA GUARDIA! · {adv}", new Color(1f, 0.85f, 0.2f));
                    break;
            }
        }

        public void Feedback(int side, string msg, Color c)
        {
            _feedback[side].text = msg;
            _feedback[side].color = c;
            _fbTimer[side] = 1.8f;
        }

        void Update()
        {
            var sim = _mc.Sim;
            if (sim == null) return;

            for (int i = 0; i < 2; i++)
            {
                for (int p = 0; p < SimConfig.MaxHp; p++)
                {
                    var c = _pips[i][p].color;
                    c.a = p < sim.Fighters[i].Hp ? 1f : 0.15f;
                    _pips[i][p].color = c;
                }

                // miembros perdidos, siempre a la vista
                var lf = sim.Fighters[i];
                _limbLabel[i].text = lf.ArmHp <= 0f && lf.LegHp <= 0f ? "SIN BRAZO · SIN PIERNA"
                                   : lf.ArmHp <= 0f ? "SIN BRAZO (ni A ni hadouken)"
                                   : lf.LegHp <= 0f ? "SIN PIERNA (sin patadas, lento)" : "";

                // guardia: se encoge y por debajo del 25% parpadea en rojo
                float g = sim.Fighters[i].Guard / SimConfig.GuardMax;
                _guardFill[i].rectTransform.sizeDelta = new Vector2(GuardBarW * g, 7f);
                _guardFill[i].color = g <= 0.25f
                    ? Color.Lerp(new Color(1f, 0.85f, 0.25f), new Color(1f, 0.2f, 0.15f), Mathf.PingPong(Time.time * 4f, 1f))
                    : new Color(1f, 0.85f, 0.25f);
                if (_fbTimer[i] > 0f)
                {
                    _fbTimer[i] -= Time.deltaTime;
                    var c = _feedback[i].color;
                    c.a = Mathf.Clamp01(_fbTimer[i] / 0.4f);
                    _feedback[i].color = c;
                }

                // estado con framedata en vivo
                if (sim.IsStunned(i))
                {
                    int rem = sim.StunRemaining(i);
                    switch (sim.Fighters[i].Stun)
                    {
                        case StunKind.Knockdown:
                            _stateLabel[i].text = $"KNOCKDOWN {rem}f";
                            _stateLabel[i].color = new Color(1f, 0.5f, 0.2f);
                            break;
                        case StunKind.Blockstun:
                            _stateLabel[i].text = $"BLOCKSTUN {rem}f";
                            _stateLabel[i].color = new Color(0.5f, 0.75f, 1f);
                            break;
                        default:
                            _stateLabel[i].text = $"HITSTUN {rem}f";
                            _stateLabel[i].color = new Color(1f, 0.35f, 0.3f);
                            break;
                    }
                }
                else
                {
                    _stateLabel[i].text = sim.IsBlockingState(i) && (_mc.State == MatchController.Flow.Executing || _mc.State == MatchController.Flow.Replay)
                        ? "bloqueando" : "";
                    _stateLabel[i].color = new Color(0.55f, 0.8f, 1f, 0.7f);
                }
            }

            bool executing = _mc.State == MatchController.Flow.Executing || _mc.State == MatchController.Flow.Replay;
            float playX = executing ? Mathf.Clamp((_mc.TickFloat - _mc.TurnStartTick) * PxPerFrame, 0f, RowW) : -1f;
            _row0.UpdateRow(_mc.GetPlan(0), _mc.RowRevealed(0), playX, _mc.DisplayStun(0), _mc.TurnStartStunKind[0]);
            _row1.UpdateRow(_mc.GetPlan(1), _mc.RowRevealed(1), playX, _mc.DisplayStun(1), _mc.TurnStartStunKind[1]);

            // distancia, rounds ganados y toggle de cajas
            _dist.text = $"dist {Mathf.Abs(sim.Fighters[1].X - sim.Fighters[0].X):0.00}";
            for (int i = 0; i < 2; i++)
                for (int w = 0; w < MatchController.RoundsToWin; w++)
                {
                    var c = _winPips[i][w].color;
                    c.a = w < _mc.GetWins(i) ? 1f : 0.15f;
                    _winPips[i][w].color = c;
                }

            // cartel de lag con fade
            if (_lagMsgTimer > 0f)
            {
                _lagMsgTimer -= Time.deltaTime;
                var lc = _lagMsg.color;
                lc.a = Mathf.Clamp01(_lagMsgTimer / 0.6f);
                _lagMsg.color = lc;
                if (_lagMsgTimer <= 0f) _lagMsg.text = "";
            }

            // wifi agonizante
            bool wifi = _mc.LagMode;
            int lvl = _mc.LagLevel;
            int alive = Mathf.Max(4 - lvl, 1);
            var barColor = lvl == 0 ? new Color(0.35f, 0.9f, 0.4f) :
                           lvl == 1 ? new Color(0.85f, 0.85f, 0.3f) :
                           lvl == 2 ? new Color(0.95f, 0.6f, 0.2f) : new Color(0.95f, 0.25f, 0.2f);
            for (int b = 0; b < 4; b++)
            {
                _wifiBars[b].gameObject.SetActive(wifi);
                if (!wifi) continue;
                bool on = b < alive;
                var c = on ? barColor : new Color(1f, 1f, 1f, 0.12f);
                if (on && lvl >= 3) c.a = 0.45f + Mathf.PingPong(Time.time * 2.2f, 0.55f); // parpadeo agónico
                _wifiBars[b].color = c;
            }
            _wifiLabel.gameObject.SetActive(wifi);
            if (wifi) _wifiLabel.text = $"ping {_mc.CurrentTurnFrames * 16}ms";

            bool toggle = GameInput.BoxesPressed();
            if (!toggle && GameInput.ClickPressed() &&
                RectTransformUtility.RectangleContainsScreenPoint(_boxBtn.rectTransform, GameInput.MousePos(), null))
                toggle = true;
            if (toggle)
            {
                VizPrefs.ShowBoxes = !VizPrefs.ShowBoxes;
                _boxBtnLabel.text = VizPrefs.ShowBoxes ? "CAJAS: ON" : "CAJAS: OFF";
                _boxBtnLabel.color = VizPrefs.ShowBoxes ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 1f, 1f, 0.5f);
            }

            // toggle del announcer (KO / guard crush)
            if (GameInput.ClickPressed() &&
                RectTransformUtility.RectangleContainsScreenPoint(_voiceBtn.rectTransform, GameInput.MousePos(), null))
            {
                Announcer.Enabled = !Announcer.Enabled;
                _voiceBtnLabel.text = Announcer.Enabled ? "VOZ: ON" : "VOZ: OFF";
                _voiceBtnLabel.color = Announcer.Enabled ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 1f, 1f, 0.5f);
            }

            // velocidad de playback: highlight del activo + clicks
            for (int s = 0; s < Speeds.Length; s++)
            {
                bool on = Mathf.Approximately(_mc.PlaybackSpeed, Speeds[s]);
                _speedBtns[s].color = on ? new Color(0.2f, 0.4f, 0.6f, 0.95f) : new Color(0.12f, 0.14f, 0.18f, 0.9f);
                _speedLabels[s].color = on ? Color.white : new Color(1f, 1f, 1f, 0.55f);
                if (GameInput.ClickPressed() &&
                    RectTransformUtility.RectangleContainsScreenPoint(_speedBtns[s].rectTransform, GameInput.MousePos(), null))
                    _mc.SetPlaybackSpeed(Speeds[s]);
            }

            // log de turnos: L o click en el botón
            bool logToggle = GameInput.LogPressed();
            if (!logToggle && GameInput.ClickPressed() &&
                RectTransformUtility.RectangleContainsScreenPoint(_logBtn.rectTransform, GameInput.MousePos(), null))
                logToggle = true;
            if (logToggle)
            {
                _logOpen = !_logOpen;
                _logPanel.gameObject.SetActive(_logOpen);
                _logBtnLabel.color = _logOpen ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 1f, 1f, 0.7f);
            }

            if (_bannerOverride != "")
                _banner.text = _bannerOverride;
            else if (_mc.State == MatchController.Flow.GameOver)
                _banner.text = (sim.Winner == 0 ? "¡GANASTE LA PELEA!" : sim.Winner == 1 ? "PERDISTE LA PELEA" : "DOBLE KO")
                               + $"\n<size=30>{_mc.GetWins(0)} — {_mc.GetWins(1)}</size>"
                               + "\n<size=26>V repetir último round · R revancha · M menú</size>";
            else
                _banner.text = "";
        }

        public static Color ChipColor(int moveIndex)
        {
            switch (moveIndex)
            {
                case MoveCatalog.AttackA: return new Color(0.9f, 0.32f, 0.24f);
                case MoveCatalog.AttackB: return new Color(0.65f, 0.3f, 0.85f);
                case MoveCatalog.Hadouken: return new Color(0.25f, 0.55f, 0.95f);
                case MoveCatalog.Shoryuken: return new Color(0.95f, 0.7f, 0.15f);
                case MoveCatalog.JumpF:
                case MoveCatalog.JumpN:
                case MoveCatalog.JumpB: return new Color(0.55f, 0.8f, 0.35f);
                case MoveCatalog.DashF:
                case MoveCatalog.DashB: return new Color(0.2f, 0.72f, 0.72f);
                case MoveCatalog.Tatsu: return new Color(0.9f, 0.45f, 0.15f);
                case MoveCatalog.Grab: return new Color(0.85f, 0.3f, 0.75f);
                case MoveCatalog.Wait: return new Color(0.45f, 0.47f, 0.52f);
                default: return new Color(0.25f, 0.72f, 0.45f); // caminar
            }
        }

        public static string ChipLabel(int moveIndex)
        {
            switch (moveIndex)
            {
                case MoveCatalog.AttackA: return "A";
                case MoveCatalog.AttackB: return "B";
                case MoveCatalog.Hadouken: return "HD";
                case MoveCatalog.Shoryuken: return "DP";
                case MoveCatalog.JumpF: return "J→";
                case MoveCatalog.JumpN: return "J";
                case MoveCatalog.JumpB: return "J←";
                case MoveCatalog.WalkF: return "→";
                case MoveCatalog.WalkB: return "←";
                case MoveCatalog.DashF: return "»";
                case MoveCatalog.DashB: return "«";
                case MoveCatalog.Tatsu: return "T";
                case MoveCatalog.Grab: return "G";
                default: return "·";
            }
        }

        // Fila de timeline: fichas secuenciales de los comandos del turno.
        class TimelineRow
        {
            readonly HudUI _hud;
            readonly RectTransform _area;
            readonly RectTransform _chipParent;
            readonly Image _playhead;
            readonly Image _stunSeg;
            readonly Text _stunLabel;
            readonly Text _hidden;
            readonly float _height;
            readonly bool _dim;
            readonly List<Image> _chips = new List<Image>();
            readonly List<Text> _labels = new List<Text>();

            public TimelineRow(HudUI hud, string name, float y, float height, bool dim)
            {
                _hud = hud;
                _height = height;
                _dim = dim;

                var bg = hud.MakeImage(hud._canvasRt, name, new Vector2(0.5f, 0f), new Vector2(0f, y), new Vector2(RowW, height), new Color(0f, 0f, 0f, dim ? 0.4f : 0.55f));
                _area = bg.rectTransform;
                _area.gameObject.AddComponent<RectMask2D>();

                var chipGo = new GameObject("Chips", typeof(RectTransform));
                _chipParent = chipGo.GetComponent<RectTransform>();
                _chipParent.SetParent(_area, false);
                _chipParent.anchorMin = Vector2.zero;
                _chipParent.anchorMax = Vector2.one;
                _chipParent.offsetMin = _chipParent.offsetMax = Vector2.zero;

                _hidden = hud.MakeText(_area, "Hidden", "? ? ?", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 24f),
                    18, new Color(1f, 1f, 1f, 0.35f), TextAnchor.MiddleCenter);

                // stun arrastrado del turno anterior: te come el arranque del turno
                _stunSeg = hud.MakeImage(_area, "StunSeg", new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, height - 4f), Color.white);
                _stunSeg.rectTransform.pivot = new Vector2(0f, 0.5f);
                _stunLabel = hud.MakeText(_stunSeg.rectTransform, "L", "", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 22f),
                    14, Color.white, TextAnchor.MiddleCenter);
                _stunLabel.fontStyle = FontStyle.Bold;

                _playhead = hud.MakeImage(_area, "Playhead", new Vector2(0f, 0.5f), Vector2.zero, new Vector2(3f, height), Color.white);
                _playhead.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            }

            public void UpdateRow(List<int> queue, bool revealed, float playX, int stunFrames, StunKind stunKind)
            {
                _hidden.gameObject.SetActive(!revealed);
                _playhead.gameObject.SetActive(playX >= 0f);
                if (playX >= 0f) _playhead.rectTransform.anchoredPosition = new Vector2(playX, 0f);

                // el stun arrastrado se marca al inicio de la fila (es info pública)
                float px = _hud.PxPerFrame;
                float offset = 0f;
                if (stunFrames > 0)
                {
                    offset = stunFrames * px;
                    _stunSeg.gameObject.SetActive(true);
                    _stunSeg.rectTransform.sizeDelta = new Vector2(offset - 2f, _height - 4f);
                    _stunSeg.color = stunKind == StunKind.Blockstun ? new Color(0.3f, 0.5f, 0.85f, 0.75f)
                                   : stunKind == StunKind.Knockdown ? new Color(0.9f, 0.45f, 0.15f, 0.8f)
                                   : new Color(0.85f, 0.25f, 0.22f, 0.8f);
                    _stunLabel.text = offset > 46f ? $"−{stunFrames}f" : "";
                }
                else
                {
                    _stunSeg.gameObject.SetActive(false);
                }

                int used = 0;
                if (revealed && queue != null)
                {
                    float x = offset; // las órdenes recién arrancan cuando termina el stun
                    foreach (var mi in queue)
                    {
                        var m = MoveCatalog.All[mi];
                        float w = m.Total * px - 2f;
                        var chip = GetChip(used++);
                        chip.rectTransform.anchoredPosition = new Vector2(x, 0f);
                        chip.rectTransform.sizeDelta = new Vector2(w, _height - 8f);
                        var c = ChipColor(mi);
                        if (_dim) c = new Color(c.r, c.g, c.b, 0.8f);
                        chip.color = c;
                        _labels[used - 1].text = ChipLabel(mi);
                        x += m.Total * px;
                    }
                }
                for (int i = used; i < _chips.Count; i++)
                    _chips[i].gameObject.SetActive(false);
            }

            Image GetChip(int i)
            {
                while (i >= _chips.Count)
                {
                    var img = _hud.MakeImage(_chipParent, "Chip", new Vector2(0f, 0.5f), Vector2.zero, Vector2.one, Color.white);
                    img.rectTransform.pivot = new Vector2(0f, 0.5f);
                    var t = _hud.MakeText(img.rectTransform, "L", "", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60f, 26f), 18, Color.white, TextAnchor.MiddleCenter);
                    t.fontStyle = FontStyle.Bold;
                    _chips.Add(img);
                    _labels.Add(t);
                }
                _chips[i].gameObject.SetActive(true);
                return _chips[i];
            }
        }

        // ---------- helpers ----------

        public Image MakeImage(RectTransform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public Text MakeText(RectTransform parent, string name, string content, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize, Color color, TextAnchor align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var t = go.GetComponent<Text>();
            t.font = _font;
            t.text = content;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }
    }
}
