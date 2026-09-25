using System;
using System.Collections.Generic;
using Deadhaul.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.HighDefinition;

namespace Deadhaul
{
    /// <summary>
    /// De overlever: beweging met voxelbotsing, third/first-person camera, slopen, bouwen,
    /// looten, eten/drinken en de zaklamp. Invoer via het Input System.
    /// </summary>
    public sealed class PlayerController : MonoBehaviour
    {
        public const float HalfWidth = 0.3f, Height = 1.75f, CrouchHeight = 1.3f;
        const float Gravity = 22f, JumpSpeed = 7.4f, Reach = 5f;

        public GameState Game;
        public Camera Cam;
        public V3 Pos, Vel;
        public float Yaw, Pitch = 12f;
        public bool FirstPerson, Crouching, Grounded, InWater, NearFire;
        public int Selected;
        public RayHit Target;
        public float MineProgress;
        public bool Flashlight;
        public float FlashBattery = 1f;

        VoxelCharacter body;
        Transform highlight;
        Light flash;
        float attackAnim, nearFireTimer, useCooldown, camDist = 3.4f, headBob;
        int mineX = int.MinValue, mineY, mineZ;
        float fallSpeed;
        readonly System.Random rng = new System.Random();

        ChunkManager Chunks => Game.Chunks;
        VoxelStore Store => Game.Chunks.Store;
        Survival Stats => Game.Stats;
        Inventory Inv => Game.Inventory;

        public void Init(GameState game, Camera cam)
        {
            Game = game; Cam = cam;
            body = VoxelCharacter.Build(transform, VoxelCharacter.Look.Survivor);
            var hl = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(hl.GetComponent<Collider>());
            hl.name = "Doelblok";
            hl.GetComponent<MeshRenderer>().sharedMaterial = VoxelAssets.HighlightMaterial;
            hl.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            hl.transform.localScale = Vector3.one * (World.VoxelSize * 1.04f);
            highlight = hl.transform;
            highlight.gameObject.SetActive(false);

            var fl = new GameObject("Zaklamp");
            fl.transform.SetParent(cam.transform, false);
            fl.transform.localPosition = new Vector3(0.25f, -0.2f, 0.2f);
            var hd = fl.AddHDLight(LightType.Spot);
            flash = fl.GetComponent<Light>();
            flash.spotAngle = 58f;
            flash.innerSpotAngle = 22f;
            flash.range = 45f;
            flash.intensity = 900f;       // candela
            flash.color = new Color(1f, 0.93f, 0.82f);
            flash.shadows = LightShadows.Soft;
            hd.SetShadowResolution(1024);
            flash.enabled = false;
            RefreshTool();
        }

        public void Teleport(V3 p) { Pos = p; Vel = new V3(0, 0, 0); fallSpeed = 0; }

        public ItemDef SelectedDef => Inv.Slots[Selected].Def;

        public void RefreshTool() => body.SetTool(SelectedDef);

        void Update()
        {
            if (Game == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            var kb = Keyboard.current; var mouse = Mouse.current;
            bool ui = Game.Hud.CapturesInput;
            Cursor.lockState = ui ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = ui;
            bool alive = !Stats.Dead;
            bool worldReady = Chunks.IsLoaded(new Vector3(Pos.X, 0, Pos.Z));

            // ------------------------------------------------ kijken
            if (!ui && alive && mouse != null)
            {
                var d = mouse.delta.ReadValue() * Game.Settings.MouseSensitivity;
                Yaw += d.x; Pitch = Mathf.Clamp(Pitch - d.y, -80f, 85f);
            }

            // ------------------------------------------------ bewegen
            Vector2 wish = Vector2.zero;
            bool sprint = false, jump = false;
            if (!ui && alive && kb != null)
            {
                if (kb.wKey.isPressed) wish.y += 1; if (kb.sKey.isPressed) wish.y -= 1;
                if (kb.dKey.isPressed) wish.x += 1; if (kb.aKey.isPressed) wish.x -= 1;
                sprint = kb.leftShiftKey.isPressed && Stats.Stamina > 3 && wish.y > 0;
                jump = kb.spaceKey.isPressed;
                if (kb.cKey.wasPressedThisFrame || kb.leftCtrlKey.wasPressedThisFrame) Crouching = !Crouching;
                if (kb.vKey.wasPressedThisFrame) FirstPerson = !FirstPerson;
                if (kb.fKey.wasPressedThisFrame) ToggleFlashlight();
            }
            if (sprint) Crouching = false;
            // opstaan kan alleen als er ruimte boven je hoofd is
            if (!Crouching && VoxelPhysics.Overlaps(Store, Pos, HalfWidth, Height)) Crouching = true;
            float h = Crouching ? CrouchHeight : Height;

            float feetBlockY = Pos.Y / World.VoxelSize;
            byte atChest = Store.Get(Mathf.FloorToInt(Pos.X / World.VoxelSize), Mathf.FloorToInt(feetBlockY + 1.6f), Mathf.FloorToInt(Pos.Z / World.VoxelSize));
            InWater = atChest == B.Water || (Pos.Y + 0.8f < World.SeaLevelMeters && Store.Get(Mathf.FloorToInt(Pos.X / World.VoxelSize), Mathf.FloorToInt(feetBlockY + 0.5f), Mathf.FloorToInt(Pos.Z / World.VoxelSize)) == B.Water);

            float speed = InWater ? 2.2f : Crouching ? 1.9f : sprint ? 7.2f : 4.3f;
            if (Stats.Health < 25) speed *= 0.8f;
            if (Inv.Weight > Inv.MaxWeight) speed *= 0.6f;
            wish = Vector2.ClampMagnitude(wish, 1f);
            float yr = Yaw * Mathf.Deg2Rad;
            Vector3 fwd = new Vector3(Mathf.Sin(yr), 0, Mathf.Cos(yr)), right = new Vector3(fwd.z, 0, -fwd.x);
            Vector3 target = (fwd * wish.y + right * wish.x) * speed;
            float accel = Grounded ? 14f : InWater ? 4f : 3f;
            Vel.X = Mathf.Lerp(Vel.X, target.x, 1 - Mathf.Exp(-accel * dt));
            Vel.Z = Mathf.Lerp(Vel.Z, target.z, 1 - Mathf.Exp(-accel * dt));
            if (InWater)
            {
                Vel.Y = Mathf.Lerp(Vel.Y, jump ? 2.6f : -0.8f, 1 - Mathf.Exp(-3f * dt));
            }
            else
            {
                if (jump && Grounded && !Crouching) { Vel.Y = JumpSpeed; Grounded = false; Stats.Stamina = Mathf.Max(0, Stats.Stamina - 4); }
                Vel.Y -= Gravity * dt;
            }
            if (worldReady)
            {
                float vyBefore = Vel.Y;
                bool wasGrounded = Grounded;
                Grounded = VoxelPhysics.Move(Store, ref Pos, ref Vel, Vel * dt, HalfWidth, h, Grounded || InWater);
                if (!wasGrounded && Grounded && vyBefore < -13f && !InWater)
                {
                    float dmg = (-vyBefore - 13f) * 6f;
                    Stats.Hurt(dmg, 0.2f, rng);
                    Game.Hud.Message($"Harde landing: −{dmg:0} gezondheid.");
                }
            }
            else Vel = new V3(0, 0, 0);

            transform.position = new Vector3(Pos.X, Pos.Y, Pos.Z);
            float hs = new Vector2(Vel.X, Vel.Z).magnitude;
            bool aiming = !ui && mouse != null && mouse.rightButton.isPressed && SelectedDef != null && SelectedDef.GunDamage > 0;
            if (hs > 0.3f && !aiming) body.transform.rotation = Quaternion.Slerp(body.transform.rotation, Quaternion.LookRotation(new Vector3(Vel.X, 0, Vel.Z)), 1 - Mathf.Exp(-12 * dt));
            if (aiming) body.transform.rotation = Quaternion.Euler(0, Yaw, 0);
            attackAnim = Mathf.Max(0, attackAnim - dt * 3.2f);
            body.Animate(hs, Crouching, Grounded || InWater, aiming, attackAnim, dt);
            body.SetVisible(!FirstPerson);

            // ------------------------------------------------ camera
            float eye = (Crouching ? 1.15f : 1.6f);
            headBob += hs * dt * 1.6f;
            Vector3 pivot = transform.position + Vector3.up * eye;
            Quaternion look = Quaternion.Euler(Pitch, Yaw, 0);
            if (FirstPerson)
            {
                Cam.transform.position = pivot + Vector3.up * (Mathf.Sin(headBob * 2f) * 0.03f * Mathf.Clamp01(hs / 4f));
            }
            else
            {
                float want = aiming ? 1.6f : 3.4f;
                camDist = Mathf.Lerp(camDist, want, 1 - Mathf.Exp(-10 * dt));
                Vector3 shoulder = pivot + look * new Vector3(0.55f, 0.15f, 0);
                Vector3 back = look * Vector3.back;
                var hit = Store.Raycast(new V3(shoulder.x, shoulder.y, shoulder.z), new V3(back.x, back.y, back.z), camDist + 0.3f, false);
                float d = hit.Hit ? Mathf.Max(0.3f, hit.Distance - 0.3f) : camDist;
                Cam.transform.position = shoulder + back * d;
            }
            Cam.transform.rotation = look;
            Cam.fieldOfView = Mathf.Lerp(Cam.fieldOfView, aiming ? 55f : sprint ? 76f : 70f, 1 - Mathf.Exp(-8 * dt));

            // ------------------------------------------------ doel en acties
            UpdateTarget(pivot);
            if (!ui && alive) HandleActions(kb, mouse, dt, pivot);

            // zaklamp
            if (Flashlight)
            {
                FlashBattery -= dt / 600f;       // 10 minuten per batterij
                if (FlashBattery <= 0)
                {
                    if (Inv.Remove("batterij", 1)) { FlashBattery = 1; Game.Hud.Message("Nieuwe batterij in de zaklamp."); }
                    else { FlashBattery = 0; Flashlight = false; Game.Hud.Message("De zaklamp is leeg."); }
                }
                flash.intensity = 900f * Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(FlashBattery * 4f)) * (FlashBattery < 0.08f && Mathf.PerlinNoise(Time.time * 9f, 0) > 0.6f ? 0.2f : 1f);
            }
            flash.enabled = Flashlight;

            // ------------------------------------------------ lichaam
            nearFireTimer -= dt;
            if (nearFireTimer <= 0) { nearFireTimer = 0.5f; NearFire = ScanForFire(); }
            float exertion = sprint && hs > 1 ? 2 : hs > 0.5f ? 1 : 0;
            Stats.Tick(dt, Game.Clock.Ambient + (Flashlight ? 0 : 0), exertion, InWater, NearFire);
        }

        void ToggleFlashlight()
        {
            if (!Flashlight && FlashBattery <= 0)
            {
                if (Inv.Remove("batterij", 1)) FlashBattery = 1;
                else { Game.Hud.Message("Geen batterij voor de zaklamp."); return; }
            }
            Flashlight = !Flashlight;
        }

        bool ScanForFire()
        {
            int cx = Mathf.FloorToInt(Pos.X / World.VoxelSize), cy = Mathf.FloorToInt(Pos.Y / World.VoxelSize), cz = Mathf.FloorToInt(Pos.Z / World.VoxelSize);
            for (int y = -3; y <= 4; y++)
                for (int z = -8; z <= 8; z++)
                    for (int x = -8; x <= 8; x++)
                        if (Store.Get(cx + x, cy + y, cz + z) == B.Campfire) return true;
            return false;
        }

        void UpdateTarget(Vector3 pivot)
        {
            Vector3 o = Cam.transform.position, f = Cam.transform.forward;
            float extra = Vector3.Distance(o, pivot);
            var hit = Store.Raycast(new V3(o.x, o.y, o.z), new V3(f.x, f.y, f.z), Reach + extra, true);
            if (hit.Hit)
            {
                var c = new Vector3((hit.X + 0.5f) * World.VoxelSize, (hit.Y + 0.5f) * World.VoxelSize, (hit.Z + 0.5f) * World.VoxelSize);
                if (Vector3.Distance(c, pivot) > Reach) hit.Hit = false;
            }
            Target = hit;
            highlight.gameObject.SetActive(hit.Hit && !Game.Hud.CapturesInput);
            if (hit.Hit) highlight.position = new Vector3((hit.X + 0.5f) * World.VoxelSize, (hit.Y + 0.5f) * World.VoxelSize, (hit.Z + 0.5f) * World.VoxelSize);
            if (!hit.Hit || hit.X != mineX || hit.Y != mineY || hit.Z != mineZ) { MineProgress = 0; mineX = hit.Hit ? hit.X : int.MinValue; mineY = hit.Y; mineZ = hit.Z; }
        }

        void HandleActions(Keyboard kb, Mouse mouse, float dt, Vector3 pivot)
        {
            if (kb != null)
            {
                for (int i = 0; i < Inventory.HotbarSize; i++)
                    if (kb[Key.Digit1 + i].wasPressedThisFrame) { Selected = i; RefreshTool(); }
                if (kb.eKey.wasPressedThisFrame) Interact();
                if (kb.qKey.wasPressedThisFrame) UseSelected();
            }
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll != 0) { Selected = (Selected + (scroll < 0 ? 1 : Inventory.HotbarSize - 1)) % Inventory.HotbarSize; RefreshTool(); }
                useCooldown -= dt;
                if (mouse.leftButton.isPressed) Mine(dt);
                else MineProgress = 0;
                if (mouse.rightButton.wasPressedThisFrame && (SelectedDef == null || SelectedDef.GunDamage <= 0)) Place();
            }
        }

        void Mine(float dt)
        {
            if (attackAnim <= 0) attackAnim = 1;
            if (!Target.Hit) return;
            var info = Blocks.Info[Target.Block];
            if ((info.Flags & BlockFlags.Unbreakable) != 0) return;
            var tool = SelectedDef;
            float mul = tool != null ? tool.MineSpeed : 1f;
            if (tool != null && (Target.Block == B.Log || Target.Block == B.Planks || Target.Block == B.WoodWall)) mul = Mathf.Max(mul, tool.WoodSpeed);
            MineProgress += dt * mul / Mathf.Max(0.05f, info.Hardness);
            if (MineProgress < 1f) return;
            MineProgress = 0;
            int x = Target.X, y = Target.Y, z = Target.Z;
            if ((info.Flags & BlockFlags.Container) != 0) GiveLoot(x, y, z, Target.Block, true);
            Chunks.SetBlock(x, y, z, B.Air);
            Game.Campfires.OnBlockChanged(x, y, z, B.Air);
            if (info.Drop != null)
            {
                int n = info.DropCount;
                int left = Inv.Add(info.Drop, n);
                var def = Items.Get(info.Drop);
                if (left < n) Game.Hud.Message($"+{n - left} {def?.Name ?? info.Drop}");
                if (left > 0) Game.Hud.Message("Je rugzak zit vol.");
            }
        }

        void Place()
        {
            if (!Target.Hit) return;
            var def = SelectedDef;
            if (def == null || def.Kind != ItemKind.Block || def.PlaceBlock == 0) return;
            int x = Target.X + Target.Nx, y = Target.Y + Target.Ny, z = Target.Z + Target.Nz;
            byte cur = Store.Get(x, y, z);
            if (cur != B.Air && cur != B.Water && cur != B.Crop) return;
            // niet in jezelf bouwen
            float vs = World.VoxelSize;
            bool overlap = x * vs < Pos.X + HalfWidth && (x + 1) * vs > Pos.X - HalfWidth &&
                           z * vs < Pos.Z + HalfWidth && (z + 1) * vs > Pos.Z - HalfWidth &&
                           y * vs < Pos.Y + Height && (y + 1) * vs > Pos.Y;
            if (overlap) return;
            Chunks.SetBlock(x, y, z, def.PlaceBlock);
            Game.Campfires.OnBlockChanged(x, y, z, def.PlaceBlock);
            Inv.TakeFromSlot(Selected);
            RefreshTool();
            attackAnim = 0.6f;
        }

        void Interact()
        {
            if (InWater && (!Target.Hit || Target.Distance > 2f))
            {
                Stats.Water = Mathf.Min(100, Stats.Water + 15);
                if (rng.NextDouble() < 0.25) { Stats.Sick = true; Game.Hud.Message("Je drinkt ongekookt water… je voelt je misselijk worden."); }
                else Game.Hud.Message("Je drinkt wat water uit het meer.");
                return;
            }
            if (!Target.Hit) return;
            byte b = Target.Block;
            if (b == B.Crop)
            {
                Chunks.SetBlock(Target.X, Target.Y, Target.Z, B.Air);
                Inv.Add("groente", 1);
                Game.Hud.Message("+1 Wilde groente");
                return;
            }
            if (Blocks.Is(b, BlockFlags.Container)) GiveLoot(Target.X, Target.Y, Target.Z, b, false);
        }

        void GiveLoot(int x, int y, int z, byte container, bool broken)
        {
            if (Store.IsLooted(x, y, z))
            {
                if (!broken) Game.Hud.Message("Leeg. Iemand was je voor.");
                return;
            }
            var lot = Game.Gen.LotAtVoxel(x, z);
            var loot = Loot.Roll(lot?.Type, container, x, y, z, Game.Gen.Seed);
            Store.MarkLooted(x, y, z);
            Game.Hud.OpenLoot(loot, Blocks.Info[container].Name);
        }

        void UseSelected()
        {
            if (useCooldown > 0) return;
            var s = Inv.Slots[Selected];
            if (s.Empty) return;
            var msg = Stats.Use(s.Def);
            if (msg == null) return;
            Inv.TakeFromSlot(Selected);
            Game.Hud.Message(msg);
            useCooldown = 0.6f;
            RefreshTool();
        }

        /// <summary>Hoogte om op te spawnen: bovenkant van de hoogste vaste voxel in deze kolom.</summary>
        public static float GroundAt(VoxelStore store, float x, float z)
        {
            int vx = Mathf.FloorToInt(x / World.VoxelSize), vz = Mathf.FloorToInt(z / World.VoxelSize);
            for (int y = World.Height - 2; y > 0; y--)
            {
                if (!Blocks.Solid[store.Get(vx, y, vz)]) continue;
                bool free = true;
                for (int k = 1; k <= 4; k++) if (Blocks.Solid[store.Get(vx, y + k, vz)]) free = false;
                if (free) return (y + 1) * World.VoxelSize;
            }
            return World.SeaLevelMeters + 1;
        }
    }
}
