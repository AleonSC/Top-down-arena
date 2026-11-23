using UnityEngine;

namespace Arena
{
    /// The machine's parts (Docs/Mockups/disk-boot-sequence.html, main-menu-machines.html B, whos-playing-carousel.html): graphite metal
    /// with a lit top edge, neon outlines, keycaps with a lamp, round keys, vents and lights. Shared by the monitor (PcMonitor.cs), the
    /// Who's playing? carousel (DiskCarousel.cs) and the main menu's case (MenuMachine.cs).
    public partial class ArenaApp
    {
        static readonly Color MetalHi = new Color(0.357f, 0.369f, 0.4f), MetalMid = new Color(0.227f, 0.235f, 0.259f), MetalLo = new Color(0.11f, 0.114f, 0.129f);
        static readonly Color MetalEdge = new Color(0.055f, 0.059f, 0.071f), VentBar = new Color(0.043f, 0.047f, 0.059f);
        static readonly Color MachNeon = new Color(0.81f, 0.878f, 1f), MachHalo = new Color(0.627f, 0.765f, 1f);
        static readonly Color LampGreen = new Color(0.42f, 1f, 0.6f), LampAmber = new Color(1f, 0.71f, 0.278f), LampOff = new Color(0.11f, 0.165f, 0.125f);
        static readonly Color KeyInk = new Color(0.914f, 0.925f, 0.949f), ChinInk = new Color(0.604f, 0.627f, 0.678f);
        static readonly float[] MetalStops = { 0f, 0.3f, 1f };
        static readonly Color[] MetalCols = { MetalHi, MetalMid, MetalLo };

        /// A soft shadow under a box: a few widening, fading rounded layers below it.
        static void SoftShadow(Rect r, int radius, float dy, float a)
        {
            for (int i = 3; i >= 1; i--)
            {
                float g = i * 4f;
                Ui.RoundBox(new Rect(r.x - g / 2f, r.y + dy - g / 4f, r.width + g, r.height + g / 2f), new Color(0f, 0f, 0f, a * 0.3f), radius + i * 2);
            }
        }

        /// Graphite metal: a dark edge, the face lit at the top and dark at the bottom, and a bright line along its top edge.
        /// radiusTop / radiusBottom: the corners (the case is rounder at the bottom).
        static void Metal(Rect r, int radius, bool shadow = true)
        {
            if (shadow)
                SoftShadow(r, radius, 10f, 0.45f);
            Ui.RoundBox(r, MetalEdge, radius);
            Ui.RoundGrad(new Rect(r.x + 2f, r.y + 2f, r.width - 4f, r.height - 4f), radius - 2f, MetalStops, MetalCols);
            Ui.Fill(new Rect(r.x + radius, r.y + 2f, r.width - 2f * radius, 1f), new Color(1f, 1f, 1f, 0.35f));
        }

        /// A neon tube round r, following its rounded corners (radius: r's): a soft halo fading out from it, the bright tube, and a hot
        /// white core along the tube. k: its strength; flare: extra glow (the case landing). In IMGUI, so it clips with groups.
        static void NeonBox(Rect r, float k, float flare = 0f, int radius = 10)
        {
            if (k <= 0f)
                return;
            int rings = 7;
            float spread = 12f + 22f * flare;
            for (int i = rings; i >= 1; i--)  // the halo: wide, faint rings stepping outward
            {
                float g = 2f + spread * i / rings, f = 1f - (float)i / (rings + 1);
                var hr = new Rect(r.x - g, r.y - g, r.width + 2f * g, r.height + 2f * g);
                Ui.RoundRim(hr, MachHalo.WithAlpha(k * (0.11f + 0.14f * flare) * f * f * 2f), radius + Mathf.RoundToInt(g), 4);
            }
            var tube = new Rect(r.x - 3f, r.y - 3f, r.width + 6f, r.height + 6f);
            Ui.RoundRim(new Rect(tube.x - 1f, tube.y - 1f, tube.width + 2f, tube.height + 2f), MachHalo.WithAlpha(0.45f * k), radius + 4, 4);
            Ui.RoundRim(tube, MachNeon.WithAlpha(0.95f * k), radius + 3, 2);
            Ui.RoundRim(new Rect(tube.x + 0.5f, tube.y + 0.5f, tube.width - 1f, tube.height - 1f), new Color(1f, 1f, 1f, 0.55f * k), radius + 3, 1);
        }

        /// A rectangular ring of width w just inside r.
        static void EdgeRing(Rect r, float w, Color c)
        {
            Ui.Fill(new Rect(r.x, r.y, r.width, w), c);
            Ui.Fill(new Rect(r.x, r.yMax - w, r.width, w), c);
            Ui.Fill(new Rect(r.x, r.y + w, w, r.height - 2f * w), c);
            Ui.Fill(new Rect(r.xMax - w, r.y + w, w, r.height - 2f * w), c);
        }

        /// Vertical slats (a grille): dark bars 5 px wide every 10 px.
        static void Vent(Rect r, float a = 1f)
        {
            for (float x = r.x; x + 5f <= r.xMax + 0.5f; x += 10f)
                Ui.Fill(new Rect(x, r.y, 5f, r.height), VentBar.WithAlpha(a));
        }

        /// Horizontal slats: dark bars 4 px tall every 9 px.
        static void HVent(Rect r)
        {
            for (float y = r.y; y + 4f <= r.yMax + 0.5f; y += 9f)
                Ui.Fill(new Rect(r.x, y, r.width, 4f), VentBar);
        }

        /// A small round light at (x, y) (its top-left), d px across, glowing when lit.
        static void Led(float x, float y, Color c, bool on, float d = 8f)
        {
            if (on)
                Ui.RadialGlow(new Rect(x - d, y - d, d * 3f, d * 3f), c.WithAlpha(0.55f));
            Ui.Dot(new Rect(x, y, d, d), on ? c : Color.Lerp(c, new Color(0.08f, 0.08f, 0.08f), 0.8f));
        }

        static int machPressed;  // the key the mouse went down on (until it comes up)

        static int MachId(Rect r) => Mathf.RoundToInt(r.x * 7f + r.y * 131f + r.width * 3f) + 17;

        /// Tracks the press on r (before Ui.HitArea takes the event): true while the mouse is held down on it.
        static bool MachHeld(Rect r, bool live)
        {
            var e = Event.current;
            int id = MachId(r);
            if (live && GUI.enabled && e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
                machPressed = id;
            else if (e.type == EventType.MouseUp)
                machPressed = 0;
            return live && machPressed == id && r.Contains(e.mousePosition);
        }

        /// A graphite keycap (the mockups' .key): rounded, lit at the top, a 6 px dark side under it that it travels down over when
        /// pressed, a lamp before the legend (lamp: -1 none, 0 off, 1 on) and an optional icon or the Play triangle. True when clicked.
        static bool MachKey(Rect r, string text, int size, bool live, int lamp = -1, Texture2D icon = null, bool tri = false, float dim = 1f)
        {
            bool held = MachHeld(r, live);
            bool hover = live && GUI.enabled && r.Contains(Event.current.mousePosition);
            float down = held ? 5f : 0f;
            SoftShadow(new Rect(r.x, r.y + 6f, r.width, r.height), 7, 3f, 0.5f);
            Ui.RoundBox(new Rect(r.x, r.y + 6f - down * 0.2f, r.width, r.height), new Color(0.086f, 0.09f, 0.102f), 7);  // the key's side
            var cap = new Rect(r.x, r.y + down, r.width, r.height);
            Ui.RoundBox(cap, new Color(0.043f, 0.047f, 0.059f), 7);
            float lit = hover ? 0.12f : 0f;
            Ui.RoundGrad(new Rect(cap.x + 2f, cap.y + 2f, cap.width - 4f, cap.height - 4f), 5f, new[] { 0f, 0.6f, 1f },
                new[] { Color.Lerp(new Color(0.302f, 0.314f, 0.345f), Color.white, lit), Color.Lerp(new Color(0.173f, 0.18f, 0.204f), Color.white, lit * 0.6f), new Color(0.129f, 0.133f, 0.153f) });
            Ui.Fill(new Rect(cap.x + 7f, cap.y + 2f, cap.width - 14f, 1f), new Color(1f, 1f, 1f, held ? 0.3f : 0.4f));
            // The legend: lamp, icon or triangle, text, centred together.
            float tw = Ui.TextWidth(text, size, true) - 6f, gap = 10f;
            float lw = lamp >= 0 ? 9f + gap : 0f, iw = tri ? 14f + gap : icon != null ? size + gap : 0f;
            float x = cap.center.x - (lw + iw + tw) / 2f, cy = cap.center.y;
            if (lamp >= 0)
            {
                if (lamp == 1)
                    Ui.RadialGlow(new Rect(x - 7f, cy - 11.5f, 23f, 23f), LampGreen.WithAlpha(0.6f));
                Ui.Dot(new Rect(x, cy - 4.5f, 9f, 9f), lamp == 1 ? LampGreen : LampOff);
                x += lw;
            }
            if (tri)
            {
                Ui.RadialGlow(new Rect(x - 6f, cy - 13f, 26f, 26f), new Color(0.49f, 1f, 0.66f, 0.35f));
                for (int k = 0; k < 9; k++)  // a right-pointing triangle, 14 wide and 18 tall
                    Ui.Fill(new Rect(x, cy - 9f + k, 14f * (k + 1) / 9f, 1f), new Color(0.49f, 1f, 0.66f));
                for (int k = 0; k < 9; k++)
                    Ui.Fill(new Rect(x, cy + k, 14f * (9 - k) / 9f, 1f), new Color(0.49f, 1f, 0.66f));
                x += iw;
            }
            else if (icon != null)
            {
                Ui.Icon(new Rect(x, cy - size / 2f, size, size), icon, KeyInk.WithAlpha(dim));
                x += iw;
            }
            Ui.Label(new Rect(x, cap.y + 1f, tw + 12f, cap.height), text, size, new Color(0f, 0f, 0f, 0.5f * dim), TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(x, cap.y, tw + 12f, cap.height), text, size, KeyInk.WithAlpha(dim), TextAnchor.MiddleLeft, true);
            return live && Ui.HitArea(new Rect(r.x, r.y, r.width, r.height + 6f));
        }

        /// A round graphite key (the carousel's arrows): d across, with a glyph. True when clicked.
        static bool RoundKey(Rect r, string glyph, bool live)
        {
            bool held = MachHeld(r, live);
            bool hover = live && r.Contains(Event.current.mousePosition);
            float down = held ? 4f : 0f;
            Ui.Dot(new Rect(r.x, r.y + 5f, r.width, r.height), new Color(0.078f, 0.082f, 0.094f));
            var cap = new Rect(r.x, r.y + down, r.width, r.height);
            Ui.Dot(cap, new Color(0.043f, 0.047f, 0.059f));
            var face = new Rect(cap.x + 2f, cap.y + 2f, cap.width - 4f, cap.height - 4f);
            Ui.Dot(face, hover ? new Color(0.2f, 0.21f, 0.235f) : new Color(0.165f, 0.173f, 0.192f));
            Ui.Dot(new Rect(face.x + face.width * 0.08f, face.y + 1f, face.width * 0.84f, face.height * 0.8f), hover ? new Color(0.27f, 0.28f, 0.31f) : new Color(0.235f, 0.245f, 0.27f));
            Ui.RadialGlow(new Rect(face.x + face.width * 0.2f, face.y + face.height * 0.06f, face.width * 0.6f, face.height * 0.5f), new Color(0.45f, 0.47f, 0.52f, 0.55f));  // the dome's light, inside its edge
            Ui.Label(new Rect(cap.x, cap.y - 2f, cap.width, cap.height), glyph, 26, new Color(0.81f, 0.878f, 1f), TextAnchor.MiddleCenter, true);
            return live && Ui.HitArea(r);
        }

        static int wheelEvtFrame = -1, wheelReadFrame = -1;

        /// The mouse wheel over r (live: it may be used), in IMGUI's wheel units (about 3 a notch; positive: down, toward the end).
        /// Taken from a wheel event when one comes, else read once a frame on Repaint from the input, as the camera zoom does: IMGUI's
        /// wheel events don't reach these lists in every setup. The machine's lists (the carousel, the runs, the bay's settings) use it.
        static float WheelOver(Rect r, bool live)
        {
            var e = Event.current;
            if (!live || !r.Contains(e.mousePosition))
                return 0f;
            if (e.type == EventType.ScrollWheel)
            {
                wheelEvtFrame = Time.frameCount;
                float d = e.delta.y;
                e.Use();
                return d;
            }
            if (e.type != EventType.Repaint || wheelEvtFrame == Time.frameCount || wheelReadFrame == Time.frameCount)
                return 0f;
            wheelReadFrame = Time.frameCount;
            return -Input.mouseScrollDelta.y * 3f;
        }

        /// A small label in the hardware's lettering (bold).
        static void ChinText(Rect r, string text, int size, Color c, TextAnchor a = TextAnchor.MiddleLeft) => Ui.Label(r, text, size, c, a, true);
    }
}
