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

    // Paleta de DUELO — "SALA DE ESPERA" (DUELO-LOOK.md).
    //
    // LA regla, la que arregla el malentendido más caro de esa pantalla:
    // **el color de LADO y el color de REGLA son dos idiomas separados y no se
    // pisan.** Antes VOS era celeste y GUARDIA también; RIVAL era naranja y
    // GOLPE también — o sea que en tu propia mano las cartas de golpe se veían
    // del rival. Ahora celeste/naranja significan UNA cosa (de quién es) y las
    // reglas viven en su propia familia (rojo · violeta · ámbar · verde).
    public static class Duelo
    {
        // cromo (el 90% de los pixeles)
        public static readonly Color Void = new Color32(0x07, 0x0A, 0x10, 0xFF);
        public static readonly Color Panel = new Color32(0x0A, 0x0F, 0x19, 0xFF);
        public static readonly Color Stage = new Color32(0x10, 0x17, 0x25, 0xFF);
        public static readonly Color StageLit = new Color32(0x1C, 0x27, 0x40, 0xFF);
        public static readonly Color Line = new Color32(0x2B, 0x3A, 0x55, 0xFF);
        public static readonly Color Paper = new Color32(0xEA, 0xF0, 0xFA, 0xFF);  // nunca blanco puro
        public static readonly Color Mute = new Color32(0x84, 0x94, 0xAD, 0xFF);

        // identidad de lado — SOLO cromo (nombre, vida, borde, luz)
        public static readonly Color P1 = new Color32(0x3F, 0xB6, 0xF5, 0xFF);
        public static readonly Color P2 = new Color32(0xFF, 0x7A, 0x3C, 0xFF);
        public static Color Side(int i) => i == 0 ? P1 : P2;

        // reglas del juego — estos mandan
        public static readonly Color Golpe = new Color32(0xFF, 0x3B, 0x30, 0xFF);
        public static readonly Color Agarre = new Color32(0xB1, 0x5C, 0xFF, 0xFF);
        public static readonly Color Guardia = new Color32(0xFF, 0xC5, 0x3D, 0xFF);
        public static readonly Color Escape = new Color32(0x4B, 0xE0, 0x8A, 0xFF);
        public static readonly Color Vel = new Color32(0x5A, 0xC8, 0xFA, 0xFF);   // solo el numeral
        public static readonly Color Gold = new Color32(0xFF, 0xE4, 0x5C, 0xFF);  // ceremonia: ganador, premio, KO

        // vida: verde → ámbar → rojo, con los mismos hues de la familia
        public static Color Hp(float frac) =>
            frac > 0.5f ? Escape : frac > 0.25f ? Guardia : Golpe;

        public static Color Alpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
        // fondo tenue de un color de regla (chips, franjas): el color pero apagado
        public static Color Wash(Color c, float k = 0.22f) => new Color(c.r * k, c.g * k, c.b * k, 1f);
    }

    // Fuentes. Tres roles, sin superposición (DUELO-LOOK.md §4):
    //
    //   Pixel  — Press Start 2P: display puro (nombres de carta, veredicto,
    //            VS, KO, los numerales de velocidad/daño).
    //   Data   — Barlow Condensed SemiBold: etiquetas, chips y prompts. Es
    //            CONDENSADA, así que entra el doble de texto al doble de
    //            tamaño — que es exactamente lo que le faltaba a esta UI.
    //   Body   — Barlow Condensed Medium: párrafos del panel de detalle.
    //
    // Además arregla un bug real: Press Start 2P dibuja las MAYÚSCULAS
    // ACENTUADAS enanas (no le entra el diacrítico arriba de la caja de 5px),
    // así que "DAÑO" se leía "DAñO" y "ELÉCTRICA" salía "ELéCTRICA". Todo lo
    // que lleve Ñ o acento va en Data/Body, que las dibuja bien.
    public static class UIFonts
    {
        static Font _pixel, _body, _data, _para;

        public static Font Data
        {
            get
            {
                if (_data == null)
                {
                    _data = Resources.Load<Font>("LagFighter/BarlowCondensed-SemiBold");
                    if (_data == null) _data = Body;
                }
                return _data;
            }
        }

        public static Font Para
        {
            get
            {
                if (_para == null)
                {
                    _para = Resources.Load<Font>("LagFighter/BarlowCondensed-Medium");
                    if (_para == null) _para = Data;
                }
                return _para;
            }
        }

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

    // Pictogramas pixel-art por move, generados en runtime (cero assets):
    // el icono se lee más rápido que la abreviatura ("BL"/"DP") en las
    // fichas de la timeline y las cartas del menú. Blancos: se tiñen con
    // el color de la Image que los muestra.
    public static class MoveIcons
    {
        static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        // 11x10, 'X' = pixel. Formas geométricas gordas: leen a 20px.
        static readonly string[] Fist =
        {
            "...........",
            ".XX.XX.XX..",
            "XXXXXXXXXX.",
            "XXXXXXXXXXX",
            "XXXXXXXXXXX",
            ".XXXXXXXXXX",
            ".XXXXXXXXX.",
            "..XXXXXXX..",
            "...........",
            "...........",
        };
        static readonly string[] Sweep = // barrida: cuña al ras del piso
        {
            "...........",
            "...........",
            "...........",
            "...........",
            "XX.........",
            "XXXX.......",
            "XXXXXXX....",
            "XXXXXXXXXX.",
            "XXXXXXXXXXX",
            "...........",
        };
        static readonly string[] Ball = // hadouken: bola con estela
        {
            "...........",
            "....XXXX...",
            "...XXXXXX..",
            "X.XXXXXXXX.",
            ".XXXXXXXXX.",
            "X.XXXXXXXX.",
            ".XXXXXXXXX.",
            "X..XXXXXX..",
            "....XXXX...",
            "...........",
        };
        static readonly string[] Rising = // shoryu: flecha bien arriba
        {
            ".....X.....",
            "....XXX....",
            "...XXXXX...",
            "..XXXXXXX..",
            ".XXXXXXXXX.",
            "....XXX....",
            "....XXX....",
            "....XXX....",
            "....XXX....",
            "...........",
        };
        static readonly string[] Spiral = // tatsu: tornado
        {
            "...........",
            ".XXXXXXXXX.",
            "...........",
            "..XXXXXXX..",
            "...........",
            "...XXXXX...",
            "...........",
            "....XXX....",
            ".....X.....",
            "...........",
        };
        static readonly string[] Claw = // agarre: pinza abierta
        {
            "...........",
            "XX......XX.",
            "XXX....XXX.",
            ".XXX..XXX..",
            "..XX..XX...",
            "..XX..XX...",
            ".XXX..XXX..",
            "XXX....XXX.",
            "XX......XX.",
            "...........",
        };
        static readonly string[] Shield =
        {
            "XXXXXXXXXXX",
            "XXXXXXXXXXX",
            "XXXXXXXXXXX",
            "XXXXXXXXXXX",
            ".XXXXXXXXX.",
            ".XXXXXXXXX.",
            "..XXXXXXX..",
            "...XXXXX...",
            "....XXX....",
            ".....X.....",
        };
        static readonly string[] ChevR = // dash adelante
        {
            "...........",
            "X....X.....",
            "XX....XX...",
            ".XX....XX..",
            "..XX....XX.",
            "..XX....XX.",
            ".XX....XX..",
            "XX....XX...",
            "X....X.....",
            "...........",
        };
        static readonly string[] JumpArc = // salto: arco con flecha
        {
            "....XXX....",
            "...XXXXX...",
            "..XX.X.XX..",
            ".XX..X..XX.",
            ".X..XXX..X.",
            "....XXX....",
            "...........",
            "XXX.....XXX",
            "...........",
            "...........",
        };
        static readonly string[] Down = // agacharse
        {
            "...........",
            "...........",
            ".XXXXXXXXX.",
            ".XXXXXXXXX.",
            "..XXXXXXX..",
            "...XXXXX...",
            "....XXX....",
            ".....X.....",
            "...........",
            "...........",
        };
        static readonly string[] Star = // super
        {
            ".....X.....",
            ".....X.....",
            "....XXX....",
            "X..XXXXX..X",
            ".XXXXXXXXX.",
            "..XXXXXXX..",
            "...XXXXX...",
            "..XXX.XXX..",
            ".XX.....XX.",
            "...........",
        };
        static readonly string[] Walk = // caminar: chevron simple
        {
            "...........",
            "...X.......",
            "...XX......",
            "....XX.....",
            ".....XX....",
            ".....XX....",
            "....XX.....",
            "...XX......",
            "...X.......",
            "...........",
        };

        public static Sprite Get(int moveIndex)
        {
            switch (moveIndex)
            {
                case MoveCatalog.AttackA:
                case MoveCatalog.Strong: return Make("fist", Fist, flip: false);
                case MoveCatalog.AttackB:
                case MoveCatalog.LowKick: return Make("sweep", Sweep, flip: false);
                case MoveCatalog.Hadouken:
                case MoveCatalog.Super: return Make("ball", Ball, flip: false);
                case MoveCatalog.Shoryuken: return Make("rising", Rising, flip: false);
                case MoveCatalog.Tatsu: return Make("spiral", Spiral, flip: false);
                case MoveCatalog.Grab:
                case MoveCatalog.YomiGrab: return Make("claw", Claw, flip: false);
                case MoveCatalog.WalkB:
                case MoveCatalog.Parry: return Make("shield", Shield, flip: false);
                case MoveCatalog.DashF: return Make("chevR", ChevR, flip: false);
                case MoveCatalog.DashB: return Make("chevL", ChevR, flip: true);
                case MoveCatalog.JumpF:
                case MoveCatalog.JumpN:
                case MoveCatalog.JumpB: return Make("jump", JumpArc, flip: false);
                case MoveCatalog.Crouch: return Make("down", Down, flip: false);
                case MoveCatalog.WalkF: return Make("walk", Walk, flip: false);
                default: return null;
            }
        }

        public static Sprite ShieldSprite() => Make("shield", Shield, flip: false);
        public static Sprite StarSprite() => Make("star", Star, flip: false);

        // ---- el idioma de DUELO: ocho símbolos y nada más ----
        // El strip "LE QUEDAN" era `A·2 B·2 C·2` a 11px: críptico y además
        // ilegible. Con pictograma + número se escanea de un vistazo, que es
        // lo único que hace que la información pública SIRVA (Ley 5).
        // La ALTURA se dice por POSICIÓN dentro del cuadrito: arriba = alto,
        // abajo = bajo. Igual que la franja de las cartas.
        public enum Duel { StrikeHigh, StrikeLow, Grab, GuardHigh, GuardLow, Escape, Knockdown, Draw }

        static readonly string[] FistHigh =
        {
            "..XXXXXXX..",
            ".XXXXXXXXX.",
            "XXXXXXXXXXX",
            "XXXXXXXXXXX",
            ".XXXXXXXXX.",
            "..XXXXXXX..",
            "...........",
            "...........",
            "...........",
            "...........",
        };
        static readonly string[] SweepLow =
        {
            "...........",
            "...........",
            "...........",
            "...........",
            "XX.........",
            "XXXX.......",
            "XXXXXXX....",
            "XXXXXXXXXX.",
            "XXXXXXXXXXX",
            "XXXXXXXXXXX",
        };
        static readonly string[] ShieldHigh =
        {
            "XXXXXXXXXXX",
            "XXXXXXXXXXX",
            ".XXXXXXXXX.",
            "..XXXXXXX..",
            "...XXXXX...",
            ".....X.....",
            "...........",
            "...........",
            "...........",
            "...........",
        };
        static readonly string[] ShieldLow =
        {
            "...........",
            "...........",
            "...........",
            "...........",
            "XXXXXXXXXXX",
            "XXXXXXXXXXX",
            ".XXXXXXXXX.",
            "..XXXXXXX..",
            "...XXXXX...",
            ".....X.....",
        };
        static readonly string[] Fallen = // cuerpo tirado en el piso
        {
            "...........",
            "...........",
            "...........",
            "...........",
            "..XX.......",
            ".XXXXXXXXX.",
            "..XXXXXXXX.",
            "...........",
            "XXXXXXXXXXX",
            "...........",
        };
        static readonly string[] Cards2 = // robar: dos cartas
        {
            "..XXXXXXX..",
            "..X.....X..",
            "XXXXXXX.X..",
            "X.....X.X..",
            "X.....X.X..",
            "X.....XXX..",
            "X.....X....",
            "X.....X....",
            "XXXXXXX....",
            "...........",
        };

        public static Sprite Get(Duel d)
        {
            switch (d)
            {
                case Duel.StrikeHigh: return Make("dHi", FistHigh, flip: false);
                case Duel.StrikeLow: return Make("dLo", SweepLow, flip: false);
                case Duel.Grab: return Make("claw", Claw, flip: false);
                case Duel.GuardHigh: return Make("dGH", ShieldHigh, flip: false);
                case Duel.GuardLow: return Make("dGL", ShieldLow, flip: false);
                case Duel.Escape: return Make("star", Star, flip: false);
                case Duel.Knockdown: return Make("dKD", Fallen, flip: false);
                default: return Make("dDraw", Cards2, flip: false);
            }
        }

        // El pictograma que le corresponde a una carta de DUELO.
        public static Sprite Get(in DuelCard c)
        {
            switch (c.Kind)
            {
                case DuelKind.Grab: return Get(Duel.Grab);
                case DuelKind.Escape: return Get(Duel.Escape);
                case DuelKind.Guard:
                    return Get(c.Height == DuelHeight.High ? Duel.GuardHigh : Duel.GuardLow);
                default:
                    return Get(c.Height == DuelHeight.High ? Duel.StrikeHigh : Duel.StrikeLow);
            }
        }

        static Sprite Make(string key, string[] rows, bool flip)
        {
            string k = flip ? key + "~" : key;
            if (_cache.TryGetValue(k, out var s)) return s;
            int w = rows[0].Length, h = rows.Length;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int sx = flip ? w - 1 - x : x;
                    bool on = rows[h - 1 - y][sx] == 'X';
                    tex.SetPixel(x, y, on ? Color.white : Color.clear);
                }
            tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
            _cache[k] = s;
            return s;
        }
    }

    // Feedback EN el mundo: daño/eventos flotando sobre el peleador golpeado
    // y badges de estado (HITSTUN 12f) sobre las cabezas. Into the Breach:
    // la información vive donde pasa la acción.
    public class WorldFX : MonoBehaviour
    {
        static WorldFX _i;

        // Carriles verticales: cada TIPO de popup nace a su altura y no se
        // pisa con los demás (antes daño, ventaja y carteles compartían
        // y=2.15 y en cuanto pasaban dos cosas juntas era una sopa).
        public const int LaneResult = 0;   // daño, BLOQUEADO, PARRY, TECH
        public const int LaneAdv = 1;      // frame advantage (+2F), chiquito
        public const int LaneCallout = 2;  // nombre del move, arriba del badge
        static readonly float[] LaneY = { 2.15f, 1.78f, 2.62f };
        static readonly float[] LaneVel = { 1.15f, 0.7f, 0.85f };
        static readonly float[] LaneLife = { 1.15f, 0.9f, 1.05f };

        class Pop
        {
            public TextMesh Tm;
            public float Life, MaxLife;
            public Vector3 Vel;
            public int Lane;
            public float X;
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
        public static void Popup(float worldX, string msg, Color c, float scale = 1f, int lane = LaneResult)
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
            // apilado: si ya hay popups vivos del mismo carril cerca de esta x,
            // el nuevo nace más arriba en vez de nacer encima
            float y = LaneY[lane];
            int stacked = 0;
            foreach (var p in i._pops)
                if (p.Tm.gameObject.activeSelf && p.Lane == lane && Mathf.Abs(p.X - worldX) < 0.9f)
                    stacked++;
            y += Mathf.Min(stacked, 3) * 0.32f;
            pop.Lane = lane;
            pop.X = worldX;
            pop.Tm.text = msg;
            pop.Tm.color = c;
            pop.Tm.characterSize = 0.05f * scale;
            pop.Tm.transform.position = new Vector3(worldX, y, -0.6f);
            pop.Tm.gameObject.SetActive(true);
            pop.MaxLife = pop.Life = LaneLife[lane];
            pop.Vel = new Vector3(0f, LaneVel[lane], 0f);
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
