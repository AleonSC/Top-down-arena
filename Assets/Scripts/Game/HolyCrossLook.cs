using UnityEngine;

namespace Arena
{
    /// Consecrate's Holy Cross (Core/HolyCross.cs): glowing gold bars with a white-hot core and a halo ring (look B of the mockup),
    /// flying straight and growing, leaving fading echoes of itself along its path (look C's animation). Drawn from the effect's
    /// age alone, so it needs nothing but FxKind.HolyCross (Pos = start, Pos2 = end). Allies it blessed run with a cyan ring
    /// and speed streaks (DrawSwiftness). Mockup: Docs/Mockups/consecrate-cross-v3.html.
    public partial class ArenaApp
    {
        static readonly Rgba HolyGold = new Rgba(1f, 0.82f, 0.4f), HolyCore = new Rgba(1f, 0.98f, 0.9f), SwiftCyan = new Rgba(0.31f, 0.82f, 0.91f);
        const float HolyEchoEvery = 0.07f, HolyEchoLife = 0.5f;

        void DrawHolyCrossFx(Effect fx)
        {
            V2 path = fx.Pos2 - fx.Pos;
            float len = path.Length;
            if (len < 1f)
                return;
            V2 dir = path / len;
            float age = fx.T;
            // The cast flash at the caster's feet.
            if (age < 0.3f)
                Arc(fx.Pos, 18f + age * 140f, 4f, HolyGold.WithAlpha(1f - age / 0.3f), 32);
            // Echoes: where the cross was every HolyEchoEvery seconds, fading over HolyEchoLife.
            for (int j = 1; j * HolyEchoEvery <= Game.HolyTime; j++)
            {
                float at = j * HolyEchoEvery, since = age - at;
                if (since <= 0f || since >= HolyEchoLife)
                    continue;
                DrawHolyCross(fx.Pos, dir, len, at, 0.28f * (1f - since / HolyEchoLife), false);
            }
            if (age <= Game.HolyTime)
            {
                float fade = age > Game.HolyTime - 0.25f ? (Game.HolyTime - age) / 0.25f : 1f;
                DrawHolyCross(fx.Pos, dir, len, age, fade, true);
            }
        }

        /// One cross at flight time `at`: two gradient bars (bright in the middle), a white core and a halo.
        void DrawHolyCross(V2 start, V2 dir, float len, float at, float a, bool glow)
        {
            if (a <= 0.01f)
                return;
            float reach = Game.HolyReach(at);
            V2 c = start + dir * (len * reach), side = dir.Orthogonal();
            float arm = Game.HolyArmMin + (Game.HolyArmMax - Game.HolyArmMin) * reach;
            float half = Game.HolyHalfMin + (Game.HolyHalfMax - Game.HolyHalfMin) * reach;
            if (glow)
                Circle(c, arm * 0.55f, HolyGold.WithAlpha(0.12f * a), 24);
            var edge = HolyGold.WithAlpha(0.55f * a);
            var mid = new Rgba(1f, 0.93f, 0.67f, 0.95f * a);
            Bar(c, dir, side, arm, half, edge, mid);
            Bar(c, side, dir, arm, half, edge, mid);
            if (glow)
            {
                Line(c - dir * arm * 0.9f, c + dir * arm * 0.9f, half * 0.56f, HolyCore.WithAlpha(a));
                Line(c - side * arm * 0.9f, c + side * arm * 0.9f, half * 0.56f, HolyCore.WithAlpha(a));
            }
            Arc(c, half * 2.6f, glow ? 3f : 2f, new Rgba(1f, 0.95f, 0.77f, a), 28);
        }

        /// A bar along `ax`, `arm` long each way and `half` thick each side, edge colour at its ends and `mid` in the middle.
        void Bar(V2 c, V2 ax, V2 side, float arm, float half, Rgba edge, Rgba mid)
        {
            V2 a0 = c - ax * arm, a1 = c, a2 = c + ax * arm;
            V2 s = side * half;
            VTri(a0 + s, edge, a0 - s, edge, a1 + s, mid);
            VTri(a0 - s, edge, a1 - s, mid, a1 + s, mid);
            VTri(a1 + s, mid, a1 - s, mid, a2 + s, edge);
            VTri(a1 - s, mid, a2 - s, edge, a2 + s, edge);
        }

        /// A player with Swiftness up: a cyan ring and speed streaks trailing behind where they face.
        void DrawSwiftness(Player p, float t)
        {
            float r = p.Def.BodyR;
            float fade = p.SwiftT < 1f ? p.SwiftT : 1f;
            Arc(p.Disp, r + 6f, 2f, SwiftCyan.WithAlpha(0.8f * fade), 24);
            V2 back = -V2.FromAngle(p.Aim), side = back.Orthogonal();
            for (int i = -1; i <= 1; i++)
            {
                float len = 14f + 8f * Mathf.Abs(Mathf.Sin(t * 14f + i * 2f));
                V2 from = p.Disp + back * (r + 4f) + side * (i * r * 0.55f);
                Line(from, from + back * len, 2f, SwiftCyan.WithAlpha(0.55f * fade));
            }
        }
    }
}
