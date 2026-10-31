using UnityEngine;

namespace Arena
{
    /// How the Hive Queen's legendaries look (QueenLoot.cs): your Hive Drones (her drones, gone gold), Bomber
    /// Escort's bombers, and Royal Dive's honey pool. They borrow her look (QueenLook.cs: the bees, the wax cells).
    public partial class ArenaApp
    {
        static readonly Rgba HoneyGold = new Rgba(1f, 0.82f, 0.3f), BeeStripe = new Rgba(0.12f, 0.08f, 0f, 0.9f);

        /// A drone: a striped gold body facing where it flies, wings beating, a small sting glint when it's out.
        void DrawHiveDrone(Minion m, float t)
        {
            var c = m.GuardFrac <= 0f ? HoneyGold.Darkened(0.4f) : HoneyGold;
            float r = m.Def.Radius;
            V2 dir = V2.FromAngle(m.Aim), side = dir.Orthogonal();
            float beat = 0.5f + 0.5f * Mathf.Sin(t * 45f + m.Id);
            for (int k = -1; k <= 1; k += 2)  // wings, a blur either side
                Circle(m.Disp + side * (k * (r * 0.9f)) - dir * 2f, r * (0.55f + 0.2f * beat), new Rgba(0.9f, 0.98f, 1f, 0.35f + 0.25f * beat), 8);
            Circle(m.Disp, r, c, 10);
            Line(m.Disp - dir * 1.5f + side * r * 0.9f, m.Disp - dir * 1.5f - side * r * 0.9f, 1.6f, BeeStripe);
            Line(m.Disp - dir * 4.5f + side * r * 0.7f, m.Disp - dir * 4.5f - side * r * 0.7f, 1.4f, BeeStripe);
            Line(m.Disp - dir * r, m.Disp - dir * (r + 3f), 1.5f, BeeStripe);  // the sting
            Circle(m.Disp + dir * (r * 0.55f), 1.6f, BeeStripe, 6);  // its head
        }

        /// A bomber: her orange dart with a hot pulsing glow, a streak behind it and its fuse burning down.
        void DrawHiveBomber(Minion m, float t)
        {
            var c = m.Def.Color;
            float r = m.Def.Radius;
            V2 dir = V2.FromAngle(m.Aim);
            Circle(m.Disp, r + 5f + 2f * Mathf.Sin(t * 30f + m.Id), new Rgba(1f, 0.5f, 0.15f, 0.3f), 14);
            Line(m.Disp - dir * r, m.Disp - dir * (r + 26f), 3f, new Rgba(1f, 0.55f, 0.2f, 0.25f));
            Polygon(m.Disp, r, 3, m.Aim, c);
            Circle(m.Disp, r * 0.35f, new Rgba(1f, 0.92f, 0.6f, 0.6f + 0.4f * Mathf.Sin(t * 25f)), 8);  // the charge inside
            if (m.T >= 0f && m.Duration > 0f)
                Arc(m.Disp, r + 4f, 1.5f, c.WithAlpha(0.5f), 16, -MathX.Pi / 2f, MathX.Tau * MathX.Clamp01(m.T / m.Duration));
        }

        /// Royal Dive's honey pool: a glossy amber puddle with wax cells in it and a slow shine across it.
        void DrawHoneyPool(Zone z, Rgba c, float t)
        {
            Circle(z.Pos, z.Radius, c.WithAlpha(0.2f + 0.04f * Mathf.Sin(t * 3f)), 48);
            Circle(z.Pos, z.Radius * 0.7f, c.WithAlpha(0.1f), 40);
            const float s = 15f;  // wax cells (flat-topped, like HexOutline draws them): columns 1.5 s apart, rows sqrt(3) s
            int n = (int)(z.Radius / s) + 1;
            for (int i = -n; i <= n; i++)
                for (int j = -n; j <= n; j++)
                {
                    V2 at = z.Pos + new V2(i * 1.5f * s, (j + (i & 1) * 0.5f) * 1.732f * s);
                    if (at.DistanceTo(z.Pos) < z.Radius - s)
                        HexOutline(at, s - 2f, 1.5f, new Rgba(1f, 0.9f, 0.55f, 0.25f));
                }
            float shine = MathX.Fmod(t * 0.5f + z.Id * 0.37f, 1f);  // a slow glint drifting over the honey
            Arc(z.Pos, z.Radius * (0.3f + 0.5f * shine), 2f, new Rgba(1f, 0.97f, 0.8f, 0.35f * (1f - shine)), 24, -2.4f, 1.2f);
        }
    }
}
