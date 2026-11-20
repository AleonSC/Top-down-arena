using UnityEngine;

namespace Arena
{
    /// The Fireball's variants (Core/ElementBalls.cs): the Waterball in flight (a wobbling ball of water with a spray
    /// trail and a bright crest) and the Earthball (a tumbling lump of rock with glowing cracks and a dust trail), their
    /// particles, and both held in the hand.
    public partial class ArenaApp
    {
        /// The Waterball: a ball of water that wobbles as it rolls, deep blue at the rim, a pale crest on the front.
        void DrawWaterball(Bullet b, Rgba c, float t)
        {
            V2 dir = b.Vel.IsZero ? new V2(1f, 0f) : b.Vel.Normalized(), side = dir.Orthogonal();
            float r = b.Radius;
            var deep = new Rgba(0.08f, 0.3f, 0.7f);
            for (int k = 0; k < 6; k++)  // the trail: droplets of spray falling behind
            {
                float f = MathX.Fmod(t * 3.2f + k / 6f + b.Id * 0.17f, 1f);
                V2 at = b.Pos - dir * (r * 0.5f + f * r * 2.4f) + side * (Mathf.Sin(t * 9f + k * 2.3f) * r * 0.45f * f);
                Circle(at, r * (0.55f - 0.35f * f), c.Mix(deep, f).WithAlpha(0.65f * (1f - f)), 10);
            }
            Circle(b.Pos, r * 1.45f, c.WithAlpha(0.16f), 16);  // a mist round it
            float wob = 0.08f * Mathf.Sin(t * 14f + b.Id);  // it wobbles: a little longer along its path, then across
            for (int i = 0; i < 3; i++)  // the body, stretched along the way it rolls
            {
                float s = 1f - i * 0.03f;
                Circle(b.Pos + dir * (r * (wob * (i - 1))), r * s, i == 0 ? deep.Mix(c, 0.35f) : c.WithAlpha(0.6f), 16);
            }
            Circle(b.Pos + dir * (r * 0.2f), r * 0.62f, new Rgba(0.45f, 0.8f, 1f), 14);
            Circle(b.Pos + dir * (r * 0.35f) + side * (r * 0.25f), r * 0.26f, new Rgba(0.9f, 0.98f, 1f, 0.9f), 10);  // the glint on its crest
        }

        /// The Earthball: a heavy, tumbling lump of rock, its cracks glowing amber.
        void DrawEarthball(Bullet b, Rgba c, float t)
        {
            V2 dir = b.Vel.IsZero ? new V2(1f, 0f) : b.Vel.Normalized();
            float r = b.Radius, spin = t * 5f + b.Id;
            Circle(b.Pos + new V2(0f, r * 0.35f), r * 0.95f, new Rgba(0f, 0f, 0f, 0.3f), 16);  // its shadow on the ground
            for (int k = 0; k < 4; k++)  // pebbles kicked up behind it
            {
                float f = MathX.Fmod(t * 2.6f + k / 4f + b.Id * 0.11f, 1f);
                Polygon(b.Pos - dir * (r * 0.7f + f * r * 1.8f), r * (0.22f - 0.12f * f), 5, spin * 1.7f + k, c.Mix(new Rgba(0.35f, 0.28f, 0.22f), 0.5f).WithAlpha(1f - f));
            }
            Polygon(b.Pos, r, 8, spin, new Rgba(0.33f, 0.26f, 0.2f));
            Polygon(b.Pos + new V2(-r * 0.08f, -r * 0.08f), r * 0.86f, 8, spin, c);
            Polygon(b.Pos + new V2(-r * 0.25f, -r * 0.25f), r * 0.4f, 6, spin + 0.4f, new Rgba(0.86f, 0.7f, 0.48f));  // the lit top
            var glow = new Rgba(1f, 0.66f, 0.25f, 0.55f + 0.3f * Mathf.Sin(t * 6f + b.Id));
            for (int i = 0; i < 3; i++)  // the cracks
            {
                float a = spin + i * 2.1f;
                V2 o = V2.FromAngle(a) * (r * 0.15f), e = V2.FromAngle(a + 0.3f) * (r * 0.8f);
                Line(b.Pos + o, b.Pos + e, 1.6f, glow);
            }
        }

        /// The particles each ball sheds as it flies (EmitFromShot).
        void EmitElementBall(Bullet b, V2 back, float dt)
        {
            if (b.Kind == Defs.WaterballShot)
            {
                if (Rnd(0f, 1f) < dt * 28f)
                    AddPart(new Part { Kind = PSpark, Pos = b.Pos + back * (b.Radius * 0.6f) + new V2(Rnd(-5f, 5f), Rnd(-5f, 5f)), Vel = back * 50f + new V2(Rnd(-25f, 25f), Rnd(-25f, 25f)), Life = 0.4f, Max = 0.4f, Size = 2.2f, Col = new Rgba(0.6f, Rnd(0.8f, 0.95f), 1f) });
            }
            else if (Rnd(0f, 1f) < dt * 14f)
                AddPart(new Part { Kind = PDust, Pos = b.Pos + back * 10f, Vel = back * 25f + new V2(Rnd(-12f, 12f), Rnd(-12f, 12f)), Life = 0.6f, Max = 0.6f, Size = 3.5f, Col = new Rgba(0.62f, 0.5f, 0.38f) });
        }

        void DrawHeldWaterball(Player p, float t, int hold)
        {
            MCirc(12f, 0f, 9f, Hx(0x3aa0ff, 0.3f), 14); MCirc(12f, 0f, 6f, Hx(0x2f7fe0), 12); MCirc(13f, -1.5f, 2.6f, Hx(0xd8f2ff), 10);
            for (int i = 0; i < 4; i++) { float a = t * 3.5f + i * 1.57f; MArc(12f, 0f, 8f, a, a + 0.8f, 1.3f, Hx(0x9fd8ff, 0.8f)); }
        }

        void DrawHeldEarthball(Player p, float t, int hold)
        {
            MCirc(12f, 0f, 7.5f, Hx(0x54412f), 10); MCirc(11.5f, -0.5f, 6.4f, Hx(0xb8844d), 10); MCirc(10f, -2f, 2.6f, Hx(0xdcb37a), 8);
            float g = 0.6f + 0.4f * Mathf.Sin(t * 5f);
            MLine(9f, 1f, 14f, -1f, 1.2f, Hx(0xffa83f, g)); MLine(14f, -1f, 16f, 2.5f, 1.2f, Hx(0xffa83f, g));
            for (int i = 0; i < 3; i++) { float a = t * 2f + i * 2.09f; MCirc(12f + Mathf.Cos(a) * 11f, Mathf.Sin(a) * 11f, 1.4f, Hx(0x9a7a58), 5); }
        }
    }
}
