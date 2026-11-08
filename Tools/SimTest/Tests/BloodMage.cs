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
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Healer", Role.Healer, 2, 0, Kit.Hemomancer);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        g.Bot = client.Game.Bot = false;
        Run(s, dt, 8, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        Check(me.Weapon.Id == WeaponId.VeinTether, "the Blood Mage healer's third weapon is Vein Tether");
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        ((List<EnemyType>)queueField.GetValue(g)).Clear(); ClearSquads(g);
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
        target.Pos = new V2(600f + Defs.WeaponOf(WeaponId.VeinTether).Reach + 60f, 450f);
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

    // Leech Tendrils grasp the enemies in an arc in front of you for as long as you hold fire: they're stunned,
    // drained slowly and the drain heals you; let go and they're free.
    static void LeechGraspTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var queueField = typeof(Game).GetField("spawnQueue", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Tank", Role.Tank, 2, 0, Kit.Hemomancer);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        g.Bot = client.Game.Bot = false;
        Run(s, dt, 8, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        Check(me.Weapon.Id == WeaponId.LeechBeam && me.Weapon.Name == "Leech Tendrils", "the Hemomancer tank's third weapon is Leech Tendrils");
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        ((List<EnemyType>)queueField.GetValue(g)).Clear(); ClearSquads(g);
        foreach (var e in g.Enemies.Values) e.Hp = 0f;
        Run(s, dt, 0.2f);
        client.Game.Players[2].Pos = new V2(300f, 200f);
        V2 at = new V2(600f, 450f);
        Func<V2, Enemy> Foe = pos =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, pos, 0f, 0, false });
            e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f;
            return e;
        };
        // Three in the arc (one off to the side, 30 degrees), one more in the arc but farther, one behind you.
        var a = Foe(at + V2.FromAngle(0f) * 90f);
        var b = Foe(at + V2.FromAngle(0.52f) * 110f);
        var c = Foe(at + V2.FromAngle(-0.3f) * 120f);
        var d = Foe(at + V2.FromAngle(0.1f) * 150f);
        var behind = Foe(at - new V2(90f, 0f));
        var all = new[] { a, b, c, d, behind };
        var hp0 = all.Select(e => e.Hp).ToArray();
        V2 dPos = d.Pos, behindPos = behind.Pos;
        Action<bool, float> Hold = (fire, secs) =>
        {
            for (int f = 0; f < secs * 60; f++)
            {
                me.Pos = at;
                foreach (var e in all) e.FireT = 9f;
                d.Pos = dPos; behind.Pos = behindPos; d.FrozenT = behind.FrozenT = 0.1f;  // these two wait where they are
                host.Poll();
                g.FixedStep(dt, new PlayerInput { Fire = fire, AimAt = at + new V2(300f, 0f) });
                g.Update(dt);
                Step(client, dt);
                if (f % 2 == 0) Thread.Sleep(1);
            }
        };
        Hold(true, 0.1f);
        V2 bStart = b.Pos;
        Hold(true, 1.9f);
        var dealt = all.Select((e, i) => hp0[i] - e.Hp).ToArray();
        var seen = client.Game.Players[1].Grasp.ToList();
        Console.WriteLine("  leech tendrils: held {0}; damage in 2 s: {1}; the friend sees {2} held", string.Join(",", me.Grasp), string.Join(", ", dealt.Select(x => x.ToString("0"))), seen.Count);
        Check(me.Grasp.Count == 3 && me.Grasp.Contains(a.Id) && me.Grasp.Contains(b.Id) && me.Grasp.Contains(c.Id), "the tendrils grab the three nearest enemies in the arc in front of you");
        Check(dealt[3] == 0f && dealt[4] == 0f, "a fourth enemy waits for a free tendril, and nothing behind you is touched");
        Check(dealt[0] > 18f && dealt[0] < 28f, "held enemies are drained slowly (about 12 a second)");
        Check(a.GraspT > 0f && b.Pos.DistanceTo(bStart) < 1f, "held enemies are stunned in place");
        Check(seen.Count == 3, "other players see who the tendrils hold");
        Check(g.Bullets.Values.All(x => x.Owner != 1), "the tendrils fire no bullets");
        // Let go and they're free.
        Hold(false, 0.5f);
        Check(me.Grasp.Count == 0 && a.GraspT <= 0f, "let go of fire and the tendrils let go");
        // The leech: 25% of the damage heals you, slowly.
        g.DevSetGod(1, false);
        me.Hp = me.MaxHp * 0.5f;
        float before = me.Hp, aHp = a.Hp;
        Hold(true, 1f);
        float healed = me.Hp - before;
        Console.WriteLine("  leech tendrils: healed {0:0.0} HP in 1 s holding three", healed);
        Check(healed > 5f && healed < 14f, "the tendrils heal you slowly for some of what they drain");
        // A held enemy that gets away (pushed far off) slips free.
        a.Pos = at + new V2(400f, 0f);
        Hold(true, 0.3f);
        Check(!me.Grasp.Contains(a.Id) && me.Grasp.Contains(d.Id), "an enemy pulled out of reach slips free and a free tendril grabs the next");
        // Alone in reach, an enemy gets every tendril: three drains at once (about 36 a second).
        foreach (var e in all) if (e != a) e.Hp = 0f;
        a.Pos = at + new V2(90f, 0f);
        Hold(true, 0.3f);
        float alone0 = a.Hp;
        Hold(true, 2f);
        float alone = alone0 - a.Hp;
        Console.WriteLine("  leech tendrils: {0:0} damage in 2 s to a lone enemy (all {1} tendrils on it)", alone, Game.TendrilsOn(3, 1, 0));
        Check(me.Grasp.Count == 1 && Game.TendrilsOn(3, 1, 0) == 3 && alone > 3f * 18f && alone < 3f * 28f,
            "spare tendrils pile onto the enemies already held: a lone one takes all three");
        Check(Game.TendrilsOn(5, 2, 0) == 3 && Game.TendrilsOn(5, 2, 1) == 2 && Game.TendrilsOn(3, 3, 2) == 1, "spare tendrils are shared out, nearest first");
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

        Check(Defs.Weapon(Role.Tank, Kit.Hemomancer, 0).Name == "Blood Lash" && Defs.Weapon(Role.Healer, Kit.Hemomancer, 0).Name == "Transfusion Darts"
            && Defs.Weapon(Role.Dps, Kit.Hemomancer, 2).Name == "Bone Spear" && Defs.Weapon(Role.Dps, Kit.Ninja, 2).Name == "Kunai"
            && Defs.Weapon(Role.Healer, Kit.Soldier, 1).Name == "Revolver" && Defs.Weapon(Role.Tank, Kit.Knight, 1).Name == "Lance",
            "Blood Mage weapons sit in the role/class tables without shifting the others");
        Check(Upgrades.RoleAbility(Role.Healer, Kit.Hemomancer, 2) == AbilityId.Martyr && Upgrades.RoleAbility(Role.Dps, Kit.Ninja, 0) == AbilityId.ShadowStep
            && Upgrades.Ultimate(Role.Dps, Kit.Hemomancer) == AbilityId.CrimsonApotheosis && Upgrades.Ultimate(Role.Healer, Kit.Ninja) == AbilityId.BlossomStorm
            && Upgrades.Ultimate(Role.Tank, Kit.Hemomancer) == AbilityId.BloodRite && Defs.KitName(Kit.Hemomancer) == "Hemomancer" && Defs.KitName(Kit.Elementalist) == "Elementalist",
            "Blood Mage abilities and ultimates map to the right role and class");

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Tank", Role.Tank, 0, 0, Kit.Hemomancer);
        var client = JoinAs(port, false, "Healer", Role.Healer, 0, 0, Kit.Hemomancer);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var hp = g.Players[1];
        var cp = g.Players[2];
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        Check(hp.Kit == Kit.Hemomancer && Math.Abs(hp.MaxHp - Defs.Get(Role.Tank).Hp) < 0.5f && Math.Abs(hp.Speed - Defs.Get(Role.Tank).Speed) < 0.5f,
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
        use(AbilityId.Exsanguinate, hp.Pos);
        float paid = h0 - hp.Hp;
        Console.WriteLine("  exsanguinate: paid {0:0.0} of {1:0} max", paid, hp.MaxHp);
        Check(Math.Abs(paid - hp.MaxHp * 0.1f) < 0.5f, "a Blood Mage ability costs its share of max health");
        hp.Hp = 1f;
        use(AbilityId.Exsanguinate, hp.Pos);
        Check(hp.Hp == 1f && !hp.Downed, "a health cost stops at 1 HP (the ability still casts, for nothing)");

        // Blood Shell: held, free to raise; stops bullets and enemies at its skin; each bullet costs you and heals allies inside.
        place(1, 500f); place(2, 540f); run(0.3f);
        hp.Hp = hp.MaxHp * 0.5f; cp.Hp = cp.MaxHp * 0.5f;
        float s0 = hp.Hp, cAlly0 = cp.Hp;
        g.HoldShell = true;
        use(AbilityId.BloodShell, hp.Pos);
        Check(hp.ShellOn && hp.Hp == s0 && hp.ShieldHp == 0f, "Blood Shell costs nothing to raise and doesn't shield");
        var inside = dummy(hp.Pos + new V2(40f, 0f));
        run(0.3f);
        float want = Game.ShellRadius + inside.Radius - 1f;
        Console.WriteLine("  blood shell: enemy put out to {0:0} (skin at {1:0})", inside.Pos.DistanceTo(hp.Pos), Game.ShellRadius + inside.Radius);
        Check(inside.Pos.DistanceTo(hp.Pos) >= want, "enemies can't be inside the shell: one in it is put out at the skin");
        g.Bullets[900001] = new Bullet { Id = 900001, Kind = 10, Pos = hp.Pos + new V2(0f, -220f), Vel = new V2(0f, 300f), Life = 3f, Radius = 4f, Dmg = 5f };
        float prevHp = hp.Hp, maxDrop = 0f;
        Run(s, dt, 1f, null, () => { clear(); foreach (var e in keep) e.FrozenT = 5f; maxDrop = Math.Max(maxDrop, prevHp - hp.Hp); prevHp = hp.Hp; });
        Console.WriteLine("  blood shell: one bullet cost {0:0.0}, ally gained {1:0.0}", maxDrop, cp.Hp - cAlly0);
        Check(!g.Bullets.ContainsKey(900001) && Math.Abs(maxDrop - 7f) < 0.6f, "a bullet stops at the skin and costs 7 health");
        Check(cp.Hp - cAlly0 >= 3.5f && cp.Hp - cAlly0 < 20f && hp.ShellOn, "an ally inside heals when it stops a bullet, and the shell stays up");
        g.HoldShell = false;
        run(0.5f);
        Check(!hp.ShellOn, "letting go brings the shell down");
        hp.Hp = hp.MaxHp * Game.ShellPopFrac + 1f;
        g.HoldShell = true;
        use(AbilityId.BloodShell, hp.Pos);
        var near = dummy(hp.Pos + new V2(Game.ShellRadius + 30f, 0f));
        run(0.2f);
        float before = near.Pos.DistanceTo(hp.Pos);
        g.Bullets[900002] = new Bullet { Id = 900002, Kind = 10, Pos = hp.Pos + new V2(0f, -220f), Vel = new V2(0f, 300f), Life = 3f, Radius = 4f, Dmg = 5f };
        run(1f);
        Console.WriteLine("  blood shell: burst with {0:0} of {1:0} health, an enemy at the skin was thrown {2:0} px", hp.Hp, hp.MaxHp, near.Pos.DistanceTo(hp.Pos) - before);
        Check(!hp.ShellOn && hp.Hp <= hp.MaxHp * Game.ShellPopFrac + 8f, "it bursts when your health falls to a third");
        Check(near.Pos.DistanceTo(hp.Pos) > before + 60f, "the burst throws nearby enemies back");
        g.HoldShell = false;
        keep.Remove(inside); keep.Remove(near); inside.Hp = near.Hp = 0f;
        run(0.2f);

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
        damage.Invoke(g, new object[] { hp, 50f / g.EnemyDamageScale(g.Wave), "test" });  // 50 after the difficulty's wave growth
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
        Check(g.Effects.Exists(e => e.Kind == FxKind.MartyrCast && (int)e.Radius == 1 && e.Pos2.DistanceTo(cp.Pos) < 5f) || client.Game.Effects.Exists(e => e.Kind == FxKind.MartyrCast),
            "a Martyr revive plays the Martyr cast effect at the ally (Radius 1)");

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
        hp.Weapon = Defs.Weapon(Role.Dps, Kit.Hemomancer, 2);
        typeof(Game).GetMethod("FireVolley", flags).Invoke(g, new object[] { hp, 0f, 0 });
        Check(Math.Abs(hp.Hp - 48f) < 0.01f && g.Bullets.Values.Any(b => b.Kind == Defs.BoneSpearShot), "a Bone Spear costs 2 health a throw");

        // Implosion: the enemy you click collapses (34), enemies against it take 15 and are pulled in;
        // an extra projectile collapses one more enemy nearby (60%). Nothing under the cursor, nothing fires.
        hp.Hp = hp.MaxHp;
        place(1, 600f); run(0.2f);
        hp.Weapon = Defs.Weapon(Role.Dps, Kit.Hemomancer, 1);
        var i2 = dummy(d1.Pos + new V2(0f, d1.Radius * 2f + 20f));
        var i3 = dummy(d1.Pos + new V2(0f, -150f));
        float gap0 = i2.Pos.DistanceTo(d1.Pos);
        float a1 = d1.Hp, a2 = i2.Hp, a3 = i3.Hp;
        Check(hp.Weapon.Id == WeaponId.Implosion && g.ImplodeTarget(hp, d1.Pos + new V2(d1.Radius + 10f, 0f)) == d1
            && g.ImplodeTarget(hp, d1.Pos + new V2(-200f, 0f)) == null && g.ImplodeTarget(hp, new V2(1590f, 450f)) == null,
            "Implosion picks the enemy under the cursor (with a little slack), nothing on empty ground or out of reach");
        hp.AimAt = d1.Pos;
        typeof(Game).GetMethod("FireVolley", flags).Invoke(g, new object[] { hp, 0f, 1 });
        run(0.1f);
        float t1 = a1 - d1.Hp, t2 = a2 - i2.Hp, t3 = a3 - i3.Hp;
        Console.WriteLine("  implosion: target {0:0.0}, touching {1:0.0} (pulled {2:0.0} px), chained {3:0.0}", t1, t2, gap0 - i2.Pos.DistanceTo(d1.Pos), t3);
        Check(Math.Abs(t1 - 34f) < 0.5f && Math.Abs(t2 - 15f) < 0.5f && Math.Abs(t3 - 34f * 0.6f) < 0.5f && i2.Pos.DistanceTo(d1.Pos) < gap0 - 10f,
            "Implosion: 34 to the target, 15 to an enemy against it (pulled in), and an extra collapses another enemy for 60%");
        keep.Remove(i2); keep.Remove(i3); i2.Hp = i3.Hp = 0f;
        hp.Weapon = Defs.Weapon(Role.Tank, Kit.Hemomancer, 0);

        // Blood Lash: the tip cracks for double and taunts, closer in flings out to the tip, and every hit clots.
        keep.Remove(d1); d1.Hp = 0f;
        place(1, 300f); run(0.2f);
        hp.Hp = hp.MaxHp; hp.Clot = 0;
        var close = dummy(new V2(360f, 450f));
        var far = dummy(new V2(415f, 450f));
        close.TargetId = far.TargetId = 2;
        close.RetargetT = far.RetargetT = 10f;  // so only the crack retargets them
        float n0 = close.Hp, f0 = far.Hp;
        var fire = typeof(Game).GetMethod("FireVolley", flags);
        fire.Invoke(g, new object[] { hp, 0f, 0 });
        run(0.1f);
        float nearDmg = n0 - close.Hp, tipDmg = f0 - far.Hp, flung = close.Pos.DistanceTo(hp.Pos);
        bool clotSeen = client.Game.Players[1].Clot == 3;
        Console.WriteLine("  blood lash: close {0:0.0} (flung to {1:0} px), tip {2:0.0}; clot {3} (client saw it {4}); targets {5} {6}", nearDmg, flung, tipDmg, hp.Clot, clotSeen, close.TargetId, far.TargetId);
        Check(nearDmg > 0f && Math.Abs(tipDmg - nearDmg * Game.LashCrack) < 0.5f && far.TargetId == 1 && close.TargetId == 2
            && close.Pos.DistanceTo(hp.Pos) + close.Radius >= Game.MeleeReach(hp, hp.Weapon.Reach) * Game.LashTipFrom,
            "Blood Lash: the tip cracks for double damage and taunts; an enemy hit closer in is flung out to the tip");
        Check(hp.Clot == 3 && clotSeen, "each Blood Lash hit adds a Clot stack, two from the tip (and clients see it)");
        hp.DevGod = false;
        damage.Invoke(g, new object[] { hp, 100f / g.EnemyDamageScale(g.Wave), "test" });
        hp.DevGod = true;
        float clotTaken = hp.MaxHp - hp.Hp;
        Check(Math.Abs(clotTaken - 100f * (1f - 3f * Game.ClotCutEach)) < 0.5f, "each Clot stack takes 4% off the damage you take");
        hp.Clot = 5;  // full: the next lash bursts all round you
        var behind = dummy(new V2(240f, 450f));
        behind.TargetId = 2; behind.RetargetT = 10f;
        float b0 = behind.Hp;
        fire.Invoke(g, new object[] { hp, 0f, 0 });
        run(0.1f);
        Console.WriteLine("  clot burst: behind took {0:0.0}; clot now {1}", b0 - behind.Hp, hp.Clot);
        Check(b0 - behind.Hp > 0f && hp.Clot == 0 && behind.TargetId == 1, "at full Clot the next lash bursts all round you, spends it, and taunts");
        hp.Clot = 3; hp.ClotT = Game.ClotHold;
        run(Game.ClotHold + 0.6f);
        Check(hp.Clot == 1, "Clot holds for 3 s without hits, then fades a stack every 0.5 s");
        foreach (var e in new[] { close, far, behind }) { keep.Remove(e); e.Hp = 0f; }
        hp.Clot = 0;

        // (Blood Rite became Vein Tithe: tests in Tithe.cs.)

        s[0].Leave(""); s[1].Leave("");
    }
}
