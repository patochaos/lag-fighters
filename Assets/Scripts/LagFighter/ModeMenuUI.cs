using UnityEngine;
using UnityEngine.UI;

namespace LagFighter
{
    // Menú inicial en dos pasos:
    //  1) NORMAL o LAG MODE (cada 4 turnos se duplican los frames del turno)
    //  2) Práctica / VS IA / 1v1 local
    // Teclas 1-3, flechas+Enter, o click.
    public class ModeMenuUI : MonoBehaviour
    {
        static readonly (string label, string desc)[] LagOptions =
        {
            ("NORMAL", "Turnos de 60 frames, parejos toda la pelea."),
            ("LAG MODE", "Cada 4 turnos el lag SE DUPLICA: 60 → 120 → 240 → 480 → 960 frames. It gets laggier."),
        };

        static readonly (string label, string desc, GameMode mode)[] Modes =
        {
            ("PRÁCTICA", "Solo vos y un dummy quieto. Probá comandos, distancias y framedata.", GameMode.Practice),
            ("VS IA", "La CPU planifica su turno en secreto, igual que vos.", GameMode.VsAI),
            ("1v1 LOCAL", "Misma PC: planifica J1, después J2 (que no mire), y se ejecuta junto.", GameMode.PvP),
        };

        MatchController _mc;
        Font _font;
        GameObject _root;
        Image[] _cards;
        Text[] _cardLabels;
        Text _desc, _stepTitle;
        int _sel;
        int _step; // 0 = lag mode, 1 = modo de juego
        bool _lagChoice;
        bool _active;

        public static ModeMenuUI Create(MatchController mc)
        {
            var go = new GameObject("LagFighter.ModeMenu");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var ui = go.AddComponent<ModeMenuUI>();
            ui._mc = mc;
            ui.Build(go.GetComponent<RectTransform>());
            ui._root.SetActive(false);
            return ui;
        }

        void Build(RectTransform canvasRt)
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _root = new GameObject("Root", typeof(RectTransform), typeof(Image));
            var rootRt = _root.GetComponent<RectTransform>();
            rootRt.SetParent(canvasRt, false);
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = rootRt.offsetMax = Vector2.zero;
            _root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);
            _root.GetComponent<Image>().raycastTarget = false;

            Txt(rootRt, "Title", "LAG FIGHTERS", new Vector2(0f, 230f), 78, Color.white, FontStyle.Bold);
            Txt(rootRt, "Sub", "programá tu turno · ejecución simultánea · footsies puro", new Vector2(0f, 166f), 21, new Color(1f, 0.9f, 0.4f), FontStyle.Normal);
            _stepTitle = Txt(rootRt, "Step", "", new Vector2(0f, 110f), 24, new Color(1f, 1f, 1f, 0.8f), FontStyle.Bold);

            // hasta 3 cartas (el paso 1 usa 2, el paso 2 usa 3)
            _cards = new Image[3];
            _cardLabels = new Text[3];
            for (int i = 0; i < 3; i++)
            {
                var card = new GameObject("Card" + i, typeof(RectTransform), typeof(Image));
                var rt = card.GetComponent<RectTransform>();
                rt.SetParent(rootRt, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(300f, 110f);
                _cards[i] = card.GetComponent<Image>();
                _cards[i].raycastTarget = false;

                Txt(rt, "K", (i + 1).ToString(), new Vector2(-118f, 32f), 20, new Color(1f, 1f, 1f, 0.5f), FontStyle.Normal);
                _cardLabels[i] = Txt(rt, "L", "", new Vector2(0f, 0f), 28, Color.white, FontStyle.Bold);
            }

            _desc = Txt(rootRt, "Desc", "", new Vector2(0f, -86f), 20, new Color(1f, 1f, 1f, 0.85f), FontStyle.Normal);
            Txt(rootRt, "Help", "1-3 o ←/→ + Enter · click también funciona · en partida: R reinicia, M vuelve acá",
                new Vector2(0f, -146f), 16, new Color(1f, 1f, 1f, 0.5f), FontStyle.Normal);
        }

        Text Txt(RectTransform parent, string name, string content, Vector2 pos, int size, Color color, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(1400f, 90f);
            var t = go.GetComponent<Text>();
            t.font = _font;
            t.text = content;
            t.fontSize = size;
            t.color = color;
            t.fontStyle = style;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        int OptionCount => _step == 0 ? LagOptions.Length : Modes.Length;

        public void Open()
        {
            _root.SetActive(true);
            _active = true;
            _step = 0;
            _sel = 0;
            Layout();
        }

        public void Close()
        {
            _active = false;
            if (_root != null) _root.SetActive(false);
        }

        void Layout()
        {
            int count = OptionCount;
            _stepTitle.text = _step == 0 ? "¿CUÁNTO LAG QUERÉS?" : (_lagChoice ? "LAG MODE — elegí rival" : "NORMAL — elegí rival");
            for (int i = 0; i < _cards.Length; i++)
            {
                bool on = i < count;
                _cards[i].gameObject.SetActive(on);
                if (!on) continue;
                float x = (i - (count - 1) * 0.5f) * 330f;
                _cards[i].rectTransform.anchoredPosition = new Vector2(x, 10f);
                _cardLabels[i].text = _step == 0 ? LagOptions[i].label : Modes[i].label;
            }
            Highlight(_sel);
        }

        void Highlight(int idx)
        {
            _sel = Mathf.Clamp(idx, 0, OptionCount - 1);
            for (int i = 0; i < OptionCount; i++)
            {
                bool lagCard = _step == 0 && i == 1;
                _cards[i].color = i == _sel
                    ? (lagCard ? new Color(0.6f, 0.25f, 0.15f, 0.98f) : new Color(0.25f, 0.42f, 0.62f, 0.98f))
                    : new Color(0.12f, 0.13f, 0.17f, 0.9f);
            }
            _desc.text = _step == 0 ? LagOptions[_sel].desc : Modes[_sel].desc;
        }

        void Confirm(int idx)
        {
            if (_step == 0)
            {
                _lagChoice = idx == 1;
                _step = 1;
                _sel = 1; // VS IA por defecto
                Layout();
                return;
            }
            _mc.StartMatch(Modes[idx].mode, _lagChoice);
        }

        void Update()
        {
            if (!_active) return;

            if (GameInput.ClickPressed())
            {
                var pos = GameInput.MousePos();
                for (int i = 0; i < OptionCount; i++)
                {
                    if (!RectTransformUtility.RectangleContainsScreenPoint(_cards[i].rectTransform, pos, null)) continue;
                    Confirm(i);
                    return;
                }
            }

            if (GameInput.LeftPressed()) Highlight(_sel - 1);
            if (GameInput.RightPressed()) Highlight(_sel + 1);
            int n = GameInput.NumberPressed();
            if (n >= 1 && n <= OptionCount) { Confirm(n - 1); return; }
            if (GameInput.ConfirmPressed()) Confirm(_sel);
        }
    }
}
