using UnityEngine;

namespace Arena
{
    /// The Relic Gauntlet's lasers (RelicGauntlet.cs, Docs/MysteryDoors.md): red beams with a white core, the rail slots they sweep along the walls with
    /// their emitters, and the spinner's hub. Pits and turrets are drawn by their own looks. Hidden while the room is dark.
    public partial class ArenaApp
    {
        static readonly Rgba LaserRed = new Rgba(1f, 0.23f, 0.31f);
        readonly System.Collections.Generic.List<RelicBeam> laserBuf = new System.Collections.Generic.List<RelicBeam>();

        void DrawRelicGauntlets(float t)
        {
            if (!game.ExpandingOn)
                return;
            foreach (var r in game.RelicGauntletRooms())
            {
                V2 mid = new V2((r.X + r.X1) * 0.5f, (r.Y + r.Y1) * 0.5f);
                if (game.FogHidesStill(mid))
                    continue;
                // rail slots along both walls of the near cell, and the hazard cells' warning chevrons at the floor's seams
                for (int side = 0; side < 2; side++)
                    Line(RelicGauntlet.ToWorld(r, 90f, side * 600f + (side == 0 ? 3f : -3f)), RelicGauntlet.ToWorld(r, 510f, side * 600f + (side == 0 ? 3f : -3f)), 3f, LaserRed.WithAlpha(0.35f));
                V2 hub = RelicGauntlet.Hub(r);
                Circle(hub, 22f, new Rgba(0.08f, 0.1f, 0.14f, 0.95f), 20);
                Arc(hub, 24f, 3f, LaserRed.WithAlpha(0.9f), 20);
                Arc(hub, RelicGauntlet.SpinArm, 2f, LaserRed.WithAlpha(0.08f), 48);
                laserBuf.Clear();
                RelicGauntlet.Beams(r, game.RelicGauntletClock, laserBuf);
                float pulse = 0.85f + 0.15f * Mathf.Sin(t * 18f);
                foreach (var b in laserBuf)
                {
                    Line(b.A, b.B, b.Half * 4.2f, LaserRed.WithAlpha(0.16f * pulse));
                    Line(b.A, b.B, b.Half * 2.2f, LaserRed.WithAlpha(0.5f * pulse));
                    Line(b.A, b.B, b.Half * 0.9f, new Rgba(1f, 0.92f, 0.94f, 0.95f));
                }
            }
        }
    }
}
