using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Arena
{
    /// The Cell Editor's HUB tab: the six hub floor plans (`Layouts`, 1600 x 900: walls, pillars and crates; the edge wall and its gates are drawn but fixed).
    /// Drag out walls, click crates in, drag either to move, right click to remove; SAVE writes HubLayoutEdits.txt (`HubLayoutEdits`; fold.py puts it in the code);
    /// TEST puts the plan down in your run and stands you in the hub.
    public partial class ArenaApp
    {
        enum HubTool { Wall, Crate, Heavy, Erase }

        static readonly string[] HubToolNames = { "WALL", "CRATE", "HEAVY CRATE", "ERASE" };
        const int HubSnap = 50, HubMinWall = 50;  // the floor grid (RoomTraps.Tile): wall corners on its lines, a wall at least a square thick
        const float HubCrate = 40f;

        LayoutId hubId = LayoutId.PillarRing;
        HubTool hubTool = HubTool.Wall;
        readonly HubLayoutData[] hubWork = new HubLayoutData[7];
        readonly List<HubLayoutData> hubUndo = new List<HubLayoutData>();
        bool hubDragging, hubTested;
        Vector2 hubFrom, hubTo, hubGrab, hubOrig;
        int hubMoveKind = -1, hubMoveIdx;  // a part being dragged: 0 box, 1 crate, 2 pillar
        string hubNote = "";

        static string HubEditPath => Application.isEditor
            ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", HubLayoutEdits.FileName))
            : Path.Combine(Application.persistentDataPath, HubLayoutEdits.FileName);

        void LoadHubLayoutEdits()
        {
            try { HubLayoutEdits.Load(HubEditPath); }
            catch (Exception e) { Debug.Log("Couldn't read " + HubEditPath + ": " + e.Message); }
        }

        HubLayoutData HubData()
        {
            int i = (int)hubId;
            return hubWork[i] ?? (hubWork[i] = HubLayoutEdits.Current(hubId));
        }

        bool HubDirty(LayoutId id)
        {
            var w = hubWork[(int)id];
            return w != null && HubLayoutEdits.Line(w) != HubLayoutEdits.Line(HubLayoutEdits.Current(id));
        }

        void HubPush()
        {
            hubUndo.Add(HubLayoutEdits.Clone(HubData()));
            if (hubUndo.Count > 60)
                hubUndo.RemoveAt(0);
        }

        /// Put every hub working copy into the game (not the file).
        void HubApplyAll()
        {
            foreach (var id in HubLayoutEdits.Ids())
                if (HubDirty(id))
                {
                    HubLayoutEdits.Set(id, hubWork[(int)id]);
                    hubTested = true;
                }
        }

        void HubSave()
        {
            HubApplyAll();
            try
            {
                HubLayoutEdits.Save(HubEditPath);
                hubNote = "Saved to " + HubLayoutEdits.FileName + ".";
                hubTested = false;
            }
            catch (Exception e)
            {
                hubNote = "Couldn't write the file: " + e.Message;
            }
        }

        static T[] HubAdd<T>(T[] a, T item)
        {
            Array.Resize(ref a, a.Length + 1);
            a[a.Length - 1] = item;
            return a;
        }

        static T[] HubRemove<T>(T[] a, int i)
        {
            var r = new List<T>(a);
            r.RemoveAt(i);
            return r.ToArray();
        }

        static float HubSnapTo(float v, float max) => Mathf.Clamp(Mathf.Round(v / HubSnap) * HubSnap, 0f, max);
        static float HubMiddleOf(float v, float max) => Mathf.Clamp(Mathf.Floor(v / HubSnap) * HubSnap + HubSnap / 2, HubSnap / 2, max - HubSnap / 2);  // a square's middle (crates)

        /// The part under a hub-local point: crates first, then pillars, then the newest box; kind 0 box, 1 crate, 2 pillar; false if none.
        static bool HubPartAt(HubLayoutData d, float x, float y, out int kind, out int idx)
        {
            for (idx = d.Crates.Length - 1; idx >= 0; idx--)
                if (Mathf.Abs(d.Crates[idx][0] - x) <= HubCrate / 2f && Mathf.Abs(d.Crates[idx][1] - y) <= HubCrate / 2f)
                {
                    kind = 1;
                    return true;
                }
            for (idx = d.Pillars.Length - 1; idx >= 0; idx--)
            {
                float dx = d.Pillars[idx][0] - x, dy = d.Pillars[idx][1] - y;
                if (dx * dx + dy * dy <= d.Pillars[idx][2] * d.Pillars[idx][2])
                {
                    kind = 2;
                    return true;
                }
            }
            for (idx = d.Boxes.Length - 1; idx >= 0; idx--)
            {
                var b = d.Boxes[idx];
                if (x >= b[0] && x <= b[0] + b[2] && y >= b[1] && y <= b[1] + b[3])
                {
                    kind = 0;
                    return true;
                }
            }
            kind = -1;
            return false;
        }

        static float[] HubWallOf(Vector2 a, Vector2 b)
        {
            float w = Mathf.Max(HubMinWall, Mathf.Abs(a.x - b.x)), h = Mathf.Max(HubMinWall, Mathf.Abs(a.y - b.y));
            float x = Mathf.Min(Mathf.Min(a.x, b.x), Game.Width - w), y = Mathf.Min(Mathf.Min(a.y, b.y), Game.Height - h);
            return new[] { x, y, w, h };
        }

        float[] HubPartPos(HubLayoutData d, int kind, int idx) => kind == 0 ? d.Boxes[idx] : kind == 1 ? d.Crates[idx] : d.Pillars[idx];

        void DrawCedHub(Rect body)
        {
            var d = HubData();
            var ids = new List<LayoutId>(HubLayoutEdits.Ids());
            var row = DkStack.Cells(new Rect(body.x, body.y, body.width, 26f), ids.Count, 4f);
            for (int i = 0; i < ids.Count; i++)
            {
                bool edited = HubDirty(ids[i]) || HubLayoutEdits.IsEdited(ids[i]);
                if (DkChoice(row[i], (edited ? "* " : "") + Layouts.Get(ids[i]).Name, ids[i] == hubId, edited ? CrtStyle.Gold : Ui.Primary) && ids[i] != hubId)
                {
                    hubId = ids[i];
                    hubUndo.Clear();
                    hubDragging = false;
                    hubMoveKind = -1;
                    d = HubData();
                }
            }

            const float k = 0.45f;
            var canvas = new Rect(body.x, body.y + 34f, Game.Width * k, Game.Height * k);
            DrawHubCanvas(canvas, k, d);

            var tools = new Rect(canvas.xMax + 16f, body.y + 34f, body.xMax - canvas.xMax - 16f, body.height - 34f);
            var tc = DkStack.Column(tools, 4f);
            Ui.Heading(tools.x, tc.Next(20f).y, tools.width, "Tool");
            for (int i = 0; i < HubToolNames.Length; i++)
                if (DkChoice(tc.Next(26f), HubToolNames[i], (int)hubTool == i, CrtStyle.Gold))
                    hubTool = (HubTool)i;
            tc.Space(6f);
            var ur = DkStack.Cells(tc.Next(26f), 2, 4f);
            bool was = GUI.enabled;
            GUI.enabled = was && hubUndo.Count > 0;
            if (DkKey(ur[0], "UNDO", Ui.Neutral, false, 11))
            {
                hubWork[(int)hubId] = hubUndo[hubUndo.Count - 1];
                hubUndo.RemoveAt(hubUndo.Count - 1);
                d = HubData();
            }
            GUI.enabled = was;
            if (DkKey(ur[1], "CLEAR", CrtStyle.Danger, false, 11))
            {
                HubPush();
                d.Boxes = new float[0][];
                d.Pillars = new float[0][];
                d.Crates = new float[0][];
            }
            if (DkKey(tc.Next(26f), "REVERT", Ui.Neutral, false, 11))
            {
                HubPush();
                hubWork[(int)hubId] = HubLayoutEdits.Current(hubId);
                hubNote = "Back to the last save.";
            }
            if (DkKey(tc.Next(26f), "BACK TO CODE", Ui.Neutral, false, 11))
            {
                HubPush();
                hubWork[(int)hubId] = HubLayoutEdits.Original(hubId);
                hubNote = "The code's plan (SAVE to keep it).";
            }
            bool dirty = hubTested;
            foreach (var id in ids)
                dirty |= HubDirty(id);
            if (DkKey(tc.Next(30f), dirty ? "SAVE *" : "SAVE", CrtStyle.Gold, dirty, 14))
                HubSave();
            if (DkKey(tc.Next(30f), "TEST LAYOUT", CrtStyle.Head, false, 12))
            {
                HubApplyAll();
                string why = game.DevTestHub(hubId);
                if (why.Length > 0)
                    hubNote = why;
                else
                    cellEditorOpen = false;
            }
            tc.Space(4f);
            var info = tc.Rest();
            Ui.Label(info, d.Boxes.Length + d.Pillars.Length + " walls, " + d.Crates.Length + " crates\n" + hubNote, 10, hubNote.Length > 0 ? CrtStyle.Head : Ui.Muted, TextAnchor.UpperLeft, false, true);
            Ui.Label(new Rect(body.x, canvas.yMax + 6f, canvas.width, 32f),
                Layouts.Get(hubId).Blurb + "  Everything snaps to the 50 px floor grid; walls are a square thick or more. The grey edge wall and its gates are fixed.", 10, Ui.Muted, TextAnchor.UpperLeft, false, true);
        }

        void DrawHubCanvas(Rect cv, float k, HubLayoutData d)
        {
            Func<float, float, float, float, Rect> At = (x, y, w, h) => new Rect(cv.x + x * k, cv.y + y * k, w * k, h * k);
            Ui.Fill(cv, new Color(0.02f, 0.04f, 0.09f, 1f));
            for (int g = 100; g < Game.Width; g += 100)
                Ui.Fill(At(g, 0, 1f / k, Game.Height), CrtStyle.Ink.WithAlpha(g == 800 ? 0.14f : 0.05f));
            for (int g = 100; g < Game.Height; g += 100)
                Ui.Fill(At(0, g, Game.Width, 1f / k), CrtStyle.Ink.WithAlpha(g == 400 || g == 500 ? 0.09f : 0.05f));

            var def = Layouts.Get(hubId);
            foreach (var s in def.Solids)
                if (s.Edge)
                    Ui.Fill(At(s.Pos.X - s.HW, s.Pos.Y - s.HH, s.HW * 2f, s.HH * 2f), CrtStyle.Ink.WithAlpha(0.28f));
            foreach (var sp in def.PlayerGates)
                Ui.Fill(HubSpan(At, sp), CrtStyle.Head.WithAlpha(0.7f));
            foreach (var sp in def.EnemyGates)
                Ui.Fill(HubSpan(At, sp), CrtStyle.Danger.WithAlpha(0.7f));
            if (def.HasHold)
                Ui.Dot(At(def.HoldPos.X - def.HoldR, def.HoldPos.Y - def.HoldR, def.HoldR * 2f, def.HoldR * 2f), CrtStyle.Gold.WithAlpha(0.6f), true);

            foreach (var b in d.Boxes)
            {
                var r = At(b[0], b[1], b[2], b[3]);
                Ui.Fill(r, CrtStyle.Ink.WithAlpha(0.55f));
                CrtKit.Frame(r, 1f, CrtStyle.Ink);
            }
            foreach (var p in d.Pillars)
                Ui.Dot(At(p[0] - p[2], p[1] - p[2], p[2] * 2f, p[2] * 2f), CrtStyle.Ink.WithAlpha(0.7f));
            foreach (var c in d.Crates)
            {
                var r = At(c[0] - HubCrate / 2f, c[1] - HubCrate / 2f, HubCrate, HubCrate);
                Ui.Fill(r, c[2] > 0.5f ? CrtStyle.Gold.WithAlpha(0.55f) : CrtStyle.Gold.WithAlpha(0.22f));
                CrtKit.Frame(r, 1f, CrtStyle.Gold);
            }
            CrtKit.Frame(cv, 2f, CrtStyle.Ink.WithAlpha(0.8f));

            var e = Event.current;
            var m = e.mousePosition;
            bool over = cv.Contains(m);
            float lx = (m.x - cv.x) / k, ly = (m.y - cv.y) / k;
            float sx = HubSnapTo(lx, Game.Width), sy = HubSnapTo(ly, Game.Height);

            if (e.type == EventType.MouseDown && over && GUI.enabled)
            {
                int kind, idx;
                if (e.button == 1 || (e.button == 0 && hubTool == HubTool.Erase))
                {
                    if (HubPartAt(d, lx, ly, out kind, out idx))
                    {
                        HubPush();
                        if (kind == 0) d.Boxes = HubRemove(d.Boxes, idx);
                        else if (kind == 1) d.Crates = HubRemove(d.Crates, idx);
                        else d.Pillars = HubRemove(d.Pillars, idx);
                    }
                }
                else if (e.button == 0)
                {
                    bool hit = HubPartAt(d, lx, ly, out kind, out idx);
                    if (hit && (kind != 0 || (hubTool == HubTool.Wall && !e.shift)))
                    {
                        HubPush();  // grab it: crates and pillars with any tool, a wall with the wall tool (Shift draws a new one over it)
                        hubMoveKind = kind;
                        hubMoveIdx = idx;
                        hubGrab = new Vector2(sx, sy);
                        var pos = HubPartPos(d, kind, idx);
                        hubOrig = new Vector2(pos[0], pos[1]);
                    }
                    else if (hubTool == HubTool.Wall)
                    {
                        hubDragging = true;
                        hubFrom = hubTo = new Vector2(sx, sy);
                    }
                    else
                    {
                        HubPush();
                        d.Crates = HubAdd(d.Crates, new[] { HubMiddleOf(lx, Game.Width), HubMiddleOf(ly, Game.Height), hubTool == HubTool.Heavy ? 1f : 0f });
                    }
                }
                e.Use();
            }
            else if (hubMoveKind >= 0 && (e.type == EventType.MouseDrag || e.type == EventType.MouseUp))
            {
                var pos = HubPartPos(d, hubMoveKind, hubMoveIdx);
                pos[0] = hubOrig.x + sx - hubGrab.x;
                pos[1] = hubOrig.y + sy - hubGrab.y;
                if (hubMoveKind == 0)
                {
                    pos[0] = Mathf.Clamp(pos[0], 0f, Game.Width - pos[2]);
                    pos[1] = Mathf.Clamp(pos[1], 0f, Game.Height - pos[3]);
                }
                if (e.type == EventType.MouseUp)
                    hubMoveKind = -1;
                e.Use();
            }
            else if (hubDragging && (e.type == EventType.MouseDrag || e.type == EventType.MouseUp))
            {
                hubTo = new Vector2(sx, sy);
                if (e.type == EventType.MouseUp)
                {
                    hubDragging = false;
                    HubPush();
                    d.Boxes = HubAdd(d.Boxes, HubWallOf(hubFrom, hubTo));
                }
                e.Use();
            }

            if (hubDragging)
            {
                var w = HubWallOf(hubFrom, hubTo);
                var r = At(w[0], w[1], w[2], w[3]);
                Ui.Fill(r, CrtStyle.Gold.WithAlpha(0.35f));
                CrtKit.Frame(r, 1f, CrtStyle.Gold);
                Ui.Label(new Rect(r.x, r.y - 16f, 120f, 14f), w[2] + " x " + w[3], 10, CrtStyle.Gold, TextAnchor.LowerLeft, true);
            }
            else if (over && (hubTool == HubTool.Crate || hubTool == HubTool.Heavy))
                Ui.Fill(At(sx - HubCrate / 2f, sy - HubCrate / 2f, HubCrate, HubCrate), CrtStyle.Gold.WithAlpha(0.3f));
            if (over)
                Ui.Label(new Rect(cv.x + 6f, cv.yMax - 20f, 140f, 16f), sx + ", " + sy, 10, Ui.Muted);
        }

        /// An edge gap as a strip along its side of the hub.
        static Rect HubSpan(Func<float, float, float, float, Rect> at, EdgeSpan s)
        {
            switch (s.Side)
            {
                case 0: return at(s.From, 0, s.To - s.From, Layouts.EdgeT);
                case 1: return at(s.From, Game.Height - Layouts.EdgeT, s.To - s.From, Layouts.EdgeT);
                case 2: return at(0, s.From, Layouts.EdgeT, s.To - s.From);
                default: return at(Game.Width - Layouts.EdgeT, s.From, Layouts.EdgeT, s.To - s.From);
            }
        }
    }
}
