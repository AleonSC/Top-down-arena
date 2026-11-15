namespace Arena
{
    /// One melee hit, one feel: every close-in hit dazes (Daze.cs) and shoves the enemy MeleeKnock px away from you
    /// (Knock.cs), whatever the weapon. A move with its own push (a finisher, an Alt, the Lance's drives) sets
    /// `Swing.Knock`, or passes `knock`, and that replaces the standard shove (`Swing.NoKnock`: none); the daze is always the same.
    /// To change how every melee hit feels, change it here.
    public partial class Game
    {
        public const float MeleeKnock = 18f;  // the standard melee shove, px (slides over KnockTime)

        /// The push a swing uses: its own if it has one, else the standard shove.
        static float MeleeKnockOf(Swing s) => s.NoKnock ? 0f : s.Knock != 0f ? s.Knock : MeleeKnock;

        /// A melee hit landed on `e`, pushing along `away` (from the attacker; `fallback` if they overlap): daze and shove.
        /// `knock` 0 = the standard shove. Heavies take the daze but aren't moved.
        void MeleeImpact(Enemy e, V2 away, V2 fallback, float knock = 0f)
        {
            Daze(e);
            if (!Heavy(e))
                KnockEnemy(e, (away.IsZero ? fallback : away) * (knock != 0f ? knock : MeleeKnock));
        }
    }
}
