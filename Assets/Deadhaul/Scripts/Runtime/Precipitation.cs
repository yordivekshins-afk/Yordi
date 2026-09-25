using System.Collections.Generic;
using Deadhaul.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Deadhaul
{
    /// <summary>
    /// Regen, sneeuw en stralingsstof rond de camera. Druppels vallen niet door daken:
    /// elke kolom kent zijn hoogste blok, en daar spatten ze uiteen.
    /// </summary>
    public sealed class Precipitation : MonoBehaviour
    {
        const int Max = 1800;
        const float Radius = 16f, Above = 14f;
        struct Drop { public Vector3 Pos; public float Floor, Speed, Phase; }
        readonly Drop[] drops = new Drop[Max];
        readonly Matrix4x4[] buffer = new Matrix4x4[1023];
        readonly Dictionary<long, float> tops = new Dictionary<long, float>();
        Mesh rainMesh, snowMesh, dustMesh;
        Material mat;
        ChunkManager chunks;
        Transform cam;
        int active;
        Vector3 lastClear;

        public float Rain, Snow, Dust, Wind;
        public Vector3 WindDir = new Vector3(1, 0, 0.3f);

        public void Init(ChunkManager c, Transform camera)
        {
            chunks = c; cam = camera;
            mat = new Material(VoxelAssets.VoxelMaterial) { name = "Neerslag", enableInstancing = true };
            rainMesh = VoxelCharacter.ModelMesh(new[] { B.Ice }, 1, 1, 1, 1f, 0.5f, 0.5f, 0.5f, "regendruppel");
            snowMesh = VoxelCharacter.ModelMesh(new[] { B.Snow }, 1, 1, 1, 1f, 0.5f, 0.5f, 0.5f, "sneeuwvlok");
            dustMesh = VoxelCharacter.ModelMesh(new[] { B.RadCrystal }, 1, 1, 1, 1f, 0.5f, 0.5f, 0.5f, "stralingsstof");
            for (int i = 0; i < Max; i++) drops[i].Phase = Random.value * 100f;
        }

        /// <summary>Hoogte (meter) van het hoogste vaste blok of water in deze kolom.</summary>
        float TopAt(float x, float z)
        {
            int vx = Mathf.FloorToInt(x / World.VoxelSize), vz = Mathf.FloorToInt(z / World.VoxelSize);
            long k = ((long)vx << 32) ^ (uint)vz;
            if (tops.TryGetValue(k, out var y)) return y;
            var store = chunks.Store;
            y = -100f;
            if (store.IsLoaded(VoxelStore.ChunkOf(vx), VoxelStore.ChunkOf(vz)))
                for (int vy = World.Height - 1; vy > 0; vy--)
                {
                    byte b = store.Get(vx, vy, vz);
                    if (b != B.Air && (Blocks.Solid[b] || b == B.Water || b == B.Leaves)) { y = (vy + 1) * World.VoxelSize; break; }
                }
            tops[k] = y;
            return y;
        }

        /// <summary>Na een blokwijziging moet de kolom opnieuw bekeken worden.</summary>
        public void Invalidate(int vx, int vz) => tops.Remove(((long)vx << 32) ^ (uint)vz);

        void Respawn(ref Drop d, Vector3 c, bool anywhere)
        {
            var o = Random.insideUnitCircle * Radius;
            float y = anywhere ? c.y + Random.Range(-4f, Above) : c.y + Above + Random.Range(0f, 4f);
            d.Pos = new Vector3(c.x + o.x, y, c.z + o.y);
            d.Floor = TopAt(d.Pos.x, d.Pos.z);
            d.Speed = Random.Range(0.85f, 1.15f);
        }

        void LateUpdate()
        {
            if (cam == null) return;
            float amount = Mathf.Max(Rain, Mathf.Max(Snow, Dust));
            int want = Mathf.RoundToInt(Max * Mathf.Clamp01(amount));
            var c = cam.position;
            if ((c - lastClear).sqrMagnitude > 60f * 60f || tops.Count > 20000) { tops.Clear(); lastClear = c; }
            while (active < want) { Respawn(ref drops[active], c, true); active++; }
            if (active > want) active = want;
            if (active == 0) return;

            float dt = Time.deltaTime;
            bool snowy = Snow > Rain && Snow > Dust, dusty = Dust > Rain && Dust > Snow;
            float fall = snowy ? 1.6f : dusty ? 0.5f : 17f;
            var wind = WindDir.normalized * Wind * (snowy ? 3f : dusty ? 9f : 4f);
            float t = Time.time;
            var scale = snowy ? Vector3.one * 0.035f : dusty ? Vector3.one * 0.02f : new Vector3(0.008f, 0.42f, 0.008f);
            var tilt = Quaternion.FromToRotation(Vector3.down, (Vector3.down * fall + wind).normalized);
            int n = 0;
            for (int i = 0; i < active; i++)
            {
                ref var d = ref drops[i];
                var v = Vector3.down * fall * d.Speed + wind;
                if (snowy || dusty) v += new Vector3(Mathf.Sin(t * 1.3f + d.Phase), dusty ? Mathf.Sin(t * 0.7f + d.Phase) * 0.8f : 0, Mathf.Cos(t * 1.1f + d.Phase)) * 0.5f;
                d.Pos += v * dt;
                var rel = d.Pos - c;
                bool outside = rel.x * rel.x + rel.z * rel.z > Radius * Radius * 1.1f || rel.y < -8f;
                if (d.Pos.y <= d.Floor || outside)
                {
                    if (!outside && !snowy && !dusty && Random.value < 0.08f) Fx.Instance.Emit(new Vector3(d.Pos.x, d.Floor + 0.02f, d.Pos.z), Random.insideUnitSphere * 0.6f + Vector3.up * 0.8f, B.Ice, 0.012f, 0.18f, 6f);
                    Respawn(ref d, c, false);
                    continue;
                }
                buffer[n++] = Matrix4x4.TRS(d.Pos, snowy || dusty ? Quaternion.identity : tilt, scale);
                if (n == buffer.Length) { Draw(snowy ? snowMesh : dusty ? dustMesh : rainMesh, n); n = 0; }
            }
            if (n > 0) Draw(snowy ? snowMesh : dusty ? dustMesh : rainMesh, n);
        }

        void Draw(Mesh mesh, int n)
        {
            var rp = new RenderParams(mat) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
            Graphics.RenderMeshInstanced(rp, mesh, 0, buffer, n);
        }
    }
}
