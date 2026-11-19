using UnityEngine;

namespace Arena
{
    /// The Blood Blast's look (Docs/BloodBlast.md, mockup Docs/Mockups/blood-blast.html): every cut is shaped blood in three
    /// layers (a dark crimson body, a red core, a pale leading edge) that flings drops as it goes.
    ///   - FxKind.BlastCut (one press; Pos = the Hemomancer, Pos2 = along the aim at the step's reach, Radius = their id * 10 + the
    ///     step, Dur = the step + BlbTail): Crimson Slash and Backcut sweep, Thorn Flurry is three short sweeps, Vein Whirl a full
    ///     spin, Sanguine Cleave a blade raised behind you, flipped over and slammed down in a shockwave, Crescent Flight a
    ///     crescent flung out to the reach. Every cut but the Flight follows its owner.
    ///   - FxKind.BlastTide (Crimson Tide; Radius = id * 10 + 1 the gather (Dur = Game.TideCharge), + 2 the wave (Pos = where it
    ///     left, Pos2 = its far end, Dur = Game.TideTravel + BlbTail)): blood gathers into a jittering crescent in front of you, drops
    ///     drawn in, then a great crescent rolls out with a fainter echo behind.
    ///   - FxKind.BlastReturn (a stacked enemy died; Pos = where, Pos2 = (the owner's id, its stacks), Radius = its size, Dur =
    ///     BlbReturnDur): the Blood Vortex: drops spin up out of the body into an orb, the orb flies to its owner trailing a helix
    ///     of drops, and the owner flashes with an absorb ring as it lands (the heal lands then: Game.BlastReturnTime).
    ///   - Enemy.BlastStacks: crimson drops circling the enemy, one a stack (up to ten), swelling a little as they build.
    public partial class ArenaApp
    {
        public const float BlbTail = Game.BlastFxTail;
        static readonly Rgba BlbDark = new Rgba(0.33f, 0f, 0.055f), BlbCore = new Rgba(0.78f, 0.055f, 0.16f), BlbEdge = new Rgba(1f, 0.5f, 0.55f);
        static readonly Rgba BlbHi = new Rgba(1f, 0.78f, 0.8f);

        static float BlbHash(float x) { float s = Mathf.Sin(x * 12.9898f) * 43758.5453f; return s - Mathf.Floor(s); }
        static float BlbEaseOut(float p) { p = Mathf.Clamp01(p); return 1f - (1f - p) * (1f - p) * (1f - p); }
        static float BlbEaseIn(float p) { p = Mathf.Clamp01(p); return p * p * p; }

        delegate void BlbShape(float u, out float ang, out float r, out float th);

        /// One layer of a band of blood round `c`: for u 0..1 the shape gives an angle, an outer radius and a thickness; the layer
        /// takes `scale` of the thickness, set in `inset` of it from the outer edge.
        void BlbLayer(V2 c, BlbShape shape, int n, float scale, float inset, Rgba col)
        {
            SetColor(col);
            V2 po = c, pi = c;
            for (int i = 0; i <= n; i++)
            {
                float a, r, th;
                shape(i / (float)n, out a, out r, out th);
                V2 d = V2.FromAngle(a), o = c + d * (r - th * inset), q = c + d * Mathf.Max(0f, r - th * (inset + scale));
                if (i > 0)
                    Quad(po, o, q, pi);
                po = o;
                pi = q;
            }
        }

        /// The three layers of blood: body, core, pale edge.
        void BlbBlood(V2 c, BlbShape shape, float al, int n = 28)
        {
            BlbLayer(c, shape, n, 1f, 0f, BlbDark.WithAlpha(0.9f * al));
            BlbLayer(c, shape, n, 0.6f, 0.06f, BlbCore.WithAlpha(al));
            BlbLayer(c, shape, n, 0.15f, 0f, BlbEdge.WithAlpha(0.85f * al));
        }

        /// A sweep `p` (0-1) through its life round `c`: the head eases from a0 to a1 (radians off `aim`) over `swing` of the
        /// life with a tail behind it, then the tail catches up and it fades. Returns the head's point and the way it's going.
        bool BlbSweep(V2 c, float aim, float p, float a0, float a1, float r, float w, float swing, float tailFrac, out V2 head, out V2 way)
        {
            head = c;
            way = V2.FromAngle(aim);
            float span = a1 - a0, e = BlbEaseOut(p / swing), f = Mathf.Clamp01((p - swing) / (1f - swing));
            float hd = a0 + span * e, tl = a0 + span * Mathf.Max(0f, e - tailFrac);
            tl += (hd - tl) * BlbEaseIn(f);
            float al = 1f - f;
            if (p <= 0f || al <= 0.01f || Mathf.Abs(hd - tl) < 0.01f)
                return false;
            BlbBlood(c, (float u, out float a, out float rr, out float th) => { a = aim + tl + (hd - tl) * u; rr = r; th = w * (0.06f + 0.94f * Mathf.Pow(u, 1.5f)); }, al);
            float ha = aim + hd;
            head = c + V2.FromAngle(ha) * (r - w * 0.4f);
            way = V2.FromAngle(ha + Mathf.Sign(span) * MathX.Pi / 2f);
            Circle(head, w * 0.16f, BlbHi.WithAlpha(0.8f * al), 8);
            return true;
        }

        /// Drops flung off a moving point: each leaves at its own moment along `way` (spread out a little) and slows; `at(t)` is
        /// where the point was at life fraction t, `wayAt(t)` its heading then.
        delegate void BlbAt(float t, out V2 pos, out V2 way);

        void BlbFling(float seed, float p, float life, float from, float to, int n, float speed, BlbAt at)
        {
            if (fxReduced)
                n /= 2;
            for (int k = 0; k < n; k++)
            {
                float born = from + (to - from) * BlbHash(seed + k * 1.7f), tau = (p - born) * life;
                if (tau <= 0f || tau > 0.45f)
                    continue;
                V2 pos, way;
                at(born, out pos, out way);
                float spread = (BlbHash(seed + k * 3.1f) - 0.5f) * 0.9f, v = speed * (0.4f + 0.6f * BlbHash(seed + k * 5.9f));
                V2 dir = V2.FromAngle(way.Angle + spread);
                float moved = v / 5f * (1f - Mathf.Exp(-5f * tau));
                float sz = 1.4f + 1.8f * BlbHash(seed + k * 7.3f), al = 1f - tau / 0.45f;
                DrawDrop(pos + dir * moved, sz, dir, BlbCore.WithAlpha(al));
            }
        }

        /// A barbed spear of blood from `o` along `a`, L long and w wide at the base.
        void BlbSpear(V2 o, float a, float L, float w, float al)
        {
            if (L < 3f || al <= 0f)
                return;
            V2.Axes(a, out V2 d, out V2 n);
            Rgba[] cols = { BlbDark.WithAlpha(0.92f * al), BlbCore.WithAlpha(al), BlbEdge.WithAlpha(al) };
            float[] sc = { 1f, 0.55f, 0.18f };
            for (int k = 0; k < 3; k++)
            {
                float s = sc[k];
                V2 b0 = o + d * 6f + n * (w / 2f * s), b1 = o + d * (L * 0.78f) + n * (w * 0.36f * s), tip = o + d * L;
                V2 c1 = o + d * (L * 0.78f) - n * (w * 0.36f * s), c0 = o + d * 6f - n * (w / 2f * s);
                SetColor(cols[k]);
                Quad(b0, b1, c1, c0);
                Tri(b1, tip, c1);
            }
            SetColor(BlbDark.WithAlpha(0.92f * al));
            for (float u = L * 0.3f; u < L * 0.74f; u += 20f)
                for (int sg = -1; sg <= 1; sg += 2)
                    Tri(o + d * u + n * (sg * w * 0.4f), o + d * (u - 11f) + n * (sg * w * 1.05f), o + d * (u - 4f) + n * (sg * w * 0.3f));
        }

        /// A ring of blood round `c`.
        void BlbRing(V2 c, float r, float th, float al) =>
            BlbBlood(c, (float u, out float a, out float rr, out float t) => { a = u * MathX.Tau; rr = r; t = th; }, al, 40);

        /// A symmetric crescent: `span` radians wide round `mid`, th thick at its middle, tapering to points.
        void BlbCrescent(V2 c, float mid, float span, float r, float th, float al) =>
            BlbBlood(c, (float u, out float a, out float rr, out float t) => { a = mid + (u - 0.5f) * span; rr = r; t = th * Mathf.Sin(Mathf.PI * u); }, al);

        Player BlbOwner(int id)
        {
            Player p;
            return game.Players.TryGetValue(id, out p) ? p : null;
        }

        /// FxKind.BlastCut: one step of the chain, from its age.
        void DrawBlastCut(Effect fx)
        {
            int code = Mathf.RoundToInt(fx.Radius), id = code / 10, step = Mathf.Clamp(code % 10, 1, Game.BlastSteps);
            var owner = BlbOwner(id);
            V2 c = step != 6 && owner != null ? owner.Disp : fx.Pos;
            float aim = (fx.Pos2 - fx.Pos).Angle, k = fx.Pos2.DistanceTo(fx.Pos) / Game.BlastReach[step - 1];
            float life = Mathf.Max(0.05f, fx.Dur - BlbTail), p = Mathf.Clamp01(fx.T / life), seed = fx.Pos.X * 0.37f + fx.Pos.Y * 1.13f + code;
            const float D = MathX.Pi / 180f;
            V2 head, way;
            switch (step)
            {
                case 1:
                case 2:
                case 4:
                {
                    float a0 = step == 1 ? -60f : step == 2 ? 65f : -180f, a1 = step == 1 ? 60f : step == 2 ? -65f : 190f;
                    float r = (step == 1 ? 115f : step == 2 ? 108f : 94f) * k, w = (step == 1 ? 30f : step == 2 ? 28f : 26f) * k;
                    float sw = step == 4 ? 0.62f : 0.55f, tf = step == 4 ? 0.55f : 0.6f;
                    BlbSweep(c, aim, p, a0 * D, a1 * D, r, w, sw, tf, out head, out way);
                    float A0 = a0 * D, A1 = a1 * D;
                    BlbFling(seed, p, life, 0.05f, sw, step == 4 ? 16 : 10, 200f, (float t, out V2 pos, out V2 wy) =>
                    {
                        float ha = aim + A0 + (A1 - A0) * BlbEaseOut(t / sw);
                        pos = c + V2.FromAngle(ha) * (r - w * 0.4f);
                        wy = V2.FromAngle(ha + Mathf.Sign(A1 - A0) * MathX.Pi / 2f);
                    });
                    break;
                }
                case 3:  // Thorn Flurry: three short sweeps, alternating
                {
                    float r = 84f * k, w = 18f * k;
                    for (int i = 0; i < 3; i++)
                    {
                        float s0 = 0.3f * i, a0 = (i == 1 ? 45f : -45f) * D, a1 = -a0, off = (i == 0 ? -12f : i == 1 ? 10f : 0f) * D;
                        float sp = (p - s0) / 0.4f;
                        BlbSweep(c, aim + off, sp, a0, a1, r, w, 0.5f, 0.6f, out head, out way);
                        float lo = aim + off;
                        BlbFling(seed + i * 11f, sp, life * 0.4f, 0.05f, 0.5f, 5, 170f, (float t, out V2 pos, out V2 wy) =>
                        {
                            float ha = lo + a0 + (a1 - a0) * BlbEaseOut(t / 0.5f);
                            pos = c + V2.FromAngle(ha) * (r - w * 0.4f);
                            wy = V2.FromAngle(ha + Mathf.Sign(a1 - a0) * MathX.Pi / 2f);
                        });
                    }
                    break;
                }
                case 5:  // Sanguine Cleave: raised behind, flipped over, slammed down
                {
                    V2 hit = c + V2.FromAngle(aim) * (105f * k);
                    if (p < 0.42f)
                    {
                        float q = p / 0.42f;
                        BlbSpear(c, aim + MathX.Pi + Mathf.Lerp(-0.3f, 0.2f, q), Mathf.Lerp(10f, 72f, BlbEaseOut(q)) * k, 16f * k, 1f);
                        Circle(c, 14f + 6f * q, BlbCore.WithAlpha(0.18f * q), 16);  // blood pooling to the blade
                    }
                    else if (p < 0.5f)
                    {
                        float q = (p - 0.42f) / 0.08f;
                        BlbSpear(c, aim + (MathX.Pi + 0.2f) * (1f - BlbEaseIn(q)), Mathf.Lerp(72f, 108f, q) * k, 18f * k, 1f);
                        Arc(c, 90f * k, 10f * k, BlbCore.WithAlpha(0.35f * (1f - q)), 20, aim, (MathX.Pi + 0.2f) * (1f - q));  // the swing's blur
                    }
                    else
                    {
                        float q = (p - 0.5f) / 0.5f;
                        BlbSpear(c, aim, 108f * k * (1f - BlbEaseIn(q) * 0.7f), 18f * k, 1f - q);
                        Circle(hit, (40f + 40f * q) * k, BlbDark.WithAlpha(0.35f * (1f - q)), 24);  // the splash on the floor
                        BlbRing(hit, (14f + 70f * BlbEaseOut(q)) * k, (16f * (1f - q) + 2f) * k, 1f - q);
                        for (int i = 0; i < 10; i++)
                        {
                            float a = i * MathX.Tau / 10f + 0.3f + BlbHash(seed + i) * 0.3f, r0 = (18f + 50f * q) * k, r1 = r0 + 18f * (1f - q) * k;
                            Line(hit + V2.FromAngle(a) * r0, hit + V2.FromAngle(a) * r1, 2f, BlbEdge.WithAlpha(0.8f * (1f - q)));
                        }
                        BlbFling(seed, p, life, 0.5f, 0.56f, 22, 280f, (float t, out V2 pos, out V2 wy) =>
                        {
                            pos = hit;
                            wy = V2.FromAngle(BlbHash(seed + t * 97f) * MathX.Tau);
                        });
                    }
                    break;
                }
                default:  // Crescent Flight: flung out, cutting all it passes
                {
                    float r = (36f + 250f * BlbEaseOut(p)) * k, th = 28f * k * (1f - 0.3f * p), al = p < 0.8f ? 1f : 1f - (p - 0.8f) / 0.2f;
                    if (p < 0.25f)
                        BlbSweep(c, aim, p / 0.25f, -0.7f, 0.7f, 40f * k, 18f * k, 0.6f, 0.6f, out head, out way);  // the throwing cut
                    BlbCrescent(c, aim, 80f * D, r, th, al);
                    BlbCrescent(c, aim, 70f * D, r * 0.86f, th * 0.6f, al * 0.3f);  // its wake
                    BlbFling(seed, p, life, 0.05f, 0.8f, 14, 120f, (float t, out V2 pos, out V2 wy) =>
                    {
                        float rr = (36f + 250f * BlbEaseOut(t)) * k;
                        pos = c + V2.FromAngle(aim + (BlbHash(seed + t * 31f) - 0.5f) * 70f * D) * (rr - 10f * k);
                        wy = V2.FromAngle(aim + MathX.Pi);
                    });
                    break;
                }
            }
        }

        /// FxKind.BlastTide: the Crimson Tide gathering, or rolling out.
        void DrawBlastTide(Effect fx)
        {
            int code = Mathf.RoundToInt(fx.Radius), id = code / 10, phase = code % 10;
            var owner = BlbOwner(id);
            float seed = fx.Pos.X * 0.53f + fx.Pos.Y * 0.71f;
            if (phase == 1)
            {
                if (owner == null || owner.Downed)
                    return;
                V2 c = owner.Disp;
                float aim = owner.Aim, q = Mathf.Clamp01(fx.T / Mathf.Max(0.05f, fx.Dur)), j = Mathf.Sin(Time.unscaledTime * 60f) * 1.5f;
                BlbCrescent(c, aim, (60f + 60f * q) * MathX.Pi / 180f, 50f + j, 8f + 26f * q, 0.6f + 0.4f * q);
                GlowDisc(c + V2.FromAngle(aim) * 40f, 30f + 20f * q, BlbCore, 0.25f * q);
                for (int k = 0; k < 12; k++)  // drops drawn in to it
                {
                    float f = Mathf.Repeat(q * 2.2f + BlbHash(seed + k), 1f), a = aim + (BlbHash(seed + k * 2.3f) - 0.5f) * 2.6f, rr = Mathf.Lerp(120f, 46f, BlbEaseIn(f));
                    V2 at = c + V2.FromAngle(a) * rr;
                    DrawDrop(at, 2f + 1.6f * f, (c - at).Normalized(), BlbCore.WithAlpha(0.9f * f));
                }
                Arc(c, owner.Def.BodyR + 6f, 2f, BlbEdge.WithAlpha(0.5f * q), 24);
                return;
            }
            float life = Mathf.Max(0.05f, fx.Dur - BlbTail), p = Mathf.Clamp01(fx.T / life), reach = fx.Pos2.DistanceTo(fx.Pos), ang = (fx.Pos2 - fx.Pos).Angle;
            float R = Game.TideRadius(fx.T, reach), thick = 46f * (1f - 0.35f * p) * reach / Game.TideR1, al = p < 0.75f ? 1f : 1f - (p - 0.75f) / 0.25f;
            float span = 100f * MathX.Pi / 180f;
            BlbCrescent(fx.Pos, ang, span, R * 0.82f, thick * 0.7f, al * 0.3f);
            BlbCrescent(fx.Pos, ang, span * 0.92f, R * 0.66f, thick * 0.45f, al * 0.15f);
            BlbCrescent(fx.Pos, ang, span, R, thick, al);
            BlbFling(seed, p, life, 0f, 0.85f, 26, 160f, (float t, out V2 pos, out V2 wy) =>
            {
                float rr = Game.TideRadius(t * life, reach);
                pos = fx.Pos + V2.FromAngle(ang + (BlbHash(seed + t * 53f) - 0.5f) * span * 0.9f) * (rr - 12f);
                wy = V2.FromAngle(ang + MathX.Pi);
            });
        }

        /// FxKind.BlastReturn: the Blood Vortex.
        void DrawBlastReturn(Effect fx)
        {
            var owner = BlbOwner((int)fx.Pos2.X);
            int n = Mathf.Clamp((int)fx.Pos2.Y, 1, 20) + 4;
            float q = Mathf.Clamp01(fx.T / Mathf.Max(0.05f, fx.Dur)), seed = fx.Pos.X * 0.29f + fx.Pos.Y * 0.61f;
            V2 to = owner != null ? owner.Disp : fx.Pos, from = fx.Pos;
            float orbR = 4f + (n - 4) * 0.8f;
            if (q < 0.25f)
                Circle(from, (fx.Radius + 4f) * (1f - q * 3f), BlbDark.WithAlpha(0.6f * (1f - q * 4f)), 18);  // the body's blood pooling up
            V2 orb;
            if (q < 0.38f)
            {
                float g = q / 0.38f;
                orb = from;
                orbR *= BlbEaseOut(g);
                for (int i = 0; i < n; i++)
                {
                    float a = seed + i * MathX.Tau / n + g * 9f, rr = 42f * (1f - BlbEaseIn(g)) + 5f;
                    V2 at = from + new V2(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr * 0.8f);
                    DrawDrop(at, 3.2f, new V2(-Mathf.Sin(a), Mathf.Cos(a)), BlbDark);
                    DrawDrop(at, 2.4f, new V2(-Mathf.Sin(a), Mathf.Cos(a)), BlbCore);
                }
            }
            else if (q < 0.84f)
            {
                float g = BlbEaseIn((q - 0.38f) / 0.46f);
                orb = from + (to - from) * g;
                orbR *= 1f - g * 0.4f;
                V2 d = to - from, nrm = d.IsZero ? new V2(0f, 1f) : d.Normalized().Orthogonal();
                for (int i = 0; i < n; i++)  // the helix trailing it
                {
                    float gg = Mathf.Max(0f, g - i * 0.035f), w = Mathf.Sin(Time.unscaledTime * 16f + i * 1.3f) * 9f;
                    V2 at = from + d * gg + nrm * w;
                    DrawDrop(at, 2.6f, d.IsZero ? new V2(1f, 0f) : d.Normalized(), BlbCore.WithAlpha(1f - (float)i / n * 0.6f));
                }
            }
            else
            {
                orb = to;
                orbR = 0f;
                float a = 1f - (q - 0.84f) / 0.16f;
                float br = (owner != null ? owner.Def.BodyR : 14f) + 26f * (1f - a);
                Arc(to, br, 3f * a, BlbEdge.WithAlpha(a), 28);  // the absorb ring
                Circle(to, br * 0.8f, BlbCore.WithAlpha(0.3f * a), 20);
            }
            if (orbR > 0.5f)
            {
                Circle(orb, orbR * 2.6f, BlbCore.WithAlpha(0.18f), 16);
                Circle(orb, orbR, BlbDark, 14);
                Circle(orb, orbR * 0.7f, BlbCore, 12);
                Circle(orb + new V2(-orbR * 0.25f, -orbR * 0.3f), orbR * 0.25f, BlbHi.WithAlpha(0.9f), 8);
            }
        }

        /// An enemy's Blood Blast stacks: a drop each, circling it; they swell a little as they build.
        void DrawBlastStacks(Enemy e, V2 pos, float r, float t)
        {
            var owner = BlbOwner(e.BlastOwner);
            int cap = owner != null ? Game.BlastStackCap(owner) : Game.BlastStackMax, n = Mathf.Min(e.BlastStacks, cap);
            float f = n / (float)cap, ring = r + 9f + (cap > Game.BlastStackMax ? 4f : 0f), sz = 2.6f + 1.2f * f;
            for (int i = 0; i < n; i++)
            {
                V2 d = V2.FromAngle(-t * 1.8f + MathX.Tau * i / cap + e.Id * 0.7f);
                V2 at = pos + new V2(d.X * ring, d.Y * ring * 0.9f);
                DrawDrop(at, sz + 1.1f, d.Orthogonal(), BlbDark);
                DrawDrop(at, sz, d.Orthogonal(), BlbCore);
            }
            if (n >= cap)
                Arc(pos, ring, 1.5f, BlbEdge.WithAlpha(0.35f + 0.2f * Mathf.Sin(t * 6f)), 24);  // full
        }

        /// The glow pass: a soft light on each cut's head, the Tide's crest and the returning orb.
        void DrawBloodBlastGlow()
        {
            foreach (var fx in game.Effects)
            {
                if (fx.Kind == FxKind.BlastTide && Mathf.RoundToInt(fx.Radius) % 10 == 2)
                {
                    float R = Game.TideRadius(fx.T, fx.Pos2.DistanceTo(fx.Pos)), ang = (fx.Pos2 - fx.Pos).Angle, al = 1f - Mathf.Clamp01(fx.T / fx.Dur);
                    for (int k = 0; k < 5; k++)
                        GlowDisc(fx.Pos + V2.FromAngle(ang + (k - 2f) * 0.3f) * R, 26f, BlbCore, 0.2f * al);
                }
                else if (fx.Kind == FxKind.BlastReturn)
                {
                    float q = fx.T / Mathf.Max(0.05f, fx.Dur);
                    var owner = BlbOwner((int)fx.Pos2.X);
                    if (q > 0.38f && q < 0.84f && owner != null)
                        GlowDisc(fx.Pos + (owner.Disp - fx.Pos) * BlbEaseIn((q - 0.38f) / 0.46f), 26f, BlbCore, 0.4f);
                    else if (q <= 0.38f)
                        GlowDisc(fx.Pos, 30f * q / 0.38f, BlbCore, 0.35f);
                    else if (q >= 0.84f && owner != null)
                        GlowDisc(owner.Disp, 34f, BlbCore, 0.35f * (1f - (q - 0.84f) / 0.16f));
                }
                else if (fx.Kind == FxKind.BlastCut && Mathf.RoundToInt(fx.Radius) % 10 == 5)
                {
                    float life = Mathf.Max(0.05f, fx.Dur - BlbTail), p = fx.T / life;
                    if (p >= 0.5f && p < 0.8f)
                        GlowDisc(fx.Pos2, 46f, BlbCore, 0.3f * (1f - (p - 0.5f) / 0.3f));
                }
            }
        }
    }
}
