using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Blood Freeze, drawn as in Docs/Mockups/blood-freeze.html. The cast's `FxKind.BloodFreeze` carries the whole show and every part
    /// runs off its age (`Effect.T`), so nothing is synced and the particles keep no state (each one is worked out from its spawn time):
    ///   charge  a rune circle spins up at the caster's feet and blood streams in from every side (0.55 s)
    ///   wave    a crimson-to-white ring tears out slowing as it goes, with a flash, a kick of the screen and ice shards and blood thrown
    ///           off its front; frost and cracks spread over the floor behind it and stay for the whole freeze
    ///   frozen  each enemy is sealed in a hexagonal crystal as the wave reaches it, with red veins, a glint and a timer ring that drains;
    ///           a banner at the top counts down
    ///   thaw    in the last second the crystals flicker and crack, then shatter into shards, blood and mist
    /// Visual effects off: the old plain ring. Reduced: less shaking, and a third of the particles.
    public partial class ArenaApp
    {
        const float BfZ = 1.5f, BfScreen = 2.1f;  // the mockup was drawn on a 720 px arena: details grow by BfZ, the big shapes by BfScreen
        static readonly Rgba BfBlood = new Rgba(224f / 255f, 40f / 255f, 74f / 255f), BfIce = new Rgba(143f / 255f, 227f / 255f, 1f);

        /// One frozen enemy: when its crystal starts to grow (renderer clock), when the freeze should end, and when it broke (0 = still held).
        class BfState { public float Start, End, Thaw, Seed, R; public V2 Pos; public bool Boss; }
        readonly Dictionary<int, BfState> bfStates = new Dictionary<int, BfState>();
        class BfCrack { public V2[] Pts; public bool Red; public float W; }
        readonly Dictionary<Effect, List<BfCrack>> bfCracks = new Dictionary<Effect, List<BfCrack>>();
        readonly HashSet<Effect> bfKicked = new HashSet<Effect>();
        readonly List<Effect> bfGone = new List<Effect>();
        readonly List<int> bfDead = new List<int>();

        void ClearBloodFreezeLook()
        {
            bfStates.Clear();
            bfCracks.Clear();
            bfKicked.Clear();
        }

        static Rgba BfMix(Rgba a, Rgba b, float t) => a.Mix(b, Mathf.Clamp01(t));
        static float BfRnd(float seed, float a, float b) => a + (b - a) * Hash01(seed);

        /// Each frame: the screen kick when a wave leaves, and forgetting what is over.
        void UpdateBloodFreezeLook(float t)
        {
            foreach (var fx in game.Effects)
            {
                if (fx.Kind != FxKind.BloodFreeze)
                    continue;
                if (fx.T >= Game.FreezeCharge && bfKicked.Add(fx))
                {
                    var me = game.Me;
                    if (me != null && me.Pos.DistanceTo(fx.Pos) < 1900f)
                        Shake(0.32f);  // the mockup's 7 px kick that dies away over half a second
                }
            }
            if (bfKicked.Count > 0 || bfCracks.Count > 0)
            {
                bfGone.Clear();
                foreach (var k in bfKicked)
                    if (!game.Effects.Contains(k))
                        bfGone.Add(k);
                foreach (var k in bfCracks.Keys)
                    if (!game.Effects.Contains(k))
                        bfGone.Add(k);
                foreach (var k in bfGone)
                {
                    bfKicked.Remove(k);
                    bfCracks.Remove(k);
                }
            }
            if (bfStates.Count > 0)
            {
                bfDead.Clear();
                foreach (var kv in bfStates)
                    if (!game.Enemies.ContainsKey(kv.Key) && kv.Value.Thaw <= 0f || kv.Value.Thaw > 0f && t - kv.Value.Thaw > 1.3f || t - kv.Value.Start > 40f)
                        bfDead.Add(kv.Key);
                foreach (int id in bfDead)
                    bfStates.Remove(id);
            }
        }

        // --- The floor: frost and cracks, the rune circle, the wave ----------------------------------------------------

        void DrawBloodFreezeFloor(float t)
        {
            if (!fxOn)
                return;
            foreach (var fx in game.Effects)
                if (fx.Kind == FxKind.BloodFreeze)
                    DrawBfFloor(fx);
        }

        List<BfCrack> BfCracksFor(Effect fx)
        {
            if (bfCracks.TryGetValue(fx, out var list))
                return list;
            list = new List<BfCrack>();
            float seed = fx.Pos.X * 0.37f + fx.Pos.Y * 1.91f;
            const int n = 30;
            for (int i = 0; i < n; i++)
            {
                float a = i * MathX.Tau / n + BfRnd(seed + i, -0.07f, 0.07f), d = 0f;
                var pts = new List<V2> { fx.Pos };
                int step = 0;
                while (d < Game.FreezeMaxR + 40f)
                {
                    step++;
                    d += BfRnd(seed + i * 31f + step, 16f, 34f) * BfScreen;
                    a += BfRnd(seed + i * 17f + step * 3f, -0.07f, 0.07f);
                    V2 at = fx.Pos + V2.FromAngle(a) * d;
                    pts.Add(at);
                    if (d > 50f * BfScreen && Hash01(seed + i * 7f + step * 5f) < 0.28f)
                    {
                        float ba = a + (Hash01(seed + i + step * 9f) < 0.5f ? -1f : 1f) * BfRnd(seed + step * 2f + i, 0.5f, 0.9f);
                        V2 bp = at;
                        var branch = new List<V2> { bp };
                        for (int k = 0; k < 3; k++)
                        {
                            ba += BfRnd(seed + i * 3f + step * 11f + k, -0.2f, 0.2f);
                            bp += V2.FromAngle(ba) * (BfRnd(seed + i + step + k * 13f, 14f, 26f) * BfScreen);
                            branch.Add(bp);
                        }
                        list.Add(new BfCrack { Pts = branch.ToArray(), Red = false, W = 0.7f });
                    }
                }
                list.Add(new BfCrack { Pts = pts.ToArray(), Red = i % 5 == 0, W = 1f });
            }
            bfCracks[fx] = list;
            return list;
        }

        void DrawBfFloor(Effect fx)
        {
            float age = fx.T, hold = fx.Pos2.Y, wr = Game.FreezeWaveR(age);
            V2 c = fx.Pos;
            // frost over what the wave has crossed, and cracks racing out along the floor
            if (age >= Game.FreezeCharge && wr > 0f)
            {
                float A = Mathf.Clamp01((Game.FreezeCharge + hold - age) / 1.2f);
                if (A > 0f)
                {
                    Circle(c, wr, new Rgba(0.43f, 0.75f, 0.94f, 0.05f * A), 96);
                    for (int k = 0; k < 4; k++)  // brighter toward the rim
                        Arc(c, wr * (1f - 0.04f * k), wr * 0.08f, new Rgba(0.56f, 0.86f, 1f, 0.035f * A), 96);
                    foreach (var cr in BfCracksFor(fx))
                    {
                        Rgba col = cr.Red ? new Rgba(BfBlood.R, BfBlood.G, BfBlood.B, 0.45f * A) : new Rgba(0.78f, 0.93f, 1f, 0.4f * A);
                        for (int i = 0; i + 1 < cr.Pts.Length; i++)
                        {
                            V2 p0 = cr.Pts[i];
                            if (p0.DistanceTo(c) > wr)
                                break;
                            if (p0.X < -60f || p0.X > Game.Width + 60f || p0.Y < -60f || p0.Y > Game.Height + 60f)
                                continue;
                            Line(p0, cr.Pts[i + 1], 1.4f * cr.W * BfZ, col);
                        }
                    }
                }
            }
            DrawBfSigil(fx, age, hold);
            DrawBfWave(fx, age, wr);
        }

        void DrawBfSigil(Effect fx, float age, float hold)
        {
            float a, icy = 0f, end = Game.FreezeCharge + hold;
            if (age < Game.FreezeCharge)
                a = age / Game.FreezeCharge * 0.9f;
            else
            {
                icy = Mathf.Clamp01((age - Game.FreezeCharge) / 0.3f);
                a = age < end ? 0.25f + 0.65f * Mathf.Max(0f, 1f - (age - Game.FreezeCharge) / 1.2f) : Mathf.Max(0f, (end + 0.6f - age) / 0.6f * 0.25f);
            }
            if (a <= 0.01f)
                return;
            Rgba col = BfMix(BfBlood, BfIce, icy).WithAlpha(a);
            float rot = age * (age < Game.FreezeCharge ? 2.4f : 0.7f), R = 46f * BfZ;
            V2 c = fx.Pos;
            Arc(c, R, 2.2f, col, 48);
            for (int i = 0; i < 18; i++)  // the dashed inner ring
                Arc(c, 36f * BfZ, 2.2f, col, 3, rot + i * MathX.Tau / 18f, MathX.Tau / 18f * 0.4f);
            for (int k = 0; k < 2; k++)  // the two triangles
                for (int i = 0; i < 3; i++)
                {
                    float a0 = rot + k * MathX.Pi / 3f + i * MathX.Tau / 3f - MathX.Pi / 2f, a1 = rot + k * MathX.Pi / 3f + (i + 1) * MathX.Tau / 3f - MathX.Pi / 2f;
                    Line(c + V2.FromAngle(a0) * R, c + V2.FromAngle(a1) * R, 2.2f, col);
                }
        }

        /// A ring of colour fading from `c0` at radius `r0` to `c1` at `r1`.
        void BfBand(V2 c, float r0, float r1, Rgba c0, Rgba c1, int segs)
        {
            for (int i = 0; i < segs; i++)
            {
                V2 d0 = V2.FromAngle(MathX.Tau * i / segs), d1 = V2.FromAngle(MathX.Tau * (i + 1) / segs);
                VTri(c + d0 * r0, c0, c + d0 * r1, c1, c + d1 * r1, c1);
                VTri(c + d0 * r0, c0, c + d1 * r1, c1, c + d1 * r0, c0);
            }
        }

        void DrawBfWave(Effect fx, float age, float wr)
        {
            if (age < Game.FreezeCharge || wr <= 2f)
                return;
            float u = (age - Game.FreezeCharge) / Game.FreezeSweep;
            float fade = u < 0.8f ? 1f : Mathf.Clamp01(1f - (u - 0.8f) / 0.45f);
            if (fade <= 0f)
                return;
            float w = 64f * BfScreen, r0 = Mathf.Max(0f, wr - w), span = wr - r0;
            int segs = 120;
            Rgba a0 = new Rgba(160f / 255f, 20f / 255f, 50f / 255f, 0f),
                 a1 = new Rgba(BfBlood.R, BfBlood.G, BfBlood.B, 0.32f * fade),
                 a2 = new Rgba(BfIce.R, BfIce.G, BfIce.B, 0.65f * fade),
                 a3 = new Rgba(1f, 1f, 1f, 0.95f * fade);
            BfBand(fx.Pos, r0, r0 + span * 0.55f, a0, a1, segs);
            BfBand(fx.Pos, r0 + span * 0.55f, r0 + span * 0.88f, a1, a2, segs);
            BfBand(fx.Pos, r0 + span * 0.88f, wr, a2, a3, segs);
            Arc(fx.Pos, wr, 2.5f * BfScreen, new Rgba(1f, 1f, 1f, 0.9f * fade), segs);
            foreach (float f in new[] { 0.86f, 0.7f })
                Arc(fx.Pos, wr * f, 1f * BfScreen, new Rgba(BfBlood.R, BfBlood.G, BfBlood.B, 0.55f * fade), segs);
        }

        // --- The effect layer: charge, wave particles, flash -----------------------------------------------------------

        void DrawBloodFreeze(Effect fx)
        {
            float age = fx.T;
            if (!fxOn)  // the old ring: out and gone in under a second
            {
                float f = age / 0.8f;
                if (f < 1f)
                    Arc(fx.Pos, 600f * (0.2f + 0.8f * f), 4f, fx.Color.WithAlpha(1f - f), 64);
                return;
            }
            V2 c = fx.Pos;
            int thin = fxReduced ? 3 : 1;
            // the caster's glow, pulsing as the blood gathers
            if (age < Game.FreezeCharge + 0.4f)
            {
                float pulse = age < Game.FreezeCharge ? 1f + 0.18f * Mathf.Sin(age * 40f) : Mathf.Clamp01(1f - (age - Game.FreezeCharge) / 0.4f);
                GlowDisc(c, 44f * BfZ * pulse, BfBlood, 0.5f * Mathf.Clamp01(age < Game.FreezeCharge ? 1f : pulse));
            }
            // blood streaming in during the charge
            const float inRate = 180f;
            if (age < Game.FreezeCharge + 0.4f)
            {
                int i0 = Mathf.Max(0, Mathf.CeilToInt((age - 0.4f) * inRate)), i1 = Mathf.FloorToInt(Mathf.Min(age, Game.FreezeCharge) * inRate);
                for (int i = i0; i <= i1; i += thin)
                {
                    float ts = i / inRate, u = age - ts;
                    float a = Hash01(i * 3.7f) * MathX.Tau, d = BfRnd(i * 5.1f, 70f, 150f) * BfScreen;
                    V2 off = V2.FromAngle(a) * d;
                    V2 now = c + off * Mathf.Exp(-7f * u), prev = c + off * Mathf.Exp(-7f * Mathf.Max(0f, u - 0.02f));
                    Line(prev, now, BfRnd(i * 1.9f, 1.5f, 3f) * BfZ, new Rgba(1f, 0.27f, 0.39f, 0.9f * Mathf.Clamp01(1f - u / 0.4f) + 0.1f));
                }
            }
            // flash as the wave leaves
            if (age >= Game.FreezeCharge && age < Game.FreezeCharge + 0.35f)
                GlowDisc(c, 1700f, new Rgba(0.84f, 0.96f, 1f), (fxReduced ? 0.16f : 0.4f) * (1f - (age - Game.FreezeCharge) / 0.35f));
            // ice shards and blood thrown off the front of the wave
            if (age >= Game.FreezeCharge)
            {
                const float shardRate = 420f, dropRate = 300f;
                float last = Game.FreezeCharge + Game.FreezeSweep;
                int s0 = Mathf.CeilToInt(Mathf.Max(Game.FreezeCharge, age - 0.9f) * shardRate), s1 = Mathf.FloorToInt(Mathf.Min(age, last) * shardRate);
                for (int i = s0; i <= s1; i += thin)
                {
                    float ts = i / shardRate, u = age - ts;
                    float a = Hash01(i * 1.13f) * MathX.Tau;
                    V2 o = c + V2.FromAngle(a) * Game.FreezeWaveR(ts);
                    if (o.X < -40f || o.X > Game.Width + 40f || o.Y < -40f || o.Y > Game.Height + 40f)
                        continue;
                    float life = BfRnd(i * 6.7f, 0.45f, 0.9f);
                    if (u > life)
                        continue;
                    float head = a + BfRnd(i * 2.9f, -0.5f, 0.5f), sp = BfRnd(i * 4.3f, 30f, 120f) * BfScreen;
                    V2 p = o + V2.FromAngle(head) * (sp * (1f - Mathf.Exp(-1.5f * u)) / 1.5f);
                    BfShard(p, head, BfRnd(i * 8.1f, 4f, 10f) * BfZ, Mathf.Min(1f, (1f - u / life) * 1.6f));
                }
                int d0 = Mathf.CeilToInt(Mathf.Max(Game.FreezeCharge, age - 0.8f) * dropRate), d1 = Mathf.FloorToInt(Mathf.Min(age, last) * dropRate);
                for (int i = d0; i <= d1; i += thin)
                {
                    float ts = i / dropRate, u = age - ts;
                    float life = BfRnd(i * 6.1f, 0.4f, 0.8f);
                    if (u > life)
                        continue;
                    float a = Hash01(i * 2.17f) * MathX.Tau;
                    float back = BfRnd(i * 3.3f, 10f, 50f) * BfScreen;
                    V2 o = c + V2.FromAngle(a) * Mathf.Max(0f, Game.FreezeWaveR(ts) - back);
                    if (o.X < -40f || o.X > Game.Width + 40f || o.Y < -40f || o.Y > Game.Height + 40f)
                        continue;
                    float sp = BfRnd(i * 4.9f, 10f, 50f) * BfScreen;
                    V2 p = o + V2.FromAngle(a) * (sp * (1f - Mathf.Exp(-1.5f * u)) / 1.5f);
                    Circle(p, BfRnd(i * 7.7f, 1.5f, 3.2f) * BfZ, BfBlood.WithAlpha(1f - u / life), 6);
                }
            }
        }

        /// An ice shard: a thin arrowhead pointing along `heading`.
        void BfShard(V2 p, float heading, float size, float alpha)
        {
            if (alpha <= 0.02f)
                return;
            V2 d = V2.FromAngle(heading), o = d.Orthogonal();
            V2 tip = p + d * size, l = p - d * (size * 0.5f) + o * (size * 0.32f), r = p - d * (size * 0.5f) - o * (size * 0.32f);
            SetColor(new Rgba(0.78f, 0.96f, 1f, 0.95f * alpha));
            Tri(tip, l, r);
            Line(tip, l, 1.2f, new Rgba(1f, 1f, 1f, 0.8f * alpha));
        }

        // --- Enemies: the crystal, its timer, its breaking -------------------------------------------------------------

        /// Finds (or starts) this enemy's crystal if a Blood Freeze froze it; null when something else did.
        BfState BfCrystal(Enemy e, float r, float t)
        {
            if (bfStates.TryGetValue(e.Id, out var s))
                return s;
            if (!e.Frozen)
                return null;
            foreach (var fx in game.Effects)
            {
                if (fx.Kind != FxKind.BloodFreeze || fx.T < Game.FreezeCharge || fx.T > Game.FreezeCharge + Game.FreezeSweep + 0.6f)
                    continue;
                float d = fx.Pos.DistanceTo(e.Disp);
                if (d - r > Game.FreezeWaveR(fx.T) + 60f)
                    continue;
                float hold = Game.FreezeHold(e.Def.IsBoss, fx.Pos2.Y - Game.FreezeTime);
                float start = t - fx.T + Game.FreezeWaveAt(Mathf.Max(0f, d - r));
                s = new BfState { Start = start, End = start + hold, Seed = e.Id * 1.37f, R = r, Pos = e.Disp, Boss = e.Def.IsBoss };
                bfStates[e.Id] = s;
                return s;
            }
            return null;
        }

        /// A frozen enemy's crystal. False when this freeze isn't a Blood Freeze's (the caller draws the ordinary ice).
        bool DrawBloodIce(Enemy e, V2 pos, float r, float t)
        {
            var s = BfCrystal(e, r, t);
            if (s == null)
                return false;
            s.Thaw = 0f;
            s.Pos = pos;
            s.R = r;
            float age = t - s.Start, remain = s.End - t;
            if (age < 0f)
                return true;  // the wave hasn't reached it yet
            bool warn = remain < 1f;
            float flick = warn ? 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(t * 20f)) : 1f;
            V2 at = pos;
            if (warn && !fxReduced)
                at += new V2(Mathf.Sin(t * 90f + s.Seed), Mathf.Cos(t * 77f + s.Seed)) * 1.3f;
            float k = Mathf.Clamp(TombBack(age / 0.28f), 0f, 1.15f), R = (r + 8f * BfZ) * k;
            float rot = s.Seed;
            // the hexagon, shaded from pale top-left to pale bottom-right
            Rgba c0 = new Rgba(0.82f, 0.96f, 1f, 0.62f * flick), c1 = new Rgba(0.43f, 0.78f, 0.94f, 0.3f * flick), c2 = new Rgba(0.67f, 0.92f, 1f, 0.55f * flick);
            Rgba[] vc = new Rgba[6];
            V2[] v = new V2[6];
            for (int i = 0; i < 6; i++)
            {
                V2 d = V2.FromAngle(rot + i * MathX.Tau / 6f);
                v[i] = at + d * R;
                float proj = (d.X + d.Y) / 1.4142f * 0.5f + 0.5f;  // 0 top-left .. 1 bottom-right
                vc[i] = proj < 0.5f ? c0.Mix(c1, proj * 2f) : c1.Mix(c2, (proj - 0.5f) * 2f);
            }
            for (int i = 0; i < 6; i++)
                VTri(v[i], vc[i], v[(i + 1) % 6], vc[(i + 1) % 6], at, c1);
            Rgba edge = new Rgba(0.92f, 0.99f, 1f, 0.9f * flick);
            for (int i = 0; i < 6; i++)
            {
                Line(v[i], v[(i + 1) % 6], 1.6f * BfZ * 0.8f, edge);
                Line(at, v[i], 1f, new Rgba(1f, 1f, 1f, 0.28f * flick));
            }
            // the blood in the ice
            Rgba vein = new Rgba(BfBlood.R, BfBlood.G, BfBlood.B, 0.45f + 0.3f * Mathf.Sin(t * 3f + s.Seed));
            for (int j = 0; j < 3; j++)
            {
                float a = Hash01(s.Seed + j * 3.1f) * MathX.Tau;
                V2 prev = at;
                for (int q = 1; q <= 3; q++)
                {
                    V2 nx = at + V2.FromAngle(a + BfRnd(s.Seed + j * 7f + q, -0.5f, 0.5f)) * (q * (r + 6f * BfZ) / 3f * k);
                    Line(prev, nx, 1.2f * BfZ * 0.8f, vein);
                    prev = nx;
                }
            }
            if (warn)  // the crack across it
            {
                Rgba w = new Rgba(1f, 1f, 1f, 0.9f);
                V2[] z = { new V2(-0.7f, -0.2f), new V2(-0.1f, 0.05f), new V2(0.1f, -0.25f), new V2(0.7f, 0.3f) };
                for (int i = 0; i + 1 < z.Length; i++)
                    Line(at + z[i] * R, at + z[i + 1] * R, 1.2f * BfZ * 0.8f, w);
            }
            // a glint wandering round inside
            float ga = t * 2f + s.Seed, gs = (3f + 2f * Mathf.Sin(t * 6f + s.Seed)) * BfZ * 0.8f;
            V2 g = at + V2.FromAngle(ga) * (R * 0.6f);
            Line(g - new V2(gs, 0f), g + new V2(gs, 0f), 1.3f * BfZ * 0.7f, Rgba.White);
            Line(g - new V2(0f, gs), g + new V2(0f, gs), 1.3f * BfZ * 0.7f, Rgba.White);
            // the timer ring
            Rgba tc = warn ? new Rgba(BfBlood.R, BfBlood.G, BfBlood.B, flick) : new Rgba(BfIce.R, BfIce.G, BfIce.B, 0.9f);
            float total = Mathf.Max(0.1f, s.End - s.Start);
            Arc(pos, r + 14f * BfZ * 0.8f, 2.2f * BfZ * 0.8f, tc, 40, -MathX.Pi / 2f, MathX.Tau * Mathf.Clamp01(remain / total));
            // the burst as it freezes: flash, shards and sparks
            if (age < 0.25f)
                GlowDisc(pos, r + 16f * BfZ * 0.8f + 12f, new Rgba(0.9f, 0.98f, 1f), 0.9f * (1f - age / 0.25f));
            if (age < 0.9f)
            {
                int n = fxReduced ? 4 : 12;
                for (int i = 0; i < n; i++)
                {
                    float life = BfRnd(s.Seed + i * 3.1f, 0.45f, 0.9f);
                    if (age > life)
                        continue;
                    float a = Hash01(s.Seed + i * 1.7f) * MathX.Tau, sp = BfRnd(s.Seed + i * 2.3f, 60f, 190f) * 1.3f;
                    BfShard(pos + V2.FromAngle(a) * (sp * (1f - Mathf.Exp(-1.5f * age)) / 1.5f), a, BfRnd(s.Seed + i * 4.9f, 4f, 10f) * BfZ * 0.8f, Mathf.Min(1f, (1f - age / life) * 1.6f));
                }
                for (int i = 0; i < 5 && age < 0.6f; i++)
                {
                    float life = BfRnd(s.Seed + i * 5.5f, 0.3f, 0.6f);
                    if (age > life)
                        continue;
                    V2 p = pos + new V2(BfRnd(s.Seed + i, -8f, 8f), BfRnd(s.Seed + i * 2f, -8f, 8f)) * BfZ * 0.8f + new V2(BfRnd(s.Seed + i * 3f, -30f, 30f), BfRnd(s.Seed + i * 4f, -30f, 30f)) * age;
                    float sz = BfRnd(s.Seed + i * 6f, 3f, 6f) * BfZ * 0.8f * (1f - age / life);
                    Rgba sc = new Rgba(1f, 1f, 1f, 1f - age / life);
                    Line(p - new V2(sz, 0f), p + new V2(sz, 0f), 1.2f, sc);
                    Line(p - new V2(0f, sz), p + new V2(0f, sz), 1.2f, sc);
                }
            }
            return true;
        }

        /// The crystal breaking: shards, blood and mist for a second once the freeze has ended.
        void DrawBloodShatter(Enemy e, V2 pos, float r, float t)
        {
            if (!bfStates.TryGetValue(e.Id, out var s) || e.Frozen)
                return;
            if (t - s.Start < 0f)
                return;
            if (s.Thaw <= 0f)
                s.Thaw = t;
            float u = t - s.Thaw;
            if (u > 1f)
                return;
            int n = fxReduced ? 6 : 18;
            for (int i = 0; i < n; i++)
            {
                float life = BfRnd(s.Seed + i * 3.1f, 0.5f, 1f);
                if (u > life)
                    continue;
                float a = Hash01(s.Seed + i * 1.7f) * MathX.Tau, sp = BfRnd(s.Seed + i * 2.3f, 70f, 230f) * 1.3f;
                BfShard(pos + V2.FromAngle(a) * (sp * (1f - Mathf.Exp(-1.5f * u)) / 1.5f), a, BfRnd(s.Seed + i * 4.9f, 4f, 10f) * BfZ * 0.8f, Mathf.Min(1f, (1f - u / life) * 1.6f));
            }
            for (int i = 0; i < 6; i++)
            {
                float life = BfRnd(s.Seed + i * 6.1f, 0.5f, 0.9f);
                if (u > life)
                    continue;
                float a = Hash01(s.Seed + i * 9.3f) * MathX.Tau, sp = BfRnd(s.Seed + i * 8.3f, 30f, 120f) * 1.3f;
                Circle(pos + V2.FromAngle(a) * (sp * (1f - Mathf.Exp(-1.5f * u)) / 1.5f), BfRnd(s.Seed + i * 5f, 1.5f, 3.2f) * BfZ, BfBlood.WithAlpha(1f - u / life), 6);
            }
            if (u < 0.9f)
            {
                float f = u / 0.9f;
                GlowDisc(pos + new V2(0f, -14f * u), (r + 8f) * (1f + (1f - (1f - f)) * 2.2f) * 1.4f, new Rgba(0.78f, 0.94f, 1f), 0.35f * (1f - f));
            }
        }

        // --- The banner ---------------------------------------------------------------------------------------------

        /// "BLOOD FREEZE 14 frozen · 3.2 s" and a draining bar, top centre, while anything is frozen by one.
        void DrawFreezeBanner()
        {
            if (bfStates.Count == 0)
                return;
            float t = game.AnimT, rem = 0f;
            int n = 0;
            foreach (var s in bfStates.Values)
                if (s.Thaw <= 0f && t >= s.Start && t < s.End)
                {
                    n++;
                    rem = Mathf.Max(rem, s.End - t);
                }
            if (n == 0)
                return;
            float w = 300f, x = W / 2f - w / 2f, y = 78f;
            var ice = new Color(BfIce.R, BfIce.G, BfIce.B);
            Ui.Label(new Rect(x - 40f, y, w + 80f, 24f), "BLOOD FREEZE   " + n + " frozen · " + rem.ToString("0.0") + " s", 16, new Color(0.85f, 0.97f, 1f), TextAnchor.MiddleCenter, true);
            Ui.Fill(new Rect(x, y + 26f, w, 5f), new Color(0f, 0f, 0f, 0.55f));
            Ui.Fill(new Rect(x, y + 26f, w * Mathf.Clamp01(rem / Game.FreezeTime), 5f), rem < 1f ? new Color(BfBlood.R, BfBlood.G, BfBlood.B) : ice);
        }
    }
}
