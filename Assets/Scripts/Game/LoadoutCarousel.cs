using UnityEngine;

namespace Arena
{
    /// The lobby loadout's carousel rows (Class, Weapon, Ability, Ultimate): one long row of keys that slides sideways, a slim
    /// arrow key at each end, the mouse wheel over the row, and a thin track under it showing where the window is. The picked key
    /// is scrolled into view when the pick changes. Use: `BeginCarousel` (clips to the row's window), draw keys `from..to` at
    /// `CarItem(i)`, then `EndCarousel`. Preview `CAROUSEL=row,first` (mode lobby) scrolls a row.
    public partial class ArenaApp
    {
        public const int CarClass = 0, CarWeapon = 1, CarAbility = 2, CarUlt = 3, CarRows = 4;
        const float CarKeyY = 31f, CarKeyH = 36f, CarRowStep = 74f;  // a row's keys under its heading, and one row to the next
        /// The loadout's grid: the right column's first heading (Role) is LoadoutTop below the body's top (just above it), so its
        /// letters (HeadingInk lower) sit on the preview's top, PreviewTop down. The preview ends on the Ultimate keys' bottom
        /// (PreviewEnd), a separator runs across the gap (SeparatorY), and the class title and detail box start BottomTop down.
        const float LoadoutTop = -1f, HeadingInk = 17f, LoadoutRows = 5f, PreviewTop = 16f, BottomTop = 387f;
        const float PreviewEnd = LoadoutTop + (LoadoutRows - 1f) * CarRowStep + CarKeyY + CarKeyH - 1f;  // (its 1.5 px frame line centred on the keys' bottom edge)
        const float SeparatorY = (PreviewEnd + 6f + BottomTop) / 2f;  // (between the carousel track and the boxes)
        const int ClassesInView = 4, KeysInView = 3;   // keys in view: classes (icon over name), weapons / abilities / ultimates
        /// The detail box's role and class pages (`detailSel`; 0-2 are the weapon, ability and ultimate).
        public const int DetailRole = 3, DetailClass = 4;
        /// The role and class keys' hover tips, retired for now (the detail box shows them on a click). False brings them back.
        static readonly bool LoadoutHoverTipsRetired = true;
        const float CarArrowW = 18f, CarGap = 6f, CarPad = 4f;  // CarPad: room above and below the keys for their glow

        readonly int[] carFirst = new int[CarRows];    // the first key in view (the target)
        readonly float[] carPos = new float[CarRows];  // where the row is drawn (slides to carFirst)
        readonly int[] carSeen = { -2, -2, -2, -2 };   // the pick last scrolled into view
        int carRow;
        Rect carView;
        float carCell, carH;
        bool carTipWas;
        Rect carTipPrev;

        /// Starts carousel `row` in r: `count` keys, `visible` at a time, `sel` the picked one (-1: none). Gives the keys to draw.
        void BeginCarousel(int row, Rect r, int count, int visible, int sel, out int from, out int to)
        {
            var ev = Event.current;
            bool calm = fxReduced;
            int maxFirst = Mathf.Max(0, count - visible);
            if (sel != carSeen[row])
            {
                bool first = carSeen[row] == -2;
                carSeen[row] = sel;
                if (sel >= 0 && sel < carFirst[row])
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
            var view = new Rect(r.x + CarArrowW + CarGap, r.y, r.width - 2f * (CarArrowW + CarGap), r.height);
            for (int side = -1; side <= 1; side += 2)
            {
                var ab = new Rect(side < 0 ? r.x : r.xMax - CarArrowW, r.y, CarArrowW, r.height);
                bool can = side < 0 ? carFirst[row] > 0 : carFirst[row] < maxFirst;
                bool click = DkKey(ab, "", false, calm, enabled: can, quiet: !can);
                Chevron(ab.center, side, can ? CrtKit.SoftText(CrtStyle.Ink, GUI.enabled && ab.Contains(ev.mousePosition)) : CrtStyle.Dim.WithAlpha(0.35f));
                if (click && can)
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
            if (maxFirst > 0)  // the track: where the window sits along the whole row
            {
                var tr = new Rect(view.x, view.yMax + 3f, view.width, 2f);
                Ui.Fill(tr, CrtStyle.Ink.WithAlpha(0.1f));
                float tw = tr.width * visible / count;
                Ui.Fill(new Rect(Mathf.Round(tr.x + (tr.width - tw) * pos / maxFirst), tr.y, Mathf.Round(tw), 2f), CrtStyle.Ink.WithAlpha(0.6f));
            }
            carRow = row;
            carView = view;
            carTipWas = lockedTipOn;
            carTipPrev = lockedTip;
            GUI.BeginGroup(new Rect(view.x, view.y - CarPad, view.width, view.height + 2f * CarPad));
            from = Mathf.Max(0, Mathf.FloorToInt(pos));
            to = Mathf.Min(count - 1, Mathf.CeilToInt(pos + visible) - 1);  // (one more while it slides)
        }

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
