using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Tab screen's Inventory page (RunStatsUi.cs, statsTab 3): everything you carry, as hologram panels (TabHolo.cs). Your wallet
    /// (gold, vectors, a revive token, the gadget in your hand), your relic, your hand of room cards (Pieces.cs), the Black Market buys in
    /// your stash (Market.cs) and the team's room kits (Expansion.RoomToken). Only drawn on your own screen; it changes nothing.
    /// Preview: `STATSTAB=3 Tools/Preview/shot.sh x tab 23 12.2 3`.
    public partial class ArenaApp
    {
        int invView;  // whose inventory the page shows: a teammate's player id, or 0 for yours
        bool invFromPause;  // the screen was opened from the pause menu: look only (nothing to use while paused)

        void DrawInventory(Player me)
        {
            float x = HoloPageX, w = HoloPageR - HoloPageX;
            var head = new Rect(x, HoloTop, w, HoloHeadH);
            if (me == null)
                return;
            Player p;
            if (invView == 0 || invView == me.Id || !game.Players.TryGetValue(invView, out p))
            {
                invView = 0;
                p = me;
            }
            bool mine = p == me;
            bool canUse = mine && !invFromPause && game.ExpandOpen && !me.Downed;  // (cards and buildables go down in a build round)
            int cards = Game.HandCount(p), stash = 0;
            foreach (var n in p.Stash) stash += n;
            string sub = (mine ? "" : p.Name + "'s · ") + cards + " card" + (cards == 1 ? "" : "s") + " · " + stash + " in " + (mine ? "your" : "their") + " stash · " + game.RoomTokens.Count + " team room kit" + (game.RoomTokens.Count == 1 ? "" : "s");
            string hint = !mine ? "Looking only: teammates use their own" : canUse ? "Click a card or buildable to use it" : invFromPause ? "Use them from the Tab screen in a build round" : "Usable in the break, while a build round is open";
            HoloPanel(head, holoPageAt, () =>
            {
                HoloTitle(head.x + 18f, head.y + 14f, "INVENTORY", sub.ToUpperInvariant(), mine ? HoloDim : Color.Lerp(ToColor(p.Def.Color), Color.white, 0.35f));
                Ui.Label(new Rect(head.x + 18f, head.yMax - 20f, 600f, 14f), hint.ToUpperInvariant(), 10, canUse ? CrtStyle.Gold : HoloDim, TextAnchor.MiddleLeft, true);
                DrawInvWho(me, head);
            });
            float top = HoloTop + HoloHeadH + HoloGap;
            const float leftW = 300f, upperH = 270f;
            var wallet = new Rect(x, top, leftW, upperH);
            var relic = new Rect(x, wallet.yMax + HoloGap, leftW, HoloBottom - wallet.yMax - HoloGap);
            float rx = wallet.xMax + HoloGap, rw = HoloPageR - rx;
            var hand = new Rect(rx, top, rw, upperH);
            float lowY = hand.yMax + HoloGap, lowH = HoloBottom - lowY, half = (rw - HoloGap) / 2f;
            var builds = new Rect(rx, lowY, half, lowH);
            var kits = new Rect(builds.xMax + HoloGap, lowY, half, lowH);
            HoloPanel(wallet, holoPageAt, () => DrawInvWallet(p, wallet, mine));
            HoloPanel(relic, holoPageAt, () => DrawInvRelic(p, relic));
            HoloPanel(hand, holoPageAt, () => DrawInvHand(p, hand, canUse));
            HoloPanel(builds, holoPageAt, () => DrawInvStash(p, builds, canUse));
            HoloPanel(kits, holoPageAt, () => DrawInvKits(kits));
        }

        /// Whose inventory: YOU and each teammate, keys on the right of the header (a teammate's key in their class colour).
        void DrawInvWho(Player me, Rect head)
        {
            var ids = new List<int> { me.Id };
            foreach (var q in game.Players.Values)
                if (q.Id != me.Id) ids.Add(q.Id);
            if (ids.Count < 2)
                return;
            const float kw = 132f, gap = 6f;
            float kx = head.xMax - 18f - ids.Count * kw - (ids.Count - 1) * gap;
            for (int i = 0; i < ids.Count; i++)
            {
                var q = game.Players[ids[i]];
                bool on = (invView == 0 ? me.Id : invView) == q.Id;
                var r = new Rect(kx + i * (kw + gap), head.y + 18f, kw, 30f);
                if (LobbyKit.Key(r, q.Id == me.Id ? "YOU" : LobbyKit.Truncate(q.Name.ToUpperInvariant(), 13, kw - 16f), on, HoloCalm, size: 13, minSize: 10) && !on)
                {
                    invView = q.Id == me.Id ? 0 : q.Id;
                    holoPageAt = Time.unscaledTime;  // (the panels switch on again for the new view)
                }
                if (q.Id != me.Id)
                    Ui.Fill(new Rect(r.x + 6f, r.yMax - 4f, r.width - 12f, 2f), ToColor(q.Def.Color));
            }
        }

        /// Why an item can't be used from here right now.
        string InvUseWhy(Player p) => p != game.Me ? "Theirs: only they can use it." : invFromPause ? "From the pause menu you can only look: use it from the Tab screen." : !game.ExpandOpen ? "Usable in the break, while a build round is open." : "You're down.";

        /// Use a room card or buildable from the inventory: the Tab screen gives way (until Tab is let go) and the build screen opens on it.
        void InvOpenBuild()
        {
            tabSuppress = true;
            runStatsOpen = false;
            expOpen = true;
            expModify = false;
            expDrawing = true;
            expDragging = expHeld = false;
            expA = expB = V2.Zero;
            expLinkMode = expGateMode = expTrapMode = false;
        }

        void UseInvCard(int slot)
        {
            InvOpenBuild();
            expGridSlot = slot;  // (the piece is picked on the build screen, GridRoomsUi.cs)
            expGridTurn = 0;
        }

        void UseInvBuild(int kind)
        {
            InvOpenBuild();
            if (kind == (int)BuildKind.Teleporter)
                expLinkMode = true;
            else
            {
                expTrapMode = true;
                expTrapKind = kind;
            }
        }

        /// The Tab screen is open on the Inventory page (drawn this frame or the last): the mouse wheel is its lists', not the camera's.
        bool InventoryUp => statsTab == 3 && Time.frameCount - statsLastFrame <= 1;

        readonly Dictionary<string, int> invScroll = new Dictionary<string, int>();  // each list's first row shown

        /// A list that can hold more rows than fit: the mouse wheel over it scrolls a row at a time, and a thin bar down its right edge
        /// shows where you are (only when it overflows). Returns the first row to draw.
        int InvScroll(string key, Rect area, int total, int shown)
        {
            int max = Mathf.Max(0, total - shown), s;
            invScroll.TryGetValue(key, out s);
            var ev = Event.current;
            if (max > 0 && ev.type == EventType.ScrollWheel && area.Contains(ev.mousePosition))
            {
                s += ev.delta.y > 0f ? 1 : -1;
                ev.Use();
            }
            s = Mathf.Clamp(s, 0, max);
            invScroll[key] = s;
            if (max > 0)
            {
                var ink = CrtUi.Ink;
                var track = new Rect(area.xMax - 4f, area.y, 4f, area.height);
                Ui.Fill(track, new Color(ink.r, ink.g, ink.b, 0.15f));
                float th = Mathf.Max(18f, area.height * shown / total), ty = area.y + (area.height - th) * s / max;
                Ui.Fill(new Rect(track.x, ty, track.width, th), new Color(ink.r, ink.g, ink.b, 0.8f));
                if (s < max)
                    Ui.Label(new Rect(area.x, area.yMax - 2f, area.width - 10f, 14f), "▼ " + (max - s) + " MORE  ·  SCROLL", 9, HoloDim, TextAnchor.MiddleRight, true);
            }
            return s;
        }

        /// Gold and vectors as big tiles, then the revive token and the gadget.
        void DrawInvWallet(Player me, Rect r, bool mine)
        {
            LobbyKit.Heading(r.x + 12f, r.y + 10f, r.width - 24f, "Wallet", null, HoloCalm);
            float tw = (r.width - 36f) / 2f;
            HoloTile(new Rect(r.x + 12f, r.y + 46f, tw, 64f), me.Gold.ToString(), "Gold");
            bool expanding = game.ExpandingOn;
            HoloTile(new Rect(r.x + 24f + tw, r.y + 46f, tw, 64f), expanding ? me.Vectors + " / " + me.VectorCap : "—", "Vectors",new Color(0.55f, 0.9f, 1f));
            var rows = new string[,]
            {
                { "Revive token", me.ReviveToken ? "1" : "0" },
                { "Gadget", me.Gadget == GadgetId.None ? "—" : "MAGNET " + Mathf.CeilToInt(me.GadgetLeft) + "s" },
                { "Rerolls", mine ? me.Rerolls.ToString() : "—" },  // (not sent for teammates)
            };
            HoloRows(r.x + 12f, r.y + 134f, r.width - 24f, rows);
        }

        /// The relic slot: its name and what it does, or an empty slot.
        void DrawInvRelic(Player me, Rect r)
        {
            LobbyKit.Heading(r.x + 12f, r.y + 10f, r.width - 24f, "Relic", null, HoloCalm);
            var slot = new Rect(r.x + 12f, r.y + 48f, 56f, 56f);
            var ink = CrtUi.Ink;
            bool has = me.Relic != RelicId.None;
            Ui.Fill(slot, new Color(ink.r, ink.g, ink.b, has ? 0.16f : 0.05f));
            CrtKit.Frame(slot, 1f, has ? CrtStyle.Gold : new Color(ink.r, ink.g, ink.b, 0.35f));
            if (has)
            {
                Ui.Dot(new Rect(slot.center.x - 14f, slot.center.y - 14f, 28f, 28f), CrtStyle.Gold, true);
                Ui.Dot(new Rect(slot.center.x - 6f, slot.center.y - 6f, 12f, 12f), CrtStyle.Gold);
                Ui.Label(new Rect(slot.xMax + 12f, slot.y + 2f, r.width - 100f, 22f), Relics.Name(me.Relic), 16, Color.white, TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(slot.xMax + 12f, slot.y + 26f, r.width - 100f, 16f), "HELD", 10, CrtStyle.Gold, TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(r.x + 14f, slot.yMax + 12f, r.width - 28f, r.yMax - slot.yMax - 20f), Relics.Desc(me.Relic), 12, HoloDesc, TextAnchor.UpperLeft, false, true);
            }
            else
            {
                Ui.Label(new Rect(slot.xMax + 12f, slot.y, r.width - 100f, slot.height), "Empty slot", 14, HoloDim, TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(r.x + 14f, slot.yMax + 12f, r.width - 28f, 40f), "Walk up to a relic node to take its relic. Your old one is left in the node.", 12, HoloDesc, TextAnchor.UpperLeft, false, true);
            }
        }

        /// A hand of room cards: six slots, each drawn as its room with its name; empty slots dashed. canUse: a click plays the card
        /// (the build screen opens on it, UseInvCard).
        void DrawInvHand(Player me, Rect r, bool canUse)
        {
            canUse &= game.ExpandingOn;
            LobbyKit.Heading(r.x + 12f, r.y + 10f, r.width - 24f, game.ExpandingOn ? "Pieces" : "Room cards", game.ExpandingOn ? Game.HandCount(me) + " / " + Expansion.HandMax : "off", HoloCalm);
            var ink = CrtUi.Ink;
            const int cols = 3;
            const float minH = 96f;
            float gap = 10f, avail = r.height - 58f;
            int rows = (Expansion.HandMax + cols - 1) / cols, shown = Mathf.Clamp(Mathf.FloorToInt((avail + gap) / (minH + gap)), 1, rows);
            var list = new Rect(r.x + 12f, r.y + 46f, r.width - 24f, avail);
            int first = InvScroll("hand", list, rows, shown);
            float cw = (list.width - (rows > shown ? 10f : 0f) - gap * (cols - 1)) / cols, ch = (avail - gap * (shown - 1)) / shown;
            for (int i = first * cols; i < Mathf.Min(Expansion.HandMax, (first + shown) * cols); i++)
            {
                int row = i / cols - first;
                var c = new Rect(list.x + (i % cols) * (cw + gap), list.y + row * (ch + gap), cw, ch);
                var card = (Piece)me.Hand[i];
                if (card == Piece.None)
                {
                    for (float dx = 0f; dx < c.width; dx += 10f)  // a dashed outline: an empty slot
                    {
                        Ui.Fill(new Rect(c.x + dx, c.y, Mathf.Min(5f, c.width - dx), 1f), new Color(ink.r, ink.g, ink.b, 0.3f));
                        Ui.Fill(new Rect(c.x + dx, c.yMax - 1f, Mathf.Min(5f, c.width - dx), 1f), new Color(ink.r, ink.g, ink.b, 0.3f));
                    }
                    for (float dy = 0f; dy < c.height; dy += 10f)
                    {
                        Ui.Fill(new Rect(c.x, c.y + dy, 1f, Mathf.Min(5f, c.height - dy)), new Color(ink.r, ink.g, ink.b, 0.3f));
                        Ui.Fill(new Rect(c.xMax - 1f, c.y + dy, 1f, Mathf.Min(5f, c.height - dy)), new Color(ink.r, ink.g, ink.b, 0.3f));
                    }
                    continue;
                }
                var ci = ToColor(CardColor(card));
                bool hot = canUse && c.Contains(Event.current.mousePosition);
                Ui.Fill(c, new Color(ci.r, ci.g, ci.b, hot ? 0.2f : 0.08f));
                CrtKit.Frame(c, hot ? 2f : 1f, hot ? Color.Lerp(ci, Color.white, 0.5f) : new Color(ci.r, ci.g, ci.b, 0.6f));
                DrawCardGlyph(card, new Rect(c.x + 10f, c.y + 8f, c.width - 20f, c.height - 40f), ci);
                Ui.Label(new Rect(c.x, c.yMax - 28f, c.width, 18f), hot ? "PLAY  ›" : Expansion.CardName(card).ToUpperInvariant(), 13, hot ? CrtStyle.Gold : Color.white, TextAnchor.MiddleCenter, true);
                if (TipHover(c))
                {
                    TipStart(c, Expansion.CardName(card), ci);
                    TipAdd(Expansion.CardBlurb(card), 12, Ui.Text);
                    TipAdd(canUse ? "Click to play it: the build screen opens with it picked." : InvUseWhy(me), 11, canUse ? CrtStyle.Gold : Ui.Muted);
                }
                if (canUse && Ui.HitArea(c))
                    UseInvCard(i);
            }
            if (!game.ExpandingOn)
            {
                Ui.Fill(new Rect(r.x + 12f, r.y + 46f, r.width - 24f, r.height - 58f), new Color(0f, 0f, 0f, 0.45f));
                Ui.Label(new Rect(r.x, r.center.y - 10f, r.width, 20f), "Pieces are for the Expanding arena", 13, HoloDim, TextAnchor.MiddleCenter, true);
            }
        }

        /// The Black Market buys waiting in a stash: each buildable's mark and how many are carried. canUse: a click on one you have
        /// opens the build screen in its tool (Teleporter or Trap, UseInvBuild).
        void DrawInvStash(Player me, Rect r, bool canUse)
        {
            LobbyKit.Heading(r.x + 12f, r.y + 10f, r.width - 24f, "Buildables", me == game.Me ? "your stash" : "their stash", HoloCalm);
            var owned = new List<int>();
            for (int q = 0; q < Market.Count; q++)
                if (me.Stash[q] > 0) owned.Add(q);
            if (owned.Count == 0)
            {
                Ui.Label(new Rect(r.x + 14f, r.y + 50f, r.width - 28f, 40f), "None held. Teleporters and traps are sold at the Black Market.", 12, HoloDesc, TextAnchor.UpperLeft, false, true);
                return;
            }
            float avail = r.height - 58f;
            int n = owned.Count, shown = Mathf.Clamp(Mathf.FloorToInt(avail / 40f), 1, n);
            var list = new Rect(r.x + 12f, r.y + 46f, r.width - 24f, avail);
            int first = InvScroll("stash", list, n, shown);
            float rowH = Mathf.Min(avail / shown, 48f), rowW = list.width - (n > shown ? 10f : 0f);
            for (int i = first; i < first + shown; i++)
            {
                int k = owned[i], have = me.Stash[k];
                var row = new Rect(list.x, list.y + (i - first) * rowH, rowW, rowH - 4f);
                var ink = CrtUi.Ink;
                bool usable = canUse, hot = usable && row.Contains(Event.current.mousePosition);
                if (i % 2 == 0 || hot)
                    Ui.Fill(row, new Color(ink.r, ink.g, ink.b, hot ? 0.2f : 0.07f));
                if (hot)
                    CrtKit.Frame(row, 1f, CrtStyle.Gold);
                if (usable && Ui.HitArea(row))
                    UseInvBuild(k);
                if (TipHover(row))
                {
                    TipStart(row, Market.Names[k], ToColor(BlackMarketInk));
                    TipAdd(Market.Blurbs[k], 12, Ui.Text);
                    TipAdd(usable ? "Click to place it: the build screen opens on its tool." : InvUseWhy(me), 11, usable ? CrtStyle.Gold : Ui.Muted);
                }
                var m = GUI.matrix;
                var icon = new Rect(row.x + 4f, row.y + (row.height - 36f) / 2f, 36f, 36f);
                GUIUtility.ScaleAroundPivot(new Vector2(0.42f, 0.42f), icon.center);
                DrawBuildGlyph((BuildKind)k, new Rect(icon.center.x - 40f, icon.center.y - 40f, 80f, 80f), 1f);
                GUI.matrix = m;
                Ui.Label(new Rect(icon.xMax + 10f, row.y, row.width - 110f, row.height), Market.Names[k], 13, HoloBody, TextAnchor.MiddleLeft, false);
                CrtUi.Title(new Rect(row.x, row.y, row.width - 8f, row.height), hot ? "PLACE ›" : have + " / " + Market.StashMax, 20, hot ? CrtStyle.Gold : Color.white, TextAnchor.MiddleRight, true);
            }
        }

        /// The team's room kits (shared: anyone spends one on a built room from the build screen).
        void DrawInvKits(Rect r)
        {
            LobbyKit.Heading(r.x + 12f, r.y + 10f, r.width - 24f, "Room kits", "team · " + game.RoomTokens.Count + " / " + Expansion.MaxTokens, HoloCalm);
            if (game.RoomTokens.Count == 0)
            {
                Ui.Label(new Rect(r.x + 14f, r.y + 50f, r.width - 28f, 40f), "None held. Bosses give a floor kit, won challenges a shop kit, and the Black Market sells them.", 12, HoloDesc, TextAnchor.UpperLeft, false, true);
                return;
            }
            const float rowH = 34f;
            float avail = r.height - 58f;
            int n = game.RoomTokens.Count, shown = Mathf.Clamp(Mathf.FloorToInt((avail + 4f) / (rowH + 4f)), 1, n);
            var list = new Rect(r.x + 12f, r.y + 46f, r.width - 24f, avail);
            int first = InvScroll("kits", list, n, shown);
            for (int i = first; i < first + shown; i++)
            {
                var t = game.RoomTokens[i];
                var row = new Rect(list.x, list.y + (i - first) * (rowH + 4f), list.width - (n > shown ? 10f : 0f), rowH);
                var ki = ToColor(t.Kind == RoomKind.Shop ? ShopInk : Paths.Get(t.Floor).Accent);
                Ui.Fill(row, new Color(ki.r, ki.g, ki.b, 0.1f));
                Ui.Fill(new Rect(row.x, row.y, 4f, row.height), ki);
                Ui.Label(new Rect(row.x + 14f, row.y, row.width - 20f, row.height), t.Name, 14, Color.white, TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(row.x, row.y, row.width - 10f, row.height), t.Kind == RoomKind.Shop ? "SHOP" : "FLOOR", 10, ki, TextAnchor.MiddleRight, true);
            }
        }
    }
}
