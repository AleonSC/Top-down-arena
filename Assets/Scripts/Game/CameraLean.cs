using UnityEngine;

namespace Arena
{
    /// Camera lean: the picture eases a few percent toward where you aim and move, so the far side of the screen shows a
    /// little more. The arena itself is unchanged (still 1600x900, players still wrap); only the drawing slides.
    ///   target   65% the crosshair's direction (scaled by how far out it is) + 35% your movement, per axis up to
    ///            LeanMaxX x LeanMaxY arena units (5% of the arena) x LeanStrength (50%), eased at dt * 4.
    ///   position the lean ignores where you are, so wrapping round an edge never makes the camera jump.
    ///   screen   world drawing is shifted in Px (WorldRenderer.cs); labels placed from a world position go through ToUi, and
    ///            MouseArena adds the lean back, so aiming stays exact. The strip the shift uncovers is dimmed like the backdrop.
    /// Setting: Settings → Gameplay → Camera lean (PlayerPrefs "cam_lean", on by default). Off draws exactly as before.
    public partial class ArenaApp
    {
        const float LeanStrength = 0.5f, LeanMaxX = 80f, LeanMaxY = 45f, LeanReach = 450f, LeanEase = 4f;

        bool camLean = true;
        V2 lean;                 // how far the picture is shifted, in arena units (the world is drawn at position - lean)
        V2 leanLast;             // your drawn position last frame, for your speed
        bool leanHasLast;
        float leanPxX, leanPxY;  // lean in screen pixels while the world is drawn (set in DrawWorld, cleared in EndJuiceFrame)

        void LoadCamLean() => camLean = PlayerPrefs.GetInt("cam_lean", 1) == 1;

        void SetCamLean(bool on)
        {
            camLean = on;
            PlayerPrefs.SetInt("cam_lean", on ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// An arena position as a point on the HUD canvas, with the lean applied (name tags, damage numbers, labels).
        V2 ToUi(V2 p) => ((CamZoomed ? ZoomPoint(p) : p) - lean) * WorldToUi;  // (and the scroll-wheel zoom, CameraZoom.cs)

        /// Once a frame from Update: ease the lean toward where you're looking and heading.
        void UpdateCameraLean(float dt)
        {
            UpdateCameraZoom(dt);  // the scroll-wheel zoom (CameraZoom.cs)
            var me = game.Me;
            bool live = camLean && session.Screen == AppScreen.Game && game.Running && me != null && !me.Downed && !EndingOn && !devFrozen;
            if (!(session.Screen == AppScreen.Game && game.Running) || me == null)
            {
                lean = V2.Zero;
                leanHasLast = false;
                return;
            }
            if (game.Paused)
                return;  // (the picture holds still behind a pause menu)
            V2 target = V2.Zero;
            if (live)
            {
                V2 vel = V2.Zero;
                if (leanHasLast && dt > 0.0001f)
                {
                    V2 d = me.Disp - leanLast;
                    if (d.Length < 120f)  // (a wrap round the edge or a teleport is a jump, not speed)
                        vel = d * (1f / dt);
                }
                V2 toAim = MouseArena() - me.Disp;
                float reach = Mathf.Clamp01(toAim.Length / LeanReach);
                V2 aim = toAim.Length > 1f ? toAim.Normalized() : V2.Zero;
                float speed = Mathf.Clamp01(vel.Length / 300f);
                V2 move = vel.Length > 1f ? vel.Normalized() : V2.Zero;
                V2 want = aim * (reach * 0.65f) + move * (speed * 0.35f);
                target = new V2(Mathf.Clamp(want.X * LeanMaxX * LeanStrength, -LeanMaxX, LeanMaxX), Mathf.Clamp(want.Y * LeanMaxY * LeanStrength, -LeanMaxY, LeanMaxY));
            }
            leanLast = me.Disp;
            leanHasLast = true;
            float k = Mathf.Min(1f, dt * LeanEase);
            lean += (target - lean) * k;
            if (!live && lean.LengthSq < 0.0004f)
                lean = V2.Zero;
        }

        /// DrawWorld: the shift in screen pixels, for Px. Called once the drawing scale is the world's.
        void BeginLeanFrame()
        {
            leanPxX = lean.X * drawScale;
            leanPxY = lean.Y * drawScale;
            camZoomOn = CamZoomed;  // CameraZoom.cs
        }

        void EndLeanFrame()
        {
            leanPxX = leanPxY = 0f;
            camZoomOn = false;
        }

        /// The dim layer over the menu background behind the arena: the arena itself, or (while leaning) the strip it uncovers too.
        void FillArenaBackdrop(Rgba c)
        {
            if (game.RoomsOn)  // the Rooms world: behind all of it (Rooms.cs), plus a lean's worth round it
            {
                V2 lo = Rooms.WorldMin, hi = Rooms.WorldMax;
                FillRect(lo.X - LeanMaxX - 2f, lo.Y - LeanMaxY - 2f, hi.X - lo.X + 2f * LeanMaxX + 4f, hi.Y - lo.Y + 2f * LeanMaxY + 4f, c);
                return;
            }
            if (lean.LengthSq < 0.0001f)
                FillRect(0, 0, AW, AH, c);
            else
                FillRect(-LeanMaxX - 2f, -LeanMaxY - 2f, AW + 2f * LeanMaxX + 4f, AH + 2f * LeanMaxY + 4f, c);
        }
    }
}
