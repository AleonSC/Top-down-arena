using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Shield Wall as the "Phalanx" look (Docs/Mockups/shield-wall.html): seven steel plates slam down in a fan and lock edge
    /// to edge, each kicks back and flashes when a shot hits it, cracks and glows hot as it wears, and flies apart when it
    /// breaks, leaving a gap. Plate health comes from Player.WallPlate (replicated); everything else (the slam, the kick, the
    /// sparks) is local, found by watching that array change between frames.
    public partial class ArenaApp
    {
        class WallVis
        {
            public readonly byte[] Last = new byte[Player.WallPlates];
            public readonly float[] HitAt = new float[Player.WallPlates], CastAt = new float[Player.WallPlates];
            public bool Up;
        }

        readonly Dictionary<int, WallVis> wallVis = new Dictionary<int, WallVis>();
        static readonly Rgba WallLo = new Rgba(0.34f, 0.39f, 0.46f), WallHi = new Rgba(0.8f, 0.85f, 0.92f);

        /// The plate's angle across the arc, relative to the aim (0 = dead ahead).
        static float PlateAngle(int i, float half) => -half + (i + 0.5f) * (2f * half / Player.WallPlates);

        void DrawShieldWall(Player p, float r, float t)
        {
            if (!wallVis.TryGetValue(p.Id, out var v))
                wallVis[p.Id] = v = new WallVis();
            bool up = p.WallUp;
            float half = 1.05f * p.Stats.WallArcMult, pw = 2f * half / Player.WallPlates;
            float reach = r + 34f;
            for (int i = 0; i < Player.WallPlates; i++)
            {
                V2 at = p.Disp + V2.FromAngle(p.Aim + PlateAngle(i, half)) * reach;
                if (up && !v.Up)  // just raised: they land from the centre out
                {
                    v.CastAt[i] = t + Mathf.Abs(i - 3) * 0.045f;
                    v.HitAt[i] = -9f;
                    if (Juicy)
                    {
                        Sparks(at, new Rgba(1f, 0.86f, 0.55f), 4, 150f, 0.3f, 2f);
                        Ring(at, 16f, 0.25f, new Rgba(0.9f, 0.94f, 1f));
                    }
                }
                else if (p.WallPlate[i] < v.Last[i] && v.Up)
                {
                    v.HitAt[i] = t;
                    if (Juicy)
                    {
                        bool broke = p.WallPlate[i] == 0;
                        Sparks(at, broke ? new Rgba(1f, 0.6f, 0.3f) : new Rgba(1f, 0.85f, 0.5f), broke ? 16 : 6, broke ? 260f : 190f, 0.35f, broke ? 2.6f : 1.8f);
                        if (broke)
                            Ring(at, 24f, 0.3f, new Rgba(1f, 0.65f, 0.35f));
                    }
                }
                v.Last[i] = p.WallPlate[i];
            }
            v.Up = up;

            float sheet = 0f;
            for (int i = 0; i < Player.WallPlates; i++)
                sheet += p.WallPlate[i] / 255f;
            if (sheet <= 0f)
                return;
            Arc(p.Disp, reach, 17f, new Rgba(0.55f, 0.78f, 1f, 0.13f * sheet / Player.WallPlates), 20, p.Aim - half, 2f * half);

            for (int i = 0; i < Player.WallPlates; i++)
            {
                float hp = p.WallPlate[i] / 255f;
                if (hp <= 0f)
                    continue;
                float land = Mathf.Clamp01((t - v.CastAt[i]) / 0.2f);
                if (land <= 0f)
                    continue;
                float s = 1f - hp, a = p.Aim + PlateAngle(i, half);
                float slam = (1f - PlateSlam(land)) * 70f;
                float dtHit = t - v.HitAt[i];
                float kick = dtHit < 0.6f ? 7f * Mathf.Exp(-dtHit * 9f) : 0f, flash = dtHit < 0.6f ? Mathf.Exp(-dtHit * 14f) : 0f;
                float jit = 0f, jr = 0f;
                if (s > 0.65f)  // nearly gone: it rattles
                {
                    float step = (float)System.Math.Floor(t * 18f);
                    jit = (Hash01(i + step) - 0.5f) * (s - 0.65f) * 14f;
                    jr = (Hash01(i * 3f + step) - 0.5f) * (s - 0.65f) * 0.5f;
                }
                V2 radial = V2.FromAngle(a), tang = radial.Orthogonal();
                V2 c = p.Disp + radial * (reach - kick + slam + jit);
                float ca = Mathf.Cos(jr), sa = Mathf.Sin(jr);

                float wOut = reach * pw * 0.5f - 0.8f, wIn = wOut * 0.86f;  // a little narrower on the inside, so the fan locks
                Rgba lo = MixWhite(WallLo, flash), hi = MixWhite(WallHi, flash);
                Quad(PlatePt(c, radial, tang, ca, sa, -8f, -wIn), PlatePt(c, radial, tang, ca, sa, -8f, wIn), PlatePt(c, radial, tang, ca, sa, 0f, wIn * 1.04f), PlatePt(c, radial, tang, ca, sa, 0f, -wIn * 1.04f), lo);  // inner half, darker
                Quad(PlatePt(c, radial, tang, ca, sa, 0f, -wIn * 1.04f), PlatePt(c, radial, tang, ca, sa, 0f, wIn * 1.04f), PlatePt(c, radial, tang, ca, sa, 8f, wOut), PlatePt(c, radial, tang, ca, sa, 8f, -wOut), lo.Mix(hi, 0.7f));  // outer half, brighter
                Rgba rim = s > 0.5f ? new Rgba(1f, Mathf.Lerp(0.92f, 0.55f, (s - 0.5f) * 2f), Mathf.Lerp(0.98f, 0.3f, (s - 0.5f) * 2f)) : new Rgba(0.93f, 0.96f, 1f);
                Line(PlatePt(c, radial, tang, ca, sa, 8f, -wOut), PlatePt(c, radial, tang, ca, sa, 8f, wOut), 1.6f, rim);
                Line(PlatePt(c, radial, tang, ca, sa, -8f, -wIn), PlatePt(c, radial, tang, ca, sa, -8f, wIn), 1.2f, rim.WithAlpha(0.7f));
                Line(PlatePt(c, radial, tang, ca, sa, -8f, -wIn), PlatePt(c, radial, tang, ca, sa, 8f, -wOut), 1.2f, rim.WithAlpha(0.8f));
                Line(PlatePt(c, radial, tang, ca, sa, -8f, wIn), PlatePt(c, radial, tang, ca, sa, 8f, wOut), 1.2f, rim.WithAlpha(0.8f));
                Circle(PlatePt(c, radial, tang, ca, sa, -4.5f, -wIn * 0.5f), 1f, new Rgba(0.23f, 0.27f, 0.34f), 5);  // rivets
                Circle(PlatePt(c, radial, tang, ca, sa, -4.5f, wIn * 0.5f), 1f, new Rgba(0.23f, 0.27f, 0.34f), 5);
                if (i == Player.WallPlates / 2)  // the crest on the centre plate
                {
                    Line(PlatePt(c, radial, tang, ca, sa, -3f, 0f), PlatePt(c, radial, tang, ca, sa, 4f, 0f), 2f, new Rgba(1f, 0.71f, 0.28f));
                    Line(PlatePt(c, radial, tang, ca, sa, 0.5f, -3.5f), PlatePt(c, radial, tang, ca, sa, 0.5f, 3.5f), 2f, new Rgba(1f, 0.71f, 0.28f));
                }
                float thr = 0.18f + 0.1f * (i % 3);
                if (s > thr)  // cracks, longer as it wears
                {
                    float k = Mathf.Clamp01((s - thr) / 0.5f);
                    for (int cr = 0; cr < 2; cr++)
                    {
                        V2 prev = PlatePt(c, radial, tang, ca, sa, 7f, (Hash01(i * 5f + cr) - 0.5f) * 10f);
                        float x = 7f, y = (Hash01(i * 5f + cr) - 0.5f) * 10f;
                        int n = 1 + Mathf.RoundToInt(k * 3f);
                        for (int m = 0; m < n; m++)
                        {
                            x -= 3.2f;
                            y += (Hash01(i * 11f + cr * 7f + m) - 0.5f) * 6f;
                            V2 next = PlatePt(c, radial, tang, ca, sa, x, y);
                            Line(prev, next, 2.6f, new Rgba(1f, 0.55f, 0.24f, 0.55f * s));
                            Line(prev, next, 1f, new Rgba(0.05f, 0.07f, 0.09f, 0.95f));
                            prev = next;
                        }
                    }
                }
            }
        }

        /// A point on a plate: x along the radius, y across it, turned by its rattle.
        static V2 PlatePt(V2 c, V2 radial, V2 tang, float ca, float sa, float x, float y) => c + radial * (x * ca - y * sa) + tang * (x * sa + y * ca);

        static float PlateSlam(float x) { const float c = 2.2f; float u = x - 1f; return 1f + (c + 1f) * u * u * u + c * u * u; }
        static Rgba MixWhite(Rgba c, float k) => c.Mix(Rgba.White, k);

        void Quad(V2 a, V2 b, V2 c, V2 d, Rgba col)
        {
            SetColor(col);
            Quad(a, b, c, d);
        }
    }
}
