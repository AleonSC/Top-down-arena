using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Hand cards drawn anywhere (the build screen's palette, the inventory, the Black Market, the Builder Shop, the boss pick): a card's colour,
    /// its little drawing (a piece's cells, or a service card's one cell and mark), and the Black Market's card item.
    public partial class ArenaApp
    {
        /// A card's colour: a floor piece in its floor's colour, a plain piece in grid teal, the Builder Shop and the service cards in their rooms' colours.
        static Rgba CardColor(RoomCard c) => Expansion.IsPieceCard(c) ? (Expansion.CardFloor(c) != PathId.None ? KindInk(Arena.RoomKind.Floor, Expansion.CardFloor(c)) : GridInk) : c == RoomCard.BuilderShop ? BuilderInk : KindInk(Expansion.CardRoomKind(c), PathId.None);

        /// One of the Black Market's cards in the shop wheel (ShopUi.cs), in the vendor's teal: the room, what it is for, the hand it joins,
        /// and its price in vectors.
        void DrawRoomCardItem(Player me, int offer, Rect r, bool selected, float t, bool allowed)
        {
            var card = game.VendorCards[offer];
            int price = Expansion.CardPrice(card), have = game.SpendableVectors(me), held = Game.HandCount(me);
            bool full = held >= Expansion.HandMax, afford = have >= price && allowed && !full;
            Color rc = ToColor(VendorInk);
            float dim = afford || full ? 1f : 0.5f;
            Ui.Glow(r, rc, selected ? 0.5f + 0.1f * Mathf.Sin(t * 4f) : 0.14f * dim);
            DkBox(r, rc, selected ? 0.9f : 0.4f);
            Ui.Fill(r, new Color(0.01f, 0.07f, 0.07f, 0.55f));
            CrtKit.Frame(r, selected ? 2f : 1f, selected ? Color.Lerp(rc, Color.white, 0.5f) : new Color(rc.r, rc.g, rc.b, 0.45f));
            var ribbon = new Rect(r.center.x - 44f, r.y + 12f, 88f, 20f);
            Ui.Pill(ribbon, new Color(rc.r, rc.g, rc.b, 0.85f * dim));
            Ui.Label(ribbon, "Card", 10, new Color(0.06f, 0.06f, 0.1f), TextAnchor.MiddleCenter, true);
            var tile = new Rect(r.center.x - 40f, r.y + 42f, 80f, 80f);
            Ui.Pill(new Rect(tile.x - 3f, tile.y - 3f, tile.width + 6f, tile.height + 6f), new Color(rc.r, rc.g, rc.b, selected ? 0.35f : 0.18f));
            Ui.Pill(tile, new Color(rc.r * 0.22f, rc.g * 0.22f, rc.b * 0.22f, 1f));
            var gi = card == RoomCard.BuilderShop || Expansion.IsServiceCard(card) ? ToColor(CardColor(card)) : rc;
            DrawCardGlyph(card, new Rect(tile.x + 8f, tile.y + 8f + (selected ? 2f * Mathf.Sin(t * 3f) : 0f), tile.width - 16f, tile.height - 16f), new Color(gi.r, gi.g, gi.b, dim));
            Ui.Label(new Rect(r.x + 8f, r.y + 130f, r.width - 16f, 26f), Expansion.CardName(card), 18, new Color(1f, 1f, 1f, dim), TextAnchor.MiddleCenter, true);
            Ui.Fill(new Rect(r.x + 36f, r.y + 160f, r.width - 72f, 1f), new Color(rc.r, rc.g, rc.b, 0.4f));
            Ui.Label(new Rect(r.x + 14f, r.y + 166f, r.width - 28f, selected ? 78f : 130f), full ? "Your hand is full. Play some cards first." : Expansion.CardBlurb(card), 13, new Color(Ui.Text.r, Ui.Text.g, Ui.Text.b, dim), TextAnchor.UpperCenter, false, true);
            if (selected && !full)
                DrawPickStrip(new Rect(r.x + 10f, r.yMax - 50f, r.width - 20f, 0f), 13, true, new List<string> { "Hand  " + held + " → " + (held + 1) }, -1, false);
            var tag = selected ? new Rect(r.center.x - 70f, r.yMax - 38f, 140f, 28f) : new Rect(r.center.x - 50f, r.yMax - 38f, 100f, 26f);
            Ui.Pill(tag, new Color(0.02f, 0.1f, 0.14f, 0.92f));
            CrtKit.Frame(tag, 1f, new Color(0.45f, 0.85f, 1f, have >= price ? 0.8f : 0.35f));
            Ui.Label(tag, (selected && afford ? "Buy · " : "") + price + " VECTORS", 13, have >= price ? new Color(0.55f, 0.9f, 1f) : new Color(1f, 0.45f, 0.5f), TextAnchor.MiddleCenter, true);
        }

        /// A card's room as tiles, fitted and centred in `r`.
        static void DrawCardGlyph(RoomCard card, Rect r, Color ink)
        {
            if (card == RoomCard.None)
                return;
            if (Expansion.IsPieceCard(card))  // a grid piece: its cells (Docs/GridPieces.md)
            {
                var cells = Expansion.GridPieceOrients(Expansion.CardPiece(card))[0];
                var size = Expansion.GridPieceSize(cells);
                float u = Mathf.Min(r.width / Mathf.Max(2, size[0]), r.height / Mathf.Max(2, size[1]));
                float gx = r.center.x - size[0] * u / 2f, gy = r.center.y - size[1] * u / 2f;
                foreach (var c in cells)
                    Ui.Fill(new Rect(gx + c[0] * u + 1f, gy + c[1] * u + 1f, u - 2f, u - 2f), new Color(ink.r, ink.g, ink.b, 0.6f));
                return;
            }
            float side = Mathf.Min(r.width, r.height) * 0.6f;  // a service or utility card: one cell with its job's mark
            var cell = new Rect(r.center.x - side / 2f, r.center.y - side / 2f, side, side);
            Ui.Fill(new Rect(cell.x + 1f, cell.y + 1f, cell.width - 2f, cell.height - 2f), new Color(ink.r, ink.g, ink.b, 0.55f));
            if (Expansion.IsServiceCard(card))
                DrawServiceIcon(card, cell, ink);
        }

        /// A service card's job over its cell: a coin (shop), a plus (Black Market) or a crosshair (Arms Dealer), as on the map.
        static void DrawServiceIcon(RoomCard card, Rect room, Color ink)
        {
            float u = Mathf.Max(1f, Mathf.Round(room.width / 14f));  // stroke
            var c = room.center;
            float h = room.width * 0.32f;
            var dark = new Color(0.02f, 0.04f, 0.06f, 0.85f);
            var col = new Color(Mathf.Lerp(ink.r, 1f, 0.55f), Mathf.Lerp(ink.g, 1f, 0.55f), Mathf.Lerp(ink.b, 1f, 0.55f), ink.a);
            switch (card)
            {
                case RoomCard.Shop:
                    Ui.Dot(new Rect(c.x - h, c.y - h, 2f * h, 2f * h), dark);
                    Ui.Dot(new Rect(c.x - h + u, c.y - h + u, 2f * (h - u), 2f * (h - u)), col, true);
                    Ui.Fill(new Rect(c.x - u / 2f, c.y - h * 0.5f, u, h), col);
                    break;
                case RoomCard.Market:
                    Ui.Fill(new Rect(c.x - h - u, c.y - u * 1.5f, 2f * (h + u), 3f * u), dark);
                    Ui.Fill(new Rect(c.x - u * 1.5f, c.y - h - u, 3f * u, 2f * (h + u)), dark);
                    Ui.Fill(new Rect(c.x - h, c.y - u / 2f, 2f * h, u), col);
                    Ui.Fill(new Rect(c.x - u / 2f, c.y - h, u, 2f * h), col);
                    break;
                default:  // Arms Dealer: a crosshair
                    Ui.Dot(new Rect(c.x - h, c.y - h, 2f * h, 2f * h), dark);
                    Ui.Dot(new Rect(c.x - h * 0.6f, c.y - h * 0.6f, 1.2f * h, 1.2f * h), col, true);
                    Ui.Fill(new Rect(c.x - h, c.y - u / 2f, h * 0.45f, u), col);
                    Ui.Fill(new Rect(c.x + h * 0.55f, c.y - u / 2f, h * 0.45f, u), col);
                    Ui.Fill(new Rect(c.x - u / 2f, c.y - h, u, h * 0.45f), col);
                    Ui.Fill(new Rect(c.x - u / 2f, c.y + h * 0.55f, u, h * 0.45f), col);
                    break;
            }
        }
    }
}
