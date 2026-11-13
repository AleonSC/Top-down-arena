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
        public bool NotFirst;  // not in the first round of a run (Rioters open the Warden's approach alone)
        public int Fade = 3;  // after its boss: the last of the 3 fade waves it is still in (0: gone at the boss, 1-3), Long Campaign only
    }

    /// A boss's world: the enemies of its approach, and squads of them (which count toward the themed share).
    public class FactionDef
    {
        public EnemyType Boss;
        public FactionMember[] Members;
        public SquadDef[] Squads = new SquadDef[0];
        public SquadDef Finale;  // always in the last wave before the boss (when the approach is on), counting toward its share
        public WaveLesson[] Lessons;  // a Campaign's ten waves before this boss, one lesson each (null: built by reach, as before)
        public SquadDef[] LessonSquads = new SquadDef[0];  // small squads only the lessons use (by name); reach, comebacks and callbacks never pick them
    }

    /// One wave of a boss's teaching cycle in the 100-wave Campaign (Docs/RunLength.md step 3): the faction's enemies and
    /// squads that wave uses, so each wave previews one thing the boss will test. Squads by name (FactionDef.Squads).
    public class WaveLesson
    {
        public string Teaches;                 // what the wave is for (the docs and the test)
        public FactionMember[] Members;        // null: all the faction's members
        public string[] Squads = new string[0];  // null: all the faction's squads
        public bool Finale;                    // the faction's set piece comes in
        public string Also;                    // a squad (by name) that comes in big on top of the share, like a callback (the exam's Breakout)
        public float SquadChance = 0.5f;       // how often a fitting squad is picked over a single
        public EnemyType? Star;  // the enemy the wave is about: always at least StarPicks of it (null: none)
        public bool Trickle;  // the star comes in as stragglers through the wave's second half, not mixed in anywhere (Docs/Approaches.md)
        public const int StarPicks = 2;
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
                    new FactionMember { Type = EnemyType.Guard, NotFirst = true, Weight = 2f },
                    new FactionMember { Type = EnemyType.RiotShield, Fade = 0, Weight = 2f, From = 2 },
                    new FactionMember { Type = EnemyType.GasGrenadier, Fade = 0, Weight = 1.5f, From = 2 },
                    new FactionMember { Type = EnemyType.TaserGuard, Fade = 0, Weight = 1.5f, From = 3 },  // the faction's shooter that stuns (WardenFaction.cs)
                    new FactionMember { Type = EnemyType.SecurityGuard, Fade = 1, Weight = 2f, From = 2 },  // its pistol guard: leading tracer rounds, a magazine of seven (SecurityGuard.cs)
                    new FactionMember { Type = EnemyType.Thug, Fade = 2, Weight = 1.5f, From = 2 },  // the prisoners' brute: a Brute with a ball and chain (PrisonKin.cs)
                    new FactionMember { Type = EnemyType.Shiv, Fade = 2, Weight = 1f, From = 3 },  // the prisoners' lunger: a taped shank (PrisonKin.cs)
                },
                Squads = new[]
                {
                    new SquadDef { Name = "Breakout", From = 1, Members = new[] { EnemyType.Guard, EnemyType.Rioter, EnemyType.Rioter, EnemyType.Rioter, EnemyType.Rioter, EnemyType.Rioter, EnemyType.Rioter } },
                    new SquadDef { Name = "Yard", From = 3, Members = new[] { EnemyType.Thug, EnemyType.Rioter, EnemyType.Rioter, EnemyType.Rioter, EnemyType.Shiv } },
                    new SquadDef { Name = "Patrol", From = 2, Members = new[] { EnemyType.SecurityGuard, EnemyType.SecurityGuard, EnemyType.SecurityGuard, EnemyType.Rioter, EnemyType.Rioter } },
                    new SquadDef { Name = "Cell block", From = 3, Members = new[] { EnemyType.RiotShield, EnemyType.RiotShield, EnemyType.Rioter, EnemyType.Rioter, EnemyType.Rioter, EnemyType.Rioter } },
                },
                // Wave 4: a wall of four Riot Shields marching at you, five Guards close behind it (WardenFaction.cs).
                LessonSquads = new[]
                {
                    new SquadDef { Name = "Rioter pack", From = 1, Members = new[] { EnemyType.Rioter, EnemyType.Rioter, EnemyType.Rioter, EnemyType.Rioter, EnemyType.Rioter } },
                    new SquadDef { Name = "Guard pair", From = 1, Members = new[] { EnemyType.Guard, EnemyType.Guard } },
                },
                Finale = new SquadDef { Name = "Riot line", From = 4, Form = SquadForm.RiotLine, Members = new[] {
                    EnemyType.RiotShield, EnemyType.RiotShield, EnemyType.RiotShield, EnemyType.RiotShield,
                    EnemyType.Guard, EnemyType.Guard, EnemyType.Guard, EnemyType.Guard, EnemyType.Guard } },
                // The Campaign's Warden cycle: each wave previews one part of its fight (Docs/RunLength.md).
                Lessons = new[]
                {
                    // Docs/Approaches.md's chart: each wave one new enemy, alone and late; squads only use enemies you have already met.
                    new WaveLesson { Teaches = "Rioters swarm (the riot stage's crowds)", Members = new[] { M(EnemyType.Rioter, 1f) } },
                    new WaveLesson { Teaches = "Guards join (the first one trickles in late)", Trickle = true, Star = EnemyType.Guard, Members = new[] { M(EnemyType.Rioter, 1f) }, Squads = new[] { "Rioter pack" } },
                    new WaveLesson { Teaches = "Thugs: the prisoners' brute", Trickle = true, Star = EnemyType.Thug, Members = new[] { M(EnemyType.Rioter, 2f), M(EnemyType.Guard, 1.5f) }, Squads = new[] { "Rioter pack", "Guard pair" } },
                    new WaveLesson { Teaches = "Security Guards' aimed tracers (its aimed fans)", Trickle = true, Star = EnemyType.SecurityGuard, Members = new[] { M(EnemyType.Rioter, 2f), M(EnemyType.Guard, 2f), M(EnemyType.Thug, 1.5f) }, Squads = new[] { "Yard", "Guard pair" } },
                    new WaveLesson { Teaches = "A recap of everything so far", Members = new[] { M(EnemyType.Rioter, 2f), M(EnemyType.Guard, 2f), M(EnemyType.Thug, 1.5f), M(EnemyType.SecurityGuard, 2f) }, Squads = new[] { "Yard", "Patrol" } },
                    new WaveLesson { Teaches = "Riot Shields: flank them (its riot shield)", Trickle = true, Star = EnemyType.RiotShield, Members = new[] { M(EnemyType.Rioter, 2f), M(EnemyType.Guard, 2f), M(EnemyType.Thug, 1.5f), M(EnemyType.SecurityGuard, 2f) }, Squads = new[] { "Yard", "Patrol" } },
                    new WaveLesson { Teaches = "Tear gas, and shields shoving you into it", Trickle = true, Star = EnemyType.GasGrenadier, Members = new[] { M(EnemyType.RiotShield, 2f), M(EnemyType.Rioter, 2f), M(EnemyType.Guard, 1f), M(EnemyType.SecurityGuard, 1f) }, Squads = new[] { "Yard", "Cell block" } },
                    new WaveLesson { Teaches = "Taser Guards: stuns, and parrying the Fork Bolt back", Trickle = true, Star = EnemyType.TaserGuard, Members = new[] { M(EnemyType.GasGrenadier, 1.5f), M(EnemyType.RiotShield, 2f), M(EnemyType.Rioter, 2f), M(EnemyType.Guard, 1f) }, Squads = new[] { "Yard", "Cell block" } },
                    new WaveLesson { Teaches = "Every squad, tighter", Members = null, Squads = null, SquadChance = 0.75f },
                    new WaveLesson { Teaches = "The exam: the Riot line and a big Breakout, with everything", Members = null, Squads = new string[0], Finale = true, Also = "Breakout" },
                },
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

        static FactionMember M(EnemyType t, float weight) => new FactionMember { Type = t, Weight = weight };

        /// The faction of a boss, or null if it has none yet.
        public static FactionDef For(EnemyType boss) => Array.Find(All, f => f.Boss == boss);

        /// The fading boss's share of the five waves of a cycle (Docs/Campaign.md), and the older pool's from wave 11 on.
        public static readonly float[] FadeShare = { 0.35f, 0.25f, 0.15f, 0.05f, 0f };
        public static readonly float[] LongFade = { 0.40f, 0.20f, 0.08f };  // the Long Campaign's fade, by shown wave after a boss: gone after three (FactionMember.Fade says who stays)

        /// The fading faction's share of track wave `n`; `shown` is the Long Campaign's wave number (0: the plain track).
        public static float FadeAt(int n, int shown)
        {
            int cycle = (n - 1) / 5, p = (n - 1) % 5;
            if (cycle < 1)
                return 0f;
            if (shown <= 0)
                return FadeShare[p];
            int i = (shown - 1) % 10;
            return i < LongFade.Length ? LongFade[i] : 0f;
        }
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
        public static List<Slice> Campaign(int n, int shown = 0)
        {
            var list = new List<Slice>();
            if (n < 1)
                return list;
            int cycle = (n - 1) / 5;
            float fade = FadeAt(n, shown);
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
            var order = BossSchedule.Campaign;  // (the Campaign's own faction mix)
            add(order.BossAt(5 * (cycle + 1)), 1f - fade - pool, true, 0f);
            if (cycle >= 1)
                add(order.BossAt(5 * cycle), fade, false, 0f);
            var older = new List<EnemyType>();
            for (int j = 1; j < cycle; j++)
                if (For(order.BossAt(5 * j)) != null)
                    older.Add(order.BossAt(5 * j));
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

        /// A comeback squad as it turns up in a late wave: its members twice over and the faction's toughest enemy as an anchor,
        /// so an old squad is a real event (a Breakout of 12 Rioters and a Guard, with a Riot Shield at the back). Docs/Approaches.md.
        public static SquadDef Callback(SquadDef sq, FactionDef from)
        {
            var members = new List<EnemyType>(sq.Members);
            members.AddRange(sq.Members);
            EnemyType anchor = sq.Members[0];
            float hp = 0f;
            foreach (var m in from.Members)
                if (Defs.Get(m.Type).Hp > hp) { hp = Defs.Get(m.Type).Hp; anchor = m.Type; }
            members.Add(anchor);
            return new SquadDef { Name = sq.Name, From = sq.From, Form = sq.Form, Members = members.ToArray() };
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

        /// The themed share of this wave (0 to 1): 0 on boss waves, for bosses without a faction, or when it's turned off.
        public float ApproachShare(int wave)
        {
            if (Hunt == null && BossSlot && wave == Wave)
                return 0f;  // the boss fight itself has no approach
            int step = ApproachStepAt(wave);
            float share = D(DiffKnob.ApproachShare);
            if (step == 0 || Approaches.For(ApproachBossAt(wave)) == null || Practice != null)
                return 0f;
            if (Hunt != null || Quick)
                return 1f;  // a Boss Hunt's (and Quick Play's) build-up waves are only the boss's own enemies (Hunts.cs, QuickPlay.cs)
            if (CampaignOn)
                return Approaches.Campaign(wave, wave == Wave && LongLegs ? ShownWave : 0).Find(x => x.Current).Share;  // the next boss's slice (0 if it has no faction)
            if (share <= 0f)
                return 0f;
            return D(DiffKnob.ApproachRamp) >= 0.5f ? share * RampSteps[step - 1] : share;
        }

        /// Is the campaign buildup on (every wave themed, Approaches.Campaign)? Not in a hunt or the Practice Range.
        public bool CampaignOn => Hunt == null && !Quick && Practice == null && D(DiffKnob.Campaign) >= 0.5f;

        /// With the ramp on, each approach wave's share of the full share: from 40% up to all of it.
        static readonly float[] RampSteps = { 0.4f, 0.6f, 0.8f, 1f };

        /// Host, BuildWave: takes the themed share off `count` and picks the faction's enemies for it (squads where they
        /// fit, singles for the rest). They're mixed into the wave by MixApproach once the rest is built. Uses no random
        /// rolls when the share is 0, so those waves come out exactly as before.
        void BuildApproach(int n, ref int count)
        {
            plan.ApproachSingles.Clear();
            plan.ApproachSquads.Clear();
            plan.ApproachTrickle.Clear();
            int of = OnPath && CurrentPath == PathId.EliteGauntlet ? count / 2 : count;  // Elite Gauntlet halves the whole wave
            if (CampaignOn)
            {
                if (ApproachStepAt(n) == 0)
                    return;
                int step = ApproachStepAt(n);
                FactionDef cbFrom;
                var cb = Approaches.Comeback(n, out cbFrom);  // (a comeback squad takes the older pool's slice this wave)
                int shown = LongLegs ? ShownWave : 0;
                foreach (var sl in Approaches.Campaign(n, shown))  // (every faction's enemies, each by its slice; the rest stays the usual mix)
                {
                    int themed = Math.Min(count, (int)Math.Round(of * (cb != null ? sl.Share - sl.Pool : sl.Share)));
                    if (themed > 0 && sl.Current && LessonNow(sl.Faction) != null)
                        PickLesson(sl.Faction, LessonNow(sl.Faction), ref count, themed);  // a Campaign's teaching cycle: this wave's lesson
                    else if (themed > 0 && shown > 0 && !sl.Current && sl.Pool <= 0f)
                    {
                        // The last boss's faction fading out (Long Campaign): singles only, the tricky ones first to go (FactionMember.Fade).
                        int stay = (shown - 1) % 10 + 1;
                        var left = Array.FindAll(sl.Faction.Members, m => m.Fade >= stay);
                        if (left.Length > 0)
                            PickFrom(left, new SquadDef[0], null, 0f, ref count, themed);
                    }
                    else if (themed > 0)
                        PickFaction(sl.Faction, sl.Current ? step : 99, sl.Current && Bosses.FinaleWave(n), ref count, themed);  // (older factions are in full; only the next boss's builds up)
                }
                if (cb != null)
                {
                    var big = Approaches.Callback(cb, cbFrom);  // (a callback: the old squad, big)
                    plan.ApproachSquads.Add(big);
                    foreach (var t in big.Members)
                        plan.Kinds.Add(t);
                    count -= Math.Min(count, big.Members.Length);
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
            int reach = ApproachStepAt(n);  // (a hunt's nine waves reach through the faction as a run's four do)
            if (LessonNow(f) != null)
            {
                PickLesson(f, LessonNow(f), ref count, themed1);
                return;
            }
            PickFaction(f, reach, Bosses.FinaleWave(n), ref count, themed1);
        }

        /// Takes `themed` enemies off `count` and picks them from a faction: squads where they fit, singles for the rest.
        /// `reach` is how far into the faction's members and squads the wave goes (their From); the finale comes in when
        /// asked for, whatever the share.
        void PickFaction(FactionDef f, int reach, bool withFinale, ref int count, int themed)
        {
            bool first = Wave <= 1;  // the first round: no NotFirst members, nor squads holding one
            var members = Array.FindAll(f.Members, m => m.From <= reach && !(first && m.NotFirst));
            var squads = Array.FindAll(f.Squads, s => s.From <= reach && !(first && Array.Exists(s.Members, t => Array.Exists(f.Members, m => m.NotFirst && m.Type == t))));
            PickFrom(members, squads,
                withFinale && f.Finale != null && f.Finale.From <= reach ? f.Finale : null, 0.5f, ref count, themed);
        }

        static SquadDef[] Concat(SquadDef[] a, SquadDef[] b)
        {
            var r = new SquadDef[a.Length + b.Length];
            a.CopyTo(r, 0);
            b.CopyTo(r, a.Length);
            return r;
        }

        /// The lesson of the wave being built, in a Campaign wave that leads to this faction's boss (null: none).
        public WaveLesson LessonNow(FactionDef f)
        {
            if (!LongLegs || f == null || f.Lessons == null || f.Lessons.Length == 0 || ShownWave < 1)
                return null;
            return f.Lessons[Math.Min(f.Lessons.Length - 1, (ShownWave - 1) % 10)];  // (a cycle is ten Campaign waves)
        }

        /// Takes `themed` enemies off `count` from a lesson's members and squads (Approaches.cs WaveLesson).
        void PickLesson(FactionDef f, WaveLesson l, ref int count, int themed)
        {
            var members = l.Members ?? f.Members;
            var squads = l.Squads == null ? f.Squads : Array.FindAll(f.Squads, s => Array.IndexOf(l.Squads, s.Name) >= 0);
            if (l.Squads != null)
                squads = Concat(squads, Array.FindAll(f.LessonSquads, s => Array.IndexOf(l.Squads, s.Name) >= 0));
            if (l.Also != null)
            {
                var also = Array.Find(f.Squads, s => s.Name == l.Also);
                if (also != null)
                {
                    var big = Approaches.Callback(also, f);
                    plan.ApproachSquads.Add(big);
                    foreach (var t in big.Members)
                        plan.Kinds.Add(t);
                    count -= Math.Min(count, big.Members.Length);
                }
            }
            int stars = 0;
            if (l.Star.HasValue)
                for (; stars < WaveLesson.StarPicks && stars < themed; stars++)
                    (l.Trickle ? plan.ApproachTrickle : plan.ApproachSingles).Add(l.Star.Value);  // the wave's lesson always shows up (a Trickle one only late in the wave)
            if (stars > 0)
                plan.Kinds.Add(l.Star.Value);
            count -= stars;
            PickFrom(members, squads, l.Finale ? f.Finale : null, l.SquadChance, ref count, themed - stars);
        }

        /// A boss faction may bring in this type (already in the wave, or a new type within its share of the type budget).
        bool ApproachKindOk(EnemyType t) => plan.Kinds.Count < plan.ApproachKindCap || plan.Kinds.Contains(t);

        /// ...and these squad members, all of them.
        bool ApproachKindsFit(EnemyType[] members)
        {
            int extra = 0;
            for (int i = 0; i < members.Length; i++)
                if (!plan.Kinds.Contains(members[i]) && Array.IndexOf(members, members[i]) == i)
                    extra++;
            return extra == 0 || plan.Kinds.Count + extra <= plan.ApproachKindCap;
        }

        /// Takes `themed` enemies off `count`: the finale if given (whatever the share), then squads (about `squadChance`
        /// of picks, where one fits) and weighted singles for the rest.
        void PickFrom(FactionMember[] members, SquadDef[] squads, SquadDef finale, float squadChance, ref int count, int themed)
        {
            int spent = 0;
            if (finale != null)
            {
                themed = Math.Max(themed, Math.Min(count, finale.Members.Length));  // the set piece comes whatever the share
                plan.ApproachSquads.Add(finale);
                spent = finale.Members.Length;
                foreach (var t in finale.Members)
                    plan.Kinds.Add(t);
            }
            count -= themed;
            while (spent < themed)
            {
                // About half of it as squads, if one fits in what's left (and in the wave's type budget).
                var fits = Array.FindAll(squads, s => s.Members.Length <= themed - spent && plan.ApproachSquads.FindAll(a => a == s).Count < 2 && ApproachKindsFit(s.Members));  // (none more than twice)
                if (fits.Length > 0 && rng.NextDouble() < squadChance)
                {
                    var s = fits[rng.Next(fits.Length)];
                    plan.ApproachSquads.Add(s);
                    spent += s.Members.Length;
                    foreach (var t in s.Members)
                        plan.Kinds.Add(t);
                    continue;
                }
                float total = 0f;
                foreach (var m in members)
                    if (ApproachKindOk(m.Type))
                        total += m.Weight;
                if (total <= 0f)
                {
                    count += themed - spent;  // the type budget is spent: the rest of the share goes back to the usual mix
                    break;
                }
                float r = (float)rng.NextDouble() * total;
                FactionMember pick = null;
                foreach (var m in members)
                {
                    if (!ApproachKindOk(m.Type))
                        continue;
                    pick = m;
                    r -= m.Weight;
                    if (r <= 0f)
                        break;
                }
                plan.Kinds.Add(pick.Type);
                plan.ApproachSingles.Add(pick.Type);
                int pack = pick.Type == EnemyType.Rioter ? 2 : pick.Type == EnemyType.Runner ? 3 : 1;  // rioters come in pairs, runners in threes
                for (int k = 1; k < pack; k++)
                    plan.ApproachSingles.Add(pick.Type);
                spent += pack;
            }
        }

        /// Mixes the approach's enemies in with the rest of the wave: singles at random places in the spawn queue,
        /// squads among the others.
        void MixApproach()
        {
            foreach (var t in plan.ApproachSingles)
                spawnQueue.Insert(rng.Next(spawnQueue.Count + 1), t);
            foreach (var s in plan.ApproachSquads)
                squadQueue.Insert(rng.Next(squadQueue.Count + 1), s);
            // The trickle: the new enemy comes last, after the wave's squads (SpawnNextSquad), alone, one at a time.
            spawnQueue.AddRange(plan.ApproachTrickle);
            stragglers = plan.ApproachTrickle.Count;
            plan.ApproachSingles.Clear();
            plan.ApproachSquads.Clear();
            plan.ApproachTrickle.Clear();
        }
    }
}
