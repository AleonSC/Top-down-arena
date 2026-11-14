using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

// Enemies stay inside the map's rooms (EnemyAI.cs KeepEnemiesInRooms): a crowd jammed in a hallway, and one taken through a wall.
static partial class Program
{
    static void ExpandingStayInRoomsTest(float dt)
    {
        string why;
        var list = new List<ExpansionRoom>();
        list.Add(Expansion.Custom(list, 1600, 300, 900, 200, out why).Value);  // a hallway off the hub's east wall
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 6 }, port, true, "Host", Role.Tank);
        var client = JoinAs(port, true, "Friend", Role.Dps);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        foreach (var gg in new[] { g, client.Game }) { gg.Diff.Set(DiffKnob.ArenaExpanding, 1f); gg.Diff.Set(DiffKnob.BuildVote, 1f); gg.Expanded.Clear(); gg.Expanded.AddRange(list); }
        Run(s, dt, 30f, () => g.State == MatchState.Fighting);
        Func<V2, bool> inRooms = p => g.CurrentWorld.Areas.Any(a => !a.Sealed && a.Contains(p));
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        foreach (var id in g.Enemies.Keys.ToList()) g.Enemies.Remove(id);
        var crowd = new List<Enemy>();
        for (int i = 0; i < 40; i++)
            crowd.Add((Enemy)spawn.Invoke(g, new object[] { i % 2 == 0 ? EnemyType.Grunt : EnemyType.Shade, new V2(1700f + 20f * i, 400f), 0.1f, -1, false }));
        foreach (var p in g.Players.Values) p.Pos = new V2(2100f, 400f);
        int outside = 0;
        Run(s, dt, 8f, null, () =>
        {
            foreach (var p in g.Players.Values) { p.Hp = p.MaxHp; p.Downed = false; p.Pos = new V2(2100f, 400f); }
            foreach (var e in crowd)
                if (g.Enemies.ContainsKey(e.Id) && !inRooms(e.Pos)) outside++;
        });
        Check(outside == 0, "a crowd jammed in a hallway stays inside the rooms (" + outside + " enemy-frames outside)");
        var one = crowd.FirstOrDefault(e => g.Enemies.ContainsKey(e.Id));
        V2 away = new V2(2050f, 150f);
        Check(one != null && !inRooms(away), "a spot outside the map to test with");
        if (one == null) return;
        one.Pos = away;
        Run(s, dt, 2 * dt);
        Check(inRooms(one.Pos), "an enemy taken through a wall comes straight back inside (" + one.Pos.X.ToString("0") + "," + one.Pos.Y.ToString("0") + ")");
    }
}
