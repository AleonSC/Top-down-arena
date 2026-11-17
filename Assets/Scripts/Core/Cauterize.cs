using System;

namespace Arena
{
    /// Cauterize (a status combo, Bleed + Burn): fire landing on a bleeding enemy sears the wound shut. All the bleed it
    /// had left lands at once, times CauterizeMult, and the bleed ends. The bleed's share is credited to whoever made it
    /// bleed, the bonus to whoever brought the fire, so co-op fire never wipes out an ally's bleed. An enemy can't be
    /// cauterized again for CauterizeGap s (a flamethrower would otherwise cash in every new bleed stack at once).
    /// Fire here: the Flamethrower's and the Fireball's ignite, and Incendiary Rounds (Combat.cs). Look: FxKind.Cauterize
    /// (Pos = the enemy, Pos2 = (its id, the burst's damage), Radius = its size; Game/CauterizeLook.cs). Host only.
    public partial class Game
    {
        public const float CauterizeMult = 1.25f, CauterizeGap = 1f, CauterizeFxTime = 0.7f;
        public static readonly Rgba CauterizeColor = new Rgba(1f, 0.93f, 0.78f);  // white-hot

        /// The bleed `e` has left: its damage a second for the seconds it still runs.
        public static float BleedLeft(Enemy e) => e.BleedT > 0f ? e.BleedDps * e.BleedT : 0f;

        /// Fire from `burner` just landed on `e`: if it's bleeding, cash the bleed in.
        void Cauterize(Enemy e, int burner)
        {
            float left = BleedLeft(e);
            if (left <= 0f || e.Hp <= 0f || e.CauterT > 0f)
                return;
            int bleeder = e.BleedBy;
            e.BleedT = e.BleedDps = 0f;
            e.BleedHits = e.BleedStacks = 0;
            e.CauterT = CauterizeGap;
            DotDamage(e, left, bleeder);                         // the bleed's own damage, for the bleeder
            DotDamage(e, left * (CauterizeMult - 1f), burner);   // the bonus, for the burner
            BroadcastFx(FxKind.Cauterize, e.Pos, new V2(e.Id, left * CauterizeMult), CauterizeColor, e.Radius, CauterizeFxTime, Sfx.ShotFireballBurn);
        }
    }
}
