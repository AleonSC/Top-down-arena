using System;

namespace Arena
{
    /// The Healer Soldier's fourth weapon (the lobby's 4th key), the Twin Pistols: a pistol in each hand
    /// (Docs/Mockups: the Twin Pistols artifact).
    ///   Fire: the guns take turns, left then right (Player.TpSide, replicated), one round every FireInterval, each within
    ///         TpSpread of your aim. A round that hits can knock a tiny health orb loose (TpShed, at most every ShedEvery s
    ///         per enemy, shared with the Repulsion Cannon).
    ///   Alt:  Laser Sights (Msg.WeaponDash WeaponAlt.Laser): for TpLaserDur s (TpLaserT, replicated) both guns fire together every
    ///         TpLaserInterval, their rounds TpLaserMult harder and aimed down the beams to meet at your cursor, knocking orbs
    ///         loose more often (TpLaserShed); you move at TpLaserSlow. TpCd (replicated) counts down from the press.
    ///   Each extra projectile (Multishot): one more round a shot, fanned TpFan apart, and Laser Sights lasts TpLaserPerExtra s longer.
    /// The look is Game/TwinPistolsLook.cs.
    public partial class Game
    {
        public const float TpSpread = 0.026f;          // radians either side of your aim (3° in all)
        public const float TpGunGap = 11f;             // each gun's distance from your middle, sideways
        public const float TpMuzzleOut = 22f;          // how far past your edge the muzzles are
        public const float TpShed = 0.15f, TpLaserShed = 0.25f;  // chance a hit knocks an orb loose
        public const float TpLaserDur = 4f, TpLaserPerExtra = 1.5f;
        public const float TpLaserCd = 12f;            // from the press, so it covers the lasers' time too
        public const float TpLaserInterval = 0.22f;    // both guns at once, this often
        public const float TpLaserMult = 1.4f;
        public const float TpLaserSlow = 0.85f;
        public const float TpMeetMin = 120f;           // the beams never meet closer than this
        public const float TpFan = 0.07f;              // Multishot's extra rounds fan out this far apart (radians)
        public static readonly Rgba TpLaserColor = new Rgba(1f, 0.19f, 0.28f);

        float laserLocalCd;

        /// Where the beams meet: your cursor's distance, from TpMeetMin to the rounds' full range.
        public float TpMeet(Player p) => Math.Max(TpMeetMin, Math.Min(p.Weapon.Speed * p.Weapon.Life * p.Stats.ShotSpeedMult, p.AimAt.DistanceTo(p.Pos)));

        /// Where gun `gun` (0 left, 1 right) holds its muzzle, aiming along `aim`.
        public static V2 TpMuzzle(Player p, float aim, int gun)
        {
            V2.Axes(aim, out V2 v, out V2 side);
            return p.Pos + v * (p.Def.BodyR + TpMuzzleOut) - side * ((gun == 1 ? 1f : -1f) * TpGunGap);  // (side is your left)
        }

        /// How long Laser Sights lasts for `p` (Multishot: longer).
        public float TpLaserTime(Player p) => TpLaserDur + TpLaserPerExtra * ExtraShots(p);

        /// FireVolley: one gun's round, or both down the beams with Laser Sights on.
        void FireTwinPistols(Player p, float aim, float dmg, int extra)
        {
            var w = p.Weapon;
            bool laser = p.TpLaserT > 0f;
            V2 meet = p.Pos + V2.FromAngle(aim) * TpMeet(p);
            for (int gun = 0; gun < 2; gun++)
            {
                if (!laser && gun != p.TpSide)
                    continue;
                V2 from = TpMuzzle(p, aim, gun);
                if (ActiveLayout != null && SegBlocked(ActiveLayout, p.Pos, from, 0f))
                    from = p.Pos + V2.FromAngle(aim) * p.Def.BodyR;  // a gun pushed into a wall fires from your edge
                float a = laser ? (meet - from).Angle : aim + Rand(-TpSpread, TpSpread);
                for (int k = 0; k <= extra; k++)  // (Multishot: more rounds, fanned a little)
                {
                    float fan = k == 0 ? 0f : (k % 2 == 1 ? 1f : -1f) * TpFan * ((k + 1) / 2);
                    var b = SpawnBullet(Defs.HealerShot, from, V2.FromAngle(a + fan) * w.Speed, 0f, w.Life, w.Damage * (laser ? TpLaserMult : 1f) * dmg, p.Id);
                    b.Radius = (laser ? 5f : 4f) * p.Stats.ShotSizeMult;  // Magnum Rounds
                    b.Sheds = laser ? TpLaserShed : TpShed;
                }
            }
        }

        /// FirePlayer, after the volley: the next gun takes its turn; with Laser Sights both fire, on their own clock.
        void TwinPistolsFired(Player p)
        {
            if (p.TpLaserT > 0f)
                p.FireCd = TpLaserInterval / FireRate(p);
            else
                p.TpSide = (byte)(p.TpSide ^ 1);
        }

        /// OnBulletHit: a Twin Pistols round now and then knocks a tiny health orb out of what it hits.
        void TwinShedHit(Bullet b, Enemy e)
        {
            if (b.Sheds <= 0f || e.T < e.ShedT || rng.NextDouble() >= b.Sheds)
                return;
            e.ShedT = e.T + ShedEvery;
            SpawnOrb(e.Pos, false, true);
        }

        /// Host, every tick: the lasers run down and the key cools down; changing weapons or going down turns the lasers off.
        void TickTwinPistols(Player p, float dt)
        {
            p.TpCd = Math.Max(0f, p.TpCd - dt);
            if (p.TpLaserT > 0f)
                p.TpLaserT = p.Weapon.Id != WeaponId.TwinPistols || p.Downed ? 0f : Math.Max(0f, p.TpLaserT - dt);
        }

        /// Your game: the weapon-action key with the Twin Pistols. True when the key belongs to them (pressed or not).
        bool LocalTwinLaser(Player me, bool pressed, V2 aimAt, float dt)
        {
            laserLocalCd -= dt;
            if (me.Weapon.Id != WeaponId.TwinPistols)
                return false;
            if (pressed && laserLocalCd <= 0f && me.TpCd <= 0f)
            {
                laserLocalCd = 0.3f;  // (the host's TpCd arrives with the next snapshot)
                if (IsHost)
                    HostWeaponDash(MyId, WeaponAlt.Laser, aimAt);
                else
                {
                    var w = Proto.Begin(Msg.WeaponDash);
                    w.Write(WeaponAlt.Laser);
                    w.Write(aimAt);
                    session.SendToHost(Proto.End(w));
                }
            }
            return true;
        }

        /// Host: a player switched on Laser Sights.
        void HostTwinLaser(Player p)
        {
            if (p.Weapon.Id != WeaponId.TwinPistols || p.Downed || p.StunT > 0f || p.SwallowT > 0f || p.TpCd > 0f || State == MatchState.GameOver)
                return;
            p.TpLaserT = TpLaserTime(p);
            p.TpCd = TpLaserCd;
            p.FireCd = Math.Min(p.FireCd, 0f);  // both guns are ready right away
            Sounds.Add(Sfx.Pick);
        }
    }
}
