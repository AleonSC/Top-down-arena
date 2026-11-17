using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Tab screen's Inventory page (RunStatsUi.cs, statsTab 3): everything you carry, as hologram panels (TabHolo.cs). Your wallet
    /// (gold, vectors, a revive token, the gadget in your hand), your relic, your hand of room cards (RoomCards.cs), the Black Market buys in
    /// your stash (Market.cs) and the team's room kits (Expansion.RoomToken). Only drawn on your own screen; it changes nothing.
    /// Preview: `STATSTAB=3 Tools/Preview/shot.sh x tab 23 12.2 3`.
    public partial class ArenaApp
    {
        void DrawInventory(Player me)
        {
            float x = HoloPageX, w = HoloPageR - HoloPageX;
            var head = new Rect(x, HoloTop, w, HoloHeadH);
            int cards = me != null ? Game.HandCount(me) : 0, stash = 0;
            if (me != null)
                foreach (var n in me.Stash) stash += n;
            HoloPanel(head, holoPageAt, () => HoloTitle(head.x + 18f, head.y + 14f, "INVENTORY",
                (cards + " room card" + (cards == 1 ? "" : "s") + " · " + stash + " in your stash · " + game.RoomTokens.Count + " team room kit" + (game.RoomTokens.Count == 1 ? "" : "s")).ToUpperInvariant(), HoloDim));
            if (me == null)
                return;
            float top = HoloTop + HoloHeadH + HoloGap;
            const float leftW = 300f, upperH = 270f;
            var wallet = new Rect(x, top, leftW, upperH);
            var relic = new Rect(x, wallet.yMax + HoloGap, leftW, HoloBottom - wallet.yMax - HoloGap);
            float rx = wallet.xMax + HoloGap, rw = HoloPageR - rx;
            var hand = new Rect(rx, top, rw, upperH);
            float lowY = hand.yMax + HoloGap, lowH = HoloBottom - lowY, half = (rw - HoloGap) / 2f;
            var builds = new Rect(rx, lowY, half, lowH);
            var kits = new Rect(builds.xMax + HoloGap, lowY, half, lowH);
            HoloPanel(wallet, holoPageAt, () => DrawInvWallet(me, wallet));
            HoloPanel(relic, holoPageAt, () => DrawInvRelic(me, relic));
            HoloPanel(hand, holoPageAt, () => DrawInvHand(me, hand));
            HoloPanel(builds, holoPageAt, () => DrawInvStash(me, builds));
            HoloPanel(kits, holoPageAt, () => DrawInvKits(kits));
        }

        /// Gold and vectors as big tiles (the vectors with a bar to the cap), then the revive token and the gadget.
        void DrawInvWallet(Player me, Rect r)
        {
            LobbyKit.Heading(r.x + 12f, r.y + 10f, r.width - 24f, "Wallet", null, HoloCalm);
            float tw = (r.width - 36f) / 2f;
            HoloTile(new Rect(r.x + 12f, r.y + 46f, tw, 64f), me.Gold.ToString(), "Gold");
            bool expanding = game.ExpandingOn;
            HoloTile(new Rect(r.x + 24f + tw, r.y + 46f, tw, 64f), expanding ? me.Vectors.ToString() : "—", expanding ? "Vectors / " + me.VectorCap : "Vectors", new Color(0.55f, 0.9f, 1f));
            if (expanding)
                HoloBar(new Rect(r.x + 24f + tw, r.y + 114f, tw, 6f), me.VectorCap > 0 ? (float)me.Vectors / me.VectorCap : 0f, new Color(0.45f, 0.85f, 1f));
            var rows = new string[,]
            {
                { "Revive token", me.ReviveToken ? "1" : "0" },
                { "Gadget", me.Gadget == GadgetId.None ? "—" : "MAGNET " + Mathf.CeilToInt(me.GadgetLeft) + "s" },
                { "Rerolls", me.Rerolls.ToString() },
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
                Ui.Label(new Rect(r.x + 14f, slot.yMax + 12f, r.width - 28f, 40f), "Relic nodes fill everyone's slot. A new one replaces the old.", 12, HoloDesc, TextAnchor.UpperLeft, false, true);
            }
        }

        /// Your hand of room cards: six slots, each drawn as its room with its name; empty slots dashed.
        void DrawInvHand(Player me, Rect r)
        {
            LobbyKit.Heading(r.x + 12f, r.y + 10f, r.width - 24f, "Room cards", game.RoomCardsOn ? Game.HandCount(me) + " / " + Expansion.HandMax : "off", HoloCalm);
            var ink = CrtUi.Ink;
            const int cols = 3;
            float gap = 10f, cw = (r.width - 24f - gap * (cols - 1)) / cols, ch = (r.height - 58f - gap) / 2f;
            for (int i = 0; i < Expansion.HandMax; i++)
            {
                var c = new Rect(r.x + 12f + (i % cols) * (cw + gap), r.y + 46f + (i / cols) * (ch + gap), cw, ch);
                var card = (RoomCard)me.Hand[i];
                if (card == RoomCard.None)
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
                var ci = ToColor(card == RoomCard.MiniBoss ? GateInk : CardInk);
                Ui.Fill(c, new Color(ci.r, ci.g, ci.b, 0.08f));
                CrtKit.Frame(c, 1f, new Color(ci.r, ci.g, ci.b, 0.6f));
                DrawCardGlyph(card, new Rect(c.x + 10f, c.y + 8f, c.width - 20f, c.height - 40f), ci);
                Ui.Label(new Rect(c.x, c.yMax - 28f, c.width, 18f), Expansion.CardName(card).ToUpperInvariant(), 13, Color.white, TextAnchor.MiddleCenter, true);
                if (TipHover(c))
                {
                    TipStart(c, Expansion.CardName(card), ci);
                    TipAdd(Expansion.CardBlurb(card), 12, Ui.Text);
                }
            }
            if (!game.RoomCardsOn)
            {
                Ui.Fill(new Rect(r.x + 12f, r.y + 46f, r.width - 24f, r.height - 58f), new Color(0f, 0f, 0f, 0.45f));
                Ui.Label(new Rect(r.x, r.center.y - 10f, r.width, 20f), game.ExpandingOn ? "This run draws its rooms (Room cards are off)" : "Room cards are for the Expanding arena", 13, HoloDim, TextAnchor.MiddleCenter, true);
            }
        }

        /// The Black Market buys waiting in your stash: each buildable's mark and how many you carry.
        void DrawInvStash(Player me, Rect r)
        {
            LobbyKit.Heading(r.x + 12f, r.y + 10f, r.width - 24f, "Buildables", "your stash", HoloCalm);
            float rowH = (r.height - 58f) / Market.Count;
            for (int k = 0; k < Market.Count; k++)
            {
                var row = new Rect(r.x + 12f, r.y + 46f + k * rowH, r.width - 24f, rowH - 4f);
                int have = me.Stash[k];
                var ink = CrtUi.Ink;
                if (k % 2 == 0)
                    Ui.Fill(row, new Color(ink.r, ink.g, ink.b, 0.07f));
                var m = GUI.matrix;
                var icon = new Rect(row.x + 4f, row.y + (row.height - 36f) / 2f, 36f, 36f);
                GUIUtility.ScaleAroundPivot(new Vector2(0.42f, 0.42f), icon.center);
                DrawBuildGlyph((BuildKind)k, new Rect(icon.center.x - 40f, icon.center.y - 40f, 80f, 80f), have > 0 ? 1f : 0.3f);
                GUI.matrix = m;
                Ui.Label(new Rect(icon.xMax + 10f, row.y, row.width - 110f, row.height), Market.Names[k], 13, have > 0 ? HoloBody : HoloDim, TextAnchor.MiddleLeft, false);
                CrtUi.Title(new Rect(row.x, row.y, row.width - 8f, row.height), have + " / " + Market.StashMax, 20, have > 0 ? Color.white : HoloDim, TextAnchor.MiddleRight, true);
            }
        }

        /// The team's room kits (shared: anyone spends one on a built room from the build screen).
        void DrawInvKits(Rect r)
        {
            LobbyKit.Heading(r.x + 12f, r.y + 10f, r.width - 24f, "Room kits", "team · " + game.RoomTokens.Count + " / " + Expansion.MaxTokens, HoloCalm);
            if (game.RoomTokens.Count == 0)
            {
                Ui.Label(new Rect(r.x + 14f, r.y + 50f, r.width - 28f, 40f), "None held. Bosses give a floor kit, won challenges a shop kit, and the vendor sells them.", 12, HoloDesc, TextAnchor.UpperLeft, false, true);
                return;
            }
            float rowH = 34f;
            for (int i = 0; i < game.RoomTokens.Count; i++)
            {
                var t = game.RoomTokens[i];
                var row = new Rect(r.x + 12f, r.y + 46f + i * (rowH + 4f), r.width - 24f, rowH);
                var ki = ToColor(t.Kind == RoomKind.Shop ? ShopInk : Paths.Get(t.Floor).Accent);
                Ui.Fill(row, new Color(ki.r, ki.g, ki.b, 0.1f));
                Ui.Fill(new Rect(row.x, row.y, 4f, row.height), ki);
                Ui.Label(new Rect(row.x + 14f, row.y, row.width - 20f, row.height), t.Name, 14, Color.white, TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(row.x, row.y, row.width - 10f, row.height), t.Kind == RoomKind.Shop ? "SHOP" : "FLOOR", 10, ki, TextAnchor.MiddleRight, true);
            }
        }
    }
}
