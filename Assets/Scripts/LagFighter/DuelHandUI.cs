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

        // El abanico vive ENTERO dentro de la pantalla: con BaseY 84 la carta
        // (232 de alto, pivote al medio) se cortaba 32px abajo y se comía los
        // chips de keywords. BaseY = 150 deja aire.
        // La carta creció (172→194) porque toda la escala de texto subió: la
        // regla nueva es que NADA baja de 14px sobre 1920 (DUELO-LOOK §4).
        const float CardW = 194f, CardH = 266f;
        const float BaseY = 158f;
        const float HoverY = 288f;
        const float HoverScale = 1.42f;

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
        bool _dimmed;

        DuelSim S => _mc.Duel;

        // ---- paleta por verbo: el color ES el tipo de carta ----
        // Ojo con la regla de DUELO-LOOK: estos cuatro NO pueden ser celeste ni
        // naranja, porque esos dos son de LADO (vos / rival). Antes GOLPE era
        // naranja y GUARDIA celeste, y en tu propia mano los golpes se veían
        // del rival.
        public static Color VerbColor(in DuelCard d)
        {
            switch (d.Kind)
            {
                case DuelKind.Grab: return Duelo.Agarre;
                case DuelKind.Guard: return Duelo.Guardia;
                case DuelKind.Escape: return Duelo.Escape;
                default: return Duelo.Golpe;
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
            if (d.Kind == DuelKind.Guard) list.Add(("ROBÁS 2 · VUELVE A TU MANO", new Color(0.45f, 0.85f, 1f)));
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

        // Los tres roles tipográficos de DUELO-LOOK §4. Data/Para son
        // condensadas: entran al doble de tamaño y dibujan bien la Ñ.
        public enum Face { Pixel, Data, Para }

        static Font FaceFont(Face f) =>
            f == Face.Pixel ? UIFonts.Pixel : f == Face.Data ? UIFonts.Data : UIFonts.Para;

        static Text MakeText(RectTransform parent, string name, string content, Vector2 anchor, Vector2 pos,
            Vector2 size, int fontSize, Color color, TextAnchor align, Face face = Face.Pixel, bool wrap = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var t = go.GetComponent<Text>();
            t.font = FaceFont(face);
            t.text = content;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = align;
            // Por defecto NADA envuelve: el texto que envuelve es exactamente
            // lo que termina pisando al elemento de abajo.
            t.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            t.verticalOverflow = wrap ? VerticalWrapMode.Overflow : VerticalWrapMode.Truncate;
            t.raycastTarget = false;
            return t;
        }

        // Texto que NO puede desbordar su caja: se achica solo hasta entrar.
        // Es el arreglo de "NUBE ELÉCTRICA" saliéndose de la carta y de los
        // chips de keyword derramados: el layout deja de depender de que el
        // contenido mida lo que esperábamos.
        static Text Fit(Text t, int min, int max, bool wrap = true)
        {
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = min;
            t.resizeTextMaxSize = max;
            t.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            return t;
        }

        // Marco de 2px con brackets de esquina: el cromo de la transmisión.
        // (DUELO-LOOK §6 — bordes rectos, nada de esquinas redondeadas.)
        public static void Brackets(RectTransform parent, float w, float h, Color c, float len = 16f, float th = 2f)
        {
            for (int s = 0; s < 4; s++)
            {
                float sx = (s & 1) == 0 ? -1f : 1f;
                float sy = (s & 2) == 0 ? 1f : -1f;
                var a = new Vector2(sx < 0 ? 0f : 1f, sy > 0 ? 1f : 0f);
                MakeImage(parent, "BrH" + s, a, new Vector2(sx * -len * 0.5f, sy * -th * 0.5f), new Vector2(len, th), c);
                MakeImage(parent, "BrV" + s, a, new Vector2(sx * -th * 0.5f, sy * -len * 0.5f), new Vector2(th, len), c);
            }
        }

        static (Image bg, Text lbl) MakeButton(RectTransform parent, string name, Vector2 pos, Vector2 size, Color c)
        {
            var bg = MakeImage(parent, name, new Vector2(0.5f, 0f), pos, size, c);
            Brackets(bg.rectTransform, size.x, size.y, Duelo.Alpha(Duelo.Paper, 0.5f));
            var lbl = MakeText(bg.rectTransform, "T", "", new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(size.x - 18f, size.y - 10f), 24, Duelo.Paper, TextAnchor.MiddleCenter,
                Face.Data, wrap: true);
            return (bg, lbl);
        }

        // Dibuja UNA carta completa dentro de un rect ya creado. Compartido con
        // la revelación del HUD para que la carta se vea IGUAL en todos lados.
        //
        // Layout en BANDAS de altura fija medidas desde ARRIBA, para que dos
        // elementos nunca puedan pisarse: franja de altura (−6) · verbo (−30) ·
        // nombre (−58) · números (−104, etiquetas −134) · chips (desde abajo).
        public static void PaintCard(RectTransform card, in DuelCard d, float w, float h, bool showKeywords = true)
        {
            var col = VerbColor(d);
            float k = w / 194f;   // todo escala con el ancho de la carta
            MakeImage(card, "Frame", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, h), col);
            MakeImage(card, "Inner", new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(w - 5f, h - 5f), Duelo.Panel);

            // FRANJA DE ALTURA: arriba si el golpe es alto, abajo si es bajo.
            // Redundancia POSICIONAL — se ve de reojo, sin leer una palabra.
            bool hasHeight = d.Height != DuelHeight.None;
            bool high = d.Height == DuelHeight.High;
            if (hasHeight)
                MakeImage(card, "HeightBar", new Vector2(0.5f, high ? 1f : 0f),
                    new Vector2(0f, high ? -11f : 11f), new Vector2(w - 14f, 11f), col);

            // banda del VERBO: fondo SÓLIDO del color de la regla con texto
            // oscuro encima. Es el elemento de mayor contraste de la carta
            // porque el verbo es lo primero que hay que leer.
            var band = MakeImage(card, "Band", new Vector2(0.5f, 1f), new Vector2(0f, -35f),
                new Vector2(w - 14f, 30f), col);
            string verb = (hasHeight ? (high ? "▲ " : "▼ ") : "") + VerbLabel(d);
            Fit(MakeText(band.rectTransform, "V", verb, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(w - 22f, 26f), 18, Duelo.Void, TextAnchor.MiddleCenter, Face.Data), 11, 18, wrap: false);

            // nombre (la letra va aparte, chiquita en la esquina)
            string name = d.Name;
            string letter = "";
            int par = name.IndexOf('(');
            if (par > 0)
            {
                letter = name.Substring(par + 1).TrimEnd(')');
                name = name.Substring(0, par).Trim();
            }
            if (!string.Equals(name, VerbLabel(d), System.StringComparison.OrdinalIgnoreCase))
                // En condensada y no en pixel: "NUBE ELÉCTRICA" salía
                // "NUBE ELéCTRICA" (la pixel dibuja enanas las mayúsculas
                // acentuadas) y encima no entraba. El ancla pixel de la carta
                // son los NÚMEROS, que es donde de verdad hace falta.
                Fit(MakeText(card, "Name", name.ToUpperInvariant(), new Vector2(0.5f, 1f), new Vector2(0f, -70f),
                    new Vector2(w - 18f, 30f), 22, Duelo.Paper, TextAnchor.MiddleCenter, Face.Data), 12, 22);
            if (letter != "")
                MakeText(card, "Letter", letter, new Vector2(0f, 1f), new Vector2(17f, -19f),
                    new Vector2(26f, 20f), 11, Duelo.Alpha(Duelo.Paper, 0.42f), TextAnchor.MiddleCenter);

            // los DOS números que deciden todo, enormes
            if (d.IsAttack)
            {
                MakeText(card, "Spd", d.Speed.ToString(), new Vector2(0f, 1f), new Vector2(w * 0.27f, -122f),
                    new Vector2(w * 0.46f, 50f), Mathf.RoundToInt(40f * k), Duelo.Vel, TextAnchor.MiddleCenter);
                MakeText(card, "SpdL", "VELOCIDAD", new Vector2(0f, 1f), new Vector2(w * 0.27f, -156f),
                    new Vector2(w * 0.5f, 20f), 15, Duelo.Alpha(Duelo.Vel, 0.9f), TextAnchor.MiddleCenter, Face.Data);
                MakeText(card, "Dmg", d.Damage.ToString(), new Vector2(1f, 1f), new Vector2(-w * 0.27f, -122f),
                    new Vector2(w * 0.46f, 50f), Mathf.RoundToInt(40f * k), Duelo.Golpe, TextAnchor.MiddleCenter);
                MakeText(card, "DmgL", "DAÑO", new Vector2(1f, 1f), new Vector2(-w * 0.27f, -156f),
                    new Vector2(w * 0.5f, 20f), 15, Duelo.Alpha(Duelo.Golpe, 0.9f), TextAnchor.MiddleCenter, Face.Data);
            }
            // el PICTOGRAMA de la carta: es el mismo símbolo que el strip
            // "LE QUEDAN" del HUD, así que la carta enseña el idioma sola.
            // En los golpes va apagado, llenando el hueco entre los números y
            // los chips; en guardia y escape ES el protagonista (no tienen
            // números que mostrar) y va grande y a todo color.
            bool hero = !d.IsAttack;
            var wm = new GameObject("Glyph", typeof(RectTransform), typeof(Image));
            var wrt = wm.GetComponent<RectTransform>();
            wrt.SetParent(card, false);
            wrt.anchorMin = wrt.anchorMax = new Vector2(0.5f, hero ? 1f : 0f);
            // en la guardia el pictograma vive ARRIBA o ABAJO de la carta según
            // qué mitad cubre: posición = significado, igual que la franja
            wrt.anchoredPosition = new Vector2(0f,
                hero ? (d.Kind == DuelKind.Guard ? (high ? -112f : -158f) : -132f) : 100f);
            wrt.sizeDelta = hero ? new Vector2(96f, 82f) : new Vector2(56f, 50f);
            var wimg = wm.GetComponent<Image>();
            wimg.sprite = MoveIcons.Get(d);
            wimg.preserveAspect = true;
            wimg.color = hero ? col : Duelo.Alpha(col, 0.3f);
            wimg.raycastTarget = false;

            if (!showKeywords) return;
            // keywords en CHIPS (con fondo): se ven, no se leen de corrido
            var kws = Keywords(d);
            int n = Mathf.Min(kws.Count, 2);
            for (int i = 0; i < n; i++)
            {
                float y = 12f + (n - 1 - i) * 40f;
                var chip = MakeImage(card, "Kw" + i, new Vector2(0.5f, 0f), new Vector2(0f, y + 18f),
                    new Vector2(w - 16f, 36f), Duelo.Wash(kws[i].col));
                // el chip SÍ envuelve y ADEMÁS se achica solo: es una caja
                // cerrada, y el texto largo no puede salirse a pisar la carta
                // vecina del abanico.
                Fit(MakeText(chip.rectTransform, "T", kws[i].txt, new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(w - 24f, 32f), 15, kws[i].col, TextAnchor.MiddleCenter, Face.Data, wrap: true), 9, 15);
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
                        new Vector2(-16f, -19f), new Vector2(24f, 20f), 11,
                        Duelo.Alpha(Duelo.Paper, 0.42f), TextAnchor.MiddleCenter);
                _cardOverlay[i] = MakeImage(card.rectTransform, "Off", new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(CardW, CardH), Duelo.Alpha(Duelo.Void, 0.8f));
                _cardOverlay[i].gameObject.SetActive(false);
            }

            // los dos botones del premio (la única decisión extra del juego)
            (_btnA, _lblA) = MakeButton(rootRt, "BtnA", new Vector2(-244f, 500f), new Vector2(460f, 96f),
                Duelo.Wash(Duelo.Golpe, 0.34f));
            (_btnB, _lblB) = MakeButton(rootRt, "BtnB", new Vector2(244f, 500f), new Vector2(460f, 96f),
                Duelo.Wash(Duelo.Guardia, 0.30f));
            _btnA.gameObject.SetActive(false);
            _btnB.gameObject.SetActive(false);

            // detalle de la carta hovereada
            _infoBg = MakeImage(rootRt, "Info", new Vector2(1f, 0f), new Vector2(-236f, 624f),
                new Vector2(436f, 286f), Duelo.Panel);
            MakeImage(_infoBg.rectTransform, "Line", new Vector2(0.5f, 1f), new Vector2(0f, -1f),
                new Vector2(436f, 2f), Duelo.Line);
            Brackets(_infoBg.rectTransform, 436f, 286f, Duelo.Line);
            _detailTitle = MakeText(_infoBg.rectTransform, "T", "", new Vector2(0.5f, 1f), new Vector2(0f, -30f),
                new Vector2(396f, 32f), 16, Duelo.Paper, TextAnchor.MiddleCenter);
            _detailStats = MakeText(_infoBg.rectTransform, "S", "", new Vector2(0.5f, 1f), new Vector2(0f, -64f),
                new Vector2(400f, 26f), 17, Duelo.Gold, TextAnchor.MiddleCenter, Face.Data);
            _detailDesc = MakeText(_infoBg.rectTransform, "D", "", new Vector2(0.5f, 1f), new Vector2(0f, -172f),
                new Vector2(396f, 180f), 20, Duelo.Alpha(Duelo.Paper, 0.94f), TextAnchor.UpperLeft, Face.Para, wrap: true);
            _infoBg.gameObject.SetActive(false);

            _status = MakeText(rootRt, "Status", "", new Vector2(0.5f, 0f), new Vector2(0f, BaseY + CardH * 0.5f + 34f),
                new Vector2(1600f, 30f), 21, Duelo.Escape, TextAnchor.MiddleCenter, Face.Data);

            RefreshStates();
            LayoutHand();
            _root.SetActive(_active);
        }

        public void Open(Mode mode)
        {
            _active = true;
            _dimmed = false;
            Rebuild(mode);
            ApplyDim();
        }

        public void Close()
        {
            _active = false;
            if (_root != null) _root.SetActive(false);
        }

        // UN FOCO POR FASE (DUELO-LOOK §7): mientras se revela y se resuelve,
        // la mano no desaparece — se APAGA. Desaparecer dejaba un tercio de la
        // pantalla en gris vacío y además cortaba la continuidad ("¿qué tenía
        // yo?"). Apagada sigue ahí, sin competir con el veredicto.
        public void SetDimmed(bool on)
        {
            if (_dimmed == on) return;
            _dimmed = on;
            _active = true;
            if (_root != null) _root.SetActive(true);
            ApplyDim();
        }

        void ApplyDim()
        {
            if (_root == null) return;
            var cg = _root.GetComponent<CanvasGroup>();
            if (cg == null) cg = _root.AddComponent<CanvasGroup>();
            cg.alpha = _dimmed ? 0.34f : 1f;
            cg.blocksRaycasts = !_dimmed;
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
            SetBtn(_btnA, _lblA, prize, "+ DAÑO\n<size=17>quemá un golpe de tu mano y sumá su daño</size>");
            SetBtn(_btnB, _lblB, prize, "DERRIBO\n<size=17>su guardia NO bloquea el próximo turno</size>");
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
                return "¡ESTÁS DERRIBADO! tu guardia no bloquea — el ESCAPE congela el turno";
            if (S != null && S.KnockedDown[1])
                return "¡RIVAL DERRIBADO! su guardia no bloquea: es el turno de pegar";
            return "elegí UNA carta en secreto — el rival elige a la vez";
        }

        // ---- interacción ----

        void Update()
        {
            if (!_active || _dimmed || S == null || _root == null) return;
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
                _btnA.color = Duelo.Wash(Duelo.Golpe, overA ? 0.55f : 0.34f);
                _btnB.color = Duelo.Wash(Duelo.Guardia, overB ? 0.48f : 0.30f);
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
            // el abanico ocupa el ancho: antes vivía apretado en el centro con
            // 1160 de tope y la mano se veía chiquita al lado de dos paneles
            // de 452. Ahora respira hasta 1500.
            float spacing = n <= 1 ? 0f : Mathf.Min(CardW + 14f, 1500f / (n - 1));
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
