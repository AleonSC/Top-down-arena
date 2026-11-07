using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Shared pieces of the CRT cabinet menus (Docs/Mockups/crt-pause-menu.html, crt-profile.html, crt-lobby-digital.html):
    /// text that backspaces and retypes, the screen power-on (a bright line that opens, then a flash), the boot-log line,
    /// the scan bar, an inverse-video digital key, and the cabinet chassis with its edge lights. Colours come from CrtUi
    /// (Ink, Dim, Bg). `calm` (Reduced effects) drops the glow, the flash and the typing: everything just shows.
    /// The power-on is a vertical flicker only: no sideways movement, and it never plays twice (the caller keeps an age
    /// that stops at 1 s; after that nothing here animates).
    public static class CrtKit
    {
        public const float EraseLen = 0.24f, TypeLen = 0.4f, PowerLen = 1f;

        /// Key text is drawn at this fraction of the size asked for: the game's font is much bigger than the mockups' pixel font,
        /// and the original menus used small, light text.
        public const float KeyScale = 0.74f;

        public static int Sz(int s) => Mathf.Max(9, Mathf.RoundToInt(s * KeyScale));

        /// Text colour on a soft key: the accent mixed toward white (more when lit).
        public static Color SoftText(Color c, bool lit) => Color.Lerp(c, Color.white, lit ? 0.75f : 0.3f);

        /// The soft key box: a faint tint of the accent and a thin outline; hover brightens it; `on` gets a 2 px outline, a
        /// stronger tint and a glowing underline (no solid fill). locked: dim and dashed.
        public static void SoftBox(Rect r, Color c, bool on, bool hover, bool locked)
        {
            if (locked)
            {
                Ui.Fill(r, new Color(c.r, c.g, c.b, 0.03f));
                Dashed(r, 2f, new Color(c.r, c.g, c.b, 0.7f));
                return;
            }
            Ui.Fill(r, new Color(c.r, c.g, c.b, on ? 0.2f : hover ? 0.14f : 0.06f));
            Frame(r, on ? 2f : 1f, new Color(c.r, c.g, c.b, on ? 1f : hover ? 0.9f : 0.5f));
            if (on)
            {
                Ui.Fill(new Rect(r.x + 2f, r.yMax - 10f, r.width - 4f, 5f), new Color(c.r, c.g, c.b, 0.12f));
                Ui.Fill(new Rect(r.x + 8f, r.yMax - 5f, r.width - 16f, 2f), c);
            }
        }

        // --- Typed text -----------------------------------------------------------------------------------------------

        class Slot
        {
            public string From = "", Target = "";
            public float T0;
        }

        static readonly Dictionary<string, Slot> slots = new Dictionary<string, Slot>();

        /// What to show now for a label that retypes when its text changes: the old text backspaces away over EraseLen,
        /// then the new text types in over TypeLen. A slot seen for the first time types in from nothing (after `delay`
        /// seconds). `slot` names the label (any unique string); calm shows the text as it is.
        public static string Typed(string slot, string target, float now, bool calm, float delay = 0f)
        {
            if (calm || string.IsNullOrEmpty(slot) || string.IsNullOrEmpty(target) && !slots.ContainsKey(slot))
                return target ?? "";
            target = target ?? "";
            Slot s;
            if (!slots.TryGetValue(slot, out s))
            {
                s = new Slot { Target = target, T0 = now + delay };
                slots[slot] = s;
            }
            else if (s.Target != target)
            {
                s.From = Visible(s, now);
                s.Target = target;
                s.T0 = now;
            }
            return Visible(s, now);
        }

        static string Visible(Slot s, float now)
        {
            float e = now - s.T0;
            if (e < 0f)
                return "";
            bool erase = s.From.Length > 0;
            if (erase && e < EraseLen)
                return s.From.Substring(0, Mathf.RoundToInt(s.From.Length * (1f - e / EraseLen)));
            float p = Mathf.Clamp01((e - (erase ? EraseLen : 0f)) / TypeLen);
            return s.Target.Substring(0, Mathf.RoundToInt(s.Target.Length * p));
        }

        /// Forget every slot whose name starts with `prefix`: the next time they are drawn they type in from nothing
        /// (call when a window opens).
        public static void Forget(string prefix)
        {
            var dead = new List<string>();
            foreach (var k in slots.Keys)
                if (k.StartsWith(prefix))
                    dead.Add(k);
            foreach (var k in dead)
                slots.Remove(k);
        }

        /// Has this label finished typing (no erase or type left)?
        public static bool Settled(string slot, float now)
        {
            Slot s;
            return !slots.TryGetValue(slot, out s) || now - s.T0 >= (s.From.Length > 0 ? EraseLen : 0f) + TypeLen;
        }

        /// The header's sub-line while a window opens: a boot log types, holds, backspaces, then the real line types.
        /// `age` is seconds since the line should start (negative: nothing yet).
        public static string BootLine(string log, string final, float age, bool calm)
        {
            if (calm)
                return final;
            if (age < 0f)
                return "";
            const float typeLog = 0.52f, hold = 0.42f, erase = 0.26f, typeFinal = 0.42f;
            if (age < typeLog)
                return log.Substring(0, Mathf.RoundToInt(log.Length * age / typeLog));
            age -= typeLog;
            if (age < hold)
                return log;
            age -= hold;
            if (age < erase)
                return log.Substring(0, Mathf.RoundToInt(log.Length * (1f - age / erase)));
            age -= erase;
            return final.Substring(0, Mathf.RoundToInt(final.Length * Mathf.Clamp01(age / typeFinal)));
        }

        /// Text revealed once, left to right, `len` seconds after `age` reaches 0 (for the opening only).
        public static string Reveal(string text, float age, float len, bool calm)
        {
            if (calm)
                return text;
            if (age <= 0f)
                return "";
            return text.Substring(0, Mathf.RoundToInt(text.Length * Mathf.Clamp01(age / len)));
        }

        // --- Screen power-on ------------------------------------------------------------------------------------------

        /// 0 not yet on, 1 a line opening (draw only `shown`, the frame), 2 on (draw normally, then PowerFlash).
        public static int PowerState(float age, bool calm)
        {
            if (calm || age >= 0.3f)
                return 2;
            return age < 0f ? 0 : 1;
        }

        /// The frame while the line opens: from a thin bright line to the full height (vertical only).
        public static Rect PowerRect(Rect r, float age)
        {
            float k = age < 0.1f ? 0.012f : Mathf.Lerp(0.012f, 1f, Mathf.Clamp01((age - 0.1f) / 0.2f));
            float h = Mathf.Max(2f, r.height * k);
            return new Rect(r.x, r.center.y - h / 2f, r.width, h);
        }

        /// The white flash over a screen as it comes on, fading out by 0.8 s. Draw it after the screen's content.
        public static void PowerFlash(Rect r, float age, bool calm)
        {
            if (calm || age < 0f || age > 0.8f || Event.current.type != EventType.Repaint)
                return;
            float a = age < 0.1f ? age / 0.1f * 0.7f : age < 0.3f ? 0.7f - (age - 0.1f) / 0.2f * 0.3f : 0.4f * (1f - (age - 0.3f) / 0.5f);
            Ui.Fill(r, new Color(0.8f, 0.9f, 1f, a * 0.5f));
        }

        /// Draws the screen frame for its opening and says whether to draw its content (state 2). Typical use:
        ///   if (CrtKit.PowerOn(r, age, calm)) { ...content...; CrtUi.End(...); CrtKit.PowerFlash(r, age, calm); }
        public static bool PowerOn(Rect r, float age, bool calm)
        {
            int st = PowerState(age, calm);
            if (st == 0)
                return false;
            if (st == 1)
            {
                var pr = PowerRect(r, age);
                CrtUi.Begin(pr);
                Ui.Fill(pr, new Color(0.8f, 0.9f, 1f, 0.55f));
                return false;
            }
            CrtUi.Begin(r);
            return true;
        }

        /// A bright bar sweeping once down a screen (the options list as it fills). `age` 0..1.1 s.
        public static void ScanBar(Rect r, float age, bool calm)
        {
            if (calm || age < 0f || age > 1.1f || Event.current.type != EventType.Repaint)
                return;
            float y = Mathf.Lerp(r.y - 46f, r.yMax, age / 1.1f);
            for (int i = 0; i < 10; i++)
            {
                float y0 = Mathf.Max(y + 46f * i / 10f, r.y), y1 = Mathf.Min(y + 46f * (i + 1) / 10f + 1f, r.yMax);
                if (y1 > y0)
                    Ui.Fill(new Rect(r.x, y0, r.width, y1 - y0), new Color(CrtUi.Ink.r, CrtUi.Ink.g, CrtUi.Ink.b, 0.5f * (i + 1f) / 10f));
            }
        }

        // --- Frames, keys, chassis ------------------------------------------------------------------------------------

        public static void Frame(Rect r, float w, Color c)
        {
            Ui.Fill(new Rect(r.x, r.y, r.width, w), c);
            Ui.Fill(new Rect(r.x, r.yMax - w, r.width, w), c);
            Ui.Fill(new Rect(r.x, r.y, w, r.height), c);
            Ui.Fill(new Rect(r.xMax - w, r.y, w, r.height), c);
        }

        /// A dashed frame (locked keys and empty slots).
        public static void Dashed(Rect r, float w, Color c)
        {
            for (float x = r.x; x < r.xMax; x += 10f)
            {
                float len = Mathf.Min(6f, r.xMax - x);
                Ui.Fill(new Rect(x, r.y, len, w), c);
                Ui.Fill(new Rect(x, r.yMax - w, len, w), c);
            }
            for (float y = r.y; y < r.yMax; y += 10f)
            {
                float len = Mathf.Min(6f, r.yMax - y);
                Ui.Fill(new Rect(r.x, y, w, len), c);
                Ui.Fill(new Rect(r.xMax - w, y, w, len), c);
            }
        }

        /// An inverse-video digital key: outlined and faintly lit, filled with the ink (dark text) under the mouse or when
        /// `on`. Optional icon (kept in its own colours when `colorIcon`), a name and a smaller line under it. `slot`
        /// makes the texts retype when they change (CrtKit.Typed). locked: dim, dashed, never clickable. Returns true on
        /// a click.
        public static bool Key(Rect r, string name, string sub, Texture2D icon, bool on, bool calm, float now = 0f, string slot = null,
            int size = 20, Color? accent = null, bool danger = false, bool locked = false, bool colorIcon = false, TextAnchor align = TextAnchor.MiddleLeft)
        {
            size = Sz(size);
            bool hover = GUI.enabled && !locked && r.Contains(Event.current.mousePosition);
            if (!locked)
                Ui.NoteHover(r);
            Color c = locked ? CrtUi.Dim : danger ? new Color(1f, 0.42f, 0.48f) : accent ?? CrtUi.Ink;
            Color dark = danger ? new Color(0.1f, 0.02f, 0.03f) : CrtUi.Bg;
            bool lit = (hover || on) && !locked;
            bool fill = false;  // the text is light on dark now, so it always glows
            SoftBox(r, c, on && !locked, hover && !locked, locked);
            Color tc = locked ? c : SoftText(c, lit);
            bool two = !string.IsNullOrEmpty(sub);
            float isz = Mathf.Min(r.height - 12f, size + 8f);
            float x = r.x + 12f;
            if (icon != null)
            {
                Ui.Icon(new Rect(x, r.center.y - isz / 2f, isz, isz), icon, colorIcon ? (locked ? new Color(1f, 1f, 1f, 0.5f) : Color.white) : tc);
                x += isz + 10f;
            }
            string n = slot != null ? Typed(slot + ".n", name, now, calm) : name;
            float tw = r.xMax - 10f - x;
            if (align == TextAnchor.MiddleCenter)
            {
                x = r.x + 6f;
                tw = r.width - 12f;
            }
            float nh = two ? size + 6f : r.height, ny = two ? r.center.y - (size + 6f + 14f) / 2f : r.y;
            var nr = new Rect(x, ny, tw, nh);
            if (fill)
                Ui.Label(nr, n, size, tc, align, true);
            else
                CrtUi.Text(nr, n, size, tc, align, true, calm);
            if (two)
            {
                string sb = slot != null ? Typed(slot + ".s", sub, now, calm) : sub;
                Ui.Label(new Rect(x, ny + size + 5f, tw, 14f), sb, 11, new Color(tc.r, tc.g, tc.b, fill ? 0.8f : 0.75f), align, false);
            }
            return Ui.HitArea(r) && !locked;
        }

        /// The cabinet chassis the menus slide out of: a dark blue panel with a bright right edge, a comb along the bottom
        /// and a strip of edge lights that switch on from the top (`age` seconds since it opened; 0.35 s in, 38 ms each).
        public static void Cabinet(Rect r, float age, bool calm)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            const int steps = 24;
            for (int i = 0; i < steps; i++)  // a left-to-right gradient, in strips that meet exactly
            {
                float x0 = Mathf.Round(r.x + r.width * i / steps), x1 = Mathf.Round(r.x + r.width * (i + 1) / steps);
                float k = (float)i / steps;
                Ui.Fill(new Rect(x0, r.y, x1 - x0, r.height), new Color(Mathf.Lerp(0.06f, 0.04f, k), Mathf.Lerp(0.085f, 0.06f, k), Mathf.Lerp(0.22f, 0.17f, k), 0.98f));
            }
            Ui.Fill(new Rect(r.xMax - 2f, r.y, 2f, r.height), new Color(0.23f, 0.33f, 0.65f, 1f));
            for (float x = r.x; x < r.xMax - 4f; x += 8f)  // the comb along the bottom
                Ui.Fill(new Rect(x, r.yMax - 28f, 2f, 28f), new Color(0.43f, 0.65f, 1f, 0.1f));
            const int dots = 24;
            for (int i = 0; i < dots; i++)
            {
                float y = Mathf.Lerp(r.y + 30f, r.yMax - 30f, i / (dots - 1f));
                float since = age - (0.35f + i * 0.038f);
                Color c = since < 0f && !calm ? new Color(0.14f, 0.21f, 0.4f) : (since < 0.3f && !calm ? new Color(0.72f, 1f, 0.83f) : new Color(0.24f, 0.38f, 0.72f));
                Ui.Fill(new Rect(r.xMax - 11f, y - 2.5f, 5f, 5f), c);
            }
        }

        /// A physical key on the chassis (Back): the tactile blue key with a lamp. Returns true on a click.
        public static bool PhysicalKey(Rect r, string text, bool calm)
        {
            return KeyUi.Key(r, text, null, 20);
        }
    }
}
