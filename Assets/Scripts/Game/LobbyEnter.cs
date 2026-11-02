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
    public partial class ArenaApp
    {
        const float EnterLen = 1.6f;
        float enterT = -1f;                       // seconds since the lobby opened from the menu; below 0 when not entering
        AppScreen enterLastScreen = AppScreen.Menu;

        /// Coming into the lobby from the menu right now, including the frame the Host click lands on (before Update ticks).
        bool LobbyEntering => session.Screen == AppScreen.Lobby && (enterT >= 0f || enterLastScreen == AppScreen.Menu);

        /// Update: notice the lobby opening from the menu, and run the clock.
        void TickLobbyEnter(float dt)
        {
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
        static Rect LobbyBodyRect => new Rect(440f, LobbyBodyTop, W - 480f, H - 96f - LobbyBodyTop);

        /// Instead of DrawLobby while entering.
        void DrawLobbyEntering()
        {
            float e = Mathf.Max(0f, enterT), t = Time.unscaledTime;
            var oldColor = GUI.color;

            // The buttons under the card drop away.
            float drop = Seg(e, 0f, 0.35f);
            if (drop < 1f)
                WithAlpha(1f - drop, () =>
                {
                    GUI.BeginGroup(new Rect(0f, drop * drop * drop * 160f, W, H));
                    DrawMenuSecondary(t, false);
                    GUI.EndGroup();
                });

            // The card grows into the loadout panel; its insides fade at once.
            float k = EaseInOut(Seg(e, 0f, 0.7f));
            var r = LerpRect(MenuCard, LobbyBodyRect, k);
            if (k < 1f || e < 1.05f)
                Ui.Panel(r, true);
            float empty = Seg(e, 0f, 0.15f);
            if (empty < 1f)
                WithAlpha(1f - empty, () => DrawMenuCardInside(t, false));

            // The lobby, in pieces: each slides in from its side and fades up. Not usable yet: the pieces are drawn on
            // Repaint only, so nothing in them takes a click (GUI.enabled = false would also draw them half see-through).
            // Its live models wait.
            uiGlBlocked = true;
            const float split = 430f;
            LobbyPiece(new Rect(180f, 0f, split - 180f, 68f), e, 0.55f, 0.8f, 0f, -20f);          // the "!" by the title (the title itself flies in)
            LobbyPiece(new Rect(0f, 68f, split, 80f), e, 0.45f, 0.85f, -60f, 0f);                  // the mode switch
            LobbyPiece(new Rect(0f, 148f, split, PartyTop - 148f), e, 0.5f, 0.9f, -60f, 0f);      // "Party (1/3)"
            LobbyPiece(new Rect(0f, PartyTop + 96f, split, H - 180f - PartyTop - 104f), e, 0.6f, 1.0f, -60f, 0f);  // the other slots
            LobbyPiece(new Rect(0f, H - 186f, split, 96f), e, 0.7f, 1.1f, -60f, 0f);              // the background picker
            LobbyPiece(new Rect(0f, PartyTop - 6f, split, 102f), e, 0.8f, 1.15f, -60f, 0f);       // your party card, last
            LobbyPiece(new Rect(split, 0f, W - split, LobbyBodyTop - 2f), e, 0.55f, 0.9f, 0f, -30f);  // the tabs
            LobbyPiece(new Rect(split, LobbyBodyTop - 2f, W - split, H - 96f - LobbyBodyTop + 4f), e, 0.65f, 1.05f, 0f, 0f);  // the loadout, filling in
            LobbyPiece(new Rect(0f, H - 92f, W, 92f), e, 0.6f, 1.05f, 0f, 60f);                   // the bottom bar
            uiGlBlocked = false;
            GUI.color = oldColor;

            // The title flies to the top left, shrinking, and turns into LOBBY on the way.
            float tk = EaseInOut(Seg(e, 0f, 0.6f));
            float cx = Lerp(W / 2f, 135f, tk), cy = Lerp(102f, 41f, tk), size = Lerp(58f, 40f, tk);
            float oldA = 1f - Seg(tk, 0.2f, 0.42f), newA = Seg(tk, 0.38f, 0.6f);
            if (oldA > 0f)
                WithAlpha(oldA, () => Ui.Fancy(new Rect(cx - W / 2f, cy - size, W, size * 2f), "CARTESIAN COLOSSEUM", Mathf.RoundToInt(size), Color.white, new Color(0.72f, 0.8f, 1f), TextAnchor.MiddleCenter, Ui.Accent));
            if (newA > 0f)
                WithAlpha(newA, () => Ui.Fancy(new Rect(40f + (cx - 135f), 16f + (cy - 41f), 300f, 50f), "LOBBY", 40, Color.white, new Color(0.72f, 0.8f, 1f), TextAnchor.MiddleLeft, Ui.Accent));

            // GL: the axis riding along under the title, the card's plot corners, and the ring off your party card.
            if (BeginUiGl())
            {
                float axisA = 1f - Seg(e, 0.5f, 0.8f);
                if (axisA > 0f)
                    DrawAxis(new V2(cx, Lerp(158f, 70f, tk)), Lerp(380f, 100f, tk), t, axisA);
                float cornersA = 1f - Seg(e, 0.75f, 1.1f);
                if (cornersA > 0f)
                    DrawPlotCorners(r, 7f, 22f, new Rgba(0.45f, 0.65f, 1f, 0.7f * cornersA));
                float ring = Seg(e, 1.1f, 1.6f);
                if (ring > 0f && ring < 1f)
                {
                    UiModelAt(V2.Zero, 1f);
                    var c = new Rgba(0.35f, 0.59f, 1f, 1f - ring);
                    float g = 10f * ring;
                    V2 a0 = new V2(40f - g, PartyTop - g), a1 = new V2(416f + g, PartyTop - g), a2 = new V2(416f + g, PartyTop + 88f + g), a3 = new V2(40f - g, PartyTop + 88f + g);
                    Line(a0, a1, 3f, c); Line(a1, a2, 3f, c); Line(a2, a3, 3f, c); Line(a3, a0, 3f, c);
                }
                EndUiGl();
            }
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
