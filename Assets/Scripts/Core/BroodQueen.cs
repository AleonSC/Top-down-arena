using System;

namespace Arena
{
    /// Brood Queen: the DPS Summoner's fourth ultimate (key 3 of the lobby's Ultimate row; Legion stays the default, Rodent
    /// Minigun is key 1, Mammoth Fall key 2), from the mockup (claude.ai artifact "Spider Queen", renamed so it doesn't clash with
    /// the Spider Calling's card).
    ///   - Drop (BqDrop s): a giant spider lowers itself on a silk thread at your cursor (up to BqRange px away). Going down while
    ///     it drops cancels it. It lands for BqLandDmg in BqLandRadius px and leaves a web there (BqLandWeb s).
    ///   - Hunt (BqTime s, mastered BqTimeMastered): the queen (`MinionKind.BroodQueen`, TickBroodQueen) walks at the nearest enemy
    ///     within BqHunt px (BqSpeed) and bites it, BqBite every BqBiteEvery s, each bite adding a venom bleed (BqVenom a second a
    ///     stack, up to five). Regular enemies within BqTaunt px go for her (walking and shooting), and her BlockCapacity soaks their
    ///     shots: worn out, she's gone early.
    ///   - Brood: every BqBroodEvery s she sends out BqVolley broodlings (`MinionKind.Broodling`, TickBroodling), at most BqAlive out.
    ///     A broodling runs (BqLingSpeed) at an enemy she isn't biting, the one with the fewest broodlings on it first, latches on
    ///     for BqLatch s and bites BqLingBite every BqLingEvery s. If its prey dies it hops to another within BqHop px, or pops.
    ///   - Web spit: every BqSpitEvery s she spits a web (`ZoneKind.BroodWeb`, BqWebRadius px, BqWebTime s) at the biggest pack
    ///     within BqSpitRange px; enemies on a web are slowed BqWebSlow (Heavy enemies and bosses wade).
    ///   - Climb (BqClimb s): she climbs back up her thread; every broodling still latched bursts for BqBurst.
    /// All damage is times your damage and the ultimate's mastery (Minion.Power). The Rare card Hatchery (Stats.BroodHatch): a
    /// broodling's kill hatches more broodlings where the enemy fell.
    /// The queen's look reads Minion.Stacks (0 dropping, 1 walking, 2 biting, 3 climbing), Wind (a bite's lunge), LinkTo (her prey)
    /// and its age (Duration - T); a broodling's reads Stacks (0 running, 1 latched) and Wind (a bite). Game/BroodQueenLook.cs.
    public partial class Game
    {
        public const float BqDrop = 0.8f, BqTime = 10f, BqTimeMastered = 13f, BqClimb = 0.9f, BqRange = 360f;
        public const float BqLandDmg = 40f, BqLandRadius = 70f, BqLandWeb = 2f;
        public const float BqHunt = 280f, BqSpeed = 120f, BqReach = 30f, BqTurn = 5f, BqBite = 30f, BqBiteEvery = 0.6f, BqVenom = 3f, BqTaunt = 120f;
        public const float BqBroodEvery = 1.2f, BqLingSpeed = 240f, BqLatch = 3f, BqLingBite = 4f, BqLingEvery = 0.25f, BqHop = 120f, BqBurst = 6f;
        public const int BqVolley = 2, BqAlive = 8, BqHatchCap = 4;  // BqHatchCap: Hatchery's broodlings may go this far over BqAlive
        public const float BqLingHunt = 700f, BqLingCrowd = 400f, BqLingQueenPrey = 600f, BqLingNearQueen = 100f;
        public const float BqSpitEvery = 3f, BqSpitRange = 360f, BqWebRadius = 80f, BqWebTime = 3f, BqWebSlow = 0.5f, BqPack = 80f;
        public static readonly Rgba BqViolet = new Rgba(0.72f, 0.49f, 1f);

        /// Brood Queen's entry: key 3 of the DPS Summoner's Ultimate row. Upgrades.Abilities gathers it.
        internal static readonly AbilityDef[] BroodQueenUltimates =
        {
            new AbilityDef
            {
                Id = AbilityId.BroodQueen, Kit = Kit.Summoner, Role = Role.Dps, Choice = AbilityDef.Ult, UltKey = 3,
                Name = "Brood Queen", Cooldown = 75f,
                Desc = "Drop a giant spider at your cursor. She bites and webs the pack and sends broodlings after other enemies.",
                Capstone = "She stays 13 s.",
                Bot = b => b.HaveTarget && b.TargetD < BqRange,
            },
        };

        /// The whole show for a hunt of `hunt` s.
        public static float BqTotal(float hunt) => BqDrop + hunt + BqClimb;

        /// Seconds since the queen was cast.
        public static float BqAge(Minion m) => m.Duration - m.T;

        /// She has started to climb away.
        public static bool BqClimbing(Minion m) => m.T <= BqClimb;

        void CastBroodQueen(Player p, V2 target, bool cap)
        {
            V2 at = ClampToArena(p.Pos + (target - p.Pos).LimitLength(BqRange), 40f);
            float dur = BqTotal(cap ? BqTimeMastered : BqTime);
            var m = SpawnMinion(MinionKind.BroodQueen, p, at, dur);  // (cast: Power carries the mastery)
            m.Aim = MathX.Pi / 2f;
            m.Stacks = 0;
            m.Cd = 0.4f;          // the first brood
            m.SeedCd = 1.5f;      // the first web spit
            m.HurtT = 0.3f;       // the bite clock (her first bite a beat after she lands)
            p.UltT = dur;
            BroadcastFx(FxKind.Ring, at, BqViolet, BqLandRadius, 0.5f, Sfx.Summon);
        }

        int BroodlingsOut(Minion queen)
        {
            int n = 0;
            foreach (var o in Minions.Values)
                if (o.Kind == MinionKind.Broodling && o.Head == queen.Id)
                    n++;
            foreach (var o in broodPending)
                if (o.Head == queen.Id)
                    n++;
            return n;
        }

        /// Host, each tick for a Brood Queen (TickMinions, instead of its usual moves). True when she's gone.
        bool TickBroodQueen(Minion m, Player owner, float dt)
        {
            float age = BqAge(m);
            if (age < BqDrop)
            {
                m.Stacks = 0;
                if (owner.Downed || !Battle)
                {
                    owner.UltT = 0f;
                    return true;  // the thread snaps with nothing on it
                }
                return false;
            }
            float dmgMult = owner.Stats.DmgMult * m.Power;
            if (!m.Stood)
            {
                m.Stood = true;  // she lands
                foreach (var e in FoesInCircle(m.Pos, BqLandRadius, by: m.Owner))
                    HurtEnemy(e, BqLandDmg * dmgMult, m.Owner);
                AddBroodWeb(owner, m.Pos, BqLandRadius, BqLandWeb);
                BroadcastFx(FxKind.Ring, m.Pos, BqViolet, BqLandRadius * 1.3f, 0.4f, Sfx.BossSlam);
            }
            m.Wind = Math.Max(0f, m.Wind - dt * 4f);
            if (BqClimbing(m))
            {
                m.Stacks = 3;
                m.LinkTo = 0;  // (each broodling sees her climbing and bursts: TickBroodling)
                return false;
            }
            // Taunt: regular enemies near her go for her.
            foreach (var e in Enemies.Values)
                if (Targetable(e) && !e.Def.IsBoss && e.Pos.WithinReach(m.Pos, BqTaunt + e.Radius))
                {
                    e.ImpTauntId = m.Id;
                    e.ImpTauntT = Math.Max(e.ImpTauntT, 0.3f);
                }
            // Hunt the nearest enemy within BqHunt.
            Enemy prey = null;
            if (m.GoalEnemy != 0 && Enemies.TryGetValue(m.GoalEnemy, out prey) && (!Targetable(prey) || prey.Pos.FartherThan(m.Pos, BqHunt * 1.3f)))
                prey = null;
            if (prey == null)
            {
                float best = BqHunt;
                foreach (var e in Enemies.Values)
                {
                    if (!Targetable(e))
                        continue;
                    float d = e.Pos.DistanceTo(m.Pos);
                    if (d < best)
                    {
                        best = d;
                        prey = e;
                    }
                }
            }
            m.GoalEnemy = prey != null ? prey.Id : 0;
            m.LinkTo = m.GoalEnemy;
            if (prey != null)
            {
                float want = (prey.Pos - m.Pos).Angle, turn = MathX.WrapPi(want - m.Aim), step = BqTurn * dt;
                m.Aim = MathX.WrapPi(m.Aim + Math.Max(-step, Math.Min(step, turn)));
                if (prey.Pos.FartherThan(m.Pos, prey.Radius + BqReach))
                {
                    m.Pos = ClampToArena(m.Pos + V2.FromAngle(m.Aim) * (BqSpeed * dt), 30f);
                    m.Stacks = 1;
                }
                else
                {
                    m.Stacks = 2;
                    m.HurtT -= dt;
                    if (m.HurtT <= 0f)
                    {
                        m.HurtT = BqBiteEvery;
                        m.Wind = 1f;
                        HurtEnemy(prey, BqBite * dmgMult, m.Owner);
                        ApplyBleed(prey, BqVenom * dmgMult, m.Owner);
                        BroadcastFx(FxKind.Slash, m.Pos, prey.Pos, new Rgba(0.6f, 1f, 0.65f), 1.1f, 0.2f, Sfx.None);
                    }
                }
            }
            else
            {
                m.Stacks = 1;  // nothing near: she wanders back toward you
                V2 to = owner.Pos - m.Pos;
                if (to.Length > 140f)
                {
                    m.Aim = to.Angle;
                    m.Pos = ClampToArena(m.Pos + to.Normalized() * (BqSpeed * dt), 30f);
                }
            }
            // The brood.
            m.Cd -= dt;
            if (m.Cd <= 0f)
            {
                m.Cd = BqBroodEvery;
                int out_ = BroodlingsOut(m);
                for (int i = 0; i < BqVolley && out_ < BqAlive; i++, out_++)
                {
                    V2 back = m.Pos - V2.FromAngle(m.Aim) * 26f + V2.FromAngle(m.Aim + MathX.Pi / 2f) * (i == 0 ? -10f : 10f);
                    SpawnBroodling(m, owner, back);
                }
            }
            // The web spit at the biggest pack.
            m.SeedCd -= dt;
            if (m.SeedCd <= 0f)
            {
                m.SeedCd = BqSpitEvery;
                Enemy at = null;
                int most = 0;
                foreach (var e in Enemies.Values)
                {
                    if (!Targetable(e) || e.Pos.FartherThan(m.Pos, BqSpitRange))
                        continue;
                    int n = 0;
                    foreach (var o in Enemies.Values)
                        if (Targetable(o) && o.Pos.WithinReach(e.Pos, BqPack))
                            n++;
                    if (n > most)
                    {
                        most = n;
                        at = e;
                    }
                }
                if (at != null)
                {
                    AddBroodWeb(owner, at.Pos, BqWebRadius, BqWebTime);
                    BroadcastFx(FxKind.Spark, m.Pos, at.Pos, new Rgba(0.93f, 0.9f, 1f), 6f, 0.25f, Sfx.None);
                }
            }
            return false;
        }

        void AddBroodWeb(Player p, V2 at, float radius, float time)
        {
            AddZone(ZoneKind.BroodWeb, p, at, radius, time);
        }

        /// Host, every tick, for each Brood Queen web (TickZones): it slows what's on it.
        void TickBroodWeb(Zone z)
        {
            foreach (var e in Enemies.Values)
            {
                if (!Targetable(e) || !e.Pos.WithinReach(z.Pos, z.Radius + e.Radius * 0.5f))
                    continue;
                if (Heavy(e))  // the usual slow skips Heavy enemies and bosses, so they wade
                {
                    e.WadeT = Math.Max(e.WadeT, 0.2f);
                    e.WadeMult = Math.Min(e.WadeMult, 1f - BqWebSlow);
                }
                else
                    Slow(e, 0.2f, 1f - BqWebSlow);
            }
        }

        /// Broodlings hatched while TickMinions walks the minions: they join after the loop (FlushBroodlings).
        readonly System.Collections.Generic.List<Minion> broodPending = new System.Collections.Generic.List<Minion>();

        void FlushBroodlings()
        {
            foreach (var l in broodPending)
                Minions[l.Id] = l;
            broodPending.Clear();
        }

        Minion SpawnBroodling(Minion queen, Player owner, V2 at)
        {
            float life = Math.Max(0.5f, queen.T);
            var l = new Minion  // (not SpawnMinion: that adds it to Minions, which TickMinions is walking; it joins after the loop)
            {
                Id = nextMinionId++, Kind = MinionKind.Broodling, Owner = owner.Id, Pos = ClampToArena(at, 10f), T = life, Duration = life,
            };
            l.Disp = l.Pos;
            broodPending.Add(l);
            l.Head = queen.Id;
            l.Power = queen.Power;
            l.Aim = queen.Aim + MathX.Pi;
            l.Stacks = 0;
            l.GoalEnemy = 0;
            return l;
        }

        /// The enemy a broodling goes for: the nearest within `range`, counting each broodling already on one as BqLingCrowd px
        /// farther, the queen's own prey BqLingQueenPrey farther, and one beside her BqLingNearQueen farther: they spread out.
        Enemy BroodPrey(Minion l, Minion queen, float range)
        {
            Enemy best = null;
            float bestD = float.MaxValue;
            foreach (var e in Enemies.Values)
            {
                if (!Targetable(e) || e.Pos.FartherThan(l.Pos, range))
                    continue;
                int on = 0;
                foreach (var o in Minions.Values)
                    if (o.Kind == MinionKind.Broodling && o != l && o.GoalEnemy == e.Id)
                        on++;
                float d = l.Pos.DistanceTo(e.Pos) + on * BqLingCrowd;
                if (queen != null && queen.GoalEnemy == e.Id)
                    d += BqLingQueenPrey;
                if (queen != null && e.Pos.WithinReach(queen.Pos, BqLingNearQueen))
                    d += 300f;
                if (d < bestD)
                {
                    bestD = d;
                    best = e;
                }
            }
            return best;
        }

        /// Host, each tick for a broodling (TickMinions, instead of its usual moves). True when it's gone.
        bool TickBroodling(Minion l, Player owner, float dt)
        {
            Minion queen;
            bool hasQueen = Minions.TryGetValue(l.Head, out queen) && queen.Kind == MinionKind.BroodQueen;
            Enemy e = null;
            if (l.GoalEnemy != 0 && Enemies.TryGetValue(l.GoalEnemy, out e) && !Targetable(e))
                e = null;
            float dmgMult = owner.Stats.DmgMult * l.Power;
            if (!hasQueen || BqClimbing(queen))
            {
                if (e != null && l.Stacks == 1)
                    HurtEnemy(e, BqBurst * dmgMult, l.Owner);  // it bursts on what it holds
                BroodlingPop(l);
                return true;
            }
            l.Wind = Math.Max(0f, l.Wind - dt * 6f);
            if (e == null)
            {
                if (l.Stacks == 1)  // its prey fell: hop to another near it, or pop
                {
                    e = BroodPrey(l, queen, BqHop);
                    if (e == null)
                    {
                        BroodlingPop(l);
                        return true;
                    }
                    l.Stacks = 0;
                }
                else
                    e = BroodPrey(l, queen, BqLingHunt);
                l.GoalEnemy = e != null ? e.Id : 0;
                if (e == null)
                {
                    BroodlingPop(l);
                    return true;
                }
            }
            if (l.Stacks == 0)  // running at it
            {
                V2 to = e.Pos - l.Pos;
                l.Aim = to.Angle;
                if (to.Length <= e.Radius + 4f + BqLingSpeed * dt)
                {
                    l.Stacks = 1;
                    l.Launch = BqLatch;
                    l.Ang = (l.Pos - e.Pos).Angle;
                    l.SentT = 0f;
                }
                else
                    l.Pos += to.Normalized() * (BqLingSpeed * dt);
                return false;
            }
            // Latched: it rides round the enemy, biting.
            l.Ang += 2f * dt;
            l.Pos = e.Pos + V2.FromAngle(l.Ang) * (e.Radius * 0.8f);
            l.Aim = (e.Pos - l.Pos).Angle;
            l.Launch -= dt;
            l.SentT -= dt;
            if (l.SentT <= 0f)
            {
                l.SentT = BqLingEvery;
                l.Wind = 1f;
                HurtEnemy(e, BqLingBite * dmgMult, l.Owner);
                if (e.Hp <= 0f && owner.Stats.BroodHatch > 0)
                    BroodHatch(queen, owner, e.Pos);
            }
            if (l.Launch <= 0f)
            {
                BroodlingPop(l);
                return true;
            }
            return false;
        }

        /// Hatchery: a broodling's kill hatches more where the enemy fell.
        void BroodHatch(Minion queen, Player owner, V2 at)
        {
            int out_ = BroodlingsOut(queen);
            for (int i = 0; i < owner.Stats.BroodHatch && out_ < BqAlive + BqHatchCap; i++, out_++)
                SpawnBroodling(queen, owner, at + V2.FromAngle(RandAngle()) * 6f);
            BroadcastFx(FxKind.Ring, at, BqViolet, 18f, 0.3f, Sfx.None);
        }

        void BroodlingPop(Minion l)
        {
            BroadcastFx(FxKind.Boom, l.Pos, BqViolet, 8f, 0.2f, Sfx.None);
        }
    }
}
