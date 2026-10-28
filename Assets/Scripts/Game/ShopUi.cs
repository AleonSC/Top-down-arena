using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Bazaar's shop on screen (Shop.cs): your stats on the left, a shelf of six cards, services, who's done,
    /// and giving gold to teammates. Click a card to select it (the stats preview it), click again to buy.
    public partial class ArenaApp
    {
        int shopSel = -1;          // shelf card selected
        int shopGiveTo;            // teammate whose Give popup is open (0 = none)
        bool shopRemoveOpen;       // choosing an upgrade to remove

        static readonly string[] ServiceIcons = { "ricochet", "axe", "regen2", "halo" };

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
            Ui.Fill(new Rect(0, 0, W, H), new Color(0.01f, 0.012f, 0.03f, 0.8f));

            // Which card the stats preview: the one under the mouse, else the selected one.
            const float x0 = StatsSidebarW + 20f, areaW = W - StatsSidebarW - 40f;
            const float cw = 150f, ch = 270f, gap = 12f, top = 140f;
            int hover = -1;
            for (int i = 0; i < Game.ShelfSize; i++)
                if (!popup && new Rect(x0 + 5f + i * (cw + gap), top, cw, ch).Contains(Event.current.mousePosition))
                    hover = i;
            int preview = popup ? -1 : hover >= 0 ? hover : shopSel;
            DrawStatPanel(me, StatsSidebar(me), preview >= 0 && me.Shelf[preview] >= 0 && !Game.Sold(me, preview) ? me.Shelf[preview] : -1);

            // Heading and wallet.
            string tag = "BAZAAR  ·  BEFORE THE WAVE " + (game.Wave + 1) + " BOSS";
            float tagW = 30f + tag.Length * 7.5f;
            var tagRect = new Rect(x0 + areaW / 2f - tagW / 2f, 28f, tagW, 22f);
            Ui.Pill(tagRect, new Color(1f, 0.8f, 0.35f, 0.22f));
            Ui.Label(tagRect, tag, 11, Ui.Gold, TextAnchor.MiddleCenter, true);
            Ui.Fancy(new Rect(x0, 52f, areaW, 44f), "THE BAZAAR", 34, new Color(1f, 0.97f, 0.85f), Ui.Gold, TextAnchor.MiddleCenter, Ui.Gold);
            Ui.Label(new Rect(x0, 98f, areaW, 18f), "Spend your gold before the boss. Whatever's left becomes XP.", 13, Ui.Muted, TextAnchor.MiddleCenter, true);
            var wallet = new Rect(W - 210f, 24f, 190f, 56f);
            Ui.Pill(wallet, new Color(0.16f, 0.12f, 0.03f, 0.92f));
            Ui.Icon(new Rect(wallet.x + 12f, wallet.y + 14f, 28f, 28f), PixelArt.Get("ui_coin"));
            Ui.Label(new Rect(wallet.x + 48f, wallet.y + 6f, 80f, 44f), me.Gold.ToString(), 26, Ui.Gold, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(wallet.x + 112f, wallet.y + 8f, 74f, 40f), "your gold\nleftover → XP", 10, new Color(1f, 0.85f, 0.6f, 0.75f), TextAnchor.MiddleLeft);

            // The shelf.
            for (int i = 0; i < Game.ShelfSize; i++)
            {
                var r = new Rect(x0 + 5f + i * (cw + gap), top, cw, ch);
                int u = me.Shelf[i];
                if (u < 0)
                {
                    Ui.Card(r, new Color(1f, 1f, 1f, 0.2f), 0.2f);
                    Ui.Label(r, "Sold out", 13, Ui.Muted, TextAnchor.MiddleCenter, true);
                    continue;
                }
                bool sold = Game.Sold(me, i);
                if (DrawShopCard(me, i, Upgrades.All[u], r, sold, shopping && shopSel == i && !sold, hover == i, t) && !popup && shopping && !sold)
                {
                    if (shopSel == i)
                        game.Shop(ShopAction.Buy, i);
                    else
                        shopSel = i;
                }
            }

            // Services.
            float sy = top + ch + 18f;
            Ui.Label(new Rect(x0 + 5f, sy, 200f, 16f), "SERVICES", 11, Ui.Muted, TextAnchor.MiddleLeft, true);
            float sw = (areaW - 10f - 3 * 12f) / 4f;
            for (int k = 0; k < 4; k++)
            {
                var r = new Rect(x0 + 5f + k * (sw + 12f), sy + 20f, sw, 66f);
                string name, desc;
                int price;
                bool used = false;
                switch (k)
                {
                    case 0: name = "Restock"; desc = "Six new items"; price = Game.RestockPrice(me); break;
                    case 1: name = "Remove upgrade"; desc = "Drop one you regret"; price = Game.RemovePrice; used = me.Taken.Count == 0; break;
                    case 2: name = "Full heal"; desc = me.Hp >= me.MaxHp ? "You're at full health" : "Back to full HP"; price = Game.HealPrice; used = me.Hp >= me.MaxHp || me.Downed; break;
                    default: name = "Revive token"; desc = me.ReviveToken ? "You have one" : "Revive yourself once"; price = Game.RevivePrice; used = me.ReviveToken; break;
                }
                bool afford = me.Gold >= price && shopping && !used;
                bool over = !popup && afford && r.Contains(Event.current.mousePosition);
                Ui.HudPanel(r, over ? Ui.Gold : (Color?)null);
                float a = afford || used ? 1f : 0.5f;
                Ui.Icon(new Rect(r.x + 12f, r.y + 15f, 36f, 36f), PixelArt.Get(ServiceIcons[k]), used ? 0.4f : a);
                Ui.Label(new Rect(r.x + 56f, r.y + 12f, r.width - 126f, 20f), name, 13, new Color(1f, 1f, 1f, used ? 0.45f : a), TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(r.x + 56f, r.y + 32f, r.width - 126f, 28f), desc, 11, new Color(Ui.Muted.r, Ui.Muted.g, Ui.Muted.b, used ? 0.6f : a), TextAnchor.UpperLeft, false, true);
                if (used && k >= 2)
                    Ui.Icon(new Rect(r.xMax - 36f, r.y + 23f, 20f, 20f), PixelArt.Get("ui_check"), Ui.Good);
                else
                    DrawPriceTag(new Rect(r.xMax - 68f, r.y + 20f, 58f, 26f), price, me.Gold >= price);
                if (!popup && afford && GUI.Button(r, GUIContent.none, GUIStyle.none))
                {
                    if (k == 1)
                        shopRemoveOpen = true;
                    else
                        game.Shop(k == 0 ? ShopAction.Restock : k == 2 ? ShopAction.Heal : ShopAction.Revive);
                    if (k == 0)
                        shopSel = -1;
                }
            }

            // Who's done, the Done button and your teammates.
            float by = sy + 104f;
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
            Ui.Label(new Rect(x0 + 5f, by, areaW - 10f, 18f), status, 13, Ui.Muted, TextAnchor.MiddleLeft, true);
            if (session.IsHost && game.ShopT >= Game.ShopHostCloseAfter && waiting.Count > 0)
            {
                if (!popup && Ui.SmallButton(new Rect(x0 + areaW - 245f, by - 4f, 240f, 26f), "Close the shop for everyone", Ui.Danger))
                    game.Shop(ShopAction.Close);
            }
            else
                Ui.Label(new Rect(x0 + 5f, by, areaW - 10f, 18f), "Leftover gold becomes XP when everyone is done", 13, Ui.Muted, TextAnchor.MiddleRight, true);
            var doneRect = new Rect(x0 + 5f, by + 28f, 320f, 48f);
            string doneText = me.ShopDone ? "Done ✓   (click to keep shopping)" : "Done shopping  ·  " + me.Gold + " gold → +" + Mathf.RoundToInt(me.Gold * Game.GoldToXp) + " XP";
            if (Ui.Button(doneRect, doneText, me.ShopDone ? new Color(0.25f, 0.7f, 0.35f) : Ui.Primary) && !popup)
                game.Shop(ShopAction.Done, me.ShopDone ? 0 : 1);
            DrawShopTeam(me, new Rect(doneRect.xMax + 16f, doneRect.y + 6f, areaW - doneRect.width - 30f, 36f), popup);

            // Gift notices.
            for (int i = 0; i < game.ShopNotices.Count; i++)
            {
                var n = game.ShopNotices[i];
                float w = Ui.TextWidth(n.Text, 14, true) + 50f;
                var r = new Rect(W - 20f - w, 86f + i * 30f, w, 26f);  // under the wallet
                Ui.Pill(r, new Color(0.16f, 0.12f, 0.03f, 0.95f * Mathf.Clamp01(n.Life * 2f)));
                Ui.Icon(new Rect(r.x + 10f, r.y + 5f, 16f, 16f), PixelArt.Get("ui_coin"), Mathf.Clamp01(n.Life * 2f));
                Ui.Label(new Rect(r.x + 32f, r.y, w - 36f, r.height), n.Text, 14, new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, Mathf.Clamp01(n.Life * 2f)), TextAnchor.MiddleLeft, true);
            }

            if (shopRemoveOpen)
                DrawRemoveUpgrade(me);
        }

        /// One shelf card: rarity ribbon, icon, name, description and price. Returns true when clicked.
        bool DrawShopCard(Player me, int index, UpgradeDef u, Rect r, bool sold, bool selected, bool hover, float t)
        {
            Color rc = ToColor(Upgrades.RarityColor(u.Rarity));
            int price = Game.Price(me.Shelf[index]);
            bool afford = me.Gold >= price;
            float dim = sold || !afford ? 0.5f : 1f;
            if (selected)
                Ui.Pill(new Rect(r.x - 5f, r.y - 5f, r.width + 10f, r.height + 10f), new Color(rc.r, rc.g, rc.b, 0.18f + 0.06f * Mathf.Sin(t * 4f)));
            Ui.Card(r, rc, selected ? 1f : hover ? 0.75f : 0.45f * dim);
            Ui.Pill(new Rect(r.x + 3f, r.y + 3f, r.width - 6f, 110f), new Color(rc.r, rc.g, rc.b, 0.08f));
            string rarity = Upgrades.RarityName(u.Rarity);
            float rw = 20f + rarity.Length * 7f;
            var ribbon = new Rect(r.center.x - rw / 2f, r.y + 10f, rw, 18f);
            Ui.Pill(ribbon, new Color(rc.r, rc.g, rc.b, 0.85f * dim));
            Ui.Label(ribbon, rarity, 9, new Color(0.06f, 0.06f, 0.1f), TextAnchor.MiddleCenter, true);
            var tile = new Rect(r.center.x - 38f, r.y + 36f, 76f, 76f);
            Ui.Pill(new Rect(tile.x - 3f, tile.y - 3f, tile.width + 6f, tile.height + 6f), new Color(rc.r, rc.g, rc.b, selected ? 0.35f : 0.18f));
            Ui.Pill(tile, new Color(rc.r * 0.22f, rc.g * 0.22f, rc.b * 0.22f, 1f));
            Ui.Icon(new Rect(tile.x + 10f, tile.y + 10f + (selected ? 2f * Mathf.Sin(t * 3f) : 0f), 56f, 56f), PixelArt.ForUpgrade(u.Name), dim);
            Ui.Label(new Rect(r.x + 6f, r.y + 118f, r.width - 12f, 24f), u.Name, u.Name.Length > 15 ? 13 : 16, new Color(1f, 1f, 1f, dim), TextAnchor.MiddleCenter, true);
            Ui.Fill(new Rect(r.x + 32f, r.y + 146f, r.width - 64f, 1f), new Color(rc.r, rc.g, rc.b, 0.4f));
            string desc = Upgrades.DescFor(u, me.Weapon);
            Ui.Label(new Rect(r.x + 10f, r.y + 152f, r.width - 20f, 74f), desc, desc.Length > 70 ? 10 : 12, new Color(Ui.Text.r, Ui.Text.g, Ui.Text.b, dim), TextAnchor.UpperCenter, false, true);
            if (sold)
            {
                var stamp = new Rect(r.center.x - 56f, r.center.y - 18f, 112f, 34f);
                Ui.Pill(stamp, new Color(0.05f, 0.2f, 0.08f, 0.92f));
                Ui.Label(stamp, "BOUGHT", 18, Ui.Good, TextAnchor.MiddleCenter, true);
            }
            else if (selected)
                DrawPriceTag(new Rect(r.center.x - 50f, r.yMax - 38f, 100f, 26f), price, afford, afford ? "Buy · " : "");
            else
                DrawPriceTag(new Rect(r.center.x - 34f, r.yMax - 38f, 68f, 26f), price, afford);
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
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
                Ui.HudPanel(chip, p.ShopDone ? Ui.Good : (Color?)null);
                Ui.Icon(new Rect(chip.x + 8f, chip.y + 9f, 18f, 18f), PixelArt.Get(RoleIcons[(int)p.Role]), ToColor(p.Def.Color));
                float cx = chip.x + 32f;
                Ui.Label(new Rect(cx, chip.y, nw + 4f, chip.height), p.Name, 12, Color.white, TextAnchor.MiddleLeft, true);
                cx += nw + 8f;
                Ui.Label(new Rect(cx, chip.y, sw2 + 4f, chip.height), state, 11, p.ShopDone ? Ui.Good : Ui.Muted, TextAnchor.MiddleLeft);
                cx += sw2 + 12f;
                Ui.Icon(new Rect(cx, chip.y + 11f, 14f, 14f), PixelArt.Get("ui_coin"));
                Ui.Label(new Rect(cx + 18f, chip.y, gw + 4f, chip.height), gold, 12, Ui.Gold, TextAnchor.MiddleLeft, true);
                if ((!popup || shopGiveTo == p.Id) && Ui.SmallButton(new Rect(chip.xMax - 48f, chip.y + 7f, 40f, 22f), "Give", Ui.GoldButton, 11))
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
            Ui.Card(pop, Ui.Gold, 0.9f);
            Ui.Label(new Rect(pop.x + 12f, pop.y + 8f, pop.width - 24f, 18f), "Give " + giving.Name + " gold", 13, Color.white, TextAnchor.MiddleLeft, true);
            int[] amounts = { 10, 50, 100, me.Gold };
            for (int k = 0; k < amounts.Length; k++)
            {
                bool all = k == amounts.Length - 1;
                var b = new Rect(pop.x + 12f + k * 62f, pop.y + 32f, all ? 60f : 56f, 28f);
                bool ok = amounts[k] > 0 && me.Gold >= amounts[k];
                bool was = GUI.enabled;
                GUI.enabled = ok;
                if (Ui.SmallButton(b, all ? "All " + me.Gold : amounts[k].ToString(), Ui.GoldButton, 12))
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
            var r = new Rect(StatsSidebarW + (W - StatsSidebarW) / 2f - pw / 2f, Mathf.Max(30f, H / 2f - ph / 2f), pw, ph);
            Ui.Fill(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, 0.5f));
            Ui.Card(r, Ui.Gold, 0.9f);
            Ui.Label(new Rect(r.x + 20f, r.y + 14f, r.width - 40f, 24f), "Remove an upgrade  ·  " + Game.RemovePrice + " gold", 17, Color.white, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(r.x + 20f, r.y + 38f, r.width - 40f, 18f), "Takes away one stack. Abilities it gave you go too.", 12, Ui.Muted);
            for (int i = 0; i < seen.Count; i++)
            {
                var u = Upgrades.All[seen[i]];
                int have = Upgrades.Count(me.Taken, seen[i]);
                var b = new Rect(r.x + 20f + (i % perRow) * (bw + gap), r.y + 64f + (i / perRow) * (bh + gap), bw, bh);
                var rc = ToColor(Upgrades.RarityColor(u.Rarity));
                bool over = b.Contains(Event.current.mousePosition);
                Ui.HudPanel(b, over ? rc : (Color?)null);
                Ui.Icon(new Rect(b.x + 6f, b.y + 5f, 24f, 24f), PixelArt.ForUpgrade(u.Name));
                Ui.Label(new Rect(b.x + 36f, b.y, b.width - 40f, b.height), u.Name + (have > 1 ? "  ×" + have : ""), 12, over ? Color.white : Ui.Text, TextAnchor.MiddleLeft, true);
                if (GUI.Button(b, GUIContent.none, GUIStyle.none))
                {
                    game.Shop(ShopAction.Remove, seen[i]);
                    shopRemoveOpen = false;
                }
            }
            if (Ui.SmallButton(new Rect(r.xMax - 120f, r.yMax - 40f, 100f, 28f), "Cancel"))
                shopRemoveOpen = false;
        }
    }
}
