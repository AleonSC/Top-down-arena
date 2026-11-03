using System;
using System.Collections.Generic;

namespace Arena
{
    /// One enemy of a boss's faction: how often it's picked, and the first wave it can come in.
    public class FactionMember
    {
        public EnemyType Type;
        public float Weight = 1f;
        public int From = 1;
    }

    /// A boss's world: the enemies of its approach, and squads of them (which count toward the themed share).
    public class FactionDef
    {
        public EnemyType Boss;
        public FactionMember[] Members;
        public SquadDef[] Squads = new SquadDef[0];
        public SquadDef Finale;  // always in the last wave before the boss (when the approach is on), counting toward its share
    }

    /// Boss approaches (Docs/Approaches.md): the four regular waves before each boss take part of their enemies from
    /// that boss's faction. How much is the difficulty's Boss approach (ApproachShare: the share of the last wave before
    /// the boss) and Approach ramp (on: the four waves rise to it, a quarter at a time; off: all four use it). At 0% the
    /// waves are built exactly as before. Waves are built on the host; every game works out the share from the wave and
    /// the difficulty, so the HUD needs nothing extra on the wire.
    public static class Approaches
    {
        public static readonly FactionDef[] All =
        {
            new FactionDef
            {
                Boss = EnemyType.Boss,  // the Warden: prisoners, guards and riot gear (Warden.cs, WardenFaction.cs)
                Members = new[]
                {
                    new FactionMember { Type = EnemyType.Rioter, Weight = 3f },
                    new FactionMember { Type = EnemyType.Guard, Weight = 2f },
                    new FactionMember { Type = EnemyType.RiotShield, Weight = 2f, From = 2 },
                    new FactionMember { Type = EnemyType.GasGrenadier, Weight = 1.5f, From = 2 },
                },
                Squads = new[]
                {
                    new SquadDef { Name = "Breakout", From = 1, Members = new[] { EnemyType.Guard, EnemyType.Rioter, EnemyType.Rioter, EnemyType.Rioter, EnemyType.Rioter, EnemyType.Rioter, EnemyType.Rioter } },
                    new SquadDef { Name = "Cell block", From = 3, Members = new[] { EnemyType.RiotShield, EnemyType.RiotShield, EnemyType.Rioter, EnemyType.Rioter, EnemyType.Rioter, EnemyType.Rioter } },
                },
                // Wave 4: a wall of four Riot Shields marching at you, five Guards close behind it (WardenFaction.cs).
                Finale = new SquadDef { Name = "Riot line", From = 4, Form = SquadForm.RiotLine, Members = new[] {
                    EnemyType.RiotShield, EnemyType.RiotShield, EnemyType.RiotShield, EnemyType.RiotShield,
                    EnemyType.Guard, EnemyType.Guard, EnemyType.Guard, EnemyType.Guard, EnemyType.Guard } },
            },
        };

        /// The faction of a boss, or null if it has none yet.
        public static FactionDef For(EnemyType boss) => Array.Find(All, f => f.Boss == boss);

        /// Is this type one of a faction's (for the Bestiary and the Practice Range)?
        public static FactionDef Of(EnemyType t) => Array.Find(All, f => Array.Exists(f.Members, m => m.Type == t));
    }

    public partial class Game
    {
        /// The boss this wave leads up to (the next boss wave's), whether or not it has a faction.
        public static EnemyType ApproachBoss(int wave) => BossForWave((wave + 4) / 5 * 5);

        /// Which of the four approach waves this is: 1 (furthest from the boss) to 4 (just before it); 0 on a boss wave.
        public static int ApproachStep(int wave) => wave % 5;

        /// The themed share of this wave (0 to 1): 0 on boss waves, for bosses without a faction, or when it's turned off.
        public float ApproachShare(int wave)
        {
            int step = ApproachStepAt(wave);
            float share = D(DiffKnob.ApproachShare);
            if (step == 0 || Approaches.For(ApproachBossAt(wave)) == null || Practice != null)
                return 0f;
            if (Hunt != null)
                return 1f;  // a Boss Hunt's build-up waves are only its boss's own enemies (Hunts.cs)
            if (share <= 0f)
                return 0f;
            return D(DiffKnob.ApproachRamp) >= 0.5f ? share * RampSteps[step - 1] : share;
        }

        /// With the ramp on, each approach wave's share of the full share: from 40% up to all of it.
        static readonly float[] RampSteps = { 0.4f, 0.6f, 0.8f, 1f };

        readonly List<EnemyType> approachSingles = new List<EnemyType>();
        readonly List<SquadDef> approachSquads = new List<SquadDef>();

        /// Host, BuildWave: takes the themed share off `count` and picks the faction's enemies for it (squads where they
        /// fit, singles for the rest). They're mixed into the wave by MixApproach once the rest is built. Uses no random
        /// rolls when the share is 0, so those waves come out exactly as before.
        void BuildApproach(int n, ref int count)
        {
            approachSingles.Clear();
            approachSquads.Clear();
            float share = ApproachShare(n);
            if (share <= 0f)
                return;
            var f = Approaches.For(ApproachBossAt(n));
            int of = OnPath && CurrentPath == PathId.EliteGauntlet ? count / 2 : count;  // Elite Gauntlet halves the whole wave
            int themed = Math.Min(count, (int)Math.Round(of * share));
            if (themed <= 0)
                return;
            int reach = Hunt != null ? Hunts.Step(n) : n;  // (a hunt's nine waves reach through the faction as a run's four do)
            var members = Array.FindAll(f.Members, m => m.From <= reach);
            var squads = Array.FindAll(f.Squads, s => s.From <= reach);
            int spent = 0;
            if (f.Finale != null && (Hunt != null ? Hunts.FinaleWave(n) : ApproachStep(n) == 4) && f.Finale.From <= reach)
            {
                themed = Math.Max(themed, Math.Min(count, f.Finale.Members.Length));  // the set piece comes whatever the share
                approachSquads.Add(f.Finale);
                spent = f.Finale.Members.Length;
            }
            count -= themed;
            float total = 0f;
            foreach (var m in members)
                total += m.Weight;
            while (spent < themed)
            {
                // About half of it as squads, if one fits in what's left.
                var fits = Array.FindAll(squads, s => s.Members.Length <= themed - spent);
                if (fits.Length > 0 && rng.NextDouble() < 0.5)
                {
                    var s = fits[rng.Next(fits.Length)];
                    approachSquads.Add(s);
                    spent += s.Members.Length;
                    continue;
                }
                float r = (float)rng.NextDouble() * total;
                var pick = members[members.Length - 1];
                foreach (var m in members)
                {
                    r -= m.Weight;
                    if (r <= 0f)
                    {
                        pick = m;
                        break;
                    }
                }
                approachSingles.Add(pick.Type);
                if (pick.Type == EnemyType.Rioter)
                    approachSingles.Add(EnemyType.Rioter);  // rioters come in pairs
                spent += pick.Type == EnemyType.Rioter ? 2 : 1;
            }
        }

        /// Mixes the approach's enemies in with the rest of the wave: singles at random places in the spawn queue,
        /// squads among the others.
        void MixApproach()
        {
            foreach (var t in approachSingles)
                spawnQueue.Insert(rng.Next(spawnQueue.Count + 1), t);
            foreach (var s in approachSquads)
                squadQueue.Insert(rng.Next(squadQueue.Count + 1), s);
            approachSingles.Clear();
            approachSquads.Clear();
        }
    }
}
