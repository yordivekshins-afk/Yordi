using System.Collections.Generic;
using Deadhaul.Core;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Deadhaul
{
    /// <summary>
    /// Je eigen basis: generatoren en zonnepanelen voeden bouwlampen, opslagkisten bewaren je spullen,
    /// in je bed slaap je de nacht door en daar word je na je dood wakker. Generatoren maken lawaai
    /// (vijanden horen ze) en echte lampen verlichten de omgeving.
    /// </summary>
    public sealed class BaseSystem : MonoBehaviour
    {
        GameState game;
        public BaseState State { get; private set; } = new BaseState();
        PowerGrid Grid => State.Power;
        readonly HashSet<long> lit = new HashSet<long>();
        readonly List<(int x, int y, int z, byte b)> edits = new List<(int, int, int, byte)>();
        readonly List<Light> lights = new List<Light>();
        readonly List<(Vector3 p, float d)> litNear = new List<(Vector3, float)>();
        AudioSource hum;
        float timer, noiseTimer;

        public void Init(GameState g)
        {
            game = g;
            g.Chunks.ChunkLoaded += OnChunkLoaded;
            g.Chunks.BlockChanged += OnBlockChanged;
            for (int i = 0; i < 8; i++)
            {
                var go = new GameObject("Bouwlamplicht");
                go.transform.SetParent(transform, false);
                go.AddHDLight(LightType.Point);
                var l = go.GetComponent<Light>();
                l.color = new Color(1f, 0.9f, 0.72f);
                l.range = 14f;
                l.intensity = 1800f;
                l.shadows = LightShadows.None;
                l.enabled = false;
                lights.Add(l);
            }
            hum = gameObject.AddComponent<AudioSource>();
            hum.clip = GeneratorClip(); hum.loop = true; hum.spatialBlend = 1f; hum.minDistance = 3f; hum.maxDistance = 70f; hum.volume = 0; hum.Play();
        }

        public void Reset(BaseState s) { State = s ?? new BaseState(); lit.Clear(); }

        static bool IsLamp(byte b) => b == B.WorkLamp || b == B.WorkLampOn;

        void OnChunkLoaded(int cx, int cz)
        {
            var pad = game.Chunks.Store.GetPadded(cx, cz);
            if (pad == null) return;
            for (int lz = 0; lz < World.ChunkSize; lz++)
                for (int lx = 0; lx < World.ChunkSize; lx++)
                    for (int y = 1; y < World.Height; y++)
                    {
                        byte b = pad[World.IdxPad(lx + 1, y, lz + 1)];
                        if (b < B.Generator || b > B.WorkLampOn) continue;
                        int x = cx * World.ChunkSize + lx, z = cz * World.ChunkSize + lz;
                        if (b == B.Generator || b == B.SolarPanel) Grid.AddSource(x, y, z, b == B.Generator);
                        else Grid.AddLamp(x, y, z);
                    }
        }

        void OnBlockChanged(int x, int y, int z, byte b)
        {
            long k = VoxelStore.VoxelKey(x, y, z);
            if (b == B.Generator || b == B.SolarPanel) { Grid.AddSource(x, y, z, b == B.Generator); return; }
            if (IsLamp(b)) { Grid.Lamps[k] = (x, y, z); Grid.Sources.Remove(k); return; }
            if (Grid.Sources.ContainsKey(k) || Grid.Lamps.ContainsKey(k)) Grid.Remove(k);
        }

        void Update()
        {
            if (game == null || !game.Ready) return;
            float dt = Time.deltaTime;
            timer -= dt;
            if (timer <= 0)
            {
                float step = 0.5f - timer;
                timer = 0.5f;
                float hours = game.InMenu ? 0 : step * 24f / GameClock.RealSecondsPerDay;
                float h = game.Clock.HourOfDay;
                bool night = h < 6.3f || h > 19.7f;
                float sun = Mathf.Clamp01((game.Clock.Ambient - 0.45f) / 0.35f) * (1f - 0.7f * game.Now.Cloud);
                Grid.Tick(hours, sun, night, lit);
                ApplyLamps();
            }
            UpdateLights();
            UpdateHum(dt);
        }

        void ApplyLamps()
        {
            edits.Clear();
            var store = game.Chunks.Store;
            foreach (var kv in Grid.Lamps)
            {
                var (x, y, z) = kv.Value;
                if (!store.IsLoaded(VoxelStore.ChunkOf(x), VoxelStore.ChunkOf(z))) continue;
                byte cur = store.Get(x, y, z);
                if (!IsLamp(cur)) continue;
                byte want = lit.Contains(kv.Key) ? B.WorkLampOn : B.WorkLamp;
                if (cur != want) edits.Add((x, y, z, want));
            }
            if (edits.Count > 0) game.Chunks.SetBlocks(edits);
        }

        /// <summary>Echte lichtbronnen bij de brandende lampen die het dichtst bij de camera zijn.</summary>
        void UpdateLights()
        {
            litNear.Clear();
            var cam = game.Player.Cam.transform.position;
            foreach (var k in lit)
            {
                if (!Grid.Lamps.TryGetValue(k, out var p)) continue;
                var w = new Vector3((p.x + 0.5f) * World.VoxelSize, (p.y + 0.5f) * World.VoxelSize, (p.z + 0.5f) * World.VoxelSize);
                float d = (w - cam).sqrMagnitude;
                if (d < 60f * 60f) litNear.Add((w, d));
            }
            litNear.Sort((a, b) => a.d.CompareTo(b.d));
            for (int i = 0; i < lights.Count; i++)
            {
                bool on = i < litNear.Count;
                lights[i].enabled = on;
                if (on) lights[i].transform.position = litNear[i].p + Vector3.down * 0.4f;
            }
        }

        /// <summary>Brommende generator in de buurt; vijanden horen hem ook.</summary>
        void UpdateHum(float dt)
        {
            PowerGrid.Source best = null; float bestD = float.MaxValue;
            var pp = game.Player.transform.position;
            foreach (var s in Grid.Sources.Values)
            {
                if (!s.Generator || !s.Active) continue;
                var w = new Vector3((s.X + 0.5f) * World.VoxelSize, (s.Y + 0.5f) * World.VoxelSize, (s.Z + 0.5f) * World.VoxelSize);
                float d = (w - pp).sqrMagnitude;
                if (d < bestD) { bestD = d; best = s; }
            }
            noiseTimer -= dt;
            if (best != null && bestD < 80f * 80f)
            {
                var w = new Vector3((best.X + 0.5f) * World.VoxelSize, (best.Y + 0.5f) * World.VoxelSize, (best.Z + 0.5f) * World.VoxelSize);
                hum.transform.position = w;
                hum.volume = Mathf.MoveTowards(hum.volume, 0.6f, dt);
                if (noiseTimer <= 0) { noiseTimer = 1.5f; game.Combat.Noise.Emit(new V3(w.x, w.y, w.z), 40f, -1); }
            }
            else hum.volume = Mathf.MoveTowards(hum.volume, 0f, dt);
        }

        // ------------------------------------------------ interactie
        /// <summary>E op een basisblok. True als het afgehandeld is.</summary>
        public bool Interact(int x, int y, int z, byte b)
        {
            var hud = game.Hud; var inv = game.Inventory;
            long k = VoxelStore.VoxelKey(x, y, z);
            switch (b)
            {
                case B.Generator:
                {
                    var s = Grid.AddSource(x, y, z, true);
                    var ch = new List<Hud.Choice>
                    {
                        new Hud.Choice("Tanken met jerrycan (+20 l)", () => { inv.Remove("jerrycan", 1); inv.Add("jerrycan_leeg", 1); s.Fuel = Mathf.Min(60f, s.Fuel + 20f); hud.Message("De generator is bijgevuld."); },
                            inv.Count("jerrycan") > 0 && s.Fuel < 41f),
                        new Hud.Choice(s.Running ? "Uitzetten" : "Aanzetten", () => { s.Running = !s.Running; hud.Message(s.Running ? "De generator slaat brullend aan." : "De generator valt stil."); }, true),
                        new Hud.Choice("Sluiten", null),
                    };
                    hud.OpenDialog("Generator", $"Brandstof: {s.Fuel:0.0} / 60 l  ({s.Fuel / PowerGrid.FuelPerHour:0} speluren)\n{(s.Running ? (s.Fuel > 0 ? $"Draait en voedt {s.Served} lampen." : "Staat aan, maar de tank is leeg.") : "Staat uit.")}  Hoorbaar tot 40 m.", ch);
                    return true;
                }
                case B.SolarPanel:
                {
                    var s = Grid.AddSource(x, y, z, false);
                    hud.Message($"Zonnepaneel: {s.Charge / PowerGrid.SolarCapacity * 100:0}% geladen, voedt {s.Served} lampen.");
                    return true;
                }
                case B.WorkLamp:
                case B.WorkLampOn:
                    hud.Message(b == B.WorkLampOn ? "De lamp brandt." : "Geen stroom (binnen 30 m) of het is nog dag.");
                    return true;
                case B.Distiller:
                {
                    var ch = new List<Hud.Choice>
                    {
                        new Hud.Choice("Biodiesel stoken", () =>
                        {
                            var err = BaseState.Distill(inv);
                            if (err != null) hud.Message(err);
                            else { hud.Message("De ketel borrelt. Je vult een jerrycan met 20 l biodiesel."); Sfx.Instance.Play("slag", game.Player.transform.position, 0.3f, 0.6f); }
                        }, inv.Count("jerrycan_leeg") > 0 && (inv.Count("frituurvet") >= 4 || (inv.Count("mais") >= 6 && inv.Count("water") >= 2))),
                        new Hud.Choice("Sluiten", null),
                    };
                    hud.OpenDialog("Destilleerketel", $"Nodig: een lege jerrycan en 4 frituurvet, of 6 maïs en 2 water.\nJe hebt: {inv.Count("jerrycan_leeg")} lege jerrycan(s), {inv.Count("frituurvet")} frituurvet, {inv.Count("mais")} maïs, {inv.Count("water")} water.", ch);
                    return true;
                }
                case B.StorageChest:
                    hud.OpenStorage(State.Chest(x, y, z), "opslagkist");
                    return true;
                case B.Bed:
                {
                    float h = game.Clock.HourOfDay;
                    bool night = h >= 19.5f || h < 5.5f;
                    int threats = game.Combat.HostilesNear(40f);
                    var pos = new V3((x + 0.5f) * World.VoxelSize, (y + 1) * World.VoxelSize + 0.05f, (z + 0.5f) * World.VoxelSize);
                    var ch = new List<Hud.Choice>
                    {
                        new Hud.Choice("Slapen tot de ochtend", () => Sleep(), night && threats == 0),
                        new Hud.Choice("Hier wakker worden als je sterft", () => { State.HasBed = true; State.BedSpawn = pos; hud.Message("Dit is nu je bed."); }),
                        new Hud.Choice("Sluiten", null),
                    };
                    string why = !night ? "Het is nog geen tijd om te slapen." : threats > 0 ? "Er zijn vijanden in de buurt." : "Je kunt de nacht doorslapen.";
                    hud.OpenDialog("Bed", why + (State.HasBed ? "\nJe hebt al een bed als thuisbasis." : ""), ch);
                    return true;
                }
            }
            return false;
        }

        void Sleep()
        {
            var c = game.Clock;
            double day = System.Math.Floor(c.Time + (c.HourOfDay >= 19f ? 1 : 0));
            double wake = day + 7.0 / 24.0;
            float hours = (float)((wake - c.Time) * 24.0);
            c.Time = wake;
            var st = game.Stats;
            st.Food = Mathf.Max(5, st.Food - hours * 2f);
            st.Water = Mathf.Max(5, st.Water - hours * 3f);
            st.Health = Mathf.Min(100, st.Health + hours * 2.5f);
            st.Stamina = 100;
            game.Hud.Banner("Een nieuwe dag", $"je sliep {hours:0} uur");
        }

        /// <summary>Mag dit blok gesloopt worden? Een volle kist niet.</summary>
        public bool CanBreak(int x, int y, int z, byte b)
        {
            if (b != B.StorageChest) return true;
            var list = State.Chest(x, y, z);
            if (list.Count == 0) { State.TakeChest(x, y, z); return true; }
            game.Hud.Message("Maak de kist eerst leeg.");
            return false;
        }

        static AudioClip GeneratorClip()
        {
            const int rate = 44100;
            int n = rate * 2;
            var d = new float[n];
            var rnd = new System.Random(3);
            float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                lp += ((float)rnd.NextDouble() * 2 - 1 - lp) * 0.08f;
                float thump = Mathf.Sin(2 * Mathf.PI * 30f * t) * 0.5f + Mathf.Sin(2 * Mathf.PI * 60f * t) * 0.3f + Mathf.Sin(2 * Mathf.PI * 120f * t) * 0.12f;
                float pulse = 0.75f + 0.25f * Mathf.Sin(2 * Mathf.PI * 15f * t);
                d[i] = (thump * pulse + lp * 0.6f) * 0.5f;
            }
            var clip = AudioClip.Create("generator", n, 1, rate, false);
            clip.SetData(d, 0);
            return clip;
        }
    }
}
