using System;
using UnityEngine;

namespace Arena
{
    /// The main menu's Play transition, "bay doors" version 2 (Servo) of Docs/Mockups/menu-bay-doors.html. `menuPlayK` runs 0 (the menu's
    /// buttons) to 1 (the saved-runs picker) over MenuPlayLen, and everything below is a function of it, so Back is the same thing backwards:
    ///   0.04 to 0.50  the header screen's letters flip like a split-flap board, left to right, to the picker's title
    ///   0.10 to 0.42  the Play key splits down the middle and its halves slide into the box's walls
    ///   0.12 to 0.48  the Quit key's legend swaps to Back (same keycap: the cap dips, the icon turns, the lamp lights)
    ///   0.08 to 0.50  Join game and Settings slide down their rails and out through the floor, one after another
    ///   0.38 to 0.64  the runs screen, behind the doors, opens to its full height
    ///   0.60 to 0.84  New run rises into the gap, overshoots a little and settles; the box gives a small thunk
    /// Sounds (Sfx.Bay*) are cued off the same number (ServoCues). Reduced effects or effects off skip all of it (the box just switches).
    public partial class ArenaApp
    {
        const float ServoDoorA = 0.10f, ServoDoorB = 0.42f, ServoDropS = 0.08f, ServoDropGap = 0.06f, ServoDropLen = 0.24f;
        const string ServoMenuTitle = "MAIN MENU";
        const string ServoAbc = " ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        float servoPrevK;     // where menuPlayK was at the last cue check
        int servoFlapTot, servoFlapSeen;   // flap steps taken so far across the header's letters, and how many have clacked

        static float ServoSeg(float p, float a, float b) => Mathf.Clamp01((p - a) / (b - a));

        struct ServoCue
        {
            public float t, d, vol;
            public Sfx fwd, back;
            public ServoCue(float t, float d, Sfx fwd, Sfx back, float vol) { this.t = t; this.d = d; this.fwd = fwd; this.back = back; this.vol = vol; }
        }

        // t: where it plays going forward; d: how long it lasts (going backwards it plays at its end); back: what plays going backwards.
        static readonly ServoCue[] ServoCues =
        {
            new ServoCue(0.07f, 0f, Sfx.BayBeep, Sfx.BayBeep, 0.6f),
            new ServoCue(ServoDoorA, 0.32f, Sfx.BayServo, Sfx.BayServoBack, 0.6f),     // the doors' rising whine
            new ServoCue(ServoDropS, 0.24f, Sfx.BayWhirr, Sfx.BayWhirr, 0.6f),         // each key leaving
            new ServoCue(ServoDropS + ServoDropGap, 0.24f, Sfx.BayWhirr, Sfx.BayWhirr, 0.6f),
            new ServoCue(ServoDropS + ServoDropLen * 0.88f, 0f, Sfx.BayTick, Sfx.BayTick, 0.6f),    // and landing
            new ServoCue(ServoDropS + ServoDropGap + ServoDropLen * 0.88f, 0f, Sfx.BayTick, Sfx.BayTick, 0.6f),
            new ServoCue(0.38f, 0.26f, Sfx.BayLift, Sfx.BayLift, 0.45f),               // the screen's frame coming out
            new ServoCue(ServoDoorB, 0f, Sfx.BayClamp, Sfx.BayClamp, 0.6f),
            new ServoCue(0.44f, 0f, Sfx.BayZap, Sfx.BayZap, 0.5f),
            new ServoCue(0.60f, 0.24f, Sfx.BayLift, Sfx.BayLift, 0.6f),                // New run lifting
            new ServoCue(0.84f, 0f, Sfx.BaySeat, Sfx.BaySeat, 0.7f),
        };

        /// Plays each cue menuPlayK passed between a and b, and a flap click for each header letter that stepped.
        void PlayServoCues(float a, float b)
        {
            if (sounds == null)  // the preview tool has no sound bank
                return;
            float now = Time.unscaledTime;
            foreach (var c in ServoCues)
            {
                if (b > a && a < c.t && c.t <= b)
                    sounds.Play(c.fwd, c.vol, now);
                else if (b < a && b <= c.t + c.d && c.t + c.d < a && c.back != Sfx.None)
                    sounds.Play(c.back, c.vol, now);
            }
            int steps = Math.Min(3, Math.Abs(servoFlapTot - servoFlapSeen));
            for (int i = 0; i < steps; i++)
                sounds.Play(Sfx.BayClack, 0.5f, now);
            servoFlapSeen = servoFlapTot;
        }

        /// Draws a piece clipped to a screen-space rect (the caller's drawing stays in screen coordinates).
        static void ServoClip(Rect clip, Action draw)
        {
            if (clip.width < 0.5f || clip.height < 0.5f)
                return;
            GUI.BeginGroup(clip);
            GUI.BeginGroup(new Rect(-clip.x, -clip.y, W, H));
            draw();
            GUI.EndGroup();
            GUI.EndGroup();
        }

        /// The box in the middle of the Play move (0 < p < 1). The shared header screen and Quit/Back key are drawn here; DrawPlayPicker
        /// and DrawMenuButtons are not asked for theirs (servoPicker).
        void DrawMenuServo(float t, float p)
        {
            var c = MenuCard;
            float dk = p - 0.86f;
            float shake = dk > 0f ? 1.5f * Mathf.Exp(-dk * 26f) * Mathf.Sin(dk * 95f) : 0f;   // the box's thunk when New run seats
            GUI.BeginGroup(new Rect(0f, shake, W, H));
            float floor = c.yMax - 12f;

            // The runs screen, behind the doors: it shows through the gap and then opens to its full height.
            float dx = 335f * EaseInOut(ServoSeg(p, ServoDoorA, ServoDoorB));
            var host = MenuHostRect;
            var scr = RunsScreenRect(c);
            float vv = EaseInOut(ServoSeg(p, 0.38f, 0.64f));
            float side = Mathf.Max(0f, c.center.x - dx - scr.x);
            float top = Mathf.Lerp(host.y, scr.y, vv), bot = Mathf.Lerp(host.yMax, scr.yMax, vv);
            var vis = new Rect(scr.x + side, top, scr.width - 2f * side, bot - top);
            if (p > ServoDoorA * 0.8f)
            {
                float content = ServoSeg(p, 0.50f, 0.66f);
                ServoClip(vis, () =>
                {
                    servoPicker = true;
                    DrawPlayPicker(t, false);
                    servoPicker = false;
                    if (content < 1f)   // the rows come up out of the dark
                        Ui.Fill(scr, new Color(CrtUi.Bg.r, CrtUi.Bg.g, CrtUi.Bg.b, 1f - content));
                });
            }

            // Join game and Settings drop through the floor, then New run rises into the gap.
            DrawServoKey(MenuJoinRect, "Join game", "ui_arrow", 0, floor, p);
            DrawServoKey(MenuSettingsRect, "Settings", "ui_gear", 1, floor, p);
            float u = ServoSeg(p, 0.60f, 0.84f);
            if (u > 0f)
            {
                float ny = 50f * (1f - EaseBack(u));
                var nr = NewRunRect(c);
                var well = KeyUi.Well(nr);
                ServoClip(new Rect(well.x, well.y, well.width, Mathf.Max(0f, Mathf.Min(well.height, floor - well.y - ny))), () =>
                    KeyUi.Key(new Rect(nr.x, nr.y + ny, nr.width, nr.height), "New run", PixelArt.Get("ui_play"), 20));
            }

            // The Play key, split: each half slides into its wall.
            if (dx < 336f)
            {
                float press = Mathf.Sin(Mathf.PI * ServoSeg(p, 0f, ServoDoorA * 0.7f)) * 4f;
                var well = KeyUi.Well(host);
                float mid = host.center.x;
                float lx0 = Mathf.Max(c.x + 12f, well.x - dx), lx1 = mid - dx;
                float rx0 = mid + dx, rx1 = Mathf.Min(c.xMax - 12f, well.xMax + dx);
                var key = PixelArt.Get("ui_play");
                ServoClip(new Rect(lx0, well.y, lx1 - lx0, well.height + 4f), () => KeyUi.Key(new Rect(host.x - dx, host.y + press, host.width, host.height), "PLAY", key, 34));
                ServoClip(new Rect(rx0, well.y, rx1 - rx0, well.height + 4f), () => KeyUi.Key(new Rect(host.x + dx, host.y + press, host.width, host.height), "PLAY", key, 34));
            }
            GUI.EndGroup();

            DrawServoHeader(p);
        }

        /// One of the three bottom keys on its way down the rails (it drops straight, clipped at the box's floor).
        void DrawServoKey(Rect r, string text, string icon, int i, float floor, float p)
        {
            float tt = ServoSeg(p, ServoDropS + i * ServoDropGap, ServoDropS + i * ServoDropGap + ServoDropLen);
            float y = 100f * EaseInOut(tt);
            var well = KeyUi.Well(r);
            float h = Mathf.Min(well.height, floor - well.y - y);
            if (h <= 0f)
                return;
            ServoClip(new Rect(well.x, well.y, well.width, h), () => KeyUi.Key(new Rect(r.x, r.y + y, r.width, r.height), text, PixelArt.Get(icon), 20));
        }

        /// The header screen (its letters flip like a split-flap board) and the shared Quit/Back keycap (only its legend changes).
        void DrawServoHeader(float p)
        {
            var hs = MenuHeaderRect;
            string menuT = ServoMenuTitle, pickT = savedRuns.Count > 0 ? "YOUR RUNS" : "START A RUN";
            bool opening = menuPlayOpen;
            string from = opening ? menuT : pickT, to = opening ? pickT : menuT;
            float q = opening ? p : 1f - p;
            int n = Mathf.Max(from.Length, to.Length);
            from = from.PadRight(n);
            to = to.PadRight(n);
            const float A = 0.04f, B = 0.50f, span = B - A;
            const int Size = 24;
            CrtUi.Begin(hs);
            int tot = 0;
            float pad = Ui.TextWidth("", Size, true);
            float x0 = hs.x + 16f;
            for (int i = 0; i < n; i++)
            {
                int ai = ServoAbc.IndexOf(from[i]), bi = ServoAbc.IndexOf(to[i]);
                if (ai < 0) ai = 0;
                if (bi < 0) bi = 0;
                int dist = (bi - ai + ServoAbc.Length) % ServoAbc.Length;
                float d = n > 1 ? span * 0.4f * i / (n - 1) : 0f;
                float tk = ServoSeg(q, A + d, A + span * 0.6f + d);
                float x = tk * dist;
                int k = Mathf.FloorToInt(x);
                float f = x - k;
                char ch = tk >= 1f ? to[i] : ServoAbc[(ai + k) % ServoAbc.Length];
                tot += tk >= 1f ? dist : k;
                // cells sit where the final text's letters will, so nothing jumps when the move ends
                float cx = x0 + Ui.TextWidth(to.Substring(0, i), Size, true) - pad;
                float cw = Mathf.Max(14f, Ui.TextWidth(to[i] == ' ' ? "A" : to[i].ToString(), Size, true) - pad);
                var cell = new Rect(cx, hs.y + 8f, cw, 28f);
                var oldM = GUI.matrix;
                bool moving = tk > 0f && tk < 1f;
                if (moving)
                {
                    GUIUtility.ScaleAroundPivot(new Vector2(1f, 1f - 0.35f * Mathf.Sin(Mathf.PI * f)), cell.center);
                    Ui.Fill(cell, new Color(0f, 0.03f, 0.015f, 0.9f));
                    Ui.Fill(new Rect(cell.x, cell.center.y - 0.5f, cell.width, 1f), new Color(0f, 0f, 0f, 0.9f));   // the flap's hinge line
                }
                if (ch != ' ')
                    CrtUi.Text(new Rect(cx, hs.y + 4f, cw + 12f, 36f), ch.ToString(), Size, CrtUi.Ink, TextAnchor.MiddleLeft, true, fxReduced);
                GUI.matrix = oldM;
            }
            if (Mathf.FloorToInt(Time.unscaledTime * 2f) % 2 == 0)   // the same blinking cursor the plain header has
                CrtUi.Text(new Rect(x0 + Ui.TextWidth(to.TrimEnd(), Size, true) - pad, hs.y + 4f, 20f, 36f), "_", Size, CrtUi.Ink, TextAnchor.MiddleLeft, true, fxReduced);
            CrtUi.End(hs, Time.unscaledTime, fxReduced);
            servoFlapTot = tot;

            // One keycap: Quit's legend swaps to Back (and back again), the cap dips as it lands, the lamp lights with Back.
            float lk = ServoSeg(p, 0.12f, 0.48f);
            float dip = Mathf.Sin(Mathf.PI * ServoSeg(lk, 0.7f, 1f)) * 3f + Mathf.Sin(Mathf.PI * ServoSeg(lk, 0.35f, 0.65f)) * 3f;
            var qr = MenuQuitKey;
            KeyUi.Key(qr, "Quit", PixelArt.Get("ui_power"), 15, lk > 0.5f, "Back", PixelArt.Get("ui_back"), lk, dip);
        }
    }
}
