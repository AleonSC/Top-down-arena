using UnityEngine;

namespace Arena
{
    /// The Bo Staff's extra looks (Core/BoStaff.cs; Docs/Mockups/bo-staff-rework.html). Its slash, jab and spin are ordinary
    /// melee swings, drawn by MeleeLooks.cs with the Bo Staff's sage energy trail. This file adds the shove (FxKind.StaffShove: the
    /// staff turns sideways, is drawn back, driven out as a bar, then drawn in, with pressure arcs rolling off its front) and the
    /// three pips over the staff-bearer's head that show where the chain is.
    public partial class ArenaApp
    {
        const float ShovePull = 0.1f, ShovePush = 0.1f, ShoveHoldT = 0.09f, ShoveBack = 0.1f;  // the pull-back, the drive out, the hold at the front, the draw in (the host lands the shove after ShovePull: Game.BoShoveWind)

        /// How far out the bar's middle is `T` seconds into the shove (`front` is where it ends).
        static float ShoveDist(float T, float front)
        {
            if (T < ShovePull)
                return Mathf.Lerp(18f, 6f, EaseOut3(T / ShovePull));
            if (T < ShovePull + ShovePush)
                return Mathf.Lerp(6f, front, EaseOut3((T - ShovePull) / ShovePush));
            float held = ShovePull + ShovePush + ShoveHoldT;
            if (T < held)
                return front;
            return Mathf.Lerp(front, 18f, Mathf.Clamp01((T - held) / ShoveBack));
        }

        /// One layer of the shove: the solid staff and sparks (light false) or the energy (light true, the additive glow pass).
        /// False: not a player's shove (nothing to draw it with).
        bool DrawStaffShoveLayer(Effect fx, bool light)
        {
            V2 to = fx.Pos2 - fx.Pos;
            float reach = to.Length, half = fx.Radius;
            if (reach < 1f)
                return false;
            var seen = MeleeOwnerOf(fx, 0.5f, false, ShovePull + ShovePush + ShoveHoldT + ShoveBack + 0.1f);
            if (seen == null)
                return false;
            V2 o = MeleeOrigin(fx, seen), dir = to / reach, side = dir.Orthogonal();
            var col = MeleeColor(seen.Weapon, fx.Color);
            float T = fx.T, front = Mathf.Max(30f, reach - 6f);
            float d = ShoveDist(T, front);
            float gone = ShovePull + ShovePush + ShoveHoldT + ShoveBack;
            float fade = T < gone ? 1f : 1f - Mathf.Clamp01((T - gone) / 0.12f);
            V2 c = o + dir * d;
            V2 a = c - side * half, b = c + side * half;
            float landed = T - ShovePull;  // time since it hit
            if (light)
            {
                // The bar's glow, brightest as it drives out, and three pressure arcs rolling off its front.
                float drive = T < ShovePull ? 0.35f : 1f;
                SoftLine(a, b, 22f * fade * drive + 4f, col.WithAlpha(0.5f * fade * drive));
                SoftLine(a, b, 9f * fade + 2f, col.WithAlpha(0.6f * fade));
                Line(a, b, 2.5f, Rgba.White.WithAlpha(0.9f * fade));
                if (landed > 0f)
                {
                    float wv = EaseOut3(Mathf.Clamp01(landed / 0.35f));
                    V2 centre = o + dir * (front - 70f);
                    float aim = to.Angle;
                    for (int i = 0; i < 3; i++)
                    {
                        float k = Mathf.Clamp01(wv * 1.15f - i * 0.12f), r = 70f + 18f * (i + 1) * k + 6f * i;
                        float al = (0.75f - 0.2f * i) * (1f - Mathf.Clamp01(landed / 0.45f));
                        if (al > 0.01f)
                            Arc(centre, r, 3.5f - i * 0.6f, col.WithAlpha(al), 20, aim - 0.62f, 1.24f);
                    }
                }
                return true;
            }
            // The solid staff, crosswise in both gloved hands (DrawBoStaffHeld), then sparks where it lands.
            if (fade > 0f)
            {
                Player owner;
                game.Players.TryGetValue(seen.Owner, out owner);
                DrawBoStaffHeld(o, dir * d, side, -15f, 15f, to.Angle, owner != null ? owner.Def.BodyR : 16f, fade, ArmColor(seen.Owner));
            }
            if (landed > 0f && landed < 0.3f)
            {
                uint r = (uint)MeleeSeed(fx);
                for (int i = 0; i < 12; i++)
                {
                    float lat = (Rand01(ref r) * 2f - 1f) * half, v = 60f + 70f * Rand01(ref r), age = landed - 0.04f * Rand01(ref r);
                    if (age < 0f)
                        continue;
                    V2 p = o + dir * (front + 4f + v * age) + side * lat * (1f + 0.3f * age);
                    Square(p, 3f, (i % 3 == 0 ? Rgba.White : col).WithAlpha(0.9f * (1f - age / 0.3f)));
                }
            }
            return true;
        }

        // --- Held and swung (Docs/Mockups/bo-staff-animations.html, Sage ribbon) ------------------------------------------------
        // The slash, jab and spin are drawn from the swing's effect: it starts when the host lands the hit, so the staff is already
        // drawn back (the mockup's wind-up is not played) and the pose runs on from the strike: sweep or thrust, follow-through,
        // back to the carry. Both gloved hands stay on the staff, joined to shoulders by arms; the Sage ribbon is the soft sweep
        // along the tip. Visual effects off keep the old stick and glow.

        const float BoHalf = 67f;  // half the staff's length: it runs from 29 px behind you to the reach (105) with its grip at 38 px
        static readonly float[] BoStrikeAt = { 0.07f, 0.10f, 0.08f }, BoTotal = { 0.32f, 0.30f, 0.80f }, BoNominal = { 105f, 140f, 100f }, BoPMax = { 40f, 73f, 33f };
        static readonly float[] BoTrailA = { 0.05f, 0.10f, 0.06f }, BoTrailB = { 0.20f, 0.18f, 0.54f };

        struct BoPose { public float A, P, G1, G2; }
        struct BoKey { public float T; public int E; public BoPose V; }  // E: 0 linear, 1 ease out, 2 ease in-out

        static BoKey BK(float t, int e, float a, float p, float g1, float g2) => new BoKey { T = t, E = e, V = new BoPose { A = a, P = p, G1 = g1, G2 = g2 } };
        static BoKey BoRest(float t, float turns = 0f) => BK(t, 2, -0.6f + turns * MathX.Tau, 22f, -14f, 20f);

        static readonly BoKey[][] BoKeys =
        {
            new[] { BoRest(0f), BK(0.07f, 1, -1.55f, 14f, -18f, 16f), BK(0.19f, 2, 1.3f, 40f, -24f, 22f), BoRest(0.32f) },                                    // slash
            new[] { BoRest(0f), BK(0.05f, 1, 0f, 36f, -22f, 18f), BK(0.10f, 2, 0f, 5f, -20f, 10f), BK(0.16f, 1, 0f, 73f, -24f, 22f), BK(0.20f, 0, 0f, 73f, -24f, 22f), BoRest(0.30f) },  // jab
            new[] { BoRest(0f), BK(0.08f, 1, -2f, 24f, -16f, 20f), BK(0.52f, 2, -0.6f + MathX.Tau, 33f, -20f, 20f), BoRest(0.80f, 1f) },                     // spin
        };

        static float BoEase(int e, float u) => e == 0 ? u : e == 1 ? EaseOut3(u) : (u < 0.5f ? 4f * u * u * u : 1f - Mathf.Pow(-2f * u + 2f, 3f) / 2f);

        /// The staff's pose `tm` seconds into move `mv` (0 slash, 1 jab, 2 spin). `dp` is how much longer than the stock reach it is (the +1 upgrade).
        static BoPose BoPoseAt(int mv, float tm, float dp)
        {
            var ks = BoKeys[mv];
            BoPose r;
            if (tm <= 0f)
                r = ks[0].V;
            else if (tm >= ks[ks.Length - 1].T)
                r = ks[ks.Length - 1].V;
            else
            {
                int i = 1;
                while (ks[i].T < tm)
                    i++;
                float u = BoEase(ks[i].E, (tm - ks[i - 1].T) / (ks[i].T - ks[i - 1].T));
                BoPose a = ks[i - 1].V, b = ks[i].V;
                r = new BoPose { A = Mathf.Lerp(a.A, b.A, u), P = Mathf.Lerp(a.P, b.P, u), G1 = Mathf.Lerp(a.G1, b.G1, u), G2 = Mathf.Lerp(a.G2, b.G2, u) };
            }
            r.P += dp * Mathf.Clamp01((r.P - 5f) / (BoPMax[mv] - 5f));
            return r;
        }

        /// The staff in two gloved hands: `centre` is its middle (from `o`), `dir` along it; `g1`, `g2` where the hands are along it from the middle.
        void DrawBoStaffHeld(V2 o, V2 centre, V2 dir, float g1, float g2, float aim, float bodyR, float alpha, Rgba arm)
        {
            if (alpha <= 0f)
                return;
            float k = bodyR / 16f;
            V2 c = o + centre, back = c - dir * BoHalf, tip = c + dir * BoHalf, fwd = V2.FromAngle(aim);
            V2 s1 = o + fwd * (1f * k) + fwd.Orthogonal() * (-12f * k), s2 = o + fwd * (1f * k) + fwd.Orthogonal() * (12f * k);
            V2 h1 = c + dir * g1, h2 = c + dir * g2;
            bool straight = s1.DistanceTo(h1) + s2.DistanceTo(h2) <= s1.DistanceTo(h2) + s2.DistanceTo(h1);
            V2 a1 = s1, b1 = straight ? h1 : h2, a2 = s2, b2 = straight ? h2 : h1;
            Rgba armc = arm.A > 0f ? arm : new Rgba(0.36f, 0.49f, 0.26f), dark = new Rgba(0.05f, 0.08f, 0.03f, alpha), leather = new Rgba(0.72f, 0.55f, 0.34f, alpha), hide = new Rgba(0.05f, 0.08f, 0.03f, alpha);
            Line(a1, b1, 7.4f, dark);
            Line(a2, b2, 7.4f, dark);
            Line(a1, b1, 5.2f, armc.WithAlpha(alpha));
            Line(a2, b2, 5.2f, armc.WithAlpha(alpha));
            // the staff: dark rim, wood, a pale highlight, sage ferrules each with a dark collar, a grip wrap under each hand
            Line(back, tip, 5.8f, new Rgba(0.08f, 0.055f, 0.03f, alpha));
            Line(back, tip, 4.1f, new Rgba(0.77f, 0.59f, 0.38f, alpha));
            Line(back + dir.Orthogonal() * 1.1f, tip + dir.Orthogonal() * 1.1f, 1f, new Rgba(1f, 0.94f, 0.84f, 0.35f * alpha));
            Line(back, back + dir * 10f, 6.4f, Game.BoStaffSage.WithAlpha(alpha));
            Line(tip, tip - dir * 10f, 6.4f, Game.BoStaffSage.WithAlpha(alpha));
            Line(back + dir * 10f, back + dir * 12f, 6.8f, new Rgba(0.16f, 0.2f, 0.12f, alpha));
            Line(tip - dir * 10f, tip - dir * 12f, 6.8f, new Rgba(0.16f, 0.2f, 0.12f, alpha));
            Line(h1 - dir * 5f, h1 + dir * 5f, 5f, new Rgba(0.24f, 0.17f, 0.1f, 0.85f * alpha));
            Line(h2 - dir * 5f, h2 + dir * 5f, 5f, new Rgba(0.24f, 0.17f, 0.1f, 0.85f * alpha));
            Circle(b1, 6.2f, hide, 14);
            Circle(b1, 4.9f, leather, 14);
            Circle(b2, 6.2f, hide, 14);
            Circle(b2, 4.9f, leather, 14);
        }

        /// The Bo Staff held at the ready in both gloved hands (the carry pose every swing returns to), drawn over the body in place
        /// of the model's own stick. It phases out while a swing or shove draws the staff, and back in after (HeldPhase).
        void DrawBoStaffIdle(Player p, float t)
        {
            if (!fxOn || p.Weapon.Id != WeaponId.BoStaff || p.Downed)
                return;
            float phase = HeldPhase(p.Id);
            if (phase <= 0f)
                return;
            var r = BoRest(0f).V;
            float bob = Mathf.Sin(t * 2.6f);
            V2 dir = V2.FromAngle(p.Aim + r.A + 0.02f * bob);
            DrawBoStaffHeld(p.Disp, dir * (r.P + Game.MeleeReach(p, p.Weapon.Reach) - BoNominal[0]), dir, r.G1, r.G2, p.Aim, p.Def.BodyR, phase, ArmColor(p.Id));
        }

        /// A slash (mv 0), jab (1) or spin (2) in the Bo Staff's look, from DrawSlashLayer. `light` is the additive pass.
        bool DrawBoStaffFx(Effect fx, bool light, MeleeSeen seen, int mv, float aim, float reach)
        {
            float span = BoTotal[mv] - BoStrikeAt[mv];
            if (fx.Dur < span + 0.02f)
            {
                fx.Dur = span + 0.02f;  // the whole motion, not just the flash (front end only)
                float now = Time.unscaledTime, st;
                swingTo[seen.Owner] = swingTo.TryGetValue(seen.Owner, out st) ? Mathf.Max(st, now + span) : now + span;
            }
            float T = fx.T, tm = T + BoStrikeAt[mv], dp = reach - BoNominal[mv];
            V2 o = MeleeOrigin(fx, seen);
            var col = MeleeColor(WeaponId.BoStaff, fx.Color);
            Player owner;
            game.Players.TryGetValue(seen.Owner, out owner);
            if (light)
            {
                // Sage ribbon: a soft sweep from the tip in toward the staff's upper half, brightest at the head and fading in 0.14 s
                // (only while it is struck, so the wind-up and the return leave nothing behind).
                const int N = 18;
                bool ok = false;
                V2 pTip = default(V2), pInn = default(V2);
                for (int i = 0; i <= N; i++)
                {
                    float ts = tm - i * 0.008f;
                    if (ts < BoTrailA[mv] || ts > BoTrailB[mv])
                    {
                        ok = false;
                        continue;
                    }
                    var P = BoPoseAt(mv, ts, dp);
                    V2 d = V2.FromAngle(aim + P.A), tip = o + d * (P.P + BoHalf), inn = tip - d * 52f;
                    if (ok)
                    {
                        float a = Mathf.Pow(1f - (float)i / N, 1.5f) * Mathf.Clamp01(pTip.DistanceTo(tip) / 9f);
                        var fill = col.WithAlpha(0.5f * a);
                        VTri(pTip, fill, tip, fill, inn, fill);
                        VTri(pTip, fill, inn, fill, pInn, fill);
                        Line(pTip, tip, 3.2f, new Rgba(0.96f, 1f, 0.9f, 0.85f * a));
                    }
                    ok = true;
                    pTip = tip;
                    pInn = inn;
                }
                if (mv == 1 && tm > 0.12f && tm < 0.3f)
                {
                    // the jab: a shock ring at the tip as it lands, and speed lines behind you
                    V2 d = V2.FromAngle(aim), tip = o + d * (BoNominal[mv] + dp + 0f);
                    float u = Mathf.Clamp01((tm - 0.14f) / 0.2f);
                    Arc(tip, 8f + 30f * EaseOut3(u), 3f * (1f - u) + 0.5f, col.WithAlpha(0.9f * (1f - u)), 20);
                    Arc(tip, 4f + 16f * EaseOut3(u), 1.6f, Rgba.White.WithAlpha(0.8f * (1f - u)), 16);
                    float lines = Mathf.Clamp01((tm - 0.1f) / 0.06f) * Mathf.Clamp01(1f - (tm - 0.16f) / 0.1f);
                    foreach (float y in new[] { -9f, 0f, 9f })
                        Line(o - d * 18f + d.Orthogonal() * y, o - d * (18f + 30f * lines) + d.Orthogonal() * y, 1.8f, Rgba.White.WithAlpha(0.35f * lines));
                }
                if (mv == 2 && tm > 0.5f && tm < 0.8f)
                {
                    float u = Mathf.Clamp01((tm - 0.5f) / 0.28f);  // the ring that lands on the floor when the spin ends
                    Arc(o, 70f + 34f * EaseOut3(u), 3.4f * (1f - u) + 0.5f, col.WithAlpha(0.8f * (1f - u)), 36);
                }
                return true;
            }

            // The solid layer: sparks where it cuts, then the staff held in both hands.
            int seed = MeleeSeed(fx);
            if (mv == 1)
                MeleeSparks(o, aim, 0f, reach, T, MeleeThrustTime, col, seed, true, 18);
            else
            {
                bool full = mv == 2;
                float half = full ? MathX.Pi : 65f * Mathf.Deg2Rad;
                float start = full ? aim + T * 14f - 1.6f : aim - half;
                MeleeSparks(o, start, 2f * half, reach, T, MeleeSweepTime, col, seed, false);
            }
            var pose = BoPoseAt(mv, tm, dp);
            V2 dir = V2.FromAngle(aim + pose.A);
            float fade = Mathf.Clamp01((BoTotal[mv] - tm) / 0.06f);
            DrawBoStaffHeld(o, dir * pose.P, dir, pose.G1, pose.G2, aim, owner != null ? owner.Def.BodyR : 16f, fade, ArmColor(seen.Owner));
            return true;
        }

        /// With Visual effects off, a plain bar sliding out and fading.
        void DrawStaffShovePlain(Effect fx, float f)
        {
            V2 to = fx.Pos2 - fx.Pos, dir = to.Normalized(), side = dir.Orthogonal();
            float k = Mathf.Clamp01(fx.T / 0.2f);
            V2 c = fx.Pos + dir * (18f + (to.Length - 24f) * EaseOut3(k));
            Line(c - side * fx.Radius, c + side * fx.Radius, 4f, fx.Color.WithAlpha(0.9f * (1f - f)));
        }

        /// Three pips over the Bo Staff's bearer: a lit pip for each step done since the chain began; with two lit, the third (the
        /// spin) pulses, since the next click is the finisher. There's no timer, so this is the only way to see where you are.
        void DrawBoStaffPips(Player p, float t)
        {
            if (p.Downed)
                return;
            V2 o = p.Disp + new V2(-14f, -(p.Def.BodyR + 14f));
            FillRect(o.X - 9f, o.Y - 6f, 46f, 12f, new Rgba(0f, 0f, 0f, 0.4f));
            for (int i = 0; i < 3; i++)
            {
                V2 at = o + new V2(i * 14f, 0f);
                bool lit = i < p.BoStep, finisher = i == 2 && p.BoStep == 2;
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * 12f);
                if (finisher)
                    Arc(at, 6f + 2f * pulse, 1.8f, Game.BoStaffSage.WithAlpha(0.5f + 0.4f * pulse), 12);
                Circle(at, i == 2 ? 4.5f : 3.5f, lit ? Game.BoStaffSage : new Rgba(0.78f, 0.9f, 0.62f, 0.25f), 10);
            }
        }
    }
}
