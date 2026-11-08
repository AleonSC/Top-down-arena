using System.Linq;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using Arena;

// Weapons: every weapon over the network, the Repulsion Cannon, Quarterstaff and Tank Knight, extra projectiles,
// range and aim, and the Frost Lance, Storm Hammer, Golem Staff, Bone Guard, Light Cone, Sanguine Orb and Fireball.
// More in WeaponsByKit.cs and WeaponsSpecial.cs.
static partial class Program
{
    // Every weapon fires and the client's bullets match the host's.
    static void WeaponSweep(float dt)
    {
        int port = 7792;
        foreach (var roles in new[] { new[] { Role.Tank, Role.Healer }, new[] { Role.Dps, Role.Dps } })
        {
            for (int w = 0; w < Defs.WeaponsPerRole; w++)
            {
                var host = HostAs(new Session { AutoStart = 2, StartWave = 2 }, port, true, "Host", roles[0], w);
                var client = JoinAs(port, true, "Friend", roles[1], w);
                port++;
                WaitForGame(host, client, dt);
                int ghosts = 0, fired = 0;
                for (int i = 0; i < 60 * 10; i++)
                {
                    Step(host, dt);
                    Step(client, dt);
                    if (i % 2 == 0)
                        Thread.Sleep(1);
                    foreach (var b in host.Game.Bullets.Values)
                        if (b.Kind < Defs.SmallShot)
                            fired++;
                    if (i % 30 == 0)
                    {
                        for (int k = 0; k < 5; k++) { Thread.Sleep(2); client.Poll(); }
                        foreach (var b in client.Game.Bullets.Values)
                            if (!host.Game.Bullets.ContainsKey(b.Id))
                                ghosts++;
                    }
                }
                string names = Defs.Weapon(roles[0], Kit.Soldier, w).Name + " + " + Defs.Weapon(roles[1], Kit.Soldier, w).Name;
                Check(fired > 0 && ghosts == 0, "weapons fire and stay in sync: " + names);
                host.Leave("");
                client.Leave("");
            }
        }
    }

    // Extra projectiles upgrade each weapon in its own way. Every weapon fires at a cluster of dummies
    // with 0 and then 2 extra projectiles; each must get stronger in its own way, and none turns
    // into a line of bullets.
    /// The Repulsion Cannon (Soldier Healer): a cone blast that shoves enemies away, does a little damage and knocks
    /// tiny health orbs loose.
    static void RepulsionTest(float dt)
    {
        var s = StartBuffed(dt, 1, 0);
        s[0].Game.Bot = s[1].Game.Bot = false;
        Run(s, dt, 3, () => s[0].Game.State == MatchState.Fighting);
        var g = s[0].Game;
        var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", bf);
        var queue = (List<EnemyType>)typeof(Game).GetField("spawnQueue", bf).GetValue(g);
        var me = g.Players[1];
        me.Weapon = Defs.Weapon(Role.Healer, Kit.Soldier, 0);
        Check(me.Weapon.Name == "Repulsion Cannon", "the Soldier Healer's first weapon is the Repulsion Cannon");
        queue.Clear(); g.Enemies.Clear(); g.Orbs.Clear();
        V2 from = new V2(500f, 450f);
        Func<V2, EnemyType, Enemy> Dummy = (at, t) => { var e = (Enemy)spawn.Invoke(g, new object[] { t, at, 0f, 0, false }); e.MaxHp = e.Hp = 1e5f; e.Speed = 0f; e.FireT = 99f; return e; };
        var front = Dummy(new V2(680f, 450f), EnemyType.Grunt);   // in the cone
        var side = Dummy(new V2(560f, 300f), EnemyType.Grunt);    // 68 degrees off the aim: outside it
        var boss = Dummy(new V2(700f, 520f), EnemyType.Boss);     // in the cone, too heavy to shove
        V2 f0 = front.Pos, b0 = boss.Pos;
        me.Pos = from;
        foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == s[0] ? new PlayerInput { AimAt = new V2(800f, 450f), Fire = true } : new PlayerInput()); }
        float pushedNow = front.Pos.X - f0.X;
        bool knockFx = g.Effects.Any(fx => fx.Kind == FxKind.Knock);
        SettleKnocks(g);  // the push slides (Knock.cs)
        float pushed = front.Pos.X - f0.X;
        Console.WriteLine("  knockback: {0:0} px on the blast's tick, {1:0} px once it has slid; streak effect {2}", pushedNow, pushed, knockFx);
        Check(pushedNow < pushed - 20f && knockFx, "the blast slides enemies back over a moment (with a streak) instead of jumping them there");
        Console.WriteLine("  repulsion: front pushed {0:0} px, took {1:0}; side took {2:0}; boss moved {3:0} px, took {4:0}", pushed, front.MaxHp - front.Hp, side.MaxHp - side.Hp, boss.Pos.DistanceTo(b0), boss.MaxHp - boss.Hp);
        Check(pushed > 60f && front.Hp < front.MaxHp && side.Hp == side.MaxHp, "it blasts enemies in its cone away and hurts them a little (not ones outside it)");
        Check(boss.Pos.DistanceTo(b0) < 1f && boss.Hp < boss.MaxHp, "bosses take the damage but don't budge");
        // Keep blasting: tiny orbs come loose, everyone sees them, and they heal a little.
        int tiny = 0; bool clientTiny = false;
        for (int f = 0; f < 8 * 60; f++)
        {
            me.Pos = from; front.Pos = f0; boss.Pos = b0; queue.Clear();
            foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == s[0] ? new PlayerInput { AimAt = new V2(800f, 450f), Fire = true } : new PlayerInput()); }
            tiny = Math.Max(tiny, g.Orbs.Values.Count(o => o.Tiny));
            clientTiny |= s[1].Game.Orbs.Values.Any(o => o.Tiny);
        }
        var orb = g.Orbs.Values.FirstOrDefault(o => o.Tiny);
        Console.WriteLine("  repulsion: {0} tiny orbs about after 8 s of blasting (client saw them {1}), heal {2}", tiny, clientTiny, orb != null ? orb.Heal : 0f);
        Check(tiny >= 2 && clientTiny && orb != null && orb.Heal < Game.OrbHeal && orb.Radius < 6f, "enemies it hits shed tiny health orbs (smaller than the others; everyone sees them)");
        s[0].Leave(""); s[1].Leave("");
    }

    /// The Quarterstaff (Knight Healer): wide sweeps that knock enemies back and parry bullets as they swing.
    static void QuarterstaffTest(float dt)
    {
        var s = StartBuffed(dt, 1, 0);
        s[0].Game.Bot = s[1].Game.Bot = false;
        Run(s, dt, 3, () => s[0].Game.State == MatchState.Fighting);
        var g = s[0].Game;
        var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", bf);
        var shoot = typeof(Game).GetMethod("SpawnBullet", bf);
        var queue = (List<EnemyType>)typeof(Game).GetField("spawnQueue", bf).GetValue(g);
        var me = g.Players[1];
        me.Weapon = Defs.Weapon(Role.Healer, Kit.Knight, 0);
        Check(me.Weapon.Name == "Quarterstaff", "the Knight Healer's first weapon is the Quarterstaff");
        queue.Clear(); g.Enemies.Clear(); g.Bullets.Clear();
        V2 from = new V2(500f, 450f);
        var dummy = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, new V2(590f, 450f), 0f, 0, false });
        dummy.MaxHp = dummy.Hp = 1e5f; dummy.Speed = 0f; dummy.FireT = 99f;
        V2 d0 = dummy.Pos;
        me.Hp = me.MaxHp * 0.5f; float hp0 = me.Hp;
        // Enemy bullets coming at the swing: a small one, a big orb, and a red aimed shot (which gets through).
        var small = (Bullet)shoot.Invoke(g, new object[] { Defs.SmallShot, new V2(570f, 420f), new V2(-150f, 0f), 0f, 5f, 12f, 0, false, 0f });
        var big = (Bullet)shoot.Invoke(g, new object[] { Defs.BigShot, new V2(575f, 480f), new V2(-110f, 0f), 0f, 5f, 20f, 0, false, 0f });
        var red = (Bullet)shoot.Invoke(g, new object[] { Defs.PierceShot, new V2(580f, 450f), new V2(-200f, 0f), 0f, 5f, 16f, 0, false, 0f });
        int idSmall = small.Id, idBig = big.Id, idRed = red.Id;
        int before = g.Bullets.Values.Count(b => b.Owner == 1 && b.Kind == Defs.HolyShot);
        for (int f = 0; f < 10; f++)
        {
            me.Pos = from; queue.Clear();
            foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == s[0] ? new PlayerInput { AimAt = new V2(800f, 450f), Fire = f == 0 } : new PlayerInput()); }
        }
        int deflected = g.Bullets.Values.Count(b => b.Owner == 1 && b.Kind == Defs.HolyShot && b.Vel.X > 0f) - before;
        Console.WriteLine("  quarterstaff: dummy pushed {0:0} px, took {1:0}; {2} bullets batted back; small gone {3}, big gone {4}, red still flying {5}",
            dummy.Pos.X - d0.X, dummy.MaxHp - dummy.Hp, deflected, !g.Bullets.ContainsKey(idSmall), !g.Bullets.ContainsKey(idBig), g.Bullets.ContainsKey(idRed));
        Check(dummy.Pos.X - d0.X > 50f && dummy.Hp < dummy.MaxHp, "its swings knock enemies back and hurt them");
        Check(me.Hp > hp0, "each enemy it hits heals you (and allies near you)");
        Check(deflected == 2 && !g.Bullets.ContainsKey(idSmall) && !g.Bullets.ContainsKey(idBig) && g.Bullets.ContainsKey(idRed),
            "each swing parries: bullets in its arc fly back at enemies as yours (red aimed shots get through)");
        s[0].Leave(""); s[1].Leave("");
    }

    /// The Quarterstaff's Jab stacks: one per enemy a swipe hits, three at most (a limit each extra projectile raises), never
    /// fading; the weapon-action key spends one on a cone jab that hits the cone and nothing outside it.
    static void QuarterstaffStacksTest(float dt)
    {
        var s = StartBuffed(dt, 1, 0);
        s[0].Game.Bot = s[1].Game.Bot = false;
        Run(s, dt, 3, () => s[0].Game.State == MatchState.Fighting);
        var g = s[0].Game;
        var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", bf);
        var queue = (List<EnemyType>)typeof(Game).GetField("spawnQueue", bf).GetValue(g);
        var me = g.Players[1];
        me.Weapon = Defs.Weapon(Role.Healer, Kit.Knight, 0);
        me.Hp = me.MaxHp;
        V2 from = new V2(500f, 450f), aimAt = new V2(800f, 450f);
        Func<V2, Enemy> Dummy = at =>
        {
            var d = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, at, 0f, 0, false });
            d.MaxHp = d.Hp = 1e5f; d.Speed = 0f; d.FireT = 99f; d.SpawnT = 0f;
            return d;
        };
        int altFrames = int.MaxValue;  // how long the jab key is held in each step
        Action<int, bool, bool> Step = (frames, fire, alt) =>
        {
            for (int f = 0; f < frames; f++)
            {
                me.Pos = from; queue.Clear();
                foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == s[0] ? new PlayerInput { AimAt = aimAt, Fire = fire && f < 3, WeaponAction = alt && f < altFrames } : new PlayerInput()); }
            }
        };
        queue.Clear(); g.Enemies.Clear(); g.Bullets.Clear();
        Check(me.StaffStacks == 0, "the staff starts with no stacks");
        Step(40, false, true);
        Check(me.StaffStacks == 0 && g.Bullets.Count == 0, "the jab key does nothing without a stack");

        Func<Enemy[]> Pair = () => { g.Enemies.Clear(); return new[] { Dummy(new V2(590f, 450f)), Dummy(new V2(570f, 500f)) }; };
        Pair();
        Step(60, true, false);
        Check(me.StaffStacks == 2, "a swipe that hits two enemies stores two stacks (" + me.StaffStacks + ")");
        Pair(); Step(60, true, false);
        Check(me.StaffStacks == Game.StaffBaseStacks, "the staff stores three at most (" + me.StaffStacks + ")");
        Step(60 * 12, false, false);
        Check(me.StaffStacks == Game.StaffBaseStacks, "stacks never fade");
        Pair(); Step(60, true, false);
        me.Stats.ExtraShots = 1;
        Check(g.StaffCap(me) == Game.StaffBaseStacks + 1, "each extra projectile raises the limit by one");
        Pair(); Step(60, true, false);
        Check(me.StaffStacks == Game.StaffBaseStacks + 1, "and the stacks fill to it (" + me.StaffStacks + ")");
        me.Stats.ExtraShots = 0;

        // The jab: a cone 170 px out and 48 degrees across.
        g.Enemies.Clear();
        var near = Dummy(new V2(640f, 450f)); var wide = Dummy(new V2(560f, 530f)); var far = Dummy(new V2(720f, 450f));
        me.StaffStacks = 3; me.Hp = me.MaxHp * 0.5f; float hp0 = me.Hp;
        float x0 = near.Pos.X;
        altFrames = 3;
        Step(30, false, true);
        altFrames = int.MaxValue;
        float dn = near.MaxHp - near.Hp;
        Console.WriteLine("  staff jab: stacks left {0}, ahead took {1:0} and moved {2:0} px, off to the side took {3:0}, past the reach took {4:0}",
            me.StaffStacks, dn, near.Pos.X - x0, wide.MaxHp - wide.Hp, far.MaxHp - far.Hp);
        Check(me.StaffStacks == 2, "a jab spends one stack");
        Check(dn >= 30f && near.Pos.X - x0 > 60f, "it hurts what is in the cone and shoves it back");
        Check(wide.Hp == wide.MaxHp && far.Hp == far.MaxHp, "enemies outside the cone or its reach are missed");
        Check(me.Hp > hp0, "it heals you for what it hits");
        Step(60, false, true);
        Check(me.StaffStacks == 0, "held down, the key keeps jabbing until the stacks are gone (" + me.StaffStacks + ")");
        s[0].Leave(""); s[1].Leave("");
    }

    static void GreatswordTest(float dt)
    {
        var s = StartBuffed(dt, 1, 0);
        s[0].Game.Bot = s[1].Game.Bot = false;
        Run(s, dt, 3, () => s[0].Game.State == MatchState.Fighting);
        var g = s[0].Game;
        var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", bf);
        var shoot = typeof(Game).GetMethod("SpawnBullet", bf);
        var queue = (List<EnemyType>)typeof(Game).GetField("spawnQueue", bf).GetValue(g);
        var me = g.Players[1];
        me.Weapon = Defs.Weapon(Role.Dps, Kit.Knight, 1);
        me.Hp = me.MaxHp;
        V2 from = new V2(500f, 450f), aimAt = new V2(800f, 450f);
        Func<V2, Enemy> Dummy = at =>
        {
            var d = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, at, 0f, 0, false });
            d.MaxHp = d.Hp = 1e5f; d.Speed = 0f; d.FireT = 99f; d.SpawnT = 0f;
            return d;
        };
        var heard = new HashSet<Sfx>();
        Action<int, bool, bool> Step = (frames, fire, alt) =>
        {
            for (int f = 0; f < frames; f++)
            {
                me.Pos = from; queue.Clear();
                foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == s[0] ? new PlayerInput { AimAt = aimAt, Fire = fire && f < 3, WeaponAction = alt } : new PlayerInput()); }
                foreach (var c in g.Sounds) heard.Add(c.Sfx);
                g.Sounds.Clear();
            }
        };
        Func<V2, V2, byte, Bullet> Shot = (at, vel, kind) => (Bullet)shoot.Invoke(g, new object[] { kind, at, vel, 0f, 3f, 5f, 0, false, 0f });
        queue.Clear(); g.Enemies.Clear(); g.Bullets.Clear();
        float mult = me.Stats.DmgMult, baseDmg = me.Weapon.Damage * mult;

        // The chain: cut, return cut, overhead rend, then round again.
        var dummy = Dummy(new V2(590f, 450f));
        Func<float> Taken = () => { float t = dummy.MaxHp - dummy.Hp; dummy.Hp = dummy.MaxHp; return t; };
        Step(6, true, false);
        float d1 = Taken();
        Check(me.GreatStep == 1 && Math.Abs(d1 - baseDmg) < 0.5f, "the first cut lands at once for the weapon's damage (" + d1 + " vs " + baseDmg + ")");
        Step(30, false, false);
        Step(6, true, false);
        float d2 = Taken();
        Check(me.GreatStep == 2 && Math.Abs(d2 - baseDmg) < 0.5f, "the return cut is the same (" + d2 + ")");
        Step(30, false, false);
        Step(3, true, false);
        float early = Taken();
        Check(me.GreatStep == 3 && early == 0f, "the overhead rend winds up first: nothing lands in its first frames");
        Check(heard.Contains(Sfx.GreatWindup), "and the blade lifting is heard");
        Step(30, false, false);
        float d3 = Taken();
        Check(Math.Abs(d3 - baseDmg * Game.GreatMult[2]) < 0.5f && d3 > 2f * d1, "then lands for 2.25 times a cut (" + d3 + " vs " + d1 + ")");
        Step(70, false, false);
        Check(me.GreatStep == 0, "the chain starts over after 0.9 s without a swing");
        Console.WriteLine("  greatsword chain: {0:0} / {1:0} / {2:0}", d1, d2, d3);

        // The cuts leave crescents that hang for their own times.
        Step(70, false, false);
        g.Bullets.Clear();
        Step(3, true, false);
        var wall1 = Shot(from + new V2(Game.MeleeReach(me, me.Weapon.Reach) + 40f, 0f), new V2(-300f, 0f), Defs.BigShot);
        Step(10, false, false);
        Check(!g.Bullets.ContainsKey(wall1.Id), "a cut's crescent stops a bullet crossing it");
        Step(60, false, false);
        var wall2 = Shot(from + new V2(Game.MeleeReach(me, me.Weapon.Reach) + 40f, 0f), new V2(-300f, 0f), Defs.BigShot);
        Step(10, false, false);
        Check(g.Bullets.ContainsKey(wall2.Id), "and after 0.35 s it has gone");
        g.Bullets.Clear();
        Step(80, false, false);

        // Guard: hold the weapon-action key.
        float speed0 = me.Speed;
        g.Enemies.Clear();
        var near = Dummy(new V2(590f, 450f));
        near.Hp = near.MaxHp;
        Step(5, true, true);
        Check(me.GreatGuard, "holding the weapon-action key raises the guard");
        Check(heard.Contains(Sfx.GreatRaise), "the guard going up is heard");
        Check(Math.Abs(me.Speed - speed0 * Game.GreatGuardSlow) < 0.01f, "you move at half speed while guarding");
        Check(near.Hp == near.MaxHp && me.GreatStep == 0, "and you can't swing while it's up");
        int temper0 = me.GreatTemper;
        var b1 = Shot(from + new V2(Game.GreatGuardRadius + 40f, 0f), new V2(-300f, 0f), Defs.BigShot);
        Step(20, false, true);
        Check(heard.Contains(Sfx.GreatBlock), "a bullet stopped on the arc rings");
        Check(!g.Bullets.ContainsKey(b1.Id) && me.GreatTemper == temper0 + 1, "a bullet hitting the arc is stopped and adds a point of Temper (" + me.GreatTemper + ")");
        var red = Shot(from + new V2(Game.GreatGuardRadius + 40f, 0f), new V2(-300f, 0f), Defs.PierceShot);
        Step(6, false, true);
        Check(g.Bullets.ContainsKey(red.Id) && me.GreatTemper == temper0 + 1, "an aimed red shot goes through and gives nothing");
        g.Bullets.Clear();
        var behind = Shot(from + new V2(-(Game.GreatGuardRadius + 40f), 0f), new V2(300f, 0f), Defs.BigShot);
        Step(14, false, true);
        Check(me.GreatTemper == temper0 + 1, "a bullet from behind is not stopped by the arc in front");
        g.Bullets.Clear();
        for (int k = 0; k < 8; k++)
        {
            Shot(from + new V2(Game.GreatGuardRadius + 30f, 0f), new V2(-300f, 0f), Defs.BigShot);
            Step(12, false, true);
        }
        Check(me.GreatTemper == Game.GreatTemperMax, "Temper stops at six (" + me.GreatTemper + ")");
        Check(heard.Contains(Sfx.GreatFull), "and a chime says it is full");
        heard.Remove(Sfx.GreatRend);

        // Release: the Rend. Full Temper, so it also primes the next chain.
        g.Enemies.Clear(); g.Bullets.Clear();
        var inLine = Dummy(new V2(700f, 450f)); var farLine = Dummy(new V2(1050f, 450f)); var wide = Dummy(new V2(700f, 520f));
        Step(10, false, false);
        float full = Game.GreatRendDamage(Game.GreatTemperMax) * mult;
        float hIn = inLine.MaxHp - inLine.Hp, hFar = farLine.MaxHp - farLine.Hp;
        Console.WriteLine("  greatsword rend at full Temper: first in line took {0:0}, second {1:0}, off the line {2:0}", hIn, hFar, wide.MaxHp - wide.Hp);
        Check(!me.GreatGuard && me.GreatTemper == 0, "letting go drops the guard and spends the Temper");
        Check(heard.Contains(Sfx.GreatRend), "and the Rend is heard");
        Check(Math.Abs(hIn - full) < 0.5f && Math.Abs(hFar - full) < 0.5f, "a six-point Rend pierces everything in its line for 120 (" + hIn + ", " + hFar + ")");
        Check(wide.Hp == wide.MaxHp, "and misses what is off the line");
        Check(me.GreatPrimed, "a full Rend primes the next chain");
        Step(30, false, false);
        Step(3, true, false);
        Check(me.GreatStep == 3 && !me.GreatPrimed, "which starts on the overhead rend (" + me.GreatStep + ")");

        // A short guard still fires a small Rend, and the guard can't go straight back up.
        Step(80, false, false);
        g.Enemies.Clear();
        var small = Dummy(new V2(700f, 450f));
        Step(4, false, true);
        me.GreatTemper = 2;
        Step(10, false, false);
        float dSmall = small.MaxHp - small.Hp;
        Check(Math.Abs(dSmall - Game.GreatRendDamage(2) * mult) < 0.5f, "a Rend with two points deals 60 (" + dSmall + ")");
        Step(10, false, true);
        Check(!me.GreatGuard, "the guard can't go straight back up after a Rend");
        Step(2, false, false);
        Step(60, false, false);
        Step(4, false, true);
        Check(me.GreatGuard, "but can after a moment");

        // Putting the sword away clears everything.
        me.GreatTemper = 3;
        me.Weapon = Defs.Weapon(Role.Dps, Kit.Knight, 0);
        Step(4, false, false);
        Check(!me.GreatGuard && me.GreatTemper == 0 && me.GreatStep == 0, "the guard and Temper go when the sword is put away");
        s[0].Leave(""); s[1].Leave("");
    }

    static void TankKnightTest(float dt)
    {
        var s = StartBuffed(dt, 1, 0);
        s[0].Game.Bot = s[1].Game.Bot = false;
        Run(s, dt, 3, () => s[0].Game.State == MatchState.Fighting);
        var g = s[0].Game;
        var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", bf);
        var queue = (List<EnemyType>)typeof(Game).GetField("spawnQueue", bf).GetValue(g);
        var me = g.Players[1];
        V2 from = new V2(500f, 450f);
        Func<V2, Enemy> Dummy = at =>
        {
            var d = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, at, 0f, 0, false });
            d.MaxHp = d.Hp = 1e5f; d.Speed = 0f; d.FireT = 99f; d.SpawnT = 0f;
            return d;
        };
        // Hold fire for `hold` frames, let go, and watch for a second.
        Action<int, V2> Attack = (hold, aimAt) =>
        {
            for (int f = 0; f < hold + 60; f++)
            {
                me.Pos = from; queue.Clear();
                foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == s[0] ? new PlayerInput { AimAt = aimAt, Fire = f < hold } : new PlayerInput()); }
            }
        };

        // The Warhammer: tap or charge, then slam a fixed circle in front of you.
        me.Weapon = Defs.Weapon(Role.Tank, Kit.Knight, 0);
        Check(me.Weapon.Name == "Warhammer" && Game.ChargeWeapon(me.Weapon.Id), "the Warhammer charges up like a bow");
        queue.Clear(); g.Enemies.Clear(); g.Bullets.Clear();
        var front = Dummy(new V2(610f, 450f));
        var side = Dummy(new V2(560f, 560f));  // off to the side: inside a wide arc, outside the circle
        var edge = Dummy(new V2(500f + Game.HammerCentre(me.Weapon.Reach) + Game.HammerRadius - 6f, 450f));  // on the circle's far rim: still hit
        var beyond = Dummy(new V2(500f + Game.HammerCentre(me.Weapon.Reach) + Game.HammerRadius + 60f, 450f));  // past it: safe
        var behind = Dummy(new V2(420f, 450f));  // behind you: safe
        float stunTap = 0f, stunFull = 0f;
        Attack(3, new V2(800f, 450f));
        float tap = front.MaxHp - front.Hp;
        front.Hp = front.MaxHp; front.FrozenT = 0f;
        for (int f = 0; f < 140; f++)
        {
            me.Pos = from; queue.Clear();
            foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == s[0] ? new PlayerInput { AimAt = new V2(800f, 450f), Fire = f < 80 } : new PlayerInput()); }
            stunFull = Math.Max(stunFull, front.FrozenT);
        }
        float full = front.MaxHp - front.Hp;
        front.Hp = front.MaxHp; front.FrozenT = 0f;
        for (int f = 0; f < 70; f++)
        {
            me.Pos = from; queue.Clear();
            foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == s[0] ? new PlayerInput { AimAt = new V2(800f, 450f), Fire = f < 3 } : new PlayerInput()); }
            stunTap = Math.Max(stunTap, front.FrozenT);
        }
        Console.WriteLine("  warhammer: tap {0:0} dmg, stun {1:0.00} s; full charge {2:0} dmg, stun {3:0.00} s; side dummy took {4:0}",
            tap, stunTap, full, stunFull, side.MaxHp - side.Hp);
        Check(tap > 0f && full > tap * 2.5f, "the slam's damage grows with the charge");
        Check(stunTap > 0f && stunFull > stunTap + 0.3f, "the slam stuns what it hits, longer at full charge");
        Check(side.Hp == side.MaxHp, "the slam hits a circle in front, not the sides");
        Check(edge.Hp < edge.MaxHp && beyond.Hp == beyond.MaxHp && behind.Hp == behind.MaxHp, "the slam's circle is a fixed size: its far rim is hit, past it and behind you are safe");

        // The Halberd: a thin cone swept across; each enemy hit once a sweep.
        me.Weapon = Defs.Weapon(Role.Tank, Kit.Knight, 2);
        Check(me.Weapon.Name == "Halberd", "the Knight Tank's third weapon is the Halberd");
        queue.Clear(); g.Enemies.Clear(); g.Bullets.Clear();
        var a = Dummy(new V2(500f + 170f * (float)Math.Cos(0.6), 450f - 170f * (float)Math.Sin(0.6)));
        var b = Dummy(new V2(670f, 450f));
        var c = Dummy(new V2(500f + 170f * (float)Math.Cos(0.6), 450f + 170f * (float)Math.Sin(0.6)));
        var far = Dummy(new V2(500f + 120f * (float)Math.Cos(1.3), 450f + 120f * (float)Math.Sin(1.3)));  // outside the sweep
        Attack(1, new V2(800f, 450f));
        float dmg = me.Weapon.Damage * me.Stats.DmgMult;
        Console.WriteLine("  halberd: hits {0:0} / {1:0} / {2:0} (one swing {3:0}); outside the sweep took {4:0}",
            a.MaxHp - a.Hp, b.MaxHp - b.Hp, c.MaxHp - c.Hp, dmg, far.MaxHp - far.Hp);
        Check(a.Hp < a.MaxHp && b.Hp < b.MaxHp && c.Hp < c.MaxHp, "the Halberd sweeps across, reaching enemies on both sides of the aim");
        Check(b.MaxHp - b.Hp < dmg * 1.5f, "each enemy is hit once a sweep, however many steps pass over it");
        Check(far.Hp == far.MaxHp, "enemies outside the sweep are safe");
        // The swing is one motion from the click (HalberdMotion.cs): a pull back, then six steps that land as the edge reaches them.
        {
            float prev = 0f; bool inOrder = true, smooth = true;
            for (int pass = 1; pass <= 2; pass++)
                for (int i = 0; i < Game.HalberdSteps; i++)
                {
                    float t = HalberdMotion.StepTime(pass, i, Game.HalberdSteps);
                    if (t <= prev) inOrder = false;
                    prev = t;
                }
            float first = HalberdMotion.StepTime(1, 0, Game.HalberdSteps), last = HalberdMotion.StepTime(1, Game.HalberdSteps - 1, Game.HalberdSteps);
            float ret = HalberdMotion.StepTime(2, 0, Game.HalberdSteps);
            for (bool up = false; ; up = true)
            {
                var pp = HalberdMotion.At(0f, up);
                for (float t = 0.002f; t < HalberdMotion.Total(up) + 0.05f; t += 0.002f)
                {
                    var q = HalberdMotion.At(t, up);
                    if (Math.Abs(q.A - pp.A) > 0.2f || Math.Abs(q.K - pp.K) > 0.1f || Math.Abs(Math.Cos(q.Roll) - Math.Cos(pp.Roll)) > 0.4) smooth = false;
                    pp = q;
                }
                if (up) break;
            }
            Console.WriteLine("  halberd motion: first step at {0:0.000} s, last {1:0.000} s, the return sweep starts {2:0.000} s, total {3:0.00} / {4:0.00} s", first, last, ret, HalberdMotion.Total(false), HalberdMotion.Total(true));
            Check(inOrder && first > 0.1f && first < 0.2f && last > first && last < 0.3f, "the Halberd's steps land in order after a short wind-up (first within 0.2 s of the click, the sweep done by 0.3 s)");
            Check(ret > last + 0.05f && ret < 0.5f, "the return sweep follows after the head turns over");
            Check(smooth, "the Halberd's pose moves smoothly the whole way (no jump in the pole, its reach or the head's roll)");
            Check(HalberdMotion.Total(false) < 0.6f, "a lone sweep is back at the ready before the next swing (0.6 s)");
        }
        s[0].Leave(""); s[1].Leave("");
    }

    static void WeaponExtraTest(float dt)
    {
        var s = StartBuffed(dt, 1, 0);
        s[0].Game.Bot = s[1].Game.Bot = false;
        Run(s, dt, 3, () => s[0].Game.State == MatchState.Fighting);
        var g = s[0].Game;
        var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", bf);
        var queue = (List<EnemyType>)typeof(Game).GetField("spawnQueue", bf).GetValue(g);
        var me = g.Players[1];
        var ally = g.Players[2];
        V2 from = new V2(540f, 450f), center = new V2(700f, 450f);  // the nearest dummy is 100 px out
        Func<WeaponDef, int, float[]> Trial = (w, extra) =>
        {
            center = new V2(700f, 450f);
            queue.Clear();
            g.Enemies.Clear();
            g.Minions.Clear();
            g.Zones.Clear();
            g.Bullets.Clear();
            var dummies = new List<Enemy>();
            for (int k = 0; k < 9; k++)
            {
                V2 at = k == 0 ? center : center + V2.FromAngle(MathX.Tau * (k - 1) / 8f) * (k % 2 == 0 ? 110f : 60f);
                var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, at, 0f, 0, false });
                e.MaxHp = e.Hp = 1e6f;
                dummies.Add(e);
            }
            ((System.Collections.IList)typeof(Game).GetField("pendingSwings", bf).GetValue(g)).Clear();  // the last weapon's swings still in flight don't land in this trial
            me.FireCd = 0f; me.Combo = 0; me.ComboT = 0f;
            me.Weapon = w;
            me.Stats.ExtraShots = extra;
            me.AmmoUsed = 0;
            me.ReloadT = 0f;
            me.CompanionCd = 0f;
            int spawned = 0, totems = 0;  // totems: now the most ink dots down at once (Spirit Chalk's extra projectiles are more ink)
            var seen = new HashSet<int>();
            for (int f = 0; f < 6 * 60; f++)
            {
                queue.Clear();
                me.Pos = w.Id == WeaponId.Kusarigama ? center - new V2(70f, 0f)  // within its sickle cut
                    : w.Melee ? center - new V2(Game.MeleeRange(w.Id) * 0.5f + 40f, 0f) : from;  // knights stand in reach
                ally.Pos = new V2(560f, 300f);
                foreach (var e in dummies) { e.Speed = 0f; e.FireT = 99f; e.Pos = e.Disp; }
                foreach (var x in s)
                {
                    x.Poll();
                    // The bow fires when you let go: draw past full (1.25 s), release, repeat.
                    x.Game.FixedStep(dt, x == s[0] ? new PlayerInput { AimAt = w.Id == WeaponId.SpiritChalk ? from + V2.FromAngle(f * 0.06f) * 280f : center, Fire = !Game.ChargeWeapon(w.Id) || f % 90 < 75 } : new PlayerInput());
                }
                foreach (var b in g.Bullets.Values)
                    if (b.Owner == 1 && seen.Add(b.Id)) spawned++;
                totems = Math.Max(totems, g.Ink.Count(d => d.Owner == 1 && d.T > Game.ChalkErase));
            }
            float dmg = 0f;
            foreach (var e in dummies) dmg += e.MaxHp - e.Hp;
            int companions = 0;
            float scale = 1f;
            foreach (var m in g.Minions.Values)
                if (m.Owner == 1 && m.Companion) { companions++; scale = Math.Max(scale, m.Scale); }
            return new[] { dmg, spawned, companions, totems, scale };
        };
        int pass = 0;
        var weak = new List<string>();
        Console.WriteLine("  weapon            dmg 0 -> +2    shots 0 -> +2   companions   (6 s at a cluster of dummies)");
        foreach (var w in Defs.Weapons)
        {
            var a = Trial(w, 0);
            var b = Trial(w, 2);
            float ratio = b[0] / Math.Max(1f, a[0]);
            bool ok;
            switch (w.Id)
            {
                case WeaponId.SpriteStaff: ok = b[2] == a[2] + 2; break;                 // +1 sprite each
                case WeaponId.SpiritChalk: ok = b[3] > a[3]; break;                      // more ink: a longer line
                case WeaponId.MendersBow: ok = b[1] >= a[1] * 2.5f && ratio > 1.2f; break;  // full draws loose extra arrows
                case WeaponId.Greatsword: ok = b[1] > a[1] && ratio > 1.1f; break;       // the overhead rend's sword waves: only one cut in three has them
                case WeaponId.TwinBlades: ok = b[1] > a[1] && ratio > 1.1f; break;       // crescents off the Pirouette and the Shear: two steps in five have them
                case WeaponId.BearTotem: ok = ratio > 1.1f; break;                        // claws plus the Bear's cleave (it wanders)
                case WeaponId.TreantRod: ok = Math.Abs(b[4] - 1.6f) < 0.01f; break;       // a bigger treant
                case WeaponId.HolyMace: ok = me.StaffStacks == Game.StaffBaseStacks + 2; break;  // each extra projectile stores one more Jab stack (the swipes fill it)
                case WeaponId.GolemStaff: ok = a[2] == 1 && b[2] == 3; break;
                case WeaponId.BoneGuard: ok = b[2] == a[2] + 2; break;                     // guards block bullets around you
                case WeaponId.WolfWhistle: case WeaponId.WispLantern: ok = b[2] == a[2] + 2 && ratio > 1.1f; break;
                case WeaponId.RadiantOrb: ok = a[2] == 0 && b[2] == 2 && ratio > 1.1f; break;  // the orbs mostly guard and heal you
                default: ok = ratio > 1.2f; break;
            }
            Console.WriteLine("  {0,-17} {1,6:0} -> {2,6:0} (x{3:0.00})  {4,4} -> {5,4}      {6} -> {7}{8}", w.Name, a[0], b[0], ratio, a[1], b[1], a[2], b[2], ok ? "" : "   <-- not stronger");
            if (ok) pass++; else weak.Add(w.Name);
        }
        Check(pass == Defs.Weapons.Length, "every weapon gets its own upgrade from extra projectiles (" + pass + "/" + Defs.Weapons.Length + ")" + (weak.Count > 0 ? ": " + string.Join(", ", weak) : ""));

        // The Revolver: six rounds (the last one heavy), then a reload; two more heavy rounds per extra projectile.
        {
            queue.Clear(); g.Enemies.Clear(); g.Bullets.Clear();
            me.Weapon = Defs.Weapon(Role.Healer, Kit.Soldier, 1);
            me.Stats.ExtraShots = 0;
            me.AmmoUsed = 0; me.ReloadT = 0f;
            var dmgs = new List<float>();
            var seen = new HashSet<int>(g.Bullets.Keys);
            bool reloaded = false, clientSawReload = false;
            float firstGap = 0f, t = 0f, lastShot = -1f;
            for (int f = 0; f < 4 * 60; f++)
            {
                t += dt;
                foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == s[0] ? new PlayerInput { AimAt = me.Pos + new V2(300f, 0f), Fire = true } : new PlayerInput()); }
                foreach (var b in g.Bullets.Values)
                    if (b.Owner == 1 && seen.Add(b.Id))
                    {
                        if (lastShot >= 0f && t - lastShot > firstGap) firstGap = t - lastShot;
                        lastShot = t;
                        dmgs.Add(b.Dmg);
                    }
                reloaded |= me.ReloadT > 0f;
                clientSawReload |= s[1].Game.Players[1].ReloadT > 0f;
            }
            float baseDmg = dmgs.Count > 0 ? dmgs[0] : 0f;
            Console.WriteLine("  revolver: {0} shots in 4 s, first six {1}, longest gap {2:0.00} s", dmgs.Count, string.Join(" ", dmgs.GetRange(0, Math.Min(7, dmgs.Count)).ConvertAll(x => x.ToString("0"))), firstGap);
            Check(dmgs.Count >= 7 && dmgs[4] == baseDmg && dmgs[5] == baseDmg * 2f && dmgs[6] == baseDmg && firstGap > 1.1f && reloaded && clientSawReload,
                "the Revolver fires six rounds (the sixth heavy), then reloads (seen on the client)");
            me.Stats.ExtraShots = 2;
            me.AmmoUsed = 0; me.ReloadT = 0f;
            Check(g.RevolverCapacity(me) == 10 && g.RevolverHeavy(me) == 5, "each extra projectile adds two heavy rounds to the Revolver's cylinder");
        }
        // Barrage builds the wall: every shot also fires copies fanned out to the sides.
        {
            Func<WeaponDef, int, int, List<Bullet>> FirstVolley = (w, fan, extra) =>
            {
                g.Bullets.Clear();
                me.Weapon = w;
                me.Stats.Fan = fan;
                me.Stats.ExtraShots = extra;
                me.AmmoUsed = 0; me.ReloadT = 0f;
                me.HeldT = 0f; me.Fanning = false; me.WasFiring = false; me.TapQueued = false;  // a fresh trigger pull
                var seen = new HashSet<int>();
                var shots = new List<Bullet>();
                for (int f = 0; f < 60 && shots.Count == 0; f++)
                {
                    foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == s[0] ? new PlayerInput { AimAt = me.Pos + new V2(300f, 0f), Fire = true } : new PlayerInput()); }
                    foreach (var b in g.Bullets.Values) if (b.Owner == 1 && seen.Add(b.Id)) shots.Add(b);
                }
                return shots;
            };
            var rev = FirstVolley(Defs.Weapon(Role.Healer, Kit.Soldier, 1), 2, 0);
            var angles = rev.ConvertAll(b => (float)Math.Round(b.Vel.Angle, 2));
            angles.Sort();
            int used = me.AmmoUsed;
            var scatter0 = FirstVolley(Defs.Weapon(Role.Tank, Kit.Soldier, 0), 0, 0);
            var scatter2 = FirstVolley(Defs.Weapon(Role.Tank, Kit.Soldier, 0), 2, 0);
            Func<List<Bullet>, float> Width = bs => { float lo = 9f, hi = -9f; foreach (var b in bs) { lo = Math.Min(lo, b.Vel.Angle); hi = Math.Max(hi, b.Vel.Angle); } return hi - lo; };
            Console.WriteLine("  barrage: revolver x2 fires at {0} using {1} round(s); scattergun {2} pellets over {3:0.00} rad -> {4} over {5:0.00} rad",
                string.Join(" ", angles), used, scatter0.Count, Width(scatter0), scatter2.Count, Width(scatter2));
            Check(rev.Count == 3 && Math.Abs(angles[0] + 0.14f) < 0.011f && Math.Abs(angles[2] - 0.14f) < 0.011f && used == 1,
                "Barrage fans every shot out to the sides (a Revolver round fans for one round of ammo)");
            Check(scatter2.Count == 3 * scatter0.Count && Width(scatter2) > Width(scatter0) + 0.25f, "Barrage stacks into a wall: a Scattergun with two copies fires three cones side by side");
            me.Stats.Fan = 0;
        }
        // Twin Rifle: barrels side by side, not a line.
        {
            g.Bullets.Clear();
            me.Weapon = Defs.Weapon(Role.Dps, Kit.Soldier, 0);
            me.Stats.ExtraShots = 2;
            var seen = new HashSet<int>();
            var first = new List<Bullet>();
            for (int f = 0; f < 60 && first.Count == 0; f++)
            {
                foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == s[0] ? new PlayerInput { AimAt = me.Pos + new V2(300f, 0f), Fire = true } : new PlayerInput()); }
                foreach (var b in g.Bullets.Values) if (b.Owner == 1 && seen.Add(b.Id)) first.Add(b);
            }
            float spreadY = 0f;
            foreach (var b in first) foreach (var c in first) spreadY = Math.Max(spreadY, Math.Abs(b.Pos.Y - c.Pos.Y));
            Check(first.Count == 4 && spreadY > 25f, "the Twin Rifle with +2 fires four parallel barrels at once");
        }
        s[0].Leave(""); s[1].Leave("");
    }

    static void WeaponRangeTest(float dt)
    {
        // The aim line ends at Game.WeaponRange: check it against how far each weapon's shots really go.
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var volley = typeof(Game).GetMethod("FireVolley", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 1 }, port, false, "Solo", Role.Dps, 0, 0, Kit.Soldier);
        var g = host.Game;
        host.StartMatch();
        var me = g.Players[1];
        g.DevSetGod(1, true);
        int pass = 0;
        var off = new List<string>();
        foreach (var w in Defs.Weapons)
        {
            foreach (int extra in new[] { 0, 2 })
            {
                g.Enemies.Clear(); g.Bullets.Clear(); g.Minions.Clear(); g.Effects.Clear();
                me.Weapon = w;
                me.Stats.ExtraShots = extra;
                me.BowShotDraw = 0f;
                me.Orbiters = w.Id == WeaponId.ArcaneMissiles ? Game.OrbitCap(extra) : 0;  // a full orbit: the cast flies at your aim
                me.SwingCount = 0;  // the Katana's every-4th lunge shouldn't depend on what was tested before it
                me.Pos = new V2(120f, 450f);
                me.Aim = 0f;
                float range = g.WeaponRange(me);
                if (w.Id == WeaponId.RadiantOrb)
                {
                    // Held out to its reach (the aim line's end), the orb touches an enemy against its edge but not one just past it.
                    Func<float, bool> Touches = gap =>
                    {
                        g.Enemies.Clear();
                        var dummy = (Enemy)typeof(Game).GetMethod("SpawnEnemyAt", flags).Invoke(g, new object[] { EnemyType.Grunt, new V2(120f, 450f), 0f, 0, false });
                        dummy.Hp = dummy.MaxHp = 1e5f; dummy.SpawnT = 0f;
                        V2 at = new V2(120f + range + Game.RadiantRadius + dummy.Radius + gap, 450f);
                        for (int f = 0; f < 90; f++)
                        {
                            dummy.Pos = at; dummy.FrozenT = 1f; me.Pos = new V2(120f, 450f);
                            foreach (var lo in g.Minions.Values.Where(m => m.Kind == MinionKind.LightOrb).ToList()) g.Minions.Remove(lo.Id);  // the orb alone: its light orbs circle it and reach past its edge
                            host.Poll(); g.FixedStep(dt, new PlayerInput { Fire = true, AimAt = new V2(1500f, 450f) });
                        }
                        for (int f = 0; f < 10; f++) { host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = new V2(1500f, 450f) }); }  // let go
                        return dummy.Hp < dummy.MaxHp;
                    };
                    bool touch = Touches(-4f), past = Touches(4f);
                    if (touch && !past) pass++; else off.Add(w.Name + (extra > 0 ? " +2" : "") + " (orb: touching hit " + touch + ", past hit " + past + ")");
                    continue;
                }
                if (w.Id == WeaponId.VeinTether || w.Id == WeaponId.Implosion || w.Id == WeaponId.LeechBeam || w.Id == WeaponId.LightBolt || w.Id == WeaponId.ChainLight || w.Id == WeaponId.ArcLightning)
                {
                    // A beam, an implosion, a tendril, a repulsion blast, a cone of light or a strip of lightning has no shots to follow: an enemy just inside its range is hit, one just past it isn't.
                    Func<float, bool> Hits = edge =>
                    {
                        g.Enemies.Clear();
                        var dummy = (Enemy)typeof(Game).GetMethod("SpawnEnemyAt", flags).Invoke(g, new object[] { EnemyType.Grunt, new V2(120f, 450f), 0f, 0, false });
                        dummy.Pos = new V2(120f + edge + dummy.Radius, 450f);
                        dummy.Hp = dummy.MaxHp = 1e5f; dummy.SpawnT = 0f;
                        me.AimAt = dummy.Pos;  // an implosion goes where you click
                        volley.Invoke(g, new object[] { me, 0f, extra });
                        host.Poll(); dummy.FrozenT = 1f; dummy.Pos = new V2(120f + edge + dummy.Radius, 450f); me.Pos = new V2(120f, 450f);
                        g.FixedStep(dt, new PlayerInput { AimAt = new V2(1500f, 450f) });
                        return dummy.Hp < dummy.MaxHp;
                    };
                    bool inside = Hits(range - 4f), outside = Hits(range + (w.Id == WeaponId.VeinTether ? Game.VeinBeamHalfWidth : 0f) + 4f);
                    if (inside && !outside) pass++; else off.Add(w.Name + (extra > 0 ? " +2" : "") + " (inside hit " + inside + ", outside hit " + outside + ")");
                    continue;
                }
                if (w.Id == WeaponId.Kusarigama)
                {
                    // The weight is state, not an effect: the aim line shows how far it is thrown (Game.KusariReachOf).
                    if (Math.Abs(range - g.KusariReachOf(me)) < 0.5f) pass++; else off.Add(w.Name + (extra > 0 ? " +2" : "") + " (throws " + g.KusariReachOf(me).ToString("0") + " vs " + range.ToString("0") + ")");
                    continue;
                }
                if (w.Id == WeaponId.WispLantern || w.Id == WeaponId.ImpCaller)
                {
                    // No shots: your companions are sent (WispLantern/ImpCaller tests); the aim line shows how far.
                    if (Math.Abs(range - (w.Id == WeaponId.WispLantern ? Game.WispReach : Game.ImpReach)) < 0.5f) pass++; else off.Add(w.Name + " (" + range.ToString("0") + ")");
                    continue;
                }
                if (w.Id == WeaponId.Nimbus || w.Id == WeaponId.SpiritChalk)
                {
                    // No shots: the cloud drifts to your cursor (and no further than its leash); the chalk draws to it (and no further than its reach).
                    g.Ink.Clear();
                    V2 stand = new V2(120f, 450f);
                    for (int f = 0; f < 5; f++) { me.Pos = stand; host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = new V2(1500f, 450f) }); }  // let go of the chalk first, so this is a new stroke
                    for (int f = 0; f < 240; f++)
                    {
                        me.Pos = stand;
                        host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = new V2(1500f, 450f), Fire = w.Id == WeaponId.SpiritChalk });
                    }
                    float reached = 0f;
                    foreach (var m in g.Minions.Values) if (m.Owner == 1 && m.Kind == MinionKind.Nimbus) reached = Math.Max(reached, m.Pos.DistanceTo(stand));
                    foreach (var d in g.Ink) reached = Math.Max(reached, d.Pos.DistanceTo(stand));
                    if (Math.Abs(reached - range) <= 12f) pass++; else off.Add(w.Name + (extra > 0 ? " +2" : "") + " (reached " + reached.ToString("0") + " vs " + range.ToString("0") + ")");
                    continue;
                }
                volley.Invoke(g, new object[] { me, 0f, extra });
                if (w.Id == WeaponId.StormHammer)  // the hammer leaves the hand a moment after the click (StormHammer.cs)
                    for (int f = 0; f < 15; f++) { me.Pos = new V2(120f, 450f); host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = new V2(1500f, 450f) }); }
                var first = new HashSet<int>(g.Bullets.Values.Where(b => b.Owner == 1).Select(b => b.Id));
                float far = 0f;
                for (int f = 0; f < 150; f++)
                {
                    g.Enemies.Clear();
                    me.Pos = new V2(120f, 450f);
                    if (w.Id == WeaponId.Lightbringer)  // its crescent leaves as the cut lands, a moment after the click (Lightbringer.cs)
                        foreach (var b in g.Bullets.Values) if (b.Owner == 1) first.Add(b.Id);
                    foreach (var b in g.Bullets.Values)
                        if (first.Contains(b.Id))
                            far = Math.Max(far, b.Pos.DistanceTo(new V2(120f, 450f)));
                    foreach (var fx in g.Effects)
                        if (fx.Kind == FxKind.Slash || fx.Kind == FxKind.Jab || fx.Kind == FxKind.Claw || fx.Kind == FxKind.Icicle || fx.Kind == FxKind.RootLash || fx.Kind == FxKind.TwinStep || fx.Kind == FxKind.CenserStep || fx.Kind == FxKind.LanceStep)
                            far = Math.Max(far, fx.Pos2.DistanceTo(fx.Pos));
                    host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = new V2(1500f, 450f) });
                }
                // Shots move in 1/60 s steps, so they can stop up to a step short; Arcane Missiles curve.
                float step = w.Melee && range < 200f ? 2f : 1700f / 60f;
                bool ok = w.Id == WeaponId.ArcaneMissiles ? far <= range + 2f && far > range * 0.6f : Math.Abs(far - range) <= step + 4f;
                if (ok) pass++; else off.Add(w.Name + (extra > 0 ? " +2" : "") + " (" + far.ToString("0") + " vs " + range.ToString("0") + ")");
            }
        }
        Console.WriteLine("  weapon ranges: " + pass + "/" + Defs.Weapons.Length * 2 + " match" + (off.Count > 0 ? "; off: " + string.Join(", ", off) : ""));
        Check(off.Count == 0, "the aim line's length matches how far each weapon's shots and swings reach");
        host.Leave("");
    }

    // Aim precision: a tiny change in where you point (a quarter of a degree) shows up exactly in
    // the direction of your shots, for the host and for a player who joined.
    static void AimTest(float dt)
    {
        int port = nextPort++;
        var host = new Session { AutoStart = 2, StartWave = 1 };
        host.Host(port, "Host", Role.Healer, 1);  // Pistol
        var client = JoinAs(port, false, "Friend", Role.Healer, 1);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        bool ok = true;
        float worst = 0f;
        foreach (var who in new[] { host, client })
        {
            var me = who.Game.Players[who.Game.MyId];
            foreach (float deg in new[] { 30f, 30.25f, 30.5f })
            {
                float want = deg * MathX.Pi / 180f;
                var seen = new HashSet<int>(host.Game.Bullets.Keys);
                float got = float.NaN;
                for (int i = 0; i < 90 && float.IsNaN(got); i++)
                {
                    V2 target = me.Pos + V2.FromAngle(want) * 400f;
                    foreach (var x in s)
                    {
                        x.Poll();
                        x.Game.FixedStep(dt, x == who ? new PlayerInput { AimAt = target, Fire = true } : new PlayerInput { AimAt = new V2(0f, 0f) });
                    }
                    Thread.Sleep(1);
                    foreach (var b in host.Game.Bullets.Values)
                        if (b.Owner == me.Id && !seen.Contains(b.Id))
                            got = b.Vel.Angle;
                }
                float err = Math.Abs(got - want) * 180f / MathX.Pi;
                worst = Math.Max(worst, float.IsNaN(err) ? 99f : err);
                ok &= !float.IsNaN(got) && err < 0.05f;
                // Let go of the trigger and wait out the fire delay before the next angle.
                for (int i = 0; i < 30; i++) foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, new PlayerInput { AimAt = me.Pos + V2.FromAngle(want) * 400f }); }
            }
        }
        Console.WriteLine("  aim: worst difference between aimed and fired direction {0:0.000} degrees", worst);
        Check(ok, "shots go exactly where you aim, to a quarter of a degree (host and joining player)");

        // The aim debug overlay's numbers: ping, and the client's record of its last shot.
        var cme = client.Game.Players[client.Game.MyId];
        float shotOff = Math.Abs(MathX.Fmod(client.Game.LastShotAngle - cme.Aim + MathX.Pi, MathX.Tau) - MathX.Pi) * 180f / MathX.Pi;
        Console.WriteLine("  aim debug: client ping {0:0.0} ms, last shot {1:0.00} degrees off its aim", client.Game.PingMs, shotOff);
        Check(client.Game.PingMs > 0f && client.Game.PingMs < 500f && client.Game.LastShotId != 0 && shotOff < 0.05f,
            "the aim debug overlay gets a ping and the direction of your last shot");
        host.Leave(""); client.Leave("");
    }

    static void FrostLanceTest(float dt)
    {
        // The Frost Lance: an icicle juts out from you, then splinters off its sides into enemies beside it. Both slow.
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var volley = typeof(Game).GetMethod("FireVolley", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 1 }, port, false, "Solo", Role.Tank, 1, 0, Kit.Elementalist);
        var g = host.Game;
        host.StartMatch();
        var me = g.Players[1];
        g.DevSetGod(1, true);
        Check(me.Weapon.Id == WeaponId.FrostLance, "the Mage Tank's second weapon is the Frost Lance");
        g.Enemies.Clear(); g.Bullets.Clear();
        me.Pos = new V2(200f, 450f);
        var spots = new[] { new V2(350f, 450f), new V2(300f, 495f), new V2(300f, 560f), new V2(480f, 450f) };  // on the line, beside it, too far beside, past its point
        var dummies = spots.Select(at => { var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false }); e.Hp = e.MaxHp = 1e5f; e.SpawnT = 0f; return e; }).ToArray();
        Action pin = () => { for (int k = 0; k < dummies.Length; k++) { dummies[k].Pos = spots[k]; dummies[k].FrozenT = 1f; } me.Pos = new V2(200f, 450f); };
        Action<int> step = n => { for (int f = 0; f < n; f++) { pin(); host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = new V2(900f, 450f) }); } };
        volley.Invoke(g, new object[] { me, 0f, 0 });
        step(2);
        float[] early = dummies.Select(e => e.MaxHp - e.Hp).ToArray();
        step(20);
        float[] took = dummies.Select(e => e.MaxHp - e.Hp).ToArray();
        Console.WriteLine("  frost lance: line {0:0.0} (after 2 ticks {1:0.0}), beside {2:0.0} (after 2 ticks {3:0.0}), far beside {4:0.0}, past the point {5:0.0}; bullets {6}",
            took[0], early[0], took[1], early[1], took[2], took[3], g.Bullets.Values.Count(b => b.Owner == 1));
        Check(early[0] > 0f && took[0] == early[0] && took[2] == 0f && took[3] == 0f && !g.Bullets.Values.Any(b => b.Owner == 1),
            "the Frost Lance is an icicle out from you to its reach: no flying spear, only what's on its line is hit at once (and only once)");
        Check(early[1] == 0f && Math.Abs(took[1] - took[0] * Game.SplinterMult) < 0.5f,
            "a moment later it splinters off its sides into enemies beside it for half damage");
        Check(dummies[0].SlowT > 0f && dummies[1].SlowT > 0f && dummies[0].SlowMult < 0.75f && dummies[1].SlowMult < 0.75f, "the icicle and its splinters both slow");
        Check(Math.Abs(g.WeaponRange(me) - Game.MeleeReach(me, me.Weapon.Reach)) < 0.5f, "the aim line reaches as far as the icicle");
        host.Leave("");
    }

    static void StormHammerTest(float dt)
    {
        // The Storm Hammer: a spinning hammer smashes one enemy (most of the damage) and electrifies it; it arcs a
        // little lightning to the enemies round it, three times over a second.
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var volley = typeof(Game).GetMethod("FireVolley", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 1 }, port, false, "Solo", Role.Tank, 2, 0, Kit.Elementalist);
        var g = host.Game;
        host.StartMatch();
        var me = g.Players[1];
        g.DevSetGod(1, true);
        Check(me.Weapon.Id == WeaponId.StormHammer, "the Mage Tank's third weapon is the Storm Hammer");
        g.Enemies.Clear(); g.Bullets.Clear();
        var spots = new[] { new V2(400f, 450f), new V2(400f, 540f), new V2(400f, 360f), new V2(470f, 530f), new V2(400f, 700f) };  // the target, three round it, one too far
        var dummies = spots.Select(at => { var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false }); e.Hp = e.MaxHp = 1e5f; e.SpawnT = 0f; return e; }).ToArray();
        Action<float> step = secs => { for (int f = 0; f < secs * 60; f++) { for (int k = 0; k < dummies.Length; k++) { dummies[k].Pos = spots[k]; dummies[k].FrozenT = 1f; } me.Pos = new V2(200f, 450f); host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = new V2(900f, 450f) }); } };
        me.Pos = new V2(200f, 450f);
        volley.Invoke(g, new object[] { me, 0f, 0 });
        bool early = g.Bullets.Values.Any(b => b.Owner == 1 && b.Kind == Defs.HammerShot);  // the hammer is still in the hand: it leaves HammerThrowRelease after the click
        step(Game.HammerThrowRelease + 0.04f);
        bool hammer = !early && g.Bullets.Values.Any(b => b.Owner == 1 && b.Kind == Defs.HammerShot);
        float landed = 0f;
        for (int f = 0; f < 60 && landed == 0f; f++) { step(1f / 60f); landed = dummies[0].MaxHp - dummies[0].Hp; }
        float first = dummies[1].MaxHp - dummies[1].Hp;
        step(1.2f);
        float[] took = dummies.Select(e => e.MaxHp - e.Hp).ToArray();
        Console.WriteLine("  storm hammer: target {0:0.0}; round it {1:0.0} / {2:0.0} / {3:0.0} (first arc {4:0.0}); too far {5:0.0}",
            took[0], took[1], took[2], took[3], first, took[4]);
        Check(hammer && landed > 0f && took[0] == landed, "the Storm Hammer flies as a spinning hammer and smashes the enemy it hits (the arcs never hit it)");
        Check(Math.Abs(first - landed * Game.HammerArcMult) < 0.2f && took.Skip(1).Take(3).All(d => Math.Abs(d - landed * Game.HammerArcMult * Game.ShockPulses) < 0.3f) && took[4] == 0f,
            "the electrified enemy arcs a little lightning to 3 enemies round it, three times, and not to one too far away");
        Check(took[1] < took[0] * 0.5f, "most of the damage is the hammer's");
        host.Leave("");
    }

    static void StormHammerSlamTest(float dt)
    {
        // The Storm Hammer's Alt slam: after its wind-up a shockwave runs out from a point ahead of you and hurts every enemy it
        // crosses once (and electrifies it), not the ones beyond its reach; the key has a cooldown.
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var slam = typeof(Game).GetMethod("HostHammerSlam", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 1 }, port, false, "Solo", Role.Tank, 2, 0, Kit.Elementalist);
        var g = host.Game;
        host.StartMatch();
        var me = g.Players[1];
        g.DevSetGod(1, true);
        g.Enemies.Clear(); g.Bullets.Clear(); g.Effects.Clear();
        var spots = new[] { new V2(330f, 450f), new V2(480f, 450f), new V2(800f, 450f) };  // near the crater (252, 450), inside the reach, far outside it
        var dummies = spots.Select(at => { var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false }); e.Hp = e.MaxHp = 1e5f; e.SpawnT = 0f; return e; }).ToArray();
        Action<float> step = secs => { for (int f = 0; f < secs * 60; f++) { for (int k = 0; k < dummies.Length; k++) { dummies[k].Pos = spots[k]; dummies[k].FrozenT = 1f; } me.Pos = new V2(200f, 450f); host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = new V2(900f, 450f) }); } };
        me.Pos = new V2(200f, 450f); me.Aim = 0f;
        slam.Invoke(g, new object[] { me, new V2(600f, 450f) });
        slam.Invoke(g, new object[] { me, new V2(600f, 450f) });  // (pressed again: the cooldown refuses it)
        Check(g.Effects.Count(e => e.Kind == FxKind.HammerThrow) == 1, "the slam's wind-up is one HammerThrow effect, and a second press inside the cooldown is refused");
        step(Game.HammerSlamWind - 0.1f);
        Check(dummies.All(e => e.Hp == e.MaxHp) && !g.Effects.Any(e => e.Kind == FxKind.HammerSlam), "nothing is hurt during the wind-up");
        step(0.1f + 1.2f);
        float[] took = dummies.Select(e => e.MaxHp - e.Hp).ToArray();
        Console.WriteLine("  hammer slam: near {0:0.0}, inside {1:0.0}, outside {2:0.0}", took[0], took[1], took[2]);
        Check(g.Effects.Any(e => e.Kind == FxKind.HammerSlam) || took[0] > 0f, "the impact sends the shockwave effect");
        Check(took[0] >= me.Weapon.Damage * Game.HammerSlamDmg && took[1] >= me.Weapon.Damage * Game.HammerSlamDmg, "the wave hurts every enemy it crosses for the slam's damage (plus the electrify arcs)");
        Check(took[2] == 0f, "an enemy beyond the wave's reach is not hurt");
        host.Leave("");
    }

    static void GolemStaffTest(float dt)
    {
        // The Golem Staff: hold to charge, let go to lob a boulder over enemies; the charge sets how far (and how hard)
        // it lands, and it crushes everything under it. The Golem still fights beside you.
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var volley = typeof(Game).GetMethod("FireVolley", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 1 }, port, false, "Solo", Role.Tank, 0, 0, Kit.Summoner);
        var g = host.Game;
        host.StartMatch();
        var me = g.Players[1];
        g.DevSetGod(1, true);
        for (int f = 0; f < 30; f++) { host.Poll(); g.FixedStep(dt, new PlayerInput()); }
        Check(me.Weapon.Id == WeaponId.GolemStaff && Game.ChargeWeapon(WeaponId.GolemStaff) && g.Minions.Values.Any(m => m.Owner == 1 && m.Companion),
            "the Summoner Tank's Golem Staff charges like a bow, and its Golem is still there");
        var golem = g.Minions.Values.First(m => m.Owner == 1 && m.Kind == MinionKind.Golem);
        for (int f = 0; f < 120; f++) { host.Poll(); g.FixedStep(dt, new PlayerInput()); }
        Check(Math.Abs(golem.Scale - Game.GolemScale) < 1e-4f && Game.TitanBlocking(golem) && golem.BlockHp > 300f && golem.BlockHp < 500f,
            "the Golem is a small Titan: scaled to " + Game.GolemScale + ", risen and standing, soaking about 400");
        V2 from = new V2(200f, 450f);
        Func<V2[], float, float[]> Throw = (spots, draw) =>
        {
            g.Enemies.Clear(); g.Bullets.Clear(); g.Minions.Clear();
            var dummies = spots.Select(at => { var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false }); e.Hp = e.MaxHp = 1e5f; e.SpawnT = 0f; return e; }).ToArray();
            me.Pos = from;
            me.BowShotDraw = draw;
            volley.Invoke(g, new object[] { me, 0f, 0 });
            for (int f = 0; f < 60; f++)
            {
                for (int k = 0; k < dummies.Length; k++) { dummies[k].Pos = spots[k]; dummies[k].FrozenT = 1f; }
                me.Pos = from;
                g.Minions.Clear();  // only the boulder's damage counts here
                host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = new V2(900f, 450f) });
            }
            return dummies.Select(e => e.MaxHp - e.Hp).ToArray();
        };
        float far = Game.BoulderDistance(1f), near = Game.BoulderDistance(0f);
        // Full charge: lands at its farthest. Under the throw and well beside the landing are untouched.
        var full = Throw(new[] { from + new V2(far, 0f), from + new V2(far, 60f), from + new V2(far * 0.5f, 0f), from + new V2(far, 120f) }, g.BowFull(me));
        Console.WriteLine("  golem staff, full: landing {0:0.0}, 60 px off {1:0.0}, under the throw {2:0.0}, 120 px off {3:0.0}", full[0], full[1], full[2], full[3]);
        Check(full[0] > 0f && full[1] == full[0] && full[2] == 0f && full[3] == 0f,
            "a fully charged boulder flies over enemies and crushes everything where it lands, at its farthest");
        // A tap: lands close, for half damage, and doesn't reach the far spot.
        var tap = Throw(new[] { from + new V2(near, 0f), from + new V2(far, 0f) }, Game.BowMinDraw);
        Console.WriteLine("  golem staff, tap: near {0:0.0}, far {1:0.0}", tap[0], tap[1]);
        Check(Math.Abs(tap[0] - full[0] * Game.BoulderTap) < 0.5f && tap[1] == 0f, "a tap lobs it close, for half the damage");
        me.Pos = from;
        Check(Math.Abs(g.WeaponRange(me) - near * me.Stats.ShotSpeedMult) < 0.5f, "the aim line shows where an uncharged boulder lands");
        host.Leave("");
    }

    static void BoneGuardTest(float dt)
    {
        // The Bone Guard: darts stick in enemies and the third cages one (not bosses); caged enemies can't move, and the
        // Skeleton Guards shoot at them. A broken cage can't be rebuilt for a moment.
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var dart = typeof(Game).GetMethod("BoneDartHit", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2 }, port, false, "Tank", Role.Tank, 1, 0, Kit.Summoner);
        var client = JoinAs(port, false, "Healer", Role.Healer, 0);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        Check(me.Weapon.Id == WeaponId.BoneGuard, "the Summoner Tank's second weapon is the Bone Guard");
        g.Bot = client.Game.Bot = false;
        V2 from = new V2(500f, 450f), at = new V2(650f, 450f);
        Enemy target = null;
        var seen = new HashSet<int>();
        int guardShots = 0;
        Action<float, bool> play = (secs, fire) =>
        {
            for (int f = 0; f < secs * 60; f++)
            {
                foreach (var e in g.Enemies.Values) if (e != target) e.Hp = 0f;
                me.Pos = from;
                if (target != null) { target.Pos = at; target.FireT = 99f; }
                host.Poll(); g.FixedStep(dt, new PlayerInput { Fire = fire, AimAt = at }); g.Update(dt);
                client.Poll(); client.Game.FixedStep(dt, new PlayerInput()); client.Game.Update(dt);
                foreach (var b in g.Bullets.Values)
                    if (b.Owner == 1 && b.Kind == Defs.MinionShot && seen.Add(b.Id)) guardShots++;
            }
        };
        play(0.3f, false);
        target = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false });
        target.Hp = target.MaxHp = 1e6f; target.SpawnT = 0f;
        play(0.3f, false);
        int shotsBefore = guardShots;
        float caged = -1f;
        for (int f = 0; f < 120 && caged < 0f; f++) { play(1f / 60f, true); if (target.CageT > 0f) caged = f / 60f; }
        Console.WriteLine("  bone guard: caged after {0:0.00} s of firing (guard shots before {1})", caged, shotsBefore);
        Check(caged > 0f && caged < 1.2f && Math.Abs(target.CageT - Game.BoneCageTime) < 0.1f, "three Bone Guard darts in one enemy cage it for 1.5 s");
        play(0.6f, false);
        bool clientSaw = client.Game.Enemies.ContainsKey(target.Id) && client.Game.Enemies[target.Id].Caged;
        int shotsWhileCaged = guardShots - shotsBefore;
        Console.WriteLine("  bone guard: guard shots while caged {0}, client sees the cage {1}", shotsWhileCaged, clientSaw);
        Check(shotsBefore == 0 && shotsWhileCaged >= 2 && clientSaw, "the Skeleton Guards shoot at a caged enemy (not before), and everyone sees the cage");
        play(1f, false);
        Check(target.CageT <= 0f && !target.Caged && target.CageImmuneT > 0f, "the cage breaks after 1.5 s");
        var bone = new Bullet { Kind = Defs.BoneShot, Owner = 1 };
        for (int k = 0; k < 3; k++) dart.Invoke(g, new object[] { bone, target });
        bool rebuiltAtOnce = target.CageT > 0f;
        play(Game.BoneCageImmune + 0.1f, false);
        for (int k = 0; k < 3; k++) dart.Invoke(g, new object[] { bone, target });
        Check(!rebuiltAtOnce && target.CageT > 0f, "a broken cage can't be rebuilt for a second, then it can");
        var boss = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Boss, at, 0f, 0, false });
        for (int k = 0; k < 3; k++) dart.Invoke(g, new object[] { bone, boss });
        Check(boss.CageT <= 0f, "bosses can't be caged");
        host.Leave(""); client.Leave("");
    }

    static void LightConeTest(float dt)
    {
        // Chain Light: a cone of light whose damage is shared between every enemy in it; light arcs to each.
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var volley = typeof(Game).GetMethod("FireVolley", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 1 }, port, false, "Solo", Role.Healer, 0, 0, Kit.Elementalist);
        var g = host.Game;
        host.StartMatch();
        var me = g.Players[1];
        g.DevSetGod(1, true);
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.ChainLight);
        V2 from = new V2(300f, 450f);
        Func<V2[], float[]> Cast = spots =>
        {
            g.Enemies.Clear(); g.Effects.Clear();
            var dummies = spots.Select(at => { var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false }); e.Hp = e.MaxHp = 1e5f; e.SpawnT = 0f; return e; }).ToArray();
            me.Pos = from;
            volley.Invoke(g, new object[] { me, 0f, 0 });
            return dummies.Select(e => e.MaxHp - e.Hp).ToArray();
        };
        var one = Cast(new[] { from + new V2(200f, 0f) });
        int arcs1 = g.Effects.Count(fx => fx.Kind == FxKind.Bolt);
        var four = Cast(new[] { from + new V2(150f, 0f), from + new V2(200f, 60f), from + new V2(250f, -80f), from + new V2(120f, 30f), from + new V2(-150f, 0f), from + new V2(150f, 260f) });
        int arcs4 = g.Effects.Count(fx => fx.Kind == FxKind.Bolt);
        bool cone = g.Effects.Any(fx => fx.Kind == FxKind.LightCone);
        Console.WriteLine("  chain light: one enemy takes {0:0.0}; four share it: {1:0.0} / {2:0.0} / {3:0.0} / {4:0.0}; behind {5:0.0}, off to the side {6:0.0}; arcs {7} then {8}",
            one[0], four[0], four[1], four[2], four[3], four[4], four[5], arcs1, arcs4);
        Check(cone && one[0] > 0f && arcs1 == 1, "Chain Light flashes a cone of light, and one enemy in it takes all of its damage");
        Check(four.Take(4).All(d => Math.Abs(d - one[0] / 4f) < 0.2f) && four[4] == 0f && four[5] == 0f && arcs4 == 4,
            "with four enemies in the cone each takes a quarter, light arcs to each, and nothing outside it is hit");
        // Barrage: more cones side by side, each sharing its own damage; a lone enemy is still in just one.
        var fanned = typeof(Game).GetMethod("FireFanned", flags);
        Func<V2[], int, float[]> CastFanned = (spots, fan) =>
        {
            g.Enemies.Clear(); g.Effects.Clear();
            var dummies = spots.Select(at => { var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false }); e.Hp = e.MaxHp = 1e5f; e.SpawnT = 0f; return e; }).ToArray();
            me.Pos = from; me.Stats.Fan = fan;
            fanned.Invoke(g, new object[] { me, 0f, 0 });
            me.Stats.Fan = 0;
            return dummies.Select(e => e.MaxHp - e.Hp).ToArray();
        };
        V2 beside = from + V2.FromAngle(2f * Game.LightConeHalf) * 200f;  // the middle of the first Barrage cone
        var lone = CastFanned(new[] { from + new V2(200f, 0f) }, 3);
        int cones = g.Effects.Count(fx => fx.Kind == FxKind.LightCone);
        var noBarrage = CastFanned(new[] { beside }, 0);
        var withBarrage = CastFanned(new[] { from + new V2(200f, 0f), beside }, 1);
        Console.WriteLine("  chain light + barrage: lone enemy takes {0:0.0} with 3 stacks ({1} cones); beside the cone {2:0.0} without, {3:0.0} with (and the one ahead {4:0.0})",
            lone[0], cones, noBarrage[0], withBarrage[1], withBarrage[0]);
        Check(cones == 4 && Math.Abs(lone[0] - one[0]) < 0.2f, "Barrage on Chain Light adds cones, but a lone enemy still takes one cone's damage");
        Check(noBarrage[0] == 0f && Math.Abs(withBarrage[1] - one[0] * Game.BarrageCopyDamage) < 0.2f && Math.Abs(withBarrage[0] - one[0]) < 0.2f,
            "each Barrage cone sits beside the first and shares out its own damage (60%, like other weapons' Barrage copies)");
        // Widened by Homing and Magnum Rounds and fanned by Barrage, it still never reaches round behind you.
        me.Stats.Homing = 6f; float size0 = me.Stats.ShotSizeMult; me.Stats.ShotSizeMult = 1.6f;
        float spread = 2f * Game.LightConeHalfFor(me, 3) * 3f;
        var round = CastFanned(new[] { from + new V2(-200f, 0f), from + new V2(0f, 200f) }, 2);
        me.Stats.Homing = 0f; me.Stats.ShotSizeMult = size0;
        Console.WriteLine("  chain light, 3 Homing + big Magnum + 2 Barrage: cones cover {0:0} degrees; behind you {1:0.0}, beside you {2:0.0}",
            spread * 180f / MathX.Pi, round[0], round[1]);
        Check(spread <= Game.LightConeMaxSpread + 1e-3f && round[0] == 0f, "Chain Light's cones never make a ring: at most 200 degrees, nothing behind you is hit");
        host.Leave("");
    }

    static void SanguineOrbTest(float dt)
    {
        // The Sanguine Orb: flies straight, homes at the last moment, circles in and coats the enemy (its hit and a bleed
        // stack land as it closes), charging the heal reserve; with enough reserve it coats a hurt ally in healing blood.
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var volley = typeof(Game).GetMethod("FireVolley", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2 }, port, false, "Healer", Role.Healer, 0, 0, Kit.Hemomancer);
        var client = JoinAs(port, false, "Tank", Role.Tank, 0);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        var ally = g.Players[2];
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        g.Bot = client.Game.Bot = false;
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.SanguineOrb);
        V2 from = new V2(300f, 450f), allyAt = new V2(300f, 200f);
        var keep = new List<Enemy>();
        Action<float> play = secs =>
        {
            for (int f = 0; f < secs * 60; f++)
            {
                foreach (var e in g.Enemies.Values) if (!keep.Contains(e)) e.Hp = 0f;
                foreach (var e in keep) e.FrozenT = 1f;
                me.Pos = from; ally.Pos = client.Game.Me.Pos = allyAt;  // the client sends its own position
                host.Poll(); g.FixedStep(dt, new PlayerInput()); g.Update(dt);
                client.Poll(); client.Game.FixedStep(dt, new PlayerInput()); client.Game.Update(dt);
            }
        };
        Func<V2, Enemy> dummy = at => { var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false }); e.Hp = e.MaxHp = 1e5f; e.SpawnT = 0f; keep.Add(e); return e; };
        play(0.3f);
        me.BloodReserve = 0f;
        // An enemy 40 px off the line of fire: the orb homes onto it at the last moment. One 150 px off is left alone.
        var near = dummy(from + new V2(260f, 40f));
        var wide = dummy(from + new V2(200f, 150f));
        volley.Invoke(g, new object[] { me, 0f, 0 });
        float caught = -1f;
        for (int f = 0; f < 90 && caught < 0f; f++) { play(1f / 60f); if (g.Effects.Any(fx => fx.Kind == FxKind.BloodCoat)) caught = f / 60f; }
        float during = near.MaxHp - near.Hp;
        play(Game.CoatTime + 0.05f);
        float hit = near.MaxHp - near.Hp, bleed1 = near.BleedDps;
        Console.WriteLine("  sanguine orb: caught after {0:0.00} s; {1:0.0} while circling, {2:0.0} once covered; bleed {3:0.0}/s; reserve {4:0}; the wide one {5:0.0}",
            caught, during, hit, bleed1, me.BloodReserve, wide.MaxHp - wide.Hp);
        Check(caught > 0f && during == 0f && hit > 0f && wide.Hp == wide.MaxHp, "a Sanguine Orb homes onto an enemy near its path at the last moment, circles it, and hits once it's covered");
        Check(near.BleedT > 0f && Math.Abs(me.BloodReserve - Game.ReserveGain) < 0.5f && client.Game.Players[1].BloodReserve == me.BloodReserve,
            "covering an enemy makes it bleed and charges the heal reserve (and the client sees it)");
        volley.Invoke(g, new object[] { me, (near.Pos - from).Angle, 0 });
        play(1f);
        Check(near.BleedDps > bleed1 * 1.5f, "each orb that covers an enemy adds a bleed stack");
        keep.Clear(); near.Hp = wide.Hp = 0f;
        // Shooting a hurt ally: nothing without enough reserve, a coat of healing blood with it.
        ally.Hp = ally.MaxHp * 0.3f;
        me.BloodReserve = Game.AllyCoatCost - 1f;
        volley.Invoke(g, new object[] { me, (allyAt - from).Angle, 0 });
        play(0.8f);
        bool noCoat = ally.CoatT <= 0f;
        me.BloodReserve = Game.AllyCoatCost + 5f;
        float h0 = ally.Hp;
        volley.Invoke(g, new object[] { me, (allyAt - from).Angle, 0 });
        play(0.8f);
        bool coated = ally.CoatT > 0f && client.Game.Players[2].CoatT > 0f;
        float reserveLeft = me.BloodReserve;
        play(Game.AllyCoatTime);
        Console.WriteLine("  sanguine orb: ally coated {0} (without reserve {1}); reserve left {2:0}; healed {3:0.0}", coated, !noCoat, reserveLeft, ally.Hp - h0);
        Check(noCoat && coated && Math.Abs(reserveLeft - 5f) < 0.5f && ally.Hp - h0 >= Game.AllyCoatHeal - 1f && ally.Hp - h0 < Game.AllyCoatHeal + 6f,  // (plus a little from the healer's aura)
            "with 25 reserve an orb coats a hurt ally in healing blood (30 health over 4 s); without it, nothing");
        host.Leave(""); client.Leave("");
    }

    static void FireballArcTest(float dt)
    {
        // The Fireball rolls on through a line of enemies, hitting and burning each. Arc Lightning charges a strip in
        // front of you (longer the longer you hold) and shocks everything in it when you let go.
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var volley = typeof(Game).GetMethod("FireVolley", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 1 }, port, false, "Solo", Role.Dps, 0, 0, Kit.Elementalist);
        var g = host.Game;
        host.StartMatch();
        var me = g.Players[1];
        g.DevSetGod(1, true);
        V2 from = new V2(200f, 450f);
        Func<V2[], Enemy[]> dummies = spots =>
        {
            g.Enemies.Clear(); g.Bullets.Clear(); g.Effects.Clear();
            return spots.Select(at => { var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false }); e.Hp = e.MaxHp = 1e5f; e.SpawnT = 0f; return e; }).ToArray();
        };
        Action<Enemy[], V2[], float> play = (ds, spots, secs) =>
        {
            for (int f = 0; f < secs * 60; f++)
            {
                for (int k = 0; k < ds.Length; k++) { ds[k].Pos = spots[k]; ds[k].FrozenT = Math.Max(ds[k].FrozenT, 0.01f); }
                me.Pos = from;
                host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = new V2(1500f, 450f) });
            }
        };

        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.Fireball);
        var line = new[] { from + new V2(150f, 0f), from + new V2(230f, 10f), from + new V2(310f, -10f) };
        var row = dummies(line);
        me.Pos = from;
        volley.Invoke(g, new object[] { me, 0f, 0 });
        var ball = g.Bullets.Values.FirstOrDefault(b => b.Owner == 1);
        play(row, line, 1.2f);
        Console.WriteLine("  fireball: radius {0}; hits {1:0.0} / {2:0.0} / {3:0.0}; all burning {4}", ball == null ? 0f : ball.Radius,
            row[0].MaxHp - row[0].Hp, row[1].MaxHp - row[1].Hp, row[2].MaxHp - row[2].Hp, row.All(e => e.BurnT > 0f || e.BurnDps > 0f));
        Check(ball != null && ball.Kind == Defs.FireballShot && ball.Radius >= 15f && row.All(e => e.Hp < e.MaxHp) && row.All(e => e.BurnDps > 0f),
            "the Fireball is big, rolls on through every enemy in its path and sets each burning");

        // It passes through everything already, so Piercing Rounds roll it 15% farther a stack instead.
        g.Bullets.Clear();
        volley.Invoke(g, new object[] { me, 0f, 0 });
        float life0 = g.Bullets.Values.First(b => b.Owner == 1).Life;
        float reach0 = g.WeaponRange(me);
        me.Stats.Pierce += 2;
        g.Bullets.Clear();
        volley.Invoke(g, new object[] { me, 0f, 0 });
        float life1 = g.Bullets.Values.First(b => b.Owner == 1).Life;
        float reach1 = g.WeaponRange(me);
        me.Stats.Pierce -= 2;
        g.Bullets.Clear();
        Console.WriteLine("  fireball with Piercing Rounds: lives {0:0.00} s (was {1:0.00}), reach {2:0} px (was {3:0})", life1, life0, reach1, reach0);
        Check(Math.Abs(life1 / life0 - 1.15f) < 0.01f && reach1 > reach0, "Piercing Rounds roll the Fireball 15% farther a stack (it passes through everything already)");

        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.ArcLightning);
        Check(Game.ChargeWeapon(WeaponId.ArcLightning), "Arc Lightning charges like a bow");
        float far = Game.ArcLength(me, 1f), near = Game.ArcLength(me, 0f);
        var strip = new[] { from + new V2(near - 30f, 0f), from + new V2(far - 30f, 20f), from + new V2(far + 60f, 0f), from + new V2(300f, Game.ArcHalfWidth + 60f), from + new V2(100f, 75f), from + new V2(far - 100f, 55f) };  // the last two: wide of the cone near you, inside it far out
        var ds2 = dummies(strip);
        me.BowShotDraw = Game.BowMinDraw;
        volley.Invoke(g, new object[] { me, 0f, 0 });
        float[] tap = ds2.Select(e => e.MaxHp - e.Hp).ToArray();
        play(ds2, strip, 0.6f);
        me.BowShotDraw = g.BowFull(me);
        volley.Invoke(g, new object[] { me, 0f, 0 });
        float[] full = ds2.Select((e, k) => e.MaxHp - e.Hp - tap[k]).ToArray();
        Console.WriteLine("  arc lightning: tap reaches {0:0} px, full {1:0} px (half the arena); tap hits {2:0.0} / {3:0.0}; full hits {4:0.0} / {5:0.0} / past it {6:0.0} / beside it {7:0.0} / wide near you {9:0.0} / wide far out {10:0.0}; stunned {8:0.00} s",
            near, far, tap[0], tap[1], full[0], full[1], full[2], full[3], ds2[1].FrozenT, full[4], full[5]);
        Check(tap[0] > 0f && tap[1] == 0f && full[0] > 0f && full[1] > 0f && full[2] == 0f && full[3] == 0f && full[4] == 0f && full[5] > 0f && Math.Abs(far - Game.Width * 0.5f) < 1f,
            "Arc Lightning shocks everything in its cone: a tap reaches a little way, a full charge half the arena, nothing past or beside it, and it is narrower near you than at its far end");
        Check(ds2[1].FrozenT > 0.3f, "its shock stuns briefly");
        // Barrage: parallel strips side by side; an enemy ahead is shocked once, one beside the strip only with Barrage.
        var fanned = typeof(Game).GetMethod("FireFanned", flags);
        var side = new[] { from + new V2(300f, 0f), from + new V2(300f, 2f * Game.ArcHalfWidth * Game.ArcTaper(300f, far)) };
        Func<int, float[]> Strip = fan =>
        {
            var ds = dummies(side);
            me.Pos = from; me.Stats.Fan = fan; me.BowShotDraw = g.BowFull(me);
            fanned.Invoke(g, new object[] { me, 0f, 0 });
            me.Stats.Fan = 0;
            return ds.Select(e => e.MaxHp - e.Hp).ToArray();
        };
        var plain = Strip(0);
        var barraged = Strip(3);
        int strips = g.Effects.Count(fx => fx.Kind == FxKind.ArcField);
        Console.WriteLine("  arc lightning + barrage: ahead {0:0.0} -> {1:0.0}, beside {2:0.0} -> {3:0.0}; {4} strips", plain[0], barraged[0], plain[1], barraged[1], strips);
        Check(strips == 4 && Math.Abs(barraged[0] - plain[0]) < 0.2f && plain[1] == 0f && Math.Abs(barraged[1] - plain[0]) < 0.2f,
            "Barrage on Arc Lightning adds parallel strips beside the first: more ground, but nothing is shocked twice");
        host.Leave("");
    }
}
