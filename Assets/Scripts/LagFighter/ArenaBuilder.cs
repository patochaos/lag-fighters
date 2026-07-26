using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LagFighter
{
    // Referencias al escenario que la UI anima en vivo (glow de esquina) y que
    // el modo DUELO reviste con su propio look.
    public static class ArenaRefs
    {
        public static readonly Renderer[] Walls = new Renderer[2]; // 0 = izquierda, 1 = derecha
        public static Color WallBase;

        public static GameObject Root, Ground, Strip, Center, Backdrop;
        public static readonly System.Collections.Generic.List<GameObject> Buildings = new System.Collections.Generic.List<GameObject>();
        public static readonly System.Collections.Generic.List<GameObject> FloorLines = new System.Collections.Generic.List<GameObject>();
        public static readonly System.Collections.Generic.List<Renderer> Crowd = new System.Collections.Generic.List<Renderer>();
        public static readonly System.Collections.Generic.List<Color> CrowdBase = new System.Collections.Generic.List<Color>();
        public static GameObject DuelLight;   // el cono/disco de luz, solo en DUELO
    }

    // Escenario 2D de vista lateral, estilo Footsies: una línea de piso,
    // marcas de límites y centro, cámara fija de costado.
    public static class ArenaBuilder
    {
        // WebGL corre en un solo hilo y sin FSR: pipeline a lo esencial.
        // (El renderScale 0.8 del asset Mobile activaba FSR → warning por
        // frame y camino de upscaling roto en WebGL.)
        static void TuneForWebGL(Camera cam)
        {
            if (Application.platform != RuntimePlatform.WebGLPlayer) return;
            // SRP Batcher roto en WebGL2: los draws del Lit se emiten (verificado
            // hookeando el contexto GL: 68 draws del programa Lit por frame) pero
            // no pintan un solo pixel — los UBOs por objeto llegan rotos y la
            // geometría degenera. Arena y peleadores invisibles; Sprites/Default,
            // que va por el camino clásico sin batcher, dibuja normal.
            GraphicsSettings.useScriptableRenderPipelineBatching = false;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset rp)
            {
                rp.renderScale = 1f;
                rp.upscalingFilter = UpscalingFilterSelection.Linear; // FSR no existe acá
                rp.msaaSampleCount = 1;
                rp.supportsHDR = false;
                rp.shadowDistance = 0f;
            }
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = false;
            camData.antialiasing = AntialiasingMode.None;
        }

        public static void Build()
        {
            var root = new GameObject("LagFighter.Arena");
            ArenaRefs.Root = root;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(root.transform);
            ground.transform.localScale = new Vector3(1.4f, 1f, 0.8f);
            Tint(ground, new Color(0.10f, 0.11f, 0.14f));
            ArenaRefs.Ground = ground;

            // franja de juego y marcas
            var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            strip.name = "Strip";
            strip.transform.SetParent(root.transform);
            strip.transform.localPosition = new Vector3(0f, 0.012f, 0f);
            strip.transform.localScale = new Vector3(SimConfig.StageHalfWidth * 2f + 0.9f, 0.02f, 1.6f);
            Tint(strip, new Color(0.16f, 0.17f, 0.21f));
            ArenaRefs.Strip = strip;

            var center = GameObject.CreatePrimitive(PrimitiveType.Cube);
            center.name = "CenterLine";
            center.transform.SetParent(root.transform);
            center.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            center.transform.localScale = new Vector3(0.05f, 0.02f, 1.6f);
            Tint(center, new Color(0.35f, 0.37f, 0.44f));
            ArenaRefs.Center = center;

            ArenaRefs.WallBase = new Color(0.35f, 0.16f, 0.16f);
            for (int s = -1; s <= 1; s += 2)
            {
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "Wall" + s;
                wall.transform.SetParent(root.transform);
                wall.transform.localPosition = new Vector3(s * (SimConfig.StageHalfWidth + 0.5f), 0.9f, 0f);
                wall.transform.localScale = new Vector3(0.15f, 1.8f, 1.2f);
                Tint(wall, ArenaRefs.WallBase);
                // la UI las pulsa con el color del acorralado (feedback de esquina)
                ArenaRefs.Walls[s < 0 ? 0 : 1] = wall.GetComponent<Renderer>();
            }

            // líneas de piso: dan lectura de distancia de un vistazo
            for (int x = -4; x <= 4; x++)
            {
                if (x == 0) continue; // el centro ya tiene su línea
                var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
                line.name = "FloorLine" + x;
                line.transform.SetParent(root.transform);
                line.transform.localPosition = new Vector3(x, 0.018f, 0f);
                line.transform.localScale = new Vector3(0.025f, 0.02f, 1.6f);
                Tint(line, new Color(0.24f, 0.25f, 0.30f));
                ArenaRefs.FloorLines.Add(line);
            }

            // fondo para contraste
            var back = GameObject.CreatePrimitive(PrimitiveType.Cube);
            back.name = "Backdrop";
            back.transform.SetParent(root.transform);
            back.transform.localPosition = new Vector3(0f, 2.2f, 3.2f);
            back.transform.localScale = new Vector3(16f, 4.4f, 0.1f);
            Tint(back, new Color(0.07f, 0.08f, 0.11f));
            ArenaRefs.Backdrop = back;

            // skyline de bloques (determinista) detrás del backdrop
            var skyRng = new System.Random(42);
            for (int b = 0; b < 14; b++)
            {
                var bld = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bld.name = "Building" + b;
                bld.transform.SetParent(root.transform);
                float bx = -7.8f + b * 1.2f + (float)skyRng.NextDouble() * 0.5f;
                float bh = 1.2f + (float)skyRng.NextDouble() * 2.6f;
                bld.transform.localPosition = new Vector3(bx, bh * 0.5f + 0.4f, 3.05f);
                bld.transform.localScale = new Vector3(0.7f + (float)skyRng.NextDouble() * 0.5f, bh, 0.12f);
                float shade = 0.10f + (float)skyRng.NextDouble() * 0.05f;
                Tint(bld, new Color(shade, shade + 0.012f, shade + 0.035f));
                ArenaRefs.Buildings.Add(bld);
            }

            // público de bloques que se mueve (adelante del backdrop, atrás de la pista)
            var crowdRng = new System.Random(7);
            for (int c = 0; c < 22; c++)
            {
                var fan = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fan.name = "Fan" + c;
                fan.transform.SetParent(root.transform);
                float fx = -5.2f + c * 0.5f + (float)crowdRng.NextDouble() * 0.25f;
                fan.transform.localPosition = new Vector3(fx, 0.55f, 2.35f);
                fan.transform.localScale = new Vector3(0.26f, 0.55f, 0.2f);
                var hue = 0.28f + (float)crowdRng.NextDouble() * 0.5f;
                var fanCol = Color.HSVToRGB(hue, 0.25f, 0.28f);
                Tint(fan, fanCol);
                ArenaRefs.Crowd.Add(fan.GetComponent<Renderer>());
                ArenaRefs.CrowdBase.Add(fanCol);
                var bob = fan.AddComponent<CrowdBob>();
                bob.Phase = (float)crowdRng.NextDouble() * Mathf.PI * 2f;
                bob.Speed = 1.6f + (float)crowdRng.NextDouble() * 1.8f;
            }

            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            cam.transform.position = new Vector3(0f, 1.45f, -7.2f);
            cam.transform.rotation = Quaternion.Euler(4f, 0f, 0f);
            cam.fieldOfView = 45f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.06f, 0.09f);
            TuneForWebGL(cam);

            if (Object.FindAnyObjectByType<Light>() == null)
            {
                var lightGo = new GameObject("Directional Light");
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                lightGo.transform.rotation = Quaternion.Euler(45f, -25f, 0f);
                light.intensity = 1.1f;
            }

            // luces de acento por lado (celeste P1 / naranja P2): despegan a los
            // blockmen del fondo gris y refuerzan la identidad de cada esquina.
            // En WebGL NO: las point lights piden la variante _ADDITIONAL_LIGHTS
            // del Lit, que la build web strippea, y el forward pass entero deja
            // de dibujar lo opaco (arena y peleadores invisibles).
            if (Application.platform == RuntimePlatform.WebGLPlayer) return;
            for (int s = 0; s < 2; s++)
            {
                var accGo = new GameObject(s == 0 ? "AccentLightP1" : "AccentLightP2");
                accGo.transform.SetParent(root.transform);
                accGo.transform.localPosition = new Vector3(s == 0 ? -3.2f : 3.2f, 2.6f, -2.2f);
                var acc = accGo.AddComponent<Light>();
                acc.type = LightType.Point;
                acc.color = s == 0 ? new Color(0.25f, 0.7f, 0.95f) : new Color(0.95f, 0.45f, 0.25f);
                acc.intensity = 0.9f;
                acc.range = 6.5f;
                acc.shadows = LightShadows.None;
            }
        }

        public static void Tint(GameObject go, Color c) => MatLib.Apply(go, c);

        // ---- EL FEED: el escenario de DUELO (DUELO-LOOK.md §2) ----
        //
        // En DUELO el escenario no es una arena, es un CUARTO OSCURO filmado:
        // un cono de luz, dos siluetas, público en negro puro. Se van el
        // skyline, las paredes rojas, las líneas de distancia (en DUELO no hay
        // distancia) y el público de colores, que era lo que hacía que el 55%
        // del alto de la pantalla compitiera contra las cartas sin decir nada.
        //
        // Es un REVESTIMIENTO, no una arena aparte: se pinta y se despinta con
        // el mismo Build() de siempre, así los otros modos no se enteran.
        static bool _duelLook;

        public static void SetDuelStage(bool on)
        {
            if (ArenaRefs.Root == null || _duelLook == on) return;
            _duelLook = on;

            if (ArenaRefs.Ground != null)
                Tint(ArenaRefs.Ground, on ? Duelo.Void : new Color(0.10f, 0.11f, 0.14f));
            if (ArenaRefs.Strip != null)
                Tint(ArenaRefs.Strip, on ? Duelo.Stage : new Color(0.16f, 0.17f, 0.21f));
            if (ArenaRefs.Center != null)
                Tint(ArenaRefs.Center, on ? Duelo.Line : new Color(0.35f, 0.37f, 0.44f));
            if (ArenaRefs.Backdrop != null)
                Tint(ArenaRefs.Backdrop, on ? Duelo.Void : new Color(0.07f, 0.08f, 0.11f));

            // el skyline y las paredes se van: en un cuarto no hay ciudad
            foreach (var b in ArenaRefs.Buildings) if (b != null) b.SetActive(!on);
            foreach (var l in ArenaRefs.FloorLines) if (l != null) l.SetActive(!on);
            for (int s = 0; s < 2; s++)
                if (ArenaRefs.Walls[s] != null) ArenaRefs.Walls[s].gameObject.SetActive(!on);

            // el público queda, pero en NEGRO PURO: sigue dando vida (salta con
            // el KO) y no aporta un solo pixel de ruido cromático.
            for (int c = 0; c < ArenaRefs.Crowd.Count; c++)
                if (ArenaRefs.Crowd[c] != null)
                    ArenaRefs.Crowd[c].material.color = on
                        ? new Color(0.016f, 0.027f, 0.047f) : ArenaRefs.CrowdBase[c];

            if (on && ArenaRefs.DuelLight == null) BuildDuelLight();
            if (ArenaRefs.DuelLight != null) ArenaRefs.DuelLight.SetActive(on);

            var cam = Camera.main;
            if (cam != null)
            {
                // encuadre de DUELO: más cerca y más bajo, para que los cuerpos
                // ocupen el centro de la franja libre entre el HUD y la mano.
                cam.transform.position = on ? new Vector3(0f, 0.85f, -6.0f) : new Vector3(0f, 1.45f, -7.2f);
                cam.transform.rotation = Quaternion.Euler(on ? 2f : 4f, 0f, 0f);
                cam.backgroundColor = on ? Duelo.Void : new Color(0.05f, 0.06f, 0.09f);
            }
        }

        // La luz, hecha de geometría (nada de post-proceso ni point lights: las
        // dos cosas están rotas o strippeadas en WebGL).
        //
        // Se probó con láminas translúcidas subiendo hacia una lámpara y se
        // descartó: a la vista lateral no se leen como un cono, se leen como
        // tres cajas grises tapando a los peleadores. Dos discos concéntricos
        // en el piso dicen "hay una luz colgando" mucho mejor y no ocupan
        // ni un pixel del aire donde pasa la acción.
        static void BuildDuelLight()
        {
            var go = new GameObject("DuelLight");
            go.transform.SetParent(ArenaRefs.Root.transform, false);
            ArenaRefs.DuelLight = go;

            Disc(go.transform, "PoolOuter", 6.6f, 2.9f, 0.028f, Duelo.Alpha(Duelo.StageLit, 0.55f));
            Disc(go.transform, "PoolInner", 4.4f, 2.0f, 0.034f,
                new Color(Duelo.StageLit.r * 1.5f, Duelo.StageLit.g * 1.5f, Duelo.StageLit.b * 1.55f, 0.75f));
        }

        // LA APUESTA SE VE EN LA LUZ. Con el truco armado el cuarto se pone
        // dorado, y más fuerte con cada subida: ×2 tibio, ×4 incandescente.
        // Es la señal más barata y más honesta que tiene el juego — no hay que
        // leer un número para saber que este intercambio vale plata.
        static Renderer _stakeInner, _stakeOuter;

        public static void SetDuelStakes(int mult)
        {
            if (ArenaRefs.DuelLight == null) return;
            if (_stakeInner == null)
            {
                var i = ArenaRefs.DuelLight.transform.Find("PoolInner");
                var o = ArenaRefs.DuelLight.transform.Find("PoolOuter");
                if (i != null) _stakeInner = i.GetComponent<Renderer>();
                if (o != null) _stakeOuter = o.GetComponent<Renderer>();
            }
            // mult: 0/1 = sin apuesta · 2,3,4 = truco armado
            float k = mult <= 1 ? 0f : Mathf.Clamp01((mult - 1) / 3f);
            var baseIn = new Color(Duelo.StageLit.r * 1.5f, Duelo.StageLit.g * 1.5f, Duelo.StageLit.b * 1.55f, 0.75f);
            var hot = new Color(Duelo.Gold.r, Duelo.Gold.g * 0.86f, Duelo.Gold.b * 0.35f, 0.8f);
            if (_stakeInner != null) _stakeInner.material.color = Color.Lerp(baseIn, hot, k);
            if (_stakeOuter != null)
                _stakeOuter.material.color = Color.Lerp(Duelo.Alpha(Duelo.StageLit, 0.55f),
                    Duelo.Alpha(hot, 0.4f), k);
        }

        static void Disc(Transform parent, string name, float w, float d, float y, Color c)
        {
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = name;
            disc.transform.SetParent(parent, false);
            disc.transform.localPosition = new Vector3(0f, y, 0.05f);
            disc.transform.localScale = new Vector3(w, 0.01f, d);
            var col = disc.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            var r = disc.GetComponent<Renderer>();
            r.material = new Material(VizLib.BaseMat) { color = c };
            r.shadowCastingMode = ShadowCastingMode.Off;
        }
    }

    // Un bloque de público que salta en su lugar, cada uno a su ritmo.
    // En el KO se emociona: salta más rápido y más alto un rato.
    public class CrowdBob : MonoBehaviour
    {
        public static float ExciteUntil; // Excite() lo setea desde el KO

        public float Phase, Speed = 2f;
        float _baseY, _t;

        public static void Excite(float seconds) => ExciteUntil = Time.time + seconds;

        void Start() { _baseY = transform.localPosition.y; }

        void Update()
        {
            bool hyped = Time.time < ExciteUntil;
            _t += Time.deltaTime * Speed * (hyped ? 2.8f : 1f); // fase acumulada: sin saltos al cambiar de ritmo
            var p = transform.localPosition;
            p.y = _baseY + Mathf.Max(0f, Mathf.Sin(_t + Phase)) * (hyped ? 0.17f : 0.10f);
            transform.localPosition = p;
        }
    }
}
