using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using Arena;

// The Boss Hunt (Hunts.cs) and the Zombie Infection modifier (Zombies.cs).
static partial class Program
{
    static void HuntTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

        // Locked until the boss is beaten in a run: the lobby can pick Boss Hunt, but it won't start.
        int port = nextPort++;
        var solo = new Session();
        var fresh = solo.Game.ProfileOverride = new Profile();
        solo.Host(port, "Host", Role.Tank);
        solo.SetMode(false, true);
        solo.RequestReady(true);
        for (int i = 0; i < 20; i++) { solo.Poll(); Thread.Sleep(2); }
        solo.StartMatch();
        Check(solo.HuntMode && solo.LobbyHunt == null && !solo.Game.Running && solo.Screen == AppScreen.Lobby,
            "a Boss Hunt is locked until its boss is beaten in a run: picking it, the match won't start");
        fresh.BossBeaten(EnemyType.Boss, 5);
        Check(solo.LobbyHunt != null && solo.LobbyHunt.Boss == EnemyType.Boss, "beating the Warden in a run opens the Warden Hunt");
        solo.Leave("");

        // The hunt itself, in co-op: the friend's lobby and game know it's a hunt.
        port = nextPort++;
        var host = new Session();
        var hp = host.Game.ProfileOverride = new Profile();
        hp.BossBeaten(EnemyType.Boss, 5);
        host.Host(port, "Host", Role.Tank);
        var client = new Session();
        var cp = client.Game.ProfileOverride = new Profile();  // the friend hasn't beaten it: the host's unlock is what counts
        client.Join("127.0.0.1", port, "Friend", Role.Dps);
        var s = new[] { host, client };
        Action Sync = () => { for (int i = 0; i < 40; i++) { host.Poll(); client.Poll(); Thread.Sleep(2); } };
        for (int i = 0; i < 300 && client.Screen != AppScreen.Lobby; i++) { host.Poll(); client.Poll(); Thread.Sleep(2); }
        Sync();
        host.SetMode(false, true);
        host.SetHuntBoss(EnemyType.HiveQueen);  // no hunt for her yet: ignored
        Sync();
        Check(client.HuntMode && client.HuntBoss == EnemyType.Boss && host.HuntBoss == EnemyType.Boss, "the friend's lobby shows the Boss Hunt and its boss (only bosses with a hunt can be picked)");
        client.RequestReady(true); host.RequestReady(true); Sync();
        host.StartMatch();
        for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
        var g = host.Game;
        var c = client.Game;
        g.ChallengesOn = true;  // (on, to show a hunt opens no portal)
        Check(g.Hunt != null && c.Hunt != null && c.Hunt.Boss == EnemyType.Boss && hp.RunsStarted == 0 && cp.RunsStarted == 0,
            "the hunt starts on every game, and it doesn't count as a run started");
        Check(g.ApproachShare(1) > 0.3f && Math.Abs(g.ApproachShare(4) - 1f) < 0.01f, "a hunt's waves are its boss's own enemies, building up to fully themed");

        // Through the waves (enemies cleared as they come, picks skipped), to the Warden.
        Action keep = () =>
        {
            foreach (var p in g.Players.Values) { p.InvulnT = 1f; p.PendingPicks = p.PendingLegendary = p.PendingMods = p.PrizePicks = 0; p.Offer = null; p.LegendaryQueue.Clear(); }
            foreach (var e in g.Enemies.Values) if (!e.Def.IsBoss && g.Enemies.Count > 0) e.Hp = 0f;
        };
        bool portal = false, pathOffered = false;
        Run(s, dt, 200f, () => g.Wave == 5 && Find(host, EnemyType.Boss) != null && !g.IsSpawning(Find(host, EnemyType.Boss)), () =>
        {
            keep();
            portal |= g.Room != RoomPhase.None;
            pathOffered |= g.PathOffer != null;
        });
        Check(g.Wave == 5 && Find(host, EnemyType.Boss) != null && c.HuntWave == 5 && !portal, "four waves, then the Warden on the hunt's fifth (and no challenge portal on the way)");
        var saveable = (bool)typeof(Game).GetProperty("Saveable", flags).GetValue(g, null);
        Check(!saveable, "a hunt isn't saved to continue later");
        Find(host, EnemyType.Boss).Hp = 0f;
        Run(s, dt, 6f, () => c.State == MatchState.Victory && cp.HuntPlayed(EnemyType.Boss) > 0, () => { keep(); pathOffered |= g.PathOffer != null; });
        Console.WriteLine("  hunt: host won {0} of {1} (best {2:0.0} s), friend won {3} of {4}; runs won {5}/{6}",
            hp.HuntWon(EnemyType.Boss), hp.HuntPlayed(EnemyType.Boss), hp.HuntBestTime(EnemyType.Boss), cp.HuntWon(EnemyType.Boss), cp.HuntPlayed(EnemyType.Boss), hp.RunsWon, cp.RunsWon);
        Check(g.State == MatchState.Victory && c.State == MatchState.Victory && !pathOffered, "beating the Warden wins the hunt (no path offer after it)");
        Check(hp.HuntWon(EnemyType.Boss) == 1 && cp.HuntWon(EnemyType.Boss) == 1 && hp.HuntBestTime(EnemyType.Boss) > 0f && hp.RunsWon == 0 && cp.RunsWon == 0,
            "every player's profile records the hunt won (with a best time), not a run won");
        g.ContinueEndless();
        Run(s, dt, 0.5f);
        Check(g.State == MatchState.Victory && !g.Endless, "a won hunt has no endless");
        var back = Profile.Parse(hp.ToText());
        Check(back.HuntWon(EnemyType.Boss) == 1 && back.HuntPlayed(EnemyType.Boss) == 1 && Math.Abs(back.HuntBestTime(EnemyType.Boss) - hp.HuntBestTime(EnemyType.Boss)) < 0.01f,
            "hunt records read back from the profile's text");
        host.Leave(""); client.Leave("");
    }

    static void ZombieTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var xpScale = typeof(Game).GetMethod("ZombieXpScale", flags);
        int port = nextPort++;
        var host = new Session { StartWave = 3 };
        host.Host(port, "Host", Role.Tank);
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Dps);
        var s = new[] { host, client };
        Action Sync = () => { for (int i = 0; i < 40; i++) { host.Poll(); client.Poll(); Thread.Sleep(2); } };
        for (int i = 0; i < 300 && client.Screen != AppScreen.Lobby; i++) { host.Poll(); client.Poll(); Thread.Sleep(2); }
        Sync();
        host.SetModifier(RunMod.ZombieInfection, true);
        Sync();
        client.RequestReady(true); host.RequestReady(true); Sync();
        host.StartMatch();
        for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
        var g = host.Game;
        var c = client.Game;
        Action safe = () => { foreach (var p in g.Players.Values) { p.InvulnT = 1f; p.PendingPicks = 0; p.Offer = null; } };
        Run(s, dt, 8f, () => g.State == MatchState.Fighting, safe);
        var queue = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
        var all = queue.Concat(g.Enemies.Values.Select(e => e.Type)).ToList();
        // Wave 3, two players: 10 + 4 for an early wave = 14 usually; a horde has x1.66 of that.
        int expect = (int)Math.Round(14 * Zombies.CountMult(3));
        Console.WriteLine("  wave 3 horde: {0} enemies (expected {1}): {2}", all.Count, expect, string.Join(",", all.GroupBy(t => t).Select(k => k.Key + " x" + k.Count())));
        Check(c.Has(RunMod.ZombieInfection) && all.Count >= expect - 1 && all.All(t => t == EnemyType.Swarmer || t == EnemyType.Brute || t == EnemyType.Lunger),
            "Zombie Infection: a regular wave is a bigger horde of melee enemies only (and the friend's game knows the modifier)");
        Check(Math.Abs((float)xpScale.Invoke(g, new object[] { g.Enemies.Values.FirstOrDefault() ?? (Enemy)spawn.Invoke(g, new object[] { EnemyType.Swarmer, new V2(800f, 450f), 0f, 0, false }) }) - 1f / Zombies.CountMult(3)) < 0.001f,
            "a horde zombie's XP is split so levels keep pace with a normal run");

        // The cap: however many are waiting, no more than Cap at once.
        for (int i = 0; i < 120; i++) queue.Add(EnemyType.Brute);
        int most = 0;
        Run(s, dt, 50f, null, () => { safe(); most = Math.Max(most, g.Enemies.Count); });
        Console.WriteLine("  cap: at most {0} alive at once ({1} still waiting)", most, queue.Count);
        Check(most <= Zombies.Cap + 2 && most >= Zombies.Cap - 5 && queue.Count > 0, "the horde is capped at " + Zombies.Cap + " alive at once; the rest wait their turn");

        // The dead rise: a quarter of those shot down get back up after a moment, at half health, worth no XP.
        queue.Clear();
        foreach (var e in g.Enemies.Values) e.Hp = 0f;
        Run(s, dt, 3.5f, null, () => { safe(); foreach (var e in g.Enemies.Values) if (!e.Risen) e.Hp = 0f; });
        foreach (var e in g.Enemies.Values.ToList()) g.Enemies.Remove(e.Id);
        var anchor = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, new V2(60f, 60f), 0f, 0, false });  // keeps the wave going
        anchor.MaxHp = anchor.Hp = 1e6f; anchor.Speed = 0f;
        Func<int, bool, int> killBatch = (n, burning) =>
        {
            for (int i = 0; i < n; i++)
            {
                var z = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Swarmer, new V2(300f + 20f * (i % 40), 300f + 10f * (i / 40)), 0f, 0, false });
                z.SpawnT = 0f; z.LastHitBy = 1; z.Hp = 0f;
                if (burning) z.BurnT = 1f;
            }
            Run(s, dt, 0.1f, null, safe);
            Run(s, dt, Zombies.RiseTime - 0.3f, null, safe);
            bool waiting = g.ZombiesRising;
            Run(s, dt, 0.6f, null, () => { safe(); anchor.Hp = anchor.MaxHp; });
            int risen = g.Enemies.Values.Count(e => e.Risen);
            if (!waiting && risen > 0) risen = -1;  // they should have been waiting first
            foreach (var e in g.Enemies.Values.Where(e => e.Risen).ToList()) g.Enemies.Remove(e.Id);
            return risen;
        };
        var sample = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Swarmer, new V2(800f, 450f), 0f, 0, false });
        int rose = killBatch(80, false);
        Console.WriteLine("  80 shot down: {0} got back up", rose);
        Check(rose >= 8 && rose <= 35, "the dead rise: about a quarter of the zombies killed get back up a few seconds later");
        g.Enemies.Remove(sample.Id);
        var r = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Swarmer, new V2(800f, 450f), 0f, 0, false });
        r.Risen = true;
        Check((float)xpScale.Invoke(g, new object[] { r }) == 0f, "a risen zombie gives no XP");
        g.Enemies.Remove(r.Id);
        int roseBurning = killBatch(40, true);
        Check(roseBurning == 0, "burned zombies stay down");

        // The wave waits for the dead to get up.
        anchor.Hp = 0f;  // the last one standing goes down with the rest
        for (int i = 0; i < 30; i++)
        {
            var z = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Swarmer, new V2(400f + 10f * i, 400f), 0f, 0, false });
            z.SpawnT = 0f; z.LastHitBy = 1; z.Hp = 0f;
        }
        Run(s, dt, 0.2f, null, safe);
        bool held = g.ZombiesRising && g.State == MatchState.Fighting && g.Remaining > 0 && g.Enemies.Count == 0;
        Run(s, dt, Zombies.RiseTime + 1f, null, safe);
        Check(held && g.Enemies.Values.Any(e => e.Risen && Math.Abs(e.Hp - e.MaxHp * Zombies.RiseHp) < 0.01f), "the wave isn't over while the dead are still getting up, and they rise at half health");
        host.Leave(""); client.Leave("");
    }
}

static partial class Program
{
    /// A shot fired in the same tick as the enemy bullets are cleared (a wave won, a boss changing stage) must not
    /// reach the clients after the clear: it would stay on their screens and never go (it did, from the Duelist's blades).
    static void ClearGhostsTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var s = StartBuffed(dt, 3, 0);
        var g = s[0].Game;
        Run(s, dt, 8f, () => g.State == MatchState.Fighting, () => { foreach (var p in g.Players.Values) p.InvulnT = 1f; });
        var spawn = typeof(Game).GetMethod("SpawnBullet", flags);
        var b = (Bullet)spawn.Invoke(g, new object[] { Defs.SmallShot, new V2(800f, 450f), new V2(50f, 0f), 0f, 8f, 5f, 0, true, 0f });
        typeof(Game).GetMethod("WaveCleared", flags).Invoke(g, null);  // clears the enemy bullets and tells everyone, before the tick's spawns go out
        Run(s, dt, 1f);
        bool hostGone = !g.Bullets.ContainsKey(b.Id), clientGone = !s[1].Game.Bullets.ContainsKey(b.Id);
        Console.WriteLine("  shot fired then cleared in one tick: gone on the host {0}, on the client {1}; ghosts {2}", hostGone, clientGone, Ghosts(s));
        Check(hostGone && clientGone && Ghosts(s) == 0, "a shot fired in the same tick enemy bullets are cleared doesn't linger on the client");
        s[0].Leave(""); s[1].Leave("");
    }
}
