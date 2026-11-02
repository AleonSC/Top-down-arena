using UnityEngine;

namespace Arena
{
    /// Launching into a run ("drop in", Docs/Mockups/run-launch.html option A), all inside the 3 s before wave 1:
    ///   0.0-0.8 s  the lobby's two sides slide off left and right and fade (only when coming from the lobby);
    ///   0.35 s     a ripple runs out from the middle of the arena, bending the grid (Juice's ripple) under a bright ring;
    ///   1.0 s on   each player beams down as a pillar of light in their colour, a quarter second apart: they appear as
    ///              it lands, with a ring and their class icon flashing above them (hidden until then);
    ///   1.7 s      the HUD slides in (its own show animation);
    ///   2.0 s      GET READY counts down (the usual between-waves banner), then a gold WAVE 1 pops in as the fight starts.
    /// Your controls are held (Game.HoldControls: no moving, dashing, shooting, parrying or abilities) until the fight starts.
    /// Everything is on your own screen only. Play again (from the results screen) runs it too, without the lobby part.
    /// Sounds (LaunchSounds, SoundBank.cs): the sweep as the lobby leaves, the ripple, each pillar's beam and landing,
    /// the HUD's swish; the fight's WaveStart as WAVE 1 pops in.
    public partial class ArenaApp
    {
        const float LaunchLen = 4.6f, LobbyOutTime = 0.8f, LaunchRippleAt = 0.35f, PillarAt = 1.0f, PillarStep = 0.25f, PillarLife = 0.7f,
            PillarLand = 0.3f, LaunchHudAt = 1.7f, LaunchBannerAt = 2.0f;

        float launchT = -1f;  // seconds since the run launched; below 0 when there's no launch going on
        bool launchFromLobby, launchRippled, uiGlBlocked;
        AppScreen launchLastScreen = AppScreen.Menu;
        MatchState launchLastState = MatchState.Idle;

        bool LaunchActive => launchT >= 0f && launchT < LaunchLen;

        /// Update: notice a run starting (from the lobby, or Play again from the results), and run the clock.
        void TickLaunch(float dt)
        {
            bool inGame = session.Screen == AppScreen.Game && game.Running;
            bool fresh = launchLastScreen != AppScreen.Game
                || launchLastState == MatchState.GameOver || launchLastState == MatchState.Victory || launchLastState == MatchState.Idle;
            if (inGame && fresh && game.State == MatchState.Intermission)
            {
                launchT = 0f;
                launchFromLobby = launchLastScreen == AppScreen.Lobby;
                launchRippled = false;
                hudT = 0f;  // the HUD comes in at LaunchHudAt
            }
            launchLastScreen = session.Screen;
            launchLastState = game.State;
            if (launchT < 0f)
                return;
            if (!inGame)
            {
                launchT = -1f;
                game.HoldControls = false;
                return;
            }
            float before = launchT;
            launchT += dt;
            game.HoldControls = game.State != MatchState.Fighting;  // no moving or shooting until WAVE 1 pops in
            LaunchSounds(before, launchT);
            if (!launchRippled && launchT >= LaunchRippleAt)
            {
                launchRippled = true;
                ripples.Add(new Ripple { Pos = new V2(Game.Width / 2f, Game.Height / 2f), Power = 2.2f });  // Juice.cs
            }
            if (launchT >= LaunchLen)
            {
                launchT = -1f;
                game.HoldControls = false;
            }
        }

        /// The launch's sounds, each as the clock passes its moment (Update, your own screen).
        void LaunchSounds(float before, float now)
        {
            float clock = Time.unscaledTime;
            if (launchFromLobby && Passed(before, now, 0.02f))
                sounds.Play(Sfx.LaunchSweep, 0.8f, clock);
            if (Passed(before, now, LaunchRippleAt))
                sounds.Play(Sfx.LaunchRipple, 0.9f, clock);
            foreach (var p in session.Game.Players.Values)
            {
                float land = LandTime(p);
                if (Passed(before, now, land - PillarLand))
                    sounds.Play(Sfx.LaunchBeam, 0.7f, clock);
                if (Passed(before, now, land))
                    sounds.Play(Sfx.LaunchLand, 0.85f, clock);
            }
            if (Passed(before, now, LaunchHudAt))
                sounds.Play(Sfx.LaunchHud, 0.6f, clock);
        }

        /// The HUD waits off screen until LaunchHudAt.
        bool LaunchHoldsHud => LaunchActive && launchT < LaunchHudAt;

        /// The GET READY banner waits until the players have landed.
        bool LaunchHoldsBanner => LaunchActive && launchT < LaunchBannerAt;

        /// When this player's pillar lands (players in id order, a quarter second apart).
        float LandTime(Player p)
        {
            int i = 0;
            foreach (var q in session.Game.Players.Values)
            {
                if (q == p)
                    break;
                i++;
            }
            return PillarAt + i * PillarStep + PillarLand;
        }

        /// Not drawn yet: their pillar hasn't landed (the real match only, not the lobby's preview).
        bool LaunchHides(Player p) => LaunchActive && ReferenceEquals(game, session.Game) && launchT < LandTime(p);

        static float Seg(float t, float a, float b) => Mathf.Clamp01((t - a) / (b - a));

        /// In the world, over the floor: the bright ring of the ripple, each player's pillar of light and landing ring.
        void DrawLaunchWorld(float t)
        {
            if (!LaunchActive || !ReferenceEquals(game, session.Game))
                return;
            float rk = Seg(launchT, LaunchRippleAt, LaunchRippleAt + 1.3f);
            if (rk > 0f && rk < 1f)
            {
                float e = 1f - (1f - rk) * (1f - rk) * (1f - rk), rad = 40f + e * 1100f;
                var c = new Rgba(0.55f, 0.72f, 1f);
                Arc(new V2(Game.Width / 2f, Game.Height / 2f), rad, 26f, c.WithAlpha(0.12f * (1f - rk)), 96);
                Arc(new V2(Game.Width / 2f, Game.Height / 2f), rad, 4f, c.WithAlpha(0.6f * (1f - rk)), 96);
            }
            foreach (var p in game.Players.Values)
            {
                float land = LandTime(p), start = land - PillarLand, k = Seg(launchT, start, start + PillarLife);
                var col = p.Def.Color;
                if (k > 0f && k < 1f)
                {
                    float a = Mathf.Sin(MathX.Pi * k), w = 40f * (1f - 0.6f * k), r = p.Def.BodyR;
                    // The beam's foot fades out just above the player's middle over two body radii, so it has no flat
                    // edge (the picker's pillar does the same; Docs/Mockups/pillar-foot.html option 2).
                    V2 top = new V2(p.Disp.X, -20f), foot = p.Disp - new V2(0f, r * 0.2f), soft = foot - new V2(0f, r * 2.1f);
                    Line(top, soft, w * 1.8f, col.WithAlpha(0.18f * a));
                    Line(top, soft, w, col.WithAlpha(0.55f * a));
                    Line(top, soft, w * 0.25f, Rgba.White.WithAlpha(0.95f * a));
                    const int steps = 16;
                    for (int s = 0; s < steps; s++)
                    {
                        V2 s0 = V2.Lerp(soft, foot, s / (float)steps), s1 = V2.Lerp(soft, foot, (s + 1f) / steps);
                        float fade = 1f - (s + 0.5f) / steps;
                        Line(s0, s1, w * 1.8f, col.WithAlpha(0.18f * a * fade));
                        Line(s0, s1, w, col.WithAlpha(0.55f * a * fade));
                        Line(s0, s1, w * 0.25f, Rgba.White.WithAlpha(0.95f * a * fade));
                    }
                    Circle(p.Disp, 26f * a + 6f, col.WithAlpha(0.35f * a), 24);  // the spot it hits
                    if (launchT >= land)  // landed: a halo round them while the beam fades
                    {
                        Arc(p.Disp, r + 7f, 2.2f, col.Mix(Rgba.White, 0.3f).WithAlpha(0.85f * a), 32);
                        Arc(p.Disp, r + 12f, 1.2f, col.WithAlpha(0.3f * a), 32);
                    }
                }
                float lk = Seg(launchT, land, land + 0.6f);
                if (lk > 0f && lk < 1f)  // the landing: a ring out, and a flash
                {
                    Arc(p.Disp, 20f + 70f * lk, 4f * (1f - lk) + 1f, col.WithAlpha(1f - lk), 40);
                    Circle(p.Disp, 30f * (1f - lk), Rgba.White.WithAlpha(0.5f * (1f - lk)), 20);
                }
            }
        }

        /// Over a landed player (HUD space): their class icon flashing up above them.
        void DrawLaunchIcon(Player p, V2 at, float r)
        {
            if (!LaunchActive)
                return;
            float k = Seg(launchT, LandTime(p) + 0.05f, LandTime(p) + 0.95f);
            if (k <= 0f || k >= 1f)
                return;
            float size = 34f, rise = 16f * k;
            Ui.Icon(new Rect(at.X - size / 2f, at.Y - r - 66f - rise, size, size), PixelArt.Get("class_" + Defs.KitName(p.Kit).ToLowerInvariant()),
                new Color(1f, 1f, 1f, Mathf.Sin(MathX.Pi * k)));
        }

        /// The lobby leaving: its left side slides off to the left and the rest to the right, fading (OnGUI, over the HUD).
        void DrawLobbyLeaving()
        {
            if (!LaunchActive || !launchFromLobby || launchT >= LobbyOutTime || session.Lobby.Count == 0)
                return;
            float k = Seg(launchT, 0f, LobbyOutTime), e = k * k * (3f - 2f * k), a = 1f - Seg(launchT, 0.3f, LobbyOutTime);
            const float split = 430f;  // where the lobby's party column ends and its tabs begin
            var oldColor = GUI.color;
            bool oldEnabled = GUI.enabled;
            GUI.enabled = false;
            uiGlBlocked = true;  // its live models (GL) don't follow the slide: they wait
            Ui.Alpha = a;
            GUI.color = new Color(oldColor.r, oldColor.g, oldColor.b, oldColor.a * a);
            GUI.BeginGroup(new Rect(-e * 520f, 0f, split, H));
            DrawLobby();
            GUI.EndGroup();
            GUI.BeginGroup(new Rect(split + e * 880f, 0f, W - split, H));
            GUI.BeginGroup(new Rect(-split, 0f, W, H));
            DrawLobby();
            GUI.EndGroup();
            GUI.EndGroup();
            Ui.Alpha = 1f;
            GUI.color = oldColor;
            GUI.enabled = oldEnabled;
            uiGlBlocked = false;
        }

        /// As the fight starts after a launch: WAVE 1 (or whichever) pops in, gold, then fades.
        void DrawLaunchWave()
        {
            if (!LaunchActive || game.State != MatchState.Fighting)
                return;
            float start = 3f, k = Seg(launchT, start, start + 0.35f), o = Seg(launchT, LaunchLen - 0.4f, LaunchLen);
            if (k <= 0f)
                return;
            float q = k - 1f, back = 1f + 2.70158f * q * q * q + 1.70158f * q * q, s = 1.5f + (1f - 1.5f) * back, a = k * (1f - o);
            var r = new Rect(W / 2f - 300f * s, H / 2f - 90f - 40f * s, 600f * s, 80f * s);
            Ui.Fill(new Rect(0f, H / 2f - 150f, W, 120f), new Color(0f, 0f, 0f, 0.35f * a));
            var oldColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, a);
            Ui.Alpha = a;
            Ui.Fancy(r, "WAVE " + Mathf.Max(1, game.Wave), Mathf.RoundToInt(64f * s), Color.white, Ui.Gold, TextAnchor.MiddleCenter, new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.6f));
            Ui.Label(new Rect(0f, H / 2f - 50f, W, 20f), game.Practice != null ? "PRACTICE" : "50 WAVES  ·  A BOSS EVERY 5", 14, new Color(0.8f, 0.84f, 0.92f, a), TextAnchor.MiddleCenter, true);
            Ui.Alpha = 1f;
            GUI.color = oldColor;
        }
    }
}
