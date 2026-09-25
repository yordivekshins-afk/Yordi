using System.Collections.Generic;
using Deadhaul.Core;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Deadhaul
{
    /// <summary>Flakkerende puntlichten op kampvuren. Alleen vuren dicht bij de speler krijgen een echt licht.</summary>
    public sealed class Campfires : MonoBehaviour
    {
        const int MaxLights = 12;
        const float LightRange = 60f;

        readonly HashSet<long> fires = new HashSet<long>();
        readonly Dictionary<long, Vector3> positions = new Dictionary<long, Vector3>();
        readonly List<Light> pool = new List<Light>();
        VoxelStore store;
        public Transform Follow;

        public void Init(ChunkManager chunks)
        {
            store = chunks.Store;
            chunks.ChunkLoaded += OnChunkLoaded;
            for (int i = 0; i < MaxLights; i++)
            {
                var go = new GameObject("Vuurlicht");
                go.transform.SetParent(transform, false);
                go.AddHDLight(LightType.Point);
                var l = go.GetComponent<Light>();
                l.color = new Color(1f, 0.58f, 0.25f);
                l.range = 14f;
                l.shadows = LightShadows.Soft;
                l.enabled = false;
                pool.Add(l);
            }
        }

        void OnChunkLoaded(int cx, int cz)
        {
            var pad = store.GetPadded(cx, cz);
            if (pad == null) return;
            for (int lz = 0; lz < World.ChunkSize; lz++)
                for (int lx = 0; lx < World.ChunkSize; lx++)
                    for (int y = 0; y < World.Height; y++)
                        if (pad[World.IdxPad(lx + 1, y, lz + 1)] == B.Campfire)
                            Add(cx * World.ChunkSize + lx, y, cz * World.ChunkSize + lz);
        }

        public void OnBlockChanged(int x, int y, int z, byte b)
        {
            long k = VoxelStore.VoxelKey(x, y, z);
            if (b == B.Campfire) Add(x, y, z);
            else if (fires.Remove(k)) positions.Remove(k);
        }

        void Add(int x, int y, int z)
        {
            long k = VoxelStore.VoxelKey(x, y, z);
            if (fires.Add(k)) positions[k] = new Vector3((x + 0.5f) * World.VoxelSize, (y + 1.2f) * World.VoxelSize, (z + 0.5f) * World.VoxelSize);
        }

        void Update()
        {
            if (Follow == null) return;
            var p = Follow.position;
            int used = 0;
            foreach (var kv in positions)
            {
                if (used >= MaxLights) break;
                if ((kv.Value - p).sqrMagnitude > LightRange * LightRange) continue;
                if (store.Get(Mathf.FloorToInt(kv.Value.x / World.VoxelSize), Mathf.FloorToInt(kv.Value.y / World.VoxelSize - 0.7f), Mathf.FloorToInt(kv.Value.z / World.VoxelSize)) != B.Campfire) continue;
                var l = pool[used++];
                float t = Time.time * 7f + kv.Key % 100;
                l.transform.position = kv.Value + new Vector3(Mathf.PerlinNoise(t, 0) - 0.5f, 0, Mathf.PerlinNoise(0, t) - 0.5f) * 0.06f;
                l.intensity = 160f * (0.8f + Mathf.PerlinNoise(t * 1.7f, 3.1f) * 0.4f);   // candela
                l.enabled = true;
            }
            for (int i = used; i < pool.Count; i++) pool[i].enabled = false;
        }
    }
}
