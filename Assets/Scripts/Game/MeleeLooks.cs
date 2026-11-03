using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Melee attacks in style E ("Weapon + energy", Docs/Mockups/melee-attacks.html, the owner's pick): the weapon
    /// itself, drawn crisply, swinging through a glowing arc that throws sparks off its tip and leaves embers. Thrusts
    /// stab out through a beam; the Lance slides out through a glowing box; the Warhammer is raised over your shoulder
    /// while it charges, then swings over in a burning arc into the floor (a glowing circle, a pillar of light, embers).
    /// Drawing only, on each screen, and only with Visual effects on: hits, reach and timing are the game's own (the
    /// hit lands as the effect starts; the sweep plays just after, as before), and with effects off DrawSlashFx /
    /// DrawJabFx draw exactly as they did. Bosses' and enemies' slashes keep the old look.
    public partial class ArenaApp
    {
        const float MeleeSweepTime = 0.11f;   // the blade's sweep across its arc
        const float MeleeLife = 0.55f;        // how long a swing's energy and embers last
        const float MeleeThrustTime = 0.07f;  // a thrust's stab out to full reach
        const float HammerSwingTime = 0.08f;  // the Warhammer's arc from over your shoulder into the floor

        static readonly Rgba MeleeSteel = new Rgba(0.9f, 0.93f, 0.99f), MeleeHandle = new Rgba(0.42f, 0.34f, 0.25f),
            MeleeGuard = new Rgba(0.85f, 0.75f, 0.48f), HammerHead = new Rgba(0.78f, 0.81f, 0.85f);

        /// What a melee effect belongs to, noted the first time it's drawn (the swinger moves on, the effect stays).
        class MeleeSeen
        {
            public int Owner;
            public WeaponId Weapon;
            public float Sign = 1f;       // which way it sweeps (Twin Blades and the Katana alternate)
            public float BaseReach;       // the weapon's own reach (a Warhammer aftershock reaches further)
        }

        readonly Dictionary<Effect, MeleeSeen> meleeSeen = new Dictionary<Effect, MeleeSeen>();
        readonly Dictionary<int, float> meleeFlip = new Dictionary<int, float>();
        readonly HashSet<Effect> meleeLive = new HashSet<Effect>();

        /// The player who made this melee effect (the one standing where it started, holding a melee weapon), or null.
        MeleeSeen MeleeOwnerOf(Effect fx, float life)
        {
            MeleeSeen seen;
            if (meleeSeen.TryGetValue(fx, out seen))
                return seen;
            Player best = null;
            float bd = 44f;
            foreach (var p in game.Players.Values)
            {
                if (p.Weapon == null || !p.Weapon.Melee)
                    continue;
                float d = Mathf.Min(p.Disp.DistanceTo(fx.Pos), p.Pos.DistanceTo(fx.Pos));
                if (d < bd)
                {
                    bd = d;
                    best = p;
                }
            }
            if (best == null)
                return null;
            if (meleeSeen.Count > 48)
            {
                meleeLive.Clear();
                foreach (var e in game.Effects)
                    meleeLive.Add(e);
                var gone = new List<Effect>();
                foreach (var k in meleeSeen.Keys)
                    if (!meleeLive.Contains(k))
                        gone.Add(k);
                foreach (var k in gone)
                    meleeSeen.Remove(k);
            }
            float flip;
            meleeFlip.TryGetValue(best.Id, out flip);
            bool alternates = best.Weapon.Id == WeaponId.TwinBlades || best.Weapon.Id == WeaponId.Katana;
            if (alternates)
                meleeFlip[best.Id] = flip = flip > 0f ? -1f : 1f;
            seen = new MeleeSeen { Owner = best.Id, Weapon = best.Weapon.Id, Sign = alternates ? flip : 1f, BaseReach = Game.MeleeReach(best, best.Weapon.Reach) };
            meleeSeen[fx] = seen;
            if (fx.Dur < life)
                fx.Dur = life;  // the energy outlasts the old flash (front end only; the effect is drawing, nothing reads it)
            return seen;
        }

        /// The swinger's body now (the weapon stays in their hand as they move), or where the swing started.
        V2 MeleeOrigin(Effect fx, MeleeSeen seen)
        {
            Player p;
            return game.Players.TryGetValue(seen.Owner, out p) && !p.Downed ? p.Disp : fx.Pos;
        }

        /// FxKind.Slash in style E. False: not a player's melee swing (draw the old look).
        bool DrawSlashE(Effect fx, float t)
        {
            if (!fxOn)
                return false;
            V2 to = fx.Pos2 - fx.Pos;
            float reach = to.Length, aim = to.Angle, half = fx.Radius;
            if (reach < 1f)
                return false;
            bool thrust = half < 0f, step = half >= 0f && half < 0.2f;
            if (step && fx.Dur > 0.25f)
                return false;  // a lunge or a dash streak, not a swing
            var seen = MeleeOwnerOf(fx, thrust ? 0.45f : step ? 0.3f : MeleeLife);
            if (seen == null || step && seen.Weapon != WeaponId.Halberd)
                return false;
            V2 o = MeleeOrigin(fx, seen);
            var col = fx.Color;
            float T = fx.T;
            int seed = (int)(fx.Pos.X * 13f + fx.Pos.Y * 7f + aim * 100f);

            if (thrust)
            {
                V2 dir = V2.FromAngle(aim);
                float ext = EaseOut3(Mathf.Clamp01(T / MeleeThrustTime)) * reach, fade = T < MeleeThrustTime ? 1f : 1f - Mathf.Clamp01((T - MeleeThrustTime) / (fx.Dur - MeleeThrustTime));
                V2 tip = o + dir * Mathf.Max(20f, ext);
                Line(o + dir * 18f, tip, 12f * fade, col.WithAlpha(0.22f * fade));
                Line(o + dir * 18f, tip, 6f * fade, col.WithAlpha(0.45f * fade));
                Line(o + dir * 18f, tip, 2.5f, Rgba.White.WithAlpha(0.95f * fade));
                GlowDisc(tip, 18f * fade + 4f, col, 0.6f * fade);
                MeleeSparks(o, aim, 0f, reach, T, MeleeThrustTime, col, seed, true);
                DrawMeleeModel(seen.Weapon, o, aim, Mathf.Max(24f, ext), T < MeleeThrustTime + 0.1f ? 1f : 1f - Mathf.Clamp01((T - MeleeThrustTime - 0.1f) / 0.15f), col);
                return true;
            }
            if (step)
            {
                // One step of the Halberd's sweep (six in a row): a slice of glowing arc, and the halberd at this angle
                // for a moment, so the steps read as one blade sweeping across.
                float fade = 1f - Mathf.Clamp01(T / fx.Dur);
                EnergyBand(o, reach * 0.7f, reach, aim - half, aim + half, col, 0.35f * fade, 0.35f * fade, 3);
                Arc(o, reach * 0.93f, 2.5f, Rgba.White.WithAlpha(0.9f * fade), 3, aim - half, 2f * half);
                MeleeSparks(o, aim - half, 2f * half, reach, T, 0.035f, col, seed, false, 4);
                if (!NewerStepThan(fx, seen))  // only the latest step holds the halberd, so there's one blade at a time
                    DrawMeleeModel(seen.Weapon, o, aim, reach, T < 0.12f ? 1f : 1f - Mathf.Clamp01((T - 0.12f) / 0.15f), col);
                return true;
            }

            // A sweep across its arc (a full circle spins).
            bool full = half >= MathX.Pi - 0.01f;
            float s = seen.Sign, e = EaseOut3(Mathf.Clamp01(T / MeleeSweepTime));
            float fadeOut = T < MeleeSweepTime ? 1f : 1f - Mathf.Clamp01((T - MeleeSweepTime) / (fx.Dur - MeleeSweepTime));
            float start = full ? aim + T * 14f * s - 1.6f * s : aim - half * s;
            float at = full ? aim + T * 14f * s : start + 2f * half * s * e;
            EnergyBand(o, reach * 0.58f, reach * 1.06f, start, at, col, 0f, 0.12f * fadeOut, 14);  // soft glow
            EnergyBand(o, reach * 0.72f, reach, start, at, col, 0.04f * fadeOut, 0.42f * fadeOut, 14);
            float a0 = Mathf.Min(start, at), sweep = Mathf.Abs(at - start);
            if (sweep > 0.01f)
                Arc(o, reach * 0.92f, 2.5f, Rgba.White.WithAlpha(0.95f * fadeOut), Mathf.Max(4, (int)(sweep * 10f)), a0, sweep);
            MeleeSparks(o, start, 2f * half * s, reach, T, MeleeSweepTime, col, seed, false);
            MeleeEmbers(o, start, at, reach, T, MeleeSweepTime, fx.Dur, col, seed);
            float hold = T < MeleeSweepTime + 0.1f ? 1f : 1f - Mathf.Clamp01((T - MeleeSweepTime - 0.1f) / 0.18f);
            if (hold > 0f)
            {
                DrawMeleeModel(seen.Weapon, o, at, reach, hold, col);
                if (T < MeleeSweepTime)
                    GlowDisc(o + V2.FromAngle(at) * reach * 0.95f, 14f, col, 0.7f);
            }
            return true;
        }

        /// Is there a later step of the same Halberd sweep than this one?
        bool NewerStepThan(Effect fx, MeleeSeen seen)
        {
            foreach (var e in game.Effects)
            {
                if (e != fx && e.Kind == FxKind.Slash && e.T < fx.T && Mathf.Abs(e.Radius - fx.Radius) < 0.001f && e.Pos.DistanceTo(fx.Pos) < 40f)
                    return true;  // a later step of the same sweep (same cone, from about the same spot)
            }
            return false;
        }

        /// FxKind.Jab in style E: the Lance's glowing box, or the Warhammer's slam. False: draw the old look.
        bool DrawJabE(Effect fx, float t)
        {
            if (!fxOn)
                return false;
            V2 to = fx.Pos2 - fx.Pos;
            float reach = to.Length, w = fx.Radius;
            if (reach < 1f)
                return false;
            var seen = MeleeOwnerOf(fx, 0.5f);
            if (seen == null || seen.Weapon != WeaponId.Lance && seen.Weapon != WeaponId.Warhammer)
                return false;
            V2 o = MeleeOrigin(fx, seen), dir = to / reach, side = dir.Orthogonal();
            float T = fx.T, aim = to.Angle;
            int seed = (int)(fx.Pos.X * 11f + fx.Pos.Y * 5f);
            var col = fx.Color;
            if (seen.Weapon == WeaponId.Warhammer)
            {
                DrawHammerSlamE(fx, seen, o, aim, reach, T, seed);
                return true;
            }
            // The Lance: the box glows from your body to its sliding front edge, the lance riding out at its front.
            float g = EaseOut3(Mathf.Clamp01(T / 0.09f)), front = reach * g;
            float fade = T < 0.09f ? 1f : 1f - Mathf.Clamp01((T - 0.09f) / (fx.Dur - 0.09f));
            V2 a0 = o + side * w, a1 = o - side * w, b0 = a0 + dir * front, b1 = a1 + dir * front;
            VTri(a0, col.WithAlpha(0.02f * fade), b0, col.WithAlpha(0.3f * fade), b1, col.WithAlpha(0.3f * fade));
            VTri(a0, col.WithAlpha(0.02f * fade), b1, col.WithAlpha(0.3f * fade), a1, col.WithAlpha(0.02f * fade));
            Line(a0, b0, 2f, col.WithAlpha(0.7f * fade));
            Line(a1, b1, 2f, col.WithAlpha(0.7f * fade));
            Line(b0, b1, 8f, col.WithAlpha(0.35f * fade));
            Line(b0, b1, 3f, Rgba.White.WithAlpha(0.95f * fade));
            GlowDisc(o + dir * front, w * 0.8f, col, 0.5f * fade);
            MeleeSparks(o, aim, 0f, reach, T, 0.09f, col, seed, true);
            DrawMeleeModel(WeaponId.Lance, o, aim, Mathf.Max(30f, front), T < 0.2f ? 1f : 1f - Mathf.Clamp01((T - 0.2f) / 0.15f), col);
            return true;
        }

        /// The Warhammer: from over your shoulder, a burning arc into the floor in front of you, then the impact. An
        /// aftershock (a later slam, further out) is just the impact.
        void DrawHammerSlamE(Effect fx, MeleeSeen seen, V2 o, float aim, float reach, float T, int seed)
        {
            var col = new Rgba(1f, 0.64f, 0.42f);
            bool aftershock = reach > seen.BaseReach + 1f;
            float d = 16f + seen.BaseReach * 0.65f + (reach - seen.BaseReach);
            V2 c = o + V2.FromAngle(aim) * d;
            float after = aftershock ? T : T - HammerSwingTime;
            if (!aftershock && T < HammerSwingTime)
            {
                // The swing: the head's burning path so far, and the hammer on it.
                float f = T / HammerSwingTime;
                const int n = 10;
                float t0 = Mathf.Max(0f, f - 0.7f);
                V2 prev = HammerHeadAt(o, aim, d, t0);
                for (int i = 1; i <= n; i++)
                {
                    float fi = t0 + (f - t0) * i / n;
                    V2 p = HammerHeadAt(o, aim, d, fi);
                    float k = (float)i / n;
                    Line(prev, p, 6f + 20f * k, col.WithAlpha(0.4f * k));
                    Line(prev, p, 2.5f, Rgba.White.WithAlpha(0.85f * k));
                    prev = p;
                }
                HammerPose(o, aim, d, f, 1f, true);
                return;
            }
            float life = 0.5f, k2 = Mathf.Clamp01(after / life);
            // The impact: a glow on the floor, a ring with turning ticks, a pillar of light, embers.
            GlowDisc(c, 62f, col, 0.75f * (1f - k2));
            Arc(c, 46f, 2.5f, col.WithAlpha(0.9f * (1f - k2)), 32);
            for (int i = 0; i < 12; i++)
            {
                float a = i / 12f * MathX.Tau + after * 2f;
                Arc(c, 38f, 2.5f, col.WithAlpha(0.8f * (1f - k2)), 2, a, 0.22f);
            }
            Arc(c, 20f + 70f * EaseOut3(Mathf.Min(1f, k2 * 1.6f)), 4f * (1f - k2) + 1f, Rgba.White.WithAlpha(0.7f * (1f - k2)), 32);
            if (after < 0.22f)
            {
                float pk = 1f - after / 0.22f;
                for (int i = 0; i < 6; i++)  // the pillar, fading upward (the arena's up is -y)
                    Line(c + new V2(0f, -i * 20f), c + new V2(0f, -(i + 1) * 20f), (12f * pk + 3f) * (1f - i / 7f), (i < 2 ? Rgba.White : col).WithAlpha(0.6f * pk * (1f - i / 6f)));
            }
            uint r = (uint)seed;
            for (int i = 0; i < 16; i++)
            {
                float a = Rand01(ref r) * MathX.Tau, v = 30f + 50f * Rand01(ref r), age = after - 0.05f * Rand01(ref r);
                if (age < 0f)
                    continue;
                V2 p = c + V2.FromAngle(a) * (20f + v * age) + new V2(0f, -40f * age);
                Circle(p, 1.8f, (i % 2 == 0 ? col : new Rgba(1f, 0.89f, 0.69f)).WithAlpha(0.9f * (1f - k2)), 4);
            }
            Player owner;
            bool charging = game.Players.TryGetValue(seen.Owner, out owner) && owner.DrawT > 0f;  // raised again for the next slam
            if (!aftershock && !charging)
                HammerPose(o, aim, d, 1f, 1f - Mathf.Clamp01((after - 0.15f) / 0.2f), after < 0.15f);
        }

        /// The Warhammer while it charges: raised up over your shoulder, higher the more it's charged.
        void DrawHammerRaised(Player p, float charge)
        {
            if (!fxOn)
                return;
            float d = 16f + Game.MeleeReach(p, p.Weapon.Reach) * 0.65f;
            HammerPose(p.Disp, p.Aim, d, -0.3f - 0.7f * charge, 1f, charge >= 1f);
        }

        /// Where the hammer's head is at `f` through its swing (0 raised over your left shoulder, 1 in the floor at d).
        /// Below 0: on the way up (-1 raised fully, -0.3 at your side).
        static V2 HammerHeadAt(V2 o, float aim, float d, float f)
        {
            float a, h, r;
            HammerArc(aim, d, f, out a, out h, out r);
            return o + V2.FromAngle(a) * r + new V2(0f, -26f * h);
        }

        static void HammerArc(float aim, float d, float f, out float a, out float h, out float r)
        {
            float rest = aim - 1.1f, up = aim - 2.6f;
            if (f < 0f)  // being raised (charging)
            {
                float k = Mathf.Clamp01(-f);
                a = rest + (up - rest) * k;
                h = 0.3f + 0.7f * k;
                r = d * 0.6f;
                return;
            }
            float e = f * f;  // speeding up as it falls
            a = up + (aim - up) * e;
            h = 1f - e;
            r = d * (0.6f + 0.4f * e);
        }

        /// The hammer itself at `f` through its arc: its handle from your body, its shadow on the floor, its head
        /// drawn bigger the higher it is.
        void HammerPose(V2 o, float aim, float d, float f, float alpha, bool glow)
        {
            if (alpha <= 0f)
                return;
            float a, h, r;
            HammerArc(aim, d, f, out a, out h, out r);
            V2 ground = o + V2.FromAngle(a) * r, head = ground + new V2(0f, -26f * h);
            float sk = 1f - 0.5f * h;
            Circle(ground + new V2(0f, 4f), 13f * sk, new Rgba(0f, 0f, 0f, (0.22f + 0.25f * (1f - h)) * alpha), 12);
            Line(o + V2.FromAngle(a) * 12f, head, 4f, MeleeHandle.WithAlpha(alpha));
            float sc = 1f + 0.55f * h;
            V2 dir = V2.FromAngle(a), side = dir.Orthogonal();
            if (glow)
                GlowDisc(head, 26f * sc, new Rgba(1f, 0.64f, 0.42f), 0.6f * alpha);
            V2 hx = dir * 9f * sc, hy = side * 16f * sc;
            SetColor(HammerHead.WithAlpha(alpha));
            Quad(head - hx - hy, head + hx - hy, head + hx + hy, head - hx + hy);
            var band = new Rgba(1f, 0.64f, 0.42f, 0.95f * alpha);
            V2 bandY = side * 5f * sc;
            SetColor(band);
            Quad(head - hx - hy, head + hx - hy, head + hx - hy + bandY, head - hx - hy + bandY);
            Quad(head - hx + hy - bandY, head + hx + hy - bandY, head + hx + hy, head - hx + hy);
        }

        // --- Pieces ------------------------------------------------------------------------------------------------

        /// A glowing band between two radii over an angle range, its alpha going from aFrom at `from` to aTo at `to`.
        void EnergyBand(V2 o, float r0, float r1, float from, float to, Rgba col, float aFrom, float aTo, int steps)
        {
            if (Mathf.Abs(to - from) < 0.005f)
                return;
            float rm = (r0 + r1) / 2f, wd = r1 - r0;
            for (int i = 0; i < steps; i++)
            {
                float a0 = from + (to - from) * i / steps, a1 = from + (to - from) * (i + 1) / steps;
                float al = aFrom + (aTo - aFrom) * (i + 0.5f) / steps;
                if (al <= 0.005f)
                    continue;
                Arc(o, rm, wd, col.WithAlpha(al), 1, Mathf.Min(a0, a1), Mathf.Abs(a1 - a0));
            }
        }

        static float Rand01(ref uint s)
        {
            s = s * 1664525u + 1013904223u;
            return (s >> 8) / 16777216f;
        }

        /// Sparks thrown off the tip as it passes (a sweep from `start` over `sweep`), or sprayed along a thrust.
        void MeleeSparks(V2 o, float start, float sweep, float reach, float T, float active, Rgba col, int seed, bool line, int count = 18)
        {
            uint r = (uint)seed;
            float s = sweep >= 0f ? 1f : -1f;
            for (int i = 0; i < count; i++)
            {
                float born = active * Rand01(ref r), spd = 110f + 160f * Rand01(ref r), spread = (Rand01(ref r) - 0.5f) * 0.9f;
                float age = T - born;
                if (age < 0f || age > 0.45f)
                    continue;
                V2 from;
                float oa;
                if (line)
                {
                    from = o + V2.FromAngle(start) * (24f + reach * born / Mathf.Max(active, 1e-4f));
                    oa = start + spread * 1.4f;
                }
                else
                {
                    float ba = start + sweep * EaseOut3(born / Mathf.Max(active, 1e-4f));
                    from = o + V2.FromAngle(ba) * reach;
                    oa = ba + s * 0.9f + spread;
                }
                float dd = spd * age * (1f - age);
                Circle(from + V2.FromAngle(oa) * dd, 1.6f, (i % 3 == 0 ? Rgba.White : col).WithAlpha(0.9f * (1f - age / 0.45f)), 4);
            }
        }

        /// Embers left along the arc once it's swept: they drift outward and fade.
        void MeleeEmbers(V2 o, float start, float end, float reach, float T, float active, float life, Rgba col, int seed)
        {
            if (T < active)
                return;
            uint r = (uint)(seed * 7 + 3);
            float k = Mathf.Clamp01((T - active) / Mathf.Max(0.01f, life - active));
            for (int i = 0; i < 7; i++)
            {
                float f = Rand01(ref r), a = start + (end - start) * f, drift = 10f + 22f * Rand01(ref r);
                V2 p = o + V2.FromAngle(a) * (reach * (0.8f + 0.2f * Rand01(ref r)) + drift * k) + new V2(0f, -12f * k);
                Circle(p, 1.5f, col.Mix(Rgba.White, 0.3f).WithAlpha(0.8f * (1f - k)), 4);
            }
        }

        /// The weapon itself, from your body out along `ang` to `reach`.
        void DrawMeleeModel(WeaponId id, V2 o, float ang, float reach, float alpha, Rgba col)
        {
            if (alpha <= 0f)
                return;
            V2 dir = V2.FromAngle(ang), side = dir.Orthogonal();
            float r0 = 12f, tip = reach * 0.97f;
            var steel = MeleeSteel.WithAlpha(alpha);
            var edge = col.WithAlpha(0.9f * alpha);
            switch (id)
            {
                case WeaponId.Rapier:
                    Line(o + dir * r0, o + dir * tip, 2f, steel);
                    Arc(o + dir * (r0 + 6f), 6f, 2.5f, MeleeGuard.WithAlpha(alpha), 8, ang - 1.6f, 3.2f);
                    break;
                case WeaponId.Halberd:
                {
                    Line(o + dir * (r0 - 4f), o + dir * tip, 3.5f, MeleeHandle.WithAlpha(alpha));
                    V2 h0 = o + dir * (tip - 34f), h1 = o + dir * (tip - 12f);
                    SetColor(steel);
                    Quad(h0, h1, h1 + side * 14f, h0 + side * 18f);  // the axe blade
                    Line(h0 + side * 18f, h1 + side * 14f, 1.5f, edge);
                    Tri(o + dir * (tip + 8f), h1 + side * 3f, h1 - side * 3f);  // the spike
                    break;
                }
                case WeaponId.Lance:
                {
                    SetColor(steel);
                    V2 b = o + dir * r0;
                    Quad(b + side * 6f, b - side * 6f, o + dir * (tip - 20f) - side * 2.5f, o + dir * (tip - 20f) + side * 2.5f);
                    Tri(o + dir * tip, o + dir * (tip - 22f) + side * 4f, o + dir * (tip - 22f) - side * 4f);
                    Line(b + side * 7f, b - side * 7f, 3f, MeleeGuard.WithAlpha(alpha));
                    Line(o + dir * (r0 + 4f), o + dir * (tip - 6f), 1.2f, edge);
                    break;
                }
                case WeaponId.HolyMace:  // the Quarterstaff
                case WeaponId.BoStaff:
                    Line(o - dir * reach * 0.25f, o + dir * tip, 4f, MeleeHandle.WithAlpha(alpha));
                    Line(o + dir * (tip - 10f), o + dir * tip, 5f, MeleeGuard.WithAlpha(alpha));
                    Line(o - dir * reach * 0.25f, o - dir * (reach * 0.25f - 8f), 5f, MeleeGuard.WithAlpha(alpha));
                    break;
                case WeaponId.Censer:
                    Line(o + dir * r0, o + dir * (tip - 12f), 1.5f, MeleeGuard.WithAlpha(alpha));
                    Circle(o + dir * (tip - 6f), 7f, MeleeGuard.WithAlpha(alpha), 10);
                    Arc(o + dir * (tip - 6f), 7f, 1.5f, edge, 10);
                    break;
                case WeaponId.Kusarigama:
                {
                    Line(o + dir * r0, o + dir * (tip - 8f), 1.5f, new Rgba(0.7f, 0.72f, 0.78f, alpha));  // the chain
                    V2 h = o + dir * tip;
                    Arc(h - side * 8f, 10f, 3f, steel, 8, ang - 0.2f, 1.9f);  // the sickle
                    break;
                }
                case WeaponId.BloodLash:
                {
                    V2 prev = o + dir * r0;  // a whip: a wavy line, thinning out
                    for (int i = 1; i <= 8; i++)
                    {
                        float f = i / 8f;
                        V2 p = o + dir * (r0 + (tip - r0) * f) + side * (Mathf.Sin(f * 6f) * 5f * f);
                        Line(prev, p, 3.5f - 2.5f * f, new Rgba(0.75f, 0.12f, 0.16f, alpha));
                        prev = p;
                    }
                    break;
                }
                default:
                {
                    // A sword: handle, guard, blade (wider for the Greatsword, slim for the Twin Blades and Katana).
                    float wd = id == WeaponId.Greatsword ? 6f : id == WeaponId.TwinBlades || id == WeaponId.Katana ? 3.5f : 4.5f;
                    float hilt = r0 + (tip - r0) * (id == WeaponId.Greatsword ? 0.2f : 0.28f);
                    Line(o + dir * (r0 - 6f), o + dir * hilt, 4f, MeleeHandle.WithAlpha(alpha));
                    Line(o + dir * hilt + side * 7f, o + dir * hilt - side * 7f, 3f, MeleeGuard.WithAlpha(alpha));
                    SetColor(id == WeaponId.Lightbringer ? new Rgba(1f, 0.95f, 0.75f, alpha) : steel);
                    V2 b0 = o + dir * hilt, b1 = o + dir * (tip - 10f);
                    Quad(b0 + side * wd, b1 + side * wd * 0.8f, b1 - side * wd * 0.6f, b0 - side * wd * 0.6f);
                    Tri(o + dir * tip, b1 + side * wd * 0.8f, b1 - side * wd * 0.6f);
                    Line(b0 + side * wd, b1 + side * wd * 0.8f, 1.2f, edge);
                    break;
                }
            }
        }
    }
}
