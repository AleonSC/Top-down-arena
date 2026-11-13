using UnityEngine;

namespace Arena
{
    /// The Eclipse's faction, the Penumbral Court (Core/Court.cs), drawn as in Docs/Mockups/eclipse-faction.html with the
    /// boss's colours (EclipseLook.cs: sun gold, flare orange, sun white).
    ///   Ember: a flickering teardrop of flame pointing where it goes; its sparks (DrawEmberSpark) glow and cool as they hang.
    ///   Corona Priest: a robed cultist inside a turning halo of spikes that flares and lengthens while it winds up.
    ///   Heliograph: a robed figure holding a sun mirror out front; its line (DrawCourtFloor) flickers as a dashed red line
    ///   while it aims, then burns as a 40 px lane of fire that fades out.
    public partial class ArenaApp
    {
        static readonly Rgba CourtGold = new Rgba(1f, 0.82f, 0.4f), CourtOrange = new Rgba(1f, 0.54f, 0.24f), CourtSun = new Rgba(1f, 0.95f, 0.8f),
            CourtRed = new Rgba(1f, 0.31f, 0.23f), CourtRobe = new Rgba(0.15f, 0.11f, 0.07f), CourtTrim = new Rgba(0.55f, 0.42f, 0.24f);

        /// On the floor, under the enemies: a Heliograph's line.
        void DrawCourtFloor(Enemy e, V2 pos, float t)
        {
            if (e.Type != EnemyType.Heliograph || e.Phase != 1 && e.Phase != 2)
                return;
            V2 u = V2.FromAngle(e.Facing);
            V2 from = pos + u * e.Radius, tip = pos + u * Game.HelioLen;
            if (e.Phase == 1)
            {
                // The warning: a thin dashed line that flickers faster as it runs out, the lane's width faintly under it.
                float k = Mathf.Clamp01(e.SeenT / Game.HelioWarn);
                Line(from, tip, Game.HelioWidth, CourtRed.WithAlpha(0.04f + 0.08f * k));
                if (Mathf.Sin(t * (22f + 34f * k)) > -0.2f)
                    DashedLine(from, tip, 2f, CourtRed.WithAlpha(0.5f + 0.35f * k));
            }
            else
            {
                float k = Mathf.Clamp01(e.SeenT / Game.HelioBurn), a = 1f - k * k;
                float hum = 0.9f + 0.1f * Mathf.Sin(t * 40f + e.Id);
                Line(from, tip, Game.HelioWidth * 1.5f, CourtOrange.WithAlpha(0.18f * a * hum));
                Line(from, tip, Game.HelioWidth, CourtOrange.WithAlpha(0.55f * a));
                Line(from, tip, Game.HelioWidth * 0.4f, CourtGold.WithAlpha(0.9f * a));
                Line(from, tip, 3f, CourtSun.WithAlpha(a));
            }
        }

        /// An Ember's spark: a hot dot with a glow, cooling to red as it runs out.
        void DrawEmberSpark(Bullet b, float t)
        {
            float life = Mathf.Clamp01(b.Life / Mathf.Max(0.01f, b.Life0));
            var col = CourtRed.Mix(CourtOrange, life);
            float flick = 0.85f + 0.15f * Mathf.Sin(t * 30f + b.Id * 1.7f);
            Circle(b.Pos, b.Radius * 2.4f, col.WithAlpha(0.22f * flick), 10);
            Circle(b.Pos, b.Radius, col.WithAlpha(0.6f + 0.4f * life), 8);
            Circle(b.Pos, b.Radius * 0.45f, CourtSun.WithAlpha(0.4f + 0.6f * life), 6);
        }

        void DrawCourtBody(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            V2 f = V2.FromAngle(e.Facing), s = f.Orthogonal();
            switch (e.Type)
            {
                case EnemyType.Ember:
                {
                    float fl = 1f + 0.15f * Mathf.Sin(t * 20f + e.Id * 3f);
                    Circle(pos, r * 2f, CourtOrange.WithAlpha(0.12f), 14);
                    Flame(pos, f, s, r * 1.25f * fl, r * 1.05f, r * 0.85f, CourtOrange);
                    Flame(pos + f * (r * 0.15f), f, s, r * 0.75f * fl, r * 0.5f, r * 0.45f, CourtSun);
                    break;
                }
                case EnemyType.CoronaPriest:
                {
                    float wind = e.Winding ? 0.5f + 0.5f * Mathf.Sin(t * 16f) : 0f;
                    Circle(pos, r * 2.4f, CourtGold.WithAlpha(0.06f + 0.16f * wind), 18);
                    float spin = t * 0.8f + e.Id;
                    for (int k = 0; k < 12; k++)
                    {
                        V2 d = V2.FromAngle(spin + k * MathX.Tau / 12f);
                        float len = r * (0.3f + (k % 2) * 0.2f + 0.45f * wind);
                        Line(pos + d * (r * 1.2f), pos + d * (r * 1.2f + len), 2f, CourtGold.WithAlpha(0.6f + 0.4f * wind));
                    }
                    Arc(pos, r * 1.2f, 1.4f, CourtGold.WithAlpha(0.6f), 24);
                    Robe(pos, f, s, r, col);
                    break;
                }
                case EnemyType.Heliograph:
                {
                    bool aiming = e.Phase == 1;
                    float heat = aiming ? Mathf.Clamp01(e.SeenT / Game.HelioWarn) : 0f;
                    Circle(pos, r * 2f, CourtGold.WithAlpha(0.07f), 16);
                    Robe(pos, f, s, r, col);
                    // The mirror: a gold disc seen edge-on, out front, glowing as it aims.
                    V2 m = pos + f * (r * 1.15f);
                    if (aiming)
                        Circle(m, r * (0.8f + 0.9f * heat), CourtSun.WithAlpha(0.25f + 0.45f * heat), 14);
                    Line(m - s * (r * 0.95f), m + s * (r * 0.95f), r * 0.55f, CourtGold);
                    Line(m - s * (r * 0.6f), m + s * (r * 0.6f), r * 0.2f, CourtSun.WithAlpha(0.6f + 0.4f * heat));
                    break;
                }
            }
        }

        /// A teardrop of flame pointing along f: `len` ahead of `at`, `back` behind it, `wide` across.
        void Flame(V2 at, V2 f, V2 s, float len, float back, float wide, Rgba c)
        {
            V2 nose = at + f * len, tail = at - f * back, l = at - f * (back * 0.3f) + s * wide, rgt = at - f * (back * 0.3f) - s * wide;
            SetColor(c);
            Tri(nose, l, tail);
            Tri(nose, tail, rgt);
        }

        /// A Court cultist's robe: a teardrop pointing where it faces, dark with a gold trim, and the hood's opening.
        void Robe(V2 pos, V2 f, V2 s, float r, Rgba col)
        {
            V2 nose = pos + f * (r * 1.1f), back = pos - f * (r * 0.95f);
            V2 l = pos - f * (r * 0.55f) + s * (r * 0.9f), rgt = pos - f * (r * 0.55f) - s * (r * 0.9f);
            SetColor(CourtRobe);
            Tri(nose, l, back);
            Tri(nose, back, rgt);
            var trim = col.Mix(CourtTrim, 0.5f);
            Line(nose, l, 1.8f, trim);
            Line(l, back, 1.8f, trim);
            Line(back, rgt, 1.8f, trim);
            Line(rgt, nose, 1.8f, trim);
            Circle(pos - f * (r * 0.15f), r * 0.4f, new Rgba(0.04f, 0.03f, 0.02f), 14);
            Arc(pos - f * (r * 0.15f), r * 0.4f, 1.2f, CourtTrim, 14);
        }
    }
}
