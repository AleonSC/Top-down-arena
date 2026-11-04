using System;
using System.Collections.Generic;

namespace Arena
{
    /// What a cosmetic changes. Titles aren't here: they come with the tier (Rank.TierName).
    public enum CosmeticKind : byte { Frame, Trail, Background, CardBackground, Emote, NameEffect, AimLine }

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
            new CosmeticDef { Id = "frame-circuit", Name = "Circuit frame", Kind = CosmeticKind.Frame, Rank = 16 },
            new CosmeticDef { Id = "trail-frost", Name = "Frost trail", Kind = CosmeticKind.Trail, Rank = 14 },
            new CosmeticDef { Id = "bg-dusk", Name = "Dusk grid", Kind = CosmeticKind.Background, Rank = 15 },
            new CosmeticDef { Id = "frame-ember", Name = "Ember Edge frame", Kind = CosmeticKind.Frame, Rank = 20 },
            new CosmeticDef { Id = "trail-storm", Name = "Storm trail", Kind = CosmeticKind.Trail, Rank = 18 },
            new CosmeticDef { Id = "frame-void", Name = "Void Pulse frame", Kind = CosmeticKind.Frame, Rank = 24 },
            new CosmeticDef { Id = "trail-void", Name = "Void trail", Kind = CosmeticKind.Trail, Rank = 22 },
            new CosmeticDef { Id = "trail-gold", Name = "Gold stardust", Kind = CosmeticKind.Trail, Rank = 25 },
            new CosmeticDef { Id = "frame-gold", Name = "Gold frame", Kind = CosmeticKind.Frame, Rank = 30 },
            new CosmeticDef { Id = "frame-foil", Name = "Legend Foil frame", Kind = CosmeticKind.Frame, Rank = 40 },
            new CosmeticDef { Id = "trail-prism", Name = "Prism trail", Kind = CosmeticKind.Trail, Rank = 35 },
            new CosmeticDef { Id = "trail-lightcycle", Name = "Light Cycle trail", Kind = CosmeticKind.Trail, Rank = 45 },
            new CosmeticDef { Id = "frame-prism", Name = "Prism frame", Kind = CosmeticKind.Frame, Rank = 50 },
            new CosmeticDef { Id = "card-dusk", Name = "Dusk Grid card", Kind = CosmeticKind.CardBackground, Rank = 6 },
            new CosmeticDef { Id = "card-stars", Name = "Starfield card", Kind = CosmeticKind.CardBackground, Rank = 10 },
            new CosmeticDef { Id = "card-blueprint", Name = "Blueprint card", Kind = CosmeticKind.CardBackground, Rank = 13 },
            new CosmeticDef { Id = "card-embers", Name = "Ember Glow card", Kind = CosmeticKind.CardBackground, Rank = 19 },
            new CosmeticDef { Id = "card-aurora", Name = "Aurora card", Kind = CosmeticKind.CardBackground, Rank = 23 },
            new CosmeticDef { Id = "card-circuit", Name = "Circuit Board card", Kind = CosmeticKind.CardBackground, Rank = 27 },
            new CosmeticDef { Id = "card-rain", Name = "Rain on Glass card", Kind = CosmeticKind.CardBackground, Rank = 32 },
            new CosmeticDef { Id = "card-void", Name = "Void Rift card", Kind = CosmeticKind.CardBackground, Rank = 37 },
            new CosmeticDef { Id = "card-foil", Name = "Gilded Foil card", Kind = CosmeticKind.CardBackground, Rank = 42 },
            new CosmeticDef { Id = "card-prism", Name = "Prism Shards card", Kind = CosmeticKind.CardBackground, Rank = 55 },
            new CosmeticDef { Id = "emote-wave", Name = "Wave emote", Kind = CosmeticKind.Emote, Rank = 1 },
            new CosmeticDef { Id = "emote-thumbs", Name = "Thumbs up emote", Kind = CosmeticKind.Emote, Rank = 1 },
            new CosmeticDef { Id = "emote-gg", Name = "GG emote", Kind = CosmeticKind.Emote, Rank = 3 },
            new CosmeticDef { Id = "emote-cheer", Name = "Cheer emote", Kind = CosmeticKind.Emote, Rank = 7 },
            new CosmeticDef { Id = "emote-heart", Name = "Heart emote", Kind = CosmeticKind.Emote, Rank = 10 },
            new CosmeticDef { Id = "emote-laugh", Name = "Laugh emote", Kind = CosmeticKind.Emote, Rank = 12 },
            new CosmeticDef { Id = "emote-question", Name = "Question emote", Kind = CosmeticKind.Emote, Rank = 15 },
            new CosmeticDef { Id = "emote-sleep", Name = "Sleep emote", Kind = CosmeticKind.Emote, Rank = 17 },
            new CosmeticDef { Id = "emote-dance", Name = "Dance emote", Kind = CosmeticKind.Emote, Rank = 21 },
            new CosmeticDef { Id = "emote-spin", Name = "Spin emote", Kind = CosmeticKind.Emote, Rank = 25 },
            new CosmeticDef { Id = "emote-salute", Name = "Salute emote", Kind = CosmeticKind.Emote, Rank = 31 },
            new CosmeticDef { Id = "emote-fireworks", Name = "Fireworks emote", Kind = CosmeticKind.Emote, Rank = 41 },
            new CosmeticDef { Id = "emote-steam", Name = "Steam emote", Kind = CosmeticKind.Emote, Rank = 52 },
            new CosmeticDef { Id = "name-outline", Name = "Steel Outline name", Kind = CosmeticKind.NameEffect, Rank = 4 },
            new CosmeticDef { Id = "name-frost", Name = "Frost name", Kind = CosmeticKind.NameEffect, Rank = 11 },
            new CosmeticDef { Id = "name-ember", Name = "Ember name", Kind = CosmeticKind.NameEffect, Rank = 13 },
            new CosmeticDef { Id = "name-neon", Name = "Neon name", Kind = CosmeticKind.NameEffect, Rank = 18 },
            new CosmeticDef { Id = "name-wave", Name = "Wave name", Kind = CosmeticKind.NameEffect, Rank = 22 },
            new CosmeticDef { Id = "name-shimmer", Name = "Shimmer name", Kind = CosmeticKind.NameEffect, Rank = 27 },
            new CosmeticDef { Id = "name-glitch", Name = "Glitch name", Kind = CosmeticKind.NameEffect, Rank = 33 },
            new CosmeticDef { Id = "name-glitchy", Name = "Glitchy name", Kind = CosmeticKind.NameEffect, Rank = 36 },
            new CosmeticDef { Id = "name-fire", Name = "Fire name", Kind = CosmeticKind.NameEffect, Rank = 38 },
            new CosmeticDef { Id = "name-gold", Name = "Gold Foil name", Kind = CosmeticKind.NameEffect, Rank = 44 },
            new CosmeticDef { Id = "name-void", Name = "Void name", Kind = CosmeticKind.NameEffect, Rank = 48 },
            new CosmeticDef { Id = "name-rainbow", Name = "Rainbow name", Kind = CosmeticKind.NameEffect, Rank = 56 },
            new CosmeticDef { Id = "aim-dotted", Name = "Dotted Tracer aim line", Kind = CosmeticKind.AimLine, Rank = 5 },
            new CosmeticDef { Id = "aim-chevron", Name = "Chevron Flow aim line", Kind = CosmeticKind.AimLine, Rank = 11 },
            new CosmeticDef { Id = "aim-ruler", Name = "Ruler aim line", Kind = CosmeticKind.AimLine, Rank = 14 },
            new CosmeticDef { Id = "aim-ember", Name = "Ember Wisp aim line", Kind = CosmeticKind.AimLine, Rank = 17 },
            new CosmeticDef { Id = "aim-frost", Name = "Frost Shard aim line", Kind = CosmeticKind.AimLine, Rank = 21 },
            new CosmeticDef { Id = "aim-storm", Name = "Storm Arc aim line", Kind = CosmeticKind.AimLine, Rank = 26 },
            new CosmeticDef { Id = "aim-pixel", Name = "Pixel Beam aim line", Kind = CosmeticKind.AimLine, Rank = 29 },
            new CosmeticDef { Id = "aim-helix", Name = "Helix aim line", Kind = CosmeticKind.AimLine, Rank = 34 },
            new CosmeticDef { Id = "aim-prism", Name = "Prism aim line", Kind = CosmeticKind.AimLine, Rank = 46 },
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
        public static byte CardBgIndex(string id) => IndexOf(CosmeticKind.CardBackground, id);
        /// Emotes (Game/EmoteLooks.cs): not equipped, every one your rank has is on the emote bar. Their number on the wire is 1.. in table order.
        public static byte EmoteIndex(string id) => IndexOf(CosmeticKind.Emote, id);
        public static string EmoteId(int index) => IdOf(CosmeticKind.Emote, index);
        public static int EmoteCount => OfKind(CosmeticKind.Emote).Count;
        /// Name effects (Game/NameEffects.cs): the look of a player's name, seen by everyone. 0 none, then 1.. in table order.
        public static byte NameFxIndex(string id) => IndexOf(CosmeticKind.NameEffect, id);
        public static string NameFxId(int index) => IdOf(CosmeticKind.NameEffect, index);
        /// Aim lines (Game/AimLineLooks.cs): only your own line is drawn, so this never goes on the wire. 0 is the Classic line, then 1.. in table order.
        public static byte AimLineIndex(string id) => IndexOf(CosmeticKind.AimLine, id);
        public static string AimLineId(int index) => IdOf(CosmeticKind.AimLine, index);
        public static string CardBgId(int index) => IdOf(CosmeticKind.CardBackground, index);  // "card-dusk" ... (Game/CardBackgrounds.cs draws them)

        /// The frame's base colour for a lobby entry's frame number (the extras on top are Game/FrameLooks.cs).
        public static Rgba FrameColor(int index)
        {
            switch (FrameId(index))
            {
                case "frame-circuit": return new Rgba(0.31f, 0.82f, 0.71f);
                case "frame-ember": return new Rgba(1f, 0.48f, 0.1f);
                case "frame-void": return new Rgba(0.71f, 0.45f, 1f);
                case "frame-gold":
                case "frame-foil": return new Rgba(1f, 0.8f, 0.35f);
                case "frame-prism": return new Rgba(0.9f, 0.95f, 1f);
                default: return new Rgba(0.72f, 0.78f, 0.9f);  // Steel
            }
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
