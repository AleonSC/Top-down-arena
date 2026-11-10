using UnityEngine;

namespace Arena
{
    /// The look of the Rooms arena (Rooms.cs, Docs/Rooms.md; mockup Docs/Mockups/rooms.html): the floor grid across the
    /// whole world, a faint tint on the side rooms and hallways so the hub stands out (the vault in gold), the doors, and
    /// the minimap. The walls themselves are layout solids, drawn with the floor plan's (LayoutLook.cs).
    ///   doors    cyan brackets between the hub and a hallway's neighbours; a room's door is red while its nest lives,
    ///            flashes amber for the 2 s before a spill comes out of it, gets a solid bar across while someone is locked
    ///            in behind it, and turns teal once the room is cleared. The vault's is gold until its mini-boss falls.
    ///   minimap  top right, under the hints: every area (rooms red until cleared, dim until someone has seen them, teal
    ///            once cleared; the vault gold), the doors in their colours with a ring filling up to each room's next spill,
    ///            enemies (spilled runners orange, nests yellow-green, the vault's mini-boss big and orange), the
    ///            players, and the box your screen shows.
    public partial class ArenaApp
    {
        static readonly Rgba OpenRoomTint = new Rgba(0.45f, 0.6f, 1f, 0.03f);
        static readonly Rgba RoomTint = new Rgba(1f, 0.3f, 0.35f, 0.03f), ClearTint = new Rgba(0.25f, 0.9f, 0.7f, 0.035f), HallTint = new Rgba(0.2f, 0.3f, 0.6f, 0.05f),
            VaultTint = new Rgba(1f, 0.62f, 0.3f, 0.06f), DoorInk = new Rgba(0.373f, 0.941f, 1f), DoorRed = new Rgba(1f, 0.3f, 0.36f),
            DoorAmber = new Rgba(1f, 0.7f, 0.28f), DoorTeal = new Rgba(0.25f, 0.9f, 0.7f), DoorGold = new Rgba(1f, 0.82f, 0.4f);

        void DrawRoomsFloor(Rgba grid)
        {
            bool vault = game.VaultOn;
            V2 lo = Rooms.MinFor(vault), hi = Rooms.WorldMax;
            for (int i = 0; i < Rooms.Areas.Length; i++)
            {
                var a = Rooms.Areas[i];
                if (a.Hub || a.Vault && !vault)
                    continue;
                var rs = RoomStateOf(i);
                var tint = a.Vault ? VaultTint : a.Hall ? HallTint : !Rooms.RoomByRoom ? OpenRoomTint : rs != null && rs.Cleared ? ClearTint : RoomTint;
                FillRect(a.Min.X, a.Min.Y, a.Size.X, a.Size.Y, tint);
            }
            for (float x = lo.X; x <= hi.X + 0.5f; x += 64f)
                Line(new V2(x, lo.Y), new V2(x, hi.Y), 1f, grid);
            for (float y = Mathf.Ceil(lo.Y / 64f) * 64f; y <= hi.Y + 0.5f; y += 64f)
                Line(new V2(lo.X, y), new V2(hi.X, y), 1f, grid);
            float a2 = Mathf.Clamp01(game.LayoutAge / 0.35f);
            for (int i = 0; i < Rooms.Doors.Length; i++)
            {
                if (!Rooms.DoorUp(i, vault))
                    continue;
                var d = Rooms.Doors[i];
                bool locked;
                var col = DoorColour(i, out locked);
                V2 j0 = d.Pos - d.Along * (Rooms.DoorW / 2f), j1 = d.Pos + d.Along * (Rooms.DoorW / 2f), t = d.Across * (Rooms.WallT * 0.9f);
                if (locked)  // a solid bar across: you can come in, not go out
                {
                    Line(j0, j1, 10f, col.WithAlpha(0.25f * a2));
                    Line(j0, j1, 4f, col.WithAlpha(0.85f * a2));
                }
                else
                    PhosDash(j0, j1, 10f, 8f, 0f, 1.5f, col.WithAlpha(0.4f * a2));
                foreach (var j in new[] { j0, j1 })
                {
                    Line(j - t, j + t, 6f, col.WithAlpha(0.2f * a2));
                    Line(j - t, j + t, 2.5f, col.WithAlpha(a2));
                }
            }
        }

        RoomState RoomStateOf(int area)
        {
            foreach (var rs in game.RoomStates)
                if (rs.Area == area)
                    return rs;
            return null;
        }

        /// A door's colour, and whether it's locked (someone is shut in behind it).
        Rgba DoorColour(int i, out bool locked)
        {
            var d = Rooms.Doors[i];
            locked = false;
            foreach (int area in new[] { d.A, d.B })
            {
                var a = Rooms.Areas[area];
                if (a.Vault)
                {
                    locked = game.AreaLocked(area) && SomeoneIn(a);
                    return game.VaultCleared ? DoorTeal : DoorGold;
                }
                var rs = RoomStateOf(area);
                if (rs == null || !Rooms.RoomByRoom)
                    continue;  // (the open map: a room's doors are plain doorways)
                if (rs.Cleared)
                    return DoorTeal;
                locked = game.AreaLocked(area) && SomeoneIn(a);
                if (rs.NestAlive && rs.NextDoor == i && rs.SpillT < Rooms.SpillWarn)
                    return Mathf.Repeat(game.AnimT * 6f, 1f) < 0.5f ? DoorAmber : DoorAmber.WithAlpha(0.35f);
                return DoorRed;
            }
            return DoorInk;
        }

        bool SomeoneIn(RoomArea a)
        {
            foreach (var p in game.Players.Values)
                if (a.Contains(p.Disp))
                    return true;
            return false;
        }

        // --- The minimap -------------------------------------------------------------------------------------------

        const float RoomMapW = 230f, RoomMapTop = 40f, RoomMapRight = 16f;

        /// DrawHud: the Rooms arena's minimap (top right).
        void DrawRoomsMinimap()
        {
            if (!game.RoomsOn || !game.Running)
                return;
            bool vault = game.VaultOn;
            V2 lo = Rooms.MinFor(vault), hi = Rooms.WorldMax;
            float k = RoomMapW / (hi.X - lo.X), mh = (hi.Y - lo.Y) * k;
            float ox = W - RoomMapRight - RoomMapW, oy = RoomMapTop;
            System.Func<V2, V2> M = p => new V2(ox + (p.X - lo.X) * k, oy + (p.Y - lo.Y) * k);
            if (!BeginUiGl())
                return;
            RoomMapRect(ox - 6f, oy - 6f, RoomMapW + 12f, mh + 12f, new Rgba(0.01f, 0.03f, 0.08f, 0.82f));
            for (int i = 0; i < Rooms.Areas.Length; i++)
            {
                var a = Rooms.Areas[i];
                if (a.Vault && !vault)
                    continue;
                var rs = RoomStateOf(i);
                Rgba fill, line;
                if (a.Hub) { fill = new Rgba(0.4f, 0.64f, 1f, 0.14f); line = new Rgba(0.4f, 0.64f, 1f, 0.75f); }
                else if (a.Hall) { fill = new Rgba(0.4f, 0.64f, 1f, 0.06f); line = new Rgba(0.4f, 0.64f, 1f, 0.35f); }
                else if (a.Vault) { fill = game.VaultCleared ? DoorTeal.WithAlpha(0.3f) : DoorGold.WithAlpha(0.22f); line = game.VaultCleared ? DoorTeal : DoorGold; }
                else if (!Rooms.RoomByRoom) { fill = new Rgba(0.4f, 0.64f, 1f, 0.09f); line = new Rgba(0.4f, 0.64f, 1f, 0.5f); }
                else if (rs != null && rs.Cleared) { fill = DoorTeal.WithAlpha(0.3f); line = DoorTeal; }
                else if (rs != null && rs.Seen) { fill = DoorRed.WithAlpha(0.2f); line = DoorRed; }
                else { fill = new Rgba(0.4f, 0.64f, 1f, 0.04f); line = new Rgba(0.4f, 0.64f, 1f, 0.3f); }
                V2 p0 = M(a.Min), p1 = M(a.Max);
                RoomMapRect(p0.X, p0.Y, p1.X - p0.X, p1.Y - p0.Y, fill);
                RoomMapFrame(p0.X, p0.Y, p1.X - p0.X, p1.Y - p0.Y, 1f, line);
            }
            for (int i = 0; i < Rooms.Doors.Length; i++)
            {
                if (!Rooms.DoorUp(i, vault))
                    continue;
                bool locked;
                var col = DoorColour(i, out locked);
                V2 c = M(Rooms.Doors[i].Pos);
                RoomMapRect(c.X - 2.5f, c.Y - 2.5f, 5f, 5f, col);
            }
            foreach (var rs in game.RoomStates)  // the next spill: a ring filling up round its door
                if (rs.NestAlive && !rs.Cleared && rs.NextDoor >= 0)
                {
                    float f = Mathf.Clamp01(1f - rs.SpillT / Rooms.SpillTime(game.Wave));
                    V2 c = M(Rooms.Doors[rs.NextDoor].Pos);
                    Arc(c, 7f, 1.5f, (rs.SpillT < Rooms.SpillWarn ? DoorAmber : DoorRed).WithAlpha(0.9f), 16, -Mathf.PI / 2f, MathX.Tau * f);
                }
            foreach (var e in game.Enemies.Values)
            {
                if (e.Hp <= 0f)
                    continue;
                V2 c = M(e.Disp);
                if (e.Type == EnemyType.Nest) { RoomMapRect(c.X - 2.5f, c.Y - 2.5f, 5f, 5f, new Rgba(0.72f, 0.9f, 0.36f)); continue; }
                if (e.Def.Mini || e.Def.IsBoss) { RoomMapRect(c.X - 3f, c.Y - 3f, 6f, 6f, new Rgba(1f, 0.62f, 0.3f)); continue; }
                RoomMapRect(c.X - 1f, c.Y - 1f, 2f, 2f, e.Type == EnemyType.Runner ? new Rgba(1f, 0.55f, 0.24f) : new Rgba(1f, 0.32f, 0.36f, 0.85f));
            }
            foreach (var ch in game.Props.Values)  // unopened treasure chests: a gold diamond-ish square
            {
                if (ch.Kind != PropKind.Chest || ch.Open || ch.Hp <= 0f)
                    continue;
                V2 c = M(ch.Pos);
                RoomMapRect(c.X - 4f, c.Y - 4f, 8f, 8f, new Rgba(0.02f, 0.03f, 0.06f, 0.9f));
                RoomMapRect(c.X - 3f, c.Y - 3f, 6f, 6f, new Rgba(1f, 0.82f, 0.3f));
            }
            foreach (var p in game.Players.Values)
            {
                V2 c = M(p.Disp);
                bool mine = p.Id == game.MyId;
                if (mine)
                    RoomMapRect(c.X - 3.5f, c.Y - 3.5f, 7f, 7f, new Rgba(1f, 1f, 1f, 0.9f));
                RoomMapRect(c.X - 2.5f, c.Y - 2.5f, 5f, 5f, p.Downed ? new Rgba(1f, 0.3f, 0.3f) : p.Def.Color);
            }
            // The box your screen shows.
            float hx = AW / (2f * camZoom), hy = AH / (2f * camZoom);
            V2 v0 = M(camFocus - new V2(hx, hy)), v1 = M(camFocus + new V2(hx, hy));
            RoomMapFrame(v0.X, v0.Y, v1.X - v0.X, v1.Y - v0.Y, 1f, new Rgba(1f, 1f, 1f, 0.5f));
            EndUiGl();
        }

        void RoomMapRect(float x, float y, float w, float h, Rgba c)
        {
            SetColor(c);
            Tri(new V2(x, y), new V2(x + w, y), new V2(x + w, y + h));
            Tri(new V2(x, y), new V2(x + w, y + h), new V2(x, y + h));
        }

        void RoomMapFrame(float x, float y, float w, float h, float t, Rgba c)
        {
            RoomMapRect(x, y, w, t, c);
            RoomMapRect(x, y + h - t, w, t, c);
            RoomMapRect(x, y, t, h, c);
            RoomMapRect(x + w - t, y, t, h, c);
        }
    }
}
