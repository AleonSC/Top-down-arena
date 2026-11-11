using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Bazaar's shop on screen (Shop.cs): one sideways carousel (like the lobby's tab carousel) holds the shelf cards, then the
    /// services; the one in the middle is big and shows what it changes as the picker's green readout on the card (Docs/Mockups/shop-carousel.html,
    /// layout A). Below it: who's done, the Done button and giving gold to teammates. Click a side card (or use the arrows, the wheel
    /// or the arrow keys) to bring it to the middle, click the one in the middle to buy it.
    public partial class ArenaApp
    {
        int shopSel = -1;          // carousel item in the middle: a shelf card (0..count-1), then the services
        float shopTurn;            // the carousel's position, easing toward shopSel
        int shopCountSeen;         // how many shelf cards there were last frame (Add stock moves the middle to the new ones)
        int shopGiveTo;            // teammate whose Give popup is open (0 = none)
        bool shopRemoveOpen;       // choosing an upgrade to remove

        static readonly string[] ServiceIcons = { "ricochet", "axe", "regen2", "halo", "ricochet", "compass" };
        static readonly string[] ServiceNames = { "Add stock", "Remove upgrade", "Full heal", "Revive token", "Floor kit", "Vector pack" };

        void DrawShop(Player me, float t)
        {
            if (!game.ShopOpen)
            {
                shopSel = -1;
                shopGiveTo = 0;
                shopRemoveOpen = false;
                return;
            }
            bool popup = shopGiveTo != 0 || shopRemoveOpen;
            bool shopping = !me.ShopDone;
            RoundBackdrop(ShopDark);  // (RoundWindows.cs)
            const float x0 = 36f, areaW = W - 72f;

            // The room shops: upgrades are bought in the shop room, heals, revives and removals at the vendor.
            bool atShop = !game.ServiceRooms || game.InShopRoom(me), atVendor = !game.ServiceRooms || game.InVendorRoom(me);

            // Heading and wallet.
            string tag = game.ServiceRooms ? (atShop ? "SHOP ROOM  ·  UPGRADES AND STOCK" : "VENDOR ROOM  ·  HEALS, REVIVES, REMOVALS") : "BAZAAR  ·  BEFORE THE WAVE " + (game.Wave + 1) + " BOSS";
            float tagW = 30f + tag.Length * 7.5f;
            var tagRect = new Rect(W / 2f - tagW / 2f, 24f, tagW, 22f);
            Ui.Pill(tagRect, new Color(1f, 0.8f, 0.35f, 0.22f));
            Ui.Label(tagRect, tag, 11, Ui.Gold, TextAnchor.MiddleCenter, true);
            DkTitle(new Rect(0f, 46f, W, 44f), game.ServiceRooms ? (atShop ? "THE SHOP" : "THE VENDOR") : "THE BAZAAR", 34, Ui.Gold, TextAnchor.MiddleCenter);
            Ui.Label(new Rect(0f, 92f, W, 18f), game.ServiceRooms ? "Your gold and shelf stay with you between waves." : "Spend your gold before the boss. Whatever's left becomes XP.", 13, Ui.Muted, TextAnchor.MiddleCenter, true);
            var wallet = new Rect(W - 210f, 22f, 190f, 54f);
            Ui.Pill(wallet, new Color(0.16f, 0.12f, 0.03f, 0.92f));
            Ui.Icon(new Rect(wallet.x + 12f, wallet.y + 13f, 28f, 28f), PixelArt.Get("ui_coin"));
            Ui.Label(new Rect(wallet.x + 48f, wallet.y + 5f, 80f, 44f), me.Gold.ToString(), 26, Ui.Gold, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(wallet.x + 112f, wallet.y + 7f, 74f, 40f), game.ServiceRooms ? "your gold" : "your gold\nleftover → XP", 10, new Color(1f, 0.85f, 0.6f, 0.75f), TextAnchor.MiddleLeft);

            // The list: the shelf's cards, then the services.
            int count = Game.ShelfCount(me), nSvc = game.ExpandingOn ? 6 : 4, n = count + nSvc;  // (the room kit and vector pack are sold in the Expanding arena)
            if (shopSel < 0)
            {
                shopSel = 0;
                shopTurn = 0f;
            }
            if (count > shopCountSeen && shopCountSeen > 0)
                shopSel = shopCountSeen;  // Add stock: the middle moves to the first new card
            shopCountSeen = count;
            shopSel = Mathf.Clamp(shopSel, 0, n - 1);
            var ev = Event.current;
            if (ev.type == EventType.Repaint)
            {
                shopTurn += (shopSel - shopTurn) * (fxReduced ? 1f : 1f - Mathf.Exp(-Time.unscaledDeltaTime * 12f));
                if (Mathf.Abs(shopSel - shopTurn) < 0.004f)
                    shopTurn = shopSel;
            }

            // Tabs: jump to the shelf or to the services.
            const float railY = 158f, cardW = 250f, cardH = 372f;
            float tabY = 118f;
            bool onShelf = shopSel < count;
            if (!popup)
            {
                if (LobbyKit.QuietTab(new Rect(W / 2f - 150f, tabY, 148f, 26f), "SHELF  (" + count + ")", onShelf))
                    shopSel = 0;
                if (LobbyKit.QuietTab(new Rect(W / 2f + 2f, tabY, 148f, 26f), "SERVICES  (" + nSvc + ")", !onShelf))
                    shopSel = count;
            }
            else
            {
                LobbyKit.QuietTab(new Rect(W / 2f - 150f, tabY, 148f, 26f), "SHELF  (" + count + ")", onShelf, false);
                LobbyKit.QuietTab(new Rect(W / 2f + 2f, tabY, 148f, 26f), "SERVICES  (" + nSvc + ")", !onShelf, false);
            }

            // The wheel: far cards first, so the middle one is on top. Each is drawn at full size, scaled about its own centre.
            var order = new List<int>();
            for (int i = 0; i < n; i++)
                order.Add(i);
            order.Sort((a, b) => Mathf.Abs(b - shopTurn).CompareTo(Mathf.Abs(a - shopTurn)));
            float oldAlpha = Ui.Alpha;
            int click = -1;
            foreach (int i in order)
            {
                float d = i - shopTurn, ad = Mathf.Abs(d);
                float s = 1f - 0.2f * Mathf.Min(ad, 1f) - 0.05f * Mathf.Max(0f, Mathf.Min(ad, 2f) - 1f);
                float cx = W / 2f + Mathf.Sign(d) * (ad <= 1f ? 232f * ad : 232f + 150f * (ad - 1f));
                float a = Mathf.Clamp01((3.2f - ad) / 1.4f) * (1f - 0.25f * Mathf.Min(ad, 1f));
                if (a <= 0.02f)
                    continue;
                var r = new Rect(cx - cardW / 2f, railY, cardW, cardH);
                bool mid = i == shopSel;
                var oldM = GUI.matrix;
                Ui.Alpha = oldAlpha * a;
                GUIUtility.ScaleAroundPivot(new Vector2(s, s), r.center);
                if (i < count)
                {
                    int u = me.Shelf[i];
                    if (u < 0)
                    {
                        DkBox(r, new Color(1f, 1f, 1f, 0.2f), 0.2f);
                        Ui.Label(r, "Sold out", 13, Ui.Muted, TextAnchor.MiddleCenter, true);
                    }
                    else
                        DrawShopCard(me, i, Upgrades.All[u], r, Game.Sold(me, i), mid, t, shopping && atShop);
                }
                else
                    DrawShopService(me, i - count, r, mid, t, shopping, i - count == 0 ? atShop : atVendor);
                GUI.matrix = oldM;
                Ui.Alpha = oldAlpha;
                var hit = new Rect(r.center.x - r.width * s / 2f, r.center.y - r.height * s / 2f, r.width * s, r.height * s);
                if (!popup && a > 0.3f && Ui.HitArea(hit))
                    click = i;
            }
            if (click >= 0)
            {
                if (click != shopSel)
                    shopSel = click;
                else if (shopping)
                    ShopUse(me, click, count, atShop, atVendor);
            }

            // Arrows, the mouse wheel and the arrow keys turn it.
            if (!popup)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    var ab = new Rect(side < 0 ? 18f : W - 58f, railY, 40f, cardH);
                    bool dis = shopSel + side < 0 || shopSel + side >= n;
                    if (LobbyKit.Key(ab, "", false, fxReduced) && !dis)
                        shopSel += side;
                    Chevron(ab.center, side, CrtKit.SoftText(CrtUi.Ink, !dis && ab.Contains(ev.mousePosition)));
                }
                if (ev.type == EventType.ScrollWheel && ev.mousePosition.y > railY - 20f && ev.mousePosition.y < railY + cardH + 20f)
                {
                    shopSel = Mathf.Clamp(shopSel + (ev.delta.y > 0f ? 1 : -1), 0, n - 1);
                    ev.Use();
                }
                else if (ev.type == EventType.KeyDown && (ev.keyCode == KeyCode.LeftArrow || ev.keyCode == KeyCode.RightArrow))
                {
                    shopSel = Mathf.Clamp(shopSel + (ev.keyCode == KeyCode.LeftArrow ? -1 : 1), 0, n - 1);
                    ev.Use();
                }
            }

            // Where you are in the list: a pip each, the services in gold after a gap.
            float px = W / 2f - (n * 24f + 12f) / 2f;
            for (int i = 0; i < n; i++)
            {
                var pr = new Rect(px + i * 24f + (i >= count ? 12f : 0f), railY + cardH + 14f, 18f, 4f);
                bool svc = i >= count;
                Ui.Fill(pr, i == shopSel ? (svc ? Ui.Gold : Color.white) : (svc ? new Color(1f, 0.8f, 0.35f, 0.3f) : new Color(CrtUi.Ink.r, CrtUi.Ink.g, CrtUi.Ink.b, 0.3f)));
                if (!popup && Ui.HitArea(new Rect(pr.x - 3f, pr.y - 6f, pr.width + 6f, pr.height + 12f)))
                    shopSel = i;
            }

            // Who's done, the Done button and your teammates.
            float by = 584f;
            int done = 0;
            var waiting = new List<string>();
            foreach (var p in game.Players.Values)
            {
                if (p.ShopDone)
                    done++;
                else
                    waiting.Add(p.Id == game.MyId ? "you" : p.Name);
            }
            string status = done + " of " + game.Players.Count + " done shopping" + (waiting.Count > 0 ? "  ·  waiting for " + string.Join(", ", waiting.ToArray()) : "");
            Ui.Label(new Rect(x0, by, areaW, 18f), status, 13, Ui.Muted, TextAnchor.MiddleLeft, true);
            if (session.IsHost && game.ShopT >= Game.ShopHostCloseAfter && waiting.Count > 0)
            {
                if (!popup && DkKey(new Rect(x0 + areaW - 245f, by - 4f, 240f, 26f), "Close the shop for everyone", Ui.Danger, false, 13))
                    game.Shop(ShopAction.Close);
            }
            else if (!game.ServiceRooms)  // (the room shops keep your gold: nothing becomes XP)
                Ui.Label(new Rect(x0, by, areaW, 18f), "Leftover gold becomes XP when everyone is done", 13, Ui.Muted, TextAnchor.MiddleRight, true);
            var doneRect = new Rect(x0, by + 30f, 320f, 48f);
            string doneText = me.ShopDone ? "Done ✓   (click to keep shopping)" : game.ServiceRooms ? "Done shopping" : "Done shopping  ·  " + me.Gold + " gold → +" + Mathf.RoundToInt(me.Gold * Game.GoldToXp) + " XP";
            if (DkKey(doneRect, doneText, Ui.Primary, me.ShopDone, 15) && !popup)
                game.Shop(ShopAction.Done, me.ShopDone ? 0 : 1);
            DrawShopTeam(me, new Rect(doneRect.xMax + 16f, doneRect.y + 6f, areaW - doneRect.width - 30f, 36f), popup);

            // Gift notices.
            for (int i = 0; i < game.ShopNotices.Count; i++)
            {
                var nt = game.ShopNotices[i];
                float w = Ui.TextWidth(nt.Text, 14, true) + 50f;
                var r = new Rect(W - 20f - w, 82f + i * 30f, w, 26f);  // under the wallet
                Ui.Pill(r, new Color(0.16f, 0.12f, 0.03f, 0.95f * Mathf.Clamp01(nt.Life * 2f)));
                Ui.Icon(new Rect(r.x + 10f, r.y + 5f, 16f, 16f), PixelArt.Get("ui_coin"), Mathf.Clamp01(nt.Life * 2f));
                Ui.Label(new Rect(r.x + 32f, r.y, w - 36f, r.height), nt.Text, 14, new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, Mathf.Clamp01(nt.Life * 2f)), TextAnchor.MiddleLeft, true);
            }

            if (shopRemoveOpen)
                DrawRemoveUpgrade(me);
        }

        /// The middle item was clicked: buy the card, or use the service.
        void ShopUse(Player me, int item, int count, bool atShop, bool atVendor)
        {
            if (item < count)
            {
                if (me.Shelf[item] >= 0 && !Game.Sold(me, item) && atShop)
                    game.Shop(ShopAction.Buy, item);
                return;
            }
            int k = item - count;
            int price = ServicePrice(me, k, out bool used);
            if (used || me.Gold < price || !(k == 0 ? atShop : atVendor))
                return;
            if (k == 1)
                shopRemoveOpen = true;
            else
                game.Shop(k == 0 ? ShopAction.Restock : k == 2 ? ShopAction.Heal : k == 4 ? ShopAction.RoomKit : k == 5 ? ShopAction.Vectors : ShopAction.Revive);
        }

        /// A service's price, and whether it can't be used right now.
        int ServicePrice(Player me, int k, out bool used)
        {
            switch (k)
            {
                case 0: used = me.Restocks >= Game.StockMax; return Game.RestockPrice(me);
                case 1: used = me.Taken.Count == 0; return Game.RemovePrice;
                case 2: used = me.Hp >= me.MaxHp || me.Downed; return Game.HealPrice;
                case 4: used = game.RoomTokens.Count >= Expansion.MaxTokens; return Game.RoomKitPrice;
                case 5: used = false; return Game.VectorPackPrice;
                default: used = me.ReviveToken; return Game.RevivePrice;
            }
        }

        /// One shelf card: rarity ribbon, icon, name, description, price, and (in the middle) the green readout of what it changes.
        void DrawShopCard(Player me, int index, UpgradeDef u, Rect r, bool sold, bool selected, float t, bool canBuy)
        {
            Color rc = ToColor(Upgrades.RarityColor(u.Rarity));
            int price = Game.Price(me.Shelf[index]);
            bool afford = me.Gold >= price;
            float dim = sold || !afford ? 0.5f : 1f;
            // The picker's holographic screen (HudPicker.DrawHolo) in the rarity colour, with the same glow and outline.
            Ui.Glow(r, rc, selected ? 0.55f + 0.1f * Mathf.Sin(t * 4f) : 0.2f * dim);
            DrawHolo(r, rc, t);
            if (dim < 1f)  // sold or too dear: the screen dims
                Ui.Fill(r, new Color(0.01f, 0.012f, 0.03f, 0.5f));
            CrtEdge(r, selected ? 2f : 1f, selected ? Color.Lerp(rc, Color.white, 0.55f) : new Color(rc.r, rc.g, rc.b, 0.5f * dim + 0.1f));
            string rarity = Upgrades.RarityName(u.Rarity);
            float rw = 24f + rarity.Length * 7.5f;
            var ribbon = new Rect(r.center.x - rw / 2f, r.y + 12f, rw, 20f);
            Ui.Pill(ribbon, new Color(rc.r, rc.g, rc.b, 0.85f * dim));
            Ui.Label(ribbon, rarity, 10, new Color(0.06f, 0.06f, 0.1f), TextAnchor.MiddleCenter, true);
            var tile = new Rect(r.center.x - 40f, r.y + 42f, 80f, 80f);
            Ui.Pill(new Rect(tile.x - 3f, tile.y - 3f, tile.width + 6f, tile.height + 6f), new Color(rc.r, rc.g, rc.b, selected ? 0.35f : 0.18f));
            Ui.Pill(tile, new Color(rc.r * 0.22f, rc.g * 0.22f, rc.b * 0.22f, 1f));
            Ui.Icon(new Rect(tile.x + 10f, tile.y + 10f + (selected ? 2f * Mathf.Sin(t * 3f) : 0f), 60f, 60f), PixelArt.ForUpgrade(u.Name), dim);
            Ui.Label(new Rect(r.x + 8f, r.y + 130f, r.width - 16f, 26f), u.Name, u.Name.Length > 16 ? 15 : 18, new Color(1f, 1f, 1f, dim), TextAnchor.MiddleCenter, true);
            Ui.Fill(new Rect(r.x + 36f, r.y + 160f, r.width - 72f, 1f), new Color(rc.r, rc.g, rc.b, 0.4f));
            string desc = Upgrades.DescFor(u, me.Weapon);
            var body = new Rect(r.x + 14f, r.y + 166f, r.width - 28f, selected ? 78f : 130f);
            if (u.Grants != AbilityId.None)  // an ability: what mastering it adds too (LobbyUi.cs)
                DrawDescAndMastery(body, desc, u.Grants, desc.Length > 70 ? 11 : 13, new Color(Ui.Text.r, Ui.Text.g, Ui.Text.b, dim), true);
            else
                Ui.Label(body, desc, desc.Length > 70 ? 11 : 13, new Color(Ui.Text.r, Ui.Text.g, Ui.Text.b, dim), TextAnchor.UpperCenter, false, true);
            if (selected && !sold)
            {
                var parts = ChangeParts(me, me.Shelf[index], 0, false);
                if (parts != null)
                    DrawPickStrip(new Rect(r.x + 10f, r.yMax - 50f, r.width - 20f, 0f), 13, true, parts, Upgrades.Count(me.Taken, me.Shelf[index]));
            }
            if (sold)
            {
                var stamp = new Rect(r.center.x - 60f, r.center.y - 18f, 120f, 36f);
                Ui.Pill(stamp, new Color(0.05f, 0.2f, 0.08f, 0.92f));
                Ui.Label(stamp, "BOUGHT", 19, Ui.Good, TextAnchor.MiddleCenter, true);
            }
            else if (selected)
                DrawPriceTag(new Rect(r.center.x - 55f, r.yMax - 38f, 110f, 28f), price, afford, afford && canBuy ? "Buy · " : "");
            else
                DrawPriceTag(new Rect(r.center.x - 34f, r.yMax - 38f, 68f, 26f), price, afford);
        }

        /// One service in the same frame as the cards, in gold: Add stock, Remove upgrade, Full heal, Revive token, Floor kit, Vector pack.
        void DrawShopService(Player me, int k, Rect r, bool selected, float t, bool shopping, bool allowed)
        {
            int price = ServicePrice(me, k, out bool used);
            bool afford = me.Gold >= price && shopping && !used && allowed;
            Color rc = Ui.Gold;
            float dim = afford || used ? 1f : 0.5f;
            string desc;
            List<string> parts;
            switch (k)
            {
                case 0:
                    desc = used ? "The shelf is full" : "Two more items join the shelf. Each add costs more.";
                    parts = used ? null : new List<string> { "Cards  " + Game.ShelfCount(me) + " → " + (Game.ShelfCount(me) + Game.StockSize), "Next add  " + price + " → " + (price + 25) };
                    break;
                case 1:
                    desc = me.Taken.Count == 0 ? "You have nothing to remove" : "Drop one you regret. Abilities it gave you go too.";
                    parts = me.Taken.Count == 0 ? null : new List<string> { "Upgrades  " + me.Taken.Count + " → " + (me.Taken.Count - 1) };
                    break;
                case 2:
                    desc = me.Hp >= me.MaxHp ? "You're at full health" : "Back to full health.";
                    parts = me.Hp >= me.MaxHp || me.Downed ? null : new List<string> { "HP  " + Mathf.CeilToInt(me.Hp) + " → " + Mathf.CeilToInt(me.MaxHp) };
                    break;
                case 4:
                    desc = used ? "Kit slots are full" : "Turn a room into a hazard floor.";
                    parts = used ? null : new List<string> { "Kit slots  " + game.RoomTokens.Count + "/" + Expansion.MaxTokens + " → " + (game.RoomTokens.Count + 1) + "/" + Expansion.MaxTokens };
                    break;
                case 5:
                    desc = Game.VectorPack + " more vectors to build with. Can go past your cap.";
                    parts = new List<string> { "Vectors  " + me.Vectors + " → " + (me.Vectors + Game.VectorPack) };
                    break;
                default:
                    desc = me.ReviveToken ? "You have one" : "Get back up once if you go down.";
                    parts = me.ReviveToken ? null : new List<string> { "Revive token  0 → 1" };
                    break;
            }
            Ui.Glow(r, rc, selected ? 0.5f + 0.1f * Mathf.Sin(t * 4f) : 0.14f * dim);
            DkBox(r, rc, selected ? 0.9f : 0.4f);
            Ui.Fill(r, new Color(0.1f, 0.08f, 0.02f, 0.55f));
            CrtEdge(r, selected ? 2f : 1f, selected ? Color.Lerp(rc, Color.white, 0.5f) : new Color(rc.r, rc.g, rc.b, 0.45f));
            var ribbon = new Rect(r.center.x - 40f, r.y + 12f, 80f, 20f);
            Ui.Pill(ribbon, new Color(rc.r, rc.g, rc.b, 0.85f * dim));
            Ui.Label(ribbon, "Service", 10, new Color(0.06f, 0.06f, 0.1f), TextAnchor.MiddleCenter, true);
            var tile = new Rect(r.center.x - 40f, r.y + 42f, 80f, 80f);
            Ui.Pill(new Rect(tile.x - 3f, tile.y - 3f, tile.width + 6f, tile.height + 6f), new Color(rc.r, rc.g, rc.b, selected ? 0.35f : 0.18f));
            Ui.Pill(tile, new Color(rc.r * 0.22f, rc.g * 0.22f, rc.b * 0.22f, 1f));
            Ui.Icon(new Rect(tile.x + 10f, tile.y + 10f + (selected ? 2f * Mathf.Sin(t * 3f) : 0f), 60f, 60f), PixelArt.Get(ServiceIcons[k]), used ? 0.4f : dim);
            Ui.Label(new Rect(r.x + 8f, r.y + 130f, r.width - 16f, 26f), ServiceNames[k], 18, new Color(1f, 1f, 1f, used ? 0.45f : dim), TextAnchor.MiddleCenter, true);
            Ui.Fill(new Rect(r.x + 36f, r.y + 160f, r.width - 72f, 1f), new Color(rc.r, rc.g, rc.b, 0.4f));
            Ui.Label(new Rect(r.x + 14f, r.y + 166f, r.width - 28f, selected ? 78f : 130f), desc, 13, new Color(Ui.Text.r, Ui.Text.g, Ui.Text.b, used ? 0.6f : dim), TextAnchor.UpperCenter, false, true);
            if (selected && parts != null)
                DrawPickStrip(new Rect(r.x + 10f, r.yMax - 50f, r.width - 20f, 0f), 13, true, parts, -1, false);
            if (used && k >= 2)
                Ui.Icon(new Rect(r.center.x - 10f, r.yMax - 38f, 20f, 20f), PixelArt.Get("ui_check"), Ui.Good);
            else if (selected)
                DrawPriceTag(new Rect(r.center.x - 55f, r.yMax - 38f, 110f, 28f), price, me.Gold >= price, afford ? "Buy · " : "");
            else
                DrawPriceTag(new Rect(r.center.x - 34f, r.yMax - 38f, 68f, 26f), price, me.Gold >= price);
        }

        /// Above the wave panel, with the path chip: your Bazaar gold, while you're on the Bazaar or have some.
        bool DrawGoldChip(float x, float y)
        {
            var me = game.Me;
            if (me == null || (me.Gold <= 0 && game.CurrentPath != PathId.Bazaar))
                return false;
            string text = me.Gold + " gold";
            var chip = new Rect(x, y, 34f + Ui.TextWidth(text, 11, true) + 10f, 20f);
            Ui.Pill(chip, new Color(0f, 0f, 0f, 0.55f));
            Ui.Fill(new Rect(chip.x + 3f, chip.y + 4f, 3f, chip.height - 8f), Ui.Gold);
            Ui.Icon(new Rect(chip.x + 12f, chip.y + 3f, 14f, 14f), PixelArt.Get("ui_coin"));
            Ui.Label(new Rect(chip.x + 30f, chip.y, chip.width - 30f, chip.height), text, 11, Ui.Gold, TextAnchor.MiddleLeft, true);
            return true;
        }

        /// A gold price: a coin and the amount (red when you can't afford it).
        void DrawPriceTag(Rect r, int price, bool afford, string prefix = "")
        {
            Ui.Pill(r, afford ? new Color(1f, 0.8f, 0.35f, prefix.Length > 0 ? 0.32f : 0.16f) : new Color(1f, 0.35f, 0.3f, 0.14f));
            Ui.Icon(new Rect(r.x + 8f, r.y + r.height / 2f - 7f, 14f, 14f), PixelArt.Get("ui_coin"), afford ? 1f : 0.6f);
            Ui.Label(new Rect(r.x + 26f, r.y, r.width - 30f, r.height), prefix + price, 13, afford ? Ui.Gold : new Color(1f, 0.55f, 0.5f), TextAnchor.MiddleLeft, true);
        }

        /// Your teammates' gold and whether they're done, each with a Give button (and its popup).
        void DrawShopTeam(Player me, Rect area, bool popup)
        {
            float x = area.x;
            Player giving = null;
            Rect givingChip = default(Rect);
            foreach (var p in game.Players.Values)
            {
                if (p.Id == game.MyId)
                    continue;
                // [role] Name  Shopping  (coin) 112  [Give]
                string state = p.ShopDone ? "Done" : "Shopping", gold = p.Gold.ToString();
                float nw = Ui.TextWidth(p.Name, 12, true), sw2 = Ui.TextWidth(state, 11), gw = Ui.TextWidth(gold, 12, true);
                float w = 32f + nw + 8f + sw2 + 12f + 18f + gw + 10f + 48f;
                var chip = new Rect(x, area.y, w, area.height);
                DkHud(chip, p.ShopDone ? Ui.Good : (Color?)null);
                Ui.Icon(new Rect(chip.x + 8f, chip.y + 9f, 18f, 18f), PixelArt.Get(RoleIcons[(int)p.Role]), ToColor(p.Def.Color));
                float cx = chip.x + 32f;
                Ui.Label(new Rect(cx, chip.y, nw + 4f, chip.height), p.Name, 12, Color.white, TextAnchor.MiddleLeft, true);
                cx += nw + 8f;
                Ui.Label(new Rect(cx, chip.y, sw2 + 4f, chip.height), state, 11, p.ShopDone ? Ui.Good : Ui.Muted, TextAnchor.MiddleLeft);
                cx += sw2 + 12f;
                Ui.Icon(new Rect(cx, chip.y + 11f, 14f, 14f), PixelArt.Get("ui_coin"));
                Ui.Label(new Rect(cx + 18f, chip.y, gw + 4f, chip.height), gold, 12, Ui.Gold, TextAnchor.MiddleLeft, true);
                if ((!popup || shopGiveTo == p.Id) && DkKey(new Rect(chip.xMax - 48f, chip.y + 7f, 40f, 22f), "Give", Ui.GoldButton, false, 11))
                    shopGiveTo = shopGiveTo == p.Id ? 0 : p.Id;
                if (shopGiveTo == p.Id)
                {
                    giving = p;
                    givingChip = chip;
                }
                x += w + 8f;
            }
            if (giving == null)
            {
                shopGiveTo = 0;
                return;
            }
            // The Give popup above the teammate's chip.
            var pop = new Rect(Mathf.Min(givingChip.x, W - 290f), givingChip.y - 104f, 270f, 96f);
            DkBox(pop, Ui.Gold, 0.9f);
            Ui.Label(new Rect(pop.x + 12f, pop.y + 8f, pop.width - 24f, 18f), "Give " + giving.Name + " gold", 13, Color.white, TextAnchor.MiddleLeft, true);
            int[] amounts = { 10, 50, 100, me.Gold };
            for (int k = 0; k < amounts.Length; k++)
            {
                bool all = k == amounts.Length - 1;
                var b = new Rect(pop.x + 12f + k * 62f, pop.y + 32f, all ? 60f : 56f, 28f);
                bool ok = amounts[k] > 0 && me.Gold >= amounts[k];
                bool was = GUI.enabled;
                GUI.enabled = ok;
                if (DkKey(b, all ? "All " + me.Gold : amounts[k].ToString(), Ui.GoldButton, false, 12))
                {
                    game.Shop(ShopAction.Give, giving.Id, amounts[k]);
                    shopGiveTo = 0;
                }
                GUI.enabled = was;
            }
            Ui.Label(new Rect(pop.x + 12f, pop.y + 66f, pop.width - 24f, 24f), giving.ShopDone ? giving.Name + " is done: this reopens their shop." : "It goes straight into their wallet.", 11,
                giving.ShopDone ? Ui.Gold : Ui.Muted, TextAnchor.MiddleLeft, false, true);
            if (Event.current.type == EventType.MouseDown && !pop.Contains(Event.current.mousePosition) && !givingChip.Contains(Event.current.mousePosition))
                shopGiveTo = 0;  // a click elsewhere closes it
        }

        /// Remove an upgrade: everything you've taken, as buttons; one click removes it (75 gold).
        void DrawRemoveUpgrade(Player me)
        {
            var seen = new List<int>();
            foreach (int u in me.Taken)
                if (!seen.Contains(u))
                    seen.Add(u);
            const int perRow = 4;
            float bw = 180f, bh = 34f, gap = 8f;
            int rows = (seen.Count + perRow - 1) / perRow;
            float pw = perRow * bw + (perRow - 1) * gap + 40f, ph = 110f + rows * (bh + gap);
            var r = new Rect(W / 2f - pw / 2f, Mathf.Max(30f, H / 2f - ph / 2f), pw, ph);
            Ui.Fill(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, 0.5f));
            DkBox(r, Ui.Gold, 0.9f);
            Ui.Label(new Rect(r.x + 20f, r.y + 14f, r.width - 40f, 24f), "Remove an upgrade  ·  " + Game.RemovePrice + " gold", 17, Color.white, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(r.x + 20f, r.y + 38f, r.width - 40f, 18f), "Takes away one stack. Abilities it gave you go too.", 12, Ui.Muted);
            for (int i = 0; i < seen.Count; i++)
            {
                var u = Upgrades.All[seen[i]];
                int have = Upgrades.Count(me.Taken, seen[i]);
                var b = new Rect(r.x + 20f + (i % perRow) * (bw + gap), r.y + 64f + (i / perRow) * (bh + gap), bw, bh);
                var rc = ToColor(Upgrades.RarityColor(u.Rarity));
                bool over = b.Contains(Event.current.mousePosition);
                DkHud(b, over ? rc : (Color?)null);
                Ui.Icon(new Rect(b.x + 6f, b.y + 5f, 24f, 24f), PixelArt.ForUpgrade(u.Name));
                Ui.Label(new Rect(b.x + 36f, b.y, b.width - 40f, b.height), u.Name + (have > 1 ? "  ×" + have : ""), 12, over ? Color.white : Ui.Text, TextAnchor.MiddleLeft, true);
                if (Ui.HitArea(b))
                {
                    game.Shop(ShopAction.Remove, seen[i]);
                    shopRemoveOpen = false;
                }
            }
            if (DkKey(new Rect(r.xMax - 120f, r.yMax - 40f, 100f, 28f), "Cancel", Ui.Neutral))
                shopRemoveOpen = false;
        }
    }
}
