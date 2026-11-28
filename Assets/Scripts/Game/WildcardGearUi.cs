using UnityEngine;

namespace Arena
{
    /// The Wildcard's gear (Docs/Wildcard.md): four slot cards in the loadout (weapon, 1st and 2nd ability, ultimate: class
    /// icon, name, key). A click opens the pick window over the right column: one centred column of class labels, one class
    /// open at a time (it opens on the class of the slot's pick), its options as keys. Hovering one plays it in the loadout
    /// preview and describes it in the window's info box; a click picks it and closes the window (Esc or X closes too).
    /// Preview: WILDCARD=1 [WILDSLOT=0-3: that slot's window open] [WILDOPEN=<class>] shot.sh x lobby.
    public partial class ArenaApp
    {
        int wildPickOpen = -1, wildPickFor = -1;  // the slot whose pick window is open (-1 none), and whose loadout it's for
        int wildHoverSlot = -1, wildHoverVal;  // the pick window's hovered option (last frame's): the preview plays it
        int wildOpenKit = -1, wildOpenFor = -1;  // the pick window's one open class, and the slot it was opened for

        int WildKitOf(LobbyEntry me, int slot, int second) =>
            slot == 0 ? (int)Defs.WildWeaponKit(me.Weapon) : slot == 1 ? me.Ability / 3 : slot == 2 ? second / 3 : (int)me.UltKit;

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

        /// The four slots (what you have) as cards in a 2 x 2 grid: what the slot is and its key on top, the class icon,
        /// the name. Click one to open the pick window for it.
        void DrawWildSlots(LobbyEntry me, float x, float y, float w, float yEnd)
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

        /// A small solid triangle: pointing down (open) or right (folded), centred on c.
        static void FoldArrow(Vector2 c, bool down, Color col)
        {
            for (int i = 0; i < 4; i++)
            {
                if (down)
                    Ui.Fill(new Rect(Mathf.Round(c.x - 4f + i), Mathf.Round(c.y - 2f + i), 8f - 2f * i, 1f), col);
                else
                    Ui.Fill(new Rect(Mathf.Round(c.x - 2f + i), Mathf.Round(c.y - 4f + i), 1f, 8f - 2f * i), col);
            }
        }

        string WildSlotKey(int slot) => slot == 0 ? ""
            : Keybinds.KeyName(Keybinds.Get(slot == 1 ? KeyAction.Ability1 : slot == 2 ? KeyAction.Ability2 : KeyAction.Ultimate)).ToUpperInvariant();

        /// The pick window over the loadout's right column: every class's options for the open slot, one block a class
        /// in one column of class labels, one open at a time. A click picks and closes; Esc or the X closes.
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
                wildOpenFor = -1;
                if (ev.type == EventType.KeyDown)
                    ev.Use();
                return;
            }
            int cur = slot == 0 ? me.Weapon : slot == 1 ? me.Ability : slot == 2 ? second : me.Ult;
            // One column down the middle: a label a class, only one class open at a time (opening one folds the others).
            // The window opens on the class of the slot's pick.
            if (wildOpenFor != slot)
            {
                wildOpenFor = slot;
                wildOpenKit = WildKitOf(me, slot, second);
            }
            const float headH = 24f, ks = 4f, kh = 26f, blockGap = 4f;
            float bw = Mathf.Min(320f, r.width - 32f), bx = r.center.x - bw / 2f, y0 = r.y + 52f;
            int hovSlot = -1, hovVal = 0, hovKit = 0;
            for (int k = 0; k < Defs.KitCount; k++)
            {
                bool open = KitOpen((Kit)k), shut = wildOpenKit != k;
                // The class's label: a click opens it (and folds the open one), or folds it if it's the open one.
                var hr = new Rect(bx, y0, bw, headH);
                bool hhov = GUI.enabled && hr.Contains(ev.mousePosition);
                Ui.Fill(hr, (shut ? CrtStyle.Ink : wild).WithAlpha(hhov ? 0.09f : shut ? 0.03f : 0.08f));
                if (Ui.HitArea(hr))
                {
                    wildOpenKit = shut ? k : -1;
                    shut = !shut;
                }
                Color lc = (open ? (shut ? CrtStyle.Ink : wild) : CrtStyle.Dim).WithAlpha(open ? (hhov || !shut ? 1f : 0.8f) : 0.5f);
                FoldArrow(new Vector2(hr.x + 10f, hr.center.y), !shut, lc);
                Ui.Icon(new Rect(hr.x + 22f, hr.center.y - 8f, 16f, 16f), PixelArt.Get(Classes.Get((Kit)k).Icon), open ? Color.white : new Color(1f, 1f, 1f, 0.35f));
                Ui.Label(new Rect(hr.x + 46f, hr.y, bw - 46f, headH), Defs.KitName((Kit)k).ToUpperInvariant(), 12, lc, TextAnchor.MiddleLeft, true);
                if (WildKitOf(me, slot, second) == k)  // where the slot's pick comes from
                    Ui.Label(new Rect(hr.x, hr.y, bw - 8f, headH), "EQUIPPED", 9, wild, TextAnchor.MiddleRight, true);
                int n = slot == 0 ? Defs.WeaponCount(Role.Dps, (Kit)k) : slot == 3 ? Upgrades.UltKeys : 3;
                float ky = hr.yMax + 4f;
                int shown = 0;
                if (shut)
                {
                    y0 = hr.yMax + blockGap;
                    continue;
                }
                for (int c = 0; c < n; c++)
                {
                    var id = slot == 3 ? (c == 0 ? Upgrades.Ultimate(Role.Wildcard, (Kit)k) : Upgrades.AltUltimate(Role.Wildcard, (Kit)k, c)) : AbilityId.None;
                    bool gapped = slot == 0 ? Defs.WeaponGap(Role.Dps, (Kit)k, c) : slot == 3 ? id == AbilityId.None : Upgrades.AbilityGap(Role.Dps, (Kit)k, c);
                    if (gapped)
                        continue;
                    var b = new Rect(bx + 14f, ky + shown * (kh + ks), bw - 14f, kh);
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
                        wildOpenFor = -1;
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
                y0 = ky + shown * (kh + ks) + blockGap + 4f;
            }
            wildHoverSlot = hovSlot;
            wildHoverVal = hovVal;

            const float infoH = 84f;
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
