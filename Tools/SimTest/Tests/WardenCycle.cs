using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Arena;

// The Campaign's Warden cycle (Docs/RunLength.md step 3): each of its ten waves uses one lesson's enemies
// (Approaches.cs WaveLesson), each with its star enemy.
static partial class Program
{
    static void WardenCycleTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        Game.LongRunDefault = true;
        try
        {
            int port = nextPort++;
            var host = HostAs(new Session { AutoStart = 2, StartWave = 1 }, port, false, "Host", Role.Tank);
            host.Diff.Set(DiffKnob.Campaign, 1f);  // (the test runner turns the campaign buildup off; the lessons are part of it)
            var client = JoinAs(port, false, "Friend", Role.Dps);
            var s = new[] { host, client };
            WaitForGame(host, client, dt);
            var g = host.Game; var cg = client.Game;
            g.DevSetGod(1, true); g.DevSetGod(2, true);
            var spawnQ = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
            var squadQ = (List<SquadDef>)typeof(Game).GetField("squadQueue", flags).GetValue(g);
            Action NoPicks = () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = 0; p.LegendaryQueue.Clear(); p.Offer = null; } };

            // Each Campaign wave of the cycle: what it brings (read as it starts, then everything is cleared away).
            var waves = new Dictionary<int, HashSet<EnemyType>>();
            var squads = new Dictionary<int, HashSet<string>>();
            var inSquads = new Dictionary<int, int>();
            var singles = new Dictionary<int, int>();
            int seenKey = -1;
            Run(s, dt, 300f, () => g.BossSlot && g.State == MatchState.Fighting, () =>
            {
                NoPicks();
                if (g.State == MatchState.Fighting && !g.BossSlot && !g.InRoom)
                {
                    int key = g.ShownWave;
                    if (key != seenKey)
                    {
                        seenKey = key;
                        var types = new HashSet<EnemyType>(spawnQ);
                        foreach (var e in g.Enemies.Values) types.Add(e.Type);
                        var names = new HashSet<string>();
                        foreach (var sq in squadQ) { names.Add(sq.Name); foreach (var m in sq.Members) types.Add(m); }
                        waves[key] = types;
                        inSquads[key] = squadQ.Sum(sq => sq.Members.Length);
                        singles[key] = spawnQ.Count + g.Enemies.Count;
                        squads[key] = names;
                    }
                    spawnQ.Clear();
                    squadQ.Clear();
                    foreach (var e in g.Enemies.Values) e.Hp = 0f;
                }
            });
            Func<int, string> show = w => waves.ContainsKey(w) ? string.Join(",", waves[w].Select(t => t.ToString()).OrderBy(x => x)) + (squads[w].Count > 0 ? " [" + string.Join(",", squads[w]) + "]" : "") : "-";
            for (int w = 1; w <= 10; w++)
                Console.WriteLine("  wave {0,2}: {1}", w, show(w));
            Func<int, EnemyType, bool> has = (w, t) => waves.ContainsKey(w) && waves[w].Contains(t);
            Func<int, EnemyType[], bool> only = (w, ok) => waves.ContainsKey(w) && waves[w].All(t => ok.Contains(t));
            Check(waves.Count == 10, "all ten Campaign waves of the Warden's cycle were played");
            Check(only(1, new[] { EnemyType.Rioter }) && has(1, EnemyType.Rioter), "wave 1 is Rioters only");
            Check(has(2, EnemyType.SecurityGuard) && !Enumerable.Range(1, 2).Any(w => has(w, EnemyType.RiotShield)), "wave 2 brings Security Guards, and no Riot Shield comes before wave 3");
            Check(has(3, EnemyType.RiotShield) && !Enumerable.Range(1, 3).Any(w => has(w, EnemyType.GasGrenadier)), "wave 3 brings Riot Shields, and no Gas Grenadier before wave 4");
            Check(has(4, EnemyType.GasGrenadier) && has(4, EnemyType.RiotShield), "wave 4 is gas with shields");
            Check(has(6, EnemyType.TaserGuard) && !Enumerable.Range(1, 5).Any(w => has(w, EnemyType.TaserGuard)), "Taser Guards first come on wave 6");
            Check(squads.ContainsKey(10) && squads[10].Contains("Riot line") && !Enumerable.Range(1, 9).Any(w => squads.ContainsKey(w) && squads[w].Contains("Riot line")), "the Riot line is wave 10's exam, and only there");
            Check(has(7, EnemyType.TaserGuard) && has(7, EnemyType.RiotShield), "wave 7 puts the Taser Guards with the shields");

            // The boss came after wave 10.
            Check(g.ShownWave == 10 && g.BossSlot, "the Warden comes after wave 10");

            // Wave 8 (track wave 4's second leg) is mostly squads: build it a few times and count.
            var build = typeof(Game).GetMethod("BuildWave", flags);
            int sq8 = 0, all8 = 0;
            for (int i = 0; i < 6; i++)
            {
                g.Wave = 4; g.Leg = 1; g.BossSlot = false;
                build.Invoke(g, new object[] { 4 });
                sq8 += squadQ.Sum(x => x.Members.Length);
                all8 += squadQ.Sum(x => x.Members.Length) + spawnQ.Count;
            }
            spawnQ.Clear(); squadQ.Clear();
            Console.WriteLine("  wave 8 built six times: {0} of {1} enemies in squads", sq8, all8);
            Check(all8 > 0 && sq8 * 2 >= all8, "wave 8 is mostly squads");
            host.Leave(""); client.Leave("");

            // A Boss Hunt and a 50-wave run don't use the lessons.
            var hunt = new Game(new Session()) { LongRun = true, Hunt = Hunts.All[0] };
            var old = new Game(new Session()) { LongRun = false };
            var f = Approaches.For(EnemyType.Boss);
            Check(hunt.LessonNow(f) == null && old.LessonNow(f) == null && f.Lessons.Length == 10, "the lessons are the Campaign's only (not a Boss Hunt's or an old 50-wave run's)");
        }
        finally
        {
            Game.LongRunDefault = false;
        }
    }
}
