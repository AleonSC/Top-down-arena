using UnityEngine;

namespace Arena
{
    /// The pause menu's "signal glitch" opening (option E of Docs/Mockups/pause-menu.html): the frozen screen tears for
    /// a moment (slices of the last frame jump sideways with red and cyan fringes), scanlines roll in, the panel
    /// flickers on and PAUSED types itself out. Closing runs it backwards, faster. With Reduced effects on (or
    /// Visual effects off) it's a plain quick fade.
    public partial class ArenaApp
    {
        const float GlitchOpenTime = 0.65f, GlitchCloseTime = 0.3f, PlainPauseFade = 0.15f;
        const int GlitchBands = 9;

        float pauseAnim;          // 0 closed .. 1 fully open
        bool pauseGrabWanted;     // grab the next rendered frame (the world as the menu opens), for the slices
        bool pauseGrabbed;
        Texture2D pauseGrab;
        string pausedTitle = "PAUSED";  // the panel's title (typed out while it opens)

        bool PlainPauseFx => fxReduced || !fxOn;

        /// Is the pause menu (or a screen opened from it) wanted?
        bool PauseMenuWanted => session.Screen == AppScreen.Game && (pauseMenuOpen || (game.Paused && !tabPaused)) && !devMenuOpen && !labSetupOpen;

        /// Each frame: run the opening or the closing.
        void StepPauseAnim(float dt)
        {
            bool want = PauseMenuWanted;
            if (want && pauseAnim <= 0f)
            {
                pauseGrabWanted = !PlainPauseFx;
                pauseGrabbed = false;
            }
            float open = PlainPauseFx ? PlainPauseFade : GlitchOpenTime, close = PlainPauseFx ? PlainPauseFade : GlitchCloseTime;
            pauseAnim = want ? Mathf.Min(1f, pauseAnim + dt / open) : Mathf.Max(0f, pauseAnim - dt / close);
            if (session.Screen != AppScreen.Game)
                pauseAnim = 0f;
        }

        /// From OnPostRender, once the world is drawn: keep a copy of the frame for the glitch's slices.
        void GrabPauseFrame()
        {
            if (!pauseGrabWanted)
                return;
            pauseGrabWanted = false;
            int w = UnityEngine.Screen.width, h = UnityEngine.Screen.height;
            if (pauseGrab == null || pauseGrab.width != w || pauseGrab.height != h)
                pauseGrab = new Texture2D(w, h, TextureFormat.RGB24, false) { hideFlags = HideFlags.HideAndDontSave };
            pauseGrab.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
            pauseGrab.Apply(false);
            pauseGrabbed = true;
        }

        static float OutCubic(float x) => 1f - (1f - x) * (1f - x) * (1f - x);  // (Seg is in LaunchIntro.cs)

        /// The pause menu with its opening (or closing) applied. While it closes nothing in it can be clicked.
        void DrawPauseMenu()
        {
            float q = pauseAnim;
            bool closing = !PauseMenuWanted;
            var oldEnabled = GUI.enabled;
            if (closing)
                GUI.enabled = false;
            float oldAlpha = Ui.Alpha;
            if (PlainPauseFx)
            {
                Ui.Fill(new Rect(0, 0, W, H), new Color(0.01f, 0.01f, 0.03f, 0.7f * q));
                var oc = GUI.color;
                GUI.color = new Color(oc.r, oc.g, oc.b, oc.a * q);
                Ui.Alpha = oldAlpha * q;
                pausedTitle = "PAUSED";
                DrawSettingsPanel(true);
                Ui.Alpha = oldAlpha;
                GUI.color = oc;
                GUI.enabled = oldEnabled;
                return;
            }
            // The tear: slices of the frozen frame shifted sideways, fringed red and cyan.
            float g = Seg(q, 0f, 0.4f);
            if (g > 0f && g < 1f && pauseGrabbed && Event.current.type == EventType.Repaint)
                DrawGlitchSlices(q, g);
            // The screen dims and scanlines roll in.
            Ui.Fill(new Rect(0, 0, W, H), new Color(0.01f, 0.01f, 0.03f, 0.7f * OutCubic(Seg(q, 0.15f, 0.6f))));
            float scan = 0.05f * OutCubic(Seg(q, 0.1f, 0.5f));
            if (scan > 0.001f)
                for (float y = 0f; y < H; y += 3f)
                    Ui.Fill(new Rect(0f, y, W, 1f), new Color(1f, 1f, 1f, scan));
            // The panel flickers on, shaking a little, and PAUSED types itself out.
            float pk = Seg(q, 0.28f, 0.55f);
            float flick = pk >= 1f ? 1f : (Mathf.Sin(q * 160f) > 0f ? pk : pk * 0.3f);
            float shake = (pk < 1f ? Mathf.Sin(q * 220f) * 4f * (1f - pk) : 0f) + (g > 0f && g < 1f ? Mathf.Sin(q * 300f) * 3f * (1f - g) : 0f);
            int typed = Mathf.RoundToInt(Seg(q, 0.45f, 0.8f) * 6f);
            pausedTitle = "PAUSED".Substring(0, typed) + (typed < 6 ? "▌" : "");
            if (flick > 0.001f)
            {
                var oldMatrix = GUI.matrix;
                GUI.matrix = Matrix4x4.TRS(new Vector3(offX + shake * scale, offY, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));  // the canvas, nudged sideways
                var oldColor = GUI.color;
                GUI.color = new Color(oldColor.r, oldColor.g, oldColor.b, oldColor.a * flick);  // the panel's box follows GUI.color,
                Ui.Alpha = oldAlpha * flick;                                                   // its fills and text Ui.Alpha (like the HUD's fade)
                DrawSettingsPanel(true);
                Ui.Alpha = oldAlpha;
                GUI.color = oldColor;
                GUI.matrix = oldMatrix;
            }
            pausedTitle = "PAUSED";
            GUI.enabled = oldEnabled;
        }

        /// Bands of the grabbed frame, each moved sideways (less as the tear settles), with a red or cyan line.
        void DrawGlitchSlices(float q, float g)
        {
            int seed = Mathf.FloorToInt(q * 40f);
            float sw = UnityEngine.Screen.width, sh = UnityEngine.Screen.height;
            for (int i = 0; i < GlitchBands; i++)
            {
                float rnd = Mathf.Sin((seed + 1) * (i + 3) * 12.9898f) * 43758.5453f, fr = rnd - (float)System.Math.Floor(rnd);
                float y = (float)System.Math.Floor(fr * H), h = 8f + (float)System.Math.Floor(fr * 46f), dx = (fr - 0.5f) * 90f * (1f - g);
                // This band of the UI canvas, in the grabbed frame's texture coordinates (its origin is bottom-left).
                float sy0 = offY + y * scale, sy1 = offY + (y + h) * scale;
                var uv = new Rect(offX / sw, 1f - sy1 / sh, W * scale / sw, (sy1 - sy0) / sh);
                GUI.DrawTextureWithTexCoords(new Rect(dx, y, W, h), pauseGrab, uv);
                Ui.Fill(new Rect(0f, y, W, 2f), i % 2 == 1 ? new Color(1f, 0.16f, 0.31f, 0.25f) : new Color(0.16f, 0.86f, 1f, 0.25f));
            }
        }
    }
}
