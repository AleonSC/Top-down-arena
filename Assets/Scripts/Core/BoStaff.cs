using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Bo Staff's shove (Docs/Mockups/bo-staff-rework.html). The weapon-action key turns the staff sideways and drives it
    /// out ahead of you as a bar: everything in it takes a little damage and is pushed straight ahead. It also starts the
    /// slash, jab, spin chain over (the chain itself is in FireNinjaWeapon, Ninja.cs). Your game sends Msg.WeaponDash
    /// (DashShove) as with the other weapon moves; the host checks the cooldown and lands the shove BoShoveWind later, once
    /// the staff has been drawn back (the look, Game/BoStaffLook.cs, plays from the same moment).
    public partial class Game
    {
        class BoShove
        {
            public int Owner;
            public float Aim, T;  // where it was aimed when pressed; seconds until it lands
        }

        readonly List<BoShove> boShoves = new List<BoShove>();

        float BoShoveReachOf(Player p) => MeleeReach(p, BoShoveReach + p.Weapon.Grow * ExtraShots(p));

        /// Host: a player pressed the weapon-action key with the Bo Staff, aiming at `aimAt`.
        void HostBoShove(Player p, V2 aimAt)
        {
            if (p.Weapon.Id != WeaponId.BoStaff || p.Downed || p.StunT > 0f || p.ShoveCd > 0f || p.SwallowT > 0f || State == MatchState.GameOver)
                return;
            p.ShoveCd = BoShoveCd;
            p.BoStep = 0;                 // the chain starts over: the next click is a slash
            p.FireCd = BoShoveTime / FireRate(p);  // (it takes over from whatever step was recovering)
            p.SwingCount++;
            V2 toward = aimAt - p.Pos;
            float aim = toward.Length > 1f ? toward.Angle : p.Aim;  // along the cursor they pressed it at (their aim as the host last heard it can be a frame old)
            boShoves.Add(new BoShove { Owner = p.Id, Aim = aim, T = BoShoveWind });
            V2 dir = V2.FromAngle(aim);
            BroadcastFx(FxKind.StaffShove, p.Pos, p.Pos + dir * BoShoveReachOf(p), BoStaffSage, BoShoveHalf, BoShoveTime + 0.45f, Sfx.SwingHeavy);
        }

        /// Host, every tick: shoves that have finished their pull-back land. Everything in the bar (from your body out to its
        /// reach, BoShoveHalf either side of the aim line) takes BoShoveMult of a slash and is pushed BoShoveKnock along the
        /// aim; bosses and heavies take the damage and stay put.
        void TickBoShoves(float dt)
        {
            for (int i = boShoves.Count - 1; i >= 0; i--)
            {
                var sh = boShoves[i];
                if ((sh.T -= dt) > 0f)
                    continue;
                boShoves.RemoveAt(i);
                Player p;
                if (!Players.TryGetValue(sh.Owner, out p) || p.Downed || p.Weapon.Id != WeaponId.BoStaff)
                    continue;
                V2 origin = p.Pos, dir = V2.FromAngle(sh.Aim), side = dir.Orthogonal();
                float reach = BoShoveReachOf(p), dmg = p.Weapon.Damage * BoShoveMult * p.Stats.DmgMult * (1f + BoExtraDmg * ExtraShots(p));
                float push = BoShoveKnock + p.Weapon.Grow * ExtraShots(p);
                p.Run.ShotsFired++;
                bool any = false;
                foreach (var e in new List<Enemy>(Enemies.Values))
                {
                    if (!Targetable(e))
                        continue;
                    V2 rel = e.Pos - origin;
                    float along = Dot(rel, dir), across = Math.Abs(Dot(rel, side));
                    if (along < -e.Radius - p.Def.BodyR || along > reach + e.Radius || across > BoShoveHalf + e.Radius)
                        continue;
                    if (BlocksShot(e, origin))
                    {
                        WearShield(e, dmg);  // a shield stops the staff (and wears)
                        continue;
                    }
                    any = true;
                    float hit = dmg * NinjaHitMult(p.Id, Defs.KnightSlash, e);  // Vanish's ambush (Ninja.cs)
                    HurtEnemy(e, hit, p.Id);
                    Daze(e);  // Daze.cs
                    ModHit(e, p.Id, hit, Defs.KnightSlash, dir);
                    if (e.Hp > 0f && !Heavy(e))
                        KnockEnemy(e, dir * push);  // slides (Knock.cs)
                }
                if (any)
                    p.Run.ShotsHit++;
            }
        }
    }
}
