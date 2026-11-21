using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Cell Editor's ROOM VIEW tab: a piece shape (single to tetromino) at a lattice spot, each cell with the layout the game would give it
    /// (`CellLayouts.PlansOfCells`, the same roll as a built piece), so the cells can be judged together. Click a cell to edit its layout; TEST builds the piece in your run.
    public partial class ArenaApp
    {
        int cedShape, cedOrient, cedPosX, cedPosY;  // the shape, its turn / mirror, and the lattice cell of its top-left corner (the roll is seeded from the place)

        void CedDrawTurned(Rect r, CellLayouts.Turned t)
        {
            float k = r.width / CellLayouts.Cell;
            foreach (var w in t.Walls)
            {
                var wr = new Rect(r.x + w[0] * k, r.y + w[1] * k, w[2] * k, w[3] * k);
                Ui.Fill(wr, CrtStyle.Ink.WithAlpha(0.55f));
            }
            float d = Mathf.Max(4f, 22f * k);
            foreach (var s in t.Sleepers)
                Ui.Dot(new Rect(r.x + s[0] * k - d / 2f, r.y + s[1] * k - d / 2f, d, d), CrtStyle.Head);
            foreach (var p in t.Traps)
                Ui.Dot(new Rect(r.x + p[0] * k - d / 2f, r.y + p[1] * k - d / 2f, d, d), CrtStyle.Danger, p[2] == 1);
            foreach (var p in t.Loot)
                Ui.Dot(new Rect(r.x + p[0] * k - d / 2f, r.y + p[1] * k - d / 2f, d, d), CrtStyle.Gold);
        }

        void DrawCedRooms(Rect body)
        {
            var shape = (PieceShape)cedShape;
            var orients = Expansion.PieceShapeOrients(shape);
            cedOrient = Mathf.Clamp(cedOrient, 0, orients.Count - 1);
            var cells = orients[cedOrient];

            var col = DkStack.Column(new Rect(body.x, body.y, 210f, body.height), 3f);
            for (int i = 0; i < 9; i++)
                if (DkChoice(col.Next(26f), Expansion.PieceShapeName((PieceShape)i) + "  (" + Expansion.PieceShapeCells((PieceShape)i) + ")", i == cedShape, Ui.Primary) && i != cedShape)
                {
                    cedShape = i;
                    cedOrient = 0;
                }
            col.Space(8f);
            var tr = DkStack.Cells(col.Next(28f), 2, 4f);
            if (DkKey(tr[0], "TURN " + (cedOrient + 1) + "/" + orients.Count, Ui.Neutral, false, 11))
                cedOrient = (cedOrient + 1) % orients.Count;
            if (DkKey(tr[1], "SHUFFLE", Ui.Neutral, false, 11))
            {
                cedPosX = UnityEngine.Random.Range(-40, 40);
                cedPosY = UnityEngine.Random.Range(-40, 40);
            }
            var mv = DkStack.Cells(col.Next(28f), 4, 3f);
            if (DkKey(mv[0], "<", Ui.Neutral, false, 12)) cedPosX--;
            if (DkKey(mv[1], ">", Ui.Neutral, false, 12)) cedPosX++;
            if (DkKey(mv[2], "^", Ui.Neutral, false, 12)) cedPosY--;
            if (DkKey(mv[3], "v", Ui.Neutral, false, 12)) cedPosY++;
            Ui.Label(col.Next(18f), "lattice cell " + cedPosX + ", " + cedPosY, 10, Ui.Muted);
            col.Space(4f);

            // The plans, as the game rolls them for this piece at this spot
            var keys = new HashSet<long>();
            var keyOf = new long[cells.Length];
            for (int i = 0; i < cells.Length; i++)
            {
                keyOf[i] = CellLayouts.CellKey(cedPosX + cells[i][0], cedPosY + cells[i][1]);
                keys.Add(keyOf[i]);
            }
            var plans = CellLayouts.PlansOfCells(keys);

            if (DkKey(col.Next(32f), "TEST THIS SHAPE", CrtStyle.Head, false, 12))
            {
                CedApplyAll();
                var arr = new CellLayouts.CellPlan[cells.Length];
                for (int i = 0; i < arr.Length; i++)
                    arr[i] = plans[keyOf[i]];
                string why = game.DevTestCells(shape, cedOrient, arr);
                if (why.Length > 0)
                    cedNote = why;
                else
                    cellEditorOpen = false;
            }
            if (cedNote.Length > 0)
                Ui.Label(col.Rest(), cedNote, 10, CrtStyle.Head, TextAnchor.UpperLeft, false, true);

            // The piece
            var area = new Rect(body.x + 226f, body.y, body.width - 226f, body.height);
            var size = Expansion.PieceShapeSize(cells);
            float cs = Mathf.Min(Mathf.Min(area.width / size[0], area.height / size[1]), 230f);
            var origin = new Vector2(area.x + (area.width - cs * size[0]) / 2f, area.y + (area.height - cs * size[1]) / 2f);
            var occupied = new HashSet<long>();
            foreach (var c in cells)
                occupied.Add(CellLayouts.CellKey(c[0], c[1]));
            var e = Event.current;
            for (int i = 0; i < cells.Length; i++)
            {
                var r = new Rect(origin.x + cells[i][0] * cs, origin.y + cells[i][1] * cs, cs, cs);
                var plan = plans[keyOf[i]];
                Ui.Fill(r, new Color(0.02f, 0.04f, 0.09f, 1f));
                CedDrawTurned(r, CellLayouts.TurnedOf(plan));
                CrtKit.Frame(r, 1f, CrtStyle.Ink.WithAlpha(0.25f));
                bool hover = r.Contains(e.mousePosition);
                if (hover)
                    Ui.Fill(r, CrtStyle.Gold.WithAlpha(0.08f));
                Ui.Label(new Rect(r.x + 5f, r.y + 3f, r.width - 10f, 14f), CellLayouts.All[plan.Layout].Name + (plan.Rot > 0 ? "  " + plan.Rot * 90 + "°" : ""), 10, CrtStyle.Gold.WithAlpha(0.8f));
                if (hover && e.type == EventType.MouseDown && e.button == 0)
                {
                    cedIndex = plan.Layout;
                    cedRot = plan.Rot;
                    cedTab = 0;
                    cedUndo.Clear();
                    e.Use();
                }
            }
            // the room's outer wall: every cell side with no cell of the piece on the other side
            int[][] dirs = { new[] { 0, -1 }, new[] { 1, 0 }, new[] { 0, 1 }, new[] { -1, 0 } };
            foreach (var c in cells)
                for (int d = 0; d < 4; d++)
                {
                    if (occupied.Contains(CellLayouts.CellKey(c[0] + dirs[d][0], c[1] + dirs[d][1])))
                        continue;
                    float x = origin.x + c[0] * cs, y = origin.y + c[1] * cs;
                    Rect line = d == 0 ? new Rect(x, y - 1.5f, cs, 3f) : d == 2 ? new Rect(x, y + cs - 1.5f, cs, 3f)
                        : d == 3 ? new Rect(x - 1.5f, y, 3f, cs) : new Rect(x + cs - 1.5f, y, 3f, cs);
                    Ui.Fill(line, CrtStyle.Ink);
                }
        }
    }
}
