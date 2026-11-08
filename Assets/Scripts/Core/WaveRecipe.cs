using System;
using System.Collections.Generic;

namespace Arena
{
    /// What an enemy is for in a wave (Docs/EnemyRoles.md): each asks for a different answer.
    public enum EnemyJob : byte
    {
        Pressure,   // shoots: weave, use cover
        Hunter,     // comes for you: move, kite, sidestep a telegraph
        Zoner,      // takes ground: pick your spot
        Disruptor,  // breaks your position: react now
        Anchor,     // holds the others up: kill it first
    }

    /// The late wave recipe (Docs/EnemyRoles.md, section 1). From wave RecipeFrom a regular wave is built to job shares
    /// instead of a flat draw from every type unlocked so far (which made waves 14-29 81-91% shooters, with the wave-1
    /// Grunt the most common enemy to the end):
    ///   Shares: Pressure 45%, Hunter 25%, Zoner 12%, Disruptor 8%, Anchor 10% of the wave, squads included. A job with
    ///     nothing unlocked yet gives its share away (Disruptor to Hunter, the rest to Pressure).
    ///   Squads take RecipeSquadShare of the wave (not 70%), a melee squad as often as the Hunter share; the single enemies
    ///     then fill what each job is still short of.
    ///   Headliners: each wave picks 2 featured types (3 from wave 25), the newer the likelier; a job with a headliner
    ///     draws it for HeadlinerPick of its singles, so a wave has an identity.
    ///   Retiring: a type's weight fades over the 10 waves after it has been around 10 waves (to 15%), and a type in its
    ///     first 4 waves comes more often, so the wave-1 Grunt gives way to what came after it.
    /// Waves before RecipeFrom, boss waves, paths with their own mix, Zombie Infection and designed waves are as before.
    public partial class Game
    {
        public const int RecipeFrom = 13;
        public const float RecipeSquadShare = 0.5f;
        public static readonly float[] JobShares = { 0.45f, 0.25f, 0.12f, 0.08f, 0.10f };
        public const float HeadlinerPick = 0.6f;
        /// A recipe wave shows at most this many distinct enemy types (squad members included), 8 from wave RecipeWideFrom,
        /// so each kind on screen can be learned (Docs/EnemyReadability.md section 3). Once reached, the rest of the wave
        /// repeats types it already has.
        public const int RecipeKinds = 6, RecipeKindsWide = 8, RecipeWideFrom = 25;
        public static int KindCap(int n) => n >= RecipeWideFrom ? RecipeKindsWide : RecipeKinds;  // a job with a headliner draws it this often (if it isn't capped)

        /// The types the recipe draws from, the wave each joins, and its job.
        static readonly (EnemyType Type, int From, EnemyJob Job)[] RecipeTypes =
        {
            (EnemyType.Grunt, 1, EnemyJob.Pressure), (EnemyType.Burster, 2, EnemyJob.Pressure), (EnemyType.Sniper, 3, EnemyJob.Pressure),
            (EnemyType.Spinner, 4, EnemyJob.Pressure), (EnemyType.Splitter, 7, EnemyJob.Pressure), (EnemyType.Mortar, 8, EnemyJob.Pressure),
            (EnemyType.Shielder, 9, EnemyJob.Pressure), (EnemyType.Pauser, 14, EnemyJob.Pressure), (EnemyType.Phantom, 16, EnemyJob.Pressure),
            (EnemyType.Leader, 19, EnemyJob.Pressure), (EnemyType.Hexer, 21, EnemyJob.Pressure), (EnemyType.Stitcher, 22, EnemyJob.Pressure),
            (EnemyType.Juggernaut, 26, EnemyJob.Pressure),
            (EnemyType.Swarmer, 1, EnemyJob.Hunter), (EnemyType.Spiker, 2, EnemyJob.Hunter), (EnemyType.Brute, 3, EnemyJob.Hunter),
            (EnemyType.Lunger, 5, EnemyJob.Hunter), (EnemyType.Buzzsaw, 5, EnemyJob.Hunter), (EnemyType.SpikeBlock, 5, EnemyJob.Hunter),
            (EnemyType.Charger, 6, EnemyJob.Hunter), (EnemyType.FuseRunner, 15, EnemyJob.Hunter), (EnemyType.Reaver, 28, EnemyJob.Hunter),
            (EnemyType.BeamSentry, 18, EnemyJob.Zoner), (EnemyType.Sapper, 18, EnemyJob.Zoner), (EnemyType.Scorcher, 24, EnemyJob.Zoner),
            (EnemyType.Hookman, 17, EnemyJob.Disruptor),
            (EnemyType.Medic, 11, EnemyJob.Anchor), (EnemyType.Nest, 31, EnemyJob.Anchor), (EnemyType.Warder, 36, EnemyJob.Anchor),
        };

        /// At most this many of a type from the recipe's singles in one wave (squads can add more).
        static int RecipeCap(EnemyType t) =>
            t == EnemyType.Hookman || t == EnemyType.Sapper ? 3 : t == EnemyType.Medic || t == EnemyType.Warder || t == EnemyType.Nest ? 2 : 99;

        /// How many one pick of a type brings (packs).
        static int PackOf(EnemyType t) => t == EnemyType.Swarmer ? 4 : t == EnemyType.Spiker || t == EnemyType.FuseRunner ? 2 : 1;

        public static EnemyJob JobOf(EnemyType t)
        {
            foreach (var r in RecipeTypes)
                if (r.Type == t)
                    return r.Job;
            var d = Defs.Get(t);
            if (t == EnemyType.HiveEgg || t == EnemyType.SapperMine)
                return EnemyJob.Zoner;
            return d.Melee ? EnemyJob.Hunter : EnemyJob.Pressure;  // the rest (boss adds, factions, zombies) by whether they shoot
        }

        /// How likely a type is on wave n (before headliners): fresh ones more, old ones fading to 15%.
        public static float RecipeWeight(int from, int n)
        {
            int age = n - from;
            if (age < 0) return 0f;
            if (age < 4) return 1.6f;
            if (age <= 10) return 1f;
            return Math.Max(0.15f, 1f - (age - 10) / 10f);
        }

        /// The job shares for wave n, with any job that has nothing unlocked handing its share on.
        public static float[] RecipeShares(int n)
        {
            var shares = (float[])JobShares.Clone();
            for (int j = 2; j < shares.Length; j++)
            {
                bool any = false;
                foreach (var r in RecipeTypes)
                    any |= (int)r.Job == j && r.From <= n;
                if (any)
                    continue;
                shares[j == (int)EnemyJob.Disruptor ? (int)EnemyJob.Hunter : (int)EnemyJob.Pressure] += shares[j];
                shares[j] = 0f;
            }
            return shares;
        }

        /// Host: the wave's headliners (BuildWave on a recipe wave; kept for the log and tests).
        public readonly List<EnemyType> Headliners = new List<EnemyType>();

        void PickHeadliners(int n)
        {
            Headliners.Clear();
            int want = n >= 25 ? 3 : 2;
            for (int k = 0; k < want; k++)
            {
                float total = 0f;
                foreach (var r in RecipeTypes)
                    if (r.Job != EnemyJob.Anchor && !Headliners.Contains(r.Type))
                        total += RecipeWeight(r.From, n) * (n - r.From < 8 ? 2f : 1f);  // newer ones headline more
                float roll = (float)rng.NextDouble() * total;
                foreach (var r in RecipeTypes)
                {
                    if (r.Job == EnemyJob.Anchor || Headliners.Contains(r.Type))
                        continue;
                    float w = RecipeWeight(r.From, n) * (n - r.From < 8 ? 2f : 1f);
                    if (w <= 0f)
                        continue;
                    roll -= w;
                    if (roll <= 0f)
                    {
                        Headliners.Add(r.Type);
                        break;
                    }
                }
            }
        }

        /// Host: a recipe wave's single enemies, after its squads (squadQueue) took their part. count is the whole wave.
        void BuildRecipeSingles(int n, int count, int squadSpent)
        {
            var shares = RecipeShares(n);
            var have = new int[shares.Length];
            foreach (var sq in squadQueue)
                foreach (var t in sq.Members)
                    have[(int)JobOf(t)]++;
            int left = count - squadSpent;
            var need = new float[shares.Length];
            float sum = 0f;
            for (int j = 0; j < shares.Length; j++)
            {
                need[j] = Math.Max(0f, count * shares[j] - have[j]);
                sum += need[j];
            }
            if (sum <= 0f)
                return;
            var taken = new Dictionary<EnemyType, int>();
            kinds.Clear();
            foreach (var h in Headliners)
                kinds.Add(h);  // headliners are always on the bill, so they count first
            foreach (var sq in squadQueue)
                foreach (var t in sq.Members)
                    kinds.Add(t);
            kindCap = KindCap(n);
            for (int j = 0; j < shares.Length; j++)
            {
                int quota = (int)Math.Round(need[j] / sum * left);
                while (quota > 0)
                {
                    var pick = DrawRecipeType((EnemyJob)j, n, taken) ?? DrawRecipeType(EnemyJob.Pressure, n, taken) ?? DrawRecipeType(EnemyJob.Hunter, n, taken);  // capped out: a shooter instead
                    if (pick == null)
                        break;
                    var t = pick.Value;
                    kinds.Add(t);
                    int pack = PackOf(t);
                    for (int k = 0; k < pack; k++)
                        spawnQueue.Add(t);
                    taken[t] = (taken.TryGetValue(t, out int c) ? c : 0) + 1;
                    quota -= pack;
                }
            }
        }

        // The distinct types this recipe wave has so far; once kindCap is reached only these can be drawn again.
        readonly HashSet<EnemyType> kinds = new HashSet<EnemyType>();
        int kindCap = 99;
        bool KindOk(EnemyType t) => kinds.Count < kindCap || kinds.Contains(t);

        EnemyType? DrawRecipeType(EnemyJob job, int n, Dictionary<EnemyType, int> taken)
        {
            if (rng.NextDouble() < HeadlinerPick)
            {
                var heads = Headliners.FindAll(h => JobOf(h) == job && !(taken.TryGetValue(h, out int hc) && hc >= RecipeCap(h)) && KindOk(h));
                if (heads.Count > 0)
                    return heads[rng.Next(heads.Count)];
            }
            float total = 0f;
            foreach (var r in RecipeTypes)
                if (r.Job == job)
                    total += SingleWeight(r, n, taken);
            if (total <= 0f)
                return null;
            float roll = (float)rng.NextDouble() * total;
            foreach (var r in RecipeTypes)
            {
                if (r.Job != job)
                    continue;
                float w = SingleWeight(r, n, taken);
                if (w <= 0f)
                    continue;
                roll -= w;
                if (roll <= 0f)
                    return r.Type;
            }
            return null;
        }

        float SingleWeight((EnemyType Type, int From, EnemyJob Job) r, int n, Dictionary<EnemyType, int> taken)
        {
            if (taken.TryGetValue(r.Type, out int c) && c >= RecipeCap(r.Type))
                return 0f;
            if (!KindOk(r.Type))
                return 0f;
            return RecipeWeight(r.From, n);
        }
    }
}
