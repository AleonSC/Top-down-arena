using UnityEngine;

namespace Arena
{
    /// The boss card pick in the upgrade picker (Docs/ArmsDealer.md step 3; BossCards.cs): five trophy cards in the picker's CRT look, each a
    /// room card (its room drawn as tiles, a service card with its icon) or a Floor kit (the floor kit icon in its path's colour), what it is
    /// for, and where it goes (your hand, the team's kits). One you can't take (a full hand, full kits) is dimmed. Preview: `bosscards`.
    public partial class ArenaApp
    {
        static readonly Color BossCardsTheme = new Color(0.45f, 0.85f, 1f);  // the cards' blue

        /// A code's colour: a card in its card colour (PieceCardsUi.CardColor), a kit in its path's accent.
        static Color BossCardColor(int code) => BossCards.IsKit(code) ? Paths.Get(BossCards.FloorOf(code)).Accent.ToColor() : CardColor(BossCards.CardOf(code)).ToColor();

        static Rgba BossCardRgba(int code) { var c = BossCardColor(code); return new Rgba(c.r, c.g, c.b); }

        static string BossCardChip(int code)
        {
            if (BossCards.IsKit(code)) return "FLOOR KIT";
            var c = BossCards.CardOf(code);
            return Expansion.IsServiceCard(c) ? "SERVICE ROOM" : c == PieceCard.BuilderShop ? "UTILITY ROOM" : "PIECE CARD";
        }

        /// What taking it changes: your hand, or the team's kits.
        System.Collections.Generic.List<string> BossCardParts(Player me, int code)
        {
            if (BossCards.IsKit(code))
                return new System.Collections.Generic.List<string> { "Team kits  " + game.RoomTokens.Count + " → " + (game.RoomTokens.Count + 1) + " / " + Expansion.MaxTokens };
            int held = Game.HandCount(me);
            return new System.Collections.Generic.List<string> { "Your hand  " + held + " → " + (held + 1) + " / " + Expansion.HandMax };
        }

        void DrawBossCardTrophy(Player me, int index, int code, Rect slot, bool selected, bool behind, bool hover, float now)
        {
            Color rc = BossCardColor(code);
            bool usable = game.BossCardUsable(me, code);
            float lift = selected ? -16f : behind ? 8f : hover ? -4f : 0f;
            float bob = behind ? 0f : Mathf.Sin(now * 1.85f + index * 2.1f) * 2.5f;
            var r = new Rect(slot.x, slot.y + lift + bob, slot.width, slot.height);
            DrawCrtCardFrame(r, rc, selected, hover, behind, now);

            string chip = BossCardChip(code);
            float cw = 22f + chip.Length * 7.2f;
            DrawCrtChip(new Rect(r.center.x - cw / 2f, r.y + 14f, cw, 20f), chip, rc, selected);
            var tile = new Rect(r.center.x - 52f, r.y + 42f, 104f, 104f);
            DrawCrtTile(tile, rc, selected);
            if (BossCards.IsKit(code))
                Ui.Icon(new Rect(tile.center.x - 38f, tile.center.y - 38f, 76f, 76f), PixelArt.Get("floorkit"), rc);
            else
                DrawCardGlyph(BossCards.CardOf(code), new Rect(tile.x + 12f, tile.y + 12f, tile.width - 24f, tile.height - 24f), Color.Lerp(rc, Color.white, selected ? 0.35f : 0.1f));

            string name = BossCards.IsKit(code) ? Paths.Get(BossCards.FloorOf(code)).Name : Expansion.CardName(BossCards.CardOf(code));
            Color nameLow = selected ? Color.Lerp(rc, Color.white, 0.25f) : new Color(0.78f, 0.83f, 0.94f);
            CrtUi.Title(new Rect(r.x + 8f, r.y + 148f, r.width - 16f, 36f), name, name.Length > 14 ? 25 : 30, Color.Lerp(nameLow, Color.white, 0.6f), TextAnchor.MiddleCenter, fxReduced);
            string desc = BossCards.Blurb(code);
            Ui.Label(new Rect(r.x + 14f, r.y + 188f, r.width - 28f, 90f), desc, desc.Length > 110 ? 12 : 13, selected ? Color.white : CrtStyle.Body, TextAnchor.UpperCenter, false, true);

            // Where it goes, or why it can't be taken.
            if (usable)
            {
                float oldA = Ui.Alpha;
                if (!selected)
                    Ui.Alpha *= 0.7f;
                DrawPickStrip(new Rect(r.x + 15.4f, r.yMax - 100f, r.width - 30.8f, 56f), 12, true, BossCardParts(me, code), -1, false);
                Ui.Alpha = oldA;
            }
            else
                Ui.Label(new Rect(r.x + 8f, r.yMax - 84f, r.width - 16f, 20f), BossCards.IsKit(code) ? "TEAM KITS FULL" : "YOUR HAND IS FULL", 13, new Color(1f, 0.45f, 0.5f), TextAnchor.MiddleCenter, true);

            string keyText = (index + 1).ToString();
            DrawKeyCap(new Rect(r.center.x - (12f + keyText.Length * 6.5f) / 2f, r.yMax - 40f, 0f, 18f), keyText, selected ? rc : new Color(1f, 1f, 1f, 0.6f));
            for (float dx = 0f; dx < r.width - 20f; dx += 8f)
                Ui.Fill(new Rect(r.x + 10f + dx, r.yMax - 12f, 5f, 2f), rc.WithAlpha(selected ? 0.95f : hover ? 0.8f : 0.5f));
            if (!usable)
                Ui.Fill(r, new Color(0.01f, 0.012f, 0.03f, 0.55f));
            else if (behind)
                Ui.Fill(r, new Color(0.01f, 0.012f, 0.03f, 0.32f));
        }
    }
}
