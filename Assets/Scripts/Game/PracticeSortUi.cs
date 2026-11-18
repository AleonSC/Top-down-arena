using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Arena
{
    /// The Practice tab's enemy grid organised (PracticeUi.cs DrawEnemyGrid): one scrolling list in sections, by faction (each
    /// in its boss's colour, the scroll bar striped to match), by job, or A-Z; bosses last in the latter two.
    public partial class ArenaApp
    {
        class PickerSection
        {
            public string Head;
            public Color Ink;
            public EnemyType[] Types;
        }

        static Color FactionInk(PickerFaction f) =>
            f.Boss != null ? Color.Lerp(ToColor(Defs.Get(f.Boss.Value).Color), Color.white, 0.25f) : f.Name == "Basic" ? CrtUi.Ink : CrtUi.Dim;

        static string FactionHead(PickerFaction f) =>
            f.Boss == null ? (f.Name == "Basic" ? "BASIC  ·  ONE OF EACH ROLE" : "OTHER") : f.Name.ToUpperInvariant() + "  ·  " + PracticePlan.NameOf(f.Boss.Value).ToUpperInvariant();

        static readonly string[] JobHeads =
        {
            "PRESSURE  ·  SHOOTS", "HUNTERS  ·  COME FOR YOU", "ZONERS  ·  TAKE GROUND", "DISRUPTORS  ·  BREAK YOUR SPOT", "ANCHORS  ·  KILL FIRST",
        };

        /// The grid's sections. sort 0: by faction; 1: by job (bosses last); 2: A-Z (bosses last).
        static List<PickerSection> PracticeSections(int sort)
        {
            var all = PracticeFactions.All;
            var pool = PracticePlan.Choices;
            var list = new List<PickerSection>();
            Action<string, Color, IEnumerable<EnemyType>> add = (h, c, ts) =>
            {
                var a = ts.ToArray();
                if (a.Length > 0)
                    list.Add(new PickerSection { Head = h, Ink = c, Types = a });
            };
            var bosses = pool.Where(x => Defs.Get(x).IsBoss).ToArray();
            var plain = pool.Where(x => !Defs.Get(x).IsBoss).ToArray();
            if (sort == 0)
                foreach (var f in all)
                    add(FactionHead(f), FactionInk(f), f.Types);
            else
            {
                if (sort == 1)
                    for (int j = 0; j < JobHeads.Length; j++)
                        add(JobHeads[j], CrtUi.Ink, plain.Where(x => (int)Game.JobOf(x) == j));
                else
                    add("A – Z", CrtUi.Dim, plain.OrderBy(x => PracticePlan.NameOf(x)));
                add("BOSSES  ·  THEY BRING THEIR USUAL ADDS", CrtStyle.Gold, bosses.OrderBy(x => sort == 2 ? PracticePlan.NameOf(x) : ""));
            }
            return list;
        }
    }
}
