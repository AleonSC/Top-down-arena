using UnityEngine;

namespace Arena
{
    /// How the Twin Reapers' fight looks (Reapers.cs): the tether (a chain: slack and dark, or pulled tight and lit
    /// for its sweep, or thickening as they fuse), the Dual Reaper's split cracks (bodies: ReaperBodyLook.cs), its lasers, the pull before it blows,
    /// its pillars (gravestones) and the holes they leave, its shield in a hole, and the reaps' marks in Sundered.
    public partial class ArenaApp
    {
        static readonly Rgba DuskCol = new Rgba(1f, 0.35f, 0.6f), DawnCol = new Rgba(0.45f, 0.55f, 1f), DualGold = Rgba.Gold;

        /// The beam between the Twin Reapers.
        void DrawReaperTether(float t)
        {
            var R = game.Reapers;
            Enemy a = null, b = null;
            foreach (var e in game.Enemies.Values)
                if (Game.IsTwin(e.Type) && !game.IsSpawning(e))
                {
                    if (e.Type == EnemyType.ReaperOfDusk) a = e; else b = e;
                }
            if (a == null || b == null || (R != null && R.Stage != ReaperStage.Twilight))
                return;
            V2 from = a.Disp, to = b.Disp, n = (to - from).Normalized().Orthogonal();
            bool fusing = R != null && R.Trans == 1;
            bool lit = fusing || (R != null && R.Turn == ReaperTurn.Sweep && R.TurnT > 0.6f);
            bool warm = R != null && R.Turn == ReaperTurn.Sweep;
            float thick = fusing ? 1f + 2f * Mathf.Clamp01(R.TransT / 1.6f) : 1f, len = from.DistanceTo(to);
            V2 d = (to - from).Normalized();
            // A chain of soul links, Dusk's pink fading into Dawn's blue: slack and faint while dark (it doesn't
            // hurt), blinking white as the sweep is about to start, pulled tight and glowing when it's lit.
            int links = Mathf.Max(4, (int)(len / 22f));
            for (int k = 0; k < links; k++)
            {
                float u = (k + 0.5f) / links;
                V2 c = V2.Lerp(from, to, u) + (lit ? V2.Zero : n * 10f * Mathf.Sin(u * MathX.Pi));
                var col = DuskCol.Mix(DawnCol, u);
                if (!lit)
                    col = warm ? new Rgba(1f, 1f, 1f, ((int)(t * 12f) & 1) == 0 ? 0.6f : 0.3f) : col.WithAlpha(0.3f);
                V2 axis = (k & 1) == 0 ? d : n;  // links alternate: one along the chain, one across
                float w = (k & 1) == 0 ? 11f : 6f;
                if (lit)
                    Line(c - axis * w, c + axis * w, 10f * thick, col.WithAlpha(0.3f));
                Line(c - axis * w, c + axis * w, (lit ? 4f : 2f) * thick, col);
            }
            if (!lit)
                return;
            Line(from, to, (Game.TetherWidth + 14f) * thick, new Rgba(1f, 0.9f, 1f, 0.1f));  // its reach
            V2 prev = from;
            for (int k = 1; k <= 16; k++)  // a jagged white core, so it reads as dangerous
            {
                V2 pt = k == 16 ? to : V2.Lerp(from, to, k / 16f) + n * (2f * thick * Mathf.Sin(t * 30f + k * 1.7f));
                Line(prev, pt, 1.5f, new Rgba(1f, 0.95f, 1f, 0.85f));
                prev = pt;
            }
        }

        /// On the floor: the holes, the lasers (or the pull), the reap marks.
        void DrawReaperFloor(float t)
        {
            var R = game.Reapers;
            if (R == null)
                return;
            DrawReaperShiftFloor(t);  // fusing or splitting (ReaperShiftLook.cs)
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
            foreach (var e in EnemiesOf(EnemyType.DualReaper))
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
                var rp = e.Type == EnemyType.ReaperOfDusk ? R.ReapA : R.ReapB;
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

        /// Under the Dual Reaper: its power, the phase change, the swing it's about to make, its shield in a hole.
        void DrawDualUnderlay(Enemy e, V2 pos, float r, float t)
        {
            var R = game.Reapers;
            Circle(pos, r + 24f, DualGold.WithAlpha(0.08f + 0.06f * Mathf.Sin(t * 4f)), 32);
            if (R == null)
                return;
            if (R.Trans != 0)
            {
                DrawReaperShiftOn(e, pos, r, t);  // ReaperShiftLook.cs
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

        /// Over the Dual Reaper's body (ReaperBodyLook.cs): cracks glowing across it as it splits apart.
        void DrawDualDetails(Enemy e, V2 pos, float r, float t)
        {
            var R = game.Reapers;
            if (R == null || R.Trans != 2)
                return;
            float k = Mathf.Clamp01(R.TransT / Game.SplitBurst);
            for (int i = 0; i < 5; i++)
            {
                float a = i * 1.26f + 0.3f;
                Line(pos, pos + V2.FromAngle(a) * r * k, 3f, new Rgba(1f, 0.95f, 0.8f, k));
            }
        }

        /// A pillar: a gravestone carved with Dusk's moon and Dawn's sun, a gold soul flame on top; during the
        /// lasers a faint line runs from the flame to the Reaper.
        void DrawPillarDetails(Enemy e, V2 pos, float r, float t)
        {
            float h = 56f, w = 40f;
            var stone = new Rgba(0.28f, 0.24f, 0.37f);
            V2 top = pos + new V2(0f, -h);
            Circle(top, w * 0.5f, stone, 18);
            FillRect(pos.X - w * 0.5f, pos.Y - h, w, h, stone);
            Arc(top, w * 0.5f, 2.5f, new Rgba(0.5f, 0.45f, 0.66f), 18, MathX.Pi, MathX.Pi);  // the lit rim
            FillRect(pos.X - w * 0.5f - 4f, pos.Y - 6f, w + 8f, 6f, new Rgba(0.2f, 0.17f, 0.27f));  // the plinth
            DrawReaperSigil(pos + new V2(-8f, -h * 0.45f), 6f, true, DuskCol.WithAlpha(0.7f), t);
            DrawReaperSigil(pos + new V2(8f, -h * 0.45f), 6f, false, DawnCol.WithAlpha(0.7f), t);
            V2 flame = top + new V2(0f, -w * 0.5f - 6f);
            float fl = 0.8f + 0.2f * Mathf.Sin(t * 9f + pos.X);
            Circle(flame, 11f, DualGold.WithAlpha(0.2f * fl), 12);
            Tri3(flame + new V2(-6f, 4f), flame + new V2(6f, 4f), flame + new V2(Mathf.Sin(t * 7f + pos.Y) * 2f, -14f * fl), DualGold);
            Circle(flame + new V2(0f, 1f), 3f, new Rgba(1f, 0.97f, 0.85f), 8);
            var R = game.Reapers;
            if (R == null || R.Turn != ReaperTurn.Lasers || R.Suck > 0f)
                return;
            foreach (var o in EnemiesOf(EnemyType.DualReaper))
                if (o.Type == EnemyType.DualReaper)
                    for (float d = 0f; d < 1f; d += 0.06f)
                        Line(V2.Lerp(flame, o.Disp, d), V2.Lerp(flame, o.Disp, d + 0.025f), 2f, DualGold.WithAlpha(0.25f));
        }
    }
}
