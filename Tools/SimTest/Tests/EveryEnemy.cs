using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Every enemy type, bosses and their second halves included, through one life over the network (suite `everyenemy`):
// it spawns, shows on the friend's screen, takes a hit both screens see, and dies and is gone from both. It loops over
// Defs.Enemies, so a new enemy is checked the day it's added (aitrace only checks that the old ones behave the same).
static partial class Program
{
    static void EveryEnemyTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var hurt = typeof(Game).GetMethod("HurtEnemy", flags);
        var spawnQueue = (System.Collections.IList)null;
        var squadQueue = (System.Collections.IList)null;
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Host", Role.Tank);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        var cg = client.Game;
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        spawnQueue = (System.Collections.IList)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
        squadQueue = (System.Collections.IList)typeof(Game).GetField("squadQueue", flags).GetValue(g);
        // No wave of its own: nothing queued, no cards up, the players parked in a corner.
        Action quiet = () =>
        {
            spawnQueue.Clear(); squadQueue.Clear();
            foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = p.PendingCards = 0; p.LegendaryQueue.Clear(); p.Offer = null; }
        };
        Action killAll = () => { quiet(); foreach (var e in g.Enemies.Values) e.Hp = 0f; };
        Func<bool> empty = () => g.Enemies.Count == 0 && cg.Enemies.Count == 0;
        Run(s, dt, 20f, empty, killAll);

        // Enemies whose own health a hit doesn't touch, by design (the hit goes elsewhere): skipped by the hit check only.
        var passHits = new HashSet<EnemyType> { EnemyType.SingularityShard };  // its damage goes to the Singularity's core
        var notShown = new List<string>();
        var notHurt = new List<string>();
        var notGone = new List<string>();
        var threw = new List<string>();
        int types = Defs.Enemies.Length;
        for (int i = 0; i < types; i++)
        {
            var t = (EnemyType)i;
            string name = Defs.Get(t).Name + " (" + t + ")";
            try
            {
                Run(s, dt, 20f, empty, killAll);  // (anything the last one left behind)
                var at = new V2(800f, 450f);
                var e = (Enemy)spawn.Invoke(g, new object[] { t, at, 0f, 0, false });
                if (e == null) { notShown.Add(name + ": not spawned"); continue; }
                Run(s, dt, 2f, () => cg.Enemies.ContainsKey(e.Id) && e.SpawnT <= 0f, quiet);
                if (!cg.Enemies.ContainsKey(e.Id)) { notShown.Add(name); continue; }
                Run(s, dt, 0.3f, null, quiet);
                if (g.Enemies.ContainsKey(e.Id) && !passHits.Contains(t))
                {
                    float before = e.Hp, seen = cg.Enemies.ContainsKey(e.Id) ? cg.Enemies[e.Id].Hp : -1f;
                    hurt.Invoke(g, new object[] { e, Math.Max(1f, e.MaxHp * 0.1f), 1 });
                    Run(s, dt, 0.5f, null, quiet);
                    bool hostHurt = e.Hp < before, clientHurt = cg.Enemies.ContainsKey(e.Id) && cg.Enemies[e.Id].Hp < seen;
                    if (!hostHurt || !clientHurt)
                        notHurt.Add(name + (hostHurt ? ": friend's screen not hurt" : ": not hurt"));
                }
                Run(s, dt, 20f, empty, killAll);
                if (!empty()) notGone.Add(name + " (host " + g.Enemies.Count + ", friend " + cg.Enemies.Count + " left: " + string.Join(",", g.Enemies.Values.Select(x => x.Type).Distinct()) + ")");
            }
            catch (Exception ex)
            {
                var inner = ex is System.Reflection.TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
                threw.Add(name + ": " + inner.GetType().Name + " " + inner.Message);
            }
        }
        Func<List<string>, string> list = l => l.Count == 0 ? "" : " (" + l.Count + ": " + string.Join("; ", l) + ")";
        Check(threw.Count == 0, "every enemy type (" + types + ") goes through its life without an exception" + list(threw));
        Check(notShown.Count == 0, "every enemy type spawns and shows on the friend's screen" + list(notShown));
        Check(notHurt.Count == 0, "every enemy type takes a hit, and the friend's screen shows it" + list(notHurt));
        Check(notGone.Count == 0, "every enemy type dies and is gone from both screens" + list(notGone));
        Check(g.Running && cg.Running, "the game still runs on both screens after all of them");
        host.Leave(""); client.Leave("");
    }
}
