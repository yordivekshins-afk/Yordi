using System.Collections.Generic;
using Deadhaul.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Deadhaul
{
    /// <summary>
    /// Voxeldeeltjes (vonken, stof, bloed, scherven, hulzen), lichtsporen en mondingsvuur.
    /// Getekend met GPU-instancing: één draw call per kleur.
    /// </summary>
    public sealed class Fx : MonoBehaviour
    {
        struct P
        {
            public Vector3 Pos, Vel, Scale;
            public Quaternion Rot;
            public float Life, MaxLife, Gravity, Drag;
            public byte Color;
            public bool Stretch;
        }

        const int Max = 2500;
        readonly P[] parts = new P[Max];
        int count;
        readonly Dictionary<byte, Mesh> meshes = new Dictionary<byte, Mesh>();
        readonly Dictionary<byte, List<Matrix4x4>> batches = new Dictionary<byte, List<Matrix4x4>>();
        readonly Matrix4x4[] buffer = new Matrix4x4[1023];
        Material mat;
        readonly List<Light> flashes = new List<Light>();
        readonly List<float> flashTimes = new List<float>();
        public static Fx Instance { get; private set; }

        void Awake()
        {
            Instance = this;
            mat = new Material(VoxelAssets.VoxelMaterial) { name = "VoxelFx", enableInstancing = true };
            for (int i = 0; i < 6; i++)
            {
                var go = new GameObject("Flits");
                go.transform.SetParent(transform, false);
                go.AddHDLight(LightType.Point);
                var l = go.GetComponent<Light>();
                l.color = new Color(1f, 0.78f, 0.45f);
                l.range = 14f;
                l.shadows = LightShadows.None;
                l.enabled = false;
                flashes.Add(l); flashTimes.Add(0);
            }
        }

        Mesh MeshFor(byte color)
        {
            if (meshes.TryGetValue(color, out var m)) return m;
            m = VoxelCharacter.ModelMesh(new[] { color }, 1, 1, 1, 1f, 0.5f, 0.5f, 0.5f, "fx " + color);
            meshes[color] = m;
            return m;
        }

        public void Emit(Vector3 pos, Vector3 vel, byte color, float size, float life, float gravity = 9.8f, float drag = 0.5f)
        {
            if (count >= Max) return;
            parts[count++] = new P
            {
                Pos = pos, Vel = vel, Scale = Vector3.one * size, Rot = Random.rotation, Life = life, MaxLife = life,
                Gravity = gravity, Drag = drag, Color = color
            };
        }

        /// <summary>Uitgerekt lichtend blokje langs een baan (kogel-lichtspoor).</summary>
        public void Streak(Vector3 from, Vector3 to, byte color, float width, float life)
        {
            if (count >= Max) return;
            var d = to - from;
            float len = d.magnitude;
            if (len < 0.01f) return;
            parts[count++] = new P
            {
                Pos = (from + to) * 0.5f, Vel = Vector3.zero, Scale = new Vector3(width, width, len), Rot = Quaternion.LookRotation(d / len),
                Life = life, MaxLife = life, Gravity = 0, Drag = 0, Color = color, Stretch = true
            };
        }

        public void Burst(Vector3 pos, Vector3 normal, byte color, int n, float speed, float size, float life, float gravity = 9.8f)
        {
            for (int i = 0; i < n; i++)
            {
                var v = (normal + Random.insideUnitSphere * 0.9f).normalized * speed * Random.Range(0.4f, 1.1f);
                Emit(pos, v, color, size * Random.Range(0.6f, 1.2f), life * Random.Range(0.6f, 1.2f), gravity);
            }
        }

        /// <summary>Wat er gebeurt als een kogel een blok raakt.</summary>
        public void Impact(Vector3 pos, Vector3 normal, byte block)
        {
            var info = Blocks.Info[block];
            bool metal = info.Metallic > 0.4f;
            if (metal) Burst(pos, normal, B.Spark, 7, 6f, 0.025f, 0.25f, 6f);
            Burst(pos, normal, block == B.Glass ? B.Glass : B.Dust, 5, 2.2f, 0.04f, 0.7f, 4f);
            Burst(pos, normal, block, 4, 3f, 0.05f, 1.2f);
        }

        public void Blood(Vector3 pos, Vector3 dir, int n = 10)
        {
            Burst(pos, dir, B.Blood, n, 2.6f, 0.05f, 0.9f);
        }

        public void MuzzleFlash(Vector3 pos, Vector3 dir, bool suppressed)
        {
            if (!suppressed)
            {
                for (int i = 0; i < 5; i++) Emit(pos + dir * (0.05f + i * 0.04f), dir * 0.5f + Random.insideUnitSphere * 0.4f, B.Flash, 0.06f - i * 0.008f, 0.045f, 0, 0);
                for (int i = 0; i < flashes.Count; i++)
                {
                    if (flashTimes[i] > 0) continue;
                    flashes[i].transform.position = pos + dir * 0.15f;
                    flashes[i].intensity = 900f;
                    flashes[i].enabled = true;
                    flashTimes[i] = 0.05f;
                    break;
                }
            }
            Emit(pos, dir * 0.4f + Vector3.up * 0.3f, B.Dust, 0.07f, 0.6f, -0.3f, 2f);
        }

        public void Casing(Vector3 pos, Vector3 right)
        {
            Emit(pos, right * Random.Range(1.5f, 2.5f) + Vector3.up * Random.Range(1f, 2f), B.Brass, 0.018f, 1.2f, 9.8f, 0.2f);
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < flashes.Count; i++)
            {
                if (flashTimes[i] <= 0) continue;
                flashTimes[i] -= dt;
                if (flashTimes[i] <= 0) flashes[i].enabled = false;
            }
            foreach (var b in batches.Values) b.Clear();
            for (int i = 0; i < count; i++)
            {
                ref var p = ref parts[i];
                p.Life -= dt;
                if (p.Life <= 0) { parts[i] = parts[--count]; i--; continue; }
                if (!p.Stretch)
                {
                    p.Vel.y -= p.Gravity * dt;
                    p.Vel *= 1f - p.Drag * dt;
                    p.Pos += p.Vel * dt;
                }
                float fade = Mathf.Clamp01(p.Life / p.MaxLife * 3f);
                if (!batches.TryGetValue(p.Color, out var list)) batches[p.Color] = list = new List<Matrix4x4>(64);
                list.Add(Matrix4x4.TRS(p.Pos, p.Rot, p.Scale * (p.Stretch ? 1f : fade)));
            }
            var rp = new RenderParams(mat) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
            foreach (var kv in batches)
            {
                var list = kv.Value;
                if (list.Count == 0) continue;
                var mesh = MeshFor(kv.Key);
                for (int start = 0; start < list.Count; start += buffer.Length)
                {
                    int n = Mathf.Min(buffer.Length, list.Count - start);
                    list.CopyTo(start, buffer, 0, n);
                    Graphics.RenderMeshInstanced(rp, mesh, 0, buffer, n);
                }
            }
        }
    }
}
