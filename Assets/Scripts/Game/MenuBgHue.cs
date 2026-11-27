using UnityEngine;

namespace Arena
{
    /// The menu hue, one setting in two places: the main menu's hue-wheel key (top right corner, hidden until the pointer comes
    /// near) and Settings > Visual effects > CRT colour. Its rainbow strip turns the Holo Floor's colours and the CRT phosphor
    /// (CrtStyle.Hue) to any hue (pref "menu_bg_hue"; RESET goes back to the floor's red and the CRT's blue).
    public partial class ArenaApp
    {
        const float HoloBaseHue = 0.981f;   // the Holo Floor's own hue (its grid red, 353°)
        const float CrtBaseHue = 0.6f;      // the CRT's own blue (CrtStyle's BlueInk, 216°)
        bool menuBgHueOpen, menuBgHueDrag, crtHueDrag;  // (the menu's strip open; it being dragged; Settings' strip being dragged)
        float menuBgHueShown;               // the key's fade (0 hidden .. 1 shown)

        /// The picked hue (0..1; -1: the floor's own red, the CRT's own blue).
        static float MenuBgHue => CrtStyle.Hue;

        void SetMenuBgHue(float h)
        {
            CrtStyle.Hue = h;
            PlayerPrefs.SetFloat("menu_bg_hue", CrtStyle.Hue);
            SavePrefsSoon();  // (a slider: Menus.cs)
        }

        /// Loads the hue (LoadFxSettings); an old CRT colour choice (Green, Amber, Red, Violet) becomes its hue.
        static void LoadMenuHue()
        {
            float h = PlayerPrefs.GetFloat("menu_bg_hue", -1f);
            if (h < 0f)
            {
                int old = PlayerPrefs.GetInt("crt_theme", 0);
                float[] oldHues = { -1f, 0.37f, 0.105f, 0f, 0.74f, -1f };  // Blue, Green, Amber, Red, Violet, White
                h = oldHues[Mathf.Clamp(old, 0, oldHues.Length - 1)];
            }
            CrtStyle.Hue = h;
        }

        /// A rainbow strip to click or drag along, with a thumb on the picked hue (on idle when none is picked). Returns the new
        /// hue (pick itself when untouched).
        static float HueStrip(Rect strip, float pick, float idle, ref bool drag, bool live)
        {
            var e = Event.current;
            const int Segs = 48;
            for (int i = 0; i < Segs; i++)
                Ui.Fill(new Rect(strip.x + strip.width * i / Segs, strip.y + 3f, strip.width / Segs + 0.5f, strip.height - 6f), HueSwatch((i + 0.5f) / Segs));
            if (e.type == EventType.MouseUp)
                drag = false;
            if (live && e.type == EventType.MouseDown && e.button == 0 && strip.Contains(e.mousePosition))
                drag = true;
            float to = pick;
            if (live && drag && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag))
            {
                to = Mathf.Clamp01((e.mousePosition.x - strip.x) / strip.width);
                e.Use();
            }
            float h = to < 0f ? idle : to;  // the thumb: a white frame round the hue
            var th = new Rect(strip.x + strip.width * h - 3f, strip.y, 6f, strip.height);
            Ui.Fill(th, HueSwatch(h));
            Ui.RectEdge(th, 1.5f, Color.white.WithAlpha(to < 0f ? 0.5f : 1f));
            return to;
        }

        /// The Holo Floor's colour c turned to the picked hue (c itself with no pick).
        Rgba HoloHue(Rgba c)
        {
            float pick = MenuBgHue;
            if (pick < 0f)
                return c;
            Color.RGBToHSV(new Color(c.R, c.G, c.B), out float h, out float s, out float v);
            var o = Color.HSVToRGB(MathX.Fmod(h + pick - HoloBaseHue + 1f, 1f), s, v);
            return new Rgba(o.r, o.g, o.b, c.A);
        }

        static Color HueSwatch(float h) => Color.HSVToRGB(h, 0.7f, 1f);

        /// IMGUI: the hue key in the top right and, when open, its strip under it. Fades in while the pointer is near the corner
        /// (or the strip is open), out again 0.25 s after it leaves.
        void DrawMenuBgHue(bool live)
        {
            var e = Event.current;
            const float Key = 30f, Pad = 14f;
            var key = new Rect(W - Pad - Key, Pad, Key, Key);
            var panel = new Rect(key.xMax - 236f, key.yMax + 6f, 236f, 34f);
            var zone = new Rect(W - 140f, 0f, 140f, 90f);  // where the pointer wakes the key
            bool near = live && (zone.Contains(e.mousePosition) || menuBgHueOpen && panel.Contains(e.mousePosition) || menuBgHueDrag);
            if (e.type == EventType.Repaint)
                menuBgHueShown = Mathf.MoveTowards(menuBgHueShown, near || menuBgHueOpen ? 1f : 0f, Time.unscaledDeltaTime / 0.25f);
            if (!live)
                menuBgHueOpen = menuBgHueDrag = false;
            if (menuBgHueShown <= 0.01f)
                return;
            float oldA = Ui.Alpha;
            Ui.Alpha *= menuBgHueShown;
            float pick = MenuBgHue;
            if (DkKey(key, "", Ui.Neutral, menuBgHueOpen, 10) && live)
                menuBgHueOpen = !menuBgHueOpen;
            DrawHueWheel(key, HueSwatch(pick < 0f ? HoloBaseHue : pick));  // LobbyUi.cs
            if (menuBgHueOpen)
                DrawMenuBgHuePanel(panel, pick);
            else if (key.Contains(e.mousePosition))
                Ui.Label(new Rect(key.x - 160f, key.y, 152f, Key), "MENU HUE", 10, CrtStyle.Ink.WithAlpha(0.85f), TextAnchor.MiddleRight, true);
            Ui.Alpha = oldA;
            if (menuBgHueOpen && e.type == EventType.MouseDown && !panel.Contains(e.mousePosition) && !key.Contains(e.mousePosition))
                menuBgHueOpen = false;  // a click anywhere else shuts it
        }

        /// The strip: a rainbow to drag along (the floor's and the CRT's hue follow live) and a RESET key (back to their own red and blue).
        void DrawMenuBgHuePanel(Rect panel, float pick)
        {
            Ui.Fill(panel, new Color(0f, 0f, 0f, 0.8f));
            Ui.RectEdge(panel, 1f, CrtStyle.Ink.WithAlpha(0.45f));
            var strip = new Rect(panel.x + 8f, panel.y + 8f, panel.width - 74f, panel.height - 16f);
            float to = HueStrip(strip, pick, HoloBaseHue, ref menuBgHueDrag, true);
            if (to != pick)
                SetMenuBgHue(to);
            var reset = new Rect(panel.xMax - 60f, panel.y + 6f, 52f, panel.height - 12f);
            if (DkKey(reset, "RESET", Ui.Neutral, pick < 0f, 10))
                SetMenuBgHue(-1f);
        }
    }
}
