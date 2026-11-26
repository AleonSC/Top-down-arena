using System;

namespace Arena
{
    /// The Fireball's two variants (Variants.cs, picked in its Variants window): the **Waterball** rolls on through every
    /// enemy like the Fireball but soaks them instead of burning (wet: slowed, and lightning arcs to them, Tsunami.cs);
    /// the **Earthball** is heavier and slower, stops on the first enemy, stuns it (shorter on heavies and bosses) and
    /// breaks into rubble that hurts everything round it (the Splash, 60%). Host only; the look is Game/ElementBallLook.cs.
    public partial class Game
    {
        public const float WaterballWet = 4f;  // s of wet a Waterball hit leaves
        public const float EarthStun = 0.8f, EarthStunHeavy = 0.4f, EarthStunBoss = 0.2f, EarthRubble = 70f;  // the Earthball's stun and its rubble's radius

        /// Fire a Waterball or an Earthball (and `extra` more, fanned out).
        void FireElementBall(Player p, WeaponDef w, float aim, V2 muzzle, float dmg, int extra)
        {
            bool water = w.Id == WeaponId.Waterball;
            for (int i = 0; i <= extra; i++)
            {
                var vel = V2.FromAngle(Fanned(aim, i, extra + 1, 0.22f)) * w.Speed;
                if (water)
                {
                    // It passes through everything already, so Piercing Rounds roll it farther instead (as the Fireball).
                    var b = SpawnBullet(Defs.WaterballShot, muzzle, vel, 0f, w.Life * PierceReach(p), w.Damage * dmg, p.Id);
                    b.Pierce += 99;
                }
                else
                {
                    var b = SpawnBullet(Defs.EarthballShot, muzzle, vel, 0f, w.Life, w.Damage * dmg, p.Id);
                    b.Splash = EarthRubble * AreaMult(p);
                }
            }
        }

        /// A Waterball or Earthball hit on `e` (OnBulletHit).
        void ElementBallHit(Bullet b, Enemy e)
        {
            if (b.Kind == Defs.WaterballShot)
            {
                e.WetT = Math.Max(e.WetT, WaterballWet);
            }
            else if (b.Kind == Defs.EarthballShot)
            {
                Stun(e, e.Def.IsBoss ? EarthStunBoss : e.Def.Heavy ? EarthStunHeavy : EarthStun);
                e.Winding = false;  // a wind-up it was in starts over
                Embrittle(e, b.Owner);  // earth: it and the rubble round it crack (Brittle.cs)
                foreach (var o in FoesInCircle(e.Pos, b.Splash, by: b.Owner))
                    Embrittle(o, b.Owner);
                BroadcastFx(FxKind.Debris, e.Pos, Defs.BulletColor(Defs.EarthballShot), b.Splash, 0.45f, Sfx.StoneHit);
            }
        }
    }
}
