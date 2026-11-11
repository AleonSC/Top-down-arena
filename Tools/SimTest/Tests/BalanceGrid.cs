using System;
using System.Linq;
using System.Collections.Generic;
using Arena;

// The balance grid: every role, class and weapon fights the boss of each listed wave in the Boss Lab, and each fight's
// numbers go out as a GRID line (Tools/BalanceGrid/grid.sh runs it in parallel and draws the heatmap).
//   run.sh 1 1 grid <waves, e.g. 10,20,30> [runs] [roles DTH] [skill 1-3] [classes, e.g. Knight,Ninja]
// Env: GRIDTEAM=T/K/0,H/M/1 (teammates beside the tested seat; default: it fights alone; auto: a Soldier Tank, Soldier DPS
//      or Elementalist Healer in each of the two other roles, the same for every row),
//      GRIDSHARD=i/n (this process runs every n-th fight from the i-th, for parallel runs; give each its own PORTBASE).
static partial class Program
{
    static void BalanceGrid(string[] args)
    {
        var waves = (args.Length > 3 ? args[3] : "10,20,30").Split(',').Select(int.Parse).ToArray();
        int runs = Arg(args, 4, 2);
        string roles = args.Length > 5 ? args[5].ToUpperInvariant() : "DTH";
        var skill = (BotSkill)Arg(args, 6, 2);
        var only = args.Length > 7 ? args[7].Split(',') : null;
        var team = (Environment.GetEnvironmentVariable("GRIDTEAM") ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        int shard = 0, shards = 1;
        var sh = Environment.GetEnvironmentVariable("GRIDSHARD");
        if (sh != null) { shard = int.Parse(sh.Split('/')[0]); shards = int.Parse(sh.Split('/')[1]); }

        var jobs = new List<Tuple<Role, Kit, int, int>>();
        foreach (char rc in roles)
        {
            var role = rc == 'T' ? Role.Tank : rc == 'H' ? Role.Healer : Role.Dps;
            foreach (var k in Classes.All)
            {
                if (only != null && !only.Any(o => k.Name.StartsWith(o, StringComparison.OrdinalIgnoreCase))) continue;
                for (int w = 0; w < Defs.WeaponCount(role, k.Id); w++)
                    foreach (int wave in waves)
                        jobs.Add(Tuple.Create(role, k.Id, w, wave));
            }
        }
        Console.WriteLine("GRIDHEAD,role,class,weapon,wave,boss,run,outcome,time,bosshp,dps,downs,taken");
        for (int j = shard; j < jobs.Count; j += shards)
        {
            var job = jobs[j];
            string seat = (job.Item1 == Role.Tank ? "T" : job.Item1 == Role.Healer ? "H" : "D") + "/" + Classes.Get(job.Item2).Name + "/" + job.Item3;
            string weapon = Defs.Weapon(job.Item1, job.Item2, job.Item3).Name;
            var mates = team.Length == 1 && team[0] == "auto" ? new[] { "T/S/0", "D/S/0", "H/M/0" }.Where(m => m[0] != seat[0]).ToArray() : team;
            for (int run = 0; run < runs; run++)
            {
                var lab = LabFight(job.Item4, skill, new[] { seat }.Concat(mates).ToArray(), 1000 + run);
                var me = lab.Stats.OrderBy(kv => kv.Key).Select(kv => kv.Value).FirstOrDefault() ?? new LabPlayerStats();
                float t = Math.Max(1f, lab.FightTime);
                Console.WriteLine(string.Join(",", "GRID", job.Item1, Classes.Get(job.Item2).Name, weapon.Replace(",", " "), job.Item4, lab.BossName.Replace(",", " "), run,
                    lab.Outcome, lab.FightTime.ToString("0.0"), (lab.BossHpFrac * 100f).ToString("0"), (me.BossDamage / t).ToString("0"), me.Downs, me.DamageTaken.ToString("0")));
                lab.Stop();
            }
        }
    }
}
