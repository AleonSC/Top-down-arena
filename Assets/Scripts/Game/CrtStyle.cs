using UnityEngine;

namespace Arena
{
    /// The CRT look in one place: its colours (and the CRT colour setting) and how its keys are drawn. The lobby, the drawer menus (pause, Settings,
    /// How to play, the profile), the HUD's console and the Play picker all read these, so changing a value here restyles every CRT
    /// screen at once. The older names (CrtUi.Ink / Dim / Bg, LobbyKit.Soft / HeadCol / Gold) are kept as aliases.
    /// Check a change with `Tools/Preview/uishots.sh` (the contact sheet shows every screen).
    public static class CrtStyle
    {
        // --- Colours ----------------------------------------------------------------------------------------------------------------

        // The phosphor colour is a setting (Settings > Visual effects > CRT colour); 0 is the original blue. Ink, Dim, Bg and Bezel follow it.
        // The other colours below are drawn for the blue theme: where they should follow the setting too, the screen passes them
        // through T (CrtUi.T), which keeps their brightness and moves them to the chosen hue.
        public static readonly string[] ThemeNames = { "Blue", "Green", "Amber", "Red", "Violet", "White" };
        static readonly Color[] ThemeInk = { new Color(0.40f, 0.64f, 1f), new Color(0.35f, 0.95f, 0.5f), new Color(1f, 0.72f, 0.25f), new Color(1f, 0.42f, 0.42f), new Color(0.72f, 0.5f, 1f), new Color(0.88f, 0.92f, 0.96f) };
        static int theme;

        public static int Theme
        {
            get => theme;
            set => theme = Mathf.Clamp(value, 0, ThemeInk.Length - 1);
        }

        public static Color Ink => ThemeInk[theme];                                           // the phosphor: text, outlines, lit keys
        public static Color Dim => Color.Lerp(Ink, new Color(0.05f, 0.08f, 0.14f), 0.38f);   // dim text, locked keys, off switches
        public static Color Bg => theme == 0 ? new Color(0.012f, 0.03f, 0.085f) : Tinted(0.045f, 0.12f);  // the screen
        public static Color Bezel => theme == 0 ? new Color(0.05f, 0.07f, 0.14f) : Tinted(0.1f, 0.2f);    // the rim round a screen (CrtUi.Begin)

        /// A colour drawn for the blue theme, moved to the chosen phosphor: same brightness and alpha, the theme's hue.
        public static Color T(Color c)
        {
            if (theme == 0)
                return c;
            Color.RGBToHSV(c, out _, out float s, out float v);
            Color.RGBToHSV(Ink, out float ih, out float isat, out _);
            var o = Color.HSVToRGB(ih, Mathf.Clamp01(s * isat / 0.6f), v);
            o.a = c.a;
            return o;
        }

        /// A very dark screen with a breath of the phosphor colour in it.
        static Color Tinted(float darkness, float tint)
        {
            var i = Ink;
            return new Color(darkness * 0.3f + i.r * tint * 0.25f, darkness * 0.3f + i.g * tint * 0.25f, darkness * 0.3f + i.b * tint * 0.25f);
        }

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
