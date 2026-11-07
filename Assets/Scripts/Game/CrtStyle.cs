using UnityEngine;

namespace Arena
{
    /// The blue-phosphor CRT look in one place: its colours and how its keys are drawn. The lobby, the drawer menus (pause, Settings,
    /// How to play, the profile), the HUD's console and the Play picker all read these, so changing a value here restyles every CRT
    /// screen at once. The older names (CrtUi.Ink / Dim / Bg, LobbyKit.Soft / HeadCol / Gold) are kept as aliases.
    /// Check a change with `Tools/Preview/uishots.sh` (the contact sheet shows every screen).
    public static class CrtStyle
    {
        // --- Colours ----------------------------------------------------------------------------------------------------------------

        public static readonly Color Ink = new Color(0.40f, 0.64f, 1f);     // the phosphor: text, outlines, lit keys
        public static readonly Color Dim = new Color(0.25f, 0.42f, 0.72f);  // dim text, locked keys, off switches
        public static readonly Color Bg = new Color(0.012f, 0.03f, 0.085f);  // the screen
        public static readonly Color Bezel = new Color(0.05f, 0.07f, 0.14f); // the rim round a screen (CrtUi.Begin)
        public static readonly Color Danger = new Color(1f, 0.42f, 0.48f);   // Quit, delete, leave
        public static readonly Color DangerDark = new Color(0.1f, 0.02f, 0.03f);  // text on a lit danger key (inverse video)
        public static readonly Color Body = new Color(0.76f, 0.80f, 0.91f);  // body text on a screen (the lobby)
        public static readonly Color Desc = new Color(0.55f, 0.7f, 0.97f);   // small descriptions on a screen (the drawer menus)
        public static readonly Color Head = new Color(0.35f, 0.85f, 0.91f);  // section headings (the mockup's teal)
        public static readonly Color Gold = new Color(1f, 0.83f, 0.42f);     // number tiles, highlights
        public const float TextGlow = 0.16f;  // CrtUi.Text: the four faint copies round glowing text

        // The big boxes and the screen round them (the lobby, and the drawer menus that sit on its loadout box)
        public static readonly Color Corner = new Color(0.36f, 0.6f, 1f);           // the bracket corners (LobbyKit.Corners)
        public static readonly Color BoxFill = new Color(0.027f, 0.051f, 0.118f, 0.98f);  // the loadout box (CrtKit.LoadoutBox)
        public static readonly Color BoxTop = new Color(0.06f, 0.1f, 0.22f, 0.22f);  // its lighter top half
        public static readonly Color BoxRim = new Color(0.19f, 0.32f, 0.55f);       // its thin rim
        public static readonly Color ScreenGlow = new Color(0.05f, 0.13f, 0.32f);   // the glow in the middle of the lobby's screen (alpha 0.38)
        public static readonly Color GlassSurround = new Color(0.02f, 0.03f, 0.07f), GlassBand = new Color(0.055f, 0.085f, 0.17f),
            GlassLine = new Color(0.17f, 0.27f, 0.50f);                              // the lobby's rounded glass frame: outside, the navy band, the blue line
        public static readonly Color ScreenFrame = new Color(0.15f, 0.21f, 0.37f);  // the thin line round the whole lobby screen

        // --- Soft keys (CrtKit.SoftBox / SoftText: CrtKit.Key, LobbyKit.Key, WinKey, DrawerRow, and the lobby's own boxes) --------

        public const float KeyScale = 0.74f;  // every CRT key's text size is scaled by this (CrtKit.Sz), never below 9
        public const float KeyTint = 0.06f, KeyTintHover = 0.14f, KeyTintOn = 0.2f;  // the accent's fill behind the text
        public const float KeyRim = 0.5f, KeyRimHover = 0.9f, KeyRimOn = 1f;         // the outline's strength
        public const float KeyRimW = 1f, KeyRimOnW = 2f;                              // the outline's width (2 px on the open/selected key)
        public const float KeyGlowOn = 0.1f;                                          // the selected key's soft glow
        public const float KeyTextMix = 0.3f, KeyTextMixLit = 0.75f;                  // text: the accent mixed this far toward white
        public const float KeyLockedTint = 0.03f, KeyLockedDashW = 2f, KeyLockedDash = 0.7f;  // a locked key: dashed

        // A key that can't be used yet (LobbyKit.Key locked: Start before everyone is ready): fainter than a locked slot.
        public const float KeyWaitTint = 0.015f, KeyWaitDash = 0.3f, KeyWaitText = 0.5f;
        // A label that looks like a key (WinKey isStatic).
        public const float KeyStaticTint = 0.06f, KeyStaticRim = 0.5f;

        // --- The HUD console's ability keys (Hud.cs CrtKeyBox) ----------------------------------------------------------------------

        public const float HudKeyTint = 0.09f, HudKeyRimW = 2f;          // a ready key: a faint fill of its colour, a 2 px outline
        public const float HudKeyEmptyTint = 0.02f, HudKeyEmptyDash = 0.55f;  // an empty slot: dashed

        // --- Inverse-video keys (CrtUi.Button / Small: the Play picker and the main menu's run screens) -----------------------------

        public const float InvTint = 0.07f, InvLit = 0.95f, InvRimW = 2f;  // outlined; under the mouse a solid block with dark text

        /// A key's colour: dim when locked, red for danger, else its accent (the phosphor by default).
        public static Color KeyColor(Color? accent, bool danger = false, bool locked = false) =>
            locked ? Dim : danger ? Danger : accent ?? Ink;
    }
}
