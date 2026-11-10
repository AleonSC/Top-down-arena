using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Expanding arena on screen (ExpansionGame.cs, Docs/ExpandingArena.md): the vote on rooms to buy after a boss, and
    /// the team purse chip under the wave plate. The map and the minimap are drawn by RoomsLook.cs.
    public partial class ArenaApp
    {
        static readonly Rgba CombatInk = new Rgba(1f, 0.3f, 0.36f), ShopInk = new Rgba(1f, 0.82f, 0.4f);

        static Rgba KindInk(RoomKind k, PathId floor) => k == RoomKind.Shop ? ShopInk : k == RoomKind.Floor ? Paths.Get(floor).Accent : CombatInk;

        static string KindName(ExpansionRoom r) => r.Kind == RoomKind.Shop ? "Shop" : r.Kind == RoomKind.Floor ? Paths.Get(r.Floor).Name : "Combat";

        /// What a Floor room does: that path's rule inside the room only, and its bonus for kills there.
        static string FloorBlurb(PathId f)
        {
            var d = Paths.Get(f);
            string rule;
            switch (f)
            {
                case PathId.SwarmFields: rule = "Twice the crowd comes in here, melee foes at 60% health."; break;
                case PathId.SniperRidge: rule = "Twice the crowd comes in here."; break;
                case PathId.EliteGauntlet: rule = "Twice the crowd comes in here, every one elite."; break;
                case PathId.BurningGrounds: rule = "Fire patches burn in this room."; break;
                case PathId.Sanctuary: rule = "Healing pools open in this room."; break;
                case PathId.FrozenLake: rule = "Ice: you slide, foes are chilled."; break;
                case PathId.BloodMoon: rule = "No regeneration or orbs here: kills heal you."; break;
                default: rule = d.Name + " ground."; break;
            }
            int pct = (int)Mathf.Round((Paths.Bonus(d.Danger) - 1f) * 100f);
            return rule + (pct > 0 ? " Kills here give +" + pct + "% XP and score." : "");
        }

        // --- The optional offer's chip -------------------------------------------------------------------------

        bool ExpandChipUp => game.ExpandOffer != null && game.ExpandOptional && !expOpen;

        /// After a regular wave with the purse for a room: a chip (the wave doesn't wait for it) with a way to open the offer.
        void DrawExpandChip(Player me)
        {
            Ui.Alpha = 1f;
            bool calm = CabCalm;
            int cheapest = int.MaxValue, voted = game.ExpandVotes.Count;
            foreach (var o in game.ExpandOffer)
                cheapest = Mathf.Min(cheapest, o.Price);
            var chip = new Rect(W / 2f - 300f, 92f, 600f, 46f);
            Ui.Pill(chip, new Color(0f, 0f, 0f, 0.6f));
            Ui.Fill(new Rect(chip.x + 4f, chip.y + 7f, 3f, chip.height - 14f), new Color(1f, 0.82f, 0.4f));
            Ui.Label(new Rect(chip.x + 16f, chip.y + 3f, 400f, 22f), "ROOMS AVAILABLE  ·  " + game.ExpandOffer.Count + " OFFERS FROM " + cheapest + " GOLD", 13, new Color(1f, 0.82f, 0.4f), TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(chip.x + 16f, chip.y + 23f, 400f, 18f), "Purse " + game.Purse + "  ·  " + voted + " of " + game.Players.Count + " voted  ·  the wave starts in " + Mathf.CeilToInt(game.StateTimer) + " s", 11, Ui.Muted, TextAnchor.MiddleLeft);
            if (CrtUi.SoftKey(new Rect(chip.xMax - 170f, chip.y + 7f, 156f, 32f), "SEE THE ROOMS", null, 15, calm))
                expOpen = true;
        }

        // --- The shop room ------------------------------------------------------------------------------------

        /// The shop covers your screen: in the Expanding arena, only while you stand in a shop room (and haven't clicked Done).
        bool ShopScreenUp(Player me) => game.ShopOpen && me != null && (!game.ExpandingOn || !game.HasShopRoom || game.InShopRoom(me) && !me.ShopDone);

        /// The shop is open but you're not in a shop room: a banner and a way to skip it.
        void DrawShopRoomHint(Player me)
        {
            Ui.Alpha = 1f;
            bool calm = CabCalm;
            float dy = ExpandChipUp ? 56f : 0f;  // (below the room chip if it's up)
            if (me.ShopDone)
            {
                Ui.Label(new Rect(0, 150f + dy, W, 22f), "Done shopping  ·  waiting for the others", 14, Ui.Muted, TextAnchor.MiddleCenter, true);
                return;
            }
            Ui.Label(new Rect(0, 130f + dy, W, 26f), "THE SHOP IS OPEN  ·  walk into the gold room on the minimap", 18, Ui.Gold, TextAnchor.MiddleCenter, true);
            Ui.Label(new Rect(0, 156f + dy, W, 20f), "You have " + me.Gold + " gold. Leftover gold becomes XP when the shop closes.", 13, Ui.Muted, TextAnchor.MiddleCenter);
            if (CrtUi.SoftKey(new Rect(W / 2f - 120f, 180f + dy, 240f, 32f), "SKIP THE SHOP", null, 18, calm))
                game.Shop(ShopAction.Done, 1);
        }

        // --- The vote ----------------------------------------------------------------------------------------

        bool expDrawing, expDragging, expOpen;  // expOpen: an optional offer's window is open (otherwise just its chip shows)

        /// The room window covers the screen: a vote the wave waits for, or an optional offer you opened.
        bool ExpandWindowUp => game.ExpandOffer != null && (!game.ExpandOptional || expOpen);
        V2 expA, expB;  // the drawing's two corners, world px (snapped to the 100 px grid when read)

        void DrawExpansionChoice(float t)
        {
            var offers = game.ExpandOffer;
            if (offers == null)
            {
                expDrawing = expDragging = expOpen = false;
                return;
            }
            Ui.Alpha = 1f;
            bool calm = CabCalm;
            if (expDrawing)
            {
                DrawExpansionDraw(t, calm);
                return;
            }
            RoundBackdrop(PathDim);  // (RoundWindows.cs)
            CrtUi.Title(new Rect(0, 22f, W, 56f), "EXPAND THE ARENA", 52, CrtUi.Ink, TextAnchor.MiddleCenter, calm);
            int mine;
            bool voted = game.ExpandVotes.TryGetValue(game.MyId, out mine);
            Ui.Label(new Rect(0, 80f, W, 20f), "Team purse " + game.Purse + " gold  ·  " + game.Expanded.Count + " room" + (game.Expanded.Count == 1 ? "" : "s") + " so far  ·  the boss fights stay in the middle", 13, Ui.Muted, TextAnchor.MiddleCenter);
            float gap = offers.Count > 3 ? 20f : 30f, cw = Mathf.Min(370f, (W - 80f - (offers.Count - 1) * gap) / offers.Count), ch = 452f, y = 118f;
            float x0 = W / 2f - (offers.Count * cw + (offers.Count - 1) * gap) / 2f;
            for (int i = 0; i < offers.Count; i++)
            {
                var r = new Rect(x0 + i * (cw + gap), y, cw, ch);
                if (DrawExpansionCard(r, offers[i], i, voted && mine == i, game.Purse, t, calm, i == game.ExpandCustom))
                    game.CastExpandVote(i);
            }
            float by = y + ch + 12f;
            bool skip = voted && mine == offers.Count;
            if (CrtUi.SoftKey(new Rect(W / 2f - 450f, by, 440f, 34f), "DRAW YOUR OWN", null, 22, calm))
            {
                expDrawing = true; expDragging = false;
                expA = expB = V2.Zero;
            }
            if (CrtUi.SoftKey(new Rect(W / 2f + 10f, by, 440f, 34f), "BUY NOTHING  ·  KEEP THE PURSE" + (skip ? "  ✓" : ""), null, 22, calm, lit: skip))
                game.CastExpandVote(offers.Count);
            int waiting = 0;
            foreach (int id in game.Players.Keys)
                if (!game.ExpandVotes.ContainsKey(id))
                    waiting++;
            if (game.ExpandOptional)
            {
                if (CrtUi.SoftKey(new Rect(W - 270f, 28f, 220f, 32f), "DECIDE LATER", null, 18, calm))
                    expOpen = false;
                Ui.Label(new Rect(0, 100f, W, 20f), "Optional: the wave starts in " + Mathf.CeilToInt(game.StateTimer) + " s. A room is bought only if more than half the team votes for it by then.", 12, Ui.Muted, TextAnchor.MiddleCenter);
            }
            string msg = !voted ? "Vote for a room: most votes wins" : waiting > 0 ? "Voted. Waiting for " + waiting + " more player" + (waiting == 1 ? "" : "s") : "Counting the votes";
            Ui.Label(new Rect(0, by + 40f, W, 22f), msg, 14, !voted ? Ui.Gold : Ui.Muted, TextAnchor.MiddleCenter, true);
        }

        /// The drawing screen: the whole map, zoomed out; drag a box on the 100 px grid. It's checked live (Expansion.Custom) and goes to the vote on release.
        void DrawExpansionDraw(float t, bool calm)
        {
            RoundBackdrop(PathDim);
            CrtUi.Title(new Rect(0, 22f, W, 56f), "DRAW A ROOM OR HALLWAY", 46, CrtUi.Ink, TextAnchor.MiddleCenter, calm);
            Ui.Label(new Rect(0, 80f, W, 20f), "Drag on the map  ·  share at least " + Expansion.MinDoorRun / 100 + " tiles of wall with a room  ·  " + Expansion.MinSide / 100 + " tiles wide is a hallway  ·  touching two rooms makes a loop", 13, Ui.Muted, TextAnchor.MiddleCenter);
            var map = new Rect(70f, 116f, W - 140f, 440f);
            var wd = game.ExpansionWorld;
            float pad = 1300f;
            V2 lo = wd.Min - new V2(pad, pad), hi = wd.Max + new V2(pad, pad);
            float k = Mathf.Min(map.width / (hi.X - lo.X), map.height / (hi.Y - lo.Y));
            float ox = map.center.x - (hi.X - lo.X) * k / 2f, oy = map.center.y - (hi.Y - lo.Y) * k / 2f;
            System.Func<V2, V2> M = p => new V2(ox + (p.X - lo.X) * k, oy + (p.Y - lo.Y) * k);
            System.Func<Vector2, V2> Snap = m => new V2(Mathf.Round(((m.x - ox) / k + lo.X) / Expansion.Step) * Expansion.Step, Mathf.Round(((m.y - oy) / k + lo.Y) / Expansion.Step) * Expansion.Step);
            var ev = Event.current;
            bool over = map.Contains(ev.mousePosition);
            if (ev.type == EventType.MouseDown && ev.button == 0 && over)
            {
                expDragging = true;
                expA = expB = Snap(ev.mousePosition);
                ev.Use();
            }
            else if (ev.type == EventType.MouseDrag && expDragging)
            {
                expB = Snap(ev.mousePosition);
                ev.Use();
            }
            int rx = (int)Mathf.Min(expA.X, expB.X), ry = (int)Mathf.Min(expA.Y, expB.Y), rw = (int)Mathf.Abs(expA.X - expB.X), rh = (int)Mathf.Abs(expA.Y - expB.Y);
            bool hasBox = rw > 0 && rh > 0;
            string why = null;
            ExpansionRoom? box = null;
            if (hasBox)
            {
                box = Expansion.Custom(game.Expanded, rx, ry, rw, rh, out why);
                if (box.HasValue && box.Value.Price > game.Purse) { why = "Can't afford it: " + box.Value.Price + " gold, " + (box.Value.Price - game.Purse) + " more needed"; box = null; }
            }
            if (ev.type == EventType.MouseUp && ev.button == 0 && expDragging)
            {
                expDragging = false;
                if (box.HasValue)
                {
                    game.ProposeExpandRoom(rx, ry, rw, rh);
                    expDrawing = false;
                }
                ev.Use();
            }
            if (BeginUiGl())
            {
                RoomMapRect(map.x, map.y, map.width, map.height, new Rgba(0.01f, 0.03f, 0.08f, 0.7f));
                for (float gx = Mathf.Ceil(lo.X / 500f) * 500f; gx <= hi.X; gx += 500f)  // a faint grid every 5 tiles
                    RoomMapRect(M(new V2(gx, lo.Y)).X, map.y, 1f, map.height, new Rgba(0.4f, 0.64f, 1f, 0.07f));
                for (float gy = Mathf.Ceil(lo.Y / 500f) * 500f; gy <= hi.Y; gy += 500f)
                    RoomMapRect(map.x, M(new V2(lo.X, gy)).Y, map.width, 1f, new Rgba(0.4f, 0.64f, 1f, 0.07f));
                foreach (var a in wd.Areas)
                {
                    V2 p0 = M(a.Min), p1 = M(a.Max);
                    var ink = a.Hub ? new Rgba(0.4f, 0.64f, 1f) : KindInk(a.Kind, a.Floor);
                    RoomMapRect(p0.X, p0.Y, p1.X - p0.X, p1.Y - p0.Y, ink.WithAlpha(a.Hub ? 0.16f : 0.1f));
                    RoomMapFrame(p0.X, p0.Y, p1.X - p0.X, p1.Y - p0.Y, 1f, ink.WithAlpha(0.8f));
                }
                foreach (var d in wd.Doors)
                {
                    V2 c = M(d.Pos);
                    RoomMapRect(c.X - 3f, c.Y - 3f, 6f, 6f, new Rgba(0.37f, 0.94f, 1f, 0.8f));
                }
                if (hasBox)
                {
                    V2 p0 = M(new V2(rx, ry)), p1 = M(new V2(rx + rw, ry + rh));
                    var ink = box.HasValue ? new Rgba(0.35f, 1f, 0.55f) : new Rgba(1f, 0.3f, 0.36f);
                    RoomMapRect(p0.X, p0.Y, p1.X - p0.X, p1.Y - p0.Y, ink.WithAlpha(0.25f));
                    RoomMapFrame(p0.X, p0.Y, p1.X - p0.X, p1.Y - p0.Y, 2f, ink);
                }
                EndUiGl();
            }
            string line;
            Color lineInk;
            if (hasBox && box.HasValue)
            {
                int loops = Expansion.LoopsOf(game.Expanded, box.Value);
                line = (box.Value.Shape == "Hallway" ? "HALLWAY" : "ROOM") + " " + rw / 100 + " x " + rh / 100 + " tiles  ·  " + box.Value.Price + " GOLD" + (loops > 0 ? "  ·  CLOSES " + (loops == 1 ? "A LOOP" : loops + " LOOPS") : "") + "  ·  let go to put it to the vote";
                lineInk = Ui.Gold;
            }
            else if (hasBox)
            {
                line = why ?? "";
                lineInk = new Color(1f, 0.45f, 0.5f);
            }
            else
            {
                line = "Purse " + game.Purse + " gold  ·  a drawn room costs " + (int)((Expansion.CustomPremium - 1f) * 100f) + "% more than a random one of the same size";
                lineInk = Ui.Muted;
            }
            Ui.Label(new Rect(0, map.yMax + 10f, W, 22f), line, 15, lineInk, TextAnchor.MiddleCenter, true);
            if (CrtUi.SoftKey(new Rect(W / 2f - 120f, map.yMax + 44f, 240f, 34f), "BACK TO THE VOTE", null, 20, calm))
                expDrawing = expDragging = false;
        }

        /// One room on offer as a CRT tube: a little map with the room lit up where it would go, what it is and what it costs.
        bool DrawExpansionCard(Rect r, ExpansionRoom o, int index, bool picked, int purse, float t, bool calm, bool custom)
        {
            bool afford = o.Price <= purse;
            bool hover = afford && r.Contains(Event.current.mousePosition);
            Color ink = afford ? CrtUi.Ink : CrtUi.Dim, dim = CrtUi.Dim;
            bool taken = false;
            CrtUi.Begin(r);
            if (hover || picked)
                CrtKit.Frame(new Rect(r.x + 3f, r.y + 3f, r.width - 6f, r.height - 6f), picked ? 3f : 2f, picked ? Ui.Gold : ink);
            var stage = new Rect(r.x + 22f, r.y + 22f, r.width - 44f, 210f);
            if (BeginUiGl())
            {
                DrawExpansionMini(stage, o, afford, t);
                EndUiGl();
            }
            var accent = ToColor(KindInk(o.Kind, o.Floor));
            float ty = stage.yMax + 14f;
            int loops = Expansion.LoopsOf(game.Expanded, o);
            string title = custom ? "YOUR " + o.Shape.ToUpperInvariant() : o.Shape == "Room" ? "ROOM" : o.Shape.ToUpperInvariant() + " ROOM";
            CrtUi.Text(new Rect(r.x + 22f, ty, r.width - 44f, 34f), title, r.width < 330f ? 22 : 28, ink, TextAnchor.MiddleLeft, true, calm);
            CrtUi.Text(new Rect(r.x + 22f, ty + 34f, r.width - 44f, 22f), (custom ? "DRAWN  ·  " : KindName(o).ToUpperInvariant() + "  ·  ") + o.W / 100 + " x " + o.H / 100 + " tiles" + (loops > 0 ? "  ·  LOOP" : ""), 15, afford ? accent : dim, TextAnchor.MiddleLeft, false, calm);
            string blurb = custom ? "Drawn by your team." + (loops > 0 ? " It joins " + (loops + 1) + " rooms, so the map loops round." : "")
                : o.Kind == RoomKind.Shop ? "A safe room: no enemies come in. Kills drop gold, and the shop opens after the first wave following each boss: walk in to shop."
                : o.Kind == RoomKind.Floor ? FloorBlurb(o.Floor)
                : "More floor to fight on: and more enemies come with it.";
            CrtUi.Text(new Rect(r.x + 22f, ty + 62f, r.width - 44f, 70f), blurb, 14, dim, TextAnchor.UpperLeft, false, calm, true);
            int votes = 0;
            foreach (var v in game.ExpandVotes.Values)
                votes += v == index ? 1 : 0;
            string price = o.Price + " GOLD" + (afford ? "" : (r.width < 330f ? "  ·  NEED " : "  ·  NEED ") + (o.Price - purse) + (r.width < 330f ? "" : " MORE"));
            CrtUi.Text(new Rect(r.x + 22f, r.yMax - 82f, r.width - 44f, 30f), price, r.width < 330f ? 19 : 22, afford ? Ui.Gold : dim, TextAnchor.MiddleLeft, true, calm);
            CrtUi.Text(new Rect(r.x + 22f, r.yMax - 50f, r.width - 44f, 24f), votes == 0 ? "NO VOTES" : votes + " VOTE" + (votes == 1 ? "" : "S") + (picked ? "  ·  YOURS" : ""), 15, votes > 0 ? CrtUi.Ink : dim, TextAnchor.MiddleLeft, false, calm);
            CrtUi.End(r, t, calm);
            taken = hover && Event.current.type == EventType.MouseDown && Event.current.button == 0;
            if (taken)
                Event.current.Use();
            return taken;
        }

        /// The map so far (dim), with the offered room lit in its colour and a marker on its door.
        void DrawExpansionMini(Rect stage, ExpansionRoom o, bool afford, float t)
        {
            var all = new List<ExpansionRoom>(game.Expanded) { o };
            var wd = Expansion.Build(all);
            V2 lo = wd.Min, hi = wd.Max;
            float pad = 14f, k = Mathf.Min((stage.width - 2f * pad) / (hi.X - lo.X), (stage.height - 2f * pad) / (hi.Y - lo.Y));
            float ox = stage.center.x - (hi.X - lo.X) * k / 2f, oy = stage.center.y - (hi.Y - lo.Y) * k / 2f;
            System.Func<V2, V2> M = p => new V2(ox + (p.X - lo.X) * k, oy + (p.Y - lo.Y) * k);
            RoomMapRect(stage.x, stage.y, stage.width, stage.height, new Rgba(0.01f, 0.03f, 0.08f, 0.6f));
            var ink = KindInk(o.Kind, o.Floor);
            float pulse = 0.65f + 0.35f * Mathf.Sin(t * 3f);
            for (int i = 0; i < wd.Areas.Length; i++)
            {
                var a = wd.Areas[i];
                bool isNew = i == wd.Areas.Length - 1;
                V2 p0 = M(a.Min), p1 = M(a.Max);
                Rgba fill, line;
                if (isNew) { fill = ink.WithAlpha((afford ? 0.3f : 0.12f) * pulse); line = ink.WithAlpha(afford ? 1f : 0.45f); }
                else if (a.Hub) { fill = new Rgba(0.4f, 0.64f, 1f, 0.16f); line = new Rgba(0.4f, 0.64f, 1f, 0.8f); }
                else { fill = KindInk(a.Kind, a.Floor).WithAlpha(a.Kind == RoomKind.Combat ? 0.08f : 0.16f); line = KindInk(a.Kind, a.Floor).WithAlpha(0.5f); }
                RoomMapRect(p0.X, p0.Y, p1.X - p0.X, p1.Y - p0.Y, fill);
                RoomMapFrame(p0.X, p0.Y, p1.X - p0.X, p1.Y - p0.Y, isNew ? 2f : 1f, line);
            }
            foreach (var d in wd.Doors)
            {
                V2 c = M(d.Pos);
                RoomMapRect(c.X - 3f, c.Y - 3f, 6f, 6f, d.Joins(wd.Areas.Length - 1) ? Rgba.White.WithAlpha(0.9f) : new Rgba(0.37f, 0.94f, 1f, 0.7f));
            }
        }

        // --- HUD chip ----------------------------------------------------------------------------------------

        /// Under the wave plate: the team purse. Returns true if it drew (the next chip goes a line up).
        bool DrawPurseChip(float x, float y)
        {
            if (!game.ExpandingOn)
                return false;
            string text = "PURSE  " + game.Purse + " GOLD  ·  " + game.Expanded.Count + "/" + Expansion.MaxRooms + " ROOMS";
            float w = 30f + text.Length * 6.4f;
            var chip = new Rect(x, y, w, 20f);
            var gold = new Color(1f, 0.82f, 0.4f);
            Ui.Pill(chip, new Color(0f, 0f, 0f, 0.55f));
            Ui.Fill(new Rect(chip.x + 3f, chip.y + 4f, 3f, chip.height - 8f), gold);
            Ui.Label(new Rect(chip.x + 12f, chip.y, w, chip.height), text, 10, gold, TextAnchor.MiddleLeft, true);
            return true;
        }
    }
}
