using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LagFighter
{
    // Menú de planificación del turno (tiempo pausado): 12 cartas con framedata.
    // Click en carta agrega · 1-9/0 agrega directo · ←/→ + Enter/J agrega ·
    // Backspace (o botón ⌫) borra la última · Espacio (o botón LISTO) cierra.
    public class PlanMenuUI : MonoBehaviour
    {
        const float CardW = 148f, CardH = 100f, Gap = 4f;

        MatchController _mc;
        GameObject _root;
        Image[] _cardBg;
        RectTransform[] _cardRt;
        Image _undoBtn, _doneBtn;
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
            _cardRt = new RectTransform[moves.Length];
            for (int i = 0; i < moves.Length; i++)
            {
                var m = moves[i];
                float x = -totalW / 2f + CardW / 2f + i * (CardW + Gap);
                var card = MakeImage(panel.rectTransform, "Card" + i, new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(CardW, CardH), new Color(0.13f, 0.14f, 0.18f, 0.95f), font);
                _cardBg[i] = card;
                _cardRt[i] = card.rectTransform;

                string key = i < 9 ? (i + 1).ToString() : i == 9 ? "0" : "";
                MakeText(card.rectTransform, "Key", key, new Vector2(0f, 1f), new Vector2(8f, -3f), new Vector2(30f, 16f), 12, new Color(1f, 1f, 1f, 0.4f), TextAnchor.UpperLeft, font);
                MakeText(card.rectTransform, "Name", m.Name, new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(CardW - 6f, 24f), 15, Color.white, TextAnchor.UpperCenter, font);
                MakeText(card.rectTransform, "Frames", $"{m.Startup}/{m.Active}/{m.Recovery} · {m.Total}f", new Vector2(0.5f, 0f), new Vector2(0f, 32f), new Vector2(CardW - 6f, 18f), 12, new Color(0.95f, 0.85f, 0.4f), TextAnchor.MiddleCenter, font);
                MakeText(card.rectTransform, "Extra", CardTag(m, i), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(CardW - 4f, 16f), 11, TagColor(m, i), TextAnchor.MiddleCenter, font);
            }

            // botones clickeables
            _undoBtn = MakeImage(rootRt, "UndoBtn", new Vector2(0.5f, 0f), new Vector2(-totalW / 2f - 70f, 88f + (CardH + 20f) / 2f), new Vector2(100f, 46f), new Color(0.35f, 0.18f, 0.18f, 0.95f), font);
            MakeText(_undoBtn.rectTransform, "T", "← BORRAR", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(96f, 30f), 14, Color.white, TextAnchor.MiddleCenter, font);
            _doneBtn = MakeImage(rootRt, "DoneBtn", new Vector2(0.5f, 0f), new Vector2(totalW / 2f + 70f, 88f + (CardH + 20f) / 2f), new Vector2(100f, 46f), new Color(0.16f, 0.4f, 0.2f, 0.95f), font);
            MakeText(_doneBtn.rectTransform, "T", "¡LISTO!", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(96f, 30f), 14, Color.white, TextAnchor.MiddleCenter, font);

            _detail = MakeText(rootRt, "Detail", "", new Vector2(0.5f, 0f), new Vector2(0f, 226f), new Vector2(1700f, 24f), 16, Color.white, TextAnchor.MiddleCenter, font);
            _status = MakeText(rootRt, "Status", "", new Vector2(0.5f, 0f), new Vector2(0f, 202f), new Vector2(1700f, 22f), 16, new Color(0.5f, 1f, 0.6f), TextAnchor.MiddleCenter, font);
            MakeText(rootRt, "Help", "click o 1-9/0 agrega · ←/→ + Enter agrega · Backspace borra · ESPACIO cierra el turno", new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1300f, 22f), 14, new Color(1f, 1f, 1f, 0.5f), TextAnchor.MiddleCenter, font);
        }

        static string CardTag(MoveDef m, int i)
        {
            if (i == MoveCatalog.Shoryuken) return "INVULN · DERRIBA";
            if (i == MoveCatalog.Hadouken) return "PROYECTIL";
            if (m.HasAir) return "AÉREO · no bloquea";
            if (i == MoveCatalog.WalkB || i == MoveCatalog.Wait) return "BLOQUEA";
            if (m.Hits.Length > 0)
                return $"{m.TotalDamage:0} DMG" + (m.Hits[0].Knockdown ? " · DERRIBA" : "") + $" · hs{m.Hits[0].Hitstun}/bs{m.Hits[0].Blockstun}";
            return m.MoveDx > 0f ? "AVANZA · no bloquea" : m.MoveDx < 0f ? "RETROCEDE" : "—";
        }

        static Color TagColor(MoveDef m, int i)
        {
            if (i == MoveCatalog.Shoryuken) return new Color(1f, 0.75f, 0.25f);
            if (i == MoveCatalog.Hadouken) return new Color(0.45f, 0.75f, 1f);
            if (i == MoveCatalog.WalkB || i == MoveCatalog.Wait) return new Color(0.5f, 0.75f, 1f);
            if (m.Hits.Length > 0) return new Color(1f, 0.55f, 0.45f);
            return new Color(0.6f, 0.9f, 0.65f);
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
            string extra = "";
            if (g.DamageIfStill > 0f) extra += $"  ·  pegaría {g.DamageIfStill:0} si no reacciona";
            if (g.BlockedCount > 0) extra += $"  ·  {g.BlockedCount} bloqueado(s) si se queda en neutral";
            _status.text = $"{framesUsed}/{SimConfig.TurnFrames} frames planificados — quedan {left}{extra}";
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

            if (GameInput.ClickPressed())
            {
                var pos = GameInput.MousePos();
                for (int i = 0; i < _cardRt.Length; i++)
                {
                    if (!RectTransformUtility.RectangleContainsScreenPoint(_cardRt[i], pos, null)) continue;
                    _mc.PlanAdd(i);
                    Highlight(i);
                    return;
                }
                if (RectTransformUtility.RectangleContainsScreenPoint(_undoBtn.rectTransform, pos, null))
                {
                    _mc.PlanUndo();
                    Highlight(_sel);
                    return;
                }
                if (RectTransformUtility.RectangleContainsScreenPoint(_doneBtn.rectTransform, pos, null))
                {
                    _mc.PlanConfirm();
                    return;
                }
            }

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
