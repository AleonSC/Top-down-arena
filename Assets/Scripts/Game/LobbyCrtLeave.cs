using UnityEngine;

namespace Arena
{
    /// Leaving the lobby for the main menu with effects on (the "CRT power-cycle", option A of Docs/Mockups/lobby-return.html), 1.05 s:
    ///   0.0 s   the Back key is pressed: the whole screen (background, panels, models) squashes to a white-hot line about the
    ///           middle, like a tube switching off, going white as it closes
    ///   0.3 s   just the line: it shrinks to a third of the width and blooms
    ///   0.46 s  the main menu snaps open from it with a slight overshoot, white at first, the flash fading by 0.78 s
    ///   1.05 s  the scanlines have faded; you leave (StartLobbyLeave in LobbyEnter.cs), and the menu takes over unchanged
    /// The squash is one transform for everything: GUI.matrix for IMGUI, crtSquash in CanvasPx for GL (WorldRenderer.cs).
    /// The menu is drawn only (Repaint, Ui.NoHit). Reduced effects or Visual effects off: the old leave, the entrance played
    /// backwards (LobbyEnter.cs).
    public partial class ArenaApp
    {
        const float CrtLeaveLen = 1.05f;
        const float CrtOffEnd = 0.3f, CrtLineEnd = 0.46f, CrtOnEnd = 0.82f;
        bool crtLeave;     // this leave is the power-cycle (decided when Back is pressed)
        bool crtSquashOn;  // GL: squash the canvas about its middle (CanvasPx)
        float crtSqX = 1f, crtSqY = 1f;
        bool crtBgMenu;    // DrawMenuBackground draws the main menu's background (the Holo Floor) though the lobby is still open

        /// Leaving the lobby through the power-cycle right now.
        bool LobbyCrtLeaving => LobbyLeaving && crtLeave;

        /// How long this leave lasts.
        float LeaveLenNow => crtLeave ? CrtLeaveLen : LeaveLen;

        bool CrtLeaveMenuSide => leaveT >= CrtLineEnd;

        static float EaseBackOut(float x) { const float c = 1.6f; return 1f + (c + 1f) * Mathf.Pow(x - 1f, 3f) + c * Mathf.Pow(x - 1f, 2f); }

        /// The screen's squash right now (x, y), and whether anything shows (not during the line).
        bool CrtLeaveSquash(out float sx, out float sy)
        {
            if (leaveT < CrtOffEnd)
            {
                float k = Seg(leaveT, 0f, CrtOffEnd);
                sx = 1f + 0.05f * k;
                sy = Mathf.Max(0.006f, 1f - k * k * k);
                return true;
            }
            if (leaveT < CrtLineEnd)
            {
                sx = sy = 0f;
                return false;
            }
            float on = Seg(leaveT, CrtLineEnd, CrtOnEnd);
            sx = 1.03f - 0.03f * EaseOut(on);
            sy = Mathf.Max(0.006f, EaseBackOut(on));
            return true;
        }

        /// GL, the background pass (WorldRenderer.cs): the lobby's background, then the menu's, squashed with the screen.
        void DrawCrtLeaveBackdrop(float time)
        {
            if (!CrtLeaveSquash(out float sx, out float sy))
                return;
            crtSquashOn = true;
            crtSqX = sx;
            crtSqY = sy;
            crtBgMenu = CrtLeaveMenuSide;
            DrawMenuBackground(time);
            crtBgMenu = false;
            crtSquashOn = false;
        }

        /// Instead of the lobby (with its CRT back and glass) while leaving through the power-cycle.
        void DrawLobbyCrtLeave()
        {
            if (Event.current.type != EventType.Repaint)
                return;  // drawn only: nothing takes a click
            float t = Time.unscaledTime;
            var full = new Rect(0f, 0f, W, H);
            bool shown = CrtLeaveSquash(out float sx, out float sy);
            // Black round the band (the background pass only drew inside it).
            float bandH = shown ? H * Mathf.Min(1f, sy) : 0f, bandY = (H - bandH) / 2f;
            Ui.Fill(new Rect(-W, -H, 3f * W, H + bandY), Color.black);
            Ui.Fill(new Rect(-W, bandY + bandH, 3f * W, H + bandY), Color.black);

            if (!shown)
            {
                // The line: shrinking to a third of the width and blooming.
                float k = Seg(leaveT, CrtOffEnd, CrtLineEnd), lw = W * 1.05f * (1f - 0.7f * EaseOut(k));
                float glow = 140f + 80f * k;
                Ui.RadialGlow(new Rect(W / 2f - glow * 1.6f, H / 2f - glow, glow * 3.2f, glow * 2f), new Color(0.63f, 0.78f, 1f, 0.35f * k + 0.25f));
                Ui.Fill(new Rect(W / 2f - lw / 2f, H / 2f - 4f, lw, 8f), new Color(0.6f, 0.77f, 1f, 0.35f));
                Ui.Fill(new Rect(W / 2f - lw / 2f, H / 2f - 1.5f, lw, 3f), Color.white);
                return;
            }

            bool menu = CrtLeaveMenuSide;
            var old = GUI.matrix;
            float sc = old.m00, tx = old.m03, ty = old.m13;
            GUI.matrix = Matrix4x4.TRS(new Vector3(tx + sc * W / 2f * (1f - sx), ty + sc * H / 2f * (1f - sy), 0f), Quaternion.identity, new Vector3(sc * sx, sc * sy, 1f));
            crtSquashOn = true;
            crtSqX = sx;
            crtSqY = sy;
            if (menu)
            {
                bool noHit = Ui.NoHit;
                Ui.NoHit = true;  // no hover lights or ticks under the pointer
                DrawMenu();
                Ui.NoHit = noHit;
            }
            else
            {
                DrawLobbyCrtBack(t);
                DrawLobby();
                DrawLobbyCrtGlass(t);
            }
            // The tube's white: building as it closes, fading as it opens.
            float flash = menu ? 0.85f * (1f - Seg(leaveT, CrtLineEnd, 0.78f)) : 0.95f * Mathf.Pow(Seg(leaveT, 0f, CrtOffEnd), 3f);
            if (flash > 0f)
                Ui.Fill(full, new Color(0.81f, 0.88f, 1f, flash));
            crtSquashOn = false;
            GUI.matrix = old;
            if (menu)
                CrtScanlines(full, 0.35f * (1f - Seg(leaveT, 0.7f, CrtLeaveLen)));
        }
    }
}
