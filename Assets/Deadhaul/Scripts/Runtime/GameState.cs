using System;
using System.IO;
using Deadhaul.Core;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Deadhaul
{
    [Serializable]
    public sealed class GameSettings
    {
        public float MouseSensitivity = 0.09f;
        public int ViewRadius = 10;
        public float Fov = 70f;
        public float Volume = 1f;
        public int RayTracing = -1;         // -1 = standaard van de omgeving

        public void Load()
        {
            MouseSensitivity = PlayerPrefs.GetFloat("dh_muis", MouseSensitivity);
            ViewRadius = PlayerPrefs.GetInt("dh_zicht", ViewRadius);
            Fov = PlayerPrefs.GetFloat("dh_fov", Fov);
            Volume = PlayerPrefs.GetFloat("dh_volume", Volume);
            RayTracing = PlayerPrefs.GetInt("dh_rt", RayTracing);
        }

        public void Save()
        {
            PlayerPrefs.SetFloat("dh_muis", MouseSensitivity);
            PlayerPrefs.SetInt("dh_zicht", ViewRadius);
            PlayerPrefs.SetFloat("dh_fov", Fov);
            PlayerPrefs.SetFloat("dh_volume", Volume);
            PlayerPrefs.SetInt("dh_rt", RayTracing);
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// Het spel: bouwt alle systemen op (wereld, omgeving, speler, HUD), regelt spawnen,
    /// doodgaan en opslaan. Zet dit component op één leeg GameObject in de scène.
    /// </summary>
    public sealed class GameState : MonoBehaviour
    {
        public int Seed = 1337;
        public GameSettings Settings = new GameSettings();

        public WorldGen Gen { get; private set; }
        public ChunkManager Chunks { get; private set; }
        public EnvironmentController Environment { get; private set; }
        public PlayerController Player { get; private set; }
        public Hud Hud { get; private set; }
        public Campfires Campfires { get; private set; }
        public CombatSystem Combat { get; private set; }
        public Equipment Equipment { get; private set; } = new Equipment();
        public VehicleManager Vehicles { get; private set; }
        public BaseSystem Base { get; private set; }
        public Survival Stats { get; private set; } = new Survival();
        public Inventory Inventory { get; private set; } = new Inventory();
        public GameClock Clock { get; private set; } = new GameClock();
        public Weather Weather { get; private set; }
        public WeatherState Now;
        Precipitation precip;
        WeatherKind lastWeather;

        bool spawned;
        V3 pendingSpawn;
        bool pendingFromSave;
        float autosave = 60f;
        string lastCity;

        static string SavePath => Path.Combine(Application.persistentDataPath, "deadhaul.sav");
        public static bool HasSave => File.Exists(SavePath);

        void Awake()
        {
            Application.targetFrameRate = -1;
            Settings.Load();
            AudioListener.volume = Settings.Volume;
            Gen = new WorldGen(Seed);
            Weather = new Weather(Seed);

            var chunkGo = new GameObject("Wereld");
            Chunks = chunkGo.AddComponent<ChunkManager>();
            Chunks.Init(Gen, new VoxelStore());
            Chunks.ViewRadius = Settings.ViewRadius;

            var envGo = new GameObject("Omgeving");
            Environment = envGo.AddComponent<EnvironmentController>();
            Environment.Init(Clock);
            if (Settings.RayTracing >= 0) Environment.SetRayTracing((RayTracingQuality)Settings.RayTracing);

            Campfires = new GameObject("Kampvuren").AddComponent<Campfires>();
            Campfires.Init(Chunks);
            new GameObject("Effecten").AddComponent<Fx>();
            new GameObject("Geluid").AddComponent<Sfx>();
            Combat = new GameObject("Gevechten").AddComponent<CombatSystem>();
            Combat.Init(this);
            var farms = new GameObject("Akkers").AddComponent<Farms>();
            farms.Init(Chunks);
            farms.GrowthScale = () => Weather.SeasonGrowth(Now.Season) * (1f + Now.Rain * 0.5f);
            Vehicles = new GameObject("Voertuigen").AddComponent<VehicleManager>();
            Vehicles.Init(this);
            Base = new GameObject("Basis").AddComponent<BaseSystem>();
            Base.Init(this);

            var cam = CreateCamera();
            precip = new GameObject("Neerslag").AddComponent<Precipitation>();
            precip.Init(Chunks, cam.transform);
            Chunks.BlockChanged += (x, y, z, b) => precip.Invalidate(x, z);
            Chunks.ChunkLoaded += (cx, cz) => { for (int z = 0; z < World.ChunkSize; z++) for (int x = 0; x < World.ChunkSize; x++) precip.Invalidate(cx * World.ChunkSize + x, cz * World.ChunkSize + z); };
            Environment.Thunder = d => Sfx.Instance.ThunderAt(Player.Cam.transform.position, 60f + d * 340f);
            Player = new GameObject("Speler").AddComponent<PlayerController>();
            Hud = gameObject.AddComponent<Hud>();
            Hud.Init(this);
            Player.Init(this, cam);
            Campfires.Follow = Player.transform;

            // achter het hoofdmenu: de startstad in het avondlicht
            InMenu = true;
            NewGame();
            Clock.Time = 18.8 / 24.0;
        }

        /// <summary>True zolang het hoofdmenu open staat: geen invoer, overleving of vijanden.</summary>
        public bool InMenu { get; private set; }

        public void StartContinue()
        {
            if (!TryLoad()) NewGame();
            InMenu = false;
            lastCity = null;
        }

        public void StartNew()
        {
            DeleteSaveAndRestart();
            InMenu = false;
            lastCity = null;
        }

        Camera CreateCamera()
        {
            var existing = Camera.main;
            if (existing != null) Destroy(existing.gameObject);
            var go = new GameObject("Camera") { tag = "MainCamera" };
            var cam = go.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 1500f;
            cam.fieldOfView = 70f;
            var hd = go.AddComponent<HDAdditionalCameraData>();
            hd.antialiasing = HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing;
            go.AddComponent<AudioListener>();
            return cam;
        }

        public void NewGame()
        {
            Stats = new Survival();
            Inventory = new Inventory();
            Equipment = StarterClothes();
            Clock.Time = 7.0 / 24.0;
            var pistol = new Stack("pistool", 1) { Ammo = 9 };
            Inventory.AddStack(pistol);
            Inventory.Add("pijp", 1);
            Inventory.Add("water", 1);
            Inventory.Add("bonen", 1);
            Inventory.Add("verband", 2);
            Inventory.Add("batterij", 1);
            Inventory.Add("9mm", 12);
            Player.Selected = 0;
            Player.FlashBattery = 1f;
            Player.RefreshLook();
            Combat.ClearAll();
            Vehicles.Clear();
            Base.Reset(null);
            SpawnAtStart(false);
        }

        static Equipment StarterClothes()
        {
            var eq = new Equipment();
            eq.Wear(new Stack("sneakers", 1));
            eq.Wear(new Stack("jeans", 1));
            eq.Wear(new Stack("jas", 1));
            return eq;
        }

        /// <summary>Trekt het kledingstuk uit dit vak aan; wat je droeg gaat in dat vak.</summary>
        public void WearFromSlot(int slot)
        {
            var s = Inventory.Slots[slot];
            if (s.Empty || s.Def.Kind != ItemKind.Clothing) return;
            var old = Equipment.Wear(s);
            Inventory.Slots[slot] = old;
            Player.RefreshLook();
            Hud.Message(old.Empty ? $"Je trekt {s.Def.Name.ToLowerInvariant()} aan." : $"Je wisselt {old.Def.Name.ToLowerInvariant()} voor {s.Def.Name.ToLowerInvariant()}.");
        }

        /// <summary>Trekt iets uit en legt het in de rugzak, als het past.</summary>
        public void TakeOff(EquipSlot slot)
        {
            var s = Equipment.Slots[(int)slot];
            if (s.Empty) return;
            Equipment.TakeOff(slot);
            Equipment.Apply(Inventory);
            // vakken die wegvallen moeten leeg zijn
            bool fits = true;
            for (int i = Inventory.Capacity; i < Inventory.Slots.Length; i++) if (!Inventory.Slots[i].Empty) fits = false;
            if (!fits || !Inventory.AddStack(s))
            {
                Equipment.Wear(s);
                Equipment.Apply(Inventory);
                Hud.Message("Maak eerst ruimte in je rugzak.");
                return;
            }
            Player.RefreshLook();
        }

        /// <summary>Start midden in de startstad, op straat.</summary>
        void SpawnAtStart(bool keepStats)
        {
            var c = Gen.GetCity(0, 0);
            float vs = World.VoxelSize;
            pendingSpawn = new V3((c.X + 2.5f) * vs, 0, (c.Z + 26.5f) * vs);
            pendingFromSave = false;
            spawned = false;
            Player.Teleport(pendingSpawn);
            Player.Yaw = 180f;
        }

        public void Respawn()
        {
            Player.LeaveVehicleImmediately();
            Stats = new Survival();
            Inventory = new Inventory();
            Equipment = StarterClothes();
            Inventory.Add("water", 1);
            Inventory.Add("verband", 1);
            Player.Flashlight = false;
            Player.RefreshLook();
            Combat.ClearAll();
            if (Base.State.HasBed)
            {
                pendingSpawn = Base.State.BedSpawn; pendingFromSave = true; spawned = false;
                Player.Teleport(pendingSpawn);
                Hud.Message("Je wordt wakker in je eigen bed. Je spullen ben je kwijt — maar je kisten niet.");
                return;
            }
            SpawnAtStart(false);
            Hud.Message("Je wordt wakker in de straten van " + Gen.GetCity(0, 0).Name + ". Je spullen ben je kwijt.");
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Chunks.ViewRadius = Settings.ViewRadius;
            Chunks.Tick(Player.transform.position);
            if (!Hud.Paused) Clock.Tick(InMenu ? dt * 0.1f : dt);

            if (!spawned && Chunks.IsLoaded(new Vector3(pendingSpawn.X, 0, pendingSpawn.Z)))
            {
                if (!pendingFromSave) pendingSpawn.Y = PlayerController.GroundAt(Chunks.Store, pendingSpawn.X, pendingSpawn.Z) + 0.05f;
                Player.Teleport(pendingSpawn);
                spawned = true;
            }

            // weer
            Now = Weather.Sample(Clock.Time);
            Environment.Weather = Now;
            precip.Rain = Now.Rain; precip.Snow = Now.Snow; precip.Dust = Now.RadStorm * 0.7f; precip.Wind = Now.Wind;
            Sfx.Instance.SetWeather(Mathf.Max(Now.Rain, Now.RadStorm * 0.4f), Now.Wind, Player.Sheltered, dt);
            if (Now.Kind != lastWeather)
            {
                if (!InMenu && spawned)
                {
                    if (Now.Kind == WeatherKind.Stralingsstorm) Hud.Banner("Stralingsstorm", "zoek beschutting onder een dak of trek een hazmatpak aan");
                    else if (Now.Kind == WeatherKind.Onweer) Hud.Message("Er komt onweer aan.");
                    else if (Now.Kind == WeatherKind.Sneeuw) Hud.Message("Het begint te sneeuwen. Kleed je warm.");
                    else if (Now.Kind == WeatherKind.Regen) Hud.Message("Het begint te regenen.");
                    else if (lastWeather == WeatherKind.Stralingsstorm) Hud.Message("De stralingsstorm is voorbij.");
                }
                lastWeather = Now.Kind;
            }

            // omgevingsgeluid
            int vx = Mathf.FloorToInt(Player.Pos.X / World.VoxelSize), vz = Mathf.FloorToInt(Player.Pos.Z / World.VoxelSize);
            var city = Gen.CityAt(vx, vz, out float d, out _);
            float inCity = city != null ? Mathf.Clamp01((city.R + 40 - d) / 80f) : 0;
            Sfx.Instance.Ambience(new Sfx.AmbienceState
            {
                Day = Mathf.Clamp01((Clock.Ambient - 0.3f) / 0.4f), Nature = 1 - inCity, City = inCity, Menu = InMenu ? 1 : 0, Rain = Mathf.Max(Now.Rain, Now.Snow),
                Radiation = Mathf.Clamp01(Player.Radiation * 3f), Listener = Player.Cam.transform.position,
            }, dt);
            if (InMenu) return;

            // stadsnaam tonen als je een stad binnenloopt
            string name = city != null && d < city.R ? city.Name : null;
            string sub = city != null ? (city.Kind == "capital" ? "hoofdstad" : city.Kind) : null;
            var settle = Gen.SettlementNear(vx, vz, out float sd);
            if (settle != null && sd <= 0) { name = settle.Name; sub = "nederzetting van overlevers"; }
            if (Gen.RadiationAt(vx, vz, out float rel) > 0.25f && name == null) { name = "Inslagkrater"; sub = "hoge straling"; }
            if (name != lastCity)
            {
                if (name != null) Hud.Banner(name, sub);
                lastCity = name;
            }

            autosave -= dt;
            if (autosave <= 0 && spawned && !Stats.Dead) { autosave = 60f; Save(); }
        }

        public bool Ready => spawned;

        void OnApplicationQuit()
        {
            if (spawned && !InMenu && !Stats.Dead) Save();
        }

        // ------------------------------------------------------------ opslaan
        const int SaveVersion = 5;      // 4: metgezellen, 5: eigen basis

        public void Save()
        {
            try
            {
                using var fs = File.Create(SavePath);
                using var w = new BinaryWriter(fs);
                w.Write(SaveVersion);
                w.Write(Seed);
                w.Write(Player.Pos.X); w.Write(Player.Pos.Y); w.Write(Player.Pos.Z);
                w.Write(Player.Yaw); w.Write(Player.Pitch);
                w.Write(Player.FlashBattery);
                w.Write(Clock.Time);
                Stats.Write(w);
                Inventory.Write(w);
                Equipment.Write(w);
                w.Write(Combat.Kills);
                Vehicles.Write(w);
                Combat.WriteFollowers(w);
                Base.State.Write(w);
                Chunks.Store.WriteEdits(w);
                Hud.Message("Spel opgeslagen.");
            }
            catch (Exception e) { Debug.LogException(e); Hud.Message("Opslaan mislukt: " + e.Message); }
        }

        bool TryLoad()
        {
            if (!HasSave) return false;
            try
            {
                using var fs = File.OpenRead(SavePath);
                using var r = new BinaryReader(fs);
                int version = r.ReadInt32();
                if (version > SaveVersion || version < 3) return false;
                if (r.ReadInt32() != Seed) return false;
                var p = new V3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                Player.Yaw = r.ReadSingle(); Player.Pitch = r.ReadSingle();
                Player.FlashBattery = r.ReadSingle();
                Clock.Time = r.ReadDouble();
                Stats = new Survival(); Stats.Read(r);
                Inventory = new Inventory(); Inventory.Read(r);
                Equipment = new Equipment(); Equipment.Read(r);
                Combat.Kills = r.ReadInt32();
                Vehicles.Read(r);
                if (version >= 4) Combat.ReadFollowers(r);
                var bs = new BaseState();
                if (version >= 5) bs.Read(r);
                Base.Reset(bs);
                Chunks.Store.ReadEdits(r);
                Chunks.ReloadAll();
                pendingSpawn = p; pendingFromSave = true; spawned = false;
                Player.Teleport(p);
                Player.RefreshLook();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Save kon niet geladen worden: " + e.Message);
                return false;
            }
        }

        public void DeleteSaveAndRestart()
        {
            if (HasSave) File.Delete(SavePath);
            Chunks.Store.ReadEdits(new BinaryReader(new MemoryStream(new byte[8])));
            Chunks.ReloadAll();
            NewGame();
        }
    }
}
