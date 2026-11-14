namespace Arena
{
    /// Step 6 of the enemy behaviour classes (Docs/EnemyBehaviours.md): the shooters. The Taser Guard's kind keeps its range
    /// and plants its feet while it charges (PlantMove, shared with the lobbers at their range); the Security Guard's kind
    /// strafes on its own timer and turns at SecurityTurn (SecurityMovePart, SecurityGuard.cs). Each fires its own way:
    /// Taser Guard and Stinger a bolt, the Geomancer a crack and the Cinder Priest a magma bolt (QuarryFaction.cs), the Maw
    /// Priest a fan from its maw (it turns slowly: LarderFaction.cs), the Security Guard and Needler single rounds, the
    /// Second a dagger fan (CourtFaction.cs); the Gallant never shoots (its salute and lunge: GallantSpecial).
    public partial class Game
    {
        /// Keeps Range away, strafing; still while winding up. SlowTurn: turns at PriestTurn (a Maw Priest).
        sealed class PlantMove : MovePart
        {
            readonly float range;
            readonly bool slowTurn;
            public PlantMove(float range, bool slowTurn = false) { this.range = range; this.slowTurn = slowTurn; }

            public override V2 Move(Game g, Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed) =>
                e.Winding ? V2.Zero : KeepDistance(dir, dist, range) + dir.Orthogonal() * e.Strafe * 0.5f;  // plants its feet while it winds up

            public override bool Turn(Game g, Enemy e, float want, V2 tpos, float dt)
            {
                if (!slowTurn)
                    return false;
                g.PriestFace(e, want, dt);
                return true;
            }
        }

        /// Holds SecurityRange, strafing (Strafe, AimStrafe: a Needler strafes harder); turns at SecurityTurn, locked while aiming.
        sealed class SecurityMovePart : MovePart
        {
            readonly float strafe, aimStrafe;
            public SecurityMovePart(float strafe, float aimStrafe) { this.strafe = strafe; this.aimStrafe = aimStrafe; }

            public override V2 Move(Game g, Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed) => g.SecurityMove(e, dir, dist, dt, strafe, aimStrafe);

            public override bool Turn(Game g, Enemy e, float want, V2 tpos, float dt)
            {
                g.SecurityFace(e, tpos, dt);
                return true;
            }
        }

        sealed class BoltFire : FirePart
        {
            readonly byte bolt;
            public BoltFire(byte bolt) { this.bolt = bolt; }
            public override void Fire(Game g, Enemy e, V2 tpos, float aim) => g.TaserFire(e, aim, bolt);
        }

        sealed class CrackFire : FirePart
        {
            public override void Fire(Game g, Enemy e, V2 tpos, float aim) => g.GeomancerCrack(e, tpos, aim);
        }

        sealed class MagmaFire : FirePart
        {
            public override void Fire(Game g, Enemy e, V2 tpos, float aim) => g.CinderBolt(e, aim);
        }

        sealed class MawFire : FirePart
        {
            public override void Fire(Game g, Enemy e, V2 tpos, float aim) => g.MawFan(e);
        }

        sealed class PistolFire : FirePart
        {
            public override void Fire(Game g, Enemy e, V2 tpos, float aim) => g.SecurityFire(e);
        }

        sealed class DaggerFire : FirePart
        {
            public override void Fire(Game g, Enemy e, V2 tpos, float aim) => g.SecondFanFire(e);
        }

        /// Never shoots.
        sealed class HoldFire : FirePart
        {
            public override void Fire(Game g, Enemy e, V2 tpos, float aim) => e.FireT = 99f;
        }

        sealed class SaluteSpecial : SpecialPart
        {
            public override void Tick(Game g, Enemy e, V2 tpos, float dt) => g.GallantSpecial(e, tpos, dt);
        }

        static void ShootBrains(Brain[] t)
        {
            var plant = new PlantMove(TaserRange);
            const BrainRole charge = BrainRole.Charge;
            t[(int)EnemyType.TaserGuard] = new Brain { Move = plant, Roles = charge, Fire = new BoltFire(Defs.TaserBolt) { WindUp = TaserWindUp } };
            t[(int)EnemyType.Stinger] = new Brain { Move = plant, Roles = charge, Fire = new BoltFire(Defs.StingBolt) { WindUp = TaserWindUp } };
            t[(int)EnemyType.Geomancer] = new Brain { Move = plant, Roles = charge, Fire = new CrackFire { WindUp = TaserWindUp } };
            t[(int)EnemyType.CinderPriest] = new Brain { Move = plant, Roles = charge, Fire = new MagmaFire { WindUp = TaserWindUp } };
            t[(int)EnemyType.MawPriest] = new Brain { Move = new PlantMove(TaserRange, true), Roles = charge, Fire = new MawFire { WindUp = TaserWindUp } };
            t[(int)EnemyType.Gallant] = new Brain { Move = plant, Roles = charge, Fire = new HoldFire { WindUp = TaserWindUp }, Special = new SaluteSpecial() };
            var security = new SecurityMovePart(0.7f, 1f);
            const BrainRole guard = BrainRole.Security;
            t[(int)EnemyType.SecurityGuard] = new Brain { Move = security, Roles = guard, Fire = new PistolFire { WindUp = SecurityWindUp } };
            t[(int)EnemyType.Needler] = new Brain { Move = new SecurityMovePart(NeedlerStrafe, NeedlerAimStrafe), Roles = guard, Fire = new PistolFire { WindUp = SecurityWindUp } };
            t[(int)EnemyType.Second] = new Brain { Move = security, Roles = guard, Fire = new DaggerFire { WindUp = SecurityWindUp } };
        }
    }
}
