using UnityEngine;

namespace Arena
{
    /// The Mender's Bow's camera, your own bow only, with Visual effects on (drawing only: nothing is sent, the game is unchanged):
    ///   - the draw: as you draw, the picture eases out (BowDrawOut smaller at full draw) round you and slides back
    ///     against your aim (BowDrawPull), like the camera riding the arrow back on the string; a full draw held stays still;
    ///   - the release: the picture snaps forward along your aim past rest (BowSnapTime), overshooting by the shot's power
    ///     (BowOverZoom bigger, BowKickPush along the aim), then settles back (BowSettle). Every shot kicks: a tap a little
    ///     (BowTapPower), a full draw fully, a Focus shot most (BowFocusPower); a Scatter is a medium kick (BowScatterPower);
    ///   - a draw dropped without a shot (Scatter, the shield, going down) eases back slowly, with no kick.
    /// Applied through the Twin Rifle's push and punch (TwinRifleFx.cs, Px in WorldRenderer.cs); MouseArena undoes the
    /// zoom so the cursor stays on what it's over. Off with Screen shake off or Reduced effects; 1.5x with Screen shake on Full.
    public partial class ArenaApp
    {
        const float BowDrawOut = 0.05f, BowDrawPull = 6f;        // full draw: 5% smaller, slid 6 back against the aim
        const float BowOverZoom = 0.022f, BowKickPush = 5f;      // a full-power release overshoots this far
        const float BowSnapTime = 0.08f, BowSettle = 14f, BowLetGo = 3.5f;  // snap in; settle (per s); a dropped draw easing back
        const float BowTapPower = 0.25f, BowFocusPower = 1.35f, BowScatterPower = 0.6f;

        float bowCamDraw;        // the draw's share, eased (0 none, 1 full)
        float bowCamDrawAim;     // the aim it slides back against (radians)
        float bowCamRelT = 99f;  // seconds since the last release (99: none)
        float bowCamRelFrom;     // the draw's share at that release
        float bowCamRelPow;      // its power (BowTapPower .. BowFocusPower)
        float bowCamRelAim;      // its aim
        int bowCamSeen = -1, bowCamScatter = -1;
        float bowCamZoom;        // this frame: how much bigger the picture is drawn round you (negative: smaller)
        V2 bowCamPush;           // ... and how far it's pushed (arena units)
        V2 bowCamAt;             // ... round this point (you)

        void ClearBowCam()
        {
            bowCamDraw = bowCamZoom = 0f;
            bowCamRelT = 99f;
            bowCamPush = V2.Zero;
            bowCamSeen = bowCamScatter = -1;
        }

        /// Once a frame (real time, Juice.cs): follow your draw, start a kick on each release or Scatter.
        void BowCamStep(Player me, float realDt)
        {
            bool on = me != null && fxShake > 0 && !fxReduced && me.Weapon != null && me.Weapon.Id == WeaponId.MendersBow;
            if (bowCamSeen < 0 || !on)
            {
                bowCamSeen = game.BowReleases; bowCamScatter = game.ScatterLooses;
                if (!on)
                {
                    ClearBowCam();
                    bowCamSeen = game.BowReleases; bowCamScatter = game.ScatterLooses;
                    return;
                }
            }
            float full = Mathf.Max(0.05f, game.BowFull(me));
            if (game.BowReleases != bowCamSeen || game.ScatterLooses != bowCamScatter)
            {
                bool scatter = game.ScatterLooses != bowCamScatter;
                bowCamSeen = game.BowReleases; bowCamScatter = game.ScatterLooses;
                bowCamRelT = 0f;
                bowCamRelFrom = bowCamDraw;
                bowCamRelAim = me.Aim;
                bowCamRelPow = scatter ? BowScatterPower : game.BowLastFocus ? BowFocusPower : Mathf.Lerp(BowTapPower, 1f, game.BowLastCharge);
                bowCamDraw = 0f;
            }
            else if (me.DrawT > 0f)
            {
                float f = Mathf.Clamp01(me.DrawT / full);
                bowCamDraw = 1f - (1f - f) * (1f - f);  // eases out: quick at first, settling as the string comes to full
                bowCamDrawAim = me.Aim;
            }
            else
                bowCamDraw *= Mathf.Exp(-realDt * BowLetGo);  // dropped without a shot: back slowly
            if (bowCamDraw < 0.001f)
                bowCamDraw = 0f;
            bowCamRelT += realDt;

            float k = fxShake == 2 ? 1.5f : 1f;
            float z = -BowDrawOut * bowCamDraw;
            V2 push = V2.FromAngle(bowCamDrawAim) * (-BowDrawPull * bowCamDraw);
            float rz, rp;
            if (BowRelease(bowCamRelT, bowCamRelFrom, bowCamRelPow, out rz, out rp))
            {
                z += rz;
                push += V2.FromAngle(bowCamRelAim) * rp;
            }
            bowCamZoom = z * k;
            bowCamPush = push * k;
            bowCamAt = me.Disp;
        }

        /// A release `t` seconds ago, drawn `from` (0..1) with power `pow`: the zoom (fraction) and the push along the aim
        /// (arena units). False once it has settled.
        static bool BowRelease(float t, float from, float pow, out float zoom, out float push)
        {
            zoom = push = 0f;
            if (t >= 1f)
                return false;
            if (t < BowSnapTime)
            {
                float u = t / BowSnapTime;
                u = 1f - (1f - u) * (1f - u) * (1f - u);  // a fast snap that lands softly on the overshoot
                zoom = Mathf.Lerp(-BowDrawOut * from, BowOverZoom * pow, u);  // (from the pulled-back draw: its own share is gone)
                push = Mathf.Lerp(-BowDrawPull * from, BowKickPush * pow, u);
            }
            else
            {
                float d = Mathf.Exp(-(t - BowSnapTime) * BowSettle);
                zoom = BowOverZoom * pow * d;
                push = BowKickPush * pow * d;
            }
            return true;
        }

        /// BeginJuiceFrame, after the Twin Rifle: the bow's share of this frame's push and punch.
        void BeginBowCam(Player me)
        {
            if (me == null || (bowCamZoom == 0f && bowCamPush.X == 0f && bowCamPush.Y == 0f))
                return;
            shakeX += bowCamPush.X * drawScale;
            shakeY -= bowCamPush.Y * drawScale;  // (GL's y runs up the screen)
            if (bowCamZoom == 0f)
                return;
            if (punchK == 0f)
            {
                V2 c = camZoomOn ? ZoomPoint(bowCamAt) : bowCamAt;
                var v = CanvasPx(glShift + c * glZoom);
                punchX = v.x + shakeX;
                punchY = v.y + shakeY;
            }
            punchK += bowCamZoom;
        }

        /// MouseArena: undo the bow's zoom so the cursor's arena point is what's drawn under it.
        V2 BowCamUnpunch(V2 a) => bowCamZoom == 0f || !Juicy ? a : bowCamAt + (a - bowCamAt) * (1f / (1f + bowCamZoom));
    }
}
