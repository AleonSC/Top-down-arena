using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Elementalist DPS's Stonespear (the DPS weapon on the lobby's 4th key; mockup: the Stonespear artifact, Basalt look).
    ///   Fire with no spears up: a slam. SsWind of wind-up (FxKind.StoneSlam, Pos2.X 0), then SsSlamDmg x the weapon's damage to every
    ///     enemy within SsSlamRadius (a small shove, not on heavies; FxKind.StoneSlam, Pos2.X 1: the ring, the cracks and the dust), and
    ///     SsPerSide spears a side tear out of the ground and lift to their slots round you (Player.SsMask, one bit a slot).
    ///   Fire with spears up: the risen spear nearest your cursor is thrown at it (Defs.StoneSpear, SsPierce), one every FireInterval.
    ///   Alt (Msg.WeaponDash, WeaponAlt.Volley): every spear left goes, SsVolleyGap apart, at the enemy nearest your cursor (within SsLockRange):
    ///     each peels out to its side, then curves in on it (Bullet.HomeOn, steered by the host), for SsVolleyMult x the damage.
    ///   SsCd after the last spear leaves, the next Fire slams again. Extra projectiles add a spear a side and throw one more a click;
    ///   Tectonic adds a spear a side.
    ///   The spears' slots (SlotOffset) are fanned at your sides round your aim; the look eases each spear toward its slot, so they trail
    ///   behind you when you move (Game/StonespearLook.cs).
    public partial class Game
    {
        public const float SsWind = 0.3f, SsSlamRadius = 110f, SsSlamDmg = 0.75f, SsSlamKnock = 34f, SsCd = 0.6f;
        public const float SsRise = 0.42f, SsRiseGap = 0.035f;                 // a spear's rise out of the ground; each slot starts this much after the last
        public const float SsVolleyGap = 0.06f, SsVolleyMult = 1.25f, SsLockRange = 220f, SsVolleySpeed = 620f, SsVolleyTop = 1100f, SsVolleyLife = 2.2f, SsPeel = 1.1f;
        public const int SsPerSide = 3, SsPierce = 1, SsMaxSide = 8;

        /// How many spears a side this player raises: 3, +1 per extra projectile and per Tectonic.
        public int SsSideCount(Player p) => Math.Min(SsMaxSide, SsPerSide + ExtraShots(p) + (p != null ? p.Stats.SsSideBonus : 0));

        /// Where slot k (of n a side: 0..n-1 on your left, n..2n-1 on your right) sits from you, with `aim` your aim and t the clock:
        /// fanned out from the aim, the inner slots nearest your front. Lift: how high it floats (its shadow is drawn that far off).
        public static V2 SlotOffset(float aim, int k, int n, float t, out float lift)
        {
            int side = k < n ? -1 : 1, i = k % Math.Max(1, n);
            float step = n > 1 ? 1f / (n - 1) : 0f, a = aim + side * (MathX.Pi * 0.36f + i * step), r = 48f + i * (n > 1 ? 32f / (n - 1) : 0f);
            float bob = (float)Math.Sin(t * 3f + i * 1.7f + side) * 2.5f;
            lift = 16f + i * 3f + bob;
            return V2.FromAngle(a) * r + new V2(0f, bob);
        }

        public static V2 SlotOffset(float aim, int k, int n, float t) { float lift; return SlotOffset(aim, k, n, t, out lift); }

        /// The time after the slam's impact when slot k has finished rising (it can't be thrown before).
        public static float SsRisen(int k) => SsRise + SsRiseGap * k;

        static int Bits(int mask) { int n = 0; for (; mask != 0; mask &= mask - 1) n++; return n; }

        /// Host, every tick the Stonespear is out (and you're up): the wind-up, the volley going out, the trigger.
        void TickStonespear(Player p, float dt)
        {
            p.SsAge += dt;
            p.SsCdT -= dt;
            if (p.SsWindT > 0f && (p.SsWindT -= dt) <= 0f)
                StoneSlam(p);
            if (p.SsQueue.Count > 0 && (p.SsVolleyT -= dt) <= 0f)
            {
                LaunchVolleySpear(p, p.SsQueue[0]);
                p.SsQueue.RemoveAt(0);
                p.SsVolleyT += SsVolleyGap;
            }
            bool up = p.SsMask != 0;
            if (p.SsHad && !up && p.SsQueue.Count == 0)
                p.SsCdT = SsCd;  // the last spear just left
            p.SsHad = up;
            if (p.SsLock != 0 && p.SsQueue.Count == 0 && !VolleyFlying(p))
                p.SsLock = 0;
            if (!p.Firing || p.ParryActive || p.FireCd > 0f || p.SsWindT > 0f || p.SsQueue.Count > 0 || State == MatchState.GameOver)
                return;
            if (!up)
            {
                if (p.SsCdT > 0f)
                    return;
                p.SsWindT = SsWind;  // the slam: crouch, then the ground breaks
                p.FireCd = SsWind;
                BroadcastFx(FxKind.StoneSlam, p.Pos, new V2(0f, p.Id), SsBasalt, SsSlamRadiusOf(p), SsWind, Sfx.None);  // (the look plays the Stonespear's sounds: StonespearSoundBank.cs)
                return;
            }
            if (PickSpear(p) < 0)
                return;  // still rising
            p.FireCd = p.Weapon.FireInterval / FireRate(p);
            FirePlayer(p);  // (FireVolley: ThrowStonespear)
        }

        public static readonly Rgba SsBasalt = new Rgba(1f, 0.69f, 0.29f);  // the amber light in the seams (the slam's colour)

        float SsSlamRadiusOf(Player p) => SsSlamRadius * AreaMult(p) * p.Stats.SsSlamMult;

        /// The slam lands: it hurts and shoves what is round you, and the spears tear out of the ground.
        void StoneSlam(Player p)
        {
            float r = SsSlamRadiusOf(p), dmg = p.Weapon.Damage * SsSlamDmg * p.Stats.DmgMult;
            var snap = SnapFoes(p.Id);
            bool any = false;
            foreach (var e in snap)
            {
                if (!Targetable(e))
                    continue;
                float d = e.Pos.DistanceTo(p.Pos);
                if (d - e.Radius > r)
                    continue;
                any = true;
                WeaponHurt(e, dmg, p.Id);
                if (e.Hp > 0f && !Heavy(e))
                    KnockEnemy(e, (d > 1f ? (e.Pos - p.Pos) / d : V2.FromAngle(p.Aim)) * (SsSlamKnock * (0.5f + 0.5f * Math.Max(0f, 1f - d / r))));
            }
            Free(snap);
            p.Run.ShotsFired++;
            if (any)
                p.Run.ShotsHit++;
            int n = SsSideCount(p);
            p.SsSide = n;
            p.SsMask = (1 << (2 * n)) - 1;
            p.SsAge = 0f;
            p.SsHad = true;
            BroadcastFx(FxKind.StoneSlam, p.Pos, new V2(1f + Rand(0f, 0.99f), p.Id), SsBasalt, r, 2.2f, Sfx.None);
        }

        /// The risen spear nearest your cursor, or -1.
        int PickSpear(Player p)
        {
            int best = -1;
            float bestD = float.MaxValue;
            for (int k = 0; k < 2 * p.SsSide; k++)
            {
                if ((p.SsMask & (1 << k)) == 0 || p.SsAge < SsRisen(k))
                    continue;
                float d = (p.Pos + SlotOffset(p.Aim, k, p.SsSide, AnimT)).DistanceTo(p.AimAt);
                if (d < bestD)
                {
                    bestD = d;
                    best = k;
                }
            }
            return best;
        }

        V2 ssThrowFrom, ssThrowDir;  // the last thrown spear, for Barrage's copies

        /// Fire (FireVolley): throw the risen spear nearest your cursor at it, and one more for each extra projectile (the next nearest).
        /// Barrage's copies fan out from the first spear (they don't use one up). With none up (anything that fires the weapon by hand)
        /// one is thrown from your hand.
        void ThrowStonespear(Player p, float aim, V2 muzzle, float dmg, int extra)
        {
            var w = p.Weapon;
            if (volleyShare < 1f)
            {
                SpawnSpear(p, ssThrowFrom, ssThrowDir.Rotated(aim - p.Aim), w.Damage * dmg);  // a Barrage copy, fanned off the throw
                return;
            }
            for (int n = 0; n <= extra; n++)
            {
                int k = PickSpear(p);
                if (k < 0 && n > 0)
                    break;
                V2 from = k >= 0 ? p.Pos + SlotOffset(p.Aim, k, p.SsSide, AnimT) : muzzle;
                V2 to = p.AimAt - from;
                V2 dir = to.Length > 4f ? to.Normalized() : V2.FromAngle(aim);
                if (k >= 0)
                    p.SsMask &= ~(1 << k);
                if (n == 0)
                {
                    ssThrowFrom = from;
                    ssThrowDir = dir;
                }
                SpawnSpear(p, from, dir, w.Damage * dmg);
            }
        }

        void SpawnSpear(Player p, V2 from, V2 dir, float dmg)
        {
            var b = SpawnBullet(Defs.StoneSpear, from, dir * p.Weapon.Speed, 0f, p.Weapon.Life * PierceReach(p), dmg, p.Id);
            b.Pierce += SsPierce;
            b.Bolts = true;  // it pierces: Ricochet flings bounce bolts off it
        }

        /// Alt: every spear left goes at the enemy nearest the cursor (or straight at the cursor with none there).
        void HostStoneVolley(Player p, V2 aimAt)
        {
            if (p.Weapon.Id != WeaponId.Stonespear || p.StunT > 0f || p.SsMask == 0 || p.SsQueue.Count > 0 || p.SsWindT > 0f || State == MatchState.GameOver)
                return;
            Enemy lockOn = null;
            float bestD = SsLockRange;
            foreach (var e in Enemies.Values)
            {
                if (!Targetable(e))
                    continue;
                float d = e.Pos.DistanceTo(aimAt);
                if (d < bestD)
                {
                    bestD = d;
                    lockOn = e;
                }
            }
            p.SsLock = lockOn != null ? lockOn.Id : 0;
            p.SsLockAt = aimAt;
            for (int k = 0; k < 2 * p.SsSide; k++)
                if ((p.SsMask & (1 << k)) != 0)
                    p.SsQueue.Add(k);
            p.SsVolleyT = 0f;
        }

        void LaunchVolleySpear(Player p, int k)
        {
            if ((p.SsMask & (1 << k)) == 0)
                return;
            p.SsMask &= ~(1 << k);
            var w = p.Weapon;
            V2 from = p.Pos + SlotOffset(p.Aim, k, p.SsSide, AnimT);
            Enemy target;
            bool locked = p.SsLock != 0 && TryFoe(p.SsLock, out target) && Targetable(target);
            V2 at = locked ? Enemies[p.SsLock].Pos : p.SsLockAt;
            V2 to = at - from;
            float a = to.Length > 1f ? to.Angle : p.Aim;
            float side = k < p.SsSide ? -1f : 1f;
            firingWeapon = true;
            var b = locked
                ? SpawnBullet(Defs.StoneSpear, from, V2.FromAngle(a + side * SsPeel) * SsVolleySpeed, 0f, SsVolleyLife, w.Damage * SsVolleyMult * p.Stats.DmgMult, p.Id)
                : SpawnBullet(Defs.StoneSpear, from, V2.FromAngle(a) * w.Speed, 0f, w.Life * PierceReach(p), w.Damage * SsVolleyMult * p.Stats.DmgMult, p.Id);
            firingWeapon = false;
            if (locked)
            {
                b.HomeOn = p.SsLock;
                b.Homing = 0f;  // (it steers itself: SteerSpear)
                if (b.Sent != null)
                    b.Sent.Homing = 0f;
                b.Pulled = true;  // the host sends its course
            }
        }

        bool VolleyFlying(Player p)
        {
            foreach (var b in Bullets.Values)
                if (b.Owner == p.Id && b.HomeOn != 0)
                    return true;
            return false;
        }

        /// Host, from MoveBullets: a volley spear curves in on the enemy it was sent at, turning harder and speeding up as it goes.
        /// If that one dies first it takes the nearest other; with none left it flies on straight.
        void SteerSpear(Bullet b, float dt)
        {
            Enemy e;
            if (!TryFoe(b.HomeOn, out e) || !Targetable(e))
            {
                e = null;
                float bestD = float.MaxValue;
                foreach (var o in Enemies.Values)
                {
                    if (!Targetable(o))
                        continue;
                    float d = o.Pos.DistanceTo(b.Pos);
                    if (d < bestD)
                    {
                        bestD = d;
                        e = o;
                    }
                }
                b.HomeOn = e != null ? e.Id : 0;
                if (e == null)
                    return;
            }
            float age = b.Life0 - b.Life;
            float want = (e.Pos - b.Pos).Angle, have = b.Vel.Angle;
            float diff = MathX.WrapPi(want - have);
            float speed = Math.Min(SsVolleyTop, b.Vel.Length + dt * 1400f);
            b.Vel = V2.FromAngle(have + diff * Math.Min(1f, dt * (5f + age * 14f))) * speed;
        }

        /// Off when you go down or put it away: the spears crumble and the slam and volley stop.
        void ClearStonespear(Player p)
        {
            p.SsMask = 0;
            p.SsWindT = 0f;
            p.SsQueue.Clear();
            p.SsHad = false;
            p.SsLock = 0;
        }

        /// Your own game: Alt was pressed with the Stonespear (the host checks the rest).
        bool LocalStoneVolley(Player me, bool pressed, V2 aimAt)
        {
            if (me.Weapon.Id != WeaponId.Stonespear || !pressed)
                return false;
            if (me.SsMask != 0)
            {
                if (IsHost)
                    HostWeaponDash(MyId, WeaponAlt.Volley, aimAt);
                else
                {
                    var w = Proto.Begin(Msg.WeaponDash);
                    w.Write(WeaponAlt.Volley);
                    w.Write(aimAt);
                    session.SendToHost(Proto.End(w));
                }
            }
            return true;
        }
    }
}
