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
        readonly Text[][] _leftLbl = new Text[2][];
        readonly Image[] _badge = new Image[2];
        readonly Text[] _badgeLbl = new Text[2];
        Text _header, _rules;

        const float PanelW = 452f, PanelH = 252f;
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
        const float RevealW = 210f, RevealH = 290f;
        const float SlotX = 235f;

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
            int font, Color c, TextAnchor align, bool pixel = true)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var t = go.GetComponent<Text>();
            t.font = pixel ? UIFonts.Pixel : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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

            // encabezado: UNA sola línea de turno y UNA de reglas, centradas
            var hdr = Img(rt, "Hdr", new Vector2(0.5f, 1f), new Vector2(0f, -38f), new Vector2(1060f, 64f),
                new Color(0.03f, 0.04f, 0.06f, 0.85f));
            _header = Txt(hdr.rectTransform, "Turn", "", new Vector2(0.5f, 1f), new Vector2(0f, -16f),
                new Vector2(1020f, 24f), 16, new Color(1f, 0.95f, 0.72f), TextAnchor.MiddleCenter);
            _rules = Txt(hdr.rectTransform, "Rules", "", new Vector2(0.5f, 1f), new Vector2(0f, -42f),
                new Vector2(1020f, 22f), 11, new Color(1f, 1f, 1f, 0.62f), TextAnchor.MiddleCenter);
            _rules.text = "<color=#ff9550>GOLPE</color> › <color=#b86bf2>AGARRE</color> › " +
                          "<color=#5abfff>GUARDIA</color> › <color=#ff9550>GOLPE</color>    ·    " +
                          "golpe vs golpe gana el más RÁPIDO    ·    cada golpe es ALTO o BAJO";

            for (int i = 0; i < 2; i++) BuildPanel(rt, i);
        }

        void BuildPanel(RectTransform rt, int i)
        {
            bool left = i == 0;
            var anchor = new Vector2(left ? 0f : 1f, 1f);
            var panel = Img(rt, "Panel" + i, anchor, new Vector2((left ? 1f : -1f) * 26f, -104f),
                new Vector2(PanelW, PanelH), new Color(0.04f, 0.05f, 0.07f, 0.95f));
            panel.rectTransform.pivot = new Vector2(left ? 0f : 1f, 1f);
            var p = panel.rectTransform;
            var accent = left ? new Color(0.35f, 0.7f, 1f) : new Color(1f, 0.55f, 0.35f);

            Img(p, "Accent", new Vector2(0.5f, 1f), new Vector2(0f, -3f), new Vector2(PanelW, 6f), accent);

            _who[i] = Txt(p, "Who", "", new Vector2(0f, 1f), new Vector2(PanelW * 0.5f, -26f),
                new Vector2(PanelW - 24f, 26f), 15, accent, TextAnchor.MiddleCenter);

            var hpBg = Img(p, "HpBg", new Vector2(0f, 1f), new Vector2(PanelW * 0.5f, -60f),
                new Vector2(HpW, 34f), new Color(0.13f, 0.05f, 0.05f, 1f));
            _hpFill[i] = Img(hpBg.rectTransform, "Fill", new Vector2(0f, 0.5f), Vector2.zero,
                new Vector2(HpW - 4f, 30f), new Color(0.35f, 0.8f, 0.35f, 1f));
            _hpFill[i].rectTransform.pivot = new Vector2(0f, 0.5f);
            _hpFill[i].rectTransform.anchoredPosition = new Vector2(2f, 0f);
            _hpNum[i] = Txt(hpBg.rectTransform, "Hp", "", new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(HpW - 8f, 30f), 18, Color.white, TextAnchor.MiddleCenter);

            _piles[i] = Txt(p, "Piles", "", new Vector2(0f, 1f), new Vector2(PanelW * 0.5f, -94f),
                new Vector2(PanelW - 24f, 22f), 12, new Color(1f, 1f, 1f, 0.85f), TextAnchor.MiddleCenter);

            Txt(p, "LeftLbl", "LE QUEDAN", new Vector2(0f, 1f), new Vector2(PanelW * 0.5f, -118f),
                new Vector2(PanelW - 24f, 16f), 9, new Color(1f, 1f, 1f, 0.4f), TextAnchor.MiddleCenter);

            // strip en GRILLA fija de 5×2: sin wrap, sin solapes posibles
            _leftChip[i] = new Image[DuelCatalog.CardsPerChar];
            _leftLbl[i] = new Text[DuelCatalog.CardsPerChar];
            float cw = (PanelW - 32f) / Cols, ch = 26f;
            for (int c = 0; c < DuelCatalog.CardsPerChar; c++)
            {
                int col = c % Cols, row = c / Cols;
                float x = 16f + cw * (col + 0.5f);
                float y = -140f - row * (ch + 4f);
                _leftChip[i][c] = Img(p, "Chip" + c, new Vector2(0f, 1f), new Vector2(x, y),
                    new Vector2(cw - 5f, ch), new Color(0.11f, 0.13f, 0.17f, 1f));
                _leftLbl[i][c] = Txt(_leftChip[i][c].rectTransform, "T", "", new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(cw - 7f, ch - 4f), 11, Color.white, TextAnchor.MiddleCenter);
            }

            _badge[i] = Img(p, "Badge", new Vector2(0f, 1f), new Vector2(PanelW * 0.5f, -226f),
                new Vector2(PanelW - 24f, 28f), new Color(0.6f, 0.22f, 0.1f, 0.98f));
            _badgeLbl[i] = Txt(_badge[i].rectTransform, "T", "", new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(PanelW - 28f, 24f), 12, new Color(1f, 0.88f, 0.75f), TextAnchor.MiddleCenter);
            _badge[i].gameObject.SetActive(false);
        }

        public void SetVisible(bool on)
        {
            if (_root != null && _root.activeSelf != on) _root.SetActive(on);
            if (!on) HideReveal();
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
        }

        void RefreshSide(int i)
        {
            var chr = S.Chr[i];
            int max = S.MaxHpOf(i);
            _who[i].text = (i == 0 ? "VOS · " : "RIVAL · ") + chr.Name;

            float f = Mathf.Clamp01(S.Hp[i] / (float)max);
            _hpFill[i].rectTransform.sizeDelta = new Vector2((HpW - 4f) * f, 30f);
            _hpFill[i].color = f > 0.5f ? new Color(0.35f, 0.8f, 0.35f, 1f)
                : f > 0.25f ? new Color(0.95f, 0.72f, 0.2f, 1f)
                : new Color(0.95f, 0.28f, 0.24f, 1f);
            _hpNum[i].text = $"{S.Hp[i]} / {max}";

            _piles[i].text = $"MANO {S.Hand[i].Count}   ·   MAZO {S.Deck[i].Count}   ·   DESCARTE {S.Discard[i].Count}";

            var used = new int[DuelCatalog.CardsPerChar];
            foreach (int c in S.Discard[i]) used[c]++;
            foreach (int c in S.Spent[i]) used[c]++;
            for (int c = 0; c < DuelCatalog.CardsPerChar; c++)
            {
                int leftN = Mathf.Max(0, chr.DeckCounts[c] - used[c]);
                bool guard = c == DuelCatalog.GuardHigh || c == DuelCatalog.GuardLow;
                _leftLbl[i][c].text = $"{chr.Cards[c].Short}·{leftN}";
                _leftLbl[i][c].color = leftN == 0 ? new Color(1f, 1f, 1f, 0.22f)
                    : guard ? new Color(0.45f, 0.82f, 1f) : new Color(1f, 1f, 1f, 0.92f);
                _leftChip[i][c].color = leftN == 0 ? new Color(0.08f, 0.08f, 0.1f, 1f)
                    : guard ? new Color(0.1f, 0.19f, 0.28f, 1f) : new Color(0.13f, 0.14f, 0.18f, 1f);
            }

            bool kd = S.KnockedDown[i], esc = S.Spent[i].Count > 0;
            bool on = kd || esc;
            if (_badge[i].gameObject.activeSelf != on) _badge[i].gameObject.SetActive(on);
            if (!on) return;
            _badge[i].color = kd ? new Color(0.62f, 0.2f, 0.09f, 0.98f) : new Color(0.14f, 0.16f, 0.2f, 0.95f);
            _badgeLbl[i].text = kd ? "¡DERRIBADO! sin guardia este turno" : "escape ya gastado";
            _badgeLbl[i].color = kd ? new Color(1f, 0.88f, 0.75f) : new Color(1f, 1f, 1f, 0.45f);
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
                    new Vector2(RevealW + 26f, RevealH + 26f), new Color(1f, 0.9f, 0.4f, 0f));

                // FRENTE
                var front = new GameObject("Front", typeof(RectTransform));
                var frt = front.GetComponent<RectTransform>();
                frt.SetParent(holder.rectTransform, false);
                frt.anchorMin = frt.anchorMax = new Vector2(0.5f, 0.5f);
                frt.sizeDelta = new Vector2(RevealW, RevealH);
                if (card >= 0) DuelHandUI.PaintCard(frt, S.Def(i, card), RevealW, RevealH);
                else Txt(frt, "None", "SIN CARTAS", new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(RevealW, 40f), 14, new Color(1f, 0.6f, 0.5f), TextAnchor.MiddleCenter);
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
                    new Vector2(RevealW, RevealH), new Color(0.02f, 0.02f, 0.04f, 0f));

                Txt(holder.rectTransform, "Owner", i == 0 ? "VOS" : "RIVAL", new Vector2(0.5f, 1f),
                    new Vector2(0f, 26f), new Vector2(RevealW, 24f), 13,
                    i == 0 ? new Color(0.6f, 0.85f, 1f) : new Color(1f, 0.78f, 0.62f), TextAnchor.MiddleCenter);
            }

            _vs = Txt(rt, "VS", "VS", new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(160f, 70f), 34,
                new Color(1f, 0.9f, 0.5f), TextAnchor.MiddleCenter);
            _verdict = Txt(rt, "Verdict", "", new Vector2(0.5f, 0.5f), new Vector2(0f, -196f),
                new Vector2(1400f, 46f), 26, new Color(1f, 0.97f, 0.75f), TextAnchor.MiddleCenter);
            _detail = Txt(rt, "Detail", "", new Vector2(0.5f, 0.5f), new Vector2(0f, -238f),
                new Vector2(1500f, 34f), 17, new Color(1f, 1f, 1f, 0.85f), TextAnchor.MiddleCenter, pixel: false);
            _verdict.gameObject.SetActive(false);
            _detail.gameObject.SetActive(false);

            _phase = Phase.Deal;
            _t = 0f;
            TickReveal();
        }

        // Dorso: no hace falta arte, hace falta que se lea "esto está oculto".
        static void PaintBack(RectTransform parent, int side)
        {
            var baseCol = side == 0 ? new Color(0.13f, 0.2f, 0.34f) : new Color(0.3f, 0.16f, 0.12f);
            Img(parent, "Frame", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(RevealW, RevealH),
                new Color(baseCol.r * 1.8f, baseCol.g * 1.8f, baseCol.b * 1.8f, 1f));
            Img(parent, "Bg", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(RevealW - 8f, RevealH - 8f), baseCol);
            for (int k = 0; k < 5; k++)
                Img(parent, "Stripe" + k, new Vector2(0.5f, 0.5f), new Vector2(0f, -90f + k * 45f),
                    new Vector2(RevealW - 40f, 10f),
                    new Color(baseCol.r * 2.4f, baseCol.g * 2.4f, baseCol.b * 2.4f, 0.55f));
            Txt(parent, "Q", "?", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(RevealW, 90f), 56,
                new Color(1f, 1f, 1f, 0.75f), TextAnchor.MiddleCenter);
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
                string alt = atk.Height == DuelHeight.High ? "ALTO" : "BAJO";
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
                bool down = g == 0 ? r.KdNext0 : r.KdNext1;
                if (S.KnockedDown[g] || down)
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
                        float ang = t * 180f;
                        _rc[i].localRotation = Quaternion.Euler(0f, ang, 0f);
                        _rc[i].localScale = Vector3.one * (1f + Mathf.Sin(t * Mathf.PI) * 0.10f);
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
                        for (int i = 0; i < 2; i++) _rc[i].localRotation = Quaternion.identity;
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
                _rc[i].anchoredPosition = new Vector2(sign * 700f, 40f);
                _rc[i].localScale = Vector3.one * 0.60f;
                _rc[i].localRotation = Quaternion.identity;
            }
            if (_verdict != null)
            {
                _verdict.rectTransform.anchoredPosition = new Vector2(0f, 252f);
                _verdict.transform.localScale = Vector3.one * 0.8f;
            }
            if (_detail != null) _detail.rectTransform.anchoredPosition = new Vector2(0f, 212f);
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
    }
}
