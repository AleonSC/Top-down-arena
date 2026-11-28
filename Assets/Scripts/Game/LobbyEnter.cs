using UnityEngine;

namespace Arena
{
    /// Going from the main menu into the lobby ("the card becomes your loadout", Docs/Mockups/host-to-lobby.html option A),
    /// about 1.6 s, after Host game or once a join connects:
    ///   0.0 s   the menu card empties and grows into the loadout panel, its plot corners riding out with it; the title
    ///           shrinks and flies to the top left, turning into LOBBY, its x-axis coming along and fading; the buttons
    ///           under the card drop away
    ///   0.45 s  the lobby's pieces slide in around the panel (with effects on they holo-boot in place instead, LobbyFx.cs): the mode switch, party and background picker from the
    ///           left, the tabs from above, the bottom bar from below; the loadout fills in
    ///   1.1 s   your party card arrives last, with a ring
    /// The lobby isn't usable until it settles (it's drawn on Repaint only, so it takes no clicks; its live models wait).
    /// With Reduced effects or Visual effects off, Leave plays it backwards (1 s, without the party card's ring; with effects on
    /// it's the CRT power-cycle of LobbyCrtLeave.cs): your card goes first, the pieces slide out, the
    /// panel shrinks back into the menu card and LOBBY flies back to the middle as the title. You actually leave as it
    /// ends (StartLobbyLeave), so the lobby is still there to draw.
    public partial class ArenaApp
    {
        const float EnterLen = 1.6f, LeaveLen = 1f, LeaveFrom = 1.15f;  // leaving runs the entrance from 1.15 s back to 0
        const float EnterMaxStep = 1f / 30f;  // the entrance clock's longest step: a hitch (the first launch's host start and first draws) pauses it instead of jumping ahead
        bool pieceGlOn;                           // a lobby piece is drawing its models (BeginUiGl)
        Rect pieceGlClip;
        float enterT = -1f;                       // seconds since the lobby opened from the menu; below 0 when not entering
        float leaveT = -1f;                       // seconds since Leave was pressed; below 0 when not leaving
        AppScreen enterLastScreen = AppScreen.Menu;

        /// Leaving the lobby for the menu, the entrance played backwards (LobbyUi's Leave button).
        bool LobbyLeaving => leaveT >= 0f && session.Screen == AppScreen.Lobby;

        void StartLobbyLeave()
        {
            if (leaveT >= 0f)
                return;
            leaveT = 0f;
            enterT = -1f;
            crtEntry = false;  // (leaving mid New run boot: the next entrance is the plain one, and the leave isn't tinted blue)
            crtLeave = LobbyFxOn;  // with effects on: the CRT power-cycle (LobbyCrtLeave.cs)
            settingsOpen = false;
            // The card shrinks back into the menu's box; show the state the menu will reset to (the buttons, not the Play
            // picker you may have launched from) so it doesn't jump when the menu takes over (MenuIntro.cs does the same reset).
            menuPlayOpen = false;
            menuPlayK = 0f;
        }

        /// Coming into the lobby from the menu right now, including the frame the Host click lands on (before Update ticks).
        bool LobbyEntering => session.Screen == AppScreen.Lobby && (enterT >= 0f || enterLastScreen == AppScreen.Menu);

        /// Update: notice the lobby opening from the menu, and run the clock.
        void TickLobbyEnter(float dt)
        {
            if (leaveT >= 0f)
            {
                if (session.Screen != AppScreen.Lobby)
                    leaveT = -1f;  // gone some other way (the host closed it)
                else if ((leaveT += dt) >= LeaveLenNow)
                {
                    leaveT = -1f;
                    session.Leave("");  // the menu's last frame is where this ends
                }
            }
            if (session.Screen == AppScreen.Lobby && enterLastScreen == AppScreen.Menu)
            {
                enterT = 0f;
                ResetLobbyUi();  // (LobbyUi.cs: no window or typed name left over from the last lobby)
            }
            enterLastScreen = session.Screen;
            if (enterT < 0f)
                return;
            float enterPrev = enterT;
            enterT += Mathf.Min(dt, EnterMaxStep);
            SkipCrtEntry();  // LobbyCrt.cs
            if (crtEntry)
                PlayCrtEntryCues(enterPrev, enterT);  // the boot's sounds (LobbyCrt.cs)
            if (session.Screen != AppScreen.Lobby || enterT >= (crtEntry ? EnterCrtLen : EnterLen))
            {
                enterT = -1f;
                crtEntry = false;
            }
        }

        static float EaseInOut(float x) => x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;

        static Rect LerpRect(Rect a, Rect b, float k) =>
            new Rect(MathX.Lerp(a.x, b.x, k), MathX.Lerp(a.y, b.y, k), MathX.Lerp(a.width, b.width, k), MathX.Lerp(a.height, b.height, k));

        /// Instead of DrawLobby while leaving: the entrance, backwards.
        void DrawLobbyLeavingToMenu() => DrawLobbyEntering(LeaveFrom * (1f - Mathf.Clamp01(leaveT / LeaveLen)), true);

        /// Instead of DrawLobby while entering (or, run backwards, leaving).
        void DrawLobbyEntering() => DrawLobbyEntering(Mathf.Max(0f, enterT), false);

        void DrawLobbyEntering(float e, bool leaving)
        {
            float t = Time.unscaledTime;
            var oldColor = GUI.color;
            float k = EaseInOut(Seg(e, 0f, 0.7f));
            // Continuing a saved run: the Seats tab has no box to grow into, so the card stays put and fades out instead.
            bool fade = session.Resume != null;
            float cardA = fade ? 1f - Seg(e, 0f, 0.5f) : 1f;
            bool modeScreen = OnModeScreen;  // a new lobby opens on the Game Mode screen (ModeScreenUi.cs): the card grows into its page
            var r = fade ? MenuCard : LerpRect(MenuCard, modeScreen ? ModePageRect : LobbyBodyRect, k);

            // The machine (the monitor and the case, MenuMachine.cs) fades as the bay's screen grows out of it into the loadout
            // panel (leaving plays this backwards: the machine comes back round the panel as it settles into the bay).
            float machA = fade ? 1f - Seg(e, 0.1f, 0.4f) : 1f - Seg(k, 0f, 0.35f);
            if (machA > 0f)
                WithAlpha(machA, () =>
                {
                    DrawMenuMachine(99f, false, t, false);  // (its title is the one flying off to become LOBBY, below)
                    DrawMenuSecondary(t, false);
                });

            // The card grows into the loadout panel; its insides fade at once.
            if (fade)
            {
                if (cardA > 0f)
                    WithAlpha(cardA, () => DkPanel(r));
            }
            else if (k < 1f || e < 1.05f)
                DkPanel(r);
            float empty = Seg(e, 0f, 0.15f);
            if (empty < 1f)
                WithAlpha(1f - empty, () => DrawMenuCardInside(t, false));

            DrawLobbyPieces(e, false);
            GUI.color = oldColor;

            // The title flies to the top left, shrinking, and turns into LOBBY (or GAME MODE) on the way.
            float tk = EaseInOut(Seg(e, 0f, 0.6f));
            var to = LobbyTitleAnchor;  // where the small LOBBY title's text sits (LobbyLayout.cs)
            float cx = MathX.Lerp(W / 2f, to.X, tk), cy = MathX.Lerp(MenuTitleY, to.Y, tk), size = MathX.Lerp(MenuTitleSize, 18f, tk);
            float oldA = 1f - Seg(tk, 0.2f, 0.42f), newA = Seg(tk, 0.38f, 0.6f);
            if (oldA > 0f)
                WithAlpha(oldA, () => Ui.Glitch(new Rect(cx - W / 2f, cy - size, W, size * 2f), CrtTitleText, Mathf.RoundToInt(size), Color.white, TextAnchor.MiddleCenter, Ui.Accent));
            var titleR = new Rect(LobbyTitleRect.x + (cx - to.X), LobbyTitleRect.y + (cy - to.Y), LobbyTitleRect.width, LobbyTitleRect.height);
            if (newA > 0f && modeScreen)
                WithAlpha(newA, () => DrawModeScreenTitle(titleR, 1f));
            else if (newA > 0f)
                WithAlpha(newA, () => DrawLobbyTitle(titleR, 1f, fxReduced, t));  // (the lobby's own title, so the hand-over at the end shows no change)

            // GL: the card's plot corners and the ring off your party card.
            if (BeginUiGl())
            {
                float neonA = fade ? cardA : 1f - Seg(e, 0f, 0.3f);  // the box's neon edge fades as it starts to grow (MenuHolo.cs)
                if (neonA > 0f)
                    DrawBoxNeon(r, neonA);
                float cornersA = fade ? cardA : 1f - Seg(e, 0.75f, 1.1f);
                if (cornersA > 0f)
                    DrawPlotCorners(r, 7f, 22f, new Rgba(0.45f, 0.65f, 1f, 0.7f * cornersA));
                float ring = Seg(e, 1.1f, 1.6f);
                if (ring > 0f && ring < 1f && !leaving && !modeScreen)
                    DrawPartyCardRing(ring);
                EndUiGl();
            }
        }

        /// The lobby's pieces sliding in and fading up (title: the LOBBY title is one of them), e seconds into the entrance.
        void DrawLobbyPieces(float e, bool title)
        {
            // The lobby, in pieces: each slides in from its side and fades up. Not usable yet: the pieces are drawn on
            // Repaint only, so nothing in them takes a click (GUI.enabled = false would also draw them half see-through).
            // Its live models wait.
            uiGlBlocked = true;
            if (OnModeScreen)
            {
                // The Game Mode screen's pieces (ModeScreenUi.cs): Back, the carousel and TO THE LOBBY from above, then its page.
                LobbyPiece(LobbyBackBand, e, 0.5f, 0.8f, -60f, 0f);
                if (title)
                    LobbyPiece(LobbyTitleBand, e, 0.45f, 0.8f, 0f, -20f);
                LobbyPiece(ModeCarouselBand, e, 0.5f, 0.85f, 0f, -30f);
                LobbyPiece(ModeGoBand, e, 0.6f, 0.95f, 0f, -30f);
                LobbyPiece(ModePageBand, e, 0.65f, 1.05f, 0f, 0f, true);
                uiGlBlocked = false;
                return;
            }
            // (the bands are LobbyLayout.cs's, so they follow the boxes)
            LobbyPiece(LobbyBackBand, e, 0.5f, 0.8f, -60f, 0f);            // the Back key
            if (title)
                LobbyPiece(LobbyTitleBand, e, 0.45f, 0.8f, 0f, -20f);      // the LOBBY title (the menu's entrance flies it in instead)
            LobbyPiece(LobbyInfoBand, e, 0.55f, 0.8f, 0f, -20f);           // the "!" by the title (the title itself flies in)
            LobbyPiece(LobbyModeBand, e, 0.45f, 0.85f, -60f, 0f);          // the mode keys
            LobbyPiece(LobbyPartyLineBand, e, 0.5f, 0.9f, -60f, 0f);       // "Party (1/3)" and Hangout
            LobbyPiece(LobbyOtherSlotsBand, e, 0.6f, 1.0f, -60f, 0f);      // the other slots
            LobbyPiece(LobbyActionsBand, e, 0.7f, 1.1f, -60f, 0f);         // Ready and Start
            LobbyPiece(LobbyYourCardBand, e, 0.8f, 1.15f, -60f, 0f, true); // your party card, last
            LobbyPiece(LobbyTabsBand, e, 0.55f, 0.9f, 0f, -30f);           // the tab keys
            LobbyPiece(LobbyBodyBand, e, 0.65f, 1.05f, 0f, 0f, true);      // the loadout, filling in (its live preview fades up with it instead of popping in after)
            uiGlBlocked = false;
        }

        /// GL (inside BeginUiGl): a ring growing off your party card and fading, `ring` 0 to 1 (also Recall.cs).
        void DrawPartyCardRing(float ring)
        {
            UiModelAt(V2.Zero, 1f);
            var c = new Rgba(0.35f, 0.59f, 1f, 1f - ring);
            float g = 10f * ring;
            V2 a0 = new V2(PartyX - g, PartyTop - g), a1 = new V2(PartyX + PartyW + g, PartyTop - g), a2 = new V2(PartyX + PartyW + g, PartyTop + PartyCardH + g), a3 = new V2(PartyX - g, PartyTop + PartyCardH + g);
            Line(a0, a1, 3f, c); Line(a1, a2, 3f, c); Line(a2, a3, 3f, c); Line(a3, a0, 3f, c);
        }

        /// One piece of the lobby: DrawLobby clipped to `clip`, sliding in from (dx, dy) and fading up between a and b seconds.
        /// With effects on it holo-boots in place instead (LobbyFx.cs): a scan line wipes down the piece between a and b,
        /// showing it as it goes, then it flickers.
        void LobbyPiece(Rect clip, float e, float a, float b, float dx, float dy, bool models = false)
        {
            float k = Seg(e, a, b);
            if (k <= 0f || Event.current.type != EventType.Repaint)
                return;
            bool holo = LobbyFxOn;
            float s = EaseOut(k);
            var shown = clip;
            if (holo)
            {
                dx = dy = 0f;
                s = 1f;
                k = 1f;
                shown.height = clip.height * HoloBoot.Reveal(e - a, b - a);
            }
            var old = GUI.color;
            Ui.Alpha = k;
            GUI.color = old.ScaleAlpha(k);
            GUI.BeginGroup(new Rect(shown.x + dx * (1f - s), shown.y + dy * (1f - s), shown.width, shown.height));
            GUI.BeginGroup(new Rect(-clip.x, -clip.y, W, H));
            if (models)  // this piece's live models slide and fade with it, cut to its rect (the other pieces' models wait)
            {
                pieceGlOn = true;
                pieceGlClip = shown;
                uiShift = new V2(dx * (1f - s), dy * (1f - s));
            }
            DrawLobby();
            pieceGlOn = false;
            uiShift = V2.Zero;
            GUI.EndGroup();
            GUI.EndGroup();
            Ui.Alpha = 1f;
            GUI.color = old;
            if (holo)
                HoloBoot.Over(clip, e - a, b - a);
        }

        /// Draws at a times the current opacity (nests: a fade inside a fade multiplies).
        void WithAlpha(float a, System.Action draw)
        {
            var old = GUI.color;
            float oldA = Ui.Alpha;
            Ui.Alpha = oldA * a;
            GUI.color = old.ScaleAlpha(a);
            draw();
            Ui.Alpha = oldA;
            GUI.color = old;
        }
    }
}
