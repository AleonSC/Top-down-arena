using System;

namespace Arena
{
    /// The Twin Rifle Auto (the Soldier DPS's fourth weapon, Choice 3). The Twin Rifle's guns held out to your sides, but you never pull
    /// the trigger: each rifle turns on its own to the nearest enemy on its side of you (left of your facing for the left gun, right for the
    /// right) and fires at it, up to the guns' range and with a clear line. A side with nobody on it borrows the other gun's target.
    /// Alt (Msg.WeaponDash, DashLock) locks both guns on the enemy nearest your cursor until it dies, or until Alt is pressed again;
    /// locked, both streams run at that one enemy and cross on it, so every hit is crossfire (1.5x). No grenades.
    /// Replicated: Player.TwinAimL / TwinAimR (each gun's heading relative to your facing, + toward your right) and Player.TwinLock (the enemy's id).
    /// The look is the Twin Rifle's, with the guns turned to those headings (Game/DpsGunLooks.cs TwinAutoPose) and a lock reticle (Game/TwinAutoLook.cs).
    public partial class Game
    {
        /// Where a gun's grip sits: out to your side (AutoShoulder) and a little ahead (AutoForward). The muzzle is AutoBarrel past it along the gun's heading.
        public const float AutoShoulder = 34f, AutoForward = 6f, AutoBarrel = 26f;
        public const float AutoTurn = 14f;           // how fast a gun swings round, radians a second
        public const float AutoAimTol = 0.2f;        // a gun fires once it points this close (radians) to its target
        public const float AutoRestOut = 0.35f;      // with nothing to shoot at a gun rests this far outward of straight ahead
        public const float AutoLockCd = 0.25f;       // between Alt presses
        public const float AutoPickRadius = 260f;    // the lock picks the enemy nearest your cursor within this of it

        /// The two weapons that wear the Twin Rifle's look: the Twin Rifle itself and the Twin Rifle Auto.
        public static bool IsTwinRifle(WeaponId id) => id == WeaponId.TwinRifle || id == WeaponId.TwinRifleAuto;

        /// Gun `gun` (0 left, 1 right): its grip in the world.
        public V2 AutoGrip(Player p, V2 pos, int gun)
        {
            V2 d = V2.FromAngle(p.Aim), left = d.Orthogonal();  // (arena y runs down the screen, so Orthogonal is to your left)
            return pos + d * AutoForward + left * ((gun == 0 ? 1f : -1f) * AutoShoulder);
        }

        /// Gun `gun`'s heading in the world.
        public float AutoHeading(Player p, int gun) => p.Aim + (gun == 0 ? p.TwinAimL : p.TwinAimR);

        /// Gun `gun`'s muzzle in the world, from `pos` (your Pos for the sim, Disp for drawing).
        public V2 AutoMuzzle(Player p, V2 pos, int gun) => AutoGrip(p, pos, gun) + V2.FromAngle(AutoHeading(p, gun)) * AutoBarrel;

        /// How far from you the guns reach (a round's speed times its life: 0.5 s, against the Twin Rifle's 0.7 s).
        public float AutoRange(Player p) => p.Weapon.Speed * p.Weapon.Life * p.Stats.ShotSpeedMult * 0.92f;

        static float AutoWrap(float a) => MathX.Fmod(a + MathX.Pi, MathX.Tau) - MathX.Pi;

        static float AutoApproach(float cur, float target, float step)
        {
            float d = AutoWrap(target - cur);
            return Math.Abs(d) <= step ? cur + d : cur + (d > 0f ? step : -step);
        }

        /// A clear shot from you to `e` (no wall between).
        bool AutoClear(Player p, Enemy e) => ActiveLayout == null || !SegBlocked(ActiveLayout, p.Pos, e.Pos, 0f);

        /// Host: the enemy gun `gun` should go for, or null: the nearest one on its side (in range, a clear line).
        Enemy AutoPick(Player p, int gun, float range)
        {
            V2 left = V2.FromAngle(p.Aim).Orthogonal();
            Enemy best = null;
            float bestD = float.MaxValue;
            foreach (var e in Enemies.Values)
            {
                if (!Targetable(e))
                    continue;
                V2 to = e.Pos - p.Pos;
                bool onLeft = to.Dot(left) >= 0f;
                if (onLeft != (gun == 0))
                    continue;
                float d = to.Length - e.Radius;
                if (d > range || d >= bestD || !AutoClear(p, e))
                    continue;
                best = e;
                bestD = d;
            }
            return best;
        }

        /// Host, every tick (the fire loop in Game.cs): aim each gun, and fire the ones that point at their target.
        void TickTwinAuto(Player p, float dt)
        {
            p.TwinAutoCd = Math.Max(0f, p.TwinAutoCd - dt);
            float range = AutoRange(p);
            Enemy locked = null;
            if (p.TwinLock != 0 && (!Enemies.TryGetValue(p.TwinLock, out locked) || !Targetable(locked)))
            {
                p.TwinLock = 0;  // it died (or left): both guns go back to picking their own
                locked = null;
            }
            var tgt = new Enemy[2];
            if (locked != null)
                tgt[0] = tgt[1] = locked;
            else
            {
                tgt[0] = AutoPick(p, 0, range);
                tgt[1] = AutoPick(p, 1, range);
                if (tgt[0] == null) tgt[0] = tgt[1];  // a side with nobody on it borrows the other gun's
                if (tgt[1] == null) tgt[1] = tgt[0];
            }
            bool stunned = p.StunT > 0f || p.ParryActive || State == MatchState.GameOver;
            var ready = new bool[2];
            for (int g = 0; g < 2; g++)
            {
                float want = g == 0 ? -AutoRestOut : AutoRestOut;  // (relative to your facing)
                if (tgt[g] != null)
                    want = AutoWrap((tgt[g].Pos - AutoGrip(p, p.Pos, g)).Angle - p.Aim);
                float cur = g == 0 ? p.TwinAimL : p.TwinAimR, now = AutoApproach(cur, want, AutoTurn * dt);
                if (g == 0) p.TwinAimL = now; else p.TwinAimR = now;
                ready[g] = tgt[g] != null && Math.Abs(AutoWrap(want - now)) <= AutoAimTol
                    && tgt[g].Pos.DistanceTo(AutoGrip(p, p.Pos, g)) - tgt[g].Radius <= range + AutoBarrel;
            }
            if (stunned || p.FireCd > 0f || !(ready[0] || ready[1]))
                return;
            p.FireCd = p.Weapon.FireInterval / FireRate(p);
            int extra = ExtraShots(p);
            firingWeapon = true;
            for (int g = 0; g < 2; g++)
                if (ready[g])
                    AutoShoot(p, g, tgt[g], locked != null, extra);
            firingWeapon = false;
        }

        /// Host: gun `gun` fires its volley: one round, plus one per extra projectile fanned a little.
        void AutoShoot(Player p, int gun, Enemy target, bool locked, int extra)
        {
            var w = p.Weapon;
            float dmg = w.Damage * p.Stats.DmgMult;
            float heading = AutoHeading(p, gun) + Rand(-TwinRifleSpread, TwinRifleSpread);
            V2 from = AutoMuzzle(p, p.Pos, gun);
            if (ActiveLayout != null && SegBlocked(ActiveLayout, p.Pos, from, 0f))
                from = p.Pos;  // a gun pushed into a wall fires from your middle
            for (int i = 0; i <= extra; i++)
            {
                float a = heading + (i == 0 ? 0f : (i % 2 == 1 ? 1f : -1f) * 0.06f * ((i + 1) / 2));
                var b = SpawnBullet(Defs.DpsShot, from, V2.FromAngle(a) * w.Speed, 0f, w.Life, dmg, p.Id);
                if (locked)
                {
                    b.Cross = target.Pos;  // both streams meet on the locked enemy: every hit is crossfire
                    b.Crossfire = true;
                }
            }
        }

        /// Host: Alt with the Twin Rifle Auto, aimed at `aimAt`. Locks the enemy nearest the cursor, or lets go of the one it has.
        void HostTwinAutoLock(Player p, V2 aimAt)
        {
            if (p.Weapon.Id != WeaponId.TwinRifleAuto || p.Downed || p.StunT > 0f || p.TwinAutoCd > 0f || State == MatchState.GameOver)
                return;
            p.TwinAutoCd = AutoLockCd;
            if (p.TwinLock != 0)
            {
                p.TwinLock = 0;
                BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.6f, 0.8f, 1f), 14f, 0.15f, Sfx.None);
                return;
            }
            float reach = AutoRange(p) + 120f;
            Enemy best = null;
            float bestD = AutoPickRadius;
            foreach (var e in Enemies.Values)
            {
                if (!Targetable(e) || e.Pos.DistanceTo(p.Pos) - e.Radius > reach)
                    continue;
                float d = e.Pos.DistanceTo(aimAt) - e.Radius;
                if (d < bestD)
                {
                    best = e;
                    bestD = d;
                }
            }
            if (best == null)
                return;
            p.TwinLock = best.Id;
            BroadcastFx(FxKind.Ring, best.Pos, new Rgba(1f, 0.36f, 0.36f), best.Radius + 14f, 0.25f, Sfx.Pick);
        }
    }
}
