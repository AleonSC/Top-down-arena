using UnityEngine;

namespace Arena
{
    /// The Singularity's faction (Core/Infall.cs), drawn as in Docs/Mockups/singularity-faction.html with the boss's colours
    /// (SingularityLook.cs: disk violet, rim cyan, horizon pink).
    ///   Debris: a jagged rock, dark with a violet rim, tumbling.   Acolyte: a hooded cultist holding a tiny black hole out
    ///   front that swells while it winds up.   Pulsar: a spinning neutron star; its two beams (DrawInfallFloor) flicker as
    ///   dashed red lines while it plants, then burn red with a white core while they sweep.
    public partial class ArenaApp
    {
        static readonly Rgba InfViolet = new Rgba(0.85f, 0.35f, 1f), InfPink = new Rgba(1f, 0.47f, 0.86f), InfCyan = new Rgba(0.5f, 0.84f, 1f),
            InfRed = new Rgba(1f, 0.35f, 0.42f), InfVoid = new Rgba(0.02f, 0.01f, 0.04f);

        /// On the floor, under the enemies: a Pulsar's beams.
        void DrawInfallFloor(Enemy e, V2 pos, float t)
        {
            if (e.Type != EnemyType.Pulsar || e.Phase != 1 && e.Phase != 2)
                return;
            V2 u = V2.FromAngle(e.Facing) * Game.PulsarBeamLen;
            for (int s = -1; s <= 1; s += 2)
            {
                V2 tip = pos + u * s;
                if (e.Phase == 1)
                {
                    // The warning: a dashed line that flickers faster as it runs out.
                    float k = Mathf.Clamp01(e.SeenT / Game.PulsarWarn);
                    float on = Mathf.Sin(t * (24f + 30f * k));
                    Line(pos, tip, 6f, InfRed.WithAlpha(0.08f + 0.1f * k));
                    if (on > -0.2f)
                        DashedLine(pos, tip, 2f, InfRed.WithAlpha(0.45f + 0.35f * k));
                }
                else
                {
                    float hum = 0.85f + 0.15f * Mathf.Sin(t * 30f + e.Id);
                    Line(pos, tip, Game.PulsarBeamWidth * 2.2f, InfRed.WithAlpha(0.18f * hum));
                    Line(pos, tip, Game.PulsarBeamWidth * 0.55f, InfRed.WithAlpha(0.9f));
                    Line(pos, tip, 1.8f, Rgba.White.WithAlpha(0.95f));
                }
            }
        }

        void DrawInfallBody(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            switch (e.Type)
            {
                case EnemyType.Debris:
                {
                    Circle(pos, r * 1.7f, InfViolet.WithAlpha(0.07f), 16);
                    const int n = 7;
                    var pts = new V2[n];
                    float rot = t * 2.4f + e.Id * 1.7f;
                    for (int i = 0; i < n; i++)
                    {
                        float rr = r * (0.8f + 0.3f * Mathf.Sin(i * 2.7f + e.Id * 5f));
                        pts[i] = pos + V2.FromAngle(rot + MathX.Tau * i / n) * rr;
                    }
                    SetColor(new Rgba(0.16f, 0.14f, 0.2f));
                    for (int i = 0; i < n; i++)
                        Tri(pos, pts[i], pts[(i + 1) % n]);
                    for (int i = 0; i < n; i++)
                        Line(pts[i], pts[(i + 1) % n], 2f, col);
                    Line(pos + V2.FromAngle(rot + 0.6f) * r * 0.2f, pos + V2.FromAngle(rot + 2.2f) * r * 0.5f, 1.2f, col.WithAlpha(0.45f));  // a crack
                    break;
                }
                case EnemyType.Acolyte:
                {
                    Circle(pos, r * 2.1f, InfPink.WithAlpha(0.06f), 18);
                    V2 f = V2.FromAngle(e.Facing), s = f.Orthogonal();
                    // The robe: a teardrop pointing where it faces.
                    V2 nose = pos + f * (r * 1.15f), back = pos - f * (r * 0.95f);
                    V2 l = pos - f * (r * 0.55f) + s * (r * 0.95f), rgt = pos - f * (r * 0.55f) - s * (r * 0.95f);
                    SetColor(new Rgba(0.11f, 0.08f, 0.15f));
                    Tri(nose, l, back);
                    Tri(nose, back, rgt);
                    Line(nose, l, 1.8f, col.Mix(new Rgba(0.55f, 0.42f, 0.66f), 0.5f));
                    Line(l, back, 1.8f, col.Mix(new Rgba(0.55f, 0.42f, 0.66f), 0.5f));
                    Line(back, rgt, 1.8f, col.Mix(new Rgba(0.55f, 0.42f, 0.66f), 0.5f));
                    Line(rgt, nose, 1.8f, col.Mix(new Rgba(0.55f, 0.42f, 0.66f), 0.5f));
                    Circle(pos - f * (r * 0.15f), r * 0.42f, InfVoid, 14);  // the hood's opening
                    Arc(pos - f * (r * 0.15f), r * 0.42f, 1.2f, new Rgba(0.35f, 0.26f, 0.44f), 14);
                    // The black hole it holds, swelling as it winds up.
                    float wind = e.Winding ? 0.5f + 0.5f * Mathf.Sin(t * 18f) : 0f;
                    float orb = r * (0.32f + 0.25f * wind);
                    V2 at = pos + f * (r * 1.5f);
                    Circle(at, orb * 2.6f, InfPink.WithAlpha(0.12f + 0.2f * wind), 16);
                    Circle(at, orb, InfVoid, 14);
                    Arc(at, orb, 1.6f, InfPink, 14);
                    break;
                }
                case EnemyType.Pulsar:
                {
                    bool live = e.Phase == 1 || e.Phase == 2;
                    Circle(pos, r * (2f + (live ? 0.4f * Mathf.Sin(t * 20f) : 0f)), InfCyan.WithAlpha(live ? 0.16f : 0.08f), 18);
                    Circle(pos, r, new Rgba(0.06f, 0.09f, 0.14f), 20);
                    Arc(pos, r, 2.2f, col, 20);
                    float spin = t * (live ? 14f : 6f);
                    for (int k = 0; k < 2; k++)
                        Arc(pos, r * 0.58f, 2f, InfCyan.WithAlpha(0.85f), 8, spin + k * MathX.Pi, 1.4f);
                    Circle(pos, r * 0.24f, Rgba.White, 10);
                    if (live)
                    {
                        // Its poles, where the beams leave.
                        V2 u = V2.FromAngle(e.Facing);
                        Circle(pos + u * r, 3f, InfRed, 8);
                        Circle(pos - u * r, 3f, InfRed, 8);
                    }
                    break;
                }
            }
        }
    }
}
