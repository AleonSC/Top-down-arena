using UnityEngine;

namespace Arena
{
    /// Leaving the lobby for the main menu with effects on (the "scan line de-res", option C of Docs/Mockups/lobby-return.html), 1.1 s:
    ///   0.0 s   the Back key is pressed; the lobby dims a little
    ///   0.08 s  a bright scan line sweeps from the top of the screen to the bottom (ease in-out, done at 0.95 s). Above it the
    ///           main menu is already there (drawn only: nothing in it takes a click or lights on hover), with a cyan afterglow
    ///           just behind the line and sparks rising off it; below it the lobby, breaking into blocks just ahead of the line
    ///           (big blocks nearest the line, smaller further down)
    ///   1.1 s   you leave (StartLobbyLeave in LobbyEnter.cs), so the menu takes over from exactly the picture above the line
    /// Each half is the real screen cut to its side of the line (GUI groups, and pieceGlClip for the GL models and edges).
    /// Reduced effects or Visual effects off: the old leave, the entrance played backwards (LobbyEnter.cs).
    public partial class ArenaApp
    {
        const float ScanLeaveLen = 1.1f;
        const float ScanLeaveFrom = 0.08f, ScanLeaveTo = 0.95f;  // the line's sweep, seconds into the leave
        const int ScanSparks = 70;
        bool scanLeave;  // this leave is the scan line (decided when Back is pressed)

        /// Leaving the lobby under the scan line right now.
        bool LobbyScanLeaving => LobbyLeaving && scanLeave;

        /// How long this leave lasts.
        float LeaveLenNow => scanLeave ? ScanLeaveLen : LeaveLen;

        /// The scan line's y (UI canvas), from just above the screen to just below it.
        float ScanLeaveY => -30f + (H + 60f) * EaseInOut(Seg(leaveT, ScanLeaveFrom, ScanLeaveTo));

        /// Instead of the lobby (with its CRT back and glass) while leaving under the scan line.
        void DrawLobbyScanLeave()
        {
            if (Event.current.type != EventType.Repaint)
                return;  // drawn only: neither half takes a click
            float t = Time.unscaledTime, y = ScanLeaveY, cut = Mathf.Clamp(y, 0f, H);

            // Below the line: the lobby as it was, dimming slightly as the line starts.
            if (cut < H)
            {
                var below = new Rect(0f, cut, W, H - cut);
                ScanLeaveClip(below, () =>
                {
                    DrawLobbyCrtBack(t);
                    DrawLobby();
                    DrawLobbyCrtGlass(t);
                });
                Ui.Fill(below, new Color(0f, 0f, 0.04f, 0.25f * Seg(leaveT, ScanLeaveFrom, 0.3f)));
            }

            // Above it: the main menu, drawn only (Ui.NoHit: no hover lights or ticks under the pointer).
            if (cut > 0f)
            {
                bool noHit = Ui.NoHit;
                Ui.NoHit = true;
                ScanLeaveClip(new Rect(0f, 0f, W, cut), DrawMenu);
                Ui.NoHit = noHit;
            }

            if (BeginUiGl())
            {
                UiModelAt(V2.Zero, 1f);
                DrawScanBlocks(y);
                if (leaveT > ScanLeaveFrom * 0.75f && leaveT < ScanLeaveTo + 0.05f)
                    DrawScanLine(y);
                EndUiGl();
            }
        }

        bool scanBgMenu;  // DrawMenuBackground draws the main menu's background (the Holo Floor) though the lobby is still open

        /// GL, the background pass (WorldRenderer.cs): the menu's background above the line, the lobby's (the player's pick) below it.
        void DrawScanLeaveBackdrop(float time)
        {
            float cut = Mathf.Clamp(ScanLeaveY, 0f, H);
            if (cut < H)
            {
                UiClip(new Rect(0f, cut, W, H - cut));
                DrawMenuBackground(time);
            }
            if (cut > 0f)
            {
                UiClip(new Rect(0f, 0f, W, cut));
                scanBgMenu = true;
                DrawMenuBackground(time);
                scanBgMenu = false;
            }
            glClipOn = false;
        }

        /// Draws `draw` cut to r: GUI through two groups (the inner one puts the canvas back where it was), GL models and edges
        /// through pieceGlClip (BeginUiGl, UiClip).
        void ScanLeaveClip(Rect r, System.Action draw)
        {
            GUI.BeginGroup(r);
            GUI.BeginGroup(new Rect(-r.x, -r.y, W, H));
            pieceGlOn = true;
            pieceGlClip = r;
            draw();
            pieceGlOn = false;
            GUI.EndGroup();
            GUI.EndGroup();
        }

        // The blocks ahead of the line: (from, to) px below it, block size, how many of the cells show.
        static readonly float[] ScanBandFrom = { 0f, 32f, 70f }, ScanBandTo = { 32f, 70f, 120f }, ScanBandCell = { 24f, 12f, 6f }, ScanBandShare = { 0.75f, 0.5f, 0.28f };

        /// GL: the lobby breaking into blocks just under the line. Each cell of a fixed grid is dark, blue or (rarely) lit cyan,
        /// picked again 30 times a second, so the band shimmers as it goes.
        void DrawScanBlocks(float y)
        {
            int flick = Mathf.FloorToInt(leaveT * 30f);
            float fade = 1f - Seg(leaveT, ScanLeaveTo - 0.1f, ScanLeaveTo);  // (off the bottom by then anyway)
            for (int b = 0; b < ScanBandCell.Length; b++)
            {
                float cell = ScanBandCell[b], y0 = Mathf.Max(0f, y + ScanBandFrom[b]), y1 = Mathf.Min(H, y + ScanBandTo[b]);
                if (y1 <= y0)
                    continue;
                for (int row = Mathf.FloorToInt(y0 / cell); row * cell < y1; row++)
                {
                    float top = Mathf.Max(row * cell, y0), bot = Mathf.Min((row + 1) * cell, y1);
                    if (bot <= top)
                        continue;
                    for (int col = 0; col * cell < W; col++)
                    {
                        int id = (b * 977 + row) * 4099 + col;
                        float pick = Hash(id * 31 + flick * 7919);
                        if (pick > ScanBandShare[b])
                            continue;
                        float tone = Hash(id * 17 + flick * 104729);
                        var c = tone < 0.55f ? new Rgba(0.02f, 0.03f, 0.07f, 0.9f)    // the lobby gone dark there
                              : tone < 0.9f ? new Rgba(0.22f, 0.34f, 0.62f, 0.55f)    // a blue block
                              : new Rgba(0.55f, 0.82f, 1f, 0.4f);                    // a lit one
                        FillRect(col * cell, top, cell, bot - top, c.WithAlpha(c.A * fade));
                    }
                }
            }
        }

        /// GL: the line itself, its afterglow over the menu behind it, a thin glow ahead of it, and sparks rising off it.
        void DrawScanLine(float y)
        {
            var glow = new Rgba(0.45f, 0.75f, 1f, 1f);
            GradQuad(new V2(0f, y - 90f), new V2(W, y - 90f), new V2(W, y), new V2(0f, y), glow.WithAlpha(0f), glow.WithAlpha(0.32f));
            GradQuad(new V2(0f, y), new V2(W, y), new V2(W, y + 14f), new V2(0f, y + 14f), glow.WithAlpha(0.45f), glow.WithAlpha(0f));
            FillRect(0f, y - 4f, W, 8f, glow.WithAlpha(0.25f));
            FillRect(0f, y - 1.5f, W, 3f, new Rgba(0.91f, 0.96f, 1f, 1f));
            for (int i = 0; i < ScanSparks; i++)
            {
                float age = (leaveT * 3f + Hash(i * 13 + 5)) % 1f;
                float sx = Hash(i * 7 + 1) * W + (Hash(i * 11 + 3) - 0.5f) * 60f * age;
                float sy = y - age * (30f + 80f * Hash(i * 19 + 2));
                FillRect(sx, sy, 2f, 2f + 4f * (1f - age), new Rgba(0.62f, 0.84f, 1f, 0.9f * (1f - age)));
            }
        }
    }
}
