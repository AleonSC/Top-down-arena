using System;

namespace Arena
{
    /// The Quarterstaff (Healer Knight; Docs/Mockups/quarterstaff-rework.html, the Beacon look). The primary button swipes left,
    /// then right, then left again (the swing itself is in FireKnightWeapon, Knight.cs; the screen's look alternates the
    /// direction). Every enemy a swipe hits adds a Jab stack (Player.StaffStacks), up to StaffBaseStacks plus one per extra
    /// projectile, and stacks never fade. The weapon-action key (Left Alt) spends one for a jab: a short cone in front of you that
    /// hurts everything in it, shoves it back, bats bullets and heals as the swipes do. Your game sends Msg.WeaponDash (DashJab)
    /// as with the other weapon moves; the host checks the stacks and the cooldown and lands the jab StaffJabWind later.
    public partial class Game
    {
        public const int StaffBaseStacks = 3;
        public const float StaffSwipeArc = 130f;                       // degrees across
        public const float StaffJabReach = 170f, StaffJabArc = 48f;    // the cone's length and its width in degrees
        public const float StaffJabMult = 30f / 18f;                   // damage against a swipe's
        public const float StaffJabKnock = 110f, StaffJabHeal = 3f;
        public const int StaffJabHealHits = 3;
        public const float StaffJabWind = 0.06f, StaffJabHold = 0.28f, StaffJabCd = 0.32f;  // pull-back before it lands, how long it holds the next swipe off, how soon the key works again

        /// How many stacks the staff can store: three, and one more for each extra projectile.
        public int StaffCap(Player p) => StaffBaseStacks + ExtraShots(p);

        /// Host: a player pressed the weapon-action key with the Quarterstaff, aiming at `aimAt`.
        void HostStaffJab(Player p, V2 aimAt)
        {
            if (p.Weapon.Id != WeaponId.HolyMace || p.Downed || p.StunT > 0f || p.StaffJabCd > 0f || p.SwallowT > 0f || p.StaffStacks == 0 || State == MatchState.GameOver)
                return;
            p.StaffStacks--;
            p.StaffJabCd = StaffJabCd;
            p.FireCd = Math.Max(p.FireCd, StaffJabHold / FireRate(p));  // it takes over from whatever swipe was recovering
            p.SwingCount++;
            V2 toward = aimAt - p.Pos;
            float aim = toward.Length > 1f ? toward.Angle : p.Aim;
            var w = p.Weapon;
            var s = NewSwing(p, aim, StaffJabReach, StaffJabArc, w.Damage * StaffJabMult * p.Stats.DmgMult);
            s.Thrust = true;
            s.Knock = StaffJabKnock;
            s.Deflect = true;
            s.HealPerHit = StaffJabHeal;
            s.HealRange = 220f;
            s.HealHits = StaffJabHealHits;
            s.Sound = Sfx.SwingHeavy;
            s.Delay = StaffJabWind;
            QueueSwing(s);
        }

        /// Host, every tick: the jab's cooldown runs down, and stacks go when the staff is put away or the limit drops.
        void TickStaff(float dt)
        {
            foreach (var p in Players.Values)
            {
                p.StaffJabCd = Math.Max(0f, p.StaffJabCd - dt);
                if (p.Weapon.Id != WeaponId.HolyMace)
                    p.StaffStacks = 0;
                else if (p.StaffStacks > StaffCap(p))
                    p.StaffStacks = (byte)StaffCap(p);
            }
        }
    }
}
