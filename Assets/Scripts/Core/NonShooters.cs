using System;
using System.Collections.Generic;

namespace Arena
{
    /// Enemies that never fire a bullet (Docs/EnemyRoles.md; drawn in Game/NonShooterLooks.cs): the hook throwers, the
    /// sword cutters (Swordsman.cs) and the Turnkey's kin (Turnkey.cs).
    ///   The hook (the Devourer's Hook Thrower, LarderFaction.cs): keeps about HookKeep px off with its squad; it aims (a
    ///     line, locked), throws, and a hit yanks you toward it (KnockPlayer). A perfect parry, a dodge's invulnerability or
    ///     a player shot across the cable stops it. A miss leaves it open for HookOpen s (+50% damage). Phase 0 waits
    ///     (PhaseT: until the next aim), 1 aims, 2 throws (T2: how far the hook is out), 3 reels in after a hit, 4 open
    ///     after a miss.
    /// PhaseT and T2 are sent in the snapshot for these (SyncNonShooter), so clients can draw the aim and the hook.
    public partial class Game
    {
        public const float HookKeep = 260f, HookEvery = 4f, HookAim = 0.5f, HookReach = 320f, HookSpeed = 1100f, HookYank = 220f,
            HookOpen = 1f, HookOpenMult = 1.5f, HookReel = 0.35f, HookCutReach = 9f;

        public static bool IsNonShooter(EnemyType t) => IsHook(t) || IsSwordsman(t) || IsTurnkeyKin(t);  // (the Turnkey's too: Turnkey.cs)

        /// A hook thrower that just missed takes more damage (Combat.cs HurtEnemy).
        static bool HookIsOpen(Enemy e) => IsHook(e.Type) && e.Phase == 4;

        /// The Devourer's Hook Thrower (LarderFaction.cs), which throws a meat hook.
        public static bool IsHook(EnemyType t) => BrainHas(t, BrainRole.Hook) ?? t == EnemyType.HookThrower;

        // --- Brains (Docs/EnemyBehaviours.md step 8c): each non-shooter's move, locked aim and special --------------------

        /// Keeps HookKeep away; stands to throw, its aim locked from the aim to the end of the throw.
        sealed class HookMove : MovePart
        {
            public override V2 Move(Game g, Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed)
            {
                if (e.Phase != 0)
                {
                    speed = 0f;  // stands to throw
                    return V2.Zero;
                }
                return KeepDistance(dir, dist, HookKeep) + dir.Orthogonal() * e.Strafe * 0.4f;
            }

            public override bool Turn(Game g, Enemy e, float want, V2 tpos, float dt) => e.Phase >= 1 && e.Phase <= 3;
        }

        /// Doesn't move or turn (the Turnkey's traps and doors, Turnkey.cs).
        sealed class StillMove : MovePart
        {
            public override V2 Move(Game g, Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed)
            {
                speed = 0f;
                return V2.Zero;
            }

            public override bool Turn(Game g, Enemy e, float want, V2 tpos, float dt) => true;
        }

        /// SwordsmanMove (Swordsman.cs); its aim is locked from its wind-up to the end of its recovery, otherwise it turns
        /// at its own pace (SwordsmanTurn).
        sealed class SwordsmanMovePart : MovePart
        {
            public override V2 Move(Game g, Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed) => g.SwordsmanMove(e, dir, dist, ref speed);

            public override bool Turn(Game g, Enemy e, float want, V2 tpos, float dt)
            {
                if (e.Phase == 0)
                    g.SwordsmanTurn(e, want, dt);
                return true;
            }
        }

        static void NonShooterBrains(Brain[] t)
        {
            t[(int)EnemyType.HookThrower] = new Brain { Move = new HookMove(), Special = new CodeSpecial((g, e, p, dt) => g.HookSpecial(e, p, dt)), Roles = BrainRole.Hook };
            var sword = new SwordsmanMovePart();
            var swordSpecial = new CodeSpecial((g, e, p, dt) => g.SwordsmanSpecial(e, p, dt));
            t[(int)EnemyType.Acolyte] = new Brain { Move = sword, Special = swordSpecial, Roles = BrainRole.Swordsman };
        }

        /// A blast that hurts players and enemies: a Floor Mine's (Turnkey.cs).
        void NonShooterBlast(Enemy e, float radius, float playerDamage, float enemyDamage, int credit)
        {
            foreach (var p in Players.Values)
            {
                if (p.Downed || p.InvulnT > 0f || p.Pos.FartherThan(e.Pos, radius + p.Def.HitR))
                    continue;
                if ((p.ParryActive || p.BbBrace) && ParryContact(p, e, playerDamage))
                    continue;  // a raised guard facing the blast takes it
                Damage(p, playerDamage, e.Def.Name);
            }
            float scaled = enemyDamage * (1f + 0.15f * (Math.Max(1, Wave) - 1));  // keeps up with enemy health (SpawnHp)
            foreach (var o in Enemies.Values)
                if (o != e && Targetable(o) && o.Pos.CloserThan(e.Pos, radius + o.Radius))
                    HurtEnemy(o, o.Def.IsBoss ? scaled * 0.25f : scaled, credit);
            BroadcastFx(FxKind.Boom, e.Pos, e.Def.Color, radius, 0.35f, Sfx.Explode);
            PropBlast(e.Pos, radius, credit);
        }

        /// Host, as a dead enemy is cleared up (Combat.cs): a Floor Mine that didn't fizzle out blows up (Turnkey.cs).
        void NonShooterDeath(Enemy e) => TurnkeyDeath(e);

        // --- The hook -------------------------------------------------------------------------------------------------

        void HookSpecial(Enemy e, V2 tpos, float dt)
        {
            e.PhaseT -= dt;
            switch (e.Phase)
            {
                case 0:  // spinning the harpoon; aims when its time comes and you're in reach
                    if (e.PhaseT <= 0f && e.SpawnT <= 0f && tpos.CloserThan(e.Pos, HookReach * 0.95f))
                    {
                        e.Phase = 1;
                        e.PhaseT = e.Type == EnemyType.HookThrower ? HookThrowAim : HookAim;
                        e.Facing = (tpos - e.Pos).Angle;  // locked from here: step off the line
                    }
                    break;
                case 1:  // the line shows
                    if (e.PhaseT <= 0f)
                    {
                        e.Phase = 2;
                        e.T2 = e.Radius;
                    }
                    break;
                case 2:  // thrown
                {
                    float before = e.T2;
                    e.T2 = Math.Min(HookReach, e.T2 + (e.Type == EnemyType.HookThrower ? HookThrowSpeed : HookSpeed) * dt);
                    V2 dir = V2.FromAngle(e.Facing), tip = e.Pos + dir * e.T2;
                    if (HookCut(e, tip))
                    {
                        BroadcastFx(FxKind.Spark, e.Pos + dir * (e.T2 * 0.5f), e.Def.Color, 18f, 0.25f, Sfx.Clang);
                        HookMissed(e);
                        break;
                    }
                    foreach (var p in Players.Values)
                    {
                        if (p.Downed || p.SwallowT > 0f)
                            continue;
                        // Swept: anyone the harpoon passed this tick, not only where it ended.
                        float d = SegDistance(p.Pos, e.Pos + dir * before, tip);
                        if (d > p.Def.BodyR + 6f)
                            continue;
                        if (p.InvulnT > 0f)
                            continue;  // a dodge's invulnerability: it passes through
                        if ((p.ParryActive || p.BbBrace) && ParryContact(p, e, 0f))
                        {
                            BroadcastFx(FxKind.Spark, p.Pos, new Rgba(0.85f, 0.9f, 1f), 24f, 0.25f, Sfx.Clang);
                            HookMissed(e);
                            return;
                        }
                        V2 to = e.Pos - p.Pos;
                        float pull = Math.Min(e.Type == EnemyType.HookThrower ? HookThrowYank : HookYank, Math.Max(0f, to.Length - e.Radius - p.Def.BodyR - 30f));
                        KnockPlayer(p, to, pull);
                        if (e.Type == EnemyType.HookThrower)
                            Damage(p, HookThrowDamage, "Hook Thrower");  // a meat hook hurts (LarderFaction.cs)
                        p.BashGraceT = Math.Max(p.BashGraceT, 0.6f);
                        BroadcastFx(FxKind.Spark, p.Pos, e.Def.Color, 26f, 0.3f, Sfx.Clang);
                        e.Phase = 3;
                        e.PhaseT = HookReel;
                        return;
                    }
                    if (e.T2 >= HookReach)
                        HookMissed(e);
                    break;
                }
                case 3:  // reeling in after a hit
                    e.T2 = Math.Max(0f, e.T2 - HookReach / HookReel * dt);
                    if (e.PhaseT <= 0f)
                        HookReady(e);
                    break;
                case 4:  // missed: open, reeling the harpoon back
                    e.T2 = Math.Max(0f, e.T2 - HookReach / HookReel * dt);
                    if (e.PhaseT <= 0f)
                        HookReady(e);
                    break;
            }
        }

        void HookMissed(Enemy e)
        {
            e.Phase = 4;
            e.PhaseT = HookOpen;
        }

        void HookReady(Enemy e)
        {
            e.Phase = 0;
            e.T2 = 0f;
            e.PhaseT = HookEvery - HookAim - HookOpen * 0.5f;
        }

        /// A player's shot crossing the cable cuts it (it's the shot's own hit test; the shot flies on).
        bool HookCut(Enemy e, V2 tip)
        {
            foreach (var b in Bullets.Values)
                if (b.Owner > 0 && !Defs.IsEnemyShot(b.Kind) && SegDistance(b.Pos, e.Pos, tip) < HookCutReach + b.Radius && b.Pos.FartherThan(e.Pos, e.Radius + 4f))
                    return true;
            return false;
        }

        static float SegDistance(V2 p, V2 a, V2 b)
        {
            V2 ab = b - a;
            float l = ab.LengthSq;
            float t = l > 0f ? Math.Max(0f, Math.Min(1f, (p - a).Dot(ab) / l)) : 0f;
            return p.DistanceTo(a + ab * t);
        }

        void Fizzle(Enemy pod)
        {
            pod.Phase = 3;
            pod.LastHitBy = 0;
            pod.Hp = 0f;
        }

        /// A wave is cleared without a Turnkey's traps and doors: the ones left go (Waves.cs WaveCleared).
        void ClearPods()
        {
            var gone = new List<int>();
            foreach (var o in Enemies.Values)
                if (IsPlanted(o.Type))
                    gone.Add(o.Id);
            foreach (int id in gone)
                Enemies.Remove(id);
        }

        /// Snapshot: the timers clients draw from (GameNet.cs SyncEnemy).
        void SyncNonShooter(NetSync s, Enemy e)
        {
            s.Float(ref e.PhaseT);
            s.Float(ref e.T2);
        }
    }
}
