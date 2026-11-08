using UnityEngine;

namespace Arena
{
    /// The between-round windows' shared frame: the dark layer over the arena, a part fading (and sliding) in, and the CRT tube a
    /// window sits in. The results screen (HudSummary.cs) is built from tubes; the upgrade picker (HudPicker.cs), the shop (ShopUi.cs),
    /// the path choice (PathsUi.cs) and the Wave cleared box (WaveClearedUi.cs) use the layer and the fade. A new or restyled
    /// between-round window starts from these. Check a change with `Tools/Preview/uishots.sh -r HEAD` (wave-cleared, picker, path,
    /// shop, gameover, victory).
    public partial class ArenaApp
    {
        // --- The dark layer over the arena ------------------------------------------------------------------------------------------

        static readonly Color RoundDark = new Color(0.01f, 0.012f, 0.03f);  // the layer's colour
        const float PickerDark = 0.82f, ShopDark = 0.8f, ResultsDark = 0.84f, WaveClearedDark = 0.35f;  // how dark each window makes the arena
        const float PathDim = 0.72f;  // the path choice's (a hair different from the others)

        /// The dark layer over the whole screen, `a` strong.
        static void RoundBackdrop(float a) => Ui.Fill(new Rect(0f, 0f, W, H), new Color(RoundDark.r, RoundDark.g, RoundDark.b, a));

        // --- A part fading in ------------------------------------------------------------------------------------------------------

        /// Draw something faded in (`a` 0..1) and moved by `offset`, then put things back. Fades Ui.Alpha as well as GUI.color, so
        /// Ui.Fill and Ui.Dot (which set GUI.color themselves) fade with the text.
        void RoundPart(float a, Vector2 offset, System.Action draw)
        {
            if (a <= 0f)
                return;
            if (a >= 1f && offset == Vector2.zero)
            {
                draw();
                return;
            }
            var oldColor = GUI.color;
            float oldAlpha = Ui.Alpha;
            Ui.Alpha = oldAlpha * a;
            GUI.color = new Color(oldColor.r, oldColor.g, oldColor.b, oldColor.a * a);
            GUI.BeginGroup(new Rect(offset.x, offset.y, W, H));
            try { draw(); }
            finally  // (an error inside must not leave the fade, colour and group stuck for the rest of the frame)
            {
                GUI.EndGroup();
                Ui.Alpha = oldAlpha;
                GUI.color = oldColor;
            }
        }

        // --- The CRT tube ------------------------------------------------------------------------------------------------------------

        /// A CRT tube (the lobby's blue screen, CrtUi.Begin / End) that comes in with `k` 0..1 (faded, rising 30 px unless `slide` is
        /// off); `draw` runs between the screen and its glass.
        void RoundTube(Rect r, float k, bool slide, System.Action draw)
        {
            RoundPart(k, slide ? new Vector2(0f, 30f * (1f - Ease(k))) : Vector2.zero, () =>
            {
                CrtUi.Begin(r);
                draw();
                CrtUi.End(r, Time.unscaledTime, fxReduced);
            });
        }
    }
}
