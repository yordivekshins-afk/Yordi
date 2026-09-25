// Minimale nabootsing van de gebruikte Unity-, HDRP-, Input System- en Editor-API's, alleen om te typechecken.
#pragma warning disable
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public class Object { public string name; public static void Destroy(Object o) { } public static void DestroyImmediate(Object o) { } public static implicit operator bool(Object o) => o != null; }
    public class Component : Object { public GameObject gameObject; public Transform transform; public T GetComponent<T>() => default; public T[] GetComponentsInChildren<T>() => default; public T AddComponent<T>() where T : Component => default; }
    public class Behaviour : Component { public bool enabled; }
    public class MonoBehaviour : Behaviour { }
    public class ScriptableObject : Object { public static T CreateInstance<T>() where T : ScriptableObject => default; }
    public sealed class GameObject : Object
    {
        public GameObject() { } public GameObject(string n) { }
        public Transform transform; public string tag; public bool isStatic;
        public T AddComponent<T>() where T : Component => default; public T GetComponent<T>() => default;
        public void SetActive(bool v) { } public static GameObject CreatePrimitive(PrimitiveType t) => null;
    }
    public enum PrimitiveType { Sphere, Capsule, Cylinder, Cube, Plane, Quad }
    public class Transform : Component
    {
        public Vector3 position, localPosition, localScale, forward, right, up; public Quaternion rotation, localRotation;
        public void SetParent(Transform p, bool w) { } public int childCount; public Transform GetChild(int i) => null;
    }
    public struct Vector2 { public float x, y; public Vector2(float a, float b) { x = a; y = b; } public static Vector2 zero; public float magnitude => 0; public float sqrMagnitude => 0;
        public static Vector2 ClampMagnitude(Vector2 v, float m) => v; public static Vector2 operator *(Vector2 a, float s) => a; }
    public struct Vector2Int : IEquatable<Vector2Int> { public Vector2Int(int a, int b) { } public bool Equals(Vector2Int o) => true; public static bool operator ==(Vector2Int a, Vector2Int b) => true; public static bool operator !=(Vector2Int a, Vector2Int b) => false; public override bool Equals(object o) => true; public override int GetHashCode() => 0; }
    public struct Vector3
    {
        public float x, y, z; public Vector3(float a, float b, float c) { x = a; y = b; z = c; }
        public static Vector3 zero, one, up, back, forward; public float sqrMagnitude => 0; public float magnitude => 0;
        public static Vector3 operator +(Vector3 a, Vector3 b) => a; public static Vector3 operator -(Vector3 a, Vector3 b) => a;
        public static Vector3 operator *(Vector3 a, float s) => a; public static Vector3 operator *(float s, Vector3 a) => a;
        public static float Distance(Vector3 a, Vector3 b) => 0; public static float Dot(Vector3 a, Vector3 b) => 0; public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => a;
        public Vector3 normalized => this; public static Vector3 operator -(Vector3 a) => a; public static Vector3 operator /(Vector3 a, float s) => a;
        public static Vector3 right, left, down;
    }
    public struct Quaternion { public static Quaternion identity; public static Quaternion Euler(float x, float y, float z) => default; public static Quaternion LookRotation(Vector3 f) => default;
        public static Quaternion Slerp(Quaternion a, Quaternion b, float t) => a; public static Vector3 operator *(Quaternion q, Vector3 v) => v; }
    public struct Color { public float r, g, b, a; public Color(float r, float g, float b, float a = 1) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white, black, grey; public Color linear => this; public static Color Lerp(Color a, Color b, float t) => a; public static Color operator *(Color c, float f) => c; }
    public struct Rect { public float x, y, width, height, xMax, yMax; public Rect(float x, float y, float w, float h) { this.x = x; this.y = y; width = w; height = h; xMax = 0; yMax = 0; } public bool Contains(Vector2 p) => true; }
    public static class Mathf
    {
        public const float Deg2Rad = 0.017f, PI = 3.14f;
        public static float Min(float a, float b) => a; public static float LerpAngle(float a, float b, float t) => a; public static float Max(float a, float b) => a; public static int Max(int a, int b) => a;
        public static float Clamp(float v, float a, float b) => v; public static float Clamp01(float v) => v; public static float Lerp(float a, float b, float t) => a;
        public static float InverseLerp(float a, float b, float t) => a; public static float Sin(float f) => f; public static float Cos(float f) => f; public static float Abs(float f) => f; public static int Abs(int f) => f;
        public static float Exp(float f) => f; public static float Repeat(float t, float l) => t; public static int RoundToInt(float f) => 0; public static int FloorToInt(float f) => 0;
        public static float PerlinNoise(float x, float y) => 0; public const float Rad2Deg = 57.3f; public static float Atan2(float y, float x) => 0; public static float MoveTowards(float a, float b, float d) => a; public static float Sqrt(float f) => f; public static float Sign(float f) => f; public static int Min(int a, int b) => a;
    }
    public static class Time { public static float deltaTime, time, unscaledDeltaTime; }
    public static class Debug { public static void Log(object o) { } public static void LogWarning(object o) { } public static void LogException(Exception e) { } }
    public static class Application { public static int targetFrameRate; public static string persistentDataPath; public static void Quit() { } }
    public static class SystemInfo { public static bool supportsRayTracing; public static int processorCount; }
    public static class Screen { public static int width, height; }
    public enum CursorLockMode { None, Locked, Confined }
    public static class Cursor { public static CursorLockMode lockState; public static bool visible; }
    public static class Resources { public static T Load<T>(string p) where T : Object => default; }
    public enum LightType { Spot, Directional, Point, Rectangle, Disc, Pyramid, Box, Tube }
    public enum LightShadows { None, Hard, Soft }
    public sealed class Light : Behaviour { public LightType type; public Color color; public float intensity, range, spotAngle, innerSpotAngle; public LightShadows shadows; public bool enableSpotReflector; }
    public sealed class Camera : Behaviour { public static Camera main; public float nearClipPlane, farClipPlane, fieldOfView; }
    public sealed class AudioListener : Behaviour { }
    public class Collider : Component { }
    public class Renderer : Component { public Material sharedMaterial; public Material[] sharedMaterials; public UnityEngine.Rendering.ShadowCastingMode shadowCastingMode; public bool receiveShadows; }
    public sealed class MeshRenderer : Renderer { }
    public sealed class MeshFilter : Component { public Mesh sharedMesh; }
    public sealed class Mesh : Object
    {
        public Vector3[] vertices, normals; public Vector2[] uv; public int subMeshCount; public UnityEngine.Rendering.IndexFormat indexFormat;
        public void MarkDynamic() { } public void Clear() { } public void SetTriangles(int[] t, int s, bool b = true) { } public void SetTriangles(List<int> t, int s, bool b = true) { } public void RecalculateBounds() { }
    }
    public class Shader : Object { public static Shader Find(string n) => null; }
    public class Material : Object
    {
        public Material(Shader s) { } public Material(Material m) { } public bool enableInstancing; public Shader shader; public string[] shaderKeywords;
        public void SetColor(string n, Color c) { } public void SetTexture(string n, Texture t) { } public void SetFloat(string n, float f) { } public float GetFloat(string n) => 0;
        public void CopyPropertiesFromMaterial(Material m) { }
    }
    public class Texture : Object { public FilterMode filterMode; public TextureWrapMode wrapMode; public int anisoLevel; }
    public enum TextureFormat { RGBA32, RGBAHalf }
    public enum FilterMode { Point, Bilinear, Trilinear }
    public enum TextureWrapMode { Repeat, Clamp }
    public sealed class Texture2D : Texture
    {
        public Texture2D(int w, int h, TextureFormat f, bool mip, bool linear) { } public Texture2D(int w, int h, TextureFormat f, bool mip) { }
        public static Texture2D whiteTexture; public void SetPixelData<T>(T[] d, int mip, int start = 0) { } public void Apply(bool a, bool b) { } public void Apply() { } public void SetPixels(Color[] c) { }
        public byte[] EncodeToPNG() => null;
    }
    public enum CubemapFace { PositiveX, NegativeX, PositiveY, NegativeY, PositiveZ, NegativeZ }
    public sealed class Cubemap : Texture { public Cubemap(int s, TextureFormat f, bool m) { } public void SetPixels(Color[] c, CubemapFace f) { } public void Apply(bool a, bool b) { } }
    public enum ColorSpace { Gamma, Linear }
    public static class QualitySettings { public static string[] names; public static int GetQualityLevel() => 0; public static void SetQualityLevel(int i, bool b) { } public static UnityEngine.Rendering.RenderPipelineAsset renderPipeline; }
    public enum TextAnchor { UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight }
    public enum FontStyle { Normal, Bold }
    public class GUIStyleState { public Color textColor; }
    public class GUIStyle { public GUIStyle() { } public GUIStyle(GUIStyle o) { } public bool wordWrap; public int fontSize; public FontStyle fontStyle; public TextAnchor alignment; public bool richText; public GUIStyleState normal, hover; }
    public class GUISkin { public GUIStyle label, button, box; }
    public static class GUI
    {
        public static GUISkin skin; public static Color color; public static bool enabled; public static Matrix4x4 matrix;
        public static void DrawTexture(Rect r, Texture t) { } public static void Label(Rect r, string s, GUIStyle st) { }
        public static bool Button(Rect r, string s, GUIStyle st) => false; public static Vector2 BeginScrollView(Rect p, Vector2 s, Rect v) => s; public static void EndScrollView() { } public static float HorizontalSlider(Rect r, float v, float a, float b) => v;
    }
    public enum EventType { MouseDown }
    public class Event { public static Event current; public EventType type; public Vector2 mousePosition; public int button; public void Use() { } }
    public class SerializeField : Attribute { }
}
namespace UnityEngine
{
    public struct Matrix4x4 { public static Matrix4x4 TRS(Vector3 p, Quaternion q, Vector3 s) => default; public static Matrix4x4 identity; }
    public sealed class AudioClip : Object { public static AudioClip Create(string n, int len, int ch, int freq, bool stream) => null; public bool SetData(float[] d, int off) => true; }
    public enum AudioRolloffMode { Logarithmic, Linear, Custom }
    public sealed class AudioSource : Behaviour
    {
        public float spatialBlend, minDistance, maxDistance, dopplerLevel, volume, pitch; public AudioRolloffMode rolloffMode; public AudioClip clip; public bool loop;
        public void Play() { } public void Stop() { } public bool isPlaying; public void PlayOneShot(AudioClip c) { } public void PlayOneShot(AudioClip c, float v) { }
    }
    public static class Random { public static Quaternion rotation; public static Vector3 insideUnitSphere; public static float Range(float a, float b) => a; public static int Range(int a, int b) => a; }
    public struct RenderParams { public RenderParams(Material m) { material = m; shadowCastingMode = default; receiveShadows = true; } public Material material; public UnityEngine.Rendering.ShadowCastingMode shadowCastingMode; public bool receiveShadows; }
    public static class Graphics { public static void RenderMeshInstanced<T>(in RenderParams rp, Mesh mesh, int submesh, T[] data, int count = -1, int start = 0) where T : unmanaged { } }
    public static class GUIUtility { public static void RotateAroundPivot(float angle, Vector2 pivot) { } }
}
namespace UnityEngine.SceneManagement { public struct Scene { public string path; } public static class SceneManager { public static Scene GetActiveScene() => default; } }
namespace UnityEngine.Rendering
{
    public enum ShadowCastingMode { Off, On, TwoSided, ShadowsOnly }
    public enum IndexFormat { UInt16, UInt32 }
    public enum GraphicsDeviceType { Direct3D11, Direct3D12, Vulkan }
    public abstract class RenderPipelineAsset : ScriptableObject { }
    public static class GraphicsSettings { public static RenderPipelineAsset defaultRenderPipeline; }
    public class VolumeParameter { public bool overrideState; }
    public class VolumeParameter<T> : VolumeParameter { public T value; public void Override(T x) { } }
    public class BoolParameter : VolumeParameter<bool> { }
    public class FloatParameter : VolumeParameter<float> { }
    public class MinFloatParameter : FloatParameter { }
    public class ClampedFloatParameter : FloatParameter { }
    public class NoInterpMinFloatParameter : FloatParameter { }
    public class IntParameter : VolumeParameter<int> { }
    public class NoInterpIntParameter : VolumeParameter<int> { }
    public class NoInterpClampedIntParameter : VolumeParameter<int> { }
    public class ColorParameter : VolumeParameter<Color> { }
    public class CubemapParameter : VolumeParameter<Texture> { }
    public class VolumeComponent : ScriptableObject { }
    public class VolumeComponentWithQuality : VolumeComponent { }
    public sealed class VolumeProfile : ScriptableObject { public T Add<T>(bool overrides = false) where T : VolumeComponent => default; }
    public sealed class Volume : MonoBehaviour { public bool isGlobal; public float priority; public VolumeProfile sharedProfile; }
}
namespace UnityEngine.Rendering.HighDefinition
{
    public enum SkyType { HDRI = 1, Procedural = 2, Gradient = 3, PhysicallyBased = 4 }
    public enum SkyAmbientMode { Static, Dynamic }
    public class SkyAmbientModeParameter : VolumeParameter<SkyAmbientMode> { }
    public sealed class VisualEnvironment : VolumeComponent { public NoInterpIntParameter skyType; public SkyAmbientModeParameter skyAmbientMode; }
    public sealed class PhysicallyBasedSky : VolumeComponent { public CubemapParameter spaceEmissionTexture; public MinFloatParameter spaceEmissionMultiplier; public ColorParameter groundTint; }
    public sealed class VolumetricClouds : VolumeComponent { public enum CloudPresets { Sparse, Cloudy, Overcast, Stormy, Custom } public BoolParameter enable; public CloudPresets cloudPreset { get; set; } }
    public sealed class Fog : VolumeComponent { public BoolParameter enabled, enableVolumetricFog; public MinFloatParameter meanFreePath, depthExtent; public FloatParameter baseHeight, maximumHeight; public ColorParameter albedo; public ClampedFloatParameter anisotropy; }
    public class HDShadowSettings : VolumeComponent { public NoInterpMinFloatParameter maxShadowDistance; public NoInterpClampedIntParameter cascadeShadowSplitCount; }
    public class ContactShadows : VolumeComponentWithQuality { public BoolParameter enable; public ClampedFloatParameter length; }
    public enum ExposureMode { Fixed = 0, Automatic = 1, AutomaticHistogram = 4 }
    public class ExposureModeParameter : VolumeParameter<ExposureMode> { }
    public sealed class Exposure : VolumeComponent { public ExposureModeParameter mode; public FloatParameter limitMin, limitMax, compensation; }
    public enum TonemappingMode { None, Neutral, ACES }
    public class TonemappingModeParameter : VolumeParameter<TonemappingMode> { }
    public sealed class Tonemapping : VolumeComponent { public TonemappingModeParameter mode; }
    public sealed class Bloom : VolumeComponentWithQuality { public ClampedFloatParameter intensity, scatter; }
    public sealed class ColorAdjustments : VolumeComponent { public ClampedFloatParameter saturation, contrast; public ColorParameter colorFilter; }
    public sealed class Vignette : VolumeComponent { public ClampedFloatParameter intensity, smoothness; }
    public enum FilmGrainLookup { Thin1, Medium3 }
    public class FilmGrainLookupParameter : VolumeParameter<FilmGrainLookup> { }
    public sealed class FilmGrain : VolumeComponent { public FilmGrainLookupParameter type; public ClampedFloatParameter intensity; }
    public enum RayCastingMode { RayMarching = 1, RayTracing = 2, Mixed = 4 }
    public class RayCastingModeParameter : VolumeParameter<RayCastingMode> { }
    public enum RayTracingMode { Performance = 1, Quality = 2 }
    public class RayTracingModeParameter : VolumeParameter<RayTracingMode> { }
    public sealed class ScreenSpaceReflection : VolumeComponentWithQuality { public BoolParameter enabled; public RayCastingModeParameter tracing; public RayTracingModeParameter mode; }
    public sealed class GlobalIllumination : VolumeComponentWithQuality { public BoolParameter enable; public RayCastingModeParameter tracing; public RayTracingModeParameter mode; }
    public sealed class ScreenSpaceAmbientOcclusion : VolumeComponentWithQuality { public BoolParameter rayTracing; public ClampedFloatParameter intensity, radius; }
    public sealed class RayTracingSettings : VolumeComponent { }
    public enum WaterSurfaceType { OceanSeaLake, River, Pool }
    public enum WaterGeometryType { Quad, Custom, InstancedQuads, Infinite }
    public sealed class WaterSurface : MonoBehaviour { public WaterSurfaceType surfaceType; public WaterGeometryType geometryType; public float largeWindSpeed, largeChaos, largeBand0Multiplier, absorptionDistance, maxRefractionDistance; public bool ripples, caustics; public Color refractionColor, scatteringColor; }
    public sealed class HDAdditionalLightData : MonoBehaviour { public float angularDiameter; public bool interactsWithSky, useRayTracedShadows, filterTracedShadow; public void SetShadowResolution(int r) { } }
    public static class GameObjectExtension { public static HDAdditionalLightData AddHDLight(this GameObject g, LightType t) => null; }
    public sealed class HDAdditionalCameraData : MonoBehaviour { public enum AntialiasingMode { None, FastApproximateAntialiasing, TemporalAntialiasing } public AntialiasingMode antialiasing; }
    public enum EmissiveIntensityUnit { Nits, EV100 }
    public static class HDMaterial
    {
        public static bool ValidateMaterial(Material m) => true; public static void SetSurfaceType(Material m, bool t) { }
        public static void SetUseEmissiveIntensity(Material m, bool v) { } public static void SetEmissiveColor(Material m, Color c) { }
        public static void SetEmissiveIntensity(Material m, float i, EmissiveIntensityUnit u) { }
    }
    public struct RenderPipelineSettings
    {
        public enum SupportedRayTracingMode { Performance = 1, Quality = 2, Both = 3 }
        public bool supportRayTracing, supportWater, supportVolumetricClouds, supportVolumetrics, supportSSGI, supportSSR, supportSSRTransparent, supportSSAO, supportMotionVectors, supportTransparentBackface;
        public SupportedRayTracingMode supportedRayTracingMode;
    }
    public class HDRenderPipelineAsset : RenderPipelineAsset { public RenderPipelineSettings currentPlatformRenderPipelineSettings { get; set; } }
}
namespace UnityEngine.InputSystem
{
    namespace Controls { public class ButtonControl { public bool isPressed, wasPressedThisFrame; } public class KeyControl : ButtonControl { } public class Vector2Control { public Vector2 ReadValue() => default; } }
    public enum Key { None, Space, Digit1 = 41, Digit2, Digit3, Digit4, Digit5, Digit6 }
    public class Keyboard
    {
        public static Keyboard current; public Controls.KeyControl this[Key k] => null;
        public Controls.KeyControl lKey, rKey, bKey, wKey, aKey, sKey, dKey, cKey, eKey, qKey, fKey, vKey, hKey, iKey, tabKey, spaceKey, escapeKey, leftShiftKey, leftCtrlKey, f5Key;
    }
    public class Mouse { public static Mouse current; public Controls.Vector2Control delta, scroll; public Controls.ButtonControl leftButton, rightButton; }
}
namespace UnityEditor
{
    public class MenuItem : Attribute { public MenuItem(string s) { } public int priority; }
    public class InitializeOnLoadAttribute : Attribute { }
    public static class EditorApplication { public static Action delayCall; public static bool isPlaying; public static bool ExecuteMenuItem(string s) => true; }
    public static class SessionState { public static bool GetBool(string k, bool d) => d; public static void SetBool(string k, bool v) { } }
    public static class EditorUtility { public static bool DisplayDialog(string a, string b, string c, string d = "") => true; public static void SetDirty(UnityEngine.Object o) { } public static void RevealInFinder(string p) { } }
    public enum ImportAssetOptions { Default, ForceSynchronousImport }
    public static class AssetDatabase
    {
        public static T LoadAssetAtPath<T>(string p) where T : UnityEngine.Object => default; public static void CreateAsset(UnityEngine.Object o, string p) { }
        public static void Refresh() { } public static void SaveAssets() { } public static void ImportAsset(string p, ImportAssetOptions o) { }
        public static UnityEngine.Object[] LoadAllAssetsAtPath(string p) => null;
    }
    public class AssetImporter : UnityEngine.Object { public static AssetImporter GetAtPath(string p) => null; public void SaveAndReimport() { } }
    public enum TextureImporterType { Default }
    public enum TextureImporterNPOTScale { None }
    public enum TextureImporterCompression { Uncompressed }
    public enum TextureImporterAlphaSource { FromInput }
    public class TextureImporter : AssetImporter { public TextureImporterType textureType; public bool sRGBTexture, mipmapEnabled; public UnityEngine.FilterMode filterMode; public UnityEngine.TextureWrapMode wrapMode; public TextureImporterNPOTScale npotScale; public TextureImporterCompression textureCompression; public TextureImporterAlphaSource alphaSource; }
    public enum BuildTarget { StandaloneWindows64 }
    public static class PlayerSettings { public static UnityEngine.ColorSpace colorSpace; public static string productName, companyName; public static void SetUseDefaultGraphicsAPIs(BuildTarget t, bool b) { } public static void SetGraphicsAPIs(BuildTarget t, UnityEngine.Rendering.GraphicsDeviceType[] a) { } }
    public class SerializedProperty { public int intValue; }
    public class SerializedObject { public SerializedObject(UnityEngine.Object o) { } public SerializedProperty FindProperty(string s) => null; public bool ApplyModifiedProperties() => true; }
    public class EditorBuildSettingsScene { public EditorBuildSettingsScene(string p, bool e) { } }
    public static class EditorBuildSettings { public static EditorBuildSettingsScene[] scenes; }
}
namespace UnityEditor.SceneManagement
{
    public enum NewSceneSetup { EmptyScene, DefaultGameObjects }
    public enum NewSceneMode { Single, Additive }
    public static class EditorSceneManager
    {
        public static bool SaveCurrentModifiedScenesIfUserWantsTo() => true;
        public static UnityEngine.SceneManagement.Scene NewScene(NewSceneSetup s, NewSceneMode m) => default;
        public static bool SaveScene(UnityEngine.SceneManagement.Scene s, string p) => true;
        public static UnityEngine.SceneManagement.Scene OpenScene(string p) => default;
    }
}
