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
            public override V2 Move(Game g, Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed)
            {
                if (e.Type == EnemyType.Rioter)
                {
                    speed *= g.GuardRallySpeed(e);  // a Guard's rally, below (the Warden's Rioter only: its kin run alone)
                    if (e.Phase == 1)
                        speed *= 0.3f;  // it slows to throw a bottle
                }
                return g.RioterMove(e, tpos);
            }
        }

        /// Some Rioters (BottleChance of them, by id: every game can tell) carry a bottle and now and then throw it at a player
        /// from BottleMin to BottleMax px away: a BottleWind s wind-up (Enemy.Phase 1: it raises its arm), then a bottle that spins out
        /// BottleLife s. Its cooldown is Enemy.LarderCd.
        public const float BottleChance = 0.3f, BottleMin = 110f, BottleMax = 300f, BottleWind = 0.45f, BottleSpeed = 330f, BottleLife = 1.0f, BottleEveryMin = 4f, BottleEveryRange = 3f;

        public static bool BottleRioter(Enemy e) => e.Type == EnemyType.Rioter && IdRoll(e.Id, 11) < BottleChance;

        /// Host, in Special: a bottle Rioter winds up and throws.
        void RioterBottle(Enemy e, V2 tpos, float dt)
        {
            if (!BottleRioter(e) || e.SpawnT > 0f)
                return;
            if (e.Phase == 1)
            {
                e.LarderT -= dt;
                if (e.LarderT > 0f)
                    return;
                e.Phase = 0;
                e.LarderCd = BottleEveryMin + IdRoll(e.Id, 12 + (uint)(e.T / 3f)) * BottleEveryRange;
                float ang = (tpos - e.Pos).Angle;
                V2 dir = V2.FromAngle(ang);
                var b = SpawnBullet(Defs.BottleShot, e.Pos + dir * e.Radius, dir * BottleSpeed, 0f, BottleLife, Defs.BulletDamage(Defs.BottleShot), 0, false);
                b.Source = e.Type;
                b.Shooter = e.Id;
                BroadcastFx(FxKind.Spark, e.Pos + dir * (e.Radius + 10f), new Rgba(0.6f, 0.9f, 0.5f), 14f, 0.15f, Sfx.Throw);
                return;
            }
            e.LarderCd -= dt;
            float d = e.Pos.DistanceTo(tpos);
            if (e.LarderCd <= 0f && d >= BottleMin && d <= BottleMax)
            {
                e.Phase = 1;
                e.LarderT = BottleWind;
            }
        }

        /// A Guard's baton rally: a Rioter within GuardRallyRadius of a Guard runs GuardRallySpeedUp faster (it doesn't stack).
        public const float GuardRallyRadius = 150f, GuardRallySpeedUp = 1.25f;

        /// A Rioter's speed multiplier from the Guards round it (1 with none near). Host only: it moves the rioter, so every game sees the pace.
        /// Any game: is this Rioter within a Guard's rally (the glow under it, WardenLook.cs)?
        public bool Rallied(Enemy e) => e.Type == EnemyType.Rioter && GuardRallySpeed(e) > 1f;

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
            t[(int)EnemyType.Rioter] = new Brain { Move = rioter, Special = new CodeSpecial((g, e, p, dt) => g.RioterBottle(e, p, dt)) };
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
