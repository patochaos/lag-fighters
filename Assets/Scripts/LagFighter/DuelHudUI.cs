using UnityEngine;
using UnityEngine.UI;

namespace LagFighter
{
    // ---- MODO DUELO: el HUD y la REVELACIÓN (2026-07-25) ----
    //
    // Dos trabajos:
    //
    // 1) Toda la información PÚBLICA de los dos lados, siempre visible y sin
    //    pisarse: vida exacta, mano, mazo, descarte y el strip "LE QUEDAN"
    //    por tipo de carta — la Ley 5 hecha interfaz ("ya gastó sus dos
    //    guardias altas → pegale arriba"). Nada de texto que envuelva: todo
    //    en GRILLA de posiciones fijas, que es lo que evita los solapes.
    //
    // 2) La revelación, que es EL momento del juego: las cartas entran BOCA
    //    ABAJO, hay un respiro, se dan vuelta, y recién ahí se canta el
    //    veredicto — la ganadora crece y brilla, la perdedora se apaga y se
    //    hunde, con el POR QUÉ en grande ("8 > 4 · MÁS RÁPIDO").
    public class DuelHudUI : MonoBehaviour
    {
        MatchController _mc;
        RectTransform _canvasRt;
        GameObject _root;

        // ---- paneles por lado ----
        readonly Text[] _who = new Text[2];
        readonly Text[] _hpNum = new Text[2];
        readonly Image[] _hpFill = new Image[2];
        readonly Text[] _piles = new Text[2];
        readonly Image[][] _leftChip = new Image[2][];
        readonly Image[][] _leftIcon = new Image[2][];
        readonly Text[][] _leftLbl = new Text[2][];
        readonly int[] _charShown = { -1, -1 };   // para repintar iconos al cambiar de personaje
        readonly Image[] _badge = new Image[2];
        readonly Text[] _badgeLbl = new Text[2];
        Text _header, _rules;

        const float PanelW = 476f, PanelH = 300f;
        const float HpW = PanelW - 28f;
        const int Cols = 5;   // el strip "LE QUEDAN" es 5×2 chips

        // ---- revelación ----
        enum Phase { Off, Deal, Suspense, Flip, Judge, Docked }
        Phase _phase = Phase.Off;
        float _t;
        GameObject _revealRoot;
        readonly RectTransform[] _rc = new RectTransform[2];      // contenedor que rota
        readonly GameObject[] _rcFront = new GameObject[2];
        readonly GameObject[] _rcBack = new GameObject[2];
        readonly Image[] _rcGlow = new Image[2];
        readonly Image[] _rcDim = new Image[2];
        Text _vs, _verdict, _detail;
        int _winner = -1;
        string _verdictTxt = "", _detailTxt = "";

        const float DealT = 0.34f, SuspenseT = 0.30f, FlipT = 0.34f, JudgeT = 1.05f;
        const float RevealW = 232f, RevealH = 318f;
        const float SlotX = 258f;
        // dockeadas: pegadas al borde y POR DEBAJO del piso de los paneles
        // (a ±700/y=40 se montaban justo encima del panel de cada lado).
        const float DockX = 828f, DockY = 20f, DockScale = 0.62f;

        public bool RevealFinished => _phase == Phase.Docked || _phase == Phase.Off;

        DuelSim S => _mc.Duel;

        public static DuelHudUI Create(MatchController mc)
        {
            var go = new GameObject("LagFighter.DuelHud");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 22;
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
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        static Text Txt(RectTransform parent, string name, string s, Vector2 anchor, Vector2 pos, Vector2 size,
            int font, Color c, TextAnchor align, DuelHandUI.Face face = DuelHandUI.Face.Pixel)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var t = go.GetComponent<Text>();
            t.font = face == DuelHandUI.Face.Pixel ? UIFonts.Pixel
                : face == DuelHandUI.Face.Data ? UIFonts.Data : UIFonts.Para;
            t.text = s;
            t.fontSize = font;
            t.color = c;
            t.alignment = align;
            // NADA envuelve: el texto que envuelve es lo que se pisa con el de
            // abajo. Si no entra, se recorta — y eso se ve y se arregla.
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.raycastTarget = false;
            return t;
        }

        // ---- construcción ----

        void Build()
        {
            _root = new GameObject("Root", typeof(RectTransform));
            var rt = _root.GetComponent<RectTransform>();
            rt.SetParent(_canvasRt, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            // encabezado: UNA sola línea de turno y UNA de reglas, centradas.
            // Es la CINTA DE LA TRANSMISIÓN: barra opaca a todo el ancho, no un
            // rectángulo translúcido flotando en el medio.
            var hdr = Img(rt, "Hdr", new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(4000f, 68f),
                Duelo.Panel);
            Img(hdr.rectTransform, "Rule", new Vector2(0.5f, 0f), new Vector2(0f, 1f), new Vector2(4000f, 2f), Duelo.Line);
            _header = Txt(hdr.rectTransform, "Turn", "", new Vector2(0.5f, 1f), new Vector2(0f, -19f),
                new Vector2(1020f, 26f), 18, Duelo.Gold, TextAnchor.MiddleCenter);
            _rules = Txt(hdr.rectTransform, "Rules", "", new Vector2(0.5f, 1f), new Vector2(0f, -47f),
                new Vector2(1400f, 26f), 19, Duelo.Mute, TextAnchor.MiddleCenter, DuelHandUI.Face.Data);
            _rules.text = $"<color=#{Hex(Duelo.Golpe)}>GOLPE</color> › <color=#{Hex(Duelo.Agarre)}>AGARRE</color> › " +
                          $"<color=#{Hex(Duelo.Guardia)}>GUARDIA</color> › <color=#{Hex(Duelo.Golpe)}>GOLPE</color>" +
                          "    ·    golpe vs golpe gana el más RÁPIDO    ·    cada golpe es ALTO o BAJO";

            for (int i = 0; i < 2; i++) BuildPanel(rt, i);
        }

        static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

        void BuildPanel(RectTransform rt, int i)
        {
            bool left = i == 0;
            var anchor = new Vector2(left ? 0f : 1f, 1f);
            var panel = Img(rt, "Panel" + i, anchor, new Vector2((left ? 1f : -1f) * 26f, -110f),
                new Vector2(PanelW, PanelH), Duelo.Panel);
            panel.rectTransform.pivot = new Vector2(left ? 0f : 1f, 1f);
            var p = panel.rectTransform;
            var accent = Duelo.Side(i);

            // el color de LADO vive acá y solo acá: barra de acento, nombre,
            // brackets. Las reglas del juego usan su propia familia.
            Img(p, "Accent", new Vector2(0.5f, 1f), new Vector2(0f, -3f), new Vector2(PanelW, 6f), accent);
            DuelHandUI.Brackets(p, PanelW, PanelH, Duelo.Alpha(accent, 0.55f));

            _who[i] = Txt(p, "Who", "", new Vector2(0f, 1f), new Vector2(PanelW * 0.5f, -28f),
                new Vector2(PanelW - 24f, 28f), 17, accent, TextAnchor.MiddleCenter);

            // LA VIDA es el primer ciudadano: barra alta y número grande.
            var hpBg = Img(p, "HpBg", new Vector2(0f, 1f), new Vector2(PanelW * 0.5f, -68f),
                new Vector2(HpW, 40f), Duelo.Wash(Duelo.Golpe, 0.16f));
            _hpFill[i] = Img(hpBg.rectTransform, "Fill", new Vector2(0f, 0.5f), Vector2.zero,
                new Vector2(HpW - 4f, 36f), Duelo.Escape);
            _hpFill[i].rectTransform.pivot = new Vector2(0f, 0.5f);
            _hpFill[i].rectTransform.anchoredPosition = new Vector2(2f, 0f);
            _hpNum[i] = Txt(hpBg.rectTransform, "Hp", "", new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(HpW - 8f, 34f), 22, Duelo.Void, TextAnchor.MiddleCenter);

            _piles[i] = Txt(p, "Piles", "", new Vector2(0f, 1f), new Vector2(PanelW * 0.5f, -106f),
                new Vector2(PanelW - 24f, 24f), 18, Duelo.Mute, TextAnchor.MiddleCenter, DuelHandUI.Face.Data);

            Txt(p, "LeftLbl", "LE QUEDAN", new Vector2(0f, 1f), new Vector2(PanelW * 0.5f, -132f),
                new Vector2(PanelW - 24f, 18f), 14, Duelo.Alpha(Duelo.Mute, 0.75f), TextAnchor.MiddleCenter,
                DuelHandUI.Face.Data);

            // strip en GRILLA fija de 5×2: sin wrap, sin solapes posibles.
            // Cada chip es PICTOGRAMA + número: `A·2` era críptico y encima
            // ilegible a 11px. Es la Ley 5 hecha interfaz, así que tiene que
            // escanearse de un vistazo o no sirve para nada.
            _leftChip[i] = new Image[DuelCatalog.CardsPerChar];
            _leftLbl[i] = new Text[DuelCatalog.CardsPerChar];
            _leftIcon[i] = new Image[DuelCatalog.CardsPerChar];
            float cw = (PanelW - 28f) / Cols, ch = 38f;
            for (int c = 0; c < DuelCatalog.CardsPerChar; c++)
            {
                int col = c % Cols, row = c / Cols;
                float x = 14f + cw * (col + 0.5f);
                float y = -158f - row * (ch + 5f);
                _leftChip[i][c] = Img(p, "Chip" + c, new Vector2(0f, 1f), new Vector2(x, y),
                    new Vector2(cw - 5f, ch), Duelo.Stage);
                var icon = Img(_leftChip[i][c].rectTransform, "I", new Vector2(0f, 0.5f),
                    new Vector2(20f, 0f), new Vector2(24f, 22f), Color.white);
                icon.sprite = MoveIcons.Get(S != null ? S.Chr[i].Cards[c] : default);
                icon.preserveAspect = true;
                _leftIcon[i][c] = icon;
                _leftLbl[i][c] = Txt(_leftChip[i][c].rectTransform, "T", "", new Vector2(1f, 0.5f),
                    new Vector2(-22f, 0f), new Vector2(30f, ch - 6f), 20, Duelo.Paper, TextAnchor.MiddleCenter);
            }

            // el badge vive PEGADO al piso del panel y su texto se achica solo:
            // antes se salía por el borde derecho de la pantalla y se leía
            // "...no bloquea este turn".
            _badge[i] = Img(p, "Badge", new Vector2(0f, 0f), new Vector2(PanelW * 0.5f, 26f),
                new Vector2(PanelW - 24f, 38f), Duelo.Wash(Duelo.Golpe, 0.5f));
            _badgeLbl[i] = Txt(_badge[i].rectTransform, "T", "", new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(PanelW - 34f, 34f), 20, Duelo.Paper, TextAnchor.MiddleCenter, DuelHandUI.Face.Data);
            _badgeLbl[i].resizeTextForBestFit = true;
            _badgeLbl[i].resizeTextMinSize = 11;
            _badgeLbl[i].resizeTextMaxSize = 20;
            _badgeLbl[i].horizontalOverflow = HorizontalWrapMode.Wrap;
            _badge[i].gameObject.SetActive(false);
        }

        public void SetVisible(bool on)
        {
            if (_root != null && _root.activeSelf != on) _root.SetActive(on);
            if (!on) { HideReveal(); HideResults(); }
        }

        // ---- refresco ----

        void Update()
        {
            if (_root == null) return;
            if (!SimConfig.DuelEnabled) { SetVisible(false); return; }
            if (S == null || !_root.activeSelf) return;
            for (int i = 0; i < 2; i++) RefreshSide(i);
            _header.text = $"TURNO {_mc.TurnNumber}";
            if (_phase != Phase.Off && _phase != Phase.Docked) TickReveal();
            if (_resultsRoot != null) TickResults();
        }

        void RefreshSide(int i)
        {
            var chr = S.Chr[i];
            int max = S.MaxHpOf(i);
            _who[i].text = (i == 0 ? "VOS · " : "RIVAL · ") + chr.Name;

            float f = Mathf.Clamp01(S.Hp[i] / (float)max);
            _hpFill[i].rectTransform.sizeDelta = new Vector2((HpW - 4f) * f, 36f);
            _hpFill[i].color = Duelo.Hp(f);
            _hpNum[i].text = $"{S.Hp[i]} / {max}";

            _piles[i].text = $"MANO {S.Hand[i].Count}   ·   MAZO {S.Deck[i].Count}   ·   DESCARTE {S.Discard[i].Count}";

            // los iconos dependen del personaje: se repintan cuando cambia
            if (_charShown[i] != S.CharIdx[i])
            {
                _charShown[i] = S.CharIdx[i];
                for (int c = 0; c < DuelCatalog.CardsPerChar; c++)
                    _leftIcon[i][c].sprite = MoveIcons.Get(chr.Cards[c]);
            }

            var used = new int[DuelCatalog.CardsPerChar];
            foreach (int c in S.Discard[i]) used[c]++;
            foreach (int c in S.Spent[i]) used[c]++;
            for (int c = 0; c < DuelCatalog.CardsPerChar; c++)
            {
                int leftN = Mathf.Max(0, chr.DeckCounts[c] - used[c]);
                bool guard = chr.Cards[c].Kind == DuelKind.Guard;
                var verb = DuelHandUI.VerbColor(chr.Cards[c]);
                _leftLbl[i][c].text = leftN.ToString();

                if (leftN == 0)
                {
                    // agotada: se APAGA. Y si era una guardia, el chip se
                    // prende en dorado — "ya no le queda guardia alta" es LA
                    // lectura del juego y tiene que ser imposible de no ver.
                    _leftChip[i][c].color = guard ? Duelo.Wash(Duelo.Gold, 0.34f) : Duelo.Alpha(Duelo.Void, 0.85f);
                    _leftIcon[i][c].color = guard ? Duelo.Gold : Duelo.Alpha(Duelo.Mute, 0.28f);
                    _leftLbl[i][c].color = guard ? Duelo.Gold : Duelo.Alpha(Duelo.Mute, 0.28f);
                }
                else
                {
                    _leftChip[i][c].color = Duelo.Stage;
                    _leftIcon[i][c].color = verb;
                    _leftLbl[i][c].color = Duelo.Paper;
                }
            }

            bool kd = S.KnockedDown[i], esc = S.Spent[i].Count > 0;
            bool on = kd || esc;
            if (_badge[i].gameObject.activeSelf != on) _badge[i].gameObject.SetActive(on);
            if (!on) return;
            _badge[i].color = kd ? Duelo.Wash(Duelo.Golpe, 0.5f) : Duelo.Stage;
            _badgeLbl[i].text = kd ? "¡DERRIBADO! LA GUARDIA NO BLOQUEA ESTE TURNO" : "escape ya gastado";
            _badgeLbl[i].color = kd ? Duelo.Paper : Duelo.Alpha(Duelo.Mute, 0.7f);
        }

        // ================= REVELACIÓN =================

        public void ShowReveal(DuelTurnResult r)
        {
            HideReveal();
            ComputeVerdict(r);

            _revealRoot = new GameObject("Reveal", typeof(RectTransform));
            var rt = _revealRoot.GetComponent<RectTransform>();
            rt.SetParent(_root.GetComponent<RectTransform>(), false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            for (int i = 0; i < 2; i++)
            {
                int card = r.Card(i);
                var holder = Img(rt, "RC" + i, new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(RevealW, RevealH), new Color(0, 0, 0, 0));
                _rc[i] = holder.rectTransform;

                // resplandor del ganador (detrás de todo)
                _rcGlow[i] = Img(holder.rectTransform, "Glow", new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(RevealW + 28f, RevealH + 28f), Duelo.Alpha(Duelo.Gold, 0f));

                // FRENTE
                var front = new GameObject("Front", typeof(RectTransform));
                var frt = front.GetComponent<RectTransform>();
                frt.SetParent(holder.rectTransform, false);
                frt.anchorMin = frt.anchorMax = new Vector2(0.5f, 0.5f);
                frt.sizeDelta = new Vector2(RevealW, RevealH);
                if (card >= 0) DuelHandUI.PaintCard(frt, S.Def(i, card), RevealW, RevealH);
                else Txt(frt, "None", "SIN CARTAS", new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(RevealW, 40f), 16, Duelo.Golpe, TextAnchor.MiddleCenter);
                _rcFront[i] = front;
                front.SetActive(false);

                // DORSO
                var back = new GameObject("Back", typeof(RectTransform));
                var brt = back.GetComponent<RectTransform>();
                brt.SetParent(holder.rectTransform, false);
                brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
                brt.sizeDelta = new Vector2(RevealW, RevealH);
                PaintBack(brt, i);
                _rcBack[i] = back;

                // velo para apagar a la perdedora
                _rcDim[i] = Img(holder.rectTransform, "Dim", new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(RevealW, RevealH), Duelo.Alpha(Duelo.Void, 0f));

                Txt(holder.rectTransform, "Owner", i == 0 ? "VOS" : "RIVAL", new Vector2(0.5f, 1f),
                    new Vector2(0f, 28f), new Vector2(RevealW, 26f), 15,
                    Duelo.Side(i), TextAnchor.MiddleCenter);
            }

            _vs = Txt(rt, "VS", "VS", new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(160f, 70f), 34,
                Duelo.Gold, TextAnchor.MiddleCenter);
            // el veredicto es la clase de teoría que el juego da solo: grande.
            _verdict = Txt(rt, "Verdict", "", new Vector2(0.5f, 0.5f), new Vector2(0f, -214f),
                new Vector2(1500f, 56f), 40, Duelo.Gold, TextAnchor.MiddleCenter);
            _detail = Txt(rt, "Detail", "", new Vector2(0.5f, 0.5f), new Vector2(0f, -264f),
                new Vector2(1500f, 40f), 25, Duelo.Alpha(Duelo.Paper, 0.9f), TextAnchor.MiddleCenter,
                DuelHandUI.Face.Para);
            _verdict.gameObject.SetActive(false);
            _detail.gameObject.SetActive(false);

            _phase = Phase.Deal;
            _t = 0f;
            TickReveal();
        }

        // Dorso: no hace falta arte, hace falta que se lea "esto está oculto".
        static void PaintBack(RectTransform parent, int side)
        {
            // el dorso es del LADO: acá el celeste/naranja sí significa "de
            // quién es esta carta", que es exactamente su trabajo.
            var acc = Duelo.Side(side);
            Img(parent, "Frame", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(RevealW, RevealH), acc);
            Img(parent, "Bg", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(RevealW - 8f, RevealH - 8f),
                Duelo.Wash(acc, 0.22f));
            for (int k = 0; k < 5; k++)
                Img(parent, "Stripe" + k, new Vector2(0.5f, 0.5f), new Vector2(0f, -96f + k * 48f),
                    new Vector2(RevealW - 44f, 10f), Duelo.Alpha(acc, 0.35f));
            Txt(parent, "Q", "?", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(RevealW, 90f), 56,
                Duelo.Alpha(Duelo.Paper, 0.8f), TextAnchor.MiddleCenter);
        }

        // El POR QUÉ, corto y grande. Es la clase de teoría que enseña sola.
        void ComputeVerdict(DuelTurnResult r)
        {
            _winner = -1;
            var d0 = r.Card0 >= 0 ? S.Def(0, r.Card0) : default;
            var d1 = r.Card1 >= 0 ? S.Def(1, r.Card1) : default;

            if (r.Escaped0 || r.Escaped1)
            {
                _verdictTxt = "ESCAPE";
                _detailTxt = "este turno no pasa nada — la válvula se gasta para siempre";
                return;
            }
            if (r.Armor)
            {
                _verdictTxt = "¡AGUANTE!";
                _detailTxt = "come el golpe y te agarra igual: cobran los dos";
                return;
            }
            if (r.Tech) { _verdictTxt = "TECH"; _detailTxt = "agarre contra agarre: se sueltan"; return; }
            if (r.Trade)
            {
                _verdictTxt = $"MISMA VELOCIDAD  {d0.Speed} = {d1.Speed}";
                _detailTxt = "se pegan los dos y nadie cobra premio";
                return;
            }
            if (r.Guarded0 || r.Guarded1)
            {
                int g = r.Guarded0 ? 0 : 1;
                _winner = g;
                var atk = g == 0 ? d1 : d0;
                // la guardia es femenina: "GUARDIA BAJA", no "GUARDIA BAJO"
                string alt = atk.Height == DuelHeight.High ? "ALTA" : "BAJA";
                _verdictTxt = $"GUARDIA {alt} · ¡ACERTADA!";
                _detailTxt = $"para el golpe entero, roba {r.Drew(g)} cartas y la guardia vuelve a la mano" +
                             (r.Chip(g) > 0 ? $" · igual pega {r.Chip(g)} de chip" : "") +
                             (r.PunishSide == g && r.PunishCard >= 0 ? " · ¡y castiga gratis!" : "");
                return;
            }
            if (r.WrongGuard0 || r.WrongGuard1)
            {
                int g = r.WrongGuard0 ? 0 : 1;
                _winner = 1 - g;
                var atk = g == 0 ? d1 : d0;
                var grd = g == 0 ? d0 : d1;
                string alt = atk.Height == DuelHeight.High ? "ALTO" : "BAJO";
                if (r.GuardWasDown(g))
                {
                    _verdictTxt = "DERRIBADO · LA GUARDIA NO BLOQUEA";
                    _detailTxt = $"el golpe {alt} entra entero";
                }
                else
                {
                    string cub = grd.Height == DuelHeight.High ? "ALTO" : "BAJO";
                    _verdictTxt = "¡ALTURA EQUIVOCADA!";
                    _detailTxt = $"cubrió {cub} y el golpe venía {alt}: entra entero";
                }
                return;
            }
            if (r.Winner >= 0)
            {
                _winner = r.Winner;
                var w = r.Winner == 0 ? d0 : d1;
                var l = r.Winner == 0 ? d1 : d0;
                if (w.Kind == DuelKind.Strike && l.Kind == DuelKind.Grab)
                    _verdictTxt = "GOLPE › AGARRE";
                else if (w.Kind == DuelKind.Grab && l.Kind == DuelKind.Guard)
                    _verdictTxt = "AGARRE › GUARDIA";
                else
                    _verdictTxt = $"MÁS RÁPIDO  {w.Speed} › {l.Speed}";
                string prize = r.Prize == DuelPrize.Damage
                    ? $" · premio +DAÑO: suma {r.PrizeDamage}"
                    : r.Prize == DuelPrize.Knockdown ? " · premio DERRIBO" : "";
                _detailTxt = $"{w.Name.ToUpperInvariant()} conecta por {w.Damage}{prize}";
                return;
            }
            _verdictTxt = "SIN CONTACTO";
            _detailTxt = "nadie conecta";
        }

        void TickReveal()
        {
            _t += Time.deltaTime;
            switch (_phase)
            {
                case Phase.Deal:
                {
                    float t = Mathf.Clamp01(_t / DealT);
                    float e = 1f - Mathf.Pow(1f - t, 3f);
                    for (int i = 0; i < 2; i++)
                    {
                        float sign = i == 0 ? -1f : 1f;
                        _rc[i].anchoredPosition = new Vector2(sign * Mathf.Lerp(1100f, SlotX, e), 10f);
                        _rc[i].localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(sign * 22f, 0f, e));
                        _rc[i].localScale = Vector3.one * Mathf.Lerp(0.65f, 1f, e);
                    }
                    _vs.transform.localScale = Vector3.one * (0.2f + 0.8f * e);
                    if (t >= 1f) { _phase = Phase.Suspense; _t = 0f; SfxLib.Play(SfxLib.Kind.UiTick, 0.7f); }
                    break;
                }
                case Phase.Suspense:
                {
                    // laten un poco: el respiro ANTES de dar vuelta es el truco
                    float pulse = 1f + Mathf.Sin(_t * 14f) * 0.02f;
                    for (int i = 0; i < 2; i++) _rc[i].localScale = Vector3.one * pulse;
                    _vs.transform.localScale = Vector3.one * (1f + Mathf.Sin(_t * 9f) * 0.09f);
                    if (_t >= SuspenseT) { _phase = Phase.Flip; _t = 0f; }
                    break;
                }
                case Phase.Flip:
                {
                    for (int i = 0; i < 2; i++)
                    {
                        float delay = i * 0.09f;                       // escalonado: primero la tuya
                        float t = Mathf.Clamp01((_t - delay) / FlipT);
                        // El giro va por ESCALA en X (1 → 0 → 1), no por
                        // rotación en Y: rotando, la cara pasa los 90° y el
                        // texto se ve ESPEJADO. Con escala el efecto es el
                        // mismo y no hay nada que des-espejar.
                        float sx = Mathf.Abs(Mathf.Cos(t * Mathf.PI));
                        float pop = 1f + Mathf.Sin(t * Mathf.PI) * 0.10f;
                        _rc[i].localScale = new Vector3(Mathf.Max(0.01f, sx) * pop, pop, 1f);
                        bool front = t >= 0.5f;
                        if (_rcFront[i].activeSelf != front)
                        {
                            _rcFront[i].SetActive(front);
                            _rcBack[i].SetActive(!front);
                            if (front) SfxLib.Play(SfxLib.Kind.UiClick, 0.9f);
                        }
                    }
                    if (_t >= FlipT + 0.09f)
                    {
                        _phase = Phase.Judge;
                        _t = 0f;
                        for (int i = 0; i < 2; i++)
                        {
                            _rc[i].localRotation = Quaternion.identity;
                            _rc[i].localScale = Vector3.one;
                        }
                        _verdict.text = _verdictTxt;
                        _detail.text = _detailTxt;
                        _verdict.gameObject.SetActive(true);
                        _detail.gameObject.SetActive(true);
                        _vs.gameObject.SetActive(false);
                        SfxLib.Play(SfxLib.Kind.Hit, 0.55f);
                    }
                    break;
                }
                case Phase.Judge:
                {
                    float t = Mathf.Clamp01(_t / 0.3f);
                    float e = 1f - Mathf.Pow(1f - t, 3f);
                    for (int i = 0; i < 2; i++)
                    {
                        bool win = i == _winner;
                        bool lose = _winner >= 0 && i != _winner;
                        float sign = i == 0 ? -1f : 1f;
                        // la ganadora crece y se acerca al centro; la perdedora
                        // se hunde, se achica y se apaga
                        float toX = win ? sign * (SlotX - 62f) : lose ? sign * (SlotX + 48f) : sign * SlotX;
                        float toY = win ? 26f : lose ? -34f : 10f;
                        float toS = win ? 1.22f : lose ? 0.82f : 1f;
                        _rc[i].anchoredPosition = Vector2.Lerp(new Vector2(sign * SlotX, 10f), new Vector2(toX, toY), e);
                        _rc[i].localScale = Vector3.one * Mathf.Lerp(1f, toS, e);
                        _rc[i].localRotation = Quaternion.Euler(0f, 0f, lose ? Mathf.Lerp(0f, sign * 7f, e) : 0f);
                        var dim = _rcDim[i].color;
                        dim.a = lose ? 0.55f * e : 0f;
                        _rcDim[i].color = dim;
                        var gl = _rcGlow[i].color;
                        gl.a = win ? (0.5f + Mathf.Sin(_t * 10f) * 0.18f) * e : 0f;
                        _rcGlow[i].color = gl;
                    }
                    float vp = Mathf.Clamp01(_t / 0.16f);
                    _verdict.transform.localScale = Vector3.one * Mathf.Lerp(1.5f, 1f, 1f - Mathf.Pow(1f - vp, 3f));
                    if (_t >= JudgeT) { _phase = Phase.Docked; DockNow(); }
                    break;
                }
            }
        }

        // Al terminar, las cartas quedan chiquitas a los costados: sigue claro
        // qué jugó cada uno mientras se resuelve el turno.
        void DockNow()
        {
            if (_revealRoot == null) return;
            // Arriba y a los costados: abajo vive la mano, y el veredicto
            // dockeado ahí abajo se pisaba con el abanico y con el status.
            for (int i = 0; i < 2; i++)
            {
                float sign = i == 0 ? -1f : 1f;
                _rc[i].anchoredPosition = new Vector2(sign * DockX, DockY);
                _rc[i].localScale = Vector3.one * DockScale;
                _rc[i].localRotation = Quaternion.identity;
            }
            if (_verdict != null)
            {
                _verdict.rectTransform.anchoredPosition = new Vector2(0f, 250f);
                _verdict.transform.localScale = Vector3.one * 0.72f;
            }
            if (_detail != null) _detail.rectTransform.anchoredPosition = new Vector2(0f, 206f);
        }

        // El jugador puede apurar la ceremonia (clic o espacio).
        public void SkipReveal()
        {
            if (_phase == Phase.Off || _phase == Phase.Docked) return;
            for (int i = 0; i < 2; i++)
            {
                _rcFront[i].SetActive(true);
                _rcBack[i].SetActive(false);
            }
            _verdict.text = _verdictTxt;
            _detail.text = _detailTxt;
            _verdict.gameObject.SetActive(true);
            _detail.gameObject.SetActive(true);
            if (_vs != null) _vs.gameObject.SetActive(false);
            _phase = Phase.Docked;
            DockNow();
        }

        public void HideReveal()
        {
            if (_revealRoot != null) Destroy(_revealRoot);
            _revealRoot = null;
            _phase = Phase.Off;
            _t = 0f;
        }

        // ================= RESULTADOS =================
        //
        // Hasta ahora DUELO cerraba con un cartel de "K.O." y nada más: quién
        // ganó, y a otra cosa. Acá se ve CÓMO peleaste — que es lo que hace que
        // la próxima partida la juegues distinto — y las dos salidas dejan de
        // ser teclas invisibles.

        GameObject _resultsRoot;
        Image _btnRematch, _btnMenu;

        public bool ResultsVisible => _resultsRoot != null;

        public void ShowResults(int winner, int turns, int[] dmg, int[] guards, int[] kds, bool timeOver = false)
        {
            HideResults();
            var rt = _root.GetComponent<RectTransform>();
            _resultsRoot = new GameObject("Results", typeof(RectTransform));
            var rr = _resultsRoot.GetComponent<RectTransform>();
            rr.SetParent(rt, false);
            rr.anchorMin = Vector2.zero;
            rr.anchorMax = Vector2.one;
            rr.offsetMin = rr.offsetMax = Vector2.zero;
            // canvas propio por ARRIBA de todo: el velo tiene que tapar también
            // los paneles y la mano, que viven en otros canvas. Si no, "apagar
            // el resto" apaga solo el 3D y la pantalla sigue siendo una sopa.
            var rc = _resultsRoot.AddComponent<Canvas>();
            rc.overrideSorting = true;
            rc.sortingOrder = 40;
            _resultsRoot.AddComponent<GraphicRaycaster>();

            // el velo: todo lo demás baja a fondo, esto es lo único que importa
            Img(rr, "Veil", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4000f, 2400f),
                Duelo.Alpha(Duelo.Void, 0.88f));

            var panel = Img(rr, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0f, 20f),
                new Vector2(880f, 560f), Duelo.Panel);
            var p = panel.rectTransform;
            var accent = winner < 0 ? Duelo.Gold : Duelo.Side(winner);
            Img(p, "Accent", new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(880f, 8f), accent);
            DuelHandUI.Brackets(p, 880f, 560f, Duelo.Alpha(accent, 0.7f));

            string big = winner < 0 ? "EMPATE" : winner == 0 ? "¡GANASTE!" : "PERDISTE";
            Txt(p, "Big", big, new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(840f, 60f),
                44, accent, TextAnchor.MiddleCenter);
            Txt(p, "How", timeOver ? "TIME OVER · decidió la vida" : "K.O.",
                new Vector2(0.5f, 1f), new Vector2(0f, -114f), new Vector2(840f, 28f),
                22, Duelo.Mute, TextAnchor.MiddleCenter, DuelHandUI.Face.Data);

            // la tabla: una fila por métrica, las dos columnas enfrentadas
            string[] rows = { "DAÑO HECHO", "GUARDIAS ACERTADAS", "DERRIBOS" };
            int[][] vals = { dmg, guards, kds };
            Txt(p, "ColL", "VOS", new Vector2(0.5f, 1f), new Vector2(-286f, -168f), new Vector2(200f, 26f),
                17, Duelo.P1, TextAnchor.MiddleCenter);
            Txt(p, "ColR", "RIVAL", new Vector2(0.5f, 1f), new Vector2(286f, -168f), new Vector2(200f, 26f),
                17, Duelo.P2, TextAnchor.MiddleCenter);
            for (int i = 0; i < rows.Length; i++)
            {
                float y = -214f - i * 54f;
                Img(p, "Row" + i, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(820f, 44f),
                    i % 2 == 0 ? Duelo.Stage : Duelo.Alpha(Duelo.Stage, 0.45f));
                Txt(p, "Lbl" + i, rows[i], new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(420f, 26f),
                    19, Duelo.Mute, TextAnchor.MiddleCenter, DuelHandUI.Face.Data);
                Txt(p, "V0" + i, vals[i][0].ToString(), new Vector2(0.5f, 1f), new Vector2(-286f, y),
                    new Vector2(160f, 34f), 26, Duelo.Paper, TextAnchor.MiddleCenter);
                Txt(p, "V1" + i, vals[i][1].ToString(), new Vector2(0.5f, 1f), new Vector2(286f, y),
                    new Vector2(160f, 34f), 26, Duelo.Paper, TextAnchor.MiddleCenter);
            }
            Txt(p, "Turns", $"{turns} TURNOS", new Vector2(0.5f, 1f), new Vector2(0f, -388f),
                new Vector2(820f, 26f), 19, Duelo.Alpha(Duelo.Mute, 0.8f), TextAnchor.MiddleCenter,
                DuelHandUI.Face.Data);

            _btnRematch = ResultBtn(p, "Rematch", -212f, "REVANCHA", "R", Duelo.Gold);
            // "SALIR" y no "MENÚ": la pixel font dibuja las mayúsculas
            // acentuadas enanas y quedaba "MENú"
            _btnMenu = ResultBtn(p, "Menu", 212f, "SALIR", "M", Duelo.Mute);
        }

        Image ResultBtn(RectTransform p, string name, float x, string label, string key, Color c)
        {
            var bg = Img(p, name, new Vector2(0.5f, 0f), new Vector2(x, 60f), new Vector2(360f, 78f),
                Duelo.Wash(c, 0.26f));
            DuelHandUI.Brackets(bg.rectTransform, 360f, 78f, Duelo.Alpha(c, 0.8f));
            Txt(bg.rectTransform, "T", label, new Vector2(0.5f, 0.5f), new Vector2(0f, 6f),
                new Vector2(340f, 32f), 22, c, TextAnchor.MiddleCenter);
            Txt(bg.rectTransform, "K", $"[{key}]", new Vector2(0.5f, 0.5f), new Vector2(0f, -22f),
                new Vector2(340f, 22f), 16, Duelo.Alpha(c, 0.55f), TextAnchor.MiddleCenter,
                DuelHandUI.Face.Data);
            return bg;
        }

        public void HideResults()
        {
            if (_resultsRoot != null) Destroy(_resultsRoot);
            _resultsRoot = null;
            _btnRematch = _btnMenu = null;
        }

        void TickResults()
        {
            var mp = GameInput.MousePos();
            bool overR = _btnRematch != null && RectTransformUtility.RectangleContainsScreenPoint(_btnRematch.rectTransform, mp, null);
            bool overM = _btnMenu != null && RectTransformUtility.RectangleContainsScreenPoint(_btnMenu.rectTransform, mp, null);
            if (_btnRematch != null) _btnRematch.color = Duelo.Wash(Duelo.Gold, overR ? 0.46f : 0.26f);
            if (_btnMenu != null) _btnMenu.color = Duelo.Wash(Duelo.Mute, overM ? 0.46f : 0.26f);
            if (!GameInput.ClickPressed()) return;
            if (overR) { SfxLib.Play(SfxLib.Kind.UiClick, 0.9f); _mc.DuelRematch(); }
            else if (overM) { SfxLib.Play(SfxLib.Kind.UiClick, 0.9f); _mc.DuelToMenu(); }
        }
    }
}
