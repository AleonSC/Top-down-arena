using System;
using System.Collections.Generic;

namespace Arena
{
    /// Blood Golem: the Tank Hemomancer's third ultimate (key 2 of the lobby's Ultimate row; Vein Tithe stays the default, Bloodbath
    /// is key 1). Docs/Mockups/blood-golem.html. You pile blood onto yourself in coats and stand up as a golem made of them.
    ///   - Gather (GolGather s, you walk at GolMoveGather): every blood stack (Blood Blast's) and bleed stack on an enemy within
    ///     GolGatherRadius is torn off one at a time and becomes a coat (GolRipDamage to it). From GolPayFrom s the coats the stacks
    ///     didn't cover are paid from your health, GolCoatCost of max health each, up to GolCoatsMax (a cost you can't pay, at 1 HP,
    ///     adds nothing). Every enemy goes for you from the cast, as Stone Colossus does.
    ///   - Form: a shove round you; each coat is GolCoatArmor of your max health in blood armour that takes every hit before your
    ///     health (GolSoak, from Damage). GolTime s (mastered GolTimeMastered), at GolMove speed.
    ///   - Fire, Golem Fist (the weapon is put away): left, right, then a Slam. A fist hits GolFistDamage in a GolFistHalf cone out to
    ///     GolFistReach; the Slam GolSlamDamage to everything within GolSlamRadius, stunning GolSlamStun. Every hit adds a blood stack.
    ///   - Alt, Hurl Clot (GolClotCd): tear off a coat and throw it up to GolClotRange: GolClotDamage within GolClotRadius where it
    ///     lands and a Gore Mortar pool (ZoneKind.BloodPool) for GolClotPool s.
    ///   - An enemy the golem kills grows a coat back (up to GolCoatsMax).
    ///   - Collapse (time, armour gone, down, the wave over, or the slot changed): each coat left bursts for GolBurstDamage (mastered
    ///     GolBurstMastered) within GolBurstRadius, gives you back GolBurstHeal of your max health and allies in it GolBurstAllyHeal of theirs.
    /// All damage scales by your damage. Replicated: Player.GolT (time left, 0 = off), GolAge (since the cast), GolCoats (armour in coats,
    /// fractional; the gather's count before it forms) and GolPunch (the punch number plus its swing 0..1). FxKind.GolemFx carries the
    /// shed chunks, the collapse and the thrown clot (Game/BloodGolemLook.cs); the torn stacks are FxKind.Drain streams.
    public partial class Game
    {
        public const float GolGather = 2f, GolTime = 10f, GolTimeMastered = 13f;
        public const int GolCoatsMax = 12;
        public const float GolGatherRadius = 300f, GolRipDamage = 6f, GolRipFrom = 0.1f, GolRipSpan = 0.9f, GolRipGap = 0.15f;
        public const float GolPayFrom = 1.1f, GolPaySpan = 0.8f, GolCoatCost = 0.025f, GolCoatArmor = 0.08f;
        public const float GolMoveGather = 0.5f, GolMove = 0.75f;
        public const float GolFormRadius = 150f, GolFormKnock = 120f;
        public const float GolFistDamage = 45f, GolFistReach = 120f, GolFistHalf = 50f * MathX.Pi / 180f, GolFistKnock = 40f;
        public const float GolSlamDamage = 70f, GolSlamRadius = 120f, GolSlamAhead = 30f, GolSlamStun = 0.5f, GolSlamKnock = 70f;
        public const float GolPunchEvery = 0.42f, GolSlamEvery = 0.6f, GolSwing = 0.3f;
        public const float GolClotCd = 2f, GolClotRange = 400f, GolClotFlight = 0.5f, GolClotDamage = 60f, GolClotRadius = 80f, GolClotPool = 4f;
        public const float GolBurstDamage = 20f, GolBurstMastered = 30f, GolBurstRadius = 180f, GolBurstHeal = 0.02f, GolBurstAllyHeal = 0.01f;
        public const float GolFxShed = 0f, GolFxCollapse = 1f, GolFxClot = 2f;  // FxKind.GolemFx's kinds (Radius)
        public const float GolShedDur = 0.9f, GolCollapseDur = 1.1f, GolClotTail = 0.35f;

        public static readonly Rgba GolRed = new Rgba(0.62f, 0.06f, 0.12f);

        /// Blood Golem's entry: key 2 of the tank Hemomancer's Ultimate row. Upgrades.Abilities gathers it.
        internal static readonly AbilityDef[] GolemUltimates =
        {
            new AbilityDef
            {
                Id = AbilityId.BloodGolem, Kit = Kit.Hemomancer, Role = Role.Tank, Choice = AbilityDef.Ult, UltKey = 2,
                Name = "Blood Golem", Cooldown = 80f,
                Desc = "Tear the blood off enemies and your own veins into coats, then fight as a golem made of them.",
                Capstone = "Lasts 13 s and each coat left bursts for 30.",
                Bot = b => b.Crowd >= JuggBotCrowd && b.Me.Hp > b.Me.MaxHp * 0.4f,
            },
        };

        /// Gathering or standing as the golem.
        public static bool GolOn(Player p) => p.GolT > 0f && !p.Downed;
        /// Formed: past the gather.
        public static bool GolStanding(Player p) => GolOn(p) && p.GolAge >= GolGather;

        /// Your speed share while it's on (Player.Speed).
        public static float GolSpeed(Player p) => p.GolT <= 0f ? 1f : p.GolAge < GolGather ? GolMoveGather : GolMove;

        /// The punch number (from 1) and its swing 0..1 from the replicated GolPunch; a Slam is every third.
        public static int GolPunchNo(Player p) => (int)Math.Floor(p.GolPunch);
        public static bool GolIsSlam(int no) => no > 0 && no % 3 == 0;

        struct GolRip { public int Owner, Enemy; public float At; }
        struct GolClot { public int Owner; public V2 At; public float T, Dmg; }
        readonly List<GolRip> golRips = new List<GolRip>();
        readonly List<GolClot> golClots = new List<GolClot>();
        float golLocalCd;  // your own game: until Hurl Clot can go again

        void CastGolem(Player p, bool mastered)
        {
            p.GolT = GolGather + (mastered ? GolTimeMastered : GolTime);
            p.GolAge = 0f;
            p.GolCoats = 0f;
            p.GolPunch = 0f;
            p.GolArmor = 0f;
            p.GolPer = Math.Max(1f, p.MaxHp * GolCoatArmor);
            p.GolStep = 0;
            p.GolPunchCd = 0f;
            p.GolSwingT = -1f;
            p.GolClotCd = 0f;
            p.GolNeed = -1;
            p.GolPaid = 0f;
            p.GolShown = 0;
            p.GolMastered = mastered;
            p.GolUp = true;
            p.UltT = p.GolT;
            foreach (var e in Enemies.Values)  // the taunt, as Stone Colossus
            {
                e.TargetId = p.Id;
                e.RetargetT = Math.Max(e.RetargetT, 1f);
            }
            // every stack in reach is torn off in turn over GolRipSpan s
            var ids = new List<int>();
            foreach (var e in AllFoes(by: p.Id))
            {
                if (e.Pos.FartherOrAt(p.Pos, GolGatherRadius + e.Radius))
                    continue;
                int n = e.BlastStacks + e.BleedStacks;
                for (int i = 0; i < n; i++)
                    ids.Add(e.Id);
            }
            int take = Math.Min(GolCoatsMax, ids.Count);
            float gap = take > 0 ? Math.Min(GolRipGap, GolRipSpan / take) : 0f;
            for (int i = 0; i < take; i++)
                golRips.Add(new GolRip { Owner = p.Id, Enemy = ids[i], At = GolRipFrom + gap * i });
            BroadcastFx(FxKind.Ring, p.Pos, GolRed, 46f, 0.5f, Sfx.BlastTideGather);
        }

        /// From Damage: the blood armour takes what it can; returns what is left for shields and health.
        float GolSoak(Player p, float amount)
        {
            if (!GolStanding(p) || p.GolArmor <= 0f || amount <= 0f)
                return amount;
            float soak = Math.Min(p.GolArmor, amount);
            p.GolArmor -= soak;
            p.GolCoats = p.GolArmor / p.GolPer;
            return amount - soak;
        }

        /// The golem bursts (time, armour gone, down, the wave over, the slot changed).
        void EndGolem(Player p)
        {
            if (!p.GolUp)
                return;
            p.GolUp = false;
            int left = p.GolAge >= GolGather && !p.Downed ? (int)Math.Ceiling(p.GolArmor / p.GolPer - 1e-3f) : 0;
            left = Math.Max(0, Math.Min(GolCoatsMax, left));
            p.GolT = 0f;
            p.GolArmor = 0f;
            p.GolCoats = 0f;
            p.GolPunch = 0f;
            p.GolBurst = left;
            golRips.RemoveAll(r => r.Owner == p.Id);
            if (p.Slots[Upgrades.UltSlot].Id == AbilityId.BloodGolem)
                p.UltT = 0f;
            if (left > 0)
            {
                float dmg = (p.GolMastered ? GolBurstMastered : GolBurstDamage) * left * p.Stats.DmgMult;
                foreach (var e in AllFoes(by: p.Id))
                {
                    if (e.Pos.FartherOrAt(p.Pos, GolBurstRadius + e.Radius))
                        continue;
                    if (!Heavy(e))
                        KnockEnemy(e, AwayFrom(e.Pos, p.Pos, V2.FromAngle(p.Aim)) * 60f);
                    HurtEnemy(e, dmg, p.Id);
                }
                Heal(p, p.MaxHp * GolBurstHeal * left, p, "Blood Golem");
                foreach (var a in Players.Values)
                    if (a != p && !a.Downed && a.Pos.CloserThan(p.Pos, GolBurstRadius))
                        Heal(a, a.MaxHp * GolBurstAllyHeal * left, p, "Blood Golem");
            }
            BroadcastFx(FxKind.GolemFx, p.Pos, new V2(left, p.Id), GolRed, GolFxCollapse, GolCollapseDur, left > 0 ? Sfx.BlastTideWave : Sfx.Explode);
        }

        /// Host, every tick: the gather, the form, the swings, shedding, the clots, the end.
        void TickGolem(float dt)
        {
            foreach (var p in Players.Values)
            {
                if (!p.GolUp)
                    continue;
                p.GolT = Math.Max(0f, p.GolT - dt);
                float was = p.GolAge;
                p.GolAge += dt;
                p.GolPunchCd -= dt;
                p.GolClotCd -= dt;
                if (p.GolT <= 0f || p.Downed || !Battle || p.Slots[Upgrades.UltSlot].Id != AbilityId.BloodGolem || (p.GolAge >= GolGather && was >= GolGather && p.GolArmor <= 0f))
                {
                    EndGolem(p);
                    continue;
                }
                if (p.GolAge < GolGather)
                {
                    TickGather(p);
                    continue;
                }
                if (was < GolGather)
                {
                    FormGolem(p);
                    if (p.GolArmor <= 0f)
                    {
                        EndGolem(p);
                        continue;
                    }
                }
                // the swing's clock, for the look
                if (p.GolSwingT >= 0f)
                {
                    p.GolSwingT += dt;
                    if (p.GolSwingT >= GolSwing)
                        p.GolSwingT = -1f;
                }
                p.GolPunch = p.GolStep + (p.GolSwingT >= 0f ? Math.Min(0.999f, p.GolSwingT / GolSwing) : 0f);
                // shedding: a chunk falls for every coat lost
                int shown = (int)Math.Ceiling(p.GolArmor / p.GolPer - 1e-3f);
                for (int i = shown; i < p.GolShown; i++)
                    BroadcastFx(FxKind.GolemFx, p.Pos, p.Pos + V2.FromAngle(RandAngle()), GolRed, GolFxShed, GolShedDur, Sfx.BlastHit);
                p.GolShown = shown;
            }
            TickClots(dt);
        }

        void TickGather(Player p)
        {
            for (int i = golRips.Count - 1; i >= 0; i--)
            {
                var r = golRips[i];
                if (r.Owner != p.Id || r.At > p.GolAge)
                    continue;
                golRips.RemoveAt(i);
                if (!Enemies.TryGetValue(r.Enemy, out Enemy e) || e.Hp <= 0f || p.GolCoats >= GolCoatsMax)
                    continue;
                if (e.BlastStacks > 0)
                    e.BlastStacks--;
                else if (e.BleedStacks > 0)
                    e.BleedStacks--;
                else
                    continue;
                p.GolCoats++;
                p.GolRipped++;
                BroadcastFx(FxKind.Drain, e.Pos, p.Pos, GolRed, 3f, 0.45f, Sfx.None);
                HurtEnemy(e, GolRipDamage * p.Stats.DmgMult, p.Id);
            }
            if (p.GolAge < GolPayFrom)
                return;
            if (p.GolNeed < 0)
                p.GolNeed = GolCoatsMax - (int)p.GolCoats;
            int due = p.GolNeed <= 0 ? 0 : Math.Min(p.GolNeed, 1 + (int)((p.GolAge - GolPayFrom) / (GolPaySpan / p.GolNeed)));
            while (p.GolPaid < due && p.GolCoats < GolCoatsMax)
            {
                p.GolPaid++;
                float cost = p.MaxHp * GolCoatCost, paid = PayHealth(p, cost, "Blood Golem");
                if (paid >= 1f)
                    BroadcastFx(FxKind.Bleed, p.Pos, new V2(p.Id, 0f), GolRed, paid, 0.55f, Sfx.None);
                if (paid >= cost * 0.5f)
                    p.GolCoats++;
            }
        }

        void FormGolem(Player p)
        {
            golRips.RemoveAll(r => r.Owner == p.Id);
            p.GolArmor = p.GolCoats * p.GolPer;
            p.GolShown = (int)p.GolCoats;
            foreach (var e in AllFoes(by: p.Id))
                if (!Heavy(e) && e.Pos.CloserThan(p.Pos, GolFormRadius + e.Radius))
                    KnockEnemy(e, AwayFrom(e.Pos, p.Pos, V2.FromAngle(p.Aim)) * GolFormKnock);
            BroadcastFx(FxKind.Ring, p.Pos, GolRed, GolFormRadius, 0.55f, Sfx.BigExplode);
        }

        /// TickPlayers' fire branch while it's on: Fire punches (the weapon is put away; nothing during the gather).
        void GolemTrigger(Player p)
        {
            if (!GolStanding(p) || !p.Firing || p.ParryActive || p.StunT > 0f || p.GolPunchCd > 0f || State == MatchState.GameOver)
                return;
            p.GolStep++;
            p.GolSwingT = 0f;
            bool slam = GolIsSlam(p.GolStep);
            p.GolPunchCd = slam ? GolSlamEvery : GolPunchEvery;
            float dmg = p.Stats.DmgMult;
            if (slam)
            {
                p.GolSlams++;
                V2 at = p.Pos + V2.FromAngle(p.Aim) * GolSlamAhead;
                foreach (var e in AllFoes(by: p.Id))
                {
                    if (e.Pos.FartherOrAt(at, GolSlamRadius + e.Radius))
                        continue;
                    if (!Heavy(e))
                    {
                        KnockEnemy(e, AwayFrom(e.Pos, at, V2.FromAngle(p.Aim)) * GolSlamKnock);
                        Stun(e, GolSlamStun);
                    }
                    GolHit(p, e, GolSlamDamage * dmg);
                }
                BroadcastFx(FxKind.Ring, at, GolRed, GolSlamRadius, 0.4f, Sfx.Shockwave);
                return;
            }
            p.GolPunches++;
            foreach (var e in AllFoes(by: p.Id))
            {
                V2 d = e.Pos - p.Pos;
                if (d.Length > GolFistReach + e.Radius || Math.Abs(MathX.WrapPi(d.Angle - p.Aim)) > GolFistHalf && d.Length > e.Radius + 10f)
                    continue;
                if (!Heavy(e))
                    KnockEnemy(e, AwayFrom(e.Pos, p.Pos, V2.FromAngle(p.Aim)) * GolFistKnock);
                GolHit(p, e, GolFistDamage * dmg);
            }
            BroadcastFx(FxKind.Slash, p.Pos, p.Pos + V2.FromAngle(p.Aim) * GolFistReach, GolRed, GolFistHalf, 0.22f, Sfx.BlastHit);
        }

        /// A golem hit: the damage and a blood stack (Blood Blast's, owned by you).
        void GolHit(Player p, Enemy e, float dmg)
        {
            if (e.Hp > 0f)
            {
                e.BlastOwner = p.Id;
                if (e.BlastStacks < BlastStackCap(p))
                    e.BlastStacks++;
            }
            HurtEnemy(e, dmg, p.Id);
        }

        /// Host, an enemy died (HemomancerKit.OnKill): the golem that killed it grows a coat back.
        void GolemKill(Enemy e)
        {
            var p = PlayerOrNull(e.LastHitBy);
            if (p == null || !GolStanding(p) || !p.GolUp)
                return;
            p.GolArmor = Math.Min(GolCoatsMax * p.GolPer, p.GolArmor + p.GolPer);
            p.GolCoats = p.GolArmor / p.GolPer;
            p.GolShown = Math.Max(p.GolShown, (int)Math.Ceiling(p.GolCoats - 1e-3f));
            p.GolKills++;
        }

        /// Your own game: Alt while it's on. True whenever it's on (the weapon's own Alt is put away too).
        bool LocalGolemClot(Player me, V2 aimAt, bool pressed, float dt)
        {
            golLocalCd = Math.Max(0f, golLocalCd - dt);
            if (!GolOn(me))
                return false;
            if (!pressed || !GolStanding(me) || golLocalCd > 0f || me.GolCoats <= 1f || me.StunT > 0f)
                return true;
            golLocalCd = GolClotCd;
            if (IsHost)
                HostClot(me, aimAt);
            else
            {
                var w = Proto.Begin(Msg.WeaponDash);
                w.Write(WeaponAlt.Clot);
                w.Write(aimAt);
                session.SendToHost(Proto.End(w));
            }
            return true;
        }

        /// Host: Hurl Clot (Msg.WeaponDash WeaponAlt.Clot, or the host's own game). A coat comes off and flies to `target`.
        void HostClot(Player p, V2 target)
        {
            if (!GolStanding(p) || p.GolClotCd > 0f || p.GolArmor <= p.GolPer || State == MatchState.GameOver)
                return;
            p.GolClotCd = GolClotCd - 0.1f;  // (a little slack for the round trip)
            p.GolArmor -= p.GolPer;
            p.GolCoats = p.GolArmor / p.GolPer;
            p.GolShown = (int)Math.Ceiling(p.GolCoats - 1e-3f);  // (the clot is the chunk: no shed too)
            V2 d = target - p.Pos;
            if (d.Length > GolClotRange)
                target = p.Pos + d.Normalized() * GolClotRange;
            V2 from = p.Pos + V2.FromAngle(p.Aim) * 30f;
            golClots.Add(new GolClot { Owner = p.Id, At = target, T = GolClotFlight, Dmg = GolClotDamage * p.Stats.DmgMult });
            BroadcastFx(FxKind.GolemFx, from, target, GolRed, GolFxClot, GolClotFlight + GolClotTail, Sfx.Throw);
        }

        void TickClots(float dt)
        {
            for (int i = golClots.Count - 1; i >= 0; i--)
            {
                var c = golClots[i];
                c.T -= dt;
                if (c.T > 0f)
                {
                    golClots[i] = c;
                    continue;
                }
                golClots.RemoveAt(i);
                var p = PlayerOrNull(c.Owner);
                foreach (var e in AllFoes(by: c.Owner))
                    if (e.Pos.CloserThan(c.At, GolClotRadius + e.Radius))
                    {
                        if (p != null)
                            GolHit(p, e, c.Dmg);
                        else
                            HurtEnemy(e, c.Dmg, c.Owner);
                    }
                if (p != null)
                    AddZone(ZoneKind.BloodPool, p, c.At, GolClotRadius, GolClotPool);
                BroadcastFx(FxKind.Ring, c.At, GolRed, GolClotRadius, 0.4f, Sfx.WwSplash);
            }
        }

        // --- Network: the player's Blood Golem fields (SyncPlayer) ---

        void SyncBloodGolem(NetSync s, Player p)
        {
            s.Float(ref p.GolT);
            s.Float(ref p.GolAge);
            s.Float(ref p.GolCoats);
            s.Float(ref p.GolPunch);
        }
    }
}
