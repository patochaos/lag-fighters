using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LagFighter
{
    // ---- MODO CARTAS v2: la MANO estilo Slay the Spire (2026-07-22) ----
    // Cartas grandes solapadas en abanico abajo de la pantalla, bien legibles
    // (sin arte: tipografía y números); el hover agranda la carta, la trae al
    // frente y llena el panel de detalle. Todos los modos de interacción del
    // turno viven acá: opener · combo (elegir cómo seguir + pump + terminar)
    // · castigo · cambio (exchange) · poder (power up con par).
    // La UI solo LEE CardSim y le habla a MatchController.
    public class CardHandUI : MonoBehaviour
    {
        public enum Mode { Opener, Combo, Punish, ExchangeGive, PowerA, PowerB }

        const float CardW = 150f, CardH = 205f;
        const float BaseY = 78f;      // el abanico asoma desde abajo (se ve "parcial")
        const float HoverY = 175f;
        const float HoverScale = 1.5f;

        MatchController _mc;
        RectTransform _canvasRt;
        GameObject _root;
        // una entrada por carta de la MANO (posición = índice de mano)
        RectTransform[] _cardRt = new RectTransform[0];
        Image[] _cardBg, _cardBand, _cardOverlay;
        int[] _baseOrder = new int[0];
        // botones contextuales
        Image _leftBtn1, _leftBtn2, _rightBtn1, _rightBtn2;
        Text _leftLbl1, _leftLbl2, _rightLbl1, _rightLbl2;
        // picker modal (descarte del cambio / beneficio del poder)
        Image _pickPanel;
        readonly Image[] _pickBtn = new Image[10];
        readonly Text[] _pickLbl = new Text[10];
        readonly int[] _pickVal = new int[10];
        int _pickCount;
        bool _pickIsPower; // false: descarte del exchange · true: beneficio del power up
        // panel de detalle a la derecha
        Text _detailTitle, _detailStats, _detailTag, _detail, _status;
        Mode _mode = Mode.Opener;
        int _exGive = -1;   // ExchangeGive: carta elegida para soltar
        int _powerA = -1;   // PowerA→B: primera carta del par
        int _hover = -1;
        bool _active;

        static readonly Color BtnGreen = new Color(0.18f, 0.45f, 0.22f, 0.95f);
        static readonly Color BtnBlue = new Color(0.16f, 0.3f, 0.5f, 0.95f);
        static readonly Color BtnGold = new Color(0.5f, 0.38f, 0.08f, 0.95f);
        static readonly Color BtnRed = new Color(0.4f, 0.2f, 0.2f, 0.95f);

        CardSim S => _mc.Cards;

        public static CardHandUI Create(MatchController mc)
        {
            var go = new GameObject("LagFighter.CardHand");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 21;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var ui = go.AddComponent<CardHandUI>();
            ui._mc = mc;
            ui._canvasRt = go.GetComponent<RectTransform>();
            return ui;
        }

        // ---- textos por carta ----

        public static string KindLabel(in CardDef d)
        {
            if (d.Kind == CardKind.Ability) return "HABILIDAD";
            if (d.IsSuper && d.Kind == CardKind.Dodge) return "SUPER ESQUIVE";
            if (d.IsSuper) return d.Projectile ? $"SUPER · PROYECTIL NV.{d.ProjLevel}" : "SUPER GOLPE";
            switch (d.Kind)
            {
                case CardKind.Throw: return "AGARRE";
                case CardKind.Block: return d.BlocksLow ? "BLOQUEO BAJO+MID" : "BLOQUEO ALTO+MID";
                case CardKind.Dodge: return "ESQUIVE";
            }
            if (d.Projectile) return $"PROYECTIL NV.{d.ProjLevel}";
            return d.Height == CardHeight.High ? "GOLPE ALTO"
                 : d.Height == CardHeight.Low ? "GOLPE BAJO" : "GOLPE MID";
        }

        static string Props(in CardDef d)
        {
            var p = new List<string>();
            if (d.IsSuper) p.Add($"cuesta {new string('★', d.SuperCost)}");
            if (d.Combo == ComboType.Chain) p.Add("CHAIN");
            else if (d.Combo == ComboType.Starter) p.Add("STARTER");
            else if (d.Combo == ComboType.Linker) p.Add("LINKER");
            else if (d.Combo == ComboType.Ender) p.Add("ENDER");
            else if (d.Combo == ComboType.CantCombo) p.Add("SIN COMBO");
            if (d.ComboPoints > 0) p.Add($"{d.ComboPoints} CP");
            if (d.KnockdownOnHit) p.Add("DERRIBA");
            if (d.UnsafeOnBlock) p.Add("UNSAFE");
            if (d.Recurring) p.Add("VUELVE");
            if (d.Lockdown) p.Add("SIN ROBO");
            if (d.BlockDamage > 0) p.Add($"chip {d.BlockDamage}");
            if (d.Pump != PumpFuel.None) p.Add($"pump +{d.PumpDamage}");
            if (d.SelfDamage > 0) p.Add($"−{d.SelfDamage} propio");
            if (d.DodgeCounter > 0) p.Add($"devuelve {d.DodgeCounter}");
            return string.Join(" · ", p);
        }

        string DetailDesc(int card, in CardDef d)
        {
            switch (card)
            {
                case CardCatalog.AttackA: return "El poke: lo más rápido de la mano. Encadena a B.";
                case CardCatalog.AttackB: return "Un paso más de la cadena baja. Encadena a C.";
                case CardCatalog.AttackC: return "El medio: cualquier bloqueo lo para, pero encadena a D.";
                case CardCatalog.AttackD: return "Pesado y ALTO: castiga al que bloquea bajo. Encadena a E.";
                case CardCatalog.AttackE: return "El más lento y el que más pega. Cierra las cadenas.";
                case CardCatalog.Throw: return "Gana a bloqueos y esquives. Derriba SI NO seguís de combo.";
                case CardCatalog.Dodge: return "Evita golpes; a un strike le devolvés UNA carta de golpe/agarre.";
                case CardCatalog.LowBlock: return "Para bajos y mids, roba 1 y vuelve a la mano si no te pegan.";
                case CardCatalog.HighBlock: return "Para altos y mids, roba 1 y vuelve. El agarre lo rompe.";
                case CardCatalog.Ability: return "Se juega en TU main phase (no es un opener). " + S.Chr[0].AbilityText;
            }
            return Props(d);
        }

        // ---- construcción ----

        Image MakeImage(RectTransform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
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

        Text MakeText(RectTransform parent, string name, string content, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize, Color color, TextAnchor align, bool pixel = true)
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

        (Image bg, Text lbl) MakeButton(RectTransform parent, string name, Vector2 pos, Vector2 size, Color c)
        {
            var bg = MakeImage(parent, name, new Vector2(0.5f, 0f), pos, size, c);
            var lbl = MakeText(bg.rectTransform, "T", "", new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(size.x - 8f, size.y - 6f), 12, Color.white, TextAnchor.MiddleCenter);
            lbl.fontStyle = FontStyle.Bold;
            return (bg, lbl);
        }

        // Reconstruye TODO desde la mano actual. Se llama en cada cambio.
        public void Rebuild(Mode mode)
        {
            _mode = mode;
            if (mode != Mode.ExchangeGive) _exGive = -1;
            if (mode != Mode.PowerA && mode != Mode.PowerB) _powerA = -1;
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
            _cardBg = new Image[n];
            _cardBand = new Image[n];
            _cardOverlay = new Image[n];
            _baseOrder = new int[n];

            float spacing = n <= 1 ? 0f : Mathf.Min(CardW + 8f, 1080f / (n - 1));
            float x0 = -spacing * (n - 1) * 0.5f;
            for (int i = 0; i < n; i++)
            {
                var d = S.Def(0, hand[i]);
                var col = HudUI.CardDefColor(d, hand[i]);
                var card = MakeImage(rootRt, "Card" + i, new Vector2(0.5f, 0f),
                    new Vector2(x0 + i * spacing, BaseY), new Vector2(CardW, CardH),
                    new Color(0.09f, 0.1f, 0.13f, 0.99f));
                _cardRt[i] = card.rectTransform;
                _cardBg[i] = card;
                _baseOrder[i] = i;

                // marco fino del color de la carta
                var frame = MakeImage(card.rectTransform, "Frame", new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(CardW, CardH), new Color(col.r, col.g, col.b, 0.55f));
                frame.transform.SetAsFirstSibling();
                MakeImage(card.rectTransform, "Inner", new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(CardW - 5f, CardH - 5f), new Color(0.09f, 0.1f, 0.13f, 1f)).transform.SetSiblingIndex(1);

                // banda superior con el color + nombre
                _cardBand[i] = MakeImage(card.rectTransform, "Band", new Vector2(0.5f, 1f),
                    new Vector2(0f, -21f), new Vector2(CardW - 8f, 36f), new Color(col.r * 0.55f, col.g * 0.55f, col.b * 0.55f, 0.95f));
                string shortName = d.Name;
                int par = shortName.IndexOf('(');
                if (par > 0) shortName = shortName.Substring(0, par).Trim();
                MakeText(_cardBand[i].rectTransform, "Name", shortName.ToUpperInvariant(), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(CardW - 14f, 34f), 9, Color.white, TextAnchor.MiddleCenter);

                // tipo (GOLPE ALTO / AGARRE / …)
                MakeText(card.rectTransform, "Kind", KindLabel(d), new Vector2(0.5f, 1f), new Vector2(0f, -50f),
                    new Vector2(CardW - 12f, 16f), 8,
                    new Color(col.r * 0.5f + 0.5f, col.g * 0.5f + 0.5f, col.b * 0.5f + 0.5f), TextAnchor.MiddleCenter);

                // los números GRANDES: speed y daño (o el rol defensivo)
                if (d.Kind == CardKind.Attack || d.Kind == CardKind.Throw)
                {
                    MakeText(card.rectTransform, "Spd", $"S{d.Speed}", new Vector2(0f, 0.5f), new Vector2(38f, 6f),
                        new Vector2(70f, 30f), 18, new Color(0.55f, 0.9f, 1f), TextAnchor.MiddleCenter);
                    MakeText(card.rectTransform, "SpdL", "speed", new Vector2(0f, 0.5f), new Vector2(38f, -16f),
                        new Vector2(70f, 14f), 7, new Color(0.55f, 0.9f, 1f, 0.7f), TextAnchor.MiddleCenter);
                    MakeText(card.rectTransform, "Dmg", d.Damage.ToString(), new Vector2(1f, 0.5f), new Vector2(-38f, 6f),
                        new Vector2(70f, 30f), 18, new Color(1f, 0.55f, 0.45f), TextAnchor.MiddleCenter);
                    MakeText(card.rectTransform, "DmgL", "daño", new Vector2(1f, 0.5f), new Vector2(-38f, -16f),
                        new Vector2(70f, 14f), 7, new Color(1f, 0.55f, 0.45f, 0.7f), TextAnchor.MiddleCenter);
                }
                else
                {
                    string big = d.Kind == CardKind.Block ? (d.BlocksLow ? "BAJO" : "ALTO")
                        : d.Kind == CardKind.Dodge ? "ESQ" : "HAB";
                    MakeText(card.rectTransform, "Big", big, new Vector2(0.5f, 0.5f), new Vector2(0f, 2f),
                        new Vector2(CardW - 16f, 34f), 20, new Color(col.r * 0.5f + 0.5f, col.g * 0.5f + 0.5f, col.b * 0.5f + 0.5f), TextAnchor.MiddleCenter);
                }

                // super: las estrellas del costo, bien visibles
                if (d.IsSuper)
                    MakeText(card.rectTransform, "Stars", new string('★', d.SuperCost), new Vector2(0.5f, 0.5f),
                        new Vector2(0f, 34f), new Vector2(CardW, 20f), 13, new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleCenter);

                // propiedades abajo (legibles al agrandarse)
                MakeText(card.rectTransform, "Props", Props(d), new Vector2(0.5f, 0f), new Vector2(0f, 34f),
                    new Vector2(CardW - 12f, 58f), 7, new Color(1f, 1f, 1f, 0.78f), TextAnchor.UpperCenter);

                // tecla
                if (i < 10)
                    MakeText(card.rectTransform, "Key", i == 9 ? "0" : (i + 1).ToString(), new Vector2(0f, 1f),
                        new Vector2(12f, -10f), new Vector2(20f, 14f), 8, new Color(1f, 1f, 1f, 0.4f), TextAnchor.MiddleLeft);

                // overlay de "no jugable en este modo"
                _cardOverlay[i] = MakeImage(card.rectTransform, "Overlay", new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(CardW, CardH), new Color(0.02f, 0.02f, 0.03f, 0.8f));
                _cardOverlay[i].gameObject.SetActive(false);
            }

            // botones contextuales a los costados del abanico
            (_leftBtn1, _leftLbl1) = MakeButton(rootRt, "L1", new Vector2(-720f, 190f), new Vector2(170f, 52f), BtnBlue);
            (_leftBtn2, _leftLbl2) = MakeButton(rootRt, "L2", new Vector2(-720f, 130f), new Vector2(170f, 52f), BtnGold);
            (_rightBtn1, _rightLbl1) = MakeButton(rootRt, "R1", new Vector2(720f, 190f), new Vector2(170f, 52f), BtnGold);
            (_rightBtn2, _rightLbl2) = MakeButton(rootRt, "R2", new Vector2(720f, 130f), new Vector2(170f, 52f), BtnGreen);

            // picker modal encima del abanico
            _pickPanel = MakeImage(rootRt, "Picker", new Vector2(0.5f, 0f), new Vector2(0f, BaseY + CardH + 60f),
                new Vector2(400f, 62f), new Color(0.05f, 0.07f, 0.1f, 1f));
            for (int o = 0; o < _pickBtn.Length; o++)
            {
                _pickBtn[o] = MakeImage(_pickPanel.rectTransform, "P" + o, new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(186f, 50f), new Color(0.16f, 0.2f, 0.28f, 1f));
                _pickLbl[o] = MakeText(_pickBtn[o].rectTransform, "T", "", new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(182f, 44f), 9, Color.white, TextAnchor.MiddleCenter);
            }
            _pickPanel.gameObject.SetActive(false);

            // panel de detalle a la derecha
            var infoBg = MakeImage(rootRt, "InfoBg", new Vector2(1f, 0f), new Vector2(-180f, 420f),
                new Vector2(330f, 210f), new Color(0.04f, 0.05f, 0.07f, 0.94f));
            var ibr = infoBg.rectTransform;
            _detailTitle = MakeText(ibr, "Title", "", new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(306f, 22f), 12, Color.white, TextAnchor.MiddleLeft);
            _detailStats = MakeText(ibr, "Stats", "", new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(306f, 20f), 10, new Color(0.95f, 0.88f, 0.55f), TextAnchor.MiddleLeft);
            _detailTag = MakeText(ibr, "Tag", "", new Vector2(0.5f, 1f), new Vector2(0f, -68f), new Vector2(306f, 30f), 8, Color.white, TextAnchor.UpperLeft);
            _detail = MakeText(ibr, "Desc", "", new Vector2(0.5f, 1f), new Vector2(0f, -104f), new Vector2(306f, 96f), 12, new Color(1f, 1f, 1f, 0.85f), TextAnchor.UpperLeft, pixel: false);

            // estado / instrucciones del modo, arriba del abanico
            _status = MakeText(rootRt, "Status", "", new Vector2(0.5f, 0f), new Vector2(0f, BaseY + CardH + 28f),
                new Vector2(1400f, 22f), 11, new Color(0.5f, 1f, 0.6f), TextAnchor.MiddleCenter);

            RefreshStates();
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

        // ---- estados / botones ----

        bool CardEnabled(int i)
        {
            var hand = S.Hand[0];
            if (i >= hand.Count) return false;
            var d = S.Def(0, hand[i]);
            switch (_mode)
            {
                case Mode.Opener:
                    if (d.Kind == CardKind.Ability) return S.Active == 0 && S.CanPlayAbility(i);
                    return S.LegalOpener(0, i) || !S.HasLegalOpener(0);
                case Mode.Combo:
                    return S.ComboOptions(0).Contains(i);
                case Mode.Punish:
                    if (S.HitBackPlayed) return false;
                    return (d.Kind == CardKind.Attack || d.Kind == CardKind.Throw) &&
                           (!d.IsSuper || S.Meter[0] >= d.SuperCost);
                case Mode.ExchangeGive:
                    return d.IsNormal;
                case Mode.PowerA:
                {
                    for (int j = 0; j < hand.Count; j++) if (j != i && hand[j] == hand[i]) return true;
                    return false;
                }
                case Mode.PowerB:
                    return i != _powerA && hand[i] == hand[_powerA];
            }
            return false;
        }

        void RefreshStates()
        {
            if (S == null) return;
            for (int i = 0; i < _cardRt.Length; i++)
                _cardOverlay[i].gameObject.SetActive(!CardEnabled(i));

            bool mine = S.Active == 0 && !S.AwaitingFollowup;
            bool hasPair = false;
            var hand = S.Hand[0];
            for (int i = 0; i < hand.Count && !hasPair; i++)
                for (int j = i + 1; j < hand.Count; j++)
                    if (hand[i] == hand[j]) { hasPair = true; break; }

            // izquierda: CAMBIO y PODER (solo en TU main phase)
            bool showEx = _mode == Mode.Opener && mine && S.ExchangesLeft > 0 && S.Discard[0].Count > 0;
            bool showPu = _mode == Mode.Opener && mine && !S.PowerUpUsed && hasPair;
            SetBtn(_leftBtn1, _leftLbl1, showEx, $"CAMBIO ×{S.ExchangesLeft}\n<size=9>con el descarte</size>", BtnBlue);
            SetBtn(_leftBtn2, _leftLbl2, showPu, "PODER\n<size=9>par → meter/super</size>", BtnGold);
            if (_mode == Mode.ExchangeGive || _mode == Mode.PowerA || _mode == Mode.PowerB)
                SetBtn(_leftBtn1, _leftLbl1, true, "CANCELAR", BtnRed);

            // derecha: PUMP y TERMINAR/PASAR según el followup
            bool pump = (_mode == Mode.Combo || _mode == Mode.Punish) && S.CanPumpLast();
            string pumpTxt = "";
            if (pump)
            {
                var d = S.Def(0, S.LastPlayed);
                string fuel = d.Pump == PumpFuel.ZCard ? "quema Z" : d.Pump == PumpFuel.SuperCard ? "quema super" : "quema 1 carta";
                pumpTxt = $"¡PUMP! +{d.PumpDamage}\n<size=9>{fuel}</size>";
            }
            SetBtn(_rightBtn1, _rightLbl1, pump, pumpTxt, BtnGold);
            bool follow = _mode == Mode.Combo || _mode == Mode.Punish;
            string endTxt = _mode == Mode.Punish && !S.HitBackPlayed ? "PASAR\n<size=9>no castigar</size>" : "TERMINAR\n<size=9>cerrar el combo</size>";
            SetBtn(_rightBtn2, _rightLbl2, follow, endTxt, _mode == Mode.Punish ? BtnRed : BtnGreen);

            _status.text = StatusText();
        }

        static void SetBtn(Image btn, Text lbl, bool on, string txt, Color c)
        {
            if (btn.gameObject.activeSelf != on) btn.gameObject.SetActive(on);
            if (!on) return;
            lbl.text = txt;
            btn.color = c;
        }

        string StatusText()
        {
            switch (_mode)
            {
                case Mode.Combo:
                    var opts = S.ComboOptions(0);
                    return $"¡CONECTASTE! seguí el combo ({S.FollowCpLeft} CP restantes) o TERMINÁ" +
                           (S.Def(0, S.LastPlayed).KnockdownOnHit ? " — terminando ACÁ conservás el DERRIBO" : "");
                case Mode.Punish:
                    return S.HitBackPlayed ? "castigo hecho: ¿pump?" : "¡CASTIGO! devolvé UN golpe o agarre (ESPACIO pasa)";
                case Mode.ExchangeGive:
                    return "CAMBIO: elegí qué carta SOLTAR (Backspace cancela)";
                case Mode.PowerA: return "PODER: elegí la PRIMERA carta del par a descartar";
                case Mode.PowerB: return "PODER: elegí la SEGUNDA carta (mismo nombre)";
            }
            if (S != null && S.Active == 0)
                return "TU TURNO: jugá tu opener (la habilidad se juega con click, antes)";
            return "TURNO RIVAL (gana empates): jugá tu opener boca arriba";
        }

        // ---- interacción ----

        void Update()
        {
            if (!_active || S == null || _root == null) return;

            var mp = GameInput.MousePos();
            // picker modal abierto: captura todo
            if (_pickPanel != null && _pickPanel.gameObject.activeSelf) { UpdatePicker(mp); return; }

            // hover del abanico: el de MÁS a la derecha bajo el mouse gana
            int hover = -1;
            for (int i = _cardRt.Length - 1; i >= 0; i--)
                if (RectTransformUtility.RectangleContainsScreenPoint(_cardRt[i], mp, null)) { hover = i; break; }
            if (hover != _hover)
            {
                _hover = hover;
                if (hover >= 0) { SfxLib.Play(SfxLib.Kind.UiTick, 0.25f); FillDetail(hover); }
                LayoutHand();
            }

            // botones
            bool overL1 = _leftBtn1.gameObject.activeSelf && RectTransformUtility.RectangleContainsScreenPoint(_leftBtn1.rectTransform, mp, null);
            bool overL2 = _leftBtn2.gameObject.activeSelf && RectTransformUtility.RectangleContainsScreenPoint(_leftBtn2.rectTransform, mp, null);
            bool overR1 = _rightBtn1.gameObject.activeSelf && RectTransformUtility.RectangleContainsScreenPoint(_rightBtn1.rectTransform, mp, null);
            bool overR2 = _rightBtn2.gameObject.activeSelf && RectTransformUtility.RectangleContainsScreenPoint(_rightBtn2.rectTransform, mp, null);

            if (GameInput.ClickPressed())
            {
                if (hover >= 0) { ClickCard(hover); return; }
                if (overL1) { ClickLeft1(); return; }
                if (overL2) { ClickLeft2(); return; }
                if (overR1) { SfxLib.Play(SfxLib.Kind.UiClick, 0.8f); _mc.CardsPump(); return; }
                if (overR2) { ClickEnd(); return; }
            }

            int num = GameInput.NumberPressed();
            if (num > 0 && num <= _cardRt.Length) ClickCard(num - 1);
            if (GameInput.UndoPressed() && (_mode == Mode.ExchangeGive || _mode == Mode.PowerA || _mode == Mode.PowerB))
            { SfxLib.Play(SfxLib.Kind.UiCancel, 0.5f); Rebuild(Mode.Opener); }
            if (GameInput.EndTurnPressed())
            {
                if (_mode == Mode.Punish && !S.HitBackPlayed) { _mc.CardsPunish(-1); return; }
                if (_mode == Mode.Combo || (_mode == Mode.Punish && S.HitBackPlayed)) { _mc.CardsComboEnd(); return; }
            }
        }

        void ClickCard(int i)
        {
            if (!CardEnabled(i)) { SfxLib.Play(SfxLib.Kind.UiCancel, 0.4f); return; }
            var hand = S.Hand[0];
            var d = S.Def(0, hand[i]);
            SfxLib.Play(SfxLib.Kind.UiClick, 0.75f);
            switch (_mode)
            {
                case Mode.Opener:
                    if (d.Kind == CardKind.Ability) { _mc.CardsPlayAbility(i); return; }
                    _mc.CardsPick(i);
                    return;
                case Mode.Combo:
                    _mc.CardsComboAdd(i);
                    return;
                case Mode.Punish:
                    _mc.CardsPunish(i);
                    return;
                case Mode.ExchangeGive:
                    _exGive = i;
                    OpenExchangePicker();
                    return;
                case Mode.PowerA:
                    _powerA = i;
                    _mode = Mode.PowerB;
                    RefreshStates();
                    return;
                case Mode.PowerB:
                    OpenPowerPicker(i);
                    return;
            }
        }

        void ClickLeft1()
        {
            SfxLib.Play(SfxLib.Kind.UiClick, 0.7f);
            if (_mode == Mode.ExchangeGive || _mode == Mode.PowerA || _mode == Mode.PowerB) { Rebuild(Mode.Opener); return; }
            Rebuild(Mode.ExchangeGive);
        }

        void ClickLeft2()
        {
            SfxLib.Play(SfxLib.Kind.UiClick, 0.7f);
            Rebuild(Mode.PowerA);
        }

        void ClickEnd()
        {
            SfxLib.Play(SfxLib.Kind.UiClick, 0.8f);
            if (_mode == Mode.Punish && !S.HitBackPlayed) { _mc.CardsPunish(-1); return; }
            _mc.CardsComboEnd();
        }

        // ---- pickers modales ----

        void OpenExchangePicker()
        {
            _pickCount = 0;
            var disc = S.Discard[0];
            var counts = new int[CardCatalog.CardsPerChar];
            foreach (int c in disc) counts[c]++;
            for (int c = 0; c < counts.Length && _pickCount < _pickBtn.Length; c++)
            {
                if (counts[c] == 0 || !S.Def(0, c).IsNormal) continue;
                _pickVal[_pickCount] = c;
                _pickLbl[_pickCount].text = counts[c] > 1 ? $"{S.Def(0, c).Name} ×{counts[c]}" : S.Def(0, c).Name;
                _pickCount++;
            }
            if (_pickCount == 0) { SfxLib.Play(SfxLib.Kind.UiCancel, 0.5f); Rebuild(Mode.Opener); return; }
            _pickIsPower = false;
            ShowPicker("¿qué RECUPERÁS del descarte?");
        }

        void OpenPowerPicker(int powerB)
        {
            _pickCount = 0;
            _pickVal[_pickCount] = -1000; // +2 meter
            _pickLbl[_pickCount].text = "+2 SUPER METER";
            _pickCount++;
            if (S.Discard[0].Contains(CardCatalog.Super1))
            {
                _pickVal[_pickCount] = CardCatalog.Super1;
                _pickLbl[_pickCount].text = $"RECUPERAR {S.Def(0, CardCatalog.Super1).Name} (+1)";
                _pickCount++;
            }
            if (S.Discard[0].Contains(CardCatalog.Super2))
            {
                _pickVal[_pickCount] = CardCatalog.Super2;
                _pickLbl[_pickCount].text = $"RECUPERAR {S.Def(0, CardCatalog.Super2).Name} (+1)";
                _pickCount++;
            }
            _powerB = powerB;
            _pickIsPower = true;
            ShowPicker("¿el beneficio del PODER?");
        }
        int _powerB = -1;

        void ShowPicker(string hint)
        {
            float w = 196f;
            for (int o = 0; o < _pickBtn.Length; o++)
            {
                bool on = o < _pickCount;
                _pickBtn[o].gameObject.SetActive(on);
                if (on) _pickBtn[o].rectTransform.anchoredPosition = new Vector2((o - (_pickCount - 1) * 0.5f) * w, 0f);
            }
            _pickPanel.rectTransform.sizeDelta = new Vector2(_pickCount * w + 14f, 62f);
            _pickPanel.gameObject.SetActive(true);
            _status.text = hint + "  (Backspace cancela)";
            SfxLib.Play(SfxLib.Kind.UiTick, 0.5f);
        }

        void UpdatePicker(Vector2 mp)
        {
            for (int o = 0; o < _pickCount; o++)
                _pickBtn[o].color = RectTransformUtility.RectangleContainsScreenPoint(_pickBtn[o].rectTransform, mp, null)
                    ? new Color(0.26f, 0.34f, 0.46f, 1f) : new Color(0.16f, 0.2f, 0.28f, 1f);
            if (GameInput.UndoPressed()) { SfxLib.Play(SfxLib.Kind.UiCancel, 0.5f); Rebuild(Mode.Opener); return; }
            if (!GameInput.ClickPressed()) return;
            for (int o = 0; o < _pickCount; o++)
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(_pickBtn[o].rectTransform, mp, null)) continue;
                SfxLib.Play(SfxLib.Kind.UiClick, 0.8f);
                if (_pickIsPower)
                {
                    bool fetch = _pickVal[o] != -1000;
                    _mc.CardsPowerUp(_powerA, _powerB, fetch, fetch ? _pickVal[o] : -1);
                }
                else
                {
                    int discIdx = S.Discard[0].IndexOf(_pickVal[o]);
                    _mc.CardsExchange(_exGive, discIdx);
                }
                return;
            }
            // click afuera: cancela
            SfxLib.Play(SfxLib.Kind.UiCancel, 0.4f);
            Rebuild(Mode.Opener);
        }

        // ---- layout / detalle ----

        void LayoutHand()
        {
            int n = _cardRt.Length;
            float spacing = n <= 1 ? 0f : Mathf.Min(CardW + 8f, 1080f / (n - 1));
            float x0 = -spacing * (n - 1) * 0.5f;
            for (int i = 0; i < n; i++)
            {
                bool hov = i == _hover;
                _cardRt[i].anchoredPosition = new Vector2(x0 + i * spacing, hov ? HoverY : BaseY);
                _cardRt[i].localScale = Vector3.one * (hov ? HoverScale : 1f);
            }
            // orden de dibujado: izquierda→derecha, y el hovereado AL FRENTE
            for (int i = 0; i < n; i++) _cardRt[i].SetSiblingIndex(i);
            if (_hover >= 0) _cardRt[_hover].SetAsLastSibling();
        }

        void FillDetail(int i)
        {
            var hand = S.Hand[0];
            if (i >= hand.Count) return;
            int card = hand[i];
            var d = S.Def(0, card);
            var col = HudUI.CardDefColor(d, card);
            _detailTitle.text = d.Name.ToUpperInvariant();
            _detailTitle.color = new Color(col.r * 0.5f + 0.5f, col.g * 0.5f + 0.5f, col.b * 0.5f + 0.5f);
            _detailStats.text = HudUI.CardDefInfo(d);
            _detailTag.text = Props(d);
            _detailTag.color = new Color(col.r * 0.6f + 0.4f, col.g * 0.6f + 0.4f, col.b * 0.6f + 0.4f);
            _detail.text = DetailDesc(card, d);
        }
    }
}
