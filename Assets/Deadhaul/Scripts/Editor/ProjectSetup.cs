using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

namespace Deadhaul.EditorTools
{
    /// <summary>
    /// Eén klik om het project klaar te zetten: HDRP met raytracing, DirectX 12, lineaire kleur,
    /// palet-texturen, materialen en de speelscène. Menu: Deadhaul → Project instellen.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        const string Root = "Assets/Deadhaul";
        const string SettingsDir = Root + "/Settings";
        const string ResDir = Root + "/Resources/" + VoxelAssets.ResourceFolder;
        const string SceneDir = Root + "/Scenes";
        const string PipelinePath = SettingsDir + "/DeadhaulHDRP.asset";
        const string ScenePath = SceneDir + "/Deadhaul.unity";

        static ProjectSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (SessionState.GetBool("DeadhaulSetupAsked", false)) return;
                SessionState.SetBool("DeadhaulSetupAsked", true);
                if (AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(PipelinePath) != null && File.Exists(ScenePath)) return;
                if (EditorUtility.DisplayDialog("Deadhaul",
                        "Het project is nog niet ingesteld.\n\nNu HDRP met raytracing, DirectX 12, de materialen en de speelscène aanmaken?",
                        "Ja, instellen", "Later"))
                    Run();
            };
        }

        [MenuItem("Deadhaul/Project instellen (HDRP + raytracing)", priority = 0)]
        public static void Run()
        {
            foreach (var d in new[] { SettingsDir, ResDir, SceneDir }) Directory.CreateDirectory(d);
            AssetDatabase.Refresh();

            var pipeline = SetupPipeline();
            SetupPlayer();
            SetupMaterials();
            SetupScene();

            AssetDatabase.SaveAssets();
            Debug.Log($"Deadhaul: project ingesteld met {pipeline.name}.");
            EditorUtility.DisplayDialog("Deadhaul is ingesteld",
                "Klaar. Nog twee dingen:\n\n" +
                "1. Het HDRP Wizard-venster gaat nu open. Klik op beide tabbladen (HDRP en HDRP + DXR) op 'Fix All'.\n" +
                "2. Start Unity opnieuw als erom gevraagd wordt (voor DirectX 12).\n\n" +
                "Daarna: open Assets/Deadhaul/Scenes/Deadhaul en druk op Play.", "OK");
            EditorApplication.ExecuteMenuItem("Window/Rendering/HDRP Wizard");
        }

        static HDRenderPipelineAsset SetupPipeline()
        {
            var asset = AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(PipelinePath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<HDRenderPipelineAsset>();
                AssetDatabase.CreateAsset(asset, PipelinePath);
            }
            var s = asset.currentPlatformRenderPipelineSettings;
            s.supportRayTracing = true;
            s.supportedRayTracingMode = RenderPipelineSettings.SupportedRayTracingMode.Both;
            s.supportWater = true;
            s.supportVolumetricClouds = true;
            s.supportVolumetrics = true;
            s.supportSSGI = true;
            s.supportSSR = true;
            s.supportSSRTransparent = true;
            s.supportSSAO = true;
            s.supportMotionVectors = true;
            s.supportTransparentBackface = true;
            asset.currentPlatformRenderPipelineSettings = s;
            EditorUtility.SetDirty(asset);

            GraphicsSettings.defaultRenderPipeline = asset;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;
            }
            QualitySettings.SetQualityLevel(current, false);
            return asset;
        }

        static void SetupPlayer()
        {
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D12 });
            PlayerSettings.productName = "Deadhaul";
            PlayerSettings.companyName = "Yordi";

            // Input System aanzetten (0 = oud, 1 = nieuw, 2 = beide). Vraagt om een herstart.
            var ps = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (ps != null && ps.Length > 0)
            {
                var so = new SerializedObject(ps[0]);
                var prop = so.FindProperty("activeInputHandler");
                if (prop != null && prop.intValue == 0) { prop.intValue = 2; so.ApplyModifiedProperties(); }
            }
        }

        static void SetupMaterials()
        {
            var baseTex = SaveTexture("Palet_basiskleur", VoxelAssets.BaseColorTexture(), true);
            var maskTex = SaveTexture("Palet_mask", VoxelAssets.MaskTexture(), false);
            var emisTex = SaveTexture("Palet_emissie", VoxelAssets.EmissionTexture(), false);
            SaveMaterial("VoxelLit", VoxelAssets.CreateVoxelMaterial(baseTex, maskTex, emisTex));
            SaveMaterial("VoxelGlass", VoxelAssets.CreateGlassMaterial(baseTex));
            SaveMaterial("Highlight", VoxelAssets.CreateHighlightMaterial());
        }

        static Texture2D SaveTexture(string name, Texture2D tex, bool srgb)
        {
            string path = $"{ResDir}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = srgb;
            ti.mipmapEnabled = false;
            ti.filterMode = FilterMode.Bilinear;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static void SaveMaterial(string name, Material m)
        {
            string path = $"{ResDir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                existing.shader = m.shader;
                existing.CopyPropertiesFromMaterial(m);
                existing.shaderKeywords = m.shaderKeywords;
                HDMaterial.ValidateMaterial(existing);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(m);
                return;
            }
            AssetDatabase.CreateAsset(m, path);
        }

        static void SetupScene()
        {
            if (!File.Exists(ScenePath))
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var go = new GameObject("Deadhaul");
                go.AddComponent<GameState>();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            if (SceneManager.GetActiveScene().path != ScenePath) EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Deadhaul/Save wissen", priority = 20)]
        static void DeleteSave()
        {
            string p = Path.Combine(Application.persistentDataPath, "deadhaul.sav");
            if (File.Exists(p)) { File.Delete(p); Debug.Log("Deadhaul: save gewist."); }
            else Debug.Log("Deadhaul: er is geen save.");
        }

        [MenuItem("Deadhaul/Map met saves openen", priority = 21)]
        static void OpenSaveFolder() => EditorUtility.RevealInFinder(Application.persistentDataPath);
    }
}
