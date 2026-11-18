using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Grid rooms on the build screen (GridRooms.cs, Docs/GridRooms.md): a footprint palette, a glowing door spot on every wall where the picked
    /// room fits, the room under the mouse as a ghost, a click to build it. Called from DrawExpansionDraw.
    public partial class ArenaApp
    {
        GridShape expGrid;                   // the footprint picked on the build screen
        List<GridOption> gridOpts;           // the picked footprint's legal spots (rebuilt when the map or the footprint changes)
        int gridOptsKey = int.MinValue;

        static readonly Rgba GridInk = new Rgba(0.35f, 0.85f, 0.78f), GridOk = new Rgba(0.35f, 1f, 0.55f), GridNo = new Rgba(1f, 0.3f, 0.36f);

        List<GridOption> GridOptionsNow()
        {
            int key = (int)expGrid * 7919 + game.Expanded.Count * 131;
            foreach (var r in game.Expanded)
                key = key * 31 + r.X * 7 + r.Y * 13 + r.W + r.H * 3;
            if (gridOpts == null || key != gridOptsKey)
            {
                gridOpts = Expansion.GridOptions(game.Expanded, expGrid);
                gridOptsKey = key;
            }
            return gridOpts;
        }

        /// The spot under the mouse (world point `wm`): the one whose room covers it (the nearest middle if several), else the nearest door spot within 5 tiles.
        GridOption? GridAt(V2 wm)
        {
            GridOption? best = null;
            float bd = float.MaxValue;
            foreach (var o in GridOptionsNow())
            {
                var q = o.Room;
                if (wm.X >= q.X && wm.X < q.X1 && wm.Y >= q.Y && wm.Y < q.Y1 && o.Mid.DistanceTo(wm) < bd)
                {
                    bd = o.Mid.DistanceTo(wm);
                    best = o;
                }
            }
            if (best.HasValue)
                return best;
            bd = 500f;
            foreach (var o in GridOptionsNow())
            {
                float d = new V2(o.SX, o.SY).DistanceTo(wm);
                if (d < bd) { bd = d; best = o; }
            }
            return best;
        }

        /// Grid mode's mouse and keys over the map. Returns the spot under the mouse; a click builds it.
        GridOption? GridModeInput(Event ev, bool over, V2 wm, bool free, out int fits)
        {
            fits = GridOptionsNow().Count;
            if ((ev.type == EventType.MouseDown && ev.button == 1) || (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.Escape))
            {
                if (free)
                    expOpen = false;
                ev.Use();
                return null;
            }
            if (ev.type == EventType.KeyDown && (ev.keyCode == KeyCode.Q || ev.keyCode == KeyCode.E))
            {
                int n = Expansion.GridSizes.Length;
                expGrid = (GridShape)(((int)expGrid + (ev.keyCode == KeyCode.E ? 1 : n - 1)) % n);
                ev.Use();
            }
            if (!over)
                return null;
            var pick = GridAt(wm);
            if (pick.HasValue && ev.type == EventType.MouseDown && ev.button == 0)
            {
                if (pick.Value.Room.Price <= game.SpendableVectors(game.Me))
                    game.ProposeExpandRoom(pick.Value.Room.X, pick.Value.Room.Y, pick.Value.Room.W, pick.Value.Room.H);
                ev.Use();
                return null;
            }
            return pick;
        }

        /// The door spots on the map (inside the GL pass): a pulsing mark on each wall spot the picked room fits, the room under the mouse lit.
        void DrawGridGhost(System.Func<V2, V2> M, GridOption? pick)
        {
            float pulse = 0.55f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f);
            foreach (var o in GridOptionsNow())
            {
                V2 c = M(new V2(o.SX, o.SY));
                RoomMapRect(c.X - 4f, c.Y - 4f, 8f, 8f, GridInk.WithAlpha(pulse));
                RoomMapFrame(c.X - 6f, c.Y - 6f, 12f, 12f, 1f, GridInk.WithAlpha(0.5f));
            }
            if (!pick.HasValue)
                return;
            var q = pick.Value.Room;
            bool afford = q.Price <= game.SpendableVectors(game.Me);
            var ink = afford ? GridOk : GridNo;
            V2 p0 = M(new V2(q.X, q.Y)), p1 = M(new V2(q.X1, q.Y1));
            RoomMapRect(p0.X, p0.Y, p1.X - p0.X, p1.Y - p0.Y, ink.WithAlpha(0.2f + 0.08f * Mathf.Sin(Time.unscaledTime * 4f)));
            RoomMapFrame(p0.X, p0.Y, p1.X - p0.X, p1.Y - p0.Y, 1.5f, ink.WithAlpha(0.9f));
            V2 d = M(new V2(pick.Value.SX, pick.Value.SY));
            RoomMapRect(d.X - 5f, d.Y - 5f, 10f, 10f, ink);
        }

        /// The status line for grid mode.
        string GridRoomLine(GridOption? pick, int fits, bool over, out Color ink)
        {
            ink = Ui.Muted;
            string name = Expansion.GridName(expGrid).ToUpperInvariant();
            if (fits == 0)
            {
                ink = new Color(1f, 0.45f, 0.5f);
                return "No door spot fits a " + name.ToLowerInvariant() + ": try another size.";
            }
            if (!pick.HasValue)
                return over ? "Move along a wall: the glowing marks are the door spots." : "Point at the map: the glowing marks are the door spots.";
            int price = pick.Value.Room.Price, have = game.SpendableVectors(game.Me);
            if (price > have)
            {
                ink = new Color(1f, 0.45f, 0.5f);
                return name + "  ·  needs " + price + " vectors, you have " + have;
            }
            ink = Ui.Gold;
            return name + "  ·  " + price + " VECTORS (of " + have + ")  ·  click to build it";
        }

        /// The footprint palette: two a row, each a little grid of cells and its name.
        void DrawGridPalette(Rect area, bool calm)
        {
            Ui.Label(new Rect(area.x, area.y, area.width, 16f), "ROOM SIZE  ·  Q / E", 10, Ui.Muted, TextAnchor.MiddleLeft, true);
            float gap = 6f, cw = (area.width - gap) / 2f, ch = 78f, y0 = area.y + 20f;
            for (int i = 0; i < Expansion.GridSizes.Length; i++)
            {
                var shape = (GridShape)i;
                var r = new Rect(area.x + (i % 2) * (cw + gap), y0 + (i / 2) * (ch + gap), cw, ch);
                bool on = shape == expGrid;
                if (CrtUi.SoftKey(r, "", null, 11, calm, lit: on))
                    expGrid = shape;
                var ink = on ? Color.white : ToColor(GridInk);
                DrawGridGlyph(shape, new Rect(r.x + 8f, r.y + 6f, r.width - 16f, 42f), ink);
                Ui.Label(new Rect(r.x, r.y + 54f, r.width, 16f), Expansion.GridName(shape).ToUpperInvariant(), 11, ink, TextAnchor.MiddleCenter, true);
                if (TipHover(r))
                {
                    TipStart(r, Expansion.GridName(shape), ToColor(GridInk));
                    TipAdd(Expansion.GridBlurb(shape), 12, Ui.Text);
                    TipAdd("Pick it, point at a door spot on a wall, click. Q and E step through the sizes.", 11, Ui.Muted);
                }
            }
            float y = y0 + 2f * (ch + gap);
            Ui.Label(new Rect(area.x, y, area.width, 44f), "Rooms hang off the walls of rooms you have built, centred on a door spot. A room's price is its new wall in vectors.", 10, Ui.Muted, TextAnchor.UpperLeft, true, true);
        }

        /// A footprint as cells: one square per 6 x 6 tile cell, fitted and centred in `r`.
        static void DrawGridGlyph(GridShape shape, Rect r, Color ink)
        {
            int cols = Expansion.GridSizes[(int)shape][0] / Expansion.GridCell, rows = Expansion.GridSizes[(int)shape][1] / Expansion.GridCell;
            float s = Mathf.Min(r.width / 2f, r.height / 2f);
            float x0 = r.center.x - cols * s / 2f, y0 = r.center.y - rows * s / 2f;
            for (int c = 0; c < cols; c++)
                for (int k = 0; k < rows; k++)
                    Ui.Fill(new Rect(x0 + c * s + 1f, y0 + k * s + 1f, s - 2f, s - 2f), new Color(ink.r, ink.g, ink.b, 0.55f));
        }
    }
}
