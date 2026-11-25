using System;
using System.Linq;
using Arena;

// Gadgets (Core/Gadgets.cs): the Magnet lies on the floor, a player walks over it, switches it on (every coin on the map flies to
// them), throws it to a teammate who catches it with the time it had left, switches it on over the network until it's used up;
// going down drops it. The bag, the Lodestone's uses, the Energy Drink, the Shield Generator, the Medkit and the throwables.
static partial class Program
{
    static void GadgetTest(float dt)
    {
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2 }, port, false, "Host", Role.Tank);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game; var cg = client.Game;
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        Action Quiet = () =>
        {
            foreach (var e in g.Enemies.Values) e.Hp = 0f;
            foreach (var q in g.Bullets.Values.Where(q => Defs.IsEnemyShot(q.Kind)).ToList()) g.Bullets.Remove(q.Id);
            foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = p.PendingCards = 0; p.LegendaryQueue.Clear(); p.Offer = null; }
        };
        V2 a = new V2(500f, 450f), b = new V2(800f, 450f);
        Action Put = () => { g.Players[1].Pos = g.Players[1].Disp = a; cg.Players[2].Pos = cg.Players[2].Disp = b; g.Players[2].Pos = b; };
        Put();
        Run(s, dt, 0.4f, null, Quiet);

        // On the floor: both screens see it; walking over it picks it up.
        var mag = g.SpawnGadget(GadgetId.Magnet, new V2(500f, 650f), Game.MagnetTime);
        Run(s, dt, 0.4f, null, () => { Quiet(); Put(); });
        Check(cg.Gadgets.Count == 1 && cg.Gadgets.Values.First().Kind == GadgetId.Magnet && g.Players[1].Gadget == GadgetId.None, "a Magnet on the floor shows on both screens and nobody holds it yet");
        a = new V2(500f, 640f);
        Run(s, dt, 0.4f, null, () => { Quiet(); Put(); });
        Check(g.Gadgets.Count == 0 && cg.Gadgets.Count == 0 && g.Players[1].Gadget == GadgetId.Magnet && cg.Players[1].Gadget == GadgetId.Magnet && !g.Players[1].GadgetOn,
            "walking over it picks it up (both screens), switched off");

        // Switched on: coins far out of the 90 px reach fly in.
        g.Players[1].Gold = 0;
        for (int i = 0; i < 6; i++)
            g.Coins[5000 + i] = new Coin { Id = 5000 + i, Value = 5, Pos = new V2(1300f, 150f + i * 40f), Disp = new V2(1300f, 150f + i * 40f) };
        Run(s, dt, 0.5f, null, () => { Quiet(); Put(); });
        Check(g.Coins.Count == 6 && g.Players[1].Gold == 0, "switched off, far coins stay put");
        g.UseGadget();
        Run(s, dt, 3f, () => g.Coins.Count == 0, () => { Quiet(); Put(); });
        float left = g.Players[1].GadgetLeft;
        Check(g.Coins.Count == 0 && g.Players[1].Gold == 30 && g.Players[1].GadgetOn && cg.Players[1].GadgetOn && left < Game.MagnetTime && left > Game.MagnetTime - 3.5f,
            "switched on, every coin on the map flies to the holder (" + g.Players[1].Gold + "/" + g.Players[2].Gold + " gold, " + g.Coins.Count + " coins, " + left.ToString("0.0") + " s left)");
        g.UseGadget();
        Run(s, dt, 0.5f, null, () => { Quiet(); Put(); });
        left = g.Players[1].GadgetLeft;
        Check(!g.Players[1].GadgetOn && !cg.Players[1].GadgetOn, "a second tap switches it off, saving the rest");
        Run(s, dt, 0.5f, null, () => { Quiet(); Put(); });
        Check(Math.Abs(g.Players[1].GadgetLeft - left) < 0.001f, "switched off, its time doesn't run");

        // Thrown to the teammate: they catch it with the time it had left.
        g.ThrowGadget(b);
        Run(s, dt, 0.05f, null, () => { Quiet(); Put(); });
        Check(g.Players[1].Gadget == GadgetId.None && g.Gadgets.Count == 1 && g.Gadgets.Values.First().Flying, "thrown: out of the hand and in the air");
        Run(s, dt, 1f, () => g.Players[2].Gadget == GadgetId.Magnet, () => { Quiet(); Put(); });
        Check(g.Players[2].Gadget == GadgetId.Magnet && Math.Abs(g.Players[2].GadgetLeft - left) < 0.001f && g.Gadgets.Count == 0, "the teammate catches it with the time it had left");
        Run(s, dt, 0.3f, null, () => { Quiet(); Put(); });
        Check(cg.Players[2].Gadget == GadgetId.Magnet && cg.Me.Gadget == GadgetId.Magnet, "...and their own screen knows they hold it");

        // The client switches it on over the network and uses it up.
        cg.UseGadget();
        Run(s, dt, 0.5f, null, () => { Quiet(); Put(); });
        Check(g.Players[2].GadgetOn, "a client switches it on (Msg.Gadget)");
        Run(s, dt, Game.MagnetTime, () => g.Players[2].Gadget == GadgetId.None, () => { Quiet(); Put(); });
        Run(s, dt, 0.3f, null, () => { Quiet(); Put(); });
        Check(g.Players[2].Gadget == GadgetId.None && cg.Players[2].Gadget == GadgetId.None && g.Gadgets.Count == 0, "used up, it's gone");

        // Throwing past the range lands at the range; going down drops it.
        g.Players[1].Gadget = GadgetId.Magnet; g.Players[1].GadgetLeft = 4f;
        g.ThrowGadget(a + new V2(0f, -2000f));
        Run(s, dt, 1f, null, () => { Quiet(); Put(); });
        var land = g.Gadgets.Values.FirstOrDefault();
        Check(land != null && !land.Flying && Math.Abs(land.Pos.DistanceTo(a) - Game.GadgetThrowRange) < 2f && g.Players[1].Gadget == GadgetId.None, "a long throw lands at the throw range, and the thrower doesn't snatch it back");
        g.Gadgets.Clear();
        g.Players[2].Gadget = GadgetId.Magnet; g.Players[2].GadgetLeft = 5f; g.Players[2].GadgetOn = true;
        g.DevSetGod(2, false);
        g.Players[2].Hp = 0f; g.Players[2].Downed = true;
        Run(s, dt, 0.3f, null, Quiet);
        Check(g.Players[2].Gadget == GadgetId.None && g.Gadgets.Count == 1 && g.Gadgets.Values.First().Left > 4.5f, "going down drops it at your feet, switched off, with its time");

        // Drops: an Expanding arena kill drops one now and then, never more than one per player.
        int max = 0;
        var drop = typeof(Game).GetMethod("DropGadget", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var e0 = g.Enemies.Values.FirstOrDefault();
        if (e0 == null)
        {
            var spawn = typeof(Game).GetMethod("SpawnEnemyAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            e0 = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, new V2(900f, 300f), 0f, 0, false });
        }
        g.Gadgets.Clear();
        for (int i = 0; i < 2000; i++)
        {
            drop.Invoke(g, new object[] { e0 });
            max = Math.Max(max, g.Gadgets.Count);
        }
        Check(max >= 1 && g.Gadgets.Values.GroupBy(x => x.Owner * 100 + (int)x.Kind).All(x => x.Count() == 1), "kills drop gadgets now and then, at most one of each kind per player (" + max + ")");
        Check(g.Gadgets.Values.Select(x => x.Kind).Distinct().Count() >= 6 && !g.Gadgets.Values.Any(x => x.Kind == GadgetId.Lodestone), "every kind but the Lodestone drops from kills (" + g.Gadgets.Values.Select(x => x.Kind).Distinct().Count() + " kinds)");

        // The bag: a second gadget goes in the bag, the wheel's pick swaps it into the hand (over the network too), a used-up one is replaced from the bag.
        var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        g.Gadgets.Clear();
        var p2 = g.Players[2];
        p2.Downed = false; p2.Hp = p2.MaxHp;
        p2.Gadget = GadgetId.None; p2.GadgetOn = false;
        for (int i = 0; i < p2.BagKind.Length; i++) p2.BagKind[i] = GadgetId.None;
        var give = typeof(Game).GetMethod("GiveGadget", bf);
        Check((bool)give.Invoke(g, new object[] { p2, GadgetId.Magnet, 20f }) && p2.Gadget == GadgetId.Magnet && p2.GadgetLeft == 20f, "the first gadget goes in the hand");
        Check((bool)give.Invoke(g, new object[] { p2, GadgetId.Lodestone, Game.LodestoneChannel }) && p2.BagKind[0] == GadgetId.Lodestone, "the second goes in the bag");
        for (int i = 0; i < 2; i++) give.Invoke(g, new object[] { p2, GadgetId.Lodestone, Game.LodestoneChannel });
        give.Invoke(g, new object[] { p2, GadgetId.Magnet, 7f });
        Check(!(bool)give.Invoke(g, new object[] { p2, GadgetId.Magnet, 1f }) && !Game.CanHoldGadget(p2), "a full hand and bag takes no more");
        Run(s, dt, 0.5f, null, () => { Quiet(); Put(); });
        Check(cg.Players[2].BagKind[0] == GadgetId.Lodestone && cg.Players[2].BagKind[3] == GadgetId.Magnet && Math.Abs(cg.Players[2].BagLeft[3] - 7f) < 0.01f, "the bag reaches the client's screen");
        cg.EquipGadget(3);
        Run(s, dt, 0.5f, null, () => { Quiet(); Put(); });
        Check(p2.Gadget == GadgetId.Magnet && Math.Abs(p2.GadgetLeft - 7f) < 0.01f && p2.BagKind[3] == GadgetId.Magnet && Math.Abs(p2.BagLeft[3] - 20f) < 0.01f, "a client swaps a bag slot into the hand (Msg.Gadget 2), the old one takes its slot with its time");
        p2.GadgetOn = true; p2.GadgetLeft = 0.05f;
        Run(s, dt, 0.3f, null, () => { Quiet(); Put(); });
        Check(p2.Gadget == GadgetId.Lodestone && p2.BagKind[0] == GadgetId.None && !p2.GadgetOn, "a used-up gadget is replaced from the bag");

        // The Lodestone: refused at home; a hit breaks the cast; finishing it sets you down in the hub and spends one of its 3 uses.
        p2.GadgetLeft = Game.LodestoneChannel;
        cg.UseGadget();
        Run(s, dt, 0.3f, null, () => { Quiet(); Put(); });
        Check(!p2.GadgetOn, "the Lodestone won't cast in the hub");
        Check(p2.GadgetUses == Game.LodestoneUses && cg.Players[2].GadgetUses == Game.LodestoneUses, "a Lodestone has " + Game.LodestoneUses + " uses (on the client's screen too)");
        p2.GadgetOn = true; p2.GadgetLeft = 2f;
        typeof(Game).GetMethod("Damage", bf).Invoke(g, new object[] { p2, 1f, "Test" });
        Check(!p2.GadgetOn && p2.GadgetLeft == Game.LodestoneChannel && p2.GadgetUses == Game.LodestoneUses, "a hit breaks the cast and restarts the 3 s, no use spent");
        p2.Hp = p2.MaxHp;
        p2.GadgetOn = true; p2.GadgetLeft = 0.05f;
        g.DevSetGod(2, true);
        Run(s, dt, 0.3f, null, () => { Quiet(); });
        Check(p2.Gadget == GadgetId.Lodestone && p2.GadgetUses == Game.LodestoneUses - 1 && !p2.GadgetOn && p2.GadgetLeft == Game.LodestoneChannel, "a finished cast spends a use; the Lodestone stays in hand, ready again");
        Check(p2.Pos.DistanceTo(new V2(Game.Width / 2f, Game.Height / 2f)) < 160f, "and sets you down by the hub's middle");
        p2.GadgetUses = 1; p2.GadgetOn = true; p2.GadgetLeft = 0.05f;
        for (int i = 0; i < p2.BagKind.Length; i++) p2.BagKind[i] = GadgetId.None;
        Run(s, dt, 0.3f, null, () => { Quiet(); });
        Check(p2.Gadget != GadgetId.Lodestone, "its last use spent, it's gone");

        // The Energy Drink: switched on, a big speed boost (on the client's screen too) that runs down like the Magnet; off, normal speed.
        p2.Gadget = GadgetId.EnergyDrink; p2.GadgetLeft = Game.EnergyTime; p2.GadgetOn = false;
        Run(s, dt, 0.2f, null, () => { Quiet(); Put(); });
        float baseSpeed = p2.Speed;
        cg.UseGadget();
        Run(s, dt, 0.5f, null, () => { Quiet(); Put(); });
        Check(p2.GadgetOn && Math.Abs(p2.Speed - baseSpeed * Game.EnergySpeed) < 0.01f && p2.GadgetLeft < Game.EnergyTime, "the Energy Drink switched on: " + Game.EnergySpeed + "x move speed, its minute running down");
        Check(cg.Players[2].GadgetOn && Math.Abs(cg.Players[2].Speed - p2.Speed) < 0.01f, "...and the client moves at the same speed");
        cg.UseGadget();
        Run(s, dt, 0.3f, null, () => { Quiet(); Put(); });
        Check(!p2.GadgetOn && Math.Abs(p2.Speed - baseSpeed) < 0.01f, "switched off: normal speed, the rest saved");
        p2.GadgetOn = true; p2.GadgetLeft = 0.05f;
        Run(s, dt, 0.3f, null, () => { Quiet(); Put(); });
        Check(p2.Gadget != GadgetId.EnergyDrink && Math.Abs(p2.Speed - baseSpeed) < 0.01f, "a finished Energy Drink is used up");

        // The newer gadgets, on the host's own player.
        var p1 = g.Players[1];
        Action Clear = () =>
        {
            foreach (var q in g.Players.Values)
            {
                q.Gadget = GadgetId.None; q.GadgetOn = false;
                for (int i = 0; i < q.BagKind.Length; i++) q.BagKind[i] = GadgetId.None;
            }
            g.Gadgets.Clear();
        };
        Action<Player, GadgetId> Hold = (q, k) => { q.Gadget = k; q.GadgetLeft = Game.GadgetTime(k); q.GadgetUses = Game.GadgetUses(k); q.GadgetOn = false; q.GadgetCd = 0f; };
        var spawnBullet = typeof(Game).GetMethods(bf).First(m => m.Name == "SpawnBullet" && m.GetParameters().Length == 9);
        Func<V2, Bullet> EnemyShot = at => (Bullet)spawnBullet.Invoke(g, new object[] { (byte)10, at, default(V2), 0f, 5f, 1f, 0, false, 0f });
        Action Calm = () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = p.PendingCards = 0; p.LegendaryQueue.Clear(); p.Offer = null; } Put(); };

        // The Shield Generator: up, it wipes out the enemy bullets inside its dome; down, they stay.
        Clear(); Quiet(); Hold(p1, GadgetId.ShieldGenerator);
        g.UseGadget();
        var shot = EnemyShot(a + new V2(Game.ShieldGenR - 14f, 0f));
        Run(s, dt, 0.15f, null, Calm);
        Check(p1.GadgetOn && !g.Bullets.ContainsKey(shot.Id), "the Shield Generator up: an enemy bullet inside its dome is wiped out");
        g.UseGadget();
        shot = EnemyShot(a + new V2(Game.ShieldGenR - 14f, 0f));
        Run(s, dt, 0.15f, null, Calm);
        Check(!p1.GadgetOn && g.Bullets.ContainsKey(shot.Id) && p1.GadgetLeft < Game.ShieldGenTime, "switched off: the bullet stays, the rest of its time saved");
        Quiet();

        // The Medkit: 3 taps, each clears your wound and heals 30% of your max health; it isn't spent while you're whole.
        Clear(); Hold(p1, GadgetId.Medkit);
        p1.Hp = p1.MaxHp; p1.Wound = 0f;
        g.UseGadget();
        Run(s, dt, 0.15f, null, Calm);
        Check(p1.GadgetUses == Game.MedkitUses, "the Medkit isn't spent at full health with no wound");
        p1.Wound = p1.MaxHp * 0.3f; p1.Hp = p1.MaxHp * 0.3f;
        g.UseGadget();
        Run(s, dt, 0.15f, null, Calm);
        Check(p1.Wound == 0f && Math.Abs(p1.Hp - p1.MaxHp * 0.6f) < 1f && p1.GadgetUses == 2, "a Medkit use clears the wound and heals 30% of max health");
        g.UseGadget();
        Run(s, dt, 0.15f, null, Calm);
        Check(p1.GadgetUses == 2, "a double tap doesn't spend two uses");
        for (int i = 0; i < 2; i++)
        {
            p1.Wound = 10f; p1.Hp = 20f;
            Run(s, dt, 0.7f, null, Calm);
            g.UseGadget();
            Run(s, dt, 0.15f, null, Calm);
        }
        Check(p1.Gadget == GadgetId.None && p1.Wound == 0f, "the 3rd use spends the Medkit");
        Hold(p2, GadgetId.Medkit);
        p2.Downed = true; p2.Hp = 0f;
        Run(s, dt, 0.3f, null, Calm);
        Check(p2.Gadget == GadgetId.None, "going down drops a Medkit like any gadget");
        p2.Downed = false; p2.Hp = p2.MaxHp;
        Clear();

        // Throwables: a tap lobs one of their 3 charges to the cursor; each lands on an enemy there.
        var spawnEnemy = typeof(Game).GetMethod("SpawnEnemyAt", bf);
        V2 spot = p1.Pos + new V2(220f, 0f);
        Enemy dummy = null;
        Action Target = () =>
        {
            Calm();
            foreach (var q in g.Bullets.Values.Where(q => Defs.IsEnemyShot(q.Kind)).ToList()) g.Bullets.Remove(q.Id);
            if (dummy == null || !g.Enemies.ContainsKey(dummy.Id))
            {
                dummy = (Enemy)spawnEnemy.Invoke(g, new object[] { EnemyType.Grunt, spot, 0f, 0, false });
                dummy.Hp = 1e5f;
            }
            dummy.Pos = spot; dummy.SpawnT = 0f;
            p1.AimAt = spot;
        };
        Clear(); Quiet(); Target();
        Hold(p1, GadgetId.Grenade);
        float hp0 = dummy.Hp;
        g.UseGadget(spot); g.UseGadget(spot);
        Run(s, dt, 0.05f, null, Target);
        Check(p1.GadgetUses == Game.ThrowUses - 1 && g.Bullets.Values.Any(q => q.Kind == Defs.GrenadeShot && q.Owner == 1), "a Grenade tap lobs one grenade (a second tap at once waits), a charge spent");
        Run(s, dt, 1f, null, Target);
        Check(dummy.Hp < hp0, "the grenade bursts on the enemy at the cursor (" + (hp0 - dummy.Hp).ToString("0") + " damage)");
        p1.GadgetUses = 1; p1.GadgetCd = 0f;
        g.UseGadget(spot);
        Run(s, dt, 0.05f, null, Target);
        Check(p1.Gadget == GadgetId.None, "its last charge thrown, it's gone");

        Hold(p1, GadgetId.SmokeBomb);
        g.UseGadget(spot);
        Run(s, dt, 1.2f, null, Target);
        Check(g.Zones.Values.Any(z => z.Kind == ZoneKind.SmokeShell && z.Owner == 1) && g.InGlSmoke(dummy), "a Smoke Bomb lands as a smoke cloud: the enemy in it can't attack");

        Hold(p1, GadgetId.GasGrenade);
        hp0 = dummy.Hp;
        g.UseGadget(spot);
        Run(s, dt, 1.5f, null, Target);
        Check(g.Zones.Values.Any(z => z.Kind == ZoneKind.ToxicGas) && cg.Zones.Values.Any(z => z.Kind == ZoneKind.ToxicGas) && dummy.Hp < hp0, "a Gas Grenade's cloud (on both screens) hurts the enemy in it (" + (hp0 - dummy.Hp).ToString("0") + ")");

        Hold(p1, GadgetId.Incendiary);
        g.Zones.Clear();
        g.UseGadget(spot);
        Run(s, dt, 1f, null, Target);
        hp0 = dummy.Hp;
        Run(s, dt, 0.5f, null, Target);
        Check(g.Zones.Values.Any(z => z.Kind == ZoneKind.FlameWall && z.Owner == 1 && z.Pos.DistanceTo(spot) < 40f) && dummy.Hp < hp0, "an Incendiary lands as burning ground that hurts the enemy on it");
        // The Land Mine: lobbed to the cursor, it lies armed; the enemy standing there sets it off.
        Hold(p1, GadgetId.LandMine);
        g.Zones.Clear();
        hp0 = dummy.Hp;
        g.UseGadget(spot);
        Run(s, dt, 0.1f, null, Target);
        Check(p1.GadgetUses == Game.ThrowUses - 1 && g.Zones.Values.Any(z => z.Kind == ZoneKind.FlakMine && z.Owner == 1), "a Land Mine tap lobs a mine (a charge spent)");
        Run(s, dt, 1f, null, Target);
        Check(dummy.Hp < hp0 && !g.Zones.Values.Any(z => z.Kind == ZoneKind.FlakMine), "the enemy on it sets it off (" + (hp0 - dummy.Hp).ToString("0") + " damage)");

        // The EMP: a pulse round you wipes out the enemy bullets in it and stuns (and hurts) the enemies; 2 pulses.
        Hold(p1, GadgetId.Emp);
        var near = EnemyShot(p1.Pos + new V2(0f, 150f));
        hp0 = dummy.Hp;
        g.UseGadget(spot);
        Check(!g.Bullets.ContainsKey(near.Id) && dummy.StunT > 0f && dummy.Hp < hp0 && p1.GadgetUses == Game.EmpUses - 1, "an EMP pulse wipes out the enemy bullets round you and stuns and hurts the enemies in it");
        Run(s, dt, 0.7f, null, Target);
        g.UseGadget(spot);
        Check(p1.Gadget != GadgetId.Emp, "its second pulse uses it up");

        // The Cloak: on, enemies can't pick you out (VanishT, on the client's screen too); off, it lapses.
        Hold(p1, GadgetId.Cloak);
        g.UseGadget();
        Run(s, dt, 0.3f, null, Target);
        Check(p1.GadgetOn && p1.VanishT > 0f && cg.Players[1].VanishT > 0f, "the Cloak on: you vanish from enemies (on both screens)");
        g.UseGadget();
        Run(s, dt, 0.5f, null, Target);
        Check(!p1.GadgetOn && p1.VanishT <= 0f, "switched off, you're seen again");

        // The Overclock Chip: on, fire rate and ability cooldowns run OverclockMult as fast.
        var fireRate = typeof(Game).GetMethod("FireRate", bf);
        Hold(p1, GadgetId.OverclockChip);
        float rate0 = (float)fireRate.Invoke(g, new object[] { p1 });
        g.UseGadget();
        Run(s, dt, 0.1f, null, Target);
        float rate1 = (float)fireRate.Invoke(g, new object[] { p1 });
        Check(p1.GadgetOn && Math.Abs(rate1 / rate0 - Game.OverclockMult) < 0.01f, "the Overclock Chip on: " + Game.OverclockMult + "x fire rate");
        g.UseGadget();
        Run(s, dt, 0.1f, null, Target);

        dummy.Hp = 0f; g.Zones.Clear();

        // Gadget chests: one each, into the hand or the bag; a full one spills at your feet (your own copy).
        foreach (var q in g.Players.Values)
        {
            q.Gadget = GadgetId.None;
            for (int i = 0; i < q.BagKind.Length; i++) q.BagKind[i] = GadgetId.None;
        }
        g.Gadgets.Clear();
        var chestGive = typeof(Game).GetMethod("GiveChestGadget", bf);
        chestGive.Invoke(g, new object[] { g.Players[1], a });
        Check(g.Players[1].Gadget != GadgetId.None && g.Gadgets.Count == 0, "a Gadget chest's gadget goes in your hand");
        for (int i = 0; i < 12; i++) chestGive.Invoke(g, new object[] { g.Players[1], a });
        Check(g.Players[1].BagKind.All(k => k != GadgetId.None) && g.Gadgets.Count > 0 && g.Gadgets.Values.All(x => x.Owner == 1), "with the hand and bag full the rest spills as your own copies");

        // The F1 dock's Spawn-a-gadget-or-relic sheet (Core/DevTools.cs): a full gadget at the player's feet as their own copy; a relic straight into the slot.
        g.Gadgets.Clear();
        Check(!cg.DevSpawnGadget(2, GadgetId.Lodestone) && !cg.DevSetRelic(2, RelicId.StoneHeart), "only the host can spawn a gadget or set a relic");
        Check(g.DevSpawnGadget(2, GadgetId.EnergyDrink) && g.Gadgets.Count == 1 && g.Gadgets.Values.First().Owner == 2
            && g.Gadgets.Values.First().Kind == GadgetId.EnergyDrink && Math.Abs(g.Gadgets.Values.First().Left - Game.EnergyTime) < 0.01f, "the dev spawn drops a full gadget, the player's own copy");
        Check(!g.DevSpawnGadget(2, GadgetId.None), "no gadget for None");
        float hpBefore = g.Players[2].MaxHp;
        Check(g.DevSetRelic(2, RelicId.StoneHeart) && g.Players[2].Relic == RelicId.StoneHeart && g.Players[2].MaxHp > hpBefore, "the dev relic goes in the slot and its stats apply");
        Check(!g.DevSetRelic(2, RelicId.StoneHeart), "setting the relic held again does nothing");
        Run(s, dt, 0.3f, null, () => { Quiet(); Put(); });
        Check(cg.Players[2].Relic == RelicId.StoneHeart && cg.Gadgets.Values.Any(x => x.Kind == GadgetId.EnergyDrink), "the client sees the dev relic and gadget");
        g.Coins.Clear();
        Check(!cg.DevSpawnCoins(2, 50, false), "only the host can spawn gold");
        Check(g.DevSpawnCoins(2, 250, false) && g.Coins.Values.Where(c => !c.Vec).Sum(c => c.Value) == 250 && g.Coins.Values.All(c => c.Owner == 2) && g.Coins.Count <= 40,
            "the dev spawn scatters the gold as the player's own coins");
        Check(g.DevSpawnCoins(2, 20, true) && g.Coins.Values.Where(c => c.Vec).Sum(c => c.Value) == 20, "and vectors as vector pickups");
        int gold0 = g.Players[2].Gold;
        foreach (var c in g.Coins.Values) c.Pos = c.Disp = g.Players[2].Pos;
        Run(s, dt, 0.5f, null, () => { Quiet(); Put(); });
        Check(g.Players[2].Gold == gold0 + 250 && cg.Players[2].Gold == gold0 + 250, "walking over them gives the gold (both screens)");
        int chestsBefore = g.Props.Values.Count(q => q.Kind == PropKind.Chest);
        Check(!cg.DevSpawnChest(2, ChestKind.Gold), "only the host can spawn a chest");
        Check(g.DevSpawnChest(1, ChestKind.Gold) && g.DevSpawnChest(1, ChestKind.Gold), "the dev spawn puts chests in front of the player");
        var devChests = g.Props.Values.Where(q => q.Kind == PropKind.Chest).Skip(chestsBefore).ToList();
        Check(devChests.Count == 2 && devChests.All(q => q.Chest == ChestKind.Gold && !q.Open) && !devChests[0].Pos.CloserThan(devChests[1].Pos, Game.ChestW) && !g.PropsHoldWave(),
            "of the kind chosen, side by side, and they don't hold the wave");
        Run(s, dt, 0.3f, null, () => { Quiet(); Put(); });
        Check(cg.Props.Values.Count(q => q.Kind == PropKind.Chest && q.Chest == ChestKind.Gold) >= 2, "the client sees the dev chests");
        Check(g.DevSetRelic(2, RelicId.None) && g.Players[2].Relic == RelicId.None && Math.Abs(g.Players[2].MaxHp - hpBefore) < 0.01f, "removing the relic takes its stats away");
        client.Leave(""); host.Leave("");
    }
}
