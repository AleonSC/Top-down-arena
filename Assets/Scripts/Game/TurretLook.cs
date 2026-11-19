using UnityEngine;

namespace Arena
{
    /// The Black Market's turrets on the arena floor (Turret.cs), in the Black Market's teal: a heavy plate on four feet, its owner's colour
    /// ringing the mount, a twin-barrelled head that tracks the nearest enemy in range (slowly sweeping with none), and a pip for each
    /// wave it has left. Preview: EXPANDMAP=0 GRIDCORE=1 TURRETS=1.
    public partial class ArenaApp
    {
        void DrawTurrets()
        {
            foreach (var b in game.Placed)
                if (b.Kind == BuildKind.Turret)
                    DrawTurret(b);
        }

        void DrawTurret(PlacedBuild b)
        {
            var ink = BlackMarketInk;
            V2 c = b.Pos;
            float t = game.AnimT;
            Player owner;
            var own = game.Players.TryGetValue(b.Owner, out owner) ? owner.Def.Color : new Rgba(0.4f, 0.64f, 1f);

            // Where it looks: the nearest enemy in range, else a slow sweep.
            float aim = t * 0.6f + b.X * 0.01f;
            float best = Game.TurretRange * Game.TurretRange;
            bool locked = false;
            foreach (var e in game.Enemies.Values)
            {
                float d = (e.Disp - c).LengthSq;
                if (d < best) { best = d; aim = (e.Disp - c).Angle; locked = true; }
            }

            // The plate and its feet.
            for (int k = 0; k < 4; k++)
            {
                var f = c + V2.FromAngle(MathX.Tau / 8f + k * MathX.Tau / 4f) * 27f;
                FillRect(f.X - 6f, f.Y - 6f, 12f, 12f, ink.WithAlpha(0.35f));
                NodeFrame(f.X - 6f, f.Y - 6f, 12f, 12f, 2f, ink.WithAlpha(0.85f));
            }
            Circle(c, 25f, new Rgba(0.03f, 0.08f, 0.09f, 0.95f), 24);
            Arc(c, 25f, 3f, ink.WithAlpha(0.95f), 24);
            Arc(c, 18f, 2.5f, own.WithAlpha(0.9f), 20);  // the owner's ring

            // The head: twin barrels and a housing, a muzzle glow when it has a target.
            var dir = V2.FromAngle(aim);
            var side = new V2(-dir.Y, dir.X);
            for (int s = -1; s <= 1; s += 2)
                Line(c + side * (4.5f * s) + dir * 6f, c + side * (4.5f * s) + dir * 32f, 4f, ink.WithAlpha(0.95f));
            Circle(c, 12f, ink.WithAlpha(0.3f), 16);
            Arc(c, 12f, 2.5f, ink.WithAlpha(1f), 16);
            Circle(c - dir * 5f, 4f, ink.WithAlpha(0.85f), 10);
            if (locked)
            {
                float flash = 0.5f + 0.5f * Mathf.Sin(t * 20f);
                Circle(c + dir * 34f, 4f + 2f * flash, new Rgba(0.75f, 1f, 1f, 0.35f + 0.4f * flash), 10);
            }

            // The waves it has left: a pip each, lit, under the plate.
            for (int i = 0; i < Market.TurretWaves; i++)
            {
                var pp = c + new V2((i - (Market.TurretWaves - 1) / 2f) * 9f, 38f);
                bool lit = i < b.Waves;
                Circle(pp, 3f, lit ? ink.WithAlpha(0.95f) : ink.WithAlpha(0.18f), 8);
            }
        }
    }
}
