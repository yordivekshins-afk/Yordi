using System.Collections.Generic;
using Deadhaul.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Deadhaul
{
    /// <summary>
    /// Tijdelijke HUD in IMGUI: meters, snelbalk, rugzak, crafting, loot, pauzemenu en doodscherm.
    /// Wordt in een latere mijlpaal vervangen door een ontworpen UI Toolkit-interface.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        GameState game;
        public bool InventoryOpen, Paused, ShowHelp = true;
        List<Stack> loot;
        string lootTitle;
        int dragSlot = -1;
        readonly List<(string text, float time)> messages = new List<(string, float)>();
        string banner, bannerSub; float bannerTime = -10;
        float fps, fpsTimer; int fpsFrames;

        GUIStyle label, small, title, huge, button, box, center;
        Texture2D white;
        static readonly Color Ink = new Color(0.92f, 0.89f, 0.8f), Dim = new Color(0.66f, 0.63f, 0.55f), Accent = new Color(0.85f, 0.58f, 0.18f);
        static readonly Color Panel = new Color(0.07f, 0.075f, 0.06f, 0.86f), Line = new Color(0.3f, 0.3f, 0.24f, 1f);

        public bool LootOpen => loot != null;
        public bool CapturesInput => InventoryOpen || LootOpen || Paused || game.Stats.Dead || !game.Ready;

        public void Init(GameState g) { game = g; }

        public void Message(string text)
        {
            messages.Add((text, Time.time));
            if (messages.Count > 6) messages.RemoveAt(0);
        }

        public void Banner(string text, string sub) { banner = text; bannerSub = sub; bannerTime = Time.time; }

        public void OpenLoot(List<Stack> items, string title)
        {
            if (items.Count == 0) { Message($"De {title} is leeg."); return; }
            loot = items; lootTitle = title;
        }

        void Update()
        {
            fpsFrames++; fpsTimer += Time.unscaledDeltaTime;
            if (fpsTimer > 0.5f) { fps = fpsFrames / fpsTimer; fpsFrames = 0; fpsTimer = 0; }
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame)
            {
                if (LootOpen) loot = null;
                else if (InventoryOpen) InventoryOpen = false;
                else if (!game.Stats.Dead) Paused = !Paused;
            }
            if (kb.tabKey.wasPressedThisFrame || kb.iKey.wasPressedThisFrame) { if (!Paused && !game.Stats.Dead) { InventoryOpen = !InventoryOpen; loot = null; dragSlot = -1; } }
            if (kb.hKey.wasPressedThisFrame) ShowHelp = !ShowHelp;
            if (kb.f5Key.wasPressedThisFrame) game.Save();
        }

        void Styles()
        {
            if (label != null) return;
            white = Texture2D.whiteTexture;
            label = new GUIStyle(GUI.skin.label) { fontSize = 15, normal = { textColor = Ink }, richText = true };
            small = new GUIStyle(label) { fontSize = 12, normal = { textColor = Dim } };
            title = new GUIStyle(label) { fontSize = 22, fontStyle = FontStyle.Bold, normal = { textColor = Accent } };
            huge = new GUIStyle(label) { fontSize = 46, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Ink } };
            center = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
            button = new GUIStyle(GUI.skin.button) { fontSize = 15, normal = { textColor = Ink }, hover = { textColor = Accent } };
            box = new GUIStyle(GUI.skin.box);
        }

        void Fill(Rect r, Color c) { var old = GUI.color; GUI.color = c; GUI.DrawTexture(r, white); GUI.color = old; }

        void Frame(Rect r)
        {
            Fill(r, Panel);
            Fill(new Rect(r.x, r.y, r.width, 1), Line); Fill(new Rect(r.x, r.yMax - 1, r.width, 1), Line);
            Fill(new Rect(r.x, r.y, 1, r.height), Line); Fill(new Rect(r.xMax - 1, r.y, 1, r.height), Line);
        }

        static Color IconColor(ItemDef d)
        {
            var i = Blocks.Info[d.IconBlock];
            return new Color(i.R / 255f, i.G / 255f, i.Bl / 255f);
        }

        void OnGUI()
        {
            if (game == null) return;
            Styles();
            float W = Screen.width, H = Screen.height;

            if (!game.Ready)
            {
                Fill(new Rect(0, 0, W, H), new Color(0.03f, 0.03f, 0.025f, 1));
                GUI.Label(new Rect(0, H * 0.4f, W, 60), "DEADHAUL", huge);
                GUI.Label(new Rect(0, H * 0.4f + 60, W, 30), $"De wereld wordt opgebouwd… ({game.Chunks.Store.LoadedCount} chunks)", center);
                return;
            }

            if (game.Stats.Dead) { DeathScreen(W, H); return; }

            Meters(H);
            Hotbar(W, H);
            TopBar(W);
            Messages(H);
            BannerDraw(W, H);
            if (!CapturesInput) Crosshair(W, H);
            if (ShowHelp && !CapturesInput) Help();
            if (InventoryOpen) InventoryWindow(W, H);
            if (LootOpen) LootWindow(W, H);
            if (Paused) PauseMenu(W, H);

            // pijn en kou aan de randen van het scherm
            var s = game.Stats;
            if (s.Health < 30) Fill(new Rect(0, 0, W, H), new Color(0.5f, 0, 0, (30 - s.Health) / 30f * 0.25f * (0.8f + Mathf.Sin(Time.time * 4) * 0.2f)));
        }

        void Meters(float H)
        {
            var s = game.Stats;
            float x = 22, y = H - 160;
            Bar(x, y, "Gezondheid", s.Health, new Color(0.72f, 0.18f, 0.14f));
            Bar(x, y + 24, "Honger", s.Food, new Color(0.78f, 0.55f, 0.2f));
            Bar(x, y + 48, "Dorst", s.Water, new Color(0.25f, 0.55f, 0.78f));
            Bar(x, y + 72, "Uithouding", s.Stamina, new Color(0.62f, 0.7f, 0.3f));
            Bar(x, y + 96, "Warmte", s.Warmth, Color.Lerp(new Color(0.4f, 0.62f, 0.9f), new Color(0.95f, 0.55f, 0.2f), s.Warmth / 100f));
            var tags = new List<string>();
            if (s.Bleeding) tags.Add("<color=#e0503c>BLOEDT</color>");
            if (s.Sick) tags.Add("<color=#9bc34a>ZIEK</color>");
            if (s.Warmth < 30) tags.Add("<color=#7fb0ff>ONDERKOELD</color>");
            if (game.Player.NearFire) tags.Add("<color=#ffae5a>BIJ HET VUUR</color>");
            if (game.Player.InWater) tags.Add("<color=#7fd0ff>IN HET WATER — E om te drinken</color>");
            if (game.Inventory.Weight > game.Inventory.MaxWeight) tags.Add("<color=#e0a03c>OVERBELAST</color>");
            GUI.Label(new Rect(x, y + 120, 600, 22), string.Join("   ", tags), label);
        }

        void Bar(float x, float y, string name, float v, Color c)
        {
            GUI.Label(new Rect(x, y, 100, 20), name, small);
            Fill(new Rect(x + 96, y + 5, 180, 10), new Color(0, 0, 0, 0.55f));
            Fill(new Rect(x + 96, y + 5, 180 * Mathf.Clamp01(v / 100f), 10), c);
        }

        void Hotbar(float W, float H)
        {
            float size = 62, gap = 6, total = Inventory.HotbarSize * (size + gap) - gap;
            float x0 = (W - total) / 2, y = H - size - 22;
            for (int i = 0; i < Inventory.HotbarSize; i++)
            {
                var r = new Rect(x0 + i * (size + gap), y, size, size);
                Slot(r, game.Inventory.Slots[i], i == game.Player.Selected, (i + 1).ToString());
            }
            var def = game.Player.SelectedDef;
            if (def != null) GUI.Label(new Rect(0, y - 26, W, 22), def.Name + (def.Kind == ItemKind.Block ? "  —  rechtermuis om te bouwen" : def.Kind is ItemKind.Food or ItemKind.Drink or ItemKind.Medical ? "  —  Q om te gebruiken" : ""), center);
        }

        void Slot(Rect r, Stack s, bool selected, string key)
        {
            Fill(r, selected ? new Color(0.25f, 0.2f, 0.1f, 0.9f) : new Color(0.06f, 0.06f, 0.05f, 0.78f));
            if (selected) { Fill(new Rect(r.x, r.yMax - 3, r.width, 3), Accent); }
            if (!s.Empty)
            {
                var d = s.Def;
                Fill(new Rect(r.x + 14, r.y + 10, r.width - 28, r.height - 30), IconColor(d));
                GUI.Label(new Rect(r.x + 3, r.yMax - 20, r.width - 6, 18), d.Name, new GUIStyle(small) { fontSize = 10, alignment = TextAnchor.MiddleCenter, normal = { textColor = Ink } });
                if (s.Count > 1) GUI.Label(new Rect(r.xMax - 30, r.y + 2, 28, 18), s.Count.ToString(), new GUIStyle(small) { alignment = TextAnchor.UpperRight, normal = { textColor = Ink } });
            }
            if (key != null) GUI.Label(new Rect(r.x + 4, r.y + 2, 20, 16), key, small);
        }

        void TopBar(float W)
        {
            var c = game.Clock;
            float yaw = Mathf.Repeat(game.Player.Yaw, 360f);
            string[] dirs = { "N", "NO", "O", "ZO", "Z", "ZW", "W", "NW" };
            string dir = dirs[Mathf.RoundToInt(yaw / 45f) % 8];
            var r = new Rect(W / 2 - 120, 12, 240, 32);
            Frame(r);
            GUI.Label(r, $"{c.Label}   ·   {dir}", center);
            string rt = game.Environment.RayTracingSupported ? game.Environment.RayTracing.ToString() : "niet ondersteund";
            GUI.Label(new Rect(W - 260, 12, 248, 20), $"{fps:0} fps   ·   raytracing: {rt}", new GUIStyle(small) { alignment = TextAnchor.UpperRight });
        }

        void Messages(float H)
        {
            float y = H - 200;
            for (int i = messages.Count - 1; i >= 0; i--)
            {
                float age = Time.time - messages[i].time;
                if (age > 8) continue;
                var col = Ink; col.a = Mathf.Clamp01(8 - age);
                GUI.Label(new Rect(22, y, 700, 22), messages[i].text, new GUIStyle(label) { normal = { textColor = col } });
                y -= 22;
            }
        }

        void BannerDraw(float W, float H)
        {
            float age = Time.time - bannerTime;
            if (age > 6 || banner == null) return;
            float a = Mathf.Clamp01(age * 2) * Mathf.Clamp01(6 - age);
            GUI.Label(new Rect(0, H * 0.18f, W, 60), banner.ToUpperInvariant(), new GUIStyle(huge) { normal = { textColor = new Color(Ink.r, Ink.g, Ink.b, a) } });
            GUI.Label(new Rect(0, H * 0.18f + 56, W, 24), bannerSub, new GUIStyle(center) { normal = { textColor = new Color(Accent.r, Accent.g, Accent.b, a) } });
        }

        void Crosshair(float W, float H)
        {
            Fill(new Rect(W / 2 - 1, H / 2 - 7, 2, 14), new Color(1, 1, 1, 0.7f));
            Fill(new Rect(W / 2 - 7, H / 2 - 1, 14, 2), new Color(1, 1, 1, 0.7f));
            var t = game.Player.Target;
            if (!t.Hit) return;
            var info = Blocks.Info[t.Block];
            string hint = (info.Flags & BlockFlags.Container) != 0 ? (game.Chunks.Store.IsLooted(t.X, t.Y, t.Z) ? "  (leeg)" : "  —  E om te doorzoeken") : t.Block == B.Crop ? "  —  E om te plukken" : "";
            GUI.Label(new Rect(0, H / 2 + 16, W, 22), info.Name + hint, center);
            if (game.Player.MineProgress > 0)
            {
                Fill(new Rect(W / 2 - 40, H / 2 + 40, 80, 5), new Color(0, 0, 0, 0.6f));
                Fill(new Rect(W / 2 - 40, H / 2 + 40, 80 * game.Player.MineProgress, 5), Accent);
            }
        }

        void Help()
        {
            var r = new Rect(22, 60, 330, 212);
            Frame(r);
            GUI.Label(new Rect(r.x + 12, r.y + 8, r.width, 24), "Besturing", title);
            GUI.Label(new Rect(r.x + 12, r.y + 38, r.width - 20, r.height - 40),
                "WASD lopen · Shift sprinten · C sluipen · Spatie springen/zwemmen\n" +
                "Linkermuis slopen/slaan · Rechtermuis bouwen\n" +
                "E doorzoeken/plukken/drinken · Q eten/gebruiken\n" +
                "1–6 of scrollen: snelbalk · Tab rugzak & crafting\n" +
                "F zaklamp · V first-person · F5 opslaan · Esc menu\n" +
                "H verbergt deze hulp", small);
        }

        void InventoryWindow(float W, float H)
        {
            var inv = game.Inventory;
            var r = new Rect(W / 2 - 470, H / 2 - 250, 940, 500);
            Frame(r);
            GUI.Label(new Rect(r.x + 18, r.y + 12, 300, 30), "Rugzak", title);
            GUI.Label(new Rect(r.x + 120, r.y + 18, 400, 24), $"{inv.Weight:0.0} / {inv.MaxWeight:0} kg   ·   klik om te verplaatsen, rechtsklik om te gebruiken of weg te gooien", small);
            float size = 70, gap = 6;
            for (int i = 0; i < inv.Slots.Length; i++)
            {
                int col = i % 6, row = i / 6;
                var sr = new Rect(r.x + 18 + col * (size + gap), r.y + 56 + row * (size + gap) + (row > 0 ? 10 : 0), size, size);
                Slot(sr, inv.Slots[i], i == dragSlot, i < Inventory.HotbarSize ? (i + 1).ToString() : null);
                var e = Event.current;
                if (e.type == EventType.MouseDown && sr.Contains(e.mousePosition))
                {
                    if (e.button == 0)
                    {
                        if (dragSlot < 0) { if (!inv.Slots[i].Empty) dragSlot = i; }
                        else { inv.Swap(dragSlot, i); dragSlot = -1; game.Player.RefreshTool(); }
                    }
                    else if (e.button == 1 && !inv.Slots[i].Empty)
                    {
                        var msg = game.Stats.Use(inv.Slots[i].Def);
                        if (msg != null) Message(msg);
                        else Message($"Weggegooid: {inv.Slots[i].Def.Name}.");
                        inv.TakeFromSlot(i);
                        game.Player.RefreshTool();
                    }
                    e.Use();
                }
            }
            // crafting
            float cx = r.x + 500;
            GUI.Label(new Rect(cx, r.y + 12, 300, 30), "Maken", title);
            float y = r.y + 56;
            foreach (var rec in Crafting.All)
            {
                var def = Items.Get(rec.Result);
                bool can = Crafting.CanCraft(inv, rec, game.Player.NearFire);
                var rr = new Rect(cx, y, 420, 46);
                Fill(rr, new Color(0.1f, 0.1f, 0.08f, 0.8f));
                Fill(new Rect(rr.x + 8, rr.y + 8, 30, 30), IconColor(def));
                GUI.Label(new Rect(rr.x + 48, rr.y + 3, 260, 22), $"{def.Name}{(rec.Count > 1 ? " ×" + rec.Count : "")}", label);
                var needs = new List<string>();
                foreach (var (id, n) in rec.Needs) needs.Add($"{n} {Items.Get(id).Name.ToLowerInvariant()} ({inv.Count(id)})");
                GUI.Label(new Rect(rr.x + 48, rr.y + 24, 280, 20), string.Join(", ", needs), small);
                GUI.enabled = can;
                if (GUI.Button(new Rect(rr.xMax - 90, rr.y + 8, 80, 30), "Maak", button))
                {
                    if (Crafting.Craft(inv, rec, game.Player.NearFire)) Message($"Gemaakt: {def.Name}.");
                    game.Player.RefreshTool();
                }
                GUI.enabled = true;
                y += 52;
            }
        }

        void LootWindow(float W, float H)
        {
            var r = new Rect(W / 2 - 220, H / 2 - 200, 440, 60 + loot.Count * 44 + 60);
            Frame(r);
            GUI.Label(new Rect(r.x + 18, r.y + 12, 400, 30), "Inhoud van de " + lootTitle, title);
            float y = r.y + 56;
            for (int i = 0; i < loot.Count; i++)
            {
                var s = loot[i];
                Fill(new Rect(r.x + 18, y + 6, 28, 28), IconColor(s.Def));
                GUI.Label(new Rect(r.x + 56, y + 8, 250, 24), $"{s.Count}× {s.Def.Name}", label);
                if (GUI.Button(new Rect(r.xMax - 100, y + 6, 82, 30), "Pak", button))
                {
                    int left = game.Inventory.Add(s.Id, s.Count);
                    if (left == 0) { loot.RemoveAt(i); i--; }
                    else { s.Count = left; loot[i] = s; Message("Je rugzak zit vol."); }
                    game.Player.RefreshTool();
                }
                y += 44;
            }
            if (GUI.Button(new Rect(r.x + 18, r.yMax - 48, 180, 32), "Alles pakken", button))
            {
                for (int i = loot.Count - 1; i >= 0; i--)
                {
                    var s = loot[i];
                    int left = game.Inventory.Add(s.Id, s.Count);
                    if (left == 0) loot.RemoveAt(i); else { s.Count = left; loot[i] = s; }
                }
                if (loot.Count > 0) Message("Niet alles past in je rugzak.");
                game.Player.RefreshTool();
            }
            if (GUI.Button(new Rect(r.xMax - 118, r.yMax - 48, 100, 32), "Sluiten", button) || loot.Count == 0) loot = null;
        }

        void PauseMenu(float W, float H)
        {
            Fill(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.55f));
            var r = new Rect(W / 2 - 200, H / 2 - 230, 400, 460);
            Frame(r);
            GUI.Label(new Rect(r.x, r.y + 16, r.width, 50), "DEADHAUL", new GUIStyle(huge) { fontSize = 36 });
            float y = r.y + 84, bw = 320, bx = r.x + 40;
            if (GUI.Button(new Rect(bx, y, bw, 36), "Verder spelen", button)) Paused = false; y += 44;
            if (GUI.Button(new Rect(bx, y, bw, 36), "Opslaan", button)) game.Save(); y += 44;
            var env = game.Environment;
            GUI.enabled = env.RayTracingSupported;
            if (GUI.Button(new Rect(bx, y, bw, 36), "Raytracing: " + (env.RayTracingSupported ? env.RayTracing.ToString() : "niet ondersteund"), button)) env.CycleRayTracing();
            GUI.enabled = true; y += 44;
            GUI.Label(new Rect(bx, y, bw, 22), $"Zichtafstand: {game.Settings.ViewRadius * World.ChunkSize * World.VoxelSize:0} m", label); y += 24;
            game.Settings.ViewRadius = Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(bx, y, bw, 20), game.Settings.ViewRadius, 5, 20)); y += 26;
            GUI.Label(new Rect(bx, y, bw, 22), $"Muisgevoeligheid: {game.Settings.MouseSensitivity * 100:0}", label); y += 24;
            game.Settings.MouseSensitivity = GUI.HorizontalSlider(new Rect(bx, y, bw, 20), game.Settings.MouseSensitivity, 0.02f, 0.3f); y += 30;
            if (GUI.Button(new Rect(bx, y, bw, 36), "Nieuw spel (wist je save)", button)) { Paused = false; game.DeleteSaveAndRestart(); } y += 44;
            if (GUI.Button(new Rect(bx, y, bw, 36), "Opslaan en afsluiten", button))
            {
                game.Save();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
        }

        void DeathScreen(float W, float H)
        {
            Fill(new Rect(0, 0, W, H), new Color(0.12f, 0.01f, 0.01f, 0.82f));
            GUI.Label(new Rect(0, H * 0.35f, W, 60), "JE BENT DOOD", huge);
            GUI.Label(new Rect(0, H * 0.35f + 64, W, 28), $"Je overleefde tot {game.Clock.Label}.", center);
            if (GUI.Button(new Rect(W / 2 - 120, H * 0.35f + 120, 240, 40), "Opnieuw beginnen", button)) game.Respawn();
        }
    }
}
