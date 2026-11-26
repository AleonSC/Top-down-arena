using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Expanding arena on screen (ExpansionGame.cs, Docs/ExpandingArena.md): the vote on rooms to buy after a boss, and
    /// the team purse chip under the wave plate. The map and the minimap are drawn by RoomsLook.cs.
    public partial class ArenaApp
    {
        static readonly Rgba CombatInk = new Rgba(1f, 0.3f, 0.36f), ShopInk = Rgba.Gold;

        static readonly Rgba BlackMarketInk = new Rgba(0.3f, 0.9f, 0.8f), GateInk = new Rgba(1f, 0.5f, 0.2f), ArmsInk = new Rgba(0.66f, 0.9f, 0.36f), MedicInk = new Rgba(1f, 0.52f, 0.68f);

        static Rgba KindInk(RoomKind k, PathId floor) => k == RoomKind.Shop ? ShopInk : k == RoomKind.BlackMarket ? BlackMarketInk : k == RoomKind.Arms ? ArmsInk : k == RoomKind.Medic ? MedicInk : k == RoomKind.Hospital ? HospInk : k == RoomKind.Builder ? BuilderInk : k == RoomKind.Gatekeeper ? GateInk : k == RoomKind.Floor ? Paths.Get(floor).Accent : CombatInk;

        /// A room's drawer's colour (their class colour), or a neutral blue when they've gone or it was nobody's.
        Rgba OwnerInk(int owner)
        {
            Player p;
            return owner != 0 && game.Players.TryGetValue(owner, out p) ? p.Def.Color : new Rgba(0.4f, 0.64f, 1f);
        }

        static string KindName(ExpansionRoom r) => r.Kind == RoomKind.Shop ? "Shop" : r.Kind == RoomKind.BlackMarket ? "Black Market" : r.Kind == RoomKind.Arms ? "Arms Dealer" : r.Kind == RoomKind.Medic ? "Medic" : r.Kind == RoomKind.Hospital ? "Field Hospital" : r.Kind == RoomKind.Builder ? "Builder Shop" : r.Kind == RoomKind.Gatekeeper ? FactionMinis.Name((EnemyType)r.Mini) : r.Kind == RoomKind.Floor ? Paths.Get(r.Floor).Name : "Combat";

        /// What a rule room does: that path's rule inside the room only, and its bonus for kills there.
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
                case PathId.Fogbound: rule = "Dark: you only see about 350 px round you in here."; break;
                default: rule = d.Name + " ground."; break;
            }
            int pct = (int)Mathf.Round((Paths.Bonus(d.Danger) - 1f) * 100f);
            return rule + (pct > 0 ? " Kills here give +" + pct + "% XP and score." : "");
        }

        // --- The building round's chip ------------------------------------------------------------------------

        bool ExpandChipUp => game.ExpandOpen && !expOpen && game.Me != null;

        /// Between waves: a chip (the wave doesn't wait for it) with a way to open the round's draw screen.
        void DrawExpandChip(Player me)
        {
            Ui.Alpha = 1f;
            bool calm = FxCalm;
            // Above the Wave cleared box (it starts at y 84 and covers everything below).
            var chip = new Rect(W / 2f - 300f, 34f, 600f, 46f);
            Ui.Pill(chip, new Color(0f, 0f, 0f, 0.75f));
            Ui.Fill(new Rect(chip.x + 4f, chip.y + 7f, 3f, chip.height - 14f), new Color(0.37f, 0.94f, 1f, 0.6f));
            Ui.Label(new Rect(chip.x + 16f, chip.y + 3f, 400f, 22f), "BUILD ROOMS" + (game.RoomTokens.Count > 0 ? "  ·  " + game.RoomTokens.Count + " KIT" + (game.RoomTokens.Count == 1 ? "" : "S") : "") + "  ·  " + game.Expanded.Count + " ROOMS  ·  YOUR VECTORS " + me.Vectors + " / " + me.VectorCap, 13, new Color(0.37f, 0.94f, 1f), TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(chip.x + 16f, chip.y + 23f, 400f, 18f), "Build freely with your own vectors  ·  the wave starts in " + Mathf.CeilToInt(game.StateTimer) + " s", 11, Ui.Muted, TextAnchor.MiddleLeft);
            if (DkKey(new Rect(chip.xMax - 170f, chip.y + 7f, 156f, 32f), "BUILD", false, calm, size: 15, rawSize: true))
                expOpen = true;
        }

        // --- The shop room ------------------------------------------------------------------------------------

        /// The shop covers your screen: in the Expanding arena, only while you stand in a shop room (and haven't clicked Done).
        bool ShopScreenUp(Player me) => game.ShopOpen && me != null && (!game.ServiceRooms || game.InServiceRoom(me) && !me.ShopDone) && !BeamShowing;  // (not over the boss beam-in: BossBeamLook.cs)

        /// Standing in a room shop (shop room, Black Market, Arms Dealer, Medic) between waves after Done shopping: Interact (F) opens it again.
        bool ShopReopenable(Player me) => me != null && !me.Downed && game.ShopOpen && game.ServiceRooms && me.ShopDone && game.InServiceRoom(me);

        /// The shop is open but you're not in a shop room: a banner and a way to skip it. In a room shop you're done with: the key that opens it again.
        void DrawShopRoomHint(Player me)
        {
            if (game.ServiceRooms)
            {
                if (ShopReopenable(me))
                    DrawShopReopenHint(me);
                return;  // (the room shops hold nothing up: the HUD chip says they're open)
            }
            Ui.Alpha = 1f;
            bool calm = FxCalm;
            float dy = 0f;
            if (me.ShopDone)
            {
                Ui.Label(new Rect(0, 150f + dy, W, 22f), "Done shopping  ·  waiting for the others", 14, Ui.Muted, TextAnchor.MiddleCenter, true);
                return;
            }
            Ui.Label(new Rect(0, 130f + dy, W, 26f), "THE SHOP IS OPEN  ·  walk into the gold room on the minimap", 18, CrtStyle.Gold, TextAnchor.MiddleCenter, true);
            Ui.Label(new Rect(0, 156f + dy, W, 20f), "You have " + me.Gold + " gold. Leftover gold becomes XP when the shop closes.", 13, Ui.Muted, TextAnchor.MiddleCenter);
            if (DkKey(new Rect(W / 2f - 120f, 180f + dy, 240f, 32f), "SKIP THE SHOP", false, calm, size: 18, rawSize: true))
                game.Shop(ShopAction.Done, 1);
        }

        /// Over you in a room shop you're done with: a gold key pill "F  OPEN THE SHOP" (or the room's own name), breathing gently.
        void DrawShopReopenHint(Player me)
        {
            string key = Keybinds.NameOrNull(KeyAction.Interact);
            if (key == null)
                return;
            string what = game.InShopRoom(me) ? "OPEN THE SHOP" : game.InArmsRoom(me) ? "OPEN THE ARMS DEALER" : game.InMedicRoom(me) ? "OPEN THE MEDIC" : "OPEN THE BLACK MARKET";
            V2 over = ToUi(me.Disp);
            float w = 64f + what.Length * 7f, breathe = FxCalm ? 1f : 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 3f);
            var r = new Rect(over.X - w / 2f, over.Y - 80f, w, 24f);
            Ui.Glow(r, CrtStyle.Gold, 0.35f * breathe);
            Ui.Pill(r, new Color(0.1f, 0.07f, 0.02f, 0.9f));
            var k = new Rect(r.x + 4f, r.y + 3f, 22f + (key.Length - 1) * 7f, 18f);
            Ui.Pill(k, CrtStyle.Gold.WithAlpha(breathe));
            Ui.Label(k, key.ToUpperInvariant(), 11, new Color(0.1f, 0.07f, 0.02f), TextAnchor.MiddleCenter, true);
            Ui.Label(new Rect(k.xMax + 8f, r.y, r.xMax - k.xMax - 12f, r.height), what, 11, CrtStyle.Gold, TextAnchor.MiddleLeft, true);
        }

        // --- The building window -----------------------------------------------------------------------------

        bool expDrawing, expDragging, expOpen, expModify;  // expModify: spending a room kit
        int expKit;  // the kit picked in the modify screen  // expOpen: the round's window is open (otherwise just its chip shows)

        /// The room window covers the screen (you opened it from the chip).
        bool ExpandWindowUp => game.ExpandOpen && expOpen && !BeamShowing;  // (not over the boss beam-in: BossBeamLook.cs)
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
            bool calm = FxCalm;
            // The draw screen is the window (DONE closes it); the room kit screen opens from it.
            if (!game.ExpandOpen)
                expModify = false;
            if (!expDrawing && !expModify)
            {
                expDrawing = true;
                expDragging = false;
                expA = expB = V2.Zero;
            }
            if (expModify)
                DrawExpansionModify(t, calm);
            else
                DrawExpansionDraw(t, calm);
        }

        /// The CRT window the room kit and draw screens sit in: a blue bezel round one phosphor screen.
        Rect CrtWindowBegin()
        {
            CrtKit.LoadoutBox(new Rect(20f, 6f, W - 40f, H - 12f));
            var S = new Rect(32f, 18f, W - 64f, H - 36f);
            CrtUi.Begin(S);
            return S;
        }

        /// The room kit screen (Docs/RoomModifiers.md): pick a kit, then click a plain room on the map to turn it into a Shop or rule room.
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
            CrtUi.Title(new Rect(0, 22f, W, 56f), "ROOM KITS", 46, CrtStyle.Ink, TextAnchor.MiddleCenter, calm);
            Ui.Label(new Rect(0, 80f, W, 20f), "Pick a kit, then click a plain room  ·  one change per room, it can't be undone  ·  bosses give floors, won challenges give shops, the Black Market sells floors", 13, Ui.Muted, TextAnchor.MiddleCenter);
            float kw = 190f, kgap = 10f, kx = W / 2f - (kits.Count * kw + (kits.Count - 1) * kgap) / 2f;
            for (int i = 0; i < kits.Count; i++)
            {
                var kr = new Rect(kx + i * (kw + kgap), 106f, kw, 34f);
                bool sel = i == expKit;
                if (DkKey(kr, kits[i].Name.ToUpperInvariant() + (sel ? "  ✓" : ""), sel, calm, size: 15, rawSize: true))
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
                if (r.Kind == RoomKind.Combat || r.Floating || r.Piece > 0)  // (one label for a room of several boxes)
                    continue;
                V2 p0 = M(new V2(r.X, r.Y));
                Ui.Label(new Rect(p0.X + 3f, p0.Y + 2f, 200f, 14f), KindName(r).ToUpperInvariant(), 10, KindInk(r.Kind, r.Floor).ToColor(), TextAnchor.UpperLeft, true);
            }
            string line = kit.Kind == RoomKind.Floor ? FloorBlurb(kit.Floor) : "A shop room: kills drop personal gold and the shop opens in the break after each boss.";
            Ui.Label(new Rect(0, map.yMax + 10f, W, 22f), hover >= 0 ? "Click to turn this room into: " + kit.Name + ".  " + line : line, 14, hover >= 0 ? CrtStyle.Gold : Ui.Muted, TextAnchor.MiddleCenter, true);
            if (DkKey(new Rect(W / 2f - 120f, map.yMax + 44f, 240f, 34f), "BACK", false, calm, size: 20, rawSize: true))
                expModify = false;
            CrtUi.End(win, t, calm);
        }

        /// The build screen: the whole map, zoomed out; place a piece from your hand on a door spot (GridRoomsUi.cs), or open a wall, fit a gate,
        /// a portal pair or a trap.
        void DrawExpansionDraw(float t, bool calm)
        {
            RoundBackdrop(PathDim);
            var win = CrtWindowBegin();
            NoteBuildDrawn();  // the open's motion starts when the screen comes up (BuildMotion.cs)
            TipClear();
            BuildSlide(0.08f, -200f, () => DrawBuildRail(calm));
            bool gridMode = !expLinkMode && !expGateMode && !expTrapMode;  // the Pieces tool: a piece snaps to a door spot (GridRoomsUi.cs)
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
            // The teleporter tool snaps to the nearest room's outer wall (never the floor); null: no wall near the mouse.
            System.Func<Vector2, V2?> WallSnap = m => Expansion.NearestWallSpot(wd, new V2((m.x - ox) / k + lo.X, (m.y - oy) / k + lo.Y), 1.5f * Expansion.Step);
            if (DkKey(new Rect(map.xMax - 150f, map.yMax - 34f, 40f, 28f), "-", false, calm, size: 18, rawSize: true)) expZoom = Mathf.Max(1f, expZoom / 1.4f);
            if (DkKey(new Rect(map.xMax - 106f, map.yMax - 34f, 40f, 28f), "+", false, calm, size: 18, rawSize: true)) expZoom = Mathf.Min(8f, expZoom * 1.4f);
            if (DkKey(new Rect(map.xMax - 62f, map.yMax - 34f, 56f, 28f), "FIT", false, calm, size: 14, rawSize: true)) expZoom = 1f;
            bool over = map.Contains(ev.mousePosition);
            // A mystery door under the mouse: a click opens it, a right click saves it (or lets your save go), from wherever you stand (MysteryDoors.cs, Msg.MysteryBuy).
            int mystHover = -1;
            for (int mi = 0; over && mi < game.MysteryDoors.Count; mi++)
            {
                V2 mc = M(game.MysteryDoors[mi].Pos);
                if (new Rect(mc.X - 12f, mc.Y - 12f, 24f, 24f).Contains(ev.mousePosition))
                    mystHover = mi;
            }
            int mystPrice = mystHover >= 0 ? game.MysteryDoors[mystHover].Price : 0;
            bool mystCan = mystHover >= 0 && mystPrice <= game.SpendableVectors(game.Me);
            if (mystHover >= 0 && ev.type == EventType.MouseDown && ev.button == 0)
            {
                if (mystCan)
                    game.BuyMysteryDoor(mystHover);
                ev.Use();
            }
            int mystSaver = mystHover >= 0 ? game.MysteryDoors[mystHover].Saver : 0;
            bool mystOthers = mystSaver != 0 && mystSaver != game.MyId && game.Players.ContainsKey(mystSaver);  // (someone else's save: theirs to keep)
            if (mystHover >= 0 && ev.type == EventType.MouseDown && ev.button == 1)
            {
                if (!mystOthers)
                    game.BuyMysteryDoor(mystHover, true);
                ev.Use();  // (not "put the piece back" or close)
            }
            // Controls: left click sets the first corner, the box follows the mouse, a second left click places it (or press, drag and let go);
            // right click or Escape cancels the box (and, with none started, goes back).
            bool confirm = false, dragRelease = false;
            // Gate mode: the door nearest the mouse (a click fits a gate on it).
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
            V2? wallAt = expLinkMode && over ? WallSnap(ev.mousePosition) : null;
            int unlinkAt = -1;  // the teleporter tool, over a built pair's end: a click dismantles it
            if (expLinkMode && over && !expDragging && game.Me != null)
            {
                V2 mouseWorld = Snap(ev.mousePosition);
                for (int li = 0; li < game.Links.Count && unlinkAt < 0; li++)
                    if (game.Links[li].A.CloserThan(mouseWorld, Expansion.LinkHalf + 50f) || game.Links[li].B.CloserThan(mouseWorld, Expansion.LinkHalf + 50f))
                        unlinkAt = li;
            }
            if (unlinkAt >= 0 && ev.type == EventType.MouseDown && ev.button == 0)
            {
                var gone = game.Links[unlinkAt];
                game.ProposeUnlink(game.Links[unlinkAt].A.CloserThan(Snap(ev.mousePosition), Expansion.LinkHalf + 50f) ? gone.A : gone.B);
                ev.Use();
            }
            if (expDragging && ev.type != EventType.Layout && ev.type != EventType.Used)
            {
                if (!expLinkMode)
                    expB = Snap(ev.mousePosition);
                else if (wallAt.HasValue)
                    expB = wallAt.Value;
            }
            V2 cursor = Snap(ev.mousePosition);
            V2 trapSpot = cursor;  // trap mode: where a click would put the trap, and whether it fits
            string trapWhy = null;
            bool trapOk = expTrapMode && over && game.Me != null && game.Me.Stash[expTrapKind] > 0 && Expansion.PlaceSpot(wd, game.Placed, trapSpot, out trapWhy);
            if (expTrapMode && over && trapWhy == null && (game.Me == null || game.Me.Stash[expTrapKind] == 0))
                trapWhy = "None in your stash: buy one at the Black Market";
            if (trapOk && expTrapKind == (int)BuildKind.Turret && game.TurretsOf(game.MyId) >= Market.TurretsEach)
            {
                trapOk = false;
                trapWhy = "You have " + Market.TurretsEach + " turrets up already";
            }
            Placement? gridPick = null;
            int gridFits = 0;
            if (gridMode)
                gridPick = GridModeInput(ev, over, new V2((ev.mousePosition.x - ox) / k + lo.X, (ev.mousePosition.y - oy) / k + lo.Y), true, out gridFits);
            else if ((ev.type == EventType.MouseDown && ev.button == 1) || (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.Escape))
            {
                if (expDragging)
                {
                    expDragging = expHeld = false;
                    expA = expB = V2.Zero;
                }
                else
                    expDrawing = expOpen = false;
                ev.Use();
            }
            else if (expTrapMode && ev.type == EventType.MouseDown && ev.button == 0 && over)
            {
                if (trapOk)
                    game.PlaceBuild((BuildKind)expTrapKind, (int)trapSpot.X, (int)trapSpot.Y);
                ev.Use();
            }
            else if (ev.type == EventType.MouseDown && ev.button == 0 && over && (!expLinkMode || wallAt.HasValue))  // (the teleporter tool: only on a wall)
            {
                if (!expDragging)
                {
                    expDragging = expHeld = true;
                    expA = expB = expLinkMode ? wallAt.Value : Snap(ev.mousePosition);
                    expPressAt = ev.mousePosition;
                }
                else
                    confirm = true;
                ev.Use();
            }
            else if (ev.type == EventType.MouseUp && ev.button == 0 && expHeld)
            {
                expHeld = false;
                if (expDragging && (ev.mousePosition - expPressAt).magnitude > 12f)
                    confirm = dragRelease = true;  // press, drag, let go
                ev.Use();
            }
            int rw = (int)Mathf.Abs(expA.X - expB.X), rh = (int)Mathf.Abs(expA.Y - expB.Y);
            string why = null;
            bool hasLink = expLinkMode && (rw > 0 || rh > 0);
            var link = new PortalLink { AX = (short)expA.X, AY = (short)expA.Y, BX = (short)expB.X, BY = (short)expB.Y };
            bool linkOk = false;
            if (hasLink)
            {
                linkOk = Expansion.LinkFits(wd, game.Links.Count, link, out why);
                if (linkOk && (game.Me == null || game.Me.Stash[(int)BuildKind.Teleporter] == 0)) { why = "No teleporter in your stash: buy one at the Black Market"; linkOk = false; }
            }
            if (confirm)
            {
                if (linkOk)
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
                DrawGridLattice(M, lo, hi);  // the lattice, fading out from the map (GridRoomsUi.cs)
                for (int ai = 0; ai < wd.Areas.Length; ai++)
                {
                    var a = wd.Areas[ai];
                    V2 p0 = M(a.Min), p1 = M(a.Max);
                    var ink = a.Hub ? new Rgba(0.4f, 0.64f, 1f) : KindInk(a.Kind, a.Floor);
                    float ghost = a.Sealed ? 0.55f + 0.25f * Mathf.Sin(Time.unscaledTime * 2.5f) : 1f;  // a sealed floating room breathes
                    RoomMapRect(p0.X, p0.Y, p1.X - p0.X, p1.Y - p0.Y, ink.WithAlpha((a.Hub ? 0.16f : 0.1f) * ghost));
                    DrawHoloGrid(a, M, k, ink.WithAlpha(ghost));
                    if (game.AreaUnseen(ai))
                        DrawFogMapBox(p0, p1, game.FogRoomShare(ai - 1));  // an unseen room: dark, a "?" below (RoomFogLook.cs)
                    if (a.Floating || a.Sealed)  // (a built room's outline is its walls, drawn below: a rim would run across the door gaps)
                        RimFrame(wd, ai, M, 1.5f, ink.WithAlpha(0.8f * ghost));
                }
                DrawHoloSolids(wd, M, k, map);  // walls, fins and cover over the floors (BuildMapLook.cs)
                foreach (var nd in game.Nodes)  // the nodes still waiting for a room built round them (claimed ones are part of their room)
                    if (!Expansion.NodeClaimed(game.Expanded, nd))
                    {
                        V2 np = M(nd.Pos);
                        var ni = NodeInkOf(nd);
                        RoomMapRect(np.X - 6f, np.Y - 6f, 12f, 12f, ni.WithAlpha(0.55f + 0.3f * Mathf.Sin(Time.unscaledTime * 2.5f)));
                        RoomMapFrame(np.X - 9f, np.Y - 9f, 18f, 18f, 1.5f, ni);
                    }
                for (int di = 0; di < wd.Doors.Length; di++)
                {
                    if (wd.Seam(di) || CoreSeam(wd, di))
                        continue;  // (inside one room; a Grid run's core is one room with the start room)
                    var d = wd.Doors[di];
                    V2 c = M(d.Pos);
                    if (d.Width > 0f)  // an opened wall: its whole length
                    {
                        V2 e0 = M(d.Pos - d.Along * (d.W / 2f)), e1 = M(d.Pos + d.Along * (d.W / 2f));
                        RoomMapRect(Mathf.Min(e0.X, e1.X) - 2f, Mathf.Min(e0.Y, e1.Y) - 2f, Mathf.Abs(e1.X - e0.X) + 4f, Mathf.Abs(e1.Y - e0.Y) + 4f, new Rgba(0.37f, 0.94f, 1f, 0.8f));
                    }
                    else
                        DrawDoorSpot(c, d.Pos, new Rgba(0.37f, 0.94f, 1f, 0.8f));  // (popping in after a build, BuildMotion.cs)
                }
                for (int li = 0; li < game.Links.Count; li++)  // the portal pairs built, in their pair's colour
                    DrawLinkMark(wd, M, game.Links[li].A, game.Links[li].B, PortalColours[(wd.Portals.Length - game.Links.Count + li) % PortalColours.Length], 1f);
                foreach (var pb in game.Placed)  // the Black Market traps standing on the map
                {
                    V2 bp = M(pb.Pos);
                    var bi = BlackMarketInk;
                    RoomMapFrame(bp.X - 7f, bp.Y - 7f, 14f, 14f, 1.5f, bi);
                    RoomMapRect(bp.X - 3f, bp.Y - 3f, 6f, 6f, bi.WithAlpha(0.8f));
                    if (pb.Kind == BuildKind.Turret)  // a turret: a pip for each wave it has left
                        for (int wi = 0; wi < pb.Waves; wi++)
                            RoomMapRect(bp.X - 7f + wi * 3f, bp.Y + 9f, 2f, 2f, bi);
                }
                if (expTrapMode && over)  // the trap about to go down: green where it fits, red where it doesn't
                {
                    V2 bp = M(trapSpot);
                    var bi = trapOk ? new Rgba(0.35f, 1f, 0.55f) : new Rgba(1f, 0.3f, 0.36f);
                    RoomMapRect(bp.X - 8f, bp.Y - 8f, 16f, 16f, bi.WithAlpha(0.3f));
                    RoomMapFrame(bp.X - 8f, bp.Y - 8f, 16f, 16f, 2f, bi);
                }
                DrawHoloGates(wd, M, k);  // the gates fitted (BuildMapLook.cs)
                DrawHoloOneWays(wd, M, k);  // a One-way piece's doors: an arrow the way each opens (OneWayDoorLook.cs)
                DrawHoloMystery(M, k, mystHover);  // sealed mystery doors (MysteryDoorLook.cs)
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
                if (expLinkMode && wallAt.HasValue && !expDragging)  // the teleporter tool: the wall spot under the mouse, a ghost Beam
                    DrawLinkBeam(M, wallAt.Value, Expansion.OuterWall(wd, wallAt.Value).Value, Rgba.White, 0.55f, 0f);
                else if ((over || expDragging) && !expGateMode && !gridMode && !expLinkMode)  // the snapped cursor (a cross) and the corner already placed (a square)
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
                if (unlinkAt >= 0)
                    DrawLinkMark(wd, M, game.Links[unlinkAt].A, game.Links[unlinkAt].B, new Rgba(1f, 0.3f, 0.36f), 1f);
                if (hasLink)
                    DrawLinkMark(wd, M, expA, expB, linkOk ? new Rgba(0.35f, 1f, 0.55f) : new Rgba(1f, 0.3f, 0.36f), 1f);
                if (gridMode)
                    DrawGridGhost(M, gridPick);
                DrawPlaceFx(M, k);  // a piece just built slams in (GridRoomsUi.cs)
                DrawGridDeny(M);  // a click where the piece fits nowhere (BuildMotion.cs)
                DrawBuildPrint(map);  // the open: the map prints top-down
                roomClip = null;
                EndUiGl();
            }
            DrawFogMapMarks(M, map);  // the unseen rooms' "?" (RoomFogLook.cs)
            CrtKit.Frame(map, 1f, CrtStyle.Ink.WithAlpha(0.25f));
            DrawCardFly(M);  // the card on its way to the map, and "+1 ROOM" over a piece just built (BuildMotion.cs)
            DrawPlaceLabel(M, map);
            foreach (var md in game.MysteryDoors)  // a saved mystery door: a lime SAVED tag under it (MysteryDoorLook.cs's ink), "YOURS" for your own save
                if (md.Saver != 0 && Event.current.type == EventType.Repaint)
                {
                    V2 mc = M(md.Pos);
                    string tag = md.Saver == game.MyId ? "SAVED · YOURS" : "SAVED";
                    float tw = Ui.TextWidth(tag, 9, true) + 10f;
                    var tr = new Rect(mc.X - tw / 2f, mc.Y + 13f, tw, 13f);
                    if (!map.Contains(new Vector2(tr.x, tr.y)) || !map.Contains(new Vector2(tr.xMax, tr.yMax)))
                        continue;
                    Ui.Fill(tr, new Color(0.02f, 0.05f, 0.03f, 0.85f));
                    CrtKit.Frame(tr, 1f, MysterySaved.ToColor().WithAlpha(0.8f));
                    Ui.Label(tr, tag, 9, MysterySaved.ToColor(), TextAnchor.MiddleCenter, true);
                }
            if (mystHover >= 0)
            {
                V2 mc = M(game.MysteryDoors[mystHover].Pos);
                var mr = new Rect(mc.X - 12f, mc.Y - 12f, 24f, 24f);
                TipHover(mr);
                TipStart(mr, "Mystery room", MysteryHolo.ToColor());
                TipAdd("A sealed door with a hidden room behind it: enemies, a shop, a chest, or nothing.", 12, Ui.Text);
                TipAdd("Click to open it for " + mystPrice + " vectors.", 12, mystCan ? CrtStyle.Gold : new Color(1f, 0.45f, 0.5f), true);
                Player sv;
                if (mystOthers && game.Players.TryGetValue(mystSaver, out sv))
                    TipAdd("Saved by " + sv.Name + ": it stays until it is opened.", 12, MysterySaved.ToColor());
                else if (mystSaver == game.MyId)
                    TipAdd("Saved by you: it stays until it is opened. Right click to let it go.", 12, MysterySaved.ToColor());
                else
                    TipAdd("Right click to save it: it won't expire. One door each.", 12, Ui.Muted);
            }
            foreach (var nd in game.Nodes)  // the nodes: named in a hover tip, not on the map (the legend shows the colours)
                if (!Expansion.NodeClaimed(game.Expanded, nd))
                {
                    V2 n0 = M(nd.Pos);
                    var nr = new Rect(n0.X - 12f, n0.Y - 12f, 24f, 24f);
                    if (map.Contains(Event.current.mousePosition) && TipHover(nr))
                    {
                        TipStart(nr, NodeName(nd), NodeInkOf(nd).ToColor());
                        TipAdd(NodeBlurb(nd), 12, Ui.Text);
                    }
                }
            foreach (var fa in wd.Areas)  // the floating rooms: named in a hover tip too
                if (fa.Floating)
                {
                    V2 f0 = M(fa.Min), f1 = M(fa.Max);
                    var fr = new Rect(f0.X, f0.Y, f1.X - f0.X, f1.Y - f0.Y);
                    if (!map.Contains(Event.current.mousePosition) || !TipHover(fr))
                        continue;
                    TipStart(fr, fa.Name, KindInk(fa.Kind, fa.Floor).ToColor());
                    if (fa.Sealed)
                        TipAdd("SEALED  ·  build a room up to it to claim it.", 12, Ui.Text);
                }
            string line;
            Color lineInk;
            if (mystHover >= 0)
            {
                line = mystCan ? "MYSTERY ROOM  ·  " + mystPrice + " VECTORS (of " + game.SpendableVectors(game.Me) + ")  ·  click to open it" + (mystOthers ? "" : mystSaver == game.MyId ? "  ·  right click to let it go" : "  ·  right click to save it") : "Can't afford it: " + mystPrice + " vectors, you have " + game.SpendableVectors(game.Me);
                lineInk = mystCan ? CrtStyle.Gold : new Color(1f, 0.45f, 0.5f);
            }
            else if (gridMode)
                line = GridRoomLine(gridPick, gridFits, over, out lineInk);
            else if (expGateMode && gateDoor >= 0)
            {
                line = gateWhy ?? "SHUTTER GATE ON THIS DOOR  ·  " + Gates.Price + " VECTORS (of " + game.SpendableVectors(game.Me) + ")  ·  click to fit it";
                lineInk = gateWhy == null ? CrtStyle.Gold : new Color(1f, 0.45f, 0.5f);
            }
            else if (expGateMode)
            {
                line = "A gate shuts its doorway to everyone. Shots still pass.";
                lineInk = Ui.Muted;
            }
            else if (expTrapMode)
            {
                line = trapWhy != null ? trapWhy : trapOk ? Market.Names[expTrapKind].ToUpperInvariant() + "  ·  click to place it  ·  " + game.Me.Stash[expTrapKind] + " in your stash" : "Move over a room to place a " + Market.Names[expTrapKind].ToLowerInvariant() + ".";
                lineInk = trapWhy != null ? new Color(1f, 0.45f, 0.5f) : trapOk ? CrtStyle.Gold : Ui.Muted;
            }
            else if (unlinkAt >= 0)
            {
                line = "TELEPORTER PAIR  ·  click to dismantle it  ·  the teleporter goes back to your stash";
                lineInk = new Color(1f, 0.45f, 0.5f);
            }
            else if (hasLink && linkOk)
            {
                line = "TELEPORTER PAIR  ·  from your stash  ·  saves " + Mathf.RoundToInt(Expansion.LinkWalk(wd, link) / 100f) + " tiles of walking  ·  click to build it";
                lineInk = CrtStyle.Gold;
            }
            else if (hasLink)
            {
                line = why ?? "";
                lineInk = new Color(1f, 0.45f, 0.5f);
            }
            else
            {
                line = "Builds the moment you place it.";
                lineInk = Ui.Muted;
            }
            int pending = 0;  // what the thing being drawn costs (a refund is negative), for the wallet bar
            if (expGateMode && gateDoor >= 0 && gateWhy == null) pending = Gates.Price;
            if (mystHover >= 0) pending = mystPrice;
            var panel = new Rect(map.xMax + 16f, map.y, win.xMax - map.xMax - 32f, H - 30f - map.y);
            BuildSlide(0.12f, 260f, () =>
            {
                float py = DrawBuildPanelTop(panel, pending, gateDoor);
                if (!lineInk.Equals(Ui.Muted) && !string.IsNullOrEmpty(line))  // (the standing hints are hover tips now: only live feedback shows here)
                {
                    DkSub(new Rect(panel.x + 10f, py, panel.width - 20f, 74f), new Color(0f, 0f, 0f, 0.35f));
                    Ui.Label(new Rect(panel.x + 18f, py + 4f, panel.width - 36f, 66f), line, 13, lineInk, TextAnchor.MiddleLeft, true, true);
                    py += 84f;
                }
                float bx = panel.x + 10f, bw = panel.width - 20f;
                float actY = panel.yMax - 140f;  // the action keys sit just above Close and Done · ready up
                if (gridMode)
                    DrawGridPalette(new Rect(bx, py, bw, actY - 46f - py), calm);
                // Close (back to the arena, the wave box folds into its strip) and Done · ready up (closes and readies you).
                // Readying needs the break (not mid-wave, not before a boss: the gate decides, nor with Auto-ready on).
                var meR = game.Me;
                bool canReady = meR != null && game.State == MatchState.Intermission && !game.GateUp && !meR.AutoReady;
                if (canReady && DkKey(new Rect(bx, panel.yMax - 50f, bw, 42f), meR.Ready ? "DONE  ·  READY  ✓" : "DONE  ·  READY UP", true, calm, size: 18, rawSize: true))
                {
                    expDrawing = expDragging = false;
                    expOpen = false;
                    if (!meR.Ready)
                        game.SetMyReady(true);
                }
                if (DkKey(canReady ? new Rect(bx, panel.yMax - 92f, bw, 36f) : new Rect(bx, panel.yMax - 50f, bw, 42f), "CLOSE", false, calm, size: canReady ? 18 : 22, rawSize: true))
                {
                    expDrawing = expDragging = false;
                    expOpen = false;
                }
            });
            Ui.Label(new Rect(map.x, map.yMax + 6f, map.width, 20f), gridMode ? "SCROLL  zoom   ·   MIDDLE DRAG  pan   ·   R  turn   ·   RIGHT CLICK  put the piece back   ·   ESC  close" : "SCROLL  zoom   ·   MIDDLE DRAG  pan   ·   RIGHT CLICK  undo   ·   ESC  close", 12, Ui.Muted, TextAnchor.MiddleLeft, true);
            DrawTip(true);
            CrtUi.End(win, t, calm);
            DrawBuildCorners();  // the breathing corners, and the tube switching on (BuildMotion.cs)
            DrawBuildPowerOn();
        }

        float expZoom = 1f;  // the drawing map's zoom (1 = the whole map)
        V2 expViewC;  // the world point at the middle of the zoomed drawing map
        bool expPan;  // middle mouse is panning it
        bool expHeld;  // the mouse button of the box being drawn is still down
        Vector2 expPressAt;
        bool expLinkMode;  // the draw screen places a portal pair (Docs/RoomBuilding2.md step 2): a teleporter from the stash (Market.cs)
        bool expTrapMode;  // the draw screen places a trap from the stash (Market.cs), one click
        int expTrapKind = (int)BuildKind.Turret;
        bool expGateMode;  // the draw screen fits a shutter gate to a door (Docs/RoomBuilding2.md step 3)
        /// A portal pair on a map: a dashed line between its ends, and at each end a bar along its wall with a chevron into the room
        /// (a wall portal), or a ring (an end off the walls: an older save's floor portal, or a click not on an outer wall yet).
        void DrawLinkMark(World wd, System.Func<V2, V2> M, V2 wa, V2 wb, Rgba ink, float alpha)
        {
            V2 a = M(wa), b = M(wb);
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
            for (int k = 0; k < 2; k++)
            {
                V2 we = k == 0 ? wa : wb, e = k == 0 ? a : b;
                var side = Expansion.OuterWall(wd, we);
                if (side.HasValue)  // a wall end: the arena's Beam look in small (DrawWallPortal, RoomsLook.cs)
                {
                    DrawLinkBeam(M, we, side.Value, ink, alpha, k * 0.5f);
                    continue;
                }
                Arc(e, 7f, 2f, ink.WithAlpha(alpha * pulse), 18);  // a ring and a turning inner arc (the portal in the arena)
                Arc(e, 3.5f, 1.5f, ink.WithAlpha(alpha), 12, spin, 4f);
                spin += 3f;
            }
        }
        /// A wall end of a portal pair on the build map, like the Beam in the arena: the stretch of wall dark inside corner brackets,
        /// and scanlines rolling out of it into the room as a short widening beam (the way you come out).
        void DrawLinkBeam(System.Func<V2, V2> M, V2 we, V2 inWorld, Rgba ink, float alpha, float seed)
        {
            V2 along = inWorld.Orthogonal();
            float h = Expansion.LinkHalf, wt = Rooms.WallT / 2f + 2f, reach = 90f;
            V2 a0 = M(we - along * h), a1 = M(we + along * h);
            V2 inn = (M(we + inWorld * 100f) - M(we)).Normalized(), al = (a1 - a0).Normalized();
            float hs = (a1 - a0).Length * 0.5f, ws = Mathf.Max(2.5f, (M(we + inWorld * wt) - M(we)).Length), rs = Mathf.Max(10f, (M(we + inWorld * reach) - M(we)).Length);
            V2 c = M(we);
            Quad2(c - al * hs - inn * ws, c + al * hs - inn * ws, c + al * hs + inn * ws, c - al * hs + inn * ws, new Rgba(0.02f, 0.01f, 0.05f, 0.95f * alpha));
            Quad2(c - al * hs - inn * ws, c + al * hs - inn * ws, c + al * hs + inn * ws, c - al * hs + inn * ws, ink.WithAlpha(0.18f * alpha));
            for (int k = 0; k < 3; k++)  // the beam on the floor, in fading slices
            {
                float d0 = k * rs / 3f, d1 = (k + 1) * rs / 3f;
                float h0 = hs * (1f + 0.25f * d0 / rs), h1 = hs * (1f + 0.25f * d1 / rs);
                V2 c0 = c + inn * (ws + d0), c1 = c + inn * (ws + d1);
                Quad2(c0 - al * h0, c0 + al * h0, c1 + al * h1, c1 - al * h1, ink.WithAlpha(0.12f * (1f - k / 3f) * alpha));
            }
            for (int k = 0; k < 3; k++)  // scanlines rolling out
            {
                float u = Mathf.Repeat(k / 3f + Time.unscaledTime * 0.45f + seed, 1f), d = ws + u * rs;
                float hh = hs * (1f + 0.25f * u) - 1.5f;
                Line(c + inn * d - al * hh, c + inn * d + al * hh, 1.2f, ink.WithAlpha(0.8f * (1f - u) * alpha));
            }
            Line(c - al * hs + inn * ws, c + al * hs + inn * ws, 1.5f, ink.WithAlpha(alpha));
            float bl = Mathf.Min(6f, hs * 0.4f), bw = ws + 1.5f;
            foreach (var sa in new[] { -1f, 1f })  // the corner brackets
                foreach (var si in new[] { -1f, 1f })
                {
                    V2 corner = c + al * (sa * (hs + 1f)) + inn * (si * bw);
                    Line(corner, corner - al * (sa * bl), 1.5f, ink.WithAlpha(alpha));
                    Line(corner, corner - inn * (si * Mathf.Min(4f, bw)), 1.5f, ink.WithAlpha(alpha));
                }
        }

        /// The draw screen's map area: between the tool rail on the left and the guide panel on the right.
        Rect DrawMapRect => new Rect(222f, 34f, W - 222f - 290f, H - 34f - 74f);

        /// The build screen's tool rail: the title, the tools (keys 1-4: pieces, teleporter, gate, trap), the kit key and what the map's colours mean.
        void DrawBuildRail(bool calm)
        {
            float x = 48f, w = 158f;
            CrtUi.Title(new Rect(x, 26f, w, 40f), "BUILD", 32, CrtStyle.Ink, TextAnchor.MiddleLeft, calm);
            Ui.Label(new Rect(x + 2f, 64f, w, 16f), "BUILT AS YOU PLACE IT", 10, Ui.Muted, TextAnchor.MiddleLeft, true);
            string[] names = { "PIECES", "TELEPORTER", "GATE", "TRAP" };
            // The tools shown: the buildables only while you own one (owner Nov 18).
            var me0 = game.Me;
            bool trapsHeld = false;
            if (me0 != null)
                for (int tk = 1; tk < Market.Count; tk++)
                    trapsHeld |= me0.Stash[tk] > 0;
            var shown = new List<int> { 0 };
            if (me0 != null && me0.Stash[(int)BuildKind.Teleporter] > 0 || game.Links.Count > 0 || expLinkMode) shown.Add(1);
            shown.Add(2);
            if (trapsHeld || expTrapMode) shown.Add(3);
            int cur = BuildTool;
            var ev = Event.current;
            int pick = -1;
            Keybinds.SetScreenNumbers(shown.Count);  // (its tool keys win over abilities bound to those numbers)
            if (ev.type == EventType.KeyDown && ev.keyCode >= KeyCode.Alpha1 && ev.keyCode < KeyCode.Alpha1 + shown.Count)
            {
                pick = shown[ev.keyCode - KeyCode.Alpha1];
                ev.Use();
            }
            bool sliding = RailSlide(cur, 92f + Mathf.Max(0, shown.IndexOf(cur)) * 42f);  // the selection frame slides to a new tool (BuildMotion.cs)
            for (int si0 = 0; si0 < shown.Count; si0++)
            {
                int i = shown[si0];
                var r = new Rect(x, 92f + si0 * 42f, w, 36f);
                bool on = i == cur && !sliding;
                if (DkKey(r, names[i], on, calm, size: 15, rawSize: true))
                    pick = i;
                Ui.Label(new Rect(r.x + 8f, r.y, 14f, r.height), (si0 + 1).ToString(), 10, on ? CrtStyle.Gold : Ui.Muted, TextAnchor.MiddleLeft, true);
                if (TipHover(r))
                {
                    string tt, tp;
                    string[] st;
                    BuildToolHelp(i, out tt, out st, out tp);
                    TipStart(r, tt, CrtStyle.Ink);
                    for (int si = 0; si < st.Length; si++)
                        TipAdd((si + 1) + "  " + st[si], 12, Ui.Text);
                    TipAdd(tp, 11, Ui.Muted);
                }
            }
            if (pick >= 0 && pick != cur)
            {
                expLinkMode = pick == 1;
                expGateMode = pick == 2;
                expTrapMode = pick == 3;
                expDragging = false;
                expA = expB = V2.Zero;
            }
            if (sliding)
                DrawRailFrame(x, w);
            float y = 92f + shown.Count * 42f + 6f;
            if (expTrapMode && game.Me != null)  // the traps in your stash: pick which one a click places
            {
                for (int tk = 1; tk < Market.Count; tk++)
                {
                    if (DkKey(new Rect(x, y, w, 28f), Market.Names[tk].ToUpperInvariant() + "  ·  " + game.Me.Stash[tk], tk == expTrapKind, calm, size: 12, rawSize: true))
                        expTrapKind = tk;
                    y += 32f;
                }
                y += 4f;
            }
            if (game.RoomTokens.Count > 0)
            {
                if (DkKey(new Rect(x, y, w, 32f), "ROOM KITS  ·  " + game.RoomTokens.Count, false, calm, size: 14, rawSize: true))
                {
                    expModify = true;
                    expKit = 0;
                }
                y += 42f;
            }
            // The legend: what the colours on the map are.
            var items = new List<KeyValuePair<string, Rgba>> { new KeyValuePair<string, Rgba>("YOUR MAP", new Rgba(0.4f, 0.64f, 1f)), new KeyValuePair<string, Rgba>("DOOR", new Rgba(0.37f, 0.94f, 1f)) };
            var seen = new HashSet<int>();
            foreach (var nd in game.Nodes)
                if (!Expansion.NodeClaimed(game.Expanded, nd) && seen.Add(nd.Hidden ? -1 : (int)nd.Kind))
                    items.Add(new KeyValuePair<string, Rgba>((nd.Hidden ? "UNKNOWN NODE" : NodeName(nd.Kind) + " NODE").ToUpperInvariant(), NodeInkOf(nd)));
            items.Add(new KeyValuePair<string, Rgba>("WALL", HoloWallInk));  // the map's floor (BuildMapLook.cs): walls, cover, and what's on it this wave
            items.Add(new KeyValuePair<string, Rgba>("COVER", HoloCoverInk));
            if (game.ActiveBreaches.Length > 0)
                items.Add(new KeyValuePair<string, Rgba>("BREACH", HoloBreachInk));
            if (game.GateFits.Count > 0)
                items.Add(new KeyValuePair<string, Rgba>("GATE", HoloGateInk));
            if (game.ExpansionWorld.OneWays.Length > 0)
                items.Add(new KeyValuePair<string, Rgba>("ONE-WAY DOOR", OneWayInk));  // (OneWayDoorLook.cs)
            if (game.MysteryDoors.Count > 0)
                items.Add(new KeyValuePair<string, Rgba>("MYSTERY DOOR", MysteryHolo));  // (MysteryDoorLook.cs)
            if (game.Links.Count > 0)
                items.Add(new KeyValuePair<string, Rgba>("PORTAL", PortalColours[(game.ExpansionWorld.Portals.Length - game.Links.Count) % PortalColours.Length]));
            bool sealedRoom = false;
            var sealedKinds = new HashSet<RoomKind>();
            foreach (var fa in game.ExpansionWorld.Areas)
                if (fa.Sealed && sealedKinds.Add(fa.Kind))
                {
                    sealedRoom = true;
                    items.Add(new KeyValuePair<string, Rgba>(fa.Kind == RoomKind.Shop ? "SHOP ROOM" : fa.Kind == RoomKind.BlackMarket ? "BLACK MARKET" : fa.Kind == RoomKind.Arms ? "ARMS DEALER" : fa.Kind == RoomKind.Medic ? "MEDIC" : "SEALED ROOM", KindInk(fa.Kind, PathId.None)));
                }
            float lh = 34f + items.Count * 20f;
            float ly = Mathf.Max(y + 8f, H - 30f - lh);
            DkSub(new Rect(x, ly, w, lh));
            Ui.Label(new Rect(x + 10f, ly + 6f, w - 20f, 16f), "ON THE MAP", 10, Ui.Muted, TextAnchor.MiddleLeft, true);
            for (int i = 0; i < items.Count; i++)
            {
                var c = (items[i].Value).ToColor();
                float iy = ly + 30f + i * 20f;
                Ui.Fill(new Rect(x + 11f, iy + 3f, 10f, 10f), c.WithAlpha(0.3f));
                CrtKit.Frame(new Rect(x + 10f, iy + 2f, 12f, 12f), 1.5f, c);
                Ui.Label(new Rect(x + 30f, iy, w - 36f, 16f), items[i].Key, 11, c, TextAnchor.MiddleLeft, true);
            }
            if ((seen.Count > 0 || sealedRoom) && TipHover(new Rect(x, ly, w, lh)))
            {
                TipStart(new Rect(x, ly, w, lh), "On the map", CrtStyle.Ink);
                TipAdd("Build a room round a node or up to a sealed room to claim it.", 12, Ui.Text);
            }
        }

        /// The top of the draw screen's guide panel: the wallet (with what the drawing would cost), the tool's name and its three steps.
        /// Returns where the panel's next part goes.
        float DrawBuildPanelTop(Rect p, int pending, int gateDoor)
        {
            DkSub(p, new Color(0.02f, 0.05f, 0.12f, 0.6f));
            float x = p.x + 14f, w = p.width - 28f, y = p.y + 10f;
            int have = game.SpendableVectors(game.Me);
            Ui.Label(new Rect(x, y, w, 30f), "YOUR VECTORS", 11, Ui.Muted, TextAnchor.MiddleLeft, true);
            var numR = new Rect(x, y - 2f, w, 34f);
            CrtUi.Text(new Rect(numR.x, numR.y + WalletTickDrop, numR.width, numR.height), WalletShown(have).ToString(), 30, CrtStyle.Gold, TextAnchor.MiddleRight, true, FxCalm);  // counting and rolling (BuildMotion.cs)
            y += 36f;
            int cap = Mathf.Max(Mathf.Max(have, game.Me != null ? game.Me.VectorCap : have), 1);
            var bar = new Rect(x, y, w, 8f);
            Ui.Fill(bar, new Color(0f, 0f, 0f, 0.5f));
            CrtKit.Frame(bar, 1f, new Color(1f, 1f, 1f, 0.12f));
            float fHave = Mathf.Clamp01((float)have / cap), fAfter = Mathf.Clamp01((float)(have - pending) / cap);
            bool over = pending > have;
            Ui.Fill(new Rect(bar.x, bar.y, bar.width * Mathf.Min(fHave, fAfter), bar.height), CrtStyle.Gold);
            if (pending > 0)  // the slice the drawing would spend, blinking
                Ui.Fill(new Rect(bar.x + bar.width * fAfter, bar.y, bar.width * (fHave - fAfter), bar.height), over ? new Color(1f, 0.3f, 0.36f, 0.9f) : new Color(1f, 0.95f, 0.75f, 0.45f + 0.35f * Mathf.Sin(Time.unscaledTime * 5f)));
            else if (pending < 0)
                Ui.Fill(new Rect(bar.x + bar.width * fHave, bar.y, bar.width * (fAfter - fHave), bar.height), new Color(0.35f, 1f, 0.55f, 0.8f));
            DrawWalletAfter(bar, numR, cap);  // the spent slice draining, "-8" floating up
            y += 12f;
            string sub = pending > 0 ? (over ? "Needs " + pending + ": " + (pending - have) + " short" : "This uses " + pending + "  ·  " + (have - pending) + " left") : pending < 0 ? "Gives back " + -pending : "";
            if (TipHover(new Rect(p.x, p.y, p.width, y + 18f - p.y)))
            {
                TipStart(new Rect(p.x, p.y, p.width, y + 18f - p.y), "Vectors", CrtStyle.Ink);
                TipAdd("What rooms cost. Touching rooms cost less, and opening walls gives some back.", 12, Ui.Text);
            }
            Ui.Label(new Rect(x, y, w, 16f), sub, 11, over ? new Color(1f, 0.45f, 0.5f) : pending != 0 ? CrtStyle.Gold : Ui.Muted, TextAnchor.MiddleLeft, true);
            y += 30f;
            CrtKit.Frame(new Rect(x, y - 8f, w, 1f), 1f, new Color(1f, 1f, 1f, 0.08f));
            string title, tip;
            string[] steps;
            BuildToolHelp(BuildTool, out title, out steps, out tip);
            CrtUi.Text(new Rect(x, y, w, 26f), TypedTitle(title), 20, CrtStyle.Ink, TextAnchor.MiddleLeft, true, FxCalm);  // typing in (BuildMotion.cs)
            y += 32f;
            return y + 4f;
        }

        /// The build tool in use: 0 pieces, 1 teleporter, 2 gate, 3 trap.
        int BuildTool => expTrapMode ? 3 : expGateMode ? 2 : expLinkMode ? 1 : 0;

        /// What each build tool does, for its hover tip (BuildTool's numbers).
        void BuildToolHelp(int tool, out string title, out string[] steps, out string tip)
        {
            if (tool == 2)
            {
                title = "FIT A GATE";
                steps = new[] { "Point at a door (gold squares)", "Click it to fit a gate: " + Gates.Price + " vectors", "In a wave, by it press " + Keybinds.KeyName(Keybinds.Get(KeyAction.Interact)).ToUpperInvariant() };
                tip = "Shuts for " + (int)Gates.CloseTime + " s, " + (int)Gates.Cooldown + " s to recharge.  " + game.GateFits.Count + " of " + Gates.Max + " fitted.";
            }
            else if (tool == 1)
            {
                title = "TELEPORTER PAIR";
                steps = new[] { "Click a room's outer wall", "Click another room's outer wall", "Walk into one, out of the other", "Click a built end to dismantle it" };
                tip = "Uses one from your stash; dismantling gives it back (" + (game.Me != null ? game.Me.Stash[(int)BuildKind.Teleporter] : 0) + "). " + game.Links.Count + " of " + Expansion.MaxLinks + " built.";
            }
            else if (tool == 3)
            {
                title = "PLACE A TRAP";
                steps = new[] { "Pick one from your stash", "Point at a spot in a room", "Click to place it" };
                tip = "Bought at the Black Market. They stand on the map, " + game.Placed.Count + " of " + Market.MaxPlaced + ". Turrets fight " + Market.TurretWaves + " waves, " + Market.TurretsEach + " each; the others have no effect yet.";
            }
            else
            {
                title = "PLACE A PIECE";
                steps = new[] { "Pick a piece from your hand", "Point at a glowing door spot on a wall", "Click to build it  ·  R turns it" };
                tip = "Every room is a piece card. Pieces snap to door spots on the walls of rooms you have built, and playing one is free.";
            }
        }

        /// Under the wave plate: your vectors and the most you can hold. True if it drew (the next chip goes a line up).
        /// The room shops are open this intermission: a chip that says where to walk.
        bool DrawServiceChip(float x, float y)
        {
            var me = game.Me;
            if (!game.ShopOpen || !game.ServiceRooms || me == null || me.ShopDone)
                return false;
            var where = new List<string>();
            if (game.HasShopRoom) where.Add("shop room");
            if (game.HasBlackMarketRoom) where.Add("Black Market");
            if (game.HasArmsRoom) where.Add("Arms Dealer");
            if (game.HasMedicRoom) where.Add("Medic");
            string text = "SHOP OPEN  ·  " + me.Gold + " GOLD  ·  walk into the " + (where.Count > 1 ? string.Join(", ", where.GetRange(0, where.Count - 1).ToArray()) + " or " + where[where.Count - 1] : where.Count == 1 ? where[0] : "shop");
            float w = 30f + text.Length * 6.2f;
            var chip = new Rect(x, y, w, 20f);
            Ui.Pill(chip, new Color(0f, 0f, 0f, 0.55f));
            Ui.Fill(new Rect(chip.x + 3f, chip.y + 4f, 3f, chip.height - 8f), CrtStyle.Gold);
            Ui.Label(new Rect(chip.x + 12f, chip.y, w, chip.height), text, 10, CrtStyle.Gold, TextAnchor.MiddleLeft, true);
            return true;
        }

        bool DrawVectorChip(float x, float y)
        {
            var me = game.Me;
            if (!game.ExpandingOn || me == null)
                return false;
            bool full = me.Vectors >= me.VectorCap;
            string text = "VECTORS  " + VectorChipCount(me) + " / " + me.VectorCap + (full ? "  FULL" : "");  // (counting up as vectors come in, VectorGainLook.cs)
            float w = 30f + text.Length * 6.4f;
            var chip = new Rect(x, y, w, 20f);
            vecChipRect = chip;
            float lit = VectorChipFlash;
            var ink = Color.Lerp(new Color(0.37f, 0.94f, 1f), Color.white, 0.6f * lit);
            if (full && vecFullNear)  // a pickup or Stash waiting by you: the chip pulses amber (VectorGainLook.cs)
            {
                float fp = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
                ink = Color.Lerp(ink, VecFullInk, 0.7f + 0.3f * fp);
                Ui.Glow(chip, VecFullInk, 0.4f + 0.4f * fp);
            }
            Ui.Glow(chip, new Color(0.3f, 0.88f, 1f), 0.9f * lit);
            Ui.Pill(chip, Color.Lerp(new Color(0f, 0f, 0f, 0.55f), new Color(0.05f, 0.3f, 0.4f, 0.9f), lit));
            Ui.Fill(new Rect(chip.x + 3f, chip.y + 4f, 3f, chip.height - 8f), ink);
            Ui.Label(new Rect(chip.x + 12f, chip.y, w, chip.height), text, 10, ink, TextAnchor.MiddleLeft, true);
            if (vecGainAge < VecGainSpan && vecGainAge > 0.3f)  // "+10" beside it while they come in
                Ui.Label(new Rect(chip.xMax + 6f, chip.y, 80f, chip.height), "+" + vecGainN, 12, new Color(0.55f, 1f, 1f, Mathf.Clamp01((VecGainSpan - vecGainAge) / 0.5f)), TextAnchor.MiddleLeft, true);
            return true;
        }

    }
}
