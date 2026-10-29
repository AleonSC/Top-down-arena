using System.Linq;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using Arena;

// The Boss Lab: its bot fights from the command line, and its test.
static partial class Program
{
    // Boss Lab from the command line: run.sh 1 1 lab <wave> <skill 1-3> <runs> <seat> [seat] [seat]
    // A seat is role/class/weapon, e.g. T/S/0 (Tank Soldier, first weapon), H/M/1, D/U/2 (U = summoner).
    public static List<LabOutcome> LabRun(int wave, BotSkill skill, int runs, string[] seats, bool quiet = false)
    {
        float dt = 1f / 60f;
        var outcomes = new List<LabOutcome>();
        float timeSum = 0f, hpSum = 0f;
        int downs = 0;
        var sources = new Dictionary<string, float>();
        for (int run = 0; run < runs; run++)
        {
            var lab = new BossLab { Wave = wave, Skill = skill, PlayerCount = seats.Length, Seed = 1000 + run };
            for (int i = 0; i < seats.Length; i++)
            {
                var parts = seats[i].Split('/');
                lab.Seats[i].Role = parts[0] == "T" ? Role.Tank : parts[0] == "H" ? Role.Healer : Role.Dps;
                lab.Seats[i].Kit = parts.Length > 1 && parts[1] == "M" ? Kit.Mage : parts.Length > 1 && parts[1] == "U" ? Kit.Summoner : parts.Length > 1 && parts[1] == "K" ? Kit.Knight : parts.Length > 1 && parts[1] == "N" ? Kit.Ninja : parts.Length > 1 && parts[1] == "B" ? Kit.BloodMage : Kit.Soldier;
                lab.Seats[i].Weapon = parts.Length > 2 ? int.Parse(parts[2]) : 0;
            }
            var host = new Session();
            lab.Start(host, nextPort++);
            for (int i = 0; i < 300 && !(host.Game.Running && host.Game.Players.Count >= seats.Length); i++)
            {
                Step(host, dt); lab.StepClients(dt); lab.UpdateClients(dt);
                Thread.Sleep(2);
            }
            int frames = 0;
            while (lab.Outcome == LabOutcome.Running && frames < (BossLab.TimeLimit + 30f) * 60)
            {
                Step(host, dt); lab.StepClients(dt); lab.UpdateClients(dt); lab.Watch(dt);
                frames++;
            }
            outcomes.Add(lab.Outcome);
            timeSum += lab.FightTime;
            hpSum += lab.BossHpFrac;
            foreach (var st in lab.Stats.Values)
            {
                downs += st.Downs;
                foreach (var kv in st.TakenFrom)
                {
                    float had; sources.TryGetValue(kv.Key, out had); sources[kv.Key] = had + kv.Value;
                }
            }
            if (!quiet)
            {
                Console.WriteLine("  run {0}: {1} vs {2} in {3:0}s, boss HP left {4:0}%, peak enemy bullets {5}, team boss DPS {6:0}", run + 1, lab.Outcome, lab.BossName, lab.FightTime, lab.BossHpFrac * 100f, lab.PeakBullets, lab.TeamBossDps);
                foreach (var st in lab.Stats.Values)
                    Console.WriteLine("     {0}: boss dmg {1:0}, taken {2:0}, healed {3:0}, downs {4}, abilities used {5}", st.Name, st.BossDamage, st.DamageTaken, st.Healing, st.Downs, st.Abilities);
            }
            lab.Stop();
        }
        Console.WriteLine("  wave {0} {1} [{2}] x{3}: won {4}, lost {5}, timed out {6}; avg fight {7:0}s, avg boss HP left {8:0}%, downs/run {9:0.0}",
            wave, skill, string.Join(" ", seats), runs, outcomes.Count(o => o == LabOutcome.Won), outcomes.Count(o => o == LabOutcome.Lost), outcomes.Count(o => o == LabOutcome.TimedOut),
            timeSum / runs, hpSum / runs * 100f, (float)downs / runs);
        Console.WriteLine("     damage taken from: " + string.Join(", ", sources.OrderByDescending(kv => kv.Value).Take(6).Select(kv => kv.Key + " " + (int)(kv.Value / runs))));
        return outcomes;
    }

    // Level (upgrade picks) a team of Good bots has when each boss wave starts.
    static void LevelReport(float dt)
    {
        var host = new Session { AutoStart = 2, StartWave = 1 };
        host.Game.Bot = true; host.Game.BotLevel = BotSkill.Good; host.Game.BotRerolls = false;
        int port = nextPort++;
        host.Host(port, "A", Role.Tank, 0);
        var c = new Session(); c.Game.Bot = true; c.Game.BotLevel = BotSkill.Good; c.Game.BotRerolls = false;
        c.Join("127.0.0.1", port, "B", Role.Dps, 0);
        var s = new[] { host, c };
        for (int i = 0; i < 300 && c.Screen != AppScreen.Game; i++) { Step(host, dt); Step(c, dt); Thread.Sleep(2); }
        int lastWave = 0;
        for (int f = 0; f < 60 * 60 * 40 && host.Game.State != MatchState.GameOver && host.Game.Wave <= 30; f++)
        {
            Step(host, dt); Step(c, dt);
            if (host.Game.Wave != lastWave && host.Game.State == MatchState.Fighting)
            {
                lastWave = host.Game.Wave;
                var ps = host.Game.Players.Values.ToList();
                Console.WriteLine("  wave {0,2} at {1,4:0}s: levels {2}, picks taken {3}, downs {4}", lastWave, host.Game.RunTime,
                    string.Join("/", ps.Select(p => p.Level)), string.Join("/", ps.Select(p => p.Taken.Count)), string.Join("/", ps.Select(p => p.Run.Downs)));
            }
        }
        Console.WriteLine("  ended: state {0} wave {1}", host.Game.State, host.Game.Wave);
        host.Leave(""); c.Leave("");
    }

    // Boss Lab: a party of smart bots fights the Warden, and the lab keeps score.
    static void BossLabTest(float dt)
    {
        var good = LabRun(5, BotSkill.Good, 1, new[] { "T/S/0", "H/M/1" }, true);
        Check(good[0] == LabOutcome.Won, "Boss Lab: Good bots (Mage Healer + Soldier Tank) beat the Warden");
        var lab = new BossLab { Wave = 15, Skill = BotSkill.Casual, PlayerCount = 3, Seed = 7 };
        lab.Seats[2].Kit = Kit.Summoner;
        var host = new Session();
        lab.Start(host, nextPort++);
        for (int i = 0; i < 300 && !(host.Game.Running && host.Game.Players.Count >= 3); i++) { Step(host, dt); lab.StepClients(dt); lab.UpdateClients(dt); Thread.Sleep(2); }
        for (int f = 0; f < 60 * 12; f++) { Step(host, dt); lab.StepClients(dt); lab.UpdateClients(dt); lab.Watch(dt); }
        var p3 = host.Game.Players.Values.FirstOrDefault(p => p.Kit == Kit.Summoner);
        Console.WriteLine("  lab: {0} players, boss {1}, fight {2:0.0}s, boss hp {3:0}%, picks each {4}, summoner {5}",
            host.Game.Players.Count, lab.BossName, lab.FightTime, lab.BossHpFrac * 100f, string.Join("/", host.Game.Players.Values.Select(p => p.Taken.Count)), p3 != null ? p3.Weapon.Name : "-");
        Check(host.Game.Players.Count == 3 && lab.BossName == "Twin Reapers" && lab.FightTime > 3f && lab.Stats.Count == 3, "Boss Lab sets up the chosen boss and party and keeps score");
        Check(host.Game.Players.Values.All(p => p.Taken.Count >= BossLab.TypicalPicks(15) + 4), "lab players get the typical upgrades, legendaries and mods for the wave");
        Check(lab.Clients.All(c => c.Game.BotLevel == BotSkill.Casual) && host.Game.BotLevel == BotSkill.Casual, "every lab seat is played by a bot of the chosen skill");
        lab.Stop();
        Check(host.Screen == AppScreen.Menu && lab.Clients.Count == 0, "stopping the lab closes every seat");
    }
}
