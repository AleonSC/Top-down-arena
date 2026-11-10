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
                    new FactionMember { Type = EnemyType.TaserGuard, Weight = 1.5f, From = 3 },  // the faction's shooter that stuns (WardenFaction.cs)
                    new FactionMember { Type = EnemyType.SecurityGuard, Weight = 2f, From = 2 },  // its pistol guard: leading tracer rounds, a magazine of seven (SecurityGuard.cs)
                },
                Squads = new[]
                {
                    new SquadDef { Name = "Breakout", From = 1, Members = new[] { EnemyType.Guard, EnemyType.Rioter, EnemyType.Rioter, EnemyType.Rioter, EnemyType.Rioter, EnemyType.Rioter, EnemyType.Rioter } },
                    new SquadDef { Name = "Patrol", From = 2, Members = new[] { EnemyType.SecurityGuard, EnemyType.SecurityGuard, EnemyType.SecurityGuard, EnemyType.Rioter, EnemyType.Rioter } },
                    new SquadDef { Name = "Cell block", From = 3, Members = new[] { EnemyType.RiotShield, EnemyType.RiotShield, EnemyType.Rioter, EnemyType.Rioter, EnemyType.Rioter, EnemyType.Rioter } },
                },
                // Wave 4: a wall of four Riot Shields marching at you, five Guards close behind it (WardenFaction.cs).
                Finale = new SquadDef { Name = "Riot line", From = 4, Form = SquadForm.RiotLine, Members = new[] {
                    EnemyType.RiotShield, EnemyType.RiotShield, EnemyType.RiotShield, EnemyType.RiotShield,
                    EnemyType.Guard, EnemyType.Guard, EnemyType.Guard, EnemyType.Guard, EnemyType.Guard } },
            },
            new FactionDef
            {
                Boss = EnemyType.Lich,  // the Lich: the restless dead, the same zombies as Zombie Infection (Zombies.cs); they don't get back up outside that modifier
                Members = new[]
                {
                    new FactionMember { Type = EnemyType.Shambler, Weight = 4f },
                    new FactionMember { Type = EnemyType.Runner, Weight = 1.5f, From = 2 },  // (a pick is a pack of three)
                    new FactionMember { Type = EnemyType.Spitter, Weight = 1.5f, From = 2 },
                    new FactionMember { Type = EnemyType.Crawler, Weight = 1f, From = 3 },
                    new FactionMember { Type = EnemyType.Mender, Weight = 0.7f, From = 4 },
                },
                Squads = new[]
                {
                    new SquadDef { Name = "Graveyard shift", From = 1, Members = new[] { EnemyType.Shambler, EnemyType.Shambler, EnemyType.Shambler, EnemyType.Shambler, EnemyType.Shambler, EnemyType.Shambler } },
                    new SquadDef { Name = "Grave rush", From = 2, Members = new[] { EnemyType.Runner, EnemyType.Runner, EnemyType.Runner, EnemyType.Runner, EnemyType.Runner, EnemyType.Shambler, EnemyType.Shambler } },
                },
                // Wave 4: a shambling wall with Menders to keep it standing, Runners on its flanks.
                Finale = new SquadDef { Name = "Dead march", From = 4, Members = new[] {
                    EnemyType.Shambler, EnemyType.Shambler, EnemyType.Shambler, EnemyType.Shambler, EnemyType.Shambler, EnemyType.Shambler,
                    EnemyType.Mender, EnemyType.Mender, EnemyType.Runner, EnemyType.Runner, EnemyType.Runner } },
            },
        };

        /// The faction of a boss, or null if it has none yet.
        public static FactionDef For(EnemyType boss) => Array.Find(All, f => f.Boss == boss);

        /// The fading boss's share of the five waves of a cycle (Docs/Campaign.md), and the older pool's from wave 11 on.
        public static readonly float[] FadeShare = { 0.35f, 0.25f, 0.15f, 0.05f, 0f };
        public const float PoolShare = 0.10f;

        /// One faction's slice of a campaign wave.
        public struct Slice
        {
            public FactionDef Faction;
            public float Share;
            public bool Current;  // the faction of the boss this wave leads to
            public float Pool;    // the part of Share that is the older pool (a comeback squad takes it over, Comeback)
        }

        /// The campaign's split of regular wave `n` between factions (Docs/Campaign.md): the last boss's faction fading
        /// out, the older bosses' as a pool, the next boss's taking the rest. Only factions that are built appear, so
        /// the shares may add up to less than 1 (the rest is the usual random mix). Depends on the wave alone.
        public static List<Slice> Campaign(int n)
        {
            var list = new List<Slice>();
            if (n < 1)
                return list;
            int cycle = (n - 1) / 5, p = (n - 1) % 5;
            float fade = cycle >= 1 ? FadeShare[p] : 0f;
            float pool = cycle >= 2 ? PoolShare : 0f;
            Action<EnemyType, float, bool, float> add = (boss, share, cur, poolPart) =>
            {
                var f = For(boss);
                if (f == null || share <= 0f)
                    return;
                int i = list.FindIndex(x => x.Faction == f);
                if (i >= 0)
                    list[i] = new Slice { Faction = f, Share = list[i].Share + share, Current = list[i].Current || cur, Pool = list[i].Pool + poolPart };
                else
                    list.Add(new Slice { Faction = f, Share = share, Current = cur, Pool = poolPart });
            };
            add(Game.BossForWave(5 * (cycle + 1)), 1f - fade - pool, true, 0f);
            if (cycle >= 1)
                add(Game.BossForWave(5 * cycle), fade, false, 0f);
            var older = new List<EnemyType>();
            for (int j = 1; j < cycle; j++)
                if (For(Game.BossForWave(5 * j)) != null)
                    older.Add(Game.BossForWave(5 * j));
            foreach (var b in older)
                add(b, pool / older.Count, false, pool / older.Count);
            return list;
        }

        /// The comeback squad of wave `n` (Docs/Campaign.md), or null: one wave in every five from wave 11 (where the older
        /// pool starts), never the last wave of a cycle (the approach stays clean). One squad from an earlier built
        /// faction, picked from the wave number alone so every game agrees. It takes over the wave's older-pool slice.
        public static SquadDef Comeback(int n, out FactionDef from)
        {
            from = null;
            int cycle = (n - 1) / 5, p = (n - 1) % 5;
            if (n < 11 || p != (cycle * 3 + 1) % 4)  // one wave in every five, never the last of the cycle, its step moving about
                return null;
            uint h = (uint)n * 2654435761u;
            var pool = Campaign(n).FindAll(x => x.Pool > 0f);
            pool.RemoveAll(x => x.Faction.Squads.Length == 0);
            if (pool.Count == 0)
                return null;
            from = pool[(int)((h >> 12) % (uint)pool.Count)].Faction;
            return from.Squads[(int)((h >> 16) % (uint)from.Squads.Length)];
        }

        /// Is this type one of a faction's (for the Bestiary and the Practice Range)?
        public static FactionDef Of(EnemyType t) => Array.Find(All, f => Array.Exists(f.Members, m => m.Type == t));
    }

    public partial class Game
    {
        /// XP for killing a faction enemy pays like a regular enemy of its health (Defs: Rioter 7, Guard 15, Riot Shield 10,
        /// Gas Grenadier 9), so a themed approach wave, much tougher than a regular one, doesn't leave you levels behind
        /// at the boss. The Warden's own riot on its boss wave sends dozens of Rioters and Guards, so those keep the old
        /// fodder XP there (RioterFodderXp, GuardFodderXp).
        public const float RioterFodderXp = 2f, GuardFodderXp = 4f;

        /// The XP a kill gives everyone, before elites, paths and modifiers (Combat.cs).
        float KillXp(Enemy e)
        {
            if (BossNow && e.Type == EnemyType.Rioter)
                return RioterFodderXp;
            if (BossNow && e.Type == EnemyType.Guard)
                return GuardFodderXp;
            return e.Def.Xp;
        }

        /// The boss this wave leads up to (the next boss wave's), whether or not it has a faction.
        public static EnemyType ApproachBoss(int wave) => BossForWave((wave + 4) / 5 * 5);

        /// Which of the four approach steps this regular wave is: 1 (furthest from the boss) to 4 (the fourth and fifth waves
        /// of a cycle are both 4, the fifth brings the set piece); 0 before wave 1.
        public static int ApproachStep(int wave) => wave <= 0 ? 0 : Math.Min(4, (wave - 1) % 5 + 1);

        /// The themed share of this wave (0 to 1): 0 on boss waves, for bosses without a faction, or when it's turned off.
        public float ApproachShare(int wave)
        {
            if (Hunt == null && BossSlot && wave == Wave)
                return 0f;  // the boss fight itself has no approach
            int step = ApproachStepAt(wave);
            float share = D(DiffKnob.ApproachShare);
            if (step == 0 || Approaches.For(ApproachBossAt(wave)) == null || Practice != null)
                return 0f;
            if (Hunt != null)
                return 1f;  // a Boss Hunt's build-up waves are only its boss's own enemies (Hunts.cs)
            if (CampaignOn)
                return Approaches.Campaign(wave).Find(x => x.Current).Share;  // the next boss's slice (0 if it has no faction)
            if (share <= 0f)
                return 0f;
            return D(DiffKnob.ApproachRamp) >= 0.5f ? share * RampSteps[step - 1] : share;
        }

        /// Is the campaign buildup on (every wave themed, Approaches.Campaign)? Not in a hunt or the Practice Range.
        public bool CampaignOn => Hunt == null && Practice == null && D(DiffKnob.Campaign) >= 0.5f;

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
            int of = OnPath && CurrentPath == PathId.EliteGauntlet ? count / 2 : count;  // Elite Gauntlet halves the whole wave
            if (CampaignOn)
            {
                if (ApproachStepAt(n) == 0)
                    return;
                int step = ApproachStep(n);
                FactionDef cbFrom;
                var cb = Approaches.Comeback(n, out cbFrom);  // (a comeback squad takes the older pool's slice this wave)
                foreach (var sl in Approaches.Campaign(n))  // (every faction's enemies, each by its slice; the rest stays the usual mix)
                {
                    int themed = Math.Min(count, (int)Math.Round(of * (cb != null ? sl.Share - sl.Pool : sl.Share)));
                    if (themed > 0)
                        PickFaction(sl.Faction, sl.Current ? step : 99, sl.Current && (n - 1) % 5 == 4, ref count, themed);  // (older factions are in full; only the next boss's builds up)
                }
                if (cb != null)
                {
                    approachSquads.Add(cb);
                    count -= Math.Min(count, cb.Members.Length);
                }
                return;
            }
            float share = ApproachShare(n);
            if (share <= 0f)
                return;
            var f = Approaches.For(ApproachBossAt(n));
            int themed1 = Math.Min(count, (int)Math.Round(of * share));
            if (themed1 <= 0)
                return;
            bool hunt = Hunt != null;  // (a hunt has the whole faction from wave 1 and a squad or two in every wave, Hunts.cs)
            int reach = hunt ? Hunts.Reach : ApproachStep(n);
            bool finale = hunt ? Hunts.FinaleWave(n) : (n - 1) % 5 == 4;
            PickFaction(f, reach, finale, ref count, themed1, hunt && !finale ? Hunts.SquadsPerWave(n) : 0);
        }

        /// Takes `themed` enemies off `count` and picks them from a faction: squads where they fit, singles for the rest.
        /// `reach` is how far into the faction's members and squads the wave goes (their From); the finale comes in when
        /// asked for, whatever the share.
        void PickFaction(FactionDef f, int reach, bool withFinale, ref int count, int themed, int forcedSquads = 0)
        {
            var members = Array.FindAll(f.Members, m => m.From <= reach);
            var squads = Array.FindAll(f.Squads, s => s.From <= reach);
            int spent = 0;
            if (withFinale && f.Finale != null && f.Finale.From <= reach)
            {
                themed = Math.Max(themed, Math.Min(count, f.Finale.Members.Length));  // the set piece comes whatever the share
                approachSquads.Add(f.Finale);
                spent = f.Finale.Members.Length;
            }
            for (int k = 0; k < forcedSquads && squads.Length > 0; k++)  // a hunt: squads whatever the share
            {
                var fs = squads[rng.Next(squads.Length)];
                approachSquads.Add(fs);
                spent += fs.Members.Length;
            }
            if (forcedSquads > 0)
                themed = Math.Max(themed, Math.Min(count, spent));
            count -= themed;
            float total = 0f;
            foreach (var m in members)
                total += m.Weight;
            while (spent < themed)
            {
                // About half of it as squads, if one fits in what's left.
                var fits = Array.FindAll(squads, s => s.Members.Length <= themed - spent && approachSquads.FindAll(a => a == s).Count < 2);  // (none more than twice)
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
                int pack = pick.Type == EnemyType.Rioter ? 2 : pick.Type == EnemyType.Runner ? 3 : 1;  // rioters come in pairs, runners in threes
                for (int k = 1; k < pack; k++)
                    approachSingles.Add(pick.Type);
                spent += pack;
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
