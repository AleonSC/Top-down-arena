using UnityEngine;

namespace Arena
{
    /// The CRT look for the developer tools and the chat box (F1 menu and its tabs, the balance report, chat): drop-ins for the old
    /// IMGUI widgets (`Ui.Panel`, `Button`, `Toggle`, `Choice`, `Field`, `Pill`, `Fancy`), drawn with the CRT key and box pieces
    /// (`CrtKit`, `CrtStyle`) so they follow the chosen phosphor and Reduced effects. A tint passed to `DkKey` picks the accent:
    /// the old Danger tint means red, GoldButton gold, Neutral / Primary the phosphor.
    public partial class ArenaApp
    {
        static bool Same(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.002f;

        /// The accent a legacy tint stands for (null = the phosphor).
        static Color? DkAccent(Color tint, out bool danger)
        {
            danger = Same(tint, Ui.Danger);
            if (danger)
                return null;
            if (Same(tint, Ui.Neutral) || Same(tint, Ui.Primary))
                return null;
            if (Same(tint, Ui.GoldButton))
                return CrtStyle.Gold;
            return tint;
        }

        /// The big dark box of a tool window: the loadout box (navy, thin rim, bracket corners).
        static void DkPanel(Rect r) => CrtKit.LoadoutBox(r);

        /// A recessed area inside a box (a list, a tile, a status card): a faint tint and a thin dim rim.
        static void DkSub(Rect r, Color? tint = null)
        {
            var ink = CrtUi.Ink;
            Color c = tint ?? new Color(0f, 0f, 0f, 0.28f);
            Ui.Fill(r, c);
            CrtKit.Frame(r, 1f, new Color(ink.r, ink.g, ink.b, 0.18f));
        }

        /// A heading in glowing phosphor text.
        void DkTitle(Rect r, string s, int size, Color? c = null, TextAnchor a = TextAnchor.MiddleLeft) =>
            CrtUi.Text(r, s, size, c ?? CrtUi.Ink, a, true, CabCalm);

        /// A soft digital key. `sel` outlines it as the chosen one. Returns true when pressed.
        bool DkKey(Rect r, string text, Color tint, bool sel = false, int size = 13)
        {
            bool danger;
            Color? acc = DkAccent(tint, out danger);
            bool locked = !GUI.enabled;
            bool hover = !locked && r.Contains(Event.current.mousePosition);
            Ui.NoteHover(r);
            Color c = CrtStyle.KeyColor(acc, danger, locked);
            CrtKit.SoftBox(r, c, sel, hover, locked);
            Color tc = CrtKit.SoftText(c, hover || sel);
            int sz = Mathf.Max(10, size);
            while (sz > 9 && Ui.TextWidth(text, sz, true) > r.width - 8f)
                sz--;  // long names shrink to fit
            CrtUi.Text(new Rect(r.x + 4f, r.y, r.width - 8f, r.height), text, sz, tc, TextAnchor.MiddleCenter, true, CabCalm);
            return !locked && Ui.HitArea(r);
        }

        bool DkKey(Rect r, string text, bool sel = false, int size = 13) => DkKey(r, text, Ui.Neutral, sel, size);

        /// The old Choice: a key that's lit while chosen.
        bool DkChoice(Rect r, string text, bool selected, Color accent) => DkKey(r, text, accent, selected, 13);

        /// A switch: a lamp (lit when on) and its label; the whole row is the hit area. Returns the new value.
        bool DkSwitch(Rect r, bool value, string text)
        {
            bool locked = !GUI.enabled;
            bool hover = !locked && r.Contains(Event.current.mousePosition);
            Ui.NoteHover(r);
            Color c = locked ? CrtUi.Dim : CrtUi.Ink;
            var lamp = new Rect(r.x + 2f, r.y + r.height / 2f - 6f, 12f, 12f);
            if (value && !CabCalm)
                Ui.Fill(new Rect(lamp.x - 3f, lamp.y - 3f, 18f, 18f), new Color(c.r, c.g, c.b, 0.22f));
            CrtKit.Frame(lamp, 1f, new Color(c.r, c.g, c.b, value ? 1f : 0.6f));
            if (value)
                Ui.Fill(new Rect(lamp.x + 3f, lamp.y + 3f, 6f, 6f), CrtKit.SoftText(c, true));
            Color tc = locked ? CrtUi.Dim : hover ? Color.white : CrtStyle.Body;
            Ui.Label(new Rect(r.x + 24f, r.y, r.width - 24f, r.height), text, 13, tc, TextAnchor.MiddleLeft);
            return !locked && Ui.HitArea(r) ? !value : value;
        }

        /// A text field: a dark well with a thin phosphor rim (brighter while it has focus).
        string DkField(Rect r, string value, int maxLength, int size = 13)
        {
            var ink = CrtUi.Ink;
            Ui.Fill(r, new Color(0f, 0f, 0f, 0.35f));
            bool focus = r.Contains(Event.current.mousePosition);
            CrtKit.Frame(r, 1f, new Color(ink.r, ink.g, ink.b, focus ? 0.8f : 0.35f));
            return Ui.BareField(new Rect(r.x + 6f, r.y, r.width - 12f, r.height), value, maxLength, size);
        }

        /// A titled card for a tool's content: a faint box with a phosphor-lit left edge, a title and a small line. Returns the y to
        /// draw its contents at.
        float DkCard(Rect r, string title, string blurb, Color accent)
        {
            var ink = CrtUi.Ink;
            Ui.Fill(r, new Color(accent.r, accent.g, accent.b, 0.05f));
            CrtKit.Frame(r, 1f, new Color(ink.r, ink.g, ink.b, 0.22f));
            Ui.Fill(new Rect(r.x, r.y + 8f, 3f, 26f), accent);
            DkTitle(new Rect(r.x + 16f, r.y + 8f, r.width - 32f, 22f), title.ToUpperInvariant(), 14, accent);
            if (blurb.Length > 0)
                Ui.Label(new Rect(r.x + 16f, r.y + 30f, r.width - 32f, 18f), blurb, 11, CrtStyle.Desc);
            return r.y + (blurb.Length > 0 ? 56f : 40f);
        }

        /// A thin CRT meter: a faint track and a lit fill (a progress or health bar).
        static void DkBar(Rect r, float frac, Color fill, Color? track = null)
        {
            var ink = CrtUi.Ink;
            Ui.Fill(r, track ?? new Color(ink.r, ink.g, ink.b, 0.14f));
            Ui.Fill(new Rect(r.x, r.y, r.width * Mathf.Clamp01(frac), r.height), fill);
        }

        /// A small outlined tag (the old Badge).
        static void DkTag(Rect r, string text, Color c)
        {
            Ui.Fill(r, new Color(c.r, c.g, c.b, 0.1f));
            CrtKit.Frame(r, 1f, new Color(c.r, c.g, c.b, 0.7f));
            Ui.Label(r, text, 11, CrtKit.SoftText(c, true), TextAnchor.MiddleCenter, true);
        }
    }
}
