using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// How the Warden's net and Holding Cells look (Core/HoldingCell.cs; mockup Docs/Mockups/warden-net-cell.html).
    ///   world   the cell block far below the arena: three cells side by side, each a 600 px floor of worn tiles with a
    ///           cot, a steel toilet and a hanging lamp, ringed by bars with a lit gap in every wall where the guards
    ///           squeeze in; an empty cell is dark. The bottom bars slide apart when the door opens.
    ///   padlock where someone was netted, out in the arena: a little barred square, a red padlock that flashes white
    ///           on each hit, and a ring that empties as their clock runs down.
    ///   net     thrown from the Warden, spinning open on its way, then wrapped tight round the one it caught.
    ///   camera  locked in a cell, your view moves to the cell block (CellViewFocus): the occupied cells, yours always whole.
    ///   HUD     the HOLDING CELL panel (clock, bar, what speeds it up, how the fight stands), the "-1 S" pops, the fades
    ///           in and out, and for those still fighting a strip naming who's locked up and the padlock to shoot.
    /// Preview: HOLDCELL=<s> [HOLDCELLS=2] [HOLDLOCK=1] shot.sh x hud 10 1 2 (ShotScenes.cs).
    public partial class ArenaApp
    {
        static readonly Rgba CellFloor = new Rgba(0.075f, 0.1f, 0.13f), CellTile = new Rgba(0.11f, 0.14f, 0.18f);
        static readonly Rgba CellBar = new Rgba(0.5f, 0.57f, 0.64f), CellBarHi = new Rgba(0.78f, 0.84f, 0.9f), CellRail = new Rgba(0.28f, 0.33f, 0.4f);
        static readonly Rgba CellGapGlow = new Rgba(1f, 0.68f, 0.28f), PadlockCol = new Rgba(0.95f, 0.28f, 0.25f), NetCol = new Rgba(0.82f, 0.74f, 0.55f);
        static readonly Color CellInk = CrtStyle.Gold;

        bool camCell;  // your view is on the cell block (CameraZoom.cs)

        /// Where the camera looks while you're locked in: the middle of the occupied cells, holding yours whole on screen.
        V2 CellViewFocus(Player me)
        {
            int mine = Game.CellSlotAt(me.Pos, Game.CellZone);
            float sum = 0f;
            int n = 0;
            for (int s = 0; s < 3; s++)
                if (s == mine || game.CellInSlot(s) != null)
                {
                    sum += Game.CellCentre(s).X;
                    n++;
                }
            float x = n > 0 ? sum / n : Game.CellCentre(mine).X, own = Game.CellCentre(mine).X, room = AW / 2f - Game.CellHalf - 40f;
            x = Mathf.Clamp(x, own - room, own + room);
            return new V2(x, Game.CellY - 12f);
        }

        // --- The world ------------------------------------------------------------------------------------

        /// On the floor: the cell block (when anyone's locked up) and the padlocks out in the arena.
        void DrawHoldingCells(float t)
        {
            if (game.HoldingCells.Count == 0 && !Game.InCellZone(game.Me != null ? game.Me.Pos : V2.Zero))
                return;
            V2 c0 = Game.CellCentre(0), c2 = Game.CellCentre(2);
            float pad = Game.CellPitch;  // the block's dark yard round the cells, past the edge of any view
            FillRect(c0.X - pad, c0.Y - pad, c2.X - c0.X + pad * 2f, pad * 2f, new Rgba(0.02f, 0.025f, 0.04f));
            // The corridor the guards come along: a lane across the block and one up and down past each cell.
            FillRect(c0.X - pad, Game.CellY - 70f, c2.X - c0.X + pad * 2f, 140f, new Rgba(0.045f, 0.055f, 0.075f));
            for (int s = 0; s < 3; s++)
            {
                V2 c = Game.CellCentre(s);
                FillRect(c.X - 70f, c.Y - pad, 140f, pad * 2f, new Rgba(0.045f, 0.055f, 0.075f));
                for (float y = c.Y - pad; y < c.Y + pad; y += 60f)  // worn lane markings
                    FillRect(c.X - 2f, y, 4f, 26f, new Rgba(0.9f, 0.7f, 0.3f, 0.06f));
            }
            for (int s = 0; s < 3; s++)
                DrawCell(s, game.CellInSlot(s), t);
            foreach (var cell in game.HoldingCells)
                DrawPadlock(cell, t);
        }

        void DrawCell(int slot, HoldingCell cell, float t)
        {
            V2 c = Game.CellCentre(slot);
            float h = Game.CellHalf, lit = cell != null ? 1f : 0.35f;
            FillRect(c.X - h, c.Y - h, h * 2f, h * 2f, CellFloor.Mix(new Rgba(0f, 0f, 0f), 1f - lit));
            // Floor tiles, a few cracked.
            for (float x = -h; x < h; x += 50f)
                for (float y = -h; y < h; y += 50f)
                {
                    int k = (int)((x + 1000f) * 7f + (y + 1000f) * 13f + slot * 31f) % 9;
                    FillRect(c.X + x + 2f, c.Y + y + 2f, 46f, 46f, CellTile.WithAlpha((k == 0 ? 0.55f : 0.9f) * lit));
                }
            // The lamp's pool, soft.
            for (int k = 0; k < 10; k++)
                Circle(c, h * (0.95f - k * 0.085f), new Rgba(1f, 0.93f, 0.75f, 0.011f * lit), 48);
            // A cot along the left wall, a steel toilet in the top right, a drain in the middle, the cell's number.
            FillRect(c.X - h + 14f, c.Y - h + 20f, 70f, 170f, new Rgba(0.16f, 0.19f, 0.24f, lit));
            FillRect(c.X - h + 18f, c.Y - h + 24f, 62f, 34f, new Rgba(0.42f, 0.45f, 0.5f, lit));  // the pillow
            Line(new V2(c.X - h + 18f, c.Y - h + 120f), new V2(c.X - h + 80f, c.Y - h + 120f), 2f, new Rgba(0.1f, 0.12f, 0.16f, lit));
            Circle(new V2(c.X + h - 44f, c.Y - h + 46f), 20f, new Rgba(0.45f, 0.5f, 0.56f, lit), 20);
            Circle(new V2(c.X + h - 44f, c.Y - h + 46f), 12f, new Rgba(0.12f, 0.15f, 0.19f, lit), 16);
            Arc(c + new V2(0f, 40f), 12f, 2f, new Rgba(0.3f, 0.34f, 0.4f, lit), 16);
            for (int k = -1; k <= 1; k++)
                Line(c + new V2(-8f, 40f + k * 4f), c + new V2(8f, 40f + k * 4f), 1.5f, new Rgba(0.3f, 0.34f, 0.4f, lit));
            if (cell != null)  // the gaps glow on the floor while guards can come through
            {
                float a = 0.1f + 0.05f * Mathf.Sin(t * 5f), gw = Game.CellGap;
                FillRect(c.X - gw, c.Y - h, gw * 2f, 34f, CellGapGlow.WithAlpha(a));
                FillRect(c.X - gw, c.Y + h - 34f, gw * 2f, 34f, CellGapGlow.WithAlpha(a));
                FillRect(c.X - h, c.Y - gw, 34f, gw * 2f, CellGapGlow.WithAlpha(a));
                FillRect(c.X + h - 34f, c.Y - gw, 34f, gw * 2f, CellGapGlow.WithAlpha(a));
            }
            float open = cell != null && cell.Out ? Mathf.Clamp01(cell.OutT / 0.35f) : 0f;
            DrawCellBars(c, open, lit);
            if (cell == null || cell.PlayerId == game.MyId)
                return;  // (your own clock is the HUD panel's)
            // The clock over the cell: a ring that empties.
            V2 ringAt = c + new V2(0f, -h - 56f);
            var pc = game.Players.ContainsKey(cell.PlayerId) ? game.Players[cell.PlayerId].Def.Color : CellGapGlow;
            Circle(ringAt, 30f, new Rgba(0.02f, 0.03f, 0.05f, 0.85f), 28);
            Arc(ringAt, 26f, 5f, pc.WithAlpha(0.2f), 32);
            Arc(ringAt, 26f, 5f, pc, 32, -MathX.Pi / 2f, MathX.Tau * Mathf.Clamp01(cell.Left / Game.CellTime));
        }

        /// The bars, seen from above: a steel rail along each wall and a round bar every 22 px, a gap in the middle of each
        /// wall; `open` slides the bottom wall's two halves apart (the door).
        void DrawCellBars(V2 c, float open, float lit)
        {
            float h = Game.CellHalf, gap = Game.CellGap, slide = open * (h - gap);
            for (int side = 0; side < 4; side++)
            {
                V2 along = side == 0 || side == 2 ? new V2(1f, 0f) : new V2(0f, 1f);
                V2 mid = c + (side == 0 ? new V2(0f, -h) : side == 1 ? new V2(h, 0f) : side == 2 ? new V2(0f, h) : new V2(-h, 0f));
                float shift = side == 2 ? slide : 0f;
                for (int half = -1; half <= 1; half += 2)
                {
                    V2 a = mid + along * (half * (gap + shift)), b = mid + along * (half * (h + shift));
                    Line(a, b, 5f, CellRail.WithAlpha(lit));
                    for (float d = gap + 6f; d <= h - 6f; d += 22f)
                    {
                        V2 p = mid + along * (half * (d + shift));
                        Circle(p, 4.2f, CellBar.WithAlpha(lit), 10);
                        Circle(p + new V2(-1.2f, -1.2f), 1.6f, CellBarHi.WithAlpha(0.8f * lit), 6);
                    }
                }
            }
            foreach (var k in new[] { new V2(-1f, -1f), new V2(1f, -1f), new V2(1f, 1f), new V2(-1f, 1f) })
                FillRect(c.X + k.X * h - 8f, c.Y + k.Y * h - 8f, 16f, 16f, new Rgba(0.42f, 0.48f, 0.55f, lit));
        }

        /// Out in the arena where someone was netted: a barred square with a padlock, and the ring of their clock.
        void DrawPadlock(HoldingCell cell, float t)
        {
            if (cell.Out)
                return;
            V2 at = cell.At;
            Player p;
            var pc = game.Players.TryGetValue(cell.PlayerId, out p) ? p.Def.Color : CellGapGlow;
            float flash = Mathf.Clamp01(1f - (Time.unscaledTime - LockFlashAt(cell)) / 0.12f);
            FillRect(at.X - 22f, at.Y - 22f, 44f, 44f, new Rgba(0.03f, 0.04f, 0.06f, 0.85f));
            for (int k = -2; k <= 2; k++)
                Line(new V2(at.X + k * 9f, at.Y - 22f), new V2(at.X + k * 9f, at.Y + 22f), 2f, CellBar.WithAlpha(0.8f));
            Line(new V2(at.X - 22f, at.Y - 22f), new V2(at.X + 22f, at.Y - 22f), 3f, CellRail);
            Line(new V2(at.X - 22f, at.Y + 22f), new V2(at.X + 22f, at.Y + 22f), 3f, CellRail);
            var lc = PadlockCol.Mix(Rgba.White, flash);
            Arc(at + new V2(0f, -3f), 7f, 3f, lc, 12, MathX.Pi, MathX.Pi);  // the shackle
            FillRect(at.X - 10f, at.Y - 3f, 20f, 15f, lc);
            FillRect(at.X - 1.5f, at.Y + 1f, 3f, 7f, new Rgba(0.2f, 0.03f, 0.04f));
            if (flash > 0f)
                Arc(at + new V2(0f, 4f), 18f + 10f * (1f - flash), 2f, Rgba.White.WithAlpha(flash * 0.8f), 20);
            Arc(at, 34f, 4f, pc.WithAlpha(0.22f), 36);
            Arc(at, 34f, 4f, pc, 36, -MathX.Pi / 2f, MathX.Tau * Mathf.Clamp01(cell.Left / Game.CellTime));
            Arc(at, 40f + 3f * Mathf.Sin(t * 4f), 1.5f, CellGapGlow.WithAlpha(0.35f), 36);  // "shoot me"
        }

        /// The net, over the bodies: spinning open on its way from the Warden, then wrapped tight round them.
        void DrawWardenNet(float t)
        {
            var e = game.TheWarden();
            if (e == null || e.Warden == null || e.Warden.NetId == 0)
                return;
            var w = e.Warden;
            Player p;
            if (!game.Players.TryGetValue(w.NetId, out p))
                return;
            float k = w.NetT / Game.NetFlight;
            if (k < 1f)
            {
                V2 from = w.NetFrom, to = p.Disp, at = V2.Lerp(from, to, k) + new V2(0f, -Mathf.Sin(k * MathX.Pi) * 70f);
                for (float f = 0f; f < k; f += 0.06f)  // the rope trailing back to its hand
                {
                    V2 q = V2.Lerp(from, to, f) + new V2(0f, -Mathf.Sin(f * MathX.Pi) * 70f);
                    Circle(q, 1.6f, NetCol.WithAlpha(0.45f), 6);
                }
                DrawNetMesh(at, 10f + 10f * k, w.NetT * 12f, 1f);
                return;
            }
            float wrap = Mathf.Clamp01((w.NetT - Game.NetFlight) / 0.15f), r = Mathf.Lerp(38f, p.Def.BodyR + 7f, wrap);
            DrawNetMesh(p.Disp, r, 0.3f + Mathf.Sin(t * 14f) * 0.04f * wrap, 1f);
            if (wrap < 1f)
                Arc(p.Disp, r + 16f * (1f - wrap), 3f, NetCol.WithAlpha(1f - wrap), 24);
        }

        void DrawNetMesh(V2 c, float r, float rot, float a)
        {
            V2 ux = V2.FromAngle(rot) * r, uy = V2.FromAngle(rot + MathX.Pi / 2f) * r;
            for (int i = -3; i <= 3; i++)
            {
                float f = i / 3f;
                Line(c + ux * f - uy, c + ux * f + uy, 1.4f, NetCol.WithAlpha(0.85f * a));
                Line(c + uy * f - ux, c + uy * f + ux, 1.4f, NetCol.WithAlpha(0.85f * a));
            }
            foreach (var s in new[] { ux + uy, ux - uy, -ux - uy, -ux + uy })
                Circle(c + s, 3.6f, new Rgba(0.56f, 0.54f, 0.48f, a), 8);  // the weights
        }

        // --- The HUD --------------------------------------------------------------------------------------

        sealed class CellPop { public string Text; public float At; public Vector2 Pos; public bool Big; }
        readonly List<CellPop> cellPops = new List<CellPop>();
        readonly Dictionary<int, int> seenSkills = new Dictionary<int, int>(), seenLocks = new Dictionary<int, int>();
        readonly Dictionary<int, float> lockFlashAt = new Dictionary<int, float>();

        float LockFlashAt(HoldingCell c)
        {
            float f;
            return lockFlashAt.TryGetValue(c.PlayerId, out f) ? f : -9f;
        }

        /// The pops off a cell's clock as its counts go up (an ability inside, a hit on its padlock).
        void NoteCellChanges(Player me)
        {
            float now = Time.unscaledTime;
            foreach (var c in game.HoldingCells)
            {
                int skills, locks;
                if (!seenSkills.TryGetValue(c.PlayerId, out skills) || !seenLocks.TryGetValue(c.PlayerId, out locks))
                {
                    seenSkills[c.PlayerId] = c.Skills;  // (first seen: no pops for what came before)
                    seenLocks[c.PlayerId] = c.LockHits;
                    continue;
                }
                bool mine = me != null && c.PlayerId == me.Id;
                if (c.Skills > skills && mine)
                    cellPops.Add(new CellPop { Text = "-" + (c.Skills - skills) + " S", At = now, Pos = new Vector2(W / 2f + 120f, 64f), Big = true });
                if (c.LockHits > locks)
                {
                    lockFlashAt[c.PlayerId] = now;
                    V2 ui = ToUi(c.At);
                    if (!mine)
                        cellPops.Add(new CellPop { Text = "-0.25", At = now, Pos = new Vector2(ui.X + 26f, ui.Y - 30f) });
                    else if (c.LockHits / 4 > locks / 4)
                        cellPops.Add(new CellPop { Text = "-1 S  LOCK", At = now, Pos = new Vector2(W / 2f + 120f, 88f) });
                }
                seenSkills[c.PlayerId] = c.Skills;
                seenLocks[c.PlayerId] = c.LockHits;
            }
            if (game.HoldingCells.Count == 0)
            {
                seenSkills.Clear();
                seenLocks.Clear();
            }
            cellPops.RemoveAll(q => now - q.At > 0.9f);
        }

        /// How dark your screen is for the net and the cell's doors (0 clear, 1 black).
        float CellFade(Player me)
        {
            if (me == null)
                return 0f;
            var e = game.TheWarden();
            if (e != null && e.Warden != null && e.Warden.NetId == me.Id)
                return Mathf.Clamp01((e.Warden.NetT - (Game.NetMoveAt - 0.6f)) / 0.5f);
            var cell = game.CellOf(me);
            if (cell == null)
                return Game.InCellZone(me.Pos) && !me.Downed ? 1f : 0f;  // (moved in, the snapshot with the cell still on its way)
            if (cell.Out)
                return cell.OutT < Game.CellOutMove ? Mathf.Clamp01((cell.OutT - 0.2f) / (Game.CellOutMove - 0.2f))
                    : Mathf.Clamp01(1f - (cell.OutT - Game.CellOutMove) / (Game.CellOutEnd - Game.CellOutMove));
            return Mathf.Clamp01(1f - cell.Age / Game.CellFadeIn);
        }

        void DrawHoldingCellHud(Player me)
        {
            NoteCellChanges(me);
            float fade = CellFade(me);
            if (fade > 0f)
                Ui.Fill(new Rect(0f, 0f, W, H), new Color(0f, 0f, 0f, fade));
            var e = game.TheWarden();
            if (me != null && e != null && e.Warden != null && e.Warden.NetId == me.Id && fade > 0.3f)
                Ui.Title(new Rect(0f, H / 2f - 40f, W, 80f), "NETTED", 54, new Color(0.95f, 0.3f, 0.3f, Mathf.Clamp01((fade - 0.3f) * 2f)));
            var mine = game.CellOf(me);
            if (mine != null && !mine.Out)
                DrawCellPanel(me, mine);
            else if (me != null && !me.Downed)
                DrawLockedUpStrip(me);
            float now = Time.unscaledTime;
            foreach (var q in cellPops)
            {
                float f = (now - q.At) / 0.9f;
                Ui.Shadowed(new Rect(q.Pos.x - 60f, q.Pos.y - 12f - f * 26f, 120f, 24f), q.Text, q.Big ? 20 : 13, CellInk.WithAlpha(1f - f * f));
            }
        }

        /// Locked in: the panel under the boss bar: the cell, its clock and bar, what speeds it up, how the fight stands.
        void DrawCellPanel(Player me, HoldingCell cell)
        {
            var r = new Rect(W / 2f - 210f, 42f, 420f, 70f);
            DkHud(r, CellInk);
            Ui.Label(new Rect(r.x + 14f, r.y + 7f, 260f, 16f), "HOLDING CELL " + Game.CellName(cell.Slot), 13, CellInk, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(r.x + 14f, r.y + 24f, 280f, 14f), "HOLD OUT UNTIL THE DOOR OPENS  ·  EVERY ABILITY -1 S", 10, CrtStyle.Body, TextAnchor.MiddleLeft);
            Ui.Title(new Rect(r.xMax - 84f, r.y + 2f, 72f, 44f), Mathf.CeilToInt(cell.Left).ToString(), 36, Color.white, TextAnchor.MiddleRight);
            DkBar(new Rect(r.x + 14f, r.y + 44f, r.width - 28f, 5f), cell.Left / Game.CellTime, CellInk);
            string fight;
            Color fc;
            if (game.ArenaHeld)
            {
                fight = game.HoldingCells.Count > 1 ? "EVERYONE LOCKED UP  ·  FIGHT PAUSED" : "FIGHT PAUSED";
                fc = CrtStyle.Danger;
            }
            else
            {
                fight = "THE FIGHT GOES ON  ·  YOUR TEAM CAN SHOOT YOUR PADLOCK";
                fc = CrtStyle.Head;
            }
            Ui.Label(new Rect(r.x + 14f, r.y + 53f, r.width - 28f, 13f), fight, 10, fc, TextAnchor.MiddleLeft, true);
            foreach (var c in game.HoldingCells)  // the other cells' clocks, over their cells (the block is in view)
            {
                if (c == cell || c.Out)
                    continue;
                V2 ui = ToUi(Game.CellCentre(c.Slot) + new V2(0f, -Game.CellHalf - 56f));
                Ui.Shadowed(new Rect(ui.X - 30f, ui.Y - 10f, 60f, 20f), Mathf.CeilToInt(c.Left).ToString(), 15, Color.white);
            }
        }

        /// Still fighting: who's locked up, and the padlock to shoot (a clock over each one in the arena).
        void DrawLockedUpStrip(Player me)
        {
            float y = 74f;
            foreach (var c in game.HoldingCells)
            {
                if (c.Out || c.PlayerId == me.Id)
                    continue;
                Player p;
                if (!game.Players.TryGetValue(c.PlayerId, out p))
                    continue;
                var r = new Rect(W / 2f - 190f, y, 380f, 26f);
                DkHud(r, p.Def.Color.ToColor());
                Ui.Label(new Rect(r.x + 12f, r.y, r.width - 24f, r.height), p.Name.ToUpperInvariant() + " IS IN A HOLDING CELL · SHOOT THE PADLOCK", 11, p.Def.Color.ToColor(), TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(r.x + 12f, r.y, r.width - 24f, r.height), Mathf.CeilToInt(c.Left) + " S", 13, Color.white, TextAnchor.MiddleRight, true);
                y += 30f;
                V2 ui = ToUi(c.At);
                Ui.Shadowed(new Rect(ui.X - 40f, ui.Y - 34f * UiWorldScale - 26f, 80f, 20f), Mathf.CeilToInt(c.Left).ToString(), 15, Color.white);
            }
        }
    }
}
