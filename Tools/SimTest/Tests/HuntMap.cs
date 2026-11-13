using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arena;

// The hunt map (HuntMap.cs, Docs/HuntMap.md): its shape, links, node types, progress and restore, over many seeds.
static partial class Program
{
    static void HuntMapTest()
    {
        const int seeds = 3000;
        int firstBad = 0, breathers = 0, rowsBad = 0, noOut = 0, noIn = 0, far = 0, cross = 0, rule = 0, badTypes = 0, nodes = 0, multi = 0, elites = 0, fights = 0, noChoiceRows = 0;
        for (int seed = 1; seed <= seeds; seed++)
        {
            var m = HuntMap.Generate(seed);
            if (m.Rows.Length != HuntMap.Steps) { rowsBad++; continue; }
            for (int st = 0; st < HuntMap.Steps; st++)
            {
                var row = m.Rows[st];
                bool edge = st == 0 || st == HuntMap.Steps - 1;
                if (edge ? row.Length != 1 || row[0].Lane != 1 : row.Length < 2 || row.Length > 3 || row.Select(n => n.Lane).Distinct().Count() != row.Length || row.Zip(row.Skip(1), (a, b) => a.Lane < b.Lane).Contains(false))
                    rowsBad++;
                if (st == HuntMap.Steps - 2 && !(row.Length == 2 && row[0].Lane == 0 && row[1].Lane == 2)) rowsBad++;
                if ((st == HuntMap.Steps - 1) != (row[0].Type == HuntNodeType.Boss)) badTypes++;
                if (st > 0 && st < HuntMap.Steps - 1 && row.All(n => n.Type == HuntNodeType.Elite)) badTypes++;
                if (st > 0 && st < HuntMap.Steps - 2 && !row.Any(n => n.Type == HuntNodeType.Skirmish)) badTypes++;
                if (st == 0 && row[0].Type != HuntNodeType.Skirmish) badTypes++;
                if (st == HuntMap.Steps - 2 && row.Any(n => n.Type != HuntNodeType.Rest && n.Type != HuntNodeType.Shop)) badTypes++;
                if (st != HuntMap.Steps - 2 && row.Any(n => (n.Type == HuntNodeType.Rest || n.Type == HuntNodeType.Shop) && (st < 2 || st > HuntMap.Steps - 4 || st == HuntMap.Steps / 2 - 1))) badTypes++;
                if (st < HuntMap.Steps - 1 && row.Any(n => !n.Fight && n.Next.Any(j => !m.Rows[st + 1][j].Fight))) badTypes++;
                if (st == HuntMap.Steps / 2 - 1 && !row.Any(n => n.Type == HuntNodeType.Elite)) rule++;
                for (int i = 0; i < row.Length; i++)
                {
                    var n = row[i];
                    if (n.Step != st || n.Index != i) rowsBad++;
                    if (st < HuntMap.Steps - 1)
                    {
                        nodes++;
                        if (n.Type == HuntNodeType.Elite) elites++;
                        if (!n.Fight) breathers++;
                        if (n.Next.Length == 0) noOut++;
                        if (n.Next.Length >= 2) multi++;
                        if (n.Next.Distinct().Count() != n.Next.Length) rowsBad++;
                        foreach (int j in n.Next)
                            if (Math.Abs(m.Rows[st + 1][j].Lane - n.Lane) > 1) far++;
                    }
                    else if (n.Next.Length != 0) noOut++;
                    if (st > 0 && !m.Rows[st - 1].Any(a => a.Next.Contains(i))) noIn++;
                }
                if (st < HuntMap.Steps - 1)
                {
                    if (row.All(n => n.Next.Length == 1)) noChoiceRows++;
                    foreach (var a in row) foreach (var b in row)
                        foreach (int ja in a.Next) foreach (int jb in b.Next)
                            if ((a.Lane - b.Lane) * (m.Rows[st + 1][ja].Lane - m.Rows[st + 1][jb].Lane) < 0) cross++;
                }
            }
            fights += m.Rows.Sum(r => r.Count(n => n.Fight));
            if (badTypes > 0 && firstBad == 0) firstBad = seed;
        }
        Check(rowsBad == 0, "every map has 10 rows: one node in the middle first and last, the row before the boss on the outer lanes, 2-3 nodes in the rest, lanes left to right");
        Check(noOut == 0 && noIn == 0, "every node leads somewhere and every node can be reached (so every route ends at the boss)");
        Check(far == 0, "a link never goes more than one lane over");
        Check(cross == 0, "no two links of a row gap cross (3000 seeds)");
        if (badTypes > 0) Console.WriteLine("  first bad seed {0}: {1}", firstBad, string.Join(" | ", HuntMap.Generate(firstBad).Rows.Select(r => string.Join(",", r.Select(n => HuntMap.Name(n.Type) + ">" + string.Join("", n.Next))))));
        Check(badTypes == 0 && rule == 0, "the first row is a Skirmish, the row before the boss a Rest or Shop, the boss is last, Rest and Shop only in rows 3-7 and before the boss, never two no-fight steps in a row, the middle row has an Elite, no row is all Elites, every fighting row has a Skirmish");
        Console.WriteLine("  info: {0:P0} of nodes have 2+ ways on, {1:P0} are Elites, {2:P0} are Rest or Shop, {3} rows with no choice at all", multi / (double)nodes, elites / (double)nodes, breathers / (double)nodes, noChoiceRows);

        // The lines of the screen (HuntMapLayout.cs): right angles only, between the nodes, and none runs over another.
        float[] lx = { 100f, 230f, 360f }, ry = new float[HuntMap.Steps];
        for (int k = 0; k < ry.Length; k++) ry[k] = 60f + k * 60f;
        int diag = 0, outside = 0, overlap = 0, linkCount = 0, wrongEnds = 0, crossings = 0;
        for (int seed = 1; seed <= 600; seed++)
        {
            var m = HuntMap.Generate(seed);
            var links = HuntMapLayout.Links(m, lx, ry, 15f, 20f);
            linkCount += links.Count;
            if (links.Count != m.Rows.Take(HuntMap.Steps - 1).SelectMany(r => r).Sum(n => n.Next.Length)) wrongEnds++;
            foreach (var l in links)
            {
                var p = l.Pts;
                for (int k = 0; k < 6; k += 2) if (p[k] != p[k + 2] && p[k + 1] != p[k + 3]) diag++;
                var a = m.Rows[l.Step][l.From]; var b = m.Rows[l.Step + 1][l.To];
                float ha = a.Type == HuntNodeType.Boss ? 20f : 15f, hb = b.Type == HuntNodeType.Boss ? 20f : 15f;
                if (Math.Abs(p[0] - lx[a.Lane]) > ha || Math.Abs(p[6] - lx[b.Lane]) > hb || p[1] != ry[l.Step] + ha || p[7] != ry[l.Step + 1] - hb) wrongEnds++;
                if (p[3] <= p[1] || p[3] >= p[7]) outside++;
            }
            for (int a = 0; a < links.Count; a++)
                for (int b = a + 1; b < links.Count; b++)
                {
                    if (links[a].Step != links[b].Step) continue;
                    var A = links[a].Pts; var B = links[b].Pts;
                    int c = HuntMapLayout.Cost(A[0], A[6], A[1], A[7], A[3], B[0], B[6], B[3]);
                    if (c >= 6) overlap++; else crossings += c;
                }
        }
        Check(diag == 0 && wrongEnds == 0 && outside == 0, "every drawn link is right angles only, leaves its node's bottom edge and enters the next one's top edge (600 maps)");
        Check(overlap == 0, "no two drawn links run on top of each other");
        Console.WriteLine("  info: {0} links drawn, {1} places where two cross at a right angle", linkCount, crossings);

        // The same seed gives the same map, other seeds other maps.
        Func<HuntMap, string> Sig = m => string.Join("|", m.Rows.Select(r => string.Join(",", r.Select(n => (int)n.Type + "@" + n.Lane + ">" + string.Join("", n.Next)))));
        Check(Sig(HuntMap.Generate(42)) == Sig(HuntMap.Generate(42)), "the same seed makes the same map");
        Check(Enumerable.Range(1, 200).Select(s => Sig(HuntMap.Generate(s))).Distinct().Count() > 150, "different seeds make different maps");

        // Walking a map: picks must be links of the cleared node, a node must be picked before it is cleared, elites give clues (up to 3), the boss ends it.
        var w = HuntMap.Generate(7);
        Check(w.Step == 0 && w.Choices().SequenceEqual(new[] { 0 }) && !w.Complete && w.Current == null, "a new map starts at the first node with nothing picked");
        Check(!w.Clear(), "nothing is cleared before a pick");
        Check(!w.Pick(1) && w.Pick(0) && !w.Pick(0) && w.Current != null && w.Current.Step == 0, "only the first node can be picked, and only once");
        Check(w.Clear() && w.Step == 1 && w.Current == null, "clearing it moves to the next row");
        var rng = new Random(3);
        for (int guard = 0; guard < 20 && !w.Complete; guard++)
        {
            var ch = w.Choices();
            foreach (int bad in Enumerable.Range(0, w.Rows[w.Step].Length).Where(i => !ch.Contains(i)))
                Check(!w.Pick(bad), "a node that isn't linked from the cleared one can't be picked");
            w.Pick(ch[rng.Next(ch.Length)]);
            w.Clear();
        }
        Check(w.Complete && w.Step == HuntMap.Steps && w.Choices().Length == 0 && w.Path.Length == HuntMap.Steps, "a walk ends with the boss cleared after 10 rows");
        int elitesOnPath = Enumerable.Range(0, HuntMap.Steps).Count(st => w.Rows[st][w.Path[st]].Type == HuntNodeType.Elite);
        Check(w.Clues == Math.Min(HuntMap.MaxClues, elitesOnPath), "clues = Elites cleared, up to 3");
        Check(w.Rows[HuntMap.Steps - 1][w.Path[HuntMap.Steps - 1]].Type == HuntNodeType.Boss, "the last node on the path is the boss");

        // A route through only Elites caps the clues at 3.
        var e = HuntMap.Generate(11);
        int cleared = 0;
        while (!e.Complete)
        {
            var ch = e.Choices();
            int pick = ch.FirstOrDefault(i => e.Rows[e.Step][i].Type == HuntNodeType.Elite);
            e.Pick(ch.Contains(pick) && e.Rows[e.Step][pick].Type == HuntNodeType.Elite ? pick : ch[0]);
            e.Clear(); cleared++;
        }
        Check(e.Clues <= HuntMap.MaxClues, "clues never go past 3");

        // Save and restore: the seed and the path rebuild the map and the progress.
        var half = HuntMap.Generate(99);
        for (int k = 0; k < 4; k++) { half.Pick(half.Choices()[0]); half.Clear(); }
        var back = HuntMap.Restore(99, half.Path);
        Check(back != null && back.Step == 4 && back.Clues == half.Clues && Sig(back) == Sig(half) && back.Choices().SequenceEqual(half.Choices()), "the seed and the cleared path restore the map and the progress");
        Check(HuntMap.Restore(99, new[] { 0, 5 }) == null, "a path that doesn't fit the map is refused");
    }
    // A hunt played as a map over the network: the lobby switch, the map on both screens, the vote (a tie goes to the host, the timer picks
    // for a party that doesn't), the picked node's wave (an Elite is tougher), the node cleared with the wave, the boss at the end.
    // A route of node indexes (one per row) through the map with a Rest and a Shop in rows 3-7.
    static int[] FindRoute(HuntMap m, int step, List<int> so)
    {
        if (step == HuntMap.Steps)
        {
            bool rest = false, shop = false;
            for (int r = 2; r <= HuntMap.Steps - 4; r++)
            {
                rest |= m.Rows[r][so[r]].Type == HuntNodeType.Rest;
                shop |= m.Rows[r][so[r]].Type == HuntNodeType.Shop;
            }
            return rest && shop ? so.ToArray() : null;
        }
        var options = step == 0 ? new[] { 0 } : m.Rows[step - 1][so[step - 1]].Next;
        foreach (int j in options)
        {
            so.Add(j);
            var r = FindRoute(m, step + 1, so);
            so.RemoveAt(so.Count - 1);
            if (r != null) return r;
        }
        return null;
    }

    static void HuntMapNetTest(float dt)
    {
        int port = nextPort++;
        string wasFolder = Profile.Folder, dir = Path.Combine(Path.GetTempPath(), "arena-huntmap-" + Guid.NewGuid().ToString("N"));
        Profile.Folder = dir;
        var host = new Session();
        var hp = host.Game.ProfileOverride = new Profile();
        hp.BossBeaten(EnemyType.Boss, 5);
        host.Host(port, "Host", Role.Tank);
        var client = new Session();
        client.Game.ProfileOverride = new Profile();
        client.Join("127.0.0.1", port, "Friend", Role.Dps);
        var s = new[] { host, client };
        Action Sync = () => PollBoth(host, client);
        WaitForLobby(host, client);
        Sync();
        host.SetMode(false, true);
        Sync();
        Check(!host.HuntMapMode && !client.HuntMapMode, "the hunt map switch is off by default: a Boss Hunt is the ten waves it always was");
        client.SetHuntMapMode(true); Sync();
        Check(!host.HuntMapMode, "only the host can turn the hunt map on");
        host.SetHuntMapMode(true); Sync();
        Check(host.HuntMapMode && client.HuntMapMode, "the host's switch shows in the friend's lobby");
        // A map with a route that has a Rest and a Shop among its middle rows (the test follows it).
        int seed = 0; int[] route = null;
        for (int sd = 1; sd < 5000 && route == null; sd++)
        {
            var m = HuntMap.Generate(sd);
            var found = FindRoute(m, 0, new List<int>());   // (and either way the timer's pick at the first vote leaves one)
            if (found != null && m.Rows[0][0].Next.All(j => FindRoute(m, 2, new List<int> { 0, j }) != null)) { seed = sd; route = found; }
        }
        Check(route != null, "a seed with a Rest and a Shop on one route exists");
        host.Game.HuntMapSeed = seed;
        client.RequestReady(true); host.RequestReady(true); Sync();
        host.StartMatch();
        WaitForGame(host, client, dt);
        var g = host.Game; var c = client.Game;
        Check(g.HuntMapOn && c.HuntMapOn && g.Hunt != null && c.Hunt != null, "both games play the hunt as a map");
        Action keep = () =>
        {
            foreach (var p in g.Players.Values) { p.InvulnT = 1f; p.PendingPicks = p.PendingLegendary = p.PendingMods = p.PrizePicks = 0; p.Offer = null; p.LegendaryQueue.Clear(); }
            foreach (var e in g.Enemies.Values) if (!e.Def.IsBoss && g.Enemies.Count > 0) e.Hp = 0f;
        };
        Run(s, dt, 20f, () => g.HuntRoute != null && c.HuntRoute != null && c.HuntRoute.Seed == g.HuntRoute.Seed, keep);
        Check(g.HuntRoute != null && c.HuntRoute != null && c.HuntRoute.Seed == g.HuntRoute.Seed, "the host rolls the map and the friend gets the same one");
        Run(s, dt, 30f, () => g.Wave == 1 && g.State == MatchState.Fighting, keep);
        Check(g.HuntRoute.Current != null && g.HuntRoute.Current.Step == 0 && g.HuntNodeAt(1) == HuntNodeType.Skirmish, "the first wave is the map's first node, picked with no vote");

        var picked = new List<HuntNodeType> { HuntNodeType.Skirmish };
        int votes = 0, timerPicks = 0, blocked = 0, eliteChecks = 0, rests = 0, shops = 0;
        for (int w = 2; w <= Hunts.Length - 1; w++)
        {
            int want = w;
            int rerollsBefore = g.Players.Values.Sum(p => p.Rerolls);
            foreach (var p in g.Players.Values) p.Hp = p.MaxHp * 0.3f;
            // (a node with a single way on is picked at once, and a Rest is cleared at once: wait for the row's node to be picked or the vote to open)
            Run(s, dt, 200f, () => g.State == MatchState.Intermission && (g.HuntRoute.Step >= want || (g.HuntRoute.Step == want - 1 && (g.HuntMapVoting || g.HuntRoute.Current != null))), keep);
            if (g.HuntMapVoting)
            {
                var choices = g.HuntRoute.Choices();
                Run(s, dt, 1f, () => c.HuntMapVoting && c.HuntRoute.Step == want - 1, keep);
                Run(s, dt, 2f, null, keep);
                if (g.State == MatchState.Intermission && g.Wave == want - 1 && g.HuntMapVoting) blocked++;  // the countdown waits for the vote
                Check(c.HuntMapVoting && c.HuntRoute.Choices().SequenceEqual(choices), "the vote is open on both screens with the same choices (wave " + w + ")");
                int expect;
                if (w == 2)
                {
                    g.HuntMapSeconds = 0.05f;  // nobody votes: the timer picks
                    timerPicks++;
                    expect = -1;
                }
                else
                {
                    expect = choices.ToList().IndexOf(route[w - 1]);   // follow the route that has a Rest and a Shop on it
                    if (choices.Length > 1)
                    {
                        c.SendHuntVote((expect + 1) % choices.Length, true); Sync();  // the friend votes the other way: a tie, the host's vote wins
                    }
                    g.SendHuntVote(expect, true);
                }
                votes++;
                Run(s, dt, 3f, () => !g.HuntMapVoting && g.HuntRoute.Current != null && c.HuntRoute.Current != null, keep);
                int pickedIndex = g.HuntRoute.Step >= w ? g.HuntRoute.Path[w - 1] : g.HuntRoute.Current != null ? g.HuntRoute.Current.Index : -1;
                if (expect < 0)
                    route = FindRoute(g.HuntRoute, 2, new List<int> { 0, pickedIndex });   // the timer chose: follow a route from there
                Check(pickedIndex >= 0 && (expect < 0 || pickedIndex == route[w - 1]), "the vote picks the node (wave " + w + (expect < 0 ? ", the timer's pick" : "") + ")");
            }
            var type = g.HuntRoute.Step >= w ? g.HuntRoute.Rows[w - 1][g.HuntRoute.Path[w - 1]].Type : g.HuntRoute.Current.Type;
            if (type == HuntNodeType.Rest || type == HuntNodeType.Shop)
            {
                picked.Add(type);
                if (type == HuntNodeType.Rest)
                {
                    rests++;
                    Run(s, dt, 2f, () => g.HuntRoute.Step == want, keep);
                    if (w < Hunts.Length - 1)   // (a Rest right before the boss: the boss wave starts straight away)
                        Check(g.Players.Values.All(p => p.Hp >= p.MaxHp - 0.01f) && g.Players.Values.Sum(p => p.Rerolls) == rerollsBefore + g.Players.Count, "a Rest heals everyone to full and gives each a reroll (wave " + w + ")");
                }
                else
                {
                    shops++;
                    Run(s, dt, 3f, () => g.ShopOpen && c.ShopOpen, keep);
                    Check(g.ShopOpen && c.ShopOpen && g.HuntRoute.Current != null, "a Shop opens the Bazaar on both screens (wave " + w + ")");
                    foreach (var p in g.Players.Values) p.ShopDone = true;
                    Run(s, dt, 3f, () => !g.ShopOpen && g.HuntRoute.Step == want, keep);
                }
                Run(s, dt, 1f, () => c.HuntRoute.Step == w, keep);
                if (w < Hunts.Length - 1)
                Check(g.Wave == w && g.HuntRoute.Step == w && g.State == MatchState.Intermission && c.HuntRoute.Step == w, "a node with no fight clears with no wave: the wave number moves on and the break goes on (wave " + w + ")");
                continue;
            }
            Run(s, dt, 200f, () => g.Wave == w && g.State == MatchState.Fighting && c.HuntRoute != null && c.HuntRoute.Current != null && c.HuntRoute.Current.Index == g.HuntRoute.Current.Index, keep);
            Check(g.Wave == w && g.HuntNodeAt(w) == type && c.HuntNodeAt(w) == type, "wave " + w + " is the picked node's: " + HuntMap.Name(type) + ", on both screens");
            if (type == HuntNodeType.Elite)
            {
                eliteChecks++;
                Check(g.ApproachStepAt(w) == Math.Min(4, Hunts.Step(w) + 1) && g.Bosses.FinaleWave(w), "an Elite wave is a step tougher and has the faction's set piece");
            }
            else
                Check(g.ApproachStepAt(w) == Hunts.Step(w), "a Skirmish wave keeps the hunt's own strength");
            picked.Add(type);
        }
        Console.WriteLine("  hunt map route: {0} ({1} votes, {2} by the timer, {3} Elites, {4} Rests, {5} Shops)", string.Join(" > ", picked.Select(t => HuntMap.Name(t))), votes, timerPicks, eliteChecks, rests, shops);
        Check(rests >= 1 && shops >= 1, "the route had a Rest and a Shop");
        Check(blocked >= 1 || votes == 0, "the break's countdown waits for the vote");
        Run(s, dt, 200f, () => g.Wave == Hunts.Length && Find(host, EnemyType.Boss) != null && !g.IsSpawning(Find(host, EnemyType.Boss)), keep);
        Check(g.Wave == Hunts.Length && g.HuntNodeAt(Hunts.Length) == HuntNodeType.Boss && g.HuntRoute.Step == HuntMap.Steps - 1, "the last node is the boss, on wave 10");
        Check(g.HuntRoute.Clues == Math.Min(HuntMap.MaxClues, picked.Count(t => t == HuntNodeType.Elite)), "the Elites cleared count as boss clues");
        Find(host, EnemyType.Boss).Hp = 0f;
        Run(s, dt, 8f, () => c.State == MatchState.Victory, keep);
        Check(g.State == MatchState.Victory && c.State == MatchState.Victory, "beating the boss wins the hunt");
        host.Leave(""); client.Leave("");
        Profile.Folder = wasFolder;
        try { Directory.Delete(dir, true); } catch (IOException) { }
    }
}
