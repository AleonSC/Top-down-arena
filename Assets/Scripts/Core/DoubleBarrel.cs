using System;

namespace Arena
{
    /// The Soldier DPS's Double Barrel (the DPS weapon on the lobby's 4th key; Docs/Mockups/double-barrel.html): a side-by-side shotgun.
    ///   Fire: each click fires one barrel (DbPellets pellets of DbDamage, in a tight random cone), left then right, DbInterval apart.
    ///     Each pellet pierces DbPierce enemies and does less the farther it has flown: full to DbFull px, falling to DbMinMult at DbReach.
    ///   Two shells (Player.DbUsed counts the spent ones), then a break-open reload of DbReloadTime (Player.DbReload counts it down; it
    ///     speeds up with fire rate). No firing while it is open.
    ///   Alt (Msg.WeaponDash, DashBoth): with both shells loaded, fires both barrels at once, 2 x the pellets in a wider cone, then reloads.
    ///   Extra projectiles add DbExtraPellets a barrel. Player.DbBarrel is the barrel that fires next (0 left, 1 right): replicated,
    ///   the look reads it with DbUsed and DbReload (Game/DoubleBarrelLook.cs).
    public partial class Game
    {
        public const int DbPellets = 9, DbExtraPellets = 4, DbPierce = 2, DbShells = 2;
        public const float DbFull = 140f, DbReach = 400f, DbMinMult = 0.35f, DbCone = 0.13f, DbBothCone = 0.17f, DbInterval = 0.32f, DbReloadTime = 0.86f, DbBarrelGap = 5f;

        /// How much of a pellet's damage is left after it has flown `d` px.
        public static float DbFalloff(float d) => d <= DbFull ? 1f : Math.Max(DbMinMult, 1f - (d - DbFull) / (DbReach - DbFull) * (1f - DbMinMult));

        float RangeMult(Bullet b) => b.FallOrigin.IsZero ? 1f : DbFalloff(b.Pos.DistanceTo(b.FallOrigin));

        /// Host, every tick the Double Barrel is out: the reload, and the trigger.
        void TickDoubleBarrel(Player p, float dt)
        {
            if (p.DbReload > 0f)
            {
                p.DbReload -= dt * p.Stats.FireRateMult;
                if (p.DbReload <= 0f)
                {
                    p.DbReload = 0f;
                    p.DbUsed = 0;
                    p.DbBarrel = 0;
                    Sounds.Add(Sfx.TankShellLoad);
                }
                return;
            }
            if (p.DbUsed >= DbShells)
            {
                p.DbReload = DbReloadTime;
                return;
            }
            if (p.Firing && !p.ParryActive && !p.GreatGuard && !p.RapierGuard && !p.RepelPull && !p.KusariWhirl && p.FireCd <= 0f && State != MatchState.GameOver)
            {
                p.FireCd = DbInterval / FireRate(p);
                FirePlayer(p);  // (FireVolley: one barrel)
            }
        }

        /// One or both barrels: the pellets, the flash and the spent shells. Called from FireVolley (one barrel) and HostBothBarrels.
        void FireDoubleBarrel(Player p, float aim, float dmg, int extra, bool both)
        {
            var w = p.Weapon;
            V2 dir = V2.FromAngle(aim), side = dir.Orthogonal();
            int n = DbPellets + DbExtraPellets * extra;
            float cone = both ? DbBothCone : DbCone;
            for (int k = 0; k < (both ? 2 : 1); k++)
            {
                int barrel = both ? k : p.DbBarrel;
                V2 muzzle = p.Pos + dir * (p.Def.BodyR + 12f) + side * ((barrel == 0 ? -1f : 1f) * DbBarrelGap);
                for (int i = 0; i < n; i++)
                {
                    float a = aim + (i == 0 ? 0f : (Rand(-1f, 1f) + Rand(-1f, 1f)) * 0.5f * cone) + (both ? (barrel == 0 ? -0.025f : 0.025f) : 0f);
                    var b = SpawnBullet(Defs.DpsShot, muzzle, V2.FromAngle(a) * w.Speed * Rand(0.94f, 1.06f), 0f, w.Life, w.Damage * dmg, p.Id);
                    b.Pierce += DbPierce;
                    b.Bolts = true;  // it pierces: Ricochet flings bounce bolts off it
                    b.FallOrigin = muzzle;
                }
                p.DbUsed++;
            }
            if (!both)
                p.DbBarrel = (byte)(1 - p.DbBarrel);
            p.DbShot = (byte)(p.DbShot + 1);  // (the look counts volleys by this)
            if (p.DbUsed >= DbShells)
                p.DbReload = DbReloadTime;
        }

        /// Host: Alt. Both barrels at once, when both are loaded.
        void HostBothBarrels(Player p, V2 aimAt)
        {
            if (p.Weapon.Id != WeaponId.DoubleBarrel || p.StunT > 0f || p.DbUsed != 0 || p.DbReload > 0f || p.FireCd > 0f || p.ParryActive || State == MatchState.GameOver)
                return;
            V2 toward = aimAt - p.Pos;
            float aim = toward.Length > 1f ? toward.Angle : p.Aim;
            firingWeapon = true;
            FireDoubleBarrel(p, aim, p.Stats.DmgMult * volleyShare, ExtraShots(p), true);
            firingWeapon = false;
            p.FireCd = DbInterval / FireRate(p);
        }
    }
}
