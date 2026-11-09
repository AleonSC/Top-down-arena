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
        // The Clot Cannon lobs a clot that splashes the same at any range and leaves a pool that congeals; Transfusion Darts give an ally a lot for a little
        // and steal a sliver from enemies; the Rapier's Tempo, skewering and Flourish; the Katana's two stances.
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

        // Clot Cannon: hold to charge, let go to lob a clot over enemies; the charge sets how far it lands, never how hard.
        // It splashes everything where it lands and leaves a big pool that congeals after a second, sticking the enemies
        // in it; standing in your own pool no longer guards you.
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.ClotBolt);
        Check(Game.ChargeWeapon(WeaponId.ClotBolt) && me.Weapon.Name == "Clot Cannon", "the tank Hemomancer's Clot Cannon charges like a bow");
        var tapNear = dummy(from + new V2(Game.ClotMin, 0f), 1e5f);
        var tapFar = dummy(from + new V2(Game.ClotMax, 0f), 1e5f);
        me.BowShotDraw = Game.BowMinDraw;
        volley.Invoke(g, new object[] { me, 0f, 0 });
        me.Hp = me.MaxHp;  // the shot's cost would raise Blood Price: keep both shots at full health so only the range differs
        play(Game.ClotFlight(0f) + 0.1f, new PlayerInput());
        float tapHit = tapNear.MaxHp - tapNear.Hp, tapMiss = tapFar.MaxHp - tapFar.Hp;
        reset();
        var under = dummy(from + new V2(Game.ClotMax * 0.5f, 0f), 1e5f);
        var stuck = dummy(from + new V2(Game.ClotMax, 0f), 1e5f);
        var beside = dummy(from + new V2(Game.ClotMax, Game.ClotSplash + 40f), 1e5f);  // outside the splash, inside the pool
        me.BowShotDraw = g.BowFull(me);
        me.Hp = me.MaxHp;
        volley.Invoke(g, new object[] { me, 0f, 0 });
        float shotCost = me.MaxHp - me.Hp;
        Console.WriteLine("  clot cannon: a shot cost {0:0.0} of {1:0} health", shotCost, me.MaxHp);
        Check(Math.Abs(shotCost - me.MaxHp * Game.ClotCost) < 0.01f, "every Clot Cannon shot costs 6% of your max health");
        me.Hp = me.MaxHp;
        play(Game.ClotFlight(1f) + 0.05f, new PlayerInput());
        float fullHit = stuck.MaxHp - stuck.Hp, underHit = under.MaxHp - under.Hp, besideHit = beside.MaxHp - beside.Hp;
        var pool = g.Zones.Values.FirstOrDefault(z => z.Kind == ZoneKind.BloodPool);
        Console.WriteLine("  clot cannon: tap {0:0.0} at {1} px, {2:0.0} at {3} px; full {4:0.0} at {3} px, under the arc {5:0.0}, beside the splash {6:0.0}; pool {7:0} px",
            tapHit, Game.ClotMin, tapMiss, Game.ClotMax, fullHit, underHit, besideHit, pool != null ? pool.Radius : 0f);
        Check(tapHit > 0f && tapMiss == 0f && fullHit > 0f && underHit == 0f && besideHit == 0f,
            "a tap lobs the clot close, a full charge as far as it goes, over the enemies under its arc");
        Check(Math.Abs(tapHit - fullHit) < 0.01f, "the clot hits just as hard at any range");
        Check(pool != null && Math.Abs(pool.Radius - Game.ClotPoolRadius) < 0.5f && Math.Abs(pool.Pos.X - (from.X + Game.ClotMax)) < 12f,
            "it leaves a big pool where it lands");
        play(0.5f, new PlayerInput());
        bool wetFree = stuck.FrozenT <= 0f && beside.FrozenT <= 0f;
        play(0.6f, new PlayerInput());
        bool setStuck = stuck.FrozenT > 0f && beside.FrozenT > 0f;
        Check(wetFree && setStuck, "the pool congeals after a second, sticking every enemy in it");
        g.DevSetGod(1, false);
        me.Hp = me.MaxHp; me.ShieldHp = 0f; damage.Invoke(g, new object[] { me, 20f, "Test" }); float outside = me.MaxHp - me.Hp;
        if (pool != null) from = pool.Pos;
        play(0.02f, new PlayerInput());
        me.Hp = me.MaxHp; me.ShieldHp = 0f; damage.Invoke(g, new object[] { me, 20f, "Test" }); float inside = me.MaxHp - me.Hp;
        g.DevSetGod(1, true);
        Console.WriteLine("  clot cannon: enemy free while wet {0}, stuck once it congealed {1}; 20 damage taken outside {2:0.0}, in your pool {3:0.0}",
            wetFree, setStuck, outside, inside);
        Check(Math.Abs(inside - outside) < 0.01f && outside > 0f, "standing in your own pool doesn't guard you any more");
        // Standing in the pool heals slowly (against your own healing standing outside it).
        Func<float> gain = () => { me.Hp = me.MaxHp * 0.5f; float h = me.Hp; play(1f, new PlayerInput()); return me.Hp - h; };
        float healIn = pool != null && g.Zones.ContainsKey(pool.Id) ? gain() : 0f;
        V2 inPool = from; from = new V2(300f, 450f);
        float healOut = gain();
        from = inPool;
        Console.WriteLine("  clot cannon: 1 s in the pool healed {0:0.0}, outside {1:0.0}", healIn, healOut);
        Check(healIn - healOut > Kits.Get(ZoneKind.BloodPool).HealRate * 0.8f && healIn - healOut < Kits.Get(ZoneKind.BloodPool).HealRate * 1.3f,
            "the pool slowly heals you while you stand in it");
        from = new V2(300f, 450f);
        reset();
        g.Orbs.Clear();  // the dummies just cleared may drop health orbs where the next test's ally stands

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
        me.Hp = me.MaxHp;  // a Hemomancer's Blood Price adds damage while hurt: start whole, so every thrust is alike
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

        // Katana (Katana.cs; Docs/KatanaCombo.md): two stances. Hayate dash-cuts along a line (a kill brings the dash back and adds
        // Flow); Senbon swipes and marks. Each fills its own meter; holding fire with it full spends it on the Kage Chain or Zanshin.
        // The weapon-action key switches stance with a small push.
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.Katana);
        me.Hp = me.MaxHp;  // (Blood Price would change the damage as health moves)
        var hostDash = typeof(Game).GetMethod("HostWeaponDash", flags);
        float kw = me.Weapon.Damage * me.Stats.DmgMult;
        Func<Sfx, bool> heard = x => g.Sounds.Any(c => c.Sfx == x);
        Func<Enemy, float> took = e => e.MaxHp - e.Hp;
        me.KatanaStance = 0; me.KatanaFlow = 0; me.KatanaKage = 0; me.KatanaZan = 0; me.FireCd = 0f;
        var onLine1 = dummy(from + new V2(60f, 0f), 1e5f);
        var onLine2 = dummy(from + new V2(140f, 0f), 1e5f);
        var offLine = dummy(from + new V2(70f, 90f), 1e5f);
        byte dashes0 = me.KatanaDashes;
        g.Sounds.Clear();
        volley.Invoke(g, new object[] { me, 0f, 0 });
        float dashCd = me.FireCd;
        play(0.2f, new PlayerInput());
        Console.WriteLine("  katana hayate: on the line {0:0.0} and {1:0.0}, off it {2:0.0}; Kage {3}, dashes {4} -> {5}, wait {6:0.00}",
            took(onLine1), took(onLine2), took(offLine), me.KatanaKage, dashes0, me.KatanaDashes, dashCd);
        Check(Math.Abs(took(onLine1) - kw * Game.KatanaDashMult) < 1f && Math.Abs(took(onLine2) - took(onLine1)) < 0.5f && took(offLine) == 0f,
            "Hayate's dash-cut cuts everything along its line (and nothing off it)");
        Check(me.KatanaDashes == (byte)(dashes0 + 1) && me.KatanaKage == 2 && client.Game.Players[1].KatanaKage == 2, "every enemy a dash hits fills the Kage Chain's meter (every screen sees it)");
        Check(dashCd > 0.3f && heard(Sfx.KatanaLunge) && heard(Sfx.KatanaHit), "a dash that kills nothing waits for its cooldown; it plays the dash and the cut");
        reset();

        // A dash that kills comes back at once and adds a level of Flow; Flow lengthens and strengthens the dash, and fades in Hayate.
        var weak = dummy(from + new V2(80f, 0f), 5f);
        me.KatanaFlow = 0; me.FireCd = 0f;
        volley.Invoke(g, new object[] { me, 0f, 0 });
        play(0.15f, new PlayerInput());
        Check(weak.Hp <= 0f && me.FireCd <= 0f && me.KatanaFlow == 1, "a dash that kills comes back at once and adds a level of Flow");
        reset();
        var flowT = dummy(from + new V2(60f, 0f), 1e5f);
        me.KatanaFlow = 5; me.FireCd = 0f;
        float reach5 = Game.KatanaDashReach(me);
        volley.Invoke(g, new object[] { me, 0f, 0 });
        play(0.15f, new PlayerInput());
        Check(Math.Abs(took(flowT) - kw * (Game.KatanaDashMult + 5 * Game.KatanaFlowDmg)) < 1f && Math.Abs(reach5 - Game.MeleeReach(me, Game.KatanaDashLen) * (1f + 5 * Game.KatanaFlowLen)) < 0.5f,
            "each level of Flow adds damage and length to the dash");
        me.KatanaFlow = 3; me.KatanaFlowT = 0f;
        play(Game.KatanaFlowKeep + 0.1f, new PlayerInput());
        int afterKeep = me.KatanaFlow;
        me.KatanaStance = 1; me.KatanaFlow = 3; me.KatanaFlowT = 0f;
        play(Game.KatanaFlowKeep + 1f, new PlayerInput());
        Check(afterKeep == 2 && me.KatanaFlow == 3, "Flow fades in Hayate after a while without a kill, and holds while you're in Senbon");
        reset();

        // +1: each dash strikes once more at 60% (on the same line).
        var plus = dummy(from + new V2(60f, 0f), 1e5f);
        me.KatanaStance = 0; me.KatanaFlow = 0; me.FireCd = 0f;
        volley.Invoke(g, new object[] { me, 0f, 1 });
        play(0.25f, new PlayerInput());
        Check(Math.Abs(took(plus) - kw * Game.KatanaDashMult * (1f + Game.KatanaExtraMult)) < 1f, "+1 makes each dash strike once more at 60%");
        reset();

        // Senbon: swipes in an arc; every enemy hit fills Zanshin's meter and takes a mark (up to 3) that every screen sees.
        me.KatanaStance = 1; me.KatanaZan = 0; me.FireCd = 0f;
        var front = dummy(from + new V2(70f, 0f), 1e5f);
        var behind = dummy(from + new V2(-70f, 0f), 1e5f);
        g.Sounds.Clear();
        volley.Invoke(g, new object[] { me, 0f, 0 });
        play(0.2f, new PlayerInput());
        float swipe = took(front);
        int marks1 = front.KatanaMarks;
        var seenFront = client.Game.Enemies.ContainsKey(front.Id) ? client.Game.Enemies[front.Id] : null;
        Console.WriteLine("  katana senbon: swipe {0:0.0}, behind {1:0.0}; marks {2} (the client sees {3}); Zan {4}", swipe, took(behind), marks1, seenFront != null ? seenFront.KatanaMarks : -1, me.KatanaZan);
        Check(Math.Abs(swipe - kw * Game.KatanaSwipeMult) < 1f && took(behind) == 0f && heard(Sfx.KatanaSwipe1), "Senbon's swipe cuts in front of you");
        Check(marks1 == 1 && seenFront != null && seenFront.KatanaMarks == 1 && me.KatanaZan == 1, "a swipe marks what it hits and fills Zanshin's meter (every screen sees the mark)");
        for (int k = 0; k < 4; k++) { volley.Invoke(g, new object[] { me, 0f, 0 }); play(0.2f, new PlayerInput()); }
        Check(front.KatanaMarks == Game.KatanaMarkMax && me.KatanaZan == 5, "marks stop at 3 an enemy; the meter keeps counting hits");
        reset();

        // The switch: the weapon-action key turns the stance over and pushes nearby enemies back a little; it has a short cooldown.
        me.KatanaStance = 0; me.KatanaSwitchCd = 0f; me.KatanaKage = 4; me.KatanaZan = 7;
        var pushed = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, from + new V2(60f, 0f), 0f, 0, false });
        pushed.SpawnT = 0f; pushed.Hp = pushed.MaxHp = 1e5f; pushed.FireT = 99f; keep.Add(pushed);
        V2 pushFrom = pushed.Pos;
        g.Sounds.Clear();
        hostDash.Invoke(g, new object[] { 1, 5, from });
        play(0.1f, new PlayerInput());
        bool switchFx = client.Game.Effects.Any(fx => fx.Kind == FxKind.KatanaStanceFx);
        play(0.3f, new PlayerInput());
        float moved = pushed.Pos.DistanceTo(pushFrom);
        bool switched = me.KatanaStance == 1 && client.Game.Players[1].KatanaStance == 1;
        hostDash.Invoke(g, new object[] { 1, 5, from });
        bool blocked = me.KatanaStance == 1;
        play(Game.KatanaSwitchCd, new PlayerInput());
        hostDash.Invoke(g, new object[] { 1, 5, from });
        Console.WriteLine("  katana switch: stance {0}, pushed {1:0} px, again at once {2}, after the cooldown {3}; meters {4}/{5}", switched, moved, blocked, me.KatanaStance, me.KatanaKage, me.KatanaZan);
        Console.WriteLine("  katana switch seen: sound {0}, client fx {1}", heard(Sfx.KatanaStanceSwap), switchFx);
        Check(switched && heard(Sfx.KatanaStanceSwap) && switchFx, "the weapon-action key switches stance (everyone sees and hears it)");
        Check(moved > 20f && moved < 80f && took(pushed) == 0f, "the switch pushes nearby enemies back a little and hurts nothing");
        Check(blocked && me.KatanaStance == 0, "the switch has a short cooldown");
        Check(me.KatanaKage == 4 && me.KatanaZan == 7, "both meters are kept when you switch");
        reset();

        // The Kage Chain (Hayate, Kage meter full, hold fire): hops through the nearest enemies, cutting each; +1 adds a hop.
        me.KatanaStance = 0; me.KatanaKage = (byte)Game.KatanaKageNeed;
        var chain = new List<Enemy>();
        for (int k = 0; k < 8; k++) chain.Add(dummy(from + V2.FromAngle(k * 0.785f) * (90f + 15f * k), 1e5f));
        g.Sounds.Clear();
        hostDash.Invoke(g, new object[] { 1, 17, from });
        float inv = me.InvulnT;
        play(Game.KatanaKageHops * Game.KatanaKageHop + 0.3f, new PlayerInput());
        int cutK = chain.Count(e => Math.Abs(took(e) - kw * Game.KatanaKageMult) < 1f);
        Console.WriteLine("  katana kage chain: {0} of 8 cut for {1:0} each; meter {2}; untouchable {3:0.00} s", cutK, kw * Game.KatanaKageMult, me.KatanaKage, inv);
        Check(cutK == Game.KatanaKageHops && me.KatanaKage == 0 && inv > 0.4f && heard(Sfx.KatanaChain1), "a full Kage meter spends on a chain through the 6 nearest enemies, untouchable");
        Check(chain.Take(6).All(e => took(e) > 0f) && took(chain[7]) == 0f, "the chain hops to the nearest enemy each time");
        var kageMax = typeof(Game).GetMethod("KatanaKageMax", flags);
        me.Stats.ExtraShots++;
        int hops1 = (int)kageMax.Invoke(g, new object[] { me });
        me.Stats.ExtraShots--;
        Check(hops1 == Game.KatanaKageHops + 1, "+1 adds a hop to the Kage Chain");
        me.KatanaKage = 3;
        hostDash.Invoke(g, new object[] { 1, 17, from });
        Check(me.KatanaKage == 3, "the chain needs a full meter");
        reset();

        // Zanshin (Senbon, Zan meter full, hold fire): the marked freeze, and as you sheathe each takes a cut per mark; shots near you fall.
        me.KatanaStance = 1; me.KatanaZan = (byte)Game.KatanaZanNeed;
        var m1 = dummy(from + new V2(90f, 0f), 1e5f); m1.KatanaMarks = 1;
        var m3 = dummy(from + new V2(0f, 120f), 1e5f); m3.KatanaMarks = 3;
        var plain = dummy(from + new V2(-90f, 0f), 1e5f);
        var near = (Bullet)shoot.Invoke(g, new object[] { Defs.SmallShot, from + new V2(150f, -150f), new V2(0f, 0f), 0f, 9f, 5f, 0, false, 0f });
        g.Sounds.Clear();
        hostDash.Invoke(g, new object[] { 1, 18, from });
        play(Game.KatanaZanTotal(2) * 0.5f, new PlayerInput());
        bool frozenMid = m1.FrozenT > 0f && took(m1) == 0f;
        play(Game.KatanaZanTotal(2) * 0.5f + 0.2f, new PlayerInput());
        Console.WriteLine("  katana zanshin: frozen midway {0}; one mark {1:0.0}, three {2:0.0}, unmarked {3:0.0}; marks left {4}/{5}; meter {6}; shot cut {7}",
            frozenMid, took(m1), took(m3), took(plain), m1.KatanaMarks, m3.KatanaMarks, me.KatanaZan, !g.Bullets.ContainsKey(near.Id));
        Check(frozenMid, "Zanshin holds the marked still and the cuts wait for the sheathe");
        Check(Math.Abs(took(m1) - kw * Game.KatanaZanMult) < 1f && Math.Abs(took(m3) - 3f * kw * Game.KatanaZanMult) < 1f && took(plain) == 0f,
            "as you sheathe, every marked enemy takes a cut per mark (unmarked ones nothing)");
        Check(m1.KatanaMarks == 0 && m3.KatanaMarks == 0 && me.KatanaZan == 0 && !g.Bullets.ContainsKey(near.Id) && heard(Sfx.KatanaSheathe),
            "Zanshin spends the marks and the meter, and cuts the shots near you");
        reset();

        // Holding fire with the meter full: the press still attacks once, then the hold spends the meter (your own game asks).
        me.KatanaStance = 0; me.KatanaKage = (byte)Game.KatanaKageNeed; me.FireCd = 0f;
        for (int k = 0; k < 3; k++) dummy(from + new V2(200f, -80f + 80f * k), 1e5f);
        byte d0 = me.KatanaDashes;
        play(Game.KatanaHoldFire + 0.15f, new PlayerInput { Fire = true, AimAt = from + new V2(-100f, 0f) });
        Check(me.KatanaDashes == (byte)(d0 + 1) && me.KatanaKage == 0, "with the meter full, a held fire dashes once, then spends the meter on the chain");
        play(0.6f, new PlayerInput());
        reset();

        // Raising the parry sends Senbon's swipes back to the first and clears the wait; the meters and Flow are kept.
        me.KatanaStance = 1; me.KatanaStep = 1; me.FireCd = 0.2f; me.KatanaZan = 6; me.KatanaFlow = 2;
        play(0.05f, new PlayerInput { Parry = true });
        Check(me.KatanaStep == 0 && me.FireCd <= 0f && me.KatanaZan == 6 && me.KatanaFlow == 2, "raising the parry cancels the Katana's wait and keeps its meters and Flow");
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
