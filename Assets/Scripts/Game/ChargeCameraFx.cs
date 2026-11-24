using UnityEngine;

namespace Arena
{
    /// The charge camera: your own hold-and-release weapon (Game.ChargeWeapon: the Mender's Bow, Railgun, Warhammer, Golem
    /// Staff, Arc Lightning, Imp Caller, Gore Mortar), with Visual effects on. Drawing only: nothing is sent, the game is unchanged.
    ///   - the draw: as you charge, the picture eases out round you (the weapon's Out at full charge) and slides back against
    ///     your aim (Pull), like the camera riding an arrow back on the string; a full charge held stays still;
    ///   - the release: the picture snaps forward along your aim past rest (CamSnapTime), overshooting by the shot's power
    ///     (Over bigger, Kick along the aim), then settles back (CamSettle). Every release kicks: a tap a little (CamTapPower),
    ///     a full charge fully; the bow's Focus shot most (CamFocusPower) and its Scatter a medium kick (CamScatterPower);
    ///   - a charge dropped without a release (Scatter, the shield, going down, a weapon swap) eases back slowly, with no kick;
    ///   - a Focus arrow's first hit holds the picture a frame or two (FocusHitPause, BowFocusLook.cs).
    /// Each weapon's numbers are a CamProfile (CamProfileOf). Applied through the Twin Rifle's push and punch (TwinRifleFx.cs,
    /// Px in WorldRenderer.cs); MouseArena undoes the zoom so the cursor stays on what it's over. Off with Screen shake off or
    /// Reduced effects; 1.5x with Screen shake on Full.
    public partial class ArenaApp
    {
        struct CamProfile
        {
            public float Out, Pull, Over, Kick;  // full charge: this much smaller, slid this far back; a full release: this much bigger, pushed this far on
            public CamProfile(float o, float p, float ov, float k) { Out = o; Pull = p; Over = ov; Kick = k; }
        }

        /// Each charge weapon's camera. A zero profile: none.
        static CamProfile CamProfileOf(WeaponId w)
        {
            switch (w)
            {
                case WeaponId.MendersBow: return new CamProfile(0.05f, 6f, 0.022f, 5f);   // the arrow drawn back and loosed
                case WeaponId.Railgun: return new CamProfile(0.06f, 7f, 0.03f, 8f);       // the longest wind-up, the hardest kick
                case WeaponId.Warhammer: return new CamProfile(0.04f, 4f, 0.035f, 3f);    // raised high, then slammed: more punch in than push
                case WeaponId.GolemStaff: return new CamProfile(0.045f, 5f, 0.026f, 6f);  // the boulder heaved and hurled
                case WeaponId.GoreMortar: return new CamProfile(0.04f, 5f, 0.02f, 5f);    // a lob
                case WeaponId.ArcLightning: return new CamProfile(0.035f, 3f, 0.018f, 3f); // tags gathered, the chain let go
                case WeaponId.ImpCaller: return new CamProfile(0.03f, 3f, 0.015f, 2f);    // a summons, the lightest
                default: return default(CamProfile);
            }
        }

        const float CamSnapTime = 0.08f, CamSettle = 14f, CamLetGo = 3.5f;  // snap in; settle (per s); a dropped charge easing back
        const float CamTapPower = 0.25f, CamFocusPower = 1.35f, CamScatterPower = 0.6f;
        const float FocusHitPause = 0.03f;  // a Focus arrow's first hit: about two frames held

        float camDraw;          // the charge's share, eased (0 none, 1 full)
        float camDrawAim;       // the aim it slides back against (radians)
        CamProfile camDrawProf; // the weapon being charged
        float camRelT = 99f;    // seconds since the last release (99: none)
        float camRelFrom;       // the charge's share at that release
        float camRelPow;        // its power (CamTapPower .. CamFocusPower)
        float camRelAim;        // its aim
        CamProfile camRelProf;  // and its weapon
        int camSeen = -1, camScatter = -1;
        float camZoomK;         // this frame: how much bigger the picture is drawn round you (negative: smaller)
        V2 camPush;             // ... and how far it's pushed (arena units)
        V2 camAt;               // ... round this point (you)

        void ClearChargeCam()
        {
            camDraw = camZoomK = 0f;
            camRelT = 99f;
            camPush = V2.Zero;
            camSeen = camScatter = -1;
        }

        /// Once a frame (real time, Juice.cs): follow your charge, start a kick on each release or Scatter.
        void ChargeCamStep(Player me, float realDt)
        {
            bool on = me != null && fxShake > 0 && !fxReduced && me.Weapon != null;
            if (!on)
            {
                ClearChargeCam();
                return;
            }
            if (camSeen < 0)
            {
                camSeen = game.ChargeReleases; camScatter = game.ScatterLooses;
            }
            var prof = CamProfileOf(me.Weapon.Id);
            if (game.ChargeReleases != camSeen || game.ScatterLooses != camScatter)
            {
                bool scatter = game.ScatterLooses != camScatter;
                camSeen = game.ChargeReleases; camScatter = game.ScatterLooses;
                var w = scatter ? WeaponId.MendersBow : game.ChargeLastWeapon;
                camRelProf = CamProfileOf(w);
                camRelT = 0f;
                camRelFrom = camDraw;
                camRelAim = me.Aim;
                camRelPow = scatter ? CamScatterPower
                    : w == WeaponId.MendersBow && game.BowLastFocus ? CamFocusPower
                    : Mathf.Lerp(CamTapPower, 1f, game.ChargeLastCharge);
                camDraw = 0f;
            }
            else if (me.DrawT > 0f && prof.Out > 0f)
            {
                float f = Mathf.Clamp01(me.DrawT / Mathf.Max(0.05f, game.BowFull(me)));
                camDraw = 1f - (1f - f) * (1f - f);  // eases out: quick at first, settling as the charge comes to full
                camDrawAim = me.Aim;
                camDrawProf = prof;
            }
            else
                camDraw *= Mathf.Exp(-realDt * CamLetGo);  // dropped without a release: back slowly
            if (camDraw < 0.001f)
                camDraw = 0f;
            camRelT += realDt;

            float k = fxShake == 2 ? 1.5f : 1f;
            float z = -camDrawProf.Out * camDraw;
            V2 push = V2.FromAngle(camDrawAim) * (-camDrawProf.Pull * camDraw);
            float rz, rp;
            if (CamRelease(camRelProf, camRelT, camRelFrom, camRelPow, out rz, out rp))
            {
                z += rz;
                push += V2.FromAngle(camRelAim) * rp;
            }
            camZoomK = z * k;
            camPush = push * k;
            camAt = me.Disp;
        }

        /// A release `t` seconds ago, charged `from` (0..1) with power `pow`: the zoom (fraction) and the push along the aim
        /// (arena units). False once it has settled.
        static bool CamRelease(CamProfile c, float t, float from, float pow, out float zoom, out float push)
        {
            zoom = push = 0f;
            if (t >= 1f)
                return false;
            if (t < CamSnapTime)
            {
                float u = t / CamSnapTime;
                u = 1f - (1f - u) * (1f - u) * (1f - u);  // a fast snap that lands softly on the overshoot
                zoom = Mathf.Lerp(-c.Out * from, c.Over * pow, u);  // (from the pulled-back charge: its own share is gone)
                push = Mathf.Lerp(-c.Pull * from, c.Kick * pow, u);
            }
            else
            {
                float d = Mathf.Exp(-(t - CamSnapTime) * CamSettle);
                zoom = c.Over * pow * d;
                push = c.Kick * pow * d;
            }
            return true;
        }

        /// BeginJuiceFrame, after the Twin Rifle: the charge camera's share of this frame's push and punch.
        void BeginChargeCam(Player me)
        {
            if (me == null || (camZoomK == 0f && camPush.X == 0f && camPush.Y == 0f))
                return;
            shakeX += camPush.X * drawScale;
            shakeY -= camPush.Y * drawScale;  // (GL's y runs up the screen)
            if (camZoomK == 0f)
                return;
            if (punchK == 0f)
            {
                V2 c = camZoomOn ? ZoomPoint(camAt) : camAt;
                var v = CanvasPx(glShift + c * glZoom);
                punchX = v.x + shakeX;
                punchY = v.y + shakeY;
            }
            punchK += camZoomK;
        }

        /// MouseArena: undo the charge camera's zoom so the cursor's arena point is what's drawn under it.
        V2 ChargeCamUnpunch(V2 a) => camZoomK == 0f || !Juicy ? a : camAt + (a - camAt) * (1f / (1f + camZoomK));

        /// Your own Focus arrow's first hit: hold the picture a frame or two (BowFocusLook.cs). Skips Hold's gap, so it
        /// still lands just after the release's own hold.
        void FocusHitHold()
        {
            if (fxReduced)
                return;
            holdT = Mathf.Max(holdT, FocusHitPause);
        }
    }
}
