using System.Linq;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using Arena;

// Weapons: every weapon over the network, extra projectiles, range and aim, the Frost Lance, Revolver, Bow,
// Bear Totem, and the bullet budget.
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
                var host = new Session { AutoStart = 2, StartWave = 2 };
                host.Game.Bot = true;
                host.Host(port, "Host", roles[0], w);
                var client = new Session();
                client.Game.Bot = true;
                client.Join("127.0.0.1", port, "Friend", roles[1], w);
                port++;
                for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
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
        float pushed = front.Pos.X - f0.X;
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

        // The Warhammer: tap or charge, then slam a strip in front of you.
        me.Weapon = Defs.Weapon(Role.Tank, Kit.Knight, 0);
        Check(me.Weapon.Name == "Warhammer" && Game.ChargeWeapon(me.Weapon.Id), "the Warhammer charges up like a bow");
        queue.Clear(); g.Enemies.Clear(); g.Bullets.Clear();
        var front = Dummy(new V2(610f, 450f));
        var side = Dummy(new V2(560f, 560f));  // off to the side: inside a wide arc, outside the strip
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
        Check(side.Hp == side.MaxHp, "the slam hits a strip in front, not the sides");

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
            me.Weapon = w;
            me.Stats.ExtraShots = extra;
            me.AmmoUsed = 0;
            me.ReloadT = 0f;
            me.TotemCd = 0f;
            me.CompanionCd = 0f;
            int spawned = 0, totems = 0;
            var seen = new HashSet<int>();
            for (int f = 0; f < 6 * 60; f++)
            {
                queue.Clear();
                me.Pos = w.Melee ? center - new V2(Game.MeleeRange(w.Id) * 0.5f + 40f, 0f) : from;  // knights stand in reach
                ally.Pos = new V2(560f, 300f);
                foreach (var e in dummies) { e.Speed = 0f; e.FireT = 99f; e.Pos = e.Disp; }
                foreach (var x in s)
                {
                    x.Poll();
                    // The bow fires when you let go: draw past full (1.25 s), release, repeat.
                    x.Game.FixedStep(dt, x == s[0] ? new PlayerInput { AimAt = center, Fire = !Game.ChargeWeapon(w.Id) || f % 90 < 75 } : new PlayerInput());
                }
                foreach (var b in g.Bullets.Values)
                    if (b.Owner == 1 && seen.Add(b.Id)) spawned++;
                foreach (var z in g.Zones.Values)
                    if (z.Kind == ZoneKind.Totem && seen.Add(-z.Id)) totems++;
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
                case WeaponId.TotemStaff: ok = b[3] > a[3]; break;                       // totems more often
                case WeaponId.MendersBow: ok = b[1] >= a[1] * 2.5f && ratio > 1.2f; break;  // full draws loose extra arrows
                case WeaponId.BearTotem: ok = ratio > 1.1f; break;                        // claws plus the Bear's cleave (it wanders)
                case WeaponId.TreantRod: ok = Math.Abs(b[4] - 1.6f) < 0.01f; break;       // a bigger treant
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
        var host = new Session { AutoStart = 1 };
        host.Host(port, "Solo", Role.Dps, 0, 0, Kit.Soldier);
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
                volley.Invoke(g, new object[] { me, 0f, extra });
                var first = new HashSet<int>(g.Bullets.Values.Where(b => b.Owner == 1).Select(b => b.Id));
                float far = 0f;
                for (int f = 0; f < 150; f++)
                {
                    g.Enemies.Clear();
                    me.Pos = new V2(120f, 450f);
                    foreach (var b in g.Bullets.Values)
                        if (first.Contains(b.Id))
                            far = Math.Max(far, b.Pos.DistanceTo(new V2(120f, 450f)));
                    foreach (var fx in g.Effects)
                        if (fx.Kind == FxKind.Slash || fx.Kind == FxKind.Jab || fx.Kind == FxKind.Claw || fx.Kind == FxKind.Icicle)
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
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Healer, 1);
        var s = new[] { host, client };
        for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
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
        var host = new Session { AutoStart = 1 };
        host.Host(port, "Solo", Role.Tank, 1, 0, Kit.Mage);
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
        var host = new Session { AutoStart = 1 };
        host.Host(port, "Solo", Role.Tank, 2, 0, Kit.Mage);
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
        bool hammer = g.Bullets.Values.Any(b => b.Owner == 1 && b.Kind == Defs.HammerShot);
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

    static void GolemStaffTest(float dt)
    {
        // The Golem Staff: hold to charge, let go to lob a boulder over enemies; the charge sets how far (and how hard)
        // it lands, and it crushes everything under it. The Golem still fights beside you.
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var volley = typeof(Game).GetMethod("FireVolley", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        int port = nextPort++;
        var host = new Session { AutoStart = 1 };
        host.Host(port, "Solo", Role.Tank, 0, 0, Kit.Summoner);
        var g = host.Game;
        host.StartMatch();
        var me = g.Players[1];
        g.DevSetGod(1, true);
        for (int f = 0; f < 30; f++) { host.Poll(); g.FixedStep(dt, new PlayerInput()); }
        Check(me.Weapon.Id == WeaponId.GolemStaff && Game.ChargeWeapon(WeaponId.GolemStaff) && g.Minions.Values.Any(m => m.Owner == 1 && m.Companion),
            "the Summoner Tank's Golem Staff charges like a bow, and its Golem is still there");
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
        var host = new Session { AutoStart = 2 };
        host.Host(port, "Tank", Role.Tank, 1, 0, Kit.Summoner);
        var client = new Session();
        client.Join("127.0.0.1", port, "Healer", Role.Healer, 0);
        var s = new[] { host, client };
        for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
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
        var host = new Session { AutoStart = 1 };
        host.Host(port, "Solo", Role.Healer, 0, 0, Kit.Mage);
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
        Check(noBarrage[0] == 0f && Math.Abs(withBarrage[1] - one[0]) < 0.2f && Math.Abs(withBarrage[0] - one[0]) < 0.2f,
            "each Barrage cone sits beside the first and shares out its own damage");
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
        var host = new Session { AutoStart = 2 };
        host.Host(port, "Healer", Role.Healer, 0, 0, Kit.BloodMage);
        var client = new Session();
        client.Join("127.0.0.1", port, "Tank", Role.Tank, 0);
        var s = new[] { host, client };
        for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
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
        var host = new Session { AutoStart = 1 };
        host.Host(port, "Solo", Role.Dps, 0, 0, Kit.Mage);
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

        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.ArcLightning);
        Check(Game.ChargeWeapon(WeaponId.ArcLightning), "Arc Lightning charges like a bow");
        float far = Game.ArcLength(me, 1f), near = Game.ArcLength(me, 0f);
        var strip = new[] { from + new V2(near - 30f, 0f), from + new V2(far - 30f, 20f), from + new V2(far + 60f, 0f), from + new V2(300f, Game.ArcHalfWidth + 60f) };
        var ds2 = dummies(strip);
        me.BowShotDraw = Game.BowMinDraw;
        volley.Invoke(g, new object[] { me, 0f, 0 });
        float[] tap = ds2.Select(e => e.MaxHp - e.Hp).ToArray();
        play(ds2, strip, 0.6f);
        me.BowShotDraw = g.BowFull(me);
        volley.Invoke(g, new object[] { me, 0f, 0 });
        float[] full = ds2.Select((e, k) => e.MaxHp - e.Hp - tap[k]).ToArray();
        Console.WriteLine("  arc lightning: tap reaches {0:0} px, full {1:0} px (half the arena); tap hits {2:0.0} / {3:0.0}; full hits {4:0.0} / {5:0.0} / past it {6:0.0} / beside it {7:0.0}; stunned {8:0.00} s",
            near, far, tap[0], tap[1], full[0], full[1], full[2], full[3], ds2[1].FrozenT);
        Check(tap[0] > 0f && tap[1] == 0f && full[0] > 0f && full[1] > 0f && full[2] == 0f && full[3] == 0f && Math.Abs(far - Game.Width * 0.5f) < 1f,
            "Arc Lightning shocks everything in its strip: a tap reaches a little way, a full charge half the arena, nothing past or beside it");
        Check(ds2[1].FrozenT > 0.3f, "its shock stuns briefly");
        // Barrage: parallel strips side by side; an enemy ahead is shocked once, one beside the strip only with Barrage.
        var fanned = typeof(Game).GetMethod("FireFanned", flags);
        var side = new[] { from + new V2(300f, 0f), from + new V2(300f, 2f * Game.ArcHalfWidth) };
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

    static void ScatterFlakBloodTest(float dt)
    {
        // Scattergun: point-blank pellets do double and shove. Flak Cannon: shells fly over enemies to the cursor and
        // burst there, also destroying enemy bullets. Blood Bolts: the fifth in a row makes an enemy hemorrhage.
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var volley = typeof(Game).GetMethod("FireVolley", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
        int port = nextPort++;
        var host = new Session { AutoStart = 1 };
        host.Host(port, "Solo", Role.Tank, 0, 0, Kit.Soldier);
        var g = host.Game;
        host.StartMatch();
        var me = g.Players[1];
        g.DevSetGod(1, true);
        V2 from = new V2(200f, 450f);
        V2[] pinned = new V2[0];
        Enemy[] ds = new Enemy[0];
        Func<V2[], Enemy[]> place = spots =>
        {
            g.Enemies.Clear(); g.Bullets.Clear(); g.Effects.Clear();
            pinned = spots;
            ds = spots.Select(at => { var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false }); e.Hp = e.MaxHp = 1e5f; e.SpawnT = 0f; e.FireT = 99f; return e; }).ToArray();
            me.Pos = from;
            return ds;
        };
        Action<float, bool> step = (secs, pin) =>
        {
            for (int f = 0; f < secs * 60; f++)
            {
                for (int k = 0; k < ds.Length; k++) { if (pin) ds[k].Pos = pinned[k]; ds[k].FrozenT = 1f; }
                me.Pos = from;
                host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = me.AimAt });
            }
        };

        // Scattergun.
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.Scattergun);
        var sg = place(new[] { from + new V2(80f, 0f) });
        volley.Invoke(g, new object[] { me, 0f, 0 });
        bool flagged = g.Bullets.Values.Where(b => b.Owner == 1).All(b => b.PointBlank);
        Func<V2, float[]> Pellet = at =>
        {
            var e = place(new[] { at })[0];
            V2 start = e.Pos;
            var b = (Bullet)shoot.Invoke(g, new object[] { Defs.TankShot, from + new V2(me.Def.BodyR, 0f), new V2(520f, 0f), 0f, 1f, 7f, 1, false, 0f });
            b.PointBlank = true;
            step(0.6f, false);
            return new[] { e.MaxHp - e.Hp, e.Pos.X - start.X };
        };
        var close = Pellet(from + new V2(80f, 0f));
        var away = Pellet(from + new V2(260f, 0f));
        Console.WriteLine("  scattergun: pellets flagged {0}; a pellet point-blank {1:0.0} (shoved {2:0} px), at range {3:0.0} (shoved {4:0} px)", flagged, close[0], close[1], away[0], away[1]);
        Check(flagged && close[0] > away[0] * 1.9f && close[1] >= Game.ScatterShove - 1f && away[1] < 1f,
            "Scattergun pellets do double point-blank and shove the enemy back, not at range");

        // Flak Cannon.
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.FlakCannon);
        V2 cursor = from + new V2(260f, 0f);
        var fl = place(new[] { cursor, cursor + new V2(0f, 50f), from + new V2(130f, 0f) });
        me.AimAt = cursor;
        var foes = new List<Bullet>();
        for (int k = 0; k < 4; k++)
            foes.Add((Bullet)shoot.Invoke(g, new object[] { Defs.SmallShot, cursor + V2.FromAngle(k * 1.6f) * 40f, new V2(0f, 0f), 0f, 5f, 5f, 0, false, 0f }));
        var far = (Bullet)shoot.Invoke(g, new object[] { Defs.SmallShot, cursor + new V2(0f, 200f), new V2(0f, 0f), 0f, 5f, 5f, 0, false, 0f });
        volley.Invoke(g, new object[] { me, 0f, 0 });
        step(1f, true);
        float[] took = fl.Select(e => e.MaxHp - e.Hp).ToArray();
        int popped = foes.Count(b => !g.Bullets.ContainsKey(b.Id));
        Console.WriteLine("  flak cannon: at the cursor {0:0.0}, 50 px off {1:0.0}, under its path {2:0.0}; enemy bullets in the burst destroyed {3}/4, one far off kept {4}",
            took[0], took[1], took[2], popped, g.Bullets.ContainsKey(far.Id));
        Check(took[0] > 0f && took[1] > 0f && took[2] == 0f, "a Flak Cannon shell flies over enemies and bursts at the cursor");
        Check(popped == 4 && g.Bullets.ContainsKey(far.Id), "its burst destroys enemy bullets caught in it, not ones outside");
        g.Bullets.Clear();

        // Blood Bolts.
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.BloodBolts);
        me.Kit = Kit.BloodMage;
        var bb = place(new[] { from + new V2(200f, 0f), from + new V2(200f, 60f) });
        Func<int, float> Bolts = n =>
        {
            float before = bb[1].Hp;
            for (int k = 0; k < n; k++) { volley.Invoke(g, new object[] { me, 0f, 0 }); step(0.15f, true); }
            step(0.4f, true);
            return before - bb[1].Hp;
        };
        float four = Bolts(4);
        float fifth = Bolts(1);
        step(Game.HemorrhageHold + 0.2f, true);
        float faded = Bolts(1);
        Console.WriteLine("  blood bolts: neighbour took {0:0.0} after 4 bolts, {1:0.0} on the 5th; after they fade, one more bolt {2:0.0}", four, fifth, faded);
        Check(four == 0f && fifth > 0f && faded == 0f, "the fifth Blood Bolt in a row makes an enemy hemorrhage and burst onto enemies round it; the stacks fade");
        host.Leave("");
    }

    // The Flamethrower: a short stream whose flames hit the first enemy hardest, pass on through the rest for a third, and
    // set them burning, which goes on after you stop.
    static void FlamethrowerTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        int port = nextPort++;
        var host = new Session { AutoStart = 1 };
        host.Host(port, "Solo", Role.Tank, 0, 0, Kit.Mage);
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
        var host = new Session { AutoStart = 2, StartWave = 1 };
        host.Host(port, "Host", Role.Tank, 0);
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Healer, 1);  // Revolver
        var s = new[] { host, client };
        for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
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
        var host = new Session { AutoStart = 2, StartWave = 1 };
        host.Host(port, "Host", Role.Tank, 0);
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Healer, 2);  // Mender's Bow
        var s = new[] { host, client };
        for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
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
        var host = new Session { AutoStart = 2, StartWave = 3 };
        host.Host(port, "Tank", Role.Tank, 2, 0, Kit.Summoner);
        var client = new Session();
        client.Join("127.0.0.1", port, "Dps", Role.Dps);
        var s = new[] { host, client };
        for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
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
        Check(budget > 100 && max <= budget * 1.35f, "enemy bullets stay near the bullet budget");
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
        // A near miss (aimed 15 degrees off, 200 px away) bends in; a shot fired sideways doesn't hunt it down.
        var near = (Bullet)shoot.Invoke(g, new object[] { Defs.DpsShot, new V2(800f, 500f), V2.FromAngle(-MathX.Pi / 2f + 0.26f) * 600f, 0f, 1.5f, 10f, 2, false, 0f });
        var wide = (Bullet)shoot.Invoke(g, new object[] { Defs.DpsShot, new V2(800f, 500f), V2.FromAngle(0f) * 600f, 0f, 1.5f, 10f, 2, false, 0f });
        float wideAngle0 = wide.Vel.Angle, hpBefore = dummy.Hp;
        Run(s, dt, 0.6f, null, () => { dummy.FrozenT = 1f; });
        bool nearHit = dummy.Hp < hpBefore;
        float wideTurn = Math.Abs(wide.Vel.Angle - wideAngle0);
        Console.WriteLine("  homing x3: near miss hit {0}, sideways shot turned {1:0.00} rad, shot damage {2:0.0} of 10", nearHit, wideTurn, near.Dmg);
        Check(nearHit && wideTurn < 0.05f, "Homing Rounds fix near misses but don't hunt enemies off to the side");
        Check(Math.Abs(near.Dmg - 7f) < 0.01f, "Homing Rounds cost 10% damage per stack");

        // On area weapons Homing Rounds grow the area instead: a Flak Cannon's blast is 75% wider at 3 stacks and
        // doesn't bend; an Implosion reaches an enemy past its usual 55 px; the card says so.
        var hp = g.Players[2];
        var volley = typeof(Game).GetMethod("FireVolley", flags);
        hp.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.FlakCannon);
        g.Bullets.Clear();
        volley.Invoke(g, new object[] { hp, 0f, 0 });
        var ball = g.Bullets.Values.First(b => b.Owner == 2);
        Console.WriteLine("  homing x3 on a Flak Cannon: blast {0:0} px ({1:0} normally), turn {2:0}, damage {3:0.0}", ball.Splash, Game.FlakBurst, ball.Homing, ball.Dmg / hp.Stats.DmgMult);
        Check(Math.Abs(ball.Splash - Game.FlakBurst * 1.75f) < 0.01f && ball.Homing == 0f && Math.Abs(ball.Dmg / hp.Stats.DmgMult - hp.Weapon.Damage * 0.7f) < 0.05f,
            "on area weapons Homing Rounds widen the blast (25% a stack) instead of bending shots, for the same damage cost");
        // Leech Tendrils: the arc widens 70 -> 122.5 degrees, so an enemy 50 degrees off your aim can be grabbed.
        hp.Weapon = Defs.Weapon(Role.Tank, Kit.BloodMage, 2);
        V2 at = hp.Pos + V2.FromAngle(50f * MathX.Pi / 180f) * 100f;
        Check(hp.Weapon.Name == "Leech Tendrils" && Math.Abs(Game.GraspArcFor(hp) - Game.GraspArc * 1.75f) < 0.001f
            && Game.InGraspArc(hp.Pos, 0f, at, 5f, Game.GraspArcFor(hp)) && !Game.InGraspArc(hp.Pos, 0f, at, 5f),
            "Leech Tendrils with Homing Rounds reach across a wider arc (75% at 3 stacks)");
        hp.Weapon = Defs.Weapon(Role.Dps, Kit.BloodMage, 1);
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
        var host = new Session { AutoStart = 2 };
        host.Host(port, "Healer", Role.Healer, 0, 0, Kit.Mage);
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Dps);
        var s = new[] { host, client };
        for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
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
                ((List<EnemyType>)queueField.GetValue(g)).Clear();
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
}
