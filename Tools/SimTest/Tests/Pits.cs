using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

// Cell variants and pits (Core/CellVariants.cs, Core/Pits.cs; Docs/CellVariants.md).
static partial class Program
{
    static void PitsTest(float dt)
    {
        // The variants: each turned four ways keeps the middle and the door lanes clear, its pits inside the cell, its slots off them.
        int bad = 0, kinds = 0;
        for (int li = 0; li < CellLayouts.All.Length; li++)
        {
            var vs = CellLayouts.VariantsOf(li);
            for (int vi = 1; vi <= vs.Length; vi++)
            {
                kinds++;
                for (int rot = 0; rot < 4; rot++)
                {
                    var t = CellLayouts.TurnedOf(new CellLayouts.CellPlan { Layout = li, Rot = rot, Variant = vi });
                    string tag = "  " + vs[vi - 1].Name + " (turn " + rot + "): ";
                    Func<float, float, float, bool> pit = (x, y, pad) => t.Pits.Any(p => CellLayouts.InPit(p, x, y, pad));
                    Func<float, float, float, bool> wall = (x, y, pad) => t.Walls.Any(w => x > w[0] - pad && x < w[0] + w[2] + pad && y > w[1] - pad && y < w[1] + w[3] + pad);
                    if (t.Pits.Length == 0) { bad++; Console.WriteLine(tag + "no pits"); }
                    if (pit(300, 300, 60) || wall(300, 300, 40)) { bad++; Console.WriteLine(tag + "the chest's middle is not clear"); }
                    foreach (var d in new[] { new[] { 300, 0 }, new[] { 300, 600 }, new[] { 0, 300 }, new[] { 600, 300 } })
                        if (pit(d[0], d[1], 100) || wall(d[0], d[1], 50)) { bad++; Console.WriteLine(tag + "a door lane is blocked"); }
                    foreach (var p in t.Pits)
                        if ((p.Length == 3 ? p[0] - p[2] : p[0]) < 14 || (p.Length == 3 ? p[1] - p[2] : p[1]) < 14 || (p.Length == 3 ? p[0] + p[2] : p[0] + p[2]) > 586 || (p.Length == 3 ? p[1] + p[2] : p[1] + p[3]) > 586) { bad++; Console.WriteLine(tag + "a pit leaves its cell"); }
                    foreach (var q in t.Sleepers) if (pit(q[0], q[1], 30) || wall(q[0], q[1], 24)) { bad++; Console.WriteLine(tag + "a sleeper slot is on a pit or wall"); }
                    foreach (var q in t.Loot) if (pit(q[0], q[1], 20) || wall(q[0], q[1], 20)) { bad++; Console.WriteLine(tag + "a loot slot is on a pit or wall"); }
                    foreach (var q in t.Traps) if (pit(q[0], q[1], 30)) { bad++; Console.WriteLine(tag + "a trap slot is on a pit"); }
                    if (t.Sleepers.Length == 0 || t.Loot.Length == 0 || t.Traps.Length == 0) { bad++; Console.WriteLine(tag + "no slots"); }
                }
            }
        }
        Check(kinds == 8 && bad == 0, "eight pit variants, each turned four ways: middle and door lanes clear, pits inside the cell, slots off them (" + bad + " bad)");

        // The roll: seeded from the cell, only plain Combat cells, about VariantChance of those whose layout has variants.
        int can = 0, took = 0, shopVaried = 0, differ = 0;
        for (int cx = -12; cx < 12; cx++)
            for (int cy = -12; cy < 12; cy++)
            {
                var r = new ExpansionRoom { X = Expansion.GridX0 + cx * 600, Y = Expansion.GridY0 + cy * 600, W = 600, H = 600 };
                CellLayouts.TryPlan(CellLayouts.Plans(new List<ExpansionRoom> { r }), cx, cy, out var a);
                CellLayouts.TryPlan(CellLayouts.Plans(new List<ExpansionRoom> { r }), cx, cy, out var b);
                if (a.Variant != b.Variant || a.Layout != b.Layout) differ++;
                if (CellLayouts.VariantsOf(a.Layout).Length > 0) { can++; if (a.Variant > 0) took++; }
                var shop = r; shop.Kind = RoomKind.Shop;
                CellLayouts.TryPlan(CellLayouts.Plans(new List<ExpansionRoom> { shop }), cx, cy, out var c);
                if (c.Variant != 0) shopVaried++;
            }
        float share = can > 0 ? took / (float)can : 0f;
        Check(differ == 0 && share > CellLayouts.VariantChance - 0.15f && share < CellLayouts.VariantChance + 0.15f, "a Combat cell rolls its variant from its place, the same every time (" + took + " of " + can + ")");
        Check(shopVaried == 0, "a service room never takes a variant");

        // The world: a pit variant's pits are the world's pits, none in the core.
        CellLayouts.Forced = "Four Wells";
        try
        {
            var rooms = new List<ExpansionRoom>(Expansion.GridCore());
            Check(Expansion.Build(rooms, Expansion.GridCoreCuts()).Pits.Length == 0, "the core has no pits");
            var mono = Expansion.PlacementsFor(rooms, PieceShape.Mono).First(); rooms.Add(mono.Rooms[0]);
            var w = Expansion.Build(rooms, Expansion.GridCoreCuts());
            var cell = mono.Rooms[0];
            Check(w.Pits.Length == 4 && w.Pits.All(p => p.Round && p.Pos.X > cell.X && p.Pos.X < cell.X1 && p.Pos.Y > cell.Y && p.Pos.Y < cell.Y1), "a Four Wells cell puts its four wells in the world (" + w.Pits.Length + ")");
            Check(!w.Pits.Any(p => w.Cover.Any(c => c.Pos.CloserThan(p.Pos, 1f))), "pits are not walls (shots fly over)");
        }
        finally { CellLayouts.Forced = null; }

        // In play: an enemy shoved in dies (the kill to the shover), one walking never goes in, a player falls, climbs out and loses 15%.
        CellLayouts.Forced = "The Chasm";
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 6 }, port, false, "Host", Role.Tank);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        var s = new[] { host, client };
        try
        {
            WaitForGame(host, client, dt);
            var g = host.Game; var cg = client.Game;
            var keep = new HashSet<int>();
            Action calm = () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = p.PendingCards = 0; p.LegendaryQueue.Clear(); p.Offer = null; } foreach (var id in g.Enemies.Keys.Where(k => !keep.Contains(k)).ToList()) g.Enemies.Remove(id); };
            Run(s, dt, 20f, () => g.State == MatchState.Fighting && g.Wave == 6, calm);
            foreach (var gg in new[] { g, cg }) { gg.Diff.Set(DiffKnob.ArenaExpanding, 1f); gg.Expanded.Clear(); gg.Nodes.Clear(); gg.ExpandOpen = true; }
            Run(s, dt, 0.6f, () => cg.Expanded.Count == 4, calm);
            var me = g.Players[1];
            me.Hand[0] = (int)Expansion.MakePieceCard(PieceShape.Mono);
            var spot = Expansion.PlacementsFor(g.Expanded, PieceShape.Mono).First();
            g.PlayPieceCard(0, spot.Orient, spot.X, spot.Y);
            Run(s, dt, 1f, () => g.Expanded.Count == 5 && cg.Expanded.Count == 5, calm);
            for (int i = 0; i < g.Expanded.Count; i++) { var r = g.Expanded[i]; r.Unseen = false; g.Expanded[i] = r; }
            Run(s, dt, 0.3f, null, calm);
            var pits = g.ActivePits;
            Check(pits.Length == 2 && cg.ActivePits.Length == 2, "the built Chasm cell has its two pits on both machines (" + pits.Length + ", " + cg.ActivePits.Length + ")");
            if (pits.Length == 2)
            {
                var pit = pits[0];
                bool tall = pit.HH > pit.HW;  // (turned: the chasm runs across the knock)
                V2 across = tall ? new V2(1f, 0f) : new V2(0f, 1f);
                float half = tall ? pit.HW : pit.HH;

                // shoved in
                var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
                var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, pit.Pos - across * (half + 40f), 0f, 0, false });
                keep.Add(e.Id); e.T = 5f; e.LastHitBy = 1; e.SpawnT = 0f;
                int kills = me.Run.Kills, pk = g.PitKills;
                typeof(Game).GetMethod("KnockEnemy", flags).Invoke(g, new object[] { e, across * (half + 40f) });
                Run(s, dt, 0.6f, null, calm);
                Check(!g.Enemies.ContainsKey(e.Id) && g.PitKills == pk + 1 && me.Run.Kills == kills + 1, "an enemy shoved into a pit dies, the kill to whoever hit it");

                // walking: never in
                var w2 = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, pit.Pos - across * (half + 40f), 0f, 0, false });
                keep.Add(w2.Id); w2.T = 5f; w2.SpawnT = 0f;
                foreach (var p in g.Players.Values) { p.Pos = pit.Pos + across * (half + 80f); }
                g.DevSetGod(1, true); g.DevSetGod(2, true);
                bool everIn = false;
                Run(s, dt, 3f, null, () => { calm(); foreach (var p in g.Players.Values) p.Pos = pit.Pos + across * (half + 80f); everIn |= g.Enemies.ContainsKey(w2.Id) && g.PitAt(w2.Pos) >= 0; });
                Check(!everIn && g.Enemies.ContainsKey(w2.Id) && g.PitKills == pk + 1, "an enemy walking at you across a pit never steps in");
                g.Enemies.Remove(w2.Id);
                g.DevSetGod(1, false); g.DevSetGod(2, false);

                // a player (the client's own game moves them)
                var friend = g.Players[2];
                friend.Hp = friend.MaxHp; friend.InvulnT = 0f;
                Run(s, dt, 0.2f, null, calm);
                cg.Me.Pos = pit.Pos;
                int falls = g.PlayerFalls;
                Run(s, dt, 0.2f, null, calm);
                Check(cg.PitFallT > 0f && cg.PitAt(cg.Me.Pos) >= 0, "a player who walks off the lip drops in: held down in the pit a moment (" + cg.PitFallT.ToString("0.00") + " s left)");
                Run(s, dt, 0.6f, null, calm);
                Check(cg.PitFallT <= 0f && cg.PitAt(cg.Me.Pos) < 0 && cg.Me.Pos.CloserThan(pit.Pos, half + 80f), "a player who falls in climbs out on the rim beside it");
                Check(g.PlayerFalls == falls + 1 && Math.Abs(friend.Hp - friend.MaxHp * (1f - Game.PitHurt)) < 1.5f && friend.InvulnT > 0.3f, "and the host takes 15% of their health and gives a moment's shield (" + friend.Hp.ToString("0") + " / " + friend.MaxHp.ToString("0") + ")");

                // never lethal: the host's own player at 5 HP
                // (no stray shots: at 5 HP one would down you before the fall, which is not what this checks)
                Action still = () => { calm(); foreach (var id in g.Bullets.Values.Where(b => Defs.IsEnemyShot(b.Kind)).Select(b => b.Id).ToList()) g.Bullets.Remove(id); };
                still();
                me.Hp = 5f; me.InvulnT = 0f;
                Run(s, dt, 0.1f, null, still);
                me.Pos = pit.Pos;
                Run(s, dt, 0.8f, null, still);
                Check(!me.Downed && Math.Abs(me.Hp - 1f) < 0.01f && g.PitAt(me.Pos) < 0, "a fall never downs you: it stops at 1 HP (" + me.Hp.ToString("0.00") + " HP, downed " + me.Downed + ", in a pit " + (g.PitAt(me.Pos) >= 0) + ")");
            }
        }
        finally
        {
            CellLayouts.Forced = null;
            host.Leave(""); client.Leave("");
        }
    }
}
