using UnityEngine;

namespace Arena
{
    /// How the Duelist's fight looks (Duelist.cs): its sword, the guard's raised blade, the salute's glint, the reeling
    /// stagger, the warning lines and dash trails, the hanging blades, the dueling circle and its decoys' hollow shots.
    public partial class ArenaApp
    {
        static readonly Rgba DuelSteel = new Rgba(0.81f, 0.84f, 1f), DuelCrimson = new Rgba(1f, 0.3f, 0.42f), DuelGold = new Rgba(1f, 0.8f, 0.35f);

        /// On the floor: the circle, the warning line or trail of a dash, and the blades left hanging.
        void DrawDuelFloor(float t)
        {
            foreach (var e in game.Enemies.Values)
            {
                if (e.Type != EnemyType.Duelist || e.Duel == null || game.IsSpawning(e))
                    continue;
                var s = e.Duel;
                V2 middle = new V2(Game.Width / 2f, Game.Height / 2f);
                // The dueling circle: a glowing ring with blades round it, slowly turning.
                if (s.Ring > 0f && s.TransT <= 0f)
                {
                    Circle(middle, s.Ring, DuelCrimson.WithAlpha(0.03f), 96);
                    Arc(middle, s.Ring, 18f, DuelCrimson.WithAlpha(0.16f), 96);
                    Arc(middle, s.Ring, 2.5f, DuelCrimson.WithAlpha(0.85f), 96);
                    int n = Mathf.RoundToInt(s.Ring / 9f);
                    for (int k = 0; k < n; k++)
                    {
                        V2 dir = V2.FromAngle(MathX.Tau * k / n + t * 0.08f), at = middle + dir * (s.Ring - 10f);
                        Line(at, at + dir * 20f, 2.5f, (k % 2 == 0 ? DuelSteel : DuelCrimson).WithAlpha(0.85f));
                    }
                }
                // A dash: the flashing warning line, then a pale trail behind it.
                if (s.Dash != DuelDash.None)
                {
                    V2 dir = V2.FromAngle(s.DashAng), end = s.DashFrom + dir * s.DashLen;
                    if (s.DashWarn > 0f)
                    {
                        float a = 0.35f + 0.35f * Mathf.Sin(t * 28f);
                        V2 side = dir.Orthogonal() * (e.Radius + 2f);
                        SetColor(DuelCrimson.WithAlpha(0.08f));
                        Quad(s.DashFrom + side, end + side, end - side, s.DashFrom - side);
                        for (float u = 0f; u < s.DashLen; u += 26f)  // dashed centre line
                            Line(s.DashFrom + dir * u, s.DashFrom + dir * Mathf.Min(s.DashLen, u + 16f), 3f, DuelCrimson.WithAlpha(a));
                    }
                    else
                        Line(s.DashFrom, e.Disp, 16f, DuelSteel.WithAlpha(0.14f));
                }
                // Blades hanging where it dashed, pulsing as they're about to fire.
                foreach (var bl in s.Blades)
                {
                    float pulse = 0.6f + 0.4f * Mathf.Sin(t * 24f);
                    V2 across = V2.FromAngle(bl.Ang + MathX.Pi / 2f), along = V2.FromAngle(bl.Ang);
                    Line(bl.Pos - across * 14f, bl.Pos + across * 14f, 3f, DuelSteel.WithAlpha(pulse));
                    Line(bl.Pos + across * 9f - along * 6f, bl.Pos + across * 9f + along * 6f, 3f, DuelGold.WithAlpha(0.9f));
                }
            }
        }

        /// On the Duelist (and its decoys, which look the same): its sword; the guard's steel arc; the salute's raised
        /// sword and gold glint; reeling when it's been parried.
        void DrawDuelistBlade(Enemy e, V2 pos, float r, float t)
        {
            if (e.Type == EnemyType.Decoy)
            {
                DrawSword(pos, e.Facing, r, false, t);
                return;
            }
            var s = e.Duel;
            bool salute = s != null && s.SaluteT > 0f, stagger = s != null && s.StaggerT > 0f;
            float face = e.Facing + (stagger ? Mathf.Sin(t * 14f) * 0.35f : 0f);
            DrawSword(pos, face, r, salute, t);
            if (s == null)
                return;
            if (Game.DuelGuarding(e))
            {
                Arc(pos, r + 16f, 16f, DuelSteel.WithAlpha(0.22f), 20, e.Facing - Game.GuardHalfArc, Game.GuardHalfArc * 2f);
                Arc(pos, r + 16f, 5f, DuelSteel.WithAlpha(0.9f), 20, e.Facing - Game.GuardHalfArc, Game.GuardHalfArc * 2f);
            }
            if (salute)
            {
                float k = Mathf.Clamp01(1f - s.SaluteT / 0.8f);
                Arc(pos, r + 30f - 20f * k, 3f, DuelGold.WithAlpha(0.45f + 0.45f * Mathf.Sin(t * 30f)), 32);
                Circle(pos + new V2(0f, -(r + 48f)), 6f + 4f * Mathf.Sin(t * 40f), new Rgba(1f, 0.96f, 0.8f), 12);
            }
            if (stagger)
            {
                Circle(pos, r + 6f, DuelCrimson.WithAlpha(0.25f + 0.1f * Mathf.Sin(t * 10f)), 24);
                for (int k = 0; k < 3; k++)
                {
                    float a = t * 5f + k * MathX.Tau / 3f;
                    Circle(pos + new V2(Mathf.Cos(a) * 26f, -(r + 12f) + Mathf.Sin(a) * 8f), 4f, DuelGold, 8);
                }
            }
        }

        /// A rapier along the facing (or held straight up in salute), with a gold guard.
        void DrawSword(V2 pos, float face, float r, bool up, float t)
        {
            V2 dir = up ? new V2(0f, -1f) : V2.FromAngle(face + 0.25f), side = dir.Orthogonal();
            V2 hilt = pos + (up ? V2.FromAngle(face) * (r * 0.4f) : V2.FromAngle(face + 0.25f) * (r * 0.6f)) + side * (r * 0.45f);
            Line(hilt, hilt + dir * (r * 1.9f), 3f, new Rgba(0.95f, 0.96f, 1f));
            Line(hilt - side * 8f, hilt + side * 8f, 4f, DuelGold);
        }

        /// A decoy's harmless shot: a pale ring.
        void DrawHollowShot(Bullet b)
        {
            Arc(b.Pos, b.Radius, 2f, Defs.BulletColor(Defs.HollowShot).WithAlpha(0.75f), 14);
        }
    }
}
