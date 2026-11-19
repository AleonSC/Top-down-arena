using UnityEngine;

namespace Arena
{
    /// Mystery doors (Core/MysteryDoors.cs, Docs/MysteryDoors.md step 3): the sealed door in the wall (the Hologram look), the reveal when one opens (the plate splits and slides into the wall, a ring and a flash), a price tag over each
    /// door and the keys by one (HUD), a violet mark on the minimap and the build map (lime once saved).
    public partial class ArenaApp
    {
        static readonly Rgba MysteryDark = new Rgba(0.06f, 0.03f, 0.08f),
            MysterySaved = new Rgba(0.6f, 1f, 0.35f), MysteryHolo = new Rgba(0.95f, 0.35f, 1f), MysteryHot2 = new Rgba(1f, 0.8f, 1f);

        /// Every sealed door and every reveal under way, over the walls (after DrawGates).
        void DrawMysteryDoors()
        {
            if (!game.ExpandingOn)
                return;
            float t = game.AnimT;
            for (int i = 0; i < game.MysteryDoors.Count; i++)
                DrawMysteryDoor(game.MysteryDoors[i], t, i * 1.7f, 0f);
            foreach (var kv in game.MysteryOpened)
            {
                float age = t - kv.Value;
                if (age >= 0f && age < Game.RevealTime)
                    DrawMysteryReveal(kv.Key, age);
            }
        }

        /// One door: the Hologram look the owner picked (Nov 17), made to stand out from the cyan walls: a dark plate that hides the wall
        /// behind it, a bright magenta-violet field with drifting scan lines, corner brackets that reach past the wall, a soft halo and a
        /// white "?" (lime instead of violet once someone saved it). open (0-1): how far the plate has slid apart (the reveal).
        void DrawMysteryDoor(MysteryDoor d, float t, float seed, float open)
        {
            V2 c = d.Pos, along = d.Vertical ? new V2(0f, 1f) : new V2(1f, 0f), across = d.Vertical ? new V2(1f, 0f) : new V2(0f, 1f);
            float half = Rooms.DoorW / 2f, wt = Rooms.WallT / 2f + 4f;
            float pulse = 0.75f + 0.25f * Mathf.Sin(t * 3f + seed);
            float flick = 0.88f + 0.12f * Mathf.Sin(t * 23f + seed * 3f);
            var ink = d.Saver != 0 ? MysterySaved : MysteryHolo;
            float shut = 1f - open;
            if (open <= 0f)
            {
                SetColor(ink.WithAlpha(0.16f * pulse));  // the halo
                Quad(c - along * (half + 18f) - across * (wt + 18f), c + along * (half + 18f) - across * (wt + 18f), c + along * (half + 18f) + across * (wt + 18f), c - along * (half + 18f) + across * (wt + 18f));
                float b = 14f, ob = wt + 7f;  // the corner brackets, past the wall on both sides
                foreach (int sa in new[] { -1, 1 })
                    foreach (int sc in new[] { -1, 1 })
                    {
                        V2 corner = c + along * (sa * (half + 4f)) + across * (sc * ob);
                        Line(corner, corner - along * (sa * b), 3f, ink.WithAlpha(0.95f));
                        Line(corner, corner - across * (sc * b), 3f, ink.WithAlpha(0.95f));
                    }
            }
            for (int s = -1; s <= 1; s += 2)  // two halves, sliding apart into the wall as it opens
            {
                float lo = s < 0 ? -half - open * half : open * half, hi = s < 0 ? -open * half : half + open * half;
                float clipLo = Mathf.Max(lo, -half), clipHi = Mathf.Min(hi, half);  // (only the part still in the doorway shows)
                if (clipHi <= clipLo)
                    continue;
                V2 a0 = c + along * clipLo, a1 = c + along * clipHi;
                SetColor(MysteryDark.WithAlpha(0.92f));  // (hides the wall's cyan stripes)
                Quad(a0 - across * wt, a1 - across * wt, a1 + across * wt, a0 + across * wt);
                SetColor(ink.WithAlpha(0.32f * flick));
                Quad(a0 - across * wt, a1 - across * wt, a1 + across * wt, a0 + across * wt);
                for (int k = 0; k < 5; k++)  // scan lines drifting along the door
                {
                    float u = Mathf.Repeat(t * 0.35f + k / 5f, 1f);
                    float at = -half + 2f * half * u + (s < 0 ? -open * half : open * half);
                    if (at > clipLo + 1f && at < clipHi - 1f)
                        Line(c + along * at - across * wt, c + along * at + across * wt, 1.5f, MysteryHot2.WithAlpha(0.45f * flick));
                }
                Line(a0 - across * wt, a1 - across * wt, 2.5f, ink.WithAlpha(0.95f));
                Line(a0 + across * wt, a1 + across * wt, 2.5f, ink.WithAlpha(0.95f));
            }
            if (shut <= 0.05f)
                return;
            DrawQuestionMark(c, 15f * shut, ink.WithAlpha(0.6f), 6f);  // (a coloured glow under the white mark)
            DrawQuestionMark(c, 15f * shut, new Rgba(1f, 0.97f, 1f, 0.95f * (0.85f + 0.15f * pulse)), 3f);
        }

        /// A "?" drawn with lines: a hook (an arc), a short stem and a dot.
        void DrawQuestionMark(V2 c, float size, Rgba col, float width)
        {
            float r = size * 0.42f;
            V2 hook = c + new V2(0f, -size * 0.35f);
            Arc(hook, r, width, col, 14, -Mathf.PI * 1.05f, Mathf.PI * 1.55f);
            V2 bend = hook + V2.FromAngle(Mathf.PI * 0.5f) * r;
            Line(bend, bend + new V2(0f, size * 0.32f), width, col);
            Circle(c + new V2(0f, size * 0.62f), width * 0.85f, col, 8);
        }

        /// The reveal: the halves slide apart into the wall, a ring goes out from the doorway and the floor beyond flashes.
        void DrawMysteryReveal(MysteryDoor d, float age)
        {
            float u = Mathf.Clamp01(age / 0.5f);
            float open = 1f - (1f - u) * (1f - u);  // eases out
            if (open < 1f)
                DrawMysteryDoor(d, game.AnimT, 0f, open);
            float k = Mathf.Clamp01(age / Game.RevealTime);
            var ink = MysteryHolo;
            Arc(d.Pos, 30f + 220f * k, 4f * (1f - k) + 1f, ink.WithAlpha(0.8f * (1f - k)), 40);
            if (age < 0.25f)
                Circle(d.Pos, 50f, MysteryHot2.WithAlpha(0.5f * (1f - age / 0.25f)), 20);
        }

        /// DrawHud: a price tag over every door, and by one the keys to open (G) or save (X) it.
        void DrawMysteryHint()
        {
            var me = game.Me;
            if (me == null || !game.ExpandingOn || game.MysteryDoors.Count == 0)
                return;
            int near = me.Downed ? -1 : game.MysteryNear(me.Pos);
            for (int i = 0; i < game.MysteryDoors.Count; i++)
            {
                var d = game.MysteryDoors[i];
                V2 at = ToUi(d.Pos);
                bool saved = d.Saver != 0, mine = d.Saver == game.MyId;
                bool afford = game.SpendableVectors(me) >= d.Price;
                if (i == near)
                {
                    string open = Keybinds.KeyName(Keybinds.Get(KeyAction.TakeItem)).ToUpperInvariant(), save = Keybinds.KeyName(Keybinds.Get(KeyAction.DiscardItem)).ToUpperInvariant();
                    string text = open + "  OPEN  " + d.Price + " VECTORS   ·   " + save + (mine ? "  UNSAVE" : saved ? "  SAVED" : "  SAVE");
                    V2 over = ToUi(me.Disp);  // (over you, not over the door it would hide)
                    var r = new Rect(over.X - 130f, over.Y - 78f, 260f, 22f);
                    Ui.Pill(r, afford ? new Color(0.95f, 0.35f, 1f, 0.92f) : new Color(0f, 0f, 0f, 0.65f));
                    Ui.Label(r, text, 11, afford ? new Color(0.1f, 0.03f, 0.12f) : new Color(1f, 0.45f, 0.42f), TextAnchor.MiddleCenter, true);
                    if (!afford)
                        Ui.Label(new Rect(r.x, r.yMax + 2f, r.width, 14f), "NEED " + (d.Price - game.SpendableVectors(me)) + " MORE", 9, new Color(1f, 0.45f, 0.42f, 0.9f), TextAnchor.MiddleCenter, true);
                }
                else
                {
                    string text = "?  " + d.Price + (saved ? "  · SAVED" : "");
                    var r = new Rect(at.X - 42f, at.Y - 40f, 84f, 18f);
                    Ui.Pill(r, new Color(0f, 0f, 0f, 0.6f));
                    Ui.Label(r, text, 10, saved ? new Color(0.6f, 1f, 0.35f) : afford ? new Color(0.98f, 0.55f, 1f) : new Color(1f, 0.55f, 0.45f, 0.85f), TextAnchor.MiddleCenter, true);
                }
            }
        }

        /// The minimap (RoomsLook.cs): an amber diamond-ish mark on each sealed door (teal once saved).
        void DrawMysteryOnMap(System.Func<V2, V2> M)
        {
            float pulse = 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 3f);
            foreach (var d in game.MysteryDoors)
            {
                V2 c = M(d.Pos);
                if (!InMap(c))
                    continue;
                var ink = d.Saver != 0 ? MysterySaved : MysteryHolo;
                MapRect(c.X - 4.5f, c.Y - 4.5f, 9f, 9f, new Rgba(0.02f, 0.03f, 0.06f, 0.9f));
                MapRect(c.X - 3.5f, c.Y - 3.5f, 7f, 7f, ink.WithAlpha(pulse));
                MapRect(c.X - 1.5f, c.Y - 1.5f, 3f, 3f, new Rgba(0.02f, 0.03f, 0.06f, 0.9f));
            }
        }

        /// The build map (BuildMapLook.cs): a sealed door is an amber bar across its doorway with a diamond, and its price.
        /// `hover`: the door under the mouse (a click buys it, ExpansionUi.cs): bigger, lit, with a ring.
        void DrawHoloMystery(System.Func<V2, V2> M, float k, int hover = -1)
        {
            for (int i = 0; i < game.MysteryDoors.Count; i++)
            {
                var d = game.MysteryDoors[i];
                V2 c = M(d.Pos), al = d.Vertical ? new V2(0f, 1f) : new V2(1f, 0f), ac = new V2(al.Y, al.X);
                float half = Mathf.Max(6f, Rooms.DoorW / 2f * k);
                var ink = i == hover ? MysteryHot2 : d.Saver != 0 ? MysterySaved : MysteryHolo;
                Line(c - al * half, c + al * half, 4f, ink.WithAlpha(0.9f));
                float s = i == hover ? 9f : 6f;
                if (i == hover)
                    Arc(c, 13f + 1.5f * Mathf.Sin(Time.unscaledTime * 6f), 1.5f, MysteryHolo.WithAlpha(0.8f), 24);
                SetColor(ink);
                Quad(c - al * s, c - ac * s, c + al * s, c + ac * s);
                SetColor(MysteryDark);
                Quad(c - al * (s - 2.5f), c - ac * (s - 2.5f), c + al * (s - 2.5f), c + ac * (s - 2.5f));
            }
        }
    }
}
