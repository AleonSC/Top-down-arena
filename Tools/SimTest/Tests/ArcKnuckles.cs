using System;
using System.Collections.Generic;
using Arena;

// The Arc Knuckles (Core/ArcKnuckles.cs, ArcKnucklesBrawl.cs, ArcKnucklesChain.cs; Docs/ArcKnuckles.md): the Knight DPS's fourth weapon.
// The Brawl stance's eight moves steered by the click wheel, the Wall Slam, the parry reset, the Counter Hammer, the stance swap and
// the Rocket stance's chain with no windows.
static partial class Program
{
    static void ArcKnucklesTest(float dt)
    {
        // The lobby: the Knight DPS has a fourth weapon; roles without one keep three.
        Check(Defs.WeaponCount(Role.Dps, Kit.Knight) == 4 && Defs.Weapon(Role.Dps, Kit.Knight, 3).Id == WeaponId.ArcKnuckles,
            "the Knight DPS's fourth weapon key is the Arc Knuckles");
        Check(Defs.WeaponCount(Role.Healer, Kit.Summoner) == 3 && Defs.Weapon(Role.Healer, Kit.Summoner, 3) == Defs.Weapon(Role.Healer, Kit.Summoner, 0) && Defs.WeaponCount(Role.Healer, Kit.Ninja) == 4 && Defs.WeaponCount(Role.Healer, Kit.Elementalist) == 4,  // (the Healer Elementalist got Static Shock as its 4th)
            "roles without a 4th weapon keep three (a fourth pick they don't have is their first weapon)");
        Check(Defs.WeaponOf(WeaponId.ArcKnuckles).Name == "Arc Knuckles", "the Arc Knuckles have their definition");
        V2 sd;
        bool decodes = Game.KnuckleSteerDir(Game.KnuckleSteerCode(-MathX.Pi / 2f), out sd) && sd.DistanceTo(new V2(0f, -1f)) < 0.03f && !Game.KnuckleSteerDir(0, out sd);
        Check(decodes, "the click wheel's steer byte carries a direction (0: none)");

        var s = StartBuffed(dt, 1, 0);
        s[0].Game.Bot = s[1].Game.Bot = false;
        Run(s, dt, 3, () => s[0].Game.State == MatchState.Fighting);
        var g = s[0].Game;
        var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", bf);
        var shoot = typeof(Game).GetMethod("SpawnBullet", bf);
        var clampArena = typeof(Game).GetMethod("ClampToArena", bf);
        var queue = (List<EnemyType>)typeof(Game).GetField("spawnQueue", bf).GetValue(g);
        var me = g.Players[1];
        me.Weapon = Defs.Weapon(Role.Dps, Kit.Knight, 3);
        me.Hp = me.MaxHp;
        float baseDmg = me.Weapon.Damage * me.Stats.DmgMult;
        Func<V2, Enemy> Dummy = at =>
        {
            var d = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, at, 0f, 0, false });
            d.MaxHp = d.Hp = 1e5f; d.Speed = 0f; d.FireT = 99f; d.SpawnT = 0f;
            return d;
        };
        Func<Enemy, float> Took = en => { float t = en.MaxHp - en.Hp; en.Hp = en.MaxHp; return t; };
        Action Clear = () => { foreach (var en in new List<Enemy>(g.Enemies.Values)) g.Enemies.Remove(en.Id); g.Bullets.Clear(); };
        var heard = new HashSet<Sfx>();
        bool untouchable = false, movedWhileRooted = false, clientSawPunch = false;
        // frames of input: Fire held for the first `hold` frames (2: a click), the wheel's steer, the parry, the weapon-action key
        Action<int, int, V2, byte, bool, bool> Step = (frames, hold, cursor, steer, parry, alt) =>
        {
            for (int f = 0; f < frames; f++)
            {
                queue.Clear();
                V2 before = me.Pos;
                bool rooted = me.KnuckleRooted;
                foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == s[0] ? new PlayerInput { AimAt = cursor, Fire = f < hold, Steer = steer, Parry = parry, WeaponAction = alt && f < 2 } : new PlayerInput()); }
                foreach (var c in g.Sounds) heard.Add(c.Sfx);
                g.Sounds.Clear();
                untouchable |= me.InvulnT > 0f;
                if (rooted && me.KnuckleRooted && me.Pos.DistanceTo(before) > 0.5f) movedWhileRooted = true;
                foreach (var fx in s[1].Game.Effects)
                    clientSawPunch |= fx.Kind == FxKind.KnuckleStep && (int)Math.Round(fx.Radius) / 10 == 1;
            }
        };
        byte Up = Game.KnuckleSteerCode(-MathX.Pi / 2f), Right = Game.KnuckleSteerCode(0f);
        queue.Clear(); Clear();
        Check(me.KnuckleStance == 0 && me.KnuckleMove == 0, "the Arc Knuckles start in the Brawl stance, at the Pull Punch");

        // The Pull Punch: a click on an enemy in range pulls you in and punches it; the group round it is stunned a moment.
        V2 S = new V2(500f, 500f);
        me.Pos = S;
        var a = Dummy(new V2(S.X + 200f, S.Y));
        var b = Dummy(new V2(S.X + 200f, S.Y + 34f));
        Step(2, 2, me.Pos + new V2(400f, -200f), 0, false, false);  // (the cursor on nothing)
        Step(20, 0, me.Pos + new V2(400f, -200f), 0, false, false);
        Check(me.KnuckleMove == 0 && Took(a) == 0f, "a click with no enemy under the cursor swings at the air and keeps your place");
        Step(2, 2, a.Pos, 0, false, false);
        Check(me.KnuckleMove == 1, "a click on an enemy throws the Pull Punch (the wheel opens: the Drive Punch is next)");
        Step(30, 0, a.Pos, 0, false, false);
        float gap = me.Pos.DistanceTo(a.Pos) - a.Radius - me.Def.BodyR, pulled = Took(a);
        Console.WriteLine("  pull punch: {0:0} px left to the enemy, {1:0} damage", gap, pulled);
        Check(gap < 20f, "it pulls you right up to the enemy (" + gap + " px)");
        Check(Math.Abs(pulled - baseDmg * Game.KnucklePullMult) < 0.6f && Took(b) > 0f, "it lands for 80% and hits the one beside it too");
        Check(a.StunT > 0f && b.StunT > 0f, "the group is stunned a moment");
        Check(s[1].Game.Players[1].KnuckleMove == 1, "the other player's game knows the move (the pips)");

        // The Drive Punch: steered up on the wheel, the enemy nearest you and the one beside it fly up.
        V2 a0 = a.Pos, b0 = b.Pos;
        Step(2, 2, a.Pos, Up, false, false);
        Step(30, 0, a.Pos, Up, false, false);
        float dA = a.Pos.Y - a0.Y, dB = b.Pos.Y - b0.Y, drive = Took(a);
        Console.WriteLine("  drive punch up: {0:0} / {1:0} px, {2:0} damage", dA, dB, drive);
        Check(dA < -120f && dB < -120f && Math.Abs(a.Pos.X - a0.X) < 40f, "a Drive Punch steered up sends the group up (" + dA + ", " + dB + ")");
        Check(Math.Abs(drive - baseDmg * Game.KnuckleDriveMult) < 0.6f, "for the weapon's damage");
        Check(me.KnuckleMove == 2, "a Slip Dash is next");

        // The Slip Dash: steered right, you dash 130 px right, untouchable.
        untouchable = false;
        V2 p0 = me.Pos;
        Step(2, 2, me.Pos + new V2(0f, 300f), Right, false, false);  // (the cursor below: the steer wins)
        Step(25, 0, me.Pos, 0, false, false);
        float dx = me.Pos.X - p0.X, dy = me.Pos.Y - p0.Y;
        Check(Math.Abs(dx - Game.KnuckleSlipLen) < 14f && Math.Abs(dy) < 8f && untouchable, "a Slip Dash steered right moves you 130 px right, untouchable (" + dx + ", " + dy + ")");
        Check(heard.Contains(Sfx.KnuckleRocket), "with the thruster's sound");

        // With no steer (the wheel's centre) a Drive Punch carries on along you to the enemy; then a dash at the cursor.
        Clear();
        me.Pos = S;
        var c1 = Dummy(new V2(S.X + 70f, S.Y));
        Step(2, 2, c1.Pos, 0, false, false);  // Drive Punch (the second)
        Step(30, 0, c1.Pos, 0, false, false);
        Check(c1.Pos.X - (S.X + 70f) > 120f && Math.Abs(Took(c1) - baseDmg * Game.KnuckleDrive2Mult) < 0.6f, "unsteered, the second Drive Punch sends it on away from you, for 115%");
        p0 = me.Pos;
        Step(2, 2, me.Pos + new V2(0f, 300f), 0, false, false);
        Step(25, 0, me.Pos, 0, false, false);
        Check(me.Pos.Y - p0.Y > 110f, "unsteered, a Slip Dash goes at the cursor");
        Check(me.KnuckleMove == 5, "the Pin Punch is next");

        // The Pin Punch stuns the group and stops it dead; the Barrage roots you and keeps the stun up; the Quake Punch charges.
        Clear();
        me.Pos = S;
        var pa = Dummy(new V2(S.X + 70f, S.Y));
        var pb = Dummy(new V2(S.X + 110f, S.Y + 30f));
        Step(2, 2, pa.Pos, Right, false, false);
        Step(16, 0, pa.Pos, 0, false, false);
        Check(pa.StunT > 1.2f && pb.StunT > 1.2f && pb.Pos.DistanceTo(new V2(S.X + 110f, S.Y + 30f)) < 4f, "the Pin Punch stuns the group for long and leaves it where it is");
        Check(Math.Abs(Took(pa) - baseDmg * Game.KnucklePinMult) < 0.6f, "for 160%");
        Took(pb);
        movedWhileRooted = false;
        Step(2, 2, pa.Pos, 0, false, false);
        Step(16, 0, pa.Pos, 0, false, false);  // (clicked during the Pin Punch: it waits, then goes)
        bool rootedNow = me.KnuckleRooted;
        bool clientRooted = s[1].Game.Players[1].KnuckleRooted;
        for (int f = 0; f < 45; f++)
        {
            queue.Clear();
            V2 before = me.Pos;
            bool r = me.KnuckleRooted;
            foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == s[0] ? new PlayerInput { AimAt = pa.Pos, Move = new V2(0f, 1f) } : new PlayerInput()); }
            if (r && me.KnuckleRooted && me.Pos.DistanceTo(before) > 0.5f) movedWhileRooted = true;
        }
        float barrage = Took(pa);
        Console.WriteLine("  barrage: {0:0} damage, the pinned enemy still stunned {1:0.00} s", barrage, pa.StunT);
        Check(rootedNow && clientRooted && !movedWhileRooted, "the Barrage roots you (the other player's game too)");
        Check(Math.Abs(barrage - baseDmg * (Game.FlurryJabs * Game.KnuckleBarrageMult + Game.KnuckleBarrageStraight)) < 1f, "eight jabs and a straight land on the pinned group");
        Check(pa.StunT > 0f, "and the stun is still holding when it ends");
        Check(me.KnuckleMove == 7, "the Quake Punch is next");
        Clear();
        me.Pos = S;
        var qa = Dummy(new V2(S.X + 150f, S.Y));
        Step((int)(1.45f / dt), 999, qa.Pos, 0, false, false);
        Check(me.KnuckleCharging && me.KnuckleCharge == 255, "holding Fire charges the Quake Punch (the ring fills)");
        V2 q0 = qa.Pos;
        Step(30, 0, qa.Pos, 0, false, false);
        float quake = Took(qa);
        Console.WriteLine("  quake punch (full charge): {0:0} damage, thrown {1:0} px", quake, qa.Pos.DistanceTo(q0));
        Check(Math.Abs(quake - baseDmg * Game.KnuckleQuakeMult1) < 1f && qa.Pos.DistanceTo(q0) > 100f, "let go at full charge it hits 150 px out for 300% and throws it back");
        Check(heard.Contains(Sfx.KnuckleSlam), "with the slam's sound");
        Check(me.KnuckleMove == 0 && !me.KnuckleCharging, "and the combo starts over at the Pull Punch");
        Check(clientSawPunch, "the other player sees the punches");

        // A Drive Punch into the arena's wall: a Wall Slam on top.
        Clear();
        Step(6, 0, S, 0, true, false);  // (the parry: from the Pull Punch)
        Step(30, 0, S, 0, false, false);
        float edge = 0f;
        for (float x = Game.Width / 2f; x < Game.Width * 2f; x += 10f)
            if (((V2)clampArena.Invoke(g, new object[] { new V2(x, S.Y), 14f })).X < x - 1f) { edge = x; break; }
        Check(edge > 0f, "the arena has a wall to slam into");
        me.Pos = new V2(edge - 160f, S.Y);
        var w1 = Dummy(new V2(edge - 100f, S.Y));
        Step(2, 2, w1.Pos, 0, false, false);  // Pull Punch
        Step(30, 0, w1.Pos, 0, false, false);
        Took(w1);
        Step(2, 2, w1.Pos, Right, false, false);  // Drive Punch, into the wall
        Step(30, 0, w1.Pos, 0, false, false);
        float slammed = Took(w1);
        Console.WriteLine("  wall slam: {0:0} damage in all (the punch {1:0})", slammed, baseDmg);
        Check(slammed >= baseDmg * (Game.KnuckleDriveMult + Game.KnuckleSlamMult) - 1f && w1.StunT > 0f, "driven into the wall it takes a Wall Slam too, and is stunned");

        // Too long without a move and the combo starts over; the parry starts it over too.
        Clear();
        Step(6, 0, S, 0, true, false);  // (the parry: from the Pull Punch)
        Step(30, 0, S, 0, false, false);
        me.Pos = S;
        var r1 = Dummy(new V2(S.X + 150f, S.Y));
        Step(2, 2, r1.Pos, 0, false, false);
        Step(10, 0, r1.Pos, 0, false, false);
        bool opened = me.KnuckleMove == 1;
        Step((int)((Game.KnuckleBrawlIdle + 0.4f) / dt), 0, r1.Pos, 0, false, false);
        Check(opened && me.KnuckleMove == 0, "1.6 s without a move and the combo starts over (the wheel closes)");
        me.Pos = S;
        r1.Pos = new V2(S.X + 150f, S.Y);
        Step(2, 2, r1.Pos, 0, false, false);
        Step(10, 0, r1.Pos, 0, false, false);
        Step(6, 0, r1.Pos, 0, true, false);
        Check(me.KnuckleMove == 0, "raising the parry starts it over");
        Step(30, 0, r1.Pos, 0, false, false);

        // A perfect parry arms the Counter Hammer: the next click is the gold slam.
        Clear();
        V2 from = new V2(500f, 450f);
        Action<int, bool, bool> Pinned = (frames, fire, parry) =>
        {
            for (int f = 0; f < frames; f++)
            {
                me.Pos = from; queue.Clear();
                foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == s[0] ? new PlayerInput { AimAt = new V2(800f, 450f), Fire = fire && f < 2, Parry = parry } : new PlayerInput()); }
                foreach (var c in g.Sounds) heard.Add(c.Sfx);
                g.Sounds.Clear();
            }
        };
        var hd = Dummy(new V2(560f, 452f));
        Pinned(40, false, false);
        shoot.Invoke(g, new object[] { Defs.SmallShot, from + new V2(50f, 34f), new V2(-330f, -224f), 0f, 2f, 10f, 0, false, 0f });  // (off the dummy's line)
        Pinned(12, false, true);
        Check(me.KnuckleArmed && heard.Contains(Sfx.KnuckleArm), "a perfect parry arms the Counter Hammer (the coils are heard charging)");
        Pinned(10, false, false);
        Check(s[1].Game.Players[1].KnuckleArmed, "the other player sees it armed (the pips go gold)");
        Took(hd);
        Pinned(2, true, false);
        Check(!me.KnuckleArmed, "the next click is the Counter Hammer");
        Pinned(30, false, false);
        float counter = Took(hd);
        Check(Math.Abs(counter - baseDmg * Game.KnuckleCounterMult) < 0.6f && heard.Contains(Sfx.KnuckleCounter), "it lands for 317% with its own sound (" + counter + ")");
        Pinned(40, false, false);
        shoot.Invoke(g, new object[] { Defs.SmallShot, from + new V2(50f, 34f), new V2(-330f, -224f), 0f, 2f, 10f, 0, false, 0f });
        Pinned(12, false, true);
        bool armed = me.KnuckleArmed;
        Pinned((int)((Game.KnuckleArmTime + 0.3f) / dt), false, false);
        Check(armed && !me.KnuckleArmed, "an unused Counter Hammer fades after 3 s");

        // The weapon-action key swaps to the Rocket stance: the dash and its chain, a click each, no windows.
        Clear();
        me.Pos = S;
        Step(2, 0, me.Pos + new V2(300f, 0f), 0, false, true);
        Step(10, 0, me.Pos + new V2(300f, 0f), 0, false, false);
        Check(me.KnuckleStance == 1 && s[1].Game.Players[1].KnuckleStance == 1, "the weapon-action key swaps to the Rocket stance (the other player's game knows)");
        var front = Dummy(new V2(S.X + 100f, S.Y));   // in the way along your aim
        var tgt = Dummy(new V2(S.X + 200f, S.Y + 20f));  // nearest the cursor
        var next = Dummy(new V2(S.X + 360f, S.Y + 24f));
        untouchable = false;
        Step(2, 2, tgt.Pos + new V2(6f, 0f), 0, false, false);
        Step(20, 0, tgt.Pos, 0, false, false);
        float tgtTook = Took(tgt), frontTook = Took(front);
        Check(me.KnuckleTarget == tgt.Id && untouchable && me.Pos.DistanceTo(tgt.Pos) < tgt.Radius + me.Def.BodyR + Game.KnuckleDashKnock + 20f,
            "a click throws the Rocket Dash: locked onto the enemy nearest the cursor, it flies you up to it, untouchable");
        Check(Math.Abs(tgtTook - baseDmg * Game.KnuckleDashMult) < 0.6f && frontTook == 0f, "the locked enemy takes the hit, the one in the way nothing");
        Check(me.KnuckleChain == 1, "and the chain waits for the next click");
        Step((int)(2.5f / dt), 0, tgt.Pos, 0, false, false);
        Check(me.KnuckleChain == 1, "with no window to hit: 2.5 s later it's still waiting");
        movedWhileRooted = false;
        Step(2, 2, tgt.Pos, 0, false, false);
        Check(me.KnuckleChain == 2, "the click throws the Flurry");
        Step(60, 0, tgt.Pos, 0, false, false);
        float flurry = Took(tgt);
        Check(Math.Abs(flurry - baseDmg * (Game.FlurryJabs * Game.FlurryJabMult + Game.FlurryStraightMult)) < 1f, "eight jabs and a straight (" + flurry + ")");
        Step(2, 2, next.Pos, 0, false, false);
        Check(me.KnuckleChain == 4 && heard.Contains(Sfx.KnuckleGrab), "the next click grabs it");
        for (int f = 0; f < 90 && me.KnuckleChain == 4; f++)
            Step(1, 0, next.Pos, 0, false, false);
        Check(heard.Contains(Sfx.KnuckleThrow) && Took(next) > 0f, "and throws it into the next enemy");
        Check(me.KnuckleChain == 5, "the Rocket Punch waits for its click");
        Took(tgt);
        Step(2, 2, next.Pos, 0, false, false);
        Step(40, 0, next.Pos, 0, false, false);
        Check(Took(next) > 0f, "the click fires the Rocket Punch down the line");
        Step(30, 0, next.Pos, 0, false, false);
        Check(me.KnuckleChain == 0, "and the chain is over when the fist is back");
        me.Pos = S;
        tgt.Pos = new V2(S.X + 150f, S.Y);
        Step(40, 0, tgt.Pos, 0, false, false);
        Step(2, 2, tgt.Pos, 0, false, false);
        Step(20, 0, tgt.Pos, 0, false, false);
        bool waiting = me.KnuckleChain == 1;
        Step((int)((Game.KnuckleRocketIdle + 0.5f) / dt), 0, tgt.Pos, 0, false, false);
        Check(waiting && me.KnuckleChain == 0, "4 s without a click and it's back to the Rocket Dash");
        Step(2, 0, tgt.Pos, 0, false, true);
        Step(10, 0, tgt.Pos, 0, false, false);
        Check(me.KnuckleStance == 0 && me.KnuckleMove == 0, "the key swaps back to Brawl, at the Pull Punch");

        // A thrown enemy stops at a wall: carried at throw speed, a frame at a time, it never crosses to the far side.
        g.DevSetLayout(LayoutId.Cross);
        Solid wall = default(Solid);
        bool found = false;
        foreach (var so in g.ActiveLayout.Solids)
            if (!so.Edge && !so.Round && (!found || Math.Min(so.HW, so.HH) < Math.Min(wall.HW, wall.HH))) { wall = so; found = true; }
        Check(found, "the Cross floor plan has a wall to throw into");
        if (found)
        {
            bool across = wall.HW <= wall.HH;  // throw across its thin side
            V2 dir = across ? new V2(1f, 0f) : new V2(0f, 1f);
            float half = across ? wall.HW : wall.HH;
            tgt.Pos = wall.Pos - dir * (half + tgt.Radius + 30f);
            bool stopped = false;
            for (int f = 0; f < 30 && !stopped; f++)
                stopped = g.KnuckleCarry(tgt, tgt.Pos + dir * Game.KnuckleThrowSpeed * dt);
            float past = (tgt.Pos - wall.Pos).X * dir.X + (tgt.Pos - wall.Pos).Y * dir.Y;
            Check(stopped && past < -half, "a thrown enemy stops on its own side of a wall, not through it (" + past + ")");
        }
        g.DevSetLayout(LayoutId.Open);

        s[0].Leave(""); s[1].Leave("");
    }
}
