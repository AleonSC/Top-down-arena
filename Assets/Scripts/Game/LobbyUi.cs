using System;
using UnityEngine;

namespace Arena
{
    /// The lobby: the mode switch (a normal run or the Practice Range), the party with each player's
    /// character drawn live, your loadout with a big preview of your character, and in Practice Range
    /// mode the shared wave editor (PracticeUi.cs).
    public partial class ArenaApp
    {
        static readonly Color PracticeColor = new Color(0.22f, 0.78f, 0.62f);
        static readonly Color ModColor = new Color(0.85f, 0.45f, 0.95f);

        int lobbyTab;              // 0 = your loadout, 1 = practice waves, 2 = modifiers, 3 = difficulty, 4 = boss hunt, 5 = perks
        bool lobbyWasPractice, lobbyWasHunt;
        bool connInfoOpen;         // the "!" popup by the title: hosting/port and the invite addresses
        readonly Player previewPlayer = new Player { Id = -100 };

        // The hero card's live mini-arena (Showcase.cs): the real game, offline, using your weapon on target
        // dummies; it casts an ability while you hover one, and for a few seconds after you pick one.
        Showcase showcase;
        float showcaseSeen;        // when the hero card last drew it (it's stopped once nobody looks)
        int abilityHover = -1;     // the ability button under the mouse last frame
        float abilityDemoUntil;
        const float AbilityDemoTime = 5f;
        float ultDemoUntil;        // clicking the ultimate on the hero card shows it for a while
        const float UltDemoTime = 12f;

        void DrawLobby()
        {
            float t = Time.unscaledTime;
            lockedTipOn = false;
            LobbyEntry me;
            session.Lobby.TryGetValue(session.MyId, out me);
            bool practice = session.PracticeMode;
            if (practice != lobbyWasPractice)
            {
                lobbyTab = practice ? 1 : 0;  // jump to the waves when the host turns practice on
                lobbyWasPractice = practice;
            }
            if (!practice && lobbyTab == 1)
                lobbyTab = 0;
            bool hunt = session.HuntMode && !practice && session.Resume == null;
            if (hunt != lobbyWasHunt)
            {
                if (hunt)
                    lobbyTab = 4;  // to the boss picker when the host picks Boss Hunt (HuntUi.cs)
                lobbyWasHunt = hunt;
            }
            if (!hunt && lobbyTab == 4)
                lobbyTab = 0;
            if (practice && lobbyTab == PerkTab)
                lobbyTab = 0;  // perks are off in the Practice Range
            var infoButton = new Rect(196f, 28f, 28f, 28f);
            var infoPanel = ConnInfoRect(infoButton);
            if (connInfoOpen && Event.current.type == EventType.MouseDown && !infoButton.Contains(Event.current.mousePosition))
            {
                // The popup is drawn last, so eat clicks on it before the buttons under it see them; a click outside closes it.
                if (!infoPanel.Contains(Event.current.mousePosition))
                    connInfoOpen = false;
                Event.current.Use();
            }

            // --- Left column, top: the title, then the mode switch (a choice for the whole lobby, so it sits with the party).
            Ui.Fancy(new Rect(40f, 16f, 300f, 50f), "LOBBY", 40, Color.white, new Color(0.72f, 0.8f, 1f), TextAnchor.MiddleLeft, Ui.Accent);
            if (Ui.Button(infoButton, "!", connInfoOpen ? Ui.Accent : Ui.Neutral))
                connInfoOpen = !connInfoOpen;
            if (session.Resume != null)
                DrawResumeBanner(new Rect(40f, 76f, 376f, 58f), t);  // continuing a saved run (SavesUi.cs)
            else
                DrawModeSwitch(new Rect(40f, 76f, 376f, 58f), t);

            // --- Left: the party, under the mode switch.
            float lx = 40f, lw = 376f, y = 150f;
            Ui.Heading(lx, y, lw, "Party  (" + session.Lobby.Count + "/" + Session.MaxPlayers + ")");
            y = PartyTop;
            int slot = 0;
            foreach (var e in session.Lobby.Values)
            {
                DrawPartyCard(new Rect(lx, y, lw, 88f), e, t, slot++);
                y += 96f;
            }
            for (; slot < Session.MaxPlayers; slot++)
            {
                DrawOpenSlot(new Rect(lx, y, lw, 88f), slot, t);
                y += 96f;
            }
            if (session.Status.Length > 0)
                Ui.Label(new Rect(lx, y + 4f, lw, H - 96f - 8f - (y + 4f)), session.Status, 12, Ui.Warn, TextAnchor.LowerLeft, false, true);

            // --- Right: tabs level with the title, then your loadout or the practice waves.
            var area = new Rect(440f, 24f, W - 480f, H - 120f);
            DrawLobbyTabs(new Rect(area.x, area.y, area.width, 34f), practice);
            var body = new Rect(area.x, LobbyBodyTop, area.width, area.yMax - LobbyBodyTop);
            if (lobbyTab == 1 && practice)
                DrawPracticeEditor(body, t);
            else if (lobbyTab == 4 && hunt)
                DrawHuntPicker(body, t);  // HuntUi.cs
            else if (lobbyTab == 2)
                DrawModifiers(body, t);
            else if (lobbyTab == 3)
                DrawDifficulty(body, t);  // DifficultyUi.cs
            else if (lobbyTab == PerkTab && !practice)
                DrawPerks(body, t);  // PerksUi.cs
            else if (session.Resume != null)
                DrawResumeSeats(body, t);  // SavesUi.cs
            else if (me != null)
                DrawLoadout(me, body, t);

            DrawLobbyBottom(me, practice);
            DrawLockedTip();  // on top of the loadout
            if (connInfoOpen)
                DrawConnInfo(infoPanel);
        }

        const float LobbyBodyTop = 68f;   // the top of the loadout box (under the tabs)
        const float PartyTop = 184f;      // the top of the first party card (under the mode switch)

        string HostName()
        {
            LobbyEntry host;
            return session.Lobby.TryGetValue(1, out host) ? host.Name : "the host";
        }

        /// Three parts: a normal run, a Boss Hunt, or the Practice Range. Only the host can switch.
        void DrawModeSwitch(Rect r, float t)
        {
            bool practice = session.PracticeMode;
            int mode = practice ? 2 : session.HuntMode ? 1 : 0;
            Ui.Pill(r, new Color(0.04f, 0.05f, 0.08f, 0.9f));
            float half = (r.width - 8f) / 3f;
            for (int i = 0; i < 3; i++)
            {
                bool on = i == mode;
                var c = i == 0 ? Ui.Primary : i == 1 ? HuntColor : PracticeColor;
                var h = new Rect(r.x + 4f + i * half, r.y + 4f, half, r.height - 8f);
                bool hover = session.IsHost && h.Contains(Event.current.mousePosition);
                if (on)
                {
                    Ui.Pill(h, new Color(c.r, c.g, c.b, 0.85f));
                    Ui.Fill(new Rect(h.x + 10f, h.yMax - 3f, h.width - 20f, 2f), new Color(1f, 1f, 1f, 0.35f + 0.15f * Mathf.Sin(t * 3f)));
                }
                else if (hover)
                    Ui.Pill(h, new Color(c.r, c.g, c.b, 0.22f));
                string title = i == 0 ? "GAUNTLET" : i == 1 ? "BOSS HUNT" : "PRACTICE";
                string sub = i == 0 ? "50 waves, a boss every 5" : i == 1 ? "10 waves to one boss" : "Build your own waves";
                Ui.Fancy(new Rect(h.x, h.y + 4f, h.width, 24f), title, 15, Color.white, on ? new Color(0.85f, 0.92f, 1f) : Color.Lerp(c, Color.white, 0.4f), TextAnchor.MiddleCenter);
                Ui.Label(new Rect(h.x, h.y + 27f, h.width, 16f), sub, 10, on ? new Color(1f, 1f, 1f, 0.85f) : Ui.Muted, TextAnchor.MiddleCenter);
                if (session.IsHost && !on && Ui.HitArea(h))
                    session.SetMode(i == 2, i == 1);
            }
        }

        void DrawLobbyTabs(Rect r, bool practice)
        {
            int mods = Modifiers.Count(session.Modifiers);
            bool hunt = session.HuntMode && !practice && session.Resume == null;
            // Indexed by tab id (0 loadout, 1 practice, 2 modifiers, 3 difficulty, 4 boss hunt, 5 perks); null = not shown.
            string[] names = { session.Resume != null ? "Seats" : "Loadout", practice ? "Practice waves  (" + session.Plan.Waves.Count + ")" : null, mods > 0 ? "Modifiers  (" + mods + ")" : "Modifiers",
                "Difficulty", hunt ? "Boss hunt" : null, practice ? null : "Perks" };
            // The same order in every mode: the shared tabs, then the mode's own tab at the end.
            int[] order = { 0, PerkTab, 3, 2, 1, 4 };
            float x = r.x;
            foreach (int i in order)
            {
                if (names[i] == null)
                    continue;
                float w = 30f + names[i].Length * 8.2f;
                var tab = new Rect(x, r.y, w, r.height);
                bool on = lobbyTab == i;
                var c = i == 1 ? PracticeColor : i == 2 ? ModColor : i == 3 ? DiffColor(session.Diff.Level) : i == 4 ? HuntColor : i == PerkTab ? Ui.Gold : Ui.Accent;
                Ui.Pill(tab, on ? new Color(c.r, c.g, c.b, 0.9f) : new Color(1f, 1f, 1f, tab.Contains(Event.current.mousePosition) ? 0.1f : 0.05f));
                Ui.Label(tab, names[i].ToUpperInvariant(), 12, on ? Color.white : Ui.Muted, TextAnchor.MiddleCenter, true);
                if (!on && Ui.HitArea(tab))
                    lobbyTab = i;
                x += w + 8f;
            }
        }

        string PracticeSummary()
        {
            int enemies = 0, bosses = 0;
            foreach (var w in session.Plan.Waves)
                foreach (var kv in w.Counts)
                {
                    if (Defs.Get(kv.Key).IsBoss)
                        bosses += kv.Value;
                    else
                        enemies += kv.Value;
                }
            return session.Plan.Waves.Count + " wave" + (session.Plan.Waves.Count == 1 ? "" : "s") + "  ·  " + enemies + " enemies  ·  " + bosses + " boss" + (bosses == 1 ? "" : "es")
                + (session.Plan.Loop ? "  ·  looping" : "");
        }

        // --- Party ----------------------------------------------------------------

        void DrawPartyCard(Rect r, LobbyEntry e, float t, int slot)
        {
            var color = ToColor(Defs.Get(e.Role).Color);
            bool mine = e.Id == session.MyId;
            Ui.Card(r, color, mine ? 0.85f : 0.3f);
            // Portrait: their character, aiming around.
            var face = new Rect(r.x + 8f, r.y + 8f, 72f, 72f);
            Ui.Pill(face, new Color(color.r * 0.25f, color.g * 0.25f, color.b * 0.25f, 0.9f));
            if (BeginUiGl())
            {
                UiModelAt(new V2(face.center.x, face.center.y), 0.95f);
                DrawPreviewPlayer(e.Role, e.Kit, e.Weapon, t + slot * 1.7f);
                EndUiGl();
            }
            float tx = face.xMax + 14f;
            // The badges sit at the top right; work out where they start so the name stops short of them.
            float bx = r.xMax - 12f;
            if (mine)
                bx -= 44f;
            if (e.Id == 1)
                bx -= 54f;
            float badgesLeft = (mine || e.Id == 1) ? bx : r.xMax - 12f;
            DrawOneLine(new Rect(tx, r.y + 8f, badgesLeft - 8f - tx, 26f), e.Name, 19, 13, Color.white, true);
            bx = r.xMax - 12f;
            if (mine)
            {
                bx -= 44f;
                Ui.Badge(new Rect(bx, r.y + 10f, 44f, 20f), "YOU", Ui.Accent);
            }
            if (e.Id == 1)
            {
                bx -= 54f;
                Ui.Badge(new Rect(bx, r.y + 10f, 50f, 20f), "HOST", Ui.GoldButton);
            }
            DrawOneLine(new Rect(tx, r.y + 34f, r.xMax - tx - 10f, 20f), Defs.KitName(e.Kit) + " " + Defs.Get(e.Role).Name, 14, 11, Color.Lerp(color, Color.white, 0.15f), true);
            DrawOneLine(new Rect(tx, r.y + 56f, r.xMax - tx - 104f, 20f),
                Defs.Weapon(e.Role, e.Kit, e.Weapon).Name + "  ·  " + Upgrades.Ability(Upgrades.RoleAbility(e.Role, e.Kit, e.Ability)).Name, 12, 10, Ui.Muted, false);
            var ready = new Rect(r.xMax - 98f, r.yMax - 30f, 86f, 20f);
            if (e.Ready)
                Ui.Pill(new Rect(ready.x - 3f, ready.y - 3f, ready.width + 6f, ready.height + 6f), new Color(0.3f, 1f, 0.45f, 0.18f + 0.1f * Mathf.Sin(t * 4f)));
            Ui.Badge(ready, e.Ready ? "READY" : "NOT READY", e.Ready ? new Color(0.2f, 0.62f, 0.32f) : Ui.Neutral);
        }

        void DrawOpenSlot(Rect r, int slot, float t)
        {
            Ui.Fill(r, new Color(1f, 1f, 1f, 0.025f));
            // A dashed outline that slowly crawls.
            var c = new Color(1f, 1f, 1f, 0.12f);
            float off = (t * 18f) % 16f;
            for (float x = r.x - 16f + off; x < r.xMax; x += 16f)
            {
                float x0 = Mathf.Max(r.x, x), x1 = Mathf.Min(r.xMax, x + 8f);
                if (x1 > x0)
                {
                    Ui.Fill(new Rect(x0, r.y, x1 - x0, 1f), c);
                    Ui.Fill(new Rect(r.xMax - (x1 - r.x), r.yMax - 1f, x1 - x0, 1f), c);
                }
            }
            Ui.Fill(new Rect(r.x, r.y, 1f, r.height), c);
            Ui.Fill(new Rect(r.xMax - 1f, r.y, 1f, r.height), c);
            Ui.Label(new Rect(r.x, r.y + 20f, r.width, 24f), "Open slot", 15, new Color(1f, 1f, 1f, 0.45f), TextAnchor.MiddleCenter, true);
            Ui.Label(new Rect(r.x, r.y + 44f, r.width, 18f), slot == 1 ? "Click ! by the title for your address" : "Room for one more (optional)", 11, Ui.Muted, TextAnchor.MiddleCenter);
        }

        string[] InviteAddresses() => (addressList ?? "").Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);

        static float AddressWidth(string addr) => 14f + addr.Length * 8.5f;

        /// Where the "!" popup goes: just under its button, as tall as its contents.
        Rect ConnInfoRect(Rect button)
        {
            const float w = 376f, pad = 14f;
            float h = pad + 30f + 22f;
            if (session.IsHost)
            {
                int rows = 1;
                float bx = 0f;
                foreach (var addr in InviteAddresses())
                {
                    if (bx > 0f && bx + AddressWidth(addr) > w - 2f * pad)
                    {
                        bx = 0f;
                        rows++;
                    }
                    bx += AddressWidth(addr) + 6f;
                }
                h += 22f + rows * 28f + 16f;
            }
            else
                h += 20f;
            return new Rect(40f, button.yMax + 8f, w, h + pad);
        }

        /// The "!" popup: who's hosting and on which port, and the addresses friends join with.
        void DrawConnInfo(Rect r)
        {
            Ui.Fill(new Rect(r.x + 3f, r.y + 3f, r.width - 6f, r.height - 6f), new Color(0.05f, 0.06f, 0.09f, 0.97f));  // solid backing over the party
            Ui.Panel(r, true);
            float x = r.x + 14f, w = r.width - 28f, y = r.y + 14f;
            Ui.Heading(x, y, w, session.IsHost ? "Invite friends" : "Connection");
            y += 30f;
            Ui.Label(new Rect(x, y, w, 18f), session.IsHost ? "You're hosting  ·  port " + session.Port : "Connected to " + HostName(), 12, Ui.Text);
            y += 22f;
            if (session.IsHost)
            {
                Ui.Label(new Rect(x, y, w, 18f), "They join with port " + session.Port + " and one of:", 12, Ui.Muted);
                y += 22f;
                float bx = x;
                foreach (var addr in InviteAddresses())
                {
                    float bw = AddressWidth(addr);
                    if (bx > x && bx + bw > x + w)
                    {
                        bx = x;
                        y += 28f;
                    }
                    Ui.Badge(new Rect(bx, y, bw, 22f), addr, addr.StartsWith("100.") ? Ui.Accent : Ui.Neutral);
                    bx += bw + 6f;
                }
                y += 28f;
                Ui.Label(new Rect(x, y, w, 16f), "Blue = Tailscale address", 11, Ui.Muted);
            }
            else
                Ui.Label(new Rect(x, y, w, 20f), "Connected. Pick your loadout and click Ready.", 13, Ui.Good);
        }

        // --- Loadout --------------------------------------------------------------

        /// A player's character for a menu preview (the party cards), aiming around.
        /// Call between BeginUiGl and EndUiGl, after UiModelAt.
        void DrawPreviewPlayer(Role role, Kit kit, int weapon, float t)
        {
            var p = previewPlayer;
            p.Role = role;
            p.Def = Defs.Get(role);
            p.Kit = kit;
            p.Weapon = Defs.Weapon(role, kit, weapon);
            p.Hp = p.MaxHp = 1f;
            p.Disp = p.Pos = V2.Zero;
            p.Aim = -0.5f + 0.9f * Mathf.Sin(t * 0.7f);
            DrawPlayer(p, t, true);
        }

        static readonly string[] RoleIcons = { "role_tank", "role_healer", "role_dps" };  // PixelArt.cs

        void DrawLoadout(LobbyEntry me, Rect r, float t)
        {
            Ui.Panel(r);
            var roleColor = ToColor(Defs.Get(me.Role).Color);

            // Hero card: your character on a glowing pedestal, with the numbers that matter.
            var hero = new Rect(r.x + 14f, r.y + 14f, 232f, r.height - 28f);
            Ui.Pill(hero, new Color(0.03f, 0.035f, 0.06f, 0.85f));
            DrawShowcase(me, new Rect(hero.x + 8f, hero.y + 8f, hero.width - 16f, 180f), roleColor);
            float hy = hero.y + 196f;
            Ui.Fancy(new Rect(hero.x, hy, hero.width, 26f), Defs.KitName(me.Kit) + " " + Defs.Get(me.Role).Name, 20, Color.white, Color.Lerp(roleColor, Color.white, 0.3f), TextAnchor.MiddleCenter, roleColor);
            hy += 32f;
            int hp = (int)(Defs.Get(me.Role).Hp * Defs.KitHpMult(me.Kit));
            var wdef = Defs.Weapon(me.Role, me.Kit, me.Weapon);
            previewPlayer.Role = me.Role;  // (set here too: the party cards' live models set it, but they can be paused)
            previewPlayer.Def = Defs.Get(me.Role);
            previewPlayer.Kit = me.Kit;
            previewPlayer.Weapon = wdef;
            float reach = game.WeaponRange(previewPlayer);
            HeroStat(hero, ref hy, "Health", hp + " HP", Mathf.Clamp01(hp / 340f), new Color(0.35f, 0.95f, 0.45f));
            float speed = Defs.Get(me.Role).Speed * Defs.KitSpeedMult(me.Kit);
            HeroStat(hero, ref hy, "Speed", Mathf.RoundToInt(speed).ToString(), Mathf.Clamp01(speed / 300f), new Color(0.45f, 0.75f, 1f));
            HeroStat(hero, ref hy, wdef.Melee ? "Reach" : "Range", Mathf.RoundToInt(reach) + " px", Mathf.Clamp01(reach / 1100f), Ui.Gold);
            // The weapon's damage a second with no upgrades (measured in the sim: Defs.WeaponDps), against the strongest weapon.
            float dps = Defs.WeaponDps(wdef.Id);
            HeroStat(hero, ref hy, "Damage", dps > 0f ? Mathf.RoundToInt(dps) + " / s" : Mathf.RoundToInt(wdef.Damage) + (wdef.Melee ? " a swing" : " a hit"),
                dps > 0f ? Mathf.Clamp01(dps / MaxWeaponDps) : Mathf.Clamp01(wdef.Damage / 80f), new Color(1f, 0.45f, 0.4f));
            hy += 8f;
            var ult = Upgrades.Ability(Upgrades.Ultimate(me.Role, me.Kit));
            Ui.Fill(new Rect(hero.x + 14f, hy, hero.width - 28f, 1f), new Color(1f, 1f, 1f, 0.08f));
            hy += 8f;
            DrawUltBox(ult, new Rect(hero.x + 6f, hy - 4f, hero.width - 12f, hero.yMax - hy - 2f));

            // Choices: role and class rows (their blurbs sit in the heading line), then weapon and ability side by
            // side, then a spec sheet for the picked weapon and ability.
            float x = hero.xMax + 20f, w = r.xMax - 18f - x, y = r.y + 16f;
            const float gap = 8f;

            // Every row shows a few locked slots for content still to come (hover one: "Coming soon").
            float quarter = (w - gap * 3f) / 4f;  // roles and classes: 4 per row
            HeadingWithBlurb(x, y, w, "Role", Defs.Get(me.Role).Desc);
            int roleSlots = Mathf.Max(RoleSlots, Defs.Roles.Length);
            for (int i = 0; i < roleSlots; i++)
            {
                var b = new Rect(x + i * (quarter + gap), y + 28f, quarter, 46f);
                if (i >= Defs.Roles.Length)
                {
                    LockedSlot(b);
                    continue;
                }
                var rc = ToColor(Defs.Roles[i].Color);
                if (Ui.Choice(b, Defs.Roles[i].Name, (int)me.Role == i, rc, PixelArt.Get(RoleIcons[i]), (int)(Defs.Roles[i].Hp * Defs.KitHpMult(me.Kit)) + " HP", glow: false, iconLeft: true))
                {
                    session.RequestRole((Role)i);
                    hostRole = (Role)i;
                    joinRole = (Role)i;
                    weaponChoice = 0;
                    abilityChoice = 0;
                }
            }
            y += 84f;

            // Classes: rows of 4, the icon beside the name.
            HeadingWithBlurb(x, y, w, "Class", KitBlurb(me.Kit));
            int classSlots = Mathf.Max(ClassSlots, Defs.KitCount);
            int kitSize = 14;  // one size for every class name, small enough for the longest beside its icon
            for (int i = 0; i < Defs.KitCount; i++)
                while (kitSize > 10 && Ui.TextWidth(Defs.KitName((Kit)i), kitSize, true) > quarter - 16f - 20f)
                    kitSize--;
            for (int i = 0; i < classSlots; i++)
            {
                var b = new Rect(x + (i % 4) * (quarter + gap), y + 28f + (i / 4) * 38f, quarter, 32f);
                if (i >= Defs.KitCount)
                {
                    LockedSlot(b);
                    continue;
                }
                if (Ui.Choice(b, Defs.KitName((Kit)i), me.Kit == (Kit)i, roleColor, PixelArt.Get(Classes.Get((Kit)i).Icon), null, kitSize, glow: false, iconLeft: true))
                {
                    session.RequestKit((Kit)i);
                    kitChoice = (Kit)i;
                    weaponChoice = 0;
                    abilityChoice = 0;
                }
            }
            y += 28f + ((classSlots + 3) / 4) * 38f + 4f;

            // Weapon and ability: side by side, each a 2 x 2 grid.
            float col = (w - 16f) / 2f, ax = x + col + 16f, cell = (col - 6f) / 2f;
            Ui.Heading(x, y, col, "Weapon");
            Ui.Heading(ax, y, col, "Ability  ·  Space");
            int weaponSlots = Mathf.Max(WeaponSlots, Defs.WeaponsPerRole);
            for (int i = 0; i < weaponSlots; i++)
            {
                var b = new Rect(x + (i % 2) * (cell + 6f), y + 28f + (i / 2) * 36f, cell, 30f);
                if (i >= Defs.WeaponsPerRole)
                    LockedSlot(b);
                else if (Ui.Choice(b, Defs.Weapon(me.Role, me.Kit, i).Name, me.Weapon == i, roleColor, glow: false))
                {
                    session.RequestWeapon(i);
                    weaponChoice = i;
                }
            }
            int hovered = -1;
            int abilitySlots = Mathf.Max(AbilitySlots, Upgrades.RoleAbilityChoices);
            for (int i = 0; i < abilitySlots; i++)
            {
                var b = new Rect(ax + (i % 2) * (cell + 6f), y + 28f + (i / 2) * 36f, cell, 30f);
                if (i >= Upgrades.RoleAbilityChoices)
                {
                    LockedSlot(b);
                    continue;
                }
                if (b.Contains(Event.current.mousePosition))
                    hovered = i;
                if (Ui.Choice(b, Upgrades.Ability(Upgrades.RoleAbility(me.Role, me.Kit, i)).Name, me.Ability == i, roleColor, glow: false))
                {
                    session.RequestAbility(i);
                    abilityChoice = i;
                    abilityDemoUntil = Time.unscaledTime + AbilityDemoTime;
                    ultDemoUntil = 0f;
                }
            }
            if (Event.current.type == EventType.Repaint)
                abilityHover = hovered;
            y += 28f + ((Mathf.Max(weaponSlots, abilitySlots) + 1) / 2) * 36f + 4f;

            // The picked weapon's ascensions: all locked for now (hover one: "Coming soon").
            DrawAscensionStrip(new Rect(x, y, w, 32f), wdef);
            y += 42f;

            var ability = Upgrades.Ability(Upgrades.RoleAbility(me.Role, me.Kit, me.Ability));
            DrawSpecSheet(new Rect(x, y, w, r.yMax - 16f - y), wdef, ability, roleColor, t);
        }

        // How many slots each loadout row shows; the ones past the real content are locked ("Coming soon").
        const int RoleSlots = 4, ClassSlots = 8, WeaponSlots = 4, AbilitySlots = 4;

        Rect lockedTip;          // the locked slot under the mouse, if any (its tooltip is drawn last, on top)
        bool lockedTipOn;

        /// A slot for content still to come: dark, a dashed rim, a padlock and "Locked". Hovering shows "Coming soon".
        void LockedSlot(Rect r)
        {
            DashedCard(r);
            bool hover = r.Contains(Event.current.mousePosition);
            Ui.NoteHover(r);
            var ink = new Color(0.55f, 0.6f, 0.72f, hover ? 0.95f : 0.7f);
            float isz = Mathf.Min(14f, r.height - 12f), tw = Ui.TextWidth("Locked", 12, true) - 6f;
            float gx = r.center.x - (isz + 6f + tw) / 2f;
            Ui.Icon(new Rect(gx, r.center.y - isz / 2f, isz, isz), PixelArt.Get("ui_lock"), ink);
            Ui.Label(new Rect(gx + isz + 6f, r.y, tw + 10f, r.height), "Locked", 12, ink, TextAnchor.MiddleLeft, true);
            if (hover)
            {
                lockedTip = r;
                lockedTipOn = true;
            }
        }

        /// A dark card with a dashed rim: the look of everything locked.
        static void DashedCard(Rect r)
        {
            Ui.Pill(r, new Color(0.05f, 0.06f, 0.1f, 0.85f));
            var dash = new Color(1f, 1f, 1f, 0.14f);
            for (float dx = r.x + 6f; dx < r.xMax - 6f; dx += 8f)
            {
                float len = Mathf.Min(4f, r.xMax - 6f - dx);
                Ui.Fill(new Rect(dx, r.y, len, 1f), dash);
                Ui.Fill(new Rect(dx, r.yMax - 1f, len, 1f), dash);
            }
            for (float dy = r.y + 6f; dy < r.yMax - 6f; dy += 8f)
            {
                float len = Mathf.Min(4f, r.yMax - 6f - dy);
                Ui.Fill(new Rect(r.x, dy, 1f, len), dash);
                Ui.Fill(new Rect(r.xMax - 1f, dy, 1f, len), dash);
            }
        }

        const int AscensionsPerWeapon = 3;
        static readonly string[] Numerals = { "I", "II", "III", "IV", "V" };

        /// The picked weapon's ascensions in one row: its name over "ASCENSIONS" on the left, then a locked card per
        /// ascension (padlock, "Ascension II", "Locked") with a chevron between them. Hovering one shows "Coming soon".
        void DrawAscensionStrip(Rect r, WeaponDef wdef)
        {
            const float labelW = 92f;
            int nameSize = 11;
            while (nameSize > 8 && Ui.TextWidth(wdef.Name.ToUpperInvariant(), nameSize, true) > labelW + 4f)
                nameSize--;
            Ui.Label(new Rect(r.x, r.y + 1f, labelW + 10f, 15f), wdef.Name.ToUpperInvariant(), nameSize, Ui.Text, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(r.x, r.y + 16f, labelW + 10f, 14f), "ASCENSIONS  0/" + AscensionsPerWeapon, 9, Ui.Muted, TextAnchor.MiddleLeft, true);
            const float arrowW = 14f;
            float cx = r.x + labelW, cw = (r.xMax - cx - arrowW * (AscensionsPerWeapon - 1)) / AscensionsPerWeapon;
            for (int i = 0; i < AscensionsPerWeapon; i++)
            {
                var c = new Rect(cx + i * (cw + arrowW), r.y, cw, r.height);
                if (i > 0)
                    Ui.Label(new Rect(c.x - arrowW, c.y, arrowW, c.height), "›", 16, new Color(1f, 1f, 1f, 0.25f), TextAnchor.MiddleCenter, true);
                DashedCard(c);
                bool hover = c.Contains(Event.current.mousePosition);
                Ui.NoteHover(c);
                var ink = new Color(0.55f, 0.6f, 0.72f, hover ? 0.95f : 0.7f);
                Ui.Icon(new Rect(c.x + 10f, c.center.y - 7f, 14f, 14f), PixelArt.Get("ui_lock"), ink);
                Ui.Label(new Rect(c.x + 30f, c.y + 2f, c.width - 34f, 15f), "Ascension " + Numerals[i], 11, new Color(0.8f, 0.68f, 0.35f, hover ? 1f : 0.75f), TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(c.x + 30f, c.y + 16f, c.width - 34f, 13f), "Locked", 9, ink, TextAnchor.MiddleLeft, true);
                if (hover)
                {
                    lockedTip = c;
                    lockedTipOn = true;
                }
            }
        }

        /// The "Coming soon" tooltip over the hovered locked slot (call after everything else is drawn).
        void DrawLockedTip()
        {
            if (!lockedTipOn)
                return;
            const string text = "Coming soon";
            float w = Ui.TextWidth(text, 12, true) + 14f, h = 26f;
            var tip = new Rect(lockedTip.center.x - w / 2f, lockedTip.y - h - 6f, w, h);
            if (tip.y < 4f)
                tip.y = lockedTip.yMax + 6f;
            tip.x = Mathf.Clamp(tip.x, 4f, W - w - 4f);
            Ui.Fill(new Rect(tip.x + 2f, tip.y + 3f, tip.width, tip.height), new Color(0f, 0f, 0f, 0.35f));  // a soft drop shadow
            Ui.Pill(tip, new Color(0.11f, 0.13f, 0.21f, 0.98f));
            Ui.Fill(new Rect(tip.x + 8f, tip.y + 1f, tip.width - 16f, 1f), new Color(1f, 0.8f, 0.35f, 0.6f));
            Ui.Label(tip, text, 12, Ui.Gold, TextAnchor.MiddleCenter, true);
        }

        /// A section heading with a short blurb right-aligned in the same line (shrunk to fit).
        void HeadingWithBlurb(float x, float y, float w, string title, string blurb)
        {
            Ui.Heading(x, y, w, title);
            float left = Ui.TextWidth(title.ToUpperInvariant(), 12, true) + 24f;
            int size = 12;
            while (size > 10 && Ui.TextWidth(blurb, size) > w - left)
                size--;
            Ui.Label(new Rect(x + left, y, w - left, 20f), blurb, size, new Color(0.78f, 0.82f, 0.92f), TextAnchor.MiddleRight);
        }

        /// The loadout's spec sheet: the picked weapon (left) and ability (right), each with its name, description
        /// and a few number tiles, in a dark panel with plot-style corner brackets in the role's colour.
        void DrawSpecSheet(Rect r, WeaponDef wdef, AbilityDef ability, Color accent, float t)
        {
            if (r.height < 120f)
                return;
            Ui.Pill(r, new Color(0.025f, 0.03f, 0.06f, 0.85f));
            float half = r.width / 2f;
            Ui.Fill(new Rect(r.x + half, r.y + 12f, 1f, r.height - 24f), new Color(1f, 1f, 1f, 0.07f));
            var left = new Rect(r.x + 14f, r.y + 12f, half - 28f, r.height - 24f);
            var right = new Rect(r.x + half + 14f, r.y + 12f, half - 28f, r.height - 24f);
            const float tileH = 38f;

            // Weapon.
            SpecHeader(left, "WEAPON", wdef.Name, accent);
            var wTiles = new System.Collections.Generic.List<string[]>();
            if (wdef.Companion != MinionKind.None && wdef.CompanionCount > 0)
                wTiles.Add(new[] { wdef.CompanionCount.ToString(), "COMPANIONS" });
            if (wdef.Damage > 0f)
                wTiles.Add(new[] { Mathf.RoundToInt(wdef.Damage).ToString(), wdef.Melee ? "PER SWING" : "PER HIT" });
            if (wdef.FireInterval > 0f)
                wTiles.Add(new[] { (1f / wdef.FireInterval).ToString(wdef.FireInterval >= 0.5f ? "0.0" : "0"), wdef.Melee ? "SWINGS / S" : "SHOTS / S" });
            FitText(new Rect(left.x, left.y + 46f, left.width, left.yMax - tileH - 4f - (left.y + 46f)), wdef.Desc, 12, new Color(0.82f, 0.85f, 0.93f));
            SpecTiles(new Rect(left.x, left.yMax - tileH, left.width, tileH), wTiles, 0);

            // Ability: a cooldown ring tile (it fills over the cooldown, on a loop), then charges or a health cost.
            SpecHeader(right, "ABILITY  ·  SPACE", ability.Name, accent);
            // What it does, then what Lv 5 adds.
            DrawDescAndMastery(new Rect(right.x, right.y + 46f, right.width, right.yMax - tileH - 4f - (right.y + 46f)), ability.Desc, ability.Id, 12, new Color(0.82f, 0.85f, 0.93f));
            var aTiles = new System.Collections.Generic.List<string[]>();
            aTiles.Add(new[] { ability.Cooldown.ToString("0.#") + " s", "COOLDOWN" });
            if (ability.ExtraCharges > 0)
                aTiles.Add(new[] { (1 + ability.ExtraCharges).ToString(), "CHARGES" });
            if (ability.HealthCost > 0f)
                aTiles.Add(new[] { Mathf.RoundToInt(ability.HealthCost * 100f) + "%", ability.CostsCurrent ? "OF CURRENT HP" : "OF MAX HP" });
            var aRow = new Rect(right.x, right.yMax - tileH, right.width, tileH);
            SpecTiles(aRow, aTiles, 34f);  // the first tile leaves room for the ring

            if (BeginUiGl())
            {
                UiModelAt(V2.Zero, 1f);
                DrawPlotCorners(r, 0f, 14f, new Rgba(accent.r, accent.g, accent.b, 0.85f));
                var ring = new V2(aRow.x + 22f, aRow.center.y);
                float f = ability.Cooldown > 0f ? (t % ability.Cooldown) / ability.Cooldown : 1f;
                Arc(ring, 12f, 3.5f, new Rgba(1f, 1f, 1f, 0.1f), 40);
                Arc(ring, 12f, 3.5f, new Rgba(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.95f), 40, -MathX.Pi / 2f, MathX.Tau * Mathf.Max(0.02f, f));
                EndUiGl();
            }
        }

        /// The hero card's ultimate (style D): a charge ring (half full, as every ultimate starts a match) with its key, the
        /// name and charge time beside it, then what it does and what mastering it adds. Click it to watch it in the
        /// preview above; while that plays, the ring fills.
        void DrawUltBox(AbilityDef ult, Rect area)
        {
            float t = Time.unscaledTime;
            bool on = t < ultDemoUntil, hover = area.Contains(Event.current.mousePosition);
            if (on || hover)
                Ui.Pill(area, new Color(1f, 0.8f, 0.35f, on ? 0.12f : 0.06f));
            if (Ui.HitArea(area))
            {
                ultDemoUntil = on ? 0f : t + UltDemoTime;  // a second click stops it
                abilityDemoUntil = 0f;
            }
            float x = area.x + 8f, w = area.width - 16f, y = area.y + 8f;
            // The ring: half full, or filling up while the preview plays.
            const float rr = 23f;
            var c = new V2(x + rr + 4f, y + rr + 4f);
            float fill = on ? 0.5f + 0.5f * Mathf.Clamp01(1f - (ultDemoUntil - t) / UltDemoTime) : 0.5f;
            if (BeginUiGl())
            {
                UiModelAt(V2.Zero, 1f);
                Circle(c, rr - 4f, new Rgba(0.06f, 0.08f, 0.15f), 32);
                Arc(c, rr, 6f, new Rgba(0.14f, 0.16f, 0.24f), 48);
                Arc(c, rr, 6f, new Rgba(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 1f), 48, -MathX.Pi / 2f, MathX.Tau * fill);
                EndUiGl();
            }
            Ui.Fancy(new Rect(c.X - 20f, c.Y - 14f, 40f, 28f), "F", 22, Color.white, Ui.Gold, TextAnchor.MiddleCenter);
            float tx = x + rr * 2f + 18f, tw = area.xMax - 8f - tx;
            Ui.Fancy(new Rect(tx, y + 2f, tw, 22f), ult.Name, 16, new Color(1f, 0.95f, 0.75f), Ui.Gold, TextAnchor.MiddleLeft);
            Ui.Label(new Rect(tx, y + 23f, tw, 16f), ult.Cooldown.ToString("0") + " s to charge", 11, Ui.Text);
            Ui.Label(new Rect(tx, y + 37f, tw, 16f), "starts each match half full", 11, Ui.Muted);
            y += rr * 2f + 14f;
            // What it does, then what mastering it adds; the click hint stays at the bottom.
            float bottom = area.yMax - 20f;
            DrawDescAndMastery(new Rect(x, y, w, bottom - y), ult.Desc, ult.Id, 11, new Color(0.79f, 0.82f, 0.91f));
            Ui.Label(new Rect(area.x, area.yMax - 18f, area.width, 14f), on ? "PREVIEWING  ·  CLICK TO STOP" : "CLICK THE RING TO PREVIEW", 9,
                on || hover ? Ui.Gold : new Color(0.44f, 0.47f, 0.58f), TextAnchor.MiddleCenter, true);
        }

        /// An ability's description with what mastering it (level 5) adds under it in gold, both always shown in r: when
        /// they don't fit, the text gets smaller (down to 9), then the description is shortened with "…" (the mastery
        /// line comes first: it's the part people look for). centre: centred, without the star (upgrade cards).
        void DrawDescAndMastery(Rect r, string desc, AbilityId id, int size, Color descColor, bool centre = false)
        {
            var align = centre ? TextAnchor.UpperCenter : TextAnchor.UpperLeft;
            string cap = Game.Capstone(id);
            GUI.BeginGroup(r);
            if (string.IsNullOrEmpty(cap))
            {
                Ui.Label(new Rect(0f, 0f, r.width, Ui.TextHeight(desc, size, r.width)), desc, size, descColor, align, false, true);
                GUI.EndGroup();
                return;
            }
            string mastery = "Mastered (Lv 5): " + cap, descFull = desc;
            float starW = centre ? 0f : 14f;
            int s = size, ms = Mathf.Max(9, size - 1);
            float dh = Ui.TextHeight(desc, s, r.width), mh = Ui.TextHeight(mastery, ms, r.width - starW);
            while (dh + 3f + mh > r.height && s > 9)
            {
                s--;
                ms = Mathf.Max(9, s - 1);
                dh = Ui.TextHeight(desc, s, r.width);
                mh = Ui.TextHeight(mastery, ms, r.width - starW);
            }
            if (dh + 3f + mh > r.height)
            {
                desc = Shorten(desc, s, r.width, r.height - 3f - mh);
                float line = Ui.TextHeight("A", ms, 999f);  // one line, as the label measures it (with its padding)
                if (desc.Length == 0 && r.height >= 2f * line - 6f)
                {
                    // Very tight: a line each, both cut short, rather than losing the description altogether.
                    mastery = Shorten(mastery, ms, r.width - starW, line);
                    mh = line;
                    desc = Shorten(descFull, s, r.width, Mathf.Max(line, r.height - mh));
                    if (desc.Length > 0)
                        dh = Ui.TextHeight(desc, s, r.width);
                }
                dh = desc.Length > 0 ? Ui.TextHeight(desc, s, r.width) : 0f;
            }
            if (desc.Length > 0)
                Ui.Label(new Rect(0f, 0f, r.width, dh), desc, s, descColor, align, false, true);
            float my = desc.Length > 0 ? Mathf.Min(dh + 3f, r.height - mh) : 0f;
            if (!centre)
                Ui.Icon(new Rect(0f, my + 1f, 10f, 10f), PixelArt.Get("ui_star"), Ui.Gold);
            Ui.Label(new Rect(starW, my, r.width - starW, mh), mastery, ms, new Color(0.85f, 0.75f, 0.48f), align, false, true);
            GUI.EndGroup();
        }

        /// One line of text that always stays inside r: smaller text first (down to minSize), then cut short with "…".
        static void DrawOneLine(Rect r, string text, int size, int minSize, Color color, bool bold)
        {
            int s = size;
            while (s > minSize && Ui.TextWidth(text, s, bold) > r.width)
                s--;
            if (Ui.TextWidth(text, s, bold) > r.width)
            {
                int n = text.Length;
                while (n > 1 && Ui.TextWidth(text.Substring(0, n).TrimEnd() + "…", s, bold) > r.width)
                    n--;
                text = text.Substring(0, n).TrimEnd() + "…";
            }
            Ui.Label(r, text, s, color, TextAnchor.MiddleLeft, bold);
        }

        /// Wrapped text that always stays inside r: smaller text first (down to 10), then cut short with "…".
        void FitText(Rect r, string text, int size, Color color)
        {
            int s = size;
            while (s > 10 && Ui.TextHeight(text, s, r.width) > r.height)
                s--;
            if (Ui.TextHeight(text, s, r.width) > r.height)
                text = Shorten(text, s, r.width, r.height);
            GUI.BeginGroup(r);
            Ui.Label(new Rect(0f, 0f, r.width, r.height), text, s, color, TextAnchor.UpperLeft, false, true);
            GUI.EndGroup();
        }

        /// The text cut at a word and ended with "…" so it fits in height h (empty if not even a line does).
        static string Shorten(string text, int size, float width, float h)
        {
            var words = text.Split(' ');
            for (int n = words.Length - 1; n > 0; n--)
            {
                string t = string.Join(" ", words, 0, n).TrimEnd(',', '.', ';', ':') + "…";
                if (Ui.TextHeight(t, size, width) <= h)
                    return t;
            }
            return "";
        }

        void SpecHeader(Rect col, string eyebrow, string name, Color accent)
        {
            Ui.Label(new Rect(col.x, col.y, col.width, 14f), eyebrow, 10, Ui.Muted, TextAnchor.MiddleLeft, true);
            int size = 20;
            while (size > 13 && Ui.TextWidth(name, size, true) > col.width)
                size--;
            Ui.Fancy(new Rect(col.x, col.y + 15f, col.width + 10f, 28f), name, size, Color.white, Color.Lerp(accent, Color.white, 0.6f), TextAnchor.MiddleLeft);
        }

        static float SpecTileWidth(Rect row, int n) => n <= 0 ? 0f : (row.width - 6f * (n - 1)) / n;

        /// A row of number tiles: a big gold value over a small caption. pad = room left free at the first tile's left.
        void SpecTiles(Rect row, System.Collections.Generic.List<string[]> tiles, float pad)
        {
            float tw = SpecTileWidth(row, tiles.Count);
            for (int i = 0; i < tiles.Count; i++)
            {
                var tile = new Rect(row.x + i * (tw + 6f), row.y, tw, row.height);
                Ui.Pill(tile, new Color(0.1f, 0.13f, 0.25f, 0.9f));
                float tx = tile.x + 9f + (i == 0 ? pad : 0f);
                int vs = 16;
                while (vs > 11 && Ui.TextWidth(tiles[i][0], vs, true) > tile.xMax - 6f - tx)
                    vs--;
                Ui.Label(new Rect(tx, tile.y + 3f, tile.xMax - 6f - tx, 22f), tiles[i][0], vs, Ui.Gold, TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(tx, tile.y + 23f, tile.xMax - 4f - tx, 14f), tiles[i][1], 9, Ui.Muted, TextAnchor.MiddleLeft, true);
            }
        }

        /// The live mini-arena on the hero card: your loadout in action, zoomed out to fit the dummies.
        void DrawShowcase(LobbyEntry me, Rect view, Color roleColor)
        {
            if (uiGlBlocked)
                return;  // the lobby sliding away as a run launches: no live preview (it would start up again) (LaunchIntro.cs)
            int shown = abilityHover >= 0 ? abilityHover : me.Ability;
            bool ult = abilityHover < 0 && Time.unscaledTime < ultDemoUntil;  // hovering an ability shows that instead
            bool demo = ult || abilityHover >= 0 || Time.unscaledTime < abilityDemoUntil;
            if (showcase == null)
                showcase = new Showcase();
            showcase.Show(me.Role, me.Kit, me.Weapon, Upgrades.RoleAbility(me.Role, me.Kit, shown), demo, ult);
            showcaseSeen = Time.unscaledTime;
            if (!BeginUiGl())
                return;
            var rc = new Rgba(roleColor.r, roleColor.g, roleColor.b);
            var sg = showcase.Game;
            float zoom = Mathf.Min(1.1f, view.height / (showcase.Extent + 190f));
            V2 focus = Showcase.Home + new V2(0f, -showcase.Extent / 2f);
            V2 centre = new V2(view.center.x, view.center.y);
            UiModelAt(centre - focus * zoom, zoom);
            UiClip(view);
            // The floor: the arena's dark grid, scaled with the view.
            float hw = view.width / 2f / zoom + 64f, hh = view.height / 2f / zoom + 64f;
            FillRect(focus.X - hw, focus.Y - hh, hw * 2f, hh * 2f, new Rgba(0.05f, 0.06f, 0.09f, 0.9f));
            var grid = new Rgba(1f, 1f, 1f, 0.04f);
            for (float gx = (float)Math.Floor((focus.X - hw) / 64f) * 64f; gx <= focus.X + hw; gx += 64f)
                Line(new V2(gx, focus.Y - hh), new V2(gx, focus.Y + hh), 1f / zoom, grid);
            for (float gy = (float)Math.Floor((focus.Y - hh) / 64f) * 64f; gy <= focus.Y + hh; gy += 64f)
                Line(new V2(focus.X - hw, gy), new V2(focus.X + hw, gy), 1f / zoom, grid);
            Circle(Showcase.Home, 46f, rc.WithAlpha(0.08f), 32);  // where you stand
            var real = game;
            game = sg;  // the drawing code reads `game`
            try
            {
                // Visual effects (Juice.cs) follow the preview while the lobby is up: the new shot looks, trails,
                // sparks, bullet endings and hit flashes, and the glow pass (melee energy, bullet and blast glows), all
                // clipped to the window (soft gradients too: ClippedVTri).
                bool fx = fxOn;  // (the preview only shows in the lobby, so no match is drawn alongside it)
                if (fx)
                {
                    UpdateJuice();
                    DrawJuiceFloor();
                }
                DrawArenaContents(sg.AnimT);
                if (fx)
                {
                    DrawJuiceParts();
                    SwitchToGlow(true);  // the added light, as in the arena (Juice.GlowPass)
                    DrawGlowPass();
                    SwitchToGlow(false);
                }
                foreach (var p in sg.Players.Values)
                    if (p.Weapon.Id == WeaponId.MendersBow && p.DrawT > 0f && !p.Downed)
                        DrawBowDraw(p, true, sg.AnimT);
                    else if (p.Weapon.Id == WeaponId.ArcLightning && p.DrawT > 0f && !p.Downed)
                        DrawArcCharge(p, true, sg.AnimT);  // (with Visual effects on this draws nothing: the glow pass has the charge)
            }
            finally
            {
                game = real;
            }
            // A thin frame in the role's colour.
            UiModelAt(V2.Zero, 1f);
            UiNoClip();
            var edge = rc.WithAlpha(0.45f);
            Line(new V2(view.x, view.y), new V2(view.xMax, view.y), 1.5f, edge);
            Line(new V2(view.x, view.yMax), new V2(view.xMax, view.yMax), 1.5f, edge);
            Line(new V2(view.x, view.y), new V2(view.x, view.yMax), 1.5f, edge);
            Line(new V2(view.xMax, view.y), new V2(view.xMax, view.yMax), 1.5f, edge);
            EndUiGl();
            if (demo)
            {
                var ab = Upgrades.Ability(ult ? Upgrades.Ultimate(me.Role, me.Kit) : Upgrades.RoleAbility(me.Role, me.Kit, shown));
                Ui.Label(new Rect(view.x + 8f, view.yMax - 20f, view.width - 16f, 16f), (ult ? "ULTIMATE  ·  " : Keybinds.KeyName(Keybinds.Get(KeyAction.Ability)).ToUpperInvariant() + "  ·  ") + ab.Name.ToUpperInvariant(), 10,
                    ult ? Ui.Gold : Color.Lerp(roleColor, Color.white, 0.5f), TextAnchor.MiddleCenter, true);
            }
        }

        /// The loadout preview runs only while the hero card is on screen.
        void ShowcaseFixedStep(float dt)
        {
            if (showcase == null)
                return;
            if (session.Screen != AppScreen.Lobby || Time.unscaledTime - showcaseSeen > 0.5f)
            {
                showcase.Stop();
                showcase = null;
                return;
            }
            showcase.Step(dt);
        }

        static float maxWeaponDps;
        static float MaxWeaponDps
        {
            get
            {
                if (maxWeaponDps <= 0f)
                    foreach (var w in Defs.Weapons)
                        maxWeaponDps = Mathf.Max(maxWeaponDps, Defs.WeaponDps(w.Id));
                return Mathf.Max(1f, maxWeaponDps);
            }
        }

        void HeroStat(Rect hero, ref float y, string name, string value, float frac, Color c)
        {
            Ui.Label(new Rect(hero.x + 14f, y, 90f, 16f), name.ToUpperInvariant(), 10, Ui.Muted, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(hero.x + 14f, y, hero.width - 28f, 16f), value, 12, Color.white, TextAnchor.MiddleRight, true);
            Ui.Bar(new Rect(hero.x + 14f, y + 18f, hero.width - 28f, 5f), frac, c);
            y += 27f;
        }

        // --- Bottom bar ------------------------------------------------------------

        void DrawLobbyBottom(LobbyEntry me, bool practice)
        {
            Ui.Fill(new Rect(0f, H - 88f, W, 88f), new Color(0.02f, 0.025f, 0.04f, 0.55f));
            Ui.Fill(new Rect(0f, H - 88f, W, 1f), new Color(1f, 1f, 1f, 0.06f));
            float t = Time.unscaledTime;
            // Leaving, Settings and the Field guide are in the menu Esc opens (MenuDrawer.cs); Ready and Start sit in the middle.
            // Bottom left: a small red back button, the same as Main Menu in the Esc menu.
            var backRect = new Rect(40f, H - 70f, 48f, 48f);
            bool back = Ui.Button(backRect, "", Ui.Danger);
            Ui.Icon(new Rect(backRect.center.x - 12f, backRect.center.y - 12f, 24f, 24f), PixelArt.Get("ui_back"));
            if (back)
                StartLobbyLeave();  // LobbyEnter.cs
            bool ready = me != null && me.Ready;
            float readyX = session.IsHost ? W / 2f - 260f : W / 2f - 120f;
            var readyRect = new Rect(readyX, H - 70f, 240f, 48f);
            if (Ui.FancyButton(readyRect, ready ? "Ready!" : "Ready?", ready ? Ui.GoldButton : Ui.Primary, PixelArt.Get("ui_check"), t, 0))
                session.RequestReady(!ready);
            int waiting = 0;  // other players who aren't ready yet
            foreach (var e in session.Lobby.Values)
                if (!e.Ready && e.Id != session.MyId)
                    waiting++;
            // Only with other players: who you're waiting on, or that everyone's ready.
            string note = session.Lobby.Count < 2 ? ""
                : waiting > 0 ? "Waiting for " + waiting + " player" + (waiting == 1 ? "" : "s") + " to ready up"
                : !ready ? "" : session.IsHost ? "Everyone's ready" : "Everyone's ready: waiting for the host to start";
            Ui.Label(new Rect(140f, H - 56f, readyX - 160f, 26f), note, 14, waiting == 0 ? Ui.Good : Ui.Muted, TextAnchor.MiddleRight);
            if (session.IsHost)
            {
                bool wasEnabled = GUI.enabled;
                GUI.enabled = session.AllReady && (!practice || session.Plan.Waves.Count > 0) && (practice || !session.HuntMode || session.Resume != null || session.LobbyHunt != null);  // (a locked hunt can't start)
                string start = "Start";
                if (Ui.FancyButton(new Rect(readyX + 260f, H - 70f, 260f, 48f), start, practice ? new Color(PracticeColor.r * 0.85f, PracticeColor.g * 0.85f, PracticeColor.b * 0.85f) : Ui.Primary, PixelArt.Get("ui_play"), t, 17))
                    session.StartMatch();
                GUI.enabled = wasEnabled;
            }
        }
    }
}
