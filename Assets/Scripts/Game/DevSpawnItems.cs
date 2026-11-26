using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The F1 dock's Spawn-a-gadget-chest-or-relic sheet (devSheet 7, opened from the PARTY drawer): for the chosen player, click a gadget
    /// to drop a full one in front of them (their own copy: walk onto it), a chest kind to put one in front of them (Game.DevSpawnChest), or a relic to put it in their relic slot at once. Host only,
    /// in a match; both mark the run as a dev run (Game.DevSpawnGadget / Game.DevSetRelic in Core/DevTools.cs).
    public partial class ArenaApp
    {
        int devItemPlayer;        // whose the gadget or relic is (player id)
        string devItemNote = "";

        void DrawDockGadgetsRelics(Rect r)
        {
            bool host = session.IsHost;
            var players = new List<Player>(game.Players.Values);
            players.Sort((a, b) => a.Id.CompareTo(b.Id));
            Player who = players.Find(q => q.Id == devItemPlayer) ?? game.Me ?? (players.Count > 0 ? players[0] : null);
            if (who == null)
                return;
            devItemPlayer = who.Id;
            string whoName = who.Id == game.MyId ? "you" : who.Name;
            var ev = Event.current;
            string hoverNote = "";
            var col = DkStack.Column(r, 8f);

            // Whose: one key per player (or just their name).
            var top = col.Next(30f);
            Ui.Label(new Rect(top.x, top.y, 46f, 30f), "FOR", 10, CrtStyle.Desc, TextAnchor.MiddleLeft, true);
            float px = top.x + 46f;
            if (players.Count > 1)
                for (int i = 0; i < players.Count; i++)
                {
                    var q = players[i];
                    float pw = Mathf.Min(170f, (top.xMax - px - 6f * (players.Count - 1)) / players.Count);
                    if (DkKey(new Rect(px, top.y, pw, 30f), q.Name + (q.Id == game.MyId ? " (you)" : ""), q.Def.Color.ToColor(), q.Id == who.Id, 12))
                        devItemPlayer = q.Id;
                    px += pw + 6f;
                }
            else
                Ui.Label(new Rect(px, top.y, top.xMax - px, 30f), who.Name + "  ·  " + who.Def.Name, 13, CrtStyle.Body, TextAnchor.MiddleLeft, true);

            // Gadgets: three cards, glyph, name, time; the line under says what they carry now.
            var gh = col.Next(16f);
            int bagUsed = 0;
            foreach (var b in who.BagKind)
                if (b != GadgetId.None)
                    bagUsed++;
            Ui.Label(gh, "GADGETS", 10, CrtStyle.Head, TextAnchor.MiddleLeft, true);
            Ui.Label(gh, "in hand: " + (who.Gadget == GadgetId.None ? "nothing" : Game.GadgetName(who.Gadget)) + "  ·  bag " + bagUsed + "/" + who.BagKind.Length,
                10, CrtStyle.Desc, TextAnchor.MiddleRight);
            int kinds = (int)GadgetId.None, perRow = (kinds + 1) / 2;  // two rows of cells
            var gc = new Rect[kinds];
            for (int row = 0; row * perRow < kinds; row++)
            {
                var cells = DkStack.Cells(col.Next(48f), perRow, 8f);
                for (int k = 0; k < perRow && row * perRow + k < kinds; k++)
                    gc[row * perRow + k] = cells[k];
            }
            for (int i = 0; i < gc.Length; i++)
            {
                var g = (GadgetId)i;
                var cell = gc[i];
                Color ink = GadgetInk(g);
                bool hover = host && cell.Contains(ev.mousePosition);
                Ui.Fill(cell, ink.WithAlpha(hover ? 0.16f : 0.05f));
                CrtKit.Frame(cell, 1f, ink.WithAlpha(hover ? 0.85f : 0.3f));
                DrawGadgetGlyph(g, new Vector2(cell.x + 16f, cell.center.y), 12f, host ? ink : ink.WithAlpha(0.45f));
                Ui.Label(new Rect(cell.x + 31f, cell.y + 4f, cell.width - 33f, 22f), WheelName(g), 11, host ? ink : ink.WithAlpha(0.5f), TextAnchor.MiddleLeft, true);
                float secs = Game.GadgetTime(g);
                string blurb = Game.GadgetTimed(g) ? secs.ToString("0") + " s" : g == GadgetId.Lodestone ? Game.LodestoneUses + " casts"
                    : g == GadgetId.Medkit ? Game.MedkitUses + " heals" : g == GadgetId.Emp ? Game.EmpUses + " pulses" : Game.GadgetUses(g) + " charges";
                Ui.Label(new Rect(cell.x + 31f, cell.y + 24f, cell.width - 33f, 20f), blurb, 10, CrtStyle.Desc, TextAnchor.MiddleLeft);
                if (hover)
                    hoverNote = "Drop a full " + Game.GadgetName(g) + " in front of " + whoName + " (only " + (who.Id == game.MyId ? "you see" : "they see") + " it; walk onto it).";
                if (host && Ui.HitArea(cell))
                    devItemNote = game.DevSpawnGadget(who.Id, g) ? "Dropped a " + Game.GadgetName(g) + " in front of " + whoName + ": walk onto it to pick it up." : "Couldn't drop it.";
            }

            // Gold and vectors: amounts scattered in front of the player as their own pickups (Game.DevSpawnCoins).
            var lr = col.Next(28f);
            Color goldInk = CrtStyle.Gold, vecInk = new Color(0.3f, 0.88f, 1f);
            Ui.Label(new Rect(lr.x, lr.y, 46f, lr.height), "GOLD", 10, goldInk, TextAnchor.MiddleLeft, true);
            int[] golds = { 10, 50, 250, 1000 }, vecs = { 1, 5, 20, 100 };
            float lx = lr.x + 46f;
            for (int i = 0; i < golds.Length; i++, lx += 70f)
            {
                var kr = new Rect(lx, lr.y, 64f, lr.height);
                if (DkKey(kr, "+" + golds[i], goldInk, false, 11) && host)
                    devItemNote = game.DevSpawnCoins(who.Id, golds[i], false) ? "Dropped " + golds[i] + " gold in front of " + whoName + ": walk over the coins." : "Couldn't drop it.";
                if (host && kr.Contains(ev.mousePosition))
                    hoverNote = "Scatter " + golds[i] + " gold as coins in front of " + whoName + " (" + (who.Id == game.MyId ? "your" : "their") + " own; walk over them). Has " + who.Gold + ".";
            }
            lx += 24f;
            Ui.Label(new Rect(lx, lr.y, 66f, lr.height), "VECTORS", 10, vecInk, TextAnchor.MiddleLeft, true);
            lx += 66f;
            for (int i = 0; i < vecs.Length; i++, lx += 70f)
            {
                var kr = new Rect(lx, lr.y, 64f, lr.height);
                if (DkKey(kr, "+" + vecs[i], vecInk, false, 11) && host)
                    devItemNote = game.DevSpawnCoins(who.Id, vecs[i], true) ? "Dropped " + vecs[i] + " vector" + (vecs[i] > 1 ? "s" : "") + " in front of " + whoName + ": walk over them." : "Couldn't drop it.";
                if (host && kr.Contains(ev.mousePosition))
                    hoverNote = "Scatter " + vecs[i] + " vector" + (vecs[i] > 1 ? "s" : "") + " as pickups in front of " + whoName + ". Has " + who.Vectors + "/" + who.VectorCap + " (a full wallet leaves them lying).";
            }

            // Chests: every kind, drawn as it stands in the arena; a click puts one in front of the player.
            Ui.Label(col.Next(16f), "CHESTS", 10, CrtStyle.Head, TextAnchor.MiddleLeft, true);
            var cc = DkStack.Cells(col.Next(62f), (int)ChestKind.Gadget + 1, 6f);
            for (int i = 0; i < cc.Length; i++)
            {
                var k = (ChestKind)i;
                var cell = cc[i];
                Color ink = ChestInkOf(k).ToColor();
                bool hover = host && cell.Contains(ev.mousePosition);
                Ui.Fill(cell, ink.WithAlpha(hover ? 0.16f : 0.05f));
                CrtKit.Frame(cell, 1f, ink.WithAlpha(hover ? 0.85f : 0.25f));
                string nm = ChestKindName(k).Replace(" CHEST", "");
                Ui.Label(new Rect(cell.x + 2f, cell.yMax - 17f, cell.width - 4f, 14f), nm, 10, host ? ink : ink.WithAlpha(0.5f), TextAnchor.MiddleCenter, true);
                if (hover)
                    hoverNote = ChestKindName(k) + ": " + ChestGives(k).ToLowerInvariant() + ". Put one in front of " + whoName + " (it doesn't hold the wave).";
                if (host && Ui.HitArea(cell))
                    devItemNote = game.DevSpawnChest(who.Id, k) ? "Put a " + ChestKindName(k).ToLowerInvariant() + " in front of " + whoName + ": walk up to it to open it." : "Couldn't place it.";
            }

            // Relics: every relic in boss order (the three classic ones first), the one held lit; click sets the slot.
            var rh = col.Next(22f);
            Ui.Label(rh, "RELICS", 10, CrtStyle.Head, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(rh.x + 60f, rh.y, rh.width - 200f, rh.height), "slot: " + (who.Relic == RelicId.None ? "empty" : Relics.Name(who.Relic)), 10, CrtStyle.Desc, TextAnchor.MiddleLeft);
            bool was = GUI.enabled;
            GUI.enabled = was && host && who.Relic != RelicId.None;
            if (DkKey(new Rect(rh.xMax - 130f, rh.y, 130f, rh.height), "REMOVE RELIC", CrtStyle.Danger, false, 10) && game.DevSetRelic(who.Id, RelicId.None))
                devItemNote = "Took " + (who.Id == game.MyId ? "your" : who.Name + "'s") + " relic away.";
            GUI.enabled = was;
            var area = col.Rest();
            area.height -= 26f;
            DkSub(area);
            const int cols = 8;
            float gap = 4f, cw = (area.width - 12f - gap * (cols - 1)) / cols;
            int rows = (Relics.Count + cols - 1) / cols;
            float chh = Mathf.Min(96f, (area.height - 8f - gap * (rows - 1)) / rows);
            var glyphs = new List<KeyValuePair<RelicId, Rect>>();
            for (int i = 0; i < Relics.Count; i++)
            {
                var rel = (RelicId)i;
                var cell = new Rect(area.x + 6f + (i % cols) * (cw + gap), area.y + 4f + (i / cols) * (chh + gap), cw, chh);
                Rgba ri;
                int shape;
                RelicStyle(rel, out ri, out shape);
                Color ink = ri.ToColor();
                bool held = who.Relic == rel, hover = host && cell.Contains(ev.mousePosition);
                Ui.Fill(cell, (held ? CrtStyle.Gold : ink).WithAlpha(held ? 0.14f : hover ? 0.16f : 0.05f));
                CrtKit.Frame(cell, held ? 2f : 1f, held ? CrtStyle.Gold : ink.WithAlpha(hover ? 0.85f : 0.25f));
                int f = Relics.FactionOf(rel);
                Ui.Label(new Rect(cell.x + 2f, cell.yMax - 30f, cell.width - 4f, 14f), Relics.Name(rel), 10, held ? CrtStyle.Gold : host ? CrtStyle.Body : CrtStyle.Desc, TextAnchor.MiddleCenter, true);
                Ui.Label(new Rect(cell.x + 2f, cell.yMax - 16f, cell.width - 4f, 12f), held ? "HELD" : f < 0 ? "classic" : RelicFactions[f].Name, 9, held ? CrtStyle.Gold : ink.WithAlpha(0.7f), TextAnchor.MiddleCenter, held);
                glyphs.Add(new KeyValuePair<RelicId, Rect>(rel, cell));
                if (hover)
                    hoverNote = Relics.Name(rel) + ": " + Relics.Desc(rel);
                if (host && !held && Ui.HitArea(cell) && game.DevSetRelic(who.Id, rel))
                    devItemNote = (who.Id == game.MyId ? "You hold " : who.Name + " holds ") + Relics.Name(rel) + " now.";
            }
            if (BeginUiGl())
            {
                float t = Time.unscaledTime;
                for (int i = 0; i < cc.Length; i++)
                {
                    UiClip(cc[i]);
                    UiModelAt(new V2(cc[i].center.x, cc[i].y + 25f), 0.55f);
                    DrawChestLook(V2.Zero, host ? 1f : 0.45f, t, (ChestKind)i, false, 0f);
                }
                glZoom = 1f;
                glShift = V2.Zero;
                foreach (var kv in glyphs)
                {
                    UiClip(kv.Value);
                    Rgba ri;
                    int shape;
                    RelicStyle(kv.Key, out ri, out shape);
                    bool lit = kv.Value.Contains(ev.mousePosition) || who.Relic == kv.Key;
                    float glyphH = kv.Value.height - 32f;
                    RelicGlyph(new V2(kv.Value.center.x, kv.Value.y + 4f + glyphH / 2f), Mathf.Min(0.95f, glyphH / 50f), shape, host ? ri : ri.WithAlpha(0.45f),
                        lit ? Mathf.Sin(t * 1.6f) : 1f);
                }
                EndUiGl();
            }

            string idle = !host ? "Only the host can spawn items." : "Click gold, vectors, a gadget or a chest to put it in front of them, or a relic to hold it (marks the run as a dev run).";
            string line = hoverNote.Length > 0 ? hoverNote : devItemNote.Length > 0 && host ? devItemNote : idle;
            Ui.Label(new Rect(r.x, r.yMax - 20f, r.width, 18f), line, 11, hoverNote.Length == 0 && devItemNote.Length > 0 && host ? CrtStyle.Gold : CrtStyle.Desc);
        }
    }
}
