using System;
using System.Linq;
using Arena;

// The Engineer (Engineer.cs, Docs/Engineer.md): one robot weapon a role (it stands in for the locked lobby places), the Drag Pylon
// (slows inside, the next one folds the old), Breach (the blast, and a room behind the wall in the Expanding arena), Draftsman,
// and the Tank and Healer abilities (Vector Wall, Bastion, Med Pylon, Safe Room).
static partial class Program
{
    static void EngineerTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);

        // The table: one robot weapon a role, standing in for the class's other (locked) places; the forges are gone.
        Check(Defs.Weapon(Role.Dps, Kit.Engineer, 0).Id == WeaponId.VectorBlade && Defs.Weapon(Role.Tank, Kit.Engineer, 0).Id == WeaponId.IronFist
            && Defs.Weapon(Role.Healer, Kit.Engineer, 0).Id == WeaponId.StasisGun && !Defs.WeaponGap(Role.Dps, Kit.Engineer, 0)
            && !Defs.WeaponGap(Role.Dps, Kit.Engineer, 1) && Defs.WeaponGap(Role.Dps, Kit.Engineer, 2) && Defs.WeaponGap(Role.Tank, Kit.Engineer, 2) && Defs.Weapon(Role.Tank, Kit.Engineer, 2).Id == WeaponId.IronFist,
            "each Engineer role's first weapon is its robot weapon, which fills the locked places");
        Check(!Enum.GetNames(typeof(WeaponId)).Any(n => n.EndsWith("Forge")) && !Enum.GetNames(typeof(MinionKind)).Any(n => n == "Strider" || n == "Sentinel" || n == "MediDrone"),
            "the Vector, Bulwark and Medi-Drone Forges and their Strider, Sentinel and med-drone are gone");
        Check(!Defs.KitLocked(Role.Tank, Kit.Engineer) && !Defs.KitLocked(Role.Healer, Kit.Engineer) && !Defs.KitLocked(Role.Dps, Kit.Engineer),
            "every role can pick the Engineer");
        Check(Upgrades.FirstAbility(Role.Tank, Kit.Engineer, 0) == AbilityId.VectorWall && Upgrades.Ultimate(Role.Tank, Kit.Engineer) == AbilityId.Bastion
            && Upgrades.FirstAbility(Role.Healer, Kit.Engineer, 0) == AbilityId.MedPylon && Upgrades.Ultimate(Role.Healer, Kit.Engineer) == AbilityId.SafeRoom
            && Upgrades.FirstAbility(Role.Tank, Kit.Engineer, 1) == AbilityId.VectorWall, "Vector Wall and Bastion for the Tank, Med Pylon and Safe Room for the Healer");
        Check(Upgrades.FirstAbility(Role.Dps, Kit.Engineer, 0) == AbilityId.DragPylon && Upgrades.AbilityGap(Role.Dps, Kit.Engineer, 1)
            && Upgrades.Ultimate(Role.Dps, Kit.Engineer) == AbilityId.Breach && !Upgrades.UltGap(Role.Dps, Kit.Engineer), "Drag Pylon as the 1st ability and Breach on the Ultimate row");
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
        Check(me.Kit == Kit.Engineer && me.Weapon.Id == WeaponId.VectorBlade && me.Slots[0].Id == AbilityId.DragPylon && me.Slots[Upgrades.UltSlot].Id == AbilityId.Breach,
            "a DPS Engineer starts with the Vector Blade, Drag Pylon and Breach");
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
        Func<EnemyType, V2, Enemy> make = (type, at) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            e.Hp = e.MaxHp = 5000f; e.SpawnT = 0f; e.FireT = 99f;
            return e;
        };

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

            // The class card.
            Check(Upgrades.All.Count(u => u.Name == "Wide Field") == 1 && !Upgrades.All.Any(u => u.Name == "Reinforced Frame" || u.Name == "Hydraulic Fists" || u.Name == "Quick Assembly"),
                "the Engineer's one class card (the Strider's three are gone)");
            var stats = new Stats();
            Upgrades.All.First(u => u.Name == "Wide Field").Apply(stats);
            Check(Math.Abs(stats.PylonSizeMult - 1.25f) < 0.001f, "Wide Field: the pylon's field 25% wider");
        }
        else
            Check(false, "the Expanding arena switches on for the Breach room check");
        host.Leave(""); client.Leave("");
        EngineerTankHealerTest(dt);
    }

    // The Tank Engineer (Vector Wall, Bastion) and the Healer Engineer (Med Pylon, Safe Room).
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
        Check(tank.Weapon.Id == WeaponId.IronFist && tank.Slots[0].Id == AbilityId.VectorWall && tank.Slots[Upgrades.UltSlot].Id == AbilityId.Bastion
            && medic.Weapon.Id == WeaponId.StasisGun && medic.Slots[0].Id == AbilityId.MedPylon && medic.Slots[Upgrades.UltSlot].Id == AbilityId.SafeRoom,
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
        step(0.5f);

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
