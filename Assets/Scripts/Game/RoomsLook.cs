using UnityEngine;

namespace Arena
{
    /// The look of the walled worlds (the Expanding arena and Survival's big room; Rooms.cs, World.cs): the floor grid,
    /// a faint tint on the rooms so the hub stands out, the doors, and the minimap. The walls themselves are layout solids,
    /// drawn with the floor plan's (LayoutLook.cs).
    ///   doors    cyan brackets.
    ///   minimap  top right, under the hints: every area, the doors, the nodes, enemies (mini-bosses and bosses big and
    ///            orange), the players, and the box your screen shows.
    public partial class ArenaApp
    {
        static readonly Rgba OpenRoomTint = new Rgba(0.45f, 0.6f, 1f, 0.03f);
        static readonly Rgba HallTint = new Rgba(0.2f, 0.3f, 0.6f, 0.05f),
            ShopTint = Rgba.Gold.WithAlpha(0.07f), DoorInk = new Rgba(0.373f, 0.941f, 1f), DoorGold = Rgba.Gold,
            DoorTeal = new Rgba(0.25f, 0.9f, 0.7f);

        /// A tiny service room built from a card or a mystery door (not floating): it gets the same mark as a floating one.
        static bool TinyService(RoomArea a)
            => (a.Kind == RoomKind.Shop || a.Kind == RoomKind.BlackMarket || a.Kind == RoomKind.Arms || a.Kind == RoomKind.Medic) && a.Size.X <= TinySide && a.Size.Y <= TinySide;  // (4 x 4, a mystery door's 5 x 4, or a Grid run's one cell)

        const int TinySide = Expansion.GridCell > Expansion.ShopRoomSide + Expansion.Step ? Expansion.GridCell : Expansion.ShopRoomSide + Expansion.Step;

        /// The mark in the middle of a floating room: a ring, and a coin ring (shop), a plus (Black Market), a crosshair (Arms Dealer), a pill (Medic) or a diamond (Gatekeeper).
        void DrawFloatingMark(RoomArea a)
        {
            if (!a.Sealed && a.Kind == RoomKind.Arms)  // an Arms Dealer: its stall (ArmsDealerLook.cs)
            {
                DrawArmsStall(a.Mid);
                return;
            }
            if (!a.Sealed && a.Kind == RoomKind.Medic)  // a Medic: its bay (MedicLook.cs)
            {
                DrawMedicBay(a.Mid);
                return;
            }
            if (!a.Sealed && (a.Kind == RoomKind.Shop || a.Kind == RoomKind.BlackMarket))  // an opened shop or Black Market room: its pedestals or pedestal
            {
                if (a.Kind == RoomKind.Shop) DrawShopPedestals(a); else DrawMarketPedestal(a.Mid);  // (the shop's wares on pedestals: ShopPedestalLook.cs)
                return;
            }
            var ink = KindInk(a.Kind, a.Floor);
            float breathe = a.Sealed ? 0.5f + 0.25f * Mathf.Sin(game.AnimT * 2.5f) : 0.85f;
            var col = ink.WithAlpha(breathe);
            V2 c = a.Mid;
            Arc(c, 56f, 3f, col, 32);
            switch (a.Kind)
            {
                case RoomKind.Shop: Arc(c, 22f, 4f, col, 20); break;
                case RoomKind.BlackMarket:
                    Line(c - new V2(24f, 0f), c + new V2(24f, 0f), 5f, col);
                    Line(c - new V2(0f, 24f), c + new V2(0f, 24f), 5f, col);
                    break;
                case RoomKind.Arms:  // a crosshair: a ring with four ticks
                    Arc(c, 20f, 3f, col, 20);
                    Line(c + new V2(-34f, 0f), c + new V2(-12f, 0f), 4f, col);
                    Line(c + new V2(12f, 0f), c + new V2(34f, 0f), 4f, col);
                    Line(c + new V2(0f, -34f), c + new V2(0f, -12f), 4f, col);
                    Line(c + new V2(0f, 12f), c + new V2(0f, 34f), 4f, col);
                    break;
                case RoomKind.Medic:  // a capsule pill on its side
                    Arc(c - new V2(14f, 0f), 12f, 4f, col, 10, MathX.Tau / 4f, MathX.Tau / 2f);
                    Arc(c + new V2(14f, 0f), 12f, 4f, col, 10, -MathX.Tau / 4f, MathX.Tau / 2f);
                    Line(c + new V2(-14f, -12f), c + new V2(14f, -12f), 4f, col);
                    Line(c + new V2(-14f, 12f), c + new V2(14f, 12f), 4f, col);
                    Line(c + new V2(0f, -12f), c + new V2(0f, 12f), 4f, col);
                    break;
                default:  // the Gatekeeper: a diamond
                    Line(c + new V2(0f, -30f), c + new V2(30f, 0f), 4f, col);
                    Line(c + new V2(30f, 0f), c + new V2(0f, 30f), 4f, col);
                    Line(c + new V2(0f, 30f), c + new V2(-30f, 0f), 4f, col);
                    Line(c + new V2(-30f, 0f), c + new V2(0f, -30f), 4f, col);
                    break;
            }
        }

        /// Widens a map view's bounds to hold the nodes still waiting for a room.
        void IncludeNodes(ref V2 lo, ref V2 hi)
        {
            foreach (var n in game.Nodes)
                if (!Expansion.NodeClaimed(game.Expanded, n))
                {
                    lo = new V2(Mathf.Min(lo.X, n.X - 150f), Mathf.Min(lo.Y, n.Y - 150f));
                    hi = new V2(Mathf.Max(hi.X, n.X + 150f), Mathf.Max(hi.Y, n.Y + 150f));
                }
        }

        /// Is this area one of a Grid run's core rooms (GridRooms.cs)? They look like the start room: no tint of their own, and the openings between them draw no door.
        bool CoreArea(int area) => game.ExpandingOn && area >= 1 && area <= game.Expanded.Count && Expansion.IsGridCore(game.Expanded[area - 1]);

        bool CoreSeam(World wd, int door)
        {
            var d = wd.Doors[door];
            return game.ExpandingOn && d.B >= 1 && (d.A == 0 || CoreArea(d.A)) && CoreArea(d.B);
        }

        void DrawRoomsFloor(Rgba grid)
        {
            var wd = game.CurrentWorld;
            V2 lo = wd.Min, hi = wd.Max;
            for (int i = 0; i < wd.Areas.Length; i++)
            {
                var a = wd.Areas[i];
                if (a.Hub || CoreArea(i))
                    continue;
                var tint = a.Surprise > 0 ? SurpriseInk(a.Surprise).WithAlpha(0.06f) : a.Floating || TinyService(a) ? KindInk(a.Kind, a.Floor).WithAlpha(a.Sealed ? 0.05f : 0.08f) : a.Owner != 0 && a.Kind == RoomKind.Combat ? OwnerInk(a.Owner).WithAlpha(0.06f) : a.Kind == RoomKind.Shop ? ShopTint : a.Kind == RoomKind.Builder ? BuilderInk.WithAlpha(0.07f) : a.Kind == RoomKind.Floor ? Paths.Get(a.Floor).Accent.WithAlpha(0.07f)
                    : a.Hall ? HallTint : OpenRoomTint;
                FillRect(a.Min.X, a.Min.Y, a.Size.X, a.Size.Y, tint);
                if (a.Surprise > 0)  // a surprise room: gold build-in and rim (SurpriseLook.cs)
                    DrawSurpriseRoom(wd, i, a);
                if (IsKept(i))  // the room kept through the map reset: gold build-in and rim (MapResetLook.cs)
                    DrawKeptRoom(wd, i, a);
                if (a.Kind == RoomKind.Builder && !a.Floating)  // a Builder Shop card's room: its counter (BuilderShopUi.cs)
                    DrawBuilderCounter(a.Mid);
                if (a.Floating || TinyService(a))  // a floating room (or a tiny service room): a ring and a mark of what it is, breathing while it is sealed
                    DrawFloatingMark(a);
                if (a.Owner != 0)  // a room somebody drew: a thin line of their colour just inside its walls
                {
                    var oi = OwnerInk(a.Owner).WithAlpha(0.35f);
                    foreach (var seg in wd.Rim(i, 18f))  // (a drawn outline's or bent hallway's boxes: round the whole room, not across its seams)
                        Line(seg[0], seg[1], 2f, oi);
                }
            }
            if (PreviewRelicSheet)
                DrawRelicSheet();
            if (game.ArenaKind == ArenaLayout.Expanding)
                for (int i = 0; i < game.Nodes.Count; i++)
                    DrawNodeMark(game.Nodes[i], Expansion.NodeClaimed(game.Expanded, game.Nodes[i]), game.NodeSpot(i), i);
            if (game.ArenaKind == ArenaLayout.Expanding)
            {
                // The Expanding map isn't a full box: the grid runs inside each room only.
                foreach (var a in wd.Areas)  // (the Pressure grid, FloorGridLook.cs)
                    DrawFloorGrid(a.Min, a.Max, new V2(Mathf.Ceil(a.Min.X / 64f) * 64f, Mathf.Ceil(a.Min.Y / 64f) * 64f), grid);
            }
            else
                DrawFloorGrid(lo, hi, new V2(lo.X, Mathf.Ceil(lo.Y / 64f) * 64f), grid);
            float a2 = Mathf.Clamp01(game.LayoutAge / 0.35f);
            for (int i = 0; i < wd.Portals.Length; i++)  // the wraparound portals, each pair in its own colour
            {
                var pt = wd.Portals[i];
                if (pt.Wall)
                    continue;  // (a wall portal: drawn over its wall, DrawWallPortals)
                DrawPortal(pt.A, pt.InA, PortalColours[i % PortalColours.Length], a2, i);
                DrawPortal(pt.B, pt.InB, PortalColours[i % PortalColours.Length], a2, i + 0.5f);
            }
            for (int i = 0; i < wd.Doors.Length; i++)
            {
                if (wd.Seam(i) || CoreSeam(wd, i))
                    continue;
                var d = wd.Doors[i];
                if (d.Pass != 0)
                {
                    DrawOneWayDoor(wd, d, a2);  // a One-way piece's door: chevrons the way it opens (OneWayDoorLook.cs)
                    continue;
                }
                var col = DoorInk;
                V2 j0 = d.Pos - d.Along * (d.W / 2f), j1 = d.Pos + d.Along * (d.W / 2f), t = d.Across * (Rooms.WallT * 0.9f);
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

        /// The wall portals (the Expanding arena's bought pairs), over their walls, in their pair's colour: the stretch of wall opens
        /// into a dark gap framed by corner brackets, scanlines roll out of it across the floor as a widening, fading beam (the way
        /// you come out), and pips on the frame blink in turn.
        void DrawWallPortals(float t)
        {
            if (!game.RoomsOn)
                return;
            var wd = game.CurrentWorld;
            float a = Mathf.Clamp01(game.LayoutAge / 0.35f);
            for (int i = 0; i < wd.Portals.Length; i++)
            {
                var pt = wd.Portals[i];
                if (!pt.Wall)
                    continue;
                var col = PortalColours[i % PortalColours.Length];
                DrawWallPortal(pt.A, pt.InA, pt.Half, col, a, t, i);
                DrawWallPortal(pt.B, pt.InB, pt.Half, col, a, t, i + 0.5f);
            }
        }

        void DrawWallPortal(V2 at, V2 inward, float half, Rgba col, float a, float t, float seed)
        {
            V2 along = inward.Orthogonal();
            float wt = Rooms.WallT / 2f + 2f;
            V2 e0 = at - along * half, e1 = at + along * half;
            Quad2(e0 - inward * wt, e1 - inward * wt, e1 + inward * wt, e0 + inward * wt, new Rgba(0.02f, 0.01f, 0.05f, 0.96f * a));
            Quad2(e0 - inward * wt, e1 - inward * wt, e1 + inward * wt, e0 + inward * wt, col.WithAlpha(0.12f * a));
            // The beam on the floor: a trapezoid widening into the room, in fading slices.
            const float reach = 90f;
            for (int k = 0; k < 6; k++)
            {
                float d0 = k * reach / 6f, d1 = (k + 1) * reach / 6f;
                float h0 = half * (1f + 0.25f * d0 / reach), h1 = half * (1f + 0.25f * d1 / reach);
                V2 c0 = at + inward * (wt + d0), c1 = at + inward * (wt + d1);
                Quad2(c0 - along * h0, c0 + along * h0, c1 + along * h1, c1 - along * h1, col.WithAlpha(0.09f * (1f - k / 6f) * a));
            }
            // Scanlines rolling from the back of the wall out across the floor.
            for (int k = 0; k < 9; k++)
            {
                float u = Mathf.Repeat(k / 9f + t * 0.45f + seed * 0.13f, 1f);
                float d = -wt + u * (2f * wt + reach), f = d < wt ? 1f : 1f - (d - wt) / reach;
                float h = half * (d < wt ? 1f : 1f + 0.25f * (d - wt) / reach) - 6f;
                V2 c = at + inward * d;
                Line(c - along * h, c + along * h, d < wt ? 2f : 1.5f, col.WithAlpha(0.75f * f * a));
            }
            GlowLine(e0 + inward * wt, e1 + inward * wt, 2f, col.WithAlpha(0.9f * a));
            // The frame: corner brackets standing just off the wall's faces.
            float fw = wt + 5f;
            foreach (var sa in new[] { -1f, 1f })
                foreach (var si in new[] { -1f, 1f })
                {
                    V2 corner = at + along * (sa * (half + 4f)) + inward * (si * fw);
                    Line(corner, corner - along * (sa * 22f), 6f, col.WithAlpha(0.2f * a));
                    Line(corner, corner - inward * (si * 12f), 6f, col.WithAlpha(0.2f * a));
                    Line(corner, corner - along * (sa * 22f), 2.5f, col.WithAlpha(a));
                    Line(corner, corner - inward * (si * 12f), 2.5f, col.WithAlpha(a));
                }
            // Pair pips on the jambs, blinking in turn.
            foreach (var sa in new[] { -1f, 1f })
                for (int k = 0; k < 3; k++)
                {
                    float on = Mathf.Repeat(t * 1.5f + seed - k / 3f, 1f) < 0.34f ? 1f : 0.3f;
                    V2 p = at + along * (sa * (half + 4f)) + inward * (-wt * 0.6f + k * wt * 0.6f);
                    Quad2(p - along * 2.5f - inward * 2.5f, p + along * 2.5f - inward * 2.5f, p + along * 2.5f + inward * 2.5f, p - along * 2.5f + inward * 2.5f, col.WithAlpha(on * a));
                }
        }


        void Quad2(V2 p, V2 q, V2 r, V2 s, Rgba c)
        {
            SetColor(c);
            Quad(p, q, r, s);
        }

        // --- The minimap -------------------------------------------------------------------------------------------

        // A fixed-size window that follows you: RoomMapW x RoomMapH px showing RoomMapScale px of minimap per world px, centred on your
        // player (held inside the map's bounds, or centred on the map when it is smaller than the window). What lies beyond the window is
        // clipped; teammates beyond it show as dots on its edge.
        const float RoomMapW = 230f, RoomMapH = 170f, RoomMapScale = 0.07f, RoomMapTop = 40f, RoomMapRight = 16f;

        float mapOx, mapOy, mapW = RoomMapW, mapH = RoomMapH;  // the window being drawn (the minimap's, the full map's or one between: MapView.cs)

        /// A rectangle inside the minimap window, clipped to it.
        void MapRect(float x, float y, float w, float h, Rgba c)
        {
            float x0 = Mathf.Max(x, mapOx), y0 = Mathf.Max(y, mapOy), x1 = Mathf.Min(x + w, mapOx + mapW), y1 = Mathf.Min(y + h, mapOy + mapH);
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

        bool InMap(V2 c) => mapH > 12f && c.X >= mapOx && c.X <= mapOx + mapW && c.Y >= mapOy && c.Y <= mapOy + mapH;

        /// DrawHud: the walled worlds' map: the minimap (top right), the full map or neither, as the Map key
        /// last set it (MapView.cs).
        void DrawRoomsMinimap()
        {
            if (!game.RoomsOn || !game.Running)
                return;
            var wd = game.CurrentWorld;
            V2 lo, hi;
            MapBounds(out lo, out hi);
            var geoN = MapGeoNow(lo, hi);
            if (!geoN.HasValue || geoN.Value.R.width < 1f)
            {
                DrawMapLabels(null);  // (the "Map off" note)
                return;
            }
            var geo = geoN.Value;
            mapLastGeo = geo; mapHasLast = true;
            float k = geo.K, ox = geo.R.x, oy = geo.R.y, mw = geo.R.width, mh = geo.R.height;  // (the window's size)
            V2 centre = geo.C;
            mapOx = ox; mapOy = oy; mapW = mw; mapH = mh;
            System.Func<V2, V2> M = p => new V2(ox + mw / 2f + (p.X - centre.X) * k, oy + mh / 2f + (p.Y - centre.Y) * k);
            if (!BeginUiGl())
                return;
            if (geo.Dim > 0f)  // the full map: the game behind it dims
                RoomMapRect(0f, 0f, W, H, new Rgba(0f, 0.01f, 0.03f, 0.45f * geo.Dim));
            float pad = 6f * Mathf.Clamp01(mh / 40f);  // (folding shut: the frame hugs the line)
            RoomMapRect(ox - pad, oy - pad, mw + 2f * pad, mh + 2f * pad, new Rgba(0.01f, 0.03f, 0.08f, 0.82f + 0.1f * geo.Dim));
            RoomMapFrame(ox - 1f, oy - 1f, mw + 2f, mh + 2f, 1f, new Rgba(0.4f, 0.64f, 1f, 0.35f + 0.25f * geo.Dim));
            DrawMapChrome(geo);  // the full map's corner brackets and legend swatches (MapView.cs)
            for (int i = 0; i < wd.Areas.Length; i++)
            {
                var a = wd.Areas[i];
                Rgba fill, line;
                if (a.Floating) { var fi = KindInk(a.Kind, a.Floor); float fb = a.Sealed ? 0.5f + 0.25f * Mathf.Sin(Time.unscaledTime * 2.5f) : 1f; fill = fi.WithAlpha(0.2f * fb); line = fi.WithAlpha(0.9f * fb); }
                else if (a.Surprise > 0 && game.SurpriseFresh) { fill = SurpriseInk(a.Surprise).WithAlpha(0.22f); line = SurpriseInk(a.Surprise); }
                else if (a.Owner != 0 && a.Kind == RoomKind.Combat) { fill = OwnerInk(a.Owner).WithAlpha(0.18f); line = OwnerInk(a.Owner).WithAlpha(0.85f); }
                else if (a.Kind == RoomKind.Shop) { fill = ShopTint.WithAlpha(0.22f); line = DoorGold; }
                else if (a.Kind == RoomKind.BlackMarket || a.Kind == RoomKind.Arms || a.Kind == RoomKind.Medic || a.Kind == RoomKind.Hospital) { fill = KindInk(a.Kind, a.Floor).WithAlpha(0.2f); line = KindInk(a.Kind, a.Floor); }  // (a Black Market, Arms Dealer or Medic from a card or a door)
                else if (a.Kind == RoomKind.Floor) { fill = Paths.Get(a.Floor).Accent.WithAlpha(0.2f); line = Paths.Get(a.Floor).Accent; }
                else if (a.Hub) { fill = new Rgba(0.4f, 0.64f, 1f, 0.14f); line = new Rgba(0.4f, 0.64f, 1f, 0.75f); }
                else if (a.Hall) { fill = new Rgba(0.4f, 0.64f, 1f, 0.06f); line = new Rgba(0.4f, 0.64f, 1f, 0.35f); }
                else { fill = new Rgba(0.4f, 0.64f, 1f, 0.09f); line = new Rgba(0.4f, 0.64f, 1f, 0.5f); }
                bool core = CoreArea(i) || a.Hub && CoreArea(1);  // a Grid run's core: one room with the start room, its rim drawn once below
                if (core && !a.Hub) { fill = new Rgba(0.4f, 0.64f, 1f, 0.14f); }
                V2 p0 = M(a.Min), p1 = M(a.Max);
                MapRect(p0.X, p0.Y, p1.X - p0.X, p1.Y - p0.Y, fill);
                if (game.AreaUnseen(i))
                    MapRect(p0.X, p0.Y, p1.X - p0.X, p1.Y - p0.Y, FogMapFill.WithAlpha(FogMapFill.A * (1f - 0.65f * game.FogRoomShare(i - 1))));  // an unseen room: dark, a "?" below (RoomFogLook.cs)  // an unseen room: dark, a "?" below (RoomFogLook.cs)
                if (!core)
                    RimFrame(wd, i, M, 1f, line, true);
            }
            foreach (var s in wd.Pits)  // the pits: black with a gold edge (PitLook.cs)
            {
                if (game.FogHidesStill(s.Pos))
                    continue;
                float pw = s.Round ? s.R : s.HW, ph = s.Round ? s.R : s.HH;
                V2 q0 = M(s.Pos - new V2(pw, ph)), q1 = M(s.Pos + new V2(pw, ph));
                MapRect(q0.X, q0.Y, q1.X - q0.X, q1.Y - q0.Y, Rgba.Gold.WithAlpha(0.8f));
                MapRect(q0.X + 1f, q0.Y + 1f, q1.X - q0.X - 2f, q1.Y - q0.Y - 2f, new Rgba(0f, 0f, 0f, 0.9f));
            }
            if (CoreArea(1))  // the core's one outline (GridRooms.cs)
            {
                V2 c0 = M(new V2(Expansion.GridX0, Expansion.GridY0)), c1 = M(new V2(Expansion.GridX0 + 4 * Expansion.GridCell, Expansion.GridY0 + 3 * Expansion.GridCell));
                var cl = new Rgba(0.4f, 0.64f, 1f, 0.75f);
                MapRect(c0.X, c0.Y, c1.X - c0.X, 1f, cl); MapRect(c0.X, c1.Y - 1f, c1.X - c0.X, 1f, cl);
                MapRect(c0.X, c0.Y, 1f, c1.Y - c0.Y, cl); MapRect(c1.X - 1f, c0.Y, 1f, c1.Y - c0.Y, cl);
            }
            foreach (var n in game.Nodes)  // the nodes: a square with a dark edge, breathing and haloed until a room is built round it
            {
                V2 np = M(n.Pos);
                bool claimed = Expansion.NodeClaimed(game.Expanded, n);
                var ink = n.Hidden && !claimed ? FogInk : NodeInk(n.Kind);
                float br = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.5f);
                bool tiered = !n.Spent && !claimed && (Expansion.IsChest(n.Kind) || n.Kind == NodeKind.Relic);  // (owner Nov 18: an unclaimed prize node's edge and halo wear its tier's metal)
                var edge = tiered ? TierInk[Expansion.NodeTier(n)] : ink;
                if (!n.Spent && !claimed)
                    MapRect(np.X - 8f, np.Y - 8f, 16f, 16f, edge.WithAlpha((tiered ? 0.3f : 0.15f) + 0.15f * br));
                MapRect(np.X - 6f, np.Y - 6f, 12f, 12f, tiered ? edge : new Rgba(0f, 0f, 0f, n.Spent ? 0.25f : 0.6f));
                if (tiered)
                    MapRect(np.X - 5f, np.Y - 5f, 10f, 10f, new Rgba(0f, 0f, 0f, 0.7f));
                if (tiered)
                    MapRect(np.X - 4f, np.Y - 4f, 8f, 8f, ink.WithAlpha(0.8f + 0.2f * br));
                else
                    MapRect(np.X - 5f, np.Y - 5f, 10f, 10f, ink.WithAlpha(n.Spent ? 0.25f : claimed ? 0.9f : 0.8f + 0.2f * br));
            }
            for (int i = 0; i < wd.Doors.Length; i++)
            {
                if (wd.Seam(i) || CoreSeam(wd, i))
                    continue;
                V2 c = M(wd.Doors[i].Pos);
                MapRect(c.X - 2.5f, c.Y - 2.5f, 5f, 5f, DoorInk);
            }
            for (int i = 0; i < wd.Portals.Length; i++)  // the portals, in their pair's colour (a wall portal: a bar along its wall)
            {
                var pt = wd.Portals[i];
                for (int end = 0; end < 2; end++)
                {
                    V2 pp = end == 0 ? pt.A : pt.B, inn = end == 0 ? pt.InA : pt.InB;
                    V2 c = M(pp);
                    if (!InMap(c))
                        continue;
                    if (pt.Wall)
                    {
                        V2 h = inn.Orthogonal() * 5f;
                        Line(c - h, c + h, 3.5f, PortalColours[i % PortalColours.Length]);
                    }
                    else
                        Arc(c, 4f, 2f, PortalColours[i % PortalColours.Length], 12);
                }
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
                if (e.Hp <= 0f || game.FogHides(e.Pos))
                    continue;
                V2 c = M(e.Disp);
                if (e.Def.Mini || e.Def.IsBoss) { MapRect(c.X - 3f, c.Y - 3f, 6f, 6f, new Rgba(1f, 0.62f, 0.3f)); continue; }
                MapRect(c.X - 1f, c.Y - 1f, 2f, 2f, new Rgba(1f, 0.32f, 0.36f, 0.85f));
            }
            foreach (var ch in game.Props.Values)  // unopened treasure chests: a square in the kind's ink
            {
                if (ch.Kind != PropKind.Chest || ch.Open || ch.Hp <= 0f)
                    continue;
                V2 c = M(ch.Pos);
                MapRect(c.X - 4f, c.Y - 4f, 8f, 8f, new Rgba(0.02f, 0.03f, 0.06f, 0.9f));
                MapRect(c.X - 3f, c.Y - 3f, 6f, 6f, ch.Chest == ChestKind.Gold ? Rgba.Gold : ChestInkOf(ch.Chest));  // its kind's ink (ChestLooks.cs)
            }
            DrawGateOnMap(M);  // the boss gate (BossGateLook.cs)
            DrawMysteryOnMap(M);  // sealed mystery doors (MysteryDoorLook.cs)
            foreach (var co in game.Coins.Values)  // gold coins: a small gold dot (5-gold coins a bit bigger); vector pickups stay off
            {
                if (co.Vec || !game.MinePickup(co.Owner) || game.FogHides(co.Pos))
                    continue;
                V2 c = M(co.Disp);
                if (!InMap(c))
                    continue;
                float s = co.Value >= 5 ? 2.5f : 1.5f;
                MapRect(c.X - s - 0.5f, c.Y - s - 0.5f, 2f * s + 1f, 2f * s + 1f, new Rgba(0.02f, 0.03f, 0.06f, 0.8f));
                MapRect(c.X - s, c.Y - s, 2f * s, 2f * s, Rgba.Gold);
            }
            foreach (var p in game.Players.Values)
            {
                V2 c = M(p.Disp);
                bool mine = p.Id == game.MyId;
                if (!InMap(c))  // beyond the window: a dot on its edge
                {
                    c = new V2(Mathf.Clamp(c.X, ox + 3f, ox + mw - 3f), Mathf.Clamp(c.Y, oy + 3f, oy + mh - 3f));
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
            if (geo.Fold > 0f)  // switching off or on: the CRT's bright line across the middle
            {
                float cy = oy + mh / 2f;
                RoomMapRect(ox, cy - 4f, mw, 8f, new Rgba(0.55f, 0.75f, 1f, 0.22f * geo.Fold));
                RoomMapRect(ox, cy - 1f, mw, 2f, new Rgba(0.9f, 0.96f, 1f, 0.95f * geo.Fold));
            }
            EndUiGl();
            DrawFogMapMarks(M, geo.R);  // the unseen rooms' "?" (RoomFogLook.cs)
            DrawMapLabels(geo);  // the full map's title and legend, the "Map off" note (MapView.cs)
        }

        /// What the map shows: the world and the nodes.
        void MapBounds(out V2 lo, out V2 hi)
        {
            var wd = game.CurrentWorld;
            lo = wd.Min; hi = wd.Max;
            IncludeNodes(ref lo, ref hi);
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
