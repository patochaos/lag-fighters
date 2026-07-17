using System.Collections.Generic;
using UnityEngine;

namespace LagFighter
{
    // Preferencias de visualización (toggle del botón CAJAS en el HUD).
    public static class VizPrefs
    {
        public static bool ShowBoxes = true;
    }

    // Cajas translúcidas para hurtboxes (verde/naranja) y hitboxes (rojo),
    // y el ghost del plan propio. Shader Sprites/Default: transparente,
    // sin luz, siempre incluido en builds.
    public static class VizLib
    {
        static Material _mat;

        public static Material BaseMat
        {
            get
            {
                if (_mat == null) _mat = new Material(Shader.Find("Sprites/Default"));
                return _mat;
            }
        }

        public static GameObject MakeBox(string name, Color c, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            var r = go.GetComponent<Renderer>();
            r.material = new Material(BaseMat) { color = c };
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        public static void SetRect(GameObject go, WorldRect rect, float depth, float z)
        {
            go.transform.position = new Vector3((rect.X0 + rect.X1) * 0.5f, (rect.Y0 + rect.Y1) * 0.5f, z);
            go.transform.localScale = new Vector3(rect.X1 - rect.X0, rect.Y1 - rect.Y0, depth);
        }
    }

    // Hurtboxes, hitboxes activas y proyectiles de la partida en vivo.
    public class LiveViz : MonoBehaviour
    {
        MatchController _mc;
        readonly GameObject[] _hurt = new GameObject[2];
        readonly List<GameObject> _hitPool = new List<GameObject>();
        readonly List<GameObject> _projPool = new List<GameObject>();
        readonly List<WorldRect> _rects = new List<WorldRect>();

        public static LiveViz Create(MatchController mc)
        {
            var go = new GameObject("LagFighter.LiveViz");
            var v = go.AddComponent<LiveViz>();
            v._mc = mc;
            v._hurt[0] = VizLib.MakeBox("Hurt0", new Color(0.3f, 1f, 0.5f, 0.16f), go.transform);
            v._hurt[1] = VizLib.MakeBox("Hurt1", new Color(1f, 0.75f, 0.3f, 0.16f), go.transform);
            return v;
        }

        void Update()
        {
            var sim = _mc.Sim;
            if (sim == null) return;

            bool show = VizPrefs.ShowBoxes;
            for (int i = 0; i < 2; i++)
            {
                _hurt[i].SetActive(show);
                if (show) VizLib.SetRect(_hurt[i], sim.HurtRect(i), 0.55f, 0f);
            }

            _rects.Clear();
            if (show)
            {
                sim.GetActiveHitRects(0, _rects);
                sim.GetActiveHitRects(1, _rects);
                sim.GetProjectileRects(0, _rects);
                sim.GetProjectileRects(1, _rects);
            }
            for (int i = 0; i < _rects.Count; i++)
            {
                if (i >= _hitPool.Count)
                    _hitPool.Add(VizLib.MakeBox("Hit", Color.white, transform));
                _hitPool[i].SetActive(true);
                // agarre en magenta para que se lea distinto de un golpe
                _hitPool[i].GetComponent<Renderer>().material.color = _rects[i].Grab
                    ? new Color(0.9f, 0.25f, 0.8f, 0.45f)
                    : new Color(1f, 0.15f, 0.1f, 0.4f);
                VizLib.SetRect(_hitPool[i], _rects[i], 0.62f, 0f);
            }
            for (int i = _rects.Count; i < _hitPool.Count; i++)
                _hitPool[i].SetActive(false);

            // bolas de energía de los hadoukens
            int used = 0;
            foreach (var p in sim.Projectiles)
            {
                if (!p.Alive) continue;
                if (used >= _projPool.Count)
                {
                    var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    ball.name = "Hadouken";
                    ball.transform.SetParent(transform, false);
                    var c = ball.GetComponent<Collider>();
                    if (c != null) Destroy(c);
                    ball.GetComponent<Renderer>().material = new Material(VizLib.BaseMat) { color = new Color(0.5f, 0.8f, 1f, 0.9f) };
                    _projPool.Add(ball);
                }
                var go = _projPool[used++];
                go.SetActive(true);
                go.transform.position = new Vector3(p.X, (SimConfig.ProjY0 + SimConfig.ProjY1) * 0.5f, 0f);
                float pulse = 0.42f + Mathf.Sin(Time.time * 14f) * 0.04f;
                go.transform.localScale = new Vector3(pulse, pulse, pulse);
            }
            for (int i = used; i < _projPool.Count; i++)
                _projPool[i].SetActive(false);
        }
    }

    // Ghost del plan: siluetas translúcidas de tu trayectoria a lo largo del
    // turno + las hitboxes que vas a tirar (contra rival quieto).
    public class GhostViz : MonoBehaviour
    {
        readonly List<GameObject> _bodyPool = new List<GameObject>();
        readonly List<GameObject> _rectPool = new List<GameObject>();
        int _bodyUsed, _rectUsed;

        public static GhostViz Create()
        {
            return new GameObject("LagFighter.Ghost").AddComponent<GhostViz>();
        }

        public void Clear()
        {
            _bodyUsed = _rectUsed = 0;
            foreach (var go in _bodyPool) go.SetActive(false);
            foreach (var go in _rectPool) go.SetActive(false);
        }

        void Update()
        {
            // el toggle CAJAS también apaga las hitboxes del ghost (no las siluetas)
            for (int i = 0; i < _rectUsed; i++)
                _rectPool[i].SetActive(VizPrefs.ShowBoxes);
        }

        public void Show(PlanPreview g, int fighter)
        {
            Clear();
            var bodyC = fighter == 0 ? new Color(0.3f, 0.8f, 1f, 0.10f) : new Color(1f, 0.5f, 0.4f, 0.10f);

            // silueta cada ~30 frames (medio segundo) + posición final marcada
            for (int t = 0; t < g.Snaps.Count; t += 30)
                Body(g.Snaps[t].X, bodyC);
            if (g.Snaps.Count > 0)
            {
                var c = bodyC; c.a = 0.35f;
                Body(g.Snaps[g.Snaps.Count - 1].X, c);
            }

            // hitboxes del plan (muestreadas; los agarres en magenta)
            for (int t = 0; t < g.Snaps.Count; t += 3)
                foreach (var r in g.Snaps[t].HitRects)
                {
                    var box = Next(_rectPool, ref _rectUsed);
                    box.GetComponent<Renderer>().material.color = r.Grab
                        ? new Color(0.9f, 0.25f, 0.8f, 0.13f)
                        : new Color(1f, 0.2f, 0.1f, 0.10f);
                    VizLib.SetRect(box, r, 0.45f, 0f);
                }
        }

        void Body(float x, Color c)
        {
            var go = Next(_bodyPool, ref _bodyUsed);
            go.GetComponent<Renderer>().material.color = c;
            go.transform.position = new Vector3(x, SimConfig.HurtHeight * 0.5f, 0f);
            go.transform.localScale = new Vector3(SimConfig.HurtHalfWidth * 2f, SimConfig.HurtHeight, 0.4f);
        }

        GameObject Next(List<GameObject> pool, ref int used)
        {
            if (used < pool.Count)
            {
                var go = pool[used++];
                go.SetActive(true);
                return go;
            }
            var box = VizLib.MakeBox("GhostBox", Color.white, transform);
            pool.Add(box);
            used = pool.Count;
            return box;
        }
    }
}
