using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

// The 50 px floor grid (RoomTraps.Tile, FloorGridLook.cs): every hand-made or rolled piece of cover sits on it.
static partial class Program
{
    static void FloorGridTest()
    {
        const float T = RoomTraps.Tile;
        Func<float, bool> line = v => Math.Abs(v / T - Math.Round(v / T)) < 0.001f;
        Func<float, bool> mid = v => line(v - T / 2f);
        // a box: edges on lines; a disc: a whole number of squares across, on a square's middle (odd) or a line (even)
        Func<Solid, bool> on = s => s.Round
            ? line(2f * s.R) && ((int)Math.Round(2f * s.R / T) % 2 == 1 ? mid(s.Pos.X) && mid(s.Pos.Y) : line(s.Pos.X) && line(s.Pos.Y))
            : line(s.Pos.X - s.HW) && line(s.Pos.X + s.HW) && line(s.Pos.Y - s.HH) && line(s.Pos.Y + s.HH);
        Func<int[], bool> wallOn = w => line(w[0]) && line(w[1]) && line(w[2]) && line(w[3]);

        // The six arena floor plans: walls (not the half-square edge wall's ends, which follow the gates), crates on square middles.
        int off = 0;
        foreach (var d in Layouts.Playable())
        {
            foreach (var s in d.Solids.Where(s => !s.Edge && !on(s))) { off++; Console.WriteLine("  " + d.Name + ": a wall off the grid at " + s.Pos); }
            foreach (var c in d.Crates.Where(c => !mid(c.Pos.X) || !mid(c.Pos.Y))) { off++; Console.WriteLine("  " + d.Name + ": a crate off the grid at " + c.Pos); }
            foreach (var g in d.PlayerGates.Concat(d.EnemyGates).Where(g => !line(g.From) || !line(g.To))) { off++; Console.WriteLine("  " + d.Name + ": a gate off the grid"); }
        }
        Check(off == 0 && Layouts.WallT == T && Layouts.EdgeT == T / 2f, "the six arena floor plans: walls a square thick on grid lines, crates on square middles, gates between lines (" + off + " off)");

        // The cell layouts and their pit variants, each turned four ways (the cell's corner is on the grid).
        off = 0;
        for (int li = 0; li < CellLayouts.All.Length; li++)
            for (int vi = 0; vi <= CellLayouts.VariantsOf(li).Length; vi++)
                for (int rot = 0; rot < 4; rot++)
                {
                    var t = CellLayouts.TurnedOf(new CellLayouts.CellPlan { Layout = li, Rot = rot, Variant = vi });
                    off += t.Walls.Count(w => !wallOn(w));
                    off += t.Pits.Count(p => p.Length == 3 ? !on(new Solid { Round = true, Pos = new V2(p[0], p[1]), R = p[2] }) : !wallOn(p));
                }
        Check(off == 0 && CellLayouts.Cell % (int)T == 0 && Expansion.GridX0 % (int)T == 0, "the 18 cell layouts and their pit variants, turned every way: walls and pits on the grid (" + off + " off)");

        // Rolled cover: the random obstacles of rooms with no hand-made plan (here the core's rooms built without one).
        var plain = Expansion.Build(Expansion.GridCore(), Expansion.GridCoreCuts());
        off = plain.Cover.Count(s => !on(s));
        Check(plain.Cover.Length > 0 && off == 0, "a room's rolled cover (pillars, bars, Ls, blocks) sits on the grid (" + plain.Cover.Length + " pieces, " + off + " off)");

        // The Survival room's walls and the Wall Kick drill's panels.
        off = SurvivalRoom.Cover().Count(s => !on(s));
        for (int i = 0; i < Training.KickWalls; i++)
            if (!on(Training.KickPanel(i, Game.Width, Game.Height))) off++;
        Check(off == 0, "the Survival room's walls and the Wall Kick panels sit on the grid (" + off + " off)");
    }
}
