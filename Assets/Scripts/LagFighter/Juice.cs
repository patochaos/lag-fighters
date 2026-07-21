using System;
using System.Collections.Generic;
using UnityEngine;

namespace LagFighter
{
    // Feedback sensorial barato: sonidos sintetizados en runtime (cero assets)
    // y shake de cámara. El hitstop vive en MatchController (pausa cosmética
    // del avance de ticks; no toca la sim).
    public static class SfxLib
    {
        public enum Kind { Hit, Counter, Block, Ko, Fireball, TurnStart, Glitch, UiTick, UiClick, UiCancel }

        // master de SFX (botón en OPC, persiste). La VOZ tiene su propio toggle.
        static bool _enabled = true;
        static bool _prefLoaded;
        public static bool Enabled
        {
            get
            {
                if (!_prefLoaded) { _prefLoaded = true; _enabled = PlayerPrefs.GetInt("lf_sfx", 1) == 1; }
                return _enabled;
            }
            set
            {
                _prefLoaded = true;
                _enabled = value;
                PlayerPrefs.SetInt("lf_sfx", value ? 1 : 0);
            }
        }

        static AudioSource _source;
        static AudioClip[] _clips;

        static void EnsureInit()
        {
            if (_source != null) return;
            var go = new GameObject("LagFighter.Audio");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _source = go.AddComponent<AudioSource>();
            _source.spatialBlend = 0f;

            var rng = new System.Random(7);
            Func<float> noise = () => (float)(rng.NextDouble() * 2.0 - 1.0);

            _clips = new AudioClip[10];
            _clips[(int)Kind.Hit] = Make("hit", 0.11f, t =>
                Mathf.Sin(t * 140f * Mathf.PI * 2f) * 0.9f * Mathf.Exp(-t * 28f) + noise() * 0.45f * Mathf.Exp(-t * 40f));
            _clips[(int)Kind.Counter] = Make("counter", 0.14f, t =>
                Mathf.Sin(t * 230f * Mathf.PI * 2f) * 0.9f * Mathf.Exp(-t * 22f) + noise() * 0.5f * Mathf.Exp(-t * 30f));
            _clips[(int)Kind.Block] = Make("block", 0.07f, t =>
                Mathf.Sin(t * 750f * Mathf.PI * 2f) * 0.5f * Mathf.Exp(-t * 50f));
            _clips[(int)Kind.Ko] = Make("ko", 0.5f, t =>
                Mathf.Sin(t * 70f * Mathf.PI * 2f) * Mathf.Exp(-t * 5f) + noise() * 0.6f * Mathf.Exp(-t * 25f));
            _clips[(int)Kind.Fireball] = Make("fireball", 0.18f, t =>
                noise() * 0.4f * Mathf.Exp(-t * 14f) + Mathf.Sin(t * 320f * Mathf.PI * 2f) * 0.25f * Mathf.Exp(-t * 12f));
            _clips[(int)Kind.TurnStart] = Make("turn", 0.06f, t =>
                Mathf.Sin(t * 660f * Mathf.PI * 2f) * 0.35f * Mathf.Exp(-t * 30f));
            // estática entrecortada: la conexión empeorando (subida de lag)
            _clips[(int)Kind.Glitch] = Make("glitch", 0.45f, t =>
                (Mathf.Sin(t * 30f * Mathf.PI * 2f) > 0f ? 1f : 0.15f) * noise() * 0.5f * Mathf.Exp(-t * 4f));
            // blips de UI: tick suave (hover/navegar), click (agregar/confirmar),
            // cancel grave (borrar/deshacer)
            _clips[(int)Kind.UiTick] = Make("uitick", 0.035f, t =>
                Mathf.Sin(t * 880f * Mathf.PI * 2f) * 0.3f * Mathf.Exp(-t * 60f));
            _clips[(int)Kind.UiClick] = Make("uiclick", 0.06f, t =>
                Mathf.Sin(t * 620f * Mathf.PI * 2f) * 0.45f * Mathf.Exp(-t * 40f) +
                Mathf.Sin(t * 930f * Mathf.PI * 2f) * 0.2f * Mathf.Exp(-t * 55f));
            _clips[(int)Kind.UiCancel] = Make("uicancel", 0.07f, t =>
                Mathf.Sin(t * 300f * Mathf.PI * 2f) * 0.4f * Mathf.Exp(-t * 35f));
        }

        static AudioClip Make(string name, float dur, Func<float, float> wave)
        {
            const int rate = 44100;
            int n = (int)(rate * dur);
            var data = new float[n];
            for (int i = 0; i < n; i++)
                data[i] = Mathf.Clamp(wave(i / (float)rate), -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static void Play(Kind kind, float volume = 1f)
        {
            if (!Enabled) return;
            EnsureInit();
            _source.PlayOneShot(_clips[(int)kind], volume);
        }

        public static void PlayClip(AudioClip clip, float volume = 1f)
        {
            EnsureInit();
            _source.PlayOneShot(clip, volume);
        }
    }

    // Announcer: el mp3 de Resources vuelve, pero SOLO en momentos (KO, guard
    // crush) y con toggle en el HUD. En el menú molestaba.
    public static class Announcer
    {
        public static bool Enabled = true;
        static AudioClip _clip;
        static bool _loaded;
        static AudioSource _src; // fuente propia: el pitch aleatorio no toca los SFX

        public static void Play(float volume = 0.85f)
        {
            if (!Enabled) return;
            if (!_loaded)
            {
                _clip = Resources.Load<AudioClip>("LagFighter/announcer");
                _loaded = true;
            }
            if (_clip == null) return;
            if (_src == null)
            {
                var go = new GameObject("LagFighter.Announcer");
                UnityEngine.Object.DontDestroyOnLoad(go);
                _src = go.AddComponent<AudioSource>();
                _src.spatialBlend = 0f;
            }
            // ±8% de pitch por reproducción: disimula que es siempre el mismo mp3
            _src.pitch = 0.92f + UnityEngine.Random.value * 0.16f;
            _src.PlayOneShot(_clip, volume);
        }
    }

    // Hit-sparks: ráfaga de cubitos que salen despedidos del punto de contacto.
    // Mismo lenguaje visual que los blockmen; cero assets de partículas.
    // POOL fijo: cada cubo, su material y su componente se crean UNA vez y se
    // reciclan — clave en WebGL, donde crear/instanciar por impacto picaba CPU.
    public static class SparkFX
    {
        const int PoolSize = 48;
        static readonly List<SparkShard> _pool = new List<SparkShard>(PoolSize);
        static System.Random _rng = new System.Random(1234);

        static SparkShard Get()
        {
            foreach (var s in _pool)
                if (!s.gameObject.activeSelf) return s;
            if (_pool.Count >= PoolSize) return null; // sin cubos libres: se pierde el spark, nadie llora

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Spark";
            var col = go.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.Destroy(col);
            var r = go.GetComponent<Renderer>();
            r.material = new Material(VizLib.BaseMat);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var shard = go.AddComponent<SparkShard>();
            shard.Rend = r;
            UnityEngine.Object.DontDestroyOnLoad(go);
            _pool.Add(shard);
            return shard;
        }

        public static void Burst(Vector3 pos, Color color, int count = 9, float speed = 3.2f)
        {
            for (int i = 0; i < count; i++)
            {
                var shard = Get();
                if (shard == null) return;
                float s = 0.05f + (float)_rng.NextDouble() * 0.06f;
                shard.transform.position = pos;
                shard.BaseScale = s;
                shard.transform.localScale = new Vector3(s, s, s);
                shard.transform.rotation = Quaternion.Euler((float)_rng.NextDouble() * 360f, (float)_rng.NextDouble() * 360f, 0f);
                shard.Rend.material.color = color;
                float ang = (float)_rng.NextDouble() * Mathf.PI * 2f;
                float up = 0.4f + (float)_rng.NextDouble() * 1.2f;
                shard.Vel = new Vector3(Mathf.Cos(ang), up, Mathf.Sin(ang) * 0.35f).normalized
                            * speed * (0.55f + (float)_rng.NextDouble() * 0.7f);
                shard.Gravity = 9f;
                shard.Life = 0.5f;
                shard.gameObject.SetActive(true);
            }
        }

        // Polvo de piso: cubitos grises rastreros (aterrizajes, dash, wakeup).
        // El movimiento se LEE cuando levanta tierra.
        public static void Dust(Vector3 pos, int count = 6)
        {
            for (int i = 0; i < count; i++)
            {
                var shard = Get();
                if (shard == null) return;
                float s = 0.045f + (float)_rng.NextDouble() * 0.05f;
                float g = 0.5f + (float)_rng.NextDouble() * 0.12f;
                shard.transform.position = pos + new Vector3(((float)_rng.NextDouble() - 0.5f) * 0.4f, 0.04f, 0f);
                shard.BaseScale = s;
                shard.transform.localScale = new Vector3(s, s, s);
                shard.transform.rotation = Quaternion.Euler(0f, (float)_rng.NextDouble() * 360f, 0f);
                shard.Rend.material.color = new Color(g, g, g + 0.03f, 0.85f);
                float ang = (float)_rng.NextDouble() * Mathf.PI * 2f;
                shard.Vel = new Vector3(Mathf.Cos(ang), 0.25f + (float)_rng.NextDouble() * 0.45f, Mathf.Sin(ang) * 0.3f)
                            * (1.1f + (float)_rng.NextDouble() * 0.9f);
                shard.Gravity = 3f; // flota un toque más que un spark de impacto
                shard.Life = 0.42f;
                shard.gameObject.SetActive(true);
            }
        }
    }

    public class SparkShard : MonoBehaviour
    {
        public Vector3 Vel;
        public float Life;
        public float BaseScale;
        public float Gravity = 9f;
        public Renderer Rend;

        void Update()
        {
            float dt = Time.deltaTime;
            Life -= dt;
            if (Life <= 0f) { gameObject.SetActive(false); return; } // vuelve al pool
            Vel += Vector3.down * (Gravity * dt);
            transform.position += Vel * dt;
            transform.localScale *= 1f - 3.4f * dt;
        }
    }

    // Afterimages del dash: siluetas translúcidas que quedan atrás y se
    // desvanecen en ~0.2s — a velocidad real el dash era un teleport visual;
    // con estela se lee como VELOCIDAD. Pool fijo, mismo criterio que SparkFX.
    public static class AfterimageFX
    {
        const int PoolSize = 10;
        static readonly List<AfterimageGhost> _pool = new List<AfterimageGhost>(PoolSize);

        public static void Spawn(Vector3 pos, Color c)
        {
            AfterimageGhost g = null;
            foreach (var a in _pool)
                if (!a.gameObject.activeSelf) { g = a; break; }
            if (g == null)
            {
                if (_pool.Count >= PoolSize) return;
                var go = new GameObject("Afterimage");
                UnityEngine.Object.DontDestroyOnLoad(go);
                g = go.AddComponent<AfterimageGhost>();
                g.Build();
                _pool.Add(g);
            }
            g.Show(pos, c);
        }
    }

    public class AfterimageGhost : MonoBehaviour
    {
        const float MaxLife = 0.22f;
        Renderer[] _rs;
        Color _c;
        float _life;

        public void Build()
        {
            // silueta mínima del blockman: cuerpo + cabeza (2 cubos alcanzan
            // para leer "acá estaba hace un instante")
            _rs = new Renderer[2];
            _rs[0] = Box(new Vector3(0f, 0.95f, 0f), new Vector3(0.44f, 1.5f, 0.28f));
            _rs[1] = Box(new Vector3(0f, 1.66f, 0f), new Vector3(0.26f, 0.26f, 0.26f));
        }

        Renderer Box(Vector3 pos, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var r = go.GetComponent<Renderer>();
            r.material = new Material(VizLib.BaseMat);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return r;
        }

        public void Show(Vector3 pos, Color c)
        {
            transform.position = pos;
            _c = c;
            _life = MaxLife;
            gameObject.SetActive(true);
        }

        void Update()
        {
            _life -= Time.deltaTime;
            if (_life <= 0f) { gameObject.SetActive(false); return; }
            float a = 0.30f * (_life / MaxLife);
            foreach (var r in _rs)
            {
                var col = _c;
                col.a = a;
                r.material.color = col;
            }
        }
    }

    // Sacudida + micro-zoom de cámara. Se agrega a la Main Camera.
    //  - Shake: ruido corto al conectar golpes (ya existía)
    //  - Punch: dolly-in que decae rápido (hits fuertes, KO) — el impacto PESA
    //  - SetDistance: framing suave según separación (acerca un pelín cuando
    //    están encima, abre cuando se alejan). SUTIL a propósito: la cámara
    //    fija es parte de la lectura de distancias, siempre vuelve a la base.
    public class CameraFX : MonoBehaviour
    {
        Vector3 _base;
        float _mag;
        float _punch;          // dolly-in instantáneo, decae exp
        float _frameZ;         // framing suavizado
        float _frameTargetZ;

        void Awake() { _base = transform.position; }

        public void Shake(float magnitude) { _mag = Mathf.Max(_mag, magnitude); }

        public void Punch(float amount) { _punch = Mathf.Max(_punch, amount); }

        public void SetDistance(float dist)
            => _frameTargetZ = Mathf.Clamp((2.6f - dist) * 0.16f, -0.45f, 0.3f);

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            _mag = Mathf.Lerp(_mag, 0f, 1f - Mathf.Exp(-9f * dt));
            _punch = Mathf.Lerp(_punch, 0f, 1f - Mathf.Exp(-7f * dt));
            _frameZ = Mathf.Lerp(_frameZ, _frameTargetZ, 1f - Mathf.Exp(-2.5f * dt));
            var off = _mag > 0.001f
                ? new Vector3((Mathf.PerlinNoise(Time.time * 30f, 0f) - 0.5f), (Mathf.PerlinNoise(0f, Time.time * 30f) - 0.5f), 0f) * (2f * _mag)
                : Vector3.zero;
            off.z += _frameZ + _punch; // la cámara mira +z: sumar z = acercarse
            transform.position = _base + off;
        }
    }
}
