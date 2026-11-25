using UnityEngine;

namespace Arena
{
    /// The main menu's background hue: a hue-wheel key in the screen's top right corner, hidden until the pointer comes near,
    /// opens a rainbow strip that turns the Holo Floor's colours to any hue (pref "menu_bg_hue"; RESET goes back to its red).
    public partial class ArenaApp
    {
        const float HoloBaseHue = 0.981f;   // the Holo Floor's own hue (its grid red, 353°)
        float menuBgHue = -1f;              // the picked hue (0..1; -1: the floor's own red), kept in PlayerPrefs
        bool menuBgHueLoaded, menuBgHueOpen, menuBgHueDrag;  // (read from PlayerPrefs; the strip open; its strip being dragged)
        float menuBgHueShown;               // the key's fade (0 hidden .. 1 shown)

        float MenuBgHue
        {
            get
            {
                if (!menuBgHueLoaded)
                {
                    menuBgHue = PlayerPrefs.GetFloat("menu_bg_hue", -1f);
                    menuBgHueLoaded = true;
                }
                return menuBgHue;
            }
        }

        void SetMenuBgHue(float h)
        {
            menuBgHue = h;
            PlayerPrefs.SetFloat("menu_bg_hue", h);
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
                Ui.Label(new Rect(key.x - 160f, key.y, 152f, Key), "BACKGROUND HUE", 10, CrtStyle.Ink.WithAlpha(0.85f), TextAnchor.MiddleRight, true);
            Ui.Alpha = oldA;
            if (menuBgHueOpen && e.type == EventType.MouseDown && !panel.Contains(e.mousePosition) && !key.Contains(e.mousePosition))
                menuBgHueOpen = false;  // a click anywhere else shuts it
        }

        /// The strip: a rainbow to drag along (the floor's hue follows live) and a RESET key (back to the floor's own red).
        void DrawMenuBgHuePanel(Rect panel, float pick)
        {
            var e = Event.current;
            Ui.Fill(panel, new Color(0f, 0f, 0f, 0.8f));
            Ui.RectEdge(panel, 1f, CrtStyle.Ink.WithAlpha(0.45f));
            var strip = new Rect(panel.x + 8f, panel.y + 8f, panel.width - 74f, panel.height - 16f);
            const int Segs = 48;
            for (int i = 0; i < Segs; i++)
                Ui.Fill(new Rect(strip.x + strip.width * i / Segs, strip.y + 3f, strip.width / Segs + 0.5f, strip.height - 6f), HueSwatch((i + 0.5f) / Segs));
            if (e.type == EventType.MouseDown && e.button == 0 && strip.Contains(e.mousePosition))
                menuBgHueDrag = true;
            if (e.type == EventType.MouseUp)
                menuBgHueDrag = false;
            if (menuBgHueDrag && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag))
            {
                SetMenuBgHue(Mathf.Clamp01((e.mousePosition.x - strip.x) / strip.width));
                e.Use();
            }
            float h = pick < 0f ? HoloBaseHue : pick;  // the thumb: a white frame round the floor's hue
            var th = new Rect(strip.x + strip.width * h - 3f, strip.y, 6f, strip.height);
            Ui.Fill(th, HueSwatch(h));
            Ui.RectEdge(th, 1.5f, Color.white);
            var reset = new Rect(panel.xMax - 60f, panel.y + 6f, 52f, panel.height - 12f);
            if (DkKey(reset, "RESET", Ui.Neutral, pick < 0f, 10))
                SetMenuBgHue(-1f);
        }
    }
}
