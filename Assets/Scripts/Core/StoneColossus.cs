using System;
using System.Collections.Generic;

namespace Arena
{
    /// Stone Colossus: the Tank Elementalist's third ultimate (lobby key 2 of the Ultimate row; Glacial Tomb stays the default,
    /// Lightning Rod is key 1). The earth piles onto you and you stand up as a golem for ColTime s.
    ///   - Rise (ColRise s): rock pillars erupt round you (ColRiseDamage, a shove) and you can't be hurt while it forms.
    ///   - Body: ColCut less damage, ColMove of your speed, and every enemy (bosses too) goes for you, as Roar does.
    ///   - Stomp: every ColStride px you walk a foot comes down beside you: ColStompDamage to enemies under it and a short stun.
    ///   - Slam: Fire is the slam (your weapon is away): both fists into the ground ColSlamAhead px ahead after ColWindup s,
    ///     ColSlamDamage to everything within ColSlamRadius, stun ColSlamStun, a shove. One every ColSlamEvery s.
    ///   - Crumble: when it ends (time, going down, the wave over) it breaks into ColBoulders boulders that roll ColRoll px
    ///     outward, ColBoulderDamage to each enemy they run over.
    /// All damage scales by your damage. Mastered: ColTimeMastered s and slams ColMasterWide wider; the Rare card Bedrock adds
    /// time and width the same way.
    /// Replicated: Player.ColT (time left, 0 = off), ColAge (seconds since the cast) and ColWind (the slam, 0..1, as the Titan's
    /// Wind). The floor marks are FxKind.TitanSlam with the earth kinds (TitanEarth + ...), drawn by Game/StoneColossusLook.cs.
    public partial class Game
    {
        public const float ColTime = 15f, ColTimeMastered = 18f, ColRise = 1f;
        public const float ColCut = 0.7f;                        // damage you take while it stands is cut by this much
        public const float ColMove = 0.8f, ColMoveRising = 0.4f, ColMoveSlamming = 0.3f;  // your speed share
        public const float ColRiseRadius = 95f, ColRiseDamage = 20f, ColRiseKnock = 90f;
        public const float ColStride = 40f;                      // px walked per step
        public const float ColStompRadius = 36f, ColStompDamage = 25f, ColStompStun = 0.15f, ColStompKnock = 40f;
        public const float ColFootSide = 36f, ColFootAhead = 14f;  // where a foot comes down, from your centre
        public const float ColSlamAhead = 110f, ColSlamRadius = 110f, ColMasterWide = 0.25f;
        public const float ColSlamDamage = 60f, ColSlamStun = 0.6f, ColSlamKnock = 80f;
        public const float ColWindup = 0.22f, ColFollow = 0.14f, ColSlamEvery = 1f;
        public const int ColBoulders = 8;
        public const float ColBoulderDamage = 40f, ColBoulderRadius = 16f, ColRoll = 260f, ColRollTime = 0.9f, ColBoulderKnock = 50f;
        public const float ColBedrockTime = 3f, ColBedrockWide = 0.25f;  // the Rare card Bedrock, a stack
        public const float ColScale = 0.65f;                      // the look: the golem's size against the Titan's body (TitanBody)

        /// FxKind.TitanSlam's earth kinds (Pos2.X): the Titan's own are 0 slam, 1 standing up, 2 rubble.
        public const int TitanEarth = 3;                          // + 0 slam, + 1 the rise, + 2 rubble
        public const int ColFxBoulders = 6, ColFxStomp = 7;       // the crumble's boulders (Pos2.Y = the first one's angle); a footfall

        /// Stone Colossus's entry: key 2 of the tank Elementalist's Ultimate row. Upgrades.Abilities gathers it.
        internal static readonly AbilityDef[] ColossusUltimates =
        {
            new AbilityDef
            {
                Id = AbilityId.StoneColossus, Kit = Kit.Elementalist, Role = Role.Tank, Choice = AbilityDef.Ult, UltKey = 2,
                Name = "Stone Colossus", Cooldown = 80f,
                Desc = "Become a huge earth golem: stomp what you walk over and slam the ground with both fists.",
                Capstone = "Lasts 18 s and slams reach 25% wider.",
                Bot = b => b.Crowd >= JuggBotCrowd,
            },
        };

        /// Standing as the golem (rising included).
        public static bool ColOn(Player p) => p.ColT > 0f && !p.Downed;
        /// Up and walking: past the rise.
        public static bool ColStanding(Player p) => ColOn(p) && p.ColAge >= ColRise;

        static float ColMult(Player p) => ColOn(p) ? 1f - ColCut : 1f;

        /// Your speed share while you're the golem (Player.Speed).
        public static float ColSpeed(Player p) => p.ColT <= 0f ? 1f : p.ColAge < ColRise ? ColMoveRising : p.ColWind > 0f ? ColMoveSlamming : ColMove;

        public static float ColSlamReach(Player p) => ColSlamRadius * (1f + (p.ColMastered ? ColMasterWide : 0f) + p.Stats.ColSlamWide);

        struct Boulder
        {
            public V2 From;
            public float Ang, T, Dmg;
            public int Owner;
            public HashSet<int> Hit;
        }
        readonly List<Boulder> boulders = new List<Boulder>();

        static float ColEase(float k) { k = Math.Max(0f, Math.Min(1f, k)); return 1f - (1f - k) * (1f - k) * (1f - k); }
        /// Where a crumble boulder is, t s after it broke off (the look rolls it the same way).
        public static V2 BoulderAt(V2 from, float ang, float t) => from + V2.FromAngle(ang) * (ColRoll * ColEase(t / ColRollTime));

        void CastColossus(Player p, bool mastered)
        {
            float time = (mastered ? ColTimeMastered : ColTime) + p.Stats.ColBonusTime;
            p.ColT = ColRise + time;
            p.ColAge = 0f;
            p.ColWind = 0f;
            p.ColSlamT = -1f;
            p.ColSlamCd = 0f;
            p.ColWalk = 0f;
            p.ColLast = p.Pos;
            p.ColMastered = mastered;
            p.ColUp = true;
            p.UltT = p.ColT;
            p.InvulnT = Math.Max(p.InvulnT, ColRise);
            foreach (var e in Enemies.Values)  // the taunt, as the Juggernaut Suit
            {
                e.TargetId = p.Id;
                e.RetargetT = Math.Max(e.RetargetT, 1f);
            }
            float dmg = p.Stats.DmgMult;
            foreach (var e in AllFoes(by: p.Id))
            {
                if (e.Pos.FartherOrAt(p.Pos, ColRiseRadius + e.Radius))
                    continue;
                if (!Heavy(e))
                    KnockEnemy(e, AwayFrom(e.Pos, p.Pos, V2.FromAngle(p.Aim)) * ColRiseKnock);
                HurtEnemy(e, ColRiseDamage * dmg, p.Id);
                Embrittle(e, p.Id);  // earth: cracked for the next hits (Brittle.cs)
            }
            BroadcastFx(FxKind.TitanSlam, p.Pos, new V2(TitanEarth + 1, 0f), ColEarth, ColRiseRadius, TitanCrackTime, Sfx.BigExplode);
        }

        public static readonly Rgba ColEarth = new Rgba(0.63f, 0.55f, 0.45f);

        /// The golem breaks apart (time, down, the wave over, the slot changed): rubble and the rolling boulders.
        void EndColossus(Player p)
        {
            if (!p.ColUp)
                return;
            p.ColUp = false;
            p.ColT = 0f;
            p.ColWind = 0f;
            p.ColSlamT = -1f;
            if (p.Slots[Upgrades.UltSlot].Id == AbilityId.StoneColossus)
                p.UltT = 0f;
            float first = RandAngle(), dmg = ColBoulderDamage * p.Stats.DmgMult;
            for (int i = 0; i < ColBoulders; i++)
                boulders.Add(new Boulder { From = p.Pos, Ang = first + MathX.Tau * i / ColBoulders, Dmg = dmg, Owner = p.Id, Hit = new HashSet<int>() });
            BroadcastFx(FxKind.TitanSlam, p.Pos, new V2(TitanEarth + 2, 0f), ColEarth, 50f, TitanRubbleTime, Sfx.Explode);
            BroadcastFx(FxKind.TitanSlam, p.Pos, new V2(ColFxBoulders, first), ColEarth, ColRoll, ColRollTime + 0.35f, Sfx.None);
        }

        /// Host, every tick: the clock, the feet, the slam, the boulders.
        void TickColossus(float dt)
        {
            foreach (var p in Players.Values)
            {
                if (!p.ColUp)
                    continue;
                p.ColT = Math.Max(0f, p.ColT - dt);
                p.ColAge += dt;
                if (p.ColT <= 0f || p.Downed || !Battle || p.Slots[Upgrades.UltSlot].Id != AbilityId.StoneColossus)
                {
                    EndColossus(p);
                    continue;
                }
                p.ColSlamCd -= dt;
                if (p.ColAge < ColRise)
                {
                    p.ColLast = p.Pos;
                    continue;
                }
                // the feet: one comes down every ColStride px walked
                V2 moved = p.Pos - p.ColLast;
                p.ColLast = p.Pos;
                if (moved.Length < 30f)  // (not a teleport)
                    p.ColWalk += moved.Length;
                if (p.ColWalk >= ColStride)
                {
                    p.ColWalk -= ColStride;
                    p.ColSide = 1 - p.ColSide;
                    V2 dir = moved.Length > 0.01f ? moved.Normalized() : V2.FromAngle(p.Aim);
                    V2.Axes(p.Aim, out V2 fx, out V2 fy);
                    ColStomp(p, p.Pos + dir * ColFootAhead + fy * ((p.ColSide == 0 ? -1f : 1f) * ColFootSide));
                }
                // the slam
                if (p.ColSlamT >= 0f)
                {
                    p.ColSlamT += dt;
                    p.ColWind = Math.Min(1f, p.ColSlamT / (ColWindup + ColFollow));
                    if (!p.ColSlamHit && p.ColSlamT >= ColWindup)
                    {
                        p.ColSlamHit = true;
                        ColSlamLand(p);
                    }
                    if (p.ColSlamT >= ColWindup + ColFollow)
                    {
                        p.ColSlamT = -1f;
                        p.ColWind = 0f;
                    }
                }
            }
            TickBoulders(dt);
        }

        /// TickPlayers' fire branch while you're the golem: Fire starts a slam (the weapon is put away).
        void ColossusTrigger(Player p)
        {
            if (!ColStanding(p) || !p.Firing || p.ParryActive || p.ColSlamT >= 0f || p.ColSlamCd > 0f || State == MatchState.GameOver)
                return;
            p.ColSlamT = 0f;
            p.ColSlamHit = false;
            p.ColSlamAng = p.Aim;
            p.ColSlamCd = ColSlamEvery;
        }

        void ColStomp(Player p, V2 at)
        {
            p.ColStomps++;
            float dmg = ColStompDamage * p.Stats.DmgMult;
            foreach (var e in AllFoes(by: p.Id))
            {
                if (e.Pos.FartherOrAt(at, ColStompRadius + e.Radius))
                    continue;
                if (!Heavy(e))
                {
                    KnockEnemy(e, AwayFrom(e.Pos, at, V2.FromAngle(p.Aim)) * ColStompKnock);
                    Stun(e, ColStompStun);
                }
                HurtEnemy(e, dmg, p.Id);
                Embrittle(e, p.Id);  // earth: cracked for the next hits (Brittle.cs)
            }
            BroadcastFx(FxKind.TitanSlam, at, new V2(ColFxStomp, 0f), ColEarth, ColStompRadius * 1.3f, 2.2f, Sfx.BruteStep);
        }

        void ColSlamLand(Player p)
        {
            p.ColSlams++;
            V2 at = p.Pos + V2.FromAngle(p.ColSlamAng) * ColSlamAhead;
            float reach = ColSlamReach(p), dmg = ColSlamDamage * p.Stats.DmgMult;
            foreach (var e in AllFoes(by: p.Id))
            {
                if (e.Pos.FartherOrAt(at, reach + e.Radius))
                    continue;
                if (!Heavy(e))
                {
                    KnockEnemy(e, AwayFrom(e.Pos, at, V2.FromAngle(p.ColSlamAng)) * ColSlamKnock);
                    Stun(e, ColSlamStun);
                }
                HurtEnemy(e, dmg, p.Id);
                Embrittle(e, p.Id);  // earth: cracked for the next hits (Brittle.cs)
            }
            BroadcastFx(FxKind.TitanSlam, at, new V2(TitanEarth, 0f), ColEarth, reach, TitanCrackTime, Sfx.Shockwave);
        }

        void TickBoulders(float dt)
        {
            for (int i = boulders.Count - 1; i >= 0; i--)
            {
                var b = boulders[i];
                b.T += dt;
                V2 at = BoulderAt(b.From, b.Ang, b.T);
                foreach (var e in AllFoes(by: b.Owner))
                {
                    if (b.Hit.Contains(e.Id) || e.Pos.FartherOrAt(at, ColBoulderRadius + e.Radius))
                        continue;
                    b.Hit.Add(e.Id);
                    if (!Heavy(e))
                        KnockEnemy(e, V2.FromAngle(b.Ang) * ColBoulderKnock);
                    HurtEnemy(e, b.Dmg, b.Owner);
                    Embrittle(e, b.Owner);  // earth: cracked for the next hits (Brittle.cs)
                }
                if (b.T >= ColRollTime)
                    boulders.RemoveAt(i);
                else
                    boulders[i] = b;
            }
        }

        // --- Network: the player's Stone Colossus fields (SyncPlayer) ---

        void SyncStoneColossus(NetSync s, Player p)
        {
            s.Float(ref p.ColT);  // Stone Colossus
            s.Float(ref p.ColAge);
            s.Float(ref p.ColWind);
        }
    }
}
