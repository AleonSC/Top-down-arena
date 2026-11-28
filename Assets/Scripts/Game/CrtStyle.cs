using UnityEngine;

namespace Arena
{
    /// The CRT look in one place: its colours (and the CRT colour setting) and how its keys are drawn. The lobby, the drawer menus (pause, Settings,
    /// How to play, the profile), the HUD's console and the Play picker all read these, so changing a value here restyles every CRT
    /// screen at once. The older names (CrtStyle.Ink / Dim / Bg, CrtStyle.Body / HeadCol / Gold) are kept as aliases.
    /// Check a change with `Tools/Preview/uishots.sh` (the contact sheet shows every screen).
    public static class CrtStyle
    {
        // --- Colours ----------------------------------------------------------------------------------------------------------------

        // The phosphor colour: -1, the original blue, in the game (there's no setting for it any more); a preview can set another
        // hue (CRTHUE). Ink, Dim, Bg and Bezel follow it. The other colours below are drawn for the blue theme: where they should
        // follow it too, the screen passes them through T (CrtUi.T), which keeps their brightness and moves them to the hue.
        static readonly Color BlueInk = new Color(0.40f, 0.64f, 1f);
        public const float InkSat = 0.6f;  // a picked hue's phosphor: this saturation at full brightness (the blue's own)
        static float hue = -1f;
        static Color ink = BlueInk;

        /// The picked hue (0..1), or -1 for the original blue.
        public static float Hue
        {
            get => hue;
            set
            {
                hue = value < 0f ? -1f : Mathf.Clamp01(value);
                ink = hue < 0f ? BlueInk : Color.HSVToRGB(hue, InkSat, 1f);
            }
        }

        // A grey screen (the Game Mode screen, the lobby): while Accent is set, the screen, its bezel and its boxes are grey and the
        // phosphor (Ink, T: text, keys, lines, corners) is that one colour (the mode's, the role's) instead of the CRT colour setting,
        // so the player's own colour never reaches it. Set it round the screen's drawing and clear it after (null).
        static Color? accent;
        static float accentHue;

        /// The grey screen's one colour, or null for the CRT colour setting (see above).
        public static Color? Accent
        {
            get => accent;
            set
            {
                accent = value;
                if (value is Color c)
                    Color.RGBToHSV(c, out accentHue, out _, out _);
            }
        }

        /// True while a grey screen is drawn (Accent set).
        public static bool Greyed => accent != null;

        public static Color Ink => accent ?? ink;                                             // the phosphor: text, outlines, lit keys
        public static Color Dim => accent != null ? Color.Lerp(Ink, new Color(0.09f, 0.09f, 0.1f), 0.42f)
            : Color.Lerp(Ink, new Color(0.05f, 0.08f, 0.14f), 0.38f);                        // dim text, locked keys, off switches
        public static Color Bg => accent != null ? new Color(0.03f, 0.031f, 0.034f)
            : hue < 0f ? new Color(0.012f, 0.03f, 0.085f) : Tinted(0.045f, 0.12f);           // the screen
        public static Color Bezel => accent != null ? new Color(0.085f, 0.088f, 0.095f)
            : hue < 0f ? new Color(0.05f, 0.07f, 0.14f) : Tinted(0.1f, 0.2f);                // the rim round a screen (CrtUi.Begin)

        /// A colour drawn for the blue theme, moved to the chosen phosphor (or a grey screen's accent): same brightness, saturation and
        /// alpha, the picked hue.
        public static Color T(Color c)
        {
            float h = accent != null ? accentHue : hue;
            if (h < 0f)
                return c;
            Color.RGBToHSV(c, out _, out float s, out float v);
            var o = Color.HSVToRGB(h, s, v);
            o.a = c.a;
            return o;
        }

        /// A dark surface drawn for the blue theme: grey on a grey screen (same brightness), else moved like T.
        public static Color Surface(Color c) => accent != null ? Grey(c) : T(c);

        /// The colour as a neutral grey of the same brightness (alpha kept).
        public static Color Grey(Color c)
        {
            float v = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
            v = Mathf.Max(v, Mathf.Max(c.r, Mathf.Max(c.g, c.b)) * 0.55f);  // a dark navy keeps about its depth, not just its luminance
            return new Color(v, v * 1.02f, v * 1.08f, c.a);
        }

        /// On a grey screen: the colour drawn for the blue theme, moved to the accent (a fixed text colour such as Body or Head).
        static Color OnScreen(Color c) => accent != null ? T(c) : c;

        /// A very dark screen with a breath of the phosphor colour in it.
        static Color Tinted(float darkness, float tint)
        {
            var i = Ink;
            return new Color(darkness * 0.3f + i.r * tint * 0.25f, darkness * 0.3f + i.g * tint * 0.25f, darkness * 0.3f + i.b * tint * 0.25f);
        }

        public static readonly Color Danger = new Color(0.78f, 0.3f, 0.34f);  // Quit, delete, leave (the one danger red; was also CrtStyle.Danger)
        public static readonly Color HudDanger = new Color(1f, 0.3f, 0.3f);  // the HUD's brighter red over the arena: downed players, low health, warning tags
        public static Color Body => OnScreen(new Color(0.76f, 0.80f, 0.91f));  // body text on a screen (the lobby)
        public static Color Desc => OnScreen(new Color(0.55f, 0.7f, 0.97f));   // small descriptions on a screen (the drawer menus)
        public static Color Head => OnScreen(new Color(0.35f, 0.85f, 0.91f));  // section headings (the mockup's teal)
        public static readonly Color Gold = new Color(1f, 0.8f, 0.35f);       // number tiles, highlights, coins (the one gold; was also CrtStyle.Gold)
        public static readonly Color Steel = new Color(0.62f, 0.85f, 1f);     // the Tank's Ironhide shield on the HUD (Ironhide.cs; IronhideLook.cs draws the same in the world)
        public const float TextGlow = 0.16f;  // CrtUi.Text: the four faint copies round glowing text

        // The big boxes and the screen round them (the lobby, and the drawer menus that sit on its loadout box)
        // (On a grey screen the boxes, glass and frame are grey: these pass through T, which keeps a grey grey.)
        public static readonly Color Corner = new Color(0.36f, 0.6f, 1f);           // the bracket corners (LobbyKit.Corners; a grey screen's accent)
        public static Color BoxWash => accent != null ? new Color(0.5f, 0.52f, 0.56f) : new Color(0.36f, 0.64f, 1f);  // the colour of its gradient wash (LobbyKit.Wash)
        public static Color BoxRim => accent != null ? new Color(0.24f, 0.25f, 0.27f) : new Color(0.19f, 0.32f, 0.55f);  // its thin rim
        public static Color ScreenGlow => accent != null ? new Color(0.1f, 0.1f, 0.11f) : new Color(0.05f, 0.13f, 0.32f);  // the glow in the middle of the lobby's screen (alpha 0.38)
        public static Color GlassSurround => accent != null ? new Color(0.03f, 0.03f, 0.033f) : new Color(0.02f, 0.03f, 0.07f);
        public static Color GlassBand => accent != null ? new Color(0.085f, 0.087f, 0.093f) : new Color(0.055f, 0.085f, 0.17f);
        public static Color GlassLine => accent != null ? new Color(0.27f, 0.28f, 0.3f) : new Color(0.17f, 0.27f, 0.50f);  // the lobby's rounded glass frame: outside, the navy band, the blue line
        public static Color ScreenFrame => accent != null ? new Color(0.2f, 0.21f, 0.22f) : new Color(0.15f, 0.21f, 0.37f);  // the thin line round the whole lobby screen

        // --- Soft keys (CrtKit.SoftBox / SoftText: CrtKit.Key, LobbyKit.Key, WinKey, DrawerRow, and the lobby's own boxes) --------

        public const float KeyScale = 0.74f;  // every CRT key's text size is scaled by this (CrtKit.Sz), never below 9
        public const float KeyTint = 0.06f, KeyTintHover = 0.14f, KeyTintOn = 0.2f;  // the accent's fill behind the text
        public const float KeyRim = 0.5f, KeyRimHover = 0.9f, KeyRimOn = 1f;         // the outline's strength
        public const float KeyRimW = 1f, KeyRimOnW = 2f;                              // the outline's width (2 px on the open/selected key)
        public const float KeyGlowOn = 0.1f;                                          // the selected key's soft glow
        public const float KeyTextMix = 0.3f, KeyTextMixLit = 0.75f;                  // text: the accent mixed this far toward white
        public const float KeyLockedTint = 0.03f, KeyLockedDashW = 2f, KeyLockedDash = 0.7f;  // a locked key: dashed
        // A quiet key (CrtUi.SoftKey quiet: the Play picker's DEL until the mouse is on it): a 1 px dashed outline, dim text.
        public const float KeyQuietTint = 0.02f, KeyQuietDashW = 1f, KeyQuietDash = 0.55f, KeyQuietText = 0.8f;

        // A key that can't be used yet (LobbyKit.Key locked: Start before everyone is ready): fainter than a locked slot.
        public const float KeyWaitTint = 0.015f, KeyWaitDash = 0.3f, KeyWaitText = 0.5f;
        // A label that looks like a key (WinKey isStatic).
        public const float KeyStaticTint = 0.06f, KeyStaticRim = 0.5f;

        // --- The HUD console's ability keys (Hud.cs CrtKeyBox) ----------------------------------------------------------------------

        public const float HudKeyTint = 0.09f, HudKeyRimW = 2f;          // a ready key: a faint fill of its colour, a 2 px outline
        public const float HudKeyEmptyTint = 0.02f, HudKeyEmptyDash = 0.55f;  // an empty slot: dashed

        // --- Key colours ----------------------------------------------------------------------------------------------------------

        /// A key's colour: dim when locked, red for danger, else its accent (the phosphor by default).
        public static Color KeyColor(Color? accent, bool danger = false, bool locked = false) =>
            locked ? Dim : danger ? Danger : accent ?? Ink;
    }
}
