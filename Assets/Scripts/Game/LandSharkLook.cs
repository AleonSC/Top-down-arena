using UnityEngine;

namespace Arena
{
    /// Land Shark (the DPS Summoner's 4th choice, Core/LandShark.cs), from the Land Shark mockup (look B, Leap). Everything is drawn
    /// from the replicated zone (`ZoneKind.LandShark`: Pos, Radius, Aim, T, Duration), so it costs nothing on the network:
    ///   the puddle  dark water with a wobbling foam rim and ripples, spreading over Game.LandSharkGrow s, a gold dashed ring
    ///               round it until the bite, and draining away from Game.LandSharkDrainFrom s (DrawLandSharkPuddle, under the enemies);
    ///   the fin     a grey fin with its shadow and wake, circling in toward the middle until the shark leaps;
    ///   the leap    the shark in profile (belly down the screen) jumping across the puddle along the aim, its jaws opening on
    ///               the way up and snapping shut at the top, with its shadow on the ground and a splash where it leaves and
    ///               re-enters the water (DrawLandSharks, over the enemies).
    /// (The bite's damage numbers and stuns are the ordinary hit effects.) Visual effects off: a plain disc, a dot for the fin and
    /// an ellipse for the shark.
    public partial class ArenaApp
    {
        static readonly Rgba SharkWaterRim = new Rgba(0.16f, 0.5f, 0.83f), SharkWaterMid = new Rgba(0.06f, 0.23f, 0.43f), SharkWaterDeep = new Rgba(0.02f, 0.09f, 0.19f),
            SharkFoam = new Rgba(0.78f, 0.93f, 1f), SharkBody = new Rgba(0.38f, 0.47f, 0.56f), SharkBelly = new Rgba(0.91f, 0.93f, 0.95f),
            SharkEdge = new Rgba(0.13f, 0.19f, 0.25f), SharkMouth = new Rgba(0.42f, 0.07f, 0.13f), SharkTooth = new Rgba(0.97f, 0.95f, 0.9f),
            SharkGold = new Rgba(1f, 0.8f, 0.35f);

        /// The leap's ground span across the puddle, and its height at the top.
        const float SharkSpanK = 0.8f, SharkJump = 110f;

        /// Under the enemies (WorldEnemies.cs): the water, its rings and the circling fin.
        void DrawLandSharkPuddle(Zone z, float t)
        {
            float age = Game.LandSharkAge(z), r = Game.LandSharkReach(z);
            if (r < 1f)
                return;
            if (!fxOn)
            {
                Circle(z.Pos, r, SharkWaterMid.WithAlpha(0.8f), 24);
                Arc(z.Pos, r, 2f, SharkFoam.WithAlpha(0.8f), 24);
                if (age < Game.LandSharkBiteAt)
                    Arc(z.Pos, z.Radius + 6f, 2f, SharkGold.WithAlpha(0.7f), 24);
                return;
            }
            SharkWater(z.Pos, r, SharkWaterRim.WithAlpha(0.75f), age, z.Id);
            SharkWater(z.Pos, r * 0.85f, SharkWaterMid.WithAlpha(0.92f), age, z.Id + 3);
            SharkWater(z.Pos, r * 0.5f, SharkWaterDeep.WithAlpha(0.95f), age, z.Id + 7);
            for (int k = 0; k < 3; k++)  // ripples running out
            {
                float f = (age * 0.9f + k / 3f) % 1f, rr = r * (0.3f + 0.65f * f);
                Arc(z.Pos, rr, 1.5f, SharkFoam.WithAlpha(0.35f * (1f - rr / r)), 32);
            }
            SharkRim(z.Pos, r, age, z.Id);
            if (age < Game.LandSharkBiteAt)  // the telegraph: a gold dashed ring where the bite will land
            {
                float a = 0.25f + 0.5f * age / Game.LandSharkBiteAt;
                for (int d = 0; d < 16; d++)
                    Arc(z.Pos, z.Radius + 6f, 2f, SharkGold.WithAlpha(a), 4, d * MathX.Tau / 16f - age * 0.4f, MathX.Tau / 16f * 0.55f);
            }
            DrawSharkFin(z, age);
        }

        /// A disc of water whose edge wobbles.
        void SharkWater(V2 c, float r, Rgba col, float age, int seed)
        {
            const int n = 40;
            SetColor(col);
            V2 prev = c + SharkEdgeAt(0, n, r, age, seed);
            for (int i = 1; i <= n; i++)
            {
                V2 next = c + SharkEdgeAt(i, n, r, age, seed);
                Tri(c, prev, next);
                prev = next;
            }
        }

        static V2 SharkEdgeAt(int i, int n, float r, float age, int seed)
        {
            float a = MathX.Tau * i / n, s = seed * 0.37f;
            return V2.FromAngle(a) * (r * (1f + 0.04f * Mathf.Sin(a * 5f + age * 4f + s) + 0.025f * Mathf.Sin(a * 9f - age * 6f + s)));
        }

        /// The foam rim along the wobbling edge.
        void SharkRim(V2 c, float r, float age, int seed)
        {
            const int n = 40;
            V2 prev = c + SharkEdgeAt(0, n, r, age, seed);
            for (int i = 1; i <= n; i++)
            {
                V2 next = c + SharkEdgeAt(i, n, r, age, seed);
                Line(prev, next, 2.5f, SharkFoam.WithAlpha(0.85f));
                prev = next;
            }
        }

        /// The fin circling in, from Game.LandSharkFinFrom s until the shark leaps.
        void DrawSharkFin(Zone z, float age)
        {
            if (age < Game.LandSharkFinFrom || age >= Game.LandSharkLeapFrom)
                return;
            float p = (age - Game.LandSharkFinFrom) / (Game.LandSharkLeapFrom - Game.LandSharkFinFrom);
            float a = z.Aim + Mathf.PI + p * 5.2f, rr = z.Radius * (0.62f - 0.4f * KEo(p));
            V2 at = z.Pos + V2.FromAngle(a) * rr, u = V2.FromAngle(a + Mathf.PI / 2f), v = new V2(-u.Y, u.X);
            float rise = Mathf.Clamp01(p * 4f);
            Ellipse(at - u * 22f, 44f, 15f, u.Angle, SharkWaterDeep.WithAlpha(0.6f), 14);  // the body under the water
            for (int s = -1; s <= 1; s += 2)  // the wake
                Line(at - u * 6f + v * (s * 4f), at - u * 44f + v * (s * 16f), 2f, SharkFoam.WithAlpha(0.55f * rise));
            V2 up = new V2(0f, -1f), tip = at - u * 6f + up * (18f * rise);
            SetColor(SharkEdge);
            Tri(at + u * 9f, at - u * 13f, tip + up * 1.5f);
            SetColor(SharkBody);
            Tri(at + u * 7.5f, at - u * 11.5f, tip);
            GlowDisc(at, 10f, SharkFoam, 0.25f * rise);
        }

        /// Over the enemies (WorldRenderer.cs): each shark in the middle of its jump.
        void DrawLandSharks(float t)
        {
            foreach (var z in game.Zones.Values)
            {
                if (z.Kind != ZoneKind.LandShark)
                    continue;
                float u = Game.LandSharkLeap(z);
                if (u < 0f)
                    continue;
                DrawLandSharkLeap(z, u);
            }
        }

        void DrawLandSharkLeap(Zone z, float u)
        {
            V2 dir = V2.FromAngle(z.Aim);
            float span = z.Radius * 2f * SharkSpanK, size = z.Radius / Game.LandSharkRadius;
            V2 ground = z.Pos + dir * ((u - 0.5f) * span);
            float h = Mathf.Sin(Mathf.PI * u) * SharkJump * size;
            // which way it is heading on the screen: along the ground, and up then down
            V2 vel = dir * span + new V2(0f, -Mathf.PI * Mathf.Cos(Mathf.PI * u) * SharkJump * size);
            float open = u < 0.45f ? KEo(u / 0.45f) : Mathf.Max(0f, 1f - (u - 0.45f) / 0.1f);
            if (!fxOn)
            {
                Ellipse(ground - new V2(0f, h), 60f * size, 18f * size, vel.Angle, SharkBody, 14);
                return;
            }
            float lift = h / (SharkJump * size);
            Ellipse(ground + new V2(0f, 6f), 70f * size * (1f - 0.3f * lift), 14f * size, 0f, new Rgba(0f, 0f, 0f, 0.25f + 0.2f * (1f - lift)), 18);  // its shadow
            for (int end = -1; end <= 1; end += 2)  // the splash where it leaves and re-enters the water
            {
                float p = end < 0 ? u / 0.3f : (u - 0.7f) / 0.3f;
                if (p < 0f || p > 1f)
                    continue;
                V2 at = z.Pos + dir * (end * span * 0.5f);
                Ellipse(at, (30f + 40f * p) * size, (10f + 12f * p) * size, 0f, SharkFoam.WithAlpha(0.35f * (1f - p)), 18);
                for (int d = 0; d < 7; d++)
                {
                    float a = -Mathf.PI / 2f + (d - 3) * 0.35f, fly = p * (34f + 10f * KHash(z.Id * 3f + d + end));
                    GlowDisc(at + V2.FromAngle(a) * fly + new V2(0f, fly * fly * 0.012f), 3f, SharkFoam, 0.8f * (1f - p));
                }
            }
            V2 fwd = V2.FromAngle(vel.Angle);
            V2 side = dir.X >= 0f ? new V2(fwd.Y, -fwd.X) : new V2(-fwd.Y, fwd.X);  // the back faces up the screen, the belly down, all the way
            DrawSharkProfile(ground - new V2(0f, h), fwd, side, 1.15f * size, open);
        }

        /// The shark side on: `fwd` its nose, `up` its back, `k` its scale, `open` how wide its jaws are (0 shut).
        void DrawSharkProfile(V2 c, V2 fwd, V2 up, float k, float open)
        {
            System.Func<float, float, V2> P = (x, y) => c + fwd * (x * k) + up * (y * k);
            float jaw = -8f - 16f * open;
            // the tail and fins, behind the body
            SharkTri(P(-58f, 0f), P(-88f, 26f), P(-76f, 0f), SharkBody);
            SharkTri(P(-58f, 0f), P(-76f, 0f), P(-84f, -20f), SharkBody);
            SharkTri(P(-2f, 14f), P(-20f, 42f), P(-28f, 12f), SharkBody);
            SharkTri(P(12f, -10f), P(-6f, -32f), P(-12f, -11f), SharkBody);
            // the mouth: dark inside, teeth along both jaws
            if (open > 0.05f)
            {
                SharkTri(P(64f, -1f), P(30f, -5f), P(58f, jaw), SharkMouth);
                for (int i = 0; i < 5; i++)
                {
                    float q = i / 5f, x = 62f - q * 28f, y = -1f - q * 4f;
                    SharkTri(P(x, y), P(x - 5f, y), P(x - 2.5f, y - 6f), SharkTooth);
                    float lx = 56f - q * 24f, ly = jaw + q * (-5f - jaw);
                    SharkTri(P(lx, ly), P(lx - 5f, ly), P(lx - 2.5f, ly + 6f), SharkTooth);
                }
            }
            // the body (a fan from its middle)
            V2[] body =
            {
                P(-60f, 0f), P(-45f, 9f), P(-25f, 14f), P(0f, 15f), P(25f, 13f), P(45f, 8f), P(58f, 3f), P(64f, -1f),
                P(30f, -5f), P(58f, jaw), P(40f, -14f - 6f * open), P(15f, -16f), P(-15f, -14f), P(-40f, -8f),
            };
            V2 mid = P(0f, 0f);
            SetColor(SharkBody);
            for (int i = 0; i < body.Length; i++)
                Tri(mid, body[i], body[(i + 1) % body.Length]);
            // the pale belly
            V2[] belly = { P(-50f, -4f), P(-30f, -2f), P(0f, -3f), P(28f, -6f), P(46f, -12f - 4f * open), P(15f, -14f), P(-15f, -12f), P(-38f, -8f) };
            V2 bm = P(0f, -8f);
            SetColor(SharkBelly);
            for (int i = 0; i < belly.Length; i++)
                Tri(bm, belly[i], belly[(i + 1) % belly.Length]);
            for (int i = 0; i < body.Length; i++)  // the outline
                Line(body[i], body[(i + 1) % body.Length], 1.5f, SharkEdge);
            for (int g = 0; g < 3; g++)  // gills
                Line(P(18f - g * 5f, 7f), P(16f - g * 5f, -5f), 1.2f, SharkEdge);
            Circle(P(42f, 6f), 2.8f * k, SharkEdge, 10);  // the eye
            GlowDisc(P(43f, 7f), 1.2f * k, SharkTooth, 0.9f);
            if (open <= 0.05f)  // shut: the teeth meet along the seam
                for (int i = 0; i < 6; i++)
                {
                    float x = 60f - i * 5f, y = -2f - i * 0.6f;
                    SharkTri(P(x, y + (i % 2 == 0 ? 3f : -3f)), P(x - 2.5f, y), P(x + 2.5f, y), SharkTooth);
                }
        }

        void SharkTri(V2 a, V2 b, V2 c, Rgba col)
        {
            SetColor(col);
            Tri(a, b, c);
        }
    }
}
