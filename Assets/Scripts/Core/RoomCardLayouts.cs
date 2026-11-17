using System;
using System.Collections.Generic;

namespace Arena
{
    /// The obstacles built into each room card's room (Docs/RoomCards.md, "Inside the rooms"): a designed layout per card in place of the
    /// random cover and side walls a drawn room gets. Each layout is written for the card's shape in its base turn (SurpriseBase, 100 px tiles,
    /// origin at the mask's top-left), then mirrored and turned to match how the room was placed. Anything near a door to another room is left out.
    public static partial class Expansion
    {
        /// One obstacle in a layout: a box (x0, y0, x1, y1) or, with Pillar, a round pillar at (x0, y0) of radius x1. Base-orientation px.
        struct Piece
        {
            public bool Pillar;
            public int X0, Y0, X1, Y1;
        }

        static Piece B(int x0, int y0, int x1, int y1) => new Piece { X0 = x0, Y0 = y0, X1 = x1, Y1 = y1 };
        static Piece P(int x, int y, int r) => new Piece { Pillar = true, X0 = x, Y0 = y, X1 = r };

        /// Each card's layout in its base turn. Lanes are 300 px wide: what stands in one hugs a wall and leaves at least 220 px to pass.
        static Piece[] CardLayout(RoomCard c)
        {
            switch (c)
            {
                case RoomCard.Box:  // 7 x 6: four pillars round a low wall in the middle
                    return new[] { P(190, 170, 32), P(510, 170, 32), P(190, 430, 32), P(510, 430, 32), B(285, 282, 415, 318) };
                case RoomCard.Hall:  // 8 x 3: blocks off alternate walls near each end, a zig-zag to run through (the middle stays clear for a door)
                    return new[] { B(140, 0, 210, 80), B(590, 220, 660, 300) };
                case RoomCard.Ring:  // 11 x 11 round a 5 x 5 block: a buttress mid-way along each outer wall, a pillar in each corner square
                    return new[] { B(490, 0, 610, 70), B(1030, 490, 1100, 610), B(490, 1030, 610, 1100), B(0, 490, 70, 610),
                                   P(150, 150, 28), P(950, 150, 28), P(150, 950, 28), P(950, 950, 28) };
                case RoomCard.Cross:  // 15 x 15, arms 3 wide: a pillar in the crossing, a block in each arm turning like a pinwheel
                    return new[] { P(750, 750, 40), B(600, 270, 670, 340), B(1160, 600, 1230, 670), B(830, 1160, 900, 1230), B(270, 830, 340, 900) };
                case RoomCard.LHall:  // a 12-tile leg along the top, 9 down the left: a pillar in the bend, blocks off alternate walls
                    return new[] { P(150, 150, 30), B(620, 230, 700, 300), B(940, 0, 1020, 70), B(230, 560, 300, 640) };
                case RoomCard.THall:  // a 15-tile bar, a 9-tile stem: a pillar facing the stem, blocks in each arm and the stem
                    return new[] { P(750, 110, 34), B(280, 230, 360, 300), B(1140, 230, 1220, 300), B(600, 600, 670, 680) };
                case RoomCard.Shop:
                case RoomCard.Market:
                case RoomCard.Arms:  // 4 x 4 service rooms: kept clear for the stall
                    return new Piece[0];
                case RoomCard.MiniBoss:  // 8 x 6 duel room: four pillars to fight round
                    return new[] { P(200, 160, 34), P(600, 160, 34), P(200, 440, 34), P(600, 440, 34) };
                case RoomCard.BuilderShop:  // 5 x 4 utility room: a pillar in each corner, the counter in the middle (drawn, not solid)
                    return new[] { P(90, 90, 24), P(410, 90, 24), P(90, 310, 24), P(410, 310, 24) };
                default:  // Chamfer, 9 x 7 with notched corners: a pillar in the middle, a low wall above and below it
                    return new[] { P(450, 350, 44), B(320, 180, 580, 220), B(320, 480, 580, 520) };
            }
        }

        /// The card room's obstacles, for the group of boxes whose areas are `members` (1-based areas; rooms[a - 1] its box). Its turn and mirror
        /// are found by matching the card's mask against the boxes (the first match in a fixed order, so every machine gets the same).
        static void AddCardFeatures(List<Solid> cover, IList<ExpansionRoom> rooms, List<int> members, List<RoomDoor> doors)
        {
            var card = (RoomCard)rooms[members[0] - 1].Card;
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
            foreach (int a in members)
            {
                var r = rooms[a - 1];
                x0 = Math.Min(x0, r.X); y0 = Math.Min(y0, r.Y); x1 = Math.Max(x1, r.X1); y1 = Math.Max(y1, r.Y1);
            }
            int cols = (x1 - x0) / Step, rows = (y1 - y0) / Step;
            var floor = new bool[cols, rows];
            foreach (int a in members)
            {
                var r = rooms[a - 1];
                for (int c = (r.X - x0) / Step; c < (r.X1 - x0) / Step; c++)
                    for (int k = (r.Y - y0) / Step; k < (r.Y1 - y0) / Step; k++)
                        floor[c, k] = true;
            }
            var shape = CardShape(card);
            var b = SurpriseBase(shape);
            int bw = b.GetLength(0) * Step, bh = b.GetLength(1) * Step;
            // The doors out of this room (not the seams between its own boxes).
            var exits = new List<RoomDoor>();
            foreach (var d in doors)
                if (members.Contains(d.A) != members.Contains(d.B))
                    exits.Add(d);
            // Every turn and mirror that matches the room (a symmetric shape matches several): the one that keeps the most pieces clear of
            // the doors wins, the first in order on a tie, so every machine picks the same.
            List<Solid> best = null;
            for (int t = 0; t < 4; t++)
                for (int f = 0; f < 2; f++)
                {
                    var m = SurpriseMask(shape, t, f == 1);
                    if (m.GetLength(0) != cols || m.GetLength(1) != rows)
                        continue;
                    bool same = true;
                    for (int c = 0; c < cols && same; c++)
                        for (int k = 0; k < rows && same; k++)
                            same = m[c, k] == floor[c, k];
                    if (!same)
                        continue;
                    var kept = LayoutAt(card, x0, y0, bw, bh, t, f == 1, exits);
                    if (best == null || kept.Count > best.Count)
                        best = kept;
                }
            if (best != null)  // (null: not the card's shape, an old save or a map reset cut it; it stays bare)
                cover.AddRange(best);
        }

        /// The card's layout turned and mirrored, at the room's corner (x0, y0), without the pieces in a door's mouth.
        static List<Solid> LayoutAt(RoomCard card, int x0, int y0, int bw, int bh, int turn, bool mirror, List<RoomDoor> exits)
        {
            var kept = new List<Solid>();
            foreach (var pc in CardLayout(card))
            {
                Solid s;
                if (pc.Pillar)
                {
                    V2 c = Orient(pc.X0, pc.Y0, bw, bh, turn, mirror);
                    s = World.Pillar(x0 + c.X, y0 + c.Y, pc.X1);
                }
                else
                {
                    V2 a = Orient(pc.X0, pc.Y0, bw, bh, turn, mirror), z = Orient(pc.X1, pc.Y1, bw, bh, turn, mirror);
                    s = World.Box(x0 + Math.Min(a.X, z.X), y0 + Math.Min(a.Y, z.Y), x0 + Math.Max(a.X, z.X), y0 + Math.Max(a.Y, z.Y));
                }
                float hw = s.Round ? s.R : s.HW, hh = s.Round ? s.R : s.HH;
                bool clear = true;
                foreach (var d in exits)  // keep a door's mouth clear: its width plus a little either side, a step in from it
                {
                    float reachAlong = d.W / 2f + 60f, reachIn = DoorMouth;
                    float dx = Math.Abs(s.Pos.X - d.Pos.X), dy = Math.Abs(s.Pos.Y - d.Pos.Y);
                    float along = d.Vertical ? dy : dx, across = d.Vertical ? dx : dy, ha = d.Vertical ? hh : hw, hc = d.Vertical ? hw : hh;
                    if (along < reachAlong + ha && across < reachIn + hc)
                        clear = false;
                }
                if (clear)
                    kept.Add(s);
            }
            return kept;
        }

        const float DoorMouth = 110f;  // a card room's obstacles stay this far in from a door

        /// A base-orientation point after the mask's mirror (left to right) and `turn` quarter turns clockwise (as SurpriseMask does), in a
        /// bw x bh footprint.
        static V2 Orient(float x, float y, int bw, int bh, int turn, bool mirror)
        {
            if (mirror)
                x = bw - x;
            float w = bw, h = bh;
            for (int t = 0; t < (turn & 3); t++)
            {
                float nx = h - y, ny = x;
                x = nx; y = ny;
                float tmp = w; w = h; h = tmp;
            }
            return new V2(x, y);
        }
    }
}
