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
        Texture2D white, scopeMask;
        float hitTime = -10; bool hitKill, hitHead;
        int modSlot = -1;
        static readonly Color Ink = new Color(0.92f, 0.89f, 0.8f), Dim = new Color(0.66f, 0.63f, 0.55f), Accent = new Color(0.85f, 0.58f, 0.18f);
        static readonly Color Panel = new Color(0.07f, 0.075f, 0.06f, 0.86f), Line = new Color(0.3f, 0.3f, 0.24f, 1f);

        public bool LootOpen => loot != null;
        public bool TradeOpen => trade != null;
        public bool VehicleOpen => vehicle != null;
        public bool CapturesInput => InventoryOpen || LootOpen || TradeOpen || VehicleOpen || Paused || game.Stats.Dead || !game.Ready;
        Vehicle vehicle;

        public void OpenVehicle(Vehicle v) { vehicle = v; InventoryOpen = false; loot = null; trade = null; }
        Settlement trade; string tradeName;
        Vector2 tradeScrollA, tradeScrollB;

        public void OpenTrade(Settlement s, string trader) { trade = s; tradeName = trader; InventoryOpen = false; loot = null; }

        public void HitMarker(bool kill, bool head) { hitTime = Time.time; hitKill = kill; hitHead = head; }

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
                else if (TradeOpen) trade = null;
                else if (VehicleOpen) vehicle = null;
                else if (InventoryOpen) InventoryOpen = false;
                else if (!game.Stats.Dead) Paused = !Paused;
            }
            if (kb.tabKey.wasPressedThisFrame || kb.iKey.wasPressedThisFrame) { if (!Paused && !game.Stats.Dead) { InventoryOpen = !InventoryOpen; loot = null; dragSlot = -1; modSlot = -1; } }
            if (kb.hKey.wasPressedThisFrame && game.Player.Vehicle == null) ShowHelp = !ShowHelp;
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
            // scope-masker: zwart met een rond gat
            const int S = 256;
            scopeMask = new Texture2D(S, S, TextureFormat.RGBA32, false);
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = (x - S / 2f) / (S / 2f), dy = (y - S / 2f) / (S / 2f);
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    px[x + y * S] = new Color(0, 0, 0, Mathf.Clamp01((r - 0.9f) / 0.08f));
                }
            scopeMask.SetPixels(px);
            scopeMask.Apply();
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

            var pl = game.Player;
            bool scoped = pl.HasGun && pl.Weapon.Zoom >= 3.5f && pl.AimT > 0.95f;
            if (scoped) Scope(W, H);
            DamageDirection(W, H);
            Meters(H);
            Hotbar(W, H);
            if (pl.HasGun) Ammo(W, H);
            TopBar(W);
            Messages(H);
            BannerDraw(W, H);
            if (!CapturesInput && !scoped) Crosshair(W, H);
            if (!CapturesInput) HitMarkerDraw(W, H);
            if (ShowHelp && !CapturesInput) Help();
            if (InventoryOpen) InventoryWindow(W, H);
            if (LootOpen) LootWindow(W, H);
            if (TradeOpen) TradeWindow(W, H);
            if (VehicleOpen) VehicleWindow(W, H);
            if (game.Player.Vehicle != null) DrivingHud(W, H);
            else if (!CapturesInput) Prompts(W, H);
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
            if (s.Radiation > 0.5f || game.Player.Radiation > 0.01f) Bar(x, y - 24, "Straling", s.Radiation, new Color(0.55f, 0.95f, 0.25f));
            var tags = new List<string>();
            if (s.Bleeding) tags.Add("<color=#e0503c>BLOEDT</color>");
            if (s.Sick) tags.Add("<color=#9bc34a>ZIEK</color>");
            if (s.Warmth < 30) tags.Add("<color=#7fb0ff>ONDERKOELD</color>");
            if (game.Player.NearFire) tags.Add("<color=#ffae5a>BIJ HET VUUR</color>");
            if (game.Player.Radiation > 0.02f) tags.Add($"<color=#9bff5a>STRALINGSZONE {game.Player.Radiation * 100:0}%</color>");
            if (game.Combat.HostilesNear(60) > 0) tags.Add("<color=#ff7a5a>IN GEVECHT</color>");
            if (game.Player.InWater) tags.Add("<color=#7fd0ff>IN HET WATER — E om te drinken</color>");
            if (game.Inventory.Weight + game.Equipment.Weight > game.Inventory.MaxWeight) tags.Add("<color=#e0a03c>OVERBELAST</color>");
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
            if (def != null) GUI.Label(new Rect(0, y - 26, W, 22), def.Name + (def.Kind == ItemKind.Block ? "  —  rechtermuis om te bouwen" : def.Kind is ItemKind.Food or ItemKind.Drink or ItemKind.Medical ? "  —  Q om te gebruiken" : def.Kind == ItemKind.Clothing ? "  —  Q om aan te trekken" : ""), center);
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
                if (d.GunDamage > 0) GUI.Label(new Rect(r.xMax - 34, r.y + 2, 32, 18), s.Ammo.ToString(), new GUIStyle(small) { alignment = TextAnchor.UpperRight, normal = { textColor = Accent } });
                if (s.Mods != null) Fill(new Rect(r.x + 2, r.y + 2, 4, 4), Accent);
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
            var pl = game.Player;
            if (pl.HasGun)
            {
                float hs = new Vector2(pl.Vel.X, pl.Vel.Z).magnitude;
                float spread = pl.Weapon.Spread * Mathf.Lerp(1f, 0.22f, pl.AimT) * (hs > 1 ? 1.6f : 1f) * (pl.Crouching ? 0.75f : 1f) * (1 + pl.Bloom);
                float gap = 3 + spread * H / Mathf.Max(10f, game.Player.Cam.fieldOfView) * 0.5f;
                var c = new Color(1, 1, 1, Mathf.Lerp(0.8f, 0.35f, pl.AimT));
                Fill(new Rect(W / 2 - 1, H / 2 - gap - 8, 2, 8), c); Fill(new Rect(W / 2 - 1, H / 2 + gap, 2, 8), c);
                Fill(new Rect(W / 2 - gap - 8, H / 2 - 1, 8, 2), c); Fill(new Rect(W / 2 + gap, H / 2 - 1, 8, 2), c);
                Fill(new Rect(W / 2 - 1, H / 2 - 1, 2, 2), new Color(1, 0.3f, 0.2f, 0.9f));
                return;
            }
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

        void HitMarkerDraw(float W, float H)
        {
            float age = Time.time - hitTime;
            if (age > 0.25f) return;
            var c = hitKill ? new Color(1f, 0.25f, 0.2f, 1 - age * 4) : hitHead ? new Color(1f, 0.85f, 0.3f, 1 - age * 4) : new Color(1, 1, 1, 1 - age * 4);
            float g = 6, l = hitKill ? 12 : 8;
            var old = GUI.matrix;
            foreach (float ang in new[] { 45f, 135f, 225f, 315f })
            {
                GUIUtility.RotateAroundPivot(ang, new Vector2(W / 2, H / 2));
                Fill(new Rect(W / 2 - 1, H / 2 + g, 2, l), c);
                GUI.matrix = old;
            }
        }

        void DamageDirection(float W, float H)
        {
            var pa = game.Combat.PlayerActor;
            float age = Time.time - pa.LastHitTime;
            if (age > 1.2f) return;
            var from = pa.LastHitFrom - pa.Pos;
            float ang = Mathf.Atan2(from.X, from.Z) * Mathf.Rad2Deg - game.Player.Yaw;
            var old = GUI.matrix;
            GUIUtility.RotateAroundPivot(ang, new Vector2(W / 2, H / 2));
            Fill(new Rect(W / 2 - 60, H / 2 - 190, 120, 8), new Color(0.9f, 0.1f, 0.05f, (1.2f - age) * 0.7f));
            GUI.matrix = old;
        }

        void Scope(float W, float H)
        {
            float size = H * 0.95f;
            var r = new Rect((W - size) / 2, (H - size) / 2, size, size);
            GUI.DrawTexture(r, scopeMask);
            Fill(new Rect(0, 0, r.x + 1, H), Color.black); Fill(new Rect(r.xMax - 1, 0, W - r.xMax + 1, H), Color.black);
            Fill(new Rect(W / 2 - 0.5f, r.y, 1, size), new Color(0, 0, 0, 0.85f));
            Fill(new Rect(r.x, H / 2 - 0.5f, size, 1), new Color(0, 0, 0, 0.85f));
            Fill(new Rect(W / 2 - 2, H / 2 - 2, 4, 4), new Color(0.9f, 0.1f, 0.05f, 0.9f));
            for (int i = 1; i <= 4; i++) Fill(new Rect(W / 2 - 6, H / 2 + i * size * 0.035f, 12, 1), new Color(0, 0, 0, 0.85f));   // valcompensatie
        }

        void Ammo(float W, float H)
        {
            var pl = game.Player;
            var s = game.Inventory.Slots[pl.Selected];
            if (s.Empty) return;
            var d = s.Def;
            int reserve = game.Inventory.Count(d.AmmoId);
            var r = new Rect(W - 260, H - 96, 238, 74);
            Frame(r);
            GUI.Label(new Rect(r.x + 14, r.y + 6, 220, 22), d.Name, label);
            GUI.Label(new Rect(r.x + 14, r.y + 26, 220, 44), $"<size=30><b>{s.Ammo}</b></size> <color=#a8a08a>/ {reserve}</color>", label);
            string mode = pl.Weapon.Automatic ? (pl.FullAuto ? "AUTO" : "ENKEL") : d.Class == WeaponClass.Shotgun ? "POMP" : d.Class == WeaponClass.Sniper ? "GRENDEL" : "SEMI";
            GUI.Label(new Rect(r.xMax - 90, r.y + 38, 80, 22), mode, new GUIStyle(small) { alignment = TextAnchor.MiddleRight, normal = { textColor = Accent } });
            if (pl.Reloading)
            {
                float t = 1 - pl.ReloadT / Mathf.Max(0.1f, pl.Weapon.ReloadTime);
                Fill(new Rect(r.x, r.yMax - 4, r.width * t, 4), Accent);
                GUI.Label(new Rect(0, H / 2 + 40, W, 22), "Herladen…", center);
            }
            else if (s.Ammo == 0) GUI.Label(new Rect(0, H / 2 + 40, W, 22), reserve > 0 ? "R om te herladen" : "Geen munitie", center);
        }

        void Help()
        {
            var r = new Rect(22, 60, 440, 232);
            Frame(r);
            GUI.Label(new Rect(r.x + 12, r.y + 8, r.width, 24), "Besturing", title);
            GUI.Label(new Rect(r.x + 12, r.y + 38, r.width - 20, r.height - 40),
                "WASD lopen · Shift sprinten · C sluipen · Spatie springen/zwemmen\n" +
                "Linkermuis schieten/slaan/slopen · Rechtermuis richten/bouwen\n" +
                "R herladen · B vuurmodus · E doorzoeken (ook lijken)\n" +
                "Q eten/gebruiken/aantrekken · 1–6 of scrollen: snelbalk\n" +
                "Tab rugzak, uitrusting, wapenbank en crafting\n" +
                "F zaklamp · V first-person · F5 opslaan · Esc menu · H hulp\n" +
                "E bij een voertuig: repareren, tanken, instappen · hengel: klik op water", small);
        }

        static readonly string[] SlotNames = { "Hoofd", "Gezicht", "Romp", "Vest", "Rug", "Benen", "Voeten" };

        void InventoryWindow(float W, float H)
        {
            var inv = game.Inventory;
            var r = new Rect(W / 2 - 640, H / 2 - 320, 1280, 640);
            Frame(r);
            // ------------------------------------------------ uitrusting
            GUI.Label(new Rect(r.x + 18, r.y + 12, 300, 30), "Uitrusting", title);
            var eq = game.Equipment;
            for (int i = 0; i < 7; i++)
            {
                var sr = new Rect(r.x + 18, r.y + 56 + i * 64, 58, 58);
                Slot(sr, eq.Slots[i], false, null);
                GUI.Label(new Rect(sr.xMax + 8, sr.y + 4, 150, 20), SlotNames[i], small);
                if (!eq.Slots[i].Empty)
                {
                    var d = eq.Slots[i].Def;
                    var bits = new List<string>();
                    if (d.Capacity > 0) bits.Add($"+{d.Capacity} vakken");
                    if (d.Armor > 0) bits.Add($"pantser {d.Armor * 100:0}%");
                    if (d.Warmth > 0) bits.Add($"warmte {d.Warmth * 100:0}");
                    if (d.RadProtection > 0) bits.Add($"straling −{d.RadProtection * 100:0}%");
                    GUI.Label(new Rect(sr.xMax + 8, sr.y + 22, 170, 34), string.Join(" · ", bits), new GUIStyle(small) { wordWrap = true, fontSize = 11 });
                }
                var e = Event.current;
                if (e.type == EventType.MouseDown && sr.Contains(e.mousePosition)) { game.TakeOff((EquipSlot)i); e.Use(); }
            }
            GUI.Label(new Rect(r.x + 18, r.y + 510, 240, 90),
                $"Draagvermogen {inv.MaxWeight:0} kg\nWarmte kleding {eq.Warmth * 100:0}\nStralingsbescherming {eq.Radiation * 100:0}%\nKlik op een stuk om het uit te trekken.", small);

            // ------------------------------------------------ rugzak
            float gx = r.x + 270;
            GUI.Label(new Rect(gx, r.y + 12, 300, 30), "Rugzak", title);
            GUI.Label(new Rect(gx + 100, r.y + 18, 520, 24), $"{inv.Weight + eq.Weight:0.0} / {inv.MaxWeight:0} kg  ·  {inv.Capacity} vakken  ·  klik = verplaatsen, rechtsklik = gebruiken / aantrekken / wapen aanpassen", small);
            float size = 62, gap = 5;
            for (int i = 0; i < inv.Slots.Length; i++)
            {
                int col = i % 8, row = i / 8;
                var sr = new Rect(gx + col * (size + gap), r.y + 56 + row * (size + gap) + (row > 0 ? 8 : 0), size, size);
                bool locked = i >= inv.Capacity;
                if (locked && inv.Slots[i].Empty) { Fill(sr, new Color(0.03f, 0.03f, 0.03f, 0.6f)); continue; }
                Slot(sr, inv.Slots[i], i == dragSlot || i == modSlot, i < Inventory.HotbarSize ? (i + 1).ToString() : null);
                if (locked) Fill(sr, new Color(0.4f, 0, 0, 0.3f));
                var e = Event.current;
                if (e.type == EventType.MouseDown && sr.Contains(e.mousePosition))
                {
                    if (e.button == 0)
                    {
                        if (dragSlot < 0) { if (!inv.Slots[i].Empty) dragSlot = i; }
                        else { if (!locked || inv.Slots[dragSlot].Empty) inv.Swap(dragSlot, i); dragSlot = -1; game.Player.RefreshTool(); }
                    }
                    else if (e.button == 1 && !inv.Slots[i].Empty) RightClick(i);
                    e.Use();
                }
            }

            // ------------------------------------------------ wapenbank of crafting
            float cx = r.x + 820;
            if (modSlot >= 0 && !inv.Slots[modSlot].Empty && inv.Slots[modSlot].Def.GunDamage > 0) WeaponBench(new Rect(cx, r.y + 12, 440, 610));
            else { modSlot = -1; CraftList(cx, r.y + 12); }
        }

        void RightClick(int i)
        {
            var inv = game.Inventory;
            var d = inv.Slots[i].Def;
            if (d.Kind == ItemKind.Clothing) { game.WearFromSlot(i); return; }
            if (d.GunDamage > 0) { modSlot = modSlot == i ? -1 : i; return; }
            if (d.Kind == ItemKind.Attachment) { Message("Rechtsklik op een wapen om attachments te monteren."); return; }
            var msg = game.Stats.Use(d);
            if (msg != null) Message(msg);
            else Message($"Weggegooid: {d.Name}.");
            inv.TakeFromSlot(i);
            game.Player.RefreshTool();
        }

        void WeaponBench(Rect r)
        {
            var inv = game.Inventory;
            ref var w = ref inv.Slots[modSlot];
            var d = w.Def;
            var st = Arsenal.Stats(w);
            GUI.Label(new Rect(r.x, r.y, r.width, 30), "Wapenbank — " + d.Name, title);
            GUI.Label(new Rect(r.x, r.y + 32, r.width, 60),
                $"Schade {st.Damage:0}{(st.Pellets > 1 ? " × " + st.Pellets : "")}   ·   {60f / st.Interval:0} schoten/min   ·   magazijn {st.MagSize}\n" +
                $"Terugslag {st.RecoilV:0.00}   ·   spreiding {st.Spread:0.0}°   ·   vergroting {st.Zoom:0.0}×   ·   geluid {st.Noise:0} m", small);
            float y = r.y + 80;
            for (int si = 0; si < Arsenal.AttachSlotCount; si++)
            {
                var slot = (AttachSlot)si;
                if ((d.Mounts & Arsenal.MaskOf(slot)) == 0) continue;
                var cur = w.Mod(slot);
                Fill(new Rect(r.x, y, r.width, 26), new Color(0.12f, 0.12f, 0.1f, 0.9f));
                GUI.Label(new Rect(r.x + 8, y + 3, 140, 22), slot.ToString(), label);
                GUI.Label(new Rect(r.x + 130, y + 3, 200, 22), cur != null ? Items.Get(cur).Name : "—", new GUIStyle(label) { normal = { textColor = cur != null ? Accent : Dim } });
                if (cur != null && GUI.Button(new Rect(r.xMax - 70, y + 1, 66, 24), "Eraf", button))
                {
                    if (inv.Add(cur, 1) == 0) { w.SetMod(slot, null); game.Player.RefreshTool(); }
                    else Message("Geen plek in je rugzak.");
                }
                y += 30;
                for (int i = 0; i < inv.Capacity && i < inv.Slots.Length; i++)
                {
                    var a = inv.Slots[i];
                    if (a.Empty || a.Def.Kind != ItemKind.Attachment || a.Def.AttachSlot != slot || !Arsenal.Fits(d, a.Def)) continue;
                    GUI.Label(new Rect(r.x + 24, y, 250, 22), a.Def.Name, small);
                    if (GUI.Button(new Rect(r.xMax - 90, y - 1, 86, 22), "Monteer", button))
                    {
                        string attId = a.Id;
                        inv.TakeFromSlot(i);
                        if (cur != null) inv.Add(cur, 1);
                        inv.Slots[modSlot].SetMod(slot, attId);
                        game.Player.RefreshTool();
                        Message($"{Items.Get(attId).Name} gemonteerd.");
                        return;
                    }
                    y += 24;
                }
                y += 4;
            }
            if (GUI.Button(new Rect(r.x, r.yMax - 40, 140, 32), "Sluiten", button)) modSlot = -1;
        }

        void CraftList(float cx, float top)
        {
            var inv = game.Inventory;
            GUI.Label(new Rect(cx, top, 300, 30), "Maken" + (game.Player.NearFire ? "  (bij het vuur)" : ""), title);
            float y = top + 44;
            foreach (var rec in Crafting.All)
            {
                var def = Items.Get(rec.Result);
                bool can = Crafting.CanCraft(inv, rec, game.Player.NearFire);
                var rr = new Rect(cx, y, 440, 46);
                Fill(rr, new Color(0.1f, 0.1f, 0.08f, 0.8f));
                Fill(new Rect(rr.x + 8, rr.y + 8, 30, 30), IconColor(def));
                GUI.Label(new Rect(rr.x + 48, rr.y + 3, 260, 22), $"{def.Name}{(rec.Count > 1 ? " ×" + rec.Count : "")}{(rec.NeedsFire ? "  (bij vuur)" : "")}", label);
                var needs = new List<string>();
                foreach (var (id, n) in rec.Needs) needs.Add($"{n} {Items.Get(id).Name.ToLowerInvariant()} ({inv.Count(id)})");
                GUI.Label(new Rect(rr.x + 48, rr.y + 24, 300, 20), string.Join(", ", needs), small);
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
                GUI.Label(new Rect(r.x + 56, y + 8, 250, 24), $"{s.Count}× {s.Def.Name}{(s.Def.GunDamage > 0 ? $"  ({s.Ammo})" : "")}{(s.Mods != null ? "  +" : "")}", label);
                if (GUI.Button(new Rect(r.xMax - 100, y + 6, 82, 30), "Pak", button))
                {
                    if (Take(s, out var rest)) { loot.RemoveAt(i); i--; }
                    else { loot[i] = rest; Message("Je rugzak zit vol."); }
                    game.Player.RefreshTool();
                }
                y += 44;
            }
            if (GUI.Button(new Rect(r.x + 18, r.yMax - 48, 180, 32), "Alles pakken", button))
            {
                for (int i = loot.Count - 1; i >= 0; i--)
                {
                    if (Take(loot[i], out var rest)) loot.RemoveAt(i); else loot[i] = rest;
                }
                if (loot.Count > 0) Message("Niet alles past in je rugzak.");
                game.Player.RefreshTool();
            }
            if (GUI.Button(new Rect(r.xMax - 118, r.yMax - 48, 100, 32), "Sluiten", button) || loot.Count == 0) loot = null;
        }

        void TradeWindow(float W, float H)
        {
            var inv = game.Inventory;
            var stock = trade.TraderStock;
            var r = new Rect(W / 2 - 560, H / 2 - 300, 1120, 600);
            Frame(r);
            int caps = inv.Count("doppen");
            GUI.Label(new Rect(r.x + 18, r.y + 12, 700, 30), $"{tradeName} — markt van {trade.Name}", title);
            GUI.Label(new Rect(r.xMax - 260, r.y + 16, 240, 24), $"Jouw doppen: <b>{caps}</b>", new GUIStyle(label) { alignment = TextAnchor.UpperRight });

            // ------------------------------------------------ kopen
            GUI.Label(new Rect(r.x + 18, r.y + 50, 400, 22), "Te koop", label);
            var viewA = new Rect(r.x + 18, r.y + 76, 520, 460);
            float rowH = 34;
            tradeScrollA = GUI.BeginScrollView(viewA, tradeScrollA, new Rect(0, 0, 500, stock.Count * rowH));
            for (int i = 0; i < stock.Count; i++)
            {
                var st = stock[i];
                var d = st.Def;
                int price = Items.ValueOf(d);
                float y = i * rowH;
                Fill(new Rect(0, y, 500, rowH - 4), new Color(0.1f, 0.1f, 0.08f, 0.8f));
                Fill(new Rect(6, y + 6, 18, 18), IconColor(d));
                GUI.Label(new Rect(32, y + 5, 300, 22), $"{d.Name}{(st.Count > 1 ? $"  ×{st.Count}" : "")}{(st.Mods != null ? "  +" : "")}", label);
                GUI.Label(new Rect(330, y + 5, 70, 22), $"{price} d", new GUIStyle(label) { alignment = TextAnchor.UpperRight, normal = { textColor = Accent } });
                GUI.enabled = caps >= price;
                if (GUI.Button(new Rect(410, y + 3, 84, 24), "Koop", button))
                {
                    var one = st; one.Count = 1;
                    bool ok = d.MaxStack == 1 ? inv.AddStack(one) : inv.Add(st.Id, 1) == 0;
                    if (ok)
                    {
                        inv.Remove("doppen", price);
                        st.Count--;
                        if (st.Count <= 0) { stock.RemoveAt(i); i--; } else stock[i] = st;
                        Sfx.Instance.Play2D("droog", 0.4f, 0.8f);
                        game.Player.RefreshTool();
                    }
                    else Message("Je rugzak zit vol.");
                }
                GUI.enabled = true;
            }
            GUI.EndScrollView();

            // ------------------------------------------------ verkopen
            GUI.Label(new Rect(r.x + 570, r.y + 50, 400, 22), "Verkopen (halve prijs)", label);
            var viewB = new Rect(r.x + 570, r.y + 76, 530, 460);
            var mine = new List<int>();
            for (int i = 0; i < inv.Slots.Length; i++) if (!inv.Slots[i].Empty && inv.Slots[i].Id != "doppen") mine.Add(i);
            tradeScrollB = GUI.BeginScrollView(viewB, tradeScrollB, new Rect(0, 0, 510, mine.Count * rowH));
            for (int k = 0; k < mine.Count; k++)
            {
                int i = mine[k];
                var st = inv.Slots[i];
                var d = st.Def;
                int price = Mathf.Max(1, Items.ValueOf(d) / 2);
                float y = k * rowH;
                Fill(new Rect(0, y, 510, rowH - 4), new Color(0.1f, 0.1f, 0.08f, 0.8f));
                Fill(new Rect(6, y + 6, 18, 18), IconColor(d));
                GUI.Label(new Rect(32, y + 5, 300, 22), $"{d.Name}{(st.Count > 1 ? $"  ×{st.Count}" : "")}", label);
                GUI.Label(new Rect(330, y + 5, 70, 22), $"{price} d", new GUIStyle(label) { alignment = TextAnchor.UpperRight, normal = { textColor = Accent } });
                if (GUI.Button(new Rect(410, y + 3, 94, 24), "Verkoop", button))
                {
                    var one = st; one.Count = 1;
                    inv.TakeFromSlot(i);
                    if (d.MaxStack == 1) stock.Add(one);
                    else
                    {
                        int idx = stock.FindIndex(x => x.Id == st.Id);
                        if (idx >= 0) { var x = stock[idx]; x.Count++; stock[idx] = x; } else stock.Add(new Stack(st.Id, 1));
                    }
                    inv.Add("doppen", price);
                    Sfx.Instance.Play2D("droog", 0.4f, 1.2f);
                    game.Player.RefreshTool();
                }
            }
            GUI.EndScrollView();
            if (GUI.Button(new Rect(r.xMax - 130, r.yMax - 46, 110, 32), "Sluiten", button)) trade = null;
        }

        void VehicleWindow(float W, float H)
        {
            var v = vehicle;
            var inv = game.Inventory;
            var r = new Rect(W / 2 - 300, H / 2 - 230, 600, 460);
            Frame(r);
            GUI.Label(new Rect(r.x + 18, r.y + 12, 560, 30), v.Def.Name + (v.Health <= 0 ? " (total loss)" : ""), title);
            GUI.Label(new Rect(r.x + 18, r.y + 46, 560, 22), $"Staat {v.Health:0}%" + (v.Def.NeedsFuel ? $"   ·   Benzine {v.Fuel:0.0} / {v.Def.FuelCapacity:0} l" : ""), label);
            float y = r.y + 84;
            if (v.Def.Parts.Length == 0) { GUI.Label(new Rect(r.x + 18, y, 560, 22), "Heeft geen onderdelen nodig.", small); y += 30; }
            foreach (var part in v.Def.Parts)
            {
                string item = Vehicles.PartItem(part);
                int need = Vehicles.PartCount(part);
                bool ok = v.PartOk[(int)part];
                Fill(new Rect(r.x + 18, y, 564, 34), new Color(0.1f, 0.1f, 0.08f, 0.8f));
                GUI.Label(new Rect(r.x + 28, y + 6, 220, 22), part.ToString(), label);
                GUI.Label(new Rect(r.x + 200, y + 6, 220, 22), ok ? "<color=#8fd06a>in orde</color>" : $"<color=#e0503c>ontbreekt</color>  ({need}× {Items.Get(item).Name.ToLowerInvariant()}, je hebt {inv.Count(item)})", label);
                if (!ok)
                {
                    GUI.enabled = inv.Count(item) >= need && v.Health > 0;
                    if (GUI.Button(new Rect(r.xMax - 118, y + 4, 96, 26), "Monteer", button))
                    {
                        inv.Remove(item, need);
                        v.PartOk[(int)part] = true;
                        game.Vehicles.Refresh(v);
                        Sfx.Instance.Play2D("herladen", 0.7f, 0.8f);
                        Message($"{part} gemonteerd.");
                    }
                    GUI.enabled = true;
                }
                y += 40;
            }
            if (v.Def.NeedsFuel)
            {
                GUI.enabled = inv.Count("jerrycan") > 0 && v.Fuel < v.Def.FuelCapacity - 1;
                if (GUI.Button(new Rect(r.x + 18, y + 6, 240, 32), "Tanken met jerrycan (+20 l)", button))
                {
                    inv.Remove("jerrycan", 1);
                    v.Fuel = Mathf.Min(v.Def.FuelCapacity, v.Fuel + 20);
                    Message("Getankt.");
                }
                GUI.enabled = true;
                y += 46;
            }
            GUI.enabled = v.Drivable;
            if (GUI.Button(new Rect(r.x + 18, r.yMax - 52, 200, 36), v.Def.Boat ? "Instappen en varen" : "Instappen en rijden", button))
            {
                vehicle = null;
                game.Player.EnterVehicle(v);
            }
            GUI.enabled = true;
            if (!v.Drivable) GUI.Label(new Rect(r.x + 230, r.yMax - 46, 250, 22), v.Health <= 0 ? "Niet meer te redden." : "Nog niet rijklaar.", small);
            if (GUI.Button(new Rect(r.xMax - 118, r.yMax - 52, 100, 36), "Sluiten", button)) vehicle = null;
        }

        void DrivingHud(float W, float H)
        {
            var v = game.Player.Vehicle;
            var r = new Rect(W - 280, H - 120, 258, 98);
            Frame(r);
            GUI.Label(new Rect(r.x + 14, r.y + 6, 230, 22), v.Def.Name, label);
            GUI.Label(new Rect(r.x + 14, r.y + 26, 230, 44), $"<size=30><b>{Mathf.Abs(v.Speed) * 3.6f:0}</b></size> <color=#a8a08a>km/u</color>", label);
            if (v.Def.NeedsFuel)
            {
                Fill(new Rect(r.x + 14, r.y + 76, 150, 8), new Color(0, 0, 0, 0.6f));
                Fill(new Rect(r.x + 14, r.y + 76, 150 * v.Fuel / v.Def.FuelCapacity, 8), v.Fuel < 5 ? new Color(0.9f, 0.3f, 0.2f) : Accent);
                GUI.Label(new Rect(r.x + 170, r.y + 70, 80, 20), $"{v.Fuel:0.0} l", small);
            }
            GUI.Label(new Rect(r.xMax - 90, r.y + 8, 80, 20), $"{v.Health:0}%", new GUIStyle(small) { alignment = TextAnchor.UpperRight });
        }

        void Prompts(float W, float H)
        {
            var pl = game.Player;
            var cam = pl.Cam.transform;
            var v = game.Vehicles.LookedAt(cam.position, cam.forward, 4.5f + Vector3.Distance(cam.position, pl.transform.position + Vector3.up * 1.5f));
            if (v != null)
            {
                string state = v.Drivable ? "rijklaar" : v.Health <= 0 ? "total loss" : $"mist {v.MissingParts().Count} onderdelen" + (v.Def.NeedsFuel && v.Fuel <= 0 ? ", geen benzine" : "");
                GUI.Label(new Rect(0, H / 2 + 60, W, 22), $"E — {v.Def.Name} ({state})", center);
            }
            if (pl.Fishing) GUI.Label(new Rect(0, H / 2 + 84, W, 22), pl.Bite ? "<b>BEET! Klik!</b>" : "Wachten op een beet…", center);
        }

        /// <summary>Stopt een stapel in de rugzak, met behoud van magazijn en attachments.</summary>
        bool Take(Stack s, out Stack rest)
        {
            rest = s;
            if (s.Def.MaxStack == 1) return game.Inventory.AddStack(s);
            int left = game.Inventory.Add(s.Id, s.Count);
            rest.Count = left;
            return left == 0;
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
            GUI.Label(new Rect(0, H * 0.35f + 64, W, 28), $"Je overleefde tot {game.Clock.Label} en schakelde {game.Combat.Kills} vijanden uit.", center);
            if (GUI.Button(new Rect(W / 2 - 120, H * 0.35f + 120, 240, 40), "Opnieuw beginnen", button)) game.Respawn();
        }
    }
}
