using System.Linq;
using System;
using Arena;

// Fixes from the November 2026 code audit: a strong slow ends on time under a weak one that is topped up every tick
// (Slows.cs), a piercing shot inside two overlapping enemies hits each once (Combat.cs PrevHit), a kill's coins carry
// all its gold (Coins.cs), and Stop clears the per-run lists (RunReset.cs).
static partial class Program
{
    static void AuditFixesTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static;
        var tickSlow = typeof(Game).GetMethod("TickSlow", flags);
        Action<Enemy, float> tick = (e, secs) => { for (int f = 0; f < secs / dt; f++) tickSlow.Invoke(null, new object[] { e, dt }); };

        // Slows: a freeze under Wet (0.2 s at 0.75, topped up every tick) ends after its 1 s, then the wet slow applies.
        var sg = new Game(new Session());
        var a = new Enemy();
        sg.Slow(a, 1f, 0.1f);
        for (int f = 0; f < 1.5f / dt; f++) { sg.Slow(a, 0.2f, 0.75f); tickSlow.Invoke(null, new object[] { a, dt }); }
        Check(Math.Abs(a.SlowMult - 0.75f) < 1e-4f, "a 1 s freeze under a topped-up weak slow ends on time (x" + a.SlowMult.ToString("0.00") + " at 1.5 s)");
        tick(a, 0.3f);
        Check(a.SlowMult == 1f && a.SlowT <= 0f, "...and the weak slow wears off when it stops being topped up");
        // A longer, weaker slow waits under a short, strong one.
        var b = new Enemy();
        sg.Slow(b, 3f, 0.5f);
        sg.Slow(b, 1f, 0.1f);
        Check(Math.Abs(b.SlowMult - 0.1f) < 1e-4f, "the stronger slow applies first");
        tick(b, 1.2f);
        Check(Math.Abs(b.SlowMult - 0.5f) < 1e-4f && b.SlowT > 1.5f, "then the longer, weaker one takes over for its remaining time");
        tick(b, 2f);
        Check(b.SlowMult == 1f, "...and ends");
        // The same slow again lasts the longer of the two; a weaker, shorter one changes nothing.
        var c = new Enemy();
        sg.Slow(c, 2f, 0.5f); sg.Slow(c, 1f, 0.5f); sg.Slow(c, 0.5f, 0.8f);
        Check(Math.Abs(c.SlowMult - 0.5f) < 1e-4f && Math.Abs(c.SlowT - 2f) < 1e-4f && c.SlowT2 <= 0f, "a repeated slow keeps the longer time; a weaker, shorter one is dropped");

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 1 }, port, false, "Host", Role.Dps, 0, 0);
        var g = host.Game;
        host.StartMatch();
        for (int f = 0; f < 60 * 8 && g.State != MatchState.Fighting; f++) Step(host, dt);
        g.Bot = false;
        g.DevSetGod(1, true);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var spawnBullet = typeof(Game).GetMethod("SpawnBullet", flags);
        Func<V2, EnemyType, Enemy> make = (where, type) => { var e = (Enemy)spawn.Invoke(g, new object[] { type, where, 0f, 0, false }); e.Hp = e.MaxHp = 1e5f; e.SpawnT = 0f; return e; };

        // Piercing: a slow shot sitting inside two overlapping enemies hits each of them once.
        g.Enemies.Clear(); g.Bullets.Clear();
        var at = new V2(700f, 450f);
        var e1 = make(at, EnemyType.Brute); var e2 = make(at, EnemyType.Brute);
        var shot = (Bullet)spawnBullet.Invoke(g, new object[] { Defs.DpsShot, at, new V2(1f, 0f), 0f, 5f, 10f, 1, false, 0f });
        shot.Pierce = 5;
        var me = g.Players[1];
        me.Pos = new V2(200f, 200f);
        for (int f = 0; f < 0.3f / dt; f++)
        {
            e1.Pos = e2.Pos = at; e1.FrozenT = e2.FrozenT = 1f;
            host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = new V2(200f, 900f) });
        }
        Console.WriteLine("  audit: pierce damage {0:0} + {1:0}, pierce left {2}", e1.MaxHp - e1.Hp, e2.MaxHp - e2.Hp, shot.Pierce);
        Check(e1.Hp < e1.MaxHp && e2.Hp < e2.MaxHp && shot.Pierce == 3 && g.Bullets.ContainsKey(shot.Id), "a piercing shot inside two overlapping enemies hits each once, not by turns");

        // Coins: a boss kill's coins add up to all its gold (at most 6 coins, the last one carries the rest).
        var drop = typeof(Game).GetMethod("DropCoins", flags);
        g.Coins.Clear();
        var boss = new Enemy { Def = Defs.Get(EnemyType.Boss), Pos = at };
        drop.Invoke(g, new object[] { boss });
        int gold = g.Coins.Values.Where(k => !k.Vec).Sum(k => k.Value), coins = g.Coins.Values.Count(k => !k.Vec);
        int want = (int)Math.Round(boss.Def.Xp * Game.GoldPerXp);
        Console.WriteLine("  audit: boss drops {0} gold in {1} coins (want {2})", gold, coins, want);
        Check(gold == want && coins <= Game.MaxCoinsPerKill, "a boss kill drops all its gold in at most 6 coins");

        // Stop clears the per-run lists (a stale entry keyed by enemy id would land on the next run's enemies).
        var steer = typeof(Game).GetField("steerMem", flags);
        var stars = typeof(Game).GetField("stuckStars", flags);
        g.Stop();
        Check(((System.Collections.ICollection)steer.GetValue(g)).Count == 0 && ((System.Collections.ICollection)stars.GetValue(g)).Count == 0 && g.Ink.Count == 0, "Stop clears the per-run lists (steering, stuck stars, ink)");
        host.Leave("");
    }
}
