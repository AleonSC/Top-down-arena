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
    /// The Rioter crowd (RiotBrains.cs): Rioters touching you slow your walk, compounding, barely with one, a lot with a pile.
    static void RioterCrowdTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var queueField = typeof(Game).GetField("spawnQueue", flags);
        var s = StartPlain(dt, 29);
        var g = s[0].Game;
        Run(s, dt, 8f, () => g.State == MatchState.Fighting);
        ((List<EnemyType>)queueField.GetValue(g)).Clear(); ClearSquads(g);
        foreach (var e0 in g.Enemies.Values) e0.Hp = 0f;
        Run(s, dt, 0.3f);
        var me = g.Players[1];
        me.Pos = new V2(800f, 450f);
        Check(Math.Abs(g.CrowdSlow(me) - 1f) < 1e-4f, "no Rioters: no slow");
        var far = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Rioter, me.Pos + new V2(80f, 0f), 0f, 0, false });
        far.SpawnT = 0f; far.Pos = me.Pos + new V2(80f, 0f);
        Check(Math.Abs(g.CrowdSlow(me) - 1f) < 1e-4f, "a Rioter 80 px off doesn't slow you");
        var ring = new List<Enemy>();
        float[] at = new float[11];
        for (int i = 0; i < 10; i++)
        {
            var r = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Rioter, me.Pos, 0f, 0, false });
            r.SpawnT = 0f; r.Pos = me.Pos + V2.FromAngle(i * 0.628f) * (me.Def.BodyR + r.Radius + 2f);
            ring.Add(r);
            at[i + 1] = g.CrowdSlow(me);
        }
        Console.WriteLine("  rioter crowd: speed x{0:0.00} with 1, x{1:0.00} with 3, x{2:0.00} with 6, x{3:0.00} with 10", at[1], at[3], at[6], at[10]);
        Check(at[1] > 0.93f && at[1] < 1f, "one Rioter on you is barely felt");
        Check(at[6] < at[3] && at[3] < at[1], "each Rioter on you slows you more");
        Check(at[10] < 0.72f && at[10] >= Game.CrowdSlowMin, "a pile of Rioters slows you a lot, down to the floor");
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
        int bottlers = 0, total = 40, early = 0;
        int wave0 = g.Wave; g.Wave = 1;
        for (int i = 0; i < total; i++) { var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Rioter, new V2(100f, 100f), 0f, 0, false }); if (Game.BottleRioter(e)) early++; e.Hp = 0f; }
        Check(early == 0, "no Rioter carries a bottle on wave 1 (" + early + " of " + total + ")");
        g.Wave = Math.Max(2, wave0);
        for (int i = 0; i < total; i++) { var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Rioter, new V2(100f, 100f), 0f, 0, false }); if (Game.BottleRioter(e)) bottlers++; e.Hp = 0f; }
        Run(s, dt, 0.3f);
        Check(bottlers >= 4 && bottlers <= 22, "some Rioters carry a bottle, not all (" + bottlers + " of " + total + ")");
        float bottleReach = 0f;
        bool keptBottle = true;
        Func<bool, int> Throws = want =>
        {
            Enemy r; int tries = 0;
            do { r = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Rioter, new V2(1200f, 450f), 0f, 0, false }); if (Game.BottleRioter(r) == want) break; r.Hp = 0f; } while (++tries < 400);
            r.SpawnT = 0f; r.Hp = r.MaxHp = 1e6f; r.Speed = 0f;
            friend.Downed = true; g.Bullets.Clear();
            var seen = new HashSet<int>(); float reach = 0f;
            Run(s, dt, 10f, null, () => { anchor.FrozenT = 1f; anchor.FireT = 99f; anchor.Pos = new V2(1500f, 80f); me.Pos = new V2(1000f, 450f); r.Pos = new V2(1200f, 450f); foreach (var b in g.Bullets.Values) if (b.Kind == Defs.BottleShot) { seen.Add(b.Id); reach = Math.Max(reach, b.Pos.DistanceTo(r.Pos)); } });
            if (want) keptBottle = Game.BottleRioter(r);
            r.Hp = 0f; Run(s, dt, 0.3f);
            if (want) bottleReach = reach;
            return seen.Count;
        };
        int thrown = Throws(true), plain = Throws(false);
        Console.WriteLine("  bottles in 10 s: bottle rioter {0}, plain rioter {1}", thrown, plain);
        Check(thrown == 1, "a bottle Rioter throws its one bottle, once");
        Check(!keptBottle, "after its throw it has no bottle left");
        Check(plain == 0, "a plain Rioter never throws");
        Check(bottleReach > 150f, "a bottle flies straight out, not in circles (got " + bottleReach.ToString("0") + " px from the thrower)");

        // A bottle that hits cuts: a short bleed. A parried one smashes: nothing comes back, no cut.
        g.DevSetGod(1, false); friend.Downed = true; g.Bullets.Clear();
        foreach (var e0 in g.Enemies.Values) e0.Hp = 0f;
        Run(s, dt, 0.3f);
        me.Pos = new V2(1000f, 450f); me.Hp = me.MaxHp; me.InvulnT = 0f; me.Parrying = false; me.BleedT = 0f;
        var cut = typeof(Game).GetMethod("ShotHitsPlayer", flags);
        var bot = (Bullet)typeof(Game).GetMethod("SpawnBullet", flags).Invoke(g, new object[] { Defs.BottleShot, me.Pos, new V2(0f, 0f), 0f, 1f, Defs.BulletDamage(Defs.BottleShot), 0, false, 0f });
        cut.Invoke(g, new object[] { me, bot });
        Check(me.BleedT > 2f, "a bottle that hits leaves you bleeding (" + me.BleedT.ToString("0.0") + " s)");
        float hp0 = me.Hp;
        Run(s, dt, 0.2f, null, () => { anchor.FrozenT = 1f; anchor.FireT = 99f; anchor.Pos = new V2(1500f, 80f); me.Pos = new V2(1000f, 450f); });
        me.InvulnT = 0f;
        float afterHit = me.Hp;
        Run(s, dt, 3.5f, null, () => { anchor.FrozenT = 1f; anchor.FireT = 99f; anchor.Pos = new V2(1500f, 80f); me.Pos = new V2(1000f, 450f); });
        Check(me.BleedT <= 0f, "the bleed runs out");
        Check(me.Hp < afterHit - 3f, "the bleed hurts a little over its time (" + (afterHit - me.Hp).ToString("0.0") + " hp)");
        g.Bullets.Clear(); me.BleedT = 0f; me.Parrying = true; me.ParryBroken = 0f; me.Aim = 0f; me.Hp = me.MaxHp; me.InvulnT = 0f;
        var bot2 = (Bullet)typeof(Game).GetMethod("SpawnBullet", flags).Invoke(g, new object[] { Defs.BottleShot, me.Pos + new V2(me.Def.BodyR + 6f, 0f), new V2(-330f, 0f), 0f, 1f, Defs.BulletDamage(Defs.BottleShot), 0, false, 0f });
        var reflects = (List<Bullet>)typeof(Game).GetField("reflects", flags).GetValue(g);
        reflects.Clear();
        bool stopped = (bool)typeof(Game).GetMethod("ShieldsStop", flags).Invoke(g, new object[] { me, bot2 });
        Check(stopped && reflects.Count == 0, "a parried bottle smashes on the shield, nothing is sent back");
        Check(me.BleedT <= 0f, "a parried bottle doesn't cut");
    }
}
