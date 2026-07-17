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
        const float PxPerFrame = RowW / SimConfig.TurnFrames;

        MatchController _mc;
        Font _font;
        RectTransform _canvasRt;
        readonly Image[][] _pips = new Image[2][];
        Text _banner, _prompt;
        readonly Text[] _feedback = new Text[2];
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

            _feedback[i] = MakeText(_canvasRt, label + "Feedback", "", anchor, new Vector2(sign * 40f, -116f), new Vector2(640f, 34f),
                24, Color.white, left ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight);
            _feedback[i].rectTransform.pivot = anchor;
            _feedback[i].fontStyle = FontStyle.Bold;
        }

        public void SetPrompt(string s) => _prompt.text = s;

        public void OnMatchReset()
        {
            _fbTimer[0] = _fbTimer[1] = 0f;
            _feedback[0].text = _feedback[1].text = "";
            _banner.text = "";
        }

        public void OnSimEvent(SimEvent ev)
        {
            int atk = ev.Attacker;
            string mv = MoveCatalog.All[ev.MoveIndex].Name.ToUpperInvariant();
            switch (ev.Kind)
            {
                case EvKind.Hit:
                    Feedback(atk, ev.Counter ? $"¡COUNTER! {mv} −{ev.Damage:0}" : $"{mv} −{ev.Damage:0}",
                        ev.Counter ? new Color(1f, 0.55f, 0.15f) : Color.white);
                    break;
                case EvKind.Blocked:
                    Feedback(1 - atk, "BLOQUEADO", new Color(0.5f, 0.75f, 1f));
                    break;
                case EvKind.Whiff:
                    Feedback(atk, $"{mv}: AL AIRE", new Color(1f, 1f, 1f, 0.55f));
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
                if (_fbTimer[i] > 0f)
                {
                    _fbTimer[i] -= Time.deltaTime;
                    var c = _feedback[i].color;
                    c.a = Mathf.Clamp01(_fbTimer[i] / 0.4f);
                    _feedback[i].color = c;
                }
            }

            bool executing = _mc.State == MatchController.Flow.Executing || _mc.State == MatchController.Flow.Replay;
            float playX = executing ? Mathf.Clamp((_mc.TickFloat - _mc.TurnStartTick) * PxPerFrame, 0f, RowW) : -1f;
            _row0.UpdateRow(_mc.GetPlan(0), _mc.RowRevealed(0), playX);
            _row1.UpdateRow(_mc.GetPlan(1), _mc.RowRevealed(1), playX);

            if (_mc.State == MatchController.Flow.GameOver)
                _banner.text = (sim.Winner == 0 ? "¡GANASTE!" : sim.Winner == 1 ? "PERDISTE" : "DOBLE KO")
                               + "\n<size=28>V ver la pelea de corrido · R reiniciar · M menú</size>";
            else
                _banner.text = "";
        }

        public static Color ChipColor(int moveIndex)
        {
            switch (moveIndex)
            {
                case MoveCatalog.AttackA: return new Color(0.9f, 0.32f, 0.24f);
                case MoveCatalog.AttackB: return new Color(0.65f, 0.3f, 0.85f);
                case MoveCatalog.Guard: return new Color(0.25f, 0.5f, 0.9f);
                case MoveCatalog.DashF:
                case MoveCatalog.DashB: return new Color(0.2f, 0.72f, 0.72f);
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
                case MoveCatalog.Guard: return "G";
                case MoveCatalog.WalkF: return "→";
                case MoveCatalog.WalkB: return "←";
                case MoveCatalog.DashF: return "»";
                case MoveCatalog.DashB: return "«";
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

                _playhead = hud.MakeImage(_area, "Playhead", new Vector2(0f, 0.5f), Vector2.zero, new Vector2(3f, height), Color.white);
                _playhead.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            }

            public void UpdateRow(List<int> queue, bool revealed, float playX)
            {
                _hidden.gameObject.SetActive(!revealed);
                _playhead.gameObject.SetActive(playX >= 0f);
                if (playX >= 0f) _playhead.rectTransform.anchoredPosition = new Vector2(playX, 0f);

                int used = 0;
                if (revealed && queue != null)
                {
                    float x = 0f;
                    foreach (var mi in queue)
                    {
                        var m = MoveCatalog.All[mi];
                        float w = m.Total * PxPerFrame - 2f;
                        var chip = GetChip(used++);
                        chip.rectTransform.anchoredPosition = new Vector2(x, 0f);
                        chip.rectTransform.sizeDelta = new Vector2(w, _height - 8f);
                        var c = ChipColor(mi);
                        if (_dim) c = new Color(c.r, c.g, c.b, 0.8f);
                        chip.color = c;
                        _labels[used - 1].text = ChipLabel(mi);
                        x += m.Total * PxPerFrame;
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
