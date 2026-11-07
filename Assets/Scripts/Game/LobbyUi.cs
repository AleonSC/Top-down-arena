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

        int lobbyTab;              // 0 = your loadout, 1 = practice waves, 2 = modifiers, 3 = difficulty, 4 = boss hunt, 5 = perks, 6 = hangout
        bool lobbyWasPractice, lobbyWasHunt;
        bool connInfoOpen;         // the "!" popup by the title: hosting/port and the invite addresses
        readonly Player previewPlayer = new Player { Id = -100 };

        // The hero card's live mini-arena (Showcase.cs): the real game, offline, using your weapon on target
        // dummies; it casts the ability or ultimate you last clicked (`detailSel`) until you click something else. Hovering shows nothing.
        Showcase showcase;
        float showcaseZoomA;       // the zoom meter's fade (0 hidden .. 1 shown)
        bool showcaseZoomDrag;
        float showcaseZoom = 1f;   // the preview's zoom meter (0.5 out .. 2 in), set by the slider under it or the mouse wheel over it
        float showcaseSeen;        // when the hero card last drew it (it's stopped once nobody looks)

        /// The lobby's content used to sit 14 px higher than its layout numbers said. The softer layout (Docs/Mockups/crt-lobby-softer.html) runs 28..684
        /// with a 36 px margin and needs no lift; the group stays so DrawLobby's click handling is unchanged.
        const float LobbyLift = 0f;

        void DrawLobby()
        {
            GUI.BeginGroup(new Rect(0f, -LobbyLift, W, H));
            var oldShift = uiShift;
            uiShift = new V2(oldShift.X, oldShift.Y - LobbyLift);  // its live models (GL) move with it
            DrawLobbyContent();
            uiShift = oldShift;
            GUI.EndGroup();
        }

        void DrawLobbyContent()
        {
            float t = Time.unscaledTime;
            bool calm = fxReduced;
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
            // The big boxes wear the shown role's colour (the bot's while the host is choosing its loadout), easing to a new role over about 0.7 s.
            LobbyEntry washFor = me;
            var washBot = session.IsHost ? partyBots.Find(editBotId) : null;
            LobbyEntry washBotEntry;
            if (washBot != null && session.Lobby.TryGetValue(washBot.Id, out washBotEntry))
                washFor = washBotEntry;
            if (washFor != null)
                LobbyKit.WashTo(ToColor(Defs.Get(washFor.Role).Color), calm);
            var infoButton = LobbyInfoKey;  // the connection key: hidden until the mouse is over it (or its popup is open)
            var infoPanel = ConnInfoRect(infoButton);
            if (connInfoOpen && Event.current.type == EventType.MouseDown && !infoButton.Contains(Event.current.mousePosition))
            {
                // The popup is drawn last, so eat clicks on it before the keys under it see them; a click outside closes it.
                if (!infoPanel.Contains(Event.current.mousePosition))
                    connInfoOpen = false;
                Event.current.Use();
            }

            if (ascOpen >= 0 && Event.current.type == EventType.MouseDown)
            {
                ascOpen = -1;  // the ascension window closes on any click (and the click goes no further)
                Event.current.Use();
            }

            // --- Left column, top: Back, LOBBY_ and the "!" key (Leaving, Settings and the Field guide are in the Esc menu, MenuDrawer.cs),
            // then the mode keys (a choice for the whole lobby, so they sit with the party).
            if (LobbyKit.Key(LobbyBackKey, "", false, calm, PixelArt.Get("ui_back"), size: 22))
                StartLobbyLeave();  // LobbyEnter.cs
            DrawLobbyTitle(LobbyTitleRect, 1f, calm, t);
            bool infoShown = connInfoOpen || (GUI.enabled && infoButton.Contains(Event.current.mousePosition));
            if (infoShown && LobbyKit.Key(infoButton, "!", connInfoOpen, calm, size: 22))
                connInfoOpen = !connInfoOpen;
            var modeR = LobbyModeRect;
            TipClear();  // the mode keys' hover tips (below) and a rank badge's title (RankUi.cs)
            if (session.Resume != null)
                DrawResumeBanner(modeR, t);  // continuing a saved run (SavesUi.cs)
            else
                DrawModeSwitch(modeR, t);

            // --- Left: the party, under the mode keys.
            const float lx = PartyX, lw = PartyW;
            LobbyKit.Spaced(LobbyPartyLabel, "PARTY (" + session.Lobby.Count + "/" + Session.MaxPlayers + ")", 11, LobbyKit.HeadCol, TextAnchor.MiddleLeft, 1.56f);  // small teal caps, like the other headings
            // The way into the Hangout (LobbyRoom.cs): a key on the Party line, which flips to "Loadout" while you're in the room.
            bool inRoom = lobbyTab == HangoutTab;
            if (LobbyKit.Key(LobbyHangoutKey, inRoom ? "< LOADOUT" : "HANGOUT >", inRoom, calm, size: 13, minSize: 10))
                lobbyTab = inRoom ? (practice ? 1 : 0) : HangoutTab;
            float y = PartyTop;
            int slot = 0;
            foreach (var e in session.Lobby.Values)
            {
                float ch = slot == 0 ? PartyCardH : PartySlotH;  // the first card is a little taller (the mockup's 84 and 80)
                DrawPartyCard(new Rect(lx, y, lw, ch), e, t, slot++);
                y += ch + PartyGap;
            }
            for (; slot < Session.MaxPlayers; slot++)
            {
                DrawOpenSlot(new Rect(lx, y, lw, PartySlotH), slot, t);
                y += PartySlotH + PartyGap;
            }
            if (session.Status.Length > 0)
                Ui.Label(new Rect(lx, y - 4f, lw, LobbyNoteY - (y - 4f)), session.Status, 12, Ui.Warn, TextAnchor.LowerLeft, false, true);

            // --- Right: the tab keys, then your loadout or the practice waves.
            DrawLobbyTabs(LobbyTabsRect, practice);  // centred over the loadout box (LobbyLayout.cs)
            var body = LobbyBodyRect;
            TickHangoutAnim();  // LobbyRoom.cs
            if (hangoutAnim > 0f && hangoutAnim < 1f && me != null && session.Resume == null)
                DrawHangoutTransition(me, body, t);  // the preview window growing into the Hangout (or shrinking back)
            else if (lobbyTab == 1 && practice)
                DrawPracticeEditor(body, t);
            else if (lobbyTab == 4 && hunt)
                DrawHuntPicker(body, t);  // HuntUi.cs
            else if (lobbyTab == 2)
                DrawModifiers(body, t);
            else if (lobbyTab == 3)
                DrawDifficulty(body, t);  // DifficultyUi.cs
            else if (lobbyTab == PerkTab && !practice)
                DrawPerks(body, t);  // PerksUi.cs
            else if (lobbyTab == HangoutTab)
                DrawHangout(body, t);  // LobbyRoom.cs
            else if (session.Resume != null)
                DrawResumeSeats(body, t);  // SavesUi.cs
            else if (me != null)
            {
                var editing = session.IsHost ? partyBots.Find(editBotId) : null;
                LobbyEntry shown;
                if (editing != null && session.Lobby.TryGetValue(editing.Id, out shown))
                    DrawLoadout(shown, body, t, editing);  // the host choosing a bot's loadout
                else
                {
                    editBotId = -1;
                    DrawLoadout(me, body, t);
                }
            }

            DrawLobbyActions(me, practice);
            DrawLockedTip();  // on top of the loadout
            DrawTip();
            if (connInfoOpen)
                DrawConnInfo(infoPanel);
        }

        // The screen's layout (every box round the edges, and the bands the entrances cut it into) is in LobbyLayout.cs.

        /// The lobby's title: a small letter-spaced label (16 px, .3 em) with a faint rule trailing off to the right (the mockup's slim header,
        /// Docs/Mockups/crt-lobby-headers.html option 2). `a` fades it (the entrance).
        void DrawLobbyTitle(Rect r, float a, bool calm, float t)
        {
            var ink = new Color(0.56f, 0.71f, 1f, a);
            const int size = 16;
            const float sp = 4.8f;
            float tw = LobbyKit.SpacedWidth("LOBBY", size, sp);
            LobbyKit.Spaced(new Rect(r.x, r.y, tw + 10f, r.height), "LOBBY", size, ink, TextAnchor.MiddleLeft, sp);
            float x0 = r.x + tw + 16f, x1 = PartyX + PartyW - 50f;  // the rule runs on to the connection key's place, fading out
            const int steps = 14;
            for (int i = 0; i < steps && x1 > x0; i++)
                Ui.Fill(new Rect(x0 + (x1 - x0) * i / steps, Mathf.Round(r.center.y), (x1 - x0) / steps + 1f, 1f), new Color(CrtUi.Ink.r, CrtUi.Ink.g, CrtUi.Ink.b, 0.5f * (1f - i / (float)steps) * a));
        }

        string HostName()
        {
            LobbyEntry host;
            return session.Lobby.TryGetValue(1, out host) ? host.Name : "the host";
        }

        /// Three keys: a normal run, a Boss Hunt, or the Practice Range, each with its description as a hover tip. Only the host can switch.
        void DrawModeSwitch(Rect r, float t)
        {
            bool practice = session.PracticeMode;
            int mode = practice ? 2 : session.HuntMode ? 1 : 0;
            float kw = (r.width - 12f) / 3f;
            for (int i = 0; i < 3; i++)
            {
                var k = new Rect(r.x + i * (kw + 6f), r.y, kw, r.height);
                string title = i == 0 ? "GAUNTLET" : i == 1 ? "BOSS HUNT" : "PRACTICE";
                string sub = i == 0 ? "50 waves, a boss every 5" : i == 1 ? "10 waves to one boss" : "Build your own waves";
                if (LobbyKit.Key(k, title, i == mode, fxReduced, size: 15, minSize: 11, enabled: session.IsHost, accent: i == 1 ? new Color(1f, 0.62f, 0.58f) : i == 2 ? new Color(0.55f, 0.95f, 0.8f) : (Color?)null) && session.IsHost && i != mode)
                    session.SetMode(i == 2, i == 1);
                if (k.Contains(Event.current.mousePosition))  // the description is a hover tip
                {
                    TipStart(k, title, CrtUi.Ink);
                    TipAdd(sub, 13, Ui.Text);
                    if (!session.IsHost)
                        TipAdd("Only the host can switch the mode.", 11, Ui.Muted);
                }
            }
        }

        /// The tab keys across the top of the big screen: the same order in every mode (the shared tabs, then the mode's own at the end).
        void DrawLobbyTabs(Rect r, bool practice)
        {
            int mods = Modifiers.Count(session.Modifiers);
            bool hunt = session.HuntMode && !practice && session.Resume == null;
            // Indexed by tab id (0 loadout, 1 practice, 2 modifiers, 3 difficulty, 4 boss hunt, 5 perks, 6 hangout); null = not shown.
            string[] names = { session.Resume != null ? "Seats" : "Loadout", practice ? "Practice waves  (" + session.Plan.Waves.Count + ")" : null, mods > 0 ? "Modifiers  (" + mods + ")" : "Modifiers",
                "Difficulty", hunt ? "Boss hunt" : null, practice ? null : "Perks", null };  // Hangout has its own key on the Party line
            int[] order = { 0, PerkTab, 3, 2, 1, 4 };
            // Quiet tabs (the mockup's): about 135 px each (wider for a long name), the row centred in r (over the loadout box); too many squeeze together.
            var ws = new float[order.Length];
            float total = -6f;
            for (int k = 0; k < order.Length; k++)
            {
                if (names[order[k]] == null)
                    continue;
                ws[k] = Mathf.Max(135.5f, LobbyKit.SpacedWidth(names[order[k]].ToUpperInvariant(), 12, 1.44f) + 32f);
                total += ws[k] + 6f;
            }
            float shrink = Mathf.Min(1f, r.width / total), x = r.center.x - total * shrink / 2f;
            for (int k = 0; k < order.Length; k++)
            {
                int i = order[k];
                if (names[i] == null)
                    continue;
                bool on = lobbyTab == i;
                if (LobbyKit.QuietTab(new Rect(x, r.y, ws[k] * shrink, r.height), names[i].ToUpperInvariant(), on) && !on)
                    lobbyTab = i;
                x += (ws[k] + 6f) * shrink;
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

        readonly PartyBots partyBots = new PartyBots();  // bots in the open slots (PartyBots.cs)
        string botNameText = "";  // the name being typed for the chosen bot
        int botNameFor = -1;
        int editBotId = -1;  // the bot whose loadout the host is choosing (click its card); -1 = your own

        /// A player's card on the screen: today's card art (the role's rim or their earned frame, the synthwave or chosen background,
        /// the live portrait, the Host / You / Ready tags) framed on the phosphor screen.
        void DrawPartyCard(Rect r, LobbyEntry e, float t, int slot)
        {
            var botSeat = session.IsHost ? partyBots.Find(e.Id) : null;
            var color = ToColor(Defs.Get(e.Role).Color);
            bool mine = e.Id == session.MyId;
            if (e.Frame > 0)  // a frame they've earned (Rank.cs): a lit rim in its colour, in place of the role's
            {
                DrawFrameCard(r, e.Frame, t, true, e.CardBg);  // FrameLooks.cs
            }
            else
            {
                Ui.Card(r, color, mine ? 0.85f : 0.3f);
                DrawCardBackground(r, e.CardBg, t);  // CardBackgrounds.cs
            }
            // Portrait: their character, aiming around.
            float fs = r.height >= PartyCardH ? 64f : 62f;
            var face = new Rect(r.x + 8f, r.y + (r.height - fs) / 2f, fs, fs);
            Ui.Pill(face, new Color(color.r * 0.25f, color.g * 0.25f, color.b * 0.25f, 0.9f));
            if (BeginUiGl())
            {
                UiModelAt(new V2(face.center.x, face.center.y), 0.76f);
                DrawPreviewPlayer(e.Role, e.Kit, e.Weapon, t + slot * 1.7f);
                EndUiGl();
            }
            var rankRect = new Rect(face.xMax - 17f, face.yMax - 17f, 24f, 24f);
            RankLook.Draw(rankRect, e.Rank);
            RankBadgeTip(rankRect, e.Rank, mine);
            float tx = r.x + 84f;
            // The tags sit at the top right (10 px caps); work out where they start so the name stops short of them.
            const float tagH = 16f, tagPad = 14f;
            float youW = Ui.TextWidth("YOU", 10, true) - 6f + tagPad, hostW = Ui.TextWidth("HOST", 10, true) - 6f + tagPad;
            float bx = r.xMax - 9f;
            if (botSeat != null)
                bx -= 118f;  // the bot's keys (below)
            float badgesLeft = bx;
            if (mine)
                badgesLeft -= youW + 6f;
            if (e.Id == 1)
                badgesLeft -= hostW + 6f;
            DrawNameLine(new Rect(tx, r.y + 6f, badgesLeft - 4f - tx, 24f), e.Name, 18, 12, e.NameFx, t);  // (the name effect, if they have one: NameEffects.cs)
            float gx = r.xMax - 9f;
            if (mine)
            {
                gx -= youW;
                LobbyTag(new Rect(gx, r.y + 9f, youW, tagH), "YOU", new Color(0.184f, 0.29f, 0.65f), new Color(0.81f, 0.88f, 1f));
                gx -= 6f;
            }
            if (e.Id == 1)
            {
                gx -= hostW;
                LobbyTag(new Rect(gx, r.y + 9f, hostW, tagH), "HOST", new Color(0.54f, 0.416f, 0.122f), new Color(1f, 0.88f, 0.54f));
            }
            DrawOneLine(new Rect(tx, r.y + 30f, r.xMax - tx - 9f - e.PerkIds.Length * 19f, 20f), Defs.KitName(e.Kit) + " " + Defs.Get(e.Role).Name, 14, 10, Color.Lerp(color, Color.white, 0.15f), true);
            DrawOneLine(new Rect(tx, r.y + 52f, r.xMax - tx - 84f, 16f),
                Defs.Weapon(e.Role, e.Kit, e.Weapon).Name + "  ·  " + Upgrades.Ability(Upgrades.RoleAbility(e.Role, e.Kit, e.Ability)).Name, 12, 9, new Color(0.78f, 0.82f, 0.94f), false);
            DrawSeatPerks(r, e);  // their perks (PerksUi.cs)
            if (session.IsHost && (botSeat != null || mine))  // click a card to choose that player's loadout: yours, or a bot's
            {
                bool picked = botSeat != null ? editBotId == e.Id : editBotId < 0;
                if (picked && partyBots.Bots.Count > 0)
                    Ui.Rim(r, Ui.Gold, 0.9f);
                float room = botSeat != null ? 118f : 0f;
                if (Ui.HitArea(new Rect(r.x, r.y, r.width - 9f - room, r.height)))
                    editBotId = botSeat != null ? e.Id : -1;
            }
            if (botSeat != null)
                DrawBotButtons(r, botSeat);
            // READY / NOT READY, bottom right.
            string rt = e.Ready ? "READY" : "NOT READY";
            float rw = Ui.TextWidth(rt, 10, true) - 6f + tagPad;
            var ready = new Rect(r.xMax - 9f - rw, r.yMax - 7f - tagH, rw, tagH);
            if (e.Ready)
                Ui.Pill(new Rect(ready.x - 3f, ready.y - 3f, ready.width + 6f, ready.height + 6f), new Color(0.3f, 1f, 0.45f, 0.18f + (fxReduced ? 0f : 0.1f * Mathf.Sin(t * 4f))));
            LobbyTag(ready, rt, e.Ready ? new Color(0.075f, 0.36f, 0.204f) : new Color(0.04f, 0.04f, 0.12f, 0.75f), e.Ready ? new Color(0.72f, 1f, 0.83f) : new Color(0.81f, 0.84f, 0.96f),
                e.Ready ? new Color(0.24f, 0.86f, 0.52f) : new Color(0.353f, 0.373f, 0.541f));
        }

        /// A party card's small tag (10 px bold caps on a flat fill, with an optional 1 px rim).
        static void LobbyTag(Rect r, string text, Color fill, Color ink, Color? rim = null)
        {
            Ui.Fill(r, fill);
            if (rim.HasValue)
                CrtKit.Frame(r, 1f, rim.Value);
            LobbyLabel(r, text, ink);
        }

        static void LobbyLabel(Rect r, string text, Color ink) => Ui.Label(new Rect(r.x, r.y, r.width, r.height), text, 10, ink, TextAnchor.MiddleCenter, true);

        /// A bot's keys on its party card (the host's): its skill (click to change), a new random loadout, and remove.
        void DrawBotButtons(Rect r, PartyBots.PartyBot bot)
        {
            bool calm = fxReduced;
            float x = r.xMax - 9f - 22f;
            if (LobbyKit.Key(new Rect(x, r.y + 8f, 22f, 20f), "x", false, calm, size: 13, minSize: 9, danger: true))
            {
                if (editBotId == bot.Id)
                    editBotId = -1;
                partyBots.Remove(bot);
                return;
            }
            x -= 38f;
            if (LobbyKit.Key(new Rect(x, r.y + 8f, 34f, 20f), "MIX", false, calm, size: 11, minSize: 8))
                partyBots.Shuffle(bot);  // a new random loadout
            x -= 62f;
            if (LobbyKit.Key(new Rect(x, r.y + 8f, 58f, 20f), bot.Skill.ToString().ToUpper(), false, calm, size: 11, minSize: 8))
                partyBots.SetSkill(bot, bot.Skill == BotSkill.Casual ? BotSkill.Good : bot.Skill == BotSkill.Good ? BotSkill.Expert : BotSkill.Casual);
        }

        /// An empty seat: a dashed frame, "OPEN SLOT" and (for the host, while there is room) the "+ ADD A BOT" key.
        void DrawOpenSlot(Rect r, int slot, float t)
        {
            bool calm = fxReduced;
            Ui.Fill(r, LobbyKit.Ink(0.015f));
            CrtKit.Dashed(r, 1f, LobbyKit.Ink(0.2f));  // faded: an empty seat should sit back
            bool canAdd = partyBots.CanAdd(session);
            LobbyKit.Spaced(new Rect(r.x, r.y + (canAdd ? 8f : 0f), r.width, canAdd ? 26f : r.height), "OPEN SLOT", 14, new Color(CrtUi.Dim.r, CrtUi.Dim.g, CrtUi.Dim.b, 0.5f), TextAnchor.MiddleCenter, 1.7f);
            if (canAdd && LobbyKit.Key(new Rect(r.center.x - 75f, r.y + 40f, 150f, 30f), "+ ADD A BOT", false, calm, size: 13, minSize: 10))
                partyBots.Add(session);  // a bot takes the slot and plays with you
        }

        string[] InviteAddresses() => (addressList ?? "").Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);

        static float AddressWidth(string addr) => 18f + addr.Length * 9f;

        /// Where the "!" popup goes: just under its key, as tall as its contents.
        Rect ConnInfoRect(Rect button)
        {
            const float w = PartyW, pad = 14f;
            float h = pad + 34f + 24f;
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
                h += 24f + rows * 32f + 20f;
            }
            else
                h += 22f;
            return new Rect(PartyX, button.yMax + 8f, w, h + pad);
        }

        /// The "!" popup: who's hosting and on which port, and the addresses friends join with.
        void DrawConnInfo(Rect r)
        {
            bool calm = fxReduced;
            Ui.Fill(r, new Color(CrtUi.Bg.r, CrtUi.Bg.g, CrtUi.Bg.b, 0.98f));  // solid backing over the party
            LobbyKit.Panel(r, false, false);
            CrtKit.Frame(r, 2f, CrtUi.Ink);
            float x = r.x + 14f, w = r.width - 28f, y = r.y + 12f;
            LobbyKit.Heading(x, y, w, session.IsHost ? "Invite friends" : "Connection", null, calm);
            y += 38f;
            Ui.Label(new Rect(x, y, w, 20f), session.IsHost ? "You're hosting  ·  port " + session.Port : "Connected to " + HostName(), 14, new Color(0.9f, 0.93f, 1f), TextAnchor.MiddleLeft, true);
            y += 24f;
            if (session.IsHost)
            {
                Ui.Label(new Rect(x, y, w, 20f), "They join with port " + session.Port + " and one of:", 13, CrtUi.Dim, TextAnchor.MiddleLeft, true);
                y += 26f;
                float bx = x;
                foreach (var addr in InviteAddresses())
                {
                    float bw = AddressWidth(addr);
                    if (bx > x && bx + bw > x + w)
                    {
                        bx = x;
                        y += 32f;
                    }
                    var tag = new Rect(bx, y, bw, 26f);
                    bool ts = addr.StartsWith("100.");  // a Tailscale address: lit
                    Ui.Fill(tag, ts ? new Color(CrtUi.Ink.r, CrtUi.Ink.g, CrtUi.Ink.b, 0.95f) : LobbyKit.Ink(0.08f));
                    CrtKit.Frame(tag, 1f, CrtUi.Ink);
                    Ui.Label(tag, addr, 13, ts ? CrtUi.Bg : CrtUi.Ink, TextAnchor.MiddleCenter, true);
                    bx += bw + 6f;
                }
                y += 32f;
                Ui.Label(new Rect(x, y, w, 16f), "Filled = Tailscale address", 12, CrtUi.Dim, TextAnchor.MiddleLeft, true);
            }
            else
                Ui.Label(new Rect(x, y, w, 20f), "Connected. Pick your loadout and press Ready.", 14, CrtUi.Ink, TextAnchor.MiddleLeft, true);
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

        static readonly string[] RoleIcons = { "role_tank", "role_healer", "role_dps", "role_dps" };  // (Wildcard borrows the DPS icon for now)  // PixelArt.cs

        int ascOpen = -1;   // the weapon slot whose ascension window is open (-1 = closed; any click closes it)
        int detailSel;      // what the detail box shows: 0 = weapon, 1 = ability, 2 = ultimate (whichever was clicked last)

        /// The big loadout screen (Docs/Mockups/lobby-declutter.html). Left: the live preview filling the column, the role / class
        /// title and the glowing stat bars. Right: Role, Class, Weapon (each with its ascension button), Ability and Ultimate keys,
        /// then one detail box for whichever weapon, ability or ultimate was clicked last.
        void DrawLoadout(LobbyEntry me, Rect r, float t, PartyBots.PartyBot bot = null)
        {
            Session tgt = bot != null ? bot.S : session;  // whose loadout the keys change: yours, or a bot's (its own session asks the host)
            bool self = bot == null;
            bool calm = fxReduced;
            LobbyKit.Panel(r);  // (the role wash)
            var roleColor = ToColor(Defs.Get(me.Role).Color);

            // Left column: the live mini-arena (your weapon on target dummies; it casts an ability while you hover one).
            float lx = r.x + 16f;
            const float lw = 300f;
            var view = ShowcaseRect(r);
            DrawShowcase(me, view, roleColor);
            DrawShowcaseZoom(view);  // (outside DrawShowcase: that returns early on every event but Repaint, and the slider needs the mouse events)
            if (!self)
            {
                // The bot's name: type to rename it (up to 16 letters).
                var nameBox = new Rect(view.x + 70f, view.y + 24f, view.width - 78f, 22f);
                Ui.Fill(new Rect(view.x + 4f, view.y + 24f, view.width - 8f, 22f), new Color(0f, 0f, 0f, 0.6f));
                Ui.Label(new Rect(view.x + 6f, view.y + 24f, 62f, 22f), "BOT NAME", 11, LobbyKit.Gold, TextAnchor.MiddleLeft, true);
                Ui.Fill(nameBox, new Color(1f, 1f, 1f, 0.08f));
                if (botNameFor != bot.Id)
                {
                    botNameFor = bot.Id;
                    botNameText = bot.Name;
                }
                botNameText = Ui.BareField(new Rect(nameBox.x + 4f, nameBox.y, nameBox.width - 8f, nameBox.height), botNameText, 16, 14);
                if (botNameText.Trim().Length > 0 && Session.CleanName(botNameText) != bot.Name)
                    partyBots.Rename(session, bot, botNameText);  // (the box keeps what you typed, spaces and all)
            }  // (LobbyRoom.cs: the Hangout grows out of this window)
            // The role / class title (look B: the role small over the class), then the stats.
            DrawRoleClassTitle(new Rect(lx, view.yMax + 8f, lw, 58f), me, roleColor, calm);
            int hp = (int)(Defs.Get(me.Role).Hp * Defs.KitHpMult(me.Kit));
            var wdef = Defs.Weapon(me.Role, me.Kit, me.Weapon);
            previewPlayer.Role = me.Role;  // (set here too: the party cards' live models set it, but they can be paused)
            previewPlayer.Def = Defs.Get(me.Role);
            previewPlayer.Kit = me.Kit;
            previewPlayer.Weapon = wdef;
            float reach = game.WeaponRange(previewPlayer);
            float sy = view.yMax + 72f;
            LobbyKit.StatBar(new Rect(lx, sy, lw, 22f), "Health", hp + " HP", Mathf.Clamp01(hp / 340f), new Color(0.3f, 1f, 0.48f), calm);
            float speed = Defs.Get(me.Role).Speed * Defs.KitSpeedMult(me.Kit);
            LobbyKit.StatBar(new Rect(lx, sy + 26f, lw, 22f), "Speed", Mathf.RoundToInt(speed).ToString(), Mathf.Clamp01(speed / 300f), new Color(0.31f, 0.65f, 1f), calm);
            LobbyKit.StatBar(new Rect(lx, sy + 52f, lw, 22f), wdef.Melee ? "Reach" : "Range", Mathf.RoundToInt(reach) + " px", Mathf.Clamp01(reach / 1100f), new Color(1f, 0.76f, 0.28f), calm);
            // The weapon's damage a second with no upgrades (measured in the sim: Defs.WeaponDps), against the strongest weapon.
            float dps = Defs.WeaponDps(wdef.Id);
            LobbyKit.StatBar(new Rect(lx, sy + 78f, lw, 22f), "Damage", dps > 0f ? Mathf.RoundToInt(dps) + " / s" : Mathf.RoundToInt(wdef.Damage) + (wdef.Melee ? " a swing" : " a hit"),
                dps > 0f ? Mathf.Clamp01(dps / MaxWeaponDps) : Mathf.Clamp01(wdef.Damage / 80f), new Color(1f, 0.36f, 0.36f), calm);
            var ult = Upgrades.Ability(Upgrades.Ultimate(me.Role, me.UltKit));

            // Right column: role, class, weapon and ability, the ultimate, then one detail box.
            float x = r.x + 334f, w = 454f, y = r.y + 16f;
            const float gap = 6f;
            float quarter = (w - gap * 3f) / 4f;  // roles, classes and ultimates: 4 per row
            LobbyKit.Heading(x, y, w, "Role", null, calm, 15);
            // Every row shows a few locked slots for content still to come (hover one: "Coming soon").
            int roleSlots = Mathf.Max(RoleSlots, Defs.Roles.Length);
            for (int i = 0; i < roleSlots; i++)
            {
                var b = new Rect(x + i * (quarter + gap), y + 32f, quarter, 34f);
                if (i >= Defs.Roles.Length)
                {
                    LockedSlot(b);
                    continue;
                }
                if ((Role)i == Role.Wildcard && !WildcardOpen)
                {
                    LockedSlot(b, WildcardLockText);  // Practice Range too: the role can't be picked until it's unlocked
                    continue;
                }
                if (b.Contains(Event.current.mousePosition))  // the description is a hover tip
                {
                    TipStart(b, Defs.Roles[i].Name, ToColor(Defs.Roles[i].Color));
                    TipAdd(Defs.Roles[i].Desc, 13, Ui.Text);
                }
                if (LobbyKit.Key(b, Defs.Roles[i].Name.ToUpperInvariant(), (int)me.Role == i, calm, PixelArt.Get(RoleIcons[i]), colorIcon: true, size: 17, minSize: 11, accent: ToColor(Defs.Roles[i].Color)))
                {
                    tgt.RequestRole((Role)i);
                    if (self)
                    {
                        hostRole = (Role)i;
                        joinRole = (Role)i;
                        weaponChoice = 0;
                        abilityChoice = 0;
                        wildSecond = 1;
                        wildKey = 1;
                        wildTab = 0;
                    }
                }
            }
            y += 76f;
            float ultY;
            if (me.Role == Role.Wildcard)
            {
                ultY = r.y + 16f + 322f;
                DrawWildcardPicks(me, tgt, self, x, y, w, ultY - 6f);
            }
            else
            {
                // Classes: rows of 4, the icon above the name so long names fit.
                LobbyKit.Heading(x, y, w, "Class", null, calm, 15);
                int classSlots = Mathf.Max(ClassSlots, Defs.KitCount);
                for (int i = 0; i < classSlots; i++)
                {
                    var b = new Rect(x + (i % 4) * (quarter + gap), y + 32f + (i / 4) * 40f, quarter, 36f);
                    if (i >= Defs.KitCount)
                    {
                        LockedSlot(b, null, true);
                        continue;
                    }
                    if (b.Contains(Event.current.mousePosition))
                    {
                        TipStart(b, Defs.KitName((Kit)i), Ui.Text);
                        TipAdd(KitBlurb((Kit)i), 13, Ui.Text);
                    }
                    if (LobbyKit.Key(b, Defs.KitName((Kit)i).ToUpperInvariant(), me.Kit == (Kit)i, calm, PixelArt.Get(Classes.Get((Kit)i).Icon), stack: true, colorIcon: true, size: 13, minSize: 9))
                    {
                        tgt.RequestKit((Kit)i);
                        if (self)
                        {
                            kitChoice = (Kit)i;
                            weaponChoice = 0;
                            abilityChoice = 0;
                        }
                    }
                }
                y += 32f + ((classSlots + 3) / 4) * 40f - 4f + 10f;

                // Weapon and ability: side by side, each a 2 x 2 grid. Each weapon key gives its right quarter to an ascension button.
                const float col = 220f;
                float ax = x + w - col, cell = (col - gap) / 2f;
                LobbyKit.Heading(x, y, col, "Weapon", null, calm, 15);
                LobbyKit.Heading(ax, y, col, "Ability", null, calm, 15);
                int weaponSlots = Mathf.Max(WeaponSlots, Defs.WeaponsPerRole);
                for (int i = 0; i < weaponSlots; i++)
                {
                    var b = new Rect(x + (i % 2) * (cell + gap), y + 32f + (i / 2) * 40f, cell, 34f);
                    if (i >= Defs.WeaponsPerRole)
                    {
                        LockedSlot(b, null, true);
                        continue;
                    }
                    var main = new Rect(b.x, b.y, Mathf.Round(b.width * 0.74f), b.height);
                    if (LobbyKit.Key(main, Defs.Weapon(me.Role, me.Kit, i).Name.ToUpperInvariant(), me.Weapon == i, calm, size: 14, minSize: 9))
                    {
                        tgt.RequestWeapon(i);
                        if (self)
                            weaponChoice = i;
                        detailSel = 0;
                    }
                    if (AscensionKey(new Rect(main.xMax - 2f, b.y, b.xMax - (main.xMax - 2f), b.height), me.Weapon == i, calm))
                        ascOpen = i;
                }
                int abilitySlots = Mathf.Max(AbilitySlots, Upgrades.RoleAbilityChoices);
                for (int i = 0; i < abilitySlots; i++)
                {
                    var b = new Rect(ax + (i % 2) * (cell + gap), y + 32f + (i / 2) * 40f, cell, 34f);
                    if (i >= Upgrades.RoleAbilityChoices)
                    {
                        LockedSlot(b, null, true);
                        continue;
                    }
                    if (LobbyKit.Key(b, Upgrades.Ability(Upgrades.RoleAbility(me.Role, me.Kit, i)).Name.ToUpperInvariant(), me.Ability == i, calm, size: 14, minSize: 9))
                    {
                        tgt.RequestAbility(i);
                        if (self)
                            abilityChoice = i;
                        detailSel = 1;
                    }
                }
                y += 32f + ((Mathf.Max(weaponSlots, abilitySlots) + 1) / 2) * 40f - 6f + 10f;
                ultY = y;
            }

            ultY = Mathf.Max(ultY, view.yMax - 72f);  // the Ultimate keys end on the line the preview ends on
            // The ultimate: its key, then locked slots for ultimates still to come. Click it to watch it in the preview.
            LobbyKit.Heading(x, ultY, w, "Ultimate", null, calm, 15);
            for (int i = 0; i < 4; i++)
            {
                var b = new Rect(x + i * (quarter + gap), ultY + 32f, quarter, 40f);
                if (i > 0)
                {
                    LockedSlot(b);
                    continue;
                }
                if (LobbyKit.Key(b, ult.Name.ToUpperInvariant(), true, calm, size: 15, minSize: 9))
                {
                    detailSel = 2;  // (the preview then casts it until you click something else)
                }
            }

            float dy = ultY + 82f;
            var ability = Upgrades.Ability(Upgrades.RoleAbility(me.Role, me.Kit, me.Ability));
            DrawDetailBox(new Rect(x, dy, w, r.yMax - 12f - dy), wdef, ability, ult);

            if (ascOpen >= 0)
            {
                lockedTipOn = false;  // (the keys under the window must not show their tips)
                DrawAscensionWindow(r, Defs.Weapon(me.Role, me.Kit, ascOpen).Name, calm);
            }
        }

        /// The role and class under the preview, look B of the mockup: the role small in its colour with a fading rule, the class
        /// name big under it, a short glowing rule below. A Wildcard has no class: its name is the big line.
        void DrawRoleClassTitle(Rect r, LobbyEntry me, Color roleColor, bool calm)
        {
            string role = Defs.Get(me.Role).Name.ToUpperInvariant();
            bool wild = me.Role == Role.Wildcard;
            string eyebrow = wild ? "ROLE" : role + "  ·  CLASS";
            string name = wild ? role : Defs.KitName(me.Kit).ToUpperInvariant();
            var rc = Color.Lerp(roleColor, Color.white, 0.2f);
            float ew = Ui.TextWidth(eyebrow, 12, true);
            Ui.Label(new Rect(r.x, r.y, ew + 6f, 16f), eyebrow, 12, rc, TextAnchor.MiddleLeft, true);
            float rx = r.x + ew + 12f, rw = Mathf.Max(0f, r.xMax - rx);
            const int steps = 24;
            for (int i = 0; i < steps; i++)  // a rule that fades out to the right
                Ui.Fill(new Rect(rx + rw * i / steps, r.y + 8f, rw / steps + 1f, 1f), new Color(roleColor.r, roleColor.g, roleColor.b, 0.7f * (1f - i / (float)steps)));
            LobbyKit.Fit(new Rect(r.x, r.y + 14f, r.width, 36f), name, 22, 14, Color.white, TextAnchor.MiddleLeft, calm);
            float ry = r.y + 52f;
            if (!calm)
                Ui.Fill(new Rect(r.x, ry - 2f, 76f, 7f), new Color(roleColor.r, roleColor.g, roleColor.b, 0.18f));
            for (int i = 0; i < 6; i++)  // a short rule in the role colour, fading out
                Ui.Fill(new Rect(r.x + i * 12f, ry, 12f, 3f), new Color(roleColor.r, roleColor.g, roleColor.b, 1f - i * 0.16f));
        }

        /// The ascension button on the right quarter of a weapon key: a rank mark over "0/3". True on a click (it opens the ascension window).
        bool AscensionKey(Rect r, bool on, bool calm)
        {
            bool hover = GUI.enabled && r.Contains(Event.current.mousePosition);
            var c = CrtUi.Ink;
            CrtKit.SoftBox(r, c, on, hover, false);
            Color ic = hover ? CrtKit.SoftText(c, true) : on ? LobbyKit.Gold : new Color(c.r, c.g, c.b, 0.85f);
            float cx = Mathf.Round(r.center.x), top = r.y + 5f;
            for (int n = 0; n < 3; n++)  // three stacked chevrons
                for (int k = 0; k < 3; k++)
                {
                    float py = top + n * 3f + k * 2f;
                    Ui.Fill(new Rect(cx - 2f * (k + 1), py, 2f, 2f), ic);
                    Ui.Fill(new Rect(cx + 2f * k, py, 2f, 2f), ic);
                }
            Ui.Label(new Rect(r.x, r.y + 18f, r.width, r.height - 20f), "0/" + AscensionsPerWeapon, 10, ic, TextAnchor.MiddleCenter, true);
            if (hover)
            {
                lockedTip = r;
                lockedTipOn = true;
                lockedTipText = "Ascensions  0/" + AscensionsPerWeapon;
            }
            return Ui.HitArea(r);
        }

        /// The ascension window over the loadout panel: the weapon's name and three locked ascensions. Any click closes it (DrawLobby).
        void DrawAscensionWindow(Rect r, string weapon, bool calm)
        {
            Ui.Fill(r, new Color(CrtUi.Bg.r, CrtUi.Bg.g, CrtUi.Bg.b, 0.86f));
            float ww = r.width * 0.78f, wh = 250f;
            var win = new Rect(r.center.x - ww / 2f, r.center.y - wh / 2f, ww, wh);
            Ui.Fill(win, new Color(CrtUi.Bg.r, CrtUi.Bg.g, CrtUi.Bg.b, 1f));
            Ui.Fill(win, LobbyKit.Ink(0.05f));
            CrtKit.Frame(win, 2f, CrtUi.Ink);
            float px = win.x + 18f;
            Ui.Label(new Rect(px, win.y + 10f, 300f, 16f), "WEAPON ASCENSIONS", 11, LobbyKit.Gold, TextAnchor.MiddleLeft, true);
            LobbyKit.Fit(new Rect(px, win.y + 26f, win.width - 36f, 40f), weapon.ToUpperInvariant(), 32, 16, Color.white, TextAnchor.MiddleLeft, calm);
            Ui.Label(new Rect(px, win.y + 68f, 300f, 18f), "ASCENSIONS 0 / " + AscensionsPerWeapon + " UNLOCKED", 12, CrtUi.Dim, TextAnchor.MiddleLeft, true);
            float tw = (win.width - 36f - 10f * (AscensionsPerWeapon - 1)) / AscensionsPerWeapon;
            var dim = CrtUi.Dim;
            for (int i = 0; i < AscensionsPerWeapon; i++)
            {
                var c = new Rect(px + i * (tw + 10f), win.y + 96f, tw, 112f);
                Ui.Fill(c, new Color(dim.r, dim.g, dim.b, 0.03f));
                CrtKit.Dashed(c, 2f, new Color(dim.r, dim.g, dim.b, 0.7f));
                Ui.Label(new Rect(c.x + 10f, c.y + 6f, c.width - 20f, 34f), Numerals[i], 28, dim, TextAnchor.MiddleLeft, true);
                float lw2 = Ui.TextWidth("LOCKED", 11, true) - 6f;
                Ui.Icon(new Rect(c.x + 10f, c.y + 44f, 12f, 12f), PixelArt.Get("ui_lock"), new Color(1f, 1f, 1f, 0.5f));
                Ui.Label(new Rect(c.x + 26f, c.y + 42f, lw2 + 14f, 16f), "LOCKED", 11, dim, TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(c.x + 10f, c.y + 62f, c.width - 20f, 44f), "Coming soon", 12, dim, TextAnchor.UpperLeft, false, true);
            }
            Ui.Label(new Rect(win.x, win.yMax - 26f, win.width, 18f), "CLICK ANYWHERE TO CLOSE", 10, CrtUi.Dim, TextAnchor.MiddleCenter, true);
        }

        /// One detail box for whichever weapon, ability or ultimate was clicked last: the kind and name on the left, the number tiles on
        /// the right, a rule, then the description (and what mastering it adds, for an ability or ultimate).
        void DrawDetailBox(Rect r, WeaponDef wdef, AbilityDef ability, AbilityDef ult)
        {
            if (r.height < 90f)
                return;
            bool calm = fxReduced;
            Ui.Fill(r, LobbyKit.Ink(0.04f));
            CrtKit.Frame(r, 1f, LobbyKit.Ink(0.35f));
            var inner = new Rect(r.x + 14f, r.y + 8f, r.width - 28f, r.height - 16f);
            string kind = detailSel == 1 ? "ABILITY" : detailSel == 2 ? "ULTIMATE" : "WEAPON";
            string name = detailSel == 1 ? ability.Name : detailSel == 2 ? ult.Name : wdef.Name;
            var tiles = new System.Collections.Generic.List<string[]>();
            if (detailSel == 0)
            {
                if (wdef.Companion != MinionKind.None && wdef.CompanionCount > 0)
                    tiles.Add(new[] { wdef.CompanionCount.ToString(), "COMPANIONS" });
                if (wdef.Damage > 0f)
                    tiles.Add(new[] { Mathf.RoundToInt(wdef.Damage).ToString(), wdef.Melee ? "PER SWING" : "PER HIT" });
                if (wdef.FireInterval > 0f)
                    tiles.Add(new[] { (1f / wdef.FireInterval).ToString(wdef.FireInterval >= 0.5f ? "0.0" : "0"), wdef.Melee ? "SWINGS / S" : "SHOTS / S" });
            }
            else
            {
                var a = detailSel == 1 ? ability : ult;
                tiles.Add(new[] { a.Cooldown.ToString("0.#") + " s", detailSel == 2 ? "TO CHARGE" : "COOLDOWN" });
                if (a.ExtraCharges > 0)
                    tiles.Add(new[] { (1 + a.ExtraCharges).ToString(), "CHARGES" });
                if (a.HealthCost > 0f)
                    tiles.Add(new[] { Mathf.RoundToInt(a.HealthCost * 100f) + "%", a.CostsCurrent ? "OF CURRENT HP" : "OF MAX HP" });
            }
            float tileW = 92f, tw = tiles.Count * tileW + Mathf.Max(0, tiles.Count - 1) * 6f;
            SpecTiles(new Rect(inner.xMax - tw, inner.y, tw, 38f), tiles, calm);
            var head = new Rect(inner.x, inner.y, inner.width - tw - 12f, 40f);
            SpecHeader(head, kind, name, calm);
            Ui.Fill(new Rect(inner.x, inner.y + 46f, inner.width, 1f), LobbyKit.Ink(0.25f));
            var body = new Rect(inner.x, inner.y + 54f, inner.width, inner.height - 54f);
            var text = new Color(0.8f, 0.84f, 0.94f);
            if (detailSel == 0)
                FitText(body, wdef.Desc, 13, text);
            else
                DrawDescAndMastery(body, detailSel == 1 ? ability.Desc : ult.Desc, detailSel == 1 ? ability.Id : ult.Id, 13, text);
        }

        // How many slots each loadout row shows; the ones past the real content are locked ("Coming soon").
        const int RoleSlots = 4, ClassSlots = 8, WeaponSlots = 4, AbilitySlots = 4;

        string lockedTipText = "Coming soon";
        const string WildcardLockText = "Win a run as a Tank, a Healer and a DPS to unlock";
        bool WildcardOpen => game.Profile == null || game.Profile.WildcardUnlocked;

        Rect lockedTip;          // the locked slot under the mouse, if any (its tooltip is drawn last, on top)
        bool lockedTipOn;

        /// A slot for content still to come: dim, dashed, the real padlock and "LOCKED". Hovering shows "Coming soon" (or `tip`).
        void LockedSlot(Rect r, string tip = null, bool stack = false)
        {
            if (LobbyKit.Locked(r, fxReduced, stack || r.width < 100f))
            {
                lockedTip = r;
                lockedTipOn = true;
                lockedTipText = tip ?? "Coming soon";
            }
        }

        /// A Wildcard's picks (Docs/Wildcard.md): every class's three DPS weapons on the left and three DPS abilities on the right,
        /// one row a class (its icon at the start). The tabs choose whether a click sets the first ability or the second; the second sits on the key chosen below it.
        /// Fills y to yEnd.
        void DrawWildcardPicks(LobbyEntry me, Session tgt, bool self, float x, float y, float w, float yEnd)
        {
            bool calm = fxReduced;
            float col = (w - 16f) / 2f, ax = x + col + 16f;
            int second = Defs.WildSecond(me.Wild), key = Defs.WildKey(me.Wild);
            LobbyKit.Heading(x, y, w, "Gear", "Any class's DPS weapon and two abilities", calm);
            y += 34f;
            // Left: which key the second ability sits on. Right: which ability a click sets.
            Ui.Label(new Rect(x, y, 96f, 24f), "2ND ABILITY KEY", 11, CrtUi.Dim, TextAnchor.MiddleLeft, true);
            string[] keyNames = { "Q", "E", "SHIFT" };
            float kw = (col - 100f - 8f) / 3f;
            for (int k = 1; k <= 3; k++)
                if (LobbyKit.Key(new Rect(x + 100f + (k - 1) * (kw + 4f), y, kw, 24f), keyNames[k - 1], key == k, calm, size: 14, minSize: 10) && key != k)
                {
                    tgt.RequestWildcard(me.Weapon, me.Ability, second, k);
                    if (self)
                        wildKey = k;
                }
            float tw = (col - 4f) / 2f;
            if (LobbyKit.Key(new Rect(ax, y, tw, 24f), "1ST ABILITY", wildTab == 0, calm, size: 14, minSize: 9))
                wildTab = 0;
            if (LobbyKit.Key(new Rect(ax + tw + 4f, y, tw, 24f), "2ND ABILITY", wildTab == 1, calm, size: 14, minSize: 9))
                wildTab = 1;
            y += 30f;

            const float gutter = 26f;  // each row starts with its class's icon
            float rowH = Mathf.Min(27f, (yEnd - y) / Defs.KitCount);
            float cellW = (col - gutter) / 3f;
            for (int kit = 0; kit < Defs.KitCount; kit++)
            {
                float ry = y + kit * rowH;
                Ui.Icon(new Rect(x + 2f, ry + (rowH - 3f - 18f) / 2f, 18f, 18f), PixelArt.Get(Classes.Get((Kit)kit).Icon), Color.white);
                for (int c = 0; c < Defs.WeaponsPerRole; c++)
                {
                    int idx = kit * Defs.WeaponsPerRole + c;
                    var wb = new Rect(x + gutter + c * cellW, ry, cellW - 3f, rowH - 3f);
                    if (LobbyKit.Key(wb, Defs.Weapon(Role.Wildcard, Kit.Soldier, idx).Name, me.Weapon == idx, calm, size: 12, minSize: 8) && me.Weapon != idx)
                    {
                        tgt.RequestWildcard(idx, me.Ability, second, key);
                        if (self)
                            weaponChoice = idx;
                        detailSel = 0;
                    }
                    var ab = new Rect(ax + c * (col / 3f), ry, col / 3f - 3f, rowH - 3f);
                    bool shownAsPicked = wildTab == 0 ? me.Ability == idx : second == idx;
                    bool takenByOther = wildTab == 0 ? second == idx : me.Ability == idx;
                    if (LobbyKit.Key(ab, Upgrades.Ability(Upgrades.RoleAbility(Role.Wildcard, Kit.Soldier, idx)).Name, shownAsPicked, calm, size: 12, minSize: 8, accent: takenByOther ? CrtUi.Dim : (Color?)null)
                        && !takenByOther && !shownAsPicked)
                    {
                        int a1 = wildTab == 0 ? idx : me.Ability, a2 = wildTab == 0 ? second : idx;
                        tgt.RequestWildcard(me.Weapon, a1, a2, key);
                        if (self)
                        {
                            abilityChoice = a1;
                            wildSecond = a2;
                        }
                        detailSel = 1;
                    }
                }
            }
        }

        /// A dark card with a dashed rim: the look of everything locked (the Perks tab's empty slots).
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

        /// The "Coming soon" tooltip over the hovered locked slot (call after everything else is drawn).
        void DrawLockedTip()
        {
            if (!lockedTipOn)
                return;
            string text = lockedTipText;
            float w = Ui.TextWidth(text, 14, true) + 22f, h = 30f;
            var tip = new Rect(lockedTip.center.x - w / 2f, lockedTip.y - h - 6f, w, h);
            if (tip.y < 4f)
                tip.y = lockedTip.yMax + 6f;
            tip.x = Mathf.Clamp(tip.x, 4f, W - w - 4f);
            Ui.Fill(tip, new Color(CrtUi.Bg.r, CrtUi.Bg.g, CrtUi.Bg.b, 0.98f));
            CrtKit.Frame(tip, 2f, CrtUi.Ink);
            CrtUi.Text(tip, text, 14, CrtUi.Ink, TextAnchor.MiddleCenter, true, fxReduced);
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
        /// A name on one line: smaller text first, then cut short, like DrawOneLine, drawn in their name effect.
        void DrawNameLine(Rect r, string text, int size, int minSize, int fx, float t)
        {
            int s = size;
            while (s > minSize && Ui.TextWidth(text, s, true) > r.width)
                s--;
            if (Ui.TextWidth(text, s, true) > r.width)
            {
                int n = text.Length;
                while (n > 1 && Ui.TextWidth(text.Substring(0, n).TrimEnd() + "…", s, true) > r.width)
                    n--;
                text = text.Substring(0, n).TrimEnd() + "…";
            }
            DrawName(r, text, s, fx, t, Color.white);
        }

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

        void SpecHeader(Rect col, string eyebrow, string name, bool calm)
        {
            Ui.Label(new Rect(col.x, col.y - 1f, col.width, 16f), eyebrow, 13, CrtUi.Dim, TextAnchor.MiddleLeft, true);
            LobbyKit.Fit(new Rect(col.x, col.y + 12f, col.width, 28f), name, 20, 13, Color.white, TextAnchor.MiddleLeft, calm);
        }

        /// A row of number tiles, a gold value over a small caption, sharing the row's width.
        static void SpecTiles(Rect row, System.Collections.Generic.List<string[]> tiles, bool calm)
        {
            if (tiles.Count == 0)
                return;
            float tw = (row.width - 6f * (tiles.Count - 1)) / tiles.Count;
            for (int i = 0; i < tiles.Count; i++)
                LobbyKit.Tile(new Rect(row.x + i * (tw + 6f), row.y, tw, row.height), tiles[i][0], tiles[i][1], calm);
        }

        /// The zoom meter along the preview's lower edge (a slider, "-" and "+" steps, and the mouse wheel over the window).
        void DrawShowcaseZoom(Rect view)
        {
            if (uiGlBlocked || hangoutAnim > 0f)
                return;
            const float Min = 0.5f, Max = 2f;
            var e = Event.current;
            bool over = view.Contains(e.mousePosition);
            if (e.type == EventType.ScrollWheel && over)
            {
                showcaseZoom = Mathf.Clamp(showcaseZoom * (e.delta.y > 0f ? 0.9f : 1.1f), Min, Max);
                e.Use();
            }
            // Fades out until the mouse is over the window (it stays up while the slider is being dragged).
            bool drag = showcaseZoomDrag;
            if (e.type == EventType.Repaint)
                showcaseZoomA = Mathf.MoveTowards(showcaseZoomA, over || drag ? 1f : 0f, Time.unscaledDeltaTime * (over || drag ? 8f : 2.5f));
            if (e.type == EventType.MouseUp)
                showcaseZoomDrag = false;
            var bar = new Rect(view.x, view.y, view.width, 18f);  // flush with the top of the window
            var track = new Rect(bar.x + 70f, bar.y, bar.width - 78f, bar.height);
            if (e.type == EventType.MouseDown && track.Contains(e.mousePosition))
                showcaseZoomDrag = true;
            float oa = Ui.Alpha;
            Ui.Alpha = oa * showcaseZoomA;
            var oc = GUI.color;
            GUI.color = new Color(oc.r, oc.g, oc.b, oc.a * showcaseZoomA);
            Ui.Fill(bar, new Color(0f, 0f, 0f, 0.6f));
            Ui.Label(new Rect(bar.x + 6f, bar.y, 62f, bar.height), "ZOOM " + Mathf.RoundToInt(showcaseZoom * 100f) + "%", 10, LobbyKit.Gold, TextAnchor.MiddleLeft, true);
            float lo = (float)System.Math.Log(Min), hi = (float)System.Math.Log(Max);  // log scale: out and in feel even
            float t = Mathf.Clamp01(((float)System.Math.Log(showcaseZoom) - lo) / (hi - lo));
            float nt = Ui.Slider(track, t);
            if (nt != t)
                showcaseZoom = (float)System.Math.Exp(lo + (hi - lo) * nt);
            GUI.color = oc;
            Ui.Alpha = oa;
        }

        /// The live mini-arena on the hero card: your loadout in action, zoomed out to fit the dummies.
        void DrawShowcase(LobbyEntry me, Rect view, Color roleColor)
        {
            if ((uiGlBlocked && !pieceGlOn) || hangoutAnim > 0f)
                return;  // (hangoutAnim: the window is turning into the Hangout, which draws its own floor and ship; pieceGlOn: the lobby's entrance fades the preview in with the loadout)
            // the lobby sliding away as a run launches: no live preview (it would start up again) (LaunchIntro.cs)
            int shown = me.Ability;
            bool ult = detailSel == 2;  // what you last clicked (a weapon, an ability or the ultimate) is what it shows
            bool demo = detailSel != 0;
            if (showcase == null)
                showcase = new Showcase();
            showcase.Show(me.Role, me.Kit, me.Weapon, Upgrades.RoleAbility(me.Role, me.Kit, shown), demo, ult);
            showcaseSeen = Time.unscaledTime;
            if (!BeginUiGl())
                return;
            var rc = new Rgba(roleColor.r, roleColor.g, roleColor.b);
            var sg = showcase.Game;
            float zoom = Mathf.Min(1.1f, view.height / (showcase.Extent + 190f)) * showcaseZoom;
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
                var ab = Upgrades.Ability(ult ? Upgrades.Ultimate(me.Role, me.UltKit) : Upgrades.RoleAbility(me.Role, me.Kit, shown));
                Ui.Label(new Rect(view.x + 8f, view.yMax - 20f, view.width - 16f, 16f), ab.Name.ToUpperInvariant(), 10,
                    Color.Lerp(roleColor, Color.white, 0.5f), TextAnchor.MiddleCenter, true);  // just the name (no key or "ultimate" prefix)
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
            // Its sounds play muffled, as if heard through the preview window's glass (nothing else is drawn alongside it)
            foreach (var sfx in showcase.Game.Sounds)
                sounds.PlayMuffled(sfx.Sfx, sfx.Ally ? 0.45f : 1f, Time.unscaledTime);
            showcase.Game.Sounds.Clear();
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

        // --- Actions under the party -------------------------------------------------

        /// Ready and Start, the two big keys at the bottom of the party column (a guest's Ready takes the Start slot). Ready is lit
        /// once you are ready; Start is dim and dashed until everyone is.
        void DrawLobbyActions(LobbyEntry me, bool practice)
        {
            const float lx = PartyX, lw = PartyW;
            bool host = session.IsHost, calm = fxReduced;
            bool ready = me != null && me.Ready;
            var readyRect = host ? new Rect(lx, LobbyReadyY, lw, LobbyReadyH) : new Rect(lx, LobbyStartY, lw, LobbyStartH);
            if (LobbyKit.Key(readyRect, ready ? "READY!" : "READY?", ready, calm, PixelArt.Get("ui_check"), size: 22, minSize: 14, accent: ready ? Ui.Good : (Color?)null))
                session.RequestReady(!ready);
            int waiting = 0;  // other players who aren't ready yet
            foreach (var e in session.Lobby.Values)
                if (!e.Ready && e.Id != session.MyId)
                    waiting++;
            // Only with other players: who you're waiting on, or that everyone's ready.
            string note = session.Lobby.Count < 2 ? ""
                : waiting > 0 ? "Waiting for " + waiting + " player" + (waiting == 1 ? "" : "s") + " to ready up"
                : !ready ? "" : host ? "Everyone's ready" : "Everyone's ready: waiting for the host to start";
            if (note.Length > 0)
                CrtUi.Text(new Rect(lx, readyRect.y - 30f, lw, 24f), note, 14, waiting == 0 ? CrtUi.Ink : CrtUi.Dim, TextAnchor.MiddleCenter, true, calm);
            if (host)
            {
                bool startOn = session.AllReady && (!practice || session.Plan.Waves.Count > 0) && (practice || !session.HuntMode || session.Resume != null || (session.LobbyHunt != null && !huntBrowsingLocked));  // (a locked hunt can't start)
                if (LobbyKit.Key(new Rect(lx, LobbyStartY, lw, LobbyStartH), "START", false, calm, PixelArt.Get("ui_play"), size: 22, minSize: 14, locked: !startOn))
                    session.StartMatch();
            }
        }
    }
}
