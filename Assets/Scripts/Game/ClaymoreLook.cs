using UnityEngine;

namespace Arena
{
    /// Claymore (the DPS Soldier's 3rd choice, Core/Claymore.cs), from the Claymore mockup (session chat). Everything is drawn from
    /// the replicated zone (`ZoneKind.Claymore`: Pos, Aim, GuardFrac the state, Marched the mastered spare blast), so it costs
    /// nothing on the network:
    ///   the mine     an olive curved plate on two legs facing its cone, a red light that blinks once armed and strobes on the fuse;
    ///   arming       a gold ring filling round it and a dashed outline of the trigger cone;
    ///   armed        the trigger cone washed faint orange with two red laser tripwires down its edges;
    ///   tripped      the cone flashes red for the fuse;
    ///   the blast    (FxKind.ClaymoreBlast) a hot flash, a wide cone of fire rushing out to its reach and a spray of steel balls,
    ///                fading over 0.7 s (DrawClaymoreBlastFx).
    /// A mastered claymore with its spare blast left shows a second light on the plate. Visual effects off: the plate, a plain
    /// outline of the cone, and the blast as a flat cone.
    public partial class ArenaApp
    {
        static readonly Rgba ClayPlate = new Rgba(0.36f, 0.47f, 0.3f), ClayEdge = new Rgba(0.62f, 0.76f, 0.54f), ClayLeg = new Rgba(0.1f, 0.12f, 0.1f),
            ClayRed = new Rgba(1f, 0.25f, 0.25f), ClayWash = new Rgba(1f, 0.48f, 0.3f), ClayGold = new Rgba(1f, 0.8f, 0.35f),
            ClayHot = new Rgba(1f, 0.95f, 0.8f), ClayBall = new Rgba(0.85f, 0.88f, 0.95f);

        /// Under the enemies (WorldEnemies.cs): the cone and the mine.
        void DrawClaymore(Zone z, float t)
        {
            float state = z.GuardFrac;
            bool armed = state >= Game.ClaymoreArmed, tripped = state >= Game.ClaymoreTripped;
            float age = z.Duration - z.T;
            float reach = Game.ClaymoreTrigReach, half = Game.ClaymoreTrigHalf;
            if (!armed)  // arming: a dashed outline of the cone and a filling gold ring
            {
                if (fxOn)
                    ClayDashedCone(z.Pos, z.Aim, half, reach, ClayGold.WithAlpha(0.25f + 0.35f * state), t);
                else
                    ClayConeEdge(z.Pos, z.Aim, half, reach, ClayGold.WithAlpha(0.4f));
                Arc(z.Pos, 15f, 2f, ClayGold.WithAlpha(0.85f), 20, -MathX.Pi / 2f, MathX.Tau * state);
            }
            else if (tripped)
            {
                bool on = Mathf.Repeat(t * 30f, 1f) < 0.5f;
                ClayCone(z.Pos, z.Aim, half, reach, ClayRed.WithAlpha(on ? 0.35f : 0.2f));
                ClayConeEdge(z.Pos, z.Aim, half, reach, ClayRed.WithAlpha(0.9f));
            }
            else
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * 5f + z.Id);
                if (fxOn)
                {
                    ClayCone(z.Pos, z.Aim, half, reach, ClayWash.WithAlpha(0.06f + 0.05f * pulse));
                    for (int s = -1; s <= 1; s += 2)  // the laser tripwires
                    {
                        V2 end = z.Pos + V2.FromAngle(z.Aim + s * half) * reach;
                        Line(z.Pos, end, 2.5f, ClayRed.WithAlpha(0.12f));
                        Line(z.Pos, end, 1f, ClayRed.WithAlpha(0.75f));
                    }
                    Arc(z.Pos, reach, 1.2f, ClayWash.WithAlpha(0.3f), 12, z.Aim - half, half * 2f);
                }
                else
                    ClayConeEdge(z.Pos, z.Aim, half, reach, ClayWash.WithAlpha(0.6f));
            }
            DrawClaymoreBody(z.Pos, z.Aim, armed, tripped, z.Marched > 0f, age, t);
        }

        /// The mine itself: a curved plate bowed toward its cone, two legs behind, its light (and a mastered spare blast's).
        void DrawClaymoreBody(V2 c, float aim, bool armed, bool tripped, bool spare, float age, float t)
        {
            V2 f = V2.FromAngle(aim), side = new V2(-f.Y, f.X);
            float drop = Mathf.Clamp01(age / 0.15f), sc = 0.7f + 0.3f * drop;  // a small set-down
            for (int s = -1; s <= 1; s += 2)  // the legs
                Line(c - f * 3f * sc + side * (9f * s * sc), c - f * 12f * sc + side * (13f * s * sc), 2.5f, ClayLeg.WithAlpha(0.95f));
            const int n = 8;
            var outer = new V2[n + 1];
            var inner = new V2[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float u = (float)i / n * 2f - 1f;  // -1..1 along the plate
                float bow = (1f - u * u) * 5f;
                outer[i] = c + side * (u * 16f * sc) + f * ((bow + 2f) * sc);
                inner[i] = c + side * (u * 15f * sc) + f * ((bow - 5f) * sc);
            }
            SetColor(ClayPlate);
            for (int i = 0; i < n; i++)
                Quad(inner[i], outer[i], outer[i + 1], inner[i + 1]);
            for (int i = 0; i < n; i++)
                { Line(outer[i], outer[i + 1], 2f, ClayEdge); Line(inner[i], inner[i + 1], 1f, ClayLeg); }
            bool lit = tripped ? Mathf.Repeat(t * 30f, 1f) < 0.5f : armed && Mathf.Repeat(t * 1.25f, 1f) < 0.3f;
            V2 light = c + f * 1f * sc;
            if (lit && fxOn)
                GlowDisc(light, 9f, ClayRed, 0.5f);
            Circle(light, 2.6f, lit ? ClayRed : new Rgba(0.35f, 0.1f, 0.1f), 8);
            if (spare)
                Circle(light + side * 7f, 2f, ClayGold.WithAlpha(armed ? 0.95f : 0.5f), 6);
        }

        /// The blast (FxKind.ClaymoreBlast): Pos the mine, Pos2 one px along its facing, Radius its reach.
        void DrawClaymoreBlastFx(Effect fx)
        {
            float v = fx.T, k = Mathf.Clamp01(v / Mathf.Max(0.1f, fx.Dur));
            float aim = (fx.Pos2 - fx.Pos).Angle, half = Game.ClaymoreBlastHalf, reach = fx.Radius;
            float grow = Mathf.Clamp01(v / 0.12f), r = reach * (1f - (1f - grow) * (1f - grow));
            if (!fxOn)
            {
                ClayCone(fx.Pos, aim, half, r, ClayWash.WithAlpha(0.5f * (1f - k)));
                return;
            }
            if (v < 0.1f)
                GlowDisc(fx.Pos, 50f * (1f - v / 0.1f) + 14f, ClayHot, 0.85f);
            ClayCone(fx.Pos, aim, half, r, ClayWash.WithAlpha(0.45f * (1f - k) * (1f - k)));
            ClayCone(fx.Pos, aim, half * 0.75f, r * 0.6f, ClayHot.WithAlpha(0.55f * (1f - k) * (1f - k)));
            Arc(fx.Pos, r, 3f, ClayGold.WithAlpha(0.8f * (1f - k)), 16, aim - half, half * 2f);
            int seed = Mathf.RoundToInt(fx.Pos.X * 3.1f + fx.Pos.Y * 1.7f);
            for (int i = 0; i < 26; i++)  // the steel balls
            {
                float h1 = Mathf.Repeat(Mathf.Sin((seed + i) * 12.9898f) * 43758.55f, 1f), h2 = Mathf.Repeat(Mathf.Sin((seed + i) * 78.233f) * 12543.7f, 1f);
                float a = aim + (h1 * 2f - 1f) * half, d = r * (0.55f + 0.45f * h2);
                V2 at = fx.Pos + V2.FromAngle(a) * d;
                Line(at - V2.FromAngle(a) * 6f, at, 1.5f, ClayBall.WithAlpha(0.9f * (1f - k)));
            }
            Circle(fx.Pos, 9f, new Rgba(0.08f, 0.07f, 0.06f, 0.6f * (1f - k)), 10);  // the scorch where it stood
        }

        /// A filled cone (a fan of triangles).
        void ClayCone(V2 c, float aim, float half, float reach, Rgba col)
        {
            const int n = 12;
            SetColor(col);
            V2 prev = c + V2.FromAngle(aim - half) * reach;
            for (int i = 1; i <= n; i++)
            {
                V2 next = c + V2.FromAngle(aim - half + 2f * half * i / n) * reach;
                Tri(c, prev, next);
                prev = next;
            }
        }

        /// A cone's outline.
        void ClayConeEdge(V2 c, float aim, float half, float reach, Rgba col)
        {
            Line(c, c + V2.FromAngle(aim - half) * reach, 1.5f, col);
            Line(c, c + V2.FromAngle(aim + half) * reach, 1.5f, col);
            Arc(c, reach, 1.5f, col, 12, aim - half, half * 2f);
        }

        /// A cone's outline in marching dashes (arming).
        void ClayDashedCone(V2 c, float aim, float half, float reach, Rgba col, float t)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                V2 dir = V2.FromAngle(aim + s * half);
                for (float d = Mathf.Repeat(t * 40f, 16f); d < reach; d += 16f)
                    Line(c + dir * d, c + dir * Mathf.Min(reach, d + 8f), 1.5f, col);
            }
            for (int d = 0; d < 8; d++)
                Arc(c, reach, 1.5f, col, 3, aim - half + 2f * half * d / 8f, 2f * half / 16f);
        }
    }
}
