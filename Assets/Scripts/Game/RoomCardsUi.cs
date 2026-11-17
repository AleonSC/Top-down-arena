using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Room cards on the build screen (RoomCards.cs, Docs/RoomCards.md): the hand panel, the card's room following the mouse as a
    /// ghost snapped to the nearest legal spot, R for the other turns that fit there, a click to build it. Called from DrawExpansionDraw.
    public partial class ArenaApp
    {
        int expCard;  // the hand slot picked on the build screen (-1: none)
        int expCardTurn;  // which of the fits under the mouse (R steps through them)
        List<SurpriseOption> cardOpts;  // the picked card's legal spots, best first (rebuilt when the map or the card changes)
        int cardOptsKey = int.MinValue;

        static readonly Rgba CardInk = new Rgba(0.45f, 0.85f, 1f), CardOk = new Rgba(0.35f, 1f, 0.55f);

        RoomCard PickedCard => game.Me != null && expCard >= 0 && expCard < game.Me.Hand.Length ? (RoomCard)game.Me.Hand[expCard] : RoomCard.None;

        /// The picked card's legal spots (cached by the map and the card).
        List<SurpriseOption> CardOptionsNow()
        {
            var card = PickedCard;
            int key = (int)card * 7919 + game.Expanded.Count * 131;
            foreach (var r in game.Expanded)
                key = key * 31 + r.X * 7 + r.Y * 13 + r.W + r.H * 3;
            if (cardOpts == null || key != cardOptsKey)
            {
                cardOpts = Expansion.CardOptions(game.Expanded, card);
                cardOptsKey = key;
            }
            return cardOpts;
        }

        /// The fits under the mouse (world point `wm`): every spot whose room covers it, else the nearest one within 6 tiles. Best first.
        List<SurpriseOption> CardFitsAt(V2 wm)
        {
            var all = CardOptionsNow();
            var hits = new List<SurpriseOption>();
            foreach (var o in all)
                foreach (var q in o.Rooms)
                    if (wm.X >= q.X && wm.X < q.X1 && wm.Y >= q.Y && wm.Y < q.Y1)
                    {
                        hits.Add(o);
                        break;
                    }
            if (hits.Count == 0)
            {
                float best = 600f;
                SurpriseOption? near = null;
                foreach (var o in all)
                {
                    float d = Expansion.OptionMid(o).DistanceTo(wm);
                    if (d < best) { best = d; near = o; }
                }
                if (near.HasValue)
                    hits.Add(near.Value);
            }
            // The best spots first, and among equals the one centred nearest the mouse.
            hits.Sort((a, b) => a.Score != b.Score ? b.Score.CompareTo(a.Score) : Expansion.OptionMid(a).DistanceTo(wm).CompareTo(Expansion.OptionMid(b).DistanceTo(wm)));
            return hits;
        }

        /// Cards mode's mouse and keys over the map. Returns the fit the mouse is on (null if none); a click builds it.
        SurpriseOption? CardModeInput(Event ev, bool over, V2 wm, bool free, out int fits)
        {
            fits = 0;
            var me = game.Me;
            if (me != null && (expCard >= Game.HandCount(me) || expCard < -1))
                expCard = -1;
            if ((ev.type == EventType.MouseDown && ev.button == 1) || (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.Escape))
            {
                if (expCard >= 0)
                    expCard = -1;
                else if (free)
                    expOpen = false;
                ev.Use();
                return null;
            }
            if (PickedCard == RoomCard.None || !over)
                return null;
            var hits = CardFitsAt(wm);
            fits = hits.Count;
            if (hits.Count == 0)
                return null;
            if (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.R)
            {
                expCardTurn++;
                ev.Use();
            }
            var pick = hits[((expCardTurn % hits.Count) + hits.Count) % hits.Count];
            if (ev.type == EventType.MouseDown && ev.button == 0)
            {
                game.PlayRoomCard(expCard, pick.X, pick.Y, pick.Turn, pick.Mirror);
                expCard = -1;
                expCardTurn = 0;
                ev.Use();
                return null;
            }
            return pick;
        }

        /// The ghost on the map (inside the GL pass): faint dots where the card fits anywhere, the room under the mouse lit.
        void DrawCardGhost(System.Func<V2, V2> M, SurpriseOption? pick)
        {
            if (PickedCard == RoomCard.None)
                return;
            var ink = PickedCard == RoomCard.MiniBoss ? GateInk : CardInk;
            foreach (var o in CardOptionsNow())
            {
                V2 c = M(Expansion.OptionMid(o));
                RoomMapRect(c.X - 1.5f, c.Y - 1.5f, 3f, 3f, ink.WithAlpha(0.35f));
            }
            if (!pick.HasValue)
                return;
            float pulse = 0.22f + 0.08f * Mathf.Sin(Time.unscaledTime * 4f);
            foreach (var q in pick.Value.Rooms)
            {
                V2 p0 = M(new V2(q.X, q.Y)), p1 = M(new V2(q.X1, q.Y1));
                RoomMapRect(p0.X, p0.Y, p1.X - p0.X, p1.Y - p0.Y, CardOk.WithAlpha(pulse));
            }
            foreach (var q in pick.Value.Rooms)
            {
                V2 p0 = M(new V2(q.X, q.Y)), p1 = M(new V2(q.X1, q.Y1));
                RoomMapFrame(p0.X, p0.Y, p1.X - p0.X, p1.Y - p0.Y, 1.5f, CardOk.WithAlpha(0.9f));
            }
        }

        /// The status line for cards mode.
        string CardLine(SurpriseOption? pick, int fits, bool over, out Color ink)
        {
            ink = Ui.Muted;
            var me = game.Me;
            if (me == null || Game.HandCount(me) == 0)
                return "Your hand is empty: a card comes each round, and the Black Market sells them.";
            var card = PickedCard;
            if (card == RoomCard.None)
                return "Pick a card from your hand.";
            if (CardOptionsNow().Count == 0)
            {
                ink = new Color(1f, 0.45f, 0.5f);
                return "No spot on the map fits a " + Expansion.CardName(card).ToLowerInvariant() + ".";
            }
            if (!pick.HasValue)
                return over ? "Move along a room's wall: the dots are where it fits." : "Point at the map: the dots are where it fits.";
            ink = Ui.Gold;
            return Expansion.CardName(card).ToUpperInvariant() + (pick.Value.Loops > 0 ? "  ·  CLOSES " + (pick.Value.Loops == 1 ? "A LOOP" : pick.Value.Loops + " LOOPS") : "")
                + (fits > 1 ? "  ·  R  " + (((expCardTurn % fits) + fits) % fits + 1) + " OF " + fits + " FITS HERE" : "") + "  ·  click to build it";
        }

        /// The hand: two cards a row, each a little drawing of its room and its name; click one to pick it.
        void DrawCardHand(Rect area, bool calm)
        {
            var me = game.Me;
            int n = me != null ? Game.HandCount(me) : 0;
            Ui.Label(new Rect(area.x, area.y, area.width, 16f), "YOUR HAND  ·  " + n + " / " + Expansion.HandMax, 10, Ui.Muted, TextAnchor.MiddleLeft, true);
            float gap = 6f, cw = (area.width - gap) / 2f, ch = 70f, y0 = area.y + 20f;
            for (int i = 0; i < n; i++)
            {
                var card = (RoomCard)me.Hand[i];
                var r = new Rect(area.x + (i % 2) * (cw + gap), y0 + (i / 2) * (ch + gap), cw, ch);
                bool on = i == expCard;
                if (CrtUi.SoftKey(r, "", null, 11, calm, lit: on))
                {
                    expCard = on ? -1 : i;
                    expCardTurn = 0;
                }
                var ink = ToColor(card == RoomCard.MiniBoss ? GateInk : CardInk);
                DrawCardGlyph(card, new Rect(r.x + 8f, r.y + 6f, r.width - 16f, 40f), on ? Color.white : ink);
                Ui.Label(new Rect(r.x, r.y + 48f, r.width, 16f), Expansion.CardName(card).ToUpperInvariant(), 11, on ? Color.white : ink, TextAnchor.MiddleCenter, true);
                if (TipHover(r))
                {
                    TipStart(r, Expansion.CardName(card), ink);
                    TipAdd(Expansion.CardBlurb(card), 12, Ui.Text);
                    TipAdd("Click it, point at the map, click to build. R turns it.", 11, Ui.Muted);
                }
            }
            float y = y0 + ((n + 1) / 2) * (ch + gap);
            Ui.Label(new Rect(area.x, y, area.width, 30f), "+1 card each round. More from bosses, chests and the Black Market.", 10, Ui.Muted, TextAnchor.UpperLeft, true, true);
        }

        /// One of the Black Market's room cards in the shop wheel (ShopUi.cs), in the vendor's teal: the room, what it is for, the hand it joins,
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
            Ui.Label(ribbon, "Room card", 10, new Color(0.06f, 0.06f, 0.1f), TextAnchor.MiddleCenter, true);
            var tile = new Rect(r.center.x - 40f, r.y + 42f, 80f, 80f);
            Ui.Pill(new Rect(tile.x - 3f, tile.y - 3f, tile.width + 6f, tile.height + 6f), new Color(rc.r, rc.g, rc.b, selected ? 0.35f : 0.18f));
            Ui.Pill(tile, new Color(rc.r * 0.22f, rc.g * 0.22f, rc.b * 0.22f, 1f));
            var gi = card == RoomCard.MiniBoss ? ToColor(GateInk) : rc;
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
            var m = Expansion.SurpriseMask(Expansion.CardShape(card), 0, false);
            int cols = m.GetLength(0), rows = m.GetLength(1);
            float s = Mathf.Min(r.width / cols, r.height / rows);
            float x0 = r.center.x - cols * s / 2f, y0 = r.center.y - rows * s / 2f;
            for (int c = 0; c < cols; c++)
                for (int k = 0; k < rows; k++)
                    if (m[c, k])
                        Ui.Fill(new Rect(x0 + c * s, y0 + k * s, s - (s > 3f ? 0.6f : 0f), s - (s > 3f ? 0.6f : 0f)), new Color(ink.r, ink.g, ink.b, 0.55f));
        }
    }
}
