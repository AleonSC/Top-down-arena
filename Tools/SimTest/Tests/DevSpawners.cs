using System;
using System.Linq;
using Arena;

// Dev spawners (Core/DevSpawners.cs): the host places one from the F1 dock; while a wave is on it brings in its enemy on its timer, never more
// than its cap alive; the client sees the pad; bosses can't have one; Undo and Clear take them away.
static partial class Program
{
    static void DevSpawnerTest(float dt)
    {
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2 }, port, false, "Host", Role.Tank);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game; var cg = client.Game;
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        Run(s, dt, 20f, () => g.State == MatchState.Fighting, null);
        Check(g.State == MatchState.Fighting, "a wave is on");

        Check(cg.DevPlaceSpawner(2, EnemyType.Grunt, 1f, 3, false) == 0, "only the host places a spawner");
        Check(g.DevPlaceSpawner(1, EnemyType.Warden, 1f, 3, false) == 0, "a boss can't have a spawner");
        int sid = g.DevPlaceSpawner(1, EnemyType.Grunt, 1f, 3, false);
        Check(sid > 0 && g.DevSpawners.Count == 1, "the host places a spawner");
        Func<int> mine = () => g.Enemies.Values.Count(e => e.DevSpawner == sid && e.Hp > 0f);
        Run(s, dt, 1.5f, null, null);
        Check(mine() >= 1 && mine() <= 2, "it brings in its enemy on its timer (" + mine() + " after 1.5 s)");
        Check(g.Enemies.Values.Where(e => e.DevSpawner == sid).All(e => e.Type == EnemyType.Grunt), "of the type chosen");
        Run(s, dt, 6f, null, null);
        Check(mine() == 3, "never more than its cap alive (" + mine() + ")");
        Check(cg.DevSpawners.Count == 1 && cg.DevSpawners.Values.First().Type == EnemyType.Grunt && cg.DevSpawners.Values.First().Alive == 3, "the client sees the pad, its enemy and how many are alive");
        foreach (var e in g.Enemies.Values.Where(e => e.DevSpawner == sid)) e.Hp = 0f;
        Run(s, dt, 1.6f, null, null);
        Check(mine() >= 1, "kill them and it starts again");
        int sid2 = g.DevPlaceSpawner(1, EnemyType.Grunt, 2f, 1, true);
        Check(g.DevRemoveSpawner(sid2) && g.DevSpawners.Count == 1, "one spawner taken away");
        g.DevClearSpawners();
        Run(s, dt, 0.3f, null, null);
        Check(g.DevSpawners.Count == 0 && cg.DevSpawners.Count == 0, "Clear takes them all away, on both screens");
        client.Leave(""); host.Leave("");
    }
}
