using UnityEngine;

namespace Arena
{
    /// The Storm Herald's faction, the Squall (Core/Squall.cs), drawn as in Docs/Mockups/storm-herald-faction.html with the
    /// Herald's colours (HeraldLook.cs: storm blue and white; strike warnings amber; wind teal).
    ///   Dust Devil: three wind arcs turning round a bright eye, dust flecks circling it.   Squall Gunner: a round body with a
    ///   barrel and a wind vane on its back that sways; the vane's side is its wind's (Enemy.Strafe).   Stormcaller: a robed
    ///   teardrop holding a staff that rises while it winds up, its tip glowing amber under a little cloud.
    ///   Grounder: a body behind a curved copper shield with three rod tips; a blue arc along the shield shows its charge
    ///   (Enemy.Phase of GrounderFull) and the tips go white near full.   Pylon: a square post turned 45° with a copper ring
    ///   and a glowing tip; the pair's line (DrawSquallFloor) is a faint dotted tether, a flickering thin bolt while it
    ///   warns, then a thick bolt as it fires.   Lineman: a body with a copper coil pack and a forked prod; its mark
    ///   (DrawSquallFloor) is a dashed amber ring round the marked player with a countdown arc.
    public partial class ArenaApp
    {
        /// On the floor, under the enemies: a Pylon pair's line, a Lineman's mark.
        void DrawSquallFloor(Enemy e, V2 pos, float t)
        {
            if (e.Type == EnemyType.Pylon)
            {
                Enemy o;
                if (e.LinkId == 0 || !game.Enemies.TryGetValue(e.LinkId, out o) || o.Type != EnemyType.Pylon || e.Id > o.Id)
                    return;  // the leader draws it
                if (pos.DistanceTo(o.Disp) > Game.PylonMaxLine)
                    return;
                int seed = (int)(t * 20f) + e.Id;
                switch (e.Phase)
                {
                    case Game.PylonWalking:
                        DashedLine(pos, o.Disp, 1f, SqBlue.WithAlpha(0.12f));
                        break;
                    case Game.PylonQuiet:
                        DashedLine(pos, o.Disp, 1.5f, SqBlue.WithAlpha(0.25f));
                        break;
                    case Game.PylonFlicker:
                        if (Mathf.Sin(t * 60f + e.Id) > -0.2f)
                            DrawBolt(pos, o.Disp, 1.2f, SqBlue.WithAlpha(0.7f), seed);
                        break;
                    case Game.PylonFiring:
                        DrawBolt(pos, o.Disp, Game.PylonWidth * 0.35f, SqBlue, seed);
                        DrawBolt(pos, o.Disp, 1.6f, Rgba.White, seed);
                        break;
                }
            }
            else if (e.Type == EnemyType.Lineman && e.Phase == 1)
            {
                Player p;
                if (!game.Players.TryGetValue(e.LinkId, out p))
                    return;
                float life = 1f - Mathf.Clamp01(e.SeenT / Game.LineMark);
                Circle(p.Disp, Game.LineRing, SqAmber.WithAlpha(0.04f), 40);
                for (int k = 0; k < 24; k += 2)
                    Arc(p.Disp, Game.LineRing, 1.6f, SqAmber.WithAlpha(0.7f), 4, k * MathX.Tau / 24f + t * 0.4f, MathX.Tau / 24f);
                Arc(p.Disp, 20f, 3f, SqAmber, 24, -MathX.Pi / 2f, MathX.Tau * life);
            }
        }

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
                case EnemyType.Grounder:
                {
                    float charge = Mathf.Clamp01(e.Phase / (float)Game.GrounderFull);
                    Circle(pos, r * 2.4f, SqBlue.WithAlpha(0.04f + 0.14f * charge), 18);
                    V2 f = V2.FromAngle(e.Facing);
                    V2 c = pos - f * (r * 0.35f);
                    Circle(c, r * 0.75f, new Rgba(0.09f, 0.11f, 0.15f), 16);
                    Arc(c, r * 0.75f, 1.5f, new Rgba(0.48f, 0.52f, 0.6f), 16);
                    var copper = new Rgba(0.82f, 0.54f, 0.29f);
                    Arc(c, r * 1.55f, 6f, copper, 14, e.Facing - Game.GrounderArc, Game.GrounderArc * 2f);
                    if (charge > 0f)
                        Arc(c, r * 1.55f, 2.4f, SqBlue, 14, e.Facing - Game.GrounderArc, Game.GrounderArc * 2f * charge);
                    for (int k = -1; k <= 1; k++)
                    {
                        V2 d = V2.FromAngle(e.Facing + k * 0.7f);
                        Line(c + d * (r * 1.75f), c + d * (r * 2.2f), 2f, copper);
                        Circle(c + d * (r * 2.2f), 2.3f, charge > 0.6f && Mathf.Sin(t * 30f + k) > 0f ? Rgba.White : copper, 8);
                    }
                    break;
                }
                case EnemyType.Pylon:
                {
                    bool hot = e.Phase == Game.PylonFiring, warm = e.Phase == Game.PylonFlicker;
                    Circle(pos, r * (hot ? 2.8f : 2f), SqBlue.WithAlpha(hot ? 0.3f : warm ? 0.16f : 0.07f), 16);
                    float s = r * 0.85f;
                    V2 a = new V2(s, 0f), b = new V2(0f, s);
                    SetColor(new Rgba(0.1f, 0.13f, 0.19f));
                    Tri(pos + a, pos + b, pos - a);
                    Tri(pos - a, pos - b, pos + a);
                    var rim = new Rgba(0.49f, 0.54f, 0.64f);
                    Line(pos + a, pos + b, 1.5f, rim); Line(pos + b, pos - a, 1.5f, rim);
                    Line(pos - a, pos - b, 1.5f, rim); Line(pos - b, pos + a, 1.5f, rim);
                    Arc(pos, r * 0.42f, 2f, new Rgba(0.82f, 0.54f, 0.29f), 12);
                    Circle(pos, r * 0.2f, hot || warm ? Rgba.White : SqBlue, 8);
                    break;
                }
                case EnemyType.Lineman:
                {
                    Circle(pos, r * 2f, SqBlue.WithAlpha(0.06f), 16);
                    V2 f = V2.FromAngle(e.Facing), s = f.Orthogonal();
                    // The coil pack on its back.
                    V2 b0 = pos - f * (r * 1.2f);
                    Quad(b0 + s * (r * 0.6f), b0 + s * (r * 0.6f) - f * (r * 0.55f), b0 - s * (r * 0.6f) - f * (r * 0.55f), b0 - s * (r * 0.6f), new Rgba(0.17f, 0.14f, 0.09f));
                    for (int i = 0; i < 4; i++)
                        Line(b0 + s * (r * (0.6f - i * 0.4f)), b0 + s * (r * (0.4f - i * 0.4f)) - f * (r * 0.55f), 1.2f, new Rgba(0.82f, 0.54f, 0.29f));
                    Circle(pos, r, new Rgba(0.08f, 0.11f, 0.14f), 16);
                    Arc(pos, r, 1.6f, new Rgba(0.48f, 0.54f, 0.64f), 16);
                    // The prod, forked at the tip.
                    V2 h = pos + s * (r * 0.35f), tip = h + f * (r * 1.9f);
                    Line(h, tip, 2.5f, new Rgba(0.6f, 0.65f, 0.73f));
                    Line(tip, tip + V2.FromAngle(e.Facing + 0.6f) * 5f, 1.5f, SqBlue);
                    Line(tip, tip + V2.FromAngle(e.Facing - 0.6f) * 5f, 1.5f, SqBlue);
                    if (e.Winding || Mathf.Sin(t * 13f + e.Id) > 0.75f)
                        Circle(tip, r * 0.6f, SqBlue.WithAlpha(0.45f), 10);
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
