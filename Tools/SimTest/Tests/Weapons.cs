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
                me.Pos = w.Id == WeaponId.Kusarigama ? center - new V2(70f, 0f)  // within its reel slash, since this holds you still
                    : w.Melee ? center - new V2(Game.MeleeRange(w.Id) * 0.5f + 40f, 0f) : from;  // knights stand in reach
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
                if (w.Id == WeaponId.WispLantern || w.Id == WeaponId.ImpCaller)
                {
                    // No shots: your companions are sent (WispLantern/ImpCaller tests); the aim line shows how far.
                    if (Math.Abs(range - (w.Id == WeaponId.WispLantern ? Game.WispReach : Game.ImpReach)) < 0.5f) pass++; else off.Add(w.Name + " (" + range.ToString("0") + ")");
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
                        if (fx.Kind == FxKind.Slash || fx.Kind == FxKind.Jab || fx.Kind == FxKind.Claw || fx.Kind == FxKind.Icicle || fx.Kind == FxKind.ChainThrow)
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
        int stacked = bb[0].Hemorrhage;
        float fifth = Bolts(1);
        Check(stacked == 4 && bb[0].Hemorrhage == 0, "Blood Bolts stack on an enemy (drawn as drops) and the burst clears them");
        step(Game.HemorrhageHold + 0.2f, true);
        float faded = Bolts(1);
        Console.WriteLine("  blood bolts: neighbour took {0:0.0} after 4 bolts, {1:0.0} on the 5th; after they fade, one more bolt {2:0.0}", four, fifth, faded);
        Check(four == 0f && fifth > 0f && faded == 0f, "the fifth Blood Bolt in a row makes an enemy hemorrhage and burst onto enemies round it; the stacks fade");
        host.Leave("");
    }

    static void TwinSmgTest(float dt)
    {
        // Twin Rifle: the streams cross at your cursor's distance, and hits there are crossfire (double).
        // Burst SMG: holding fire spins it up: tighter spray, harder hits; it winds down when you stop.
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var volley = typeof(Game).GetMethod("FireVolley", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        int port = nextPort++;
        var host = new Session { AutoStart = 1 };
        host.Host(port, "Solo", Role.Dps, 0, 0, Kit.Soldier);
        var g = host.Game;
        host.StartMatch();
        var me = g.Players[1];
        g.DevSetGod(1, true);
        V2 from = new V2(200f, 450f);
        Func<V2, V2, float> Volley = (dummyAt, cursor) =>
        {
            g.Enemies.Clear(); g.Bullets.Clear();
            var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, dummyAt, 0f, 0, false });
            e.Hp = e.MaxHp = 1e5f; e.SpawnT = 0f; e.FireT = 99f;
            me.Pos = from; me.AimAt = cursor;
            volley.Invoke(g, new object[] { me, 0f, 0 });
            for (int f = 0; f < 50; f++) { e.Pos = dummyAt; e.FrozenT = 1f; me.Pos = from; host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = cursor }); }
            return e.MaxHp - e.Hp;
        };
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.TwinRifle);
        float crossed = Volley(from + new V2(300f, 0f), from + new V2(300f, 0f));
        float before = Volley(from + new V2(200f, 0f), from + new V2(600f, 0f));
        Console.WriteLine("  twin rifle: a volley where the streams cross {0:0.0}; short of the crossing {1:0.0}", crossed, before);
        Check(before > 0f && Math.Abs(crossed - before * Game.TwinCrossMult) < 0.5f, "the Twin Rifle's streams cross at your cursor's distance, and hits there do double");

        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.BurstSmg);
        g.Enemies.Clear(); g.Bullets.Clear();
        me.Pos = from;
        var seen = new HashSet<int>();
        var early = new List<Bullet>();
        var late = new List<Bullet>();
        for (int f = 0; f < 150; f++)
        {
            me.Pos = from;
            host.Poll(); g.FixedStep(dt, new PlayerInput { Fire = true, AimAt = from + new V2(500f, 0f) });
            foreach (var b in g.Bullets.Values)
                if (b.Owner == 1 && seen.Add(b.Id)) (f < 12 ? early : f > 130 ? late : null)?.Add(b);
        }
        float spun = me.SpinUp;
        Func<List<Bullet>, float> spread = bs => bs.Count == 0 ? 0f : bs.Max(b => Math.Abs(b.Vel.Angle));
        Func<List<Bullet>, float> dmgOf = bs => bs.Count == 0 ? 0f : bs.Average(b => b.Dmg);
        for (int f = 0; f < 45; f++) { me.Pos = from; host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = from + new V2(500f, 0f) }); }
        Console.WriteLine("  burst smg: spin-up {0:0.00} after 2.5 s; first shots {1:0.00} dmg, spray {2:0.000} rad; spun-up shots {3:0.00} dmg, spray {4:0.000} rad; 0.75 s after letting go {5:0.00}",
            spun, dmgOf(early), spread(early), dmgOf(late), spread(late), me.SpinUp);
        Check(spun > 0.99f && dmgOf(late) > dmgOf(early) * 1.5f && spread(late) <= Game.SmgSprayTight + 0.001f && spread(early) > Game.SmgSprayTight * 1.5f,
            "holding fire spins the Burst SMG up: its spray tightens and its hits climb to +60%");
        Check(me.SpinUp == 0f, "letting go spins it back down");
        host.Leave("");
    }

    static void PrismWolfRailTest(float dt)
    {
        // Prism Beam: through an ally it heals them and splits in three. Wolf Whistle: darts mark the prey, the wolves
        // hunt it, and every wolf biting within a second is a takedown. Railgun: its path stays charged for a second.
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var volley = typeof(Game).GetMethod("FireVolley", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        int port = nextPort++;
        var host = new Session { AutoStart = 2 };
        host.Host(port, "Healer", Role.Healer, 0, 0, Kit.Mage);
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
        V2 from = new V2(300f, 450f), allyAt = new V2(420f, 450f);
        var keep = new List<Enemy>();
        var pins = new Dictionary<Enemy, V2>();
        Func<V2, Enemy> dummy = at => { var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false }); e.Hp = e.MaxHp = 1e5f; e.SpawnT = 0f; e.FireT = 99f; keep.Add(e); pins[e] = at; return e; };
        Action<float> play = secs =>
        {
            for (int f = 0; f < secs * 60; f++)
            {
                foreach (var e in g.Enemies.Values) if (!keep.Contains(e)) e.Hp = 0f;
                foreach (var e in keep) { if (pins.ContainsKey(e)) e.Pos = pins[e]; e.FireT = 99f; }
                me.Pos = from; ally.Pos = client.Game.Me.Pos = allyAt;
                host.Poll(); g.FixedStep(dt, new PlayerInput()); g.Update(dt);
                client.Poll(); client.Game.FixedStep(dt, new PlayerInput()); client.Game.Update(dt);
            }
        };
        Action reset = () => { foreach (var e in keep) e.Hp = 0f; keep.Clear(); pins.Clear(); g.Bullets.Clear(); };
        play(0.3f);

        // Prism Beam.
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.PrismBeam);
        var aside = dummy(allyAt + V2.FromAngle(Game.RefractFan) * 120f);  // where a split beam goes
        ally.Hp = ally.MaxHp * 0.5f;
        float a0 = ally.Hp;
        g.Bullets.Clear();
        volley.Invoke(g, new object[] { me, 0f, 0 });
        int split = 0;
        for (int f = 0; f < 20; f++) { play(1f / 60f); split = Math.Max(split, g.Bullets.Values.Count(b => b.Owner == 1)); }
        float healed = ally.Hp - a0, asideHit = aside.MaxHp - aside.Hp;
        ally.Pos = client.Game.Me.Pos = new V2(300f, 800f);
        allyAt = new V2(300f, 800f);
        float asideBefore = aside.Hp;
        volley.Invoke(g, new object[] { me, 0f, 0 });
        play(0.5f);
        Console.WriteLine("  prism beam: through the ally it healed {0:0.0} and split into {1} beams; the one off to the side took {2:0.0}; without the ally {3:0.0}",
            healed, split, asideHit, asideBefore - aside.Hp);
        Check(healed > 0f && split == 3 && asideHit > 0f && aside.Hp == asideBefore, "a Prism Beam through an ally heals them and splits into three beams beyond them");
        reset();
        allyAt = new V2(420f, 200f);

        // Wolf Whistle.
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.WolfWhistle);
        play(1f);  // the wolves come
        var near = dummy(from + new V2(-80f, 60f));
        var prey = dummy(from + new V2(260f, 0f));
        pins.Remove(prey);  // the prey stays where it is (frozen only by the takedown)
        volley.Invoke(g, new object[] { me, 0f, 0 });
        bool marked = false, clientSaw = false;
        float stun = 0f, preyBefore = prey.Hp, nearAtMark = -1f;
        for (int f = 0; f < 240 && stun <= 0f; f++)
        {
            play(1f / 60f);
            prey.Pos = from + new V2(260f, 0f);
            marked |= me.PreyId == prey.Id;
            if (marked && nearAtMark < 0f) nearAtMark = near.Hp;
            clientSaw |= client.Game.Players[1].PreyId == prey.Id;
            if (f % 20 == 0) volley.Invoke(g, new object[] { me, 0f, 0 });
            stun = Math.Max(stun, prey.FrozenT);
        }
        int wolves = g.Minions.Values.Count(m => m.Owner == 1 && m.Kind == MinionKind.Wolf);
        Console.WriteLine("  wolf whistle: prey marked {0} (client saw {1}); {2} wolves; takedown stun {3:0.00} s; prey took {4:0.0}, the near one {5:0.0} after the mark",
            marked, clientSaw, wolves, stun, preyBefore - prey.Hp, nearAtMark - near.Hp);
        Check(marked && clientSaw && wolves >= 2, "Wolf Whistle darts mark the prey, and everyone sees the mark");
        Check(stun > 0.5f && near.Hp == nearAtMark, "once it's marked, the wolves hunt the prey (not the nearer enemy) and a pack takedown stuns it");
        reset();

        // Railgun.
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.Railgun);
        g.Minions.Clear();
        var onLine = dummy(from + new V2(300f, 0f));
        volley.Invoke(g, new object[] { me, 0f, 0 });
        play(0.3f);
        var late = dummy(from + new V2(250f, 0f));  // steps into the path just after the round passed
        play(0.5f);
        float lateHit = late.MaxHp - late.Hp;
        play(1f);
        var after = dummy(from + new V2(200f, 0f));  // long after
        play(0.5f);
        Console.WriteLine("  railgun: on the line {0:0.0}; stepped into the path just after {1:0.0}; long after {2:0.0}", onLine.MaxHp - onLine.Hp, lateHit, after.MaxHp - after.Hp);
        Check(lateHit > 5f && after.Hp == after.MaxHp && onLine.MaxHp - onLine.Hp > me.Weapon.Damage, "a Railgun shot leaves its path charged for a second, hurting what steps into it");
        host.Leave(""); client.Leave("");
    }

    static void MissilesRiotBladesTest(float dt)
    {
        // Arcane Missiles bank in orbit and launch at enemies that come near. Riot Repeater rounds suppress (slow the
        // enemy's shooting). Twin Blades: 10 slashes in a row prime a dash-strike.
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var volley = typeof(Game).GetMethod("FireVolley", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
        int port = nextPort++;
        var host = new Session { AutoStart = 1 };
        host.Host(port, "Solo", Role.Dps, 0, 0, Kit.Mage);
        var g = host.Game;
        host.StartMatch();
        var me = g.Players[1];
        g.DevSetGod(1, true);
        V2 from = new V2(300f, 450f);
        Func<V2, Enemy> dummy = at => { var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false }); e.Hp = e.MaxHp = 1e5f; e.SpawnT = 0f; return e; };
        Action<int, PlayerInput> step = (frames, input) => { for (int f = 0; f < frames; f++) { me.Pos = from; host.Poll(); g.FixedStep(dt, input); } };

        // Arcane Missiles.
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.ArcaneMissiles);
        g.Enemies.Clear(); g.Bullets.Clear(); me.Orbiters = 0;
        volley.Invoke(g, new object[] { me, 0f, 0 });
        volley.Invoke(g, new object[] { me, 0f, 0 });
        volley.Invoke(g, new object[] { me, 0f, 0 });
        int banked = me.Orbiters, flying = g.Bullets.Values.Count(b => b.Owner == 1);
        step(30, new PlayerInput());
        bool held = me.Orbiters == banked;
        var target = dummy(from + new V2(200f, 120f));
        float t0 = target.Hp;
        step(90, new PlayerInput());
        Console.WriteLine("  arcane missiles: banked {0} (cap {1}) with {2} flying from the full cast; held with no enemy near {3}; once one came near: {4} left, it took {5:0.0}",
            banked, Game.OrbitCap(0), flying, held, me.Orbiters, t0 - target.Hp);
        Check(banked == Game.OrbitCap(0) && flying == 3 && held, "Arcane Missiles bank in orbit (a full orbit's cast flies at your aim instead) and wait while no enemy is near");
        Check(me.Orbiters == 0 && target.Hp < t0, "orbiting missiles launch at an enemy that comes near");

        // Riot Repeater.
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.RiotRepeater);
        g.Enemies.Clear(); g.Bullets.Clear();
        var gunner = dummy(from + new V2(200f, 0f));
        for (int k = 0; k < 10; k++)
        {
            var b = (Bullet)shoot.Invoke(g, new object[] { Defs.TankShot, from + new V2(150f, 0f), new V2(600f, 0f), 0f, 0.5f, 11f, 1, false, 0f });
            b.Suppressive = true;
            step(8, new PlayerInput());
            gunner.Pos = from + new V2(200f, 0f);
        }
        float suppressed = gunner.Suppress;
        bool shown = gunner.Suppressed;
        step(150, new PlayerInput());
        Console.WriteLine("  riot repeater: 10 rounds suppressed it to {0:0.00} (shown {1}); 2.5 s later {2:0.00}", suppressed, shown, gunner.Suppress);
        Check(Math.Abs(suppressed - Game.SuppressMax) < 0.05f && shown && gunner.Suppress == 0f, "Riot Repeater rounds suppress an enemy up to 50% slower shooting, and it fades");

        // Twin Blades.
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.TwinBlades);
        me.Kit = Kit.Knight;
        g.Enemies.Clear(); g.Bullets.Clear();
        var close = dummy(from + new V2(50f, 0f));
        var beyond = dummy(from + new V2(180f, 0f));
        int combo = 0;
        float x0 = 0f, beyondBefore = 0f, dashed = 0f;
        for (int f = 0; f < 240; f++)
        {
            if (me.Combo < Game.ComboMax) { me.Pos = from; x0 = from.X; beyondBefore = beyond.Hp; }
            host.Poll(); g.FixedStep(dt, new PlayerInput { Fire = true, AimAt = me.Pos + new V2(400f, 0f) });
            close.Pos = from + new V2(50f, 0f); beyond.Pos = from + new V2(180f, 0f);
            combo = Math.Max(combo, me.Combo);
            dashed = Math.Max(dashed, me.Pos.X - x0);
            if (combo >= Game.ComboMax && me.Combo == 0)
            {
                for (int k = 0; k < 15; k++) { host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = me.Pos + new V2(400f, 0f) }); dashed = Math.Max(dashed, me.Pos.X - x0); }  // the lunge carries you
                break;
            }
        }
        Console.WriteLine("  twin blades: combo reached {0}; the dash-strike lunged {1:0} px and hit the one beyond for {2:0.0}; combo now {3}",
            combo, dashed, beyondBefore - beyond.Hp, me.Combo);
        Check(combo == Game.ComboMax && dashed > Game.DashStrikeDist * 0.8f && beyondBefore - beyond.Hp >= me.Weapon.Damage * Game.DashStrikeMult * 0.99f && me.Combo == 0,
            "10 Twin Blades hits in a row prime a dash-strike that lunges you through enemies for triple damage");
        host.Leave("");
    }

    static void FiveMoreTest(float dt)
    {
        // Medicine Darts dose (poison enemies, heal allies over time), the Greatsword's crescent stops bullets, a shadow
        // Kunai's mark is a blink, the Wisp Lantern sends its wisps, and the Imp Caller sends (or merges) its imps.
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var volley = typeof(Game).GetMethod("FireVolley", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
        int port = nextPort++;
        var host = new Session { AutoStart = 2 };
        host.Host(port, "Healer", Role.Healer, 0, 0, Kit.Ninja);
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
        V2 from = new V2(300f, 450f), allyAt = new V2(450f, 450f);
        var keep = new List<Enemy>();
        var pins = new Dictionary<Enemy, V2>();
        Func<V2, Enemy> dummy = at => { var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false }); e.Hp = e.MaxHp = 1e5f; e.SpawnT = 0f; e.FireT = 99f; keep.Add(e); pins[e] = at; return e; };
        Action<float, PlayerInput> play = (secs, input) =>
        {
            for (int f = 0; f < secs * 60; f++)
            {
                foreach (var e in g.Enemies.Values) if (!keep.Contains(e)) e.Hp = 0f;
                foreach (var e in keep) { if (pins.ContainsKey(e)) e.Pos = pins[e]; e.FireT = 99f; }
                if (input.Move.IsZero) me.Pos = from;
                ally.Pos = client.Game.Me.Pos = allyAt;
                host.Poll(); g.FixedStep(dt, input); g.Update(dt);
                client.Poll(); client.Game.FixedStep(dt, new PlayerInput()); client.Game.Update(dt);
            }
        };
        Action reset = () => { foreach (var e in keep) e.Hp = 0f; keep.Clear(); pins.Clear(); g.Bullets.Clear(); g.Minions.Clear(); };
        play(0.3f, new PlayerInput());

        // Medicine Darts.
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.MedicineDarts);
        ally.Hp = ally.MaxHp * 0.3f;
        for (int k = 0; k < 3; k++) { volley.Invoke(g, new object[] { me, 0f, 0 }); play(0.2f, new PlayerInput()); }
        float dose = ally.DoseHps;
        bool clientDose = client.Game.Players[2].DoseT > 0f;
        float h0 = ally.Hp;
        play(1f, new PlayerInput());
        float healed1s = ally.Hp - h0;
        allyAt = new V2(450f, 150f);
        var sick = dummy(from + new V2(200f, 0f));
        for (int k = 0; k < 3; k++) { volley.Invoke(g, new object[] { me, 0f, 0 }); play(0.2f, new PlayerInput()); }
        float hitNow = sick.MaxHp - sick.Hp, poison = sick.PoisonDps;
        play(1f, new PlayerInput());
        float poisoned1s = sick.MaxHp - sick.Hp - hitNow;
        bool clientPoison = client.Game.Enemies.ContainsKey(sick.Id) && client.Game.Enemies[sick.Id].Poisoned;
        Console.WriteLine("  medicine darts: 3 darts dose the ally {0:0.0}/s (healed {1:0.0} in 1 s; client saw {2}); poison {3:0.0}/s on the enemy ({4:0.0} in 1 s; client saw {5})",
            dose, healed1s, clientDose, poison, poisoned1s, clientPoison);
        Check(Math.Abs(dose - 3f * Game.DoseHeal * me.Stats.DmgMult) < 0.3f && healed1s >= dose * 0.9f && clientDose, "Medicine Darts dose a hurt ally with stacking healing over time");
        Check(poison > 0f && poisoned1s >= poison * 0.9f && clientPoison, "and poison enemies with stacking damage over time");
        reset();

        // Greatsword.
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.Greatsword);
        float reach = Game.MeleeReach(me, me.Weapon.Reach);
        volley.Invoke(g, new object[] { me, 0f, 0 });
        var into = (Bullet)shoot.Invoke(g, new object[] { Defs.BigShot, from + new V2(reach + 60f, 0f), new V2(-300f, 0f), 0f, 2f, 5f, 0, false, 0f });   // flies into the crescent
        var behind = (Bullet)shoot.Invoke(g, new object[] { Defs.BigShot, from + new V2(-reach - 60f, 0f), new V2(300f, 0f), 0f, 2f, 5f, 0, false, 0f });  // comes from behind
        play(0.3f, new PlayerInput());
        Console.WriteLine("  greatsword: a big shot flying into the crescent stopped {0}; one from behind still flying {1}", !g.Bullets.ContainsKey(into.Id), g.Bullets.ContainsKey(behind.Id));
        Check(!g.Bullets.ContainsKey(into.Id) && g.Bullets.ContainsKey(behind.Id), "a Greatsword sweep leaves a crescent of steel that stops bullets crossing it");
        reset();

        // Kunai.
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.Kunai);
        me.KunaiCount = 0; me.AmbushT = 0f;
        for (int k = 0; k < Game.KunaiShadowEvery; k++) volley.Invoke(g, new object[] { me, 0f, 0 });
        play(1f, new PlayerInput());
        V2 mark = me.KunaiMark;
        bool marked = me.KunaiMarkT > 0f && mark.X > from.X + 300f;
        bool clientMark = client.Game.Players[1].KunaiMarkT > 0f;
        // Double-tap fire: blink.
        for (int f = 0; f < 3; f++) { host.Poll(); g.FixedStep(dt, new PlayerInput { Fire = f == 0 || f == 2, AimAt = mark }); }
        Console.WriteLine("  kunai: the shadow kunai marked {0} (at {1:0} px; client saw {2}); double-tap blinked to {3:0} px of it; next hit primed {4}",
            marked, mark.X - from.X, clientMark, me.Pos.DistanceTo(mark), me.AmbushT > 0f);
        Check(marked && clientMark && me.Pos.DistanceTo(mark) < 2f && me.AmbushT > 0f && me.KunaiMarkT == 0f, "every 4th kunai leaves a mark; double-tapping fire blinks you there and primes a double hit");
        me.AmbushT = 0f;
        reset();

        // Wisp Lantern.
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.WispLantern);
        play(1.5f, new PlayerInput());  // the wisps come
        var mark2 = dummy(from + new V2(250f, 40f));
        me.AimAt = mark2.Pos;
        volley.Invoke(g, new object[] { me, 0f, 0 });
        bool sentOne = g.Minions.Values.Count(m => m.Owner == 1 && m.Kind == MinionKind.Wisp && m.Sent) == 1;
        play(0.6f, new PlayerInput { AimAt = mark2.Pos });
        float wispHit = mark2.MaxHp - mark2.Hp;
        play(1f, new PlayerInput { AimAt = mark2.Pos });
        bool back = g.Minions.Values.Where(m => m.Owner == 1 && m.Kind == MinionKind.Wisp).All(m => !m.Sent);
        Console.WriteLine("  wisp lantern: one wisp sent {0}; it struck for {1:0.0}; all back in orbit {2}", sentOne, wispHit, back);
        Check(sentOne && Math.Abs(wispHit - me.Weapon.Damage * me.Stats.DmgMult) < 0.5f && back, "firing the Wisp Lantern sends one wisp to strike what you aim at, and it comes back");
        reset();

        // Wisp Lantern with Homing Rounds: a wisp sent at empty ground near an enemy turns onto it (for 10% less damage).
        Func<float> wide = () =>
        {
            play(1.5f, new PlayerInput());
            var foe = dummy(from + new V2(250f, 0f));
            V2 miss = from + new V2(250f, 200f);  // 200 px off: too far for a plain wisp to pick it
            me.AimAt = miss;
            volley.Invoke(g, new object[] { me, 0f, 0 });
            play(1.2f, new PlayerInput { AimAt = miss });
            float done = foe.MaxHp - foe.Hp;
            reset();
            return done;
        };
        float plainMiss = wide();
        g.GrantUpgrade(1, Array.FindIndex(Upgrades.All, u => u.Name == "Homing Rounds"));
        float homed = wide();
        Console.WriteLine("  wisp lantern + Homing Rounds: sent beside an enemy, a plain wisp did {0:0.0}, a homing one {1:0.0}", plainMiss, homed);
        Check(plainMiss == 0f && Math.Abs(homed - me.Weapon.Damage * me.Stats.DmgMult * 0.9f) < 0.5f, "Homing Rounds turn a sent wisp onto a nearby enemy, for 10% less damage");

        // Imp Caller.
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.ImpCaller);
        Func<int> imps = () => g.Minions.Values.Count(m => m.Owner == 1 && m.Kind == MinionKind.Imp && !m.Sent);
        for (int f = 0; f < 600 && imps() < 3; f++) { me.CompanionCd = Math.Min(me.CompanionCd, 0.05f); play(1f / 60f, new PlayerInput()); }
        var target = dummy(from + new V2(240f, 0f));
        me.AimAt = target.Pos;
        me.BowShotDraw = Game.BowMinDraw;
        volley.Invoke(g, new object[] { me, 0f, 0 });
        play(1.5f, new PlayerInput { AimAt = target.Pos });
        float tap = target.MaxHp - target.Hp;
        for (int f = 0; f < 600 && imps() < 3; f++) { me.CompanionCd = Math.Min(me.CompanionCd, 0.05f); play(1f / 60f, new PlayerInput { AimAt = target.Pos }); }
        float t1 = target.Hp;
        me.BowShotDraw = g.BowFull(me);
        volley.Invoke(g, new object[] { me, 0f, 0 });
        var big = g.Minions.Values.FirstOrDefault(m => m.Owner == 1 && m.Kind == MinionKind.Imp && m.Sent);
        float bigScale = big != null ? big.Scale : 0f;
        int sentCount = g.Minions.Values.Count(m => m.Owner == 1 && m.Kind == MinionKind.Imp && m.Sent);
        play(1.5f, new PlayerInput { AimAt = target.Pos });
        float merged = t1 - target.Hp;
        Console.WriteLine("  imp caller: a tap sent 3 imps for {0:0.0}; a full charge merged them into one (scale {1:0.00}, {2} sent) for {3:0.0}", tap, bigScale, sentCount, merged);
        Check(tap > 0f && sentCount == 1 && bigScale > 1.2f && merged > tap * 1.1f, "a tap sends the waiting imps at your cursor; a full charge merges them into one bigger blast");
        host.Leave(""); client.Leave("");
    }

    static void FourMoreTest(float dt)
    {
        // Shuriken stars stick and are recalled; Bone Spears stay stuck as health pickups; the Lightbringer's full light
        // gauge triples its crescents' healing; the Treant Rod's Treant roots with a vine to you that heals and slows.
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var volley = typeof(Game).GetMethod("FireVolley", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        int port = nextPort++;
        var host = new Session { AutoStart = 2 };
        host.Host(port, "Healer", Role.Healer, 0, 0, Kit.Knight);
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
        V2 from = new V2(300f, 450f), allyAt = new V2(300f, 150f);
        var keep = new List<Enemy>();
        var pins = new Dictionary<Enemy, V2>();
        Func<V2, Enemy> dummy = at => { var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false }); e.Hp = e.MaxHp = 1e5f; e.SpawnT = 0f; e.FireT = 99f; keep.Add(e); pins[e] = at; return e; };
        bool pinMe = true;
        Action<float, PlayerInput> play = (secs, input) =>
        {
            for (int f = 0; f < secs * 60; f++)
            {
                foreach (var e in g.Enemies.Values) if (!keep.Contains(e)) e.Hp = 0f;
                foreach (var e in keep) { if (pins.ContainsKey(e)) e.Pos = pins[e]; e.FireT = 99f; }
                if (pinMe) me.Pos = from;
                ally.Pos = client.Game.Me.Pos = allyAt;
                host.Poll(); g.FixedStep(dt, input); g.Update(dt);
                client.Poll(); client.Game.FixedStep(dt, new PlayerInput()); client.Game.Update(dt);
            }
        };
        Action reset = () => { foreach (var e in keep) e.Hp = 0f; keep.Clear(); pins.Clear(); g.Bullets.Clear(); g.Minions.Clear(); g.Zones.Clear(); };
        play(0.3f, new PlayerInput());

        // Shuriken.
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.Shuriken);
        var star = dummy(from + new V2(120f, 0f));
        volley.Invoke(g, new object[] { me, 0f, 0 });
        volley.Invoke(g, new object[] { me, 0f, 0 });
        play(0.4f, new PlayerInput());
        int stuck = star.StarsStuck;
        bool clientStars = client.Game.Enemies.ContainsKey(star.Id) && client.Game.Enemies[star.Id].StarsStuck == stuck;
        float before = star.Hp;
        for (int f = 0; f < 3; f++) { host.Poll(); g.FixedStep(dt, new PlayerInput { Fire = f != 1, AimAt = star.Pos }); star.Pos = from + new V2(120f, 0f); }  // double-tap
        float recalled = before - star.Hp;
        play(0.4f, new PlayerInput());
        Console.WriteLine("  shuriken: {0} stars stuck (client saw them {1}); the recall ripped them out for {2:0.0}; stuck now {3}", stuck, clientStars, recalled, star.StarsStuck);
        Check(stuck >= 2 && clientStars && recalled >= stuck * me.Weapon.Damage * me.Stats.DmgMult * 0.99f && star.StarsStuck < stuck,  // (the double-tap's own throws stick again)
            "Shuriken stars stick in what they hit; double-tapping fire recalls them, ripping them out for another hit");
        reset();

        // Bone Spear.
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.BoneSpear);
        me.Kit = Kit.BloodMage;
        me.Hp = me.MaxHp * 0.5f;
        volley.Invoke(g, new object[] { me, 0f, 0 });
        float paid = me.MaxHp * 0.5f - me.Hp;
        play(1.2f, new PlayerInput());
        var spear = g.Zones.Values.FirstOrDefault(z => z.Kind == ZoneKind.BoneSpear && z.Owner == 1);
        bool clientSpear = client.Game.Zones.Values.Any(z => z.Kind == ZoneKind.BoneSpear);
        float h0 = me.Hp;
        if (spear != null) { pinMe = false; me.Pos = spear.Pos; play(0.1f, new PlayerInput()); pinMe = true; }
        Console.WriteLine("  bone spear: paid {0:0.0}; stuck at {1:0} px (client saw it {2}); walking over it gave {3:0.0} back; gone {4}",
            paid, spear != null ? spear.Pos.X - from.X : -1f, clientSpear, me.Hp - h0, !g.Zones.Values.Any(z => z.Kind == ZoneKind.BoneSpear));
        Check(spear != null && clientSpear && me.Hp - h0 >= Game.BoneSpearCost - 0.01f && me.Hp - h0 < Game.BoneSpearCost + 1f && !g.Zones.Values.Any(z => z.Kind == ZoneKind.BoneSpear),  // (plus a little regeneration)
            "a Bone Spear stays stuck where it stops, and walking over it gives its health back");
        me.Kit = Kit.Knight;
        reset();

        // Lightbringer.
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.Lightbringer);
        me.Light = 0;
        var foe = dummy(from + new V2(60f, 0f));
        for (int k = 0; k < 5; k++) { volley.Invoke(g, new object[] { me, 0f, 0 }); play(0.1f, new PlayerInput()); }
        int filled = me.Light;
        reset();
        Func<int, float> crescent = light =>
        {
            ally.Hp = ally.MaxHp * 0.3f;
            me.Light = light;
            float a0 = ally.Hp;
            if (light >= 0) volley.Invoke(g, new object[] { me, (allyAt - from).Angle, 0 });
            play(0.5f, new PlayerInput());
            return ally.Hp - a0;
        };
        allyAt = from + new V2(0f, -120f);
        float aura = crescent(-1);  // the healer's aura alone, over the same time
        float plain = crescent(0) - aura, radiant = crescent(Game.LightMax) - aura;
        Console.WriteLine("  lightbringer: 5 swings filled the gauge to {0}; a crescent healed {1:0.0}, a radiant one {2:0.0} (gauge now {3})", filled, plain, radiant, me.Light);
        Check(filled == 5 && plain > 0f && Math.Abs(radiant - plain * Game.LightRadiant) < 1f && me.Light == 0, "Lightbringer hits fill its light gauge; full, the next crescents heal three times as much");
        allyAt = new V2(300f, 150f);
        reset();

        // Treant Rod.
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.TreantRod);
        play(1f, new PlayerInput());
        var treant = g.Minions.Values.FirstOrDefault(m => m.Owner == 1 && m.Kind == MinionKind.Treant);
        V2 rooted = treant != null ? treant.Pos : V2.Zero;
        from = new V2(560f, 450f);  // you walk off: it stays
        play(0.6f, new PlayerInput());
        float stayed = treant != null ? treant.Pos.DistanceTo(rooted) : -1f;
        V2 mid = (from + (treant != null ? treant.Pos : from)) * 0.5f;
        allyAt = mid;
        var crossing = dummy(mid + new V2(0f, 30f));
        ally.Hp = ally.MaxHp * 0.3f;
        float ah = ally.Hp, eh = crossing.Hp;
        play(1f, new PlayerInput());
        float vineHeal = ally.Hp - ah, vineHurt = eh - crossing.Hp, slow = crossing.SlowMult;
        from = new V2(1200f, 450f);  // too far: it follows
        play(1.5f, new PlayerInput());
        float followed = treant != null ? rooted.DistanceTo(treant.Pos) : 0f;
        Console.WriteLine("  treant rod: it stayed rooted ({0:0} px moved); the vine healed the ally {1:0.0} in 1 s, hurt the enemy {2:0.0} and slowed it to {3:0.00}; far off, it followed {4:0} px",
            stayed, vineHeal, vineHurt, slow, followed);
        Check(treant != null && stayed < 2f && vineHeal >= Game.VineHeal * 0.9f && vineHurt > 0f && slow <= 1f - Game.VineSlow + 0.01f && followed > 200f,
            "the Treant roots where it is; a vine to you heals allies and slows enemies crossing it, and the Treant follows if you go too far");
        host.Leave(""); client.Leave("");
    }

    static void HealerFiveTest(float dt)
    {
        // The Censer's incense trail heals and spoils enemy aim; the Spirit Fan alternates pushing and pulling; Ofuda
        // charms lie as wards that burst under enemies; Sprite Staff seeds free sprites from kills; Totem Staff totems
        // are thrown and charged by seed hits.
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var volley = typeof(Game).GetMethod("FireVolley", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
        int port = nextPort++;
        var host = new Session { AutoStart = 2 };
        host.Host(port, "Healer", Role.Healer, 0, 0, Kit.Knight);
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
        V2 from = new V2(300f, 450f), allyAt = new V2(300f, 150f);
        var keep = new List<Enemy>();
        var pins = new Dictionary<Enemy, V2>();
        Func<V2, float, Enemy> dummy = (at, hp) => { var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false }); e.Hp = e.MaxHp = hp; e.SpawnT = 0f; e.FireT = 99f; keep.Add(e); pins[e] = at; return e; };
        bool pinMe = true;
        Action<float, PlayerInput> play = (secs, input) =>
        {
            for (int f = 0; f < secs * 60; f++)
            {
                foreach (var e in g.Enemies.Values) if (!keep.Contains(e)) e.Hp = 0f;
                foreach (var e in keep) { if (pins.ContainsKey(e)) e.Pos = pins[e]; e.FireT = 99f; }
                if (pinMe) me.Pos = from;
                ally.Pos = client.Game.Me.Pos = allyAt;
                host.Poll(); g.FixedStep(dt, input); g.Update(dt);
                client.Poll(); client.Game.FixedStep(dt, new PlayerInput()); client.Game.Update(dt);
            }
        };
        Action reset = () => { foreach (var e in keep) e.Hp = 0f; keep.Clear(); pins.Clear(); g.Bullets.Clear(); g.Minions.Clear(); g.Zones.Clear(); };
        play(0.3f, new PlayerInput());

        // Censer: walk, leaving incense; an ally in it heals; enemy shots fired from inside it stray.
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.Censer);
        pinMe = false;
        me.Pos = from;
        play(1f, new PlayerInput { Move = new V2(1f, 0f), AimAt = from + new V2(400f, 0f) });
        pinMe = true;
        int trail = g.Zones.Values.Count(z => z.Kind == ZoneKind.Incense);
        bool clientTrail = client.Game.Zones.Values.Any(z => z.Kind == ZoneKind.Incense);
        var puff = g.Zones.Values.Where(z => z.Kind == ZoneKind.Incense).OrderBy(z => z.T).Last();
        allyAt = puff.Pos;
        ally.Hp = ally.MaxHp * 0.3f;
        float a0 = ally.Hp;
        from = new V2(300f, 800f);  // step away so the spin's healing doesn't count
        play(0.5f, new PlayerInput());
        float trailHeal = ally.Hp - a0;
        int strayed = 0, straight = 0;
        for (int k = 0; k < 20; k++)
        {
            var inside = (Bullet)shoot.Invoke(g, new object[] { Defs.SmallShot, puff.Pos, new V2(300f, 0f), 0f, 1f, 5f, 0, false, 0f });
            var outside = (Bullet)shoot.Invoke(g, new object[] { Defs.SmallShot, new V2(1200f, 800f), new V2(300f, 0f), 0f, 1f, 5f, 0, false, 0f });
            if (Math.Abs(inside.Vel.Angle) > 0.01f) strayed++;
            if (Math.Abs(outside.Vel.Angle) < 0.001f) straight++;
        }
        Console.WriteLine("  censer: walking left {0} puffs of incense (client saw {1}); an ally in it healed {2:0.0} in 0.5 s; enemy shots from inside strayed {3}/20, outside straight {4}/20",
            trail, clientTrail, trailHeal, strayed, straight);
        Check(trail >= 3 && clientTrail && trailHeal > 0f, "the Censer leaves a trail of incense as you move, healing allies in it");
        Check(strayed >= 17 && straight == 20, "enemy shots fired from inside the incense stray");
        from = new V2(300f, 450f); allyAt = new V2(300f, 150f);
        reset();

        // Spirit Fan: one shot blows enemies away, the next draws them in.
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.SpiritFan);
        var blown = dummy(from + new V2(110f, 0f), 1e5f);
        pins.Remove(blown);
        me.Pos = from;
        float d0 = blown.Pos.DistanceTo(from);
        volley.Invoke(g, new object[] { me, 0f, 0 });
        float d1 = blown.Pos.DistanceTo(from);
        volley.Invoke(g, new object[] { me, 0f, 0 });
        float d2 = blown.Pos.DistanceTo(from);
        Console.WriteLine("  spirit fan: an enemy {0:0} px away, then {1:0}, then {2:0}", d0, d1, d2);
        Check(Math.Abs(Math.Abs(d1 - d0) - Game.FanShove) < 1f && Math.Abs(d2 - d1 + (d1 - d0)) < 1f, "Spirit Fan shots alternate blowing enemies away and drawing them in");
        reset();

        // Ofuda: a charm lies at the cursor as a ward; an enemy stepping on it sets it off, hurting it and healing allies near.
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.Ofuda);
        me.Pos = from;
        me.AimAt = from + new V2(300f, 0f);
        volley.Invoke(g, new object[] { me, 0f, 0 });
        play(1.2f, new PlayerInput { AimAt = me.AimAt });
        var ward = g.Zones.Values.FirstOrDefault(z => z.Kind == ZoneKind.OfudaWard);
        float wardAt = ward != null ? ward.Pos.DistanceTo(from) : -1f;
        allyAt = ward != null ? ward.Pos + new V2(0f, 60f) : allyAt;
        play(0.2f, new PlayerInput());  // the ally gets there (its own game sends where it is)
        ally.Hp = ally.MaxHp * 0.3f;
        float ah = ally.Hp;
        var walker = dummy(ward != null ? ward.Pos : from, 1e5f);
        play(0.1f, new PlayerInput());
        float hurt = walker.MaxHp - walker.Hp, healed = ally.Hp - ah;
        bool gone = !g.Zones.Values.Any(z => z.Kind == ZoneKind.OfudaWard);
        walker.Hp = 0f; keep.Remove(walker);
        for (int k = 0; k < 7; k++) { me.AimAt = from + new V2(200f + 30f * k, -100f); volley.Invoke(g, new object[] { me, 0f, 0 }); }
        play(1.5f, new PlayerInput());
        int wards = g.Zones.Values.Count(z => z.Kind == ZoneKind.OfudaWard);
        Console.WriteLine("  ofuda: a ward lay {0:0} px out; an enemy stepping on it took {1:0.0}, an ally near healed {2:0.0}, ward gone {3}; 7 more thrown, {4} lie (cap {5})",
            wardAt, hurt, healed, gone, wards, Game.WardCap);
        Check(Math.Abs(wardAt - 300f) < 30f && hurt > 0f && healed >= Game.WardHeal - 0.5f && gone && wards == Game.WardCap,
            "Ofuda charms lie at your cursor as wards that burst when an enemy steps on one, healing allies near");
        allyAt = new V2(300f, 150f);
        reset();

        // Sprite Staff: a seeded enemy that dies frees a sprite.
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.SpriteStaff);
        var seedling = dummy(from + new V2(200f, 0f), 15f);
        volley.Invoke(g, new object[] { me, 0f, 0 });
        volley.Invoke(g, new object[] { me, 0f, 0 });
        play(0.8f, new PlayerInput());
        int freed = g.Minions.Values.Count(m => m.Owner == 1 && m.Kind == MinionKind.Sprite && !m.Companion);
        Console.WriteLine("  sprite staff: the seeded enemy died {0}; freed sprites {1}", seedling.Hp <= 0f || !g.Enemies.ContainsKey(seedling.Id), freed);
        Check(freed == 1, "killing a seeded enemy frees a sprite");
        reset();

        // Totem Staff: thrown to the cursor; seed hits near it charge a heal pulse.
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.TotemStaff);
        me.TotemCd = 0f;
        V2 throwTo = from + new V2(250f, 0f);
        play(0.1f, new PlayerInput { Fire = true, AimAt = throwTo });
        var totem = g.Zones.Values.FirstOrDefault(z => z.Kind == ZoneKind.Totem && z.Owner == 1);
        float totemAt = totem != null ? totem.Pos.DistanceTo(throwTo) : -1f;
        allyAt = totem != null ? totem.Pos + new V2(0f, 100f) : allyAt;  // in the pulse, outside its steady heal
        var near = dummy(throwTo + new V2(60f, 0f), 1e5f);
        ally.Hp = ally.MaxHp * 0.3f;
        play(0.5f, new PlayerInput());
        float before = ally.Hp;
        for (int k = 0; k < (int)Game.TotemCharge; k++) { volley.Invoke(g, new object[] { me, 0f, 0 }); play(0.3f, new PlayerInput()); }
        float pulsed = ally.Hp - before;
        Console.WriteLine("  totem staff: thrown {0:0} px from the cursor; {1} seed hits near it healed an ally just outside it {2:0.0}", totemAt, (int)Game.TotemCharge, pulsed);
        Check(totem != null && totemAt < 5f && pulsed >= Game.TotemPulse - 1f, "the Totem Staff throws its totem to your cursor, and seed hits near it charge a heal pulse");
        host.Leave(""); client.Leave("");
    }

    static void LastFourTest(float dt)
    {
        // Clot Bolt pools congeal, sticking enemies and guarding their owner; Transfusion Darts give an ally a lot for a little
        // and steal a sliver from enemies; the Rapier skewers bullets into its streak; the Katana's sheathed quick-draw cut.
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var volley = typeof(Game).GetMethod("FireVolley", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
        var damage = typeof(Game).GetMethod("Damage", flags);
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
        V2 from = new V2(300f, 450f), allyAt = new V2(300f, 150f);
        var keep = new List<Enemy>();
        var pins = new Dictionary<Enemy, V2>();
        Func<V2, float, Enemy> dummy = (at, hp) => { var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false }); e.Hp = e.MaxHp = hp; e.SpawnT = 0f; e.FireT = 99f; keep.Add(e); pins[e] = at; return e; };
        Action<float, PlayerInput> play = (secs, input) =>
        {
            for (int f = 0; f < secs * 60; f++)
            {
                foreach (var e in g.Enemies.Values) if (!keep.Contains(e)) e.Hp = 0f;
                foreach (var e in keep) { if (pins.ContainsKey(e)) e.Pos = pins[e]; e.FireT = 99f; }
                me.Pos = from;
                ally.Pos = client.Game.Me.Pos = allyAt;
                host.Poll(); g.FixedStep(dt, input); g.Update(dt);
                client.Poll(); client.Game.FixedStep(dt, new PlayerInput()); client.Game.Update(dt);
            }
        };
        Action reset = () => { foreach (var e in keep) e.Hp = 0f; keep.Clear(); pins.Clear(); g.Bullets.Clear(); g.Zones.Clear(); };
        play(0.3f, new PlayerInput());

        // Clot Bolt: the pool congeals after a second, sticking the enemy in it; standing in it, you take less.
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.ClotBolt);
        var stuck = dummy(from + new V2(200f, 0f), 1e5f);
        volley.Invoke(g, new object[] { me, 0f, 0 });
        play(0.6f, new PlayerInput());
        var pool = g.Zones.Values.FirstOrDefault(z => z.Kind == ZoneKind.BloodPool);
        bool wetFree = stuck.FrozenT <= 0f;
        play(0.8f, new PlayerInput());
        bool setStuck = stuck.FrozenT > 0f;
        g.DevSetGod(1, false);
        me.Hp = me.MaxHp; me.ShieldHp = 0f; damage.Invoke(g, new object[] { me, 20f, "Test" }); float outside = me.MaxHp - me.Hp;
        if (pool != null) from = pool.Pos;
        play(0.02f, new PlayerInput());
        me.Hp = me.MaxHp; me.ShieldHp = 0f; damage.Invoke(g, new object[] { me, 20f, "Test" }); float inside = me.MaxHp - me.Hp;
        g.DevSetGod(1, true);
        Console.WriteLine("  clot bolt: pool {0}; enemy free while wet {1}, stuck once it congealed {2}; 20 damage taken outside {3:0.0}, in your pool {4:0.0}",
            pool != null, wetFree, setStuck, outside, inside);
        Check(pool != null && wetFree && setStuck, "a Clot Bolt's pool congeals after a second, sticking the enemies in it");
        Check(Math.Abs(inside - outside * (1f - Game.PoolGuard)) < 0.6f, "you take less damage standing in your own pool");
        from = new V2(300f, 450f);
        reset();

        // Transfusion Darts: a hurt ally gets a lot, you pay a little; an enemy hit steals a sliver.
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.TransfusionDarts);
        allyAt = from + new V2(200f, 0f);
        play(0.2f, new PlayerInput());
        Func<float> drift = () => { me.Hp = me.MaxHp * 0.5f; float h = me.Hp; play(0.5f, new PlayerInput()); return me.Hp - h; };  // your own healing meanwhile (regen, aura)
        float selfHeal = drift();
        ally.Hp = ally.MaxHp * 0.3f; me.Hp = me.MaxHp * 0.5f;
        float ah = ally.Hp, mh = me.Hp;
        volley.Invoke(g, new object[] { me, 0f, 0 });
        play(0.5f, new PlayerInput());
        float given = ally.Hp - ah, paid = mh - me.Hp + selfHeal;
        allyAt = new V2(300f, 150f);
        play(0.2f, new PlayerInput());
        dummy(from + new V2(200f, 0f), 1e5f);
        me.Hp = me.MaxHp * 0.5f; float before = me.Hp;
        volley.Invoke(g, new object[] { me, 0f, 0 });
        play(0.5f, new PlayerInput());
        float stolen = me.Hp - before - selfHeal;
        Console.WriteLine("  transfusion darts: one dart gave an ally {0:0.0} for {1:0.0} of yours; one dart into an enemy healed you {2:0.00}", given, paid, stolen);
        Check(Math.Abs(given - Game.TransfuseHeal) < 1f && Math.Abs(paid - Game.TransfuseCost) < 0.5f, "Transfusion Darts give a hurt ally a lot of health for a little of yours");
        Check(stolen >= Game.TransfuseSteal - 0.05f && stolen < 1.2f, "Transfusion Darts steal only a sliver from enemies");
        reset();

        // Rapier: a thrust skewers enemy bullets along its line (3 at most), each adding to the streak.
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.Rapier);
        var mark = dummy(from + new V2(120f, 0f), 1e5f);
        volley.Invoke(g, new object[] { me, 0f, 0 });
        play(0.1f, new PlayerInput());
        int s0 = me.RapierStreak;
        var shots = new List<Bullet>();
        for (int k = 0; k < 5; k++)
            shots.Add((Bullet)shoot.Invoke(g, new object[] { Defs.SmallShot, from + new V2(30f + 18f * k, 0f), new V2(0f, 0f), 0f, 5f, 5f, 0, false, 0f }));
        var aside = (Bullet)shoot.Invoke(g, new object[] { Defs.SmallShot, from + new V2(60f, 80f), new V2(0f, 0f), 0f, 5f, 5f, 0, false, 0f });
        volley.Invoke(g, new object[] { me, 0f, 0 });
        play(0.05f, new PlayerInput());
        int skewered = shots.Count(b => !g.Bullets.ContainsKey(b.Id));
        bool asideLeft = g.Bullets.ContainsKey(aside.Id);
        Console.WriteLine("  rapier: streak {0} -> {1}; skewered {2} of 5 bullets on its line, one off the line left alone {3}", s0, me.RapierStreak, skewered, asideLeft);
        Check(skewered == Game.RapierSkewerMax && asideLeft, "a Rapier thrust skewers up to 3 enemy bullets along its line");
        Check(me.RapierStreak >= Math.Min(6, s0 + 1 + Game.RapierSkewerMax), "each skewered bullet adds to the Rapier's streak");
        reset();

        // Katana: held off, the blade sheathes; the next cut is a long quick-draw through everything in its line.
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.Katana);
        me.Hp = me.MaxHp;  // (Blood Price would change the damage as health moves)
        var nearK = dummy(from + new V2(60f, 0f), 1e5f);
        var farK = dummy(from + new V2(200f, 0f), 1e5f);
        me.SheathT = 0f;
        volley.Invoke(g, new object[] { me, 0f, 0 });
        play(0.1f, new PlayerInput());
        float plainNear = nearK.MaxHp - nearK.Hp, plainFar = farK.MaxHp - farK.Hp;
        play(Game.KatanaSheath + 0.1f, new PlayerInput());
        bool sheathed = me.SheathT >= Game.KatanaSheath, seen = client.Game.Players[1].SheathT >= Game.KatanaSheath;
        float range = g.WeaponRange(me);
        nearK.Hp = nearK.MaxHp; farK.Hp = farK.MaxHp;
        volley.Invoke(g, new object[] { me, 0f, 0 });
        play(0.1f, new PlayerInput());
        float drawNear = nearK.MaxHp - nearK.Hp, drawFar = farK.MaxHp - farK.Hp;
        Console.WriteLine("  katana: a plain cut hit near {0:0} / far {1:0}; sheathed {2} (client saw {3}, aim line {4:0} px); the quick-draw hit near {5:0} / far {6:0}; unsheathed after {7}",
            plainNear, plainFar, sheathed, seen, range, drawNear, drawFar, me.SheathT < Game.KatanaSheath);
        Check(plainNear > 0f && plainFar == 0f, "a plain Katana slash keeps its short reach");
        Check(sheathed && seen && range > 200f, "holding off sheathes the Katana (synced, and its aim line reaches the quick-draw)");
        Check(Math.Abs(drawNear - plainNear * Game.KatanaDrawMult) < 1.5f && Math.Abs(drawFar - drawNear) < 1.5f && me.SheathT < Game.KatanaSheath,
            "the sheathed Katana's quick-draw cuts through everything in its line for 2.5x");
        host.Leave(""); client.Leave("");
    }

    static void HealerWardsTest(float dt)
    {
        // Spirit Ward takes allies' damage and pays some back; Healing Mist sheds dew to pick up; Arcane Ward pays back what it soaked.
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var cast = typeof(Game).GetMethod("HostAbility", flags);
        var damage = typeof(Game).GetMethod("Damage", flags);
        int port = nextPort++;
        var host = new Session { AutoStart = 2 };
        host.Host(port, "Healer", Role.Healer, 0, 0, Kit.Ninja);
        var client = new Session();
        client.Join("127.0.0.1", port, "Tank", Role.Tank, 0);
        var s = new[] { host, client };
        for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        var ally = g.Players[2];
        g.Bot = client.Game.Bot = false;
        V2 from = new V2(400f, 450f), allyAt = new V2(500f, 450f);
        Action<float> play = secs =>
        {
            for (int f = 0; f < secs * 60; f++)
            {
                foreach (var e in g.Enemies.Values) e.Hp = 0f;
                me.Pos = from;
                ally.Pos = client.Game.Me.Pos = allyAt;
                host.Poll(); g.FixedStep(dt, new PlayerInput()); g.Update(dt);
                client.Poll(); client.Game.FixedStep(dt, new PlayerInput()); client.Game.Update(dt);
            }
        };
        Action<AbilityId, V2> use = (ability, at) => { g.ShowcaseSetAbility(ability); me.Slots[0].Charges = 1; cast.Invoke(g, new object[] { 1, 0, at, true }); };
        Action<Player, float> hurt = (q, amount) => damage.Invoke(g, new object[] { q, amount, "Test" });
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        play(0.3f);
        g.Zones.Clear(); g.Orbs.Clear();

        // Spirit Ward: it takes an ally's damage until it breaks, then a quarter of what it took heals them.
        use(AbilityId.SpiritWard, allyAt);
        var ward = g.Zones.Values.FirstOrDefault(z => z.Kind == ZoneKind.Ward);
        g.DevSetGod(2, false);
        ally.Hp = ally.MaxHp * 0.5f;
        float h0 = ally.Hp;
        hurt(ally, 100f);
        float firstTaken = h0 - ally.Hp;
        play(0.05f);
        float frac = client.Game.Zones.Values.Where(z => z.Kind == ZoneKind.Ward).Select(z => z.GuardFrac).FirstOrDefault();
        float h1 = ally.Hp;
        hurt(ally, 80f);
        float secondTaken = h1 - ally.Hp;
        float h2 = ally.Hp;
        play(0.1f);
        bool broke = !g.Zones.Values.Any(z => z.Kind == ZoneKind.Ward);
        float repaid = ally.Hp - h2;
        g.DevSetGod(2, true);
        Console.WriteLine("  spirit ward: an ally took {0:0} of 100 (client saw {1:0.00} left), then {2:0} of 80; it broke {3} and healed them {4:0.0}",
            firstTaken, frac, secondTaken, broke, repaid);
        Check(ward != null && firstTaken == 0f && Math.Abs(frac - 1f / 3f) < 0.05f && Math.Abs(secondTaken - 30f) < 0.5f, "a Spirit Ward takes the damage its allies would take, up to 150");
        Check(broke && Math.Abs(repaid - Game.WardSoakMax * Game.WardRepay) < 3f, "a broken Spirit Ward heals the allies near it for a quarter of what it took");
        g.Zones.Clear();

        // Healing Mist: it follows you shedding dew; a hurt ally picks the drops up.
        ally.Hp = ally.MaxHp * 0.3f;
        allyAt = from + new V2(30f, 0f);
        float a0 = ally.Hp;
        use(AbilityId.HealingMist, from);
        play(1.6f);
        int drops = g.Orbs.Values.Count(o => o.Dew) ;
        bool clientDew = client.Game.Orbs.Values.Any(o => o.Dew);
        float mistHealed = ally.Hp - a0;
        Console.WriteLine("  healing mist: dew on the ground {0} (client saw some {1}); a hurt ally beside you healed {2:0.0} in 1.6 s", drops, clientDew, mistHealed);
        Check(mistHealed >= Game.DewHeal * 2f - 0.5f, "Healing Mist sheds dew that heals the allies who pick it up");
        Check(drops + (int)(mistHealed / Game.DewHeal) >= 3 && (clientDew || drops == 0), "Healing Mist's dew drops are synced");
        g.Zones.Clear(); g.Orbs.Clear();
        allyAt = new V2(500f, 450f);
        play(0.2f);

        // Arcane Ward: a shield that soaks damage, paying back half of it as healing when it breaks or runs out.
        use(AbilityId.ArcaneWard, from);
        float shieldMe = me.ShieldHp, shieldAlly = ally.ShieldHp;
        g.DevSetGod(1, false); g.DevSetGod(2, false);
        me.Hp = me.MaxHp * 0.5f; ally.Hp = ally.MaxHp * 0.5f;
        float m0 = me.Hp, b0 = ally.Hp;
        hurt(me, 40f);       // soaked; then it runs out
        me.ShieldT = 0.01f;
        hurt(ally, 70f);     // breaks it: 10 gets through, 30 comes back
        float allyNet = ally.Hp - b0;
        play(0.05f);
        float meNet = me.Hp - m0;
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        Console.WriteLine("  arcane ward: shields {0:0} / {1:0}; you soaked 40 and got back {2:0.0} when it ran out; the ally took 70, net {3:+0.0;-0.0}",
            shieldMe, shieldAlly, meNet, allyNet);
        Check(shieldMe >= 59f && shieldAlly >= 59f, "Arcane Ward shields you and your ally");
        Check(Math.Abs(meNet - 40f * Game.ArcaneRepay) < 2f && Math.Abs(allyNet - (-10f + 60f * Game.ArcaneRepay)) < 1f, "an Arcane Ward pays back half of what it soaked when it runs out or breaks");
        host.Leave(""); client.Leave("");
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
        Action clear = () => { foreach (var e in g.Enemies.Values) e.Hp = 0f; for (int f = 0; f < 3; f++) { host.Poll(); g.FixedStep(dt, new PlayerInput()); } g.Bullets.Clear(); g.Zones.Clear(); };
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
        Check(gain.All(x => x > 0.2f && x < 0.3f), "Incendiary Rounds add 25% of every hit as burn, on fast and slow weapons alike");

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
        host.Leave("");
    }
}
