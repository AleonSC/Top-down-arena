using UnityEngine;

namespace Arena
{
    /// A candidate redesign of the Wildcard's gear picker (LobbyUi.DrawWildcardPicks), drawn with the real widgets for the
    /// owner to judge: `wildLook` 1 = four slot cards and one scrolling row of options for the open slot. 0 keeps today's
    /// wheel. Preview: WILDCARD=1 WILDLOOK=1 [WILDSLOT=0-3].
    public partial class ArenaApp
    {
        int wildLook;     // 0 today's wheel, 1 the slots mockup
        int wildSlot;     // the slots mockup: the slot open (0 weapon, 1 1st ability, 2 2nd ability, 3 ultimate)

        /// Fills x, y .. yEnd (the Gear and Ultimate rows' space) with the picked look.
        void DrawWildMock(LobbyEntry me, Session tgt, bool self, float x, float y, float w, float yEnd)
        {
            DrawWildSlots(me, tgt, self, x, y, w, yEnd);
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

        // ---- look 3: slots + one row ----

        /// Four slot cards (what you have: class icon, name, key); click one to open it. Under them one scrolling row of every
        /// class's options for that slot (class icon over the name), so more gear only makes the row longer.
        void DrawWildSlots(LobbyEntry me, Session tgt, bool self, float x, float y, float w, float yEnd)
        {
            bool calm = fxReduced;
            var ev = Event.current;
            var wild = (Defs.Get(Role.Wildcard).Color).ToColor();
            int second = Defs.WildSecond(me.Wild);
            LobbyKit.Heading(x, y, w, "Gear", null, calm, 15);
            y += CarKeyY;

            // The four slots.
            const float gap = 6f;
            float rowH = 62f, cardH = yEnd - y - rowH - 16f;
            float sw = (w - 3f * gap) / 4f;
            int ns = 13;
            for (int i = 0; i < 4; i++)
                ns = Mathf.Min(ns, LobbyKit.FitSize(ChipName(me, i, second).ToUpperInvariant(), 13, 8, sw - 12f));
            for (int i = 0; i < 4; i++)
            {
                var b = new Rect(x + i * (sw + gap), y, sw, cardH);
                bool on = wildSlot == i;
                if (DkKey(b, "", on, calm, accent: wild))
                {
                    wildSlot = i;
                    detailSel = i == 0 ? 0 : i == 3 ? 2 : 1;
                    detailSecond = i == 2;
                }
                bool hover = GUI.enabled && b.Contains(ev.mousePosition);
                Color tc = CrtKit.SoftText(on ? wild : CrtStyle.KeyColor(null, false), on || hover);
                string key = i == 0 ? "" : WildSlotSub(i).Substring(WildSlotSub(i).IndexOf('·') + 2);
                Ui.Label(new Rect(b.x + 7f, b.y + 5f, b.width - 14f, 14f), i == 0 ? "WEAPON" : i == 3 ? "ULTIMATE" : "ABILITY", 9, tc.WithAlpha(0.7f), TextAnchor.MiddleLeft, true);
                if (key.Length > 0)  // its keybind, top right
                {
                    var kr = new Rect(b.xMax - 20f, b.y + 5f, 14f, 14f);
                    CrtKit.Frame(kr, 1f, tc.WithAlpha(0.6f));
                    Ui.Label(kr, key, 9, tc, TextAnchor.MiddleCenter, true);
                }
                float isz = Mathf.Min(48f, cardH - 60f);
                Ui.Icon(new Rect(b.center.x - isz / 2f, b.y + (cardH - isz) / 2f - 4f, isz, isz), PixelArt.Get(Classes.Get((Kit)WildKitOf(me, i, second)).Icon), Color.white);
                CrtUi.Text(new Rect(b.x + 4f, b.yMax - 24f, b.width - 8f, 18f), LobbyKit.Truncate(ChipName(me, i, second).ToUpperInvariant(), ns, b.width - 8f), ns, tc, TextAnchor.MiddleCenter, true, calm);
                if (on)  // a notch under the open slot, pointing at the row
                {
                    for (int k = 0; k < 5; k++)
                        Ui.Fill(new Rect(b.center.x - 5f + k, b.yMax + 3f + k, 10f - 2f * k, 1f), wild);
                }
            }
            y += cardH + 16f;

            // The row: every class's options for the open slot, in class order.
            var kits = new System.Collections.Generic.List<int>();
            var vals = new System.Collections.Generic.List<int>();
            for (int k = 0; k < Defs.KitCount; k++)
            {
                int n = wildSlot == 0 ? Defs.WeaponCount(Role.Dps, (Kit)k) : wildSlot == 3 ? Upgrades.UltKeys : 3;
                for (int c = 0; c < n; c++)
                {
                    bool gapped = wildSlot == 0 ? Defs.WeaponGap(Role.Dps, (Kit)k, c)
                        : wildSlot == 3 ? (c == 0 ? Upgrades.Ultimate(Role.Wildcard, (Kit)k) : Upgrades.AltUltimate(Role.Wildcard, (Kit)k, c)) == AbilityId.None
                        : Upgrades.AbilityGap(Role.Dps, (Kit)k, c);
                    if (gapped)
                        continue;
                    kits.Add(k);
                    vals.Add(wildSlot == 0 ? Defs.WildWeaponPick((Kit)k, c) : wildSlot == 3 ? Upgrades.WildUlt((Kit)k, c) : k * 3 + c);
                }
            }
            int cur = wildSlot == 0 ? me.Weapon : wildSlot == 1 ? me.Ability : wildSlot == 2 ? second : me.Ult;
            int sel = vals.IndexOf(cur);
            BeginCarousel(CarWeapon, new Rect(x, y, w, rowH), vals.Count, KeysInView, sel, out int from, out int to);
            for (int j = from; j <= to; j++)
            {
                var b = CarItem(j);
                int k = kits[j], v = vals[j];
                if (!KitOpen((Kit)k))
                {
                    LockedSlot(b, EngineerLockText, true);
                    continue;
                }
                string name = wildSlot == 0 ? Defs.Weapon(Role.Wildcard, Kit.Soldier, v).Name
                    : wildSlot == 3 ? Upgrades.Ability(Upgrades.Ultimate(Role.Wildcard, (Kit)k, v)).Name
                    : Upgrades.Ability(Upgrades.FirstAbility(Role.Wildcard, Kit.Soldier, v)).Name;
                if (DkKey(b, name.ToUpperInvariant(), v == cur, calm, PixelArt.Get(Classes.Get((Kit)k).Icon), stack: true, colorIcon: true, size: 12, minSize: 8))
                {
                    if (wildSlot == 0)
                        WildPickWeapon(me, tgt, self, v, second);
                    else if (wildSlot == 3)
                        WildPickUlt(me, tgt, self, v);
                    else
                        WildPickAbility(me, tgt, self, v, second, wildSlot - 1);
                }
            }
            EndCarousel();
        }
    }
}
