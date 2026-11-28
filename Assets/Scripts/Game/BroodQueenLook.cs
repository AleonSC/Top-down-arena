using UnityEngine;

namespace Arena
{
    /// Brood Queen (the DPS Summoner's fourth ultimate, Core/BroodQueen.cs), from its mockup (claude.ai artifact "Spider Queen").
    /// Everything comes from the minion sync (Pos, Aim, T and Duration for her age, Stacks her state, Wind a bite, LinkTo her prey,
    /// GuardFrac her health) and the web zones, so it costs nothing extra on the network:
    ///   the queen: lowered on a silk thread (her shadow grows under her), then from above eight long jointed legs that step in
    ///     turn, a big dark violet abdomen with a gold hourglass and faint stripes, a crowned head with green fangs that snap
    ///     forward on a bite and a cluster of pink eyes, a soft violet glow under her; gold brackets on her prey; she climbs her
    ///     thread back up at the end;
    ///   a broodling: the same spider at a few px, scuttling; a green tick on a bite while latched;
    ///   a web: ten spokes and four wobbling rings of silk over a faint violet wash, popping out as it lands and fading at the end.
    /// Visual effects off: plain octagons and a thin ring for a web.
    public partial class ArenaApp
    {
        static readonly Rgba BqBody = new Rgba(0.14f, 0.07f, 0.29f), BqBack = new Rgba(0.2f, 0.1f, 0.4f), BqRim = new Rgba(0.72f, 0.49f, 1f);
        static readonly Rgba BqLeg = new Rgba(0.16f, 0.09f, 0.31f), BqLegLit = new Rgba(0.56f, 0.36f, 1f), BqGold = new Rgba(0.95f, 0.76f, 0.31f);
        static readonly Rgba BqFang = new Rgba(0.49f, 1f, 0.6f), BqEye = new Rgba(1f, 0.42f, 0.82f), BqSilk = new Rgba(0.93f, 0.9f, 1f);
        const float BqSky = 420f;  // how high her thread starts above where she lands

        /// How high the queen hangs over her spot: dropping, then climbing away.
        static float BqLift(Minion m)
        {
            float age = Game.BqAge(m);
            if (age < Game.BqDrop)
            {
                float k = 1f - Mathf.Clamp01(age / Game.BqDrop);
                return k * k * BqSky;
            }
            if (Game.BqClimbing(m))
            {
                float k = 1f - Mathf.Clamp01(m.T / Game.BqClimb);
                return k * k * BqSky;
            }
            return 0f;
        }

        /// Eight jointed legs round c, facing f; `r` the body's size, `walk` the stepping phase.
        void BqLegs(V2 c, V2 f, V2 side, float r, float walk, float w, Rgba under, Rgba lit)
        {
            for (int k = 0; k < 2; k++)
            {
                float sgn = k == 0 ? 1f : -1f;
                for (int i = 0; i < 4; i++)
                {
                    float fan = -0.95f + 0.62f * i;
                    float step = Mathf.Sin(walk + i * 1.6f + k * 3.14f) * 0.2f;
                    V2 hip = c + f * (r * (0.3f - 0.18f * i)) + side * (sgn * r * 0.3f);
                    V2 dir = (f * Mathf.Sin(-fan + step) + side * (sgn * Mathf.Cos(-fan + step))).Normalized();
                    V2 knee = hip + dir * (r * 1.15f) - f * (r * 0.05f) + new V2(0f, -r * 0.3f);
                    V2 foot = hip + dir * (r * 2.1f) + f * (r * 0.4f * (i < 2 ? 1f : -1f));
                    Line(hip, knee, w + 1.5f, under);
                    Line(knee, foot, w + 0.8f, under);
                    if (lit.A > 0f)
                    {
                        Line(hip, knee, w * 0.4f, lit);
                        Line(knee, foot, w * 0.35f, lit);
                        Circle(knee, w * 0.55f, lit, 6);
                    }
                }
            }
        }

        /// The Brood Queen (DrawMinion).
        void DrawBroodQueen(Minion m, float t)
        {
            float lift = BqLift(m), r = m.Def.Radius;
            V2 ground = m.Disp, c = ground - new V2(0f, lift);
            V2.Axes(m.Aim, out V2 f, out V2 side);
            if (!fxOn)
            {
                Circle(ground, r * 0.9f, new Rgba(0f, 0f, 0f, 0.25f), 16);
                Circle(c, r, BqBack, 8);
                Arc(c, r, 2f, BqRim, 8);
                return;
            }
            float land = 1f - Mathf.Clamp01(lift / BqSky);
            Ellipse(ground + new V2(0f, r * 0.35f), r * (1.1f + 0.9f * land), r * (0.45f + 0.35f * land), 0f, new Rgba(0f, 0f, 0f, 0.15f + 0.3f * land), 18);
            if (lift > 0.5f)
                Line(c - new V2(0f, BqSky + 200f), c, 1.2f, BqSilk.WithAlpha(0.8f));
            GlowDisc(c, r * 3.2f, BqRim, 0.22f);
            // Her prey: gold brackets.
            Enemy prey;
            if (lift <= 0.5f && m.LinkTo != 0 && game.Enemies.TryGetValue(m.LinkTo, out prey))
            {
                float s = prey.Radius + 9f, a = 6f;
                for (int i = 0; i < 4; i++)
                {
                    float dx = i == 0 || i == 3 ? -1f : 1f, dy = i < 2 ? -1f : 1f;
                    V2 corner = prey.Disp + new V2(dx * s, dy * s);
                    Line(corner, corner - new V2(dx * a, 0f), 2f, BqGold);
                    Line(corner, corner - new V2(0f, dy * a), 2f, BqGold);
                }
            }
            float walk = m.Stacks == 1 ? t * 9f + m.Id : m.Stacks == 2 ? Mathf.Sin(t * 3f) * 0.6f : t * 2f;
            BqLegs(c, f, side, r, walk, 3.2f, BqLeg, BqLegLit.WithAlpha(0.9f));
            // Abdomen: dark violet, a violet rim, faint stripes and the gold hourglass.
            V2 belly = c - f * (r * 0.95f);
            float bodyRot = m.Aim + MathX.Pi / 2f;
            Ellipse(belly, r * 0.9f + 1.5f, r * 1.2f + 1.5f, bodyRot, BqRim, 20);
            Ellipse(belly, r * 0.9f, r * 1.2f, bodyRot, BqBody, 20);
            for (int i = 1; i <= 2; i++)
            {
                V2 row = belly - f * (r * 0.3f * i);
                Line(row - side * (r * (0.75f - 0.2f * i)), row + side * (r * (0.75f - 0.2f * i)), 1.2f, BqRim.WithAlpha(0.35f));
            }
            V2 hg = belly + f * (r * 0.05f);
            Tri2(hg, hg + f * (r * 0.42f) + side * (r * 0.26f), hg + f * (r * 0.42f) - side * (r * 0.26f), BqGold);
            Tri2(hg, hg - f * (r * 0.42f) + side * (r * 0.26f), hg - f * (r * 0.42f) - side * (r * 0.26f), BqGold);
            Ellipse(belly + f * (r * 0.4f) + side * (r * 0.3f), r * 0.18f, r * 0.35f, bodyRot, new Rgba(1f, 1f, 1f, 0.12f), 10);  // a sheen
            // Head and thorax, snapping forward on a bite.
            float lunge = Mathf.Clamp01(m.Wind);
            V2 head = c + f * (r * (0.25f + 0.3f * lunge));
            Circle(head, r * 0.68f + 1.5f, BqRim, 18);
            Circle(head, r * 0.68f, BqBack, 18);
            // The crown: five gold points along the back of the head.
            for (int i = -2; i <= 2; i++)
            {
                V2 b = head - f * (r * 0.45f) + side * (i * r * 0.16f);
                float tall = i == 0 ? 0.42f : i == -2 || i == 2 ? 0.26f : 0.32f;
                Tri2(b - side * (r * 0.09f), b + side * (r * 0.09f), b - f * (r * tall), BqGold);
            }
            // Fangs: two green hooks, wider apart as she bites.
            for (int k = -1; k <= 1; k += 2)
            {
                V2 root = head + f * (r * 0.5f) + side * (k * r * 0.2f);
                V2 mid = root + f * (r * 0.3f) + side * (k * r * (0.14f + 0.12f * lunge));
                V2 tip = mid + f * (r * 0.22f) - side * (k * r * 0.16f);
                Line(root, mid, 2.6f, BqFang);
                Line(mid, tip, 2f, BqFang);
            }
            // Eyes: two big, four small.
            for (int k = -1; k <= 1; k += 2)
            {
                GlowDisc(head + f * (r * 0.28f) + side * (k * r * 0.16f), r * 0.3f, BqEye, 0.35f);
                Circle(head + f * (r * 0.28f) + side * (k * r * 0.16f), r * 0.1f, BqEye, 8);
                Circle(head + f * (r * 0.12f) + side * (k * r * 0.33f), r * 0.07f, BqEye, 6);
                Circle(head + f * (r * 0.42f) + side * (k * r * 0.07f), r * 0.055f, BqEye, 6);
            }
            if (lift <= 0.5f && m.GuardFrac < 0.999f && m.GuardFrac > 0f)
                Bar(ground + new V2(-20f, -r - 18f), 40f, 4f, Mathf.Clamp01(m.GuardFrac), BqRim);
        }

        /// A broodling (DrawMinion): Stacks 0 running, 1 latched; Wind a bite.
        void DrawBroodling(Minion m, float t)
        {
            float r = m.Def.Radius;
            V2 c = m.Disp;
            V2.Axes(m.Aim, out V2 f, out V2 side);
            if (!fxOn)
            {
                Circle(c, r, BqBack, 8);
                return;
            }
            Ellipse(c + new V2(1f, r * 0.6f), r * 1.2f, r * 0.5f, 0f, new Rgba(0f, 0f, 0f, 0.28f), 10);
            BqLegs(c, f, side, r * 0.8f, t * (m.Stacks == 0 ? 30f : 12f) + m.Id, 1f, BqLegLit.WithAlpha(0.85f), new Rgba(0f, 0f, 0f, 0f));
            V2 belly = c - f * (r * 0.4f);
            Ellipse(belly, r * 0.6f + 1f, r * 0.8f + 1f, m.Aim + MathX.Pi / 2f, BqRim, 10);
            Ellipse(belly, r * 0.6f, r * 0.8f, m.Aim + MathX.Pi / 2f, BqBody, 10);
            Line(belly - f * (r * 0.3f), belly + f * (r * 0.3f), 1.2f, BqGold);
            V2 head = c + f * (r * 0.45f);
            Circle(head, r * 0.45f, BqBack, 8);
            Circle(head + f * (r * 0.2f) + side * (r * 0.15f), 0.8f, BqEye, 4);
            Circle(head + f * (r * 0.2f) - side * (r * 0.15f), 0.8f, BqEye, 4);
            if (m.Wind > 0f)
                GlowDisc(head + f * (r * 0.6f), r * 0.9f, BqFang, 0.5f * m.Wind);
        }

        /// A Brood Queen web on the floor (DrawZone).
        void DrawBroodWebFloor(Zone z, float t)
        {
            float age = z.Duration - z.T;
            float pop = Mathf.Clamp01(age / 0.18f), r = z.Radius * (0.6f + 0.4f * pop);
            float a = Mathf.Clamp01(z.T / 0.5f) * pop;
            if (!fxOn)
            {
                Arc(z.Pos, z.Radius, 1.5f, BqSilk.WithAlpha(0.5f * a), 24);
                return;
            }
            GlowDisc(z.Pos, r, BqRim, 0.12f * a);
            float rot = z.Pos.X * 0.031f + z.Pos.Y * 0.017f;
            const int spokes = 10;
            for (int i = 0; i < spokes; i++)
                Line(z.Pos, z.Pos + V2.FromAngle(rot + MathX.Tau * i / spokes) * r, 1.1f, BqSilk.WithAlpha(0.55f * a));
            for (int ring = 1; ring <= 4; ring++)
            {
                float rr = r * ring / 4f;
                for (int i = 0; i < spokes; i++)
                {
                    float sag0 = i % 2 == 0 ? 1f : 0.92f, sag1 = (i + 1) % 2 == 0 ? 1f : 0.92f;
                    V2 p0 = z.Pos + V2.FromAngle(rot + MathX.Tau * i / spokes) * (rr * sag0);
                    V2 p1 = z.Pos + V2.FromAngle(rot + MathX.Tau * (i + 1) / spokes) * (rr * sag1);
                    Line(p0, p1, 1f, BqSilk.WithAlpha(0.45f * a));
                }
            }
        }
    }
}
