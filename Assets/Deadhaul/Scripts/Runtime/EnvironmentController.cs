using Deadhaul.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Deadhaul
{
    public enum RayTracingQuality { Uit, Prestatie, Kwaliteit }

    /// <summary>
    /// Zon, maan, fysieke lucht met sterren, volumetrische wolken en mist, oceaan,
    /// post-processing en de raytracing-effecten. Alles wordt in code opgebouwd,
    /// zodat de scène zelf leeg kan blijven.
    /// </summary>
    public sealed class EnvironmentController : MonoBehaviour
    {
        public GameClock Clock;
        public Light Sun, Moon;
        HDAdditionalLightData sunHd, moonHd;
        Volume volume;
        VolumeProfile profile;
        ScreenSpaceReflection ssr;
        GlobalIllumination ssgi;
        ScreenSpaceAmbientOcclusion ssao;
        Fog fog;
        VolumetricClouds clouds;
        PhysicallyBasedSky sky;
        Exposure exposure;
        ColorAdjustments grade;

        public RayTracingQuality RayTracing { get; private set; }
        public bool RayTracingSupported => SystemInfo.supportsRayTracing;

        public void Init(GameClock clock)
        {
            Clock = clock;
            Sun = MakeDirectional("Zon", new Color(1f, 0.96f, 0.9f), true, out sunHd);
            sunHd.angularDiameter = 0.53f;
            Moon = MakeDirectional("Maan", new Color(0.62f, 0.72f, 1f), false, out moonHd);
            moonHd.angularDiameter = 1.6f;
            BuildVolume();
            BuildOcean();
            SetRayTracing(RayTracingSupported ? RayTracingQuality.Prestatie : RayTracingQuality.Uit);
        }

        Light MakeDirectional(string name, Color color, bool shadows, out HDAdditionalLightData hd)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            hd = go.AddHDLight(LightType.Directional);
            var light = go.GetComponent<Light>();
            light.color = color;
            light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            hd.interactsWithSky = true;
            if (shadows) hd.SetShadowResolution(4096);
            return light;
        }

        void BuildVolume()
        {
            var go = new GameObject("Omgeving (volume)");
            go.transform.SetParent(transform, false);
            volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Deadhaul runtime-profiel";
            volume.sharedProfile = profile;

            var env = profile.Add<VisualEnvironment>();
            env.skyType.Override((int)SkyType.PhysicallyBased);
            env.skyAmbientMode.Override(SkyAmbientMode.Dynamic);

            sky = profile.Add<PhysicallyBasedSky>();
            sky.spaceEmissionTexture.Override(StarField.Create(512, 1337));
            sky.spaceEmissionMultiplier.Override(0.6f);
            sky.groundTint.Override(new Color(0.14f, 0.12f, 0.1f));

            clouds = profile.Add<VolumetricClouds>();
            clouds.enable.Override(true);
            clouds.cloudPreset = VolumetricClouds.CloudPresets.Cloudy;

            fog = profile.Add<Fog>();
            fog.enabled.Override(true);
            fog.meanFreePath.Override(320f);
            fog.baseHeight.Override(World.SeaLevelMeters);
            fog.maximumHeight.Override(70f);
            fog.enableVolumetricFog.Override(true);
            fog.albedo.Override(new Color(0.86f, 0.84f, 0.8f));
            fog.anisotropy.Override(0.65f);
            fog.depthExtent.Override(90f);

            var shadows = profile.Add<HDShadowSettings>();
            shadows.maxShadowDistance.Override(180f);
            shadows.cascadeShadowSplitCount.Override(4);

            var contact = profile.Add<ContactShadows>();
            contact.enable.Override(true);
            contact.length.Override(0.25f);

            exposure = profile.Add<Exposure>();
            exposure.mode.Override(ExposureMode.AutomaticHistogram);
            exposure.limitMin.Override(-1.5f);
            exposure.limitMax.Override(15f);
            exposure.compensation.Override(0.3f);

            var tonemap = profile.Add<Tonemapping>();
            tonemap.mode.Override(TonemappingMode.ACES);

            var bloom = profile.Add<Bloom>();
            bloom.intensity.Override(0.18f);
            bloom.scatter.Override(0.72f);

            grade = profile.Add<ColorAdjustments>();
            grade.saturation.Override(-14f);
            grade.contrast.Override(8f);
            grade.colorFilter.Override(new Color(1f, 0.97f, 0.92f));

            var vignette = profile.Add<Vignette>();
            vignette.intensity.Override(0.28f);
            vignette.smoothness.Override(0.45f);

            var grain = profile.Add<FilmGrain>();
            grain.type.Override(FilmGrainLookup.Medium3);
            grain.intensity.Override(0.18f);

            ssr = profile.Add<ScreenSpaceReflection>();
            ssr.enabled.Override(true);
            ssgi = profile.Add<GlobalIllumination>();
            ssgi.enable.Override(true);
            ssao = profile.Add<ScreenSpaceAmbientOcclusion>();
            ssao.intensity.Override(1.2f);
            ssao.radius.Override(1.5f);
            profile.Add<RayTracingSettings>();
        }

        void BuildOcean()
        {
            var go = new GameObject("Oceaan");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(0, World.SeaLevelMeters, 0);
            var water = go.AddComponent<WaterSurface>();
            water.surfaceType = WaterSurfaceType.OceanSeaLake;
            water.geometryType = WaterGeometryType.Infinite;
            water.largeWindSpeed = 12f;
            water.largeChaos = 0.85f;
            water.largeBand0Multiplier = 0.25f;     // kalm binnenwater, geen oceaandeining
            water.ripples = true;
            water.refractionColor = new Color(0.07f, 0.24f, 0.22f);
            water.scatteringColor = new Color(0.02f, 0.16f, 0.14f);
            water.absorptionDistance = 3.5f;
            water.maxRefractionDistance = 1.2f;
            water.caustics = true;
        }

        /// <summary>Schakelt tussen raytracing (RTX/RX 6000+) en de schermruimte-varianten.</summary>
        public void SetRayTracing(RayTracingQuality q)
        {
            if (!RayTracingSupported) q = RayTracingQuality.Uit;
            RayTracing = q;
            bool rt = q != RayTracingQuality.Uit;
            var mode = q == RayTracingQuality.Kwaliteit ? RayTracingMode.Quality : RayTracingMode.Performance;
            ssr.tracing.Override(rt ? RayCastingMode.RayTracing : RayCastingMode.RayMarching);
            ssr.mode.Override(mode);
            ssgi.tracing.Override(rt ? (q == RayTracingQuality.Kwaliteit ? RayCastingMode.RayTracing : RayCastingMode.Mixed) : RayCastingMode.RayMarching);
            ssgi.mode.Override(mode);
            ssao.rayTracing.Override(rt);
            sunHd.useRayTracedShadows = rt;
            sunHd.filterTracedShadow = true;
        }

        public void CycleRayTracing()
        {
            SetRayTracing((RayTracingQuality)(((int)RayTracing + 1) % 3));
        }

        void Update()
        {
            if (Clock == null) return;
            float h = Clock.HourOfDay;
            // zon: op om 6:00, hoogste punt om 13:00, onder om 20:00
            float sunAngle = (h - 6f) / 14f * 180f;
            float sunElev = Mathf.Sin(sunAngle * Mathf.Deg2Rad) * 58f;
            if (h < 6f || h > 20f) sunElev = -10f - Mathf.Sin(((h < 6f ? h + 24f : h) - 20f) / 10f * Mathf.PI) * 30f;
            float azimuth = -70f + (h - 6f) / 14f * 140f;
            Sun.transform.rotation = Quaternion.Euler(sunElev, azimuth, 0f);
            float day = Mathf.Clamp01((sunElev + 4f) / 14f);
            Sun.intensity = Mathf.Lerp(0f, 100000f, day * day);
            Sun.enabled = sunElev > -6f;
            // warme kleur laag bij de horizon
            float low = 1f - Mathf.Clamp01(sunElev / 25f);
            Sun.color = Color.Lerp(new Color(1f, 0.97f, 0.92f), new Color(1f, 0.62f, 0.38f), low * low);

            // maan staat tegenover de zon
            Moon.transform.rotation = Quaternion.Euler(Mathf.Max(8f, -sunElev * 0.9f), azimuth + 180f, 0f);
            float night = 1f - Mathf.Clamp01((sunElev + 8f) / 10f);
            Moon.intensity = 0.6f * night;
            Moon.enabled = night > 0.01f;
            moonHd.useRayTracedShadows = false;
            Moon.shadows = night > 0.5f ? LightShadows.Soft : LightShadows.None;

            // 's nachts dikkere, koelere mist
            fog.meanFreePath.Override(Mathf.Lerp(320f, 140f, night));
            fog.albedo.Override(Color.Lerp(new Color(0.86f, 0.84f, 0.8f), new Color(0.55f, 0.62f, 0.75f), night));
        }
    }

    /// <summary>Procedurele sterrenhemel als cubemap voor de ruimte-emissie van de fysieke lucht.</summary>
    public static class StarField
    {
        public static Cubemap Create(int size, int seed)
        {
            var cube = new Cubemap(size, TextureFormat.RGBAHalf, false) { name = "Sterren" };
            var rnd = new System.Random(seed);
            var px = new Color[size * size];
            for (int f = 0; f < 6; f++)
            {
                for (int i = 0; i < px.Length; i++) px[i] = Color.black;
                int stars = size * size / 180;
                for (int s = 0; s < stars; s++)
                {
                    int x = rnd.Next(size), y = rnd.Next(size);
                    float b = (float)System.Math.Pow(rnd.NextDouble(), 7) * 2.5f + 0.05f;
                    float t = (float)rnd.NextDouble();
                    var c = Color.Lerp(new Color(0.75f, 0.82f, 1f), new Color(1f, 0.86f, 0.7f), t) * b;
                    px[x + y * size] = c;
                }
                cube.SetPixels(px, (CubemapFace)f);
            }
            cube.Apply(false, true);
            return cube;
        }
    }
}
