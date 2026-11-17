using System;
using System.Collections.Generic;
using Arena;

static partial class Program
{
    /// The riot Guard's baton (GuardSwing.cs): close to you it swings without stopping, forehand then backhand, each swing
    /// landing BatonDamage in its arc, and it never swings at someone out of reach.
    static void GuardSwingTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var queueField = typeof(Game).GetField("spawnQueue", flags);
        var s = StartPlain(dt, 23);
        var g = s[0].Game;
        var me = g.Players[1]; var friend = g.Players[2];
        Run(s, dt, 8f, () => g.State == MatchState.Fighting);
        ((List<EnemyType>)queueField.GetValue(g)).Clear(); ClearSquads(g);
        foreach (var e0 in g.Enemies.Values) e0.Hp = 0f;
        Run(s, dt, 0.2f);
        g.Bullets.Clear();

        // Far away: it walks in and doesn't swing until it's close.
        V2 home = new V2(800f, 450f);
        var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Guard, home + new V2(-400f, 0f), 0f, 0, false });
        e.SpawnT = 0f; e.Hp = e.MaxHp = 1e6f;
        float farthestSwing = 0f, still = 0f;
        int fore = 0, back = 0, hits = 0; byte last = 0; V2 lastPos = e.Pos;
        Run(s, dt, 8f, null, () =>
        {
            bool hurt = me.Hp < me.MaxHp;
            me.Pos = home; me.Hp = me.MaxHp; me.InvulnT = 0f;
            friend.Pos = new V2(150f, 820f); friend.Downed = true;
            if (e.LarderStage != last && (e.LarderStage == Game.BatonActing || e.LarderStage == Game.BatonActingBack))
            {
                if (e.LarderStage == Game.BatonActing) fore++; else back++;
                farthestSwing = Math.Max(farthestSwing, e.Pos.DistanceTo(me.Pos));
                if (hurt) hits++;
            }
            if (e.LarderStage != Game.LarderIdle && e.Pos.DistanceTo(lastPos) < 0.01f) still += dt;
            last = e.LarderStage; lastPos = e.Pos;
        });
        Console.WriteLine("  guard 8 s: forehands {0}, backhands {1}, swings that hit {2}, farthest swing start {3:0} px, still while swinging {4:0.00} s",
            fore, back, hits, farthestSwing, still);
        Check(fore >= 3 && back >= 3 && Math.Abs(fore - back) <= 1, "it swings forehand and backhand in turn");
        Check(hits >= (fore + back) / 2, "its swings land on you when you're in the arc");
        Check(farthestSwing <= Game.BatonTrigger + e.Radius + 30f, "it only swings when you're close");
        Check(still < 0.5f, "it keeps walking while it swings (no plant)");
        e.Hp = 0f; Run(s, dt, 0.3f);
    }
    /// A Guard's rally (RiotBrains.cs): a Rioter near a Guard runs GuardRallySpeedUp faster than one alone.
    static void GuardRallyTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var queueField = typeof(Game).GetField("spawnQueue", flags);
        var s = StartPlain(dt, 23);
        var g = s[0].Game;
        Run(s, dt, 8f, () => g.State == MatchState.Fighting);
        ((List<EnemyType>)queueField.GetValue(g)).Clear(); ClearSquads(g);
        foreach (var e0 in g.Enemies.Values) e0.Hp = 0f;
        Run(s, dt, 0.3f);
        g.Bullets.Clear(); g.DevSetGod(1, true);
        var me = g.Players[1]; var friend = g.Players[2];
        Func<bool, float> Pace = withGuard =>
        {
            me.Pos = new V2(1500f, 450f); friend.Downed = true;
            var r = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Rioter, new V2(300f, 450f), 0f, 0, false });
            Enemy guard = null;
            if (withGuard) { guard = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Guard, new V2(300f, 450f), 0f, 0, false }); guard.SpawnT = 0f; guard.Hp = guard.MaxHp = 1e6f; }
            r.SpawnT = 0f; r.Hp = r.MaxHp = 1e6f;
            V2 start = r.Pos;
            Run(s, dt, 1f, null, () => { me.Pos = new V2(1500f, 450f); if (guard != null) guard.Pos = r.Pos + new V2(-110f, 0f); });
            float d = r.Pos.DistanceTo(start);
            r.Hp = 0f; if (guard != null) guard.Hp = 0f;
            Run(s, dt, 0.3f);
            return d;
        };
        float alone = Pace(false), rallied = Pace(true);
        Console.WriteLine("  guard rally: rioter 1 s alone {0:0} px, with a guard {1:0} px", alone, rallied);
        Check(alone > 20f && rallied > alone * 1.1f, "a Rioter beside a Guard runs faster than one alone");
    }
    /// Rioter bottles (RiotBrains.cs): only some Rioters carry one; they throw at a player 110-300 px away, a plain Rioter never does.
    static void RioterBottleTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var queueField = typeof(Game).GetField("spawnQueue", flags);
        var s = StartPlain(dt, 29);
        var g = s[0].Game;
        Run(s, dt, 8f, () => g.State == MatchState.Fighting);
        ((List<EnemyType>)queueField.GetValue(g)).Clear(); ClearSquads(g);
        var anchor = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, new V2(1500f, 80f), 0f, 0, false });  // keeps the wave fighting
        foreach (var e0 in g.Enemies.Values) if (e0 != anchor) e0.Hp = 0f;
        Run(s, dt, 0.3f, null, () => { anchor.FrozenT = 1f; anchor.FireT = 99f; anchor.Pos = new V2(1500f, 80f); });
        g.Bullets.Clear(); g.DevSetGod(1, true);
        var me = g.Players[1]; var friend = g.Players[2];
        int bottlers = 0, total = 40;
        for (int i = 0; i < total; i++) { var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Rioter, new V2(100f, 100f), 0f, 0, false }); if (Game.BottleRioter(e)) bottlers++; e.Hp = 0f; }
        Run(s, dt, 0.3f);
        Check(bottlers >= 4 && bottlers <= 22, "some Rioters carry a bottle, not all (" + bottlers + " of " + total + ")");
        float bottleReach = 0f;
        Func<bool, int> Throws = want =>
        {
            Enemy r; int tries = 0;
            do { r = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Rioter, new V2(1200f, 450f), 0f, 0, false }); if (Game.BottleRioter(r) == want) break; r.Hp = 0f; } while (++tries < 400);
            r.SpawnT = 0f; r.Hp = r.MaxHp = 1e6f; r.Speed = 0f;
            friend.Downed = true; g.Bullets.Clear();
            var seen = new HashSet<int>(); float reach = 0f;
            Run(s, dt, 10f, null, () => { anchor.FrozenT = 1f; anchor.FireT = 99f; anchor.Pos = new V2(1500f, 80f); me.Pos = new V2(1000f, 450f); r.Pos = new V2(1200f, 450f); foreach (var b in g.Bullets.Values) if (b.Kind == Defs.BottleShot) { seen.Add(b.Id); reach = Math.Max(reach, b.Pos.DistanceTo(r.Pos)); } });
            r.Hp = 0f; Run(s, dt, 0.3f);
            if (want) bottleReach = reach;
            return seen.Count;
        };
        int thrown = Throws(true), plain = Throws(false);
        Console.WriteLine("  bottles in 10 s: bottle rioter {0}, plain rioter {1}", thrown, plain);
        Check(thrown >= 1 && thrown <= 4, "a bottle Rioter throws now and then");
        Check(plain == 0, "a plain Rioter never throws");
        Check(bottleReach > 150f, "a bottle flies straight out, not in circles (got " + bottleReach.ToString("0") + " px from the thrower)");
    }
}
