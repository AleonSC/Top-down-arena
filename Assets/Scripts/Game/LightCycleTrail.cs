using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Light Cycle trail (Rank.cs, rank 45): a wall of light that follows where you've been but snaps to long straight runs
    /// with 90 degree corners, like a light cycle's. The floor tiles it crosses light up and fade, and the wall itself breaks into
    /// small squares that drift off as it ages (Grid wake and Derezz from Docs/Mockups/tron-trail.html). Drawing only, like every
    /// rank trail: each screen keeps one point per corner (not one every few pixels) and draws it from the lobby's trail number.
    public partial class ArenaApp
    {
        const string CycleId = "trail-lightcycle";
        const float CycleLife = 2.4f;    // how long a bit of wall lasts
        const float CycleTurn = 50f;     // sideways drift that makes the wall turn a corner
        const float CycleCell = 40f;     // the floor tiles that light up
        const float CycleSolid = 0.42f;  // the share of its life a bit of wall stays solid before it breaks apart
        static readonly Rgba CycleCol = new Rgba(70f / 255f, 230f / 255f, 1f);

        /// One player's wall: its corners (each with when it was made), and the axis it's holding.
        class CycleRider
        {
            public readonly List<TrailPoint> Corners = new List<TrailPoint>();
            public bool Horizontal = true, Ready;
            public float Fx, Fy, LastMove;
            public V2 Pos, Head;

            public void Reset()
            {
                Corners.Clear();
                Ready = false;
            }

            /// Follow the player: hold the wall on one axis until they drift CycleTurn sideways, then turn 90 degrees there.
            public void Step(V2 pos, float t)
            {
                if (Ready && (pos - Pos).Length > 150f)
                    Reset();  // teleported (a Substitution, a respawn): no wall across the gap
                if (!Ready)
                {
                    Ready = true;
                    Fx = pos.X;
                    Fy = pos.Y;
                    Corners.Add(new TrailPoint { At = pos, T = t });
                    LastMove = t;
                }
                if ((pos - Pos).LengthSq > 0.25f)
                    LastMove = t;
                Pos = pos;
                if (Horizontal)
                {
                    if (Mathf.Abs(pos.Y - Fy) > CycleTurn)
                    {
                        Corners.Add(new TrailPoint { At = new V2(pos.X, Fy), T = t });
                        Horizontal = false;
                        Fx = pos.X;
                    }
                }
                else if (Mathf.Abs(pos.X - Fx) > CycleTurn)
                {
                    Corners.Add(new TrailPoint { At = new V2(Fx, pos.Y), T = t });
                    Horizontal = true;
                    Fy = pos.Y;
                }
                Head = Horizontal ? new V2(pos.X, Fy) : new V2(Fx, pos.Y);
                while (Corners.Count > 1 && t - Corners[1].T > CycleLife)
                    Corners.RemoveAt(0);
            }
        }

        struct CyclePiece
        {
            public V2 A, B;
            public float Age;  // 0 new, 1 gone
            public int Key;
        }

        // Scratch lists, reused every frame.
        static readonly List<V2> cycleAt = new List<V2>();
        static readonly List<float> cycleAge = new List<float>();
        static readonly List<CyclePiece> cyclePieces = new List<CyclePiece>();
        static readonly Dictionary<int, float> cycleLit = new Dictionary<int, float>();
        readonly Dictionary<int, CycleRider> cycles = new Dictionary<int, CycleRider>();

        /// The wall as a polyline: the corners and the head, each with its age in seconds.
        static void CycleFill(CycleRider r, float t)
        {
            cycleAt.Clear();
            cycleAge.Clear();
            foreach (var c in r.Corners)
            {
                cycleAt.Add(c.At);
                cycleAge.Add(t - c.T);
            }
            cycleAt.Add(r.Head);
            cycleAge.Add(t - r.LastMove);  // standing still lets the whole wall fade
        }

        /// Cut the polyline into pieces of about 10 px, each with its age as a share of its life.
        static void CycleCut()
        {
            cyclePieces.Clear();
            for (int i = 1; i < cycleAt.Count; i++)
            {
                V2 a = cycleAt[i - 1], b = cycleAt[i];
                float len = (b - a).Length;
                int n = Mathf.Max(1, Mathf.RoundToInt(len / 10f));
                for (int k = 0; k < n; k++)
                {
                    float f0 = k / (float)n, f1 = (k + 1) / (float)n;
                    float ag = (cycleAge[i - 1] + (cycleAge[i] - cycleAge[i - 1]) * (f0 + f1) / 2f) / CycleLife;
                    if (ag < 1f)
                        cyclePieces.Add(new CyclePiece { A = a + (b - a) * f0, B = a + (b - a) * f1, Age = Mathf.Max(0f, ag), Key = i * 100 + k });
                }
            }
        }

        static Rgba CycleWhite(float f) => new Rgba(CycleCol.R + (1f - CycleCol.R) * f, CycleCol.G + (1f - CycleCol.G) * f, CycleCol.B + (1f - CycleCol.B) * f);

        /// GL: the wall for the rider, shifted so `origin` is at (0, 0) (the arena passes zero; a menu model passes the player's spot).
        void DrawCycleTrail(CycleRider r, float t, V2 origin)
        {
            if (!r.Ready)
                return;
            CycleFill(r, t);
            DrawCycleWall(origin, r.Pos);
        }

        void DrawCycleWall(V2 origin, V2 pos)
        {
            CycleCut();
            if (cyclePieces.Count == 0)
                return;
            if (!fxOn)  // Visual effects off: one flat line
            {
                foreach (var p in cyclePieces)
                    Line(p.A - origin, p.B - origin, 2f, CycleWhite(0.8f).WithAlpha(0.85f * (1f - p.Age)));
                return;
            }
            // The floor tiles the wall crosses light up, and fade as the wall that lit them ages.
            cycleLit.Clear();
            foreach (var p in cyclePieces)
            {
                V2 m = (p.A + p.B) * 0.5f;
                int key = (Mathf.FloorToInt(m.X / CycleCell) + 512) * 4096 + Mathf.FloorToInt(m.Y / CycleCell) + 512;  // (a cell number, offset so it is never negative)
                float old;
                if (!cycleLit.TryGetValue(key, out old) || old > p.Age)
                    cycleLit[key] = p.Age;
            }
            foreach (var kv in cycleLit)
            {
                int cx = kv.Key / 4096 - 512, cy = kv.Key % 4096 - 512;
                float a = 1f - kv.Value, x = cx * CycleCell - origin.X, y = cy * CycleCell - origin.Y;
                FillRect(x + 1f, y + 1f, CycleCell - 2f, CycleCell - 2f, CycleCol.WithAlpha(0.15f * a));
                var edge = CycleCol.WithAlpha(0.42f * a);
                FillRect(x + 1f, y + 1f, CycleCell - 2f, 1f, edge);
                FillRect(x + 1f, y + CycleCell - 2f, CycleCell - 2f, 1f, edge);
                FillRect(x + 1f, y + 1f, 1f, CycleCell - 2f, edge);
                FillRect(x + CycleCell - 2f, y + 1f, 1f, CycleCell - 2f, edge);
            }
            // The wall: solid light while it's new, then gone in a blink as it breaks into squares.
            foreach (var p in cyclePieces)
            {
                float a = p.Age < CycleSolid ? 1f : Mathf.Max(0f, 1f - (p.Age - CycleSolid) / 0.1f);
                if (a <= 0f)
                    continue;
                V2 a0 = p.A - origin, b0 = p.B - origin;
                Line(a0, b0, 10f, CycleCol.WithAlpha(0.10f * a));
                Line(a0, b0, 4.5f, CycleCol.WithAlpha(0.34f * a));
                Line(a0, b0, 2f, CycleWhite(0.8f).WithAlpha(0.95f * a));
            }
            for (int i = 1; i < cycleAt.Count - 1; i++)  // glowing corners
            {
                float ag = cycleAge[i] / CycleLife;
                if (ag < CycleSolid)
                    Circle(cycleAt[i] - origin, 3.2f, CycleWhite(0.85f).WithAlpha(0.9f * (1f - ag)), 8);
            }
            // The squares: as a bit of wall passes its solid part, three of them lift off and drift away, fading.
            foreach (var p in cyclePieces)
            {
                if (p.Age < CycleSolid - 0.05f)
                    continue;
                float f = Mathf.Clamp01((p.Age - CycleSolid) / (1f - CycleSolid));
                V2 d = p.B - p.A;
                float len = Mathf.Max(0.001f, d.Length);
                for (int j = 0; j < 3; j++)
                {
                    float h = FrameHash((int)(p.Key * 3.1f * 10f) + j), h2 = FrameHash((int)(p.Key * 7.7f * 10f) + j + 91);
                    float s = 2f + 3f * h, side = (h2 - 0.5f) * 2f, spread = 4f + 26f * f;
                    V2 at = p.A + d * h + new V2(-d.Y / len, d.X / len) * (side * spread) + new V2((h2 - 0.5f) * 10f * f, -36f * f * f - 6f * f) - origin;
                    FillRect(at.X - s / 2f, at.Y - s / 2f, s, s, CycleWhite(0.5f * (1f - f)).WithAlpha((1f - f) * 0.95f));
                }
            }
            V2 link = cycleAt[cycleAt.Count - 1] - origin;  // the last stretch to the player, so the wall always reaches them
            Line(link, pos - origin, 2f, CycleCol.WithAlpha(0.35f));
        }

        /// GL: a fixed L of wall flowing off to the left of a menu figure at the origin (the cosmetics window's sample).
        void DrawCycleSample(float len, float amp)
        {
            cycleAt.Clear();
            cycleAge.Clear();
            float turnX = -len * 0.42f, drop = Mathf.Max(4f, amp * 1.6f);
            cycleAt.Add(new V2(-len, drop)); cycleAge.Add(2.2f);
            cycleAt.Add(new V2(turnX, drop)); cycleAge.Add(1.5f);
            cycleAt.Add(new V2(turnX, 0f)); cycleAge.Add(0.8f);
            cycleAt.Add(V2.Zero); cycleAge.Add(0.05f);
            DrawCycleWall(V2.Zero, V2.Zero);
        }
    }
}
