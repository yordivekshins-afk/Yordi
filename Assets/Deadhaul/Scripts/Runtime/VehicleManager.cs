using System.Collections.Generic;
using System.IO;
using Deadhaul.Core;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Deadhaul
{
    /// <summary>
    /// Auto's en boten in de wereld: laden per chunk, voxelmodellen met draaiende wielen en koplampen,
    /// rijden, botsen, overrijden, motorgeluid en opslaan.
    /// </summary>
    public sealed class VehicleManager : MonoBehaviour
    {
        const float U = 0.25f;

        sealed class View
        {
            public Vehicle V;
            public Transform Root;
            public Transform[] Wheels;
            public Light[] Lamps;
            public AudioSource Engine;
            public float Spin;
        }

        GameState game;
        readonly Dictionary<long, Vehicle> known = new Dictionary<long, Vehicle>();
        readonly Dictionary<long, View> views = new Dictionary<long, View>();
        readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();
        AudioClip engineClip, hornClip;
        float cullTimer;
        public bool Lights = true;

        public void Init(GameState g)
        {
            game = g;
            g.Chunks.ChunkLoaded += OnChunkLoaded;
            engineClip = MakeEngineClip();
            hornClip = MakeHornClip();
        }

        void OnChunkLoaded(int cx, int cz)
        {
            foreach (var sp in game.Gen.VehicleSpawns(cx, cz))
                if (!known.ContainsKey(sp.Key)) known[sp.Key] = Vehicles.Create(sp);
        }

        public IEnumerable<Vehicle> All => known.Values;

        void Update()
        {
            if (game == null || !game.Ready) return;
            float dt = Time.deltaTime;
            cullTimer -= dt;
            if (cullTimer <= 0)
            {
                cullTimer = 0.5f;
                var pp = game.Player.Pos;
                foreach (var v in known.Values)
                {
                    float dx = v.Pos.X - pp.X, dz = v.Pos.Z - pp.Z;
                    bool near = dx * dx + dz * dz < 150f * 150f && game.Chunks.IsLoaded(new Vector3(v.Pos.X, 0, v.Pos.Z));
                    bool has = views.ContainsKey(v.Key);
                    if (near && !has) views[v.Key] = BuildView(v);
                    else if (!near && has && !v.Occupied) { Destroy(views[v.Key].Root.gameObject); views.Remove(v.Key); }
                }
            }
            bool night = game.Clock.Ambient < 0.45f;
            foreach (var view in views.Values)
            {
                var v = view.V;
                view.Root.position = CombatSystem.ToV(v.Pos);
                view.Root.rotation = Quaternion.Euler(v.Pitch, v.Yaw, v.Roll);
                if (view.Wheels != null)
                {
                    view.Spin += v.Speed * dt / 0.32f * Mathf.Rad2Deg;
                    for (int i = 0; i < view.Wheels.Length; i++)
                        view.Wheels[i].localRotation = Quaternion.Euler(view.Spin, i < 2 ? v.Steer * 28f : 0, 0);
                }
                bool running = v.Occupied && v.Drivable;
                if (view.Lamps != null) foreach (var l in view.Lamps) l.enabled = running && Lights && night;
                if (view.Engine)
                {
                    if (running && !view.Engine.isPlaying) view.Engine.Play();
                    if (!running && view.Engine.isPlaying) view.Engine.Stop();
                    view.Engine.pitch = 0.7f + Mathf.Abs(v.Speed) / v.Def.MaxSpeed * 1.4f;
                    view.Engine.volume = v.Def.Type == VehicleType.Roeiboot ? 0 : 0.55f;
                }
            }
        }

        /// <summary>Het voertuig waar de speler naar kijkt (binnen bereik).</summary>
        public Vehicle LookedAt(Vector3 eye, Vector3 dir, float range = 4.5f)
        {
            Vehicle best = null; float bestD = range;
            foreach (var view in views.Values)
            {
                var v = view.V;
                var c = CombatSystem.ToV(v.Pos) + Vector3.up * v.Def.Height * 0.5f;
                float d = Vector3.Distance(eye, c) - v.Def.Length * 0.35f;
                if (d < bestD && Vector3.Dot((c - eye).normalized, dir) > 0.5f) { best = v; bestD = d; }
            }
            return best;
        }

        /// <summary>Rijdt het voertuig een stap en verwerkt botsingen en aanrijdingen.</summary>
        public void Drive(Vehicle v, VehiclePhysics.Input input, float dt)
        {
            VehiclePhysics.Step(v, input, dt, game.Chunks.Store, out float crash);
            if (crash > 8f)
            {
                v.Health = Mathf.Max(0, v.Health - (crash - 8f) * 4f);
                if (crash > 11f) game.Stats.Hurt((crash - 11f) * 2.2f, 0.25f, new System.Random());
                Sfx.Instance.Play("slag", CombatSystem.ToV(v.Pos), 1f, 0.5f);
                Fx.Instance.Burst(CombatSystem.ToV(v.Pos) + Vector3.up, Vector3.up, B.Spark, 12, 5, 0.03f, 0.4f);
                if (v.Health <= 0) game.Hud.Message("De motor is kapot. Dit voertuig rijdt niet meer.");
            }
            if (Mathf.Abs(v.Speed) > 3f && v.Def.Type != VehicleType.Roeiboot) game.Combat.Noise.Emit(v.Pos, 45f + Mathf.Abs(v.Speed) * 2f, game.Combat.PlayerActor.Id);
            // aanrijdingen
            if (Mathf.Abs(v.Speed) > 5f)
            {
                float yr = v.Yaw * Mathf.Deg2Rad;
                var nose = new V3(v.Pos.X + Mathf.Sin(yr) * v.Def.Length * 0.5f * Mathf.Sign(v.Speed), v.Pos.Y, v.Pos.Z + Mathf.Cos(yr) * v.Def.Length * 0.5f * Mathf.Sign(v.Speed));
                foreach (var a in game.Combat.Actors.All)
                {
                    if (a == game.Combat.PlayerActor || !a.Alive) continue;
                    float dx = a.Pos.X - nose.X, dz = a.Pos.Z - nose.Z;
                    if (dx * dx + dz * dz > (v.Def.Width * 0.6f + a.Radius) * (v.Def.Width * 0.6f + a.Radius) || Mathf.Abs(a.Pos.Y - v.Pos.Y) > 2) continue;
                    a.TakeDamage(Mathf.Abs(v.Speed) * 5f, HitZone.Romp, game.Combat.PlayerActor.Id);
                    Fx.Instance.Blood(CombatSystem.ToV(a.Chest), Vector3.up, 14);
                    v.Speed *= 0.8f;
                }
            }
        }

        /// <summary>Na een reparatie het model opnieuw opbouwen (banden, kleur).</summary>
        public void Refresh(Vehicle v)
        {
            if (!views.TryGetValue(v.Key, out var view)) return;
            Destroy(view.Root.gameObject);
            views[v.Key] = BuildView(v);
        }

        public void Horn(Vehicle v)
        {
            if (!views.TryGetValue(v.Key, out var view)) return;
            var src = view.Root.GetComponent<AudioSource>();
            if (src) src.PlayOneShot(hornClip, 0.8f);
            game.Combat.Noise.Emit(v.Pos, 120, game.Combat.PlayerActor.Id);
        }

        // ------------------------------------------------ modellen
        View BuildView(Vehicle v)
        {
            var view = new View { V = v };
            var root = new GameObject(v.Def.Name).transform;
            root.SetParent(transform, false);
            view.Root = root;
            string key = v.Def.Type + "|" + v.Paint + "|" + (v.Health <= 0 ? "x" : "");
            if (!meshCache.TryGetValue(key, out var mesh)) meshCache[key] = mesh = BodyMesh(v);
            var body = new GameObject("Carrosserie");
            body.transform.SetParent(root, false);
            body.AddComponent<MeshFilter>().sharedMesh = mesh;
            body.AddComponent<MeshRenderer>().sharedMaterial = VoxelAssets.VoxelMaterial;
            if (!v.Def.Boat)
            {
                if (!meshCache.TryGetValue("wiel", out var wheel)) meshCache["wiel"] = wheel = WheelMesh();
                float hw = v.Def.Width * 0.5f - 0.12f, hl = v.Def.Length * 0.33f;
                var positions = new[] { new Vector3(-hw, 0.32f, hl), new Vector3(hw, 0.32f, hl), new Vector3(-hw, 0.32f, -hl), new Vector3(hw, 0.32f, -hl) };
                view.Wheels = new Transform[4];
                for (int i = 0; i < 4; i++)
                {
                    var w = new GameObject("Wiel").transform;
                    w.SetParent(root, false);
                    w.localPosition = positions[i];
                    w.gameObject.AddComponent<MeshFilter>().sharedMesh = wheel;
                    w.gameObject.AddComponent<MeshRenderer>().sharedMaterial = VoxelAssets.VoxelMaterial;
                    // banden die ontbreken tonen we niet
                    w.gameObject.SetActive(!v.Needs(VehiclePart.Banden) || i % 2 == 0);
                    view.Wheels[i] = w;
                }
                view.Lamps = new Light[2];
                for (int i = 0; i < 2; i++)
                {
                    var l = new GameObject("Koplamp");
                    l.transform.SetParent(root, false);
                    l.transform.localPosition = new Vector3(i == 0 ? -0.65f : 0.65f, 0.7f, v.Def.Length * 0.5f + 0.05f);
                    l.transform.localRotation = Quaternion.Euler(8, 0, 0);
                    l.AddHDLight(LightType.Spot);
                    var light = l.GetComponent<Light>();
                    light.spotAngle = 60; light.innerSpotAngle = 30; light.range = 60; light.intensity = 4000; light.color = new Color(1f, 0.95f, 0.85f);
                    light.shadows = i == 0 ? LightShadows.Soft : LightShadows.None;
                    light.enabled = false;
                    view.Lamps[i] = light;
                }
            }
            var src = root.gameObject.AddComponent<AudioSource>();
            src.spatialBlend = 1; src.minDistance = 3; src.maxDistance = 120; src.rolloffMode = AudioRolloffMode.Logarithmic;
            var eng = root.gameObject.AddComponent<AudioSource>();
            eng.clip = engineClip; eng.loop = true; eng.spatialBlend = 1; eng.minDistance = 3; eng.maxDistance = 150; eng.rolloffMode = AudioRolloffMode.Logarithmic;
            view.Engine = eng;
            return view;
        }

        Mesh BodyMesh(Vehicle v)
        {
            var d = v.Def;
            int sx = Mathf.RoundToInt(d.Width / U), sy = Mathf.RoundToInt(d.Height / U) + 1, sz = Mathf.RoundToInt(d.Length / U);
            var g = new byte[sx * sy * sz];
            byte paint = v.Health <= 0 ? B.Rust : v.Paint;
            void Set(int x, int y, int z, byte b) { if (x >= 0 && y >= 0 && z >= 0 && x < sx && y < sy && z < sz) g[x + sx * (y + sy * z)] = b; }
            switch (d.Type)
            {
                case VehicleType.Auto:
                case VehicleType.Pickup:
                {
                    bool pickup = d.Type == VehicleType.Pickup;
                    int cab0 = pickup ? sz - 9 : 5, cab1 = pickup ? sz - 5 : sz - 5;
                    for (int z = 0; z < sz; z++)
                        for (int x = 0; x < sx; x++)
                        {
                            bool edgeZ = z == 0 || z == sz - 1;
                            for (int y = 1; y <= 3; y++)
                            {
                                byte b = paint;
                                if (y == 1 && edgeZ) b = B.Polymer;                                   // bumpers
                                if (y == 2 && z == sz - 1 && (x == 1 || x == sx - 2)) b = B.Lamp;     // koplampen
                                if (y == 2 && z == 0 && (x == 1 || x == sx - 2)) b = B.Bandana;       // achterlichten
                                if (y == 2 && z == sz - 1 && x > 2 && x < sx - 3) b = B.Steel;        // grille
                                if (pickup && y == 3 && z > 0 && z < cab0 - 1 && x > 0 && x < sx - 1) b = 0;   // laadbak
                                Set(x, y, z, b);
                            }
                            if (z >= cab0 && z <= cab1)
                            {
                                bool pillar = z == cab0 || z == cab1 || z == (cab0 + cab1) / 2;
                                for (int y = 4; y <= sy - 2; y++)
                                {
                                    bool side = x == 0 || x == sx - 1;
                                    bool frontBack = z == cab0 || z == cab1;
                                    if (side || frontBack) Set(x, y, z, (pillar && side) || (frontBack && side) ? paint : B.Glass);
                                    else if (y == 4 && (z == cab0 + 1 || z == cab1 - 2) && x > 0 && x < sx - 1) Set(x, y, z, B.Leather);   // stoelen
                                }
                                Set(x, sy - 1, z, paint);                                           // dak
                            }
                            if (pickup && z < cab0 - 1 && (x == 0 || x == sx - 1 || z == 0)) Set(x, 4, z, paint);   // laadbakrand
                        }
                    break;
                }
                case VehicleType.Roeiboot:
                    for (int z = 0; z < sz; z++)
                    {
                        float t = Mathf.Abs(z - (sz - 1) / 2f) / ((sz - 1) / 2f);
                        int half = Mathf.Max(1, Mathf.RoundToInt((sx / 2f) * (1 - t * t * 0.8f)));
                        int c0 = sx / 2 - half, c1 = sx / 2 + half - 1;
                        for (int x = c0; x <= c1; x++)
                        {
                            Set(x, 0, z, B.Planks);
                            if (x == c0 || x == c1 || z == 0 || z == sz - 1) { Set(x, 1, z, B.Planks); Set(x, 2, z, B.Wood); }
                            if ((z == sz / 3 || z == 2 * sz / 3) && x > c0 && x < c1) Set(x, 1, z, B.Wood);   // bankjes
                        }
                    }
                    break;
                default: // motorboot
                    for (int z = 0; z < sz; z++)
                    {
                        float t = z > sz * 0.55f ? (z - sz * 0.55f) / (sz * 0.45f) : 0;
                        int half = Mathf.Max(1, Mathf.RoundToInt((sx / 2f) * (1 - t * t)));
                        int c0 = sx / 2 - half, c1 = sx / 2 + half - 1;
                        for (int x = c0; x <= c1; x++)
                        {
                            Set(x, 0, z, B.CarBlue);
                            Set(x, 1, z, x == c0 || x == c1 || z == 0 || z == sz - 1 ? B.CarWhite : (byte)0);
                            if (x == c0 || x == c1 || z == sz - 1) Set(x, 2, z, B.CarWhite);
                            if (z == (int)(sz * 0.6f) && x > c0 && x < c1) { Set(x, 2, z, B.CarWhite); Set(x, 3, z, B.Glass); }   // voorruit
                            if (z == (int)(sz * 0.55f) && x == sx / 2) Set(x, 2, z, B.Polymer);                                   // stuur
                        }
                    }
                    for (int y = 0; y <= 3; y++) for (int x = sx / 2 - 1; x <= sx / 2; x++) Set(x, y, 0, B.Polymer);           // buitenboordmotor
                    break;
            }
            return VoxelCharacter.ModelMesh(g, sx, sy, sz, U, sx / 2f, 0, sz / 2f, d.Name);
        }

        static Mesh WheelMesh()
        {
            const int n = 5;
            var g = new byte[2 * n * n];
            for (int z = 0; z < n; z++)
                for (int y = 0; y < n; y++)
                {
                    float dy = y - 2, dz = z - 2;
                    float r = Mathf.Sqrt(dy * dy + dz * dz);
                    if (r > 2.4f) continue;
                    byte b = r < 1.1f ? B.Steel : B.Tire;
                    for (int x = 0; x < 2; x++) g[x + 2 * (y + n * z)] = b;
                }
            return VoxelCharacter.ModelMesh(g, 2, n, n, 0.13f, 1, 2.5f, 2.5f, "wiel");
        }

        // ------------------------------------------------ geluid
        static AudioClip MakeEngineClip()
        {
            const int rate = 44100; int n = rate;   // 1 seconde, naadloos
            var d = new float[n];
            var rnd = new System.Random(3);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float f = 55f;
                float s = Mathf.Sin(2 * Mathf.PI * f * t) * 0.5f + Mathf.Sin(2 * Mathf.PI * f * 2 * t) * 0.25f + Mathf.Sign(Mathf.Sin(2 * Mathf.PI * f * 0.5f * t)) * 0.15f;
                d[i] = (s + (float)(rnd.NextDouble() - 0.5) * 0.15f) * 0.5f;
            }
            var c = AudioClip.Create("motor", n, 1, rate, false);
            c.SetData(d, 0);
            return c;
        }

        static AudioClip MakeHornClip()
        {
            const int rate = 44100; int n = rate / 2;
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float env = Mathf.Min(1, t * 40) * Mathf.Min(1, (0.5f - t) * 20);
                d[i] = (Mathf.Sign(Mathf.Sin(2 * Mathf.PI * 415 * t)) * 0.3f + Mathf.Sign(Mathf.Sin(2 * Mathf.PI * 494 * t)) * 0.3f) * env * 0.5f;
            }
            var c = AudioClip.Create("toeter", n, 1, rate, false);
            c.SetData(d, 0);
            return c;
        }

        // ------------------------------------------------ opslaan
        public void Write(BinaryWriter w)
        {
            w.Write(known.Count);
            foreach (var v in known.Values) v.Write(w);
        }

        public void Read(BinaryReader r)
        {
            foreach (var view in views.Values) Destroy(view.Root.gameObject);
            views.Clear(); known.Clear();
            int n = r.ReadInt32();
            for (int i = 0; i < n; i++) { var v = Vehicle.Read(r); known[v.Key] = v; }
        }

        public void Clear()
        {
            foreach (var view in views.Values) Destroy(view.Root.gameObject);
            views.Clear(); known.Clear();
        }
    }
}
