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
            && Defs.WeaponGap(Role.Dps, Kit.Engineer, 1), "the Vector Forge is the DPS Engineer's weapon; its other places are locked");
        Check(Defs.Weapon(Role.Tank, Kit.Engineer, 0).Id == WeaponId.BulwarkForge && Defs.Weapon(Role.Healer, Kit.Engineer, 0).Id == WeaponId.MediDroneForge
            && Defs.Weapon(Role.Tank, Kit.Engineer, 2).Id == WeaponId.BulwarkForge && Defs.WeaponGap(Role.Tank, Kit.Engineer, 2),
            "the Tank and Healer Engineers have the Bulwark Forge and the Medi-Drone Forge (their own stand-ins fill the locked places)");
        Check(!Defs.KitLocked(Role.Tank, Kit.Engineer) && !Defs.KitLocked(Role.Healer, Kit.Engineer) && !Defs.KitLocked(Role.Dps, Kit.Engineer),
            "every role can pick the Engineer");
        Check(Upgrades.RoleAbility(Role.Tank, Kit.Engineer, 0) == AbilityId.VectorWall && Upgrades.Ultimate(Role.Tank, Kit.Engineer) == AbilityId.Bastion
            && Upgrades.RoleAbility(Role.Healer, Kit.Engineer, 0) == AbilityId.MedPylon && Upgrades.Ultimate(Role.Healer, Kit.Engineer) == AbilityId.SafeRoom
            && Upgrades.RoleAbility(Role.Tank, Kit.Engineer, 1) == AbilityId.VectorWall, "Vector Wall and Bastion for the Tank, Med Pylon and Safe Room for the Healer");
        Check(Upgrades.RoleAbility(Role.Dps, Kit.Engineer, 0) == AbilityId.DragPylon && Upgrades.AbilityGap(Role.Dps, Kit.Engineer, 1)
            && Upgrades.Ultimate(Role.Dps, Kit.Engineer) == AbilityId.Breach && !Upgrades.UltGap(Role.Dps, Kit.Engineer), "Drag Pylon on Space and Breach on the Ultimate row");
        var fresh = new Profile();
        Check(Unlocks.Get("class-engineer") != null && !fresh.EngineerUnlocked, "the Engineer is a locked class on a new profile (the lobby opens it for everyone for now)");
        fresh.RunsWon = 1;
        Check(fresh.EngineerUnlocked && Unlocks.Get("class-engineer").Earned(fresh), "winning a run unlocks it");
        Check(Upgrades.All.Count(u => u.Name == "Drag Pylon") == 1, "one Drag Pylon card, not one per place it stands in for");

        // A locked weapon key is refused: the lobby keeps the first.
        int tport = nextPort++;
        var tank = HostAs(new Session(), tport, false, "Tank", Role.Tank, 2, 1, Kit.Engineer);
        Check(tank.Lobby[1].Kit == Kit.Engineer && tank.Lobby[1].Weapon == 0 && tank.Lobby[1].Ability == 0, "a locked weapon or ability pick goes back to the first");
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
        step(Game.StriderRebuild + 0.4f);
        Check(strider() != null && strider().Wind >= 2f && strider().Wind < 3f, "and rebuilt after 8 s, building in first (Wind 2-3 for the look)");
        step(Game.StriderBuild);
        Check(strider().Wind < 2f, "then it's up");

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

        // Step 4 lives in its own match below.
        // Breach in the Expanding arena: a room opens behind the wall.
        foreach (var gg in new[] { g, client.Game }) gg.Diff.Set(DiffKnob.ArenaExpanding, 1f);
        step(0.2f);
        if (g.ExpandingOn)
        {
            UseMap(new[] { g, client.Game }, Expansion.GridCore());
            foreach (var gg in new[] { g, client.Game }) gg.RoomsUp = true;  // (the core is up)
            int rooms = g.Expanded.Count;
            mePos = new V2(1100f, -320f);  // (mid-cell, by the Grid core's top wall: the hub's own walls open into the core)
            me.Pos = mePos;
            me.Slots[Upgrades.UltSlot].Timer = 0f; me.Slots[Upgrades.UltSlot].Charges = 1;
            hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, new V2(1100f, -700f), false });
            Check(g.Expanded.Count > rooms, "Breach at the Expanding arena's wall opens a room behind it (" + (g.Expanded.Count - rooms) + " room pieces)");
            step(0.5f);
            Check(client.Game.Expanded.Count == g.Expanded.Count, "the client gets the new room");
            bool stash = g.Expanded.Skip(rooms).Any(r => r.Surprise == (byte)(1 + (int)SurpriseShape.Stash));
            var lootNode = g.Nodes.FindIndex(n => n.Kind == NodeKind.BreachLoot);
            Check(stash ? lootNode < 0 : lootNode >= 0 && !g.Nodes[lootNode].Spent, "the room holds its loot to walk up to (a Stash: its own vector cache)");
            Check(client.Game.Nodes.Count(n => n.Kind == NodeKind.BreachLoot) == g.Nodes.Count(n => n.Kind == NodeKind.BreachLoot), "the client sees the loot");
            // Walking up to it takes it (here a supply crate: 40 gold in coins).
            g.Nodes.Add(new MapNode { X = 700, Y = 700, Kind = NodeKind.BreachLoot, Rule = 1 });
            int coins = g.Coins.Count, gold = g.Players.Values.Sum(q => q.Gold);
            mePos = new V2(700f, 700f);
            step(0.2f);
            var taken = g.Nodes.Last(n => n.Kind == NodeKind.BreachLoot);
            Check(taken.Spent && (g.Coins.Count > coins || g.Players.Values.Sum(q => q.Gold) > gold), "walking up to the loot takes it (a supply crate drops its gold)");
            mePos = new V2(800f, 80f);

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
        EngineerTankHealerTest(dt);
    }

    // Step 4: the Tank Engineer (Bulwark Forge and its Sentinel, Vector Wall, Bastion) and the Healer Engineer (the med-drone,
    // Med Pylon, Safe Room).
    static void EngineerTankHealerTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Tank", Role.Tank, 0, 0, Kit.Engineer);
        var client = JoinAs(port, false, "Medic", Role.Healer, 0, 0, Kit.Engineer);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var tank = g.Players[1]; var medic = g.Players[2];
        Check(tank.Weapon.Id == WeaponId.BulwarkForge && tank.Slots[0].Id == AbilityId.VectorWall && tank.Slots[Upgrades.UltSlot].Id == AbilityId.Bastion
            && medic.Weapon.Id == WeaponId.MediDroneForge && medic.Slots[0].Id == AbilityId.MedPylon && medic.Slots[Upgrades.UltSlot].Id == AbilityId.SafeRoom,
            "a Tank and a Healer Engineer start with their own kits");
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        V2 tPos = new V2(500f, 450f), mPos = new V2(400f, 600f);
        Action<float> step = secs =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                tank.Pos = tPos; medic.Pos = mPos;
                foreach (var e in g.Enemies.Values) e.FireT = 99f;
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = new V2(900f, 450f) });
            }
        };
        Func<EnemyType, V2, Enemy> make = (type, at) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            e.Hp = e.MaxHp = 5000f; e.SpawnT = 0f; e.FireT = 99f;
            return e;
        };
        g.Enemies.Clear();
        make(EnemyType.Grunt, new V2(1500f, 880f));
        step(2f);
        var sentinel = g.Minions.Values.FirstOrDefault(m => m.Kind == MinionKind.Sentinel && m.Owner == 1);
        Check(sentinel != null && Math.Abs(sentinel.MaxHp - Game.StriderMaxHp(g.Wave, MinionKind.Sentinel)) < 1f, "the Bulwark Forge builds a Sentinel with " + Game.StriderMaxHp(g.Wave, MinionKind.Sentinel) + " health");
        Check(!g.Minions.Values.Any(m => m.Kind == MinionKind.Strider), "(no Strider: that is the DPS's)");
        var drone = g.Minions.Values.FirstOrDefault(m => m.Kind == MinionKind.MediDrone && m.Owner == 2);
        Check(drone != null, "the Medi-Drone Forge brings a med-drone");
        Check(client.Game.Minions.Values.Any(m => m.Kind == MinionKind.Sentinel) && client.Game.Minions.Values.Any(m => m.Kind == MinionKind.MediDrone), "the client sees both");

        // The Sentinel taunts from 220 px.
        var striderTarget = typeof(Game).GetMethod("StriderTarget", flags);
        var far = make(EnemyType.Grunt, sentinel.Pos + new V2(200f, 0f));
        Check(((V2)striderTarget.Invoke(g, new object[] { far, new V2(1500f, 900f) })).DistanceTo(sentinel.Pos) < 1f, "an enemy 200 px from the Sentinel goes for it");
        g.Enemies.Remove(far.Id);

        // The nail shotgun: six nails a shot.
        var fire = typeof(Game).GetMethod("FireEngineerWeapon", flags);
        int before = g.Bullets.Count;
        fire.Invoke(g, new object[] { tank, 0f, new V2(1f, 0f), tank.Pos + new V2(20f, 0f), 1f, 0 });
        Check(g.Bullets.Count - before == 6, "the Bulwark Forge fires six nails");

        // The med-drone heals the hurt Tank.
        tank.Hp = tank.MaxHp * 0.5f;
        float hurt = tank.Hp;
        step(3f);
        Check(tank.Hp > hurt + 10f, "the med-drone flies to the hurt ally and heals it (" + (tank.Hp - hurt).ToString("0") + ")");

        // Vector Wall: five tiles across the aim that stop enemy shots.
        g.Bullets.Clear();
        tank.Slots[0].Timer = 0f; tank.Slots[0].Charges = 1;
        hostAbility.Invoke(g, new object[] { 1, 0, new V2(700f, 450f), false });
        var tiles = g.Minions.Values.Where(m => m.Kind == MinionKind.VectorBlock && m.Owner == 1).ToList();
        Check(tiles.Count == Game.VWallTiles && tiles.All(m => Math.Abs(m.Pos.X - 700f) < 1f), "Vector Wall draws five tiles across your aim at the cursor");
        var shot = (Bullet)shoot.Invoke(g, new object[] { Defs.SmallShot, new V2(800f, 450f), new V2(-500f, 0f), 0f, 3f, 10f, 0, false, 0f });
        step(0.4f);
        Check(!g.Bullets.Values.Contains(shot) && tiles.Any(m => m.BlockHp < m.MaxHp || !g.Minions.ContainsKey(m.Id)), "an enemy shot stops on the wall");
        step(Game.VWallLife);
        Check(!g.Minions.Values.Any(m => m.Kind == MinionKind.VectorBlock && m.Owner == 1), "the wall is gone after 8 s");

        // Bastion: a ring of 12 tiles (16 places, 4 gaps).
        tank.Slots[Upgrades.UltSlot].Timer = 0f; tank.Slots[Upgrades.UltSlot].Charges = 1;
        hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, new V2(900f, 450f), false });
        var ring = g.Minions.Values.Where(m => m.Kind == MinionKind.VectorBlock && m.Owner == 1).ToList();
        Check(ring.Count == Game.BastionTiles - Game.BastionGaps && ring.All(m => Math.Abs(m.Pos.DistanceTo(tPos) - Game.BastionRadius) < 2f), "Bastion rings you with 12 tiles, four gaps");

        // Med Pylon: heals allies inside; one at a time.
        tank.Hp = tank.MaxHp * 0.5f;
        float h0 = tank.Hp;
        medic.Slots[0].Timer = 0f; medic.Slots[0].Charges = 1;
        hostAbility.Invoke(g, new object[] { 2, 0, tPos, false });
        g.Minions.Remove(drone.Id);  // (only the pylon heals here)
        step(1f);
        Check(g.Zones.Values.Count(z => z.Kind == ZoneKind.MedPylon) == 1 && tank.Hp > h0 + 5f, "Med Pylon's field heals allies inside");

        // Safe Room with no wall in reach: a healing dome that cuts damage.
        medic.Slots[Upgrades.UltSlot].Timer = 0f; medic.Slots[Upgrades.UltSlot].Charges = 1;
        hostAbility.Invoke(g, new object[] { 2, Upgrades.UltSlot, tPos, false });
        var safe = g.Zones.Values.FirstOrDefault(z => z.Kind == ZoneKind.SafeRoom);
        Check(safe != null && safe.Radius == Game.SafeDomeRadius && safe.DamageCut == Game.SafeRoomCut && Math.Abs(safe.Duration - Game.SafeRoomLife) < 0.01f,
            "Safe Room with no wall: a 160 px dome for 12 s that cuts damage by 30%");
        host.Leave(""); client.Leave("");
    }
}
