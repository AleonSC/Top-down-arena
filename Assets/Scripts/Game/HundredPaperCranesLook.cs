using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Hundred Paper Cranes' look (Core/HundredPaperCranes.cs; from its mockup, the artifact "Hundred Paper Cranes" v2):
    ///   - DrawCraneRing (over the model): the cranes still in the ring, each at Game.HpcSlot from the replicated clock, flapping
    ///     along its orbit, over a faint lilac ink vortex of three spiral arms; a gold arc round the Ninja counts the 4 s down.
    ///   - DrawCraneShot (WorldShots.cs): a crane in flight (Defs.PaperCrane), bigger, banking along its heading, with a lilac ink
    ///     trail kept from where this screen saw it.
    ///   - DrawPaperCraneFx (FxKind.PaperCrane): the cast's puff of paper (HpcFxCast), a hit unfolding into scraps (HpcFxHit),
    ///     the leftover cranes scattering at the end (HpcFxScatter).
    /// A crane: cream folded paper, a darker fold on the wings, a thin ink outline and a red seal dot. With Visual effects off:
    /// no vortex, trails or glow.
    public partial class ArenaApp
    {
        static readonly Rgba HpcWing = C255(226, 216, 194), HpcEdge = new Rgba(0.08f, 0.07f, 0.1f, 0.55f), HpcGoldCol = C255(227, 181, 74);
        const int HpcTrailMax = 12;

        class HpcView
        {
            public float Clock, Seen = -1f, Age;
        }

        class HpcTrail
        {
            public readonly List<V2> Pts = new List<V2>();
            public float SeenT;
        }

        readonly Dictionary<int, HpcView> hpcViews = new Dictionary<int, HpcView>();
        readonly Dictionary<int, HpcTrail> hpcTrails = new Dictionary<int, HpcTrail>();
        readonly List<int> hpcStale = new List<int>();

        /// This screen's clock for a player's ring, eased toward the host's.
        float HpcClock(Player p, float dt)
        {
            HpcView v;
            if (!hpcViews.TryGetValue(p.Id, out v))
                hpcViews[p.Id] = v = new HpcView { Clock = p.HpcT };
            if (p.HpcT != v.Seen)
            {
                v.Seen = p.HpcT;
                v.Age = 0f;
            }
            else
                v.Age += dt;
            float est = p.HpcT + Mathf.Min(v.Age, 0.25f);
            v.Clock = Mathf.Abs(est - v.Clock) > 0.25f ? est : v.Clock + dt + (est - v.Clock) * Mathf.Min(1f, 8f * dt);
            return v.Clock;
        }

        /// Over the model: the cranes still in the ring and the vortex they swirl in.
        void DrawCraneRing(Player p, float t)
        {
            if (p.HpcT <= 0f || p.HpcN <= 0)
            {
                hpcViews.Remove(p.Id);
                return;
            }
            float clock = HpcClock(p, Time.deltaTime);
            V2 c = p.Disp;
            if (fxOn)
            {
                float a = Mathf.Clamp01(p.HpcN / 40f) * 0.45f * Mathf.Clamp01(clock / 0.4f);
                for (int k = 0; k < 3; k++)
                {
                    V2 prev = c;
                    for (int s = 0; s <= 24; s++)
                    {
                        float f = s / 24f, ang = f * Mathf.PI * 1.6f + clock * 2.6f + k * 2.09f, r = 40f + f * 100f;
                        V2 at = c + V2.FromAngle(ang) * r;
                        if (s > 0)
                            Line(prev, at, 1.2f, Game.HpcInk.WithAlpha(a * (1f - f * 0.6f)));
                        prev = at;
                    }
                }
            }
            for (int i = 0; i < p.HpcN; i++)
            {
                if (clock < Game.HpcBorn(i))
                    continue;
                float heading;
                V2 at = c + Game.HpcSlot(i, clock, out heading);
                DrawCrane(at, heading, clock * 14f + i * 1.7f, 0.95f, 1f);
            }
            if (clock < Game.HpcEnd)
            {
                float left = 1f - clock / Game.HpcEnd;
                Arc(c, p.Def.BodyR + 8f, 2.4f, (clock < Game.HpcGather ? Game.HpcInk : HpcGoldCol).WithAlpha(0.85f), 40, -Mathf.PI / 2f, MathX.Tau * left);
            }
        }

        /// One folded paper crane, nose along `ang`; `flap` drives the wings.
        void DrawCrane(V2 at, float ang, float flap, float scale, float alpha)
        {
            V2 f = V2.FromAngle(ang) * scale, s = f.Orthogonal();
            System.Func<float, float, V2> P = (x, y) => at + f * x + s * y;
            float w = Mathf.Sin(flap) * 0.8f;
            Rgba paper = Game.HpcPaper.WithAlpha(alpha), wing = HpcWing.WithAlpha(alpha), edge = HpcEdge.WithAlpha(HpcEdge.A * alpha);
            V2 nose = P(10f, 0f), back = P(-2f, -2.5f), tail = P(-11f, -1f), belly = P(-2f, 2.5f);
            VTri(nose, paper, back, paper, tail, paper);
            VTri(nose, paper, tail, paper, belly, paper);
            VTri(nose, paper, P(13f, -3f), paper, P(11f, 0.5f), paper);  // the head, bent forward
            V2 wl = P(-1f, -9f - 6f * w), wr = P(-1f, 9f + 6f * w);
            VTri(P(2f, -1f), wing, P(-4f, -1f), wing, wl, wing);
            VTri(P(2f, 1f), wing, P(-4f, 1f), wing, wr, wing);
            float lw = Mathf.Max(0.8f, 0.8f * scale);
            Line(nose, tail, lw, edge);
            Line(P(2f, -1f), wl, lw, edge);
            Line(P(2f, 1f), wr, lw, edge);
            Circle(P(-1f, 0f), 1.6f * scale, Game.HpcSeal.WithAlpha(alpha), 8);
        }

        /// A crane in flight: its ink trail (from where this screen saw it), then the crane, flapping hard.
        void DrawCraneShot(Bullet b, float t)
        {
            V2 d = b.Vel.IsZero ? V2.FromAngle(0f) : b.Vel.Normalized();
            if (fxOn)
            {
                HpcTrail tr;
                if (!hpcTrails.TryGetValue(b.Id, out tr))
                    hpcTrails[b.Id] = tr = new HpcTrail();
                if (tr.Pts.Count == 0 || tr.Pts[tr.Pts.Count - 1].DistanceTo(b.Pos) > 3f)
                {
                    tr.Pts.Add(b.Pos);
                    if (tr.Pts.Count > HpcTrailMax)
                        tr.Pts.RemoveAt(0);
                }
                tr.SeenT = t;
                if (tr.Pts.Count < 3)
                    Line(b.Pos - d * 22f, b.Pos, 1.4f, Game.HpcInk.WithAlpha(0.35f));
                else
                    for (int j = 1; j < tr.Pts.Count; j++)
                    {
                        float k = j / (float)tr.Pts.Count;
                        Line(tr.Pts[j - 1], j == tr.Pts.Count - 1 ? b.Pos : tr.Pts[j], 0.8f + k * 1.6f, Game.HpcInk.WithAlpha(k * 0.45f));
                    }
                if (hpcTrails.Count > 160)  // forget the trails of cranes gone
                {
                    hpcStale.Clear();
                    foreach (var kv in hpcTrails)
                        if (kv.Value.SeenT < t - 0.5f)
                            hpcStale.Add(kv.Key);
                    foreach (int id in hpcStale)
                        hpcTrails.Remove(id);
                }
            }
            DrawCrane(b.Pos, d.Angle, t * 20f + b.Id * 0.9f, 1.2f, 1f);
        }

        void DrawPaperCraneFx(Effect fx)
        {
            float k = fx.Dur > 0f ? Mathf.Clamp01(fx.T / fx.Dur) : 1f, fade = 1f - k;
            V2 c = fx.Pos;
            int seed = Mathf.RoundToInt(c.X * 7f + c.Y * 13f);
            switch (Mathf.RoundToInt(fx.Radius))
            {
                case Game.HpcFxCast:  // a puff of paper and a lilac ring as the cranes fly out
                    Arc(c, 30f + 90f * (1f - fade * fade), 2f, Game.HpcInk.WithAlpha(0.7f * fade), 40);
                    DrawScraps(c, seed, 14, 110f, k, fade);
                    break;
                case Game.HpcFxHit:  // the crane unfolds into scraps, thrown on along its flight
                {
                    V2 on = fx.Pos2 - c;
                    DrawScraps(c + on * k * 2f, seed, 6, 45f, k, fade);
                    if (fxOn)
                        GlowDisc(c, 10f, Game.HpcPaper, 0.35f * fade);
                    break;
                }
                default:  // HpcFxScatter: the leftover cranes blown off as scraps
                    DrawScraps(c, seed, 22, 150f, k, fade);
                    break;
            }
        }

        /// n little paper squares flying out from c, slowing and turning as they fade.
        void DrawScraps(V2 c, int seed, int n, float reach, float k, float fade)
        {
            float out1 = 1f - (1f - k) * (1f - k);
            for (int i = 0; i < n; i++)
            {
                float h = Mathf.Repeat(Mathf.Sin((seed + i) * 12.9898f) * 43758.547f, 1f);
                float h2 = Mathf.Repeat(Mathf.Sin((seed + i) * 78.233f) * 12345.678f, 1f);
                V2 d = V2.FromAngle(i * MathX.Tau / n + h * 1.2f);
                V2 at = c + d * reach * (0.4f + 0.6f * h2) * out1;
                V2 a = V2.FromAngle(h * 6f + k * (4f + 6f * h2)) * 3f, b = a.Orthogonal() * 0.7f;
                Rgba col = (i % 4 == 0 ? Game.HpcSeal : Game.HpcPaper).WithAlpha(fade);
                VTri(at + a + b, col, at - a + b, col, at - a - b, col);
                VTri(at + a + b, col, at - a - b, col, at + a - b, col);
            }
        }
    }
}
