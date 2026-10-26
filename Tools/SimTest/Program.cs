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
        s.Game.Sounds.Clear();
    }

    static void CheckOffers()
    {
        var rng = new Random(1);
        bool ok = true;
        var rarityCount = new int[4];
        foreach (Role role in new[] { Role.Tank, Role.Healer, Role.Dps })
        {
            var taken = new List<int>();
            for (int level = 1; level < 60; level++)
            {
                var offer = Upgrades.RollOffer(rng, role, taken, level);
                var seen = new HashSet<int>();
                foreach (int id in offer)
                {
                    ok &= id >= 0 && seen.Add(id) && Upgrades.Available(id, role, taken);
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
            ok &= grants <= Upgrades.SlotCount - 1;
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
        bool bossSeen = false, bossDead = false;
        for (int i = 0; i < 60 * 60 && !bossDead; i++)
        {
            Step(host, dt);
            Step(client, dt);
            if (i % 2 == 0)
                Thread.Sleep(1);
            bool bossAlive = false;
            foreach (var e in host.Game.Enemies.Values)
                bossAlive |= e.Type == EnemyType.Boss;
            bossSeen |= bossAlive;
            bossDead = bossSeen && !bossAlive;
            if (host.Game.State == MatchState.GameOver)
                break;
        }
        for (int i = 0; i < 20; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
        Console.WriteLine("  boss dead: {0}, rerolls host {1}/{2}, client sees {3}/{4}", bossDead, host.Game.Players[1].Rerolls, host.Game.Players[2].Rerolls,
            client.Game.Players[1].Rerolls, client.Game.Players[2].Rerolls);
        Check(bossDead && host.Game.Players[1].Rerolls == 4 && client.Game.Players[2].Rerolls == 4, "boss kill gives everyone +2 rerolls");
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
                string names = Defs.Weapon(roles[0], w).Name + " + " + Defs.Weapon(roles[1], w).Name;
                Check(fired > 0 && ghosts == 0, "weapons fire and stay in sync: " + names);
                host.Leave("");
                client.Leave("");
            }
        }
    }

    static int Main(string[] args)
    {
        int startWave = args.Length > 0 ? int.Parse(args[0]) : 1;
        int seconds = args.Length > 1 ? int.Parse(args[1]) : 60;
        int port = 7790;
        const float dt = 1f / 60f;

        CheckOffers();

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
        for (int i = 0; i < seconds * 60; i++)
        {
            Step(host, dt);
            Step(client, dt);
            if (i % 2 == 0)
                Thread.Sleep(1);
            maxBullets = Math.Max(maxBullets, host.Game.Bullets.Count);
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
                if (Upgrades.Available(U(name), p.Role, p.Taken))
                    host.Game.GrantUpgrade(id, U(name));
            };
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
                turretsSeen = Math.Max(turretsSeen, client.Game.Turrets.Count);
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
                for (int k = 1; k < Upgrades.SlotCount; k++)
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

        Console.WriteLine(failures == 0 ? "ALL PASSED" : failures + " FAILED");
        return failures == 0 ? 0 : 1;
    }
}
