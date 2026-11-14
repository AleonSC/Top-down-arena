using System;

namespace Arena
{
    /// Step 8a of the enemy behaviour classes (Docs/EnemyBehaviours.md): the basic faction and the plain early enemies.
    /// Each one's code moved here unchanged from EnemyAI.cs's type switches; its parts call it.
    /// Shooters that keep a distance (KeepMove: Grunt, Sniper, Burster, Mortar, Shielder, Medic, Splitter), the Hive Queen's
    /// Shard drones, the Charger (wind-up, dash, ring), the Spinner (wanders, bursts), and the melee Spiker, Buzzsaw and
    /// Spike Block (MeleeGrunts.cs). The Shielder's shield is a guard part (ShielderGuard).
    public partial class Game
    {
        // --- the moved code ----------------------------------------------------------------------------------------------

        void GruntFire(Enemy e, V2 tpos, float aim)
        {
            e.FireT = 1.8f;
            for (int i = 0; i < 3; i++)
                EnemyShot(e, aim + (i - 1) * 0.22f, 175f, Defs.SmallShot);
        }

        void BursterFire(Enemy e, V2 tpos, float aim)
        {
            e.FireT = 2.4f;
            int n = 8 + Math.Min(Wave / 2, 4);  // the ring grows up to 12 bullets (wave 8), with gaps to slip through (was up to 18 at 135)
            e.Ang += MathX.Pi / n;
            for (int i = 0; i < n; i++)
                EnemyShot(e, e.Ang + MathX.Tau * i / n, 115f, Defs.SmallShot);
        }

        void SniperFire(Enemy e, V2 tpos, float aim)
        {
            e.FireT = 2.6f;
            EnemyShot(e, aim, 400f, Defs.FastShot);
        }

        void SpinnerFire(Enemy e, V2 tpos, float aim)
        {
            // Bursts: a three-armed spiral for a moment, then a breather. Phase 1 = bursting.
            float c = MathX.Fmod(e.T + e.Id * 0.7f, SpinnerCycle);
            if (c >= SpinnerBurst)
            {
                e.Phase = 0;
                e.FireT = SpinnerCycle - c;  // wait for the next burst
                return;
            }
            e.Phase = 1;
            e.FireT = 0.11f;
            e.Ang += 0.27f;
            for (int k = 0; k < 3; k++)
                EnemyShot(e, e.Ang + MathX.Tau * k / 3f, 125f, Defs.SmallShot, 0.25f);
        }

        void ChargerAim(Enemy e, V2 tpos, float aim)
        {
            if (e.Phase == 0)
            {
                e.Phase = 1;      // start winding up toward the target
                e.PhaseT = 0.7f;
                e.Facing = aim;
                BroadcastFx(FxKind.Target, ClampToArena(e.Pos + V2.FromAngle(aim) * (ChargerDashSpeed * ChargerDashTime), e.Radius), e.Def.Color, ChargerRingMark, 0.7f + ChargerDashTime, Sfx.None);  // where its ring will burst, from the start of the wind-up
            }
            e.FireT = 99f;        // the dash cycle resets this
        }

        void SplitterFire(Enemy e, V2 tpos, float aim)
        {
            e.FireT = 2f;
            EnemyShot(e, aim - 0.08f, 150f, Defs.SmallShot);
            EnemyShot(e, aim + 0.08f, 150f, Defs.SmallShot);
        }

        void ShardFire(Enemy e, V2 tpos, float aim)
        {
            e.FireT = 1.6f;
            EnemyShot(e, aim, 260f, Defs.FastShot);
        }

        void MortarFire(Enemy e, V2 tpos, float aim)
        {
            // Lob a bomb at where the target is now; it bursts into a ring when it lands.
            e.FireT = 3f;
            const float flight = 1.3f;
            V2 vel = ((tpos - e.Pos) / flight).LimitLength(330f);
            var bomb = SpawnBullet(Defs.MineShot, e.Pos + V2.FromAngle(aim) * e.Radius, vel, 0f, flight, Defs.BulletDamage(Defs.MineShot), 0);
            bomb.Burst = e.Elite ? 10 : 7;  // was 14 / 10
            bomb.Source = e.Type;
            BroadcastFx(FxKind.Target, e.Pos + vel * flight, e.Def.Color, MortarMarkRadius, flight, Sfx.None);  // where it lands (the same ring the gas and the bombers use)
        }

        void ShielderFire(Enemy e, V2 tpos, float aim)
        {
            e.FireT = 2.2f;
            for (int i = 0; i < 5; i++)
                EnemyShot(e, e.Facing + (i - 2) * 0.15f, 190f, Defs.SmallShot);
        }

        void MedicFire(Enemy e, V2 tpos, float aim)
        {
            e.FireT = 2.5f;
            EnemyShot(e, aim, 160f, Defs.SmallShot);
        }

        void ShielderDecay(Enemy e, V2 tpos, float dt)
        {
            WearShield(e, e.ShieldMax * ShieldDecay * dt);
        }

        void ChargerDash(Enemy e, V2 tpos, float dt)
        {
            // Phase 1: wind up (telegraph line); phase 2: dash, then burst.
            if (e.Phase == 0)
                return;
            e.PhaseT -= dt;
            if (e.PhaseT > 0f)
                return;
            if (e.Phase == 1)
            {
                e.Phase = 2;
                e.PhaseT = ChargerDashTime;
            }
            else
            {
                e.Phase = 0;
                e.FireT = 3.2f;
                for (int i = 0; i < 8; i++)  // its ring: 8, slow enough to step through (was 10 at 140)
                    EnemyShot(e, e.Facing + MathX.Tau * i / 8f, 120f, Defs.SmallShot);
            }
        }

        void MedicHeal(Enemy e, V2 tpos, float dt)
        {
            e.PhaseT -= dt;
            if (e.PhaseT <= 0f)
            {
                e.PhaseT = 3f;
                foreach (var o in Enemies.Values)
                    if (o != e && !o.Def.IsBoss && o.Pos.DistanceTo(e.Pos) < 180f)
                        o.Hp = Math.Min(o.MaxHp, o.Hp + o.MaxHp * 0.15f);
                BroadcastFx(FxKind.Ring, e.Pos, e.Def.Color, 180f, 0.5f, Sfx.None);
            }
        }

        /// A Shielder's shield: a hit it stopped (or time) wears it down; it cracks in three steps, then shatters.
        void ShielderWear(Enemy e, float amount)
        {
            if (e.Phase >= ShieldBroken || e.ShieldMax <= 0f)
                return;
            e.ShieldHp -= amount;
            float frac = e.ShieldHp / e.ShieldMax;
            byte phase = (byte)(frac <= 0f ? ShieldBroken : frac <= 0.25f ? 3 : frac <= 0.5f ? 2 : frac <= 0.75f ? 1 : 0);
            if (phase == e.Phase)
                return;
            e.Phase = phase;
            if (phase == ShieldBroken)
                BroadcastFx(FxKind.Shatter, e.Pos, e.Pos + V2.FromAngle(e.Facing) * (e.Radius + 7f), new Rgba(0.7f, 0.85f, 1f), ShieldHalfAngle, 0.6f, Sfx.Clang);
            else
                BroadcastFx(FxKind.Spark, e.Pos + V2.FromAngle(e.Facing) * (e.Radius + 7f), new Rgba(0.8f, 0.9f, 1f), 16f, 0.25f, Sfx.Hit);

        }

        /// Wanders between random spots, round walls and crates; gives up on one it can't reach.
        V2 SpinnerWander(Enemy e, float dt)
        {
            e.PhaseT -= dt;
            if (e.Pos.DistanceTo(e.Waypoint) < 20f || e.PhaseT <= 0f)
            {
                e.Waypoint = WanderSpot(Walled ? 80f : 150f, e.Radius + 12f);
                e.PhaseT = e.Pos.DistanceTo(e.Waypoint) / Math.Max(1f, e.Speed) * 1.6f + 4f;
            }
            return RouteTo(e, e.Waypoint);  // LayoutRules.cs
        }

        // --- the parts ----------------------------------------------------------------------------------------------------

        /// Keeps Range away; with a strafe, sidles round you at Strafe.
        sealed class KeepMove : MovePart
        {
            readonly float range, strafe, contact;
            readonly bool strafes, slowTurn;
            public KeepMove(float range, float strafe = 0f, bool strafes = true, bool slowTurn = false, float contact = -1f)
            {
                this.range = range; this.strafe = strafe; this.strafes = strafes; this.slowTurn = slowTurn; this.contact = contact;
            }

            /// Its touch damage (a Juggernaut's 25); below 0: the usual.
            public override float Contact(Game g, Enemy e) => contact;

            public override V2 Move(Game g, Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed) =>
                strafes ? KeepDistance(dir, dist, range) + dir.Orthogonal() * e.Strafe * strafe : KeepDistance(dir, dist, range);

            /// A Shielder turns slowly, so you can get round its shield.
            public override bool Turn(Game g, Enemy e, float want, V2 tpos, float dt)
            {
                if (!slowTurn)
                    return false;
                float diff = MathX.Fmod(want - e.Facing + MathX.Pi, MathX.Tau) - MathX.Pi;
                e.Facing += Math.Max(-1.6f * dt, Math.Min(1.6f * dt, diff));
                return true;
            }
        }

        /// The Hive Queen's drones: keep 180 px and strafe; fly back into her when she calls them (Queen.cs).
        sealed class ShardMove : MovePart
        {
            public override V2 Move(Game g, Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed) =>
                g.DroneGoingHome(e) ? g.DroneHomeMove(e, ref speed) : KeepDistance(dir, dist, 180f) + dir.Orthogonal() * e.Strafe;
        }

        /// Keeps 300 px and strafes; a straight dash in Phase 2 (locked in while winding up and dashing; it hurts more).
        sealed class ChargerMove : MovePart
        {
            public override V2 Move(Game g, Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed)
            {
                if (e.Phase == 0)
                    return KeepDistance(dir, dist, 300f) + dir.Orthogonal() * e.Strafe * 0.5f;
                if (e.Phase == 2)
                {
                    speed = ChargerDashSpeed;
                    return V2.FromAngle(e.Facing);
                }
                return V2.Zero;
            }

            public override bool Turn(Game g, Enemy e, float want, V2 tpos, float dt) => e.Phase != 0;
            public override float Contact(Game g, Enemy e) => e.Phase == 2 ? 25f : -1f;
        }

        sealed class SpinnerMove : MovePart
        {
            public override V2 Move(Game g, Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed) => g.SpinnerWander(e, dt);
        }

        /// Heads for you in a zig-zag (MeleeGrunts.cs).
        sealed class SpikerMovePart : MovePart
        {
            public override V2 Move(Game g, Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed) => g.SpikerMove(e, dir, dist, dt);
        }

        /// Swoops in on curves, steering with momentum.
        sealed class SawMove : MovePart
        {
            public override V2 Move(Game g, Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed)
            {
                e.Vel += (dir - e.Vel) * Math.Min(1f, dt * SawTurn);
                return e.Vel;
            }
        }

        /// Rocks from side to side as it closes in; stands still to wind up and recover; a locked lunge in Phase 2.
        sealed class SpikeBlockMove : MovePart
        {
            public override V2 Move(Game g, Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed)
            {
                if (e.Phase == 0)
                    return dir * 0.6f + dir.Orthogonal() * (float)Math.Sin(e.T * 2.6f + e.Id) * 0.8f;
                if (e.Phase == 2)
                {
                    speed = SpikeLungeSpeed;
                    return V2.FromAngle(e.Facing);
                }
                return V2.Zero;
            }

            public override bool Turn(Game g, Enemy e, float want, V2 tpos, float dt) => e.Phase >= 1;
            public override float Contact(Game g, Enemy e) => e.Phase == 2 ? SpikeLungeDamage : -1f;
        }

        delegate V2 MoveFn(Game g, Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed);

        /// A move part that runs a given method; Locked: when its facing stays put (a wind-up's aim, a sweep, a dash).
        sealed class CodeMove : MovePart
        {
            readonly MoveFn move;
            readonly Func<Enemy, bool> locked;
            public CodeMove(MoveFn move, Func<Enemy, bool> locked = null) { this.move = move; this.locked = locked; }
            public override V2 Move(Game g, Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed) => move(g, e, tpos, dir, dist, dt, ref speed);
            public override bool Turn(Game g, Enemy e, float want, V2 tpos, float dt) => locked != null && locked(e);
        }

        /// A fire part that runs one of the methods above.
        sealed class CodeFire : FirePart
        {
            readonly Action<Game, Enemy, V2, float> fire;
            public CodeFire(Action<Game, Enemy, V2, float> fire) { this.fire = fire; }
            public override void Fire(Game g, Enemy e, V2 tpos, float aim) => fire(g, e, tpos, aim);
        }

        /// A special part that runs one of the methods above.
        sealed class CodeSpecial : SpecialPart
        {
            readonly Action<Game, Enemy, V2, float> tick;
            public CodeSpecial(Action<Game, Enemy, V2, float> tick) { this.tick = tick; }
            public override void Tick(Game g, Enemy e, V2 tpos, float dt) => tick(g, e, tpos, dt);
        }

        /// A Shielder's shield: blocks from the front until broken.
        sealed class ShielderGuard : GuardPart
        {
            public override bool Blocks(Game g, Enemy e, V2 from)
            {
                if (e.Phase >= ShieldBroken)
                    return false;
                float diff = Math.Abs(MathX.Fmod((from - e.Pos).Angle - e.Facing + MathX.Pi, MathX.Tau) - MathX.Pi);
                return diff < ShieldHalfAngle;
            }

            public override void Setup(Game g, Enemy e) => e.ShieldHp = e.ShieldMax = e.MaxHp * ShieldShare;
            public override void Wear(Game g, Enemy e, float amount) => g.ShielderWear(e, amount);
        }

        static void BasicBrains(Brain[] t)
        {
            t[(int)EnemyType.Grunt] = new Brain { Move = new KeepMove(230f, 0.6f), Fire = new CodeFire((g, e, p, a) => g.GruntFire(e, p, a)) };
            t[(int)EnemyType.Sniper] = new Brain { Move = new KeepMove(420f, 0.8f), Fire = new CodeFire((g, e, p, a) => g.SniperFire(e, p, a)) };
            t[(int)EnemyType.Burster] = new Brain { Move = new KeepMove(160f, strafes: false), Fire = new CodeFire((g, e, p, a) => g.BursterFire(e, p, a)) };
            t[(int)EnemyType.Mortar] = new Brain { Move = new KeepMove(450f, 0.4f), Fire = new CodeFire((g, e, p, a) => g.MortarFire(e, p, a)) };
            t[(int)EnemyType.Shielder] = new Brain { Move = new KeepMove(200f, strafes: false, slowTurn: true), Fire = new CodeFire((g, e, p, a) => g.ShielderFire(e, p, a)),
                Special = new CodeSpecial((g, e, p, dt) => g.ShielderDecay(e, p, dt)), Guard = new ShielderGuard() };
            t[(int)EnemyType.Medic] = new Brain { Move = new KeepMove(360f, 0.5f), Fire = new CodeFire((g, e, p, a) => g.MedicFire(e, p, a)), Special = new CodeSpecial((g, e, p, dt) => g.MedicHeal(e, p, dt)) };
            t[(int)EnemyType.Splitter] = new Brain { Move = new KeepMove(220f, strafes: false), Fire = new CodeFire((g, e, p, a) => g.SplitterFire(e, p, a)) };
            t[(int)EnemyType.Shard] = new Brain { Move = new ShardMove(), Fire = new CodeFire((g, e, p, a) => g.ShardFire(e, p, a)) };
            t[(int)EnemyType.Charger] = new Brain { Move = new ChargerMove(), Fire = new CodeFire((g, e, p, a) => g.ChargerAim(e, p, a)), Special = new CodeSpecial((g, e, p, dt) => g.ChargerDash(e, p, dt)) };
            t[(int)EnemyType.Spinner] = new Brain { Move = new SpinnerMove(), Fire = new CodeFire((g, e, p, a) => g.SpinnerFire(e, p, a)) };
            t[(int)EnemyType.Spiker] = new Brain { Move = new SpikerMovePart() };
            t[(int)EnemyType.Buzzsaw] = new Brain { Move = new SawMove() };
            t[(int)EnemyType.SpikeBlock] = new Brain { Move = new SpikeBlockMove(), Special = new CodeSpecial((g, e, p, dt) => g.SpikeBlockSpecial(e, p, dt)) };
        }
    }
}
