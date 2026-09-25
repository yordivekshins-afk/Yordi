using System;

namespace Deadhaul.Core
{
    public enum Season : byte { Lente, Zomer, Herfst, Winter }

    public enum WeatherKind : byte { Helder, Bewolkt, Regen, Onweer, Sneeuw, Stralingsstorm }

    /// <summary>Het weer op één moment, met vloeiende overgangen.</summary>
    public struct WeatherState
    {
        public WeatherKind Kind;
        public Season Season;
        public float Cloud, Rain, Snow, Thunder, RadStorm, Wind, Fog, Wetness;
    }

    /// <summary>
    /// Seizoenen en weer. Volledig bepaald door seed en speltijd, dus niets om op te slaan:
    /// elk seizoen duurt een paar speldagen, het weer wisselt per blok van drie uur met
    /// zachte overgangen. Na regen blijft de wereld nog een tijd nat.
    /// </summary>
    public sealed class Weather
    {
        public const int DaysPerSeason = 5;
        public const float SlotHours = 3f;
        readonly int seed;

        public Weather(int seed) { this.seed = seed; }

        public static Season SeasonOf(double time) => (Season)((int)(Math.Floor(Math.Max(0, time)) / DaysPerSeason) % 4);

        public static string SeasonName(Season s) => s switch { Season.Lente => "lente", Season.Zomer => "zomer", Season.Herfst => "herfst", _ => "winter" };

        public static string KindName(WeatherKind k) => k switch
        {
            WeatherKind.Helder => "helder", WeatherKind.Bewolkt => "bewolkt", WeatherKind.Regen => "regen", WeatherKind.Onweer => "onweer",
            WeatherKind.Sneeuw => "sneeuw", _ => "stralingsstorm",
        };

        /// <summary>Verschuiving op de omgevingswarmte (0..1-schaal van GameClock.Ambient).</summary>
        public static float SeasonWarmth(Season s) => s switch { Season.Zomer => 0.08f, Season.Herfst => -0.07f, Season.Winter => -0.22f, _ => 0f };

        /// <summary>Hoe snel gewassen groeien in dit seizoen.</summary>
        public static float SeasonGrowth(Season s) => s switch { Season.Lente => 1.1f, Season.Zomer => 1.3f, Season.Herfst => 0.6f, _ => 0.1f };

        static readonly float[][] Weights =
        {
            //            helder bewolkt regen onweer sneeuw straling
            new[] { 0.35f, 0.30f, 0.25f, 0.06f, 0.00f, 0.04f },   // lente
            new[] { 0.55f, 0.18f, 0.10f, 0.13f, 0.00f, 0.04f },   // zomer
            new[] { 0.20f, 0.35f, 0.32f, 0.07f, 0.00f, 0.06f },   // herfst
            new[] { 0.28f, 0.32f, 0.02f, 0.00f, 0.32f, 0.06f },   // winter
        };

        public WeatherKind KindAt(long slot)
        {
            if (slot <= 3) return WeatherKind.Helder;             // een nieuw spel begint met mooi weer
            double time = slot * SlotHours / 24.0;
            var season = SeasonOf(time);
            // weer blijft vaak even hangen: per twee blokken een nieuwe trekking, met kans op verandering tussendoor
            float r = Hash.H2((int)(slot / 2), 11, seed * 31 + 77);
            if (Hash.H2((int)slot, 23, seed + 5) < 0.3f) r = Hash.H2((int)slot, 37, seed * 17 + 3);
            var w = Weights[(int)season];
            float acc = 0;
            for (int i = 0; i < w.Length; i++) { acc += w[i]; if (r < acc) return (WeatherKind)i; }
            return WeatherKind.Helder;
        }

        struct Target { public float Cloud, Rain, Snow, Thunder, Rad, Wind, Fog; }

        static Target TargetOf(WeatherKind k) => k switch
        {
            WeatherKind.Helder => new Target { Cloud = 0.15f, Wind = 0.2f },
            WeatherKind.Bewolkt => new Target { Cloud = 0.6f, Wind = 0.35f, Fog = 0.1f },
            WeatherKind.Regen => new Target { Cloud = 0.85f, Rain = 0.75f, Wind = 0.5f, Fog = 0.35f },
            WeatherKind.Onweer => new Target { Cloud = 1f, Rain = 1f, Thunder = 1f, Wind = 0.85f, Fog = 0.45f },
            WeatherKind.Sneeuw => new Target { Cloud = 0.8f, Snow = 0.8f, Wind = 0.4f, Fog = 0.5f },
            _ => new Target { Cloud = 0.7f, Rad = 1f, Wind = 0.9f, Fog = 0.6f },
        };

        public WeatherState Sample(double time)
        {
            double hours = time * 24.0;
            long slot = (long)Math.Floor(hours / SlotHours);
            float frac = (float)(hours / SlotHours - slot);
            var a = KindAt(slot); var b = KindAt(slot + 1);
            // overgang in de tweede helft van het blok (anderhalf speeluur)
            float t = frac < 0.5f ? 0 : (frac - 0.5f) / 0.5f;
            t = t * t * (3 - 2 * t);
            var ta = TargetOf(a); var tb = TargetOf(b);
            var s = new WeatherState
            {
                Kind = t < 0.5f ? a : b,
                Season = SeasonOf(time),
                Cloud = Lerp(ta.Cloud, tb.Cloud, t), Rain = Lerp(ta.Rain, tb.Rain, t), Snow = Lerp(ta.Snow, tb.Snow, t),
                Thunder = Lerp(ta.Thunder, tb.Thunder, t), RadStorm = Lerp(ta.Rad, tb.Rad, t), Wind = Lerp(ta.Wind, tb.Wind, t), Fog = Lerp(ta.Fog, tb.Fog, t),
            };
            // nat: na een bui droogt alles langzaam op
            float wet = s.Rain;
            for (int k = 1; k <= 8; k++)
            {
                double past = time - k * 0.5 / 24.0;
                long ps = (long)Math.Floor(past * 24.0 / SlotHours);
                float r = TargetOf(KindAt(ps)).Rain;
                wet = Math.Max(wet, r * MathF.Exp(-k * 0.3f));
            }
            s.Wetness = Math.Min(1f, wet);
            return s;
        }

        static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
