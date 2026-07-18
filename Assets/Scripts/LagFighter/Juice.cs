using System;
using UnityEngine;

namespace LagFighter
{
    // Feedback sensorial barato: sonidos sintetizados en runtime (cero assets)
    // y shake de cámara. El hitstop vive en MatchController (pausa cosmética
    // del avance de ticks; no toca la sim).
    public static class SfxLib
    {
        public enum Kind { Hit, Counter, Block, Ko, Fireball, TurnStart, Glitch }

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

            _clips = new AudioClip[7];
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

        public static void Play(float volume = 0.85f)
        {
            if (!Enabled) return;
            if (!_loaded)
            {
                _clip = Resources.Load<AudioClip>("LagFighter/announcer");
                _loaded = true;
            }
            if (_clip != null) SfxLib.PlayClip(_clip, volume);
        }
    }

    // Hit-sparks: ráfaga de cubitos que salen despedidos del punto de contacto.
    // Mismo lenguaje visual que los blockmen; cero assets de partículas.
    public static class SparkFX
    {
        public static void Burst(Vector3 pos, Color color, int count = 9, float speed = 3.2f)
        {
            var rng = new System.Random((int)(Time.realtimeSinceStartup * 1000f));
            for (int i = 0; i < count; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Spark";
                var col = go.GetComponent<Collider>();
                if (col != null) UnityEngine.Object.Destroy(col);
                float s = 0.05f + (float)rng.NextDouble() * 0.06f;
                go.transform.position = pos;
                go.transform.localScale = new Vector3(s, s, s);
                go.transform.rotation = UnityEngine.Random.rotation;
                var r = go.GetComponent<Renderer>();
                r.material = new Material(VizLib.BaseMat) { color = color };
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                var shard = go.AddComponent<SparkShard>();
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                float up = 0.4f + (float)rng.NextDouble() * 1.2f;
                shard.Vel = new Vector3(Mathf.Cos(ang), up, Mathf.Sin(ang) * 0.35f).normalized
                            * speed * (0.55f + (float)rng.NextDouble() * 0.7f);
            }
        }
    }

    public class SparkShard : MonoBehaviour
    {
        public Vector3 Vel;
        float _life = 0.5f;

        void Update()
        {
            float dt = Time.deltaTime;
            _life -= dt;
            if (_life <= 0f) { Destroy(gameObject); return; }
            Vel += Vector3.down * (9f * dt);
            transform.position += Vel * dt;
            transform.localScale *= 1f - 3.4f * dt;
        }
    }

    // Sacudida de cámara al conectar golpes. Se agrega a la Main Camera.
    public class CameraFX : MonoBehaviour
    {
        Vector3 _base;
        float _mag;

        void Awake() { _base = transform.position; }

        public void Shake(float magnitude) { _mag = Mathf.Max(_mag, magnitude); }

        void LateUpdate()
        {
            _mag = Mathf.Lerp(_mag, 0f, 1f - Mathf.Exp(-9f * Time.deltaTime));
            var off = _mag > 0.001f
                ? new Vector3((Mathf.PerlinNoise(Time.time * 30f, 0f) - 0.5f), (Mathf.PerlinNoise(0f, Time.time * 30f) - 0.5f), 0f) * (2f * _mag)
                : Vector3.zero;
            transform.position = _base + off;
        }
    }
}
