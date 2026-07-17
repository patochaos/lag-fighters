using UnityEngine;
using UnityEngine.UI;

namespace LagFighter
{
    // Menú de planificación del turno (tiempo pausado): 8 cartas con framedata.
    // 1-8 agrega directo · ←/→ + Enter/J agrega · Backspace borra la última ·
    // Espacio/F cierra el turno (el resto queda en neutral).
    public class PlanMenuUI : MonoBehaviour
    {
        const float CardW = 172f, CardH = 96f, Gap = 5f;

        MatchController _mc;
        GameObject _root;
        Image[] _cardBg;
        Text _detail, _status;
        int _sel;
        bool _active;

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

        void Build(RectTransform canvasRt)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _root = new GameObject("Root", typeof(RectTransform));
            var rootRt = _root.GetComponent<RectTransform>();
            rootRt.SetParent(canvasRt, false);
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = rootRt.offsetMax = Vector2.zero;

            var moves = MoveCatalog.All;
            float totalW = moves.Length * (CardW + Gap) - Gap;

            var panel = MakeImage(rootRt, "Panel", new Vector2(0.5f, 0f), new Vector2(0f, 88f), new Vector2(totalW + 24f, CardH + 20f), new Color(0f, 0f, 0f, 0.65f), font);

            _cardBg = new Image[moves.Length];
            for (int i = 0; i < moves.Length; i++)
            {
                var m = moves[i];
                float x = -totalW / 2f + CardW / 2f + i * (CardW + Gap);
                var card = MakeImage(panel.rectTransform, "Card" + i, new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(CardW, CardH), new Color(0.13f, 0.14f, 0.18f, 0.95f), font);
                _cardBg[i] = card;

                MakeText(card.rectTransform, "Key", (i + 1).ToString(), new Vector2(0f, 1f), new Vector2(9f, -3f), new Vector2(30f, 16f), 12, new Color(1f, 1f, 1f, 0.4f), TextAnchor.UpperLeft, font);
                MakeText(card.rectTransform, "Name", m.Name, new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(CardW - 8f, 24f), 16, Color.white, TextAnchor.UpperCenter, font);
                MakeText(card.rectTransform, "Frames", $"{m.Startup}/{m.Active}/{m.Recovery}  ·  {m.Total}f", new Vector2(0.5f, 0f), new Vector2(0f, 32f), new Vector2(CardW - 8f, 18f), 13, new Color(0.95f, 0.85f, 0.4f), TextAnchor.MiddleCenter, font);
                string extra = m.IsAttack ? $"{m.TotalDamage:0} DMG" + (m.Hits[0].Knockdown ? " · DERRIBA" : "") :
                               m.IsGuard ? "BLOQUEA" :
                               m.MoveDx != 0f ? (m.MoveDx > 0f ? "AVANZA" : "RETROCEDE") : "—";
                MakeText(card.rectTransform, "Extra", extra, new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(CardW - 8f, 16f), 12, new Color(1f, 1f, 1f, 0.75f), TextAnchor.MiddleCenter, font);
            }

            _detail = MakeText(rootRt, "Detail", "", new Vector2(0.5f, 0f), new Vector2(0f, 222f), new Vector2(1600f, 24f), 17, Color.white, TextAnchor.MiddleCenter, font);
            _status = MakeText(rootRt, "Status", "", new Vector2(0.5f, 0f), new Vector2(0f, 198f), new Vector2(1600f, 22f), 16, new Color(0.5f, 1f, 0.6f), TextAnchor.MiddleCenter, font);
            MakeText(rootRt, "Help", "1-8 agrega · ←/→ + Enter agrega · Backspace borra · ESPACIO cierra el turno", new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1200f, 22f), 15, new Color(1f, 1f, 1f, 0.5f), TextAnchor.MiddleCenter, font);
        }

        public void Open(int picker)
        {
            _root.SetActive(true);
            _active = true;
            Highlight(_sel);
        }

        public void Close()
        {
            _active = false;
            if (_root != null) _root.SetActive(false);
        }

        public void SetPrediction(PlanPreview g, int framesUsed)
        {
            int left = SimConfig.TurnFrames - framesUsed;
            string dmg = g.DamageIfStill > 0f ? $"  ·  pegaría {g.DamageIfStill:0} si el rival no se mueve" : "";
            _status.text = $"{framesUsed}/{SimConfig.TurnFrames} frames planificados — quedan {left}{dmg}";
            _status.color = left == 0 ? new Color(1f, 0.85f, 0.3f) : new Color(0.5f, 1f, 0.6f);
        }

        void Highlight(int idx)
        {
            int n = MoveCatalog.All.Length;
            _sel = ((idx % n) + n) % n;
            for (int i = 0; i < _cardBg.Length; i++)
            {
                bool fits = _mc.PlanFits(i);
                _cardBg[i].color = i == _sel ? new Color(0.25f, 0.42f, 0.62f, 1f) :
                    fits ? new Color(0.13f, 0.14f, 0.18f, 0.95f) : new Color(0.1f, 0.1f, 0.12f, 0.6f);
            }
            var m = MoveCatalog.All[_sel];
            _detail.text = $"{m.Name} — {m.Desc}";
        }

        void Update()
        {
            if (!_active) return;
            if (GameInput.LeftPressed()) Highlight(_sel - 1);
            if (GameInput.RightPressed()) Highlight(_sel + 1);
            int num = GameInput.NumberPressed();
            if (num > 0 && num <= MoveCatalog.All.Length) { _mc.PlanAdd(num - 1); Highlight(num - 1); }
            else if (GameInput.AddPressed()) { _mc.PlanAdd(_sel); Highlight(_sel); }
            if (GameInput.UndoPressed()) { _mc.PlanUndo(); Highlight(_sel); }
            if (GameInput.EndTurnPressed()) _mc.PlanConfirm();
        }

        Image MakeImage(RectTransform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color color, Font font)
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

        Text MakeText(RectTransform parent, string name, string content, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize, Color color, TextAnchor align, Font font)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var t = go.GetComponent<Text>();
            t.font = font;
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
