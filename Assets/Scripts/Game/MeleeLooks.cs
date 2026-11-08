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
            public int Fired;             // the Halberd: which sweeps' first cut has already kicked your screen (bit 1, bit 2)
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
            bool alternates = best.Weapon.Id == WeaponId.TwinBlades || best.Weapon.Id == WeaponId.Katana || best.Weapon.Id == WeaponId.HolyMace;
            if (alternates && !(thrust && (best.Weapon.Id == WeaponId.Katana || best.Weapon.Id == WeaponId.HolyMace)))  // (a Katana lunge or a Quarterstaff jab is a thrust between swipes: it doesn't turn the next swipe round)  // (the Katana's lunge is a thrust between its swipes: it doesn't turn the next swipe round)
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
            float sign = alternates ? flip : 1f;
            if (best.Weapon.Id == WeaponId.Greatsword)
                sign = best.GreatStep == 2 ? -1f : 1f;  // the return cut sweeps back; the cut and the rend sweep forward (GreatswordLook.cs)
            seen = new MeleeSeen { Owner = best.Id, Weapon = best.Weapon.Id, Sign = sign, BaseReach = Game.MeleeReach(best, best.Weapon.Reach) };
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
                case WeaponId.Greatsword: return new Rgba(0.78f, 0.88f, 1f);  // Moonlit Crescent (Docs/Mockups/greatsword-sweep.html)
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
            if (!thrust && IsHalberdFx(fx))
                return DrawHalberdFx(fx, light);  // HalberdLook.cs: the whole swing from the click
            var seen = MeleeOwnerOf(fx, (thrust ? MeleeThrustTime : HalberdSweepTime) + MeleeTail, thrust, thrust ? MeleeThrustTime + 0.25f : (half >= MathX.Pi - 0.01f ? 0.4f : MeleeSweepTime + 0.28f));
            if (seen == null)
                return false;
            if (seen.Weapon == WeaponId.BoStaff)
                return DrawBoStaffFx(fx, light, seen, thrust ? 1 : half >= MathX.Pi - 0.01f ? 2 : 0, aim, reach);  // BoStaffLook.cs: held in two hands, with the Sage ribbon
            V2 o = MeleeOrigin(fx, seen);
            var col = MeleeColor(seen.Weapon, fx.Color);
            float T = fx.T;
            int seed = MeleeSeed(fx);
            ownerOfModel = seen.Owner;

            if (thrust)
            {
                V2 dir = V2.FromAngle(aim);
                float ext = EaseOut3(Mathf.Clamp01(T / MeleeThrustTime)) * reach;
                float fade = T < MeleeThrustTime ? 1f : 1f - Mathf.Clamp01((T - MeleeThrustTime) / MeleeTail);
                V2 tip = o + dir * Mathf.Max(30f, ext);
                if (light && seen.Weapon == WeaponId.Katana)
                {
                {
                    KatanaSpear(o, dir, aim, ext, T, fade, col);
                    Player owner;
                    KatanaGhosts(fx.Pos, dir, T, game.Players.TryGetValue(seen.Owner, out owner) ? owner.Def.BodyR : 16f, col);
                    KatanaMarks(o, aim, 0f, 1f, reach, T, MeleeThrustTime, true, col);
                }
                }
                else if (light && seen.Weapon == WeaponId.HolyMace)
                {
                    StaffJabCone(o, aim, -half, reach, T, MeleeThrustTime, fade, col);  // QuarterstaffLook.cs
                }
                else if (light && seen.Weapon == WeaponId.Rapier)
                {
                    RapierThrustLook(seen, o, dir, aim, ext, reach, T, fade, col);  // Needle
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
                    DrawMeleeModel(seen.Weapon, o, aim, Mathf.Max(26f, ext), T < MeleeThrustTime + 0.1f ? 1f : 1f - Mathf.Clamp01((T - MeleeThrustTime - 0.1f) / 0.15f), col, T < MeleeThrustTime, Mathf.Sin(Mathf.Clamp01(T / (MeleeThrustTime * 1.8f)) * MathX.Pi), ArmColor(seen.Owner));
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
            bool moon = seen.Weapon == WeaponId.Greatsword && !full;  // Moonlit Crescent
            if (light && katana)
            {
                KatanaCrescent(o, reach, start, at, T, sweepTime, col);
                KatanaMarks(o, aim, half, s, reach, T, sweepTime, false, col);
            }
            else if (light && moon)
            {
                KatanaCrescent(o, reach, start, at, T, sweepTime, col, half > 1.5f ? 0.4f : 0.3f, false);  // one long crescent, thin at the tail and fat at the blade (fatter for the overhead rend)
                KatanaMarks(o, aim, half, s, reach, T, sweepTime, false, col);
            }
            else if (light)
                GlowArcBand(o, reach, start, at, col, 0.05f * fadeOut, MeleeBright * fadeOut);
            else
            {
                if (katana)
                    KatanaSparks(o, start, 2f * half * s, reach, T, sweepTime, seed, false, 20);
                else if (moon)
                    KatanaSparks(o, start, 2f * half * s, reach, T, sweepTime, seed, false, 20, col);
                else
                    MeleeSparks(o, start, 2f * half * s, reach, T, sweepTime, col, seed, false);
                float hold = T < sweepTime + 0.1f ? 1f : 1f - Mathf.Clamp01((T - sweepTime - 0.1f) / 0.18f);
                if (katana && T < sweepTime + 0.25f)  // three lagging ghosts of the blade
                    for (int g = 1; g <= 3; g++)
                    {
                        float ga = at - s * g * 0.16f * (T < sweepTime ? 1f : 0.6f);
                        if ((ga - start) * s < 0f)
                            continue;
                        DrawMeleeModel(seen.Weapon, o, ga, reach, hold * (0.34f - g * 0.09f), col, false, s, ArmColor(seen.Owner));
                    }
                DrawMeleeModel(seen.Weapon, o, at, reach, hold, col, T < sweepTime, s, ArmColor(seen.Owner));
            }
            return true;
        }

        // --- The Katana's Moon Steel look (Docs/Mockups/katana-reach.html) --------------------------------------------------

        static readonly Rgba KatanaSpark = new Rgba(1f, 0.69f, 0.44f);

        /// The swipe's trail: a crescent thick at the blade and thinning to nothing behind it, its tail eaten away as the
        /// sweep ends (no band left hanging), a white edge on the outside and a faint air-shock arc ahead of the blade.
        void KatanaCrescent(V2 o, float reach, float start, float at, float T, float sweepTime, Rgba col, float width = 0.12f, bool shock = true)
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
            float maxW = reach * width;
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
            float s = sweep >= 0f ? 1f : -1f, shockA = fade * (T < sweepTime + 0.1f ? 1f : 0.5f);
            for (int k = 0; k < (shock ? 2 : 0); k++)
                Arc(o, reach + 9f + 8f * k, 1.3f, col.WithAlpha((0.35f - 0.15f * k) * shockA), 14, at - s * (0.52f - 0.1f * k), s * 0.64f);
        }

        /// Five afterimages of the swordsman along the lunge's step, from where they stood, fading as the step ends.
        void KatanaGhosts(V2 from, V2 dir, float T, float bodyR, Rgba col)
        {
            const float step = 0.12f, tail = 0.35f;
            if (T < 0f || T > step + tail)
                return;
            float fade = 1f - Mathf.Clamp01((T - step) / tail), cur = EaseOut3(Mathf.Clamp01(T / step));
            for (int i = 1; i <= 5; i++)
            {
                float u = cur - i * 0.14f, a = fade * (0.42f - i * 0.07f);
                if (u < 0f || a <= 0f)
                    continue;
                V2 c = from + dir * (Game.KatanaLungeStep * EaseOut3(u));
                GlowDisc(c, bodyR, col, a * 0.6f);
                Arc(c, bodyR, 1.4f, Rgba.White.WithAlpha(a * 0.7f), 18);
            }
        }

        // --- The Rapier's Needle look (Docs/Mockups/rapier-streak.html) ---------------------------------------------------

        static readonly Rgba NeedleGold = new Rgba(1f, 0.84f, 0.51f);

        /// A thrust: a thin streak from the hand to the point (wider with the streak), a star glint and a ring where it
        /// landed, and at six stacks a gold glow on the point.
        void RapierThrustLook(MeleeSeen seen, V2 o, V2 dir, float aim, float ext, float reach, float T, float fade, Rgba col)
        {
            Player owner;
            int streak = game.Players.TryGetValue(seen.Owner, out owner) ? owner.RapierStreak : 0;
            V2 tipP = o + dir * Mathf.Max(30f, ext);
            Ribbon(o + dir * 14f, tipP, 0.4f, 1.6f + 0.2f * streak, col.WithAlpha(0f), Rgba.White.WithAlpha(fade));
            if (streak >= 6)
                GlowDisc(tipP, 16f, NeedleGold, 0.9f * fade);
            // Where it landed: the nearest enemy in its narrow cone.
            Enemy near = null;
            float best = 1e9f;
            foreach (var e in game.Enemies.Values)
            {
                V2 rel = e.Disp - o;
                float d = rel.Length, diff = Mathf.Abs(Mathf.Repeat(rel.Angle - aim + MathX.Pi, MathX.Tau) - MathX.Pi);
                if (d - e.Radius * 0.5f > reach || diff > 0.34f || d >= best)
                    continue;
                best = d;
                near = e;
            }
            if (near == null)
                return;
            float q = (T - MeleeThrustTime * Mathf.Clamp01(best / reach)) / 0.3f;
            if (q < 0f || q > 1f)
                return;
            V2 p = near.Disp;
            float sz = 6f + 14f * EaseOut3(q), a = 1f - q;
            Line(p - new V2(sz, 0f), p + new V2(sz, 0f), 1.5f, Rgba.White.WithAlpha(a));
            Line(p - new V2(0f, sz), p + new V2(0f, sz), 1.5f, Rgba.White.WithAlpha(a));
            Arc(p, 6f + 10f * EaseOut3(q), 1.5f, col.WithAlpha(0.8f * a), 14);
        }

        /// Six small diamonds on an arc behind you: they fill one by one as the streak builds, and turn gold at six with a faint
        /// halo until it breaks. Shown for a Rapier with a streak going.
        void DrawRapierPips(Player p)
        {
            if (!fxOn || p.Downed || p.RapierStreak <= 0)
                return;
            int n = Mathf.Min(6, p.RapierStreak);
            bool gold = n >= 6;
            var col = MeleeColor(WeaponId.Rapier, Rgba.White);
            for (int d = 0; d < 6; d++)
            {
                float da = p.Aim + MathX.Pi + (d - 2.5f) * 0.34f;
                V2 c = p.Disp + V2.FromAngle(da) * 30f;
                Polygon(c, 4.3f, 4, 0f, d < n ? (gold ? NeedleGold : col) : new Rgba(1f, 1f, 1f, 0.14f));
            }
            if (gold)
                Arc(p.Disp, 26f, 3f, NeedleGold.WithAlpha(0.25f + 0.1f * Mathf.Sin(Time.unscaledTime * 9f)), 32);
        }


        /// A bright cut across each enemy the swing or lunge passes through, timed to the blade reaching it. Worked out from
        /// the swing's own numbers and where the enemies are drawn (drawing only: the host's hits are its own).
        void KatanaMarks(V2 o, float aim, float half, float s, float reach, float T, float sweepTime, bool thrust, Rgba col)
        {
            foreach (var e in game.Enemies.Values)
            {
                V2 rel = e.Disp - o;
                float d = rel.Length, hitT, markAng, len;
                if (thrust)
                {
                    V2 dir = V2.FromAngle(aim);
                    float along = rel.X * dir.X + rel.Y * dir.Y, across = Mathf.Abs(-rel.X * dir.Y + rel.Y * dir.X);
                    if (along < 0f || along > reach + e.Radius || across > 26f + e.Radius * 0.6f)
                        continue;
                    hitT = sweepTime * Mathf.Clamp01(along / reach);
                    markAng = aim + MathX.Pi * 0.5f + 0.2f;
                    len = 46f;
                }
                else
                {
                    if (d - e.Radius * 0.5f > reach)
                        continue;
                    float diff = Mathf.Repeat(rel.Angle - aim + MathX.Pi, MathX.Tau) - MathX.Pi;
                    if (Mathf.Abs(diff) > half + 0.12f)
                        continue;
                    float p = Mathf.Clamp01((diff + half * s) / (2f * half * s));
                    hitT = sweepTime * (1f - Mathf.Pow(1f - p, 1f / 3f));
                    markAng = rel.Angle + s * MathX.Pi * 0.5f - 0.6f * s;
                    len = 34f;
                }
                float q = (T - hitT) / 0.32f;
                if (q < 0f || q > 1f)
                    continue;
                float a = 1f - q, l = Mathf.Lerp(10f, len, EaseOut3(Mathf.Clamp01(q * 2.2f)));
                V2 u = V2.FromAngle(markAng), c = e.Disp;
                SoftLine(c - u * (l * 0.7f), c + u * (l * 0.7f), 7f * a, col.WithAlpha(0.5f * a));
                Ribbon(c - u * l, c, 0.3f, 3.4f * a + 0.4f, Rgba.White.WithAlpha(0f), Rgba.White.WithAlpha(a));
                Ribbon(c + u * l, c, 0.3f, 3.4f * a + 0.4f, Rgba.White.WithAlpha(0f), Rgba.White.WithAlpha(a));
            }
        }

        /// Hot orange and white streaks thrown off the blade (a short line each, not the other weapons' squares).
        void KatanaSparks(V2 o, float start, float sweep, float reach, float T, float active, int seed, bool line, int count, Rgba warm = default(Rgba))
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
                Line(p - dir * (14f * (1f - q)), p, 1.5f, (i % 2 == 0 ? (warm.A > 0f ? warm : KatanaSpark) : Rgba.White).WithAlpha(0.9f * (1f - q)));
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
            var seen = MeleeOwnerOf(fx, HammerSwingTime + (fx.Color.R == Game.HammerBoomColor.R && fx.Color.G == Game.HammerBoomColor.G ? 2.3f : 0.5f), false, 0.5f);  // (a hammer slam's cracks cool over about 2 s)
            if (seen == null || seen.Weapon != WeaponId.Lance && seen.Weapon != WeaponId.Warhammer)
                return false;
            V2 o = MeleeOrigin(fx, seen), dir = to / reach, side = dir.Orthogonal();
            float T = fx.T, aim = to.Angle;
            int seed = MeleeSeed(fx);
            var col = MeleeColor(seen.Weapon, fx.Color);
            if (seen.Weapon == WeaponId.Warhammer)
            {
                DrawHammerSlamE(seen, o, aim, reach, w, fx.Color.A, T, seed, light, fx.Pos2);
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
        void DrawHammerSlamE(MeleeSeen seen, V2 o, float aim, float reach, float radius, float power01, float T, int seed, bool light, V2 centre)
        {
            var col = HammerDust;
            bool aftershock = reach > Game.HammerCentre(seen.BaseReach) + 1f;  // `reach`: how far out the slam's circle is centred
            float d = reach, R = radius, power = Mathf.Clamp01((power01 - 0.2f) / 0.8f);
            V2 c = centre;  // where it hit: the circle stays put when the player moves
            float after = aftershock ? T : T - HammerSwingTime;
            if (!aftershock && T < HammerSwingTime)
            {
                float f = T / HammerSwingTime;
                if (light)
                {
                    // The head's path so far: a wide pale ribbon with a white core, widening toward the head.
                    const int n = 16;
                    float t0 = Mathf.Max(0f, f - 0.7f);
                    V2 prev = HammerHeadAt(o, aim, d, t0);
                    for (int i = 1; i <= n; i++)
                    {
                        float fi = t0 + (f - t0) * i / n, k0 = (float)(i - 1) / n, k1 = (float)i / n;
                        V2 p = HammerHeadAt(o, aim, d, fi);
                        Ribbon(prev, p, 3f + 10f * k0, 3f + 10f * k1, col.WithAlpha(0.7f * k0), col.WithAlpha(0.7f * k1));
                        Ribbon(prev, p, 1.2f, 1.2f, Rgba.White.WithAlpha(0.85f * k0 * k0), Rgba.White.WithAlpha(0.85f * k1 * k1));
                        prev = p;
                    }
                }
                else
                {
                    Rgba arm = ArmColor(seen.Owner);
                    for (int gh = 2; gh >= 1; gh--)  // two lagging ghosts of the head
                        HammerPose(o, aim, d, Mathf.Max(0f, f - gh * 0.2f), 0.3f / gh, false, 0f, false, arm);
                    HammerPose(o, aim, d, f, 1f, false, 0f, true, arm);
                }
                return;
            }
            float pw = 0.45f + 0.55f * power;
            if (light)
            {
                // Quake: a brief flash, and a ring of turning ticks marking the circle. Everything else is solid (below).
                float fl = 1f - Mathf.Clamp01(after / 0.1f);
                if (fl > 0f)
                    GlowDisc(c, R * 0.7f, HammerFlash, 0.5f * fl * pw);
                float tk = 1f - Mathf.Clamp01(after / 0.8f);
                if (tk > 0f)
                    for (int i = 0; i < 28; i++)
                    {
                        float ta = (float)i / 28f * MathX.Tau + after * 1.5f;
                        V2 dn = V2.FromAngle(ta);
                        Line(c + dn * (R - 4f), c + dn * (R + 4f), 2f, HammerFlash.WithAlpha(0.5f * tk));
                    }
                HammerStunStars(c, R, power, after);
                return;
            }
            Player owner;
            bool charging = game.Players.TryGetValue(seen.Owner, out owner) && owner.DrawT > 0f;  // raised again for the next slam
            if (!aftershock && !charging)
            {
                // It stays in the floor a moment, is drawn back up, then settles into the held pose (DrawHammerIdle takes over there).
                float f;
                if (after < 0.15f)
                    f = 1f;
                else if (after < 0.55f)
                    f = Mathf.Lerp(1f, 0f, EaseOut3((after - 0.15f) / 0.4f));
                else if (after < HammerReturn)
                    f = Mathf.Lerp(-1f, -0.3f, EaseOut3((after - 0.55f) / (HammerReturn - 0.55f)));
                else
                    return;
                HammerPose(o, aim, d, f, 1f, false, 0f, true, ArmColor(seen.Owner));
            }
        }

        const float HammerReturn = 0.85f;  // after the slam: when the hammer is back in the held pose
        static readonly Rgba HammerDust = new Rgba(0.9f, 0.84f, 0.75f), HammerFlash = new Rgba(1f, 0.93f, 0.78f);

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
            HammerPose(p.Disp, p.Aim, d, -0.3f - 0.7f * charge, 1f, false, 0f, true, p.Def.Color);
        }

        /// The hammer held at the ready (no charge, no slam playing): the same pose a charge starts from and a slam settles back
        /// into, so it moves smoothly in and out of the animation, as the Halberd does (HalberdLook.cs).
        void DrawHammerIdle(Player p, float t)
        {
            if (!fxOn || p.Weapon.Id != WeaponId.Warhammer || p.Downed || HammerBusy(p))
                return;
            float d = 16f + Game.MeleeReach(p, p.Weapon.Reach) * 0.65f;
            HammerPose(p.Disp, p.Aim, d, -0.3f + 0.03f * Mathf.Sin(t * 2.6f), 1f, false, 0f, true, p.Def.Color);
        }

        /// True while the hammer is being drawn by something else: charging, or the swing and its return after a slam.
        bool HammerBusy(Player p)
        {
            if (p.DrawT > 0f)
                return true;
            foreach (var fx in game.Effects)
                if (fx.Kind == FxKind.Jab && fx.Color.R == Game.HammerBoomColor.R && fx.Color.G == Game.HammerBoomColor.G && fx.Color.B == Game.HammerBoomColor.B
                    && fx.T < HammerSwingTime + HammerReturn && (fx.Pos.DistanceTo(p.Pos) < 44f || fx.Pos.DistanceTo(p.Disp) < 44f))
                    return true;
            return false;
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
        void HammerPose(V2 o, float aim, float d, float f, float alpha, bool glow, float heat = 0f, bool hands = true, Rgba arm = default(Rgba))
        {
            if (alpha <= 0f)
                return;
            float a, h, r;
            HammerArc(aim, d, f, out a, out h, out r);
            V2 dir = V2.FromAngle(a), side = dir.Orthogonal();
            V2 ground = o + dir * r, head = ground + new V2(0f, -26f * h);
            float sk = 1f - 0.5f * h;
            Circle(ground + new V2(0f, 4f), 13f * sk, new Rgba(0f, 0f, 0f, (0.22f + 0.25f * (1f - h)) * alpha), 16);
            Line(o + dir * 5f, head, 5f, new Rgba(0.23f, 0.17f, 0.12f, alpha));
            Line(o + dir * 5f, head, 3f, MeleeHandle.WithAlpha(alpha));
            if (hands)
            {
                // Two gloved hands on the handle, each joined to the body by an arm: it is held, not floating.
                float sg = Mathf.Cos(a) < 0f ? -1f : 1f;
                Rgba armc = arm.A > 0f ? arm : new Rgba(0.29f, 0.66f, 0.61f);
                V2 h1 = o + dir * 17f, h2 = o + dir * 34f, s1 = o - side * (3f * sg), s2 = o + side * (3f * sg);
                Line(s1, h1, 7.2f, new Rgba(0.05f, 0.1f, 0.1f, alpha));
                Line(s2, h2, 7.2f, new Rgba(0.05f, 0.1f, 0.1f, alpha));
                Line(s1, h1, 5f, armc.WithAlpha(alpha));
                Line(s2, h2, 5f, armc.WithAlpha(alpha));
                var leather = new Rgba(0.72f, 0.56f, 0.34f, alpha);
                Circle(h1, 6f, new Rgba(0.16f, 0.11f, 0.06f, alpha), 14);
                Circle(h1, 4.8f, leather, 14);
                Circle(h2, 5.2f, new Rgba(0.16f, 0.11f, 0.06f, alpha), 14);
                Circle(h2, 4f, leather, 14);
            }
            float sc = 1f + 0.55f * h;
            V2 hx = dir * 9f * sc, hy = side * 16f * sc;
            SetColor(new Rgba(0.42f, 0.4f, 0.37f, alpha));  // dull stone-grey iron, nothing glowing (Quake)
            Quad(head - hx - hy, head + hx - hy, head + hx + hy, head - hx + hy);
            SetColor(new Rgba(0.64f, 0.61f, 0.57f, alpha));
            Quad(head - hx - hy, head + hx - hy, head + hx - hy * 0.4f, head - hx - hy * 0.4f);  // the lit side
            Line(head - hx * 0.8f - hy, head - hx * 0.8f + hy, 1f, new Rgba(1f, 1f, 1f, 0.35f * alpha));
            V2 bandY = side * 5f * sc;
            SetColor(new Rgba(0.72f, 0.6f, 0.29f, alpha));  // brass bands on both striking faces
            Quad(head - hx - hy, head + hx - hy, head + hx - hy + bandY, head - hx - hy + bandY);
            Quad(head - hx + hy - bandY, head + hx + hy - bandY, head + hx + hy, head - hx + hy);
        }

                /// The owner's body colour, for the arms that hold the hammer.
        Rgba ArmColor(int owner)
        {
            Player p;
            return game.Players.TryGetValue(owner, out p) ? p.Def.Color : default(Rgba);
        }

        /// Stun stars over each enemy the slam caught, for as long as it is stunned (not bosses). The host's stun is not sent to
        /// the screens, so this works it out as the host does: the circle's enemies, for HammerStunMin..Max by the charge.
        void HammerStunStars(V2 c, float R, float power, float after)
        {
            float stun = 0.25f + 0.65f * Mathf.Clamp01((power - 0.25f) / 0.75f);
            if (after < 0f || after > stun + 0.05f)
                return;
            float a = Mathf.Clamp01((stun - after) / 0.15f) * 0.9f, t = Time.unscaledTime;
            if (a <= 0f)
                return;
            foreach (var e in game.Enemies.Values)
            {
                if (e.Def.IsBoss || e.Disp.DistanceTo(c) > R + e.Radius * 0.5f)
                    continue;
                for (int i = 0; i < 3; i++)
                {
                    float an = t * 7f + i * MathX.Tau / 3f;
                    V2 sp = e.Disp + new V2(Mathf.Cos(an) * 13f, -e.Radius - 6f + Mathf.Sin(an) * 4f);
                    V2 u = V2.FromAngle(an) * 3.4f, v = u.Orthogonal();
                    Line(sp - u, sp + u, 1.4f, new Rgba(1f, 0.88f, 0.47f, a));
                    Line(sp - v, sp + v, 1.4f, new Rgba(1f, 0.88f, 0.47f, a));
                }
            }
        }

        /// Quake: the ground taking the blow. A dark crater with a lighter rim and cracks that stay for about two seconds, dust
        /// puffs rolling out round the circle, and stone chips that land with a small shadow, all drawn on the floor before the enemies so they walk over
        /// them (called from DrawArenaContents). Same geometry as DrawHammerSlamE.
        void DrawHammerFloor()
        {
            if (!fxOn)
                return;
            foreach (var fx in game.Effects)
            {
                if (fx.Kind != FxKind.Jab || fx.Color.R != Game.HammerBoomColor.R || fx.Color.G != Game.HammerBoomColor.G
                    || fx.Color.B != Game.HammerBoomColor.B)
                    continue;
                V2 to = fx.Pos2 - fx.Pos;
                float reach = to.Length;
                if (reach < 1f)
                    continue;
                var seen = MeleeOwnerOf(fx, HammerSwingTime + 2.3f, false, 0.5f);
                if (seen == null || seen.Weapon != WeaponId.Warhammer)
                    continue;
                bool aftershock = reach > Game.HammerCentre(seen.BaseReach) + 1f;
                float after = aftershock ? fx.T : fx.T - HammerSwingTime;
                if (after < 0f)
                    continue;
                float aim = to.Angle, power = Mathf.Clamp01((fx.Color.A - 0.2f) / 0.8f), pw = 0.45f + 0.55f * power;
                V2 c = fx.Pos2;  // where it hit, not where the player now stands
                HammerCrater(c, fx.Radius, pw, after, MeleeSeed(fx));
                HammerDustPuffs(fx.Radius, c, pw, after, MeleeSeed(fx));
                DrawHammerChips(pw, c, after, MeleeSeed(fx));
            }
        }

        /// The crater with a lighter rim and the cracks that stay for about two seconds.
        void HammerCrater(V2 c, float R, float pw, float after, int seed)
        {
            float stay = 1f - Mathf.Clamp01((after - 0.4f) / 1.8f);
            if (stay > 0f)
            {
                Circle(c, R * 0.88f, new Rgba(0.03f, 0.027f, 0.023f, 0.34f * stay * pw), 36);
                Arc(c, R * 0.88f, 2f, new Rgba(0.75f, 0.69f, 0.59f, 0.28f * stay), 36);
                float vis = EaseOut3(Mathf.Clamp01(after / 0.12f));
                int n = 7 + Mathf.RoundToInt(4f * pw);
                uint r = (uint)seed * 2654435761u + 7u;
                for (int i = 0; i < n; i++)
                {
                    float a = (float)i / n * MathX.Tau + (Rand01(ref r) - 0.5f) * 0.5f, len = R * (0.55f + 0.5f * Rand01(ref r)) * (0.6f + 0.4f * pw);
                    const int steps = 6;
                    int shown = Mathf.Max(2, Mathf.CeilToInt((steps + 1) * vis));
                    V2 prev = c;
                    for (int k = 1; k <= steps; k++)
                    {
                        float u = (float)k / steps, j = (Rand01(ref r) - 0.5f) * 0.34f;
                        V2 pt = c + V2.FromAngle(a + j) * (len * u);
                        if (k < shown)
                        {
                            Line(prev, pt, 2.4f, new Rgba(0.04f, 0.03f, 0.024f, 0.85f * stay));
                            Line(prev + new V2(1.4f, 1.4f), pt + new V2(1.4f, 1.4f), 0.8f, new Rgba(0.78f, 0.72f, 0.63f, 0.25f * stay));
                        }
                        prev = pt;
                    }
                }
            }
        }

        /// Dust puffs rolling out round the circle, drawn under the enemies with the crater.
        void HammerDustPuffs(float R, V2 c, float pw, float after, int seed)
        {
            for (int d = 0; d < 16; d++)  // dust rolling out
            {
                float an = (float)d / 16f * MathX.Tau + Hash01(seed, d) * 0.3f, k = after / 1.1f;
                if (k < 0f || k > 1f)
                    continue;
                float rr = R * (0.55f + 0.55f * EaseOut3(Mathf.Min(1f, k * 1.1f))), pr = 8f + 20f * EaseOut3(k);
                Circle(c + new V2(Mathf.Cos(an) * rr, Mathf.Sin(an) * rr * 0.9f - k * 10f), pr, new Rgba(0.62f, 0.58f, 0.52f, 0.34f * (1f - k) * (0.5f + 0.5f * pw)), 12);
            }
        }

        void DrawHammerChips(float pw, V2 c, float after, int seed)
        {
            for (int s2 = 0; s2 < 14; s2++)  // stone chips
            {
                float age = after - 0.02f * Hash01(seed, s2 + 20);
                if (age < 0f || age > 0.9f)
                    continue;
                float a3 = Hash01(seed, s2 + 30) * MathX.Tau, v = 70f + 150f * Hash01(seed, s2 + 40) * pw, hh = Mathf.Max(0f, 150f * age - 260f * age * age);
                float al = 1f - Mathf.Clamp01((age - 0.55f) / 0.35f);
                V2 g = c + new V2(Mathf.Cos(a3) * v * age * 0.8f, Mathf.Sin(a3) * v * age * 0.6f);
                Circle(g + new V2(0f, 3f), 3f, new Rgba(0f, 0f, 0f, 0.3f * al), 6);
                Square(g + new V2(0f, -hh), 5f, (s2 % 2 == 0 ? new Rgba(0.59f, 0.56f, 0.5f) : new Rgba(0.43f, 0.41f, 0.37f)).WithAlpha(al));
            }
        }

        static float Hash01(int seed, int k)
        {
            uint h = (uint)(seed * 73856093) ^ (uint)(k * 19349663);
            h = h * 2654435761u + 12345u;
            h ^= h >> 13;
            return (h & 0xffff) / 65535f;
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
        int ownerOfModel;  // whose staff DrawMeleeModel is drawing (its stacks light the head)
        void DrawMeleeModel(WeaponId id, V2 o, float ang, float reach, float alpha, Rgba col, bool bright, float bend = 0f, Rgba arm = default(Rgba))
        {
            if (alpha <= 0f)
                return;
            V2 dir = V2.FromAngle(ang), side = dir.Orthogonal();
            var edge = col.WithAlpha(0.9f * alpha);
            switch (id)
            {
                case WeaponId.Rapier:
                {
                    // Needle: the hand and arm reach with the thrust, the thin blade flexes at the end of it, a swept hilt, and the
                    // off hand stays behind your back.
                    float x01 = Mathf.Clamp01((reach - 26f) / 104f), flex = bend * 5f;
                    Rgba armc = arm.A > 0f ? arm : new Rgba(0.29f, 0.66f, 0.61f), leather = new Rgba(0.72f, 0.56f, 0.34f, alpha), hide = new Rgba(0.16f, 0.11f, 0.06f, alpha);
                    V2 hand = o + dir * (12f + 12f * x01);
                    Line(o - side * 4f, o - dir * 9f + side * 9f, 5f, armc.WithAlpha(alpha));
                    Circle(o - dir * 11f + side * 9f, 4.6f, hide, 10);
                    Circle(o - dir * 11f + side * 9f, 3.6f, leather, 10);
                    Line(o + dir * 2f + side * 5f, hand, 7.2f, new Rgba(0.05f, 0.1f, 0.1f, alpha));
                    Line(o + dir * 2f + side * 5f, hand, 5f, armc.WithAlpha(alpha));
                    V2 prev = hand + dir * 8f, tipR = o + dir * reach;
                    for (int i = 1; i <= 8; i++)
                    {
                        float u = i / 8f, bow = 1f - (2f * u - 1f) * (2f * u - 1f);
                        V2 pt = V2.Lerp(hand + dir * 8f, tipR, u) - side * (flex * bow);
                        Line(prev, pt, 2.5f, new Rgba(0.95f, 0.96f, 1f, alpha));
                        prev = pt;
                    }
                    Arc(hand + dir * 4f, 7f, 3f, MeleeGuard.WithAlpha(alpha), 10, ang - 1.7f, 3.4f);
                    Line(hand + dir * 5f + side * 8f, hand + dir * 5f - side * 6f, 2f, MeleeGuard.WithAlpha(alpha));
                    Circle(hand, 6f, hide, 12);
                    Circle(hand, 5.2f, leather, 12);
                    break;
                }
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
                case WeaponId.HolyMace:  // the Quarterstaff: two hands, a lantern head (QuarterstaffLook.cs)
                    DrawQuarterstaffModel(o, ang, reach, alpha, col, bright, bend, arm, ownerOfModel);
                    break;
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
                case WeaponId.Greatsword:
                {
                    // Moonlit Crescent: a wide blade with a fuller, a big gold guard, held in two gloved hands joined to the body by arms.
                    const float gx = 36f;
                    float L = reach * 0.98f, w = 7.6f;
                    Rgba armc = arm.A > 0f ? arm : new Rgba(0.29f, 0.66f, 0.61f), dark = new Rgba(0.05f, 0.1f, 0.1f, alpha), leather = new Rgba(0.72f, 0.56f, 0.34f, alpha), hide = new Rgba(0.16f, 0.11f, 0.06f, alpha);
                    V2 h1 = o + dir * 17f, h2 = o + dir * 31f;
                    Line(o - side * 3f, h1, 7.2f, dark);
                    Line(o + side * 3f, h2, 7.2f, dark);
                    Line(o - side * 3f, h1, 5f, armc.WithAlpha(alpha));
                    Line(o + side * 3f, h2, 5f, armc.WithAlpha(alpha));
                    Line(o - dir * 6f, o + dir * gx, 5f, new Rgba(0.23f, 0.17f, 0.12f, alpha));
                    Line(o - dir * 6f, o + dir * gx, 3f, MeleeHandle.WithAlpha(alpha));
                    Circle(o - dir * 7f, 3.4f, new Rgba(0.72f, 0.6f, 0.29f, alpha), 8);
                    Circle(h1, 6f, hide, 14);
                    Circle(h1, 4.8f, leather, 14);
                    Circle(h2, 5.2f, hide, 14);
                    Circle(h2, 4f, leather, 14);
                    V2 g0 = o + dir * (gx - 3f), g1 = o + dir * (gx + 3f);
                    SetColor(MeleeGuard.WithAlpha(alpha));
                    Quad(g0 - side * 12f, g1 - side * 10f, g1 + side * 10f, g0 + side * 12f);
                    V2 b0 = o + dir * (gx + 1f);
                    float bl = L - gx - 1f;
                    V2 q0 = b0 - side * w, q1 = b0 + dir * (bl - 18f) - side * w * 0.82f, tipG = b0 + dir * bl, q3 = b0 + dir * (bl - 18f) + side * w * 0.5f, q4 = b0 + side * w * 0.62f;
                    SetColor((bright ? Rgba.White : new Rgba(0.88f, 0.93f, 0.99f)).WithAlpha(alpha));
                    Quad(q0, q1, q3, q4);
                    Tri(q1, tipG, q3);
                    var edgeG = Rgba.White.WithAlpha(0.95f * alpha);
                    Line(q0, q1, 1.1f, edgeG);
                    Line(q1, tipG, 1.1f, edgeG);
                    Line(tipG, q3, 1.1f, edgeG);
                    Line(q3, q4, 1.1f, edgeG);
                    Line(b0 + dir * 8f - side * 0.6f, b0 + dir * (bl * 0.86f) - side * 0.4f, 1.8f, new Rgba(0.16f, 0.24f, 0.31f, 0.5f * alpha));
                    Line(b0 + dir * 8f - side * 2.1f, b0 + dir * (bl * 0.84f) - side * 1.8f, 0.8f, Rgba.White.WithAlpha(0.55f * alpha));
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
                    if (id == WeaponId.Katana && bend != 0f)
                    {
                        // Moon Steel: the blade curves toward the way it sweeps, so the cut always bends along its motion
                        // (the mockup's blade-curve flip). The curve grows toward the tip, like a real katana's sori.
                        float len = r1 - hilt, sori = len * 0.07f * bend;
                        const int segs = 6;
                        V2 pt = h0 + side * wd, pb = h0 - side * wd * 0.6f;
                        for (int i = 1; i <= segs; i++)
                        {
                            float u = (float)i / segs;
                            V2 c = h0 + dir * (len * u) + side * (sori * u * u);
                            float wt = wd * (1f - 0.2f * u), wb = wd * 0.6f * (1f - 0.17f * u);
                            if (i == segs)
                                break;
                            V2 nt = c + side * wt, nb = c - side * wb;
                            SetColor(fill.WithAlpha(alpha));
                            Quad(pt, nt, nb, pb);
                            Line(pt, nt, 1.2f, edge);
                            Line(nb, pb, 1.2f, edge);
                            pt = nt;
                            pb = nb;
                        }
                        V2 tip2 = h0 + dir * (len + 10f) + side * sori;
                        SetColor(fill.WithAlpha(alpha));
                        Tri(pt, tip2, pb);
                        Line(pt, tip2, 1.2f, edge);
                        Line(tip2, pb, 1.2f, edge);
                        Line(h0 + side * wd, h0 - side * wd * 0.6f, 1.2f, edge);
                        break;
                    }
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
