using UnityEngine;

namespace Arena
{
    /// The Devourer's faction, The Larder (Core/LarderFaction.cs), drawn as in `Docs/Mockups/devourer-faction.html`:
    ///   Brood (Gnawling, Gulper): a lump of pink flesh that is mostly mouth, no eyes, a row of bone teeth on each lip, two
    ///     pale pustules. The Gnawling's mouth chomps as it runs; the Gulper's cheeks swell over the wind-up, then its mouth
    ///     gapes while it breathes in.
    ///   Butcher: a hooded robe, a stained apron on its front, a bone mask, a cleaver that rises over the wind-up and
    ///     sweeps across the front in the chop.
    /// DrawLarderFloor draws the tells under everyone: the Butcher's half-circle filling, the Gulper's cone and its breath.
    public partial class ArenaApp
    {
        static readonly Rgba LarderInk = new Rgba(0.12f, 0.04f, 0.06f, 0.95f), LarderMaw = new Rgba(0.16f, 0.03f, 0.06f),
            LarderBone = new Rgba(0.95f, 0.9f, 0.81f), LarderFlesh = new Rgba(0.91f, 0.53f, 0.6f), LarderBlood = new Rgba(0.67f, 0.12f, 0.18f),
            LarderApron = new Rgba(0.85f, 0.8f, 0.71f), LarderHood = new Rgba(0.23f, 0.14f, 0.13f), LarderSteel = new Rgba(0.81f, 0.84f, 0.85f);

        void DrawLarderBody(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            switch (e.Type)
            {
                case EnemyType.Gnawling:
                    DrawBrood(pos, r, e.Facing, 0.5f + 0.5f * Mathf.Sin(t * 16f + e.Id), col, e.Id, 1f);
                    break;
                case EnemyType.Gulper:
                {
                    float wind = Game.LarderWind(e);
                    bool breathing = e.LarderStage == Game.LarderActing;
                    float swell = breathing ? 0.1f : wind * 0.15f;
                    DrawBrood(pos, r * (1f + swell), e.Facing, breathing ? 1f : 0.12f * (1f - wind), col, e.Id, 1f + wind * 0.8f);
                    break;
                }
                case EnemyType.Butcher: DrawButcher(e, pos, r, col, t); break;
            }
        }

        /// A lump's outline: `n` points, each pushed out a little by a fixed jitter from `seed`.
        V2 LumpPt(V2 c, float r, int n, int i, float rot, int seed) =>
            c + V2.FromAngle(rot + MathX.Tau * i / n) * (r * (0.88f + 0.18f * OrbHash(seed * 17 + i * 5)));

        /// A brood body facing `face`: a fleshy lump, a mouth opened `open` (0-1) with teeth on both lips. `cheeks` puffs
        /// the sides (1 is none).
        void DrawBrood(V2 pos, float r, float face, float open, Rgba col, int seed, float cheeks)
        {
            const int n = 12;
            V2 f = V2.FromAngle(face), s = f.Orthogonal();
            SetColor(LarderInk);
            for (int i = 0; i < n; i++)
                Tri(pos, LumpPt(pos, r + 2f, n, i, face, seed), LumpPt(pos, r + 2f, n, i + 1, face, seed));
            SetColor(col.Mix(new Rgba(0.35f, 0.12f, 0.17f), 0.35f));
            for (int i = 0; i < n; i++)
                Tri(pos, LumpPt(pos, r, n, i, face, seed), LumpPt(pos, r, n, i + 1, face, seed));
            if (cheeks > 1.01f)
                for (int k = -1; k <= 1; k += 2)
                    Circle(pos + s * k * r * 0.45f + f * r * 0.15f, r * 0.42f * (cheeks - 0.6f), col.Mix(Rgba.White, 0.12f), 14);
            Circle(pos - f * r * 0.15f + s * r * 0.1f, r * 0.62f, col, 16);  // the lit middle
            Circle(pos + new V2(-r * 0.32f, -r * 0.36f), r * 0.25f, col.Mix(Rgba.White, 0.3f).WithAlpha(0.55f), 10);
            Circle(pos - f * r * 0.4f - s * r * 0.45f, r * 0.1f, new Rgba(1f, 0.88f, 0.84f, 0.55f), 8);  // pustules
            Circle(pos - f * r * 0.55f + s * r * 0.3f, r * 0.08f, new Rgba(1f, 0.88f, 0.84f, 0.5f), 8);
            // the mouth: a dark wedge from just behind the middle out past the front, teeth along both lips
            float half = 0.25f + open * 0.6f;
            V2 mc = pos - f * r * 0.05f;
            Wedge(mc, r * 1.02f, face, half, LarderMaw, 10);
            for (int k = -1; k <= 1; k += 2)
                for (int j = 1; j <= 3; j++)
                {
                    float ea = face + k * half;
                    V2 at = mc + V2.FromAngle(ea) * (r * (0.32f + j * 0.2f));
                    Tooth(at, ea - k * MathX.Pi / 2f, r * 0.2f);
                }
        }

        /// The Butcher (a Guard): a hooded robe, its apron, a bone mask, the cleaver up over the wind-up and across in the chop.
        void DrawButcher(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            V2 f = V2.FromAngle(e.Facing), s = f.Orthogonal();
            float wind = Game.LarderWind(e), chop = e.LarderStage == Game.LarderActing ? 1f - Mathf.Clamp01(e.LarderT / Game.ChopFlash) : -1f;
            Ellipse(pos, r * 1.05f + 1.8f, r * 1.15f + 1.8f, e.Facing, LarderInk, 16);
            Ellipse(pos, r * 1.05f, r * 1.15f, e.Facing, col, 16);
            Quad(pos + f * r * 0.15f - s * r * 0.55f, pos + f * r * 1.0f - s * r * 0.5f, pos + f * r * 1.0f + s * r * 0.5f, pos + f * r * 0.15f + s * r * 0.55f, LarderApron);
            Ellipse(pos + f * r * 0.7f + s * r * 0.18f, r * 0.3f, r * 0.2f, e.Facing + 0.5f, LarderBlood.WithAlpha(0.8f), 10);
            Circle(pos, r * 0.62f + 1.5f, LarderInk, 14);
            Circle(pos, r * 0.62f, LarderHood, 14);
            Ellipse(pos + f * r * 0.34f, r * 0.26f, r * 0.38f, e.Facing, LarderBone, 12);
            for (int k = -1; k <= 1; k += 2)
                Circle(pos + f * r * 0.42f + s * k * r * 0.14f, r * 0.08f, LarderMaw, 6);
            // the cleaver in its right hand: held forward, raised back over the wind-up, swept across in the chop
            float ca = chop >= 0f ? e.Facing + 1.4f - chop * 2.8f : e.Facing + 1.1f + wind * 0.9f;
            V2 hand = pos + V2.FromAngle(ca) * r * 0.95f, cf = V2.FromAngle(ca), cs = cf.Orthogonal();
            Line(pos + s * r * 0.5f, hand, 3.5f, LarderHood);
            Line(hand, hand + cf * r * 0.45f, 2.6f, new Rgba(0.29f, 0.17f, 0.11f));
            V2 b0 = hand + cf * r * 0.45f, b1 = hand + cf * r * 1.25f;
            Quad(b0 - cs * r * 0.2f, b1 - cs * r * 0.2f, b1 + cs * r * 0.45f, b0 + cs * r * 0.45f, LarderInk);
            Quad(b0 - cs * r * 0.12f, b1 - cs * r * 0.12f, b1 + cs * r * 0.37f, b0 + cs * r * 0.37f, LarderSteel);
            Quad(b1 - cs * r * 0.12f - cf * r * 0.3f, b1 - cs * r * 0.12f, b1 + cs * r * 0.1f, b1 + cs * r * 0.1f - cf * r * 0.3f, LarderBlood.WithAlpha(0.85f));
            if (wind > 0.6f && Mathf.Sin(t * 50f) > 0f)
                Circle(b1, 3f, Rgba.White.WithAlpha(0.8f), 8);  // a glint just before it falls
        }

        /// Under everyone: the Butcher's half-circle filling over its wind-up and its chop's white sweep; the Gulper's cone
        /// showing over its swell and filled while it breathes in, with streaks rushing to its mouth.
        void DrawLarderFloor(float t)
        {
            var red = new Rgba(1f, 0.35f, 0.42f);
            var pink = new Rgba(0.91f, 0.53f, 0.6f);
            foreach (var e in game.Enemies.Values)
            {
                if (e.Type == EnemyType.Butcher && e.LarderStage != Game.LarderIdle)
                {
                    V2 pos = e.Disp;
                    float half = MathX.Pi / 2f, R = Game.ChopReach;
                    if (e.LarderStage == Game.LarderWinding)
                    {
                        float w = Game.LarderWind(e);
                        Wedge(pos, R, e.Facing, half, red.WithAlpha(0.1f), 20);
                        Wedge(pos, R * w, e.Facing, half, red.WithAlpha(0.18f), 20);
                        Arc(pos, R, 2f, red.WithAlpha(0.75f), 20, e.Facing - half, half * 2f);
                    }
                    else
                    {
                        float u = 1f - Mathf.Clamp01(e.LarderT / Game.ChopFlash);
                        Wedge(pos, R, e.Facing, half, Rgba.White.WithAlpha(0.16f * (1f - u)), 20);
                        Arc(pos, R * 0.85f, 7f, Rgba.White.WithAlpha(0.9f * (1f - u * 0.5f)), 12, e.Facing + half - u * half * 2f - 0.5f, 0.5f);
                    }
                }
                else if (e.Type == EnemyType.Gulper && e.LarderStage != Game.LarderIdle)
                {
                    V2 pos = e.Disp;
                    float half = Game.GulpHalf, R = Game.GulpReach;
                    if (e.LarderStage == Game.LarderWinding)
                    {
                        float w = Game.LarderWind(e);
                        Wedge(pos, R, e.Facing, half, pink.WithAlpha(0.06f + 0.08f * w), 20);
                        Arc(pos, R, 1.5f, pink.WithAlpha(0.3f + 0.4f * w), 20, e.Facing - half, half * 2f);
                    }
                    else
                    {
                        Wedge(pos, R, e.Facing, half, pink.WithAlpha(0.2f), 20);
                        Arc(pos, R, 2f, pink.WithAlpha(0.8f), 20, e.Facing - half, half * 2f);
                        for (int i = 0; i < 9; i++)  // the breath: streaks rushing in to the mouth
                        {
                            float k = (t * 1.8f + i / 9f) % 1f, a = e.Facing + (i / 8f - 0.5f) * half * 1.8f, d = R * (1f - k);
                            if (d < e.Radius)
                                continue;
                            Line(pos + V2.FromAngle(a) * d, pos + V2.FromAngle(a) * (d + 18f), 1.5f, new Rgba(1f, 0.86f, 0.88f, 0.2f + 0.6f * k));
                        }
                    }
                }
            }
        }
    }
}
