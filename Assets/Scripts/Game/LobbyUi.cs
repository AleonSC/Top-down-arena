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

        int lobbyTab;              // 0 = your loadout, 1 = practice waves
        bool lobbyWasPractice;
        bool connInfoOpen;         // the "!" popup by the title: hosting/port and the invite addresses
        readonly Player previewPlayer = new Player { Id = -100 };

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
            if (!practice)
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
            DrawModeSwitch(new Rect(W / 2f - 230f, 18f, 460f, 58f), t);
            Ui.Label(new Rect(W - 240f, 8f, 200f, 16f), Session.VersionText, 11, Ui.Muted, TextAnchor.MiddleRight);
            DrawBackgroundPicker(W - 40f, 26f);  // MenuBackgrounds.cs

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
            if (session.Status.Length > 0)
                Ui.Label(new Rect(lx, y + 4f, lw, H - 96f - (y + 4f)), session.Status, 12, Ui.Warn, TextAnchor.LowerLeft, false, true);

            // --- Right: tabs, then your loadout or the practice waves.
            var area = new Rect(440f, 100f, W - 480f, H - 196f);
            DrawLobbyTabs(new Rect(area.x, area.y, area.width, 34f), practice);
            var body = new Rect(area.x, area.y + 40f, area.width, area.height - 40f);
            if (lobbyTab == 1 && practice)
                DrawPracticeEditor(body, t);
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
            string[] names = { "Your loadout", practice ? "Practice waves  (" + session.Plan.Waves.Count + ")" : null };
            float x = r.x;
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] == null)
                    continue;
                float w = 30f + names[i].Length * 8.2f;
                var tab = new Rect(x, r.y, w, r.height);
                bool on = lobbyTab == i;
                var c = i == 1 ? PracticeColor : Ui.Accent;
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
                DrawPreviewPlayer(e.Role, e.Kit, e.Weapon, t + slot * 1.7f, false);
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

        /// A player's character for a menu preview, aiming around (and swinging or shooting now and then).
        /// Call between BeginUiGl and EndUiGl, after UiModelAt.
        void DrawPreviewPlayer(Role role, Kit kit, int weapon, float t, bool shots)
        {
            var p = previewPlayer;
            p.Role = role;
            p.Def = Defs.Get(role);
            p.Kit = kit;
            p.Weapon = Defs.Weapon(role, kit, weapon);
            p.Hp = p.MaxHp = 1f;
            p.Disp = p.Pos = V2.Zero;
            p.Aim = -0.5f + 0.9f * Mathf.Sin(t * 0.7f);
            if (shots)
            {
                V2 dir = V2.FromAngle(p.Aim);
                if (p.Weapon.Melee)
                {
                    float f = (t % 1.4f) / 0.3f;  // a swing every 1.4 s
                    if (f < 1f)
                        Arc(V2.Zero, p.Def.BodyR + 22f, 6f * (1f - f), new Rgba(0.85f, 0.92f, 1f, 0.8f * (1f - f)), 16, p.Aim - 0.9f, 1.8f * Mathf.Min(1f, f * 2f));
                }
                else
                    for (int k = 0; k < 3; k++)
                    {
                        float d = p.Def.BodyR + 10f + ((t * 160f + k * 30f) % 90f);
                        Circle(dir * d, 3f, p.Def.Color.WithAlpha(1f - (d - p.Def.BodyR) / 100f), 10);
                    }
            }
            DrawPlayer(p, t, true);
        }

        void DrawLoadout(LobbyEntry me, Rect r, float t)
        {
            Ui.Panel(r);
            var roleColor = ToColor(Defs.Get(me.Role).Color);

            // Hero card: your character on a glowing pedestal, with the numbers that matter.
            var hero = new Rect(r.x + 14f, r.y + 14f, 232f, r.height - 28f);
            Ui.Pill(hero, new Color(0.03f, 0.035f, 0.06f, 0.85f));
            V2 stand = new V2(hero.center.x, hero.y + 108f);
            if (BeginUiGl())
            {
                UiModelAt(stand, 1f);
                for (int k = 0; k < 3; k++)  // pedestal glow
                    Circle(V2.Zero, 78f - k * 16f, new Rgba(roleColor.r, roleColor.g, roleColor.b, 0.07f + 0.04f * k), 48);
                Arc(V2.Zero, 78f, 2f, new Rgba(roleColor.r, roleColor.g, roleColor.b, 0.5f), 64);
                float pulse = (t * 0.6f) % 1f;
                Arc(V2.Zero, 40f + 40f * pulse, 2f, new Rgba(roleColor.r, roleColor.g, roleColor.b, 0.35f * (1f - pulse)), 48);
                UiModelAt(stand, 2.1f);
                DrawPreviewPlayer(me.Role, me.Kit, me.Weapon, t, true);
                EndUiGl();
            }
            float hy = hero.y + 196f;
            Ui.Fancy(new Rect(hero.x, hy, hero.width, 26f), Defs.KitName(me.Kit) + " " + Defs.Get(me.Role).Name, 20, Color.white, Color.Lerp(roleColor, Color.white, 0.3f), TextAnchor.MiddleCenter, roleColor);
            hy += 32f;
            int hp = (int)(Defs.Get(me.Role).Hp * Defs.KitHpMult(me.Kit));
            var wdef = Defs.Weapon(me.Role, me.Kit, me.Weapon);
            previewPlayer.Weapon = wdef;
            float reach = game.WeaponRange(previewPlayer);
            HeroStat(hero, ref hy, "Health", hp + " HP", Mathf.Clamp01(hp / 340f), new Color(0.35f, 0.95f, 0.45f));
            HeroStat(hero, ref hy, "Speed", Mathf.RoundToInt(Defs.Get(me.Role).Speed).ToString(), Mathf.Clamp01(Defs.Get(me.Role).Speed / 300f), new Color(0.45f, 0.75f, 1f));
            HeroStat(hero, ref hy, wdef.Melee ? "Reach" : "Range", Mathf.RoundToInt(reach) + " px", Mathf.Clamp01(reach / 1100f), Ui.Gold);
            hy += 8f;
            var ult = Upgrades.Ability(Upgrades.Ultimate(me.Role, me.Kit));
            Ui.Fill(new Rect(hero.x + 14f, hy, hero.width - 28f, 1f), new Color(1f, 1f, 1f, 0.08f));
            hy += 8f;
            Ui.Label(new Rect(hero.x + 14f, hy, hero.width - 28f, 16f), "ULTIMATE  ·  F", 10, Ui.Muted, TextAnchor.MiddleLeft, true);
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
                var b = new Rect(x + i * (third + gap), y + 28f, third, 48f);
                if (Ui.Choice(b, "", (int)me.Role == i, rc))
                {
                    session.RequestRole((Role)i);
                    hostRole = (Role)i;
                    joinRole = (Role)i;
                    weaponChoice = 0;
                    abilityChoice = 0;
                }
                bool on = (int)me.Role == i;
                Ui.Label(new Rect(b.x, b.y + 5f, b.width, 22f), Defs.Roles[i].Name, 16, on ? Color.white : Color.Lerp(rc, Color.white, 0.3f), TextAnchor.MiddleCenter, true);
                Ui.Label(new Rect(b.x, b.y + 26f, b.width, 16f), (int)(Defs.Roles[i].Hp * Defs.KitHpMult(me.Kit)) + " HP", 11, on ? new Color(1f, 1f, 1f, 0.8f) : Ui.Muted, TextAnchor.MiddleCenter);
            }
            Ui.Label(new Rect(x, y + 80f, w, 18f), Defs.Get(me.Role).Desc, 12, Ui.Muted);
            y += 106f;

            Ui.Heading(x, y, w, "Class");
            float quarter = (w - gap * (Defs.KitCount - 1)) / Defs.KitCount;
            for (int i = 0; i < Defs.KitCount; i++)
                if (Ui.Choice(new Rect(x + i * (quarter + gap), y + 28f, quarter, 36f), Defs.KitName((Kit)i), me.Kit == (Kit)i, roleColor))
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
            for (int i = 0; i < Upgrades.RoleAbilityChoices; i++)
                if (Ui.Choice(new Rect(x + i * (third + gap), y + 28f, third, 36f), Upgrades.Ability(Upgrades.RoleAbility(me.Role, me.Kit, i)).Name, me.Ability == i, roleColor))
                {
                    session.RequestAbility(i);
                    abilityChoice = i;
                }
            var ability = Upgrades.Ability(Upgrades.RoleAbility(me.Role, me.Kit, me.Ability));
            Ui.Label(new Rect(x, y + 68f, w, 34f), ability.Desc + "  (" + ability.Cooldown.ToString("0.#") + " s cooldown)", 12, Ui.Muted, TextAnchor.UpperLeft, false, true);
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
            if (Ui.Button(new Rect(readyX, H - 70f, 240f, 48f), ready ? "Ready!  (click to cancel)" : "Ready", ready ? Ui.GoldButton : Ui.Primary))
                session.RequestReady(!ready);
            int waiting = 0;  // other players who aren't ready yet
            foreach (var e in session.Lobby.Values)
                if (!e.Ready && e.Id != session.MyId)
                    waiting++;
            // Only with other players: who you're waiting on, or that everyone's ready.
            string note = session.Lobby.Count < 2 ? ""
                : waiting > 0 ? "Waiting for " + waiting + " player" + (waiting == 1 ? "" : "s") + " to ready up"
                : !ready ? "" : session.IsHost ? "Everyone's ready" : "Everyone's ready: waiting for the host to start";
            Ui.Label(new Rect(210f, H - 78f, readyX - 230f, 26f), practice ? "PRACTICE RANGE" : "RUN", 11, practice ? PracticeColor : Ui.Accent, TextAnchor.MiddleRight, true);
            Ui.Label(new Rect(210f, H - 56f, readyX - 230f, 26f), note, 14, waiting == 0 ? Ui.Good : Ui.Muted, TextAnchor.MiddleRight);
            if (session.IsHost)
            {
                bool wasEnabled = GUI.enabled;
                GUI.enabled = session.AllReady && (!practice || session.Plan.Waves.Count > 0);
                string start = practice ? "Start practice" : session.Lobby.Count == 1 ? "Start solo" : "Start game (" + session.Lobby.Count + " players)";
                if (Ui.Button(new Rect(W - 300f, H - 70f, 260f, 48f), start, practice ? new Color(PracticeColor.r * 0.85f, PracticeColor.g * 0.85f, PracticeColor.b * 0.85f) : Ui.Primary))
                    session.StartMatch();
                GUI.enabled = wasEnabled;
            }
        }
    }
}
