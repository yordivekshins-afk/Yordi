using System.Collections.Generic;
using UnityEngine;

namespace Deadhaul
{
    /// <summary>
    /// Procedureel gesynthetiseerde geluiden (geen audiobestanden nodig): schoten per kaliber,
    /// gedempte schoten, herladen, inslagen, slagen, mutanten, geigerteller en wind.
    /// Wordt in een latere mijlpaal aangevuld met opgenomen geluiden.
    /// </summary>
    public sealed class Sfx : MonoBehaviour
    {
        public static Sfx Instance { get; private set; }
        const int Rate = 44100;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly List<AudioSource> pool = new List<AudioSource>();
        AudioSource wind, ui;
        int next;
        System.Random rnd = new System.Random(5);

        void Awake()
        {
            Instance = this;
            clips["schot_licht"] = Shot(0.35f, 1400f, 0.9f, 0.018f);
            clips["schot_zwaar"] = Shot(0.6f, 900f, 1f, 0.028f);
            clips["schot_hagel"] = Shot(0.55f, 700f, 1f, 0.035f);
            clips["schot_gedempt"] = Shot(0.18f, 2600f, 0.35f, 0.006f);
            clips["droog"] = Click(0.02f, 3200f, 0.5f);
            clips["herladen"] = Reload();
            clips["inslag"] = Noise(0.08f, 0.5f, 0.35f, 900f);
            clips["glas"] = Glass();
            clips["slag"] = Noise(0.12f, 0.6f, 0.25f, 400f);
            clips["grom"] = Growl();
            clips["tik"] = Click(0.004f, 5000f, 0.6f);
            clips["treffer"] = Click(0.03f, 1800f, 0.4f);
            for (int i = 0; i < 24; i++)
            {
                var go = new GameObject("Geluid");
                go.transform.SetParent(transform, false);
                var s = go.AddComponent<AudioSource>();
                s.spatialBlend = 1f; s.rolloffMode = AudioRolloffMode.Logarithmic; s.minDistance = 2f; s.maxDistance = 400f; s.dopplerLevel = 0;
                pool.Add(s);
            }
            wind = gameObject.AddComponent<AudioSource>();
            wind.clip = Wind(); wind.loop = true; wind.volume = 0.12f; wind.spatialBlend = 0; wind.Play();
            ui = gameObject.AddComponent<AudioSource>();
            ui.spatialBlend = 0;
        }

        public void Play(string name, Vector3 pos, float volume = 1f, float pitch = 1f, float maxDistance = 400f)
        {
            if (!clips.TryGetValue(name, out var clip)) return;
            var s = pool[next]; next = (next + 1) % pool.Count;
            s.transform.position = pos;
            s.maxDistance = maxDistance;
            s.pitch = pitch * (0.94f + (float)rnd.NextDouble() * 0.12f);
            s.volume = volume;
            s.PlayOneShot(clip);
        }

        public void Play2D(string name, float volume = 1f, float pitch = 1f)
        {
            if (!clips.TryGetValue(name, out var clip)) return;
            ui.pitch = pitch;
            ui.PlayOneShot(clip, volume);
        }

        public void SetWind(float strength) { if (wind) wind.volume = Mathf.Lerp(0.05f, 0.25f, strength); }

        // ------------------------------------------------ synthese
        AudioClip Make(string name, float[] data)
        {
            var c = AudioClip.Create(name, data.Length, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }

        float R() => (float)rnd.NextDouble() * 2f - 1f;

        /// <summary>Knal: ruisexplosie met lage dreun en een staartje galm.</summary>
        AudioClip Shot(float length, float lowpass, float gain, float crack)
        {
            int n = (int)(Rate * length);
            var d = new float[n];
            float lp = 0, lp2 = 0, a = Mathf.Exp(-2f * Mathf.PI * lowpass / Rate);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float env = t < crack ? 1f : Mathf.Exp(-(t - crack) * 9f);
                float noise = R();
                lp = a * lp + (1 - a) * noise;
                lp2 = 0.995f * lp2 + 0.005f * noise;
                float thump = Mathf.Sin(2 * Mathf.PI * 55f * t) * Mathf.Exp(-t * 16f);
                float sample = (t < crack ? noise : lp * 1.6f) * env + thump * 0.8f + lp2 * 3f * Mathf.Exp(-t * 3f);
                d[i] = Mathf.Clamp(sample * gain, -1f, 1f);
            }
            return Make("schot", d);
        }

        AudioClip Click(float length, float freq, float gain)
        {
            int n = (int)(Rate * Mathf.Max(0.01f, length * 4));
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                d[i] = (Mathf.Sin(2 * Mathf.PI * freq * t) * 0.5f + R() * 0.5f) * Mathf.Exp(-t / length) * gain;
            }
            return Make("klik", d);
        }

        AudioClip Noise(float length, float gain, float decay, float lowpass)
        {
            int n = (int)(Rate * length * 3);
            var d = new float[n];
            float lp = 0, a = Mathf.Exp(-2f * Mathf.PI * lowpass / Rate);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                lp = a * lp + (1 - a) * R();
                d[i] = lp * 3f * Mathf.Exp(-t / (length * decay * 2f)) * gain;
            }
            return Make("ruis", d);
        }

        AudioClip Reload()
        {
            int n = Rate;
            var d = new float[n];
            float[] at = { 0.05f, 0.42f, 0.5f, 0.85f };
            foreach (var s in at)
                for (int i = 0; i < Rate * 0.05f; i++)
                {
                    int k = (int)(s * Rate) + i;
                    if (k >= n) break;
                    float t = i / (float)Rate;
                    d[k] += (R() * 0.6f + Mathf.Sin(2 * Mathf.PI * 2400 * t) * 0.4f) * Mathf.Exp(-t * 90f) * 0.6f;
                }
            return Make("herladen", d);
        }

        AudioClip Glass()
        {
            int n = (int)(Rate * 0.6f);
            var d = new float[n];
            for (int k = 0; k < 14; k++)
            {
                float f = 2500 + (float)rnd.NextDouble() * 5000, start = (float)rnd.NextDouble() * 0.25f;
                for (int i = (int)(start * Rate); i < n; i++)
                {
                    float t = i / (float)Rate - start;
                    d[i] += Mathf.Sin(2 * Mathf.PI * f * t) * Mathf.Exp(-t * 18f) * 0.12f;
                }
            }
            for (int i = 0; i < 2000; i++) d[i] += R() * Mathf.Exp(-i / 400f) * 0.5f;
            return Make("glas", d);
        }

        AudioClip Growl()
        {
            int n = (int)(Rate * 0.9f);
            var d = new float[n];
            float ph = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float f = 70 + Mathf.Sin(t * 9f) * 20 + R() * 15;
                ph += 2 * Mathf.PI * f / Rate;
                float env = Mathf.Sin(Mathf.PI * t / 0.9f);
                d[i] = (Mathf.Sign(Mathf.Sin(ph)) * 0.35f + R() * 0.25f) * env * 0.5f;
            }
            return Make("grom", d);
        }

        AudioClip Wind()
        {
            int n = Rate * 6;
            var d = new float[n];
            float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float cut = 0.004f + 0.003f * Mathf.Sin(t * 0.9f) + 0.002f * Mathf.Sin(t * 2.3f);
                lp += (R() - lp) * cut;
                d[i] = lp * 4f;
            }
            // naadloos laten lopen
            for (int i = 0; i < Rate / 2; i++) { float k = i / (Rate / 2f); d[i] = d[i] * k + d[n - Rate / 2 + i] * (1 - k); }
            return Make("wind", d);
        }
    }
}
