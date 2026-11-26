using System;

namespace Arena
{
    /// Dazzled (the light status; enemies only): a light hit (the Radiant Orb, Chain Light's cone, standing in a Lumen Ward)
    /// dazzles what it hits for DazzleTime s (refreshed, never stacked). A dazzled enemy's shots stray up to DazzleStray either
    /// way (bosses DazzleBossStray), as the Censer's incense does (SpawnBullet, Combat.cs). Enemy.Dazzled is replicated for the
    /// icon and the glints round its head (Game/ElementStatusLook.cs).
    public partial class Game
    {
        public const float DazzleTime = 3f;
        public const float DazzleStray = 25f * MathX.Pi / 180f, DazzleBossStray = 12f * MathX.Pi / 180f;
        public static readonly Rgba DazzleColor = new Rgba(1f, 0.92f, 0.55f);

        /// Host: a light hit dazzles e (not a PvP player's stand-in).
        internal void Dazzle(Enemy e, float t = DazzleTime)
        {
            if (e == null || e.Stand != null || e.Hp <= 0f || e.SpawnT > 0f)
                return;
            e.DazzleT = Math.Max(e.DazzleT, t);
        }

        /// How far either way a shot from `e` may stray (0: not dazzled).
        static float DazzleStrayOf(Enemy e) => e == null || e.DazzleT <= 0f ? 0f : e.Def.IsBoss ? DazzleBossStray : DazzleStray;

        /// Host, every tick per enemy: the dazzle wears off.
        static void TickDazzle(Enemy e, float dt)
        {
            if (e.DazzleT > 0f)
                e.DazzleT = Math.Max(0f, e.DazzleT - dt);
        }
    }
}
