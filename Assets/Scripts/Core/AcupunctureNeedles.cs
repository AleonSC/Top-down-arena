using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Healer Ninja's Acupuncture Needles (was Medicine Darts; mockup Docs/Mockups/acupuncture-needles.html).
    ///   Fire (Ninja.cs FireNinjaWeapon): fast needles (Bullet.Medicine) that fly through enemies and stick in the first hurt
    ///     teammate they touch (NeedleTouches). Each needle in you heals NeedleHeal a second; up to NeedleMax at once, all
    ///     falling out NeedleTime after the last one went in. The NeedleMax-th opens the meridian: they all come out at
    ///     once for a burst of MeridianHeal (FxKind.NeedleBurst).
    ///   Alt (Msg.WeaponDash DashBloodlet): Bloodletting, three long needles (Defs.BloodNeedle) fanned at the cursor, every
    ///     BloodletCd. Each hits for the weapon's damage, sticks (Enemy.NeedlesIn) and adds a stack of the shared bleed
    ///     (BloodBleed a second). The NeedleMax-th in one enemy bursts at the pressure point: PressureDmg (bosses
    ///     PressureBoss), the bleed renewed, the needles out.
    ///   Needles Return: an enemy that dies with needles in it sends them back (ReturnNeedles) to the most hurt player,
    ///     each healing ReturnHeal on arrival.
    ///   Potent Dose (Stats.DoseMult) strengthens the needles' healing and the bleed; damage upgrades do too (b.Dmg).
    public partial class Game
    {
        public const float NeedleHeal = 3f, NeedleTime = 4f, MeridianHeal = 15f;
        public const int NeedleMax = 5;
        public const float BloodletCd = 0.9f, BloodSpeed = 760f, BloodRange = 420f, BloodFan = 0.04f, BloodBleed = 3f;
        public const float PressureDmg = 25f, PressureBoss = 15f, ReturnHeal = 4f, ReturnSpeed = 700f, ReturnTurn = 9f;
        public static readonly Rgba NeedleJade = new Rgba(0.37f, 0.88f, 0.64f), NeedleBlood = new Rgba(1f, 0.3f, 0.38f);
        const byte DashBloodlet = 64;  // Msg.WeaponDash kind

        float DoseMultOf(int owner) => Players.ContainsKey(owner) ? Players[owner].Stats.DoseMult : 1f;
        static float NeedlePower(Bullet b) => b.Dmg / Defs.WeaponOf(WeaponId.AcupunctureNeedles).Damage;  // damage upgrades

        // --- Fire: healing needles -----------------------------------------------------------------

        /// Host, in the shot loop: a healing needle touching a hurt teammate sticks in them (a returning one heals on
        /// arrival). True when it is spent; it never touches enemies.
        bool NeedleTouches(Bullet b)
        {
            foreach (var q in Players.Values)
            {
                bool returning = b.ReturnTo != 0;
                if (returning ? q.Id != b.ReturnTo : q.Id == b.Owner || q.Hp >= q.MaxHp)
                    continue;
                float r = q.Def.BodyR + b.Radius;
                if (q.Downed || b.Pos.DistanceSq(q.Pos) >= r * r)
                    continue;
                if (returning)
                {
                    Heal(q, b.HealAlly, PlayerOrNull(b.Owner), "Needles Return");
                    BroadcastFx(FxKind.Spark, b.Pos, NeedleJade, 5f, 0.25f, Sfx.None);
                    return true;
                }
                StickNeedle(q, b);
                return true;
            }
            return false;
        }

        void StickNeedle(Player q, Bullet b)
        {
            q.NeedleHps = NeedleHeal * NeedlePower(b) * DoseMultOf(b.Owner);
            q.NeedleT = NeedleTime;
            q.NeedleBy = b.Owner;
            q.NeedleN = Math.Min(NeedleMax, q.NeedleN + 1);
            var owner = PlayerOrNull(b.Owner);
            if (owner != null)
                owner.Run.ShotsHit++;
            if (q.NeedleN < NeedleMax)
                return;
            // Meridian Open: all five come out at once for a burst of healing.
            Heal(q, MeridianHeal * NeedlePower(b) * DoseMultOf(b.Owner), owner, "Meridian Open");
            q.NeedleN = 0;
            q.NeedleT = 0f;
            BroadcastFx(FxKind.NeedleBurst, q.Pos, new V2(0f, q.Id), NeedleJade, q.Def.BodyR, 0.55f, Sfx.SpellHoly);
        }

        /// Host, every tick (TickNinjas): the needles in you heal until they fall out.
        void TickNeedlesInPlayer(Player p, float dt)
        {
            p.BloodletCd = Math.Max(0f, p.BloodletCd - dt);
            if (p.NeedleT <= 0f)
                return;
            p.NeedleT = p.Downed ? 0f : Math.Max(0f, p.NeedleT - dt);
            if (p.NeedleT > 0f)
                Heal(p, p.NeedleN * p.NeedleHps * dt, PlayerOrNull(p.NeedleBy));
            else
                p.NeedleN = 0;
        }

        // --- Alt: Bloodletting -------------------------------------------------------------------------

        float bloodletLocalCd;  // your own game: until the Alt can go again (the cooldown ring under your feet)
        public float BloodletLocalCd => bloodletLocalCd;

        /// Your own game: Alt with the Acupuncture Needles. True when the weapon is out (the Alt is its own).
        bool LocalBloodletting(Player me, bool pressed, V2 aimAt, float dt)
        {
            bloodletLocalCd = Math.Max(0f, bloodletLocalCd - dt);
            if (me.Weapon.Id != WeaponId.AcupunctureNeedles)
                return false;
            if (pressed && bloodletLocalCd <= 0f)
            {
                bloodletLocalCd = BloodletCd;
                if (IsHost)
                    HostWeaponDash(MyId, DashBloodlet, aimAt);
                else
                {
                    var w = Proto.Begin(Msg.WeaponDash);
                    w.Write(DashBloodlet);
                    w.Write(aimAt);
                    session.SendToHost(Proto.End(w));
                }
            }
            return true;
        }

        /// Host: Alt pressed with the Acupuncture Needles: three long needles fanned at the cursor, if the cooldown is done.
        void HostBloodletting(Player p, V2 target)
        {
            if (p.Weapon.Id != WeaponId.AcupunctureNeedles || p.Downed || p.StunT > 0f || p.SwallowT > 0f || p.BloodletCd > 0f || State == MatchState.GameOver)
                return;
            p.BloodletCd = BloodletCd;
            V2 to = target - p.Pos;
            float aim = to.Length > 1f ? to.Angle : p.Aim;
            V2 muzzle = p.Pos + V2.FromAngle(aim) * (p.Def.BodyR + 4f);
            int n = 3 + ExtraShots(p);
            float dmg = p.Weapon.Damage * p.Stats.DmgMult;
            bool was = firingWeapon;
            firingWeapon = true;  // counted as the weapon's shots (accuracy, mods)
            for (int i = 0; i < n; i++)
                SpawnBullet(Defs.BloodNeedle, muzzle, V2.FromAngle(Fanned(aim, i, n, BloodFan)) * BloodSpeed, 0f, BloodRange * PierceReach(p) / BloodSpeed, dmg, p.Id);
            firingWeapon = was;
            BroadcastSound(Sfx.ShotKunai);
        }

        /// A Bloodletting needle in an enemy (OnBulletHit): it sticks and bleeds; the fifth bursts at the pressure point.
        void BloodNeedleHit(Bullet b, Enemy e)
        {
            if (b.Kind != Defs.BloodNeedle || e.Hp <= 0f)
                return;
            float power = NeedlePower(b) * DoseMultOf(b.Owner);
            ApplyBleed(e, BloodBleed * power, b.Owner);
            e.NeedlesIn = Math.Min(NeedleMax, e.NeedlesIn + 1);
            e.NeedleT = BleedTime;
            e.NeedleBy = b.Owner;
            if (e.NeedlesIn < NeedleMax)
                return;
            // Pressure Point: the needles burst out of the wound (a kill sends them back first: ReturnNeedles).
            BroadcastFx(FxKind.NeedleBurst, e.Pos, new V2(1f, e.Id), NeedleBlood, e.Radius, 0.55f, Sfx.Hit);
            HurtEnemy(e, (e.Def.IsBoss ? PressureBoss : PressureDmg) * NeedlePower(b), b.Owner);  // (b.Dmg carries the damage upgrades)
            ApplyBleed(e, BloodBleed * power, b.Owner);  // (the bleed renewed)
            if (e.Hp > 0f)
                e.NeedlesIn = 0;
        }

        /// Host, every tick for each enemy: Bloodletting needles fall out with the bleed.
        void TickNeedlesIn(Enemy e, float dt)
        {
            if (e.NeedlesIn == 0)
                return;
            if ((e.NeedleT -= dt) <= 0f)
                e.NeedlesIn = 0;
        }

        // --- Needles Return ------------------------------------------------------------------------------

        /// Host (EnemyDied): its needles fly back to the most hurt player, healing ReturnHeal each.
        void ReturnNeedles(Enemy e)
        {
            int n = e.NeedlesIn;
            e.NeedlesIn = 0;
            if (n == 0 || !Players.ContainsKey(e.NeedleBy))
                return;
            Player to = null;
            foreach (var q in Players.Values)
                if (!q.Downed && q.Hp < q.MaxHp && (to == null || q.Hp / q.MaxHp < to.Hp / to.MaxHp))
                    to = q;
            if (to == null)
                return;
            float heal = ReturnHeal * DoseMultOf(e.NeedleBy);
            for (int i = 0; i < n; i++)
            {
                float a = (to.Pos - e.Pos).Angle + (i - (n - 1) / 2f) * 0.5f;  // they fan out, then curve in
                var b = SpawnBullet(Defs.NeedleShot, e.Pos + V2.FromAngle(a) * e.Radius, V2.FromAngle(a) * ReturnSpeed, 0f, 2.5f, 0f, e.NeedleBy);
                b.Medicine = true;
                b.ReturnTo = to.Id;
                b.HealAlly = heal;
            }
        }

        /// Host, every tick: a returning needle curves in on its player (it gives up if they're gone or down).
        void SteerReturnNeedle(Bullet b, float dt)
        {
            if (!Players.TryGetValue(b.ReturnTo, out var q) || q.Downed)
            {
                b.Life = 0f;
                return;
            }
            float diff = MathX.WrapPi((q.Pos - b.Pos).Angle - b.Vel.Angle);
            float turn = Math.Max(-ReturnTurn * dt, Math.Min(ReturnTurn * dt, diff));
            b.Vel = b.Vel.Rotated(turn);
            b.Pulled = true;  // the host sends its new course
        }
    }
}
