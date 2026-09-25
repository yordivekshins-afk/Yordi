using System.Collections.Generic;
using Deadhaul.Core;
using UnityEngine;

namespace Deadhaul
{
    /// <summary>Bouwt het voxelmodel van een wapen met zijn attachments op de montagepunten.</summary>
    public static class WeaponView
    {
        static readonly Dictionary<string, (Mesh mesh, Arsenal.Blueprint bp)> cache = new Dictionary<string, (Mesh, Arsenal.Blueprint)>();

        static (Mesh mesh, Arsenal.Blueprint bp) Get(string id)
        {
            if (cache.TryGetValue(id, out var c)) return c;
            var bp = Arsenal.Model(id);
            var g = bp.Rasterize(out int sx, out int sy, out int sz, out int ox, out int oy, out int oz);
            var mesh = VoxelCharacter.ModelMesh(g, sx, sy, sz, Arsenal.ModelUnit, ox, oy, oz, id);
            c = (mesh, bp);
            cache[id] = c;
            return c;
        }

        /// <summary>Losse mesh van een model (bijv. een pijl die ergens in steekt).</summary>
        public static Mesh MeshFor(string id) => Get(id).mesh;

        /// <summary>Maakt het wapen als kind van parent. Geeft het mondingspunt terug (voor vuur en lichtsporen).</summary>
        public static Transform Build(Stack s, Transform parent, out Transform muzzle, out Transform lamp)
        {
            muzzle = null; lamp = null;
            var def = s.Def;
            var root = new GameObject(def.Name).transform;
            root.SetParent(parent, false);
            var (mesh, bp) = Get(def.Id);
            Part(root, mesh, Vector3.zero);
            float u = Arsenal.ModelUnit;
            // monding: voor aan de loop (of aan het eind van de demper)
            Vector3 muzzlePos = bp.Mounts.TryGetValue(AttachSlot.Loop, out var mp) ? new Vector3(mp.x, mp.y, mp.z) * u : new Vector3(0, 0.03f, 0.5f);
            if (s.Mods != null)
                for (int i = 0; i < s.Mods.Length; i++)
                {
                    var id = s.Mods[i];
                    if (id == null || !bp.Mounts.TryGetValue((AttachSlot)i, out var m)) continue;
                    var (am, abp) = Get(id);
                    var at = new Vector3(m.x, m.y, m.z) * u;
                    Part(root, am, at);
                    if ((AttachSlot)i == AttachSlot.Loop)
                    {
                        int maxZ = 0; foreach (var b in abp.Boxes) maxZ = Mathf.Max(maxZ, b.z1);
                        muzzlePos = at + new Vector3(0, 0, (maxZ + 1) * u);
                    }
                    if (Items.Get(id).Flashlight)
                    {
                        lamp = new GameObject("Lamppunt").transform;
                        lamp.SetParent(root, false);
                        lamp.localPosition = at + new Vector3(0.015f, 0, 0.1f);
                    }
                }
            muzzle = new GameObject("Monding").transform;
            muzzle.SetParent(root, false);
            muzzle.localPosition = muzzlePos;
            return root;
        }

        static void Part(Transform root, Mesh mesh, Vector3 pos)
        {
            var go = new GameObject(mesh.name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = pos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = VoxelAssets.VoxelMaterial;
        }

        /// <summary>Vergroting van het vizier (voor het HUD-overlay en het gezichtsveld).</summary>
        public static float Zoom(Stack s) => Arsenal.Stats(s).Zoom;
    }
}
