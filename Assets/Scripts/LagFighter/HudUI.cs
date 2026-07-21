using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LagFighter
{
    // HUD con la identidad "netplay roto" (UI_PLAN.md):
    //  - fuente pixel para labels cortos, cuerpo para texto largo
    //  - bloques de vida protagonistas con panel y borde por jugador
    //  - tira de conexión (dist + ping + wifi) estilo overlay de stream
    //  - feedback de golpes EN el mundo (WorldFX), no en las esquinas
    //  - overlay frío durante la planificación, flash al ejecutar
    //  - timeline con scrub (arrastrar = posar el ghost) y click derecho
    //    para borrar una orden puntual
    //  - ajustes agrupados en [OPC] y botones de fin de partida
    public class HudUI : MonoBehaviour
    {
        public const float RowW = 1440f;
        // La escala de la timeline se ANIMA cuando sube el lag: ves tus fichas
        // comprimirse mientras aparece el espacio nuevo (que además se destaca).
        float _shownTurnFrames = SimConfig.TurnFrames;
        float PxPerFrame => RowW / _shownTurnFrames;

        MatchController _mc;
        Font _font;      // cuerpo (Liberation): texto largo y símbolos ←»
        Font _pixel;     // Press Start 2P: labels cortos, jerarquía
        RectTransform _canvasRt;
        readonly Image[][] _pips = new Image[2][];
        readonly Image[][] _winPips = new Image[2][];
        readonly Image[] _guardFill = new Image[2];
        readonly Image[] _superFill = new Image[2];
        readonly Image[] _superBg = new Image[2]; // se oculta con el turno fluido apagado

        // ---- modo YOMI: circulitos de AP + cartas de revelación ----
        readonly Image[][] _apPips = new Image[2][];
        readonly Text[] _apLabel = new Text[2];
        readonly Image[] _yomiCard = new Image[2];
        readonly Image[] _yomiCardEdge = new Image[2];
        readonly Text[] _yomiCardName = new Text[2];
        readonly Text[] _yomiCardInfo = new Text[2];
        Text _yomiVs;
        Text _yomiExplain; // el fallo del turno: qué regla de la matriz aplicó
        float _yomiPop;   // entrada de las cartas (0..1, con overshoot)
        float _yomiDock;  // 0 = grandes al centro · 1 = chicas al costado
        bool _yomiDocked;
        static Sprite _circleSprite;
        const float PipW = 42f, PipGap = 46f;
        const float GuardBarW = SimConfig.MaxHp * PipGap - (PipGap - PipW);
        Text _banner, _prompt, _turnSummary, _planTimerText;
        string _bannerOverride = "";
        int _lastPlanTimerShown = -2;

        // tira de conexión
        Text _connText;
        readonly Image[] _wifiBars = new Image[4];

        // ajustes [OPC]
        Image _optBtn, _optPanel;
        Text _optBtnLabel;
        bool _optOpen;
        Image _boxBtn, _voiceBtn, _sfxBtn;
        Text _boxBtnLabel, _voiceBtnLabel, _sfxBtnLabel;
        static readonly float[] Speeds = { 0.5f, 1f, 2f };
        readonly Image[] _speedBtns = new Image[3];
        readonly Text[] _speedLabels = new Text[3];

        // log lateral de turnos, colapsable con L (o desde OPC)
        Image _logBtn, _logPanel;
        Text _logBtnLabel, _logText;
        bool _logOpen;
        readonly List<string> _logLines = new List<string>();

        // carteles grandes en dos slots: 0 = combate (COUNTER, K.O.), 1 = sistema
        // (subidas de lag). Separados para que no se pisen entre sí.
        readonly Text[] _bigMsg = new Text[2];
        readonly float[] _bigMsgTimer = new float[2];
        readonly Text[] _limbLabel = new Text[2];
        TimelineRow _row0, _row1;

        // animación de pips de vida al romperse (flash + pop + fade)
        readonly int[] _shownHp = { -1, -1 };
        readonly float[][] _pipAnim = new float[2][];

        // tip contextual del modo práctica (lo maneja MatchController)
        Text _tip;

        // botones con hover: tinte + tick al entrar
        readonly List<Image> _hoverBtns = new List<Image>();
        readonly List<Color> _hoverBase = new List<Color>();
        Image _hovered;

        // overlay de planificación + flash de ejecución
        Image _planOverlay;
        MatchController.Flow _prevFlow = MatchController.Flow.ModeSelect;

        // FX de subida de lag: glitch de pantalla + highlight del espacio nuevo
        int _prevLagLevel;
        float _lagFxTimer;          // glitch fuerte los primeros ~0.7s
        readonly Image[] _glitchBars = new Image[6];

        // botones de fin de partida
        Image _btnRematch, _btnReplay, _btnMenu;

        // cartel de replay + botón SKIP (el replay corre siempre, saltearlo es opcional)
        Image _replayPanel, _skipBtn;
        Text _replayTitle;
        bool _replayStalled;
        int _fakePing;      // ping spike falso durante el tirón del replay
        bool _connSpiked;

        // modos de visualización del replay: LAG / NORMAL / RÁPIDO, en vivo
        static readonly string[] ReplayModeNames = { "LAG", "NORMAL", "RÁPIDO" };
        readonly Image[] _replayModeBtns = new Image[3];
        readonly Text[] _replayModeLabels = new Text[3];

        // caches para no armar strings por frame (WebGL sufre el GC)
        float _lastConnDist = -1f;
        int _lastConnPing = -1;

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
            hud._font = UIFonts.Body;
            hud._pixel = UIFonts.Pixel;
            hud.BuildAll();
            return hud;
        }

        void BuildAll()
        {
            // overlay frío de planificación: primero, así queda detrás de todo
            // (alfa bajito: es una insinuación de "tiempo pausado", no un filtro)
            _planOverlay = MakeImage(_canvasRt, "PlanOverlay", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.25f, 0.5f, 0.9f, 0.03f));
            var por = _planOverlay.rectTransform;
            por.anchorMin = Vector2.zero;
            por.anchorMax = Vector2.one;
            por.offsetMin = por.offsetMax = Vector2.zero;

            BuildSide(0, left: true, "VOS");
            BuildSide(1, left: false, "RIVAL");

            // tira de conexión (dist + ping + wifi), estética overlay de stream
            var strip = MakePanel(_canvasRt, "ConnStrip", new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(430f, 34f), Palette.Neutral);
            _connText = MakeTextP(strip.rectTransform, "Conn", "", new Vector2(0.5f, 0.5f), new Vector2(-24f, 0f), new Vector2(360f, 22f),
                8, new Color(1f, 1f, 1f, 0.8f), TextAnchor.MiddleCenter);
            for (int b = 0; b < 4; b++)
            {
                _wifiBars[b] = MakeImage(strip.rectTransform, "Wifi" + b, new Vector2(1f, 0f),
                    new Vector2(-52f + b * 11f, 7f), new Vector2(7f, 6f + b * 6f), Palette.Ok);
                _wifiBars[b].rectTransform.pivot = new Vector2(0.5f, 0f);
            }

            _prompt = MakeTextP(_canvasRt, "Prompt", "", new Vector2(0.5f, 1f), new Vector2(0f, -76f), new Vector2(1500f, 30f),
                16, Palette.Startup, TextAnchor.MiddleCenter);

            // timer de planificación (online / 1v1): grande, a la izquierda del
            // bloque del rival (antes quedaba ABAJO del panel y la vida lo tapaba)
            _planTimerText = MakeTextP(_canvasRt, "PlanTimer", "", new Vector2(1f, 1f), new Vector2(-352f, -40f), new Vector2(200f, 44f),
                22, Palette.Guard, TextAnchor.MiddleRight);
            _planTimerText.rectTransform.pivot = new Vector2(1f, 0.5f);

            _turnSummary = MakeText(_canvasRt, "TurnSummary", "", new Vector2(0.5f, 1f), new Vector2(0f, -102f), new Vector2(1400f, 22f),
                16, new Color(0.85f, 0.9f, 1f, 0.8f), TextAnchor.MiddleCenter);

            // timelines del turno (fila propia abajo, rival arriba) — LAS
            // protagonistas: acá se cargan los movimientos, que se vean bien
            _row1 = new TimelineRow(this, "Row1", y: 246f, height: 52f, dim: true, side: 1);
            _row0 = new TimelineRow(this, "Row0", y: 312f, height: 52f, dim: false, side: 0);
            MakeTextP(_canvasRt, "Row0Label", "VOS", new Vector2(0.5f, 0f), new Vector2(-RowW / 2f - 52f, 312f + 26f), new Vector2(90f, 20f),
                8, Palette.P1, TextAnchor.MiddleRight);
            MakeTextP(_canvasRt, "Row1Label", "RIVAL", new Vector2(0.5f, 0f), new Vector2(-RowW / 2f - 52f, 246f + 26f), new Vector2(90f, 20f),
                8, Palette.P2, TextAnchor.MiddleRight);

            _banner = MakeText(_canvasRt, "Banner", "", new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(1200f, 160f),
                58, Color.white, TextAnchor.MiddleCenter);
            _banner.fontStyle = FontStyle.Bold;

            // carteles grandes: slot 0 combate (COUNTER / K.O.), slot 1 sistema (lag)
            _bigMsg[0] = MakeTextP(_canvasRt, "BigMsg", "", new Vector2(0.5f, 0.5f), new Vector2(0f, 280f), new Vector2(1600f, 120f),
                32, new Color(1f, 0.35f, 0.3f), TextAnchor.MiddleCenter);
            _bigMsg[1] = MakeTextP(_canvasRt, "SysMsg", "", new Vector2(0.5f, 0.5f), new Vector2(0f, 396f), new Vector2(1600f, 100f),
                24, new Color(1f, 0.35f, 0.3f), TextAnchor.MiddleCenter);

            // tip contextual (práctica): entre el menú de plan y las timelines
            _tip = MakeText(_canvasRt, "Tip", "", new Vector2(0.5f, 0f), new Vector2(0f, 396f), new Vector2(1400f, 24f),
                16, new Color(0.55f, 1f, 0.65f, 0.9f), TextAnchor.MiddleCenter);

            // cartas de revelación del modo YOMI: entran GRANDES al centro
            // ("esto eligió cada uno"), y durante la acción se van chicas al
            // costado para que se lea qué está haciendo cada lado.
            for (int i = 0; i < 2; i++)
            {
                bool left = i == 0;
                _yomiCard[i] = MakeImage(_canvasRt, "YomiCard" + i, new Vector2(0.5f, 0.5f),
                    new Vector2(left ? -310f : 310f, 120f), new Vector2(340f, 170f), new Color(0.05f, 0.06f, 0.09f, 0.96f));
                _yomiCardEdge[i] = MakeImage(_yomiCard[i].rectTransform, "Edge", new Vector2(0.5f, 1f),
                    new Vector2(0f, -6f), new Vector2(328f, 10f), Color.white);
                MakeTextP(_yomiCard[i].rectTransform, "Who", left ? "VOS" : "RIVAL", new Vector2(0.5f, 1f),
                    new Vector2(0f, -30f), new Vector2(300f, 22f), 8,
                    left ? new Color(0.55f, 0.8f, 1f) : new Color(1f, 0.63f, 0.5f), TextAnchor.MiddleCenter);
                _yomiCardName[i] = MakeTextP(_yomiCard[i].rectTransform, "Name", "", new Vector2(0.5f, 0.5f),
                    new Vector2(0f, 4f), new Vector2(330f, 44f), 24, Color.white, TextAnchor.MiddleCenter);
                _yomiCardInfo[i] = MakeTextP(_yomiCard[i].rectTransform, "Info", "", new Vector2(0.5f, 0f),
                    new Vector2(0f, 28f), new Vector2(330f, 22f), 8, new Color(1f, 1f, 1f, 0.75f), TextAnchor.MiddleCenter);
                _yomiCard[i].gameObject.SetActive(false);
            }
            _yomiVs = MakeTextP(_canvasRt, "YomiVs", "VS", new Vector2(0.5f, 0.5f), new Vector2(0f, 120f),
                new Vector2(160f, 60f), 32, new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleCenter);
            _yomiVs.gameObject.SetActive(false);
            _yomiExplain = MakeTextP(_canvasRt, "YomiExplain", "", new Vector2(0.5f, 0.5f), new Vector2(0f, 8f),
                new Vector2(1500f, 40f), 16, new Color(1f, 0.9f, 0.5f), TextAnchor.MiddleCenter);
            _yomiExplain.gameObject.SetActive(false);

            // REPLAY + SKIP, arriba al medio (solo visible durante la repetición)
            _replayPanel = MakePanel(_canvasRt, "ReplayPanel", new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(430f, 44f), Palette.Damage);
            _replayTitle = MakeTextP(_replayPanel.rectTransform, "T", "► REPLAY", new Vector2(0f, 0.5f), new Vector2(120f, 0f), new Vector2(220f, 24f),
                16, new Color(1f, 0.4f, 0.35f), TextAnchor.MiddleLeft);
            _skipBtn = MakeImage(_replayPanel.rectTransform, "SkipBtn", new Vector2(1f, 0.5f), new Vector2(-84f, 0f), new Vector2(150f, 32f), new Color(0.1f, 0.12f, 0.16f, 0.95f));
            MakeTextP(_skipBtn.rectTransform, "T", "SKIP — ESPACIO", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(146f, 20f),
                8, new Color(1f, 1f, 1f, 0.85f), TextAnchor.MiddleCenter);
            // botones GRANDES de modo de replay, colgados del panel (se muestran
            // y ocultan con él): cambian cómo se ve la repetición en vivo
            for (int m = 0; m < 3; m++)
            {
                var btn = MakeImage(_replayPanel.rectTransform, "Mode" + m, new Vector2(0.5f, 0f),
                    new Vector2((m - 1) * 168f, -36f), new Vector2(158f, 50f), new Color(0.1f, 0.12f, 0.16f, 0.95f));
                var ol = btn.gameObject.AddComponent<Outline>();
                ol.effectColor = new Color(1f, 1f, 1f, 0.18f);
                ol.effectDistance = new Vector2(1.5f, -1.5f);
                _replayModeBtns[m] = btn;
                _replayModeLabels[m] = MakeTextP(btn.rectTransform, "T", ReplayModeNames[m],
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(154f, 24f),
                    16, Color.white, TextAnchor.MiddleCenter);
            }
            _replayPanel.gameObject.SetActive(false);
            RegisterHover(_skipBtn);

            // ajustes [OPC] colapsados, esquina derecha bajo el bloque del rival
            _optBtn = MakePanel(_canvasRt, "OptBtn", new Vector2(1f, 1f), new Vector2(-28f, -150f), new Vector2(96f, 30f), Palette.Neutral);
            _optBtn.rectTransform.pivot = new Vector2(1f, 1f);
            _optBtnLabel = MakeTextP(_optBtn.rectTransform, "T", "OPC", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(92f, 20f),
                8, new Color(1f, 1f, 1f, 0.75f), TextAnchor.MiddleCenter);
            RegisterHover(_optBtn);

            _optPanel = MakePanel(_canvasRt, "OptPanel", new Vector2(1f, 1f), new Vector2(-28f, -186f), new Vector2(210f, 212f), Palette.Neutral);
            _optPanel.rectTransform.pivot = new Vector2(1f, 1f);
            var opr = _optPanel.rectTransform;

            _boxBtn = MakeImage(opr, "BoxBtn", new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(186f, 30f), new Color(0.1f, 0.12f, 0.16f, 0.95f));
            _boxBtnLabel = MakeTextP(_boxBtn.rectTransform, "T", VizPrefs.ShowBoxes ? "CAJAS: ON" : "CAJAS: OFF",
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(182f, 20f),
                8, VizPrefs.ShowBoxes ? Palette.Ok : new Color(1f, 1f, 1f, 0.5f), TextAnchor.MiddleCenter);
            RegisterHover(_boxBtn);

            _voiceBtn = MakeImage(opr, "VoiceBtn", new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(186f, 30f), new Color(0.1f, 0.12f, 0.16f, 0.95f));
            _voiceBtnLabel = MakeTextP(_voiceBtn.rectTransform, "T", "VOZ: ON", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(182f, 20f),
                8, Palette.Ok, TextAnchor.MiddleCenter);
            RegisterHover(_voiceBtn);

            _sfxBtn = MakeImage(opr, "SfxBtn", new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(186f, 30f), new Color(0.1f, 0.12f, 0.16f, 0.95f));
            _sfxBtnLabel = MakeTextP(_sfxBtn.rectTransform, "T", SfxLib.Enabled ? "SFX: ON" : "SFX: OFF",
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(182f, 20f),
                8, SfxLib.Enabled ? Palette.Ok : new Color(1f, 1f, 1f, 0.5f), TextAnchor.MiddleCenter);
            RegisterHover(_sfxBtn);

            _logBtn = MakeImage(opr, "LogBtn", new Vector2(0.5f, 1f), new Vector2(0f, -132f), new Vector2(186f, 30f), new Color(0.1f, 0.12f, 0.16f, 0.95f));
            _logBtnLabel = MakeTextP(_logBtn.rectTransform, "T", "LOG (L)", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(182f, 20f),
                8, new Color(1f, 1f, 1f, 0.75f), TextAnchor.MiddleCenter);
            RegisterHover(_logBtn);

            MakeTextP(opr, "SpeedTag", "VEL", new Vector2(0f, 1f), new Vector2(14f, -182f), new Vector2(50f, 20f),
                8, new Color(1f, 1f, 1f, 0.5f), TextAnchor.MiddleLeft);
            for (int s = 0; s < Speeds.Length; s++)
            {
                _speedBtns[s] = MakeImage(opr, "Speed" + s, new Vector2(0f, 1f),
                    new Vector2(58f + s * 48f, -182f), new Vector2(44f, 26f), new Color(0.1f, 0.12f, 0.16f, 0.95f));
                _speedBtns[s].rectTransform.pivot = new Vector2(0f, 0.5f);
                _speedLabels[s] = MakeText(_speedBtns[s].rectTransform, "T", Speeds[s] == 0.5f ? "×½" : $"×{Speeds[s]:0}",
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(42f, 22f), 14, Color.white, TextAnchor.MiddleCenter);
            }
            _optPanel.gameObject.SetActive(false);

            // panel del log
            _logPanel = MakePanel(_canvasRt, "LogPanel", new Vector2(1f, 1f), new Vector2(-28f, -370f), new Vector2(400f, 420f), Palette.Neutral);
            _logPanel.rectTransform.pivot = new Vector2(1f, 1f);
            _logText = MakeText(_logPanel.rectTransform, "T", "", new Vector2(0f, 1f), new Vector2(12f, -10f), new Vector2(380f, 400f),
                14, new Color(0.9f, 0.92f, 0.95f), TextAnchor.UpperLeft);
            _logText.rectTransform.pivot = new Vector2(0f, 1f);
            _logPanel.gameObject.SetActive(false);

            // botones de fin de partida (aparecen en GameOver)
            _btnRematch = MakeButton("REVANCHA", new Vector2(-190f, -40f), Palette.Ok);
            _btnReplay = MakeButton("REPLAY", new Vector2(0f, -40f), Palette.Block);
            _btnMenu = MakeButton("MENU", new Vector2(190f, -40f), Palette.Neutral);
            SetGameOverButtons(false);

            // barras de glitch (subida de lag): franjas que parpadean un instante
            for (int g = 0; g < _glitchBars.Length; g++)
            {
                _glitchBars[g] = MakeImage(_canvasRt, "Glitch" + g, new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(2200f, 8f), new Color(0.6f, 0.9f, 1f, 0.25f));
                _glitchBars[g].gameObject.SetActive(false);
            }
        }

        Image MakeButton(string label, Vector2 pos, Color accent)
        {
            var b = MakePanel(_canvasRt, "Btn" + label, new Vector2(0.5f, 0.5f), pos, new Vector2(170f, 44f), accent);
            MakeTextP(b.rectTransform, "T", label, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(166f, 24f),
                8, Color.white, TextAnchor.MiddleCenter);
            RegisterHover(b);
            return b;
        }

        // ---------- hover de botones: tinte + tick al entrar ----------

        void RegisterHover(Image img)
        {
            _hoverBtns.Add(img);
            _hoverBase.Add(img.color);
        }

        void UpdateHover()
        {
            var mp = GameInput.MousePos();
            Image now = null;
            for (int i = 0; i < _hoverBtns.Count; i++)
            {
                var b = _hoverBtns[i];
                if (!b.gameObject.activeInHierarchy) continue;
                bool over = Inside(b, mp);
                b.color = over ? Color.Lerp(_hoverBase[i], Color.white, 0.22f) : _hoverBase[i];
                if (over && now == null) now = b;
            }
            if (now != _hovered)
            {
                _hovered = now;
                if (now != null) SfxLib.Play(SfxLib.Kind.UiTick, 0.35f);
            }
        }

        void SetGameOverButtons(bool on)
        {
            _btnRematch.gameObject.SetActive(on);
            _btnReplay.gameObject.SetActive(on && _mc.HasReplay);
            _btnMenu.gameObject.SetActive(on);
        }

        public void SetTurnSummary(string s) => _turnSummary.text = s;

        // -1 = ocultar; <=10s se pone rojo y grita
        public void SetPlanTimer(int seconds)
        {
            if (seconds == _lastPlanTimerShown) return;
            _lastPlanTimerShown = seconds;
            _planTimerText.text = seconds < 0 ? "" : $"{seconds}s";
            _planTimerText.color = seconds >= 0 && seconds <= 10 ? Palette.Damage : Palette.Guard;
        }

        public void AddTurnLog(string line)
        {
            _logLines.Insert(0, line);
            if (_logLines.Count > 24) _logLines.RemoveAt(_logLines.Count - 1);
            _logText.text = string.Join("\n", _logLines);
        }

        public void ClearTurnLog()
        {
            _logLines.Clear();
            _logText.text = "";
        }

        // los avisos de lag van al slot de sistema: no pisan COUNTER/K.O. ni al revés
        public void ShowLagMessage(string msg) => ShowBigMessage(msg, new Color(1f, 0.35f, 0.3f), 2.6f, 1);

        // lag teatral del replay: franjas de glitch prestadas del efecto de subida
        // de lag, y el cartel del replay pasa a "|| LAG..." mientras está trabado
        public void GlitchBurst(float dur) => _lagFxTimer = Mathf.Max(_lagFxTimer, dur);

        public void SetReplayStalled(bool on)
        {
            if (on == _replayStalled) return;
            _replayStalled = on;
            _replayTitle.text = on ? "|| LAG..." : "► REPLAY";
            if (on) _fakePing = Random.Range(1800, 4800); // cada tirón, su spike
        }

        public void ShowBigMessage(string msg, Color c, float duration = 2.6f, int slot = 0)
        {
            _bigMsg[slot].text = msg;
            _bigMsg[slot].color = c;
            _bigMsgTimer[slot] = duration;
        }

        public void SetTip(string s) => _tip.text = s ?? "";

        void BuildSide(int i, bool left, string label)
        {
            float sign = left ? 1f : -1f;
            var anchor = left ? new Vector2(0f, 1f) : new Vector2(1f, 1f);
            var color = Palette.Side(i);

            // panel contenedor del bloque de jugador
            var panel = MakePanel(_canvasRt, label + "Panel", anchor, new Vector2(sign * 24f, -22f), new Vector2(GuardBarW + 32f, 124f), color);
            panel.rectTransform.pivot = anchor;
            var pr = panel.rectTransform;

            var nm = MakeTextP(pr, "Name", label, new Vector2(left ? 0f : 1f, 1f), new Vector2(sign * 14f, -8f), new Vector2(200f, 20f),
                16, color, left ? TextAnchor.UpperLeft : TextAnchor.UpperRight);
            nm.rectTransform.pivot = new Vector2(left ? 0f : 1f, 1f);

            // rounds ganados junto al nombre
            _winPips[i] = new Image[MatchController.RoundsToWin];
            for (int w = 0; w < MatchController.RoundsToWin; w++)
            {
                var wp = MakeImage(pr, $"Win{w}", new Vector2(left ? 1f : 0f, 1f),
                    new Vector2(-sign * (14f + w * 24f), -16f), new Vector2(16f, 16f), Palette.Guard);
                wp.rectTransform.pivot = new Vector2(left ? 1f : 0f, 1f);
                _winPips[i][w] = wp;
            }

            // vida GRANDE
            _pips[i] = new Image[SimConfig.MaxHp];
            _pipAnim[i] = new float[SimConfig.MaxHp];
            for (int p = 0; p < SimConfig.MaxHp; p++)
            {
                var pip = MakeImage(pr, $"Pip{p}", new Vector2(left ? 0f : 1f, 1f),
                    new Vector2(sign * (14f + p * PipGap), -34f), new Vector2(PipW, 34f), color);
                pip.rectTransform.pivot = new Vector2(left ? 0f : 1f, 1f);
                _pips[i][p] = pip;
            }

            // guardia debajo, en ámbar
            var gbg = MakeImage(pr, "GuardBg", new Vector2(left ? 0f : 1f, 1f),
                new Vector2(sign * 14f, -80f), new Vector2(GuardBarW, 9f), new Color(0f, 0f, 0f, 0.55f));
            gbg.rectTransform.pivot = new Vector2(left ? 0f : 1f, 1f);
            _guardFill[i] = MakeImage(pr, "Guard", new Vector2(left ? 0f : 1f, 1f),
                new Vector2(sign * 14f, -80f), new Vector2(GuardBarW, 9f), Palette.Guard);
            _guardFill[i].rectTransform.pivot = new Vector2(left ? 0f : 1f, 1f);

            // barra de SUPER: dorada y finita bajo la guardia; carga con overflow
            _superBg[i] = MakeImage(pr, "SuperBg", new Vector2(left ? 0f : 1f, 1f),
                new Vector2(sign * 14f, -92f), new Vector2(GuardBarW, 6f), new Color(0f, 0f, 0f, 0.55f));
            _superBg[i].rectTransform.pivot = new Vector2(left ? 0f : 1f, 1f);
            _superFill[i] = MakeImage(pr, "Super", new Vector2(left ? 0f : 1f, 1f),
                new Vector2(sign * 14f, -92f), new Vector2(0f, 6f), new Color(1f, 0.75f, 0.2f, 0.9f));
            _superFill[i].rectTransform.pivot = new Vector2(left ? 0f : 1f, 1f);

            _limbLabel[i] = MakeTextP(pr, "Limbs", "", new Vector2(left ? 0f : 1f, 1f), new Vector2(sign * 14f, -102f), new Vector2(400f, 14f),
                8, new Color(1f, 0.45f, 0.35f), left ? TextAnchor.UpperLeft : TextAnchor.UpperRight);
            _limbLabel[i].rectTransform.pivot = new Vector2(left ? 0f : 1f, 1f);

            // circulitos de AP (modo YOMI): GRANDES, abajo del lado de cada
            // peleador — lleno = punto disponible, vacío = no hay
            _apPips[i] = new Image[YomiConfig.ApCap];
            for (int p = 0; p < YomiConfig.ApCap; p++)
            {
                var pip = MakeImage(_canvasRt, $"ApPip{i}_{p}", new Vector2(left ? 0f : 1f, 0f),
                    new Vector2(sign * (52f + p * 42f), 84f), new Vector2(32f, 32f), Color.white);
                pip.sprite = CircleSprite();
                pip.gameObject.SetActive(false);
                _apPips[i][p] = pip;
            }
            var apT = MakeTextP(_canvasRt, "ApLabel" + i, "AP", new Vector2(left ? 0f : 1f, 0f),
                new Vector2(sign * 52f, 118f), new Vector2(80f, 20f), 8,
                new Color(0.45f, 0.9f, 1f, 0.8f), left ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight);
            apT.rectTransform.pivot = new Vector2(left ? 0f : 1f, 0.5f);
            _apLabel[i] = apT;
            _apLabel[i].gameObject.SetActive(false);
        }

        // circulito procedural (el proyecto no usa assets: todo se genera)
        static Sprite CircleSprite()
        {
            if (_circleSprite != null) return _circleSprite;
            const int S = 24;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            float c = (S - 1) * 0.5f, r = S * 0.42f;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    float a = Mathf.Clamp01(r - d + 0.5f); // borde suave de 1px
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            _circleSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
            return _circleSprite;
        }

        public void SetPrompt(string s) => _prompt.text = s;
        public void SetBanner(string s) => _bannerOverride = s;

        public void OnMatchReset()
        {
            _banner.text = "";
            _bannerOverride = "";
            SetGameOverButtons(false);
        }

        // El feedback vive EN el mundo, sobre el peleador correspondiente.
        public void Feedback(int side, string msg, Color c)
        {
            float x = _mc.Sim != null ? _mc.Sim.Fighters[side].X : 0f;
            WorldFX.Popup(x, msg, c);
        }

        public void OnSimEvent(SimEvent ev)
        {
            if (ev.Kind == EvKind.Whiff && (ev.MoveIndex == MoveCatalog.JumpF || ev.MoveIndex == MoveCatalog.JumpN)) return;
            var sim = _mc.Sim;
            int atk = ev.Attacker, def = 1 - ev.Attacker;
            float atkX = sim.Fighters[atk].X, defX = sim.Fighters[def].X;

            if (ev.Kind == EvKind.Tech)
            {
                WorldFX.Popup((atkX + defX) * 0.5f, "¡TECH!", new Color(0.5f, 0.95f, 1f), 1.2f);
                return;
            }
            if (ev.Kind == EvKind.Parry)
            {
                WorldFX.Popup(atkX, "¡PARRY!", new Color(0.35f, 0.9f, 1f), 1.25f);
                if (ev.FrameAdv != 0) WorldFX.Popup(atkX, $"+{ev.FrameAdv}F", new Color(1f, 1f, 1f, 0.8f), 0.8f);
                return;
            }
            if (ev.Kind == EvKind.LimbLost)
            {
                WorldFX.Popup(defX, ev.Limb == Limb.Arm ? "¡BRAZO FUERA!" : "¡PIERNA FUERA!", new Color(1f, 0.35f, 0.3f), 1.25f);
                return;
            }

            string adv = ev.FrameAdv >= 0 ? $"+{ev.FrameAdv}F" : $"-{-ev.FrameAdv}F";
            switch (ev.Kind)
            {
                case EvKind.Hit:
                    WorldFX.Popup(defX, $"-{ev.Damage:0}", ev.Counter ? new Color(1f, 0.55f, 0.15f) : Palette.Damage, ev.Counter ? 1.45f : 1.15f);
                    WorldFX.Popup(atkX, adv, new Color(1f, 1f, 1f, 0.75f), 0.8f);
                    break;
                case EvKind.Blocked:
                    WorldFX.Popup(defX, "BLOQUEADO", Palette.Block, 0.9f);
                    WorldFX.Popup(atkX, adv, new Color(1f, 1f, 1f, 0.75f), 0.8f);
                    break;
                case EvKind.Whiff:
                    WorldFX.Popup(atkX, "AL AIRE", new Color(1f, 1f, 1f, 0.5f), 0.85f);
                    break;
                case EvKind.GuardCrush:
                    WorldFX.Popup(defX, "¡GUARDIA ROTA!", Palette.Guard, 1.4f);
                    break;
            }
        }

        void Update()
        {
            var sim = _mc.Sim;
            if (sim == null) return;
            var flow = _mc.State;

            // transición de fase: overlay frío al planificar, flash al ejecutar
            _planOverlay.gameObject.SetActive(flow == MatchController.Flow.Planning);

            // cartel REPLAY + SKIP mientras corre la repetición
            bool replaying = flow == MatchController.Flow.Replay;
            if (_replayPanel.gameObject.activeSelf != replaying) _replayPanel.gameObject.SetActive(replaying);
            if (!replaying && _replayStalled) SetReplayStalled(false);
            if (replaying)
            {
                var rc = _replayTitle.color;
                rc.a = 0.65f + Mathf.PingPong(Time.time * 1.6f, 0.35f); // parpadeo estilo VHS
                _replayTitle.color = rc;
                var rmp = GameInput.MousePos();
                bool rclick = GameInput.ClickPressed();
                if (rclick && Inside(_skipBtn, rmp))
                {
                    SfxLib.Play(SfxLib.Kind.UiClick, 0.8f);
                    _mc.SkipReplay();
                    return;
                }
                // LAG / NORMAL / RÁPIDO: quedan en pantalla y conmutan en vivo
                for (int m = 0; m < 3; m++)
                {
                    bool on = (int)_mc.ReplayMode == m;
                    var c = on ? new Color(0.2f, 0.4f, 0.6f, 0.95f) : new Color(0.1f, 0.12f, 0.16f, 0.95f);
                    bool over = Inside(_replayModeBtns[m], rmp);
                    if (over) c = Color.Lerp(c, Color.white, 0.22f);
                    _replayModeBtns[m].color = c;
                    _replayModeLabels[m].color = on ? Color.white : new Color(1f, 1f, 1f, 0.6f);
                    if (rclick && over)
                    {
                        SfxLib.Play(SfxLib.Kind.UiClick, 0.8f);
                        _mc.SetReplayMode((ReplayViewMode)m);
                    }
                }
            }
            if (flow != _prevFlow)
            {
                // en YOMI la transición la cuentan las cartas de revelación
                if (flow == MatchController.Flow.Executing && _prevFlow == MatchController.Flow.Planning && !SimConfig.YomiEnabled)
                    ShowBigMessage("¡EJECUTANDO!", new Color(0.5f, 0.95f, 1f), 0.8f);
                _prevFlow = flow;
            }

            AnimateYomiCards();

            bool executing = flow == MatchController.Flow.Executing || flow == MatchController.Flow.Replay;

            // ---- subida de lag: glitch + escala animada + highlight ----
            int lagLvl = _mc.LagLevel;
            if (lagLvl != _prevLagLevel)
            {
                if (lagLvl > _prevLagLevel && _mc.LagMode)
                {
                    _lagFxTimer = 0.7f;
                    SfxLib.Play(SfxLib.Kind.Glitch, 0.9f);
                }
                _prevLagLevel = lagLvl;
            }

            // la timeline se estira animada hacia la escala nueva (snap al achicarse)
            float targetFrames = _mc.CurrentTurnFrames;
            if (targetFrames < _shownTurnFrames) _shownTurnFrames = targetFrames;
            else _shownTurnFrames = Mathf.Lerp(_shownTurnFrames, targetFrames, 1f - Mathf.Exp(-4.5f * Time.deltaTime));
            if (Mathf.Abs(_shownTurnFrames - targetFrames) < 0.5f) _shownTurnFrames = targetFrames;

            // franjas de glitch (la conexión rompiéndose)
            if (_lagFxTimer > 0f)
            {
                _lagFxTimer -= Time.deltaTime;
                foreach (var gb in _glitchBars)
                {
                    bool on = _lagFxTimer > 0f && Random.value < 0.6f;
                    gb.gameObject.SetActive(on);
                    if (!on) continue;
                    gb.rectTransform.anchoredPosition = new Vector2(Random.Range(-50f, 50f), Random.Range(-520f, 520f));
                    gb.rectTransform.sizeDelta = new Vector2(2200f, Random.Range(3f, 14f));
                    gb.color = new Color(0.6f + Random.value * 0.4f, 0.9f, 1f, 0.10f + Random.value * 0.22f);
                }
                if (_lagFxTimer <= 0f)
                    foreach (var gb in _glitchBars) gb.gameObject.SetActive(false);
            }

            // el espacio NUEVO de la barra se destaca durante toda la
            // planificación del primer turno de cada nivel de lag
            // (la fórmula vive en el controller: +50% cada 3 turnos)
            int prevTurnLvl = _mc.LagLevelForTurn(_mc.TurnNumber - 1);
            bool newLagTurn = _mc.LagMode && flow == MatchController.Flow.Planning && lagLvl > prevTurnLvl;
            int oldFrames = _mc.FramesForLevel(prevTurnLvl);
            float lagFromX = oldFrames * PxPerFrame;
            string lagLabel = $"+{_mc.CurrentTurnFrames - oldFrames}F NUEVOS";
            _row0.SetLagHighlight(newLagTurn, lagFromX, lagLabel);
            _row1.SetLagHighlight(newLagTurn, lagFromX, "");

            for (int i = 0; i < 2; i++)
            {
                // pips: al perder vida el pip "se rompe" (flash blanco + pop + fade),
                // no se apaga en seco. Si la vida sube (dummy/round nuevo), snap.
                float hpNow = sim.Fighters[i].Hp;
                int hpInt = Mathf.CeilToInt(hpNow);
                if (_shownHp[i] < 0 || hpInt > _shownHp[i])
                {
                    _shownHp[i] = hpInt;
                    for (int p = 0; p < SimConfig.MaxHp; p++) _pipAnim[i][p] = 0f;
                }
                else if (hpInt < _shownHp[i])
                {
                    for (int p = Mathf.Max(0, hpInt); p < _shownHp[i] && p < SimConfig.MaxHp; p++)
                        _pipAnim[i][p] = 0.5f;
                    _shownHp[i] = hpInt;
                }
                for (int p = 0; p < SimConfig.MaxHp; p++)
                {
                    var c = Palette.Side(i);
                    float a = p < hpNow ? 1f : 0.13f;
                    float t = _pipAnim[i][p];
                    if (t > 0f)
                    {
                        _pipAnim[i][p] = t - Time.deltaTime;
                        float k = Mathf.Clamp01(t / 0.5f);
                        c = Color.Lerp(c, Color.white, k);
                        a = Mathf.Lerp(0.13f, 1f, k);
                        _pips[i][p].rectTransform.localScale = Vector3.one * (1f + 0.45f * k);
                    }
                    else _pips[i][p].rectTransform.localScale = Vector3.one;
                    c.a = a;
                    _pips[i][p].color = c;
                }

                var lf = sim.Fighters[i];
                // en YOMI los AP viven en los circulitos grandes de abajo
                _limbLabel[i].text = SimConfig.YomiEnabled ? ""
                    : lf.ArmHp <= 0f && lf.LegHp <= 0f ? "SIN BRAZO · SIN PIERNA"
                    : lf.ArmHp <= 0f ? "SIN BRAZO" : lf.LegHp <= 0f ? "SIN PIERNA" : "";

                float g = sim.Fighters[i].Guard / SimConfig.GuardMax;
                _guardFill[i].rectTransform.sizeDelta = new Vector2(GuardBarW * g, 9f);
                _guardFill[i].color = g <= 0.25f
                    ? Color.Lerp(Palette.Guard, new Color(1f, 0.2f, 0.15f), Mathf.PingPong(Time.time * 4f, 1f))
                    : Palette.Guard;

                // super: solo tiene sentido en turno fluido (carga con overflow).
                // En YOMI en su lugar van los circulitos de AP.
                bool yomiOn = SimConfig.YomiEnabled;
                bool superOn = SimConfig.CarryoverEnabled && !yomiOn;
                if (_superBg[i].gameObject.activeSelf != superOn)
                {
                    _superBg[i].gameObject.SetActive(superOn);
                    _superFill[i].gameObject.SetActive(superOn);
                }
                if (superOn)
                {
                    float sp = Mathf.Clamp01(sim.Fighters[i].Super / (float)SimConfig.SuperMax);
                    _superFill[i].rectTransform.sizeDelta = new Vector2(GuardBarW * sp, 6f);
                    _superFill[i].color = sp >= 1f
                        ? Color.Lerp(new Color(1f, 0.8f, 0.2f), Color.white, Mathf.PingPong(Time.time * 3f, 0.65f))
                        : new Color(1f, 0.75f, 0.2f, 0.9f);
                }

                // circulitos de AP: lleno = disponible, vacío = no hay.
                // Muestran el AP del ARRANQUE del turno durante la acción
                // (YomiDisplayAp): el cobro se ve recién al cerrar el turno.
                bool pipsOn = yomiOn && _mc.Yomi != null;
                int apNow = pipsOn ? _mc.YomiDisplayAp(i) : 0;
                if (_apLabel[i].gameObject.activeSelf != pipsOn) _apLabel[i].gameObject.SetActive(pipsOn);
                for (int p = 0; p < _apPips[i].Length; p++)
                {
                    if (_apPips[i][p].gameObject.activeSelf != pipsOn)
                        _apPips[i][p].gameObject.SetActive(pipsOn);
                    if (pipsOn)
                        _apPips[i][p].color = p < apNow
                            ? new Color(0.35f, 0.85f, 1f, 1f)
                            : new Color(0.22f, 0.28f, 0.36f, 0.5f);
                }

                for (int w = 0; w < MatchController.RoundsToWin; w++)
                {
                    var c = _winPips[i][w].color;
                    c.a = w < _mc.GetWins(i) ? 1f : 0.14f;
                    _winPips[i][w].color = c;
                }

                // badge de estado sobre la cabeza (world-space): el lugar
                // único de status effects — stun, guard crush y overflow
                string badge = "";
                Color bc = Color.white;
                if (sim.IsStunned(i))
                {
                    int rem = sim.StunRemaining(i);
                    if (sim.Fighters[i].Crushed) { badge = $"GUARD CRUSH {rem}F"; bc = new Color(1f, 0.35f, 0.75f); }
                    else switch (sim.Fighters[i].Stun)
                    {
                        case StunKind.Knockdown: badge = $"KD {rem}F"; bc = new Color(1f, 0.5f, 0.2f); break;
                        case StunKind.Blockstun: badge = $"BLOCK {rem}F"; bc = Palette.Block; break;
                        default: badge = $"HIT {rem}F"; bc = Palette.Damage; break;
                    }
                }
                else if (_mc.OverflowFrames(i) is int ovf && ovf > 0)
                {
                    // turno fluido: este move cruza el turno (o ya cruzó
                    // y arrancás comprometido)
                    badge = $"OVERFLOW »{ovf}F";
                    bc = new Color(1f, 0.6f, 0.15f);
                }
                else if (sim.IsBlockingState(i) && executing)
                {
                    badge = "GUARD";
                    bc = new Color(Palette.Block.r, Palette.Block.g, Palette.Block.b, 0.7f);
                }
                WorldFX.SetBadge(i, sim.Fighters[i].X, 2.35f, badge, bc);
            }

            float playX = executing ? Mathf.Clamp((_mc.TickFloat - _mc.TurnStartTick) * PxPerFrame, 0f, RowW) : -1f;
            _row0.UpdateRow(_mc.GetPlan(0), _mc.RowRevealed(0), playX, _mc.TimelineOffset(0), _mc.TurnStartStunKind[0]);
            _row1.UpdateRow(_mc.GetPlan(1), _mc.RowRevealed(1), playX, _mc.TimelineOffset(1), _mc.TurnStartStunKind[1]);

            UpdateTimelineInteraction(flow);
            UpdateConnStrip(sim);
            UpdateWallGlow(sim);
            UpdateBigMessage();
            UpdateOptions();
            UpdateBanner(sim, flow);
            UpdateHover();
        }

        // esquina: la pared pulsa con el color del jugador acorralado
        void UpdateWallGlow(MatchSim sim)
        {
            for (int w = 0; w < 2; w++)
            {
                var r = ArenaRefs.Walls[w];
                if (r == null) continue;
                float best = 0f;
                int who = -1;
                for (int i = 0; i < 2; i++)
                {
                    float x = sim.Fighters[i].X;
                    if ((w == 0 && x > 0f) || (w == 1 && x < 0f)) continue;
                    float t = Mathf.Clamp01((Mathf.Abs(x) - (SimConfig.StageHalfWidth - 1.1f)) / 1.1f);
                    if (t > best) { best = t; who = i; }
                }
                var target = ArenaRefs.WallBase;
                if (who >= 0 && best > 0.01f)
                {
                    float pulse = 0.45f + 0.3f * Mathf.PingPong(Time.time * 2.4f, 1f);
                    target = Color.Lerp(ArenaRefs.WallBase, Palette.Side(who), best * pulse);
                }
                r.material.color = target;
            }
        }

        // scrub (arrastrar) y borrar orden (click derecho) sobre la fila del picker
        void UpdateTimelineInteraction(MatchController.Flow flow)
        {
            bool scrubbed = false;
            if (flow == MatchController.Flow.Planning)
            {
                var row = _mc.Picker == 0 ? _row0 : _row1;
                var mp = GameInput.MousePos();
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(row.AreaRt, mp, null, out var local)
                    && row.AreaRt.rect.Contains(local))
                {
                    float xFromLeft = local.x + RowW * 0.5f;
                    float frame = xFromLeft / PxPerFrame;

                    if (GameInput.ClickHeld())
                    {
                        _mc.GhostScrub(frame);
                        scrubbed = true;
                    }
                    if (GameInput.RightClickPressed())
                    {
                        int idx = ChipIndexAt(frame);
                        if (idx >= 0) _mc.PlanRemoveAt(_mc.Picker, idx);
                    }
                }
            }
            if (!scrubbed) _mc.GhostScrub(-1f);
        }

        int ChipIndexAt(float frame)
        {
            var plan = _mc.GetPlan(_mc.Picker);
            float start = _mc.TimelineOffset(_mc.Picker);
            for (int i = 0; i < plan.Count; i++)
            {
                float end = start + MoveCatalog.All[plan[i]].Total;
                if (frame >= start && frame < end) return i;
                start = end;
            }
            return -1;
        }

        void UpdateConnStrip(MatchSim sim)
        {
            // tirón del replay: ping spike en rojo y wifi en pánico
            if (_replayStalled && ReplayLagFX.PingSpike)
            {
                if (!_connSpiked)
                {
                    _connSpiked = true;
                    _connText.text = $"PING {_fakePing}MS · PACKET LOSS";
                    _connText.color = new Color(1f, 0.35f, 0.3f);
                }
                for (int b = 0; b < 4; b++)
                    _wifiBars[b].color = new Color(0.95f, 0.25f, 0.2f, Random.value < 0.5f ? 0.7f : 0.12f);
                return;
            }
            if (_connSpiked)
            {
                _connSpiked = false;
                _connText.color = new Color(1f, 1f, 1f, 0.8f);
                _lastConnDist = -1f; // forzar rearmado del texto normal
            }

            float dist = Mathf.Abs(sim.Fighters[1].X - sim.Fighters[0].X);
            int lvl = _mc.LagLevel;
            // armar el string solo cuando cambia algo (GC por frame en WebGL)
            float distShown = Mathf.Round(dist * 100f) / 100f;
            int pingShown = _mc.LagMode ? _mc.CurrentTurnFrames * 16 : 0;
            if (!Mathf.Approximately(distShown, _lastConnDist) || pingShown != _lastConnPing)
            {
                _lastConnDist = distShown;
                _lastConnPing = pingShown;
                string ping = _mc.LagMode ? $"PING {pingShown}MS" : "PING 0MS · LAN";
                _connText.text = $"DIST {distShown:0.00}   ·   {ping}";
            }

            int alive = _mc.LagMode ? Mathf.Max(4 - lvl, 1) : 4;
            var barColor = !_mc.LagMode || lvl == 0 ? Palette.Ok :
                           lvl == 1 ? new Color(0.85f, 0.85f, 0.3f) :
                           lvl == 2 ? new Color(0.95f, 0.6f, 0.2f) : new Color(0.95f, 0.25f, 0.2f);
            for (int b = 0; b < 4; b++)
            {
                bool on = b < alive;
                var c = on ? barColor : new Color(1f, 1f, 1f, 0.12f);
                if (on && _mc.LagMode && lvl >= 3) c.a = 0.45f + Mathf.PingPong(Time.time * 2.2f, 0.55f);
                _wifiBars[b].color = c;
            }
        }

        void UpdateBigMessage()
        {
            for (int s = 0; s < 2; s++)
            {
                if (_bigMsgTimer[s] <= 0f) continue;
                _bigMsgTimer[s] -= Time.deltaTime;
                var lc = _bigMsg[s].color;
                lc.a = Mathf.Clamp01(_bigMsgTimer[s] / 0.5f);
                _bigMsg[s].color = lc;
                if (_bigMsgTimer[s] <= 0f) _bigMsg[s].text = "";
            }
        }

        void UpdateOptions()
        {
            bool click = GameInput.ClickPressed();
            var mp = GameInput.MousePos();

            if (click && Inside(_optBtn, mp))
            {
                SfxLib.Play(SfxLib.Kind.UiClick, 0.7f);
                _optOpen = !_optOpen;
                _optPanel.gameObject.SetActive(_optOpen);
                _optBtnLabel.color = _optOpen ? Palette.Ok : new Color(1f, 1f, 1f, 0.75f);
            }

            bool toggleBoxes = GameInput.BoxesPressed() || (click && _optOpen && Inside(_boxBtn, mp));
            if (toggleBoxes)
            {
                SfxLib.Play(SfxLib.Kind.UiClick, 0.7f);
                VizPrefs.ShowBoxes = !VizPrefs.ShowBoxes;
                _boxBtnLabel.text = VizPrefs.ShowBoxes ? "CAJAS: ON" : "CAJAS: OFF";
                _boxBtnLabel.color = VizPrefs.ShowBoxes ? Palette.Ok : new Color(1f, 1f, 1f, 0.5f);
            }

            if (click && _optOpen && Inside(_voiceBtn, mp))
            {
                SfxLib.Play(SfxLib.Kind.UiClick, 0.7f);
                Announcer.Enabled = !Announcer.Enabled;
                _voiceBtnLabel.text = Announcer.Enabled ? "VOZ: ON" : "VOZ: OFF";
                _voiceBtnLabel.color = Announcer.Enabled ? Palette.Ok : new Color(1f, 1f, 1f, 0.5f);
            }

            if (click && _optOpen && Inside(_sfxBtn, mp))
            {
                SfxLib.Enabled = !SfxLib.Enabled;
                _sfxBtnLabel.text = SfxLib.Enabled ? "SFX: ON" : "SFX: OFF";
                _sfxBtnLabel.color = SfxLib.Enabled ? Palette.Ok : new Color(1f, 1f, 1f, 0.5f);
                SfxLib.Play(SfxLib.Kind.UiClick, 0.7f); // suena solo si quedó ON
            }

            bool logToggle = GameInput.LogPressed() || (click && _optOpen && Inside(_logBtn, mp));
            if (logToggle)
            {
                SfxLib.Play(SfxLib.Kind.UiClick, 0.7f);
                _logOpen = !_logOpen;
                _logPanel.gameObject.SetActive(_logOpen);
                _logBtnLabel.color = _logOpen ? Palette.Ok : new Color(1f, 1f, 1f, 0.75f);
            }

            for (int s = 0; s < Speeds.Length; s++)
            {
                bool on = Mathf.Approximately(_mc.PlaybackSpeed, Speeds[s]);
                var c = on ? new Color(0.2f, 0.4f, 0.6f, 0.95f) : new Color(0.1f, 0.12f, 0.16f, 0.95f);
                if (_optOpen && Inside(_speedBtns[s], mp)) c = Color.Lerp(c, Color.white, 0.22f); // hover
                _speedBtns[s].color = c;
                _speedLabels[s].color = on ? Color.white : new Color(1f, 1f, 1f, 0.55f);
                if (click && _optOpen && Inside(_speedBtns[s], mp))
                {
                    SfxLib.Play(SfxLib.Kind.UiClick, 0.7f);
                    _mc.SetPlaybackSpeed(Speeds[s]);
                }
            }
        }

        void UpdateBanner(MatchSim sim, MatchController.Flow flow)
        {
            bool over = flow == MatchController.Flow.GameOver && _bannerOverride == "";
            SetGameOverButtons(over);
            if (over)
            {
                int w = _mc.EffectiveWinner(); // KO o decisión por vida (TIME OVER)
                _banner.text = (w == 0 ? "¡GANASTE LA PELEA!" : w == 1 ? "PERDISTE LA PELEA" : "EMPATE")
                               + $"\n<size=30>{_mc.GetWins(0)} — {_mc.GetWins(1)}</size>";
                var mp = GameInput.MousePos();
                if (GameInput.ClickPressed())
                {
                    if (Inside(_btnRematch, mp)) { SfxLib.Play(SfxLib.Kind.UiClick, 0.8f); _mc.RequestRematch(); }
                    else if (_btnReplay.gameObject.activeSelf && Inside(_btnReplay, mp)) { SfxLib.Play(SfxLib.Kind.UiClick, 0.8f); _mc.RequestReplay(); }
                    else if (Inside(_btnMenu, mp)) { SfxLib.Play(SfxLib.Kind.UiClick, 0.8f); _mc.GoToModeSelect(); }
                }
            }
            else
            {
                _banner.text = _bannerOverride;
            }
        }

        static bool Inside(Image img, Vector2 screenPos)
            => RectTransformUtility.RectangleContainsScreenPoint(img.rectTransform, screenPos, null);

        public static Color ChipColor(int moveIndex)
        {
            switch (moveIndex)
            {
                case MoveCatalog.AttackA: return Palette.Damage;
                case MoveCatalog.AttackB: return new Color(0.65f, 0.3f, 0.85f);
                case MoveCatalog.Hadouken: return new Color(0.25f, 0.55f, 0.95f);
                case MoveCatalog.Shoryuken: return new Color(0.95f, 0.7f, 0.15f);
                case MoveCatalog.JumpF:
                case MoveCatalog.JumpN:
                case MoveCatalog.JumpB: return new Color(0.55f, 0.8f, 0.35f);
                case MoveCatalog.DashF:
                case MoveCatalog.DashB: return new Color(0.2f, 0.72f, 0.72f);
                case MoveCatalog.Tatsu: return new Color(0.9f, 0.45f, 0.15f);
                case MoveCatalog.Grab: return Palette.GrabC;
                case MoveCatalog.Parry: return new Color(0.25f, 0.75f, 0.95f);
                case MoveCatalog.WalkB: // bloquear: azul defensivo, como agacharse
                case MoveCatalog.Crouch: return new Color(0.35f, 0.55f, 0.85f);
                case MoveCatalog.LowKick: return new Color(0.75f, 0.28f, 0.3f);
                case MoveCatalog.Super: return new Color(1f, 0.78f, 0.2f); // dorada
                case MoveCatalog.Strong: return new Color(0.95f, 0.55f, 0.2f); // golpe fuerte (yomi)
                case MoveCatalog.YomiGrab: return Palette.GrabC;
                default: return new Color(0.25f, 0.72f, 0.45f); // caminar
            }
        }

        public static string ChipLabel(int moveIndex)
        {
            switch (moveIndex)
            {
                case MoveCatalog.AttackA: return "JAB";
                case MoveCatalog.AttackB: return "BAR";
                case MoveCatalog.Hadouken: return "HD";
                case MoveCatalog.Shoryuken: return "DP";
                case MoveCatalog.JumpF: return "J→";
                case MoveCatalog.JumpN: return "J";
                case MoveCatalog.JumpB: return "J←";
                case MoveCatalog.WalkF: return "→"; // retirado del menú; aparece en replays viejos
                case MoveCatalog.WalkB: return "BL";
                case MoveCatalog.DashF: return "»";
                case MoveCatalog.DashB: return "«";
                case MoveCatalog.Tatsu: return "T";
                case MoveCatalog.Grab: return "G";
                case MoveCatalog.Parry: return "P";
                case MoveCatalog.Crouch: return "▼";
                case MoveCatalog.LowKick: return "b";
                case MoveCatalog.Super: return "SPR";
                case MoveCatalog.Strong: return "GF";
                case MoveCatalog.YomiGrab: return "G";
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
            Image _ovfSeg;   // pestaña "»Nf" del move que cruza el turno
            Text _ovfLabel;
            Image _lagSeg;
            Text _lagLabel;
            int _lastStunShown = -1; // para no armar el string del stun por frame
            readonly Text _hidden;
            readonly float _height;
            readonly bool _dim;
            readonly List<Image> _chips = new List<Image>();
            readonly List<Text> _labels = new List<Text>();
            // sub-franjas S/A/R al pie de cada ficha: se lee en qué frame pega
            readonly List<Image> _phS = new List<Image>();
            readonly List<Image> _phA = new List<Image>();
            readonly List<Image> _phR = new List<Image>();

            public RectTransform AreaRt => _area;

            public TimelineRow(HudUI hud, string name, float y, float height, bool dim, int side)
            {
                _hud = hud;
                _height = height;
                _dim = dim;

                var bg = hud.MakeImage(hud._canvasRt, name, new Vector2(0.5f, 0f), new Vector2(0f, y), new Vector2(RowW, height), new Color(0f, 0f, 0f, dim ? 0.45f : 0.6f));
                var outline = bg.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(Palette.Side(side).r, Palette.Side(side).g, Palette.Side(side).b, 0.5f);
                outline.effectDistance = new Vector2(1.5f, -1.5f);
                _area = bg.rectTransform;
                _area.gameObject.AddComponent<RectMask2D>();

                var chipGo = new GameObject("Chips", typeof(RectTransform));
                _chipParent = chipGo.GetComponent<RectTransform>();
                _chipParent.SetParent(_area, false);
                _chipParent.anchorMin = Vector2.zero;
                _chipParent.anchorMax = Vector2.one;
                _chipParent.offsetMin = _chipParent.offsetMax = Vector2.zero;

                _hidden = hud.MakeTextP(_area, "Hidden", "? ? ?", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 24f),
                    16, new Color(1f, 1f, 1f, 0.3f), TextAnchor.MiddleCenter);

                // highlight del espacio nuevo cuando sube el lag (pulsa en ámbar)
                _lagSeg = hud.MakeImage(_area, "LagSeg", new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, height), Palette.Guard);
                _lagSeg.rectTransform.pivot = new Vector2(0f, 0.5f);
                _lagLabel = hud.MakeTextP(_lagSeg.rectTransform, "L", "", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400f, 20f),
                    8, Palette.Guard, TextAnchor.MiddleCenter);
                _lagSeg.gameObject.SetActive(false);

                _stunSeg = hud.MakeImage(_area, "StunSeg", new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, height - 4f), Color.white);
                _stunSeg.rectTransform.pivot = new Vector2(0f, 0.5f);
                _stunLabel = hud.MakeText(_stunSeg.rectTransform, "L", "", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 22f),
                    14, Color.white, TextAnchor.MiddleCenter);
                _stunLabel.fontStyle = FontStyle.Bold;

                // pestaña OVERFLOW (turno fluido): el move que cruza el límite
                // se corta en el borde y esta flecha dice cuántos frames siguen
                _ovfSeg = hud.MakeImage(_area, "Ovf", new Vector2(0f, 0.5f), Vector2.zero, new Vector2(44f, height - 4f), new Color(1f, 0.6f, 0.15f, 0.85f));
                _ovfSeg.rectTransform.pivot = new Vector2(0f, 0.5f);
                _ovfLabel = hud.MakeText(_ovfSeg.rectTransform, "L", "", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80f, 22f),
                    13, Color.white, TextAnchor.MiddleCenter);
                _ovfLabel.fontStyle = FontStyle.Bold;
                _ovfSeg.gameObject.SetActive(false);

                _playhead = hud.MakeImage(_area, "Playhead", new Vector2(0f, 0.5f), Vector2.zero, new Vector2(4f, height), Color.white);
                _playhead.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            }

            // destaca la zona nueva de la barra: "de acá para allá es TODO nuevo"
            public void SetLagHighlight(bool on, float fromX, string label)
            {
                _lagSeg.gameObject.SetActive(on);
                if (!on) return;
                float w = RowW - fromX;
                _lagSeg.rectTransform.anchoredPosition = new Vector2(fromX, 0f);
                _lagSeg.rectTransform.sizeDelta = new Vector2(Mathf.Max(0f, w), _height);
                float pulse = 0.10f + Mathf.PingPong(Time.time * 0.35f, 0.13f);
                _lagSeg.color = new Color(Palette.Guard.r, Palette.Guard.g, Palette.Guard.b, pulse);
                _lagLabel.text = w > 160f ? label : "";
                var lc = Palette.Guard;
                lc.a = 0.75f + Mathf.PingPong(Time.time * 0.8f, 0.25f);
                _lagLabel.color = lc;
            }

            public void UpdateRow(List<int> queue, bool revealed, float playX, int stunFrames, StunKind stunKind)
            {
                _hidden.gameObject.SetActive(!revealed);
                _playhead.gameObject.SetActive(playX >= 0f);
                if (playX >= 0f) _playhead.rectTransform.anchoredPosition = new Vector2(playX, 0f);

                float px = _hud.PxPerFrame;
                float offset = 0f;
                if (stunFrames > 0)
                {
                    offset = stunFrames * px;
                    _stunSeg.gameObject.SetActive(true);
                    _stunSeg.rectTransform.sizeDelta = new Vector2(offset - 2f, _height - 4f);
                    // StunKind.None con offset = move comprometido (turno fluido): verde-agua
                    _stunSeg.color = stunKind == StunKind.Blockstun ? new Color(0.3f, 0.5f, 0.85f, 0.75f)
                                   : stunKind == StunKind.Knockdown ? new Color(0.9f, 0.45f, 0.15f, 0.8f)
                                   : stunKind == StunKind.None ? new Color(0.2f, 0.72f, 0.72f, 0.7f)
                                   : new Color(0.85f, 0.25f, 0.22f, 0.8f);
                    int shown = offset > 46f ? stunFrames : 0;
                    if (shown != _lastStunShown)
                    {
                        _lastStunShown = shown;
                        _stunLabel.text = shown > 0 ? (stunKind == StunKind.None ? $"{shown}f" : $"−{shown}f") : "";
                    }
                }
                else
                {
                    _stunSeg.gameObject.SetActive(false);
                }

                int used = 0;
                int overflowF = 0; // frames del último move que cruzan al próximo turno
                if (revealed && queue != null)
                {
                    float x = offset;
                    foreach (var mi in queue)
                    {
                        var m = MoveCatalog.All[mi];
                        float w = m.Total * px - 2f;
                        // turno fluido: el chip se CORTA en el borde del turno
                        // y cambia de identidad (naranja pulsante + "»") para
                        // que se lea que SIGUE en el próximo turno
                        bool ovfChip = x + w > RowW;
                        if (ovfChip)
                        {
                            overflowF = Mathf.RoundToInt((x + m.Total * px - RowW) / px);
                            w = Mathf.Max(10f, RowW - x);
                        }
                        var chip = GetChip(used++);
                        chip.rectTransform.anchoredPosition = new Vector2(x, 0f);
                        chip.rectTransform.sizeDelta = new Vector2(w, _height - 8f);
                        var c = ChipColor(mi);
                        if (ovfChip)
                            c = Color.Lerp(c, new Color(1f, 0.55f, 0.1f), 0.4f + 0.25f * Mathf.PingPong(Time.time * 1.4f, 1f));
                        if (_dim) c = new Color(c.r, c.g, c.b, 0.8f);
                        chip.color = c;
                        _labels[used - 1].text = ovfChip ? ChipLabel(mi) + "»" : ChipLabel(mi);

                        // fases dentro de la ficha (solo moves con ventana activa):
                        // el mismo amarillo/rojo/azul del panel de info
                        int ci = used - 1;
                        bool phased = m.Active > 0;
                        _phS[ci].gameObject.SetActive(phased);
                        _phA[ci].gameObject.SetActive(phased);
                        _phR[ci].gameObject.SetActive(phased);
                        if (phased)
                        {
                            float wFull = m.Total * px - 2f;
                            float wS = wFull * m.Startup / m.Total;
                            float wA = wFull * m.Active / m.Total;
                            float wR = wFull - wS - wA;
                            // recortadas al ancho visible del chip (overflow)
                            wS = Mathf.Min(wS, w);
                            wA = Mathf.Min(wA, Mathf.Max(0f, w - wS));
                            wR = Mathf.Max(0f, Mathf.Min(wR, w - wS - wA));
                            _phS[ci].rectTransform.anchoredPosition = new Vector2(0f, 2f);
                            _phS[ci].rectTransform.sizeDelta = new Vector2(wS, 5f);
                            _phA[ci].rectTransform.anchoredPosition = new Vector2(wS, 2f);
                            _phA[ci].rectTransform.sizeDelta = new Vector2(wA, 5f);
                            _phR[ci].rectTransform.anchoredPosition = new Vector2(wS + wA, 2f);
                            _phR[ci].rectTransform.sizeDelta = new Vector2(wR, 5f);
                        }
                        x += m.Total * px;
                    }
                }
                for (int i = used; i < _chips.Count; i++)
                    _chips[i].gameObject.SetActive(false);

                if (overflowF > 0)
                {
                    // creada después de _chipParent: siempre dibuja sobre los chips.
                    // ADENTRO del borde derecho: la fila tiene RectMask2D y
                    // fuera de la barra quedaba recortada (invisible).
                    _ovfSeg.gameObject.SetActive(true);
                    _ovfSeg.rectTransform.anchoredPosition = new Vector2(RowW - 47f, 0f);
                    float pulse = 0.7f + Mathf.PingPong(Time.time * 1.1f, 0.3f);
                    _ovfSeg.color = new Color(1f, 0.5f, 0.05f, pulse);
                    _ovfLabel.text = $"»{overflowF}f";
                }
                else _ovfSeg.gameObject.SetActive(false);
            }

            Image GetChip(int i)
            {
                while (i >= _chips.Count)
                {
                    var img = _hud.MakeImage(_chipParent, "Chip", new Vector2(0f, 0.5f), Vector2.zero, Vector2.one, Color.white);
                    img.rectTransform.pivot = new Vector2(0f, 0.5f);
                    var t = _hud.MakeText(img.rectTransform, "L", "", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64f, 30f), 21, Color.white, TextAnchor.MiddleCenter);
                    t.fontStyle = FontStyle.Bold;
                    _chips.Add(img);
                    _labels.Add(t);
                    _phS.Add(PhaseBar(img, new Color(0.95f, 0.85f, 0.25f, 0.9f)));
                    _phA.Add(PhaseBar(img, new Color(0.95f, 0.3f, 0.22f, 0.95f)));
                    _phR.Add(PhaseBar(img, new Color(0.3f, 0.55f, 0.95f, 0.9f)));
                }
                _chips[i].gameObject.SetActive(true);
                return _chips[i];
            }

            Image PhaseBar(Image chip, Color c)
            {
                var bar = _hud.MakeImage(chip.rectTransform, "Ph", new Vector2(0f, 0f), Vector2.zero, Vector2.zero, c);
                bar.rectTransform.pivot = new Vector2(0f, 0f);
                return bar;
            }
        }

        // ---------- helpers ----------

        // panel con fondo oscuro y borde de 1.5px del color de acento
        public Image MakePanel(RectTransform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color accent)
        {
            var img = MakeImage(parent, name, anchor, pos, size, Palette.PanelBg);
            var outline = img.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(accent.r, accent.g, accent.b, 0.55f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            return img;
        }

        // ---- cartas de revelación del modo YOMI ----

        public static Color YomiActionColor(YomiAction a)
        {
            switch (a)
            {
                case YomiAction.Jab: return new Color(0.9f, 0.32f, 0.24f);
                case YomiAction.Kick: return new Color(0.95f, 0.55f, 0.2f);
                case YomiAction.Grab: return new Color(0.85f, 0.3f, 0.75f);
                case YomiAction.Parry: return new Color(0.25f, 0.75f, 0.95f);
                case YomiAction.Shoryu: return new Color(0.95f, 0.7f, 0.15f);
                case YomiAction.Dash: return new Color(0.2f, 0.72f, 0.72f);
                case YomiAction.Jump: return new Color(0.55f, 0.8f, 0.35f);
                case YomiAction.Charge: return new Color(1f, 0.8f, 0.3f);
                default: return new Color(0.6f, 0.6f, 0.65f); // recovery
            }
        }

        static string YomiCardInfo(YomiAction a, bool close)
        {
            if (a == YomiAction.Recovery) return "WHIFFEÓ EL SHORYU: PIERDE EL TURNO";
            if (a == YomiAction.Charge) return "+2 AP SI NADIE LE PEGA";
            int cost = YomiConfig.Cost(a);
            int dmg = YomiConfig.Damage(a);
            string s = cost == 0 ? "GRATIS" : $"{cost} AP";
            if (dmg > 0) s += $"  ·  {dmg} DMG";
            if (a == YomiAction.Dash) s += close ? "  ·  SE VA" : "  ·  ENTRA";
            if (a == YomiAction.Jump) s += close ? "  ·  ESCAPA" : "  ·  ENTRA PEGANDO";
            return s;
        }

        public void ShowYomiReveal(YomiAction a0, YomiAction a1, bool close, string ruling)
        {
            var acts = new[] { a0, a1 };
            for (int i = 0; i < 2; i++)
            {
                var c = YomiActionColor(acts[i]);
                _yomiCard[i].gameObject.SetActive(true);
                _yomiCardEdge[i].color = new Color(c.r, c.g, c.b, 0.95f);
                _yomiCardName[i].text = YomiConfig.Name(acts[i]).ToUpperInvariant();
                _yomiCardName[i].color = new Color(c.r * 0.45f + 0.55f, c.g * 0.45f + 0.55f, c.b * 0.45f + 0.55f);
                _yomiCardInfo[i].text = YomiCardInfo(acts[i], close);
            }
            _yomiVs.gameObject.SetActive(true);
            // el FALLO: la regla de la matriz que decidió este turno, cantada
            // antes de que la acción se actúe
            _yomiExplain.text = ruling;
            _yomiExplain.gameObject.SetActive(true);
            _yomiPop = 0f;
            _yomiDock = 0f;
            _yomiDocked = false;
            LayoutYomiCards();
        }

        public void DockYomiCards()
        {
            _yomiDocked = true;
            _yomiVs.gameObject.SetActive(false);
        }

        public void HideYomiCards()
        {
            _yomiCard[0].gameObject.SetActive(false);
            _yomiCard[1].gameObject.SetActive(false);
            _yomiVs.gameObject.SetActive(false);
            if (_yomiExplain != null) _yomiExplain.gameObject.SetActive(false);
        }

        void AnimateYomiCards()
        {
            if (_yomiCard[0] == null || !_yomiCard[0].gameObject.activeSelf) return;
            _yomiPop = Mathf.MoveTowards(_yomiPop, 1f, Time.unscaledDeltaTime * 4.5f);
            _yomiDock = Mathf.MoveTowards(_yomiDock, _yomiDocked ? 1f : 0f, Time.unscaledDeltaTime * 4f);
            LayoutYomiCards();
        }

        void LayoutYomiCards()
        {
            // entrada con overshoot (ease-out-back) + viaje al costado al dockear
            float t = _yomiPop - 1f;
            const float k = 1.70158f;
            float pop = 1f + (k + 1f) * t * t * t + k * t * t;
            float dock = _yomiDock * _yomiDock * (3f - 2f * _yomiDock);
            float scale = Mathf.Lerp(1f, 0.5f, dock) * Mathf.Max(0.05f, pop);
            for (int i = 0; i < 2; i++)
            {
                float sign = i == 0 ? -1f : 1f;
                var pos = new Vector2(sign * Mathf.Lerp(310f, 640f, dock), Mathf.Lerp(120f, 288f, dock));
                _yomiCard[i].rectTransform.anchoredPosition = pos;
                _yomiCard[i].rectTransform.localScale = new Vector3(scale, scale, 1f);
            }
            var vc = _yomiVs.color;
            vc.a = Mathf.Clamp01(_yomiPop * 1.4f - 0.4f);
            _yomiVs.color = vc;
            // el fallo aparece un pelo después de las cartas y queda durante la acción
            var ec = _yomiExplain.color;
            ec.a = Mathf.Clamp01(_yomiPop * 2f - 1f);
            _yomiExplain.color = ec;
        }

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
            => MakeTextCore(parent, name, content, anchor, pos, size, fontSize, color, align, _font);

        // texto con la fuente pixel (labels cortos, jerarquía)
        public Text MakeTextP(RectTransform parent, string name, string content, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize, Color color, TextAnchor align)
            => MakeTextCore(parent, name, content, anchor, pos, size, fontSize, color, align, _pixel);

        Text MakeTextCore(RectTransform parent, string name, string content, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize, Color color, TextAnchor align, Font font)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var t = go.GetComponent<Text>();
            t.font = font;
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
