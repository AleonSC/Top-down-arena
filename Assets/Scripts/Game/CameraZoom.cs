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
    ///   rooms    with the Rooms arena's side rooms up (Rooms.cs) the camera follows you even at zoom 1: it holds inside the
    ///            area you're in (the hub, a room or a hallway; an area smaller than the view is centred) and inside the
    ///            world, and slides (dt * RoomEase) when you step through a door. At zoom 1 in the hub it shows the whole
    ///            hub, as without the rooms.
    /// Setting: Settings → Gameplay → Scroll-wheel zoom (PlayerPrefs "cam_zoom", on by default). Off keeps zoom 1.
    public partial class ArenaApp
    {
        const float ZoomMax = 2f, ZoomStep = 1.12f, ZoomEase = 10f, FocusEase = 8f, RoomEase = 6f;

        bool scrollZoom = true;
        float camZoom = 1f;      // the drawn zoom (1: the whole arena)
        float zoomWant = 1f;     // where the scroll wheel put it
        V2 camFocusRaw;          // eased toward you, not yet held inside the arena
        V2 camFocus = new V2(AW / 2f, AH / 2f);  // the arena point at the centre of the screen
        bool camZoomOn;          // while the world is drawn and zoomed (set in BeginLeanFrame, cleared in EndLeanFrame)
        bool camFollow;          // the Rooms world is up: the camera follows you at any zoom (Rooms.cs)
        float zoomHintT;         // the "scroll to zoom out" prompt, while it shows (the open Rooms map; gone once you zoom out)
        const float ZoomHintTime = 14f;

        /// Any zoom at all this frame, or the rooms' following camera (false keeps every transform exactly as before).
        bool CamZoomed => camZoom > 1.0001f || camFollow;

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
                camFollow = false;
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
            if (game.RoomsOn)
            {
                bool fresh = !camFollow;
                camFollow = true;
                if (fresh && !Rooms.RoomByRoom && scrollZoom)
                {
                    zoomWant = camZoom = Rooms.StartZoom;  // room rounds start zoomed in, with a prompt to zoom out (DrawZoomHint)
                    zoomHintT = ZoomHintTime;
                }
                if (zoomHintT > 0f)
                    zoomHintT = zoomWant <= 1.02f ? 0f : zoomHintT - dt;
                V2 want2 = RoomsFocus(me.Disp, camZoom);
                if (fresh || (want2 - camFocus).Length > 1600f)
                    camFocus = want2;  // (the rooms just came up, or a teleport: a cut)
                else
                    camFocus += (want2 - camFocus) * Mathf.Min(1f, dt * RoomEase);
                return;
            }
            if (camFollow)
            {
                camFollow = false;  // the rooms went (a boss wave): back to the hub view at once
                camFocusRaw = me.Disp;
            }
            if (!CamZoomed)
            {
                camFocus = new V2(AW / 2f, AH / 2f);
                return;
            }
            float hx = AW / (2f * camZoom), hy = AH / (2f * camZoom);
            camFocus = new V2(Mathf.Clamp(camFocusRaw.X, hx, AW - hx), Mathf.Clamp(camFocusRaw.Y, hy, AH - hy));
        }

        /// Where the rooms' camera looks: at you, held inside the area you're in (centred on it when it's smaller than
        /// the view), then inside the world.
        V2 RoomsFocus(V2 at, float zoom) => game.CurrentWorld.ViewCentre(at, AW / (2f * zoom), AH / (2f * zoom), game.VaultOn);

        /// DrawHud: the open Rooms map starts zoomed in; this asks you to zoom out, low in the middle, until you do (or a while).
        void DrawZoomHint()
        {
            if (zoomHintT <= 0f || !game.RoomsOn || !game.Running)
                return;
            float a = Mathf.Clamp01(zoomHintT / 1.5f) * Mathf.Clamp01((ZoomHintTime - zoomHintT) / 0.6f);
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f);
            var r = new Rect(W / 2f - 150f, H - 200f, 300f, 34f);
            var old = GUI.color;
            GUI.color = new Color(old.r, old.g, old.b, old.a * a);
            Ui.Pill(r, new Color(0f, 0f, 0f, 0.6f));
            Ui.Label(r, "SCROLL ▼  TO ZOOM OUT", 15, new Color(0.55f, 0.94f, 1f, pulse), TextAnchor.MiddleCenter, true);
            GUI.color = old;
        }

        /// An arena point through the zoom (arena units on the unzoomed screen). Only while CamZoomed.
        V2 ZoomPoint(V2 p) => (p - camFocus) * camZoom + new V2(AW / 2f, AH / 2f);

        /// A point on the unzoomed screen (arena units) back to the arena. Only while CamZoomed.
        V2 UnzoomPoint(V2 s) => (s - new V2(AW / 2f, AH / 2f)) * (1f / camZoom) + camFocus;
    }
}
