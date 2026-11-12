namespace Arena
{
    /// A Brute enrages as it is shot: the more of its health is gone, the faster it walks and the harder it hits.
    /// Stateless (worked out from Hp / MaxHp, which every peer has), so a heal calms it down again.
    public partial class Game
    {
        public const float BruteEnrageSpeed = 0.8f, BruteEnrageDamage = 0.6f;  // extra speed / contact damage at the brink of death

        /// 0 at full health up to 1 as it nears death (Brutes only).
        public static float BruteRage(Enemy e)
        {
            if (!Game.IsBrute(e.Type) || e.MaxHp <= 0f) return 0f;
            return MathX.Clamp01(1f - e.Hp / e.MaxHp);
        }

        public static float BruteSpeedMult(Enemy e) => 1f + BruteEnrageSpeed * BruteRage(e);
        public static float BruteDamageMult(Enemy e) => 1f + BruteEnrageDamage * BruteRage(e);
    }
}
