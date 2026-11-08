using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Storm Hammer (Elementalist Tank; Docs/Mockups/storm-hammer-throw.html and storm-hammer-slam.html, the Thunderhead and
    /// Pulse looks). The primary button throws it: the hammer in your hand rises over your shoulder and snaps forward, and the
    /// projectile leaves your hand HammerThrowRelease seconds after the click (FireVolley queues it, TickHammerThrows spawns it).
    /// The weapon-action key (Left Alt) slams it into the floor instead: HammerSlamWind later a shockwave runs out from a point
    /// HammerSlamReach ahead of you, hurting, shoving and electrifying every enemy it crosses (HostHammerSlam, TickHammerSlams).
    /// Your game sends Msg.WeaponDash (DashHammerSlam); the host checks the cooldown and does the rest. Screens draw the pose from
    /// FxKind.HammerThrow (Pos2.X = 0 a throw, 1 a slam's wind-up; Radius = the player's id) and the shockwave from FxKind.HammerSlam.
    public partial class Game
    {
        public const float HammerThrowRaise = 0.16f, HammerThrowRelease = 0.22f;     // the arm drawn back, then the release (the hammer leaves the hand)
        public const float HammerThrowHide = 0.5f, HammerThrowBack = 0.75f, HammerThrowDur = 0.8f;  // the hand is empty until this, the new hammer fades in until the next
        public const float HammerSlamWind = 0.58f, HammerSlamRaise = 0.5f;         // click to impact; the hammer is overhead by HammerSlamRaise
        public const float HammerSlamHold = 1.5f, HammerSlamBack = 2.0f;            // it stays in the floor until here, then re-forms in the hand
        public const float HammerSlamReach = 52f, HammerSlamRadius = 280f, HammerSlamSpeed = 560f;  // how far ahead it lands, how far the wave runs, how fast
        public const float HammerSlamDmg = 1f, HammerSlamKnock = 40f, HammerSlamCd = 6f;  // x the throw's damage; the shove (not on bosses or heavies); seconds between slams
        public const int HammerSlamJumps = 1;                                         // arcs of the electrify an enemy the wave crossed gets
        public static readonly Rgba HammerStormColor = new Rgba(0.49f, 0.75f, 1f);

        class HammerThrowRun { public int Owner, Extra; public float T, Dmg; }
        class HammerSlamRun
        {
            public int Owner;
            public float T, Dmg, Age, Radius;
            public bool Landed;
            public V2 At;
            public HashSet<int> Hit = new HashSet<int>();
        }

        readonly List<HammerThrowRun> hammerThrows = new List<HammerThrowRun>();
        readonly List<HammerSlamRun> hammerSlams = new List<HammerSlamRun>();
        readonly Dictionary<int, float> hammerSlamCd = new Dictionary<int, float>();
        float hammerSlamLocalCd;

        /// Host: the primary button was pressed with the Storm Hammer (FireVolley): the throw starts now and the hammer leaves
        /// the hand HammerThrowRelease later.
        void QueueHammerThrow(Player p, int extra, float dmg)
        {
            hammerThrows.Add(new HammerThrowRun { Owner = p.Id, Extra = extra, Dmg = dmg, T = HammerThrowRelease });
            BroadcastFx(FxKind.HammerThrow, p.Pos, new V2(0f, 0f), HammerStormColor, p.Id, HammerThrowDur, Sfx.None);
        }

        /// Host, every tick: throws that have wound up leave the hand along the aim as it is now.
        void TickHammerThrows(float dt)
        {
            for (int i = hammerThrows.Count - 1; i >= 0; i--)
            {
                var run = hammerThrows[i];
                if ((run.T -= dt) > 0f)
                    continue;
                hammerThrows.RemoveAt(i);
                Player p;
                if (!Players.TryGetValue(run.Owner, out p) || p.Downed || p.Weapon.Id != WeaponId.StormHammer)
                    continue;
                var w = p.Weapon;
                V2 dir = V2.FromAngle(p.Aim);
                V2 muzzle = p.Pos + dir * p.Def.BodyR;  // (as every shot: the aim line is drawn to match)
                SpawnBullet(Defs.HammerShot, muzzle, dir * w.Speed, 0f, w.Life, w.Damage * (1f + HammerGrow * run.Extra) * run.Dmg, p.Id).Shock = 3 + run.Extra;
                BroadcastSound(Sfx.Throw);
            }
        }

        /// The hammer landed on an enemy: a bolt from the sky, a flash and a ring (FxKind.Strike, as the Nimbus's).
        void HammerStrikeFx(V2 at)
        {
            BroadcastFx(FxKind.Strike, at, HammerStormColor, 54f, 0.5f, Sfx.None);
        }

        /// Host: a player pressed the weapon-action key with the Storm Hammer, aiming at `aimAt`.
        void HostHammerSlam(Player p, V2 aimAt)
        {
            if (p.Weapon.Id != WeaponId.StormHammer || p.Downed || p.StunT > 0f || p.SwallowT > 0f || State == MatchState.GameOver)
                return;
            float cd;
            if (hammerSlamCd.TryGetValue(p.Id, out cd) && cd > 0f)
                return;
            hammerSlamCd[p.Id] = HammerSlamCd;
            p.FireCd = Math.Max(p.FireCd, HammerSlamWind + 0.4f);  // it takes over from the throw
            V2 toward = aimAt - p.Pos;
            if (toward.Length > 1f)
                p.Aim = toward.Angle;
            hammerThrows.RemoveAll(r => r.Owner == p.Id);  // (a throw still winding up is dropped)
            hammerSlams.Add(new HammerSlamRun { Owner = p.Id, T = HammerSlamWind, Dmg = p.Weapon.Damage * HammerSlamDmg * p.Stats.DmgMult * (1f + HammerGrow * ExtraShots(p)), Radius = HammerSlamRadius * AreaMult(p) });
            BroadcastFx(FxKind.HammerThrow, p.Pos, new V2(1f, 0f), HammerStormColor, p.Id, HammerSlamBack, Sfx.None);
        }

        /// Host, every tick: slams count down to the impact; then the wave runs out and each enemy it reaches is hurt once.
        void TickHammerSlams(float dt)
        {
            foreach (var id in new List<int>(hammerSlamCd.Keys))
                hammerSlamCd[id] = Math.Max(0f, hammerSlamCd[id] - dt);
            for (int i = hammerSlams.Count - 1; i >= 0; i--)
            {
                var run = hammerSlams[i];
                Player p;
                if (!Players.TryGetValue(run.Owner, out p) || p.Downed || p.Weapon.Id != WeaponId.StormHammer)
                {
                    hammerSlams.RemoveAt(i);
                    continue;
                }
                if (!run.Landed)
                {
                    if ((run.T -= dt) > 0f)
                        continue;
                    run.Landed = true;
                    run.At = p.Pos + V2.FromAngle(p.Aim) * HammerSlamReach;
                    BroadcastFx(FxKind.HammerSlam, run.At, p.Pos, HammerStormColor, run.Radius, 2.4f, Sfx.Shockwave);
                    p.Run.ShotsFired++;
                }
                run.Age += dt;
                float front = run.Age * HammerSlamSpeed;
                bool any = false;
                foreach (var e in new List<Enemy>(Enemies.Values))
                {
                    if (e.SpawnT > 0f || e.Hp <= 0f || run.Hit.Contains(e.Id))
                        continue;
                    float d = e.Pos.DistanceTo(run.At);
                    if (d - e.Radius > run.Radius)
                    {
                        continue;
                    }
                    if (d - e.Radius > front)
                        continue;
                    run.Hit.Add(e.Id);
                    any = true;
                    HurtEnemy(e, run.Dmg, p.Id);
                    if (e.Hp > 0f)
                    {
                        if (!Heavy(e))
                            KnockEnemy(e, (e.Pos - run.At).Normalized() * HammerSlamKnock);
                        Electrify(e, run.Dmg, HammerSlamJumps, p.Id);
                    }
                }
                if (any)
                    p.Run.ShotsHit++;
                if (front > run.Radius + 60f)
                    hammerSlams.RemoveAt(i);
            }
        }

        /// Your own game: the slam key was pressed with the Storm Hammer (the host checks its own cooldown too).
        bool LocalHammerSlamReady(Player me, bool pressed, float dt)
        {
            hammerSlamLocalCd -= dt;
            if (!pressed || me.Weapon.Id != WeaponId.StormHammer || hammerSlamLocalCd > 0f)
                return false;
            hammerSlamLocalCd = HammerSlamCd;
            return true;
        }
    }
}
