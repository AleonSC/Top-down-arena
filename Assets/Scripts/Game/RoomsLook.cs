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
            VaultTint = new Rgba(1f, 0.62f, 0.3f, 0.06f), ShopTint = new Rgba(1f, 0.82f, 0.3f, 0.07f), DoorInk = new Rgba(0.373f, 0.941f, 1f), DoorRed = new Rgba(1f, 0.3f, 0.36f),
            DoorAmber = new Rgba(1f, 0.7f, 0.28f), DoorTeal = new Rgba(0.25f, 0.9f, 0.7f), DoorGold = new Rgba(1f, 0.82f, 0.4f);

        void DrawRoomsFloor(Rgba grid)
        {
            bool vault = game.VaultOn;
            var wd = game.CurrentWorld;
            V2 lo = wd.MinFor(vault), hi = wd.Max;
            for (int i = 0; i < wd.Areas.Length; i++)
            {
                var a = wd.Areas[i];
                if (a.Hub || a.Vault && !vault)
                    continue;
                var rs = RoomStateOf(i);
                var tint = a.Vault ? VaultTint : a.Kind == RoomKind.Shop ? ShopTint : a.Kind == RoomKind.Floor ? Paths.Get(a.Floor).Accent.WithAlpha(0.07f)
                    : a.Hall ? HallTint : !Rooms.RoomByRoom ? OpenRoomTint : rs != null && rs.Cleared ? ClearTint : RoomTint;
                FillRect(a.Min.X, a.Min.Y, a.Size.X, a.Size.Y, tint);
            }
            if (game.ArenaKind == ArenaLayout.Expanding)
            {
                // The Expanding map isn't a full box: the grid runs inside each room only.
                foreach (var a in wd.Areas)
                {
                    for (float x = Mathf.Ceil(a.Min.X / 64f) * 64f; x <= a.Max.X + 0.5f; x += 64f)
                        Line(new V2(x, a.Min.Y), new V2(x, a.Max.Y), 1f, grid);
                    for (float y = Mathf.Ceil(a.Min.Y / 64f) * 64f; y <= a.Max.Y + 0.5f; y += 64f)
                        Line(new V2(a.Min.X, y), new V2(a.Max.X, y), 1f, grid);
                }
            }
            else
            {
                for (float x = lo.X; x <= hi.X + 0.5f; x += 64f)
                    Line(new V2(x, lo.Y), new V2(x, hi.Y), 1f, grid);
                for (float y = Mathf.Ceil(lo.Y / 64f) * 64f; y <= hi.Y + 0.5f; y += 64f)
                    Line(new V2(lo.X, y), new V2(hi.X, y), 1f, grid);
            }
            float a2 = Mathf.Clamp01(game.LayoutAge / 0.35f);
            for (int i = 0; i < wd.Portals.Length; i++)  // the wraparound portals, each pair in its own colour
            {
                var pt = wd.Portals[i];
                DrawPortal(pt.A, pt.InA, PortalColours[i % PortalColours.Length], a2, i);
                DrawPortal(pt.B, pt.InB, PortalColours[i % PortalColours.Length], a2, i + 0.5f);
            }
            for (int i = 0; i < wd.Doors.Length; i++)
            {
                if (!wd.DoorUp(i, vault))
                    continue;
                var d = wd.Doors[i];
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

        static readonly Rgba[] PortalColours = { new Rgba(0.75f, 0.45f, 1f), new Rgba(0.35f, 0.95f, 0.85f), new Rgba(1f, 0.6f, 0.85f) };

        /// A portal on the floor: a dark core, a turning dashed ring, a slow pulse, and chevrons pointing into the map
        /// (the way you come out).
        void DrawPortal(V2 at, V2 inward, Rgba col, float a, float seed)
        {
            float t = game.AnimT, r = Rooms.PortalR;
            Circle(at, r, new Rgba(0.02f, 0.01f, 0.05f, 0.8f * a), 28);
            Circle(at, r * 0.75f, col.WithAlpha(0.12f * a), 24);
            float pulse = Mathf.Repeat(t * 0.8f + seed, 1f);
            Arc(at, r * (0.35f + 0.65f * pulse), 2f, col.WithAlpha((1f - pulse) * 0.6f * a), 28);
            int n = 12;
            for (int k = 0; k < n; k++)
            {
                float st = MathX.Tau * k / n + t * 1.6f;
                Arc(at, r, 6f, col.WithAlpha(0.18f * a), 3, st, MathX.Tau / n * 0.55f);
                Arc(at, r, 2.5f, col.WithAlpha(a), 3, st, MathX.Tau / n * 0.55f);
            }
            Arc(at, r * 0.55f, 2f, col.WithAlpha(0.7f * a), 20, -t * 2.2f, MathX.Tau * 0.6f);
            V2 side = inward.Orthogonal();
            for (int k = 0; k < 2; k++)
            {
                V2 c = at + inward * (r + 14f + k * 12f);
                Line(c - inward * 6f + side * 8f, c, 2.5f, col.WithAlpha((0.8f - 0.3f * k) * a));
                Line(c - inward * 6f - side * 8f, c, 2.5f, col.WithAlpha((0.8f - 0.3f * k) * a));
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
            var d = game.CurrentWorld.Doors[i];
            locked = false;
            foreach (int area in new[] { d.A, d.B })
            {
                var a = game.CurrentWorld.Areas[area];
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

        const float RoomMapW = 230f, RoomMapMaxH = 300f, RoomMapTop = 40f, RoomMapRight = 16f;

        /// DrawHud: the Rooms arena's minimap (top right).
        void DrawRoomsMinimap()
        {
            if (!game.RoomsOn || !game.Running)
                return;
            bool vault = game.VaultOn;
            var wd = game.CurrentWorld;
            V2 lo = wd.MinFor(vault), hi = wd.Max;
            float k = Mathf.Min(RoomMapW / (hi.X - lo.X), RoomMapMaxH / (hi.Y - lo.Y)), mh = (hi.Y - lo.Y) * k, mw = (hi.X - lo.X) * k;
            float ox = W - RoomMapRight - mw, oy = RoomMapTop;
            System.Func<V2, V2> M = p => new V2(ox + (p.X - lo.X) * k, oy + (p.Y - lo.Y) * k);
            if (!BeginUiGl())
                return;
            RoomMapRect(ox - 6f, oy - 6f, mw + 12f, mh + 12f, new Rgba(0.01f, 0.03f, 0.08f, 0.82f));
            for (int i = 0; i < wd.Areas.Length; i++)
            {
                var a = wd.Areas[i];
                if (a.Vault && !vault)
                    continue;
                var rs = RoomStateOf(i);
                Rgba fill, line;
                if (a.Kind == RoomKind.Shop) { fill = ShopTint.WithAlpha(0.22f); line = DoorGold; }
                else if (a.Kind == RoomKind.Floor) { fill = Paths.Get(a.Floor).Accent.WithAlpha(0.2f); line = Paths.Get(a.Floor).Accent; }
                else if (a.Hub) { fill = new Rgba(0.4f, 0.64f, 1f, 0.14f); line = new Rgba(0.4f, 0.64f, 1f, 0.75f); }
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
            for (int i = 0; i < wd.Doors.Length; i++)
            {
                if (!wd.DoorUp(i, vault))
                    continue;
                bool locked;
                var col = DoorColour(i, out locked);
                V2 c = M(wd.Doors[i].Pos);
                RoomMapRect(c.X - 2.5f, c.Y - 2.5f, 5f, 5f, col);
            }
            foreach (var rs in game.RoomStates)  // the next spill: a ring filling up round its door
                if (rs.NestAlive && !rs.Cleared && rs.NextDoor >= 0)
                {
                    float f = Mathf.Clamp01(1f - rs.SpillT / Rooms.SpillTime(game.Wave));
                    V2 c = M(wd.Doors[rs.NextDoor].Pos);
                    Arc(c, 7f, 1.5f, (rs.SpillT < Rooms.SpillWarn ? DoorAmber : DoorRed).WithAlpha(0.9f), 16, -Mathf.PI / 2f, MathX.Tau * f);
                }
            for (int i = 0; i < wd.Portals.Length; i++)  // the portals, in their pair's colour
                foreach (var pp in new[] { wd.Portals[i].A, wd.Portals[i].B })
                {
                    V2 c = M(pp);
                    Arc(c, 4f, 2f, PortalColours[i % PortalColours.Length], 12);
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
