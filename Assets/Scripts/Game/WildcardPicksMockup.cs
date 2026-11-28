using UnityEngine;

namespace Arena
{
    /// A candidate redesign of the Wildcard's gear picker (LobbyUi.DrawWildcardPicks), drawn with the real widgets for the
    /// owner to judge: `wildLook` 1 = four slot cards; a click opens a pick window over the column with every class's
    /// options for that slot. 0 keeps today's wheel. Preview: WILDCARD=1 WILDLOOK=1 [WILDSLOT=0-3: that slot's window open].
    public partial class ArenaApp
    {
        int wildLook;     // 0 today's wheel, 1 the slots mockup
        int wildPickOpen = -1;
        int wildHoverSlot = -1, wildHoverVal;  // the pick window's hovered option (last frame's): the preview plays it  // the slots mockup: the slot whose pick window is open (-1 none) (0 weapon, 1 1st ability, 2 2nd ability, 3 ultimate)

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

        // ---- the slot cards and the pick window ----

        /// The four slots (what you have) as cards in a 2 x 2 grid: what the slot is and its key on top, the class icon,
        /// the name. Click one to open the pick window for it.
        void DrawWildSlots(LobbyEntry me, Session tgt, bool self, float x, float y, float w, float yEnd)
        {
            bool calm = fxReduced;
            var ev = Event.current;
            var wild = (Defs.Get(Role.Wildcard).Color).ToColor();
            int second = Defs.WildSecond(me.Wild);
            LobbyKit.Heading(x, y, w, "Gear", null, calm, 15);
            y += CarKeyY;
            const float gap = 8f;
            float cw = (w - gap) / 2f, ch = (yEnd - y - gap) / 2f;
            for (int i = 0; i < 4; i++)
            {
                var b = new Rect(x + (i % 2) * (cw + gap), y + (i / 2) * (ch + gap), cw, ch);
                if (DkKey(b, "", false, calm, accent: wild))
                {
                    wildPickOpen = i;
                    detailSel = i == 0 ? 0 : i == 3 ? 2 : 1;
                    detailSecond = i == 2;
                }
                bool hover = GUI.enabled && b.Contains(ev.mousePosition);
                Color tc = CrtKit.SoftText(CrtStyle.KeyColor(null, false), hover);
                Ui.Label(new Rect(b.x + 10f, b.y + 7f, b.width - 40f, 14f), i == 0 ? "WEAPON" : i == 3 ? "ULTIMATE" : "ABILITY", 10, wild.WithAlpha(0.9f), TextAnchor.MiddleLeft, true);
                string key = WildSlotKey(i);
                if (key.Length > 0)  // its keybind, top right
                {
                    var kr = new Rect(b.xMax - 24f, b.y + 7f, 15f, 15f);
                    CrtKit.Frame(kr, 1f, tc.WithAlpha(0.6f));
                    Ui.Label(kr, key, 9, tc, TextAnchor.MiddleCenter, true);
                }
                float isz = Mathf.Min(48f, ch - 52f);
                int k = WildKitOf(me, i, second);
                Ui.Icon(new Rect(b.x + 14f, b.center.y - isz / 2f + 6f, isz, isz), PixelArt.Get(Classes.Get((Kit)k).Icon), Color.white);
                float tx = b.x + 14f + isz + 12f, tw = b.xMax - 10f - tx;
                LobbyKit.Fit(new Rect(tx, b.center.y - 8f, tw, 22f), ChipName(me, i, second).ToUpperInvariant(), 15, 9, tc, TextAnchor.MiddleLeft, calm);
                Ui.Label(new Rect(tx, b.center.y + 13f, tw, 14f), Defs.KitName((Kit)k).ToUpperInvariant(), 9, tc.WithAlpha(0.55f), TextAnchor.MiddleLeft, true);
            }
        }

        string WildSlotKey(int slot) => slot == 0 ? ""
            : Keybinds.KeyName(Keybinds.Get(slot == 1 ? KeyAction.Ability1 : slot == 2 ? KeyAction.Ability2 : KeyAction.Ultimate)).ToUpperInvariant();

        /// The pick window over the loadout's right column: every class's options for the open slot, one block a class
        /// (icon and name, then its options in a column) in a 2 x 4 grid. A click picks and closes; Esc or the X closes.
        void DrawWildPickWindow(LobbyEntry me, Session tgt, bool self, Rect r, bool calm)
        {
            var ev = Event.current;
            var wild = (Defs.Get(Role.Wildcard).Color).ToColor();
            int second = Defs.WildSecond(me.Wild);
            int slot = wildPickOpen;
            Ui.Fill(r, new Color(0.02f, 0.035f, 0.07f, 0.97f));
            LobbyKit.Panel(r);
            string title = slot == 0 ? "Pick a weapon" : slot == 3 ? "Pick an ultimate" : slot == 1 ? "Pick ability 1" : "Pick ability 2";
            LobbyKit.Heading(r.x + 16f, r.y + 10f, r.width - 60f, title, null, calm);
            if (DkKey(new Rect(r.xMax - 40f, r.y + 10f, 26f, 26f), "X", false, calm, size: 13, minSize: 10)
                || (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.Escape))
            {
                wildPickOpen = -1;
                wildHoverSlot = -1;
                if (ev.type == EventType.KeyDown)
                    ev.Use();
                return;
            }
            int cur = slot == 0 ? me.Weapon : slot == 1 ? me.Ability : slot == 2 ? second : me.Ult;
            const int cols = 2, rows = 4;
            float gx = 12f, gy = 10f;
            const float infoH = 92f;
            float top = r.y + 50f, bw = (r.width - 32f - gx) / cols, bh = (r.yMax - 14f - infoH - 10f - top - gy * (rows - 1)) / rows;
            int hovSlot = -1, hovVal = 0, hovKit = 0;
            for (int k = 0; k < Defs.KitCount; k++)
            {
                var blk = new Rect(r.x + 16f + (k % cols) * (bw + gx), top + (k / cols) * (bh + gy), bw, bh);
                bool open = KitOpen((Kit)k);
                Ui.Icon(new Rect(blk.x, blk.y + 1f, 16f, 16f), PixelArt.Get(Classes.Get((Kit)k).Icon), open ? Color.white : new Color(1f, 1f, 1f, 0.35f));
                Ui.Label(new Rect(blk.x + 22f, blk.y, blk.width - 22f, 18f), Defs.KitName((Kit)k).ToUpperInvariant(), 11, (open ? CrtStyle.Ink : CrtStyle.Dim).WithAlpha(open ? 0.9f : 0.5f), TextAnchor.MiddleLeft, true);
                int n = slot == 0 ? Defs.WeaponCount(Role.Dps, (Kit)k) : slot == 3 ? Upgrades.UltKeys : 3;
                float ky = blk.y + 22f, kh = Mathf.Min(26f, (blk.yMax - ky - 3f * (n - 1)) / n);
                int shown = 0;
                for (int c = 0; c < n; c++)
                {
                    var id = slot == 3 ? (c == 0 ? Upgrades.Ultimate(Role.Wildcard, (Kit)k) : Upgrades.AltUltimate(Role.Wildcard, (Kit)k, c)) : AbilityId.None;
                    bool gapped = slot == 0 ? Defs.WeaponGap(Role.Dps, (Kit)k, c) : slot == 3 ? id == AbilityId.None : Upgrades.AbilityGap(Role.Dps, (Kit)k, c);
                    if (gapped)
                        continue;
                    var b = new Rect(blk.x, ky + shown * (kh + 3f), blk.width, kh);
                    shown++;
                    if (!open)
                    {
                        LockedSlot(b, EngineerLockText);
                        continue;
                    }
                    int v = slot == 0 ? Defs.WildWeaponPick((Kit)k, c) : slot == 3 ? Upgrades.WildUlt((Kit)k, c) : k * 3 + c;
                    string name = slot == 0 ? Defs.Weapon(Role.Wildcard, Kit.Soldier, v).Name
                        : slot == 3 ? Upgrades.Ability(id).Name
                        : Upgrades.Ability(Upgrades.FirstAbility(Role.Wildcard, Kit.Soldier, v)).Name;
                    int other = slot == 1 ? second : slot == 2 ? me.Ability : -1;  // the other ability slot's pick: tagged
                    if (DkKey(b, name.ToUpperInvariant(), v == cur, calm, size: 12, minSize: 8, accent: wild))
                    {
                        if (slot == 0)
                            WildPickWeapon(me, tgt, self, v, second);
                        else if (slot == 3)
                            WildPickUlt(me, tgt, self, v);
                        else
                            WildPickAbility(me, tgt, self, v, second, slot - 1);
                        wildPickOpen = -1;
                        hovSlot = -1;
                    }
                    if (GUI.enabled && b.Contains(ev.mousePosition))
                    {
                        hovSlot = slot;
                        hovVal = v;
                        hovKit = k;
                    }
                    if (v == other)
                        SlotBadge(new Rect(b.xMax - 14f, b.center.y - 6f, 12f, 12f), slot == 1 ? 2 : 1, wild);
                }
            }
            wildHoverSlot = hovSlot;
            wildHoverVal = hovVal;

            // The info box: the hovered option (else the slot's pick), named with its class, and what it does.
            int iv = hovSlot >= 0 ? hovVal : cur, ik = hovSlot >= 0 ? hovKit : WildKitOf(me, slot, second);
            var ib = new Rect(r.x + 16f, r.yMax - 14f - infoH, r.width - 32f, infoH);
            CrtKit.SoftBox(ib, wild, false, false, false);
            string iname, idesc;
            if (slot == 0)
            {
                var wd = Defs.Weapon(Role.Wildcard, Kit.Soldier, iv);
                iname = wd.Name;
                idesc = KeyText.Fill(wd.Desc);
            }
            else
            {
                var ad = Upgrades.Ability(slot == 3 ? Upgrades.Ultimate(Role.Wildcard, (Kit)ik, iv) : Upgrades.FirstAbility(Role.Wildcard, Kit.Soldier, iv));
                iname = ad.Name;
                idesc = KeyText.Fill(ad.Desc);
            }
            Ui.Icon(new Rect(ib.x + 10f, ib.y + 9f, 16f, 16f), PixelArt.Get(Classes.Get((Kit)ik).Icon), Color.white);
            LobbyKit.Fit(new Rect(ib.x + 32f, ib.y + 6f, ib.width - 140f, 22f), iname, 16, 10, Color.white, TextAnchor.MiddleLeft, calm);
            Ui.Label(new Rect(ib.xMax - 110f, ib.y + 6f, 100f, 22f), hovSlot >= 0 && iv != cur ? "PREVIEWING" : "EQUIPPED", 9, wild, TextAnchor.MiddleRight, true);
            FitText(new Rect(ib.x + 10f, ib.y + 30f, ib.width - 20f, ib.height - 36f), idesc, 12, Ui.Text);
        }
    }
}
