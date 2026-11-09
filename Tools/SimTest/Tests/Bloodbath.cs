using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Bloodbath (Bloodbath.cs), the Tank Hemomancer's second ultimate, and the lobby's ultimate pick (LobbyEntry.Ult): the 15% cost and the
// 1 HP floor, the pool growing out, the slow (normal, Heavy, boss), the bleed starting in the pool and stopping outside it, the 25% heal
// with no extra 3%, the 20% guard inside, Blood Price, ending on time and between waves but not on going down, mastery, the bot's rule,
// and the pick reaching the match (both screens, saves, a continued run's seat, the Practice Range, a class with one ultimate).
static partial class Program
{
    static void BloodbathTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var damage = typeof(Game).GetMethod("Damage", flags);
        var waveCleared = typeof(Game).GetMethod("WaveCleared", flags);

        // The table: key 0 stays Vein Tithe, key 1 is Bloodbath; other classes have one.
        var def = Upgrades.Ability(AbilityId.Bloodbath);
        Check(Upgrades.Ultimate(Role.Tank, Kit.Hemomancer) == AbilityId.BloodRite && Upgrades.Ultimate(Role.Tank, Kit.Hemomancer, 1) == AbilityId.Bloodbath
            && Upgrades.AltUltimate(Role.Tank, Kit.Hemomancer, 1) == AbilityId.Bloodbath && Upgrades.AltUltimate(Role.Tank, Kit.Hemomancer, 2) == AbilityId.None && Upgrades.AltUltimate(Role.Healer, Kit.Hemomancer, 1) == AbilityId.None
            && Upgrades.AltUltimate(Role.Tank, Kit.Soldier, 1) == AbilityId.JuggernautSuit
            && Upgrades.Ultimate(Role.Healer, Kit.Hemomancer, 1) == AbilityId.SanguineRain && Upgrades.Ultimate(Role.Wildcard, Kit.Hemomancer, 1) == AbilityId.CrimsonApotheosis,
            "the Tank Hemomancer has two ultimates (Vein Tithe the default, Bloodbath second); a pick a class lacks falls back to its default");
        Check(def.Name == "Bloodbath" && def.Cooldown == 75f && def.HealthCost == 0.15f && def.Choice == AbilityDef.Ult && def.UltKey == 1 && def.Kit == Kit.Hemomancer && def.Role == Role.Tank,
            "Bloodbath: a 75 s Tank Hemomancer ultimate costing 15% of max health");

        int port = nextPort++;
        var hostS = new Session { AutoStart = 2, StartWave = 3 };
        hostS.Host(port, "Blood", Role.Tank, 0, 0, Kit.Hemomancer, 0, 1);
        var client = JoinAs(port, false, "Friend", Role.Healer, 0, 0, Kit.Knight);
        var s = new[] { hostS, client };
        WaitForGame(hostS, client, dt);
        var g = hostS.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1]; var ally = g.Players[2];
        Check(me.UltPick == 1 && me.Slots[Upgrades.UltSlot].Id == AbilityId.Bloodbath && client.Game.Players[1].Slots[Upgrades.UltSlot].Id == AbilityId.Bloodbath
            && ally.Slots[Upgrades.UltSlot].Id == Upgrades.Ultimate(Role.Healer, Kit.Knight), "the lobby pick reaches the match, on both screens");
        g.DevSetGod(2, true);

        V2 mePos = new V2(500f, 450f), allyOut = new V2(1200f, 200f);
        var mine = new HashSet<int>();
        var pinned = new Dictionary<int, V2>();
        Func<EnemyType, V2, Enemy> make = (type, at) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; mine.Add(e.Id); pinned[e.Id] = at; return e;
        };
        float pinHp = 0f;
        V2 allyPos = allyOut;
        Action<float> step = secs =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                foreach (var id in g.Enemies.Keys.Where(k => !mine.Contains(k)).ToList()) g.Enemies.Remove(id);
                foreach (var kv in pinned) { Enemy e; if (g.Enemies.TryGetValue(kv.Key, out e)) e.Pos = kv.Value; }
                g.Bullets.Clear();
                me.Pos = mePos; ally.Pos = allyPos;
                if (pinHp > 0f && !me.Downed) me.Hp = pinHp;
                hostS.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = new V2(900f, 450f) });
            }
        };
        Action reset = () =>
        {
            mine.Clear(); pinned.Clear(); g.Enemies.Clear(); g.Bullets.Clear(); g.Effects.Clear(); g.Zones.Clear();
            me.Hp = me.MaxHp; me.Downed = false; me.UltT = 0f; me.InvulnT = 0f; me.Slots[Upgrades.UltSlot].Charges = 1; me.Pos = mePos;
            pinHp = 0f; allyPos = allyOut;
            me.MasteryXp.Remove(AbilityId.Bloodbath);  // (casting levels it up, and its power would grow the bleed)
            make(EnemyType.Grunt, new V2(1500f, 120f));  // far off: the wave goes on (clearing it ends the pool)
        };
        Func<Zone> bath = () => g.Zones.Values.FirstOrDefault(z => z.Kind == ZoneKind.Bloodbath);
        Action cast = () => { me.Slots[Upgrades.UltSlot].Charges = 1; hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, mePos, false }); };
        float regen = g.PassiveRegen + me.Stats.Regen;

        // The cost: 15% of max health up front; at 1 HP it still goes off.
        reset();
        cast();
        var z0 = bath();
        Check(Math.Abs(me.Hp - me.MaxHp * 0.85f) < 0.5f, "casting costs 15% of max health");
        Check(z0 != null && z0.Radius == Game.BathRadius && z0.T == Game.BathTime && z0.Pos.DistanceTo(mePos) < 1f, "the pool is 170 px round where you cast, for 10 s");
        Check(z0 != null && Game.BathReach(z0) < Game.BathRadius * 0.2f, "it starts small");
        step(Game.BathGrow + 0.05f);
        Check(z0 != null && Math.Abs(Game.BathReach(z0) - Game.BathRadius) < 0.5f, "and floods out to its full size over 0.9 s");
        mePos = new V2(300f, 300f);
        step(0.3f);
        Check(bath() != null && bath().Pos.DistanceTo(new V2(500f, 450f)) < 1f, "it doesn't follow you");
        mePos = new V2(500f, 450f);
        Check(client.Game.Zones.Values.Any(z => z.Kind == ZoneKind.Bloodbath && z.Pos.DistanceTo(z0.Pos) < 1f && Math.Abs(z.T - z0.T) < 0.5f), "other screens see the pool and its time left");
        reset();
        me.Hp = 1f;
        cast();
        Check(me.Hp == 1f && bath() != null, "at 1 HP it costs nothing more and still goes off");
        reset();
        me.Hp = me.MaxHp * 0.1f;
        cast();
        Check(me.Hp >= 1f && bath() != null, "the cost never takes you below 1 HP");

        // The slow: 50%, Heavy enemies 35%, bosses 20%; nothing outside it.
        reset();
        var grunt = make(EnemyType.Grunt, mePos + new V2(60f, 0f));
        var heavy = make(EnemyType.Juggernaut, mePos + new V2(-70f, 40f));
        var boss = make(EnemyType.Colossus, mePos + new V2(0f, -90f));
        var far = make(EnemyType.Grunt, mePos + new V2(400f, 0f));
        cast();
        step(Game.BathGrow + 0.1f);
        Console.WriteLine("  bloodbath: speed grunt x{0:0.00}, heavy x{1:0.00}, boss x{2:0.00}, outside x{3:0.00}", grunt.SlowMult, heavy.WadeMult, boss.WadeMult, far.SlowT > 0f ? far.SlowMult : 1f);
        Check(Math.Abs(grunt.SlowMult - 0.5f) < 1e-4f && grunt.SlowT > 0f, "enemies wading in it move 50% slower");
        Check(Math.Abs(heavy.WadeMult - 0.65f) < 1e-4f && heavy.WadeT > 0f, "Heavy enemies 35% slower (the usual slow skips them)");
        Check(Math.Abs(boss.WadeMult - 0.8f) < 1e-4f && boss.WadeT > 0f, "bosses 20% slower");
        Check(far.SlowT <= 0f && far.Hp == far.MaxHp, "an enemy outside it isn't slowed or bled");

        // The bleed: 15 a second while in it (bosses too), and it stops the moment they step out.
        reset();
        pinHp = me.MaxHp;  // full health: no Blood Price
        grunt = make(EnemyType.Grunt, mePos + new V2(60f, 0f));
        boss = make(EnemyType.Colossus, mePos + new V2(0f, -90f));
        cast();
        step(Game.BathGrow);
        float gh = grunt.Hp, bh = boss.Hp;
        step(2f);
        float bled = gh - grunt.Hp, bossBled = bh - boss.Hp;
        Console.WriteLine("  bloodbath: bled {0:0.0} in 2 s (boss {1:0.0}); expect 30 x {2:0.00}", bled, bossBled, me.Stats.DmgMult);
        Check(Math.Abs(bled - 30f * me.Stats.DmgMult) < 0.5f, "enemies in it bleed 15 a second");
        Check(bossBled > 0f, "bosses bleed too");
        pinned[grunt.Id] = mePos + new V2(400f, 0f);
        step(0.05f);
        gh = grunt.Hp;
        step(1.5f);
        Check(grunt.Hp == gh, "the bleed stops as soon as they leave the pool");
        pinned[grunt.Id] = mePos + new V2(30f, 30f);
        step(1.05f);
        Check(grunt.Hp < gh, "and starts again when they wade back in");

        // Blood Price scales it.
        reset();
        pinHp = me.MaxHp * 0.1f;
        grunt = make(EnemyType.Grunt, mePos + new V2(60f, 0f));
        cast();
        pinHp = me.MaxHp * 0.1f;
        step(Game.BathGrow);
        gh = grunt.Hp;
        step(2f);
        float priced = gh - grunt.Hp;
        Console.WriteLine("  bloodbath: at 10% health it bled {0:0.0} ({1:0.00}x)", priced, priced / bled);
        Check(Math.Abs(priced / bled - 1.4f) < 0.02f, "Blood Price scales the bleed (40% more at 10% health)");

        // The heal: 25% of what it dealt, wherever you are; not the class's 3% on top.
        reset();
        grunt = make(EnemyType.Grunt, mePos + new V2(60f, 0f));
        var grunt2 = make(EnemyType.Grunt, mePos + new V2(-50f, 50f));
        cast();
        step(Game.BathGrow);
        mePos = new V2(1300f, 800f);  // walk away: the pool stays and still heals you
        me.Hp = me.MaxHp * 0.5f;
        step(0.02f);
        float h0 = me.Hp, e0 = grunt.Hp + grunt2.Hp;
        step(2f);
        float dealtBy = e0 - grunt.Hp - grunt2.Hp, healed = me.Hp - h0 - regen * 2f;
        Console.WriteLine("  bloodbath: dealt {0:0.0}, healed {1:0.0} ({2:0.000})", dealtBy, healed, healed / dealtBy);
        Check(Math.Abs(healed / dealtBy - Game.BathHeal) < 0.01f, "every tick heals you 25% of its damage, wherever you are, with no extra 3%");
        Check(g.Effects.Any(e => e.Kind == FxKind.BathHeal && (int)e.Pos2.X == 1 && e.Radius > 0f), "each heal sends its number");
        mePos = new V2(500f, 450f);

        // Allies in it take 20% less; outside they take it all. You too.
        reset();
        cast();
        step(Game.BathGrow + 0.1f);
        g.DevSetGod(2, false);
        Func<Player, V2, float> hit = (q, at) =>
        {
            q.Pos = at; q.InvulnT = 0f; q.ShieldHp = 0f; q.Hp = q.MaxHp;
            damage.Invoke(g, new object[] { q, 40f, "test" });
            float lost = q.MaxHp - q.Hp; q.Hp = q.MaxHp; q.InvulnT = 0f; return lost;
        };
        float allyIn = hit(ally, mePos + new V2(40f, 20f)), allyOutLoss = hit(ally, allyOut);
        float meIn = hit(me, mePos), meOut = hit(me, new V2(1300f, 800f));
        me.Pos = mePos; ally.Pos = allyPos;
        g.DevSetGod(2, true);
        Console.WriteLine("  bloodbath: ally took {0:0.0} in it, {1:0.0} outside; you {2:0.0} / {3:0.0}", allyIn, allyOutLoss, meIn, meOut);
        Check(allyOutLoss > 0f && Math.Abs(allyIn / allyOutLoss - 0.8f) < 0.01f, "allies standing in it take 20% less damage, outside it full damage");
        Check(meOut > 0f && Math.Abs(meIn / meOut - 0.8f) < 0.01f, "and so do you");

        // Going down doesn't end it; it still bleeds what's in it.
        reset();
        grunt = make(EnemyType.Grunt, mePos + new V2(60f, 0f));
        cast();
        step(Game.BathGrow);
        me.InvulnT = 0f; me.ShieldHp = 0f;
        damage.Invoke(g, new object[] { me, me.MaxHp * 10f, "test" });
        gh = grunt.Hp;
        step(1.5f);
        Check(me.Downed && bath() != null && grunt.Hp < gh, "going down doesn't end it");
        me.Downed = false; me.Hp = me.MaxHp;

        // It ends after 10 s.
        reset();
        cast();
        step(Game.BathTime - 0.3f);
        Check(bath() != null, "it lasts its 10 s");
        step(0.5f);
        Check(bath() == null, "and is gone after them");

        // Mastered: 220 px and 35% back.
        reset();
        me.MasteryXp[AbilityId.Bloodbath] = 1e6f;
        cast();
        Check(bath() != null && bath().Radius == Game.BathRadiusMastered && bath().HealBack == Game.BathHealMastered, "mastered it spreads to 220 px and gives back 35%");
        me.MasteryXp.Remove(AbilityId.Bloodbath);

        // The bot casts it when several enemies are close and it's above 40% health.
        var probe = new Player { Hp = 60f, MaxHp = 100f };
        Check(def.Bot(new BotSense { Me = probe, NearEnemies = 3 }) && !def.Bot(new BotSense { Me = probe, NearEnemies = 1 })
            && !def.Bot(new BotSense { Me = new Player { Hp = 35f, MaxHp = 100f }, NearEnemies = 5 }), "a bot casts it with several enemies close and over 40% health");

        // It ends between waves (last: this clears the wave).
        reset();
        cast();
        step(0.5f);
        waveCleared.Invoke(g, null);
        Check(bath() == null, "it ends between waves");
        hostS.Leave(""); client.Leave("");

        BloodbathLobbyTest(dt);
    }

    // The lobby's ultimate pick (LobbyEntry.Ult): reaches the host and the others, is clamped, works in saves, a continued run and the Practice Range.
    static void BloodbathLobbyTest(float dt)
    {
        int port = nextPort++;
        var host = new Session();
        host.Host(port, "Host", Role.Healer, 0, 0, Kit.Knight);
        var client = new Session();
        client.Join("127.0.0.1", port, "Blood", Role.Tank, 0, 0, Kit.Hemomancer, 0, 1);
        for (int i = 0; i < 300 && !(client.Lobby.Count == 2 && host.Lobby.Count == 2); i++) { Step(host, dt); Step(client, dt); System.Threading.Thread.Sleep(2); }
        int cid = client.MyId;
        Func<int, bool> settle = want => { for (int i = 0; i < 200; i++) { Step(host, dt); Step(client, dt); if (host.Lobby[cid].Ult == want && client.Lobby[cid].Ult == want) return true; System.Threading.Thread.Sleep(1); } return false; };
        Check(host.Lobby.ContainsKey(cid) && settle(1) && host.Lobby[cid].UltId == AbilityId.Bloodbath, "a pick made before joining reaches the host and comes back to everyone");
        client.RequestUlt(0);
        Check(settle(0) && client.Lobby[cid].UltId == AbilityId.BloodRite, "picking key 0 goes back to Vein Tithe");
        client.RequestUlt(1);
        Check(settle(1), "and key 1 to Bloodbath again");
        client.RequestUlt(3);
        Check(settle(0), "a key with nothing on it is clamped to the default");
        client.RequestUlt(1);
        settle(1);
        host.RequestUlt(1);
        for (int i = 0; i < 20; i++) { Step(host, dt); Step(client, dt); }
        Check(host.Lobby[1].Ult == 0 && client.Lobby[1].Ult == 0, "a class with one ultimate keeps it");
        client.RequestKit(Kit.Knight);
        Check(settle(0), "changing class goes back to the default");
        client.RequestKit(Kit.Hemomancer);
        for (int i = 0; i < 20; i++) { Step(host, dt); Step(client, dt); }
        client.RequestUlt(1);
        settle(1);

        // The Practice Range plays with it too.
        host.PracticeMode = true;
        host.AutoStart = 2;
        host.StartMatch();
        for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++) { Step(host, dt); Step(client, dt); System.Threading.Thread.Sleep(2); }
        Check(host.Game.Practice != null && host.Game.Players[cid].Slots[Upgrades.UltSlot].Id == AbilityId.Bloodbath
            && client.Game.Players[cid].Slots[Upgrades.UltSlot].Id == AbilityId.Bloodbath && host.Game.Players[1].Slots[Upgrades.UltSlot].Id == Upgrades.Ultimate(Role.Healer, Kit.Knight),
            "in the Practice Range each player has the ultimate they picked");
        host.Leave(""); client.Leave("");

        // A save keeps it, and a continued run's seat brings it back.
        var saved = new RunSave { Wave = 4 };
        saved.Seats.Add(new SeatSave { Name = "Blood", Role = Role.Tank, Kit = Kit.Hemomancer, Ult = 1 });
        var back = RunSave.Parse(saved.ToText());
        var backSeat = back != null ? back.Seats.FirstOrDefault(x => x.Name == "Blood") : null;
        Check(backSeat != null && backSeat.Ult == 1, "a saved run keeps the ultimate each seat picked");
        var old = RunSave.Parse(saved.ToText().Replace("Tank Hemomancer 0 0 0 1", "Tank Hemomancer 0 0 0"));
        Check(old != null && old.Seats[0].Ult == 0, "saves from before the pick read as the default");
        var again = new Session();
        again.HostResume(back, nextPort++, "Blood");
        Check(again.Lobby[1].Ult == 1 && again.Lobby[1].UltId == AbilityId.Bloodbath, "continuing a run, your seat brings back its ultimate");
        again.Leave("");

        // The loadout preview casts the ultimate you picked.
        var show = new Showcase();
        show.Show(Role.Tank, Kit.Hemomancer, 0, Upgrades.RoleAbility(Role.Tank, Kit.Hemomancer, 0), true, true, 1);
        Check(show.Me != null && show.Me.Slots[Upgrades.UltSlot].Id == AbilityId.Bloodbath, "the lobby's preview shows the ultimate you picked");
        bool cast = false;
        for (int i = 0; i < 60 * 4 && !cast; i++) { show.Step(dt); cast = show.Game.Zones.Values.Any(z => z.Kind == ZoneKind.Bloodbath); }
        Check(cast, "and casts it");
        show.Stop();
    }
}
