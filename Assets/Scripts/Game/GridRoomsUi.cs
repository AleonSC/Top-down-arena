using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Grid pieces on the build screen (Docs/GridPieces.md step 3): your hand of pieces on the right, the picked piece following the mouse as a ghost
    /// on the door spots, R for the other turns and spots that fit there, a click to build it. Called from DrawExpansionDraw in a Grid run.
    public partial class ArenaApp
    {
        int expGridSlot = -1;                 // the hand slot picked on the build screen (-1: none)
        int expGridTurn;                      // which of the fits under the mouse (R steps through them)
        List<Placement> gridOpts;       // the picked piece's legal places (rebuilt when the map or the card changes)
        int gridOptsKey = int.MinValue;

        static readonly Rgba PieceCombatInk = new Rgba(0.9f, 0.38f, 0.36f), PieceLootInk = new Rgba(1f, 0.8f, 0.3f), GridInk = new Rgba(0.35f, 0.85f, 0.78f), GridOk = new Rgba(0.35f, 1f, 0.55f);

        PieceCard PickedPieceCard
        {
            get
            {
                var me = game.Me;
                if (me == null || expGridSlot < 0 || expGridSlot >= me.Hand.Length)
                    return PieceCard.None;
                var c = (PieceCard)me.Hand[expGridSlot];
                return Expansion.IsGridPieceCard(c) ? c : PieceCard.None;
            }
        }

        List<Placement> PlacementsNow()
        {
            var card = PickedPieceCard;
            if (card == PieceCard.None)
                return gridOpts = new List<Placement>();
            int key = (int)card * 7919 + game.Expanded.Count * 131 + game.Nodes.Count * 17;
            foreach (var r in game.Expanded)
                key = key * 31 + r.X * 7 + r.Y * 13 + r.W + r.H * 3;
            if (gridOpts == null || key != gridOptsKey)
            {
                gridOpts = Expansion.PlacementsForCard(game.Expanded, game.Nodes, card);
                gridOptsKey = key;
            }
            return gridOpts;
        }

        readonly List<Placement> gridHits = new List<Placement>();  // GridFitsAt's result, refilled each call
        readonly NearestMidFirst gridNearFirst = new NearestMidFirst();
        /// Orders places by how far their middle is from a point (To), nearest first.
        sealed class NearestMidFirst : IComparer<Placement>
        {
            public V2 To;
            public int Compare(Placement a, Placement b) => a.Mid.DistanceTo(To).CompareTo(b.Mid.DistanceTo(To));
        }

        /// The places under the mouse (world point `wm`): every one whose room covers it, else the nearest within 5 tiles; nearest middle first.
        List<Placement> GridFitsAt(V2 wm)
        {
            var hits = gridHits;
            hits.Clear();
            foreach (var o in PlacementsNow())
                foreach (var q in o.Rooms)
                    if (wm.X >= q.X && wm.X < q.X1 && wm.Y >= q.Y && wm.Y < q.Y1)
                    {
                        hits.Add(o);
                        break;
                    }
            if (hits.Count == 0)
            {
                float best = 500f;
                Placement? near = null;
                foreach (var o in PlacementsNow())
                {
                    float d = new V2(o.SX, o.SY).DistanceTo(wm);
                    if (d < best) { best = d; near = o; }
                }
                if (near.HasValue)
                    hits.Add(near.Value);
            }
            gridNearFirst.To = wm;
            hits.Sort(gridNearFirst);
            return hits;
        }

        /// Grid mode's mouse and keys over the map. Returns the place under the mouse (null if none); a click builds it.
        Placement? GridModeInput(Event ev, bool over, V2 wm, bool free, out int fits)
        {
            fits = 0;
            var me = game.Me;
            if (me != null && (expGridSlot >= me.Hand.Length || expGridSlot < -1 || (expGridSlot >= 0 && me.Hand[expGridSlot] == 0)))
                expGridSlot = -1;
            if ((ev.type == EventType.MouseDown && ev.button == 1) || (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.Escape))
            {
                if (expGridSlot >= 0)
                    expGridSlot = -1;
                else if (free)
                    expOpen = false;
                ev.Use();
                return null;
            }
            if (PickedPieceCard == PieceCard.None || !over)
                return null;
            var hits = GridFitsAt(wm);
            fits = hits.Count;
            if (hits.Count == 0)
            {
                if (ev.type == EventType.MouseDown && ev.button == 0)  // fits nowhere here: a red cross shakes (BuildMotion.cs)
                {
                    gridDenyT0 = Time.unscaledTime;
                    gridDenyAt = wm;
                }
                return null;
            }
            if (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.R)
            {
                expGridTurn++;
                ev.Use();
            }
            var pick = hits[((expGridTurn % hits.Count) + hits.Count) % hits.Count];
            if (ev.type == EventType.MouseDown && ev.button == 0)
            {
                StartCardFly(expGridSlot, pick.Mid);  // the card flies to the spot (BuildMotion.cs)
                game.PlayPieceCard(expGridSlot, pick.Orient, pick.X, pick.Y);
                expGridSlot = -1;
                expGridTurn = 0;
                ev.Use();
                return null;
            }
            return pick;
        }

        /// The map (inside the GL pass): a pulsing mark on every door spot the picked piece fits, the piece under the mouse lit cell by cell.
        void DrawGridGhost(System.Func<V2, V2> M, Placement? pick)
        {
            if (PickedPieceCard == PieceCard.None)
                return;
            float pulse = 0.55f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f);
            var seen = new HashSet<long>();
            foreach (var o in PlacementsNow())
            {
                if (!seen.Add(((long)o.SX << 32) ^ (uint)o.SY))
                    continue;
                V2 c = M(new V2(o.SX, o.SY));
                RoomMapRect(c.X - 4f, c.Y - 4f, 8f, 8f, GridInk.WithAlpha(pulse));
                RoomMapFrame(c.X - 6f, c.Y - 6f, 12f, 12f, 1f, GridInk.WithAlpha(0.5f));
            }
            if (!pick.HasValue)
            {
                ghostLast = null;
                return;
            }
            V2 off = GhostOffset(pick.Value);  // gliding from the last spot (BuildMotion.cs)
            System.Func<V2, V2> G = w => M(w + off);
            var cells = Expansion.PieceShapeOrients(Expansion.PieceShapeOf(PickedPieceCard))[pick.Value.Orient];
            float fill = 0.2f + 0.08f * Mathf.Sin(Time.unscaledTime * 4f);
            float spin = GhostSpin;
            if (spin != 0f)  // R: the new turn spins the last quarter into place, cells as turned quads
            {
                V2 mid = G(pick.Value.Mid);
                float cs = Mathf.Cos(spin), sn = Mathf.Sin(spin);
                System.Func<V2, V2> rot = p => { V2 q = p - mid; return mid + new V2(q.X * cs - q.Y * sn, q.X * sn + q.Y * cs); };
                foreach (var cell in cells)
                {
                    V2 a = G(new V2(pick.Value.X + cell[0] * Expansion.GridCell, pick.Value.Y + cell[1] * Expansion.GridCell));
                    V2 b = G(new V2(pick.Value.X + (cell[0] + 1) * Expansion.GridCell, pick.Value.Y + (cell[1] + 1) * Expansion.GridCell));
                    V2 c0 = rot(a), c1 = rot(new V2(b.X, a.Y)), c2 = rot(b), c3 = rot(new V2(a.X, b.Y));
                    SetColor(GridOk.WithAlpha(fill + 0.1f));
                    Quad(c0, c1, c2, c3);
                    Line(c0, c1, 1.5f, GridOk); Line(c1, c2, 1.5f, GridOk); Line(c2, c3, 1.5f, GridOk); Line(c3, c0, 1.5f, GridOk);
                }
                return;
            }
            foreach (var cell in cells)  // each cell, so the piece reads as cells joined into one room
            {
                V2 p0 = G(new V2(pick.Value.X + cell[0] * Expansion.GridCell, pick.Value.Y + cell[1] * Expansion.GridCell));
                V2 p1 = G(new V2(pick.Value.X + (cell[0] + 1) * Expansion.GridCell, pick.Value.Y + (cell[1] + 1) * Expansion.GridCell));
                RoomMapRect(p0.X, p0.Y, p1.X - p0.X, p1.Y - p0.Y, GridOk.WithAlpha(fill));
                RoomMapFrame(p0.X + 2f, p0.Y + 2f, p1.X - p0.X - 4f, p1.Y - p0.Y - 4f, 1f, GridOk.WithAlpha(0.35f));
            }
            foreach (var q in pick.Value.Rooms)  // the room's edge: marching dashes (still under Reduced effects)
            {
                V2 p0 = G(new V2(q.X, q.Y)), p1 = G(new V2(q.X1, q.Y1));
                if (FxCalm)
                    RoomMapFrame(p0.X, p0.Y, p1.X - p0.X, p1.Y - p0.Y, 1.5f, GridOk.WithAlpha(0.9f));
                else
                    DashFrame(p0.X, p0.Y, p1.X - p0.X, p1.Y - p0.Y, 2f, GridOk.WithAlpha(0.95f));
            }
            V2 d = G(new V2(pick.Value.SX, pick.Value.SY));
            RoomMapRect(d.X - 5f, d.Y - 5f, 10f, 10f, GridOk);
            DrawGhostOneWays(G, pick.Value, PickedPieceCard);  // a One-way card: the arrows its doors would get (OneWayDoorLook.cs)
        }

        /// The status line for grid mode.
        string GridRoomLine(Placement? pick, int fits, bool over, out Color ink)
        {
            ink = Ui.Muted;
            var me = game.Me;
            if (me == null || Game.HandCount(me) == 0)
                return "No pieces in your hand: one comes each round, and the Black Market and the Builder Shop sell them.";
            var card = PickedPieceCard;
            if (card == PieceCard.None)
                return "Pick a piece from your hand.";
            if (PlacementsNow().Count == 0)
            {
                ink = new Color(1f, 0.45f, 0.5f);
                return "No door spot fits a " + Expansion.CardName(card).ToLowerInvariant() + " right now.";
            }
            if (!pick.HasValue)
                return over ? "Move along a wall: the glowing marks are the door spots." : "Point at the map: the glowing marks are the door spots.";
            ink = CrtStyle.Gold;
            return Expansion.CardName(card).ToUpperInvariant() + (fits > 1 ? "  ·  R  " + (((expGridTurn % fits) + fits) % fits + 1) + " OF " + fits + " FITS HERE" : "") + "  ·  click to build it";
        }

        /// The hand: two pieces a row, each drawn as its cells with its name; click one to pick it. Cards a Grid run can't play are dimmed.
        void DrawGridPalette(Rect area, bool calm)
        {
            var me = game.Me;
            int n = me != null ? Game.HandCount(me) : 0;
            Ui.Label(new Rect(area.x, area.y, area.width, 16f), "YOUR PIECES  ·  " + n + " / " + Expansion.HandMax, 10, Ui.Muted, TextAnchor.MiddleLeft, true);
            float gap = 6f, cw = (area.width - gap) / 2f, ch = 74f, y0 = area.y + 20f;
            if (me != null)
                NoteHand(me.Hand, n, s => new Vector2((s % 2) * (cw + gap), (s / 2) * (ch + gap)));  // the hand closing up after a card leaves (BuildMotion.cs)
            float a0 = Ui.Alpha;
            for (int i = 0; i < n; i++)
            {
                var card = (PieceCard)me.Hand[i];
                bool playable = Expansion.IsGridPieceCard(card);
                var r = new Rect(area.x + (i % 2) * (cw + gap), y0 + (i / 2) * (ch + gap), cw, ch);
                if (i < gridSlotRects.Length) gridSlotRects[i] = r;
                bool on = i == expGridSlot;
                float ca;
                r = HandCardRect(i, r, out ca);
                if (on && !FxCalm)
                    r.y -= 3f + 1.5f * Mathf.Sin(Time.unscaledTime * 3f);  // the picked card lifts
                Ui.Alpha = a0 * ca;
                if (DkKey(r, "", on, calm, size: 11, rawSize: true) && playable)
                {
                    expGridSlot = on ? -1 : i;
                    expGridTurn = 0;
                }
                var ink = CardColor(card).ToColor();
                if (!playable)
                    ink = ink.WithAlpha(0.35f);
                DrawCardGlyph(card, new Rect(r.x + 8f, r.y + 6f, r.width - 16f, 38f), on ? Color.white : ink);
                var modNames = Expansion.IsShapePieceCard(card) ? PieceMods.Names(Expansion.PieceCardMods(card)) : new List<string>();
                bool floor = modNames.Count > 0;  // a piece with modifiers (floor, combat, ...)
                string name = Expansion.IsShapePieceCard(card) ? Expansion.PieceShapeName(Expansion.ShapeOfPieceCard(card)) : Expansion.CardName(card);
                bool stacked = modNames.Count > 1, packed = modNames.Count > 2;  // several modifiers: one line each, a size smaller (three: tighter still)
                Ui.Label(new Rect(r.x, r.y + (packed ? 39f : stacked ? 42f : floor ? 46f : 52f), r.width, 16f), name.ToUpperInvariant(), 11, on ? Color.white : ink, TextAnchor.MiddleCenter, true);
                for (int mi = 0; mi < modNames.Count; mi++)  // a modded piece: its modifiers under the name, in the card's colour
                    Ui.Label(new Rect(r.x + 4f, r.y + (packed ? 52f + mi * 8f : stacked ? 55f + mi * 9f : 59f), r.width - 8f, packed ? 9f : stacked ? 10f : 13f), modNames[mi].ToUpperInvariant(), packed ? 7 : stacked ? 8 : 9, on ? Color.white : ink, TextAnchor.MiddleCenter, true);
                if (TipHover(r))
                {
                    TipStart(r, Expansion.CardName(card), ink);
                    TipAdd(playable ? Expansion.CardBlurb(card) : "An old room card: it can't be played in a Grid run.", 12, Ui.Text);
                    if (playable)
                        TipAdd("Click it, point at a door spot, click to build. R turns it.", 11, Ui.Muted);
                }
                if (on)
                    DrawPickedCardLook(r);
            }
            Ui.Alpha = a0;
            float y = y0 + ((n + 1) / 2) * (ch + gap);
            Ui.Label(new Rect(area.x, y, area.width, 30f), "+1 piece each round. More from bosses, chests, the Black Market and the Builder Shop.", 10, Ui.Muted, TextAnchor.UpperLeft, true, true);
        }
    
        // --- The map's lattice and the placing effect (owner Nov 18: grid C, effect C) --------------------------------------------

        /// The lattice the pieces snap to, faint, fading out with distance from the map: cell lines (6 tiles) nearest the rooms you have, gone 15 tiles out.
        void DrawGridLattice(System.Func<V2, V2> M, V2 lo, V2 hi)
        {
            int bx0 = 0, by0 = 0, bx1 = (int)Game.Width, by1 = (int)Game.Height;
            foreach (var r in game.Expanded)
                if (!r.Floating)
                {
                    bx0 = Mathf.Min(bx0, r.X); by0 = Mathf.Min(by0, r.Y); bx1 = Mathf.Max(bx1, r.X1); by1 = Mathf.Max(by1, r.Y1);
                }
            int c = Expansion.GridCell;
            int x0 = Expansion.GridX0 + Mathf.FloorToInt((lo.X - Expansion.GridX0) / c) * c, y0 = Expansion.GridY0 + Mathf.FloorToInt((lo.Y - Expansion.GridY0) / c) * c;
            for (int x = x0; x <= hi.X; x += c)
                for (int y = y0; y <= hi.Y; y += c)
                {
                    float mx = x + c / 2f, my = y + c / 2f;
                    float dx = Mathf.Max(0f, Mathf.Max(bx0 - mx, mx - bx1)), dy = Mathf.Max(0f, Mathf.Max(by0 - my, my - by1));
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = 0.16f * (1f - dist / 1500f);
                    if (!FxCalm)  // a slow shimmer rolling out from the map (BuildMotion.cs H)
                        a *= 0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * 1.4f - dist / 300f);
                    if (a <= 0.005f)
                        continue;
                    var ink = GridInk.WithAlpha(a);
                    V2 p0 = M(new V2(x, y)), p1 = M(new V2(x + c, y + c));
                    RoomMapRect(p0.X, p0.Y, p1.X - p0.X, 1f, ink);  // (each cell its top and left edge: every line drawn once)
                    RoomMapRect(p0.X, p0.Y, 1f, p1.Y - p0.Y, ink);
                }
        }

        int placeFxSeen = -1;            // the map's room count last frame (-1: not looked yet)
        float placeFxT0 = -9f;
        readonly List<ExpansionRoom> placeFxRooms = new List<ExpansionRoom>();
        V2 placeFxDoor, placeFxDir;      // where the new piece meets the map, and the way it faces (out from the map)
        float placeFxEdge0, placeFxEdge1; // the shared stretch along that wall
        bool placeFxVertical;

        /// A piece just built slides in from a cell out and slams into the wall: a white flash over it, the wall it joins pulsing gold, sparks at the door.
        void DrawPlaceFx(System.Func<V2, V2> M, float k)
        {
            var built = game.Expanded;
            if (placeFxSeen < 0 || built.Count < placeFxSeen)
                placeFxSeen = built.Count;
            if (built.Count > placeFxSeen)
            {
                int first = placeFxSeen;
                placeFxSeen = built.Count;
                placeFxRooms.Clear();
                for (int i = first; i < built.Count; i++)
                    if (!built[i].Floating)
                        placeFxRooms.Add(built[i]);
                if (placeFxRooms.Count > 0 && placeFxRooms.Count <= 8 && FindPlaceDoor(built, first))
                    placeFxT0 = Time.unscaledTime;
            }
            float t = Time.unscaledTime - placeFxT0;
            if (t < 0f || t > 1.4f || placeFxRooms.Count == 0)
                return;
            float sl = Mathf.Min(1f, t / 0.35f), off = (1f - sl * sl) * Expansion.GridCell;
            foreach (var r in placeFxRooms)
            {
                V2 p0 = M(new V2(r.X, r.Y) + placeFxDir * off), p1 = M(new V2(r.X1, r.Y1) + placeFxDir * off);
                RoomMapRect(p0.X, p0.Y, p1.X - p0.X, p1.Y - p0.Y, GridInk.WithAlpha(sl < 1f ? 0.3f : 0f));
                if (sl < 1f)
                    RoomMapFrame(p0.X, p0.Y, p1.X - p0.X, p1.Y - p0.Y, 2f, GridInk.WithAlpha(0.95f));
            }
            float after = t - 0.35f;
            if (after < 0f)
                return;
            DrawPlaceRing(M, after);  // the shockwave (BuildMotion.cs)
            float flash = Mathf.Max(0f, 1f - after * 2.5f);
            if (flash > 0f)
                foreach (var r in placeFxRooms)
                {
                    V2 p0 = M(new V2(r.X, r.Y)), p1 = M(new V2(r.X1, r.Y1));
                    RoomMapRect(p0.X, p0.Y, p1.X - p0.X, p1.Y - p0.Y, new Rgba(1f, 1f, 1f, 0.55f * flash));
                }
            float pulse = Mathf.Max(0f, 1f - after * 1.6f);
            if (pulse > 0f)
            {
                var gold = new Rgba(1f, 0.78f, 0.29f, pulse);
                V2 e0 = M(placeFxVertical ? new V2(placeFxDoor.X, placeFxEdge0) : new V2(placeFxEdge0, placeFxDoor.Y));
                V2 e1 = M(placeFxVertical ? new V2(placeFxDoor.X, placeFxEdge1) : new V2(placeFxEdge1, placeFxDoor.Y));
                RoomMapRect(Mathf.Min(e0.X, e1.X) - 2f, Mathf.Min(e0.Y, e1.Y) - 2f, Mathf.Abs(e1.X - e0.X) + 4f, Mathf.Abs(e1.Y - e0.Y) + 4f, gold);
            }
            float spark = Mathf.Max(0f, 1f - after * 1.8f);
            if (spark > 0f)
            {
                V2 side = new V2(-placeFxDir.Y, placeFxDir.X);
                for (int i = 0; i < 14; i++)
                {
                    float f = (i / 13f - 0.5f) * 2.4f, reach = 0.6f + 0.4f * ((i * 37) % 7) / 7f;
                    V2 dir = (placeFxDir * Mathf.Cos(f) + side * Mathf.Sin(f));
                    V2 p = M(placeFxDoor + dir * (after * 300f * reach));
                    RoomMapRect(p.X - 1.5f, p.Y - 1.5f, 3f, 3f, new Rgba(1f, 0.86f, 0.47f, spark));
                }
            }
        }

        /// Where the new rooms (from `first` on) meet the old map: the longest wall they share, its middle, and the way out from the map.
        bool FindPlaceDoor(IList<ExpansionRoom> built, int first)
        {
            float best = 0f;
            for (int i = first; i < built.Count; i++)
            {
                var n = built[i];
                for (int a = 0; a <= first; a++)
                {
                    int ox0, oy0, ox1, oy1;
                    if (a == 0) { ox0 = 0; oy0 = 0; ox1 = (int)Game.Width; oy1 = (int)Game.Height; }
                    else { var o = built[a - 1]; ox0 = o.X; oy0 = o.Y; ox1 = o.X1; oy1 = o.Y1; }
                    bool v = n.X == ox1 || n.X1 == ox0, h = n.Y == oy1 || n.Y1 == oy0;
                    float run = v ? Mathf.Min(n.Y1, oy1) - Mathf.Max(n.Y, oy0) : h ? Mathf.Min(n.X1, ox1) - Mathf.Max(n.X, ox0) : 0f;
                    if (run <= best)
                        continue;
                    best = run;
                    placeFxVertical = v;
                    if (v)
                    {
                        float x = n.X == ox1 ? n.X : n.X1;
                        placeFxEdge0 = Mathf.Max(n.Y, oy0); placeFxEdge1 = Mathf.Min(n.Y1, oy1);
                        placeFxDoor = new V2(x, (placeFxEdge0 + placeFxEdge1) / 2f);
                        placeFxDir = new V2(n.X == ox1 ? 1f : -1f, 0f);
                    }
                    else
                    {
                        float y = n.Y == oy1 ? n.Y : n.Y1;
                        placeFxEdge0 = Mathf.Max(n.X, ox0); placeFxEdge1 = Mathf.Min(n.X1, ox1);
                        placeFxDoor = new V2((placeFxEdge0 + placeFxEdge1) / 2f, y);
                        placeFxDir = new V2(0f, n.Y == oy1 ? 1f : -1f);
                    }
                }
            }
            return best > 0f;
        }
    }
}
