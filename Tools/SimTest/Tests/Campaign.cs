using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// The campaign buildup (Approaches.Campaign, Docs/Campaign.md): every wave themed, the last boss's faction fading out,
// the older ones a pool, the next boss's building up.
static partial class Program
{
    static void CampaignTest()
    {
        var np = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        // --- The slices, wave by wave ------------------------------------------------------------------------------------
        Func<int, Dictionary<EnemyType, float>> slices = n => Approaches.Campaign(n).ToDictionary(s => s.Faction.Boss, s => s.Share);
        var w1 = slices(1);
        Check(w1.Count == 1 && Math.Abs(w1[EnemyType.Boss] - 1f) < 1e-4f, "waves 1-5: all Warden's faction (nothing before it to fade)");
        var all50 = Enumerable.Range(1, 60).Select(n => Approaches.Campaign(n).Sum(s => s.Share)).ToList();
        Check(all50.All(t => t <= 1.0001f), "no wave's slices add up to more than 100%");
        // The Hive Queen (6-10): her faction builds the rest while the Warden's fades.
        var w6 = slices(6);
        Check(w6.ContainsKey(EnemyType.Boss) && Math.Abs(w6[EnemyType.Boss] - 0.35f) < 1e-4f && w6.ContainsKey(EnemyType.HiveQueen) && Math.Abs(w6[EnemyType.HiveQueen] - 0.65f) < 1e-4f && w6.Count == 2, "wave 6: the Warden's faction fades (35%) and the Hive Queen's builds the rest (65%)");
        var fade = Enumerable.Range(6, 5).Select(n => slices(n).ContainsKey(EnemyType.Boss) ? slices(n)[EnemyType.Boss] : 0f).ToArray();
        Check(fade.SequenceEqual(new[] { 0.35f, 0.25f, 0.15f, 0.05f, 0f }), "the fading slice goes 35, 25, 15, 5, 0% over the next five waves");
        // The Lich (31-35): current builds, fading Duelist unbuilt, pool of built earlier factions (the Warden's, the Hive Queen's, the Singularity's).
        var w31 = slices(31);
        float third = 0.1f / 3f;
        Check(Math.Abs(w31[EnemyType.Lich] - 0.55f) < 1e-4f && Math.Abs(w31[EnemyType.Boss] - third) < 1e-4f && Math.Abs(w31[EnemyType.HiveQueen] - third) < 1e-4f
            && Math.Abs(w31[EnemyType.Singularity] - third) < 1e-4f && w31.Count == 4,
            "wave 31: the Lich's faction 55% (65% minus the 10% pool), the Warden's, the Hive Queen's and the Singularity's a third of the older pool each");
        var w35 = slices(35);
        Check(Math.Abs(w35[EnemyType.Lich] - 0.90f) < 1e-4f, "wave 35: the Lich's faction is 90%");
        var w36 = slices(36);
        Check(Math.Abs(w36[EnemyType.Lich] - 0.35f) < 1e-4f && w36.ContainsKey(EnemyType.Boss), "wave 36: the Lich's fades at 35% while the Warden's stays in the pool");
        Check(Enumerable.Range(1, 120).All(n => Approaches.Campaign(n).SequenceEqual(Approaches.Campaign(n), new SliceCmp())), "the slices depend on the wave alone");

        // --- Waves built from them ---------------------------------------------------------------------------------------
        var build = typeof(Game).GetMethod("BuildWave", np);
        var queueF = typeof(Game).GetField("spawnQueue", np);
        var squadsF = typeof(Game).GetField("squadQueue", np);
        Func<int, bool, List<EnemyType>> wave = (n, on) =>
        {
            Game.SeedForTests(777 + n);
            var g = new Session().Game;
            g.Diff.Set(DiffKnob.Campaign, on ? 1f : 0f);
            g.Diff.Set(DiffKnob.ApproachShare, 0f);
            build.Invoke(g, new object[] { n });
            var l = new List<EnemyType>((List<EnemyType>)queueF.GetValue(g));
            foreach (var s in (List<SquadDef>)squadsF.GetValue(g))
                l.AddRange(s.Members);
            return l;
        };
        var warden = new HashSet<EnemyType>(Approaches.For(EnemyType.Boss).Members.Select(m => m.Type));
        var zombie = new HashSet<EnemyType>(Approaches.For(EnemyType.Lich).Members.Select(m => m.Type));
        var a1 = wave(1, true);
        Check(a1.Count > 0 && a1.All(warden.Contains), "campaign wave 1 is all Warden's faction (" + a1.Count + " enemies)");
        Check(wave(1, true).SequenceEqual(a1), "the same seed builds the same campaign wave");
        var a7 = wave(7, true);  // Warden fading 25%, rest usual mix
        int w7 = a7.Count(warden.Contains);
        Check(w7 >= a7.Count * 0.15f && w7 <= a7.Count * 0.45f, "wave 7: about a quarter Warden's (" + w7 + " of " + a7.Count + ")");
        var a10 = wave(10, true);
        Check(!a10.Any(warden.Contains), "wave 10: the Warden's is gone from the mix (Hive Queen has none yet)");
        var a33 = wave(33, true);
        int z33 = a33.Count(zombie.Contains), p33 = a33.Count(warden.Contains);
        Console.WriteLine("  wave 33 campaign: {0} enemies, {1} zombies, {2} Warden's (older pool)", a33.Count, z33, p33);
        Check(z33 >= a33.Count * 0.55f && p33 > 0 && p33 <= a33.Count * 0.25f, "wave 33: mostly the Lich's zombies, with a few of the Warden's from the older pool");
        var a41 = wave(41, true);
        Check(a41.Count(zombie.Contains) > 0 && a41.Count(zombie.Contains) <= a41.Count * 0.2f && a41.Count(warden.Contains) <= a41.Count * 0.25f, "wave 41: the Lich's has faded to the pool");
        // --- Comeback squads (step 2) -------------------------------------------------------------------------------------
        FactionDef cf;
        var cbWaves = Enumerable.Range(1, 120).Where(n => Approaches.Comeback(n, out cf) != null).ToList();
        Console.WriteLine("  comeback waves 1-60: " + string.Join(" ", cbWaves.Where(n => n <= 60)));
        int inRun = cbWaves.Count(n => n >= 11 && n <= 50);
        Check(cbWaves.All(n => n >= 11 && (n - 1) % 5 != 4), "comeback squads never come before wave 11 or on the last wave of a cycle");
        Check(inRun == 8 && Enumerable.Range(2, 8).All(c => cbWaves.Count(n => (n - 1) / 5 == c) == 1), "one wave in every five from wave 11 has a comeback squad (" + inRun + " in waves 11-50)");
        Check(cbWaves.All(n => { FactionDef f; var sq = Approaches.Comeback(n, out f); return f != null && Array.IndexOf(f.Squads, sq) >= 0 && Approaches.Campaign(n).Any(x => x.Faction == f && x.Pool > 0f); }),
            "every comeback squad is one of an older faction's own squads");
        Check(cbWaves.All(n => { FactionDef a, b; return Approaches.Comeback(n, out a) == Approaches.Comeback(n, out b) && a == b; }), "the comeback depends on the wave alone");
        int cw = cbWaves.First(n => n >= 31);
        FactionDef cfrom;
        var csq = Approaches.Comeback(cw, out cfrom);
        var cwave = wave(cw, true);
        Check(csq.Members.GroupBy(t => t).All(gr => cwave.Count(t => t == gr.Key) >= gr.Count()), "wave " + cw + " has the " + csq.Name + " squad's members");
        var g0 = new Session().Game;
        g0.Diff.Set(DiffKnob.Campaign, 0f);
        Check(g0.ApproachShare(6) > 0f && g0.ApproachShare(1) > 0f && g0.ApproachShare(11) == 0f, "off: the old approach share applies (the Hive Queen's on waves 6-9, none before the Reapers)");
        var gOn = new Session().Game;
        Check(gOn.CampaignOn && Math.Abs(gOn.ApproachShare(1) - 1f) < 1e-4f && Math.Abs(gOn.ApproachShare(6) - 0.65f) < 1e-4f && Math.Abs(gOn.ApproachShare(31) - 0.55f) < 1e-4f, "on (the default): the wave panel's approach share is the next boss's slice");
    }

    class SliceCmp : IEqualityComparer<Approaches.Slice>
    {
        public bool Equals(Approaches.Slice a, Approaches.Slice b) => a.Faction == b.Faction && a.Share == b.Share && a.Current == b.Current;
        public int GetHashCode(Approaches.Slice s) => s.Share.GetHashCode();
    }
}
