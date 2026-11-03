using System.Linq;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using Arena;

// Weapons with their own rules: Flamethrower, Revolver reload, Mender's Bow, Bear Totem, the bullet budget
// and homing, Radiant Orb, and the weapon-upgrade fixes.
static partial class Program
{
    // The Flamethrower: a short stream whose flames hit the first enemy hardest, pass on through the rest for a third, and
    // set them burning, which goes on after you stop.
    static void FlamethrowerTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 1 }, port, false, "Solo", Role.Tank, 0, 0, Kit.Elementalist);
        var g = host.Game;
        g.Bot = false;
        host.StartMatch();
        var me = g.Players[1];
        g.DevSetGod(1, true);
        Check(me.Weapon.Id == WeaponId.Flamethrower, "the Elementalist Tank's first weapon is the Flamethrower");
        g.Enemies.Clear(); g.Bullets.Clear();
        Func<float, Enemy> dummy = x => { var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, new V2(x, 450f), 0f, 0, false }); e.Hp = e.MaxHp = 1e5f; e.SpawnT = 0f; return e; };
        var near = dummy(300f); var behind = dummy(345f); var far = dummy(520f);
        Action<float, bool> run = (secs, fire) =>
        {
            for (int f = 0; f < secs * 60; f++)
            {
                foreach (var e in new[] { near, behind, far }) { e.Pos = new V2(e == near ? 300f : e == behind ? 345f : 520f, 450f); e.FrozenT = 1f; }
                me.Pos = new V2(200f, 450f);
                host.Poll(); g.FixedStep(dt, new PlayerInput { Fire = fire, AimAt = new V2(900f, 450f) }); g.Update(dt);
            }
        };
        run(1f, true);
        float n1 = near.MaxHp - near.Hp, b1 = behind.MaxHp - behind.Hp, f1 = far.MaxHp - far.Hp;
        bool burning = near.BurnT > 0f && behind.BurnT > 0f;
        run(0.6f, false);  // the last flames burn out
        float h0 = near.Hp;
        run(1f, false);
        float afterwards = h0 - near.Hp;
        Console.WriteLine("  flamethrower: 1 s on two enemies in a row: {0:0} and {1:0} (one out of reach: {2:0}); burning {3}; {4:0.0} more in the next second from the fire",
            n1, b1, f1, burning, afterwards);
        Check(n1 > 70f && b1 > 15f && n1 > b1 * 2f && f1 == 0f, "the Flamethrower's flames hit the first enemy hardest, carry on through it for less, and don't reach far");
        Check(burning && Math.Abs(afterwards - Game.FlameBurn) < 1.5f, "what it hits keeps burning (" + Game.FlameBurn + " a second) after you stop");
        host.Leave("");
    }

    // Revolver, played by the joining player the way people actually shoot it: tapping. Each tap fires a
    // round with no reload in between until the cylinder is empty; a pause tops it back up a round at a
    // time; and Multishot's extra rounds really are there to fire.
    static void RevolverReloadTest(float dt)
    {
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 1 }, port, false, "Host", Role.Tank, 0);
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Healer, 1);  // Revolver
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var hp = host.Game.Players[client.Game.MyId];
        host.Game.DevSetGod(hp.Id, true);  // a quiet range: no enemies to down you or eat your rounds
        Action Quiet = () => host.Game.Enemies.Clear();
        var cme = client.Game.Players[client.Game.MyId];
        Func<int, float, bool, int[]> Tap = (taps, every, untilEmpty) =>
        {
            // Tap fire (held for 3 frames) every `every` seconds; count rounds fired and reloads started.
            var seen = new HashSet<int>(host.Game.Bullets.Keys);
            int fired = 0, reloads = 0;
            bool wasReloading = hp.ReloadT > 0f;
            int frames = (int)(taps * every * 60);
            for (int f = 0; f < frames; f++)
            {
                bool fire = f % (int)(every * 60) < 3;
                Quiet(); foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == client ? new PlayerInput { AimAt = new V2(1500f, 450f), Fire = fire } : new PlayerInput()); }
                Thread.Sleep(1);
                foreach (var b in host.Game.Bullets.Values)
                    if (b.Owner == hp.Id && seen.Add(b.Id)) fired++;
                bool reloading = hp.ReloadT > 0f;
                if (reloading && !wasReloading) reloads++;
                wasReloading = reloading;
                if (untilEmpty && reloads > 0) break;
            }
            return new[] { fired, reloads };
        };
        Action<float> Idle = secs => { for (int f = 0; f < secs * 60; f++) { Quiet(); foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, new PlayerInput { AimAt = new V2(1500f, 450f) }); } Thread.Sleep(1); } };

        Idle(1f);
        var slow = Tap(12, 0.7f, true);  // deliberate taps, slower than the old 0.6 s auto-reload
        Console.WriteLine("  revolver: tapping every 0.7 s fired {0} rounds before the first reload", slow[0]);
        Check(slow[0] == 6, "tapping the Revolver slowly fires all six rounds before it reloads");
        Idle(2f);
        Tap(3, 0.35f, false);
        int afterThree = cme.AmmoUsed;
        Idle(1.6f);  // 0.8 s, then a round every 0.25 s
        Console.WriteLine("  revolver: after 3 shots the client shows {0} used; after a 1.6 s pause, {1} used", afterThree, cme.AmmoUsed);
        Check(afterThree == 3 && cme.AmmoUsed == 0 && hp.ReloadT <= 0f, "a pause tops the cylinder back up a round at a time, without locking you out");

        // Tapping fast is capped at the aimed rate (one every 0.45 s), and every tap shot is dead on.
        Idle(2f);
        {
            var seen = new HashSet<int>(host.Game.Bullets.Keys);
            var shots = new List<Bullet>();
            for (int f = 0; f < 90; f++)  // 1.5 s of clicking ten times a second
            {
                bool fire = f % 6 < 2;
                Quiet(); foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == client ? new PlayerInput { AimAt = hp.Pos + new V2(400f, 0f), Fire = fire } : new PlayerInput()); }
                Thread.Sleep(1);
                foreach (var b in host.Game.Bullets.Values) if (b.Owner == hp.Id && seen.Add(b.Id)) shots.Add(b);
            }
            float worst = 0f;
            foreach (var b in shots) worst = Math.Max(worst, Math.Abs(b.Vel.Angle));
            Console.WriteLine("  revolver: clicking 10x a second for 1.5 s fired {0} aimed rounds, worst {1:0.000} rad off aim", shots.Count, worst);
            Check(shots.Count >= 3 && shots.Count <= 4 && worst < 0.001f, "tapping fires aimed rounds at the slower tap rate");
        }
        // Holding fans the hammer: the whole cylinder goes in a quick burst, spread over a 10 degree arc.
        Idle(2.5f);
        {
            var seen = new HashSet<int>(host.Game.Bullets.Keys);
            var shots = new List<Bullet>();
            float t = 0f, lastAt = 0f;
            for (int f = 0; f < 60; f++)  // hold for a second
            {
                t += dt;
                Quiet(); foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == client ? new PlayerInput { AimAt = hp.Pos + new V2(400f, 0f), Fire = true } : new PlayerInput()); }
                Thread.Sleep(1);
                foreach (var b in host.Game.Bullets.Values) if (b.Owner == hp.Id && seen.Add(b.Id)) { shots.Add(b); lastAt = t; }
            }
            float lo = 9f, hi = -9f;
            foreach (var b in shots) { lo = Math.Min(lo, b.Vel.Angle); hi = Math.Max(hi, b.Vel.Angle); }
            Console.WriteLine("  revolver: holding fired {0} rounds in {1:0.00} s, spread {2:0.0} to {3:0.0} degrees; reloading {4}",
                shots.Count, lastAt, lo * 180f / MathX.Pi, hi * 180f / MathX.Pi, hp.ReloadT > 0f);
            Check(shots.Count == 6 && lastAt < 0.7f && lo >= -0.0875f && hi <= 0.0875f && hi - lo > 0.02f,
                "holding fans the hammer: all six rounds in a quick burst within a 10 degree arc");
        }
        Idle(2f);

        host.Game.GrantUpgrade(hp.Id, Array.FindIndex(Upgrades.All, u => u.Name == "Multishot"));
        Idle(2f);
        var multi = Tap(16, 0.35f, true);
        Console.WriteLine("  revolver + Multishot: {0} rounds before the reload (capacity {1}, client sees {2})", multi[0], host.Game.RevolverCapacity(hp), client.Game.RevolverCapacity(cme));
        Check(multi[0] == 8 && client.Game.RevolverCapacity(cme) == 8, "with Multishot the Revolver really fires eight rounds per cylinder");
        Idle(2f);
        var again = Tap(16, 0.35f, true);
        Check(again[0] == 8, "and gets all eight back after reloading");
        host.Leave(""); client.Leave("");
    }

    static void BowTest(float dt)
    {
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 1 }, port, false, "Host", Role.Tank, 0);
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Healer, 2);  // Mender's Bow
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var hp = host.Game.Players[client.Game.MyId];
        var cme = client.Game.Players[client.Game.MyId];
        host.Game.DevSetGod(hp.Id, true);
        bool sawDrawOnHost = false;
        Func<Func<int, bool>, int, V2, List<Bullet>> Play = (fireAt, frames, move) =>
        {
            var seen = new HashSet<int>(host.Game.Bullets.Keys);
            var shots = new List<Bullet>();
            for (int f = 0; f < frames; f++)
            {
                host.Game.Enemies.Clear();
                foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == client ? new PlayerInput { AimAt = cme.Pos + new V2(400f, 0f), Fire = fireAt(f), Move = move } : new PlayerInput()); }
                Thread.Sleep(1);
                sawDrawOnHost |= hp.DrawT > 0.3f;
                foreach (var b in host.Game.Bullets.Values) if (b.Owner == hp.Id && seen.Add(b.Id)) shots.Add(b);
            }
            return shots;
        };
        Func<int, List<Bullet>> Draw = holdFrames => Play(f => f < holdFrames, holdFrames + 40, V2.Zero);
        Draw(0);
        var tap = Draw(2);
        var half = Draw(30);       // 0.5 s
        var perfect = Draw(57);    // 0.95 s: just past full draw
        var full = Draw(120);      // 2 s: held well past it
        Func<List<Bullet>, string> D = bs => bs.Count == 0 ? "none" : string.Join("/", bs.ConvertAll(b => b.Dmg.ToString("0.#") + " p" + b.Pierce + " heal " + (b.HealAlly * b.Dmg / 9f).ToString("0.#")));
        Console.WriteLine("  bow: tap {0}; 0.5 s {1}; 0.95 s {2}; 2 s {3}", D(tap), D(half), D(perfect), D(full));
        Check(tap.Count == 1 && Math.Abs(tap[0].Dmg - 8f) < 0.01f && tap[0].Pierce == 0, "a quick tap looses a weak arrow (8 damage)");
        Check(half.Count == 1 && half[0].Dmg > 15f && half[0].Dmg < 30f, "a half draw hits for something in between");
        Check(perfect.Count == 1 && Math.Abs(perfect[0].Dmg - 50f) < 0.01f && perfect[0].Pierce == 2 && Math.Abs(perfect[0].HealAlly * perfect[0].Dmg / 9f - 25f) < 0.1f,
            "letting go right at full draw is a perfect shot: 50 damage, pierces 2, heals an ally 25");
        Check(full.Count == 1 && Math.Abs(full[0].Dmg - 40f) < 0.01f && full[0].Pierce == 2 && full[0].Vel.Length > 900f, "holding past full draw keeps it at full (40 damage, fast, pierces 2), with no auto-fire");
        Check(sawDrawOnHost, "the host sees the joining player's draw (for the draw ring)");

        // Drawing slows you to 85%.
        V2 a0 = cme.Pos; Play(f => false, 30, new V2(1f, 0f)); float walk = cme.Pos.DistanceTo(a0);
        V2 b0 = cme.Pos; Play(f => true, 30, new V2(-1f, 0f)); float drawWalk = cme.Pos.DistanceTo(b0);
        Play(f => false, 30, V2.Zero);
        Console.WriteLine("  bow: walked {0:0} px in 0.5 s, {1:0} px while drawing", walk, drawWalk);
        Check(drawWalk / walk > 0.8f && drawWalk / walk < 0.9f, "drawing slows you to 85% speed");

        // Spamming taps is capped (a 0.25 s recovery between shots), and weaker than drawing.
        var spam = Play(f => f % 6 < 2, 90, V2.Zero);
        Console.WriteLine("  bow: clicking 10x a second for 1.5 s loosed {0} arrows", spam.Count);
        Check(spam.Count >= 3 && spam.Count <= 6, "tap spam is limited by the recovery between shots");

        // Extra projectiles nock more arrows, but only on a full draw.
        host.Game.GrantUpgrade(hp.Id, Array.FindIndex(Upgrades.All, u => u.Name == "Multishot"));
        host.Game.GrantUpgrade(hp.Id, Array.FindIndex(Upgrades.All, u => u.Name == "Multishot"));
        Play(f => false, 40, V2.Zero);
        var multiTap = Draw(2);
        var multiFull = Draw(80);
        Console.WriteLine("  bow +2: tap {0} arrow(s); full draw {1} arrows ({2})", multiTap.Count, multiFull.Count, D(multiFull));
        Check(multiTap.Count == 1 && multiFull.Count == 3 && Math.Abs(multiFull[1].Dmg - 20f) < 0.01f, "with +2 extra projectiles a full draw looses 3 arrows (the extras at half damage); taps stay single");
        host.Leave(""); client.Leave("");
    }

    // Bear Totem (the summoner Tank's weapon): two quick claw swipes in front of you that make enemies bleed.
    static void BearTotemTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Tank", Role.Tank, 2, 0, Kit.Summoner);
        var client = JoinAs(port, false, "Dps", Role.Dps);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        Check(me.Weapon.Name == "Bear Totem" && me.Weapon.Melee && Math.Abs(g.WeaponRange(me) - Game.MeleeReach(me, Defs.WeaponOf(WeaponId.BearTotem).Reach)) < 0.01f,
            "Bear Totem is a melee weapon with " + (int)g.WeaponRange(me) + " px of reach");

        var keep = new HashSet<Enemy>();
        Func<V2, Enemy> Dummy = at => { var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false }); e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; keep.Add(e); return e; };
        var front = Dummy(new V2(760f, 450f));
        var behind = Dummy(new V2(640f, 450f));
        int claws = 0, sides = 0;
        var seenFx = new HashSet<Effect>();
        Action<float, bool> Play = (secs, fire) =>
        {
            for (int f = 0; f < secs * 60; f++)
            {
                me.Pos = new V2(700f, 450f);
                host.Poll(); g.FixedStep(dt, new PlayerInput { Fire = fire, AimAt = new V2(900f, 450f) }); g.Update(dt);
                client.Poll(); client.Game.FixedStep(dt, new PlayerInput { AimAt = new V2(800, 450) }); client.Game.Update(dt);
                foreach (var e in g.Enemies.Values) if (!keep.Contains(e)) e.Hp = 0f;
                foreach (var e in keep) { e.FrozenT = 1f; }
                front.Pos = new V2(760f, 450f); behind.Pos = new V2(640f, 450f);
                foreach (var m in g.Minions.Values) m.Pos = new V2(200f, 800f);  // keep the Bear out of it
                foreach (var fx in g.Effects) if (fx.Kind == FxKind.Claw && seenFx.Add(fx)) { claws++; sides += fx.Radius > 0f ? 1 : -1; }
            }
        };
        Play(0.3f, false);
        float hp0 = front.Hp, behind0 = behind.Hp;
        Play(1f / 60f, true);   // one attack
        Play(0.05f, false);
        float afterFirst = hp0 - front.Hp;
        Play(0.15f, false);
        float afterBoth = hp0 - front.Hp;
        Console.WriteLine("  bear totem: first swipe {0:0.0}, both {1:0.0}, claw effects {2}, bleed {3:0.0}/s for {4:0.0} s", afterFirst, afterBoth, claws, front.BleedDps, front.BleedT);
        Check(claws == 2 && sides == 0 && afterFirst > 5f && afterBoth > afterFirst * 1.8f, "one attack is two quick swipes, raking one way then back");
        Check(behind.Hp == behind0, "the claws only hit in front of you");
        Check(front.BleedT > 2.5f && front.BleedDps > 0f, "a claw hit makes the enemy bleed");
        Play(0.2f, false);
        Check(client.Game.Enemies.ContainsKey(front.Id) && client.Game.Enemies[front.Id].Bleeding, "other players see the bleeding");
        float hp1 = front.Hp;
        Play(1f, false);
        float bled = hp1 - front.Hp;
        Play(3f, false);
        float hp2 = front.Hp;
        Play(1f, false);
        Check(bled > 1f && front.Hp == hp2 && front.BleedT <= 0f && !client.Game.Enemies[front.Id].Bleeding, "bleeding hurts over time (" + bled.ToString("0.0") + " in a second), then stops after 3 s");
        Play(4f, true);
        float perHit = 11f * me.Stats.DmgMult * 0.3f;
        Check(front.BleedDps > perHit * 3f && front.BleedDps <= perHit * 5f + 0.01f, "bleed stacks with each hit, up to five hits' worth (" + front.BleedDps.ToString("0.0") + "/s)");
        host.Leave(""); client.Leave("");
    }

    // The bullet budget caps enemy bullets on screen; Homing Rounds only fix near misses and cost damage.
    static void BudgetHomingTest(float dt)
    {
        var s = StartBuffed(dt, 21, 6);
        s[0].Game.DevSetGod(1, true); s[0].Game.DevSetGod(2, true);
        Run(s, dt, 8, () => s[0].Game.State == MatchState.Fighting);
        int max = 0, budget = s[0].Game.ShotBudget;
        Run(s, dt, 20, () => s[0].Game.State != MatchState.Fighting, () =>
        {
            if (!s[0].Game.Enemies.Values.Any(e => e.Def.IsBoss))
                max = Math.Max(max, s[0].Game.Bullets.Values.Count(b => Defs.IsEnemyShot(b.Kind)));
        });
        Console.WriteLine("  wave 21: at most {0} enemy bullets on screen (budget {1}; it used to reach 800+)", max, budget);
        // Only volleys check the budget: a Phantom's blink ring, a Charger's dash ring and a Mortar bomb's burst
        // fire regardless, so the screen can run a little over it (183-214 at wave 21 over random seeds).
        Check(budget > 100 && max <= budget * 1.5f, "enemy bullets stay near the bullet budget");
        s[0].Leave(""); s[1].Leave("");

        s = StartBuffed(dt, 3, 0);
        s[0].Game.Bot = s[1].Game.Bot = false;
        Run(s, dt, 6, () => s[0].Game.State == MatchState.Fighting);
        var g = s[0].Game;
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
        foreach (var e in g.Enemies.Values) e.Hp = 0f;
        Run(s, dt, 0.2f);
        var dummy = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, new V2(800f, 300f), 0f, 0, false });
        dummy.Hp = dummy.MaxHp = 1e6f;
        for (int k = 0; k < 3; k++) g.GrantUpgrade(2, Array.FindIndex(Upgrades.All, u => u.Name == "Homing Rounds"));
        Run(s, dt, 0.2f, null, () => { dummy.FrozenT = 1f; });
        // (3 stacks: 15% less damage.) A near miss (aimed 15 degrees off, 200 px away) bends in; a shot fired sideways doesn't hunt it down.
        var near = (Bullet)shoot.Invoke(g, new object[] { Defs.DpsShot, new V2(800f, 500f), V2.FromAngle(-MathX.Pi / 2f + 0.26f) * 600f, 0f, 1.5f, 10f, 2, false, 0f });
        var wide = (Bullet)shoot.Invoke(g, new object[] { Defs.DpsShot, new V2(800f, 500f), V2.FromAngle(0f) * 600f, 0f, 1.5f, 10f, 2, false, 0f });
        float wideAngle0 = wide.Vel.Angle, hpBefore = dummy.Hp;
        Run(s, dt, 0.6f, null, () => { dummy.FrozenT = 1f; });
        bool nearHit = dummy.Hp < hpBefore;
        float wideTurn = Math.Abs(wide.Vel.Angle - wideAngle0);
        Console.WriteLine("  homing x3: near miss hit {0}, sideways shot turned {1:0.00} rad, shot damage {2:0.0} of 10", nearHit, wideTurn, near.Dmg);
        Check(nearHit && wideTurn < 0.05f, "Homing Rounds fix near misses but don't hunt enemies off to the side");
        Check(Math.Abs(near.Dmg - 8.5f) < 0.01f, "Homing Rounds cost 5% damage per stack");

        // On area weapons Homing Rounds grow the area instead: a Flak Cannon's blast is 75% wider at 3 stacks and
        // doesn't bend; an Implosion reaches an enemy past its usual 55 px; the card says so.
        var hp = g.Players[2];
        var volley = typeof(Game).GetMethod("FireVolley", flags);
        hp.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.FlakCannon);
        g.Bullets.Clear();
        volley.Invoke(g, new object[] { hp, 0f, 0 });
        var ball = g.Bullets.Values.First(b => b.Owner == 2);
        Console.WriteLine("  homing x3 on a Flak Cannon: blast {0:0} px ({1:0} normally), turn {2:0}, damage {3:0.0}", ball.Splash, Game.FlakBurst, ball.Homing, ball.Dmg / hp.Stats.DmgMult);
        Check(Math.Abs(ball.Splash - Game.FlakBurst * 1.75f) < 0.01f && ball.Homing == 0f && Math.Abs(ball.Dmg / hp.Stats.DmgMult - hp.Weapon.Damage * 0.85f) < 0.05f,
            "on area weapons Homing Rounds widen the blast (25% a stack) instead of bending shots, for the same damage cost");
        // Cone weapons condense instead: Leech Tendrils, Chain Light and the Repulsion Cannon are 15% narrower and 10%
        // stronger a stack, so 3 stacks give 55% of the width and 130% of the damage. Tendrils: 70 -> 38.5 degrees, so
        // an enemy 30 degrees off your aim is grabbed normally but not with Homing.
        hp.Weapon = Defs.Weapon(Role.Tank, Kit.Hemomancer, 2);
        V2 at = hp.Pos + V2.FromAngle(30f * MathX.Pi / 180f) * 100f;
        Check(hp.Weapon.Name == "Leech Tendrils" && Math.Abs(Game.GraspArcFor(hp) - Game.GraspArc * 0.55f) < 0.001f
            && !Game.InGraspArc(hp.Pos, 0f, at, 5f, Game.GraspArcFor(hp)) && Game.InGraspArc(hp.Pos, 0f, at, 5f),
            "Leech Tendrils with Homing Rounds reach across a narrower arc (55% at 3 stacks)");
        {
            float crit0 = hp.Stats.CritChance; hp.Stats.CritChance = 0f;
            float homing = hp.Stats.Homing;
            V2 me0 = hp.Pos;
            Func<WeaponId, float, float> blast = (id, stacks) =>
            {
                hp.Weapon = Defs.Weapons.First(w => w.Id == id); hp.Stats.Homing = stacks;
                foreach (var e in g.Enemies.Values) e.Hp = 0f;
                g.Enemies.Clear();
                var t = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, me0 + new V2(150f, 0f), 0f, 0, false });
                t.SpawnT = 0f; t.Hp = t.MaxHp = 1e6f; hp.Pos = me0;
                volley.Invoke(g, new object[] { hp, 0f, 0 });
                return t.MaxHp - t.Hp;
            };
            float cl0 = blast(WeaponId.ChainLight, 0f), cl3 = blast(WeaponId.ChainLight, homing);
            float half3 = Game.LightConeHalfFor(hp, 1);
            float rp0 = blast(WeaponId.LightBolt, 0f), rp3 = blast(WeaponId.LightBolt, homing);
            hp.Stats.Homing = homing; hp.Stats.CritChance = crit0;
            string card = Upgrades.DescFor(Upgrades.All[Array.FindIndex(Upgrades.All, u => u.Name == "Homing Rounds")], Defs.Weapons.First(w => w.Id == WeaponId.ChainLight));
            Console.WriteLine("  homing x3, condensed cones: chain light {0:0.0} -> {1:0.0} ({2:0} degrees across), repulsion {3:0.0} -> {4:0.0}; card: {5}",
                cl0, cl3, 2f * half3 * 180f / MathX.Pi, rp0, rp3, card);
            Check(cl0 > 0f && Math.Abs(cl3 / cl0 - 1.3f) < 0.01f && Math.Abs(half3 - Game.LightConeHalf * 0.55f) < 0.001f && card.StartsWith("Chain Light: the cone of light 15% narrower"),
                "Chain Light with Homing Rounds is a narrower cone that hits 30% harder at 3 stacks, and the card says so");
            Check(rp0 > 0f && Math.Abs(rp3 / rp0 - 1.3f) < 0.01f, "the Repulsion Cannon with Homing Rounds is a narrower blast that hits 30% harder at 3 stacks");
        }
        hp.Weapon = Defs.Weapon(Role.Dps, Kit.Hemomancer, 1);
        var core = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, new V2(500f, 600f), 0f, 0, false });
        var far = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, new V2(500f + 2f * core.Radius + 80f, 600f), 0f, 0, false });
        core.SpawnT = far.SpawnT = 0f; core.Hp = core.MaxHp = far.Hp = far.MaxHp = 1e6f;
        typeof(Game).GetMethod("Collapse", flags).Invoke(g, new object[] { hp, core, 1f, true, new Rgba(1f, 0f, 0f) });
        Check(far.Hp < far.MaxHp && Upgrades.DescFor(Upgrades.All[Array.FindIndex(Upgrades.All, u => u.Name == "Homing Rounds")], hp.Weapon).StartsWith("Implosion: the collapse"),
            "an Implosion with Homing Rounds reaches enemies past its usual 55 px, and the card says what Homing does for it");
        s[0].Leave(""); s[1].Leave("");
    }

    // Radiant Orb: held out on the cursor, dragged through enemies (it burns them) and over allies (it heals them).
    static void RadiantOrbTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var queueField = typeof(Game).GetField("spawnQueue", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2 }, port, false, "Healer", Role.Healer, 0, 0, Kit.Elementalist);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        g.Bot = client.Game.Bot = false;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        var ally = g.Players[2];
        g.DevSetGod(1, true);
        Check(me.Weapon.Id == WeaponId.RadiantOrb, "the Elementalist Healer's first weapon is the Radiant Orb");
        var dummy = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, new V2(700f, 300f), 0f, 0, false });
        dummy.Hp = dummy.MaxHp = 1e5f; dummy.SpawnT = 0f;
        Action<float, bool, V2> hold = (secs, fire, at) =>
        {
            for (int f = 0; f < secs * 60; f++)
            {
                ((List<EnemyType>)queueField.GetValue(g)).Clear(); ClearSquads(g);
                foreach (var e in g.Enemies.Values) if (e != dummy) e.Hp = 0f;
                dummy.Pos = new V2(700f, 300f); dummy.FrozenT = 1f; dummy.FireT = 9f;
                me.Pos = me.Disp = new V2(400f, 450f);
                client.Game.Players[2].Pos = new V2(400f, 650f);
                host.Poll(); g.FixedStep(dt, new PlayerInput { Fire = fire, AimAt = at }); g.Update(dt);
                client.Poll(); client.Game.FixedStep(dt, new PlayerInput { AimAt = new V2(400f, 900f) }); client.Game.Update(dt);
                if (f % 2 == 0) Thread.Sleep(1);
            }
        };
        hold(0.3f, false, dummy.Pos);
        float hp0 = dummy.Hp;
        hold(2f, true, dummy.Pos);
        float burned = hp0 - dummy.Hp;
        var seen = client.Game.Players[1];
        Console.WriteLine("  radiant orb: held on an enemy 2 s: {0:0} damage; the orb {1:0} px from it (the friend sees it on: {2}, {3:0} px off)",
            burned, me.RadiantPos.DistanceTo(dummy.Pos), seen.RadiantOn, seen.RadiantPos.DistanceTo(me.RadiantPos));
        Check(burned > 50f && me.RadiantPos.DistanceTo(dummy.Pos) < 5f && seen.RadiantOn && seen.RadiantPos.DistanceTo(me.RadiantPos) < 30f,
            "holding fire casts the orb out to your cursor, where it burns the enemy (and your friend sees it)");
        ally.Hp = ally.MaxHp * 0.3f;
        float a0 = ally.Hp, hp1 = dummy.Hp;
        hold(1.5f, true, new V2(400f, 650f));  // drag it over to your friend
        Console.WriteLine("  radiant orb: dragged onto your friend: healed {0:0.0} in 1.5 s; the enemy took {1:0} meanwhile", ally.Hp - a0, hp1 - dummy.Hp);
        Check(ally.Hp > a0 + 10f, "dragging the orb over an ally heals them");
        hold(1f, true, new V2(1500f, 450f));
        Check(me.RadiantPos.DistanceTo(me.Pos) <= g.RadiantReach(me) + 0.5f, "it stops at its reach (" + g.RadiantReach(me) + " px) however far your cursor goes");
        hold(0.3f, false, dummy.Pos);
        float hp2 = dummy.Hp;
        hold(1f, false, dummy.Pos);
        Check(!me.RadiantOn && !seen.RadiantOn && dummy.Hp == hp2, "let go and it's gone");
        host.Leave(""); client.Leave("");
    }

    // Upgrade fixes: Velocity Rounds no longer carry a Flak Cannon shell or Ofuda charm past the cursor, and Incendiary
    // Rounds add up over every hit (about +25% a stack on a fast weapon and a slow one alike).
    static void UpgradeFixesTest(float dt)
    {
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var volley = typeof(Game).GetMethod("FireVolley", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var host = new Session { AutoStart = 1, StartWave = 3 };
        host.Host(nextPort++, "A", Role.Tank, 0);
        host.StartMatch();
        var g = host.Game;
        for (int i = 0; i < 300 && !g.Running; i++) { host.Poll(); g.FixedStep(dt, new PlayerInput()); }
        var me = g.Players[1];
        g.DevSetGod(1, true);
        Func<string, int> U = n => Array.FindIndex(Upgrades.All, u => u.Name == n);
        Action<string, int> build = (up, n) => { me.Taken.Clear(); for (int k = 0; k < n; k++) me.Taken.Add(U(up)); g.GrantUpgrade(1, U("Vitality")); };
        V2 from = new V2(400f, 450f);
        Action clear = () => { foreach (var e in g.Enemies.Values) e.Hp = 0f; for (int f = 0; f < 3; f++) { host.Poll(); g.FixedStep(dt, new PlayerInput()); } g.Bullets.Clear(); g.Zones.Clear(); g.Minions.Clear(); };
        Func<V2, Enemy> dummy = at => { var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false }); e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; e.FireT = 99f; return e; };

        // Velocity Rounds: where a Flak shell bursts and an Ofuda charm lands, aimed 250 px away.
        var landed = new List<string>();
        bool allAtCursor = true;
        foreach (var id in new[] { WeaponId.FlakCannon, WeaponId.Ofuda })
            foreach (int stacks in new[] { 0, 3 })
            {
                clear();
                build("Velocity Rounds", stacks);
                me.Weapon = Defs.Weapons.First(w => w.Id == id);
                me.Pos = from;
                me.AimAt = from + new V2(250f, 0f);
                volley.Invoke(g, new object[] { me, 0f, 0 });
                var b = g.Bullets.Values.First(x => x.Owner == 1);
                V2 last = b.Pos + b.Vel * b.Life;  // where its flight ends (it bursts or lands there)
                float dist = last.DistanceTo(me.AimAt);
                landed.Add(string.Format("{0} x{1}: {2:0} px from the cursor", me.Weapon.Name, stacks, dist));
                allAtCursor &= dist < 30f;
            }
        Console.WriteLine("  " + string.Join("; ", landed));
        Check(allAtCursor, "Velocity Rounds don't carry Flak Cannon shells or Ofuda charms past the cursor");

        // Incendiary Rounds on a fast weapon (Riot Repeater) and a slow one (Revolver, as a Healer would): 6 s on one enemy.
        var gain = new List<float>();
        foreach (var id in new[] { WeaponId.RiotRepeater, WeaponId.Pistol })
        {
            var dealt = new float[2];
            for (int pass = 0; pass < 2; pass++)
            {
                clear();
                build("Incendiary Rounds", pass);
                me.Weapon = Defs.Weapons.First(w => w.Id == id);
                var e = dummy(from + new V2(200f, 0f));
                for (int f = 0; f < 60 * 6; f++) { e.Pos = from + new V2(200f, 0f); e.FrozenT = 1f; me.Pos = from; host.Poll(); g.FixedStep(dt, new PlayerInput { Fire = true, AimAt = e.Pos }); }
                for (int f = 0; f < 60 * 4; f++) { e.Pos = from + new V2(200f, 0f); e.FrozenT = 1f; me.Pos = from; host.Poll(); g.FixedStep(dt, new PlayerInput()); }  // let it burn out
                dealt[pass] = e.MaxHp - e.Hp;
            }
            gain.Add(dealt[1] / dealt[0] - 1f);
        }
        Console.WriteLine("  incendiary (1 stack): Riot Repeater {0:+0}%, Revolver {1:+0}%", gain[0] * 100f, gain[1] * 100f);
        Check(gain.All(x => x > 0.35f && x < 0.45f), "Incendiary Rounds add 40% of every hit as burn, on fast and slow weapons alike");

        // Hits that aren't bullets (a cone, a blast, a ward burst) carry weapon mods too: Explosive Rounds used to be
        // dropped for them (their splashes were cleared before they were spent), and some never applied mods at all.
        var res = new List<string>();
        bool modsRide = true;
        foreach (var id in new[] { WeaponId.ChainLight, WeaponId.LightBolt, WeaponId.Ofuda })
        {
            var dealt = new float[2];
            bool burned = false;
            for (int pass = 0; pass < 2; pass++)
            {
                clear();
                me.Taken.Clear();
                if (pass == 1) { me.Taken.Add(U("Explosive Rounds")); me.Taken.Add(U("Incendiary Rounds")); }
                g.GrantUpgrade(1, U("Vitality"));
                me.Weapon = Defs.Weapons.First(w => w.Id == id);
                V2 at = from + new V2(150f, 0f), side = at + new V2(10f, 28f);
                var a1 = dummy(at); var a2 = dummy(side);
                for (int f = 0; f < 60 * 6; f++)
                {
                    a1.Pos = at; a2.Pos = side; a1.FrozenT = a2.FrozenT = 1f; me.Pos = from;
                    host.Poll(); g.FixedStep(dt, new PlayerInput { Fire = true, AimAt = at });
                    burned |= pass == 1 && (a1.PoolT > 0f || a2.PoolT > 0f);
                }
                dealt[pass] = (a1.MaxHp - a1.Hp) + (a2.MaxHp - a2.Hp);
            }
            res.Add(string.Format("{0} {1:+0}%{2}", Defs.WeaponOf(id).Name, (dealt[1] / Math.Max(1f, dealt[0]) - 1f) * 100f, burned ? " burning" : " NOT burning"));
            modsRide &= burned && dealt[1] > dealt[0] * 1.03f;  // the Repulsion Cannon shoves enemies out of its own explosions, so it gains least
        }
        Console.WriteLine("  Explosive + Incendiary Rounds on non-bullet weapons: " + string.Join(", ", res));
        Check(modsRide, "weapon mods (Explosive, Incendiary) apply to Chain Light, Repulsion Cannon and Ofuda hits");

        // Ricochet on a weapon whose hits can't bounce (Chain Light's cone): a bounce bolt carries on to an enemy outside it.
        var outside = new float[2];
        for (int pass = 0; pass < 2; pass++)
        {
            clear();
            me.Taken.Clear();
            if (pass == 1) me.Taken.Add(U("Ricochet"));
            g.GrantUpgrade(1, U("Vitality"));
            me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.ChainLight);
            V2 at = from + new V2(150f, 0f), beside = at + new V2(0f, 200f);  // well outside the cone, within a bounce of the first
            var a1 = dummy(at); var a2 = dummy(beside);
            for (int f = 0; f < 60 * 3; f++)
            {
                a1.Pos = at; a2.Pos = beside; a1.FrozenT = a2.FrozenT = 1f; me.Pos = from;
                host.Poll(); g.FixedStep(dt, new PlayerInput { Fire = true, AimAt = at });
            }
            outside[pass] = a2.MaxHp - a2.Hp;
        }
        Console.WriteLine("  ricochet on Chain Light: the enemy outside the cone took {0:0.0} without, {1:0.0} with", outside[0], outside[1]);
        Check(outside[0] == 0f && outside[1] > 0f, "Ricochet flings bounce bolts off hits that can't bounce (Chain Light's cone)");

        // Piercing Rounds on weapons without bullets that run out of pierce: each stack reaches 10% farther.
        var longer = new List<string>();
        bool allLonger = true;
        foreach (var id in new[] { WeaponId.Flamethrower, WeaponId.FrostLance, WeaponId.FumaShuriken, WeaponId.LightBolt, WeaponId.RadiantOrb,
            WeaponId.ChainLight, WeaponId.SpiritFan, WeaponId.Ofuda, WeaponId.ArcLightning, WeaponId.WispLantern, WeaponId.ImpCaller })
        {
            me.Weapon = Defs.Weapons.First(w => w.Id == id);
            build("Piercing Rounds", 0);
            float r0 = g.WeaponRange(me);
            build("Piercing Rounds", 2);
            float r2 = g.WeaponRange(me);
            // Some ranges count from your edge (body radius added), some from your centre: the reach itself is 1.3x either way.
            float ratio = Math.Abs((r2 - me.Def.BodyR) / (r0 - me.Def.BodyR) - 1.3f) < Math.Abs(r2 / r0 - 1.3f) ? (r2 - me.Def.BodyR) / (r0 - me.Def.BodyR) : r2 / r0;
            if (Math.Abs(ratio - 1.3f) > 0.01f) { allLonger = false; longer.Add(me.Weapon.Name + string.Format(" x{0:0.00}", ratio)); }
        }
        Console.WriteLine("  piercing (2 stacks): every reach x1.3 {0}{1}", allLonger, longer.Count > 0 ? " (off: " + string.Join(", ", longer) + ")" : "");
        Check(allLonger, "Piercing Rounds make weapons without piercing bullets reach 15% farther a stack");
        var reached = new float[2];
        for (int pass = 0; pass < 2; pass++)
        {
            clear();
            build("Piercing Rounds", pass * 2);
            me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.ChainLight);
            V2 at = from + new V2(Game.LightConeReach * 1.2f + Defs.Get(EnemyType.Brute).Radius, 0f);  // its edge 20% past the plain cone (the cone counts from your centre)
            var a1 = dummy(at);
            for (int f = 0; f < 60 * 2; f++) { a1.Pos = at; a1.FrozenT = 1f; me.Pos = from; host.Poll(); g.FixedStep(dt, new PlayerInput { Fire = true, AimAt = at }); }
            reached[pass] = a1.MaxHp - a1.Hp;
        }
        Console.WriteLine("  piercing on Chain Light: an enemy 20% past the plain cone took {0:0.0} without, {1:0.0} with 2 stacks", reached[0], reached[1]);
        Check(reached[0] == 0f && reached[1] > 0f, "with Piercing Rounds the Chain Light's cone reaches enemies past its plain length");

        // Multishot / Barrage per weapon (balance item I).
        // Frost Lance: each extra projectile is 25% more damage, as well as a longer icicle.
        var icicle = new float[2];
        for (int pass = 0; pass < 2; pass++)
        {
            clear();
            build("Multishot", pass);
            me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.FrostLance);
            V2 at = from + new V2(80f, 0f);
            var a1 = dummy(at);
            me.Pos = from; me.AimAt = at;
            volley.Invoke(g, new object[] { me, 0f, g.ExtraShots(me) });  // FireVolley takes the extra projectiles
            for (int f = 0; f < 20; f++) { a1.Pos = at; a1.FrozenT = 1f; me.Pos = from; host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = at }); }
            icicle[pass] = a1.MaxHp - a1.Hp;
        }
        Console.WriteLine("  frost lance: one icicle {0:0.0}, with Multishot {1:0.0}", icicle[0], icicle[1]);
        Check(icicle[0] > 0f && Math.Abs(icicle[1] / icicle[0] - 1.25f) < 0.02f, "Multishot makes the Frost Lance's icicle hit 25% harder");

        // Radiant Orb: Multishot and Barrage each add a light orb, and they circle the orb while it's out.
        clear();
        me.Taken.Clear(); me.Taken.Add(U("Multishot")); me.Taken.Add(U("Barrage")); g.GrantUpgrade(1, U("Vitality"));
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.RadiantOrb);
        V2 orbAt = from + new V2(220f, 60f);
        for (int f = 0; f < 120; f++) { me.Pos = from; me.CompanionCd = Math.Min(me.CompanionCd, 0.05f); host.Poll(); g.FixedStep(dt, new PlayerInput { Fire = true, AimAt = orbAt }); }
        var lights = g.Minions.Values.Where(m => m.Owner == 1 && m.Kind == MinionKind.LightOrb).ToList();
        float farthest = lights.Count > 0 ? lights.Max(m => m.Pos.DistanceTo(me.RadiantPos)) : 999f;
        Console.WriteLine("  radiant orb + Multishot + Barrage: {0} light orbs, farthest {1:0} px from the orb (ring {2})", lights.Count, farthest, Game.LightOrbRing);
        Check(lights.Count == 2 && farthest < Game.LightOrbRing + 5f, "Radiant Orb: Multishot and Barrage each add a light orb, circling the orb while it's out");

        // Wolf Whistle: a dart that clips an enemy farther from your cursor than the prey doesn't move the pack off it.
        clear();
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.WolfWhistle);
        var dartHit = typeof(Game).GetMethod("WolfDartHit", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var preyA = dummy(from + new V2(200f, 0f)); var preyB = dummy(from + new V2(260f, 80f));
        me.AimAt = preyA.Pos;
        dartHit.Invoke(g, new object[] { new Bullet { Kind = Defs.BoneShot, Owner = 1 }, preyA });
        dartHit.Invoke(g, new object[] { new Bullet { Kind = Defs.BoneShot, Owner = 1 }, preyB });
        bool kept = me.PreyId == preyA.Id;
        me.AimAt = preyB.Pos;
        dartHit.Invoke(g, new object[] { new Bullet { Kind = Defs.BoneShot, Owner = 1 }, preyB });
        bool moved = me.PreyId == preyB.Id;
        Console.WriteLine("  wolf whistle: a stray dart kept the prey {0}; aiming at the other and hitting it moved it {1}", kept, moved);
        Check(kept && moved, "Wolf Whistle: stray (fanned or piercing) darts don't pull the pack off the enemy you aim at");

        // Magnum Rounds: shots 15% bigger a stack, the same size on the client; area weapons' areas 15% wider.
        clear();
        build("Magnum Rounds", 2);
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.Pistol);
        me.Pos = from; me.AimAt = from + new V2(300f, 0f);
        volley.Invoke(g, new object[] { me, 0f, 0 });
        var round = g.Bullets.Values.First(x => x.Owner == 1);
        float plain = Defs.BulletRadius(round.Kind);
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.FlakCannon);
        float area = Game.AreaMult(me);
        Console.WriteLine("  magnum rounds x2: a Revolver round {0:0.00} px (plain {1:0.00}); a Flak Cannon's area x{2:0.00}", round.Radius, plain, area);
        Check(Math.Abs(round.Radius / plain - 1.3f) < 0.01f && Math.Abs(area - 1.3f) < 0.01f, "Magnum Rounds make shots 15% bigger a stack, and area weapons' areas 15% wider");
        host.Leave("");
    }
}
