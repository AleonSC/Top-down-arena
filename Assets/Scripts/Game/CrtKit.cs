using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Shared pieces of the CRT cabinet menus (Docs/Mockups/crt-pause-menu.html, crt-profile.html, crt-lobby-digital.html):
    /// text that backspaces and retypes, the screen power-on (a bright line that opens, then a flash), the boot-log line,
    /// the scan bar, a soft digital key, the floating cabinet chassis and the screen's soft edges. Colours come from CrtUi
    /// (Ink, Dim, Bg). `calm` (Reduced effects) drops the glow, the flash and the typing: everything just shows.
    /// The power-on is a vertical flicker only: no sideways movement, and it never plays twice (the caller keeps an age
    /// that stops at 1 s; after that nothing here animates).
    public static class CrtKit
    {
        public const float EraseLen = 0.24f, TypeLen = 0.4f, PowerLen = 1f;

        /// Key text is drawn at this fraction of the size asked for: the game's font is much bigger than the mockups' pixel font,
        /// and the original menus used small, light text.
        public const float KeyScale = CrtStyle.KeyScale;

        public static int Sz(int s) => Mathf.Max(9, Mathf.RoundToInt(s * KeyScale));

        /// Text colour on a soft key: the accent mixed toward white (more when lit).
        public static Color SoftText(Color c, bool lit) => Color.Lerp(c, Color.white, lit ? CrtStyle.KeyTextMixLit : CrtStyle.KeyTextMix);

        /// The soft key box: a faint tint of the accent and a thin outline; hover brightens it; `on` gets a 2 px outline, a
        /// stronger tint and a glowing underline (no solid fill). locked: dim and dashed.
        public static void SoftBox(Rect r, Color c, bool on, bool hover, bool locked)
        {
            if (locked)
            {
                Ui.Fill(r, new Color(c.r, c.g, c.b, CrtStyle.KeyLockedTint));
                Dashed(r, CrtStyle.KeyLockedDashW, new Color(c.r, c.g, c.b, CrtStyle.KeyLockedDash));
                return;
            }
            // (the strengths and widths: CrtStyle.cs)
            Ui.Fill(r, new Color(c.r, c.g, c.b, on ? CrtStyle.KeyTintOn : hover ? CrtStyle.KeyTintHover : CrtStyle.KeyTint));
            Frame(r, on ? CrtStyle.KeyRimOnW : CrtStyle.KeyRimW, new Color(c.r, c.g, c.b, on ? CrtStyle.KeyRimOn : hover ? CrtStyle.KeyRimHover : CrtStyle.KeyRim));
            if (on)
                Ui.Glow(r, c, CrtStyle.KeyGlowOn);  // the selected key glows a little (no underline)
        }

        // --- Typed text -----------------------------------------------------------------------------------------------

        class Slot
        {
            public string From = "", Target = "";
            public float T0;
        }

        /// While true, `Typed` leaves existing slots alone (the old section keeps drawing its text while it backspaces away).
        public static bool Hold;

        /// Start every slot whose name starts with `prefix` backspacing to nothing.
        public static void EraseAll(string prefix, float now)
        {
            foreach (var kv in slots)
                if (kv.Key.StartsWith(prefix) && kv.Value.Target.Length > 0)
                {
                    kv.Value.From = Visible(kv.Value, now);
                    kv.Value.Target = "";
                    kv.Value.T0 = now;
                }
        }

        /// Make sure the first `count` rows of the cabinet's list have slots, empty, so a list that switches types them all
        /// (a row never drawn before would otherwise just appear).
        public static void SeedRows(int count, float now)
        {
            for (int i = 0; i < count; i++)
                foreach (var sfx in new[] { ".n", ".s" })
                {
                    string k = "cab.r" + i + sfx;
                    if (!slots.ContainsKey(k))
                        slots[k] = new Slot { T0 = now };
                }
        }

        static readonly Dictionary<string, Slot> slots = new Dictionary<string, Slot>();

        /// What to show now for a label that retypes when its text changes: the old text backspaces away over EraseLen,
        /// then the new text types in over TypeLen. A slot seen for the first time types in from nothing (after `delay`
        /// seconds). `slot` names the label (any unique string); calm shows the text as it is.
        public static string Typed(string slot, string target, float now, bool calm, float delay = 0f)
        {
            Slot held;
            if (Hold && !calm && slots.TryGetValue(slot ?? "", out held))
                return Visible(held, now);  // (a section is erasing: show what the slot is doing, whatever it is asked for)
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

        /// A soft digital key (SoftBox): a faint tint and a thin outline, brighter under the mouse, outlined 2 px with an underline when
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
            Color c = CrtStyle.KeyColor(accent, danger, locked);
            bool lit = (hover || on) && !locked;
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
            CrtUi.Text(nr, n, size, tc, align, true, calm);
            if (two)
            {
                string sb = slot != null ? Typed(slot + ".s", sub, now, calm) : sub;
                Ui.Label(new Rect(x, ny + size + 5f, tw, 14f), sb, 11, new Color(tc.r, tc.g, tc.b, 0.75f), align, false);
            }
            return Ui.HitArea(r) && !locked;
        }

        /// The lobby loadout's box (Docs/Mockups/pause-settings-loadout-box.html): a navy fill, a thin blue rim and the blue bracket corners.
        /// The pause, lobby, Settings and How to play menus sit on it, the cabinet's left box and the window to its right alike. Static.
        public static void LoadoutBox(Rect r)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            Ui.Fill(r, CrtUi.T(CrtStyle.BoxFill));  // (the blue-theme colours: CrtStyle.cs; T moves them to the chosen phosphor)
            Ui.Fill(new Rect(r.x, r.y, r.width, r.height * 0.5f), CrtUi.T(CrtStyle.BoxTop));  // a lighter top, like the lobby's wash
            Frame(r, 1f, CrtUi.T(CrtStyle.BoxRim));
            LobbyKit.Corners(r);
        }

        /// The "Soft edges" of the cabinet menus (the mockup's `.edges::after`): the whole screen darkens toward its edges (about 190 px
        /// deep, a black that is solid for the first 24 px and fades out smoothly) and a thin rim runs round the very edge. Draw it over
        /// the menu and under the cursor; `vis` 0..1 fades it with the menu. Static, also with Reduced effects.
        public static void SoftEdges(Rect screen, float vis)
        {
            if (vis <= 0.01f || Event.current.type != EventType.Repaint)
                return;
            const float rim = 7f, line = 2f, step = 6f, reach = 190f, spread = 24f, blur = 80f;
            for (float d = rim + line; d < reach; d += step)
            {
                float z = (spread - (d + step / 2f)) / blur, a = 0.74f / (1f + Mathf.Exp(-1.702f * z));  // a gaussian edge, as the CSS shadow's
                var c = new Color(0f, 0f, 0f, a * vis);
                Ui.Fill(new Rect(screen.x + d, screen.y + d, screen.width - 2f * d, step), c);
                Ui.Fill(new Rect(screen.x + d, screen.yMax - d - step, screen.width - 2f * d, step), c);
                Ui.Fill(new Rect(screen.x + d, screen.y + d + step, step, screen.height - 2f * (d + step)), c);
                Ui.Fill(new Rect(screen.xMax - d - step, screen.y + d + step, step, screen.height - 2f * (d + step)), c);
            }
            Frame(screen, rim, CrtUi.T(new Color(0.07f, 0.125f, 0.247f, vis)));
            Frame(new Rect(screen.x + rim, screen.y + rim, screen.width - 2f * rim, screen.height - 2f * rim), line, new Color(CrtUi.Dim.r, CrtUi.Dim.g, CrtUi.Dim.b, vis));
        }

        /// A physical key on the chassis (Back): the tactile blue key with a lamp. Returns true on a click.
        public static bool PhysicalKey(Rect r, string text)
        {
            return KeyUi.Key(r, text, null, r.height >= 44f ? 20 : 16);
        }
    }
}
