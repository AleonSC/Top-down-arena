using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Alpha Call's Blood Scent: minions start at 2 stacks, each bite adds one, a full stack lunges and drops back to 2;
// the stacks are replicated and clear when it ends. Legion's frenzy alone stays flat.
static partial class Program
{
    static void AlphaCallTest(float dt)
    {
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var spawnMinion = typeof(Game).GetMethod("SpawnMinion", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2 }, port, false, "Summoner", Role.Dps, 0, 0, Kit.Summoner);
        var client = JoinAs(port, false, "Tank", Role.Tank, 0);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        g.Bot = client.Game.Bot = false;
        foreach (var e in g.Enemies.Values) e.Hp = 0f;
        V2 at = new V2(300f, 450f);
        var foe = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at + new V2(60f, 0f), 0f, 0, false });
        foe.Hp = foe.MaxHp = 1e6f; foe.SpawnT = 0f; foe.FireT = 99f;
        var wolf = (Minion)spawnMinion.Invoke(g, new object[] { MinionKind.Wolf, me, at + new V2(30f, 0f), 30f, false });
        Action<float> play = secs =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                foreach (var e in g.Enemies.Values) if (e != foe) e.Hp = 0f;
                foe.Pos = at + new V2(60f, 0f); foe.FireT = 99f; foe.Hp = Math.Max(foe.Hp, 9e5f);
                me.Pos = at; client.Game.Me.Pos = new V2(60f, 60f);
                host.Poll(); g.FixedStep(dt, new PlayerInput()); g.Update(dt);
                client.Poll(); client.Game.FixedStep(dt, new PlayerInput()); client.Game.Update(dt);
            }
        };

        // Legion's flat frenzy: no stacks.
        me.FrenzyT = 2f;
        play(0.5f);
        Check(wolf.Stacks == 0, "a frenzy without Blood Scent has no stacks");

        // Blood Scent.
        me.FrenzyT = me.ScentT = 6f;
        int peak = 0, drops = 0, prev = 0, clientSaw = 0;
        float hpBefore = foe.Hp, lungeHit = 0f;
        for (int f = 0; f < 4f / dt; f++)
        {
            float before = foe.Hp;
            play(dt);
            peak = Math.Max(peak, wolf.Stacks);
            if (prev >= Game.ScentMax && wolf.Stacks == Game.ScentStart) { drops++; lungeHit = Math.Max(lungeHit, before - foe.Hp); }
            prev = wolf.Stacks;
            Minion cm;
            if (client.Game.Minions.TryGetValue(wolf.Id, out cm)) clientSaw = Math.Max(clientSaw, cm.Stacks);
        }
        Console.WriteLine("  alpha call: peak {0} stacks, {1} lunges, biggest lunge hit {2:0.0}, client saw up to {3}", peak, drops, lungeHit, clientSaw);
        Check(peak == Game.ScentMax, "bites build stacks up to " + Game.ScentMax);
        Check(drops >= 1, "a full stack lunges and drops back to " + Game.ScentStart);
        Check(lungeHit > 0f, "the lunge bite hurts");
        Check(clientSaw >= Game.ScentMax, "clients see the stacks");

        // It ends.
        me.FrenzyT = me.ScentT = 0.1f;
        play(0.4f);
        Check(wolf.Stacks == 0, "stacks clear when Blood Scent ends");
        s[0].Leave(""); s[1].Leave("");
    }
}
