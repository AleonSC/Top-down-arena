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
            int n = game.ProposalHeads.Count;
            // Above the Wave cleared box (it starts at y 84 and covers everything below), pulsing until you've voted or drawn.
            var chip = new Rect(W / 2f - 300f, 34f, 600f, 46f);
            bool free = game.FreeBuild;  // free building: no proposals or votes, a drawing is built at once
            bool mine = free || game.ExpandVotes.ContainsKey(game.MyId) || game.ProposalOf(game.MyId).HasValue;
            float pulse = mine ? 0f : 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
            Ui.Pill(chip, new Color(0f, 0.08f * pulse, 0.12f * pulse, 0.75f));
            Ui.Fill(new Rect(chip.x + 4f, chip.y + 7f, 3f, chip.height - 14f), new Color(0.37f, 0.94f, 1f, 0.6f + 0.4f * pulse));
            if (!mine)
                CrtKit.Frame(chip, 1.5f, new Color(0.37f, 0.94f, 1f, 0.35f + 0.5f * pulse));
            Ui.Label(new Rect(chip.x + 16f, chip.y + 3f, 400f, 22f), "BUILD ROOMS" + (game.RoomTokens.Count > 0 ? "  ·  " + game.RoomTokens.Count + " KIT" + (game.RoomTokens.Count == 1 ? "" : "S") : "") + "  ·  " + (free ? game.Expanded.Count + " ROOMS" : n == 0 ? "NO PROPOSALS YET" : n + " PROPOSAL" + (n == 1 ? "" : "S")) + (game.TeamPurse ? "  ·  TEAM PURSE " + game.SpendableVectors(me) : "  ·  YOUR VECTORS " + me.Vectors + " / " + me.VectorCap), 13, new Color(0.37f, 0.94f, 1f), TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(chip.x + 16f, chip.y + 23f, 400f, 18f), (free ? "Build freely with your own vectors" : game.ExpandVotes.Count + " of " + game.Players.Count + " voted") + "  ·  the wave starts in " + Mathf.CeilToInt(game.StateTimer) + " s", 11, Ui.Muted, TextAnchor.MiddleLeft);
            if (CrtUi.SoftKey(new Rect(chip.xMax - 170f, chip.y + 7f, 156f, 32f), game.FreeBuild ? "BUILD" : "BUILD / VOTE", null, 15, calm))
                expOpen = true;
        }

        // --- Doors mid-wave (free building) ---------------------------------------------------------------------

        /// Free building with a wave on: a small prompt to open a wall (doors only, no new rooms until the wave is over).
        bool DoorChipUp => game.DoorsOpen && !expOpen && game.Me != null && !game.Me.Downed;

        void DrawDoorChip(Player me)
        {
            Ui.Alpha = 1f;
            var chip = new Rect(W / 2f - 190f, 36f, 380f, 28f);
            Ui.Pill(chip, new Color(0f, 0.08f, 0.12f, 0.7f));
            Ui.Label(chip, "[B]  OPEN A DOOR  ·  YOUR VECTORS " + me.Vectors + " / " + me.VectorCap, 12, new Color(0.37f, 0.94f, 1f), TextAnchor.MiddleCenter, true);
            var ev = Event.current;
            if (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.B && !Keybinds.ChatOpen && GUIUtility.keyboardControl == 0)
            {
                expOpen = true;
                ev.Use();
            }
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
        bool ExpandWindowUp => game.CanCut && expOpen;
        V2 expA, expB;  // the drawing's two corners, world px (snapped to the 100 px grid when read)

        void DrawExpansionChoice(float t)
        {
            var me = game.Me;
            if (!game.CanCut || me == null)
            {
                expDrawing = expDragging = expOpen = expModify = false;
                return;
            }
            Ui.Alpha = 1f;
            bool calm = CabCalm;
            if (game.FreeBuild)  // free building has no vote screen: the draw screen is the window (DONE closes it)
            {
                if (!game.ExpandOpen)
                    expModify = false;
                if (!expDrawing && !expModify)
                {
                    expDrawing = true;
                    expDragging = false;
                    expA = expB = V2.Zero;
                    expLegs.Clear();
                }
            }
            if (expModify)
            {
                DrawExpansionModify(t, calm);
                return;
            }
            if (expDrawing)
            {
                DrawExpansionDraw(t, calm);
                return;
            }
            RoundBackdrop(PathDim);  // (RoundWindows.cs)
            // One big phosphor screen in a blue bezel (look B of Docs/Mockups/crt-build-arena.html).
            CrtKit.LoadoutBox(new Rect(30f, 12f, W - 60f, H - 24f));
            var S = new Rect(42f, 24f, W - 84f, H - 48f);
            CrtUi.Begin(S);
            bool voted = game.ExpandVotes.ContainsKey(game.MyId);
            int backing = game.EffectiveVote(game.MyId);
            var props = game.ProposalHeads;
            int secs = Mathf.CeilToInt(game.StateTimer);
            var dim = CrtUi.Dim;
            // The status strip: vote lamp, vectors and rooms, the wave countdown.
            float sy = S.y + 8f;
            bool lampOn = calm || Mathf.Repeat(t, 1.2f) < 0.6f;
            CrtUi.Text(new Rect(S.x + 20f, sy, 200f, 20f), (lampOn ? "● " : "  ") + "VOTE OPEN", 15, lampOn ? new Color(1f, 0.36f, 0.36f) : dim, TextAnchor.MiddleLeft, false, calm);
            CrtUi.Text(new Rect(S.x, sy, S.width, 20f), "VECTORS " + me.Vectors + " / " + me.VectorCap + "  ·  ROOMS " + game.Expanded.Count, 15, dim, TextAnchor.MiddleCenter, false, calm);
            CrtUi.Text(new Rect(S.xMax - 220f, sy, 200f, 20f), "WAVE IN " + secs / 60 + ":" + (secs % 60).ToString("00"), 15, secs <= 5 ? Ui.Gold : dim, TextAnchor.MiddleRight, false, calm);
            Ui.Fill(new Rect(S.x + 20f, sy + 24f, S.width - 40f, 1f), new Color(CrtUi.Ink.r, CrtUi.Ink.g, CrtUi.Ink.b, 0.28f));
            // The title with a blinking block cursor.
            const string heading = "BUILD THE ARENA";
            CrtUi.Title(new Rect(S.x, S.y + 38f, S.width, 54f), heading, 50, CrtUi.Ink, TextAnchor.MiddleCenter, calm);
            if (calm || Mathf.Repeat(t, 1f) < 0.5f)
                Ui.Fill(new Rect(S.center.x + CrtUi.TitleWidth(heading, 50) / 2f + 8f, S.y + 54f, 20f, 26f), CrtUi.Ink);
            Ui.Label(new Rect(S.x, S.y + 92f, S.width, 20f), "Most votes wins, a tie is a coin flip", 13, Ui.Muted, TextAnchor.MiddleCenter);
            float gap = 30f, cw = Mathf.Min(370f, (S.width - 80f - (props.Count - 1) * gap) / Mathf.Max(1, props.Count)), ch = 452f, y = S.y + 118f;
            float x0 = W / 2f - (props.Count * cw + (props.Count - 1) * gap) / 2f;
            if (props.Count == 0)
                Ui.Label(new Rect(0, y + 180f, W, 40f), "No proposals yet. Draw a room or hallway and put it to the team.", 20, Ui.Muted, TextAnchor.MiddleCenter);
            for (int i = 0; i < props.Count; i++)
            {
                var r = new Rect(x0 + i * (cw + gap), y, cw, ch);
                if (DrawProposalCard(r, props[i], backing == props[i].Owner, t, calm))
                    game.CastExpandVote(props[i].Owner);
            }
            // The keys in one row: room kits (when you hold any), draw, no room, decide later.
            float by = y + ch + 10f;
            bool mineDrawn = game.ProposalOf(game.MyId).HasValue;
            bool kits = game.RoomTokens.Count > 0;
            int nk = kits ? 4 : 3;
            float kg = 14f, kw = Mathf.Min(300f, (S.width - 80f - (nk - 1) * kg) / nk), kx = W / 2f - (nk * kw + (nk - 1) * kg) / 2f;
            int slot = 0;
            if (kits && CrtUi.SoftKey(new Rect(kx + slot++ * (kw + kg), by, kw, 34f), "ROOM KITS  ·  " + game.RoomTokens.Count, null, 20, calm))
            {
                expModify = true; expKit = 0;
            }
            if (CrtUi.SoftKey(new Rect(kx + slot++ * (kw + kg), by, kw, 34f), mineDrawn ? "REDRAW YOUR ROOM" : "DRAW A ROOM", null, 20, calm))
            {
                expDrawing = true; expDragging = false;
                expA = expB = V2.Zero;
                expLegs.Clear();
                expPts.Clear();
            }
            bool skip = voted && backing == 0;
            if (CrtUi.SoftKey(new Rect(kx + slot++ * (kw + kg), by, kw, 34f), "NO ROOM" + (skip ? "  ✓" : ""), null, 20, calm, lit: skip))
                game.CastExpandVote(0);
            if (CrtUi.SoftKey(new Rect(kx + slot++ * (kw + kg), by, kw, 34f), "DECIDE LATER", null, 20, calm))
                expOpen = false;
            int waiting = 0;
            foreach (int id in game.Players.Keys)
                if (!game.ExpandVotes.ContainsKey(id))
                    waiting++;
            bool ownDrawn = game.ProposalOf(game.MyId).HasValue;
            string msg = voted ? (waiting > 0 ? "Voted. Waiting for " + waiting + " more player" + (waiting == 1 ? "" : "s") + " to vote (or the wave to start)" : "Counting the votes")
                : ownDrawn ? "Your room counts as your vote. Click a card to back another, or click yours to lock it in"
                : props.Count > 0 ? "Vote for a room, or draw your own" : "Nothing to vote on yet";
            Ui.Label(new Rect(0, by + 40f, W, 22f), msg, 14, !voted && !ownDrawn && props.Count > 0 ? Ui.Gold : Ui.Muted, TextAnchor.MiddleCenter, true);
            CrtUi.End(S, t, calm);
        }

        /// The CRT window the room kit and draw screens sit in: a blue bezel round one phosphor screen (as the vote window).
        Rect CrtWindowBegin()
        {
            CrtKit.LoadoutBox(new Rect(20f, 6f, W - 40f, H - 12f));
            var S = new Rect(32f, 18f, W - 64f, H - 36f);
            CrtUi.Begin(S);
            return S;
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
            var win = CrtWindowBegin();
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
                var hoverGroup = hover >= 0 ? Expansion.GroupOf(game.Expanded, hover) : new List<int>();  // (a drawn room lights up whole)
                for (int i = 0; i < wd.Areas.Length; i++)
                {
                    var a = wd.Areas[i];
                    V2 p0 = M(a.Min), p1 = M(a.Max);
                    var ink = a.Hub ? new Rgba(0.4f, 0.64f, 1f) : KindInk(a.Kind, a.Floor);
                    bool can = i > 0 && hoverGroup.Contains(i - 1);
                    var pick = KindInk(kit.Kind, kit.Floor);
                    float pulse = 0.6f + 0.4f * Mathf.Sin(t * 5f);
                    RoomMapRect(p0.X, p0.Y, p1.X - p0.X, p1.Y - p0.Y, (can ? pick : ink).WithAlpha(can ? 0.35f * pulse : a.Hub ? 0.16f : 0.12f));
                    RimFrame(wd, i, M, can ? 2.5f : 1f, (can ? pick : ink).WithAlpha(0.85f));
                }
                EndUiGl();
            }
            for (int i = 0; i < game.Expanded.Count; i++)
            {
                var r = game.Expanded[i];
                if (r.Kind == RoomKind.Combat || r.Floating || r.Leg > 0)  // (one label for a room of several boxes)
                    continue;
                V2 p0 = M(new V2(r.X, r.Y));
                Ui.Label(new Rect(p0.X + 3f, p0.Y + 2f, 200f, 14f), KindName(r).ToUpperInvariant(), 10, ToColor(KindInk(r.Kind, r.Floor)), TextAnchor.UpperLeft, true);
            }
            string line = kit.Kind == RoomKind.Floor ? FloorBlurb(kit.Floor) : "A shop room: kills drop personal gold and the shop opens in the break after each boss.";
            Ui.Label(new Rect(0, map.yMax + 10f, W, 22f), hover >= 0 ? "Click to turn this room into: " + kit.Name + ".  " + line : line, 14, hover >= 0 ? Ui.Gold : Ui.Muted, TextAnchor.MiddleCenter, true);
            if (CrtUi.SoftKey(new Rect(W / 2f - 120f, map.yMax + 44f, 240f, 34f), "BACK", null, 20, calm))
                expModify = false;
            CrtUi.End(win, t, calm);
        }

        /// The drawing screen: the whole map, zoomed out; drag a box on the 100 px grid. It's checked live (Expansion.Custom) and goes to the vote on release.
        void DrawExpansionDraw(float t, bool calm)
        {
            RoundBackdrop(PathDim);
            var win = CrtWindowBegin();
            bool free = game.FreeBuild, doorsOnly = !game.ExpandOpen;  // doorsOnly: free building with a wave on, walls only
            if (doorsOnly)
            {
                expCutMode = true;
                expLinkMode = expHallMode = expGateMode = false;
            }
            CrtUi.Title(new Rect(0, 22f, W, 56f), expGateMode ? "FIT A GATE" : expLinkMode ? "LINK TWO ROOMS" : expCutMode ? "OPEN A WALL" : expHallMode ? "LAY A HALLWAY" : "DRAW A ROOM", 40, CrtUi.Ink, TextAnchor.MiddleCenter, calm);
            if (!doorsOnly && CrtUi.SoftKey(new Rect(50f, 26f, 62f, 30f), "ROOM", null, 15, calm, lit: !expCutMode && !expLinkMode && !expHallMode && !expGateMode)) { expCutMode = expLinkMode = expHallMode = expGateMode = false; expDragging = false; expA = expB = V2.Zero; }
            if (!doorsOnly && CrtUi.SoftKey(new Rect(116f, 26f, 90f, 30f), "HALLWAY", null, 15, calm, lit: expHallMode)) { expHallMode = true; expCutMode = expLinkMode = expGateMode = false; expDragging = false; expA = expB = V2.Zero; }
            if (!doorsOnly && CrtUi.SoftKey(new Rect(210f, 26f, 100f, 30f), "OPEN WALL", null, 15, calm, lit: expCutMode)) { expCutMode = true; expLinkMode = expHallMode = expGateMode = false; expDragging = false; expA = expB = V2.Zero; }
            if (!doorsOnly && CrtUi.SoftKey(new Rect(314f, 26f, 74f, 30f), "PORTAL", null, 15, calm, lit: expLinkMode)) { expLinkMode = true; expCutMode = expHallMode = expGateMode = false; expDragging = false; expA = expB = V2.Zero; }
            if (!doorsOnly && CrtUi.SoftKey(new Rect(392f, 26f, 56f, 30f), "GATE", null, 15, calm, lit: expGateMode)) { expGateMode = true; expCutMode = expHallMode = expLinkMode = false; expDragging = false; expA = expB = V2.Zero; }
            if (free && !doorsOnly && game.RoomTokens.Count > 0 && CrtUi.SoftKey(new Rect(W - 200f, 26f, 150f, 30f), "KITS  ·  " + game.RoomTokens.Count, null, 15, calm))  // (free building has no vote window to hold the kit key)
            {
                expModify = true;
                expKit = 0;
            }
            if (!expHallMode)
                expLegs.Clear();
            bool roomMode = !expCutMode && !expLinkMode && !expHallMode && !expGateMode;
            if (!roomMode)
                expPts.Clear();
            Ui.Label(new Rect(0, 80f, W, 20f), doorsOnly ? "A wave is on: doors only  ·  click both ends of a wall stretch (the cyan ones)  ·  right click cancels  ·  you get 1 vector back for every tile that was wall" : expGateMode ? "Click a door to fit a shutter gate  ·  " + Gates.Price + " vectors  ·  stand by it and press " + Keybinds.KeyName(Keybinds.Get(KeyAction.TakeItem)).ToUpperInvariant() + " to shut it for " + (int)Gates.CloseTime + " s (" + (int)Gates.Cooldown + " s to recharge)  ·  " + game.GateFits.Count + " of " + Gates.Max + " fitted" : expHallMode ? "Left click where it starts, move along it, left click to lay a leg  ·  up to " + Expansion.MaxLegs + " legs, each from the end of the last  ·  right click undoes  ·  Enter proposes  ·  a hallway costs half its new wall" : expLinkMode ? "Left click a spot in one room, then a spot in another  ·  right click cancels  ·  " + Expansion.LinkBase + " vectors + 1 per 2 tiles of walking it saves  ·  enemies use it too  ·  " + game.Links.Count + " of " + Expansion.MaxLinks + " pairs built" : expCutMode ? "Left click one end of a wall stretch, then the other (the cyan stretches)  ·  right click cancels  ·  at least " + Expansion.MinCut / 100 + " tiles  ·  you get 1 vector back for every tile that was wall  ·  a door is just a short opening" : "Left click corners: walls run straight (a slanted click makes an L)  ·  click the first corner or press Enter to close it  ·  right click undoes a corner  ·  or drag a box  ·  " + Expansion.MinSide / 100 + " tiles wide everywhere, " + Expansion.MinDoorRun / 100 + " tiles of wall shared with a room", 13, Ui.Muted, TextAnchor.MiddleCenter);
            var map = new Rect(70f, 112f, W - 140f, 370f);
            var wd = game.ExpansionWorld;
            float pad = 1300f;
            V2 lo = wd.Min - new V2(pad, pad), hi = wd.Max + new V2(pad, pad);
            float k = Mathf.Min(map.width / (hi.X - lo.X), map.height / (hi.Y - lo.Y));
            float ox = map.center.x - (hi.X - lo.X) * k / 2f, oy = map.center.y - (hi.Y - lo.Y) * k / 2f;
            System.Func<V2, V2> M = p => new V2(ox + (p.X - lo.X) * k, oy + (p.Y - lo.Y) * k);
            System.Func<Vector2, V2> Snap = m => new V2(Mathf.Round(((m.x - ox) / k + lo.X) / Expansion.Step) * Expansion.Step, Mathf.Round(((m.y - oy) / k + lo.Y) / Expansion.Step) * Expansion.Step);
            var ev = Event.current;
            bool over = map.Contains(ev.mousePosition);
            // Controls: left click sets the first corner, the box follows the mouse, a second left click places it (or press, drag and let go);
            // right click or Escape cancels the box (and, with none started, goes back).
            bool confirm = false, dragRelease = false;
            // Gate mode: the door nearest the mouse (a click proposes a gate on it).
            int gateDoor = -1;
            string gateWhy = null;
            if (expGateMode && over)
            {
                V2 wm = new V2((ev.mousePosition.x - ox) / k + lo.X, (ev.mousePosition.y - oy) / k + lo.Y);
                gateDoor = Gates.NearestDoor(wd, wm, 14f / k);
                if (gateDoor >= 0 && !Gates.CanFit(wd, game.GateFits, wd.Doors[gateDoor].Pos, out gateWhy)) { }
                else if (gateDoor >= 0 && game.SpendableVectors(game.Me) < Gates.Price) gateWhy = "Can't afford it: " + Gates.Price + " vectors, you have " + game.SpendableVectors(game.Me);
                if (gateDoor >= 0 && gateWhy == null && ev.type == EventType.MouseDown && ev.button == 0)
                {
                    game.ProposeExpandGate(wd.Doors[gateDoor].Pos);
                    expDrawing = expDragging = false;
                    ev.Use();
                }
            }
            if (expGateMode && ev.type == EventType.MouseDown && ev.button == 0 && over)
                ev.Use();  // (gate mode never draws a box)
            if (expDragging && ev.type != EventType.Layout && ev.type != EventType.Used)
                expB = Snap(ev.mousePosition);
            // The room is an outline: left clicks lay corners (a slanted click an L), the first corner or Enter closes it and puts it
            // to the vote, right click (Esc, Backspace) takes the last corner back; with none laid, a press, drag and let go draws a box.
            V2 cursor = Snap(ev.mousePosition);
            bool closeRoom = false;
            List<int[]> dragBox = null;
            if (roomMode)
            {
                if ((ev.type == EventType.MouseDown && ev.button == 1) || (ev.type == EventType.KeyDown && (ev.keyCode == KeyCode.Escape || ev.keyCode == KeyCode.Backspace)))
                {
                    if (expHeld) expHeld = false;
                    else if (expPts.Count > 0) expPts.RemoveAt(expPts.Count - 1);
                    else
                    {
                        expDrawing = false;
                        if (free)
                            expOpen = false;
                    }
                    ev.Use();
                }
                else if (ev.type == EventType.MouseDown && ev.button == 0 && over)
                {
                    expHeld = true;
                    expPressAt = ev.mousePosition;
                    expA = cursor;
                    ev.Use();
                }
                else if (ev.type == EventType.MouseUp && ev.button == 0 && expHeld)
                {
                    expHeld = false;
                    if (expPts.Count == 0 && (ev.mousePosition - expPressAt).magnitude > 12f && expA.X != cursor.X && expA.Y != cursor.Y)
                        dragBox = BoxCorners(expA, cursor);
                    else if (expPts.Count >= 3 && expPts[0][0] == (int)cursor.X && expPts[0][1] == (int)cursor.Y)
                        closeRoom = true;
                    else if (expPts.Count < Expansion.MaxPoints)
                        AddWall(expPts, (int)cursor.X, (int)cursor.Y);
                    ev.Use();
                }
                else if (ev.type == EventType.KeyDown && (ev.keyCode == KeyCode.Return || ev.keyCode == KeyCode.KeypadEnter) && expPts.Count >= 2)
                {
                    closeRoom = true;
                    ev.Use();
                }
            }
            else if ((ev.type == EventType.MouseDown && ev.button == 1) || (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.Escape))
            {
                if (expDragging)
                {
                    expDragging = expHeld = false;
                    expA = expB = V2.Zero;
                }
                else if (expHallMode && expLegs.Count > 0)
                    expLegs.RemoveAt(expLegs.Count - 1);  // undo the last leg
                else
                {
                    expDrawing = false;
                    if (free)
                        expOpen = false;
                }
                ev.Use();
            }
            else if (!roomMode && ev.type == EventType.MouseDown && ev.button == 0 && over)
            {
                if (!expDragging)
                {
                    expDragging = expHeld = true;
                    expA = expB = Snap(ev.mousePosition);
                    expPressAt = ev.mousePosition;
                }
                else
                    confirm = true;
                ev.Use();
            }
            else if (!roomMode && ev.type == EventType.MouseUp && ev.button == 0 && expHeld)
            {
                expHeld = false;
                if (expDragging && (ev.mousePosition - expPressAt).magnitude > 12f)
                    confirm = dragRelease = true;  // press, drag, let go
                ev.Use();
            }
            bool proposeHall = expHallMode && expLegs.Count > 0 && ev.type == EventType.KeyDown && (ev.keyCode == KeyCode.Return || ev.keyCode == KeyCode.KeypadEnter);
            if (proposeHall)
                ev.Use();
            int rx = (int)Mathf.Min(expA.X, expB.X), ry = (int)Mathf.Min(expA.Y, expB.Y), rw = (int)Mathf.Abs(expA.X - expB.X), rh = (int)Mathf.Abs(expA.Y - expB.Y);
            if (expHallMode && (rw > 0 || rh > 0))  // a hallway leg: along the way the mouse went furthest, MinSide wide on the side it leans
            {
                if (rw >= rh) { rh = Expansion.MinSide; ry = expB.Y >= expA.Y ? (int)expA.Y : (int)expA.Y - Expansion.MinSide; }
                else { rw = Expansion.MinSide; rx = expB.X >= expA.X ? (int)expA.X : (int)expA.X - Expansion.MinSide; }
            }
            var hallSoFar = expHallMode && expLegs.Count > 0 ? Expansion.Hall(game.Expanded, expLegs, out string hallWhy) : null;
            // The outline as it stands: the corners laid, the wall to the cursor, and back to the first corner (or the box being dragged).
            List<int[]> shape = null, laid = null;
            List<ExpansionRoom> outlineRoom = null;
            string outlineWhy = null;
            int myVectors = game.SpendableVectors(game.Me);
            if (roomMode)
            {
                if (dragBox != null) shape = dragBox;
                else if (expHeld && expPts.Count == 0 && (ev.mousePosition - expPressAt).magnitude > 12f && expA.X != cursor.X && expA.Y != cursor.Y) shape = BoxCorners(expA, cursor);
                else if (expPts.Count > 0)
                {
                    laid = new List<int[]>(expPts);
                    if (!closeRoom && over)
                        AddWall(laid, (int)cursor.X, (int)cursor.Y);
                    shape = Closed(laid);
                }
                if (shape != null && shape.Count >= 4)
                {
                    outlineRoom = Expansion.Outline(game.Expanded, shape, expTheme, out outlineWhy);
                    if (outlineRoom != null && outlineRoom[0].Price > myVectors) { outlineWhy = "Can't afford it: " + outlineRoom[0].Price + " vectors, you have " + myVectors; outlineRoom = null; }
                }
                else if (shape != null)
                    outlineWhy = "Keep going: click more corners";
                if ((closeRoom || dragBox != null) && outlineRoom != null)
                {
                    game.ProposeExpandOutline(shape, expTheme);
                    expDrawing = false;
                    expPts.Clear();
                }
            }
            bool hasBox = !roomMode && !expCutMode && !expLinkMode && !expGateMode && rw > 0 && rh > 0;
            string why = null;
            ExpansionRoom? box = null;
            WallCut cut = default(WallCut);
            int cutRefund = 0;
            bool hasCut = expCutMode && (rw > 0 || rh > 0);
            bool hasLink = expLinkMode && (rw > 0 || rh > 0);
            var link = new PortalLink { AX = (short)expA.X, AY = (short)expA.Y, BX = (short)expB.X, BY = (short)expB.Y };
            int linkPrice = 0;
            if (hasLink)
            {
                linkPrice = Expansion.LinkPrice(wd, game.Links.Count, link, out why);
                int have = game.SpendableVectors(game.Me);
                if (linkPrice > have) { why = "Can't afford it: " + linkPrice + " vectors, you have " + have; linkPrice = 0; }
            }
            if (hasCut)
            {
                cut.Vertical = rh > rw;
                cut.At = (short)(cut.Vertical ? expA.X : expA.Y);
                cut.From = (short)(cut.Vertical ? ry : rx);
                cut.Till = (short)(cut.Vertical ? ry + rh : rx + rw);
                cutRefund = Expansion.CutRefund(game.Expanded, game.Openings, cut, out why);
            }
            int hallPrice = 0;  // the hallway with this leg on (or the legs so far)
            if (hasBox && expHallMode)
            {
                var legs = new List<int[]>(expLegs) { new[] { rx, ry, rw, rh } };
                var hall = legs.Count > Expansion.MaxLegs ? null : Expansion.Hall(game.Expanded, legs, out why);
                if (legs.Count > Expansion.MaxLegs) why = "At most " + Expansion.MaxLegs + " legs: press Enter to propose it";
                int have = game.SpendableVectors(game.Me);
                if (hall != null && hall[0].Price > have) { why = "Can't afford it: " + hall[0].Price + " vectors, you have " + have; hall = null; }
                if (hall != null) { box = hall[hall.Count - 1]; hallPrice = hall[0].Price; }
            }
            else if (hasBox)
            {
                box = Expansion.Custom(game.Expanded, rx, ry, rw, rh, expTheme, out why);
                int have = game.SpendableVectors(game.Me);
                if (box.HasValue && box.Value.Price > have) { why = "Can't afford it: " + box.Value.Price + " vectors, you have " + have; box = null; }
            }
            int laidPrice = hallSoFar != null ? hallSoFar[0].Price : 0;  // what PROPOSE puts to the vote: the legs laid so far
            bool hallReady = hallSoFar != null && game.Me != null && hallSoFar[0].Price <= game.SpendableVectors(game.Me);
            if (proposeHall && hallReady)
            {
                game.ProposeExpandHall(expLegs);
                expDrawing = expDragging = false;
                expLegs.Clear();
            }
            if (confirm)
            {
                if (box.HasValue && expHallMode)  // lay the leg; the last one allowed goes straight to the vote
                {
                    expLegs.Add(new[] { rx, ry, rw, rh });
                    expDragging = false;
                    expA = expB = V2.Zero;
                    if (expLegs.Count >= Expansion.MaxLegs)
                    {
                        game.ProposeExpandHall(expLegs);
                        expDrawing = false;
                        expLegs.Clear();
                    }
                }
                else if (box.HasValue)
                {
                    game.ProposeExpandRoom(rx, ry, rw, rh, expTheme);
                    expDrawing = expDragging = false;
                }
                else if (cutRefund > 0)
                {
                    game.ProposeExpandCut(cut);
                    expDrawing = expDragging = false;
                }
                else if (linkPrice > 0)
                {
                    game.ProposeExpandLink(link);
                    expDrawing = expDragging = false;
                }
                else if (dragRelease)  // a dragged box that doesn't fit is dropped; a clicked one stays so you can move the corner
                {
                    expDragging = false;
                    expA = expB = V2.Zero;
                }
            }
            if (BeginUiGl())
            {
                RoomMapRect(map.x, map.y, map.width, map.height, new Rgba(0.01f, 0.03f, 0.08f, 0.7f));
                for (float gx = Mathf.Ceil(lo.X / 500f) * 500f; gx <= hi.X; gx += 500f)  // a faint grid every 5 tiles
                    RoomMapRect(M(new V2(gx, lo.Y)).X, map.y, 1f, map.height, new Rgba(0.4f, 0.64f, 1f, 0.07f));
                for (float gy = Mathf.Ceil(lo.Y / 500f) * 500f; gy <= hi.Y; gy += 500f)
                    RoomMapRect(map.x, M(new V2(lo.X, gy)).Y, map.width, 1f, new Rgba(0.4f, 0.64f, 1f, 0.07f));
                for (int ai = 0; ai < wd.Areas.Length; ai++)
                {
                    var a = wd.Areas[ai];
                    V2 p0 = M(a.Min), p1 = M(a.Max);
                    var ink = a.Hub ? new Rgba(0.4f, 0.64f, 1f) : KindInk(a.Kind, a.Floor);
                    float ghost = a.Sealed ? 0.55f + 0.25f * Mathf.Sin(Time.unscaledTime * 2.5f) : 1f;  // a sealed floating room breathes
                    RoomMapRect(p0.X, p0.Y, p1.X - p0.X, p1.Y - p0.Y, ink.WithAlpha((a.Hub ? 0.16f : 0.1f) * ghost));
                    RimFrame(wd, ai, M, a.Floating ? 1.5f : 1f, ink.WithAlpha(0.8f * ghost));
                }
                for (int di = 0; di < wd.Doors.Length; di++)
                {
                    if (wd.Seam(di))
                        continue;  // (inside one room)
                    var d = wd.Doors[di];
                    V2 c = M(d.Pos);
                    if (d.Width > 0f)  // an opened wall: its whole length
                    {
                        V2 e0 = M(d.Pos - d.Along * (d.W / 2f)), e1 = M(d.Pos + d.Along * (d.W / 2f));
                        RoomMapRect(Mathf.Min(e0.X, e1.X) - 2f, Mathf.Min(e0.Y, e1.Y) - 2f, Mathf.Abs(e1.X - e0.X) + 4f, Mathf.Abs(e1.Y - e0.Y) + 4f, new Rgba(0.37f, 0.94f, 1f, 0.8f));
                    }
                    else
                        RoomMapRect(c.X - 3f, c.Y - 3f, 6f, 6f, new Rgba(0.37f, 0.94f, 1f, 0.8f));
                }
                if (expCutMode)  // the walls two rooms share: where an opening can go
                    foreach (var sw in Expansion.SharedWalls(wd))
                    {
                        V2 w0 = M(new V2(sw[0] == 1 ? sw[1] : sw[2], sw[0] == 1 ? sw[2] : sw[1])), w1 = M(new V2(sw[0] == 1 ? sw[1] : sw[3], sw[0] == 1 ? sw[3] : sw[1]));
                        RoomMapRect(Mathf.Min(w0.X, w1.X) - 1.5f, Mathf.Min(w0.Y, w1.Y) - 1.5f, Mathf.Abs(w1.X - w0.X) + 3f, Mathf.Abs(w1.Y - w0.Y) + 3f, new Rgba(0.37f, 0.94f, 1f, 0.35f));
                    }
                for (int li = 0; li < game.Links.Count; li++)  // the portal pairs built, in their pair's colour
                    DrawLinkMark(M(game.Links[li].A), M(game.Links[li].B), PortalColours[(wd.Portals.Length - game.Links.Count + li) % PortalColours.Length], 1f);
                foreach (var gf in game.GateFits)  // the gates fitted: a red bar on the door
                {
                    V2 gc = M(gf.Pos);
                    RoomMapRect(gc.X - (gf.Vertical ? 2.5f : 6f), gc.Y - (gf.Vertical ? 6f : 2.5f), gf.Vertical ? 5f : 12f, gf.Vertical ? 12f : 5f, new Rgba(1f, 0.3f, 0.32f, 0.95f));
                }
                if (expGateMode)  // the doors a gate can go on, and the one under the mouse
                    for (int di = 0; di < wd.Doors.Length; di++)
                    {
                        string dw;
                        if (!Gates.CanFit(wd, game.GateFits, wd.Doors[di].Pos, out dw))
                            continue;
                        V2 dc = M(wd.Doors[di].Pos);
                        bool hot = di == gateDoor;
                        float s2 = hot ? 9f : 6f;
                        RoomMapFrame(dc.X - s2, dc.Y - s2, 2f * s2, 2f * s2, hot ? 2f : 1.5f, (hot ? (gateWhy == null ? new Rgba(0.35f, 1f, 0.55f) : new Rgba(1f, 0.3f, 0.36f)) : new Rgba(1f, 0.85f, 0.4f)).WithAlpha(hot ? 1f : 0.5f + 0.3f * Mathf.Sin(Time.unscaledTime * 3f)));
                    }
                foreach (var q in game.ExpandProposals)  // everyone's proposals so far, in their drawer's colour
                {
                    V2 q0 = M(new V2(q.X, q.Y)), q1 = M(new V2(q.X + q.W, q.Y + q.H));
                    var qi = OwnerInk(q.Owner);
                    if (q.Kind == RoomKind.Gate)
                    {
                        RoomMapFrame(q0.X - 7f, q0.Y - 7f, 14f, 14f, 2f, qi.WithAlpha(0.95f));
                        continue;
                    }
                    if (q.Kind == RoomKind.Link)
                    {
                        DrawLinkMark(q0, q1, qi, 0.85f);
                        continue;
                    }
                    if (q.Kind == RoomKind.Opening)
                    {
                        RoomMapRect(q0.X - 2f, q0.Y - 2f, q1.X - q0.X + 4f, q1.Y - q0.Y + 4f, qi.WithAlpha(0.9f));
                        continue;
                    }
                    RoomMapRect(q0.X, q0.Y, q1.X - q0.X, q1.Y - q0.Y, qi.WithAlpha(Expansion.IsPiece(q) || game.LegsOf(q.Owner).Count > 0 ? 0.22f : 0.14f));
                    if (q.Leg == 0 && game.LegsOf(q.Owner).Count == 0)  // (a room of several boxes shows as one fill, no seams)
                        RoomMapFrame(q0.X, q0.Y, q1.X - q0.X, q1.Y - q0.Y, 1.5f, qi.WithAlpha(0.85f));
                }
                if ((over || expDragging) && !expGateMode)  // the snapped cursor (a cross) and the corner already placed (a square)
                {
                    V2 cur = M(Snap(ev.mousePosition));
                    RoomMapRect(cur.X - 7f, cur.Y - 0.5f, 14f, 1f, Rgba.White.WithAlpha(0.8f));
                    RoomMapRect(cur.X - 0.5f, cur.Y - 7f, 1f, 14f, Rgba.White.WithAlpha(0.8f));
                }
                if (expDragging)
                {
                    V2 anchor = M(expA);
                    RoomMapRect(anchor.X - 4f, anchor.Y - 4f, 8f, 8f, new Rgba(1f, 0.85f, 0.4f, 0.95f));
                }
                if (hasCut)
                {
                    V2 p0 = M(new V2(cut.Vertical ? cut.At : cut.From, cut.Vertical ? cut.From : cut.At)), p1 = M(new V2(cut.Vertical ? cut.At : cut.Till, cut.Vertical ? cut.Till : cut.At));
                    var ink = cutRefund > 0 ? new Rgba(0.35f, 1f, 0.55f) : new Rgba(1f, 0.3f, 0.36f);
                    RoomMapRect(Mathf.Min(p0.X, p1.X) - 2.5f, Mathf.Min(p0.Y, p1.Y) - 2.5f, Mathf.Abs(p1.X - p0.X) + 5f, Mathf.Abs(p1.Y - p0.Y) + 5f, ink);
                }
                if (hasLink)
                    DrawLinkMark(M(expA), M(expB), linkPrice > 0 ? new Rgba(0.35f, 1f, 0.55f) : new Rgba(1f, 0.3f, 0.36f), 1f);
                foreach (var l in expLegs)  // the hallway's legs laid so far
                {
                    V2 l0 = M(new V2(l[0], l[1])), l1 = M(new V2(l[0] + l[2], l[1] + l[3]));
                    RoomMapRect(l0.X, l0.Y, l1.X - l0.X, l1.Y - l0.Y, new Rgba(1f, 0.85f, 0.4f, 0.3f));
                    RoomMapFrame(l0.X, l0.Y, l1.X - l0.X, l1.Y - l0.Y, 2f, new Rgba(1f, 0.85f, 0.4f, 0.95f));
                }
                if (roomMode)
                    DrawOutlineDraft(M, shape, laid, outlineRoom, cursor, over);
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
            foreach (var q in game.ProposalHeads)
            {
                Player qo;
                if (!game.Players.TryGetValue(q.Owner, out qo))
                    continue;
                V2 q0 = M(new V2(q.X, q.Y));
                Ui.Label(new Rect(q0.X + 3f, q0.Y + 2f, 160f, 14f), qo.Name + "  " + q.Price, 10, ToColor(OwnerInk(q.Owner)), TextAnchor.UpperLeft, true);
            }
            string line;
            Color lineInk;
            if (expGateMode && gateDoor >= 0)
            {
                line = gateWhy ?? "SHUTTER GATE ON THIS DOOR  ·  " + Gates.Price + " VECTORS (of " + game.SpendableVectors(game.Me) + ")  ·  click to propose it";
                lineInk = gateWhy == null ? Ui.Gold : new Color(1f, 0.45f, 0.5f);
            }
            else if (expGateMode)
            {
                line = "Point at a door (the gold squares)  ·  a gate shuts the doorway to everyone; shots still pass";
                lineInk = Ui.Muted;
            }
            else if (hasCut && cutRefund > 0)
            {
                line = "OPEN " + cut.Length / Expansion.Step + " TILES OF WALL  ·  +" + cutRefund + " VECTORS (you have " + game.SpendableVectors(game.Me) + " of " + (game.Me != null ? game.Me.VectorCap : 0) + ")  ·  click to " + (free ? "build it" : "propose it") + "";
                lineInk = Ui.Gold;
            }
            else if (hasLink && linkPrice > 0)
            {
                line = "PORTAL PAIR  ·  " + linkPrice + " VECTORS (of " + game.SpendableVectors(game.Me) + ")  ·  saves " + Mathf.RoundToInt(Expansion.WalkDistance(wd, link.A, link.B) / 100f) + " tiles of walking  ·  click to " + (free ? "build it" : "propose it") + "";
                lineInk = Ui.Gold;
            }
            else if (hasLink)
            {
                line = why ?? "";
                lineInk = new Color(1f, 0.45f, 0.5f);
            }
            else if (hasCut)
            {
                line = why ?? "";
                lineInk = new Color(1f, 0.45f, 0.5f);
            }
            else if (roomMode && outlineRoom != null)
            {
                int loops = 0;
                foreach (var pr in outlineRoom) loops += Expansion.LoopsOf(game.Expanded, pr);
                line = "ROOM  ·  " + Expansion.FloorTiles(outlineRoom[0], outlineRoom.GetRange(1, outlineRoom.Count - 1)) + " TILES OF FLOOR  ·  " + outlineRoom[0].Price + " VECTORS (of " + myVectors + ")" + (loops > 0 ? "  ·  CLOSES " + (loops == 1 ? "A LOOP" : loops + " LOOPS") : "") + (expPts.Count >= 3 ? "  ·  click the first corner or press Enter to propose it" : "");
                lineInk = Ui.Gold;
            }
            else if (roomMode && shape != null)
            {
                line = outlineWhy ?? "";
                lineInk = new Color(1f, 0.45f, 0.5f);
            }
            else if (roomMode && expPts.Count == 0)
            {
                line = "Your vectors " + myVectors + "  ·  click a corner to start  ·  a room costs its new wall: shared walls are free, so touching rooms is cheaper";
                lineInk = Ui.Muted;
            }
            else if (hasBox && box.HasValue && expHallMode)
            {
                line = "HALLWAY LEG " + (expLegs.Count + 1) + " OF " + Expansion.MaxLegs + "  ·  " + Mathf.Max(rw, rh) / 100 + " tiles  ·  " + hallPrice + " VECTORS IN ALL (of " + game.SpendableVectors(game.Me) + ")  ·  click to lay it";
                lineInk = Ui.Gold;
            }
            else if (expHallMode && !hasBox && hallSoFar != null)
            {
                line = expLegs.Count + " LEG" + (expLegs.Count == 1 ? "" : "S") + " LAID  ·  " + laidPrice + " VECTORS  ·  lay another from its end, or " + (free ? "build it" : "propose it");
                lineInk = hallReady ? Ui.Gold : new Color(1f, 0.45f, 0.5f);
            }
            else if (hasBox && box.HasValue)
            {
                int loops = Expansion.LoopsOf(game.Expanded, box.Value);
                line = (box.Value.Shape == "Hallway" ? "HALLWAY" : "ROOM") + " " + rw / 100 + " x " + rh / 100 + " tiles  ·  " + box.Value.Price + " VECTORS (of " + game.SpendableVectors(game.Me) + ")" + (loops > 0 ? "  ·  CLOSES " + (loops == 1 ? "A LOOP" : loops + " LOOPS") : "") + "  ·  click to " + (free ? "build it" : "propose it") + "";
                lineInk = Ui.Gold;
            }
            else if (hasBox)
            {
                line = why ?? "";
                lineInk = new Color(1f, 0.45f, 0.5f);
            }
            else
            {
                line = (free ? "Builds the moment you place it  ·  " : "") + (game.TeamPurse ? "Team purse " : "Your vectors ") + game.SpendableVectors(game.Me) + "  ·  a room costs its new wall: shared walls are free, so touching rooms is cheaper";
                lineInk = Ui.Muted;
            }
            if (expHallMode)
            {
                float hy = map.yMax + 4f;
                if (CrtUi.SoftKey(new Rect(W / 2f - 330f, hy, 400f, 32f), expLegs.Count == 0 ? "LAY THE FIRST LEG" : (free ? "BUILD HALLWAY  ·  " : "PROPOSE HALLWAY  ·  ") + laidPrice + " VECTORS", null, 17, calm, lit: hallReady) && hallReady)
                {
                    game.ProposeExpandHall(expLegs);
                    expDrawing = expDragging = false;
                    expLegs.Clear();
                }
                if (CrtUi.SoftKey(new Rect(W / 2f + 80f, hy, 250f, 32f), "UNDO LEG", null, 17, calm) && expLegs.Count > 0)
                    expLegs.RemoveAt(expLegs.Count - 1);
            }
            else if (!expCutMode && !expLinkMode && !expGateMode)
                DrawThemeRow(new Rect(map.x, map.yMax + 4f, map.width, 32f), calm);
            if (roomMode && expPts.Count > 0)
            {
                var closedLaid = Closed(new List<int[]>(expPts));
                var laidRoom = closedLaid.Count >= 4 ? Expansion.Outline(game.Expanded, closedLaid, expTheme, out string _) : null;
                bool can = laidRoom != null && laidRoom[0].Price <= myVectors;
                if (CrtUi.SoftKey(new Rect(map.xMax - 330f, map.y + 6f, 190f, 28f), can ? "PROPOSE  ·  " + laidRoom[0].Price : "PROPOSE", null, 14, calm, lit: can) && can)
                {
                    game.ProposeExpandOutline(closedLaid, expTheme);
                    expDrawing = false;
                    expPts.Clear();
                }
                if (CrtUi.SoftKey(new Rect(map.xMax - 134f, map.y + 6f, 128f, 28f), "UNDO", null, 14, calm) && expPts.Count > 0)
                    expPts.RemoveAt(expPts.Count - 1);
            }
            Ui.Label(new Rect(0, map.yMax + 44f, W, 22f), line, 15, lineInk, TextAnchor.MiddleCenter, true);
            if (CrtUi.SoftKey(new Rect(W / 2f - 120f, map.yMax + 72f, 240f, 34f), free ? "DONE" : "BACK", null, 20, calm))
            {
                expDrawing = expDragging = false;
                if (free)
                    expOpen = false;
            }
            CrtUi.End(win, t, calm);
        }

        bool expHeld;  // the mouse button of the box being drawn is still down
        Vector2 expPressAt;
        bool expCutMode;  // the draw screen opens a wall instead of drawing a room
        bool expLinkMode;  // the draw screen places a portal pair (Docs/RoomBuilding2.md step 2)
        bool expHallMode;  // the draw screen lays a hallway, leg by leg (Expansion.Hall)
        bool expGateMode;  // the draw screen fits a shutter gate to a door (Docs/RoomBuilding2.md step 3)
        readonly List<int[]> expLegs = new List<int[]>();  // its legs laid so far: x, y, w, h
        readonly List<int[]> expPts = new List<int[]>();   // the room outline's corners laid so far: x, y (Expansion.Outline)

        /// A wall from the last corner to (x, y): straight on, or an L (the longer way first) when the click is slanted.
        static void AddWall(List<int[]> pts, int x, int y)
        {
            if (pts.Count > 0)
            {
                var l = pts[pts.Count - 1];
                if (l[0] == x && l[1] == y)
                    return;
                if (l[0] != x && l[1] != y)
                    pts.Add(Mathf.Abs(x - l[0]) >= Mathf.Abs(y - l[1]) ? new[] { x, l[1] } : new[] { l[0], y });
            }
            pts.Add(new[] { x, y });
        }

        /// The corners with the walls back to the first one added.
        static List<int[]> Closed(List<int[]> pts)
        {
            var c = new List<int[]>(pts);
            if (c.Count < 2)
                return c;
            var f = c[0];
            AddWall(c, f[0], f[1]);
            if (c.Count > 1 && c[c.Count - 1][0] == f[0] && c[c.Count - 1][1] == f[1])
                c.RemoveAt(c.Count - 1);
            return c;
        }

        static List<int[]> BoxCorners(V2 a, V2 b) => new List<int[]>
        {
            new[] { (int)a.X, (int)a.Y }, new[] { (int)b.X, (int)a.Y }, new[] { (int)b.X, (int)b.Y }, new[] { (int)a.X, (int)b.Y },
        };

        /// The outline being drawn: its floor (green when it fits, the pieces it would be built from), the walls laid (bright), the wall to
        /// the cursor and the way back to the first corner (dim), each corner, and the first corner ringed when clicking it would close it.
        void DrawOutlineDraft(System.Func<V2, V2> M, List<int[]> shape, List<int[]> laid, List<ExpansionRoom> room, V2 cursor, bool over)
        {
            if (shape == null)
                return;
            var ink = room != null ? new Rgba(0.35f, 1f, 0.55f) : new Rgba(1f, 0.3f, 0.36f);
            if (room != null)
                foreach (var r in room)
                {
                    V2 p0 = M(new V2(r.X, r.Y)), p1 = M(new V2(r.X1, r.Y1));
                    RoomMapRect(p0.X, p0.Y, p1.X - p0.X, p1.Y - p0.Y, ink.WithAlpha(0.22f));
                }
            int solid = laid == null ? shape.Count : expPts.Count - 1;  // walls up to here are laid; the rest follow the cursor
            for (int i = 0; i < shape.Count; i++)
            {
                if (laid != null && i == shape.Count - 1 && shape.Count < 3)
                    break;
                V2 a = M(new V2(shape[i][0], shape[i][1])), b = M(new V2(shape[(i + 1) % shape.Count][0], shape[(i + 1) % shape.Count][1]));
                bool firm = i < solid;
                float th = firm ? 2.5f : 1.5f;
                RoomMapRect(Mathf.Min(a.X, b.X) - th / 2f, Mathf.Min(a.Y, b.Y) - th / 2f, Mathf.Abs(b.X - a.X) + th, Mathf.Abs(b.Y - a.Y) + th, ink.WithAlpha(firm ? 1f : 0.5f));
            }
            for (int i = 0; i < expPts.Count; i++)
            {
                V2 c = M(new V2(expPts[i][0], expPts[i][1]));
                float s = i == 0 ? 5f : 3f;
                RoomMapRect(c.X - s, c.Y - s, 2f * s, 2f * s, i == 0 ? new Rgba(1f, 0.85f, 0.4f, 0.95f) : Rgba.White.WithAlpha(0.9f));
            }
            if (over && expPts.Count >= 3 && expPts[0][0] == (int)cursor.X && expPts[0][1] == (int)cursor.Y)
                Arc(M(cursor), 10f, 2f, new Rgba(1f, 0.85f, 0.4f, 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 6f)), 20);
        }

        /// A portal pair on a map: a ring at each end and a dashed line between them.
        void DrawLinkMark(V2 a, V2 b, Rgba ink, float alpha)
        {
            float pulse = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 3f);
            V2 d = b - a;
            float len = d.Length;
            if (len > 1f)
            {
                V2 u = d * (1f / len);
                for (float s = 8f; s < len - 8f; s += 10f)
                {
                    V2 p = a + u * s, q = a + u * Mathf.Min(s + 5f, len - 8f);
                    Line(p, q, 1.5f, ink.WithAlpha(0.55f * alpha));
                }
            }
            Arc(a, 6f, 2.5f, ink.WithAlpha(alpha * pulse), 16);
            Arc(b, 6f, 2.5f, ink.WithAlpha(alpha * pulse), 16);
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
            bool afford = owner != null && o.Price <= game.SpendableVectors(owner);
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
            int loops = o.Kind == RoomKind.Link || o.Kind == RoomKind.Opening || o.Kind == RoomKind.Gate ? 0 : Expansion.LoopsOf(game.Expanded, o);
            var legs = game.LegsOf(o.Owner);
            bool drawn = legs.Count > 0 && Expansion.IsPiece(legs[0]);  // a room drawn as an outline
            int hallTiles = Mathf.Max(o.W, o.H) / 100;
            foreach (var lg in legs)
                hallTiles += Mathf.Max(lg.W, lg.H) / 100;
            bool mineCard = o.Owner == game.MyId;
            CrtUi.Text(new Rect(r.x + 22f, ty, r.width - 44f, 34f), (mineCard ? "YOUR " : "") + (o.Kind == RoomKind.Gate ? "SHUTTER GATE" : o.Kind == RoomKind.Link ? "PORTAL PAIR" : o.Kind == RoomKind.Opening ? "OPEN A WALL" : (o.Kind == RoomKind.Floor ? Paths.Get(o.Floor).Name.ToUpperInvariant() + " " : "") + (drawn ? "DRAWN ROOM" : legs.Count > 0 ? "BENT HALLWAY" : o.Shape.ToUpperInvariant())), r.width < 330f ? 22 : 28, ink, TextAnchor.MiddleLeft, true, calm);
            CrtUi.Text(new Rect(r.x + 22f, ty + 34f, r.width - 44f, 22f), (owner != null ? "DRAWN BY " + owner.Name.ToUpperInvariant() : "DRAWN") + "  ·  " + (o.Kind == RoomKind.Gate ? "on a door" : o.Kind == RoomKind.Link ? "saves " + Mathf.RoundToInt(Expansion.WalkDistance(game.ExpansionWorld, Expansion.LinkOf(o).A, Expansion.LinkOf(o).B) / 100f) + " tiles of walking" : o.Kind == RoomKind.Opening ? (o.W + o.H) / 100 + " tiles of wall" : drawn ? Expansion.FloorTiles(o, legs) + " tiles of floor" + (loops > 0 ? "  ·  LOOP" : "") : legs.Count > 0 ? (legs.Count + 1) + " legs, " + hallTiles + " tiles long" : o.W / 100 + " x " + o.H / 100 + " tiles" + (loops > 0 ? "  ·  LOOP" : "")), 15, afford ? CrtUi.Ink : dim, TextAnchor.MiddleLeft, false, calm);
            string blurb = o.Kind == RoomKind.Gate ? "A red laser grid on this door. Stand by it and press " + Keybinds.KeyName(Keybinds.Get(KeyAction.TakeItem)).ToUpperInvariant() + " to shut it for " + (int)Gates.CloseTime + " s and split a crowd." : o.Kind == RoomKind.Link ? "Two portals: step into one and come out of the other. Enemies use them too." : o.Kind == RoomKind.Opening ? "Takes this stretch of wall down: the rooms either side open into one, and the proposer gets the vectors back." : o.Kind == RoomKind.Floor ? FloorBlurb(o.Floor) : loops > 0 ? "It joins " + (loops + 1) + " rooms, so the map loops round." : "More floor to fight on, and more enemies come with it.";
            CrtUi.Text(new Rect(r.x + 22f, ty + 62f, r.width - 44f, 70f), blurb, 14, dim, TextAnchor.UpperLeft, false, calm, true);
            int votes = game.VotesFor(o.Owner);
            CrtUi.Text(new Rect(r.x + 22f, r.yMax - 82f, r.width - 44f, 30f), (o.Price < 0 ? "+" + -o.Price : o.Price.ToString()) + " VECTORS" + (owner != null ? "  ·  " + owner.Name.ToUpperInvariant() + " HAS " + owner.Vectors : ""), r.width < 330f ? 17 : 20, afford ? new Color(0.37f, 0.94f, 1f) : dim, TextAnchor.MiddleLeft, true, calm);
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
            bool isCut = o.Kind == RoomKind.Opening || o.Kind == RoomKind.Link || o.Kind == RoomKind.Gate;  // (none adds a room)
            var all = new List<ExpansionRoom>(game.Expanded);
            int added = 0;
            if (!isCut)
            {
                all.Add(o);
                var legs = game.LegsOf(o.Owner);  // (a bent hallway's other legs)
                all.AddRange(legs);
                added = 1 + legs.Count;
            }
            var wd = Expansion.Build(all, game.Openings, game.Links);
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
                bool isNew = i >= wd.Areas.Length - added;
                V2 p0 = M(a.Min), p1 = M(a.Max);
                Rgba fill, line;
                if (isNew) { fill = ink.WithAlpha((afford ? 0.3f : 0.12f) * pulse); line = ink.WithAlpha(afford ? 1f : 0.45f); }
                else if (a.Hub) { fill = new Rgba(0.4f, 0.64f, 1f, 0.16f); line = new Rgba(0.4f, 0.64f, 1f, 0.8f); }
                else if (a.Owner != 0 && a.Kind == RoomKind.Combat) { fill = OwnerInk(a.Owner).WithAlpha(0.14f); line = OwnerInk(a.Owner).WithAlpha(0.7f); }
                else { fill = KindInk(a.Kind, a.Floor).WithAlpha(a.Kind == RoomKind.Combat ? 0.08f : 0.16f); line = KindInk(a.Kind, a.Floor).WithAlpha(0.5f); }
                RoomMapRect(p0.X, p0.Y, p1.X - p0.X, p1.Y - p0.Y, fill);
                RimFrame(wd, i, M, isNew ? 2f : 1f, line);
            }
            for (int di = 0; di < wd.Doors.Length; di++)
            {
                var d = wd.Doors[di];
                if (wd.Seam(di))
                    continue;  // (inside one room: a bent hallway's open corner, an outline's seam)
                V2 c = M(d.Pos);
                RoomMapRect(c.X - 3f, c.Y - 3f, 6f, 6f, !isCut && (d.A >= wd.Areas.Length - added) != (d.B >= wd.Areas.Length - added) ? Rgba.White.WithAlpha(0.9f) : new Rgba(0.37f, 0.94f, 1f, 0.7f));
            }
            if (o.Kind == RoomKind.Gate)  // the door it would go on
            {
                V2 gc = M(new V2(o.X, o.Y));
                RoomMapFrame(gc.X - 8f, gc.Y - 8f, 16f, 16f, 2.5f, new Rgba(1f, 0.3f, 0.32f, (afford ? 1f : 0.5f) * pulse));
            }
            else if (o.Kind == RoomKind.Link)  // the two portals it would place
                DrawLinkMark(M(Expansion.LinkOf(o).A), M(Expansion.LinkOf(o).B), ink.WithAlpha(afford ? 1f : 0.5f), 1f);
            else if (isCut)  // the stretch of wall it would open
            {
                var c = Expansion.CutOf(o);
                V2 c0 = M(new V2(c.Vertical ? c.At : c.From, c.Vertical ? c.From : c.At)), c1 = M(new V2(c.Vertical ? c.At : c.Till, c.Vertical ? c.Till : c.At));
                RoomMapRect(Mathf.Min(c0.X, c1.X) - 2f, Mathf.Min(c0.Y, c1.Y) - 2f, Mathf.Abs(c1.X - c0.X) + 4f, Mathf.Abs(c1.Y - c0.Y) + 4f, ink.WithAlpha((afford ? 1f : 0.5f) * pulse));
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
