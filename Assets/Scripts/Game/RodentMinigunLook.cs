using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Rodent Minigun's look (Core/RodentMinigun.cs; from its mockup, the claude.ai artifact "Rodent Minigun"). Drawing only,
    /// from the replicated Player.RmT / RmAng / RmLen and the rodents themselves (Defs.RodentShot bullets):
    ///   - the portal (DrawRodentPortal, under the player): a violet hoop seen edge-on across your aim, a dark hole with teal
    ///     swirls turning faster as it heats, six gold studs spinning round its rim like a minigun's barrels and sparks at
    ///     the mouth at full fire; it tears open and pinches shut;
    ///   - a rodent in flight (DrawRodentShot): a mouse (grey), rat (brown, longer tail) or hamster (gold, tailless, with a
    ///     cream belly), stretched by its speed, legs scrabbling, with a faint violet streak;
    ///   - after it: each rodent that vanishes (it hit something or ran out of reach) is left behind on this screen only as a
    ///     scamperer that tumbles back off what it hit, then runs off a random way and fades; and one rodent in three that comes out
    ///     kicks a cheese crumb out of the side of the portal, like a spent case (DrawRodentAfter, over the arena's contents).
    /// With Visual effects off: a plain violet ring for the portal, a plain dot for each rodent, no scamperers or crumbs.
    public partial class ArenaApp
    {
        static readonly Rgba RmViolet = new Rgba(0.69f, 0.49f, 1f), RmTeal = new Rgba(0.35f, 0.85f, 0.91f), RmGold = new Rgba(1f, 0.8f, 0.35f);
        static readonly Rgba RmHole = new Rgba(0.04f, 0.02f, 0.09f);
        static readonly Rgba[] RmFur = { new Rgba(0.72f, 0.7f, 0.77f), new Rgba(0.54f, 0.43f, 0.35f), new Rgba(1f, 0.8f, 0.35f) };
        static readonly Rgba[] RmEar = { new Rgba(0.91f, 0.66f, 0.72f), new Rgba(0.83f, 0.6f, 0.6f), new Rgba(0.94f, 0.63f, 0.38f) };
        static readonly float[] RmLen = { 7f, 9.5f, 7.5f };  // body length, px: mouse, rat, hamster

        class RmSeen { public V2 Pos, Vel; public float Life; public int Owner, Frame; public Game.Rodent Kind; }
        class RmScamper { public V2 Pos, Vel; public float Age, Ang, Spin, Phase; public bool Bounce; public Game.Rodent Kind; }
        class RmCrumb { public V2 Pos, Vel; public float Age, Rot; }

        readonly Dictionary<int, RmSeen> rmSeen = new Dictionary<int, RmSeen>();
        readonly List<RmScamper> rmScampers = new List<RmScamper>();
        readonly List<RmCrumb> rmCrumbs = new List<RmCrumb>();
        readonly List<int> rmGone = new List<int>();
        int rmFrame;
        float rmLastT;

        static float RmHash(int n) { float s = Mathf.Sin(n * 127.1f + 311.7f) * 43758.5453f; return s - Mathf.Floor(s); }

        Game.Rodent RodentKind(Bullet b)
        {
            Player owner;
            game.Players.TryGetValue(b.Owner, out owner);
            return Game.RodentOf(b.Id, Game.RatShareOf(owner));
        }

        /// WorldRenderer.DrawPlayerAuras: the portal in front of a player whose Rodent Minigun is out.
        void DrawRodentPortal(Player p, float t)
        {
            if (p.RmT <= 0f || p.Downed)
                return;
            float o = Game.RmOpenness(p.RmT, p.RmLen), heat = Game.RmHeat(p.RmT, p.RmLen), rate = Game.RmRate(p.RmT, p.RmLen) / Game.RmRateMax;
            if (o <= 0f)
                return;
            V2 c = Game.RmMouth(p.Disp, p.RmAng), ax = V2.FromAngle(p.RmAng), side = ax.Orthogonal();
            float ry = 20f * EaseOut3(o), rx = 7f * o;
            if (!fxOn)
            {
                Arc(c, ry, 2f, RmViolet.WithAlpha(0.8f), 24);
                return;
            }
            SoftGlow(c + ax * 4f, ry * 1.9f, RmViolet, 0.35f + 0.45f * heat);
            Ellipse(c, rx + 2.5f, ry + 2.5f, p.RmAng, RmViolet.WithAlpha(0.95f), 24);
            Ellipse(c, rx, ry, p.RmAng, RmHole, 24);
            // the swirl: three arcs inside the hole, turning faster as it heats
            float spin = t * (3f + 16f * heat);
            for (int i = 0; i < 3; i++)
            {
                float k = 0.75f - i * 0.2f, a0 = spin * (1f + i * 0.3f) + i * 2f;
                V2 prev = c + ax * (Mathf.Cos(a0) * rx * k) + side * (Mathf.Sin(a0) * ry * k);
                for (int s = 1; s <= 8; s++)
                {
                    float a = a0 + 2.2f * s / 8f;
                    V2 next = c + ax * (Mathf.Cos(a) * rx * k) + side * (Mathf.Sin(a) * ry * k);
                    Line(prev, next, 1.3f, RmTeal.WithAlpha((0.45f + 0.45f * heat) * o));
                    prev = next;
                }
            }
            // six studs round the rim, spinning like a minigun's barrels: gold in front, dim behind
            float barrel = t * (2f + 22f * rate);
            for (int i = 0; i < 6; i++)
            {
                float a = barrel + i * MathX.Tau / 6f;
                bool front = Mathf.Cos(a) > 0f;
                V2 at = c + ax * (Mathf.Cos(a) * (rx + 1f)) + side * (Mathf.Sin(a) * (ry + 1f));
                Circle(at, 2.4f * o, front ? RmGold : RmGold.Mix(RmHole, 0.55f), 8);
            }
            if (rate > 0.8f)  // sparks at the mouth at full fire
                for (int i = 0; i < 2; i++)
                {
                    int seed = Mathf.FloorToInt(t * 30f) * 7 + i + p.Id * 13;
                    V2 at = c + ax * (rx + 4f + 8f * RmHash(seed)) + side * (RmHash(seed + 3) - 0.5f) * 12f;
                    Circle(at, 1.5f + 2f * RmHash(seed + 5), RmGold.WithAlpha(0.8f), 8);
                }
        }

        /// WorldShots.DrawSmallShot: one rodent in flight. Notes it for DrawRodentAfter.
        void DrawRodentShot(Bullet b, float t)
        {
            var kind = RodentKind(b);
            if (!fxOn)
            {
                Circle(b.Pos, 4f, RmFur[(int)kind], 8);
                return;
            }
            RmSeen seen;
            if (!rmSeen.TryGetValue(b.Id, out seen))
            {
                seen = new RmSeen { Kind = kind, Owner = b.Owner };
                rmSeen[b.Id] = seen;
                RmKickCrumb(b);
            }
            seen.Pos = b.Pos;
            seen.Vel = b.Vel;
            seen.Life = b.Life;
            seen.Frame = rmFrame;
            float ang = Mathf.Atan2(b.Vel.Y, b.Vel.X);
            Line(b.Pos, b.Pos - b.Vel * 0.03f, 2f, RmViolet.WithAlpha(0.25f));
            DrawRodent(b.Pos, ang, kind, t * 40f + b.Id, 1.35f, 1f);
        }

        /// A cheese crumb kicked out of the side of the portal as a rodent leaves it (one rodent in three).
        void RmKickCrumb(Bullet b)
        {
            Player p;
            if (b.Id % 3 != 0 || !game.Players.TryGetValue(b.Owner, out p) || p.RmT <= 0f)  // (one rodent in three: a trickle, not a stream)
                return;
            V2 ax = V2.FromAngle(p.RmAng);
            float side = RmHash(b.Id) < 0.5f ? 1f : -1f;
            V2 kick = ax.Orthogonal() * side * (40f + 50f * RmHash(b.Id + 7)) - ax * (30f + 30f * RmHash(b.Id + 13));
            rmCrumbs.Add(new RmCrumb { Pos = Game.RmMouth(p.Disp, p.RmAng), Vel = kick, Rot = RmHash(b.Id + 11) * 6f });
        }

        /// A rodent at `c` heading `ang`: tail, four scrabbling legs, body, head, ears, eyes and nose. `stretch` lengthens it at
        /// speed; `phase` runs its legs and tail.
        void DrawRodent(V2 c, float ang, Game.Rodent kind, float phase, float stretch, float alpha)
        {
            int k = (int)kind;
            float L = RmLen[k], S = L * stretch;
            V2 f = V2.FromAngle(ang), s = f.Orthogonal();
            Rgba fur = RmFur[k].WithAlpha(alpha), ear = RmEar[k].WithAlpha(alpha);
            if (kind != Game.Rodent.Hamster)  // the tail
            {
                float wig = Mathf.Sin(phase) * 3f, len = kind == Game.Rodent.Rat ? 2.6f : 2.2f;
                V2 a = c - f * (S * 0.8f), m = c - f * (S * 1.6f) + s * wig, e = c - f * (S * len) - s * wig;
                var tail = new Rgba(0.83f, 0.63f, 0.66f, alpha);
                Line(a, m, 1.2f, tail);
                Line(m, e, 1f, tail);
            }
            float lg = Mathf.Sin(phase) * 2.5f;  // the legs, front and back pairs out of step
            for (int i = 0; i < 4; i++)
            {
                float along = i < 2 ? 0.35f : -0.35f, out1 = i % 2 == 0 ? 1f : -1f, swing = (i == 0 || i == 3 ? lg : -lg);
                Line(c + f * (L * along) + s * (L * 0.45f * out1), c + f * (L * along + swing) + s * (L * 0.85f * out1), 1.4f, ear);
            }
            Ellipse(c, S * 0.9f, L * 0.55f, ang, fur, 12);
            if (kind == Game.Rodent.Hamster)
                Ellipse(c + f * (L * 0.2f), L * 0.45f, L * 0.35f, ang, new Rgba(1f, 0.95f, 0.84f, alpha), 10);
            V2 head = c + f * (S * 0.85f);
            Ellipse(head, L * 0.5f, L * 0.42f, ang, fur, 10);
            Circle(c + f * (S * 0.6f) + s * (L * 0.45f), L * 0.25f, ear, 8);
            Circle(c + f * (S * 0.6f) - s * (L * 0.45f), L * 0.25f, ear, 8);
            var eye = new Rgba(0.07f, 0.07f, 0.07f, alpha);
            Circle(c + f * (S * 1.05f) + s * (L * 0.18f), 1.1f, eye, 6);
            Circle(c + f * (S * 1.05f) - s * (L * 0.18f), 1.1f, eye, 6);
            Circle(c + f * (S * 1.3f), 1.3f, new Rgba(0.94f, 0.54f, 0.63f, alpha), 6);
        }

        /// WorldRenderer.DrawArenaOver: the rodents left behind (tumbling off what they hit, then running off) and the crumbs.
        /// Runs once a frame, so it also notices which rodents have gone since the last.
        void DrawRodentAfter(float t)
        {
            float dt = Mathf.Clamp(t - rmLastT, 0f, 0.1f);
            rmLastT = t;
            rmFrame++;
            if (!fxOn || game == null)
            {
                rmSeen.Clear();
                rmScampers.Clear();
                rmCrumbs.Clear();
                return;
            }
            rmGone.Clear();
            foreach (var kv in rmSeen)
                if (kv.Value.Frame < rmFrame - 1 && !game.Bullets.ContainsKey(kv.Key))
                    rmGone.Add(kv.Key);
            foreach (int id in rmGone)
            {
                var g = rmSeen[id];
                rmSeen.Remove(id);
                if (rmScampers.Count > 160)
                    continue;
                bool hit = g.Life > 0.06f;  // gone before its reach ran out: it hit something (or a wall)
                float back = Mathf.Atan2(g.Vel.Y, g.Vel.X) + MathX.Pi + (RmHash(id) - 0.5f) * 2.4f;
                float run = RmHash(id + 3) * MathX.Tau;
                rmScampers.Add(new RmScamper
                {
                    Pos = g.Pos, Kind = g.Kind, Bounce = hit, Phase = RmHash(id + 5) * 6f,
                    Ang = hit ? back : run, Spin = (RmHash(id + 9) - 0.5f) * 28f,
                    Vel = hit ? V2.FromAngle(back) * (120f + 100f * RmHash(id + 1)) : V2.FromAngle(run) * (90f + 50f * RmHash(id + 1)),
                });
            }
            for (int i = rmScampers.Count - 1; i >= 0; i--)
            {
                var r = rmScampers[i];
                r.Age += dt;
                r.Phase += dt * 22f;
                if (r.Bounce)
                {
                    r.Pos += r.Vel * dt;
                    r.Vel *= Mathf.Pow(0.06f, dt);
                    r.Ang += r.Spin * dt;
                    if (r.Age > 0.35f)  // landed: off it runs
                    {
                        r.Bounce = false;
                        r.Age = 0f;
                        r.Vel = V2.FromAngle(r.Ang) * 120f;
                    }
                }
                else
                    r.Pos += r.Vel * dt;
                float alpha = r.Bounce ? 1f : 1f - r.Age / 0.9f;
                if (alpha <= 0f)
                {
                    rmScampers.RemoveAt(i);
                    continue;
                }
                DrawRodent(r.Pos, r.Ang, r.Kind, r.Phase, 1f, alpha);
            }
            for (int i = rmCrumbs.Count - 1; i >= 0; i--)
            {
                var c = rmCrumbs[i];
                c.Age += dt;
                c.Pos += c.Vel * dt;
                c.Vel *= Mathf.Pow(0.08f, dt);
                c.Rot += dt * 8f;
                if (c.Age > 1.2f || rmCrumbs.Count > 120)
                {
                    rmCrumbs.RemoveAt(i);
                    continue;
                }
                V2 u = V2.FromAngle(c.Rot), v = u.Orthogonal();
                SetColor(RmGold.WithAlpha(Mathf.Min(1f, (1.2f - c.Age) * 2f)));
                Tri(c.Pos + u * 2.2f + v * 1.5f, c.Pos - u * 2.2f + v * 1.5f, c.Pos - v * 2.2f);
            }
        }
    }
}
