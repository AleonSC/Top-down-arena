using UnityEngine;

namespace Arena
{
    /// Camera rotation: the Rotate camera left / right keys ([ and ] by default, Keybinds) turn the view a quarter turn
    /// anticlockwise / clockwise, so you can look at what sits above or below you. Purely local drawing like the zoom:
    /// ZoomPoint/UnzoomPoint (CameraZoom.cs) turn the screen about its centre, so Px, ToUi and MouseArena (aiming) follow it.
    ///   camRot   the wanted quarter turns clockwise (counts past 3 and below 0, so left and right swing the short way)
    ///   camAng   the drawn angle in radians, a swing toward camRot (eases at RotateEase, snaps when close, so a settled view is exact)
    ///   view     a turned view is a following camera (CamZoomed); HoldInside keeps the focus in the world, with the turned
    ///            screen's bounding box (ViewHalf) mid-swing
    ///   lean     the camera lean's target is an arena direction; it is turned with the view (LeanTurn) so it still leans toward your aim
    public partial class ArenaApp
    {
        const float RotateEase = 9f;

        int camRot;       // wanted quarter turns clockwise
        float camAng;     // drawn angle, radians clockwise

        /// Once a frame (UpdateCameraZoom): the keys step the wanted turn, the drawn angle swings to it; leaving the run resets it.
        void UpdateCameraRotate(float dt)
        {
            if (!(session.Screen == AppScreen.Game && game.Running) || game.Me == null)
            {
                camRot = 0;
                camAng = 0f;
                return;
            }
            if (!game.Paused && !devMenuOpen && GUIUtility.keyboardControl == 0)
            {
                if (Keybinds.Down(KeyAction.RotateRight))
                    camRot++;
                if (Keybinds.Down(KeyAction.RotateLeft))
                    camRot--;
            }
            float want = camRot * (Mathf.PI / 2f);
            camAng += (want - camAng) * Mathf.Min(1f, dt * RotateEase);
            if (Mathf.Abs(want - camAng) < 0.002f)
                camAng = want;
            if (camAng == 0f)
                camRot = 0;  // (settled back at the start: forget the laps)
        }

        /// A view offset (arena units, y down) turned by the drawn angle clockwise.
        V2 RotateView(V2 d)
        {
            if (camAng == 0f)
                return d;
            float c = Mathf.Cos(camAng), s = Mathf.Sin(camAng);
            return new V2(d.X * c - d.Y * s, d.X * s + d.Y * c);
        }

        /// The inverse of RotateView.
        V2 UnrotateView(V2 d)
        {
            if (camAng == 0f)
                return d;
            float c = Mathf.Cos(camAng), s = Mathf.Sin(camAng);
            return new V2(d.X * c + d.Y * s, -d.X * s + d.Y * c);
        }

        /// A lean (a screen shift) from an arena direction: turned with the view.
        V2 LeanTurn(V2 d) => RotateView(d);

        /// Half the arena the screen covers on each axis at this zoom, the turned screen's bounding box.
        V2 ViewHalf()
        {
            float c = Mathf.Abs(Mathf.Cos(camAng)), s = Mathf.Abs(Mathf.Sin(camAng));
            return new V2((c * AW + s * AH) / (2f * camZoom), (s * AW + c * AH) / (2f * camZoom));
        }

        /// Hold a focus inside the world; a view wider than the world is centred.
        static float HoldInside(float v, float half, float size) => half * 2f >= size ? size / 2f : Mathf.Clamp(v, half, size - half);
    }
}
