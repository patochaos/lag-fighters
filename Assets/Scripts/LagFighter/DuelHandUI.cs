using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LagFighter
{
    // ---- MODO DUELO: la MANO (2026-07-25) ----
    // Cartas grandes en abanico abajo, hover que agranda y trae al frente
    // (Slay the Spire). La regla de oro de esta pantalla: **la altura y el
    // verbo tienen que leerse sin leer**. Por eso cada carta tiene, además
    // del texto, dos códigos redundantes:
    //   · una BARRA de altura que vive ARRIBA si el golpe es alto y ABAJO si
    //     es bajo (posición = significado, no hace falta traducir),
    //   · una franja de color por verbo (golpe naranja · agarre violeta ·
    //     guardia celeste · escape verde).
    // Las keywords van en CHIPS con fondo, no perdidas en un párrafo.
    // La UI solo LEE DuelSim y le habla a MatchController.
    public class DuelHandUI : MonoBehaviour
    {
        public enum Mode { Pick, Prize, Punish }

        const float CardW = 168f, CardH = 232f;
        const float BaseY = 84f;
        const float HoverY = 196f;
        const float HoverScale = 1.5f;

        MatchController _mc;
        RectTransform _canvasRt;
        GameObject _root;
        RectTransform[] _cardRt = new RectTransform[0];
        Image[] _cardOverlay = new Image[0];
        Image _btnA, _btnB;
        Text _lblA, _lblB;
        Image _infoBg;
        Text _detailTitle, _detailStats, _detailDesc, _status;
        Mode _mode = Mode.Pick;
        int _hover = -1;
        bool _active;

        DuelSim S => _mc.Duel;

        // ---- paleta por verbo: el color ES el tipo de carta ----
        public static Color VerbColor(in DuelCard d)
        {
            switch (d.Kind)
            {
                case DuelKind.Grab: return new Color(0.72f, 0.42f, 0.95f);
                case DuelKind.Guard: return new Color(0.35f, 0.75f, 1f);
                case DuelKind.Escape: return new Color(0.45f, 0.95f, 0.6f);
                default: return new Color(1f, 0.58f, 0.3f);
            }
        }

        public static string VerbLabel(in DuelCard d)
        {
            switch (d.Kind)
            {
                case DuelKind.Grab: return "AGARRE";
                case DuelKind.Guard: return d.Height == DuelHeight.High ? "GUARDIA ALTA" : "GUARDIA BAJA";
                case DuelKind.Escape: return "ESCAPE";
                default: return d.Height == DuelHeight.High ? "GOLPE ALTO" : "GOLPE BAJO";
            }
        }

        // Las keywords, cada una en su chip. Máximo dos por carta (Ley 14).
        static List<(string txt, Color col)> Keywords(in DuelCard d)
        {
            var list = new List<(string, Color)>();
            if (d.Armor) list.Add(("AGUANTE: TE PEGAN Y AGARRÁS IGUAL", new Color(1f, 0.85f, 0.4f)));
            if (d.Chip > 0) list.Add(($"PEGA {d.Chip} AUNQUE LA DEFIENDAN", new Color(1f, 0.75f, 0.25f)));
            if (d.FreeKnockdown) list.Add(("DERRIBO GRATIS", new Color(1f, 0.5f, 0.35f)));
            if (d.PunishOnGuard) list.Add(("SI TE LA DEFIENDEN, TE PEGAN", new Color(1f, 0.4f, 0.45f)));
            if (d.Kind == DuelKind.Guard) list.Add(("ROBÁS 2 Y VUELVE A TU MANO", new Color(0.45f, 0.85f, 1f)));
            if (d.Kind == DuelKind.Grab) list.Add(("LE GANA A LA GUARDIA", new Color(0.85f, 0.6f, 1f)));
            if (d.Kind == DuelKind.Escape) list.Add(("UNA POR PARTIDA", new Color(0.5f, 1f, 0.7f)));
            return list;
        }

        // Qué hace la carta, en castellano y sin jerga.
        static string Explain(in DuelCard d)
        {
            switch (d.Kind)
            {
                case DuelKind.Grab:
                    return d.Armor
                        ? "AGUANTE: si te pegan, comés el golpe pero tu agarre entra IGUAL (cobran los dos). " +
                          "Le gana a la GUARDIA, y contra otro agarre gana el más rápido."
                        : "Le gana a la GUARDIA (cualquier altura) y pierde con cualquier golpe. " +
                          "Contra otro agarre gana el más rápido; si empatan, se sueltan.";
                case DuelKind.Guard:
                    return d.Height == DuelHeight.High
                        ? "Para los golpes ALTOS: no comés nada, robás 2 cartas y la guardia vuelve a tu mano. " +
                          "Los golpes BAJOS te entran enteros, y el agarre te rompe la guardia."
                        : "Para los golpes BAJOS: no comés nada, robás 2 cartas y la guardia vuelve a tu mano. " +
                          "Los golpes ALTOS te entran enteros, y el agarre te rompe la guardia.";
                case DuelKind.Escape:
                    return "No pasa nada este turno: ni pegás ni te pegan. Es la salida cuando estás " +
                           "derribado y tu guardia no funciona. Se gasta para siempre.";
                default:
                    return $"Golpe {(d.Height == DuelHeight.High ? "ALTO" : "BAJO")}: le gana al AGARRE, " +
                           $"y a la guardia que defienda {(d.Height == DuelHeight.High ? "abajo" : "arriba")}. " +
                           "Contra otro golpe gana el más rápido; si empatan, se pegan los dos.";
            }
        }

        public static DuelHandUI Create(MatchController mc)
        {
            var go = new GameObject("LagFighter.DuelHand");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 21;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var ui = go.AddComponent<DuelHandUI>();
            ui._mc = mc;
            ui._canvasRt = go.GetComponent<RectTransform>();
            return ui;
        }

        // ---- helpers de construcción ----

        static Image MakeImage(RectTransform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
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

        static Text MakeText(RectTransform parent, string name, string content, Vector2 anchor, Vector2 pos,
            Vector2 size, int fontSize, Color color, TextAnchor align, bool pixel = true)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var t = go.GetComponent<Text>();
            t.font = pixel ? UIFonts.Pixel : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = content;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        static (Image bg, Text lbl) MakeButton(RectTransform parent, string name, Vector2 pos, Vector2 size, Color c)
        {
            var bg = MakeImage(parent, name, new Vector2(0.5f, 0f), pos, size, c);
            var lbl = MakeText(bg.rectTransform, "T", "", new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(size.x - 12f, size.y - 8f), 13, Color.white, TextAnchor.MiddleCenter);
            lbl.fontStyle = FontStyle.Bold;
            return (bg, lbl);
        }

        // Dibuja UNA carta completa dentro de un rect ya creado. Compartido con
        // la revelación del HUD para que la carta se vea IGUAL en todos lados.
        public static void PaintCard(RectTransform card, in DuelCard d, float w, float h, bool showKeywords = true)
        {
            var col = VerbColor(d);
            MakeImage(card, "Frame", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, h),
                new Color(col.r, col.g, col.b, 0.85f));
            MakeImage(card, "Inner", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w - 6f, h - 6f),
                new Color(0.07f, 0.08f, 0.11f, 1f));

            // BARRA DE ALTURA: arriba = alto, abajo = bajo. Redundancia
            // posicional para que la altura se lea sin leer.
            if (d.Height != DuelHeight.None)
            {
                bool high = d.Height == DuelHeight.High;
                var bar = MakeImage(card, "HeightBar", new Vector2(0.5f, high ? 1f : 0f),
                    new Vector2(0f, high ? -5f : 5f), new Vector2(w - 12f, 9f),
                    new Color(col.r, col.g, col.b, 0.95f));
                MakeText(bar.rectTransform, "HL", high ? "▲ ALTO" : "▼ BAJO", new Vector2(0.5f, 0.5f),
                    new Vector2(0f, high ? -13f : 13f), new Vector2(w, 14f), 9,
                    new Color(col.r, col.g, col.b, 1f), TextAnchor.MiddleCenter);
            }

            // banda del VERBO
            var band = MakeImage(card, "Band", new Vector2(0.5f, 1f), new Vector2(0f, -34f),
                new Vector2(w - 14f, 26f), new Color(col.r * 0.42f, col.g * 0.42f, col.b * 0.42f, 1f));
            MakeText(band.rectTransform, "V", VerbLabel(d), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(w - 18f, 24f), 11, Color.white, TextAnchor.MiddleCenter);

            // nombre (sin la letra entre paréntesis: va aparte, grande)
            string name = d.Name;
            string letter = "";
            int par = name.IndexOf('(');
            if (par > 0)
            {
                letter = name.Substring(par + 1).TrimEnd(')');
                name = name.Substring(0, par).Trim();
            }
            MakeText(card, "Name", name.ToUpperInvariant(), new Vector2(0.5f, 1f), new Vector2(0f, -62f),
                new Vector2(w - 16f, 30f), 13, Color.white, TextAnchor.MiddleCenter);
            if (letter != "")
                MakeText(card, "Letter", letter, new Vector2(0f, 1f), new Vector2(17f, -16f),
                    new Vector2(26f, 20f), 12, new Color(1f, 1f, 1f, 0.45f), TextAnchor.MiddleCenter);

            // los DOS números que deciden todo, enormes
            if (d.IsAttack)
            {
                MakeText(card, "Spd", d.Speed.ToString(), new Vector2(0f, 0.5f), new Vector2(w * 0.27f, 4f),
                    new Vector2(w * 0.46f, 42f), 30, new Color(0.5f, 0.85f, 1f), TextAnchor.MiddleCenter);
                MakeText(card, "SpdL", "VELOCIDAD", new Vector2(0f, 0.5f), new Vector2(w * 0.27f, -22f),
                    new Vector2(w * 0.5f, 14f), 8, new Color(0.5f, 0.85f, 1f, 0.8f), TextAnchor.MiddleCenter);
                MakeText(card, "Dmg", d.Damage.ToString(), new Vector2(1f, 0.5f), new Vector2(-w * 0.27f, 4f),
                    new Vector2(w * 0.46f, 42f), 30, new Color(1f, 0.5f, 0.42f), TextAnchor.MiddleCenter);
                MakeText(card, "DmgL", "DAÑO", new Vector2(1f, 0.5f), new Vector2(-w * 0.27f, -22f),
                    new Vector2(w * 0.5f, 14f), 8, new Color(1f, 0.5f, 0.42f, 0.8f), TextAnchor.MiddleCenter);
            }
            else
            {
                string big = d.Kind == DuelKind.Guard ? (d.Height == DuelHeight.High ? "▲" : "▼") : "✦";
                MakeText(card, "Big", big, new Vector2(0.5f, 0.5f), new Vector2(0f, 6f),
                    new Vector2(w - 16f, 46f), 34, col, TextAnchor.MiddleCenter);
            }

            if (!showKeywords) return;
            // keywords en CHIPS (con fondo): se ven, no se leen de corrido
            var kws = Keywords(d);
            for (int k = 0; k < kws.Count && k < 2; k++)
            {
                float y = 34f + (kws.Count - 1 - k) * 26f;
                var chip = MakeImage(card, "Kw" + k, new Vector2(0.5f, 0f), new Vector2(0f, y),
                    new Vector2(w - 18f, 23f), new Color(kws[k].col.r * 0.24f, kws[k].col.g * 0.24f, kws[k].col.b * 0.24f, 1f));
                MakeText(chip.rectTransform, "T", kws[k].txt, new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(w - 22f, 21f), 8, kws[k].col, TextAnchor.MiddleCenter);
            }
        }

        // ---- construcción de la pantalla ----

        public void Rebuild(Mode mode)
        {
            _mode = mode;
            _hover = -1;
            if (_root != null) Destroy(_root);
            _root = new GameObject("Root", typeof(RectTransform));
            var rootRt = _root.GetComponent<RectTransform>();
            rootRt.SetParent(_canvasRt, false);
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = rootRt.offsetMax = Vector2.zero;

            var hand = S != null ? S.Hand[0] : new List<int>();
            int n = hand.Count;
            _cardRt = new RectTransform[n];
            _cardOverlay = new Image[n];

            for (int i = 0; i < n; i++)
            {
                var d = S.Def(0, hand[i]);
                var card = MakeImage(rootRt, "Card" + i, new Vector2(0.5f, 0f), Vector2.zero,
                    new Vector2(CardW, CardH), new Color(0, 0, 0, 0));
                _cardRt[i] = card.rectTransform;
                PaintCard(card.rectTransform, d, CardW, CardH);
                if (i < 9)
                    MakeText(card.rectTransform, "Key", (i + 1).ToString(), new Vector2(1f, 1f),
                        new Vector2(-15f, -16f), new Vector2(22f, 18f), 10,
                        new Color(1f, 1f, 1f, 0.35f), TextAnchor.MiddleCenter);
                _cardOverlay[i] = MakeImage(card.rectTransform, "Off", new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(CardW, CardH), new Color(0.02f, 0.02f, 0.03f, 0.78f));
                _cardOverlay[i].gameObject.SetActive(false);
            }

            // los dos botones del premio (la única decisión extra del juego)
            (_btnA, _lblA) = MakeButton(rootRt, "BtnA", new Vector2(-215f, 372f), new Vector2(400f, 84f),
                new Color(0.5f, 0.2f, 0.16f, 0.97f));
            (_btnB, _lblB) = MakeButton(rootRt, "BtnB", new Vector2(215f, 372f), new Vector2(400f, 84f),
                new Color(0.18f, 0.34f, 0.55f, 0.97f));
            _btnA.gameObject.SetActive(false);
            _btnB.gameObject.SetActive(false);

            // detalle de la carta hovereada
            _infoBg = MakeImage(rootRt, "Info", new Vector2(1f, 0f), new Vector2(-215f, 470f),
                new Vector2(400f, 250f), new Color(0.03f, 0.04f, 0.06f, 0.97f));
            _detailTitle = MakeText(_infoBg.rectTransform, "T", "", new Vector2(0.5f, 1f), new Vector2(0f, -26f),
                new Vector2(368f, 30f), 15, Color.white, TextAnchor.MiddleCenter);
            _detailStats = MakeText(_infoBg.rectTransform, "S", "", new Vector2(0.5f, 1f), new Vector2(0f, -58f),
                new Vector2(368f, 24f), 11, new Color(0.95f, 0.9f, 0.6f), TextAnchor.MiddleCenter);
            _detailDesc = MakeText(_infoBg.rectTransform, "D", "", new Vector2(0.5f, 1f), new Vector2(0f, -150f),
                new Vector2(364f, 160f), 16, new Color(1f, 1f, 1f, 0.93f), TextAnchor.UpperLeft, pixel: false);
            _infoBg.gameObject.SetActive(false);

            _status = MakeText(rootRt, "Status", "", new Vector2(0.5f, 0f), new Vector2(0f, BaseY + CardH + 26f),
                new Vector2(1500f, 26f), 14, new Color(0.55f, 1f, 0.65f), TextAnchor.MiddleCenter);

            RefreshStates();
            LayoutHand();
            _root.SetActive(_active);
        }

        public void Open(Mode mode)
        {
            _active = true;
            Rebuild(mode);
        }

        public void Close()
        {
            _active = false;
            if (_root != null) _root.SetActive(false);
        }

        // ---- estados ----

        bool CardEnabled(int i)
        {
            if (S == null || i >= S.Hand[0].Count) return false;
            switch (_mode)
            {
                case Mode.Pick: return true;   // en DUELO TODA carta es jugable siempre
                case Mode.Prize: return S.PrizeFuel(0).Contains(i);
                case Mode.Punish: return S.Def(0, S.Hand[0][i]).IsAttack;
            }
            return false;
        }

        void RefreshStates()
        {
            if (S == null) return;
            for (int i = 0; i < _cardOverlay.Length; i++)
                _cardOverlay[i].gameObject.SetActive(!CardEnabled(i));

            bool prize = _mode == Mode.Prize;
            SetBtn(_btnA, _lblA, prize, "+ DAÑO\n<size=11>quemá un golpe de tu mano y sumá su daño</size>");
            SetBtn(_btnB, _lblB, prize, "DERRIBO\n<size=11>su guardia NO bloquea el próximo turno</size>");
            _status.text = StatusText();
        }

        static void SetBtn(Image btn, Text lbl, bool on, string txt)
        {
            if (btn == null) return;
            if (btn.gameObject.activeSelf != on) btn.gameObject.SetActive(on);
            if (on) lbl.text = txt;
        }

        string StatusText()
        {
            switch (_mode)
            {
                case Mode.Prize:
                    return S.PrizeFuel(0).Count > 0
                        ? "¡GANASTE EL INTERCAMBIO! elegí tu premio — click en un golpe de la mano para +DAÑO"
                        : "¡GANASTE! sin golpes para quemar: te queda el DERRIBO";
                case Mode.Punish:
                    return "¡SE LA DEFENDISTE! pegale gratis: elegí un golpe o agarre (ESPACIO para no castigar)";
            }
            if (S != null && S.KnockedDown[0])
                return "¡ESTÁS DERRIBADO! tu GUARDIA no bloquea este turno — el ESCAPE congela el turno";
            if (S != null && S.KnockedDown[1])
                return "¡RIVAL DERRIBADO! su guardia no bloquea: es el turno de pegar";
            return "elegí UNA carta en secreto — el rival elige a la vez";
        }

        // ---- interacción ----

        void Update()
        {
            if (!_active || S == null || _root == null) return;
            var mp = GameInput.MousePos();

            int hover = -1;
            for (int i = _cardRt.Length - 1; i >= 0; i--)
                if (RectTransformUtility.RectangleContainsScreenPoint(_cardRt[i], mp, null)) { hover = i; break; }
            if (hover != _hover)
            {
                _hover = hover;
                if (hover >= 0) { SfxLib.Play(SfxLib.Kind.UiTick, 0.25f); FillDetail(hover); }
                _infoBg.gameObject.SetActive(hover >= 0);
                LayoutHand();
            }

            bool overA = _btnA.gameObject.activeSelf && RectTransformUtility.RectangleContainsScreenPoint(_btnA.rectTransform, mp, null);
            bool overB = _btnB.gameObject.activeSelf && RectTransformUtility.RectangleContainsScreenPoint(_btnB.rectTransform, mp, null);
            if (_btnA.gameObject.activeSelf)
            {
                _btnA.color = overA ? new Color(0.68f, 0.28f, 0.22f, 1f) : new Color(0.5f, 0.2f, 0.16f, 0.97f);
                _btnB.color = overB ? new Color(0.26f, 0.48f, 0.72f, 1f) : new Color(0.18f, 0.34f, 0.55f, 0.97f);
            }

            if (GameInput.ClickPressed())
            {
                if (hover >= 0) { ClickCard(hover); return; }
                if (overA) { SfxLib.Play(SfxLib.Kind.UiCancel, 0.5f); _status.text = "elegí CUÁL golpe quemás: click en una carta de tu mano"; return; }
                if (overB) { SfxLib.Play(SfxLib.Kind.UiClick, 0.9f); _mc.DuelChoosePrize(DuelPrize.Knockdown, -1); return; }
            }

            int num = GameInput.NumberPressed();
            if (num > 0 && num <= _cardRt.Length) ClickCard(num - 1);
            if (GameInput.EndTurnPressed())
            {
                if (_mode == Mode.Punish) { _mc.DuelPunish(-1); return; }
                if (_mode == Mode.Prize) { _mc.DuelChoosePrize(DuelPrize.Knockdown, -1); return; }
            }
        }

        void ClickCard(int i)
        {
            if (!CardEnabled(i)) { SfxLib.Play(SfxLib.Kind.UiCancel, 0.4f); return; }
            SfxLib.Play(SfxLib.Kind.UiClick, 0.8f);
            switch (_mode)
            {
                case Mode.Pick: _mc.DuelPick(i); return;
                case Mode.Prize: _mc.DuelChoosePrize(DuelPrize.Damage, i); return;
                case Mode.Punish: _mc.DuelPunish(i); return;
            }
        }

        // ---- layout / detalle ----

        void LayoutHand()
        {
            int n = _cardRt.Length;
            float spacing = n <= 1 ? 0f : Mathf.Min(CardW + 10f, 1120f / (n - 1));
            float x0 = -spacing * (n - 1) * 0.5f;
            for (int i = 0; i < n; i++)
            {
                bool hov = i == _hover;
                _cardRt[i].anchoredPosition = new Vector2(x0 + i * spacing, hov ? HoverY : BaseY);
                _cardRt[i].localScale = Vector3.one * (hov ? HoverScale : 1f);
                _cardRt[i].SetSiblingIndex(i);
            }
            if (_hover >= 0) _cardRt[_hover].SetAsLastSibling();
        }

        void FillDetail(int i)
        {
            var hand = S.Hand[0];
            if (i >= hand.Count) return;
            var d = S.Def(0, hand[i]);
            var col = VerbColor(d);
            _detailTitle.text = d.Name.ToUpperInvariant();
            _detailTitle.color = col;
            _detailStats.text = d.IsAttack
                ? $"{VerbLabel(d)}  ·  VELOCIDAD {d.Speed}  ·  DAÑO {d.Damage}"
                : VerbLabel(d);
            _detailDesc.text = Explain(d);
        }
    }
}
