using System;

namespace Arena
{
    /// The Swordsman and the Shield Swordsman (Docs/Mockups/swordsman.html; drawn in Game/SwordsmanLook.cs). Melee enemies that
    /// never fire: they walk up, raise a long sword and cut across a cone in front of them. The cone drawn on the floor is
    /// exactly what the blade covers: a curved band from SwordInner (the hand) out to SwordReach (the tip), SwordHalf either
    /// side of where it faces. It stops turning once it starts to wind up, so you can sidestep or get out of the cone.
    ///   Phase 0 closes in (turns at SwordTurn rad/s), 1 winds up (PhaseT: SwordWind), 2 cuts (PhaseT: SwordCut; the damage
    ///   lands as it starts), 3 recovers (PhaseT: SwordRecover, or ShieldRecover for the Shield Swordsman).
    ///   The Shield Swordsman holds a shield up in front (GuardHalf either side) that stops every shot from the front while
    ///   it is up: up while it closes in and winds up, down for the cut and for the recovery bar its last SwordGuardRaise s.
    ///   T2 is the shield's block flash (SwordBlockFlash s, drawn as a bright plate).
    /// Both are non-shooters (NonShooters.cs): PhaseT and T2 reach clients in the snapshot, and clients time the swing from
    /// the age of the phase they see (Enemy.SeenT).
    public partial class Game
    {
        public const float SwordReach = 140f, SwordInner = 18f, SwordHalf = MathX.Pi / 4f, SwordTurn = 2.8f,
            SwordWind = 0.55f, SwordCut = 0.15f, SwordRecover = 0.8f, ShieldRecover = 1f, SwordDamage = 14f,
            GuardHalf = 1.1f, SwordGuardDrop = 0.06f, SwordGuardRaise = 0.25f, SwordBlockFlash = 0.3f;

        public const int SwordsmanFrom = 7, ShieldSwordsmanFrom = 10;  // the waves they join

        public static bool IsSwordsman(EnemyType t) => t == EnemyType.Swordsman || t == EnemyType.ShieldSwordsman;

        /// How long its recovery lasts.
        public static float SwordRecoverOf(EnemyType t) => t == EnemyType.ShieldSwordsman ? ShieldRecover : SwordRecover;

        /// 1: the shield is up in front, 0: down at its side. `age` is the time into `phase`. Everyone draws and blocks from this.
        public static float SwordGuardAt(EnemyType t, byte phase, float age)
        {
            if (t != EnemyType.ShieldSwordsman)
                return 0f;
            if (phase == 2)
                return 1f - Math.Min(1f, age / SwordGuardDrop);
            if (phase == 3)
            {
                float u = age - (ShieldRecover - SwordGuardRaise);
                if (u <= 0f)
                    return 0f;
                float k = Math.Min(1f, u / SwordGuardRaise);
                return k * k * (3f - 2f * k);
            }
            return 1f;
        }

        /// The host's guard: the time into the phase comes from PhaseT counting down.
        static float SwordGuard(Enemy e)
        {
            float age = e.Phase == 2 ? SwordCut - e.PhaseT : e.Phase == 3 ? ShieldRecover - e.PhaseT : 0f;
            return SwordGuardAt(e.Type, e.Phase, age);
        }

        /// BlocksShot (EnemyAI.cs): the raised shield stops a shot from the front.
        static bool SwordShieldBlocks(Enemy e, V2 from)
        {
            if (e.Type != EnemyType.ShieldSwordsman || SwordGuard(e) <= 0.5f)
                return false;
            float diff = Math.Abs(MathX.Fmod((from - e.Pos).Angle - e.Facing + MathX.Pi, MathX.Tau) - MathX.Pi);
            return diff < GuardHalf + 0.1f;
        }

        /// WearShield (EnemyAI.cs): a blocked hit flashes the plate and sparks (a spark at most every 0.08 s, however many shots).
        void SwordShieldStruck(Enemy e)
        {
            bool fresh = e.T2 < SwordBlockFlash - 0.08f;
            e.T2 = SwordBlockFlash;
            if (fresh)
                BroadcastFx(FxKind.Spark, e.Pos + V2.FromAngle(e.Facing) * (e.Radius + 12f), new Rgba(0.8f, 0.92f, 1f), 18f, 0.25f, Sfx.Clang);
        }

        /// Is a player of radius `hitR` at `ppos` inside the blade's cone? (The band between the hand and the tip.)
        static bool SwordCovers(Enemy e, V2 ppos, float hitR)
        {
            V2 rel = ppos - e.Pos;
            float d = rel.Length;
            if (d > SwordReach + hitR)
                return false;
            float off = Math.Abs(MathX.Fmod(rel.Angle - e.Facing + MathX.Pi, MathX.Tau) - MathX.Pi);
            float slack = d > hitR ? (float)Math.Asin(Math.Min(1f, hitR / d)) : MathX.Pi;  // its body counts, not just its centre
            return off <= SwordHalf + slack;
        }

        /// It closes in until its target is at 70% of the blade's reach, then stands and waits to be in the cone.
        V2 SwordsmanMove(Enemy e, V2 dir, float dist, ref float speed)
        {
            if (e.Phase != 0)
            {
                speed = 0f;
                return V2.Zero;
            }
            return dist > SwordReach * 0.7f ? dir : V2.Zero;
        }

        /// It turns toward its target at SwordTurn rad/s (EnemyAI.cs Face); once it winds up the aim is locked (NonShooterFaceLocked).
        void SwordsmanTurn(Enemy e, float want, float dt)
        {
            float diff = MathX.Fmod(want - e.Facing + MathX.Pi, MathX.Tau) - MathX.Pi;
            e.Facing += Math.Max(-SwordTurn * dt, Math.Min(SwordTurn * dt, diff));
        }

        void SwordsmanSpecial(Enemy e, V2 tpos, float dt)
        {
            if (e.T2 > 0f)
                e.T2 = Math.Max(0f, e.T2 - dt);
            if (e.SpawnT > 0f)
                return;
            switch (e.Phase)
            {
                case 0:
                {
                    float dist = tpos.DistanceTo(e.Pos);
                    float off = Math.Abs(MathX.Fmod((tpos - e.Pos).Angle - e.Facing + MathX.Pi, MathX.Tau) - MathX.Pi);
                    if (dist <= SwordReach * 0.95f && off <= SwordHalf)
                    {
                        e.Phase = 1;
                        e.PhaseT = SwordWind;
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
                            if (p.Downed || p.InvulnT > 0f || !SwordCovers(e, p.Pos, p.Def.HitR))
                                continue;
                            if (p.ParryActive && ParryContact(p, e, SwordDamage))
                                continue;  // a raised guard takes it
                            Damage(p, SwordDamage, e.Def.Name + " (sword)");
                        }
                        BroadcastFx(FxKind.Spark, e.Pos + V2.FromAngle(e.Facing) * (SwordReach * 0.6f), new Rgba(1f, 0.93f, 0.8f), 14f, 0.15f, Sfx.SwingHeavy);
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
