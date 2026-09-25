using System.Collections.Generic;
using Deadhaul.Core;
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
        AudioSource wind, ui, birds, crickets, city, music, rain;
        float oneShotTimer = 8f;

        /// <summary>Wat er om de speler heen is, voor het omgevingsgeluid.</summary>
        public struct AmbienceState { public float Day, Nature, City, Rain, Menu, Radiation; public Vector3 Listener; }
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
            clips["explosie"] = Boom();
            clips["pees"] = String(0.45f, 110f);
            clips["span"] = Creak();
            clips["pijl_inslag"] = Thunk();
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

            foreach (Surface f in System.Enum.GetValues(typeof(Surface))) clips["stap_" + f] = Step(f);
            clips["kraai"] = Crow();
            clips["huil"] = Howl();
            clips["ver_schot"] = DistantShot();
            birds = Loop(Birds(), 0);
            crickets = Loop(Crickets(), 0);
            city = Loop(CityHum(), 0);
            music = Loop(Music(), 0);
            rain = Loop(RainLoop(), 0);
            clips["donder"] = ThunderClip();
        }

        /// <summary>Regen en wind: binnen onder een dak klinkt regen gedempt.</summary>
        public void SetWeather(float rainAmount, float windAmount, bool sheltered, float dt)
        {
            float k = 1 - Mathf.Exp(-dt * 1.5f);
            rain.volume = Mathf.Lerp(rain.volume, rainAmount * (sheltered ? 0.12f : 0.4f), k);
            rain.pitch = sheltered ? 0.7f : 1f;
            if (wind) wind.volume = Mathf.Lerp(wind.volume, Mathf.Lerp(0.05f, 0.32f, windAmount) * (sheltered ? 0.4f : 1f), k);
        }

        public void ThunderAt(Vector3 listener, float distance)
        {
            var dir = Quaternion.Euler(0, (float)rnd.NextDouble() * 360f, 0) * Vector3.forward;
            Play("donder", listener + dir * Mathf.Clamp(distance, 20f, 300f) + Vector3.up * 60f, 1f, 0.8f + (float)rnd.NextDouble() * 0.4f, 1500f);
        }

        AudioSource Loop(AudioClip c, float vol)
        {
            var a = gameObject.AddComponent<AudioSource>();
            a.clip = c; a.loop = true; a.volume = vol; a.spatialBlend = 0; a.Play();
            return a;
        }

        /// <summary>Voetstap op deze ondergrond; stiller bij sluipen, luider bij rennen.</summary>
        public void Footstep(Surface s, Vector3 pos, float loudness, bool player)
        {
            if (!clips.TryGetValue("stap_" + s, out var clip)) return;
            if (player) { ui.pitch = 0.9f + (float)rnd.NextDouble() * 0.2f; ui.PlayOneShot(clip, 0.35f * loudness); return; }
            var src = pool[next]; next = (next + 1) % pool.Count;
            src.transform.position = pos;
            src.maxDistance = 30f;
            src.pitch = 0.85f + (float)rnd.NextDouble() * 0.25f;
            src.volume = 0.6f * loudness;
            src.PlayOneShot(clip);
        }

        /// <summary>Elke frame: omgevingslagen mengen en af en toe een geluid in de verte.</summary>
        public void Ambience(AmbienceState a, float dt)
        {
            float k = 1 - Mathf.Exp(-dt * 0.8f);
            float nature = a.Nature * (1 - a.Radiation);
            birds.volume = Mathf.Lerp(birds.volume, 0.22f * a.Day * nature * (1 - a.Rain) * (1 - a.Menu * 0.5f), k);
            crickets.volume = Mathf.Lerp(crickets.volume, 0.12f * (1 - a.Day) * nature * (1 - a.Rain), k);
            city.volume = Mathf.Lerp(city.volume, 0.2f * a.City, k);
            music.volume = Mathf.Lerp(music.volume, a.Menu > 0.5f ? 0.35f : 0f, 1 - Mathf.Exp(-dt * 0.5f));
            if (a.Menu > 0.5f) return;
            oneShotTimer -= dt;
            if (oneShotTimer > 0) return;
            oneShotTimer = 14f + (float)rnd.NextDouble() * 30f;
            var dir = Quaternion.Euler(0, (float)rnd.NextDouble() * 360f, 0) * Vector3.forward;
            double r = rnd.NextDouble();
            if (a.Day > 0.5f && r < 0.45) Play("kraai", a.Listener + dir * 25f + Vector3.up * 8f, 0.5f, 1f, 200f);
            else if (a.Day < 0.3f && r < 0.4) Play("huil", a.Listener + dir * 90f, 0.7f, 0.9f + (float)rnd.NextDouble() * 0.2f, 600f);
            else if (r < 0.7) Play("ver_schot", a.Listener + dir * 150f, 0.6f, 0.8f + (float)rnd.NextDouble() * 0.4f, 900f);
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

        AudioClip Boom()
        {
            int n = (int)(Rate * 2.2f);
            var d = new float[n];
            float lp = 0, lp2 = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float noise = R();
                lp += (noise - lp) * 0.08f;
                lp2 += (noise - lp2) * 0.01f;
                float env = t < 0.01f ? 1 : Mathf.Exp(-(t - 0.01f) * 3.2f);
                float thump = Mathf.Sin(2 * Mathf.PI * (38f - t * 10f) * t) * Mathf.Exp(-t * 4f);
                d[i] = Mathf.Clamp((noise * Mathf.Exp(-t * 30f) + lp * 2.2f * env + lp2 * 6f * env + thump * 1.2f) * 0.9f, -1, 1);
            }
            return Make("explosie", d);
        }

        /// <summary>Boogpees: een geplukte, snel uitdempende snaar met een zucht lucht.</summary>
        AudioClip String(float length, float freq)
        {
            int n = (int)(Rate * length);
            var d = new float[n];
            float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float f = freq * (1f + 0.6f * Mathf.Exp(-t * 40f));
                float str = (Mathf.Sin(2 * Mathf.PI * f * t) + 0.4f * Mathf.Sin(2 * Mathf.PI * f * 2.02f * t)) * Mathf.Exp(-t * 14f);
                lp += (R() - lp) * 0.15f;
                float whoosh = lp * Mathf.Exp(-t * 9f) * Mathf.Clamp01(t * 60f);
                d[i] = Mathf.Clamp((str * 0.55f + whoosh * 1.2f) * 0.8f, -1, 1);
            }
            return Make("pees", d);
        }

        /// <summary>Kraken van hout en touw bij het spannen.</summary>
        AudioClip Creak()
        {
            int n = (int)(Rate * 0.7f);
            var d = new float[n];
            float ph = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                ph += 2 * Mathf.PI * (180f + t * 120f) / Rate;
                float grain = (Mathf.Sin(ph) > 0.92f ? 1f : 0f) * R();
                d[i] = grain * 0.35f * Mathf.Sin(Mathf.PI * t / 0.7f);
            }
            return Make("span", d);
        }

        /// <summary>Doffe tik van een pijl die in hout, grond of vlees slaat.</summary>
        AudioClip Thunk()
        {
            int n = (int)(Rate * 0.25f);
            var d = new float[n];
            float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                lp += (R() - lp) * 0.2f;
                d[i] = (Mathf.Sin(2 * Mathf.PI * 160f * t) * Mathf.Exp(-t * 35f) + lp * Mathf.Exp(-t * 60f) * 1.5f) * 0.8f;
            }
            return Make("pijl_inslag", d);
        }

        // ------------------------------------------------ voetstappen
        AudioClip Step(Surface f)
        {
            float len = f == Surface.Sneeuw ? 0.22f : f == Surface.Water ? 0.3f : 0.14f;
            int n = (int)(Rate * len);
            var d = new float[n];
            float lp = 0, cut = f switch { Surface.Zacht => 0.05f, Surface.Zand => 0.35f, Surface.Grind => 0.5f, Surface.Hard => 0.3f, Surface.Hout => 0.12f, Surface.Metaal => 0.4f, _ => 0.2f };
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float noise = R();
                lp += (noise - lp) * cut;
                float env = Mathf.Exp(-t * (f == Surface.Hard ? 60f : f == Surface.Metaal ? 25f : 30f)) * Mathf.Clamp01(t * 800f);
                float v = lp * env * 1.6f;
                switch (f)
                {
                    case Surface.Hard: v += Mathf.Sin(2 * Mathf.PI * 1800 * t) * Mathf.Exp(-t * 180f) * 0.25f; break;
                    case Surface.Hout: v += Mathf.Sin(2 * Mathf.PI * 210 * t) * Mathf.Exp(-t * 35f) * 0.5f; break;
                    case Surface.Metaal: v += (Mathf.Sin(2 * Mathf.PI * 930 * t) + Mathf.Sin(2 * Mathf.PI * 1470 * t)) * Mathf.Exp(-t * 20f) * 0.2f; break;
                    case Surface.Sneeuw: v = (rnd.NextDouble() < 0.02 ? R() : 0) * Mathf.Sin(Mathf.PI * t / len) * 0.9f + lp * 0.3f * Mathf.Sin(Mathf.PI * t / len); break;
                    case Surface.Grind: v += (rnd.NextDouble() < 0.01 ? R() * 0.8f : 0) * Mathf.Exp(-t * 20f); break;
                    case Surface.Water: v = lp * Mathf.Sin(Mathf.PI * t / len) * (0.6f + 0.4f * Mathf.Sin(t * 180f)) * 1.4f; break;
                }
                d[i] = Mathf.Clamp(v, -1, 1);
            }
            return Make("stap", d);
        }

        // ------------------------------------------------ omgeving
        AudioClip Birds()
        {
            int n = Rate * 9;
            var d = new float[n];
            for (int c = 0; c < 26; c++)
            {
                float start = (float)rnd.NextDouble() * 8.5f, f0 = 2600 + (float)rnd.NextDouble() * 2800;
                int notes = 2 + rnd.Next(5);
                float slope = ((float)rnd.NextDouble() - 0.5f) * 3000f, gain = 0.05f + (float)rnd.NextDouble() * 0.1f;
                for (int k = 0; k < notes; k++)
                {
                    float ns = start + k * 0.11f, nl = 0.05f + (float)rnd.NextDouble() * 0.06f;
                    float ph = 0;
                    for (int i = (int)(ns * Rate); i < Mathf.Min(n, (int)((ns + nl) * Rate)); i++)
                    {
                        float t = i / (float)Rate - ns;
                        ph += 2 * Mathf.PI * (f0 + slope * t / nl + Mathf.Sin(t * 260f) * 150f) / Rate;
                        d[i] += Mathf.Sin(ph) * Mathf.Sin(Mathf.PI * t / nl) * gain;
                    }
                }
            }
            return Make("vogels", Seam(d));
        }

        AudioClip Crickets()
        {
            int n = Rate * 6;
            var d = new float[n];
            for (int c = 0; c < 5; c++)
            {
                float f = 4200 + c * 230, period = 0.7f + c * 0.13f, off = (float)rnd.NextDouble(), gain = 0.05f + c * 0.012f;
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)Rate + off;
                    float cyc = t % period;
                    float pulse = cyc < 0.18f ? Mathf.Max(0, Mathf.Sin(2 * Mathf.PI * 33f * cyc)) : 0;
                    d[i] += Mathf.Sin(2 * Mathf.PI * f * t) * pulse * gain;
                }
            }
            return Make("krekels", Seam(d));
        }

        AudioClip CityHum()
        {
            int n = Rate * 10;
            var d = new float[n];
            float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                lp += (R() - lp) * 0.003f;
                d[i] = lp * 5f + Mathf.Sin(2 * Mathf.PI * 50f * t) * 0.015f;
            }
            // krakend metaal en klapperende plaat
            for (int c = 0; c < 4; c++)
            {
                float start = 0.5f + c * 2.3f + (float)rnd.NextDouble(), f = 300 + (float)rnd.NextDouble() * 500;
                for (int i = (int)(start * Rate); i < Mathf.Min(n, (int)((start + 1.2f) * Rate)); i++)
                {
                    float t = i / (float)Rate - start;
                    d[i] += Mathf.Sin(2 * Mathf.PI * (f + Mathf.Sin(t * 7f) * 40f) * t) * Mathf.Sin(Mathf.PI * t / 1.2f) * 0.04f * (Mathf.Sin(t * 90f) > 0.3f ? 1 : 0.3f);
                }
            }
            return Make("stad", Seam(d));
        }

        AudioClip Crow()
        {
            int n = (int)(Rate * 1.4f);
            var d = new float[n];
            for (int c = 0; c < 3; c++)
            {
                float start = c * 0.42f, ph = 0;
                for (int i = (int)(start * Rate); i < Mathf.Min(n, (int)((start + 0.3f) * Rate)); i++)
                {
                    float t = i / (float)Rate - start;
                    ph += 2 * Mathf.PI * (620f - t * 300f) / Rate;
                    float saw = (ph / Mathf.PI % 2f) - 1f;
                    d[i] = (saw * 0.5f + R() * 0.25f) * Mathf.Sin(Mathf.PI * t / 0.3f) * 0.6f;
                }
            }
            return Make("kraai", d);
        }

        AudioClip Howl()
        {
            int n = (int)(Rate * 3.2f);
            var d = new float[n];
            float ph = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float f = t < 0.8f ? 380 + t * 400 : 700 - (t - 0.8f) * 110;
                ph += 2 * Mathf.PI * (f + Mathf.Sin(t * 30f) * 8f) / Rate;
                d[i] = (Mathf.Sin(ph) + 0.25f * Mathf.Sin(2 * ph)) * Mathf.Sin(Mathf.PI * t / 3.2f) * 0.45f;
            }
            return Make("huil", d);
        }

        AudioClip DistantShot()
        {
            int n = (int)(Rate * 2.5f);
            var d = new float[n];
            float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                lp += (R() - lp) * 0.02f;
                float echo = Mathf.Exp(-t * 2.2f) * (1 + 0.5f * Mathf.Sin(t * 13f));
                d[i] = Mathf.Clamp(lp * 5f * echo + Mathf.Sin(2 * Mathf.PI * 45f * t) * Mathf.Exp(-t * 10f) * 0.5f, -1, 1);
            }
            return Make("ver_schot", d);
        }

        /// <summary>Somber ambient-thema voor het hoofdmenu: Am – F – Dm – E, zachte zaagtanden door een laagdoorlaat.</summary>
        AudioClip Music()
        {
            float[][] chords = { new[] { 110f, 164.8f, 220f, 261.6f }, new[] { 87.3f, 174.6f, 220f, 261.6f }, new[] { 73.4f, 146.8f, 174.6f, 220f }, new[] { 82.4f, 164.8f, 207.7f, 246.9f } };
            const float bar = 7f;
            int n = (int)(Rate * bar * chords.Length);
            var d = new float[n];
            var phase = new float[8];
            float lp = 0, lp2 = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                int ci = (int)(t / bar);
                float local = t - ci * bar;
                float env = Mathf.Clamp01(local / 2.2f) * Mathf.Clamp01((bar - local) / 1.8f);
                float v = 0;
                for (int k = 0; k < 4; k++)
                    for (int det = 0; det < 2; det++)
                    {
                        int pi = k * 2 + det;
                        phase[pi] += chords[ci][k] * (det == 0 ? 0.998f : 1.003f) / Rate;
                        phase[pi] -= Mathf.Floor(phase[pi]);
                        v += (phase[pi] * 2 - 1) * (k == 0 ? 0.35f : 0.18f);
                    }
                float cut = 0.03f + 0.02f * Mathf.Sin(t * 0.4f);
                lp += (v - lp) * cut; lp2 += (lp - lp2) * cut;
                // hoge klok als melodie
                float bell = 0;
                float bt = local - 3.5f;
                if (bt > 0) bell = Mathf.Sin(2 * Mathf.PI * chords[ci][3] * 2 * bt) * Mathf.Exp(-bt * 1.6f) * 0.12f;
                d[i] = (lp2 * env * 0.5f + bell) * 0.8f;
            }
            return Make("muziek", Seam(d));
        }

        AudioClip RainLoop()
        {
            int n = Rate * 6;
            var d = new float[n];
            float lp = 0, hp = 0;
            for (int i = 0; i < n; i++)
            {
                float noise = R();
                lp += (noise - lp) * 0.25f;
                hp = noise - lp;
                float drops = rnd.NextDouble() < 0.004 ? R() * 1.5f : 0;
                d[i] = (lp * 0.9f + hp * 0.25f) * 0.55f + drops * 0.3f;
            }
            return Make("regen", Seam(d));
        }

        AudioClip ThunderClip()
        {
            int n = (int)(Rate * 5f);
            var d = new float[n];
            float lp = 0, lp2 = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                lp += (R() - lp) * 0.02f; lp2 += (lp - lp2) * 0.05f;
                float crack = t < 0.15f ? R() * Mathf.Exp(-t * 25f) * 0.5f : 0;
                float roll = (1 + 0.6f * Mathf.Sin(t * 5.3f) * Mathf.Sin(t * 2.1f)) * Mathf.Exp(-t * 0.8f) * Mathf.Clamp01(t * 8f);
                d[i] = Mathf.Clamp(crack + lp2 * 14f * roll, -1, 1);
            }
            return Make("donder", d);
        }

        /// <summary>Laat het einde in het begin overvloeien zodat een lus niet klikt.</summary>
        static float[] Seam(float[] d)
        {
            int f = Rate / 2, n = d.Length;
            for (int i = 0; i < f; i++) { float k = i / (float)f; d[i] = d[i] * k + d[n - f + i] * (1 - k); }
            return d;
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
