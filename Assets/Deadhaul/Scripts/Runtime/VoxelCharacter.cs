using Deadhaul.Core;
using UnityEngine;

namespace Deadhaul
{
    /// <summary>
    /// Voxelpersonage opgebouwd uit losse ledematen (romp, hoofd, armen, benen, rugzak)
    /// met procedurele loop-, ren-, sluip- en hakanimatie. Voxels van 6 cm.
    /// </summary>
    public sealed class VoxelCharacter : MonoBehaviour
    {
        public const float U = 0.06f;

        public struct Look
        {
            public byte Skin, Hair, Top, TopDark, Pants, Boots, Extra;
            public bool Backpack, Bandana;

            public static Look Survivor => new Look { Skin = B.Skin, Hair = B.Hair, Top = B.Jacket, TopDark = B.JacketDark, Pants = B.Jeans, Boots = B.Boot, Backpack = true };
            public static Look Raider => new Look { Skin = B.SkinDark, Hair = B.Hair, Top = B.RaiderCoat, TopDark = B.Strap, Pants = B.RaiderPants, Boots = B.Boot, Bandana = true };
        }

        Transform hips, torso, head, armL, armR, legL, legR, tool;
        float phase, swing, crouch, speedSmooth;

        public static VoxelCharacter Build(Transform parent, Look look)
        {
            var root = new GameObject("Personage");
            root.transform.SetParent(parent, false);
            var c = root.AddComponent<VoxelCharacter>();
            c.hips = new GameObject("Heupen").transform;
            c.hips.SetParent(root.transform, false);
            c.hips.localPosition = new Vector3(0, 13 * U, 0);

            // benen: 4 × 13 × 4, pivot bovenaan
            c.legL = Part("Been L", c.hips, new Vector3(-2.2f * U, 0, 0), 4, 13, 4, 2, 13, 2, (x, y, z) => y < 2 ? look.Boots : y < 3 && z == 3 ? look.Boots : look.Pants);
            c.legR = Part("Been R", c.hips, new Vector3(2.2f * U, 0, 0), 4, 13, 4, 2, 13, 2, (x, y, z) => y < 2 ? look.Boots : y < 3 && z == 3 ? look.Boots : look.Pants);
            // romp: 9 × 10 × 5, pivot onderaan midden
            c.torso = Part("Romp", c.hips, Vector3.zero, 9, 10, 5, 4.5f, 0, 2.5f, (x, y, z) =>
            {
                if (y == 0) return B.Strap;                                // riem
                if (y >= 8 && (x == 0 || x == 8)) return look.TopDark;     // schouders
                if (z == 4 && x == 4 && y > 1 && y < 9) return look.TopDark; // rits
                return look.Top;
            });
            // hoofd: 6 × 6 × 6
            c.head = Part("Hoofd", c.torso, new Vector3(0, 10 * U, 0), 6, 7, 6, 3, 0, 3, (x, y, z) =>
            {
                if (y == 0 && (x == 0 || x == 5 || z == 0 || z == 5)) return 0;   // nek
                if (y >= 5 || (z == 0 && y >= 2) || ((x == 0 || x == 5) && y >= 4 && z < 4)) return look.Hair;
                if (z == 5 && y == 3 && (x == 1 || x == 4)) return B.Eye;
                if (look.Bandana && z >= 4 && y <= 2 && y >= 1) return B.Bandana;
                return look.Skin;
            });
            // armen: 3 × 11 × 3, pivot bij de schouder
            c.armL = Part("Arm L", c.torso, new Vector3(-6 * U, 9.5f * U, 0), 3, 11, 3, 1.5f, 11, 1.5f, (x, y, z) => y < 2 ? look.Skin : y > 8 ? look.TopDark : look.Top);
            c.armR = Part("Arm R", c.torso, new Vector3(6 * U, 9.5f * U, 0), 3, 11, 3, 1.5f, 11, 1.5f, (x, y, z) => y < 2 ? look.Skin : y > 8 ? look.TopDark : look.Top);
            if (look.Backpack)
                Part("Rugzak", c.torso, new Vector3(0, 2 * U, -2.5f * U), 7, 7, 3, 3.5f, 0, 3, (x, y, z) => (y == 6 && z == 2) || x == 0 && z == 2 ? B.Strap : B.Backpack);
            return c;
        }

        delegate byte ColorFn(int x, int y, int z);

        static Transform Part(string name, Transform parent, Vector3 local, int sx, int sy, int sz, float px, float py, float pz, ColorFn fn)
        {
            var grid = new byte[sx * sy * sz];
            for (int z = 0; z < sz; z++)
                for (int y = 0; y < sy; y++)
                    for (int x = 0; x < sx; x++)
                        grid[x + sx * (y + sy * z)] = fn(x, y, z);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.AddComponent<MeshFilter>().sharedMesh = ModelMesh(grid, sx, sy, sz, U, px, py, pz, name);
            go.AddComponent<MeshRenderer>().sharedMaterial = VoxelAssets.VoxelMaterial;
            return go.transform;
        }

        public static Mesh ModelMesh(byte[] grid, int sx, int sy, int sz, float scale, float px, float py, float pz, string name)
        {
            var m = new MeshData();
            ChunkMesher.BuildModel(grid, sx, sy, sz, scale, px, py, pz, m);
            int n = m.VertexCount;
            var pos = new Vector3[n]; var nrm = new Vector3[n]; var uv = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                pos[i] = new Vector3(m.Positions[i * 3], m.Positions[i * 3 + 1], m.Positions[i * 3 + 2]);
                nrm[i] = new Vector3(m.Normals[i * 3], m.Normals[i * 3 + 1], m.Normals[i * 3 + 2]);
                uv[i] = new Vector2(m.Uvs[i * 2], m.Uvs[i * 2 + 1]);
            }
            var mesh = new Mesh { name = name, vertices = pos, normals = nrm, uv = uv };
            mesh.SetTriangles(m.Opaque, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Zet een voorwerp in de rechterhand (of niets).</summary>
        public void SetTool(ItemDef def)
        {
            if (tool) Destroy(tool.gameObject);
            tool = null;
            if (def == null || (def.Kind != ItemKind.Tool && def.Kind != ItemKind.Weapon)) return;
            byte[] g; int sx, sy, sz;
            switch (def.Id)
            {
                case "bijl":
                    sx = 2; sy = 12; sz = 5; g = new byte[sx * sy * sz];
                    for (int y = 0; y < sy; y++) for (int x = 0; x < sx; x++) g[x + sx * (y + sy * 1)] = B.Wood;
                    for (int y = 8; y < 12; y++) for (int z = 2; z < 5; z++) for (int x = 0; x < sx; x++) g[x + sx * (y + sy * z)] = B.Blade;
                    break;
                case "pistool":
                    sx = 2; sy = 4; sz = 6; g = new byte[sx * sy * sz];
                    for (int z = 0; z < sz; z++) for (int x = 0; x < sx; x++) { g[x + sx * (3 + sy * z)] = B.Gunmetal; g[x + sx * (2 + sy * z)] = B.Gunmetal; }
                    for (int y = 0; y < 2; y++) for (int x = 0; x < sx; x++) g[x + sx * (y + sy * 1)] = B.Wood;
                    break;
                case "geweer":
                    sx = 2; sy = 4; sz = 18; g = new byte[sx * sy * sz];
                    for (int z = 0; z < sz; z++) for (int x = 0; x < sx; x++) g[x + sx * (2 + sy * z)] = z < 7 ? B.Wood : B.Gunmetal;
                    for (int z = 0; z < 5; z++) for (int x = 0; x < sx; x++) g[x + sx * (1 + sy * z)] = B.Wood;
                    break;
                default: // pijp, breekijzer
                    sx = 2; sy = 14; sz = 2; g = new byte[sx * sy * sz];
                    byte c = def.Id == "breekijzer" ? B.Rust : B.Gunmetal;
                    for (int y = 0; y < sy; y++) for (int z = 0; z < sz; z++) for (int x = 0; x < sx; x++) g[x + sx * (y + sy * z)] = c;
                    break;
            }
            var go = new GameObject(def.Name);
            go.transform.SetParent(armR, false);
            bool gun = def.Kind == ItemKind.Weapon && def.GunDamage > 0;
            go.transform.localPosition = new Vector3(0, -10.5f * U, gun ? 0.5f * U : 0);
            go.transform.localRotation = gun ? Quaternion.Euler(90, 0, 0) : Quaternion.Euler(-70, 0, 0);
            go.AddComponent<MeshFilter>().sharedMesh = ModelMesh(g, sx, sy, sz, U * 0.8f, sx / 2f, 1, sz / 2f, def.Name);
            go.AddComponent<MeshRenderer>().sharedMaterial = VoxelAssets.VoxelMaterial;
            tool = go.transform;
        }

        /// <summary>Animeert op basis van horizontale snelheid (m/s), hurken en een hak/slag-signaal.</summary>
        public void Animate(float speed, bool crouching, bool grounded, bool aiming, float attack, float dt)
        {
            speedSmooth = Mathf.Lerp(speedSmooth, speed, 1 - Mathf.Exp(-dt * 10));
            phase += dt * Mathf.Lerp(4f, 9.5f, Mathf.InverseLerp(1.5f, 7f, speedSmooth)) * (speedSmooth > 0.2f ? 1 : 0);
            swing = Mathf.Lerp(swing, Mathf.Clamp01(speedSmooth / 4f), 1 - Mathf.Exp(-dt * 8));
            crouch = Mathf.Lerp(crouch, crouching ? 1 : 0, 1 - Mathf.Exp(-dt * 10));
            float s = Mathf.Sin(phase) * 38f * swing;
            float air = grounded ? 0 : 1;
            legL.localRotation = Quaternion.Euler(s - crouch * 35 - air * 20, 0, 0);
            legR.localRotation = Quaternion.Euler(-s - crouch * 35 + air * 25, 0, 0);
            hips.localPosition = new Vector3(0, (13 - crouch * 2.5f) * U + Mathf.Abs(Mathf.Cos(phase)) * 0.03f * swing, 0);
            torso.localRotation = Quaternion.Euler(crouch * 18 + swing * 6, 0, 0);
            armL.localRotation = Quaternion.Euler(-s * 0.8f, 0, -4);
            if (aiming) armR.localRotation = Quaternion.Euler(-85, 0, 0);
            else if (attack > 0) armR.localRotation = Quaternion.Euler(Mathf.Lerp(40, -120, attack), 0, 0);
            else armR.localRotation = Quaternion.Euler(s * 0.8f - (tool ? 15 : 0), 0, 4);
            head.localRotation = Quaternion.Euler(-crouch * 10, 0, 0);
        }

        public void SetVisible(bool v)
        {
            foreach (var r in GetComponentsInChildren<Renderer>()) r.shadowCastingMode = v ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        }
    }
}
