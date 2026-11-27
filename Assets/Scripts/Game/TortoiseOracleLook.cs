using UnityEngine;

namespace Arena
{
    /// How Tortoise Oracle looks (the Healer Summoner's third ultimate, Core/TortoiseOracle.cs), from the mockup
    /// (Docs/Mockups/tortoise-oracle.html). Everything is drawn on the floor from the replicated zone (`ZoneKind.TortoiseOracle`:
    /// Pos the tortoise, Radius, T, Duration), under the arena's contents (DrawTortoiseOracle, from DrawZone):
    ///   - the circle: a jade pool with slow ripples drifting out, a clock face of 60 ticks turning slowly, the rim, a hand
    ///     sweeping round once over the whole cast and the time left as a gold arc outside the rim;
    ///   - the enemies inside wear a dashed amber ring (slowed), the allies inside a jade tempo ring pulsing out (double tempo),
    ///     and enemy shots over it an amber halo;
    ///   - the old tortoise in the middle: legs, tail, a slowly turning bearded head that blinks, a domed shell of hex scutes with a
    ///     glowing clock on its centre scute.
    public partial class ArenaApp
    {
        static readonly Rgba ToJade = new Rgba(0.37f, 0.88f, 0.73f), ToMint = new Rgba(0.55f, 0.95f, 0.8f), ToAmber = new Rgba(1f, 0.7f, 0.36f);
        static readonly Rgba ToShell = new Rgba(0.24f, 0.35f, 0.27f), ToScute = new Rgba(0.65f, 0.78f, 0.6f), ToSkin = new Rgba(0.47f, 0.58f, 0.45f);
        static readonly Rgba ToGold = new Rgba(1f, 0.78f, 0.35f);

        void DrawTortoiseOracle(Zone z, float t)
        {
            float age = Game.ToAge(z), reach = Game.ToReach(z), R = z.Radius;
            float fade = Mathf.Clamp01(z.T / 0.6f) * Mathf.Clamp01(age / 0.3f);
            float left = Mathf.Clamp01(z.T / z.Duration);
            if (!fxOn)
            {
                Circle(z.Pos, reach, ToJade.WithAlpha(0.18f * fade), 40);
                Arc(z.Pos, reach, 2f, ToJade.WithAlpha(0.8f * fade), 48);
                Arc(z.Pos, R + 8f, 3f, ToGold.WithAlpha(0.85f * fade), 48, -MathX.Pi * 0.5f, MathX.Tau * left);
                DrawTortoise(z.Pos, t, fade);
                return;
            }
            Circle(z.Pos, reach, ToJade.WithAlpha(0.13f * fade), 56);  // the pool, brighter towards the rim
            Arc(z.Pos, reach * 0.9f, reach * 0.2f, ToJade.WithAlpha(0.08f * fade), 56);
            for (int i = 0; i < 3; i++)  // slow ripples drifting out
            {
                float p = (t * 0.12f + i / 3f) % 1f;
                Arc(z.Pos, reach * p, 2f, ToMint.WithAlpha(0.2f * (1f - p) * fade), 48);
            }
            for (int i = 0; i < 60; i++)  // the clock face, turning slowly
            {
                float a = i * MathX.Tau / 60f + t * 0.05f;
                bool big = i % 5 == 0;
                V2 d = V2.FromAngle(a);
                Line(z.Pos + d * (reach - (big ? 16f : 8f)), z.Pos + d * reach, big ? 3f : 1.5f, ToMint.WithAlpha((big ? 0.65f : 0.4f) * fade));
            }
            Arc(z.Pos, reach, 3f, ToMint.WithAlpha(0.85f * fade), 64);  // the rim
            float hand = -MathX.Pi * 0.5f + MathX.Tau * (1f - left);  // the hand, once round over the cast
            Line(z.Pos, z.Pos + V2.FromAngle(hand) * reach, 2f, ToMint.WithAlpha(0.25f * fade));
            Arc(z.Pos, R + 9f, 4f, ToGold.WithAlpha(0.9f * fade), 64, -MathX.Pi * 0.5f, MathX.Tau * left);  // time left
            foreach (var b in game.Bullets.Values)  // enemy shots over it: an amber halo (they fly at half speed)
                if (Defs.IsEnemyShot(b.Kind) && Game.InOracle(z, b.Pos))
                    Arc(b.Pos, 9f, 1.5f, ToAmber.WithAlpha(0.6f * fade), 10);
            foreach (var e in game.Enemies.Values)  // slowed: a dashed amber ring
            {
                if (!Game.InOracle(z, e.Disp))
                    continue;
                float r = e.Radius + 7f;
                for (int k = 0; k < 8; k++)
                    Arc(e.Disp, r, 1.5f, ToAmber.WithAlpha(0.75f * fade), 3, k * MathX.Tau / 8f + t * 0.4f, MathX.Tau / 16f);
            }
            foreach (var p in game.Players.Values)  // double tempo: a jade ring pulsing out
            {
                if (p.Downed || !Game.InOracle(z, p.Disp))
                    continue;
                for (int k = 0; k < 2; k++)
                {
                    float ph = (t * 1.6f + k * 0.5f) % 1f;
                    Arc(p.Disp, p.Def.BodyR + 4f + ph * 14f, 2f, ToJade.WithAlpha(0.8f * (1f - ph) * fade), 20);
                }
            }
            DrawTortoise(z.Pos, t, fade);
        }

        /// The old tortoise, facing up the screen, at c.
        void DrawTortoise(V2 c, float t, float alpha)
        {
            float bob = Mathf.Sin(t * 0.8f) * 1.5f;
            if (fxOn)
                for (int i = 0; i < 4; i++)  // a soft glow round it
                    Circle(c, 90f - i * 18f, ToJade.WithAlpha(0.05f * alpha), 32);
            Ellipse(c + new V2(0f, 8f), 50f, 46f, 0f, new Rgba(0f, 0f, 0f, 0.3f * alpha), 24);  // its shadow
            Rgba skin = ToSkin.WithAlpha(alpha);
            Ellipse(c + new V2(-34f, -26f), 12f, 9f, 0.5f, skin, 12);  // legs
            Ellipse(c + new V2(34f, -26f), 12f, 9f, -0.5f, skin, 12);
            Ellipse(c + new V2(-34f, 30f), 12f, 9f, -0.5f, skin, 12);
            Ellipse(c + new V2(34f, 30f), 12f, 9f, 0.5f, skin, 12);
            SetColor(skin);
            Tri(c + new V2(-6f, 44f), c + new V2(0f, 57f), c + new V2(6f, 44f));  // tail
            float turn = Mathf.Sin(t * 0.35f) * 0.35f;  // the head, looking slowly round
            V2 fwd = V2.FromAngle(-MathX.Pi * 0.5f + turn), side = new V2(-fwd.Y, fwd.X);
            V2 head = c + fwd * 54f;
            Ellipse(head, 13f, 16f, -MathX.Pi * 0.5f + turn + MathX.Pi * 0.5f, ToSkin.Mix(new Rgba(1f, 1f, 1f), 0.08f).WithAlpha(alpha), 14);
            V2 eyeL = head + fwd * 5f + side * 5.5f, eyeR = head + fwd * 5f - side * 5.5f;
            Rgba ink = new Rgba(0.05f, 0.07f, 0.06f, alpha);
            if (t % 4.2f < 0.18f)  // a slow blink
            {
                Line(eyeL - side * 2.5f, eyeL + side * 2.5f, 1.5f, ink);
                Line(eyeR - side * 2.5f, eyeR + side * 2.5f, 1.5f, ink);
            }
            else
            {
                Circle(eyeL, 2.2f, ink, 8);
                Circle(eyeR, 2.2f, ink, 8);
            }
            SetColor(new Rgba(0.9f, 0.92f, 0.86f, 0.85f * alpha));  // its beard
            Tri(head - fwd * 7f + side * 5f, head - fwd * 7f - side * 5f, head - fwd * 19f);
            V2 s = c + new V2(0f, bob);  // the shell
            Ellipse(s, 44f, 48f, 0f, ToShell.WithAlpha(alpha), 28);
            Arc(s, 44f, 2.5f, ToScute.WithAlpha(alpha), 28);
            Rgba scute = ToScute.WithAlpha(0.55f * alpha);
            HexOutline(s, 14f, 1.5f, scute);
            for (int i = 0; i < 6; i++)
            {
                float a = i * MathX.Tau / 6f;
                HexOutline(s + new V2(Mathf.Cos(a) * 26f, Mathf.Sin(a) * 28f), 11f, 1.5f, scute);
            }
            Rgba glow = ToMint.WithAlpha(0.95f * alpha);  // the clock on the centre scute
            Arc(s, 9f, 2f, glow, 16);
            Line(s, s + V2.FromAngle(t * 0.6f) * 7f, 1.8f, glow);
            Line(s, s + V2.FromAngle(t * 0.05f) * 4.5f, 2f, glow);
        }
    }
}
