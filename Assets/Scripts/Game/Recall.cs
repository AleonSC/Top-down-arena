using UnityEngine;

namespace Arena
{
    /// Going back to the lobby from a run ("beam out"): the run launch (LaunchIntro.cs) played the other way.
    /// The host starts it (the pause menu's Leave to lobby, once confirmed, or Back to lobby on the results screen) with
    /// Session.StartRecall, so it plays on every screen:
    ///   0.0 s   the pause menu and the HUD slide away; nobody can move or shoot
    ///   0.2 s   a pillar of light in each player's colour comes down onto them, a few tenths apart; as it peaks they
    ///           vanish up it with a ring and a flash, and it fades
    ///   1.0 s   a ring closes in on the middle of the arena as the screen fades to dark
    ///   1.7 s   the host calls ReturnToLobby (a client stays dark until its ToLobby arrives)
    /// Then, in the lobby (1.2 s): the dark lifts and the lobby's two sides slide back in, its party column from the
    /// left and the rest from the right (the way they left at the launch), then a ring round your party card.
    /// The lobby isn't usable until it settles (drawn on Repaint only, like LobbyEnter.cs).
    public partial class ArenaApp
    {
        const float RecallLen = 1.7f, RecallPillarAt = 0.2f, RecallStep = 0.18f, RecallLift = 0.3f, RecallPillarLife = 0.7f,
            RecallFadeAt = 1.0f, RecallFadeEnd = 1.6f, ReturnLen = 1.3f;

        float recallT = -1f;   // seconds since the beam-out began; below 0 when there's none
        float returnT = -1f;   // seconds since the lobby came back after one; below 0 when it isn't coming in
        int recallsSeen;
        bool recallSent;       // the host has called ReturnToLobby for this one
        bool pauseConfirmLobby;  // the pause menu asking "back to the lobby?" (MenuDrawer.cs)
        int confirmSel = 1;

        bool RecallActive => recallT >= 0f && session.Screen == AppScreen.Game;

        /// The lobby coming back after a beam-out.
        bool LobbyReturning => returnT >= 0f && session.Screen == AppScreen.Lobby;

        /// The host's way back to the lobby from a run: everyone's beam-out, then ReturnToLobby.
        void RecallToLobby()
        {
            if (!session.IsHost || RecallActive)
                return;
            session.StartRecall();
        }

        /// Update: notice a beam-out starting, run its clock (the host leaves when it's done), then the lobby's return.
        void TickRecall(float dt)
        {
            if (session.Recalls != recallsSeen)
            {
                recallsSeen = session.Recalls;
                if (session.Screen == AppScreen.Game && recallT < 0f)
                {
                    recallT = 0f;
                    recallSent = false;
                    pauseMenuOpen = false;  // everything over the arena gets out of the way
                    pauseSettings = pauseHowTo = pauseRecords = pauseConfirmLobby = false;
                    runStatsOpen = false;
                }
            }
            if (recallT >= 0f)
            {
                if (session.Screen == AppScreen.Lobby)
                {
                    recallT = -1f;
                    returnT = 0f;
                    game.HoldControls = false;
                }
                else if (session.Screen != AppScreen.Game)
                {
                    recallT = -1f;
                    game.HoldControls = false;
                }
                else
                {
                    float before = recallT;
                    recallT += dt;
                    game.HoldControls = true;
                    RecallSounds(before, recallT);
                    if (session.IsHost && recallT >= RecallLen && !recallSent)
                    {
                        recallSent = true;
                        session.ReturnToLobby();
                    }
                }
            }
            if (returnT >= 0f)
            {
                float before = returnT;
                returnT += dt;
                if (Passed(before, returnT, 0.15f))
                    sounds.Play(Sfx.LaunchSweep, 0.7f, Time.unscaledTime);
                if (Passed(before, returnT, 0.85f))
                    sounds.Play(Sfx.LaunchHud, 0.6f, Time.unscaledTime);
                if (session.Screen != AppScreen.Lobby || returnT >= ReturnLen)
                    returnT = -1f;
            }
        }

        void RecallSounds(float before, float now)
        {
            float clock = Time.unscaledTime;
            foreach (var p in session.Game.Players.Values)
            {
                float start = RecallStart(p);
                if (Passed(before, now, start))
                    sounds.Play(Sfx.LaunchBeam, 0.7f, clock);
                if (Passed(before, now, start + RecallLift))
                    sounds.Play(Sfx.LaunchLand, 0.75f, clock);
            }
            if (Passed(before, now, RecallFadeAt))
                sounds.Play(Sfx.LaunchRipple, 0.7f, clock);
        }

        /// When this player's pillar starts coming down (players in id order).
        float RecallStart(Player p)
        {
            int i = 0;
            foreach (var q in session.Game.Players.Values)
            {
                if (q == p)
                    break;
                i++;
            }
            return RecallPillarAt + i * RecallStep;
        }

        /// Gone up their pillar (the real match only).
        bool RecallHides(Player p) => RecallActive && ReferenceEquals(game, session.Game) && recallT >= RecallStart(p) + RecallLift;

        /// In the world, over the floor: each player's pillar and the ring they leave with, and the ring closing in.
        void DrawRecallWorld(float t)
        {
            if (!RecallActive || !ReferenceEquals(game, session.Game))
                return;
            foreach (var p in game.Players.Values)
            {
                float start = RecallStart(p), lift = start + RecallLift, k = Seg(recallT, start, start + RecallPillarLife);
                var col = p.Def.Color;
                if (k > 0f && k < 1f)
                {
                    // Comes down bright and wide; once they've gone up it, it narrows away.
                    float a = Mathf.Sin(MathX.Pi * k), w = 40f * (recallT < lift ? 0.6f + 0.4f * Seg(recallT, start, lift) : 1f - 0.8f * Seg(recallT, lift, start + RecallPillarLife));
                    DrawPlayerBeam(p, a, w);
                    if (recallT < lift)  // the halo tightening round them as it takes them
                    {
                        float g = Seg(recallT, start, lift), r = p.Def.BodyR;
                        Arc(p.Disp, r + 30f * (1f - g) + 4f, 2.5f, col.Mix(Rgba.White, 0.3f).WithAlpha(0.9f * g), 32);
                    }
                }
                float lk = Seg(recallT, lift, lift + 0.55f);
                if (lk > 0f && lk < 1f)  // gone: a ring out, a flash, and a streak up the beam
                {
                    Arc(p.Disp, 20f + 70f * lk, 4f * (1f - lk) + 1f, col.WithAlpha(1f - lk), 40);
                    Circle(p.Disp, 30f * (1f - lk), Rgba.White.WithAlpha(0.6f * (1f - lk)), 20);
                    float up = 600f * EaseOut(lk);
                    Line(p.Disp - new V2(0f, up), p.Disp - new V2(0f, up + 90f), 6f * (1f - lk), Rgba.White.WithAlpha(0.8f * (1f - lk)));
                }
            }
            float rk = Seg(recallT, RecallFadeAt, RecallFadeEnd);
            if (rk > 0f && rk < 1f)  // the launch's ripple, closing in
            {
                float e = rk * rk, rad = 40f + (1f - e) * 1100f;
                var c = new Rgba(0.55f, 0.72f, 1f);
                Arc(new V2(Game.Width / 2f, Game.Height / 2f), rad, 26f, c.WithAlpha(0.12f * rk), 96);
                Arc(new V2(Game.Width / 2f, Game.Height / 2f), rad, 4f, c.WithAlpha(0.6f * rk), 96);
            }
        }

        static readonly Color RecallDark = new Color(0.02f, 0.025f, 0.06f);

        /// Over everything in the game (OnGUI): the fade to dark at the end.
        void DrawRecallFade()
        {
            if (!RecallActive)
                return;
            float a = Seg(recallT, RecallFadeAt, RecallFadeEnd);
            if (a > 0f)
                Ui.Fill(new Rect(0, 0, W, H), new Color(RecallDark.r, RecallDark.g, RecallDark.b, a * a * (3f - 2f * a)));
        }

        /// Instead of DrawLobby while it comes back: the dark lifting, the two sides sliding in, the ring on your card.
        void DrawLobbyReturning()
        {
            float e = returnT;
            uiGlBlocked = true;  // its live models wait
            LobbyPiece(LobbyLeftSide, e, 0.15f, 0.75f, -520f, 0f);   // the two sides (LobbyLayout.cs), as LaunchIntro's DrawLobbyLeaving
            LobbyPiece(LobbyRightSide, e, 0.25f, 0.85f, 880f, 0f);
            uiGlBlocked = false;
            float dark = 1f - Seg(e, 0f, 0.45f);
            if (dark > 0f)
                Ui.Fill(new Rect(0, 0, W, H), new Color(RecallDark.r, RecallDark.g, RecallDark.b, dark));
            float ring = Seg(e, 0.8f, 1.3f);
            if (ring > 0f && ring < 1f && BeginUiGl())
            {
                DrawPartyCardRing(ring);
                EndUiGl();
            }
        }
    }
}
