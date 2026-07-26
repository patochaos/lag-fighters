using System;
using UnityEngine;
using UnityEngine.UI;

namespace LagFighter
{
    // La mesa de CANTOS del modo DUELO (DUELO.md §11-12): los botones para
    // cantar (¡ENVIDO! / ¡TRUCO!), el modal de respuesta cuando te cantan
    // (QUIERO / NO QUIERO / SUBIR), el cartel de resultado y la línea de
    // estado (round, marcador, truco armado, tanto cantado).
    //
    // La NEGOCIACIÓN la orquesta MatchController — esto solo muestra botones
    // y avisa qué se clickeó. La sim ni se entera de que existe.
    public class DuelCantoUI : MonoBehaviour
    {
        MatchController _mc;
        RectTransform _canvasRt;

        // ofertas: cantar vos, durante la planificación
        GameObject _offersRoot;
        Image _btnEnvido, _btnTruco;

        // modal: te cantaron, respondé
        GameObject _modalRoot;
        Image[] _modalBtns;
        Color[] _modalCols;
        Action<int> _onModal;
        public bool ModalOpen => _modalRoot != null;

        // banner de resultado del canto
        Text _banner;
        float _bannerT;

        // estado persistente arriba
        Text _status;

        public static DuelCantoUI Create(MatchController mc)
        {
            var go = new GameObject("LagFighter.DuelCanto");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 23;   // arriba de la mano (21) y el HUD duel (20)
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var ui = go.AddComponent<DuelCantoUI>();
            ui._mc = mc;
            ui._canvasRt = go.GetComponent<RectTransform>();
            ui.BuildStatus();
            return ui;
        }

        // ---- helpers (mismo idioma que DuelHudUI) ----

        static Image Img(RectTransform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color c, bool ray = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var i = go.GetComponent<Image>();
            i.color = c;
            i.raycastTarget = ray;
            return i;
        }

        static Text Txt(RectTransform parent, string name, string s, Vector2 anchor, Vector2 pos, Vector2 size,
            int fs, Color c, TextAnchor align, DuelHandUI.Face face = DuelHandUI.Face.Data, bool wrap = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var t = go.GetComponent<Text>();
            t.font = face == DuelHandUI.Face.Pixel ? UIFonts.Pixel : face == DuelHandUI.Face.Data ? UIFonts.Data : UIFonts.Para;
            t.text = s;
            t.fontSize = fs;
            t.color = c;
            t.alignment = align;
            t.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        void BuildStatus()
        {
            _status = Txt(_canvasRt, "Status", "", new Vector2(0.5f, 1f), new Vector2(0f, -138f),
                new Vector2(900f, 26f), 21, Duelo.Mute, TextAnchor.MiddleCenter);
        }

        public void SetVisible(bool on)
        {
            if (!on) { HideOffers(); CloseModal(); if (_banner != null) { Destroy(_banner.gameObject); _banner = null; } }
            gameObject.SetActive(on);
        }

        // ---- ofertas ----

        public void ShowOffers(bool envido, bool truco)
        {
            HideOffers();
            if (!envido && !truco) return;
            _offersRoot = new GameObject("Offers", typeof(RectTransform));
            var rt = _offersRoot.GetComponent<RectTransform>();
            rt.SetParent(_canvasRt, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(-640f, 150f);
            rt.sizeDelta = Vector2.zero;
            float y = 0f;
            if (envido)
            {
                _btnEnvido = OfferBtn(rt, "¡ENVIDO!", "apuesta de apertura", Duelo.Gold, y);
                y += 66f;
            }
            if (truco)
                _btnTruco = OfferBtn(rt, "¡TRUCO!", "el intercambio vale ×2", Duelo.Golpe, y);
        }

        Image OfferBtn(RectTransform parent, string label, string sub, Color c, float y)
        {
            var bg = Img(parent, label, new Vector2(0.5f, 0f), new Vector2(0f, y), new Vector2(210f, 58f), Duelo.Wash(c, 0.26f));
            DuelHandUI.Brackets(bg.rectTransform, 210f, 58f, Duelo.Alpha(c, 0.8f));
            Txt(bg.rectTransform, "T", label, new Vector2(0.5f, 0.5f), new Vector2(0f, 8f),
                new Vector2(200f, 26f), 24, c, TextAnchor.MiddleCenter);
            Txt(bg.rectTransform, "S", sub, new Vector2(0.5f, 0.5f), new Vector2(0f, -16f),
                new Vector2(200f, 18f), 13, Duelo.Alpha(c, 0.6f), TextAnchor.MiddleCenter);
            return bg;
        }

        public void HideOffers()
        {
            if (_offersRoot != null) Destroy(_offersRoot);
            _offersRoot = null;
            _btnEnvido = _btnTruco = null;
        }

        // ---- modal de respuesta ----

        public void ShowModal(string title, string sub, string[] options, Color accent, Action<int> onPick)
        {
            CloseModal();
            HideOffers();
            _onModal = onPick;
            _modalRoot = new GameObject("Modal", typeof(RectTransform));
            var rt = _modalRoot.GetComponent<RectTransform>();
            rt.SetParent(_canvasRt, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            Img(rt, "Dim", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4000f, 3000f),
                new Color(0.02f, 0.03f, 0.06f, 0.72f), ray: true);

            float w = Mathf.Max(680f, options.Length * 230f + 40f);
            var panel = Img(rt, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(w, 250f), Duelo.Panel);
            DuelHandUI.Brackets(panel.rectTransform, w, 250f, Duelo.Alpha(accent, 0.9f), len: 26f);
            Txt(panel.rectTransform, "Title", title, new Vector2(0.5f, 1f), new Vector2(0f, -46f),
                new Vector2(w - 40f, 44f), 40, accent, TextAnchor.MiddleCenter);
            Txt(panel.rectTransform, "Sub", sub, new Vector2(0.5f, 1f), new Vector2(0f, -92f),
                new Vector2(w - 60f, 40f), 19, Duelo.Paper, TextAnchor.MiddleCenter, DuelHandUI.Face.Para, wrap: true);

            _modalBtns = new Image[options.Length];
            _modalCols = new Color[options.Length];
            float bw = 214f, gap = 16f;
            float total = options.Length * bw + (options.Length - 1) * gap;
            for (int i = 0; i < options.Length; i++)
            {
                // QUIERO verde · NO QUIERO gris · SUBIR rojo
                Color c = i == 0 ? Duelo.Escape : i == 1 ? Duelo.Mute : Duelo.Golpe;
                float x = -total * 0.5f + bw * 0.5f + i * (bw + gap);
                var b = Img(panel.rectTransform, "B" + i, new Vector2(0.5f, 0f), new Vector2(x, 46f),
                    new Vector2(bw, 56f), Duelo.Wash(c, 0.26f));
                DuelHandUI.Brackets(b.rectTransform, bw, 56f, Duelo.Alpha(c, 0.75f));
                Txt(b.rectTransform, "T", options[i], new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(bw - 12f, 46f), 21, c, TextAnchor.MiddleCenter);
                _modalBtns[i] = b;
                _modalCols[i] = c;
            }
        }

        void CloseModal()
        {
            if (_modalRoot != null) Destroy(_modalRoot);
            _modalRoot = null;
            _modalBtns = null;
            _onModal = null;
        }

        // ---- banner ----

        public void Banner(string s, Color c)
        {
            if (_banner != null) Destroy(_banner.gameObject);
            _banner = Txt(_canvasRt, "Banner", s, new Vector2(0.5f, 0.5f), new Vector2(0f, 210f),
                new Vector2(1400f, 60f), 44, c, TextAnchor.MiddleCenter);
            _bannerT = 2.6f;
            SfxLib.Play(SfxLib.Kind.UiClick, 1f);
        }

        // ---- tick ----

        void Update()
        {
            if (_mc == null || _mc.Duel == null || !SimConfig.DuelEnabled) return;

            // línea de estado: round, marcador, truco armado, tanto cantado
            var d = _mc.Duel;
            bool hot = d.TrucoLevel > 0;
            if (d.Over) _status.text = "";
            else
            {
                string s = $"ROUND {d.Round}  ·  {d.RoundWins[0]}–{d.RoundWins[1]}";
                if (hot) s += $"   ·   ×{DuelSim.TrucoMult(d.TrucoLevel)} EN JUEGO";
                else if (d.TrucoChainUsed) s += "   ·   truco gastado este round";
                if (d.PublicTantoSide >= 0)
                    s += $"   ·   {(d.PublicTantoSide == 0 ? "CANTASTE" : "TE CANTÓ")} {d.PublicTanto}";
                _status.text = s;
                _status.color = hot ? Duelo.Gold : Duelo.Mute;
            }

            if (_banner != null)
            {
                _bannerT -= Time.unscaledDeltaTime;
                if (_bannerT <= 0.8f)
                    _banner.color = Duelo.Alpha(_banner.color, Mathf.Clamp01(_bannerT / 0.8f));
                if (_bannerT <= 0f) { Destroy(_banner.gameObject); _banner = null; }
            }

            var mp = GameInput.MousePos();

            if (_modalRoot != null && _modalBtns != null)
            {
                int over = -1;
                for (int i = 0; i < _modalBtns.Length; i++)
                {
                    bool o = RectTransformUtility.RectangleContainsScreenPoint(_modalBtns[i].rectTransform, mp, null);
                    _modalBtns[i].color = Duelo.Wash(_modalCols[i], o ? 0.5f : 0.26f);
                    if (o) over = i;
                }
                if (over >= 0 && GameInput.ClickPressed())
                {
                    SfxLib.Play(SfxLib.Kind.UiClick, 0.9f);
                    var cb = _onModal;
                    CloseModal();
                    cb?.Invoke(over);
                }
                return;   // el modal bloquea las ofertas
            }

            bool overE = _btnEnvido != null && RectTransformUtility.RectangleContainsScreenPoint(_btnEnvido.rectTransform, mp, null);
            bool overT = _btnTruco != null && RectTransformUtility.RectangleContainsScreenPoint(_btnTruco.rectTransform, mp, null);
            if (_btnEnvido != null) _btnEnvido.color = Duelo.Wash(Duelo.Gold, overE ? 0.5f : 0.26f);
            if (_btnTruco != null) _btnTruco.color = Duelo.Wash(Duelo.Golpe, overT ? 0.5f : 0.26f);
            if (!GameInput.ClickPressed()) return;
            if (overE) { SfxLib.Play(SfxLib.Kind.UiClick, 0.9f); _mc.DuelSingEnvido(); }
            else if (overT) { SfxLib.Play(SfxLib.Kind.UiClick, 0.9f); _mc.DuelSingTruco(); }
        }
    }
}
