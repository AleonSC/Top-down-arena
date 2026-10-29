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
                    x.Game.FixedStep(dt, x == s[0] ? new PlayerInput { AimAt = center, Fire = w.Id != WeaponId.MendersBow || f % 90 < 75 } : new PlayerInput());
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
                case WeaponId.HolyMace: ok = ratio > 0.9f && a[0] > 0f; break;              // heals more per hit (checked in KnightTest's tables); damage unchanged
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
                if (w.Id == WeaponId.VeinTether || w.Id == WeaponId.Implosion || w.Id == WeaponId.LeechBeam)
                {
                    // A beam, an implosion or a tendril has no shots to follow: an enemy just inside its range is hit, one just past it isn't.
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
                        if (fx.Kind == FxKind.Slash || fx.Kind == FxKind.Jab || fx.Kind == FxKind.Claw)
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
        // The Frost Lance flies as a wide spear: it clips a small enemy 15 px off its line, which the old 4.5 px shard missed.
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
        var shard = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Shard, new V2(520f, 465f), 0f, 0, false });
        shard.Hp = shard.MaxHp = 1e5f; shard.SpawnT = 0f;
        volley.Invoke(g, new object[] { me, 0f, 0 });
        var lance = g.Bullets.Values.FirstOrDefault(b => b.Owner == 1);
        float hp = shard.Hp;
        for (int f = 0; f < 40; f++)
        {
            shard.Pos = new V2(520f, 465f); shard.FrozenT = 1f;
            me.Pos = new V2(200f, 450f);
            host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = new V2(900f, 450f) });
        }
        Console.WriteLine("  frost lance: kind {0}, radius {1}, hit a Shard 15 px off its line: {2}", lance == null ? -1 : lance.Kind, lance == null ? 0f : lance.Radius, shard.Hp < hp);
        Check(lance != null && lance.Kind == Defs.FrostLance && lance.Radius >= 10f && shard.Hp < hp, "the Frost Lance has a wide spearhead hitbox");
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
        s[0].Leave(""); s[1].Leave("");
    }
}
