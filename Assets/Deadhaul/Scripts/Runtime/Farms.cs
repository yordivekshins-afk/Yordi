using System.Collections.Generic;
using Deadhaul.Core;
using UnityEngine;

namespace Deadhaul
{
    /// <summary>Laat zaailingen in de geladen wereld langzaam uitgroeien tot rijpe gewassen.</summary>
    public sealed class Farms : MonoBehaviour
    {
        const float Interval = 3f;
        readonly Dictionary<long, HashSet<long>> seedlings = new Dictionary<long, HashSet<long>>();
        readonly List<(int x, int y, int z, byte b)> grow = new List<(int, int, int, byte)>();
        ChunkManager chunks;
        float timer;
        readonly System.Random rng = new System.Random();
        public System.Func<float> GrowthScale;       // seizoen en regen

        public void Init(ChunkManager c)
        {
            chunks = c;
            chunks.ChunkLoaded += OnChunkLoaded;
            chunks.BlockChanged += OnBlockChanged;
        }

        void OnChunkLoaded(int cx, int cz)
        {
            var pad = chunks.Store.GetPadded(cx, cz);
            if (pad == null) return;
            var set = new HashSet<long>();
            for (int lz = 0; lz < World.ChunkSize; lz++)
                for (int lx = 0; lx < World.ChunkSize; lx++)
                    for (int y = 1; y < World.Height; y++)
                        if (B.IsSeedling(pad[World.IdxPad(lx + 1, y, lz + 1)]))
                            set.Add(VoxelStore.VoxelKey(cx * World.ChunkSize + lx, y, cz * World.ChunkSize + lz));
            seedlings[VoxelStore.Key(cx, cz)] = set;
        }

        void OnBlockChanged(int x, int y, int z, byte b)
        {
            long ck = VoxelStore.Key(VoxelStore.ChunkOf(x), VoxelStore.ChunkOf(z));
            if (!seedlings.TryGetValue(ck, out var set)) return;
            long k = VoxelStore.VoxelKey(x, y, z);
            if (B.IsSeedling(b)) set.Add(k); else set.Remove(k);
        }

        void Update()
        {
            timer -= Time.deltaTime;
            if (timer > 0) return;
            timer = Interval;
            grow.Clear();
            double chance = Farming.GrowChancePerSecond * Interval * (GrowthScale?.Invoke() ?? 1f);
            var remove = new List<long>();
            foreach (var kv in seedlings)
            {
                int cx = (int)(kv.Key >> 32), cz = (int)(kv.Key & 0xFFFFFFFF);
                if (!chunks.Store.IsLoaded(cx, cz)) { remove.Add(kv.Key); continue; }
                foreach (var k in kv.Value)
                {
                    if (rng.NextDouble() > chance) continue;
                    int y = (int)(k & 0xFFFF);
                    int z = (int)((k >> 16) & 0xFFFFFF); if (z >= 0x800000) z -= 0x1000000;
                    int x = (int)((k >> 40) & 0xFFFFFF); if (x >= 0x800000) x -= 0x1000000;
                    byte b = chunks.Store.Get(x, y, z);
                    if (B.IsSeedling(b)) grow.Add((x, y, z, Farming.Grown(b)));
                }
            }
            foreach (var k in remove) seedlings.Remove(k);
            foreach (var (x, y, z, b) in grow) chunks.SetBlock(x, y, z, b);
        }
    }
}
