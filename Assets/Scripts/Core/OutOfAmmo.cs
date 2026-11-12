using System;
using System.Collections.Generic;

namespace Arena
{
    /// Out of Ammo (the Twin Rifle's second variant, Variants window tier II; Choice 4). Two sets of arms:
    ///  - The rifle arms, out at your sides, work on their own and don't shoot. Swat: each knocks the nearest enemy bullet within reach out of the air
    ///    (one each, then AmmoSwatCd of recovery, so two at a time). Bash: with an enemy close in front (AmmoReach, within AmmoCone of your facing) they bash
    ///    in turn: left, right, left, then a rifle clap (an area with a shockwave), and round again. Player.AmmoMove 1-4 is the move running.
    ///  - Your own arms hold a knife, worked by hand: holding fire runs a three-hit chain (cross cut, reverse cut, lunging stab) whether or not anything
    ///    is there to hit; it adds a little damage on top of the rifles. Player.KnifeMove 1-3 is the move running.
    ///  - Alt (Msg.WeaponDash, DashSpin): a spin for AmmoSpinTime that cuts everything round you every AmmoSpinTick and swats every enemy bullet that
    ///    comes in reach (no one-each limit), then AmmoSpinCd. Both sets of arms are busy while it runs.
    /// Replicated: AmmoMove/AmmoSeq, KnifeMove/KnifeSeq (the look restarts its clock when a Seq rises), SwatSeqL/R and SwatAngL/R, AmmoSpin.
    /// Drawn in Game/OutOfAmmoLook.cs with the same timings (AmmoMotion).
    public static class AmmoMotion
    {
        // The rifle arms' moves (left bash, right bash, left bash, rifle clap), seconds into the move: wind-up end, strike end, total, the hit's moment
        public static readonly float[] Wind = { .12f, .12f, .12f, .40f };
        public static readonly float[] Strike = { .20f, .17f, .20f, .52f };
        public static readonly float[] Total = { .42f, .42f, .42f, 1.20f };
        public static readonly float[] Hit = { .20f, .17f, .20f, .50f };
        public static readonly float[] Damage = { 10f, 10f, 10f, 35f };
        public static readonly float[] Knock = { 12f, 12f, 12f, 60f };
        public static readonly string[] Names = { "Left bash", "Right bash", "Left bash", "Rifle clap" };
        // The knife's moves (cross cut, reverse cut, lunging stab)
        public static readonly float[] KWind = { .10f, .10f, .14f };
        public static readonly float[] KStrike = { .22f, .22f, .30f };
        public static readonly float[] KTotal = { .38f, .38f, .55f };
        public static readonly float[] KHit = { .16f, .16f, .26f };
        public static readonly float[] KDamage = { 12f, 12f, 20f };
        public static readonly float[] KCone = { 1.0f, 1.0f, 0.55f };
        public static readonly float[] KReach = { 84f, 84f, 106f };
        public static readonly string[] KNames = { "Cross cut", "Reverse cut", "Lunge stab" };
    }

    public partial class Game
    {
        public const float AmmoReach = 78f, AmmoCone = 1.0f, AmmoClapCone = 1.3f, AmmoClapRadius = 92f;
        public const float AmmoChainGap = 0.5f, AmmoStartGap = 0.04f, KnifeChainGap = 0.45f;
        public const float AmmoSwatReach = 72f, AmmoSwatCd = 0.5f;
        public const float AmmoSpinTime = 1.2f, AmmoSpinCd = 6f, AmmoSpinTick = 0.2f, AmmoSpinRadius = 88f, AmmoSpinDamage = 14f, AmmoSpinKnock = 22f;

        /// Host, every tick (the fire loop in Game.cs).
        void TickOutOfAmmo(Player p, float dt)
        {
            p.AmmoCd = Math.Max(0f, p.AmmoCd - dt);
            p.AmmoSwatCdL = Math.Max(0f, p.AmmoSwatCdL - dt);
            p.AmmoSwatCdR = Math.Max(0f, p.AmmoSwatCdR - dt);
            if (p.Downed || p.StunT > 0f || State == MatchState.GameOver)
            {
                p.AmmoMove = 0;
                p.KnifeMove = 0;
                p.AmmoSpin = 0f;
                return;
            }
            if (p.AmmoSpin > 0f)
            {
                TickAmmoSpin(p, dt);
                return;
            }
            AmmoSwat(p);
            float rate = FireRate(p);
            // the rifle arms: bash in turn on whatever is close in front
            if (p.AmmoMove > 0)
            {
                int m = p.AmmoMove - 1;
                p.AmmoT += dt * rate;
                if (!p.AmmoHit && p.AmmoT >= AmmoMotion.Hit[m])
                {
                    p.AmmoHit = true;
                    AmmoLand(p, m);
                }
                if (p.AmmoT >= AmmoMotion.Total[m])
                {
                    p.AmmoMove = 0;
                    p.AmmoIdle = 0f;
                    p.AmmoCombo = m == 3 ? 0 : m + 1;
                }
            }
            else
            {
                p.AmmoIdle += dt;
                if (p.AmmoCombo != 0 && p.AmmoIdle > AmmoChainGap)
                    p.AmmoCombo = 0;  // nothing close for a moment: the bashes start again
                if (p.AmmoIdle >= AmmoStartGap && !p.ParryActive && AmmoCloseEnemy(p, AmmoCone))
                    AmmoStart(p);
            }
            // your own arms: the knife, while fire is held
            if (p.KnifeMove > 0)
            {
                int k = p.KnifeMove - 1;
                p.KnifeT += dt * rate;
                if (!p.KnifeHit && p.KnifeT >= AmmoMotion.KHit[k])
                {
                    p.KnifeHit = true;
                    KnifeLand(p, k);
                }
                if (p.KnifeT >= AmmoMotion.KTotal[k])
                {
                    p.KnifeMove = 0;
                    p.KnifeIdle = 0f;
                    p.KnifeCombo = k == 2 ? 0 : k + 1;
                }
            }
            else
            {
                p.KnifeIdle += dt;
                if (p.KnifeCombo != 0 && p.KnifeIdle > KnifeChainGap)
                    p.KnifeCombo = 0;  // let go for a moment: the chain starts again
                if (p.Firing && !p.ParryActive && p.KnifeIdle >= AmmoStartGap)
                    KnifeStart(p);
            }
        }

        /// Host: start the rifle arms' next move now (also what firing the weapon by hand does).
        void AmmoStart(Player p)
        {
            p.AmmoMove = (byte)(p.AmmoCombo + 1);
            p.AmmoT = 0f;
            p.AmmoHit = false;
            p.AmmoSeq++;
        }

        void KnifeStart(Player p)
        {
            p.KnifeMove = (byte)(p.KnifeCombo + 1);
            p.KnifeT = 0f;
            p.KnifeHit = false;
            p.KnifeSeq++;
        }

        bool AmmoInCone(Player p, Enemy e, float cone, float reach)
        {
            V2 to = e.Pos - p.Pos;
            float d = to.Length - e.Radius;
            if (d > reach)
                return false;
            if (to.Length < 1f)
                return true;
            float a = Math.Abs(MathX.Fmod(to.Angle - p.Aim + MathX.Pi, MathX.Tau) - MathX.Pi);
            return a <= cone + (e.Radius / Math.Max(20f, to.Length));
        }

        bool AmmoCloseEnemy(Player p, float cone)
        {
            foreach (var e in Enemies.Values)
                if (Targetable(e) && AmmoInCone(p, e, cone, AmmoReach))
                    return true;
            return false;
        }

        /// Host: rifle move `m` (0-3) lands: everything in front, within reach, takes the hit; the clap is wider and shoves hard.
        void AmmoLand(Player p, int m)
        {
            bool clap = m == 3;
            float dmg = AmmoMotion.Damage[m] * p.Stats.DmgMult * (1f + 0.5f * ExtraShots(p));
            V2 fwd = V2.FromAngle(p.Aim);
            foreach (var e in new List<Enemy>(Enemies.Values))
            {
                if (!Targetable(e))
                    continue;
                bool inside = clap ? AmmoInCone(p, e, AmmoClapCone, AmmoClapRadius) : AmmoInCone(p, e, AmmoCone, AmmoReach);
                if (!inside)
                    continue;
                HurtEnemy(e, dmg, p.Id);
                V2 away = (e.Pos - p.Pos).Normalized();
                if (!Heavy(e))
                    KnockEnemy(e, (away.IsZero ? fwd : away) * AmmoMotion.Knock[m]);
                BroadcastFx(FxKind.Spark, e.Pos - away * e.Radius, clap ? new Rgba(1f, 0.76f, 0.24f) : new Rgba(0.8f, 0.85f, 0.9f), clap ? 16f : 14f, 0.25f, clap ? Sfx.Hit : Sfx.Clang);
            }
            if (clap)
                BroadcastFx(FxKind.Ring, p.Pos + fwd * 50f, new Rgba(0.25f, 0.88f, 0.91f), AmmoClapRadius, 0.45f, Sfx.Shockwave);
        }

        /// Host: knife move `k` (0-2) lands on everything in its cone and reach.
        void KnifeLand(Player p, int k)
        {
            float dmg = AmmoMotion.KDamage[k] * p.Stats.DmgMult * (1f + 0.5f * ExtraShots(p));
            V2 fwd = V2.FromAngle(p.Aim);
            bool hitAny = false;
            foreach (var e in new List<Enemy>(Enemies.Values))
            {
                if (!Targetable(e) || !AmmoInCone(p, e, AmmoMotion.KCone[k], AmmoMotion.KReach[k]))
                    continue;
                hitAny = true;
                HurtEnemy(e, dmg, p.Id);
                V2 away = (e.Pos - p.Pos).Normalized();
                if (!Heavy(e) && k == 2)
                    KnockEnemy(e, (away.IsZero ? fwd : away) * 18f);
                BroadcastFx(FxKind.Spark, e.Pos - away * e.Radius, new Rgba(1f, 0.76f, 0.24f), k == 2 ? 24f : 16f, 0.25f, Sfx.SwingLight);
            }
            if (!hitAny)
                BroadcastFx(FxKind.Ring, p.Pos + fwd * 40f, new Rgba(0.8f, 0.85f, 0.9f), 10f, 0.12f, Sfx.SwingLight);
        }

        /// Host: each rifle swats the nearest enemy bullet in reach, if it is ready (one bullet each).
        void AmmoSwat(Player p)
        {
            for (int g = 0; g < 2; g++)
            {
                if ((g == 0 ? p.AmmoSwatCdL : p.AmmoSwatCdR) > 0f)
                    continue;
                V2 left = V2.FromAngle(p.Aim).Orthogonal();
                Bullet best = null;
                float bd = AmmoSwatReach;
                foreach (var b in Bullets.Values)
                {
                    if (!Defs.IsEnemyShot(b.Kind) || Defs.Unblockable(b.Kind))
                        continue;
                    float d = b.Pos.DistanceTo(p.Pos);
                    bool onLeft = (b.Pos - p.Pos).Dot(left) >= 0f;
                    if (d < bd && (onLeft == (g == 0) || d < AmmoSwatReach * 0.45f))
                    {
                        bd = d;
                        best = b;
                    }
                }
                if (best == null)
                    continue;
                float ang = (best.Pos - p.Pos).Angle;
                if (g == 0) { p.AmmoSwatCdL = AmmoSwatCd; p.SwatAngL = ang; p.SwatSeqL++; }
                else { p.AmmoSwatCdR = AmmoSwatCd; p.SwatAngR = ang; p.SwatSeqR++; }
                SwatPop(best);
            }
        }

        void SwatPop(Bullet b)
        {
            BroadcastFx(FxKind.Spark, b.Pos, new Rgba(0.25f, 0.88f, 0.91f), 14f, 0.25f, Sfx.Clang);
            Bullets.Remove(b.Id);
            pendingRemoved.Add(b.Id);
        }

        /// Host: Alt with Out of Ammo: the spin, if it is ready.
        void HostAmmoSpin(Player p)
        {
            if (p.Weapon.Id != WeaponId.OutOfAmmo || p.Downed || p.StunT > 0f || p.AmmoCd > 0f || p.AmmoSpin > 0f || State == MatchState.GameOver)
                return;
            p.AmmoSpin = AmmoSpinTime;
            p.AmmoCd = AmmoSpinCd + AmmoSpinTime;
            p.AmmoMove = 0;
            p.KnifeMove = 0;
            p.AmmoTick = 0f;
            BroadcastFx(FxKind.Ring, p.Pos, new Rgba(1f, 0.76f, 0.24f), AmmoSpinRadius, 0.3f, Sfx.SwingHeavy);
        }

        void TickAmmoSpin(Player p, float dt)
        {
            p.AmmoSpin = Math.Max(0f, p.AmmoSpin - dt);
            var gone = new List<Bullet>();
            foreach (var b in Bullets.Values)
                if (Defs.IsEnemyShot(b.Kind) && !Defs.Unblockable(b.Kind) && b.Pos.DistanceTo(p.Pos) <= AmmoSpinRadius)
                    gone.Add(b);
            foreach (var b in gone)
                SwatPop(b);  // every bullet that touches the spin, no limit
            p.AmmoTick -= dt;
            if (p.AmmoTick > 0f)
                return;
            p.AmmoTick += AmmoSpinTick;
            float dmg = AmmoSpinDamage * p.Stats.DmgMult * (1f + 0.5f * ExtraShots(p));
            foreach (var e in new List<Enemy>(Enemies.Values))
            {
                if (!Targetable(e) || e.Pos.DistanceTo(p.Pos) - e.Radius > AmmoSpinRadius)
                    continue;
                HurtEnemy(e, dmg, p.Id);
                V2 away = (e.Pos - p.Pos).Normalized();
                if (!Heavy(e) && !away.IsZero)
                    KnockEnemy(e, away * AmmoSpinKnock);
                BroadcastFx(FxKind.Spark, e.Pos - away * e.Radius, new Rgba(1f, 0.76f, 0.24f), 12f, 0.2f, Sfx.Hit);
            }
        }
    }
}
