using UnityEngine;

namespace Arena
{
    /// Rock Gloves's look (Core/RockGloves.cs; from its mockup). Drawing only, from the replicated GlvT, GlvAge and GlvPunch.
    ///   - Gather: lava cracks open in the ground round you and shards of earth rise from them, then fly in and lock together
    ///     into the two fists, which swell into place.
    ///   - Gloves: two giant fists of earth (Stone Colossus's stone, with glowing lava seams) hover at your sides, bobbing over
    ///     their shadows. A jab drives one fist out to the cursor and back, with dust streaks behind it; a Haymaker drives both.
    ///     A gold ring drains round you with the time left, and eight pips count the punches to the next Haymaker.
    ///   - Crumble warning: over the last GlvCrumbleWarn s the fists shake and shed chips. The hits, the crumble's shards and its
    ///     rubble are FxKind.TitanSlam's earth kinds (StoneColossusLook.cs).
    public partial class ArenaApp
    {
        const int GlvShards = 18;
        const float GlvScale = 1.35f;  // the fists' size against their drawing units
        static float GlvH(float n) { float x = Mathf.Sin(n * 91.7f + 47.3f) * 43758.5453f; return x - Mathf.Floor(x); }

        /// How far out a fist is for a swing k (0..1): fast out, slower back.
        static float GlvExt(float k)
        {
            if (k < 0f || k >= 1f)
                return 0f;
            float e = k < 0.35f ? k / 0.35f : 1f - (k - 0.35f) / 0.65f;
            return e * (2f - e);
        }

        /// Where fist `side` (-1 left, +1 right) is and how far out (0..1); `hay` while a Haymaker drives it.
        static V2 GlvFistAt(Player p, float side, float t, out float ext, out bool hay)
        {
            V2.Axes(p.Aim, out V2 X, out V2 Y);
            V2 c = p.Disp;
            V2 rest = c + X * Game.GlvFistAhead + Y * (side * Game.GlvFistSide) + new V2(0f, Mathf.Sin(t * 3f + side + p.Id) * 3.5f);
            int n = Game.GlvPunchNo(p);
            float swing = p.GlvPunch - n;
            ext = 0f;
            hay = false;
            if (n > 0 && Game.GlvIsHay(n))
            {
                hay = true;
                ext = GlvExt(swing);
                return V2.Lerp(rest, c + X * Game.GlvHayAhead + Y * (side * 26f), ext);
            }
            float mine = (n % 2 == 1) == (side > 0f) ? swing : -1f;  // this fist threw punch n...
            if (mine < 0f && n > 1 && !Game.GlvIsHay(n - 1) && swing > 0f)  // ...or the one before, still coming back
                mine = (swing * Game.GlvJabSwing + Game.GlvJabEvery) / Game.GlvJabSwing;
            if (n <= 0 || (swing <= 0f && mine <= 0f))
                return rest;
            ext = GlvExt(mine);
            V2 tip = c + X * (Game.GlvJabReach - 34f) + Y * (side * 12f);
            return V2.Lerp(rest, tip, ext);
        }

        /// One fist of earth facing `ang`, at c, scale sc (thumb on the inner side).
        void DrawGlvFist(V2 c, float ang, float side, float sc, float glow, float alpha)
        {
            V2.Axes(ang, out V2 X, out V2 Y);
            System.Func<float, float, V2> P = (x, y) => c + X * (x * sc) + Y * (y * sc);
            float seed = side > 0f ? 11f : 23f;
            TitanStone(P(-27f, 0f), 13f * sc, seed, 6, 0f, alpha);         // the wrist
            TitanStone(P(-5f, 0f), 21f * sc, seed + 1f, 7, glow, alpha);   // the back of the hand
            TitanStone(P(-4f, -side * 20f), 9.5f * sc, seed + 2f, 5, 0f, alpha);  // the thumb, toward you
            for (int i = 0; i < 4; i++)                                    // the knuckles
                TitanStone(P(15f, -15f + i * 10f), 8.5f * sc, seed + 3f + i, 5, 0f, alpha);
            float w = 2.2f * sc;
            var lava = Game.GlvLava;
            Line(P(-24f, -6f), P(-12f, -2f), w * 2.6f, lava.WithAlpha(0.2f * glow * alpha));
            Line(P(-12f, -2f), P(-2f, -11f), w * 2.6f, lava.WithAlpha(0.2f * glow * alpha));
            Line(P(-24f, -6f), P(-12f, -2f), w, lava.WithAlpha(0.95f * glow * alpha));
            Line(P(-12f, -2f), P(-2f, -11f), w, lava.WithAlpha(0.95f * glow * alpha));
            Line(P(-18f, 9f), P(-5f, 6f), w, lava.WithAlpha(0.8f * glow * alpha));
            Line(P(-5f, 6f), P(6f, 12f), w, lava.WithAlpha(0.8f * glow * alpha));
        }

        /// WorldRenderer.DrawPlayerAuras: the gather's cracks, the fists' shadows, the time ring and the Haymaker pips.
        void DrawRockGlovesUnder(Player p, float t)
        {
            if (!Game.GlvOn(p))
                return;
            V2 c = p.Disp;
            float r = p.Def.BodyR, age = p.GlvAge;
            if (age < Game.GlvGather + 0.6f)  // the cracks the shards come out of, cooling
            {
                float k = Mathf.Clamp01(age / 0.15f) * (1f - Mathf.Clamp01((age - Game.GlvGather) / 0.6f));
                for (int i = 0; i < GlvShards; i++)
                {
                    float a = GlvH(i + p.Id * 7) * MathX.Tau, d = 70f + GlvH(i * 3.3f + 1f) * 90f, l = 9f + GlvH(i * 5.1f) * 12f;
                    V2 at = c + V2.FromAngle(a) * d, dir = V2.FromAngle(a + 1.2f + GlvH(i * 1.7f));
                    V2 mid = at + dir * (l * 0.5f) + dir.Orthogonal() * 3f;
                    Line(at - dir * (l * 0.5f), mid, 2f, Game.GlvLava.WithAlpha(0.75f * k));
                    Line(mid, at + dir * l, 1.5f, Game.GlvLava.WithAlpha(0.6f * k));
                }
            }
            if (age < Game.GlvGather)
            {
                float k = Mathf.Clamp01(age / Game.GlvGather);
                Arc(c, r + 6f + 40f * (1f - EaseOut3(k)), 2f, Game.ColEarth.WithAlpha(0.3f + 0.4f * k), 40);
                return;
            }
            for (int s = -1; s <= 1; s += 2)
            {
                V2 f = GlvFistAt(p, s, t, out float ext, out bool hay);
                Circle(f + new V2(0f, 16f), 30f - 5f * ext, new Rgba(0f, 0f, 0f, 0.28f), 18);
            }
            float total = Mathf.Max(0.01f, p.GlvAge + p.GlvT - Game.GlvGather), left = Mathf.Clamp01(p.GlvT / total);
            Rgba ring = p.GlvT < Game.GlvCrumbleWarn && Mathf.Sin(t * 30f) > 0f ? Game.GlvLava : Game.BoltGold;
            Arc(c, r + 13f, 3f, ring.WithAlpha(0.18f), 40);
            Arc(c, r + 13f, 2.5f, ring.WithAlpha(0.9f), 40, -MathX.Pi / 2f, MathX.Tau * left);
            int n = Game.GlvPunchNo(p), done = n % Game.GlvHayEvery;
            if (Game.GlvIsHay(n) && p.GlvPunch - n > 0f)
                done = Game.GlvHayEvery;
            for (int i = 0; i < Game.GlvHayEvery; i++)
            {
                float a = -MathX.Pi / 2f + (i - (Game.GlvHayEvery - 1) * 0.5f) * 0.2f + MathX.Pi;  // a short row under you
                V2 at = c + V2.FromAngle(a) * (r + 21f);
                bool lit = i < done;
                Circle(at, lit ? 2.6f : 1.8f, (lit ? (i == Game.GlvHayEvery - 1 ? Game.GlvLava : Game.BoltGold) : Game.ColEarth).WithAlpha(lit ? 0.95f : 0.5f), 8);
            }
        }

        /// WorldRenderer, after the player's body: the gather's flying shards, then the fists.
        void DrawRockGlovesOver(Player p, float t)
        {
            if (!Game.GlvOn(p) || LaunchHides(p))
                return;
            V2 c = p.Disp;
            float age = p.GlvAge;
            TitanPaint(true);
            if (age < Game.GlvGather)
            {
                for (int i = 0; i < GlvShards; i++)
                {
                    float side = i % 2 == 0 ? -1f : 1f, delay = GlvH(i * 2.9f + 4f) * 0.2f;
                    float a = GlvH(i + p.Id * 7) * MathX.Tau, d = 70f + GlvH(i * 3.3f + 1f) * 90f;
                    V2 from = c + V2.FromAngle(a) * d;
                    V2 to = GlvFistAt(p, side, t, out _, out _);
                    float k = Mathf.Clamp01((age - 0.12f - delay) / 0.36f), e = k * k * (3f - 2f * k);
                    float rise = Mathf.Clamp01(age / (0.12f + delay));
                    V2 at = V2.Lerp(from, to, e) - new V2(0f, 14f * rise * (1f - e) + 8f * e);
                    if (fxOn && k > 0f && k < 1f)
                        Line(at, V2.Lerp(from, at, 0.5f), 2f, Game.ColEarth.WithAlpha(0.3f));
                    TitanStone(at, (5f + GlvH(i * 7.7f) * 5f) * (0.6f + 0.4f * rise), i * 13f + 5f, 5, 0f, rise);
                }
                float form = Mathf.Clamp01((age - 0.4f) / 0.2f);
                if (form > 0f)
                    for (int s = -1; s <= 1; s += 2)
                        DrawGlvFist(GlvFistAt(p, s, t, out _, out _) - new V2(0f, 8f), p.Aim, s, GlvScale * (0.6f + 0.4f * EaseOut3(form)), form, form);
                TitanPaint(false);
                return;
            }
            bool warn = p.GlvT < Game.GlvCrumbleWarn;
            for (int s = -1; s <= 1; s += 2)
            {
                V2 f = GlvFistAt(p, s, t, out float ext, out bool hay) - new V2(0f, 8f);
                if (warn && fxOn)
                    f += new V2(Mathf.Sin(t * 47f + s) * 1.6f, Mathf.Cos(t * 53f + s) * 1.2f);
                V2 X = V2.FromAngle(p.Aim);
                if (fxOn && ext > 0.3f)  // dust streaks behind a fist driving out
                    for (int j = -1; j <= 1; j++)
                    {
                        V2 o = X.Orthogonal() * (j * 11f);
                        Line(f - X * 80f + o, f - X * 38f + o, 2.5f, Game.ColEarth.WithAlpha(0.45f * ext));
                    }
                float glow = 0.6f + 0.4f * Mathf.Sin(t * 8f + s) + (hay ? 0.5f * ext : 0f);
                DrawGlvFist(f, p.Aim, s, GlvScale * (1f + ext * (hay ? 0.25f : 0.12f)), Mathf.Min(1.2f, glow), 1f);
                if (fxOn && ext > 0.6f)  // the knuckles white-hot at full reach
                {
                    V2 Y = X.Orthogonal();
                    Line(f + X * 32f - Y * 24f, f + X * 32f + Y * 24f, 3f, new Rgba(1f, 0.85f, 0.55f, 0.8f * ext));
                }
                if (warn && fxOn && Mathf.Repeat(t * 6f + s * 0.37f, 1f) < 0.25f)  // chips coming off
                {
                    float ph = Mathf.Repeat(t * 6f + s * 0.37f, 1f) / 0.25f;
                    TitanStone(f + new V2(GlvH(Mathf.Floor(t * 6f) + s) * 30f - 15f, 10f + ph * 16f), 3.5f, t, 4, 0f, 1f - ph);
                }
            }
            TitanPaint(false);
        }
    }
}
