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
        var host = new Session { AutoStart = 2, StartWave = 3 };
        host.Host(port, "Tank", Role.Tank, 0, 0, Kit.Ninja);
        var client = new Session();
        client.Join("127.0.0.1", port, "Healer", Role.Healer, 0, 0, Kit.Ninja);
        var s = new[] { host, client };
        for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
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

        // Kusarigama: the throw hooks the first enemy in its path; holding fire reels you in to it for a heavy, stunning slash.
        place(1, 400f); run(0.2f);
        var front = dummy(new V2(600f, 450f));
        var behind = dummy(new V2(650f, 450f));
        V2 frontAt = front.Pos, behindAt = behind.Pos;
        Action pinBoth = () => { front.Pos = frontAt; behind.Pos = behindAt; front.FireT = behind.FireT = 99f; };
        float hookedAt = -1f, hitOnHook = 0f, behindOnHook = -1f, maxStun = 0f;
        bool clientSawHook = false;
        for (int f = 0; f < 70; f++)
        {
            host.Poll(); g.FixedStep(dt, new PlayerInput { Fire = true, AimAt = frontAt }); g.Update(dt);
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
        Check(reeled < front.Radius + hp.Def.BodyR + 20f && total >= hitOnHook * 2.5f && maxStun > 0.3f, "firing while hooked reels you in to it and lands a heavy, stunning slash");
        front.Hp = behind.Hp = 0f; keep.Remove(front); keep.Remove(behind);
        var d1 = dummy(new V2(770f, 450f));

        // Bo Staff: click while moving to vault, striking what you pass; holding or standing still doesn't.
        hp.Weapon = Defs.Weapon(Role.Tank, Kit.Ninja, 1);
        place(1, 300f); run(0.3f);
        d1.Pos = new V2(380f, 450f);
        var aside = dummy(new V2(380f, 600f));
        float d1hp = d1.Hp;
        Func<int, bool, V2, float> stepFor = (frames, fire, mv) =>
        {
            float x0 = hp.Pos.X;
            for (int f = 0; f < frames; f++)
            {
                host.Poll(); g.FixedStep(dt, new PlayerInput { Fire = fire, Move = mv, AimAt = hp.Pos + new V2(100f, 0f) }); g.Update(dt);
                client.Poll(); client.Game.FixedStep(dt, new PlayerInput()); client.Game.Update(dt);
                clear(); d1.Pos = new V2(380f, 450f); aside.Pos = new V2(380f, 600f); d1.FireT = aside.FireT = 99f;
            }
            return hp.Pos.X - x0;
        };
        float vaulted = stepFor(11, true, new V2(1f, 0f));
        float vaultHit = d1hp - d1.Hp;
        float held = stepFor(90, true, new V2(1f, 0f)) / 90f * 11f;  // holding: walking pace only
        stepFor(10, false, V2.Zero);
        float still = stepFor(11, true, V2.Zero);
        Console.WriteLine("  bo staff: vaulted {0:0} px in 11 ticks (walking pace {1:0}), struck the one in its path for {2:0.0}, the one aside {3:0.0}; standing still moved {4:0}",
            vaulted, held, vaultHit, aside.MaxHp - aside.Hp, still);
        Check(vaulted > Game.VaultDist * 0.9f && vaultHit >= hp.Weapon.Damage * Game.VaultMult && aside.Hp == aside.MaxHp,
            "clicking fire while moving vaults the Bo Staff 150 px, striking everything it passes");
        Check(held < vaulted * 0.6f && Math.Abs(still) < 2f, "holding fire or standing still doesn't vault");
        // The same vault by a joined player: their game moves them and tells the host, which lands the strike.
        var cme = client.Game.Me;
        cp.Weapon = cme.Weapon = Defs.Weapon(Role.Tank, Kit.Ninja, 1);
        place(2, 900f); run(0.3f);
        aside.Pos = new V2(980f, 450f);
        float asideHp = aside.Hp, cx0 = cme.Pos.X;
        for (int f = 0; f < 15; f++)
        {
            host.Poll(); g.FixedStep(dt, new PlayerInput()); g.Update(dt);
            client.Poll(); client.Game.FixedStep(dt, new PlayerInput { Fire = true, Move = new V2(1f, 0f), AimAt = cme.Pos + new V2(100f, 0f) }); client.Game.Update(dt);
            Thread.Sleep(1);
            clear(); aside.Pos = new V2(980f, 450f); aside.FireT = 99f;
        }
        for (int f = 0; f < 10; f++) { Thread.Sleep(2); host.Poll(); g.FixedStep(dt, new PlayerInput()); client.Poll(); }
        Console.WriteLine("  bo staff (joined player): vaulted {0:0} px; struck the one in its path for {1:0.0}", cme.Pos.X - cx0, asideHp - aside.Hp);
        Check(cme.Pos.X - cx0 > Game.VaultDist * 0.9f && asideHp - aside.Hp >= cp.Weapon.Damage * Game.VaultMult, "a joined player's vault moves them and the host lands its strike");
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
        damage.Invoke(g, new object[] { hp, 100f, "test" });
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
        damage.Invoke(g, new object[] { hp, 100f, "test" });
        float smokeTaken = hp.MaxHp - hp.Hp;
        hp.Hp = hp.MaxHp; hp.DevGod = true;
        Console.WriteLine("  smoke: a 200 px/s bullet moved {0:0} px in 0.5 s (client {1:0}); took {2:0} of 100 inside", moved, cmoved, smokeTaken);
        Check(moved > 40f && moved < 60f && Math.Abs(cmoved - moved) < 15f && Math.Abs(smokeTaken - 70f) < 1f,
            "Smoke Bomb slows enemy bullets inside to half speed on every machine, and allies inside take 30% less");
        g.Zones.Clear();

        // Shadow Clone: enemies aim at it, and it bursts when it ends.
        place(1, 650f); run(0.2f);
        use(1, AbilityId.ShadowClone, hp.Pos);
        run(0.1f);
        Minion clone = g.Minions.Values.FirstOrDefault(m => m.Kind == MinionKind.Clone);
        V2 aimed = clone != null ? (V2)aimAt.Invoke(g, new object[] { d1, hp, hp.Pos }) : new V2(-1f, -1f);
        Check(clone != null && aimed.DistanceTo(clone.Pos) < 1f, "enemies aim at a Shadow Clone instead of you");
        clone.Pos = d1.Pos + new V2(-30f, 0f);
        dhp = d1.Hp;
        run(5.2f);
        Check(!g.Minions.Values.Any(m => m.Kind == MinionKind.Clone) && d1.Hp < dhp - 30f, "a Shadow Clone bursts when it ends");

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

        // Healing Mist follows you and heals allies in it; Blossom Storm heals everyone and clears bullets near them.
        place(1, 700f); place(2, 760f); run(0.3f);
        cp.Hp = cp.MaxHp * 0.3f;
        float low = cp.Hp;
        use(1, AbilityId.HealingMist, hp.Pos);
        run(1f);
        Check(cp.Hp > low + 6f, "Healing Mist heals allies in it");
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
        int kunai = g.Bullets.Values.Count(b => b.Kind == Defs.KunaiShot && b.Owner == 1);
        var marks = new List<Enemy>();
        for (int i = 0; i < 10; i++) marks.Add(dummy(new V2(1100f + (i % 5) * 40f, 300f + (i / 5) * 60f)));
        run(0.2f);
        var hpsBefore = marks.Select(e => e.Hp).ToArray();
        hp.Slots[Upgrades.UltSlot].Charges = 1;
        g.ShowcaseSetAbility(AbilityId.DeathLotus);  // on Space for the test
        hp.Slots[0].Charges = 1;
        cast.Invoke(g, new object[] { 1, 0, new V2(1180f, 330f), true });
        int struck = marks.Where((e, i) => e.Hp < hpsBefore[i]).Count();
        Console.WriteLine("  blade fan: {0} kunai; death lotus struck {1} of 10", kunai, struck);
        Check(kunai == 12 && struck == 8, "Blade Fan throws 12 kunai and Death Lotus strikes 8 enemies");

        // Thousand Clones (the Tank Ninja's ultimate): five clones.
        hp.Slots[Upgrades.UltSlot].Charges = 1;
        cast.Invoke(g, new object[] { 1, Upgrades.UltSlot, hp.Pos, true });
        Check(g.Minions.Values.Count(m => m.Kind == MinionKind.Clone && m.Owner == 1) == 5, "Thousand Clones surrounds you with five clones");
        s[0].Leave(""); s[1].Leave("");
    }
}
