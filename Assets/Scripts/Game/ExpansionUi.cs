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

        static string KindName(ExpansionRoom r) => r.Kind == RoomKind.Shop ? "Shop" : r.Kind == RoomKind.Vendor ? "Black Market" : r.Kind == RoomKind.Gatekeeper ? "Gatekeeper" : r.Kind == RoomKind.Floor ? Paths.Get(r.Floor).Name : "Combat";

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
        bool ShopScreenUp(Player me) => game.ShopOpen && me != null && (!game.ServiceRooms || game.InServiceRoom(me) && !me.ShopDone) && !BeamShowing;  // (not over the boss beam-in: BossBeamLook.cs)

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
        bool ExpandWindowUp => game.CanCut && expOpen && !BeamShowing;  // (not over the boss beam-in: BossBeamLook.cs)
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
                Ui.Label(new Rect(0, y + 180f, W, 40f), "No proposals yet. Draw a room and put it to the team.", 20, Ui.Muted, TextAnchor.MiddleCenter);
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
            TipClear();
            bool free = game.FreeBuild, doorsOnly = !game.ExpandOpen;  // doorsOnly: free building with a wave on, walls only
            if (doorsOnly)
            {
                expCutMode = true;
                expLinkMode = expGateMode = expTrapMode = false;
            }
            DrawBuildRail(doorsOnly, free, calm);
            bool roomMode = !expCutMode && !expLinkMode && !expGateMode && !expTrapMode;
            if (!roomMode)
                expPts.Clear();
            var map = DrawMapRect;
            var wd = game.ExpansionWorld;
            float pad = 1300f;
            V2 lo = wd.Min - new V2(pad, pad), hi = wd.Max + new V2(pad, pad);
            IncludeNodes(ref lo, ref hi);
            float k0 = Mathf.Min(map.width / (hi.X - lo.X), map.height / (hi.Y - lo.Y));
            var ev = Event.current;
            // Zoom: mouse wheel (about the pointer), + / - keys, middle-drag to pan, the buttons under the map; FIT resets.
            if (ev.type == EventType.ScrollWheel && map.Contains(ev.mousePosition))
            {
                float z0 = expZoom;
                expZoom = Mathf.Clamp(expZoom * Mathf.Pow(1.2f, -ev.delta.y), 1f, 8f);
                V2 under = new V2(expViewC.X + (ev.mousePosition.x - map.center.x) / (k0 * z0), expViewC.Y + (ev.mousePosition.y - map.center.y) / (k0 * z0));
                expViewC = new V2(under.X - (ev.mousePosition.x - map.center.x) / (k0 * expZoom), under.Y - (ev.mousePosition.y - map.center.y) / (k0 * expZoom));
                ev.Use();
            }
            else if (ev.type == EventType.KeyDown && (ev.character == '+' || ev.character == '=' || ev.character == '-' || ev.character == '_'))
            {
                expZoom = Mathf.Clamp(expZoom * (ev.character == '-' || ev.character == '_' ? 1f / 1.4f : 1.4f), 1f, 8f);
                ev.Use();
            }
            else if (ev.type == EventType.MouseDown && ev.button == 2 && map.Contains(ev.mousePosition))
            {
                expPan = true;
                ev.Use();
            }
            else if (ev.type == EventType.MouseDrag && expPan)
            {
                expViewC = new V2(expViewC.X - ev.delta.x / (k0 * expZoom), expViewC.Y - ev.delta.y / (k0 * expZoom));
                ev.Use();
            }
            else if (ev.type == EventType.MouseUp && ev.button == 2)
                expPan = false;
            if (expZoom <= 1.001f) { expZoom = 1f; expViewC = (lo + hi) * 0.5f; }
            expViewC = new V2(Mathf.Clamp(expViewC.X, lo.X, hi.X), Mathf.Clamp(expViewC.Y, lo.Y, hi.Y));
            float k = k0 * expZoom;
            float ox = map.center.x - (expViewC.X - lo.X) * k, oy = map.center.y - (expViewC.Y - lo.Y) * k;
            System.Func<V2, V2> M = p => new V2(ox + (p.X - lo.X) * k, oy + (p.Y - lo.Y) * k);
            System.Func<Vector2, V2> Snap = m => new V2(Mathf.Round(((m.x - ox) / k + lo.X) / Expansion.Step) * Expansion.Step, Mathf.Round(((m.y - oy) / k + lo.Y) / Expansion.Step) * Expansion.Step);
            if (CrtUi.SoftKey(new Rect(map.xMax - 150f, map.yMax - 34f, 40f, 28f), "-", null, 18, calm)) expZoom = Mathf.Max(1f, expZoom / 1.4f);
            if (CrtUi.SoftKey(new Rect(map.xMax - 106f, map.yMax - 34f, 40f, 28f), "+", null, 18, calm)) expZoom = Mathf.Min(8f, expZoom * 1.4f);
            if (CrtUi.SoftKey(new Rect(map.xMax - 62f, map.yMax - 34f, 56f, 28f), "FIT", null, 14, calm)) expZoom = 1f;
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
            V2 trapSpot = cursor;  // trap mode: where a click would put the trap, and whether it fits
            string trapWhy = null;
            bool trapOk = expTrapMode && over && game.Me != null && game.Me.Stash[expTrapKind] > 0 && Expansion.PlaceSpot(wd, game.Placed, trapSpot, out trapWhy);
            if (expTrapMode && over && trapWhy == null && (game.Me == null || game.Me.Stash[expTrapKind] == 0))
                trapWhy = "None in your stash: buy one at the Black Market";
            bool closeRoom = false, laidWall = false;
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
                    {
                        AddWall(expPts, (int)cursor.X, (int)cursor.Y);
                        laidWall = true;
                    }
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
                else
                {
                    expDrawing = false;
                    if (free)
                        expOpen = false;
                }
                ev.Use();
            }
            else if (expTrapMode && ev.type == EventType.MouseDown && ev.button == 0 && over)
            {
                if (trapOk)
                    game.PlaceBuild((BuildKind)expTrapKind, (int)trapSpot.X, (int)trapSpot.Y);
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
            int rx = (int)Mathf.Min(expA.X, expB.X), ry = (int)Mathf.Min(expA.Y, expB.Y), rw = (int)Mathf.Abs(expA.X - expB.X), rh = (int)Mathf.Abs(expA.Y - expB.Y);
            // The outline as it stands: the corners laid, the wall to the cursor, and back to the first corner (or the box being dragged).
            List<int[]> shape = null, laid = null;
            List<ExpansionRoom> outlineRoom = null;
            string outlineWhy = null;
            bool fillDraft = false;  // the walls run from one wall to another and fence a space in (no closing back to the first corner)
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
                    // Ending on another room's wall: the map's walls may close it off, and the space fenced in is the room (Expansion.Fill).
                    if (laid.Count >= 2 && Expansion.OnWall(game.Expanded, laid[laid.Count - 1]))
                    {
                        var filled = Expansion.Fill(game.Expanded, laid, expTheme, out string fillWhy);
                        if (filled != null)
                        {
                            fillDraft = true;
                            shape = laid;
                            outlineRoom = filled;
                            if (filled[0].Price > myVectors) { outlineWhy = "Can't afford it: " + filled[0].Price + " vectors, you have " + myVectors; outlineRoom = null; }
                        }
                    }
                }
                if (fillDraft)
                {
                    if (outlineRoom != null && (laidWall || closeRoom))  // the wall just reached another wall and fences a space in: propose it
                    {
                        game.ProposeExpandOutline(expPts, expTheme, true);
                        expDrawing = false;
                        expPts.Clear();
                        outlineRoom = null;
                        shape = null;
                    }
                }
                else if (shape != null && shape.Count >= 4)
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
            bool hasBox = !roomMode && !expCutMode && !expLinkMode && !expGateMode && !expTrapMode && rw > 0 && rh > 0;
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
                if (linkPrice > 0 && (game.Me == null || game.Me.Stash[(int)BuildKind.Teleporter] == 0)) { why = "No teleporter in your stash: buy one at the Black Market"; linkPrice = 0; }
            }
            if (hasCut)
            {
                cut.Vertical = rh > rw;
                cut.At = (short)(cut.Vertical ? expA.X : expA.Y);
                cut.From = (short)(cut.Vertical ? ry : rx);
                cut.Till = (short)(cut.Vertical ? ry + rh : rx + rw);
                cutRefund = Expansion.CutRefund(game.Expanded, game.Openings, cut, out why);
            }
            if (hasBox)
            {
                box = Expansion.Custom(game.Expanded, rx, ry, rw, rh, expTheme, out why);
                int have = game.SpendableVectors(game.Me);
                if (box.HasValue && box.Value.Price > have) { why = "Can't afford it: " + box.Value.Price + " vectors, you have " + have; box = null; }
            }
            if (confirm)
            {
                if (box.HasValue)
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
                roomClip = map;
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
                    DrawHoloGrid(a, M, k, ink.WithAlpha(ghost));
                    if (a.Floating || a.Sealed)  // (a built room's outline is its walls, drawn below: a rim would run across the door gaps)
                        RimFrame(wd, ai, M, 1.5f, ink.WithAlpha(0.8f * ghost));
                }
                DrawHoloSolids(wd, M, k, map);  // walls, fins and cover over the floors (BuildMapLook.cs)
                foreach (var nd in game.Nodes)  // the nodes still waiting for a room built round them (claimed ones are part of their room)
                    if (!Expansion.NodeClaimed(game.Expanded, nd))
                    {
                        V2 np = M(nd.Pos);
                        var ni = NodeInk(nd.Kind);
                        RoomMapRect(np.X - 6f, np.Y - 6f, 12f, 12f, ni.WithAlpha(0.55f + 0.3f * Mathf.Sin(Time.unscaledTime * 2.5f)));
                        RoomMapFrame(np.X - 9f, np.Y - 9f, 18f, 18f, 1.5f, ni);
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
                foreach (var pb in game.Placed)  // the Black Market traps standing on the map
                {
                    V2 bp = M(pb.Pos);
                    var bi = VendorInk;
                    RoomMapFrame(bp.X - 7f, bp.Y - 7f, 14f, 14f, 1.5f, bi);
                    RoomMapRect(bp.X - 3f, bp.Y - 3f, 6f, 6f, bi.WithAlpha(0.8f));
                }
                if (expTrapMode && over)  // the trap about to go down: green where it fits, red where it doesn't
                {
                    V2 bp = M(trapSpot);
                    var bi = trapOk ? new Rgba(0.35f, 1f, 0.55f) : new Rgba(1f, 0.3f, 0.36f);
                    RoomMapRect(bp.X - 8f, bp.Y - 8f, 16f, 16f, bi.WithAlpha(0.3f));
                    RoomMapFrame(bp.X - 8f, bp.Y - 8f, 16f, 16f, 2f, bi);
                }
                DrawHoloGates(wd, M, k);  // the gates fitted (BuildMapLook.cs)
                DrawHoloBreaches(M, k, map);  // this wave's breaches
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
                if (roomMode)
                    DrawOutlineDraft(M, shape, laid, outlineRoom, cursor, over, fillDraft);
                if (hasBox)
                {
                    V2 p0 = M(new V2(rx, ry)), p1 = M(new V2(rx + rw, ry + rh));
                    var ink = box.HasValue ? new Rgba(0.35f, 1f, 0.55f) : new Rgba(1f, 0.3f, 0.36f);
                    RoomMapRect(p0.X, p0.Y, p1.X - p0.X, p1.Y - p0.Y, ink.WithAlpha(0.25f));
                    RoomMapFrame(p0.X, p0.Y, p1.X - p0.X, p1.Y - p0.Y, 2f, ink);
                }
                roomClip = null;
                EndUiGl();
            }
            CrtKit.Frame(map, 1f, new Color(CrtUi.Ink.r, CrtUi.Ink.g, CrtUi.Ink.b, 0.25f));
            foreach (var nd in game.Nodes)  // the nodes: named in a hover tip, not on the map (the legend shows the colours)
                if (!Expansion.NodeClaimed(game.Expanded, nd))
                {
                    V2 n0 = M(nd.Pos);
                    var nr = new Rect(n0.X - 12f, n0.Y - 12f, 24f, 24f);
                    if (map.Contains(Event.current.mousePosition) && TipHover(nr))
                    {
                        TipStart(nr, nd.Kind.ToString(), ToColor(NodeInk(nd.Kind)));
                        TipAdd("Build a room round it to claim it.", 12, Ui.Text);
                    }
                }
            foreach (var fa in wd.Areas)  // the floating rooms: named in a hover tip too
                if (fa.Floating)
                {
                    V2 f0 = M(fa.Min), f1 = M(fa.Max);
                    var fr = new Rect(f0.X, f0.Y, f1.X - f0.X, f1.Y - f0.Y);
                    if (!map.Contains(Event.current.mousePosition) || !TipHover(fr))
                        continue;
                    TipStart(fr, fa.Name, ToColor(KindInk(fa.Kind, fa.Floor)));
                    if (fa.Sealed)
                        TipAdd("SEALED  ·  build a room up to it to claim it.", 12, Ui.Text);
                }
            foreach (var q in game.ProposalHeads)
            {
                Player qo;
                if (!game.Players.TryGetValue(q.Owner, out qo))
                    continue;
                V2 q0 = M(new V2(q.X, q.Y));
                if (!map.Contains(new Vector2(q0.X + 3f, q0.Y + 2f))) continue;
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
                line = "A gate shuts its doorway to everyone. Shots still pass.";
                lineInk = Ui.Muted;
            }
            else if (hasCut && cutRefund > 0)
            {
                line = "OPEN " + cut.Length / Expansion.Step + " TILES OF WALL  ·  +" + cutRefund + " VECTORS (you have " + game.SpendableVectors(game.Me) + " of " + (game.Me != null ? game.Me.VectorCap : 0) + ")  ·  click to " + (free ? "build it" : "propose it") + "";
                lineInk = Ui.Gold;
            }
            else if (expTrapMode)
            {
                line = trapWhy != null ? trapWhy : trapOk ? Market.Names[expTrapKind].ToUpperInvariant() + "  ·  click to place it  ·  " + game.Me.Stash[expTrapKind] + " in your stash" : "Move over a room to place a " + Market.Names[expTrapKind].ToLowerInvariant() + ".";
                lineInk = trapWhy != null ? new Color(1f, 0.45f, 0.5f) : trapOk ? Ui.Gold : Ui.Muted;
            }
            else if (hasLink && linkPrice > 0)
            {
                line = "TELEPORTER PAIR  ·  from your stash  ·  saves " + Mathf.RoundToInt(Expansion.WalkDistance(wd, link.A, link.B) / 100f) + " tiles of walking  ·  click to build it";
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
                line = "ROOM  ·  " + Expansion.FloorTiles(outlineRoom[0], outlineRoom.GetRange(1, outlineRoom.Count - 1)) + " TILES OF FLOOR  ·  " + outlineRoom[0].Price + " VECTORS (of " + myVectors + ")" + (loops > 0 ? "  ·  CLOSES " + (loops == 1 ? "A LOOP" : loops + " LOOPS") : "") + (fillDraft ? "  ·  FENCED IN BY THE WALLS AROUND IT: click to propose it" : expPts.Count >= 3 ? "  ·  click the first corner or press Enter to propose it" : "");
                lineInk = Ui.Gold;
            }
            else if (roomMode && shape != null)
            {
                line = outlineWhy ?? "";
                lineInk = new Color(1f, 0.45f, 0.5f);
            }
            else if (roomMode && expPts.Count == 0)
            {
                line = free ? "Builds the moment you close it." : "Goes to the team vote when you close it.";
                lineInk = Ui.Muted;
            }
            else if (hasBox && box.HasValue)
            {
                int loops = Expansion.LoopsOf(game.Expanded, box.Value);
                line = "ROOM " + rw / 100 + " x " + rh / 100 + " tiles  ·  " + box.Value.Price + " VECTORS (of " + game.SpendableVectors(game.Me) + ")" + (loops > 0 ? "  ·  CLOSES " + (loops == 1 ? "A LOOP" : loops + " LOOPS") : "") + "  ·  click to " + (free ? "build it" : "propose it") + "";
                lineInk = Ui.Gold;
            }
            else if (hasBox)
            {
                line = why ?? "";
                lineInk = new Color(1f, 0.45f, 0.5f);
            }
            else
            {
                line = free ? "Builds the moment you place it." : "Goes to the team vote when you place it.";
                lineInk = Ui.Muted;
            }
            int pending = 0;  // what the thing being drawn costs (a refund is negative), for the wallet bar
            if (roomMode && outlineRoom != null) pending = outlineRoom[0].Price;
            else if (hasBox && box.HasValue) pending = box.Value.Price;
            else if (hasCut && cutRefund > 0) pending = -cutRefund;
            else if (expGateMode && gateDoor >= 0 && gateWhy == null) pending = Gates.Price;
            var panel = new Rect(map.xMax + 16f, map.y, win.xMax - map.xMax - 32f, H - 30f - map.y);
            float py = DrawBuildPanelTop(panel, doorsOnly, roomMode, pending, gateDoor);
            if (!lineInk.Equals(Ui.Muted) && !string.IsNullOrEmpty(line))  // (the standing hints are hover tips now: only live feedback shows here)
            {
                DkSub(new Rect(panel.x + 10f, py, panel.width - 20f, 74f), new Color(0f, 0f, 0f, 0.35f));
                Ui.Label(new Rect(panel.x + 18f, py + 4f, panel.width - 36f, 66f), line, 13, lineInk, TextAnchor.MiddleLeft, true, true);
                py += 84f;
            }
            float bx = panel.x + 10f, bw = panel.width - 20f;
            float actY = panel.yMax - 98f;  // the action keys sit just above DONE
            if (!expCutMode && !expLinkMode && !expGateMode && !expTrapMode)
                DrawThemeGrid(new Rect(bx, py, bw, actY - 46f - py), calm);
            if (roomMode && expPts.Count > 0)
            {
                bool laidFill = expPts.Count >= 2 && Expansion.OnWall(game.Expanded, expPts[expPts.Count - 1]);
                var closedLaid = laidFill ? new List<int[]>(expPts) : Closed(new List<int[]>(expPts));
                var laidRoom = laidFill ? Expansion.Fill(game.Expanded, closedLaid, expTheme, out string fw0) : null;
                if (laidRoom == null)
                {
                    laidFill = false;
                    closedLaid = Closed(new List<int[]>(expPts));
                    laidRoom = closedLaid.Count >= 4 ? Expansion.Outline(game.Expanded, closedLaid, expTheme, out string fw1) : null;
                }
                bool can = laidRoom != null && laidRoom[0].Price <= myVectors;
                float hw = (bw - 6f) / 2f;
                if (CrtUi.SoftKey(new Rect(bx, actY, hw + 30f, 36f), can ? (free ? "BUILD  ·  " : "PROPOSE  ·  ") + laidRoom[0].Price : free ? "BUILD" : "PROPOSE", null, 15, calm, lit: can) && can)
                {
                    game.ProposeExpandOutline(closedLaid, expTheme, laidFill);
                    expDrawing = false;
                    expPts.Clear();
                }
                if (CrtUi.SoftKey(new Rect(bx + hw + 36f, actY, hw - 30f, 36f), "UNDO", null, 15, calm) && expPts.Count > 0)
                    expPts.RemoveAt(expPts.Count - 1);
            }
            if (CrtUi.SoftKey(new Rect(bx, panel.yMax - 50f, bw, 42f), free ? "DONE" : "BACK", null, 22, calm))
            {
                expDrawing = expDragging = false;
                if (free)
                    expOpen = false;
            }
            Ui.Label(new Rect(map.x, map.yMax + 6f, map.width, 20f), "SCROLL  zoom   ·   MIDDLE DRAG  pan   ·   RIGHT CLICK  undo   ·   ESC  close", 12, Ui.Muted, TextAnchor.MiddleLeft, true);
            DrawTip(true);
            CrtUi.End(win, t, calm);
        }

        float expZoom = 1f;  // the drawing map's zoom (1 = the whole map)
        V2 expViewC;  // the world point at the middle of the zoomed drawing map
        bool expPan;  // middle mouse is panning it
        bool expHeld;  // the mouse button of the box being drawn is still down
        Vector2 expPressAt;
        bool expCutMode;  // the draw screen opens a wall instead of drawing a room
        bool expLinkMode;  // the draw screen places a portal pair (Docs/RoomBuilding2.md step 2): a teleporter from the stash (Market.cs)
        bool expTrapMode;  // the draw screen places a trap from the stash (Market.cs), one click
        int expTrapKind = (int)BuildKind.SpikeTrap;
        bool expGateMode;  // the draw screen fits a shutter gate to a door (Docs/RoomBuilding2.md step 3)
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
        void DrawOutlineDraft(System.Func<V2, V2> M, List<int[]> shape, List<int[]> laid, List<ExpansionRoom> room, V2 cursor, bool over, bool open = false)
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
                if (laid != null && i == shape.Count - 1 && (shape.Count < 3 || open))
                    break;  // (an open run of walls has no way back to its first corner)
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
            float spin = Time.unscaledTime * 2f;
            foreach (var e in new[] { a, b })  // each end: a ring and a turning inner arc (the portal in the arena)
            {
                Arc(e, 7f, 2f, ink.WithAlpha(alpha * pulse), 18);
                Arc(e, 3.5f, 1.5f, ink.WithAlpha(alpha), 12, spin, 4f);
                spin += 3f;
            }
        }
        PathId expTheme = PathId.None;  // the theme the next drawn room carries

        /// The themes you can draw a room with: Plain, then each path as a room (its extra vectors; locked until enough bosses are down).
        /// The draw screen's map area: between the tool rail on the left and the guide panel on the right.
        Rect DrawMapRect => new Rect(222f, 34f, W - 222f - 290f, H - 34f - 74f);

        /// The draw screen's tool rail: the title, the four tools (keys 1-4), the kit key and what the map's colours mean.
        void DrawBuildRail(bool doorsOnly, bool free, bool calm)
        {
            float x = 48f, w = 158f;
            CrtUi.Title(new Rect(x, 26f, w, 40f), "BUILD", 32, CrtUi.Ink, TextAnchor.MiddleLeft, calm);
            Ui.Label(new Rect(x + 2f, 64f, w, 16f), doorsOnly ? "WAVE ON  ·  DOORS ONLY" : free ? "BUILT AS YOU PLACE IT" : "THE TEAM VOTES", 10, doorsOnly ? Ui.Gold : Ui.Muted, TextAnchor.MiddleLeft, true);
            string[] names = { "ROOM", "OPEN WALL", "TELEPORTER", "GATE", "TRAP" };
            int cur = expTrapMode ? 4 : expGateMode ? 3 : expLinkMode ? 2 : expCutMode ? 1 : 0;
            var ev = Event.current;
            int pick = -1;
            if (!doorsOnly && ev.type == EventType.KeyDown && ev.keyCode >= KeyCode.Alpha1 && ev.keyCode <= KeyCode.Alpha5)
            {
                pick = ev.keyCode - KeyCode.Alpha1;
                ev.Use();
            }
            for (int i = 0; i < names.Length; i++)
            {
                var r = new Rect(x, 92f + i * 42f, w, 36f);
                bool on = i == cur;
                if (doorsOnly && i != 1)
                {
                    Ui.Pill(r, new Color(0f, 0f, 0f, 0.4f));
                    Ui.Label(r, names[i], 14, new Color(1f, 1f, 1f, 0.25f), TextAnchor.MiddleCenter, true);
                    continue;
                }
                if (CrtUi.SoftKey(r, names[i], null, 15, calm, lit: on) && !doorsOnly)
                    pick = i;
                Ui.Label(new Rect(r.x + 8f, r.y, 14f, r.height), (i + 1).ToString(), 10, on ? Ui.Gold : Ui.Muted, TextAnchor.MiddleLeft, true);
                if (TipHover(r))
                {
                    string tt, tp;
                    string[] st;
                    BuildToolHelp(i, doorsOnly, out tt, out st, out tp);
                    TipStart(r, tt, CrtUi.Ink);
                    for (int si = 0; si < st.Length; si++)
                        TipAdd((si + 1) + "  " + st[si], 12, Ui.Text);
                    TipAdd(tp, 11, Ui.Muted);
                }
            }
            if (pick >= 0 && pick != cur)
            {
                expCutMode = pick == 1;
                expLinkMode = pick == 2;
                expGateMode = pick == 3;
                expTrapMode = pick == 4;
                expDragging = false;
                expA = expB = V2.Zero;
            }
            float y = 92f + names.Length * 42f + 6f;
            if (expTrapMode && !doorsOnly && game.Me != null)  // the traps in your stash: pick which one a click places
            {
                for (int tk = 1; tk < Market.Count; tk++)
                {
                    if (CrtUi.SoftKey(new Rect(x, y, w, 28f), Market.Names[tk].ToUpperInvariant() + "  ·  " + game.Me.Stash[tk], null, 12, calm, lit: tk == expTrapKind))
                        expTrapKind = tk;
                    y += 32f;
                }
                y += 4f;
            }
            if (free && !doorsOnly && game.RoomTokens.Count > 0)  // (free building has no vote window to hold the kit key)
            {
                if (CrtUi.SoftKey(new Rect(x, y, w, 32f), "ROOM KITS  ·  " + game.RoomTokens.Count, null, 14, calm))
                {
                    expModify = true;
                    expKit = 0;
                }
                y += 42f;
            }
            // The legend: what the colours on the map are.
            var items = new List<KeyValuePair<string, Rgba>> { new KeyValuePair<string, Rgba>("YOUR MAP", new Rgba(0.4f, 0.64f, 1f)), new KeyValuePair<string, Rgba>("DOOR", new Rgba(0.37f, 0.94f, 1f)) };
            var seen = new HashSet<NodeKind>();
            foreach (var nd in game.Nodes)
                if (!Expansion.NodeClaimed(game.Expanded, nd) && seen.Add(nd.Kind))
                    items.Add(new KeyValuePair<string, Rgba>(nd.Kind.ToString().ToUpperInvariant() + " NODE", NodeInk(nd.Kind)));
            items.Add(new KeyValuePair<string, Rgba>("WALL", HoloWallInk));  // the map's floor (BuildMapLook.cs): walls, cover, and what's on it this wave
            items.Add(new KeyValuePair<string, Rgba>("COVER", HoloCoverInk));
            if (game.ActiveBreaches.Length > 0)
                items.Add(new KeyValuePair<string, Rgba>("BREACH", HoloBreachInk));
            if (game.GateFits.Count > 0)
                items.Add(new KeyValuePair<string, Rgba>("GATE", HoloGateInk));
            if (game.Links.Count > 0)
                items.Add(new KeyValuePair<string, Rgba>("PORTAL", PortalColours[(game.ExpansionWorld.Portals.Length - game.Links.Count) % PortalColours.Length]));
            bool sealedRoom = false;
            var sealedKinds = new HashSet<RoomKind>();
            foreach (var fa in game.ExpansionWorld.Areas)
                if (fa.Sealed && sealedKinds.Add(fa.Kind))
                {
                    sealedRoom = true;
                    items.Add(new KeyValuePair<string, Rgba>(fa.Kind == RoomKind.Shop ? "SHOP ROOM" : fa.Kind == RoomKind.Vendor ? "BLACK MARKET" : "SEALED ROOM", KindInk(fa.Kind, PathId.None)));
                }
            float lh = 34f + items.Count * 20f;
            float ly = Mathf.Max(y + 8f, H - 30f - lh);
            DkSub(new Rect(x, ly, w, lh));
            Ui.Label(new Rect(x + 10f, ly + 6f, w - 20f, 16f), "ON THE MAP", 10, Ui.Muted, TextAnchor.MiddleLeft, true);
            for (int i = 0; i < items.Count; i++)
            {
                var c = ToColor(items[i].Value);
                float iy = ly + 30f + i * 20f;
                Ui.Fill(new Rect(x + 11f, iy + 3f, 10f, 10f), new Color(c.r, c.g, c.b, 0.3f));
                CrtKit.Frame(new Rect(x + 10f, iy + 2f, 12f, 12f), 1.5f, c);
                Ui.Label(new Rect(x + 30f, iy, w - 36f, 16f), items[i].Key, 11, c, TextAnchor.MiddleLeft, true);
            }
            if ((seen.Count > 0 || sealedRoom) && TipHover(new Rect(x, ly, w, lh)))
            {
                TipStart(new Rect(x, ly, w, lh), "On the map", CrtUi.Ink);
                TipAdd("Build a room round a node or up to a sealed room to claim it.", 12, Ui.Text);
            }
        }

        /// The top of the draw screen's guide panel: the wallet (with what the drawing would cost), the tool's name and its three steps.
        /// Returns where the panel's next part goes.
        float DrawBuildPanelTop(Rect p, bool doorsOnly, bool roomMode, int pending, int gateDoor)
        {
            DkSub(p, new Color(0.02f, 0.05f, 0.12f, 0.6f));
            float x = p.x + 14f, w = p.width - 28f, y = p.y + 10f;
            int have = game.SpendableVectors(game.Me);
            Ui.Label(new Rect(x, y, w, 30f), game.TeamPurse ? "TEAM PURSE" : "YOUR VECTORS", 11, Ui.Muted, TextAnchor.MiddleLeft, true);
            CrtUi.Text(new Rect(x, y - 2f, w, 34f), have.ToString(), 30, Ui.Gold, TextAnchor.MiddleRight, true, CabCalm);
            y += 36f;
            int cap = Mathf.Max(Mathf.Max(have, game.Me != null ? game.Me.VectorCap : have), 1);
            var bar = new Rect(x, y, w, 8f);
            Ui.Fill(bar, new Color(0f, 0f, 0f, 0.5f));
            CrtKit.Frame(bar, 1f, new Color(1f, 1f, 1f, 0.12f));
            float fHave = Mathf.Clamp01((float)have / cap), fAfter = Mathf.Clamp01((float)(have - pending) / cap);
            bool over = pending > have;
            Ui.Fill(new Rect(bar.x, bar.y, bar.width * Mathf.Min(fHave, fAfter), bar.height), Ui.Gold);
            if (pending > 0)  // the slice the drawing would spend, blinking
                Ui.Fill(new Rect(bar.x + bar.width * fAfter, bar.y, bar.width * (fHave - fAfter), bar.height), over ? new Color(1f, 0.3f, 0.36f, 0.9f) : new Color(1f, 0.95f, 0.75f, 0.45f + 0.35f * Mathf.Sin(Time.unscaledTime * 5f)));
            else if (pending < 0)
                Ui.Fill(new Rect(bar.x + bar.width * fHave, bar.y, bar.width * (fAfter - fHave), bar.height), new Color(0.35f, 1f, 0.55f, 0.8f));
            y += 12f;
            string sub = pending > 0 ? (over ? "Needs " + pending + ": " + (pending - have) + " short" : "This uses " + pending + "  ·  " + (have - pending) + " left") : pending < 0 ? "Gives back " + -pending : "";
            if (TipHover(new Rect(p.x, p.y, p.width, y + 18f - p.y)))
            {
                TipStart(new Rect(p.x, p.y, p.width, y + 18f - p.y), "Vectors", CrtUi.Ink);
                TipAdd("What rooms cost. Touching rooms cost less, and opening walls gives some back.", 12, Ui.Text);
            }
            Ui.Label(new Rect(x, y, w, 16f), sub, 11, over ? new Color(1f, 0.45f, 0.5f) : pending != 0 ? Ui.Gold : Ui.Muted, TextAnchor.MiddleLeft, true);
            y += 30f;
            CrtKit.Frame(new Rect(x, y - 8f, w, 1f), 1f, new Color(1f, 1f, 1f, 0.08f));
            string title, tip;
            string[] steps;
            BuildToolHelp(expTrapMode ? 4 : expGateMode ? 3 : expLinkMode ? 2 : expCutMode ? 1 : 0, doorsOnly, out title, out steps, out tip);
            CrtUi.Text(new Rect(x, y, w, 26f), title, 20, CrtUi.Ink, TextAnchor.MiddleLeft, true, CabCalm);
            y += 32f;
            return y + 4f;
        }

        /// What each build tool does, for its hover tip: tool 0 room, 1 open wall, 2 teleporter, 3 gate, 4 trap.
        void BuildToolHelp(int tool, bool doorsOnly, out string title, out string[] steps, out string tip)
        {
            if (tool == 3)
            {
                title = "FIT A GATE";
                steps = new[] { "Point at a door (gold squares)", "Click it to fit a gate: " + Gates.Price + " vectors", "In a wave, by it press " + Keybinds.KeyName(Keybinds.Get(KeyAction.TakeItem)).ToUpperInvariant() };
                tip = "Shuts for " + (int)Gates.CloseTime + " s, " + (int)Gates.Cooldown + " s to recharge.  " + game.GateFits.Count + " of " + Gates.Max + " fitted.";
            }
            else if (tool == 2)
            {
                title = "TELEPORTER PAIR";
                steps = new[] { "Click a spot in one room", "Click a spot in another room", "Walk in one, come out the other" };
                tip = "Uses one from your stash (" + (game.Me != null ? game.Me.Stash[(int)BuildKind.Teleporter] : 0) + "). Enemies use them too.  " + game.Links.Count + " of " + Expansion.MaxLinks + " built.";
            }
            else if (tool == 4)
            {
                title = "PLACE A TRAP";
                steps = new[] { "Pick one from your stash", "Point at a spot in a room", "Click to place it" };
                tip = "Bought at the Black Market. They stand on the map, " + game.Placed.Count + " of " + Market.MaxPlaced + ". They have no effect yet.";
            }
            else if (tool == 1)
            {
                title = "OPEN A WALL";
                steps = new[] { "Find a cyan wall two rooms share", "Click one end of the stretch", "Click the other end" };
                tip = (doorsOnly ? "A wave is on: only doors for now. " : "") + "At least " + Expansion.MinCut / 100 + " tiles. You get 1 vector back per tile of wall.";
            }
            else
            {
                title = "DRAW A ROOM";
                steps = new[] { "Click a corner on the grid", "Click more corners", "Click the first corner or a wall" };
                tip = "A slanted click makes an L. Or drag a box. " + Expansion.MinSide / 100 + " tiles wide everywhere.";
            }
        }

        /// The room types for a drawn room, two to a row; the locked ones say which boss unlocks them, the picked one says what it does.
        void DrawThemeGrid(Rect area, bool calm)
        {
            var themes = new List<PathId> { PathId.None };
            themes.AddRange(Expansion.Floors);
            if (!Expansion.ThemeUnlocked(expTheme, game.Wave))
                expTheme = PathId.None;
            Ui.Label(new Rect(area.x, area.y, area.width, 16f), "ROOM TYPE", 10, Ui.Muted, TextAnchor.MiddleLeft, true);
            float gap = 6f, cw = (area.width - gap) / 2f, ch = 34f, y0 = area.y + 20f;
            int lockedCount = 0;
            var open = new List<PathId>();
            foreach (var f in themes)
                if (Expansion.ThemeUnlocked(f, game.Wave)) open.Add(f);
                else lockedCount++;
            for (int i = 0; i < open.Count; i++)
            {
                var f = open[i];
                var r = new Rect(area.x + (i % 2) * (cw + gap), y0 + (i / 2) * (ch + gap), cw, ch);
                string name = f == PathId.None ? "PLAIN" : Paths.Get(f).Name.ToUpperInvariant();
                bool on = expTheme == f;
                if (CrtUi.SoftKey(r, "", null, 11, calm, lit: on))
                    expTheme = f;
                Ui.Label(new Rect(r.x, r.y + 3f, r.width, 16f), name, 11, on ? Color.white : CrtUi.Ink, TextAnchor.MiddleCenter, true);
                Ui.Label(new Rect(r.x, r.y + 18f, r.width, 13f), f == PathId.None ? "FREE" : "+" + Expansion.ThemeCost(f) + " VECTORS", 9, on ? Ui.Gold : Ui.Muted, TextAnchor.MiddleCenter, true);
                if (TipHover(r))
                {
                    TipStart(r, name, CrtUi.Ink);
                    TipAdd(f == PathId.None ? "An ordinary room: no theme, no extra cost." : FloorBlurb(f), 12, Ui.Text);
                }
            }
            float y = y0 + ((open.Count + 1) / 2) * (ch + gap);
            if (lockedCount > 0)
            {
                int next = 99;
                foreach (var f in themes)
                    if (!Expansion.ThemeUnlocked(f, game.Wave)) next = Mathf.Min(next, Expansion.ThemeBoss(f));
                Ui.Label(new Rect(area.x, y, area.width, 16f), "+" + lockedCount + " more after boss " + next, 10, Ui.Muted, TextAnchor.MiddleLeft, true);
                y += 20f;
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
            bool mineCard = o.Owner == game.MyId;
            CrtUi.Text(new Rect(r.x + 22f, ty, r.width - 44f, 34f), (mineCard ? "YOUR " : "") + (o.Kind == RoomKind.Gate ? "SHUTTER GATE" : o.Kind == RoomKind.Link ? "PORTAL PAIR" : o.Kind == RoomKind.Opening ? "OPEN A WALL" : (o.Kind == RoomKind.Floor ? Paths.Get(o.Floor).Name.ToUpperInvariant() + " " : "") + (drawn ? "DRAWN ROOM" : o.Shape.ToUpperInvariant())), r.width < 330f ? 22 : 28, ink, TextAnchor.MiddleLeft, true, calm);
            CrtUi.Text(new Rect(r.x + 22f, ty + 34f, r.width - 44f, 22f), (owner != null ? "DRAWN BY " + owner.Name.ToUpperInvariant() : "DRAWN") + "  ·  " + (o.Kind == RoomKind.Gate ? "on a door" : o.Kind == RoomKind.Link ? "saves " + Mathf.RoundToInt(Expansion.WalkDistance(game.ExpansionWorld, Expansion.LinkOf(o).A, Expansion.LinkOf(o).B) / 100f) + " tiles of walking" : o.Kind == RoomKind.Opening ? (o.W + o.H) / 100 + " tiles of wall" : drawn ? Expansion.FloorTiles(o, legs) + " tiles of floor" + (loops > 0 ? "  ·  LOOP" : "") : o.W / 100 + " x " + o.H / 100 + " tiles" + (loops > 0 ? "  ·  LOOP" : "")), 15, afford ? CrtUi.Ink : dim, TextAnchor.MiddleLeft, false, calm);
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
                var legs = game.LegsOf(o.Owner);  // (a drawn outline's other pieces)
                all.AddRange(legs);
                added = 1 + legs.Count;
            }
            var wd = Expansion.Build(all, game.Openings, game.Links, game.Nodes);
            V2 lo = wd.Min, hi = wd.Max;
            IncludeNodes(ref lo, ref hi);
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
            foreach (var nd in game.Nodes)  // the nodes still waiting for a room round them
                if (!Expansion.NodeClaimed(all, nd))
                {
                    V2 np = M(nd.Pos);
                    RoomMapRect(np.X - 3f, np.Y - 3f, 6f, 6f, NodeInk(nd.Kind).WithAlpha(0.6f + 0.3f * Mathf.Sin(t * 3f)));
                }
            for (int di = 0; di < wd.Doors.Length; di++)
            {
                var d = wd.Doors[di];
                if (wd.Seam(di))
                    continue;  // (inside one room: an outline's seam)
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
            string text = "SHOP OPEN  ·  " + me.Gold + " GOLD  ·  walk into the " + (game.HasShopRoom && game.HasVendorRoom ? "shop room or Black Market" : game.HasShopRoom ? "shop room" : "Black Market");
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
