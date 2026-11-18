namespace Arena
{
    /// The Black Market's Turret (Docs/BlackMarket.md): a sentry placed from the stash in the break between waves (the draw screen's Trap
    /// tool, like the other buildables in `Placed`). In a wave it shoots the nearest enemy in range with its owner's shots; it fights
    /// Market.TurretWaves waves and goes when the last is cleared. Each player may have Market.TurretsEach up. Enemies don't attack it.
    /// Game/TurretLook.cs draws it.
    public partial class Game
    {
        public const float TurretRange = 560f, TurretInterval = 0.3f, TurretShotSpeed = 720f;

        /// A turret shot's damage: it grows with the wave, so one placed early keeps up.
        public static float TurretDamage(int wave) => 12f + 1.5f * System.Math.Max(0, wave);

        /// The turrets this player has standing.
        public int TurretsOf(int id)
        {
            int n = 0;
            foreach (var b in Placed)
                if (b.Kind == BuildKind.Turret && b.Owner == id)
                    n++;
            return n;
        }

        /// Host, every tick: in a wave each turret shoots the nearest enemy in range.
        void TickTurrets(float dt)
        {
            if (Placed.Count == 0 || !Battle)
                return;
            for (int i = 0; i < Placed.Count; i++)
            {
                var b = Placed[i];
                if (b.Kind != BuildKind.Turret)
                    continue;
                b.Cd -= dt;
                if (b.Cd <= 0f)
                {
                    var e = NearestEnemy(b.Pos, TurretRange);
                    if (e != null)
                    {
                        b.Cd = TurretInterval;
                        V2 dir = (e.Pos - b.Pos).Normalized();
                        SpawnBullet(Defs.TurretShot, b.Pos + dir * 26f, dir * TurretShotSpeed, 0f, TurretRange / TurretShotSpeed, TurretDamage(Wave), b.Owner);
                    }
                }
                Placed[i] = b;
            }
        }

        /// Host, when a wave is cleared: every turret has one wave fewer; the ones that fought their last go.
        void AgeTurrets()
        {
            bool changed = false;
            for (int i = Placed.Count - 1; i >= 0; i--)
            {
                var b = Placed[i];
                if (b.Kind != BuildKind.Turret)
                    continue;
                changed = true;
                if (b.Waves <= 1)
                {
                    Placed.RemoveAt(i);
                    DoLog("a turret's " + Market.TurretWaves + " waves are up");
                    continue;
                }
                b.Waves--;
                Placed[i] = b;
            }
            if (changed)
                BroadcastExpansion();
        }
    }
}
