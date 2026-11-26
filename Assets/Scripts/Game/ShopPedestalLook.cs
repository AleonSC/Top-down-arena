using UnityEngine;

namespace Arena
{
    /// MOCKUP: the shop room with no stall and no inner walls (Expansion.ShopBare), its wares on pedestals along the side walls: your
    /// shelf's first four cards on the left, the services (heal, revive token, add stock, sell) on the right; each is a stone plinth with a lit plate in the item's colour, the item's icon floating and turning
    /// over it, and a coin price tag under it (DrawPedestalTags, HUD). The one you stand by lights up and shows its name and the buy key.
    /// Preview: PEDESTALS=1 GRIDCORE=1 EXPANDMAP=0 CARDROOM=shop CARDDY=60 ZOOM=1.5 shot.sh x hud 1 0 1.
    public partial class ArenaApp
    {
        public static bool ShopPedestalsMock;
        static readonly Rgba HealInk = new Rgba(1f, 0.35f, 0.4f), HaloInk = new Rgba(1f, 0.9f, 0.55f);

        struct PedestalItem { public V2 At; public string[] Rows; public Rgba Ink; public string Name; public int Price; }

        PedestalItem[] ShopPedestalItems(V2 c)
        {
            var me = game.Me;
            var items = new PedestalItem[8];
            float[] ys = { -195f, -65f, 65f, 195f };
            for (int i = 0; i < 4; i++)  // the left wall: your shelf's first four cards
            {
                int id = me != null && me.ShelfLen > i ? me.Shelf[i] : new[] { 0, 7, 14, 3 }[i];
                var u = Upgrades.All[id];
                items[i] = new PedestalItem { At = c + new V2(-232f, ys[i]), Rows = PixelArt.RowsFor(u), Ink = Upgrades.RarityColor(u.Rarity), Name = u.Name, Price = Game.Price(id) };
            }
            // the right wall: the services
            items[4] = new PedestalItem { At = c + new V2(232f, ys[0]), Rows = PixelArt.RowsOf("regen2"), Ink = HealInk, Name = "Full heal", Price = Game.HealPrice };
            items[5] = new PedestalItem { At = c + new V2(232f, ys[1]), Rows = PixelArt.RowsOf("halo"), Ink = HaloInk, Name = "Revive token", Price = Game.RevivePrice };
            items[6] = new PedestalItem { At = c + new V2(232f, ys[2]), Rows = PixelArt.RowsOf("ricochet"), Ink = ShopInk, Name = "Add stock", Price = me != null ? Game.RestockPrice(me) : 25 };
            items[7] = new PedestalItem { At = c + new V2(232f, ys[3]), Rows = PixelArt.RowsOf("axe"), Ink = new Rgba(0.75f, 0.8f, 0.9f), Name = "Sell upgrade", Price = 0 };
            return items;
        }

        int NearPedestal(PedestalItem[] items)
        {
            var me = game.Me;
            if (me == null) return -1;
            int best = -1; float bd = 70f * 70f;
            for (int i = 0; i < items.Length; i++)
            {
                float d = (items[i].At - me.Disp).LengthSq;
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        public void DrawShopPedestals(V2 c)
        {
            var ink = ShopInk;
            float t = game.AnimT, pulse = 0.5f + 0.5f * Mathf.Sin(t * 2.2f);

            // The floor: a warm pool of light and a gold runner from door to door, a strip of lit floor along each wall under the pedestals.
            Circle(c, 230f, ink.WithAlpha(0.025f + 0.015f * pulse), 40);
            FillRect(c.X - 40f, c.Y - 300f, 80f, 600f, StallDeep.WithAlpha(0.16f));
            for (int s = -1; s <= 1; s += 2)
            {
                Line(new V2(c.X + s * 40f, c.Y - 300f), new V2(c.X + s * 40f, c.Y + 300f), 1.5f, ink.WithAlpha(0.4f));
                Line(new V2(c.X + s * 32f, c.Y - 300f), new V2(c.X + s * 32f, c.Y + 300f), 1f, ink.WithAlpha(0.18f));
                FillRect(c.X + s * 232f - 44f, c.Y - 270f, 88f, 540f, StallDeep.WithAlpha(0.12f));
                NodeFrame(c.X + s * 232f - 44f, c.Y - 270f, 88f, 540f, 1.5f, ink.WithAlpha(0.3f));
            }
            for (int i = 0; i < 9; i++)
                Polygon(new V2(c.X, c.Y - 256f + i * 64f), 9f, 4, 0f, ink.WithAlpha(i % 2 == 0 ? 0.3f : 0.15f));
            // the coin crest in the middle of the runner
            Polygon(c, 22f, 4, 0f, StallDeep.WithAlpha(0.8f));
            Polygon(c, 15f, 4, 0f, ink.WithAlpha(0.45f + 0.3f * pulse));
            Polygon(c, 6f, 4, 0f, Rgba.White.WithAlpha(0.5f + 0.35f * pulse));

            var items = ShopPedestalItems(c);
            int near = NearPedestal(items);
            for (int i = 0; i < items.Length; i++)
                DrawWarePedestal(items[i], t + i * 0.7f, i == near);

            // a lantern on the wall between each pair of pedestals
            for (int s = -1; s <= 1; s += 2)
                for (int j = 0; j < 3; j++)
                {
                    var lamp = new V2(c.X + s * 284f, c.Y - 130f + j * 130f);
                    float flick = 0.75f + 0.25f * Mathf.Sin(t * 9f + j * 2f + s) * Mathf.Sin(t * 5.3f + j);
                    GlowDisc(lamp, 34f, ink, 0.3f * flick);
                    FillRect(lamp.X - 5f, lamp.Y - 6f, 10f, 12f, ink.WithAlpha(0.45f + 0.4f * flick));
                    NodeFrame(lamp.X - 6f, lamp.Y - 7f, 12f, 14f, 1.5f, ink.WithAlpha(0.95f));
                }
        }

        /// One pedestal: a floor ring, a stone plinth with a lit plate in the item's colour, a beam, and the item's icon turning over it.
        void DrawWarePedestal(PedestalItem it, float t, bool near)
        {
            var c = it.At; var col = it.Ink;
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 2.2f), k = near ? 1.15f : 1f;
            var stone = new Rgba(0.14f, 0.12f, 0.1f, 0.95f);
            var edge = ShopInk;
            Circle(c + new V2(0f, 30f), 38f, col.WithAlpha((near ? 0.12f : 0.05f) + 0.03f * pulse), 24);
            if (near)
                for (int q = 0; q < 4; q++)
                    Arc(c + new V2(0f, 30f), 42f, 2f, col.WithAlpha(0.8f), 8, t * 1.2f + q * MathX.Tau / 4f, 0.9f);
            // base slab, shaft, top plate
            FillRect(c.X - 26f, c.Y + 30f, 52f, 9f, stone);
            NodeFrame(c.X - 26f, c.Y + 30f, 52f, 9f, 2f, edge.WithAlpha(0.7f));
            SetColor(stone);
            Quad(new V2(c.X - 13f, c.Y + 30f), new V2(c.X + 13f, c.Y + 30f), new V2(c.X + 9f, c.Y + 10f), new V2(c.X - 9f, c.Y + 10f));
            Line(new V2(c.X - 13f, c.Y + 30f), new V2(c.X - 9f, c.Y + 10f), 2f, edge.WithAlpha(0.55f));
            Line(new V2(c.X + 13f, c.Y + 30f), new V2(c.X + 9f, c.Y + 10f), 2f, edge.WithAlpha(0.55f));
            FillRect(c.X - 20f, c.Y + 3f, 40f, 8f, stone);
            NodeFrame(c.X - 20f, c.Y + 3f, 40f, 8f, 2f, edge.WithAlpha(0.9f));
            FillRect(c.X - 18f, c.Y + 3f, 36f, 2.5f, col.WithAlpha(0.55f + 0.3f * pulse));
            // the beam and rising motes
            FillRect(c.X - 6f, c.Y - 40f, 12f, 44f, col.WithAlpha((near ? 0.14f : 0.07f) + 0.04f * pulse));
            for (int i = 0; i < 3; i++)
            {
                float ph = (t * 0.5f + i * 0.33f) % 1f;
                Circle(c + new V2(Mathf.Sin(t * 1.3f + i * 1.9f) * 12f, 2f - ph * 44f), 1.6f, col.WithAlpha(0.7f * (1f - ph)), 6);
            }
            // the item, floating and slowly turning, on a soft halo
            var at = c + new V2(0f, -34f + Mathf.Sin(t * 2.4f) * 3.5f);
            GlowDisc(at, 34f * k, col, near ? 0.45f : 0.28f);
            Arc(at, 26f * k, 1.5f, col.WithAlpha(0.4f), 24, t * 0.9f, 1.6f);
            Arc(at, 26f * k, 1.5f, col.WithAlpha(0.4f), 24, t * 0.9f + MathX.Pi, 1.6f);
            float turn = Mathf.Max(0.55f, Mathf.Abs(Mathf.Cos(t * 0.6f)));
            DrawIconGl(it.Rows, at, 40f * k, turn);
        }

        /// DrawHud: a coin price under every pedestal, and over the one you stand by its name and the buy key.
        void DrawPedestalTags()
        {
            if (!ShopPedestalsMock || game.Me == null) return;
            var world = game.CurrentWorld;
            if (world == null) return;
            foreach (var a in world.Areas)
            {
                if (a.Sealed || a.Kind != RoomKind.Shop) continue;
                var items = ShopPedestalItems(a.Mid);
                int near = NearPedestal(items);
                for (int i = 0; i < items.Length; i++)
                {
                    var it = items[i];
                    bool afford = game.Me.Gold >= it.Price;
                    V2 p = ToUi(it.At + new V2(0f, 48f));
                    var r = new Rect(p.X - 30f, p.Y - 9f, 60f, 18f);
                    Ui.Pill(r, new Color(0.08f, 0.06f, 0.02f, 0.85f));
                    Ui.Icon(new Rect(r.x + 6f, r.y + 3f, 12f, 12f), PixelArt.Get("ui_coin"), afford ? 1f : 0.5f);
                    Ui.Label(new Rect(r.x + 18f, r.y, r.width - 20f, r.height), it.Price == 0 ? "SELL" : it.Price.ToString(), 11, afford ? CrtStyle.Gold : CrtStyle.Danger, TextAnchor.MiddleCenter, true);
                    if (i != near) continue;
                    string key = Keybinds.KeyName(Keybinds.Get(KeyAction.Interact)).ToUpperInvariant();
                    V2 q = ToUi(game.Me.Disp) + new V2(0f, 74f);  // (under you, clear of the back row)
                    var n = new Rect(q.X - 90f, q.Y - 26f, 180f, 20f);
                    Ui.Label(n, it.Name.ToUpperInvariant(), 13, it.Ink.ToColor(), TextAnchor.MiddleCenter, true);
                    var b = new Rect(q.X - 52f, q.Y - 4f, 104f, 20f);
                    Ui.Pill(b, afford ? CrtStyle.Gold : new Color(0f, 0f, 0f, 0.65f));
                    Ui.Label(b, key + (it.Price == 0 ? "  SELL" : "  BUY"), 11, afford ? new Color(0.12f, 0.08f, 0.02f) : CrtStyle.Danger, TextAnchor.MiddleCenter, true);
                }
            }
        }
    }
}
