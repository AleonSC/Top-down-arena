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
            // The chart in Docs/Approaches.md: one new enemy a wave (Guard 2, Thug 3, Security Guard 4, Riot Shield 6, Gas 7, Taser 8), none on 5 and 9.
            Func<EnemyType, int> first = t => Enumerable.Range(1, 10).FirstOrDefault(w => has(w, t));
            Check(first(EnemyType.Guard) == 2 && first(EnemyType.Thug) == 3 && first(EnemyType.SecurityGuard) == 4,
                "the Guard first comes on wave 2, the Thug on 3 and the Security Guard on 4 (first waves: " + first(EnemyType.Guard) + ", " + first(EnemyType.Thug) + ", " + first(EnemyType.SecurityGuard) + ")");
            Check(first(EnemyType.RiotShield) == 6 && first(EnemyType.GasGrenadier) == 7 && first(EnemyType.TaserGuard) == 8,
                "the Riot Shield first comes on wave 6, the Gas Grenadier on 7 and the Taser Guard on 8 (first waves: " + first(EnemyType.RiotShield) + ", " + first(EnemyType.GasGrenadier) + ", " + first(EnemyType.TaserGuard) + ")");
            var before5 = new HashSet<EnemyType>(Enumerable.Range(1, 4).SelectMany(w => waves[w]));
            Check(waves[5].All(t => before5.Contains(t) || t == EnemyType.Shiv), "wave 5 is a recap: nothing new in it (bar the Shiv, which the Yard squad carries and the chart gives no intro wave)");
            Check(squads[2].All(n => n == "Rioter pack") && !squads[1].Any() && squads[4].All(n => n == "Yard" || n == "Guard pair"),
                "squads only hold enemies you have met: a Rioter pack on wave 2, Yard and Guard pair by wave 4");
            Check(squads.ContainsKey(10) && squads[10].Contains("Riot line") && !Enumerable.Range(1, 9).Any(w => squads.ContainsKey(w) && squads[w].Contains("Riot line")), "the Riot line is wave 10's exam, and only there");

            // The boss came after wave 10.
            Check(g.ShownWave == 10 && g.BossSlot, "the Warden comes after wave 10");

            // Wave 9 (track wave 5's first leg, "every squad, tighter") is mostly squads: build it a few times and count.
            var build = typeof(Game).GetMethod("BuildWave", flags);
            int sq9 = 0, all9 = 0;
            for (int i = 0; i < 6; i++)
            {
                g.Wave = 5; g.Leg = 0; g.BossSlot = false;
                build.Invoke(g, new object[] { 5 });
                sq9 += squadQ.Sum(x => x.Members.Length);
                all9 += squadQ.Sum(x => x.Members.Length) + spawnQ.Count;
            }
            spawnQ.Clear(); squadQ.Clear();
            Console.WriteLine("  wave 9 built six times: {0} of {1} enemies in squads", sq9, all9);
            Check(all9 > 0 && sq9 * 2 >= all9, "wave 9 is mostly squads");
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
