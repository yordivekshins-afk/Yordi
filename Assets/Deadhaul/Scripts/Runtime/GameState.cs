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
        public Survival Stats { get; private set; } = new Survival();
        public Inventory Inventory { get; private set; } = new Inventory();
        public GameClock Clock { get; private set; } = new GameClock();

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
            Gen = new WorldGen(Seed);

            var chunkGo = new GameObject("Wereld");
            Chunks = chunkGo.AddComponent<ChunkManager>();
            Chunks.Init(Gen, new VoxelStore());
            Chunks.ViewRadius = Settings.ViewRadius;

            var envGo = new GameObject("Omgeving");
            Environment = envGo.AddComponent<EnvironmentController>();
            Environment.Init(Clock);

            Campfires = new GameObject("Kampvuren").AddComponent<Campfires>();
            Campfires.Init(Chunks);

            var cam = CreateCamera();
            Player = new GameObject("Speler").AddComponent<PlayerController>();
            Hud = gameObject.AddComponent<Hud>();
            Hud.Init(this);
            Player.Init(this, cam);
            Campfires.Follow = Player.transform;

            if (!TryLoad()) NewGame();
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
            Clock.Time = 7.0 / 24.0;
            Inventory.Add("pijp", 1);
            Inventory.Add("water", 1);
            Inventory.Add("bonen", 1);
            Inventory.Add("verband", 2);
            Inventory.Add("batterij", 1);
            Player.Selected = 0;
            Player.FlashBattery = 1f;
            Player.RefreshTool();
            SpawnAtStart(false);
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
            Stats = new Survival();
            Inventory = new Inventory();
            Inventory.Add("water", 1);
            Inventory.Add("verband", 1);
            Player.Flashlight = false;
            Player.RefreshTool();
            SpawnAtStart(false);
            Hud.Message("Je wordt wakker in de straten van " + Gen.GetCity(0, 0).Name + ". Je spullen ben je kwijt.");
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Chunks.ViewRadius = Settings.ViewRadius;
            Chunks.Tick(Player.transform.position);
            if (!Hud.Paused) Clock.Tick(dt);

            if (!spawned && Chunks.IsLoaded(new Vector3(pendingSpawn.X, 0, pendingSpawn.Z)))
            {
                if (!pendingFromSave) pendingSpawn.Y = PlayerController.GroundAt(Chunks.Store, pendingSpawn.X, pendingSpawn.Z) + 0.05f;
                Player.Teleport(pendingSpawn);
                spawned = true;
            }

            // stadsnaam tonen als je een stad binnenloopt
            int vx = Mathf.FloorToInt(Player.Pos.X / World.VoxelSize), vz = Mathf.FloorToInt(Player.Pos.Z / World.VoxelSize);
            var city = Gen.CityAt(vx, vz, out float d, out _);
            string name = city != null && d < city.R ? city.Name : null;
            if (name != lastCity)
            {
                if (name != null) Hud.Banner(name, city.Kind == "capital" ? "hoofdstad" : city.Kind);
                lastCity = name;
            }

            autosave -= dt;
            if (autosave <= 0 && spawned && !Stats.Dead) { autosave = 60f; Save(); }
        }

        public bool Ready => spawned;

        void OnApplicationQuit()
        {
            if (spawned && !Stats.Dead) Save();
        }

        // ------------------------------------------------------------ opslaan
        const int SaveVersion = 1;

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
                if (r.ReadInt32() != SaveVersion) return false;
                if (r.ReadInt32() != Seed) return false;
                var p = new V3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                Player.Yaw = r.ReadSingle(); Player.Pitch = r.ReadSingle();
                Player.FlashBattery = r.ReadSingle();
                Clock.Time = r.ReadDouble();
                Stats = new Survival(); Stats.Read(r);
                Inventory = new Inventory(); Inventory.Read(r);
                Chunks.Store.ReadEdits(r);
                Chunks.ReloadAll();
                pendingSpawn = p; pendingFromSave = true; spawned = false;
                Player.Teleport(p);
                Player.RefreshTool();
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
