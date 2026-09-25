using System;
using System.Collections.Generic;

namespace Deadhaul.Core
{
    /// <summary>
    /// Explosies slaan een bol uit de voxelwereld (zachte materialen verder dan harde), doen schade
    /// met afname over afstand en laten andere explosieve vaten meeknallen.
    /// </summary>
    public static class Explosion
    {
        public struct Result
        {
            public List<(int x, int y, int z, byte b)> Removed;
            public List<V3> Chain;        // explosieve vaten die meegaan
        }

        public static Result Carve(VoxelStore store, V3 center, float radius, float power)
        {
            var res = new Result { Removed = new List<(int, int, int, byte)>(), Chain = new List<V3>() };
            float vs = World.VoxelSize;
            int r = (int)MathF.Ceiling(radius / vs);
            int cx = (int)MathF.Floor(center.X / vs), cy = (int)MathF.Floor(center.Y / vs), cz = (int)MathF.Floor(center.Z / vs);
            for (int dy = -r; dy <= r; dy++)
                for (int dz = -r; dz <= r; dz++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        float d = MathF.Sqrt(dx * dx + dy * dy + dz * dz) * vs;
                        if (d > radius) continue;
                        int x = cx + dx, y = cy + dy, z = cz + dz;
                        byte b = store.Get(x, y, z);
                        if (b == B.Air || b == B.Water) continue;
                        var info = Blocks.Info[b];
                        if ((info.Flags & BlockFlags.Unbreakable) != 0) continue;
                        if (b == B.ExplosiveBarrel && d > 0.3f) { res.Chain.Add(new V3((x + 0.5f) * vs, (y + 0.5f) * vs, (z + 0.5f) * vs)); res.Removed.Add((x, y, z, b)); continue; }
                        // kracht neemt af met afstand; harde materialen houden meer tegen
                        float force = power * (1 - d / radius) * 4f + (Hash.H3(x, y, z, 77) - 0.5f) * 0.6f;
                        if (force > info.Hardness || B.IsPlant(b)) res.Removed.Add((x, y, z, b));
                    }
            return res;
        }

        /// <summary>Schade op afstand d (meter) van het midden.</summary>
        public static float Damage(float d, float radius, float maxDamage)
        {
            float reach = radius * 2.2f;
            if (d >= reach) return 0;
            float t = 1 - d / reach;
            return maxDamage * t * t;
        }
    }
}
