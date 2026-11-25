using System;

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

        /// From wave BottleFromWave, some Rioters (BottleChance of them, by id: every game can tell) carry one bottle and throw it at a
        /// player BottleMin to BottleMax px away: a BottleWind s wind-up (Enemy.Phase 1: it raises its arm and a dashed line marks the
        /// throw, WardenLook.cs), then a bottle that spins out BottleLife s. Phase 2: no bottle (spawned before BottleFromWave, or
        /// thrown; it doesn't get another). Its first throw waits Enemy.LarderCd.
        public const float BottleChance = 0.3f, BottleMin = 110f, BottleMax = 300f, BottleWind = 0.6f, BottleSpeed = 330f, BottleLife = 1.0f;
        public const int BottleFromWave = 2;

        public static bool BottleRioter(Enemy e) => e.Type == EnemyType.Rioter && e.Phase != 2 && IdRoll(e.Id, 11) < BottleChance;

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
                e.Phase = 2;  // thrown: no bottle left
                float ang = (tpos - e.Pos).Angle;
                V2 dir = V2.FromAngle(ang);
                var b = SpawnBullet(Defs.BottleShot, e.Pos + dir * e.Radius, dir * BottleSpeed, 0f, BottleLife, Defs.BulletDamage(Defs.BottleShot), 0, false);
                b.Source = e.Type;
                b.Shooter = e.Id;
                BroadcastFx(FxKind.Spark, e.Pos + dir * (e.Radius + 10f), new Rgba(0.6f, 0.9f, 0.5f), 14f, 0.15f, Sfx.RiotWhoosh);
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
            foreach (var o in EnemiesOf(EnemyType.Guard))  // (EnemyIndex.cs)
                if (o.SpawnT <= 0f && o.Pos.CloserThan(e.Pos, GuardRallyRadius) && StillIn(o))
                    return GuardRallySpeedUp;
            return 1f;
        }

        /// The Rioter crowd: each Rioter within CrowdGap px of your body (edge to edge) slows you to CrowdSlowEach of your
        /// speed, compounding (1: 0.96, 3: 0.88, 6: 0.78, 10: 0.66), never below CrowdSlowMin. Barely felt until they pile on.
        /// Any game: it reads replicated Rioter positions, so your own game slows your walk (Game.cs) with nothing sent.
        public const float CrowdGap = 6f, CrowdSlowEach = 0.96f, CrowdSlowMin = 0.5f;

        public float CrowdSlow(Player p)
        {
            float slow = 1f;
            foreach (var e in Enemies.Values)
                if (e.Type == EnemyType.Rioter && e.Hp > 0f && e.SpawnT <= 0f && e.Pos.CloserThan(p.Pos, e.Radius + p.Def.BodyR + CrowdGap))
                    slow *= CrowdSlowEach;
            return Math.Max(CrowdSlowMin, slow);
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
