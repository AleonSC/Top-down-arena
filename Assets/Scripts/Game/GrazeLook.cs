using UnityEngine;

namespace Arena
{
    /// FxKind.Graze (Core/Graze.cs): an enemy shot skimming past a player. A thin pale crescent flares round them where the
    /// shot went by (Pos; Pos2 their middle, Radius the distance, signed by the way it went), sliding round the way it flew, with a short speed streak and two flecks thrown off it.
    public partial class ArenaApp
    {
        static readonly Rgba GrazeCore = new Rgba(0.92f, 0.99f, 1f), GrazeEdge = new Rgba(0.45f, 0.85f, 1f);

        void DrawGrazeFx(Effect fx, float f)
        {
            V2 mid = fx.Pos2, n = fx.Pos - mid;
            float body = Mathf.Abs(fx.Radius), len = n.Length;
            if (len < 0.01f)
                return;
            n /= len;
            float sign = fx.Radius < 0f ? -1f : 1f;
            V2 along = n.Orthogonal() * sign;  // the way the shot went round them
            float e = 1f - (1f - f) * (1f - f);  // ease out
            float fade = 1f - f;
            float a0 = Mathf.Atan2(n.Y, n.X) + sign * 0.35f * e;  // slides round as it fades
            float r = body + 1f + 6f * e;
            const float sweep = 1.3f;
            Arc(mid, r + 1.5f, 5f * fade + 0.5f, GrazeEdge.WithAlpha(0.4f * fade), 12, a0 - sweep * 0.5f, sweep);
            Arc(mid, r, 2.6f * fade + 0.4f, GrazeCore.WithAlpha(fade), 12, a0 - sweep * 0.4f, sweep * 0.8f);

            // the speed streak: a bright line along the shot's way, just outside the crescent
            V2 at = mid + n * (r + 2f);
            float tail = 22f * (1f - e * 0.6f);
            Line(at - along * tail, at + along * (6f + 10f * e), 1.6f * fade + 0.3f, GrazeCore.WithAlpha(0.8f * fade));

            // two flecks thrown off outward
            for (int i = 0; i < 2; i++)
            {
                V2 d = (n * 0.8f + along * (i == 0 ? 0.6f : -0.2f)).Normalized();
                V2 p = mid + n * r + d * (4f + 16f * e);
                Circle(p, 1.6f * fade + 0.3f, GrazeCore.WithAlpha(fade), 6);
            }
        }
    }
}
