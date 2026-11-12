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
        Check(Defs.WeaponCount(Role.Tank, Kit.Knight) == 3 && Defs.WeaponCount(Role.Tank, Kit.Soldier) == 3 && Defs.Weapon(Role.Tank, Kit.Soldier, 3) == Defs.Weapon(Role.Tank, Kit.Soldier, 0),
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
        Check(dmgOk, "each punch lands for its own damage (22, 26, 29, 33, 55 before upgrades)");
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

        // The Rocket Dash (the weapon-action key): it locks onto the enemy nearest your cursor and flies you up to it, untouchable;
        // that enemy takes the hit and a short shove, even with another in the way. With nothing in reach it flies 130 px along your aim.
        Step(80, false, false);
        g.Bullets.Clear();
        foreach (var kv in pinned) kv.Key.Pos = new V2(150f, 120f);
        pinned.Clear();
        var front = Dummy(new V2(600f, 450f));   // in the way along your aim
        var locked = Dummy(new V2(700f, 470f));  // nearest the cursor
        pinned.Clear();  // (they move: the shove)
        V2 lockedAt0 = locked.Pos;
        V2 start = from;
        me.Pos = start;
        bool clientSawDash = false, untouchable = false, clientSawHit = false;
        V2 cursor = locked.Pos + new V2(10f, 5f);
        Action<int, bool> Fly = (frames, alt) =>
        {
            for (int f = 0; f < frames; f++)
            {
                queue.Clear();
                foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == s[0] ? new PlayerInput { AimAt = cursor, WeaponAction = alt && f < 2 } : new PlayerInput()); }
                foreach (var c in g.Sounds) heard.Add(c.Sfx);
                g.Sounds.Clear();
                untouchable |= me.InvulnT > 0f;
                foreach (var fx in s[1].Game.Effects)
                {
                    clientSawDash |= fx.Kind == FxKind.KnuckleDash && (int)Math.Round(fx.Radius) == 1;
                    clientSawHit |= fx.Kind == FxKind.KnuckleHit && (int)fx.Pos2.X == 1 && (int)fx.Pos2.Y == 7;
                }
            }
        };
        front.Hp = front.MaxHp; locked.Hp = locked.MaxHp;
        Fly(2, true);
        Fly(28, false);
        float frontTook = front.MaxHp - front.Hp, lockedTook = locked.MaxHp - locked.Hp, gap = me.Pos.DistanceTo(locked.Pos);
        Console.WriteLine("  rocket dash: ended {0:0} px from the locked enemy, it took {1:0}, the one in the way {2:0}", gap, lockedTook, frontTook);
        Check(me.KnuckleTarget == locked.Id && s[1].Game.Players[1].KnuckleTarget == locked.Id, "the Rocket Dash locks onto the enemy nearest your cursor (the other player's game knows it)");
        Check(gap < locked.Radius + me.Def.BodyR + Game.KnuckleDashKnock + 20f, "and flies you right up to it (" + gap + " px)");
        Check(untouchable, "you're untouchable while you fly");
        Check(Math.Abs(lockedTook - me.Weapon.Damage * Game.KnuckleDashMult * me.Stats.DmgMult) < 0.6f && frontTook == 0f, "the locked enemy takes the hit, the one in the way nothing");
        Check(locked.Pos.DistanceTo(lockedAt0) > 5f && locked.Pos.DistanceTo(lockedAt0) < Game.KnuckleDashKnock + 10f, "and it's shoved a short way, still in reach");
        Check(heard.Contains(Sfx.KnuckleRocket) && heard.Contains(Sfx.KnuckleImpactHeavy), "the thruster and the hit are heard");
        Check(clientSawDash && clientSawHit, "the other player sees the dash and the hit (sparks; your own game holds and shakes)");
        V2 after = me.Pos;
        locked.Hp = locked.MaxHp;
        Fly(2, true);
        Fly(20, false);
        Check(me.Pos.DistanceTo(after) < 2f && locked.Hp == locked.MaxHp, "it can't dash again until its 3 s cooldown is up");
        Fly((int)(Game.KnuckleDashCooldown / dt), false);
        foreach (var en in new List<Enemy>(g.Enemies.Values)) en.Pos = new V2(150f, 120f);
        after = me.Pos = new V2(500f, 700f);
        cursor = me.Pos + new V2(300f, 0f);
        Fly(2, true);
        Fly(20, false);
        float free = me.Pos.DistanceTo(after);
        Check(Math.Abs(free - Game.KnuckleDashLen) < 12f, "with nothing in reach it flies 130 px along your aim (" + free + "; dashes move in whole 1/60 s frames)");

        // The dash chain: a dash that hits opens a window; Fire throws the Flurry where you landed, then Grab & Throw (the Flurry's
        // target thrown into the next enemy), then the Rocket Punch down the line.
        Fly((int)(Game.KnuckleDashCooldown / dt) + 10, false);
        foreach (var en in new List<Enemy>(g.Enemies.Values)) en.Pos = new V2(150f, 120f);
        V2 S = new V2(400f, 450f);
        me.Pos = S;
        var tgt = Dummy(new V2(S.X + 150f, S.Y));
        var next = Dummy(new V2(S.X + 300f, S.Y + 4f));
        pinned.Clear();
        tgt.Hp = tgt.MaxHp; next.Hp = next.MaxHp;
        bool movedWhileRooted = false, clientSawChain = false;
        Action<int, bool, bool, V2> Do = (frames, fire, alt, move) =>
        {
            for (int f = 0; f < frames; f++)
            {
                queue.Clear();
                V2 before = me.Pos;
                bool rooted = me.KnuckleChain == 2 || me.KnuckleChain == 4;
                foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == s[0] ? new PlayerInput { AimAt = alt ? tgt.Pos : me.Pos + new V2(300f, 0f), Fire = fire && f < 2, WeaponAction = alt && f < 2, Move = move } : new PlayerInput()); }  // (the dash's cursor on the target: it locks on)
                foreach (var c in g.Sounds) heard.Add(c.Sfx);
                g.Sounds.Clear();
                if (rooted && me.KnuckleChain == (rooted ? me.KnuckleChain : 0) && me.Pos.DistanceTo(before) > 0.5f) movedWhileRooted = true;
                foreach (var fx in s[1].Game.Effects)
                    clientSawChain |= fx.Kind == FxKind.KnuckleChain && (int)Math.Round(fx.Radius) / 10 == 1;
            }
        };
        Func<Enemy, float> Took = en => { float t = en.MaxHp - en.Hp; en.Hp = en.MaxHp; return t; };
        Do(2, false, true, V2.Zero);
        Do(12, false, false, V2.Zero);
        Took(tgt);
        Check(me.KnuckleChain == 1, "a Rocket Dash that hits opens the chain's window (" + me.KnuckleChain + ")");
        Check(s[1].Game.Players[1].KnuckleChain == 1, "the other player's game knows (the Fire prompt shows)");
        Do(2, true, false, V2.Zero);
        Check(me.KnuckleChain == 2, "Fire throws the Flurry");
        Do(10, false, false, new V2(0f, 1f));  // (trying to walk away)
        Do(38, false, false, V2.Zero);
        float flurry = Took(tgt);
        Check(!movedWhileRooted, "you're rooted while the Flurry runs");
        Check(Math.Abs(flurry - baseDmg * (Game.FlurryJabs * Game.FlurryJabMult + Game.FlurryStraightMult)) < 1f, "eight jabs and a straight land on the target (" + flurry + ")");
        Check(me.KnuckleChain == 3, "the straight opens the grab's window");
        Do(2, true, false, V2.Zero);
        Check(me.KnuckleChain == 4 && heard.Contains(Sfx.KnuckleGrab), "Fire grabs the Flurry's target");
        float nearAt = -1f;
        for (int f = 0; f < 70 && me.KnuckleChain == 4; f++)
        {
            Do(1, false, false, V2.Zero);
            if (f == 25) nearAt = tgt.Pos.DistanceTo(me.Pos);
        }
        Check(nearAt > 0f && nearAt < 60f, "it's held right beside you as you swing it round (" + nearAt + " px)");
        float thrown = Took(tgt), hitBy = Took(next);
        Check(heard.Contains(Sfx.KnuckleThrow), "the throw is heard");
        Check(Math.Abs(thrown - baseDmg * Game.KnuckleThrowMult) < 1f && Math.Abs(hitBy - baseDmg * Game.KnuckleThrowHitMult) < 1f,
            "the thrown enemy takes 20 and the one it hits 26 (" + thrown + ", " + hitBy + ")");
        Check(next.FrozenT > 0f, "and the one it hits is stunned");
        Check(me.KnuckleChain == 5, "the landing opens the Rocket Punch's window");
        Do(2, true, false, V2.Zero);
        Check(me.KnuckleChain == 6, "Fire throws the Rocket Punch");
        Do(25, false, false, V2.Zero);
        float p1 = Took(tgt), p2 = Took(next);
        Check(Math.Abs(p1 - baseDmg * Game.RocketPunchMult) < 1f && Math.Abs(p2 - baseDmg * Game.RocketPunchMult) < 1f, "the Rocket Punch hits both down the line (" + p1 + ", " + p2 + ")");
        Do(50, false, false, V2.Zero);
        Check(me.KnuckleChain == 0, "and the chain is over when the fist is back");
        Check(clientSawChain, "the other player sees the chain's moves");

        // A window you miss closes, and the parry ends the chain.
        Do((int)(Game.KnuckleDashCooldown / dt), false, false, V2.Zero);
        foreach (var en in new List<Enemy>(g.Enemies.Values)) en.Pos = new V2(150f, 120f);
        me.Pos = S;
        tgt.Pos = new V2(S.X + 150f, S.Y);
        Do(2, false, true, V2.Zero);
        Do(14, false, false, V2.Zero);
        bool opened = me.KnuckleChain == 1;
        Do(40, false, false, V2.Zero);
        Check(opened && me.KnuckleChain == 0, "a missed window closes the chain");
        Do((int)(Game.KnuckleDashCooldown / dt), false, false, V2.Zero);
        me.Pos = S;
        tgt.Pos = new V2(S.X + 150f, S.Y);
        Do(2, false, true, V2.Zero);
        Do(14, false, false, V2.Zero);
        Do(2, true, false, V2.Zero);
        bool flurrying = me.KnuckleChain == 2;
        for (int f = 0; f < 6; f++)
        {
            queue.Clear();
            foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == s[0] ? new PlayerInput { AimAt = me.Pos + new V2(300f, 0f), Parry = true } : new PlayerInput()); }
        }
        Check(flurrying && me.KnuckleChain == 0, "raising the parry ends the chain");

        s[0].Leave(""); s[1].Leave("");
    }
}
