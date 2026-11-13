using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

// The basic faction (BasicFaction.cs): the eight plain enemies left in the waves, Quick Play's build-up for a boss with
// no faction of its own, and no retired enemy in any wave builder.
static partial class Program
{
    static void BasicFactionTest()
    {
        var NP = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var build = typeof(Game).GetMethod("BuildWave", NP);
        var queueF = typeof(Game).GetField("spawnQueue", NP);
        var squadsF = typeof(Game).GetField("squadQueue", NP);
        Func<Game, int, List<EnemyType>> wave = (g, n) =>
        {
            g.Wave = n - 1; g.BossSlot = false;
            build.Invoke(g, new object[] { n });
            var all = new List<EnemyType>((List<EnemyType>)queueF.GetValue(g));
            foreach (var s in (List<SquadDef>)squadsF.GetValue(g))
                all.AddRange(s.Members);
            return all;
        };
        var basic = new HashSet<EnemyType>(BasicFaction.Types);

        Check(BasicFaction.Types.Length == 8 && !BasicFaction.Types.Any(Game.IsRetired)
            && BasicFaction.Def.Members.All(m => basic.Contains(m.Type))
            && BasicFaction.Def.Squads.Concat(new[] { BasicFaction.Def.Finale }).All(q => q.Members.All(basic.Contains))
            && Approaches.All.All(f => f != BasicFaction.Def),
            "the basic faction is eight plain enemies, none retired; its squads and set piece use only them; no boss owns it");

        // Quick Play: a boss with no faction (the Storm Herald) is led up to by the basic faction alone; a faction boss by its own.
        var gq = new Session().Game;
        gq.Quick = true;
        gq.QuickOrder = new[] { EnemyType.StormHerald, EnemyType.Boss };
        var duel = new HashSet<EnemyType>();
        var ward = new HashSet<EnemyType>();
        for (int n = 1; n <= 9; n++) foreach (var t in wave(gq, n)) duel.Add(t);
        for (int n = 11; n <= 19; n++) foreach (var t in wave(gq, n)) ward.Add(t);
        var wardens = new HashSet<EnemyType>(Approaches.For(EnemyType.Boss).Members.Select(m => m.Type));
        Console.WriteLine("  Quick Play before the Storm Herald: {0}", string.Join(", ", duel));
        Check(gq.BossAt(10) == EnemyType.StormHerald && Approaches.For(EnemyType.StormHerald) == null && gq.ApproachShare(5) == 1f
            && duel.Count >= 6 && duel.All(basic.Contains), "Quick Play: the waves before a boss with no faction (the Storm Herald) are all the basic faction");
        Check(gq.BossAt(20) == EnemyType.Boss && ward.All(wardens.Contains), "Quick Play: the waves before the Warden are still all his own");

        // The Campaign and plain runs: no retired enemy in any wave, ever (squads, the recipe, paths' pools included).
        var gc = new Session().Game;
        var seen = new HashSet<EnemyType>();
        for (int n = 1; n <= 60; n++)
            if (n % 5 != 0)
                for (int k = 0; k < 2; k++)
                    foreach (var t in wave(gc, n)) seen.Add(t);
        var retired = seen.Where(Game.IsRetired).ToList();
        Console.WriteLine("  Campaign waves 1-60: {0} types; retired seen: {1}", seen.Count, retired.Count == 0 ? "none" : string.Join(", ", retired));
        Check(retired.Count == 0 && BasicFaction.Types.All(seen.Contains), "no retired enemy in the Campaign's waves 1-60; all eight basic ones show up");
        Check(Enumerable.Range(1, 60).All(n => Game.WavePool(n).All(t => !Game.IsRetired(t))), "the wave pool holds only basic enemies");
    }
}
