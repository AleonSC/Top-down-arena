using System;
using UnityEngine;

namespace Arena
{
    /// The lobby: the mode switch (a normal run or the Practice Range), the party with each player's
    /// character drawn live, your loadout with a big preview of your character, and in Practice Range
    /// mode the shared wave editor (PracticeUi.cs).
    public partial class ArenaApp
    {
        
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
        bool showcaseIdle;         // the preview's IDLE key: the character stands still
        bool showcaseMute;         // the preview's MUTE key (next to the zoom slider)
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
            if (!practice && (lobbyTab == 1 || lobbyTab == TrainingTab))
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
                if (ascBuyRect.Contains(Event.current.mousePosition))
                    Profile.Current.BuyVariant(ascWeapon);  // the Buy button keeps the window open
                else if (ascEquipRects[0].Contains(Event.current.mousePosition))
                    ascEquipPick = ascEquipTo[0];  // the Equip buttons too: that weapon variant on or off (applied where the loadout is drawn)
                else if (ascEquipRects[1].Contains(Event.current.mousePosition))
                    ascEquipPick = ascEquipTo[1];
                else
                    ascOpen = -1;  // the variants window closes on any other click (and the click goes no further)
                Event.current.Use();
            }

            // --- Left column, top: Back, LOBBY_ and the "!" key (Leaving, Settings and the Field guide are in the Esc menu, MenuDrawer.cs),
            // then the mode keys (a choice for the whole lobby, so they sit with the party).
            if (LobbyKit.Key(LobbyBackKey, "", false, calm, PixelArt.Get("ui_back"), size: 22, accent: Color.Lerp(CrtUi.Ink, CrtStyle.Danger, 0.55f)))  // a slight red: it leaves the lobby
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
            else if (lobbyTab == TrainingTab && practice)
                DrawTraining(body, t);  // TrainingUi.cs
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
            DrawTip(true);
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

        // The mode carousel's turn: modeTurn is the mode on top (unwrapped, mode = modeTurn mod 3), modeTurnPos eases toward it.
        int modeTurn;
        float modeTurnPos;
        bool modeTurnSet;

        static string ModeTitle(int i) => i == 0 ? "CAMPAIGN" : i == 1 ? "BOSS HUNT" : "PRACTICE";
        static string ModeSub(int i) => i == 0 ? "100 waves, build as you go" : i == 1 ? "10 waves to one boss" : "Build your own waves, or train a mechanic";
        static Color? ModeAccent(int i) => i == 1 ? new Color(1f, 0.62f, 0.58f) : i == 2 ? new Color(0.37f, 0.94f, 1f) : (Color?)null;

        /// The mode carousel (like the Wildcard's class wheel): the mode in play big in the middle with its line under it, the other
        /// two smaller and faded either side, and arrows at the ends. Arrows, the mouse wheel or a click on a side mode turn it;
        /// only the host can.
        void DrawModeSwitch(Rect r, float t)
        {
            const int n = 3;
            bool calm = fxReduced, host = session.IsHost;
            var ev = Event.current;
            int mode = session.PracticeMode ? 2 : session.HuntMode ? 1 : 0;
            int d0 = ((mode - modeTurn) % n + n) % n;  // follow the session the short way round (the host's turn, or someone else's)
            if (!modeTurnSet)
            {
                modeTurn = mode;
                modeTurnPos = mode;
                modeTurnSet = true;
            }
            else if (d0 != 0)
                modeTurn += d0 > n / 2 ? d0 - n : d0;
            if (ev.type == EventType.Repaint)
            {
                modeTurnPos += (modeTurn - modeTurnPos) * (calm ? 1f : 1f - Mathf.Exp(-Time.unscaledDeltaTime * 12f));
                if (Mathf.Abs(modeTurn - modeTurnPos) < 0.004f)
                    modeTurnPos = modeTurn;
            }
            int turnTo = int.MinValue;  // a turn asked for this frame (unwrapped)
            if (host && ev.type == EventType.ScrollWheel && GUI.enabled && r.Contains(ev.mousePosition))
            {
                turnTo = modeTurn + (ev.delta.y > 0f ? 1 : -1);
                ev.Use();
            }

            const float arrowW = 22f, gap = 6f;
            float ix = r.x + arrowW + gap, iw = r.width - 2f * (arrowW + gap);
            float bigW = Mathf.Round(iw * 0.44f), smallW = Mathf.Floor((iw - bigW - 2f * gap) / 2f);
            float step = (bigW + smallW) / 2f + gap, cx = ix + iw / 2f;
            float oldAlpha = Ui.Alpha;
            // Draw the far ones first so the middle one is on top while they slide.
            var order = new int[n];
            var dist = new float[n];
            for (int i = 0; i < n; i++)
            {
                float d = ((i - modeTurnPos) % n + n) % n;
                if (d > n / 2f)
                    d -= n;  // -1.5 .. 1.5, the mode on top at 0
                dist[i] = d;
                order[i] = i;
            }
            System.Array.Sort(order, (a, b) => Mathf.Abs(dist[b]).CompareTo(Mathf.Abs(dist[a])));
            foreach (int i in order)
            {
                float d = dist[i], ad = Mathf.Abs(d);
                float a = ad <= 1f ? 1f - 0.45f * ad : 0.55f * Mathf.Clamp01((1.5f - ad) / 0.5f);  // fades out round the back
                if (a <= 0.01f)
                    continue;
                float k = Mathf.Clamp01(ad);
                float w = Mathf.Lerp(bigW, smallW, k), h = Mathf.Lerp(r.height, r.height - 16f, k);
                var kr = new Rect(Mathf.Round(cx + d * step - w / 2f), Mathf.Round(r.center.y - h / 2f), Mathf.Round(w), Mathf.Round(h));
                bool top = i == mode && ad < 0.5f;
                Ui.Alpha = oldAlpha * a;
                if (top && !calm)
                    Ui.Glow(kr, ModeAccent(i) ?? CrtUi.Ink, 0.35f * (1f - ad * 2f));
                bool click = LobbyKit.Key(kr, ModeTitle(i), top, calm, size: ad < 0.5f ? 15 : 12, minSize: 9, enabled: host && ad < 1.2f,
                    accent: ModeAccent(i), sub: ad < 0.35f ? ModeSub(i) : null);
                Ui.Alpha = oldAlpha;
                if (click && i != mode)
                    turnTo = modeTurn + (d > 0f ? 1 : -1);
                if (ad < 1.2f && kr.Contains(ev.mousePosition))  // the description is a hover tip
                {
                    TipStart(kr, ModeTitle(i), CrtUi.Ink);
                    TipAdd(ModeSub(i), 13, Ui.Text);
                    if (!host)
                        TipAdd("Only the host can switch the mode.", 11, Ui.Muted);
                }
            }
            // The arrows at the ends.
            for (int side = -1; side <= 1; side += 2)
            {
                var ab = new Rect(side < 0 ? r.x : r.xMax - arrowW, r.center.y - 14f, arrowW, 28f);
                bool click = LobbyKit.Key(ab, "", false, calm, enabled: host);
                Chevron(ab.center, side, host ? CrtKit.SoftText(CrtUi.Ink, GUI.enabled && ab.Contains(ev.mousePosition)) : new Color(CrtUi.Dim.r, CrtUi.Dim.g, CrtUi.Dim.b, 0.5f));
                if (click)
                    turnTo = modeTurn + side;
            }
            if (turnTo != int.MinValue && host)
            {
                int m = ((turnTo % n) + n) % n;
                modeTurn = turnTo;
                if (m != mode)
                    session.SetMode(m == 2, m == 1);
            }
        }

        /// The tab keys across the top of the big screen: the same order in every mode (the shared tabs, then the mode's own at the end).
        void DrawLobbyTabs(Rect r, bool practice)
        {
            int mods = Modifiers.Count(session.Modifiers);
            bool hunt = session.HuntMode && !practice && session.Resume == null;
            // Indexed by tab id (0 loadout, 1 practice, 2 modifiers, 3 difficulty, 4 boss hunt, 5 perks, 6 hangout); null = not shown.
            string[] names = { session.Resume != null ? "Seats" : "Loadout", practice ? "Practice  (" + session.Plan.Waves.Count + ")" : null, mods > 0 ? "Modifiers  (" + mods + ")" : "Modifiers",
                "Difficulty", hunt ? "Boss hunt" : null, practice ? null : "Perks", null, practice ? "Training" : null };  // Hangout has its own key on the Party line
            int[] order = { 0, PerkTab, 3, 2, 1, TrainingTab, 4 };
            // A carousel (like the mode carousel above the party): the open tab in the middle, the others in order either side,
            // smaller and fading toward the ends; the arrows, the mouse wheel or a click on a tab slide it.
            var shown = new System.Collections.Generic.List<int>();
            foreach (int i in order)
                if (names[i] != null)
                    shown.Add(i);
            int n = shown.Count, cur = Mathf.Max(0, shown.IndexOf(lobbyTab));
            var ev = Event.current;
            // tabTurnPos is unwrapped (cur = pos mod n): turn the short way round to the open tab.
            float diff = ((cur - tabTurnPos) % n + n) % n;
            if (diff > n / 2f)
                diff -= n;
            if (!tabTurnSet || n != tabTurnCount)
            {
                tabTurnPos = cur;
                diff = 0f;
                tabTurnSet = true;
                tabTurnCount = n;
            }
            if (ev.type == EventType.Repaint)
            {
                tabTurnPos += diff * (fxReduced ? 1f : 1f - Mathf.Exp(-Time.unscaledDeltaTime * 12f));
                if (Mathf.Abs(diff) < 0.004f)
                    tabTurnPos = Mathf.Round(tabTurnPos);
            }
            int want = cur;
            if (ev.type == EventType.ScrollWheel && GUI.enabled && r.Contains(ev.mousePosition))
            {
                want = cur + (ev.delta.y > 0f ? 1 : -1);
                ev.Use();
            }
            const float arrowW = 22f, gap = 6f, bigW = 168f, smallW = 128f, step = 152f;
            float cx = r.center.x, half = r.width / 2f - arrowW - gap;
            float oldAlpha = Ui.Alpha;
            for (int k = 0; k < n; k++)
            {
                int i = shown[k];
                float d = ((k - tabTurnPos) % n + n) % n;
                if (d > n / 2f)
                    d -= n;  // the open tab at 0, the rest either side
                float ad = Mathf.Abs(d);
                float w = Mathf.Lerp(bigW, smallW, Mathf.Clamp01(ad));
                float x = cx + Mathf.Sign(d) * (step * Mathf.Min(ad, 1f) + 124f * Mathf.Max(0f, ad - 1f));  // the far ones closer in
                float edge = Mathf.Clamp01((half - (Mathf.Abs(x - cx) + w / 2f)) / 50f + 1f);  // fades out at the arrows
                float a = (1f - 0.25f * Mathf.Clamp01(ad)) * Mathf.Clamp01((2.4f - ad) / 0.9f) * edge;
                if (a <= 0.02f)
                    continue;
                bool on = lobbyTab == i;
                float inset = Mathf.Clamp01(ad) * 3f;
                var tr = new Rect(Mathf.Round(x - w / 2f), r.y + inset, Mathf.Round(w), r.height - 2f * inset);
                Ui.Alpha = oldAlpha * a;
                if (on && !fxReduced)
                    Ui.Glow(tr, CrtUi.Ink, 0.3f * Mathf.Clamp01(1f - ad * 2f));
                bool click = LobbyKit.QuietTab(tr, names[i].ToUpperInvariant(), on, a > 0.4f);
                Ui.Alpha = oldAlpha;
                if (click && !on)
                    want = k;
            }
            for (int side = -1; side <= 1; side += 2)
            {
                var ab = new Rect(side < 0 ? r.x : r.xMax - arrowW, r.y, arrowW, r.height);
                bool click = LobbyKit.Key(ab, "", false, fxReduced);
                Chevron(ab.center, side, CrtKit.SoftText(CrtUi.Ink, GUI.enabled && ab.Contains(ev.mousePosition)));
                if (click)
                    want = cur + side;
            }
            want = ((want % n) + n) % n;
            if (want != cur)
                lobbyTab = shown[want];
        }

        // The tab carousel's turn (unwrapped: the open tab is tabTurnPos mod the tab count), easing toward the open tab.
        float tabTurnPos;
        bool tabTurnSet;
        int tabTurnCount;

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
                DkBox(r, color, mine ? 0.85f : 0.3f);
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
                if (Ui.HitArea(new Rect(tx, r.y, r.width - (tx - r.x) - 9f - room, r.height)))
                    editBotId = botSeat != null ? e.Id : -1;
                PartyCardClick(new Rect(r.x, r.y, tx - r.x, r.height), e, ToColor(Rank.TierColor(e.Rank)), t);  // the portrait: your profile / their holo card (PlayerInspect.cs)
            }
            else
                PartyCardClick(r, e, ToColor(Rank.TierColor(e.Rank)), t);  // the whole card (a remote player's, or a non-host's own)
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

        int ascOpen = -1;
        Rect ascBuyRect;   // the variants window's Buy button this frame (empty when none)
        readonly Rect[] ascEquipRects = new Rect[2];  // its Equip buttons this frame, one per tier that is a variant weapon (empty when none)
        readonly int[] ascEquipTo = new int[2];       // the weapon pick each Equip button would set
        int ascEquipPick = -1;                        // a click waiting to be applied
        string ascWeapon;  // the weapon whose variants window is open   // the weapon slot whose ascension window is open (-1 = closed; any click closes it)
        int detailSel;      // what the detail box shows: 0 = weapon, 1 = ability, 2 = ultimate (whichever was clicked last)

        /// The ability the detail box and the preview show: a Wildcard's second when that was clicked last, else the first.
        int ShownAbility(LobbyEntry me) => me.Role == Role.Wildcard && detailSecond ? Defs.WildSecond(me.Wild) : me.Ability;

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
            var ult = Upgrades.Ability(me.UltId);  // the one picked on the Ultimate row

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
                        ultChoice = 0;
                        wildSecond = 1;
                        wildKey = 1;
                        wildTab = 0;
                        wildBrowseFor = -1;
                        detailSecond = false;
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
                            ultChoice = 0;
                        }
                    }
                }
                y += 32f + ((classSlots + 3) / 4) * 40f - 4f + 10f;

                // Weapon and ability: side by side, each a 2 x 2 grid. Each weapon key gives its right quarter to an ascension button.
                const float col = 220f;
                float ax = x + w - col, cell = (col - gap) / 2f;
                LobbyKit.Heading(x, y, col, "Weapon", null, calm, 15);
                LobbyKit.Heading(ax, y, col, "Ability", null, calm, 15);
                int weaponCount = Defs.WeaponCount(me.Role, me.Kit), weaponSlots = Mathf.Max(WeaponSlots, weaponCount);
                for (int i = 0; i < weaponSlots; i++)
                {
                    var b = new Rect(x + (i % 2) * (cell + gap), y + 32f + (i / 2) * 40f, cell, 34f);
                    if (i >= weaponCount)  // (the 4th key is a weapon where the class has a fourth: the Knight DPS's Arc Knuckles)
                    {
                        LockedSlot(b, null, true);
                        continue;
                    }
                    var main = new Rect(b.x, b.y, Mathf.Round(b.width * 0.74f), b.height);
                    if (LobbyKit.Key(main, Defs.Weapon(me.Role, me.Kit, i).Name.ToUpperInvariant(), WeaponKeyOn(me, i), calm, size: 14, minSize: 9))
                    {
                        tgt.RequestWeapon(i);
                        if (self)
                            weaponChoice = i;
                        detailSel = 0;
                    }
                    if (AscensionKey(new Rect(main.xMax - 2f, b.y, b.xMax - (main.xMax - 2f), b.height), Defs.Weapon(me.Role, me.Kit, i).Name, WeaponKeyOn(me, i), calm))
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
                        detailSecond = false;
                    }
                }
                y += 32f + ((Mathf.Max(weaponSlots, abilitySlots) + 1) / 2) * 40f - 6f + 10f;
                ultY = y;
            }

            ultY = Mathf.Max(ultY, view.yMax - 72f);  // the Ultimate keys end on the line the preview ends on
            // The ultimate: key 0 is the role and class's default one, keys 1-3 the others it has (the Tank Soldier's Juggernaut Suit on
            // key 1), locked while empty. Click one to pick it (inverse video, like the weapon and ability keys) and watch it in the preview.
            LobbyKit.Heading(x, ultY, w, "Ultimate", me.Role == Role.Wildcard ? Defs.KitName((Kit)WildBrowseKit) + "'s ultimates" : null, calm, 15);
            bool wildRow = me.Role == Role.Wildcard;  // a Wildcard: the class wheel's class's DPS ultimates (pick = class * UltKeys + key)
            var rowKit = wildRow ? (Kit)WildBrowseKit : me.Kit;
            for (int i = 0; i < Upgrades.UltKeys; i++)
            {
                var b = new Rect(x + i * (quarter + gap), ultY + 32f, quarter, 40f);
                var id = i == 0 ? Upgrades.Ultimate(me.Role, wildRow ? rowKit : me.UltKit) : Upgrades.AltUltimate(me.Role, rowKit, i);
                if (id == AbilityId.None)
                {
                    LockedSlot(b);
                    continue;
                }
                int pick = wildRow ? Upgrades.WildUlt(rowKit, i) : i;
                if (LobbyKit.Key(b, Upgrades.Ability(id).Name.ToUpperInvariant(), me.Ult == pick, calm, size: 15, minSize: 9))
                {
                    if (me.Ult != pick)
                        tgt.RequestUlt(pick);
                    if (self)
                        ultChoice = pick;
                    detailSel = 2;  // (the preview then casts it until you click something else)
                }
            }

            float dy = ultY + 82f;
            var ability = Upgrades.Ability(Upgrades.RoleAbility(me.Role, me.Kit, ShownAbility(me)));
            DrawDetailBox(new Rect(x, dy, w, r.yMax - 12f - dy), wdef, ability, ult);

            if (ascEquipPick >= 0)
            {
                tgt.RequestWeapon(ascEquipPick);  // the Variants window's Equip button: the weapon's variant (or back to the weapon)
                if (self)
                    weaponChoice = ascEquipPick;
                detailSel = 0;
                ascEquipPick = -1;
            }
            if (ascOpen >= 0)
            {
                lockedTipOn = false;  // (the keys under the window must not show their tips)
                DrawAscensionWindow(r, Defs.Weapon(me.Role, me.Kit, ascOpen), me, calm);
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
        /// Weapon key `i` is lit when it is the picked weapon, or when its variant weapon is (the Twin Rifle while the Twin Rifle Auto is out).
        bool WeaponKeyOn(LobbyEntry me, int i)
        {
            if (me.Weapon == i)
                return true;
            return Variants.IsVariantOf(Defs.Weapon(me.Role, me.Kit, i).Id, Defs.Weapon(me.Role, me.Kit, me.Weapon).Id);
        }

        bool AscensionKey(Rect r, string weapon, bool on, bool calm)
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
            Ui.Label(new Rect(r.x, r.y + 18f, r.width, r.height - 20f), Profile.Current.VariantTier(weapon) + "/" + AscensionsPerWeapon, 10, ic, TextAnchor.MiddleCenter, true);
            if (hover)
            {
                lockedTip = r;
                lockedTipOn = true;
                lockedTipText = "Variants  " + Profile.Current.VariantTier(weapon) + "/" + AscensionsPerWeapon;
            }
            return Ui.HitArea(r);
        }

        /// The variants window over the loadout panel: the weapon's name, its two variant tiers (bought or locked) and a Buy button for the
        /// next one, paid in Cores. Any other click closes it (DrawLobby).
        void DrawAscensionWindow(Rect r, WeaponDef baseDef, LobbyEntry me, bool calm)
        {
            string weapon = baseDef.Name;
            bool hasVariant = Variants.VariantOf(baseDef.Id) != null && Variants.WeaponVariantsUnlocked;  // a weapon with variant weapons shows them as its tiers (unlocked for now, Variants.WeaponVariantsUnlocked)
            var activeId = Defs.Weapon(me.Role, me.Kit, me.Weapon).Id;
            var prof = Profile.Current;
            int owned = prof.VariantTier(weapon), cost = Variants.Cost(owned);
            if (hasVariant)
                cost = -1;  // (both tiers are variant weapons, unlocked for now: nothing to buy)
            ascWeapon = weapon;
            Ui.Fill(r, new Color(CrtUi.Bg.r, CrtUi.Bg.g, CrtUi.Bg.b, 0.86f));
            float ww = r.width * 0.78f, wh = hasVariant ? 312f : 280f;
            var win = new Rect(r.center.x - ww / 2f, r.center.y - wh / 2f, ww, wh);
            Ui.Fill(win, new Color(CrtUi.Bg.r, CrtUi.Bg.g, CrtUi.Bg.b, 1f));
            Ui.Fill(win, LobbyKit.Ink(0.05f));
            CrtKit.Frame(win, 2f, CrtUi.Ink);
            float px = win.x + 18f;
            Ui.Label(new Rect(px, win.y + 10f, 300f, 16f), "WEAPON VARIANTS", 11, LobbyKit.Gold, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(win.xMax - 218f, win.y + 10f, 200f, 16f), "CORES  " + prof.Cores, 11, LobbyKit.Gold, TextAnchor.MiddleRight, true);
            LobbyKit.Fit(new Rect(px, win.y + 26f, win.width - 36f, 40f), weapon.ToUpperInvariant(), 32, 16, Color.white, TextAnchor.MiddleLeft, calm);
            Ui.Label(new Rect(px, win.y + 68f, 300f, 18f), "VARIANTS " + (hasVariant ? AscensionsPerWeapon : owned) + " / " + AscensionsPerWeapon + " UNLOCKED", 12, CrtUi.Dim, TextAnchor.MiddleLeft, true);
            ascEquipRects[0] = ascEquipRects[1] = default;
            float tw = (win.width - 36f - 10f * (AscensionsPerWeapon - 1)) / AscensionsPerWeapon;
            var dim = CrtUi.Dim;
            for (int i = 0; i < AscensionsPerWeapon; i++)
            {
                var vid = hasVariant ? Variants.VariantOf(baseDef.Id, i) : null;
                bool isVariant = vid != null;
                WeaponDef variant = isVariant ? Defs.WeaponOf(vid.Value) : null;
                bool variantOn = isVariant && activeId == vid.Value;
                bool got = i < owned || isVariant;
                var col = got ? LobbyKit.Gold : dim;
                var c = new Rect(px + i * (tw + 10f), win.y + 96f, tw, hasVariant ? 144f : 112f);
                Ui.Fill(c, new Color(col.r, col.g, col.b, 0.03f));
                if (got) CrtKit.Frame(c, 2f, col); else CrtKit.Dashed(c, 2f, new Color(dim.r, dim.g, dim.b, 0.7f));
                Ui.Label(new Rect(c.x + 10f, c.y + 6f, c.width - 20f, 34f), Numerals[i], 28, col, TextAnchor.MiddleLeft, true);
                if (isVariant)
                {
                    Ui.Label(new Rect(c.x + 36f, c.y + 6f, c.width - 46f, 34f), variant.Name.ToUpperInvariant(), 13, variantOn ? Color.white : col, TextAnchor.MiddleLeft, true);
                    Ui.Label(new Rect(c.x + 10f, c.y + 44f, c.width - 20f, 16f), variantOn ? "EQUIPPED" : "UNLOCKED", 11, col, TextAnchor.MiddleLeft, true);
                    Ui.Label(new Rect(c.x + 10f, c.y + 62f, c.width - 20f, 46f), Variants.Blurb(vid.Value), 12, CrtUi.Dim, TextAnchor.UpperLeft, false, true);
                    var eq = new Rect(c.x + 10f, c.yMax - 28f, c.width - 20f, 22f);
                    bool hv = eq.Contains(Event.current.mousePosition);
                    var ec = LobbyKit.Gold;
                    CrtKit.SoftBox(eq, ec, variantOn, hv, false);
                    Ui.Label(eq, variantOn ? "USE " + baseDef.Name.ToUpperInvariant() : "EQUIP", 11, CrtKit.SoftText(ec, hv), TextAnchor.MiddleCenter, true);
                    ascEquipRects[i] = eq;
                    ascEquipTo[i] = variantOn ? 0 : Defs.WeaponsPerRole + i;  // (the variants sit at picks 3 and 4)
                    continue;
                }
                if (got)
                    Ui.Label(new Rect(c.x + 10f, c.y + 44f, c.width - 20f, 16f), "UNLOCKED", 11, col, TextAnchor.MiddleLeft, true);
                else
                {
                    float lw2 = Ui.TextWidth("LOCKED", 11, true) - 6f;
                    Ui.Icon(new Rect(c.x + 10f, c.y + 44f, 12f, 12f), PixelArt.Get("ui_lock"), new Color(1f, 1f, 1f, 0.5f));
                    Ui.Label(new Rect(c.x + 26f, c.y + 42f, lw2 + 14f, 16f), "LOCKED", 11, dim, TextAnchor.MiddleLeft, true);
                }
                Ui.Label(new Rect(c.x + 10f, c.y + 62f, c.width - 20f, 44f), "Effect coming soon", 12, dim, TextAnchor.UpperLeft, false, true);
            }
            ascBuyRect = default;
            if (cost >= 0)
            {
                bool afford = prof.Cores >= cost;
                var buy = new Rect(win.center.x - 90f, win.yMax - 58f, 180f, 24f);
                var bc = afford ? LobbyKit.Gold : dim;
                bool hover = buy.Contains(Event.current.mousePosition);
                CrtKit.SoftBox(buy, bc, afford, hover && afford, false);
                Ui.Label(buy, "UNLOCK " + Numerals[owned] + "  -  " + cost + " CORES", 11, afford ? CrtKit.SoftText(bc, hover) : dim, TextAnchor.MiddleCenter, true);
                if (afford) ascBuyRect = buy;
            }
            Ui.Label(new Rect(win.x, win.yMax - 26f, win.width, 18f), "CLICK ANYWHERE ELSE TO CLOSE", 10, CrtUi.Dim, TextAnchor.MiddleCenter, true);
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
        const bool WildcardUnlockedForTesting = true;  // TODO: set false on release so the Profile.WildcardUnlocked requirement applies
        bool WildcardOpen => WildcardUnlockedForTesting || game.Profile == null || game.Profile.WildcardUnlocked;

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

        // The Wildcard's class wheel (Docs/Mockups/wildcard-gear-carousel.html): wildBrowse counts classes without end (its class is
        // wildBrowse mod KitCount), wildBrowsePos eases toward it. wildBrowseFor: whose loadout it was set up for (-1: set it up again).
        int wildBrowse, wildBrowseFor = -1;
        float wildBrowsePos;
        bool detailSecond;  // the detail box and the preview show the Wildcard's second ability (the last ability clicked was the 2nd)
        static int KitMod(int a) => ((a % Defs.KitCount) + Defs.KitCount) % Defs.KitCount;
        int WildBrowseKit => KitMod(wildBrowse);

        /// A Wildcard's picks (Docs/Wildcard.md, look: Docs/Mockups/wildcard-gear-carousel.html): a wheel of the six classes on the
        /// left (arrows, the mouse wheel or the dots turn it), and the class on top's three DPS weapons and three DPS abilities as keys
        /// beside it, with the second ability's key under them. Under those, the loadout: weapon, 1st, 2nd and ultimate, each with
        /// its class's icon; a click turns the wheel to that class (the 1st and 2nd choose which ability an ability key fills).
        /// Fills y to yEnd; the Ultimate row under it lists the wheel's class's ultimates.
        void DrawWildcardPicks(LobbyEntry me, Session tgt, bool self, float x, float y, float w, float yEnd)
        {
            bool calm = fxReduced;
            var ev = Event.current;
            int second = Defs.WildSecond(me.Wild), key = Defs.WildKey(me.Wild);
            const int per = 3;  // weapons and abilities a class (Defs.WeaponsPerRole, Upgrades.RoleAbilityChoices)
            if (wildBrowseFor != me.Id)
            {
                wildBrowseFor = me.Id;
                wildBrowse = (int)Defs.WildWeaponKit(me.Weapon);
                wildBrowsePos = wildBrowse;
                wildTab = 0;
            }
            LobbyKit.Heading(x, y, w, "Gear", "Any class's DPS weapon and two abilities", calm);
            y += 36f;

            // The wheel.
            const float wheelW = 150f;
            float wheelH = Mathf.Max(120f, yEnd - y - 52f);
            var wr = new Rect(x, y, wheelW, wheelH);
            if (ev.type == EventType.ScrollWheel && GUI.enabled && wr.Contains(ev.mousePosition))
            {
                wildBrowse += ev.delta.y > 0f ? 1 : -1;
                ev.Use();
            }
            if (ev.type == EventType.Repaint)
            {
                wildBrowsePos += (wildBrowse - wildBrowsePos) * (calm ? 1f : 1f - Mathf.Exp(-Time.unscaledDeltaTime * 12f));
                if (Mathf.Abs(wildBrowse - wildBrowsePos) < 0.004f)
                    wildBrowsePos = wildBrowse;
                if (Mathf.Abs(wildBrowse) > 600)  // keep the numbers small
                {
                    int sub = wildBrowse / Defs.KitCount * Defs.KitCount;
                    wildBrowse -= sub;
                    wildBrowsePos -= sub;
                }
            }
            int bk = WildBrowseKit;
            var wild = ToColor(Defs.Get(Role.Wildcard).Color);
            CrtKit.SoftBox(wr, CrtUi.Ink, false, false, false);
            float iconY = wr.y + 14f, big = Mathf.Min(64f, wheelH - 70f);
            float oldAlpha = Ui.Alpha;
            for (int k = 0; k < Defs.KitCount; k++)
            {
                float d = ((k - wildBrowsePos) % Defs.KitCount + Defs.KitCount) % Defs.KitCount;
                if (d > Defs.KitCount / 2f)
                    d -= Defs.KitCount;  // -3 .. 3, the class on top at 0
                float ad = Mathf.Abs(d), a = Mathf.Clamp01(1.4f - ad * 0.9f);
                if (a <= 0.01f)
                    continue;
                float sz = Mathf.Lerp(big, big * 0.45f, Mathf.Clamp01(ad));
                var ir = new Rect(wr.center.x + d * 46f - sz / 2f, iconY + (big - sz) / 2f, sz, sz);
                Ui.Alpha = oldAlpha * a;
                if (ad < 0.5f)
                    Ui.Glow(ir, CrtUi.Ink, 0.5f * (1f - ad * 2f));
                Ui.Icon(ir, PixelArt.Get(Classes.Get((Kit)k).Icon), ad < 0.5f ? Color.white : new Color(0.7f, 0.75f, 0.85f, 1f));
            }
            Ui.Alpha = oldAlpha;
            // The class's name between the two arrows.
            const float arrowW = 24f;
            float ny = iconY + big + 6f;
            LobbyKit.Fit(new Rect(wr.x + arrowW + 8f, ny, wr.width - 2f * arrowW - 16f, 24f), Defs.KitName((Kit)bk).ToUpperInvariant(), 15, 9, CrtUi.Ink, TextAnchor.MiddleCenter, calm);
            for (int side = -1; side <= 1; side += 2)
            {
                var ab = new Rect(side < 0 ? wr.x + 5f : wr.xMax - 5f - arrowW, ny, arrowW, 24f);
                bool click = LobbyKit.Key(ab, "", false, calm);
                Chevron(ab.center, side, CrtKit.SoftText(CrtUi.Ink, GUI.enabled && ab.Contains(ev.mousePosition)));
                if (click)
                    wildBrowse += side;
            }
            // The dots: one a class, lit on the class on top, purple where some of your gear comes from. Click one to turn to it.
            const float dot = 7f, dotGap = 9f;
            float dx0 = wr.center.x - (Defs.KitCount * dot + (Defs.KitCount - 1) * dotGap) / 2f, dyy = wr.yMax - 16f;
            int ultKit = (int)me.UltKit;
            for (int k = 0; k < Defs.KitCount; k++)
            {
                var dr = new Rect(dx0 + k * (dot + dotGap), dyy, dot, dot);
                bool used = (int)Defs.WildWeaponKit(me.Weapon) == k || me.Ability / per == k || second / per == k || ultKit == k;
                bool on = k == bk;
                Color dc = on ? CrtUi.Ink : used ? wild : CrtUi.Dim;
                Ui.Fill(dr, new Color(dc.r, dc.g, dc.b, on ? 1f : used ? 0.55f : 0.12f));
                CrtKit.Frame(dr, 1f, new Color(dc.r, dc.g, dc.b, on ? 1f : 0.8f));
                if (on)
                    Ui.Glow(dr, dc, 0.6f);
                var hit = new Rect(dr.x - 4f, dr.y - 5f, dr.width + 8f, dr.height + 10f);
                if (hit.Contains(ev.mousePosition))
                {
                    TipStart(hit, Defs.KitName((Kit)k), Ui.Text);
                    if (Ui.HitArea(hit))
                        TurnWheelTo(k);
                }
            }

            // Beside the wheel: the class's weapons, abilities, then the second ability's key.
            float px = wr.xMax + 12f, pw = x + w - px;
            float cw = (pw - 8f) / 3f;
            LobbyKit.Lab(new Rect(px, y - 2f, pw, 16f), "Weapon", 11, calm);
            int wn = Defs.WeaponCount(Role.Dps, (Kit)bk);  // three, or four with a class's fourth (the Knight's Arc Knuckles)
            float ww = (pw - 4f * (wn - 1)) / wn;
            for (int c = 0; c < wn; c++)
            {
                int idx = Defs.WildWeaponPick((Kit)bk, c);
                var b = new Rect(px + c * (ww + 4f), y + 15f, ww, 32f);
                if (LobbyKit.Key(b, Defs.Weapon(Role.Wildcard, Kit.Soldier, idx).Name.ToUpperInvariant(), me.Weapon == idx, calm, size: 13, minSize: 8))
                {
                    if (me.Weapon != idx)
                        tgt.RequestWildcard(idx, me.Ability, second, key);
                    if (self)
                        weaponChoice = idx;
                    detailSel = 0;
                }
            }
            float ay = y + 55f;
            LobbyKit.Lab(new Rect(px, ay - 2f, 80f, 16f), "Ability", 11, calm);
            string fills = wildTab == 0 ? "CLICK FILLS 1ST" : "CLICK FILLS 2ND";
            Ui.Label(new Rect(px + 80f, ay - 2f, pw - 80f, 16f), fills, 10, wild, TextAnchor.MiddleRight, true);
            for (int c = 0; c < per; c++)
            {
                int idx = bk * per + c;
                var b = new Rect(px + c * (cw + 4f), ay + 15f, cw, 32f);
                int inSlot = me.Ability == idx ? 1 : second == idx ? 2 : 0;
                bool on = inSlot == wildTab + 1;
                if (LobbyKit.Key(b, Upgrades.Ability(Upgrades.RoleAbility(Role.Wildcard, Kit.Soldier, idx)).Name.ToUpperInvariant(), on, calm, size: 13, minSize: 8))
                {
                    // Fill the slot being edited; the other slot's ability swaps over (the two must differ).
                    int a1 = me.Ability, a2 = second;
                    if (wildTab == 0)
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
                        tgt.RequestWildcard(me.Weapon, a1, a2, key);
                    if (self)
                    {
                        abilityChoice = a1;
                        wildSecond = a2;
                    }
                    detailSel = 1;
                    detailSecond = wildTab == 1;
                }
                if (inSlot > 0)
                    SlotBadge(new Rect(b.xMax - 13f, b.y + 2f, 11f, 11f), inSlot, wild);
            }
            float ky = ay + 55f;
            LobbyKit.Lab(new Rect(px, ky, 110f, 24f), "2nd ability key", 11, calm);
            string[] keyNames =
            {
                Keybinds.KeyName(Keybinds.Get(KeyAction.AbilityQ)).ToUpperInvariant(),
                Keybinds.KeyName(Keybinds.Get(KeyAction.AbilityE)).ToUpperInvariant(),
                Keybinds.KeyName(Keybinds.Get(KeyAction.AbilityShift)).ToUpperInvariant(),
            };
            float kw = Mathf.Min(60f, (pw - 114f - 8f) / 3f);
            for (int k = 1; k <= 3; k++)
                if (LobbyKit.Key(new Rect(x + w - 3f * kw - 8f + (k - 1) * (kw + 4f), ky, kw, 24f), keyNames[k - 1], key == k, calm, size: 13, minSize: 9) && key != k)
                {
                    tgt.RequestWildcard(me.Weapon, me.Ability, second, k);
                    if (self)
                        wildKey = k;
                }

            // The loadout: what you have from every class. The 1st and 2nd keys choose which slot an ability click fills.
            float ly = wr.yMax + 8f, lh = Mathf.Max(34f, yEnd - ly);
            float chipW = (w - 3f * 6f) / 4f;
            float isz = Mathf.Min(16f, lh - 12f), tw = chipW - 5f - isz - 5f - 5f;
            int ns = 12;  // one size for all four names
            for (int i = 0; i < 4; i++)
                ns = Mathf.Min(ns, LobbyKit.FitSize(ChipName(me, i, second).ToUpperInvariant(), 12, 8, tw));
            for (int i = 0; i < 4; i++)
            {
                var b = new Rect(x + i * (chipW + 6f), ly, chipW, lh);
                int from = i == 0 ? (int)Defs.WildWeaponKit(me.Weapon) : i == 1 ? me.Ability / per : i == 2 ? second / per : ultKit;
                string name = ChipName(me, i, second);
                string sub = i == 0 ? "WEAPON" : i == 1 ? "1ST · " + Keybinds.KeyName(Keybinds.Get(KeyAction.Ability)).ToUpperInvariant() : i == 2 ? "2ND · " + keyNames[key - 1] : "ULTIMATE · " + Keybinds.KeyName(Keybinds.Get(KeyAction.Ultimate)).ToUpperInvariant();
                bool editing = (i == 1 && wildTab == 0) || (i == 2 && wildTab == 1);
                bool hover = GUI.enabled && b.Contains(ev.mousePosition);
                // The box from a bare key; the class icon on the left, the name over what it is.
                bool click = LobbyKit.Key(b, "", editing, calm, accent: editing ? wild : (Color?)null);
                Color tc = CrtKit.SoftText(editing ? wild : CrtStyle.KeyColor(null, false), editing || hover);
                Ui.Icon(new Rect(b.x + 5f, b.center.y - isz / 2f, isz, isz), PixelArt.Get(Classes.Get((Kit)from).Icon), Color.white);
                float tx = b.x + 5f + isz + 5f;
                string up = name.ToUpperInvariant();
                int ss = LobbyKit.FitSize(sub, 10, 8, tw, false);
                CrtUi.Text(new Rect(tx, b.center.y - 16f, tw, 18f), LobbyKit.Truncate(up, ns, tw), ns, tc, TextAnchor.MiddleLeft, true, calm);
                Ui.Label(new Rect(tx, b.center.y + 1f, tw, 15f), LobbyKit.Truncate(sub, ss, tw, false), ss, new Color(tc.r, tc.g, tc.b, 0.75f), TextAnchor.MiddleLeft, false);
                if (click)
                {
                    TurnWheelTo(from);
                    if (i == 1 || i == 2)
                    {
                        wildTab = i - 1;
                        detailSel = 1;
                        detailSecond = i == 2;
                    }
                    else
                        detailSel = i == 0 ? 0 : 2;
                }
            }
        }

        /// A Wildcard loadout chip's name: 0 the weapon, 1 the first ability, 2 the second, 3 the ultimate.
        static string ChipName(LobbyEntry me, int i, int second) =>
            i == 0 ? Defs.Weapon(Role.Wildcard, Kit.Soldier, me.Weapon).Name
            : i == 3 ? Upgrades.Ability(me.UltId).Name : Upgrades.Ability(Upgrades.RoleAbility(Role.Wildcard, Kit.Soldier, i == 1 ? me.Ability : second)).Name;

        /// A small pixel chevron pointing left (dir -1) or right (1), centred on c.
        static void Chevron(Vector2 c, int dir, Color col)
        {
            for (int i = 0; i < 5; i++)
            {
                float off = (2 - Mathf.Abs(i - 2)) * 2f * dir;
                Ui.Fill(new Rect(Mathf.Round(c.x + off - 1f), Mathf.Round(c.y - 5f + i * 2f), 2f, 2f), col);
            }
        }

        /// Turn the Wildcard's class wheel to class k the short way round.
        void TurnWheelTo(int k)
        {
            int d = KitMod(k - wildBrowse);
            wildBrowse += d > Defs.KitCount / 2 ? d - Defs.KitCount : d;
        }

        /// The small "1" / "2" tag on a Wildcard ability key: which ability slot it's in.
        static void SlotBadge(Rect r, int slot, Color c)
        {
            Ui.Fill(r, c);
            Ui.Label(r, slot.ToString(), 9, new Color(0.05f, 0.02f, 0.1f), TextAnchor.MiddleCenter, true);
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

        const int AscensionsPerWeapon = Variants.Tiers;
        static readonly string[] Numerals = { "I", "II", "III", "IV", "V" };

        /// The "Coming soon" tooltip over the hovered locked slot (call after everything else is drawn).
        void DrawLockedTip()
        {
            if (!lockedTipOn)
                return;
            string text = lockedTipText;
            if (holoTips)
            {
                DrawHoloChip(lockedTip, text);
                return;
            }
            float w = Ui.TextWidth(text, 14, true) + 22f, h = 30f;
            var tip = new Rect(lockedTip.center.x - w / 2f, lockedTip.y - h - 6f, w, h);
            if (tip.y < 4f)
                tip.y = lockedTip.yMax + 6f;
            tip.x = Mathf.Clamp(tip.x, 4f, W - w - 4f);
            Ui.Fill(tip, new Color(CrtUi.Bg.r, CrtUi.Bg.g, CrtUi.Bg.b, 0.98f));
            Ui.Fill(tip, new Color(CrtUi.Ink.r, CrtUi.Ink.g, CrtUi.Ink.b, 0.09f));
            CrtKit.Frame(tip, 1f, new Color(CrtUi.Ink.r, CrtUi.Ink.g, CrtUi.Ink.b, 0.75f));
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
            var muteKey = new Rect(bar.xMax - 50f, bar.y + 1f, 46f, bar.height - 2f);
            var idleKey = new Rect(muteKey.x - 50f, muteKey.y, 46f, muteKey.height);
            var track = new Rect(bar.x + 70f, bar.y, bar.width - 78f - 100f, bar.height);
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
            float nt = Ui.CrtSlider(track, t);
            if (nt != t)
                showcaseZoom = (float)System.Math.Exp(lo + (hi - lo) * nt);
            if (showcaseZoomA > 0.3f && DkKey(muteKey, showcaseMute ? "MUTED" : "MUTE", showcaseMute ? LobbyKit.Gold : Ui.Neutral, false, 10))
                showcaseMute = !showcaseMute;
            if (showcaseZoomA > 0.3f && DkKey(idleKey, "IDLE", showcaseIdle ? LobbyKit.Gold : Ui.Neutral, false, 10))
                showcaseIdle = !showcaseIdle;
            GUI.color = oc;
            Ui.Alpha = oa;
        }

        /// The live mini-arena on the hero card: your loadout in action, zoomed out to fit the dummies.
        void DrawShowcase(LobbyEntry me, Rect view, Color roleColor)
        {
            if ((uiGlBlocked && !pieceGlOn) || hangoutAnim > 0f)
                return;  // (hangoutAnim: the window is turning into the Hangout, which draws its own floor and ship; pieceGlOn: the lobby's entrance fades the preview in with the loadout)
            // the lobby sliding away as a run launches: no live preview (it would start up again) (LaunchIntro.cs)
            int shown = ShownAbility(me);
            bool ult = detailSel == 2;  // what you last clicked (a weapon, an ability or the ultimate) is what it shows
            bool demo = detailSel != 0;
            if (showcase == null)
                showcase = new Showcase();
            showcase.Show(me.Role, me.Kit, me.Weapon, Upgrades.RoleAbility(me.Role, me.Kit, shown), demo, ult, me.Ult);
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
                var ab = Upgrades.Ability(ult ? me.UltId : Upgrades.RoleAbility(me.Role, me.Kit, shown));
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
            showcase.Idle = showcaseIdle;
            showcase.Step(dt);
            // Its sounds play muffled, as if heard through the preview window's glass (nothing else is drawn alongside it)
            foreach (var sfx in showcase.Game.Sounds)
                if (!showcaseMute)
                    sounds.PlayMuffled(sfx.Sfx, sfx.Volume, Time.unscaledTime);
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
