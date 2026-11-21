using System;
using System.Linq;
using Arena;

static partial class Program
{
    // The Cell Editor's hub edits and TEST LAYOUT (Game.DevTestCells / DevTestHub, HubLayoutEdits.cs).
    static void LayoutTestTest(float dt)
    {
        // a hub plan edit round-trips, changes the plan's walls and reverts
        var before = Layouts.Get(LayoutId.Cross);
        var edit = HubLayoutEdits.Original(LayoutId.Cross);
        edit.Boxes = edit.Boxes.Concat(new[] { new[] { 100f, 100f, 60f, 24f } }).ToArray();
        edit.Crates = new float[0][];
        HubLayoutEdits.Set(LayoutId.Cross, edit);
        string file = HubLayoutEdits.Serialize();
        bool set = HubLayoutEdits.IsEdited(LayoutId.Cross) && Layouts.Get(LayoutId.Cross).Crates.Length == 0 && Layouts.Get(LayoutId.Cross).Solids.Length == before.Solids.Length + 1
            && Layouts.Get(LayoutId.Cross).Waypoints.Length > before.Waypoints.Length && file.Contains("Cross\t");
        HubLayoutEdits.Revert(LayoutId.Cross);
        bool back = !HubLayoutEdits.IsEdited(LayoutId.Cross) && Layouts.Get(LayoutId.Cross).Crates.Length == before.Crates.Length && HubLayoutEdits.Serialize().IndexOf('\t') < 0;
        Check(set && back && HubLayoutEdits.Parse(file) == 1 && Layouts.Get(LayoutId.Cross).Crates.Length == 0, "a hub plan's edit changes its walls, crates and corners, is saved as one line, read back and reverts");
        HubLayoutEdits.Revert(LayoutId.Cross);

        // TEST LAYOUT builds the piece in a Grid run, its cells keep the plans asked for, and the player stands in it
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 1 }, port, false, "Host", Role.Dps, 0, 0);
        var g = host.Game;
        host.StartMatch();
        for (int f = 0; f < 60 * 8 && g.State != MatchState.Fighting; f++) Step(host, dt);
        g.Bot = false;
        g.Diff.Set(DiffKnob.ArenaExpanding, 1f); g.Expanded.Clear(); g.Nodes.Clear(); g.ExpandOpen = true;
        for (int f = 0; f < 60 * 3 && g.Expanded.Count != 4; f++) Step(host, dt);
        int rooms = g.Expanded.Count;
        var plans = new[] { new CellLayouts.CellPlan { Layout = 4, Rot = 1 }, new CellLayouts.CellPlan { Layout = 9, Rot = 0 } };
        string why = g.DevTestCells(PieceShape.Domino, 0, plans);
        var all = CellLayouts.Plans(g.Expanded);
        var last = g.Expanded[g.Expanded.Count - 1];
        int cx, cy;
        CellLayouts.CellOf(last.X + 1, last.Y + 1, out cx, out cy);
        CellLayouts.CellPlan got;
        var me = g.Me;
        Check(why == "" && g.Expanded.Count > rooms && CellLayouts.TryPlan(all, cx, cy, out got) && (got.Layout == 4 || got.Layout == 9) && me.Pos.X > last.X - 700 && me.Pos.X < last.X1 + 700,
            "TEST LAYOUT builds the piece with the asked layouts and stands the player in it (" + why + ")");
        CellLayouts.ClearForced();
        Check(g.DevTestHub(LayoutId.Bunker) == "" && g.Layout == LayoutId.Bunker && Math.Abs(g.Me.Pos.X - Game.Width / 2f) < 1f, "TEST LAYOUT puts a hub plan down and stands the player in the hub");
        g.Expanded.Clear();
        Check(g.DevTestCells(PieceShape.Mono, 0, plans).Length > 0, "without the Grid core TEST LAYOUT says to start a run");
    }
}
