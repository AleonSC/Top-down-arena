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

        int lobbyTab;              // 0 = your loadout, 1 = practice waves, 2 = modifiers
        bool lobbyWasPractice;
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
            var infoButton = new Rect(196f, 28f, 28f, 28f);
            var infoPanel = ConnInfoRect(infoButton);
            if (connInfoOpen && Event.current.type == EventType.MouseDown && !infoButton.Contains(Event.current.mousePosition))
            {
                // The popup is drawn last, so eat clicks on it before the buttons under it see them; a click outside closes it.
                if (!infoPanel.Contains(Event.current.mousePosition))
                    connInfoOpen = false;
                Event.current.Use();
            }

            // --- Top bar: title, mode switch, version.
            Ui.Fancy(new Rect(40f, 16f, 300f, 50f), "LOBBY", 40, Color.white, new Color(0.72f, 0.8f, 1f), TextAnchor.MiddleLeft, Ui.Accent);
            if (Ui.Button(infoButton, "!", connInfoOpen ? Ui.Accent : Ui.Neutral))
                connInfoOpen = !connInfoOpen;
            if (session.Resume != null)
                DrawResumeBanner(new Rect(W / 2f - 230f, 18f, 460f, 58f), t);  // continuing a saved run (SavesUi.cs)
            else
                DrawModeSwitch(new Rect(W / 2f - 230f, 18f, 460f, 58f), t);
            Ui.Label(new Rect(W - 240f, 8f, 200f, 16f), Session.VersionText, 11, Ui.Muted, TextAnchor.MiddleRight);

            // --- Left: the party.
            float lx = 40f, lw = 376f, y = 100f;
            Ui.Heading(lx, y, lw, "Party  (" + session.Lobby.Count + "/" + Session.MaxPlayers + ")");
            y += 30f;
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
            var picker = new Rect(lx, H - 96f - 84f, lw, 84f);
            if (session.Status.Length > 0)
                Ui.Label(new Rect(lx, y + 4f, lw, picker.y - 8f - (y + 4f)), session.Status, 12, Ui.Warn, TextAnchor.LowerLeft, false, true);
            DrawBackgroundPicker(picker);  // MenuBackgrounds.cs

            // --- Right: tabs, then your loadout or the practice waves.
            var area = new Rect(440f, 100f, W - 480f, H - 196f);
            DrawLobbyTabs(new Rect(area.x, area.y, area.width, 34f), practice);
            var body = new Rect(area.x, area.y + 40f, area.width, area.height - 40f);
            if (lobbyTab == 1 && practice)
                DrawPracticeEditor(body, t);
            else if (lobbyTab == 2)
                DrawModifiers(body, t);
            else if (session.Resume != null)
                DrawResumeSeats(body, t);  // SavesUi.cs
            else if (me != null)
                DrawLoadout(me, body, t);

            DrawLobbyBottom(me, practice);
            if (connInfoOpen)
                DrawConnInfo(infoPanel);
        }

        string HostName()
        {
            LobbyEntry host;
            return session.Lobby.TryGetValue(1, out host) ? host.Name : "the host";
        }

        /// Two big halves: a normal run, or the Practice Range. Only the host can switch.
        void DrawModeSwitch(Rect r, float t)
        {
            bool practice = session.PracticeMode;
            Ui.Pill(r, new Color(0.04f, 0.05f, 0.08f, 0.9f));
            float half = (r.width - 8f) / 2f;
            for (int i = 0; i < 2; i++)
            {
                bool on = (i == 1) == practice;
                var c = i == 0 ? Ui.Primary : PracticeColor;
                var h = new Rect(r.x + 4f + i * half, r.y + 4f, half, r.height - 8f);
                bool hover = session.IsHost && h.Contains(Event.current.mousePosition);
                if (on)
                {
                    Ui.Pill(h, new Color(c.r, c.g, c.b, 0.85f));
                    Ui.Fill(new Rect(h.x + 10f, h.yMax - 3f, h.width - 20f, 2f), new Color(1f, 1f, 1f, 0.35f + 0.15f * Mathf.Sin(t * 3f)));
                }
                else if (hover)
                    Ui.Pill(h, new Color(c.r, c.g, c.b, 0.22f));
                string title = i == 0 ? "RUN" : "PRACTICE RANGE";
                string sub = i == 0 ? "50 waves, a boss every 5" : "Build your own waves";
                Ui.Fancy(new Rect(h.x, h.y + 4f, h.width, 24f), title, 17, Color.white, on ? new Color(0.85f, 0.92f, 1f) : Color.Lerp(c, Color.white, 0.4f), TextAnchor.MiddleCenter);
                Ui.Label(new Rect(h.x, h.y + 27f, h.width, 16f), sub, 11, on ? new Color(1f, 1f, 1f, 0.85f) : Ui.Muted, TextAnchor.MiddleCenter);
                if (session.IsHost && !on && GUI.Button(h, GUIContent.none, GUIStyle.none))
                    session.SetPracticeMode(i == 1);
            }
            if (!session.IsHost)
                Ui.Label(new Rect(r.x, r.yMax + 2f, r.width, 14f), "The host picks the mode", 10, Ui.Muted, TextAnchor.MiddleCenter);
        }

        void DrawLobbyTabs(Rect r, bool practice)
        {
            int mods = Modifiers.Count(session.Modifiers);
            string[] names = { session.Resume != null ? "Seats" : "Your loadout", practice ? "Practice waves  (" + session.Plan.Waves.Count + ")" : null, mods > 0 ? "Modifiers  (" + mods + ")" : "Modifiers" };
            float x = r.x;
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] == null)
                    continue;
                float w = 30f + names[i].Length * 8.2f;
                var tab = new Rect(x, r.y, w, r.height);
                bool on = lobbyTab == i;
                var c = i == 1 ? PracticeColor : i == 2 ? ModColor : Ui.Accent;
                Ui.Pill(tab, on ? new Color(c.r, c.g, c.b, 0.9f) : new Color(1f, 1f, 1f, tab.Contains(Event.current.mousePosition) ? 0.1f : 0.05f));
                Ui.Label(tab, names[i].ToUpperInvariant(), 12, on ? Color.white : Ui.Muted, TextAnchor.MiddleCenter, true);
                if (!on && GUI.Button(tab, GUIContent.none, GUIStyle.none))
                    lobbyTab = i;
                x += w + 8f;
            }
            if (practice && session.Plan.Waves.Count > 0 && lobbyTab == 0)
                Ui.Label(new Rect(x + 8f, r.y, r.xMax - x - 8f, r.height), PracticeSummary(), 11, PracticeColor, TextAnchor.MiddleRight);
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
            Ui.Label(new Rect(tx, r.y + 8f, r.xMax - tx - 110f, 26f), e.Name, 19, Color.white, TextAnchor.MiddleLeft, true);
            float bx = r.xMax - 12f;
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
            Ui.Label(new Rect(tx, r.y + 34f, r.xMax - tx - 10f, 20f), Defs.KitName(e.Kit) + " " + Defs.Get(e.Role).Name, 14, Color.Lerp(color, Color.white, 0.15f), TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(tx, r.y + 56f, r.xMax - tx - 104f, 20f),
                Defs.Weapon(e.Role, e.Kit, e.Weapon).Name + "  ·  " + Upgrades.Ability(Upgrades.RoleAbility(e.Role, e.Kit, e.Ability)).Name, 12, Ui.Muted);
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
            previewPlayer.Weapon = wdef;
            float reach = game.WeaponRange(previewPlayer);
            HeroStat(hero, ref hy, "Health", hp + " HP", Mathf.Clamp01(hp / 340f), new Color(0.35f, 0.95f, 0.45f));
            float speed = Defs.Get(me.Role).Speed * Defs.KitSpeedMult(me.Kit);
            HeroStat(hero, ref hy, "Speed", Mathf.RoundToInt(speed).ToString(), Mathf.Clamp01(speed / 300f), new Color(0.45f, 0.75f, 1f));
            HeroStat(hero, ref hy, wdef.Melee ? "Reach" : "Range", Mathf.RoundToInt(reach) + " px", Mathf.Clamp01(reach / 1100f), Ui.Gold);
            hy += 8f;
            var ult = Upgrades.Ability(Upgrades.Ultimate(me.Role, me.Kit));
            Ui.Fill(new Rect(hero.x + 14f, hy, hero.width - 28f, 1f), new Color(1f, 1f, 1f, 0.08f));
            hy += 8f;
            // Click the ultimate to watch it in the preview above.
            var ultArea = new Rect(hero.x + 6f, hy - 4f, hero.width - 12f, hero.yMax - hy - 2f);
            bool ultOn = Time.unscaledTime < ultDemoUntil, ultHover = ultArea.Contains(Event.current.mousePosition);
            if (ultOn || ultHover)
                Ui.Pill(ultArea, new Color(1f, 0.8f, 0.35f, ultOn ? 0.12f : 0.06f));
            Ui.Label(new Rect(hero.x + 14f, hy, hero.width - 28f, 16f), ultOn ? "ULTIMATE  ·  F  ·  PREVIEWING" : "ULTIMATE  ·  F  ·  CLICK TO PREVIEW", 10, ultOn || ultHover ? Ui.Gold : Ui.Muted, TextAnchor.MiddleLeft, true);
            if (GUI.Button(ultArea, GUIContent.none, GUIStyle.none))
            {
                ultDemoUntil = ultOn ? 0f : Time.unscaledTime + UltDemoTime;  // a second click stops it
                abilityDemoUntil = 0f;
            }
            Ui.Fancy(new Rect(hero.x + 14f, hy + 14f, hero.width - 28f, 22f), ult.Name, 16, new Color(1f, 0.95f, 0.75f), Ui.Gold, TextAnchor.MiddleLeft);
            Ui.Label(new Rect(hero.x + 14f, hy + 38f, hero.width - 28f, hero.yMax - hy - 44f), ult.Desc + "  (" + ult.Cooldown.ToString("0") + " s)", 11, Ui.Muted, TextAnchor.UpperLeft, false, true);

            // Choices.
            float x = hero.xMax + 20f, w = r.xMax - 18f - x, y = r.y + 16f;
            const float gap = 8f;
            float third = (w - gap * 2f) / 3f;

            Ui.Heading(x, y, w, "Role");
            for (int i = 0; i < Defs.Roles.Length; i++)
            {
                var rc = ToColor(Defs.Roles[i].Color);
                var b = new Rect(x + i * (third + gap), y + 28f, third, 66f);
                if (Ui.Choice(b, Defs.Roles[i].Name, (int)me.Role == i, rc, PixelArt.Get(RoleIcons[i]), (int)(Defs.Roles[i].Hp * Defs.KitHpMult(me.Kit)) + " HP"))
                {
                    session.RequestRole((Role)i);
                    hostRole = (Role)i;
                    joinRole = (Role)i;
                    weaponChoice = 0;
                    abilityChoice = 0;
                }
            }
            Ui.Label(new Rect(x, y + 98f, w, 18f), Defs.Get(me.Role).Desc, 12, Ui.Muted);
            y += 122f;

            Ui.Heading(x, y, w, "Class");
            float quarter = (w - gap * (Defs.KitCount - 1)) / Defs.KitCount;
            int kitSize = 14;  // one size for the whole row, small enough for the longest name
            for (int i = 0; i < Defs.KitCount; i++)
                while (kitSize > 10 && Ui.TextWidth(Defs.KitName((Kit)i), kitSize, true) > quarter - 14f)
                    kitSize--;
            for (int i = 0; i < Defs.KitCount; i++)
                if (Ui.Choice(new Rect(x + i * (quarter + gap), y + 28f, quarter, 36f), Defs.KitName((Kit)i), me.Kit == (Kit)i, roleColor, null, null, kitSize))
                {
                    session.RequestKit((Kit)i);
                    kitChoice = (Kit)i;
                    weaponChoice = 0;
                    abilityChoice = 0;
                }
            Ui.Label(new Rect(x, y + 68f, w, 18f), KitBlurb(me.Kit), 12, Ui.Muted);
            y += 94f;

            Ui.Heading(x, y, w, "Weapon");
            for (int i = 0; i < Defs.WeaponsPerRole; i++)
                if (Ui.Choice(new Rect(x + i * (third + gap), y + 28f, third, 36f), Defs.Weapon(me.Role, me.Kit, i).Name, me.Weapon == i, roleColor))
                {
                    session.RequestWeapon(i);
                    weaponChoice = i;
                }
            Ui.Label(new Rect(x, y + 68f, w, 34f), wdef.Desc, 12, Ui.Muted, TextAnchor.UpperLeft, false, true);
            y += 106f;

            Ui.Heading(x, y, w, "Ability  ·  Space");
            int hovered = -1;
            for (int i = 0; i < Upgrades.RoleAbilityChoices; i++)
            {
                var b = new Rect(x + i * (third + gap), y + 28f, third, 36f);
                if (b.Contains(Event.current.mousePosition))
                    hovered = i;
                if (Ui.Choice(b, Upgrades.Ability(Upgrades.RoleAbility(me.Role, me.Kit, i)).Name, me.Ability == i, roleColor))
                {
                    session.RequestAbility(i);
                    abilityChoice = i;
                    abilityDemoUntil = Time.unscaledTime + AbilityDemoTime;
                    ultDemoUntil = 0f;
                }
            }
            if (Event.current.type == EventType.Repaint)
                abilityHover = hovered;
            var ability = Upgrades.Ability(Upgrades.RoleAbility(me.Role, me.Kit, me.Ability));
            Ui.Label(new Rect(x, y + 68f, w, 34f), ability.Desc + "  (" + ability.Cooldown.ToString("0.#") + " s cooldown)", 12, Ui.Muted, TextAnchor.UpperLeft, false, true);
        }

        /// The live mini-arena on the hero card: your loadout in action, zoomed out to fit the dummies.
        void DrawShowcase(LobbyEntry me, Rect view, Color roleColor)
        {
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
                DrawArenaContents(sg.AnimT);
                foreach (var p in sg.Players.Values)
                    if (p.Weapon.Id == WeaponId.MendersBow && p.DrawT > 0f && !p.Downed)
                        DrawBowDraw(p, true, sg.AnimT);
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
                Ui.Label(new Rect(view.x + 8f, view.yMax - 20f, view.width - 16f, 16f), (ult ? "ULTIMATE  ·  " : "SPACE  ·  ") + ab.Name.ToUpperInvariant(), 10,
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

        void HeroStat(Rect hero, ref float y, string name, string value, float frac, Color c)
        {
            Ui.Label(new Rect(hero.x + 14f, y, 90f, 16f), name.ToUpperInvariant(), 10, Ui.Muted, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(hero.x + 14f, y, hero.width - 28f, 16f), value, 12, Color.white, TextAnchor.MiddleRight, true);
            Ui.Bar(new Rect(hero.x + 14f, y + 18f, hero.width - 28f, 5f), frac, c);
            y += 30f;
        }

        // --- Bottom bar ------------------------------------------------------------

        void DrawLobbyBottom(LobbyEntry me, bool practice)
        {
            Ui.Fill(new Rect(0f, H - 88f, W, 88f), new Color(0.02f, 0.025f, 0.04f, 0.55f));
            Ui.Fill(new Rect(0f, H - 88f, W, 1f), new Color(1f, 1f, 1f, 0.06f));
            if (Ui.Button(new Rect(40f, H - 70f, 150f, 48f), "Leave", Ui.Danger))
                session.Leave("");
            bool ready = me != null && me.Ready;
            float readyX = session.IsHost ? W - 560f : W - 300f;
            var readyRect = new Rect(readyX, H - 70f, 240f, 48f);
            if (ready)
                Ui.PulseRing(readyRect, new Color(1f, 0.85f, 0.48f), Time.unscaledTime);
            if (Ui.Button(readyRect, ready ? "Ready!  (click to cancel)" : "Ready", ready ? Ui.GoldButton : Ui.Primary, PixelArt.Get("ui_check")))
                session.RequestReady(!ready);
            int waiting = 0;  // other players who aren't ready yet
            foreach (var e in session.Lobby.Values)
                if (!e.Ready && e.Id != session.MyId)
                    waiting++;
            // Only with other players: who you're waiting on, or that everyone's ready.
            string note = session.Lobby.Count < 2 ? ""
                : waiting > 0 ? "Waiting for " + waiting + " player" + (waiting == 1 ? "" : "s") + " to ready up"
                : !ready ? "" : session.IsHost ? "Everyone's ready" : "Everyone's ready: waiting for the host to start";
            Ui.Label(new Rect(210f, H - 56f, readyX - 230f, 26f), note, 14, waiting == 0 ? Ui.Good : Ui.Muted, TextAnchor.MiddleRight);
            if (session.IsHost)
            {
                bool wasEnabled = GUI.enabled;
                GUI.enabled = session.AllReady && (!practice || session.Plan.Waves.Count > 0);
                string start = practice ? "Start practice" : session.Lobby.Count == 1 ? "Start solo" : "Start game (" + session.Lobby.Count + " players)";
                if (Ui.Button(new Rect(W - 300f, H - 70f, 260f, 48f), start, practice ? new Color(PracticeColor.r * 0.85f, PracticeColor.g * 0.85f, PracticeColor.b * 0.85f) : Ui.Primary, PixelArt.Get("ui_play")))
                    session.StartMatch();
                GUI.enabled = wasEnabled;
            }
        }
    }
}
