using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using Arena;

// The rule-changing modifiers: Hunted, Death Throes, Solid Walls and Second Wind (Modifiers.cs).
static partial class Program
{
    static void RuleModifiersTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var hurt = typeof(Game).GetMethod("HurtEnemy", flags);
        var damage = typeof(Game).GetMethod("Damage", flags);
        var pick = typeof(Game).GetMethod("PickTarget", flags);
        var keep = typeof(Game).GetMethod("KeepInArena", flags);
        var gameOver = typeof(Game).GetMethod("CheckGameOver", flags);
        int port = nextPort++;
        var host = new Session();
        host.Host(port, "Host", Role.Tank);
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Dps);
        var s = new[] { host, client };
        Action Sync = () => { for (int i = 0; i < 40; i++) { host.Poll(); client.Poll(); Thread.Sleep(2); } };
        for (int i = 0; i < 300 && client.Screen != AppScreen.Lobby; i++) { host.Poll(); client.Poll(); Thread.Sleep(2); }
        Sync();
        foreach (var m in new[] { RunMod.Hunted, RunMod.DeathThroes, RunMod.SolidWalls, RunMod.SecondWind })
            host.SetModifier(m, true);
        Sync();
        client.RequestReady(true); host.RequestReady(true); Sync();
        host.StartMatch();
        for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
        var g = host.Game;
        var c = client.Game;
        Run(s, dt, 6f, () => g.State == MatchState.Fighting);
        Run(s, dt, 0.3f);
        Check(c.Has(RunMod.Hunted) && c.Has(RunMod.SecondWind) && Modifiers.Count(client.Modifiers) == 4, "the friend's game knows the new modifiers");
        var queue = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
        // A frozen dummy in a corner keeps the wave going.
        var anchor = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, new V2(60f, 60f), 0f, 0, false });
        anchor.MaxHp = anchor.Hp = 1e6f;
        Action Quiet = () =>
        {
            queue.Clear(); ClearSquads(g);
            foreach (var e in g.Enemies.Values.ToList()) if (e != anchor && e.T > 0.05f && e.Hp > 0f) g.Enemies.Remove(e.Id);
            anchor.FrozenT = 1f; anchor.FireT = 1e6f; anchor.Speed = 0f;
            foreach (var b in g.Bullets.Values) if (Defs.IsEnemyShot(b.Kind) && b.Source != EnemyType.Grunt) b.Life = 0f;
        };
        Run(s, dt, 0.3f, null, Quiet);

        // Hunted: someone is picked as the wave starts, everyone sees who, every enemy goes for them, and they hit twice as hard.
        int hunted = g.HuntedId, other = hunted == 1 ? 2 : 1;
        Check(hunted != 0 && c.HuntedId == hunted, "Hunted: a player is hunted this wave, and the friend's game knows who");
        Check((int)pick.Invoke(g, null) == hunted, "Hunted: enemies pick the hunted player");
        var dummy = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, new V2(800f, 450f), 0f, 0, false });
        dummy.SpawnT = 0f; dummy.MaxHp = dummy.Hp = 1e5f;
        float h0 = dummy.Hp;
        hurt.Invoke(g, new object[] { dummy, 10f, hunted });
        float byHunted = h0 - dummy.Hp;
        h0 = dummy.Hp;
        hurt.Invoke(g, new object[] { dummy, 10f, other });
        float byOther = h0 - dummy.Hp;
        Console.WriteLine("  hunted: player {0}; a 10 hit from them dealt {1:0.#}, from the other {2:0.#}", hunted, byHunted, byOther);
        Check(byOther > 0f && Math.Abs(byHunted / byOther - Modifiers.HuntedDamage) < 0.05f, "Hunted: the hunted player deals double damage");
        g.Players[hunted].Downed = true;
        Check((int)pick.Invoke(g, null) != hunted, "Hunted: while they're down, enemies go for someone else");
        g.Players[hunted].Downed = false;
        g.Enemies.Remove(dummy.Id);

        // Death Throes: a regular enemy bursts into a ring of 8 a moment after it dies; a boss doesn't.
        var grunt = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Sniper, new V2(800f, 300f), 0f, 0, false });
        grunt.SpawnT = 0f;
        hurt.Invoke(g, new object[] { grunt, grunt.Hp + 1f, 1 });
        int now = g.Bullets.Values.Count(b => b.Source == EnemyType.Sniper);
        Run(s, dt, Modifiers.ThroesFuse + 0.15f, null, () => { anchor.FrozenT = 1f; anchor.FireT = 1e6f; foreach (var p in g.Players.Values) p.InvulnT = 1f; });
        var ring = g.Bullets.Values.Where(b => b.Source == EnemyType.Sniper && b.Pos.DistanceTo(new V2(800f, 300f)) < 200f).ToList();
        Console.WriteLine("  death throes: {0} shots right away, {1} after the fuse", now, ring.Count);
        Check(now == 0 && ring.Count == Modifiers.ThroesShots, "Death Throes: a killed enemy bursts into a ring of 8 a moment after");
        Run(s, dt, 0.2f, null, Quiet);

        // Solid Walls: walking off an edge stops at it instead of wrapping round (on your own game too).
        var me = g.Players[1];
        V2 kept = (V2)keep.Invoke(g, new object[] { new V2(-30f, 450f), me.Def.BodyR });
        V2 keptClient = (V2)keep.Invoke(c, new object[] { new V2(Game.Width + 30f, 450f), me.Def.BodyR });
        Console.WriteLine("  solid walls: walked off the left edge to x {0:0}, off the right on the friend's game to x {1:0}", kept.X, keptClient.X);
        Check(g.Walled && c.Walled && kept.X >= 0f && kept.X < 40f && keptClient.X > Game.Width - 40f && keptClient.X <= Game.Width,
            "Solid Walls: the edges are walls, not a way round (on every screen)");

        // Second Wind: everyone has one from the first wave; down, you're back up by yourself after a moment.
        Check(g.Players.Values.All(p => p.SecondWind) && c.Players.Values.All(p => p.SecondWind), "Second Wind: everyone has one from the start (and the friend's game shows it)");
        var friend = g.Players[2];
        friend.DevGod = false; friend.InvulnT = 0f; friend.ShieldHp = 0f;
        damage.Invoke(g, new object[] { friend, friend.Hp + friend.MaxHp, "test" });
        bool down = friend.Downed;
        // Everyone down at once with a Second Wind in hand isn't game over.
        me.Downed = true; me.Hp = 0f;
        gameOver.Invoke(g, null);
        bool stillOn = g.State != MatchState.GameOver;
        me.Downed = false; me.Hp = me.MaxHp;
        Run(s, dt, Modifiers.SecondWindTime + 0.3f, null, () => { anchor.FrozenT = 1f; anchor.FireT = 1e6f; me.InvulnT = 1f; Quiet(); });
        Console.WriteLine("  second wind: went down {0}; up again {1} at {2:0}% health; used up {3} (friend's game {4})",
            down, !friend.Downed, 100f * friend.Hp / friend.MaxHp, !friend.SecondWind, !c.Players[2].SecondWind);
        Check(down && stillOn, "Second Wind: going down with one in hand isn't the end, even with everyone down");
        Check(!friend.Downed && Math.Abs(friend.Hp / friend.MaxHp - 0.5f) < 0.05f && !friend.SecondWind && !c.Players[2].SecondWind,
            "Second Wind: back up by yourself at half health, and it's used up (on every screen)");
        // It comes back with the next boss cycle (wave 6), not before.
        g.Wave = 3;
        typeof(Game).GetMethod("ModsWaveStart", flags).Invoke(g, null);
        bool early = friend.SecondWind;
        g.Wave = 6;
        typeof(Game).GetMethod("ModsWaveStart", flags).Invoke(g, null);
        Check(!early && friend.SecondWind, "Second Wind: it comes back after each boss, not before");
        host.Leave(""); client.Leave("");
    }
}
