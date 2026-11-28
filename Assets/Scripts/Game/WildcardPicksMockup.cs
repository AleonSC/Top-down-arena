using UnityEngine;

namespace Arena
{
    /// Two candidate redesigns of the Wildcard's gear picker (LobbyUi.DrawWildcardPicks), drawn with the real widgets for
    /// the owner to choose from: `wildLook` 1 = slot first (pick a slot, then any class's options for it), 2 = class first
    /// (the other roles' rows, with the ability keys' own E / 1 slot toggles). 0 keeps today's wheel. Preview: WILDCARD=1 WILDLOOK=1|2.
    public partial class ArenaApp
    {
        int wildLook;     // 0 today's wheel, 1 slot first, 2 class first (mockups)
        int wildSlot;     // look 1: the slot being edited (0 weapon, 1 1st ability, 2 2nd ability, 3 ultimate)

        /// Fills x, y .. yEnd (the Gear and Ultimate rows' space) with the picked look.
        void DrawWildMock(LobbyEntry me, Session tgt, bool self, float x, float y, float w, float yEnd)
        {
            if (wildLook == 1)
                DrawWildSlotFirst(me, tgt, self, x, y, w, yEnd);
            else
                DrawWildClassFirst(me, tgt, self, x, y, w, yEnd);
        }

        // ---- shared ----

        int WildKitOf(LobbyEntry me, int slot, int second) =>
            slot == 0 ? (int)Defs.WildWeaponKit(me.Weapon) : slot == 1 ? me.Ability / 3 : slot == 2 ? second / 3 : (int)me.UltKit;

        string WildSlotSub(int slot) =>
            slot == 0 ? "WEAPON"
            : slot == 1 ? "1ST · " + Keybinds.KeyName(Keybinds.Get(KeyAction.Ability1)).ToUpperInvariant()
            : slot == 2 ? "2ND · " + Keybinds.KeyName(Keybinds.Get(KeyAction.Ability2)).ToUpperInvariant()
            : "ULT · " + Keybinds.KeyName(Keybinds.Get(KeyAction.Ultimate)).ToUpperInvariant();

        void WildPickWeapon(LobbyEntry me, Session tgt, bool self, int idx, int second)
        {
            if (me.Weapon != idx)
                tgt.RequestWildcard(idx, me.Ability, second);
            if (self)
                weaponChoice = idx;
            detailSel = 0;
        }

        /// Put ability idx in slot 0 (1st) or 1 (2nd); the other slot's ability swaps over if it was the same one.
        void WildPickAbility(LobbyEntry me, Session tgt, bool self, int idx, int second, int slot)
        {
            int a1 = me.Ability, a2 = second;
            if (slot == 0)
            {
                if (a2 == idx)
                    a2 = a1;
                a1 = idx;
            }
            else
            {
                if (a1 == idx)
                    a1 = a2;
                a2 = idx;
            }
            if (a1 != me.Ability || a2 != second)
                tgt.RequestWildcard(me.Weapon, a1, a2);
            if (self)
            {
                abilityChoice = a1;
                wildSecond = a2;
            }
            detailSel = 1;
            detailSecond = slot == 1;
        }

        void WildPickUlt(LobbyEntry me, Session tgt, bool self, int pick)
        {
            if (me.Ult != pick)
                tgt.RequestUlt(pick);
            if (self)
                ultChoice = pick;
            detailSel = 2;
        }

        // ---- look 1: slot first ----

        /// Four slot keys (weapon, 1st, 2nd, ultimate), each with its class's icon; the lit one is being edited. Under them,
        /// every class's options for that slot: one row a class, its icon and name on the left.
        void DrawWildSlotFirst(LobbyEntry me, Session tgt, bool self, float x, float y, float w, float yEnd)
        {
            bool calm = fxReduced;
            var ev = Event.current;
            var wild = (Defs.Get(Role.Wildcard).Color).ToColor();
            int second = Defs.WildSecond(me.Wild);
            LobbyKit.Heading(x, y, w, "Loadout", "Click a slot, then pick from any class", calm, 15);
            y += CarKeyY;

            // The slots.
            const float slotH = 38f, gap = 6f;
            float sw = (w - 3f * gap) / 4f;
            int ns = 12;  // one size for the four names
            for (int i = 0; i < 4; i++)
                ns = Mathf.Min(ns, LobbyKit.FitSize(ChipName(me, i, second).ToUpperInvariant(), 12, 8, sw - 33f));
            for (int i = 0; i < 4; i++)
            {
                var b = new Rect(x + i * (sw + gap), y, sw, slotH);
                bool on = wildSlot == i;
                if (DkKey(b, "", on, calm, accent: on ? wild : (Color?)null))
                {
                    wildSlot = i;
                    detailSel = i == 0 ? 0 : i == 3 ? 2 : 1;
                    detailSecond = i == 2;
                }
                bool hover = GUI.enabled && b.Contains(ev.mousePosition);
                Color tc = CrtKit.SoftText(on ? wild : CrtStyle.KeyColor(null, false), on || hover);
                const float isz = 16f;
                Ui.Icon(new Rect(b.x + 6f, b.center.y - isz / 2f, isz, isz), PixelArt.Get(Classes.Get((Kit)WildKitOf(me, i, second)).Icon), Color.white);
                float tx = b.x + 6f + isz + 6f, tw = b.xMax - 5f - tx;
                string name = ChipName(me, i, second).ToUpperInvariant();
                CrtUi.Text(new Rect(tx, b.center.y - 16f, tw, 18f), LobbyKit.Truncate(name, ns, tw), ns, tc, TextAnchor.MiddleLeft, true, calm);
                Ui.Label(new Rect(tx, b.center.y + 1f, tw, 15f), WildSlotSub(i), 9, tc.WithAlpha(0.75f), TextAnchor.MiddleLeft, false);
                if (on)  // a notch under the lit slot, pointing at the list it fills
                {
                    float nx = b.center.x;
                    for (int k = 0; k < 5; k++)
                        Ui.Fill(new Rect(nx - 5f + k, b.yMax + 2f + k, 10f - 2f * k, 1f), wild);
                }
            }
            y += slotH + 9f;

            // The list: one row a class.
            int classes = Defs.KitCount;
            float rowGap = 3f, rh = Mathf.Min(34f, (yEnd - y - (classes - 1) * rowGap) / classes);
            const float labW = 96f;
            int cols = wildSlot == 0 || wildSlot == 3 ? 4 : 3;
            float kx = x + labW, kw = (w - labW - (cols - 1) * 4f) / cols;
            for (int k = 0; k < classes; k++)
            {
                float ry = y + k * (rh + rowGap);
                bool mine = WildKitOf(me, wildSlot, second) == k;
                Ui.Fill(new Rect(x, ry, w, rh), (mine ? wild : CrtStyle.Ink).WithAlpha(mine ? 0.07f : 0.025f));
                const float isz = 16f;
                Ui.Icon(new Rect(x + 6f, ry + (rh - isz) / 2f, isz, isz), PixelArt.Get(Classes.Get((Kit)k).Icon), mine ? Color.white : new Color(0.75f, 0.78f, 0.88f, 1f));
                Ui.Label(new Rect(x + 28f, ry, labW - 30f, rh), LobbyKit.Truncate(Defs.KitName((Kit)k).ToUpperInvariant(), 9, labW - 32f), 9, (mine ? wild : CrtStyle.Dim).WithAlpha(mine ? 1f : 0.9f), TextAnchor.MiddleLeft, true);
                for (int c = 0; c < cols; c++)
                {
                    var b = new Rect(kx + c * (kw + 4f), ry, kw, rh);
                    if (!KitOpen((Kit)k))
                    {
                        LockedSlot(b, EngineerLockText, true);
                        continue;
                    }
                    if (wildSlot == 0)
                    {
                        if (c >= Defs.WeaponCount(Role.Dps, (Kit)k) || Defs.WeaponGap(Role.Dps, (Kit)k, c))
                        {
                            LockedSlot(b, null, true);
                            continue;
                        }
                        int idx = Defs.WildWeaponPick((Kit)k, c);
                        if (DkKey(b, Defs.Weapon(Role.Wildcard, Kit.Soldier, idx).Name.ToUpperInvariant(), me.Weapon == idx, calm, size: 11, minSize: 7, accent: wild))
                            WildPickWeapon(me, tgt, self, idx, second);
                    }
                    else if (wildSlot == 3)
                    {
                        var id = c == 0 ? Upgrades.Ultimate(Role.Wildcard, (Kit)k) : Upgrades.AltUltimate(Role.Wildcard, (Kit)k, c);
                        if (id == AbilityId.None)
                        {
                            LockedSlot(b, null, true);
                            continue;
                        }
                        int pick = Upgrades.WildUlt((Kit)k, c);
                        if (DkKey(b, Upgrades.Ability(id).Name.ToUpperInvariant(), me.Ult == pick, calm, size: 11, minSize: 7, accent: wild))
                            WildPickUlt(me, tgt, self, pick);
                    }
                    else
                    {
                        if (Upgrades.AbilityGap(Role.Dps, (Kit)k, c))
                        {
                            LockedSlot(b, null, true);
                            continue;
                        }
                        int idx = k * 3 + c;
                        int inSlot = me.Ability == idx ? 1 : second == idx ? 2 : 0;
                        if (DkKey(b, Upgrades.Ability(Upgrades.FirstAbility(Role.Wildcard, Kit.Soldier, idx)).Name.ToUpperInvariant(), inSlot == wildSlot, calm, size: 11, minSize: 7, accent: wild))
                            WildPickAbility(me, tgt, self, idx, second, wildSlot - 1);
                        if (inSlot > 0 && inSlot != wildSlot)  // the other ability slot's pick: tagged, a click swaps the two
                            SlotBadge(new Rect(b.xMax - 13f, b.y + 2f, 11f, 11f), inSlot, wild);
                    }
                }
            }
        }

        // ---- look 2: class first ----

        /// The other roles' rows: Class (six keys, tagged with the slots whose gear comes from each), then that class's
        /// Weapon, Abilities and Ultimate keys. Each ability key ends in two small slot keys named by their keybinds
        /// (E, 1): click one to put the ability there; a click on the name fills the 1st.
        void DrawWildClassFirst(LobbyEntry me, Session tgt, bool self, float x, float y, float w, float yEnd)
        {
            bool calm = fxReduced;
            var ev = Event.current;
            var wild = (Defs.Get(Role.Wildcard).Color).ToColor();
            int second = Defs.WildSecond(me.Wild);
            if (wildBrowseFor != me.Id)
            {
                wildBrowseFor = me.Id;
                wildBrowse = (int)Defs.WildWeaponKit(me.Weapon);
                wildBrowsePos = wildBrowse;
            }
            int bk = WildBrowseKit;
            float step = (yEnd - y) / 4f;  // four even rows: Class, Weapon, Abilities, Ultimate
            float keyY = 26f, keyH = Mathf.Min(CarKeyH, step - keyY - 6f);

            // Class: six keys, each tagged under its name with the slots it fills (W, E, 1, Q).
            LobbyKit.Heading(x, y, w, "Class", "Browse · tags show where your gear comes from", calm, 15);
            int n = Defs.KitCount;
            float cw = (w - (n - 1) * 4f) / n;
            string[] tags = { "W", Keybinds.KeyName(Keybinds.Get(KeyAction.Ability1)).ToUpperInvariant(), Keybinds.KeyName(Keybinds.Get(KeyAction.Ability2)).ToUpperInvariant(), Keybinds.KeyName(Keybinds.Get(KeyAction.Ultimate)).ToUpperInvariant() };
            for (int k = 0; k < n; k++)
            {
                var b = new Rect(x + k * (cw + 4f), y + keyY, cw, keyH);
                if (!KitOpen((Kit)k))
                {
                    LockedSlot(b, EngineerLockText, true);
                    continue;
                }
                if (DkKey(b, "", k == bk, calm, accent: wild))
                    TurnWheelTo(k);
                const float isz = 16f;
                Ui.Icon(new Rect(b.center.x - isz / 2f, b.y + 3f, isz, isz), PixelArt.Get(Classes.Get((Kit)k).Icon), Color.white);
                // under the icon: the slot tags (W, E, 1, Q) whose gear comes from this class, else its short name
                int used = 0;
                for (int s = 0; s < 4; s++)
                    if (WildKitOf(me, s, second) == k)
                        used++;
                float tx = b.center.x - (used * 12f - 1f) / 2f, ty = b.yMax - 14f;
                if (used == 0)
                {
                    string nm = Defs.KitName((Kit)k).ToUpperInvariant();
                    Ui.Label(new Rect(b.x, ty - 1f, b.width, 13f), LobbyKit.Truncate(nm, 8, b.width - 4f), 8,
                        CrtKit.SoftText(k == bk ? wild : CrtStyle.KeyColor(null, false), k == bk).WithAlpha(0.8f), TextAnchor.MiddleCenter, true);
                }
                for (int s = 0, u = 0; s < 4; s++)
                    if (WildKitOf(me, s, second) == k)
                    {
                        var tr = new Rect(tx + u * 12f, ty, 11f, 11f);
                        Ui.Fill(tr, wild);
                        Ui.Label(tr, tags[s], 8, new Color(0.05f, 0.02f, 0.1f), TextAnchor.MiddleCenter, true);
                        u++;
                    }
            }
            y += step;

            // Weapon.
            LobbyKit.Heading(x, y, w, "Weapon", Defs.KitName((Kit)bk), calm, 15);
            int wn = Defs.WeaponCount(Role.Dps, (Kit)bk);
            float ww = (w - 3f * 4f) / 4f;
            for (int c = 0; c < 4; c++)
            {
                var b = new Rect(x + c * (ww + 4f), y + keyY, ww, keyH);
                if (c >= wn || Defs.WeaponGap(Role.Dps, (Kit)bk, c))
                {
                    LockedSlot(b, null, true);
                    continue;
                }
                int idx = Defs.WildWeaponPick((Kit)bk, c);
                if (DkKey(b, Defs.Weapon(Role.Wildcard, Kit.Soldier, idx).Name.ToUpperInvariant(), me.Weapon == idx, calm, size: 13, minSize: 8))
                    WildPickWeapon(me, tgt, self, idx, second);
            }
            y += step;

            // Abilities: name key + two slot keys.
            LobbyKit.Heading(x, y, w, "Abilities", "Pick two · click the key it goes on", calm, 15);
            float aw = (w - 2f * 6f) / 3f;
            for (int c = 0; c < 3; c++)
            {
                var b = new Rect(x + c * (aw + 6f), y + keyY, aw, keyH);
                if (Upgrades.AbilityGap(Role.Dps, (Kit)bk, c))
                {
                    LockedSlot(b, null, true);
                    continue;
                }
                int idx = bk * 3 + c;
                int inSlot = me.Ability == idx ? 1 : second == idx ? 2 : 0;
                const float sk = 22f;
                var nb = new Rect(b.x, b.y, b.width - 2f * (sk + 2f), b.height);
                if (DkKey(nb, Upgrades.Ability(Upgrades.FirstAbility(Role.Wildcard, Kit.Soldier, idx)).Name.ToUpperInvariant(), inSlot > 0, calm, size: 12, minSize: 7))
                    WildPickAbility(me, tgt, self, idx, second, inSlot == 2 ? 1 : 0);
                for (int s = 0; s < 2; s++)
                {
                    var sb = new Rect(nb.xMax + 2f + s * (sk + 2f), b.y, sk, b.height);
                    if (DkKey(sb, tags[1 + s], inSlot == s + 1, calm, size: 11, minSize: 8, accent: wild))
                        WildPickAbility(me, tgt, self, idx, second, s);
                }
            }
            y += step;

            // Ultimate.
            LobbyKit.Heading(x, y, w, "Ultimate", Defs.KitName((Kit)bk), calm, 15);
            for (int c = 0; c < Upgrades.UltKeys; c++)
            {
                var b = new Rect(x + c * (ww + 4f), y + keyY, ww, keyH);
                var id = c == 0 ? Upgrades.Ultimate(Role.Wildcard, (Kit)bk) : Upgrades.AltUltimate(Role.Wildcard, (Kit)bk, c);
                if (id == AbilityId.None)
                {
                    LockedSlot(b, null, true);
                    continue;
                }
                int pick = Upgrades.WildUlt((Kit)bk, c);
                if (DkKey(b, Upgrades.Ability(id).Name.ToUpperInvariant(), me.Ult == pick, calm, size: 13, minSize: 8))
                    WildPickUlt(me, tgt, self, pick);
            }
        }
    }
}
