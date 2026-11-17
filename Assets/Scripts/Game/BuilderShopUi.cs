using UnityEngine;

namespace Arena
{
    /// The Builder Shop on screen (Core/BuilderShop.cs): while you stand in a Builder room in a build round, its shelf sits at the bottom of the
    /// screen (the arena stays live round it): five room cards, each a drawing of its room, its name and a Buy key with the price in vectors
    /// (the vendor's price struck through). The counter itself stands in the room (`DrawBuilderCounter`). Preview:
    /// `EXPANDMAP=0 CARDROOM=buildershop BUILDEROPEN=1 [BUILDERVEC=n] Tools/Preview/shot.sh x hud`.
    public partial class ArenaApp
    {
        static readonly Rgba BuilderInk = new Rgba(1f, 0.72f, 0.3f);
        const float BuilderPanelW = 700f, BuilderPanelH = 184f;

        Rect BuilderPanelRect => new Rect(W / 2f - BuilderPanelW / 2f, H - BuilderPanelH - 118f, BuilderPanelW, BuilderPanelH);  // (above the XP bar and hotbar)

        bool BuilderPanelUp(Player me) => me != null && game.BuilderShopOpen(me) && !ShopScreenUp(me) && !ExpandWindowUp;

        /// The pointer is over the Builder Shop's shelf: a click there buys, it doesn't fire (ArenaApp.cs).
        bool MouseOnBuilderShop(Player me)
        {
            if (!BuilderPanelUp(me))
                return false;
            Vector3 m = Input.mousePosition;
            return BuilderPanelRect.Contains(new Vector2((m.x - offX) / scale, (UnityEngine.Screen.height - m.y - offY) / scale));
        }

        void DrawBuilderShop(Player me, float t)
        {
            if (!BuilderPanelUp(me))
                return;
            var r = BuilderPanelRect;
            var ink = ToColor(BuilderInk);
            Ui.Fill(r, new Color(0.01f, 0.015f, 0.04f, 0.88f));
            DkPanel(r);
            Ui.Fill(new Rect(r.x, r.y, r.width, 2f), new Color(ink.r, ink.g, ink.b, 0.8f));  // an amber top edge
            DkTitle(new Rect(r.x + 18f, r.y + 10f, 260f, 26f), "BUILDER SHOP", 20, ink);
            Ui.Label(new Rect(r.x + 18f, r.y + 34f, 380f, 16f), "Room cards for vectors  ·  a new shelf each wave  ·  a quarter off", 11, Ui.Muted, TextAnchor.MiddleLeft, true);
            int held = Game.HandCount(me), have = game.SpendableVectors(me);
            Ui.Label(new Rect(r.xMax - 260f, r.y + 12f, 242f, 20f), have + " VECTORS", 15, new Color(0.55f, 0.9f, 1f), TextAnchor.MiddleRight, true);
            Ui.Label(new Rect(r.xMax - 260f, r.y + 34f, 242f, 16f), "HAND  " + held + " / " + Expansion.HandMax, 11, held >= Expansion.HandMax ? new Color(1f, 0.45f, 0.5f) : Ui.Muted, TextAnchor.MiddleRight, true);

            var offers = game.BuilderCards;
            const float tw = 124f, gap = 9f, th = 116f;
            float x0 = r.center.x - (offers.Length * tw + (offers.Length - 1) * gap) / 2f, y0 = r.y + 56f;
            for (int i = 0; i < offers.Length; i++)
            {
                var card = offers[i];
                var tile = new Rect(x0 + i * (tw + gap), y0, tw, th);
                string why = game.BuilderRefusal(me, i);
                bool hover = tile.Contains(Event.current.mousePosition);
                var ci = ToColor(CardInkOf(card));
                if (hover && why == null)
                    Ui.Glow(tile, ink, 0.35f + 0.08f * Mathf.Sin(t * 4f));
                DkSub(tile, new Color(ci.r * 0.12f, ci.g * 0.12f, ci.b * 0.12f, 0.75f));
                float dim = why == null ? 1f : 0.55f;
                float bob = hover && !fxReduced ? 2f * Mathf.Sin(t * 3f) : 0f;
                DrawCardGlyph(card, new Rect(tile.x + 22f, tile.y + 8f + bob, tile.width - 44f, 34f), new Color(ci.r, ci.g, ci.b, dim));
                Ui.Label(new Rect(tile.x, tile.y + 44f, tile.width, 16f), Expansion.CardName(card).ToUpperInvariant(), 12, new Color(1f, 1f, 1f, dim), TextAnchor.MiddleCenter, true);
                int price = Expansion.BuilderPrice(card), full = Expansion.CardPrice(card);
                string was = full.ToString();
                float wasW = Ui.TextWidth(was, 10, true), cx = tile.center.x;
                var wasR = new Rect(cx - wasW / 2f, tile.y + 60f, wasW, 12f);
                Ui.Label(wasR, was, 10, Ui.Muted, TextAnchor.MiddleCenter, true);
                Ui.Fill(new Rect(wasR.x - 2f, wasR.center.y, wasR.width + 4f, 1f), Ui.Muted);  // (struck through: the vendor's price)
                bool old = GUI.enabled;
                GUI.enabled = why == null;
                if (DkKey(new Rect(tile.x + 10f, tile.yMax - 34f, tile.width - 20f, 26f), why == null ? "BUY  " + price + " V" : why == "Hand full" ? "HAND FULL" : price + " V  ·  NEED " + (price - have), ink, false, 12))
                    game.BuyBuilderCard(i);
                GUI.enabled = old;
                if (TipHover(tile))
                {
                    TipStart(tile, Expansion.CardName(card), ci);
                    TipAdd(Expansion.CardBlurb(card), 12, Ui.Text);
                    TipAdd(price + " vectors here, " + full + " at the Black Market. It goes to your hand.", 11, Ui.Muted);
                }
            }
        }

        /// The Builder Shop's counter in its room: a workbench with a card fanned on it and a hammer, in amber.
        void DrawBuilderCounter(V2 c)
        {
            var ink = BuilderInk;
            float t = game.AnimT, pulse = 0.5f + 0.5f * Mathf.Sin(t * 2.2f);
            Circle(c, 78f, ink.WithAlpha(0.06f + 0.03f * pulse), 28);
            FillRect(c.X - 58f, c.Y - 4f, 116f, 26f, ink.WithAlpha(0.2f));  // the bench
            NodeFrame(c.X - 58f, c.Y - 4f, 116f, 26f, 3f, ink.WithAlpha(0.9f));
            for (int i = 0; i < 3; i++)  // three cards fanned above it
            {
                float x = c.X - 40f + i * 24f, y = c.Y - 40f + (i == 1 ? -4f : 0f) + Mathf.Sin(t * 2.4f + i) * 1.5f;
                FillRect(x, y, 32f, 30f, ink.WithAlpha(i == 1 ? 0.5f : 0.25f));
                NodeFrame(x, y, 32f, 30f, 2f, ink.WithAlpha(0.85f));
            }
            Line(c + new V2(30f, 14f), c + new V2(48f, -6f), 4f, ink.WithAlpha(0.95f));  // the hammer
            Line(c + new V2(40f, -14f), c + new V2(56f, 2f), 6f, ink.WithAlpha(0.95f));
        }
    }
}
