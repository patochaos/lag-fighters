using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace LagFighter
{
    // ---- MODO DUELO: el HUD (2026-07-25) ----
    // Todo lo PÚBLICO del duelo, de los dos lados y siempre a la vista: vida
    // exacta, mano, mazo, descarte y —lo que más importa para leer al rival—
    // QUÉ LE QUEDA de cada carta (total − descarte). Ese strip es la Ley 5
    // hecha interfaz: "ya gastó sus dos guardias altas → pegale arriba".
    // Arriba al centro vive el triángulo, que es todo el reglamento.
    public class DuelHudUI : MonoBehaviour
    {
        MatchController _mc;
        RectTransform _canvasRt;

        // panel por lado
        readonly Image[] _panel = new Image[2];
        readonly Text[] _who = new Text[2];
        readonly Text[] _hpNum = new Text[2];
        readonly Image[] _hpFill = new Image[2];
        readonly Text[] _piles = new Text[2];
        readonly Text[] _left = new Text[2];
        readonly Image[] _kdBadge = new Image[2];
        readonly Text[] _kdLbl = new Text[2];
        readonly Image[][] _backs = new Image[2][];   // "cartas en mano" como dorsos

        Text _rules, _turnLbl;
        // revelación: las dos cartas gigantes + el fallo
        GameObject _revealRoot;
        RectTransform[] _revealCard = new RectTransform[2];
        Text _ruling;
        float _revealT;
        bool _docked;

        const float PanelW = 470f, PanelH = 236f;
        const int MaxBacks = 8;

        DuelSim S => _mc.Duel;

        public static DuelHudUI Create(MatchController mc)
        {
            var go = new GameObject("LagFighter.DuelHud");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 19;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var ui = go.AddComponent<DuelHudUI>();
            ui._mc = mc;
            ui._canvasRt = go.GetComponent<RectTransform>();
            ui.Build();
            ui.SetVisible(false);
            return ui;
        }

        static Image Img(RectTransform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        static Text Txt(RectTransform parent, string name, string s, Vector2 anchor, Vector2 pos, Vector2 size,
            int size2, Color c, TextAnchor align, bool pixel = true)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var t = go.GetComponent<Text>();
            t.font = pixel ? UIFonts.Pixel : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = s;
            t.fontSize = size2;
            t.color = c;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        GameObject _root;

        void Build()
        {
            _root = new GameObject("Root", typeof(RectTransform));
            var rt = _root.GetComponent<RectTransform>();
            rt.SetParent(_canvasRt, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            for (int i = 0; i < 2; i++)
            {
                float sign = i == 0 ? 1f : -1f;   // izquierda para vos, derecha para el rival
                var anchor = new Vector2(i == 0 ? 0f : 1f, 1f);
                // pegado a su esquina y por DEBAJO del header del HUD clásico
                _panel[i] = Img(rt, "Panel" + i, anchor,
                    new Vector2(sign * (PanelW * 0.5f + 22f), -290f),
                    new Vector2(PanelW, PanelH), new Color(0.04f, 0.05f, 0.07f, 0.93f));
                var p = _panel[i].rectTransform;

                Img(p, "Accent", new Vector2(0.5f, 1f), new Vector2(0f, -3f), new Vector2(PanelW, 6f),
                    i == 0 ? new Color(0.35f, 0.7f, 1f, 0.95f) : new Color(1f, 0.5f, 0.35f, 0.95f));

                _who[i] = Txt(p, "Who", "", new Vector2(0f, 1f), new Vector2(126f, -26f), new Vector2(240f, 26f),
                    15, Color.white, TextAnchor.MiddleLeft);

                // vida: barra gorda + número exacto
                var hpBg = Img(p, "HpBg", new Vector2(0.5f, 1f), new Vector2(0f, -58f), new Vector2(PanelW - 28f, 32f),
                    new Color(0.14f, 0.05f, 0.05f, 1f));
                _hpFill[i] = Img(hpBg.rectTransform, "Fill", new Vector2(0f, 0.5f), new Vector2(2f, 0f),
                    new Vector2(PanelW - 32f, 28f), new Color(0.9f, 0.28f, 0.24f, 1f));
                _hpFill[i].rectTransform.pivot = new Vector2(0f, 0.5f);
                _hpNum[i] = Txt(hpBg.rectTransform, "Hp", "", new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(PanelW - 34f, 28f), 17, Color.white, TextAnchor.MiddleCenter);

                // dorsos = cartas en mano, de un vistazo
                _backs[i] = new Image[MaxBacks];
                for (int k = 0; k < MaxBacks; k++)
                {
                    _backs[i][k] = Img(p, "Back" + k, new Vector2(0f, 1f),
                        new Vector2(24f + k * 26f, -96f), new Vector2(22f, 30f),
                        new Color(0.22f, 0.3f, 0.45f, 1f));
                    Img(_backs[i][k].rectTransform, "In", new Vector2(0.5f, 0.5f), Vector2.zero,
                        new Vector2(16f, 24f), new Color(0.12f, 0.17f, 0.28f, 1f));
                }

                _piles[i] = Txt(p, "Piles", "", new Vector2(1f, 1f), new Vector2(-14f, -96f), new Vector2(230f, 22f),
                    12, new Color(1f, 1f, 1f, 0.82f), TextAnchor.MiddleRight);

                Txt(p, "LeftLbl", "LE QUEDAN (mazo + mano)", new Vector2(0f, 1f), new Vector2(150f, -124f),
                    new Vector2(300f, 16f), 9, new Color(1f, 1f, 1f, 0.42f), TextAnchor.MiddleLeft);
                _left[i] = Txt(p, "Left", "", new Vector2(0.5f, 1f), new Vector2(0f, -166f),
                    new Vector2(PanelW - 26f, 62f), 12, new Color(1f, 1f, 1f, 0.92f), TextAnchor.UpperCenter);

                _kdBadge[i] = Img(p, "Kd", new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(PanelW - 40f, 28f),
                    new Color(0.55f, 0.2f, 0.1f, 0.98f));
                _kdLbl[i] = Txt(_kdBadge[i].rectTransform, "T", "", new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(PanelW - 44f, 26f), 12, new Color(1f, 0.85f, 0.7f), TextAnchor.MiddleCenter);
                _kdBadge[i].gameObject.SetActive(false);
            }

            // el reglamento entero, permanente, arriba al centro
            _turnLbl = Txt(rt, "Turn", "", new Vector2(0.5f, 1f), new Vector2(0f, -196f), new Vector2(700f, 26f),
                15, new Color(1f, 0.95f, 0.7f), TextAnchor.MiddleCenter);
            _rules = Txt(rt, "Rules", "", new Vector2(0.5f, 1f), new Vector2(0f, -226f), new Vector2(860f, 24f),
                12, new Color(1f, 1f, 1f, 0.55f), TextAnchor.MiddleCenter);
            _rules.text = "<color=#ff9550>GOLPE</color> gana a <color=#b86bf2>AGARRE</color> · " +
                          "<color=#b86bf2>AGARRE</color> gana a <color=#5abfff>GUARDIA</color> · " +
                          "<color=#5abfff>GUARDIA</color> gana a <color=#ff9550>GOLPE</color> · " +
                          "golpe vs golpe: gana el más RÁPIDO";
        }

        public void SetVisible(bool on)
        {
            if (_root != null && _root.activeSelf != on) _root.SetActive(on);
            if (!on) HideReveal();
        }

        // ---- refresco por frame ----

        void Update()
        {
            if (_root == null) return;
            if (!SimConfig.DuelEnabled) { SetVisible(false); return; }
            if (S == null || !_root.activeSelf) return;
            for (int i = 0; i < 2; i++) RefreshSide(i);
            _turnLbl.text = $"TURNO {_mc.TurnNumber}";
            if (_revealRoot != null && _revealRoot.activeSelf && !_docked) AnimateReveal();
        }

        void RefreshSide(int i)
        {
            var chr = S.Chr[i];
            int max = S.MaxHpOf(i);
            _who[i].text = (i == 0 ? "VOS · " : "RIVAL · ") + chr.Name;
            _who[i].color = i == 0 ? new Color(0.6f, 0.85f, 1f) : new Color(1f, 0.75f, 0.6f);

            float f = Mathf.Clamp01(S.Hp[i] / (float)max);
            _hpFill[i].rectTransform.sizeDelta = new Vector2((PanelW - 32f) * f, 28f);
            _hpFill[i].color = f > 0.5f ? new Color(0.35f, 0.8f, 0.35f, 1f)
                : f > 0.25f ? new Color(0.95f, 0.75f, 0.2f, 1f)
                : new Color(0.95f, 0.28f, 0.24f, 1f);
            _hpNum[i].text = $"{S.Hp[i]} / {max}";

            int hand = S.Hand[i].Count;
            for (int k = 0; k < MaxBacks; k++)
            {
                bool on = k < hand;
                if (_backs[i][k].gameObject.activeSelf != on) _backs[i][k].gameObject.SetActive(on);
            }
            _piles[i].text = $"mano <b>{hand}</b>  ·  mazo <b>{S.Deck[i].Count}</b>  ·  descarte <b>{S.Discard[i].Count}</b>";
            _left[i].text = RemainingStrip(i);

            bool kd = S.KnockedDown[i];
            bool esc = S.Spent[i].Count > 0;
            bool badge = kd || esc;
            if (_kdBadge[i].gameObject.activeSelf != badge) _kdBadge[i].gameObject.SetActive(badge);
            if (!badge) return;
            _kdBadge[i].color = kd ? new Color(0.6f, 0.22f, 0.1f, 0.98f) : new Color(0.16f, 0.18f, 0.22f, 0.95f);
            _kdLbl[i].text = kd
                ? (i == 0 ? "¡DERRIBADO! tu guardia NO bloquea este turno" : "¡DERRIBADO! su guardia NO bloquea este turno")
                : "escape ya gastado";
            _kdLbl[i].color = kd ? new Color(1f, 0.85f, 0.7f) : new Color(1f, 1f, 1f, 0.5f);
        }

        // El strip de lectura: cuántas copias de cada carta NO están en el
        // descarte. Las guardias van resaltadas porque son LA lectura del
        // juego (sin guardias en mano, atacar es gratis).
        string RemainingStrip(int side)
        {
            var chr = S.Chr[side];
            var used = new int[DuelCatalog.CardsPerChar];
            foreach (int c in S.Discard[side]) used[c]++;
            foreach (int c in S.Spent[side]) used[c]++;
            var sb = new StringBuilder();
            for (int c = 0; c < DuelCatalog.CardsPerChar; c++)
            {
                int total = chr.DeckCounts[c];
                int left = Mathf.Max(0, total - used[c]);
                bool guard = c == DuelCatalog.GuardHigh || c == DuelCatalog.GuardLow;
                string hex = left == 0 ? "#4a4a52" : guard ? "#5abfff" : "#e8e8ee";
                if (left == 0) sb.Append($"<color={hex}>{chr.Cards[c].Short}·0</color>");
                else sb.Append($"<color={hex}>{chr.Cards[c].Short}·{left}</color>");
                if (c < DuelCatalog.CardsPerChar - 1) sb.Append("   ");
            }
            return sb.ToString();
        }

        // ---- revelación de las dos cartas ----

        public void ShowReveal(int card0, int card1, string ruling)
        {
            HideReveal();
            _revealRoot = new GameObject("Reveal", typeof(RectTransform));
            var rt = _revealRoot.GetComponent<RectTransform>();
            rt.SetParent(_root.GetComponent<RectTransform>(), false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            for (int i = 0; i < 2; i++)
            {
                int c = i == 0 ? card0 : card1;
                var holder = Img(rt, "RC" + i, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200f, 276f),
                    new Color(0, 0, 0, 0));
                _revealCard[i] = holder.rectTransform;
                if (c >= 0) DuelHandUI.PaintCard(holder.rectTransform, S.Def(i, c), 200f, 276f);
                else Txt(holder.rectTransform, "None", "SIN CARTAS", new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(190f, 40f), 14, new Color(1f, 0.6f, 0.5f), TextAnchor.MiddleCenter);
                Txt(holder.rectTransform, "Owner", i == 0 ? "VOS" : "RIVAL", new Vector2(0.5f, 1f),
                    new Vector2(0f, 24f), new Vector2(200f, 24f), 13,
                    i == 0 ? new Color(0.6f, 0.85f, 1f) : new Color(1f, 0.75f, 0.6f), TextAnchor.MiddleCenter);
            }
            Txt(rt, "VS", "VS", new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(120f, 60f), 30,
                new Color(1f, 0.9f, 0.5f), TextAnchor.MiddleCenter);
            _ruling = Txt(rt, "Ruling", ruling, new Vector2(0.5f, 0.5f), new Vector2(0f, -190f),
                new Vector2(1200f, 60f), 19, new Color(1f, 0.97f, 0.8f), TextAnchor.MiddleCenter, pixel: false);
            _revealT = 0f;
            _docked = false;
            AnimateReveal();
        }

        // Entrada con ease-out-back: las cartas llegan desde los costados.
        void AnimateReveal()
        {
            _revealT += Time.deltaTime;
            float t = Mathf.Clamp01(_revealT / 0.34f);
            float e = 1f - Mathf.Pow(1f - t, 3f);
            e += Mathf.Sin(t * Mathf.PI) * 0.12f;
            for (int i = 0; i < 2; i++)
            {
                float sign = i == 0 ? -1f : 1f;
                _revealCard[i].anchoredPosition = new Vector2(sign * Mathf.Lerp(900f, 190f, e), 20f);
                _revealCard[i].localScale = Vector3.one * Mathf.Lerp(0.7f, 1.15f, e);
            }
        }

        // Al arrancar la acción, las cartas se achican y se van a los costados
        // para que se vea la pelea pero siga claro QUÉ jugó cada uno.
        public void DockReveal()
        {
            if (_revealRoot == null) return;
            _docked = true;
            for (int i = 0; i < 2; i++)
            {
                float sign = i == 0 ? -1f : 1f;
                _revealCard[i].anchoredPosition = new Vector2(sign * 700f, -30f);
                _revealCard[i].localScale = Vector3.one * 0.72f;
            }
            if (_ruling != null) _ruling.rectTransform.anchoredPosition = new Vector2(0f, -300f);
        }

        public void HideReveal()
        {
            if (_revealRoot != null) Destroy(_revealRoot);
            _revealRoot = null;
            _docked = false;
        }
    }
}
