using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

// The Rooms arena's balance (Docs/Rooms.md, phase 3): a duo of Good bots plays the same seed on the Classic arena and on
// the Rooms arena, and each regular wave's numbers are printed side by side: how long it took, the damage each player
// took (as a share of their max health), how many threats were in a 1x view at once (on average and at worst; Classic's
// view is the whole arena), and downs. Run: run.sh 1 1 arenabal [last wave] [seed]
static partial class Program
{
    sealed class ArenaWave
    {
        public float Length, TakenPct, ViewAvg, ViewMax, Shots;
        public int Downs, Samples;
    }

    sealed class ArenaRound
    {
        public int Wave, Rooms, Proposals, BuiltCost, BuiltOwner;
        public int[] Vectors, Caps;
        public float Scale;
    }

    static string arenaNodeStats = "";  // the Expanding arena's nodes at the end of a run: claimed/placed per kind
    static List<ArenaRound> arenaRounds = new List<ArenaRound>();  // the Expanding arena's building rounds, filled by PlayArena(expanding: true)

    const float StallSeconds = 600f;  // a regular wave longer than this ends the run as stalled (the bots can't finish it)

    static Dictionary<int, ArenaWave> PlayArena(float dt, bool rooms, int lastWave, int seed, out string ending, bool expanding = false)
    {
        arenaRounds = new List<ArenaRound>();
        Game.SeedForTests(seed);
        var host = new Session { AutoStart = 2, StartWave = 1 };
        host.Game.Bot = true; host.Game.BotLevel = BotSkill.Good; host.Game.BotRerolls = false;
        int port = nextPort++;
        host.Host(port, "A", Role.Tank, 0);
        host.Diff.Set(DiffKnob.ArenaRooms, rooms && !expanding ? 1f : 0f);  // (after Host: hosting starts from the saved lobby)
        host.Diff.Set(DiffKnob.ArenaExpanding, expanding ? 1f : 0f);
        host.Diff.Set(DiffKnob.BuildVote, 1f);  // (the balance report measures the voted rounds)
        var c = new Session(); c.Game.Bot = true; c.Game.BotLevel = BotSkill.Good; c.Game.BotRerolls = false;
        c.Join("127.0.0.1", port, "B", Role.Dps, 0);
        WaitForGame(host, c, dt);
        var g = host.Game;
        var waves = new Dictionary<int, ArenaWave>();
        ArenaWave cur = null;
        int curWave = -1;
        float startT = 0f, sampleT = 0f, idle = 0f;
        bool dumped = false, stalled = false;
        var downSeen = new Dictionary<int, bool>();
        var taken0 = new Dictionary<int, float>();
        var downs0 = new Dictionary<int, int>();
        ArenaRound openRound = null;
        int roomsBefore = 0, peakProps = 0;
        for (int f = 0; f < 60 * 60 * 90 && g.State != MatchState.GameOver && g.State != MatchState.Victory && g.Wave <= lastWave; f++)
        {
            Step(host, dt); Step(c, dt);
            foreach (var p in g.Players.Values) { p.PendingMods = 0; }
            if (expanding)  // the building rounds: what the players could afford, what was proposed, what was built
            {
                if (g.ExpandOpen && openRound == null)
                {
                    openRound = new ArenaRound { Wave = g.Wave, Rooms = g.Expanded.Count, Vectors = g.Players.Values.Select(p => p.Vectors).ToArray(), Caps = g.Players.Values.Select(p => p.VectorCap).ToArray(), Scale = g.ExpansionScale };
                    roomsBefore = g.Expanded.Count; peakProps = 0;
                }
                if (openRound != null)
                {
                    peakProps = Math.Max(peakProps, g.ExpandProposals.Count);
                    if (!g.ExpandOpen)
                    {
                        openRound.Proposals = peakProps;
                        if (g.Expanded.Count > roomsBefore) { var r = g.Expanded[g.Expanded.Count - 1]; openRound.BuiltCost = r.Price; openRound.BuiltOwner = r.Owner; }
                        arenaRounds.Add(openRound);
                        openRound = null;
                    }
                }
            }
            // The bots can get stuck on crates going for a chest or an item (they don't steer round props, in either arena):
            // after 20 s with nothing left to fight, take the treasure for them, as a person would.
            idle = g.State == MatchState.Fighting && g.Foes() - g.Enemies.Values.Count(e => g.InVault(e)) == 0 ? idle + dt : 0f;  // (the vault's mini-boss is optional)
            if (idle > 20f)
            {
                foreach (var pr in g.Props.Values.Where(pr => pr.Kind == PropKind.Chest).ToList()) g.Props.Remove(pr.Id);
                g.Loots.Clear();
                idle = 0f;
            }
            if (Environment.GetEnvironmentVariable("ARENADBG") != null)
                foreach (var p in g.Players.Values)
                {
                    bool was;
                    downSeen.TryGetValue(p.Id, out was);
                    if (p.Downed && !was)
                        Console.WriteLine("  [{0}] wave {1} t={2:0}: player {3} down at {4:0},{5:0} ({6}, locked {7}); near: {8}", rooms ? "R" : "C", g.Wave, g.RunTime, p.Id, p.Pos.X, p.Pos.Y,
                            Rooms.Areas[Rooms.AreaAt(p.Pos)].Name, g.AreaLocked(Rooms.AreaAt(p.Pos)),
                            string.Join(", ", g.Enemies.Values.Where(e => e.Pos.DistanceTo(p.Pos) < 300f).Select(e => e.Type + (e.Spilled ? "*" : ""))));
                    downSeen[p.Id] = p.Downed;
                }
            bool fighting = g.State == MatchState.Fighting && !g.BossNow && !g.InRoom;
            if (fighting && curWave != g.Wave)
            {
                curWave = g.Wave;
                if (Environment.GetEnvironmentVariable("ARENADBG") != null) Console.WriteLine("  [{0}] wave {1}: roomsUp {2} arena {3} path {4} mods {5}", rooms ? "R" : "C", g.Wave, g.RoomsUp, g.RoomsArena, g.CurrentPath, g.Mods);
                cur = waves[g.Wave] = new ArenaWave();
                startT = g.RunTime;
                foreach (var p in g.Players.Values) { taken0[p.Id] = p.Run.DamageTaken; downs0[p.Id] = p.Run.Downs; }
            }
            if (cur != null && curWave == g.Wave)
            {
                if (fighting)
                {
                    cur.Length = g.RunTime - startT;
                    if (cur.Length > StallSeconds && Environment.GetEnvironmentVariable("ARENADBG") == null) { stalled = true; break; }  // a wave that never ends (bots that can't reach the last foes): stop, say so
                    if (cur.Length > 150f && !dumped && Environment.GetEnvironmentVariable("ARENADBG") != null)
                    {
                        Console.WriteLine("  STUCK wave {0}: remaining {1}, foes {2}, rooms {3}, vault {4}", g.Wave, g.Remaining, g.Foes(),
                            string.Join("; ", g.RoomStates.Select(r => r.Area + " nest " + r.NestAlive + " clr " + r.Cleared + " spill " + r.SpillT.ToString("0"))), g.VaultUp);
                        foreach (var e in g.Enemies.Values) Console.WriteLine("    {0} at {1:0},{2:0} home {3} spilled {4} hp {5:0} spawnT {6}", e.Type, e.Pos.X, e.Pos.Y, e.Home, e.Spilled, e.Hp, e.SpawnT);
                        if (g.RoomsOn) foreach (var e in g.Enemies.Values) { var w = g.CurrentWorld; int ai = w.AreaAt(e.Pos); Console.WriteLine("    {0} is in area {1} {2} {3:0},{4:0}-{5:0},{6:0} (floating {7}, sealed {8}); {9} areas", e.Type, ai, w.Areas[ai].Name, w.Areas[ai].Min.X, w.Areas[ai].Min.Y, w.Areas[ai].Max.X, w.Areas[ai].Max.Y, w.Areas[ai].Floating, w.Areas[ai].Sealed, w.Areas.Length); }
                        foreach (var p in g.Players.Values) Console.WriteLine("    player {0} at {1:0},{2:0} downed {3}", p.Id, p.Pos.X, p.Pos.Y, p.Downed);
                        foreach (var pr in g.Props.Values) Console.WriteLine("    prop {0} at {1:0},{2:0} open {3} hp {4}", pr.Kind, pr.Pos.X, pr.Pos.Y, pr.Open, pr.Hp);
                        foreach (var l in g.Loots.Values) Console.WriteLine("    loot owner {0} at {1:0},{2:0}", l.Owner, l.Pos.X, l.Pos.Y);
                        Console.WriteLine("    client: foes {0}, loots {1}, myLoot {2}, roomsOn {3}, offer {4}, pos {5}", c.Game.Foes(), c.Game.Loots.Count, c.Game.MyLoot != null, c.Game.RoomsOn, c.Game.Me.Offer != null, c.Game.Me.Pos);
                        dumped = true;
                        Console.WriteLine("    state {0} picks {1} offer {2} zombies {3} design {4}", g.State, string.Join(",", g.Players.Values.Select(q => q.PendingPicks + "/" + q.PrizePicks + "/" + (q.Offer != null))), "", g.ZombiesRising, g.DesignPending);
                    }
                    cur.TakenPct = g.Players.Values.Average(p => 100f * (p.Run.DamageTaken - taken0[p.Id]) / Math.Max(1f, p.MaxHp));
                    cur.Downs = g.Players.Values.Sum(p => p.Run.Downs - downs0[p.Id]);
                    sampleT -= dt;
                    if (sampleT <= 0f)
                    {
                        sampleT = 0.5f;
                        foreach (var p in g.Players.Values)
                        {
                            if (p.Downed) continue;
                            V2 lo, hi;
                            if (g.RoomsOn)
                            {
                                V2 mid = g.CurrentWorld.ViewCentre(p.Pos, Game.Width / 2f, Game.Height / 2f, g.VaultOn);
                                lo = mid - new V2(Game.Width / 2f, Game.Height / 2f); hi = mid + new V2(Game.Width / 2f, Game.Height / 2f);
                            }
                            else { lo = V2.Zero; hi = new V2(Game.Width, Game.Height); }
                            int n = g.Enemies.Values.Count(e => e.Hp > 0f && e.Pos.X >= lo.X && e.Pos.X <= hi.X && e.Pos.Y >= lo.Y && e.Pos.Y <= hi.Y);
                            int sh = g.Bullets.Values.Count(b => Defs.IsEnemyShot(b.Kind) && b.Pos.X >= lo.X && b.Pos.X <= hi.X && b.Pos.Y >= lo.Y && b.Pos.Y <= hi.Y);
                            cur.ViewAvg += n; cur.Shots += sh; cur.Samples++;
                            cur.ViewMax = Math.Max(cur.ViewMax, n);
                        }
                    }
                }
            }
        }
        ending = (stalled ? "STALLED (a wave ran over " + StallSeconds + " s) at wave " : g.State + " at wave ") + g.Wave + " (" + g.RunTime.ToString("0") + " s)";
        arenaNodeStats = !expanding ? "" : string.Join(", ", new[] { NodeKind.Chest, NodeKind.Relic, NodeKind.Rule }.Select(k =>
            k + " " + g.Nodes.Count(n => n.Kind == k && Expansion.NodeClaimed(g.Expanded, n)) + "/" + g.Nodes.Count(n => n.Kind == k) + " claimed, " + g.Nodes.Count(n => n.Kind == k && n.Spent) + " spent"
            + (k == NodeKind.Rule ? " (" + string.Join(" ", g.Nodes.Where(n => n.Kind == k).Select(n => (NodeRule)n.Rule + (Expansion.NodeClaimed(g.Expanded, n) ? n.Cleared ? "+cleared" : "+claimed" : "")).ToArray()) + ")" : "")).ToArray());
        foreach (var w in waves.Values) if (w.Samples > 0) { w.ViewAvg /= w.Samples; w.Shots /= w.Samples; }
        host.Leave(""); c.Leave("");
        return waves;
    }

    static void ArenaBalanceReport(float dt, int lastWave, int seed, float roomsDamage)
    {
        float keepMult = Rooms.EnemyDamageMult;
        if (roomsDamage > 0f) Rooms.EnemyDamageMult = roomsDamage;
        string endC, endR;
        var classic = PlayArena(dt, false, lastWave, seed, out endC);
        var rooms = PlayArena(dt, true, lastWave, seed, out endR);
        Console.WriteLine("  Good bot duo, seed {0}, Rooms damage x{3}. Classic: {1}. Rooms: {2}.", seed, endC, endR, Rooms.EnemyDamageMult);
        Rooms.EnemyDamageMult = keepMult;
        Console.WriteLine("  wave | length s     | dmg taken % max HP | threats in view avg / max | shots in view | downs");
        Console.WriteLine("       | Class  Rooms | Class   Rooms      | Class       Rooms         | Class  Rooms  | C  R");
        foreach (int w in classic.Keys.Union(rooms.Keys).OrderBy(x => x))
        {
            ArenaWave a, b;
            classic.TryGetValue(w, out a); rooms.TryGetValue(w, out b);
            Func<ArenaWave, Func<ArenaWave, string>, string> S = (x, fmt) => x == null ? "-" : fmt(x);
            Console.WriteLine("  {0,4} | {1,5} {2,5}  | {3,5}   {4,5}      | {5,5} / {6,-3}  {7,5} / {8,-3}   | {9,5} {10,5}  | {11} {12}",
                w, S(a, x => x.Length.ToString("0")), S(b, x => x.Length.ToString("0")),
                S(a, x => x.TakenPct.ToString("0")), S(b, x => x.TakenPct.ToString("0")),
                S(a, x => x.ViewAvg.ToString("0.0")), S(a, x => x.ViewMax.ToString("0")), S(b, x => x.ViewAvg.ToString("0.0")), S(b, x => x.ViewMax.ToString("0")),
                S(a, x => x.Shots.ToString("0")), S(b, x => x.Shots.ToString("0")),
                S(a, x => x.Downs.ToString()), S(b, x => x.Downs.ToString()));
        }
        Func<Dictionary<int, ArenaWave>, Func<ArenaWave, float>, float> Avg = (d, sel) => d.Count == 0 ? 0f : d.Values.Average(sel);
        Console.WriteLine("  average | {0:0} {1:0} s | {2:0}% {3:0}% taken | {4:0.0} {5:0.0} in view | {6:0} {7:0} shots | downs {8} {9}",
            Avg(classic, x => x.Length), Avg(rooms, x => x.Length), Avg(classic, x => x.TakenPct), Avg(rooms, x => x.TakenPct),
            Avg(classic, x => x.ViewAvg), Avg(rooms, x => x.ViewAvg), Avg(classic, x => x.Shots), Avg(rooms, x => x.Shots),
            classic.Values.Sum(x => x.Downs), rooms.Values.Sum(x => x.Downs));
    }
}
