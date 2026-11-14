using System;
using System.Collections.Generic;

namespace Arena
{
    /// Survival's arena (Docs/Survival.md): one big room, 3200 x 1800 (four times today's arena), walled all round, with simple
    /// walls in the very middle and the corners. It is a World like the Rooms arena's (World.cs), so the walls, the enemies'
    /// steering and the camera that follows you (World.ViewCentre) work as they do there. No doors, no portals, no vault.
    public static class SurvivalRoom
    {
        public const float W = Game.Width * 2f, H = Game.Height * 2f;

        public static readonly World World = Build();

        /// The wall pieces standing in the room: a plus in the middle (each arm with a gap at the centre) and a bracket in each corner.
        public static Solid[] Cover()
        {
            var l = new List<Solid>();
            float t = Rooms.WallT / 2f, cx = W / 2f, cy = H / 2f;
            // The middle: four arms of a plus, 300 long, leaving a 140-wide gap at the centre for walking through.
            l.Add(World.Box(cx - 440f, cy - t, cx - 70f, cy + t));
            l.Add(World.Box(cx + 70f, cy - t, cx + 440f, cy + t));
            l.Add(World.Box(cx - t, cy - 440f, cx + t, cy - 70f));
            l.Add(World.Box(cx - t, cy + 70f, cx + t, cy + 440f));
            // Each corner: an L bracket 360 long, 160 in from the walls.
            const float inset = 160f, arm = 360f;
            foreach (int sx in new[] { 0, 1 })
                foreach (int sy in new[] { 0, 1 })
                {
                    float x = sx == 0 ? inset : W - inset, y = sy == 0 ? inset : H - inset;
                    float dx = sx == 0 ? 1f : -1f, dy = sy == 0 ? 1f : -1f;
                    l.Add(World.Box(Math.Min(x, x + dx * arm), y - t, Math.Max(x, x + dx * arm), y + t));
                    l.Add(World.Box(x - t, Math.Min(y, y + dy * arm), x + t, Math.Max(y, y + dy * arm)));
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
                Portals = new WorldPortal[0],
                Spans = new[]
                {
                    new WallSpan(false, 0f, 0f, W), new WallSpan(false, H, 0f, W),
                    new WallSpan(true, 0f, 0f, H), new WallSpan(true, W, 0f, H),
                },
                Cover = Cover(),
                Min = V2.Zero,
                VaultMin = V2.Zero,
                Max = new V2(W, H),
            };
        }
    }
}
