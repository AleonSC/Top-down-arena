using System.Linq;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using Arena;

// Weapons, class by class: the scatter/flak/blood, SMG, prism/wolf/rail, missile and riot-blade weapons, and
// the rest of each role's lineup (the "more" tests) and the Healer wards.
static partial class Program
{
    static void ScatterFlakBloodTest(float dt)
    {
        // Scattergun: point-blank pellets do double and shove. Flak Cannon: shells fly over enemies to the cursor and
        // burst there, also destroying enemy bullets. Blood Bolts: the fifth in a row makes an enemy hemorrhage.
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var volley = typeof(Game).GetMethod("FireVolley", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 1 }, port, false, "Solo", Role.Tank, 0, 0, Kit.Soldier);
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
        me.Kit = Kit.Hemomancer;
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
        var host = HostAs(new Session { AutoStart = 1 }, port, false, "Solo", Role.Dps, 0, 0, Kit.Soldier);
        var g = host.Game;
        host.StartMatch();
        var me = g.Players[1];
        g.DevSetGod(1, true);
        V2 from = new V2(200f, 450f);
        Func<V2, V2, EnemyType, float> VolleyAt = (dummyAt, cursor, type) =>
        {
            g.Enemies.Clear(); g.Bullets.Clear();
            var e = (Enemy)spawn.Invoke(g, new object[] { type, dummyAt, 0f, 0, false });
            e.Hp = e.MaxHp = 1e5f; e.SpawnT = 0f; e.FireT = 99f;
            me.Pos = from; me.AimAt = cursor;
            volley.Invoke(g, new object[] { me, 0f, 0 });
            for (int f = 0; f < 50; f++) { e.Pos = dummyAt; e.FrozenT = 1f; me.Pos = from; host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = cursor }); }
            return e.MaxHp - e.Hp;
        };
        Func<V2, V2, float> Volley = (dummyAt, cursor) => VolleyAt(dummyAt, cursor, EnemyType.Brute);
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.TwinRifle);
        // A big target (radius 34) for the comparison: the barrels are 60 px apart, so up close the streams sit ~20 px either
        // side of the aim line, and a small one could be hit by just one stream. Both must land for the doubling to read cleanly.
        float crossed = VolleyAt(from + new V2(300f, 0f), from + new V2(300f, 0f), EnemyType.Gatekeeper);
        float before = VolleyAt(from + new V2(200f, 0f), from + new V2(600f, 0f), EnemyType.Gatekeeper);
        Console.WriteLine("  twin rifle: a volley where the streams cross {0:0.0}; short of the crossing {1:0.0}", crossed, before);
        Check(before > 0f && Math.Abs(crossed - before * Game.TwinCrossMult) < 0.5f, "the Twin Rifle's streams cross at your cursor's distance, and hits there do double");
        // Its barrels are set wide: up close, aiming past a small enemy, the streams go either side of it; and it reaches
        // about 575 px, no further.
        float close = VolleyAt(from + new V2(70f, 0f), from + new V2(500f, 0f), EnemyType.Swarmer);
        float far = Volley(from + new V2(650f, 0f), from + new V2(650f, 0f));
        Console.WriteLine("  twin rifle: a small enemy 70 px away, aiming past it {0:0.0}; a big one 650 px away {1:0.0}", close, far);
        Check(close == 0f && far == 0f, "the Twin Rifle's wide barrels miss a small enemy up close unless you aim at it, and it reaches about 575 px");

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
        var host = HostAs(new Session { AutoStart = 2 }, port, false, "Healer", Role.Healer, 0, 0, Kit.Elementalist);
        var client = JoinAs(port, false, "Tank", Role.Tank, 0);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
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
        var host = HostAs(new Session { AutoStart = 1 }, port, false, "Solo", Role.Dps, 0, 0, Kit.Elementalist);
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
        Console.WriteLine("  ice shards: banked {0} (cap {1}) with {2} flying from the full cast; held with no enemy near {3}; once one came near: {4} left, it took {5:0.0}",
            banked, Game.OrbitCap(0), flying, held, me.Orbiters, t0 - target.Hp);
        Check(banked == Game.OrbitCap(0) && flying == 3 && held, "Ice Shards bank in orbit (a full orbit's cast flies at your aim instead) and wait while no enemy is near");
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
            host.Poll(); g.FixedStep(dt, new PlayerInput { Fire = true, WeaponAction = me.Combo >= Game.ComboMax, AimAt = me.Pos + new V2(400f, 0f) });
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
            "10 Twin Blades hits in a row prime a dash-strike; the weapon-action key lunges you through enemies for triple damage");
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
        var host = HostAs(new Session { AutoStart = 2 }, port, false, "Healer", Role.Healer, 0, 0, Kit.Ninja);
        var client = JoinAs(port, false, "Tank", Role.Tank, 0);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
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
        // The weapon-action key: blink.
        for (int f = 0; f < 3; f++) { host.Poll(); g.FixedStep(dt, new PlayerInput { WeaponAction = f == 0, AimAt = mark }); }
        Console.WriteLine("  kunai: the shadow kunai marked {0} (at {1:0} px; client saw {2}); key blinked to {3:0} px of it; next hit primed {4}",
            marked, mark.X - from.X, clientMark, me.Pos.DistanceTo(mark), me.AmbushT > 0f);
        Check(marked && clientMark && me.Pos.DistanceTo(mark) < 2f && me.AmbushT > 0f && me.KunaiMarkT == 0f, "every 4th kunai leaves a mark; the weapon-action key blinks you there and primes a double hit");
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
        Check(plainMiss == 0f && Math.Abs(homed - me.Weapon.Damage * me.Stats.DmgMult * 0.95f) < 0.5f, "Homing Rounds turn a sent wisp onto a nearby enemy, for 5% less damage");

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
        var host = HostAs(new Session { AutoStart = 2 }, port, false, "Healer", Role.Healer, 0, 0, Kit.Knight);
        var client = JoinAs(port, false, "Tank", Role.Tank, 0);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
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
        for (int f = 0; f < 3; f++) { host.Poll(); g.FixedStep(dt, new PlayerInput { WeaponAction = f == 0, AimAt = star.Pos }); star.Pos = from + new V2(120f, 0f); }  // the weapon-action key
        float recalled = before - star.Hp;
        play(0.4f, new PlayerInput());
        Console.WriteLine("  shuriken: {0} stars stuck (client saw them {1}); the recall ripped them out for {2:0.0}; stuck now {3}", stuck, clientStars, recalled, star.StarsStuck);
        Check(stuck >= 2 && clientStars && recalled >= stuck * me.Weapon.Damage * me.Stats.DmgMult * 0.99f && star.StarsStuck < stuck,  // (the recall's own throws stick again)
            "Shuriken stars stick in what they hit; the weapon-action key recalls them, ripping them out for another hit");
        reset();


        // Bone Spear.
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.BoneSpear);
        me.Kit = Kit.Hemomancer;
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
        // Shuriken again, last (it takes a while, which would shift the wave's end into the healing checks above).
        reset();
        from = new V2(300f, 450f);
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.Shuriken);
        me.Kit = Kit.Ninja;
        play(0.1f, new PlayerInput());  // (back to the start spot)
        // The fan is tight enough that a boss-sized target 400 px away takes all three stars.
        var far = dummy(from + new V2(400f, 0f));
        far.Radius = Defs.Get(EnemyType.Boss).Radius;
        volley.Invoke(g, new object[] { me, 0f, 0 });
        play(0.8f, new PlayerInput());
        int farStuck = far.StarsStuck;
        // Left alone, stuck stars come back by themselves after StarStick seconds, for their second hit.
        float beforeBack = far.Hp;
        play(Game.StarStick + 0.3f, new PlayerInput());
        float back = beforeBack - far.Hp;
        Console.WriteLine("  shuriken: {0} of 3 stars stuck in a boss-sized target 400 px off; left alone they came back for {1:0.0} (stuck now {2})", farStuck, back, far.StarsStuck);
        Check(farStuck == 3, "a Shuriken fan lands all three stars on a boss 400 px away");
        Check(far.StarsStuck == 0 && back >= 3 * me.Weapon.Damage * me.Stats.DmgMult * 0.99f, "stuck stars come back on their own after 5 s, hitting again");
        reset();
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
        var host = HostAs(new Session { AutoStart = 2 }, port, false, "Healer", Role.Healer, 0, 0, Kit.Knight);
        var client = JoinAs(port, false, "Tank", Role.Tank, 0);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
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
        SettleKnocks(g);  // the push slides (Knock.cs)
        float d1 = blown.Pos.DistanceTo(from);
        volley.Invoke(g, new object[] { me, 0f, 0 });
        SettleKnocks(g);
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
        var seedling = dummy(from + new V2(200f, 0f), 13f);  // two fireflies (7 each) kill it
        volley.Invoke(g, new object[] { me, 0f, 0 });
        volley.Invoke(g, new object[] { me, 0f, 0 });
        play(0.8f, new PlayerInput());
        int freed = g.Minions.Values.Count(m => m.Owner == 1 && m.Kind == MinionKind.Sprite && !m.Companion);
        Console.WriteLine("  sprite staff: the seeded enemy died {0}; freed sprites {1}", seedling.Hp <= 0f || !g.Enemies.ContainsKey(seedling.Id), freed);
        Check(freed == 1, "killing a seeded enemy frees a sprite");
        reset();

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
        var host = HostAs(new Session { AutoStart = 2 }, port, false, "Healer", Role.Healer, 0, 0, Kit.Ninja);
        var client = JoinAs(port, false, "Tank", Role.Tank, 0);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
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
}
