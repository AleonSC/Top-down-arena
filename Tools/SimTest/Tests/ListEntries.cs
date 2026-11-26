using System.Linq;
using System;
using System.IO;
using System.Collections.Generic;
using Arena;

// Every entry of the game's smaller lists (run modifiers, paths, Boss Hunts, challenge rooms): each is complete and
// each one plays over the network without breaking, so a new entry is checked the day it's added (suite `lists`).
static partial class Program
{
    // Runs one entry's play check; an exception fails that entry and the rest still run.
    static void Entry(string what, Func<bool> play)
    {
        bool ok;
        try { ok = play(); }
        catch (Exception e) { ok = false; what += " (threw " + e.GetType().Name + ": " + e.Message + ")"; }
        Check(ok, what);
    }

    static void ListEntriesTest(float dt)
    {
        // --- Run modifiers (Modifiers.cs) -----------------------------------------------------------------------------
        var mods = Enum.GetValues(typeof(RunMod)).Cast<RunMod>().Where(m => m != RunMod.None).ToList();
        var noDef = mods.Where(m => Modifiers.All.Count(d => d.Mod == m) != 1).ToList();
        Check(noDef.Count == 0, "every run modifier has one entry in Modifiers.All" + (noDef.Count > 0 ? " (missing: " + string.Join(", ", noDef) + ")" : ""));
        Check(Modifiers.All.All(d => !string.IsNullOrEmpty(d.Name) && !string.IsNullOrEmpty(d.Desc)) && Modifiers.All.Select(d => d.Name).Distinct().Count() == Modifiers.All.Length,
            "every run modifier has a name of its own and a description");
        foreach (var d in Modifiers.All)
            Entry("the " + d.Name + " modifier alone: a run starts with it on every screen and plays 4 s", () =>
            {
                int port = nextPort++;
                var host = HostAs(new Session(), port, false, "Host", Role.Tank);
                var client = JoinAs(port, false, "Friend", Role.Dps);
                try
                {
                    WaitForLobby(host, client);
                    host.SetModifier(d.Mod, true);
                    PollBoth(host, client);
                    client.RequestReady(true); host.RequestReady(true); PollBoth(host, client);
                    host.StartMatch();
                    WaitForGame(host, client, dt);
                    host.Game.DevSetGod(1, true); host.Game.DevSetGod(2, true);
                    Run(new[] { host, client }, dt, 4f);
                    return host.Game.Running && host.Game.Has(d.Mod) && client.Game.Has(d.Mod);
                }
                finally { host.Leave(""); client.Leave(""); }
            });

        // --- Paths (Paths.cs) -----------------------------------------------------------------------------------------
        var paths = Enum.GetValues(typeof(PathId)).Cast<PathId>().Where(p => p != PathId.None).ToList();
        var noPath = paths.Where(p => Paths.All.Count(d => d.Id == p) != 1).ToList();
        Check(noPath.Count == 0, "every path has one entry in Paths.All" + (noPath.Count > 0 ? " (missing: " + string.Join(", ", noPath) + ")" : ""));
        Check(Paths.All.All(d => !string.IsNullOrEmpty(d.Name) && !string.IsNullOrEmpty(d.Kind) && !string.IsNullOrEmpty(d.Blurb) && d.Rules != null && d.Rules.Length > 0
                                 && d.Danger >= 1 && d.Danger <= 3 && d.Showcase != null && d.Showcase.All(t => (int)t < Defs.Enemies.Length))
              && Paths.All.Select(d => d.Name).Distinct().Count() == Paths.All.Length,
            "every path has a name of its own, its kind, blurb and rules, a danger of 1-3 and real enemies on its card");
        {
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var build = typeof(Game).GetMethod("BuildWave", flags);
            var queueField = typeof(Game).GetField("spawnQueue", flags);
            int port = nextPort++;
            var host = HostAs(new Session { AutoStart = 2, StartWave = 6 }, port, false, "Host", Role.Tank);
            var client = JoinAs(port, false, "Friend", Role.Dps);
            var s = new[] { host, client };
            WaitForGame(host, client, dt);
            var g = host.Game;
            g.DevSetGod(1, true); g.DevSetGod(2, true);
            foreach (var d in Paths.All)
                Entry("the " + d.Name + " path: its wave builds from real enemies, and every screen is on it for 3 s", () =>
                {
                    g.PathOffer = new[] { d.Id, d.Id == PathId.SwarmFields ? PathId.SniperRidge : PathId.SwarmFields };
                    g.ChoosePath(0);  // (as the host's pick after a boss: sent to every screen)
                    g.BossSlot = false;
                    build.Invoke(g, new object[] { g.PathFrom });
                    var queue = (List<EnemyType>)queueField.GetValue(g);
                    bool wave = queue.Count > 0 && queue.All(t => (int)t < Defs.Enemies.Length);
                    Run(s, dt, 3f, null, () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = p.PendingCards = 0; p.LegendaryQueue.Clear(); p.Offer = null; } });
                    return wave && g.Running && g.CurrentPath == d.Id && client.Game.CurrentPath == d.Id;
                });
            host.Leave(""); client.Leave("");
        }

        // --- Boss Hunts (Hunts.cs) ------------------------------------------------------------------------------------
        Check(Hunts.All.Length > 0 && Hunts.All.All(h => Defs.Bosses.Contains(h.Boss) && !string.IsNullOrEmpty(h.Name) && !string.IsNullOrEmpty(h.UnlockId) && h.HomeWave > 0 && Hunts.Get(h.Boss) == h),
            "every Boss Hunt is for one of the bosses (Defs.Bosses), with a name, an unlock and its home wave");
        string wasFolder = Profile.Folder;
        Profile.Folder = Path.Combine(Path.GetTempPath(), "arena-lists-" + Guid.NewGuid().ToString("N"));  // (a hunt writes its curve)
        try
        {
            foreach (var h in Hunts.All)
                Entry("the " + h.Name + ": it starts for everyone and plays 4 s", () =>
                {
                    int port = nextPort++;
                    var host = new Session();
                    host.Game.ProfileOverride = new Profile();
                    host.Game.ProfileOverride.BossBeaten(h.Boss, h.HomeWave);  // (the hunt's unlock)
                    HostAs(host, port, false, "Host", Role.Tank);
                    var client = JoinAs(port, false, "Friend", Role.Dps);
                    try
                    {
                        WaitForLobby(host, client);
                        host.SetMode(GameMode.Hunt);
                        host.SetHuntBoss(h.Boss);
                        PollBoth(host, client);
                        client.RequestReady(true); host.RequestReady(true); PollBoth(host, client);
                        host.StartMatch();
                        WaitForGame(host, client, dt);
                        host.Game.DevSetGod(1, true); host.Game.DevSetGod(2, true);
                        Run(new[] { host, client }, dt, 4f);
                        return host.Game.Running && host.Game.Hunt == h && client.Game.Hunt != null && client.Game.Hunt.Boss == h.Boss;
                    }
                    finally { host.Leave(""); client.Leave(""); }
                });
        }
        finally
        {
            try { if (Directory.Exists(Profile.Folder)) Directory.Delete(Profile.Folder, true); } catch { }
            Profile.Folder = wasFolder;
        }

        // --- Challenge rooms (Challenges.cs) --------------------------------------------------------------------------
        var kinds = Enum.GetValues(typeof(ChallengeKind)).Cast<ChallengeKind>().Where(k => k != ChallengeKind.None).ToList();
        Check(kinds.All(k => Challenges.All.Count(c => c == k) == 1) && Challenges.All.Length == kinds.Count,
            "every challenge room is in Challenges.All, once" + (kinds.Any(k => !Challenges.All.Contains(k)) ? " (missing: " + string.Join(", ", kinds.Where(k => !Challenges.All.Contains(k))) + ")" : ""));
        Check(kinds.All(k => !string.IsNullOrEmpty(Challenges.Name(k))) && kinds.Select(Challenges.Name).Distinct().Count() == kinds.Count,
            "every challenge room has a name of its own");
        foreach (var kind in Challenges.All)
            Entry("the " + Challenges.Name(kind) + " room: its portal takes everyone in, and it plays 4 s", () =>
            {
                int port = nextPort++;
                var host = HostAs(new Session { AutoStart = 2, StartWave = 7 }, port, false, "Host", Role.Tank);
                var client = JoinAs(port, false, "Friend", Role.Dps);
                var s = new[] { host, client };
                try
                {
                    WaitForGame(host, client, dt);
                    var g = host.Game;
                    var cg = client.Game;
                    g.DevSetGod(1, true); g.DevSetGod(2, true);
                    var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                    Action noPicks = () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = p.PendingCards = 0; p.LegendaryQueue.Clear(); p.Offer = null; } };
                    Action quiet = () =>
                    {
                        ((System.Collections.IList)typeof(Game).GetField("spawnQueue", flags).GetValue(g)).Clear();
                        ((System.Collections.IList)typeof(Game).GetField("squadQueue", flags).GetValue(g)).Clear();
                    };
                    Run(s, dt, 60f, () => g.State == MatchState.Intermission && g.ChallengePhase == RoomPhase.None, () => { noPicks(); quiet(); foreach (var e in g.Enemies.Values) e.Hp = 0f; });
                    g.StateTimer = 5f;
                    g.DevOpenPortal(kind);
                    V2 at = g.PortalPos;
                    Run(s, dt, 3f, () => g.InRoom, () => { noPicks(); g.Players[1].Pos = at; cg.Players[2].Pos = at; });
                    bool inside = g.InRoom && g.ChallengeNow == kind;
                    Run(s, dt, 4f, null, noPicks);
                    return inside && g.Running && cg.ChallengeNow == kind;
                }
                finally { host.Leave(""); client.Leave(""); }
            });
    }
}
