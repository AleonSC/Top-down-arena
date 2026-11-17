using System;
using System.Linq;
using Arena;

// The Engineer (Engineer.cs, Docs/Engineer.md step 1): a DPS-only class (its other lobby places are locked stand-ins), the Vector
// Forge's Strider (built at your side, hunts and punches, Alt locks it on, rebuilt after it falls, replicated), the Drag Pylon
// (slows inside, the next one folds the old) and Breach (the blast, and a room behind the wall in the Expanding arena).
static partial class Program
{
    static void EngineerTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var dash = typeof(Game).GetMethod("HostWeaponDash", flags);
        var hurtGuard = typeof(Game).GetMethod("HurtGuard", flags);

        // The table: a DPS-only class, its other places locked stand-ins.
        Check(Defs.Weapon(Role.Dps, Kit.Engineer, 0).Id == WeaponId.VectorForge && !Defs.WeaponGap(Role.Dps, Kit.Engineer, 0)
            && Defs.WeaponGap(Role.Dps, Kit.Engineer, 1) && Defs.WeaponGap(Role.Tank, Kit.Engineer, 0), "the Vector Forge is the DPS Engineer's weapon; its other places are locked");
        Check(Defs.KitLocked(Role.Tank, Kit.Engineer) && Defs.KitLocked(Role.Healer, Kit.Engineer) && !Defs.KitLocked(Role.Dps, Kit.Engineer) && !Defs.KitLocked(Role.Tank, Kit.Soldier),
            "the Engineer is DPS only for now");
        Check(Upgrades.RoleAbility(Role.Dps, Kit.Engineer, 0) == AbilityId.DragPylon && Upgrades.AbilityGap(Role.Dps, Kit.Engineer, 1)
            && Upgrades.Ultimate(Role.Dps, Kit.Engineer) == AbilityId.Breach && !Upgrades.UltGap(Role.Dps, Kit.Engineer), "Drag Pylon on Space and Breach on the Ultimate row");
        Check(Upgrades.All.Count(u => u.Name == "Drag Pylon") == 1, "one Drag Pylon card, not one per place it stands in for");

        // A Tank asking for the Engineer stays a Soldier.
        int tport = nextPort++;
        var tank = HostAs(new Session(), tport, false, "Tank", Role.Tank, 0, 0, Kit.Engineer);
        Check(tank.Lobby[1].Kit == Kit.Soldier, "a Tank can't pick the Engineer yet (the lobby keeps the Soldier)");
        tank.Leave("");

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Eng", Role.Dps, 0, 0, Kit.Engineer);
        var client = JoinAs(port, false, "Friend", Role.Healer, 0, 0, Kit.Knight);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        Check(me.Kit == Kit.Engineer && me.Weapon.Id == WeaponId.VectorForge && me.Slots[0].Id == AbilityId.DragPylon && me.Slots[Upgrades.UltSlot].Id == AbilityId.Breach,
            "a DPS Engineer starts with the Vector Forge, Drag Pylon and Breach");
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        V2 mePos = new V2(500f, 450f);
        Action<float> step = secs =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                me.Pos = mePos;
                foreach (var e in g.Enemies.Values) e.FireT = 99f;
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = new V2(900f, 450f) });
            }
        };
        Func<Minion> strider = () => g.Minions.Values.FirstOrDefault(m => m.Kind == MinionKind.Strider && m.Owner == 1);
        Func<EnemyType, V2, Enemy> make = (type, at) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            e.Hp = e.MaxHp = 5000f; e.SpawnT = 0f; e.FireT = 99f;
            return e;
        };

        // The Strider: built at your side, with health that grows with the wave, and seen by the client.
        g.Enemies.Clear();
        make(EnemyType.Grunt, new V2(1500f, 880f));  // (keeps the wave going)
        step(1.5f);
        var st = strider();
        Check(st != null && Math.Abs(st.MaxHp - Game.StriderMaxHp(g.Wave)) < 1f && st.Hp > 0f, "the Strider is built with " + Game.StriderMaxHp(g.Wave) + " health on wave " + g.Wave);
        Check(client.Game.Minions.Values.Any(m => m.Kind == MinionKind.Strider), "the client sees the Strider");

        // It hunts the enemy nearest you and punches it.
        var near = make(EnemyType.Grunt, new V2(640f, 450f));
        float before = near.Hp;
        step(3f);
        Check(near.Hp < before - 30f, "the Strider walks to the enemy near you and punches it (" + (before - near.Hp).ToString("0") + " damage)");

        // Alt on an enemy locks it on: it goes there, through the nearer one.
        var far = make(EnemyType.Grunt, new V2(800f, 250f));
        dash.Invoke(g, new object[] { 1, 43, far.Pos });
        step(0.1f);
        st = strider();
        Check(st.GoalEnemy == far.Id && st.Stacks == 1 && st.LinkTo == far.Id, "Alt on an enemy locks the Strider on (replicated: its mode and target)");
        float farBefore = far.Hp;
        step(3f);
        Check(far.Hp < farBefore, "locked on, it goes for that enemy");
        // Alt on the floor away from you posts it there; by you calls it back.
        dash.Invoke(g, new object[] { 1, 43, new V2(300f, 800f) });
        step(0.1f);
        Check(strider().Sent && strider().GoalEnemy == 0 && strider().Stacks == 2, "Alt on the floor posts it there");
        dash.Invoke(g, new object[] { 1, 43, mePos });
        step(0.1f);
        Check(!strider().Sent, "Alt by you calls it back");

        // Enemies near it go for it.
        var striderTarget = typeof(Game).GetMethod("StriderTarget", flags);
        var drawn = make(EnemyType.Grunt, strider().Pos + new V2(80f, 0f));
        Check(((V2)striderTarget.Invoke(g, new object[] { drawn, new V2(1500f, 450f) })).DistanceTo(strider().Pos) < 1f
            && ((V2)striderTarget.Invoke(g, new object[] { near, new V2(1500f, 450f) })).DistanceTo(new V2(1500f, 450f)) < 1f || near.Pos.DistanceTo(strider().Pos) < Game.StriderTaunt,
            "an enemy within 150 px of the Strider goes for it instead of you");
        g.Enemies.Remove(drawn.Id);

        // It falls and is rebuilt after 8 s.
        hurtGuard.Invoke(g, new object[] { strider(), 1e6f });
        Check(strider() == null && Math.Abs(me.StriderCd - Game.StriderRebuild) < 0.01f, "a broken Strider is gone, its rebuild 8 s away");
        step(Game.StriderRebuild + 0.5f);
        Check(strider() != null, "and rebuilt after 8 s");

        // Drag Pylon: slows enemies inside; the next one folds the old one up.
        g.Enemies.Clear();
        make(EnemyType.Grunt, new V2(1500f, 880f));
        var slowed = make(EnemyType.Grunt, new V2(700f, 300f));
        me.Slots[0].Timer = 0f; me.Slots[0].Charges = 1;
        hostAbility.Invoke(g, new object[] { 1, 0, new V2(700f, 300f), false });
        step(0.1f);
        var pylons = g.Zones.Values.Where(z => z.Kind == ZoneKind.DragPylon).ToList();
        Check(pylons.Count == 1 && pylons[0].Radius == Game.PylonRadius, "Drag Pylon plants a 130 px field at the cursor");
        Check(slowed.SlowT > 0f && Math.Abs(slowed.SlowMult - Game.PylonSlow) < 0.01f, "enemies inside are slowed 45%");
        Check(client.Game.Zones.Values.Any(z => z.Kind == ZoneKind.DragPylon), "the client sees the pylon");
        me.Slots[0].Timer = 0f; me.Slots[0].Charges = 1;
        hostAbility.Invoke(g, new object[] { 1, 0, new V2(400f, 700f), false });
        step(0.2f);
        pylons = g.Zones.Values.Where(z => z.Kind == ZoneKind.DragPylon).ToList();
        Check(pylons.Count == 1 && pylons[0].Pos.DistanceTo(new V2(400f, 700f)) < 1f, "the next pylon folds the old one up: one at a time");
        me.Slots[0].Timer = 0f; me.Slots[0].Charges = 1;
        hostAbility.Invoke(g, new object[] { 1, 0, new V2(1400f, 450f), false });
        step(0.1f);
        pylons = g.Zones.Values.Where(z => z.Kind == ZoneKind.DragPylon).ToList();
        Check(pylons.Count == 1 && pylons[0].Pos.DistanceTo(mePos) <= Game.PylonReach + 1f, "a pylon lands at most 320 px away");

        // Breach in the open: the blast lands at the cursor (no wall in reach).
        g.Enemies.Clear();
        make(EnemyType.Grunt, new V2(1500f, 880f));
        var hit = make(EnemyType.Grunt, new V2(690f, 450f));
        float hitBefore = hit.Hp;
        me.Slots[Upgrades.UltSlot].Timer = 0f; me.Slots[Upgrades.UltSlot].Charges = 1;
        hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, new V2(700f, 450f), false });
        Check(hit.Hp < hitBefore - 50f && g.Effects.Any(fx => fx.Kind == FxKind.BreachSmash), "Breach with no wall in reach: the hammer's blast lands at the cursor");

        // Breach in the Expanding arena: a room opens behind the wall.
        foreach (var gg in new[] { g, client.Game }) gg.Diff.Set(DiffKnob.ArenaExpanding, 1f);
        step(0.2f);
        if (g.ExpandingOn)
        {
            int rooms = g.Expanded.Count;
            mePos = new V2(800f, 80f);
            me.Pos = mePos;
            me.Slots[Upgrades.UltSlot].Timer = 0f; me.Slots[Upgrades.UltSlot].Charges = 1;
            hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, new V2(800f, -300f), false });
            Check(g.Expanded.Count > rooms, "Breach at the Expanding arena's wall opens a room behind it (" + (g.Expanded.Count - rooms) + " room pieces)");
            step(0.5f);
            Check(client.Game.Expanded.Count == g.Expanded.Count, "the client gets the new room");

            // Draftsman (step 2): +25% vectors from every gain, part-vectors carried over.
            var addVectors = typeof(Game).GetMethod("AddVectors", flags);
            me.VectorCap = 500; me.Vectors = 0; me.DraftCarry = 0f;
            addVectors.Invoke(g, new object[] { me, 8 });
            Check(me.Vectors == 10, "Draftsman: 8 vectors come in as 10");
            addVectors.Invoke(g, new object[] { me, 1 }); addVectors.Invoke(g, new object[] { me, 1 }); addVectors.Invoke(g, new object[] { me, 1 }); addVectors.Invoke(g, new object[] { me, 1 });
            Check(me.Vectors == 15, "four single vectors come in as five (the quarters add up)");
            var friend = g.Players[2];
            friend.VectorCap = 500; friend.Vectors = 0;
            addVectors.Invoke(g, new object[] { friend, 8 });
            Check(friend.Vectors == 8, "only the Engineer gets Draftsman's bonus");

            // Repairs: standing by your hurt Strider spends a vector every 0.25 s for 30 health.
            var rs = strider();
            mePos = rs.Pos + new V2(40f, 0f);
            rs.Sent = true; rs.Goal = rs.Pos; g.Enemies.Clear(); make(EnemyType.Grunt, new V2(1500f, 880f));
            rs.Hp = rs.MaxHp - 100f;
            int vBefore = me.Vectors;
            step(1.2f);
            Check(rs.Hp >= rs.MaxHp - 1f && me.Vectors < vBefore && me.Vectors >= vBefore - 5, "standing by the hurt Strider repairs it with vectors (" + (vBefore - me.Vectors) + " spent for 100 health)");
            int vFull = me.Vectors;
            step(0.6f);
            Check(me.Vectors == vFull, "a whole Strider costs nothing");

            // The class cards.
            Func<string, UpgradeDef> card = n => Upgrades.All.First(u => u.Name == n);
            Check(new[] { "Reinforced Frame", "Hydraulic Fists", "Quick Assembly", "Wide Field" }.All(n => Upgrades.All.Count(u => u.Name == n) == 1), "the Engineer's four class cards");
            var stats = new Stats();
            card("Reinforced Frame").Apply(stats); card("Quick Assembly").Apply(stats); card("Wide Field").Apply(stats);
            Check(Math.Abs(stats.StriderHpMult - 1.3f) < 0.001f && Math.Abs(stats.StriderRebuildCut - 2.5f) < 0.001f && Math.Abs(stats.PylonSizeMult - 1.25f) < 0.001f,
                "Reinforced Frame +30% Strider health, Quick Assembly 2.5 s sooner, Wide Field 25% wider");
        }
        else
            Check(false, "the Expanding arena switches on for the Breach room check");
        host.Leave(""); client.Leave("");
    }
}
