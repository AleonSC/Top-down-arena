using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Arena
{
    /// The Tab menu: its stats, abilities and upgrades pages, and the ability hover.
    public partial class ArenaApp
    {
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
    }
}
