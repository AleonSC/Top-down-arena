using UnityEngine;

namespace Arena
{
    /// The look of the Rooms arena (Rooms.cs, Docs/Rooms.md; mockup Docs/Mockups/rooms.html): the floor grid across the
    /// whole world, a faint tint on the side rooms and hallways so the hub stands out, and a bracket at each doorway.
    /// The walls themselves are layout solids, drawn with the floor plan's (LayoutLook.cs).
    public partial class ArenaApp
    {
        static readonly Rgba RoomTint = new Rgba(0.45f, 0.6f, 1f, 0.025f), HallTint = new Rgba(0.2f, 0.3f, 0.6f, 0.05f), DoorInk = new Rgba(0.373f, 0.941f, 1f);

        void DrawRoomsFloor(Rgba grid)
        {
            V2 lo = Rooms.WorldMin, hi = Rooms.WorldMax;
            foreach (var a in Rooms.Areas)
                if (!a.Hub)
                    FillRect(a.Min.X, a.Min.Y, a.Size.X, a.Size.Y, a.Hall ? HallTint : RoomTint);
            for (float x = lo.X; x <= hi.X + 0.5f; x += 64f)
                Line(new V2(x, lo.Y), new V2(x, hi.Y), 1f, grid);
            for (float y = lo.Y; y <= hi.Y + 0.5f; y += 64f)
                Line(new V2(lo.X, y), new V2(hi.X, y), 1f, grid);
            float a2 = Mathf.Clamp01(game.LayoutAge / 0.35f);
            foreach (var d in Rooms.Doors)  // brackets at the jambs and a faint dashed line across the doorway
            {
                V2 j0 = d.Pos - d.Along * (Rooms.DoorW / 2f), j1 = d.Pos + d.Along * (Rooms.DoorW / 2f), t = d.Across * (Rooms.WallT * 0.9f);
                PhosDash(j0, j1, 10f, 8f, 0f, 1.5f, DoorInk.WithAlpha(0.35f * a2));
                foreach (var j in new[] { j0, j1 })
                {
                    Line(j - t, j + t, 6f, DoorInk.WithAlpha(0.18f * a2));
                    Line(j - t, j + t, 2.5f, DoorInk.WithAlpha(a2));
                }
            }
        }
    }
}
