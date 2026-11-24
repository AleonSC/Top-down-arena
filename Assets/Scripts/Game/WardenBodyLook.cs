using UnityEngine;

namespace Arena
{
    /// The Warden's body, the Chief Warden (Docs/Mockups/warden-rework.html, option A): a navy greatcoat with crimson
    /// trim, a peaked cap with a gold badge, a crimson sash, gold epaulettes and a ring of keys, and a pose for each move,
    /// worked out from the replicated WardenState (Warden.cs) so every game draws the same thing: racking and kicking back
    /// with a riot shotgun (Fan), arms out turning on his heel (Spiral), the radio to the tower and the baton pointing out
    /// the light (Light), an arm over his eyes (exposed), dashing with the coat streaming and slamming the baton into the
    /// floor (Cross), an overhand throw (the gas barrage), both hands on the riot shield (Rioters), the crouched wind-up and
    /// the charge (Charge), kneeling as the armour locks on and braced for the last stand. Shield, armour plates, lanes and
    /// the glare stay in WardenLook.cs.
    public partial class ArenaApp
    {
        static readonly Rgba WdCoat = new Rgba(0.12f, 0.17f, 0.29f), WdCoatLit = new Rgba(0.19f, 0.26f, 0.42f);
        static readonly Rgba WdTrim = new Rgba(0.72f, 0.16f, 0.27f), WdGold = new Rgba(0.94f, 0.72f, 0.29f);
        static readonly Rgba WdGlove = new Rgba(0.07f, 0.08f, 0.1f), WdCrown = new Rgba(0.11f, 0.15f, 0.25f), WdPeak = new Rgba(0.04f, 0.05f, 0.09f);
        static readonly Rgba WdGunMetal = new Rgba(0.23f, 0.25f, 0.29f), WdGunDark = new Rgba(0.12f, 0.13f, 0.16f), WdWood = new Rgba(0.43f, 0.29f, 0.17f);
        static readonly Rgba WdBaton = new Rgba(0.12f, 0.13f, 0.16f), WdTip = new Rgba(1f, 0.7f, 0.28f), WdCan = new Rgba(0.44f, 0.49f, 0.35f);

        enum WdPose { Walk, Gun, Spin, Radio, Point, Cover, Shield, WindUp, Slam, Throw, Kneel, Brace, Spread, Dazed, Keys }

        // The pose being drawn (set by WardenPose, read by the part drawers): the body's frame and a few dials.
        V2 wdPos, wdF, wdS;
        float wdU;
        bool wdSil;
        Rgba wdSilCol;
        float wdFlash;

        V2 WdAt(float lx, float ly) => wdPos + wdF * (lx * wdU) + wdS * (ly * wdU);
        Rgba Wd(Rgba c) => wdSil ? wdSilCol : wdFlash > 0f ? c.Mix(Rgba.White, wdFlash) : c;

        /// What the Warden is doing, as a pose: which way he faces, the pose, and its dial `k` (0-1 through the move),
        /// the recoil, the crouch and how far his coat flares.
        WdPose WardenPose(Enemy e, float t, out float face, out float k, out float recoil, out float crouch, out float flare, out float tilt)
        {
            var w = e.Warden;
            face = e.Facing;
            k = recoil = crouch = flare = tilt = 0f;
            if (w == null)
                return WdPose.Walk;
            if (w.TransT > 0f)
                return WardenShiftPose(e, w, t, ref face, ref k, ref recoil, ref crouch, ref flare, ref tilt);
            if (w.StunT > 0f)
            {
                tilt = 0.14f * Mathf.Sin(t * 9f);
                return WdPose.Dazed;
            }
            if (w.ExposedT > 0f)
            {
                tilt = 0.22f + 0.05f * Mathf.Sin(t * 14f);
                face = (w.LightDisp - e.Disp).Angle;  // turned into the light, an arm over his eyes
                return WdPose.Cover;
            }
            float tt = w.TurnT;
            switch (w.Turn)
            {
                case WardenTurn.Fan:
                {
                    // Warned at 0.3 s into the turn, fired 0.35 s later, then every 0.8 s (WardenFire): rack during
                    // the red lanes, kick back as it fires.
                    if (w.FanWarn > 0f)
                    {
                        face = w.FanAim;
                        k = Mathf.Sin(Mathf.Clamp01(1f - w.FanWarn / Game.FanWarnTime) * MathX.Pi);  // the pump
                    }
                    if (tt >= 0.65f)
                    {
                        float since = MathX.Fmod(tt - 0.65f, 0.8f);
                        recoil = Mathf.Exp(-since * 10f);
                    }
                    return WdPose.Gun;
                }
                case WardenTurn.Spiral:
                    face = tt * 2.7f + e.Id;  // turning on his heel with the four arms of the spiral
                    flare = Mathf.Clamp01(tt / 0.4f);
                    return WdPose.Spin;
                case WardenTurn.Rings:
                    if (tt >= 0.3f)
                        k = Mathf.Exp(-MathX.Fmod(tt - 0.3f, 1f) * 6f);  // throws his arms wide with each ring
                    return WdPose.Spread;
                case WardenTurn.Light:
                    if (w.LightT < 1.2f && w.LightMode == WardenLight.Follow)
                        return WdPose.Radio;  // calling the tower
                    face = (w.LightDisp - e.Disp).Angle;  // directing the light at you with his baton
                    return WdPose.Point;
                case WardenTurn.Cross:
                {
                    float into = MathX.Fmod(tt, 3f);
                    if (w.Dashing)
                    {
                        flare = 1f;
                        crouch = 0.25f;
                        return WdPose.Walk;
                    }
                    k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((into - 1f) / 0.15f));  // the baton slams into the floor as it plants
                    crouch = into < 1.5f ? 0.5f : 0.2f;
                    flare = Mathf.Clamp01(1f - (into - 1f) * 4f);
                    return WdPose.Slam;
                }
                case WardenTurn.Gas:
                {
                    // Two barrages, at 0.3 s and 4.3 s: wound back for 0.4 s, then over the top.
                    foreach (float at in new[] { 0.3f, 4.3f })
                        if (tt >= at - 0.45f && tt < at + 0.35f)
                        {
                            k = tt < at ? -0.15f * Mathf.Clamp01((tt - at + 0.45f) / 0.3f) : Mathf.SmoothStep(0f, 1f, (tt - at) / 0.25f);
                            crouch = 0.25f;
                            return WdPose.Throw;
                        }
                    return WdPose.Gun;  // the slow aimed bursts through the gas
                }
                case WardenTurn.Rioters:
                    return Game.WardenShieldCrack(w) >= 4 ? WdPose.Walk : WdPose.Shield;
                case WardenTurn.Charge:
                {
                    face = w.Aux;
                    float run = 0.9f + 620f / 700f;  // ChargeWarn + ChargeLength / ChargeSpeed
                    if (tt < 0.9f)
                    {
                        crouch = 0.55f * Mathf.Clamp01(tt / 0.3f);
                        return WdPose.WindUp;
                    }
                    if (tt < run)
                    {
                        crouch = 0.4f;
                        flare = 1f;
                        return WdPose.WindUp;
                    }
                    tilt = 0.08f * Mathf.Sin(t * 12f) * Mathf.Clamp01(1f - (tt - run) * 2f);  // the skid
                    return WdPose.Walk;
                }
                case WardenTurn.LastStand:
                {
                    float into = MathX.Fmod(tt, Game.LastStandCycle);
                    face = Game.LastStandFacing(tt) + MathX.Pi / 4f;  // between two of the streams, braced
                    crouch = 0.3f;
                    if (into >= Game.LastStandGap)
                        tilt = 0.035f * Mathf.Sin(t * 40f);  // the shudder of the burst
                    return WdPose.Brace;
                }
            }
            return WdPose.Walk;
        }

        /// A phase change, the Warden's 5 s set piece (Game.WardenTransTime): staggered by the blow that broke the phase, then
        /// a move per phase while the players regroup. Lockdown: radios the tower, holds up his keys, slams the baton down.
        /// Riot: roars with his arms wide, points the baton round the cell doors, turns the key, racks the shotgun.
        /// Last stand: kneels while the plates lock on (Game.ArmorPlateAt), rises roaring, braces.
        WdPose WardenShiftPose(Enemy e, WardenState w, float t, ref float face, ref float k, ref float recoil, ref float crouch, ref float flare, ref float tilt)
        {
            float at = Game.WardenTransTime - w.TransT;
            if (at < 0.7f)  // staggered
            {
                float f = 1f - at / 0.7f;
                tilt = 0.25f * f * Mathf.Sin(at * 18f);
                crouch = 0.45f * f;
                return WdPose.Dazed;
            }
            switch (w.TransTo)
            {
                case WardenStage.Lockdown:
                    if (at < 2.2f)
                        return WdPose.Radio;
                    if (at < 3.6f)
                    {
                        k = Mathf.Sin((at - 2.2f) * 9f);  // shakes the keys at you
                        return WdPose.Keys;
                    }
                    if (at < 4.4f)
                    {
                        k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((at - 3.65f) / 0.15f));
                        crouch = at < 3.8f ? 0.5f : 0.3f;
                        flare = Mathf.Clamp01(1f - (at - 3.8f) * 3f);
                        return WdPose.Slam;
                    }
                    crouch = 0.3f;
                    return WdPose.Brace;
                case WardenStage.Riot:
                    if (at < 1.9f)
                    {
                        k = Mathf.Exp(-MathX.Fmod(at - 0.7f, 0.6f) * 5f);  // two roars, arms flung wide
                        flare = 0.6f + 0.4f * k;
                        return WdPose.Spread;
                    }
                    if (at < 3.4f)
                    {
                        face += Mathf.Lerp(-1.6f, 1.6f, Mathf.SmoothStep(0f, 1f, (at - 1.9f) / 1.5f));  // round the cell doors
                        return WdPose.Point;
                    }
                    if (at < 4.3f)
                    {
                        k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((at - 3.6f) / 0.4f)) * 1.6f;  // turns the key
                        return WdPose.Keys;
                    }
                    k = Mathf.Sin(Mathf.Clamp01((at - 4.3f) / 0.5f) * MathX.Pi);  // racks the shotgun
                    return WdPose.Gun;
                default:  // the last stand
                    if (at < 3.8f)
                    {
                        crouch = 0.8f * Mathf.Clamp01((at - 0.7f) / 0.3f);
                        return WdPose.Kneel;
                    }
                    if (at < 4.5f)
                    {
                        float f = (at - 3.8f) / 0.7f;
                        crouch = 0.8f * (1f - f);
                        k = Mathf.Sin(f * MathX.Pi);
                        flare = k;
                        return WdPose.Spread;
                    }
                    crouch = 0.3f;
                    return WdPose.Brace;
            }
        }

        /// The hands, in the body's frame (units of r/30, x forward, y to his right).
        void WdHands(WdPose p, float k, float recoil, float t, out Vector2 l, out Vector2 r)
        {
            switch (p)
            {
                case WdPose.Gun: l = new Vector2(25f - recoil * 5f - k * 7f, 5f); r = new Vector2(9f - recoil * 6f, 12f); return;
                case WdPose.Spin: l = new Vector2(2f, -35f); r = new Vector2(2f, 35f); return;
                case WdPose.Radio: l = new Vector2(12f, -6f); r = new Vector2(7f, 21f); return;
                case WdPose.Point: l = new Vector2(6f, -21f); r = new Vector2(24f, 12f); return;
                case WdPose.Cover: l = new Vector2(15f, -2f); r = new Vector2(5f, 22f); return;
                case WdPose.Shield: l = new Vector2(21f, -13f); r = new Vector2(21f, 13f); return;
                case WdPose.WindUp: l = new Vector2(20f, -12f); r = new Vector2(-13f, 27f); return;
                case WdPose.Slam: l = new Vector2(8f, -20f); r = new Vector2(Mathf.Lerp(-4f, 27f, k), Mathf.Lerp(25f, 6f, k)); return;
                case WdPose.Throw:
                {
                    float a = Mathf.Lerp(MathX.Pi * 0.85f, -MathX.Pi * 0.05f, k);
                    l = new Vector2(14f, -17f);
                    r = new Vector2(Mathf.Cos(a) * 24f + 4f, 18f - Mathf.Sin(a) * 6f);
                    return;
                }
                case WdPose.Kneel: case WdPose.Brace: l = new Vector2(16f, -14f); r = new Vector2(16f, 14f); return;
                case WdPose.Spread: l = new Vector2(8f - k * 4f, -24f - k * 10f); r = new Vector2(8f - k * 4f, 24f + k * 10f); return;
                case WdPose.Dazed: l = new Vector2(2f, -24f); r = new Vector2(3f, 24f); return;
                case WdPose.Keys: l = new Vector2(27f, -9f + k * 1.5f); r = new Vector2(7f, 21f); return;  // the ring held out in front
                default:
                {
                    float sw = Mathf.Sin(t * 1.8f) * 2f;  // the baton bobs as he walks
                    l = new Vector2(6f, -21f + sw * 0.3f);
                    r = new Vector2(7f + sw, 21f);
                    return;
                }
            }
        }

        void DrawWardenBody(Enemy e, V2 pos, float r, Rgba col, bool spawning, float t)
        {
            var pose = WardenPose(e, t, out float face, out float k, out float recoil, out float crouch, out float flare, out float tilt);
            if (spawning)
            {
                pose = WdPose.Brace;
                face = MathX.Pi / 2f;
                k = recoil = crouch = flare = tilt = 0f;
            }
            if (pose == WdPose.Gun)
                pos -= V2.FromAngle(face) * (recoil * 6f);  // the kick
            wdPos = pos;
            wdU = r / 30f;
            V2.Axes(face + tilt, out wdF, out wdS);
            wdSil = spawning && e.Type == EnemyType.Warden && WardenIntroOn;  // a silhouette in its cell (WardenIntro.cs)
            wdSilCol = col;
            wdFlash = e.Flash > 0f || JuiceFlash(e) ? HitFlashMix(e) : 0f;
            float fa = face + tilt, sh = 1f - crouch * 0.12f, br = Mathf.Sin(t * 2.2f) * 0.7f;
            WdHands(pose, k, recoil, t, out Vector2 hl, out Vector2 hr);
            V2 L = WdAt(hl.x, hl.y), R = WdAt(hr.x, hr.y);

            WardenFloorFx(e, pos, r, t);

            // The greatcoat's tails, flaring as he dashes, spins and charges.
            float tx = -24f - flare * 14f - crouch * 3f, wx = -15f - flare * 8f, wy = 18f + flare * 9f;
            V2[] tail = { WdAt(-4f, -16f), WdAt(wx, -wy), WdAt(tx, -6f), WdAt(tx, 6f), WdAt(wx, wy), WdAt(-4f, 16f) };
            WdFan(tail, GuardInk, 1.6f * wdU);
            WdFan(tail, Wd(WdCoat.Mix(GuardInk, 0.25f)), 0f);
            for (int i = 0; i < tail.Length - 1; i++)
                Line(tail[i], tail[i + 1], 1f * wdU, Wd(WdTrim).WithAlpha(0.45f));
            Line(WdAt(-9f, 0f), WdAt(tx + 1f, 0f), 1.5f * wdU, new Rgba(0f, 0f, 0f, 0.45f));  // the vent

            // The shoulders and chest: the sash, its buckle, the epaulettes and the keys.
            Oval(pos, 14f * sh * wdU + OffInk, (25f + br) * sh * wdU + OffInk, fa, GuardInk);
            Oval(pos, 14f * sh * wdU, (25f + br) * sh * wdU, fa, Wd(WdCoat));
            Oval(WdAt(2f, 0f), 9f * sh * wdU, 19f * sh * wdU, fa, Wd(WdCoatLit));
            Arc(WdAt(1f, 0f), 13.5f * wdU, 3f * wdU, Wd(WdTrim), 20, fa - 1.1f, 2.2f);  // the crimson collar
            Circle(WdAt(9f, -6f), 1.5f * wdU, Wd(WdGold), 8);  // two brass buttons
            Circle(WdAt(9f, 6f), 1.5f * wdU, Wd(WdGold), 8);
            for (int sy = -1; sy <= 1; sy += 2)
            {
                V2 ep = WdAt(-1f, sy * 22f * sh);
                Quad(ep - wdF * (5f * wdU) - wdS * (3f * wdU), ep + wdF * (5f * wdU) - wdS * (3f * wdU), ep + wdF * (5f * wdU) + wdS * (3f * wdU), ep - wdF * (5f * wdU) + wdS * (3f * wdU), Wd(WdGold));
                Line(ep - wdF * (5f * wdU), ep + wdF * (5f * wdU), 1f, new Rgba(0f, 0f, 0f, 0.35f));
            }
            bool held = pose == WdPose.Keys;  // the ring in his hand (a phase change), else on his belt
            V2 keys = held ? L + wdF * (6f * wdU) : WdAt(-10f, 15f * sh);
            float kr = held ? 1.8f : 1f;
            Arc(keys, 3f * kr * wdU, 1.3f * wdU, Wd(WdGold), 14);
            float jangle = held ? k * 0.9f + Mathf.Sin(t * 14f) * 0.25f : Mathf.Sin(t * 3.1f) * 0.5f + (pose == WdPose.Walk && flare > 0f ? Mathf.Sin(t * 20f) * 0.6f : 0f);
            for (int i = 0; i < (held ? 5 : 3); i++)
            {
                V2 d = V2.FromAngle(fa + (held ? 0f : MathX.Pi * 0.75f) + jangle + (i - (held ? 2 : 1)) * 0.5f);
                Line(keys + d * (3f * kr * wdU), keys + d * ((3f * kr + 4f * kr) * wdU), 1.3f * kr * wdU, Wd(new Rgba(0.81f, 0.63f, 0.29f)));
                if (held)
                    Line(keys + d * ((3f * kr + 4f * kr) * wdU), keys + d * ((3f * kr + 4f * kr) * wdU) + V2.FromAngle(fa + jangle + (i - 2) * 0.5f + 1.57f) * (2.5f * wdU), 1.3f * wdU, Wd(new Rgba(0.81f, 0.63f, 0.29f)));  // the bit
            }
            if (held && !wdSil)
                Circle(keys, 9f * kr * wdU, WdGold.WithAlpha(0.12f + 0.08f * Mathf.Sin(t * 10f)), 20);  // a glint

            // What he holds under his arms: the shotgun.
            if (pose == WdPose.Gun)
            {
                V2 m = WdAt(hr.x + 41f, 5f);
                Cap(WdAt(hr.x - 7f, hr.y - 1f), WdAt(hr.x + 31f, 5f), 5.5f * wdU, Wd(WdGunMetal));
                Line(WdAt(hr.x + 10f, 4f), m, 3f * wdU, Wd(WdGunDark));
                Line(WdAt(hl.x - 5f, 5f), WdAt(hl.x + 5f, 5f), 6.5f * wdU, Wd(WdWood));  // the pump
                if (recoil > 0.55f && !wdSil)
                    WdMuzzle(m, fa, recoil);
            }
            if (pose == WdPose.Spin)
            {
                Cap(WdAt(2f, 30f), WdAt(2f, 60f), 5.5f * wdU, Wd(WdGunMetal));
                Line(WdAt(2f, 50f), WdAt(2f, 70f), 3f * wdU, Wd(WdGunDark));
            }

            // The arms.
            OfficerArm(WdAt(1f, -19f * sh), L, 9f * wdU * 0.55f, Wd(WdCoat), Wd(WdGlove));
            OfficerArm(WdAt(1f, 19f * sh), R, 9f * wdU * 0.55f, Wd(WdCoat), Wd(WdGlove));

            // What he holds over his arms.
            switch (pose)
            {
                case WdPose.Walk: case WdPose.Radio: case WdPose.Cover: case WdPose.Keys: case WdPose.Kneel: case WdPose.Brace: case WdPose.Dazed: case WdPose.Point: case WdPose.Spread:
                {
                    V2 tip = pose == WdPose.Point ? WdAt(hr.x + 22f, hr.y - 4f) : WdAt(hr.x + 17f, hr.y + 5f);
                    WdBatonTo(R, tip, pose == WdPose.Point ? 0.8f + 0.2f * Mathf.Sin(t * 6f) : 0.6f);
                    break;
                }
                case WdPose.Spin:
                    WdBatonTo(L, WdAt(2f, -60f), 1f);
                    break;
                case WdPose.WindUp:
                    WdBatonTo(R, WdAt(hr.x - 19f, hr.y + 9f), 1f);
                    break;
                case WdPose.Slam:
                    WdBatonTo(R, WdAt(hr.x + 17f * (1f - k * 0.75f), hr.y + 2f), 0.6f + 0.4f * k);
                    break;
                case WdPose.Throw:
                    if (k < 0.85f)
                    {
                        Circle(R, 5.5f * wdU + OffInk, GuardInk, 16);
                        Circle(R, 5.5f * wdU, Wd(WdCan), 16);
                        Line(R - wdF * (4f * wdU), R + wdF * (4f * wdU), 1.5f * wdU, Wd(new Rgba(0.78f, 0.83f, 0.54f)));
                    }
                    break;
            }
            if (pose == WdPose.Radio)
            {
                V2 a = L + wdF * (1f * wdU) - wdS * (9f * wdU), b = L - wdS * (17f * wdU);
                Cap(L + wdS * (2f * wdU), a, 6f * wdU, Wd(new Rgba(0.15f, 0.16f, 0.19f)));
                Line(a, b, 1.4f * wdU, Wd(new Rgba(0.33f, 0.33f, 0.33f)));
                Circle(L - wdS * (5f * wdU), 1.7f * wdU, Mathf.Sin(t * 16f) > 0f ? new Rgba(1f, 0.29f, 0.29f) : new Rgba(0.29f, 0.06f, 0.06f), 8);
                if (!wdSil)
                    for (int i = 0; i < 3; i++)  // the call going out to the tower
                    {
                        float q = MathX.Fmod(t * 1.6f + i / 3f, 1f), up = (Tower - pos).Angle;
                        Arc(pos, r + 6f + q * 70f, 1.6f, new Rgba(1f, 0.35f, 0.35f, 0.6f * (1f - q)), 12, up - 0.5f, 1f);
                    }
            }

            // The cap: its peak out front, the crimson band, the gold badge.
            float hx = 3f + crouch * 3f;
            V2 head = WdAt(hx, 0f);
            HalfDisc(head + wdF * wdU, 15.5f * wdU + OffInk, fa, GuardInk);
            HalfDisc(head + wdF * wdU, 15.5f * wdU, fa, Wd(WdPeak));
            Circle(WdAt(hx - 1f, 0f), 12f * wdU + OffInk, GuardInk, 28);
            Circle(WdAt(hx - 1f, 0f), 12f * wdU, Wd(WdCrown), 28);
            Arc(WdAt(hx - 1f, 0f), 11.2f * wdU, 1.4f * wdU, Wd(WdTrim.Mix(GuardInk, 0.3f)), 28);
            Circle(WdAt(hx - 2f, 0f), 7.3f * wdU, Wd(WdCrown.Mix(Rgba.White, 0.08f)), 24);
            V2 bd = WdAt(hx + 8.4f, 0f);
            SetColor(Wd(WdGold));
            Quad(bd - wdF * (3f * wdU), bd - wdS * (3.6f * wdU), bd + wdF * (3.2f * wdU), bd + wdS * (3.6f * wdU));
            Arc(head + wdF * wdU, 15.5f * wdU, 1.2f, new Rgba(1f, 1f, 1f, wdSil ? 0f : 0.2f), 12, fa - 0.9f, 1.8f);
        }

        /// Under him: the cross's baton slam (a crack ring and dust), the charge's speed lines and the skid's dust.
        void WardenFloorFx(Enemy e, V2 pos, float r, float t)
        {
            var w = e.Warden;
            if (w == null || w.TransT > 0f || w.StunT > 0f || game.IsSpawning(e))
                return;
            if (w.Turn == WardenTurn.Cross && !w.Dashing)
            {
                float q = (MathX.Fmod(w.TurnT, 3f) - 1.15f) / 0.6f;
                if (q >= 0f && q < 1f)
                {
                    Arc(pos, r + 6f + q * 80f, 4f * (1f - q), new Rgba(1f, 0.78f, 0.47f, 1f - q), 40);
                    int plant = (int)(w.TurnT / 3f);
                    for (int c = 0; c < 5; c++)  // the cracks
                    {
                        float a = c * 1.26f + plant;
                        V2 prev = pos + V2.FromAngle(a) * (r * 0.5f);
                        for (int s = 1; s < 4; s++)
                        {
                            V2 next = pos + V2.FromAngle(a + (Hash01(c * 7 + s + plant) - 0.5f) * 0.5f) * (r * 0.5f + s * 14f);
                            Line(prev, next, 2f, new Rgba(1f, 0.67f, 0.31f, 0.8f * (1f - q)));
                            prev = next;
                        }
                    }
                    for (int c = 0; c < 6; c++)
                        Circle(pos + V2.FromAngle(c * 1.05f + 0.3f) * (r * 0.6f + q * 40f), 4f + q * 9f, new Rgba(0.6f, 0.55f, 0.5f, 0.22f * (1f - q)), 10);
                }
            }
            if (w.Turn == WardenTurn.Charge)
            {
                float run = 0.9f + 620f / 700f, tt = w.TurnT;
                V2 f = V2.FromAngle(w.Aux), s = f.Orthogonal();
                if (tt >= 0.9f && tt < run)
                    for (int i = 0; i < 6; i++)
                    {
                        float off = (Hash01(i + 3) - 0.5f) * r * 1.6f, len = 40f + Hash01(i + 11) * 60f;
                        V2 a = pos - f * (r * 0.6f) + s * off;
                        Line(a, a - f * len, 2f, new Rgba(1f, 0.8f, 0.6f, 0.5f));
                    }
                float q = (tt - run) / 0.8f;
                if (q >= 0f && q < 1f)
                    for (int i = 0; i < 5; i++)
                        Circle(pos - f * (r * 0.4f + i * 12f) + s * ((i % 2 == 0 ? 1f : -1f) * r * 0.45f), 6f + q * 14f, new Rgba(0.63f, 0.59f, 0.55f, 0.3f * (1f - q)), 12);
            }
        }

        /// A filled polygon (convex enough for a fan from its first point), grown by `grow` for an outline.
        void WdFan(V2[] p, Rgba c, float grow)
        {
            V2 mid = V2.Zero;
            foreach (var q in p)
                mid += q;
            mid = mid * (1f / p.Length);
            SetColor(c);
            for (int i = 0; i < p.Length; i++)
            {
                V2 a = p[i], b = p[(i + 1) % p.Length];
                if (grow > 0f)
                {
                    a += (a - mid).Normalized() * grow;
                    b += (b - mid).Normalized() * grow;
                }
                Tri(mid, a, b);
            }
        }

        void WdBatonTo(V2 hand, V2 tip, float lit)
        {
            GuardStick(hand, tip, 3.6f * wdU * 0.8f, Wd(WdBaton));
            if (wdSil)
                return;
            Circle(tip, 4.5f * wdU, WdTip.WithAlpha(0.2f * lit), 12);
            Circle(tip, 2.2f * wdU, WdTip.WithAlpha(0.9f * lit), 10);
        }

        void WdMuzzle(V2 m, float fa, float recoil)
        {
            float k = (recoil - 0.55f) / 0.45f;
            SetColor(new Rgba(1f, 0.86f, 0.55f, 0.95f * k));
            for (int i = 0; i < 6; i++)
            {
                float a = fa + i * MathX.Tau / 6f;
                Tri(m, m + V2.FromAngle(a - 0.3f) * (6f * wdU), m + V2.FromAngle(a) * (16f * wdU * (i == 0 ? 1.4f : 0.8f)));
                Tri(m, m + V2.FromAngle(a) * (16f * wdU * (i == 0 ? 1.4f : 0.8f)), m + V2.FromAngle(a + 0.3f) * (6f * wdU));
            }
            Circle(m, 5f * wdU, Rgba.White.WithAlpha(k), 12);
        }
    }
}
