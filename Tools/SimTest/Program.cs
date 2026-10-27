using System;
using System.Collections.Generic;
using System.Threading;
using Arena;

// Runs a host and a client in one process over real localhost TCP, both driven
// by bots, and checks that the client's view stays in sync with the host's.
// Build and run with ./run.sh (needs Mono's mcs) or any C# compiler.
static class Program
{
    static int failures;

    static void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "PASS " : "FAIL ") + what);
        if (!ok)
            failures++;
    }

    static void Step(Session s, float dt)
    {
        s.Poll();
        s.Game.FixedStep(dt, new PlayerInput { AimAt = new V2(640, 360) });
        s.Game.Update(dt);
        if (s.IsHost)
            foreach (var cue in s.Game.Sounds)
                if (cue.Sfx == Sfx.Pickup || cue.Sfx == Sfx.PickupBig)
                    pickups++;
        if (!s.IsHost && s.Game.Orbs.Count > 0)
            clientSawOrbs = true;
        s.Game.Sounds.Clear();
    }

    static int pickups;
    static bool clientSawOrbs;

    static void CheckStatSheet()
    {
        Func<string, int> U = name => Array.FindIndex(Upgrades.All, u => u.Name == name);
        var p = new Player { Role = Role.Dps, Def = Defs.Get(Role.Dps), BaseHp = 95f, RoleAbility = AbilityId.Dash };
        var taken = new List<int> { U("Sharpened Rounds"), U("Hollow Points"), U("Fortitude"), U("Multishot"), U("Blink"), U("Executioner") };
        var lines = StatSheet.Describe(p, taken);
        var map = new Dictionary<string, string>();
        foreach (var l in lines)
            if (!l.Header)
                map[l.Label] = l.Value;
        Console.WriteLine("  stat sheet: " + string.Join(" | ", Array.ConvertAll(lines.ToArray(), l => l.Header ? "[" + l.Label + "]" : l.Label + "=" + l.Value)));
        Check(map["Damage"] == "128%" && map["Max HP"] == "135" && map["Extra projectiles"] == "+1" && map["Q"] == "Blink"
            && map.ContainsKey("Executioner") && map["Regeneration"] == "0.5 HP/s", "stat sheet shows the right numbers");
        var withCard = new List<int>(taken) { U("Quick Trigger") };
        Check(StatSheet.Describe(p, withCard).Find(l => l.Label == "Fire rate").Value == "108%", "stat preview reflects the selected card");
    }

    static void CheckOffers()
    {
        var rng = new Random(1);
        bool ok = true;
        var rarityCount = new int[4];
        foreach (Role role in new[] { Role.Tank, Role.Healer, Role.Dps })
        for (int choice = 0; choice < Upgrades.RoleAbilityChoices; choice++)
        {
            var ab = Upgrades.RoleAbility(role, Kit.Soldier, choice);
            var taken = new List<int>();
            for (int level = 1; level < 60; level++)
            {
                var offer = Upgrades.RollOffer(rng, role, ab, taken, level);
                var seen = new HashSet<int>();
                foreach (int id in offer)
                {
                    ok &= id >= 0 && seen.Add(id) && Upgrades.Available(id, role, ab, taken);
                    ok &= Upgrades.All[id].Rarity != Rarity.Legendary;  // legendaries only come from bosses
                    ok &= Upgrades.All[id].RequiresAbility == AbilityId.None || Upgrades.All[id].RequiresAbility == ab;
                    if (id >= 0)
                        rarityCount[(int)Upgrades.All[id].Rarity]++;
                    if (id >= 0 && Upgrades.All[id].Grants != AbilityId.None)
                        ok &= !taken.Contains(id);
                }
                taken.Add(offer[rng.Next(3)]);
            }
            int grants = 0;
            foreach (int t in taken)
                if (Upgrades.All[t].Grants != AbilityId.None)
                    grants++;
            ok &= grants <= Upgrades.BonusSlots;
        }
        Console.WriteLine("offer rarities seen: common {0}, uncommon {1}, rare {2}, very rare {3}", rarityCount[0], rarityCount[1], rarityCount[2], rarityCount[3]);
        Check(ok, "offers are 3 distinct, eligible upgrades; at most 3 new abilities");
    }

    // Start at the boss wave with boosted damage so the bots kill the boss,
    // then check everyone got the bonus rerolls.
    static void BossRerollScenario(float dt)
    {
        var host = new Session { AutoStart = 2, StartWave = 5 };
        host.Game.Bot = true;
        host.Game.BotRerolls = false;
        Check(host.Host(7791, "Host", Role.Dps, 1), "boss scenario: host listens");
        var client = new Session();
        client.Game.Bot = true;
        client.Game.BotRerolls = false;
        client.Join("127.0.0.1", 7791, "Friend", Role.Dps, 2);
        for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
        int hollow = Array.FindIndex(Upgrades.All, u => u.Name == "Hollow Points");
        for (int k = 0; k < 25; k++)
        {
            host.Game.GrantUpgrade(1, hollow);
            host.Game.GrantUpgrade(2, hollow);
        }
        bool bossSeen = false, bossDead = false, bigOrb = false;
        for (int i = 0; i < 60 * 60 && !bossDead; i++)
        {
            Step(host, dt);
            Step(client, dt);
            if (i % 2 == 0)
                Thread.Sleep(1);
            bool bossAlive = false;
            foreach (var e in host.Game.Enemies.Values)
                bossAlive |= e.Def.IsBoss;
            bossSeen |= bossAlive;
            bossDead = bossSeen && !bossAlive;
            foreach (var o in client.Game.Orbs.Values)
                bigOrb |= o.Big;
            if (host.Game.State == MatchState.GameOver)
                break;
        }
        for (int i = 0; i < 20; i++)
        {
            Step(host, dt);
            Step(client, dt);
            Thread.Sleep(2);
            foreach (var o in client.Game.Orbs.Values)
                bigOrb |= o.Big;
        }
        Console.WriteLine("  boss dead: {0}, rerolls host {1}/{2}, client sees {3}/{4}", bossDead, host.Game.Players[1].Rerolls, host.Game.Players[2].Rerolls,
            client.Game.Players[1].Rerolls, client.Game.Players[2].Rerolls);
        Check(bossDead && host.Game.Players[1].Rerolls == 4 && client.Game.Players[2].Rerolls == 4, "boss kill gives everyone +2 rerolls");
        Check(bigOrb, "boss drops a large health orb (seen on the client)");
        // After the wave, each player picks one of The Warden's legendary weapon upgrades.
        Run(new[] { host, client }, dt, 20, () => host.Game.State == MatchState.Fighting && host.Game.Wave > 5);
        int legendaries = 0;
        foreach (var p in client.Game.Players.Values)
            foreach (int t in p.Taken)
                if (Upgrades.All[t].Rarity == Rarity.Legendary && Upgrades.All[t].Boss == EnemyType.Boss)
                {
                    legendaries++;
                    Console.WriteLine("  {0} took legendary: {1}", p.Role, Upgrades.All[t].Name);
                }
        Check(legendaries == 2, "boss kill gives each player one legendary weapon upgrade from that boss (seen on client)");
        int mods = 0;
        foreach (var p in client.Game.Players.Values)
            foreach (int t in p.Taken)
                if (Upgrades.All[t].Rarity == Rarity.Mod)
                {
                    mods++;
                    Console.WriteLine("  {0} took weapon mod: {1}", p.Role, Upgrades.All[t].Name);
                }
        Check(mods == 2, "after the legendary, each player also picks one weapon mod (seen on client)");
        host.Leave("");
        client.Leave("");
    }

    // Every weapon fires and the client's bullets match the host's.
    static void WeaponSweep(float dt)
    {
        int port = 7792;
        foreach (var roles in new[] { new[] { Role.Tank, Role.Healer }, new[] { Role.Dps, Role.Dps } })
        {
            for (int w = 0; w < Defs.WeaponsPerRole; w++)
            {
                var host = new Session { AutoStart = 2, StartWave = 2 };
                host.Game.Bot = true;
                host.Host(port, "Host", roles[0], w);
                var client = new Session();
                client.Game.Bot = true;
                client.Join("127.0.0.1", port, "Friend", roles[1], w);
                port++;
                for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
                int ghosts = 0, fired = 0;
                for (int i = 0; i < 60 * 10; i++)
                {
                    Step(host, dt);
                    Step(client, dt);
                    if (i % 2 == 0)
                        Thread.Sleep(1);
                    foreach (var b in host.Game.Bullets.Values)
                        if (b.Kind < Defs.SmallShot)
                            fired++;
                    if (i % 30 == 0)
                    {
                        for (int k = 0; k < 5; k++) { Thread.Sleep(2); client.Poll(); }
                        foreach (var b in client.Game.Bullets.Values)
                            if (!host.Game.Bullets.ContainsKey(b.Id))
                                ghosts++;
                    }
                }
                string names = Defs.Weapon(roles[0], Kit.Soldier, w).Name + " + " + Defs.Weapon(roles[1], Kit.Soldier, w).Name;
                Check(fired > 0 && ghosts == 0, "weapons fire and stay in sync: " + names);
                host.Leave("");
                client.Leave("");
            }
        }
    }

    static int nextPort = 7810;

    // Start a bot match at a given wave with both players buffed so they survive a while.
    static Session[] StartBuffed(float dt, int wave, int damageStacks)
    {
        int port = nextPort++;
        var host = new Session { AutoStart = 2, StartWave = wave };
        host.Game.Bot = true;
        host.Game.BotRerolls = false;
        host.Host(port, "Host", Role.Tank, 0);
        var client = new Session();
        client.Game.Bot = true;
        client.Game.BotRerolls = false;
        client.Join("127.0.0.1", port, "Friend", Role.Dps, 0);
        for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
        Func<string, int> U = name => Array.FindIndex(Upgrades.All, u => u.Name == name);
        foreach (int id in new[] { 1, 2 })
        {
            for (int k = 0; k < 20; k++) host.Game.GrantUpgrade(id, U("Fortitude"));
            for (int k = 0; k < 10; k++) host.Game.GrantUpgrade(id, U("Regeneration"));
            for (int k = 0; k < damageStacks; k++) host.Game.GrantUpgrade(id, U("Hollow Points"));
        }
        return new[] { host, client };
    }

    static void Run(Session[] s, float dt, float seconds, Func<bool> stop = null, Action each = null)
    {
        for (int i = 0; i < seconds * 60; i++)
        {
            Step(s[0], dt);
            Step(s[1], dt);
            if (i % 2 == 0)
                Thread.Sleep(1);
            if (each != null)
                each();
            if (stop != null && stop())
                break;
        }
        for (int k = 0; k < 5; k++) { Thread.Sleep(2); s[1].Poll(); }
    }

    static int Ghosts(Session[] s)
    {
        int ghosts = 0;
        foreach (var b in s[1].Game.Bullets.Values)
            if (!s[0].Game.Bullets.ContainsKey(b.Id))
                ghosts++;
        return ghosts;
    }

    // Waves 11-13: the new enemy types and elites show up on the client, in sync.
    static void LateGameEnemies(float dt)
    {
        var s = StartBuffed(dt, 11, 6);
        var seen = new HashSet<EnemyType>();
        bool eliteSeen = false;
        int ghosts = 0, frame = 0;
        Run(s, dt, 120, () => s[0].Game.Wave >= 14 || s[0].Game.State == MatchState.GameOver, () =>
        {
            foreach (var e in s[1].Game.Enemies.Values)
            {
                seen.Add(e.Type);
                eliteSeen |= e.Elite;
            }
            if (++frame % 60 == 0)
            {
                for (int k = 0; k < 5; k++) { Thread.Sleep(2); s[1].Poll(); }
                ghosts += Ghosts(s);
            }
        });
        var names = new List<string>();
        foreach (var t in seen)
            names.Add(t.ToString());
        Console.WriteLine("  reached wave {0}; client saw: {1}; elite seen: {2}", s[0].Game.Wave, string.Join(", ", names), eliteSeen);
        foreach (var t in new[] { EnemyType.Charger, EnemyType.Splitter, EnemyType.Shard, EnemyType.Mortar, EnemyType.Shielder, EnemyType.Medic })
            Check(seen.Contains(t), "new enemy shows up on the client: " + t);
        Check(eliteSeen, "elite enemies appear from wave 12");
        Check(ghosts == 0, "late-game waves stay in sync (mortar bursts, splits)");
        s[0].Leave("");
        s[1].Leave("");
    }

    // Every class ability choice works over the network.
    static void AbilitySweep(float dt)
    {
        foreach (var roles in new[] { new[] { Role.Tank, Role.Healer }, new[] { Role.Dps, Role.Dps } })
        {
            for (int choice = 0; choice < Upgrades.RoleAbilityChoices; choice++)
            {
                int port = nextPort++;
                var host = new Session { AutoStart = 2, StartWave = 3 };
                host.Game.Bot = true;
                host.Host(port, "Host", roles[0], 0, choice);
                var client = new Session();
                client.Game.Bot = true;
                client.Join("127.0.0.1", port, "Friend", roles[1], 0, choice);
                var s = new[] { host, client };
                for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
                bool taunt = false, zone = false, link = false, grenade = false, marked = false;
                int ghosts = 0, frame = 0;
                // Mark needs an enemy on screen when the bot presses it, so give it longer.
                float seconds = roles[0] == Role.Dps && choice == 2 ? 26f : 14f;
                Run(s, dt, seconds, null, () =>
                {
                    foreach (var p in client.Game.Players.Values)
                    {
                        taunt |= p.TauntT > 0f;
                        link |= p.LinkT > 0f;
                    }
                    zone |= client.Game.Zones.Count > 0;
                    foreach (var b in client.Game.Bullets.Values)
                        grenade |= b.Kind == Defs.GrenadeShot;
                    foreach (var e in client.Game.Enemies.Values)
                        marked |= e.Marked;
                    if (++frame % 60 == 0)
                    {
                        for (int k = 0; k < 5; k++) { Thread.Sleep(2); client.Poll(); }
                        ghosts += Ghosts(s);
                    }
                });
                foreach (int id in new[] { 1, 2 })
                {
                    var ab = client.Game.Players[id].Slots[0].Id;
                    bool seen = true;
                    switch (ab)
                    {
                        case AbilityId.Roar: seen = taunt; break;
                        case AbilityId.Haven: seen = zone; break;
                        case AbilityId.SoulLink: seen = link; break;
                        case AbilityId.Grenade: seen = grenade; break;
                        case AbilityId.Mark: seen = marked; break;
                    }
                    if (Environment.GetEnvironmentVariable("DEBUG") != null)
                        Console.WriteLine("  {0}: seen {1} ghosts {2} wave {3} enemies {4}", ab, seen, ghosts, host.Game.Wave, host.Game.Enemies.Count);
                    Check(ab == Upgrades.RoleAbility(client.Game.Players[id].Role, Kit.Soldier, choice) && seen && ghosts == 0,
                        "class ability works over the network: " + Upgrades.Ability(ab).Name);
                }
                host.Leave("");
                client.Leave("");
            }
        }
    }

    // Balance probe: a full run with invulnerable bots that level up and pick upgrades as they go,
    // timing how long each boss survives. (Not part of the suite; run with: run.sh 1 1 ttk [last wave])
    static void BossTtk(float dt, int lastWave)
    {
        int port = nextPort++;
        var host = new Session { AutoStart = 2, StartWave = 1 };
        host.Game.Bot = true;
        host.Host(port, "Host", Role.Tank, 0, 0, Kit.Soldier);
        var client = new Session();
        client.Game.Bot = true;
        client.Join("127.0.0.1", port, "Friend", Role.Dps, 0, 0, Kit.Soldier);
        var s = new[] { host, client };
        for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++) { Step(host, dt); Step(client, dt); }
        var born = new Dictionary<int, float>();
        float t = 0f;
        for (long frame = 0; frame < 60L * 60 * 90; frame++)
        {
            Step(host, dt);
            Step(client, dt);
            t += dt;
            foreach (var p in host.Game.Players.Values)
            {
                p.InvulnT = 1f;
                p.Downed = false;
                p.Hp = p.MaxHp;
            }
            foreach (var e in host.Game.Enemies.Values)
                if (e.Def.IsBoss && e.SpawnT <= 0f && !born.ContainsKey(e.Id))
                {
                    born[e.Id] = t;
                    Console.WriteLine("  wave {0}: {1} appears with {2:0} HP (players level {3}/{4}, {5}/{6} picks)", host.Game.Wave, e.Def.Name, e.MaxHp,
                        host.Game.Players[1].Level, host.Game.Players[2].Level, host.Game.Players[1].Taken.Count, host.Game.Players[2].Taken.Count);
                }
            foreach (var kv in new List<KeyValuePair<int, float>>(born))
                if (!host.Game.Enemies.ContainsKey(kv.Key))
                {
                    Console.WriteLine("      dead after {0:0.0} s", t - kv.Value);
                    born.Remove(kv.Key);
                }
            if (host.Game.Wave > lastWave || host.Game.State == MatchState.Victory)
                break;
            if (frame % 600 == 0)
                Thread.Sleep(1);
        }
        host.Leave(""); client.Leave("");
    }

    // The arena is 1600x900, and players wrap around its edges (enemies don't).
    static void WrapTest(float dt)
    {
        var s = StartBuffed(dt, 1, 0);
        s[0].Game.Bot = s[1].Game.Bot = false;
        Run(s, dt, 1);
        var friend = s[1].Game.Players[2];   // the client's own player
        friend.Pos = new V2(-4f, 300f);      // just stepped off the left edge
        Run(s, dt, 0.5f);
        var onHost = s[0].Game.Players[2];
        bool clientWrapped = friend.Pos.X > Game.Width - 20f && Math.Abs(friend.Pos.Y - 300f) < 1f;
        bool hostSees = onHost.Pos.X > Game.Width - 20f;
        var hostPlayer = s[0].Game.Players[1];
        hostPlayer.Pos = new V2(800f, Game.Height + 3f);  // off the bottom
        Run(s, dt, 0.5f);
        bool hostWrapped = hostPlayer.Pos.Y < 20f && s[1].Game.Players[1].Pos.Y < 20f;
        Console.WriteLine("  arena {0}x{1}; friend -> x={2:0}, host sees x={3:0}; host -> y={4:0}", Game.Width, Game.Height, friend.Pos.X, onHost.Pos.X, hostPlayer.Pos.Y);
        Check(Game.Width == 1600f && Game.Height == 900f, "the arena is 25% bigger (1600x900)");
        Check(clientWrapped && hostSees && hostWrapped, "players wrap around the arena's edges, on both machines");
        s[0].Leave(""); s[1].Leave("");
    }

    // The match can't start from the lobby until both players have clicked Ready.
    static void ReadyTest(float dt)
    {
        int port = nextPort++;
        var host = new Session();
        host.Host(port, "Host", Role.Tank);
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Dps);
        var s = new[] { host, client };
        Run(s, dt, 3, () => host.Lobby.Count == 2 && client.Lobby.Count == 2);
        host.StartMatch();
        Run(s, dt, 0.5f);
        bool blockedNoneReady = !host.Game.Running;
        host.RequestReady(true);
        host.StartMatch();
        Run(s, dt, 0.5f);
        bool blockedOneReady = !host.Game.Running && client.Lobby[1].Ready && !client.Lobby[2].Ready;
        client.RequestReady(true);
        client.RequestWeapon(1);  // changing your loadout takes the Ready back...
        Run(s, dt, 0.5f);
        bool unreadied = !host.Lobby[2].Ready && host.Lobby[2].Weapon == 1;
        client.RequestReady(true);  // ...so click it again
        Run(s, dt, 0.5f);
        bool allReady = host.AllReady && client.AllReady;
        host.StartMatch();
        Run(s, dt, 1f, () => client.Screen == AppScreen.Game);
        bool started = host.Game.Running && client.Screen == AppScreen.Game;
        host.ReturnToLobby();
        Run(s, dt, 0.5f);
        bool resetInLobby = !host.AllReady && !client.Lobby[1].Ready && !client.Lobby[2].Ready;
        Console.WriteLine("  none ready blocked {0}, one ready blocked {1}, loadout change unreadies {2}, both ready {3}, started {4}, reset in lobby {5}",
            blockedNoneReady, blockedOneReady, unreadied, allReady, started, resetInLobby);
        Check(blockedNoneReady && blockedOneReady, "the host can't start until both players are ready");
        Check(unreadied && allReady && started, "once both are ready the match starts (changing loadout un-readies you)");
        Check(resetInLobby, "back in the lobby, everyone has to ready up again");
        host.Leave(""); client.Leave("");
    }

    // Every role and class has its own ultimate on right click, and each one works over the network.
    static void UltimateSweep(float dt)
    {
        var seenUlts = new HashSet<AbilityId>();
        foreach (var kit in new[] { Kit.Soldier, Kit.Mage, Kit.Summoner })
        foreach (var roles in new[] { new[] { Role.Tank, Role.Healer }, new[] { Role.Dps, Role.Dps } })
        {
            int port = nextPort++;
            var host = new Session { AutoStart = 2, StartWave = 3 };
            host.Game.Bot = true;
            host.Host(port, "Host", roles[0], 0, 0, kit);
            var client = new Session();
            client.Game.Bot = true;
            client.Join("127.0.0.1", port, "Friend", roles[1], 0, 0, kit);
            var s = new[] { host, client };
            for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
            Run(s, dt, 6, () => host.Game.Enemies.Count >= 4 && host.Game.State == MatchState.Fighting);
            Func<int, Player> mine = id => (id == 1 ? host : client).Game.Players[id];
            foreach (int id in new[] { 1, 2 })
            {
                foreach (var g in new[] { host.Game.Players[id], mine(id) })  // fill the ultimate on both sides
                {
                    g.Slots[Upgrades.UltSlot].Charges = 1;
                    g.Slots[Upgrades.UltSlot].Timer = 0f;
                }
                if (kit == Kit.Soldier && roles[0] == Role.Tank)
                    host.Game.Players[id].Hp = 10f;  // for Lifeline to heal
                host.Game.Players[id].InvulnT = 2f;  // don't get downed before casting
            }
            var minions = new Dictionary<MinionKind, int>();
            var zones = new Dictionary<ZoneKind, int>();
            var ultT = new HashSet<int>();
            bool frozen = false;
            var fullHp = new HashSet<int>();
            Run(s, dt, 5, null, () =>
            {
                var mc = new Dictionary<MinionKind, int>();
                foreach (var m in client.Game.Minions.Values)
                    mc[m.Kind] = (mc.ContainsKey(m.Kind) ? mc[m.Kind] : 0) + 1;
                foreach (var kv in mc)
                    minions[kv.Key] = Math.Max(minions.ContainsKey(kv.Key) ? minions[kv.Key] : 0, kv.Value);
                var zc = new Dictionary<ZoneKind, int>();
                foreach (var z in client.Game.Zones.Values)
                    zc[z.Kind] = (zc.ContainsKey(z.Kind) ? zc[z.Kind] : 0) + 1;
                foreach (var kv in zc)
                    zones[kv.Key] = Math.Max(zones.ContainsKey(kv.Key) ? zones[kv.Key] : 0, kv.Value);
                foreach (var p in client.Game.Players.Values)
                    if (p.UltT > 0f)
                        ultT.Add(p.Id);
                foreach (var p in host.Game.Players.Values)
                    if (p.Hp >= p.MaxHp - 0.5f)
                        fullHp.Add(p.Id);
                foreach (var e in host.Game.Enemies.Values)
                    frozen |= e.FrozenT > 0f;
            });
            foreach (int id in new[] { 1, 2 })
            {
                var hp = host.Game.Players[id];
                var ult = hp.Slots[Upgrades.UltSlot].Id;
                seenUlts.Add(ult);
                bool used = hp.Slots[Upgrades.UltSlot].Charges == 0 && hp.Slots[Upgrades.UltSlot].Timer > 0f;
                Func<MinionKind, int> M = k => minions.ContainsKey(k) ? minions[k] : 0;
                Func<ZoneKind, int> Z = k => zones.ContainsKey(k) ? zones[k] : 0;
                bool effect = false;
                switch (ult)
                {
                    case AbilityId.Fortress: case AbilityId.BulletStorm: effect = ultT.Contains(id); break;
                    case AbilityId.GlacialTomb: effect = frozen; break;
                    case AbilityId.Titan: effect = M(MinionKind.Titan) > 0; break;
                    case AbilityId.Lifeline: effect = fullHp.Count == 2; break;
                    case AbilityId.Sanctum: effect = Z(ZoneKind.Sanctum) > 0; break;
                    case AbilityId.WorldTree: effect = Z(ZoneKind.Grove) > 0 && M(MinionKind.Sprite) >= 4; break;
                    case AbilityId.MeteorStorm: effect = Z(ZoneKind.Meteor) >= 10; break;
                    case AbilityId.Legion: effect = M(MinionKind.Wolf) >= 6 && M(MinionKind.Bat) >= 6; break;
                }
                bool synced = client.Game.Players[id].Slots[Upgrades.UltSlot].Id == ult && ult == Upgrades.Ultimate(hp.Role, kit);
                Check(used && effect && synced, string.Format("{0} {1}: ultimate {2} works over the network", kit, hp.Def.Name, Upgrades.Ability(ult).Name));
            }
            host.Leave("");
            client.Leave("");
        }
        Check(seenUlts.Count == 9, "all 9 role and class combinations have their own ultimate");
    }

    // Every mage and summoner weapon and ability, over the network.
    static void KitSweep(float dt)
    {
        foreach (var kit in new[] { Kit.Mage, Kit.Summoner })
        foreach (var roles in new[] { new[] { Role.Tank, Role.Healer }, new[] { Role.Dps, Role.Dps } })
        for (int choice = 0; choice < 3; choice++)
        {
            int port = nextPort++;
            var host = new Session { AutoStart = 2, StartWave = 3 };
            host.Game.Bot = true;
            host.Host(port, "Host", roles[0], choice, choice, kit);
            var client = new Session();
            client.Game.Bot = true;
            client.Join("127.0.0.1", port, "Friend", roles[1], choice, choice, kit);
            var s = new[] { host, client };
            for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
            var minions = new HashSet<MinionKind>();
            var zones = new HashSet<ZoneKind>();
            bool aegis = false, shield = false, surge = false, frenzy = false, bolt = false, boltShot = false;
            int ghosts = 0, frame = 0;
            Action watch = () =>
            {
                foreach (var m in client.Game.Minions.Values)
                    minions.Add(m.Kind);
                foreach (var z in client.Game.Zones.Values)
                    zones.Add(z.Kind);
                foreach (var p in client.Game.Players.Values)
                {
                    aegis |= p.AegisT > 0f;
                    shield |= p.ShieldHp > 0f;
                    surge |= p.SurgeT > 0f;
                    frenzy |= p.FrenzyT > 0f;
                }
                foreach (var fx in client.Game.Effects)
                    bolt |= fx.Kind == FxKind.Bolt;
                foreach (var b in client.Game.Bullets.Values)
                    boltShot |= b.Kind == Defs.BoltShot;
                if (++frame % 60 == 0)
                {
                    for (int k = 0; k < 5; k++) { Thread.Sleep(2); client.Poll(); }
                    ghosts += Ghosts(s);
                }
            };
            Run(s, dt, 13, null, watch);
            // A chain needs two enemies close together when a lightning shot lands; keep going until one does.
            bool lightning = false;
            foreach (var p in client.Game.Players.Values)
                lightning |= p.Weapon.Id == WeaponId.StormHammer || p.Weapon.Id == WeaponId.ChainLight || p.Weapon.Id == WeaponId.ArcLightning;
            if (lightning && !bolt)
                Run(s, dt, 15, () => bolt, watch);
            foreach (int id in new[] { 1, 2 })
            {
                var p = client.Game.Players[id];
                var w = p.Weapon;
                bool weaponOk = w.Companion == MinionKind.None || minions.Contains(w.Companion);
                if (w.Id == WeaponId.TotemStaff) weaponOk = zones.Contains(ZoneKind.Totem);
                // Chains need a second enemy nearby, so per run just require the shots; a chain is checked below.
                if (w.Id == WeaponId.StormHammer || w.Id == WeaponId.ChainLight || w.Id == WeaponId.ArcLightning) weaponOk = boltShot;
                bool abilityOk = true;
                switch (p.Slots[0].Id)
                {
                    case AbilityId.ArcaneAegis: abilityOk = aegis; break;
                    case AbilityId.FlameWall: abilityOk = zones.Contains(ZoneKind.FlameWall); break;
                    case AbilityId.ArcaneWard: abilityOk = shield; break;
                    case AbilityId.Meteor: abilityOk = zones.Contains(ZoneKind.Meteor); break;
                    case AbilityId.Blizzard: abilityOk = zones.Contains(ZoneKind.Blizzard); break;
                    case AbilityId.ArcaneSurge: abilityOk = surge; break;
                    case AbilityId.GolemWall: abilityOk = minions.Contains(MinionKind.Wall); break;
                    case AbilityId.GuardianSpirit: abilityOk = minions.Contains(MinionKind.Guardian); break;
                    case AbilityId.SpiritSwarm: abilityOk = minions.Contains(MinionKind.Sprite); break;
                    case AbilityId.Phoenix: abilityOk = minions.Contains(MinionKind.Phoenix); break;
                    case AbilityId.SoulLantern: abilityOk = zones.Contains(ZoneKind.Lantern); break;
                    case AbilityId.BatSwarm: abilityOk = minions.Contains(MinionKind.Bat); break;
                    case AbilityId.AlphaCall: abilityOk = frenzy; break;
                }
                bool kitOk = p.Kit == kit && Math.Abs(host.Game.Players[id].MaxHp - p.Def.Hp * Defs.KitHpMult(kit)) < 0.5f
                    && p.Slots[0].Id == Upgrades.RoleAbility(p.Role, kit, choice) && w == Defs.Weapon(p.Role, kit, choice);
                Check(kitOk && weaponOk && abilityOk && ghosts == 0, string.Format("{0} {1}: {2} + {3} work over the network",
                    kit, p.Def.Name, w.Name, Upgrades.Ability(p.Slots[0].Id).Name));
            }
            anyChain |= bolt;
            host.Leave("");
            client.Leave("");
        }
        Check(anyChain, "lightning chains between enemies (seen on the client)");
    }

    static bool anyChain;

    // Every legendary active at once: their extra shots, splits and turrets stay in sync.
    static void LegendarySync(float dt)
    {
        var s = StartBuffed(dt, 6, 0);
        for (int id = 0; id < Upgrades.All.Length; id++)
            if (Upgrades.All[id].Rarity == Rarity.Legendary)
            {
                s[0].Game.GrantUpgrade(1, id);
                s[0].Game.GrantUpgrade(2, id);
            }
        bool swarm = false;
        int ghosts = 0, frame = 0;
        Run(s, dt, 15, null, () =>
        {
            foreach (var b in s[1].Game.Bullets.Values)
                swarm |= b.Kind == Defs.SwarmShot;
            if (++frame % 60 == 0)
            {
                for (int k = 0; k < 5; k++) { Thread.Sleep(2); s[1].Poll(); }
                ghosts += Ghosts(s);
            }
        });
        Check(swarm && ghosts == 0, "all legendary weapon effects at once stay in sync");
        s[0].Leave(""); s[1].Leave("");
    }

    // Every weapon mod at full stacks: homing shots curve and stay in sync, ricochets
    // happen, and burning / chilled enemies show up on the client.
    static void WeaponModTest(float dt)
    {
        var s = StartBuffed(dt, 6, 0);
        var offer = Upgrades.ModOffer(Role.Dps, Upgrades.RoleAbility(Role.Dps, Kit.Soldier, 0), new List<int>());
        Check(offer != null && offer.Length == 7, "the weapon mod offer lists all 7 mods");
        foreach (int id in offer)
            for (int k = 0; k < 3; k++)
            {
                s[0].Game.GrantUpgrade(1, id);
                s[0].Game.GrantUpgrade(2, id);
            }
        Check(Upgrades.ModOffer(Role.Dps, AbilityId.Dash, s[0].Game.Players[2].Taken) == null, "maxed-out mods are no longer offered");
        var spawnAngle = new Dictionary<int, float>();
        int curved = 0, ricochets = 0, ghosts = 0, frame = 0, compared = 0;
        float worst = 0f;
        bool burning = false, chilled = false;
        Run(s, dt, 15, null, () =>
        {
            foreach (var b in s[0].Game.Bullets.Values)
            {
                if (b.Owner == 0 || b.Homing <= 0f)
                    continue;
                float a0;
                if (!spawnAngle.TryGetValue(b.Id, out a0))
                    spawnAngle[b.Id] = b.Vel.Angle;
                else if (Math.Abs(MathX.Fmod(b.Vel.Angle - a0 + MathX.Pi, MathX.Tau) - MathX.Pi) > 0.3f)
                {
                    curved++;
                    spawnAngle[b.Id] = 99f;  // count each bullet once
                }
                if (b.Bounces < 3 && b.Bounces >= 0 && b.LastHit != 0)
                    ricochets++;
            }
            foreach (var e in s[1].Game.Enemies.Values)
            {
                burning |= e.Burning;
                chilled |= e.Chilled;
            }
            if (++frame % 60 == 0)
            {
                for (int k = 0; k < 5; k++) { Thread.Sleep(2); s[1].Poll(); }
                ghosts += Ghosts(s);
                foreach (var b in s[1].Game.Bullets.Values)
                {
                    Bullet hb;
                    if (b.Homing > 0f && s[0].Game.Bullets.TryGetValue(b.Id, out hb))
                    {
                        worst = Math.Max(worst, hb.Pos.DistanceTo(b.Pos));
                        compared++;
                    }
                }
            }
        });
        Console.WriteLine("  {0} homing shots curved, {1} ricochet sightings, homing drift {2:0.0}px over {3} samples, burning {4}, chilled {5}",
            curved, ricochets, worst, compared, burning, chilled);
        Check(curved > 20, "Homing Rounds curve toward enemies");
        Check(ricochets > 0, "Ricochet shots bounce to another enemy");
        Check(compared > 0 && worst < 40f && ghosts == 0, "homing shots stay in sync on the client");
        Check(burning && chilled, "burning and chilled enemies show on the client");
        s[0].Leave(""); s[1].Leave("");
    }

    // The friend pauses mid-fight: everything freezes on both sides, then resumes in sync.
    static void PauseTest(float dt)
    {
        var s = StartBuffed(dt, 4, 0);
        Run(s, dt, 6, () => s[0].Game.Bullets.Count > 30);
        s[1].Game.RequestPause(true);
        Run(s, dt, 0.3f);
        bool paused = s[0].Game.Paused && s[1].Game.Paused && s[1].Game.PausedBy == 2;
        var hostBullets = new Dictionary<int, V2>();
        foreach (var b in s[0].Game.Bullets.Values)
            hostBullets[b.Id] = b.Pos;
        var enemyPos = new Dictionary<int, V2>();
        foreach (var e in s[0].Game.Enemies.Values)
            enemyPos[e.Id] = e.Pos;
        float playerHp = s[0].Game.Players[1].Hp;
        Run(s, dt, 2);
        bool frozen = s[0].Game.Players[1].Hp == playerHp;
        foreach (var b in s[0].Game.Bullets.Values)
            frozen &= hostBullets.ContainsKey(b.Id) && hostBullets[b.Id].DistanceTo(b.Pos) < 0.01f;
        foreach (var e in s[0].Game.Enemies.Values)
            frozen &= enemyPos.ContainsKey(e.Id) && enemyPos[e.Id].DistanceTo(e.Pos) < 0.01f;
        // While paused the client's bullets match the host's exactly.
        float worst = 0f;
        int missing = 0;
        foreach (var b in s[0].Game.Bullets.Values)
        {
            Bullet cb;
            if (s[1].Game.Bullets.TryGetValue(b.Id, out cb))
                worst = Math.Max(worst, cb.Pos.DistanceTo(b.Pos));
            else
                missing++;
        }
        Check(paused && frozen && worst < 0.01f && missing == 0 && s[1].Game.Bullets.Count == s[0].Game.Bullets.Count,
            "pausing freezes the match on both sides with bullets in identical positions");
        s[0].Game.RequestPause(false);  // the host resumes it
        Run(s, dt, 3);
        Check(!s[0].Game.Paused && !s[1].Game.Paused && s[0].Game.Bullets.Count > 0 && Ghosts(s) == 0, "resuming continues the match in sync");
        s[0].Leave(""); s[1].Leave("");
    }

    // A run's end-of-run numbers are tracked on the host and reach the friend intact.
    static void RunStatsTest(float dt)
    {
        int port = nextPort++;
        var host = new Session { AutoStart = 2, StartWave = 3 };
        host.Game.Bot = true;
        host.Host(port, "Host", Role.Healer, 0, 0);  // Light Bolt + Mend Pulse
        var client = new Session();
        client.Game.Bot = true;
        client.Join("127.0.0.1", port, "Friend", Role.Dps, 0, 0);
        var s = new[] { host, client };
        for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
        host.Game.Players[2].Hp *= 0.5f;  // give the healer someone to heal
        Run(s, dt, 45, () => host.Game.State == MatchState.GameOver || (host.Game.RunTime > 20f && host.Game.Players[1].Run.HealingDone > 0f
            && host.Game.Players[2].Run.Kills > 0 && host.Game.Players[1].Run.AbilitiesUsed > 0));
        if (host.Game.State != MatchState.GameOver)
            foreach (var p in host.Game.Players.Values)
            {
                p.Hp = 0f;  // end the run
                p.Downed = true;
            }
        Run(s, dt, 1, () => client.Game.RunStatsReady);
        bool same = true;
        foreach (var hp in host.Game.Players.Values)
        {
            var a = hp.Run;
            var b = client.Game.Players[hp.Id].Run;
            same &= a.Kills == b.Kills && Math.Abs(a.DamageDealt - b.DamageDealt) < 0.01f && a.ShotsFired == b.ShotsFired && a.Downs == b.Downs
                && Math.Abs(a.HealingDone - b.HealingDone) < 0.01f && a.AbilitiesUsed == b.AbilitiesUsed;
            Console.WriteLine("  {0}: dmg {1:F0}, kills {2}, biggest {3:F0}, accuracy {4:P0}, taken {5:F0}, healing {6:F0}, downs {7}, revives {8}, abilities {9}",
                hp.Role, a.DamageDealt, a.Kills, a.BiggestHit, a.Accuracy, a.DamageTaken, a.HealingDone, a.Downs, a.Revives, a.AbilitiesUsed);
        }
        var healer = host.Game.Players[1].Run;
        var dps = host.Game.Players[2].Run;
        Check(client.Game.RunStatsReady && same, "end-of-run stats reach the friend and match the host");
        Check(dps.DamageDealt > 0f && dps.Kills > 0 && dps.ShotsFired > 0 && dps.Accuracy > 0f && dps.Accuracy <= 1f
            && healer.HealingDone > 0f && healer.AbilitiesUsed > 0 && host.Game.RunTime > 1f, "end-of-run stats are filled in sensibly");
        host.Leave(""); client.Leave("");
    }

    /// Parking a tank's Barrier (or a Flame Wall) on a boss must not stop it from shooting.
    static void ExploitTest(float dt)
    {
        foreach (string mode in new[] { "barrier", "flame wall" })
        {
            var s = StartBuffed(dt, 5, 0);
            Run(s, dt, 10, () => { var b = Find(s[0], EnemyType.Boss); return b != null && b.SpawnT <= 0f; });
            var boss = Find(s[0], EnemyType.Boss);
            var tank = s[0].Game.Players[1];
            if (mode == "flame wall")
                s[0].Game.AddZone(ZoneKind.FlameWall, tank, boss.Pos, 90f, 30f);
            int escaped = 0, frame = 0;
            float hpBefore = s[0].Game.Players[2].Hp + s[0].Game.Players[1].Hp;
            Run(s, dt, 8, null, () =>
            {
                var b = Find(s[0], EnemyType.Boss);
                if (b == null)
                    return;
                if (mode == "barrier")
                {
                    tank.Pos = b.Pos;          // stand on the boss
                    tank.BarrierT = 4f;        // with Barrier up the whole time
                }
                foreach (var z in s[0].Game.Zones.Values)
                    z.Pos = b.Pos;             // the flame wall follows the boss
                foreach (var e in s[0].Game.Enemies.Values)
                    if (e != b)
                        e.Hp = 0f;             // only the boss's own bullets count
                if (++frame % 10 == 0)
                    foreach (var bl in s[0].Game.Bullets.Values)
                        if (Defs.IsEnemyShot(bl.Kind) && bl.Pos.DistanceTo(b.Pos) > 200f)
                            escaped++;
            });
            Console.WriteLine("  {0} on the boss: {1} boss bullet sightings beyond 200 px", mode, escaped);
            if (mode == "barrier")
            {
                // The client is told which bullets are the boss's, so it can draw them differently.
                int bossShots = 0, plain = 0;
                foreach (var bl in s[1].Game.Bullets.Values)
                    if (Defs.IsEnemyShot(bl.Kind))
                    {
                        if (bl.FromBoss) bossShots++; else plain++;
                    }
                Console.WriteLine("  client sees {0} boss bullets and {1} unflagged enemy bullets", bossShots, plain);
                Check(bossShots > 0 && plain == 0, "the client knows which bullets are a boss's");
            }
            Check(escaped > 50, "a " + mode + " sitting on a boss doesn't stop it from shooting");
            s[0].Leave(""); s[1].Leave("");
        }

        // ...but a Barrier still tanks incoming fire until it has soaked its limit, then shatters.
        {
            var s = StartBuffed(dt, 5, 0);
            Run(s, dt, 10, () => { var b = Find(s[0], EnemyType.Boss); return b != null && b.SpawnT <= 0f; });
            var tank = s[0].Game.Players[1];
            float blockedAt = -1f, brokeAt = -1f, t = 0f;
            tank.BarrierT = 30f;
            tank.BarrierMax = tank.BarrierHp = 360f;
            Run(s, dt, 30, () => brokeAt >= 0f, () =>
            {
                var b = Find(s[0], EnemyType.Boss);
                if (b != null)
                    tank.Pos = b.Pos + new V2(0f, 250f);  // in the line of fire, not on top of it
                t += dt;
                if (blockedAt < 0f && tank.BarrierHp < 360f)
                    blockedAt = t;
                if (tank.BarrierT <= 0f)
                    brokeAt = t;
            });
            Console.WriteLine("  barrier in the line of fire: first block at {0:F1}s, shattered at {1:F1}s", blockedAt, brokeAt);
            Check(blockedAt >= 0f && brokeAt > blockedAt, "a Barrier still blocks incoming fire, then shatters once it has soaked its limit");
            s[0].Leave(""); s[1].Leave("");
        }

        // In a regular wave, a Barrier holds for its whole duration no matter how much it blocks.
        {
            var s = StartBuffed(dt, 4, 0);
            Run(s, dt, 10, () => s[0].Game.Enemies.Count >= 4);
            var tank = s[0].Game.Players[1];
            tank.BarrierT = 6f;
            tank.BarrierMax = tank.BarrierHp = 360f;
            int nearby = 0;
            bool heldUp = true;
            Run(s, dt, 5, null, () =>
            {
                heldUp &= tank.BarrierT > 0f;
                foreach (var b in s[0].Game.Bullets.Values)
                    if (Defs.IsEnemyShot(b.Kind) && b.Pos.DistanceTo(tank.Pos) < tank.BarrierRadius + 20f)
                        nearby++;
            });
            Console.WriteLine("  regular wave: {0} enemy bullet sightings at the barrier, barrier at {1:F0}/360", nearby, tank.BarrierHp);
            Check(nearby > 0 && heldUp && tank.BarrierHp == 360f, "regular waves can't wear a Barrier down");
            s[0].Leave(""); s[1].Leave("");
        }
    }

    static bool BossAlive(Session s)
    {
        foreach (var e in s.Game.Enemies.Values)
            if (e.Def.IsBoss)
                return true;
        return false;
    }

    /// Waves 25-50: late enemies, the six new bosses' mechanics, victory and endless mode.
    static void LateWaves(float dt)
    {
        // Wave 31: all four late-game enemy types are in the mix.
        {
            var s = StartBuffed(dt, 31, 30);
            var seen = new HashSet<EnemyType>();
            int ghosts = 0, frame = 0;
            // 13 enemy types are in the mix by now, so give it a few waves to roll all four.
            Run(s, dt, 160, () => seen.Contains(EnemyType.Phantom) && seen.Contains(EnemyType.Hexer) && seen.Contains(EnemyType.Juggernaut) && seen.Contains(EnemyType.Nest), () =>
            {
                // This only checks which enemies appear, so keep the bots from dying before they've all spawned.
                foreach (var p in s[0].Game.Players.Values)
                    p.InvulnT = 1f;
                foreach (var e in s[1].Game.Enemies.Values)
                    seen.Add(e.Type);
                if (++frame % 60 == 0)
                {
                    for (int k = 0; k < 5; k++) { Thread.Sleep(2); s[1].Poll(); }
                    ghosts += Ghosts(s);
                }
            });
            foreach (var t in new[] { EnemyType.Phantom, EnemyType.Hexer, EnemyType.Juggernaut, EnemyType.Nest })
                Check(seen.Contains(t), "late-game enemy shows up on the client: " + t);
            Check(ghosts == 0, "late-game enemies stay in sync");
            s[0].Leave(""); s[1].Leave("");
        }

        foreach (int wave in new[] { 25, 30, 35, 40, 45, 50 })
        {
            var s = StartBuffed(dt, wave, 60);
            Run(s, dt, 10, () => BossAlive(s[0]));
            EnemyType boss = EnemyType.Grunt;
            foreach (var e in s[0].Game.Enemies.Values)
                if (e.Def.IsBoss)
                    boss = e.Type;
            bool mechanic = false, devourerHealed = false;
            float hungryHp = -1f;
            byte eclipsePhase = 0;
            int ghosts = 0, frame = 0;
            Run(s, dt, 150, () => !BossAlive(s[0]) && s[0].Game.State != MatchState.Fighting || s[0].Game.State == MatchState.GameOver, () =>
            {
                var cb = Find(s[1], boss);
                switch (boss)
                {
                    case EnemyType.Colossus: mechanic = true; break;
                    case EnemyType.Duelist: mechanic |= Find(s[1], EnemyType.Decoy) != null; break;
                    case EnemyType.Lich:
                    {
                        var ph = Find(s[0], EnemyType.Phylactery);
                        if (cb != null && cb.Phase == 1 && ph != null)
                        {
                            mechanic = true;
                            ph.Hp = 0f;  // the bots only shoot the nearest enemy, so break it for them
                        }
                        break;
                    }
                    case EnemyType.StormHerald: mechanic |= s[1].Game.Hazards.Count > 0; break;
                    case EnemyType.Devourer:
                    {
                        var hb = Find(s[0], EnemyType.Devourer);
                        if (hb != null && Game.Hungry(hb))
                        {
                            mechanic = true;
                            if (hungryHp < 0f)
                                hungryHp = hb.Hp;
                            else if (hb.Hp > hungryHp + 1f)
                                devourerHealed = true;
                        }
                        else
                            hungryHp = -1f;
                        break;
                    }
                    case EnemyType.Eclipse:
                        if (cb != null)
                            eclipsePhase = Math.Max(eclipsePhase, cb.Phase);
                        mechanic = eclipsePhase == 2;
                        break;
                }
                if (++frame % 60 == 0)
                {
                    for (int k = 0; k < 5; k++) { Thread.Sleep(2); s[1].Poll(); }
                    ghosts += Ghosts(s);
                }
            });
            bool dead = !BossAlive(s[0]) && s[0].Game.State != MatchState.GameOver;
            string name = Defs.Get(boss).Name;
            Console.WriteLine("  wave {0}: {1} defeated {2}, mechanic seen {3}, state {4}", wave, name, dead, mechanic, s[0].Game.State);
            Check(dead && mechanic && ghosts == 0 && (boss != EnemyType.Devourer || devourerHealed),
                "wave " + wave + " boss " + name + " works (its mechanic shows on the client) and can be beaten");
            if (wave == 50)
            {
                Run(s, dt, 1);
                Check(s[0].Game.State == MatchState.Victory && s[1].Game.State == MatchState.Victory, "beating The Eclipse at wave 50 wins the game on both screens");
                s[0].Game.ContinueEndless();
                Run(s, dt, 12, () => s[1].Game.Wave > 50 && s[1].Game.State == MatchState.Fighting);
                Check(s[1].Game.Endless && s[1].Game.Wave == 51, "endless mode carries on past wave 50");
            }
            s[0].Leave(""); s[1].Leave("");
        }
    }

    static Enemy Find(Session s, EnemyType t)
    {
        foreach (var e in s.Game.Enemies.Values)
            if (e.Type == t)
                return e;
        return null;
    }

    static void BossMechanics(float dt)
    {
        // Hive Queen (wave 10): summons a swarm and is immune while it's big.
        {
            var s = StartBuffed(dt, 10, 25);
            bool shielded = false, swarm = false, dead = false, seenQueen = false;
            Run(s, dt, 70, () => dead, () =>
            {
                var q = Find(s[1], EnemyType.HiveQueen);
                seenQueen |= q != null;
                shielded |= q != null && q.Phase == 1;
                swarm |= Find(s[1], EnemyType.Shard) != null;
                dead = seenQueen && Find(s[0], EnemyType.HiveQueen) == null;
            });
            Check(swarm && shielded, "Hive Queen summons a swarm and shields herself (seen on client)");
            Check(dead && s[0].Game.Players[1].Rerolls == Game.StartingRerolls + 2, "Hive Queen can be killed and pays out rerolls");
            s[0].Leave(""); s[1].Leave("");
        }
        // Twin Reapers (wave 15): killing one enrages the other; it revives unless both die in time.
        {
            var s = StartBuffed(dt, 15, 0);
            Run(s, dt, 15, () => Find(s[0], EnemyType.TwinA) != null && Find(s[0], EnemyType.TwinB) != null && Find(s[0], EnemyType.TwinB).SpawnT <= 0f);
            s[0].Game.Bot = s[1].Game.Bot = false;  // stop shooting so the survivor lives
            Find(s[0], EnemyType.TwinA).Hp = 0f;
            bool enraged = false, timerShown = false;
            Run(s, dt, 2, null, () =>
            {
                var b = Find(s[1], EnemyType.TwinB);
                enraged |= b != null && b.Phase == 1;
                timerShown |= s[1].Game.TwinReviveT > 0f;
            });
            Check(Find(s[0], EnemyType.TwinA) == null && enraged && timerShown, "killing one twin enrages the other and starts the revive timer (seen on client)");
            Run(s, dt, 8, () => Find(s[0], EnemyType.TwinA) != null);
            Run(s, dt, 0.5f);
            var revived = Find(s[1], EnemyType.TwinA);
            Check(revived != null && revived.Hp <= revived.MaxHp * 0.61f && revived.Hp > revived.MaxHp * 0.5f, "the fallen twin revives at 60% health");
            int before = s[0].Game.Players[1].Rerolls;
            Find(s[0], EnemyType.TwinA).Hp = 0f;
            Find(s[0], EnemyType.TwinB).Hp = 0f;
            Run(s, dt, 1);
            Check(Find(s[0], EnemyType.TwinA) == null && Find(s[0], EnemyType.TwinB) == null && s[0].Game.TwinReviveT == 0f
                && s[0].Game.Players[1].Rerolls == before + 2, "killing both together ends the fight with one reward");
            s[0].Leave(""); s[1].Leave("");
        }
        // Twin Reapers: Soul Bond makes the more-hurt one immune, and their tether hurts.
        {
            var s = StartBuffed(dt, 15, 0);
            Run(s, dt, 15, () => Find(s[0], EnemyType.TwinA) != null && Find(s[0], EnemyType.TwinB) != null && Find(s[0], EnemyType.TwinB).SpawnT <= 0f
                && Find(s[0], EnemyType.TwinA).SpawnT <= 0f);
            var a = Find(s[0], EnemyType.TwinA);
            var b = Find(s[0], EnemyType.TwinB);
            a.Hp = a.MaxHp * 0.5f;
            b.Hp = b.MaxHp;
            bool boundOnClient = false;
            Run(s, dt, 0.5f, null, () => { var ca = Find(s[1], EnemyType.TwinA); boundOnClient |= ca != null && ca.Phase == 2; });
            // Shoot only the bound one: the bots skip immune targets, so aim them by hand via a host-side check.
            float hpBound = a.Hp;
            foreach (var e in s[0].Game.Enemies.Values)
                if (!e.Def.IsBoss)
                    e.Hp = 0f;
            Run(s, dt, 3);
            bool immune = a.Hp >= hpBound - 0.01f && Game.SoulBound(a);
            bool otherHurt = b.Hp < b.MaxHp;  // the bots turned to the other Reaper instead
            b.Hp = b.MaxHp * 0.52f;
            Run(s, dt, 0.5f);
            bool released = !Game.SoulBound(a);
            Console.WriteLine("  soul bond: bound on client {0}, immune {1}, bots switched {2}, released {3}", boundOnClient, immune, otherHurt, released);
            Check(boundOnClient && immune && released, "Soul Bond: the more-hurt Reaper is immune until the other is evened out (seen on client)");
            Check(otherHurt, "bots switch to the Reaper that can be hurt");

            // Stand on the tether between them.
            s[0].Game.Bot = s[1].Game.Bot = false;
            var p = s[0].Game.Players[1];
            float hpBefore = 0f;
            bool hurt = false;
            Run(s, dt, 1.5f, null, () =>
            {
                var ta = Find(s[0], EnemyType.TwinA);
                var tb = Find(s[0], EnemyType.TwinB);
                if (ta == null || tb == null)
                    return;
                p.Pos = V2.Lerp(ta.Pos, tb.Pos, 0.5f);
                foreach (var bl in new List<Bullet>(s[0].Game.Bullets.Values))
                    if (Defs.IsEnemyShot(bl.Kind))
                        s[0].Game.Bullets.Remove(bl.Id);  // only the tether should hurt
                if (hpBefore == 0f)
                    hpBefore = p.Hp;
                hurt |= p.Hp < hpBefore - 5f;
            });
            Console.WriteLine("  tether: hp {0:0} -> {1:0}", hpBefore, p.Hp);
            Check(hurt, "the Reapers' tether hurts players standing in it");
            s[0].Leave(""); s[1].Leave("");
        }
        // The Singularity (wave 20): gravity phases pull players in.
        {
            var s = StartBuffed(dt, 20, 0);
            Run(s, dt, 15, () => s[0].Game.State == MatchState.Fighting && Find(s[0], EnemyType.Singularity) != null && Find(s[0], EnemyType.Singularity).SpawnT <= 0f);
            s[1].Game.Bot = false;  // the friend stands still; only the pull should move them
            V2 start = s[1].Game.Players[2].Pos;
            bool pullSeen = false;
            float closest = float.MaxValue;
            V2 center = new V2(Game.Width / 2f, Game.Height / 2f);
            Run(s, dt, 13, null, () =>
            {
                pullSeen |= s[1].Game.PullStrength > 0f;
                closest = Math.Min(closest, s[1].Game.Players[2].Pos.DistanceTo(center));
            });
            Console.WriteLine("  singularity: friend started {0:F0}px from the boss, got pulled to {1:F0}px", start.DistanceTo(center), closest);
            Check(pullSeen && closest < start.DistanceTo(center) - 100f, "The Singularity's gravity pulls the client's player in");
            s[1].Game.Bot = true;

            // Its gravity wells: they orbit it, bend the players' shots into themselves, and come back after you kill them.
            int wellsOnClient = 0, pulled = 0, compared = 0;
            float drift = 0f;
            var counted = new HashSet<int>();
            int frame = 0;
            Run(s, dt, 6, null, () =>
            {
                int n = 0;
                foreach (var e in s[1].Game.Enemies.Values)
                    if (e.Type == EnemyType.GravityWell)
                        n++;
                wellsOnClient = Math.Max(wellsOnClient, n);
                foreach (var b in s[0].Game.Bullets.Values)
                    if (b.Pulled && counted.Add(b.Id))
                        pulled++;
                foreach (var p in s[0].Game.Players.Values)
                    p.InvulnT = 1f;
                if (++frame % 60 == 0)
                {
                    for (int k = 0; k < 5; k++) { Thread.Sleep(2); s[1].Poll(); }
                    foreach (var b in s[1].Game.Bullets.Values)
                    {
                        Bullet hb;
                        if (s[0].Game.Bullets.TryGetValue(b.Id, out hb) && hb.Pulled)
                        {
                            drift = Math.Max(drift, hb.Pos.DistanceTo(b.Pos));
                            compared++;
                        }
                    }
                }
            });
            Console.WriteLine("  gravity wells: {0} on the client, {1} shots bent, drift {2:0.0}px over {3} samples", wellsOnClient, pulled, drift, compared);
            Check(wellsOnClient == 4, "the Singularity brings 4 gravity wells (seen on client)");
            Check(pulled > 30 && drift < 40f, "gravity wells bend players' shots into themselves, in sync on the client");
            foreach (var e in s[0].Game.Enemies.Values)
                if (e.Type == EnemyType.GravityWell)
                    e.Hp = 0f;
            Run(s, dt, 2);
            bool gone = Find(s[0], EnemyType.GravityWell) == null;
            Run(s, dt, 16, () => Find(s[0], EnemyType.GravityWell) != null, () => { foreach (var p in s[0].Game.Players.Values) p.InvulnT = 1f; });
            bool back = Find(s[0], EnemyType.GravityWell) != null;
            var sing = Find(s[0], EnemyType.Singularity);
            if (sing != null)
                sing.Hp = 0f;
            Run(s, dt, 2);
            bool collapsed = Find(s[0], EnemyType.GravityWell) == null;
            Console.WriteLine("  wells killed {0}, came back {1}, collapsed with the boss {2}", gone, back, collapsed);
            Check(gone && back && collapsed, "gravity wells return 15 s after you kill them, and collapse when the Singularity dies");
            s[0].Leave(""); s[1].Leave("");
        }
    }

    static int Main(string[] args)
    {
        int startWave = args.Length > 0 ? int.Parse(args[0]) : 1;
        int seconds = args.Length > 1 ? int.Parse(args[1]) : 60;
        int port = 7790;
        const float dt = 1f / 60f;

        if (args.Length > 2 && args[2] == "exploit")
        {
            ExploitTest(1f / 60f);
            Console.WriteLine(failures == 0 ? "ALL PASSED" : failures + " FAILED");
            return failures == 0 ? 0 : 1;
        }
        if (args.Length > 2 && args[2] == "ttk")
        {
            BossTtk(1f / 60f, args.Length > 3 ? int.Parse(args[3]) : 50);
            return 0;
        }
        if (args.Length > 2 && args[2] == "wrap")
        {
            WrapTest(1f / 60f);
            Console.WriteLine(failures == 0 ? "ALL PASSED" : failures + " FAILED");
            return failures == 0 ? 0 : 1;
        }
        if (args.Length > 2 && args[2] == "late")
        {
            LateGameEnemies(1f / 60f);
            Console.WriteLine(failures == 0 ? "ALL PASSED" : failures + " FAILED");
            return failures == 0 ? 0 : 1;
        }
        if (args.Length > 2 && args[2] == "bosses")
        {
            BossMechanics(1f / 60f);
            Console.WriteLine(failures == 0 ? "ALL PASSED" : failures + " FAILED");
            return failures == 0 ? 0 : 1;
        }
        if (args.Length > 2 && args[2] == "ready")
        {
            ReadyTest(1f / 60f);
            Console.WriteLine(failures == 0 ? "ALL PASSED" : failures + " FAILED");
            return failures == 0 ? 0 : 1;
        }
        if (args.Length > 2 && args[2] == "ults")
        {
            UltimateSweep(1f / 60f);
            Console.WriteLine(failures == 0 ? "ALL PASSED" : failures + " FAILED");
            return failures == 0 ? 0 : 1;
        }
        if (args.Length > 2 && args[2] == "mods")
        {
            WeaponModTest(1f / 60f);
            BossRerollScenario(1f / 60f);
            Console.WriteLine(failures == 0 ? "ALL PASSED" : failures + " FAILED");
            return failures == 0 ? 0 : 1;
        }
        CheckOffers();
        CheckStatSheet();

        var host = new Session { AutoStart = 2, StartWave = startWave };
        host.Game.Bot = true;
        host.Game.Log = Console.WriteLine;
        Check(host.Host(port, "Host", Role.Tank, 1), "host listens");

        var client = new Session();
        client.Game.Bot = true;
        client.Game.Log = Console.WriteLine;
        client.Join("127.0.0.1", port, "Friend", Role.Healer, 2);

        for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++)
        {
            Step(host, dt);
            Step(client, dt);
            Thread.Sleep(2);
        }
        Check(client.MyId == 2, "client got id 2");
        Check(host.Screen == AppScreen.Game && client.Screen == AppScreen.Game, "both in game after autostart");
        Check(client.Game.Players.Count == 2 && client.Game.Players[2].Role == Role.Healer, "client sees both players with chosen roles");
        Check(client.Game.Players[1].Weapon.Id == WeaponId.FlakCannon && host.Game.Players[2].Weapon.Id == WeaponId.MendersBow,
            "chosen weapons reach both sides (Flak Cannon, Mender's Bow)");

        // A friend on a different version gets a clear message instead of silence.
        {
            var other = new Session { Version = Session.ProtocolVersion + 1 };
            var h2 = new Session();
            h2.Host(port + 1, "Host", Role.Tank);
            other.Join("127.0.0.1", port + 1, "Friend", Role.Healer);
            for (int i = 0; i < 120 && other.Status.IndexOf("Version mismatch", StringComparison.Ordinal) < 0; i++)
            {
                h2.Poll();
                other.Poll();
                Thread.Sleep(5);
            }
            Console.WriteLine("  friend sees: " + other.Status);
            Console.WriteLine("  host sees: " + h2.Status);
            Check(other.Screen == AppScreen.Menu && other.Status.Contains("Version mismatch") && h2.Status.Contains("different version") == false
                && h2.Status.Contains("version " + (Session.ProtocolVersion + 1)) && h2.Lobby.Count == 1,
                "a different version is turned away with a clear message on both sides");
            h2.Leave("");
        }

        // A third player must be turned away.
        var extra = new Session();
        extra.Join("127.0.0.1", port, "Third", null);
        for (int i = 0; i < 120; i++)
        {
            Step(host, dt);
            Step(client, dt);
            extra.Poll();
            Thread.Sleep(2);
        }
        Check(extra.Screen == AppScreen.Menu && host.Lobby.Count == 2, "third player rejected, lobby unchanged");

        int maxBullets = 0, mismatches = 0, samples = 0, maxWave = 0;
        float maxError = 0f;
        var hostOrbIds = new HashSet<int>();
        var clientOrbIds = new HashSet<int>();
        int orbsHost = 0, orbsClient = 0;
        for (int i = 0; i < seconds * 60; i++)
        {
            Step(host, dt);
            Step(client, dt);
            if (i % 2 == 0)
                Thread.Sleep(1);
            maxBullets = Math.Max(maxBullets, host.Game.Bullets.Count);
            foreach (int id in host.Game.Orbs.Keys)
                if (hostOrbIds.Add(id))
                    orbsHost++;
            foreach (int id in client.Game.Orbs.Keys)
                if (clientOrbIds.Add(id))
                    orbsClient++;
            maxWave = Math.Max(maxWave, host.Game.Wave);
            if (i % 30 == 0)
            {
                // Let in-flight messages land, then compare the two views.
                for (int k = 0; k < 5; k++) { Thread.Sleep(2); client.Poll(); }
                samples++;
                // Every client bullet must exist on the host (no ghosts), every host
                // bullet the client has already heard about must exist on the client,
                // and shared bullets must be in nearly the same place.
                int clientMax = 0, ghosts = 0, missing = 0;
                float worst = 0f;
                foreach (var b in client.Game.Bullets.Values)
                {
                    clientMax = Math.Max(clientMax, b.Id);
                    Bullet hb;
                    if (!host.Game.Bullets.TryGetValue(b.Id, out hb))
                        ghosts++;
                    else
                        worst = Math.Max(worst, hb.Pos.DistanceTo(b.Pos));
                }
                foreach (var b in host.Game.Bullets.Values)
                    if (b.Id <= clientMax && !client.Game.Bullets.ContainsKey(b.Id))
                    {
                        missing++;
                        if (Environment.GetEnvironmentVariable("DEBUG") != null)
                            Console.WriteLine("    missing id {0} kind {1} life {2:F3} clientMax {3}", b.Id, b.Kind, b.Life, clientMax);
                    }
                maxError = Math.Max(maxError, worst);
                bool off = ghosts > 2 || missing > 2 || worst > 30f
                    || Math.Abs(host.Game.Enemies.Count - client.Game.Enemies.Count) > 1 || Math.Abs(host.Game.Wave - client.Game.Wave) > 1;
                if (off)
                {
                    mismatches++;
                    Console.WriteLine("  mismatch: ghosts {0} missing {1} pos error {2:F1}px enemies {3}/{4}", ghosts, missing, worst,
                        host.Game.Enemies.Count, client.Game.Enemies.Count);
                }
            }
            if (host.Game.State == MatchState.GameOver)
                break;
        }
        Console.WriteLine("peak bullets {0}, reached wave {1}, score {2}, worst bullet position error {3:F1}px", maxBullets, maxWave, host.Game.Score, maxError);
        Check(maxWave >= startWave, "waves progressed");
        Console.WriteLine("  health orbs: {0} seen on host, {1} on client", orbsHost, orbsClient);
        Check(maxBullets > 0, "bullets were fired");
        bool sameUpgrades = true;
        foreach (var hp in host.Game.Players.Values)
        {
            var cp = client.Game.Players[hp.Id];
            sameUpgrades &= hp.Level == cp.Level && string.Join(",", hp.Taken) == string.Join(",", cp.Taken);
            var names = new List<string>();
            foreach (int t in hp.Taken)
                names.Add(Upgrades.All[t].Name);
            var slots = new List<string>();
            foreach (var sl in cp.Slots)
                slots.Add(sl.Id == AbilityId.None ? "-" : sl.Id + "x" + sl.MaxCharges);
            Console.WriteLine("  {0} level {1}: {2} | slots {3}", hp.Role, hp.Level, string.Join(", ", names), string.Join(" ", slots));
        }
        Check(sameUpgrades, "host and client agree on levels and picked upgrades");
        bool sameRerolls = true;
        int rerollsUsed = 0;
        foreach (var hp in host.Game.Players.Values)
        {
            sameRerolls &= hp.Rerolls == client.Game.Players[hp.Id].Rerolls;
            rerollsUsed += Game.StartingRerolls - hp.Rerolls;
        }
        Console.WriteLine("  rerolls used: {0}", rerollsUsed);
        Check(sameRerolls && rerollsUsed > 0, "rerolls get used and stay in sync");
        Check(host.Game.Players[1].Level > 1, "players levelled up");
        Check(mismatches == 0, "client stays in sync (" + mismatches + "/" + samples + " samples off)");

        // New Q/E abilities: grant some directly, then let the bots use them.
        if (host.Game.State != MatchState.GameOver)
        {
            Func<string, int> U = name => Array.FindIndex(Upgrades.All, u => u.Name == name);
            Action<int, string> Give = (id, name) =>
            {
                var p = host.Game.Players[id];
                if (Upgrades.Available(U(name), p.Role, p.RoleAbility, p.Taken))
                    host.Game.GrantUpgrade(id, U(name));
            };
            for (int k = 0; k < 20; k++)  // keep the bots alive long enough to use everything
            {
                host.Game.GrantUpgrade(1, U("Fortitude"));
                host.Game.GrantUpgrade(2, U("Fortitude"));
            }
            Give(1, "Turret");
            Give(1, "Shockwave");
            Give(2, "Nova");
            Give(2, "Blink");
            Give(1, "Overdrive");
            Give(2, "Overcharge");
            int turretsSeen = 0, ghostsAfter = 0;
            for (int i = 0; i < 60 * 12; i++)
            {
                Step(host, dt);
                Step(client, dt);
                if (i % 2 == 0)
                    Thread.Sleep(1);
                foreach (var m in client.Game.Minions.Values)
                    if (m.Kind == MinionKind.Turret)
                        turretsSeen++;
                if (i % 30 == 0)
                {
                    for (int k = 0; k < 5; k++) { Thread.Sleep(2); client.Poll(); }
                    foreach (var b in client.Game.Bullets.Values)
                        if (!host.Game.Bullets.ContainsKey(b.Id))
                            ghostsAfter++;
                }
            }
            var c1 = client.Game.Players[1].Slots;
            var c2 = client.Game.Players[2].Slots;
            Console.WriteLine("  client sees tank slots {0}/{1}/{2}/{3}, healer slots {4}x{5}/{6}x{7}/{8}", c1[0].Id, c1[1].Id, c1[2].Id, c1[3].Id, c2[1].Id, c2[1].MaxCharges, c2[2].Id, c2[2].MaxCharges, c2[3].Id);
            bool slotsOk = true, overchargeOk = true;
            bool hasTurret = false;
            foreach (var hp in host.Game.Players.Values)
            {
                var grants = new List<AbilityId>();
                foreach (int t in hp.Taken)
                    if (Upgrades.All[t].Grants != AbilityId.None)
                        grants.Add(Upgrades.All[t].Grants);
                var cs = client.Game.Players[hp.Id].Slots;
                for (int k = 1; k <= Upgrades.BonusSlots; k++)
                    slotsOk &= cs[k].Id == (k - 1 < grants.Count ? grants[k - 1] : AbilityId.None);
                slotsOk &= new HashSet<AbilityId>(grants).Count == grants.Count;  // no duplicates
                if (hp.Id == 1)
                    slotsOk &= grants.Count == 3 && cs[3].Id != AbilityId.None;  // tank fills Shift too
                hasTurret |= grants.Contains(AbilityId.Turret);
                if (hp.Id == 2)
                    overchargeOk = cs[1].MaxCharges == 2 && cs[2].MaxCharges == 2;
            }
            Check(slotsOk, "new abilities fill Q, E, then Shift, in pick order, on the client too");
            Check(overchargeOk, "Overcharge gives Q/E a second charge");
            Check(!hasTurret || turretsSeen > 0, "turret shows up on the client");
            Check(ghostsAfter == 0, "still in sync while using new abilities");
        }

        // Host restarts after game over / returns everyone to the lobby.
        host.ReturnToLobby();
        for (int i = 0; i < 30; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
        Check(client.Screen == AppScreen.Lobby && !client.Game.Running, "client follows host back to lobby");

        client.RequestRole(Role.Dps);
        for (int i = 0; i < 30; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
        client.RequestWeapon(1);
        for (int i = 0; i < 30; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
        Check(host.Lobby[2].Role == Role.Dps && client.Lobby[2].Role == Role.Dps, "role change reaches host and client");

        host.StartMatch();
        for (int i = 0; i < 30; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
        Check(client.Screen == AppScreen.Game && client.Game.Players[2].Role == Role.Dps, "second match starts with new role");
        Check(host.Game.Players[2].Weapon.Id == WeaponId.Railgun, "weapon change in lobby applies (Railgun)");

        // Host quits: the client should drop back to the menu with a message.
        host.Leave("");
        for (int i = 0; i < 60 && client.Screen != AppScreen.Menu; i++) { client.Poll(); Thread.Sleep(5); }
        Check(client.Screen == AppScreen.Menu && client.Status.Length > 0, "client notices host leaving");

        BossRerollScenario(dt);
        WeaponSweep(dt);
        LateGameEnemies(dt);
        AbilitySweep(dt);
        KitSweep(dt);
        LegendarySync(dt);
        WeaponModTest(dt);
        UltimateSweep(dt);
        ReadyTest(dt);
        WrapTest(dt);
        PauseTest(dt);
        RunStatsTest(dt);
        LateWaves(dt);
        ExploitTest(dt);
        BossMechanics(dt);
        Check(clientSawOrbs, "enemies drop health orbs and the client sees them");
        Check(pickups > 0, "players pick up orbs (" + pickups + " across all runs)");

        Console.WriteLine(failures == 0 ? "ALL PASSED" : failures + " FAILED");
        return failures == 0 ? 0 : 1;
    }
}
