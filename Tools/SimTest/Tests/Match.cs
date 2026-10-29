using System.Linq;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using Arena;

// The core match: a host and a friend over real TCP, in sync; joining, versions, restarts, pausing, run stats,
// keys, ready-up, wrapping, three players, session recording, the dev tools, and boss exploits.
static partial class Program
{
    // The core match: a host and a friend, both bots, over real localhost TCP from startWave for `seconds`; the client
    // must stay in sync. Then joining rules, new abilities, going back to the lobby, a second match, the host leaving,
    // and the core sweeps.
    static void CoreMatchTest(int startWave, int seconds)
    {
        int port = 7790;
        const float dt = 1f / 60f;

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
        bool extra = true;
        var missing = new List<string>();
        foreach (var key in new[] { "\"format\": 2", "\"mode\": \"run\"", "\"paths\"", "\"afterWave\": 5", "\"chosen\": \"", "\"healingReceivedFrom\"", "\"regeneration\"",
                                    "\"devTools\"", "bot plays for the host: on", "\"abilityMastery\"", "\"levelEnd\"" })
            if (!text.Contains(key)) { extra = false; missing.Add(key); }
        if (missing.Count > 0) Console.WriteLine("  missing: " + string.Join(" ", missing));
        Check(extra, "it also has the mode, the path choices, healing received by source, dev tool use and each player's final abilities");
        string copy = Path.Combine(Path.GetTempPath(), "arena-last-session.json");
        if (ok)
            File.Copy(path, copy, true);
        try { Directory.Delete(folder, true); } catch (IOException) { }
        return ok ? copy : null;
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
            int escaped = 0, frame = 0, bossShots = 0, plain = 0;
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
                {
                    foreach (var bl in s[0].Game.Bullets.Values)
                        if (Defs.IsEnemyShot(bl.Kind) && bl.Pos.DistanceTo(b.Pos) > 200f)
                            escaped++;
                    // The client is told which bullets are the boss's, so it can draw them differently.
                    foreach (var bl in s[1].Game.Bullets.Values)
                        if (Defs.IsEnemyShot(bl.Kind))
                        {
                            if (bl.FromBoss) bossShots++; else plain++;
                        }
                }
            });
            Console.WriteLine("  {0} on the boss: {1} boss bullet sightings beyond 200 px", mode, escaped);
            if (mode == "barrier")
            {
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
}
