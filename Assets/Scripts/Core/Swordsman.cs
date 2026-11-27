using System;

namespace Arena
{
    /// The sword cut (Docs/Mockups/swordsman.html; drawn in Game/SwordsmanLook.cs), today the Twin Reapers' Acolyte's sickle
    /// (ReaperFaction.cs): a melee enemy that never fires: it walks up, raises its blade and cuts across a cone in front of
    /// it. The cone drawn on the floor is exactly what the blade covers: a curved band from its inner edge (the hand) out to
    /// its reach (the tip), its half-angle either side of where it faces. It stops turning once it starts to wind up, so you
    /// can sidestep or get out of the cone.
    ///   Phase 0 closes in (turns at SwordTurn rad/s), 1 winds up (PhaseT: its wind-up), 2 cuts (PhaseT: SwordCut; the
    ///   damage lands as it starts), 3 recovers (PhaseT: SwordRecover).
    /// A non-shooter (NonShooters.cs): PhaseT and T2 reach clients in the snapshot, and clients time the swing from the age
    /// of the phase they see (Enemy.SeenT).
    public partial class Game
    {
        public const float SwordReach = 140f, SwordInner = 18f, SwordHalf = MathX.Pi / 4f, SwordTurn = 2.8f,
            SwordWind = 0.55f, SwordCut = 0.15f, SwordRecover = 0.8f, SwordDamage = 14f;

        // The Acolyte (the Twin Reapers' cult, ReaperFaction.cs) swings a sickle: shorter and wider, a quicker wind-up.
        public const float SickleReach = 96f, SickleInner = 14f, SickleHalf = MathX.Pi / 3f, SickleWind = 0.5f, SickleDamage = 12f;
        public static float SwordReachOf(EnemyType t) => t == EnemyType.Acolyte ? SickleReach : SwordReach;
        public static float SwordInnerOf(EnemyType t) => t == EnemyType.Acolyte ? SickleInner : SwordInner;
        public static float SwordHalfOf(EnemyType t) => t == EnemyType.Acolyte ? SickleHalf : SwordHalf;
        public static float SwordWindOf(EnemyType t) => t == EnemyType.Acolyte ? SickleWind : SwordWind;
        public static float SwordDamageOf(EnemyType t) => t == EnemyType.Acolyte ? SickleDamage : SwordDamage;

        public static bool IsSwordsman(EnemyType t) => BrainHas(t, BrainRole.Swordsman) ?? t == EnemyType.Acolyte;  // (the Acolyte: a shorter, wider sickle, ReaperFaction.cs)

        /// How long its recovery lasts.
        public static float SwordRecoverOf(EnemyType t) => SwordRecover;

        /// Is a player of radius `hitR` at `ppos` inside the blade's cone? (The band between the hand and the tip.)
        static bool SwordCovers(Enemy e, V2 ppos, float hitR)
        {
            V2 rel = ppos - e.Pos;
            float d = rel.Length;
            if (d > SwordReachOf(e.Type) + hitR)
                return false;
            float off = Math.Abs(MathX.WrapPi(rel.Angle - e.Facing));
            float slack = d > hitR ? (float)Math.Asin(Math.Min(1f, hitR / d)) : MathX.Pi;  // its body counts, not just its centre
            return off <= SwordHalfOf(e.Type) + slack;
        }

        /// It closes in until its target is at 70% of the blade's reach, then stands and waits to be in the cone.
        V2 SwordsmanMove(Enemy e, V2 dir, float dist, ref float speed)
        {
            if (e.Phase != 0)
            {
                speed = 0f;
                return V2.Zero;
            }
            return dist > SwordReachOf(e.Type) * 0.7f ? dir : V2.Zero;
        }

        /// It turns toward its target at SwordTurn rad/s (EnemyAI.cs Face); once it winds up the aim is locked (SwordsmanMovePart, NonShooters.cs).
        void SwordsmanTurn(Enemy e, float want, float dt)
        {
            float diff = MathX.WrapPi(want - e.Facing);
            e.Facing += Math.Max(-SwordTurn * dt, Math.Min(SwordTurn * dt, diff));
        }

        void SwordsmanSpecial(Enemy e, V2 tpos, float dt)
        {
            if (e.SpawnT > 0f)
                return;
            switch (e.Phase)
            {
                case 0:
                {
                    float dist = tpos.DistanceTo(e.Pos);
                    float off = Math.Abs(MathX.WrapPi((tpos - e.Pos).Angle - e.Facing));
                    if (dist <= SwordReachOf(e.Type) * 0.95f && off <= SwordHalfOf(e.Type))
                    {
                        e.Phase = 1;
                        e.PhaseT = SwordWindOf(e.Type);
                    }
                    break;
                }
                case 1:
                    e.PhaseT -= dt;
                    if (e.PhaseT <= 0f)
                    {
                        // The cut: everyone in the cone, once.
                        foreach (var p in Players.Values)
                        {
                            if (!Hittable(p) || !SwordCovers(e, p.Pos, p.Def.HitR))
                                continue;
                            if ((p.ParryActive || p.BbBrace) && ParryStrike(p, e, SwordDamageOf(e.Type)))
                                continue;  // a raised guard takes it
                            Damage(p, SwordDamageOf(e.Type), HitBy(e.Def, "sword"));
                            RodTouched(p, e);  // LightningRod.cs
                        }
                        BroadcastFx(FxKind.Spark, e.Pos + V2.FromAngle(e.Facing) * (SwordReachOf(e.Type) * 0.6f), new Rgba(1f, 0.93f, 0.8f), 14f, 0.15f, Sfx.SwingHeavy);
                        e.Phase = 2;
                        e.PhaseT = SwordCut;
                    }
                    break;
                case 2:
                    e.PhaseT -= dt;
                    if (e.PhaseT <= 0f)
                    {
                        e.Phase = 3;
                        e.PhaseT = SwordRecoverOf(e.Type);
                    }
                    break;
                case 3:
                    e.PhaseT -= dt;
                    if (e.PhaseT <= 0f)
                        e.Phase = 0;
                    break;
            }
        }
    }
}
