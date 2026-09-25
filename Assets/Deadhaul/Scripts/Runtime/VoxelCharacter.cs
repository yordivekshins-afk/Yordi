using Deadhaul.Core;
using UnityEngine;

namespace Deadhaul
{
    /// <summary>
    /// Voxelpersonage uit losse ledematen met procedurele animatie. Het uiterlijk komt uit de
    /// gedragen uitrusting (schoenen, broek, jas, vest, rugzak, helm, masker) of uit een mutantvariant.
    /// Voxels van 6 cm.
    /// </summary>
    public sealed class VoxelCharacter : MonoBehaviour
    {
        public const float U = 0.06f;

        public struct Look
        {
            public byte Skin, Hair, Top, TopDark, Pants, PantsDark, Shoes, Sole;
            public int TopStyle, PantsStyle;          // 1 = camo, 3 = hazmat
            public int VestStyle, BackStyle, HeadStyle, FaceStyle;   // -1 = geen
            public byte VestColor, VestColor2, BackColor, BackColor2, HeadColor, FaceColor;
            public bool GlowEyes, Torn;
            public float Scale;

            public static Look Naked(byte skin) => new Look
            {
                Skin = skin, Hair = B.Hair, Top = skin, TopDark = skin, Pants = B.Jeans, PantsDark = B.Jeans, Shoes = skin, Sole = skin,
                VestStyle = -1, BackStyle = -1, HeadStyle = -1, FaceStyle = -1, Scale = 1
            };

            public static Look FromEquipment(Equipment eq, byte skin, byte hair)
            {
                var l = Naked(skin); l.Hair = hair;
                var feet = eq[EquipSlot.Voeten]; if (feet != null) { l.Shoes = feet.Color1; l.Sole = feet.Color2; }
                var legs = eq[EquipSlot.Benen]; if (legs != null) { l.Pants = legs.Color1; l.PantsDark = legs.Color2; l.PantsStyle = legs.Style; } else { l.Pants = B.Jeans; l.PantsDark = B.Jeans; }
                var top = eq[EquipSlot.Torso]; if (top != null) { l.Top = top.Color1; l.TopDark = top.Color2; l.TopStyle = top.Style; }
                var vest = eq[EquipSlot.Vest]; if (vest != null) { l.VestStyle = vest.Style; l.VestColor = vest.Color1; l.VestColor2 = vest.Color2; }
                var back = eq[EquipSlot.Rug]; if (back != null) { l.BackStyle = back.Style; l.BackColor = back.Color1; l.BackColor2 = back.Color2; }
                var head = eq[EquipSlot.Hoofd]; if (head != null) { l.HeadStyle = head.Style; l.HeadColor = head.Color1; }
                var face = eq[EquipSlot.Gezicht]; if (face != null) { l.FaceStyle = face.Style; l.FaceColor = face.Color1; }
                return l;
            }

            public static Look Ghoul => new Look
            {
                Skin = B.MutantSkin, Hair = B.MutantSkin, Top = B.RaiderCoat, TopDark = B.MutantFlesh, Pants = B.RaiderPants, PantsDark = B.MutantFlesh,
                Shoes = B.MutantSkin, Sole = B.MutantSkin, VestStyle = -1, BackStyle = -1, HeadStyle = -1, FaceStyle = -1, GlowEyes = true, Torn = true, Scale = 0.97f
            };

            public static Look Brute => new Look
            {
                Skin = B.MutantFlesh, Hair = B.MutantSkin, Top = B.MutantFlesh, TopDark = B.MutantSkin, Pants = B.RaiderPants, PantsDark = B.MutantFlesh,
                Shoes = B.MutantFlesh, Sole = B.MutantSkin, VestStyle = -1, BackStyle = -1, HeadStyle = -1, FaceStyle = -1, GlowEyes = true, Torn = true, Scale = 1.5f
            };
        }

        Transform hips, torso, head, armL, armR, legL, legR, tool, crate;
        float phase, swing, crouch, speedSmooth, deathT, lieT, workT;
        bool dead;
        public Transform Hand => armR;

        // houdingen voor bewoners
        public bool Sleeping, Working, Sitting, Carrying;

        public static VoxelCharacter Build(Transform parent, Look look)
        {
            var root = new GameObject("Personage");
            root.transform.SetParent(parent, false);
            var c = root.AddComponent<VoxelCharacter>();
            c.Rebuild(look);
            return c;
        }

        public void Rebuild(Look look)
        {
            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
            tool = null;
            transform.localScale = Vector3.one * (look.Scale <= 0 ? 1 : look.Scale);
            uint seed = 1;
            bool Holes(int x, int y, int z) { seed = seed * 1103515245 + 12345; return look.Torn && ((seed >> 16) & 7) == 0; }
            byte Pattern(byte a, byte b, int style, int x, int y, int z) => style == 1 ? (((x * 7 + y * 3 + z * 5) / 3) % 3 == 0 ? b : a) : a;

            hips = new GameObject("Heupen").transform;
            hips.SetParent(transform, false);
            hips.localPosition = new Vector3(0, 13 * U, 0);

            byte Leg(int x, int y, int z)
            {
                if (y == 0) return look.Sole;
                if (y < 3 && !(y == 2 && z < 2)) return look.Shoes;
                if (look.PantsStyle != 1 && look.Pants == B.Khaki && y == 7 && (x == 0 || x == 3)) return look.PantsDark;   // cargozak
                if (Holes(x, y, z)) return look.Skin;
                return Pattern(look.Pants, look.PantsDark, look.PantsStyle, x, y, z);
            }
            legL = Part("Been L", hips, new Vector3(-2.2f * U, 0, 0), 4, 13, 4, 2, 13, 2, Leg);
            legR = Part("Been R", hips, new Vector3(2.2f * U, 0, 0), 4, 13, 4, 2, 13, 2, Leg);

            torso = Part("Romp", hips, Vector3.zero, 9, 10, 5, 4.5f, 0, 2.5f, (x, y, z) =>
            {
                if (y == 0) return look.TopStyle == 3 ? look.TopDark : B.Strap;
                if (Holes(x, y, z)) return look.Skin;
                if (y >= 8 && (x == 0 || x == 8)) return look.TopDark;
                if (look.TopStyle == 0 && z == 4 && x == 4 && y > 1 && y < 9) return look.TopDark;
                return Pattern(look.Top, look.TopDark, look.TopStyle, x, y, z);
            });

            head = Part("Hoofd", torso, new Vector3(0, 10 * U, 0), 6, 7, 6, 3, 0, 3, (x, y, z) =>
            {
                if (y == 0 && (x == 0 || x == 5 || z == 0 || z == 5)) return 0;
                if (look.TopStyle == 3) return y >= 2 && z == 5 && y <= 4 && x >= 1 && x <= 4 ? B.Lens : look.Top;   // hazmatkap
                bool hair = y >= 5 || (z == 0 && y >= 2) || ((x == 0 || x == 5) && y >= 4 && z < 4);
                if (hair && look.HeadStyle < 0) return look.Hair;
                if (z == 5 && y == 3 && (x == 1 || x == 4)) return look.GlowEyes ? B.Glow : B.Eye;
                if (hair) return look.Hair;
                return look.Skin;
            });
            if (look.HeadStyle >= 0 && look.TopStyle != 3) HeadGear(look);
            if (look.FaceStyle >= 0 && look.TopStyle != 3) FaceGear(look);

            byte Arm(int x, int y, int z) => y < 2 ? (look.TopStyle == 3 ? look.TopDark : look.Skin) : y > 8 ? look.TopDark : Holes(x, y, z) ? look.Skin : Pattern(look.Top, look.TopDark, look.TopStyle, x, y, z);
            armL = Part("Arm L", torso, new Vector3(-6 * U, 9.5f * U, 0), 3, 11, 3, 1.5f, 11, 1.5f, Arm);
            armR = Part("Arm R", torso, new Vector3(6 * U, 9.5f * U, 0), 3, 11, 3, 1.5f, 11, 1.5f, Arm);

            if (look.VestStyle >= 0) Vest(look);
            if (look.BackStyle >= 0) Back(look);
            crate = Part("Kist", torso, new Vector3(0, 4 * U, 5 * U), 7, 5, 5, 3.5f, 0, 0, (x, y, z) => y == 4 || x == 0 || x == 6 ? B.Wood : B.Planks);
            crate.gameObject.SetActive(false);
        }

        void HeadGear(Look l)
        {
            switch (l.HeadStyle)
            {
                case 0: // muts
                    Part("Muts", head, new Vector3(0, 5 * U, 0), 7, 3, 7, 3.5f, 0, 3.5f, (x, y, z) => y == 2 && (x == 0 || x == 6 || z == 0 || z == 6) ? (byte)0 : l.HeadColor);
                    break;
                case 1: // pet
                    Part("Pet", head, new Vector3(0, 5 * U, 0), 7, 2, 10, 3.5f, 0, 3.5f, (x, y, z) => z >= 7 ? (y == 0 ? l.HeadColor : (byte)0) : z < 7 ? l.HeadColor : (byte)0);
                    break;
                case 2: // bouwhelm
                    Part("Bouwhelm", head, new Vector3(0, 4.5f * U, 0), 9, 3, 9, 4.5f, 0, 4.5f, (x, y, z) => y == 0 ? l.HeadColor : (x == 0 || x == 8 || z == 0 || z == 8) ? (byte)0 : l.HeadColor);
                    break;
                default: // gevechtshelm
                    Part("Helm", head, new Vector3(0, 3.5f * U, 0), 8, 5, 8, 4, 0, 4, (x, y, z) =>
                    {
                        if (y == 4 && (x == 0 || x == 7 || z == 0 || z == 7)) return 0;
                        if (y < 2 && z >= 6 && x >= 1 && x <= 6) return 0;   // open gezicht
                        if (y == 3 && z == 7 && x >= 3 && x <= 4) return B.Polymer;   // NVG-mount
                        return (x + z) % 5 == 0 ? B.OD : l.HeadColor;
                    });
                    break;
            }
        }

        void FaceGear(Look l)
        {
            if (l.FaceStyle == 0)
                Part("Bandana", head, new Vector3(0, 0.5f * U, 3.2f * U), 7, 3, 2, 3.5f, 0, 1, (x, y, z) => z == 0 && (x == 0 || x == 6) ? (byte)0 : l.FaceColor);
            else
                Part("Gasmasker", head, new Vector3(0, 0, 3 * U), 6, 5, 3, 3, 0, 0, (x, y, z) =>
                {
                    if (z == 2 && y == 3 && (x == 1 || x == 4)) return B.Lens;
                    if (z == 2 && y <= 1 && (x == 2 || x == 3)) return B.Steel;   // filter
                    return B.Rubber;
                });
        }

        void Vest(Look l)
        {
            switch (l.VestStyle)
            {
                case 0: // chest rig: magazijnvakken voorop
                    Part("Chest rig", torso, new Vector3(0, 2 * U, 2.5f * U), 9, 5, 2, 4.5f, 0, 0, (x, y, z) => z == 1 && x % 3 == 1 && y == 4 ? l.VestColor2 : (x == 0 || x == 8) && y > 2 ? (byte)0 : l.VestColor);
                    Part("Riemen", torso, new Vector3(0, 6 * U, -2.5f * U), 9, 4, 1, 4.5f, 0, 1, (x, y, z) => x == 1 || x == 7 ? B.Strap : (byte)0);
                    break;
                case 1: // kogelwerend vest
                    Part("Vest", torso, new Vector3(0, 1 * U, 0), 11, 8, 7, 5.5f, 0, 3.5f, (x, y, z) => (x > 0 && x < 10 && z > 0 && z < 6) ? (byte)0 : l.VestColor);
                    break;
                default: // plate carrier met platen en vakken
                    Part("Plate carrier", torso, new Vector3(0, 1 * U, 0), 11, 8, 8, 5.5f, 0, 4, (x, y, z) =>
                    {
                        if (x > 0 && x < 10 && z > 0 && z < 7) return 0;
                        if (z == 7 && y <= 3 && x % 3 == 1) return l.VestColor2;
                        return l.VestColor;
                    });
                    break;
            }
        }

        void Back(Look l)
        {
            switch (l.BackStyle)
            {
                case 0: Part("Schoudertas", torso, new Vector3(-4 * U, 1 * U, -2.8f * U), 4, 5, 2, 2, 0, 2, (x, y, z) => y == 4 ? l.BackColor2 : l.BackColor); break;
                case 1: Part("Rugzak", torso, new Vector3(0, 2 * U, -2.5f * U), 7, 7, 3, 3.5f, 0, 3, (x, y, z) => (y == 6 && z == 2) || x == 0 && z == 2 ? l.BackColor2 : l.BackColor); break;
                default:
                    Part("Legerrugzak", torso, new Vector3(0, 0, -2.5f * U), 9, 11, 4, 4.5f, 0, 4, (x, y, z) =>
                    {
                        if (y >= 9 && (x == 0 || x == 8)) return B.Khaki;   // slaapmat
                        if (z == 0 && y % 4 == 1) return l.BackColor2;
                        return ((x + y) / 2) % 3 == 0 ? l.BackColor2 : l.BackColor;
                    });
                    break;
            }
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
            var tris = new System.Collections.Generic.List<int>(m.Opaque);
            tris.AddRange(m.Glass);
            var mesh = new Mesh { name = name, vertices = pos, normals = nrm, uv = uv };
            if (n > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Zet een voorwerp in de rechterhand. Geeft de monding terug voor vuurwapens.</summary>
        public Transform SetTool(Stack s, out Transform lamp)
        {
            lamp = null;
            if (tool) Destroy(tool.gameObject);
            tool = null;
            var def = s.Def;
            if (def == null || (def.Kind != ItemKind.Tool && def.Kind != ItemKind.Weapon)) return null;
            if (def.GunDamage > 0)
            {
                // wapen in de hand; schaal compenseert de lichaamsschaal niet (personages zijn 1:1)
                tool = WeaponView.Build(s, armR, out var muzzle, out lamp);
                tool.localPosition = new Vector3(0, -10.5f * U, 0.5f * U);
                tool.localRotation = Quaternion.Euler(90, 0, 0);
                return muzzle;
            }
            byte[] g; int sx, sy, sz;
            switch (def.Id)
            {
                case "bijl":
                    sx = 2; sy = 12; sz = 5; g = new byte[sx * sy * sz];
                    for (int y = 0; y < sy; y++) for (int x = 0; x < sx; x++) g[x + sx * (y + sy * 1)] = B.Wood;
                    for (int y = 8; y < 12; y++) for (int z = 2; z < 5; z++) for (int x = 0; x < sx; x++) g[x + sx * (y + sy * z)] = B.Blade;
                    break;
                case "mes":
                case "machete":
                {
                    int blade = def.Id == "mes" ? 6 : 12;
                    sx = 1; sy = 4 + blade + 1; sz = def.Id == "mes" ? 2 : 3; g = new byte[sx * sy * sz];
                    for (int y = 0; y < 4; y++) for (int z = 0; z < sz; z++) g[sx * (y + sy * z)] = def.Id == "mes" ? B.Polymer : B.Leather;   // heft
                    for (int z = 0; z < sz; z++) g[sx * (4 + sy * z)] = B.Steel;                                                            // stootplaat
                    for (int y = 5; y < sy; y++) for (int z = 0; z < sz; z++) if (!(y == sy - 1 && z == 0)) g[sx * (y + sy * z)] = B.Blade;   // lemmet met punt
                    break;
                }
                default: // pijp, breekijzer
                    sx = 2; sy = 14; sz = 2; g = new byte[sx * sy * sz];
                    byte c = def.Id == "breekijzer" ? B.Rust : B.Gunmetal;
                    for (int y = 0; y < sy; y++) for (int z = 0; z < sz; z++) for (int x = 0; x < sx; x++) g[x + sx * (y + sy * z)] = c;
                    break;
            }
            var go = new GameObject(def.Name);
            go.transform.SetParent(armR, false);
            go.transform.localPosition = new Vector3(0, -10.5f * U, 0);
            go.transform.localRotation = Quaternion.Euler(-70, 0, 0);
            go.AddComponent<MeshFilter>().sharedMesh = ModelMesh(g, sx, sy, sz, U * 0.8f, sx / 2f, 1, sz / 2f, def.Name);
            go.AddComponent<MeshRenderer>().sharedMaterial = VoxelAssets.VoxelMaterial;
            tool = go.transform;
            return null;
        }

        /// <summary>Animeert op basis van horizontale snelheid (m/s), hurken, richten en een slag-signaal.</summary>
        public void Animate(float speed, bool crouching, bool grounded, bool aiming, float attack, float dt, float pitch = 0)
        {
            if (dead)
            {
                deathT = Mathf.Min(1, deathT + dt * 2.5f);
                transform.localRotation = Quaternion.Euler(-88 * deathT * deathT, 0, 0);
                return;
            }
            // liggen in bed
            lieT = Mathf.MoveTowards(lieT, Sleeping ? 1 : 0, dt * 1.5f);
            transform.localRotation = Quaternion.Euler(-88 * lieT, 0, 0);
            transform.localPosition = new Vector3(0, lieT * 0.5f, lieT * -0.9f);
            if (crate) crate.gameObject.SetActive(Carrying && !Sleeping);
            speedSmooth = Mathf.Lerp(speedSmooth, speed, 1 - Mathf.Exp(-dt * 10));
            phase += dt * Mathf.Lerp(4f, 9.5f, Mathf.InverseLerp(1.5f, 7f, speedSmooth)) * (speedSmooth > 0.2f ? 1 : 0);
            swing = Mathf.Lerp(swing, Mathf.Clamp01(speedSmooth / 4f), 1 - Mathf.Exp(-dt * 8));
            crouch = Mathf.Lerp(crouch, crouching || (Working && !Sitting) ? 1 : Sitting ? 1.9f : 0, 1 - Mathf.Exp(-dt * 10));
            workT += dt * (Working ? 6f : 0f);
            float s = Mathf.Sin(phase) * 38f * swing;
            float air = grounded ? 0 : 1;
            legL.localRotation = Quaternion.Euler(s - crouch * 35 - air * 20, 0, 0);
            legR.localRotation = Quaternion.Euler(-s - crouch * 35 + air * 25, 0, 0);
            hips.localPosition = new Vector3(0, (13 - crouch * 2.5f) * U + Mathf.Abs(Mathf.Cos(phase)) * 0.03f * swing, 0);
            torso.localRotation = Quaternion.Euler(crouch * 18 + swing * 6, 0, 0);
            if (Sleeping) { armL.localRotation = Quaternion.identity; armR.localRotation = Quaternion.identity; legL.localRotation = Quaternion.identity; legR.localRotation = Quaternion.identity; return; }
            if (Carrying)
            {
                armR.localRotation = Quaternion.Euler(-70, 0, 0);
                armL.localRotation = Quaternion.Euler(-70, 0, 0);
            }
            else if (Working)
            {
                float w = Mathf.Sin(workT) * 25f;
                armR.localRotation = Quaternion.Euler(-45 + w, 0, 0);
                armL.localRotation = Quaternion.Euler(-40 - w, 0, 0);
            }
            else if (aiming)
            {
                armR.localRotation = Quaternion.Euler(-90 + pitch, 0, 0);
                armL.localRotation = Quaternion.Euler(-80 + pitch, 25, 0);
            }
            else
            {
                armL.localRotation = Quaternion.Euler(-s * 0.8f, 0, -4);
                if (attack > 0) armR.localRotation = Quaternion.Euler(Mathf.Lerp(40, -120, attack), 0, 0);
                else armR.localRotation = Quaternion.Euler(s * 0.8f - (tool ? 15 : 0), 0, 4);
            }
            head.localRotation = Quaternion.Euler(-crouch * 10 + (aiming ? pitch * 0.5f : 0) + (Working ? 20 : 0), 0, 0);
            if (Sitting)
            {
                legL.localRotation = Quaternion.Euler(-85, 0, 0);
                legR.localRotation = Quaternion.Euler(-85, 0, 0);
                hips.localPosition = new Vector3(0, 7 * U, 0);
                torso.localRotation = Quaternion.Euler(5, 0, 0);
            }
        }

        public void Die() { dead = true; }

        public void SetVisible(bool v)
        {
            foreach (var r in GetComponentsInChildren<Renderer>()) r.shadowCastingMode = v ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        }
    }

    /// <summary>Viervoeters: mutantwolf en hert.</summary>
    public sealed class VoxelAnimal : MonoBehaviour
    {
        const float U = 0.06f;
        Transform body, head, fl, fr, bl, br, tail;
        float phase, deathT; bool dead;

        public static VoxelAnimal Build(Transform parent, NpcType type)
        {
            var root = new GameObject(type.ToString());
            root.transform.SetParent(parent, false);
            var a = root.AddComponent<VoxelAnimal>();
            bool deer = type == NpcType.Hert;
            byte fur = deer ? B.Fur : B.FurDark, fur2 = deer ? B.Khaki : B.MutantFlesh;
            int legH = deer ? 11 : 7, bodyL = deer ? 14 : 13, bodyH = deer ? 6 : 6;
            a.body = Part("Lijf", root.transform, new Vector3(0, legH * U, 0), 6, bodyH, bodyL, 3, 0, bodyL / 2f, (x, y, z) =>
                !deer && y == bodyH - 1 && z % 3 == 0 && (x == 2 || x == 3) ? B.Bone : (y == 0 ? fur2 : fur));
            a.head = Part("Kop", a.body, new Vector3(0, (bodyH - 1) * U, bodyL / 2f * U), 4, 5, 7, 2, 1, 0, (x, y, z) =>
            {
                if (z >= 4 && y >= 3) return 0;
                if (z == 3 && y == 3 && (x == 0 || x == 3)) return deer ? B.Eye : B.Glow;
                if (!deer && z >= 5 && y == 0) return B.Bone;   // tanden
                return fur;
            });
            if (deer)
            {
                Part("Gewei L", a.head, new Vector3(-1.2f * U, 4 * U, 1 * U), 1, 5, 1, 0.5f, 0, 0.5f, (x, y, z) => B.Bone);
                Part("Gewei R", a.head, new Vector3(1.2f * U, 4 * U, 1 * U), 1, 5, 1, 0.5f, 0, 0.5f, (x, y, z) => B.Bone);
            }
            ColorFn leg = (x, y, z) => y < 2 ? B.FurDark : fur;
            float lz = bodyL / 2f - 2;
            a.fl = Part("Poot LV", root.transform, new Vector3(-1.8f * U, legH * U, lz * U), 2, legH, 2, 1, legH, 1, leg);
            a.fr = Part("Poot RV", root.transform, new Vector3(1.8f * U, legH * U, lz * U), 2, legH, 2, 1, legH, 1, leg);
            a.bl = Part("Poot LA", root.transform, new Vector3(-1.8f * U, legH * U, -lz * U), 2, legH, 2, 1, legH, 1, leg);
            a.br = Part("Poot RA", root.transform, new Vector3(1.8f * U, legH * U, -lz * U), 2, legH, 2, 1, legH, 1, leg);
            a.tail = Part("Staart", a.body, new Vector3(0, (bodyH - 2) * U, -bodyL / 2f * U), 2, 2, deer ? 2 : 6, 1, 1, deer ? 2 : 6, (x, y, z) => fur);
            return a;
        }

        delegate byte ColorFn(int x, int y, int z);

        static Transform Part(string name, Transform parent, Vector3 local, int sx, int sy, int sz, float px, float py, float pz, ColorFn fn)
        {
            var grid = new byte[sx * sy * sz];
            for (int z = 0; z < sz; z++) for (int y = 0; y < sy; y++) for (int x = 0; x < sx; x++) grid[x + sx * (y + sy * z)] = fn(x, y, z);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.AddComponent<MeshFilter>().sharedMesh = VoxelCharacter.ModelMesh(grid, sx, sy, sz, U, px, py, pz, name);
            go.AddComponent<MeshRenderer>().sharedMaterial = VoxelAssets.VoxelMaterial;
            return go.transform;
        }

        public void Animate(float speed, float dt, bool attacking)
        {
            if (dead)
            {
                deathT = Mathf.Min(1, deathT + dt * 2.5f);
                transform.localRotation = Quaternion.Euler(0, 0, 85 * deathT * deathT);
                return;
            }
            phase += dt * Mathf.Lerp(3f, 14f, Mathf.Clamp01(speed / 8f)) * (speed > 0.2f ? 1 : 0);
            float a = Mathf.Sin(phase) * Mathf.Clamp01(speed / 3f) * 40f;
            fl.localRotation = Quaternion.Euler(a, 0, 0); br.localRotation = Quaternion.Euler(a, 0, 0);
            fr.localRotation = Quaternion.Euler(-a, 0, 0); bl.localRotation = Quaternion.Euler(-a, 0, 0);
            head.localRotation = Quaternion.Euler(attacking ? 25 : Mathf.Sin(phase * 0.5f) * 4, 0, 0);
            tail.localRotation = Quaternion.Euler(-20 + Mathf.Sin(Time.time * 6) * 8, Mathf.Sin(Time.time * 3) * 15, 0);
        }

        public void Die() { dead = true; }
    }
}
