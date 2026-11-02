using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Arena
{
    /// The in-match HUD (IMGUI): wave plate, player cards, boss bars, ability bar, Tab menu, the upgrade picker and
    /// the stats sidebar, banners and the end-of-run summary.
    public partial class ArenaApp
    {
        static readonly string[] SlotKeys = { "SPACE", "Q", "E", "SHIFT", "F" };

        void DrawHud()
        {
            var me = game.Me;
            DrawJuiceHud();  // damage numbers and a boss's entrance card, under the rest of the HUD (Juice.cs)
            DrawAbilityCallouts();  // what each player just cast, over their head (AbilityLooks.cs)
            HudGroup(1, new Vector2(-150f, 30f), new Rect(SideL, SideY, SideW, SideH), DrawWaveCorner);
            HudGroup(3, new Vector2(0f, -80f), new Rect(W / 2f - 320f, 0f, 640f, 60f), () =>
            {
                if (game.Recorder != null)  // this match is being recorded (hosts only)
                    Ui.Label(new Rect(W - 70f, 34f, 62f, 16f), "● REC", 10, new Color(1f, 0.4f, 0.4f, 0.8f), TextAnchor.MiddleRight, true);
                DrawBossBars();
            });
            DrawStageBanner();  // a boss changing stage (StageShiftLook.cs)
            DrawMiniBossBars(1f - hudT);  // the HUD hidden (H): a slim bar per boss stays at the top (HealthBars.cs)
            DrawWardenTags();   // "EXPOSED" / "STUNNED" over the Warden (WardenLook.cs)
            DrawUnlockToasts(W / 2f, 150f);  // SavesUi.cs
            if (aimDebug && me != null)
                DrawAimDebug(me);

            // Names, level-up popups and downed labels over players (hidden while they're under
            // the between-waves banner, so they don't show on top of its text).
            bool banner = game.State == MatchState.Intermission && (me == null || me.Offer == null);
            foreach (var p in game.Players.Values)
            {
                float r = p.Def.BodyR;
                V2 at = p.Disp * WorldToUi;  // arena -> HUD coordinates
                r *= WorldToUi;
                if (banner && !LaunchHoldsBanner && at.X > BannerRect.x - 100f && at.X < BannerRect.xMax + 100f && at.Y > BannerRect.y - 20f && at.Y < BannerRect.yMax + r + 60f)
                    continue;
                if (LaunchHides(p))
                    continue;  // not landed yet (LaunchIntro.cs)
                DrawLaunchIcon(p, at, r);
                Ui.Shadowed(new Rect(at.X - 100f, at.Y - r - 30f, 200f, 18f), p.Name + (p.Id == game.MyId ? " (you)" : ""), 12,
                    ToColor(p.Def.Color.WithAlpha(0.95f)));
                if (p.LevelFlash > 0f)
                {
                    float rise = (1.5f - p.LevelFlash) * 22f;
                    Ui.Shadowed(new Rect(at.X - 100f, at.Y - r - 58f - rise * (Juicy ? 2f : 1f), 200f, 30f), "LEVEL UP!", Juicy ? 25 : 19,
                        new Color(1f, 0.88f, 0.35f, Mathf.Min(1f, p.LevelFlash)));
                }
                if (p.Downed)
                {
                    var tag = new Rect(at.X - 92f, at.Y + r + 10f, 184f, 34f);
                    Ui.HudPanel(tag, new Color(1f, 0.35f, 0.35f));
                    Ui.Label(new Rect(tag.x, tag.y + 3f, tag.width, 14f), "DOWNED — stand close to revive", 10, new Color(1f, 0.7f, 0.7f), TextAnchor.MiddleCenter, true);
                    Ui.Bar(new Rect(tag.x + 10f, tag.y + 21f, tag.width - 20f, 6f), p.Revive, Ui.Good);
                }
            }

            HudGroup(2, new Vector2(150f, 30f), new Rect(SideR, SideY, SideW, SideH), DrawPlayerCorner);
            if (me != null)
                HudGroup(0, new Vector2(0f, 110f), new Rect(W / 2f - 391f, H - 92f, 782f, 80f), () => DrawAbilityBar(me));
            DrawTreasureHud(me);  // your chest item's card, or what the wave is waiting for (PropsLook.cs)

            switch (game.State)
            {
                case MatchState.Intermission:
                    if (me != null && me.Offer != null)
                    {
                        if (!tabHeld && !PickOutroPlaying)
                            DrawUpgradePicker(me);  // holding Tab swaps the cards for the Tab menu; the next pick waits for the last one's outro (PickOutro.cs)
                        break;
                    }
                    if (game.PathOffer != null && !tabHeld)
                    {
                        DrawPathChoice(Time.unscaledTime);  // PathsUi.cs
                        break;
                    }
                    if (game.ShopOpen && me != null)
                    {
                        if (!tabHeld)
                            DrawShop(me, Time.unscaledTime);  // ShopUi.cs
                        break;
                    }
                    var choosing = new List<string>();
                    foreach (var p in game.Players.Values)
                        if (p.PendingPicks + p.PendingLegendary + p.PendingMods > 0 && p.Id != game.MyId)
                            choosing.Add(p.Name);
                    var pr = game.Practice;
                    int pNext = pr == null ? 0 : (game.PracticeIndex + 1) % Math.Max(1, pr.Waves.Count);
                    string sub = choosing.Count > 0 ? "Waiting for " + string.Join(" and ", choosing.ToArray()) + " to choose an upgrade…"
                        : game.AnyonePicking() ? ""
                        : pr != null ? "Practice wave " + (pNext + 1) + " starts in " + Mathf.CeilToInt(game.StateTimer)
                        : ApproachLine(game.Wave + 1) + "Wave " + (game.Wave + 1) + " starts in " + Mathf.CeilToInt(game.StateTimer);  // ApproachUi.cs
                    if (LaunchHoldsBanner)
                        break;  // a run launching: GET READY waits until the players have landed (LaunchIntro.cs)
                    if (pr != null)
                        DrawBanner(game.PracticeIndex < 0 ? "PRACTICE RANGE" : "PRACTICE WAVE " + (game.PracticeIndex + 1) + " CLEARED", sub, Ui.Accent);
                    else
                        DrawBanner(game.Wave == 0 ? "GET READY" : "WAVE " + game.Wave + " CLEARED", sub, game.Wave > 0 && game.Wave % 5 == 0 ? Ui.Gold : Ui.Accent);
                    break;
                case MatchState.Victory:
                case MatchState.GameOver:
                    if (!pauseMenuOpen && !tabHeld)
                        DrawRunSummary();
                    break;
            }
            if (tabHeld)
                DrawRunStatsScreen(me, false);  // RunStatsUi.cs
            else if (me != null && game.State != MatchState.GameOver && game.State != MatchState.Victory)
            {
                HudGroup(3, new Vector2(0f, -60f), new Rect(W - 290f, 10f, 274f, 22f), () =>
                {
                    var hint = new Rect(W - 290f, 10f, 274f, 22f);
                    Ui.Pill(hint, new Color(0f, 0f, 0f, 0.35f));
                    Ui.Label(hint, "Hold TAB  ·  run stats & build      H  ·  hide HUD", 11, new Color(1f, 1f, 1f, 0.55f), TextAnchor.MiddleCenter, true);
                });
                // With the HUD hidden, a quiet reminder of how to get it back, so nobody's locked out of it.
                float back = Mathf.Clamp01((0.35f - hudT) / 0.35f);
                if (back > 0f && !LaunchActive)  // (not while a run launches: the HUD's on its way in)
                {
                    float breathe = 0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * 2f);
                    Ui.Shadowed(new Rect(0f, H - 30f + 8f * (1f - back), W, 18f), "Press H to show the HUD", 12, new Color(1f, 1f, 1f, 0.38f * back * breathe));
                }
            }
        }

        // --- Hiding the HUD (H) ---------------------------------------------------------

        bool hudShown = true;
        float hudT = 1f;               // 0 hidden .. 1 shown, eased in Update
        const float HudFadeTime = 0.55f, HudStagger = 0.14f;
        const int HudGroups = 4;       // 0 ability bar, 1 wave panel, 2 player cards, 3 top row (boss bars, hints)

        /// One piece of the HUD, played in its turn: showing, the ability bar rises first, then the side panels slide
        /// in, then the top row drops; hiding runs the other way. Each piece slides, scales up from 90% and fades,
        /// overshooting a little as it lands (and pulling back before it leaves).
        void HudGroup(int order, Vector2 away, Rect pivot, Action draw)
        {
            float p = Mathf.Clamp01(hudT * (1f + HudStagger * (HudGroups - 1)) - HudStagger * order);
            if (p <= 0f)
                return;
            if (p >= 1f)
            {
                draw();
                return;
            }
            const float back = 1.70158f;  // ease-out-back
            float q = p - 1f, e = 1f + (back + 1f) * q * q * q + back * q * q;
            float k = 0.9f + 0.1f * e, a = p * p * (3f - 2f * p);
            Vector2 c = pivot.center;
            float ox = away.x * (1f - e) + c.x * (1f - k), oy = away.y * (1f - e) + c.y * (1f - k);
            var oldMatrix = GUI.matrix;
            var oldColor = GUI.color;
            GUI.matrix = Matrix4x4.TRS(new Vector3(offX + ox * scale, offY + oy * scale, 0f), Quaternion.identity, new Vector3(scale * k, scale * k, 1f));
            GUI.color = new Color(oldColor.r, oldColor.g, oldColor.b, oldColor.a * a);
            Ui.Alpha = a;
            draw();
            Ui.Alpha = 1f;
            GUI.color = oldColor;
            GUI.matrix = oldMatrix;
        }

        // --- Tab: your stats and loadout ---------------------------------------

        /// Held with Tab. Only drawn on this player's screen; nothing is sent to the
        /// other player and the game doesn't pause.
        void DrawTabMenu(Player me, bool dim = true)
        {
            TipClear();  // hovering the weapon or an upgrade shows what it does (Tooltips.cs)
            if (dim)  // (the Run stats screen draws one backdrop itself while it switches pages: RunStatsUi.cs)
                Ui.Fill(new Rect(0, 0, W, H), new Color(0.01f, 0.012f, 0.03f, 0.6f));
            DrawTabStats(me, StatsSidebar(me));
            var panel = new Rect(StatsSidebarW + 18f, 54f, W - StatsSidebarW - 18f - 24f, 640f);
            Ui.Panel(panel, true);
            Ui.Title(new Rect(panel.x, panel.y + 12f, panel.width, 34f), "YOUR LOADOUT", 26, Color.white);
            Ui.Label(new Rect(panel.x, panel.y + 46f, panel.width, 18f),
                Defs.KitName(me.Kit) + " " + me.Def.Name + "   ·   Level " + me.Level + "   ·   " + me.Rerolls + " reroll" + (me.Rerolls == 1 ? "" : "s") + " left",
                13, ToColor(me.Def.Color), TextAnchor.MiddleCenter, true);
            float top = panel.y + 76f, bottom = panel.yMax - 16f;
            const float pad = 16f, gap = 14f, abilW = 330f;
            var abilBox = new Rect(panel.x + pad, top, abilW, bottom - top);
            var upBox = new Rect(abilBox.xMax + gap, top, panel.xMax - pad - abilBox.xMax - gap, bottom - top);
            DrawTabAbilities(me, abilBox);
            DrawTabUpgrades(me, upBox);
            DrawAbilityHover(abilBox);
            DrawTip();
        }

        /// Stats, grouped so they're quick to scan (in the stats sidebar).
        void DrawTabStats(Player me, Rect box)
        {
            var lines = new Dictionary<string, string>();
            foreach (var line in StatSheet.Describe(me, me.Taken, 0, game.PassiveRegen))
                if (!line.Header && !lines.ContainsKey(line.Label))
                    lines[line.Label] = line.Value;
            Func<string, string> get = k => lines.ContainsKey(k) ? lines[k] : "";
            string charges = "";
            foreach (var kv in lines)
                if (kv.Key.EndsWith(" charges") && kv.Key != "Bonus charges")
                    charges = kv.Value;
            var groups = new[]
            {
                new KeyValuePair<string, string[,]>("OFFENSE", new string[,] { { "Damage", get("Damage") }, { "Fire rate", get("Fire rate") }, { "Extra projectiles", get("Extra projectiles") }, { "Pierce", get("Pierce") } }),
                new KeyValuePair<string, string[,]>("DEFENSE", new string[,] { { "Health", Mathf.CeilToInt(me.Hp) + " / " + Mathf.CeilToInt(me.MaxHp) }, { "Damage taken", get("Damage taken") }, { "Regeneration", get("Regeneration") }, { "Parry durability", get("Parry durability") } }),
                new KeyValuePair<string, string[,]>("UTILITY", new string[,] { { "Move speed", get("Move speed") }, { "Ability cooldowns", get("Ability cooldowns") }, { "Space charges", charges } }),
            };
            float y = box.y + 12f;
            foreach (var g in groups)
            {
                Ui.Label(new Rect(box.x + 16f, y, box.width - 32f, 16f), g.Key, 11, Ui.Gold, TextAnchor.MiddleLeft, true);
                y += 22f;
                for (int i = 0; i < g.Value.GetLength(0); i++)
                {
                    var row = new Rect(box.x + 10f, y, box.width - 20f, 28f);
                    if (i % 2 == 0)
                        Ui.Fill(row, new Color(1f, 1f, 1f, 0.035f));
                    Ui.Label(new Rect(row.x + 8f, row.y, row.width - 16f, row.height), g.Value[i, 0], 14, Ui.Muted);
                    Ui.Label(new Rect(row.x + 8f, row.y, row.width - 16f, row.height), g.Value[i, 1], 15, Color.white, TextAnchor.MiddleRight, true);
                    y += 28f;
                }
                y += 14f;
            }
        }

        /// The weapon and each ability on its own tile.
        /// The hovered Tab ability tile's full text, beside the abilities box (drawn after the upgrades so it's on top).
        void DrawAbilityHover(Rect box)
        {
            if (hoverAbility == null)
                return;
            {
                // The hovered ability in full: what it does and what Lv 5 adds.
                const float pw = 320f;
                string cap = "Mastered (Lv 5): " + Game.Capstone(hoverAbility.Id);
                float dh = Ui.TextHeight(hoverAbility.Desc, 12, pw - 28f), ch = Ui.TextHeight(cap, 11, pw - 42f);
                var pop = new Rect(box.xMax + 10f, hoverTile.y, pw, 44f + dh + 6f + ch + 12f);
                pop.y = Mathf.Min(pop.y, H - 12f - pop.height);
                Ui.Panel(pop, true);
                Ui.Label(new Rect(pop.x + 14f, pop.y + 10f, pw - 28f, 24f), hoverAbility.Name, 16, Color.white, TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(pop.x + 14f, pop.y + 40f, pw - 28f, dh), hoverAbility.Desc, 12, Ui.Text, TextAnchor.UpperLeft, false, true);
                Ui.Icon(new Rect(pop.x + 14f, pop.y + 47f + dh, 10f, 10f), PixelArt.Get("ui_star"), Ui.Gold);
                Ui.Label(new Rect(pop.x + 28f, pop.y + 46f + dh, pw - 42f, ch), cap, 11, new Color(0.85f, 0.75f, 0.48f), TextAnchor.UpperLeft, false, true);
            }
        }

        AbilityDef hoverAbility;
        Rect hoverTile;

        void DrawTabAbilities(Player me, Rect box)
        {
            hoverAbility = null;
            Ui.HudPanel(box);
            float x = box.x + 12f, w = box.width - 24f, y = box.y + 12f;
            Ui.Label(new Rect(x + 4f, y, w, 16f), "WEAPON", 11, Ui.Gold, TextAnchor.MiddleLeft, true);
            y += 20f;
            float wdh = Mathf.Min(44f, Ui.TextHeight(me.Weapon.Desc, 11, w - 8f));  // at most three lines (hover for all of it)
            TipWeapon(new Rect(x, y - 2f, w, 26f + wdh), me);
            Ui.Label(new Rect(x + 4f, y, w, 22f), me.Weapon.Name, 17, Color.white, TextAnchor.MiddleLeft, true);
            GUI.BeginGroup(new Rect(x + 4f, y + 22f, w - 8f, wdh));
            Ui.Label(new Rect(0f, 0f, w - 8f, Ui.TextHeight(me.Weapon.Desc, 11, w - 8f)), me.Weapon.Desc, 11, Ui.Muted, TextAnchor.UpperLeft, false, true);
            GUI.EndGroup();
            y += 30f + wdh;
            Ui.Label(new Rect(x + 4f, y, w, 16f), "ABILITIES", 11, Ui.Gold, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(x + 4f, y, w - 8f, 16f), "click a key to move an ability", 10, Ui.Muted, TextAnchor.MiddleRight);
            y += 22f;
            // Tiles grow to fit each ability's description and what mastering it adds, when there's room.
            int filled = 0;
            foreach (var sl in me.Slots)
                if (sl.Id != AbilityId.None)
                    filled++;
            float tileH = Mathf.Clamp((box.yMax - 10f - y) / Mathf.Max(1, filled) - 4f, 80f, 118f);
            for (int i = 0; i < me.Slots.Length; i++)
            {
                var slot = me.Slots[i];
                if (slot.Id == AbilityId.None)
                    continue;
                var a = Upgrades.Ability(slot.Id);
                bool ult = i == Upgrades.UltSlot;
                var tile = new Rect(x, y, w, tileH);
                Ui.Fill(tile, new Color(1f, 1f, 1f, 0.04f));
                DrawKeyCap(new Rect(tile.x + 8f, tile.y + 8f, 0f, 18f), ult ? "ULT · F" : SlotKeys[i], ult ? Ui.Gold : ToColor(me.Def.Color));
                Ui.Label(new Rect(tile.x + 8f, tile.y + 28f, tile.width - 16f, 20f), a.Name, 15, Color.white, TextAnchor.MiddleLeft, true);
                string state = slot.Charges > 0 ? "Ready" : Mathf.CeilToInt(slot.Timer) + "s";
                if (slot.MaxCharges > 1)
                    state = slot.Charges + "/" + slot.MaxCharges + " charges" + (slot.Charges < slot.MaxCharges ? "  ·  " + Mathf.CeilToInt(slot.Timer) + "s" : "");
                Ui.Label(new Rect(tile.x + 8f, tile.y + 8f, tile.width - 16f, 18f), state, 12, slot.Charges > 0 ? Ui.Good : Ui.Muted, TextAnchor.MiddleRight, true);
                bool bonus = i >= 1 && i <= Upgrades.BonusSlots;
                // Mastery: level pips by the name; hovering the tile shows what level 5 unlocks.
                int level = Game.MasteryLevel(me, slot.Id);
                float xp;
                me.MasteryXp.TryGetValue(slot.Id, out xp);
                float capW = 12f + (ult ? "ULT · F" : SlotKeys[i]).Length * 6.5f;  // same width DrawKeyCap uses
                DrawMasteryPips(new Rect(tile.x + 18f + capW, tile.y + 14f, 0f, 6f), level, Game.MasteryProgress(xp));
                Ui.Label(new Rect(tile.x + 18f + capW + 68f, tile.y + 8f, 70f, 18f), level >= Game.MasteryMax ? "MASTERED" : "Lv " + level, 10,
                    level >= Game.MasteryMax ? Ui.Gold : Ui.Muted, TextAnchor.MiddleLeft, true);
                bool hover = tile.Contains(Event.current.mousePosition);
                var descR = new Rect(tile.x + 8f, tile.y + 47f, tile.width - 16f - (bonus ? 118f : 0f), tile.height - 50f);
                DrawDescAndMastery(descR, a.Desc, slot.Id, 11, Ui.Muted);  // what it does, then what Lv 5 adds (LobbyUi.cs)
                if (tile.Contains(Event.current.mousePosition))
                {
                    hoverAbility = a;  // the full text, beside the box, when the tile has had to cut it short
                    hoverTile = tile;
                }
                if (bonus)  // move it to another key (swaps with whatever is there)
                {
                    Ui.Label(new Rect(tile.xMax - 122f, tile.y + 30f, 114f, 14f), "MOVE TO", 9, Ui.Muted, TextAnchor.MiddleRight, true);
                    for (int k = 1; k <= Upgrades.BonusSlots; k++)
                    {
                        var kr = new Rect(tile.xMax - 122f + (k - 1) * 39f, tile.y + 47f, k == 3 ? 44f : 34f, 24f);
                        if (k == i)
                            Ui.Pill(kr, new Color(1f, 1f, 1f, 0.12f));
                        bool over = k != i && kr.Contains(Event.current.mousePosition);
                        if (k != i)
                            Ui.Pill(kr, over ? new Color(0.35f, 0.55f, 1f, 0.85f) : new Color(0.28f, 0.32f, 0.45f, 0.9f));
                        Ui.Label(kr, SlotKeys[k], 10, Color.white, TextAnchor.MiddleCenter, true);
                        if (k != i && Ui.HitArea(kr))
                            game.SetAbilityKey(slot.Id, k);
                    }
                }
                y += tileH + 4f;
            }
        }

        /// Every upgrade picked, grouped by rarity (rarest first) in two columns.
        void DrawTabUpgrades(Player me, Rect box)
        {
            Ui.HudPanel(box);
            var counts = new Dictionary<int, int>();
            foreach (int id in me.Taken)
                counts[id] = counts.ContainsKey(id) ? counts[id] + 1 : 1;
            // A flat list of headers (id < 0, rarity in -id - 1) and upgrades, rarest first.
            var items = new List<int>();
            for (int rr = (int)Rarity.Mod; rr >= 0; rr--)
            {
                var group = new List<int>();
                foreach (var id in counts.Keys)
                    if ((int)Upgrades.All[id].Rarity == rr)
                        group.Add(id);
                if (group.Count == 0)
                    continue;
                group.Sort((a, b) => string.CompareOrdinal(Upgrades.All[a].Name, Upgrades.All[b].Name));
                items.Add(-rr - 1);
                items.AddRange(group);
            }
            Ui.Label(new Rect(box.x + 16f, box.y + 12f, box.width - 32f, 16f), "UPGRADES", 11, Ui.Gold, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(box.x + 16f, box.y + 12f, box.width - 32f, 16f), me.Taken.Count + " picks", 11, Ui.Muted, TextAnchor.MiddleRight, true);
            if (items.Count == 0)
            {
                Ui.Label(new Rect(box.x + 16f, box.y + 40f, box.width - 32f, 22f), "None yet: level up to pick some.", 13, Ui.Muted);
                return;
            }
            const float rowH = 23f, colGap = 14f;
            float listTop = box.y + 36f, colW = (box.width - 24f - colGap) / 2f;
            int perCol = Mathf.FloorToInt((box.yMax - 10f - listTop) / rowH);
            int maxScroll = Math.Max(0, items.Count - perCol * 2);
            if (Event.current.type == EventType.ScrollWheel)
            {
                tabScroll += Event.current.delta.y > 0f ? 2 : -2;
                Event.current.Use();
            }
            tabScroll = Mathf.Clamp(tabScroll, 0, maxScroll);
            if (maxScroll > 0)
                Ui.Label(new Rect(box.x + 16f, box.yMax - 20f, box.width - 32f, 14f), "scroll for more", 10, Ui.Muted, TextAnchor.MiddleRight);
            for (int k = 0; k < perCol * 2 && tabScroll + k < items.Count; k++)
            {
                int item = items[tabScroll + k];
                float cx = box.x + 12f + (k / perCol) * (colW + colGap), cy = listTop + (k % perCol) * rowH;
                if (item < 0)
                {
                    var rarity = (Rarity)(-item - 1);
                    Color rc = ToColor(Upgrades.RarityColor(rarity));
                    Ui.Pill(new Rect(cx, cy + 9f, 3f, 8f), rc);
                    Ui.Label(new Rect(cx + 9f, cy + 3f, colW - 9f, 20f), Upgrades.RarityName(rarity), 11, rc, TextAnchor.MiddleLeft, true);
                    Ui.Fill(new Rect(cx, cy + rowH - 1f, colW, 1f), new Color(rc.r, rc.g, rc.b, 0.25f));
                    continue;
                }
                var u = Upgrades.All[item];
                TipUpgrade(new Rect(cx - 2f, cy, colW + 4f, rowH), me, u, counts[item]);  // hover: what it does (Tooltips.cs)
                Ui.Icon(new Rect(cx + 2f, cy + 3f, 18f, 18f), PixelArt.ForUpgrade(u.Name));
                Ui.Label(new Rect(cx + 26f, cy, colW - 60f, rowH), u.Name, 13, Ui.Text, TextAnchor.MiddleLeft);
                if (counts[item] > 1)
                {
                    var badge = new Rect(cx + colW - 30f, cy + 4f, 28f, 16f);
                    Ui.Pill(badge, new Color(1f, 1f, 1f, 0.12f));
                    Ui.Label(badge, "x" + counts[item], 10, Color.white, TextAnchor.MiddleCenter, true);
                }
            }
        }

        // --- End of run ---------------------------------------------------------

        struct SummaryRow
        {
            public string Label, Award;
            public Func<RunStats, float> Value;
            public Func<RunStats, string> Format;
        }

        static readonly SummaryRow[] SummaryRows =
        {
            new SummaryRow { Label = "Damage dealt", Award = "Most damage", Value = s => s.DamageDealt, Format = s => Big(s.DamageDealt) },
            new SummaryRow { Label = "Kills", Award = "Most kills", Value = s => s.Kills, Format = s => s.Kills + (s.EliteKills + s.BossKills > 0 ? "  (" + s.EliteKills + " elite, " + s.BossKills + " boss)" : "") },
            new SummaryRow { Label = "Biggest hit", Award = "Heavy hitter", Value = s => s.BiggestHit, Format = s => Big(s.BiggestHit) },
            new SummaryRow { Label = "Accuracy", Award = "Sharpshooter", Value = s => s.Accuracy, Format = s => Mathf.RoundToInt(s.Accuracy * 100f) + "%" },
            new SummaryRow { Label = "Healing done", Award = "Best healer", Value = s => s.HealingDone, Format = s => Big(s.HealingDone) },
            new SummaryRow { Label = "Allies revived", Award = "Lifesaver", Value = s => s.Revives, Format = s => s.Revives.ToString() },
            new SummaryRow { Label = "Damage taken", Award = "Damage sponge", Value = s => s.DamageTaken, Format = s => Big(s.DamageTaken) },
            new SummaryRow { Label = "Times downed", Award = null, Value = s => s.Downs, Format = s => s.Downs.ToString() },
            new SummaryRow { Label = "Abilities used", Award = null, Value = s => s.AbilitiesUsed, Format = s => s.AbilitiesUsed.ToString() },
        };

        static string Big(float v) => v >= 10000f ? (v / 1000f).ToString("0.#") + "k" : Mathf.RoundToInt(v).ToString();

        /// The end of a run, in the level-up picker's "trophy" style: a big outlined title (GAME OVER in red, VICTORY! in
        /// gold), the run's totals as tiles, a trophy card per player (their class icon big on a hatched backdrop in their
        /// colour, level and awards beside it, then their numbers with bars), and the lobby's fancy buttons.
        void DrawRunSummary()
        {
            bool won = game.State == MatchState.Victory;
            float now = Time.unscaledTime;
            // Everything comes in as the run's ending plays out (RunEnding.cs): the dark layer, the title stamped in the
            // middle and gliding up, then the tiles, the cards and the buttons.
            Ui.Fill(new Rect(0, 0, W, H), new Color(0.01f, 0.012f, 0.03f, 0.84f * EndBg));
            Color theme = won ? Ui.Gold : new Color(1f, 0.36f, 0.38f);
            string title = game.Practice != null ? (won ? "PRACTICE COMPLETE" : "PRACTICE OVER") : won ? "VICTORY!" : "GAME OVER";
            float titleIn = EndTitleIn;
            if (titleIn > 0f)
            {
                float ts = EndTitleScale, ty = Mathf.Lerp(H / 2f - 33f, 22f, EndLift);
                int baseSize = game.Practice != null ? 46 : 56;
                var shake = EndTitleShake();
                var tr = new Rect(shake.x - W * (ts - 1f) / 2f, ty + shake.y - 33f * (ts - 1f), W * ts, 66f * ts);
                EndPart(titleIn, Vector2.zero, () =>
                    Ui.Fancy(tr, title, Mathf.RoundToInt(baseSize * ts), Color.white, theme, TextAnchor.MiddleCenter, new Color(theme.r, theme.g, theme.b, 0.55f)));
            }
            var oldAlpha = Ui.Alpha;
            var oldColor = GUI.color;
            float subIn = EndSubIn;
            Ui.Alpha = oldAlpha * subIn;
            GUI.color = new Color(oldColor.r, oldColor.g, oldColor.b, oldColor.a * subIn);
            string sub = game.Practice != null ? (won ? "YOU CLEARED ALL " + game.Practice.Waves.Count + " PRACTICE WAVES" : "YOU MADE IT TO PRACTICE WAVE " + Math.Max(1, game.PracticeIndex + 1) + " OF " + game.Practice.Waves.Count)
                : won ? "YOU BEAT ALL " + Game.MaxWave + " WAVES AND THE ECLIPSE" : "YOU MADE IT TO WAVE " + game.Wave;
            if (!won && game.Practice == null && game.Wave % 5 == 0 && game.Wave > 0)
                sub += "  ·  " + Defs.Get(Game.BossForWave(game.Wave)).Name.ToUpperInvariant();
            if (subIn > 0f)
                Ui.Label(new Rect(0f, 88f, W, 20f), sub, 14, Ui.Muted, TextAnchor.MiddleCenter, true);
            Ui.Alpha = oldAlpha;
            GUI.color = oldColor;

            // Run totals, as tiles.
            int kills = 0;
            foreach (var p in game.Players.Values)
                kills += p.Run.Kills;
            int secs = Mathf.RoundToInt(game.RunTime);
            string[,] tiles = { { "WAVE", game.Wave.ToString() }, { "TIME", secs / 60 + ":" + (secs % 60).ToString("00") }, { "SCORE", game.Score.ToString("N0") }, { "KILLS", kills.ToString() } };
            const float tw = 168f, tgap = 12f;
            float tx = W / 2f - (tw * 4f + tgap * 3f) / 2f;
            for (int i = 0; i < 4; i++)
            {
                var c = new Rect(tx + i * (tw + tgap), 120f, tw, 64f);
                float k = EndTileIn(i);
                int ti = i;
                EndPart(k, new Vector2(0f, -40f * (1f - Ease(k))), () =>  // each drops in (RunEnding.cs)
                {
                    Ui.Card(c, theme, 0.25f);
                    Ui.Fancy(new Rect(c.x, c.y + 6f, c.width, 34f), tiles[ti, 1], 26, Color.white, Color.Lerp(theme, Color.white, 0.55f), TextAnchor.MiddleCenter);
                    Ui.Label(new Rect(c.x, c.y + 40f, c.width, 16f), tiles[ti, 0], 10, Ui.Muted, TextAnchor.MiddleCenter, true);
                });
            }

            // A trophy card for each player.
            const float cardTop = 200f, cardH = 428f;
            if (!game.RunStatsReady)
                Ui.Label(new Rect(0f, cardTop + 180f, W, 30f), "Collecting results…", 18, Ui.Muted, TextAnchor.MiddleCenter);
            else
            {
                var players = new List<Player>(game.Players.Values);
                int n = Mathf.Max(1, players.Count);
                float cgap = 24f, cw = n == 1 ? 760f : (W - 140f - cgap * (n - 1)) / n;
                float cx = W / 2f - (cw * n + cgap * (n - 1)) / 2f;
                for (int i = 0; i < players.Count; i++)
                {
                    float k = EndCardIn(i);
                    int pi = i;
                    EndPart(k, new Vector2(0f, 80f * (1f - Ease(k))), () =>  // each rises in (RunEnding.cs)
                        DrawPlayerSummary(players[pi], players, new Rect(cx + pi * (cw + cgap), cardTop, cw, cardH), now));
                }
            }

            // Buttons, laid out like the lobby's: Leave on the left, the host's choices on the right.
            float buttonsIn = EndButtonsIn;
            EndPart(buttonsIn, new Vector2(0f, 30f * (1f - Ease(buttonsIn))), () =>  // they come up last (RunEnding.cs)
            {
                float by = H - 82f;
                if (Ui.FancyButton(new Rect(70f, by, 170f, 48f), "Leave", Ui.Danger, PixelArt.Get("ui_power"), now))
                    session.Leave("");
                if (session.IsHost)
                {
                    float bx = W - 70f;
                    if (won)
                    {
                        bx -= 300f;
                        if (Ui.FancyButton(new Rect(bx, by, 300f, 48f), "Keep going (endless)  [C]", Ui.GoldButton, PixelArt.Get("ui_star"), now))
                            game.ContinueEndless();
                        bx -= 14f;
                    }
                    bx -= 230f;
                    if (Ui.FancyButton(new Rect(bx, by, 230f, 48f), "Back to lobby  [L]", Ui.Neutral, PixelArt.Get("ui_back"), now))
                        session.ReturnToLobby();
                    bx -= 244f;
                    if (Ui.FancyButton(new Rect(bx, by, 230f, 48f), "Play again  [R]", Ui.Primary, PixelArt.Get("ui_play"), now))
                        session.StartMatch();
                }
                else
                    Ui.Label(new Rect(W - 470f, by, 400f, 48f), "Waiting for the host…", 16, Ui.Muted, TextAnchor.MiddleRight, true);
            });
            if (game.Recorder != null)
                Ui.Label(new Rect(70f, H - 26f, W - 140f, 16f), "Session recorded: " + game.Recorder.Path, 10, new Color(1f, 1f, 1f, 0.35f), TextAnchor.MiddleLeft);
        }

        /// One player's trophy card: on the left their class icon big on a glowing, hatched backdrop in their colour,
        /// their role, level and awards (where they had the best number); on the right their name and every number,
        /// with a bar against the best in the party (gold where they lead), and their upgrades by rarity.
        void DrawPlayerSummary(Player p, List<Player> all, Rect r, float now)
        {
            var color = ToColor(p.Def.Color);
            bool mine = p.Id == game.MyId;
            if (mine && all.Count > 1)
                for (int k = 3; k >= 1; k--)
                    Ui.Pill(new Rect(r.x - 4f * k, r.y - 4f * k, r.width + 8f * k, r.height + 8f * k), new Color(color.r, color.g, color.b, 0.05f * (4 - k) / 3f));
            Ui.Card(r, color, mine ? 0.85f : 0.45f);

            // The art column.
            float artW = Mathf.Min(170f, r.width * 0.3f);
            var art = new Rect(r.x + 3f, r.y + 3f, artW, r.height - 6f);
            Ui.Pill(art, new Color(color.r * 0.12f, color.g * 0.12f, color.b * 0.12f, 0.95f));
            for (int k = 0; k < 4; k++)
            {
                float s = 1f - k * 0.2f;
                Ui.Pill(new Rect(art.center.x - art.width * s / 2f, art.y + 20f + 70f * (1f - s), art.width * s, 150f * s), new Color(color.r, color.g, color.b, 0.06f + 0.02f * k));
            }
            Ui.Icon(art, Hatch(), new Color(color.r, color.g, color.b, 0.06f));
            float isz = Mathf.Min(108f, artW - 30f);
            Ui.Icon(new Rect(art.center.x - isz / 2f, art.y + 30f + 2f * Mathf.Sin(now * 2.2f + p.Id), isz, isz), PixelArt.Get("class_" + Defs.KitName(p.Kit).ToLowerInvariant()));
            float ay = art.y + 46f + isz;
            var roleChip = new Rect(art.x + 12f, ay, art.width - 24f, 24f);
            Ui.Pill(roleChip, new Color(color.r, color.g, color.b, 0.22f));
            Ui.Icon(new Rect(roleChip.x + 8f, roleChip.y + 4f, 16f, 16f), PixelArt.Get(RoleIcons[(int)p.Role]), color);
            Ui.Label(new Rect(roleChip.x + 28f, roleChip.y, roleChip.width - 32f, 24f), p.Def.Name.ToUpperInvariant(), 11, Color.Lerp(color, Color.white, 0.4f), TextAnchor.MiddleLeft, true);
            ay += 32f;
            var lvl = new Rect(art.x + 12f, ay, art.width - 24f, 24f);
            Ui.Pill(lvl, new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.9f));
            Ui.Label(lvl, "LEVEL " + p.Level, 12, new Color(0.08f, 0.06f, 0.02f), TextAnchor.MiddleCenter, true);
            ay += 36f;
            // Awards: rows where this player has the best number (and it isn't zero).
            if (all.Count > 1)
                foreach (var row in SummaryRows)
                {
                    if (row.Award == null || row.Value(p.Run) <= 0f || ay > art.yMax - 28f)
                        continue;
                    bool best = true;
                    foreach (var o in all)
                        if (o != p && row.Value(o.Run) >= row.Value(p.Run))
                            best = false;
                    if (!best)
                        continue;
                    var award = new Rect(art.x + 12f, ay, art.width - 24f, 22f);
                    Ui.Pill(award, new Color(Ui.GoldButton.r, Ui.GoldButton.g, Ui.GoldButton.b, 0.28f));
                    Ui.Icon(new Rect(award.x + 7f, award.y + 4f, 14f, 14f), PixelArt.Get("ui_trophy"), Ui.Gold);
                    Ui.Label(new Rect(award.x + 25f, award.y, award.width - 28f, 22f), row.Award.ToUpperInvariant(), 10, Ui.Gold, TextAnchor.MiddleLeft, true);
                    ay += 27f;
                }

            // Name, class and weapon, then the numbers.
            float x = art.xMax + 18f, w = r.xMax - 18f - x;
            Ui.Fancy(new Rect(x, r.y + 12f, w, 34f), p.Name, 26, Color.white, Color.Lerp(color, Color.white, 0.35f), TextAnchor.MiddleLeft,
                mine ? (Color?)new Color(color.r, color.g, color.b, 0.5f) : null);
            Ui.Label(new Rect(x, r.y + 46f, w, 18f), Defs.KitName(p.Kit) + " " + p.Def.Name + "  ·  " + p.Weapon.Name, 13, color, TextAnchor.MiddleLeft, true);
            Ui.Fill(new Rect(x, r.y + 72f, w, 1f), new Color(1f, 1f, 1f, 0.08f));
            float y = r.y + 82f;
            foreach (var row in SummaryRows)
            {
                float val = row.Value(p.Run), top = 0f;
                foreach (var o in all)
                    top = Mathf.Max(top, row.Value(o.Run));
                bool leading = all.Count > 1 && row.Award != null && val > 0f && val >= top;
                Ui.Label(new Rect(x, y, w * 0.55f, 20f), row.Label, 14, Ui.Muted);
                Ui.Label(new Rect(x + w * 0.4f, y, w * 0.6f, 20f), row.Format(p.Run), 15, leading ? Ui.Gold : Color.white, TextAnchor.MiddleRight, true);
                if (top > 0f && row.Award != null)
                {
                    Ui.Fill(new Rect(x, y + 22f, w, 3f), new Color(1f, 1f, 1f, 0.05f));
                    Ui.Fill(new Rect(x, y + 22f, w * Mathf.Clamp01(val / top), 3f), leading ? Ui.Gold : new Color(color.r, color.g, color.b, 0.75f));
                }
                y += 30f;
            }

            // Upgrades picked, by rarity, and any legendaries by name.
            var counts = new int[Enum.GetValues(typeof(Rarity)).Length];
            var legendaries = new List<string>();
            foreach (int id in p.Taken)
            {
                counts[(int)Upgrades.All[id].Rarity]++;
                if (Upgrades.All[id].Rarity == Rarity.Legendary)
                    legendaries.Add(Upgrades.All[id].Name);
            }
            y += 4f;
            Ui.Label(new Rect(x, y, 90f, 22f), "UPGRADES", 11, Ui.Muted, TextAnchor.MiddleLeft, true);
            float px = x + 86f;
            for (int i = 0; i < counts.Length; i++)
            {
                if (counts[i] == 0)
                    continue;
                var rc = ToColor(Upgrades.RarityColor((Rarity)i));
                var pill = new Rect(px, y + 1f, 40f, 20f);
                Ui.Pill(pill, new Color(rc.r, rc.g, rc.b, 0.9f));
                Ui.Label(pill, counts[i].ToString(), 11, new Color(0.06f, 0.06f, 0.1f), TextAnchor.MiddleCenter, true);
                px += 46f;
            }
            if (legendaries.Count > 0)
                Ui.Label(new Rect(x, y + 24f, w, 18f), string.Join(", ", legendaries.ToArray()), 12, ToColor(Upgrades.RarityColor(Rarity.Legendary)), TextAnchor.MiddleLeft, true);
        }

        /// Name and health bar for each boss on the field, plus warnings for their mechanics: wide across the
        /// top middle, which the rest of the HUD leaves free.
        static string HeraldStageName(HeraldStage s) => s == HeraldStage.Gathering ? "GATHERING STORM" : s == HeraldStage.Gale ? "GALE"
            : s == HeraldStage.Supercell ? "SUPERCELL" : "LAST STAND";

        public static string EclipseStageName(EclipseStage s) => s == EclipseStage.Corona ? "CORONA" : s == EclipseStage.Penumbra ? "PENUMBRA"
            : s == EclipseStage.Umbra ? "UMBRA" : s == EclipseStage.Echoes ? "ECHOES" : s == EclipseStage.LastLight ? "LAST LIGHT" : "TOTALITY";

        /// The Eclipse's stage under its health bar, and what to watch for.
        string EclipseNote(EclipseState s, ref Color c)
        {
            if (s.TransT > 0f)
            {
                c = new Color(1f, 0.95f, 0.8f);
                return "CAN'T BE HURT — " + EclipseStageName(s.TransTo) + " is coming";
            }
            if (s.FlashT > 0f)
            {
                c = new Color(1f, 0.92f, 0.7f);
                return "HIDE IN THE MOON'S SHADOW — " + s.FlashT.ToString("0.0") + " s";
            }
            if (s.Stage == EclipseStage.Totality)
            {
                c = new Color(1f, 0.45f, 0.4f);
                return "TOTALITY — kill it before the nova: " + s.NovaT.ToString("0.0") + " s (it takes +50%)";
            }
            string stage = "STAGE " + ((int)s.Stage + 1) + " / 5 · " + EclipseStageName(s.Stage);
            switch (s.Turn)
            {
                case EclipseTurn.Flares: return stage + " — SOLAR FLARES: step out of the lines";
                case EclipseTurn.LunarWake: return stage + " — the moon soaks your shots";
                case EclipseTurn.Crescents: return stage + " — CRESCENTS: they come back";
                case EclipseTurn.ClosingUmbra: return stage + " — CLOSING UMBRA: find a gap";
                case EclipseTurn.DarkSpiral: return stage + " — stay in the light";
                case EclipseTurn.EchoColossus: return stage + " — ECHO OF THE COLOSSUS";
                case EclipseTurn.EchoLich: return stage + " — ECHO OF THE LICH: walk round with the beams";
                case EclipseTurn.EchoHerald: return stage + " — ECHO OF THE STORM HERALD";
                case EclipseTurn.EchoSingularity: return stage + " — ECHO OF THE SINGULARITY";
                case EclipseTurn.SolarStorm: return stage + " — SOLAR STORM";
            }
            if (s.Stage == EclipseStage.Umbra)
                return stage + " — the dark drains you: stay in the light";
            return stage;
        }

        /// The Storm Herald's stage under its health bar, and what to watch for.
        string HeraldNote(HeraldState s, ref Color c)
        {
            if (s.TransT > 0f)
            {
                c = new Color(0.9f, 0.92f, 1f);
                return "CAN'T BE HURT — " + HeraldStageName(s.TransTo) + " is coming";
            }
            if (s.CondId == game.MyId && s.CondId != 0)
            {
                c = new Color(0.83f, 0.6f, 1f);
                return "YOU'RE THE CONDUCTOR — get away from your allies!";
            }
            if (s.CondId != 0)
            {
                c = new Color(0.83f, 0.6f, 1f);
                return "CONDUCTOR — keep away from " + (game.Players.ContainsKey(s.CondId) ? game.Players[s.CondId].Name : "them") + "!";
            }
            switch (s.Turn)
            {
                case HeraldTurn.Rods: return "LIGHTNING RODS — stay off the lines between them";
                case HeraldTurn.RodCage:
                    c = new Color(0.83f, 0.6f, 1f);
                    return "ROD CAGE — get out through the open side!";
                case HeraldTurn.Crosswind: return "GALE — step into the wind to dodge";
                case HeraldTurn.BallLightning: return "BALL LIGHTNING — shoot them, keep out from between them";
                case HeraldTurn.Downburst: return "DOWNBURST — get out of the row";
                case HeraldTurn.Thunderclap: return "THUNDERCLAP — slip through the gaps";
                case HeraldTurn.StormFront: return "STORM FRONT — find the gap";
                case HeraldTurn.Eye: return "EYE OF THE STORM — walk round with the spokes";
            }
            return HeraldStageName(s.Stage);
        }

        static string DuelStageName(DuelStage s) => s == DuelStage.EnGarde ? "EN GARDE" : s == DuelStage.Masquerade ? "MASQUERADE" : s == DuelStage.ToTheDeath ? "TO THE DEATH" : "LAST STAND";

        /// The Duelist's line under its boss bar: what it's doing and what to do about it (Duelist.cs).
        static string DuelNote(DuelState s, ref Color col)
        {
            if (s.TransT > 0f)
            {
                col = new Color(0.9f, 0.92f, 1f);
                return "CAN'T BE HURT — " + DuelStageName(s.TransTo) + " is coming";
            }
            if (s.StaggerT > 0f)
            {
                col = new Color(1f, 0.8f, 0.35f);
                return "OPEN — it takes 50% more damage!";
            }
            if (s.SaluteT > 0f || s.Dash == DuelDash.Salute)
            {
                col = new Color(1f, 0.8f, 0.35f);
                return "SALUTE — perfect-parry the lunge to open it up";
            }
            if (s.Turn == DuelTurn.Guard)
            {
                col = new Color(0.81f, 0.84f, 1f);
                return "GUARD — its blade turns your shots back; get behind it";
            }
            if (s.Turn == DuelTurn.Masquerade)
            {
                col = new Color(0.71f, 0.61f, 1f);
                return "MASQUERADE — only the real one's shots are solid";
            }
            return DuelStageName(s.Stage);
        }

        static string LichStageName(LichStage s) => s == LichStage.Crypt ? "THE CRYPT" : s == LichStage.Harvest ? "SOUL HARVEST" : s == LichStage.Undeath ? "UNDEATH" : "THE FINAL RITE";
        static string SingStageName(SingStage s) => s == SingStage.EventHorizon ? "EVENT HORIZON" : s == SingStage.Accretion ? "ACCRETION"
            : s == SingStage.Spaghettify ? "SPAGHETTIFY" : "LAST STAND";

        static string ColStageName(ColStage s) => s == ColStage.Awakening ? "AWAKENING" : s == ColStage.Crumbling ? "CRUMBLING"
            : s == ColStage.MoltenCore ? "MOLTEN CORE" : "LAST STAND";

        /// The Colossus's stage under its health bar, and what to watch for.
        static string ColossusNote(ColossusState c, ref Color col)
        {
            if (c.TransT > 0f)
            {
                col = new Color(0.95f, 0.9f, 0.85f);
                return "CAN'T BE HURT — " + ColStageName(c.TransTo) + " is coming";
            }
            if (c.DazeT > 0f)
            {
                col = new Color(1f, 0.85f, 0.35f);
                return "DAZED — it takes 50% more damage!";
            }
            if (c.ChargeWarn > 0f)
            {
                col = new Color(1f, 0.45f, 0.4f);
                return "CHARGE — get off the red line (or put a plate in its way)";
            }
            if (c.Rings.Count > 0)
            {
                col = new Color(0.6f, 1f, 0.7f);
                return "STOMP — the rings shove you: slip through a green gap";
            }
            return ColStageName(c.Stage);
        }

        static string DevourStageName(DevourStage s) => s == DevourStage.Appetite ? "APPETITE" : s == DevourStage.Gorging ? "GORGING"
            : s == DevourStage.Ravenous ? "RAVENOUS" : "LAST MEAL";

        /// The Devourer's stage under its health bar, and what to watch for.
        string DevourNote(DevourState s, ref Color c)
        {
            var hungry = new Color(1f, 0.45f, 0.7f);
            if (s.TransT > 0f)
            {
                c = new Color(1f, 0.85f, 0.88f);
                return "CAN'T BE HURT — " + DevourStageName(s.TransTo) + " is coming";
            }
            if (s.SwallowId != 0 && s.SwallowId == game.MyId)
            {
                c = new Color(0.8f, 0.9f, 0.4f);
                return "SWALLOWED — hold on, your allies' hits get you out";
            }
            if (s.SwallowId != 0)
            {
                c = new Color(0.8f, 0.9f, 0.4f);
                return (game.Players.ContainsKey(s.SwallowId) ? game.Players[s.SwallowId].Name.ToUpper() : "AN ALLY") + " IS SWALLOWED — shoot it, double damage!";
            }
            if (s.BurrowT > 0f)
                return "BURROWING — it's coming up under you, keep moving";
            switch (s.Turn)
            {
                case DevourTurn.Hungry:
                    c = hungry;
                    return "HUNGRY — don't shoot it, shoot the morsels!";
                case DevourTurn.Snap:
                case DevourTurn.Frenzy: return "LUNGES — step off the red line";
                case DevourTurn.Inhale:
                    c = new Color(0.8f, 0.9f, 0.4f);
                    return "INHALE — get out of the cone!";
                case DevourTurn.Bile: return "BILE — keep out of the pools";
                case DevourTurn.Jaws: return "JAWS — dodge out to the side";
                case DevourTurn.Vomit: return "VOMIT — get behind it";
                case DevourTurn.LastMeal: return "LAST MEAL — keep walking out, mind the teeth";
            }
            if (s.Stage == DevourStage.Ravenous)
            {
                c = hungry;
                return "THE MAW — shots into its mouth heal it, hit its back";
            }
            return DevourStageName(s.Stage);
        }

        /// The Warden's stage under its health bar, and what to watch for.
        string WardenNote(WardenState w, ref Color c)
        {
            if (w.TransT > 0f && w.TransTo == WardenStage.LastStand)
            {
                c = new Color(0.7f, 0.82f, 1f);
                return "CAN'T BE HURT — he's putting on his armor";
            }
            if (w.TransT > 0f)
            {
                c = new Color(0.9f, 0.92f, 1f);
                return "CAN'T BE HURT — " + WardenStageName(w.TransTo) + " is coming";
            }
            if (w.Stage == WardenStage.LastStand && w.Armor > 0f)
            {
                c = new Color(0.7f, 0.82f, 1f);
                return "LAST STAND — ARMORED: break it to reach his health";
            }
            var me = game.Me;
            if (me != null && me.StunT > 0f)
            {
                c = new Color(0.45f, 0.88f, 1f);
                return WardenStageName(w.Stage) + " — STUNNED!";
            }
            foreach (var g in game.Enemies.Values)
                if (g.Type == EnemyType.Guard)
                {
                    c = new Color(0.45f, 0.65f, 1f);
                    return WardenStageName(w.Stage) + " — THE GUARDS ARE COMING FOR YOU";
                }
            if (w.Turn == WardenTurn.Rioters && w.RiotShieldMax > 0f)  // its riot shield: break it (Warden.cs)
            {
                if (w.RiotShield <= 0f)
                {
                    c = new Color(1f, 0.6f, 0.35f);
                    return "RIOT — SHIELD BROKEN: he takes 50% more";
                }
                c = new Color(0.8f, 0.88f, 1f);
                return "RIOT — break his shield (" + Mathf.CeilToInt(100f * w.RiotShield / w.RiotShieldMax) + "%)";
            }
            if (w.Turn == WardenTurn.Light && w.LightMode == WardenLight.Lock)
            {
                c = new Color(1f, 0.4f, 0.35f);
                return WardenStageName(w.Stage) + " — SPOTTED!";
            }
            c = w.Stage == WardenStage.Count ? new Color(1f, 0.92f, 0.65f) : w.Stage == WardenStage.Lockdown ? new Color(0.7f, 0.8f, 1f) : new Color(1f, 0.55f, 0.3f);
            return WardenStageName(w.Stage);
        }

        /// The Hive Queen's stage under her health bar, and what to watch for.
        string QueenNote(Enemy e, ref Color c)
        {
            var q = e.Queen;
            if (q.TransT > 0f)
            {
                c = new Color(0.9f, 0.92f, 1f);
                return "CAN'T BE HURT — " + QueenStageName(q.TransTo) + " is coming";
            }
            c = q.Stage == QueenStage.Swarm ? new Color(1f, 0.82f, 0.3f) : q.Stage == QueenStage.Honeycomb ? new Color(1f, 0.65f, 0.15f) : new Color(1f, 0.45f, 0.3f);
            if (e.Phase == 1)
                return "SWARM — SHIELDED, kill her drones!";
            if (q.Turn == QueenTurn.Eggs)
                return "HONEYCOMB — break the eggs before they hatch!";
            if (Game.QueenAir(q) > 0.5f)
                return "FRENZY — DIVING, get out from under her!";
            return QueenStageName(q.Stage);
        }

        static string QueenStageName(QueenStage s) =>
            s == QueenStage.Swarm ? "SWARM" : s == QueenStage.Honeycomb ? "HONEYCOMB" : s == QueenStage.Frenzy ? "FRENZY" : "LAST STAND";

        /// The Reapers' stage under their health bars, and what to do about the Dual Reaper.
        string ReaperNote(Enemy e, ReaperFight R, ref Color c)
        {
            if (R.Trans != 0)
            {
                c = new Color(0.9f, 0.92f, 1f);
                return R.Trans == 1 ? "CAN'T BE HURT — THEY'RE FUSING" : "CAN'T BE HURT — IT'S SPLITTING";
            }
            if (e.Type == EnemyType.TwinB)
                return null;  // Dusk's bar speaks for both
            if (R.Stage == ReaperStage.Twilight)
            {
                c = new Color(0.77f, 0.55f, 1f);
                return "TWILIGHT";
            }
            if (R.Stage == ReaperStage.Sundered)
            {
                c = new Color(1f, 0.4f, 0.4f);
                return "SUNDERED";
            }
            c = new Color(1f, 0.8f, 0.35f);
            if (R.Trapped)
                return R.ShieldFrac > 0f ? "TRAPPED — BREAK ITS SHIELD!" : "OPEN — HIT IT NOW!";
            if (R.Turn == ReaperTurn.Chase)
                return "ARMORED — LURE IT INTO A HOLE";
            if (R.Turn == ReaperTurn.Lasers)
                return "ARMORED — KILL THE PILLARS";
            return "THE DUAL REAPER";
        }

        static string WardenStageName(WardenStage s) =>
            s == WardenStage.Count ? "COUNT" : s == WardenStage.Lockdown ? "LOCKDOWN" : s == WardenStage.Riot ? "RIOT" : "LAST STAND";

        void DrawBossBars()
        {
            float panelW = 640f, y = 12f, x0 = W / 2f - panelW / 2f;
            foreach (var e in game.Enemies.Values)
            {
                if (!e.Def.IsBoss)
                    continue;
                var col = ToColor(e.Def.Color);
                string note = null;
                Color noteColor = Ui.Warn;
                if (e.Type == EnemyType.HiveQueen && e.Queen != null)
                    note = QueenNote(e, ref noteColor);
                else if (e.Type == EnemyType.HiveQueen && e.Phase == 1)
                    note = "SHIELDED — kill her swarm!";
                else if (Game.IsTwin(e.Type) && e.Phase == 1)
                {
                    note = "ENRAGED";
                    noteColor = new Color(1f, 0.4f, 0.4f);
                }
                else if (Game.SoulBound(e))
                {
                    note = "SOUL BOUND — hurt the other Reaper!";
                    noteColor = new Color(0.85f, 0.88f, 1f);
                }
                else if (e.Type == EnemyType.Singularity && e.Sing != null && e.Sing.TransT > 0f)
                {
                    note = "CAN'T BE HURT — " + SingStageName(e.Sing.TransTo) + " is coming";
                    noteColor = new Color(0.9f, 0.92f, 1f);
                }
                else if (e.Type == EnemyType.Singularity && ImCaged())
                {
                    note = "CAGED — shoot the wells around you!";
                    noteColor = new Color(0.9f, 0.55f, 1f);
                }
                else if (e.Type == EnemyType.Singularity && e.Phase == Game.SingSplit)
                {
                    note = "SPLIT — shoot the shards, the core can't be hurt!";
                    noteColor = new Color(0.9f, 0.55f, 1f);
                }
                else if (e.Type == EnemyType.Singularity && e.Phase == Game.SingWhiteHole)
                {
                    note = "WHITE HOLE — it pushes, and every bullet turns round!";
                    noteColor = new Color(0.95f, 0.95f, 1f);
                }
                else if (e.Type == EnemyType.Singularity && AnyWells())
                    note = "The corner wells swallow your shots — kill them!";
                else if (e.Type == EnemyType.Singularity && e.Sing != null)
                    note = SingStageName(e.Sing.Stage);
                else if (e.Type == EnemyType.StormHerald && e.Herald != null)
                    note = HeraldNote(e.Herald, ref noteColor);
                else if (e.Type == EnemyType.Duelist && e.Duel != null)
                    note = DuelNote(e.Duel, ref noteColor);
                else if (e.Type == EnemyType.Colossus && e.Colossus != null)
                    note = ColossusNote(e.Colossus, ref noteColor);
                else if (e.Type == EnemyType.Lich && e.Lich != null && e.Lich.TransT > 0f)
                {
                    note = "CAN'T BE HURT — " + LichStageName(e.Lich.TransTo) + " is coming";
                    noteColor = new Color(0.9f, 0.92f, 1f);
                }
                else if (e.Type == EnemyType.Lich && e.Phase == Game.LichGuarded)
                    note = e.Lich != null && e.Lich.Stage == LichStage.Harvest ? "GUARDED — break all three phylacteries!" : "GUARDED — break the phylactery!";
                else if (e.Type == EnemyType.Lich && e.Lich != null && e.Lich.Window > 0f)
                {
                    note = "OPEN — " + Mathf.CeilToInt(e.Lich.Window) + " s before its phylacteries rise again";
                    noteColor = new Color(1f, 0.8f, 0.35f);
                }
                else if (e.Type == EnemyType.Lich && e.Lich != null)
                    note = LichStageName(e.Lich.Stage);
                else if (e.Type == EnemyType.Devourer && e.Devour != null)
                    note = DevourNote(e.Devour, ref noteColor);
                else if (Game.Hungry(e))
                {
                    note = "HUNGRY — stop shooting, your hits heal it!";
                    noteColor = new Color(1f, 0.45f, 0.7f);
                }
                else if (e.Type == EnemyType.Eclipse && e.Eclipse != null)
                    note = EclipseNote(e.Eclipse, ref noteColor);
                else if (e.Type == EnemyType.Boss && e.Warden != null)
                    note = WardenNote(e.Warden, ref noteColor);
                else if (Game.IsReaper(e.Type) && game.Reapers != null)
                    note = ReaperNote(e, game.Reapers, ref noteColor);
                // The Warden's last-stand armor: a blue bar above its health, as long as it, that wears down first.
                bool armored = e.Type == EnemyType.Boss && e.Warden != null && e.Warden.Armor > 0f && e.Warden.ArmorMax > 0f;
                float armorH = armored ? 9f : 0f;
                float h = (note != null ? 62f : 46f) + armorH;
                var panel = new Rect(x0, y, panelW, h);
                Ui.HudPanel(panel, col);
                float frac = Mathf.Clamp01(e.Hp / e.MaxHp);
                // Name with small diamonds either side, and the health percentage.
                Ui.Pill(new Rect(panel.x + 14f, panel.y + 12f, 6f, 6f), col);
                Ui.Label(new Rect(panel.x + 26f, panel.y + 5f, panelW - 120f, 20f), e.Def.Name.ToUpperInvariant(), 14, Color.white, TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(panel.xMax - 90f, panel.y + 5f, 76f, 20f), Mathf.CeilToInt(frac * 100f) + "%", 13, Ui.Muted, TextAnchor.MiddleRight, true);
                if (armored)
                    GhostBar(new Rect(panel.x + 14f, panel.y + 26f, panelW - 28f, 6f), Mathf.Clamp01(e.Warden.Armor / e.Warden.ArmorMax),
                        new Color(0.45f, 0.65f, 1f), EnemyKey(e.Id) | 2L << 40);
                var bar = new Rect(panel.x + 14f, panel.y + 27f + armorH, panelW - 28f, 12f);
                GhostBar(bar, frac, col, EnemyKey(e.Id) | 1L << 40);  // with its damage ghost (HealthBars.cs)
                for (int k = 1; k < 10; k++)  // tick marks every 10%
                    Ui.Fill(new Rect(bar.x + bar.width * k / 10f, bar.y + 2f, 1f, bar.height - 4f), new Color(0f, 0f, 0f, 0.35f));
                if (note != null)
                    Ui.Label(new Rect(panel.x, panel.y + 41f + armorH, panelW, 18f), note, 12, noteColor, TextAnchor.MiddleCenter, true);
                y += h + 6f;
            }
            string warning = null;
            if (game.TwinReviveT > 0f)
                warning = "Kill the other Reaper in " + game.TwinReviveT.ToString("0.0") + "s or its twin revives!";
            else if (game.Reapers != null && game.Reapers.Suck > 0f)
                warning = "THE PILLARS STOOD — it's pulling you in!";
            else if (game.PullStrength > 0f && FindDevourer(game) != null)
                warning = "ITS LAST MEAL — it's pulling you in!";  // Devourer.cs
            else if (game.PullStrength > 0f)
                warning = "GRAVITY WELL — run!";
            if (warning != null)
            {
                var r = new Rect(x0 + panelW / 2f - 230f, y, 460f, 28f);
                float pulse = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 8f);
                Ui.Pill(r, new Color(0.45f, 0.06f, 0.1f, 0.75f));
                Ui.Label(r, warning, 14, new Color(1f, 0.75f, 0.75f, pulse), TextAnchor.MiddleCenter, true);
            }
        }

        // --- Beside the hotbar ----------------------------------------------------------

        // The wave panel sits left of the ability bar and your card right of it, both 72 px tall and lined up with
        // the bar's bottom; the path and modifier chips stack up from the wave panel, teammates from your card.
        const float SideW = 229f, SideL = 12f, SideR = W - 12f - SideW, SideH = 72f, SideY = H - 12f - SideH;

        void DrawWaveCorner()
        {
            DrawWavePlate(new Rect(SideL, SideY, SideW, SideH));
            float y = SideY - 28f;
            y -= DrawApproachChip(SideL, y);  // ApproachUi.cs
            if (game.CurrentPath != PathId.None)
            {
                DrawPathChip(SideL, y);  // PathsUi.cs
                y -= 24f;
            }
            if (DrawGoldChip(SideL, y))  // ShopUi.cs
                y -= 24f;
            DrawActiveModifiers(DrawActiveDifficulty(SideL, y + 2f), y + 2f);  // DifficultyUi.cs, ModifiersUi.cs
        }

        void DrawPlayerCorner()
        {
            var ids = new List<int>(game.Players.Keys);
            ids.Sort((a, b) => a == game.MyId ? -1 : b == game.MyId ? 1 : a.CompareTo(b));
            if (ids.Count == 0)
                return;
            // Yours in full; teammates as slim rows above it. (A spectator-less game always has you first.)
            DrawPlayerHudCard(game.Players[ids[0]], new Rect(SideR, SideY, SideW, SideH), ids[0] == game.MyId);
            float y = SideY - 6f;
            for (int i = 1; i < ids.Count; i++)
            {
                y -= 30f;
                DrawTeammateRow(game.Players[ids[i]], new Rect(SideR, y, SideW, 30f));
                y -= 5f;
            }
        }

        /// A teammate as one slim row: name and class, level, and health (or how far their revive has got).
        void DrawTeammateRow(Player p, Rect r)
        {
            var role = ToColor(p.Def.Color);
            Ui.HudPanel(r, p.Downed ? new Color(1f, 0.3f, 0.3f) : (Color?)null);
            Ui.Pill(new Rect(r.x + 5f, r.y + 6f, 3f, r.height - 12f), p.Downed ? new Color(0.5f, 0.5f, 0.55f) : role);
            Ui.Label(new Rect(r.x + 14f, r.y + 2f, 82f, 14f), p.Name, 11, Color.white, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(r.x + 14f, r.y + 15f, 82f, 12f), Defs.KitName(p.Kit) + " " + p.Def.Name, 8, role, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(r.xMax - 40f, r.y + 2f, 34f, 14f), "LV " + p.Level, 9, Ui.Gold, TextAnchor.MiddleRight, true);
            var hp = new Rect(r.x + 98f, r.y + 17f, r.width - 106f, 7f);
            float frac = Mathf.Clamp01(p.Hp / Mathf.Max(1f, p.MaxHp));
            if (p.Downed)
            {
                Ui.Bar(hp, p.Revive, new Color(0.6f, 0.6f, 0.65f));
                Ui.Label(new Rect(hp.x, r.y + 2f, hp.width - 36f, 14f), "DOWNED", 9, new Color(1f, 0.6f, 0.6f), TextAnchor.MiddleLeft, true);
                return;
            }
            GhostBar(hp, frac, HealthColor(frac), PlayerKey(p.Id, true));
            if (p.ShieldHp > 0f)
                Ui.Bar(new Rect(hp.x + 1f, hp.yMax - 2f, (hp.width - 2f) * Mathf.Clamp01(p.ShieldHp / Mathf.Max(1f, p.MaxHp)), 2f), 1f, new Color(0.55f, 0.8f, 1f), new Color(0f, 0f, 0f, 0f));
            Ui.Label(new Rect(hp.x, r.y + 2f, hp.width - 36f, 14f), Mathf.CeilToInt(p.Hp) + " / " + Mathf.CeilToInt(p.MaxHp), 9, Ui.Muted, TextAnchor.MiddleLeft, true);
        }

        /// Green when healthy, through yellow, to red when low.
        static Color HealthColor(float frac) =>
            frac > 0.5f ? Color.Lerp(new Color(1f, 0.85f, 0.3f), new Color(0.35f, 0.95f, 0.45f), (frac - 0.5f) * 2f)
                : Color.Lerp(new Color(1f, 0.3f, 0.3f), new Color(1f, 0.85f, 0.3f), frac * 2f);

        // --- HUD pieces -----------------------------------------------------------

        int plateWave = -1, plateMax = 1;

        /// Left of the ability bar: the wave number, how many enemies are left (with a progress bar) and the score.
        void DrawWavePlate(Rect r)
        {
            if (game.Wave != plateWave)
            {
                plateWave = game.Wave;
                plateMax = 1;
            }
            plateMax = Math.Max(plateMax, game.Remaining);
            var practice = game.Practice;
            bool bossWave = practice != null ? BossUp() : game.Wave > 0 && game.Wave % 5 == 0;
            Ui.HudPanel(r, bossWave ? Ui.Gold : (Color?)null);
            string head = practice != null ? "PRACTICE  ·  LV " + game.Wave : bossWave ? "BOSS WAVE" : "WAVE";
            Ui.Label(new Rect(r.x + 16f, r.y + 8f, 120f, 14f), head, 10, bossWave ? Ui.Gold : practice != null ? Ui.Accent : Ui.Muted, TextAnchor.MiddleLeft, true);
            string num = practice != null ? Math.Max(1, game.PracticeIndex + 1).ToString() : game.Wave.ToString();
            Ui.Label(new Rect(r.x + 14f, r.y + 20f, 120f, 40f), num, 34, Color.white, TextAnchor.MiddleLeft, true);
            string of = practice != null ? (practice.Loop ? "/ " + practice.Waves.Count + "  LOOP" : "/ " + practice.Waves.Count) : game.Endless ? "ENDLESS" : "/ " + Game.MaxWave;
            Ui.Label(new Rect(r.x + 18f + num.Length * 20f, r.y + 31f, 100f, 24f), of, 13,
                game.Endless || practice != null && practice.Loop ? Ui.Gold : Ui.Muted, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(r.xMax - 140f, r.y + 8f, 124f, 14f), "ENEMIES LEFT", 10, Ui.Muted, TextAnchor.MiddleRight, true);
            Ui.Label(new Rect(r.xMax - 140f, r.y + 20f, 124f, 26f), game.Remaining.ToString(), 20, Color.white, TextAnchor.MiddleRight, true);
            Ui.Label(new Rect(r.xMax - 140f, r.y + 44f, 124f, 14f), "SCORE " + game.Score.ToString("N0"), 10, Ui.Gold, TextAnchor.MiddleRight, true);
            float progress = game.State == MatchState.Fighting ? 1f - (float)game.Remaining / Math.Max(1, plateMax) : 1f;
            Ui.Bar(new Rect(r.x + 14f, r.yMax - 12f, r.width - 28f, 5f), progress, bossWave ? Ui.Gold : Ui.Accent);
        }

        bool BossUp()
        {
            foreach (var e in game.Enemies.Values)
                if (e.Def.IsBoss)
                    return true;
            return false;
        }

        /// One player's card: role-colored accent, name and class, health (with shield) and XP.
        void DrawPlayerHudCard(Player p, Rect r, bool mine)
        {
            var role = ToColor(p.Def.Color);
            Ui.HudPanel(r, p.Downed ? new Color(1f, 0.3f, 0.3f) : mine ? role : (Color?)null);
            Ui.Pill(new Rect(r.x + 6f, r.y + 9f, 4f, r.height - 18f), p.Downed ? new Color(0.5f, 0.5f, 0.55f) : role);
            Ui.Label(new Rect(r.x + 18f, r.y + 6f, r.width - 70f, 18f), p.Name, 13, Color.white, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(r.x + 18f, r.y + 22f, r.width - 70f, 14f), Defs.KitName(p.Kit) + " " + p.Def.Name, 10, role, TextAnchor.MiddleLeft, true);
            var lv = new Rect(r.xMax - 52f, r.y + 8f, 42f, 18f);
            Ui.Pill(lv, new Color(1f, 0.8f, 0.35f, 0.22f));
            Ui.Label(lv, "LV " + p.Level, 10, Ui.Gold, TextAnchor.MiddleCenter, true);

            var hpBar = new Rect(r.x + 18f, r.y + 39f, r.width - 28f, 12f);
            float frac = Mathf.Clamp01(p.Hp / Mathf.Max(1f, p.MaxHp));
            Color hpCol = HealthColor(frac);
            if (p.Downed)
            {
                Ui.Bar(hpBar, p.Revive, new Color(0.6f, 0.6f, 0.65f));
                Ui.Label(hpBar, "DOWNED", 9, new Color(1f, 0.6f, 0.6f), TextAnchor.MiddleCenter, true);
            }
            else
            {
                GhostBar(hpBar, frac, hpCol, PlayerKey(p.Id, true));
                if (p.ShieldHp > 0f)  // Arcane Ward: a blue stripe along the bottom of the health bar
                    Ui.Bar(new Rect(hpBar.x + 2f, hpBar.yMax - 4f, (hpBar.width - 4f) * Mathf.Clamp01(p.ShieldHp / Mathf.Max(1f, p.MaxHp)), 3f), 1f, new Color(0.55f, 0.8f, 1f), new Color(0f, 0f, 0f, 0f));
                Ui.Shadowed(new Rect(hpBar.x, hpBar.y - 1f, hpBar.width, hpBar.height + 2f), Mathf.CeilToInt(p.Hp) + " / " + Mathf.CeilToInt(p.MaxHp), 10, Color.white);
            }
            Ui.Bar(new Rect(r.x + 18f, r.y + 55f, r.width - 28f, 4f), p.Xp / Defs.XpToNext(p.Level), new Color(1f, 0.82f, 0.3f), new Color(0f, 0f, 0f, 0.4f));
        }

        /// F3: the numbers behind the aim lines.
        void DrawAimDebug(Player me)
        {
            var r = new Rect(14f, 12f, 300f, 104f);
            Ui.HudPanel(r, new Color(0.3f, 0.9f, 1f));
            Func<float, string> deg = a => (MathX.Fmod(a * 180f / MathX.Pi + 360f, 360f)).ToString("0.00") + "°";
            float diff = MathX.Fmod(game.LastShotAngle - game.LastShotAim + MathX.Pi, MathX.Tau) - MathX.Pi;
            Ui.Label(new Rect(r.x + 12f, r.y + 6f, r.width - 24f, 16f), "AIM DEBUG  (F3)", 11, new Color(0.3f, 0.9f, 1f), TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(r.x + 12f, r.y + 24f, r.width - 24f, 18f), "Aim now", 12, Ui.Muted);
            Ui.Label(new Rect(r.x + 12f, r.y + 24f, r.width - 24f, 18f), deg(me.Aim), 12, Color.white, TextAnchor.MiddleRight, true);
            Ui.Label(new Rect(r.x + 12f, r.y + 42f, r.width - 24f, 18f), "Last shot", 12, Ui.Muted);
            Ui.Label(new Rect(r.x + 12f, r.y + 42f, r.width - 24f, 18f), game.LastShotId != 0 ? deg(game.LastShotAngle) : "-", 12, new Color(1f, 0.85f, 0.2f), TextAnchor.MiddleRight, true);
            Ui.Label(new Rect(r.x + 12f, r.y + 60f, r.width - 24f, 18f), "Shot vs aim when it appeared", 12, Ui.Muted);
            Ui.Label(new Rect(r.x + 12f, r.y + 60f, r.width - 24f, 18f), game.LastShotId != 0 ? (diff * 180f / MathX.Pi).ToString("+0.00;-0.00;0.00") + "°" : "-", 12, Color.white, TextAnchor.MiddleRight, true);
            Ui.Label(new Rect(r.x + 12f, r.y + 78f, r.width - 24f, 18f), "Ping to host", 12, Ui.Muted);
            Ui.Label(new Rect(r.x + 12f, r.y + 78f, r.width - 24f, 18f), session.IsHost ? "0 ms (you're the host)" : Mathf.RoundToInt(game.PingMs) + " ms", 12,
                session.IsHost || game.PingMs < 60f ? Ui.Good : game.PingMs < 120f ? Ui.Warn : new Color(1f, 0.45f, 0.45f), TextAnchor.MiddleRight, true);
        }

        /// A centered announcement between waves.
        static readonly Rect BannerRect = new Rect(W / 2f - 280f, H / 2f - 92f, 560f, 96f);

        void DrawBanner(string title, string sub, Color accent)
        {
            var r = new Rect(BannerRect.x, BannerRect.y, BannerRect.width, sub.Length > 0 ? 96f : 70f);
            Ui.Card(r, accent, 0.8f);  // solid, so nothing in the arena shows through
            Ui.Fill(new Rect(r.x + 40f, r.y + 14f, r.width - 80f, 1f), new Color(accent.r, accent.g, accent.b, 0.35f));
            Ui.Title(new Rect(r.x, r.y + 16f, r.width, 40f), title, 30, Color.white);
            if (sub.Length > 0)
                Ui.Label(new Rect(r.x, r.y + 60f, r.width, 22f), sub, 15, Ui.Muted, TextAnchor.MiddleCenter, true);
        }

        /// XP bar plus the ability tiles (Space / Q / E / Shift / ultimate) along the bottom.
        void DrawAbilityBar(Player me)
        {
            const float tileW = 150f, tileH = 58f, gap = 8f;
            float total = tileW * Upgrades.SlotCount + gap * (Upgrades.SlotCount - 1);
            float x0 = (W - total) / 2f, y = H - tileH - 12f;

            // XP strip with the level badge.
            var lv = new Rect(x0, y - 22f, 52f, 16f);
            Ui.Pill(lv, new Color(1f, 0.8f, 0.35f, 0.25f));
            Ui.Label(lv, "LV " + me.Level, 10, Ui.Gold, TextAnchor.MiddleCenter, true);
            Ui.Bar(new Rect(x0 + 58f, y - 17f, total - 58f, 6f), me.Xp / Defs.XpToNext(me.Level), new Color(1f, 0.82f, 0.3f), new Color(0f, 0f, 0f, 0.5f));

            for (int i = 0; i < Upgrades.SlotCount; i++)
            {
                var r = new Rect(x0 + i * (tileW + gap), y, tileW, tileH);
                var look = TileLookFor(i, i == Upgrades.UltSlot);  // how the tile reacts this frame (HudTiles.cs)
                int slotIndex = i;
                TileTransformed(r.center, look.Scale, look.Dx, () => DrawAbilityTile(me, slotIndex, r, look));
            }
            DrawMasteryToasts(x0 + total / 2f, y - 64f);
        }

        /// One ability tile: key, name, READY or the timer, mastery and recharge lines, charge pips; `look` adds the
        /// tile's reactions (rim flashes, READY sliding in, a pip popping).
        void DrawAbilityTile(Player me, int i, Rect r, TileLook look)
        {
            var slot = me.Slots[i];
            bool ultimate = i == Upgrades.UltSlot;
            if (slot.Id == AbilityId.None)
            {
                Ui.HudPanel(r);
                DrawKeyCap(new Rect(r.x + 8f, r.y + 7f, 0f, 16f), SlotKeys[i], new Color(1f, 1f, 1f, 0.45f));
                Ui.Label(new Rect(r.x + 8f, r.y + 26f, r.width - 16f, 18f), "Empty", 12, new Color(1f, 1f, 1f, 0.45f), TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(r.x + 8f, r.y + 40f, r.width - 16f, 14f), "from a very rare upgrade", 9, new Color(1f, 1f, 1f, 0.35f), TextAnchor.MiddleLeft);
                return;
            }
            bool ready = slot.Charges > 0;
            float cd = game.Cooldown(me, i);
            float fill = slot.Charges >= slot.MaxCharges ? 1f : 1f - slot.Timer / Mathf.Max(0.01f, cd);
            Color accent = ultimate ? Ui.Gold : ToColor(me.Def.Color);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
            Ui.HudPanel(r, ready ? new Color(accent.r, accent.g, accent.b, ultimate ? 0.6f + 0.4f * pulse : 1f) : (Color?)null);
            if (look.White > 0f)
                Ui.HudRim(r, new Color(1f, 1f, 1f, 0.9f * look.White));

            // Cooldown: a dark shade over the tile that drains downward as it recharges.
            if (!ready)
            {
                GUI.BeginGroup(new Rect(r.x, r.y, r.width, r.height * (1f - fill)));
                Ui.Pill(new Rect(0f, 0f, r.width, r.height), new Color(0f, 0f, 0f, 0.35f));
                GUI.EndGroup();
            }
            DrawKeyCap(new Rect(r.x + 8f, r.y + 7f, 0f, 16f), ultimate ? "ULT · F" : SlotKeys[i], ready ? accent : new Color(1f, 1f, 1f, 0.6f));
            // Name: bright with a sheen when ready, dimmer while recharging.
            Color nameBottom = ready ? Color.Lerp(accent, Color.white, 0.25f) : new Color(0.62f, 0.66f, 0.78f, 0.9f);
            Ui.Fancy(new Rect(r.x + 8f, r.y + 24f, r.width - 16f, 22f), Upgrades.Ability(slot.Id).Name, 15, ready ? Color.white : new Color(0.9f, 0.92f, 1f, 0.9f), nameBottom,
                TextAnchor.MiddleLeft, ready ? accent : (Color?)null);
            if (ready)
            {
                Color hi = ultimate ? new Color(1f, 0.95f, 0.7f) : new Color(0.8f, 1f, 0.85f), lo = ultimate ? Ui.Gold : Ui.Good;
                float rin = look.ReadyIn;  // sliding in from the right as the tile pops
                if (rin > 0f)
                    Ui.Fancy(new Rect(r.x + 8f + 14f * (1f - rin), r.y + 5f, r.width - 16f, 20f), ultimate ? "READY!" : "READY", 11, Fade(hi, rin), Fade(lo, rin), TextAnchor.MiddleRight,
                        new Color(lo.r, lo.g, lo.b, (ultimate ? 0.6f + 0.4f * pulse : 0.8f) * rin));
            }
            else if (slot.Id == AbilityId.StunGun && me.StunShots > 0)  // the gun is out: its cooldown waits for the last shot
                Ui.Fancy(new Rect(r.x + 8f, r.y + 4f, r.width - 16f, 20f), me.StunShots + (me.StunShots == 1 ? " SHOT" : " SHOTS"), 12, Color.white, new Color(0.45f, 0.88f, 1f), TextAnchor.MiddleRight);
            else
            {
                // Pressed too early: the timer flashes red.
                float red = look.Red;
                Ui.Fancy(new Rect(r.x + 8f, r.y + 4f, r.width - 16f, 20f), ultimate ? Mathf.FloorToInt(fill * 100f) + "%" : slot.Timer.ToString(slot.Timer < 10f ? "0.0" : "0") + "s",
                    14, Color.Lerp(Color.white, new Color(1f, 0.55f, 0.55f), red), Color.Lerp(new Color(0.7f, 0.8f, 1f), new Color(1f, 0.3f, 0.3f), red), TextAnchor.MiddleRight,
                    red > 0f ? new Color(1f, 0.3f, 0.3f, 0.7f * red) : (Color?)null);
            }
            // Along the bottom: the mastery XP line (green, full once mastered) sitting right on top of the
            // recharge line, the pair one strip.
            float mxp;
            me.MasteryXp.TryGetValue(slot.Id, out mxp);
            var strip = new Rect(r.x + 8f, r.yMax - 13f, r.width - 16f, 7f);
            const float xpH = 3f;
            Ui.Pill(strip, new Color(0f, 0f, 0f, 0.45f));
            GUI.BeginGroup(new Rect(strip.x, strip.y, strip.width * Game.MasteryProgress(mxp), xpH));
            Ui.Pill(new Rect(0f, 0f, strip.width, strip.height), new Color(0.35f, 0.9f, 0.45f));
            GUI.EndGroup();
            GUI.BeginGroup(new Rect(strip.x, strip.y + xpH, strip.width * Mathf.Clamp01(fill), strip.height - xpH));
            Ui.Pill(new Rect(0f, -xpH, strip.width, strip.height), ready ? accent : new Color(0.5f, 0.6f, 0.8f));
            GUI.EndGroup();
            if (slot.MaxCharges > 1)
                for (int k = 0; k < slot.MaxCharges; k++)
                {
                    var pip = new Rect(r.xMax - 12f - k * 9f, r.y + 30f, 6f, 6f);
                    if (k == look.Pip)
                    {
                        // The charge that just came back: a fading halo spreading round it, and the pip popping.
                        float h = 3f + 6f * look.PipHalo;
                        Ui.Pill(new Rect(pip.x - h, pip.y - h, pip.width + 2f * h, pip.height + 2f * h), new Color(1f, 1f, 1f, 0.35f * (1f - look.PipHalo)));
                        float e = 3f * look.PipPop;
                        pip = new Rect(pip.x - e, pip.y - e, pip.width + 2f * e, pip.height + 2f * e);
                    }
                    Ui.Pill(pip, k < slot.Charges ? accent : new Color(1f, 1f, 1f, 0.15f));
                }
            if (look.Red > 0f)
                Ui.HudRim(r, new Color(1f, 0.35f, 0.35f, 0.85f * look.Red));
        }

        static Color Fade(Color c, float a) => new Color(c.r, c.g, c.b, c.a * a);

        /// Five small bars for an ability's mastery level; the next one fills as it earns XP.
        void DrawMasteryPips(Rect r, int level, float progress)
        {
            bool max = level >= Game.MasteryMax;
            Color on = max ? Ui.Gold : new Color(0.6f, 0.85f, 1f);
            for (int k = 0; k < Game.MasteryMax; k++)
            {
                var pip = new Rect(r.x + k * 13f, r.y, 10f, r.height);
                Ui.Fill(pip, new Color(1f, 1f, 1f, 0.22f));
                if (k < level)
                    Ui.Fill(pip, on);
                else if (k == level && progress > 0f)
                    Ui.Fill(new Rect(pip.x, pip.y, pip.width * progress, pip.height), new Color(on.r, on.g, on.b, 0.55f));
            }
        }

        /// "Barrier · Mastery 3" when one of your abilities levels up.
        void DrawMasteryToasts(float cx, float y)
        {
            var toasts = game.MasteryToasts;
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                toasts[i].T -= Time.unscaledDeltaTime;
                if (toasts[i].T <= 0f)
                    toasts.RemoveAt(i);
            }
            for (int i = 0; i < toasts.Count && i < 3; i++)
            {
                var t = toasts[toasts.Count - 1 - i];
                bool max = t.Level >= Game.MasteryMax;
                float a = Mathf.Clamp01(t.T / 0.5f);
                string text = Upgrades.Ability(t.Ability).Name + (max ? "  MASTERED: " + Game.Capstone(t.Ability) : "  ·  Mastery " + t.Level);
                var c = max ? Ui.Gold : new Color(0.6f, 0.85f, 1f);
                Ui.Shadowed(new Rect(cx - 400f, y - i * 22f, 800f, 20f), text, max ? 15 : 14, new Color(c.r, c.g, c.b, a), TextAnchor.MiddleCenter);
            }
        }

        /// A small key label like [SPACE]; r.width is ignored (it's sized to the text).
        void DrawKeyCap(Rect r, string key, Color color)
        {
            float w = 12f + key.Length * 6.5f;
            var cap = new Rect(r.x, r.y, w, r.height);
            Ui.Pill(cap, new Color(color.r, color.g, color.b, 0.2f * color.a + 0.05f));
            Ui.Fancy(cap, key, 9, new Color(1f, 1f, 1f, 0.55f + 0.45f * color.a), new Color((color.r + 1f) / 2f, (color.g + 1f) / 2f, (color.b + 1f) / 2f, 0.5f + 0.5f * color.a), TextAnchor.MiddleCenter);
        }

        /// The level-up picker ("trophy cards", Docs/Mockups/upgrade-picker.html option C), opening out of your character
        /// (PickIntro.cs) and closing back into it (PickOutro.cs): a gold LEVEL UP! title, three
        /// tall cards with the icon big on a rarity backdrop (the picked one lifts and glows, the others step back; rare
        /// and better shine), one line saying what your pick changes, and the lobby's fancy buttons. Weapon mods (more than
        /// three cards) use a grid of smaller cards. Hold Tab for your full stats.
        void DrawUpgradePicker(Player me)
        {
            SyncSelection(me);
            float now = Time.unscaledTime;
            bool opening = PickIntroPlaying(me);  // the cards are still flying out of you: nothing can be chosen yet
            Ui.Fill(new Rect(0, 0, W, H), new Color(0.01f, 0.012f, 0.03f, 0.82f * (introFull ? Seg(IntroElapsed, 0f, 0.3f) : 1f)));
            const float cardW = 270f, cardH = 384f, gap = 30f;
            float mid = W / 2f;

            // Heading: a big outlined title in the offer's colour, and a line under it.
            Color theme = me.OfferLegendary ? ToColor(Upgrades.RarityColor(Rarity.Legendary))
                : me.OfferMod ? ToColor(Upgrades.RarityColor(Rarity.Mod)) : Ui.Gold;
            string title = me.OfferLegendary ? "BOSS REWARD!" : me.OfferMod ? "WEAPON MOD" : "LEVEL UP!";
            float titleY, band;
            int titleSize;
            PickTitleMotion(72f, 54, out titleY, out titleSize, out band);  // a full opening pops it in the middle first
            if (band > 0f)
                for (int k = 0; k < 7; k++)  // a dark band behind it, soft at the edges
                    Ui.Fill(new Rect(0f, titleY - 70f + k * 20f, W, 20f), new Color(0f, 0f, 0f, 0.55f * band * Mathf.Sin((k + 0.5f) / 7f * Mathf.PI)));
            Ui.Fancy(new Rect(0f, titleY - 32f - (titleSize - 54) * 0.6f, W, 64f + (titleSize - 54) * 1.2f), title, titleSize, Color.white, theme, TextAnchor.MiddleCenter, new Color(theme.r, theme.g, theme.b, 0.55f));
            int more = me.PendingPicks + me.PendingLegendary + me.PendingMods - 1;
            string sub = me.OfferLegendary ? "PICK A LEGENDARY UPGRADE" : me.OfferMod ? "PICK HOW YOUR SHOTS BEHAVE" : "LEVEL " + me.Level + "  ·  PICK ONE";
            if (more > 0)
                sub += "  ·  " + more + " MORE AFTER THIS";
            if (titleY <= 73f)
                Ui.Label(new Rect(0f, 102f, W, 20f), sub, 14, Ui.Muted, TextAnchor.MiddleCenter, true);

            bool grid = me.Offer.Length > 3;
            if (pickCardRects.Length != me.Offer.Length)
                pickCardRects = new Rect[me.Offer.Length];  // where each card is, for the outro when one is taken
            float by;
            V2 home = PickHome(me);
            var slots = new Rect[me.Offer.Length];  // where each card goes (the opening flies them there from you)
            if (grid)
            {
                const int perRow = 4;
                float areaW = cardW * 3 + gap * 2, gw = 18f, cw = (areaW - gw * (perRow - 1)) / perRow, ch = 150f, top = 140f;
                for (int i = 0; i < me.Offer.Length; i++)
                {
                    int row = i / perRow, col = i % perRow, inRow = Math.Min(perRow, me.Offer.Length - row * perRow);
                    float rowX = mid - (inRow * cw + (inRow - 1) * gw) / 2f;
                    slots[i] = new Rect(rowX + col * (cw + gw), top + row * (ch + gw), cw, ch);
                }
            }
            else
            {
                float x0 = mid - (me.Offer.Length * cardW + (me.Offer.Length - 1) * gap) / 2f;
                for (int i = 0; i < me.Offer.Length; i++)
                    slots[i] = new Rect(x0 + i * (cardW + gap), 150f, cardW, cardH);
            }
            DrawPickIntroUnder(me, slots, me.Offer.Length);  // the pillar out of you, and streaks behind the cards (PickIntro.cs)
            if (grid)
            {
                // Weapon mods: every one you can take, in a grid of smaller cards.
                const int perRow = 4;
                float areaW = cardW * 3 + gap * 2, gw = 18f, cw = (areaW - gw * (perRow - 1)) / perRow, ch = 150f, top = 140f;
                int rows = (me.Offer.Length + perRow - 1) / perRow;
                for (int i = 0; i < me.Offer.Length; i++)
                {
                    var u = Upgrades.All[me.Offer[i]];
                    int row = i / perRow, col = i % perRow, inRow = Math.Min(perRow, me.Offer.Length - row * perRow);
                    float rowX = mid - (inRow * cw + (inRow - 1) * gw) / 2f;
                    var slot = slots[i];
                    pickCardRects[i] = slot;
                    bool hover = !opening && slot.Contains(Event.current.mousePosition), selected = selectedCard == i;
                    float a = IntroCardsAt + Mathf.Min(i, 5) * IntroCardGap, f = EaseOut(Seg(IntroElapsed, a, a + IntroCardFly));
                    if (f > 0f)
                        DrawCardInFlight(home, slot, i, f, Mathf.Min(1f, f * 3f), () => DrawUpgradeCard(me, i, u, new Rect(slot.x, slot.y - (selected ? 8f : hover ? 3f : 0f), slot.width, slot.height), true, selected, hover, now));
                    if (!opening && Ui.HitArea(slot) && selectedCard != i)
                    {
                        selectedCard = i;
                        pickKey = 0;
                    }
                }
                by = top + rows * ch + (rows - 1) * gw + 20f;
            }
            else
            {
                const float top = 150f;
                float x0 = mid - (me.Offer.Length * cardW + (me.Offer.Length - 1) * gap) / 2f;
                for (int i = 0; i < me.Offer.Length; i++)
                {
                    var u = Upgrades.All[me.Offer[i]];
                    var slot = slots[i];
                    pickCardRects[i] = slot;
                    bool hover = !opening && slot.Contains(Event.current.mousePosition), selected = selectedCard == i;
                    float a = IntroCardsAt + Mathf.Min(i, 5) * IntroCardGap, f = EaseOut(Seg(IntroElapsed, a, a + IntroCardFly));
                    if (f > 0f)
                        DrawCardInFlight(home, slot, i, f, Mathf.Min(1f, f * 3f), () => DrawTrophyCard(me, i, u, slot, selected, selectedCard >= 0 && !selected, hover, now));
                    if (!opening && Ui.HitArea(slot) && selectedCard != i)
                    {
                        selectedCard = i;
                        pickKey = 0;
                    }
                }
                by = top + cardH + 14f;
            }
            DrawPickIntroOver(me, slots, me.Offer.Length);  // a ring as each card lands (PickIntro.cs)

            // The bottom row rises from below as the last cards land, like the HUD does when a run launches.
            float rowAlpha, rise = PickButtonsRise(me, out rowAlpha);
            var oldRowColor = GUI.color;
            Ui.Alpha = rowAlpha;
            GUI.color = new Color(oldRowColor.r, oldRowColor.g, oldRowColor.b, oldRowColor.a * rowAlpha);
            by += rise;
            if (rowAlpha <= 0.01f)
            {
                Ui.Alpha = 1f;
                GUI.color = oldRowColor;
                return;  // not risen yet
            }

            // What the pick changes, in one line (Hold Tab for everything).
            string change = PickChangeLine(me);
            if (change.Length > 0)
            {
                Ui.Label(new Rect(0f, by, W, 20f), change, 14, new Color(0.5f, 1f, 0.58f), TextAnchor.MiddleCenter, true);
                by += 28f;
            }

            // A card that grants a new ability: choose which key it goes on.
            if (selectedCard >= 0 && selectedCard < me.Offer.Length && Upgrades.All[me.Offer[selectedCard]].Grants != AbilityId.None)
            {
                var grant = Upgrades.All[me.Offer[selectedCard]].Grants;
                if (pickKey < 1 || pickKey > Upgrades.BonusSlots)
                {
                    pickKey = 1;  // default: the first free key
                    for (int k = 1; k <= Upgrades.BonusSlots; k++)
                        if (me.Slots[k].Id == AbilityId.None) { pickKey = k; break; }
                }
                const float keyW = 190f;
                float kx = mid - (keyW * 3 + 16f) / 2f;
                Ui.Label(new Rect(0f, by - 4f, W, 18f), "PUT " + Upgrades.Ability(grant).Name.ToUpperInvariant() + " ON", 12, Ui.Muted, TextAnchor.MiddleCenter, true);
                for (int k = 1; k <= Upgrades.BonusSlots; k++)
                {
                    var on = me.Slots[k].Id;
                    string label = SlotKeys[k] + (on != AbilityId.None ? "  ·  swap " + Upgrades.Ability(on).Name : "  ·  empty");
                    if (Ui.Choice(new Rect(kx + (k - 1) * (keyW + 8f), by + 16f, keyW, 32f), label, pickKey == k, ToColor(me.Def.Color)))
                        pickKey = k;
                }
                by += 60f;
            }

            // Reroll and take, the lobby's fancy buttons: the take button names your pick.
            by = Mathf.Min(by, H - 70f);
            bool wasEnabled = GUI.enabled;
            bool picked = selectedCard >= 0 && selectedCard < me.Offer.Length;
            bool canReroll = !me.OfferLegendary && !me.OfferMod;  // boss rewards can't be rerolled: no button
            const float rerollW = 240f, takeW = 320f, bgap = 18f;
            float bx = canReroll ? mid - (rerollW + bgap + takeW) / 2f : mid - takeW / 2f - rerollW - bgap;
            GUI.enabled = me.Rerolls > 0 && !game.RerollPending && !RerollLocked;  // (not off while opening: GUI.enabled draws at half opacity)
            if (canReroll && Ui.FancyButton(new Rect(bx, by, rerollW, 48f), "Reroll  " + me.Rerolls + "  [R]", Ui.Neutral, PixelArt.Get("ui_arrow"), now) && !opening)
                game.RerollOffer();
            GUI.enabled = picked && !game.RerollPending;
            string take = picked ? "Take " + Upgrades.All[me.Offer[selectedCard]].Name : "Pick a card";
            if (Ui.FancyButton(new Rect(bx + rerollW + bgap, by, takeW, 48f), take, picked ? Ui.GoldButton : Ui.Neutral, PixelArt.Get("ui_check"), now) && !opening)
                ConfirmPick();
            SyncSelection(me);  // notes when these cards appeared, for the reroll lock (ArenaApp.cs)
            GUI.enabled = wasEnabled;
            string hint = me.OfferMod ? "Every weapon mod you can still take is shown. Each stacks up to 3 times."
                : me.OfferLegendary ? "Boss rewards can't be rerolled  ·  Hold Tab for your build"
                : "1 / 2 / 3 or click a card, Enter to take it  ·  Rerolls: 2 to start, +2 a boss  ·  Hold Tab for your build";
            Ui.Label(new Rect(0f, by + 54f, W, 18f), hint, 12, new Color(1, 1, 1, 0.45f), TextAnchor.MiddleCenter);
            Ui.Alpha = 1f;
            GUI.color = oldRowColor;
        }

        /// "Regeneration 0.25 → 3.25 HP/s · Damage 100% → 110%": what the selected card changes (up to three stats),
        /// or the new ability and its key.
        string PickChangeLine(Player me)
        {
            if (selectedCard < 0 || me.Offer == null || selectedCard >= me.Offer.Length)
                return "";
            var u = Upgrades.All[me.Offer[selectedCard]];
            var now = StatSheet.Describe(me, me.Taken, 0, game.PassiveRegen);
            var after = StatSheet.Describe(me, new List<int>(me.Taken) { me.Offer[selectedCard] }, pickKey, game.PassiveRegen);
            var parts = new List<string>();
            string grant = u.Grants != AbilityId.None ? Upgrades.Ability(u.Grants).Name : null;
            foreach (var line in after)
            {
                if (line.Header || parts.Count >= 3 || grant != null && (line.Value.Contains(grant) || line.Label.Contains(grant)))
                    continue;  // (a new ability is named once, below)
                string old = null;
                foreach (var n in now)
                    if (!n.Header && n.Label == line.Label)
                        old = n.Value;
                if (old == line.Value)
                    continue;
                parts.Add(old != null ? line.Label + "  " + old + " → " + line.Value : line.Label + "  " + line.Value);
            }
            if (grant != null)
                parts.Insert(0, "New ability: " + grant + (pickKey >= 1 ? " on " + SlotKeys[pickKey] : ""));
            return parts.Count == 0 ? "" : "THIS PICK   " + string.Join("   ·   ", parts.ToArray());
        }

        static Texture2D hatchTex;

        /// A diagonal hatch for the cards' art (drawn tinted and stretched, so its stripes are chunky pixels).
        static Texture2D Hatch()
        {
            if (hatchTex != null)
                return hatchTex;
            const int w = 68, h = 44;
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = (x + y) % 6 < 2 ? Color.white : new Color(1f, 1f, 1f, 0f);
            hatchTex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            hatchTex.SetPixels(px);
            hatchTex.Apply();
            return hatchTex;
        }

        /// A tall "trophy" card: the icon big on a glowing, hatched rarity backdrop, its rarity and key, the name in
        /// outlined lettering, what it does, a note, and a rarity line along the bottom that fades at both ends.
        /// Picked: it lifts, glows and its icon bobs. Another one picked: it steps back and dims. Rare and better shine.
        void DrawTrophyCard(Player me, int index, UpgradeDef u, Rect slot, bool selected, bool behind, bool hover, float now)
        {
            Color rc = ToColor(Upgrades.RarityColor(u.Rarity));
            float lift = selected ? -16f : behind ? 8f : hover ? -4f : 0f;
            var r = new Rect(slot.x, slot.y + lift, slot.width, slot.height);
            if (selected)  // a soft glow round the picked card
                for (int k = 3; k >= 1; k--)
                    Ui.Pill(new Rect(r.x - 5f * k, r.y - 5f * k, r.width + 10f * k, r.height + 10f * k),
                        new Color(rc.r, rc.g, rc.b, (0.07f + 0.03f * Mathf.Sin(now * 4f)) * (4 - k) / 3f));
            Ui.Card(r, rc, selected ? 1f : hover ? 0.7f : 0.4f);

            // The art: a glow of the rarity colour, a pixel hatch, then the icon big.
            var art = new Rect(r.x + 3f, r.y + 3f, r.width - 6f, 176f);
            Ui.Pill(art, new Color(rc.r * 0.12f, rc.g * 0.12f, rc.b * 0.12f, 0.95f));
            for (int k = 0; k < 5; k++)
            {
                float s = 1f - k * 0.17f;
                var g = new Rect(art.center.x - art.width * s / 2f, art.center.y - art.height * s / 2f + 6f, art.width * s, art.height * s);
                Ui.Pill(g, new Color(rc.r, rc.g, rc.b, 0.06f + 0.02f * k + (selected ? 0.02f : 0f)));
            }
            Ui.Icon(art, Hatch(), new Color(rc.r, rc.g, rc.b, selected ? 0.1f : 0.07f));
            var icon = PixelArt.ForUpgrade(u.Name);
            float bob = selected ? 4f * Mathf.Sin(now * 3f) : 0f, isz = 108f;
            Ui.Icon(new Rect(art.center.x - isz / 2f, art.center.y - isz / 2f + 8f + bob, isz, isz), icon);
            if (u.Rarity >= Rarity.Rare)
            {
                // A slow shine across rare and better cards (quicker and brighter on legendaries).
                bool leg = u.Rarity == Rarity.Legendary;
                float sweep = MathX.Fmod(now * (leg ? 0.6f : 0.35f) + index * 0.3f, 1.6f) - 0.3f;
                GUI.BeginGroup(new Rect(r.x + 3f, r.y + 3f, r.width - 6f, r.height - 6f));
                Ui.Fill(new Rect(sweep * r.width, 0f, 22f, r.height), new Color(1f, 1f, 1f, leg ? 0.08f : 0.05f));
                Ui.Fill(new Rect(sweep * r.width + 28f, 0f, 7f, r.height), new Color(1f, 1f, 1f, leg ? 0.06f : 0.04f));
                GUI.EndGroup();
            }
            // Rarity chip and key, top corners.
            string rarity = Upgrades.RarityName(u.Rarity);
            float rw = 22f + rarity.Length * 7.2f;
            var chipR = new Rect(r.x + 12f, r.y + 12f, rw, 20f);
            Ui.Pill(chipR, new Color(rc.r, rc.g, rc.b, 0.9f));
            Ui.Label(chipR, rarity, 10, new Color(0.06f, 0.06f, 0.1f), TextAnchor.MiddleCenter, true);
            DrawKeyCap(new Rect(r.xMax - 34f, r.y + 12f, 0f, 20f), (index + 1).ToString(), selected ? rc : new Color(1f, 1f, 1f, 0.6f));

            // Name, description (and what mastering a granted ability adds), a note, the fading rarity line.
            Color nameLow = selected ? Color.Lerp(rc, Color.white, 0.25f) : new Color(0.78f, 0.83f, 0.94f);
            Ui.Fancy(new Rect(r.x + 8f, r.y + 184f, r.width - 16f, 34f), u.Name, u.Name.Length > 16 ? 21 : 25, Color.white, nameLow, TextAnchor.MiddleCenter,
                selected ? (Color?)new Color(rc.r, rc.g, rc.b, 0.6f) : null);
            string desc = Upgrades.DescFor(u, me.Weapon);
            int ds = desc.Length > 110 ? 12 : 14;
            string note = UpgradeNote(me, index, u);
            var body = new Rect(r.x + 16f, r.y + 224f, r.width - 32f, r.height - 224f - 44f);
            if (u.Grants != AbilityId.None)
            {
                DrawDescAndMastery(body, desc, u.Grants, ds, Ui.Text, true);
                note = "";
            }
            else
                Ui.Label(body, desc, ds, selected ? Color.white : Ui.Text, TextAnchor.UpperCenter, false, true);
            if (note.Length > 0)
                Ui.Label(new Rect(r.x, r.yMax - 38f, r.width, 16f), note, 11, new Color(1, 1, 1, 0.55f), TextAnchor.MiddleCenter, true);
            for (int k = 0; k < 24; k++)  // the line along the bottom, fading out at both ends
            {
                float f = Mathf.Sin(MathX.Pi * (k + 0.5f) / 24f);
                Ui.Fill(new Rect(r.x + 8f + (r.width - 16f) * k / 24f, r.yMax - 12f, (r.width - 16f) / 24f + 0.5f, selected ? 4f : 3f),
                    new Color(rc.r, rc.g, rc.b, f * (selected ? 0.95f : 0.55f)));
            }
            if (behind)  // another card is picked: this one steps back
                Ui.Pill(r, new Color(0.01f, 0.012f, 0.03f, 0.32f));
        }

        /// One upgrade card: rarity ribbon, pixel-art icon in a glowing frame, name, description and a note.
        void DrawUpgradeCard(Player me, int index, UpgradeDef u, Rect r, bool compact, bool selected, bool hover, float now)
        {
            Color rc = ToColor(Upgrades.RarityColor(u.Rarity));
            if (selected)  // a soft glow under the selected card
                Ui.Pill(new Rect(r.x - 6f, r.y - 6f, r.width + 12f, r.height + 12f), new Color(rc.r, rc.g, rc.b, 0.18f + 0.06f * Mathf.Sin(now * 4f)));
            Ui.Card(r, rc, selected ? 1f : hover ? 0.75f : 0.45f);
            // A wash of the rarity color across the top of the card.
            Ui.Pill(new Rect(r.x + 3f, r.y + 3f, r.width - 6f, compact ? 30f : 120f), new Color(rc.r, rc.g, rc.b, 0.08f));
            if (u.Rarity == Rarity.Legendary)
            {
                // A shimmer that sweeps across legendary cards.
                float sweep = MathX.Fmod(now * 0.6f + index * 0.3f, 1.6f) - 0.3f;
                GUI.BeginGroup(r);
                Ui.Fill(new Rect(sweep * r.width, 0f, 18f, r.height), new Color(1f, 0.9f, 0.6f, 0.07f));
                Ui.Fill(new Rect(sweep * r.width + 22f, 0f, 6f, r.height), new Color(1f, 0.9f, 0.6f, 0.05f));
                GUI.EndGroup();
            }
            var icon = PixelArt.ForUpgrade(u.Name);
            float bob = selected ? 3f * Mathf.Sin(now * 3f) : 0f;
            string note = UpgradeNote(me, index, u);
            if (compact)
            {
                // Weapon mod grid: icon on the left, name and stacks on the right, description under.
                var frame = new Rect(r.x + 12f, r.y + 14f, 52f, 52f);
                Ui.Pill(frame, new Color(rc.r * 0.3f, rc.g * 0.3f, rc.b * 0.3f, 0.9f));
                Ui.Icon(new Rect(frame.x + 2f, frame.y + 2f + bob * 0.5f, 48f, 48f), icon);
                Ui.Label(new Rect(r.x + 74f, r.y + 14f, r.width - 82f, 16f), note, 10, rc, TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(r.x + 74f, r.y + 30f, r.width - 82f, 36f), u.Name, 15, Color.white, TextAnchor.UpperLeft, true, true);
                Ui.Label(new Rect(r.x + 12f, r.y + 74f, r.width - 24f, 52f), Upgrades.DescFor(u, me.Weapon), 11, Ui.Text, TextAnchor.UpperLeft, false, true);
                Ui.Label(new Rect(r.x, r.yMax - 22f, r.width - 12f, 16f), selected ? "SELECTED" : "[" + (index + 1) + "]", 11,
                    selected ? rc : new Color(1, 1, 1, 0.45f), TextAnchor.MiddleRight, true);
                return;
            }
            // Rarity ribbon.
            string rarity = Upgrades.RarityName(u.Rarity);
            float rw = 24f + rarity.Length * 7.5f;
            var ribbon = new Rect(r.x + r.width / 2f - rw / 2f, r.y + 12f, rw, 20f);
            Ui.Pill(ribbon, new Color(rc.r, rc.g, rc.b, 0.85f));
            Ui.Label(ribbon, rarity, 10, new Color(0.06f, 0.06f, 0.1f), TextAnchor.MiddleCenter, true);
            // Icon in a framed, glowing tile.
            var tile = new Rect(r.x + r.width / 2f - 46f, r.y + 42f, 92f, 92f);
            Ui.Pill(new Rect(tile.x - 4f, tile.y - 4f, tile.width + 8f, tile.height + 8f), new Color(rc.r, rc.g, rc.b, selected ? 0.35f : 0.18f));
            Ui.Pill(tile, new Color(rc.r * 0.22f, rc.g * 0.22f, rc.b * 0.22f, 1f));
            Ui.Icon(new Rect(tile.x + 10f, tile.y + 10f + bob, 72f, 72f), icon);
            Ui.Label(new Rect(r.x + 10f, r.y + 144f, r.width - 20f, 28f), u.Name, 21, Color.white, TextAnchor.MiddleCenter, true);
            Ui.Fill(new Rect(r.x + 40f, r.y + 176f, r.width - 80f, 1f), new Color(rc.r, rc.g, rc.b, 0.35f));
            string desc = Upgrades.DescFor(u, me.Weapon);
            int ds = desc.Length > 110 ? 12 : 14;
            if (u.Grants != AbilityId.None)
            {
                // A card that gives an ability also says what mastering it adds (it may take the note's line: the key
                // chooser below the cards says the same).
                DrawDescAndMastery(new Rect(r.x + 18f, r.y + 184f, r.width - 36f, r.height - 184f - 36f), desc, u.Grants, ds, Ui.Text, true);
                note = "";
            }
            else
                Ui.Label(new Rect(r.x + 18f, r.y + 184f, r.width - 36f, 66f), desc, ds, Ui.Text, TextAnchor.UpperCenter, false, true);
            if (note.Length > 0)
                Ui.Label(new Rect(r.x, r.yMax - 50f, r.width, 16f), note, 11, new Color(1, 1, 1, 0.55f), TextAnchor.MiddleCenter, true);
            var key = new Rect(r.x + r.width / 2f - 50f, r.yMax - 30f, 100f, 20f);
            Ui.Pill(key, selected ? new Color(rc.r, rc.g, rc.b, 0.85f) : new Color(1f, 1f, 1f, 0.08f));
            Ui.Label(key, selected ? "SELECTED" : "PRESS " + (index + 1), 10, selected ? new Color(0.06f, 0.06f, 0.1f) : new Color(1, 1, 1, 0.6f), TextAnchor.MiddleCenter, true);
        }

        /// The small line on a card: where a new ability goes, a role restriction, or how many you have.
        string UpgradeNote(Player me, int index, UpgradeDef u)
        {
            int have = Upgrades.Count(me.Taken, me.Offer[index]);
            if (u.Rarity == Rarity.Mod)
                return have > 0 ? "YOU HAVE " + have + " / " + u.MaxStacks : "NEW";
            if (u.Grants != AbilityId.None)
            {
                string cls = u.ClassAbility ? "Class ability  ·  " : "";
                if (index == selectedCard && pickKey >= 1)
                    return cls + "Goes on " + SlotKeys[pickKey] + " (choose below)";
                return cls + "You choose its key";
            }
            if (u.Role.HasValue)
                return Defs.Get(u.Role.Value).Name + " only";
            return have > 0 ? "You have " + have : "";
        }

        /// Your current stats; with a card selected, the ones it would change show their new value.
        /// shopPreview: an upgrade from the Bazaar's shelf to preview instead of the picker's selection.
        void DrawStatPanel(Player me, Rect r, int shopPreview = -1)
        {
            var now = StatSheet.Describe(me, me.Taken, 0, game.PassiveRegen);
            List<StatLine> after = null;
            if (shopPreview >= 0)
                after = StatSheet.Describe(me, new List<int>(me.Taken) { shopPreview }, 0, game.PassiveRegen);
            else if (selectedCard >= 0 && me.Offer != null && selectedCard < me.Offer.Length)
            {
                var preview = new List<int>(me.Taken) { me.Offer[selectedCard] };
                after = StatSheet.Describe(me, preview, pickKey, game.PassiveRegen);
            }
            var lines = after ?? now;
            var green = new Color(0.45f, 1f, 0.5f);
            float y = r.y;
            for (int i = 0; i < lines.Count && y < r.yMax - 20f; i++)
            {
                var line = lines[i];
                if (line.Header)
                {
                    y += 6f;
                    Label(new Rect(r.x + 12f, y, r.width - 24f, 18f), line.Label.ToUpperInvariant(), 12, new Color(1f, 0.8f, 0.45f), TextAnchor.MiddleLeft, true);
                    y += 20f;
                    continue;
                }
                // Compare with the same row in the current stats to spot what the card changes.
                string old = null;
                foreach (var n in now)
                    if (n.Label == line.Label && !n.Header)
                        old = n.Value;
                bool changed = after != null && old != line.Value;
                Label(new Rect(r.x + 12f, y, r.width - 24f, 18f), line.Label, 13, changed ? green : new Color(1, 1, 1, 0.75f), TextAnchor.MiddleLeft);
                if (changed && old != null)
                {
                    // old value, an arrow, the new value (right-aligned)
                    float right = r.xMax - 12f, nw = Ui.TextWidth(line.Value, 13, true);
                    Label(new Rect(right - nw - 2f, y, nw + 4f, 18f), line.Value, 13, green, TextAnchor.MiddleRight, true);
                    var arrow = new Rect(right - nw - 18f, y + 3f, 12f, 12f);
                    Ui.Icon(arrow, PixelArt.Get("ui_arrow"), green);
                    Label(new Rect(r.x + 12f, y, arrow.x - 4f - (r.x + 12f), 18f), old, 13, new Color(green.r, green.g, green.b, 0.6f), TextAnchor.MiddleRight);
                }
                else
                    Label(new Rect(r.x + 12f, y, r.width - 24f, 18f), changed ? "NEW " + line.Value : line.Value, 13, changed ? green : Color.white, TextAnchor.MiddleRight, changed);
                y += 20f;
            }
        }

        const float StatsSidebarW = 270f;

        /// YOUR STATS (Tab menu and upgrade picker): a see-through strip down the whole left edge of the screen,
        /// with the title and your weapon and ability at the top. Returns the space below for the stats.
        Rect StatsSidebar(Player me)
        {
            var r = new Rect(0f, 0f, StatsSidebarW, H);
            var rc = ToColor(me.Def.Color);
            Ui.Fill(r, new Color(0.05f, 0.07f, 0.13f, 0.6f));
            Ui.Fill(new Rect(r.xMax - 1f, 0f, 1f, H), new Color(1f, 1f, 1f, 0.22f));
            Ui.Fill(new Rect(r.xMax - 5f, 0f, 4f, H), new Color(rc.r, rc.g, rc.b, 0.18f));
            Ui.Label(new Rect(20f, 20f, r.width - 40f, 24f), "YOUR STATS", 16, Color.white, TextAnchor.MiddleLeft, true);
            Label(new Rect(20f, 44f, r.width - 40f, 18f), me.Weapon.Name + " · " + Upgrades.Ability(me.RoleAbility).Name, 12, rc, TextAnchor.MiddleLeft);
            Ui.Fill(new Rect(20f, 70f, r.width - 40f, 1f), new Color(1f, 1f, 1f, 0.1f));
            return new Rect(8f, 80f, r.width - 20f, H - 92f);
        }
    }
}
