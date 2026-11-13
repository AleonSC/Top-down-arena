using UnityEngine;

namespace Arena
{
    /// The Storm Herald's faction, the Squall (Core/Squall.cs), drawn as in Docs/Mockups/storm-herald-faction.html with the
    /// Herald's colours (HeraldLook.cs: storm blue and white; strike warnings amber; wind teal).
    ///   Dust Devil: three wind arcs turning round a bright eye, dust flecks circling it.   Squall Gunner: a round body with a
    ///   barrel and a wind vane on its back that sways; the vane's side is its wind's (Enemy.Strafe).   Stormcaller: a robed
    ///   teardrop holding a staff that rises while it winds up, its tip glowing amber under a little cloud.
    public partial class ArenaApp
    {
        static readonly Rgba SqWind = new Rgba(0.62f, 0.91f, 0.85f), SqBlue = new Rgba(0.56f, 0.82f, 1f), SqAmber = new Rgba(1f, 0.82f, 0.4f),
            SqDust = new Rgba(0.79f, 0.71f, 0.54f), SqCloud = new Rgba(0.43f, 0.48f, 0.57f);

        void DrawSquallBody(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            switch (e.Type)
            {
                case EnemyType.DustDevil:
                {
                    Circle(pos, r * 2.1f, SqWind.WithAlpha(0.07f), 16);
                    for (int k = 0; k < 3; k++)
                    {
                        float spin = t * (7f - k * 1.5f) + e.Id * 1.3f + k * 2f;
                        Arc(pos, r * (0.5f + k * 0.45f), 2.4f - k * 0.5f, SqWind.WithAlpha(0.95f - k * 0.22f), 10, spin, MathX.Pi * 1.25f);
                    }
                    Circle(pos, r * 0.32f, new Rgba(0.89f, 1f, 0.97f), 10);
                    for (int i = 0; i < 4; i++)
                        Circle(pos + V2.FromAngle(t * 9f + i * 1.7f + e.Id) * (r * 1.6f), 1.4f, SqDust.WithAlpha(0.8f), 6);
                    break;
                }
                case EnemyType.SquallGunner:
                {
                    Circle(pos, r * 2.1f, SqBlue.WithAlpha(0.06f), 18);
                    V2 f = V2.FromAngle(e.Facing), s = f.Orthogonal();
                    // The vane on its back, swaying, leaning to its wind's side.
                    float side = e.Strafe >= 0f ? 1f : -1f;
                    V2 hinge = pos - f * (r * 0.95f);
                    V2 vd = V2.FromAngle(e.Facing + MathX.Pi + side * 0.35f + 0.3f * Mathf.Sin(t * 5f + e.Id));
                    V2 vs = vd.Orthogonal();
                    SetColor(SqWind.WithAlpha(0.55f));
                    Tri(hinge, hinge + vd * (r * 1.05f) + vs * (r * 0.45f), hinge + vd * (r * 1.05f) - vs * (r * 0.45f));
                    // The barrel, then the body.
                    Quad(pos + s * (r * 0.27f), pos + f * (r * 1.6f) + s * (r * 0.27f), pos + f * (r * 1.6f) - s * (r * 0.27f), pos - s * (r * 0.27f), new Rgba(0.16f, 0.23f, 0.3f));
                    Circle(pos, r, new Rgba(0.08f, 0.11f, 0.15f), 18);
                    Arc(pos, r, 1.8f, col.Mix(new Rgba(0.44f, 0.56f, 0.69f), 0.5f), 18);
                    Circle(pos + f * (r * 0.15f), r * 0.38f, new Rgba(0.04f, 0.06f, 0.09f), 12);
                    Arc(pos + f * (r * 0.15f), r * 0.38f, 1.2f, SqBlue, 12);
                    if (e.Winding)
                    {
                        float w = 0.5f + 0.5f * Mathf.Sin(t * 18f);
                        Circle(pos + f * (r * 1.75f), r * (0.5f + 0.35f * w), SqBlue.WithAlpha(0.3f + 0.3f * w), 12);
                    }
                    break;
                }
                case EnemyType.Stormcaller:
                {
                    float raise = e.Winding ? 1f : 0.15f;
                    Circle(pos, r * 2f, SqAmber.WithAlpha(0.05f + 0.12f * raise), 18);
                    V2 f = V2.FromAngle(e.Facing), s = f.Orthogonal();
                    V2 nose = pos + f * (r * 1.1f), back = pos - f * (r * 0.9f);
                    V2 l = pos - f * (r * 0.55f) + s * (r * 0.95f), rgt = pos - f * (r * 0.55f) - s * (r * 0.95f);
                    var rim = col.Mix(new Rgba(0.5f, 0.55f, 0.69f), 0.6f);
                    SetColor(new Rgba(0.1f, 0.12f, 0.17f));
                    Tri(nose, l, back);
                    Tri(nose, back, rgt);
                    Line(nose, l, 1.6f, rim);
                    Line(l, back, 1.6f, rim);
                    Line(back, rgt, 1.6f, rim);
                    Line(rgt, nose, 1.6f, rim);
                    Circle(pos - f * (r * 0.1f), r * 0.4f, new Rgba(0.04f, 0.05f, 0.08f), 12);
                    // The staff from its hand, up (screen up) as it winds up, its tip glowing.
                    V2 hand = pos + f * (r * 0.3f) + s * (r * 0.7f);
                    V2 tip = hand + f * (r * (1.1f - 0.6f * raise)) + new V2(0f, -r * 1.6f * raise);
                    Line(hand, tip, 2.5f, new Rgba(0.72f, 0.61f, 0.42f));
                    Circle(tip, r * (0.55f + 0.6f * raise), SqAmber.WithAlpha(0.22f + 0.3f * raise), 12);
                    Circle(tip, 2.5f + 1.5f * raise, Rgba.White, 8);
                    if (e.Winding)
                        for (int i = 0; i < 4; i++)
                            Circle(tip + new V2(Mathf.Cos(t * 3f + i * 1.6f) * r * 0.8f, -r * 0.6f + Mathf.Sin(t * 3f + i * 1.6f) * 2.5f), r * 0.38f, SqCloud.WithAlpha(0.5f), 10);
                    break;
                }
            }
        }
    }
}
