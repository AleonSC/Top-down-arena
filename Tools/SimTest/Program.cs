using System.Linq;
using System;
using System.IO;
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

    // Three players: a host and two friends, all in sync, with enemies scaled up for the bigger party.
    static void ThreePlayerTest(float dt)
    {
        int port = nextPort++;
        var host = new Session { AutoStart = 3, StartWave = 1 };
        host.Game.Bot = true;
        host.Host(port, "Host", Role.Tank);
        var a = new Session();
        a.Game.Bot = true;
        a.Join("127.0.0.1", port, "Friend A", Role.Healer);
        var b = new Session();
        b.Game.Bot = true;
        b.Join("127.0.0.1", port, "Friend B", Role.Dps, 0, 0, Kit.Mage);
        var all = new[] { host, a, b };
        for (int i = 0; i < 600 && (a.Screen != AppScreen.Game || b.Screen != AppScreen.Game); i++)
        {
            foreach (var x in all) Step(x, dt);
            Thread.Sleep(2);
        }
        bool started = host.Game.Players.Count == 3 && a.Game.Players.Count == 3 && b.Game.Players.Count == 3;
        float hpRatio = 0f;
        int ghosts = 0;
        float worstPos = 0f;
        for (int i = 0; i < 60 * 90 && host.Game.Wave < 4; i++)
        {
            foreach (var x in all) Step(x, dt);
            if (i % 2 == 0) Thread.Sleep(1);
            foreach (var p in host.Game.Players.Values)
                p.InvulnT = 1f;
            foreach (var e in host.Game.Enemies.Values)
                if (!e.Def.IsBoss && !e.Elite && e.ParentId == 0 && hpRatio == 0f)
                    hpRatio = e.MaxHp / (e.Def.Hp * (1f + 0.15f * (host.Game.Wave - 1)));
            if (i % 120 == 0)
            {
                for (int k = 0; k < 5; k++) { Thread.Sleep(2); a.Poll(); b.Poll(); }
                foreach (var c in new[] { a, b })
                {
                    foreach (var bl in c.Game.Bullets.Values)
                        if (!host.Game.Bullets.ContainsKey(bl.Id))
                            ghosts++;
                    // Everyone else's player, as this client sees it, is where the host has it.
                    foreach (var p in c.Game.Players.Values)
                        if (p.Id != c.Game.MyId)
                        {
                            // Measured around the wrap: x=1599 and x=1 are 2 px apart.
                            V2 d = p.Pos - host.Game.Players[p.Id].Pos;
                            float dx = Math.Abs(d.X), dy = Math.Abs(d.Y);
                            dx = Math.Min(dx, Game.Width - dx);
                            dy = Math.Min(dy, Game.Height - dy);
                            worstPos = Math.Max(worstPos, (float)Math.Sqrt(dx * dx + dy * dy));
                        }
                }
            }
        }
        Console.WriteLine("  three players: started {0}, reached wave {1}, grunt hp x{2:0.00}, ghosts {3}, worst ally position error {4:0.0}px",
            started, host.Game.Wave, hpRatio, ghosts, worstPos);
        Check(started, "three players can join one game");
        Check(host.Game.Wave >= 3 && ghosts == 0 && worstPos < 40f, "a three-player game runs in sync for every client");
        Check(Math.Abs(hpRatio - 1.2f) < 0.01f, "enemies get 20% more health with three players");
        foreach (var x in all) x.Leave("");
    }

    // Early waves are melee rushers; shooters take over until the melee are gone by wave 13.
    static void MeleeTest(float dt)
    {
        Check(Game.MeleeShare(1) == 1f && Math.Abs(Game.MeleeShare(7) - 0.5f) < 0.01f && Game.MeleeShare(13) == 0f && Game.MeleeShare(30) == 0f,
            "melee share: all of wave 1, half by wave 7, none from wave 13");
        foreach (int wave in new[] { 1, 7, 14 })
        {
            var s = StartBuffed(dt, wave, 0);
            var types = new HashSet<EnemyType>();
            int melee = 0, ranged = 0, enemyShotsFromMelee = 0;
            bool leapSeen = false, meleeHurt = false;
            var counted = new HashSet<int>();
            Run(s, dt, 25, () => s[0].Game.Wave > wave, () =>
            {
                foreach (var e in s[1].Game.Enemies.Values)
                {
                    types.Add(e.Type);
                    if (counted.Add(e.Id))
                    {
                        if (e.Def.Melee) melee++; else ranged++;
                    }
                    leapSeen |= e.Type == EnemyType.Lunger && e.Phase == 2;
                }
            });
            var names = new List<string>();
            foreach (var t in types) names.Add(t.ToString());
            Console.WriteLine("  wave {0}: {1} melee, {2} shooters ({3})", wave, melee, ranged, string.Join(", ", names.ToArray()));
            if (wave == 1)
                Check(melee > 0 && ranged == 0, "wave 1 is all melee");
            else if (wave == 7)
                Check(melee > 0 && ranged > 0, "wave 7 mixes melee and shooters");
            else
                Check(melee == 0 && ranged > 0, "from wave 13 there are no melee enemies");
            s[0].Leave(""); s[1].Leave("");
        }

        // A brute walks into a player and hurts them; melee enemies never fire.
        {
            var s = StartBuffed(dt, 4, 0);
            s[0].Game.Bot = s[1].Game.Bot = false;
            Run(s, dt, 20, () => { foreach (var e in s[0].Game.Enemies.Values) if (e.Type == EnemyType.Brute && e.SpawnT <= 0f) return true; return false; });
            Enemy brute = null;
            foreach (var e in s[0].Game.Enemies.Values)
                if (e.Type == EnemyType.Brute && e.SpawnT <= 0f)
                    brute = e;
            var p = s[0].Game.Players[1];
            float before = p.Run.DamageTaken;  // health alone hides hits (the test player regenerates fast)
            int meleeShots = 0;
            Run(s, dt, 5, null, () =>
            {
                if (brute != null && s[0].Game.Enemies.ContainsKey(brute.Id))
                    p.Pos = brute.Pos + new V2(brute.Radius, 0f);
                // A melee enemy's fire timer is parked at 99 s the first time it comes up, so it never fires
                foreach (var e in s[0].Game.Enemies.Values)
                    // (it ticks down at most ~2.5 s per second, and is re-parked if it ever runs out)
                    if (e.Def.Melee && e.T > 3f && e.SpawnT <= 0f && e.FireT < 99f - e.T * 2.5f)
                        meleeShots++;
            });
            Console.WriteLine("  brute contact: found {0}, damage taken {1:0}", brute != null, p.Run.DamageTaken - before);
            Check(brute != null && p.Run.DamageTaken - before >= 24f, "melee enemies hurt you when they touch you");
            Check(meleeShots == 0, "melee enemies don't shoot");
            s[0].Leave(""); s[1].Leave("");
        }

        // Lungers wind up and leap (seen on the client).
        {
            var s = StartBuffed(dt, 6, 0);
            s[0].Game.Bot = s[1].Game.Bot = false;  // hold fire so the lungers reach us
            Run(s, dt, 6, () => s[0].Game.State == MatchState.Fighting);
            // Waves are random, so make sure there are lungers to watch.
            var spawn = typeof(Game).GetMethod("SpawnEnemyAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            for (int k = 0; k < 3; k++)
                spawn.Invoke(s[0].Game, new object[] { EnemyType.Lunger, new V2(300f + k * 400f, 120f), 0.5f, 0, false });
            bool windup = false, leap = false;
            Run(s, dt, 40, () => windup && leap, () =>
            {
                foreach (var e in s[1].Game.Enemies.Values)
                    if (e.Type == EnemyType.Lunger)
                    {
                        windup |= e.Phase == 1;
                        leap |= e.Phase == 2;
                    }
                foreach (var p in s[0].Game.Players.Values)
                    p.InvulnT = 1f;
            });
            Check(windup && leap, "lungers telegraph, then leap (seen on client)");
            s[0].Leave(""); s[1].Leave("");
        }
    }

    // The parry: blocks a boss's fire from the front, breaks after its limit, stops you shooting,
    // reflects shots on a perfect parry, and everyone sees it.
    static void ParryTest(float dt)
    {
        var s = StartBuffed(dt, 5, 0);
        Run(s, dt, 10, () => { var b = Find(s[0], EnemyType.Boss); return b != null && b.SpawnT <= 0f; });
        var w = Find(s[0], EnemyType.Boss);
        var me = s[0].Game.Players[1];
        s[1].Game.Bot = false;
        s[0].Game.Bot = true;  // aims at the Warden and holds the trigger
        Action pin = () =>
        {
            foreach (var e in s[0].Game.Enemies.Values)
                if (!e.Def.IsBoss) e.Hp = 0f;
            var bw = Find(s[0], EnemyType.Boss);
            if (bw != null)
                me.Pos = bw.Pos + new V2(0f, 170f);  // stand in front of it
            s[1].Game.Players[2].Pos = new V2(60f, Game.Height - 60f);  // the friend stays out of the way
        };
        // Down for a moment first, then raise it just as a shot is about to land (a perfect parry).
        Run(s, dt, 1f, null, pin);
        Run(s, dt, 6f, () =>
        {
            foreach (var b in s[0].Game.Bullets.Values)
                if (Defs.IsEnemyShot(b.Kind) && b.Pos.DistanceTo(me.Pos) < me.Def.BodyR + 45f && (b.Vel.X * (me.Pos.X - b.Pos.X) + b.Vel.Y * (me.Pos.Y - b.Pos.Y)) > 0f)
                    return true;
            return false;
        }, pin);
        float taken0 = me.Run.DamageTaken;
        int myShots = 0, reflected = 0;
        var seenShots = new HashSet<int>();
        foreach (var b in s[0].Game.Bullets.Values)
            seenShots.Add(b.Id);  // shots already in the air before raising it don't count
        bool clientSeesIt = false, broke = false;
        float takenWhileUp = -1f, upFor = 0f;
        s[0].Game.HoldParry = true;
        Run(s, dt, 12f, () => broke, () =>
        {
            pin();
            foreach (var b in s[0].Game.Bullets.Values)
                if (b.Owner == 1 && seenShots.Add(b.Id))
                {
                    if (b.Kind == Defs.ArcaneShot) reflected++;
                    else if (b.Kind != Defs.GrenadeShot) myShots++;
                }
            clientSeesIt |= s[1].Game.Players[1].Parrying;
            if (!broke) upFor += dt;
            if (me.ParryBroken > 0f && !broke)
            {
                broke = true;
                takenWhileUp = me.Run.DamageTaken - taken0;
            }
        });
        float takenAtBreak = me.Run.DamageTaken;
        Run(s, dt, 2f, null, pin);
        float takenAfter = me.Run.DamageTaken - takenAtBreak;
        Console.WriteLine("  parry: held {6:0.0}s against the Warden before breaking, damage taken while up {1:0}, after it broke {2:0}, own shots while up {3}, reflected {4}, client sees it {5}",
            broke, takenWhileUp, takenAfter, myShots, reflected, clientSeesIt, upFor);
        Check(broke && takenWhileUp < 20f, "the parry blocks a boss's fire from the front until it breaks");
        Check(takenAfter > 0f, "once broken, the parry stops blocking");
        Check(myShots == 0, "you can't shoot while parrying");
        Check(reflected > 0, "a perfect parry sends the shots back");
        Check(clientSeesIt, "other players see your parry shield");
        s[0].Game.HoldParry = false;
        s[0].Leave(""); s[1].Leave("");
    }

    // The parry upgrades: Reinforced, Wide and Light Guard, Riposte, Counterstance, Mirror Guard.
    static void ParryUpgradeTest(float dt)
    {
        var s = StartBuffed(dt, 5, 0);
        Run(s, dt, 10, () => { var b = Find(s[0], EnemyType.Boss); return b != null && b.SpawnT <= 0f; });
        var me = s[0].Game.Players[1];
        Func<string, int> U = name => Array.FindIndex(Upgrades.All, u => u.Name == name);
        s[0].Game.Bot = s[1].Game.Bot = false;

        // Wide Guard: a point 75 degrees off your aim is only covered with it.
        me.Aim = 0f;
        V2 side = me.Pos + V2.FromAngle(1.3f) * (me.Def.BodyR + 10f);
        bool narrowBefore = !Game.InParryArc(me, side, 2f);
        for (int k = 0; k < 2; k++) s[0].Game.GrantUpgrade(1, U("Wide Guard"));
        bool wideAfter = Game.InParryArc(me, side, 2f);
        Check(narrowBefore && wideAfter, "Wide Guard widens the parry arc");

        // Reinforced Guard: +25% durability per stack.
        for (int k = 0; k < 4; k++) s[0].Game.GrantUpgrade(1, U("Reinforced Guard"));
        Check(Math.Abs(Game.ParryMax(me) - Game.ParryCapacity * 2f) < 0.01f, "Reinforced Guard x4 doubles parry durability");

        // Light Guard: full speed while parrying.
        Func<float> speedWhileParrying = () =>
        {
            s[0].Game.HoldParry = true;
            V2 start = me.Pos;
            for (int i = 0; i < 30; i++)
            {
                s[0].Poll();
                s[0].Game.FixedStep(dt, new PlayerInput { Move = new V2(1f, 0f), AimAt = me.Pos + new V2(0f, -100f) });
                foreach (var p in s[0].Game.Players.Values) p.InvulnT = 1f;
            }
            s[0].Game.HoldParry = false;
            float dx = Math.Abs(me.Pos.X - start.X);
            return Math.Min(dx, Game.Width - dx) / (30 * dt);
        };
        float slow = speedWhileParrying();
        s[0].Game.GrantUpgrade(1, U("Light Guard"));
        float fast = speedWhileParrying();
        Console.WriteLine("  parry move speed: {0:0} -> {1:0} px/s (normal {2:0})", slow, fast, me.Speed);
        Check(slow < me.Speed * 0.8f && fast > me.Speed * 0.95f, "Light Guard removes the parry slowdown");

        // Mirror Guard, Riposte and Counterstance: block the Warden's fire and look at what comes back.
        for (int k = 0; k < 3; k++) s[0].Game.GrantUpgrade(1, U("Mirror Guard"));
        s[0].Game.GrantUpgrade(1, U("Riposte"));
        s[0].Game.GrantUpgrade(1, U("Counterstance"));
        int reflected = 0;
        float biggestReflect = 0f;
        bool counter = false;
        var seen = new HashSet<int>();
        foreach (var b in s[0].Game.Bullets.Values) seen.Add(b.Id);
        Action pin = () =>
        {
            foreach (var e in s[0].Game.Enemies.Values)
                if (!e.Def.IsBoss) e.Hp = 0f;
            var w = Find(s[0], EnemyType.Boss);
            if (w != null)
                me.Pos = w.Pos + new V2(0f, 170f);
            s[1].Game.Players[2].Pos = new V2(60f, Game.Height - 60f);
            foreach (var b in s[0].Game.Bullets.Values)
                if (b.Owner == 1 && b.Kind == Defs.ArcaneShot && seen.Add(b.Id))
                {
                    reflected++;
                    biggestReflect = Math.Max(biggestReflect, b.Dmg);
                }
            counter |= me.CounterT > 0f;
        };
        Run(s, dt, 1f, null, pin);  // shield down long enough that raising it is a fresh parry
        s[0].Game.Bot = true;       // aims at the Warden
        s[0].Game.HoldParry = true;
        Run(s, dt, 4f, null, pin);
        s[0].Game.HoldParry = false;
        float plain = 12f * me.Stats.DmgMult;
        Console.WriteLine("  mirror/riposte: {0} shots reflected, strongest {1:0.0} (plain reflect {2:0.0}), counterstance {3}", reflected, biggestReflect, plain, counter);
        Check(reflected > 10, "Mirror Guard sends blocked shots back");
        Check(biggestReflect >= plain * 1.99f, "Riposte doubles reflected shot damage");
        Check(counter, "Counterstance kicks in after a perfect parry");
        s[0].Leave(""); s[1].Leave("");
    }

    // Aim precision: a tiny change in where you point (a quarter of a degree) shows up exactly in
    // the direction of your shots, for the host and for a player who joined.
    static void AimTest(float dt)
    {
        int port = nextPort++;
        var host = new Session { AutoStart = 2, StartWave = 1 };
        host.Host(port, "Host", Role.Healer, 1);  // Pistol
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Healer, 1);
        var s = new[] { host, client };
        for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
        bool ok = true;
        float worst = 0f;
        foreach (var who in new[] { host, client })
        {
            var me = who.Game.Players[who.Game.MyId];
            foreach (float deg in new[] { 30f, 30.25f, 30.5f })
            {
                float want = deg * MathX.Pi / 180f;
                var seen = new HashSet<int>(host.Game.Bullets.Keys);
                float got = float.NaN;
                for (int i = 0; i < 90 && float.IsNaN(got); i++)
                {
                    V2 target = me.Pos + V2.FromAngle(want) * 400f;
                    foreach (var x in s)
                    {
                        x.Poll();
                        x.Game.FixedStep(dt, x == who ? new PlayerInput { AimAt = target, Fire = true } : new PlayerInput { AimAt = new V2(0f, 0f) });
                    }
                    Thread.Sleep(1);
                    foreach (var b in host.Game.Bullets.Values)
                        if (b.Owner == me.Id && !seen.Contains(b.Id))
                            got = b.Vel.Angle;
                }
                float err = Math.Abs(got - want) * 180f / MathX.Pi;
                worst = Math.Max(worst, float.IsNaN(err) ? 99f : err);
                ok &= !float.IsNaN(got) && err < 0.05f;
                // Let go of the trigger and wait out the fire delay before the next angle.
                for (int i = 0; i < 30; i++) foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, new PlayerInput { AimAt = me.Pos + V2.FromAngle(want) * 400f }); }
            }
        }
        Console.WriteLine("  aim: worst difference between aimed and fired direction {0:0.000} degrees", worst);
        Check(ok, "shots go exactly where you aim, to a quarter of a degree (host and joining player)");

        // The aim debug overlay's numbers: ping, and the client's record of its last shot.
        var cme = client.Game.Players[client.Game.MyId];
        float shotOff = Math.Abs(MathX.Fmod(client.Game.LastShotAngle - cme.Aim + MathX.Pi, MathX.Tau) - MathX.Pi) * 180f / MathX.Pi;
        Console.WriteLine("  aim debug: client ping {0:0.0} ms, last shot {1:0.00} degrees off its aim", client.Game.PingMs, shotOff);
        Check(client.Game.PingMs > 0f && client.Game.PingMs < 500f && client.Game.LastShotId != 0 && shotOff < 0.05f,
            "the aim debug overlay gets a ping and the direction of your last shot");
        host.Leave(""); client.Leave("");
    }

    // Extra projectiles upgrade each weapon in its own way. Every weapon fires at a cluster of dummies
    // with 0 and then 2 extra projectiles; each must get stronger in its own way, and none turns
    // into a line of bullets.
    static void WeaponExtraTest(float dt)
    {
        var s = StartBuffed(dt, 1, 0);
        s[0].Game.Bot = s[1].Game.Bot = false;
        Run(s, dt, 3, () => s[0].Game.State == MatchState.Fighting);
        var g = s[0].Game;
        var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", bf);
        var queue = (List<EnemyType>)typeof(Game).GetField("spawnQueue", bf).GetValue(g);
        var me = g.Players[1];
        var ally = g.Players[2];
        V2 from = new V2(540f, 450f), center = new V2(700f, 450f);  // the nearest dummy is 100 px out
        Func<WeaponDef, int, float[]> Trial = (w, extra) =>
        {
            center = new V2(700f, 450f);
            queue.Clear();
            g.Enemies.Clear();
            g.Minions.Clear();
            g.Zones.Clear();
            g.Bullets.Clear();
            var dummies = new List<Enemy>();
            for (int k = 0; k < 9; k++)
            {
                V2 at = k == 0 ? center : center + V2.FromAngle(MathX.Tau * (k - 1) / 8f) * (k % 2 == 0 ? 110f : 60f);
                var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, at, 0f, 0, false });
                e.MaxHp = e.Hp = 1e6f;
                dummies.Add(e);
            }
            me.Weapon = w;
            me.Stats.ExtraShots = extra;
            me.AmmoUsed = 0;
            me.ReloadT = 0f;
            me.TotemCd = 0f;
            me.CompanionCd = 0f;
            int spawned = 0, totems = 0;
            var seen = new HashSet<int>();
            for (int f = 0; f < 6 * 60; f++)
            {
                queue.Clear();
                me.Pos = from;
                ally.Pos = new V2(560f, 300f);
                foreach (var e in dummies) { e.Speed = 0f; e.FireT = 99f; e.Pos = e.Disp; }
                foreach (var x in s)
                {
                    x.Poll();
                    // The bow fires when you let go: draw past full (1.25 s), release, repeat.
                    x.Game.FixedStep(dt, x == s[0] ? new PlayerInput { AimAt = center, Fire = w.Id != WeaponId.MendersBow || f % 90 < 75 } : new PlayerInput());
                }
                foreach (var b in g.Bullets.Values)
                    if (b.Owner == 1 && seen.Add(b.Id)) spawned++;
                foreach (var z in g.Zones.Values)
                    if (z.Kind == ZoneKind.Totem && seen.Add(-z.Id)) totems++;
            }
            float dmg = 0f;
            foreach (var e in dummies) dmg += e.MaxHp - e.Hp;
            int companions = 0;
            float scale = 1f;
            foreach (var m in g.Minions.Values)
                if (m.Owner == 1 && m.Companion) { companions++; scale = Math.Max(scale, m.Scale); }
            return new[] { dmg, spawned, companions, totems, scale };
        };
        int pass = 0;
        var weak = new List<string>();
        Console.WriteLine("  weapon            dmg 0 -> +2    shots 0 -> +2   companions   (6 s at a cluster of dummies)");
        foreach (var w in Defs.Weapons)
        {
            var a = Trial(w, 0);
            var b = Trial(w, 2);
            float ratio = b[0] / Math.Max(1f, a[0]);
            bool ok;
            switch (w.Id)
            {
                case WeaponId.SpriteStaff: ok = b[2] == a[2] + 2; break;                 // +1 sprite each
                case WeaponId.TotemStaff: ok = b[3] > a[3]; break;                       // totems more often
                case WeaponId.MendersBow: ok = b[1] >= a[1] * 2.5f && ratio > 1.2f; break;  // full draws loose extra arrows
                case WeaponId.BearTotem: ok = ratio > 1.1f; break;                        // claws plus the Bear's cleave (it wanders)
                case WeaponId.TreantRod: ok = Math.Abs(b[4] - 1.6f) < 0.01f; break;       // a bigger treant
                case WeaponId.GolemStaff: ok = a[2] == 1 && b[2] == 3; break;
                case WeaponId.BoneGuard: ok = b[2] == a[2] + 2; break;                     // guards block bullets around you
                case WeaponId.WolfWhistle: case WeaponId.WispLantern: ok = b[2] == a[2] + 2 && ratio > 1.1f; break;
                case WeaponId.RadiantOrb: ok = a[2] == 0 && b[2] == 2 && ratio > 1.1f; break;  // the orbs mostly guard and heal you
                default: ok = ratio > 1.2f; break;
            }
            Console.WriteLine("  {0,-17} {1,6:0} -> {2,6:0} (x{3:0.00})  {4,4} -> {5,4}      {6} -> {7}{8}", w.Name, a[0], b[0], ratio, a[1], b[1], a[2], b[2], ok ? "" : "   <-- not stronger");
            if (ok) pass++; else weak.Add(w.Name);
        }
        Check(pass == Defs.Weapons.Length, "every weapon gets its own upgrade from extra projectiles (" + pass + "/" + Defs.Weapons.Length + ")" + (weak.Count > 0 ? ": " + string.Join(", ", weak) : ""));

        // The Revolver: six rounds (the last one heavy), then a reload; two more heavy rounds per extra projectile.
        {
            queue.Clear(); g.Enemies.Clear(); g.Bullets.Clear();
            me.Weapon = Defs.Weapon(Role.Healer, Kit.Soldier, 1);
            me.Stats.ExtraShots = 0;
            me.AmmoUsed = 0; me.ReloadT = 0f;
            var dmgs = new List<float>();
            var seen = new HashSet<int>(g.Bullets.Keys);
            bool reloaded = false, clientSawReload = false;
            float firstGap = 0f, t = 0f, lastShot = -1f;
            for (int f = 0; f < 4 * 60; f++)
            {
                t += dt;
                foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == s[0] ? new PlayerInput { AimAt = me.Pos + new V2(300f, 0f), Fire = true } : new PlayerInput()); }
                foreach (var b in g.Bullets.Values)
                    if (b.Owner == 1 && seen.Add(b.Id))
                    {
                        if (lastShot >= 0f && t - lastShot > firstGap) firstGap = t - lastShot;
                        lastShot = t;
                        dmgs.Add(b.Dmg);
                    }
                reloaded |= me.ReloadT > 0f;
                clientSawReload |= s[1].Game.Players[1].ReloadT > 0f;
            }
            float baseDmg = dmgs.Count > 0 ? dmgs[0] : 0f;
            Console.WriteLine("  revolver: {0} shots in 4 s, first six {1}, longest gap {2:0.00} s", dmgs.Count, string.Join(" ", dmgs.GetRange(0, Math.Min(7, dmgs.Count)).ConvertAll(x => x.ToString("0"))), firstGap);
            Check(dmgs.Count >= 7 && dmgs[4] == baseDmg && dmgs[5] == baseDmg * 2f && dmgs[6] == baseDmg && firstGap > 1.1f && reloaded && clientSawReload,
                "the Revolver fires six rounds (the sixth heavy), then reloads (seen on the client)");
            me.Stats.ExtraShots = 2;
            me.AmmoUsed = 0; me.ReloadT = 0f;
            Check(g.RevolverCapacity(me) == 10 && g.RevolverHeavy(me) == 5, "each extra projectile adds two heavy rounds to the Revolver's cylinder");
        }
        // Barrage builds the wall: every shot also fires copies fanned out to the sides.
        {
            Func<WeaponDef, int, int, List<Bullet>> FirstVolley = (w, fan, extra) =>
            {
                g.Bullets.Clear();
                me.Weapon = w;
                me.Stats.Fan = fan;
                me.Stats.ExtraShots = extra;
                me.AmmoUsed = 0; me.ReloadT = 0f;
                me.HeldT = 0f; me.Fanning = false; me.WasFiring = false; me.TapQueued = false;  // a fresh trigger pull
                var seen = new HashSet<int>();
                var shots = new List<Bullet>();
                for (int f = 0; f < 60 && shots.Count == 0; f++)
                {
                    foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == s[0] ? new PlayerInput { AimAt = me.Pos + new V2(300f, 0f), Fire = true } : new PlayerInput()); }
                    foreach (var b in g.Bullets.Values) if (b.Owner == 1 && seen.Add(b.Id)) shots.Add(b);
                }
                return shots;
            };
            var rev = FirstVolley(Defs.Weapon(Role.Healer, Kit.Soldier, 1), 2, 0);
            var angles = rev.ConvertAll(b => (float)Math.Round(b.Vel.Angle, 2));
            angles.Sort();
            int used = me.AmmoUsed;
            var scatter0 = FirstVolley(Defs.Weapon(Role.Tank, Kit.Soldier, 0), 0, 0);
            var scatter2 = FirstVolley(Defs.Weapon(Role.Tank, Kit.Soldier, 0), 2, 0);
            Func<List<Bullet>, float> Width = bs => { float lo = 9f, hi = -9f; foreach (var b in bs) { lo = Math.Min(lo, b.Vel.Angle); hi = Math.Max(hi, b.Vel.Angle); } return hi - lo; };
            Console.WriteLine("  barrage: revolver x2 fires at {0} using {1} round(s); scattergun {2} pellets over {3:0.00} rad -> {4} over {5:0.00} rad",
                string.Join(" ", angles), used, scatter0.Count, Width(scatter0), scatter2.Count, Width(scatter2));
            Check(rev.Count == 3 && Math.Abs(angles[0] + 0.14f) < 0.011f && Math.Abs(angles[2] - 0.14f) < 0.011f && used == 1,
                "Barrage fans every shot out to the sides (a Revolver round fans for one round of ammo)");
            Check(scatter2.Count == 3 * scatter0.Count && Width(scatter2) > Width(scatter0) + 0.25f, "Barrage stacks into a wall: a Scattergun with two copies fires three cones side by side");
            me.Stats.Fan = 0;
        }
        // Twin Rifle: barrels side by side, not a line.
        {
            g.Bullets.Clear();
            me.Weapon = Defs.Weapon(Role.Dps, Kit.Soldier, 0);
            me.Stats.ExtraShots = 2;
            var seen = new HashSet<int>();
            var first = new List<Bullet>();
            for (int f = 0; f < 60 && first.Count == 0; f++)
            {
                foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == s[0] ? new PlayerInput { AimAt = me.Pos + new V2(300f, 0f), Fire = true } : new PlayerInput()); }
                foreach (var b in g.Bullets.Values) if (b.Owner == 1 && seen.Add(b.Id)) first.Add(b);
            }
            float spreadY = 0f;
            foreach (var b in first) foreach (var c in first) spreadY = Math.Max(spreadY, Math.Abs(b.Pos.Y - c.Pos.Y));
            Check(first.Count == 4 && spreadY > 25f, "the Twin Rifle with +2 fires four parallel barrels at once");
        }
        s[0].Leave(""); s[1].Leave("");
    }

    // Bonus abilities go on the key you choose when you pick them, and can be moved in the Tab menu
    // (cooldowns move with them). Tested from the joining player, so it goes through the host.
    static void KeybindTest(float dt)
    {
        var s = StartBuffed(dt, 1, 0);
        s[0].Game.Bot = s[1].Game.Bot = false;
        Run(s, dt, 2);
        var host = s[0].Game;
        var hp = host.Players[2];
        var cp = s[1].Game.Players[2];
        Func<string, int> U = name => Array.FindIndex(Upgrades.All, u => u.Name == name);
        Action<string, int> Pick = (name, key) =>
        {
            host.State = MatchState.Intermission;
            hp.Offer = new[] { U(name), U("Vitality"), U("Focus") };
            hp.PendingPicks = 1;
            cp.Offer = hp.Offer;
            s[1].Game.PickUpgrade(0, key);
            Run(s, dt, 0.3f);
        };
        Func<Game, Player, string> Keys = (gm, p) => string.Join(",", new[] { 1, 2, 3 }.Select(k => p.Slots[k].Id == AbilityId.None ? "-" : p.Slots[k].Id.ToString()));
        Pick("Shockwave", 3);  // straight onto Shift
        string a = Keys(host, hp);
        Pick("Blink", 3);      // also Shift: Shockwave moves to a free key
        string b = Keys(host, hp);
        Console.WriteLine("  keys (Q,E,Shift): picked Shockwave on Shift -> {0}; then Blink on Shift -> {1}; client sees {2}", a, b, Keys(s[1].Game, cp));
        Check(a == "-,-,Shockwave" && b == "Shockwave,-,Blink" && Keys(s[1].Game, cp) == b, "a new ability goes on the key you choose (an ability already there moves to a free key)");

        // Use Blink so it's on cooldown, then move it from Shift to E in the Tab menu.
        hp.Slots[3].Charges = 0;
        hp.Slots[3].Timer = 5f;
        cp.Slots[3].Charges = 0;
        cp.Slots[3].Timer = 5f;
        s[1].Game.SetAbilityKey(AbilityId.Blink, 2);
        Run(s, dt, 0.3f);
        string c = Keys(host, hp);
        Console.WriteLine("  moved Blink to E: {0} (client {1}); its cooldown {2:0.0}s on the host, {3:0.0}s on the client", c, Keys(s[1].Game, cp), hp.Slots[2].Timer, cp.Slots[2].Timer);
        Check(c == "Shockwave,Blink,-" && Keys(s[1].Game, cp) == c && hp.Slots[2].Charges == 0 && hp.Slots[2].Timer > 4f && cp.Slots[2].Timer > 4f,
            "abilities can be moved to another key, and their cooldown moves with them");
        s[1].Game.SetAbilityKey(AbilityId.Shockwave, 2);  // onto Blink's key: they swap
        Run(s, dt, 0.3f);
        Check(Keys(host, hp) == "Blink,Shockwave,-", "moving onto a taken key swaps the two abilities");
        s[1].Game.SetAbilityKey(AbilityId.Nova, 1);  // an ability you don't have: ignored
        Run(s, dt, 0.3f);
        Check(Keys(host, hp) == "Blink,Shockwave,-", "you can't put an ability you don't have on a key");
        s[0].Leave(""); s[1].Leave("");
    }

    // Revolver, played by the joining player the way people actually shoot it: tapping. Each tap fires a
    // round with no reload in between until the cylinder is empty; a pause tops it back up a round at a
    // time; and Multishot's extra rounds really are there to fire.
    static void RevolverReloadTest(float dt)
    {
        int port = nextPort++;
        var host = new Session { AutoStart = 2, StartWave = 1 };
        host.Host(port, "Host", Role.Tank, 0);
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Healer, 1);  // Revolver
        var s = new[] { host, client };
        for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
        var hp = host.Game.Players[client.Game.MyId];
        host.Game.DevSetGod(hp.Id, true);  // a quiet range: no enemies to down you or eat your rounds
        Action Quiet = () => host.Game.Enemies.Clear();
        var cme = client.Game.Players[client.Game.MyId];
        Func<int, float, bool, int[]> Tap = (taps, every, untilEmpty) =>
        {
            // Tap fire (held for 3 frames) every `every` seconds; count rounds fired and reloads started.
            var seen = new HashSet<int>(host.Game.Bullets.Keys);
            int fired = 0, reloads = 0;
            bool wasReloading = hp.ReloadT > 0f;
            int frames = (int)(taps * every * 60);
            for (int f = 0; f < frames; f++)
            {
                bool fire = f % (int)(every * 60) < 3;
                Quiet(); foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == client ? new PlayerInput { AimAt = new V2(1500f, 450f), Fire = fire } : new PlayerInput()); }
                Thread.Sleep(1);
                foreach (var b in host.Game.Bullets.Values)
                    if (b.Owner == hp.Id && seen.Add(b.Id)) fired++;
                bool reloading = hp.ReloadT > 0f;
                if (reloading && !wasReloading) reloads++;
                wasReloading = reloading;
                if (untilEmpty && reloads > 0) break;
            }
            return new[] { fired, reloads };
        };
        Action<float> Idle = secs => { for (int f = 0; f < secs * 60; f++) { Quiet(); foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, new PlayerInput { AimAt = new V2(1500f, 450f) }); } Thread.Sleep(1); } };

        Idle(1f);
        var slow = Tap(12, 0.7f, true);  // deliberate taps, slower than the old 0.6 s auto-reload
        Console.WriteLine("  revolver: tapping every 0.7 s fired {0} rounds before the first reload", slow[0]);
        Check(slow[0] == 6, "tapping the Revolver slowly fires all six rounds before it reloads");
        Idle(2f);
        Tap(3, 0.35f, false);
        int afterThree = cme.AmmoUsed;
        Idle(1.6f);  // 0.8 s, then a round every 0.25 s
        Console.WriteLine("  revolver: after 3 shots the client shows {0} used; after a 1.6 s pause, {1} used", afterThree, cme.AmmoUsed);
        Check(afterThree == 3 && cme.AmmoUsed == 0 && hp.ReloadT <= 0f, "a pause tops the cylinder back up a round at a time, without locking you out");

        // Tapping fast is capped at the aimed rate (one every 0.45 s), and every tap shot is dead on.
        Idle(2f);
        {
            var seen = new HashSet<int>(host.Game.Bullets.Keys);
            var shots = new List<Bullet>();
            for (int f = 0; f < 90; f++)  // 1.5 s of clicking ten times a second
            {
                bool fire = f % 6 < 2;
                Quiet(); foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == client ? new PlayerInput { AimAt = hp.Pos + new V2(400f, 0f), Fire = fire } : new PlayerInput()); }
                Thread.Sleep(1);
                foreach (var b in host.Game.Bullets.Values) if (b.Owner == hp.Id && seen.Add(b.Id)) shots.Add(b);
            }
            float worst = 0f;
            foreach (var b in shots) worst = Math.Max(worst, Math.Abs(b.Vel.Angle));
            Console.WriteLine("  revolver: clicking 10x a second for 1.5 s fired {0} aimed rounds, worst {1:0.000} rad off aim", shots.Count, worst);
            Check(shots.Count >= 3 && shots.Count <= 4 && worst < 0.001f, "tapping fires aimed rounds at the slower tap rate");
        }
        // Holding fans the hammer: the whole cylinder goes in a quick burst, spread over a 10 degree arc.
        Idle(2.5f);
        {
            var seen = new HashSet<int>(host.Game.Bullets.Keys);
            var shots = new List<Bullet>();
            float t = 0f, lastAt = 0f;
            for (int f = 0; f < 60; f++)  // hold for a second
            {
                t += dt;
                Quiet(); foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == client ? new PlayerInput { AimAt = hp.Pos + new V2(400f, 0f), Fire = true } : new PlayerInput()); }
                Thread.Sleep(1);
                foreach (var b in host.Game.Bullets.Values) if (b.Owner == hp.Id && seen.Add(b.Id)) { shots.Add(b); lastAt = t; }
            }
            float lo = 9f, hi = -9f;
            foreach (var b in shots) { lo = Math.Min(lo, b.Vel.Angle); hi = Math.Max(hi, b.Vel.Angle); }
            Console.WriteLine("  revolver: holding fired {0} rounds in {1:0.00} s, spread {2:0.0} to {3:0.0} degrees; reloading {4}",
                shots.Count, lastAt, lo * 180f / MathX.Pi, hi * 180f / MathX.Pi, hp.ReloadT > 0f);
            Check(shots.Count == 6 && lastAt < 0.7f && lo >= -0.0875f && hi <= 0.0875f && hi - lo > 0.02f,
                "holding fans the hammer: all six rounds in a quick burst within a 10 degree arc");
        }
        Idle(2f);

        host.Game.GrantUpgrade(hp.Id, Array.FindIndex(Upgrades.All, u => u.Name == "Multishot"));
        Idle(2f);
        var multi = Tap(16, 0.35f, true);
        Console.WriteLine("  revolver + Multishot: {0} rounds before the reload (capacity {1}, client sees {2})", multi[0], host.Game.RevolverCapacity(hp), client.Game.RevolverCapacity(cme));
        Check(multi[0] == 8 && client.Game.RevolverCapacity(cme) == 8, "with Multishot the Revolver really fires eight rounds per cylinder");
        Idle(2f);
        var again = Tap(16, 0.35f, true);
        Check(again[0] == 8, "and gets all eight back after reloading");
        host.Leave(""); client.Leave("");
    }

    // Mender's Bow, drawn by the joining player: taps are weak, a full draw hits hard and pierces, a
    // release right at full draw is perfect, drawing slows you, and the draw is timed on the shooter's side.
    static void BowTest(float dt)
    {
        int port = nextPort++;
        var host = new Session { AutoStart = 2, StartWave = 1 };
        host.Host(port, "Host", Role.Tank, 0);
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Healer, 2);  // Mender's Bow
        var s = new[] { host, client };
        for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
        var hp = host.Game.Players[client.Game.MyId];
        var cme = client.Game.Players[client.Game.MyId];
        host.Game.DevSetGod(hp.Id, true);
        bool sawDrawOnHost = false;
        Func<Func<int, bool>, int, V2, List<Bullet>> Play = (fireAt, frames, move) =>
        {
            var seen = new HashSet<int>(host.Game.Bullets.Keys);
            var shots = new List<Bullet>();
            for (int f = 0; f < frames; f++)
            {
                host.Game.Enemies.Clear();
                foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == client ? new PlayerInput { AimAt = cme.Pos + new V2(400f, 0f), Fire = fireAt(f), Move = move } : new PlayerInput()); }
                Thread.Sleep(1);
                sawDrawOnHost |= hp.DrawT > 0.3f;
                foreach (var b in host.Game.Bullets.Values) if (b.Owner == hp.Id && seen.Add(b.Id)) shots.Add(b);
            }
            return shots;
        };
        Func<int, List<Bullet>> Draw = holdFrames => Play(f => f < holdFrames, holdFrames + 40, V2.Zero);
        Draw(0);
        var tap = Draw(2);
        var half = Draw(30);       // 0.5 s
        var perfect = Draw(57);    // 0.95 s: just past full draw
        var full = Draw(120);      // 2 s: held well past it
        Func<List<Bullet>, string> D = bs => bs.Count == 0 ? "none" : string.Join("/", bs.ConvertAll(b => b.Dmg.ToString("0.#") + " p" + b.Pierce + " heal " + (b.HealAlly * b.Dmg / 9f).ToString("0.#")));
        Console.WriteLine("  bow: tap {0}; 0.5 s {1}; 0.95 s {2}; 2 s {3}", D(tap), D(half), D(perfect), D(full));
        Check(tap.Count == 1 && Math.Abs(tap[0].Dmg - 8f) < 0.01f && tap[0].Pierce == 0, "a quick tap looses a weak arrow (8 damage)");
        Check(half.Count == 1 && half[0].Dmg > 15f && half[0].Dmg < 30f, "a half draw hits for something in between");
        Check(perfect.Count == 1 && Math.Abs(perfect[0].Dmg - 50f) < 0.01f && perfect[0].Pierce == 2 && Math.Abs(perfect[0].HealAlly * perfect[0].Dmg / 9f - 25f) < 0.1f,
            "letting go right at full draw is a perfect shot: 50 damage, pierces 2, heals an ally 25");
        Check(full.Count == 1 && Math.Abs(full[0].Dmg - 40f) < 0.01f && full[0].Pierce == 2 && full[0].Vel.Length > 900f, "holding past full draw keeps it at full (40 damage, fast, pierces 2), with no auto-fire");
        Check(sawDrawOnHost, "the host sees the joining player's draw (for the draw ring)");

        // Drawing slows you to 85%.
        V2 a0 = cme.Pos; Play(f => false, 30, new V2(1f, 0f)); float walk = cme.Pos.DistanceTo(a0);
        V2 b0 = cme.Pos; Play(f => true, 30, new V2(-1f, 0f)); float drawWalk = cme.Pos.DistanceTo(b0);
        Play(f => false, 30, V2.Zero);
        Console.WriteLine("  bow: walked {0:0} px in 0.5 s, {1:0} px while drawing", walk, drawWalk);
        Check(drawWalk / walk > 0.8f && drawWalk / walk < 0.9f, "drawing slows you to 85% speed");

        // Spamming taps is capped (a 0.25 s recovery between shots), and weaker than drawing.
        var spam = Play(f => f % 6 < 2, 90, V2.Zero);
        Console.WriteLine("  bow: clicking 10x a second for 1.5 s loosed {0} arrows", spam.Count);
        Check(spam.Count >= 3 && spam.Count <= 6, "tap spam is limited by the recovery between shots");

        // Extra projectiles nock more arrows, but only on a full draw.
        host.Game.GrantUpgrade(hp.Id, Array.FindIndex(Upgrades.All, u => u.Name == "Multishot"));
        host.Game.GrantUpgrade(hp.Id, Array.FindIndex(Upgrades.All, u => u.Name == "Multishot"));
        Play(f => false, 40, V2.Zero);
        var multiTap = Draw(2);
        var multiFull = Draw(80);
        Console.WriteLine("  bow +2: tap {0} arrow(s); full draw {1} arrows ({2})", multiTap.Count, multiFull.Count, D(multiFull));
        Check(multiTap.Count == 1 && multiFull.Count == 3 && Math.Abs(multiFull[1].Dmg - 20f) < 0.01f, "with +2 extra projectiles a full draw looses 3 arrows (the extras at half damage); taps stay single");
        host.Leave(""); client.Leave("");
    }

    // The F1 dev menu's host commands: hand-edited stats reach every player, god mode, and jumping to a wave.
    static void DevToolsTest(float dt)
    {
        var s = StartBuffed(dt, 1, 0);
        s[0].Game.Bot = s[1].Game.Bot = false;
        Run(s, dt, 2);
        var host = s[0].Game;
        int dmg = Array.FindIndex(Game.StatFields, f => f.Name == "DmgMult");
        int shots = Array.FindIndex(Game.StatFields, f => f.Name == "ExtraShots");
        int hp = Array.FindIndex(Game.StatFields, f => f.Name == "BonusHp");
        Check(dmg >= 0 && shots >= 0 && hp >= 0 && Game.StatFields.Length > 40, "the dev menu lists every stat");
        float baseMax = s[1].Game.Players[2].MaxHp;
        host.DevSetStat(2, dmg, 3f);
        host.DevSetStat(2, shots, 2.4f);  // rounds to a whole number
        host.DevSetStat(2, hp, s[1].Game.Players[2].Stats.BonusHp + 500f);
        Run(s, dt, 0.5f);
        var cp = s[1].Game.Players[2];
        Console.WriteLine("  dev stats on the client: dmg x{0}, extra shots {1}, max HP {2:0} (was {3:0})", cp.Stats.DmgMult, cp.Stats.ExtraShots, cp.MaxHp, baseMax);
        Check(cp.Stats.DmgMult == 3f && cp.Stats.ExtraShots == 2 && cp.MaxHp > baseMax + 499f && host.Players[2].Stats.DmgMult == 3f,
            "hand-edited stats apply on the host and the player's own game");
        // They survive picking an upgrade (stats are recomputed from upgrades, then the edits go back on top).
        host.GrantUpgrade(2, Array.FindIndex(Upgrades.All, u => u.Name == "Sharpened Rounds"));
        Run(s, dt, 0.3f);
        Check(s[1].Game.Players[2].Stats.DmgMult == 3f, "edited stats stay edited when you take an upgrade");
        host.DevResetStats(2);
        Run(s, dt, 0.3f);
        Check(s[1].Game.Players[2].Stats.DmgMult != 3f && s[1].Game.Players[2].Stats.ExtraShots == 0, "Reset stats goes back to what upgrades give");

        host.DevSetGod(1, true);
        var me = host.Players[1];
        float taken = me.Run.DamageTaken;
        var damage = typeof(Game).GetMethod("Damage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        me.InvulnT = 0f;
        damage.Invoke(host, new object[] { me, 500f, "test" });
        Run(s, dt, 0.3f);
        Check(me.Run.DamageTaken == taken && !me.Downed && s[1].Game.Players[1].DevGod, "god mode takes no damage (and the other player sees it)");
        host.DevSetGod(1, false);

        host.DevJumpToWave(10);
        Run(s, dt, 6, () => Find(s[1], EnemyType.HiveQueen) != null);
        Console.WriteLine("  dev jump: client on wave {0}, queen there {1}", s[1].Game.Wave, Find(s[1], EnemyType.HiveQueen) != null);
        Check(s[1].Game.Wave == 10 && Find(s[1], EnemyType.HiveQueen) != null, "jumping to wave 10 starts it (with its boss) for everyone");
        host.DevJumpToWave(3);
        Run(s, dt, 0.5f);
        Check(Find(s[1], EnemyType.HiveQueen) == null && host.Enemies.Count == 0, "jumping clears the arena");
        Run(s, dt, 3);
        Check(host.Wave == 3 && host.State == MatchState.Fighting, "and can go back to an earlier wave");
        s[0].Leave(""); s[1].Leave("");
    }

    // Ability mastery: XP from use, levels give power and shorter cooldowns, level 5 unlocks a capstone.
    static void MasteryTest(float dt)
    {
        var s = StartBuffed(dt, 3, 0);
        s[0].Game.Bot = s[1].Game.Bot = false;
        Run(s, dt, 6, () => s[0].Game.State == MatchState.Fighting);
        var host = s[0].Game;
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var cast = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        Func<string, int> U = name => Array.FindIndex(Upgrades.All, u => u.Name == name);
        var hp = host.Players[1];
        host.DevSetGod(1, true);
        host.DevSetGod(2, true);
        hp.Pos = s[1].Game.Players[1].Pos = new V2(600f, 450f);
        // A frozen, unkillable dummy right next to the host.
        Func<V2, Enemy> Dummy = at =>
        {
            var e = (Enemy)spawn.Invoke(host, new object[] { EnemyType.Grunt, at, 0f, 0, false });
            e.Hp = e.MaxHp = 1e6f;
            return e;
        };
        foreach (var e in host.Enemies.Values) e.Hp = 0f;
        Run(s, dt, 0.2f);
        var dummy = Dummy(new V2(680f, 450f));
        Action Hold = () => { foreach (var e in host.Enemies.Values) { e.FrozenT = 1f; if (e != dummy && e.Hp < 1e5f) e.Hp = 0f; } };
        Run(s, dt, 0.3f, null, Hold);

        host.GrantUpgrade(1, U("Shockwave"));
        Run(s, dt, 0.2f);
        int slot = Array.FindIndex(hp.Slots, sl => sl.Id == AbilityId.Shockwave);
        Func<float> Xp = () => { float x; hp.MasteryXp.TryGetValue(AbilityId.Shockwave, out x); return x; };
        float cd1 = host.Cooldown(hp, slot);
        float before = dummy.Hp;
        cast.Invoke(host, new object[] { 1, slot, dummy.Pos, true });
        float dmg1 = before - dummy.Hp, xpHit = Xp();
        Run(s, dt, 0.1f, null, Hold);
        // Cast with nothing in range: it still earns a little (instant abilities earn by what they hit).
        hp.Pos = s[1].Game.Players[1].Pos = new V2(1400f, 800f);
        cast.Invoke(host, new object[] { 1, slot, dummy.Pos, true });
        float xpMiss = Xp() - xpHit;
        Console.WriteLine("  shockwave XP: {0:0.0} for a hit, {1:0.0} for a miss (cooldown {2:0.0}s)", xpHit, xpMiss, cd1);
        Check(xpHit > 0f && xpMiss > 0f && xpMiss < xpHit * 0.8f, "abilities earn mastery XP, more when they land");

        // Level 3: +16% damage, -8% cooldown.
        host.GainMastery(hp, AbilityId.Shockwave, Game.MasteryXpFor[2] - Xp());
        hp.Pos = s[1].Game.Players[1].Pos = new V2(600f, 450f);
        Run(s, dt, 0.3f, null, Hold);
        before = dummy.Hp;
        cast.Invoke(host, new object[] { 1, slot, dummy.Pos, true });
        float dmg3 = before - dummy.Hp, cd3 = host.Cooldown(hp, slot);
        var cp = s[1].Game.Players[1];
        Console.WriteLine("  shockwave level {0}: damage {1:0.0} -> {2:0.0}, cooldown {3:0.00}s -> {4:0.00}s; client sees level {5}",
            Game.MasteryLevel(hp, AbilityId.Shockwave), dmg1, dmg3, cd1, cd3, Game.MasteryLevel(cp, AbilityId.Shockwave));
        Check(Game.MasteryLevel(hp, AbilityId.Shockwave) == 3 && Math.Abs(dmg3 / dmg1 - 1.16f) < 0.02f && Math.Abs(cd3 / cd1 - 0.92f) < 0.01f,
            "each mastery level adds 8% potency and takes 4% off the cooldown");


        // Capstones: Shockwave knocks back, Nova fires 48, Dash gets a charge, Mark hits three.
        host.GainMastery(hp, AbilityId.Shockwave, 1e4f);
        var pushed = Dummy(new V2(660f, 470f));
        Run(s, dt, 0.3f, null, Hold);
        V2 was = pushed.Pos;
        cast.Invoke(host, new object[] { 1, slot, dummy.Pos, true });
        float shove = pushed.Pos.DistanceTo(was);
        host.GrantUpgrade(1, U("Nova"));
        Run(s, dt, 0.2f, null, Hold);
        int nova = Array.FindIndex(hp.Slots, sl => sl.Id == AbilityId.Nova);
        Func<int> NovaShots = () => host.Bullets.Values.Count(b => b.Kind == Defs.NovaShot);
        foreach (var b in host.Bullets.Values.Where(b => b.Kind == Defs.NovaShot).ToList()) host.Bullets.Remove(b.Id);
        cast.Invoke(host, new object[] { 1, nova, dummy.Pos, true });
        int novaBase = NovaShots();
        foreach (var b in host.Bullets.Values.Where(b => b.Kind == Defs.NovaShot).ToList()) host.Bullets.Remove(b.Id);
        host.GainMastery(hp, AbilityId.Nova, 1e4f);
        cast.Invoke(host, new object[] { 1, nova, dummy.Pos, true });
        int novaMax = NovaShots();
        var dp = host.Players[2];
        int dash = Array.FindIndex(dp.Slots, sl => sl.Id == AbilityId.Dash);
        int dashBefore = dash >= 0 ? dp.Slots[dash].MaxCharges : -1;
        host.DevMasterAll(2);
        Run(s, dt, 0.3f, null, Hold);
        int dashAfter = dash >= 0 ? dp.Slots[dash].MaxCharges : -1, dashClient = dash >= 0 ? s[1].Game.Players[2].Slots[dash].MaxCharges : -1;
        Console.WriteLine("  capstones: shockwave shoves {0:0} px, nova {1} -> {2} shots, dash charges {3} -> {4} (client {5}), {6} gold",
            shove, novaBase, novaMax, dashBefore, dashAfter, dashClient, string.Join("/", dp.Slots.Where(sl => sl.Id != AbilityId.None).Select(sl => Game.MasteryLevel(dp, sl.Id))));
        Check(shove > 100f && novaBase == 24 && novaMax == 48, "level 5 unlocks each ability's capstone");
        Check(Game.MasteryLevel(cp, AbilityId.Shockwave) == 5 && host.MasteryToasts.Any(t => t.Ability == AbilityId.Shockwave && t.Level == 3)
            && s[1].Game.MasteryToasts.Any(t => t.Ability == AbilityId.Dash && t.Level == 5),
            "the player's own game knows the level (and shows a level-up popup)");
        Check(dash < 0 || dashAfter == dashBefore + 1 && dashClient == dashAfter, "a mastered Dash gets an extra charge on both games");
        Check(dp.Slots.Where(sl => sl.Id != AbilityId.None).All(sl => Game.Mastered(dp, sl.Id)), "the dev menu can master every ability");
        Check(Enum.GetValues(typeof(AbilityId)).Cast<AbilityId>().Where(a => a != AbilityId.None).All(a => Game.Capstone(a).Length > 0), "every ability has a capstone");

        // No XP between waves.
        float xpNow = Xp();
        host.State = MatchState.Intermission;
        cast.Invoke(host, new object[] { 1, slot, dummy.Pos, true });
        Check(Xp() == xpNow, "no mastery XP outside a fight");
        s[0].Leave(""); s[1].Leave("");
    }

    // How crowded the screen gets: enemy bullets on screen through a wave (bots playing, buffed so they survive).
    static void BulletCountReport(float dt)
    {
        foreach (int wave in new[] { 3, 6, 9, 12, 15, 18, 21, 24, 27 })
        {
            var s = StartBuffed(dt, wave, 6);
            foreach (var g in new[] { s[0].Game, s[1].Game }) foreach (var p in g.Players.Values) p.DevGod = true;
            s[0].Game.DevSetGod(1, true); s[0].Game.DevSetGod(2, true);
            Run(s, dt, 8, () => s[0].Game.State == MatchState.Fighting);
            var counts = new List<int>();
            bool boss = false;
            Run(s, dt, 40, () => s[0].Game.State != MatchState.Fighting, () =>
            {
                counts.Add(s[0].Game.Bullets.Values.Count(b => Defs.IsEnemyShot(b.Kind)));
                boss |= s[0].Game.Enemies.Values.Any(e => e.Def.IsBoss);
            });
            counts.Sort();
            Console.WriteLine("  wave {0,2}{1}: enemy bullets avg {2,4:0} median {3,4} p90 {4,4} max {5,4}", wave, boss ? " (boss)" : "       ",
                counts.Count > 0 ? counts.Average() : 0, counts.Count > 0 ? counts[counts.Count / 2] : 0, counts.Count > 0 ? counts[counts.Count * 9 / 10] : 0, counts.Count > 0 ? counts.Last() : 0);
            s[0].Leave(""); s[1].Leave("");
        }
    }

    // The bullet budget caps enemy bullets on screen; Homing Rounds only fix near misses and cost damage.
    static void BudgetHomingTest(float dt)
    {
        var s = StartBuffed(dt, 21, 6);
        s[0].Game.DevSetGod(1, true); s[0].Game.DevSetGod(2, true);
        Run(s, dt, 8, () => s[0].Game.State == MatchState.Fighting);
        int max = 0, budget = s[0].Game.ShotBudget;
        Run(s, dt, 20, () => s[0].Game.State != MatchState.Fighting, () =>
        {
            if (!s[0].Game.Enemies.Values.Any(e => e.Def.IsBoss))
                max = Math.Max(max, s[0].Game.Bullets.Values.Count(b => Defs.IsEnemyShot(b.Kind)));
        });
        Console.WriteLine("  wave 21: at most {0} enemy bullets on screen (budget {1}; it used to reach 800+)", max, budget);
        Check(budget > 100 && max <= budget * 1.35f, "enemy bullets stay near the bullet budget");
        s[0].Leave(""); s[1].Leave("");

        s = StartBuffed(dt, 3, 0);
        s[0].Game.Bot = s[1].Game.Bot = false;
        Run(s, dt, 6, () => s[0].Game.State == MatchState.Fighting);
        var g = s[0].Game;
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
        foreach (var e in g.Enemies.Values) e.Hp = 0f;
        Run(s, dt, 0.2f);
        var dummy = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, new V2(800f, 300f), 0f, 0, false });
        dummy.Hp = dummy.MaxHp = 1e6f;
        for (int k = 0; k < 3; k++) g.GrantUpgrade(2, Array.FindIndex(Upgrades.All, u => u.Name == "Homing Rounds"));
        Run(s, dt, 0.2f, null, () => { dummy.FrozenT = 1f; });
        // A near miss (aimed 15 degrees off, 200 px away) bends in; a shot fired sideways doesn't hunt it down.
        var near = (Bullet)shoot.Invoke(g, new object[] { Defs.DpsShot, new V2(800f, 500f), V2.FromAngle(-MathX.Pi / 2f + 0.26f) * 600f, 0f, 1.5f, 10f, 2, false, 0f });
        var wide = (Bullet)shoot.Invoke(g, new object[] { Defs.DpsShot, new V2(800f, 500f), V2.FromAngle(0f) * 600f, 0f, 1.5f, 10f, 2, false, 0f });
        float wideAngle0 = wide.Vel.Angle, hpBefore = dummy.Hp;
        Run(s, dt, 0.6f, null, () => { dummy.FrozenT = 1f; });
        bool nearHit = dummy.Hp < hpBefore;
        float wideTurn = Math.Abs(wide.Vel.Angle - wideAngle0);
        Console.WriteLine("  homing x3: near miss hit {0}, sideways shot turned {1:0.00} rad, shot damage {2:0.0} of 10", nearHit, wideTurn, near.Dmg);
        Check(nearHit && wideTurn < 0.05f, "Homing Rounds fix near misses but don't hunt enemies off to the side");
        Check(Math.Abs(near.Dmg - 7f) < 0.01f, "Homing Rounds cost 10% damage per stack");
        s[0].Leave(""); s[1].Leave("");
    }

    // Boss Lab from the command line: run.sh 1 1 lab <wave> <skill 1-3> <runs> <seat> [seat] [seat]
    // A seat is role/class/weapon, e.g. T/S/0 (Tank Soldier, first weapon), H/M/1, D/U/2 (U = summoner).
    public static List<LabOutcome> LabRun(int wave, BotSkill skill, int runs, string[] seats, bool quiet = false)
    {
        float dt = 1f / 60f;
        var outcomes = new List<LabOutcome>();
        float timeSum = 0f, hpSum = 0f;
        int downs = 0;
        var sources = new Dictionary<string, float>();
        for (int run = 0; run < runs; run++)
        {
            var lab = new BossLab { Wave = wave, Skill = skill, PlayerCount = seats.Length, Seed = 1000 + run };
            for (int i = 0; i < seats.Length; i++)
            {
                var parts = seats[i].Split('/');
                lab.Seats[i].Role = parts[0] == "T" ? Role.Tank : parts[0] == "H" ? Role.Healer : Role.Dps;
                lab.Seats[i].Kit = parts.Length > 1 && parts[1] == "M" ? Kit.Mage : parts.Length > 1 && parts[1] == "U" ? Kit.Summoner : Kit.Soldier;
                lab.Seats[i].Weapon = parts.Length > 2 ? int.Parse(parts[2]) : 0;
            }
            var host = new Session();
            lab.Start(host, nextPort++);
            for (int i = 0; i < 300 && !(host.Game.Running && host.Game.Players.Count >= seats.Length); i++)
            {
                Step(host, dt); lab.StepClients(dt); lab.UpdateClients(dt);
                Thread.Sleep(2);
            }
            int frames = 0;
            while (lab.Outcome == LabOutcome.Running && frames < (BossLab.TimeLimit + 30f) * 60)
            {
                Step(host, dt); lab.StepClients(dt); lab.UpdateClients(dt); lab.Watch(dt);
                frames++;
            }
            outcomes.Add(lab.Outcome);
            timeSum += lab.FightTime;
            hpSum += lab.BossHpFrac;
            foreach (var st in lab.Stats.Values)
            {
                downs += st.Downs;
                foreach (var kv in st.TakenFrom)
                {
                    float had; sources.TryGetValue(kv.Key, out had); sources[kv.Key] = had + kv.Value;
                }
            }
            if (!quiet)
            {
                Console.WriteLine("  run {0}: {1} vs {2} in {3:0}s, boss HP left {4:0}%, peak enemy bullets {5}, team boss DPS {6:0}", run + 1, lab.Outcome, lab.BossName, lab.FightTime, lab.BossHpFrac * 100f, lab.PeakBullets, lab.TeamBossDps);
                foreach (var st in lab.Stats.Values)
                    Console.WriteLine("     {0}: boss dmg {1:0}, taken {2:0}, healed {3:0}, downs {4}, abilities used {5}", st.Name, st.BossDamage, st.DamageTaken, st.Healing, st.Downs, st.Abilities);
            }
            lab.Stop();
        }
        Console.WriteLine("  wave {0} {1} [{2}] x{3}: won {4}, lost {5}, timed out {6}; avg fight {7:0}s, avg boss HP left {8:0}%, downs/run {9:0.0}",
            wave, skill, string.Join(" ", seats), runs, outcomes.Count(o => o == LabOutcome.Won), outcomes.Count(o => o == LabOutcome.Lost), outcomes.Count(o => o == LabOutcome.TimedOut),
            timeSum / runs, hpSum / runs * 100f, (float)downs / runs);
        Console.WriteLine("     damage taken from: " + string.Join(", ", sources.OrderByDescending(kv => kv.Value).Take(6).Select(kv => kv.Key + " " + (int)(kv.Value / runs))));
        return outcomes;
    }

    // Level (upgrade picks) a team of Good bots has when each boss wave starts.
    static void LevelReport(float dt)
    {
        var host = new Session { AutoStart = 2, StartWave = 1 };
        host.Game.Bot = true; host.Game.BotLevel = BotSkill.Good; host.Game.BotRerolls = false;
        int port = nextPort++;
        host.Host(port, "A", Role.Tank, 0);
        var c = new Session(); c.Game.Bot = true; c.Game.BotLevel = BotSkill.Good; c.Game.BotRerolls = false;
        c.Join("127.0.0.1", port, "B", Role.Dps, 0);
        var s = new[] { host, c };
        for (int i = 0; i < 300 && c.Screen != AppScreen.Game; i++) { Step(host, dt); Step(c, dt); Thread.Sleep(2); }
        int lastWave = 0;
        for (int f = 0; f < 60 * 60 * 40 && host.Game.State != MatchState.GameOver && host.Game.Wave <= 30; f++)
        {
            Step(host, dt); Step(c, dt);
            if (host.Game.Wave != lastWave && host.Game.State == MatchState.Fighting)
            {
                lastWave = host.Game.Wave;
                var ps = host.Game.Players.Values.ToList();
                Console.WriteLine("  wave {0,2} at {1,4:0}s: levels {2}, picks taken {3}, downs {4}", lastWave, host.Game.RunTime,
                    string.Join("/", ps.Select(p => p.Level)), string.Join("/", ps.Select(p => p.Taken.Count)), string.Join("/", ps.Select(p => p.Run.Downs)));
            }
        }
        Console.WriteLine("  ended: state {0} wave {1}", host.Game.State, host.Game.Wave);
        host.Leave(""); c.Leave("");
    }

    // Boss Lab: a party of smart bots fights the Warden, and the lab keeps score.
    static void BossLabTest(float dt)
    {
        var good = LabRun(5, BotSkill.Good, 1, new[] { "T/S/0", "H/M/1" }, true);
        Check(good[0] == LabOutcome.Won, "Boss Lab: Good bots (Mage Healer + Soldier Tank) beat the Warden");
        var lab = new BossLab { Wave = 15, Skill = BotSkill.Casual, PlayerCount = 3, Seed = 7 };
        lab.Seats[2].Kit = Kit.Summoner;
        var host = new Session();
        lab.Start(host, nextPort++);
        for (int i = 0; i < 300 && !(host.Game.Running && host.Game.Players.Count >= 3); i++) { Step(host, dt); lab.StepClients(dt); lab.UpdateClients(dt); Thread.Sleep(2); }
        for (int f = 0; f < 60 * 12; f++) { Step(host, dt); lab.StepClients(dt); lab.UpdateClients(dt); lab.Watch(dt); }
        var p3 = host.Game.Players.Values.FirstOrDefault(p => p.Kit == Kit.Summoner);
        Console.WriteLine("  lab: {0} players, boss {1}, fight {2:0.0}s, boss hp {3:0}%, picks each {4}, summoner {5}",
            host.Game.Players.Count, lab.BossName, lab.FightTime, lab.BossHpFrac * 100f, string.Join("/", host.Game.Players.Values.Select(p => p.Taken.Count)), p3 != null ? p3.Weapon.Name : "-");
        Check(host.Game.Players.Count == 3 && lab.BossName == "Twin Reapers" && lab.FightTime > 3f && lab.Stats.Count == 3, "Boss Lab sets up the chosen boss and party and keeps score");
        Check(host.Game.Players.Values.All(p => p.Taken.Count >= BossLab.TypicalPicks(15) + 4), "lab players get the typical upgrades, legendaries and mods for the wave");
        Check(lab.Clients.All(c => c.Game.BotLevel == BotSkill.Casual) && host.Game.BotLevel == BotSkill.Casual, "every lab seat is played by a bot of the chosen skill");
        lab.Stop();
        Check(host.Screen == AppScreen.Menu && lab.Clients.Count == 0, "stopping the lab closes every seat");
    }

    // Spinners spray in bursts with breathers in between (they used to spray nonstop).
    static void SpinnerTest(float dt)
    {
        var s = StartBuffed(dt, 6, 0);
        s[0].Game.Bot = s[1].Game.Bot = false;
        Run(s, dt, 6, () => s[0].Game.State == MatchState.Fighting);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var spinner = (Enemy)spawn.Invoke(s[0].Game, new object[] { EnemyType.Spinner, new V2(800f, 300f), 0.3f, 0, false });
        var seen = new HashSet<int>();
        var shotTimes = new List<float>();
        float t = 0f;
        bool clientBursting = false, clientResting = false;
        Run(s, dt, 10, null, () =>
        {
            t += dt;
            foreach (var e in s[0].Game.Enemies.Values)
                if (e != spinner) e.Hp = 0f;  // only the spinner's bullets
            foreach (var p in s[0].Game.Players.Values) p.InvulnT = 1f;
            foreach (var b in s[0].Game.Bullets.Values)
                if (b.Source == EnemyType.Spinner && seen.Add(b.Id))
                    shotTimes.Add(t);
            var ce = Find(s[1], EnemyType.Spinner);
            if (ce != null && !s[1].Game.IsSpawning(ce))
            {
                clientBursting |= ce.Phase == 1;
                clientResting |= ce.Phase == 0;
            }
        });
        float longestGap = 0f;
        int bursts = shotTimes.Count > 0 ? 1 : 0;
        for (int i = 1; i < shotTimes.Count; i++)
        {
            float gap = shotTimes[i] - shotTimes[i - 1];
            longestGap = Math.Max(longestGap, gap);
            if (gap > 1f) bursts++;
        }
        float perSecond = shotTimes.Count / 10f;
        Console.WriteLine("  spinner: {0} bullets in 10 s ({1:0.0}/s, was ~27/s+), {2} bursts, longest gap {3:0.0}s, client saw burst {4} / rest {5}",
            shotTimes.Count, perSecond, bursts, longestGap, clientBursting, clientResting);
        Check(bursts >= 2 && longestGap > 1.5f && perSecond < 20f, "Spinners fire in bursts with pauses between");
        Check(clientBursting && clientResting, "clients see when a Spinner is bursting");
        s[0].Leave(""); s[1].Leave("");
    }

    // Session recording: a run from wave 4 through the Warden is written to a JSON file with
    // waves, the boss fight, damage sources, picks and samples.
    static string SessionTest(float dt)
    {
        string folder = Path.Combine(Path.GetTempPath(), "arena-sessions-" + Guid.NewGuid().ToString("N").Substring(0, 6));
        int port = nextPort++;
        var host = new Session { AutoStart = 2, StartWave = 4 };
        host.Game.Bot = true;
        host.Game.RecordFolder = folder;
        host.Host(port, "Host", Role.Tank, 0, 0, Kit.Mage);
        var client = new Session();
        client.Game.Bot = true;
        client.Join("127.0.0.1", port, "Friend", Role.Dps);
        var s = new[] { host, client };
        for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
        Func<string, int> U = name => Array.FindIndex(Upgrades.All, u => u.Name == name);
        foreach (int id in new[] { 1, 2 })
        {
            for (int k = 0; k < 20; k++) host.Game.GrantUpgrade(id, U("Fortitude"));
            for (int k = 0; k < 15; k++) host.Game.GrantUpgrade(id, U("Hollow Points"));
        }
        Run(s, dt, 120, () => host.Game.Wave >= 6 && host.Game.State == MatchState.Fighting || host.Game.State == MatchState.GameOver);
        string path = host.Game.Recorder != null ? host.Game.Recorder.Path : null;
        host.Leave(""); client.Leave("");  // ends the recording ("left")
        bool ok = path != null && File.Exists(path);
        string text = ok ? File.ReadAllText(path) : "";
        foreach (var key in new[] { "\"waves\"", "\"bosses\"", "\"The Warden\"", "\"timeToKill\"", "\"picks\"", "\"damageTakenFrom\"", "\"samples\"", "\"outcome\": \"left\"", "\"enemyTypes\"" })
            ok &= text.Contains(key);
        int depth = 0;
        bool inStr = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (inStr) { if (c == '\\') i++; else if (c == '"') inStr = false; continue; }
            if (c == '"') inStr = true;
            else if (c == '{' || c == '[') depth++;
            else if (c == '}' || c == ']') depth--;
        }
        ok &= depth == 0 && !inStr;
        Console.WriteLine("  session file: {0} ({1:0.0} KB)", path, text.Length / 1024.0);
        Check(ok, "a recorded session has waves, the boss fight, damage sources, picks and samples, as valid JSON");
        string copy = Path.Combine(Path.GetTempPath(), "arena-last-session.json");
        if (ok)
            File.Copy(path, copy, true);
        try { Directory.Delete(folder, true); } catch (IOException) { }
        return ok ? copy : null;
    }

    // Every music track renders: a sensible length, no clipping or NaNs, and a seamless loop.
    static void MusicTest()
    {
        var recipes = new List<KeyValuePair<string, Func<float[]>>>();
        recipes.Add(new KeyValuePair<string, Func<float[]>>("menu", MenuMusic.Render));
        recipes.Add(new KeyValuePair<string, Func<float[]>>("battle", () => GameMusic.Render(GameMusic.Battle)));
        var bosses = new HashSet<EnemyType>();
        for (int wave = 5; wave <= Game.MaxWave; wave += 5)
            bosses.Add(Game.BossForWave(wave));
        foreach (var boss in bosses)
        {
            var b = boss;
            recipes.Add(new KeyValuePair<string, Func<float[]>>(b.ToString(), () => GameMusic.Render(GameMusic.ForBoss(b))));
        }
        bool ok = true;
        var lengths = new List<string>();
        foreach (var kv in recipes)
        {
            var s = kv.Value();
            float peak = 0f;
            bool nan = false;
            foreach (var v in s)
            {
                nan |= float.IsNaN(v) || float.IsInfinity(v);
                peak = Math.Max(peak, Math.Abs(v));
            }
            float secs = s.Length / (float)MenuMusic.Rate;
            bool good = !nan && peak <= 0.81f && peak > 0.2f && secs > 20f && secs < 60f && Math.Abs(s[0] - s[s.Length - 1]) < 0.1f;
            lengths.Add(kv.Key + " " + secs.ToString("0") + "s");
            if (!good) Console.WriteLine("  bad track: " + kv.Key + " peak " + peak + " length " + secs + " nan " + nan);
            ok &= good;
        }
        Console.WriteLine("  music: " + string.Join(", ", lengths.ToArray()));
        Check(ok && bosses.Count == 10, "menu, battle and all 10 boss tracks render cleanly and loop");
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
            var host = new Session { AutoStart = 2, StartWave = 9 };  // enemies tough enough to survive Glacial Tomb's hit
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
                host.Game.Players[id].InvulnT = 6f;  // don't get downed before casting
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
                foreach (var e in client.Game.Enemies.Values)
                    frozen |= e.Frozen;  // the client sees them frozen (drawn in ice with a snowflake)
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
        Check(offer != null && offer.Length == 8, "the weapon mod offer lists all 8 mods");
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
            var s = StartBuffed(dt, 14, 0);  // all shooters (early waves are melee)
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
        // The Warden's fourth attack: it dashes to a spot, plants, and fires straight streams up, down,
        // left and right (a static "+" that cuts the arena into quadrants), then moves and does it again.
        {
            var s = StartBuffed(dt, 5, 0);
            Run(s, dt, 10, () => { var b = Find(s[0], EnemyType.Boss); return b != null && b.SpawnT <= 0f; });
            var w = Find(s[0], EnemyType.Boss);
            s[0].Game.Bot = s[1].Game.Bot = false;
            w.T = 17.9f;  // jump to just before the fourth attack (the first three last 6 s each)
            V2 start = w.Pos;
            float farthest = 0f, movedWhileFiring = 0f, crossTime = 0f, guidesWhileMoving = 0f;
            V2 lastPos = w.Pos;
            bool phaseOnClient = false, movingOnClient = false;
            var plants = new List<V2>();
            int onAxes = 0, offAxes = 0;
            var seen = new HashSet<int>();
            Run(s, dt, 13f, null, () =>
            {
                foreach (var p in s[0].Game.Players.Values)
                    p.InvulnT = 1f;
                var cw = Find(s[1], EnemyType.Boss);
                phaseOnClient |= cw != null && cw.Phase == 3;
                movingOnClient |= cw != null && cw.Phase == 4;
                if (w.Phase == 3 || w.Phase == 4)
                    crossTime += dt;
                if (w.Phase == 3 && w.Pos.DistanceTo(lastPos) > 0.5f)
                    guidesWhileMoving += dt;
                if (w.Phase != 3)
                {
                    lastPos = w.Pos;
                    return;
                }
                if (plants.Count == 0 || plants[plants.Count - 1].DistanceTo(w.Pos) > 100f)
                    plants.Add(w.Pos);
                farthest = Math.Max(farthest, w.Pos.DistanceTo(start));
                bool fired = false;
                foreach (var b in s[0].Game.Bullets.Values)
                    if (b.Kind == Defs.SmallShot && b.FromBoss && seen.Add(b.Id) && b.Pos.DistanceTo(w.Pos) < w.Radius + 12f)
                    {
                        fired = true;
                        // Each shot leaves straight along one of the four axes.
                        float rel = MathX.Fmod(b.Vel.Angle + MathX.Pi / 4f, MathX.Pi / 2f) - MathX.Pi / 4f;
                        if (Math.Abs(rel) < 0.01f && b.AngVel == 0f) onAxes++; else offAxes++;
                    }
                if (fired)
                    movedWhileFiring = Math.Max(movedWhileFiring, w.Pos.DistanceTo(lastPos));
                lastPos = w.Pos;
            });
            Console.WriteLine("  warden cross: lasted {0:0.0}s, {1} plants, client saw firing {2} / moving {3}, moved {4:0}px between plants, {5:0.00}s of guides while moving, moved {6:0.0}px per tick while firing, shots on the axes {7}, off {8}",
                crossTime, plants.Count, phaseOnClient, movingOnClient, farthest, guidesWhileMoving, movedWhileFiring, onAxes, offAxes);
            Check(phaseOnClient && movingOnClient && farthest > 150f, "the Warden's cross attack moves it to new spots (seen on client)");
            Check(Math.Abs(crossTime - 12f) < 0.3f && plants.Count >= 4, "the Warden's cross lasts 12 s across four plants");
            Check(guidesWhileMoving == 0f, "the cross guides are hidden while the Warden moves");
            Check(onAxes > 100 && offAxes == 0 && movedWhileFiring < 0.5f, "the Warden's cross is a static '+' along the axes, fired while it stands still");
            s[0].Leave(""); s[1].Leave("");
        }
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
        // Her drones don't hold up the fight: with all of them still alive, she carries on with
        // bombers and bullets, and stays shielded the whole time.
        {
            var s = StartBuffed(dt, 10, 0);
            Run(s, dt, 10, () => { var qq = Find(s[0], EnemyType.HiveQueen); return qq != null && qq.SpawnT <= 0f; });
            s[0].Game.Bot = s[1].Game.Bot = false;
            var q = Find(s[0], EnemyType.HiveQueen);
            var seenStages = new HashSet<int>();
            bool alwaysShielded = true, bombers = false;
            Run(s, dt, 14f, null, () =>
            {
                foreach (var p in s[0].Game.Players.Values) p.InvulnT = 1f;
                seenStages.Add((int)q.T2);
                bombers |= Find(s[0], EnemyType.HiveBomber) != null;
                if ((int)q.T2 >= 2 && Find(s[0], EnemyType.Shard) != null) alwaysShielded &= q.Phase == 1;  // while bombing and shooting
            });
            int drones = 0;
            foreach (var e in s[0].Game.Enemies.Values) if (e.Type == EnemyType.Shard) drones++;
            Console.WriteLine("  queen with her drones alive: stages seen {0}, bombers {1}, {2} drones at the end, shielded throughout {3}",
                string.Join(",", seenStages.OrderBy(x => x)), bombers, drones, alwaysShielded);
            Check(seenStages.Contains(2) && seenStages.Contains(3) && bombers && drones > 0 && alwaysShielded,
                "the Hive Queen's drones don't pause her: bombers and bullets carry on while they live (and she stays shielded)");
            s[0].Leave(""); s[1].Leave("");
        }
        // Hive Queen's loop: shooting drones (shielded until they're all dead), then explosive drones
        // aimed at where each player was when they launched, then her bullet patterns, then again.
        {
            var s = StartBuffed(dt, 10, 0);
            Run(s, dt, 10, () => { var qq = Find(s[0], EnemyType.HiveQueen); return qq != null && qq.SpawnT <= 0f; });
            s[0].Game.Bot = s[1].Game.Bot = false;
            var q = Find(s[0], EnemyType.HiveQueen);
            Check(q.Pos.DistanceTo(new V2(Game.Width / 2f, Game.Height / 2f)) < 40f, "bosses appear in the middle of the arena");
            q.Pos = new V2(800f, 140f);  // she spawns in the middle; move her clear of the runners' circles
            Run(s, dt, 1.5f);
            bool shieldUp = q.Phase == 1 && Find(s[0], EnemyType.Shard) != null && (int)q.T2 == 1;
            bool bombersEarly = Find(s[0], EnemyType.HiveBomber) != null;
            foreach (var e in s[0].Game.Enemies.Values)
                if (e.Type == EnemyType.Shard) e.Hp = 0f;
            var stages = new List<int> { (int)q.T2 };
            var aimedAt = new Dictionary<int, V2>();
            var lastPos = new Dictionary<int, V2>();
            int launched = 0, onTarget = 0, offTarget = 0, aimedRight = 0, fans = 0;
            bool clientBomber = false, clientMarker = false, resummoned = false;
            float hpBefore = 1f, hpAfter = 1f, t = 0f;  // minus damage taken so far, when the bomber stage starts / ends
            Run(s, dt, 26f, null, () =>
            {
                t += dt;
                int st = (int)q.T2;
                if (stages[stages.Count - 1] != st) stages.Add(st);
                // Both players keep running in circles.
                int k = 0;
                foreach (var p in s[0].Game.Players.Values)
                {
                    // Separate circles, so neither runs into the spot a bomber was sent at the other.
                    p.Pos = new V2(800f, 480f) + V2.FromAngle(t * 1.1f + k * MathX.Pi) * (k == 0 ? 300f : 130f);
                    k++;
                    Player cp;
                    if (s[1].Game.Players.TryGetValue(p.Id, out cp)) cp.Pos = p.Pos;  // the joining player reports its own position
                    if (st != 2) p.Hp = p.MaxHp;
                }
                if (st == 2 && hpBefore > 0f) hpBefore = -(s[0].Game.Players[1].Run.DamageTaken + s[0].Game.Players[2].Run.DamageTaken);
                if (st == 3 && hpAfter > 0f) hpAfter = -(s[0].Game.Players[1].Run.DamageTaken + s[0].Game.Players[2].Run.DamageTaken);
                if (st == 1 && stages.Count > 3) resummoned = true;
                foreach (var e in s[0].Game.Enemies.Values)
                {
                    if (e.Type != EnemyType.HiveBomber) continue;
                    if (!aimedAt.ContainsKey(e.Id))
                    {
                        launched++;
                        aimedAt[e.Id] = e.Waypoint;
                        foreach (var p in s[0].Game.Players.Values)
                            if (p.Pos.DistanceTo(e.Waypoint) < 15f) { aimedRight++; break; }  // within a frame or two of running
                    }
                    lastPos[e.Id] = e.Pos;
                }
                foreach (var id in new List<int>(lastPos.Keys))
                    if (!s[0].Game.Enemies.ContainsKey(id))
                    {
                        if (lastPos[id].DistanceTo(aimedAt[id]) < 25f) onTarget++; else offTarget++;
                        lastPos.Remove(id);
                    }
                foreach (var b in s[0].Game.Bullets.Values)
                    if (b.Kind == Defs.PierceShot && b.Source == EnemyType.HiveQueen) { fans++; break; }
                clientBomber |= Find(s[1], EnemyType.HiveBomber) != null;
                foreach (var fx in s[1].Game.Effects) clientMarker |= fx.Kind == FxKind.Target;
            });
            Console.WriteLine("  queen loop: stages {0}; {1} bombers launched ({2} aimed at a player's spot), {3} blew up on their spot, {4} elsewhere; running players lost {5:0} HP to them; fans {6}",
                string.Join(">", stages), launched, aimedRight, onTarget, offTarget, hpBefore - hpAfter, fans);
            Check(shieldUp && !bombersEarly, "the Hive Queen opens with shooting drones and is shielded while they live");
            Check(stages.Count >= 4 && stages[0] == 1 && stages[1] == 2 && stages[2] == 3 && resummoned, "the Hive Queen loops: drones, then bombers, then bullets, then drones again");
            Check(launched >= 8 && aimedRight == launched && onTarget >= launched - 2 && clientBomber && clientMarker,
                "Hive bombers fly to where a player was when they launched and blow up there (seen on client, with a marker)");
            Check(hpBefore - hpAfter < 45f && fans > 0, "keeping on the move dodges the bombers; her bullet patterns follow");
            // ...but standing still gets you blown up.
            {
                foreach (var e in s[0].Game.Enemies.Values) if (e.Type != EnemyType.HiveQueen) e.Hp = 0f;
                q.T2 = 1; q.PhaseT = 0f;  // hold her in the drone stage (no drones left to shoot)
                Run(s, dt, 0.1f);
                var still = s[0].Game.Players[1];
                still.Hp = still.MaxHp; still.InvulnT = 0f;
                typeof(Game).GetMethod("LaunchBomber", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .Invoke(s[0].Game, new object[] { q, still.Pos });
                V2 at = still.Pos;
                float taken0 = still.Run.DamageTaken;
                Run(s, dt, 2.5f, null, () => { still.Pos = at; if ((int)q.T2 != 1) { q.T2 = 1; q.PhaseT = 0f; } });
                Check(still.Run.DamageTaken - taken0 >= 20f, "a Hive bomber blows up a player who stands still");
            }
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
        if (args.Length > 2 && args[2] == "extra")
        {
            WeaponExtraTest(1f / 60f);
            Console.WriteLine(failures == 0 ? "ALL PASSED" : failures + " FAILED");
            return failures == 0 ? 0 : 1;
        }
        if (args.Length > 2 && args[2] == "revolver")
        {
            RevolverReloadTest(1f / 60f);
            Console.WriteLine(failures == 0 ? "ALL PASSED" : failures + " FAILED");
            return failures == 0 ? 0 : 1;
        }
        if (args.Length > 2 && args[2] == "bow")
        {
            BowTest(1f / 60f);
            Console.WriteLine(failures == 0 ? "ALL PASSED" : failures + " FAILED");
            return failures == 0 ? 0 : 1;
        }
        if (args.Length > 2 && args[2] == "keys")
        {
            KeybindTest(1f / 60f);
            Console.WriteLine(failures == 0 ? "ALL PASSED" : failures + " FAILED");
            return failures == 0 ? 0 : 1;
        }
        if (args.Length > 2 && args[2] == "bulletcount")
        {
            BulletCountReport(1f / 60f);
            return 0;
        }
        if (args.Length > 2 && args[2] == "levels")
        {
            LevelReport(1f / 60f);
            return 0;
        }
        if (args.Length > 2 && args[2] == "labtest")
        {
            BossLabTest(1f / 60f);
            Console.WriteLine(failures == 0 ? "ALL PASSED" : failures + " FAILED");
            return failures == 0 ? 0 : 1;
        }
        if (args.Length > 2 && args[2] == "lab")
        {
            LabRun(int.Parse(args[3]), (BotSkill)int.Parse(args[4]), int.Parse(args[5]), args.Skip(6).ToArray());
            return 0;
        }
        if (args.Length > 2 && args[2] == "budget")
        {
            BudgetHomingTest(1f / 60f);
            Console.WriteLine(failures == 0 ? "ALL PASSED" : failures + " FAILED");
            return failures == 0 ? 0 : 1;
        }
        if (args.Length > 2 && args[2] == "mastery")
        {
            MasteryTest(1f / 60f);
            Console.WriteLine(failures == 0 ? "ALL PASSED" : failures + " FAILED");
            return failures == 0 ? 0 : 1;
        }
        if (args.Length > 2 && args[2] == "dev")
        {
            DevToolsTest(1f / 60f);
            Console.WriteLine(failures == 0 ? "ALL PASSED" : failures + " FAILED");
            return failures == 0 ? 0 : 1;
        }
        if (args.Length > 2 && args[2] == "spinner")
        {
            SpinnerTest(1f / 60f);
            Console.WriteLine(failures == 0 ? "ALL PASSED" : failures + " FAILED");
            return failures == 0 ? 0 : 1;
        }
        if (args.Length > 2 && args[2] == "aim")
        {
            AimTest(1f / 60f);
            Console.WriteLine(failures == 0 ? "ALL PASSED" : failures + " FAILED");
            return failures == 0 ? 0 : 1;
        }
        if (args.Length > 2 && args[2] == "session")
        {
            var path = SessionTest(1f / 60f);
            if (args.Length > 3 && path != null)
                File.Copy(path, args[3], true);
            Console.WriteLine(failures == 0 ? "ALL PASSED" : failures + " FAILED");
            return failures == 0 ? 0 : 1;
        }
        if (args.Length > 2 && args[2] == "parry")
        {
            ParryTest(1f / 60f);
            ParryUpgradeTest(1f / 60f);
            Console.WriteLine(failures == 0 ? "ALL PASSED" : failures + " FAILED");
            return failures == 0 ? 0 : 1;
        }
        if (args.Length > 2 && args[2] == "music")
        {
            MusicTest();
        ParryTest(dt);
        SessionTest(dt);
        ParryUpgradeTest(dt);
        AimTest(dt);
        SpinnerTest(dt);
        DevToolsTest(dt);
        MasteryTest(dt);
        BudgetHomingTest(dt);
        BossLabTest(dt);
        WeaponExtraTest(dt);
        KeybindTest(dt);
        RevolverReloadTest(dt);
        BowTest(dt);
            Console.WriteLine(failures == 0 ? "ALL PASSED" : failures + " FAILED");
            return failures == 0 ? 0 : 1;
        }
        if (args.Length > 2 && args[2] == "melee")
        {
            MeleeTest(1f / 60f);
            Console.WriteLine(failures == 0 ? "ALL PASSED" : failures + " FAILED");
            return failures == 0 ? 0 : 1;
        }
        if (args.Length > 2 && args[2] == "three")
        {
            ThreePlayerTest(1f / 60f);
            Console.WriteLine(failures == 0 ? "ALL PASSED" : failures + " FAILED");
            return failures == 0 ? 0 : 1;
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
        ThreePlayerTest(dt);
        MeleeTest(dt);
        MusicTest();
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
