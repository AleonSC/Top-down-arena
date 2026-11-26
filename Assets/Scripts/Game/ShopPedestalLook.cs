using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Pedestal rooms (ShopPedestals.cs): the shop, Black Market, Arms Dealer and Medic have no stall and no screen. A runner from the door(s),
    /// a lit strip along each pedestal wall with lanterns between the pedestals, and the wares on stone plinths lit in their colour with the
    /// item turning over each; the one you stand at lights up. The shop: your shelf's unsold cards on one wall, Add stock, Sell, Vector pack and
    /// Floor kit on the other; the Black Market its four buildables; the Arms Dealer every weapon mod; the Medic a full heal and a revive token.
    /// HUD (DrawPedestalTags): a coin price by each, and the nearest one's name, what it does and the Interact key (TickPedestalKey buys it;
    /// Sell opens the sell list, ShopUi.cs DrawSellUpgrade).
    /// Preview: GRIDCORE=1 EXPANDMAP=0 CARDROOM=shop|market|arms|medic CARDABOVE=1 SHOPGOLD=90 ZOOM=0.9 shot.sh x hud 1 0 1 (CARDDX/CARDDY=<px>: stand there).
    public partial class ArenaApp
    {
        static readonly int[] PedestalServices = { 0, 1, 5, 4 };  // Add stock, Sell upgrade, Vector pack, Floor kit (ShopUi.cs service ids; the last drops first in a small room)
        static readonly Rgba SellInk = new Rgba(0.75f, 0.8f, 0.9f), VectorInk = new Rgba(0.45f, 0.85f, 1f), ReviveInk = new Rgba(1f, 0.9f, 0.55f);
        static readonly string[] BuildNames = { "Teleporter", "Turret", "Landmine", "Slow field" };
        static readonly string[] BuildBlurbs = { "A pair of pads that link two rooms. Placed from the build map.", "A sentry that fires at what comes near.", "Blows up under the first enemy to step on it.", "Slows every enemy inside it." };

        struct ShopWare
        {
            public V2 At;
            public byte Wall;                  // the wall it stands against (ShopPedestals bits)
            public string[] Rows;              // its pixel icon, or null (a buildable: Build >= 0 draws its glyph)
            public Rgba Ink;
            public string Name, Desc;
            public int Price, Slot, Service, Build, Mod;  // Slot: a shelf slot; Service: a ShopUi service id; Build: a BuildKind; Mod: an upgrade id (-1 when not)
            public bool Empty, Used;           // Empty: nothing on it; Used: can't be had now (maxed, nothing to sell, can't take it)
        }

        static Rgba VendorInk(RoomKind k) => k == RoomKind.BlackMarket ? BlackMarketInk : k == RoomKind.Arms ? ArmsInk : k == RoomKind.Medic ? MedicInk : ShopInk;
        static Rgba VendorDeep(RoomKind k) => k == RoomKind.Shop ? StallDeep : VendorInk(k).Darkened(0.4f);

        /// This player's pedestal room, if they stand in one.
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

        static ShopWare NoWare() => new ShopWare { Slot = -1, Service = -1, Build = -1, Mod = -1 };

        /// The door walls of a pedestal room (the walls without pedestals).
        static byte DoorWalls(RoomArea a) => (byte)(15 & ~a.PedestalWalls);

        static byte OppositeWall(byte w) => w == ShopPedestals.West ? ShopPedestals.East : w == ShopPedestals.East ? ShopPedestals.West : w == ShopPedestals.North ? ShopPedestals.South : ShopPedestals.North;

        /// Lay these wares out on the room's pedestal walls: as evenly as they go, the wall facing the door (if one) taking the extras.
        static void Place(RoomArea a, List<ShopWare> items, List<ShopWare> res)
        {
            var walls = ShopPedestals.Walls(a);
            byte doors = DoorWalls(a);
            byte far = doors == ShopPedestals.West || doors == ShopPedestals.East || doors == ShopPedestals.North || doors == ShopPedestals.South ? OppositeWall(doors) : (byte)0;
            if (far != 0 && walls.Remove(far))
                walls.Insert(0, far);
            int k = walls.Count, next = 0;
            var caps = new int[k];
            var counts = new int[k];
            for (int i = 0; i < k; i++)
                caps[i] = ShopPedestals.Spots(a, walls[i]).Length;
            for (int left = items.Count; left > 0; left--)  // (one at a time to the emptiest wall with room, the far wall first on a tie)
            {
                int best = -1;
                for (int i = 0; i < k; i++)
                    if (counts[i] < caps[i] && (best < 0 || counts[i] * caps[best] < counts[best] * caps[i]))
                        best = i;
                if (best < 0)
                    break;
                counts[best]++;
            }
            for (int i = 0; i < k; i++)
            {
                int count = counts[i];
                var spots = ShopPedestals.Spots(a, walls[i]);
                if (count == 0) continue;
                // (count of the wall's spots, spread over its length)
                for (int j = 0; j < count; j++)
                {
                    float f = (j + 0.5f) / count * spots.Length - 0.5f;
                    V2 at = spots[Mathf.Clamp(Mathf.FloorToInt(f), 0, spots.Length - 1)];
                    if (spots.Length > 1)
                    {
                        float lo = Mathf.Clamp(Mathf.Floor(f), 0, spots.Length - 2);
                        at = spots[(int)lo] + (spots[(int)lo + 1] - spots[(int)lo]) * (f - lo);
                    }
                    var it = items[next++];
                    it.At = at;
                    it.Wall = walls[i];
                    res.Add(it);
                }
            }
        }

        /// The wares on this room's pedestals for this player.
        List<ShopWare> ShopWares(Player me, RoomArea a)
        {
            var res = new List<ShopWare>();
            if (a.Kind == RoomKind.Shop)
            {
                var walls = ShopPedestals.Walls(a);
                var cards = walls.Count > 0 ? ShopPedestals.Spots(a, walls[0]) : new V2[0];
                int slot = 0, shelf = me != null ? Game.ShelfCount(me) : 0;
                for (int i = 0; i < cards.Length; i++)
                {
                    while (slot < shelf && (me.Shelf[slot] < 0 || Game.Sold(me, slot)))
                        slot++;
                    var w = NoWare();
                    w.At = cards[i]; w.Wall = walls[0]; w.Ink = ShopInk;
                    if (slot >= shelf)
                    {
                        w.Empty = true;
                        res.Add(w);
                        continue;
                    }
                    int id = me.Shelf[slot];
                    var u = Upgrades.All[id];
                    w.Rows = PixelArt.RowsFor(u); w.Ink = Upgrades.RarityColor(u.Rarity); w.Name = u.Name; w.Desc = Upgrades.DescFor(u, me.Weapon);
                    w.Price = Game.Price(id); w.Slot = slot; w.Used = !Upgrades.Available(id, game.OfferFor(me));
                    res.Add(w);
                    slot++;
                }
                var svc = walls.Count > 1 ? ShopPedestals.Spots(a, walls[1]) : new V2[0];
                for (int i = 0; i < svc.Length && i < PedestalServices.Length; i++)
                    res.Add(ServiceWare(me, PedestalServices[i], svc[i], walls[1]));
                return res;
            }
            var items = new List<ShopWare>();
            if (a.Kind == RoomKind.BlackMarket)
                for (int k = 0; k < Market.Count; k++)
                {
                    var w = NoWare();
                    w.Build = k; w.Ink = BlackMarketInk; w.Name = BuildNames[Mathf.Min(k, BuildNames.Length - 1)]; w.Desc = BuildBlurbs[Mathf.Min(k, BuildBlurbs.Length - 1)];
                    w.Price = Market.Prices[k]; w.Used = me == null || me.Stash[k] >= Market.StashMax;
                    items.Add(w);
                }
            else if (a.Kind == RoomKind.Arms)
                foreach (int id in Game.ModIds)
                {
                    var u = Upgrades.All[id];
                    var w = NoWare();
                    w.Mod = id; w.Rows = PixelArt.RowsFor(u); w.Ink = ArmsInk; w.Name = u.Name; w.Desc = me != null ? Upgrades.DescFor(u, me.Weapon) : "";
                    w.Price = me != null ? Game.RoomTypePrice(me, id) : 0; w.Used = me == null || !Upgrades.Available(id, game.OfferFor(me));
                    items.Add(w);
                }
            else if (a.Kind == RoomKind.Medic)
            {
                items.Add(ServiceWare(me, 2, default(V2), 0));
                items.Add(ServiceWare(me, 3, default(V2), 0));
            }
            Place(a, items, res);
            return res;
        }

        ShopWare ServiceWare(Player me, int k, V2 at, byte wall)
        {
            var w = NoWare();
            bool used = true;
            w.Price = me != null ? ServicePrice(me, k, out used) : 0;
            w.Used = used; w.At = at; w.Wall = wall; w.Service = k;
            w.Rows = PixelArt.RowsOf(ServiceIcons[k]); w.Name = ServiceNames[k]; w.Desc = ServiceBlurb(me, k);
            w.Ink = k == 1 ? SellInk : k == 5 ? VectorInk : k == 2 ? MedicInk : k == 3 ? ReviveInk : ShopInk;
            return w;
        }

        string ServiceBlurb(Player me, int k)
        {
            switch (k)
            {
                case 0: return "Two more cards join your shelf. Each add costs more.";
                case 1: return me != null && me.Taken.Count == 0 ? "You have nothing to sell." : "Sell one stack for half its shelf price.";
                case 2: return me != null && me.Hp >= me.MaxHp ? "You're at full health." : "Back to full health.";
                case 3: return me != null && me.ReviveToken ? "You have one." : "Get back up once if you go down.";
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
            var ink = VendorInk(a.Kind);
            var deep = VendorDeep(a.Kind);
            var c = a.Mid;
            float t = game.AnimT, pulse = 0.5f + 0.5f * Mathf.Sin(t * 2.2f);
            byte doors = DoorWalls(a);
            bool nOpen = (doors & ShopPedestals.North) != 0, sOpen = (doors & ShopPedestals.South) != 0, wOpen = (doors & ShopPedestals.West) != 0, eOpen = (doors & ShopPedestals.East) != 0;
            bool vertical = nOpen || sOpen || !(wOpen || eOpen);  // (the runner goes north to south when a door is on either of those walls)
            float halfLong = vertical ? a.Size.Y / 2f : a.Size.X / 2f, halfAcross = vertical ? a.Size.X / 2f : a.Size.Y / 2f;
            System.Func<float, float, V2> P = (ac, al) => vertical ? c + new V2(ac, al) : c + new V2(al, ac);  // (across the runner, then along it)
            // The runner runs door to door, or from the one door to the pedestals facing it.
            bool fromLo = vertical ? nOpen : wOpen, fromHi = vertical ? sOpen : eOpen;
            float r0 = fromLo ? -halfLong : -halfLong + 140f, r1 = fromHi ? halfLong : halfLong - 140f;

            // The floor: a pool of light, the runner with its diamonds and a crest in the middle.
            Circle(c, Mathf.Min(halfLong, halfAcross) * 0.8f, ink.WithAlpha(0.025f + 0.015f * pulse), 40);
            V2 q0 = P(-40f, r0), q1 = P(40f, r1);
            FillRect(Mathf.Min(q0.X, q1.X), Mathf.Min(q0.Y, q1.Y), Mathf.Abs(q1.X - q0.X), Mathf.Abs(q1.Y - q0.Y), deep.WithAlpha(0.16f));
            for (int s = -1; s <= 1; s += 2)
            {
                Line(P(s * 40f, r0), P(s * 40f, r1), 1.5f, ink.WithAlpha(0.4f));
                Line(P(s * 32f, r0), P(s * 32f, r1), 1f, ink.WithAlpha(0.18f));
            }
            int diamonds = Mathf.Max(2, (int)((r1 - r0) / 64f));
            for (int i = 0; i < diamonds; i++)
            {
                float along = r0 + (i + 0.5f) * (r1 - r0) / diamonds;
                if (Mathf.Abs(along) > 30f)
                    Polygon(P(0f, along), 9f, 4, 0f, ink.WithAlpha(i % 2 == 0 ? 0.3f : 0.15f));
            }
            Polygon(c, 22f, 4, 0f, deep.WithAlpha(0.8f));
            Polygon(c, 15f, 4, 0f, ink.WithAlpha(0.45f + 0.3f * pulse));
            Polygon(c, 6f, 4, 0f, Rgba.White.WithAlpha(0.5f + 0.35f * pulse));

            var me = game.Me;
            var wares = ShopWares(me, a);
            bool open = game.ShopOpen;
            int near = me != null && open && a.Contains(me.Disp) ? NearWare(wares, me.Disp) : -1;

            // The lit strip along each pedestal wall and a lantern on the wall between each pair of its pedestals.
            foreach (var wall in ShopPedestals.Walls(a))
            {
                var spots = new List<V2>();
                foreach (var w in wares)
                    if (w.Wall == wall)
                        spots.Add(w.At);
                if (spots.Count == 0)
                    continue;
                bool ew = wall == ShopPedestals.West || wall == ShopPedestals.East;
                float lo = float.MaxValue, hi = float.MinValue;
                foreach (var s in spots) { float v = ew ? s.Y : s.X; lo = Mathf.Min(lo, v); hi = Mathf.Max(hi, v); }
                float acrossAt = ew ? spots[0].X : spots[0].Y - 8f;
                float x0 = ew ? acrossAt - 44f : lo - 54f, y0 = ew ? lo - 70f : acrossAt - 56f, ww = ew ? 88f : hi - lo + 108f, hh = ew ? hi - lo + 140f : 112f;
                FillRect(x0, y0, ww, hh, deep.WithAlpha(0.12f));
                NodeFrame(x0, y0, ww, hh, 1.5f, ink.WithAlpha(0.3f));
                float wallAt = wall == ShopPedestals.West ? a.Min.X + 16f : wall == ShopPedestals.East ? a.Max.X - 16f : wall == ShopPedestals.North ? a.Min.Y + 16f : a.Max.Y - 16f;
                spots.Sort((p, q) => (ew ? p.Y : p.X).CompareTo(ew ? q.Y : q.X));
                for (int j = 0; j + 1 < spots.Count; j++)
                {
                    var mid = (spots[j] + spots[j + 1]) * 0.5f;
                    var lamp = ew ? new V2(wallAt, mid.Y + 22f) : new V2(mid.X, wallAt);
                    float flick = 0.75f + 0.25f * Mathf.Sin(t * 9f + j * 2f + wall) * Mathf.Sin(t * 5.3f + j);
                    GlowDisc(lamp, 34f, ink, (open ? 0.3f : 0.15f) * flick);
                    FillRect(lamp.X - 5f, lamp.Y - 6f, 10f, 12f, ink.WithAlpha((open ? 0.45f : 0.25f) + 0.4f * flick));
                    NodeFrame(lamp.X - 6f, lamp.Y - 7f, 12f, 14f, 1.5f, ink.WithAlpha(0.95f));
                }
            }
            for (int i = 0; i < wares.Count; i++)
                DrawWarePedestal(wares[i], t + i * 0.7f, i == near, open, ink);
        }

        /// One pedestal: a floor ring, a stone plinth with a lit plate in the item's colour, a beam, and the item turning over it.
        void DrawWarePedestal(ShopWare it, float t, bool near, bool open, Rgba edge)
        {
            var c = it.At;
            var col = it.Empty ? edge.WithAlpha(0.35f) : it.Used || !open ? it.Ink.Mix(new Rgba(0.5f, 0.5f, 0.5f), 0.6f) : it.Ink;
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 2.2f), k = near ? 1.15f : 1f;
            var stone = new Rgba(0.14f, 0.12f, 0.1f, 0.95f);
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
            if (it.Build >= 0)
                BuildGlyphGl((BuildKind)it.Build, at, 0.85f * k * turn, k, col);
            else
                DrawIconGl(it.Rows, at, 40f * k, turn);
            if (it.Used || !open)
                FillRect(at.X - 22f * k, at.Y - 22f * k, 44f * k, 44f * k, new Rgba(0.05f, 0.05f, 0.07f, 0.45f));  // (dimmed: can't be had now)
        }

        /// A Black Market buildable's glyph in the world (ShopUi.cs DrawBuildGlyph's shapes), sx wide by k tall (sx: the turn).
        void BuildGlyphGl(BuildKind kind, V2 c, float sx, float k, Rgba ink)
        {
            var hot = ink.Mix(new Rgba(1f, 1f, 1f), 0.4f);
            switch (kind)
            {
                case BuildKind.Teleporter:  // two pads and the dashes between them
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Ellipse(c + new V2(s * 12f * sx, 0f), 11f * sx, 11f * k, ink.WithAlpha(0.35f));
                        Arc(c + new V2(s * 12f * sx, 0f), 11f * k, 2f, hot, 16);
                        Ellipse(c + new V2(s * 12f * sx, 0f), 4f * sx, 4f * k, hot);
                    }
                    break;
                case BuildKind.Turret:  // four feet, the plate, the head and twin barrels
                    for (int i = 0; i < 4; i++)
                        FillRect(c.X + (i % 2 == 0 ? -13f : 9f) * sx, c.Y + (i < 2 ? -13f : 9f) * k, 4f * sx, 4f * k, ink);
                    Arc(c, 11f * k, 2f, hot, 18);
                    Ellipse(c, 5f * sx, 5f * k, hot);
                    FillRect(c.X + 2f * sx, c.Y - 4f * k, 14f * sx, 2.5f * k, hot);
                    FillRect(c.X + 2f * sx, c.Y + 1.5f * k, 14f * sx, 2.5f * k, hot);
                    break;
                case BuildKind.Landmine:
                    Ellipse(c, 12f * sx, 12f * k, ink.WithAlpha(0.4f));
                    Arc(c, 12f * k, 2f, hot, 18);
                    Ellipse(c, 4f * sx, 4f * k, new Rgba(1f, 0.35f, 0.3f));
                    break;
                default:  // the slow field: rings spreading out
                    for (int r = 0; r < 3; r++)
                        Arc(c, (5f + r * 5.5f) * k, 2f, hot.WithAlpha(1f - r * 0.25f), 20);
                    break;
            }
        }

        /// DrawHud: in a pedestal room, a coin price by every pedestal, and the nearest one's name, what it does and the key.
        void DrawPedestalTags()
        {
            var me = game.Me;
            if (me == null || me.Downed || !game.ServiceRooms)
                return;
            var room = PedestalShopOf(me);
            if (!room.HasValue)
                return;
            var rm = room.Value;
            var wares = ShopWares(me, rm);
            bool open = game.ShopOpen;
            int near = NearWare(wares, me.Disp);
            for (int i = 0; i < wares.Count; i++)
            {
                var it = wares[i];
                if (it.Empty || !open || i == near)
                    continue;  // (the one you stand at: its panel says it)
                bool afford = me.Gold >= it.Price;
                bool side = it.Wall == ShopPedestals.West || it.Wall == ShopPedestals.East;
                V2 p = ToUi(it.At + (side ? new V2(it.Wall == ShopPedestals.West ? 78f : -78f, 4f) : new V2(0f, 52f)));  // (beside it, toward the middle; under it on a north or south wall)
                var r = new Rect(p.X - 30f, p.Y - 9f, 60f, 18f);
                Ui.Pill(r, new Color(0.08f, 0.06f, 0.02f, 0.85f));
                if (it.Service == 1)
                    Ui.Label(r, "SELL", 11, CrtStyle.Gold, TextAnchor.MiddleCenter, true);
                else if (it.Used)
                    Ui.Label(r, it.Service == 4 ? "FULL" : it.Service == 2 ? "FULL HP" : it.Service == 3 ? "HAVE ONE" : "MAXED", 10, Ui.Muted, TextAnchor.MiddleCenter, true);
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
            bool sideW = w.Wall == ShopPedestals.West || w.Wall == ShopPedestals.East;
            V2 q = sideW ? new V2(ToUi(rm.Mid).X, ToUi(w.At).Y - 20f) : new V2(ToUi(w.At).X, ToUi(w.At + new V2(0f, w.Wall == ShopPedestals.North ? 80f : -150f)).Y);  // (toward the middle, level with the pedestal: clear of the others and their prices)
            var panel = new Rect(q.X - 100f, q.Y, 200f, 100f);
            Ui.Fill(panel, new Color(0.05f, 0.04f, 0.02f, 0.9f));
            DkBox(panel, w.Ink.ToColor(), 0.8f);
            Ui.Label(new Rect(panel.x + 10f, panel.y + 4f, panel.width - 20f, 20f), w.Name.ToUpperInvariant(), 13, w.Ink.ToColor(), TextAnchor.MiddleCenter, true);
            Ui.Label(new Rect(panel.x + 8f, panel.y + 23f, panel.width - 16f, 48f), w.Desc ?? "", 10, Ui.Text, TextAnchor.UpperCenter, false, true);
            string key = Keybinds.KeyName(Keybinds.Get(KeyAction.Interact)).ToUpperInvariant();
            bool can = open && !w.Used && (w.Service == 1 || me.Gold >= w.Price);
            string verb = !open ? "OPENS AFTER THE WAVE" : w.Used ? (w.Service == 1 ? "NOTHING TO SELL" : w.Service == 4 ? "KIT SLOTS FULL" : w.Service == 2 ? "YOU'RE AT FULL HEALTH" : w.Service == 3 ? "YOU HAVE ONE" : w.Build >= 0 ? "STASH FULL" : "CAN'T TAKE MORE")
                : w.Service == 1 ? key + "  SELL" : me.Gold < w.Price ? "NEED " + (w.Price - me.Gold) + " MORE GOLD" : key + "  BUY  ·  " + w.Price;
            var b = new Rect(q.X - 84f, panel.yMax - 24f, 168f, 20f);
            Ui.Pill(b, can ? CrtStyle.Gold : new Color(0f, 0f, 0f, 0.65f));
            Ui.Label(b, verb, 11, can ? new Color(0.12f, 0.08f, 0.02f) : CrtStyle.Danger, TextAnchor.MiddleCenter, true);
        }

        /// Interact at a pedestal: buy it, or open the sell list. True if the key was taken.
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
            else if (w.Build >= 0)
                game.Shop(ShopAction.BuyBuild, w.Build);
            else if (w.Mod >= 0)
                game.Shop(ShopAction.BuyMod, w.Mod);
            else if (w.Service == 1)
                shopSellOpen = true;
            else
                game.Shop(w.Service == 0 ? ShopAction.Restock : w.Service == 2 ? ShopAction.Heal : w.Service == 3 ? ShopAction.Revive : w.Service == 4 ? ShopAction.RoomKit : ShopAction.Vectors);
            return true;
        }

        /// The sell list over the shop room (Sell's pedestal), while you stay in the room and the shop is open.
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
