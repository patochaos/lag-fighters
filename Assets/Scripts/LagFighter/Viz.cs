using System.Collections.Generic;
using UnityEngine;

namespace LagFighter
{
    // Preferencias de visualización (toggle del botón CAJAS en el HUD).
    public static class VizPrefs
    {
        public static bool ShowBoxes = true;
    }

    // Materiales base cargados desde Resources: al ser assets referenciados,
    // sus shaders SÍ entran en la build (los creados 100% por código se
    // strippean y todo sale magenta, como pasó en la primera build).
    public static class MatLib
    {
        static Material _lit;

        public static Material Lit
        {
            get
            {
                if (_lit == null) _lit = Resources.Load<Material>("LagFighter/LitBase");
                return _lit;
            }
        }

        // Asigna material lit con color a un primitivo (fallback: tintar el default)
        public static void Apply(GameObject go, Color c)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
            if (Lit != null) r.material = new Material(Lit) { color = c };
            else r.material.color = c;
        }
    }

    // Cajas translúcidas para hurtboxes (verde/naranja) y hitboxes (rojo),
    // y el ghost del plan propio. Sprites/Default vía asset de Resources.
    public static class VizLib
    {
        static Material _mat;

        public static Material BaseMat
        {
            get
            {
                if (_mat == null)
                {
                    _mat = Resources.Load<Material>("LagFighter/GhostBase");
                    if (_mat == null) _mat = new Material(Shader.Find("Sprites/Default"));
                }
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

    // Ghost del plan: el MISMO blockman semi-transparente ejecutando tu plan
    // en loop, con su propia sim clonada (el rival queda con lo que ya tiene
    // comprometido). Muestra hurtbox y hitboxes del preview.
    public class GhostViz : MonoBehaviour
    {
        MatchSim _base;
        MatchSim _sim;
        int _fighter;
        int _t0;
        float _clock;
        float _loopFrames;
        bool _active;

        FighterView _ghost0, _ghost1;
        GameObject _hurtBox;
        readonly List<GameObject> _hitPool = new List<GameObject>();
        readonly List<WorldRect> _rects = new List<WorldRect>();

        public static GhostViz Create()
        {
            var go = new GameObject("LagFighter.Ghost");
            var v = go.AddComponent<GhostViz>();
            v._ghost0 = FighterView.CreateGhost(0);
            v._ghost0.transform.SetParent(go.transform);
            v._ghost1 = FighterView.CreateGhost(1);
            v._ghost1.transform.SetParent(go.transform);
            v._hurtBox = VizLib.MakeBox("GhostHurt", new Color(0.3f, 1f, 0.5f, 0.2f), go.transform);
            v.HideAll();
            return v;
        }

        FighterView Ghost(int i) => i == 0 ? _ghost0 : _ghost1;

        void HideAll()
        {
            _ghost0.gameObject.SetActive(false);
            _ghost1.gameObject.SetActive(false);
            _hurtBox.SetActive(false);
            foreach (var go in _hitPool) go.SetActive(false);
        }

        public void Clear()
        {
            _active = false;
            HideAll();
        }

        public void Show(MatchSim src, int fighter, List<int> plan, int turnFrames)
        {
            if (plan == null || plan.Count == 0)
            {
                Clear();
                return;
            }

            _fighter = fighter;
            _base = src.Clone();
            _base.SetQueue(fighter, plan);
            _base.SetQueue(1 - fighter, new List<int>());
            _t0 = _base.Tick;

            int planFrames = 0;
            foreach (var mi in plan) planFrames += MoveCatalog.All[mi].Total;
            _loopFrames = Mathf.Min(src.StunRemaining(fighter) + planFrames + 18, turnFrames + 18);

            _active = true;
            Ghost(fighter).gameObject.SetActive(true);
            Ghost(1 - fighter).gameObject.SetActive(false);
            Restart();
        }

        void Restart()
        {
            _sim = _base.Clone();
            _clock = 0f;
        }

        void Update()
        {
            if (!_active) return;
            float dt = Time.deltaTime;
            _clock += dt * SimConfig.TicksPerSecond;
            if (_clock >= _loopFrames) { Restart(); return; }

            while (_sim.Tick - _t0 < (int)_clock && !_sim.Over)
                _sim.Step();

            var ghost = Ghost(_fighter);
            ghost.ApplyPose(_sim, _t0 + _clock, dt, true);

            // cajas del ghost (respetan el toggle CAJAS)
            bool show = VizPrefs.ShowBoxes;
            _hurtBox.SetActive(show);
            if (show) VizLib.SetRect(_hurtBox, _sim.HurtRect(_fighter), 0.5f, 0f);

            _rects.Clear();
            if (show)
            {
                _sim.GetActiveHitRects(_fighter, _rects);
                _sim.GetProjectileRects(_fighter, _rects);
            }
            for (int i = 0; i < _rects.Count; i++)
            {
                if (i >= _hitPool.Count)
                    _hitPool.Add(VizLib.MakeBox("GhostHit", Color.white, transform));
                _hitPool[i].SetActive(true);
                _hitPool[i].GetComponent<Renderer>().material.color = _rects[i].Grab
                    ? new Color(0.9f, 0.25f, 0.8f, 0.35f)
                    : new Color(1f, 0.2f, 0.1f, 0.3f);
                VizLib.SetRect(_hitPool[i], _rects[i], 0.58f, 0f);
            }
            for (int i = _rects.Count; i < _hitPool.Count; i++)
                _hitPool[i].SetActive(false);
        }
    }
}
