using UnityEngine;

namespace Arena
{
    /// The shop room's stall (RoomsLook.cs `DrawFloatingMark`), in the floating rooms' neon style, in the shop's gold: a patterned rug and a
    /// warm pool of light, two posts with swinging lanterns, a striped awning with a scalloped edge and a coin sign, a planked counter with
    /// its wares (a coin stack, an upgrade card, a potion and a sack), a big coin turning over it all and sparkles drifting up.
    /// Standing in it opens the shop screen; F (Interact) opens it again once you're done (ArenaApp.cs, `DrawShopReopenHint`).
    /// Preview: GRIDCORE=1 EXPANDMAP=0 CARDROOM=shop CARDDY=130 ZOOM=2 shot.sh x hud 1 0 1.
    public partial class ArenaApp
    {
        static readonly Rgba StallDeep = new Rgba(0.55f, 0.36f, 0.08f), StallCard = new Rgba(0.45f, 0.9f, 1f), StallPotion = new Rgba(0.45f, 1f, 0.55f);

        public void DrawShopStall(V2 c)
        {
            var ink = ShopInk;
            float t = game.AnimT, pulse = 0.5f + 0.5f * Mathf.Sin(t * 2.2f);

            // The floor: a warm pool of light and a rug with a diamond pattern and a fringe.
            Circle(c, 150f, ink.WithAlpha(0.035f + 0.02f * pulse), 36);
            Circle(c + new V2(0f, 10f), 105f, ink.WithAlpha(0.05f + 0.02f * pulse), 32);
            float rx = c.X - 84f, ry = c.Y + 22f, rw = 168f, rh = 74f;
            FillRect(rx, ry, rw, rh, StallDeep.WithAlpha(0.22f));
            NodeFrame(rx, ry, rw, rh, 2f, ink.WithAlpha(0.6f));
            NodeFrame(rx + 7f, ry + 7f, rw - 14f, rh - 14f, 1.5f, ink.WithAlpha(0.3f));
            for (int i = 0; i < 5; i++)
                Polygon(new V2(rx + 26f + i * 29f, ry + rh / 2f), 11f, 4, 0f, ink.WithAlpha(i % 2 == 0 ? 0.3f : 0.16f));
            for (int i = 0; i <= 12; i++)
            {
                float fx = rx + 4f + i * (rw - 8f) / 12f;
                Line(new V2(fx, ry + rh), new V2(fx, ry + rh + 6f), 1.5f, ink.WithAlpha(0.4f));
                Line(new V2(fx, ry), new V2(fx, ry - 5f), 1.5f, ink.WithAlpha(0.25f));
            }

            // The posts, and a lantern swinging from each.
            for (int s = -1; s <= 1; s += 2)
            {
                float px = c.X + s * 72f;
                FillRect(px - 3f, c.Y - 82f, 6f, 98f, StallDeep.WithAlpha(0.6f));
                Line(new V2(px, c.Y - 82f), new V2(px, c.Y + 16f), 2f, ink.WithAlpha(0.75f));
                float sw = Mathf.Sin(t * 1.7f + s) * 0.18f;
                var hook = new V2(px + s * 10f, c.Y - 62f);
                Line(new V2(px, c.Y - 62f), hook, 2f, ink.WithAlpha(0.7f));
                var lamp = hook + new V2(Mathf.Sin(sw) * 14f, Mathf.Cos(sw) * 14f);
                Line(hook, lamp, 1.5f, ink.WithAlpha(0.6f));
                float flick = 0.75f + 0.25f * Mathf.Sin(t * 9f + s * 2f) * Mathf.Sin(t * 5.3f);
                GlowDisc(lamp + new V2(0f, 6f), 30f, ink, 0.35f * flick);
                FillRect(lamp.X - 5f, lamp.Y, 10f, 12f, ink.WithAlpha(0.45f + 0.4f * flick));
                NodeFrame(lamp.X - 6f, lamp.Y - 1f, 12f, 14f, 1.5f, ink.WithAlpha(0.95f));
                Line(new V2(lamp.X - 3f, lamp.Y - 3f), new V2(lamp.X + 3f, lamp.Y - 3f), 2f, ink.WithAlpha(0.9f));
            }

            // The awning: stripes, a scalloped edge, a bright ridge.
            const int stripes = 9;
            const float aw = 172f, sw0 = aw / stripes, ay = -100f, ah = 30f;
            float ax = c.X - aw / 2f;
            for (int i = 0; i < stripes; i++)
            {
                var col = i % 2 == 0 ? ink.WithAlpha(0.62f) : StallDeep.WithAlpha(0.55f);
                float x = ax + i * sw0;
                Circle(new V2(x + sw0 / 2f, c.Y + ay + ah), sw0 / 2f, col, 12);
                Quad(new V2(x + 3f, c.Y + ay), new V2(x + sw0 - 3f + 3f, c.Y + ay), new V2(x + sw0, c.Y + ay + ah), new V2(x, c.Y + ay + ah), col);
            }
            for (int i = 0; i < stripes; i++)
                Arc(new V2(ax + i * sw0 + sw0 / 2f, c.Y + ay + ah), sw0 / 2f, 1.5f, ink.WithAlpha(0.8f), 12, 0f, MathX.Tau / 2f);
            GlowLine(new V2(ax, c.Y + ay), new V2(ax + aw, c.Y + ay), 3f, ink.WithAlpha(0.95f));
            Line(new V2(ax, c.Y + ay + ah), new V2(ax + aw, c.Y + ay + ah), 1.5f, ink.WithAlpha(0.5f));

            // The crest on the awning's ridge: a gem in a diamond plate with a glint.
            var sign = new V2(c.X, c.Y + ay - 4f);
            Polygon(sign, 16f, 4, 0f, StallDeep.WithAlpha(0.95f));
            Polygon(sign, 11f, 4, 0f, ink.WithAlpha(0.6f + 0.3f * pulse));
            Polygon(sign, 5f, 4, 0f, Rgba.White.WithAlpha(0.55f + 0.35f * pulse));
            for (int s = -1; s <= 1; s += 2)
                Line(new V2(c.X + s * 18f, sign.Y), new V2(c.X + s * 40f, sign.Y), 2f, ink.WithAlpha(0.8f));

            // The counter: a planked front with a bright top edge.
            float cx0 = c.X - 66f, cw = 132f, ctop = c.Y - 22f, ch = 38f;
            FillRect(cx0, ctop, cw, ch, StallDeep.WithAlpha(0.42f));
            for (int i = 1; i < 3; i++)
                Line(new V2(cx0 + 4f, ctop + i * ch / 3f), new V2(cx0 + cw - 4f, ctop + i * ch / 3f), 1.5f, ink.WithAlpha(0.25f));
            for (int i = 1; i < 6; i++)
                Line(new V2(cx0 + i * cw / 6f, ctop + (i % 2 == 0 ? 0f : ch / 3f)), new V2(cx0 + i * cw / 6f, ctop + (i % 2 == 0 ? ch / 3f : 2f * ch / 3f)), 1.5f, ink.WithAlpha(0.2f));
            NodeFrame(cx0, ctop, cw, ch, 2.5f, ink.WithAlpha(0.9f));
            GlowLine(new V2(cx0 - 6f, ctop), new V2(cx0 + cw + 6f, ctop), 4f, ink.WithAlpha(0.95f));

            // The wares on the counter: a stack of coins, an upgrade card with a shine running over it, a potion and a coin sack.
            float top = ctop - 2f;
            for (int i = 0; i < 4; i++)
            {
                float y = top - 5f - i * 5f;
                FillRect(c.X - 56f, y, 20f, 5f, ink.WithAlpha(0.6f + 0.1f * (i % 2)));
                Line(new V2(c.X - 56f, y), new V2(c.X - 36f, y), 1f, ink.WithAlpha(0.95f));
            }
            float cb = Mathf.Sin(t * 2f) * 2f;
            var card = new V2(c.X - 12f, top - 18f + cb);
            FillRect(card.X - 11f, card.Y - 15f, 22f, 30f, StallCard.WithAlpha(0.25f));
            NodeFrame(card.X - 11f, card.Y - 15f, 22f, 30f, 2f, StallCard.WithAlpha(0.95f));
            Polygon(card + new V2(0f, -2f), 6f, 4, 0f, StallCard.WithAlpha(0.8f));
            float sh = Mathf.Repeat(t * 0.6f, 1.6f) - 0.3f;
            if (sh >= 0f && sh <= 1f)
                Line(new V2(card.X - 11f + 22f * sh, card.Y + 15f), new V2(card.X - 11f + 22f * Mathf.Min(1f, sh + 0.25f), card.Y - 15f), 2f, Rgba.White.WithAlpha(0.6f));
            var pot = new V2(c.X + 22f, top - 10f);
            Circle(pot, 10f, StallPotion.WithAlpha(0.3f), 16);
            Circle(pot + new V2(0f, 2f), 7f * (0.8f + 0.2f * pulse), StallPotion.WithAlpha(0.55f), 14);
            Arc(pot, 10f, 2f, StallPotion.WithAlpha(0.95f), 16);
            FillRect(pot.X - 3f, pot.Y - 19f, 6f, 9f, StallPotion.WithAlpha(0.6f));
            FillRect(pot.X - 4f, pot.Y - 22f, 8f, 4f, ink.WithAlpha(0.9f));
            var sack = new V2(c.X + 48f, top - 11f);
            Circle(sack, 12f, StallDeep.WithAlpha(0.8f), 16);
            Arc(sack, 12f, 2f, ink.WithAlpha(0.9f), 16);
            Line(sack + new V2(-6f, -12f), sack + new V2(6f, -12f), 3f, ink.WithAlpha(0.9f));
            Polygon(sack + new V2(-4f, -18f), 4f, 3, 0.3f, ink.WithAlpha(0.8f));
            Polygon(sack + new V2(4f, -18f), 4f, 3, -0.3f, ink.WithAlpha(0.8f));
            Circle(sack + new V2(0f, 2f), 4f, ink.WithAlpha(0.85f), 10);

            // The big coin turning over the stall: its width is the turn, a rim, a face and a bright edge when it shows its side.
            var coin = new V2(c.X, c.Y - 148f + Mathf.Sin(t * 1.6f) * 5f);
            float turn = Mathf.Cos(t * 2.4f), wide = Mathf.Max(0.12f, Mathf.Abs(turn));
            GlowDisc(coin, 46f, ink, 0.3f + 0.15f * pulse);
            for (int k = 0; k < 3; k++)
            {
                float r = 18f - k * 5f;
                var col = k == 0 ? ink.WithAlpha(0.95f) : k == 1 ? StallDeep.WithAlpha(0.95f) : ink.WithAlpha(0.85f);
                Ellipse(coin, r * wide, r, col);
            }
            if (wide > 0.45f)
                Line(coin + new V2(0f, -6f), coin + new V2(0f, 6f), 2.5f * wide, StallDeep.WithAlpha(0.9f));

            // Sparkles drifting up through the light.
            for (int k = 0; k < 7; k++)
            {
                float life = Mathf.Repeat(t * 0.35f + k * 0.37f, 1f);
                float x = c.X + Mathf.Sin(k * 12.9898f) * 70f + Mathf.Sin(t * 1.3f + k) * 6f;
                float y = c.Y + 20f - life * 170f;
                float a = Mathf.Sin(life * Mathf.PI) * 0.85f;
                float s = 2.5f + 2f * Mathf.Sin(t * 6f + k * 2f) * Mathf.Sin(t * 6f + k * 2f);
                Line(new V2(x - s, y), new V2(x + s, y), 1.5f, ink.WithAlpha(a));
                Line(new V2(x, y - s), new V2(x, y + s), 1.5f, ink.WithAlpha(a));
            }
        }

    }
}
