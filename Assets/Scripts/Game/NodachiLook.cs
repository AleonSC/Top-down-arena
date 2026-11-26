using UnityEngine;
using System.Collections.Generic;

namespace Arena
{
    /// The Nodachi, held and swung (Core/NodachiMotion.cs is the motion, which the host times the hits from). The Ninja's model
    /// leaves its arms out with it; this draws both arms on one long grip and the blade:
    ///   - at the ready in the stance you're in (Arashi low and trailing behind, Mamori's point up at their eyes), breathing;
    ///   - each step (FxKind.NodStep, Radius = id * 100 + code) posed from its age, eased in from wherever the blade was, with a
    ///     brush trail behind each sweep's edge out to its full reach (DrawNodIdle);
    ///   - a stance switch or a step ending eases into the ready pose over NodBlend, so a combo flows from step to step;
    ///   - on the ground (DrawNodStepFx): each sweep's crescent left behind, Engetsu's moon, Tenchi's shockwave and cracks, the
    ///     jabs' streaks and the pommel's knock;
    ///   - the Storm's armour as a gold shimmer on the wind-up, the Guard's cone while a jab plays, and two rows of five pips over
    ///     the head: the stance you're in bright, the other one's kept place dim under it.
    public partial class ArenaApp
    {
        static readonly Rgba NodInk = new Rgba(0.03f, 0.03f, 0.05f), NodSleeve = new Rgba(0.13f, 0.13f, 0.18f), NodWrap = new Rgba(0.16f, 0.12f, 0.2f),
            NodWrapLine = new Rgba(0.75f, 0.68f, 0.88f), NodGuardGold = new Rgba(1f, 0.8f, 0.35f), NodEdge = new Rgba(0.97f, 0.98f, 1f);
        const float NodBlend = 0.2f, NodTrailDt = 0.012f;
        const int NodTrailN = 12;

        class NodView { public int Key = -1; public float Since = 99f, LastAim; public NodachiMotion.Pose From, Last; }
        readonly Dictionary<int, NodView> nodViews = new Dictionary<int, NodView>();

        static Rgba NodCol(int stance) => stance == 0 ? Game.NodSteel : Game.NodJade;

        /// This player's newest step effect, or null.
        Effect NodFxOf(int id)
        {
            Effect best = null;
            foreach (var fx in FxOf(FxKind.NodStep))
                if (fx.Kind == FxKind.NodStep && fx.T < fx.Dur && Mathf.RoundToInt(fx.Radius) / 100 == id && (best == null || fx.T < best.T))
                    best = fx;
            return best;
        }

        /// The grip, the blade's root and its tip for a pose along `aim` from `o`.
        static void NodPoints(V2 o, float aim, NodachiMotion.Pose m, out V2 grip, out V2 butt, out V2 tip, out V2 dir)
        {
            dir = V2.FromAngle(aim + m.Th);
            grip = o + V2.FromAngle(aim) * m.Fwd + dir * (NodachiMotion.Grip + m.Ext);
            butt = grip - dir * (NodachiMotion.Handle * m.Len);
            tip = grip + dir * (NodachiMotion.Blade * m.Len);
        }

        /// The blade in hand (from the player loop): the step playing, or the stance's ready pose.
        void DrawNodIdle(Player p, float t)
        {
            if (p.Weapon.Id != WeaponId.Nodachi || p.Downed)
                return;
            NodView v;
            var ready = NodachiMotion.Ready(p.NodStance);
            if (!nodViews.TryGetValue(p.Id, out v))
                nodViews[p.Id] = v = new NodView { From = ready, Last = ready, LastAim = p.Aim, Key = p.NodStance + 2 };
            float dt = Time.deltaTime;
            var fx = NodFxOf(p.Id);
            int code = fx != null ? Mathf.RoundToInt(fx.Radius) % 100 : -1;
            var step = fx != null ? NodachiMotion.StepOf(code) : null;
            if (step != null && (fx.T >= step.Dur || NodachiMotion.StanceOf(code) != p.NodStance))
                step = null;  // over, or cut short by a switch: the ready pose takes it from here
            float aim = step != null ? (fx.Pos2 - fx.Pos).Angle : p.Aim;
            int key = step != null ? fx.GetHashCode() : p.NodStance + 2;
            if (key != v.Key)
            {
                v.Key = key;
                v.From = v.Last;
                v.From.Th = v.LastAim + v.Last.Th - aim;  // (the same blade, measured from the new aim)
                v.From.R = 0f;
                v.Since = 0f;
            }
            else
                v.Since += dt;
            NodachiMotion.Pose now;
            if (step != null)
                now = NodachiMotion.At(step, p.NodStance, fx.T, v.From);
            else
            {
                var r = ready;
                r.Th += 0.035f * Mathf.Sin(t * 2.2f);  // breathing
                float k = Mathf.Clamp01(v.Since / NodBlend);
                now = NodachiMotion.Mix(v.From, r, k * k * (3f - 2f * k));
            }
            v.Last = now;
            v.LastAim = aim;
            V2 o = p.Disp;
            if (step != null)
            {
                if (p.NodStance == 0 && NodachiMotion.Armoured(step, fx.T))
                    DrawNodArmour(o, fx.T / step.Windup, t);
                if (p.NodStance == 1 && NodachiMotion.Guarding(step, fx.T))
                    DrawNodCone(p, o, p.Aim, fx.T, step);
                DrawNodTrail(o, aim, step, p.NodStance, fx.T, v.From);
            }
            DrawNodBlade(o, aim, now, p.NodStance);
            DrawNodPips(p, t);
        }

        /// The brush trail behind a sweep's edge: the blade's path a few moments back, from its middle out to the sweep's reach.
        void DrawNodTrail(V2 o, float aim, NodachiMotion.Step s, int stance, float c, NodachiMotion.Pose from)
        {
            Rgba col = NodCol(stance);
            V2 pIn = default(V2), pOut = default(V2);
            bool have = false;
            for (int i = 0; i <= NodTrailN; i++)
            {
                float ci = c - i * NodTrailDt;
                if (ci <= 0f) break;
                var m = NodachiMotion.At(s, stance, ci, from);
                if (m.R <= 0f) { have = false; continue; }
                V2 dir = V2.FromAngle(aim + m.Th);
                V2 a = o + dir * (NodachiMotion.Grip + NodachiMotion.Blade * 0.38f), b = o + dir * m.R;
                if (have)
                {
                    float a0 = 1f - (i - 1f) / NodTrailN, a1 = 1f - (float)i / NodTrailN;
                    a0 *= a0; a1 *= a1;
                    VTri(pOut, col.WithAlpha(0.6f * a0), b, col.WithAlpha(0.6f * a1), a, col.WithAlpha(0f));
                    VTri(pOut, col.WithAlpha(0.6f * a0), a, col.WithAlpha(0f), pIn, col.WithAlpha(0f));
                    Line(pOut, b, 4f * a0 + 0.8f, (stance == 0 ? Game.NodVermilion : NodEdge).WithAlpha(0.85f * a0));
                }
                pIn = a; pOut = b; have = true;
            }
        }

        /// Both arms on the grip, then the nodachi: a long wrapped handle, a gold tsuba, the steel with its temper line.
        void DrawNodBlade(V2 o, float aim, NodachiMotion.Pose m, int stance)
        {
            NodPoints(o, aim, m, out V2 grip, out V2 butt, out V2 tip, out V2 dir);
            V2 side = dir.Orthogonal(), sh = new V2(3f, 5f);
            Rgba shc = new Rgba(0f, 0f, 0f, 0.24f);
            Line(butt + sh, tip + sh, 4.6f * Mathf.Lerp(0.7f, 1f, m.Len), shc);  // its shadow on the floor
            V2 hand2 = grip - dir * (NodachiMotion.Handle * 0.55f * m.Len);
            for (int k = 0; k < 2; k++)  // the arms, shoulder to hand: the right on the grip, the left lower on the handle
            {
                V2 s = o + V2.FromAngle(aim + (k == 0 ? 1.45f : -1.45f)) * 10.5f, hand = k == 0 ? grip : hand2;
                V2 d = hand - s, n = d.IsZero ? new V2(0f, 1f) : d.Normalized().Orthogonal();
                if (n.X * (s.X - o.X) + n.Y * (s.Y - o.Y) < 0f)
                    n = n * -1f;
                V2 elbow = (s + hand) * 0.5f + n * Mathf.Max(0f, 6f - d.Length * 0.2f);
                Line(s, elbow, 7.4f, NodInk);
                Line(elbow, hand, 7.4f, NodInk);
                Line(s, elbow, 5f, NodSleeve);
                Line(elbow, hand, 5f, NodSleeve);
                Circle(s, 5.2f, NodInk, 12);
                Circle(s, 4.2f, NodSleeve.Mix(NodCol(stance), 0.15f), 12);
            }
            // the handle: dark wrap with crossing diamonds, a cap at the end
            Line(butt, grip + dir * 3f, 7f, NodInk);
            Line(butt, grip + dir * 3f, 5f, NodWrap);
            int turns = 5;
            for (int i = 0; i < turns; i++)
            {
                V2 at = butt + (grip - butt) * ((i + 0.5f) / turns);
                Line(at - side * 1.8f - dir * 1.4f, at + side * 1.8f + dir * 1.4f, 0.8f, NodWrapLine.WithAlpha(0.55f));
            }
            Circle(butt, 2.6f, new Rgba(0.75f, 0.62f, 0.3f), 8);
            // the tsuba: a gold oval across the blade
            V2 ts = grip + dir * 4f;
            Line(ts - side * 6.4f, ts + side * 6.4f, 4.4f, NodInk);
            Line(ts - side * 5.4f, ts + side * 5.4f, 2.8f, NodGuardGold);
            // the blade: broad at the root, tapering to the point, curving a touch; ink outline, steel, a bright edge, the temper line
            V2 root = grip + dir * 6f;
            float L = (tip - root).Length;
            const int N = 10;
            var back = new V2[N + 1];
            var edge = new V2[N + 1];
            for (int i = 0; i <= N; i++)
            {
                float u = (float)i / N;
                V2 c = root + dir * (L * u) + side * (3f * u * u);  // the curve (sori)
                back[i] = c + side * Mathf.Lerp(3.6f, 0.8f, u * u);
                edge[i] = c - side * Mathf.Lerp(2.8f, 0.6f, u);
            }
            back[N] = edge[N] = root + dir * (L + 4f) + side * 2.4f;
            for (int i = 0; i < N; i++)  // the ink outline first
                Line((back[i] + edge[i]) * 0.5f, (back[i + 1] + edge[i + 1]) * 0.5f, Mathf.Lerp(8.6f, 3.4f, (float)i / N), NodInk);
            Rgba steel = new Rgba(0.7f, 0.76f, 0.86f), bright = NodEdge;
            for (int i = 0; i < N; i++)
            {
                V2 m0 = (back[i] + edge[i]) * 0.5f, m1 = (back[i + 1] + edge[i + 1]) * 0.5f;
                VTri(back[i], steel, back[i + 1], steel, m1, bright);
                VTri(back[i], steel, m1, bright, m0, bright);
                VTri(m0, bright, m1, bright, edge[i + 1], bright);
                VTri(m0, bright, edge[i + 1], bright, edge[i], bright);
                Line(Vector2Lerp(m0, edge[i], 0.45f), Vector2Lerp(m1, edge[i + 1], 0.45f), 0.9f, (stance == 0 ? Game.NodVermilion : Game.NodJade).WithAlpha(0.8f));  // the hamon
            }
            Line(back[0], back[N - 1], 0.8f, new Rgba(0.3f, 0.34f, 0.42f, 0.8f));  // the back's ridge
            Circle(grip, 3.6f, NodInk, 10);
            Circle(grip, 2.7f, NodSleeve.Mix(NodWrapLine, 0.2f), 10);
            Circle(hand2, 3.6f, NodInk, 10);
            Circle(hand2, 2.7f, NodSleeve.Mix(NodWrapLine, 0.2f), 10);
        }

        /// The Storm's armour while a step winds up: a gold shell round the body that tightens as the swing comes.
        void DrawNodArmour(V2 o, float u, float t)
        {
            float a = 0.35f + 0.25f * Mathf.Sin(t * 30f);
            Arc(o, 22f - 3f * u, 2.2f, NodGuardGold.WithAlpha(a), 24);
            Arc(o, 22f - 3f * u, 1f, NodGuardGold.WithAlpha(0.7f), 24, -MathX.Pi / 2f, MathX.Tau * u);
        }

        /// The Guard's cone in front while a jab plays: a faint jade fan and its rim where shots stop.
        void DrawNodCone(Player p, V2 o, float aim, float c, NodachiMotion.Step s)
        {
            float half = (Game.NodConeDeg + p.Stats.NodConeBonus) * MathX.Pi / 360f, k = Mathf.Clamp01(c / 0.06f) * Mathf.Clamp01((s.StrikesEnd + 0.05f - c) / 0.08f);
            const int N = 14;
            for (int i = 0; i < N; i++)
            {
                float a0 = aim - half + 2f * half * i / N, a1 = aim - half + 2f * half * (i + 1) / N;
                V2 p0 = o + V2.FromAngle(a0) * Game.NodConeOut, p1 = o + V2.FromAngle(a1) * Game.NodConeOut;
                V2 q0 = o + V2.FromAngle(a0) * Game.NodConeIn, q1 = o + V2.FromAngle(a1) * Game.NodConeIn;
                VTri(q0, Game.NodJade.WithAlpha(0f), p0, Game.NodJade.WithAlpha(0.12f * k), p1, Game.NodJade.WithAlpha(0.12f * k));
                VTri(q0, Game.NodJade.WithAlpha(0f), p1, Game.NodJade.WithAlpha(0.12f * k), q1, Game.NodJade.WithAlpha(0f));
            }
            Arc(o, Game.NodConeOut, 1.6f, Game.NodJade.WithAlpha(0.6f * k), 20, aim - half, 2f * half);
        }

        /// Two rows of five pips over the head: the stance you're in on top, lit up to its place (the finisher larger, pulsing
        /// when it's next), and the other stance's kept place dim and small under it.
        void DrawNodPips(Player p, float t)
        {
            V2 c = p.Disp + new V2(0f, -p.Def.BodyR - 17f);
            for (int row = 0; row < 2; row++)
            {
                int stance = row == 0 ? p.NodStance : p.NodStance ^ 1, at = Game.NodAt(p, stance);
                float sc = row == 0 ? 1f : 0.62f, gap = 9f * sc;
                V2 rc = c + new V2(0f, row * 8f);
                for (int i = 0; i < NodachiMotion.Steps; i++)
                {
                    bool fin = (stance == 0 ? NodachiMotion.Storm : NodachiMotion.Guard)[i].Finisher;
                    V2 q = rc + new V2((i - 2f) * gap, 0f);
                    float r = (fin ? 3.8f : 3f) * sc;
                    bool lit = i < at, next = i == at && fin;
                    Rgba col = NodCol(stance);
                    Rgba fill = lit ? col.WithAlpha(row == 0 ? 1f : 0.55f) : next ? NodGuardGold.WithAlpha((0.6f + 0.4f * Mathf.Sin(t * 12f)) * (row == 0 ? 1f : 0.5f)) : new Rgba(1f, 1f, 1f, 0.14f);
                    Polygon(q, r + 1f, 4, 0f, new Rgba(0f, 0f, 0f, 0.55f));
                    Polygon(q, r, 4, 0f, fill);
                }
            }
        }

        /// A step on the ground, drawn from its age: each sweep's crescent, Engetsu's moon, Tenchi's shockwave, the jabs' streaks.
        void DrawNodStepFx(Effect fx)
        {
            int code = Mathf.RoundToInt(fx.Radius) % 100, stance = NodachiMotion.StanceOf(code);
            var s = NodachiMotion.StepOf(code);
            if (s == null)
                return;
            float aim = (fx.Pos2 - fx.Pos).Angle;
            V2 fwd = V2.FromAngle(aim);
            Rgba col = NodCol(stance);
            float moved = 0f;
            foreach (var g in s.Segs)
            {
                V2 o = fx.Pos + fwd * moved;
                switch (g.K)
                {
                    case NodachiMotion.Kind.Walk:
                        moved += g.Reach * 0.5f;  // (Nagi's sweeps are drawn from the middle of its walk)
                        break;
                    case NodachiMotion.Kind.Hop:
                        moved += g.Reach;
                        break;
                    case NodachiMotion.Kind.Arc:
                    {
                        float u = fx.T - g.T1;
                        if (u < 0f || u > 0.35f)
                            break;
                        float al = 1f - u / 0.35f, a0 = aim + g.A0 * MathX.Pi / 180f, a1 = aim + g.A1 * MathX.Pi / 180f;
                        const int N = 24;
                        for (int i = 0; i < N; i++)
                        {
                            float x0 = Mathf.Lerp(a0, a1, (float)i / N), x1 = Mathf.Lerp(a0, a1, (i + 1f) / N), w = (i + 0.5f) / N;
                            V2 p0 = o + V2.FromAngle(x0) * g.Reach, p1 = o + V2.FromAngle(x1) * g.Reach;
                            V2 q0 = o + V2.FromAngle(x0) * g.Reach * 0.7f, q1 = o + V2.FromAngle(x1) * g.Reach * 0.7f;
                            float k = al * (0.25f + 0.75f * w);
                            VTri(q0, col.WithAlpha(0f), p0, col.WithAlpha(0.2f * k), p1, col.WithAlpha(0.2f * k));
                            VTri(q0, col.WithAlpha(0f), p1, col.WithAlpha(0.2f * k), q1, col.WithAlpha(0f));
                        }
                        if (code == 2)  // Engetsu: the full moon drawn round you by the tip
                        {
                            Arc(o, g.Reach, 2.4f * al + 0.4f, NodEdge.WithAlpha(0.7f * al), 48);
                            Arc(o, g.Reach + 3f, 1f, Game.NodVermilion.WithAlpha(0.5f * al), 48);
                        }
                        break;
                    }
                    case NodachiMotion.Kind.Jab:
                    case NodachiMotion.Kind.Pommel:
                    {
                        float u = fx.T - NodachiMotion.HitAt(g);
                        if (u < 0f || u > 0.18f)
                            break;
                        float al = 1f - u / 0.18f;
                        V2 d = V2.FromAngle(aim + g.A0 * MathX.Pi / 180f), side = d.Orthogonal();
                        if (g.K == NodachiMotion.Kind.Pommel)
                        {
                            V2 at = o + d * g.Reach;
                            Arc(at, 10f + 40f * u, 3f * al, NodEdge.WithAlpha(0.8f * al), 18);
                            for (int i = 0; i < 6; i++)
                                Line(at + V2.FromAngle(i * 1.05f) * (8f + 40f * u), at + V2.FromAngle(i * 1.05f) * (14f + 60f * u), 1.4f, NodGuardGold.WithAlpha(al));
                            break;
                        }
                        V2 a = o + d * (NodachiMotion.Tip * 0.6f), b = o + d * g.Reach;
                        float w = g.Half * 0.6f * (0.5f + 0.5f * al);
                        VTri(a + side * w * 0.3f, col.WithAlpha(0f), a - side * w * 0.3f, col.WithAlpha(0f), b - side * w, col.WithAlpha(0.5f * al));
                        VTri(a + side * w * 0.3f, col.WithAlpha(0f), b - side * w, col.WithAlpha(0.5f * al), b + side * w, col.WithAlpha(0.5f * al));
                        VTri(b + side * w, col.WithAlpha(0.6f * al), b - side * w, col.WithAlpha(0.6f * al), b + d * 12f, col.WithAlpha(0f));
                        Line(a, b + d * 6f * al, 1.2f, NodEdge.WithAlpha(al));
                        break;
                    }
                    case NodachiMotion.Kind.Slam:
                    {
                        int extra = Mathf.Max(0, Mathf.RoundToInt((fx.Dur - s.Dur - 0.6f) / Game.NodAftershockDelay));
                        for (int k = 0; k <= extra; k++)
                        {
                            float u = fx.T - g.T0 - Game.NodAftershockDelay * k;
                            if (u < 0f || u > 0.75f)
                                continue;
                            float R = g.Reach + Game.NodAftershockOut * k, v = Mathf.Clamp01(u / 0.28f), e = 1f - (1f - v) * (1f - v) * (1f - v);
                            float fade = 1f - Mathf.Clamp01(u / 0.75f);
                            Arc(o, R * e, 4f * (1f - v) + 0.8f, NodEdge.WithAlpha((1f - v) * 0.9f), 56);
                            Arc(o, R * e * 0.93f, 1.6f, Game.NodVermilion.WithAlpha((1f - v) * 0.8f), 56);
                            Arc(o, R * e * 0.8f, 1f, NodEdge.WithAlpha((1f - v) * 0.35f), 56);
                            if (k > 0)
                                continue;
                            V2 land = o + fwd * (NodachiMotion.Tip * 0.8f);
                            if (u < 0.12f)
                                Circle(land, 12f + 70f * u, new Rgba(1f, 1f, 1f, 0.5f * (1f - u / 0.12f)), 20);
                            for (int i = 0; i < 11; i++)  // cracks out from where the blade bit the floor
                            {
                                float a = aim + (i - 5f) * 0.42f + Mathf.Sin(i * 7.1f) * 0.12f, l = (50f + 30f * Mathf.Abs(Mathf.Sin(i * 3.3f))) * (1f - Mathf.Pow(1f - Mathf.Clamp01(u / 0.12f), 3f));
                                V2 m = land + V2.FromAngle(a + 0.18f) * l * 0.5f;
                                Line(land, m, 1.8f, new Rgba(0f, 0f, 0f, 0.6f * fade));
                                Line(m, land + V2.FromAngle(a - 0.08f) * l, 1.2f, new Rgba(0f, 0f, 0f, 0.5f * fade));
                                Line(m, land + V2.FromAngle(a - 0.08f) * l, 0.6f, Game.NodVermilion.WithAlpha(0.5f * fade * (1f - v)));
                            }
                        }
                        break;
                    }
                }
            }
        }
    }
}
