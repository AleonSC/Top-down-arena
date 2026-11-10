using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

// The Expanding arena's economy (Docs/VectorBuilding.md, step 5): a duo of Good bots plays the same seed on the Classic arena and on
// the Expanding arena; each regular wave's length and damage are printed side by side, then each building round: the wave it came
// after, the rooms so far, the vectors every player held against their cap, how many proposals there were, and what was built and
// what it cost. Run: run.sh 1 1 vecbal [last wave] [seed] [1 = skip the Classic run]
static partial class Program
{
    static void VectorBalanceReport(float dt, int lastWave, int seed, bool skipClassic = false)
    {
        string sc = Environment.GetEnvironmentVariable("VECSCALE");  // VECSCALE=<rate>: try another crowd scale rate
        if (sc != null) Game.ExpandScaleRate = float.Parse(sc, System.Globalization.CultureInfo.InvariantCulture);
        string endC = "(skipped)", endE;
        var classic = skipClassic ? new Dictionary<int, ArenaWave>() : PlayArena(dt, false, lastWave, seed, out endC);
        var expanding = PlayArena(dt, false, lastWave, seed, out endE, true);
        var rounds = arenaRounds;
        Console.WriteLine("  Good bot duo, seed {0}. Classic: {1}. Expanding: {2}.", seed, endC, endE);
        Console.WriteLine("  wave | length s     | dmg taken % max HP | downs");
        Console.WriteLine("       | Class  Expd  | Class   Expd       | C  E");
        foreach (int w in classic.Keys.Union(expanding.Keys).OrderBy(x => x))
        {
            ArenaWave a, b;
            classic.TryGetValue(w, out a); expanding.TryGetValue(w, out b);
            Func<ArenaWave, Func<ArenaWave, string>, string> S = (x, fmt) => x == null ? "-" : fmt(x);
            Console.WriteLine("  {0,4} | {1,5} {2,5}  | {3,5}   {4,5}      | {5} {6}", w, S(a, x => x.Length.ToString("0")), S(b, x => x.Length.ToString("0")),
                S(a, x => x.TakenPct.ToString("0")), S(b, x => x.TakenPct.ToString("0")), S(a, x => x.Downs.ToString()), S(b, x => x.Downs.ToString()));
        }
        Func<Dictionary<int, ArenaWave>, Func<ArenaWave, float>, float> Avg = (d, sel) => d.Count == 0 ? 0f : d.Values.Average(sel);
        Console.WriteLine("  average | {0:0} {1:0} s | {2:0}% {3:0}% taken | downs {4} {5}", Avg(classic, x => x.Length), Avg(expanding, x => x.Length), Avg(classic, x => x.TakenPct), Avg(expanding, x => x.TakenPct),
            classic.Values.Sum(x => x.Downs), expanding.Values.Sum(x => x.Downs));
        Console.WriteLine();
        Console.WriteLine("  round after wave | rooms | crowd x | vectors held / cap (each player) | proposals | built (cost, by)");
        foreach (var r in rounds)
            Console.WriteLine("  {0,16} | {1,5} | {2,7:0.00} | {3,-32} | {4,9} | {5}", r.Wave, r.Rooms, r.Scale,
                string.Join("  ", r.Vectors.Select((v, i) => v + "/" + r.Caps[i]).ToArray()), r.Proposals, r.BuiltCost > 0 ? r.BuiltCost + " by " + r.BuiltOwner : "nothing");
        int built = rounds.Count(r => r.BuiltCost > 0);
        int atCap = rounds.Sum(r => r.Vectors.Where((v, i) => v >= r.Caps[i]).Count()), seats = rounds.Sum(r => r.Vectors.Length);
        Console.WriteLine("  {0} rounds, {1} rooms built, {2} of {3} player-rounds opened at the cap (income wasted), average cost {4:0.0} vectors",
            rounds.Count, built, atCap, seats, built == 0 ? 0f : (float)rounds.Where(r => r.BuiltCost > 0).Average(r => r.BuiltCost));
    }
}
