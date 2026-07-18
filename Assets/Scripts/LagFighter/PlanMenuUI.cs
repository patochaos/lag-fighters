using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LagFighter
{
    // Menú de planificación del turno (tiempo pausado). Grilla 7x2:
    // fila de arriba = movimiento, fila de abajo = ataques y defensa.
    // Cada carta lleva una franja de color por categoría y una mini-barra
    // de framedata (startup amarillo / activo rojo / recovery azul), el
    // mismo lenguaje visual que la barra de fases del HUD.
    // Click o 1-9/0 agrega · ←→↑↓ + Enter/J agrega · Backspace borra ·
    // Espacio (o botón ¡LISTO!) cierra el turno.
    public class PlanMenuUI : MonoBehaviour
    {
        const int Cols = 7;
        const float CardW = 168f, CardH = 44f, Gap = 6f;

        // orden de display: movimiento arriba, acción abajo
        static readonly int[] Order =
        {
            MoveCatalog.WalkF, MoveCatalog.WalkB, MoveCatalog.DashF, MoveCatalog.DashB,
            MoveCatalog.JumpF, MoveCatalog.JumpN, MoveCatalog.JumpB,
            MoveCatalog.AttackA, MoveCatalog.AttackB, MoveCatalog.Tatsu, MoveCatalog.Hadouken,
            MoveCatalog.Shoryuken, MoveCatalog.Grab, MoveCatalog.Parry,
            // agachado desactivado — reactivar junto con SimConfig.CrouchEnabled:
            // MoveCatalog.LowKick, MoveCatalog.Crouch,
        };

        MatchController _mc;
        Font _font;
        GameObject _root;
        Image[] _cardBg, _cardEdge, _cardOverlay;
        Text[] _cardName;
        RectTransform[] _cardRt;
        Image _undoBtn, _doneBtn, _wakeBtn;
        Text _wakeLabel, _doneLabel;
        // hover: tinte sobre el color base de cada botón
        static readonly Color DoneC = new Color(0.18f, 0.45f, 0.22f, 0.95f);
        static readonly Color UndoC = new Color(0.4f, 0.2f, 0.2f, 0.95f);
        static readonly Color WakeC = new Color(0.5f, 0.32f, 0.1f, 0.95f);
        // panel de info a la derecha de la grilla: se llena con el hover
        Text _detailTitle, _detailFrames, _detailAdv, _detailTag, _detail, _status;
        Image _segBg, _segS, _segA, _segR;
        float _segW;
        int _sel;
        bool _active;
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
            ui.Build(go.GetComponent<RectTransform>());
            ui._root.SetActive(false);
            return ui;
        }

        static Color CategoryColor(int mi)
        {
            var m = MoveCatalog.All[mi];
            if (mi == MoveCatalog.Grab) return new Color(0.85f, 0.3f, 0.75f);
            if (mi == MoveCatalog.Shoryuken) return new Color(0.95f, 0.7f, 0.15f);
            if (mi == MoveCatalog.Hadouken) return new Color(0.3f, 0.55f, 0.95f);
            if (mi == MoveCatalog.Tatsu) return new Color(0.9f, 0.45f, 0.15f);
            if (m.Hits.Length > 0 && m.HasAir) return new Color(0.55f, 0.8f, 0.35f);
            if (m.IsAttack) return new Color(0.9f, 0.32f, 0.24f);
            if (m.HasAir) return new Color(0.55f, 0.8f, 0.35f);
            if (mi == MoveCatalog.Parry || mi == MoveCatalog.WalkB || mi == MoveCatalog.Crouch) return new Color(0.35f, 0.55f, 0.85f);
            return new Color(0.3f, 0.7f, 0.45f);
        }

        static string CardTag(int mi)
        {
            var m = MoveCatalog.All[mi];
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
                case MoveCatalog.JumpB: return "sobre hadoukens · −1.9";
                case MoveCatalog.AttackA: return "+2 hit / −5 block";
                case MoveCatalog.AttackB: return "DERRIBA · −10 block";
                case MoveCatalog.DashF: return "cierra distancia · no bloquea";
                case MoveCatalog.DashB: return "el bait · no bloquea";
                default: return "avanza · no bloquea";
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

            int rows = Mathf.CeilToInt(Order.Length / (float)Cols);
            float totalW = Cols * (CardW + Gap) - Gap;
            float totalH = rows * (CardH + Gap) - Gap;

            var panel = MakeImage(rootRt, "Panel", new Vector2(0.5f, 0f), new Vector2(0f, 26f + totalH / 2f + 12f),
                new Vector2(totalW + 28f, totalH + 28f), new Color(0.04f, 0.05f, 0.07f, 0.85f));

            _cardBg = new Image[Order.Length];
            _cardEdge = new Image[Order.Length];
            _cardName = new Text[Order.Length];
            _cardOverlay = new Image[Order.Length];
            _cardRt = new RectTransform[Order.Length];

            for (int pos = 0; pos < Order.Length; pos++)
            {
                int mi = Order[pos];
                var m = MoveCatalog.All[mi];
                int col = pos % Cols, row = pos / Cols;
                float x = -totalW / 2f + CardW / 2f + col * (CardW + Gap);
                float y = totalH / 2f - CardH / 2f - row * (CardH + Gap);
                var cat = CategoryColor(mi);

                // carta compacta: solo el nombre, grande. La data vive en el
                // panel de info de la derecha (se llena con el hover).
                var card = MakeImage(panel.rectTransform, "Card" + pos, new Vector2(0.5f, 0.5f), new Vector2(x, y),
                    new Vector2(CardW, CardH), new Color(0.12f, 0.13f, 0.17f, 0.98f));
                _cardBg[pos] = card;
                _cardRt[pos] = card.rectTransform;

                // borde izquierdo con el color de la categoría
                _cardEdge[pos] = MakeImage(card.rectTransform, "Edge", new Vector2(0f, 0.5f), new Vector2(3f, 0f),
                    new Vector2(6f, CardH - 6f), new Color(cat.r, cat.g, cat.b, 0.9f));

                _cardName[pos] = MakeText(card.rectTransform, "Name", m.Name.ToUpperInvariant(), new Vector2(0.5f, 0.5f), new Vector2(6f, 0f),
                    new Vector2(CardW - 26f, 30f), 8, new Color(1f, 1f, 1f, 0.92f), TextAnchor.MiddleCenter);
                _cardName[pos].font = UIFonts.Pixel;

                string key = pos < 9 ? (pos + 1).ToString() : pos == 9 ? "0" : "";
                var keyT = MakeText(card.rectTransform, "Key", key, new Vector2(0f, 1f), new Vector2(14f, -10f),
                    new Vector2(20f, 16f), 8, new Color(1f, 1f, 1f, 0.5f), TextAnchor.MiddleLeft);
                keyT.font = UIFonts.Pixel;

                // overlay de "no te entra en el turno" (tapa toda la carta)
                _cardOverlay[pos] = MakeImage(card.rectTransform, "Overlay", new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(CardW, CardH), new Color(0.02f, 0.02f, 0.03f, 0.78f));
                _cardOverlay[pos].gameObject.SetActive(false);
            }

            // LISTO y BORRAR apilados a la izquierda de la grilla
            float sideX = totalW / 2f + 96f;
            _doneBtn = MakeImage(rootRt, "DoneBtn", new Vector2(0.5f, 0f), new Vector2(-sideX, 118f), new Vector2(150f, 54f), DoneC);
            _doneLabel = MakeText(_doneBtn.rectTransform, "T", "¡LISTO!", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(146f, 30f), 16, Color.white, TextAnchor.MiddleCenter);
            _doneLabel.fontStyle = FontStyle.Bold;
            _undoBtn = MakeImage(rootRt, "UndoBtn", new Vector2(0.5f, 0f), new Vector2(-sideX, 62f), new Vector2(150f, 44f), UndoC);
            MakeText(_undoBtn.rectTransform, "T", "← BORRAR", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(146f, 30f), 14, Color.white, TextAnchor.MiddleCenter).fontStyle = FontStyle.Bold;

            // wakeup option: solo aparece si arrancás el turno derribado
            _wakeBtn = MakeImage(rootRt, "WakeBtn", new Vector2(0.5f, 0f), new Vector2(-sideX, 190f), new Vector2(160f, 56f), WakeC);
            _wakeLabel = MakeText(_wakeBtn.rectTransform, "T", "", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(156f, 50f), 13, Color.white, TextAnchor.MiddleCenter);
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
            MakeText(rootRt, "Help", "click o 1-9 agrega  ·  Backspace borra  ·  ESPACIO cierra el turno\narrastrá tu timeline para mover el ghost cuadro a cuadro  ·  click derecho en una ficha la borra",
                new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(1300f, 36f), 13, new Color(1f, 1f, 1f, 0.45f), TextAnchor.MiddleCenter);
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
            _root.SetActive(true);
            _active = true;
            RefreshWake();
            Highlight(_sel);
        }

        void RefreshWake()
        {
            bool avail = _mc.WakeupAvailable(_mc.Picker);
            _wakeBtn.gameObject.SetActive(avail);
            if (!avail) return;
            _wakeLabel.text = _mc.WakeQuickChoice(_mc.Picker)
                ? $"WAKEUP: RÁPIDO\n<size=11>{MatchController.WakeQuickDelta}f de knockdown</size>"
                : $"WAKEUP: QUEDARSE\n<size=11>+{MatchController.WakeStayDelta}f, baitea el meaty</size>";
        }

        public void Close()
        {
            _active = false;
            if (_root != null) _root.SetActive(false);
            RangePreview.Clear();
        }

        public void SetPrediction(PlanPreview g, int framesUsed, int available)
        {
            int left = available - framesUsed;
            int turnFrames = _mc.CurrentTurnFrames; // en Lag Mode el turno crece
            string stunNote = available < turnFrames ? $" (perdés {turnFrames - available}f por el stun)" : "";
            string extra = "";
            if (g.DamageIfStill > 0f) extra += $"  ·  pegaría {g.DamageIfStill:0} si no reacciona";
            if (g.BlockedCount > 0) extra += $"  ·  {g.BlockedCount} bloqueado(s) si se queda en neutral";
            _status.text = $"{framesUsed}/{available} frames planificados{stunNote} — quedan {left}{extra}";
            _status.color = left == 0 ? new Color(1f, 0.85f, 0.3f) : available < SimConfig.TurnFrames ? new Color(1f, 0.65f, 0.4f) : new Color(0.5f, 1f, 0.6f);

            // sin órdenes, confirmar es jugada válida (quieto bloqueando):
            // que el botón lo diga, no que parezca un LISTO en falso
            bool empty = framesUsed == 0;
            _doneLabel.text = empty ? "PASAR\n<size=11>(quieto, bloquea)</size>" : "¡LISTO!";
            _doneLabel.fontSize = empty ? 14 : 16;
        }

        void Highlight(int pos)
        {
            int n = Order.Length;
            _sel = ((pos % n) + n) % n;
            for (int i = 0; i < n; i++)
            {
                bool fits = _mc.PlanFits(Order[i]);
                bool sel = i == _sel;
                _cardBg[i].color = sel ? new Color(0.22f, 0.3f, 0.42f, 1f) : new Color(0.12f, 0.13f, 0.17f, 0.98f);
                _cardName[i].color = sel ? Color.white : new Color(1f, 1f, 1f, 0.85f);
                _cardOverlay[i].gameObject.SetActive(!fits);
            }

            // panel de info: toda la data que antes vivía apretada en la carta
            int mi = Order[_sel];
            var m = MoveCatalog.All[mi];
            var cat = CategoryColor(mi);
            _detailTitle.text = m.Name.ToUpperInvariant();
            _detailTitle.color = new Color(cat.r * 0.5f + 0.5f, cat.g * 0.5f + 0.5f, cat.b * 0.5f + 0.5f);

            string dmg = m.TotalDamage > 0f ? $"   ·   {m.TotalDamage:0} DMG" + (m.Hits.Length > 1 ? $" ({m.Hits.Length} hits)" : "") : "";
            _detailFrames.text = $"{m.Startup} / {m.Active} / {m.Recovery}  ·  {m.Total}f{dmg}";

            float px = _segW / m.Total;
            _segS.rectTransform.sizeDelta = new Vector2(m.Startup * px, 8f);
            _segA.rectTransform.anchoredPosition = new Vector2(14f + m.Startup * px, -58f);
            _segA.rectTransform.sizeDelta = new Vector2(m.Active * px, 8f);
            _segR.rectTransform.anchoredPosition = new Vector2(14f + (m.Startup + m.Active) * px, -58f);
            _segR.rectTransform.sizeDelta = new Vector2(m.Recovery * px, 8f);

            _detailAdv.text = AdvRange(m);
            _detailTag.text = CardTag(mi).ToUpperInvariant();
            _detailTag.color = new Color(cat.r * 0.6f + 0.4f, cat.g * 0.6f + 0.4f, cat.b * 0.6f + 0.4f);
            _detail.text = m.Desc;

            // el rango del movimiento se dibuja EN el escenario (Into the Breach)
            RangePreview.Show(_mc.Sim, _mc.Picker, Order[_sel]);
        }

        void Update()
        {
            if (!_active) return;

            // hover: pasar el mouse por una carta ya muestra qué hace,
            // sin tener que apretarla (apretar = agregarla al plan)
            var mp = GameInput.MousePos();
            if ((mp - _lastMouse).sqrMagnitude > 4f)
            {
                _lastMouse = mp;
                for (int i = 0; i < _cardRt.Length; i++)
                {
                    if (!RectTransformUtility.RectangleContainsScreenPoint(_cardRt[i], mp, null)) continue;
                    if (i != _sel) { Highlight(i); SfxLib.Play(SfxLib.Kind.UiTick, 0.3f); }
                    break;
                }
            }

            // tinte de hover en los botones laterales
            _doneBtn.color = HoverTint(_doneBtn, DoneC, mp);
            _undoBtn.color = HoverTint(_undoBtn, UndoC, mp);
            if (_wakeBtn.gameObject.activeSelf) _wakeBtn.color = HoverTint(_wakeBtn, WakeC, mp);

            if (GameInput.ClickPressed())
            {
                var pos = GameInput.MousePos();
                for (int i = 0; i < _cardRt.Length; i++)
                {
                    if (!RectTransformUtility.RectangleContainsScreenPoint(_cardRt[i], pos, null)) continue;
                    TryAdd(Order[i]);
                    Highlight(i);
                    return;
                }
                if (RectTransformUtility.RectangleContainsScreenPoint(_undoBtn.rectTransform, pos, null))
                {
                    TryUndo();
                    Highlight(_sel);
                    return;
                }
                if (RectTransformUtility.RectangleContainsScreenPoint(_doneBtn.rectTransform, pos, null))
                {
                    SfxLib.Play(SfxLib.Kind.UiClick, 0.8f);
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
            }

            if (GameInput.LeftPressed()) { Highlight(_sel - 1); SfxLib.Play(SfxLib.Kind.UiTick, 0.3f); }
            if (GameInput.RightPressed()) { Highlight(_sel + 1); SfxLib.Play(SfxLib.Kind.UiTick, 0.3f); }
            if (GameInput.UpPressed()) { Highlight(_sel - Cols); SfxLib.Play(SfxLib.Kind.UiTick, 0.3f); }
            if (GameInput.DownPressed()) { Highlight(_sel + Cols); SfxLib.Play(SfxLib.Kind.UiTick, 0.3f); }
            int num = GameInput.NumberPressed();
            if (num > 0 && num <= Order.Length) { TryAdd(Order[num - 1]); Highlight(num - 1); }
            else if (GameInput.AddPressed()) { TryAdd(Order[_sel]); Highlight(_sel); }
            if (GameInput.UndoPressed()) { TryUndo(); Highlight(_sel); }
            if (GameInput.EndTurnPressed()) _mc.PlanConfirm();
        }

        static Color HoverTint(Image btn, Color baseC, Vector2 mp)
            => RectTransformUtility.RectangleContainsScreenPoint(btn.rectTransform, mp, null)
                ? Color.Lerp(baseC, Color.white, 0.22f) : baseC;

        // agrega/borra con su blip solo si la acción realmente pasó
        void TryAdd(int mi)
        {
            if (_mc.PlanFits(mi)) SfxLib.Play(SfxLib.Kind.UiClick, 0.6f);
            _mc.PlanAdd(mi);
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
