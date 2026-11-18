using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Grid pieces on the build screen (Docs/GridPieces.md step 3): your hand of piece cards on the right, the picked piece following the mouse as a ghost
    /// on the door spots, R for the other turns and spots that fit there, a click to build it. Called from DrawExpansionDraw in a Grid run.
    public partial class ArenaApp
    {
        int expGridSlot = -1;                 // the hand slot picked on the build screen (-1: none)
        int expGridTurn;                      // which of the fits under the mouse (R steps through them)
        List<GridPieceOption> gridOpts;       // the picked piece's legal places (rebuilt when the map or the card changes)
        int gridOptsKey = int.MinValue;

        static readonly Rgba GridInk = new Rgba(0.35f, 0.85f, 0.78f), GridOk = new Rgba(0.35f, 1f, 0.55f);

        RoomCard PickedGridCard
        {
            get
            {
                var me = game.Me;
                if (me == null || expGridSlot < 0 || expGridSlot >= me.Hand.Length)
                    return RoomCard.None;
                var c = (RoomCard)me.Hand[expGridSlot];
                return Expansion.IsGridCard(c) ? c : RoomCard.None;
            }
        }

        List<GridPieceOption> GridOptionsNow()
        {
            var card = PickedGridCard;
            if (card == RoomCard.None)
                return gridOpts = new List<GridPieceOption>();
            var shape = Expansion.GridCardShape(card);
            int key = (int)shape * 7919 + game.Expanded.Count * 131;
            foreach (var r in game.Expanded)
                key = key * 31 + r.X * 7 + r.Y * 13 + r.W + r.H * 3;
            if (gridOpts == null || key != gridOptsKey)
            {
                gridOpts = Expansion.GridPieceOptions(game.Expanded, shape);
                gridOptsKey = key;
            }
            return gridOpts;
        }

        /// The places under the mouse (world point `wm`): every one whose room covers it, else the nearest within 5 tiles; nearest middle first.
        List<GridPieceOption> GridFitsAt(V2 wm)
        {
            var hits = new List<GridPieceOption>();
            foreach (var o in GridOptionsNow())
                foreach (var q in o.Rooms)
                    if (wm.X >= q.X && wm.X < q.X1 && wm.Y >= q.Y && wm.Y < q.Y1)
                    {
                        hits.Add(o);
                        break;
                    }
            if (hits.Count == 0)
            {
                float best = 500f;
                GridPieceOption? near = null;
                foreach (var o in GridOptionsNow())
                {
                    float d = new V2(o.SX, o.SY).DistanceTo(wm);
                    if (d < best) { best = d; near = o; }
                }
                if (near.HasValue)
                    hits.Add(near.Value);
            }
            hits.Sort((a, b) => a.Mid.DistanceTo(wm).CompareTo(b.Mid.DistanceTo(wm)));
            return hits;
        }

        /// Grid mode's mouse and keys over the map. Returns the place under the mouse (null if none); a click builds it.
        GridPieceOption? GridModeInput(Event ev, bool over, V2 wm, bool free, out int fits)
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
            if (PickedGridCard == RoomCard.None || !over)
                return null;
            var hits = GridFitsAt(wm);
            fits = hits.Count;
            if (hits.Count == 0)
                return null;
            if (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.R)
            {
                expGridTurn++;
                ev.Use();
            }
            var pick = hits[((expGridTurn % hits.Count) + hits.Count) % hits.Count];
            if (ev.type == EventType.MouseDown && ev.button == 0)
            {
                game.PlayGridCard(expGridSlot, pick.Orient, pick.X, pick.Y);
                expGridSlot = -1;
                expGridTurn = 0;
                ev.Use();
                return null;
            }
            return pick;
        }

        /// The map (inside the GL pass): a pulsing mark on every door spot the picked piece fits, the piece under the mouse lit cell by cell.
        void DrawGridGhost(System.Func<V2, V2> M, GridPieceOption? pick)
        {
            if (PickedGridCard == RoomCard.None)
                return;
            float pulse = 0.55f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f);
            var seen = new HashSet<long>();
            foreach (var o in GridOptionsNow())
            {
                if (!seen.Add(((long)o.SX << 32) ^ (uint)o.SY))
                    continue;
                V2 c = M(new V2(o.SX, o.SY));
                RoomMapRect(c.X - 4f, c.Y - 4f, 8f, 8f, GridInk.WithAlpha(pulse));
                RoomMapFrame(c.X - 6f, c.Y - 6f, 12f, 12f, 1f, GridInk.WithAlpha(0.5f));
            }
            if (!pick.HasValue)
                return;
            var cells = Expansion.GridPieceOrients(Expansion.GridCardShape(PickedGridCard))[pick.Value.Orient];
            float fill = 0.2f + 0.08f * Mathf.Sin(Time.unscaledTime * 4f);
            foreach (var cell in cells)  // each cell, so the piece reads as cells joined into one room
            {
                V2 p0 = M(new V2(pick.Value.X + cell[0] * Expansion.GridCell, pick.Value.Y + cell[1] * Expansion.GridCell));
                V2 p1 = M(new V2(pick.Value.X + (cell[0] + 1) * Expansion.GridCell, pick.Value.Y + (cell[1] + 1) * Expansion.GridCell));
                RoomMapRect(p0.X, p0.Y, p1.X - p0.X, p1.Y - p0.Y, GridOk.WithAlpha(fill));
                RoomMapFrame(p0.X + 2f, p0.Y + 2f, p1.X - p0.X - 4f, p1.Y - p0.Y - 4f, 1f, GridOk.WithAlpha(0.35f));
            }
            foreach (var q in pick.Value.Rooms)
            {
                V2 p0 = M(new V2(q.X, q.Y)), p1 = M(new V2(q.X1, q.Y1));
                RoomMapFrame(p0.X, p0.Y, p1.X - p0.X, p1.Y - p0.Y, 1.5f, GridOk.WithAlpha(0.9f));
            }
            V2 d = M(new V2(pick.Value.SX, pick.Value.SY));
            RoomMapRect(d.X - 5f, d.Y - 5f, 10f, 10f, GridOk);
        }

        /// The status line for grid mode.
        string GridRoomLine(GridPieceOption? pick, int fits, bool over, out Color ink)
        {
            ink = Ui.Muted;
            var me = game.Me;
            if (me == null || Game.HandCount(me) == 0)
                return "No pieces in your hand: one comes each round, and the Black Market and the Builder Shop sell them.";
            var card = PickedGridCard;
            if (card == RoomCard.None)
                return "Pick a piece from your hand.";
            if (GridOptionsNow().Count == 0)
            {
                ink = new Color(1f, 0.45f, 0.5f);
                return "No door spot fits a " + Expansion.CardName(card).ToLowerInvariant() + " right now.";
            }
            if (!pick.HasValue)
                return over ? "Move along a wall: the glowing marks are the door spots." : "Point at the map: the glowing marks are the door spots.";
            ink = Ui.Gold;
            return Expansion.CardName(card).ToUpperInvariant() + (fits > 1 ? "  ·  R  " + (((expGridTurn % fits) + fits) % fits + 1) + " OF " + fits + " FITS HERE" : "") + "  ·  click to build it";
        }

        /// The hand: two pieces a row, each drawn as its cells with its name; click one to pick it. Cards a Grid run can't play are dimmed.
        void DrawGridPalette(Rect area, bool calm)
        {
            var me = game.Me;
            int n = me != null ? Game.HandCount(me) : 0;
            Ui.Label(new Rect(area.x, area.y, area.width, 16f), "YOUR PIECES  ·  " + n + " / " + Expansion.HandMax, 10, Ui.Muted, TextAnchor.MiddleLeft, true);
            float gap = 6f, cw = (area.width - gap) / 2f, ch = 74f, y0 = area.y + 20f;
            for (int i = 0; i < n; i++)
            {
                var card = (RoomCard)me.Hand[i];
                bool playable = Expansion.IsGridCard(card);
                var r = new Rect(area.x + (i % 2) * (cw + gap), y0 + (i / 2) * (ch + gap), cw, ch);
                bool on = i == expGridSlot;
                if (CrtUi.SoftKey(r, "", null, 11, calm, lit: on) && playable)
                {
                    expGridSlot = on ? -1 : i;
                    expGridTurn = 0;
                }
                var ink = ToColor(CardColor(card));
                if (!playable)
                    ink = new Color(ink.r, ink.g, ink.b, 0.35f);
                DrawCardGlyph(card, new Rect(r.x + 8f, r.y + 6f, r.width - 16f, 38f), on ? Color.white : ink);
                bool floor = Expansion.IsPieceCard(card) && Expansion.CardFloor(card) != PathId.None;
                string name = Expansion.IsPieceCard(card) ? Expansion.GridPieceName(Expansion.CardPiece(card)) : Expansion.CardName(card);
                Ui.Label(new Rect(r.x, r.y + (floor ? 46f : 52f), r.width, 16f), name.ToUpperInvariant(), 11, on ? Color.white : ink, TextAnchor.MiddleCenter, true);
                if (floor)  // a floor piece: its ground under the name, in the floor's colour
                    Ui.Label(new Rect(r.x + 4f, r.y + 59f, r.width - 8f, 13f), Paths.Get(Expansion.CardFloor(card)).Name.ToUpperInvariant(), 9, on ? Color.white : ink, TextAnchor.MiddleCenter, true);
                if (TipHover(r))
                {
                    TipStart(r, Expansion.CardName(card), ink);
                    TipAdd(playable ? Expansion.CardBlurb(card) : "An old room card: it can't be played in a Grid run.", 12, Ui.Text);
                    if (playable)
                        TipAdd("Click it, point at a door spot, click to build. R turns it.", 11, Ui.Muted);
                }
            }
            float y = y0 + ((n + 1) / 2) * (ch + gap);
            Ui.Label(new Rect(area.x, y, area.width, 30f), "+1 piece each round. More from bosses, chests, the Black Market and the Builder Shop.", 10, Ui.Muted, TextAnchor.UpperLeft, true, true);
        }
    }
}
