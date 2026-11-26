using UnityEngine;

namespace Arena
{
    /// The Nimbus (Docs/Mockups/nimbus-rework.html: look C, Anvil, and the Eye of the Storm Alt). Drawing only; the drift, the
    /// rain, the strikes and the hurricane are in Core/Nimbus.cs. The Minion's position is the ground point; the cloud floats
    /// above it.
    ///   Anvil: a three-tier cumulonimbus (dark belly, lit middle, a wide anvil top with a slow swirl), rain falling as streaked
    ///     curtains into a rimmed circle, and a forked bolt that leaves a scorch mark (FxKind.NimbusStrike).
    ///   Eye of the Storm: while an FxKind.NimbusStorm names this cloud, a hurricane is drawn instead: wind streaks and rain on
    ///     the ground, faint outer wisps, three arms of tapered blade strands with gaps between them (pointed at both ends,
    ///     the leading strand edged bright), a ring of crescent blades round the calm green eye, and debris flung round.
    public partial class ArenaApp
    {
        const float CloudHeight = 42f;

        static readonly Rgba CloudEdge = new Rgba(0.56f, 0.71f, 0.91f), CloudBody = new Rgba(0.2f, 0.26f, 0.36f),
            CloudBelly = new Rgba(0.13f, 0.17f, 0.25f), CloudTop = new Rgba(0.38f, 0.48f, 0.64f), CloudMid = new Rgba(0.24f, 0.31f, 0.43f),
            CloudAnvil = new Rgba(0.29f, 0.37f, 0.5f), CloudEye = new Rgba(0.75f, 0.88f, 1f), RainColor = new Rgba(0.6f, 0.78f, 1f),
            BoltWhite = new Rgba(0.9f, 0.96f, 1f), EyeGreen = new Rgba(0.47f, 0.9f, 0.59f), Debris = new Rgba(0.67f, 0.59f, 0.47f);
        // Anvil tiers, each blob {x, y, radius} from the cloud's middle: the belly at the bottom, the middle, the anvil on top.
        static readonly float[][] AnvilBase = { new[] { -38f, 10f, 12f }, new[] { -22f, 12f, 14f }, new[] { -4f, 13f, 15f }, new[] { 15f, 12f, 14f }, new[] { 33f, 10f, 12f }, new[] { -28f, 0f, 13f }, new[] { 30f, 1f, 12f } };
        static readonly float[][] AnvilMid = { new[] { -16f, -4f, 16f }, new[] { 4f, -6f, 18f }, new[] { 22f, -3f, 14f } };
        static readonly float[][] AnvilTop = { new[] { -40f, -20f, 8f }, new[] { -24f, -22f, 10f }, new[] { -6f, -24f, 11f }, new[] { 12f, -23f, 11f }, new[] { 30f, -21f, 10f }, new[] { 44f, -19f, 8f } };

        void DrawNimbus(Minion m, float t)
        {
            Player owner;
            game.Players.TryGetValue(m.Owner, out owner);
            V2 g = m.Disp;
            Effect storm = null;
            bool hot = false;
            foreach (var fx in game.Effects)
            {
                if (fx.Kind == FxKind.NimbusStorm && (int)fx.Pos2.X == m.Id && fx.T < fx.Dur)
                    storm = fx;
                if (fx.Kind == FxKind.NimbusStrike && fx.T < 0.12f && fx.Pos.DistanceTo(g) < 70f)
                    hot = true;  // a strike in the last instant makes the cloud flash
            }
            if (storm != null)
            {
                DrawNimbusStorm(g, storm, t);
                return;
            }
            float r = owner != null ? Game.NimbusRainRadius(owner) : Game.NimbusRain;
            float cy = g.Y - CloudHeight - 10f + Mathf.Sin(t * 2f) * 2f;
            // The rain's circle on the ground: a soft fill and a double rim.
            Circle(g, r, RainColor.WithAlpha(0.08f), 40);
            Arc(g, r, 3f, RainColor.WithAlpha(0.22f), 40);
            Arc(g, r - 5f, 1f, RainColor.WithAlpha(0.35f), 40);
            // Curtains of rain: six columns of streaks falling from the cloud's belly to the ground circle.
            for (int c = 0; c < 6; c++)
            {
                float x = g.X - r * 0.75f + c * (r * 1.5f / 5f), dx = x - g.X;
                float top = cy + 16f, bot = g.Y + Mathf.Sqrt(Mathf.Max(0f, r * r - dx * dx)) * 0.5f;
                for (int s = 0; s < 5; s++)
                {
                    float f = MathX.Fmod(t * 1.8f + s / 5f + c * 0.13f, 1f), y = top + (bot - top) * f;
                    Line(new V2(x, y - 12f), new V2(x, y), 1.4f, RainColor.WithAlpha(0.35f * (1f - f * 0.5f)));
                }
                for (int s = 0; s < 3; s++)
                {
                    float f = MathX.Fmod(t * 2.3f + s / 3f + c * 0.37f, 1f), y = top + (bot - top) * f;
                    Line(new V2(x + 5f, y - 7f), new V2(x + 5f, y), 1f, RainColor.WithAlpha(0.25f));
                }
            }
            // The cloud: a shadow, then the anvil top, the lit middle and the dark belly, each rimmed.
            Circle(g + new V2(0f, 4f), 36f, new Rgba(0f, 0f, 0f, 0.22f), 24);
            var edge = hot ? Rgba.White : CloudEdge;
            var flash = new Rgba(0.89f, 0.95f, 1f);
            Blobs(g.X, cy, AnvilTop, 2f, edge.WithAlpha(0.9f));
            Blobs(g.X, cy, AnvilTop, 0f, hot ? flash : CloudAnvil);
            Blobs(g.X, cy, AnvilMid, 2.5f, edge);
            Blobs(g.X, cy, AnvilMid, 0f, hot ? flash : CloudMid);
            foreach (var b in AnvilMid)
                Circle(new V2(g.X + b[0] - 3f, cy + b[1] - 5f), b[2] * 0.5f, hot ? Rgba.White : CloudTop, 12);
            Blobs(g.X, cy, AnvilBase, 2f, edge.WithAlpha(0.85f));
            Blobs(g.X, cy, AnvilBase, 0f, hot ? flash : CloudBelly);
            foreach (var b in AnvilBase)
                Circle(new V2(g.X + b[0], cy + b[1] - 4f), b[2] * 0.45f, hot ? flash : CloudBody, 12);
            // A slow swirl on the anvil, and two small eyes.
            for (int i = 0; i < 2; i++)
            {
                float a = t * 0.8f + i * MathX.Pi;
                Arc(new V2(g.X + 2f, cy - 22f), 14f + i * 8f, 1.4f, CloudEye.WithAlpha(0.35f), 12, a, 1.6f);
            }
            var eye = hot ? CloudBody : CloudEye;
            Circle(new V2(g.X - 7f, cy + 6f), 2.4f, eye, 8);
            Circle(new V2(g.X + 10f, cy + 6f), 2.4f, eye, 8);
        }

        void Blobs(float x, float y, float[][] blobs, float grow, Rgba c)
        {
            foreach (var b in blobs)
                Circle(new V2(x + b[0], y + b[1]), b[2] + grow, c, 14);
        }

        /// FxKind.Strike (the Storm Hammer's landing): a bolt from the sky to the ground point (Radius = the blast), a flash and a ring.
        void DrawStrikeFx(Effect fx, float f)
        {
            V2 hit = fx.Pos, top = hit + new V2(0f, -CloudHeight - 10f);
            float a = 1f - f;
            Circle(hit, fx.Radius * (0.6f + 0.4f * f), new Rgba(0.8f, 0.9f, 1f, 0.28f * a), 24);
            Arc(hit, fx.Radius * (0.5f + f), 3f, new Rgba(0.9f, 0.96f, 1f, a), 28);
            if (f < 0.55f)
                StrikeBolt(top, hit, Seed(hit), 6, 2.2f, 1f - f / 0.55f);
        }

        static int Seed(V2 p) => System.Math.Abs((int)(p.X * 7f + p.Y * 13f));

        /// A jagged bolt from a to b: a soft blue halo under a white core. Returns nothing; kinks come from the seed.
        void StrikeBolt(V2 a, V2 b, int seed, int steps, float width, float alpha)
        {
            V2 prev = a;
            for (int k = 1; k <= steps; k++)
            {
                V2 pt = k == steps ? b : V2.Lerp(a, b, k / (float)steps) + new V2(((seed * (k + 3)) % 17 - 8) * 1.4f, 0f);
                Line(prev, pt, width * 2.6f, new Rgba(0.6f, 0.78f, 1f, 0.4f * alpha));
                Line(prev, pt, width, BoltWhite.WithAlpha(alpha));
                prev = pt;
            }
        }

        /// FxKind.NimbusStrike: the Anvil's strike. In its first StrikeFxTime: a forked bolt from the cloud, a white flash, rings and
        /// sparks; for the rest of its life a scorch mark fades where it landed.
        void DrawNimbusStrikeFx(Effect fx)
        {
            V2 hit = fx.Pos, top = hit + new V2(0f, -CloudHeight - 20f);
            int seed = Seed(hit);
            float life = Game.NimbusStrikeFxTime, s = 1f - fx.T / life;
            // The scorch: a dark burn, cracks out of it and an ember rim that cools first.
            Circle(hit, 22f, new Rgba(0.04f, 0.03f, 0.02f, 0.55f * s), 20);
            Circle(hit, 12f, new Rgba(0.16f, 0.12f, 0.08f, 0.5f * s), 14);
            for (int i = 0; i < 6; i++)
            {
                float ang = i / 6f * MathX.Tau + seed, len = 28f + (seed * (i + 1)) % 9;
                V2 d = V2.FromAngle(ang);
                Line(hit + d * 10f, hit + d * len, 1.5f, new Rgba(0.04f, 0.03f, 0.02f, 0.6f * s));
            }
            Arc(hit, 22f, 1f, new Rgba(0.94f, 0.63f, 0.31f, 0.35f * s * s), 20);
            float u = fx.T / Game.StrikeFxTime;
            if (u >= 1f)
                return;
            float a = 1f - u, R = fx.Radius;
            Circle(hit, R * (0.6f + 0.4f * u), BoltWhite.WithAlpha(0.25f * a), 24);
            Arc(hit, R * (0.5f + u), 3f, BoltWhite.WithAlpha(a), 28);
            Arc(hit, R * (0.3f + u * 1.4f), 1.2f, RainColor.WithAlpha(a * 0.6f), 28);
            if (u < 0.15f)
                Circle(hit, R * 0.5f, Rgba.White.WithAlpha(1f - u / 0.15f), 16);
            for (int i = 0; i < 8; i++)
            {
                V2 d = V2.FromAngle(i / 8f * MathX.Tau + seed);
                float r0 = R * (0.4f + u * 0.6f);
                Line(hit + d * r0, hit + d * (r0 + 10f * a), 1.6f, BoltWhite.WithAlpha(a));
            }
            if (u < 0.55f)
            {
                float b = 1f - u / 0.55f;
                StrikeBolt(top, hit, seed, 7, 2.4f, b);
                // Two branches forking off the main bolt.
                for (int k = 2; k <= 4; k += 2)
                {
                    V2 from = V2.Lerp(top, hit, k / 7f) + new V2(((seed * (k + 3)) % 17 - 8) * 1.4f, 0f);
                    float dir = ((seed >> k) & 1) == 1 ? 1f : -1f;
                    StrikeBolt(from, from + new V2(dir * (18f + seed % 8), 14f + (seed >> 3) % 8), seed + k, 3, 1.3f, b * 0.8f);
                }
            }
        }

        /// A tapered blade from radius r0 to r1 round c, turning tw radians from angle a0, widest (w each side) at peak of its
        /// length and pointed at both ends; sy squashes it (1 = flat on the ground). Filled as one strip of quads.
        void StormBlade(V2 c, float r0, float r1, float a0, float tw, float w, Rgba col, float peak = 0.35f, bool rim = false)
        {
            const int Steps = 14;
            SetColor(col);
            V2 pIn = c + V2.FromAngle(a0) * r0, pOut = pIn;
            for (int i = 1; i <= Steps; i++)
            {
                float u = i / (float)Steps, r = r0 + (r1 - r0) * u, a = a0 + u * tw;
                float hw = w * (u < peak ? Mathf.Sin(u / peak * MathX.Pi / 2f) : Mathf.Cos((u - peak) / (1f - peak) * MathX.Pi / 2f));
                V2 n = V2.FromAngle(a), qIn = c + n * (r - hw), qOut = c + n * (r + hw);
                Quad(pIn, pOut, qOut, qIn);
                pIn = qIn;
                pOut = qOut;
            }
            if (!rim)
                return;
            // The leading edge, bright.
            V2 prev = c + V2.FromAngle(a0) * r0;
            for (int i = 1; i <= Steps; i++)
            {
                float u = i / (float)Steps, r = r0 + (r1 - r0) * u, a = a0 + u * tw;
                float hw = w * (u < peak ? Mathf.Sin(u / peak * MathX.Pi / 2f) : Mathf.Cos((u - peak) / (1f - peak) * MathX.Pi / 2f));
                V2 q = c + V2.FromAngle(a) * (r + hw);
                Line(prev, q, 1.3f, BoltWhite.WithAlpha(0.7f * col.A));
                prev = q;
            }
        }

        static Rgba StormTone(Rgba col, float a, bool flash, float alpha) => (flash ? col.Mix(Rgba.White, 0.35f) : col).WithAlpha(a * alpha);

        /// The Eye of the Storm: the hurricane at the cloud's ground point, spinning up over NimbusStormSpinUp and winding down
        /// over its last 0.45 s. fx.Radius is its full radius, fx.T how long it's blown.
        void DrawNimbusStorm(V2 c, Effect fx, float t)
        {
            float grow = Mathf.Clamp01(fx.T / Game.NimbusStormSpinUp), fade = Mathf.Clamp01((fx.Dur - fx.T) / 0.45f);
            grow = 1f - (1f - grow) * (1f - grow) * (1f - grow);
            float R = fx.Radius * grow * (0.4f + 0.6f * fade), g = R / Game.NimbusStormRadius, rot = fx.T * 3.2f, alpha = 0.35f + 0.65f * fade;
            if (R < 4f)
                return;
            bool flash = false;
            foreach (var e in FxOf(FxKind.Bolt))
                if (e.Kind == FxKind.Bolt && e.T < 0.06f && e.Pos.DistanceTo(c) < R)
                    flash = true;  // a bolt from the arms lights the whole storm
            // Ground: shadow, wind streaks racing round underneath, rain under the bands.
            Circle(c, R * 1.05f, CloudBelly.WithAlpha(0.35f * alpha), 40);
            for (int i = 0; i < 34; i++)
            {
                float rr = R * (0.3f + ((i * 53) % 68) / 100f), a = rot * (2.2f - rr / R) + i * 1.37f;
                Arc(c, rr, 1.3f, RainColor.WithAlpha(0.28f * alpha), 6, a, 0.35f + ((i * 7) % 5) * 0.06f);
            }
            for (int i = 0; i < 40; i++)
            {
                float f = MathX.Fmod(t * 1.6f + i * 0.13f, 1f), rr = R * (0.25f + ((i * 29) % 70) / 100f), a = rot + i * 2.1f;
                V2 p = c + V2.FromAngle(a) * rr;
                Line(new V2(p.X - 3f, p.Y - 8f + 8f * f), new V2(p.X, p.Y + 8f * f), 1.2f, RainColor.WithAlpha(0.45f * (1f - f) * alpha));
            }
            float eyeR = 24f * g;
            // Outer wisps.
            for (int i = 0; i < 9; i++)
                StormBlade(c, R * 0.72f, R, rot * 0.9f + i * MathX.Tau / 9f, 0.9f, 3f * g, StormTone(CloudEdge, 0.3f, flash, alpha));
            // Three arms, each four strands with gaps, the leading one edged; a faint shadow under each.
            for (int arm = 0; arm < 3; arm++)
                for (int k = 0; k < 4; k++)
                {
                    float a0 = rot + arm * MathX.Tau / 3f + k * 0.22f, r0 = eyeR + (2f + k * 7f) * g, r1 = R * (0.78f + 0.06f * k);
                    float tw = 2.8f - k * 0.25f, w = (10f - k * 2f) * g;
                    StormBlade(c + new V2(5f, 7f), r0, r1, a0, tw, w, new Rgba(0f, 0f, 0f, 0.14f * alpha));
                    StormBlade(c, r0, r1, a0, tw, w, StormTone(k % 2 == 1 ? CloudBody : CloudTop, 0.62f - k * 0.1f, flash, alpha), 0.35f, k == 0);
                }
            // The eyewall: crescent blades close round the eye.
            for (int i = 0; i < 10; i++)
                StormBlade(c, eyeR * 0.95f, eyeR + 16f * g, rot * 1.5f + i * MathX.Tau / 10f, 1.1f, 4.5f * g, StormTone(CloudEdge, 0.75f, flash, alpha));
            // The calm eye: clear ground, a green rim and gentle healing rain.
            Circle(c, eyeR * 0.9f, new Rgba(0.06f, 0.09f, 0.13f, alpha), 20);
            Circle(c, eyeR * 0.9f, EyeGreen.WithAlpha(0.14f * alpha), 20);
            Arc(c, eyeR * 0.9f, 1.5f, EyeGreen.WithAlpha(0.7f * alpha), 20);
            for (int i = 0; i < 5; i++)
            {
                float f = MathX.Fmod(t * 1.4f + i / 5f, 1f);
                V2 p = c + V2.FromAngle(i * 1.9f) * (eyeR * 0.45f);
                Line(new V2(p.X, p.Y - 17f + 12f * f), new V2(p.X, p.Y - 12f + 12f * f), 1.3f, EyeGreen.WithAlpha(0.8f * (1f - f) * alpha));
            }
            // Debris flung round.
            for (int i = 0; i < 16; i++)
            {
                float rr = R * (0.4f + ((i * 41) % 55) / 100f), a = rot * (2f - rr / R) * 1.2f + i * 2.4f;
                V2 p = c + V2.FromAngle(a) * rr;
                Line(p, p + new V2(-Mathf.Sin(a), Mathf.Cos(a)) * 7f, 2f, Debris.WithAlpha(0.8f * alpha));
            }
        }
    }
}
