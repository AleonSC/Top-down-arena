using UnityEngine;

namespace Arena
{
    /// The Flamethrower's variants (Core/ElementGuns.cs): the Water Gun's jets and held nozzle, the Gale Gun's gusts and held blower.
    ///   Water Gun jet: a cyan streak with a white core, thinning and breaking into droplets as it nears its end.
    ///   Gale Gun gust: a pale curl of wind (a short crescent) that spins and widens as it flies, with a thin cutting edge.
    public partial class ArenaApp
    {
        static readonly Rgba WaterJetCol = new Rgba(0.31f, 0.82f, 0.91f), GaleCol = new Rgba(0.85f, 0.97f, 1f);

        void DrawWaterJet(Bullet b, float t)
        {
            float age = 1f - Mathf.Clamp01(b.Life / Mathf.Max(0.01f, b.Life0));
            V2 dir = b.Vel.IsZero ? new V2(1f, 0f) : b.Vel.Normalized();
            float len = 26f * (1f - 0.4f * age), a = 0.9f * (1f - age * age);
            Line(b.Pos - dir * len * 1.2f, b.Pos, 12f * (1f - 0.3f * age), WaterJetCol.WithAlpha(a * 0.22f));  // the spray's mist
            Line(b.Pos - dir * len, b.Pos, 7f * (1f - 0.4f * age), WaterJetCol.WithAlpha(a * 0.75f));
            Line(b.Pos - dir * len * 0.8f, b.Pos, 2.6f, new Rgba(0.9f, 0.98f, 1f, a));
            if (age > 0.55f)  // breaking up into droplets
            {
                V2 side = dir.Orthogonal();
                for (int k = 0; k < 2; k++)
                    Circle(b.Pos - dir * (6f + 5f * k) + side * (k == 0 ? 3f : -3f) * age, 1.8f, WaterJetCol.WithAlpha(a), 6);
            }
        }

        void DrawGaleShot(Bullet b, float t)
        {
            float age = 1f - Mathf.Clamp01(b.Life / Mathf.Max(0.01f, b.Life0));
            float r = 5f + 10f * age, a = 0.75f * (1f - age * age);
            float spin = (b.Vel.IsZero ? 0f : b.Vel.Angle) + age * 4f + b.Id;
            Arc(b.Pos, r, 2.2f, GaleCol.WithAlpha(a * 0.6f), 10, spin, 2.2f);   // the curl
            Arc(b.Pos, r * 0.6f, 1.2f, Rgba.White.WithAlpha(a), 8, spin + 1.4f, 1.8f);  // the cutting edge
        }

        void DrawHeldWaterGun(Player p, float t, int hold)
        {
            // A pump water gun: a cyan tank on top, a dark body and a brass nozzle with a drip.
            MRect(-6f, -3f, 10f, 6f, MDark); MRect(2f, -2.2f, 20f, 4.4f, Hx(0x2a6f8a)); MCirc(9f, -5.5f, 4.2f, Hx(0x4fd2e8, 0.9f), 10);
            MCirc(9f, -6.5f, 1.4f, Hx(0xe8fbff), 6); MRect(22f, -1.5f, 6f, 3f, Hx(0xc9a14a));
            float drip = Mathf.Repeat(t * 1.6f, 1f);
            MCirc(28f, 2f + drip * 4f, 1.2f * (1f - drip), Hx(0x4fd2e8), 6);
        }

        void DrawHeldGaleGun(Player p, float t, int hold)
        {
            // A leaf-blower: a grey intake drum with a spinning fan, a long pale tube, and wind curling out of its mouth.
            MRect(-6f, -3f, 10f, 6f, MDark); MCirc(2f, 0f, 6f, Hx(0x5a6470), 12); MCirc(2f, 0f, 4.2f, Hx(0x2a2f36), 10);
            for (int k = 0; k < 3; k++)
            {
                float a = t * 22f + k * 2.094f;
                MLine(2f, 0f, 2f + Mathf.Cos(a) * 4f, Mathf.Sin(a) * 4f, 1.2f, Hx(0xd8e4ee));
            }
            MRect(7f, -2.4f, 20f, 4.8f, Hx(0xb9c7d2)); MRect(25f, -3.2f, 4f, 6.4f, Hx(0x7c8894));
            float c = Mathf.Repeat(t * 3f, 1f);
            MArc(31f + c * 5f, 0f, 2f + c * 3f, -1.2f, 1.2f, 1.2f, Hx(0xe8f7ff, 0.8f * (1f - c)));
        }
    }
}
