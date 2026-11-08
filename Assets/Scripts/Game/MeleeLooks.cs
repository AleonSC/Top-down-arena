using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Melee attacks in style E ("Weapon + energy", Docs/Mockups/melee-attacks.html, the owner's pick), ported from the
    /// mockup's numbers (arena px = the mockup's canvas px). Two layers, like the mockup's two:
    ///   - the weapon itself and its sparks, drawn normally (DrawSlashE / DrawJabE, from DrawSlashFx / DrawJabFx);
    ///   - the energy (the glowing band behind the blade, its soft halo, the white core, beams, the Lance's box, the
    ///     hammer's trail and impact), drawn in the additive glow pass (DrawMeleeGlow, from DrawGlowPass), which adds
    ///     light the way the mockup's "lighter" canvas does.
    /// Drawing only, on each screen, and only with Visual effects on: hits, reach and timing are the game's own (the hit
    /// lands as the effect starts; the sweep plays just after, as before). With effects off DrawSlashFx / DrawJabFx draw
    /// exactly as they did. Bosses' and enemies' slashes keep the old look.
    public partial class ArenaApp
    {
        const float MeleeSweepTime = 0.11f;   // the blade's sweep across its arc (the mockup's `active`)
        const float MeleeTail = 0.55f;        // the energy fading after it (the mockup's `tail`)
        const float MeleeThrustTime = 0.066f; // a thrust's stab out to full reach (60% of the sweep, as in the mockup)
        const float HammerSwingTime = 0.08f;  // the Warhammer's arc from over your shoulder into the floor
        const float HalberdSweepTime = Game.HalberdStepTime * Game.HalberdSteps;  // the Halberd's sweep: its six steps' 0.21 s
        const float MeleeBright = 0.6f;       // the energy band at its brightest (added light; the mockup's 0.45 plus its blur)

        static readonly Rgba MeleeSteel = new Rgba(0.9f, 0.93f, 0.99f), MeleeHandle = new Rgba(0.42f, 0.34f, 0.25f),
            MeleeGuard = new Rgba(0.85f, 0.75f, 0.48f), HammerHead = new Rgba(0.78f, 0.81f, 0.85f), HammerGlow = new Rgba(1f, 0.64f, 0.42f);

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
        readonly Dictionary<int, float> halberdLast = new Dictionary<int, float>();
        // The held weapon phases out while the swinging model is out, and back in after (HeldPhase, CharacterModels.cs).
        readonly Dictionary<int, float> swingFrom = new Dictionary<int, float>(), swingTo = new Dictionary<int, float>();
        const float HeldFadeOut = 0.04f, HeldFadeIn = 0.14f;

        /// 1 = the weapon is in hand, 0 = it's out (a swing's model is drawn in its place).
        float HeldPhase(int id)
        {
            float to, from;
            if (!swingTo.TryGetValue(id, out to) || !swingFrom.TryGetValue(id, out from))
                return 1f;
            float now = Time.unscaledTime;
            if (now < to)
                return 1f - Mathf.Clamp01((now - from) / HeldFadeOut);
            return Mathf.Clamp01((now - to) / HeldFadeIn);
        }

        readonly HashSet<Effect> meleeLive = new HashSet<Effect>();

        /// The player who made this melee effect (the one standing where it started, holding a melee weapon), or null.
        MeleeSeen MeleeOwnerOf(Effect fx, float life, bool thrust = false, float hold = 0.4f)
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
            if (alternates && !(thrust && best.Weapon.Id == WeaponId.Katana))  // (the Katana's lunge is a thrust between its swipes: it doesn't turn the next swipe round)
                meleeFlip[best.Id] = flip = flip > 0f ? -1f : 1f;
            else if (best.Weapon.Id == WeaponId.Halberd)
            {
                // The Halberd sweeps left to right; an extra projectile's second pass (0.29 s later) sweeps back.
                float last;
                bool backPass = halberdLast.TryGetValue(best.Id, out last) && Time.unscaledTime - last < 0.4f && flip > 0f;
                halberdLast[best.Id] = Time.unscaledTime;
                meleeFlip[best.Id] = flip = backPass ? -1f : 1f;
                alternates = true;
            }
            seen = new MeleeSeen { Owner = best.Id, Weapon = best.Weapon.Id, Sign = alternates ? flip : 1f, BaseReach = Game.MeleeReach(best, best.Weapon.Reach) };
            meleeSeen[fx] = seen;
            float now = Time.unscaledTime;
            float st;
            swingFrom[best.Id] = swingTo.TryGetValue(best.Id, out st) && st > now ? swingFrom[best.Id] : now;  // chained swings keep the weapon out
            swingTo[best.Id] = Mathf.Max(st, now + hold);
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

        /// Each melee weapon's energy colour (the mockup's for its five; the rest to match their look).
        static Rgba MeleeColor(WeaponId id, Rgba fallback)
        {
            switch (id)
            {
                case WeaponId.Greatsword: return new Rgba(0.56f, 0.78f, 1f);
                case WeaponId.TwinBlades: return new Rgba(1f, 0.82f, 0.48f);
                case WeaponId.Halberd: return new Rgba(0.56f, 0.94f, 0.78f);
                case WeaponId.Rapier: return new Rgba(0.8f, 0.72f, 1f);
                case WeaponId.Warhammer: return new Rgba(1f, 0.64f, 0.42f);
                case WeaponId.Lance: return new Rgba(0.62f, 0.88f, 1f);
                case WeaponId.HolyMace: return new Rgba(1f, 0.88f, 0.54f);
                case WeaponId.Lightbringer: return new Rgba(1f, 0.94f, 0.62f);
                case WeaponId.Censer: return new Rgba(1f, 0.8f, 0.5f);
                case WeaponId.Katana: return new Rgba(0.745f, 0.87f, 1f);  // Moon Steel (Docs/Mockups/katana-reach.html)
                case WeaponId.BoStaff: return new Rgba(0.78f, 0.9f, 0.62f);
                case WeaponId.Kusarigama: return new Rgba(0.7f, 0.64f, 1f);
                case WeaponId.BloodLash: return new Rgba(1f, 0.32f, 0.38f);
                default: return fallback;
            }
        }

        static int MeleeSeed(Effect fx) => (int)(fx.Pos.X * 13f + fx.Pos.Y * 7f + (fx.Pos2 - fx.Pos).Angle * 100f);

        /// FxKind.Slash in style E, the solid layer (weapon and sparks). False: not a player's melee swing (old look).
        bool DrawSlashE(Effect fx, float t)
        {
            return fxOn && DrawSlashLayer(fx, false);
        }

        /// FxKind.Jab in style E, the solid layer. False: draw the old look.
        bool DrawJabE(Effect fx, float t)
        {
            return fxOn && DrawJabLayer(fx, false);
        }

        /// The glow pass (Juice.DrawGlowPass, additive): every melee effect's energy.
        void DrawMeleeGlow()
        {
            foreach (var fx in game.Effects)
            {
                if (!meleeSeen.ContainsKey(fx))
                    continue;
                if (fx.Kind == FxKind.Slash)
                    DrawSlashLayer(fx, true);
                else if (fx.Kind == FxKind.Jab)
                    DrawJabLayer(fx, true);
                else if (fx.Kind == FxKind.StaffShove)
                    DrawStaffShoveLayer(fx, true);  // BoStaffLook.cs
            }
        }

        // --- Sweeps, thrusts and the Halberd ----------------------------------------------------------------------

        bool DrawSlashLayer(Effect fx, bool light)
        {
            V2 to = fx.Pos2 - fx.Pos;
            float reach = to.Length, aim = to.Angle, half = fx.Radius;
            if (reach < 1f)
                return false;
            bool thrust = half < 0f;
            if (half >= 0f && half < 0.2f)
                return false;  // a lunge or a dash streak, not a swing: the old look
            var seen = MeleeOwnerOf(fx, (thrust ? MeleeThrustTime : HalberdSweepTime) + MeleeTail, thrust, thrust ? MeleeThrustTime + 0.25f : (half >= MathX.Pi - 0.01f ? 0.4f : MeleeSweepTime + 0.28f));
            if (seen == null)
                return false;
            V2 o = MeleeOrigin(fx, seen);
            var col = MeleeColor(seen.Weapon, fx.Color);
            float T = fx.T;
            int seed = MeleeSeed(fx);

            if (thrust)
            {
                V2 dir = V2.FromAngle(aim);
                float ext = EaseOut3(Mathf.Clamp01(T / MeleeThrustTime)) * reach;
                float fade = T < MeleeThrustTime ? 1f : 1f - Mathf.Clamp01((T - MeleeThrustTime) / MeleeTail);
                V2 tip = o + dir * Mathf.Max(30f, ext);
                if (light && seen.Weapon == WeaponId.Katana)
                {
                    KatanaSpear(o, dir, aim, ext, T, fade, col);
                }
                else if (light)
                {
                    SoftLine(o + dir * 20f, tip, 26f * fade + 4f, col.WithAlpha(0.5f * fade));  // the beam and its glow
                    SoftLine(o + dir * 20f, tip, 10f * fade + 2f, col.WithAlpha(0.6f * fade));
                    Line(o + dir * 20f, tip, 2.5f, Rgba.White.WithAlpha(0.9f * fade));
                }
                else
                {
                    if (seen.Weapon == WeaponId.Katana)
                        KatanaSparks(o, aim, 0f, reach, T, MeleeThrustTime, seed, true, 14);
                    else
                        MeleeSparks(o, aim, 0f, reach, T, MeleeThrustTime, col, seed, true, 18);
                    DrawMeleeModel(seen.Weapon, o, aim, Mathf.Max(26f, ext), T < MeleeThrustTime + 0.1f ? 1f : 1f - Mathf.Clamp01((T - MeleeThrustTime - 0.1f) / 0.15f), col, T < MeleeThrustTime);
                }
                return true;
            }

            // A sweep across its arc (a full circle spins).
            // The Halberd's sweep takes its six steps' 0.21 s at a steady speed, in step with its hits; the others ease out
            // over 0.11 s (the hit has already landed).
            bool full = half >= MathX.Pi - 0.01f, halberd = seen.Weapon == WeaponId.Halberd;
            float sweepTime = halberd ? HalberdSweepTime : MeleeSweepTime;
            float s = seen.Sign, e = halberd ? Mathf.Clamp01(T / sweepTime) : EaseOut3(Mathf.Clamp01(T / sweepTime));
            float fadeOut = T < sweepTime ? 1f : 1f - Mathf.Clamp01((T - sweepTime) / MeleeTail);
            float start = full ? aim + T * 14f * s - 1.6f * s : aim - half * s;
            float at = full ? aim + T * 14f * s : start + 2f * half * s * e;
            bool katana = seen.Weapon == WeaponId.Katana;
            if (light && katana)
                KatanaCrescent(o, reach, start, at, T, sweepTime, col);
            else if (light)
                GlowArcBand(o, reach, start, at, col, 0.05f * fadeOut, MeleeBright * fadeOut);
            else
            {
                if (katana)
                    KatanaSparks(o, start, 2f * half * s, reach, T, sweepTime, seed, false, 20);
                else
                    MeleeSparks(o, start, 2f * half * s, reach, T, sweepTime, col, seed, false);
                float hold = T < sweepTime + 0.1f ? 1f : 1f - Mathf.Clamp01((T - sweepTime - 0.1f) / 0.18f);
                DrawMeleeModel(seen.Weapon, o, at, reach, hold, col, T < sweepTime);
            }
            return true;
        }

        // --- The Katana's Moon Steel look (Docs/Mockups/katana-reach.html) --------------------------------------------------

        static readonly Rgba KatanaSpark = new Rgba(1f, 0.69f, 0.44f);

        /// The swipe's trail: a crescent thick at the blade and thinning to nothing behind it, its tail eaten away as the
        /// sweep ends (no band left hanging), a white edge on the outside and a faint air-shock arc ahead of the blade.
        void KatanaCrescent(V2 o, float reach, float start, float at, float T, float sweepTime, Rgba col)
        {
            float sweep = at - start;
            if (Mathf.Abs(sweep) < 0.01f)
                return;
            float tailP = Mathf.Min(Mathf.Clamp01((T - 0.05f) / 0.38f) * Mathf.Clamp01((T - 0.05f) / 0.38f), 1f);
            float e = Mathf.Clamp01(T / sweepTime), tailE = Mathf.Min(tailP, EaseOut3(e));
            float tail = start + (at - start) * (T <= sweepTime ? 0f : tailE);
            float fade = T < sweepTime + 0.12f ? 1f : 1f - Mathf.Clamp01((T - sweepTime - 0.12f) / 0.5f);
            if (Mathf.Abs(at - tail) < 0.01f || fade <= 0f)
                return;
            float maxW = reach * 0.12f;
            int n = Mathf.Max(8, Mathf.CeilToInt(Mathf.Abs(at - tail) * 40f));
            for (int i = 0; i < n; i++)
            {
                float f0 = (float)i / n, f1 = (float)(i + 1) / n;
                float a0 = tail + (at - tail) * f0, a1 = tail + (at - tail) * f1;
                float w0 = maxW * Mathf.Pow(f0, 1.5f) + 0.6f, w1 = maxW * Mathf.Pow(f1, 1.5f) + 0.6f;
                float k0 = Mathf.Pow(f0, 1.25f) * fade, k1 = Mathf.Pow(f1, 1.25f) * fade;
                V2 d0 = V2.FromAngle(a0), d1 = V2.FromAngle(a1);
                // haze: clear on the inside, pale blue at the edge
                V2 h00 = o + d0 * (reach - w0 * 1.8f), h01 = o + d1 * (reach - w1 * 1.8f), h10 = o + d0 * (reach + 3f), h11 = o + d1 * (reach + 3f);
                VTri(h00, col.WithAlpha(0f), h10, col.WithAlpha(0.5f * k0), h11, col.WithAlpha(0.5f * k1));
                VTri(h00, col.WithAlpha(0f), h11, col.WithAlpha(0.5f * k1), h01, col.WithAlpha(0f));
                // the white edge
                V2 c00 = o + d0 * (reach - w0 * 0.35f - 1f), c01 = o + d1 * (reach - w1 * 0.35f - 1f), c10 = o + d0 * reach, c11 = o + d1 * reach;
                VTri(c00, Rgba.White.WithAlpha(0.2f * k0), c10, Rgba.White.WithAlpha(k0), c11, Rgba.White.WithAlpha(k1));
                VTri(c00, Rgba.White.WithAlpha(0.2f * k0), c11, Rgba.White.WithAlpha(k1), c01, Rgba.White.WithAlpha(0.2f * k1));
            }
            float s = sweep >= 0f ? 1f : -1f, shock = fade * (T < sweepTime + 0.1f ? 1f : 0.5f);
            for (int k = 0; k < 2; k++)
                Arc(o, reach + 9f + 8f * k, 1.3f, col.WithAlpha((0.35f - 0.15f * k) * shock), 14, at - s * (0.52f - 0.1f * k), s * 0.64f);
        }

        /// Hot orange and white streaks thrown off the blade (a short line each, not the other weapons' squares).
        void KatanaSparks(V2 o, float start, float sweep, float reach, float T, float active, int seed, bool line, int count)
        {
            uint r = (uint)seed;
            float s = sweep >= 0f ? 1f : -1f;
            for (int i = 0; i < count; i++)
            {
                float born = active * Rand01(ref r), spd = 150f * (0.4f + Rand01(ref r)), spread = (Rand01(ref r) - 0.5f) * 0.9f, age = T - born;
                if (age < 0f || age > 0.5f)
                    continue;
                float q = age / 0.5f, ba = line ? start : start + sweep * EaseOut3(born / Mathf.Max(active, 1e-4f));
                V2 from = line ? o + V2.FromAngle(start) * (30f + reach * born / Mathf.Max(active, 1e-4f)) : o + V2.FromAngle(ba) * reach;
                float oa = line ? start + spread * 1.4f : ba + s * (0.9f + spread);
                V2 dir = V2.FromAngle(oa), p = from + dir * (spd * EaseOut3(q * 0.9f));
                Line(p - dir * (14f * (1f - q)), p, 1.5f, (i % 2 == 0 ? KatanaSpark : Rgba.White).WithAlpha(0.9f * (1f - q)));
            }
        }

        /// The lunge: a hairline spear of light that tapers behind its tip, shock rings at the head and speed lines behind you.
        void KatanaSpear(V2 o, V2 dir, float aim, float ext, float T, float fade, Rgba col)
        {
            V2 tail = o - dir * 6f, tip = o + dir * Mathf.Max(30f, ext), side = dir.Orthogonal();
            SoftLine(tail, tip, 8f * fade + 2f, col.WithAlpha(0.5f * fade));
            Ribbon(tail, tip, 0.4f, 2.2f * fade + 0.4f, Rgba.White.WithAlpha(0f), Rgba.White.WithAlpha(fade));
            for (int r = 0; r < 3; r++)
            {
                float rq = Mathf.Clamp01((T - r * 0.04f) / 0.4f);
                if (rq <= 0f || rq >= 1f)
                    continue;
                Arc(tip + dir * (rq * 26f - 4f), 10f + r * 8f + rq * 14f, 1.4f, col.WithAlpha((1f - rq) * 0.7f), 12, aim - 1.05f, 2.1f);
            }
            uint seed = 77u;
            for (int i = 0; i < 7; i++)
            {
                float yo = (Rand01(ref seed) - 0.5f) * 40f, start = 30f * Rand01(ref seed), len = 40f + 60f * Rand01(ref seed), a = 0.4f * fade * (0.4f + Rand01(ref seed) * 0.6f);
                V2 b = o - dir * start + side * yo;
                Line(b - dir * len, b, 1f, col.WithAlpha(a));
            }
        }

        // --- The Lance and the Warhammer ----------------------------------------------------------------------------

        bool DrawJabLayer(Effect fx, bool light)
        {
            V2 to = fx.Pos2 - fx.Pos;
            float reach = to.Length, w = fx.Radius;
            if (reach < 1f)
                return false;
            var seen = MeleeOwnerOf(fx, HammerSwingTime + 0.5f, false, 0.5f);
            if (seen == null || seen.Weapon != WeaponId.Lance && seen.Weapon != WeaponId.Warhammer)
                return false;
            V2 o = MeleeOrigin(fx, seen), dir = to / reach, side = dir.Orthogonal();
            float T = fx.T, aim = to.Angle;
            int seed = MeleeSeed(fx);
            var col = MeleeColor(seen.Weapon, fx.Color);
            if (seen.Weapon == WeaponId.Warhammer)
            {
                DrawHammerSlamE(seen, o, aim, reach, w, fx.Color.A, T, seed, light);
                return true;
            }
            // The Lance: the box glows from your body to its sliding front edge, the lance riding out at its front.
            float g = EaseOut3(Mathf.Clamp01(T / 0.09f)), front = reach * g;
            float fade = T < 0.09f ? 1f : 1f - Mathf.Clamp01((T - 0.09f) / 0.45f);
            if (light)
            {
                V2 a0 = o + side * w, a1 = o - side * w, b0 = a0 + dir * front, b1 = a1 + dir * front;
                var clear = col.WithAlpha(0f);
                VTri(a0, clear, b0, col.WithAlpha(0.35f * fade), b1, col.WithAlpha(0.35f * fade));
                VTri(a0, clear, b1, col.WithAlpha(0.35f * fade), a1, clear);
                SoftLine(b0, b1, 22f * fade + 4f, col.WithAlpha(0.6f * fade));
                Line(b0, b1, 2.5f, Rgba.White.WithAlpha(0.9f * fade));
            }
            else
            {
                MeleeSparks(o, aim, 0f, reach, T, 0.09f, col, seed, true, 18);
                DrawMeleeModel(WeaponId.Lance, o, aim, Mathf.Max(30f, front), T < 0.2f ? 1f : 1f - Mathf.Clamp01((T - 0.2f) / 0.15f), col, T < 0.09f);
            }
            return true;
        }

        /// The Warhammer: from over your shoulder, a burning arc into the floor in front of you, then the impact (a
        /// glow, a ring with turning ticks, a pillar of light, embers). An aftershock (a later slam, further out) is just
        /// the impact.
        void DrawHammerSlamE(MeleeSeen seen, V2 o, float aim, float reach, float radius, float power01, float T, int seed, bool light)
        {
            var col = HammerGlow;
            bool aftershock = reach > Game.HammerCentre(seen.BaseReach) + 1f;  // `reach`: how far out the slam's circle is centred
            float d = reach, R = radius, power = Mathf.Clamp01((power01 - 0.2f) / 0.8f);
            V2 c = o + V2.FromAngle(aim) * d;
            float after = aftershock ? T : T - HammerSwingTime;
            if (!aftershock && T < HammerSwingTime)
            {
                float f = T / HammerSwingTime;
                if (light)
                {
                    // The head's burning path so far: one ribbon widening toward the head, with a white core.
                    const int n = 16;
                    float t0 = Mathf.Max(0f, f - 0.7f);
                    V2 prev = HammerHeadAt(o, aim, d, t0);
                    for (int i = 1; i <= n; i++)
                    {
                        float fi = t0 + (f - t0) * i / n, k0 = (float)(i - 1) / n, k1 = (float)i / n;
                        V2 p = HammerHeadAt(o, aim, d, fi);
                        Ribbon(prev, p, 4f + 18f * k0, 4f + 18f * k1, col.WithAlpha(0.7f * k0), col.WithAlpha(0.7f * k1));
                        Ribbon(prev, p, 1.2f, 1.2f, Rgba.White.WithAlpha(0.85f * k0 * k0), Rgba.White.WithAlpha(0.85f * k1 * k1));
                        prev = p;
                    }
                }
                else
                    HammerPose(o, aim, d, f, 1f, true);
                return;
            }
            const float life = 0.5f;
            float k2 = Mathf.Clamp01(after / life);
            if (light)
            {
                // The area it hit, always the same circle: a faint glow and outline, brighter the harder the slam.
                float pw = 0.45f + 0.55f * power, fade = 1f - k2;
                GlowDisc(c, R * 1.1f, col, 0.35f * pw * fade);
                GlowDisc(c, R * 0.6f, Rgba.White, 0.18f * pw * fade);
                Arc(c, R, 2f, col.WithAlpha(0.7f * pw * fade), 48);
                float wave = Mathf.Clamp01(after / 0.18f);  // a shockwave racing out to the edge
                Arc(c, R * wave, 3f, Rgba.White.WithAlpha(0.6f * pw * (1f - wave)), 48);
                if (after < 0.22f)
                {
                    float pk = (1f - after / 0.22f) * power;  // a pillar of light, only for a charged slam (the arena's up is -y)
                    SoftLineFade(c, c + new V2(0f, -130f), 24f * pk + 6f, Rgba.White.WithAlpha(0.7f * pk), col.WithAlpha(0f));
                }
            }
            else
            {
                uint r = (uint)seed;
                for (int i = 0; i < 16; i++)
                {
                    float a = Rand01(ref r) * MathX.Tau, v = 30f + 50f * Rand01(ref r), age = after - 0.05f * Rand01(ref r);
                    if (age < 0f)
                        continue;
                    V2 p = c + V2.FromAngle(a) * (R * 0.3f * Rand01(ref r) + v * age) + new V2(0f, -40f * age);
                    Square(p, 3f, (i % 2 == 0 ? col : new Rgba(1f, 0.89f, 0.69f)).WithAlpha(0.9f * (1f - k2)));
                }
                Player owner;
                bool charging = game.Players.TryGetValue(seen.Owner, out owner) && owner.DrawT > 0f;  // raised again for the next slam
                if (!aftershock && !charging)
                    HammerPose(o, aim, d, 1f, 1f - Mathf.Clamp01((after - 0.15f) / 0.2f), after < 0.15f);
            }
        }

        /// The Warhammer's blast (Game.HammerBoomColor), sent beside its slam: with Visual effects on, the slam above draws the
        /// impact, so the blast's old ring and its glow are left out (effects off: drawn as before).
        bool HammerBoomDrawnByMelee(Effect fx) => fxOn && fx.Kind == FxKind.Boom && fx.Color.R == Game.HammerBoomColor.R && fx.Color.G == Game.HammerBoomColor.G
            && fx.Color.B == Game.HammerBoomColor.B;

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

        /// The hammer itself at `f` through its arc: its handle from your body, its shadow on the floor, its head (18 x
        /// 32 with coloured bands, as in the mockup) drawn bigger the higher it is.
        void HammerPose(V2 o, float aim, float d, float f, float alpha, bool glow)
        {
            if (alpha <= 0f)
                return;
            float a, h, r;
            HammerArc(aim, d, f, out a, out h, out r);
            V2 ground = o + V2.FromAngle(a) * r, head = ground + new V2(0f, -26f * h);
            float sk = 1f - 0.5f * h;
            Circle(ground + new V2(0f, 4f), 13f * sk, new Rgba(0f, 0f, 0f, (0.22f + 0.25f * (1f - h)) * alpha), 16);
            Line(o + V2.FromAngle(a) * 12f, head, 4f, MeleeHandle.WithAlpha(alpha));
            float sc = 1f + 0.55f * h;
            V2 dir = V2.FromAngle(a), side = dir.Orthogonal();
            if (glow)
                GlowDisc(head, 26f * sc, HammerGlow, 0.5f * alpha);
            V2 hx = dir * 9f * sc, hy = side * 16f * sc;
            SetColor(HammerHead.WithAlpha(alpha));
            Quad(head - hx - hy, head + hx - hy, head + hx + hy, head - hx + hy);
            V2 bandY = side * 5f * sc;
            SetColor(HammerGlow.WithAlpha(0.95f * alpha));
            Quad(head - hx - hy, head + hx - hy, head + hx - hy + bandY, head - hx - hy + bandY);
            Quad(head - hx + hy - bandY, head + hx + hy - bandY, head + hx + hy, head - hx + hy);
        }

        // --- Pieces ------------------------------------------------------------------------------------------------

        /// The mockup's energy arc (its `band` with `lighter` and a blur, then the white core), for the glow pass: a band
        /// from 72% of the reach to the reach, its alpha going from aFrom at `from` to aTo at `to`, softened past both
        /// edges like the blur; then a 2 px white line at 92%.
        void GlowArcBand(V2 o, float reach, float from, float to, Rgba col, float aFrom, float aTo)
        {
            float span = Mathf.Abs(to - from);
            if (span < 0.005f || aFrom <= 0.003f && aTo <= 0.003f)
                return;
            float r0 = reach * 0.72f, r1 = reach;
            // radii across the band and how bright each is (the blur: fading in and out over 6 px at each edge)
            float[] rr = { r0 - 6f, r0 + 3f, r1 - 3f, r1 + 6f };
            float[] kk = { 0f, 1f, 1f, 0f };
            int n = Mathf.Max(4, Mathf.CeilToInt(span * 24f));
            for (int i = 0; i < n; i++)
            {
                float f0 = (float)i / n, f1 = (float)(i + 1) / n;
                float a0 = from + (to - from) * f0, a1 = from + (to - from) * f1;
                float al0 = aFrom + (aTo - aFrom) * f0, al1 = aFrom + (aTo - aFrom) * f1;
                V2 d0 = V2.FromAngle(a0), d1 = V2.FromAngle(a1);
                for (int j = 0; j < 3; j++)
                {
                    Rgba ci0 = col.WithAlpha(al0 * kk[j]), ci1 = col.WithAlpha(al1 * kk[j]), co0 = col.WithAlpha(al0 * kk[j + 1]), co1 = col.WithAlpha(al1 * kk[j + 1]);
                    V2 p00 = o + d0 * rr[j], p01 = o + d1 * rr[j], p10 = o + d0 * rr[j + 1], p11 = o + d1 * rr[j + 1];
                    VTri(p00, ci0, p10, co0, p11, co1);
                    VTri(p00, ci0, p11, co1, p01, ci1);
                }
            }
            // The white core, even along the sweep, as bright as the band's leading edge.
            float rc = reach * 0.92f, hw = 1f;
            var cw = Rgba.White.WithAlpha(0.95f * Mathf.Min(1f, Mathf.Max(aFrom, aTo) / MeleeBright));
            for (int i = 0; i < n; i++)
            {
                float a0 = from + (to - from) * i / n, a1 = from + (to - from) * (i + 1) / n;
                V2 d0 = V2.FromAngle(a0), d1 = V2.FromAngle(a1);
                VTri(o + d0 * (rc - hw), cw, o + d0 * (rc + hw), cw, o + d1 * (rc + hw), cw);
                VTri(o + d0 * (rc - hw), cw, o + d1 * (rc + hw), cw, o + d1 * (rc - hw), cw);
            }
        }

        /// A line that's brightest along its middle and fades to nothing at its sides.
        void SoftLine(V2 a, V2 b, float width, Rgba col)
        {
            SoftLineFade(a, b, width, col, col);
        }

        /// SoftLine, its colour going from ca at a to cb at b.
        void SoftLineFade(V2 a, V2 b, float width, Rgba ca, Rgba cb)
        {
            V2 n = (b - a).Normalized().Orthogonal() * (width / 2f);
            Rgba za = ca.WithAlpha(0f), zb = cb.WithAlpha(0f);
            VTri(a + n, za, b + n, zb, b, cb);
            VTri(a + n, za, b, cb, a, ca);
            VTri(a - n, za, a, ca, b, cb);
            VTri(a - n, za, b, cb, b - n, zb);
        }

        /// A segment of a ribbon: half-widths w0 at a and w1 at b, bright down its middle, clear at its edges.
        void Ribbon(V2 a, V2 b, float w0, float w1, Rgba ca, Rgba cb)
        {
            V2 n = (b - a).Normalized().Orthogonal();
            Rgba za = ca.WithAlpha(0f), zb = cb.WithAlpha(0f);
            VTri(a + n * w0, za, b + n * w1, zb, b, cb);
            VTri(a + n * w0, za, b, cb, a, ca);
            VTri(a - n * w0, za, a, ca, b, cb);
            VTri(a - n * w0, za, b, cb, b - n * w1, zb);
        }

        void Square(V2 c, float size, Rgba col)
        {
            float h = size / 2f;
            SetColor(col);
            Quad(c + new V2(-h, -h), c + new V2(h, -h), c + new V2(h, h), c + new V2(-h, h));
        }

        static float Rand01(ref uint s)
        {
            s = s * 1664525u + 1013904223u;
            return (s >> 8) / 16777216f;
        }

        /// Sparks thrown off the tip as it passes (a sweep from `start` over `sweep`), or sprayed along a thrust: 3 px
        /// squares, a third of them white, as in the mockup.
        void MeleeSparks(V2 o, float start, float sweep, float reach, float T, float active, Rgba col, int seed, bool line, int count = 22)
        {
            uint r = (uint)seed;
            float s = sweep >= 0f ? 1f : -1f;
            for (int i = 0; i < count; i++)
            {
                float born = active * Rand01(ref r), spd = 120f + 160f * Rand01(ref r), spread = (Rand01(ref r) - 0.5f) * 0.9f;
                float age = T - born;
                if (age < 0f || age > 0.5f)
                    continue;
                V2 from;
                float oa;
                if (line)
                {
                    from = o + V2.FromAngle(start) * (30f + reach * born / Mathf.Max(active, 1e-4f));
                    oa = start + spread * 1.4f;
                }
                else
                {
                    float ba = start + sweep * EaseOut3(born / Mathf.Max(active, 1e-4f));
                    from = o + V2.FromAngle(ba) * reach;
                    oa = ba + s * 0.9f + spread;
                }
                float dd = spd * age * (1f - age);
                Square(from + V2.FromAngle(oa) * dd, 3f, (i % 3 == 0 ? Rgba.White : col).WithAlpha(0.9f * (1f - age / 0.5f)));
            }
        }

        /// The weapon itself, from your body out along `ang` to `reach` (the mockup's shapes and sizes: a handle, a gold
        /// guard and a tapered blade with a coloured edge; a pole for the Halberd; a thin blade for the Rapier).
        /// `bright`: mid-swing, the blade is white.
        void DrawMeleeModel(WeaponId id, V2 o, float ang, float reach, float alpha, Rgba col, bool bright)
        {
            if (alpha <= 0f)
                return;
            V2 dir = V2.FromAngle(ang), side = dir.Orthogonal();
            var edge = col.WithAlpha(0.9f * alpha);
            switch (id)
            {
                case WeaponId.Rapier:
                    Line(o + dir * 18f, o + dir * reach, 2.5f, new Rgba(0.93f, 0.95f, 1f, alpha));
                    Arc(o + dir * 20f, 6f, 3f, MeleeGuard.WithAlpha(alpha), 10, ang - 1.6f, 3.2f);
                    break;
                case WeaponId.Lance:
                {
                    float tip = reach * 0.98f, r0 = 18f;
                    SetColor((bright ? Rgba.White : MeleeSteel).WithAlpha(alpha));
                    V2 b = o + dir * r0;
                    Quad(b + side * 6f, b - side * 6f, o + dir * (tip - 20f) - side * 2.5f, o + dir * (tip - 20f) + side * 2.5f);
                    Tri(o + dir * tip, o + dir * (tip - 22f) + side * 4f, o + dir * (tip - 22f) - side * 4f);
                    Line(b + side * 7f, b - side * 7f, 3f, MeleeGuard.WithAlpha(alpha));
                    Line(o + dir * (r0 + 4f), o + dir * (tip - 6f), 1.2f, edge);
                    break;
                }
                case WeaponId.HolyMace:  // the Quarterstaff
                case WeaponId.BoStaff:
                    Line(o - dir * reach * 0.25f, o + dir * reach * 0.97f, 4f, MeleeHandle.WithAlpha(alpha));
                    Line(o + dir * (reach * 0.97f - 10f), o + dir * reach * 0.97f, 5f, MeleeGuard.WithAlpha(alpha));
                    Line(o - dir * reach * 0.25f, o - dir * (reach * 0.25f - 8f), 5f, MeleeGuard.WithAlpha(alpha));
                    break;
                case WeaponId.Censer:
                    Line(o + dir * 12f, o + dir * (reach * 0.97f - 12f), 1.5f, MeleeGuard.WithAlpha(alpha));
                    Circle(o + dir * (reach * 0.97f - 6f), 7f, MeleeGuard.WithAlpha(alpha), 12);
                    Arc(o + dir * (reach * 0.97f - 6f), 7f, 1.5f, edge, 12);
                    break;
                case WeaponId.Kusarigama:
                {
                    float tip = reach * 0.97f;
                    Line(o + dir * 12f, o + dir * (tip - 8f), 1.5f, new Rgba(0.7f, 0.72f, 0.78f, alpha));  // the chain
                    Arc(o + dir * tip - side * 8f, 10f, 3f, MeleeSteel.WithAlpha(alpha), 10, ang - 0.2f, 1.9f);  // the sickle
                    break;
                }
                case WeaponId.BloodLash:
                {
                    float tip = reach * 0.97f;
                    V2 prev = o + dir * 12f;  // a whip: a wavy line, thinning out
                    for (int i = 1; i <= 10; i++)
                    {
                        float f = i / 10f;
                        V2 p = o + dir * (12f + (tip - 12f) * f) + side * (Mathf.Sin(f * 6f) * 5f * f);
                        Line(prev, p, 3.5f - 2.5f * f, new Rgba(0.75f, 0.12f, 0.16f, alpha));
                        prev = p;
                    }
                    break;
                }
                default:
                {
                    // The mockup's blade(): handle (4 px) from just inside your body to the hilt, a guard 14 px across,
                    // then the blade, wd wide at the hilt, tapering to its tip at 98% of the reach. The Halberd is a pole
                    // with its blade on the last 30%.
                    bool pole = id == WeaponId.Halberd;
                    float bladeShare = pole ? 0.3f : id == WeaponId.TwinBlades || id == WeaponId.Katana ? 0.7f : 0.78f;
                    float r0 = 18f, r1 = reach * 0.98f, hilt = pole ? reach * (1f - bladeShare) : r0 + (r1 - r0) * (1f - bladeShare);
                    float wd = pole ? 7f : 5f;
                    Line(o + dir * (r0 - 6f), o + dir * hilt, 4f, MeleeHandle.WithAlpha(alpha));
                    Line(o + dir * hilt + side * 7f, o + dir * hilt - side * 7f, 3f, MeleeGuard.WithAlpha(alpha));
                    var fill = bright ? Rgba.White : id == WeaponId.Lightbringer ? new Rgba(1f, 0.95f, 0.75f) : new Rgba(0.9f, 0.93f, 0.99f);
                    V2 h0 = o + dir * hilt, b1 = o + dir * (r1 - 10f), tip = o + dir * r1;
                    V2 p0 = h0 + side * wd, p1 = b1 + side * wd * 0.8f, p3 = b1 - side * wd * 0.6f, p4 = h0 - side * wd * 0.6f;
                    SetColor(fill.WithAlpha(alpha));
                    Quad(p0, p1, p3, p4);
                    Tri(p1, tip, p3);
                    Line(p0, p1, 1.2f, edge);  // its coloured edge
                    Line(p1, tip, 1.2f, edge);
                    Line(tip, p3, 1.2f, edge);
                    Line(p3, p4, 1.2f, edge);
                    break;
                }
            }
        }
    }
}
