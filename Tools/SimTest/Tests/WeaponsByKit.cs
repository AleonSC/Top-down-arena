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
        float close = VolleyAt(from + new V2(70f, 0f), from + new V2(500f, 0f), EnemyType.Spiker);
        float far = Volley(from + new V2(650f, 0f), from + new V2(650f, 0f));
        Console.WriteLine("  twin rifle: a small enemy 70 px away, aiming past it {0:0.0}; a big one 650 px away {1:0.0}", close, far);
        Check(close == 0f && far == 0f, "the Twin Rifle's wide barrels miss a small enemy up close unless you aim at it, and it reaches about 575 px");

        // Extra barrels fill the same width instead of widening it, and a Barrage copy fires from a narrow cluster.
        Func<int, float> Width = extraN =>
        {
            g.Enemies.Clear(); g.Bullets.Clear(); me.Pos = from; me.AimAt = from + new V2(400f, 0f);
            volley.Invoke(g, new object[] { me, 0f, extraN });
            return g.Bullets.Values.Where(b => b.Owner == 1).Max(b => b.Pos.Y) - g.Bullets.Values.Where(b => b.Owner == 1).Min(b => b.Pos.Y);
        };
        float w2 = Width(0), w4 = Width(2);
        Console.WriteLine("  twin rifle: barrel width with 2 barrels {0:0.0}, with 4 {1:0.0}", w2, w4);
        Check(Math.Abs(w4 - w2) < 1f, "extra Twin Rifle barrels fill the same width instead of widening it");

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

        // Twin Blades: the Crescent Waltz (TwinBlades.cs).
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.TwinBlades);
        me.Kit = Kit.Knight;
        g.Enemies.Clear(); g.Bullets.Clear();
        var close = dummy(from + new V2(45f, 0f));
        var beyond = dummy(from + new V2(170f, 0f));
        var steps = new List<int>();
        var twinHeard = new HashSet<Sfx>();
        var twinCues = new HashSet<int>();  // the hit cues' kinds (1-5 a step, 6 a sabre passing, 7 the burst)
        Action twinListen = () =>
        {
            foreach (var c in g.Sounds) twinHeard.Add(c.Sfx);
            foreach (var e in g.Effects) if (e.Kind == FxKind.TwinHit && (int)e.Pos2.X == me.Id) twinCues.Add((int)e.Pos2.Y);
        };
        float closeHp = close.Hp, beyondHp = beyond.Hp;
        for (int f = 0; f < 240 && steps.Count < 6; f++)
        {
            me.Pos = from;
            host.Poll(); g.FixedStep(dt, new PlayerInput { Fire = true, AimAt = from + new V2(400f, 0f) });
            close.Pos = from + new V2(45f, 0f); beyond.Pos = from + new V2(170f, 0f);
            twinListen();
            int st = TwinBladesMotion.StepOf(me.Combo);
            if (st != 0 && (steps.Count == 0 || steps[steps.Count - 1] != st))
                steps.Add(st);
        }
        for (int k = 0; k < 40; k++) { me.Pos = from; host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = from + new V2(400f, 0f) }); beyond.Pos = from + new V2(170f, 0f); twinListen(); }
        float closeDmg = closeHp - close.Hp, beyondDmg = beyondHp - beyond.Hp;
        Console.WriteLine("  twin blades: steps {0}; the close one took {1:0}, the one beyond (only the crescent reaches it) {2:0}", string.Join(",", steps), closeDmg, beyondDmg);
        Check(string.Join(",", steps) == "1,2,3,4,5,1" && closeDmg >= me.Weapon.Damage * 6f && beyondDmg >= me.Weapon.Damage * Game.TwinWaveMult * 0.99f,
            "Twin Blades chain rising cut, falling cut, double jab, pirouette, crescent shear and start over; the shear's crescent reaches past the blades");
        for (int k = 0; k < 90; k++) { me.Pos = from; host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = from + new V2(400f, 0f) }); }
        Check(TwinBladesMotion.StepOf(me.Combo) == 0, "the Twin Blades chain starts over after a pause");

        // Twin Fang: both sabres thrown to the cursor and back.
        float fangHp = beyond.Hp;
        V2 cursor = from + new V2(170f, 0f);
        for (int k = 0; k < 90; k++)
        {
            me.Pos = from;
            host.Poll(); g.FixedStep(dt, new PlayerInput { WeaponAction = k < 2, AimAt = cursor });
            beyond.Pos = cursor;
            twinListen();
        }
        float fangDmg = fangHp - beyond.Hp, cdAfter = me.FangCd;
        Console.WriteLine("  twin fang: the enemy at the cursor took {0:0}; cooldown left {1:0.0} s", fangDmg, cdAfter);
        Check(fangDmg >= me.Weapon.Damage * (Game.FangBurstMult + 2f * Game.FangMult) * 0.99f && cdAfter > 0f && cdAfter < Game.FangCooldown,
            "Twin Fang throws both sabres to the cursor, they burst where they cross and cut it again on the way back, then it waits on its cooldown");
        var twinWant = new[] { Sfx.TwinCut1, Sfx.TwinCut2, Sfx.TwinJab, Sfx.TwinSpin, Sfx.TwinShear, Sfx.TwinHit, Sfx.TwinHitHeavy, Sfx.TwinThrow, Sfx.TwinCross, Sfx.TwinCatch };
        var twinMissing = twinWant.Where(x => !twinHeard.Contains(x)).ToList();
        Console.WriteLine("  twin sounds missing: {0}; hit cues seen: {1}", twinMissing.Count == 0 ? "none" : string.Join(", ", twinMissing), string.Join(",", twinCues.OrderBy(x => x)));
        Check(twinMissing.Count == 0, "every Twin Blades sound is raised: both cuts, the jab, the Pirouette, the Shear, a cut and a heavy cut landing, Twin Fang's throw, burst and catch");
        Check(twinCues.SetEquals(new[] { 1, 2, 3, 4, 5, 6, 7 }), "every step and both kinds of Twin Fang hit send a hit cue (your hit-pause and shake)");
        Check(!twinHeard.Contains(Sfx.SwingLight) && !twinHeard.Contains(Sfx.SwingHeavy), "the Twin Blades no longer use the generic swing whooshes");
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
        for (int f = 0; f < 600 && imps() < 5; f++) { me.CompanionCd = Math.Min(me.CompanionCd, 0.05f); play(1f / 60f, new PlayerInput()); }
        var target = dummy(from + new V2(240f, 0f));
        me.AimAt = target.Pos;
        me.BowShotDraw = Game.BowMinDraw;
        volley.Invoke(g, new object[] { me, 0f, 0 });
        int afterTap = imps();  // before the 1 s respawn refills it
        play(1.5f, new PlayerInput { AimAt = target.Pos });
        float tap = target.MaxHp - target.Hp;
        for (int f = 0; f < 600 && imps() < 5; f++) { me.CompanionCd = Math.Min(me.CompanionCd, 0.05f); play(1f / 60f, new PlayerInput { AimAt = target.Pos }); }
        float t1 = target.Hp;
        me.BowShotDraw = g.BowFull(me);
        volley.Invoke(g, new object[] { me, 0f, 0 });
        var big = g.Minions.Values.FirstOrDefault(m => m.Owner == 1 && m.Kind == MinionKind.Imp && m.Sent);
        float bigScale = big != null ? big.Scale : 0f;
        int sentCount = g.Minions.Values.Count(m => m.Owner == 1 && m.Kind == MinionKind.Imp && m.Sent);
        play(1.5f, new PlayerInput { AimAt = target.Pos });
        float merged = t1 - target.Hp;
        Console.WriteLine("  imp caller: a tap sent 1 imp (left {4} waiting) for {0:0.0}; a full charge merged them into one (scale {1:0.00}, {2} sent) for {3:0.0}", tap, bigScale, sentCount, merged, afterTap);
        Check(tap > 0f && afterTap == 4 && sentCount == 1 && bigScale > 1.2f && merged > tap * 2f, "a tap sends one waiting imp at your cursor; a full charge merges them all into one bigger blast");
        host.Leave(""); client.Leave("");
    }

    static void FourMoreTest(float dt)
    {
        // Shuriken stars stick and are recalled; Bone Spears stay stuck as health pickups; the
        // Treant Rod's Treant roots with a vine to you that heals and slows.
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

        // (The Lightbringer has its own suite: run.sh 1 30 lightbringer, Tests/Lightbringer.cs.)

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
        // The Censer's incense heals and spoils enemy aim (its rite and Censer Cast too); the Spirit Fan alternates pushing and pulling; Ofuda
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

        // Censer: walking leaves no incense; a sweep leaves a puff where the head ends up; an ally in it heals; enemy shots fired
        // from inside it stray.
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.Censer);
        var censerHeard = new HashSet<Sfx>();
        Action<float, PlayerInput> censerPlay = (secs, input) =>
        {
            for (int f = 0; f < Math.Max(1, (int)Math.Round(secs * 60)); f++) { play(1f / 60f, input); foreach (var c in g.Sounds) censerHeard.Add(c.Sfx); }
        };
        pinMe = false;
        me.Pos = from;
        censerPlay(1f, new PlayerInput { Move = new V2(1f, 0f), AimAt = from + new V2(400f, 0f) });
        pinMe = true;
        int walked = g.Zones.Values.Count(z => z.Kind == ZoneKind.Incense);
        from = me.Pos;
        allyAt = from + new V2(0f, 100f);  // in the sweep's 140 px heal: the blessing chimes
        censerPlay(2f / 60f, new PlayerInput { Fire = true, AimAt = from + new V2(400f, 0f) });
        censerPlay(0.5f, new PlayerInput { AimAt = from + new V2(400f, 0f) });
        int puffs = g.Zones.Values.Count(z => z.Kind == ZoneKind.Incense);
        bool clientTrail = client.Game.Zones.Values.Any(z => z.Kind == ZoneKind.Incense);
        var puff = g.Zones.Values.Where(z => z.Kind == ZoneKind.Incense).OrderBy(z => z.T).Last();
        allyAt = puff.Pos;
        ally.Hp = ally.MaxHp * 0.3f;
        float a0 = ally.Hp;
        from = new V2(300f, 800f);  // step away so the swing's healing doesn't count
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
        Console.WriteLine("  censer: walking left {0} incense; a sweep left {1} puff (client saw it {2}); an ally in it healed {3:0.0} in 0.5 s; enemy shots from inside strayed {4}/20, outside straight {5}/20",
            walked, puffs, clientTrail, trailHeal, strayed, straight);
        Check(walked == 0, "walking with the Censer leaves no incense trail");
        Check(puffs == 1 && clientTrail && trailHeal > 0f, "a Censer sweep leaves a puff of incense where the head ends up, healing allies in it");
        Check(strayed >= 17 && straight == 20, "enemy shots fired from inside the incense stray");
        from = new V2(300f, 450f); allyAt = new V2(300f, 150f);
        reset();

        // Censer: the Thurible Rite (sweep, return sweep, Benediction), Embers and Censer Cast (Censer.cs).
        me.Embers = 0; me.Combo = 0; g.Zones.Clear();
        var front = dummy(from + new V2(55f, 0f), 1e5f);
        var behind = dummy(from + new V2(-62f, 0f), 1e5f);  // only the Benediction goes all the way round
        allyAt = from + new V2(0f, 150f);  // outside the sweeps' 140 px heal, inside the Benediction's 170
        play(0.2f, new PlayerInput());
        ally.Hp = ally.MaxHp * 0.3f;
        float frontHp = front.Hp, behindHp = behind.Hp, allyHp0 = ally.Hp;
        var rite = new List<int>();
        int peakEmbers = 0, clientEmbers = 0;
        float bestJump = 0f;  // the biggest one-frame heal the ally got (the healer's aura trickles; a step's heal lands at once)
        for (int f = 0; f < 180 && rite.Count < 4; f++)
        {
            float hpWas = ally.Hp;
            censerPlay(1f / 60f, new PlayerInput { Fire = true, AimAt = from + new V2(400f, 0f) });
            bestJump = Math.Max(bestJump, ally.Hp - hpWas);
            int st = CenserMotion.StepOf(me.Combo);
            if (st != 0 && (rite.Count == 0 || rite[rite.Count - 1] != st))
                rite.Add(st);
            peakEmbers = Math.Max(peakEmbers, me.Embers);
            clientEmbers = Math.Max(clientEmbers, client.Game.Players[1].Embers);
        }
        censerPlay(0.6f, new PlayerInput { AimAt = from + new V2(400f, 0f) });
        peakEmbers = Math.Max(peakEmbers, me.Embers);
        clientEmbers = Math.Max(clientEmbers, client.Game.Players[1].Embers);
        float frontDmg = frontHp - front.Hp, behindDmg = behindHp - behind.Hp, allyGain = ally.Hp - allyHp0;
        int incense = g.Zones.Values.Count(z => z.Kind == ZoneKind.Incense);
        Console.WriteLine("  censer rite: steps {0}; front took {1:0}, behind {2:0}; Embers peaked at {3} (client saw {4}); the ally 150 px off healed {5:0.0} (at most {7:0.0} at once); {6} incense zones",
            string.Join(",", rite), frontDmg, behindDmg, peakEmbers, clientEmbers, allyGain, incense, bestJump);
        Check(string.Join(",", rite) == "1,2,3,1", "the Censer's rite goes sweep, return sweep, Benediction, then starts over");
        Check(frontDmg >= 13f * 2f + 18f - 0.5f && behindDmg >= 17.5f && behindDmg <= 18.5f, "the sweeps hit in front only; the Benediction goes all the way round");
        Check(peakEmbers == g.EmberCap(me) && clientEmbers == peakEmbers, "every enemy a step hits lights an Ember, up to the cap, and other players see them");
        Check(bestJump >= 8f - 0.01f && bestJump < 12f, "the Benediction heals further than the sweeps, and 1 more for each enemy it hit");
        Check(incense >= 3, "the sweeps leave puffs of incense and the Benediction a cloud");
        for (int k = 0; k < 60; k++) play(1f / 60f, new PlayerInput { AimAt = from + new V2(400f, 0f) });
        Check(CenserMotion.StepOf(me.Combo) == 0, "the Censer's rite starts over after a pause");

        // Censer Cast: the head thrown to the cursor spends every Ember on a burst and a healing cloud.
        g.Zones.Clear();
        V2 castAt = from + new V2(240f, 0f);
        var target = dummy(castAt, 1e5f);
        allyAt = castAt + new V2(0f, 30f);
        play(0.1f, new PlayerInput());
        ally.Hp = ally.MaxHp * 0.3f;
        int spent = me.Embers;
        float targetHp = target.Hp, castAlly0 = ally.Hp;
        for (int k = 0; k < 66; k++) censerPlay(1f / 60f, new PlayerInput { WeaponAction = k < 2, AimAt = castAt });
        var cloud = g.Zones.Values.Where(z => z.Kind == ZoneKind.Incense).OrderByDescending(z => z.Radius).FirstOrDefault();
        float castDmg = targetHp - target.Hp, castHeal = ally.Hp - castAlly0;
        Console.WriteLine("  censer cast: spent {0} Embers; the enemy at the cursor took {1:0}; cloud {2:0} px at {3:0} px out; the ally in it healed {4:0.0}; Embers left {5}, cooldown {6:0.0} s",
            spent, castDmg, cloud != null ? cloud.Radius : 0f, cloud != null ? cloud.Pos.DistanceTo(from) : 0f, castHeal, me.Embers, me.CenserCd);
        Check(spent > 0 && castDmg >= Game.CastBurstPerEmber * spent * 0.99f && me.Embers == 0, "Censer Cast bursts for every Ember spent, where the cursor is, and spends them all");
        Check(cloud != null && Math.Abs(cloud.Radius - (Game.CastCloudBase + Game.CastCloudPerEmber * spent)) < 0.5f && cloud.Pos.DistanceTo(castAt) < 2f && castHeal >= Game.CastHealBase + spent,
            "the cast leaves an incense cloud that grows with each Ember and heals the allies in it");
        Check(me.CenserCd > 0f && me.CenserCd < Game.CastCooldown, "Censer Cast then waits on its cooldown");
        int before = (int)target.Hp;
        for (int k = 0; k < 10; k++) play(1f / 60f, new PlayerInput { WeaponAction = k % 2 == 0, AimAt = castAt });
        Check((int)target.Hp == before, "with no Embers, or on its cooldown, the key does nothing");

        // Censer Cast on a downed ally: the landing brings them a share of the way back, and the cloud revives them twice as fast.
        g.Zones.Clear();
        me.CenserCd = 0f; me.Embers = 3;
        typeof(Game).GetField("censerLocalCd", flags).SetValue(g, 0f);
        ally.Downed = true; ally.Revive = 0f;
        for (int k = 0; k < 24; k++) censerPlay(1f / 60f, new PlayerInput { WeaponAction = k < 2, AimAt = castAt });  // to the landing (0.38 s)
        float kick = ally.Revive;
        censerPlay(0.5f, new PlayerInput { AimAt = castAt });
        float later = ally.Revive - kick, normal = 0.5f / g.D(DiffKnob.ReviveTime);
        Console.WriteLine("  censer cast on a downed ally: revive {0:0.00} when it landed, +{1:0.000} in the next 0.5 s (a teammate beside them alone: {2:0.000})", kick, later, normal);
        Check(kick >= Game.CastReviveKick - 0.01f && later >= normal * 0.9f, "Censer Cast landing on a downed ally brings them a fifth of the way back, and its cloud revives them as fast again as a teammate beside them");
        ally.Downed = false; ally.Revive = 0f; ally.Hp = ally.MaxHp;
        var censerWant = new[] { Sfx.CenserSweep1, Sfx.CenserSweep2, Sfx.CenserWhirl, Sfx.CenserHit, Sfx.CenserHitHeavy, Sfx.CenserBless, Sfx.CenserBell, Sfx.CenserThrow, Sfx.CenserLand, Sfx.CenserCatch };
        var censerMissing = censerWant.Where(x => !censerHeard.Contains(x)).ToList();
        Console.WriteLine("  censer sounds missing: {0}", censerMissing.Count == 0 ? "none" : string.Join(", ", censerMissing));
        Check(censerMissing.Count == 0, "every Censer sound is raised: both sweeps, the whirl, a hit and a heavy hit, the blessing, the bell, the throw, the landing and the catch");
        Check(!censerHeard.Contains(Sfx.SwingLight) && !censerHeard.Contains(Sfx.SwingHeavy), "the Censer no longer uses the generic swing whooshes");
        allyAt = new V2(300f, 150f);
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
        // and steal a sliver from enemies; the Rapier's Tempo, skewering and Flourish; the Katana's chain, meter and Alt strike.
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

        // Rapier (Rapier.cs): a thrust skewers enemy bullets along its line (3 at most); they add no Tempo, the enemy hit adds one.
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.Rapier);
        me.RapierTempoF = 0f; me.RapierTempo = 0; me.FireCd = 0f;
        var mark = dummy(from + new V2(120f, 0f), 1e5f);
        volley.Invoke(g, new object[] { me, 0f, 0 });
        play(0.1f, new PlayerInput());
        int s0 = me.RapierTempo;
        var shots = new List<Bullet>();
        for (int k = 0; k < 5; k++)
            shots.Add((Bullet)shoot.Invoke(g, new object[] { Defs.SmallShot, from + new V2(30f + 18f * k, 0f), new V2(0f, 0f), 0f, 5f, 5f, 0, false, 0f }));
        var aside = (Bullet)shoot.Invoke(g, new object[] { Defs.SmallShot, from + new V2(60f, 80f), new V2(0f, 0f), 0f, 5f, 5f, 0, false, 0f });
        volley.Invoke(g, new object[] { me, 0f, 0 });
        play(0.05f, new PlayerInput());
        int skewered = shots.Count(b => !g.Bullets.ContainsKey(b.Id));
        bool asideLeft = g.Bullets.ContainsKey(aside.Id);
        Console.WriteLine("  rapier: Tempo {0} -> {1}; skewered {2} of 5 bullets on its line, one off the line left alone {3}", s0, me.RapierTempo, skewered, asideLeft);
        Check(s0 == 1, "a Rapier thrust that hits an enemy adds one Tempo");
        Check(skewered == Game.RapierSkewerMax && asideLeft, "a Rapier thrust skewers up to 3 enemy bullets along its line");
        Check(me.RapierTempo == s0 + 1, "skewered bullets add no Tempo (only the enemy hit does)");
        reset();

        // Tempo makes the thrusts quicker: the wait after a thrust at 0 Tempo against one at full.
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.Rapier);
        dummy(from + new V2(80f, 0f), 1e6f);
        me.RapierTempoF = 0f; me.RapierTempo = 0; me.FireCd = 0f;
        volley.Invoke(g, new object[] { me, 0f, 0 });
        float slowCd = me.FireCd;
        me.RapierTempoF = Game.RapierTempoMax; me.RapierTempo = Game.RapierTempoMax; me.FireCd = 0f;
        volley.Invoke(g, new object[] { me, 0f, 0 });
        float fastCd = me.FireCd;
        Console.WriteLine("  rapier tempo: the next thrust waits {0:0.000} s at 0 Tempo, {1:0.000} s at {2}", slowCd, fastCd, Game.RapierTempoMax);
        Check(Math.Abs(slowCd / fastCd - Game.RapierSlow / Game.RapierFast) < 0.05f, "a full-Tempo Rapier thrusts three times as often as at 0 Tempo");
        Check(fastCd >= Game.RapierMinInterval - 1e-4f, "the Rapier's thrusts never come quicker than its floor");
        play(0.6f, new PlayerInput());
        reset();

        // Grace, then the drain: Tempo holds for RapierGrace after the last hit, then drops RapierDrain a second.
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.Rapier);
        me.RapierTempoF = 0f; me.RapierTempo = 0; me.FireCd = 0f;
        dummy(from + new V2(80f, 0f), 1e6f);
        for (int k = 0; k < 10; k++) { me.RapierTempoF = Math.Min(me.RapierTempoF, 9f); volley.Invoke(g, new object[] { me, 0f, 0 }); play(0.1f, new PlayerInput()); }
        int built = me.RapierTempo, seen = client.Game.Players[1].RapierTempo;
        play(Game.RapierGrace - 0.4f, new PlayerInput());
        int heldT = me.RapierTempo;
        bool graceShown = me.RapierGraceLeft > 0;
        play(1.5f, new PlayerInput());
        int drained = me.RapierTempo;
        Console.WriteLine("  rapier drain: built {0} (the client sees {1}); {2} near the end of the grace period; {3} 1.5 s later", built, seen, heldT, drained);
        Check(built == 10 && seen == built, "Tempo builds a stack a hit and every screen sees it");
        Check(heldT == built && graceShown, "Tempo holds through the grace period");
        Check(drained < heldT && drained >= heldT - 4, "after the grace period Tempo drains slowly");
        reset();

        // With the +1 upgrade a thrust hits two enemies: two Tempo, and both take the same flat damage.
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.Rapier);
        me.RapierTempoF = 0f; me.RapierTempo = 0; me.FireCd = 0f;
        var near2 = dummy(from + new V2(60f, 0f), 1e6f);
        var far2 = dummy(from + new V2(100f, 0f), 1e6f);
        var tempos = new List<int>();
        float nearDmg = 0f, farDmg = 0f, firstNear = 0f;
        for (int k = 0; k < 4; k++)
        {
            near2.Hp = near2.MaxHp; far2.Hp = far2.MaxHp;
            volley.Invoke(g, new object[] { me, 0f, 1 });
            play(0.1f, new PlayerInput());
            tempos.Add(me.RapierTempo);
            nearDmg = near2.MaxHp - near2.Hp; farDmg = far2.MaxHp - far2.Hp;
            if (k == 0) firstNear = nearDmg;
        }
        Console.WriteLine("  rapier +1: Tempo per thrust {0}; last thrust did {1:0.0} to the near enemy, {2:0.0} to the far one (first {3:0.0})", string.Join(",", tempos), nearDmg, farDmg, firstNear);
        Check(me.RapierTempo == 8, "a two-target Rapier thrust adds two Tempo");
        Check(farDmg > 0f && Math.Abs(nearDmg - farDmg) < 0.5f && Math.Abs(nearDmg - firstNear) < 0.5f, "every Rapier thrust deals the same damage to both enemies, however many in a row");
        reset();

        // Flourish: the weapon-action key spends all the Tempo on one swing (and one piercing wave) per two stacks.
        var flourish = typeof(Game).GetMethod("HostRapierFlourish", flags);
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.Rapier);
        Func<int, float> flourishDmg = tempo =>
        {
            var a1 = dummy(from + new V2(170f, 0f), 1e6f);
            var a2 = dummy(from + new V2(260f, 0f), 1e6f);
            me.RapierTempoF = tempo; me.RapierTempo = tempo; me.RapierHitAgo = 0f; me.FireCd = 0f;
            flourish.Invoke(g, new object[] { me, from + new V2(300f, 0f) });
            play(1.8f, new PlayerInput());
            float d = (a1.MaxHp - a1.Hp) + (a2.MaxHp - a2.Hp);
            reset();
            return d;
        };
        float lowFl = flourishDmg(2), smallFl = flourishDmg(4);
        int left = me.RapierTempo;
        float fullFl = flourishDmg(Game.RapierTempoMax);
        Console.WriteLine("  rapier flourish: with 2 Tempo {0:0}, with 4 {1:0} (Tempo after {2}), with {3} {4:0}", lowFl, smallFl, left, Game.RapierTempoMax, fullFl);
        Check(lowFl == 0f, "a Flourish needs at least 3 Tempo");
        Check(smallFl > 0f && left == 0, "a Flourish throws waves and spends the Tempo");
        Check(Math.Abs(fullFl / smallFl - Game.RapierFlourishSwings(Game.RapierTempoMax) / (float)Game.RapierFlourishSwings(4)) < 0.6f, "a full-Tempo Flourish throws a wave per two stacks, each piercing both enemies in line");

        // Katana (Docs/KatanaCombo.md): swipe, swipe, lunge in that order; the lunge cuts through everything in its line; a pause starts the chain over.
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.Katana);
        me.Hp = me.MaxHp;  // (Blood Price would change the damage as health moves)
        var nearK = dummy(from + new V2(60f, 0f), 1e5f);
        var farK = dummy(from + new V2(200f, 0f), 1e5f);
        me.KatanaStep = 0; me.KatanaMeter = 0f; me.KatanaGap = 0f;
        var kNear = new float[3]; var kFar = new float[3]; var kOrder = new int[3];
        Func<Sfx, bool> heard = x => g.Sounds.Any(c => c.Sfx == x);
        Func<Sfx, int> heardAt = x => g.Sounds.FindIndex(c => c.Sfx == x);
        g.Sounds.Clear();
        Effect lungeCue = null;
        for (int step = 0; step < 3; step++)
        {
            nearK.Hp = nearK.MaxHp; farK.Hp = farK.MaxHp;
            kOrder[step] = me.KatanaStep;
            volley.Invoke(g, new object[] { me, 0f, 0 });
            if (step == 2)
            {
                play(0.18f, new PlayerInput());  // the lunge has landed (0.15 s), its cue is still alive (0.2 s)
                lungeCue = g.Effects.FirstOrDefault(fx => fx.Kind == FxKind.KatanaHitCue && fx.Color.A > 0.75f);
                play(0.12f, new PlayerInput());
            }
            else
                play(0.3f, new PlayerInput());
            kNear[step] = nearK.MaxHp - nearK.Hp; kFar[step] = farK.MaxHp - farK.Hp;
        }
        Console.WriteLine("  katana: steps {0},{1},{2}; near {3:0}/{4:0}/{5:0}, far {6:0}/{7:0}/{8:0}; next step {9}; meter {10:0}",
            kOrder[0], kOrder[1], kOrder[2], kNear[0], kNear[1], kNear[2], kFar[0], kFar[1], kFar[2], me.KatanaStep, me.KatanaMeter);
        Check(kOrder[0] == 0 && kOrder[1] == 1 && kOrder[2] == 2 && me.KatanaStep == 0, "the Katana goes swipe, swipe, lunge, then starts over");
        Console.WriteLine("  katana sounds: swipe1 {0}, swipe2 {1}, wind-up {2}, lunge {3}, hit {4}, generic swing {5}; lunge cue cut {6}",
            heard(Sfx.KatanaSwipe1), heard(Sfx.KatanaSwipe2), heard(Sfx.KatanaWindup), heard(Sfx.KatanaLunge), heard(Sfx.KatanaHit),
            heard(Sfx.SwingLight) || heard(Sfx.SwingHeavy), lungeCue == null ? -1f : lungeCue.Radius);
        Check(heard(Sfx.KatanaSwipe1) && heard(Sfx.KatanaSwipe2) && heard(Sfx.KatanaWindup) && heard(Sfx.KatanaLunge) && heard(Sfx.KatanaHit)
            && !heard(Sfx.SwingLight) && !heard(Sfx.SwingHeavy), "the chain plays the Katana's own Steel sounds (swipes, wind-up, lunge, cut), not the Knight's");
        Check(lungeCue != null && (int)lungeCue.Radius == 2 && (int)lungeCue.Pos2.X == me.Id, "a lunge that cuts sends a hit cue with the enemies it cut and its owner (for the hit-pause)");
        Check(kNear[0] > 0f && kFar[0] == 0f && kFar[1] == 0f && Math.Abs(kNear[1] - kNear[0]) < 0.5f, "both swipes keep the short reach and the same damage");
        Check(Math.Abs(kNear[2] - kNear[0] * Game.KatanaLungeMult) < 1.5f && Math.Abs(kFar[2] - kNear[2]) < 1.5f, "the lunge cuts through everything in its line for 2.25x");
        Check(Math.Abs(me.KatanaMeter - (Game.KatanaSwipeMeter * 2 + Game.KatanaLungeMeter * 2)) < 0.5f && client.Game.Players[1].KatanaMeter > 0f, "each enemy a step hits builds the strike meter (and the client sees it)");
        volley.Invoke(g, new object[] { me, 0f, 0 });
        play(0.05f, new PlayerInput());
        float held = me.KatanaMeter;
        Check(me.MeleeGraceT > 0f, "a melee swing that lands keeps contact damage off you for a moment");
        play(1.0f, new PlayerInput());
        Check(me.KatanaStep == 0 && me.KatanaMeter == held, "a pause of 0.7 s resets the chain; the meter holds for 1.2 s");
        play(0.2f + 1.0f, new PlayerInput());
        Check(me.KatanaMeter < held - 15f && me.KatanaMeter > 0f, "then the meter drains");

        // Tapping ahead queues the next step: two quick taps play swipe 1 at once and swipe 2 the moment it ends, with no third press.
        me.KatanaStep = 0; me.KatanaGap = 5f; me.FireCd = 0f;
        var tapSteps = new List<int>();
        int lastStep = me.KatanaStep;
        for (int f = 0; f < 0.6f * 60; f++)
        {
            bool tap = f == 0 || f == 6;  // a press, then another during the cooldown
            play(1f / 60f, new PlayerInput { Fire = tap, AimAt = from + new V2(100f, 0f) });
            if (me.KatanaStep != lastStep) { tapSteps.Add(me.KatanaStep); lastStep = me.KatanaStep; }
        }
        Console.WriteLine("  katana queue: steps after two taps {0}", string.Join(",", tapSteps));
        Check(tapSteps.Count >= 2 && tapSteps[0] == 1 && tapSteps[1] == 2, "a tap during a step queues the next one, which starts as soon as the first ends");

        // The lunge steps you forward as it lands (your own game moves you; nothing here pins the player).
        reset();
        me.KatanaStep = 2; me.KatanaGap = 0f; me.FireCd = 0f;
        me.Pos = from;
        byte lungesBefore = me.KatanaLunges;
        volley.Invoke(g, new object[] { me, 0f, 0 });
        for (int f = 0; f < 0.6f * 60; f++)
        {
            host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = from + new V2(300f, 0f) }); g.Update(dt);
            client.Poll(); client.Game.FixedStep(dt, new PlayerInput()); client.Game.Update(dt);
        }
        float stepped = me.Pos.X - from.X;
        Console.WriteLine("  katana lunge: stepped {0:0} px forward (lunge count {1} -> {2})", stepped, lungesBefore, me.KatanaLunges);
        Check(me.KatanaLunges == (byte)(lungesBefore + 1) && Math.Abs(stepped - Game.KatanaLungeStep) < 20f, "the lunge steps you forward about 110 px (a dash moves in whole frames)");
        me.Pos = from;

        // Raising the parry cancels the chain: back to swipe 1, no wait for the next step, and a lunge that hasn't landed never does.
        reset();
        var parryDummy = dummy(from + new V2(150f, 0f), 1e5f);
        me.KatanaStep = 2; me.KatanaGap = 0f; me.FireCd = 0f; me.KatanaMeter = 40f; me.KatanaIdle = 0f;
        byte lungesP = me.KatanaLunges;
        volley.Invoke(g, new object[] { me, 0f, 0 });  // the lunge starts pulling back
        bool pending = me.KatanaLunges == (byte)(lungesP + 1) && me.FireCd > 0.3f;
        play(0.05f, new PlayerInput { Parry = true });
        play(Game.KatanaLungeWind + 0.3f, new PlayerInput { Parry = true });
        Console.WriteLine("  katana parry cancel: pending {0}; step {1}, fire wait {2:0.00}, lunge never landed {3}, count back {4}, meter kept {5:0}", pending, me.KatanaStep, me.FireCd, parryDummy.Hp == parryDummy.MaxHp, me.KatanaLunges == lungesP, me.KatanaMeter);
        Check(pending && me.KatanaStep == 0 && me.FireCd <= 0f, "raising the parry sends the Katana back to swipe 1 and clears its wait");
        Check(parryDummy.Hp == parryDummy.MaxHp && me.KatanaLunges == lungesP, "a lunge that has not landed yet never does (and the step forward is taken back)");
        Check(me.KatanaMeter >= 40f, "the strike meter is not touched by the parry");
        play(0.7f, new PlayerInput());
        me.KatanaStep = 1; me.FireCd = 0.2f;
        play(0.05f, new PlayerInput { Parry = true });
        Check(me.KatanaStep == 0 && me.FireCd <= 0f, "it cancels after a swipe too");
        play(0.6f, new PlayerInput());

        // The same for the Bo Staff: back to the slash with no wait, and a shove still drawing back never lands (no cooldown spent).
        reset();
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.BoStaff);
        var parryBo = dummy(from + new V2(60f, 0f), 1e5f);
        me.BoStep = 2; me.ShoveCd = 0f; me.FireCd = 0f;
        typeof(Game).GetMethod("HostWeaponDash", flags).Invoke(g, new object[] { 1, 6, from + new V2(100f, 0f) });  // the shove starts drawing back
        play(0.04f, new PlayerInput { Parry = true, AimAt = from + new V2(100f, 0f) });
        play(0.4f, new PlayerInput { Parry = true, AimAt = from + new V2(100f, 0f) });
        Check(me.BoStep == 0 && me.FireCd <= 0f && parryBo.Hp == parryBo.MaxHp && me.ShoveCd <= 0f, "raising the parry cancels a Bo Staff shove that has not landed and clears its wait");
        play(0.6f, new PlayerInput());
        me.BoStep = 2; me.FireCd = 0.5f;
        play(0.05f, new PlayerInput { Parry = true });
        Check(me.BoStep == 0 && me.FireCd <= 0f, "and sends the Bo Staff chain back to the slash");
        play(0.6f, new PlayerInput());
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.Katana);  // (the Katana's Alt strike tests follow)

        // The Alt strike: hold the weapon-action key with a full meter to aim, let go to cut across the arena to the enemy and land behind it.
        reset();
        me.Hp = me.MaxHp;
        var kTarget = dummy(from + new V2(400f, 0f), 1e5f);
        var kRoute = dummy(from + new V2(200f, 6f), 1e5f);
        var kOff = dummy(from + new V2(200f, -150f), 1e5f);
        var hostDash = typeof(Game).GetMethod("HostWeaponDash", flags);
        me.KatanaMeter = 50f; me.KatanaIdle = 0f;
        hostDash.Invoke(g, new object[] { 1, 5, kTarget.Pos });
        play(Game.KatanaWind + Game.KatanaCut + 0.2f, new PlayerInput());
        Check(kTarget.Hp == kTarget.MaxHp && me.KatanaMeter > 0f, "a strike with less than a full meter does nothing");
        g.Sounds.Clear();
        me.KatanaMeter = 50f; me.KatanaIdle = 0f;
        play(0.05f, new PlayerInput { WeaponAction = true, AimAt = kTarget.Pos });
        play(0.05f, new PlayerInput { AimAt = kTarget.Pos });
        bool emptyTick = heard(Sfx.KatanaEmpty) && !heard(Sfx.KatanaReady);
        me.KatanaMeter = 100f; me.KatanaIdle = 0f;
        play(0.1f, new PlayerInput { WeaponAction = true, AimAt = kTarget.Pos });
        bool aiming = g.KatanaAiming && g.KatanaMark == kTarget.Id;
        bool readyAim = heard(Sfx.KatanaReady) && heard(Sfx.KatanaAim);
        g.Sounds.Clear();
        play(0.05f, new PlayerInput { AimAt = kTarget.Pos });  // let go (the cursor stays where it was)
        bool started = g.KatanaStriking && me.KatanaMeter == 0f && me.InvulnT > 0f;
        play(Game.KatanaWind + Game.KatanaCut + Game.KatanaHold + 0.2f, new PlayerInput());
        int sStance = heardAt(Sfx.KatanaStance), sCut = heardAt(Sfx.KatanaStrikeCut), sSheathe = heardAt(Sfx.KatanaSheathe), sBlow = heardAt(Sfx.KatanaBlow);
        Console.WriteLine("  katana strike sounds: empty tick {0}, ready + aim {1}; order stance {2}, cut {3}, sheathe {4}, blow {5}; lost {6}", emptyTick, readyAim, sStance, sCut, sSheathe, sBlow, heard(Sfx.KatanaLost));
        Check(emptyTick, "pressing the key without a full meter ticks (and the meter isn't full)");
        Check(readyAim, "the meter reaching full chimes, and aiming starts with a breath in");
        Check(sStance >= 0 && sStance < sCut && sCut < sSheathe && sSheathe < sBlow, "the strike plays stance, cut, sheathe and blow in order");
        Check(heard(Sfx.KatanaLost), "a target that lives plays the streak-lost drop");
        float hitT = kTarget.MaxHp - kTarget.Hp, hitR = kRoute.MaxHp - kRoute.Hp, hitO = kOff.MaxHp - kOff.Hp;
        Console.WriteLine("  katana strike: aiming {0}, started {1}; target took {2:0}, enemy on the route {3:0}, one off it {4:0}; striking after {5}", aiming, started, hitT, hitR, hitO, g.KatanaStriking);
        Check(aiming, "holding the key with a full meter aims at the enemy nearest the cursor");
        Check(started, "letting go spends the whole meter and starts the strike (untouchable)");
        Check(Math.Abs(hitT - kNear[0] * Game.KatanaStrikeDmg / Defs.Weapons.First(w => w.Id == WeaponId.Katana).Damage) < 1.5f, "the target takes the full 120");
        Check(Math.Abs(hitR - hitT / 2f) < 1.5f && hitO == 0f, "an enemy on the route takes half, one off it nothing");
        Check(Math.Abs(g.KatanaStrikeEnd(from, kTarget.Pos, 10f).X - (kTarget.Pos.X + Game.KatanaBehind)) < 1f, "the strike lands you behind the target, on the far side");

        // The chain streak: a kill keeps the meter full and adds a chain; a target that lives spends it and ends the streak; the route's kills
        // don't count; the meter doesn't drain on a streak; at five the next strike is empowered (240 to the target, 120 on the route).
        float strikeWait = Game.KatanaWind + Game.KatanaCut + Game.KatanaHold + 0.25f;
        Action<Enemy> strikeAt = t => { me.KatanaMeter = 100f; me.KatanaIdle = 0f; hostDash.Invoke(g, new object[] { 1, 5, t.Pos }); play(strikeWait, new PlayerInput()); };
        reset();
        var kill1 = dummy(from + new V2(400f, 0f), 100f);
        var routeKill = dummy(from + new V2(200f, 6f), 50f);
        me.KatanaChain = 0;
        bool predictKill = g.KatanaWouldKill(me, kill1);
        g.Sounds.Clear();
        strikeAt(kill1);
        Check(heard(Sfx.KatanaChain1) && !heard(Sfx.KatanaLost), "a kill adds a chain and plays its first chime");
        Console.WriteLine("  katana chain: target dead {0}, route dead {1}, chain {2}, meter {3:0}", kill1.Hp <= 0f, routeKill.Hp <= 0f, me.KatanaChain, me.KatanaMeter);
        Check(kill1.Hp <= 0f && me.KatanaMeter == 100f && me.KatanaChain == 1, "a strike that kills its target keeps the meter full and adds a chain");
        Check(routeKill.Hp <= 0f && me.KatanaChain == 1, "kills along the route do not add to the chain");
        reset();
        var tough = dummy(from + new V2(400f, 0f), 500f);
        me.KatanaChain = 3;
        bool predictLive = !g.KatanaWouldKill(me, tough);
        strikeAt(tough);
        Check(predictKill && predictLive, "the aim overlay's kill check is right for a target that dies (100) and one that lives (500)");
        Check(tough.Hp > 0f && me.KatanaMeter == 0f && me.KatanaChain == 0, "a target that survives spends the meter and ends the streak");
        reset();
        me.KatanaChain = 2; me.KatanaMeter = 80f; me.KatanaIdle = 0f;
        play(Game.KatanaDrainDelay + 1.5f, new PlayerInput());
        bool held80 = me.KatanaMeter == 80f;
        me.KatanaChain = 0; me.KatanaIdle = 0f;
        play(Game.KatanaDrainDelay + 1.0f, new PlayerInput());
        Check(held80 && me.KatanaMeter < 70f, "on a streak the meter does not drain; with no streak it does");
        reset();
        var fifth = dummy(from + new V2(400f, 0f), 100f);
        me.KatanaChain = Game.KatanaChainNeed - 1;
        g.Sounds.Clear();
        strikeAt(fifth);
        Check(me.KatanaChain == Game.KatanaChainNeed && heard(Sfx.KatanaChain5) && heard(Sfx.KatanaEmpReady), "the fifth chain chimes and rings in the empowered strike");
        reset();
        var big = dummy(from + new V2(400f, 0f), 230f);
        var mid = dummy(from + new V2(200f, 6f), 110f);
        me.KatanaChain = Game.KatanaChainNeed;
        bool predictEmp = g.KatanaWouldKill(me, big);
        g.Sounds.Clear();
        strikeAt(big);
        Check(predictEmp && heard(Sfx.KatanaEmpStrikeCut) && !heard(Sfx.KatanaStrikeCut), "an empowered strike plays its own cut, and the kill check counts its 240");
        Console.WriteLine("  katana empowered: target dead {0}, route dead {1}, chain {2}, meter {3:0}", big.Hp <= 0f, mid.Hp <= 0f, me.KatanaChain, me.KatanaMeter);
        Check(big.Hp <= 0f && mid.Hp <= 0f, "an empowered strike deals 240 to the target and 120 to everything it crossed");
        Check(me.KatanaChain == 0 && me.KatanaMeter == 100f, "an empowered kill starts the chain again and the meter stays full");

        // Bo Staff (Docs/Mockups/bo-staff-rework.html): slash, jab, spin in that order with no timer; the shove (weapon-action key) starts it over.
        reset();
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.BoStaff);
        me.Hp = me.MaxHp;
        var bNear = dummy(from + new V2(70f, 0f), 1e5f);   // inside every swing's reach, on the aim line
        var bFar = dummy(from + new V2(130f, 0f), 1e5f);   // past the slash and the spin; the jab's line reaches it
        var bSide = dummy(from + new V2(0f, 80f), 1e5f);   // beside you: only the spin goes round to it
        Func<float[]> bo = () => new[] { bNear.MaxHp - bNear.Hp, bFar.MaxHp - bFar.Hp, bSide.MaxHp - bSide.Hp };
        var boHit = new float[3][]; var boOrder = new int[3];
        me.BoStep = 0;
        for (int step = 0; step < 3; step++)
        {
            bNear.Hp = bNear.MaxHp; bFar.Hp = bFar.MaxHp; bSide.Hp = bSide.MaxHp;
            boOrder[step] = me.BoStep;
            volley.Invoke(g, new object[] { me, 0f, 0 });
            play(0.3f, new PlayerInput());
            boHit[step] = bo();
        }
        Console.WriteLine("  bo staff: steps {0},{1},{2}; near {3:0.0}/{4:0.0}/{5:0.0}, far {6:0.0}/{7:0.0}/{8:0.0}, side {9:0.0}/{10:0.0}/{11:0.0}; next step {12}",
            boOrder[0], boOrder[1], boOrder[2], boHit[0][0], boHit[1][0], boHit[2][0], boHit[0][1], boHit[1][1], boHit[2][1], boHit[0][2], boHit[1][2], boHit[2][2], me.BoStep);
        Check(boOrder[0] == 0 && boOrder[1] == 1 && boOrder[2] == 2 && me.BoStep == 0, "the Bo Staff goes slash, jab, spin, then starts over");
        Check(boHit[0][0] > 0f && boHit[0][1] == 0f && boHit[0][2] == 0f, "the slash reaches what's ahead in its cone, not the far one or the one beside you");
        Check(Math.Abs(boHit[1][0] - boHit[0][0] * Game.BoJabMult) < 0.6f && Math.Abs(boHit[1][1] - boHit[1][0]) < 0.6f && boHit[1][2] == 0f, "the jab hits a line straight ahead, near and far, for 22");
        Check(Math.Abs(boHit[2][0] - boHit[0][0] * Game.BoSpinMult) < 0.6f && boHit[2][1] == 0f && Math.Abs(boHit[2][2] - boHit[2][0]) < 0.6f, "the spin goes all the way round for 30");
        me.BoStep = 0; me.FireCd = 0f;
        volley.Invoke(g, new object[] { me, 0f, 0 });
        play(4f, new PlayerInput());
        Check(me.BoStep == 1 && client.Game.Players[1].BoStep == 1, "there is no timer: after a long pause the next click is the jab (and the client sees the step)");
        // Clicks held down carry on through the chain by themselves, with each step's own hold between them.
        me.BoStep = 0; me.FireCd = 0f;
        var holdSteps = new List<int>();
        int boLast = me.BoStep;
        for (int f = 0; f < 60; f++)
        {
            play(1f / 60f, new PlayerInput { Fire = true, AimAt = from + new V2(100f, 0f) });
            if (me.BoStep != boLast) { holdSteps.Add(me.BoStep); boLast = me.BoStep; }
        }
        Console.WriteLine("  bo staff held: steps over 1 s {0}", string.Join(",", holdSteps));
        Check(holdSteps.Count == 3 && holdSteps[0] == 1 && holdSteps[1] == 2 && holdSteps[2] == 0, "holding the button plays slash, jab, spin, then slash again as each step ends");
        // The shove: starts the chain over, once a second; its damage is 6/16 of a slash.
        reset();
        me.BoStep = 2; me.ShoveCd = 0f; me.FireCd = 0f;
        var shoveT = dummy(from + new V2(60f, 0f), 1e5f);
        hostDash.Invoke(g, new object[] { 1, 6, from + new V2(100f, 0f) });
        play(0.3f, new PlayerInput { AimAt = from + new V2(100f, 0f) });
        float shoveDmg = shoveT.MaxHp - shoveT.Hp;
        Console.WriteLine("  bo staff shove: chain step {0}, hit for {1:0.0}", me.BoStep, shoveDmg);
        Check(me.BoStep == 0 && Math.Abs(shoveDmg - boHit[0][0] * Game.BoShoveMult) < 0.6f, "the shove starts the chain over and deals 6 for a 16 slash");
        me.BoStep = 2;
        hostDash.Invoke(g, new object[] { 1, 6, from + new V2(100f, 0f) });
        play(0.3f, new PlayerInput { AimAt = from + new V2(100f, 0f) });
        Check(me.BoStep == 2 && Math.Abs((shoveT.MaxHp - shoveT.Hp) - shoveDmg) < 0.1f, "a second shove inside its cooldown does nothing");
        host.Leave(""); client.Leave("");
    }

    static void HealerWardsTest(float dt)
    {
        // Spirit Ward takes allies' damage and pays some back; Healing Mist sheds dew to pick up; Energy Ward pays back what it soaked.
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

        // Healing Dash: you sprint and leave footprints; a hurt ally standing in them heals, once, however many overlap.
        ally.Hp = ally.MaxHp * 0.3f;
        allyAt = from + new V2(30f, 0f);
        play(1.0f);
        float idle = ally.Hp - ally.MaxHp * 0.3f;  // what the ally regains with no footprints at all
        ally.Hp = ally.MaxHp * 0.3f;
        float a0 = ally.Hp;
        use(AbilityId.HealingDash, from);
        Check(g.Players[1].DashT > 0f, "Healing Dash starts the sprint");
        play(1.0f);
        int prints = g.Zones.Values.Count(z => z.Kind == ZoneKind.Mist);
        bool clientPrints = client.Game.Zones.Values.Any(z => z.Kind == ZoneKind.Mist);
        float dashHealed = ally.Hp - a0;
        Console.WriteLine("  healing dash: footprints {0} (client saw some {1}); a hurt ally near the start healed {2:0.0} in 1 s (idle {3:0.0})", prints, clientPrints, dashHealed, idle);
        Check(prints >= 1 && clientPrints, "Healing Dash leaves footprints that are synced");
        Check(dashHealed - idle > Game.FootprintHeal * 0.5f && dashHealed - idle <= Game.FootprintHeal * g.Players[1].DashHeal + 0.5f, "Healing Dash heals an ally in its footprints, and overlapping ones don't stack");
        g.Zones.Clear(); g.Orbs.Clear();
        allyAt = new V2(500f, 450f);
        play(0.2f);

        // Energy Ward: a shield that soaks damage, paying back half of it as healing when it breaks or runs out.
        use(AbilityId.EnergyWard, from);
        float shieldMe = me.ShieldHp, shieldAlly = ally.ShieldHp;
        play(0.2f);  // a few ticks, so the client has the snapshot
        var cMe = client.Game.Players[1];
        bool wardSynced = me.EnergyMax >= 59f && cMe.EnergyMax >= 59f && cMe.ShieldHp > 0f && cMe.ShieldT > 6f;  // the bubble's look reads these (Game/EnergyWardLook.cs)
        shieldMe = me.ShieldHp; shieldAlly = ally.ShieldHp;
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
        Console.WriteLine("  energy ward: shields {0:0} / {1:0}; you soaked 40 and got back {2:0.0} when it ran out; the ally took 70, net {3:+0.0;-0.0}",
            shieldMe, shieldAlly, meNet, allyNet);
        Check(shieldMe >= 59f && shieldAlly >= 59f, "Energy Ward shields you and your ally");
        Check(wardSynced, "an Energy Ward's full size and timer reach the client, for the bubble to shrink by");
        Check(me.EnergyMax == 0f, "the ward's size is cleared once it has paid out");
        Check(Math.Abs(meNet - 40f * Game.EnergyRepay) < 2f && Math.Abs(allyNet - (-10f + 60f * Game.EnergyRepay)) < 1f, "an Energy Ward pays back half of what it soaked when it runs out or breaks");
        host.Leave(""); client.Leave("");
    }
}
