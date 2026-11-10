using System;
using System.Collections.Generic;
using Arena;

// The Arc Knuckles (Core/ArcKnuckles.cs, Docs/ArcKnuckles.md): the Knight DPS's fourth weapon, its five-punch chain, the parry
// cancel and the perfect parry's Counter Hammer.
static partial class Program
{
    static void ArcKnucklesTest(float dt)
    {
        // The lobby: the Knight DPS has a fourth weapon; everyone else keeps three.
        Check(Defs.WeaponCount(Role.Dps, Kit.Knight) == 4 && Defs.Weapon(Role.Dps, Kit.Knight, 3).Id == WeaponId.ArcKnuckles,
            "the Knight DPS's fourth weapon key is the Arc Knuckles");
        Check(Defs.WeaponCount(Role.Tank, Kit.Knight) == 3 && Defs.WeaponCount(Role.Dps, Kit.Soldier) == 3 && Defs.Weapon(Role.Dps, Kit.Soldier, 3) == Defs.Weapon(Role.Dps, Kit.Soldier, 0),
            "other roles and classes keep three (a fourth pick they don't have is their first weapon)");
        Check(Defs.WeaponOf(WeaponId.ArcKnuckles).Name == "Arc Knuckles", "the Arc Knuckles have their definition");

        var s = StartBuffed(dt, 1, 0);
        s[0].Game.Bot = s[1].Game.Bot = false;
        Run(s, dt, 3, () => s[0].Game.State == MatchState.Fighting);
        var g = s[0].Game;
        var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", bf);
        var shoot = typeof(Game).GetMethod("SpawnBullet", bf);
        var queue = (List<EnemyType>)typeof(Game).GetField("spawnQueue", bf).GetValue(g);
        var me = g.Players[1];
        me.Weapon = Defs.Weapon(Role.Dps, Kit.Knight, 3);
        me.Hp = me.MaxHp;
        V2 from = new V2(500f, 450f), aimAt = new V2(800f, 450f);
        var pinned = new List<KeyValuePair<Enemy, V2>>();
        Func<V2, Enemy> Dummy = at =>
        {
            var d = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, at, 0f, 0, false });
            d.MaxHp = d.Hp = 1e5f; d.Speed = 0f; d.FireT = 99f; d.SpawnT = 0f;
            pinned.Add(new KeyValuePair<Enemy, V2>(d, at));
            return d;
        };
        var heard = new HashSet<Sfx>();
        bool clientSawPunch = false;
        Action<int, bool, bool> Step = (frames, fire, parry) =>
        {
            for (int f = 0; f < frames; f++)
            {
                me.Pos = from; queue.Clear();
                foreach (var kv in pinned) kv.Key.Pos = kv.Value;
                foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == s[0] ? new PlayerInput { AimAt = aimAt, Fire = fire && f < 2, Parry = parry } : new PlayerInput()); }
                foreach (var c in g.Sounds) heard.Add(c.Sfx);
                g.Sounds.Clear();
                foreach (var fx in s[1].Game.Effects)
                    clientSawPunch |= fx.Kind == FxKind.KnuckleStep && (int)Math.Round(fx.Radius) / 10 == 1;
            }
        };
        queue.Clear(); g.Enemies.Clear(); g.Bullets.Clear();
        float baseDmg = me.Weapon.Damage * me.Stats.DmgMult;
        var dummy = Dummy(new V2(560f, 452f));
        Func<float> Taken = () => { float t = dummy.MaxHp - dummy.Hp; dummy.Hp = dummy.MaxHp; return t; };

        // The chain: each press throws the next punch, and each lands for its share of the weapon's damage.
        var got = new float[5];
        bool stepsOk = true, windups = true;
        for (int k = 0; k < 5; k++)
        {
            Step(2, true, false);
            windups &= Taken() == 0f;  // (nothing lands in the first frames: every punch winds up)
            stepsOk &= Game.KnuckleStepOf(me.Combo) == k + 1;
            Step(28, false, false);
            got[k] = Taken();
            if (k == 3)
                Check(dummy.FrozenT > 0f, "the Uppercut launches what it hits (stunned for a moment)");
        }
        Check(stepsOk, "five presses throw Jab, Cross, Hook, Uppercut and Twin Hammer in order");
        Check(windups, "every punch winds up before it lands");
        bool dmgOk = true;
        for (int k = 0; k < 5; k++)
            dmgOk &= Math.Abs(got[k] - baseDmg * Game.KnuckleMult[k]) < 0.6f;
        Console.WriteLine("  arc knuckles chain: {0:0} / {1:0} / {2:0} / {3:0} / {4:0}", got[0], got[1], got[2], got[3], got[4]);
        Check(dmgOk, "each punch lands for its own damage (12, 14, 16, 18, 30 before upgrades)");
        Check(heard.Contains(Sfx.KnuckleJab) && heard.Contains(Sfx.KnuckleHook) && heard.Contains(Sfx.KnuckleUpper) && heard.Contains(Sfx.KnuckleSlam),
            "each punch has its sound, and the Twin Hammer slams");
        Check(clientSawPunch, "the other player sees the punches");

        // The Twin Hammer's shockwave catches a crowd round the point ahead of you.
        var side = Dummy(new V2(560f, 530f));
        Step(80, false, false);
        Check(Game.KnuckleStepOf(me.Combo) == 0, "the chain starts over after 1.2 s without a punch");
        for (int k = 0; k < 4; k++) { Step(2, true, false); Step(28, false, false); }
        side.Hp = side.MaxHp;
        Step(2, true, false);
        Step(28, false, false);
        Check(side.Hp < side.MaxHp && side.FrozenT > 0f, "the Twin Hammer's shockwave hits and stuns an enemy off to the side");
        Taken();
        Step(80, false, false);

        // The parry cancels a punch still winding up and starts the chain over.
        Step(2, true, false);
        Step(28, false, false);
        Taken();
        Step(2, true, false);   // the Cross, winding up...
        Step(10, false, true);  // ...the parry goes up before it lands
        Step(40, false, false);
        Check(Taken() == 0f, "raising the parry cancels a punch that hasn't landed");
        Check(Game.KnuckleStepOf(me.Combo) == 0, "and sends the chain back to the Jab");
        Step(2, true, false);
        Step(28, false, false);
        Check(Game.KnuckleStepOf(me.Combo) == 1 && Math.Abs(Taken() - baseDmg) < 0.6f, "so the next press is the Jab");
        Step(80, false, false);

        // A perfect parry arms the Counter Hammer: the next press, from any step.
        Step(2, true, false);
        Step(28, false, false);
        Taken();
        Step(40, false, false);  // (the parry has to have been down a moment)
        shoot.Invoke(g, new object[] { Defs.SmallShot, from + new V2(50f, 34f), new V2(-330f, -224f), 0f, 2f, 10f, 0, false, 0f });  // (off the dummy's line, so the needle sent back misses it)
        Step(12, false, true);
        Check(me.KnuckleArmed, "a perfect parry arms the Counter Hammer");
        Check(heard.Contains(Sfx.KnuckleArm), "and the coils are heard charging");
        Step(10, false, false);
        Check(s[1].Game.Players[1].KnuckleArmed, "the other player sees it armed (the pips go gold)");
        Step(2, true, false);
        Check(Game.KnuckleStepOf(me.Combo) == 6 && !me.KnuckleArmed, "the next press is the Counter Hammer");
        Step(28, false, false);
        float counter = Taken();
        Check(Math.Abs(counter - baseDmg * Game.KnuckleMult[5]) < 0.6f && counter > got[4], "it lands harder than the Twin Hammer (" + counter + " vs " + got[4] + ")");
        Check(heard.Contains(Sfx.KnuckleCounter), "with its own sound");
        Step(12, false, false);  // (its 0.62 s before the next press counts)
        Step(2, true, false);
        Check(Game.KnuckleStepOf(me.Combo) == 1, "and the chain starts again from the Jab");
        Step(80, false, false);

        // An armed Counter Hammer fades unused.
        Step(40, false, false);
        shoot.Invoke(g, new object[] { Defs.SmallShot, from + new V2(50f, 34f), new V2(-330f, -224f), 0f, 2f, 10f, 0, false, 0f });  // (off the dummy's line, so the needle sent back misses it)
        Step(12, false, true);
        bool armed = me.KnuckleArmed;
        Step((int)((Game.KnuckleArmTime + 0.3f) / dt), false, false);
        Check(armed && !me.KnuckleArmed, "an unused Counter Hammer fades after 3 s");

        s[0].Leave(""); s[1].Leave("");
    }
}
