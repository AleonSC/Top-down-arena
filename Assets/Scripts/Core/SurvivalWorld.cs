using System;
using System.Collections.Generic;

namespace Arena
{
    /// Survival's arena (Docs/Survival.md): one big room, 3200 x 1800 (four times today's arena), walled all round, with simple
    /// walls in the very middle and the corners. It is a World like the Rooms arena's (World.cs), so the walls, the enemies'
    /// steering and the camera that follows you (World.ViewCentre) work as they do there. No doors, no vault; two pairs of
    /// wall portals (the Expanding arena's, WorldPortal.Half) in the middle of opposite walls: left with right, top with bottom.
    public static class SurvivalRoom
    {
        public const float W = Game.Width * 2f, H = Game.Height * 2f;

        public static readonly World World = Build();

        /// The wall pieces standing in the room: a plus in the middle (each arm with a gap at the centre) and a bracket in each corner.
        public static Solid[] Cover()
        {
            var l = new List<Solid>();
            float cx = W / 2f, cy = H / 2f;  // (every piece on the 50 px floor grid, a square thick: RoomTraps.Tile)
            // The middle: four arms of a plus, 350 long, each half a square off the middle line (a pinwheel), leaving a 200-wide square at the centre.
            l.Add(World.Box(cx - 450f, cy - 50f, cx - 100f, cy));
            l.Add(World.Box(cx + 100f, cy, cx + 450f, cy + 50f));
            l.Add(World.Box(cx, cy - 450f, cx + 50f, cy - 100f));
            l.Add(World.Box(cx - 50f, cy + 100f, cx, cy + 450f));
            // Each corner: an L bracket 350 long, 150 in from the walls.
            const float inset = 150f, arm = 350f, t = 50f;
            foreach (int sx in new[] { 0, 1 })
                foreach (int sy in new[] { 0, 1 })
                {
                    float x0 = sx == 0 ? inset : W - inset - arm, y0 = sy == 0 ? inset : H - inset - t;  // the bar along the wall
                    l.Add(World.Box(x0, y0, x0 + arm, y0 + t));
                    float vx = sx == 0 ? inset : W - inset - t, vy = sy == 0 ? inset : H - inset - arm;  // the bar down the wall
                    l.Add(World.Box(vx, vy, vx + t, vy + arm));
                }
            return l.ToArray();
        }

        static World Build()
        {
            return new World
            {
                Name = "Survival",
                Areas = new[] { new RoomArea { Name = "Room", Min = V2.Zero, Max = new V2(W, H), Hub = true } },
                Doors = new RoomDoor[0],
                Portals = new[]
                {
                    new WorldPortal { A = new V2(0f, H / 2f), B = new V2(W, H / 2f), InA = new V2(1f, 0f), InB = new V2(-1f, 0f), Half = Expansion.LinkHalf },
                    new WorldPortal { A = new V2(W / 2f, 0f), B = new V2(W / 2f, H), InA = new V2(0f, 1f), InB = new V2(0f, -1f), Half = Expansion.LinkHalf },
                },
                Spans = new[]
                {
                    new WallSpan(false, 0f, 0f, W), new WallSpan(false, H, 0f, W),
                    new WallSpan(true, 0f, 0f, H), new WallSpan(true, W, 0f, H),
                },
                Cover = Cover(),
                Min = V2.Zero,
                Max = new V2(W, H),
            };
        }
    }
}
