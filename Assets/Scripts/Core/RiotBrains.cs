namespace Arena
{
    /// Step 4 of the enemy behaviour classes (Docs/EnemyBehaviours.md): the Rioters and Guards. A Rioter rushes in a loose
    /// crowd (RioterMove, Warden.cs): Rioter, Worker (a crowd of Workers runs faster: WorkerPackSpeed, HiveBees.cs), Rubble,
    /// Gnawling (it goes for morsels and eats them: LarderFaction.cs). A Guard keeps between you and the Rioters, then wades
    /// in (GuardMove, Warden.cs): Guard, Quarryman, Soldier (charges once close: SoldierCharge, HiveBees.cs), Butcher (its
    /// chop: LarderFaction.cs), Blade Dancer (its lunge and blades: CourtFaction.cs).
    public partial class Game
    {
        sealed class RioterMovePart : MovePart
        {
            public override V2 Move(Game g, Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed) => g.RioterMove(e, tpos);
        }

        /// A Rioter: the crowd rush, faster while a Guard is rallying it (GuardRallySpeed).
        sealed class RallyRioterMovePart : MovePart
        {
            public override V2 Move(Game g, Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed)
            {
                speed *= g.GuardRallySpeed(e);
                return g.RioterMove(e, tpos);
            }
        }

        /// A Guard's baton rally: a Rioter within GuardRallyRadius of a Guard runs GuardRallySpeedUp faster (it doesn't stack).
        public const float GuardRallyRadius = 150f, GuardRallySpeedUp = 1.25f;

        /// A Rioter's speed multiplier from the Guards round it (1 with none near). Host only: it moves the rioter, so every game sees the pace.
        float GuardRallySpeed(Enemy e)
        {
            foreach (var o in Enemies.Values)
                if (o.Type == EnemyType.Guard && o.SpawnT <= 0f && o.Pos.CloserThan(e.Pos, GuardRallyRadius))
                    return GuardRallySpeedUp;
            return 1f;
        }

        sealed class GuardMovePart : MovePart
        {
            readonly bool charges;  // a Soldier: faster once it's close
            public GuardMovePart(bool charges) { this.charges = charges; }

            public override V2 Move(Game g, Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed)
            {
                var v = g.GuardMove(e, tpos);
                if (charges)
                    speed *= g.SoldierCharge(e, dist);
                return v;
            }
        }

        sealed class ChopSpecial : SpecialPart
        {
            public override void Tick(Game g, Enemy e, V2 tpos, float dt) => g.LarderSpecial(e, tpos, dt);
        }

        sealed class EatSpecial : SpecialPart
        {
            public override void Tick(Game g, Enemy e, V2 tpos, float dt) => g.GnawlingEat(e);
        }

        sealed class DanceSpecial : SpecialPart
        {
            public override void Tick(Game g, Enemy e, V2 tpos, float dt) => g.BladeDancerSpecial(e, tpos, dt);
        }

        static void RiotBrains(Brain[] t)
        {
            var rioter = new RioterMovePart();
            var guard = new GuardMovePart(false);
            var rush = new Brain { Move = rioter };
            t[(int)EnemyType.Rioter] = new Brain { Move = new RallyRioterMovePart() };
            t[(int)EnemyType.Worker] = rush;
            t[(int)EnemyType.Rubble] = rush;
            t[(int)EnemyType.Gnawling] = new Brain { Move = rioter, Special = new EatSpecial() };
            var hold = new Brain { Move = guard };
            t[(int)EnemyType.Guard] = new Brain { Move = guard, Special = new GuardSwingSpecial() };  // its baton swing (GuardSwing.cs)
            t[(int)EnemyType.Quarryman] = hold;
            t[(int)EnemyType.Soldier] = new Brain { Move = new GuardMovePart(true) };
            t[(int)EnemyType.Butcher] = new Brain { Move = guard, Special = new ChopSpecial() };
            t[(int)EnemyType.BladeDancer] = new Brain { Move = guard, Special = new DanceSpecial() };
        }
    }
}
