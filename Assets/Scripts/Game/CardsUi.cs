using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Hand cards drawn anywhere (the build screen's palette, the inventory, the Builder Shop, the boss pick): a card's colour and
    /// its little drawing (a piece's cells, or a service card's one cell and mark).
    public partial class ArenaApp
    {
        /// A card's colour: a floor piece in its floor's colour, a plain piece in grid teal, the Builder Shop and the service cards in their rooms' colours.
        static Rgba CardColor(PieceCard c) => Expansion.IsShapePieceCard(c) ? (Expansion.PieceCardFloor(c) != PathId.None ? KindInk(Arena.RoomKind.Floor, Expansion.PieceCardFloor(c)) : GridInk) : c == PieceCard.BuilderShop ? BuilderInk : KindInk(Expansion.CardRoomKind(c), PathId.None);

        /// A card's room as tiles, fitted and centred in `r`.
        static void DrawCardGlyph(PieceCard card, Rect r, Color ink)
        {
            if (card == PieceCard.None)
                return;
            if (Expansion.IsShapePieceCard(card))  // a piece: its cells (Docs/GridPieces.md)
            {
                var cells = Expansion.PieceShapeOrients(Expansion.ShapeOfPieceCard(card))[0];
                var size = Expansion.PieceShapeSize(cells);
                float u = Mathf.Min(r.width / Mathf.Max(2, size[0]), r.height / Mathf.Max(2, size[1]));
                float gx = r.center.x - size[0] * u / 2f, gy = r.center.y - size[1] * u / 2f;
                foreach (var c in cells)
                    Ui.Fill(new Rect(gx + c[0] * u + 1f, gy + c[1] * u + 1f, u - 2f, u - 2f), ink.WithAlpha(0.6f));
                return;
            }
            float side = Mathf.Min(r.width, r.height) * 0.6f;  // a service or utility card: one cell with its job's mark
            var cell = new Rect(r.center.x - side / 2f, r.center.y - side / 2f, side, side);
            Ui.Fill(new Rect(cell.x + 1f, cell.y + 1f, cell.width - 2f, cell.height - 2f), ink.WithAlpha(0.55f));
            if (Expansion.IsServiceCard(card))
                DrawServiceIcon(card, cell, ink);
        }

        /// A service card's job over its cell: a coin (shop), a plus (Black Market), a crosshair (Arms Dealer) or a pill (Medic), as on the map.
        static void DrawServiceIcon(PieceCard card, Rect room, Color ink)
        {
            float u = Mathf.Max(1f, Mathf.Round(room.width / 14f));  // stroke
            var c = room.center;
            float h = room.width * 0.32f;
            var dark = new Color(0.02f, 0.04f, 0.06f, 0.85f);
            var col = new Color(Mathf.Lerp(ink.r, 1f, 0.55f), Mathf.Lerp(ink.g, 1f, 0.55f), Mathf.Lerp(ink.b, 1f, 0.55f), ink.a);
            switch (card)
            {
                case PieceCard.Shop:
                    Ui.Dot(new Rect(c.x - h, c.y - h, 2f * h, 2f * h), dark);
                    Ui.Dot(new Rect(c.x - h + u, c.y - h + u, 2f * (h - u), 2f * (h - u)), col, true);
                    Ui.Fill(new Rect(c.x - u / 2f, c.y - h * 0.5f, u, h), col);
                    break;
                case PieceCard.Market:
                    Ui.Fill(new Rect(c.x - h - u, c.y - u * 1.5f, 2f * (h + u), 3f * u), dark);
                    Ui.Fill(new Rect(c.x - u * 1.5f, c.y - h - u, 3f * u, 2f * (h + u)), dark);
                    Ui.Fill(new Rect(c.x - h, c.y - u / 2f, 2f * h, u), col);
                    Ui.Fill(new Rect(c.x - u / 2f, c.y - h, u, 2f * h), col);
                    break;
                case PieceCard.Medic:  // a capsule pill, its left half filled
                {
                    float pw = h * 2f, ph = h * 0.9f;
                    var pill = new Rect(c.x - pw / 2f, c.y - ph / 2f, pw, ph);
                    Ui.Pill(new Rect(pill.x - u, pill.y - u, pill.width + 2f * u, pill.height + 2f * u), dark);
                    Ui.Pill(pill, col);
                    Ui.Pill(new Rect(pill.center.x, pill.y + u, pill.width / 2f - u, pill.height - 2f * u), dark);
                    Ui.Fill(new Rect(pill.center.x - u / 2f, pill.y, u, pill.height), dark);
                    break;
                }
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
