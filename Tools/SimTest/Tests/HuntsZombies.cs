using System.IO;
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
        string wasFolder = Profile.Folder, huntDir = Path.Combine(Path.GetTempPath(), "arena-hunt-" + Guid.NewGuid().ToString("N"));
        Profile.Folder = huntDir;  // (so the hunt's curve is written)
        var host = new Session();
        var hp = host.Game.ProfileOverride = new Profile();
        hp.BossBeaten(EnemyType.Boss, 5);
        host.Host(port, "Host", Role.Tank);
        var client = new Session();
        var cp = client.Game.ProfileOverride = new Profile();  // the friend hasn't beaten it: the host's unlock is what counts
        client.Join("127.0.0.1", port, "Friend", Role.Dps);
        var s = new[] { host, client };
        Action Sync = () => PollBoth(host, client);
        WaitForLobby(host, client);
        Sync();
        host.SetMode(false, true);
        host.SetHuntBoss(EnemyType.HiveQueen);  // no hunt for her yet: ignored
        Sync();
        Check(client.HuntMode && client.HuntBoss == EnemyType.Boss && host.HuntBoss == EnemyType.Boss, "the friend's lobby shows the Boss Hunt and its boss (only bosses with a hunt can be picked)");
        client.RequestReady(true); host.RequestReady(true); Sync();
        host.StartMatch();
        WaitForGame(host, client, dt);
        var g = host.Game;
        var c = client.Game;
        g.ChallengesOn = true;  // (on, to show a hunt opens no portal)
        Check(g.Hunt != null && c.Hunt != null && c.Hunt.Boss == EnemyType.Boss && hp.RunsStarted == 0 && cp.RunsStarted == 0,
            "the hunt starts on every game, and it doesn't count as a run started");
        Check(Math.Abs(g.ApproachShare(1) - 1f) < 0.01f && Math.Abs(g.ApproachShare(9) - 1f) < 0.01f && g.ApproachShare(10) == 0f && !g.IsBossWave(5) && g.IsBossWave(10),
            "a hunt's nine build-up waves are all its boss's own enemies, and only its tenth is a boss wave");
        Check(c.LastWave == Hunts.Length && c.IsBossWave(10) && !c.IsBossWave(5), "the friend's game counts the hunt's ten waves too (the HUD and Tab screen show wave n / 10)");

        // Through the waves (enemies cleared as they come, picks skipped), to the Warden.
        Action keep = () =>
        {
            foreach (var p in g.Players.Values) { p.InvulnT = 1f; p.PendingPicks = p.PendingLegendary = p.PendingMods = p.PrizePicks = 0; p.Offer = null; p.LegendaryQueue.Clear(); }
            foreach (var e in g.Enemies.Values) if (!e.Def.IsBoss && g.Enemies.Count > 0) e.Hp = 0f;
        };
        bool portal = false, pathOffered = false;
        var faction = new HashSet<EnemyType>(Approaches.For(EnemyType.Boss).Members.Select(m => m.Type));
        var seen = new HashSet<EnemyType>();
        var others = new HashSet<EnemyType>();
        Run(s, dt, 400f, () => g.Wave == Hunts.Length && Find(host, EnemyType.Boss) != null && !g.IsSpawning(Find(host, EnemyType.Boss)), () =>
        {
            keep();
            portal |= g.Room != RoomPhase.None;
            if (g.Wave < Hunts.Length)
                foreach (var e in g.Enemies.Values)
                    (faction.Contains(e.Type) ? seen : others).Add(e.Type);
            pathOffered |= g.PathOffer != null;
        });
        Console.WriteLine("  build-up waves: the Warden's own {0}; anything else: {1}", string.Join(", ", seen), others.Count == 0 ? "none" : string.Join(", ", others));
        Check(others.Count == 0 && seen.Count == faction.Count, "the build-up waves bring only the Warden's own enemies, every kind of them by the end");
        Check(g.Wave == Hunts.Length && Find(host, EnemyType.Boss) != null && c.HuntWave == Hunts.Length && !portal && !pathOffered,
            "nine waves, then the Warden on the hunt's tenth (no challenge portal or path offer on the way)");
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
        var huntLog = hp.Recent.Count > 0 ? hp.Recent[0] : null;
        var huntTl = huntLog == null ? null : RunTimelineStore.Load(huntLog.File);
        Check(huntLog != null && huntLog.Result == 3 && huntLog.Mode == "Hunt" && huntLog.Class.Length > 0 && huntTl != null && huntTl.Won && huntTl.Samples.Count >= 3 && huntTl.Mode == "Hunt",
            "a finished hunt keeps its curve too (the results screen graphs it)");
        host.Leave(""); client.Leave("");
        Profile.Folder = wasFolder;
        try { Directory.Delete(huntDir, true); } catch (IOException) { }
    }

    static void ZombieTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var xpScale = typeof(Game).GetMethod("ZombieXpScale", flags);
        int port = nextPort++;
        var host = HostAs(new Session { StartWave = 3 }, port, false, "Host", Role.Tank);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        var s = new[] { host, client };
        Action Sync = () => PollBoth(host, client);
        WaitForLobby(host, client);
        Sync();
        host.SetModifier(RunMod.ZombieInfection, true);
        Sync();
        client.RequestReady(true); host.RequestReady(true); Sync();
        host.StartMatch();
        WaitForGame(host, client, dt);
        var g = host.Game;
        var c = client.Game;
        Action safe = () => { foreach (var p in g.Players.Values) { p.InvulnT = 1f; p.PendingPicks = 0; p.Offer = null; } };
        Run(s, dt, 8f, () => g.State == MatchState.Fighting, safe);
        var queue = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
        var all = queue.Concat(g.Enemies.Values.Select(e => e.Type)).ToList();
        // Wave 3, two players: 10 + 2 for an early wave = 12 usually; a horde has x1.66 of that.
        int expect = (int)Math.Round(12 * Zombies.CountMult(3));
        Console.WriteLine("  wave 3 horde: {0} enemies (expected {1}): {2}", all.Count, expect, string.Join(",", all.GroupBy(t => t).Select(k => k.Key + " x" + k.Count())));
        Check(c.Has(RunMod.ZombieInfection) && all.Count >= expect - 1 && all.All(t => t == EnemyType.Shambler || t == EnemyType.Runner),
            "Zombie Infection: a regular wave is a bigger horde of zombies (wave 3: Shamblers and Runners) and the friend's game knows the modifier");
        var mix = new Dictionary<EnemyType, int>();
        var rngMix = new Random(3);
        for (int i = 0; i < 4000; i++) { var t = Zombies.Pick(rngMix, 20); int n0; mix.TryGetValue(t, out n0); mix[t] = n0 + 1; }
        Check(mix.Count == 7 && mix[EnemyType.Shambler] > mix[EnemyType.Spitter] && mix[EnemyType.Runner] * 3 > mix[EnemyType.Spitter] && mix[EnemyType.Spitter] > mix[EnemyType.Mender]
              && Zombies.Pick(new Random(1), 2) == EnemyType.Shambler,
            "by wave 20 all seven kinds are in the mix, the support ones rarest (" + string.Join(", ", mix.OrderByDescending(k => k.Value).Select(k => k.Key + " " + k.Value)) + ")");
        Check(Math.Abs((float)xpScale.Invoke(g, new object[] { g.Enemies.Values.FirstOrDefault() ?? (Enemy)spawn.Invoke(g, new object[] { EnemyType.Shambler, new V2(800f, 450f), 0f, 0, false }) }) - 1f / Zombies.CountMult(3)) < 0.001f,
            "a horde zombie's XP is split so levels keep pace with a normal run");

        // The cap: however many are waiting, no more than Cap at once.
        for (int i = 0; i < 120; i++) queue.Add(EnemyType.Shambler);
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
                var z = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Shambler, new V2(300f + 20f * (i % 40), 300f + 10f * (i / 40)), 0f, 0, false });
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
        var sample = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Shambler, new V2(800f, 450f), 0f, 0, false });
        int rose = killBatch(80, false);
        Console.WriteLine("  80 shot down: {0} got back up", rose);
        Check(rose >= 8 && rose <= 35, "the dead rise: about a quarter of the zombies killed get back up a few seconds later");
        g.Enemies.Remove(sample.Id);
        var r = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Shambler, new V2(800f, 450f), 0f, 0, false });
        r.Risen = true;
        Check((float)xpScale.Invoke(g, new object[] { r }) == 0f, "a risen zombie gives no XP");
        g.Enemies.Remove(r.Id);
        int roseBurning = killBatch(40, true);
        Check(roseBurning == 0, "burned zombies stay down");

        // The wave waits for the dead to get up.
        anchor.Hp = 0f;  // the last one standing goes down with the rest
        for (int i = 0; i < 30; i++)
        {
            var z = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Shambler, new V2(400f + 10f * i, 400f), 0f, 0, false });
            z.SpawnT = 0f; z.LastHitBy = 1; z.Hp = 0f;
        }
        Run(s, dt, 0.2f, null, safe);
        bool held = g.ZombiesRising && g.State == MatchState.Fighting && g.Remaining > 0 && g.Enemies.Count == 0;
        Run(s, dt, Zombies.RiseTime + 1f, null, safe);
        Check(held && g.Enemies.Values.Any(e => e.Risen && Math.Abs(e.Hp - e.MaxHp * Zombies.RiseHp) < 0.01f), "the wave isn't over while the dead are still getting up, and they rise at half health");

        // Each kind's trick. A still Brute in a corner keeps the wave going; the players stand still.
        foreach (var e in g.Enemies.Values.ToList()) g.Enemies.Remove(e.Id);
        queue.Clear();
        var keep = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, new V2(60f, 840f), 0f, 0, false });
        keep.MaxHp = keep.Hp = 1e6f; keep.Speed = 0f;
        var hurt = typeof(Game).GetMethod("HurtEnemy", flags);
        var slowAt = typeof(Game).GetMethod("HoneySlowAt", flags);
        var p1 = g.Players[1];
        Func<EnemyType, V2, Enemy> put = (t, at) => { var z = (Enemy)spawn.Invoke(g, new object[] { t, at, 0f, 0, false }); z.SpawnT = 0f; return z; };
        Action clearOut = () => { foreach (var e in g.Enemies.Values.ToList()) if (e != keep) g.Enemies.Remove(e.Id); g.Patches.Clear(); c.Patches.Clear(); };
        Action calm = () => { keep.Hp = keep.MaxHp; foreach (var p in g.Players.Values) { p.PendingPicks = 0; p.Offer = null; } };

        // Spitter: a glob at you lands as acid that burns and slows.
        var spit = put(EnemyType.Spitter, p1.Pos + new V2(250f, 0f));
        spit.FireT = 0f;
        spit.TargetId = p1.Id; spit.RetargetT = 30f;  // at the host (with another seed it may pick the friend)
        p1.InvulnT = 0f; p1.Hp = p1.MaxHp;
        float hp0 = p1.Hp;
        Run(s, dt, 2.2f, () => g.Patches.Values.Any(pt => pt.Kind == PatchKind.Acid && pt.Active && pt.Pos.DistanceTo(p1.Pos) < pt.Radius) && p1.Hp < hp0 - 4f, calm);
        bool acid = g.Patches.Values.Any(pt => pt.Kind == PatchKind.Acid && pt.Pos.DistanceTo(p1.Pos) < pt.Radius) && c.Patches.Values.Any(pt => pt.Kind == PatchKind.Acid);
        float acidSlow = (float)slowAt.Invoke(g, new object[] { p1.Pos });
        Check(acid && p1.Hp < hp0 && Math.Abs(acidSlow - Zombies.AcidSlow) < 0.01f, "Spitter: its glob lands as acid at your feet that burns and slows you (" + (hp0 - p1.Hp).ToString("0") + " damage, speed x" + acidSlow + ")");
        clearOut();
        g.DevSetGod(1, true); g.DevSetGod(2, true);

        // Crawler: grabs and roots you (shown on the friend's game too) until it's shot for 25.
        p1.InvulnT = 0f; p1.StunT = 0f;
        var crawl = put(EnemyType.Crawler, p1.Pos + new V2(28f, 0f));
        Run(s, dt, 1f, () => p1.StunT > 0f, () => { calm(); p1.InvulnT = 0f; });
        Run(s, dt, 0.2f, null, calm);
        Enemy cc;
        bool grabbed = p1.StunT > 0f && (crawl.Phase & Zombies.PhGrab) != 0 && c.Players[1].StunT > 0f
                       && c.Enemies.TryGetValue(crawl.Id, out cc) && (cc.Phase & Zombies.PhGrab) != 0;
        hurt.Invoke(g, new object[] { crawl, 30f, 2 });
        Run(s, dt, 0.2f, null, calm);
        Check(grabbed && p1.StunT == 0f && (crawl.Phase & Zombies.PhGrab) == 0, "Crawler: it grabs and roots you (the friend's game sees it), and shooting it for 25 breaks you free");
        clearOut();

        // Bloater: it bursts into a spore cloud that slows.
        var bloat = put(EnemyType.Bloater, new V2(500f, 300f));
        bloat.Hp = 0f;
        Run(s, dt, 0.2f, null, calm);
        float sporeSlow = (float)slowAt.Invoke(g, new object[] { new V2(500f, 300f) });
        Check(g.Patches.Values.Any(pt => pt.Kind == PatchKind.Spores) && c.Patches.Values.Any(pt => pt.Kind == PatchKind.Spores) && Math.Abs(sporeSlow - Zombies.SporeSlow) < 0.01f,
            "Bloater: killed, it bursts into a spore cloud that slows you (on every game)");
        clearOut();

        // Shrieker: its scream frenzies the zombies round it (the friend's game draws the streaks).
        var shriek = put(EnemyType.Shrieker, new V2(1300f, 300f));
        shriek.T = 1f; shriek.T2 = 0.2f;
        var near = put(EnemyType.Shambler, new V2(1200f, 330f));
        var far = put(EnemyType.Shambler, new V2(200f, 800f));
        near.MaxHp = near.Hp = far.MaxHp = far.Hp = 1e5f;
        Run(s, dt, 1f, () => near.FrenzyT > 0f, calm);
        Run(s, dt, 0.2f, null, calm);
        Enemy cn;
        Check(near.FrenzyT > 0f && far.FrenzyT == 0f && c.Enemies.TryGetValue(near.Id, out cn) && (cn.Phase & Zombies.PhFrenzy) != 0,
            "Shrieker: its scream frenzies the zombies near it (not the far ones), and the friend's game sees it");
        clearOut();

        // Mender: its pulse heals the zombies round it.
        var mend = put(EnemyType.Mender, new V2(1300f, 300f));
        mend.T = 1f; mend.T2 = 0.2f;
        var hurtOne = put(EnemyType.Shambler, new V2(1250f, 360f));
        hurtOne.MaxHp = 1000f; hurtOne.Hp = 300f; hurtOne.Speed = 0f;
        Run(s, dt, 1f, () => hurtOne.Hp > 300f, calm);
        Check(hurtOne.Hp >= 300f + 1000f * Zombies.MendHeal - 1f, "Mender: its pulse heals the zombies round it by 15%");

        // ... and the dead near it rise twice as often, in half the time.
        mend.Pos = new V2(500f, 300f); mend.Speed = 0f; mend.MaxHp = mend.Hp = 1e5f;
        for (int i = 0; i < 60; i++)
        {
            var z = put(EnemyType.Shambler, new V2(400f + 20f * (i % 10), 230f + 20f * (i / 10)));
            z.LastHitBy = 1; z.Hp = 0f;
        }
        Run(s, dt, Zombies.RiseTime * 0.5f + 0.4f, null, () => { calm(); mend.Hp = mend.MaxHp; });
        int quick = g.Enemies.Values.Count(e => e.Risen);
        Console.WriteLine("  60 shot down by a Mender: {0} up within {1:0.0} s", quick, Zombies.RiseTime * 0.5f + 0.4f);
        Check(quick >= 18 && quick <= 45, "near a Mender about half the dead rise, twice as fast");
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
