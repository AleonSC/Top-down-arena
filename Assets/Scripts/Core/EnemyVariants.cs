namespace Arena
{
    /// Enemy variants: a look and a twist on an enemy that spawns in its place now and then (same type, brain and kin,
    /// so everything that asks "is it a Rioter?" still says yes). `Enemy.Variant` (0: the regular one) is rolled on the
    /// host at spawn from the enemy's id (no rng, so the rest of the run plays out the same) and replicated for the look.
    /// To add one: a value at the end of `EnemyVariant`, a row in `Table`, and the look reading `e.Variant`.
    public enum EnemyVariant : byte { None = 0, BallAndChain = 1 }

    public class EnemyVariantDef
    {
        public EnemyType Base;
        public EnemyVariant Variant;
        public float Chance;          // share of the base enemy's spawns that come in as this
        public float SpeedMult = 1f;
        public float HpMult = 1f;
        public int MinWave = 1;       // appears from this wave on
        public int MaxWave = 999;     // and stops after this one
    }

    public partial class Game
    {
        /// The Rioter's Ball and Chain: drags an iron ball on its ankle, so it runs slower than the Brawler.
        public static readonly EnemyVariantDef[] VariantTable =
        {
            new EnemyVariantDef { Base = EnemyType.Rioter, Variant = EnemyVariant.BallAndChain, Chance = 0.3f, SpeedMult = 0.75f, MinWave = 3 },
        };

        public static float VariantChanceScale = 1f;  // tests: 0 never, a big number always

        /// Host, at spawn: maybe turn a fresh enemy into one of its variants (bosses and children never).
        void RollVariant(Enemy e, int parent)
        {
            if (e.Def.IsBoss || parent != 0)
                return;
            float roll = IdRoll(e.Id, 7);  // CoreDrops.cs
            foreach (var v in VariantTable)
            {
                if (v.Base != e.Type || Wave < v.MinWave || Wave > v.MaxWave)
                    continue;
                float chance = v.Chance * VariantChanceScale;
                if (roll < chance)
                {
                    e.Variant = v.Variant;
                    e.Speed *= v.SpeedMult;
                    e.MaxHp *= v.HpMult;
                    return;
                }
                roll -= chance;
            }
        }

        public static EnemyVariantDef VariantDef(EnemyType type, EnemyVariant v)
        {
            foreach (var d in VariantTable)
                if (d.Base == type && d.Variant == v)
                    return d;
            return null;
        }
    }
}
