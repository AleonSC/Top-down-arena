using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Wall push in the world (Docs/WallPush.md step 2): the Push wall key (C) by a wall between waves, push mode (the mouse picks a
    /// stretch of outer wall, the wheel sets its width, a click pushes it out a tile, holding keeps pushing, right click or C stops),
    /// the lit section with the next row as a ghost, the label with its price or why it can't go, and the prompt by a wall.
    /// The rules are Core's (Expansion.Push, PushPick); the host builds it (Game.PushWall).
    public partial class ArenaApp
    {
        static readonly Rgba PushGold = new Rgba(1f, 0.79f, 0.3f), PushRed = new Rgba(1f, 0.36f, 0.42f), PushGrey = new Rgba(0.5f, 0.6f, 0.66f);
        const float PushRepeatWait = 0.38f, PushRepeatGap = 0.2f;   // holding the button: the first repeat, then every this long
        const float PushUnlockPx = 30f;                              // the mouse moving this far (screen px) lets go of the section just pushed

        bool pushOn;                 // push mode
        int pushWidth = 4;           // the section's width in tiles (the wheel)
        bool pushLock;               // after a push the selection rides the section's new face (lockSide/Line/Start/Width) until the mouse moves
        int lockSide, lockLine, lockStart, lockWidth;
        Vector3 lockMouse;
        WallPush? pushSel;           // this frame's section (null: none, or the push is still on its way to the host)
        bool pushOk, pushFar;        // it can be pushed now; refused only for being too far
        string pushWhy;              // why not (shown in red)
        int pushCost, pushDepth;     // what this push costs, and how far out the section will be
        float pushNext, pushT = -9f, pushBadT = -9f;  // the next held repeat; the last push; the last refused click

        /// The preview's mouse (Tools/Preview PUSHSEL): the world point the mouse is over, instead of the real mouse.
        public static V2? PushMouseOverride;

        bool PushingWall => pushOn;

        /// ArenaApp's input: C by a wall starts push mode; a click pushes, holding repeats; the wheel sets the width; right click or C stops.
        void TickWallPush(Player me, bool free)
        {
            if (!free || me == null || me.Downed || !game.CanPush || ExpandWindowUp)
            {
                if (pushOn)
                    StopPush();
                return;
            }
            if (Keybinds.Down(KeyAction.PushWall))
            {
                if (pushOn)
                {
                    StopPush();
                    return;
                }
                if (WallToPushNear(me).HasValue)
                {
                    pushOn = true;
                    pushLock = false;
                }
            }
            if (!pushOn)
                return;
            if (Keybinds.Down(KeyAction.Parry))
            {
                StopPush();
                return;
            }
            float wheel = Input.mouseScrollDelta.y;
            if (wheel > 0.01f)
                SetPushWidth(pushWidth + 1);
            else if (wheel < -0.01f)
                SetPushWidth(pushWidth - 1);
            if (pushLock && Mathf.Abs(Input.mousePosition.x - lockMouse.x) + Mathf.Abs(Input.mousePosition.y - lockMouse.y) > PushUnlockPx)
                pushLock = false;
            PushRefresh(me);
            float now = Time.unscaledTime;
            if (Keybinds.Down(KeyAction.Fire))
            {
                TryPush(me, false);
                pushNext = now + PushRepeatWait;
            }
            else if (Keybinds.Held(KeyAction.Fire) && now >= pushNext && pushSel.HasValue && TryPush(me, true))
                pushNext = now + PushRepeatGap;
        }

        void StopPush()
        {
            pushOn = false;
            pushLock = false;
            pushSel = null;
        }

        void SetPushWidth(int w)
        {
            w = Mathf.Clamp(w, Expansion.PushMinWidth, Expansion.PushMaxWidth);
            if (w != pushWidth)
                pushLock = false;  // (a new width is a new section)
            pushWidth = w;
        }

        /// The outer wall within 3 tiles of you that C would start on (the prompt's wall), or null.
        WallPush? WallToPushNear(Player me) => Expansion.PushPick(game.Expanded, me.Pos, me.Pos, pushWidth, 300f);

        /// This frame's section and whether it can be pushed: the locked face (after a push), else the wall under the mouse.
        void PushRefresh(Player me)
        {
            pushSel = null; pushOk = pushFar = false; pushWhy = null; pushCost = pushDepth = 0;
            var rooms = game.Expanded;
            if (pushLock)
            {
                int a = Expansion.PushAreaAt(rooms, lockSide, lockLine, lockStart, lockWidth);
                if (a >= 0)
                    pushSel = new WallPush { Area = (short)a, Side = (byte)lockSide, Start = (short)lockStart, Width = (byte)lockWidth };
            }
            else
                pushSel = Expansion.PushPick(rooms, PushMouseOverride ?? MouseArena(), me.Pos, pushWidth);
            if (!pushSel.HasValue)
                return;
            int idx;
            string why;
            var made = Expansion.Push(rooms, pushSel.Value, out idx, out pushCost, out why);
            pushDepth = made.HasValue ? Expansion.PushDepth(made.Value, pushSel.Value.Side) : 0;
            if (!made.HasValue)
                pushWhy = why;
            else if (Expansion.PushFace(rooms, pushSel.Value).DistanceTo(me.Pos) > Expansion.PushReach)
            {
                pushWhy = "Walk closer";
                pushFar = true;
            }
            else if (pushCost > game.SpendableVectors(me))
                pushWhy = "Need " + pushCost + " vectors";
            else
                pushOk = true;
        }

        /// Send this frame's push; the selection then locks onto where the section's face will be. False if it can't go (a click flashes the reason).
        bool TryPush(Player me, bool held)
        {
            float now = Time.unscaledTime;
            if (!pushSel.HasValue || !pushOk)
            {
                if (!held && pushSel.HasValue)
                    pushBadT = now;
                return false;
            }
            var s = pushSel.Value;
            int line = Expansion.PushLine(game.Expanded, s);
            game.PushWall(s);
            lockSide = s.Side;
            lockLine = line + (s.Side == 0 || s.Side == 2 ? -Expansion.Step : Expansion.Step);
            lockStart = s.Start;
            lockWidth = s.Width;
            lockMouse = Input.mousePosition;
            pushLock = true;
            pushT = now;
            pushSel = null;  // (until the host's map comes back with the new face)
            sounds.PlayUiClick(now);
            return true;
        }

        /// The section's face: its two ends on the wall line, outward and along directions.
        void PushGeometry(WallPush s, out V2 a, out V2 b, out V2 n, out V2 along)
        {
            float line = Expansion.PushLine(game.Expanded, s);
            bool vertical = s.Side < 2;
            n = s.Side == 0 ? new V2(-1f, 0f) : s.Side == 1 ? new V2(1f, 0f) : s.Side == 2 ? new V2(0f, -1f) : new V2(0f, 1f);
            along = vertical ? new V2(0f, 1f) : new V2(1f, 0f);
            a = vertical ? new V2(line, s.Start) : new V2(s.Start, line);
            b = vertical ? new V2(line, s.End) : new V2(s.End, line);
        }

        /// WorldRenderer: in push mode, your reach, the lit section, the next row as a ghost and an arrow out.
        void DrawWallPush()
        {
            var me = game.Me;
            if (me == null || !game.ExpandingOn)
                return;
            if (!pushOn && PushMouseOverride == null)
            {
                var near = PushPrompt(me);  // the prompt's wall, faintly lit
                if (near.HasValue)
                {
                    V2 pa, pb, pn, pal;
                    PushGeometry(near.Value, out pa, out pb, out pn, out pal);
                    float glow = 0.35f + 0.2f * Mathf.Sin(game.AnimT * 3f);
                    Line(pa, pb, 12f, new Rgba(0.37f, 0.94f, 1f, 0.12f * glow));
                    DashedLine(pa - pn * 20f, pb - pn * 20f, 2f, new Rgba(0.37f, 0.94f, 1f, glow));
                }
                return;
            }
            if (PushMouseOverride != null)
            {
                pushOn = true;
                PushRefresh(me);  // (the preview draws without ticking)
            }
            float t = game.AnimT, now = Time.unscaledTime;
            Arc(me.Disp, Expansion.PushReach, 1.5f, PushGold.WithAlpha(0.1f), 72);
            if (!pushSel.HasValue)
                return;
            V2 a, b, n, along;
            PushGeometry(pushSel.Value, out a, out b, out n, out along);
            var col = pushOk ? PushGold : pushFar ? PushGrey : PushRed;
            float shake = now - pushBadT < 0.3f ? Mathf.Sin((now - pushBadT) * 60f) * 4f * (1f - (now - pushBadT) / 0.3f) : 0f;
            a += along * shake; b += along * shake;
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 6f), step = Expansion.Step;
            // The next row: a ghost tile row past the wall, dashed round its outer three sides.
            V2 a2 = a + n * step, b2 = b + n * step;
            SetColor(col.WithAlpha(0.08f + 0.07f * pulse));
            Quad(a, b, b2, a2);
            for (int k = 0; k < pushSel.Value.Width; k++)  // (its tiles, faintly)
            {
                V2 p = a + along * (k * step);
                Line(p + n * 6f, p + n * (step - 6f), 1f, col.WithAlpha(0.18f));
            }
            DashedLine(a, a2, 1.6f, col.WithAlpha(0.75f));
            DashedLine(a2, b2, 1.6f, col.WithAlpha(0.75f));
            DashedLine(b2, b, 1.6f, col.WithAlpha(0.75f));
            // The section itself, lit on the wall, with brackets at its ends.
            Line(a, b, 16f, col.WithAlpha(0.18f));
            Line(a, b, 5f, col);
            foreach (var e in new[] { a, b })
                Line(e - n * 14f, e + n * 22f, 3f, col);
            // An arrow out, bobbing.
            if (pushOk)
            {
                V2 m = (a + b) * 0.5f + n * (step * 1.45f + 6f * Mathf.Sin(t * 5f));
                SetColor(col);
                Tri(m + n * 14f, m - n * 6f + along * 12f, m - n * 6f - along * 12f);
            }
            // Just pushed: a white flash on the wall that moved.
            float since = now - pushT;
            if (since < 0.35f)
                Line(a - n * step, b - n * step, 6f, new Rgba(1f, 1f, 1f, 0.7f * (1f - since / 0.35f)));
        }

        /// The HUD: in push mode the section's label (price and depth, or why not) and the keys; otherwise, by a wall between waves, the C prompt.
        void DrawWallPushHint()
        {
            var me = game.Me;
            if (me == null || me.Downed || !game.ExpandingOn)
                return;
            if (pushOn && pushSel.HasValue)
            {
                V2 a, b, n, along;
                PushGeometry(pushSel.Value, out a, out b, out n, out along);
                V2 at = ToUi((a + b) * 0.5f + n * (Expansion.Step * (Mathf.Abs(n.X) > 0.5f ? 2.9f : 2.6f)));  // (beyond the arrow; further on a side wall, where the label is wide)
                var col = pushOk ? new Color(1f, 0.79f, 0.3f) : pushFar ? new Color(0.62f, 0.72f, 0.78f) : new Color(1f, 0.42f, 0.46f);
                string head = pushOk ? "PUSH +1  ·  " + pushCost + " VECTOR" + (pushCost == 1 ? "" : "S") : (pushWhy ?? "").ToUpperInvariant();
                var style = Expansion.PushStyleOfPush(game.Expanded, pushSel.Value);
                string sub = pushOk || pushFar ? pushSel.Value.Width + " WIDE  ·  " + pushDepth + " / " + Expansion.PushMaxDepth + " OUT" + (style != PushStyle.None ? "  ·  " + Expansion.PushStyleName(style).ToUpperInvariant() : "") : null;
                float w = Mathf.Max(150f, 26f + head.Length * 7.4f);
                var r = new Rect(at.X - w / 2f, at.Y - (sub != null ? 19f : 12f), w, sub != null ? 38f : 24f);
                r.x = Mathf.Clamp(r.x, 8f, W - r.width - 8f);  // (kept on screen: a wall at the view's edge)
                r.y = Mathf.Clamp(r.y, 90f, H - 150f);
                DkHud(r, col);
                Ui.Label(new Rect(r.x, r.y + 2f, r.width, 20f), head, 12, col, TextAnchor.MiddleCenter, true);
                if (sub != null)
                    Ui.Label(new Rect(r.x, r.y + 19f, r.width, 16f), sub, 10, Ui.Muted, TextAnchor.MiddleCenter, true);
            }
            if (pushOn)
            {
                // The keys, under you.
                V2 at = ToUi(me.Disp) + new V2(0f, 46f);
                string fire = PushKeyName(Keybinds.Get(KeyAction.Fire)), done = PushKeyName(Keybinds.Get(KeyAction.Parry));
                var keys = new[] { fire, "HOLD", "WHEEL", done };
                var what = new[] { "PUSH", "KEEP PUSHING", "WIDTH " + pushWidth, "DONE" };
                float total = 0f;
                for (int i = 0; i < keys.Length; i++)
                    total += keys[i].Length * 6.6f + 12f + what[i].Length * 6f + 16f;
                float x = at.X - total / 2f;
                for (int i = 0; i < keys.Length; i++)
                {
                    float kw = keys[i].Length * 6.6f + 12f, tw = what[i].Length * 6f + 6f;
                    var bg = new Rect(x - 3f, at.Y - 2f, kw + tw + 8f, 20f);
                    Ui.Fill(bg, new Color(CrtUi.Bg.r, CrtUi.Bg.g, CrtUi.Bg.b, 0.8f));
                    DkTag(new Rect(x, at.Y, kw, 16f), keys[i], new Color(1f, 0.79f, 0.3f));
                    Ui.Label(new Rect(x + kw + 4f, at.Y - 1f, tw + 4f, 18f), what[i], 10, CrtUi.Ink, TextAnchor.MiddleLeft, true);
                    x += kw + tw + 16f;
                }
                return;
            }
            var prompt = PushPrompt(me);
            if (!prompt.HasValue)
                return;
            V2 spot = ToUi(me.Disp) + new V2(0f, prompt.Value.Side == 2 ? 64f : -64f);  // (over your head, or under it by a north wall; the wall it means is lit, DrawWallPush)
            string key = Keybinds.KeyName(Keybinds.Get(KeyAction.PushWall)).ToUpperInvariant();
            float kw2 = key.Length * 6.6f + 14f;
            var chip = new Rect(spot.X - (kw2 + 96f) / 2f, spot.Y - 12f, kw2 + 96f, 24f);
            DkHud(chip, new Color(0.37f, 0.94f, 1f));
            DkTag(new Rect(chip.x + 4f, chip.y + 4f, kw2, 16f), key, new Color(0.37f, 0.94f, 1f));
            Ui.Label(new Rect(chip.x + kw2 + 10f, chip.y, 90f, chip.height), "PUSH WALL", 11, CrtUi.Ink, TextAnchor.MiddleLeft, true);
        }

        V2 pushFocusAt;  // the last section's face (the camera keeps leaning there while a push is on its way)

        /// The camera in push mode: halfway from you toward the section's next row, allowed 4 tiles past the map's edge (where you push).
        V2 PushFocus(V2 subject, float zoom)
        {
            if (pushSel.HasValue)
            {
                V2 a, b, n, along;
                PushGeometry(pushSel.Value, out a, out b, out n, out along);
                pushFocusAt = (a + b) * 0.5f + n * (Expansion.Step * 1.5f);
            }
            V2 at = pushSel.HasValue || pushLock ? subject + (pushFocusAt - subject) * 0.5f : subject;
            var wd = game.CurrentWorld;
            const float pad = 4f * Expansion.Step;
            V2 lo = wd.MinFor(game.VaultOn) - new V2(pad, pad), hi = wd.Max + new V2(pad, pad);
            float hx = AW / (2f * zoom), hy = AH / (2f * zoom);
            return new V2(hi.X - lo.X <= 2f * hx ? (lo.X + hi.X) / 2f : Mathf.Clamp(at.X, lo.X + hx, hi.X - hx),
                          hi.Y - lo.Y <= 2f * hy ? (lo.Y + hi.Y) / 2f : Mathf.Clamp(at.Y, lo.Y + hy, hi.Y - hy));
        }

        /// A key's short name for the push mode strip (the mouse buttons as LMB, RMB, MMB).
        static string PushKeyName(KeyCode k) => k == KeyCode.Mouse0 ? "LMB" : k == KeyCode.Mouse1 ? "RMB" : k == KeyCode.Mouse2 ? "MMB" : Keybinds.KeyName(k).ToUpperInvariant();

        /// Between waves, not pushing, by an outer wall: the wall C would start on (the prompt shows), else null.
        WallPush? PushPrompt(Player me)
        {
            if (pushOn || me == null || me.Downed || !game.CanPush || ExpandWindowUp || WaveBoxShown(me) || game.GateNear(me.Pos) >= 0 || game.MyLootInReach != null)
                return null;  // (the Wave cleared box takes the clicks: fold it away first)
            return WallToPushNear(me);
        }

        // --- Features building in (step 3) ------------------------------------------------------------------------

        struct PushFlash { public float X0, Y0, X1, Y1, T; public bool Round; }
        struct PushName { public V2 At; public string Text; public float T; }
        World pushSeenWorld;
        HashSet<string> pushSeenPieces = new HashSet<string>(), pushSeenSections = new HashSet<string>();
        readonly List<PushFlash> pushFlashes = new List<PushFlash>();
        readonly List<PushName> pushNames = new List<PushName>();
        const float PushFlashTime = 0.6f, PushNameTime = 1.6f;

        /// Is this point inside a pushed section (not on its walls)?
        bool InPushSection(V2 p)
        {
            foreach (var r in game.Expanded)
                if (Expansion.IsPush(r) && p.X > r.X + 6f && p.X < r.X1 - 6f && p.Y > r.Y + 6f && p.Y < r.Y1 - 6f)
                    return true;
            return false;
        }

        /// WorldRenderer: when the map changes, the pieces new in a pushed section (a wall, a crate, a pillar) flash in, and a new section's
        /// style is named over it (DrawPushNames). Everyone sees it, whoever pushed.
        void DrawPushFlashes()
        {
            if (!game.ExpandingOn || !game.RoomsOn)
                return;
            float now = Time.unscaledTime;
            TrackPushPieces(game.ExpansionWorld, now - PushPreviewAge);
            DrawFlashesNow(now);
        }

        /// The preview (PUSHFLASH=<s>): the flashes and names drawn this long after they started.
        public static float PushPreviewAge;

        /// Compare this map with the last one seen: what's new in a pushed section flashes, a new section's style is named.
        void TrackPushPieces(World w, float now)
        {
            if (w != pushSeenWorld)
            {
                bool first = pushSeenWorld == null;
                var pieces = new HashSet<string>();
                foreach (var s in w.Spans)
                {
                    V2 mid = s.Vertical ? new V2(s.At, (s.From + s.Till) / 2f) : new V2((s.From + s.Till) / 2f, s.At);
                    if (!InPushSection(mid))
                        continue;
                    string key = (s.Vertical ? "v" : "h") + s.At + ":" + s.From + ":" + s.Till;
                    pieces.Add(key);
                    if (!first && !pushSeenPieces.Contains(key))
                    {
                        float t = Expansion.FinT / 2f;
                        pushFlashes.Add(s.Vertical ? new PushFlash { X0 = s.At - t, X1 = s.At + t, Y0 = s.From, Y1 = s.Till, T = now }
                                                   : new PushFlash { X0 = s.From, X1 = s.Till, Y0 = s.At - t, Y1 = s.At + t, T = now });
                    }
                }
                foreach (var c in w.Cover)
                {
                    if (!InPushSection(c.Pos))
                        continue;
                    string key = "c" + c.Pos.X + ":" + c.Pos.Y;
                    pieces.Add(key);
                    float h = c.Round ? c.R : c.HW, hh = c.Round ? c.R : c.HH;
                    if (!first && !pushSeenPieces.Contains(key))
                        pushFlashes.Add(new PushFlash { X0 = c.Pos.X - h, X1 = c.Pos.X + h, Y0 = c.Pos.Y - hh, Y1 = c.Pos.Y + hh, T = now, Round = c.Round });
                }
                var sections = new HashSet<string>();
                for (int i = 0; i < game.Expanded.Count; i++)
                {
                    var r = game.Expanded[i];
                    int away = Expansion.PushAway(game.Expanded, i);
                    if (away < 0)
                        continue;
                    string key = r.Parent + ":" + away + ":" + (away < 2 ? r.Y : r.X) + ":" + (away < 2 ? r.H : r.W);
                    sections.Add(key);
                    var style = Expansion.PushStyleOf(game.Expanded, i);
                    if (!first && !pushSeenSections.Contains(key) && style != PushStyle.None)
                        pushNames.Add(new PushName { At = new V2(r.X + r.W / 2f, r.Y + r.H / 2f), Text = Expansion.PushStyleName(style).ToUpperInvariant(), T = now });
                }
                pushSeenPieces = pieces;
                pushSeenSections = sections;
                pushSeenWorld = w;
            }
        }

        void DrawFlashesNow(float now)
        {
            for (int i = pushFlashes.Count - 1; i >= 0; i--)
            {
                var f = pushFlashes[i];
                float k = (now - f.T) / PushFlashTime;
                if (k >= 1f)
                {
                    pushFlashes.RemoveAt(i);
                    continue;
                }
                float grow = 10f * k, a = 1f - k;
                V2 c = new V2((f.X0 + f.X1) / 2f, (f.Y0 + f.Y1) / 2f);
                if (f.Round)
                {
                    Circle(c, (f.X1 - f.X0) / 2f, new Rgba(1f, 1f, 1f, 0.45f * a * a), 20);
                    Arc(c, (f.X1 - f.X0) / 2f + grow, 2f, new Rgba(0.85f, 0.98f, 1f, a), 24);
                    continue;
                }
                FillRect(f.X0, f.Y0, f.X1 - f.X0, f.Y1 - f.Y0, new Rgba(1f, 1f, 1f, 0.45f * a * a));
                V2 p0 = new V2(f.X0 - grow, f.Y0 - grow), p1 = new V2(f.X1 + grow, f.Y0 - grow), p2 = new V2(f.X1 + grow, f.Y1 + grow), p3 = new V2(f.X0 - grow, f.Y1 + grow);
                var edge = new Rgba(0.85f, 0.98f, 1f, a);
                Line(p0, p1, 2f, edge); Line(p1, p2, 2f, edge); Line(p2, p3, 2f, edge); Line(p3, p0, 2f, edge);
            }
        }

        /// The HUD: a new section's style, named over it as it rises and fades.
        void DrawPushNames()
        {
            float now = Time.unscaledTime;
            for (int i = pushNames.Count - 1; i >= 0; i--)
            {
                var n = pushNames[i];
                float k = (now - n.T) / PushNameTime;
                if (k >= 1f || !game.ExpandingOn)
                {
                    pushNames.RemoveAt(i);
                    continue;
                }
                V2 at = ToUi(n.At) + new V2(0f, -24f * k);
                float a = k < 0.15f ? k / 0.15f : 1f - Mathf.Clamp01((k - 0.6f) / 0.4f);
                var r = new Rect(at.X - 80f, at.Y - 11f, 160f, 22f);
                Ui.Alpha = a;
                DkHud(r, new Color(0.37f, 0.94f, 1f));
                Ui.Label(r, n.Text + "  ROLLED", 11, new Color(0.37f, 0.94f, 1f), TextAnchor.MiddleCenter, true);
                Ui.Alpha = 1f;
            }
        }

        /// The vectors chip's note in push mode: what the next push spends.
        string PushChipNote => pushOn && pushOk ? "  ·  PUSH −" + pushCost : "";
    }
}
