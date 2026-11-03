using UnityEngine;

namespace Arena
{
    /// The Nimbus (Docs/Mockups/totem-staff-rework.html, concept 1): a small storm cloud floating over its ground point, rain
    /// falling in a circle beneath it, and lightning from the cloud when you click. Drawing only; the cloud's drift, the rain's
    /// healing and the strike's damage are in Nimbus.cs. The Minion's position is the ground point; the cloud floats above it.
    public partial class ArenaApp
    {
        const float CloudHeight = 42f;

        static readonly Rgba CloudEdge = new Rgba(0.56f, 0.71f, 0.91f), CloudBody = new Rgba(0.2f, 0.26f, 0.36f),
            CloudEye = new Rgba(0.75f, 0.88f, 1f), RainColor = new Rgba(0.6f, 0.78f, 1f);
        static readonly float[][] CloudBlobs = { new[] { -24f, 6f, 13f }, new[] { -9f, -5f, 17f }, new[] { 12f, -3f, 16f }, new[] { 28f, 6f, 12f }, new[] { 2f, 9f, 15f } };

        void DrawNimbus(Minion m, float t)
        {
            Player owner;
            game.Players.TryGetValue(m.Owner, out owner);
            float r = Game.NimbusRain * (owner != null ? Game.AreaMult(owner) : 1f);
            V2 g = m.Disp;
            // A strike in the last instant makes the cloud flash.
            bool hot = false;
            foreach (var fx in game.Effects)
                if (fx.Kind == FxKind.Strike && fx.T < 0.12f && fx.Pos.DistanceTo(g) < 70f)
                    hot = true;
            // The rain's circle on the ground, and drops falling from the cloud into it.
            Circle(g, r, RainColor.WithAlpha(0.07f), 40);
            Arc(g, r, 2f, RainColor.WithAlpha(0.45f), 40);
            for (int i = 0; i < 16; i++)
            {
                float a = i * 2.39996f, rr = r * Mathf.Sqrt((i + 0.5f) / 16f) * 0.92f;
                V2 land = g + new V2(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr);
                float f = MathX.Fmod(t * 1.3f + i * 0.37f, 1f);
                float y = land.Y - CloudHeight + CloudHeight * f;
                Line(new V2(land.X - 1f * (1f - f), y - 8f), new V2(land.X, y), 1.6f, RainColor.WithAlpha(0.55f * (1f - 0.4f * f)));
                if (f > 0.88f)
                    Arc(land, 2f + (f - 0.88f) * 40f, 1f, RainColor.WithAlpha(0.5f), 8);
            }
            // The cloud: a shadow on the ground, then five blobs with a pale rim and two small eyes.
            Circle(g + new V2(0f, 4f), 22f, new Rgba(0f, 0f, 0f, 0.2f), 20);
            float cy = g.Y - CloudHeight + Mathf.Sin(t * 2f) * 2f;
            var edge = hot ? Rgba.White : CloudEdge;
            var body = hot ? new Rgba(0.89f, 0.95f, 1f) : CloudBody;
            foreach (var b in CloudBlobs)
                Circle(new V2(g.X + b[0], cy + b[1]), b[2] + 2f, edge, 14);
            foreach (var b in CloudBlobs)
                Circle(new V2(g.X + b[0], cy + b[1]), b[2], body, 14);
            var eye = hot ? CloudBody : CloudEye;
            Circle(new V2(g.X - 6f, cy + 6f), 2.6f, eye, 8);
            Circle(new V2(g.X + 9f, cy + 6f), 2.6f, eye, 8);
        }

        /// FxKind.Strike: a bolt from the cloud to the ground point (Radius = the blast), a flash, and a ring spreading.
        void DrawStrikeFx(Effect fx, float f)
        {
            V2 hit = fx.Pos, top = hit + new V2(0f, -CloudHeight - 10f);
            float a = 1f - f;
            Circle(hit, fx.Radius * (0.6f + 0.4f * f), new Rgba(0.8f, 0.9f, 1f, 0.28f * a), 24);
            Arc(hit, fx.Radius * (0.5f + f), 3f, new Rgba(0.9f, 0.96f, 1f, a), 28);
            if (f < 0.55f)
            {
                float bolt = 1f - f / 0.55f;
                int seed = System.Math.Abs((int)(hit.X * 7f + hit.Y * 13f));
                V2 prev = top;
                for (int k = 1; k <= 6; k++)
                {
                    V2 pt = k == 6 ? hit : V2.Lerp(top, hit, k / 6f) + new V2(((seed * (k + 3)) % 17 - 8) * 1.3f, 0f);
                    Line(prev, pt, 6f, new Rgba(0.6f, 0.78f, 1f, 0.45f * bolt));
                    Line(prev, pt, 2.2f, new Rgba(1f, 1f, 1f, bolt));
                    prev = pt;
                }
            }
        }
    }
}
