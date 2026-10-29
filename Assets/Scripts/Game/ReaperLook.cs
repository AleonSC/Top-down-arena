using UnityEngine;

namespace Arena
{
    /// How the Twin Reapers' fight looks (Reapers.cs): the tether (dark, or lit for its sweep, or winding tight as
    /// they fuse), the Dual Reaper (half Dusk, half Dawn, a face on each side), its lasers, the pull before it blows,
    /// its pillars and the holes they leave, its shield in a hole, and the reaps' marks in Sundered.
    public partial class ArenaApp
    {
        static readonly Rgba DuskCol = new Rgba(1f, 0.35f, 0.6f), DawnCol = new Rgba(0.45f, 0.55f, 1f), DualGold = new Rgba(1f, 0.8f, 0.35f);

        /// The beam between the Twin Reapers.
        void DrawReaperTether(float t)
        {
            var R = game.Reapers;
            Enemy a = null, b = null;
            foreach (var e in game.Enemies.Values)
                if (Game.IsTwin(e.Type) && !game.IsSpawning(e))
                {
                    if (e.Type == EnemyType.TwinA) a = e; else b = e;
                }
            if (a == null || b == null || (R != null && R.Stage != ReaperStage.Twilight))
                return;
            V2 from = a.Disp, to = b.Disp, n = (to - from).Normalized().Orthogonal();
            bool fusing = R != null && R.Trans == 1;
            bool lit = fusing || (R != null && R.Turn == ReaperTurn.Sweep && R.TurnT > 0.6f);
            if (!lit)
            {
                // Dark: a faint dashed thread (it doesn't hurt); blinking white as the sweep is about to start.
                bool warm = R != null && R.Turn == ReaperTurn.Sweep;
                var c = warm ? new Rgba(1f, 1f, 1f, ((int)(t * 12f) & 1) == 0 ? 0.6f : 0.3f) : new Rgba(0.7f, 0.67f, 0.86f, 0.22f);
                float len = from.DistanceTo(to);
                for (float d = 0f; d < len; d += 12f)
                    Line(V2.Lerp(from, to, d / len), V2.Lerp(from, to, Mathf.Min(1f, (d + 5f) / len)), 2f, c);
                return;
            }
            float thick = fusing ? 1f + 2f * Mathf.Clamp01(R.TransT / 1.6f) : 1f;
            Line(from, to, (Game.TetherWidth + 14f) * thick, new Rgba(1f, 0.9f, 1f, 0.16f));
            // Dusk's pink fading into Dawn's blue.
            for (int k = 0; k < 8; k++)
                Line(V2.Lerp(from, to, k / 8f), V2.Lerp(from, to, (k + 1) / 8f), Game.TetherWidth * thick, DuskCol.Mix(DawnCol, (k + 0.5f) / 8f).WithAlpha(0.9f));
            V2 prev = from;
            for (int k = 1; k <= 16; k++)  // a jagged white core, so it reads as dangerous
            {
                V2 pt = k == 16 ? to : V2.Lerp(from, to, k / 16f) + n * (3f * thick * Mathf.Sin(t * 30f + k * 1.7f));
                Line(prev, pt, 2f, new Rgba(1f, 0.95f, 1f, 0.9f));
                prev = pt;
            }
        }

        /// On the floor: the holes, the lasers (or the pull), the reap marks.
        void DrawReaperFloor(float t)
        {
            var R = game.Reapers;
            if (R == null)
                return;
            foreach (var h in R.Holes)
            {
                Ellipse(h, Game.HoleRadius, Game.HoleRadius * 0.76f, new Rgba(0f, 0f, 0f, 0.88f));
                for (int i = 0; i < 24; i++)
                {
                    float a0 = MathX.Tau * i / 24f, a1 = MathX.Tau * (i + 1) / 24f;
                    Line(h + new V2(Mathf.Cos(a0) * Game.HoleRadius, Mathf.Sin(a0) * Game.HoleRadius * 0.76f), h + new V2(Mathf.Cos(a1) * Game.HoleRadius, Mathf.Sin(a1) * Game.HoleRadius * 0.76f),
                        3f, DualGold.WithAlpha(0.5f + 0.2f * Mathf.Sin(t * 3f)));
                }
            }
            Enemy d = null;
            foreach (var e in game.Enemies.Values)
                if (e.Type == EnemyType.DualReaper)
                    d = e;
            if (d != null && R.Trans == 0 && R.Turn == ReaperTurn.Lasers && !R.Trapped && R.Leap <= 0f)
            {
                V2 c = d.Disp;
                if (R.Suck > 0f)
                {
                    // Pulling everyone in: streaks flying toward it, and the blast's reach blinking.
                    for (int i = 0; i < 16; i++)
                    {
                        float a = i * 0.39f + t, f = 1f - MathX.Fmod(t * 1.5f + i * 0.13f, 1f), dd = f * 500f + 40f;
                        Line(c + V2.FromAngle(a) * dd, c + V2.FromAngle(a) * (dd + 40f), 2f, new Rgba(1f, 0.6f, 0.6f, 0.5f));
                    }
                    Arc(c, 260f, 3f, new Rgba(1f, 0.35f, 0.35f, ((int)(t * 12f) & 1) == 0 ? 0.8f : 0.4f), 64);
                }
                else
                {
                    bool warm = R.TurnT <= Game.DualLaserWarm;
                    for (int k = 0; k < 2; k++)
                    {
                        var col = k == 0 ? DawnCol : DuskCol;
                        V2 dir = V2.FromAngle(R.Laser + k * MathX.Pi), end = c + dir * 1900f;
                        if (warm)
                            Line(c, end, 2f, col.WithAlpha(((int)(t * 14f) & 1) == 0 ? 0.6f : 0.3f));
                        else
                        {
                            Line(c, end, 26f, col.WithAlpha(0.3f));
                            Line(c, end, 12f, col);
                            Line(c, end, 3f, new Rgba(1f, 1f, 1f, 0.9f));
                        }
                    }
                }
            }
            foreach (var e in game.Enemies.Values)
            {
                if (!Game.IsTwin(e.Type))
                    continue;
                var rp = e.Type == EnemyType.TwinA ? R.ReapA : R.ReapB;
                if (rp.Phase != 1)
                    continue;
                // The reap's mark: the dash line, and the scythe's arc where it ends.
                float a = ((int)(t * 14f) & 1) == 0 ? 0.7f : 0.35f;
                V2 from = e.Disp, end = from + V2.FromAngle(rp.A) * rp.Len;
                Line(from, end, 30f, e.Def.Color.WithAlpha(a * 0.6f));
                SetColor(e.Def.Color.WithAlpha(a * 0.35f));
                for (int i = 0; i < 12; i++)
                {
                    float a0 = rp.A - 1.4f + 2.8f * i / 12f, a1 = rp.A - 1.4f + 2.8f * (i + 1) / 12f;
                    Tri(end, end + V2.FromAngle(a0) * 120f, end + V2.FromAngle(a1) * 120f);
                }
            }
        }

        void Ellipse(V2 c, float rx, float ry, Rgba col)
        {
            SetColor(col);
            for (int i = 0; i < 28; i++)
            {
                float a0 = MathX.Tau * i / 28f, a1 = MathX.Tau * (i + 1) / 28f;
                Tri(c, c + new V2(Mathf.Cos(a0) * rx, Mathf.Sin(a0) * ry), c + new V2(Mathf.Cos(a1) * rx, Mathf.Sin(a1) * ry));
            }
        }

        /// Under the Dual Reaper: its power, the stage change, the swing it's about to make, its shield in a hole.
        void DrawDualUnderlay(Enemy e, V2 pos, float r, float t)
        {
            var R = game.Reapers;
            Circle(pos, r + 24f, DualGold.WithAlpha(0.08f + 0.06f * Mathf.Sin(t * 4f)), 32);
            if (R == null)
                return;
            if (R.Trans != 0)
            {
                DrawStageShiftBoss(e, pos, r, t);  // StageShiftLook.cs
                return;
            }
            if (R.Turn == ReaperTurn.Chase && !R.Trapped && R.Leap <= 0f && R.SwingT <= Game.SwingWarn)
                for (int side = -1; side <= 1; side += 2)  // both scythes, flashing just before they swing
                    Arc(pos, 140f, 4f, new Rgba(1f, 1f, 1f, 0.85f), 16, R.Heading + side * MathX.Pi / 2f - 1.05f, 2.1f);
            if (R.Trapped && R.ShieldFrac > 0f)
            {
                Circle(pos, r + 18f, new Rgba(0.8f, 0.86f, 1f, 0.15f), 40);
                Arc(pos, r + 18f, 4f, new Rgba(0.82f, 0.88f, 1f, 0.7f + 0.25f * Mathf.Sin(t * 10f)), 40, -MathX.Pi / 2f, MathX.Tau * R.ShieldFrac);
            }
            else if (R.Trapped)
                Arc(pos, r + 12f, 3f, new Rgba(1f, 0.35f, 0.35f, 0.6f + 0.4f * Mathf.Sin(t * 16f)), 40);
        }

        /// The Dual Reaper itself: half Dusk, half Dawn, joined by a bright seam, with a hooded face on each side
        /// (where the lasers come from). Cracks glow across it as it splits apart.
        void DrawDualDetails(Enemy e, V2 pos, float r, float t)
        {
            var R = game.Reapers;
            float facing = R != null && R.Turn == ReaperTurn.Lasers ? R.Laser : R != null && R.Turn == ReaperTurn.Chase ? R.Heading : t * 0.4f;
            V2 f = V2.FromAngle(facing), side = f.Orthogonal();
            for (int k = 0; k < 2; k++)
            {
                var col = k == 0 ? DawnCol : DuskCol;
                float from = facing + (k == 0 ? -MathX.Pi / 2f : MathX.Pi / 2f);
                SetColor(col.Darkened(0.55f));
                for (int i = 0; i < 8; i++)
                    Tri(pos, pos + V2.FromAngle(from + MathX.Pi * i / 8f) * (r - 3f), pos + V2.FromAngle(from + MathX.Pi * (i + 1) / 8f) * (r - 3f));
                Arc(pos, r - 2f, 3f, col, 16, from, MathX.Pi);
            }
            Line(pos - side * r, pos + side * r, 2f, new Rgba(1f, 0.94f, 0.78f, 0.85f));  // the seam
            DrawReaperFace(pos + f * r * 0.45f, 0.9f, DawnCol);
            DrawReaperFace(pos - f * r * 0.45f, 0.9f, DuskCol);
            if (R != null && R.Trans == 2)
            {
                float k = Mathf.Clamp01(R.TransT / 1.2f);
                for (int i = 0; i < 5; i++)
                {
                    float a = i * 1.26f + 0.3f;
                    Line(pos, pos + V2.FromAngle(a) * r * k, 3f, new Rgba(1f, 0.95f, 0.8f, k));
                }
            }
        }

        void DrawReaperFace(V2 c, float s, Rgba col)
        {
            Circle(c, 12f * s, new Rgba(0f, 0f, 0f, 0.6f), 14);
            Circle(c + new V2(-4f * s, 2f * s), 2.2f * s, col, 6);
            Circle(c + new V2(4f * s, 2f * s), 2.2f * s, col, 6);
        }

        /// A pillar: a stone column with a glowing gem; during the lasers a faint line runs from it to the Reaper.
        void DrawPillarDetails(Enemy e, V2 pos, float r, float t)
        {
            float h = 64f;
            FillRect(pos.X - 20f, pos.Y - h, 40f, h, new Rgba(0.28f, 0.24f, 0.37f));
            FillRect(pos.X - 20f, pos.Y - h, 40f, 8f, new Rgba(0.45f, 0.4f, 0.6f));
            Circle(pos + new V2(0f, -h * 0.55f), 7f, DualGold.WithAlpha(0.6f + 0.3f * Mathf.Sin(t * 5f + pos.X)), 10);
            var R = game.Reapers;
            if (R == null || R.Turn != ReaperTurn.Lasers || R.Suck > 0f)
                return;
            foreach (var o in game.Enemies.Values)
                if (o.Type == EnemyType.DualReaper)
                    for (float d = 0f; d < 1f; d += 0.06f)
                        Line(V2.Lerp(pos + new V2(0f, -h * 0.55f), o.Disp, d), V2.Lerp(pos + new V2(0f, -h * 0.55f), o.Disp, d + 0.025f), 2f, DualGold.WithAlpha(0.25f));
        }
    }
}
