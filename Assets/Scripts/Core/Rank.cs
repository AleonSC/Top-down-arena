using System;
using System.Collections.Generic;

namespace Arena
{
    /// What a cosmetic changes. Titles aren't here: they come with the tier (Rank.TierName).
    public enum CosmeticKind : byte { Frame, Trail, Background }

    /// A cosmetic reward for reaching a rank. Id is stored in the profile: never change one once players own it.
    public class CosmeticDef
    {
        public string Id, Name;
        public CosmeticKind Kind;
        public int Rank;
    }

    /// One line of the end-of-run XP breakdown.
    public class XpLine
    {
        public string Label, Detail;
        public int Xp;
    }

    /// What a finished run (or hunt) is worth in XP, and how it was worked out (Rank.Award).
    public class XpAward
    {
        public readonly List<XpLine> Lines = new List<XpLine>();
        public readonly List<string> ModNotes = new List<string>();  // "Tough Crowd +25%"
        public int Subtotal, Total;
        public float ModMult = 1f, DiffMult = 1f;
        public string Mods = "";  // active modifier names, '|' between
    }

    /// An award applied to a profile: where its XP and rank went, and what it unlocked.
    public class RankGain
    {
        public XpAward Award;
        public long Before, After;
        public int RankBefore, RankAfter;
        public readonly List<CosmeticDef> Unlocked = new List<CosmeticDef>();
        public bool RankedUp => RankAfter > RankBefore;
    }

    /// The account rank: XP from every finished run raises it, and ranks unlock cosmetics only (a title per tier, and the
    /// frames, trails and backgrounds below). Never power. See Docs/Rank.md. Plain C#, no Unity.
    public static class Rank
    {
        public const int XpPerWave = 20, XpPerBoss = 150, XpPerRevive = 40, XpWin = 500;
        public const float HarderBonus = 0.25f, RiskyBonus = 0.15f, ModCap = 3f;  // modifiers: x(1 + sum of bonuses), at most x3
        public const int MaxRank = 999;

        static readonly float[] DifficultyMult = { 0.75f, 1f, 1.25f, 1.5f, 1f };  // Easy, Normal, Hard, Nightmare, Custom

        /// XP to get from this rank to the next.
        public static int NeedFor(int rank) => 200 * Math.Max(1, rank);

        public struct Info
        {
            public int Rank;
            public long Into;   // XP earned towards the next rank
            public int Need;    // XP the next rank costs
            public float Fraction => Need > 0 ? Math.Min(1f, (float)Into / Need) : 0f;
        }

        public static Info Of(long xp)
        {
            int r = 1;
            xp = Math.Max(0, xp);
            while (r < MaxRank && xp >= NeedFor(r))
            {
                xp -= NeedFor(r);
                r++;
            }
            return new Info { Rank = r, Into = xp, Need = NeedFor(r) };
        }

        /// The total XP at which a rank starts.
        public static long XpAt(int rank)
        {
            long xp = 0;
            for (int r = 1; r < rank; r++)
                xp += NeedFor(r);
            return xp;
        }

        // --- Tiers: a title, a colour and a number of chevrons each ----------------------------------

        public static int Tier(int rank) => rank >= 30 ? 4 : rank >= 20 ? 3 : rank >= 10 ? 2 : rank >= 5 ? 1 : 0;
        public static readonly string[] TierNames = { "Recruit", "Challenger", "Veteran", "Champion", "Legend" };
        public static string Title(int rank) => TierNames[Tier(rank)];
        public static readonly Rgba[] TierColors =
        {
            new Rgba(0.54f, 0.58f, 0.69f), new Rgba(0.35f, 0.59f, 1f), new Rgba(0.31f, 0.82f, 0.71f), new Rgba(0.71f, 0.45f, 1f), new Rgba(1f, 0.8f, 0.35f),
        };
        public static Rgba TierColor(int rank) => TierColors[Tier(rank)];

        // --- Cosmetics ---------------------------------------------------------------------------------

        public static readonly CosmeticDef[] Cosmetics =
        {
            new CosmeticDef { Id = "trail-ember", Name = "Ember trail", Kind = CosmeticKind.Trail, Rank = 8 },
            new CosmeticDef { Id = "frame-steel", Name = "Steel frame", Kind = CosmeticKind.Frame, Rank = 12 },
            new CosmeticDef { Id = "bg-dusk", Name = "Dusk grid", Kind = CosmeticKind.Background, Rank = 15 },
            new CosmeticDef { Id = "trail-gold", Name = "Gold stardust", Kind = CosmeticKind.Trail, Rank = 25 },
            new CosmeticDef { Id = "frame-gold", Name = "Gold frame", Kind = CosmeticKind.Frame, Rank = 30 },
        };

        public static CosmeticDef Cosmetic(string id)
        {
            foreach (var c in Cosmetics)
                if (c.Id == id)
                    return c;
            return null;
        }

        public static bool Owns(int rank, string id)
        {
            var c = Cosmetic(id);
            return c != null && rank >= c.Rank;
        }

        public static List<CosmeticDef> OfKind(CosmeticKind kind)
        {
            var l = new List<CosmeticDef>();
            foreach (var c in Cosmetics)
                if (c.Kind == kind)
                    l.Add(c);
            return l;
        }

        /// Rewards a rank brings, for the "you unlocked" lines: its cosmetics, and the tier title if it starts one.
        public static List<CosmeticDef> RewardsAt(int rank)
        {
            var l = new List<CosmeticDef>();
            foreach (var c in Cosmetics)
                if (c.Rank == rank)
                    l.Add(c);
            return l;
        }

        /// A cosmetic's number on the wire and in the lobby: 0 is none, then that kind's cosmetics in table order.
        public static byte IndexOf(CosmeticKind kind, string id)
        {
            byte n = 0;
            foreach (var c in Cosmetics)
                if (c.Kind == kind)
                {
                    n++;
                    if (c.Id == id)
                        return n;
                }
            return 0;
        }

        public static string IdOf(CosmeticKind kind, int index)
        {
            int n = 0;
            foreach (var c in Cosmetics)
                if (c.Kind == kind && ++n == index)
                    return c.Id;
            return "";
        }

        public static byte FrameIndex(string id) => IndexOf(CosmeticKind.Frame, id);
        public static string FrameId(int index) => IdOf(CosmeticKind.Frame, index);
        public static byte TrailIndex(string id) => IndexOf(CosmeticKind.Trail, id);
        public static string TrailId(int index) => IdOf(CosmeticKind.Trail, index);

        /// The frame colour to draw for a lobby entry's frame number (steel grey, gold).
        public static Rgba FrameColor(int index)
        {
            string id = FrameId(index);
            return id == "frame-gold" ? new Rgba(1f, 0.8f, 0.35f) : new Rgba(0.72f, 0.78f, 0.9f);
        }

        // --- XP for a finished run ------------------------------------------------------------------

        /// The XP a run or hunt is worth: 20 a wave cleared, 150 a boss, 40 a revive and 500 for a win, times
        /// (1 + each modifier's bonus: Harder +25%, Risky +15%, Easier none; at most x3) and the difficulty's step.
        public static XpAward Award(bool won, int wavesCleared, int bosses, int revives, RunMod mods, DifficultyLevel diff)
        {
            var a = new XpAward();
            wavesCleared = Math.Max(0, wavesCleared);
            bosses = Math.Max(0, bosses);
            revives = Math.Max(0, revives);
            Add(a, "Waves cleared", wavesCleared + " x " + XpPerWave, wavesCleared * XpPerWave);
            if (bosses > 0)
                Add(a, "Bosses defeated", bosses + " x " + XpPerBoss, bosses * XpPerBoss);
            if (revives > 0)
                Add(a, "Allies revived", revives + " x " + XpPerRevive, revives * XpPerRevive);
            if (won)
                Add(a, "Victory", "", XpWin);
            foreach (var l in a.Lines)
                a.Subtotal += l.Xp;

            float bonus = 0f;
            var names = new List<string>();
            foreach (var d in Modifiers.All)
            {
                if ((mods & d.Mod) == 0)
                    continue;
                names.Add(d.Name);
                float b = d.Lean == ModLean.Harder ? HarderBonus : d.Lean == ModLean.Risky ? RiskyBonus : 0f;
                if (b > 0f)
                    a.ModNotes.Add(d.Name + " +" + (int)Math.Round(b * 100f) + "%");
                bonus += b;
            }
            a.Mods = string.Join("|", names.ToArray());
            a.ModMult = Math.Min(ModCap, 1f + bonus);
            a.DiffMult = DifficultyMult[Math.Max(0, Math.Min(DifficultyMult.Length - 1, (int)diff))];
            a.Total = (int)Math.Round(a.Subtotal * a.ModMult * a.DiffMult);
            return a;
        }

        static void Add(XpAward a, string label, string detail, int xp) => a.Lines.Add(new XpLine { Label = label, Detail = detail, Xp = xp });

        /// How many bosses a run of this many cleared waves has beaten (one every fifth wave).
        public static int BossesIn(int wavesCleared) => Math.Max(0, wavesCleared) / 5;
    }
}
