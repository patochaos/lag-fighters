using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LagFighter
{
    // Menú de planificación del turno (tiempo pausado). Grilla 6x2:
    // fila de arriba = movimiento y defensa, fila de abajo = ataques.
    // Cada carta lleva una franja de color por categoría y una mini-barra
    // de framedata (startup amarillo / activo rojo / recovery azul), el
    // mismo lenguaje visual que la barra de fases del HUD.
    // Click o 1-9/0 agrega · ←→↑↓ + Enter/J agrega · Backspace borra ·
    // Espacio (o botón ¡LISTO!) cierra el turno.
    public class PlanMenuUI : MonoBehaviour
    {
        const float CardW = 168f, CardH = 44f, Gap = 6f;

        // Grilla 3×3 (2026-07-20, anti-clutter): Dash y Salto son UNA carta
        // que abre un mini-picker de dirección al apretarla (Salto − vuelve
        // como dirección). El Parry SALIÓ del clásico: Bloquear es LA defensa
        // y banca AP. Sentinelas negativos = cartas de grupo.
        public const int CardDash = -2, CardJump = -3;
        static readonly int[] ClassicOrder =
        {
            MoveCatalog.WalkB, CardDash, CardJump,
            MoveCatalog.AttackA, MoveCatalog.AttackB, MoveCatalog.Grab,
            MoveCatalog.Tatsu, MoveCatalog.Hadouken, MoveCatalog.Shoryuken,
            // agachado desactivado — reactivar junto con SimConfig.CrouchEnabled:
            // MoveCatalog.LowKick, MoveCatalog.Crouch,
        };

        // el representante de cada grupo (color, costo, framedata del panel)
        static int Rep(int v) => v == CardDash ? MoveCatalog.DashF : v == CardJump ? MoveCatalog.JumpF : v;
        static readonly (int move, string label)[] DashOptions =
            { (MoveCatalog.DashF, "ADELANTE →"), (MoveCatalog.DashB, "← ATRÁS") };
        static readonly (int move, string label)[] JumpOptions =
            { (MoveCatalog.JumpF, "ADELANTE →"), (MoveCatalog.JumpN, "NEUTRO ↑"), (MoveCatalog.JumpB, "← ATRÁS") };

        // Modo YOMI v2 (discreto): 8 acciones en una fila, UNA por turno —
        // click en la carta = jugarla. La dirección de dash/salto la decide
        // la distancia actual (cerca: te vas · lejos: entrás).
        static readonly int[] YomiOrder =
        {
            (int)YomiAction.Jab, (int)YomiAction.Kick, (int)YomiAction.Grab,
            (int)YomiAction.Shoryu, (int)YomiAction.Parry, (int)YomiAction.Dash,
            (int)YomiAction.Jump, (int)YomiAction.Charge,
        };

        int[] _order = ClassicOrder;
        int _cols = 6;
        bool _builtYomi;
        // Modo CARTAS (copia de Yomi 2): la grilla ES la mano (hasta 12,
        // posición = índice de mano) y se reconstruye cada turno. Estados:
        // opener (click juega) · castigo (solo golpes/agarres, ESPACIO pasa)
        // · cambio (elegís qué soltar y un picker ofrece el descarte).
        bool _builtCards;
        bool _punishMode;
        bool _exMode;
        int _exGive = -1;                 // índice de mano elegido para soltar
        Image _exPanel;
        readonly Image[] _exBtns = new Image[9];
        readonly Text[] _exLabels = new Text[9];
        readonly int[] _exCards = new int[9];
        int _exCount;
        float _gridTotalH;
        RectTransform _canvasRt;

        MatchController _mc;
        Font _font;
        GameObject _root;
        Image[] _cardBg, _cardEdge, _cardOverlay;
        Text[] _cardName, _cardOvfMark; // "»" sutil: este move cruzaría el turno
        RectTransform[] _cardRt;
        Image _undoBtn, _doneBtn, _wakeBtn, _superBtn;
        Text _wakeLabel, _doneLabel, _superLabel;
        // hover: tinte sobre el color base de cada botón
        static readonly Color DoneC = new Color(0.18f, 0.45f, 0.22f, 0.95f);
        static readonly Color UndoC = new Color(0.4f, 0.2f, 0.2f, 0.95f);
        static readonly Color WakeC = new Color(0.5f, 0.32f, 0.1f, 0.95f);
        static readonly Color SuperDimC = new Color(0.22f, 0.18f, 0.05f, 0.92f); // cargando: dorado apagado
        // panel de info a la derecha de la grilla: se llena con el hover
        Text _detailTitle, _detailFrames, _detailAdv, _detailTag, _detail, _status;
        // mini-picker de dirección para las cartas de grupo (DASH / SALTO):
        // se abre encima de la carta, click o 1-3 elige, afuera cierra.
        // (Las bolitas de AP viven en el HUD, bajo las barras de cada lado.)
        Image _subPanel;
        readonly Image[] _subBtn = new Image[3];
        readonly Text[] _subLabel = new Text[3];
        readonly int[] _subMoves = new int[3];
        int _subGroup; // CardDash/CardJump, 0 = cerrado
        int _subCount;
        RectTransform _gridPanelRt;
        Image _segBg, _segS, _segA, _segR;
        float _segW;
        int _sel;
        bool _active;
        bool _mouseOnCards;
        Vector2 _lastMouse;

        public static PlanMenuUI Create(MatchController mc)
        {
            var go = new GameObject("LagFighter.PlanMenu");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var ui = go.AddComponent<PlanMenuUI>();
            ui._mc = mc;
            ui._canvasRt = go.GetComponent<RectTransform>();
            ui.Rebuild();
            return ui;
        }

        // La grilla depende del modo (12 cartas clásicas vs 7 yomi): se
        // reconstruye al abrir si el modo cambió desde la última vez.
        void Rebuild()
        {
            if (_root != null) Destroy(_root);
            _subGroup = 0; // el picker vivía dentro de _root
            _builtYomi = SimConfig.YomiEnabled;
            _builtCards = SimConfig.CardsEnabled;
            _punishMode = _exMode = false;
            _exGive = -1;
            _order = _builtCards ? CardsHand() : _builtYomi ? YomiOrder : ClassicOrder;
            _cols = _builtCards ? 6 : _builtYomi ? YomiOrder.Length : 3; // clásico: 3×3, teclas 1-9
            _sel = 0;
            Build(_canvasRt);
            _root.SetActive(false);
        }

        // La mano actual como grilla: posición = índice de mano (CardsPick
        // y el resto de la tubería usan ese índice directo).
        int[] CardsHand()
        {
            var cs = _mc.Cards;
            if (cs == null || cs.Hand[0].Count == 0) return new int[0];
            return cs.Hand[0].ToArray();
        }

        static Color CategoryColor(int mi)
        {
            var m = MoveCatalog.All[mi];
            if (mi == MoveCatalog.Grab || mi == MoveCatalog.YomiGrab) return new Color(0.85f, 0.3f, 0.75f);
            if (mi == MoveCatalog.Strong) return new Color(0.95f, 0.55f, 0.2f);
            if (mi == MoveCatalog.Shoryuken) return new Color(0.95f, 0.7f, 0.15f);
            if (mi == MoveCatalog.Hadouken) return new Color(0.3f, 0.55f, 0.95f);
            if (mi == MoveCatalog.Tatsu) return new Color(0.9f, 0.45f, 0.15f);
            if (m.Hits.Length > 0 && m.HasAir) return new Color(0.55f, 0.8f, 0.35f);
            if (m.IsAttack) return new Color(0.9f, 0.32f, 0.24f);
            if (m.HasAir) return new Color(0.55f, 0.8f, 0.35f);
            if (mi == MoveCatalog.Parry || mi == MoveCatalog.WalkB || mi == MoveCatalog.Crouch) return new Color(0.35f, 0.55f, 0.85f);
            return new Color(0.3f, 0.7f, 0.45f);
        }

        // En YOMI la tag canta la fila de la matriz EN la distancia actual:
        // qué le gana y qué la castiga, sin letra chica.
        static string YomiTag(YomiAction a, bool close)
        {
            if (close)
                switch (a)
                {
                    case YomiAction.Jab: return "GANA A: KICK, AGARRE, SALTO · PIERDE CON: PARRY, SHORYU";
                    case YomiAction.Kick: return "GANA A: AGARRE, CARGAR · CAZA EL DASH · PIERDE CON: JAB, PARRY";
                    case YomiAction.Grab: return "ROMPE EL PARRY, TIRA A LEJOS · PIERDE CON: GOLPES";
                    case YomiAction.Parry: return "BLOQUEA GOLPES: +1 AP Y DEVUELVE 1 · PIERDE CON: AGARRE, SHORYU";
                    case YomiAction.Shoryu: return "LE GANA A TODO DE CERCA · SI SE VAN: WHIFF Y RECOVERY";
                    case YomiAction.Dash: return "TE VAS A LEJOS · ESQUIVA JAB, AGARRE Y SHORYU · KICK TE CAZA";
                    case YomiAction.Jump: return "ESCAPÁS SALTANDO · ESQUIVA KICK Y AGARRE · JAB TE BAJA";
                    default: return "+2 AP SI NO TE PEGAN · TODO GOLPE ES COUNTER (+1)";
                }
            switch (a)
            {
                case YomiAction.Jab: case YomiAction.Grab: return "NO LLEGA DESDE LEJOS";
                case YomiAction.Kick: return "LA ZONEADORA: CAZA DASH Y CARGAR · PIERDE CON: SALTO, PARRY";
                case YomiAction.Parry: return "BLOQUEA KICK Y PATADA: +1 AP Y DEVUELVE 1";
                case YomiAction.Shoryu: return "SOLO LECTURA: BAJA AL SALTO ENTRANTE · SI NO, WHIFF Y RECOVERY";
                case YomiAction.Dash: return "ENTRÁS · GRATIS VS PARRY/CARGAR · KICK TE FRENA";
                case YomiAction.Jump: return "ENTRÁS CON PATADA · LE GANA A KICK · PARRY LA BLOQUEA";
                default: return "+2 AP SI NO TE PEGAN · KICK TE CASTIGA";
            }
        }

        static string YomiDesc(YomiAction a)
        {
            switch (a)
            {
                case YomiAction.Jab: return "El golpe rápido y seguro: 1 de daño. Solo llega de cerca.";
                case YomiAction.Kick: return "2 de daño y llega a AMBAS distancias, pero es lenta: el jab la gana de cerca.";
                case YomiAction.Grab: return "2 de daño, derriba y manda el combate a LEJOS. La respuesta al que parrea.";
                case YomiAction.Parry: return "La lectura defensiva: si comés un golpe con el parry listo, lo devolvés.";
                case YomiAction.Shoryu: return "3 de daño, derriba, imparable de cerca. Carísimo, y si whiffea perdés el turno siguiente.";
                case YomiAction.Dash: return "Cambiás de distancia por 1 AP. La dirección la decide dónde estás parado.";
                case YomiAction.Jump: return "El cambio de distancia con patada (1): entra pegando desde lejos.";
                default: return "No hacés nada… y juntás 2 AP. El rival lo sabe: cargar es una apuesta.";
            }
        }

        static string CardTag(int mi)
        {
            if (mi == CardDash) return "ADELANTE o ATRÁS · no bloquea";
            if (mi == CardJump) return "ADELANTE / NEUTRO / ATRÁS · patada al caer";
            switch (mi)
            {
                case MoveCatalog.Grab: return "ROMPE GUARDIA · TIRA";
                case MoveCatalog.Shoryuken: return "INVULN 1-10 · DERRIBA";
                case MoveCatalog.Hadouken: return "PROYECTIL · turno entero";
                case MoveCatalog.Tatsu: return "PASA HADOUKENS · DERRIBA";
                case MoveCatalog.WalkB: return "BLOQUEA · retrocede";
                case MoveCatalog.Parry: return "PARRY f3-7 · pierde vs AGARRE";
                case MoveCatalog.Crouch: return "BLOQUEA · esquiva ALTOS";
                case MoveCatalog.LowKick: return "PEGA BAJO · agachado";
                case MoveCatalog.JumpF: return "PATADA AL CAER · +1.9";
                case MoveCatalog.JumpN: return "PATADA AL CAER · vertical";
                case MoveCatalog.AttackA: return "+2 hit / −5 block";
                case MoveCatalog.AttackB: return "DERRIBA · −10 block";
                case MoveCatalog.DashF: return "cierra distancia · no bloquea";
                case MoveCatalog.DashB: return "el bait · no bloquea";
                default: return "";
            }
        }

        // Nombre en la carta: en YOMI el valor de _order es una YomiAction;
        // en CARTAS es un id de CardCatalog.
        string DisplayName(int mi)
        {
            if (_builtCards) return CardCatalog.All[mi].Name.ToUpperInvariant();
            if (mi == CardDash) return "DASH";
            if (mi == CardJump) return "SALTO";
            return _builtYomi
                ? YomiConfig.Name((YomiAction)mi).ToUpperInvariant()
                : MoveCatalog.All[mi].Name.ToUpperInvariant();
        }

        // color de categoría según el modo (en YOMI/CARTAS, el mismo color
        // que las cartas de revelación del HUD)
        Color CardColor(int v) => _builtCards ? HudUI.CardIdColor(v)
            : _builtYomi ? HudUI.YomiActionColor((YomiAction)v) : CategoryColor(Rep(v));

        // ---- textos del modo CARTAS ----

        static string CardsTag(int card)
        {
            switch (card)
            {
                case CardCatalog.AttackA:
                case CardCatalog.AttackB: return "PEGA BAJO · GANA AL AGARRE · LO PARA EL BLOQUEO BAJO";
                case CardCatalog.AttackC: return "PEGA MID · CUALQUIER BLOQUEO LO PARA";
                case CardCatalog.AttackD:
                case CardCatalog.AttackE: return "PEGA ALTO · GANA AL AGARRE · LO PARA EL BLOQUEO ALTO";
                case CardCatalog.Throw: return "GANA A BLOQUEOS Y ESQUIVES · PIERDE CON GOLPES · DERRIBA";
                case CardCatalog.Dodge: return "EVITA GOLPES · CASTIGA STRIKES · PIERDE CON AGARRE";
                case CardCatalog.LowBlock: return "PARA BAJOS Y MIDS · ROBA 1 · VUELVE A LA MANO";
                case CardCatalog.HighBlock: return "PARA ALTOS Y MIDS · ROBA 1 · VUELVE A LA MANO";
                case CardCatalog.SpecialX: return "PROYECTIL NV.1 · CHIP 4 · SIN ROBO AL BLOQUEARLO · VUELVE";
                case CardCatalog.SpecialY: return "SPEED 11: EL REVERSAL · UNSAFE SI TE LO BLOQUEAN";
                default: return "PEGA ALTO A SPEED 7 · EL MIXUP RÁPIDO DE ALTURA";
            }
        }

        static string CardsDesc(int card)
        {
            switch (card)
            {
                case CardCatalog.AttackA: return "El poke: lo más rápido de la mano (speed 8), pega poco y BAJO.";
                case CardCatalog.AttackB: return "Un pelín más lento que A, un punto más de daño. También BAJO.";
                case CardCatalog.AttackC: return "El medio de la tabla: speed 6, 5 de daño, cualquier bloqueo lo para.";
                case CardCatalog.AttackD: return "Pesado y ALTO: castiga al que bloquea bajo esperando A/B.";
                case CardCatalog.AttackE: return "El más lento (speed 4) y el que más pega (7). Solo entra con lectura.";
                case CardCatalog.Throw: return "7 de daño y DERRIBA: derribado no esquivás y sus golpes suben a speed 10.";
                case CardCatalog.Dodge: return "Esquiva cualquier golpe; si era un strike, devolvés UNA carta de ataque o agarre.";
                case CardCatalog.LowBlock: return "La defensa que se paga sola: roba 1 carta y vuelve a la mano si no te pegan.";
                case CardCatalog.HighBlock: return "Ídem bajo, pero cubre lo ALTO. Ojo: el agarre rompe cualquier bloqueo.";
                case CardCatalog.SpecialX: return "El motor de mano del zoner: pega 8, hace 4 de chip, no deja robar y VUELVE.";
                case CardCatalog.SpecialY: return "Speed 11: le gana a todo lo demás. Pero si te lo bloquean, hay castigo.";
                default: return "Speed 7 que pega ALTO: el que bloquea bajo por miedo a A/B lo come entero.";
            }
        }

        void Build(RectTransform canvasRt)
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _root = new GameObject("Root", typeof(RectTransform));
            var rootRt = _root.GetComponent<RectTransform>();
            rootRt.SetParent(canvasRt, false);
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = rootRt.offsetMax = Vector2.zero;

            int rows = Mathf.CeilToInt(_order.Length / (float)_cols);
            float totalW = _cols * (CardW + Gap) - Gap;
            float totalH = rows * (CardH + Gap) - Gap;

            var panel = MakeImage(rootRt, "Panel", new Vector2(0.5f, 0f), new Vector2(0f, 26f + totalH / 2f + 12f),
                new Vector2(totalW + 28f, totalH + 28f), new Color(0.04f, 0.05f, 0.07f, 0.85f));

            _cardBg = new Image[_order.Length];
            _cardEdge = new Image[_order.Length];
            _cardName = new Text[_order.Length];
            _cardOvfMark = new Text[_order.Length];
            _cardOverlay = new Image[_order.Length];
            _cardRt = new RectTransform[_order.Length];

            for (int pos = 0; pos < _order.Length; pos++)
            {
                int mi = _order[pos];
                int col = pos % _cols, row = pos / _cols;
                float x = -totalW / 2f + CardW / 2f + col * (CardW + Gap);
                float y = totalH / 2f - CardH / 2f - row * (CardH + Gap);
                var cat = CardColor(mi);

                // carta compacta: solo el nombre, grande. La data vive en el
                // panel de info de la derecha (se llena con el hover).
                var card = MakeImage(panel.rectTransform, "Card" + pos, new Vector2(0.5f, 0.5f), new Vector2(x, y),
                    new Vector2(CardW, CardH), new Color(0.12f, 0.13f, 0.17f, 0.98f));
                _cardBg[pos] = card;
                _cardRt[pos] = card.rectTransform;

                // borde izquierdo con el color de la categoría
                _cardEdge[pos] = MakeImage(card.rectTransform, "Edge", new Vector2(0f, 0.5f), new Vector2(3f, 0f),
                    new Vector2(6f, CardH - 6f), new Color(cat.r, cat.g, cat.b, 0.9f));

                // pictograma del move junto al borde (clásico; en YOMI los
                // valores son acciones de otra tabla, sin icono)
                var iconSpr = _builtYomi ? null : MoveIcons.Get(Rep(mi));
                if (iconSpr != null)
                {
                    var icon = MakeImage(card.rectTransform, "Icon", new Vector2(0f, 0.5f),
                        new Vector2(14f, 0f), new Vector2(22f, 20f), new Color(cat.r * 0.5f + 0.5f, cat.g * 0.5f + 0.5f, cat.b * 0.5f + 0.5f, 0.95f));
                    icon.rectTransform.pivot = new Vector2(0f, 0.5f);
                    icon.sprite = iconSpr;
                    icon.preserveAspect = true;
                }

                _cardName[pos] = MakeText(card.rectTransform, "Name", DisplayName(mi), new Vector2(0.5f, 0.5f), new Vector2(6f, 0f),
                    new Vector2(CardW - 26f, 30f), 8, new Color(1f, 1f, 1f, 0.92f), TextAnchor.MiddleCenter);
                _cardName[pos].font = UIFonts.Pixel;

                string key = pos < 9 ? (pos + 1).ToString() : pos == 9 ? "0" : "";
                var keyT = MakeText(card.rectTransform, "Key", key, new Vector2(0f, 1f), new Vector2(14f, -10f),
                    new Vector2(20f, 16f), 8, new Color(1f, 1f, 1f, 0.5f), TextAnchor.MiddleLeft);
                keyT.font = UIFonts.Pixel;

                // costo en AP abajo a la derecha (YOMI: su tabla · clásico:
                // ceil(frames/12) — el presupuesto del turno ES en AP)
                if (_builtYomi || (!_builtCards && SimConfig.ApActive))
                {
                    int cost = _builtYomi ? YomiConfig.Cost((YomiAction)mi) : MoveCatalog.All[Rep(mi)].ApCost;
                    var costT = MakeText(card.rectTransform, "Cost", cost == 0 ? "GRATIS" : $"{cost} AP",
                        new Vector2(1f, 0f), new Vector2(-8f, 10f), new Vector2(60f, 14f), 8,
                        new Color(0.5f, 0.95f, 1f, 0.85f), TextAnchor.MiddleRight);
                    costT.font = UIFonts.Pixel;
                    costT.rectTransform.pivot = new Vector2(1f, 0.5f);
                }
                else if (_builtCards)
                {
                    // en CARTAS lo que importa es speed y daño (o qué cubre)
                    var d = CardCatalog.All[mi];
                    string txt = d.Kind == CardKind.Block ? (d.BlocksLow ? "BAJO+MID" : "ALTO+MID")
                        : d.Kind == CardKind.Dodge ? "ESQUIVA"
                        : d.Kind == CardKind.Throw ? $"s{d.Speed}·{d.Damage}·KD"
                        : $"s{d.Speed}·{d.Damage}";
                    var costT = MakeText(card.rectTransform, "Cost", txt,
                        new Vector2(1f, 0f), new Vector2(-8f, 10f), new Vector2(86f, 14f), 8,
                        new Color(0.5f, 0.95f, 1f, 0.85f), TextAnchor.MiddleRight);
                    costT.font = UIFonts.Pixel;
                    costT.rectTransform.pivot = new Vector2(1f, 0.5f);
                }

                // overlay de "no te entra en el turno" (tapa toda la carta)
                _cardOverlay[pos] = MakeImage(card.rectTransform, "Overlay", new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(CardW, CardH), new Color(0.02f, 0.02f, 0.03f, 0.78f));
                _cardOverlay[pos].gameObject.SetActive(false);

                // marca sutil de overflow: "»" a la derecha (la carta sigue
                // viéndose usable — cruzar no es un error, es una decisión)
                _cardOvfMark[pos] = MakeText(card.rectTransform, "Ovf", "»", new Vector2(1f, 0.5f), new Vector2(-14f, 0f),
                    new Vector2(22f, 26f), 20, new Color(1f, 0.6f, 0.15f, 0.95f), TextAnchor.MiddleCenter);
                _cardOvfMark[pos].fontStyle = FontStyle.Bold;
                _cardOvfMark[pos].gameObject.SetActive(false);
            }

            // LISTO y BORRAR apilados a la izquierda de la grilla
            float sideX = totalW / 2f + 96f;
            _doneBtn = MakeImage(rootRt, "DoneBtn", new Vector2(0.5f, 0f), new Vector2(-sideX, 118f), new Vector2(150f, 54f), DoneC);
            _doneLabel = MakeText(_doneBtn.rectTransform, "T", "¡LISTO!", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(146f, 30f), 16, Color.white, TextAnchor.MiddleCenter);
            _doneLabel.fontStyle = FontStyle.Bold;
            _undoBtn = MakeImage(rootRt, "UndoBtn", new Vector2(0.5f, 0f), new Vector2(-sideX, 62f), new Vector2(150f, 44f), UndoC);
            MakeText(_undoBtn.rectTransform, "T", "← BORRAR", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(146f, 30f), 14, Color.white, TextAnchor.MiddleCenter).fontStyle = FontStyle.Bold;

            // REGLA DE LAYOUT: todo el menú vive DEBAJO de y≈220 — arriba de
            // eso arrancan las timelines del HUD. Los botones contextuales van
            // en una SEGUNDA COLUMNA a la izquierda, nunca apilados hacia arriba.
            float sideX2 = sideX + 164f;

            // SUPER: botón dorado; late cuando la barra está llena. Solo
            // existe en turno fluido (la barra carga con overflow).
            _superBtn = MakeImage(rootRt, "SuperBtn", new Vector2(0.5f, 0f), new Vector2(-sideX2, 118f), new Vector2(160f, 54f), SuperDimC);
            _superLabel = MakeText(_superBtn.rectTransform, "T", "", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(156f, 46f), 13, Color.white, TextAnchor.MiddleCenter);
            _superLabel.fontStyle = FontStyle.Bold;
            _superBtn.gameObject.SetActive(false);

            // wakeup option: solo aparece si arrancás el turno derribado
            _wakeBtn = MakeImage(rootRt, "WakeBtn", new Vector2(0.5f, 0f), new Vector2(-sideX2, 62f), new Vector2(160f, 50f), WakeC);
            _wakeLabel = MakeText(_wakeBtn.rectTransform, "T", "", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(156f, 46f), 12, Color.white, TextAnchor.MiddleCenter);
            _wakeLabel.fontStyle = FontStyle.Bold;
            _wakeBtn.gameObject.SetActive(false);

            // panel de info a la DERECHA de la grilla: nombre, framedata con
            // mini-barra S/A/R, tag y descripción del movimiento hovereado.
            // Nunca tapa la timeline: vive en el espacio muerto del costado.
            var infoBg = MakeImage(rootRt, "InfoBg", new Vector2(0.5f, 0f), new Vector2(sideX + 96f, 26f + (totalH + 60f) / 2f),
                new Vector2(330f, totalH + 60f), new Color(0.04f, 0.05f, 0.07f, 0.94f));
            var ibr = infoBg.rectTransform;

            _detailTitle = MakeText(ibr, "Title", "", new Vector2(0f, 1f), new Vector2(14f, -18f),
                new Vector2(302f, 22f), 16, Color.white, TextAnchor.MiddleLeft);
            _detailTitle.font = UIFonts.Pixel;
            _detailTitle.rectTransform.pivot = new Vector2(0f, 0.5f);

            _detailFrames = MakeText(ibr, "Frames", "", new Vector2(0f, 1f), new Vector2(14f, -42f),
                new Vector2(302f, 20f), 14, new Color(0.95f, 0.88f, 0.55f), TextAnchor.MiddleLeft);
            _detailFrames.rectTransform.pivot = new Vector2(0f, 0.5f);

            // mini-barra S/A/R (amarillo/rojo/azul, mismo lenguaje del HUD)
            _segW = 302f;
            _segBg = MakeImage(ibr, "SegBg", new Vector2(0f, 1f), new Vector2(14f, -58f), new Vector2(_segW, 8f), new Color(0f, 0f, 0f, 0.55f));
            _segBg.rectTransform.pivot = new Vector2(0f, 0.5f);
            _segS = MakeImage(ibr, "SegS", new Vector2(0f, 1f), new Vector2(14f, -58f), new Vector2(0f, 8f), new Color(0.95f, 0.85f, 0.25f, 0.95f));
            _segS.rectTransform.pivot = new Vector2(0f, 0.5f);
            _segA = MakeImage(ibr, "SegA", new Vector2(0f, 1f), new Vector2(14f, -58f), new Vector2(0f, 8f), new Color(0.95f, 0.3f, 0.22f, 0.95f));
            _segA.rectTransform.pivot = new Vector2(0f, 0.5f);
            _segR = MakeImage(ibr, "SegR", new Vector2(0f, 1f), new Vector2(14f, -58f), new Vector2(0f, 8f), new Color(0.3f, 0.55f, 0.95f, 0.95f));
            _segR.rectTransform.pivot = new Vector2(0f, 0.5f);

            // rango de ventaja real: depende de en qué frame activo conecta
            _detailAdv = MakeText(ibr, "Adv", "", new Vector2(0f, 1f), new Vector2(14f, -74f),
                new Vector2(302f, 18f), 13, new Color(0.7f, 0.95f, 0.75f), TextAnchor.MiddleLeft);
            _detailAdv.rectTransform.pivot = new Vector2(0f, 0.5f);

            _detailTag = MakeText(ibr, "Tag", "", new Vector2(0f, 1f), new Vector2(14f, -94f),
                new Vector2(302f, 18f), 8, Color.white, TextAnchor.MiddleLeft);
            _detailTag.font = UIFonts.Pixel;
            _detailTag.rectTransform.pivot = new Vector2(0f, 0.5f);

            _detail = MakeText(ibr, "Desc", "", new Vector2(0f, 1f), new Vector2(14f, -106f),
                new Vector2(302f, 40f), 13, new Color(1f, 1f, 1f, 0.85f), TextAnchor.UpperLeft);
            _detail.rectTransform.pivot = new Vector2(0f, 1f);
            _detail.horizontalOverflow = HorizontalWrapMode.Wrap; // que envuelva, no que desborde

            // estado del plan, arriba de la grilla a la derecha
            _status = MakeText(rootRt, "Status", "", new Vector2(0.5f, 0f), new Vector2(totalW / 2f + 14f, 26f + totalH + 44f),
                new Vector2(900f, 22f), 14, new Color(0.5f, 1f, 0.6f), TextAnchor.MiddleRight);
            _status.rectTransform.pivot = new Vector2(1f, 0.5f);

            _gridPanelRt = panel.rectTransform; // el picker de dirección se posiciona relativo a la grilla
            _gridTotalH = totalH;
            MakeText(rootRt, "Help", _builtCards
                    ? "tu MANO: click (o 1-9/0) juega la carta · golpes por SPEED (empate al activo) · bloqueá la ALTURA correcta · AGARRE rompe bloqueos\nCAMBIO recupera cartas del descarte (2 por turno) · bloquear roba carta · el descarte rival es público"
                    : _builtYomi
                    ? "UNA acción por turno: click (o 1-8) la juega YA · de cerca: JAB › AGARRE › PARRY › JAB · el SHORYU gana pero whiffear = recovery\nlos AP no gastados se acumulan (tope 6) · CARGAR junta +2 pero todo golpe es counter"
                    : "click o 1-9 agrega  ·  Backspace borra  ·  ESPACIO cierra el turno  ·  DASH y SALTO preguntan la dirección\ncada carta cuesta AP y lo que no gastás SE GUARDA  ·  BLOQUEAR que bloquea un golpe banca +1 AP  ·  arrastrá tu timeline para mover el ghost",
                new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(1300f, 36f), 13, new Color(1f, 1f, 1f, 0.45f), TextAnchor.MiddleCenter);

            // en YOMI no hay cola: sin LISTO ni BORRAR (la carta se juega al click)
            if (_builtYomi)
            {
                _doneBtn.gameObject.SetActive(false);
                _undoBtn.gameObject.SetActive(false);
            }

            // en CARTAS: sin BORRAR; el botón grande es contextual
            // (CAMBIO / CANCELAR / PASAR el castigo) — RefreshCardsButtons manda
            if (_builtCards)
            {
                _undoBtn.gameObject.SetActive(false);
                _doneBtn.gameObject.SetActive(false);

                // picker de exchange: una fila de botones con las normales del
                // descarte, encima de la grilla; creado acá, se llena al abrir
                _exPanel = MakeImage(rootRt, "ExPicker", new Vector2(0.5f, 0f),
                    new Vector2(0f, 26f + totalH + 46f), new Vector2(400f, CardH + 18f), new Color(0.05f, 0.07f, 0.1f, 1f));
                for (int o = 0; o < _exBtns.Length; o++)
                {
                    _exBtns[o] = MakeImage(_exPanel.rectTransform, "Ex" + o, new Vector2(0.5f, 0.5f),
                        Vector2.zero, new Vector2(184f, CardH - 2f), new Color(0.16f, 0.2f, 0.28f, 1f));
                    _exLabels[o] = MakeText(_exBtns[o].rectTransform, "T", "", new Vector2(0.5f, 0.5f), Vector2.zero,
                        new Vector2(180f, 26f), 8, Color.white, TextAnchor.MiddleCenter);
                    _exLabels[o].font = UIFonts.Pixel;
                }
                _exPanel.gameObject.SetActive(false);
            }

            // mini-picker de dirección (DASH/SALTO): creado ÚLTIMO y colgado
            // del root — dibuja ENCIMA de todo (antes quedaba abajo del texto
            // de estado y parecía roto). Opaco: es un momento modal.
            if (!_builtYomi)
            {
                _subPanel = MakeImage(rootRt, "SubPicker", new Vector2(0.5f, 0f), Vector2.zero,
                    new Vector2(3 * 154f + 14f, CardH + 18f), new Color(0.05f, 0.07f, 0.1f, 1f));
                for (int o = 0; o < 3; o++)
                {
                    _subBtn[o] = MakeImage(_subPanel.rectTransform, "Opt" + o, new Vector2(0.5f, 0.5f),
                        Vector2.zero, new Vector2(148f, CardH - 2f), new Color(0.16f, 0.2f, 0.28f, 1f));
                    _subLabel[o] = MakeText(_subBtn[o].rectTransform, "T", "", new Vector2(0.5f, 0.5f), Vector2.zero,
                        new Vector2(146f, 26f), 8, Color.white, TextAnchor.MiddleCenter);
                    _subLabel[o].font = UIFonts.Pixel;
                }
                _subPanel.gameObject.SetActive(false);
            }
        }

        // Rango de ventaja REAL: la ventaja depende de en qué frame activo
        // conecta el golpe (contacto tardío = más ventaja, el recovery del
        // atacante es fijo). Un solo número mentía.
        static string AdvRange(MoveDef m)
        {
            if (m.Hits.Length == 0) return "";
            int hMin = int.MaxValue, hMax = int.MinValue, bMin = int.MaxValue, bMax = int.MinValue;
            bool kd = false, grab = false;
            foreach (var h in m.Hits)
            {
                int first = h.Start, last = h.Start + h.Duration - 1;
                hMin = Mathf.Min(hMin, h.Hitstun - (m.Total - first));
                hMax = Mathf.Max(hMax, h.Hitstun - (m.Total - last));
                if (h.IsGrab) grab = true;
                else
                {
                    bMin = Mathf.Min(bMin, h.Blockstun - (m.Total - first));
                    bMax = Mathf.Max(bMax, h.Blockstun - (m.Total - last));
                }
                kd |= h.Knockdown;
            }
            string S(int v) => v >= 0 ? $"+{v}" : $"−{-v}";
            string R(int a, int b) => a == b ? S(a) : $"{S(a)}…{S(b)}";
            string res = (kd ? "KD · " : "") + $"HIT {R(hMin, hMax)}";
            if (bMin != int.MaxValue) res += $"   BLOCK {R(bMin, bMax)}";
            else if (grab) res += "   NO BLOQUEABLE";
            return res;
        }

        public void Open(int picker)
        {
            // CARTAS: la mano cambia cada turno → grilla nueva siempre
            if (SimConfig.CardsEnabled || _builtYomi != SimConfig.YomiEnabled ||
                _builtCards != SimConfig.CardsEnabled) Rebuild();
            _root.SetActive(true);
            _active = true;
            RefreshWake();
            Highlight(_sel);
        }

        // CARTAS: se abre para elegir el castigo (dodge a strike / unsafe
        // bloqueado) — la mano ya cambió (el opener salió), grilla nueva.
        public void OpenCardsPunish()
        {
            Rebuild();
            _punishMode = true;
            _root.SetActive(true);
            _active = true;
            Highlight(0);
        }

        void CancelExchange()
        {
            _exMode = false;
            _exGive = -1;
            if (_exPanel != null) _exPanel.gameObject.SetActive(false);
            RefreshCardStates();
        }

        void RefreshWake()
        {
            bool avail = _mc.WakeupAvailable(_mc.Picker);
            _wakeBtn.gameObject.SetActive(avail);
            if (!avail) return;
            _wakeLabel.text = _mc.WakeReversalChoice(_mc.Picker)
                ? $"WAKEUP: ¡REVERSAL!\n<size=11>{ApEconomy.ReversalCost} AP · 1 por round · parás YA y separa</size>"
                : _mc.WakeQuickChoice(_mc.Picker)
                    ? $"WAKEUP: RÁPIDO\n<size=11>{MatchController.WakeQuickDelta}f de knockdown</size>"
                    : $"WAKEUP: QUEDARSE\n<size=11>+{MatchController.WakeStayDelta}f, baitea el meaty</size>"
                      + (_mc.ReversalSelectable(_mc.Picker) ? "\n<size=10>otro click: REVERSAL</size>" : "");
        }

        public void Close()
        {
            _active = false;
            CloseSubPicker();
            if (_root != null) _root.SetActive(false);
            RangePreview.Clear();
        }

        public void SetPrediction(PlanPreview g, int framesUsed, int available)
        {
            string extra = "";
            if (g.DamageIfStill > 0f) extra += $"  ·  pegaría {g.DamageIfStill:0} si no reacciona";
            if (g.BlockedCount > 0) extra += $"  ·  {g.BlockedCount} bloqueado(s) si se queda en neutral";

            if (SimConfig.ApActive && !_builtYomi)
            {
                // presupuesto en ACTION POINTS: las bolitas viven en el HUD
                // (bajo las barras de cada lado) — acá el texto acompaña
                int apUsed = _mc.PlanApUsed(_mc.Picker);
                int apAvail = _mc.PlanApAvailable(_mc.Picker);
                int apLeft = Mathf.Max(0, apAvail - apUsed);
                int stock = _mc.ApStock(_mc.Picker);
                int slots = _mc.PlanFramesAvailable(_mc.Picker) / SimConfig.FramesPerAp;
                string note = _mc.WakeReversalChoice(_mc.Picker) ? $" (REVERSAL: −{ApEconomy.ReversalCost} AP)"
                    : slots < stock && slots < _mc.ApPerTurn ? $" (el stun te dejó {slots} slots)"
                    : stock < _mc.ApPerTurn ? " (stock corto: gastaste de más)" : "";
                if (apLeft > 0) extra += $"  ·  lo que no gastás SE GUARDA (tope {_mc.ApStockCap})";
                _status.text = $"{apUsed}/{apAvail} AP{note} — quedan {apLeft}{extra}";
                _status.color = apLeft == 0 ? new Color(1f, 0.85f, 0.3f)
                    : apAvail < _mc.ApPerTurn ? new Color(1f, 0.65f, 0.4f) : new Color(0.5f, 1f, 0.6f);
            }
            else
            {
                int over = Mathf.Max(0, framesUsed - available);   // frames que cruzan al próximo turno
                int left = Mathf.Max(0, available - framesUsed);
                int turnFrames = _mc.CurrentTurnFrames; // en Lag Mode el turno crece
                int committed = _mc.TurnStartCommitted[_mc.Picker];
                string stunNote = committed > 0
                    ? $" ({committed}f ya comprometidos en {MoveCatalog.All[_mc.Sim.Fighters[_mc.Picker].MoveIndex].Name})"
                    : available < turnFrames ? $" (perdés {turnFrames - available}f por el stun)" : "";
                if (over > 0) extra += $"  ·  » el último move CRUZA {over}f al próximo turno (quedás comprometido)";
                _status.text = $"{framesUsed}/{available} frames planificados{stunNote} — quedan {left}{extra}";
                _status.color = over > 0 ? new Color(1f, 0.6f, 0.15f)
                    : left == 0 ? new Color(1f, 0.85f, 0.3f)
                    : available < SimConfig.TurnFrames ? new Color(1f, 0.65f, 0.4f) : new Color(0.5f, 1f, 0.6f);
            }

            // sin órdenes, confirmar es jugada válida (quieto bloqueando):
            // que el botón lo diga, no que parezca un LISTO en falso
            bool empty = framesUsed == 0;
            _doneLabel.text = empty ? "PASAR\n<size=11>(quieto, bloquea)</size>" : "¡LISTO!";
            _doneLabel.fontSize = empty ? 14 : 16;

            RefreshCardStates(); // el plan cambió: gris/overflow de cada carta
        }

        // Estado visual de cada carta según lo que queda del turno:
        // - gris oscuro: no entra (estricto) o ni siquiera arranca (fluido)
        // - franja naranja + "»": entra pero CRUZARÍA el turno — sutil, no
        //   parece prohibido (cruzar es una decisión válida, no un error)
        void RefreshCardStates()
        {
            if (_cardOverlay == null) return;
            if (_builtCards)
            {
                var cs = _mc.Cards;
                for (int i = 0; i < _order.Length; i++)
                {
                    bool ok;
                    var d = CardCatalog.All[_order[i]];
                    if (_punishMode) ok = d.Kind == CardKind.Attack || d.Kind == CardKind.Throw;
                    else if (_exMode) ok = d.IsNormal && i != _exGive;
                    else ok = cs != null && (cs.LegalOpener(0, i) || !cs.HasLegalOpener(0));
                    _cardOverlay[i].gameObject.SetActive(!ok);
                    _cardOvfMark[i].gameObject.SetActive(false);
                    var cc = CardColor(_order[i]);
                    _cardEdge[i].color = new Color(cc.r, cc.g, cc.b, 0.9f);
                }
                RefreshCardsButtons();
                return;
            }
            if (_builtYomi)
            {
                // legalidad de la matriz: distancia + AP + recovery
                var y = _mc.Yomi;
                for (int i = 0; i < _order.Length; i++)
                {
                    bool ok = y != null && y.Legal(0, (YomiAction)_order[i]);
                    _cardOverlay[i].gameObject.SetActive(!ok);
                    _cardOvfMark[i].gameObject.SetActive(false);
                    var cc = CardColor(_order[i]);
                    _cardEdge[i].color = new Color(cc.r, cc.g, cc.b, 0.9f);
                }
                return;
            }
            int used = _mc.PlanFramesUsed(_mc.Picker);
            int avail = _mc.PlanFramesAvailable(_mc.Picker);
            int usedAp = _mc.PlanApUsed(_mc.Picker);
            int availAp = _mc.PlanApAvailable(_mc.Picker);
            for (int i = 0; i < _order.Length; i++)
            {
                int rep = Rep(_order[i]); // las cartas de grupo validan por su representante
                bool startable = _mc.PlanFits(rep);
                // "cruzaría el turno" solo existe con overflow habilitado
                bool crosses = startable && (SimConfig.ApActive
                    ? SimConfig.ApOverflowEnabled && usedAp + MoveCatalog.All[rep].ApCost > availAp
                    : SimConfig.CarryoverEnabled && used + MoveCatalog.All[rep].Total > avail);
                _cardOverlay[i].gameObject.SetActive(!startable);
                _cardOvfMark[i].gameObject.SetActive(crosses);
                var cat = CategoryColor(rep);
                _cardEdge[i].color = crosses
                    ? new Color(1f, 0.6f, 0.15f, 0.95f)
                    : new Color(cat.r, cat.g, cat.b, 0.9f);
            }
        }

        // botón contextual del modo CARTAS: CAMBIO / CANCELAR / PASAR castigo
        void RefreshCardsButtons()
        {
            if (!_builtCards || _doneBtn == null) return;
            var cs = _mc.Cards;
            bool show = _punishMode || _exMode ||
                (cs != null && !cs.Over && cs.Active == 0 && cs.ExchangesLeft > 0);
            if (_doneBtn.gameObject.activeSelf != show) _doneBtn.gameObject.SetActive(show);
            if (!show) return;
            _doneLabel.text = _punishMode ? "PASAR\n<size=11>(no castigar)</size>"
                : _exMode ? "CANCELAR\n<size=11>el cambio</size>"
                : $"CAMBIO ×{cs.ExchangesLeft}\n<size=11>con el descarte</size>";
            _doneLabel.fontSize = 14;
        }

        void Highlight(int pos)
        {
            int n = _order.Length;
            if (n == 0) return; // mano vacía: no hay nada que resaltar
            _sel = ((pos % n) + n) % n;
            for (int i = 0; i < n; i++)
            {
                bool sel = i == _sel;
                _cardBg[i].color = sel ? new Color(0.22f, 0.3f, 0.42f, 1f) : new Color(0.12f, 0.13f, 0.17f, 0.98f);
                _cardName[i].color = sel ? Color.white : new Color(1f, 1f, 1f, 0.85f);
            }
            RefreshCardStates();

            int mi = _order[_sel];
            var cat = CardColor(mi);
            _detailTitle.text = DisplayName(mi);
            _detailTitle.color = new Color(cat.r * 0.5f + 0.5f, cat.g * 0.5f + 0.5f, cat.b * 0.5f + 0.5f);

            // panel de info CARTAS: speed/daño/altura + la fila de la tabla
            if (_builtCards)
            {
                _detailFrames.text = HudUI.CardIdInfo(mi);
                _segS.rectTransform.sizeDelta = new Vector2(0f, 8f);
                _segA.rectTransform.sizeDelta = new Vector2(0f, 8f);
                _segR.rectTransform.sizeDelta = new Vector2(0f, 8f);
                bool mine = _mc.Cards != null && _mc.Cards.Active == 0;
                _detailAdv.text = mine ? "TU TURNO: ganás los empates de speed" : "TURNO RIVAL: gana los empates de speed";
                _detailTag.text = CardsTag(mi);
                _detailTag.color = new Color(cat.r * 0.6f + 0.4f, cat.g * 0.6f + 0.4f, cat.b * 0.6f + 0.4f);
                _detail.text = CardsDesc(mi);
                _status.text = _punishMode ? "¡CASTIGO! elegí un golpe o agarre · ESPACIO pasa"
                    : _exMode ? (_exGive < 0 ? "CAMBIO: elegí qué carta SOLTAR · Backspace cancela"
                                             : "CAMBIO: elegí qué RECUPERAR del descarte")
                    : "elegí UNA carta: click la juega YA · el rival ya eligió en secreto";
                _status.color = _punishMode ? new Color(1f, 0.55f, 0.9f) : new Color(0.5f, 1f, 0.6f);
                return;
            }

            // panel de info YOMI: costo, daño, y la fila de la matriz en la
            // distancia actual — sin framedata (acá no existen los frames)
            if (_builtYomi)
            {
                var act = (YomiAction)mi;
                int cost = YomiConfig.Cost(act);
                int dmgY = YomiConfig.Damage(act);
                _detailFrames.text = (cost == 0 ? "GRATIS" : $"CUESTA {cost} AP") +
                                     (dmgY > 0 ? $"   ·   {dmgY} DMG" : "");
                _segS.rectTransform.sizeDelta = new Vector2(0f, 8f);
                _segA.rectTransform.sizeDelta = new Vector2(0f, 8f);
                _segR.rectTransform.sizeDelta = new Vector2(0f, 8f);
                bool close = _mc.Yomi != null && _mc.Yomi.Close;
                _detailAdv.text = close ? "DISTANCIA ACTUAL: CERCA" : "DISTANCIA ACTUAL: LEJOS";
                _detailTag.text = YomiTag(act, close);
                _detailTag.color = new Color(cat.r * 0.6f + 0.4f, cat.g * 0.6f + 0.4f, cat.b * 0.6f + 0.4f);
                _detail.text = YomiDesc(act);
                _status.text = "elegí UNA acción: click la juega ya · el rival ya eligió en secreto";
                _status.color = new Color(0.5f, 1f, 0.6f);
                return;
            }

            // panel de info clásico: costo en AP + framedata con mini-barra
            // S/A/R (las cartas de grupo muestran su variante representativa)
            var m = MoveCatalog.All[Rep(mi)];
            string dmg = m.TotalDamage > 0f ? $"   ·   {m.TotalDamage:0} DMG" + (m.Hits.Length > 1 ? $" ({m.Hits.Length} hits)" : "") : "";
            string ap = SimConfig.ApActive ? $"{m.ApCost} AP   ·   " : "";
            _detailFrames.text = $"{ap}{m.Startup} / {m.Active} / {m.Recovery}  ·  {m.Total}f{dmg}";

            float px = _segW / m.Total;
            _segS.rectTransform.sizeDelta = new Vector2(m.Startup * px, 8f);
            _segA.rectTransform.anchoredPosition = new Vector2(14f + m.Startup * px, -58f);
            _segA.rectTransform.sizeDelta = new Vector2(m.Active * px, 8f);
            _segR.rectTransform.anchoredPosition = new Vector2(14f + (m.Startup + m.Active) * px, -58f);
            _segR.rectTransform.sizeDelta = new Vector2(m.Recovery * px, 8f);

            _detailAdv.text = AdvRange(m);
            _detailTag.text = CardTag(mi).ToUpperInvariant();
            _detailTag.color = new Color(cat.r * 0.6f + 0.4f, cat.g * 0.6f + 0.4f, cat.b * 0.6f + 0.4f);
            _detail.text = mi == CardDash ? "Arremetida sin bloqueo: al apretarla elegís ADELANTE (presión) o ATRÁS (el bait)."
                : mi == CardJump ? "Salto con patada al caer: al apretarla elegís ADELANTE (jump-in), NEUTRO (wakeup) o ATRÁS (retirada)."
                : m.Desc;

            // el rango del movimiento se dibuja EN el escenario (Into the Breach)
            RangePreview.Show(_mc.Sim, _mc.Picker, Rep(_order[_sel]));
            // y el ghost lo ACTÚA: plan actual + la carta bajo el cursor
            _mc.PreviewHover(Rep(_order[_sel]));
        }

        void Update()
        {
            if (!_active) return;

            // el mini-picker de dirección abierto captura todo el input
            if (_subGroup != 0) { UpdateSubPicker(); return; }

            // ídem el picker de exchange del modo CARTAS
            if (_builtCards && _exMode && _exGive >= 0) { UpdateExchangePicker(); return; }

            // hover: pasar el mouse por una carta ya muestra qué hace,
            // sin tener que apretarla (apretar = agregarla al plan)
            var mp = GameInput.MousePos();
            if ((mp - _lastMouse).sqrMagnitude > 4f)
            {
                _lastMouse = mp;
                int hover = -1;
                for (int i = 0; i < _cardRt.Length; i++)
                    if (RectTransformUtility.RectangleContainsScreenPoint(_cardRt[i], mp, null)) { hover = i; break; }
                if (hover >= 0)
                {
                    if (hover != _sel) SfxLib.Play(SfxLib.Kind.UiTick, 0.3f);
                    if (hover != _sel || !_mouseOnCards) Highlight(hover); // re-entrar re-arma el preview
                }
                else if (_mouseOnCards)
                {
                    // salió de la grilla (p.ej. hacia ¡LISTO!/PASAR): el ghost
                    // muestra SOLO el plan — nada de la última carta hovereada
                    _mc.PreviewHover(-1);
                    RangePreview.Clear();
                }
                _mouseOnCards = hover >= 0;
            }

            // tinte de hover en los botones laterales
            _doneBtn.color = HoverTint(_doneBtn, DoneC, mp);
            _undoBtn.color = HoverTint(_undoBtn, UndoC, mp);
            if (_wakeBtn.gameObject.activeSelf) _wakeBtn.color = HoverTint(_wakeBtn, WakeC, mp);
            RefreshSuper();

            if (GameInput.ClickPressed())
            {
                var pos = GameInput.MousePos();
                for (int i = 0; i < _cardRt.Length; i++)
                {
                    if (!RectTransformUtility.RectangleContainsScreenPoint(_cardRt[i], pos, null)) continue;
                    if (_builtCards) { CardsClick(i); return; }  // la mano manda
                    if (_builtYomi) { TryPlayYomi(i); return; } // una acción = el turno entero
                    TryAdd(_order[i]);
                    Highlight(i);
                    return;
                }
                if (_undoBtn.gameObject.activeSelf &&
                    RectTransformUtility.RectangleContainsScreenPoint(_undoBtn.rectTransform, pos, null))
                {
                    TryUndo();
                    Highlight(_sel);
                    return;
                }
                if (_doneBtn.gameObject.activeSelf &&
                    RectTransformUtility.RectangleContainsScreenPoint(_doneBtn.rectTransform, pos, null))
                {
                    SfxLib.Play(SfxLib.Kind.UiClick, 0.8f);
                    if (_builtCards) { CardsContextButton(); return; }
                    _mc.PlanConfirm();
                    return;
                }
                if (_wakeBtn.gameObject.activeSelf &&
                    RectTransformUtility.RectangleContainsScreenPoint(_wakeBtn.rectTransform, pos, null))
                {
                    SfxLib.Play(SfxLib.Kind.UiClick, 0.7f);
                    _mc.ToggleWakeup();
                    RefreshWake();
                    Highlight(_sel); // el presupuesto de frames pudo cambiar
                    return;
                }
                if (_superBtn.gameObject.activeSelf &&
                    RectTransformUtility.RectangleContainsScreenPoint(_superBtn.rectTransform, pos, null))
                {
                    if (_mc.PlanFits(MoveCatalog.Super)) { SfxLib.Play(SfxLib.Kind.UiClick, 0.9f); _mc.PlanAdd(MoveCatalog.Super); }
                    else SfxLib.Play(SfxLib.Kind.UiCancel, 0.5f);
                    return;
                }
            }

            if (GameInput.LeftPressed()) { Highlight(_sel - 1); SfxLib.Play(SfxLib.Kind.UiTick, 0.3f); }
            if (GameInput.RightPressed()) { Highlight(_sel + 1); SfxLib.Play(SfxLib.Kind.UiTick, 0.3f); }
            if (GameInput.UpPressed()) { Highlight(_sel - _cols); SfxLib.Play(SfxLib.Kind.UiTick, 0.3f); }
            if (GameInput.DownPressed()) { Highlight(_sel + _cols); SfxLib.Play(SfxLib.Kind.UiTick, 0.3f); }
            int num = GameInput.NumberPressed();
            if (num > 0 && num <= _order.Length)
            {
                if (_builtCards) { Highlight(num - 1); CardsClick(num - 1); }
                else if (_builtYomi) { Highlight(num - 1); TryPlayYomi(num - 1); }
                else { TryAdd(_order[num - 1]); Highlight(num - 1); }
            }
            else if (GameInput.AddPressed())
            {
                if (_builtCards) { CardsClick(_sel); return; }
                if (_builtYomi) { TryPlayYomi(_sel); return; }
                TryAdd(_order[_sel]);
                Highlight(_sel);
            }
            if (_builtCards)
            {
                // Backspace cancela el cambio · ESPACIO pasa el castigo
                if (GameInput.UndoPressed() && _exMode) { CancelExchange(); Highlight(_sel); }
                if (GameInput.EndTurnPressed() && _punishMode) _mc.CardsPunish(-1);
                return;
            }
            if (_builtYomi) return; // sin cola: no hay BORRAR ni cerrar turno aparte
            if (GameInput.UndoPressed()) { TryUndo(); Highlight(_sel); }
            if (GameInput.EndTurnPressed()) _mc.PlanConfirm();
        }

        // ---- interacción del modo CARTAS ----

        void CardsContextButton()
        {
            if (_punishMode) { _mc.CardsPunish(-1); return; }
            if (_exMode) { CancelExchange(); Highlight(_sel); return; }
            _exMode = true;
            _exGive = -1;
            RefreshCardStates();
            Highlight(_sel);
        }

        void CardsClick(int idx)
        {
            if (idx < 0 || idx >= _order.Length) return;
            var d = CardCatalog.All[_order[idx]];
            if (_punishMode)
            {
                bool okP = d.Kind == CardKind.Attack || d.Kind == CardKind.Throw;
                SfxLib.Play(okP ? SfxLib.Kind.UiClick : SfxLib.Kind.UiCancel, okP ? 0.8f : 0.5f);
                if (okP) _mc.CardsPunish(idx);
                return;
            }
            if (_exMode)
            {
                if (!d.IsNormal) { SfxLib.Play(SfxLib.Kind.UiCancel, 0.5f); return; }
                _exGive = idx;
                OpenExchangePicker();
                return;
            }
            var cs = _mc.Cards;
            bool ok = cs != null && (cs.LegalOpener(0, idx) || !cs.HasLegalOpener(0));
            SfxLib.Play(ok ? SfxLib.Kind.UiClick : SfxLib.Kind.UiCancel, ok ? 0.8f : 0.5f);
            if (ok) _mc.CardsPick(idx);
        }

        // Fila con las normales del DESCARTE propio (con conteo): elegir una
        // completa el cambio; click afuera o Backspace cancela.
        void OpenExchangePicker()
        {
            var disc = _mc.Cards.Discard[0];
            _exCount = 0;
            for (int c = 0; c < CardCatalog.All.Length && _exCount < _exBtns.Length; c++)
            {
                if (!CardCatalog.All[c].IsNormal) continue;
                int n = 0;
                foreach (int x in disc) if (x == c) n++;
                if (n == 0) continue;
                _exCards[_exCount] = c;
                _exLabels[_exCount].text = n > 1 ? $"{CardCatalog.All[c].Name} ×{n}" : CardCatalog.All[c].Name;
                _exCount++;
            }
            if (_exCount == 0)
            {
                SfxLib.Play(SfxLib.Kind.UiCancel, 0.5f);
                CancelExchange();
                Highlight(_sel);
                return;
            }
            float w = 190f;
            for (int o = 0; o < _exBtns.Length; o++)
            {
                bool on = o < _exCount;
                _exBtns[o].gameObject.SetActive(on);
                if (on) _exBtns[o].rectTransform.anchoredPosition = new Vector2((o - (_exCount - 1) * 0.5f) * w, 0f);
            }
            _exPanel.rectTransform.sizeDelta = new Vector2(_exCount * w + 14f, CardH + 18f);
            _exPanel.rectTransform.anchoredPosition = new Vector2(0f, 26f + _gridTotalH + 46f);
            _exPanel.gameObject.SetActive(true);
            RefreshCardStates();
            SfxLib.Play(SfxLib.Kind.UiTick, 0.5f);
        }

        void UpdateExchangePicker()
        {
            var mp = GameInput.MousePos();
            for (int o = 0; o < _exCount; o++)
                _exBtns[o].color = RectTransformUtility.RectangleContainsScreenPoint(_exBtns[o].rectTransform, mp, null)
                    ? new Color(0.26f, 0.34f, 0.46f, 1f) : new Color(0.16f, 0.2f, 0.28f, 1f);
            if (GameInput.UndoPressed()) { CancelExchange(); Highlight(_sel); return; }
            if (!GameInput.ClickPressed()) return;
            for (int o = 0; o < _exCount; o++)
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(_exBtns[o].rectTransform, mp, null)) continue;
                int discIdx = _mc.Cards.Discard[0].IndexOf(_exCards[o]);
                int give = _exGive;
                CancelExchange();
                if (discIdx >= 0 && _mc.CardsExchange(give, discIdx))
                {
                    // la mano cambió: grilla nueva, mismo turno
                    Rebuild();
                    _root.SetActive(true);
                    _active = true;
                    Highlight(0);
                }
                return;
            }
            // click afuera: cancela el cambio
            CancelExchange();
            Highlight(_sel);
        }

        // YOMI: jugar la carta seleccionada resuelve el turno entero
        void TryPlayYomi(int idx)
        {
            var act = (YomiAction)_order[idx];
            bool ok = _mc.Yomi != null && _mc.Yomi.Legal(0, act);
            SfxLib.Play(ok ? SfxLib.Kind.UiClick : SfxLib.Kind.UiCancel, ok ? 0.8f : 0.5f);
            if (ok) _mc.YomiPick(act);
        }

        // botón SUPER: porcentaje mientras carga, dorado latiendo cuando está lista
        // (en YOMI no hay super: el overflow es parte del flujo normal)
        void RefreshSuper()
        {
            bool show = SimConfig.FluidTurn && !SimConfig.YomiEnabled;
            if (_superBtn.gameObject.activeSelf != show) _superBtn.gameObject.SetActive(show);
            if (!show) return;
            var fs = _mc.Sim.Fighters[_mc.Picker];
            bool full = fs.Super >= SimConfig.SuperMax;
            if (full)
            {
                bool fits = _mc.PlanFits(MoveCatalog.Super);
                _superLabel.text = fits
                    ? "SHINKU HADOUKEN\n<size=10>click: a la cola (56f)</size>"
                    : "SHINKU HADOUKEN\n<size=10>ya en el plan / sin frames</size>";
                _superBtn.color = Color.Lerp(new Color(0.85f, 0.62f, 0.1f, 0.98f),
                    new Color(1f, 0.9f, 0.45f, 1f), Mathf.PingPong(Time.time * 2.6f, 1f));
            }
            else
            {
                _superLabel.text = $"SUPER {fs.Super * 100 / SimConfig.SuperMax}%\n<size=10>se carga con OVERFLOW</size>";
                _superBtn.color = SuperDimC;
            }
        }

        static Color HoverTint(Image btn, Color baseC, Vector2 mp)
            => RectTransformUtility.RectangleContainsScreenPoint(btn.rectTransform, mp, null)
                ? Color.Lerp(baseC, Color.white, 0.22f) : baseC;

        // agrega/borra con su blip solo si la acción realmente pasó.
        // Las cartas de grupo (DASH/SALTO) abren el picker de dirección.
        void TryAdd(int mi)
        {
            if (mi == CardDash || mi == CardJump) { OpenSubPicker(mi); return; }
            // que apretar una carta que no entra NUNCA se sienta muerto
            if (_mc.PlanFits(mi)) SfxLib.Play(SfxLib.Kind.UiClick, 0.6f);
            else SfxLib.Play(SfxLib.Kind.UiCancel, 0.4f);
            _mc.PlanAdd(mi);
        }

        void OpenSubPicker(int group)
        {
            if (!_mc.PlanFits(Rep(group))) { SfxLib.Play(SfxLib.Kind.UiCancel, 0.5f); return; }
            var opts = group == CardDash ? DashOptions : JumpOptions;
            _subGroup = group;
            _subCount = opts.Length;
            // encima de la carta del grupo: grilla y picker cuelgan del mismo
            // root (anclas bottom-center), así que las posiciones se suman
            int pos = System.Array.IndexOf(_order, group);
            _subPanel.rectTransform.anchoredPosition =
                _gridPanelRt.anchoredPosition + _cardRt[pos].anchoredPosition + new Vector2(0f, CardH + 14f);
            _subPanel.rectTransform.sizeDelta = new Vector2(_subCount * 154f + 14f, CardH + 18f);
            for (int o = 0; o < 3; o++)
            {
                bool on = o < _subCount;
                _subBtn[o].gameObject.SetActive(on);
                if (!on) continue;
                _subMoves[o] = opts[o].move;
                _subLabel[o].text = $"{o + 1}  {opts[o].label}";
                _subBtn[o].rectTransform.anchoredPosition = new Vector2((o - (_subCount - 1) * 0.5f) * 154f, 0f);
            }
            _subPanel.gameObject.SetActive(true);
            _mc.PreviewHover(_subMoves[0]); // el ghost arranca actuando ADELANTE
            SfxLib.Play(SfxLib.Kind.UiTick, 0.5f);
        }

        void CloseSubPicker()
        {
            _subGroup = 0;
            if (_subPanel != null) _subPanel.gameObject.SetActive(false);
        }

        void UpdateSubPicker()
        {
            var mp = GameInput.MousePos();
            int hovered = -1;
            for (int o = 0; o < _subCount; o++)
            {
                bool over = RectTransformUtility.RectangleContainsScreenPoint(_subBtn[o].rectTransform, mp, null);
                _subBtn[o].color = over ? new Color(0.28f, 0.38f, 0.52f, 1f) : new Color(0.16f, 0.2f, 0.28f, 1f);
                if (over) hovered = o;
            }
            // el ghost Y el rango SIEMPRE actúan una variante concreta (sin
            // hover: la 1ra — antes quedaba lo último hovereado y "andaba mal")
            int shown = _subMoves[hovered >= 0 ? hovered : 0];
            _mc.PreviewHover(shown);
            RangePreview.Show(_mc.Sim, _mc.Picker, shown);
            int num = GameInput.NumberPressed();
            if (num > 0 && num <= _subCount) { PickSub(num - 1); return; }
            if (GameInput.AddPressed()) { PickSub(0); return; } // Enter = adelante
            if (GameInput.UndoPressed()) { CloseSubPicker(); Highlight(_sel); return; }
            if (!GameInput.ClickPressed()) return;
            for (int o = 0; o < _subCount; o++)
                if (RectTransformUtility.RectangleContainsScreenPoint(_subBtn[o].rectTransform, mp, null))
                {
                    PickSub(o);
                    return;
                }
            CloseSubPicker(); // click afuera = cancelar
            Highlight(_sel);  // y el preview vuelve a la selección
        }

        void PickSub(int o)
        {
            int mv = _subMoves[o];
            CloseSubPicker();
            SfxLib.Play(_mc.PlanFits(mv) ? SfxLib.Kind.UiClick : SfxLib.Kind.UiCancel, 0.6f);
            _mc.PlanAdd(mv);
            Highlight(_sel);
        }

        void TryUndo()
        {
            if (_mc.GetPlan(_mc.Picker).Count > 0) SfxLib.Play(SfxLib.Kind.UiCancel, 0.7f);
            _mc.PlanUndo();
        }

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

        Text MakeText(RectTransform parent, string name, string content, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize, Color color, TextAnchor align)
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
