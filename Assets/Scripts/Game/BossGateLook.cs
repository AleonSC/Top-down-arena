using UnityEngine;

namespace Arena
{
    /// The boss gate (Core/BossGate.cs), a summoning circle on the hub floor: two rune rings turning opposite ways, a
    /// seven-point star, a skull in the middle. It grows open over 0.9 s (timed from when this screen saw it), glows
    /// brighter and spins faster as the team's fill rises, and has a socket per standing player round its bottom, lit as
    /// each steps in, with a thread from each player inside. Reduced effects stops the turning.
    public partial class ArenaApp
    {
        static readonly Rgba BgRed = new Rgba(1f, 0.22f, 0.25f), BgHot = new Rgba(1f, 0.72f, 0.45f), BgDark = new Rgba(0.04f, 0.0f, 0.02f);

        float gateSeenAt = -1f;  // Time.time when this screen saw the gate open (-1: it's shut)
        internal float gateAgeOverride = -1f;  // Tools/Preview: a fixed age

        void DrawBossGate(float t)
        {
            if (!game.GateUp)
            {
                gateSeenAt = -1f;
                return;
            }
            if (gateSeenAt < 0f)
                gateSeenAt = Time.time;
            float age = gateAgeOverride >= 0f ? gateAgeOverride : Time.time - gateSeenAt;
            float open = Mathf.Clamp01(age / 0.9f), e = 1f - (1f - open) * (1f - open) * (1f - open);
            float fill = Mathf.Clamp01(game.GateFill), tt = fxReduced ? 0f : t * (1f + 2.5f * fill);
            float pulse = fxReduced ? 0.6f : 0.5f + 0.5f * Mathf.Sin(t * 3f);
            DrawGateSigil(game.GatePos, e, pulse, fill, tt);
            int alive, inside;
            game.GateCount(out alive, out inside);
            DrawGateSockets(game.GatePos, alive, inside, e, t);
        }

        void DrawGateSigil(V2 c, float e, float pulse, float fill, float t)
        {
            float R = Game.GateR * e;
            if (R < 1f)
                return;
            var red = BgRed.Mix(BgHot, fill * 0.6f);
            GlowDisc(c, R * 2.2f, BgRed, 0.25f + 0.12f * pulse + 0.35f * fill);
            Circle(c, R * 1.18f, BgDark.WithAlpha(0.75f), 56);
            Arc(c, R * 1.18f, 3f, red, 64);
            Arc(c, R * 1.05f, 1.5f, red.WithAlpha(0.7f), 64);
            for (int k = 0; k < 28; k++)  // rune marks between the outer rings, turning
            {
                float a = k * MathX.Tau / 28f + t * 0.25f;
                V2 d = V2.FromAngle(a), s = d.Orthogonal();
                V2 m = c + d * R * 1.115f;
                int kind = (int)(GateHash(k, 7, 2) * 3f);
                if (kind == 0)
                    Line(m - d * 4f, m + d * 4f, 1.8f, red);
                else if (kind == 1)
                {
                    Line(m - d * 4f + s * 3f, m + d * 4f, 1.8f, red);
                    Line(m - d * 4f - s * 3f, m + d * 4f, 1.8f, red);
                }
                else
                    Arc(m, 3f, 1.6f, red, 8);
            }
            for (int k = 0; k < 40; k++)  // the inner dashed ring, turning the other way
                Arc(c, R * 0.86f, 2f, red.WithAlpha(0.8f), 3, k * MathX.Tau / 40f - t * 0.4f, MathX.Tau / 40f * 0.5f);
            if (fill > 0f)  // the team's fill: the inner ring burns round from the top
                Arc(c, R * 0.95f, 5f, BgHot.WithAlpha(0.9f), 48, -MathX.Pi / 2f, MathX.Tau * fill);
            float sp = -t * 0.15f;  // a seven-point star, drawn as one stroke
            for (int k = 0; k < 7; k++)
            {
                V2 a = c + V2.FromAngle(sp + k * MathX.Tau / 7f - MathX.Pi / 2f) * R * 0.84f;
                V2 b = c + V2.FromAngle(sp + (k + 3) * MathX.Tau / 7f - MathX.Pi / 2f) * R * 0.84f;
                Line(a, b, 2.2f, red.WithAlpha(0.65f + 0.35f * pulse));
            }
            Circle(c, R * 0.36f, BgDark.WithAlpha(0.9f), 32);
            Arc(c, R * 0.36f, 2.5f, BgHot.WithAlpha(0.7f + 0.3f * pulse), 32);
            DrawGateSkull(c, R * 0.24f, BgHot.WithAlpha(0.85f + 0.15f * pulse));
        }

        /// A small skull: cranium, eye holes and teeth.
        void DrawGateSkull(V2 c, float s, Rgba col)
        {
            Circle(c + new V2(0f, -s * 0.15f), s, col, 20);
            FillRect(c.X - s * 0.55f, c.Y + s * 0.5f, s * 1.1f, s * 0.5f, col);
            Circle(c + new V2(-s * 0.4f, -s * 0.1f), s * 0.28f, BgDark, 12);
            Circle(c + new V2(s * 0.4f, -s * 0.1f), s * 0.28f, BgDark, 12);
            for (int k = -1; k <= 1; k++)
                FillRect(c.X + k * s * 0.3f - s * 0.04f, c.Y + s * 0.55f, s * 0.08f, s * 0.42f, BgDark);
        }

        /// A socket per standing player round the gate's bottom, lit as they step in; a thread from each player inside.
        void DrawGateSockets(V2 c, int alive, int inside, float e, float t)
        {
            float ring = Game.GateR * 1.6f;
            for (int k = 0; k < alive; k++)
            {
                float a = MathX.Pi / 2f + (k - (alive - 1) / 2f) * 0.45f;
                V2 at = c + V2.FromAngle(a) * ring;
                bool lit = k < inside;
                GlowDisc(at, 30f, lit ? BgHot : BgRed, lit ? 0.5f : 0.15f);
                Polygon(at, 13f * e, 4, 0.785f, lit ? BgHot : BgDark.WithAlpha(0.9f));
                Arc(at, 13f * e, 2.5f, lit ? Rgba.White : BgRed.WithAlpha(0.8f), 4, 0.785f, MathX.Tau);
            }
            foreach (var p in game.Players.Values)
                if (!p.Downed && p.Disp.DistanceTo(c) < Game.GateR)
                    Line(c, p.Disp, 2f, BgHot.WithAlpha(0.45f + 0.2f * Mathf.Sin(t * 6f)));
        }
    }
}
