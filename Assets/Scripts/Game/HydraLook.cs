using UnityEngine;

namespace Arena
{
    /// The Hydra (Hydra.cs; Docs/Mockups/summoner-sacrifice-rework.html concept B): a dark green body with a head on a
    /// curving neck for each head it has, each turned toward the enemy it is spitting at (worked out from the snapshot
    /// the same way the host picks them, Game.HydraTarget). It rises over half a second and sinks as it ends.
    public partial class ArenaApp
    {
        static readonly Rgba HydraDark = new Rgba(0.14f, 0.35f, 0.19f), HydraMid = new Rgba(0.24f, 0.54f, 0.29f), HydraLight = new Rgba(0.37f, 0.77f, 0.43f),
            HydraEye = new Rgba(1f, 0.89f, 0.42f);

        void DrawHydra(Minion m, float t)
        {
            float age = m.Duration > 0f ? m.Duration - m.T : 1f;
            float k = Mathf.Min(MathX.Clamp01(age / 0.5f), m.T >= 0f ? MathX.Clamp01(m.T / 0.5f) : 1f);
            if (k <= 0f)
                return;
            int heads = Game.HydraHeads(m);
            V2 b = m.Disp;
            Circle(b, 100f * k, m.Def.Color.WithAlpha(0.06f), 28);
            Circle(b + new V2(0f, 6f), 36f * k, new Rgba(0f, 0f, 0f, 0.3f), 20);
            V2[] hp = new V2[heads];
            float[] ha = new float[heads];
            for (int i = 0; i < heads; i++)
            {
                var tg = Game.HydraTarget(game.Enemies.Values, m, i);
                float rest = (i - (heads - 1) / 2f) * 0.7f;
                float a = tg != null ? (tg.Pos - m.Pos).Angle : rest + Mathf.Sin(t * 1.3f + i) * 0.2f;
                float len = (58f + 4f * Mathf.Sin(t * 3f + i * 2f)) * k;
                ha[i] = a;
                hp[i] = b + V2.FromAngle(a) * len;
                V2 n = V2.FromAngle(a).Orthogonal();
                V2 mid = V2.Lerp(b, hp[i], 0.5f) + n * (14f * Mathf.Sin(t * 2f + i) * k);
                V2 p0 = b;
                for (int s = 1; s <= 8; s++)  // the neck: a curve through a bent midpoint, dark under light
                {
                    float f = s / 8f;
                    V2 p1 = V2.Lerp(V2.Lerp(b, mid, f), V2.Lerp(mid, hp[i], f), f);
                    Line(p0, p1, 13f * k, HydraDark);
                    Line(p0, p1, 8f * k, HydraMid);
                    p0 = p1;
                }
            }
            Circle(b, 30f * k, HydraDark, 24);
            Circle(b + new V2(-2f, -1f), 19f * k, HydraMid, 20);
            for (int i = 0; i < heads; i++)
            {
                V2 dir = V2.FromAngle(ha[i]), side = dir.Orthogonal();
                Circle(hp[i], 12f * k, HydraDark, 14);
                Circle(hp[i] + dir * 1f, 10f * k, HydraLight, 14);
                float jaw = 2f + 2f * Mathf.Max(0f, Mathf.Sin(t * 5f + i * 1.7f));  // jaws working
                SetColor(HydraDark);
                Tri(hp[i] + dir * 7f + side * jaw, hp[i] + dir * 17f * k, hp[i] + dir * 7f);
                Tri(hp[i] + dir * 7f - side * jaw, hp[i] + dir * 17f * k, hp[i] + dir * 7f);
                Circle(hp[i] + dir * 2f + side * 5f, 2f * k, HydraEye, 6);
                Circle(hp[i] + dir * 2f - side * 5f, 2f * k, HydraEye, 6);
            }
            if (m.T >= 0f && m.Duration > 0f)
                Arc(b, 36f, 2f, m.Def.Color.WithAlpha(0.5f), 24, -MathX.Pi / 2f, MathX.Tau * MathX.Clamp01(m.T / m.Duration));
        }
    }
}
