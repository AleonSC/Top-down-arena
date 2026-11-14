using System;
using System.Collections.Generic;

namespace Arena
{
    /// Regular enemies that never fire a bullet (Docs/EnemyRoles.md; looks from Docs/Mockups/non-shooter-enemies.html,
    /// drawn in Game/NonShooterLooks.cs):
    ///   Fuse Runner (Hunter, wave 15): sprints at you, plants its feet within FusePlant px and blows up after FuseCountdown.
    ///     The blast hurts players and enemies. Shot dead during the fuse, it blows up early where it stands.
    ///     Phase 0 runs, 1 fuse (PhaseT counts the fuse down).
    ///   Hookman (Disruptor, wave 17): keeps about HookKeep px off with its squad and spins its harpoon; it aims (a line,
    ///     locked, HookAim s), throws, and a hit yanks you HookYank px toward it (KnockPlayer, no damage). A perfect parry,
    ///     a dodge's invulnerability or a player shot across the cable stops it. A miss leaves it open for HookOpen s
    ///     (+50% damage). Phase 0 spins (PhaseT: until the next aim), 1 aims, 2 throws (T2: how far the harpoon is out),
    ///     3 reels in after a hit, 4 open after a miss.
    ///   Sapper (Zoner, wave 18): circles at mid range planting a Tripwire Pod every SapperEvery s (its oldest goes past
    ///     SapperMax). A pod arms after PodArm s; armed pods within PodWire px of each other are joined by a laser. Stepping
    ///     near an armed pod or crossing a laser triggers it (a PodBeep s beep, both ends of a laser), then it blows up,
    ///     hurting players and enemies. Shooting a pod blows it up at once. Pods last PodLife s and don't count for
    ///     clearing a wave. Pod: T2 is its age, Phase 0 arming, 1 armed, 2 triggered (PhaseT: the beep left), 3 fizzled out.
    /// PhaseT and T2 are sent in the snapshot for these four (SyncNonShooter), so clients can draw the fuse, aim and harpoon.
    public partial class Game
    {
        public const float FusePlant = 70f, FuseCountdown = 0.8f, FuseBlast = 90f, FuseDamage = 35f, FuseEnemyDamage = 40f;
        public const float HookKeep = 260f, HookEvery = 4f, HookAim = 0.5f, HookReach = 320f, HookSpeed = 1100f, HookYank = 220f,
            HookOpen = 1f, HookOpenMult = 1.5f, HookReel = 0.35f, HookCutReach = 9f;
        public const float SapperEvery = 1.6f, SapperKeep = 240f;
        public const int SapperMax = 4;
        public const float PodArm = 1f, PodTrigger = 40f, PodWire = 180f, PodBeep = 0.3f, PodLife = 12f,
            PodBlast = 70f, PodDamage = 28f, PodEnemyDamage = 30f;

        public static bool IsNonShooter(EnemyType t) => t == EnemyType.FuseRunner || IsHook(t) || t == EnemyType.Sapper || t == EnemyType.SapperMine || IsSwordsman(t);

        /// A Hookman that just missed takes more damage (Combat.cs HurtEnemy).
        static bool HookIsOpen(Enemy e) => IsHook(e.Type) && e.Phase == 4;

        /// The Hookman, and the Devourer's Hook Thrower (LarderFaction.cs), which throws a meat hook the same way.
        public static bool IsHook(EnemyType t) => BrainHas(t, BrainRole.Hook) ?? (t == EnemyType.Hookman || t == EnemyType.HookThrower);

        // --- Brains (Docs/EnemyBehaviours.md step 8c): each non-shooter's move, locked aim and special --------------------

        /// Straight at you, fast; planted (and its aim locked) once it lights its fuse.
        sealed class FuseMove : MovePart
        {
            public override V2 Move(Game g, Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed)
            {
                if (e.Phase == 0)
                    return dir;
                speed = 0f;
                return V2.Zero;
            }

            public override bool Turn(Game g, Enemy e, float want, V2 tpos, float dt) => e.Phase == 1;
        }

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

        /// Circles round you.
        sealed class SapperMove : MovePart
        {
            public override V2 Move(Game g, Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed) =>
                KeepDistance(dir, dist, SapperKeep) * 0.7f + dir.Orthogonal() * e.Strafe;
        }

        /// Doesn't move or turn (a Sapper's Tripwire Pod).
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

        /// A Shield Swordsman's shield: blocks from the front (SwordShieldBlocks); a blocked hit flashes it, never wears it.
        sealed class SwordShieldGuard : GuardPart
        {
            public override bool Blocks(Game g, Enemy e, V2 from) => SwordShieldBlocks(e, from);
            public override void Wear(Game g, Enemy e, float amount) => g.SwordShieldStruck(e);
        }

        static void NonShooterBrains(Brain[] t)
        {
            t[(int)EnemyType.FuseRunner] = new Brain { Move = new FuseMove(), Special = new CodeSpecial((g, e, p, dt) => g.FuseSpecial(e, p, dt)) };
            var hook = new Brain { Move = new HookMove(), Special = new CodeSpecial((g, e, p, dt) => g.HookSpecial(e, p, dt)), Roles = BrainRole.Hook };
            t[(int)EnemyType.Hookman] = hook;
            t[(int)EnemyType.HookThrower] = hook;
            t[(int)EnemyType.Sapper] = new Brain { Move = new SapperMove(), Special = new CodeSpecial((g, e, p, dt) => g.SapperSpecial(e, dt)) };
            t[(int)EnemyType.SapperMine] = new Brain { Move = new StillMove(), Special = new CodeSpecial((g, e, p, dt) => g.PodSpecial(e, dt)) };
            var sword = new SwordsmanMovePart();
            var swordSpecial = new CodeSpecial((g, e, p, dt) => g.SwordsmanSpecial(e, p, dt));
            t[(int)EnemyType.Swordsman] = new Brain { Move = sword, Special = swordSpecial, Roles = BrainRole.Swordsman };
            t[(int)EnemyType.ShieldSwordsman] = new Brain { Move = sword, Special = swordSpecial, Guard = new SwordShieldGuard(), Roles = BrainRole.Swordsman };
            t[(int)EnemyType.Acolyte] = new Brain { Move = sword, Special = swordSpecial, Roles = BrainRole.Swordsman };
        }

        // --- Fuse Runner ---------------------------------------------------------------------------------------------

        void FuseSpecial(Enemy e, V2 tpos, float dt)
        {
            if (e.Phase == 0)
            {
                if (tpos.DistanceTo(e.Pos) < FusePlant && e.SpawnT <= 0f)
                {
                    e.Phase = 1;
                    e.PhaseT = FuseCountdown;
                    e.Facing = (tpos - e.Pos).Angle;
                    BroadcastFx(FxKind.Target, e.Pos, e.Def.Color, FuseBlast, FuseCountdown, Sfx.None);
                }
                return;
            }
            e.PhaseT -= dt;
            if (e.PhaseT <= 0f)
            {
                e.LastHitBy = 0;  // no kill credit for blowing itself up
                e.Hp = 0f;        // NonShooterDeath blows it up
            }
        }

        /// A blast that hurts players and enemies: the Fuse Runner's and a Tripwire Pod's.
        void NonShooterBlast(Enemy e, float radius, float playerDamage, float enemyDamage, int credit)
        {
            foreach (var p in Players.Values)
            {
                if (p.Downed || p.InvulnT > 0f || p.Pos.DistanceTo(e.Pos) > radius + p.Def.HitR)
                    continue;
                if (p.ParryActive && ParryContact(p, e, playerDamage))
                    continue;  // a raised guard facing the blast takes it
                Damage(p, playerDamage, e.Def.Name);
            }
            float scaled = enemyDamage * (1f + 0.15f * (Math.Max(1, Wave) - 1));  // keeps up with enemy health (SpawnHp)
            foreach (var o in Enemies.Values)
                if (o != e && Targetable(o) && o.Pos.DistanceTo(e.Pos) < radius + o.Radius)
                    HurtEnemy(o, o.Def.IsBoss ? scaled * 0.25f : scaled, credit);
            BroadcastFx(FxKind.Boom, e.Pos, e.Def.Color, radius, 0.35f, Sfx.Explode);
            PropBlast(e.Pos, radius, credit);
        }

        /// Host, as a dead enemy is cleared up (Combat.cs): a Fuse Runner killed during its fuse, or any pod that didn't
        /// fizzle out, blows up. A player's killing shot gets the credit for what the blast hits.
        void NonShooterDeath(Enemy e)
        {
            int credit = Players.ContainsKey(e.LastHitBy) ? e.LastHitBy : 0;
            if (e.Type == EnemyType.FuseRunner && e.Phase == 1)
                NonShooterBlast(e, FuseBlast, FuseDamage, FuseEnemyDamage, credit);
            else if (e.Type == EnemyType.SapperMine && e.Phase != 3)
                NonShooterBlast(e, PodBlast, PodDamage, PodEnemyDamage, credit);
        }

        // --- Hookman -------------------------------------------------------------------------------------------------

        void HookSpecial(Enemy e, V2 tpos, float dt)
        {
            e.PhaseT -= dt;
            switch (e.Phase)
            {
                case 0:  // spinning the harpoon; aims when its time comes and you're in reach
                    if (e.PhaseT <= 0f && e.SpawnT <= 0f && tpos.DistanceTo(e.Pos) < HookReach * 0.95f)
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
                        if (p.ParryActive && ParryContact(p, e, 0f))
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
                if (b.Owner > 0 && !Defs.IsEnemyShot(b.Kind) && SegDistance(b.Pos, e.Pos, tip) < HookCutReach + b.Radius && b.Pos.DistanceTo(e.Pos) > e.Radius + 4f)
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

        // --- Sapper and its Tripwire Pods ----------------------------------------------------------------------------

        readonly List<Enemy> podScratch = new List<Enemy>();

        void SapperSpecial(Enemy e, float dt)
        {
            e.PhaseT -= dt;
            if (e.PhaseT > 0f || e.SpawnT > 0f)
                return;
            e.PhaseT = SapperEvery;
            podScratch.Clear();
            foreach (var o in Enemies.Values)
                if (o.Type == EnemyType.SapperMine && o.ParentId == e.Id && o.Hp > 0f && o.Phase < 2)
                    podScratch.Add(o);
            if (podScratch.Count >= SapperMax)
            {
                Enemy oldest = podScratch[0];
                foreach (var o in podScratch)
                    if (o.T2 > oldest.T2)
                        oldest = o;
                Fizzle(oldest);
            }
            var pod = SpawnEnemyAt(EnemyType.SapperMine, e.Pos, 0f, e.Id);
            pod.Hp = pod.MaxHp = 1f;
            pod.Elite = false;  // (a path or a modifier can't make a pod elite)
            pod.Traits = 0;
            pod.FireT = 99f;
            pod.Speed = 0f;
        }

        void Fizzle(Enemy pod)
        {
            pod.Phase = 3;
            pod.LastHitBy = 0;
            pod.Hp = 0f;
        }

        static bool PodArmed(Enemy o) => o.Type == EnemyType.SapperMine && o.Phase == 1 && o.Hp > 0f;

        void PodSpecial(Enemy e, float dt)
        {
            e.T2 += dt;
            if (e.Phase == 0 && e.T2 >= PodArm)
                e.Phase = 1;
            if (e.Phase == 1)
            {
                if (e.T2 >= PodLife)
                {
                    Fizzle(e);
                    return;
                }
                foreach (var p in Players.Values)
                    if (!p.Downed && p.Pos.DistanceTo(e.Pos) < PodTrigger + p.Def.HitR)
                    {
                        TriggerPod(e);
                        break;
                    }
                if (e.Phase == 1)
                {
                    // Lasers: each pod checks the ones it leads (lower id), so every wire is checked once.
                    foreach (var o in Enemies.Values)
                    {
                        if (o == e || o.Id < e.Id || !PodArmed(o) || o.Pos.DistanceTo(e.Pos) >= PodWire)
                            continue;
                        foreach (var p in Players.Values)
                            if (!p.Downed && p.InvulnT <= 0f && SegDistance(p.Pos, e.Pos, o.Pos) < p.Def.HitR + 4f)
                            {
                                TriggerPod(e);
                                TriggerPod(o);
                                break;
                            }
                        if (e.Phase != 1)
                            break;
                    }
                }
            }
            else if (e.Phase == 2)
            {
                e.PhaseT -= dt;
                if (e.PhaseT <= 0f)
                {
                    e.LastHitBy = 0;
                    e.Hp = 0f;  // NonShooterDeath blows it up
                }
            }
        }

        void TriggerPod(Enemy pod)
        {
            if (pod.Phase >= 2)
                return;
            pod.Phase = 2;
            pod.PhaseT = PodBeep;
            BroadcastFx(FxKind.Target, pod.Pos, pod.Def.Color, PodBlast, PodBeep, Sfx.None);
        }

        /// A wave is cleared without its pods: the ones left fizzle out (Waves.cs WaveCleared).
        void ClearPods()
        {
            var gone = new List<int>();
            foreach (var o in Enemies.Values)
                if (o.Type == EnemyType.SapperMine)
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
