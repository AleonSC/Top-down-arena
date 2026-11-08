using System.Linq;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using Arena;

// The Ninja class.
static partial class Program
{
    // The Ninja: tables, speed and health, weapons (pull, returning stars, Kunai bonus), and its abilities with a
    // host Tank Ninja and a joining Healer Ninja.
    static void NinjaTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
        var cast = typeof(Game).GetMethod("HostAbility", flags);
        var damage = typeof(Game).GetMethod("Damage", flags);
        var hitMult = typeof(Game).GetMethod("NinjaHitMult", flags);
        var aimAt = typeof(Game).GetMethod("NinjaTarget", flags);

        Check(Defs.Weapon(Role.Tank, Kit.Ninja, 0).Name == "Kusarigama" && Defs.Weapon(Role.Healer, Kit.Ninja, 1).Name == "Spirit Fan" && Defs.Weapon(Role.Dps, Kit.Ninja, 2).Name == "Kunai"
            && Defs.Weapon(Role.Dps, Kit.Knight, 2).Name == "Rapier" && Defs.Weapon(Role.Healer, Kit.Soldier, 1).Name == "Revolver" && Defs.Weapon(Role.Tank, Kit.Knight, 1).Name == "Lance",
            "Ninja weapons sit in the role/class tables without shifting the others");
        Check(Upgrades.RoleAbility(Role.Healer, Kit.Ninja, 1) == AbilityId.Substitution && Upgrades.RoleAbility(Role.Dps, Kit.Knight, 0) == AbilityId.Whirlwind
            && Upgrades.Ultimate(Role.Dps, Kit.Ninja) == AbilityId.DeathLotus && Upgrades.Ultimate(Role.Healer, Kit.Knight) == AbilityId.HallowedBlade
            && Upgrades.Ultimate(Role.Tank, Kit.Ninja) == AbilityId.ThousandClones, "Ninja abilities and ultimates map to the right role and class");

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Tank", Role.Tank, 0, 0, Kit.Ninja);
        var client = JoinAs(port, false, "Healer", Role.Healer, 0, 0, Kit.Ninja);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var hp = g.Players[1];
        var cp = g.Players[2];
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        Check(hp.Kit == Kit.Ninja && Math.Abs(hp.Speed - Defs.Get(Role.Tank).Speed * 1.12f) < 0.5f && Math.Abs(hp.MaxHp - Defs.Get(Role.Tank).Hp * 0.85f) < 0.5f,
            "Ninjas are 12% faster with 85% health");

        var keep = new HashSet<Enemy>();
        Action clear = () => { foreach (var e in g.Enemies.Values) if (!keep.Contains(e)) e.Hp = 0f; };
        Func<V2, Enemy> dummy = at => { var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false }); e.Hp = e.MaxHp = 1e6f; keep.Add(e); return e; };
        Action<float> run = secs => Run(s, dt, secs, null, () => { clear(); foreach (var e in keep) e.FrozenT = 5f; });
        Action<int, AbilityId, V2> use = (id, ability, at) => { g.ShowcaseSetAbility(ability); g.Players[id].Slots[0].Charges = 1; cast.Invoke(g, new object[] { id, 0, at, true }); };
        Action<int, float> place = (id, x) => { var q = (id == 1 ? host : client).Game.Me; q.Pos = q.Disp = new V2(x, 450f); };
        clear(); run(0.3f);

        // Kusarigama: the throw hooks the first enemy in its path; the weapon-action key reels you in to it for a heavy, stunning slash.
        place(1, 400f); run(0.2f);
        var front = dummy(new V2(600f, 450f));
        var behind = dummy(new V2(650f, 450f));
        V2 frontAt = front.Pos, behindAt = behind.Pos;
        Action pinBoth = () => { front.Pos = frontAt; behind.Pos = behindAt; front.FireT = behind.FireT = 99f; };
        float hookedAt = -1f, hitOnHook = 0f, behindOnHook = -1f, maxStun = 0f;
        bool clientSawHook = false;
        for (int f = 0; f < 70; f++)
        {
            host.Poll(); g.FixedStep(dt, new PlayerInput { Fire = true, WeaponAction = f % 10 < 5, AimAt = frontAt }); g.Update(dt);
            client.Poll(); client.Game.FixedStep(dt, new PlayerInput()); client.Game.Update(dt);
            clear(); pinBoth();
            if (hookedAt < 0f && hp.HookId == front.Id) { hookedAt = f / 60f; hitOnHook = front.MaxHp - front.Hp; behindOnHook = behind.MaxHp - behind.Hp; }
            clientSawHook |= client.Game.Players[1].HookId == front.Id;
            maxStun = Math.Max(maxStun, front.FrozenT);
        }
        float reeled = hp.Pos.DistanceTo(frontAt), total = front.MaxHp - front.Hp;
        Console.WriteLine("  kusarigama: hooked after {0:0.00} s ({1:0.0}), behind it {2:0.0}; reeled in to {3:0} px of it; slash brought it to {4:0.0}; stunned {5:0.00} s; client saw the hook {6}",
            hookedAt, hitOnHook, behindOnHook, reeled, total, maxStun, clientSawHook);
        Check(hookedAt >= 0f && hitOnHook > 0f && behindOnHook == 0f && clientSawHook, "the Kusarigama's throw hooks the first enemy in its path (not the one behind it), and everyone sees the chain");
        Check(reeled < front.Radius + hp.Def.BodyR + 20f && total >= hitOnHook * 2.5f && maxStun > 0.3f, "the weapon-action key while hooked reels you in to it and lands a heavy, stunning slash");
        front.Hp = behind.Hp = 0f; keep.Remove(front); keep.Remove(behind);
        var d1 = dummy(new V2(770f, 450f));

        // Bo Staff: the weapon-action key shoves a bar of staff out ahead of you: what's in it is pushed straight ahead 70 px for a little
        // damage, what's outside it isn't touched, and it cools down for a second. Standing still or moving makes no difference.
        hp.Weapon = Defs.Weapon(Role.Tank, Kit.Ninja, 1);
        place(1, 300f); run(0.3f);
        d1.Pos = new V2(360f, 450f);
        var aside = dummy(new V2(360f, 600f));
        float d1hp = d1.Hp, d1x0 = d1.Pos.X, asideY0 = aside.Pos.Y;
        Action<int, bool, V2> shoveFor = (frames, key, mv) =>
        {
            for (int f = 0; f < frames; f++)
            {
                host.Poll(); g.FixedStep(dt, new PlayerInput { WeaponAction = key, Move = mv, AimAt = hp.Pos + new V2(100f, 0f) }); g.Update(dt);
                client.Poll(); client.Game.FixedStep(dt, new PlayerInput()); client.Game.Update(dt);
                clear(); d1.FireT = aside.FireT = 99f; d1.FrozenT = aside.FrozenT = 5f;
            }
        };
        hp.BoStep = 2;
        shoveFor(30, true, V2.Zero);
        float shoveHit = d1hp - d1.Hp, pushed = d1.Pos.X - d1x0;
        float expect = hp.Weapon.Damage * Game.BoShoveMult * hp.Stats.DmgMult;
        Console.WriteLine("  bo staff shove: hit for {0:0.0} (expected {1:0.0}), pushed {2:0} px; the one off to the side moved {3:0} and took {4:0.0}; chain step now {5}",
            shoveHit, expect, pushed, Math.Abs(aside.Pos.Y - asideY0), aside.MaxHp - aside.Hp, hp.BoStep);
        Check(Math.Abs(shoveHit - expect) < 0.6f && Math.Abs(pushed - Game.BoShoveKnock) < 12f, "the weapon-action key shoves what's in front of the Bo Staff back 70 px for a little damage");
        Check(aside.Hp == aside.MaxHp && Math.Abs(aside.Pos.Y - asideY0) < 2f, "an enemy outside the bar's width is left alone");
        Check(hp.BoStep == 0, "the shove starts the slash, jab, spin chain over");
        d1.Pos = new V2(360f, 450f); d1hp = d1.Hp; hp.BoStep = 2;
        shoveFor(1, false, V2.Zero);
        shoveFor(20, true, new V2(0f, 1f));  // pressed again 0.3 s later, walking: the cooldown holds it back
        Check(d1.Hp == d1hp && hp.BoStep == 2, "the shove cools down for a second");
        shoveFor(1, false, V2.Zero);
        shoveFor(60, false, V2.Zero);
        d1.Pos = new V2(360f, 450f); hp.Pos = hp.Disp = new V2(300f, 450f);
        shoveFor(1, false, V2.Zero);
        shoveFor(30, true, V2.Zero);
        Check(d1.Hp < d1hp && hp.BoStep == 0, "and works again after that");
        // The same by a joined player: their game sends the press and the host lands the shove.
        var cme = client.Game.Me;
        cp.Weapon = cme.Weapon = Defs.Weapon(Role.Tank, Kit.Ninja, 1);
        place(2, 900f); run(0.3f);
        aside.Pos = new V2(960f, 450f);
        float asideHp = aside.Hp, ax0 = aside.Pos.X;
        cp.BoStep = 2;
        for (int f = 0; f < 15; f++)
        {
            host.Poll(); g.FixedStep(dt, new PlayerInput()); g.Update(dt);
            client.Poll(); client.Game.FixedStep(dt, new PlayerInput { WeaponAction = true, AimAt = cme.Pos + new V2(100f, 0f) }); client.Game.Update(dt);
            Thread.Sleep(1);
            clear(); aside.FireT = 99f; aside.FrozenT = 5f;
        }
        for (int f = 0; f < 25; f++) { Thread.Sleep(2); host.Poll(); g.FixedStep(dt, new PlayerInput()); client.Poll(); clear(); aside.FireT = 99f; aside.FrozenT = 5f; }
        Console.WriteLine("  bo staff (joined player): hit for {0:0.0}, pushed {1:0} px, chain step {2} (host sees them at {3:0},{4:0}, weapon {5}, cooldown {6:0.00})", asideHp - aside.Hp, aside.Pos.X - ax0, cp.BoStep, cp.Pos.X, cp.Pos.Y, cp.Weapon.Id, cp.ShoveCd);
        Check(asideHp - aside.Hp > 0f && aside.Pos.X - ax0 > 30f && cp.BoStep == 0, "a joined player's shove reaches the host, which lands it and starts their chain over");
        cp.Weapon = cme.Weapon = Defs.Weapon(Role.Healer, Kit.Ninja, 0);
        aside.Hp = 0f; keep.Remove(aside);
        hp.Weapon = Defs.Weapon(Role.Tank, Kit.Ninja, 0);
        d1.Pos = new V2(770f, 450f);

        // Kunai: +50% on enemies that aren't after you; Vanish's ambush doubles the next hit, once.
        d1.TargetId = 2;
        float off = (float)hitMult.Invoke(g, new object[] { 1, Defs.KunaiShot, d1 });
        d1.TargetId = 1;
        float on = (float)hitMult.Invoke(g, new object[] { 1, Defs.KunaiShot, d1 });
        Check(Math.Abs(off - 1.5f) < 0.01f && Math.Abs(on - 1f) < 0.01f, "Kunai deal 50% more to enemies that aren't targeting you");

        // Fuma Shuriken: a giant star thrown in an oval loop out in front and back, the same on the host and the client.
        clear();
        hp.Weapon = Defs.Weapon(Role.Tank, Kit.Ninja, 2);
        place(1, 300f); run(0.1f);
        V2 throwAt = hp.Pos;
        typeof(Game).GetMethod("FireVolley", flags).Invoke(g, new object[] { hp, 0f, 0 });
        var star = g.Bullets.Values.First(b => b.Owner == 1 && b.Kind == Defs.FumaStar);
        float lap = star.Life0, ahead = 0f, wide = 0f, drift = 0f, lastGap = 0f;
        for (int f = 0; f < lap * 60f - 2f && g.Bullets.ContainsKey(star.Id); f++)
        {
            hp.Pos = throwAt;
            host.Poll(); g.FixedStep(dt, new PlayerInput()); g.Update(dt);
            client.Poll(); client.Game.FixedStep(dt, new PlayerInput()); client.Game.Update(dt);
            clear();
            ahead = Math.Max(ahead, star.Pos.X - throwAt.X);
            wide = Math.Max(wide, Math.Abs(star.Pos.Y - throwAt.Y));
            Bullet cs;
            if (client.Game.Bullets.TryGetValue(star.Id, out cs)) drift = Math.Max(drift, cs.Pos.DistanceTo(star.Pos));
            lastGap = star.Pos.DistanceTo(throwAt);
        }
        Console.WriteLine("  fuma shuriken: radius {0}, {1:0} px ahead, {2:0} px to the side, ends {3:0} px from where it was thrown; client drift {4:0.0} px",
            star.Radius, ahead, wide, lastGap, drift);
        Check(hp.Weapon.Name == "Fuma Shuriken" && star.Radius >= 20f && Math.Abs(ahead - Game.FumaReach) < 15f && Math.Abs(wide - Game.FumaWidth) < 10f && lastGap < 40f,
            "the Fuma Shuriken is a giant star that loops an oval out in front of you and comes back");
        Check(drift < 25f, "the Fuma Shuriken follows the same loop on the client");
        hp.Weapon = Defs.Weapon(Role.Tank, Kit.Ninja, 0);

        // Iron Body: 80% less damage for 2 s, then a shockwave.
        place(1, 650f); run(0.2f);
        use(1, AbilityId.IronBody, hp.Pos);
        hp.DevGod = false; float hp0 = hp.Hp = hp.MaxHp;
        damage.Invoke(g, new object[] { hp, 100f / g.EnemyDamageScale(g.Wave), "test" });
        float taken = hp0 - hp.Hp;
        hp.Hp = hp.MaxHp; hp.DevGod = true;
        float dhp = d1.Hp;
        run(2.2f);
        Console.WriteLine("  iron body: took {0:0} of 100; shockwave dealt {1:0}", taken, dhp - d1.Hp);
        Check(Math.Abs(taken - 20f) < 1f && d1.Hp < dhp, "Iron Body takes 80% less damage, then ends in a shockwave");

        // Smoke Bomb: enemy bullets inside crawl at half speed (host and client); allies inside take 30% less.
        place(1, 400f); run(0.2f);
        use(1, AbilityId.SmokeBomb, hp.Pos);
        run(0.2f);
        var slow = (Bullet)shoot.Invoke(g, new object[] { Defs.SmallShot, new V2(300f, 380f), new V2(200f, 0f), 0f, 3f, 10f, 0, false, 0f });  // inside the smoke, missing you
        V2 start = slow.Pos;
        run(0.5f);
        Bullet cslow;
        float moved = slow.Pos.X - start.X, cmoved = client.Game.Bullets.TryGetValue(slow.Id, out cslow) ? cslow.Pos.X - start.X : -1f;
        hp.DevGod = false; hp.Hp = hp.MaxHp;
        damage.Invoke(g, new object[] { hp, 100f / g.EnemyDamageScale(g.Wave), "test" });
        float smokeTaken = hp.MaxHp - hp.Hp;
        hp.Hp = hp.MaxHp; hp.DevGod = true;
        Console.WriteLine("  smoke: a 200 px/s bullet moved {0:0} px in 0.5 s (client {1:0}); took {2:0} of 100 inside", moved, cmoved, smokeTaken);
        Check(moved > 40f && moved < 60f && Math.Abs(cmoved - moved) < 15f && Math.Abs(smokeTaken - 70f) < 1f,
            "Smoke Bomb slows enemy bullets inside to half speed on every machine, and allies inside take 30% less");
        g.Zones.Clear();

        // Shadow Clone: a wall of nine that enemies aim at, that eats bullets, that players shove, and that ends with no burst.
        place(1, 650f); run(0.2f);
        use(1, AbilityId.ShadowClone, hp.Pos);
        run(0.1f);
        var wall = g.Minions.Values.Where(m => m.Kind == MinionKind.CloneWall).ToList();
        V2 aimed = wall.Count > 0 ? (V2)aimAt.Invoke(g, new object[] { d1, hp, hp.Pos }) : new V2(-1f, -1f);
        Check(wall.Count == Game.CloneWallCount && wall.Any(m => aimed.DistanceTo(m.Pos) < 1f)
            && client.Game.Minions.Values.Count(m => m.Kind == MinionKind.CloneWall) == Game.CloneWallCount, "Shadow Clone raises a wall of 9 clones that enemies aim at, on every machine");
        var w0 = wall.OrderBy(m => m.Pos.DistanceSq(hp.Pos)).First();
        float hpBefore = hp.Hp;
        var shotAt = typeof(Game).GetMethod("SpawnBullet", flags);
        V2 fromSide = (w0.Pos - hp.Pos).Normalized();
        shotAt.Invoke(g, new object[] { (byte)10, w0.Pos + fromSide * 70f, fromSide * -300f, 0f, 1f, 10f, 0, false, 0f });
        run(0.4f);
        float wallHp = wall.Sum(m => m.BlockHp);
        Console.WriteLine("  wall: health left {0:0} of {1:0}, player took {2:0}", wallHp, wall.Sum(m => m.MaxHp), hpBefore - hp.Hp);
        Check(wallHp < wall.Count * 50f && hp.Hp >= hpBefore, "a wall clone soaks an enemy bullet");
        V2 w0Was = w0.Pos;
        hp.Pos = hp.Disp = w0.Pos - (w0.Pos - hp.Pos).Normalized() * 8f;
        run(0.3f);
        Check(w0.Pos.DistanceTo(w0Was) > 4f, "walking into a wall clone shoves it");
        dhp = d1.Hp;
        run(7.5f);
        Check(!g.Minions.Values.Any(m => m.Kind == MinionKind.CloneWall) && d1.Hp >= dhp, "the wall fades after 7 s and does no damage");

        // Shadow Step: the host sends the smoke look's effect with where they stood, where they landed and who and what it struck.
        place(1, 300f); run(0.2f);
        var stepTarget = dummy(new V2(500f, 450f));
        stepTarget.Pos = new V2(500f, 450f);
        V2 landed = new V2(500f + stepTarget.Radius + hp.Def.BodyR + 8f, 450f);
        hp.Pos = landed;  // (your own game moves you to the landing spot before the cast)
        g.Effects.Clear();
        use(1, AbilityId.ShadowStep, landed);
        var stepFx = g.Effects.FirstOrDefault(e => e.Kind == FxKind.ShadowStep);
        Check(stepFx != null && stepFx.Pos2.DistanceTo(landed) < 1f && Math.Abs(stepFx.Radius - (1000f + stepTarget.Radius)) < 0.01f && Math.Abs(stepFx.Dur - Game.ShadowStepFxTime) < 0.001f,
            "Shadow Step sends its look: landing spot, the caster's id and the struck enemy's radius");
        run(0.1f);
        Check(client.Game.Effects.Any(e => e.Kind == FxKind.ShadowStep && e.Pos2.DistanceTo(landed) < 1f), "the other player's screen gets the Shadow Step look too");
        place(1, 400f); run(0.2f);

        // Vanish: enemies lose you and the next hit is an ambush (double damage, once).
        use(1, AbilityId.Vanish, hp.Pos);
        run(3.5f / 3f);
        bool lost = g.Enemies.Values.Where(e => !keep.Contains(e)).All(e => e.TargetId != 1);
        float ambush = (float)hitMult.Invoke(g, new object[] { 1, Defs.KnightSlash, d1 });
        float again = (float)hitMult.Invoke(g, new object[] { 1, Defs.KnightSlash, d1 });
        Check(lost && hp.VanishT > 0f && client.Game.Players[1].VanishT > 0f && Math.Abs(ambush - 2f) < 0.01f && Math.Abs(again - 1f) < 0.01f,
            "Vanish hides you (on every screen) and your next hit deals double, once");
        hp.VanishT = 0f;

        // Substitution: the two players swap places (each on their own machine) and the ally is shielded.
        place(1, 500f); place(2, 1000f); run(0.4f);
        use(1, AbilityId.Substitution, new V2(1000f, 450f));
        run(0.4f);
        V2 hostAt = host.Game.Me.Pos, clientAt = client.Game.Me.Pos;
        Console.WriteLine("  substitution: host {0:0},{1:0} client {2:0},{3:0}; ally shield {4:0}", hostAt.X, hostAt.Y, clientAt.X, clientAt.Y, cp.ShieldHp);
        Check(Math.Abs(hostAt.X - 1000f) < 10f && Math.Abs(clientAt.X - 500f) < 10f && cp.ShieldHp > 50f, "Substitution swaps you with your ally and shields them");

        // Healing Dash's footprints heal allies who stand in them; Blossom Storm heals everyone and clears bullets near them.
        place(1, 700f); place(2, 725f); run(0.3f);
        cp.Hp = cp.MaxHp * 0.3f;
        float low = cp.Hp;
        use(1, AbilityId.HealingDash, hp.Pos);
        run(1f);
        Check(cp.Hp > low + 6f, "Healing Dash footprints heal allies");
        g.Zones.Clear();
        cp.Hp = cp.MaxHp * 0.3f; low = cp.Hp;
        cp.Slots[Upgrades.UltSlot].Charges = 1;
        cast.Invoke(g, new object[] { 2, Upgrades.UltSlot, cp.Pos, true });
        var near = (Bullet)shoot.Invoke(g, new object[] { Defs.SmallShot, cp.Pos + new V2(60f, 0f), new V2(0f, 0f), 0f, 5f, 10f, 0, false, 0f });
        run(0.5f);
        Check(cp.Hp > low + 6f && !g.Bullets.ContainsKey(near.Id), "Blossom Storm heals the party and erases bullets near them");

        // Blade Fan throws 12 kunai; Death Lotus strikes up to 8 enemies near the cursor.
        g.Bullets.Clear();
        use(1, AbilityId.BladeFan, hp.Pos);
        int orbiting = hp.OrbitN, thrownAtCast = g.Bullets.Values.Count(b => b.Kind == Defs.KunaiShot && b.Owner == 1);
        run(0.3f);
        var spin = dummy(hp.Pos + new V2(60f, 0f));  // an enemy on the circle gets cut as a kunai passes (at most once per 0.5 s)
        var stray = (Bullet)shoot.Invoke(g, new object[] { Defs.SmallShot, hp.Pos + new V2(0f, 60f), new V2(0f, 0f), 0f, 5f, 10f, 0, false, 0f });
        run(0.7f);
        bool cut = spin.Hp < spin.MaxHp;
        bool erased = !g.Bullets.ContainsKey(stray.Id);
        keep.Remove(spin);
        spin.Hp = 0f;
        float tLeft = hp.OrbitT;
        use(1, AbilityId.BladeFan, hp.Pos);  // pressed again: they fly out, and the cooldown is not spent again
        int kunai = g.Bullets.Values.Count(b => b.Kind == Defs.KunaiShot && b.Owner == 1);
        Check(orbiting == 12 && thrownAtCast == 0 && cut && erased && hp.OrbitT == 0f && hp.OrbitN == 0 && kunai == 12 && tLeft > 1f,
            "Blade Fan: 12 kunai orbit (none thrown), cut a touching enemy, erase bullets, and fly out as 12 on the second press");
        g.Bullets.Clear();
        var marks = new List<Enemy>();
        for (int i = 0; i < 10; i++) marks.Add(dummy(new V2(1100f + (i % 5) * 40f, 300f + (i / 5) * 60f)));
        run(0.2f);
        var hpsBefore = marks.Select(e => e.Hp).ToArray();
        hp.Slots[Upgrades.UltSlot].Charges = 1;
        g.ShowcaseSetAbility(AbilityId.DeathLotus);  // on Space for the test
        hp.Slots[0].Charges = 1;
        g.Effects.Clear();
        cast.Invoke(g, new object[] { 1, 0, new V2(1180f, 330f), true });
        var lotusFx = g.Effects.Where(e => e.Kind == FxKind.DeathLotus).ToList();
        Check(lotusFx.Count(e => e.Pos2.X < 0f) == 1 && lotusFx.Count(e => e.Pos2.X >= 0f) == 8, "Death Lotus sends one cast effect and one per enemy struck");
        Check(lotusFx.Where(e => e.Pos2.X >= 0f).Select(e => e.Pos2.X).OrderBy(x => x).SequenceEqual(Enumerable.Range(0, 8).Select(i => i * 0.11f)), "the cuts are staggered 0.11 s apart");
        int struck = marks.Where((e, i) => e.Hp < hpsBefore[i]).Count();
        Console.WriteLine("  blade fan: {0} kunai; death lotus struck {1} of 10", kunai, struck);
        Check(struck == 8, "Death Lotus strikes 8 enemies");

        // Thousand Clones (the Tank Ninja's ultimate): twelve clones all over the map, until they die or the round ends.
        hp.Slots[Upgrades.UltSlot].Charges = 1;
        cast.Invoke(g, new object[] { 1, Upgrades.UltSlot, hp.Pos, true });
        var tc = g.Minions.Values.Where(m => m.Kind == MinionKind.Clone && m.Owner == 1).ToList();
        Check(tc.Count == 12, "Thousand Clones makes twelve clones");
        float minGap = 1e9f;
        for (int i = 0; i < tc.Count; i++) for (int j = i + 1; j < tc.Count; j++) minGap = Math.Min(minGap, tc[i].Pos.DistanceTo(tc[j].Pos));
        Check(minGap > 70f && tc.Max(m => m.Pos.DistanceTo(hp.Pos)) > 400f, "the clones are spread over the whole map, not ringed around you");
        Check(tc.All(m => m.T < 0f), "the clones have no timer");
        run(12f);
        Check(g.Minions.Values.Count(m => m.Kind == MinionKind.Clone && m.Owner == 1) >= 1, "a clone outlives the old 8 s");
        var tgt = g.Minions.Values.First(m => m.Kind == MinionKind.Clone && m.Owner == 1);
        Check(tgt.BlockHp > 0f && tgt.Def.BlockCapacity == 100f, "a clone soaks 100 damage");
        s[0].Leave(""); s[1].Leave("");
    }
}
