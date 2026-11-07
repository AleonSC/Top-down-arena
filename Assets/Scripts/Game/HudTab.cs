using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Arena
{
    /// The Tab screen's own parts: YOUR STATS down the left and the Your build page (your loadout: the weapon, the abilities with their
    /// keys, every upgrade picked), as hologram panels (TabHolo.cs). The screen around them, Run so far and Team: RunStatsUi.cs.
    public partial class ArenaApp
    {
        // --- Tab: your stats and loadout ---------------------------------------

        /// Your build. Only drawn on this player's screen; nothing is sent to the other player and the game doesn't pause.
        /// `dim`: draw the dark layer and the beam too (the Run stats screen draws them itself).
        void DrawTabMenu(Player me, bool dim = true)
        {
            TipClear();  // hovering the weapon or an upgrade shows what it does (Tooltips.cs)
            if (dim)
            {
                RoundBackdrop(TabDark);
                DrawHoloBeam();
            }
            DrawHoloStats(me);
            float x = HoloPageX, w = HoloPageR - HoloPageX;
            var head = new Rect(x, HoloTop, w, HoloHeadH);
            var rc = ToColor(me.Def.Color);
            HoloPanel(head, holoPageAt, () => HoloTitle(head.x + 18f, head.y + 14f, "YOUR LOADOUT",
                (Defs.KitName(me.Kit) + " " + me.Def.Name + " · Level " + me.Level + " · " + me.Rerolls + " reroll" + (me.Rerolls == 1 ? "" : "s") + " left").ToUpperInvariant(),
                Color.Lerp(rc, Color.white, 0.35f)));
            float top = HoloTop + HoloHeadH + HoloGap;
            var abilBox = new Rect(x, top, 360f, HoloBottom - top);
            var upBox = new Rect(abilBox.xMax + HoloGap, top, HoloPageR - abilBox.xMax - HoloGap, HoloBottom - top);
            HoloPanel(abilBox, holoPageAt, () => DrawTabAbilities(me, abilBox));
            HoloPanel(upBox, holoPageAt, () => DrawTabUpgrades(me, upBox));
            DrawAbilityHover(abilBox);
            DrawTip();
        }

        /// YOUR STATS: a panel down the left, with your weapon and ability, then the stats grouped so they're quick to scan.
        void DrawHoloStats(Player me)
        {
            var r = new Rect(HoloStatsX, HoloTop, HoloStatsW, HoloBottom - HoloTop);
            HoloPanel(r, holoOpenAt, () =>
            {
                var rc = ToColor(me.Def.Color);
                HoloTitle(r.x + 16f, r.y + 12f, "YOUR STATS", null, rc, 30);
                Ui.Label(new Rect(r.x + 16f, r.y + 46f, r.width - 32f, 16f), LobbyKit.Truncate(me.Weapon.Name + " · " + Upgrades.Ability(me.RoleAbility).Name, 12, r.width - 32f), 12,
                    Color.Lerp(rc, Color.white, 0.3f), TextAnchor.MiddleLeft, true);
                DrawTabStats(me, new Rect(r.x + 10f, r.y + 70f, r.width - 20f, r.height - 80f));
            });
        }

        /// The stats, in three groups.
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
                new KeyValuePair<string, string[,]>("Offense", new string[,] { { "Damage", get("Damage") }, { "Fire rate", get("Fire rate") }, { "Extra projectiles", get("Extra projectiles") }, { "Pierce", get("Pierce") } }),
                new KeyValuePair<string, string[,]>("Defense", new string[,] { { "Health", Mathf.CeilToInt(me.Hp) + " / " + Mathf.CeilToInt(me.MaxHp) }, { "Damage taken", get("Damage taken") }, { "Regeneration", get("Regeneration") }, { "Parry durability", get("Parry durability") } }),
                new KeyValuePair<string, string[,]>("Utility", new string[,] { { "Move speed", get("Move speed") }, { "Ability cooldowns", get("Ability cooldowns") }, { "Space charges", charges } }),
            };
            float y = box.y;
            foreach (var g in groups)
            {
                LobbyKit.Heading(box.x + 6f, y, box.width - 12f, g.Key, null, HoloCalm);
                y += 34f;
                HoloRows(box.x, y, box.width, g.Value);
                y += g.Value.GetLength(0) * 26f + 12f;
            }
        }

        /// The hovered Tab ability tile's full text, beside the abilities panel (drawn after the upgrades so it's on top).
        void DrawAbilityHover(Rect box)
        {
            if (hoverAbility == null)
                return;
            // The hovered ability in full: what it does and what Lv 5 adds.
            const float pw = 320f;
            string cap = "Mastered (Lv 5): " + Game.Capstone(hoverAbility.Id);
            float dh = Ui.TextHeight(hoverAbility.Desc, 12, pw - 28f), ch = Ui.TextHeight(cap, 11, pw - 42f);
            var pop = new Rect(box.xMax + 10f, hoverTile.y, pw, 44f + dh + 6f + ch + 12f);
            pop.y = Mathf.Min(pop.y, H - 12f - pop.height);
            var ink = CrtUi.Ink;
            Ui.Fill(pop, new Color(0.02f, 0.05f, 0.12f, 0.97f));
            CrtKit.Frame(pop, 1f, ink);
            Ui.Glow(pop, ink, 0.12f);
            Ui.Label(new Rect(pop.x + 14f, pop.y + 10f, pw - 28f, 24f), hoverAbility.Name, 16, Color.white, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(pop.x + 14f, pop.y + 40f, pw - 28f, dh), hoverAbility.Desc, 12, HoloBody, TextAnchor.UpperLeft, false, true);
            Ui.Icon(new Rect(pop.x + 14f, pop.y + 47f + dh, 10f, 10f), PixelArt.Get("ui_star"), CrtStyle.Gold);
            Ui.Label(new Rect(pop.x + 28f, pop.y + 46f + dh, pw - 42f, ch), cap, 11, CrtStyle.Gold, TextAnchor.UpperLeft, false, true);
        }

        AbilityDef hoverAbility;
        Rect hoverTile;

        /// The weapon, then each ability on its own tile: key, mastery, charges or cooldown, name, what it does, and (bonus keys)
        /// the keys it can move to.
        void DrawTabAbilities(Player me, Rect box)
        {
            hoverAbility = null;
            bool calm = HoloCalm;
            var ink = CrtUi.Ink;
            float x = box.x + 16f, w = box.width - 32f, y = box.y + 8f;
            LobbyKit.Heading(x, y, w, "Weapon", null, calm);
            y += 34f;
            float wdh = Mathf.Min(30f, Ui.TextHeight(me.Weapon.Desc, 11, w));  // at most two lines (hover for all of it)
            TipWeapon(new Rect(x - 4f, y - 2f, w + 8f, 24f + wdh), me);
            Ui.Label(new Rect(x, y, w, 22f), me.Weapon.Name, 17, Color.white, TextAnchor.MiddleLeft, true);
            GUI.BeginGroup(new Rect(x, y + 22f, w, wdh));
            Ui.Label(new Rect(0f, 0f, w, Ui.TextHeight(me.Weapon.Desc, 11, w)), me.Weapon.Desc, 11, HoloDesc, TextAnchor.UpperLeft, false, true);
            GUI.EndGroup();
            y += 30f + wdh;
            LobbyKit.Heading(x, y, w, "Abilities", "click a key to move it", calm);
            y += 36f;
            int filled = 0;
            foreach (var sl in me.Slots)
                if (sl.Id != AbilityId.None)
                    filled++;
            float tileH = Mathf.Clamp((box.yMax - 10f - y) / Mathf.Max(1, filled) - 6f, 58f, 96f);
            for (int i = 0; i < me.Slots.Length; i++)
            {
                var slot = me.Slots[i];
                if (slot.Id == AbilityId.None)
                    continue;
                var a = Upgrades.Ability(slot.Id);
                bool ult = i == Upgrades.UltSlot;
                var tile = new Rect(x, y, w, tileH);
                Ui.Fill(tile, new Color(ink.r, ink.g, ink.b, 0.05f));
                CrtKit.Frame(tile, 1f, new Color(ink.r, ink.g, ink.b, 0.4f));
                string keyName = ult ? "ULT · " + SlotKeys[4] : SlotKeys[i];
                DrawCrtCap(new Rect(tile.x + 8f, tile.y + 7f, 0f, 16f), keyName, ult ? CrtStyle.Gold : ink);
                float capW = 12f + keyName.Length * 7f;  // as DrawCrtCap sizes it
                int level = Game.MasteryLevel(me, slot.Id);
                float xp;
                me.MasteryXp.TryGetValue(slot.Id, out xp);
                DrawMasteryPips(new Rect(tile.x + 16f + capW, tile.y + 12f, 0f, 5f), level, Game.MasteryProgress(xp));
                bool was = Ui.CrtFont;
                Ui.CrtFont = true;
                string state = slot.Charges > 0 ? "READY" : Mathf.CeilToInt(slot.Timer) + "S";
                if (slot.MaxCharges > 1)
                    state = slot.Charges + "/" + slot.MaxCharges + " CHARGES" + (slot.Charges < slot.MaxCharges ? " · " + Mathf.CeilToInt(slot.Timer) + "S" : "");
                string lvText = level >= Game.MasteryMax ? "MASTERED" : "LV " + level;
                float lvX = tile.x + 16f + capW + 68f;
                if (lvX + Ui.TextWidth(lvText, 15, false) + 8f < tile.xMax - 8f - Ui.TextWidth(state, 17, false))  // (only with room: the pips show the level too)
                    CrtUi.Text(new Rect(lvX, tile.y + 5f, 90f, 20f), lvText, 15, level >= Game.MasteryMax ? CrtStyle.Gold : HoloDim, TextAnchor.MiddleLeft, false, calm);
                CrtUi.Text(new Rect(tile.x + 8f, tile.y + 5f, tile.width - 16f, 20f), state, 17, slot.Charges > 0 ? Ui.Good : HoloDim, TextAnchor.MiddleRight, false, calm);
                Ui.CrtFont = was;
                bool bonus = i >= 1 && i <= Upgrades.BonusSlots;
                Ui.Label(new Rect(tile.x + 8f, tile.y + 26f, tile.width - 16f, 20f), a.Name, 15, Color.white, TextAnchor.MiddleLeft, true);
                float dw = tile.width - 16f - (bonus ? 126f : 0f);
                var descR = new Rect(tile.x + 8f, tile.y + 45f, dw, tile.height - 48f);
                if (descR.height >= 26f)
                    DrawDescAndMastery(descR, a.Desc, slot.Id, 11, HoloDesc);  // what it does, then what Lv 5 adds (LobbyUi.cs)
                else
                    Ui.Label(new Rect(descR.x, descR.y, dw, 14f), LobbyKit.Truncate(a.Desc, 11, dw, false), 11, HoloDesc, TextAnchor.MiddleLeft, false);
                if (tile.Contains(Event.current.mousePosition))
                {
                    hoverAbility = a;  // the full text, beside the panel
                    hoverTile = tile;
                }
                if (bonus)  // move it to another key (swaps with whatever is there)
                {
                    bool w2 = Ui.CrtFont;
                    Ui.CrtFont = true;
                    CrtUi.Text(new Rect(tile.xMax - 132f, tile.y + 28f, 44f, 16f), "MOVE TO", 13, HoloDim, TextAnchor.MiddleRight, false, calm);
                    Ui.CrtFont = w2;
                    float kx = tile.xMax - 84f;
                    for (int k = 1; k <= Upgrades.BonusSlots; k++)
                    {
                        if (k == i)
                            continue;
                        float kw = k == 3 ? 44f : 32f;
                        var kr = new Rect(kx, tile.y + 26f, kw, 22f);
                        if (LobbyKit.Key(kr, SlotKeys[k], false, calm, size: 13, minSize: 9))
                            game.SetAbilityKey(slot.Id, k);
                        kx += kw + 4f;
                    }
                }
                y += tileH + 6f;
            }
        }

        /// Every upgrade picked, grouped by rarity (rarest first) in two columns.
        void DrawTabUpgrades(Player me, Rect box)
        {
            bool calm = HoloCalm;
            var counts = new Dictionary<int, int>();
            foreach (int id in me.Taken)
                counts[id] = counts.ContainsKey(id) ? counts[id] + 1 : 1;
            // A flat list of headers (id < 0, rarity in -id - 1) and upgrades, rarest first.
            var items = new List<int>();
            for (int rr = (int)Rarity.Milestone; rr >= 0; rr--)
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
            float x = box.x + 16f, w = box.width - 32f;
            LobbyKit.Heading(x, box.y + 8f, w, "Upgrades", me.Taken.Count + " picks", calm);
            if (items.Count == 0)
            {
                Ui.Label(new Rect(x, box.y + 48f, w, 22f), "None yet: level up to pick some.", 13, HoloDim);
                return;
            }
            const float rowH = 23f, colGap = 20f;
            float listTop = box.y + 46f, colW = (w - colGap) / 2f;
            int perCol = Mathf.FloorToInt((box.yMax - 12f - listTop) / rowH);
            int maxScroll = Math.Max(0, items.Count - perCol * 2);
            if (Event.current.type == EventType.ScrollWheel)
            {
                tabScroll += Event.current.delta.y > 0f ? 2 : -2;
                Event.current.Use();
            }
            tabScroll = Mathf.Clamp(tabScroll, 0, maxScroll);
            if (maxScroll > 0)
            {
                bool was = Ui.CrtFont;
                Ui.CrtFont = true;
                CrtUi.Text(new Rect(x, box.yMax - 22f, w, 16f), "SCROLL FOR MORE", 15, HoloDim, TextAnchor.MiddleRight, false, calm);
                Ui.CrtFont = was;
            }
            for (int k = 0; k < perCol * 2 && tabScroll + k < items.Count; k++)
            {
                int item = items[tabScroll + k];
                float cx = x + (k / perCol) * (colW + colGap), cy = listTop + (k % perCol) * rowH;
                if (item < 0)
                {
                    var rarity = (Rarity)(-item - 1);
                    Color rc = ToColor(Upgrades.RarityColor(rarity));
                    Ui.Fill(new Rect(cx, cy + 7f, 3f, 11f), rc);
                    bool was = Ui.CrtFont;
                    Ui.CrtFont = true;
                    CrtUi.Text(new Rect(cx + 9f, cy + 2f, colW - 9f, 20f), Upgrades.RarityName(rarity).ToUpperInvariant(), 16, rc, TextAnchor.MiddleLeft, false, calm);
                    Ui.CrtFont = was;
                    Ui.Fill(new Rect(cx, cy + rowH - 1f, colW, 1f), new Color(rc.r, rc.g, rc.b, 0.3f));
                    continue;
                }
                var u = Upgrades.All[item];
                TipUpgrade(new Rect(cx - 2f, cy, colW + 4f, rowH), me, u, counts[item]);  // hover: what it does (Tooltips.cs)
                Ui.Icon(new Rect(cx + 2f, cy + 3f, 18f, 18f), PixelArt.ForUpgrade(u.Name));
                Ui.Label(new Rect(cx + 26f, cy, colW - 60f, rowH), u.Name, 13, HoloBody, TextAnchor.MiddleLeft);
                if (counts[item] > 1)
                    CrtUi.Title(new Rect(cx, cy, colW - 4f, rowH), "×" + counts[item], 17, CrtStyle.Gold, TextAnchor.MiddleRight, calm);
            }
        }
    }
}
