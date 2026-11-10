using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Expanding arena on screen (ExpansionGame.cs, Docs/ExpandingArena.md): the vote on rooms to buy after a boss, and
    /// the team purse chip under the wave plate. The map and the minimap are drawn by RoomsLook.cs.
    public partial class ArenaApp
    {
        static readonly Rgba CombatInk = new Rgba(1f, 0.3f, 0.36f), ShopInk = new Rgba(1f, 0.82f, 0.4f);

        static readonly Rgba VendorInk = new Rgba(0.3f, 0.9f, 0.8f), GateInk = new Rgba(1f, 0.5f, 0.2f);

        static Rgba KindInk(RoomKind k, PathId floor) => k == RoomKind.Shop ? ShopInk : k == RoomKind.Vendor ? VendorInk : k == RoomKind.Gatekeeper ? GateInk : k == RoomKind.Floor ? Paths.Get(floor).Accent : CombatInk;

        /// A room's drawer's colour (their class colour), or a neutral blue when they've gone or it was nobody's.
        Rgba OwnerInk(int owner)
        {
            Player p;
            return owner != 0 && game.Players.TryGetValue(owner, out p) ? p.Def.Color : new Rgba(0.4f, 0.64f, 1f);
        }

        static string KindName(ExpansionRoom r) => r.Kind == RoomKind.Shop ? "Shop" : r.Kind == RoomKind.Vendor ? "Vendor" : r.Kind == RoomKind.Gatekeeper ? "Gatekeeper" : r.Kind == RoomKind.Floor ? Paths.Get(r.Floor).Name : "Combat";

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

        // --- The building round's chip ------------------------------------------------------------------------

        bool ExpandChipUp => game.ExpandOpen && !expOpen && game.Me != null;

        /// Between waves: a chip (the wave doesn't wait for it) with a way to open the round: draw a room, look at the proposals, vote.
        void DrawExpandChip(Player me)
        {
            Ui.Alpha = 1f;
            bool calm = CabCalm;
            int n = game.ExpandProposals.Count;
            // Above the Wave cleared box (it starts at y 84 and covers everything below), pulsing until you've voted or drawn.
            var chip = new Rect(W / 2f - 300f, 34f, 600f, 46f);
            bool mine = game.ExpandVotes.ContainsKey(game.MyId) || game.ProposalOf(game.MyId).HasValue;
            float pulse = mine ? 0f : 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
            Ui.Pill(chip, new Color(0f, 0.08f * pulse, 0.12f * pulse, 0.75f));
            Ui.Fill(new Rect(chip.x + 4f, chip.y + 7f, 3f, chip.height - 14f), new Color(0.37f, 0.94f, 1f, 0.6f + 0.4f * pulse));
            if (!mine)
                CrtKit.Frame(chip, 1.5f, new Color(0.37f, 0.94f, 1f, 0.35f + 0.5f * pulse));
            Ui.Label(new Rect(chip.x + 16f, chip.y + 3f, 400f, 22f), "BUILD ROOMS" + (game.RoomTokens.Count > 0 ? "  ·  " + game.RoomTokens.Count + " KIT" + (game.RoomTokens.Count == 1 ? "" : "S") : "") + "  ·  " + (n == 0 ? "NO PROPOSALS YET" : n + " PROPOSAL" + (n == 1 ? "" : "S")) + "  ·  YOUR VECTORS " + me.Vectors + " / " + me.VectorCap, 13, new Color(0.37f, 0.94f, 1f), TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(chip.x + 16f, chip.y + 23f, 400f, 18f), game.ExpandVotes.Count + " of " + game.Players.Count + " voted  ·  the wave starts in " + Mathf.CeilToInt(game.StateTimer) + " s", 11, Ui.Muted, TextAnchor.MiddleLeft);
            if (CrtUi.SoftKey(new Rect(chip.xMax - 170f, chip.y + 7f, 156f, 32f), "BUILD / VOTE", null, 15, calm))
                expOpen = true;
        }

        // --- The shop room ------------------------------------------------------------------------------------

        /// The shop covers your screen: in the Expanding arena, only while you stand in a shop room (and haven't clicked Done).
        bool ShopScreenUp(Player me) => game.ShopOpen && me != null && (!game.ServiceRooms || game.InServiceRoom(me) && !me.ShopDone);

        /// The shop is open but you're not in a shop room: a banner and a way to skip it.
        void DrawShopRoomHint(Player me)
        {
            if (game.ServiceRooms)
                return;  // (the room shops hold nothing up: the HUD chip says they're open)
            Ui.Alpha = 1f;
            bool calm = CabCalm;
            float dy = 0f;
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

        bool expDrawing, expDragging, expOpen, expModify;  // expModify: spending a room kit
        int expKit;  // the kit picked in the modify screen  // expOpen: the round's window is open (otherwise just its chip shows)

        /// The room window covers the screen (you opened it from the chip).
        bool ExpandWindowUp => game.ExpandOpen && expOpen;
        V2 expA, expB;  // the drawing's two corners, world px (snapped to the 100 px grid when read)

        void DrawExpansionChoice(float t)
        {
            var me = game.Me;
            if (!game.ExpandOpen || me == null)
            {
                expDrawing = expDragging = expOpen = expModify = false;
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
            CrtUi.Title(new Rect(0, 22f, W, 56f), "BUILD THE ARENA", 52, CrtUi.Ink, TextAnchor.MiddleCenter, calm);
            bool voted = game.ExpandVotes.ContainsKey(game.MyId);
            int backing = game.EffectiveVote(game.MyId);
            var props = game.ExpandProposals;
            Ui.Label(new Rect(0, 80f, W, 20f), "Your vectors " + me.Vectors + " / " + me.VectorCap + "  ·  " + game.Expanded.Count + " room" + (game.Expanded.Count == 1 ? "" : "s") + " so far  ·  most votes wins, a tie is a coin flip  ·  the wave starts in " + Mathf.CeilToInt(game.StateTimer) + " s", 13, Ui.Muted, TextAnchor.MiddleCenter);
            float gap = 30f, cw = Mathf.Min(370f, (W - 80f - (props.Count - 1) * gap) / Mathf.Max(1, props.Count)), ch = 452f, y = 118f;
            float x0 = W / 2f - (props.Count * cw + (props.Count - 1) * gap) / 2f;
            if (props.Count == 0)
                Ui.Label(new Rect(0, y + 180f, W, 40f), "No proposals yet. Draw a room or hallway and put it to the team.", 20, Ui.Muted, TextAnchor.MiddleCenter);
            for (int i = 0; i < props.Count; i++)
            {
                var r = new Rect(x0 + i * (cw + gap), y, cw, ch);
                if (DrawProposalCard(r, props[i], backing == props[i].Owner, t, calm))
                    game.CastExpandVote(props[i].Owner);
            }
            float by = y + ch + 12f;
            bool mineDrawn = game.ProposalOf(game.MyId).HasValue;
            if (CrtUi.SoftKey(new Rect(W / 2f - 450f, by, 440f, 34f), mineDrawn ? "REDRAW YOUR ROOM" : "DRAW A ROOM", null, 22, calm))
            {
                expDrawing = true; expDragging = false;
                expA = expB = V2.Zero;
            }
            bool skip = voted && backing == 0;
            if (CrtUi.SoftKey(new Rect(W / 2f + 10f, by, 440f, 34f), "NO ROOM" + (skip ? "  ✓" : ""), null, 22, calm, lit: skip))
                game.CastExpandVote(0);
            if (CrtUi.SoftKey(new Rect(W - 270f, 28f, 220f, 32f), "DECIDE LATER", null, 18, calm))
                expOpen = false;
            if (game.RoomTokens.Count > 0 && CrtUi.SoftKey(new Rect(50f, 28f, 260f, 32f), "ROOM KITS  ·  " + game.RoomTokens.Count, null, 18, calm))
            {
                expModify = true; expKit = 0;
            }
            int waiting = 0;
            foreach (int id in game.Players.Keys)
                if (!game.ExpandVotes.ContainsKey(id))
                    waiting++;
            bool ownDrawn = game.ProposalOf(game.MyId).HasValue;
            string msg = voted ? (waiting > 0 ? "Voted. Waiting for " + waiting + " more player" + (waiting == 1 ? "" : "s") + " to vote (or the wave to start)" : "Counting the votes")
                : ownDrawn ? "Your room counts as your vote. Click a card to back another, or click yours to lock it in"
                : props.Count > 0 ? "Vote for a room, or draw your own" : "Nothing to vote on yet";
            Ui.Label(new Rect(0, by + 40f, W, 22f), msg, 14, !voted && !ownDrawn && props.Count > 0 ? Ui.Gold : Ui.Muted, TextAnchor.MiddleCenter, true);
        }

        /// The room kit screen (Docs/RoomModifiers.md): pick a kit, then click a plain room on the map to turn it into a Shop or Floor room.
        void DrawExpansionModify(float t, bool calm)
        {
            RoundBackdrop(PathDim);
            var kits = game.RoomTokens;
            if (kits.Count == 0)
            {
                expModify = false;
                return;
            }
            expKit = Mathf.Clamp(expKit, 0, kits.Count - 1);
            CrtUi.Title(new Rect(0, 22f, W, 56f), "ROOM KITS", 46, CrtUi.Ink, TextAnchor.MiddleCenter, calm);
            Ui.Label(new Rect(0, 80f, W, 20f), "Pick a kit, then click a plain room  ·  one change per room, it can't be undone  ·  bosses give floors, won challenges give shops, the vendor sells floors", 13, Ui.Muted, TextAnchor.MiddleCenter);
            float kw = 190f, kgap = 10f, kx = W / 2f - (kits.Count * kw + (kits.Count - 1) * kgap) / 2f;
            for (int i = 0; i < kits.Count; i++)
            {
                var kr = new Rect(kx + i * (kw + kgap), 106f, kw, 34f);
                bool sel = i == expKit;
                if (CrtUi.SoftKey(kr, kits[i].Name.ToUpperInvariant() + (sel ? "  ✓" : ""), null, 15, calm, lit: sel))
                    expKit = i;
            }
            var kit = kits[expKit];
            var map = new Rect(70f, 152f, W - 140f, 410f);
            var wd = game.ExpansionWorld;
            float pad = 300f;
            V2 lo = wd.Min - new V2(pad, pad), hi = wd.Max + new V2(pad, pad);
            float k = Mathf.Min(map.width / (hi.X - lo.X), map.height / (hi.Y - lo.Y));
            float ox = map.center.x - (hi.X - lo.X) * k / 2f, oy = map.center.y - (hi.Y - lo.Y) * k / 2f;
            System.Func<V2, V2> M = p => new V2(ox + (p.X - lo.X) * k, oy + (p.Y - lo.Y) * k);
            var ev = Event.current;
            int hover = -1;
            for (int i = 0; i < game.Expanded.Count; i++)
            {
                var r = game.Expanded[i];
                V2 p0 = M(new V2(r.X, r.Y)), p1 = M(new V2(r.X1, r.Y1));
                string why;
                if (Expansion.CanRetype(game.Expanded, i, kit, out why) && new Rect(p0.X, p0.Y, p1.X - p0.X, p1.Y - p0.Y).Contains(ev.mousePosition))
                    hover = i;
            }
            if (ev.type == EventType.MouseDown && ev.button == 0 && hover >= 0)
            {
                game.ApplyRoomToken(expKit, hover);
                ev.Use();
            }
            if (BeginUiGl())
            {
                RoomMapRect(map.x, map.y, map.width, map.height, new Rgba(0.01f, 0.03f, 0.08f, 0.7f));
                for (int i = 0; i < wd.Areas.Length; i++)
                {
                    var a = wd.Areas[i];
                    V2 p0 = M(a.Min), p1 = M(a.Max);
                    var ink = a.Hub ? new Rgba(0.4f, 0.64f, 1f) : KindInk(a.Kind, a.Floor);
                    bool can = i > 0 && i - 1 == hover;
                    var pick = KindInk(kit.Kind, kit.Floor);
                    float pulse = 0.6f + 0.4f * Mathf.Sin(t * 5f);
                    RoomMapRect(p0.X, p0.Y, p1.X - p0.X, p1.Y - p0.Y, (can ? pick : ink).WithAlpha(can ? 0.35f * pulse : a.Hub ? 0.16f : 0.12f));
                    RoomMapFrame(p0.X, p0.Y, p1.X - p0.X, p1.Y - p0.Y, can ? 2.5f : 1f, (can ? pick : ink).WithAlpha(0.85f));
                }
                EndUiGl();
            }
            for (int i = 0; i < game.Expanded.Count; i++)
            {
                var r = game.Expanded[i];
                if (r.Kind == RoomKind.Combat || r.Floating)
                    continue;
                V2 p0 = M(new V2(r.X, r.Y));
                Ui.Label(new Rect(p0.X + 3f, p0.Y + 2f, 200f, 14f), KindName(r).ToUpperInvariant(), 10, ToColor(KindInk(r.Kind, r.Floor)), TextAnchor.UpperLeft, true);
            }
            string line = kit.Kind == RoomKind.Floor ? FloorBlurb(kit.Floor) : "A shop room: kills drop personal gold and the shop opens in the break after each boss.";
            Ui.Label(new Rect(0, map.yMax + 10f, W, 22f), hover >= 0 ? "Click to turn this room into: " + kit.Name + ".  " + line : line, 14, hover >= 0 ? Ui.Gold : Ui.Muted, TextAnchor.MiddleCenter, true);
            if (CrtUi.SoftKey(new Rect(W / 2f - 120f, map.yMax + 44f, 240f, 34f), "BACK", null, 20, calm))
                expModify = false;
        }

        /// The drawing screen: the whole map, zoomed out; drag a box on the 100 px grid. It's checked live (Expansion.Custom) and goes to the vote on release.
        void DrawExpansionDraw(float t, bool calm)
        {
            RoundBackdrop(PathDim);
            CrtUi.Title(new Rect(0, 22f, W, 56f), "DRAW A ROOM OR HALLWAY", 46, CrtUi.Ink, TextAnchor.MiddleCenter, calm);
            Ui.Label(new Rect(0, 80f, W, 20f), "Drag on the map  ·  share at least " + Expansion.MinDoorRun / 100 + " tiles of wall with a room  ·  " + Expansion.MinSide / 100 + " tiles wide is a hallway  ·  touching two rooms makes a loop", 13, Ui.Muted, TextAnchor.MiddleCenter);
            var map = new Rect(70f, 116f, W - 140f, 400f);
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
                box = Expansion.Custom(game.Expanded, rx, ry, rw, rh, expTheme, out why);
                int have = game.Me != null ? game.Me.Vectors : 0;
                if (box.HasValue && box.Value.Price > have) { why = "Can't afford it: " + box.Value.Price + " vectors, you have " + have; box = null; }
            }
            if (ev.type == EventType.MouseUp && ev.button == 0 && expDragging)
            {
                expDragging = false;
                if (box.HasValue)
                {
                    game.ProposeExpandRoom(rx, ry, rw, rh, expTheme);
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
                    float ghost = a.Sealed ? 0.55f + 0.25f * Mathf.Sin(Time.unscaledTime * 2.5f) : 1f;  // a sealed floating room breathes
                    RoomMapRect(p0.X, p0.Y, p1.X - p0.X, p1.Y - p0.Y, ink.WithAlpha((a.Hub ? 0.16f : 0.1f) * ghost));
                    RoomMapFrame(p0.X, p0.Y, p1.X - p0.X, p1.Y - p0.Y, a.Floating ? 1.5f : 1f, ink.WithAlpha(0.8f * ghost));
                }
                foreach (var d in wd.Doors)
                {
                    V2 c = M(d.Pos);
                    RoomMapRect(c.X - 3f, c.Y - 3f, 6f, 6f, new Rgba(0.37f, 0.94f, 1f, 0.8f));
                }
                foreach (var q in game.ExpandProposals)  // everyone's proposals so far, in their drawer's colour
                {
                    V2 q0 = M(new V2(q.X, q.Y)), q1 = M(new V2(q.X + q.W, q.Y + q.H));
                    var qi = OwnerInk(q.Owner);
                    RoomMapRect(q0.X, q0.Y, q1.X - q0.X, q1.Y - q0.Y, qi.WithAlpha(0.14f));
                    RoomMapFrame(q0.X, q0.Y, q1.X - q0.X, q1.Y - q0.Y, 1.5f, qi.WithAlpha(0.85f));
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
            foreach (var fa in wd.Areas)  // the floating rooms, named
                if (fa.Floating)
                {
                    V2 f0 = M(fa.Min);
                    Ui.Label(new Rect(f0.X + 3f, f0.Y + 2f, 200f, 14f), fa.Name.ToUpperInvariant() + (fa.Sealed ? "  ·  SEALED: BUILD TO IT" : ""), 10, ToColor(KindInk(fa.Kind, fa.Floor)), TextAnchor.UpperLeft, true);
                }
            foreach (var q in game.ExpandProposals)
            {
                Player qo;
                if (!game.Players.TryGetValue(q.Owner, out qo))
                    continue;
                V2 q0 = M(new V2(q.X, q.Y));
                Ui.Label(new Rect(q0.X + 3f, q0.Y + 2f, 160f, 14f), qo.Name + "  " + q.Price, 10, ToColor(OwnerInk(q.Owner)), TextAnchor.UpperLeft, true);
            }
            string line;
            Color lineInk;
            if (hasBox && box.HasValue)
            {
                int loops = Expansion.LoopsOf(game.Expanded, box.Value);
                line = (box.Value.Shape == "Hallway" ? "HALLWAY" : "ROOM") + " " + rw / 100 + " x " + rh / 100 + " tiles  ·  " + box.Value.Price + " VECTORS (of " + (game.Me != null ? game.Me.Vectors : 0) + ")" + (loops > 0 ? "  ·  CLOSES " + (loops == 1 ? "A LOOP" : loops + " LOOPS") : "") + "  ·  let go to propose it";
                lineInk = Ui.Gold;
            }
            else if (hasBox)
            {
                line = why ?? "";
                lineInk = new Color(1f, 0.45f, 0.5f);
            }
            else
            {
                line = "Your vectors " + (game.Me != null ? game.Me.Vectors : 0) + "  ·  a room costs its new wall: shared walls are free, so touching rooms is cheaper";
                lineInk = Ui.Muted;
            }
            DrawThemeRow(new Rect(map.x, map.yMax + 6f, map.width, 32f), calm);
            Ui.Label(new Rect(0, map.yMax + 46f, W, 22f), line, 15, lineInk, TextAnchor.MiddleCenter, true);
            if (CrtUi.SoftKey(new Rect(W / 2f - 120f, map.yMax + 80f, 240f, 34f), "BACK", null, 20, calm))
                expDrawing = expDragging = false;
        }

        PathId expTheme = PathId.None;  // the theme the next drawn room carries

        /// The themes you can draw a room with: Plain, then each path as a room (its extra vectors; locked until enough bosses are down).
        void DrawThemeRow(Rect row, bool calm)
        {
            var themes = new List<PathId> { PathId.None };
            themes.AddRange(Expansion.Floors);
            float gap = 6f, w = (row.width - gap * (themes.Count - 1)) / themes.Count;
            if (!Expansion.ThemeUnlocked(expTheme, game.Wave))
                expTheme = PathId.None;
            for (int i = 0; i < themes.Count; i++)
            {
                var f = themes[i];
                var r = new Rect(row.x + i * (w + gap), row.y, w, row.height);
                bool locked = !Expansion.ThemeUnlocked(f, game.Wave);
                string name = f == PathId.None ? "PLAIN" : Paths.Get(f).Name.ToUpperInvariant();
                string tag = locked ? "BOSS " + Expansion.ThemeBoss(f) : f == PathId.None ? "FREE" : "+" + Expansion.ThemeCost(f);
                if (locked)
                {
                    Ui.Pill(r, new Color(0f, 0f, 0f, 0.45f));
                    Ui.Label(new Rect(r.x, r.y + 2f, r.width, 15f), name, 10, new Color(1f, 1f, 1f, 0.35f), TextAnchor.MiddleCenter, true);
                    Ui.Label(new Rect(r.x, r.y + 16f, r.width, 14f), tag + " TO UNLOCK", 9, Ui.Muted, TextAnchor.MiddleCenter);
                    continue;
                }
                bool on = expTheme == f;
                if (CrtUi.SoftKey(r, name + "  " + tag, null, 11, calm, lit: on))
                    expTheme = f;
            }
        }

        /// One proposal as a CRT tube: a little map with the room lit where it would go, who drew it, what it costs and how it's voted.
        bool DrawProposalCard(Rect r, ExpansionRoom o, bool picked, float t, bool calm)
        {
            Player owner;
            game.Players.TryGetValue(o.Owner, out owner);
            bool afford = owner != null && o.Price <= owner.Vectors;
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
            float ty = stage.yMax + 14f;
            int loops = Expansion.LoopsOf(game.Expanded, o);
            bool mineCard = o.Owner == game.MyId;
            CrtUi.Text(new Rect(r.x + 22f, ty, r.width - 44f, 34f), (mineCard ? "YOUR " : "") + (o.Kind == RoomKind.Floor ? Paths.Get(o.Floor).Name.ToUpperInvariant() + " " : "") + o.Shape.ToUpperInvariant(), r.width < 330f ? 22 : 28, ink, TextAnchor.MiddleLeft, true, calm);
            CrtUi.Text(new Rect(r.x + 22f, ty + 34f, r.width - 44f, 22f), (owner != null ? "DRAWN BY " + owner.Name.ToUpperInvariant() : "DRAWN") + "  ·  " + o.W / 100 + " x " + o.H / 100 + " tiles" + (loops > 0 ? "  ·  LOOP" : ""), 15, afford ? CrtUi.Ink : dim, TextAnchor.MiddleLeft, false, calm);
            string blurb = o.Kind == RoomKind.Floor ? FloorBlurb(o.Floor) : loops > 0 ? "It joins " + (loops + 1) + " rooms, so the map loops round." : "More floor to fight on, and more enemies come with it.";
            CrtUi.Text(new Rect(r.x + 22f, ty + 62f, r.width - 44f, 70f), blurb, 14, dim, TextAnchor.UpperLeft, false, calm, true);
            int votes = game.VotesFor(o.Owner);
            CrtUi.Text(new Rect(r.x + 22f, r.yMax - 82f, r.width - 44f, 30f), o.Price + " VECTORS" + (owner != null ? "  ·  " + owner.Name.ToUpperInvariant() + " HAS " + owner.Vectors : ""), r.width < 330f ? 17 : 20, afford ? new Color(0.37f, 0.94f, 1f) : dim, TextAnchor.MiddleLeft, true, calm);
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
            var ink = o.Owner != 0 ? OwnerInk(o.Owner) : KindInk(o.Kind, o.Floor);
            float pulse = 0.65f + 0.35f * Mathf.Sin(t * 3f);
            for (int i = 0; i < wd.Areas.Length; i++)
            {
                var a = wd.Areas[i];
                bool isNew = i == wd.Areas.Length - 1;
                V2 p0 = M(a.Min), p1 = M(a.Max);
                Rgba fill, line;
                if (isNew) { fill = ink.WithAlpha((afford ? 0.3f : 0.12f) * pulse); line = ink.WithAlpha(afford ? 1f : 0.45f); }
                else if (a.Hub) { fill = new Rgba(0.4f, 0.64f, 1f, 0.16f); line = new Rgba(0.4f, 0.64f, 1f, 0.8f); }
                else if (a.Owner != 0 && a.Kind == RoomKind.Combat) { fill = OwnerInk(a.Owner).WithAlpha(0.14f); line = OwnerInk(a.Owner).WithAlpha(0.7f); }
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

        // --- Vector chip --------------------------------------------------------------------------------------

        /// Under the wave plate: your vectors and the most you can hold. True if it drew (the next chip goes a line up).
        /// The room shops are open this intermission: a chip that says where to walk.
        bool DrawServiceChip(float x, float y)
        {
            var me = game.Me;
            if (!game.ShopOpen || !game.ServiceRooms || me == null || me.ShopDone)
                return false;
            string text = "SHOP OPEN  ·  " + me.Gold + " GOLD  ·  walk into the " + (game.HasShopRoom && game.HasVendorRoom ? "shop or vendor room" : game.HasShopRoom ? "shop room" : "vendor room");
            float w = 30f + text.Length * 6.2f;
            var chip = new Rect(x, y, w, 20f);
            Ui.Pill(chip, new Color(0f, 0f, 0f, 0.55f));
            Ui.Fill(new Rect(chip.x + 3f, chip.y + 4f, 3f, chip.height - 8f), Ui.Gold);
            Ui.Label(new Rect(chip.x + 12f, chip.y, w, chip.height), text, 10, Ui.Gold, TextAnchor.MiddleLeft, true);
            return true;
        }

        bool DrawVectorChip(float x, float y)
        {
            var me = game.Me;
            if (!game.ExpandingOn || me == null)
                return false;
            string text = "VECTORS  " + me.Vectors + " / " + me.VectorCap;
            float w = 30f + text.Length * 6.4f;
            var chip = new Rect(x, y, w, 20f);
            var ink = new Color(0.37f, 0.94f, 1f);
            Ui.Pill(chip, new Color(0f, 0f, 0f, 0.55f));
            Ui.Fill(new Rect(chip.x + 3f, chip.y + 4f, 3f, chip.height - 8f), ink);
            Ui.Label(new Rect(chip.x + 12f, chip.y, w, chip.height), text, 10, ink, TextAnchor.MiddleLeft, true);
            return true;
        }

    }
}
