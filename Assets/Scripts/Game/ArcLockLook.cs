using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Arc Lightning's lock-on chain in the Static Brand look (the "Arc Lightning Lock-On" artifact, look 2), ported from the mockup's
    /// numbers at the game's own scale (the mockup was drawn 1:1). Core/ArcLock.cs has the rules.
    ///   Holding: the cone fills with a violet glow from your feet, its edges dashed and crawling, a scan line sweeping across it;
    ///     the staff glows brighter and spits more static with every tag. Each tagged enemy wears a brand: a violet crackle round it,
    ///     a diamond rune on it and a thin live wire back to your staff, so you can see who's locked after they wander off.
    ///   Letting go: a cyan bolt jumps hop to hop down the chain (FxKind.ArcLink), white-hot for 0.12 s, then fading over 0.6 s, each
    ///     landing in a white flash; stunned enemies crackle. Forked Volley's bolts (FxKind.ArcFork) curve out to alternate sides
    ///     and grow into their targets in 0.08 s.
    /// Bolts are three strokes, as in the mockup: a wide faint glow, a coloured body and a white core. With Visual effects on it's
    /// all drawn in the additive glow pass (DrawArcLightningGlow); off, the same strokes in the world pass without the soft glows.
    public partial class ArenaApp
    {
        static readonly Rgba LockViolet = new Rgba(0.69f, 0.486f, 1f), LockCyan = new Rgba(0.47f, 0.86f, 1f);
        static readonly List<V2> lockPts = new List<V2>(48);
        bool lockGlow;  // drawing in the glow pass (soft glows on)

        static float LockRand(ref uint s)
        {
            s ^= s << 13; s ^= s >> 17; s ^= s << 5;
            return (s & 0xFFFFFF) / 16777216f;
        }

        static uint LockSeed(int a, int b) => (uint)(a * 73856093 ^ b * 19349663) | 1u;

        /// A jagged run from a to b into lockPts: each middle point thrown sideways by up to amp, most in the middle (the mockup's jag).
        static void LockJag(V2 a, V2 b, int n, float amp, ref uint s)
        {
            lockPts.Clear();
            lockPts.Add(a);
            V2 nrm = (b - a).Normalized().Orthogonal();
            for (int i = 1; i < n; i++)
            {
                float f = (float)i / n;
                lockPts.Add(a + (b - a) * f + nrm * ((LockRand(ref s) * 2f - 1f) * amp * Mathf.Sin(Mathf.PI * f)));
            }
            lockPts.Add(b);
        }

        /// The mockup's bolt: a wide faint stroke, the body in its colour, a white core (count: how many of lockPts to draw).
        void LockBolt(float w, Rgba col, float a, bool core, int count = -1)
        {
            if (a <= 0.01f)
                return;
            int n = count < 0 ? lockPts.Count : Mathf.Min(count, lockPts.Count);
            for (int pass = lockGlow ? 0 : 1; pass < (core ? 3 : 2); pass++)
            {
                float width = pass == 0 ? w * 4.5f : pass == 1 ? w * 1.8f : Mathf.Max(0.8f, w * 0.6f);
                Rgba c = pass == 0 ? col.WithAlpha(0.16f * a) : pass == 1 ? col.WithAlpha(0.6f * a) : Rgba.White.WithAlpha(0.9f * a);
                for (int i = 0; i + 1 < n; i++)
                    Line(lockPts[i], lockPts[i + 1], width, c);
            }
        }

        void LockGlowDisc(V2 c, float r, Rgba col, float a)
        {
            if (lockGlow)
                GlowDisc(c, r, col, a);
        }

        void LockDashes(V2 a, V2 b, float t, Rgba col)
        {
            float len = (b - a).Length;
            if (len < 1f)
                return;
            V2 d = (b - a) / len;
            for (float x = -MathX.Fmod(t * 30f, 13f); x < len; x += 13f)
            {
                float x0 = Mathf.Max(0f, x), x1 = Mathf.Min(len, x + 6f);
                if (x1 > x0)
                    Line(a + d * x0, a + d * x1, 1.2f, col);
            }
        }

        /// The glow pass (Juice.cs DrawGlowPass): every Arc Lightning draw being held and every chain and fork bolt.
        void DrawArcLightningGlow()
        {
            lockGlow = true;
            foreach (var p in game.Players.Values)
                if (p.Weapon.Id == WeaponId.ArcLightning && p.DrawT > 0f && !p.Downed)
                    DrawArcLockCharge(p, p.Id == game.MyId, game.AnimT);
            foreach (var fx in game.Effects)
                DrawArcLockFx(fx);
        }

        /// The world pass with Visual effects off (WorldRenderer and WorldEffects call these).
        void DrawArcLockFlatCharge(Player p, bool mine, float t)
        {
            if (fxOn)
                return;
            lockGlow = false;
            DrawArcLockCharge(p, mine, t);
        }

        void DrawArcLockFlatFx(Effect fx)
        {
            if (fxOn)
                return;
            lockGlow = false;
            DrawArcLockFx(fx);
        }

        /// The cone, the staff, and the brands and wires on everything tagged.
        void DrawArcLockCharge(Player p, bool mine, float t)
        {
            float k = mine ? 1f : 0.4f;  // an ally's cone is quieter
            int n = p.ArcTags.Count, max = Game.ArcTagMax(p);
            float fill = Mathf.Clamp01(p.DrawT / Mathf.Max(0.1f, game.BowFull(p)));
            float half = Game.ArcHalf(p), reach = Game.ArcReach(p), aim = p.Aim;
            V2 o = p.Disp;
            // The cone: a glow from your feet out to its rim, dashed edges crawling outwards, and the scan line.
            FanGradient(o, reach, LockViolet.WithAlpha((0.16f + 0.08f * fill) * k), LockViolet.WithAlpha(0.02f * k), 28, aim - half, 2f * half);
            var edge = LockViolet.WithAlpha(0.55f * k);
            LockDashes(o + V2.FromAngle(aim - half) * 14f, o + V2.FromAngle(aim - half) * reach, t, edge);
            LockDashes(o + V2.FromAngle(aim + half) * 14f, o + V2.FromAngle(aim + half) * reach, t, edge);
            int rim = 40;
            for (int i = 0; i < rim; i += 2)
            {
                float a0 = aim - half + 2f * half * i / rim, a1 = aim - half + 2f * half * (i + 1) / rim;
                Line(o + V2.FromAngle(a0) * reach, o + V2.FromAngle(a1) * reach, 1.2f, edge);
            }
            float scan = aim + half * 0.92f * Mathf.Sin(t * 7f);
            Line(o, o + V2.FromAngle(scan) * reach, 2f, LockViolet.WithAlpha(0.28f * k));
            // The staff: brighter and noisier with every tag.
            V2 tip = o + V2.FromAngle(aim) * (p.Def.BodyR + 12f);
            LockGlowDisc(tip, 12f + n * 2.4f, LockViolet, 0.4f + 0.04f * n);
            uint s = LockSeed(p.Id, (int)(t * 18f));
            for (int i = 0; i < 1 + n / 3; i++)
            {
                float a = LockRand(ref s) * MathX.Tau, l = 8f + n * 1.5f;
                LockJag(tip, tip + V2.FromAngle(a) * l, 3, 3f, ref s);
                LockBolt(0.7f, LockViolet, 0.8f, true);
            }
            // The brands and their live wires.
            for (int i = 0; i < n; i++)
            {
                Enemy e;
                if (!game.Enemies.TryGetValue(p.ArcTags[i], out e) || e.Hp <= 0f)
                    continue;
                V2 c = e.Disp;
                float len = (c - tip).Length;
                uint ws = LockSeed(i * 7 + p.Id, (int)(t * 14f));
                LockJag(tip, c, Mathf.Max(6, (int)(len / 24f)), 7f, ref ws);
                LockBolt(0.6f, LockViolet, 0.4f * (mine ? 1f : 0.6f), false);
                DrawLockBrand(e, i, t);
            }
            if (mine && n >= max)
                LockGlowDisc(tip, 30f, Rgba.White, 0.25f + 0.15f * Mathf.Sin(t * 20f));  // full: the staff flickers white
        }

        /// A brand: a crackle round the enemy, a diamond rune on it, and the flash of being tagged.
        void DrawLockBrand(Enemy e, int i, float t)
        {
            V2 c = e.Disp;
            float r = e.Radius + 4f;
            uint s = LockSeed(e.Id * 13 + i, (int)(t * 16f));
            for (int k = 0; k < 4; k++)
            {
                float a = LockRand(ref s) * MathX.Tau;
                LockJag(c + V2.FromAngle(a) * r, c + V2.FromAngle(a + 1f) * r, 4, 4f, ref s);
                LockBolt(0.9f, LockViolet, 0.9f, true);
            }
            var rune = LockViolet.WithAlpha(0.9f);
            V2 up = c + new V2(0f, -5f), rt = c + new V2(4f, 0f), dn = c + new V2(0f, 5f), lf = c + new V2(-4f, 0f);
            Line(up, rt, 1.5f, rune); Line(rt, dn, 1.5f, rune); Line(dn, lf, 1.5f, rune); Line(lf, up, 1.5f, rune);
            foreach (var fx in FxOf(FxKind.ArcTag))
                if (fx.Kind == FxKind.ArcTag && (int)fx.Pos2.Y == e.Id && fx.T < 0.15f)
                    LockGlowDisc(c, 26f, LockViolet, 0.8f * (1f - fx.T / 0.15f));
        }

        /// A chain hop, a fizzle or a Forked Volley bolt.
        void DrawArcLockFx(Effect fx)
        {
            if (fx.Kind == FxKind.ArcLink)
                DrawLockLink(fx);
            else if (fx.Kind == FxKind.ArcFork)
                DrawLockFork(fx);
        }

        void DrawLockLink(Effect fx)
        {
            float d = fx.T;
            V2 from = fx.Pos, to = fx.Pos2;
            if (fx.Radius < 0f)  // a fizzle: nothing tagged
            {
                float f = Mathf.Clamp01(d / 0.3f);
                uint fs = LockSeed((int)from.X, (int)from.Y);
                for (int k = 0; k < 5; k++)
                {
                    float a = LockRand(ref fs) * MathX.Tau, l = 6f + 14f * f;
                    LockJag(from + V2.FromAngle(a) * (l * 0.4f), from + V2.FromAngle(a) * l, 3, 2f, ref fs);
                    LockBolt(0.7f, LockViolet, 1f - f, false);
                }
                LockGlowDisc(from, 14f, LockViolet, 0.5f * (1f - f));
                return;
            }
            float a0 = d < 0.12f ? 1f : Mathf.Max(0f, 1f - (d - 0.12f) / 0.6f);
            float len = (to - from).Length;
            uint s = LockSeed((int)fx.Radius * 31 + (int)to.X, (int)(game.AnimT * 20f) + (int)to.Y);
            LockJag(from, to, Mathf.Max(3, Mathf.RoundToInt(len / 18f)), Mathf.Min(16f, len / 6f), ref s);
            LockBolt(d < 0.12f ? 2.2f : 1.2f, LockCyan, a0, d < 0.3f);
            if (d < 0.15f)
                LockGlowDisc(to, 26f, Rgba.White, 0.7f * (1f - d / 0.15f));
            if (fx.Radius < 0.5f && d < 0.1f)
                LockGlowDisc(from, 40f, Rgba.White, 0.5f * (1f - d / 0.1f));  // the release, at the staff
            if (d < Game.ArcStun)  // the stun: violet crackle round whoever it hit (bosses aren't stunned)
                foreach (var e in game.Enemies.Values)
                {
                    if (e.Def.IsBoss || e.Hp <= 0f || e.Pos.DistanceTo(to) > e.Radius)
                        continue;
                    uint cs = LockSeed(e.Id, (int)(game.AnimT * 16f));
                    for (int k = 0; k < 2; k++)
                    {
                        float a = LockRand(ref cs) * MathX.Tau, r = e.Radius + 3f;
                        LockJag(e.Disp + V2.FromAngle(a) * r, e.Disp + V2.FromAngle(a + 1f) * r, 3, 4f, ref cs);
                        LockBolt(0.7f, LockViolet, 0.9f, true);
                    }
                    break;
                }
        }

        void DrawLockFork(Effect fx)
        {
            float d = fx.T;
            if (d > 0.4f)
                return;
            V2 from = fx.Pos, to = fx.Pos2, mid = (from + to) * 0.5f, dir = to - from;
            float len = dir.Length;
            if (len < 1f)
                return;
            V2 ctrl = mid + (dir / len).Orthogonal() * fx.Radius;
            uint s = LockSeed((int)fx.Radius + (int)to.X * 3, (int)(game.AnimT * 25f));
            lockPts.Clear();
            for (int k = 0; k <= 12; k++)
            {
                float u = k / 12f, j = k > 0 && k < 12 ? (LockRand(ref s) - 0.5f) * 8f : 0f;
                V2 q = from * ((1f - u) * (1f - u)) + ctrl * (2f * (1f - u) * u) + to * (u * u);
                lockPts.Add(q + new V2(j, j));
            }
            int grown = Mathf.CeilToInt(Mathf.Clamp01(d / 0.08f) * 12f) + 1;
            float a = d < 0.12f ? 1f : 1f - (d - 0.12f) / 0.28f;
            bool middle = Mathf.Abs(fx.Radius) < Game.ForkBend * 0.5f;
            LockBolt(1.4f, middle ? LockCyan : LockViolet, a, true, grown);
            if (d > 0.08f)
                LockGlowDisc(to, 18f, Rgba.White, 0.6f * a);
        }
    }

    public partial class ArenaApp
    {
        /// Every frame (sound on whatever Visual effects say): the lock-on chain's one-shots from the effects as they arrive, and the hum
        /// while your own draw is held.
        void TickArcLockSounds()
        {
            float now = Time.unscaledTime;
            foreach (var fx in game.Effects)
            {
                if (fx.Heard)
                    continue;
                fx.Heard = true;
                if (fx.Kind == FxKind.ArcTag)
                    sounds.PlayArcTag(now, (int)fx.Radius, (int)fx.Pos2.X == game.MyId);
                else if (fx.Kind == FxKind.ArcLink || fx.Kind == FxKind.ArcFork)
                {
                    var me = game.Me;
                    bool mine = me != null && fx.Pos.DistanceTo(me.Pos) < me.Def.BodyR + 40f;  // it left from your staff
                    if (fx.Kind == FxKind.ArcFork)
                        sounds.PlayArcFork(now, fx.Radius < -1f ? 0 : fx.Radius > 1f ? 2 : 1, mine);
                    else if (fx.Radius < 0f)
                        sounds.PlayArcFizzle(now, mine);
                    else
                        sounds.PlayArcHop(now, (int)fx.Radius, mine || fx.Radius > 0.5f);  // (later hops are far from anyone's staff: full volume)
                }
            }
            var p = game.Me;
            bool humming = p != null && p.Weapon != null && p.Weapon.Id == WeaponId.ArcLightning && p.DrawT > 0f && !p.Downed;
            sounds.SetArcHum(humming ? 1f : 0f, humming ? p.ArcTags.Count : 0);
        }
    }
}
