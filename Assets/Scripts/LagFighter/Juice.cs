using System;
using UnityEngine;

namespace LagFighter
{
    // Feedback sensorial barato: sonidos sintetizados en runtime (cero assets)
    // y shake de cámara. El hitstop vive en MatchController (pausa cosmética
    // del avance de ticks; no toca la sim).
    public static class SfxLib
    {
        public enum Kind { Hit, Counter, Block, Ko, Fireball, TurnStart }

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

            _clips = new AudioClip[6];
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
