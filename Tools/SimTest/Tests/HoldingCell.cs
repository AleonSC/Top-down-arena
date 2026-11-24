using System;
using System.Linq;
using System.Reflection;
using Arena;

// The Warden's net and Holding Cells (Core/HoldingCell.cs): netted into a cell off the arena (seen by both games), guards
// piling in after the prisoner alone, the padlock (0.25 s a hit) and abilities (1 s each), the fight going on while
// someone's left in the arena and pausing when nobody is, the second prisoner next door, the first one out resuming
// the fight, downed inside = sent back downed, and everyone let out when the Warden falls.
static partial class Program
{
    static void HoldingCellTest(float dt)
    {
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var s = StartBuffed(dt, 5, 0);
        var g = s[0].Game; var cg = s[1].Game;
        g.Bot = false; cg.Bot = false;
        g.DevSetGod(1, true);
        Run(s, dt, 12, () => { var b = Find(s[0], EnemyType.Warden); return b != null && b.SpawnT <= 0f; });
        Run(s, dt, 0.2f);
        var w = Find(s[0], EnemyType.Warden);
        Console.WriteLine("  holding cell: warden {0}, state {1}, players {2}", w != null, w != null && w.Warden != null, g.Players.Count);
        if (w == null || w.Warden == null) { Check(false, "the Warden is up"); return; }
        var me = g.Players[1]; var friend = g.Players[2];
        Action quiet = () => { foreach (var e in g.Enemies.Values) if (!e.Def.IsBoss && !Game.InCellZone(e.Pos)) e.Hp = 0f; };
        Action park = () => { if (!Game.InCellZone(me.Pos)) me.Pos = new V2(200f, 800f); };

        // Netted: the light locks on, the net flies, and the friend is moved into a cell (their own game too).
        var ws = w.Warden;
        ws.Turn = WardenTurn.Light; ws.TurnT = 0f; ws.TurnLen = 18f; ws.LightMode = WardenLight.Follow; ws.LightT = 1f;
        friend.Pos = new V2(1200f, 700f); ws.Light = friend.Pos;
        bool sawNet = false, clientNet = false;
        Run(s, dt, 6f, () => g.HoldingCells.Count > 0, () =>
        {
            quiet(); park();
            if (!Game.InCellZone(friend.Pos) && ws.NetId == 0) friend.Pos = new V2(1200f, 700f);
            w.TargetId = 2; w.RetargetT = 99f;
            sawNet |= ws.LightMode == WardenLight.Net && ws.NetId == 2;
            clientNet |= cg.Enemies.ContainsKey(w.Id) && cg.Enemies[w.Id].Warden != null && cg.Enemies[w.Id].Warden.NetId == 2;
        });
        var cell = g.CellOf(friend);
        Run(s, dt, 0.3f, null, () => { quiet(); park(); });
        Console.WriteLine("  holding cell: net seen {0} (client {1}), cell slot {2}, friend at {3}, client sees {4} cell(s), its own player in the cell {5}",
            sawNet, clientNet, cell == null ? -1 : cell.Slot, friend.Pos, cg.HoldingCells.Count, cg.Me != null && Game.InCellZone(cg.Me.Pos));
        Check(sawNet && clientNet && cell != null && cell.Slot == 1 && Game.InCellZone(friend.Pos), "the searchlight's lock-on ends in a net, and the one it caught is locked in a Holding Cell");
        Check(cg.HoldingCells.Count == 1 && cg.Me != null && Game.InCellZone(cg.Me.Pos), "the prisoner's own game moves them into the cell and knows about it");
        if (cell == null) return;

        // Guards pile in for the prisoner alone; the fight goes on (the host is still in the arena).
        float turnBefore = ws.TurnT, hurt0 = friend.Run.DamageTaken;
        int guards = 0; bool guardOnFriend = true, wardenOnFriend = false, guardsOut = false;
        Run(s, dt, 3f, null, () =>
        {
            quiet(); park();
            guards = Math.Max(guards, g.Enemies.Values.Count(e => Game.InCellZone(e.Pos)));
            foreach (var e in g.Enemies.Values)
            {
                if (Game.InCellZone(e.Pos) && !g.IsSpawning(e) && e.T > 0.7f && e.TargetId != 2) guardOnFriend = false;
                if (e.Type == EnemyType.Guard && Game.CellSlotAt(e.Pos, 0f) < 0 && Game.CellSlotAt(e.Pos, Game.CellZone) >= 0) guardsOut = true;
            }
            wardenOnFriend |= w.TargetId == 2 && ws.TurnT > turnBefore + 0.5f;
        });
        Console.WriteLine("  holding cell: guards in the cell {0}, all on the prisoner {1}, the arena moving {2}, held {3}", guards, guardOnFriend, ws.TurnT > turnBefore, g.ArenaHeld);
        Check(guards >= 4 && guardOnFriend && !guardsOut, "Riot Guards pile into the cell (four at once, then more), go for its prisoner alone and stay inside the bars");
        Check(ws.TurnT > turnBefore + 2f && !g.ArenaHeld, "with a player still in the arena, the fight goes on");
        Console.WriteLine("  holding cell: the guards' batons took {0:0} off the prisoner", friend.Run.DamageTaken - hurt0);
        Check(friend.Run.DamageTaken > hurt0, "the guards' batons hurt the prisoner");

        // The padlock: a teammate's shot takes a quarter second off; an ability inside takes a second off.
        var spawn = typeof(Game).GetMethod("SpawnBullet", flags);
        float left0 = cell.Left; int hits0 = cell.LockHits;
        spawn.Invoke(g, new object[] { Defs.DpsShot, cell.At + new V2(-60f, 0f), new V2(600f, 0f), 0f, 1f, 10f, 1, false, 0f });
        Run(s, dt, 0.3f, () => cell.LockHits > hits0, () => { quiet(); park(); });
        float lockCut = left0 - cell.Left;
        left0 = cell.Left;
        typeof(Game).GetMethod("HostAbility", flags, null, new[] { typeof(int), typeof(int), typeof(V2), typeof(bool) }, null)
            .Invoke(g, new object[] { 2, 0, friend.Pos, false });
        float skillCut = left0 - cell.Left;
        Console.WriteLine("  holding cell: a padlock hit took {0:0.00} s off (hits {1}), an ability took {2:0.00} s off (skills {3})", lockCut, cell.LockHits, skillCut, cell.Skills);
        Check(cell.LockHits == hits0 + 1 && lockCut > 0.24f && lockCut < 0.5f, "a teammate's shot on the padlock takes a quarter second off the cell's clock");
        Check(cell.Skills == 1 && Math.Abs(skillCut - 1f) < 0.05f, "an ability cast inside takes a second off");

        // Bots: locked in, one fights the guards and casts what it has; outside, one shoots its teammate's padlock.
        cell.Left = 25f;
        int skills0 = cell.Skills, locks0 = cell.LockHits;
        foreach (var sl in friend.Slots) if (sl.Id != AbilityId.None) { sl.Charges = sl.MaxCharges; sl.Timer = 0f; }  // (fresh: the test used one above)
        g.Bot = true; cg.Bot = true;
        Run(s, dt, 4f, null, quiet);
        g.Bot = false; cg.Bot = false;
        Console.WriteLine("  holding cell: bots: casts inside {0}, padlock hits from outside {1}, prisoner still in {2}", cell.Skills - skills0, cell.LockHits - locks0, Game.InCellZone(friend.Pos));
        Check(cell.Skills - skills0 >= 1 && cell.LockHits - locks0 >= 4, "a bot in a cell casts its abilities to get out sooner; a bot outside shoots its teammate's padlock");
        cell.Left = Math.Max(cell.Left, 10f);

        // The host netted too: next door, both in view; nobody left in the arena, so the fight pauses.
        typeof(Game).GetMethod("JailPlayer", flags).Invoke(g, new object[] { me, me.Pos });
        var mine = g.CellOf(me);
        Run(s, dt, 0.6f, null, quiet);
        V2 wAt = w.Pos; float turnT = ws.TurnT; float cellLeft = mine.Left;
        var arenaShot = (Bullet)spawn.Invoke(g, new object[] { Defs.BigShot, new V2(800f, 300f), new V2(0f, 150f), 0f, 5f, 10f, 0, true, 0f });
        V2 shotAt = arenaShot.Pos;
        Run(s, dt, 1f, null, quiet);
        Console.WriteLine("  holding cell: second cell slot {0}; held {1} (client {2}); Warden moved {3:0.0}, its turn clock {4:0.00} -> {5:0.00}; arena shot moved {6:0.0}; the cell clock ran {7:0.00}",
            mine.Slot, g.ArenaHeld, cg.ArenaHeld, w.Pos.DistanceTo(wAt), turnT, ws.TurnT, arenaShot.Pos.DistanceTo(shotAt), cellLeft - mine.Left);
        Check(mine.Slot == 0 && Math.Abs(Game.CellCentre(0).X - Game.CellCentre(1).X) == Game.CellPitch, "a second player netted lands in the cell next door");
        Check(g.ArenaHeld && cg.ArenaHeld && w.Pos.DistanceTo(wAt) < 0.5f && Math.Abs(ws.TurnT - turnT) < 0.001f && arenaShot.Pos.DistanceTo(shotAt) < 0.5f && cellLeft - mine.Left > 0.9f,
            "with everyone locked up the fight pauses (the Warden and shots in the arena hold still) while the cells' clocks run");

        // The friend's door opens first: back where they were netted, and the fight goes on.
        cell.Left = 0.02f;
        Run(s, dt, 2f, () => g.CellOf(friend) == null, quiet);
        Run(s, dt, 0.5f, null, quiet);
        float t1 = ws.TurnT;
        Run(s, dt, 0.5f, null, quiet);
        Console.WriteLine("  holding cell: friend out at {0} (netted at {1}), held {2}, the Warden's clock {3:0.00} -> {4:0.00}", friend.Pos, cell.At, g.ArenaHeld, t1, ws.TurnT);
        Check(!Game.InCellZone(friend.Pos) && friend.Pos.DistanceTo(cell.At) < 40f && !g.ArenaHeld && ws.TurnT > t1 + 0.3f,
            "the first one out is back where they were netted, and the fight starts again");

        // Downed inside: sent straight back to the fight, downed, the cell emptied.
        g.CellOf(me).Left = 30f;
        typeof(Game).GetMethod("JailPlayer", flags).Invoke(g, new object[] { friend, friend.Pos });
        var fc = g.CellOf(friend);
        Run(s, dt, 2f, null, quiet);
        int fslot = fc.Slot;
        friend.Hp = 0f;
        typeof(Game).GetMethod("DownIfDead", flags).Invoke(g, new object[] { friend, "test" });
        Run(s, dt, 0.2f, null, quiet);
        int left = g.Enemies.Values.Count(e => Game.CellSlotAt(e.Pos, Game.CellZone) == fslot);
        Console.WriteLine("  holding cell: downed inside -> at {0} (netted at {1}), downed {2}, cell gone {3}, guards left in it {4}", friend.Pos, fc.At, friend.Downed, g.CellOf(friend) == null, left);
        Check(friend.Downed && !Game.InCellZone(friend.Pos) && friend.Pos.DistanceTo(fc.At) < 40f && g.CellOf(friend) == null && left == 0,
            "downed in the cell, you're sent back to the fight downed, for a teammate to revive");

        // The Warden falls: everyone still locked up comes back at once.
        g.Enemies.Remove(w.Id);
        Run(s, dt, 1f, () => g.HoldingCells.Count == 0, null);
        Console.WriteLine("  holding cell: the Warden down -> cells {0}, host in a cell {1}", g.HoldingCells.Count, Game.InCellZone(me.Pos));
        Check(g.HoldingCells.Count == 0 && !Game.InCellZone(me.Pos), "when the Warden falls, everyone still locked up is let out");
        s[0].Leave(""); s[1].Leave("");
    }
}
