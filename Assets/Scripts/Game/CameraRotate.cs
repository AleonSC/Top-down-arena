using UnityEngine;

namespace Arena
{
    /// Camera rotation (mockup): press the Rotate Camera key (] by default) and the view turns a quarter turn clockwise, then
    /// the next, back to 0 after four, so you can look at what sits above or below you. Purely local drawing like the zoom:
    /// ZoomPoint/UnzoomPoint (CameraZoom.cs) turn the screen about its centre, so Px, ToUi and MouseArena (aiming) follow it.
    ///   camRot   0-3 quarter turns clockwise; eased to the picture by camRotAnim (a turn is a short swing, not a cut)
    ///   view     a turned view is a following camera (CamZoomed), and the arena's far axis is held by HoldInside
    /// Not done yet: the lean's pixel shift is not turned, and the key is not in Keybinds.
    public partial class ArenaApp
    {
        int camRot;  // quarter turns clockwise

        /// Once a frame (UpdateCameraZoom): the key steps to the next quarter turn; leaving the run turns back to 0.
        void UpdateCameraRotate()
        {
            if (!(session.Screen == AppScreen.Game && game.Running) || game.Me == null)
            {
                camRot = 0;
                return;
            }
            if (!game.Paused && !devMenuOpen && GUIUtility.keyboardControl == 0 && Input.GetKeyDown(KeyCode.RightBracket))
                camRot = (camRot + 1) & 3;
        }

        /// A view offset (arena units, y down) turned camRot quarter turns clockwise.
        V2 RotateView(V2 d)
        {
            switch (camRot & 3)
            {
                case 1: return new V2(-d.Y, d.X);
                case 2: return new V2(-d.X, -d.Y);
                case 3: return new V2(d.Y, -d.X);
                default: return d;
            }
        }

        /// The inverse of RotateView.
        V2 UnrotateView(V2 d)
        {
            switch (camRot & 3)
            {
                case 1: return new V2(d.Y, -d.X);
                case 2: return new V2(-d.X, -d.Y);
                case 3: return new V2(-d.Y, d.X);
                default: return d;
            }
        }

        /// Hold a focus inside the world; a view wider than the world is centred.
        static float HoldInside(float v, float half, float size) => half * 2f >= size ? size / 2f : Mathf.Clamp(v, half, size - half);
    }
}
