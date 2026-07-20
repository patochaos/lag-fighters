using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LagFighter
{
    // Paleta oficial (UI_PLAN.md): color = significado, siempre el mismo.
    public static class Palette
    {
        public static readonly Color P1 = new Color(0.25f, 0.70f, 0.95f);      // celeste
        public static readonly Color P2 = new Color(0.95f, 0.45f, 0.25f);      // naranja
        public static readonly Color Startup = new Color(0.95f, 0.85f, 0.25f); // amarillo
        public static readonly Color Damage = new Color(0.95f, 0.30f, 0.22f);  // rojo
        public static readonly Color Block = new Color(0.30f, 0.55f, 0.90f);   // azul
        public static readonly Color GrabC = new Color(0.90f, 0.25f, 0.77f);   // magenta
        public static readonly Color Ok = new Color(0.35f, 0.85f, 0.42f);      // verde
        public static readonly Color Guard = new Color(1f, 0.85f, 0.25f);      // ámbar
        public static readonly Color Neutral = new Color(0.45f, 0.47f, 0.50f); // gris
        public static readonly Color PanelBg = new Color(0.02f, 0.03f, 0.05f, 0.78f);

        public static Color Side(int i) => i == 0 ? P1 : P2;
    }

    // Fuentes: pixel (Press Start 2P, solo mayúsculas, para títulos/labels
    // cortos) y cuerpo (Liberation) para descripciones y símbolos (←» etc.).
    public static class UIFonts
    {
        static Font _pixel, _body;

        public static Font Pixel
        {
            get
            {
                if (_pixel == null)
                {
                    _pixel = Resources.Load<Font>("LagFighter/PressStart2P");
                    if (_pixel == null) _pixel = Body;
                }
                return _pixel;
            }
        }

        public static Font Body
        {
            get
            {
                if (_body == null) _body = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _body;
            }
        }
    }

    // Feedback EN el mundo: daño/eventos flotando sobre el peleador golpeado
    // y badges de estado (HITSTUN 12f) sobre las cabezas. Into the Breach:
    // la información vive donde pasa la acción.
    public class WorldFX : MonoBehaviour
    {
        static WorldFX _i;

        class Pop
        {
            public TextMesh Tm;
            public float Life, MaxLife;
            public Vector3 Vel;
        }

        readonly List<Pop> _pops = new List<Pop>();
        readonly TextMesh[] _badges = new TextMesh[2];

        static WorldFX Ensure()
        {
            if (_i == null)
            {
                _i = new GameObject("LagFighter.WorldFX").AddComponent<WorldFX>();
                for (int f = 0; f < 2; f++)
                    _i._badges[f] = _i.MakeTm(28, 0.055f);
            }
            return _i;
        }

        TextMesh MakeTm(int fontSize, float charSize)
        {
            var go = new GameObject("WorldText");
            go.transform.SetParent(transform, false);
            var tm = go.AddComponent<TextMesh>();
            tm.font = UIFonts.Pixel;
            tm.fontSize = fontSize;
            tm.characterSize = charSize;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            go.GetComponent<MeshRenderer>().material = UIFonts.Pixel.material;
            go.SetActive(false);
            return tm;
        }

        // texto flotante sobre una posición del mundo (x del peleador golpeado)
        public static void Popup(float worldX, string msg, Color c, float scale = 1f)
        {
            var i = Ensure();
            Pop pop = null;
            foreach (var p in i._pops)
                if (!p.Tm.gameObject.activeSelf) { pop = p; break; }
            if (pop == null)
            {
                pop = new Pop { Tm = i.MakeTm(34, 0.05f) };
                i._pops.Add(pop);
            }
            pop.Tm.text = msg;
            pop.Tm.color = c;
            pop.Tm.characterSize = 0.05f * scale;
            pop.Tm.transform.position = new Vector3(worldX, 2.15f, -0.6f);
            pop.Tm.gameObject.SetActive(true);
            pop.MaxLife = pop.Life = 1.15f;
            pop.Vel = new Vector3(0f, 1.15f, 0f);
        }

        // badges de estado sobre las cabezas, actualizados por frame desde el HUD
        public static void SetBadge(int fighter, float worldX, float worldY, string msg, Color c)
        {
            var i = Ensure();
            var tm = i._badges[fighter];
            if (string.IsNullOrEmpty(msg)) { tm.gameObject.SetActive(false); return; }
            tm.gameObject.SetActive(true);
            tm.text = msg;
            tm.color = c;
            tm.transform.position = new Vector3(worldX, worldY, -0.5f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            foreach (var p in _pops)
            {
                if (!p.Tm.gameObject.activeSelf) continue;
                p.Life -= dt;
                if (p.Life <= 0f) { p.Tm.gameObject.SetActive(false); continue; }
                p.Vel *= 1f - 2.2f * dt;
                p.Tm.transform.position += p.Vel * dt;
                var c = p.Tm.color;
                c.a = Mathf.Clamp01(p.Life / (p.MaxLife * 0.45f));
                p.Tm.color = c;
            }
        }
    }

    // Cursor pixel-art generado en runtime (cero assets): flecha blanca con
    // borde oscuro, a tono con la estética Press Start 2P.
    public static class CursorFX
    {
        // '#' = relleno claro, 'X' = borde oscuro, '.' = transparente
        static readonly string[] Arrow =
        {
            "X...........",
            "XX..........",
            "X#X.........",
            "X##X........",
            "X###X.......",
            "X####X......",
            "X#####X.....",
            "X######X....",
            "X#######X...",
            "X########X..",
            "X#####XXXXX.",
            "X##X##X.....",
            "X#X.X##X....",
            "XX..X##X....",
            ".....X##X...",
            ".....X##X...",
            "......XX....",
        };

        public static void Apply()
        {
            const int S = 24;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            var clear = new Color(0f, 0f, 0f, 0f);
            var fill = new Color(0.95f, 0.97f, 1f);
            var edge = new Color(0.04f, 0.05f, 0.09f);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                    tex.SetPixel(x, y, clear);
            for (int r = 0; r < Arrow.Length; r++)
                for (int c = 0; c < Arrow[r].Length; c++)
                {
                    if (Arrow[r][c] == '.') continue;
                    tex.SetPixel(c, S - 1 - r, Arrow[r][c] == '#' ? fill : edge);
                }
            tex.Apply();
            Cursor.SetCursor(tex, Vector2.zero, CursorMode.Auto);
        }
    }

    // Rango del movimiento hovereado, dibujado EN el escenario frente al
    // peleador que planifica (hitboxes y línea del proyectil). La framedata
    // como intuición espacial; el desplazamiento lo actúa el ghost.
    public class RangePreview : MonoBehaviour
    {
        static RangePreview _i;
        readonly List<GameObject> _pool = new List<GameObject>();
        int _used;

        static RangePreview Ensure()
        {
            if (_i == null) _i = new GameObject("LagFighter.RangePreview").AddComponent<RangePreview>();
            return _i;
        }

        public static void Clear()
        {
            if (_i == null) return;
            _i._used = 0;
            foreach (var go in _i._pool) go.SetActive(false);
        }

        public static void Show(MatchSim sim, int fighter, int moveIndex)
        {
            var i = Ensure();
            Clear();
            if (sim == null) return;
            var f = sim.Fighters[fighter];
            var m = MoveCatalog.All[moveIndex];
            int face = f.Face;

            // hitboxes de cada ventana, en su posición de impacto estimada
            foreach (var h in m.Hits)
            {
                // desplazamiento propio acumulado hasta el primer frame activo
                float dx = 0f;
                if (m.MotionEnd > m.MotionStart)
                {
                    float frac = Mathf.Clamp01((h.Start - m.MotionStart) / (float)(m.MotionEnd - m.MotionStart));
                    dx = m.MoveDx * frac;
                }
                float cx = f.X + face * (dx + (h.Fwd0 + h.Fwd1) * 0.5f);
                var c = h.IsGrab ? Palette.GrabC : Palette.Damage;
                i.Box(new Vector3(cx, (h.Y0 + h.Y1) * 0.5f, 0f),
                    new Vector3(h.Fwd1 - h.Fwd0, h.Y1 - h.Y0, 0.35f), new Color(c.r, c.g, c.b, 0.20f));
            }

            // proyectil: la franja que recorre el hadouken
            if (m.SpawnFrame >= 0)
            {
                float x0 = f.X + face * 0.7f;
                float x1 = face > 0 ? SimConfig.StageHalfWidth + 0.6f : -SimConfig.StageHalfWidth - 0.6f;
                i.Box(new Vector3((x0 + x1) * 0.5f, (SimConfig.ProjY0 + SimConfig.ProjY1) * 0.5f, 0f),
                    new Vector3(Mathf.Abs(x1 - x0), SimConfig.ProjY1 - SimConfig.ProjY0, 0.3f),
                    new Color(Palette.Block.r, Palette.Block.g, Palette.Block.b, 0.14f));
            }

            // (los puntos de trayectoria se retiraron: el ghost ACTÚA el
            // movimiento hovereado, que se lee mucho mejor que los dots)
        }

        void Box(Vector3 pos, Vector3 scale, Color c)
        {
            GameObject go;
            if (_used < _pool.Count) go = _pool[_used];
            else
            {
                go = VizLib.MakeBox("Range", Color.white, transform);
                _pool.Add(go);
            }
            _used++;
            go.SetActive(true);
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().material.color = c;
        }
    }
}
