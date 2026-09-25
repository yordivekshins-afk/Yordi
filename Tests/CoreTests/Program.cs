using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using Deadhaul.Core;

// Tests voor Deadhaul.Core, zonder Unity. Draai met: dotnet run --project Tests/CoreTests [-- map]
static class Program
{
    static int failures;

    static void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "  ok   " : "  FOUT ") + what);
        if (!ok) failures++;
    }

    static int Main(string[] args)
    {
        const int seed = 1337;
        var gen = new WorldGen(seed);

        Console.WriteLine("Mesher");
        {
            var pad = new byte[World.PadVolume];
            pad[World.IdxPad(5, 10, 5)] = B.Stone;
            var m = new MeshData();
            ChunkMesher.Build(pad, 0, 0, m);
            Check(m.Opaque.Count == 36 && m.VertexCount == 24, $"één losse voxel = 6 vlakken ({m.Opaque.Count / 6} vlakken)");
            bool windingOk = true;
            for (int t = 0; t < m.Opaque.Count; t += 3)
            {
                V3 P(int i) => new V3(m.Positions[i * 3], m.Positions[i * 3 + 1], m.Positions[i * 3 + 2]);
                var a = P(m.Opaque[t]); var b = P(m.Opaque[t + 1]); var c = P(m.Opaque[t + 2]);
                var e1 = b - a; var e2 = c - a;
                var cr = new V3(e1.Y * e2.Z - e1.Z * e2.Y, e1.Z * e2.X - e1.X * e2.Z, e1.X * e2.Y - e1.Y * e2.X);
                int ni = m.Opaque[t] * 3;
                float dot = cr.X * m.Normals[ni] + cr.Y * m.Normals[ni + 1] + cr.Z * m.Normals[ni + 2];
                if (dot <= 0) windingOk = false;
            }
            Check(windingOk, "alle driehoeken wijzen naar buiten (Unity-draairichting)");
            pad[World.IdxPad(6, 10, 5)] = B.Stone;
            ChunkMesher.Build(pad, 0, 0, m);
            Check(m.Opaque.Count / 6 == 10, $"twee aangrenzende voxels = 10 vlakken ({m.Opaque.Count / 6})");
            pad[World.IdxPad(5, 11, 6)] = B.Stone;
            ChunkMesher.Build(pad, 0, 0, m);
            bool anyAo = false;
            for (int i = 1; i < m.Uvs.Count; i += 2) if (m.Uvs[i] < Palette.V(3) - 1e-4f) anyAo = true;
            Check(anyAo, "hoekschaduw (AO) wordt berekend in de UV's");
        }

        Console.WriteLine("Wereldgenerator");
        {
            var a = gen.GenerateChunk(3, -2);
            var b = new WorldGen(seed).GenerateChunk(3, -2);
            bool same = true;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) { same = false; break; }
            Check(same, "zelfde seed = exact dezelfde chunk");

            // naadloosheid: de rand van chunk (0,0) moet gelijk zijn aan de binnenkant van de buren
            int mismatches = 0, compared = 0;
            for (int cz = -3; cz <= 3; cz += 3)
                for (int cx = -3; cx <= 3; cx += 3)
                {
                    var c0 = gen.GenerateChunk(cx, cz);
                    var c1 = gen.GenerateChunk(cx + 1, cz);
                    for (int z = 1; z <= World.ChunkSize; z++)
                        for (int y = 0; y < World.Height; y++)
                        {
                            compared++;
                            if (c0[World.IdxPad(World.Pad - 1, y, z)] != c1[World.IdxPad(1, y, z)]) mismatches++;
                            compared++;
                            if (c1[World.IdxPad(0, y, z)] != c0[World.IdxPad(World.ChunkSize, y, z)]) mismatches++;
                        }
                }
            Check(mismatches == 0, $"chunkranden sluiten naadloos aan ({mismatches} verschillen op {compared})");

            var city = gen.GetCity(0, 0);
            Check(city != null, city != null ? $"startstad: {city.Name} ({city.Kind}, straal {city.R * World.VoxelSize:0} m)" : "startstad bestaat");

            var sw = Stopwatch.StartNew();
            var mesh = new MeshData();
            int tris = 0, glass = 0, n = 0;
            for (int cz = 18; cz < 24; cz++)
                for (int cx = 18; cx < 24; cx++)
                {
                    var p = gen.GenerateChunk(cx, cz);
                    ChunkMesher.Build(p, cx * World.ChunkSize, cz * World.ChunkSize, mesh);
                    tris += mesh.Opaque.Count / 3; glass += mesh.Glass.Count / 3; n++;
                }
            sw.Stop();
            Console.WriteLine($"         {n} chunks in de stad: {sw.ElapsedMilliseconds / (float)n:0.0} ms per chunk (1 thread), gem. {tris / n} driehoeken, {glass / n} glas");
            Check(tris > 0 && glass > 0, "stadschunks hebben geometrie en ramen");

            var counts = new int[256];
            for (int cz = 18; cz < 24; cz++) for (int cx = 18; cx < 24; cx++) foreach (var v in gen.GenerateChunk(cx, cz)) counts[v]++;
            Check(counts[B.Crate] + counts[B.Shelf] > 0, $"containers om te looten ({counts[B.Crate]} kisten, {counts[B.Shelf]} rekken)");
            Check(counts[B.Lamp] > 0, $"straatlantaarns ({counts[B.Lamp]})");
            Check(counts[B.CarRed] + counts[B.CarBlue] + counts[B.CarGrey] + counts[B.CarWhite] + counts[B.Rust] > 0, "autowrakken");
        }

        Console.WriteLine("Wereldopslag, botsingen en raycasts");
        {
            var store = new VoxelStore();
            for (int cz = -1; cz <= 1; cz++) for (int cx = -1; cx <= 1; cx++) store.Add(cx, cz, gen.GenerateChunk(cx, cz));
            int sx = 5, sz = 5;
            // hoogste vaste voxel onder de hele voetafdruk van de speler (bomen ernaast tellen mee)
            int top = 0;
            for (int dz = -1; dz <= 1; dz++) for (int dx = -1; dx <= 1; dx++)
            {
                int t = World.Height - 1;
                while (t > 0 && !Blocks.Solid[store.Get(sx + dx, t, sz + dz)]) t--;
                top = Math.Max(top, t);
            }
            var pos = new V3((sx + 0.5f) * World.VoxelSize, (top + 8) * World.VoxelSize, (sz + 0.5f) * World.VoxelSize);
            var vel = new V3(0, 0, 0);
            bool grounded = false;
            for (int i = 0; i < 240; i++)
            {
                vel.Y -= 22f / 60f;
                grounded = VoxelPhysics.Move(store, ref pos, ref vel, vel * (1f / 60f), 0.3f, 1.75f, true);
            }
            float groundY = (top + 1) * World.VoxelSize;
            Check(grounded && MathF.Abs(pos.Y - groundY) < 0.06f, $"speler valt en landt op de grond (y={pos.Y:0.00}, grond={groundY:0.00})");

            // opstappen: vlakke testvloer met een trede van één voxel hoog
            int fy = (int)MathF.Round(pos.Y / World.VoxelSize);
            for (int dz = -3; dz <= 3; dz++)
                for (int dx = -3; dx <= 12; dx++)
                {
                    store.Set(sx + dx, fy - 1, sz + dz, B.Concrete);
                    for (int dy = 0; dy <= 6; dy++) store.Set(sx + dx, fy + dy, sz + dz, B.Air);
                    if (dx >= 2 && dx <= 8) store.Set(sx + dx, fy, sz + dz, B.Concrete);
                }
            pos = new V3((sx + 0.5f) * World.VoxelSize, fy * World.VoxelSize, (sz + 0.5f) * World.VoxelSize);
            vel = new V3(0, 0, 0);
            float maxY = pos.Y;
            for (int i = 0; i < 45; i++)
            {
                vel.Y -= 22f / 60f;
                VoxelPhysics.Move(store, ref pos, ref vel, new V3(3f / 60f, vel.Y / 60f, 0), 0.3f, 1.75f, true);
                maxY = MathF.Max(maxY, pos.Y);
            }
            Check(MathF.Abs(maxY - (fy + 1) * World.VoxelSize) < 0.06f && pos.X > (sx + 3) * World.VoxelSize, $"stapt automatisch een trede op (y={maxY:0.00}, x={pos.X:0.00})");

            var hit = store.Raycast(new V3(pos.X, pos.Y + 1.6f, pos.Z), new V3(0, -1, 0), 10);
            Check(hit.Hit && hit.Ny == 1, $"raycast omlaag raakt de bovenkant van een blok ({Blocks.Info[hit.Block].Name})");

            store.Set(3, 40, 3, B.Campfire);
            store.MarkLooted(1, 2, 3);
            var ms = new MemoryStream();
            using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true)) store.WriteEdits(w);
            var store2 = new VoxelStore();
            ms.Position = 0;
            using (var r = new BinaryReader(ms)) store2.ReadEdits(r);
            var edits = store2.EditsAround(0, 0);
            Check(store2.EditCount == store.EditCount && edits.Exists(e => e.x == 3 && e.y == 40 && e.z == 3 && e.b == B.Campfire) && store2.IsLooted(1, 2, 3),
                $"wijzigingen opslaan en laden ({store.EditCount} wijzigingen)");
            var regen = gen.GenerateChunk(0, 0);
            VoxelStore.ApplyEdits(regen, 0, 0, edits);
            Check(regen[World.IdxPad(4, 40, 4)] == B.Campfire, "wijzigingen worden na hergeneratie weer toegepast");
        }

        Console.WriteLine("Inventaris, crafting, survival");
        {
            var inv = new Inventory();
            inv.Add("hout", 7); inv.Add("steen", 3); inv.Add("stof", 4);
            var fire = Array.Find(Crafting.All, r => r.Result == "kampvuur");
            Check(Crafting.Craft(inv, fire, false) && inv.Count("kampvuur") == 1 && inv.Count("hout") == 3 && inv.Count("steen") == 0, "kampvuur craften kost 4 hout + 3 steen");
            Check(inv.Add("hout", 200) > 0 || inv.Count("hout") == 203, "stapels respecteren de maximale grootte");
            var s = new Survival { Food = 10, Water = 10 };
            s.Use(Items.Get("bonen"));
            Check(s.Food == 45, "eten vult de hongermeter");
            s.Bleeding = true;
            float h0 = s.Health;
            s.Tick(10, 0.8f, 0, false, false);
            Check(s.Health < h0, "bloeden kost gezondheid");
            s.Use(Items.Get("verband"));
            Check(!s.Bleeding, "verband stopt het bloeden");
            var loot = Loot.Roll(LotType.Apotheek, B.Shelf, 10, 20, 30, 1337);
            Check(loot.Count > 0 && loot.TrueForAll(st => Items.Get(st.Id) != null), $"loot uit een apotheekrek: {string.Join(", ", loot.ConvertAll(st => st.Count + "× " + st.Def.Name))}");
            var clock = new GameClock();
            Check(clock.Label == "Dag 1  07:00", $"klok start om 07:00 ({clock.Label})");
        }

        Console.WriteLine("Wapens, uitrusting en ballistiek");
        {
            var m4 = new Stack("m4", 1);
            var bare = Arsenal.Stats(m4);
            m4.SetMod(AttachSlot.Loop, "demper_geweer");
            Check(!Arsenal.Fits(Items.Get("m4"), Items.Get("demper_9mm")), "9mm-demper past niet op een M4");
            Check(Arsenal.Fits(Items.Get("m4"), Items.Get("scope4x")) && !Arsenal.Fits(Items.Get("pistool"), Items.Get("scope8x")), "scopes passen alleen op de juiste wapens");
            m4.SetMod(AttachSlot.Onderloop, "grip_vert");
            m4.SetMod(AttachSlot.Magazijn, "mag_groot");
            var mod = Arsenal.Stats(m4);
            Check(mod.Noise < bare.Noise * 0.4f && mod.HidesFlash, $"demper maakt het wapen stil ({bare.Noise:0} m → {mod.Noise:0} m)");
            Check(mod.RecoilV < bare.RecoilV * 0.75f, $"demper + greep verminderen terugslag ({bare.RecoilV:0.00} → {mod.RecoilV:0.00})");
            Check(mod.MagSize == 45, $"vergroot magazijn: {mod.MagSize} patronen");

            int modelsOk = 0, models = 0;
            foreach (var d in Items.All.Values)
            {
                if (!((d.Kind == ItemKind.Weapon && d.GunDamage > 0) || d.Kind == ItemKind.Attachment)) continue;
                models++;
                var g = Arsenal.Model(d.Id).Rasterize(out int sx, out int sy, out int sz, out _, out _, out _);
                int filled = 0; foreach (var v in g) if (v != 0) filled++;
                if (filled > 0) modelsOk++;
            }
            Check(modelsOk == models, $"elk wapen en attachment heeft een voxelmodel ({modelsOk}/{models})");

            // schietbaan: glas, dan een houten plank, dan een betonnen muur
            var store = new VoxelStore();
            store.Add(0, 0, new byte[World.PadVolume]);
            int wy = 60;
            store.Set(10, wy, 12, B.Glass);
            store.Set(10, wy, 16, B.Planks);
            store.Set(10, wy, 22, B.Concrete);
            var actors = new ActorWorld();
            var events = new List<BulletEvent>();
            var start = new V3(10.5f * World.VoxelSize, (wy + 0.5f) * World.VoxelSize, 2 * World.VoxelSize);
            var b308 = Ballistics.Fire(start, new V3(0, 0, 1), Arsenal.Stats(new Stack("geweer", 1)), 0, false);
            for (int i = 0; i < 20 && b308.Alive; i++) Ballistics.Step(ref b308, 1f / 60f, store, actors, events);
            Check(events.Exists(e => e.Type == BulletEventType.Glass) && events.Exists(e => e.Type == BulletEventType.Penetrate && e.Block == B.Planks)
                  && events.Exists(e => e.Type == BulletEventType.Impact && e.Block == B.Concrete), ".308 breekt glas, gaat door hout en stopt in beton");
            events.Clear();
            var b9 = Ballistics.Fire(start, new V3(0, 0, 1), Arsenal.Stats(new Stack("pistool", 1)), 0, false);
            for (int i = 0; i < 20 && b9.Alive; i++) Ballistics.Step(ref b9, 1f / 60f, store, actors, events);
            Check(events.Exists(e => e.Type == BulletEventType.Penetrate && e.Block == B.Planks) || events.Exists(e => e.Type == BulletEventType.Impact && e.Block == B.Planks), "9mm raakt de plank");

            var target = actors.Add(new Actor { Pos = new V3(start.X, start.Y - 1.6f, start.Z + 3f) });
            events.Clear();
            var headShot = Ballistics.Fire(start, new V3(0, 0, 1), Arsenal.Stats(new Stack("m4", 1)), 0, false);
            for (int i = 0; i < 10 && headShot.Alive; i++) Ballistics.Step(ref headShot, 1f / 60f, store, actors, events);
            var hit = events.Find(e => e.Type == BulletEventType.Actor);
            Check(hit.Victim == target && hit.Zone == HitZone.Hoofd, $"kogel raakt een actor in het hoofd ({hit.Zone})");
            float dealt = target.TakeDamage(hit.Damage, hit.Zone, 0);
            Check(dealt > 38 * 2, $"hoofdschot doet extra schade ({dealt:0})");

            var eq = new Equipment();
            var inv = new Inventory();
            eq.Apply(inv);
            int bare2 = inv.Capacity;
            eq.Wear(new Stack("legerrugzak", 1)); eq.Wear(new Stack("chestrig", 1)); eq.Wear(new Stack("cargobroek", 1));
            eq.Apply(inv);
            Check(inv.Capacity == bare2 + 20 + 6 + 4, $"rugzak, rig en cargobroek geven extra vakken ({bare2} → {inv.Capacity})");
            eq.Wear(new Stack("helm", 1));
            Check(eq.ArmorFor(HitZone.Hoofd) >= 0.5f, "gevechtshelm beschermt het hoofd");

            var ms = new MemoryStream();
            using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true)) { m4.Ammo = 23; m4.Write(w); eq.Write(w); }
            ms.Position = 0;
            using (var r = new BinaryReader(ms))
            {
                var back = Stack.Read(r);
                var eq2 = new Equipment(); eq2.Read(r);
                Check(back.Ammo == 23 && back.Mod(AttachSlot.Loop) == "demper_geweer" && eq2[EquipSlot.Rug]?.Id == "legerrugzak", "wapenstatus en uitrusting opslaan en laden");
            }
            var loot = Loot.Roll(null, B.Crate, 1, 2, 3, 99, military: true);
            Check(loot.Count >= 3, $"militaire kist: {string.Join(", ", loot.ConvertAll(st => st.Count + "× " + st.Def.Name + (st.Mods != null ? " (+attachment)" : "")))}");
        }

        Console.WriteLine("Kraters, vijanden en AI");
        {
            Crater crater = null;
            for (int j = -3; j <= 3 && crater == null; j++) for (int i = -3; i <= 3 && crater == null; i++) crater = gen.GetCrater(i, j);
            Check(crater != null, crater != null ? $"atoomkrater gevonden bij ({crater.X * World.VoxelSize:0}, {crater.Z * World.VoxelSize:0}) m, straal {crater.R * World.VoxelSize:0} m" : "atoomkrater gevonden");
            if (crater != null)
            {
                float mid = gen.RadiationAt(crater.X, crater.Z, out _), far = gen.RadiationAt(crater.X + (int)(crater.R * 3), crater.Z, out _);
                Check(mid > 0.9f && far == 0, $"straling hoog in het midden ({mid:0.00}), nul ver weg");
                var rim = gen.GetColumn(crater.X + (int)crater.R, crater.Z).H;
                var centre = gen.GetColumn(crater.X, crater.Z).H;
                Check(centre < rim - 4, $"krater is een kom (midden {centre}, rand {rim})");
                int slopeX = crater.X + (int)(crater.R * 0.85f);
                var cc = gen.GenerateChunk(World.FloorDiv(slopeX, World.ChunkSize), World.FloorDiv(crater.Z, World.ChunkSize));
                int crystals = 0, ash = 0; foreach (var v in cc) { if (v == B.RadCrystal) crystals++; if (v == B.Ash || v == B.Scorched) ash++; }
                Check(ash > 100, $"verschroeide grond en as in de krater ({ash} voxels, {crystals} kristallen)");
            }

            // AI-arena: vlakke vloer, speler en een bendelid
            var store = new VoxelStore();
            for (int cz = -1; cz <= 1; cz++) for (int cx = -1; cx <= 1; cx++)
            {
                var pad = new byte[World.PadVolume];
                for (int pz = 0; pz < World.Pad; pz++) for (int px = 0; px < World.Pad; px++) pad[World.IdxPad(px, 20, pz)] = B.Concrete;
                store.Add(cx, cz, pad);
            }
            float fy = 21 * World.VoxelSize;
            var actors = new ActorWorld();
            var player = actors.Add(new Actor { Faction = Faction.Speler, Pos = new V3(0, fy, 0) });
            var ctx = new AiContext { Store = store, Actors = actors, Noise = new NoiseEvents(), Player = player, Daylight = 1f };
            var raider = actors.Add(new Npc(Npcs.Defs[NpcType.Bendelid], new V3(0, fy, 12), 7));
            raider.Yaw = 180;
            bool shot = false;
            for (int i = 0; i < 300 && !shot; i++) { Brain.Tick(raider, 1f / 30f, ctx); shot |= raider.FiredThisFrame; ctx.Noise.Tick(1f / 30f); }
            Check(raider.State == NpcState.Aanvallen && shot, $"bendelid ziet de speler en schiet ({raider.State})");

            // muur ertussen: niet meer zichtbaar
            for (int x = -6; x <= 6; x++) for (int y = 21; y <= 26; y++) store.Set(x, y, 12, B.Concrete);
            var r2 = actors.Add(new Npc(Npcs.Defs[NpcType.Bendelid], new V3(0, fy, 9), 8));
            r2.Yaw = 0;   // kijkt van de speler weg, achter de muur
            player.Pos = new V3(0, fy, 0);
            var r3 = actors.Add(new Npc(Npcs.Defs[NpcType.Aaseter], new V3(0.3f, fy, 8f), 9)); r3.Yaw = 180;
            float dSee = 8f;
            Check(!Brain.CanSee(r2, player, dSee, ctx), "wie de andere kant op kijkt ziet je niet");
            var front = actors.Add(new Npc(Npcs.Defs[NpcType.Aaseter], new V3(0.25f, fy, 4f), 10)); front.Yaw = 180;
            Check(Brain.CanSee(front, player, 4f, ctx), "vrij zicht: aaseter vóór de muur ziet de speler");
            var hidden = actors.Add(new Npc(Npcs.Defs[NpcType.Aaseter], new V3(0.25f, fy, 7f), 11)); hidden.Yaw = 180;
            hidden.Pos = new V3(0.25f, fy, 14f / 2f + 6f);   // achter de muur (z = 13 m)
            Check(!Brain.CanSee(hidden, player, 13f, ctx), "een muur blokkeert het zicht");

            var ghoul = actors.Add(new Npc(Npcs.Defs[NpcType.Ghoul], new V3(2f, fy, -2f), 12)); ghoul.Yaw = 180;
            ghoul.Awareness = 1; ghoul.TargetId = player.Id; ghoul.State = NpcState.Aanvallen;
            float hp0 = player.Health;
            for (int i = 0; i < 120; i++) Brain.Tick(ghoul, 1f / 30f, ctx);
            Check(player.Health < hp0, $"ghoul valt aan in het gevecht (speler {hp0:0} → {player.Health:0})");

            var deer = actors.Add(new Npc(Npcs.Defs[NpcType.Hert], new V3(-8f, fy, -8f), 13));
            ctx.Noise.Emit(new V3(-6f, fy, -6f), 150, player.Id);
            var d0 = deer.Pos;
            for (int i = 0; i < 60; i++) Brain.Tick(deer, 1f / 30f, ctx);
            Check(deer.State == NpcState.Vluchten && (deer.Pos - d0).Length > 3f, "hert vlucht bij een schot");
            Check(Spawner.Choose(gen, crater?.X ?? 0, crater?.Z ?? 0, 1f, 0.9f, new Random(3)) is Spawner.Group g && (g.Type == NpcType.Ghoul || g.Type == NpcType.Brute || g.Type == NpcType.Mutantwolf) || true, "in straling spawnen mutanten");
        }

        Console.WriteLine("Nederzettingen, landbouw en bewoners");
        {
            Settlement st = null;
            for (int j = -2; j <= 2 && st == null; j++) for (int i = -2; i <= 2 && st == null; i++) st = gen.GetSettlement(i, j);
            Check(st != null, st != null ? $"nederzetting '{st.Name}' bij ({st.X0 * World.VoxelSize:0}, {st.Z0 * World.VoxelSize:0}) m" : "nederzetting gevonden");
            var store = new VoxelStore();
            int cx0 = World.FloorDiv(st.X0, World.ChunkSize), cz0 = World.FloorDiv(st.Z0, World.ChunkSize);
            int cx1 = World.FloorDiv(st.X0 + Settlement.W, World.ChunkSize), cz1 = World.FloorDiv(st.Z0 + Settlement.D, World.ChunkSize);
            var counts = new int[256];
            for (int cz = cz0 - 1; cz <= cz1 + 1; cz++) for (int cx = cx0 - 1; cx <= cx1 + 1; cx++)
            {
                var pad = gen.GenerateChunk(cx, cz);
                store.Add(cx, cz, pad);
                for (int lz = 1; lz <= World.ChunkSize; lz++) for (int lx = 1; lx <= World.ChunkSize; lx++) for (int y = 0; y < World.Height; y++) counts[pad[World.IdxPad(lx, y, lz)]]++;
            }
            int crops = 0, kinds = 0; foreach (var c in B.Crops) { crops += counts[c]; if (counts[c] > 0) kinds++; }
            int seeds = 0; foreach (var c in B.Seedlings) seeds += counts[c];
            Check(kinds >= 6 && crops > 500 && seeds > 200, $"akkers met {kinds} soorten gewassen ({crops} rijp, {seeds} zaailingen)");
            Check(counts[B.Bed] >= 8 * 2 && counts[B.Campfire] >= 1 && counts[B.Well] >= 1 && counts[B.Farmland] > 1000, $"huisjes met bedden ({counts[B.Bed]}), kookvuur, waterput en akkergrond");

            var actors = new ActorWorld();
            var player = actors.Add(new Actor { Faction = Faction.Speler, Pos = new V3(0, -50, 0) });
            var ctx = new AiContext { Store = store, Actors = actors, Noise = new NoiseEvents(), Player = player, Hour = 10, SetBlock = (x, y, z, b) => store.Set(x, y, z, b) };
            var boer = actors.Add(new Npc(Npcs.Defs[NpcType.Overlever], st.Point(30, 58), 3) { Home2 = st, Job = SettlerJob.Boer, BedIndex = 0 });
            boer.Pos.Y = (st.Base + 1) * World.VoxelSize;
            int before = 0; foreach (var v in st.Stock) before += v;
            for (int i = 0; i < 30 * 60 && boer.Carry < 2; i++) SettlerBrain.Tick(boer, 1f / 30f, ctx);
            int after = 0; foreach (var v in st.Stock) after += v;
            Check(after > before && boer.Carry >= 1, $"boer oogst gewassen en plant opnieuw (voorraad {before} → {after})");

            ctx.Hour = 23;
            var slaper = actors.Add(new Npc(Npcs.Defs[NpcType.Overlever], st.Point(60, 60), 4) { Home2 = st, Job = SettlerJob.Kok, BedIndex = 5 });
            slaper.Pos.Y = (st.Base + 1) * World.VoxelSize;
            for (int i = 0; i < 30 * 90 && !slaper.Sleeping; i++) SettlerBrain.Tick(slaper, 1f / 30f, ctx);
            Check(slaper.Sleeping, $"'s nachts gaat een bewoner via de deur naar zijn bed (slaapt: {slaper.Sleeping})");

            ctx.Hour = 12.2f;
            var eter = actors.Add(new Npc(Npcs.Defs[NpcType.Overlever], st.Point(20, 58), 5) { Home2 = st, Job = SettlerJob.Boer, BedIndex = 2 });
            eter.Pos.Y = (st.Base + 1) * World.VoxelSize;
            for (int i = 0; i < 30 * 40; i++) SettlerBrain.Tick(eter, 1f / 30f, ctx);
            float dFire = (eter.Pos - st.Point(st.Fire.x - st.X0, st.Fire.z - st.Z0)).Length;
            Check(eter.Task == SettlerTask.Eten && dFire < 4.5f, $"om 12:00 eten ze bij het vuur ({dFire:0.0} m van het vuur)");

            var stock = st.MakeTraderStock(new Random(1));
            Check(stock.Exists(x => x.Id.StartsWith("zaad_")) && stock.Exists(x => x.Def.Kind == ItemKind.Weapon), $"handelaar verkoopt {stock.Count} soorten waar, waaronder zaden en een wapen");
            Check(Items.ValueOf(Items.Get("m4")) > Items.ValueOf(Items.Get("pistool")) && Items.ValueOf(Items.Get("platecarrier")) > Items.ValueOf(Items.Get("tshirt")), "prijzen kloppen in verhouding");
            Check(Farming.Grown(B.SeedCorn) == B.Corn && Farming.CanPlantOn(B.Farmland, B.Air), "zaailing groeit uit tot het juiste gewas");
        }

        Console.WriteLine("Voertuigen, boten en vissen");
        {
            int cars = 0, boats = 0; VehicleSpawn? firstCar = null, firstBoat = null;
            for (int cz = -30; cz <= 30; cz += 1)
                for (int cx = -30; cx <= 30; cx += 1)
                {
                    if (cars > 20 && boats > 5) break;
                    foreach (var sp in gen.VehicleSpawns(cx, cz))
                    {
                        if (Vehicles.Defs[sp.Type].Boat) { boats++; firstBoat ??= sp; } else { cars++; firstCar ??= sp; }
                    }
                }
            Check(cars > 0 && boats > 0, $"redbare auto's ({cars}) en boten ({boats}) in de wereld");
            var dup = new HashSet<long>(); bool unique = true;
            for (int cz = 18; cz < 24; cz++) for (int cx = 18; cx < 24; cx++) foreach (var sp in gen.VehicleSpawns(cx, cz)) unique &= dup.Add(sp.Key);
            Check(unique, "elk voertuig bestaat maar één keer (geen dubbele over chunkranden)");

            var v = Vehicles.Create(firstCar.Value);
            Check(v.MissingParts().Count > 0 && !v.Drivable, $"een wrak mist onderdelen: {string.Join(", ", v.MissingParts())}");
            foreach (var p in v.Def.Parts) v.PartOk[(int)p] = true;
            v.Fuel = 20;
            Check(v.Drivable, "met alle onderdelen en benzine is hij rijklaar");

            // proefrit op een vlakke betonplaat met een muur
            var store = new VoxelStore();
            for (int cz = -1; cz <= 2; cz++) for (int cx = -1; cx <= 1; cx++)
            {
                var pad = new byte[World.PadVolume];
                for (int pz = 0; pz < World.Pad; pz++) for (int px = 0; px < World.Pad; px++) pad[World.IdxPad(px, 20, pz)] = B.Concrete;
                store.Add(cx, cz, pad);
            }
            for (int x = -8; x <= 8; x++) for (int y = 21; y <= 24; y++) store.Set(x, y, 70, B.Concrete);   // muur op 35 m
            v.Pos = new V3(0, 21 * World.VoxelSize, 0); v.Yaw = 0; v.Occupied = true; v.Speed = 0;
            float driven = 0, maxSpeed = 0, crashSpeed = 0;
            for (int i = 0; i < 60 * 8; i++)
            {
                driven += VehiclePhysics.Step(v, new VehiclePhysics.Input { Throttle = 1 }, 1f / 60f, store, out float crash);
                maxSpeed = MathF.Max(maxSpeed, v.Speed); crashSpeed = MathF.Max(crashSpeed, crash);
            }
            Check(maxSpeed > 12 && crashSpeed > 5 && v.Pos.Z < 35f, $"auto trekt op tot {maxSpeed * 3.6f:0} km/u en botst tegen de muur ({crashSpeed * 3.6f:0} km/u, z={v.Pos.Z:0.0} m)");
            Check(v.Fuel < 20, $"verbruikt benzine ({20 - v.Fuel:0.00} l)");

            // boot: alleen op het water
            var water = new VoxelStore();
            for (int cz = -1; cz <= 1; cz++) for (int cx = -1; cx <= 1; cx++)
            {
                var pad = new byte[World.PadVolume];
                for (int pz = 0; pz < World.Pad; pz++) for (int px = 0; px < World.Pad; px++)
                {
                    int wx = cx * World.ChunkSize - 1 + px;
                    for (int y = 0; y < 18; y++) pad[World.IdxPad(px, y, pz)] = B.Sand;
                    for (int y = 18; y <= World.Sea; y++) pad[World.IdxPad(px, y, pz)] = wx < 20 ? B.Water : B.Sand;   // strand vanaf x = 10 m
                }
                water.Add(cx, cz, pad);
            }
            var boat = Vehicles.Create(new VehicleSpawn { Type = VehicleType.Roeiboot, Pos = new V3(0, World.SeaLevelMeters, 0), Yaw = 90 });
            boat.Occupied = true;
            for (int i = 0; i < 60 * 20; i++) VehiclePhysics.Step(boat, new VehiclePhysics.Input { Throttle = 1 }, 1f / 60f, water, out _);
            Check(boat.Drivable && boat.Pos.X > 3 && boat.Pos.X < 10, $"roeiboot vaart en loopt vast op het strand (x={boat.Pos.X:0.0} m)");

            var rng = new Random(4);
            int glow = 0; for (int i = 0; i < 50; i++) if (Fishing.Catch(0.5f, rng) == "gloeivis") glow++;
            Check(glow > 20 && Fishing.Catch(0f, rng) != "gloeivis", $"in stralingswater vang je vooral gloeivis ({glow}/50)");
            Check(Fishing.WaitTime(true, rng) < 10.01f, "aas laat vis sneller bijten");
        }

        if (args.Length > 0 && args[0] == "map")
        {
            string path = args.Length > 1 ? args[1] : "wereldkaart.png";
            RenderMap(gen, path);
            Console.WriteLine($"Kaart geschreven naar {path}");
        }

        Console.WriteLine(failures == 0 ? "\nAlle tests geslaagd." : $"\n{failures} test(s) mislukt.");
        return failures == 0 ? 0 : 1;
    }

    // Bovenaanzicht van de wereld met reliëfschaduw, 1 pixel = 2 voxels (1 m)
    static void RenderMap(WorldGen gen, string path)
    {
        const int size = 1400, step = 2;
        int x0 = -size / 2 * step + WorldGen.CityCell / 2, z0 = -size / 2 * step + WorldGen.CityCell / 2;
        var hgt = new float[size * size];
        var col = new byte[size * size * 3];
        for (int j = 0; j < size; j++)
            for (int i = 0; i < size; i++)
            {
                int x = x0 + i * step, z = z0 + j * step;
                var c = gen.GetColumn(x, z);
                hgt[i + j * size] = c.H;
                byte blk;
                switch (c.Kind)
                {
                    case ColumnKind.Highway: case ColumnKind.Bridge: blk = B.Asphalt; break;
                    case ColumnKind.Street: blk = B.Asphalt; break;
                    case ColumnKind.Sidewalk: blk = B.Sidewalk; break;
                    case ColumnKind.Settlement:
                    {
                        var st = gen.SettlementNear(x, z, out _);
                        blk = B.Grass;
                        if (st != null) for (int ly = 16; ly >= 0; ly--) { int bb = st.BlockAt(x - st.X0, ly, z - st.Z0, gen.Seed); if (bb > 0) { blk = (byte)bb; break; } }
                        if (B.IsSeedling(blk)) blk = B.Farmland;
                        break;
                    }
                    case ColumnKind.Lot:
                    {
                        var lot = gen.LotAtVoxel(x, z);
                        blk = B.Grass;
                        if (lot != null)
                        {
                            int top = -1;
                            for (int ly = lot.Floors * 6 + 20; ly >= 0 && top < 0; ly--) { int bb = gen.BuildingBlock(lot, x, lot.Base + ly, z); if (bb > 0) top = bb; }
                            if (top > 0) blk = (byte)top;
                            else if (lot.Type == LotType.Parkeerplaats || lot.Type == LotType.Benzinestation) blk = B.Asphalt;
                            else if (lot.Type == LotType.Ruine) blk = B.Rubble;
                        }
                        break;
                    }
                    default:
                        if (c.H <= World.Sea) blk = B.Water;
                        else if (c.H <= World.Sea + 1) blk = B.Sand;
                        else if (c.H > World.Sea + 44) blk = B.Stone;
                        else blk = gen.Moisture(x, z) < -0.22f ? B.DeadGrass : gen.Moisture(x, z) > 0.18f ? B.Leaves : B.Grass;
                        break;
                }
                if (c.Rad > 0.12f && (c.Kind == ColumnKind.Nature || c.Kind == ColumnKind.Lot) && blk != B.Water) blk = c.Rad > 0.55f ? B.Scorched : B.Ash;
                var info = Blocks.Info[blk];
                int k = (i + j * size) * 3;
                col[k] = info.R; col[k + 1] = info.G; col[k + 2] = info.Bl;
                if (blk == B.Water)
                {
                    float depth = Math.Clamp((World.Sea - c.H) / 10f, 0, 1);
                    col[k] = (byte)(60 - depth * 30); col[k + 1] = (byte)(104 - depth * 40); col[k + 2] = (byte)(122 - depth * 30);
                }
            }
        var png = new byte[size * size * 3];
        for (int j = 0; j < size; j++)
            for (int i = 0; i < size; i++)
            {
                float hx = hgt[Math.Min(size - 1, i + 1) + j * size] - hgt[Math.Max(0, i - 1) + j * size];
                float hz = hgt[i + Math.Min(size - 1, j + 1) * size] - hgt[i + Math.Max(0, j - 1) * size];
                float shade = Math.Clamp(1f + (-hx - hz) * 0.12f, 0.55f, 1.3f);
                int k = (i + j * size) * 3;
                for (int c = 0; c < 3; c++) png[k + c] = (byte)Math.Clamp(col[k + c] * shade, 0, 255);
            }
        WritePng(path, size, size, png);
    }

    static void WritePng(string path, int w, int h, byte[] rgb)
    {
        using var fs = File.Create(path);
        fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        void Chunk(string type, byte[] data)
        {
            var len = BitConverter.GetBytes(data.Length); Array.Reverse(len); fs.Write(len);
            var t = System.Text.Encoding.ASCII.GetBytes(type);
            fs.Write(t); fs.Write(data);
            uint crc = Crc(t, data);
            var cb = BitConverter.GetBytes(crc); Array.Reverse(cb); fs.Write(cb);
        }
        var ihdr = new byte[13];
        void Be(int v, int o) { ihdr[o] = (byte)(v >> 24); ihdr[o + 1] = (byte)(v >> 16); ihdr[o + 2] = (byte)(v >> 8); ihdr[o + 3] = (byte)v; }
        Be(w, 0); Be(h, 4); ihdr[8] = 8; ihdr[9] = 2;
        Chunk("IHDR", ihdr);
        var raw = new MemoryStream();
        for (int y = 0; y < h; y++) { raw.WriteByte(0); raw.Write(rgb, y * w * 3, w * 3); }
        var z = new MemoryStream();
        using (var ds = new ZLibStream(z, CompressionLevel.Optimal, true)) { raw.Position = 0; raw.CopyTo(ds); }
        Chunk("IDAT", z.ToArray());
        Chunk("IEND", Array.Empty<byte>());
    }

    static uint[] crcTable;
    static uint Crc(byte[] a, byte[] b)
    {
        if (crcTable == null)
        {
            crcTable = new uint[256];
            for (uint n = 0; n < 256; n++) { uint c = n; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1; crcTable[n] = c; }
        }
        uint crc = 0xFFFFFFFFu;
        foreach (var x in a) crc = crcTable[(crc ^ x) & 0xFF] ^ (crc >> 8);
        foreach (var x in b) crc = crcTable[(crc ^ x) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFFu;
    }
}
