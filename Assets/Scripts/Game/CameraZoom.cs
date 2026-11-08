using UnityEngine;

namespace Arena
{
    /// Scroll-wheel zoom: scroll up to zoom in on yourself (up to ZoomMax), down to zoom back out to the whole arena.
    /// Purely local drawing, like the camera lean: the arena, the game and the network are unchanged.
    ///   zoom     each notch multiplies the wanted zoom by ZoomStep; the drawn zoom eases toward it (dt * ZoomEase).
    ///   focus    the view centres on you, eased (dt * FocusEase), and is held inside the arena so the zoomed view never
    ///            shows past its edge; a wrap round the edge or a teleport snaps it instead of sweeping across.
    ///   screen   Px (WorldRenderer.cs) maps an arena point p to (p - focus) * zoom + centre before the ending's zoom and the
    ///            lean; ToUi and MouseArena follow it, and UiWorldScale sizes HUD rings around world things. The mood tint,
    ///            the ending's vignette and the game-over dim are screen overlays and skip it (camZoomOn off).
    ///   reset    zoom 1 means the focus sits on the centre and everything draws exactly as without it. The view eases back
    ///            out during a run's ending, and snaps back between matches.
    /// Setting: Settings → Gameplay → Scroll-wheel zoom (PlayerPrefs "cam_zoom", on by default). Off keeps zoom 1.
    public partial class ArenaApp
    {
        const float ZoomMax = 2f, ZoomStep = 1.12f, ZoomEase = 10f, FocusEase = 8f;

        bool scrollZoom = true;
        float camZoom = 1f;      // the drawn zoom (1: the whole arena)
        float zoomWant = 1f;     // where the scroll wheel put it
        V2 camFocusRaw;          // eased toward you, not yet held inside the arena
        V2 camFocus = new V2(AW / 2f, AH / 2f);  // the arena point at the centre of the screen
        bool camZoomOn;          // while the world is drawn and zoomed (set in BeginLeanFrame, cleared in EndLeanFrame)

        /// Any zoom at all this frame (false keeps every transform exactly as before).
        bool CamZoomed => camZoom > 1.0001f;

        /// Arena units -> HUD canvas units for sizes around world things (rings, label offsets), with the zoom.
        float UiWorldScale => CamZoomed ? WorldToUi * camZoom : WorldToUi;

        void LoadCamZoom() => scrollZoom = PlayerPrefs.GetInt("cam_zoom", 1) == 1;

        void SetCamZoom(bool on)
        {
            scrollZoom = on;
            if (!on)
                zoomWant = 1f;
            PlayerPrefs.SetInt("cam_zoom", on ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// Once a frame (from UpdateCameraLean): read the wheel, ease the zoom and the focus.
        void UpdateCameraZoom(float dt)
        {
            var me = game.Me;
            if (!(session.Screen == AppScreen.Game && game.Running) || me == null)
            {
                camZoom = zoomWant = 1f;
                camFocus = camFocusRaw = new V2(AW / 2f, AH / 2f);
                return;
            }
            if (game.Paused)
                return;  // (the picture holds still behind a pause menu, and the wheel is the menu's)
            if (scrollZoom && !EndingOn && !devMenuOpen && !devFrozen)
            {
                float wheel = Input.mouseScrollDelta.y;
                if (wheel > 0.01f || wheel < -0.01f)
                    zoomWant = Mathf.Clamp(zoomWant * Mathf.Pow(ZoomStep, Mathf.Clamp(wheel, -3f, 3f)), 1f, ZoomMax);
            }
            float want = scrollZoom && !EndingOn ? zoomWant : 1f;
            camZoom += (want - camZoom) * Mathf.Min(1f, dt * ZoomEase);
            if (Mathf.Abs(camZoom - want) < 0.0005f)
                camZoom = want;
            if (camZoom < 1f)
                camZoom = 1f;

            V2 to = me.Disp - camFocusRaw;
            if (!CamZoomed || to.Length > 200f)
                camFocusRaw = me.Disp;  // (zoomed out it already follows you; a wrap or a teleport is a cut, not a pan)
            else
                camFocusRaw += to * Mathf.Min(1f, dt * FocusEase);
            if (!CamZoomed)
            {
                camFocus = new V2(AW / 2f, AH / 2f);
                return;
            }
            float hx = AW / (2f * camZoom), hy = AH / (2f * camZoom);
            camFocus = new V2(Mathf.Clamp(camFocusRaw.X, hx, AW - hx), Mathf.Clamp(camFocusRaw.Y, hy, AH - hy));
        }

        /// An arena point through the zoom (arena units on the unzoomed screen). Only while CamZoomed.
        V2 ZoomPoint(V2 p) => (p - camFocus) * camZoom + new V2(AW / 2f, AH / 2f);

        /// A point on the unzoomed screen (arena units) back to the arena. Only while CamZoomed.
        V2 UnzoomPoint(V2 s) => (s - new V2(AW / 2f, AH / 2f)) * (1f / camZoom) + camFocus;
    }
}
