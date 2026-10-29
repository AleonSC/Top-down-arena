using UnityEngine;

namespace Arena
{
    /// How the Hive Queen's fight looks (Queen.cs): the Honeycomb's wax floor and its sealing cells, the royal
    /// dive's shadow, the nectar spray's aim, the swarm cloud's bees, eggs, honey globs and the stage looks on her.
    public partial class ArenaApp
    {
        static readonly Rgba Wax = new Rgba(1f, 0.75f, 0.3f), FrenzyWax = new Rgba(1f, 0.43f, 0.27f);

        /// On the floor, under everything else.
        void DrawQueenFloor(float t)
        {
            var e = game.TheQueen();
            if (e == null || e.Queen == null)
                return;
            var q = e.Queen;
            var stage = q.TransT > 0f ? q.TransTo : q.Stage;
            if (stage >= QueenStage.Honeycomb)
            {
                // Wax cells across the floor: they spread in as the Honeycomb begins.
                float a = q.TransT > 0f && q.TransTo == QueenStage.Honeycomb ? 1f - q.TransT / 2f : 1f;
                var c = (stage >= QueenStage.Frenzy ? FrenzyWax : Wax).WithAlpha(0.1f * a);
                foreach (var cell in Game.HiveCells)
                    HexOutline(cell, Game.HiveCell - 3f, 2f, c);
            }
            foreach (var cell in q.Cells)
                DrawSealingCell(Game.HiveCells[cell.Index], cell.T, t);
            if (q.TransT <= 0f && q.Turn == QueenTurn.Dive && q.Dives < 3)
                DrawDiveShadow(q, t);
            if (q.TransT <= 0f && q.Turn == QueenTurn.Spray)
            {
                V2 d = V2.FromAngle((20f + 140f * q.Aux) * MathX.Pi / 180f);
                Line(e.Disp, e.Disp + d * 640f, 26f, new Rgba(1f, 0.67f, 0.16f, 0.1f));
            }
        }

        /// A cell that's about to seal: amber filling from the middle and a blinking rim, then a hard flash as it
        /// seals, fading out.
        void DrawSealingCell(V2 c, float ct, float t)
        {
            float r = Game.HiveCell - 4f;
            if (ct < Game.CellWarn)
            {
                float f = ct / Game.CellWarn;
                HexFill(c, r, new Rgba(1f, 0.67f, 0.16f, 0.05f + 0.12f * f));
                HexFill(c, r * f, new Rgba(1f, 0.78f, 0.31f, 0.16f));
                bool on = ((int)(t * (f > 0.6f ? 16f : 8f)) & 1) == 0;
                HexOutline(c, r, 3f, new Rgba(1f, 0.78f, 0.31f, on ? 0.9f : 0.5f));
            }
            else
            {
                float f = Mathf.Clamp01((ct - Game.CellWarn) / (Game.CellLife - Game.CellWarn));
                HexFill(c, r, new Rgba(1f, 0.88f, 0.55f, 0.55f * (1f - f)));
                HexOutline(c, r, 4f, new Rgba(0.47f, 0.27f, 0.04f, 0.8f * (1f - f)));
            }
        }

        void HexFill(V2 c, float r, Rgba col)
        {
            SetColor(col);
            for (int i = 0; i < 6; i++)
                Tri(c, c + V2.FromAngle(MathX.Tau * i / 6f) * r, c + V2.FromAngle(MathX.Tau * (i + 1) / 6f) * r);
        }

        void HexOutline(V2 c, float r, float w, Rgba col)
        {
            for (int i = 0; i < 6; i++)
                Line(c + V2.FromAngle(MathX.Tau * i / 6f) * r, c + V2.FromAngle(MathX.Tau * (i + 1) / 6f) * r, w, col);
        }

        /// Where she'll land: a shadow that darkens as she comes down, ringed in red (blinking once it's locked).
        void DrawDiveShadow(QueenState q, float t)
        {
            float f = Mathf.Clamp01(q.DiveT / 1.3f), k = 0.4f + 0.6f * f;
            SetColor(new Rgba(0f, 0f, 0f, 0.25f + 0.35f * f));
            for (int i = 0; i < 32; i++)
            {
                float a0 = MathX.Tau * i / 32f, a1 = MathX.Tau * (i + 1) / 32f;
                V2 c = q.Shadow + new V2(0f, 6f);
                Tri(c, c + new V2(Mathf.Cos(a0) * Game.DiveRadius * k, Mathf.Sin(a0) * Game.DiveRadius * 0.72f * k), c + new V2(Mathf.Cos(a1) * Game.DiveRadius * k, Mathf.Sin(a1) * Game.DiveRadius * 0.72f * k));
            }
            bool locked = q.DiveT >= 0.5f;
            float alpha = !locked ? 0.5f : ((int)(t * 14f) & 1) == 0 ? 1f : 0.6f;
            Arc(q.Shadow, Game.DiveRadius, 3f, new Rgba(1f, 0.35f, 0.24f, alpha), 40);
        }

        /// On top: the swarm cloud's bees.
        void DrawQueenSwarm(float t)
        {
            var e = game.TheQueen();
            if (e == null || e.Queen == null || !e.Queen.CloudOn)
                return;
            var q = e.Queen;
            for (int i = 0; i < Game.BeeCount; i++)
            {
                V2 b = Game.BeePos(i, q.CloudDisp, q.TurnT, q.CloudHome);
                float a = t * 40f + i;
                Circle(b + new V2(0f, -4f), 2.6f, new Rgba(0.9f, 0.98f, 1f, 0.45f + 0.2f * Mathf.Sin(a)), 6);  // wings
                Circle(b, Game.BeeRadius, new Rgba(1f, 0.82f, 0.24f), 8);
                Line(b + new V2(-1.5f, -3.5f), b + new V2(-1.5f, 3.5f), 1.5f, new Rgba(0.12f, 0.08f, 0f, 0.9f));
            }
        }

        /// Under the Queen: her drone shield, the stage-change shell, the frenzy's glow, and a wingbeat blur in a dive.
        void DrawQueenUnderlay(Enemy e, V2 pos, float r, float t)
        {
            if (e.Phase == 1)  // shielded by her drones: a shell of little hex cells
            {
                Circle(pos, r + 18f, new Rgba(1f, 0.86f, 0.47f, 0.1f), 40);
                for (int i = 0; i < 6; i++)
                    HexOutline(pos + V2.FromAngle(MathX.Tau * i / 6f + t * 0.3f) * (r + 8f), 14f, 2f, new Rgba(1f, 0.9f, 0.55f, 0.55f + 0.2f * Mathf.Sin(t * 6f)));
            }
            var q = e.Queen;
            if (q == null)
                return;
            if (q.TransT > 0f)
            {
                Circle(pos, r + 14f, new Rgba(1f, 1f, 1f, 0.08f), 32);
                Arc(pos, r + 12f, 3f, new Rgba(1f, 1f, 1f, 0.5f + 0.4f * Mathf.Sin(t * 18f)), 40);
                return;
            }
            if (q.Stage >= QueenStage.Frenzy)
                Circle(pos, r + 20f, new Rgba(1f, 0.35f, 0.2f, 0.12f + 0.06f * Mathf.Sin(t * 8f)), 32);
            float air = Game.QueenAir(q);
            if (air > 0f)  // up in the air: a blur of wings
            {
                Circle(pos, r + 26f * air, new Rgba(0.95f, 0.98f, 1f, 0.12f * air), 32);
                Arc(pos, r + 22f * air, 2f, new Rgba(1f, 1f, 1f, 0.4f * air), 32);
            }
            if (q.Turn == QueenTurn.LastStand)
                Arc(pos, r + 10f + 3f * Mathf.Sin(t * 14f), 3f, new Rgba(1f, 0.5f, 0.2f, 0.8f), 32);
        }

        /// An egg: a pale shell, the larva showing through, and a ring that runs down until it hatches (red near the end).
        void DrawEggDetails(Enemy e, V2 pos, float r, float t)
        {
            var queen = game.TheQueen();
            var q = queen != null ? queen.Queen : null;
            float left = q != null && q.Turn == QueenTurn.Eggs ? Mathf.Clamp01(1f - (q.TurnT - 0.6f) / 6f) : 1f;
            float wob = left < 0.25f ? Mathf.Sin(t * 30f) * 2f : 0f;
            Circle(pos + new V2(wob, 3f), r * 0.5f, new Rgba(0.47f, 0.67f, 0.24f, 0.55f), 12);
            Circle(pos + new V2(-r * 0.3f, -r * 0.35f), r * 0.18f, new Rgba(1f, 1f, 1f, 0.6f), 8);
            Arc(pos, r + 12f, 3f, left < 0.25f ? new Rgba(1f, 0.35f, 0.24f, 0.9f) : new Rgba(1f, 0.82f, 0.4f, 0.85f), 32, -MathX.Pi / 2f, MathX.Tau * left);
        }

        /// A honey glob: amber, with a glint.
        void DrawHoneyShot(Bullet b)
        {
            float r = b.Radius;
            Circle(b.Pos, r * 1.6f, new Rgba(1f, 0.67f, 0.16f, 0.22f), 12);
            Circle(b.Pos, r, new Rgba(1f, 0.67f, 0.16f), 12);
            Circle(b.Pos + new V2(-2f, -2f), r * 0.4f, new Rgba(1f, 0.94f, 0.7f, 0.8f), 8);
        }
    }
}
