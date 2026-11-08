using UnityEngine;

namespace Arena
{
    /// The Lightbringer's light, ported from the chosen looks in Docs/Mockups/lightbringer-looks.html (same shapes, sizes, timings
    /// and colours; the mockup's additive layers are drawn in the additive glow pass, DrawLightGlow, or in the ordinary pass when
    /// Visual effects are off):
    ///   - Noon Sunburst, "Sundial" (DrawSundial): a ring with two echoes, a turning circle of glyphs that draws back into the
    ///     blade, shards riding out with the ring, a pillar, a glint and cracks; the Radiant one adds a green ring;
    ///   - Dawn and Mercy crescents, "Sunrise" (DrawSunriseCrescent, on a player's sword wave whose Look marks it the Lightbringer's):
    ///     a gold crescent with three afterimages, green-edged and trailing motes for Dawn, doubled and trailing hexagons for Mercy;
    ///   - the Sunlance, "Spear" (DrawSpearLight): a spear forming over the off hand, then flying, a beam pinching to a thread in a
    ///     green spiral, and a halo dropping onto the ally;
    ///   - Mercy's ward, "Hex plates" (DrawMercyPlates): six gold plates with a light running round them; the plate a shot strikes
    ///     flashes white and cracks;
    ///   - FxKind.LightMark: the heal burst and the ward snapping shut on an ally (DrawLightMark), and the sparks of a struck ward.
    public partial class ArenaApp
    {
        static readonly Rgba LbHealHot = new Rgba(214f / 255f, 1f, 222f / 255f);

        // The mockup's helpers, so the numbers carry over as they are.
        static float LbRnd(float s) { double v = System.Math.Sin(s * 127.1 + 311.7) * 43758.5453; return (float)(v - System.Math.Floor(v)); }
        static float Lb01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        static float LbEo(float t) { float m = 1f - t; return 1f - m * m * m; }
        static float LbEi(float t) => t * t * t;
        static V2 LbPerp(V2 d) => new V2(-d.Y, d.X);  // a quarter turn clockwise on screen (the canvas's +y side)

        /// A disc that fades from `inner` at its centre to `outer` at its edge (a two-stop radial gradient).
        void LbRadial(V2 c, float r, Rgba inner, Rgba outer, int segs = 40)
        {
            if (r <= 0f)
                return;
            V2 prev = c + new V2(r, 0f);
            for (int i = 1; i <= segs; i++)
            {
                V2 next = c + V2.FromAngle(MathX.Tau * i / segs) * r;
                VTri(c, inner, prev, outer, next, outer);
                prev = next;
            }
        }

        /// The outline of an ellipse, `w` wide.
        void LbEllipse(V2 c, float rx, float ry, float w, Rgba col, int segs = 32)
        {
            SetColor(col);
            for (int i = 0; i < segs; i++)
            {
                float a0 = MathX.Tau * i / segs, a1 = MathX.Tau * (i + 1) / segs;
                V2 o0 = c + new V2(Mathf.Cos(a0) * (rx + w / 2f), Mathf.Sin(a0) * (ry + w / 2f)), o1 = c + new V2(Mathf.Cos(a1) * (rx + w / 2f), Mathf.Sin(a1) * (ry + w / 2f));
                V2 i0 = c + new V2(Mathf.Cos(a0) * Mathf.Max(0f, rx - w / 2f), Mathf.Sin(a0) * Mathf.Max(0f, ry - w / 2f)), i1 = c + new V2(Mathf.Cos(a1) * Mathf.Max(0f, rx - w / 2f), Mathf.Sin(a1) * Mathf.Max(0f, ry - w / 2f));
                Quad(o0, o1, i1, i0);
            }
        }

        /// A filled regular polygon with no outline.
        void LbNgon(V2 c, float r, int n, float rot, Rgba col)
        {
            SetColor(col);
            V2 prev = c + V2.FromAngle(rot) * r;
            for (int i = 1; i <= n; i++)
            {
                V2 next = c + V2.FromAngle(rot + MathX.Tau * i / n) * r;
                Tri(c, prev, next);
                prev = next;
            }
        }

        /// Everything of the Lightbringer that glows, from DrawGlowPass (Juice.cs).
        void DrawLightGlow()
        {
            foreach (var fx in game.Effects)
            {
                if (fx.T >= fx.Dur)
                    continue;
                if (fx.Kind == FxKind.LightStep)
                {
                    int kind = Mathf.RoundToInt(fx.Radius) % 10;
                    if (kind == 3 || kind == 4)
                        DrawSundial(fx, kind == 4, true);
                }
                else if (fx.Kind == FxKind.Sunlance)
                    DrawSpearLight(fx);
                else if (fx.Kind == FxKind.LightMark)
                    DrawLightMark(fx);
            }
            foreach (var p in game.Players.Values)
                DrawMercyPlates(p, Time.time);
        }

        // --- Noon: the Sundial ------------------------------------------------------------------------------------------

        /// Noon's ring from its step's age (q seconds after the blow). `light`: the additive layers (everything but the cracks).
        void DrawSundial(Effect fx, bool radiant, bool light)
        {
            var s = LightbringerMotion.Steps[2];
            float q = fx.T - s.Dur * LightbringerMotion.NoonHit;
            if (q < 0f || q > 1.05f)
                return;
            V2 dir = (fx.Pos2 - fx.Pos).Normalized(), at = fx.Pos + dir * Game.NoonAt;
            float R = (fx.Pos2 - fx.Pos).Length;
            if (!light)
            {
                LbCracks(at, Lb01(1f - q / 1.05f));
                return;
            }
            float fade = Lb01(1f - (q - 0.45f) / 0.6f);
            LbRadial(at, R * 0.8f, LbLight.WithAlpha(0.22f * fade), LbLight.WithAlpha(0f));
            for (int i = 0; i < 3; i++)  // the ring and its two echoes
            {
                float d = i * 0.06f, u = (q - d) / 0.32f;
                if (u < 0f)
                    continue;
                float r = R * LbEo(Lb01(u)) * (1f - i * 0.14f), al = Lb01(1f - (q - d) / 0.6f) * (i > 0 ? 0.45f : 1f);
                Arc(at, r, (i > 0 ? 3f : 9f) * al + 1f, LbLight.WithAlpha(0.85f * al), 72);
                if (i == 0)
                    Arc(at, r * 0.97f, 1.5f, new Rgba(1f, 1f, 1f, al), 72);
            }
            if (radiant)
            {
                float u = Lb01((q - 0.04f) / 0.36f), al = Lb01(1f - q / 0.8f);
                Arc(at, R * 0.88f * LbEo(u), 3f, LbHeal.WithAlpha(0.6f * al), 72);
            }
            // the dial: grows in, turns, then draws back into the blade
            float sc = LbEo(Lb01(q / 0.15f)) * (q > 0.6f ? 1f - LbEi(Lb01((q - 0.6f) / 0.4f)) : 1f), dr = R * 0.48f * sc, dal = Lb01(q / 0.08f) * Lb01(1f - (q - 0.75f) / 0.3f);
            if (dr > 2f)
            {
                float rot = q * 1.6f;
                Arc(at, dr, 1.5f, LbLight.WithAlpha(0.7f * dal), 48);
                Arc(at, dr * 0.82f, 1.5f, LbLight.WithAlpha(0.7f * dal), 48);
                for (int i = 0; i < 24; i++)
                {
                    V2 d = V2.FromAngle(i / 24f * MathX.Tau + rot);
                    float l = i % 3 != 0 ? 4f : 9f;
                    Line(at + d * (dr * 0.82f), at + d * (dr * 0.82f + l), i % 3 != 0 ? 1f : 2f, LbHot.WithAlpha(0.8f * dal));
                }
                for (int i = 0; i < 4; i++)
                {
                    float a = i / 4f * MathX.Tau - rot * 1.4f;
                    V2 c = at + V2.FromAngle(a) * (dr * 1.12f), ux = V2.FromAngle(a) * 3f, uy = LbPerp(V2.FromAngle(a)) * 3f;
                    SetColor(LbHot.WithAlpha(0.9f * dal));
                    Quad(c - ux - uy, c + ux - uy, c + ux + uy, c - ux + uy);
                }
            }
            int n = radiant ? 22 : 16;  // shards riding out with the ring
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * MathX.Tau + LbRnd(i) * 0.2f, u = Lb01(q / 0.4f), r = R * (0.35f + 0.75f * LbEo(u)) * (0.85f + 0.15f * LbRnd(i + 4)), al = Lb01(1f - q / 0.55f);
                V2 d = V2.FromAngle(a);
                Line(at + d * (r - 12f), at + d * r, 1.8f, LbHot.WithAlpha(al));
            }
            if (q < 0.12f)
                Circle(at, R * 0.5f, new Rgba(1f, 1f, 1f, 0.35f * (1f - q / 0.12f)), 40);
            LbPillar(at, Lb01(1f - q / 0.4f), radiant ? 12f : 8f, 110f);
            LbGlint(at, 26f * Lb01(1f - q / 0.25f) + (q > 0.75f && q < 1f ? 10f * Mathf.Sin((q - 0.75f) / 0.25f * MathX.Pi) : 0f), q < 0.25f ? 1f - q / 0.25f : 0.8f);
        }

        /// A column of light standing on `at`, `h` tall, fading upward (f 0..1 its strength and width).
        void LbPillar(V2 at, float f, float w, float h)
        {
            if (f <= 0f)
                return;
            Rgba top = LbLight.WithAlpha(0f), bottom = LbHot.WithAlpha(0.7f * f);
            VQuad(at + new V2(-w * f, -h), top, at + new V2(w * f, -h), top, at + new V2(w * f, 0f), bottom, at + new V2(-w * f, 0f), bottom);
        }

        /// A four-pointed glint of size `s`.
        void LbGlint(V2 at, float s, float al)
        {
            if (s <= 0f || al <= 0f)
                return;
            SetColor(new Rgba(1f, 1f, 1f, al));
            Quad(at - new V2(s, 0f), at + new V2(0f, s * 0.12f), at + new V2(s, 0f), at - new V2(0f, s * 0.12f));
            Quad(at - new V2(0f, s), at + new V2(s * 0.12f, 0f), at + new V2(0f, s), at - new V2(s * 0.12f, 0f));
        }

        /// Cracks in the floor where the blade went in (dark, with a thin gold light in them).
        void LbCracks(V2 at, float al)
        {
            if (al <= 0f)
                return;
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < 9; i++)
                {
                    float a = i / 9f * MathX.Tau + LbRnd(i) * 0.5f, l = 22f + LbRnd(i + 3) * 26f;
                    V2 m = at + new V2(Mathf.Cos(a) * l * 0.5f + (LbRnd(i + 7) - 0.5f) * 6f, Mathf.Sin(a) * l * 0.5f), e = at + V2.FromAngle(a + 0.1f) * l;
                    Rgba col = pass == 0 ? new Rgba(16f / 255f, 20f / 255f, 28f / 255f, 0.85f * al) : LbLight.WithAlpha(0.35f * al);
                    float w = pass == 0 ? 2f : 1f;
                    Line(at, m, w, col);
                    Line(m, e, w, col);
                }
        }

        // --- Dawn and Mercy: Sunrise crescents -----------------------------------------------------------------------------

        /// A Lightbringer crescent: a player's sword wave whose Look the host set (Dart Dawn, Crescent Mercy; Lightbringer.cs).
        static bool IsLightCrescent(Bullet b) => b.Kind == Defs.KnightWave && b.Owner != 0 && b.Look != ShotLook.Round;

        void DrawSunriseCrescent(Bullet b)
        {
            bool ward = b.Look == ShotLook.Crescent;
            float life0 = b.Life0 > 0f ? b.Life0 : 0.45f, age = Mathf.Max(0f, life0 - b.Life), al = Lb01(1f - (age - 0.3f) / 0.15f);
            float ang = b.Vel.Angle;
            V2 dir = V2.FromAngle(ang), perp = LbPerp(dir);
            for (int g = 3; g >= 0; g--)  // three afterimages behind it, then the crescent
            {
                float back = g * 11f, ga = g > 0 ? 0.22f * (1f - g / 4f) : 1f, R = 20f + age * 20f;
                V2 o = b.Pos - dir * back, c1 = o - dir * (R * 0.6f), c2 = o - dir * (R * 0.6f + 9f);
                Rgba fill = (ward ? LbLight : LbHeal).WithAlpha(0.35f * al * ga);
                SetColor(fill);
                const int N = 14;
                for (int k = 0; k < N; k++)
                {
                    float t0 = -1.05f + 2.1f * k / N, t1 = -1.05f + 2.1f * (k + 1) / N;
                    Quad(c1 + V2.FromAngle(ang + t0) * R, c1 + V2.FromAngle(ang + t1) * R, c2 + V2.FromAngle(ang + t1) * (R - 4f), c2 + V2.FromAngle(ang + t0) * (R - 4f));
                }
                Arc(c1, R, g > 0 ? 1.2f : 2.6f, LbHot.WithAlpha(al * ga), 16, ang - 1.05f, 2.1f);
                if (ward && g == 0)
                    Arc(o - dir * (R * 0.6f + 8f), R, 1.6f, LbLight.WithAlpha(0.85f * al), 16, ang - 0.95f, 1.9f);
            }
            for (int i = 0; i < 10; i++)  // the stream behind it: motes for Dawn, tiny hexagons for Mercy
            {
                float k = Mathf.Repeat(i / 10f + age * 3f, 1f), d = k * 60f, off = (LbRnd(i) - 0.5f) * 22f * (1f - k);
                V2 at = b.Pos - dir * d + perp * off;
                Rgba col = (ward ? LbHot : LbHealHot).WithAlpha(al * (1f - k));
                if (ward)
                    LbNgon(at, 2.4f, 6, age * 6f + i, col);
                else
                    LbNgon(at, 1.6f, 10, 0f, col);
            }
        }

        // --- The Sunlance: the Spear ---------------------------------------------------------------------------------------

        /// The spear forming over the off hand, then flying to the ally, and the halo it drops on them (all light).
        void DrawSpearLight(Effect fx)
        {
            Player owner, ally;
            V2 o, to;
            float aim;
            TwinBladesMotion.Pose m;
            if (!SunlanceFrame(fx, out owner, out ally, out o, out to, out aim, out m))
                return;
            float c = fx.T, release = LightbringerMotion.LanceRelease;
            if (c < release && c < LightbringerMotion.Lance.Dur)  // the spear forming over the off hand
            {
                float k = Lb01(c / release);
                V2 hl, dl;
                TwinHand(o, aim, m, false, out hl, out dl);
                V2 bse = hl + new V2(0f, -18f), da = V2.FromAngle(aim), pa = LbPerp(da), tip = bse + da * (22f * k);
                Rgba col = LbHot.WithAlpha(0.8f * k);
                Line(bse - da * (22f * k), tip, 1.5f, col);
                Line(tip, bse + da * (14f * k) - pa * (4f * k), 1.5f, col);
                Line(tip, bse + da * (14f * k) + pa * (4f * k), 1.5f, col);
            }
            float q = c - release;
            if (q < 0f || q > 0.7f)
                return;
            V2 hand, bdir;
            TwinHand(o, aim, m, true, out hand, out bdir);
            V2 from = hand + bdir * (5f + LightbringerMotion.BladeLen), dv = to - from;
            float L = dv.Length;
            if (L < 1f)
                return;
            V2 dir = dv / L, n = LbPerp(dir);
            float head = L * LbEo(Lb01(q / 0.12f));
            float bw = q < 0.12f ? 7f : 7f * Lb01(1f - (q - 0.12f) / 0.35f), bal = Lb01(1f - (q - 0.2f) / 0.4f);
            V2 hp = from + dir * head;
            if (bw > 0.2f)  // the beam it leaves
                VQuad(from - n * bw, LbLight.WithAlpha(0.15f * bal), hp - n * bw, LbHot.WithAlpha(0.6f * bal), hp + n * bw, LbHot.WithAlpha(0.6f * bal), from + n * bw, LbLight.WithAlpha(0.15f * bal));
            Line(from, hp, 1.2f, new Rgba(1f, 1f, 1f, 0.9f * bal));
            V2 prev = from;
            for (int s = 0; s <= 60; s++)  // the green spiral round it
            {
                float u = s / 60f * head, y = Mathf.Sin(u * 0.09f - q * 30f) * (6f + 3f * bal);
                V2 pt = from + dir * u + n * y;
                if (s > 0)
                    Line(prev, pt, 1.4f, LbHeal.WithAlpha(0.75f * bal));
                prev = pt;
            }
            if (q < 0.16f)  // the spearhead and its glow
            {
                float hal = q < 0.12f ? 1f : 1f - (q - 0.12f) / 0.04f;
                V2 p0 = hp + dir * 16f, p1 = hp - dir * 6f - n * 6f, p2 = hp - dir * 2f, p3 = hp - dir * 6f + n * 6f;
                SetColor(new Rgba(1f, 1f, 1f, hal));
                Tri(p0, p1, p2);
                Tri(p0, p2, p3);
                LbRadial(hp, 22f, LbHot.WithAlpha(0.7f * hal), LbLight.WithAlpha(0f), 24);
            }
            float hq = q - 0.12f;
            if (hq > 0f && hq < 0.55f)  // a halo drops onto the ally
            {
                float u = LbEo(Lb01(hq / 0.3f)), al = Lb01(1f - (hq - 0.25f) / 0.3f);
                LbEllipse(to + new V2(0f, -40f * (1f - u) - 22f), 14f, 5f, 3f, LbLight.WithAlpha(0.9f * al));
                Arc(to, 18f + 50f * u, 2f, LbHeal.WithAlpha(0.8f * al), 40);
                LbRadial(to, 30f, new Rgba(1f, 1f, 1f, 0.5f * al), LbHeal.WithAlpha(0f), 24);
            }
        }

        /// Where a Sunlance's knight and ally are, its aim and the pose now; false when a newer move of theirs has taken over.
        bool SunlanceFrame(Effect fx, out Player owner, out Player ally, out V2 o, out V2 to, out float aim, out TwinBladesMotion.Pose m)
        {
            int code = Mathf.RoundToInt(fx.Radius), id = code / 1000, target = code % 1000;
            game.Players.TryGetValue(id, out owner);
            game.Players.TryGetValue(target, out ally);
            o = owner != null ? owner.Disp : fx.Pos;
            to = ally != null ? ally.Disp : fx.Pos2;
            aim = (to - o).Angle;
            m = LightbringerMotion.At(LightbringerMotion.Lance, fx.T);
            return true;  // (its light plays out even if the knight has moved on to another move)
        }

        // --- Mercy's ward: Hex plates ----------------------------------------------------------------------------------------

        /// The newest strike on this player's ward (FxKind.LightMark kind 3), or null.
        Effect MercyHitOf(int id)
        {
            Effect best = null;
            foreach (var fx in game.Effects)
                if (fx.Kind == FxKind.LightMark && fx.T < fx.Dur && Mathf.RoundToInt(fx.Radius) == id && Mathf.RoundToInt(fx.Pos2.X) == 3 && (best == null || fx.T < best.T))
                    best = fx;
            return best;
        }

        /// Six gold plates round a warded player, a light running round them; the plate a shot strikes flashes white and cracks.
        void DrawMercyPlates(Player p, float t)
        {
            if (p.MercyT <= 0f || p.Downed)
                return;
            float since = Game.MercyTime - p.MercyT, fade = Mathf.Min(Lb01(since / 0.15f), Lb01(p.MercyT / 0.2f));
            float r = p.Def.BodyR + 10f, rot = t * 0.35f;
            int lit = Mathf.FloorToInt(t * 6f) % 6;
            var hit = MercyHitOf(p.Id);
            int hitPlate = -1;
            float hitK = 0f;
            if (hit != null && hit.T < 0.35f)
            {
                hitK = 1f - hit.T / 0.35f;
                hitPlate = Mathf.FloorToInt(Mathf.Repeat(hit.Pos2.Y - rot, MathX.Tau) / (MathX.Tau / 6f)) % 6;
            }
            V2 a = p.Disp;
            for (int i = 0; i < 6; i++)
            {
                float a0 = i / 6f * MathX.Tau + rot, a1 = a0 + MathX.Tau / 6f, gap = 0.06f, hk = i == hitPlate ? hitK : 0f;
                V2 p0 = a + V2.FromAngle(a0 + gap) * r, p1 = a + V2.FromAngle(a1 - gap) * r, q0 = a + V2.FromAngle(a0 + gap) * (r - 7f), q1 = a + V2.FromAngle(a1 - gap) * (r - 7f);
                float b = (i == lit ? 0.5f : 0.22f) + hk * 0.6f;
                SetColor(hk > 0f ? new Rgba(1f, 1f, 1f, b * fade) : LbLight.WithAlpha(b * fade));
                Quad(p0, p1, q1, q0);
                Line(p0, p1, 1.2f, LbHot.WithAlpha(0.7f * fade));
                if (hk > 0f)
                {
                    V2 mid = (p0 + p1) * 0.5f;
                    for (int c = 0; c < 3; c++)
                    {
                        float an = a0 + MathX.Tau / 12f + (c - 1) * 0.5f;
                        Line(mid, mid - V2.FromAngle(an) * 6f, 1f, new Rgba(1f, 1f, 1f, hk));
                    }
                }
            }
        }

        // --- Marks on allies -------------------------------------------------------------------------------------------------

        /// FxKind.LightMark: a heal burst (1), the ward snapping shut (2), or the sparks where a shot struck the ward (3).
        void DrawLightMark(Effect fx)
        {
            int id = Mathf.RoundToInt(fx.Radius), kind = Mathf.RoundToInt(fx.Pos2.X);
            Player p;
            game.Players.TryGetValue(id, out p);
            V2 a = p != null ? p.Disp : fx.Pos;
            float bodyR = p != null ? p.Def.BodyR : 14f;
            if (kind == 3)
            {
                float h = fx.T;
                if (h >= 0.25f)
                    return;
                V2 at = a + V2.FromAngle(fx.Pos2.Y) * (bodyR + 10f);
                for (int i = 0; i < 6; i++)
                {
                    V2 d = V2.FromAngle(LbRnd(i) * MathX.Tau);
                    float rr = 4f + h * 80f;
                    Line(at + d * rr, at + d * (rr + 6f), 1.5f, LbHot.WithAlpha(1f - h / 0.25f));
                }
                return;
            }
            float q = fx.T - fx.Pos2.Y;
            if (kind == 2)
            {
                if (q < 0f || q >= 0.5f)
                    return;
                float al = 1f - q / 0.5f, r = bodyR + 7f + 12f * (1f - LbEo(Lb01(q / 0.3f)));
                V2 prev = default(V2);
                for (int i = 0; i <= 6; i++)
                {
                    V2 pt = a + V2.FromAngle(i / 6f * MathX.Tau + MathX.Pi / 6f) * r;
                    if (i > 0)
                        Line(prev, pt, 3f * al + 1f, LbLight.WithAlpha(al));
                    prev = pt;
                }
                return;
            }
            if (q < 0f || q > 0.8f)
                return;
            bool big = fx.Color.A > 0.75f;
            float fa = 1f - q / 0.8f;
            if (q < 0.25f)
                Arc(a, 16f + q * (big ? 140f : 70f), 2.5f, LbHeal.WithAlpha(0.8f * (1f - q / 0.25f)), 40);
            for (int i = 0; i < (big ? 9 : 6); i++)  // little plus signs of light rising off them
            {
                float an = LbRnd(i + id * 7) * MathX.Tau, rr = 8f + LbRnd(i + 3) * 12f;
                float yy = a.Y + Mathf.Sin(an) * rr - q * (30f + LbRnd(i + 5) * 30f), xx = a.X + Mathf.Cos(an) * rr;
                Line(new V2(xx - 2.5f, yy), new V2(xx + 2.5f, yy), 1.6f, LbHealHot.WithAlpha(fa));
                Line(new V2(xx, yy - 2.5f), new V2(xx, yy + 2.5f), 1.6f, LbHealHot.WithAlpha(fa));
            }
        }
    }
}
