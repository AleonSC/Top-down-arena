using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Readable regular waves (Readability.cs): shooters take turns, a wind-up before each volley, bullet shapes, thinner rings.
static partial class Program
{
    static void ReadabilityTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var s = StartBuffed(dt, 20, 0);
        s[0].Game.Bot = s[1].Game.Bot = false;
        var g = s[0].Game;
        var c = s[1].Game;
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        Run(s, dt, 15, () => g.State == MatchState.Fighting);
        g.Wave = 13; g.BossSlot = false;

        // The limit: 4, one more every 6 waves up to 8, a quarter more per extra player.
        int limit = g.ShooterLimit;
        g.Wave = 1; g.BossSlot = false; int early = g.ShooterLimit; g.Wave = 40; g.BossSlot = true; int late = g.ShooterLimit; g.Wave = 13; g.BossSlot = false;
        Console.WriteLine("  shooters at once (2 players): {0} at wave 1, {1} at 13, {2} at 40", early, limit, late);
        Check(early == 5 && limit == 8 && late == 10, "the shooter limit: 4 to 8 by the wave, a quarter more for a second player");

        // A crowd of Grunts: never more than the limit hold a turn, and the turns go round.
        foreach (var e in g.Enemies.Values) if (!e.Def.IsBoss) e.Hp = 0f;
        Run(s, dt, 0.2f);
        var crowd = new List<Enemy>();
        for (int i = 0; i < 16; i++)
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, new V2(200f + 80f * i, 120f + 40f * (i % 3)), 0f, 0, false });
            e.Hp = e.MaxHp = 1e5f;
            crowd.Add(e);
        }
        int most = 0;
        var hadTurn = new HashSet<int>();
        bool clientWinding = false, windingWithoutTurn = false;
        Run(s, dt, 14f, null, () =>
        {
            ClearSquads(g);
            ((List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g)).Clear();
            int now = crowd.Count(e => e.ShootT > 0f);
            most = Math.Max(most, now);
            foreach (var e in crowd) if (e.ShootT > 0f) hadTurn.Add(e.Id);
            foreach (var e in crowd) windingWithoutTurn |= e.Winding && e.ShootT <= 0f;
            clientWinding |= c.Enemies.Values.Any(e => e.Winding);
        });
        Console.WriteLine("  16 Grunts: at most {0} held a turn at once (limit {1}); {2} of them got one in 14 s", most, limit, hadTurn.Count);
        Check(most <= limit && most >= limit - 1, "only so many shooters at once");
        Check(hadTurn.Count == crowd.Count, "the turns go round: every Grunt got one");
        Check(clientWinding && !windingWithoutTurn, "a shooter glows before it fires (seen on the client), and only on its turn");

        // Shapes: aimed shots are darts, curving ones crescents (on the client too), a ring is round.
        var shots = g.Bullets.Values.Where(b => b.Source == EnemyType.Grunt).ToList();
        int darts = shots.Count(b => b.Look == ShotLook.Dart);
        int clientDarts = shots.Count(b => { Bullet cb; return c.Bullets.TryGetValue(b.Id, out cb) && cb.Look == ShotLook.Dart; });
        // A frozen dummy in a corner keeps the wave going (clearing it would stop everyone shooting).
        var anchor = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, new V2(40f, 40f), 0f, 0, false });
        anchor.Hp = anchor.MaxHp = 1e6f; anchor.Speed = 0f;
        foreach (var e in crowd) e.Hp = 0f;
        foreach (var b in g.Bullets.Values) if (Defs.IsEnemyShot(b.Kind)) b.Life = 0f;  // an empty screen, under the bullet budget
        Run(s, dt, 0.3f);
        // A Spinner's spiral curves (crescents); a Shade's blink ring is a ring (round). Its aimed pair is held off.
        var spinner = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Spinner, new V2(800f, 200f), 0f, 0, false });
        var shade = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Shade, new V2(400f, 200f), 0f, 0, false });
        spinner.Hp = spinner.MaxHp = shade.Hp = shade.MaxHp = 1e5f;
        spinner.FireT = 0.3f;
        shade.Phase = 1; shade.PhaseT = 0.3f; shade.Waypoint = new V2(400f, 260f);
        Func<Bullet, bool> fromRing = b => b.Source == EnemyType.Shade && Math.Abs(b.Vel.Length - 115f) < 1f;
        Run(s, dt, 4f, () => g.Bullets.Values.Any(b => b.Source == EnemyType.Spinner) && g.Bullets.Values.Any(fromRing),
            () => { ClearSquads(g); ((List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g)).Clear(); foreach (var e in g.Enemies.Values) if (e != spinner && e != shade && e != anchor && !e.Def.IsBoss) e.Hp = 0f; anchor.FrozenT = 1f; shade.FireT = 9f; });
        Run(s, dt, 0.2f, null, () => { spinner.FireT = shade.FireT = 9f; shade.PhaseT = 9f; });
        var hex = g.Bullets.Values.Where(b => b.Source == EnemyType.Spinner).ToList();
        var ring = g.Bullets.Values.Where(fromRing).ToList();
        bool clientCrescent = hex.Any(b => { Bullet cb; return c.Bullets.TryGetValue(b.Id, out cb) && cb.Look == ShotLook.Crescent; });
        Console.WriteLine("  shapes: Grunt shots {0} darts of {1} (client {2}); Spinner {3} crescents of {4}; Shade ring {5} shots, {6} round",
            darts, shots.Count, clientDarts, hex.Count(b => b.Look == ShotLook.Crescent), hex.Count, ring.Count, ring.Count(b => b.Look == ShotLook.Round));
        Check(shots.Count > 0 && darts == shots.Count && clientDarts == darts, "aimed shots are darts, on every screen");
        Check(hex.Count > 0 && hex.All(b => b.Look == ShotLook.Crescent) && clientCrescent, "curving shots are crescents, on every screen");
        Check(ring.Count == 8 && ring.All(b => b.Look == ShotLook.Round), "a Shade's blink ring is 8 shots, all round");
        s[0].Leave(""); s[1].Leave("");
    }
}
