using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The DPS Knight's Drawbridge (Core/Drawbridge.cs), look Oak & Iron of Docs/Mockups/knight-dps-abilities.html, ported
    /// from the mockup's drawing code. All from the zone's age:
    ///   0 s      the lane marked on the floor (dashed amber outline, chevrons marching along), two stone towers rising
    ///            behind you in a puff of dust with the lintel between them, a gold glow round you;
    ///   0.22 s   the bridge stands upright at the hinge, its long shadow thrown across the lane;
    ///   0.42 s   it tips and falls, foreshortened (the planks squashed toward the hinge), lit as it nears the ground,
    ///            its shadow sweeping in under it; iron chains run from the towers to its far corners...
    ///   0.72 s   ...and snap, links flying;
    ///   1.05 s   the slam: dust rolling out along both sides and off the far end, a bright line down each edge, sparks
    ///            off the planks, and a big screen shake;
    ///   then     oak planks with two iron bands, rivets and darker rails; shots striking a rail spark;
    ///   the end  the planks give way one by one from the far end, and the towers sink.
    /// The falling bridge is drawn over the enemies it's about to crush (DrawBridgesFalling); the rest under them.
    public partial class ArenaApp
    {
        static readonly Rgba OakWood = C255(122, 84, 50), OakWood2 = C255(94, 63, 37), OakBand = C255(58, 61, 67), OakRail = C255(84, 56, 32),
            OakDust = C255(148, 128, 102), OakTele = C255(240, 180, 70), OakGlint = C255(255, 210, 140);
        const float BridgePlank = 22f;

        readonly HashSet<int> bridgesSlammed = new HashSet<int>();

        static float BridgeTheta(float age) => age < Game.BridgeFallStart ? 1.45f : 1.45f * (1f - Mathf.Pow(Mathf.Clamp01((age - Game.BridgeFallStart) / (Game.BridgeSlam - Game.BridgeFallStart)), 2f));
        static float BridgeCrumbleAt(Zone z) => z.Duration - Game.BridgeCrumble;

        static V2 BW(Zone z, float u, float v) => Game.BridgeWorld(z, u, v);

        /// A tower of the gatehouse, seen from above: a square of stone with merlons round its top.
        void DrawBridgeTower(V2 at, float aim, float s)
        {
            if (s <= 0.01f)
                return;
            V2 u = V2.FromAngle(aim) * s, v = new V2(-u.Y, u.X);
            System.Func<float, float, V2> P = (x, y) => at + u * x + v * y;
            SetColor(new Rgba(0f, 0f, 0f, 0.45f));
            Quad(P(-13f, -13f), P(21f, -13f), P(21f, 21f), P(-13f, 21f));
            VTri(P(-16f, -16f), C255(123, 128, 137), P(16f, -16f), C255(99, 104, 112), P(16f, 16f), C255(71, 75, 82));
            VTri(P(-16f, -16f), C255(123, 128, 137), P(16f, 16f), C255(71, 75, 82), P(-16f, 16f), C255(99, 104, 112));
            SetColor(C255(46, 49, 55));
            Quad(P(-10f, -10f), P(10f, -10f), P(10f, 10f), P(-10f, 10f));
            SetColor(C255(141, 146, 155));
            for (int side = 0; side < 4; side++)
                for (int k = 0; k < 3; k++)
                {
                    float o = -16f + k * 12f;
                    V2 a0 = new V2(o, -16f).Rotated(side * MathX.Pi / 2f), a1 = new V2(o + 7f, -16f).Rotated(side * MathX.Pi / 2f), a2 = new V2(o + 7f, -11f).Rotated(side * MathX.Pi / 2f), a3 = new V2(o, -11f).Rotated(side * MathX.Pi / 2f);
                    Quad(P(a0.X, a0.Y), P(a1.X, a1.Y), P(a2.X, a2.Y), P(a3.X, a3.Y));
                }
        }

        /// The bridge's planks, iron bands, rivets and rails, squashed along its length by `cosT` while it falls; at the
        /// end each plank drops away from the far end.
        void DrawBridgeDeck(Zone z, float age, float cosT)
        {
            float hw = Game.BridgeHalfWidth, len = Game.BridgeLength, cr = BridgeCrumbleAt(z);
            int n = Mathf.CeilToInt(len / BridgePlank);
            for (int i = 0; i < n; i++)
            {
                float u0 = i * BridgePlank, u1 = Mathf.Min(len, u0 + BridgePlank), w = u1 - u0, uc = (u0 + u1) / 2f;
                float gone = 0f;
                if (age >= cr)
                    gone = Mathf.Clamp01((age - (cr + (1f - i / (float)n) * 0.55f)) / 0.3f);
                if (gone >= 1f)
                    continue;
                float drop = gone, sc = 1f - gone * 0.25f, rot = drop * (KHash(i) - 0.5f) * 0.6f, al = 1f - gone;
                System.Func<float, float, V2> P = (x, y) =>
                {
                    V2 r = new V2(x * sc, y * sc).Rotated(rot);
                    return BW(z, (uc + r.X) * cosT, r.Y + drop * 10f);
                };
                float hx = w / 2f;
                SetColor(OakWood.Mix(OakWood2, KHash(i * 3.7f) * 0.7f).WithAlpha(al));
                Quad(P(-hx + 0.6f, -hw), P(hx - 0.6f, -hw), P(hx - 0.6f, hw), P(-hx + 0.6f, hw));
                for (int k = 0; k < 3; k++)
                {
                    float yy = -hw + 8f + KHash(i + k * 5f) * (hw * 2f - 16f);
                    Line(P(-hx + 2f, yy), P(hx - 2f, yy + 1.5f), 1f, new Rgba(0f, 0f, 0f, 0.25f * al));
                }
                SetColor(OakBand.WithAlpha(al));
                Quad(P(-hx, -hw * 0.58f - 3f), P(hx, -hw * 0.58f - 3f), P(hx, -hw * 0.58f + 3f), P(-hx, -hw * 0.58f + 3f));
                Quad(P(-hx, hw * 0.58f - 3f), P(hx, hw * 0.58f - 3f), P(hx, hw * 0.58f + 3f), P(-hx, hw * 0.58f + 3f));
                Circle(P(0f, -hw * 0.58f), 1.4f * cosT, C255(154, 160, 168).WithAlpha(al), 6);
                Circle(P(0f, hw * 0.58f), 1.4f * cosT, C255(154, 160, 168).WithAlpha(al), 6);
                SetColor(OakRail.WithAlpha(al));
                Quad(P(-hx, -hw), P(hx, -hw), P(hx, -hw + 8f), P(-hx, -hw + 8f));
                Quad(P(-hx, hw - 8f), P(hx, hw - 8f), P(hx, hw), P(-hx, hw));
                SetColor(new Rgba(1f, 1f, 1f, 0.12f * al));
                Quad(P(-hx, -hw), P(hx, -hw), P(hx, -hw + 1.5f), P(-hx, -hw + 1.5f));
                Quad(P(-hx, hw - 1.5f), P(hx, hw - 1.5f), P(hx, hw), P(-hx, hw));
            }
        }

        /// One chain link, an oval ring along the chain.
        void DrawChainLink(V2 at, float ang, float rx, float ry, Rgba col, float w)
        {
            const int segs = 10;
            V2 u = V2.FromAngle(ang), v = new V2(-u.Y, u.X), prev = at + u * rx;
            for (int i = 1; i <= segs; i++)
            {
                float a = MathX.Tau * i / segs;
                V2 next = at + u * (Mathf.Cos(a) * rx) + v * (Mathf.Sin(a) * ry);
                Line(prev, next, w, col);
                prev = next;
            }
        }

        void DrawBridgeChain(V2 a, V2 b, float alpha)
        {
            float d = a.DistanceTo(b);
            if (d < 2f || alpha <= 0.01f)
                return;
            V2 dv = (b - a) / d;
            int n = Mathf.FloorToInt(d / 7f);
            for (int q = 0; q < n; q++)
                DrawChainLink(a + dv * (q * 7f + 3.5f), dv.Angle, 4.2f, q % 2 == 1 ? 2f : 2.4f, (q % 2 == 1 ? C255(144, 151, 161) : C255(108, 114, 123)).WithAlpha(alpha), 1.8f);
        }

        /// The zone (under the enemies): the lane marked, the towers, the shadow, the bridge once it's down, the slam's
        /// dust and sparks.
        void DrawDrawbridge(Zone z, float t)
        {
            float age = Game.BridgeAge(z), hw = Game.BridgeHalfWidth, len = Game.BridgeLength, cr = BridgeCrumbleAt(z);
            float th = BridgeTheta(age), cosT = Mathf.Cos(th), sinT = Mathf.Sin(th);
            if (age < Game.BridgeSlam + 0.05f)  // the lane, marked
            {
                float a = Mathf.Clamp01(age / 0.25f) * (age > Game.BridgeSlam - 0.15f ? Mathf.Clamp01((Game.BridgeSlam + 0.05f - age) / 0.2f) : 1f);
                V2 c0 = BW(z, 0f, -hw), c1 = BW(z, len, -hw), c2 = BW(z, len, hw), c3 = BW(z, 0f, hw);
                SetColor(OakTele.WithAlpha(0.07f * a));
                Quad(c0, c1, c2, c3);
                DashedEdge(c0, c1, OakTele.WithAlpha(0.6f * a), age);
                DashedEdge(c1, c2, OakTele.WithAlpha(0.6f * a), age);
                DashedEdge(c2, c3, OakTele.WithAlpha(0.6f * a), age);
                DashedEdge(c3, c0, OakTele.WithAlpha(0.6f * a), age);
                for (float u = 40f; u < len; u += 60f)
                {
                    float uu = Mathf.Repeat(u + age * 90f, len);
                    V2 m = BW(z, uu, 0f);
                    Line(BW(z, uu - 10f, -12f), m, 2f, OakTele.WithAlpha(0.35f * a));
                    Line(m, BW(z, uu - 10f, 12f), 2f, OakTele.WithAlpha(0.35f * a));
                }
            }
            float ts = Mathf.Clamp01(age / 0.35f), tsE = KEo(ts) * (age > cr + 0.4f ? Mathf.Clamp01(1f - (age - cr - 0.4f) / 0.4f) : 1f);
            for (int k = 0; k < 2; k++)
            {
                V2 tw = BW(z, -12f, (k == 0 ? -1f : 1f) * (hw + 16f));
                if (age < 0.5f)
                    KPuffs(tw, age, 7, k * 5f + z.Id, 0f, MathX.Tau, 50f, 0.6f, OakDust, 14f, 0.5f);
                DrawBridgeTower(tw, z.Aim, 0.4f + 0.6f * tsE);
            }
            if (tsE > 0f)
            {
                V2 l0 = BW(z, -12f, -(hw + 6f)), l1 = BW(z, -12f, hw + 6f);
                Line(l0, l1, 12f, C255(93, 98, 107).WithAlpha(tsE));
                Line(l0, l1, 4f, C255(58, 62, 69).WithAlpha(tsE));
            }
            if (age >= 0.22f && age < Game.BridgeSlam)  // the shadow, thrown across the lane while it stands and falls
            {
                V2 sh = new V2(0.28f, 0.42f) * (len * sinT);
                V2 s0 = BW(z, 0f, -hw), s1 = BW(z, 0f, hw), f0 = BW(z, len * cosT, -hw) + sh, f1 = BW(z, len * cosT, hw) + sh;
                SetColor(new Rgba(0f, 0f, 0f, 0.75f * 0.55f * Mathf.Clamp01((age - 0.22f) / 0.2f)));
                Quad(s0, f0, f1, s1);
            }
            if (age >= Game.BridgeSlam && age < cr + 0.9f)
                DrawBridgeDeck(z, age, 1f);
            float a1 = age - Game.BridgeSlam;
            if (a1 >= 0f && a1 < 1.2f)  // the slam
            {
                for (int sd = -1; sd <= 1; sd += 2)
                {
                    V2 outward = BW(z, 0f, sd) - z.Pos;
                    for (int k = 0; k < 14; k++)
                    {
                        float u = k / 13f * len;
                        KPuffs(BW(z, u, sd * hw), a1, 2, k * 7f + sd * 50f + z.Id, outward.Angle, 0.9f, 110f, 1.1f, OakDust, 16f, 0.55f);
                    }
                    Line(BW(z, 0f, sd * hw), BW(z, len, sd * hw), 4f, OakGlint.WithAlpha(0.8f * Mathf.Clamp01(1f - a1 / 0.2f)));
                }
                KPuffs(BW(z, len, 0f), a1, 6, 91f + z.Id, z.Aim, 1.8f, 90f, 1.1f, OakDust, 18f, 0.5f);
                for (int s2 = 0; s2 < 3; s2++)
                    KSparks(BW(z, 80f + s2 * 170f, 0f), a1, 10, s2 * 13f + 4f, 0f, MathX.Tau, 260f, 0.45f, C255(200, 160, 110), 2.2f);
            }
        }

        void DashedEdge(V2 a, V2 b, Rgba col, float age)
        {
            float d = a.DistanceTo(b);
            if (d < 1f)
                return;
            V2 dv = (b - a) / d;
            float off = Mathf.Repeat(age * 30f, 14f);
            for (float s = -off; s < d; s += 14f)  // 8 on, 6 off, marching
            {
                float s0 = Mathf.Max(0f, s), s1 = Mathf.Min(d, s + 8f);
                if (s1 > s0)
                    Line(a + dv * s0, a + dv * s1, 1.5f, col);
            }
        }

        /// Over the enemies: the bridge standing and falling, its chains and their snapping, and the gold glow round the
        /// knight as they call it down.
        void DrawBridgesFalling(float t)
        {
            foreach (var z in game.Zones.Values)
            {
                if (z.Kind != ZoneKind.Drawbridge)
                    continue;
                float age = Game.BridgeAge(z), hw = Game.BridgeHalfWidth, len = Game.BridgeLength;
                Player owner;
                if (age < Game.BridgeSlam && game.Players.TryGetValue(z.Owner, out owner))
                {
                    float ra = Mathf.Clamp01(age / 0.15f) * (1f - Mathf.Clamp01((age - 0.5f) / 0.3f));
                    GlowDisc(owner.Disp, 46f, C255(255, 200, 110), 0.5f * ra);
                    Arc(owner.Disp, 18f + 30f * KEo(age / 0.4f), 2f, C255(255, 215, 140).WithAlpha(0.7f * ra), 32);
                }
                if (age < 0.22f || age >= Game.BridgeSlam)
                    continue;
                float th = BridgeTheta(age), cosT = Mathf.Cos(th), sinT = Mathf.Sin(th), far = len * cosT;
                float fadeIn = Mathf.Clamp01((age - 0.22f) / 0.15f);
                float keep = glAlpha;
                glAlpha = keep * fadeIn;
                DrawBridgeDeck(z, age, Mathf.Max(0.06f, cosT));
                glAlpha = keep;
                Line(BW(z, far, -hw), BW(z, far, hw), 3f, Rgba.White.WithAlpha(0.25f * sinT * fadeIn));
                SetColor(new Rgba(1f, 0.94f, 0.86f, 0.35f * 0.35f * sinT));
                Quad(BW(z, 0f, -hw), BW(z, far, -hw), BW(z, far, hw), BW(z, 0f, hw));
                if (age < Game.BridgeChainsSnap + 0.5f)
                    for (int k = 0; k < 2; k++)
                    {
                        float sd = k == 0 ? -1f : 1f;
                        V2 tw = BW(z, -12f, sd * (hw + 16f)), fc = BW(z, far, sd * hw), mid = V2.Lerp(tw, fc, 0.5f);
                        if (age < Game.BridgeChainsSnap)
                        {
                            DrawBridgeChain(tw, fc, 1f);
                            continue;
                        }
                        float a = (age - Game.BridgeChainsSnap) / 0.5f;
                        DrawBridgeChain(tw, V2.Lerp(tw, mid, 1f - KEo(a) * 0.7f), 1f - a * 0.4f);
                        DrawBridgeChain(fc, V2.Lerp(fc, mid, 1f - KEo(a)), 1f - a);
                        KSparks(mid, age - Game.BridgeChainsSnap, 12, k * 9f + 2f, 0f, MathX.Tau, 240f, 0.4f, C255(220, 224, 230));
                        for (int q = 0; q < 5; q++)
                        {
                            float s = k * 10f + q, aa = KHash(s) * MathX.Tau, dd = KEo(a) * (30f + KHash(s * 2f) * 40f);
                            DrawChainLink(mid + V2.FromAngle(aa) * dd, a * 8f + q, 4f, 2f, C255(144, 151, 161).WithAlpha(1f - a), 1.6f);
                        }
                    }
            }
        }

        /// The slam shakes every screen (the caster's holds a moment too); the cast rumbles a little.
        void DrawBridgeShake()
        {
            foreach (var z in game.Zones.Values)
            {
                if (z.Kind != ZoneKind.Drawbridge || Game.BridgeAge(z) < Game.BridgeSlam || bridgesSlammed.Contains(z.Id))
                    continue;
                bridgesSlammed.Add(z.Id);
                if (Game.BridgeAge(z) > Game.BridgeSlam + 0.3f)
                    continue;  // (joined late, or a preview)
                Shake(0.45f);
                if (z.Owner == game.MyId)
                    Hold(0.08f, 0.2f);
            }
            if (bridgesSlammed.Count > 0)
                bridgesSlammed.RemoveWhere(id => !game.Zones.ContainsKey(id));
        }

        /// An enemy shot stopped by a rail.
        void DrawBridgeSparkFx(Effect fx)
        {
            float a = fx.T;
            KSparks(fx.Pos, a, 9, fx.Pos.X * 0.4f + fx.Pos.Y, fx.Pos2.Angle, 2.2f, 170f, 0.3f, OakGlint);
            GlowDisc(fx.Pos, 14f * (1f - a / 0.2f), C255(255, 200, 140), 0.7f * Mathf.Clamp01(1f - a / 0.2f));
        }
    }
}
