using Deadhaul.Core;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Deadhaul
{
    /// <summary>
    /// Materialen en palet-texturen. Het menu "Deadhaul → Project instellen" slaat ze op als assets
    /// in Resources; bestaan ze niet, dan worden ze hier in code gemaakt (werkt in de editor).
    /// </summary>
    public static class VoxelAssets
    {
        public const string ResourceFolder = "Deadhaul";
        static Material voxel, glass, highlight;

        public static Material VoxelMaterial => voxel ? voxel : voxel = Fresh(Load("VoxelLit")) ?? CreateVoxelMaterial(BaseColorTexture(), MaskTexture(), EmissionTexture());
        public static Material GlassMaterial => glass ? glass : glass = Load("VoxelGlass") ?? CreateGlassMaterial(BaseColorTexture());

        /// <summary>
        /// Het opgeslagen materiaal met een vers palet: nieuwe bloksoorten krijgen zo altijd hun kleur,
        /// ook als de palet-PNG's in Resources van een oudere versie zijn.
        /// </summary>
        static Material Fresh(Material saved)
        {
            if (saved == null) return null;
            var m = new Material(saved) { name = saved.name };
            m.SetTexture("_BaseColorMap", BaseColorTexture());
            m.SetTexture("_MaskMap", MaskTexture());
            m.SetTexture("_EmissiveColorMap", EmissionTexture());
            return m;
        }
        public static Material HighlightMaterial => highlight ? highlight : highlight = Load("Highlight") ?? CreateHighlightMaterial();

        static Material Load(string name) => Resources.Load<Material>($"{ResourceFolder}/{name}");

        public static Texture2D BaseColorTexture() => MakeTexture("Palet basiskleur", Palette.BaseColorPixels(), false);
        public static Texture2D MaskTexture() => MakeTexture("Palet mask", Palette.MaskPixels(), true);
        public static Texture2D EmissionTexture() => MakeTexture("Palet emissie", Palette.EmissionPixels(), true);

        public static Texture2D MakeTexture(string name, byte[] rgba, bool linear)
        {
            var t = new Texture2D(Palette.Width, Palette.Rows, TextureFormat.RGBA32, false, linear)
            {
                name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, anisoLevel = 0
            };
            t.SetPixelData(rgba, 0);
            t.Apply(false, false);
            return t;
        }

        public static Material CreateVoxelMaterial(Texture2D baseColor, Texture2D mask, Texture2D emission)
        {
            var m = new Material(Shader.Find("HDRP/Lit")) { name = "VoxelLit" };
            m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_BaseColorMap", baseColor);
            m.SetTexture("_MaskMap", mask);
            m.SetFloat("_Metallic", 1f);
            m.SetFloat("_Smoothness", 1f);
            m.SetTexture("_EmissiveColorMap", emission);
            HDMaterial.SetUseEmissiveIntensity(m, true);
            HDMaterial.SetEmissiveColor(m, Color.white);
            HDMaterial.SetEmissiveIntensity(m, 2500f, EmissiveIntensityUnit.Nits);
            HDMaterial.ValidateMaterial(m);
            return m;
        }

        public static Material CreateGlassMaterial(Texture2D baseColor)
        {
            var m = new Material(Shader.Find("HDRP/Lit")) { name = "VoxelGlass" };
            HDMaterial.SetSurfaceType(m, true);
            m.SetColor("_BaseColor", new Color(0.6f, 0.72f, 0.76f, 0.28f));
            m.SetTexture("_BaseColorMap", baseColor);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", 0.96f);
            HDMaterial.ValidateMaterial(m);
            return m;
        }

        public static Material CreateHighlightMaterial()
        {
            var m = new Material(Shader.Find("HDRP/Unlit")) { name = "Highlight" };
            HDMaterial.SetSurfaceType(m, true);
            m.SetColor("_UnlitColor", new Color(1f, 0.92f, 0.7f, 0.22f));
            HDMaterial.ValidateMaterial(m);
            return m;
        }
    }
}
