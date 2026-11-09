using UnityEngine;

namespace Arena
{
    /// The enemies that don't shoot (Core/NonShooters.cs), in the looks the owner picked from
    /// Docs/Mockups/non-shooter-enemies.html: the Fuse Runner as look A (Powder Keg: a banded keg whose wick burns down,
    /// a ring closing on it), the Hookman as look B (Harpoon: a diamond, a thin aim line that blinks faster as the throw
    /// nears, a barbed harpoon on a cable) and the Sapper as look C (Tripwire: hex pods joined by lasers).
    /// Drawn the same with Visual effects on or off; the timers come from the snapshot (PhaseT, T2).
    public partial class ArenaApp
    {
        static readonly Rgba FuseDanger = new Rgba(1f, 0.3f, 0.3f), FuseHot = new Rgba(1f, 0.96f, 0.82f), FuseRing = new Rgba(1f, 0.69f, 0.3f);
        static readonly Rgba HookCable = new Rgba(0.85f, 0.8f, 0.96f), WireRed = new Rgba(1f, 0.35f, 0.35f);

        /// Behind the body: the fuse's blast ring, the Hookman's aim line, cable and harpoon, a pod's lasers.
        void DrawNonShooterUnder(Enemy e, V2 pos, float r, float t)
        {
            switch (e.Type)
            {
                case EnemyType.FuseRunner:
                    if (e.Phase == 1)
                    {
                        float f = 1f - Mathf.Clamp01(e.PhaseT / Game.FuseCountdown);
                        Circle(pos, Game.FuseBlast, FuseDanger.WithAlpha(0.08f + 0.06f * f), 48);
                        Arc(pos, Game.FuseBlast, 2f, FuseDanger.WithAlpha(0.6f), 48);
                        Arc(pos, r + 6f + (Game.FuseBlast - r - 6f) * (1f - f), 4f, FuseRing.WithAlpha(0.95f), 40);
                    }
                    break;
                case EnemyType.Hookman:
                    DrawHookUnder(e, pos, r, t);
                    break;
                case EnemyType.Swordsman:
                case EnemyType.ShieldSwordsman:
                    DrawSwordsmanUnder(e, pos, r, t);  // the cone it cuts (SwordsmanLook.cs)
                    break;
                case EnemyType.SapperMine:
                    if (e.Phase == 1)  // each pod draws the lasers to the armed pods after it (so each is drawn once)
                        foreach (var o in game.Enemies.Values)
                            if (o.Type == EnemyType.SapperMine && o.Phase == 1 && o.Id > e.Id && o.Disp.DistanceTo(pos) < Game.PodWire)
                            {
                                float a = 0.55f + 0.25f * Mathf.Sin(t * 9f + e.Id);
                                Line(pos, o.Disp, 5f, WireRed.WithAlpha(0.14f));
                                Line(pos, o.Disp, 1.6f, WireRed.WithAlpha(a));
                            }
                    if (e.Phase == 2)
                    {
                        bool on = Mathf.Sin(t * 60f) > 0f;
                        Circle(pos, Game.PodBlast, FuseDanger.WithAlpha(on ? 0.16f : 0.06f), 40);
                        Arc(pos, Game.PodBlast, 2f, FuseDanger.WithAlpha(0.7f), 40);
                    }
                    break;
            }
        }

        void DrawHookUnder(Enemy e, V2 pos, float r, float t)
        {
            V2 dir = V2.FromAngle(e.Facing);
            if (e.Phase == 1)
            {
                // The aim: a thin line to the harpoon's reach, blinking faster as the throw nears, with a chevron at the end.
                float f = 1f - Mathf.Clamp01(e.PhaseT / Game.HookAim), rate = 6f + f * 22f;
                bool on = Mathf.Sin(t * rate) > -0.2f;
                var c = e.Def.Color.WithAlpha(on ? 0.9f : 0.25f);
                V2 end = pos + dir * Game.HookReach;
                Line(pos + dir * r, end, 2f, c);
                Line(end - V2.FromAngle(e.Facing - 0.5f) * 14f, end, 2f, c);
                Line(end - V2.FromAngle(e.Facing + 0.5f) * 14f, end, 2f, c);
            }
            else if (e.Phase >= 2 && e.T2 > r)
            {
                V2 tip = pos + dir * e.T2;
                Line(pos, tip, 1.6f, HookCable.WithAlpha(0.9f));
                DrawHarpoon(tip, e.Facing, 1.6f);
            }
        }

        /// A barbed harpoon head pointing along `ang`.
        void DrawHarpoon(V2 at, float ang, float s)
        {
            V2 f = V2.FromAngle(ang), side = f.Orthogonal();
            var c = new Rgba(0.92f, 0.88f, 1f);
            Tri3(at + f * 8f * s, at - side * 4f * s, at + f * 2f * s, c);
            Tri3(at + f * 8f * s, at + f * 2f * s, at + side * 4f * s, c);
            Line(at - f * 5f * s, at + f * 2f * s, 1.6f * s, c);
        }

        void Tri3(V2 a, V2 b, V2 c, Rgba col)
        {
            SetColor(col);
            Tri(a, b, c);
        }

        /// The body, in place of the usual polygon. col is already tinted by status and the hit flash.
        void DrawNonShooterBody(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            switch (e.Type)
            {
                case EnemyType.FuseRunner:
                {
                    // Powder Keg: a round keg with two dark bands; the wick burns down through the fuse and the keg flashes hot.
                    float f = e.Phase == 1 ? 1f - Mathf.Clamp01(e.PhaseT / Game.FuseCountdown) : 0f;
                    var body = e.Phase == 1 && Mathf.Sin(t * (20f + f * 30f)) > 0f ? col.Mix(FuseHot, 0.3f + 0.6f * f) : col;
                    Circle(pos, r, body.Darkened(0.45f), 24);
                    Circle(pos, r - 1.6f, body, 24);
                    var band = new Rgba(0.35f, 0.16f, 0.05f);
                    Line(pos + new V2(-r * 0.92f, -r * 0.35f), pos + new V2(r * 0.92f, -r * 0.35f), 2f, band);
                    Line(pos + new V2(-r * 0.92f, r * 0.35f), pos + new V2(r * 0.92f, r * 0.35f), 2f, band);
                    float wick = (1f - f) * 9f + 3f;
                    V2 top = pos + new V2(0f, -r), spark = top + new V2(2.5f, -wick);
                    Line(top, spark, 2f, new Rgba(0.85f, 0.79f, 0.66f));
                    float flick = 0.7f + 0.3f * Mathf.Sin(t * 31f + e.Id);
                    Circle(spark, 6f * flick, new Rgba(1f, 0.83f, 0.42f, 0.35f), 10);
                    Circle(spark, 2.2f, Rgba.White, 8);
                    break;
                }
                case EnemyType.Hookman:
                {
                    // Harpoon: a diamond facing its aim, with a dark eye toward it. Open after a miss: it shimmers white (+50% damage).
                    var body = e.Phase == 4 ? col.Mix(Rgba.White, 0.35f + 0.25f * Mathf.Sin(t * 16f)) : col;
                    Polygon(pos, r, 4, e.Facing, body);
                    Circle(pos + V2.FromAngle(e.Facing) * r * 0.35f, r * 0.2f, new Rgba(0.16f, 0.09f, 0.25f), 10);
                    if (e.Phase == 0)
                    {
                        // The harpoon spins round it on a short cable, faster in the last second before it aims.
                        float spin = e.PhaseT < 1f ? 14f : 5f, a = t * spin + e.Id;
                        V2 h = pos + V2.FromAngle(a) * (r + 12f);
                        Line(pos, h, 1.4f, HookCable.WithAlpha(0.85f));
                        DrawHarpoon(h, a + MathX.Pi / 2f, 1.1f);
                    }
                    else if (e.Phase == 1)
                        DrawHarpoon(pos + V2.FromAngle(e.Facing) * (r + 4f), e.Facing, 1.2f);  // held ready on the line
                    break;
                }
                case EnemyType.Swordsman:
                case EnemyType.ShieldSwordsman:
                    DrawSwordsmanBody(e, pos, r, col, t);  // SwordsmanLook.cs
                    break;
                case EnemyType.Sapper:
                {
                    // Tripwire: a hexagon with a red core light that pulses.
                    Polygon(pos, r, 6, e.Facing, col);
                    float p = 0.5f + 0.5f * Mathf.Sin(t * 6f + e.Id);
                    Circle(pos, r * 0.7f, new Rgba(1f, 0.35f, 0.35f, 0.25f * p), 14);
                    Circle(pos, r * 0.32f, new Rgba(1f, 0.36f, 0.36f), 12);
                    break;
                }
                case EnemyType.SapperMine:
                {
                    // A hex pod: dim while arming (a ring closing on it), lit once armed, blinking while it beeps.
                    float arm = Mathf.Clamp01(e.T2 / Game.PodArm);
                    bool armed = e.Phase >= 1, beep = e.Phase == 2, blink = Mathf.Sin(t * 60f) > 0f;
                    var shell = armed ? new Rgba(1f, 0.42f, 0.35f) : new Rgba(0.42f, 0.25f, 0.25f);
                    Polygon(pos, r, 6, 0f, shell);
                    Circle(pos, r * 0.36f, armed && (!beep || blink) ? Rgba.White : new Rgba(0.16f, 0.06f, 0.06f), 8);
                    if (!armed)
                        Arc(pos, r + (1f - arm) * 16f, 1.5f, shell.WithAlpha(0.6f), 20);
                    if (beep)
                        Circle(pos, r * 2.4f, new Rgba(1f, 0.3f, 0.3f, blink ? 0.5f : 0.15f), 16);
                    break;
                }
            }
        }
    }
}
