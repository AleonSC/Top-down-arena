using UnityEngine;

namespace Arena
{
    /// Going from the main menu into the lobby ("the card becomes your loadout", Docs/Mockups/host-to-lobby.html option A),
    /// about 1.6 s, after Host game or once a join connects:
    ///   0.0 s   the menu card empties and grows into the loadout panel, its plot corners riding out with it; the title
    ///           shrinks and flies to the top left, turning into LOBBY, its x-axis coming along and fading; the buttons
    ///           under the card drop away
    ///   0.45 s  the lobby's pieces slide in around the panel: the mode switch, party and background picker from the
    ///           left, the tabs from above, the bottom bar from below; the loadout fills in
    ///   1.1 s   your party card arrives last, with a ring
    /// The lobby isn't usable until it settles (it's drawn on Repaint only, so it takes no clicks; its live models wait).
    /// Leave plays it backwards (1 s, without the party card's ring): your card goes first, the pieces slide out, the
    /// panel shrinks back into the menu card and LOBBY flies back to the middle as the title. You actually leave as it
    /// ends (StartLobbyLeave), so the lobby is still there to draw.
    public partial class ArenaApp
    {
        const float EnterLen = 1.6f, LeaveLen = 1f, LeaveFrom = 1.15f;  // leaving runs the entrance from 1.15 s back to 0
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
            settingsOpen = false;
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
                else if ((leaveT += dt) >= LeaveLen)
                {
                    leaveT = -1f;
                    session.Leave("");  // the menu's last frame is where this ends
                }
            }
            if (session.Screen == AppScreen.Lobby && enterLastScreen == AppScreen.Menu)
                enterT = 0f;
            enterLastScreen = session.Screen;
            if (enterT < 0f)
                return;
            enterT += dt;
            if (session.Screen != AppScreen.Lobby || enterT >= EnterLen)
                enterT = -1f;
        }

        static float EaseInOut(float x) => x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;

        static Rect LerpRect(Rect a, Rect b, float k) =>
            new Rect(Lerp(a.x, b.x, k), Lerp(a.y, b.y, k), Lerp(a.width, b.width, k), Lerp(a.height, b.height, k));

        /// The loadout panel the menu card grows into (DrawLobby's body).
        static Rect LobbyBodyRect => new Rect(440f, LobbyBodyTop, W - 480f, H - LobbyBottomPad - LobbyBodyTop);

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
            var r = fade ? MenuCard : LerpRect(MenuCard, LobbyBodyRect, k);
            if (fade ? cardA > 0f : (k < 1f || e < 1.05f))
                Ui.BodyGlow(r, fade ? cardA : 1f - k);  // first, so it sits under everything else; it fades as the card becomes the lobby's box, which has none

            // The bottom bar (your name card with it) drops away and fades; the tip tray fades.
            float drop = Seg(e, 0f, 0.35f);
            if (drop < 1f)
                WithAlpha(1f - drop, () =>
                {
                    GUI.BeginGroup(new Rect(0f, drop * drop * drop * 160f, W, H));
                    DrawMenuSecondary(t, false);
                    GUI.EndGroup();
                    DrawMenuTipTray(t, 1f);
                });

            // The card grows into the loadout panel; its insides fade at once.
            if (fade)
            {
                if (cardA > 0f)
                    WithAlpha(cardA, () => Ui.BodyBox(r));
            }
            else if (k < 1f || e < 1.05f)
                Ui.BodyBox(r);
            float empty = Seg(e, 0f, 0.15f);
            if (empty < 1f)
                WithAlpha(1f - empty, () => DrawMenuCardInside(t, false));

            // The lobby, in pieces: each slides in from its side and fades up. Not usable yet: the pieces are drawn on
            // Repaint only, so nothing in them takes a click (GUI.enabled = false would also draw them half see-through).
            // Its live models wait.
            uiGlBlocked = true;
            const float split = 430f;
            LobbyPiece(new Rect(0f, 0f, 100f, 68f), e, 0.5f, 0.8f, -60f, 0f);                      // the back button
            LobbyPiece(new Rect(240f, 0f, split - 240f, 68f), e, 0.55f, 0.8f, 0f, -20f);          // the "!" by the title (the title itself flies in)
            LobbyPiece(new Rect(0f, 68f, split, 80f), e, 0.45f, 0.85f, -60f, 0f);                  // the mode switch
            LobbyPiece(new Rect(0f, 148f, split, PartyTop - 148f), e, 0.5f, 0.9f, -60f, 0f);      // "Party (1/3)"
            LobbyPiece(new Rect(0f, PartyTop + 96f, split, LobbyNoteY - 4f - (PartyTop + 96f)), e, 0.6f, 1.0f, -60f, 0f);  // the other slots
            LobbyPiece(new Rect(0f, LobbyNoteY - 4f, split, H - LobbyNoteY + 4f), e, 0.7f, 1.1f, -60f, 0f);  // Ready and Start
            LobbyPiece(new Rect(0f, PartyTop - 6f, split, 102f), e, 0.8f, 1.15f, -60f, 0f);       // your party card, last
            LobbyPiece(new Rect(split, 0f, W - split, LobbyBodyTop - 2f), e, 0.55f, 0.9f, 0f, -30f);  // the tabs
            LobbyPiece(new Rect(split, LobbyBodyTop - 2f, W - split, H - LobbyBottomPad - LobbyBodyTop + 4f), e, 0.65f, 1.05f, 0f, 0f);  // the loadout, filling in
            uiGlBlocked = false;
            GUI.color = oldColor;

            // The title flies to the top left, shrinking, and turns into LOBBY on the way.
            float tk = EaseInOut(Seg(e, 0f, 0.6f));
            float cx = Lerp(W / 2f, 199f, tk), cy = Lerp(MenuTitleY, 41f, tk), size = Lerp(MenuTitleSize, 40f, tk);
            float oldA = 1f - Seg(tk, 0.2f, 0.42f), newA = Seg(tk, 0.38f, 0.6f);
            if (oldA > 0f)
                WithAlpha(oldA, () => Ui.Glitch(new Rect(cx - W / 2f, cy - size, W, size * 2f), "CARTESIAN COLOSSEUM", Mathf.RoundToInt(size), Color.white, TextAnchor.MiddleCenter, Ui.Accent, Ui.GlitchKind.Punch));
            if (newA > 0f)
                WithAlpha(newA, () => Ui.Glitch(new Rect(104f + (cx - 199f), 16f + (cy - 41f), 300f, 50f), "LOBBY", 40, Color.white, TextAnchor.MiddleLeft, Ui.Accent));

            // GL: the axis riding along under the title, the card's plot corners, and the ring off your party card.
            if (BeginUiGl())
            {
                float axisA = 1f - Seg(e, 0.5f, 0.8f);
                if (axisA > 0f)
                    DrawAxis(new V2(cx, Lerp(MenuAxisY, 70f, tk)), Lerp(MenuAxisHalf, 100f, tk), t, axisA);
                float arcA = 1f - Seg(e, 0f, 0.3f);  // the shot's arc over the title fades
                if (arcA > 0f)
                    DrawMenuArc(t, arcA);
                float cornersA = fade ? cardA : 1f - Seg(e, 0.75f, 1.1f);
                if (cornersA > 0f)
                    DrawPlotCorners(r, 7f, 22f, new Rgba(0.45f, 0.65f, 1f, 0.7f * cornersA));
                float ring = Seg(e, 1.1f, 1.6f);
                if (ring > 0f && ring < 1f && !leaving)
                    DrawPartyCardRing(ring);
                EndUiGl();
            }
        }

        /// GL (inside BeginUiGl): a ring growing off your party card and fading, `ring` 0 to 1 (also Recall.cs).
        void DrawPartyCardRing(float ring)
        {
            UiModelAt(V2.Zero, 1f);
            var c = new Rgba(0.35f, 0.59f, 1f, 1f - ring);
            float g = 10f * ring;
            V2 a0 = new V2(40f - g, PartyTop - g), a1 = new V2(416f + g, PartyTop - g), a2 = new V2(416f + g, PartyTop + 88f + g), a3 = new V2(40f - g, PartyTop + 88f + g);
            Line(a0, a1, 3f, c); Line(a1, a2, 3f, c); Line(a2, a3, 3f, c); Line(a3, a0, 3f, c);
        }

        /// One piece of the lobby: DrawLobby clipped to `clip`, sliding in from (dx, dy) and fading up between a and b seconds.
        void LobbyPiece(Rect clip, float e, float a, float b, float dx, float dy)
        {
            float k = Seg(e, a, b);
            if (k <= 0f || Event.current.type != EventType.Repaint)
                return;
            float s = EaseOut(k);
            var old = GUI.color;
            Ui.Alpha = k;
            GUI.color = new Color(old.r, old.g, old.b, old.a * k);
            GUI.BeginGroup(new Rect(clip.x + dx * (1f - s), clip.y + dy * (1f - s), clip.width, clip.height));
            GUI.BeginGroup(new Rect(-clip.x, -clip.y, W, H));
            DrawLobby();
            GUI.EndGroup();
            GUI.EndGroup();
            Ui.Alpha = 1f;
            GUI.color = old;
        }

        void WithAlpha(float a, System.Action draw)
        {
            var old = GUI.color;
            Ui.Alpha = a;
            GUI.color = new Color(old.r, old.g, old.b, old.a * a);
            draw();
            Ui.Alpha = 1f;
            GUI.color = old;
        }
    }
}
