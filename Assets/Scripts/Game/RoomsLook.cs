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

        /// The mark in the middle of a floating room: a ring, and a coin ring (shop), a plus (vendor) or a diamond (Gatekeeper).
        void DrawFloatingMark(RoomArea a)
        {
            var ink = KindInk(a.Kind, a.Floor);
            float breathe = a.Sealed ? 0.5f + 0.25f * Mathf.Sin(game.AnimT * 2.5f) : 0.85f;
            var col = ink.WithAlpha(breathe);
            V2 c = a.Mid;
            Arc(c, 56f, 3f, col, 32);
            switch (a.Kind)
            {
                case RoomKind.Shop: Arc(c, 22f, 4f, col, 20); break;
                case RoomKind.Vendor:
                    Line(c - new V2(24f, 0f), c + new V2(24f, 0f), 5f, col);
                    Line(c - new V2(0f, 24f), c + new V2(0f, 24f), 5f, col);
                    break;
                default:  // the Gatekeeper: a diamond
                    Line(c + new V2(0f, -30f), c + new V2(30f, 0f), 4f, col);
                    Line(c + new V2(30f, 0f), c + new V2(0f, 30f), 4f, col);
                    Line(c + new V2(0f, 30f), c + new V2(-30f, 0f), 4f, col);
                    Line(c + new V2(-30f, 0f), c + new V2(0f, -30f), 4f, col);
                    break;
            }
        }

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
                var tint = a.Surprise > 0 ? DoorGold.WithAlpha(0.06f) : a.Floating ? KindInk(a.Kind, a.Floor).WithAlpha(a.Sealed ? 0.05f : 0.08f) : a.Owner != 0 && a.Kind == RoomKind.Combat ? OwnerInk(a.Owner).WithAlpha(0.06f) : a.Vault ? VaultTint : a.Kind == RoomKind.Shop ? ShopTint : a.Kind == RoomKind.Floor ? Paths.Get(a.Floor).Accent.WithAlpha(0.07f)
                    : a.Hall ? HallTint : !Rooms.RoomByRoom ? OpenRoomTint : rs != null && rs.Cleared ? ClearTint : RoomTint;
                FillRect(a.Min.X, a.Min.Y, a.Size.X, a.Size.Y, tint);
                if (a.Surprise > 0)  // a surprise room: gold build-in and rim (SurpriseLook.cs)
                    DrawSurpriseRoom(wd, i, a);
                if (a.Floating)  // a floating room: a ring and a mark of what it is, breathing while it is sealed
                    DrawFloatingMark(a);
                if (a.Owner != 0)  // a room somebody drew: a thin line of their colour just inside its walls
                {
                    var oi = OwnerInk(a.Owner).WithAlpha(0.35f);
                    foreach (var seg in wd.Rim(i, 18f))  // (a drawn outline's or bent hallway's boxes: round the whole room, not across its seams)
                        Line(seg[0], seg[1], 2f, oi);
                }
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
                if (!wd.DoorUp(i, vault) || wd.Seam(i))
                    continue;
                var d = wd.Doors[i];
                bool locked;
                var col = DoorColour(i, out locked);
                V2 j0 = d.Pos - d.Along * (d.W / 2f), j1 = d.Pos + d.Along * (d.W / 2f), t = d.Across * (Rooms.WallT * 0.9f);
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

        // A fixed-size window that follows you: RoomMapW x RoomMapH px showing RoomMapScale px of minimap per world px, centred on your
        // player (held inside the map's bounds, or centred on the map when it is smaller than the window). What lies beyond the window is
        // clipped; teammates beyond it show as dots on its edge.
        const float RoomMapW = 230f, RoomMapH = 170f, RoomMapScale = 0.07f, RoomMapTop = 40f, RoomMapRight = 16f;

        float mapOx, mapOy;

        /// A rectangle inside the minimap window, clipped to it.
        void MapRect(float x, float y, float w, float h, Rgba c)
        {
            float x0 = Mathf.Max(x, mapOx), y0 = Mathf.Max(y, mapOy), x1 = Mathf.Min(x + w, mapOx + RoomMapW), y1 = Mathf.Min(y + h, mapOy + RoomMapH);
            if (x1 > x0 && y1 > y0)
                RoomMapRect(x0, y0, x1 - x0, y1 - y0, c);
        }

        void MapFrame(float x, float y, float w, float h, float t, Rgba c)
        {
            MapRect(x, y, w, t, c);
            MapRect(x, y + h - t, w, t, c);
            MapRect(x, y, t, h, c);
            MapRect(x + w - t, y, t, h, c);
        }

        bool InMap(V2 c) => c.X >= mapOx && c.X <= mapOx + RoomMapW && c.Y >= mapOy && c.Y <= mapOy + RoomMapH;

        /// DrawHud: the Rooms and Expanding arenas' minimap (top right).
        void DrawRoomsMinimap()
        {
            if (!game.RoomsOn || !game.Running)
                return;
            bool vault = game.VaultOn;
            var wd = game.CurrentWorld;
            V2 lo = wd.MinFor(vault), hi = wd.Max;
            if (game.ExpandOpen)  // the open round's proposals count too
                foreach (var q in game.ExpandProposals)
                {
                    lo = new V2(Mathf.Min(lo.X, Mathf.Min(q.X, q.X + q.W)), Mathf.Min(lo.Y, Mathf.Min(q.Y, q.Y + q.H)));  // (a portal pair's W and H can be negative)
                    hi = new V2(Mathf.Max(hi.X, Mathf.Max(q.X, q.X + q.W)), Mathf.Max(hi.Y, Mathf.Max(q.Y, q.Y + q.H)));
                }
            float k = RoomMapScale, hwx = RoomMapW / k / 2f, hwy = RoomMapH / k / 2f;
            V2 at = game.Me != null ? game.Me.Disp : camFocus;
            V2 centre = new V2(hi.X - lo.X <= 2f * hwx ? (lo.X + hi.X) / 2f : Mathf.Clamp(at.X, lo.X + hwx, hi.X - hwx),
                               hi.Y - lo.Y <= 2f * hwy ? (lo.Y + hi.Y) / 2f : Mathf.Clamp(at.Y, lo.Y + hwy, hi.Y - hwy));
            float ox = W - RoomMapRight - RoomMapW, oy = RoomMapTop;
            mapOx = ox; mapOy = oy;
            System.Func<V2, V2> M = p => new V2(ox + RoomMapW / 2f + (p.X - centre.X) * k, oy + RoomMapH / 2f + (p.Y - centre.Y) * k);
            if (!BeginUiGl())
                return;
            RoomMapRect(ox - 6f, oy - 6f, RoomMapW + 12f, RoomMapH + 12f, new Rgba(0.01f, 0.03f, 0.08f, 0.82f));
            RoomMapFrame(ox - 1f, oy - 1f, RoomMapW + 2f, RoomMapH + 2f, 1f, new Rgba(0.4f, 0.64f, 1f, 0.35f));
            for (int i = 0; i < wd.Areas.Length; i++)
            {
                var a = wd.Areas[i];
                if (a.Vault && !vault)
                    continue;
                var rs = RoomStateOf(i);
                Rgba fill, line;
                if (a.Floating) { var fi = KindInk(a.Kind, a.Floor); float fb = a.Sealed ? 0.5f + 0.25f * Mathf.Sin(Time.unscaledTime * 2.5f) : 1f; fill = fi.WithAlpha(0.2f * fb); line = fi.WithAlpha(0.9f * fb); }
                else if (a.Surprise > 0 && game.SurpriseFresh) { fill = DoorGold.WithAlpha(0.22f); line = DoorGold; }
                else if (a.Owner != 0 && a.Kind == RoomKind.Combat) { fill = OwnerInk(a.Owner).WithAlpha(0.18f); line = OwnerInk(a.Owner).WithAlpha(0.85f); }
                else if (a.Kind == RoomKind.Shop) { fill = ShopTint.WithAlpha(0.22f); line = DoorGold; }
                else if (a.Kind == RoomKind.Floor) { fill = Paths.Get(a.Floor).Accent.WithAlpha(0.2f); line = Paths.Get(a.Floor).Accent; }
                else if (a.Hub) { fill = new Rgba(0.4f, 0.64f, 1f, 0.14f); line = new Rgba(0.4f, 0.64f, 1f, 0.75f); }
                else if (a.Hall) { fill = new Rgba(0.4f, 0.64f, 1f, 0.06f); line = new Rgba(0.4f, 0.64f, 1f, 0.35f); }
                else if (a.Vault) { fill = game.VaultCleared ? DoorTeal.WithAlpha(0.3f) : DoorGold.WithAlpha(0.22f); line = game.VaultCleared ? DoorTeal : DoorGold; }
                else if (!Rooms.RoomByRoom) { fill = new Rgba(0.4f, 0.64f, 1f, 0.09f); line = new Rgba(0.4f, 0.64f, 1f, 0.5f); }
                else if (rs != null && rs.Cleared) { fill = DoorTeal.WithAlpha(0.3f); line = DoorTeal; }
                else if (rs != null && rs.Seen) { fill = DoorRed.WithAlpha(0.2f); line = DoorRed; }
                else { fill = new Rgba(0.4f, 0.64f, 1f, 0.04f); line = new Rgba(0.4f, 0.64f, 1f, 0.3f); }
                V2 p0 = M(a.Min), p1 = M(a.Max);
                MapRect(p0.X, p0.Y, p1.X - p0.X, p1.Y - p0.Y, fill);
                RimFrame(wd, i, M, 1f, line, true);
            }
            if (game.ExpandOpen)  // proposals as ghosts in their drawer's colour, pulsing
                foreach (var q in game.ExpandProposals)
                {
                    V2 q0 = M(new V2(q.X, q.Y)), q1 = M(new V2(q.X + q.W, q.Y + q.H));
                    var qi = OwnerInk(q.Owner);
                    float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 3f);
                    if (q.Kind == RoomKind.Gate)
                    {
                        MapRect(q0.X - 3f, q0.Y - 3f, 6f, 6f, qi.WithAlpha(0.95f * pulse));
                        continue;
                    }
                    if (q.Kind == RoomKind.Link)  // a portal pair: two dots
                    {
                        MapRect(q0.X - 3f, q0.Y - 3f, 6f, 6f, qi.WithAlpha(0.95f * pulse));
                        MapRect(q1.X - 3f, q1.Y - 3f, 6f, 6f, qi.WithAlpha(0.95f * pulse));
                        continue;
                    }
                    MapRect(q0.X, q0.Y, q1.X - q0.X, q1.Y - q0.Y, qi.WithAlpha(0.16f * pulse));
                    MapFrame(q0.X, q0.Y, q1.X - q0.X, q1.Y - q0.Y, 1.5f, qi.WithAlpha(0.95f));
                }
            for (int i = 0; i < wd.Doors.Length; i++)
            {
                if (!wd.DoorUp(i, vault) || wd.Seam(i))
                    continue;
                bool locked;
                var col = DoorColour(i, out locked);
                V2 c = M(wd.Doors[i].Pos);
                MapRect(c.X - 2.5f, c.Y - 2.5f, 5f, 5f, col);
            }
            foreach (var rs in game.RoomStates)  // the next spill: a ring filling up round its door
                if (rs.NestAlive && !rs.Cleared && rs.NextDoor >= 0)
                {
                    float f = Mathf.Clamp01(1f - rs.SpillT / Rooms.SpillTime(game.Wave));
                    V2 c = M(wd.Doors[rs.NextDoor].Pos);
                    if (InMap(c)) Arc(c, 7f, 1.5f, (rs.SpillT < Rooms.SpillWarn ? DoorAmber : DoorRed).WithAlpha(0.9f), 16, -Mathf.PI / 2f, MathX.Tau * f);
                }
            for (int i = 0; i < wd.Portals.Length; i++)  // the portals, in their pair's colour
                foreach (var pp in new[] { wd.Portals[i].A, wd.Portals[i].B })
                {
                    V2 c = M(pp);
                    if (InMap(c)) Arc(c, 4f, 2f, PortalColours[i % PortalColours.Length], 12);
                }
            for (int gi = 0; gi < game.GateFits.Count; gi++)  // shutter gates: a red bar, bright while shut (GateLook.cs)
            {
                if (!game.GateLive(gi))
                    continue;
                var gf = game.GateFits[gi];
                V2 c = M(gf.Pos);
                if (!InMap(c))
                    continue;
                var gcol = new Rgba(1f, 0.3f, 0.32f, game.GateShut(gi) > 0f ? 1f : 0.45f);
                if (gf.Vertical) MapRect(c.X - 2f, c.Y - 6f, 4f, 12f, gcol);
                else MapRect(c.X - 6f, c.Y - 2f, 12f, 4f, gcol);
            }
            foreach (var b in game.ActiveBreaches)  // the wave's breaches: a red dash on the wall (BreachLook.cs)
            {
                V2 c = M(b.Pos);
                if (!InMap(c))
                    continue;
                float lh = Breaches.Len * k / 2f + 1f;
                var bc = new Rgba(1f, 0.22f, 0.26f, 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 6f));
                if (b.Vertical) MapRect(c.X - 2f, c.Y - lh, 4f, 2f * lh, bc);
                else MapRect(c.X - lh, c.Y - 2f, 2f * lh, 4f, bc);
            }
            foreach (var e in game.Enemies.Values)
            {
                if (e.Hp <= 0f)
                    continue;
                V2 c = M(e.Disp);
                if (e.Type == EnemyType.Nest) { MapRect(c.X - 2.5f, c.Y - 2.5f, 5f, 5f, new Rgba(0.72f, 0.9f, 0.36f)); continue; }
                if (e.Def.Mini || e.Def.IsBoss) { MapRect(c.X - 3f, c.Y - 3f, 6f, 6f, new Rgba(1f, 0.62f, 0.3f)); continue; }
                MapRect(c.X - 1f, c.Y - 1f, 2f, 2f, e.Type == EnemyType.Runner ? new Rgba(1f, 0.55f, 0.24f) : new Rgba(1f, 0.32f, 0.36f, 0.85f));
            }
            foreach (var ch in game.Props.Values)  // unopened treasure chests: a gold diamond-ish square
            {
                if (ch.Kind != PropKind.Chest || ch.Open || ch.Hp <= 0f)
                    continue;
                V2 c = M(ch.Pos);
                MapRect(c.X - 4f, c.Y - 4f, 8f, 8f, new Rgba(0.02f, 0.03f, 0.06f, 0.9f));
                MapRect(c.X - 3f, c.Y - 3f, 6f, 6f, new Rgba(1f, 0.82f, 0.3f));
            }
            foreach (var p in game.Players.Values)
            {
                V2 c = M(p.Disp);
                bool mine = p.Id == game.MyId;
                if (!InMap(c))  // beyond the window: a dot on its edge
                {
                    c = new V2(Mathf.Clamp(c.X, ox + 3f, ox + RoomMapW - 3f), Mathf.Clamp(c.Y, oy + 3f, oy + RoomMapH - 3f));
                    RoomMapRect(c.X - 2.5f, c.Y - 2.5f, 5f, 5f, (p.Downed ? new Rgba(1f, 0.3f, 0.3f) : p.Def.Color).WithAlpha(0.75f));
                    continue;
                }
                if (mine)
                    MapRect(c.X - 3.5f, c.Y - 3.5f, 7f, 7f, new Rgba(1f, 1f, 1f, 0.9f));
                MapRect(c.X - 2.5f, c.Y - 2.5f, 5f, 5f, p.Downed ? new Rgba(1f, 0.3f, 0.3f) : p.Def.Color);
            }
            // The box your screen shows.
            float hx = AW / (2f * camZoom), hy = AH / (2f * camZoom);
            V2 v0 = M(camFocus - new V2(hx, hy)), v1 = M(camFocus + new V2(hx, hy));
            MapFrame(v0.X, v0.Y, v1.X - v0.X, v1.Y - v0.Y, 1f, new Rgba(1f, 1f, 1f, 0.5f));
            EndUiGl();
        }

        /// When set, RoomMapRect clips to it (the zoomed drawing map).
        Rect? roomClip;

        void RoomMapRect(float x, float y, float w, float h, Rgba c)
        {
            if (roomClip.HasValue)
            {
                var cl = roomClip.Value;
                float x1 = Mathf.Min(x + w, cl.xMax), y1 = Mathf.Min(y + h, cl.yMax);
                x = Mathf.Max(x, cl.x); y = Mathf.Max(y, cl.y);
                w = x1 - x; h = y1 - y;
                if (w <= 0f || h <= 0f) return;
            }
            SetColor(c);
            Tri(new V2(x, y), new V2(x + w, y), new V2(x + w, y + h));
            Tri(new V2(x, y), new V2(x + w, y + h), new V2(x, y + h));
        }

        /// An area's frame on a map: round its whole room (a drawn outline's or bent hallway's boxes leave their seams open), `t` px
        /// thick. clip: inside the minimap window.
        void RimFrame(World wd, int i, System.Func<V2, V2> M, float t, Rgba c, bool clip = false)
        {
            var a = wd.Areas[i];
            if (a.Group == 0)
            {
                V2 p0 = M(a.Min), p1 = M(a.Max);
                if (clip) MapFrame(p0.X, p0.Y, p1.X - p0.X, p1.Y - p0.Y, t, c);
                else RoomMapFrame(p0.X, p0.Y, p1.X - p0.X, p1.Y - p0.Y, t, c);
                return;
            }
            foreach (var seg in wd.Rim(i, 0f))
            {
                V2 e0 = M(seg[0]), e1 = M(seg[1]);
                float x = Mathf.Min(e0.X, e1.X) - t / 2f, y = Mathf.Min(e0.Y, e1.Y) - t / 2f, w = Mathf.Abs(e1.X - e0.X) + t, h = Mathf.Abs(e1.Y - e0.Y) + t;
                if (clip) MapRect(x, y, w, h, c);
                else RoomMapRect(x, y, w, h, c);
            }
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
