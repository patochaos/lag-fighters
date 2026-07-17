using UnityEngine;
using UnityEngine.UI;

namespace LagFighter
{
    // Menú inicial: Práctica / VS IA / 1v1 local. Teclas 1-3 o flechas+Enter.
    public class ModeMenuUI : MonoBehaviour
    {
        static readonly (string label, string desc, GameMode mode)[] Options =
        {
            ("PRÁCTICA", "Solo vos y un dummy quieto. Probá comandos, distancias y framedata.", GameMode.Practice),
            ("VS IA", "La CPU planifica su turno en secreto, igual que vos.", GameMode.VsAI),
            ("1v1 LOCAL", "Misma PC: planifica J1, después J2 (que no mire), y se ejecuta junto.", GameMode.PvP),
        };

        MatchController _mc;
        GameObject _root;
        Image[] _cards;
        Text _desc;
        int _sel;
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
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _root = new GameObject("Root", typeof(RectTransform), typeof(Image));
            var rootRt = _root.GetComponent<RectTransform>();
            rootRt.SetParent(canvasRt, false);
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = rootRt.offsetMax = Vector2.zero;
            _root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
            _root.GetComponent<Image>().raycastTarget = false;

            Txt(rootRt, "Title", "LAG FIGHTERS", new Vector2(0f, 220f), 78, Color.white, font, FontStyle.Bold);
            Txt(rootRt, "Sub", "programá 240 frames por turno · ejecución simultánea · footsies puro", new Vector2(0f, 158f), 22, new Color(1f, 0.9f, 0.4f), font, FontStyle.Normal);

            _cards = new Image[Options.Length];
            for (int i = 0; i < Options.Length; i++)
            {
                var card = new GameObject("Mode" + i, typeof(RectTransform), typeof(Image));
                var rt = card.GetComponent<RectTransform>();
                rt.SetParent(rootRt, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2((i - 1) * 320f, 20f);
                rt.sizeDelta = new Vector2(290f, 110f);
                _cards[i] = card.GetComponent<Image>();
                _cards[i].raycastTarget = false;

                Txt(rt, "K", (i + 1).ToString(), new Vector2(-115f, 30f), 20, new Color(1f, 1f, 1f, 0.5f), font, FontStyle.Normal);
                Txt(rt, "L", Options[i].label, new Vector2(0f, 0f), 30, Color.white, font, FontStyle.Bold);
            }

            _desc = Txt(rootRt, "Desc", "", new Vector2(0f, -80f), 20, new Color(1f, 1f, 1f, 0.85f), font, FontStyle.Normal);
            Txt(rootRt, "Help", "1-3 o ←/→ + Enter · durante la partida: R reinicia, M vuelve acá", new Vector2(0f, -140f), 17, new Color(1f, 1f, 1f, 0.5f), font, FontStyle.Normal);
        }

        Text Txt(RectTransform parent, string name, string content, Vector2 pos, int size, Color color, Font font, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(1400f, 90f);
            var t = go.GetComponent<Text>();
            t.font = font;
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

        public void Open()
        {
            _root.SetActive(true);
            _active = true;
            Highlight(_sel);
        }

        public void Close()
        {
            _active = false;
            if (_root != null) _root.SetActive(false);
        }

        void Highlight(int idx)
        {
            _sel = Mathf.Clamp(idx, 0, Options.Length - 1);
            for (int i = 0; i < _cards.Length; i++)
                _cards[i].color = i == _sel ? new Color(0.25f, 0.42f, 0.62f, 0.95f) : new Color(0.12f, 0.13f, 0.17f, 0.9f);
            _desc.text = Options[_sel].desc;
        }

        void Update()
        {
            if (!_active) return;
            if (GameInput.ClickPressed())
            {
                var pos = GameInput.MousePos();
                for (int i = 0; i < _cards.Length; i++)
                {
                    if (!RectTransformUtility.RectangleContainsScreenPoint(_cards[i].rectTransform, pos, null)) continue;
                    _mc.StartMatch(Options[i].mode);
                    return;
                }
            }
            if (GameInput.LeftPressed()) Highlight(_sel - 1);
            if (GameInput.RightPressed()) Highlight(_sel + 1);
            int n = GameInput.NumberPressed();
            if (n >= 1 && n <= Options.Length) { _mc.StartMatch(Options[n - 1].mode); return; }
            if (GameInput.ConfirmPressed()) _mc.StartMatch(Options[_sel].mode);
        }
    }
}
