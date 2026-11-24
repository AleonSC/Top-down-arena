using System;

namespace Arena
{
    /// The Flamethrower's two variants (Variants.cs, picked in its Variants window):
    ///   Water Gun: a long, narrow spray of water (Defs.WaterJet). A jet stops on the first enemy it hits and splashes: that enemy
    ///     and every enemy within WaterSplashR of it are soaked WET for WaterGunWet s (Tsunami.cs: slowed, lightning arcs to it,
    ///     and it reacts with fire, wind and ice: Steam.cs, Wind.cs).
    ///   Gale Gun: an air blower (Defs.GaleShot). Its gusts pass through enemies like flames (full damage to the first, a third
    ///     behind) and slice them: a bleed of GaleBleed of the hit a second (KitWeapons.cs ApplyBleed, the shared bleed and its
    ///     stacks), a plain slow (GaleSlow for GaleSlowFor s: Slowed, never Chilled), a light push (not heavies) and wind, so it
    ///     chills wet enemies and fans burning ones (Wind.cs).
    /// Looks: Game/ElementGunLook.cs.
    public partial class Game
    {
        public const float WaterSplashR = 55f, WaterGunWet = 3f;
        public const float GaleBleed = 0.6f, GaleSlow = 0.3f, GaleSlowFor = 0.6f, GalePush = 6f;

        void FireWaterGun(Player p, float aim, V2 muzzle, float dmg, int extra)
        {
            var w = p.Weapon;
            float reach = w.Life * (1f + w.Grow * extra) * PierceReach(p);
            for (int i = 0; i < 1 + extra; i++)
                SpawnBullet(Defs.WaterJet, muzzle, V2.FromAngle(aim + Rand(-0.05f, 0.05f)) * (i == 0 ? w.Speed : Rand(0.9f * w.Speed, w.Speed)), 0f, reach, w.Damage * dmg, p.Id);
        }

        void FireGaleGun(Player p, float aim, V2 muzzle, float dmg, int extra)
        {
            var w = p.Weapon;
            float reach = w.Life * (1f + w.Grow * extra) * PierceReach(p);
            for (int i = 0; i < 2 + extra; i++)
            {
                var b = SpawnBullet(Defs.GaleShot, muzzle, V2.FromAngle(aim + Rand(-0.22f, 0.22f)) * (i == 0 ? w.Speed : Rand(0.8f * w.Speed, w.Speed)), 0f, reach, w.Damage * dmg, p.Id);
                b.Pierce += 99;
            }
        }

        /// A Water Gun jet or a Gale Gun gust landing on e (OnBulletHit, before the damage).
        void ElementGunHit(Bullet b, Enemy e)
        {
            if (b.Kind == Defs.WaterJet)
            {
                foreach (var o in AllFoes(by: b.Owner))
                    if (o == e || o.Pos.CloserThan(e.Pos, WaterSplashR + o.Radius))
                        o.WetT = Math.Max(o.WetT, WaterGunWet);
                if (rng.NextDouble() < 0.25)  // a splash now and then (a stream of them would be noise)
                    BroadcastFx(FxKind.WwSplash, e.Pos, e.Pos, WwColor, WaterSplashR * 0.6f, WwSplashT, Sfx.None);
                return;
            }
            if (b.Kind != Defs.GaleShot)
                return;
            ApplyBleed(e, b.Dmg * GaleBleed, b.Owner);
            if (!b.Pierced)  // full damage to the first enemy, a third to the ones behind (as a flame)
            {
                b.Pierced = true;
                b.Dmg *= FlamePierced;
            }
            Slow(e, GaleSlowFor, 1f - GaleSlow);
            V2 dir = b.Vel.IsZero ? b.Vel : b.Vel.Normalized();
            WindHit(e, dir);  // Wind.cs
            if (!Heavy(e) && !IceFrozen(e))
                KnockEnemy(e, dir * GalePush);
        }
    }
}
