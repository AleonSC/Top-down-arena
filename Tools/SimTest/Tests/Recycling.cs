using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

// Enemy recycling (Core/Recycling.cs): a straggler far from every player comes back in near the party; a boss, and one
// that isn't far, stay put.
static partial class Program
{
    static void RecyclingTest(float dt)
    {
        var list = new List<ExpansionRoom>();
        list.Add(Box(1600, 300, 3000, 200).Value);  // a long hallway off the hub's east wall
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 6 }, port, true, "Host", Role.Tank);
        var client = JoinAs(port, true, "Friend", Role.Dps);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        foreach (var gg in new[] { g, client.Game }) { gg.Diff.Set(DiffKnob.ArenaExpanding, 1f); gg.Expanded.Clear(); gg.Expanded.AddRange(list); }
        Run(s, dt, 30f, () => g.State == MatchState.Fighting);
        Check(g.State == MatchState.Fighting, "the wave starts");
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        foreach (var id in g.Enemies.Keys.ToList()) g.Enemies.Remove(id);
        V2 me = new V2(400f, 450f), farSpot = new V2(4400f, 400f);
        Func<Enemy> make = () => (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, farSpot, 0.1f, -1, false });
        var straggler = make();
        var near = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, me + new V2(600f, 0f), 0.1f, -1, false });
        float startDist = straggler.Pos.DistanceTo(me);
        Check(startDist > Recycling.Far, "the straggler starts far away (" + startDist.ToString("0") + " px)");
        int before = g.Recycled;
        V2 nearStart = near.Pos;
        Action hold = () =>
        {
            foreach (var p in g.Players.Values) { p.Hp = p.MaxHp; p.Downed = false; p.Pos = me; }
            straggler.Speed = 0f; near.Speed = 0f;  // (stand still: only recycling moves them)
            straggler.Hp = straggler.MaxHp; near.Hp = near.MaxHp;
        };
        Run(s, dt, Recycling.After - 1f, null, hold);
        Check(g.Recycled == before, "not recycled before " + Recycling.After + " s");
        Run(s, dt, 2f, null, hold);
        float now = straggler.Pos.DistanceTo(me);
        Check(g.Recycled == before + 1, "recycled once after " + Recycling.After + " s (" + (g.Recycled - before) + ")");
        Check(now < startDist && now <= Rooms.SpawnReach + 50f && now >= Rooms.SpawnClear - 1f,
            "it comes back in near the party, outside the spawn clearance (" + now.ToString("0") + " px)");
        Check(near.Pos.DistanceTo(nearStart) < 1f, "an enemy already near the party stays put");
        Check(g.CurrentWorld.Areas.Any(a => !a.Sealed && a.Contains(straggler.Pos)), "its new spot is inside the map");

        // Nobody up: nothing recycles toward a wiped party.
        straggler.Pos = straggler.Disp = farSpot;
        straggler.FarT = 0f;
        int mid = g.Recycled;
        Run(s, dt, Recycling.After + 1f, null, () =>
        {
            foreach (var p in g.Players.Values) { p.Downed = true; p.Pos = me; }
            straggler.Speed = 0f; straggler.Hp = straggler.MaxHp;
        });
        Check(g.Recycled == mid, "nothing recycles with every player down");
    }
}
