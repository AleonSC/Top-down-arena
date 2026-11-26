using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The pedestal shop (ShopPedestals.cs): no stall and no screen. A gold runner from door to door, a lit strip along each pedestal wall
    /// with lanterns between the pedestals, your shelf's unsold cards on one wall and the shop room's services (Add stock, Sell, Vector
    /// pack, Floor kit) on the other. Each is a stone plinth with a lit plate in the item's colour and the item's icon floating and turning
    /// over it; the one you stand at lights up. HUD (DrawPedestalTags): a coin price under each, and under you the nearest one's name,
    /// what it does and the Interact key (TickPedestalKey buys it; Sell opens the sell list, ShopUi.cs DrawSellUpgrade).
    /// Preview: GRIDCORE=1 EXPANDMAP=0 CARDROOM=shop CARDABOVE=1 SHOPGOLD=90 ZOOM=0.9 shot.sh x hud 1 0 1 (CARDDY/CARDDX=<px>: stand there).
    public partial class ArenaApp
    {
        static readonly int[] PedestalServices = { 0, 1, 5, 4 };  // Add stock, Sell upgrade, Vector pack, Floor kit (ShopUi.cs service ids; the last drops first in a small room)
        static readonly Rgba SellInk = new Rgba(0.75f, 0.8f, 0.9f), VectorInk = new Rgba(0.45f, 0.85f, 1f);

        struct ShopWare
        {
            public V2 At;
            public string[] Rows;
            public Rgba Ink;
            public string Name, Desc;
            public int Price, Slot, Service;  // Slot: the shelf slot (-1 for a service); Service: the ShopUi service id (-1 for a card)
            public bool Empty, Used;           // Empty: nothing on it; Used: can't be had now (maxed, nothing to sell, can't take the card)
        }

        /// This player's pedestal shop room, if they stand in one.
        RoomArea? PedestalShopOf(Player me)
        {
            if (me == null || !game.ExpandingOn || !game.RoomsOn)
                return null;
            var w = game.CurrentWorld;
            if (w == null || w.Areas == null)
                return null;
            foreach (var a in w.Areas)
                if (a.PedestalWalls != 0 && !a.Sealed && a.Contains(me.Disp))
                    return a;
            return null;
        }

        bool InPedestalShop(Player me) => game.ServiceRooms && PedestalShopOf(me).HasValue;

        /// The wares on this room's pedestals for this player: wall 0 the shelf's unsold cards in order, wall 1 the services.
        List<ShopWare> ShopWares(Player me, RoomArea a)
        {
            var res = new List<ShopWare>();
            var cards = ShopPedestals.Spots(a, 0);
            int slot = 0, shelf = me != null ? Game.ShelfCount(me) : 0;
            for (int i = 0; i < cards.Length; i++)
            {
                while (slot < shelf && (me.Shelf[slot] < 0 || Game.Sold(me, slot)))
                    slot++;
                if (slot >= shelf)
                {
                    res.Add(new ShopWare { At = cards[i], Ink = ShopInk, Empty = true, Slot = -1, Service = -1 });
                    continue;
                }
                int id = me.Shelf[slot];
                var u = Upgrades.All[id];
                res.Add(new ShopWare { At = cards[i], Rows = PixelArt.RowsFor(u), Ink = Upgrades.RarityColor(u.Rarity), Name = u.Name, Desc = Upgrades.DescFor(u, me.Weapon),
                    Price = Game.Price(id), Slot = slot, Service = -1, Used = !Upgrades.Available(id, game.OfferFor(me)) });
                slot++;
            }
            var svc = ShopPedestals.Spots(a, 1);
            for (int i = 0; i < svc.Length && i < PedestalServices.Length; i++)
            {
                int k = PedestalServices[i];
                bool used = true;
                int price = me != null ? ServicePrice(me, k, out used) : 0;
                res.Add(new ShopWare { At = svc[i], Rows = PixelArt.RowsOf(ServiceIcons[k]), Ink = k == 1 ? SellInk : k == 5 ? VectorInk : ShopInk, Name = ServiceNames[k],
                    Desc = ServiceBlurb(me, k), Price = price, Slot = -1, Service = k, Used = used });
            }
            return res;
        }

        string ServiceBlurb(Player me, int k)
        {
            switch (k)
            {
                case 0: return "Two more cards join your shelf. Each add costs more.";
                case 1: return me != null && me.Taken.Count == 0 ? "You have nothing to sell." : "Sell one stack for half its shelf price.";
                case 4: return "Turn a room into a hazard floor.";
                default: return Game.VectorPack + " more vectors to build with. Can go past your cap.";
            }
        }

        /// The ware you stand at (an index into the list), or -1.
        static int NearWare(List<ShopWare> wares, V2 p)
        {
            int best = -1;
            float bd = ShopPedestals.Reach * ShopPedestals.Reach;
            for (int i = 0; i < wares.Count; i++)
            {
                float d = (wares[i].At - p).LengthSq;
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        public void DrawShopPedestals(RoomArea a)
        {
            var ink = ShopInk;
            var c = a.Mid;
            float t = game.AnimT, pulse = 0.5f + 0.5f * Mathf.Sin(t * 2.2f);
            bool ew = a.PedestalWalls == 1;  // (pedestals on the west and east walls: the runner goes north to south)
            float halfLong = ew ? a.Size.Y / 2f : a.Size.X / 2f, halfAcross = ew ? a.Size.X / 2f : a.Size.Y / 2f;
            System.Func<float, float, V2> P = (ac, al) => ew ? c + new V2(ac, al) : c + new V2(al, ac);  // (across toward the pedestal walls, then along them)

            // The floor: a warm pool of light, the gold runner from door to door with its diamonds and a coin crest in the middle.
            Circle(c, Mathf.Min(halfLong, halfAcross) * 0.8f, ink.WithAlpha(0.025f + 0.015f * pulse), 40);
            V2 r0 = P(-40f, -halfLong), r1 = P(40f, halfLong);
            FillRect(Mathf.Min(r0.X, r1.X), Mathf.Min(r0.Y, r1.Y), Mathf.Abs(r1.X - r0.X), Mathf.Abs(r1.Y - r0.Y), StallDeep.WithAlpha(0.16f));
            for (int s = -1; s <= 1; s += 2)
            {
                Line(P(s * 40f, -halfLong), P(s * 40f, halfLong), 1.5f, ink.WithAlpha(0.4f));
                Line(P(s * 32f, -halfLong), P(s * 32f, halfLong), 1f, ink.WithAlpha(0.18f));
            }
            int diamonds = Mathf.Max(3, (int)(halfLong * 2f / 64f));
            for (int i = 0; i < diamonds; i++)
            {
                float along = -halfLong + (i + 0.5f) * halfLong * 2f / diamonds;
                if (Mathf.Abs(along) > 30f)
                    Polygon(P(0f, along), 9f, 4, 0f, ink.WithAlpha(i % 2 == 0 ? 0.3f : 0.15f));
            }
            Polygon(c, 22f, 4, 0f, StallDeep.WithAlpha(0.8f));
            Polygon(c, 15f, 4, 0f, ink.WithAlpha(0.45f + 0.3f * pulse));
            Polygon(c, 6f, 4, 0f, Rgba.White.WithAlpha(0.5f + 0.35f * pulse));

            var me = game.Me;
            var wares = ShopWares(me, a);
            bool open = game.ShopOpen;
            int near = me != null && open && a.Contains(me.Disp) ? NearWare(wares, me.Disp) : -1;

            // The lit strip under each wall's pedestals and a lantern on the wall between each pair.
            for (int w = 0; w < 2; w++)
            {
                var spots = ShopPedestals.Spots(a, w);
                if (spots.Length == 0)
                    continue;
                float acrossW = (w == 0 ? -1f : 1f) * (halfAcross - ShopPedestals.WallGap);
                V2 s0 = P(acrossW - 44f, -halfLong + 30f), s1 = P(acrossW + 44f, halfLong - 30f);
                float sx = Mathf.Min(s0.X, s1.X), sy = Mathf.Min(s0.Y, s1.Y), sw = Mathf.Abs(s1.X - s0.X), sh = Mathf.Abs(s1.Y - s0.Y);
                FillRect(sx, sy, sw, sh, StallDeep.WithAlpha(0.12f));
                NodeFrame(sx, sy, sw, sh, 1.5f, ink.WithAlpha(0.3f));
                float wallAcross = (w == 0 ? -1f : 1f) * (halfAcross - 16f);
                for (int j = 0; j + 1 < spots.Length; j++)
                {
                    var mid = (spots[j] + spots[j + 1]) * 0.5f;
                    var lamp = ew ? new V2(c.X + wallAcross, mid.Y + 22f) : new V2(mid.X, c.Y + wallAcross);
                    float flick = 0.75f + 0.25f * Mathf.Sin(t * 9f + j * 2f + w) * Mathf.Sin(t * 5.3f + j);
                    GlowDisc(lamp, 34f, ink, (open ? 0.3f : 0.15f) * flick);
                    FillRect(lamp.X - 5f, lamp.Y - 6f, 10f, 12f, ink.WithAlpha((open ? 0.45f : 0.25f) + 0.4f * flick));
                    NodeFrame(lamp.X - 6f, lamp.Y - 7f, 12f, 14f, 1.5f, ink.WithAlpha(0.95f));
                }
            }
            for (int i = 0; i < wares.Count; i++)
                DrawWarePedestal(wares[i], t + i * 0.7f, i == near, open);
        }

        /// One pedestal: a floor ring, a stone plinth with a lit plate in the item's colour, a beam, and the item's icon turning over it.
        void DrawWarePedestal(ShopWare it, float t, bool near, bool open)
        {
            var c = it.At;
            var col = it.Empty ? ShopInk.WithAlpha(0.35f) : it.Used || !open ? it.Ink.Mix(new Rgba(0.5f, 0.5f, 0.5f), 0.6f) : it.Ink;
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
            if (it.Empty)
                return;  // (a sold-out spot: the plinth alone)
            // the beam and rising motes
            FillRect(c.X - 6f, c.Y - 40f, 12f, 44f, col.WithAlpha((near ? 0.14f : 0.07f) + 0.04f * pulse));
            for (int i = 0; i < 3; i++)
            {
                float ph = (t * 0.5f + i * 0.33f) % 1f;
                Circle(c + new V2(Mathf.Sin(t * 1.3f + i * 1.9f) * 12f, 2f - ph * 44f), 1.6f, col.WithAlpha(0.7f * (1f - ph)), 6);
            }
            // the item, floating and slowly turning, on a soft halo with two arcs going round
            var at = c + new V2(0f, -34f + Mathf.Sin(t * 2.4f) * 3.5f);
            GlowDisc(at, 34f * k, col, near ? 0.45f : 0.28f);
            Arc(at, 26f * k, 1.5f, col.WithAlpha(0.4f), 24, t * 0.9f, 1.6f);
            Arc(at, 26f * k, 1.5f, col.WithAlpha(0.4f), 24, t * 0.9f + MathX.Pi, 1.6f);
            float turn = Mathf.Max(0.55f, Mathf.Abs(Mathf.Cos(t * 0.6f)));
            DrawIconGl(it.Rows, at, 40f * k, turn);
            if (it.Used || !open)
                FillRect(at.X - 22f * k, at.Y - 22f * k, 44f * k, 44f * k, new Rgba(0.05f, 0.05f, 0.07f, 0.45f));  // (dimmed: can't be had now)
        }

        /// DrawHud: in a pedestal shop, a coin price under every pedestal, and under you the nearest one's name, what it does and the key.
        void DrawPedestalTags()
        {
            var me = game.Me;
            if (me == null || me.Downed || !game.ServiceRooms)
                return;
            var room = PedestalShopOf(me);
            if (!room.HasValue)
                return;
            var wares = ShopWares(me, room.Value);
            bool open = game.ShopOpen;
            int near = NearWare(wares, me.Disp);
            for (int i = 0; i < wares.Count; i++)
            {
                var it = wares[i];
                if (it.Empty || !open || i == near)
                    continue;  // (the one you stand at: its panel says it)
                bool afford = me.Gold >= it.Price;
                V2 p = ToUi(it.At + (room.Value.PedestalWalls == 1 ? new V2(it.At.X < room.Value.Mid.X ? 78f : -78f, 4f) : new V2(0f, 52f)));  // (beside it, toward the runner; under it on a north or south wall)
                var r = new Rect(p.X - 30f, p.Y - 9f, 60f, 18f);
                Ui.Pill(r, new Color(0.08f, 0.06f, 0.02f, 0.85f));
                if (it.Service == 1)
                    Ui.Label(r, "SELL", 11, CrtStyle.Gold, TextAnchor.MiddleCenter, true);
                else if (it.Used)
                    Ui.Label(r, it.Service == 4 ? "FULL" : "MAXED", 10, Ui.Muted, TextAnchor.MiddleCenter, true);
                else
                {
                    Ui.Icon(new Rect(r.x + 6f, r.y + 3f, 12f, 12f), PixelArt.Get("ui_coin"), afford ? 1f : 0.5f);
                    Ui.Label(new Rect(r.x + 18f, r.y, r.width - 20f, r.height), it.Price.ToString(), 11, afford ? CrtStyle.Gold : CrtStyle.Danger, TextAnchor.MiddleCenter, true);
                }
            }
            if (near < 0)
                return;
            var w = wares[near];
            if (w.Empty)
                return;
            var rm = room.Value;
            V2 q = rm.PedestalWalls == 1 ? new V2(ToUi(rm.Mid).X, ToUi(w.At).Y - 20f) : new V2(ToUi(w.At).X, ToUi(rm.Mid).Y - 42f);  // (on the runner, level with the pedestal: clear of the others and their prices)
            var panel = new Rect(q.X - 96f, q.Y, 192f, 88f);
            Ui.Fill(panel, new Color(0.05f, 0.04f, 0.02f, 0.9f));
            DkBox(panel, w.Ink.ToColor(), 0.8f);
            Ui.Label(new Rect(panel.x + 10f, panel.y + 4f, panel.width - 20f, 20f), w.Name.ToUpperInvariant(), 13, w.Ink.ToColor(), TextAnchor.MiddleCenter, true);
            Ui.Label(new Rect(panel.x + 10f, panel.y + 23f, panel.width - 20f, 34f), w.Desc ?? "", 10, Ui.Text, TextAnchor.UpperCenter, false, true);
            string key = Keybinds.KeyName(Keybinds.Get(KeyAction.Interact)).ToUpperInvariant();
            bool can = open && !w.Used && (w.Service == 1 || me.Gold >= w.Price);
            string verb = !open ? "OPENS AFTER THE WAVE" : w.Used ? (w.Service == 1 ? "NOTHING TO SELL" : w.Service == 4 ? "KIT SLOTS FULL" : "CAN'T TAKE MORE") : w.Service == 1 ? key + "  SELL" : me.Gold < w.Price ? "NEED " + (w.Price - me.Gold) + " MORE GOLD" : key + "  BUY  ·  " + w.Price;
            var b = new Rect(q.X - 84f, panel.yMax - 24f, 168f, 20f);
            Ui.Pill(b, can ? CrtStyle.Gold : new Color(0f, 0f, 0f, 0.65f));
            Ui.Label(b, verb, 11, can ? new Color(0.12f, 0.08f, 0.02f) : CrtStyle.Danger, TextAnchor.MiddleCenter, true);
        }

        /// Interact at a pedestal: buy it (a card, Add stock, Vector pack, Floor kit) or open the sell list. True if the key was taken.
        bool TickPedestalKey(Player me)
        {
            if (me == null || me.Downed || !game.ShopOpen || !game.ServiceRooms || !Keybinds.Down(KeyAction.Interact))
                return false;
            var room = PedestalShopOf(me);
            if (!room.HasValue)
                return false;
            var wares = ShopWares(me, room.Value);
            int near = NearWare(wares, me.Disp);
            if (near < 0 || wares[near].Empty)
                return false;
            var w = wares[near];
            if (w.Used || w.Service != 1 && me.Gold < w.Price)
                return true;  // (nothing happens: the tag says why)
            if (me.ShopDone)
                game.Shop(ShopAction.Done, 0);  // (Done elsewhere: back to shopping; the host reads it before the buy)
            if (w.Slot >= 0)
                game.Shop(ShopAction.Buy, w.Slot);
            else if (w.Service == 1)
                shopSellOpen = true;
            else
                game.Shop(w.Service == 0 ? ShopAction.Restock : w.Service == 4 ? ShopAction.RoomKit : ShopAction.Vectors);
            return true;
        }

        /// The sell list over a pedestal shop (Sell's pedestal), while you stay in the room and the shop is open.
        bool PedestalSellUp(Player me)
        {
            if (ShopScreenUp(me))
                return false;
            if (shopSellOpen && !(game.ShopOpen && InPedestalShop(me)))
                shopSellOpen = false;
            return shopSellOpen;
        }
    }
}
