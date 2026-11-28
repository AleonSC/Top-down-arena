using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Arena
{
    /// The Cell Editor (F1 > LABS > CELL EDITOR): pick any of the grid cell layouts (`CellLayouts.All`), see it at full size and
    /// drag out walls, click to put sleeper, trap and loot spots, right click (or the ERASE tool) to remove. SAVE puts every edit
    /// into the game for the rooms built from then on and writes CellLayoutEdits.txt (`CellLayoutEdits`; fold.py writes it into
    /// the code). Numbers are cell-local, 0..600, snapped to 10.
    public partial class ArenaApp
    {
        enum CellTool { Wall, Sleeper, Trap, WallTrap, Loot, Erase }

        static readonly string[] CellToolNames = { "WALL", "SLEEPER", "TRAP", "WALL TRAP", "LOOT", "ERASE" };
        const int CellSnap = 25, CellWallSnap = 50, CellMinWall = 50;  // the floor grid (RoomTraps.Tile): wall corners on its lines, walls a square thick; spots on lines or square middles

        UiScreen screenCellEd;
        int cedIndex;
        CellTool cedTool = CellTool.Wall;
        CellLayout[] cedWork;                       // a working copy per layout, kept while the editor is open
        readonly List<CellLayout> cedUndo = new List<CellLayout>();
        bool cedDragging;
        int cedWallIdx = -1;                        // a wall being dragged (its index) from cedWallGrab, which started at cedWallOrig
        Vector2 cedWallGrab, cedWallOrig;
        int cedMoveSet = -1, cedMoveIdx;            // a spot being dragged: which list (0 sleepers, 1 traps, 2 loot) and where in it
        Vector2 cedFrom, cedTo;                     // a wall being dragged, cell-local
        string cedNote = "";
        int cedMine = -1;                           // editing your own template (index in CellTemplateRequests.Mine), or -1 for a built-in layout
        int cedSel = -1;                            // the selected wall: its corners can be dragged
        int cedCorner = -1;                         // a corner being dragged (0 top left, 1 top right, 2 bottom left, 3 bottom right)
        Vector2 cedAnchor;                          // ...and the opposite corner, which stays put

        static string CellReqPath => Application.isEditor
            ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", CellTemplateRequests.FileName))
            : Path.Combine(Application.persistentDataPath, CellTemplateRequests.FileName);

        static string CellEditPath => Application.isEditor
            ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", CellLayoutEdits.FileName))
            : Path.Combine(Application.persistentDataPath, CellLayoutEdits.FileName);

        void LoadCellLayoutEdits()
        {
            LoadHubLayoutEdits();  // (HubEditorUi.cs)
            LoadCoreLayoutEdits();  // (CoreEditorUi.cs)
            try { CellTemplateRequests.Load(CellReqPath); }
            catch (Exception e) { Debug.Log("Couldn't read " + CellReqPath + ": " + e.Message); }
            try { CellLayoutEdits.Load(CellEditPath); }
            catch (Exception e) { Debug.Log("Couldn't read " + CellEditPath + ": " + e.Message); }
        }

        bool cellEditorOpen { get { InitScreens(); return screenCellEd.IsOpen; } set { InitScreens(); SetScreen(screenCellEd, value); } }

        void OpenCellEditor()
        {
            devMenuOpen = false;
            cedWork = new CellLayout[CellLayouts.All.Length];
            cedUndo.Clear();
            cedDragging = false;
            cedMoveSet = -1;
            cedWallIdx = -1;
            cedSel = -1;
            cedCorner = -1;
            cedMine = -1;
            cedNote = "";
            cellEditorOpen = true;
        }

        CellLayout CedLayout()
        {
            if (cedWork == null)
                cedWork = new CellLayout[CellLayouts.All.Length];
            if (cedMine >= 0 && cedMine < CellTemplateRequests.Mine.Count)
                return CellTemplateRequests.Mine[cedMine];
            cedMine = -1;
            return cedWork[cedIndex] ?? (cedWork[cedIndex] = CellLayoutEdits.Clone(CellLayouts.All[cedIndex]));
        }

        /// Put `l` in place of the layout being edited (UNDO, REVERT).
        void CedReplace(CellLayout l)
        {
            if (cedMine >= 0)
            {
                l.Name = CellTemplateRequests.Mine[cedMine].Name;
                CellTemplateRequests.Mine[cedMine] = l;
            }
            else
                cedWork[cedIndex] = l;
        }

        bool CedDirty(int i) => cedWork != null && cedWork[i] != null && CellLayoutEdits.Line(cedWork[i]) != CellLayoutEdits.Line(CellLayouts.All[i]);

        void CedPush()
        {
            cedUndo.Add(CellLayoutEdits.Clone(CedLayout()));
            if (cedUndo.Count > 60)
                cedUndo.RemoveAt(0);
        }

        void CedSave()
        {
            int n = 0;
            for (int i = 0; i < cedWork.Length; i++)
                if (CedDirty(i))
                {
                    CellLayoutEdits.Set(i, cedWork[i]);
                    n++;
                }
            try
            {
                CellLayoutEdits.Save(CellEditPath);
                bool any = n > 0 || cedTested;
                cedTested = false;
                cedNote = !any ? "Nothing new to save." : "Saved " + n + " layout" + (n == 1 ? "" : "s") + " to " + CellLayoutEdits.FileName + ". New rooms use them.";
            }
            catch (Exception e)
            {
                cedNote = "Couldn't write the file: " + e.Message;
            }
        }

        /// TEST: apply the edits in this game (not saved) and build this layout as a single cell in the run, standing you in it.
        void CedTestCell()
        {
            if (cedMine >= 0)
            {
                cedNote = "Your own templates can't be tested: the code has to add them first.";
                return;
            }
            CedApplyAll();
            string why = game.DevTestCells(PieceShape.Mono, 0, new[] { new CellLayouts.CellPlan { Layout = cedIndex, Rot = cedRot } });
            if (why.Length > 0)
            {
                cedNote = why;
                return;
            }
            cellEditorOpen = false;
        }

        /// Put every working copy into the game (not the file): what a test builds from.
        void CedApplyAll()
        {
            for (int i = 0; i < cedWork.Length; i++)
                if (CedDirty(i))
                {
                    CellLayoutEdits.Set(i, cedWork[i]);
                    cedTested = true;
                }
        }

        static int[][] CedAdd(int[][] a, int[] item)
        {
            Array.Resize(ref a, a.Length + 1);
            a[a.Length - 1] = item;
            return a;
        }

        static int[][] CedRemove(int[][] a, int i)
        {
            var r = new List<int[]>(a);
            r.RemoveAt(i);
            return r.ToArray();
        }

        /// Take out the spot or wall under a cell-local point (spots first, the newest wall on top); true if one went.
        bool CedErase(float x, float y)
        {
            var l = CedLayout();
            float best = 18f * 18f;
            int[][][] sets = { l.Sleepers, l.Traps, l.Loot };
            for (int s = 0; s < 3; s++)
                for (int i = sets[s].Length - 1; i >= 0; i--)
                {
                    float dx = sets[s][i][0] - x, dy = sets[s][i][1] - y;
                    if (dx * dx + dy * dy <= best)
                    {
                        CedPush();
                        if (s == 0) l.Sleepers = CedRemove(l.Sleepers, i);
                        else if (s == 1) l.Traps = CedRemove(l.Traps, i);
                        else l.Loot = CedRemove(l.Loot, i);
                        return true;
                    }
                }
            for (int i = l.Walls.Length - 1; i >= 0; i--)
            {
                var w = l.Walls[i];
                if (x >= w[0] - 4 && x <= w[0] + w[2] + 4 && y >= w[1] - 4 && y <= w[1] + w[3] + 4)
                {
                    CedPush();
                    l.Walls = CedRemove(l.Walls, i);
                    return true;
                }
            }
            return false;
        }

        /// The spot under a cell-local point: its list (0 sleepers, 1 traps, 2 loot) and index, or false.
        bool CedSpotAt(CellLayout l, float x, float y, out int set, out int idx)
        {
            int[][][] sets = { l.Sleepers, l.Traps, l.Loot };
            for (set = 0; set < 3; set++)
                for (idx = sets[set].Length - 1; idx >= 0; idx--)
                {
                    float dx = sets[set][idx][0] - x, dy = sets[set][idx][1] - y;
                    if (dx * dx + dy * dy <= 14f * 14f)
                        return true;
                }
            set = idx = -1;
            return false;
        }

        /// The newest wall under a cell-local point, or -1.
        static int CedWallAt(CellLayout l, float x, float y)
        {
            for (int i = l.Walls.Length - 1; i >= 0; i--)
            {
                var w = l.Walls[i];
                if (x >= w[0] && x <= w[0] + w[2] && y >= w[1] && y <= w[1] + w[3])
                    return i;
            }
            return -1;
        }

        /// A wall's corners: top left, top right, bottom left, bottom right (so corner i faces corner 3 - i).
        static Vector2[] CedCorners(int[] w) => new[]
        {
            new Vector2(w[0], w[1]), new Vector2(w[0] + w[2], w[1]), new Vector2(w[0], w[1] + w[3]), new Vector2(w[0] + w[2], w[1] + w[3]),
        };

        static int CedCornerAt(int[] w, float x, float y)
        {
            var cs = CedCorners(w);
            for (int i = 0; i < 4; i++)
                if ((cs[i].x - x) * (cs[i].x - x) + (cs[i].y - y) * (cs[i].y - y) <= 12f * 12f)
                    return i;
            return -1;
        }

        static int CedSnapTo(float v) => Mathf.Clamp(Mathf.RoundToInt(v / CellSnap) * CellSnap, 0, CellLayouts.Cell);
        static int CedWallSnapTo(float v) => Mathf.Clamp(Mathf.RoundToInt(v / CellWallSnap) * CellWallSnap, 0, CellLayouts.Cell);

        /// The wall a drag from `a` to `b` makes: at least 20 each way, inside the cell.
        static int[] CedWallOf(Vector2 a, Vector2 b)
        {
            int x0 = (int)Mathf.Min(a.x, b.x), y0 = (int)Mathf.Min(a.y, b.y);
            int w = Mathf.Max(CellMinWall, (int)Mathf.Abs(a.x - b.x)), h = Mathf.Max(CellMinWall, (int)Mathf.Abs(a.y - b.y));
            x0 = Mathf.Min(x0, CellLayouts.Cell - w);
            y0 = Mathf.Min(y0, CellLayouts.Cell - h);
            return new[] { x0, y0, w, h };
        }

        int cedTab;  // 0 cells, 1 room view (RoomViewUi.cs), 2 hub (HubEditorUi.cs)
        static readonly string[] CedTabNames = { "CELLS", "ROOM VIEW", "HUB", "CORE" };
        bool cedTested;  // layouts were applied by a test without SAVE

        void DrawCellEditor()
        {
            var panel = DevPanel;
            string sub = cedTab == 0 ? "Drag out walls (drag a wall to move it, Shift draws over one), click to place spots, drag a spot to move it, right click to remove. SAVE keeps the edits. Esc to close."
                : cedTab == 1 ? "A piece as the game builds it: every cell's layout together. Click a cell to edit its layout. Esc to close."
                : cedTab == 2 ? "The hub's floor plans (1600 x 900): drag walls and crates, right click to remove. SAVE keeps the edits. Esc to close."
                : "The core's ten floor plans: edit the bright part, the dim copies are its mirrors. Drag walls, click pillars and crates in, right click to remove. Esc to close.";
            var body = DkWindow(panel, "CELL EDITOR", sub, () => cellEditorOpen = false, CrtStyle.Gold);
            var tabs = DkStack.Cells(new Rect(body.x, body.y - 6f, 4 * 150f + 24f, 28f), 4, 8f);
            for (int i = 0; i < 4; i++)
                if (DkChoice(tabs[i], CedTabNames[i], cedTab == i, CrtStyle.Gold) && cedTab != i)
                {
                    cedTab = i;
                    if (i == 1)
                        CedApplyAll();  // (the room view shows what the game would build: your edits too)
                    cedDragging = false;
                    cedMoveSet = -1;
                    cedWallIdx = -1;
                    coreDragging = false;
                    coreMoveIdx = -1;
                    hubDragging = false;
                    hubMoveKind = -1;
                }
            body = new Rect(body.x, body.y + 30f, body.width, body.height - 30f);
            if (cedTab == 1)
                DrawCedRooms(body);
            else if (cedTab == 2)
                DrawCedHub(body);
            else if (cedTab == 3)
                DrawCedCore(body);
            else
                DrawCedCells(body);
        }

        int cedRot;  // the quarter turns TEST LAYOUT builds the cell with

        void DrawCedCells(Rect body)
        {
            var l = CedLayout();

            // The layouts, one column
            var listArea = new Rect(body.x, body.y, 210f, body.height);
            var col = DkStack.Column(listArea, 2f);
            for (int i = 0; i < CellLayouts.All.Length; i++)
            {
                bool edited = CedDirty(i) || CellLayoutEdits.IsEdited(i);
                if (DkChoice(col.Next(24f), (edited ? "* " : "") + (CellTemplateRequests.Deletes.Contains(CellLayouts.All[i].Name) ? "x " : "") + CellLayouts.All[i].Name, cedMine < 0 && i == cedIndex, edited ? CrtStyle.Gold : Ui.Primary) && i != cedIndex)
                {
                    cedIndex = i;
                    cedMine = -1;
                    cedSel = -1;
                    cedUndo.Clear();
                    cedDragging = false;
                    cedMoveSet = -1;
                    cedWallIdx = -1;
                    l = CedLayout();
                }
            }

            for (int i = 0; i < CellTemplateRequests.Mine.Count; i++)
                if (DkChoice(col.Next(24f), "+ " + CellTemplateRequests.Mine[i].Name, cedMine == i, CrtStyle.Head) && cedMine != i)
                {
                    cedMine = i;
                    cedSel = -1;
                    cedUndo.Clear();
                    cedDragging = false;
                    cedMoveSet = -1;
                    cedWallIdx = -1;
                    l = CedLayout();
                }

            // The cell
            const float size = 480f;
            var cell = new Rect(body.x + 226f, body.y + 4f, size, size);
            DrawCedCanvas(cell, l);

            // Tools
            var tools = new Rect(cell.xMax + 16f, body.y, body.xMax - cell.xMax - 16f, body.height);
            var tc = DkStack.Column(tools, 4f);
            Ui.Heading(tools.x, tc.Next(20f).y, tools.width, "Tool");
            for (int i = 0; i < CellToolNames.Length; i++)
                if (DkChoice(tc.Next(26f), CellToolNames[i], (int)cedTool == i, CrtStyle.Gold))
                    cedTool = (CellTool)i;
            tc.Space(6f);
            var ur = DkStack.Cells(tc.Next(26f), 2, 4f);
            bool canUndo = cedUndo.Count > 0;
            bool was = GUI.enabled;
            GUI.enabled = was && canUndo;
            if (DkKey(ur[0], "UNDO", Ui.Neutral, false, 11))
            {
                CedReplace(cedUndo[cedUndo.Count - 1]);
                cedUndo.RemoveAt(cedUndo.Count - 1);
                l = CedLayout();
            }
            GUI.enabled = was;
            if (DkKey(ur[1], "CLEAR", CrtStyle.Danger, false, 11))
            {
                CedPush();
                l.Walls = new int[0][];
                l.Sleepers = new int[0][];
                l.Traps = new int[0][];
                l.Loot = new int[0][];
            }
            if (DkKey(tc.Next(26f), "REVERT", Ui.Neutral, false, 11))
            {
                CedPush();
                CedReplace(cedMine >= 0 ? new CellLayout { Name = "", Walls = new int[0][], Sleepers = new int[0][], Traps = new int[0][], Loot = new int[0][], Pits = new int[0][] } : CellLayoutEdits.Clone(CellLayouts.All[cedIndex]));
                cedNote = "Back to the last save.";
            }
            if (DkKey(tc.Next(26f), "BACK TO CODE", Ui.Neutral, false, 11))
            {
                CedPush();
                CedReplace(CellLayoutEdits.Original(cedIndex));
                cedNote = "The code's layout (SAVE to keep it).";
            }
            var nb = DkStack.Cells(tc.Next(26f), 2, 4f);
            if (DkKey(nb[0], "NEW BLANK", CrtStyle.Head, false, 10))
            {
                CellTemplateRequests.Blank();
                cedMine = CellTemplateRequests.Mine.Count - 1;
                cedSel = -1;
                cedUndo.Clear();
                l = CedLayout();
                cedNote = "A blank template. SAVE MINE keeps it for me to add.";
            }
            if (DkKey(nb[1], "COPY TO MINE", CrtStyle.Head, false, 10))
            {
                var c = CellLayoutEdits.Clone(l);
                var blank = CellTemplateRequests.Blank();
                c.Name = blank.Name;
                CellTemplateRequests.Mine[CellTemplateRequests.Mine.Count - 1] = c;
                cedMine = CellTemplateRequests.Mine.Count - 1;
                cedSel = -1;
                cedUndo.Clear();
                l = CedLayout();
                cedNote = "Copied into " + c.Name + ".";
            }
            var mb = DkStack.Cells(tc.Next(26f), 2, 4f);
            if (cedMine >= 0)
            {
                if (DkKey(mb[0], "DROP MINE", CrtStyle.Danger, false, 10))
                {
                    CellTemplateRequests.Mine.RemoveAt(cedMine);
                    cedMine = -1;
                    cedSel = -1;
                    l = CedLayout();
                }
            }
            else
            {
                string nm = CellLayouts.All[cedIndex].Name;
                bool marked = CellTemplateRequests.Deletes.Contains(nm);
                if (DkKey(mb[0], marked ? "UNMARK DELETE" : "MARK DELETE", CrtStyle.Danger, marked, 10))
                {
                    if (marked) CellTemplateRequests.Deletes.Remove(nm); else CellTemplateRequests.Deletes.Add(nm);
                }
            }
            if (DkKey(mb[1], "SAVE MINE", CrtStyle.Gold, false, 10))
            {
                try
                {
                    CellTemplateRequests.Save(CellReqPath);
                    cedNote = "Saved " + CellTemplateRequests.Mine.Count + " of your templates and " + CellTemplateRequests.Deletes.Count + " deletions to " + CellTemplateRequests.FileName + ".";
                }
                catch (Exception ex)
                {
                    cedNote = "Couldn't write the file: " + ex.Message;
                }
            }
            bool anyDirty = false;
            for (int i = 0; i < cedWork.Length; i++)
                anyDirty |= CedDirty(i);
            anyDirty |= cedTested;
            if (DkKey(tc.Next(30f), anyDirty ? "SAVE *" : "SAVE", CrtStyle.Gold, anyDirty, 14))
                CedSave();
            var tr = DkStack.Cells(tc.Next(30f), 2, 4f);
            if (DkKey(tr[0], "TURN " + cedRot * 90, Ui.Neutral, false, 10))
                cedRot = (cedRot + 1) & 3;
            if (DkKey(tr[1], "TEST", CrtStyle.Head, false, 12))
                CedTestCell();

            tc.Space(4f);
            var info = tc.Rest();
            var problems = CellLayoutEdits.Problems(l);
            string text = l.Walls.Length + " walls, " + l.Sleepers.Length + " sleepers, " + l.Traps.Length + " traps, " + l.Loot.Length + " loot";
            Ui.Label(new Rect(info.x, info.y, info.width, 42f), text, 10, Ui.Muted, TextAnchor.UpperLeft, false, true);
            float y = info.y + 44f;
            foreach (var p in problems)
            {
                Ui.Label(new Rect(info.x, y, info.width, 40f), "! " + p, 10, CrtStyle.Danger, TextAnchor.UpperLeft, false, true);
                y += 40f;
            }
            if (cedNote.Length > 0)
                Ui.Label(new Rect(info.x, Mathf.Max(y, info.yMax - 70f), info.width, 70f), cedNote, 10, CrtStyle.Head, TextAnchor.LowerLeft, false, true);
        }

        void DrawCedCanvas(Rect cell, CellLayout l)
        {
            float k = cell.width / CellLayouts.Cell;
            Func<float, float, float, float, Rect> At = (x, y, w, h) => new Rect(cell.x + x * k, cell.y + y * k, w * k, h * k);
            Func<float, float, float, Rect> Spot = (x, y, r) => At(x - r, y - r, r * 2f, r * 2f);

            Ui.Fill(cell, new Color(0.02f, 0.04f, 0.09f, 1f));
            for (int g = 50; g < CellLayouts.Cell; g += 50)
            {
                float a = g == 300 ? 0.14f : 0.05f;
                Ui.Fill(At(g, 0, 1f / k, CellLayouts.Cell), CrtStyle.Ink.WithAlpha(a));
                Ui.Fill(At(0, g, CellLayouts.Cell, 1f / k), CrtStyle.Ink.WithAlpha(a));
            }
            // keep-clear: the chest in the middle and the four door lanes
            Ui.Fill(At(260, 260, 80, 80), CrtStyle.Gold.WithAlpha(0.10f));
            Ui.Fill(At(250, 0, 100, 50), CrtStyle.Head.WithAlpha(0.10f));
            Ui.Fill(At(250, 550, 100, 50), CrtStyle.Head.WithAlpha(0.10f));
            Ui.Fill(At(0, 250, 50, 100), CrtStyle.Head.WithAlpha(0.10f));
            Ui.Fill(At(550, 250, 50, 100), CrtStyle.Head.WithAlpha(0.10f));
            Ui.Label(At(260, 285, 80, 30), "CHEST", 9, CrtStyle.Gold.WithAlpha(0.5f), TextAnchor.MiddleCenter);

            foreach (var w in l.Walls)
            {
                var r = At(w[0], w[1], w[2], w[3]);
                Ui.Fill(r, CrtStyle.Ink.WithAlpha(0.55f));
                CrtKit.Frame(r, 1f, CrtStyle.Ink);
            }
            foreach (var s in l.Sleepers)
                Ui.Dot(Spot(s[0], s[1], 11f), CrtStyle.Head);
            foreach (var t in l.Traps)
            {
                Ui.Dot(Spot(t[0], t[1], 9f), CrtStyle.Danger);
                if (t[2] == 1)
                    Ui.Dot(Spot(t[0], t[1], 14f), CrtStyle.Danger, true);
            }
            foreach (var p in l.Loot)
                Ui.Dot(Spot(p[0], p[1], 9f), CrtStyle.Gold);
            if (cedSel >= l.Walls.Length)
                cedSel = -1;
            if (cedSel >= 0)
            {
                var sw = l.Walls[cedSel];
                CrtKit.Frame(At(sw[0], sw[1], sw[2], sw[3]), 2f, CrtStyle.Gold);
                foreach (var cp in CedCorners(sw))
                {
                    var hr = Spot(cp.x, cp.y, 6f);
                    Ui.Fill(hr, CrtStyle.Gold);
                    CrtKit.Frame(hr, 1f, CrtStyle.Ink);
                }
            }
            CrtKit.Frame(cell, 2f, CrtStyle.Ink.WithAlpha(0.8f));

            // Mouse
            var e = Event.current;
            var m = e.mousePosition;
            bool over = cell.Contains(m);
            float lx = (m.x - cell.x) / k, ly = (m.y - cell.y) / k;
            int sx = CedSnapTo(lx), sy = CedSnapTo(ly), wx = CedWallSnapTo(lx), wy = CedWallSnapTo(ly);  // (spots, walls)

            if (e.type == EventType.MouseDown && over && GUI.enabled)
            {
                int corner = e.button == 0 && cedTool != CellTool.Erase && cedSel >= 0 ? CedCornerAt(l.Walls[cedSel], lx, ly) : -1;
                if (corner >= 0)
                {
                    CedPush();  // a corner of the selected wall: dragging it resizes the wall, the opposite corner stays
                    var cs = CedCorners(l.Walls[cedSel]);
                    cedCorner = corner;
                    cedAnchor = cs[3 - corner];
                }
                else if (e.button == 1 || (e.button == 0 && cedTool == CellTool.Erase))
                    CedErase(lx, ly);
                else if (e.button == 0 && CedSpotAt(l, lx, ly, out int ms, out int mi))
                {
                    CedPush();  // grab a spot: it follows the mouse until let go
                    cedMoveSet = ms;
                    cedMoveIdx = mi;
                }
                else if (e.button == 0 && cedTool == CellTool.Wall && !e.shift && CedWallAt(l, lx, ly) >= 0)
                {
                    CedPush();  // grab a wall (hold Shift to draw a new one over it)
                    cedWallIdx = CedWallAt(l, lx, ly);
                    cedSel = cedWallIdx;
                    cedWallGrab = new Vector2(wx, wy);
                    cedWallOrig = new Vector2(l.Walls[cedWallIdx][0], l.Walls[cedWallIdx][1]);
                }
                else if (e.button == 0)
                {
                    cedSel = -1;
                    if (cedTool == CellTool.Wall)
                    {
                        cedDragging = true;
                        cedFrom = cedTo = new Vector2(wx, wy);
                    }
                    else
                    {
                        CedPush();
                        var spot = new[] { sx, sy };
                        if (cedTool == CellTool.Sleeper) l.Sleepers = CedAdd(l.Sleepers, spot);
                        else if (cedTool == CellTool.Loot) l.Loot = CedAdd(l.Loot, spot);
                        else l.Traps = CedAdd(l.Traps, new[] { sx, sy, cedTool == CellTool.WallTrap ? 1 : 0 });
                    }
                }
                e.Use();
            }
            else if (cedMoveSet >= 0 && (e.type == EventType.MouseDrag || e.type == EventType.MouseUp))
            {
                var sp = (cedMoveSet == 0 ? l.Sleepers : cedMoveSet == 1 ? l.Traps : l.Loot)[cedMoveIdx];
                sp[0] = sx;
                sp[1] = sy;
                if (e.type == EventType.MouseUp)
                    cedMoveSet = -1;
                e.Use();
            }
            else if (cedCorner >= 0 && (e.type == EventType.MouseDrag || e.type == EventType.MouseUp))
            {
                l.Walls[cedSel] = CedWallOf(cedAnchor, new Vector2(wx, wy));
                if (e.type == EventType.MouseUp)
                    cedCorner = -1;
                e.Use();
            }
            else if (cedWallIdx >= 0 && (e.type == EventType.MouseDrag || e.type == EventType.MouseUp))
            {
                var wl = l.Walls[cedWallIdx];
                wl[0] = Mathf.Clamp((int)(cedWallOrig.x + wx - cedWallGrab.x), 0, CellLayouts.Cell - wl[2]);
                wl[1] = Mathf.Clamp((int)(cedWallOrig.y + wy - cedWallGrab.y), 0, CellLayouts.Cell - wl[3]);
                if (e.type == EventType.MouseUp)
                    cedWallIdx = -1;
                e.Use();
            }
            else if (cedDragging && (e.type == EventType.MouseDrag || e.type == EventType.MouseUp))
            {
                cedTo = new Vector2(wx, wy);
                if (e.type == EventType.MouseUp)
                {
                    cedDragging = false;
                    CedPush();
                    l.Walls = CedAdd(l.Walls, CedWallOf(cedFrom, cedTo));
                }
                e.Use();
            }

            if (cedDragging)
            {
                var w = CedWallOf(cedFrom, cedTo);
                var r = At(w[0], w[1], w[2], w[3]);
                Ui.Fill(r, CrtStyle.Gold.WithAlpha(0.35f));
                CrtKit.Frame(r, 1f, CrtStyle.Gold);
                Ui.Label(new Rect(r.x, r.y - 16f, 120f, 14f), w[2] + " x " + w[3], 10, CrtStyle.Gold, TextAnchor.LowerLeft, true);
            }
            else if (over && cedTool != CellTool.Wall && cedTool != CellTool.Erase)
                Ui.Dot(Spot(sx, sy, 9f), (cedTool == CellTool.Sleeper ? CrtStyle.Head : cedTool == CellTool.Loot ? CrtStyle.Gold : CrtStyle.Danger).WithAlpha(0.5f));
            if (over)
                Ui.Label(new Rect(cell.x + 6f, cell.yMax - 20f, 140f, 16f), sx + ", " + sy, 10, Ui.Muted);
        }
    }
}
