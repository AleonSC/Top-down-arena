using UnityEngine;

namespace Arena
{
    /// The Singularity's faction (Core/Infall.cs), drawn as in Docs/Mockups/singularity-faction.html with the boss's colours
    /// (SingularityLook.cs: disk violet, rim cyan, horizon pink).
    ///   Debris: a jagged rock, dark with a violet rim, tumbling.   Acolyte: a hooded cultist holding a tiny black hole out
    ///   front that swells while it winds up.   Pulsar: a spinning neutron star; its two beams (DrawInfallFloor) flicker as
    ///   dashed red lines while it plants, then burn red with a white core while they sweep.
    ///   Graviton: a squat body with a small well swirling in its hand, lifted as it winds up.   Small Well: a black core
    ///   with three violet spiral arms and rings drawn inward.   Orbiter: a core with three orbs turning round it; the one
    ///   facing you flares as it winds up, and the one it threw is missing until it grows back.   Collapsar: a pink star
    ///   that shrinks and darkens as it's hurt.   Implosion: rings closing in on a shrinking white core.
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

        /// A small black hole: three violet spiral arms round a black core with a pink rim.
        void DrawSwirl(V2 pos, float r, float t, float a)
        {
            Circle(pos, r * 3f, InfViolet.WithAlpha(0.12f * a), 16);
            for (int k = 0; k < 3; k++)
            {
                V2 prev = pos;
                for (int i = 0; i <= 10; i++)
                {
                    float f = i / 10f, ang = -t * 5f + k * MathX.Tau / 3f + f * 2.2f, rr = r * (0.6f + f * 1.6f);
                    V2 p = pos + V2.FromAngle(ang) * rr;
                    if (i > 0)
                        Line(prev, p, 1.6f, InfViolet.WithAlpha(0.9f * a * (1f - f * 0.6f)));
                    prev = p;
                }
            }
            Circle(pos, r * 0.7f, InfVoid, 12);
            Arc(pos, r * 0.7f, 1.3f, InfPink.WithAlpha(a), 12);
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
                case EnemyType.Graviton:
                {
                    Circle(pos, r * 2f, InfViolet.WithAlpha(0.06f), 18);
                    V2 f = V2.FromAngle(e.Facing), s = f.Orthogonal();
                    Circle(pos, r, new Rgba(0.11f, 0.08f, 0.16f), 18);
                    Arc(pos, r, 2f, col, 18);
                    // A pack on its back.
                    V2 b = pos - f * (r * 0.75f);
                    SetColor(new Rgba(0.23f, 0.17f, 0.31f));
                    Quad(b + s * (r * 0.4f), b - s * (r * 0.4f), b - s * (r * 0.4f) - f * (r * 0.45f), b + s * (r * 0.4f) - f * (r * 0.45f));
                    float lift = e.Winding ? 0.5f + 0.5f * Mathf.Sin(t * 16f) : 0f;
                    DrawSwirl(pos + f * (r * 0.9f) - new V2(0f, lift * 8f), r * 0.38f, t, 0.9f);
                    break;
                }
                case EnemyType.MiniWell:
                {
                    for (int k = 0; k < 3; k++)
                    {
                        float ring = (t * 0.9f + k / 3f) % 1f;
                        Arc(pos, Game.MiniWellDriftRange * 0.45f * (1f - ring) + r, 1.2f, InfViolet.WithAlpha(0.3f * ring), 32);
                    }
                    Circle(pos, Game.MiniWellBendRange, InfViolet.WithAlpha(0.035f), 32);
                    DrawSwirl(pos, r, t, 1f);
                    break;
                }
                case EnemyType.Orbiter:
                {
                    Arc(pos, Game.OrbiterOrbit, 1f, InfViolet.WithAlpha(0.18f), 28);
                    Circle(pos, r * 2f, InfViolet.WithAlpha(0.08f), 16);
                    Circle(pos, r, new Rgba(0.1f, 0.08f, 0.14f), 16);
                    Arc(pos, r, 2f, col, 16);
                    Circle(pos, r * 0.35f, InfVoid, 10);
                    // Three orbs; the last one thrown grows back over the next 0.9 s; the one it's about to throw flares toward you.
                    int gone = (e.Phase + 2) % 3;
                    float regrow = Mathf.Clamp01(e.SeenT / 0.9f);
                    for (int k = 0; k < 3; k++)
                    {
                        V2 at = pos + V2.FromAngle(t * 3f + k * MathX.Tau / 3f + e.Id) * Game.OrbiterOrbit;
                        float sz = k == gone ? 5f * regrow : 5f;
                        if (sz < 0.5f)
                            continue;
                        Circle(at, sz * 2.2f, InfViolet.WithAlpha(0.25f), 10);
                        Circle(at, sz, InfViolet, 10);
                    }
                    if (e.Winding)
                    {
                        V2 at = pos + V2.FromAngle(e.Facing) * Game.OrbiterOrbit;
                        float fl = 0.6f + 0.4f * Mathf.Sin(t * 30f);
                        Circle(at, 15f * fl, InfViolet.WithAlpha(0.35f), 12);
                        Circle(at, 7f, InfViolet, 12);
                        Arc(at, 7f, 1.5f, Rgba.White, 12);
                    }
                    break;
                }
                case EnemyType.Collapsar:
                {
                    float hp = e.MaxHp > 0f ? Mathf.Clamp01(e.Hp / e.MaxHp) : 1f, hurt = 1f - hp;
                    float rr = r * (0.6f + 0.4f * hp);
                    Circle(pos, rr * 2.2f, InfPink.WithAlpha(0.08f + 0.08f * hurt), 20);
                    Circle(pos, rr, new Rgba(0.16f, 0.06f, 0.12f).Mix(InfVoid, hurt), 22);
                    Arc(pos, rr, 2.5f, col, 22);
                    for (int k = 0; k < 3; k++)
                        Arc(pos, rr * 0.62f, 1.5f, InfPink.WithAlpha(0.5f), 8, -t * 1.5f + k * MathX.Tau / 3f, 1.2f);
                    Circle(pos, rr * 0.35f, InfVoid, 12);
                    Arc(pos, rr * 0.35f, 1.5f, Rgba.White.WithAlpha(0.4f + 0.4f * hurt), 12);
                    break;
                }
                case EnemyType.Implosion:
                {
                    for (int k = 0; k < 3; k++)
                    {
                        float ring = (t * 2.2f + k / 3f) % 1f;
                        Arc(pos, Game.ImplosionDriftRange * 0.4f * (1f - ring) + 6f, 1.6f, InfPink.WithAlpha(0.6f * ring), 40);
                    }
                    float k2 = Mathf.Clamp01(e.SeenT / Game.ImplosionTime);  // (its Phase never changes: SeenT is its age)
                    Circle(pos, 14f, Rgba.White.WithAlpha(0.15f + 0.3f * k2), 14);
                    Circle(pos, Mathf.Lerp(12f, 3f, k2), InfVoid, 12);
                    Arc(pos, Mathf.Lerp(12f, 3f, k2), 2f, Rgba.White, 12);
                    break;
                }
            }
        }
    }
}
