using Deadhaul.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.HighDefinition;

namespace Deadhaul
{
    /// <summary>
    /// De overlever: beweging met voxelbotsing, third/first-person camera, vuurwapens (richten,
    /// terugslag, herladen, vuurmodus), melee, slopen, bouwen, looten, eten/drinken, zaklamp en straling.
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
        public float Radiation;                 // straling op deze plek (0..1)
        public bool Sheltered;                  // een dak boven je hoofd (tegen regen en stralingsstorm)

        // wapen
        public bool Aiming, Reloading, FullAuto = true;
        public float AimT, ReloadT, Bloom;
        public WeaponStats Weapon;
        public bool HasGun;
        public float Draw;                      // spanning van de boog (0..1)
        float fireCooldown, recoilPitch, recoilYaw, meleeCooldown, geigerTimer;

        // voertuig en vissen
        public Vehicle Vehicle;
        float shake;
        public void Shake(float amount) { shake = Mathf.Max(shake, amount); }
        public bool Fishing, Bite;
        public float FishTimer;
        Vector3 bobber;
        float lastMouseMove = -10;

        VoxelCharacter body;
        Transform highlight, muzzle, viewModel, viewMuzzle;
        Light flash;
        float stepDist, attackAnim, nearFireTimer, useCooldown, camDist = 3.4f, headBob;
        int mineX = int.MinValue, mineY, mineZ;
        readonly System.Random rng = new System.Random();
        string lastToolKey;

        ChunkManager Chunks => Game.Chunks;
        VoxelStore Store => Game.Chunks.Store;
        Survival Stats => Game.Stats;
        Inventory Inv => Game.Inventory;

        public void Init(GameState game, Camera cam)
        {
            Game = game; Cam = cam;
            body = VoxelCharacter.Build(transform, LookNow());
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

        VoxelCharacter.Look LookNow() => VoxelCharacter.Look.FromEquipment(Game.Equipment, B.Skin, B.Hair);

        /// <summary>Na het wisselen van kleding: nieuw uiterlijk, nieuwe rugzakgrootte.</summary>
        public void RefreshLook()
        {
            Game.Equipment.Apply(Inv);
            body.Rebuild(LookNow());
            lastToolKey = null;
            RefreshTool();
        }

        public void Teleport(V3 p) { Pos = p; Vel = new V3(0, 0, 0); }

        public ItemDef SelectedDef => Inv.Slots[Selected].Def;
        public ref Stack SelectedStack => ref Inv.Slots[Selected];

        public void RefreshTool()
        {
            var s = Inv.Slots[Selected];
            string key = s.Empty ? "" : s.Id + "|" + (s.Mods == null ? "" : string.Join(",", s.Mods));
            HasGun = !s.Empty && s.Def.GunDamage > 0;
            if (HasGun) Weapon = Arsenal.Stats(s);
            if (key == lastToolKey) return;
            lastToolKey = key;
            muzzle = body.SetTool(s, out _);
            Reloading = false;
            if (viewModel) Destroy(viewModel.gameObject);
            viewModel = null; viewMuzzle = null;
            if (HasGun)
            {
                viewModel = WeaponView.Build(s, Cam.transform, out viewMuzzle, out _);
                foreach (var r in viewModel.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        float menuOrbit;

        /// <summary>Hoofdmenu: de camera draait langzaam rond boven de startstraat.</summary>
        void MenuCamera(float dt)
        {
            menuOrbit += dt * 2.2f;
            transform.position = new Vector3(Pos.X, Pos.Y, Pos.Z);
            body.SetVisible(true);
            body.Animate(0, false, true, false, 0, dt, 0);
            var center = transform.position + Vector3.up * 1.4f;
            var rot = Quaternion.Euler(8f, Yaw + 150f + menuOrbit, 0);
            Cam.transform.position = center + rot * new Vector3(0.9f, 0.6f, -6.5f);
            Cam.transform.rotation = Quaternion.LookRotation(center + Vector3.up * 1.2f - Cam.transform.position);
            Cam.fieldOfView = 50f;
        }

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
            RefreshTool();
            if (Game.InMenu) { MenuCamera(dt); return; }

            // ------------------------------------------------ kijken
            if (!ui && alive && mouse != null)
            {
                float sens = Game.Settings.MouseSensitivity / Mathf.Lerp(1f, Mathf.Max(1f, Weapon.Zoom * 0.8f), HasGun ? AimT : 0);
                var d = mouse.delta.ReadValue() * sens;
                Yaw += d.x; Pitch = Mathf.Clamp(Pitch - d.y, -80f, 85f);
                if (d.sqrMagnitude > 0.01f) lastMouseMove = Time.time;
            }
            // terugslag veert terug
            float rec = 1 - Mathf.Exp(-9f * dt);
            Pitch += recoilPitch * rec * 0.6f; recoilPitch -= recoilPitch * rec;
            Yaw += recoilYaw * rec * 0.4f; recoilYaw -= recoilYaw * rec;

            if (Vehicle != null) { DriveUpdate(dt, kb, ui, alive); return; }

            // ------------------------------------------------ bewegen
            Vector2 wish = Vector2.zero;
            bool sprint = false, jump = false;
            if (!ui && alive && kb != null)
            {
                if (kb.wKey.isPressed) wish.y += 1; if (kb.sKey.isPressed) wish.y -= 1;
                if (kb.dKey.isPressed) wish.x += 1; if (kb.aKey.isPressed) wish.x -= 1;
                sprint = kb.leftShiftKey.isPressed && Stats.Stamina > 3 && wish.y > 0 && !Aiming;
                jump = kb.spaceKey.isPressed;
                if (kb.cKey.wasPressedThisFrame || kb.leftCtrlKey.wasPressedThisFrame) Crouching = !Crouching;
                if (kb.vKey.wasPressedThisFrame) FirstPerson = !FirstPerson;
                if (kb.fKey.wasPressedThisFrame) ToggleFlashlight();
            }
            if (sprint) Crouching = false;
            if (!Crouching && VoxelPhysics.Overlaps(Store, Pos, HalfWidth, Height)) Crouching = true;
            float h = Crouching ? CrouchHeight : Height;

            float feetBlockY = Pos.Y / World.VoxelSize;
            int bx = Mathf.FloorToInt(Pos.X / World.VoxelSize), bz = Mathf.FloorToInt(Pos.Z / World.VoxelSize);
            byte atChest = Store.Get(bx, Mathf.FloorToInt(feetBlockY + 1.6f), bz);
            InWater = atChest == B.Water || (Pos.Y + 0.8f < World.SeaLevelMeters && Store.Get(bx, Mathf.FloorToInt(feetBlockY + 0.5f), bz) == B.Water);

            float speed = InWater ? 2.2f : Crouching ? 1.9f : sprint ? 7.2f : 4.3f;
            if (Aiming) speed *= 0.55f;
            if (Stats.Health < 25) speed *= 0.8f;
            float over = Inv.Weight + Game.Equipment.Weight - Inv.MaxWeight;
            if (over > 0) speed *= Mathf.Max(0.35f, 1f - over / 20f);
            wish = Vector2.ClampMagnitude(wish, 1f);
            float yr = Yaw * Mathf.Deg2Rad;
            Vector3 fwd = new Vector3(Mathf.Sin(yr), 0, Mathf.Cos(yr)), right = new Vector3(fwd.z, 0, -fwd.x);
            Vector3 target = (fwd * wish.y + right * wish.x) * speed;
            float accel = Grounded ? 14f : InWater ? 4f : 3f;
            Vel.X = Mathf.Lerp(Vel.X, target.x, 1 - Mathf.Exp(-accel * dt));
            Vel.Z = Mathf.Lerp(Vel.Z, target.z, 1 - Mathf.Exp(-accel * dt));
            if (InWater) Vel.Y = Mathf.Lerp(Vel.Y, jump ? 2.6f : -0.8f, 1 - Mathf.Exp(-3f * dt));
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
            if (sprint && hs > 3 && Grounded) Game.Combat.Noise.Emit(Pos, 18, Game.Combat.PlayerActor.Id);
            else if (!Crouching && hs > 2 && Grounded) Game.Combat.Noise.Emit(Pos, 7, Game.Combat.PlayerActor.Id);
            // voetstappen op de ondergrond
            if ((Grounded || InWater) && hs > 0.4f)
            {
                stepDist += hs * dt;
                float stride = sprint ? 1.7f : Crouching ? 0.9f : 1.25f;
                if (stepDist > stride)
                {
                    stepDist = 0;
                    byte under = InWater ? B.Water : Store.Get(Mathf.FloorToInt(Pos.X / World.VoxelSize), Mathf.FloorToInt((Pos.Y - 0.05f) / World.VoxelSize), Mathf.FloorToInt(Pos.Z / World.VoxelSize));
                    Sfx.Instance.Footstep(Blocks.SurfaceOf(under), transform.position, Crouching ? 0.35f : sprint ? 1.2f : 0.8f, true);
                }
            }

            // ------------------------------------------------ richten
            Aiming = !ui && alive && HasGun && mouse != null && mouse.rightButton.isPressed && !Reloading && !sprint;
            AimT = Mathf.MoveTowards(AimT, Aiming ? 1 : 0, dt / Mathf.Max(0.05f, HasGun ? Weapon.AdsTime : 0.2f));
            bool faceAim = Aiming || (HasGun && mouse != null && mouse.leftButton.isPressed && !ui);
            if (hs > 0.3f && !faceAim) body.transform.rotation = Quaternion.Slerp(body.transform.rotation, Quaternion.LookRotation(new Vector3(Vel.X, 0, Vel.Z)), 1 - Mathf.Exp(-12 * dt));
            if (faceAim) body.transform.rotation = Quaternion.Euler(0, Yaw, 0);
            attackAnim = Mathf.Max(0, attackAnim - dt * 3.2f);
            body.Animate(hs, Crouching, Grounded || InWater, faceAim && HasGun, attackAnim, dt, Pitch);
            bool scoped = HasGun && Weapon.Zoom >= 3.5f && AimT > 0.95f;
            body.SetVisible(!FirstPerson && !scoped);

            // ------------------------------------------------ camera
            float eye = Crouching ? 1.15f : 1.6f;
            headBob += hs * dt * 1.6f;
            Vector3 pivot = transform.position + Vector3.up * eye;
            Quaternion look = Quaternion.Euler(Pitch, Yaw, 0);
            if (FirstPerson || scoped)
            {
                Cam.transform.position = pivot + look * new Vector3(0, 0, 0.12f) + Vector3.up * (Mathf.Sin(headBob * 2f) * 0.03f * Mathf.Clamp01(hs / 4f) * (1 - AimT));
            }
            else
            {
                float want = Mathf.Lerp(3.4f, 1.5f, AimT);
                camDist = Mathf.Lerp(camDist, want, 1 - Mathf.Exp(-10 * dt));
                Vector3 shoulder = pivot + look * new Vector3(Mathf.Lerp(0.55f, 0.42f, AimT), 0.15f, 0);
                Vector3 back = look * Vector3.back;
                var hit = Store.Raycast(new V3(shoulder.x, shoulder.y, shoulder.z), new V3(back.x, back.y, back.z), camDist + 0.3f, false);
                float d = hit.Hit ? Mathf.Max(0.3f, hit.Distance - 0.3f) : camDist;
                Cam.transform.position = shoulder + back * d;
            }
            Cam.transform.rotation = look;
            if (shake > 0)
            {
                shake = Mathf.Max(0, shake - dt * 1.6f);
                Cam.transform.position += Random.insideUnitSphere * shake * 0.25f;
                Cam.transform.rotation *= Quaternion.Euler(Random.Range(-1f, 1f) * shake * 2.5f, Random.Range(-1f, 1f) * shake * 2.5f, 0);
            }
            float zoom = HasGun ? Mathf.Lerp(1f, Weapon.Zoom, AimT) : 1f;
            float baseFov = Game.Settings.Fov + (sprint ? 6f : 0f);
            Cam.fieldOfView = Mathf.Lerp(Cam.fieldOfView, baseFov / zoom, 1 - Mathf.Exp(-14 * dt));

            // first-person wapenmodel
            if (viewModel)
            {
                bool show = (FirstPerson && !scoped);
                viewModel.gameObject.SetActive(show);
                var hip = new Vector3(0.2f, -0.2f, 0.42f);
                var ads = new Vector3(0f, -0.1f, 0.3f);
                float bob = Mathf.Sin(headBob * 2f) * 0.01f * (1 - AimT);
                viewModel.localPosition = Vector3.Lerp(hip, ads, AimT) + new Vector3(bob, Mathf.Abs(bob), -recoilPitch * 0.01f);
                viewModel.localRotation = Quaternion.Euler(recoilPitch * 0.8f, 0, 0);
            }

            // ------------------------------------------------ doel en acties
            UpdateTarget(pivot);
            fireCooldown -= dt; meleeCooldown -= dt;
            Bloom = Mathf.MoveTowards(Bloom, 0, dt * 2.5f);
            if (Reloading)
            {
                ReloadT -= dt;
                if (ReloadT <= 0) FinishReload();
            }
            if (!ui && alive) HandleActions(kb, mouse, dt, pivot);

            // laser
            if (HasGun && Weapon.Laser && !ui)
            {
                var o = Cam.transform.position; var f = Cam.transform.forward;
                var lh = Store.Raycast(new V3(o.x, o.y, o.z), new V3(f.x, f.y, f.z), 120f, true);
                if (lh.Hit) Fx.Instance.Emit(o + f * (lh.Distance - 0.02f), Vector3.zero, B.Glow, 0.025f, 0.02f, 0, 0);
            }

            // zaklamp
            bool weaponLamp = HasGun && Weapon.Flashlight;
            if (Flashlight && !weaponLamp)
            {
                FlashBattery -= dt / 600f;       // 10 minuten per batterij
                if (FlashBattery <= 0)
                {
                    if (Inv.Remove("batterij", 1)) { FlashBattery = 1; Game.Hud.Message("Nieuwe batterij in de zaklamp."); }
                    else { FlashBattery = 0; Flashlight = false; Game.Hud.Message("De zaklamp is leeg."); }
                }
            }
            flash.intensity = weaponLamp ? 1400f : 900f * Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(FlashBattery * 4f)) * (FlashBattery < 0.08f && Mathf.PerlinNoise(Time.time * 9f, 0) > 0.6f ? 0.2f : 1f);
            flash.spotAngle = weaponLamp ? 40f : 58f;
            flash.enabled = Flashlight;

            // ------------------------------------------------ lichaam en straling
            nearFireTimer -= dt;
            if (nearFireTimer <= 0)
            {
                nearFireTimer = 0.5f; NearFire = ScanForFire();
                var head = new V3(Pos.X, Pos.Y + 1.6f, Pos.Z);
                Sheltered = Store.Raycast(head, new V3(0, 1, 0), 40f, false).Hit;
            }
            Radiation = Game.Gen.RadiationAt(bx, bz, out _);
            var wx = Game.Now;
            if (!Sheltered && wx.RadStorm > 0.05f) Radiation = Mathf.Max(Radiation, wx.RadStorm * 0.55f);
            float dose = Radiation * Radiation * 3.5f * (1 - Game.Equipment.Radiation);
            if (Radiation > 0.02f)
            {
                geigerTimer -= dt;
                if (geigerTimer <= 0) { Sfx.Instance.Play2D("tik", 0.5f, 1f + (float)rng.NextDouble() * 0.3f); geigerTimer = Mathf.Lerp(0.6f, 0.02f, Radiation) * (float)(0.3 + rng.NextDouble()); }
            }
            float exertion = sprint && hs > 1 ? 2 : hs > 0.5f ? 1 : 0;
            // hoger is kouder: boven de boomgrens vriest het
            float altitude = Pos.Y - (World.Sea + 30) * World.VoxelSize;
            float ambient = Game.Clock.Ambient - Mathf.Clamp01(altitude / 12f) * 0.45f + Weather.SeasonWarmth(wx.Season);
            if (!Sheltered) ambient -= wx.Rain * 0.12f + wx.Snow * 0.1f + wx.Wind * 0.05f;
            Stats.Tick(dt, ambient, exertion, InWater, NearFire, Game.Equipment.Warmth, dose);
        }

        void ToggleFlashlight()
        {
            bool weaponLamp = HasGun && Weapon.Flashlight;
            if (!Flashlight && FlashBattery <= 0 && !weaponLamp)
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
            highlight.gameObject.SetActive(hit.Hit && !Game.Hud.CapturesInput && !HasGun);
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
                if (kb.rKey.wasPressedThisFrame && HasGun) StartReload();
                if (kb.bKey.wasPressedThisFrame && HasGun && Weapon.Automatic) { FullAuto = !FullAuto; Game.Hud.Message(FullAuto ? "Vuurmodus: automatisch" : "Vuurmodus: enkel schot"); Sfx.Instance.Play2D("droog", 0.5f, 1.4f); }
            }
            if (mouse == null) return;
            float scroll = mouse.scroll.ReadValue().y;
            if (scroll != 0) { Selected = (Selected + (scroll < 0 ? 1 : Inventory.HotbarSize - 1)) % Inventory.HotbarSize; RefreshTool(); }
            useCooldown -= dt;

            if (HasGun && SelectedDef.Id == "boog")
            {
                // boog: vasthouden om te spannen, loslaten om te schieten; na het schot een nieuwe pijl opleggen
                ref var bs = ref Inv.Slots[Selected];
                if (bs.Ammo <= 0 && !Reloading && fireCooldown <= 0 && Inv.Count("pijl") > 0) StartReload();
                if (mouse.leftButton.isPressed && bs.Ammo > 0 && !Reloading)
                {
                    if (Draw == 0) Sfx.Instance.Play2D("span", 0.5f);
                    Draw = Mathf.Min(1f, Draw + dt / 0.75f);
                }
                else if (Draw > 0)
                {
                    if (Draw > 0.2f && bs.Ammo > 0) Shoot(Draw);
                    Draw = 0;
                }
                return;
            }
            Draw = 0;
            if (HasGun)
            {
                bool trigger = Weapon.Automatic && FullAuto ? mouse.leftButton.isPressed : mouse.leftButton.wasPressedThisFrame;
                if (trigger) Shoot();
                return;
            }
            if (SelectedDef != null && SelectedDef.Id == "hengel") { FishUpdate(mouse, dt); return; }
            if (SelectedDef != null && SelectedDef.Id == "granaat")
            {
                if (mouse.leftButton.wasPressedThisFrame && meleeCooldown <= 0)
                {
                    var f = Cam.transform.forward;
                    var from = transform.position + Vector3.up * 1.6f + f * 0.6f;
                    Game.Combat.ThrowGrenade(from, f * 15f + Vector3.up * 3.5f, Game.Combat.PlayerActor.Id);
                    Inv.TakeFromSlot(Selected);
                    RefreshTool();
                    attackAnim = 1; meleeCooldown = 0.8f;
                    Game.Hud.Message("Granaat!");
                }
                return;
            }
            if (Fishing) Fishing = false;
            if (mouse.leftButton.wasPressedThisFrame && meleeCooldown <= 0)
            {
                var mdef = SelectedDef;
                var dmg = mdef?.MeleeDamage ?? 0;
                if (dmg <= 0) dmg = 9;   // vuisten
                float stab = mdef != null && mdef.MeleeDamage > 0 ? mdef.BackstabMul : 2f;
                if (Game.Combat.Melee(Cam.transform.position, Cam.transform.forward, dmg, stab)) { meleeCooldown = mdef?.MeleeInterval ?? 0.5f; attackAnim = 1; MineProgress = 0; return; }
            }
            if (mouse.leftButton.isPressed) Mine(dt);
            else MineProgress = 0;
            if (mouse.rightButton.wasPressedThisFrame) Place();
        }

        // ------------------------------------------------ vuurwapens
        void Shoot(float power = 1f)
        {
            if (fireCooldown > 0 || Reloading) return;
            ref var s = ref Inv.Slots[Selected];
            if (s.Ammo <= 0)
            {
                Sfx.Instance.Play2D("droog", 0.7f);
                fireCooldown = 0.25f;
                if (Inv.Count(s.Def.AmmoId) > 0) StartReload();
                return;
            }
            s.Ammo--;
            fireCooldown = Weapon.Interval;
            var st = Weapon;
            if (power < 1f)
            {
                // half gespannen: trager, minder schade, minder zuiver
                st.Damage *= power * power;
                st.Velocity *= 0.45f + 0.55f * power;
                st.Spread *= 2f - power;
            }
            // richtpunt: waar het midden van het scherm naar wijst
            Vector3 camPos = Cam.transform.position, camFwd = Cam.transform.forward;
            var vh = Store.Raycast(new V3(camPos.x, camPos.y, camPos.z), new V3(camFwd.x, camFwd.y, camFwd.z), 400f, false);
            Game.Combat.Actors.Raycast(new V3(camPos.x, camPos.y, camPos.z), new V3(camFwd.x, camFwd.y, camFwd.z), vh.Hit ? vh.Distance : 400f, Game.Combat.PlayerActor.Id, out float at, out _);
            Vector3 aimPoint = camPos + camFwd * Mathf.Max(2f, at);
            Vector3 origin = (FirstPerson || AimT > 0.95f) ? camPos + camFwd * 0.2f : transform.position + Vector3.up * (Crouching ? 1.1f : 1.45f) + Cam.transform.right * 0.2f;
            Vector3 dir = (aimPoint - origin).normalized;
            float hs = new Vector2(Vel.X, Vel.Z).magnitude;
            float spread = st.Spread * Mathf.Lerp(1f, 0.22f, AimT) * (hs > 1 ? 1.6f : 1f) * (Crouching ? 0.75f : 1f) * (Grounded ? 1f : 2.5f) * (1f + Bloom);
            for (int p = 0; p < st.Pellets; p++)
            {
                Vector3 d = dir;
                float sp = spread * (st.Pellets > 1 ? 1f : 0.5f);
                d = Quaternion.Euler(Gauss() * sp, Gauss() * sp, 0) * d;
                Game.Combat.Fire(new V3(origin.x, origin.y, origin.z), new V3(d.x, d.y, d.z), st, Game.Combat.PlayerActor.Id, p == 0 && s.Ammo % 3 == 0);
            }
            Bloom = Mathf.Min(1.5f, Bloom + 0.18f);
            float kick = st.RecoilV * Mathf.Lerp(1f, 0.7f, AimT) * (Crouching ? 0.8f : 1f);
            recoilPitch -= kick * (0.8f + (float)rng.NextDouble() * 0.4f);
            recoilYaw += st.RecoilH * Gauss();
            var mz = FirstPerson && viewMuzzle ? viewMuzzle : muzzle;
            Vector3 mpos = mz ? mz.position : origin;
            if (!st.Arrow)
            {
                Fx.Instance.MuzzleFlash(mpos, dir, st.HidesFlash);
                Fx.Instance.Casing(mpos - dir * 0.25f, Cam.transform.right);
            }
            Sfx.Instance.Play(CombatSystem.SoundFor(s), mpos, 1f, 1f, st.Noise * 2f);
            Game.Combat.Noise.Emit(new V3(mpos.x, mpos.y, mpos.z), st.Noise, Game.Combat.PlayerActor.Id);
        }

        float Gauss() => (float)((rng.NextDouble() + rng.NextDouble() + rng.NextDouble()) / 3.0 - 0.5) * 2f;

        void StartReload()
        {
            ref var s = ref Inv.Slots[Selected];
            if (Reloading || s.Ammo >= Weapon.MagSize) return;
            if (Inv.Count(s.Def.AmmoId) <= 0) { Game.Hud.Message($"Geen {Items.Get(s.Def.AmmoId).Name}-munitie."); return; }
            Reloading = true;
            ReloadT = Weapon.ReloadTime;
            if (Weapon.Arrow) Sfx.Instance.Play2D("tik", 0.4f, 0.6f);
            else Sfx.Instance.Play2D("herladen", 0.8f, 1.9f / Mathf.Max(0.8f, Weapon.ReloadTime));
        }

        void FinishReload()
        {
            Reloading = false;
            ref var s = ref Inv.Slots[Selected];
            if (s.Empty || s.Def.GunDamage <= 0) return;
            int need = Weapon.MagSize - s.Ammo;
            int have = Inv.Count(s.Def.AmmoId);
            int take = Mathf.Min(need, have);
            if (take > 0) { Inv.Remove(s.Def.AmmoId, take); s.Ammo += take; }
        }

        // ------------------------------------------------ slopen, bouwen, looten
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
            var p = new Vector3((x + 0.5f) * World.VoxelSize, (y + 0.5f) * World.VoxelSize, (z + 0.5f) * World.VoxelSize);
            Fx.Instance.Burst(p, Vector3.up, Target.Block, 8, 2.5f, 0.06f, 1.1f);
            Sfx.Instance.Play(Target.Block == B.Glass ? "glas" : "slag", p, 0.6f, 0.8f);
            Game.Combat.Noise.Emit(new V3(p.x, p.y, p.z), 14, Game.Combat.PlayerActor.Id);
            if ((info.Flags & BlockFlags.Container) != 0) GiveLoot(x, y, z, Target.Block, true);
            if (B.IsPlant(Target.Block)) { Harvest(x, y, z, Target.Block); return; }
            Chunks.SetBlock(x, y, z, B.Air);
            Game.Campfires.OnBlockChanged(x, y, z, B.Air);
            BonusDrops(Target.Block);
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
            if (B.IsSeedling(def.PlaceBlock))
            {
                // zaaien: bovenop akkergrond, gras of aarde
                if (Target.Ny != 1 || !Farming.CanPlantOn(Target.Block, cur)) { Game.Hud.Message("Zaai op akkergrond, gras of aarde."); return; }
                if (Target.Block != B.Farmland) Chunks.SetBlock(Target.X, Target.Y, Target.Z, B.Farmland);
                Chunks.SetBlock(x, y, z, def.PlaceBlock);
                Inv.TakeFromSlot(Selected);
                attackAnim = 0.6f;
                return;
            }
            if (cur != B.Air && cur != B.Water && !B.IsPlant(cur)) return;
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
            if (Game.Combat.TryPickArrow(Cam.transform.position, Cam.transform.forward)) return;
            if (Game.Combat.TryLootCorpse(Cam.transform.position, Cam.transform.forward)) return;
            if (Game.Combat.TryTalk(Cam.transform.position, Cam.transform.forward)) return;
            var veh = Game.Vehicles.LookedAt(Cam.transform.position, Cam.transform.forward, 4.5f + Vector3.Distance(Cam.transform.position, transform.position + Vector3.up * 1.5f));
            if (veh != null) { Game.Hud.OpenVehicle(veh); return; }
            if (InWater && (!Target.Hit || Target.Distance > 2f))
            {
                Stats.Water = Mathf.Min(100, Stats.Water + 15);
                if (rng.NextDouble() < 0.25 || Radiation > 0.1f) { Stats.Sick = true; Game.Hud.Message("Je drinkt ongekookt water… je voelt je misselijk worden."); }
                else Game.Hud.Message("Je drinkt wat water uit het meer.");
                if (Radiation > 0.1f) Stats.Radiation = Mathf.Min(100, Stats.Radiation + 12);
                return;
            }
            if (!Target.Hit) return;
            byte b = Target.Block;
            if (B.IsPlant(b)) { Harvest(Target.X, Target.Y, Target.Z, b); return; }
            if (Blocks.Is(b, BlockFlags.Container)) GiveLoot(Target.X, Target.Y, Target.Z, b, false);
        }

        /// <summary>Sloopbuit: onderdelen uit autowrakken, wormen uit de aarde.</summary>
        void BonusDrops(byte b)
        {
            string item = null;
            double r = rng.NextDouble();
            if (b == B.Tire && r < 0.25) item = "band";
            else if (b == B.CarRed || b == B.CarBlue || b == B.CarGrey || b == B.CarWhite)
                item = r < 0.05 ? "bougies" : r < 0.07 ? "brandstofpomp" : r < 0.085 ? "accu" : null;
            else if ((b == B.Dirt || b == B.Grass || b == B.Farmland) && r < 0.12) item = "aas";
            if (item == null) return;
            if (Inv.Add(item, 1) == 0) Game.Hud.Message($"Gevonden: {Items.Get(item).Name}!");
        }

        // ------------------------------------------------ voertuigen
        public void EnterVehicle(Vehicle v)
        {
            if (!v.Drivable) return;
            Vehicle = v; v.Occupied = true;
            Aiming = false; Fishing = false; Crouching = false;
            body.gameObject.SetActive(false);
            if (viewModel) viewModel.gameObject.SetActive(false);
            Yaw = v.Yaw; Pitch = 15;
            Game.Hud.Message(v.Def.Boat ? "W/S varen, A/D sturen, E uitstappen." : "W/S gas en achteruit, A/D sturen, spatie remmen, H toeteren, L lichten, E uitstappen.");
        }

        /// <summary>Direct uit het voertuig (bij doodgaan of respawnen).</summary>
        public void LeaveVehicleImmediately()
        {
            if (Vehicle != null) { Vehicle.Occupied = false; Vehicle.Speed = 0; }
            Vehicle = null;
            body.gameObject.SetActive(true);
            lastToolKey = null;
        }

        public void ExitVehicle()
        {
            var v = Vehicle;
            if (v == null) return;
            if (Mathf.Abs(v.Speed) > 3f) { Game.Hud.Message("Eerst stoppen."); return; }
            float yr = v.Yaw * Mathf.Deg2Rad;
            var right = new Vector3(Mathf.Cos(yr), 0, -Mathf.Sin(yr));
            foreach (float side in new[] { -1f, 1f })
            {
                var p = CombatSystem.ToV(v.Pos) + right * side * (v.Def.Width * 0.5f + 0.7f);
                float gy = GroundAt(Store, p.x, p.z);
                if (Mathf.Abs(gy - v.Pos.Y) > 2.5f && !v.Def.Boat) continue;
                Teleport(new V3(p.x, v.Def.Boat ? Mathf.Max(gy, World.SeaLevelMeters - 1.2f) : gy + 0.02f, p.z));
                break;
            }
            v.Occupied = false; v.Speed = 0;
            Vehicle = null;
            body.gameObject.SetActive(true);
            lastToolKey = null;
            RefreshTool();
        }

        void DriveUpdate(float dt, Keyboard kb, bool ui, bool alive)
        {
            var v = Vehicle;
            var input = new VehiclePhysics.Input();
            if (!ui && alive && kb != null)
            {
                if (kb.wKey.isPressed) input.Throttle += 1; if (kb.sKey.isPressed) input.Throttle -= 1;
                if (kb.dKey.isPressed) input.Steer += 1; if (kb.aKey.isPressed) input.Steer -= 1;
                input.Brake = kb.spaceKey.isPressed;
                if (kb.eKey.wasPressedThisFrame) { ExitVehicle(); if (Vehicle == null) return; }
                if (kb.hKey.wasPressedThisFrame && !v.Def.Boat) Game.Vehicles.Horn(v);
                if (kb.lKey.wasPressedThisFrame) Game.Vehicles.Lights = !Game.Vehicles.Lights;
            }
            if (!alive) { LeaveVehicleImmediately(); return; }
            Game.Vehicles.Drive(v, input, dt);
            Pos = new V3(v.Pos.X, v.Pos.Y + 0.4f, v.Pos.Z);
            transform.position = CombatSystem.ToV(Pos);
            // camera: achter het voertuig, vrij rond te kijken met de muis
            if (Time.time - lastMouseMove > 1.5f) { Yaw = Mathf.LerpAngle(Yaw, v.Yaw, 1 - Mathf.Exp(-2.5f * dt)); Pitch = Mathf.Lerp(Pitch, 14, 1 - Mathf.Exp(-2 * dt)); }
            var look = Quaternion.Euler(Pitch, Yaw, 0);
            var pivot = CombatSystem.ToV(v.Pos) + Vector3.up * (v.Def.Height + 0.8f);
            float want = v.Def.Length * 1.3f + 3f;
            var back = look * Vector3.back;
            var hit = Store.Raycast(new V3(pivot.x, pivot.y, pivot.z), new V3(back.x, back.y, back.z), want + 0.3f, false);
            Cam.transform.position = pivot + back * (hit.Hit ? Mathf.Max(1f, hit.Distance - 0.3f) : want);
            Cam.transform.rotation = look;
            Cam.fieldOfView = Mathf.Lerp(Cam.fieldOfView, Game.Settings.Fov - 2 + Mathf.Abs(v.Speed) * 0.4f, 1 - Mathf.Exp(-4 * dt));
            highlight.gameObject.SetActive(false);
            flash.enabled = false;
            Stats.Tick(dt, Game.Clock.Ambient, 0, false, false, Game.Equipment.Warmth, Game.Gen.RadiationAt(Mathf.FloorToInt(Pos.X / World.VoxelSize), Mathf.FloorToInt(Pos.Z / World.VoxelSize), out _) * 2f * (1 - Game.Equipment.Radiation));
        }

        // ------------------------------------------------ vissen
        void FishUpdate(Mouse mouse, float dt)
        {
            if (!Fishing)
            {
                if (!mouse.leftButton.wasPressedThisFrame) return;
                // zoek wateroppervlak in de kijkrichting
                Vector3 o = Cam.transform.position, f = Cam.transform.forward;
                for (float t = 1; t < 22; t += 0.25f)
                {
                    var p = o + f * t;
                    int vx = Mathf.FloorToInt(p.x / World.VoxelSize), vy = Mathf.FloorToInt(p.y / World.VoxelSize), vz = Mathf.FloorToInt(p.z / World.VoxelSize);
                    byte b = Store.Get(vx, vy, vz);
                    if (b == B.Water) { bobber = new Vector3(p.x, World.SeaLevelMeters + 0.02f, p.z); break; }
                    if (Blocks.Solid[b]) { Game.Hud.Message("Werp uit op open water."); return; }
                    if (t >= 21.75f) { Game.Hud.Message("Werp uit op open water."); return; }
                }
                bool bait = Inv.Remove("aas", 1);
                Fishing = true; Bite = false;
                FishTimer = Deadhaul.Core.Fishing.WaitTime(bait, rng);
                attackAnim = 1;
                Sfx.Instance.Play("inslag", bobber, 0.3f, 1.8f, 30f);
                Game.Hud.Message(bait ? "Uitgeworpen met aas." : "Uitgeworpen (zonder aas bijt het minder snel).");
                return;
            }
            // dobber
            float bob = Bite ? -0.12f + Mathf.Sin(Time.time * 25) * 0.05f : Mathf.Sin(Time.time * 2) * 0.02f;
            Fx.Instance.Emit(bobber + Vector3.up * bob, Vector3.zero, B.Bandana, 0.06f, 0.03f, 0, 0);
            if ((CombatSystem.ToV(Pos) - bobber).magnitude > 26f) { Fishing = false; Game.Hud.Message("De lijn is te lang geworden."); return; }
            FishTimer -= dt;
            if (!Bite && FishTimer <= 0)
            {
                Bite = true; FishTimer = 1.3f;
                Fx.Instance.Burst(bobber, Vector3.up, B.Glass, 8, 1.5f, 0.04f, 0.5f, 6);
                Sfx.Instance.Play2D("tik", 0.8f, 0.6f);
                Game.Hud.Message("Beet! Klik nu!");
            }
            else if (Bite && FishTimer <= 0) { Fishing = false; Game.Hud.Message("De vis is ontsnapt."); return; }
            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (Bite)
                {
                    int bx = Mathf.FloorToInt(bobber.x / World.VoxelSize), bz = Mathf.FloorToInt(bobber.z / World.VoxelSize);
                    var fish = Deadhaul.Core.Fishing.Catch(Game.Gen.RadiationAt(bx, bz, out _), rng);
                    if (Inv.Add(fish, 1) == 0) Game.Hud.Message($"Gevangen: {Items.Get(fish).Name}!");
                    else Game.Hud.Message("Je rugzak zit vol.");
                    Fx.Instance.Burst(bobber, Vector3.up, B.Glass, 14, 2.5f, 0.05f, 0.6f, 6);
                }
                else Game.Hud.Message("Binnengehaald.");
                Fishing = false;
                attackAnim = 1;
            }
        }

        /// <summary>Oogsten: rijpe gewassen geven eten en soms zaad; zaailingen geven hun zaad terug.</summary>
        void Harvest(int x, int y, int z, byte b)
        {
            var p = new Vector3((x + 0.5f) * World.VoxelSize, (y + 0.3f) * World.VoxelSize, (z + 0.5f) * World.VoxelSize);
            Chunks.SetBlock(x, y, z, B.Air);
            Fx.Instance.Burst(p, Vector3.up, B.Potato, 6, 1.5f, 0.04f, 0.8f);
            if (b == B.Crop) { Inv.Add("groente", 1); Game.Hud.Message("+1 Wilde groente"); return; }
            int type = B.CropIndex(b);
            string crop = B.CropItems[type];
            if (B.IsSeedling(b)) { Inv.Add("zaad_" + crop, 1); Game.Hud.Message($"+1 {Items.Get("zaad_" + crop).Name}"); }
            else
            {
                int n = 1 + rng.Next(3), seeds = rng.NextDouble() < 0.6 ? 1 + rng.Next(2) : 0;
                Inv.Add(crop, n);
                if (seeds > 0) Inv.Add("zaad_" + crop, seeds);
                Game.Hud.Message($"+{n} {Items.Get(crop).Name}" + (seeds > 0 ? $", +{seeds} zaad" : ""));
            }
            var st = Game.Gen.SettlementNear(x, z, out float d);
            if (st != null && d <= 0) Game.Combat.ReportTheft(st, new V3(p.x, p.y, p.z));
        }

        void GiveLoot(int x, int y, int z, byte container, bool broken)
        {
            if (Store.IsLooted(x, y, z))
            {
                if (!broken) Game.Hud.Message("Leeg. Iemand was je voor.");
                return;
            }
            var lot = Game.Gen.LotAtVoxel(x, z);
            var loot = Loot.Roll(lot?.Type, container, x, y, z, Game.Gen.Seed, container == B.AmmoCrate || lot?.Type == LotType.Militair);
            Store.MarkLooted(x, y, z);
            Game.Hud.OpenLoot(loot, Blocks.Info[container].Name);
        }

        void UseSelected()
        {
            if (useCooldown > 0) return;
            var s = Inv.Slots[Selected];
            if (s.Empty) return;
            if (s.Def.Kind == ItemKind.Clothing) { Game.WearFromSlot(Selected); return; }
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
