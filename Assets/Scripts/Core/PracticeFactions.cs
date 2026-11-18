using System;
using System.Collections.Generic;
using System.Linq;

namespace Arena
{
    /// One faction as the Practice picker groups it: its boss (null for the basic one and "Other"),
    /// a short name, and the pickable enemies in it (the boss and its mini-boss first).
    public class PickerFaction
    {
        public string Name;
        public EnemyType? Boss;
        public EnemyType[] Types;
    }

    /// The Practice picker's faction groups (Practice tab "by faction"): the basic faction, each boss's faction in Campaign
    /// order (Approaches.All), then everything else. Each pickable enemy is in exactly one group: the first that names it.
    public static class PracticeFactions
    {
        static readonly Dictionary<EnemyType, string> Names = new Dictionary<EnemyType, string>
        {
            { EnemyType.Boss, "Prison" }, { EnemyType.HiveQueen, "Hive" }, { EnemyType.TwinA, "Cult" },
            { EnemyType.Singularity, "Infall" }, { EnemyType.Colossus, "Quarry" }, { EnemyType.StormHerald, "Squall" },
            { EnemyType.Duelist, "Masked Court" }, { EnemyType.Lich, "Dead" }, { EnemyType.Devourer, "Larder" },
            { EnemyType.Eclipse, "Penumbral" },
        };

        public static string NameOf(EnemyType boss) => Names.TryGetValue(boss, out var n) ? n : PracticePlan.NameOf(boss);

        static List<PickerFaction> all;
        public static List<PickerFaction> All => all ?? (all = Build());

        static IEnumerable<EnemyType> TypesOf(FactionDef f)
        {
            foreach (var m in f.Members) yield return m.Type;
            foreach (var s in f.Squads) foreach (var t in s.Members) yield return t;
            foreach (var s in f.LessonSquads) foreach (var t in s.Members) yield return t;
            if (f.Finale != null) foreach (var t in f.Finale.Members) yield return t;
        }

        static List<PickerFaction> Build()
        {
            var pickable = new HashSet<EnemyType>(PracticePlan.Choices);
            var taken = new HashSet<EnemyType>();
            var list = new List<PickerFaction>();
            Func<IEnumerable<EnemyType>, EnemyType[]> claim = ts =>
                ts.Where(t => pickable.Contains(t) && taken.Add(t)).ToArray();
            list.Add(new PickerFaction { Name = "Basic", Types = claim(BasicFaction.Types) });
            foreach (var f in Approaches.All)
            {
                var head = new List<EnemyType> { f.Boss };
                var mini = FactionMinis.For(f.Boss);
                if (FactionMinis.Is(mini)) head.Add(mini);
                list.Add(new PickerFaction { Name = NameOf(f.Boss), Boss = f.Boss, Types = claim(head.Concat(TypesOf(f).OrderBy(t => (int)t))) });
            }
            var rest = claim(PracticePlan.Choices);
            if (rest.Length > 0)
                list.Add(new PickerFaction { Name = "Other", Types = rest });
            return list;
        }

        /// The group an enemy is in (its index in All).
        public static int GroupOf(EnemyType t) => All.FindIndex(g => Array.IndexOf(g.Types, t) >= 0);
    }
}
