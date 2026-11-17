using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

// The Bulwark Barrier (BulwarkBarrier.cs): the half guard, the Shield Chain, the Brace soaking shots into Ram, the Shield Bash.
static partial class Program
{
    static void BulwarkTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var shot = typeof(Game).GetMethod("SpawnBullet", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2 }, port, false, "Tank", Role.Tank, 3, 0, Kit.Knight);
        var client = JoinAs(port, false, "Healer", Role.Healer, 0);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        var ally = g.Players[2];
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        g.Bot = client.Game.Bot = false;
        Check(me.Weapon.Id == WeaponId.BulwarkBarrier, "the Tank Knight's fourth weapon is the Bulwark Barrier");
        V2 from = new V2(500f, 450f);
        var keep = new List<Enemy>();
        var fxSeen = new List<Effect>();
        Func<V2, Enemy> dummy = at =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false });
            e.Hp = e.MaxHp = 1e5f; e.SpawnT = 0f; e.FireT = 99f; e.FrozenT = 30f;
            keep.Add(e);
            return e;
        };
        // One frame on both games; `pin` keeps you where you are put.
        Action<PlayerInput, bool> frame = (input, pin) =>
        {
            foreach (var e in g.Enemies.Values) if (!keep.Contains(e)) e.Hp = 0f;
            foreach (var e in keep) e.FireT = 99f;
            if (pin) me.Pos = from;
            ally.Pos = client.Game.Me.Pos = from + new V2(-300f, 200f);
            host.Poll(); g.FixedStep(dt, input); g.Update(dt);
            client.Poll(); client.Game.FixedStep(dt, new PlayerInput()); client.Game.Update(dt);
            foreach (var e in client.Game.Effects) if ((e.Kind == FxKind.BulwarkStep || e.Kind == FxKind.BulwarkBlock || e.Kind == FxKind.BulwarkBash) && !fxSeen.Contains(e)) fxSeen.Add(e);
        };
        Action<float, PlayerInput, bool> play = (secs, input, pin) => { for (int f = 0; f < secs * 60; f++) frame(input, pin); };
        var aimRight = new PlayerInput { AimAt = from + new V2(300f, 0f) };
        Action reset = () => { foreach (var e in keep) e.Hp = 0f; keep.Clear(); g.Bullets.Clear(); me.Combo = 0; me.ComboT = 0f; me.FireCd = 0f; me.BbRam = 0f; me.BbBrace = false; play(0.3f, aimRight, true); fxSeen.Clear(); };
        // An enemy round from `at` flying at you.
        Func<V2, byte, Bullet> fire = (at, kind) => (Bullet)shot.Invoke(g, new object[] { kind, at, (from - at).Normalized() * 300f, 0f, 3f, 10f, -1, false, 0f });
        play(0.4f, aimRight, true);

        // The chain: held fire runs Rim Cut, Backhand, Boss Slam and starts over; the other screen sees every step.
        reset();
        var steps = new List<int>();
        for (int f = 0; f < 300 && steps.Count < 4; f++)
        {
            frame(new PlayerInput { Fire = true, AimAt = from + new V2(300f, 0f) }, true);
            int st = TwinBladesMotion.StepOf(me.Combo);
            if (st != 0 && (steps.Count == 0 || steps[steps.Count - 1] != st))
                steps.Add(st);
        }
        var fxSteps = fxSeen.Where(e => e.Kind == FxKind.BulwarkStep).Select(e => (int)Math.Round(e.Radius) % 10).Distinct().OrderBy(x => x);
        play(1.3f, aimRight, true);
        Console.WriteLine("  chain: steps {0}, on the other screen {1}; after a pause the step is {2}", string.Join(",", steps), string.Join(",", fxSteps), TwinBladesMotion.StepOf(me.Combo));
        Check(string.Join(",", steps) == "1,2,3,1" && string.Join(",", fxSteps) == "1,2,3", "the chain runs Rim Cut, Backhand, Boss Slam, then starts over");
        Check(TwinBladesMotion.StepOf(me.Combo) == 0, "the chain starts over after a pause");

        // The Boss Slam: hits harder and stuns.
        reset();
        var slammed = dummy(from + new V2(70f, 0f));
        slammed.FrozenT = 0f; slammed.Speed = 0f;
        float hp0 = slammed.Hp;
        me.Combo = (1 << 3) | 2; me.ComboT = 1f;
        play(0.05f, new PlayerInput { Fire = true, AimAt = from + new V2(300f, 0f) }, true);
        play(0.4f, aimRight, true);
        Console.WriteLine("  slam: took {0:0}, stunned {1:0.00} s", hp0 - slammed.Hp, slammed.FrozenT);
        Check(hp0 - slammed.Hp >= 25f && slammed.FrozenT > 0.1f, "the Boss Slam hits for 26 and stuns");

        // The guard: a shot from the front stops on the plate (red aimed shots too); one from behind gets through it.
        reset();
        var front = fire(from + new V2(160f, 20f), Defs.SmallShot);
        var red = fire(from + new V2(150f, -60f), Defs.PierceShot);
        play(0.6f, aimRight, true);
        int blocks = fxSeen.Count(e => e.Kind == FxKind.BulwarkBlock && e.Pos2.Y < 0.5f);
        fxSeen.Clear();
        var back = fire(from + new V2(-160f, 10f), Defs.SmallShot);
        play(0.6f, aimRight, true);
        int backBlocks = fxSeen.Count(e => e.Kind == FxKind.BulwarkBlock && e.Pos.X < from.X);
        Console.WriteLine("  guard: front and red shots stopped {0}, from behind {1}; still flying {2}", blocks, backBlocks, g.Bullets.Count);
        Check(blocks == 2 && !g.Bullets.ContainsKey(front.Id) && !g.Bullets.ContainsKey(red.Id), "the guard stops shots from the front, red aimed shots too");
        Check(backBlocks == 0 && !g.Bullets.ContainsKey(back.Id), "a shot from behind gets past the shield to you");

        // The Brace: held, you slow down and shots soak into Ram (7 a round, 14 a red one); the other screen sees it.
        reset();
        float walk = me.Speed;
        var brace = new PlayerInput { AimAt = from + new V2(300f, 0f), WeaponAction = true };
        play(0.2f, brace, true);
        float braced = me.Speed;
        fire(from + new V2(160f, 0f), Defs.SmallShot);
        fire(from + new V2(160f, 40f), Defs.SmallShot);
        fire(from + new V2(150f, -50f), Defs.PierceShot);
        play(0.7f, brace, true);
        Console.WriteLine("  brace: speed {0:0} -> {1:0}; Ram {2:0}; the other screen braced {3}, Ram {4:0}", walk, braced, me.BbRam, client.Game.Players[1].BbBrace, client.Game.Players[1].BbRam);
        Check(me.BbBrace && Math.Abs(braced / walk - Game.BraceSlow) < 0.02f, "bracing slows you to 38%");
        Check(Math.Abs(me.BbRam - (2 * Game.BbRamShot + Game.BbRamBig)) < 0.1f, "soaked shots fill Ram: 7 a round, 14 a red one");
        Check(client.Game.Players[1].BbBrace && Math.Abs(client.Game.Players[1].BbRam - me.BbRam) < 0.1f, "the other screen sees the Brace and the Ram");
        play(0.2f, aimRight, true);
        Check(!me.BbBrace, "letting go of the key drops the Brace");
        play(Game.BbRamHold + 0.6f, aimRight, true);
        Console.WriteLine("  Ram after {0:0.0} s out of the Brace: {1:0.0}", Game.BbRamHold + 0.6f, me.BbRam);
        Check(me.BbRam < 2 * Game.BbRamShot + Game.BbRamBig - 3f, "Ram drains once you've been out of the Brace a while");

        // The Shield Bash at full Ram: a long dash that hits what is in its lane hard, stuns it, and throws shards.
        reset();
        var lane = dummy(from + new V2(160f, 10f));
        lane.FrozenT = 0f; lane.Speed = 0f;
        hp0 = lane.Hp;
        play(0.2f, brace, true);
        me.BbRam = Game.BbRamMax;
        int shardsBefore = g.Bullets.Values.Count(b => b.Kind == Defs.SteelShard);
        V2 start = me.Pos;
        int shardsMax = 0;
        frame(new PlayerInput { AimAt = from + new V2(400f, 0f), WeaponAction = true, Fire = true }, false);
        for (int f = 0; f < 60; f++)
        {
            frame(new PlayerInput { AimAt = me.Pos + new V2(400f, 0f) }, false);
            shardsMax = Math.Max(shardsMax, g.Bullets.Values.Count(b => b.Kind == Defs.SteelShard) - shardsBefore);
        }
        float went = me.Pos.X - start.X;
        Console.WriteLine("  full bash: went {0:0} px, hit for {1:0}, stunned {2:0.00} s, {3} shards, Ram {4:0}", went, hp0 - lane.Hp, lane.FrozenT, shardsMax, me.BbRam);
        Check(went > Game.BashLength(Game.BbRamMax) - 30f, "a full-Ram bash dashes about 270 px");
        Check(hp0 - lane.Hp >= Game.BbBashDmg + Game.BbBashDmgPer * Game.BbRamMax - 1f, "it hits what is in its lane for 110 or more");
        Check(shardsMax >= Game.BbShardsMin, "a full-Ram bash throws its soaked shots out as shards");
        Check(me.BbRam == 0f && !me.BbBrace, "the bash spends the Ram and drops the Brace");
        Check(fxSeen.Any(e => e.Kind == FxKind.BulwarkBash), "the other screen sees the bash");

        // A bash with no Ram: allowed, a short shove.
        reset();
        me.Pos = from;
        play(0.2f, brace, true);
        start = me.Pos;
        frame(new PlayerInput { AimAt = from + new V2(400f, 0f), WeaponAction = true, Fire = true }, false);
        for (int f = 0; f < 40; f++)
            frame(new PlayerInput { AimAt = me.Pos + new V2(400f, 0f) }, false);
        went = me.Pos.X - start.X;
        Console.WriteLine("  empty bash: went {0:0} px", went);
        Check(went > Game.BbBashBase - 15f && went < Game.BbBashBase + 25f, "a bash with no Ram is a short 70 px shove");
        me.Pos = from;
    }
}
