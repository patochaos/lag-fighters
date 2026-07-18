using UnityEngine;
using UnityEngine.UI;

namespace LagFighter
{
    // Menú inicial en dos pasos:
    //  1) NORMAL o LAG MODE (cada 3 turnos el lag sube 50%)
    //  2) Práctica / VS IA / 1v1 local / POR CÓDIGO
    // Teclas 1-3, flechas+Enter, o click.
    public class ModeMenuUI : MonoBehaviour
    {
        static readonly (string label, string desc)[] LagOptions =
        {
            ("NORMAL", "Turnos de 60 frames, parejos toda la pelea."),
            ("LAG MODE", "Cada 3 turnos el lag sube 50%: 60 → 90 → 135 → 202 → 303 frames. It gets laggier."),
        };

        static readonly (string label, string desc, GameMode mode)[] Modes =
        {
            ("PRÁCTICA", "Solo vos y un dummy quieto. Probá comandos, distancias y framedata.", GameMode.Practice),
            ("VS IA", "La CPU planifica su turno en secreto, igual que vos.", GameMode.VsAI),
            ("1v1 LOCAL", "Misma PC: planifica J1, pantalla de 'pasá el teclado', planifica J2.", GameMode.PvP),
            ("POR CÓDIGO", "Pelea por chat: cada turno intercambian un código corto y ambos ven la misma pelea. Sin servidores.", GameMode.Async),
        };

        static readonly (string label, string desc)[] Sides =
        {
            ("SOY JUGADOR 1", "El de la izquierda (azul). Arreglen quién es quién antes de empezar."),
            ("SOY JUGADOR 2", "El de la derecha (naranja)."),
        };

        MatchController _mc;
        Font _font;
        GameObject _root;
        Image[] _cards;
        Text[] _cardLabels;
        Text _desc, _stepTitle;
        int _sel;
        int _step; // 0 = lag mode, 1 = modo de juego, 2 = lado (solo async)
        bool _lagChoice;
        bool _active;
        Vector2 _lastMouse;

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

            // splash art de fondo (si está importada) + announcer
            var splash = Resources.Load<Texture2D>("LagFighter/splash");
            if (splash != null)
            {
                var bgGo = new GameObject("Splash", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
                var bgRt = bgGo.GetComponent<RectTransform>();
                bgRt.SetParent(rootRt, false);
                bgRt.anchorMin = bgRt.anchorMax = new Vector2(0.5f, 0.5f);
                var raw = bgGo.GetComponent<RawImage>();
                raw.texture = splash;
                raw.raycastTarget = false;
                var fitter = bgGo.GetComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = splash.width / (float)splash.height;
                bgGo.transform.SetAsFirstSibling(); // detrás de todo lo demás… pero delante del velo oscuro
                _root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            }
            bool hasSplash = splash != null;
            if (!hasSplash)
            {
                Txt(rootRt, "Title", "LAG FIGHTERS", new Vector2(0f, 230f), 78, Color.white, FontStyle.Bold);
                Txt(rootRt, "Sub", "programá tu turno · ejecución simultánea · footsies puro", new Vector2(0f, 166f), 21, new Color(1f, 0.9f, 0.4f), FontStyle.Normal);
            }

            // banda oscura detrás de la parte interactiva (la splash es clara)
            var band = new GameObject("Band", typeof(RectTransform), typeof(Image));
            var bandRt = band.GetComponent<RectTransform>();
            bandRt.SetParent(rootRt, false);
            bandRt.anchorMin = bandRt.anchorMax = new Vector2(0.5f, 0.5f);
            bandRt.anchoredPosition = new Vector2(0f, -10f);
            bandRt.sizeDelta = new Vector2(1160f, 380f);
            band.GetComponent<Image>().color = new Color(0f, 0f, 0f, hasSplash ? 0.62f : 0.25f);
            band.GetComponent<Image>().raycastTarget = false;

            _stepTitle = Txt(rootRt, "Step", "", new Vector2(0f, 128f), 15, new Color(1f, 1f, 1f, 0.85f), FontStyle.Normal);
            _stepTitle.font = UIFonts.Pixel;

            // hasta 4 cartas (paso 0 usa 2, paso 1 usa 4, paso 2 usa 2)
            _cards = new Image[4];
            _cardLabels = new Text[4];
            for (int i = 0; i < 4; i++)
            {
                var card = new GameObject("Card" + i, typeof(RectTransform), typeof(Image));
                var rt = card.GetComponent<RectTransform>();
                rt.SetParent(rootRt, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(300f, 110f);
                _cards[i] = card.GetComponent<Image>();
                _cards[i].raycastTarget = false;

                var k = Txt(rt, "K", (i + 1).ToString(), new Vector2(-118f, 32f), 12, new Color(1f, 1f, 1f, 0.5f), FontStyle.Normal);
                k.font = UIFonts.Pixel;
                _cardLabels[i] = Txt(rt, "L", "", new Vector2(0f, 0f), 15, Color.white, FontStyle.Normal);
                _cardLabels[i].font = UIFonts.Pixel;
            }

            _desc = Txt(rootRt, "Desc", "", new Vector2(0f, -86f), 20, new Color(1f, 1f, 1f, 0.85f), FontStyle.Normal);
            Txt(rootRt, "Help", "1-4 o ←/→ + Enter · click también funciona · en partida: R reinicia, M vuelve acá",
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

        int OptionCount => _step == 0 ? LagOptions.Length : _step == 1 ? Modes.Length : Sides.Length;

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
            _stepTitle.text = _step == 0 ? "¿CUÁNTO LAG QUERÉS?" :
                              _step == 1 ? (_lagChoice ? "LAG MODE — elegí rival" : "NORMAL — elegí rival") :
                              "POR CÓDIGO — ¿de qué lado jugás?";
            float cardW = count >= 4 ? 300f : 330f;
            for (int i = 0; i < _cards.Length; i++)
            {
                bool on = i < count;
                _cards[i].gameObject.SetActive(on);
                if (!on) continue;
                float x = (i - (count - 1) * 0.5f) * cardW;
                _cards[i].rectTransform.anchoredPosition = new Vector2(x, 10f);
                _cardLabels[i].text = _step == 0 ? LagOptions[i].label : _step == 1 ? Modes[i].label : Sides[i].label;
                _cardLabels[i].fontSize = _step == 1 && count >= 4 ? 24 : 28;
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
            _desc.text = _step == 0 ? LagOptions[_sel].desc : _step == 1 ? Modes[_sel].desc : Sides[_sel].desc;
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
            if (_step == 1)
            {
                if (Modes[idx].mode == GameMode.Async)
                {
                    _step = 2;
                    _sel = 0;
                    Layout();
                    return;
                }
                _mc.StartMatch(Modes[idx].mode, _lagChoice);
                return;
            }
            _mc.StartMatch(GameMode.Async, _lagChoice, idx);
        }

        void Update()
        {
            if (!_active) return;

            // hover muestra la descripción de cada opción; click la confirma
            var mp = GameInput.MousePos();
            if ((mp - _lastMouse).sqrMagnitude > 4f)
            {
                _lastMouse = mp;
                for (int i = 0; i < OptionCount; i++)
                {
                    if (!RectTransformUtility.RectangleContainsScreenPoint(_cards[i].rectTransform, mp, null)) continue;
                    if (i != _sel) Highlight(i);
                    break;
                }
            }

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
