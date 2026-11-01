using System;

namespace Arena
{
    /// How each class looks in the arena, beside its KitDef (Classes.cs) on the front end: the body drawn over the
    /// player's circle (not while downed) and anything under them. Either can be null.
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
            new KitLook(),                                                                    // Soldier: the plain role marker
            new KitLook { Body = (a, p, r, t, col) => a.DrawMageMark(p, r) },                // Elementalist (Kit.Elementalist)
            new KitLook { Body = (a, p, r, t, col) => a.DrawSummonerMark(p, r, t) },         // Summoner
            new KitLook { Body = (a, p, r, t, col) => a.DrawKnight(p, r, t) },               // Knight (WorldRenderer.cs)
            new KitLook { Body = (a, p, r, t, col) => a.DrawNinja(p, r, t, col),             // Ninja (WorldRenderer.cs)
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

        /// The Elementalist's four-point star.
        void DrawMageMark(Player p, float r)
        {
            for (int k = 0; k < 4; k++)
                Line(p.Disp + V2.FromAngle(MathX.Pi / 4f + k * MathX.Pi / 2f) * (r * 0.35f),
                     p.Disp + V2.FromAngle(MathX.Pi / 4f + k * MathX.Pi / 2f) * (r + 6f), 2f, new Rgba(0.8f, 0.55f, 1f, 0.8f));
        }

        /// The Summoner's three orbiting motes.
        void DrawSummonerMark(Player p, float r, float t)
        {
            for (int k = 0; k < 3; k++)
                Circle(p.Disp + V2.FromAngle(t * 2f + MathX.Tau * k / 3f) * (r + 7f), 2.5f, new Rgba(0.6f, 1f, 0.7f, 0.9f), 8);
        }
    }
}
