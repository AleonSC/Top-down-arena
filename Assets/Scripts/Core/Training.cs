using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Arena
{
    /// The Training section of the Practice Range (Docs/Mockups/crt-practice-redesign.html): short drills, one mechanic each.
    /// A drill is a small PracticePlan with a name, a group, a goal and a few things to do. This is the groundwork: the library,
    /// the text format and the lobby screen (Game/TrainingUi.cs). Pick a drill in the lobby and its plan goes into the Practice
    /// setup, exactly as if the host had built it, so it needs no new network messages; drills with their own rules come later.
    public class TrainingDrill
    {
        public string Id = "", Name = "", Group = "", Goal = "";
        public bool Example;  // a placeholder showing the format (marked EXAMPLE on its card)
        public readonly List<string> Steps = new List<string>();
        public PracticePlan Plan = new PracticePlan();

        // --- Text format ---------------------------------------------------------------
        //   # comments
        //   name: Dodge Basics
        //   group: Move & Dodge            (one of TrainingLibrary.Groups; anything else lands in the last group)
        //   goal: Slip through three bullet patterns.
        //   step: Clear three patterns without a hit      (as many step lines as you like)
        //   example: yes                   (optional)
        //   loop: no                       (then the waves, exactly as in a Practice setup)
        //   wave 3: Burster x2, Spinner x1
        public static TrainingDrill Parse(string id, string text, out List<string> problems)
        {
            var d = new TrainingDrill { Id = id ?? "" };
            var rest = new List<string>();
            foreach (var raw in (text ?? "").Split('\n'))
            {
                string line = raw.Trim();
                string lower = line.ToLowerInvariant();
                if (line.Length == 0 || line.StartsWith("#"))
                    continue;
                if (lower.StartsWith("name:")) d.Name = line.Substring(5).Trim();
                else if (lower.StartsWith("group:")) d.Group = line.Substring(6).Trim();
                else if (lower.StartsWith("goal:")) d.Goal = line.Substring(5).Trim();
                else if (lower.StartsWith("step:")) d.Steps.Add(line.Substring(5).Trim());
                else if (lower.StartsWith("example:")) d.Example = lower.Substring(8).Trim() == "yes";
                else rest.Add(line);
            }
            d.Plan = PracticePlan.Parse(string.Join("\n", rest), out problems);
            if (d.Name.Length == 0)
                problems.Add("no name");
            if (d.Name.Length == 0)
                d.Name = id ?? "Drill";
            return d;
        }
    }

    /// Every drill the lobby can show. Built-ins are listed in `All`; the front end adds the ones in the player's training folder.
    public static class TrainingLibrary
    {
        public static readonly string[] Groups = { "Move & Dodge", "Weapons", "Abilities", "Enemies", "Co-op" };

        /// Slots shown per group row (empty ones are drawn dashed, as places for drills to come).
        public const int SlotsPerGroup = 4;

        public static readonly List<TrainingDrill> All = new List<TrainingDrill>();

        static TrainingLibrary()
        {
            List<string> problems;
            // A sample drill so the screen has something to show and the format has an example. Delete this line once real drills exist.
            All.Add(TrainingDrill.Parse("dodge-basics",
                "name: Dodge Basics\ngroup: Move & Dodge\ngoal: Slip through three bullet patterns.\nstep: Clear three patterns without a hit\nstep: Dodge through one volley\nexample: yes\nloop: yes\nwave 1: Burster x2, Spinner x1\n",
                out problems));
        }

        /// The group a drill sits in (an unknown group name goes in the last one).
        public static int GroupOf(TrainingDrill d)
        {
            int i = Array.FindIndex(Groups, g => string.Equals(g, d.Group, StringComparison.OrdinalIgnoreCase));
            return i >= 0 ? i : Groups.Length - 1;
        }

        public static List<TrainingDrill> InGroup(int group, IEnumerable<TrainingDrill> extra = null) =>
            All.Concat(extra ?? Enumerable.Empty<TrainingDrill>()).Where(d => GroupOf(d) == group).ToList();
    }
}
