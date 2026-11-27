using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Arena
{
    /// The Cell Editor's CORE tab: the ten floor plans of the Grid run's 4 x 3 cell core (`CoreLayouts`). A plan is one quarter (or one half, for the turned ones) that the game mirrors
    /// to the rest, so you edit that part (bright) and see the mirrored copies (dim) follow. Drag walls, click pillars and crates in, drag to move, right click removes;
    /// SAVE writes CoreLayoutEdits.txt (`CoreLayoutEdits`; fold.py puts it in the code); TEST puts the plan down in your run.
    public partial class ArenaApp
    {
        enum CoreTool { Wall, Pillar, Crate, Heavy, Erase }

        static readonly string[] CoreToolNames = { "WALL", "PILLAR", "CRATE", "HEAVY CRATE", "ERASE" };
        const int CoreSnap = 50, CoreMinWall = 50;  // the floor grid (RoomTraps.Tile): wall corners on its lines, a wall at least a square thick

        int coreIndex;
        CoreTool coreTool = CoreTool.Wall;
        readonly int[][][] coreWork = new int[10][][];
        readonly List<int[][]> coreUndo = new List<int[][]>();
        bool coreDragging, coreTested;
        Vector2 coreFrom, coreTo, coreGrab, coreOrig;
        int coreMoveIdx = -1;
        string coreNote = "";

        static string CoreEditPath => Application.isEditor
            ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", CoreLayoutEdits.FileName))
            : Path.Combine(Application.persistentDataPath, CoreLayoutEdits.FileName);

        void LoadCoreLayoutEdits()
        {
            try { CoreLayoutEdits.Load(CoreEditPath); }
            catch (Exception e) { Debug.Log("Couldn't read " + CoreEditPath + ": " + e.Message); }
        }

        int[][] CorePieces() => coreWork[coreIndex] ?? (coreWork[coreIndex] = CoreLayoutEdits.CopyOf(CoreLayouts.All[coreIndex].Pieces));

        bool CoreDirty(int i) => coreWork[i] != null && CoreLayoutEdits.Line("", coreWork[i]) != CoreLayoutEdits.Line("", CoreLayouts.All[i].Pieces);

        void CorePush()
        {
            coreUndo.Add(CoreLayoutEdits.CopyOf(CorePieces()));
            if (coreUndo.Count > 60)
                coreUndo.RemoveAt(0);
        }

        void CoreApplyAll()
        {
            for (int i = 0; i < coreWork.Length; i++)
                if (CoreDirty(i))
                {
                    CoreLayoutEdits.Set(i, coreWork[i]);
                    coreTested = true;
                }
        }

        void CoreSave()
        {
            CoreApplyAll();
            try
            {
                CoreLayoutEdits.Save(CoreEditPath);
                coreNote = "Saved to " + CoreLayoutEdits.FileName + ".";
                coreTested = false;
            }
            catch (Exception e)
            {
                coreNote = "Couldn't write the file: " + e.Message;
            }
        }

        /// The area of the core a plan is edited in: its quarter, or its left half when turned (core frame).
        static Rect CoreEditArea(CoreLayout l) => new Rect(0f, 0f, CoreLayouts.W / 2f, l.Turned ? CoreLayouts.H : CoreLayouts.H / 2f);

        static float[] CoreBox(int[] p)
        {
            if (p[0] == 0) return new float[] { p[1], p[2], p[3], p[4] };
            float r = p[0] == 1 ? p[3] : p[0] == 3 ? 23f : 18f;
            return new[] { p[1] - r, p[2] - r, r * 2f, r * 2f };
        }

        /// The piece under a core-frame point (crates, pillars, then the newest wall), or -1.
        static int CorePieceAt(int[][] pieces, float x, float y)
        {
            for (int pass = 0; pass < 2; pass++)
                for (int i = pieces.Length - 1; i >= 0; i--)
                {
                    bool wall = pieces[i][0] == 0;
                    if (wall == (pass == 0))
                        continue;
                    var b = CoreBox(pieces[i]);
                    if (x >= b[0] - 3 && x <= b[0] + b[2] + 3 && y >= b[1] - 3 && y <= b[1] + b[3] + 3)
                        return i;
                }
            return -1;
        }

        static int[] CoreWallOf(Vector2 a, Vector2 b, Rect area)
        {
            int w = Mathf.Max(CoreMinWall, (int)Mathf.Abs(a.x - b.x)), h = Mathf.Max(CoreMinWall, (int)Mathf.Abs(a.y - b.y));
            int x = (int)Mathf.Min(Mathf.Min(a.x, b.x), area.xMax - w), y = (int)Mathf.Min(Mathf.Min(a.y, b.y), area.yMax - h);
            return new[] { 0, x, y, w, h };
        }

        void DrawCedCore(Rect body)
        {
            var layout = CoreLayouts.All[coreIndex];
            var pieces = CorePieces();

            // the ten plans, two rows of five
            var rows = DkStack.Column(new Rect(body.x, body.y, body.width, 54f), 4f);
            for (int r = 0; r < 2; r++)
            {
                var row = DkStack.Cells(rows.Next(25f), 5, 4f);
                for (int c = 0; c < 5; c++)
                {
                    int i = r * 5 + c;
                    bool edited = CoreDirty(i) || CoreLayoutEdits.IsEdited(i);
                    if (DkChoice(row[c], (edited ? "* " : "") + CoreLayouts.All[i].Name, i == coreIndex, edited ? CrtStyle.Gold : Ui.Primary) && i != coreIndex)
                    {
                        coreIndex = i;
                        coreUndo.Clear();
                        coreDragging = false;
                        coreMoveIdx = -1;
                        layout = CoreLayouts.All[i];
                        pieces = CorePieces();
                    }
                }
            }

            const float k = 0.245f;
            var cv = new Rect(body.x, body.y + 62f, CoreLayouts.W * k, CoreLayouts.H * k);
            DrawCoreCanvas(cv, k, layout, pieces);

            var tools = new Rect(cv.xMax + 16f, body.y + 62f, body.xMax - cv.xMax - 16f, body.height - 62f);
            var tc = DkStack.Column(tools, 4f);
            Ui.Heading(tools.x, tc.Next(20f).y, tools.width, "Tool");
            var tcols = DkStack.Cells(tc.Next(26f * 3 + 8f), 2, 6f);
            for (int i = 0; i < CoreToolNames.Length; i++)
            {
                var cell = tcols[i % 2];
                if (DkChoice(new Rect(cell.x, cell.y + (i / 2) * 30f, cell.width, 26f), CoreToolNames[i], (int)coreTool == i, CrtStyle.Gold))
                    coreTool = (CoreTool)i;
            }
            tc.Space(4f);
            var ur = DkStack.Cells(tc.Next(26f), 2, 4f);
            bool was = GUI.enabled;
            GUI.enabled = was && coreUndo.Count > 0;
            if (DkKey(ur[0], "UNDO", Ui.Neutral, false, 11))
            {
                coreWork[coreIndex] = coreUndo[coreUndo.Count - 1];
                coreUndo.RemoveAt(coreUndo.Count - 1);
                pieces = CorePieces();
            }
            GUI.enabled = was;
            if (DkKey(ur[1], "CLEAR", CrtStyle.Danger, false, 11))
            {
                CorePush();
                coreWork[coreIndex] = new int[0][];
            }
            var rr = DkStack.Cells(tc.Next(26f), 2, 4f);
            if (DkKey(rr[0], "REVERT", Ui.Neutral, false, 11))
            {
                CorePush();
                coreWork[coreIndex] = CoreLayoutEdits.CopyOf(layout.Pieces);
                coreNote = "Back to the last save.";
            }
            if (DkKey(rr[1], "BACK TO CODE", Ui.Neutral, false, 10))
            {
                CorePush();
                coreWork[coreIndex] = CoreLayoutEdits.Original(coreIndex);
                coreNote = "The code's plan (SAVE to keep it).";
            }
            bool dirty = coreTested;
            for (int i = 0; i < coreWork.Length; i++)
                dirty |= CoreDirty(i);
            var sr = DkStack.Cells(tc.Next(30f), 2, 6f);
            if (DkKey(sr[0], dirty ? "SAVE *" : "SAVE", CrtStyle.Gold, dirty, 14))
                CoreSave();
            if (DkKey(sr[1], "TEST", CrtStyle.Head, false, 13))
            {
                CoreApplyAll();
                string why = game.DevTestCore((CoreLayoutId)coreIndex);
                if (why.Length > 0)
                    coreNote = why;
                else
                    cellEditorOpen = false;
            }
            tc.Space(4f);
            var info = tc.Rest();
            var problems = CoreLayoutEdits.Problems(layout, pieces);
            float y = info.y;
            Ui.Label(new Rect(info.x, y, info.width, 28f), pieces.Length + " pieces" + (layout.Turned ? " (the left half, turned 180° for the right)" : " (one quarter, mirrored four ways)"), 10, Ui.Muted, TextAnchor.UpperLeft, false, true);
            y += 30f;
            foreach (var p in problems)
            {
                Ui.Label(new Rect(info.x, y, info.width, 16f), "! " + p, 10, CrtStyle.Danger, TextAnchor.UpperLeft, false, true);
                y += 16f;
            }
            if (coreNote.Length > 0)
                Ui.Label(new Rect(info.x, y + 4f, info.width, 40f), coreNote, 10, CrtStyle.Head, TextAnchor.UpperLeft, false, true);
        }

        void DrawCoreCanvas(Rect cv, float k, CoreLayout layout, int[][] pieces)
        {
            Func<float, float, float, float, Rect> At = (x, y, w, h) => new Rect(cv.x + x * k, cv.y + y * k, w * k, h * k);
            Ui.Fill(cv, new Color(0.02f, 0.04f, 0.09f, 1f));
            for (int g = 300; g < CoreLayouts.W; g += 300)
                Ui.Fill(At(g, 0, 1f / k, CoreLayouts.H), CrtStyle.Ink.WithAlpha(g == 1200 ? 0.16f : 0.05f));
            for (int g = 300; g < CoreLayouts.H; g += 300)
                Ui.Fill(At(0, g, CoreLayouts.W, 1f / k), CrtStyle.Ink.WithAlpha(g == 900 ? 0.16f : 0.05f));
            var area = CoreEditArea(layout);
            // keep-clear: the door lanes and the gate's floor
            foreach (var d in CoreLayoutEdits.DoorLanes())
                Ui.Fill(At(d[0], d[1], d[2] - d[0], d[3] - d[1]), CrtStyle.Head.WithAlpha(0.10f));
            Ui.Dot(At(CoreLayouts.W / 2f - CoreLayouts.GateClear, CoreLayouts.H / 2f - CoreLayouts.GateClear, CoreLayouts.GateClear * 2f, CoreLayouts.GateClear * 2f), CrtStyle.Gold.WithAlpha(0.10f));

            // the whole plan (mirrors included), the edited part bright over it
            var saved = layout.Pieces;
            layout.Pieces = pieces;
            var all = CoreLayouts.Expanded(layout.Id);
            layout.Pieces = saved;
            foreach (var p in all)
                CoreDrawPiece(At, p, 0.3f);
            foreach (var p in pieces)
                CoreDrawPiece(At, p, 1f);
            Ui.Fill(At(area.x, area.yMax, area.width, 1f / k), CrtStyle.Gold.WithAlpha(0.35f));
            Ui.Fill(At(area.xMax, area.y, 1f / k, area.height), CrtStyle.Gold.WithAlpha(0.35f));
            CrtKit.Frame(cv, 2f, CrtStyle.Ink.WithAlpha(0.8f));

            var e = Event.current;
            var m = e.mousePosition;
            float lx = (m.x - cv.x) / k, ly = (m.y - cv.y) / k;
            bool over = cv.Contains(m) && area.Contains(new Vector2(lx, ly));
            bool crate = coreTool == CoreTool.Crate || coreTool == CoreTool.Heavy;
            bool middle = crate || coreTool == CoreTool.Pillar;  // (crates and pillars sit on a square's middle, walls' corners on lines)
            float sx = middle ? Mathf.Floor(lx / CoreSnap) * CoreSnap + CoreSnap / 2 : Mathf.Round(lx / CoreSnap) * CoreSnap;
            float sy = middle ? Mathf.Floor(ly / CoreSnap) * CoreSnap + CoreSnap / 2 : Mathf.Round(ly / CoreSnap) * CoreSnap;
            sx = Mathf.Clamp(sx, area.x, area.xMax); sy = Mathf.Clamp(sy, area.y, area.yMax);

            if (e.type == EventType.MouseDown && over && GUI.enabled)
            {
                int hit = CorePieceAt(pieces, lx, ly);
                if (e.button == 1 || (e.button == 0 && coreTool == CoreTool.Erase))
                {
                    if (hit >= 0)
                    {
                        CorePush();
                        var l = new List<int[]>(pieces);
                        l.RemoveAt(hit);
                        coreWork[coreIndex] = l.ToArray();
                    }
                }
                else if (e.button == 0)
                {
                    if (hit >= 0 && (pieces[hit][0] != 0 || (coreTool == CoreTool.Wall && !e.shift)))
                    {
                        CorePush();  // grab it (a wall with the wall tool; Shift draws a new one over it)
                        coreMoveIdx = hit;
                        coreGrab = new Vector2(sx, sy);
                        coreOrig = new Vector2(pieces[hit][1], pieces[hit][2]);
                    }
                    else if (coreTool == CoreTool.Wall)
                    {
                        coreDragging = true;
                        coreFrom = coreTo = new Vector2(sx, sy);
                    }
                    else
                    {
                        CorePush();
                        int[] np = coreTool == CoreTool.Pillar ? new[] { 1, (int)sx, (int)sy, CoreSnap / 2 } : new[] { coreTool == CoreTool.Heavy ? 3 : 2, (int)sx, (int)sy };
                        var l = new List<int[]>(pieces) { np };
                        coreWork[coreIndex] = l.ToArray();
                    }
                }
                e.Use();
            }
            else if (coreMoveIdx >= 0 && (e.type == EventType.MouseDrag || e.type == EventType.MouseUp))
            {
                var p = pieces[coreMoveIdx];
                p[1] = (int)(coreOrig.x + sx - coreGrab.x);
                p[2] = (int)(coreOrig.y + sy - coreGrab.y);
                if (p[0] == 0)
                {
                    p[1] = (int)Mathf.Clamp(p[1], area.x, area.xMax - p[3]);
                    p[2] = (int)Mathf.Clamp(p[2], area.y, area.yMax - p[4]);
                }
                if (e.type == EventType.MouseUp)
                    coreMoveIdx = -1;
                e.Use();
            }
            else if (coreDragging && (e.type == EventType.MouseDrag || e.type == EventType.MouseUp))
            {
                coreTo = new Vector2(sx, sy);
                if (e.type == EventType.MouseUp)
                {
                    coreDragging = false;
                    CorePush();
                    var l = new List<int[]>(pieces) { CoreWallOf(coreFrom, coreTo, area) };
                    coreWork[coreIndex] = l.ToArray();
                }
                e.Use();
            }

            if (coreDragging)
            {
                var w = CoreWallOf(coreFrom, coreTo, area);
                var r = At(w[1], w[2], w[3], w[4]);
                Ui.Fill(r, CrtStyle.Gold.WithAlpha(0.35f));
                CrtKit.Frame(r, 1f, CrtStyle.Gold);
                Ui.Label(new Rect(r.x, r.y - 16f, 120f, 14f), w[3] + " x " + w[4], 10, CrtStyle.Gold, TextAnchor.LowerLeft, true);
            }
            else if (over && coreTool != CoreTool.Wall && coreTool != CoreTool.Erase)
            {
                float rad = coreTool == CoreTool.Pillar ? 32f : coreTool == CoreTool.Heavy ? 23f : 18f;
                Ui.Fill(At(sx - rad, sy - rad, rad * 2f, rad * 2f), CrtStyle.Gold.WithAlpha(0.3f));
            }
            if (over)
                Ui.Label(new Rect(cv.x + 6f, cv.yMax - 20f, 140f, 16f), sx + ", " + sy, 10, Ui.Muted);
        }

        static void CoreDrawPiece(Func<float, float, float, float, Rect> at, int[] p, float strength)
        {
            var b = CoreBox(p);
            var r = at(b[0], b[1], b[2], b[3]);
            if (p[0] <= 1)
            {
                if (p[0] == 1)
                    Ui.Dot(r, CrtStyle.Ink.WithAlpha(0.7f * strength));
                else
                {
                    Ui.Fill(r, CrtStyle.Ink.WithAlpha(0.6f * strength));
                    if (strength >= 1f)
                        CrtKit.Frame(r, 1f, CrtStyle.Ink);
                }
            }
            else
            {
                Ui.Fill(r, CrtStyle.Gold.WithAlpha((p[0] == 3 ? 0.55f : 0.25f) * strength));
                if (strength >= 1f)
                    CrtKit.Frame(r, 1f, CrtStyle.Gold);
            }
        }
    }
}
