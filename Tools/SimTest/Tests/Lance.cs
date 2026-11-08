using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

// The Lance (Lance.cs): the Breach chain (Jab, Second Jab, Wide Sweep, Breaching Thrust), Impact, and Hook & Haul.
static partial class Program
{
    static void LanceTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var volley = typeof(Game).GetMethod("FireVolley", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2 }, port, false, "Tank", Role.Tank, 1, 0, Kit.Knight);
        var client = JoinAs(port, false, "Healer", Role.Healer, 0);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        var ally = g.Players[2];
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        g.Bot = client.Game.Bot = false;
        Check(me.Weapon.Id == WeaponId.Lance, "the Tank Knight's second weapon is the Lance");
        V2 from = new V2(500f, 450f);
        var keep = new List<Enemy>();
        var pins = new Dictionary<Enemy, V2>();
        var heard = new HashSet<Sfx>();
        // A Brute (shoved, not heavy) with endless health; `still`: frozen in place so it doesn't walk at you (pushes still slide it).
        Func<V2, bool, Enemy> dummy = (at, still) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false });
            e.Hp = e.MaxHp = 1e5f; e.SpawnT = 0f; e.FireT = 99f; e.FrozenT = still ? 30f : 0f;
            keep.Add(e);
            return e;
        };
        Action<float, PlayerInput> play = (secs, input) =>
        {
            for (int f = 0; f < secs * 60; f++)
            {
                foreach (var e in g.Enemies.Values) if (!keep.Contains(e)) e.Hp = 0f;
                foreach (var e in keep) { if (pins.ContainsKey(e)) e.Pos = pins[e]; e.FireT = 99f; }
                me.Pos = from;
                ally.Pos = client.Game.Me.Pos = from + new V2(-300f, 200f);
                host.Poll(); g.FixedStep(dt, input);
                foreach (var c in g.Sounds) heard.Add(c.Sfx);
                g.Update(dt);
                client.Poll(); client.Game.FixedStep(dt, new PlayerInput()); client.Game.Update(dt);
            }
        };
        Action reset = () => { foreach (var e in keep) e.Hp = 0f; keep.Clear(); pins.Clear(); g.Bullets.Clear(); me.Combo = 0; me.ComboT = 0f; me.FireCd = 0f; me.LancePrimed = false; play(0.1f, new PlayerInput()); };
        // One step of the chain, aimed along +x: the step before it was `step - 1` a moment ago.
        Action<int> step = n => { me.Combo = n == 1 ? 0 : (1 << 3) | (n - 1); me.ComboT = n == 1 ? 0f : 1f; me.LancePrimed = false; volley.Invoke(g, new object[] { me, 0f, 0 }); };
        play(0.3f, new PlayerInput());

        // The chain: held fire runs Jab, Second Jab, Wide Sweep, Breaching Thrust and starts over, and every screen sees each step.
        var steps = new List<int>();
        var fx = new HashSet<int>();
        for (int f = 0; f < 300 && steps.Count < 5; f++)
        {
            play(1f / 60f, new PlayerInput { Fire = true, AimAt = from + new V2(300f, 0f) });
            foreach (var e in client.Game.Effects) if (e.Kind == FxKind.LanceStep) fx.Add((int)Math.Round(e.Radius) % 10);
            int st = TwinBladesMotion.StepOf(me.Combo);
            if (st != 0 && (steps.Count == 0 || steps[steps.Count - 1] != st))
                steps.Add(st);
        }
        play(1.2f, new PlayerInput { AimAt = from + new V2(300f, 0f) });
        Console.WriteLine("  chain: steps {0}, effects on the client {1}; after a pause the step is {2}", string.Join(",", steps), string.Join(",", fx.OrderBy(x => x)), TwinBladesMotion.StepOf(me.Combo));
        Check(string.Join(",", steps) == "1,2,3,4,1" && fx.SetEquals(new[] { 1, 2, 3, 4 }), "the Lance chains Jab, Second Jab, Wide Sweep and the Breaching Thrust, then starts over");
        Check(TwinBladesMotion.StepOf(me.Combo) == 0, "the chain starts over after a pause");
        reset();

        // A jab: a thin box straight ahead, 14 and a small shove; something off to the side isn't hit.
        var ahead = dummy(from + new V2(120f, 0f), true);
        var beside = dummy(from + new V2(100f, Game.LanceJabHalfWidth + 19f + 20f), true);
        float a0 = ahead.Hp, b0 = beside.Hp, ax = ahead.Pos.X;
        step(1);
        play(0.4f, new PlayerInput());
        Console.WriteLine("  jab: ahead took {0:0} and slid {1:0} px; beside took {2:0}", a0 - ahead.Hp, ahead.Pos.X - ax, b0 - beside.Hp);
        Check(Math.Abs(a0 - ahead.Hp - 14f) < 0.5f && ahead.Pos.X - ax > 20f && ahead.Pos.X - ax < 40f, "a jab hits 14 straight ahead and shoves 30 px");
        Check(beside.Hp == b0, "a jab is a thin box: something beside its line isn't hit");
        reset();

        // The Wide Sweep: everything across the front is hit once, batted along the swing (to your left); nothing behind you.
        var right = dummy(from + V2.FromAngle(0.9f) * 100f, true);
        var left = dummy(from + V2.FromAngle(-0.9f) * 100f, true);
        var behind = dummy(from + new V2(-100f, 0f), true);
        float r0 = right.Hp, l0 = left.Hp, h0 = behind.Hp, ly = left.Pos.Y;
        step(3);
        play(0.6f, new PlayerInput());
        Console.WriteLine("  sweep: right took {0:0}, left took {1:0} and was batted {2:0} px left, behind took {3:0}", r0 - right.Hp, l0 - left.Hp, ly - left.Pos.Y, h0 - behind.Hp);
        Check(Math.Abs(r0 - right.Hp - 16f) < 0.5f && Math.Abs(l0 - left.Hp - 16f) < 0.5f && behind.Hp == h0, "the Wide Sweep hits each enemy across the front once, for 16");
        Check(ly - left.Pos.Y > 40f, "the Wide Sweep bats enemies along its swing, out of your lane");
        reset();

        // The Breaching Thrust: 40, a 160 px shove straight ahead and a 1 s stun.
        var target = dummy(from + new V2(140f, 0f), false);
        float t0 = target.Hp, tx = target.Pos.X, stun = 0f;
        step(4);
        for (int f = 0; f < 42; f++) { play(1f / 60f, new PlayerInput()); stun = Math.Max(stun, target.FrozenT); }
        Console.WriteLine("  breaching thrust: took {0:0}, slid {1:0} px, stunned {2:0.00} s", t0 - target.Hp, target.Pos.X - tx, stun);
        Check(Math.Abs(t0 - target.Hp - 40f) < 0.5f && target.Pos.X - tx > 130f && stun > 0.9f, "the Breaching Thrust hits 40, shoves 160 px and stuns for 1 s");
        reset();

        // Impact: shoved into another enemy, both take 10 and the second slides on; shoved into a wall, 18.
        var front = dummy(from + new V2(110f, 0f), true);
        var back = dummy(from + new V2(250f, 0f), true);
        float f0 = front.Hp, k0 = back.Hp, kx = back.Pos.X;
        step(4);
        play(0.7f, new PlayerInput());
        bool impactSeen = client.Game.Effects.Any(e => e.Kind == FxKind.LanceImpact) || g.Effects.Any(e => e.Kind == FxKind.LanceImpact);
        Console.WriteLine("  impact: the shoved one took {0:0}, the one it hit took {1:0} and slid {2:0} px (effect seen {3})", f0 - front.Hp, k0 - back.Hp, back.Pos.X - kx, impactSeen);
        Check(Math.Abs(f0 - front.Hp - 50f) < 0.5f && Math.Abs(k0 - back.Hp - Game.ImpactDmg) < 0.5f && back.Pos.X - kx > 20f && impactSeen,
            "a shoved enemy that hits another: both take Impact damage, and the one it hits slides on");
        reset();
        V2 nearWall = new V2(g.ArenaMax.X - 260f, 450f);
        from = nearWall;
        var walled = dummy(nearWall + new V2(160f, 0f), true);
        float w0 = walled.Hp;
        step(4);
        play(0.6f, new PlayerInput());
        bool wallSeen = g.Effects.Any(e => e.Kind == FxKind.LanceImpact && Math.Round(e.Pos2.Y) == -1);
        Console.WriteLine("  wall: took {0:0} (wall effect {1})", w0 - walled.Hp, wallSeen);
        Check(Math.Abs(w0 - walled.Hp - 40f - Game.WallImpactDmg) < 0.5f && wallSeen, "a shoved enemy that hits the wall takes 18 (Impact)");
        from = new V2(500f, 450f);
        reset();

        // Hook & Haul: the enemy nearest the cursor is hauled to your tip, hurt and stunned; the next press is the Breaching Thrust.
        var far = dummy(from + new V2(220f, 30f), false);
        float h1 = far.Hp;
        me.LanceHookCd = 0f;
        play(2f / 60f, new PlayerInput { WeaponAction = true, AimAt = far.Pos + new V2(10f, 0f) });
        play(0.45f, new PlayerInput { AimAt = far.Pos });
        float dist = far.Pos.DistanceTo(from), cd = me.LanceHookCd;
        bool primed = me.LancePrimed, clientPrimed = client.Game.Players[1].LancePrimed;
        bool hookSeen = client.Game.Effects.Any(e => e.Kind == FxKind.LanceHook);
        Console.WriteLine("  hook: hauled to {0:0} px, took {1:0}, stunned {2:0.00}, primed {3} (client {4}), cooldown {5:0.0} s, effect seen {6}", dist, h1 - far.Hp, far.FrozenT, primed, clientPrimed, cd, hookSeen);
        Check(Math.Abs(dist - Game.HookHold) < 14f && Math.Abs(h1 - far.Hp - 10f) < 0.5f && far.FrozenT > 0.3f, "Hook & Haul hauls the enemy nearest your cursor to your tip, hurt and stunned");
        Check(primed && clientPrimed && cd > 0f && hookSeen, "the hook primes the Breaching Thrust (every screen sees it) and has a cooldown");
        play(2f / 60f, new PlayerInput { Fire = true, AimAt = from + new V2(300f, 0f) });
        Console.WriteLine("  after the hook the next press is step {0}", TwinBladesMotion.StepOf(me.Combo));
        Check(TwinBladesMotion.StepOf(me.Combo) == 4 && !me.LancePrimed, "after a hook, the next press is the Breaching Thrust");
        play(0.8f, new PlayerInput());
        reset();

        // Its own sound bank (LanceSoundBank.cs), and none of the generic swing whooshes.
        var want = new[] { Sfx.LanceJab, Sfx.LanceSweep, Sfx.LanceBreach, Sfx.LanceHitSfx, Sfx.LanceHitHeavy, Sfx.LanceImpactSfx, Sfx.LanceWall, Sfx.LanceHookOut, Sfx.LanceHaul };
        var missing = want.Where(x => !heard.Contains(x)).ToList();
        Console.WriteLine("  lance sounds missing: {0}", missing.Count == 0 ? "none" : string.Join(", ", missing));
        Check(missing.Count == 0, "every Lance sound is raised: a jab, the sweep, the Breaching Thrust, a hit and a heavy one, Impact, a wall, the hook and its haul");
        Check(!heard.Contains(Sfx.SwingLight) && !heard.Contains(Sfx.SwingHeavy), "the Lance no longer uses the generic swing sounds");
        host.Leave(""); client.Leave("");
    }
}
