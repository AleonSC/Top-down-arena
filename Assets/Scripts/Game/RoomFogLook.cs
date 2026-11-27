using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The look of room fog (Docs/FogOfWar.md, Core/RoomFog.cs): an unseen room under a dark veil with slow diagonal hatching
    /// (its walls, doors and breaches stay bright), its enemies and pickups hidden unless a player is within peeking reach; as
    /// it's seen the veil lifts behind a gold scan line. On the maps: dark with a "?" (RoomsLook.cs, ExpansionUi.cs).
    public partial class ArenaApp
    {
        static readonly Rgba FogVeil = new Rgba(0.01f, 0.015f, 0.035f, 0.72f);
        static readonly Rgba FogHatch = new Rgba(0.45f, 0.55f, 0.75f, 0.05f);
        static readonly Rgba FogMapFill = new Rgba(0.02f, 0.03f, 0.06f, 0.92f);
        static readonly Color RoomFogInk = new Color(0.62f, 0.7f, 0.86f, 0.75f);

        readonly HashSet<int> fogDark = new HashSet<int>();                 // areas drawn unseen last frame
        readonly Dictionary<int, float> fogLift = new Dictionary<int, float>();  // area -> when it was seen (the sweep)
        int fogMap = -1;  // the map number the areas belong to (a map reset renumbers them)

        /// DrawArenaContents, over the floor and under the walls: the veil on each unseen room, the lift on a room just seen.
        void DrawRoomFogVeil(float t)
        {
            if (!game.ExpandingOn || !game.RoomsOn)
                return;
            if (fogMap != game.MapNumber)
            {
                fogMap = game.MapNumber;
                fogDark.Clear();
                fogLift.Clear();
            }
            var wd = game.CurrentWorld;
            float now = Time.unscaledTime;
            for (int i = 1; i < wd.Areas.Length; i++)
            {
                var a = wd.Areas[i];
                if (game.AreaUnseen(i))
                {
                    fogDark.Add(i);
                    DrawFogCells(i, a, t);
                    continue;
                }
                if (fogDark.Remove(i))
                    fogLift[i] = now;
                float at;
                if (!fogLift.TryGetValue(i, out at))
                    continue;
                float k = (now - at) / RoomFog.RevealTime;
                if (k >= 1f)
                {
                    fogLift.Remove(i);
                    continue;
                }
                // The lift: the veil pulls back left to right behind a gold scan line, a soft gold flash over what's uncovered.
                float x = Mathf.Lerp(a.Min.X, a.Max.X, k);
                var rest = new RoomArea { Min = new V2(x, a.Min.Y), Max = a.Max };
                DrawFogBox(rest, 1f - k * 0.5f, t);
                FillRect(a.Min.X, a.Min.Y, x - a.Min.X, a.Size.Y, DoorGold.WithAlpha(0.12f * (1f - k)));
                Line(new V2(x, a.Min.Y), new V2(x, a.Max.Y), 4f, DoorGold.WithAlpha(0.9f * (1f - k * k)));
            }
        }

        /// An unseen room's box under the cone (Docs/FogOfWar.md): cells nobody has looked at are dark and hatched, cells explored but
        /// out of sight are dim (the layout is remembered, what moves in it isn't shown), cells in somebody's cone are clear.
        // DrawFogCells' scratch, reused for every fogged room each frame (cleared at the start).
        readonly Dictionary<long, int> fogOpen = new Dictionary<long, int>();
        readonly List<long> fogPrev = new List<long>(), fogCur = new List<long>();

        void DrawFogCells(int area, RoomArea a, float t)
        {
            int cols, rows;
            RoomFog.Grid(a, out cols, out rows);
            var open = fogOpen;   // (first cell, end cell, state) -> the row the block started on
            var prev = fogPrev;
            var cur = fogCur;
            open.Clear();
            prev.Clear();
            for (int r = 0; r <= rows; r++)
            {
                cur.Clear();
                for (int c = 0; r < rows && c < cols;)
                {
                    int state = FogCellState(area, a, c, r), c1 = c + 1;
                    while (c1 < cols && FogCellState(area, a, c1, r) == state)
                        c1++;
                    if (state < 2)
                        cur.Add(((long)(uint)c << 40) | ((long)(uint)c1 << 8) | (long)(uint)state);
                    c = c1;
                }
                foreach (long k in prev)  // a block ends when the next row doesn't continue it: draw it once, however tall
                    if (!cur.Contains(k))
                    {
                        DrawFogBlock(a, k, open[k], r, t);
                        open.Remove(k);
                    }
                foreach (long k in cur)
                    if (!open.ContainsKey(k))
                        open[k] = r;
                var swap = prev; prev = cur; cur = swap;
            }
        }

        void DrawFogBlock(RoomArea a, long key, int r0, int r1, float t)
        {
            int c0 = (int)(key >> 40), c1 = (int)((key >> 8) & 0xFFFFFFFFL), state = (int)(key & 0xFF);
            float x0 = a.Min.X + c0 * RoomFog.Cell, y0 = a.Min.Y + r0 * RoomFog.Cell;
            float x1 = Mathf.Min(a.Max.X, a.Min.X + c1 * RoomFog.Cell), y1 = Mathf.Min(a.Max.Y, a.Min.Y + r1 * RoomFog.Cell);
            if (state == 0)
                DrawFogBox(new RoomArea { Min = new V2(x0, y0), Max = new V2(x1, y1) }, 1f, t);
            else
                FillRect(x0, y0, x1 - x0, y1 - y0, FogVeil.WithAlpha(0.3f));
        }

        /// 0: unexplored, 1: explored but nobody is looking, 2: in somebody's cone or circle.
        int FogCellState(int area, RoomArea a, int c, int r)
        {
            if (game.FogSees(RoomFog.CellMid(a, c, r)))
                return 2;
            return game.FogExplored(area - 1, c, r) ? 1 : 0;
        }

        /// One box of veil: the dark fill and diagonal hatching drifting slowly (the lines are laid across the world, so boxes side by side join up).
        void DrawFogBox(RoomArea a, float alpha, float t)
        {
            var sz = a.Size;
            if (sz.X <= 1f || sz.Y <= 1f)
                return;
            FillRect(a.Min.X, a.Min.Y, sz.X, sz.Y, FogVeil.WithAlpha(FogVeil.A * alpha));
            const float step = 48f;
            float drift = (t * 10f) % step;
            float s0 = Mathf.Floor((a.Min.X - a.Max.Y - drift) / step) * step + drift;
            for (float s = s0; s < a.Max.X - a.Min.Y; s += step)  // lines x - y = s across the box, clipped to it
            {
                float x0 = Mathf.Max(a.Min.X, s + a.Min.Y), x1 = Mathf.Min(a.Max.X, s + a.Max.Y);
                if (x1 <= x0)
                    continue;
                Line(new V2(x0, x0 - s), new V2(x1, x1 - s), 2f, FogHatch.WithAlpha(FogHatch.A * alpha));
            }
        }

        /// The maps: an unseen room's box drawn dark (over its fill), for the "?" afterwards.
        void DrawFogMapBox(V2 p0, V2 p1, float explored)
        {
            RoomMapRect(p0.X, p0.Y, p1.X - p0.X, p1.Y - p0.Y, FogMapFill.WithAlpha(FogMapFill.A * (1f - 0.65f * explored)));  // (the more of it you've explored, the lighter)
        }

        /// The maps, after the GL pass: a "?" in the middle of each unseen room (its head box), sized to the room.
        void DrawFogMapMarks(System.Func<V2, V2> M, Rect clip)
        {
            if (!game.ExpandingOn)
                return;
            var rooms = game.Expanded;
            for (int i = 0; i < rooms.Count; i++)
            {
                var r = rooms[i];
                if (!r.Unseen || Expansion.HeadOf(rooms, i) != i)
                    continue;
                V2 p0 = M(new V2(r.X, r.Y)), p1 = M(new V2(r.X1, r.Y1));
                float h = Mathf.Min(p1.X - p0.X, p1.Y - p0.Y);
                if (h < 7f)
                    continue;
                var c = new V2((p0.X + p1.X) / 2f, (p0.Y + p1.Y) / 2f);
                if (!clip.Contains(new Vector2(c.X, c.Y)))
                    continue;
                int size = Mathf.Clamp(Mathf.RoundToInt(h * 0.6f), 8, 22);
                Ui.Label(new Rect(c.X - 20f, c.Y - size, 40f, 2f * size), "?", size, RoomFogInk, TextAnchor.MiddleCenter, true);
            }
        }

        /// Dormant groups (Core/DormantGroups.cs): small z's drifting up off each sleeping enemy, staggered so a group breathes out of step.
        void DrawDormantMarks(float t)
        {
            var ink = new Rgba(0.62f, 0.75f, 1f);
            foreach (var e in game.Enemies.Values)
            {
                if (!e.Dormant || e.Hp <= 0f || game.FogHidesStill(e.Pos))
                    continue;
                for (int k = 0; k < 2; k++)
                {
                    float ph = (t * 0.45f + k * 0.5f + (e.Id % 7) * 0.13f) % 1f;  // each z rises and fades over ~2 s
                    float s = (5f + 4f * ph) * (k == 0 ? 1f : 0.8f);
                    V2 c = e.Disp + new V2(e.Radius * 0.6f + 10f * ph, -e.Radius - 6f - 26f * ph);
                    var col = ink.WithAlpha(0.85f * (1f - ph) * Mathf.Clamp01(ph * 5f));
                    Line(c + new V2(-s, -s), c + new V2(s, -s), 2f, col);
                    Line(c + new V2(s, -s), c + new V2(-s, s), 2f, col);
                    Line(c + new V2(-s, s), c + new V2(s, s), 2f, col);
                }
            }
        }
    }
}
