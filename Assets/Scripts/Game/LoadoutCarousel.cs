using UnityEngine;

namespace Arena
{
    /// The lobby loadout's carousel rows (Class, Weapon, Ability, Ultimate): one long row of keys that slides sideways, a slim
    /// arrow key at each end, the mouse wheel over the row, and a thin track under it showing where the window is. It loops. The picked key
    /// is scrolled into view when the pick changes. Use: `BeginCarousel` (clips to the row's window), draw slots `from..to` (key
    /// `CarIndex(j)`) at `CarItem(j)`, then `EndCarousel`. Preview `CAROUSEL=row,first` (mode lobby) scrolls a row.
    public partial class ArenaApp
    {
        public const int CarClass = 0, CarWeapon = 1, CarAbility = 2, CarUlt = 3, CarRows = 4;
        const float CarKeyY = 31f, CarKeyH = 36f, CarRowStep = 74f;  // a row's keys under its heading, and one carousel row to the next
        /// The Role row (no track under it) to the Class heading: the carousels' step less their track (3 px gap + 2 px), so every
        /// section's last line is the same distance above the next heading.
        const float RoleRowStep = CarRowStep - 5f;
        /// The loadout's grid: the right column's first heading (Role) is LoadoutTop below the body's top (just above it), so its
        /// letters (HeadingInk lower) sit on the preview's top, PreviewTop down. The preview ends on the Ultimate keys' bottom
        /// (PreviewEnd), a separator runs across the gap (SeparatorY), and the class title and detail box start BottomTop down.
        const float LoadoutTop = -1f, HeadingInk = 17f, LoadoutRows = 5f, PreviewTop = 16f, BottomTop = 387f;
        const float PreviewEnd = LoadoutTop + RoleRowStep + (LoadoutRows - 2f) * CarRowStep + CarKeyY + CarKeyH - 1f;  // (its 1.5 px frame line centred on the keys' bottom edge)
        const float SeparatorY = (PreviewEnd + 6f + BottomTop) / 2f;  // (between the Ultimate row's track and the boxes)
        const int ClassesInView = 4, KeysInView = 4;   // keys in view on every row (the same size as the class keys)
        /// The detail box's role and class pages (`detailSel`; 0-2 are the weapon, ability and ultimate).
        public const int DetailRole = 3, DetailClass = 4;
        /// The role and class keys' hover tips, retired for now (the detail box shows them on a click). False brings them back.
        static readonly bool LoadoutHoverTipsRetired = true;
        const float CarArrowW = 18f, CarGap = 6f, CarPad = 4f, CarTrackH = 2f;  // CarPad: room above and below the keys for their glow

        readonly int[] carFirst = new int[CarRows];    // the first key in view (the target)
        readonly float[] carPos = new float[CarRows];  // where the row is drawn (slides to carFirst)
        readonly int[] carSeen = { -2, -2, -2, -2 };   // the pick last scrolled into view
        int carRow;
        Rect carView;
        float carCell, carH;
        bool carTipWas;
        Rect carTipPrev;

        /// Starts carousel `row` in r: `count` keys, `visible` at a time, `sel` the picked one (-1: none). The row loops: it scrolls
        /// on past the last key round to the first, either way. Gives the slots to draw (`from..to`; `CarIndex(j)` is slot j's key).
        void BeginCarousel(int row, Rect r, int count, int visible, int sel, out int from, out int to)
        {
            var ev = Event.current;
            bool calm = fxReduced;
            carCount = Mathf.Max(1, count);
            bool loops = count > 1;
            if (sel != carSeen[row])
            {
                bool first = carSeen[row] == -2;
                carSeen[row] = sel;
                if (sel >= 0)  // scroll the pick into view, the short way round
                {
                    int d = CarMod(sel - carFirst[row]);
                    if (d >= visible)
                    {
                        int fwd = d - visible + 1, back = carCount - d;
                        carFirst[row] += fwd <= back ? fwd : -back;
                    }
                }
                if (first)
                    carPos[row] = carFirst[row];  // (no slide on the lobby's first frame)
            }
            if (ev.type == EventType.ScrollWheel && GUI.enabled && r.Contains(ev.mousePosition) && loops)
            {
                carFirst[row] += ev.delta.y > 0f ? 1 : -1;
                ev.Use();
            }
            var view = new Rect(r.x + CarArrowW + CarGap, r.y, r.width - 2f * (CarArrowW + CarGap), r.height);
            for (int side = -1; side <= 1; side += 2)
            {
                var ab = new Rect(side < 0 ? r.x : r.xMax - CarArrowW, r.y, CarArrowW, r.height);
                bool click = DkKey(ab, "", false, calm, enabled: loops, quiet: !loops);
                Chevron(ab.center, side, loops ? CrtKit.SoftText(CrtStyle.Ink, GUI.enabled && ab.Contains(ev.mousePosition)) : CrtStyle.Dim.WithAlpha(0.35f));
                if (click && loops)
                    carFirst[row] += side;
            }
            if (ev.type == EventType.Repaint)
            {
                carPos[row] += (carFirst[row] - carPos[row]) * (calm ? 1f : 1f - Mathf.Exp(-Time.unscaledDeltaTime * 14f));
                if (Mathf.Abs(carFirst[row] - carPos[row]) < 0.004f)
                    carPos[row] = carFirst[row];
                if (Mathf.Abs(carFirst[row]) > 600 && carFirst[row] == carPos[row])  // keep the numbers small
                {
                    int sub = carFirst[row] / carCount * carCount;
                    carFirst[row] -= sub;
                    carPos[row] -= sub;
                }
            }
            float pos = carPos[row];
            carCell = (view.width - CarGap * (visible - 1)) / visible;
            carH = view.height;
            // The track: where the window sits round the loop (the thumb wraps from the right end to the left).
            var tr = new Rect(view.x, view.yMax + 3f, view.width, CarTrackH);
            Ui.Fill(tr, CrtStyle.Ink.WithAlpha(0.1f));
            float tw = Mathf.Round(tr.width * Mathf.Min(1f, visible / (float)carCount));
            float tx = tr.width * (((pos % carCount) + carCount) % carCount) / carCount;
            var thumb = CrtStyle.Ink.WithAlpha(0.6f);
            float right = Mathf.Min(tw, tr.width - tx);
            Ui.Fill(new Rect(Mathf.Round(tr.x + tx), tr.y, Mathf.Round(right), CarTrackH), thumb);
            if (right < tw)
                Ui.Fill(new Rect(tr.x, tr.y, Mathf.Round(tw - right), CarTrackH), thumb);
            carRow = row;
            carView = view;
            carTipWas = lockedTipOn;
            carTipPrev = lockedTip;
            GUI.BeginGroup(new Rect(view.x, view.y - CarPad, view.width, view.height + 2f * CarPad));
            from = Mathf.FloorToInt(pos);
            to = Mathf.CeilToInt(pos + visible) - 1;  // (one more while it slides)
        }

        int carCount = 1;
        int CarMod(int j) => ((j % carCount) + carCount) % carCount;
        /// The key in slot j of the open carousel (slots run on forever; keys repeat round the loop).
        int CarIndex(int j) => CarMod(j);

        /// Slot j's place in the open carousel (inside its clip).
        Rect CarItem(int j) => new Rect(Mathf.Round((j - carPos[carRow]) * (carCell + CarGap)), CarPad, Mathf.Round(carCell), carH);

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
