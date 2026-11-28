using UnityEngine;

namespace Arena
{
    /// The map's style (minimap and full map, owner's "fancier minimap", Nov 26; Holo picked): the same map drawn by DrawRoomsMinimap
    /// (RoomsLook.cs) with a different dress. 0 = the old plain look; 1 = Holo (a glowing tactical screen: lattice grid,
    /// scanlines, a sweep, bracket corners, sector letters, aim arrows); 2 = Blueprint (navy paper, fine and bold grid lines,
    /// pale double walls, hatched unseen rooms, a title block and scale bar). Preview: MAPSTYLE=<n>.
    public partial class ArenaApp
    {
        internal static int MapStyle = 1;  // (owner Nov 26: Holo is the look; 0 and 2 stay for comparison previews)

        static readonly Rgba HoloInk = new Rgba(0.4f, 0.78f, 1f), BlueprintInk = new Rgba(0.78f, 0.9f, 1f);

        /// How far grown into the full map (0 minimap, 1 full).
        float MapFullness(MapGeo g) => Mathf.Clamp01((g.R.width - RoomMapW) / (MapFullRect.width - RoomMapW));

        /// The backing, grid and frame, in place of the plain panel.
        void DrawStyledMapBack(MapGeo g, System.Func<V2, V2> M)
        {
            float ox = g.R.x, oy = g.R.y, mw = g.R.width, mh = g.R.height;
            float pad = 6f * Mathf.Clamp01(mh / 40f);
            float cellPx = Expansion.GridCell * g.K;
            V2 g0 = M(new V2(Expansion.GridX0, Expansion.GridY0));
            if (MapStyle == 1)
            {
                for (int i = 3; i >= 1; i--)  // the soft glow round the screen
                    RoomMapRect(ox - pad - 2f * i, oy - pad - 2f * i, mw + 2f * pad + 4f * i, mh + 2f * pad + 4f * i, HoloInk.WithAlpha(0.025f));
                RoomMapRect(ox - pad, oy - pad, mw + 2f * pad, mh + 2f * pad, new Rgba(0.005f, 0.02f, 0.05f, 0.88f + 0.08f * g.Dim));
                for (int i = 0; i < 6; i++)  // a lighter band at the top, like a lit glass
                    RoomMapRect(ox, oy + i * mh / 12f, mw, mh / 12f, HoloInk.WithAlpha(0.035f * (1f - i / 6f)));
                MapLattice(g0, cellPx, 0f, HoloInk.WithAlpha(0.09f), HoloInk.WithAlpha(0.0f));
                var edge = HoloInk.WithAlpha(0.3f + 0.2f * g.Dim);
                RoomMapFrame(ox - pad, oy - pad, mw + 2f * pad, mh + 2f * pad, 1f, edge);
                Brackets(ox - pad - 3f, oy - pad - 3f, mw + 2f * pad + 6f, mh + 2f * pad + 6f, 12f, 2f, HoloInk.WithAlpha(0.95f));
                return;
            }
            // Blueprint: navy paper, a fine grid every quarter cell and a bold one every cell, a double rule round it.
            RoomMapRect(ox - pad, oy - pad, mw + 2f * pad, mh + 2f * pad, new Rgba(0.03f, 0.1f, 0.24f, 0.92f + 0.06f * g.Dim));
            MapLattice(g0, cellPx, cellPx / 4f, BlueprintInk.WithAlpha(0.2f), BlueprintInk.WithAlpha(0.06f));
            RoomMapFrame(ox - pad, oy - pad, mw + 2f * pad, mh + 2f * pad, 1f, BlueprintInk.WithAlpha(0.85f));
            RoomMapFrame(ox - pad + 3f, oy - pad + 3f, mw + 2f * pad - 6f, mh + 2f * pad - 6f, 1f, BlueprintInk.WithAlpha(0.4f));
        }

        /// The lattice's lines across the window: bold every cell from g0, fine every `fine` px (0: none).
        void MapLattice(V2 g0, float cell, float fine, Rgba bold, Rgba thin)
        {
            if (cell < 4f)
                return;
            if (fine >= 3f)
            {
                for (float x = g0.X + Mathf.Floor((mapOx - g0.X) / fine) * fine; x < mapOx + mapW; x += fine) MapRect(x, mapOy, 1f, mapH, thin);
                for (float y = g0.Y + Mathf.Floor((mapOy - g0.Y) / fine) * fine; y < mapOy + mapH; y += fine) MapRect(mapOx, y, mapW, 1f, thin);
            }
            for (float x = g0.X + Mathf.Floor((mapOx - g0.X) / cell) * cell; x < mapOx + mapW; x += cell) MapRect(x, mapOy, 1f, mapH, bold);
            for (float y = g0.Y + Mathf.Floor((mapOy - g0.Y) / cell) * cell; y < mapOy + mapH; y += cell) MapRect(mapOx, y, mapW, 1f, bold);
        }

        /// Bracket corners round a box: arms `len` long, `t` thick.
        void Brackets(float x, float y, float w, float h, float len, float t, Rgba c)
        {
            RoomMapRect(x, y, len, t, c); RoomMapRect(x, y, t, len, c);
            RoomMapRect(x + w - len, y, len, t, c); RoomMapRect(x + w - t, y, t, len, c);
            RoomMapRect(x, y + h - t, len, t, c); RoomMapRect(x, y + h - len, t, len, c);
            RoomMapRect(x + w - len, y + h - t, len, t, c); RoomMapRect(x + w - t, y + h - len, t, len, c);
        }

        /// After an area's fill and rim: Holo adds an inner glow line, Blueprint a pale double wall and hatching when unseen.
        void StyledRoomExtra(World wd, int i, System.Func<V2, V2> M, Rgba line, bool unseen)
        {
            var a = wd.Areas[i];
            V2 p0 = M(a.Min), p1 = M(a.Max);
            float w = p1.X - p0.X, h = p1.Y - p0.Y;
            if (w < 4f || h < 4f)
                return;
            if (MapStyle == 1)
            {
                if (!unseen)
                {
                    MapFrame(p0.X + 1f, p0.Y + 1f, w - 2f, h - 2f, 1f, line.WithAlpha(0.28f));
                    MapFrame(p0.X + 2f, p0.Y + 2f, w - 4f, h - 4f, 1f, line.WithAlpha(0.1f));
                }
                return;
            }
            var wall = new Rgba(line.R + (BlueprintInk.R - line.R) * 0.6f, line.G + (BlueprintInk.G - line.G) * 0.6f, line.B + (BlueprintInk.B - line.B) * 0.6f);
            RimFrame(wd, i, M, 1.5f, wall.WithAlpha(0.95f), true);
            if (a.Group == 0)
                MapFrame(p0.X + 2.5f, p0.Y + 2.5f, w - 5f, h - 5f, 1f, wall.WithAlpha(0.35f));
            if (unseen)  // hatched, like a part not drawn yet
                for (float s = 6f; s < w + h; s += 6f)
                    ClippedLine(new V2(p0.X + s, p0.Y), new V2(p0.X + s - h, p0.Y + h), p0, p1, 1f, BlueprintInk.WithAlpha(0.16f));
        }

        /// A line clipped to the box lo-hi and to the map window.
        void ClippedLine(V2 a, V2 b, V2 lo, V2 hi, float wdt, Rgba c)
        {
            float x0 = Mathf.Max(lo.X, mapOx), y0 = Mathf.Max(lo.Y, mapOy), x1 = Mathf.Min(hi.X, mapOx + mapW), y1 = Mathf.Min(hi.Y, mapOy + mapH);
            float t0 = 0f, t1 = 1f, dx = b.X - a.X, dy = b.Y - a.Y;
            float[] p = { -dx, dx, -dy, dy }, q = { a.X - x0, x1 - a.X, a.Y - y0, y1 - a.Y };
            for (int k = 0; k < 4; k++)
            {
                if (Mathf.Abs(p[k]) < 1e-5f) { if (q[k] < 0f) return; continue; }
                float r = q[k] / p[k];
                if (p[k] < 0f) t0 = Mathf.Max(t0, r); else t1 = Mathf.Min(t1, r);
            }
            if (t1 > t0)
                Line(new V2(a.X + dx * t0, a.Y + dy * t0), new V2(a.X + dx * t1, a.Y + dy * t1), wdt, c);
        }

        /// An enemy's mark: Holo gives it a soft halo.
        /// Holo's sweep: how far across the map window it is (0 left edge, 1 right), one pass every 1/SweepRate s.
        const float SweepRate = 0.35f, SweepHold = 1f, SweepFade = 0.8f;
        static float SweepPhase() => (Time.unscaledTime * SweepRate) % 1f;

        /// Holo's sweep reveals enemies (owner Nov 28): how much of an enemy at window x shows: full as the line passes it
        /// and for SweepHold s, then fading out over SweepFade s; 1 without the sweep (other styles). ping: 1 at the
        /// crossing, gone by 0.25 s (the bright flash and ring).
        float SweepReveal(float x, float ox, float mw, out float ping)
        {
            ping = 0f;
            if (MapStyle != 1 || mw < 1f)
                return 1f;
            float since = (SweepPhase() - (x - ox) / mw) % 1f;  // the share of a pass since the line crossed x
            if (since < 0f) since += 1f;
            since /= SweepRate;
            ping = Mathf.Clamp01(1f - since / 0.25f);
            return since <= SweepHold ? 1f : Mathf.Clamp01(1f - (since - SweepHold) / SweepFade);
        }

        void StyledEnemyHalo(V2 c, bool big, float a = 1f)
        {
            if (MapStyle == 1)
                MapRect(c.X - (big ? 6f : 3f), c.Y - (big ? 6f : 3f), big ? 12f : 6f, big ? 12f : 6f, new Rgba(1f, 0.3f, 0.32f, (big ? 0.25f : 0.16f) * a));
        }

        /// A player inside the window: an arrow along their aim (yours with a pulsing ring), a dark edge under it.
        void DrawStyledPlayerMark(Player p, V2 c, bool mine)
        {
            var col = p.Downed ? new Rgba(1f, 0.3f, 0.3f) : p.Def.Color;
            V2 d = new V2(Mathf.Cos(p.Aim), Mathf.Sin(p.Aim)), n = d.Orthogonal();
            float s = mine ? 6f : 4.5f;
            if (MapStyle == 1)
            {
                if (mine)
                {
                    float pulse = (Time.unscaledTime * 0.8f) % 1f;
                    Arc(c, 5f + 9f * pulse, 1.2f, new Rgba(1f, 1f, 1f, 0.6f * (1f - pulse)), 20);
                    Circle(c, 7f, HoloInk.WithAlpha(0.18f), 16);
                }
                SetColor(new Rgba(0f, 0.01f, 0.03f, 0.9f));
                Tri(c + d * (s + 2f), c - d * (s * 0.6f + 1.5f) + n * (s * 0.8f + 1.5f), c - d * (s * 0.6f + 1.5f) - n * (s * 0.8f + 1.5f));
                SetColor(mine ? new Rgba(1f, 1f, 1f) : col);
                Tri(c + d * s, c - d * (s * 0.6f) + n * (s * 0.8f), c - d * (s * 0.6f) - n * (s * 0.8f));
                if (mine)
                {
                    SetColor(col);
                    Tri(c + d * (s * 0.35f), c - d * (s * 0.35f) + n * (s * 0.4f), c - d * (s * 0.35f) - n * (s * 0.4f));
                }
                return;
            }
            // Blueprint: a ringed dot with an aim tick, like a surveyor's mark.
            Circle(c, mine ? 4.5f : 3.5f, new Rgba(0.03f, 0.1f, 0.24f, 1f), 14);
            Arc(c, mine ? 4.5f : 3.5f, 1.3f, mine ? new Rgba(1f, 1f, 1f) : col, 16);
            Circle(c, mine ? 2f : 1.6f, col, 10);
            Line(c + d * (mine ? 5.5f : 4.5f), c + d * (mine ? 10f : 8f), 1.4f, mine ? new Rgba(1f, 1f, 1f) : col);
            if (mine)
            {
                Line(c - new V2(9f, 0f), c - new V2(6f, 0f), 1f, BlueprintInk.WithAlpha(0.7f));
                Line(c + new V2(6f, 0f), c + new V2(9f, 0f), 1f, BlueprintInk.WithAlpha(0.7f));
                Line(c - new V2(0f, 9f), c - new V2(0f, 6f), 1f, BlueprintInk.WithAlpha(0.7f));
                Line(c + new V2(0f, 6f), c + new V2(0f, 9f), 1f, BlueprintInk.WithAlpha(0.7f));
            }
        }

        /// The box your screen shows: corners only (Holo) or a dashed box (Blueprint).
        void StyledViewBox(float x0, float y0, float x1, float y1)
        {
            float w = x1 - x0, h = y1 - y0, len = Mathf.Min(8f, Mathf.Min(w, h) / 3f);
            if (MapStyle == 1)
            {
                MapRect(x0, y0, w, h, new Rgba(1f, 1f, 1f, 0.03f));
                MapRect(x0, y0, len, 1f, Rgba.White); MapRect(x0, y0, 1f, len, Rgba.White);
                MapRect(x1 - len, y0, len, 1f, Rgba.White); MapRect(x1 - 1f, y0, 1f, len, Rgba.White);
                MapRect(x0, y1 - 1f, len, 1f, Rgba.White); MapRect(x0, y1 - len, 1f, len, Rgba.White);
                MapRect(x1 - len, y1 - 1f, len, 1f, Rgba.White); MapRect(x1 - 1f, y1 - len, 1f, len, Rgba.White);
                return;
            }
            var c = new Rgba(1f, 1f, 1f, 0.6f);
            for (float x = x0; x < x1; x += 6f) { MapRect(x, y0, Mathf.Min(3f, x1 - x), 1f, c); MapRect(x, y1 - 1f, Mathf.Min(3f, x1 - x), 1f, c); }
            for (float y = y0; y < y1; y += 6f) { MapRect(x0, y, 1f, Mathf.Min(3f, y1 - y), c); MapRect(x1 - 1f, y, 1f, Mathf.Min(3f, y1 - y), c); }
        }

        /// Over everything (GL): Holo's scanlines, sweep and edge ticks; Blueprint's title block box and scale bar.
        void DrawStyledMapOver(MapGeo g, System.Func<V2, V2> M)
        {
            float ox = g.R.x, oy = g.R.y, mw = g.R.width, mh = g.R.height;
            float cellPx = Expansion.GridCell * g.K;
            V2 g0 = M(new V2(Expansion.GridX0, Expansion.GridY0));
            if (MapStyle == 1)
            {
                for (float y = oy; y < oy + mh; y += 3f)
                    RoomMapRect(ox, y, mw, 1f, new Rgba(0f, 0f, 0f, 0.16f));
                float sx = ox + SweepPhase() * mw;  // the sweep: a bright line with a fading wake
                for (int i = 0; i < 10; i++)
                    MapRect(sx - (i + 1) * 4f, oy, 4f, mh, HoloInk.WithAlpha(0.06f * (1f - i / 10f)));
                MapRect(sx, oy, 1.5f, mh, HoloInk.WithAlpha(0.55f));
                if (cellPx >= 8f)  // ticks along the edges at each lattice line
                {
                    for (float x = g0.X + Mathf.Ceil((ox - g0.X) / cellPx) * cellPx; x < ox + mw; x += cellPx)
                    { RoomMapRect(x, oy, 1f, 4f, HoloInk.WithAlpha(0.8f)); RoomMapRect(x, oy + mh - 4f, 1f, 4f, HoloInk.WithAlpha(0.8f)); }
                    for (float y = g0.Y + Mathf.Ceil((oy - g0.Y) / cellPx) * cellPx; y < oy + mh; y += cellPx)
                    { RoomMapRect(ox, y, 4f, 1f, HoloInk.WithAlpha(0.8f)); RoomMapRect(ox + mw - 4f, y, 4f, 1f, HoloInk.WithAlpha(0.8f)); }
                }
                if (MapFullness(g) < 0.3f)  // the minimap's tab above it
                {
                    RoomMapRect(ox - 6f, oy - 22f, 92f, 14f, new Rgba(0.005f, 0.02f, 0.05f, 0.88f));
                    RoomMapRect(ox - 6f, oy - 22f, 3f, 14f, HoloInk);
                    RoomMapRect(ox + 86f, oy - 22f, mw - 80f, 1f, HoloInk.WithAlpha(0.35f));
                }
                return;
            }
            // Blueprint: a title block in the bottom-right corner and a one-cell scale bar bottom-left.
            float lift = MapFullness(g) > 0.7f ? MapFullFoot + 6f : 0f, bw = 84f, bh = 22f, bx = ox + mw - bw - lift / 4f, by = oy + mh - bh - lift;
            RoomMapRect(bx, by, bw, bh, new Rgba(0.03f, 0.1f, 0.24f, 0.95f));
            RoomMapFrame(bx, by, bw, bh, 1f, BlueprintInk.WithAlpha(0.8f));
            RoomMapRect(bx, by + bh / 2f, bw, 1f, BlueprintInk.WithAlpha(0.4f));
            if (cellPx >= 8f)
            {
                float sx = ox + 8f, sy = oy + mh - 9f - lift, len = cellPx;
                RoomMapRect(sx, sy, len, 1.5f, BlueprintInk);
                RoomMapRect(sx, sy - 3f, 1f, 6f, BlueprintInk); RoomMapRect(sx + len - 1f, sy - 3f, 1f, 6f, BlueprintInk);
                RoomMapRect(sx, sy - 1.5f, len / 2f, 3f, BlueprintInk.WithAlpha(0.8f));
            }
        }

        /// After the GL pass: the labels (Holo's tab text and sector letters; Blueprint's title block text and scale).
        void DrawStyledMapLabels(MapGeo g, System.Func<V2, V2> M)
        {
            if (g.Fold > 0.05f)
                return;
            float ox = g.R.x, oy = g.R.y, mw = g.R.width, mh = g.R.height, full = MapFullness(g);
            float cellPx = Expansion.GridCell * g.K;
            V2 g0 = M(new V2(Expansion.GridX0, Expansion.GridY0));
            int wave = game.Wave;
            if (MapStyle == 1)
            {
                var ink = HoloInk.ToColor();
                if (full < 0.3f)
                {
                    Ui.Label(new Rect(ox + 2f, oy - 23f, 90f, 16f), "MAP  ·  W" + wave, 10, ink, TextAnchor.MiddleLeft, true);
                    Ui.Label(new Rect(ox + mw - 120f, oy - 23f, 120f, 16f), game.Remaining + " HOSTILES", 9, new Color(1f, 0.45f, 0.45f, 0.85f), TextAnchor.MiddleRight, true);
                }
                if (full > 0.7f && cellPx >= 24f)  // sector letters across the top, numbers down the side
                {
                    float la = Mathf.Clamp01((full - 0.7f) / 0.3f);
                    int i0 = Mathf.CeilToInt((ox + 24f - g0.X) / cellPx - 0.5f);  // (lettered from the first whole column: A, B, C...)
                    for (int i = i0; g0.X + (i + 0.5f) * cellPx < ox + mw - 8f; i++)
                    {
                        float x = g0.X + (i + 0.5f) * cellPx;
                        string s = ((char)('A' + (i - i0) % 26)).ToString();
                        Ui.Label(new Rect(x - 12f, oy + MapFullTop + 2f, 24f, 14f), s, 9, new Color(ink.r, ink.g, ink.b, 0.55f * la), TextAnchor.MiddleCenter, true);
                    }
                    int j0 = Mathf.CeilToInt((oy + MapFullTop + 16f - g0.Y) / cellPx - 0.5f);
                    for (int j = j0; g0.Y + (j + 0.5f) * cellPx < oy + mh - MapFullFoot; j++)
                        Ui.Label(new Rect(ox + 4f, g0.Y + (j + 0.5f) * cellPx - 7f, 20f, 14f), (j - j0 + 1).ToString(), 9, new Color(ink.r, ink.g, ink.b, 0.55f * la), TextAnchor.MiddleCenter, true);
                }
                return;
            }
            var bi = BlueprintInk.ToColor();
            float lift = MapFullness(g) > 0.7f ? MapFullFoot + 6f : 0f, bw = 84f, bh = 22f, bx = ox + mw - bw - lift / 4f, by = oy + mh - bh - lift;
            Ui.Label(new Rect(bx + 4f, by, bw - 8f, bh / 2f), "SHEET " + wave.ToString("00") + "  ·  ARENA", 8, bi, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(bx + 4f, by + bh / 2f, bw - 8f, bh / 2f), "SCALE 1:" + Mathf.RoundToInt(1f / g.K), 8, new Color(bi.r, bi.g, bi.b, 0.7f), TextAnchor.MiddleLeft, true);
            if (cellPx >= 8f)
                Ui.Label(new Rect(ox + 8f + cellPx + 3f, oy + mh - 16f - lift, 50f, 14f), "1 CELL", 8, new Color(bi.r, bi.g, bi.b, 0.8f), TextAnchor.MiddleLeft, true);
        }
    }
}
