using UnityEngine;

namespace Arena
{
    /// The lobby loadout's carousel rows (Class, Weapon, Ability, Ultimate; the Modifiers tab's tier rows use them too): one long row of keys that slides sideways, a slim
    /// arrow key at each end, the mouse wheel over the row, and it stops at its ends, where the arrow on that side greys out. The picked key
    /// is scrolled into view when the pick changes. Use: `BeginCarousel` (clips to the row's window), draw slots `from..to` (key
    /// `CarIndex(j)`) at `CarItem(j)`, then `EndCarousel`. Preview `CAROUSEL=row,first` (mode lobby) scrolls a row.
    public partial class ArenaApp
    {
        public const int CarClass = 0, CarWeapon = 1, CarAbility = 2, CarUlt = 3, CarModEasier = 4, CarRows = 7;  // 4-6: the Modifiers tab's tier rows (ModifiersUi.cs)
        const float CarKeyY = 31f, CarKeyH = 36f, CarRowStep = 74f;  // a row's keys under its heading, and one carousel row to the next
        /// The Role row to the Class heading: the same step as the carousels', so every section is the same distance above the next.
        const float RoleRowStep = CarRowStep;
        /// The loadout's grid: the right column's first heading (Role) is LoadoutTop below the body's top (just above it), so its
        /// letters (HeadingInk lower) sit on the preview's top, PreviewTop down. The preview ends on the Ultimate keys' bottom
        /// (PreviewEnd), a separator runs across the gap (SeparatorY), and the class title and detail box start BottomTop down.
        const float LoadoutTop = -1f, HeadingInk = 17f, LoadoutRows = 5f, PreviewTop = 16f, BottomTop = 387f;
        const float PreviewEnd = LoadoutTop + RoleRowStep + (LoadoutRows - 2f) * CarRowStep + CarKeyY + CarKeyH - 1f;  // (its 1.5 px frame line centred on the keys' bottom edge)
        const float SeparatorY = (PreviewEnd + 6f + BottomTop) / 2f;  // (between the Ultimate keys and the boxes)
        const int ClassesInView = 4, KeysInView = 4;   // keys in view on every row (the same size as the class keys)
        /// Which rows scroll: only Class for now. Weapon, Ability and Ultimate are fixed rows of four (they never have more);
        /// set one true to give it the arrows and the wheel again.
        static readonly bool[] CarScrolls = { true, false, false, false };
        /// The detail box's role and class pages (`detailSel`; 0-2 are the weapon, ability and ultimate).
        public const int DetailRole = 3, DetailClass = 4;
        const float CarArrowW = 18f, CarGap = 6f, CarPad = 4f;  // CarPad: room above and below the keys for their glow

        readonly int[] carFirst = new int[CarRows];    // the first key in view (the target)
        readonly float[] carPos = new float[CarRows];  // where the row is drawn (slides to carFirst)
        readonly int[] carSeen = { -2, -2, -2, -2, -2, -2, -2 };   // the pick last scrolled into view
        int carRow;
        Rect carView;
        float carCell, carH;
        bool carTipWas;
        Rect carTipPrev;

        /// Starts carousel `row` in r: `count` keys, `visible` at a time, `sel` the picked one (-1: none). The row stops at its ends:
        /// the arrow on that side greys out and the wheel goes no further. Gives the keys to draw (`from..to`, at `CarItem(i)`).
        /// `scroll` false: a fixed row (no arrows, no wheel) with its keys where a scrolling row's would be.
        void BeginCarousel(int row, Rect r, int count, int visible, int sel, out int from, out int to, bool scroll = true)
        {
            var ev = Event.current;
            bool calm = fxReduced;
            int maxFirst = scroll ? Mathf.Max(0, count - visible) : 0;
            if (sel != carSeen[row])
            {
                bool first = carSeen[row] == -2;
                carSeen[row] = sel;
                if (sel >= 0 && sel < carFirst[row])  // scroll the pick into view
                    carFirst[row] = sel;
                else if (sel >= carFirst[row] + visible)
                    carFirst[row] = sel - visible + 1;
                if (first)
                    carPos[row] = Mathf.Clamp(carFirst[row], 0, maxFirst);  // (no slide on the lobby's first frame)
            }
            if (ev.type == EventType.ScrollWheel && GUI.enabled && r.Contains(ev.mousePosition) && maxFirst > 0)
            {
                carFirst[row] += ev.delta.y > 0f ? 1 : -1;
                ev.Use();
            }
            carFirst[row] = Mathf.Clamp(carFirst[row], 0, maxFirst);
            var view = new Rect(r.x + CarArrowW + CarGap, r.y, r.width - 2f * (CarArrowW + CarGap), r.height);
            for (int side = -1; side <= 1 && scroll; side += 2)  // a floating arrow (no key box) that fades almost out at its end of the row
            {
                var ab = new Rect(side < 0 ? r.x : r.xMax - CarArrowW, r.y, CarArrowW, r.height);
                bool can = side < 0 ? carFirst[row] > 0 : carFirst[row] < maxFirst;
                bool hover = can && GUI.enabled && ab.Contains(ev.mousePosition);
                if (ev.type == EventType.Repaint)
                    FloatArrow(ab.center, side, can ? (hover ? Color.white : CrtStyle.Ink.WithAlpha(0.75f)) : CrtStyle.Dim.WithAlpha(0.25f), hover);
                if (Ui.HitArea(ab) && can)
                    carFirst[row] += side;
            }
            carFirst[row] = Mathf.Clamp(carFirst[row], 0, maxFirst);
            if (ev.type == EventType.Repaint)
            {
                carPos[row] += (carFirst[row] - carPos[row]) * (calm ? 1f : 1f - Mathf.Exp(-Time.unscaledDeltaTime * 14f));
                if (Mathf.Abs(carFirst[row] - carPos[row]) < 0.004f)
                    carPos[row] = carFirst[row];
            }
            float pos = carPos[row];
            carCell = (view.width - CarGap * (visible - 1)) / visible;
            carH = view.height;
            carRow = row;
            carView = view;
            carTipWas = lockedTipOn;
            carTipPrev = lockedTip;
            GUI.BeginGroup(new Rect(view.x, view.y - CarPad, view.width, view.height + 2f * CarPad));
            from = Mathf.Max(0, Mathf.FloorToInt(pos));
            to = Mathf.Min(count - 1, Mathf.CeilToInt(pos + visible) - 1);  // (one more while it slides; a fixed row: the first `visible`)
        }

        /// A carousel's arrow: a bare pixel chevron, 2 px strokes, pointing left (dir -1) or right (1); `glow` adds a soft halo (hover).
        static void FloatArrow(Vector2 c, int dir, Color col, bool glow)
        {
            if (glow)
                Ui.Glow(new Rect(c.x - 5f, c.y - 8f, 10f, 16f), col, 0.25f);
            for (int i = 0; i < 7; i++)
            {
                float off = (3 - Mathf.Abs(i - 3)) * 2f * dir;
                Ui.Fill(new Rect(Mathf.Round(c.x + off - 3f * dir - 1f), Mathf.Round(c.y - 7f + i * 2f), 2f, 2f), col);
                Ui.Fill(new Rect(Mathf.Round(c.x + off - 3f * dir - 1f + dir), Mathf.Round(c.y - 7f + i * 2f), 2f, 2f), col);
            }
        }

        /// The key in slot j (the rows don't loop: slot j is key j).
        static int CarIndex(int j) => j;

        /// Key i's place in the open carousel (inside its clip).
        Rect CarItem(int i) => new Rect(Mathf.Round((i - carPos[carRow]) * (carCell + CarGap)), CarPad, Mathf.Round(carCell), carH);

        void EndCarousel()
        {
            GUI.EndGroup();
            bool moved = lockedTip.x != carTipPrev.x || lockedTip.y != carTipPrev.y;
            if (lockedTipOn && (!carTipWas || moved))  // a locked key's tip was set inside the clip: back to screen space
            {
                lockedTip.x += carView.x;
                lockedTip.y += carView.y - CarPad;
            }
        }
    }
}
