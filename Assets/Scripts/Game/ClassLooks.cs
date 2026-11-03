using System;

namespace Arena
{
    /// How each class looks in the arena, beside its KitDef (Classes.cs) on the front end: the body drawn over the
    /// player's model (not while downed: CharacterModels.cs draws the body and weapon, this adds ability and buff
    /// feedback over it) and anything under them. Either can be null.
    class KitLook
    {
        public Action<ArenaApp, Player, float, float, Rgba> Body;  // (player, body radius, time, body colour)
        public Action<ArenaApp, Player, float> Auras;              // (player, time): under the player, with the other auras
        public Func<ArenaApp, Bullet, Rgba, float, bool> Shot;     // (shot, its colour, time): draws the class's own shot kinds
                                                                   // and returns true; false for any other kind
    }

    public partial class ArenaApp
    {
        /// One look per class, in Kit order (a new class adds its line; LookOf refuses a missing one).
        static readonly KitLook[] KitLooks =
        {
            new KitLook(),                                                                    // Soldier
            new KitLook(),                                                                    // Elementalist (Kit.Elementalist)
            new KitLook(),                                                                    // Summoner
            new KitLook { Body = (a, p, r, t, col) => a.DrawKnight(p, r, t) },               // Knight (WorldPlayers.cs)
            new KitLook { Body = (a, p, r, t, col) => a.DrawNinja(p, r, t, col),             // Ninja (WorldPlayers.cs)
                          Shot = (a, b, c, t) => a.DrawNinjaShot(b, c, t) },
            new KitLook { Body = (a, p, r, t, col) => a.DrawBloodMage(p, r, t, col),         // Hemomancer (BloodMageLook.cs)
                          Auras = (a, p, t) => a.DrawBloodAuras(p, t),
                          Shot = (a, b, c, t) => a.DrawBloodShot(b, c, t) },
        };

        static KitLook LookOf(Kit k)
        {
            if (KitLooks.Length != Defs.KitCount)
                throw new InvalidOperationException("ClassLooks.cs has " + KitLooks.Length + " looks for " + Defs.KitCount + " classes");
            return KitLooks[(int)k];
        }
    }
}
