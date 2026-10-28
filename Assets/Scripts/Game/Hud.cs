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
            DrawWaveCorner();
            if (game.Recorder != null)  // this match is being recorded (hosts only)
                Ui.Label(new Rect(W - 70f, 34f, 62f, 16f), "● REC", 10, new Color(1f, 0.4f, 0.4f, 0.8f), TextAnchor.MiddleRight, true);
            if (aimDebug && me != null)
                DrawAimDebug(me);
            DrawBossBars();

            // Names, level-up popups and downed labels over players (hidden while they're under
            // the between-waves banner, so they don't show on top of its text).
            bool banner = game.State == MatchState.Intermission && (me == null || me.Offer == null);
            foreach (var p in game.Players.Values)
            {
                float r = p.Def.BodyR;
                V2 at = p.Disp * WorldToUi;  // arena -> HUD coordinates
                r *= WorldToUi;
                if (banner && at.X > BannerRect.x - 100f && at.X < BannerRect.xMax + 100f && at.Y > BannerRect.y - 20f && at.Y < BannerRect.yMax + r + 60f)
                    continue;
                Ui.Shadowed(new Rect(at.X - 100f, at.Y - r - 30f, 200f, 18f), p.Name + (p.Id == game.MyId ? " (you)" : ""), 12,
                    ToColor(p.Def.Color.WithAlpha(0.95f)));
                if (p.LevelFlash > 0f)
                {
                    float rise = (1.5f - p.LevelFlash) * 22f;
                    Ui.Shadowed(new Rect(at.X - 100f, at.Y - r - 54f - rise, 200f, 24f), "LEVEL UP!", 19,
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

            DrawPlayerCorner();

            if (me != null)
                DrawAbilityBar(me);

            switch (game.State)
            {
                case MatchState.Intermission:
                    if (me != null && me.Offer != null)
                    {
                        if (!tabHeld)
                            DrawUpgradePicker(me);  // holding Tab swaps the cards for the Tab menu
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
                        : "Wave " + (game.Wave + 1) + " starts in " + Mathf.CeilToInt(game.StateTimer);
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
                DrawTabMenu(me);
            else if (me != null && game.State != MatchState.GameOver && game.State != MatchState.Victory)
            {
                var hint = new Rect(W - 196f, 10f, 180f, 22f);
                Ui.Pill(hint, new Color(0f, 0f, 0f, 0.35f));
                Ui.Label(hint, "Hold TAB  ·  stats & upgrades", 11, new Color(1f, 1f, 1f, 0.55f), TextAnchor.MiddleCenter, true);
            }
        }

        // --- Tab: your stats and loadout ---------------------------------------

        /// Held with Tab. Only drawn on this player's screen; nothing is sent to the
        /// other player and the game doesn't pause.
        void DrawTabMenu(Player me)
        {
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
        }

        /// Stats, grouped so they're quick to scan (in the stats sidebar).
        void DrawTabStats(Player me, Rect box)
        {
            var lines = new Dictionary<string, string>();
            foreach (var line in StatSheet.Describe(me, me.Taken))
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
        void DrawTabAbilities(Player me, Rect box)
        {
            Ui.HudPanel(box);
            float x = box.x + 12f, w = box.width - 24f, y = box.y + 12f;
            Ui.Label(new Rect(x + 4f, y, w, 16f), "WEAPON", 11, Ui.Gold, TextAnchor.MiddleLeft, true);
            y += 20f;
            Ui.Label(new Rect(x + 4f, y, w, 22f), me.Weapon.Name, 17, Color.white, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(x + 4f, y + 22f, w - 8f, 18f), me.Weapon.Desc, 12, Ui.Muted, TextAnchor.UpperLeft, false, true);
            y += 54f;
            Ui.Label(new Rect(x + 4f, y, w, 16f), "ABILITIES", 11, Ui.Gold, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(x + 4f, y, w - 8f, 16f), "click a key to move an ability", 10, Ui.Muted, TextAnchor.MiddleRight);
            y += 22f;
            for (int i = 0; i < me.Slots.Length; i++)
            {
                var slot = me.Slots[i];
                if (slot.Id == AbilityId.None)
                    continue;
                var a = Upgrades.Ability(slot.Id);
                bool ult = i == Upgrades.UltSlot;
                var tile = new Rect(x, y, w, 80f);
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
                var descR = new Rect(tile.x + 8f, tile.y + 47f, tile.width - 16f - (bonus ? 118f : 0f), 30f);
                if (hover || level >= Game.MasteryMax)
                    Ui.Label(descR, (level >= Game.MasteryMax ? "" : "At Lv 5: ") + Game.Capstone(slot.Id), 11, level >= Game.MasteryMax ? Ui.Gold : new Color(0.75f, 0.85f, 1f), TextAnchor.UpperLeft, false, true);
                else
                    Ui.Label(descR, a.Desc, 11, Ui.Muted, TextAnchor.UpperLeft, false, true);
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
                        if (k != i && GUI.Button(kr, GUIContent.none, GUIStyle.none))
                            game.SetAbilityKey(slot.Id, k);
                    }
                }
                y += 84f;
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

        void DrawRunSummary()
        {
            bool won = game.State == MatchState.Victory;
            Ui.Fill(new Rect(0, 0, W, H), new Color(0.01f, 0.01f, 0.03f, 0.75f));
            var panel = new Rect(70f, 30f, W - 140f, H - 60f);
            Ui.Panel(panel, true);
            Ui.Title(new Rect(panel.x, panel.y + 14f, panel.width, 56f), game.Practice != null ? (won ? "PRACTICE COMPLETE" : "PRACTICE OVER") : won ? "VICTORY!" : "GAME OVER", game.Practice != null ? 40 : 46, won ? Ui.Gold : new Color(1f, 0.45f, 0.45f));
            Ui.Label(new Rect(panel.x, panel.y + 66f, panel.width, 22f),
                game.Practice != null ? (won ? "You cleared all " + game.Practice.Waves.Count + " practice waves" : "You made it to practice wave " + Math.Max(1, game.PracticeIndex + 1) + " of " + game.Practice.Waves.Count)
                : won ? "You beat all " + Game.MaxWave + " waves and The Eclipse" : "You made it to wave " + game.Wave, 16, Ui.Muted, TextAnchor.MiddleCenter);
            if (game.Recorder != null)
                Ui.Label(new Rect(panel.x + 20f, panel.yMax - 22f, panel.width - 40f, 16f), "Session recorded: " + game.Recorder.Path, 10, Ui.Muted, TextAnchor.MiddleLeft);

            // Run totals.
            int kills = 0;
            foreach (var p in game.Players.Values)
                kills += p.Run.Kills;
            int secs = Mathf.RoundToInt(game.RunTime);
            string[,] chips = { { "WAVE", game.Wave.ToString() }, { "TIME", secs / 60 + ":" + (secs % 60).ToString("00") }, { "SCORE", game.Score.ToString() }, { "KILLS", kills.ToString() } };
            float cw = 170f, cx = panel.x + panel.width / 2f - (cw * 4f + 30f) / 2f;
            for (int i = 0; i < 4; i++)
            {
                var c = new Rect(cx + i * (cw + 10f), panel.y + 98f, cw, 58f);
                Ui.Fill(c, new Color(1f, 1f, 1f, 0.04f));
                Ui.Label(new Rect(c.x, c.y + 6f, c.width, 16f), chips[i, 0], 11, Ui.Muted, TextAnchor.MiddleCenter, true);
                Ui.Label(new Rect(c.x, c.y + 22f, c.width, 30f), chips[i, 1], 24, Color.white, TextAnchor.MiddleCenter, true);
            }

            if (!game.RunStatsReady)
                Ui.Label(new Rect(panel.x, panel.y + 300f, panel.width, 30f), "Collecting results…", 18, Ui.Muted, TextAnchor.MiddleCenter);
            else
            {
                var players = new List<Player>(game.Players.Values);
                float colW = (panel.width - 60f - 20f * (players.Count - 1)) / Mathf.Max(1, players.Count);
                for (int i = 0; i < players.Count; i++)
                    DrawPlayerSummary(players[i], players, new Rect(panel.x + 30f + i * (colW + 20f), panel.y + 168f, colW, 410f));
            }

            // Buttons.
            float by = panel.yMax - 66f;
            if (session.IsHost)
            {
                float bx = panel.x + 30f;
                if (Ui.Button(new Rect(bx, by, 200f, 46f), "Play again  [R]", Ui.Primary))
                    session.StartMatch();
                bx += 212f;
                if (Ui.Button(new Rect(bx, by, 200f, 46f), "Back to lobby  [L]"))
                    session.ReturnToLobby();
                bx += 212f;
                if (won && Ui.Button(new Rect(bx, by, 280f, 46f), "Keep going (endless)  [C]", Ui.GoldButton))
                    game.ContinueEndless();
            }
            else
                Ui.Label(new Rect(panel.x + 30f, by, 400f, 46f), "Waiting for the host…", 16, Ui.Muted);
            if (Ui.Button(new Rect(panel.xMax - 190f, by, 160f, 46f), "Leave", Ui.Danger))
                session.Leave("");
        }

        void DrawPlayerSummary(Player p, List<Player> all, Rect r)
        {
            var color = ToColor(p.Def.Color);
            Ui.Panel(r, p.Id == game.MyId);
            Ui.Fill(new Rect(r.x + 3f, r.y + 12f, 4f, 44f), color);
            Ui.Label(new Rect(r.x + 18f, r.y + 10f, r.width - 120f, 26f), p.Name, 20, Color.white, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(r.xMax - 110f, r.y + 10f, 94f, 26f), "LEVEL " + p.Level, 14, Ui.Gold, TextAnchor.MiddleRight, true);
            Ui.Label(new Rect(r.x + 18f, r.y + 36f, r.width - 36f, 20f), Defs.KitName(p.Kit) + " " + p.Def.Name + "  ·  " + p.Weapon.Name, 13, color);

            // Awards: rows where this player has the best number (and it isn't zero).
            float ax = r.x + 18f;
            if (all.Count > 1)
                foreach (var row in SummaryRows)
                {
                    if (row.Award == null || row.Value(p.Run) <= 0f)
                        continue;
                    bool best = true;
                    foreach (var o in all)
                        if (o != p && row.Value(o.Run) >= row.Value(p.Run))
                            best = false;
                    if (!best || ax > r.xMax - 120f)
                        continue;
                    float bw = 16f + row.Award.Length * 7.2f;
                    Ui.Badge(new Rect(ax, r.y + 62f, bw, 22f), row.Award.ToUpperInvariant(), Ui.GoldButton);
                    ax += bw + 6f;
                }

            float y = r.y + 96f;
            foreach (var row in SummaryRows)
            {
                float mine = row.Value(p.Run), top = 0f;
                foreach (var o in all)
                    top = Mathf.Max(top, row.Value(o.Run));
                bool leading = all.Count > 1 && row.Award != null && mine > 0f && mine >= top;
                Ui.Label(new Rect(r.x + 18f, y, 150f, 20f), row.Label, 14, Ui.Muted);
                Ui.Label(new Rect(r.x + 160f, y, r.width - 178f, 20f), row.Format(p.Run), 14, leading ? Ui.Gold : Color.white, TextAnchor.MiddleRight, true);
                if (top > 0f && row.Award != null)
                    Ui.Meter(new Rect(r.x + 18f, y + 21f, r.width - 36f, 2f), mine / top, leading ? Ui.Gold : color * 0.8f);
                y += 27f;
            }

            // Upgrades picked, by rarity.
            var counts = new int[Enum.GetValues(typeof(Rarity)).Length];
            var legendaries = new List<string>();
            foreach (int id in p.Taken)
            {
                counts[(int)Upgrades.All[id].Rarity]++;
                if (Upgrades.All[id].Rarity == Rarity.Legendary)
                    legendaries.Add(Upgrades.All[id].Name);
            }
            y += 6f;
            Ui.Label(new Rect(r.x + 18f, y, 120f, 20f), "Upgrades", 14, Ui.Muted);
            float px = r.x + 120f;
            for (int i = 0; i < counts.Length; i++)
            {
                if (counts[i] == 0)
                    continue;
                Ui.Badge(new Rect(px, y, 44f, 20f), counts[i].ToString(), ToColor(Upgrades.RarityColor((Rarity)i)));
                px += 50f;
            }
            if (legendaries.Count > 0)
                Ui.Label(new Rect(r.x + 18f, y + 24f, r.width - 36f, 20f), string.Join(", ", legendaries.ToArray()), 12, ToColor(Upgrades.RarityColor(Rarity.Legendary)));
        }

        /// Name and health bar for each boss on the field, plus warnings for their mechanics: wide across the
        /// top middle, which the rest of the HUD leaves free.
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
                if (e.Type == EnemyType.HiveQueen && e.Phase == 1)
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
                else if (e.Type == EnemyType.Singularity && ImCaged())
                {
                    note = "CAGED — shoot the wells around you!";
                    noteColor = new Color(0.9f, 0.55f, 1f);
                }
                else if (e.Type == EnemyType.Singularity && AnyWells())
                    note = "The corner wells swallow your shots — kill them!";
                else if (e.Type == EnemyType.Lich && e.Phase == 1)
                    note = "IMMUNE — destroy the phylactery!";
                else if (Game.Hungry(e))
                {
                    note = "HUNGRY — stop shooting, your hits heal it!";
                    noteColor = new Color(1f, 0.45f, 0.7f);
                }
                else if (e.Type == EnemyType.Eclipse)
                    note = "Phase " + (e.Phase + 1) + " of 3";
                float h = note != null ? 62f : 46f;
                var panel = new Rect(x0, y, panelW, h);
                Ui.HudPanel(panel, col);
                float frac = Mathf.Clamp01(e.Hp / e.MaxHp);
                // Name with small diamonds either side, and the health percentage.
                Ui.Pill(new Rect(panel.x + 14f, panel.y + 12f, 6f, 6f), col);
                Ui.Label(new Rect(panel.x + 26f, panel.y + 5f, panelW - 120f, 20f), e.Def.Name.ToUpperInvariant(), 14, Color.white, TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(panel.xMax - 90f, panel.y + 5f, 76f, 20f), Mathf.CeilToInt(frac * 100f) + "%", 13, Ui.Muted, TextAnchor.MiddleRight, true);
                var bar = new Rect(panel.x + 14f, panel.y + 27f, panelW - 28f, 12f);
                Ui.Bar(bar, frac, col);
                for (int k = 1; k < 10; k++)  // tick marks every 10%
                    Ui.Fill(new Rect(bar.x + bar.width * k / 10f, bar.y + 2f, 1f, bar.height - 4f), new Color(0f, 0f, 0f, 0.35f));
                if (note != null)
                    Ui.Label(new Rect(panel.x, panel.y + 41f, panelW, 18f), note, 12, noteColor, TextAnchor.MiddleCenter, true);
                y += h + 6f;
            }
            string warning = null;
            if (game.TwinReviveT > 0f)
                warning = "Kill the other Reaper in " + game.TwinReviveT.ToString("0.0") + "s or its twin revives!";
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
            if (game.CurrentPath != PathId.None)
            {
                DrawPathChip(SideL, y);  // PathsUi.cs
                y -= 24f;
            }
            if (DrawGoldChip(SideL, y))  // ShopUi.cs
                y -= 24f;
            DrawActiveModifiers(SideL, y + 2f);  // ModifiersUi.cs
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
            Ui.Bar(hp, frac, HealthColor(frac));
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
                Ui.Bar(hpBar, frac, hpCol);
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
                var slot = me.Slots[i];
                var r = new Rect(x0 + i * (tileW + gap), y, tileW, tileH);
                bool ultimate = i == Upgrades.UltSlot;
                if (slot.Id == AbilityId.None)
                {
                    Ui.HudPanel(r);
                    DrawKeyCap(new Rect(r.x + 8f, r.y + 7f, 0f, 16f), SlotKeys[i], new Color(1f, 1f, 1f, 0.45f));
                    Ui.Label(new Rect(r.x + 8f, r.y + 26f, r.width - 16f, 18f), "Empty", 12, new Color(1f, 1f, 1f, 0.45f), TextAnchor.MiddleLeft, true);
                    Ui.Label(new Rect(r.x + 8f, r.y + 40f, r.width - 16f, 14f), "from a very rare upgrade", 9, new Color(1f, 1f, 1f, 0.35f), TextAnchor.MiddleLeft);
                    continue;
                }
                bool ready = slot.Charges > 0;
                float cd = game.Cooldown(me, i);
                float fill = slot.Charges >= slot.MaxCharges ? 1f : 1f - slot.Timer / Mathf.Max(0.01f, cd);
                Color accent = ultimate ? Ui.Gold : ToColor(me.Def.Color);
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
                Ui.HudPanel(r, ready ? new Color(accent.r, accent.g, accent.b, ultimate ? 0.6f + 0.4f * pulse : 1f) : (Color?)null);

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
                    Ui.Fancy(new Rect(r.x + 8f, r.y + 5f, r.width - 16f, 20f), ultimate ? "READY!" : "READY", 11, hi, lo, TextAnchor.MiddleRight,
                        new Color(lo.r, lo.g, lo.b, ultimate ? 0.6f + 0.4f * pulse : 0.8f));
                }
                else
                    Ui.Fancy(new Rect(r.x + 8f, r.y + 4f, r.width - 16f, 20f), ultimate ? Mathf.FloorToInt(fill * 100f) + "%" : slot.Timer.ToString(slot.Timer < 10f ? "0.0" : "0") + "s",
                        14, Color.white, new Color(0.7f, 0.8f, 1f), TextAnchor.MiddleRight);
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
                        Ui.Pill(new Rect(r.xMax - 12f - k * 9f, r.y + 30f, 6f, 6f), k < slot.Charges ? accent : new Color(1f, 1f, 1f, 0.15f));
            }
            DrawMasteryToasts(x0 + total / 2f, y - 64f);
        }

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

        void DrawUpgradePicker(Player me)
        {
            SyncSelection(me);
            float now = Time.unscaledTime;
            Ui.Fill(new Rect(0, 0, W, H), new Color(0.01f, 0.012f, 0.03f, 0.8f));
            // Stats on the left; the cards and buttons fill the rest.
            const float cardW = 290f, cardH = 300f, gap = 22f;
            float x0 = StatsSidebarW + 20f;
            float areaW = cardW * 3 + gap * 2, mid = x0 + areaW / 2f;
            DrawStatPanel(me, StatsSidebar(me));

            // Heading: a small tag, a big title, and how many picks are left.
            Color theme = me.OfferLegendary ? ToColor(Upgrades.RarityColor(Rarity.Legendary))
                : me.OfferMod ? ToColor(Upgrades.RarityColor(Rarity.Mod)) : Ui.Gold;
            string tag = me.OfferLegendary ? "BOSS REWARD" : me.OfferMod ? "BOSS REWARD · WEAPON MOD" : "LEVEL " + me.Level;
            string title = me.OfferLegendary ? "Choose a legendary upgrade" : me.OfferMod ? "Choose how your shots behave" : "Choose an upgrade";
            float tagW = 30f + tag.Length * 8f;
            var tagRect = new Rect(mid - tagW / 2f, 104f, tagW, 24f);
            Ui.Pill(tagRect, new Color(theme.r, theme.g, theme.b, 0.25f));
            Ui.Label(tagRect, tag, 12, theme, TextAnchor.MiddleCenter, true);
            Ui.Title(new Rect(x0, 128f, areaW, 40f), title.ToUpperInvariant(), 30, Color.white);
            int more = me.PendingPicks + me.PendingLegendary + me.PendingMods - 1;
            if (more > 0)
                Ui.Label(new Rect(x0, 164f, areaW, 18f), more + " more to choose after this", 13, Ui.Muted, TextAnchor.MiddleCenter, true);

            // Weapon mods show the whole list in a grid of smaller cards.
            bool grid = me.Offer.Length > 3;
            const int perRow = 4;
            float cw = grid ? (areaW - gap * (perRow - 1)) / perRow : cardW, ch = grid ? 150f : cardH;
            int rows = grid ? (me.Offer.Length + perRow - 1) / perRow : 1;
            float top = grid ? 196f : 196f;
            for (int i = 0; i < me.Offer.Length; i++)
            {
                var u = Upgrades.All[me.Offer[i]];
                int row = grid ? i / perRow : 0, col = grid ? i % perRow : i;
                int inRow = grid ? Math.Min(perRow, me.Offer.Length - row * perRow) : me.Offer.Length;
                float rowX = mid - (inRow * cw + (inRow - 1) * gap) / 2f;  // center short rows
                var slot = new Rect(rowX + col * (cw + gap), top + row * (ch + gap), cw, ch);
                bool hover = slot.Contains(Event.current.mousePosition);
                bool selected = selectedCard == i;
                // The card lifts a little when hovered and more when selected.
                var r = new Rect(slot.x, slot.y - (selected ? 10f : hover ? 4f : 0f), slot.width, slot.height);
                DrawUpgradeCard(me, i, u, r, grid, selected, hover, now);
                if (GUI.Button(slot, GUIContent.none, GUIStyle.none) && selectedCard != i)
                {
                    selectedCard = i;
                    pickKey = 0;
                }
            }

            // Confirm and reroll buttons under the cards.
            const float btnW = 280f;
            float by = top + rows * ch + (rows - 1) * gap + 26f;

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
                const float keyW = 170f;
                float kx = mid - (keyW * 3 + 16f) / 2f;
                Ui.Label(new Rect(x0, by - 6f, areaW, 18f), "Put " + Upgrades.Ability(grant).Name + " on:", 13, Ui.Muted, TextAnchor.MiddleCenter, true);
                for (int k = 1; k <= Upgrades.BonusSlots; k++)
                {
                    var on = me.Slots[k].Id;
                    string label = SlotKeys[k] + (on != AbilityId.None ? "  ·  swap " + Upgrades.Ability(on).Name : "  ·  empty");
                    if (Ui.Choice(new Rect(kx + (k - 1) * (keyW + 8f), by + 14f, keyW, 32f), label, pickKey == k, ToColor(me.Def.Color)))
                        pickKey = k;
                }
                by += 58f;
            }
            bool wasEnabled = GUI.enabled;
            GUI.enabled = selectedCard >= 0 && !game.RerollPending;
            string confirmText = selectedCard >= 0 ? "Confirm  [Enter]" : "Pick a card first";  // the selected card shows its name
            if (Ui.Button(new Rect(mid - btnW - 10f, by, btnW, 46f), confirmText, selectedCard >= 0 ? Ui.Primary : Ui.Neutral))
                ConfirmPick();
            GUI.enabled = me.Rerolls > 0 && !game.RerollPending && !me.OfferLegendary && !me.OfferMod;
            if (Ui.Button(new Rect(mid + 10f, by, btnW, 46f), "Reroll (" + me.Rerolls + " left)  [R]"))
                game.RerollOffer();
            GUI.enabled = wasEnabled;
            string hint = me.OfferLegendary || me.OfferMod ? "Boss rewards can't be rerolled" : "Rerolls: start with " + Game.StartingRerolls + ", +2 for every boss you kill";
            if (me.OfferMod)
                hint = "Every weapon mod you can still take is shown. Each stacks up to 3 times.";
            Ui.Label(new Rect(x0, by + 50f, areaW, 18), hint, 12, new Color(1, 1, 1, 0.45f), TextAnchor.MiddleCenter);
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
            Ui.Label(new Rect(r.x + 18f, r.y + 184f, r.width - 36f, 66f), desc, desc.Length > 110 ? 12 : 14, Ui.Text, TextAnchor.UpperCenter, false, true);
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
            var now = StatSheet.Describe(me, me.Taken);
            List<StatLine> after = null;
            if (shopPreview >= 0)
                after = StatSheet.Describe(me, new List<int>(me.Taken) { shopPreview });
            else if (selectedCard >= 0 && me.Offer != null && selectedCard < me.Offer.Length)
            {
                var preview = new List<int>(me.Taken) { me.Offer[selectedCard] };
                after = StatSheet.Describe(me, preview, pickKey);
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
