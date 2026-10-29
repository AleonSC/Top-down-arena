using System.Linq;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using Arena;

// The Blood Mage class.
static partial class Program
{
    // Vein Tether is a beam: it hurts what's on your aim line each tick, so you have to keep it on the enemy.
    static void VeinBeamTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var queueField = typeof(Game).GetField("spawnQueue", flags);
        int port = nextPort++;
        var host = new Session { AutoStart = 2, StartWave = 3 };
        host.Host(port, "Healer", Role.Healer, 2, 0, Kit.BloodMage);
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Dps);
        var s = new[] { host, client };
        for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
        var g = host.Game;
        g.Bot = client.Game.Bot = false;
        Run(s, dt, 8, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        Check(me.Weapon.Id == WeaponId.VeinTether, "the Blood Mage healer's third weapon is Vein Tether");
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        ((List<EnemyType>)queueField.GetValue(g)).Clear();
        foreach (var e in g.Enemies.Values) e.Hp = 0f;
        Run(s, dt, 0.2f);
        me.Pos = me.Disp = new V2(600f, 450f);
        var target = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, new V2(800f, 450f), 0f, 0, false });
        target.Hp = target.MaxHp = 1e6f;
        client.Game.Players[2].Pos = new V2(300f, 200f);  // out of the Vein's sharing range
        Func<V2, float, float> Beam = (aimAt, secs) =>
        {
            float before = target.Hp;
            for (int f = 0; f < secs * 60; f++)
            {
                target.FrozenT = 1f; target.FireT = 9f; me.Pos = new V2(600f, 450f);
                host.Poll();
                g.FixedStep(dt, new PlayerInput { Fire = true, AimAt = aimAt });
                g.Update(dt);
                Step(client, dt);
                if (f % 2 == 0) Thread.Sleep(1);
            }
            return before - target.Hp;
        };
        int bulletsBefore = g.Bullets.Count;
        float onTarget = Beam(target.Pos, 1f);
        bool clientSees = client.Game.Players[1].FiringSeen;
        float offTarget = Beam(new V2(600f, 100f), 1f);  // aimed straight up, away from it
        Console.WriteLine("  vein tether: {0:0} damage in 1 s on target, {1:0} aimed away; no bullets ({2}); the friend sees the beam: {3}", onTarget, offTarget, g.Bullets.Count - bulletsBefore, clientSees);
        Check(onTarget > 30f && offTarget == 0f, "Vein Tether only hurts while you hold the beam on the enemy");
        Check(g.Bullets.Values.All(b => b.Kind != Defs.VeinShot) && clientSees, "it's a beam, not bullets, and other players see it firing");
        target.Pos = new V2(600f + Game.VeinBeamRange + 60f, 450f);
        Check(Beam(target.Pos, 0.5f) == 0f, "the beam doesn't reach past its range");
        target.Pos = new V2(800f, 450f);
        g.DevSetGod(1, false);
        me.Hp = me.MaxHp * 0.5f;
        float hp0 = me.Hp;
        Beam(target.Pos, 1f);
        Console.WriteLine("  vein tether: healed {0:0.0} HP in 1 s", me.Hp - hp0);
        Check(me.Hp > hp0 + 5f, "the beam drains: its damage heals you");
        host.Leave(""); client.Leave("");
    }

    static void BloodMageTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var cast = typeof(Game).GetMethod("HostAbility", flags);
        var damage = typeof(Game).GetMethod("Damage", flags);
        var price = typeof(Game).GetMethod("BloodDamageMult", flags);
        var hurt = typeof(Game).GetMethod("HurtEnemy", flags);

        Check(Defs.Weapon(Role.Tank, Kit.BloodMage, 0).Name == "Blood Lash" && Defs.Weapon(Role.Healer, Kit.BloodMage, 0).Name == "Transfusion Darts"
            && Defs.Weapon(Role.Dps, Kit.BloodMage, 2).Name == "Bone Spear" && Defs.Weapon(Role.Dps, Kit.Ninja, 2).Name == "Kunai"
            && Defs.Weapon(Role.Healer, Kit.Soldier, 1).Name == "Revolver" && Defs.Weapon(Role.Tank, Kit.Knight, 1).Name == "Lance",
            "Blood Mage weapons sit in the role/class tables without shifting the others");
        Check(Upgrades.RoleAbility(Role.Healer, Kit.BloodMage, 2) == AbilityId.Martyr && Upgrades.RoleAbility(Role.Dps, Kit.Ninja, 0) == AbilityId.ShadowStep
            && Upgrades.Ultimate(Role.Dps, Kit.BloodMage) == AbilityId.CrimsonApotheosis && Upgrades.Ultimate(Role.Healer, Kit.Ninja) == AbilityId.BlossomStorm
            && Upgrades.Ultimate(Role.Tank, Kit.BloodMage) == AbilityId.BloodRite && Defs.KitName(Kit.BloodMage) == "Blood Mage",
            "Blood Mage abilities and ultimates map to the right role and class");

        int port = nextPort++;
        var host = new Session { AutoStart = 2, StartWave = 3 };
        host.Host(port, "Tank", Role.Tank, 0, 0, Kit.BloodMage);
        var client = new Session();
        client.Join("127.0.0.1", port, "Healer", Role.Healer, 0, 0, Kit.BloodMage);
        var s = new[] { host, client };
        for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var hp = g.Players[1];
        var cp = g.Players[2];
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        Check(hp.Kit == Kit.BloodMage && Math.Abs(hp.MaxHp - Defs.Get(Role.Tank).Hp) < 0.5f && Math.Abs(hp.Speed - Defs.Get(Role.Tank).Speed) < 0.5f,
            "Blood Mages have full health and normal speed");

        var keep = new HashSet<Enemy>();
        Action clear = () => { foreach (var e in g.Enemies.Values) if (!keep.Contains(e)) e.Hp = 0f; };
        Func<V2, Enemy> dummy = at => { var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false }); e.Hp = e.MaxHp = 1e6f; keep.Add(e); return e; };
        Action<float> run = secs => Run(s, dt, secs, null, () => { clear(); foreach (var e in keep) e.FrozenT = 5f; });
        Action<AbilityId, V2> use = (ability, at) => { g.ShowcaseSetAbility(ability); hp.Slots[0].Charges = 1; cast.Invoke(g, new object[] { 1, 0, at, true }); };
        Action<int, float> place = (id, x) => { var q = (id == 1 ? host : client).Game.Me; q.Pos = q.Disp = new V2(x, 450f); };
        clear(); run(0.3f);

        // Health costs: a share of max health, never below 1.
        hp.Hp = hp.MaxHp * 0.5f;
        float h0 = hp.Hp;
        use(AbilityId.BloodShell, hp.Pos);
        float paid = h0 - hp.Hp;
        Console.WriteLine("  blood shell: paid {0:0.0} of {1:0} max; shield {2:0}", paid, hp.MaxHp, hp.ShieldHp);
        Check(Math.Abs(paid - hp.MaxHp * 0.15f) < 0.5f && Math.Abs(hp.ShieldHp - paid * 3f) < 1f, "Blood Shell costs 15% of max health and shields three times as much");
        hp.ShieldHp = 0f; hp.Hp = 1f;
        use(AbilityId.BloodShell, hp.Pos);
        Check(hp.Hp == 1f && hp.ShieldHp == 0f && !hp.Downed, "a health cost stops at 1 HP (the ability still casts, for nothing)");

        // Blood Price: up to +40% at 10% health.
        hp.Hp = hp.MaxHp;
        float full = (float)price.Invoke(g, new object[] { hp });
        hp.Hp = hp.MaxHp * 0.1f;
        float low = (float)price.Invoke(g, new object[] { hp });
        hp.Hp = hp.MaxHp * 0.55f;
        float mid = (float)price.Invoke(g, new object[] { hp });
        Console.WriteLine("  blood price: x{0:0.00} full, x{1:0.00} at 55%, x{2:0.00} at 10%", full, mid, low);
        Check(Math.Abs(full - 1f) < 0.01f && Math.Abs(mid - 1.2f) < 0.01f && Math.Abs(low - 1.4f) < 0.01f, "Blood Price: up to 40% more damage the lower your health");

        // Lifesteal: 3% of damage dealt.
        place(1, 500f); run(0.2f);
        var d1 = dummy(new V2(900f, 450f));
        hp.Hp = hp.MaxHp * 0.5f;
        float hBefore = hp.Hp, eBefore = d1.Hp;
        hurt.Invoke(g, new object[] { d1, 100f, 1 });
        float dealt = eBefore - d1.Hp, healed = hp.Hp - hBefore;
        Console.WriteLine("  lifesteal: dealt {0:0.0} (Blood Price at half health), healed {1:0.00}", dealt, healed);
        Check(dealt > 120f && Math.Abs(healed - dealt * 0.03f) < 0.05f, "Blood Mage hits heal it for 3% of the damage dealt");

        // Crimson Pact: everyone targets you, and the damage you took bursts back out.
        hp.Hp = hp.MaxHp;
        place(1, 820f); run(0.2f);
        use(AbilityId.CrimsonPact, hp.Pos);
        bool taunted = g.Enemies.Values.Where(e => !keep.Contains(e)).All(e => e.TargetId == 1);
        hp.DevGod = false;
        damage.Invoke(g, new object[] { hp, 50f, "test" });
        hp.DevGod = true;
        run(0.3f);
        bool seen = client.Game.Players[1].PactT > 0f;
        float stored = hp.PactStore;
        eBefore = d1.Hp;
        run(4f);
        Console.WriteLine("  crimson pact: stored {0:0}, burst dealt {1:0}; client saw it {2}", stored, eBefore - d1.Hp, seen);
        Check(taunted && seen && Math.Abs(stored - 50f) < 0.5f && eBefore - d1.Hp > 74f, "Crimson Pact draws every enemy and bursts back 150% of the damage you took");

        // Exsanguinate: pulls enemies in and drains them into you.
        place(1, 700f); run(0.2f);
        hp.Hp = hp.MaxHp * 0.5f;
        float dist0 = d1.Pos.DistanceTo(hp.Pos);
        use(AbilityId.Exsanguinate, hp.Pos);
        float hpAfter = hp.Hp - (hp.MaxHp * 0.5f - hp.MaxHp * 0.1f);
        Console.WriteLine("  exsanguinate: dummy {0:0} -> {1:0} px; healed {2:0.0} after the cost", dist0, d1.Pos.DistanceTo(hp.Pos), hpAfter);
        Check(d1.Pos.DistanceTo(hp.Pos) < dist0 - 50f && hpAfter >= 20f, "Exsanguinate pulls enemies in and drains health from them");

        // Blood Gift: the most hurt ally heals twice what you paid.
        place(1, 600f); place(2, 700f); run(0.3f);
        hp.Hp = hp.MaxHp; cp.Hp = cp.MaxHp * 0.2f;
        float c0 = cp.Hp;
        use(AbilityId.BloodGift, hp.Pos);
        float gave = hp.MaxHp - hp.Hp;
        Console.WriteLine("  blood gift: paid {0:0}, ally healed {1:0}", gave, cp.Hp - c0);
        Check(Math.Abs(gave - hp.MaxHp * 0.25f) < 0.5f && Math.Abs(cp.Hp - c0 - Math.Min(gave * 2f, cp.MaxHp - c0)) < 1f, "Blood Gift pays 25% of your health and the most hurt ally heals twice that");

        // Martyr: revives a downed ally anywhere.
        place(2, 1400f); run(0.3f);
        cp.Hp = 0f; cp.Downed = true;
        hp.Hp = hp.MaxHp;
        use(AbilityId.Martyr, new V2(1400f, 450f));
        run(0.3f);
        Check(!cp.Downed && Math.Abs(cp.Hp - cp.MaxHp * 0.5f) < 2f && !client.Game.Me.Downed && Math.Abs(hp.Hp - hp.MaxHp * 0.6f) < 0.5f,
            "Martyr spends 40% of your health to revive a downed ally across the arena at 50%");

        // Blood Boil: enemies near you burst after 1 s for three times what you paid.
        place(1, 820f); run(0.2f);
        hp.Hp = hp.MaxHp;
        eBefore = d1.Hp;
        use(AbilityId.BloodBoil, hp.Pos);
        float boilPaid = hp.MaxHp - hp.Hp;
        run(0.5f);
        float early = eBefore - d1.Hp;
        run(0.8f);
        float boiled = eBefore - d1.Hp;
        Console.WriteLine("  blood boil: paid {0:0}; {1:0} after 0.5 s, {2:0} after 1.3 s", boilPaid, early, boiled);
        Check(early < 1f && boiled >= boilPaid * 3f - 1f, "Blood Boil bursts after 1 s for three times the health paid");

        // Frenzy Pact: faster firing, a drain that stops at 1 HP.
        hp.Hp = 30f;
        use(AbilityId.FrenzyPact, hp.Pos);
        run(5f);
        Check(hp.RushT > 0f && hp.Hp >= 1f && hp.Hp < 30f && !hp.Downed, "Frenzy Pact drains health while it lasts, never below 1");
        hp.RushT = 0f;

        // Hemo Nova: 16 bolts in a ring, each that hits heals you.
        g.Bullets.Clear();
        hp.Hp = hp.MaxHp;
        use(AbilityId.HemoNova, hp.Pos);
        int bolts = g.Bullets.Values.Count(b => b.Kind == Defs.BloodShot && b.Owner == 1 && b.HealOwner > 0f);
        Check(bolts == 16, "Hemo Nova fires a ring of 16 blood bolts that heal you");

        // Bone Spear: 2 health a throw.
        g.Bullets.Clear();
        hp.Hp = 50f;
        hp.Weapon = Defs.Weapon(Role.Dps, Kit.BloodMage, 2);
        typeof(Game).GetMethod("FireVolley", flags).Invoke(g, new object[] { hp, 0f, 0 });
        Check(Math.Abs(hp.Hp - 48f) < 0.01f && g.Bullets.Values.Any(b => b.Kind == Defs.BoneSpearShot), "a Bone Spear costs 2 health a throw");
        hp.Weapon = Defs.Weapon(Role.Tank, Kit.BloodMage, 0);

        // Blood Rite: 60% less damage, and the damage you take heals your allies.
        hp.Hp = hp.MaxHp; cp.Hp = cp.MaxHp * 0.3f;
        hp.Slots[Upgrades.UltSlot].Charges = 1;
        cast.Invoke(g, new object[] { 1, Upgrades.UltSlot, hp.Pos, true });
        float c1 = cp.Hp;
        hp.DevGod = false;
        damage.Invoke(g, new object[] { hp, 100f, "test" });
        hp.DevGod = true;
        float moonTaken = hp.MaxHp - hp.Hp;
        Console.WriteLine("  blood rite: took {0:0} of 100; ally healed {1:0}", moonTaken, cp.Hp - c1);
        Check(Math.Abs(moonTaken - 40f) < 1f && Math.Abs(cp.Hp - c1 - moonTaken) < 1f, "Blood Rite: you take 60% less and your allies heal what you take");
        hp.UltT = 0f;

        // Crimson Apotheosis: can't drop below 1 HP.
        hp.Slots[Upgrades.UltSlot].Id = AbilityId.CrimsonApotheosis;
        hp.UltT = 6f; hp.ApoUp = true;
        hp.DevGod = false; hp.Hp = 10f;
        damage.Invoke(g, new object[] { hp, 500f, "test" });
        hp.DevGod = true;
        Check(hp.Hp == 1f && !hp.Downed, "during Crimson Apotheosis you can't drop below 1 HP");
        s[0].Leave(""); s[1].Leave("");
    }
}
