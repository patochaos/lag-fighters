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
        const float PipW = 42f, PipGap = 46f;
        const float GuardBarW = SimConfig.MaxHp * PipGap - (PipGap - PipW);
        Text _banner, _prompt, _turnSummary;
        string _bannerOverride = "";

        // tira de conexión
        Text _connText;
        readonly Image[] _wifiBars = new Image[4];

        // ajustes [OPC]
        Image _optBtn, _optPanel;
        Text _optBtnLabel;
        bool _optOpen;
        Image _boxBtn, _voiceBtn;
        Text _boxBtnLabel, _voiceBtnLabel;
        static readonly float[] Speeds = { 0.5f, 1f, 2f };
        readonly Image[] _speedBtns = new Image[3];
        readonly Text[] _speedLabels = new Text[3];

        // log lateral de turnos, colapsable con L (o desde OPC)
        Image _logBtn, _logPanel;
        Text _logBtnLabel, _logText;
        bool _logOpen;
        readonly List<string> _logLines = new List<string>();

        Text _lagMsg;
        float _lagMsgTimer;
        readonly Text[] _limbLabel = new Text[2];
        TimelineRow _row0, _row1;

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
            _planOverlay = MakeImage(_canvasRt, "PlanOverlay", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.25f, 0.5f, 0.9f, 0.08f));
            var por = _planOverlay.rectTransform;
            por.anchorMin = Vector2.zero;
            por.anchorMax = Vector2.one;
            por.offsetMin = por.offsetMax = Vector2.zero;

            BuildSide(0, left: true, "VOS");
            BuildSide(1, left: false, "RIVAL");

            // tira de conexión (dist + ping + wifi), estética overlay de stream
            var strip = MakePanel(_canvasRt, "ConnStrip", new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(430f, 34f), Palette.Neutral);
            _connText = MakeTextP(strip.rectTransform, "Conn", "", new Vector2(0.5f, 0.5f), new Vector2(-24f, 0f), new Vector2(360f, 22f),
                10, new Color(1f, 1f, 1f, 0.8f), TextAnchor.MiddleCenter);
            for (int b = 0; b < 4; b++)
            {
                _wifiBars[b] = MakeImage(strip.rectTransform, "Wifi" + b, new Vector2(1f, 0f),
                    new Vector2(-52f + b * 11f, 7f), new Vector2(7f, 6f + b * 6f), Palette.Ok);
                _wifiBars[b].rectTransform.pivot = new Vector2(0.5f, 0f);
            }

            _prompt = MakeTextP(_canvasRt, "Prompt", "", new Vector2(0.5f, 1f), new Vector2(0f, -76f), new Vector2(1500f, 30f),
                13, Palette.Startup, TextAnchor.MiddleCenter);

            _turnSummary = MakeText(_canvasRt, "TurnSummary", "", new Vector2(0.5f, 1f), new Vector2(0f, -102f), new Vector2(1400f, 22f),
                16, new Color(0.85f, 0.9f, 1f, 0.8f), TextAnchor.MiddleCenter);

            // timelines del turno (fila propia abajo, rival arriba) — LAS
            // protagonistas: acá se cargan los movimientos, que se vean bien
            _row1 = new TimelineRow(this, "Row1", y: 246f, height: 52f, dim: true, side: 1);
            _row0 = new TimelineRow(this, "Row0", y: 312f, height: 52f, dim: false, side: 0);
            MakeTextP(_canvasRt, "Row0Label", "VOS", new Vector2(0.5f, 0f), new Vector2(-RowW / 2f - 52f, 312f + 26f), new Vector2(90f, 20f),
                9, Palette.P1, TextAnchor.MiddleRight);
            MakeTextP(_canvasRt, "Row1Label", "RIVAL", new Vector2(0.5f, 0f), new Vector2(-RowW / 2f - 52f, 246f + 26f), new Vector2(90f, 20f),
                9, Palette.P2, TextAnchor.MiddleRight);

            _banner = MakeText(_canvasRt, "Banner", "", new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(1200f, 160f),
                58, Color.white, TextAnchor.MiddleCenter);
            _banner.fontStyle = FontStyle.Bold;

            // cartel grande (IT GETS LAGGIER / COUNTER / K.O.)
            _lagMsg = MakeTextP(_canvasRt, "LagMsg", "", new Vector2(0.5f, 0.5f), new Vector2(0f, 280f), new Vector2(1600f, 120f),
                30, new Color(1f, 0.35f, 0.3f), TextAnchor.MiddleCenter);

            // REPLAY + SKIP, arriba al medio (solo visible durante la repetición)
            _replayPanel = MakePanel(_canvasRt, "ReplayPanel", new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(430f, 44f), Palette.Damage);
            _replayTitle = MakeTextP(_replayPanel.rectTransform, "T", "► REPLAY", new Vector2(0f, 0.5f), new Vector2(120f, 0f), new Vector2(220f, 24f),
                13, new Color(1f, 0.4f, 0.35f), TextAnchor.MiddleLeft);
            _skipBtn = MakeImage(_replayPanel.rectTransform, "SkipBtn", new Vector2(1f, 0.5f), new Vector2(-84f, 0f), new Vector2(150f, 32f), new Color(0.1f, 0.12f, 0.16f, 0.95f));
            MakeTextP(_skipBtn.rectTransform, "T", "SKIP (ESP)", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(146f, 20f),
                9, new Color(1f, 1f, 1f, 0.85f), TextAnchor.MiddleCenter);
            _replayPanel.gameObject.SetActive(false);

            // ajustes [OPC] colapsados, esquina derecha bajo el bloque del rival
            _optBtn = MakePanel(_canvasRt, "OptBtn", new Vector2(1f, 1f), new Vector2(-28f, -150f), new Vector2(96f, 30f), Palette.Neutral);
            _optBtn.rectTransform.pivot = new Vector2(1f, 1f);
            _optBtnLabel = MakeTextP(_optBtn.rectTransform, "T", "OPC", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(92f, 20f),
                10, new Color(1f, 1f, 1f, 0.75f), TextAnchor.MiddleCenter);

            _optPanel = MakePanel(_canvasRt, "OptPanel", new Vector2(1f, 1f), new Vector2(-28f, -186f), new Vector2(210f, 176f), Palette.Neutral);
            _optPanel.rectTransform.pivot = new Vector2(1f, 1f);
            var opr = _optPanel.rectTransform;

            _boxBtn = MakeImage(opr, "BoxBtn", new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(186f, 30f), new Color(0.1f, 0.12f, 0.16f, 0.95f));
            _boxBtnLabel = MakeTextP(_boxBtn.rectTransform, "T", VizPrefs.ShowBoxes ? "CAJAS: ON" : "CAJAS: OFF",
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(182f, 20f),
                9, VizPrefs.ShowBoxes ? Palette.Ok : new Color(1f, 1f, 1f, 0.5f), TextAnchor.MiddleCenter);

            _voiceBtn = MakeImage(opr, "VoiceBtn", new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(186f, 30f), new Color(0.1f, 0.12f, 0.16f, 0.95f));
            _voiceBtnLabel = MakeTextP(_voiceBtn.rectTransform, "T", "VOZ: ON", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(182f, 20f),
                9, Palette.Ok, TextAnchor.MiddleCenter);

            _logBtn = MakeImage(opr, "LogBtn", new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(186f, 30f), new Color(0.1f, 0.12f, 0.16f, 0.95f));
            _logBtnLabel = MakeTextP(_logBtn.rectTransform, "T", "LOG (L)", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(182f, 20f),
                9, new Color(1f, 1f, 1f, 0.75f), TextAnchor.MiddleCenter);

            MakeTextP(opr, "SpeedTag", "VEL", new Vector2(0f, 1f), new Vector2(14f, -146f), new Vector2(50f, 20f),
                9, new Color(1f, 1f, 1f, 0.5f), TextAnchor.MiddleLeft);
            for (int s = 0; s < Speeds.Length; s++)
            {
                _speedBtns[s] = MakeImage(opr, "Speed" + s, new Vector2(0f, 1f),
                    new Vector2(58f + s * 48f, -146f), new Vector2(44f, 26f), new Color(0.1f, 0.12f, 0.16f, 0.95f));
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
                11, Color.white, TextAnchor.MiddleCenter);
            return b;
        }

        void SetGameOverButtons(bool on)
        {
            _btnRematch.gameObject.SetActive(on);
            _btnReplay.gameObject.SetActive(on && _mc.HasReplay);
            _btnMenu.gameObject.SetActive(on);
        }

        public void SetTurnSummary(string s) => _turnSummary.text = s;

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

        public void ShowLagMessage(string msg) => ShowBigMessage(msg, new Color(1f, 0.35f, 0.3f));

        public void ShowBigMessage(string msg, Color c, float duration = 2.6f)
        {
            _lagMsg.text = msg;
            _lagMsg.color = c;
            _lagMsgTimer = duration;
        }

        void BuildSide(int i, bool left, string label)
        {
            float sign = left ? 1f : -1f;
            var anchor = left ? new Vector2(0f, 1f) : new Vector2(1f, 1f);
            var color = Palette.Side(i);

            // panel contenedor del bloque de jugador
            var panel = MakePanel(_canvasRt, label + "Panel", anchor, new Vector2(sign * 24f, -22f), new Vector2(GuardBarW + 32f, 112f), color);
            panel.rectTransform.pivot = anchor;
            var pr = panel.rectTransform;

            var nm = MakeTextP(pr, "Name", label, new Vector2(left ? 0f : 1f, 1f), new Vector2(sign * 14f, -8f), new Vector2(200f, 20f),
                11, color, left ? TextAnchor.UpperLeft : TextAnchor.UpperRight);
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

            _limbLabel[i] = MakeTextP(pr, "Limbs", "", new Vector2(left ? 0f : 1f, 1f), new Vector2(sign * 14f, -96f), new Vector2(400f, 14f),
                8, new Color(1f, 0.45f, 0.35f), left ? TextAnchor.UpperLeft : TextAnchor.UpperRight);
            _limbLabel[i].rectTransform.pivot = new Vector2(left ? 0f : 1f, 1f);
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
            if (replaying)
            {
                var rc = _replayTitle.color;
                rc.a = 0.65f + Mathf.PingPong(Time.time * 1.6f, 0.35f); // parpadeo estilo VHS
                _replayTitle.color = rc;
                if (GameInput.ClickPressed() && Inside(_skipBtn, GameInput.MousePos()))
                {
                    _mc.SkipReplay();
                    return;
                }
            }
            if (flow != _prevFlow)
            {
                if (flow == MatchController.Flow.Executing && _prevFlow == MatchController.Flow.Planning)
                    ShowBigMessage("¡EJECUTANDO!", new Color(0.5f, 0.95f, 1f), 0.8f);
                _prevFlow = flow;
            }

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
                for (int p = 0; p < SimConfig.MaxHp; p++)
                {
                    var c = _pips[i][p].color;
                    c.a = p < sim.Fighters[i].Hp ? 1f : 0.13f;
                    _pips[i][p].color = c;
                }

                var lf = sim.Fighters[i];
                _limbLabel[i].text = lf.ArmHp <= 0f && lf.LegHp <= 0f ? "SIN BRAZO · SIN PIERNA"
                                   : lf.ArmHp <= 0f ? "SIN BRAZO" : lf.LegHp <= 0f ? "SIN PIERNA" : "";

                float g = sim.Fighters[i].Guard / SimConfig.GuardMax;
                _guardFill[i].rectTransform.sizeDelta = new Vector2(GuardBarW * g, 9f);
                _guardFill[i].color = g <= 0.25f
                    ? Color.Lerp(Palette.Guard, new Color(1f, 0.2f, 0.15f), Mathf.PingPong(Time.time * 4f, 1f))
                    : Palette.Guard;

                for (int w = 0; w < MatchController.RoundsToWin; w++)
                {
                    var c = _winPips[i][w].color;
                    c.a = w < _mc.GetWins(i) ? 1f : 0.14f;
                    _winPips[i][w].color = c;
                }

                // badge de estado sobre la cabeza (world-space)
                string badge = "";
                Color bc = Color.white;
                if (sim.IsStunned(i))
                {
                    int rem = sim.StunRemaining(i);
                    switch (sim.Fighters[i].Stun)
                    {
                        case StunKind.Knockdown: badge = $"KD {rem}F"; bc = new Color(1f, 0.5f, 0.2f); break;
                        case StunKind.Blockstun: badge = $"BLOCK {rem}F"; bc = Palette.Block; break;
                        default: badge = $"HIT {rem}F"; bc = Palette.Damage; break;
                    }
                }
                else if (sim.IsBlockingState(i) && executing)
                {
                    badge = "GUARD";
                    bc = new Color(Palette.Block.r, Palette.Block.g, Palette.Block.b, 0.7f);
                }
                WorldFX.SetBadge(i, sim.Fighters[i].X, 2.35f, badge, bc);
            }

            float playX = executing ? Mathf.Clamp((_mc.TickFloat - _mc.TurnStartTick) * PxPerFrame, 0f, RowW) : -1f;
            _row0.UpdateRow(_mc.GetPlan(0), _mc.RowRevealed(0), playX, _mc.DisplayStun(0), _mc.TurnStartStunKind[0]);
            _row1.UpdateRow(_mc.GetPlan(1), _mc.RowRevealed(1), playX, _mc.DisplayStun(1), _mc.TurnStartStunKind[1]);

            UpdateTimelineInteraction(flow);
            UpdateConnStrip(sim);
            UpdateBigMessage();
            UpdateOptions();
            UpdateBanner(sim, flow);
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
            float start = _mc.DisplayStun(_mc.Picker);
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
            if (_lagMsgTimer <= 0f) return;
            _lagMsgTimer -= Time.deltaTime;
            var lc = _lagMsg.color;
            lc.a = Mathf.Clamp01(_lagMsgTimer / 0.5f);
            _lagMsg.color = lc;
            if (_lagMsgTimer <= 0f) _lagMsg.text = "";
        }

        void UpdateOptions()
        {
            bool click = GameInput.ClickPressed();
            var mp = GameInput.MousePos();

            if (click && Inside(_optBtn, mp))
            {
                _optOpen = !_optOpen;
                _optPanel.gameObject.SetActive(_optOpen);
                _optBtnLabel.color = _optOpen ? Palette.Ok : new Color(1f, 1f, 1f, 0.75f);
            }

            bool toggleBoxes = GameInput.BoxesPressed() || (click && _optOpen && Inside(_boxBtn, mp));
            if (toggleBoxes)
            {
                VizPrefs.ShowBoxes = !VizPrefs.ShowBoxes;
                _boxBtnLabel.text = VizPrefs.ShowBoxes ? "CAJAS: ON" : "CAJAS: OFF";
                _boxBtnLabel.color = VizPrefs.ShowBoxes ? Palette.Ok : new Color(1f, 1f, 1f, 0.5f);
            }

            if (click && _optOpen && Inside(_voiceBtn, mp))
            {
                Announcer.Enabled = !Announcer.Enabled;
                _voiceBtnLabel.text = Announcer.Enabled ? "VOZ: ON" : "VOZ: OFF";
                _voiceBtnLabel.color = Announcer.Enabled ? Palette.Ok : new Color(1f, 1f, 1f, 0.5f);
            }

            bool logToggle = GameInput.LogPressed() || (click && _optOpen && Inside(_logBtn, mp));
            if (logToggle)
            {
                _logOpen = !_logOpen;
                _logPanel.gameObject.SetActive(_logOpen);
                _logBtnLabel.color = _logOpen ? Palette.Ok : new Color(1f, 1f, 1f, 0.75f);
            }

            for (int s = 0; s < Speeds.Length; s++)
            {
                bool on = Mathf.Approximately(_mc.PlaybackSpeed, Speeds[s]);
                _speedBtns[s].color = on ? new Color(0.2f, 0.4f, 0.6f, 0.95f) : new Color(0.1f, 0.12f, 0.16f, 0.95f);
                _speedLabels[s].color = on ? Color.white : new Color(1f, 1f, 1f, 0.55f);
                if (click && _optOpen && Inside(_speedBtns[s], mp))
                    _mc.SetPlaybackSpeed(Speeds[s]);
            }
        }

        void UpdateBanner(MatchSim sim, MatchController.Flow flow)
        {
            bool over = flow == MatchController.Flow.GameOver && _bannerOverride == "";
            SetGameOverButtons(over);
            if (over)
            {
                _banner.text = (sim.Winner == 0 ? "¡GANASTE LA PELEA!" : sim.Winner == 1 ? "PERDISTE LA PELEA" : "DOBLE KO")
                               + $"\n<size=30>{_mc.GetWins(0)} — {_mc.GetWins(1)}</size>";
                var mp = GameInput.MousePos();
                if (GameInput.ClickPressed())
                {
                    if (Inside(_btnRematch, mp)) _mc.RequestRematch();
                    else if (_btnReplay.gameObject.activeSelf && Inside(_btnReplay, mp)) _mc.RequestReplay();
                    else if (Inside(_btnMenu, mp)) _mc.GoToModeSelect();
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
                case MoveCatalog.Wait: return Palette.Neutral;
                case MoveCatalog.Crouch: return new Color(0.35f, 0.55f, 0.85f);
                case MoveCatalog.LowKick: return new Color(0.75f, 0.28f, 0.3f);
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
                case MoveCatalog.Crouch: return "▼";
                case MoveCatalog.LowKick: return "b";
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
            Image _lagSeg;
            Text _lagLabel;
            int _lastStunShown = -1; // para no armar el string del stun por frame
            readonly Text _hidden;
            readonly float _height;
            readonly bool _dim;
            readonly List<Image> _chips = new List<Image>();
            readonly List<Text> _labels = new List<Text>();

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
                    12, new Color(1f, 1f, 1f, 0.3f), TextAnchor.MiddleCenter);

                // highlight del espacio nuevo cuando sube el lag (pulsa en ámbar)
                _lagSeg = hud.MakeImage(_area, "LagSeg", new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, height), Palette.Guard);
                _lagSeg.rectTransform.pivot = new Vector2(0f, 0.5f);
                _lagLabel = hud.MakeTextP(_lagSeg.rectTransform, "L", "", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400f, 20f),
                    9, Palette.Guard, TextAnchor.MiddleCenter);
                _lagSeg.gameObject.SetActive(false);

                _stunSeg = hud.MakeImage(_area, "StunSeg", new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, height - 4f), Color.white);
                _stunSeg.rectTransform.pivot = new Vector2(0f, 0.5f);
                _stunLabel = hud.MakeText(_stunSeg.rectTransform, "L", "", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 22f),
                    14, Color.white, TextAnchor.MiddleCenter);
                _stunLabel.fontStyle = FontStyle.Bold;

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
                    _stunSeg.color = stunKind == StunKind.Blockstun ? new Color(0.3f, 0.5f, 0.85f, 0.75f)
                                   : stunKind == StunKind.Knockdown ? new Color(0.9f, 0.45f, 0.15f, 0.8f)
                                   : new Color(0.85f, 0.25f, 0.22f, 0.8f);
                    int shown = offset > 46f ? stunFrames : 0;
                    if (shown != _lastStunShown)
                    {
                        _lastStunShown = shown;
                        _stunLabel.text = shown > 0 ? $"−{shown}f" : "";
                    }
                }
                else
                {
                    _stunSeg.gameObject.SetActive(false);
                }

                int used = 0;
                if (revealed && queue != null)
                {
                    float x = offset;
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
                    var t = _hud.MakeText(img.rectTransform, "L", "", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64f, 30f), 21, Color.white, TextAnchor.MiddleCenter);
                    t.fontStyle = FontStyle.Bold;
                    _chips.Add(img);
                    _labels.Add(t);
                }
                _chips[i].gameObject.SetActive(true);
                return _chips[i];
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
