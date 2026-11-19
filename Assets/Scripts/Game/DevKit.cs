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
            danger = Same(tint, CrtStyle.Danger);
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
            var ink = CrtStyle.Ink;
            Color c = tint ?? new Color(0f, 0f, 0f, 0.28f);
            Ui.Fill(r, c);
            CrtKit.Frame(r, 1f, ink.WithAlpha(0.18f));
        }

        /// A heading in glowing phosphor text.
        void DkTitle(Rect r, string s, int size, Color? c = null, TextAnchor a = TextAnchor.MiddleLeft) =>
            CrtUi.Text(r, s, size, c ?? CrtStyle.Ink, a, true, FxCalm);

        /// The one CRT key (soft digital key) every screen uses. Returns true when pressed.
        /// on: lit as the chosen one. calm: Reduced effects (FxCalm). The face fits the text (shrinks to minSize, wraps when
        /// the key is tall enough, then cuts); an icon sits beside it (or above it, `stack`), `sub` adds a small second line,
        /// `lamp` a lamp before the text (lampLit). rawSize: size is the drawn size, not scaled by CrtStyle.KeyScale. States: `locked` dim and dashed (can't be used yet), enabled = false shown
        /// as it is but never hot (a guest viewing the host's pick), `isStatic` a plain label in a key's shape, `quiet` dashed
        /// until hovered. danger: red; accent: any other colour.
        public static bool DkKey(Rect r, string text, bool on, bool calm, Texture2D icon = null, bool stack = false, bool colorIcon = false,
            int size = 18, int minSize = 11, bool danger = false, bool enabled = true, Color? accent = null, string sub = null, bool locked = false,
            bool quiet = false, bool isStatic = false, bool lamp = false, bool lampLit = true, bool rawSize = false)
        {
            if (!rawSize)  // (sizes are scaled by CrtStyle.KeyScale unless given as drawn: the old DkKey and SoftKey sizes)
            {
                size = CrtKit.Sz(size);
                minSize = Mathf.Min(size, CrtKit.Sz(minSize));
            }
            int lampKind = lamp ? (lampLit ? 1 : 2) : 0;
            if (locked)
            {
                var d = CrtStyle.Dim;
                Ui.Fill(r, d.WithAlpha(CrtStyle.KeyWaitTint * CrtKit.FillFade));
                CrtKit.Dashed(r, 1f, d.WithAlpha(CrtStyle.KeyWaitDash));  // faded: nothing to press yet
                LobbyKit.KeyFace(r, text, icon, stack, colorIcon, size, minSize, d.WithAlpha(CrtStyle.KeyWaitText), false, calm, sub, true, lampKind);
                return false;
            }
            bool live = enabled && !isStatic;
            bool hover = live && GUI.enabled && r.Contains(Event.current.mousePosition);
            if (live)
                Ui.NoteHover(r);
            Color c = CrtStyle.KeyColor(accent, danger), tc;
            if (isStatic)
            {
                Ui.Fill(r, c.WithAlpha(CrtStyle.KeyStaticTint));
                CrtKit.Frame(r, 1f, c.WithAlpha(CrtStyle.KeyStaticRim));
                tc = CrtKit.SoftText(c, false);
            }
            else if (quiet && !hover && !on)
            {
                Ui.Fill(r, c.WithAlpha(CrtStyle.KeyQuietTint));
                CrtKit.Dashed(r, CrtStyle.KeyQuietDashW, c.WithAlpha(CrtStyle.KeyQuietDash));
                tc = c.WithAlpha(CrtStyle.KeyQuietText);
            }
            else
            {
                CrtKit.SoftBox(r, c, on, hover, false);  // a faint tint and a thin outline; the chosen key gets an underline glow, not a solid fill
                tc = CrtKit.SoftText(c, hover || on);
            }
            LobbyKit.KeyFace(r, text, icon, stack, colorIcon, size, minSize, tc, false, calm, sub, false, lampKind);
            return live && Ui.HitArea(r);
        }

        /// DkKey with the old tint argument (the Danger tint means red, GoldButton gold, Neutral / Primary the phosphor);
        /// off (GUI.enabled false) draws it locked.
        bool DkKey(Rect r, string text, Color tint, bool sel = false, int size = 13)
        {
            bool danger;
            Color? acc = DkAccent(tint, out danger);
            return DkKey(r, text, sel, FxCalm, size: Mathf.Max(10, size), minSize: 9, danger: danger, accent: acc, locked: !GUI.enabled, rawSize: true);
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
            Color c = locked ? CrtStyle.Dim : CrtStyle.Ink;
            var lamp = new Rect(r.x + 2f, r.y + r.height / 2f - 6f, 12f, 12f);
            if (value && !FxCalm)
                Ui.Fill(new Rect(lamp.x - 3f, lamp.y - 3f, 18f, 18f), c.WithAlpha(0.22f));
            CrtKit.Frame(lamp, 1f, c.WithAlpha(value ? 1f : 0.6f));
            if (value)
                Ui.Fill(new Rect(lamp.x + 3f, lamp.y + 3f, 6f, 6f), CrtKit.SoftText(c, true));
            Color tc = locked ? CrtStyle.Dim : hover ? Color.white : CrtStyle.Body;
            Ui.Label(new Rect(r.x + 24f, r.y, r.width - 24f, r.height), text, 13, tc, TextAnchor.MiddleLeft);
            return !locked && Ui.HitArea(r) ? !value : value;
        }

        /// A text field: a dark well with a thin phosphor rim (brighter while it has focus).
        string DkField(Rect r, string value, int maxLength, int size = 13)
        {
            var ink = CrtStyle.Ink;
            Ui.Fill(r, new Color(0f, 0f, 0f, 0.35f));
            bool focus = r.Contains(Event.current.mousePosition);
            CrtKit.Frame(r, 1f, ink.WithAlpha(focus ? 0.8f : 0.35f));
            return Ui.BareField(new Rect(r.x + 6f, r.y, r.width - 12f, r.height), value, maxLength, size);
        }

        /// A titled card for a tool's content: a faint box with a phosphor-lit left edge, a title and a small line. Returns the y to
        /// draw its contents at.
        float DkCard(Rect r, string title, string blurb, Color accent)
        {
            var ink = CrtStyle.Ink;
            Ui.Fill(r, accent.WithAlpha(0.05f));
            CrtKit.Frame(r, 1f, ink.WithAlpha(0.22f));
            Ui.Fill(new Rect(r.x, r.y + 8f, 3f, 26f), accent);
            DkTitle(new Rect(r.x + 16f, r.y + 8f, r.width - 32f, 22f), title.ToUpperInvariant(), 14, accent);
            if (blurb.Length > 0)
                Ui.Label(new Rect(r.x + 16f, r.y + 30f, r.width - 32f, 18f), blurb, 11, CrtStyle.Desc);
            return r.y + (blurb.Length > 0 ? 56f : 40f);
        }

        /// A card backing: a faint tint of the colour and a thin rim in it (`strength` 0..1 brightens the rim). The CRT look of the old Ui.Card.
        static void DkBox(Rect r, Color accent, float strength)
        {
            strength = Mathf.Clamp01(strength);
            Ui.Fill(r, accent.WithAlpha(0.05f + 0.08f * strength));
            CrtKit.Frame(r, strength > 0.6f ? 2f : 1f, accent.WithAlpha(0.3f + 0.6f * strength));
        }

        /// A translucent in-run panel (player tiles, boss bars, banners): the screen colour at ~88%, a thin rim in the accent (or the phosphor).
        static void DkHud(Rect r, Color? accent = null)
        {
            var ink = CrtStyle.Ink;
            Ui.Fill(r, CrtStyle.Bg.WithAlpha(0.88f));
            Color c = accent ?? ink;
            CrtKit.Frame(r, 1f, c.WithAlpha(accent.HasValue ? 0.7f : 0.28f));
        }

        /// A thin CRT meter: a faint track and a lit fill (a progress or health bar).
        static void DkBar(Rect r, float frac, Color fill, Color? track = null)
        {
            var ink = CrtStyle.Ink;
            Ui.Fill(r, track ?? ink.WithAlpha(0.14f));
            Ui.Fill(new Rect(r.x, r.y, r.width * Mathf.Clamp01(frac), r.height), fill);
        }

        /// A small outlined tag (the old Badge).
        static void DkTag(Rect r, string text, Color c)
        {
            Ui.Fill(r, c.WithAlpha(0.1f));
            CrtKit.Frame(r, 1f, c.WithAlpha(0.7f));
            Ui.Label(r, text, 11, CrtKit.SoftText(c, true), TextAnchor.MiddleCenter, true);
        }
    }
}
