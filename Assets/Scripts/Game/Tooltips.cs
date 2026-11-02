using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Hover tooltips for the Tab screens (Your build, Team): hovering an upgrade, weapon or ability shows what it does.
    /// While a screen draws, each hoverable thing calls one of the Tip... methods with its rect; the one under the mouse
    /// is remembered, highlighted, and drawn last (DrawTip), beside it, so it sits on top of everything.
    public partial class ArenaApp
    {
        class TipLine
        {
            public string Text;
            public int Size;
            public Color Color;
            public bool Bold;
        }

        Rect tipAnchor;
        string tipTitle;
        Color tipColor;
        readonly List<TipLine> tipLines = new List<TipLine>();
        bool tipOn;

        /// Call at the start of a screen that has tooltips.
        void TipClear()
        {
            tipOn = false;
            tipLines.Clear();
        }

        /// True (and highlights the row) if the mouse is over r; the caller then fills in the tip with TipStart/TipAdd.
        bool TipHover(Rect r)
        {
            if (!r.Contains(Event.current.mousePosition))
                return false;
            Ui.Fill(r, new Color(1f, 1f, 1f, 0.07f));
            return true;
        }

        void TipStart(Rect anchor, string title, Color color)
        {
            tipOn = true;
            tipAnchor = anchor;
            tipTitle = title;
            tipColor = color;
            tipLines.Clear();
        }

        void TipAdd(string text, int size, Color color, bool bold = false)
        {
            if (!string.IsNullOrEmpty(text))
                tipLines.Add(new TipLine { Text = text, Size = size, Color = color, Bold = bold });
        }

        /// An upgrade someone took: its rarity, how many they have, and what it does for their weapon.
        void TipUpgrade(Rect r, Player p, UpgradeDef u, int count)
        {
            if (!TipHover(r))
                return;
            var rc = ToColor(Upgrades.RarityColor(u.Rarity));
            TipStart(r, u.Name, Color.Lerp(rc, Color.white, 0.35f));
            string sub = Upgrades.RarityName(u.Rarity).ToUpperInvariant();
            if (count > 1)
                sub += "  ·  TAKEN ×" + count;
            if (u.MaxStacks < 99)
                sub += "  ·  STACKS TO " + u.MaxStacks;
            TipAdd(sub, 10, rc, true);
            TipAdd(Upgrades.DescFor(u, p.Weapon), 13, Ui.Text);
            if (u.Grants != AbilityId.None)
            {
                var a = Upgrades.Ability(u.Grants);
                TipAdd(a.Name + ": " + a.Desc, 12, Ui.Muted);
                TipAdd("Mastered (Lv 5): " + Game.Capstone(u.Grants), 11, new Color(0.85f, 0.75f, 0.48f));
            }
        }

        /// Someone's weapon: what it does, and what each extra projectile adds to it.
        void TipWeapon(Rect r, Player p)
        {
            if (!TipHover(r))
                return;
            TipStart(r, p.Weapon.Name, Color.white);
            TipAdd("WEAPON  ·  " + Defs.KitName(p.Kit).ToUpperInvariant() + " " + p.Def.Name.ToUpperInvariant(), 10, ToColor(p.Def.Color), true);
            TipAdd(p.Weapon.Desc, 13, Ui.Text);
            if (!string.IsNullOrEmpty(p.Weapon.ExtraDesc))
                TipAdd("Each extra projectile: " + p.Weapon.ExtraDesc, 11, Ui.Muted);
        }

        /// One of someone's abilities: what it does, their mastery, and what Lv 5 adds.
        void TipAbility(Rect r, Player p, int slot)
        {
            var id = p.Slots[slot].Id;
            if (id == AbilityId.None || !TipHover(r))
                return;
            var a = Upgrades.Ability(id);
            bool ult = slot == Upgrades.UltSlot;
            TipStart(r, a.Name, ult ? Ui.Gold : Color.white);
            int level = Game.MasteryLevel(p, id);
            TipAdd((ult ? "ULTIMATE · F" : "ABILITY · " + SlotKeys[slot]) + "  ·  " + (level >= Game.MasteryMax ? "MASTERED" : "LV " + level) + "  ·  " + Mathf.RoundToInt(a.Cooldown) + " S COOLDOWN",
                10, ult ? Ui.Gold : ToColor(p.Def.Color), true);
            TipAdd(a.Desc, 13, Ui.Text);
            TipAdd("Mastered (Lv 5): " + Game.Capstone(id), 11, new Color(0.85f, 0.75f, 0.48f));
        }

        /// The upgrades a card had no room for, by name.
        void TipMore(Rect r, List<string> names)
        {
            if (!TipHover(r))
                return;
            TipStart(r, names.Count + " more upgrades", Color.white);
            TipAdd(string.Join(",  ", names.ToArray()), 12, Ui.Text);
        }

        /// The remembered tip, beside what it's for (right of it if there's room, else left; kept on screen).
        void DrawTip()
        {
            if (!tipOn)
                return;
            const float pw = 300f, pad = 14f;
            float h = pad + 22f;
            foreach (var l in tipLines)
                h += Ui.TextHeight(l.Text, l.Size, pw - pad * 2f) + 6f;
            h += pad - 6f;
            float x = tipAnchor.xMax + 10f;
            if (x + pw > W - 8f)
                x = tipAnchor.x - 10f - pw;
            if (x < 8f)
                x = Mathf.Clamp(Event.current.mousePosition.x + 16f, 8f, W - 8f - pw);
            var pop = new Rect(x, Mathf.Clamp(tipAnchor.y - 6f, 8f, H - 8f - h), pw, h);
            Ui.Panel(pop, true);
            float y = pop.y + pad;
            Ui.Label(new Rect(pop.x + pad, y - 2f, pw - pad * 2f, 22f), tipTitle, 16, tipColor, TextAnchor.MiddleLeft, true);
            y += 24f;
            foreach (var l in tipLines)
            {
                float lh = Ui.TextHeight(l.Text, l.Size, pw - pad * 2f);
                Ui.Label(new Rect(pop.x + pad, y, pw - pad * 2f, lh), l.Text, l.Size, l.Color, TextAnchor.UpperLeft, l.Bold, true);
                y += lh + 6f;
            }
        }
    }
}
