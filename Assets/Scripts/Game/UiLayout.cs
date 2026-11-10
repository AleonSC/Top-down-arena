using System;
using UnityEngine;

namespace Arena
{
    /// A one-direction flow of rects, so a window lays itself out instead of every row being worked out by hand:
    ///   var col = DkStack.Column(body, gap: 8f);
    ///   var title = col.Next(30f);          // the next 30 px, full width
    ///   var cards = col.Next(120f);
    ///   col.Space(12f);                      // an extra gap
    ///   var rest = col.Rest();               // whatever is left
    /// Row works the same sideways (Next takes a width). Change a size and everything after it moves.
    public struct DkStack
    {
        readonly Rect area;
        readonly bool horizontal;
        readonly float gap;
        float pos;

        DkStack(Rect area, bool horizontal, float gap)
        {
            this.area = area;
            this.horizontal = horizontal;
            this.gap = gap;
            pos = 0f;
        }

        public static DkStack Column(Rect area, float gap = 8f) => new DkStack(area, false, gap);
        public static DkStack Row(Rect area, float gap = 8f) => new DkStack(area, true, gap);

        /// How much of the area is used so far (including the gaps).
        public float Used => Mathf.Max(0f, pos - gap);

        /// The next `size` along the flow (full across it), then a gap.
        public Rect Next(float size)
        {
            Rect r = horizontal ? new Rect(area.x + pos, area.y, size, area.height) : new Rect(area.x, area.y + pos, area.width, size);
            pos += size + gap;
            return r;
        }

        /// Extra space with no rect.
        public void Space(float size) => pos += size;

        /// Everything that's left.
        public Rect Rest()
        {
            return horizontal ? new Rect(area.x + pos, area.y, Mathf.Max(0f, area.width - pos), area.height)
                : new Rect(area.x, area.y + pos, area.width, Mathf.Max(0f, area.height - pos));
        }

        /// `n` equal cells across the area with a gap between (a row of keys, a strip of tiles).
        public static Rect[] Cells(Rect area, int n, float gap = 8f)
        {
            var cells = new Rect[n];
            float w = (area.width - gap * (n - 1)) / Mathf.Max(1, n);
            for (int i = 0; i < n; i++)
                cells[i] = new Rect(area.x + i * (w + gap), area.y, w, area.height);
            return cells;
        }

        /// `n` cells in a grid of `cols` columns, each `cellH` tall.
        public static Rect[] Grid(Rect area, int n, int cols, float cellH, float gap = 6f)
        {
            var cells = new Rect[n];
            float w = (area.width - gap * (cols - 1)) / cols;
            for (int i = 0; i < n; i++)
                cells[i] = new Rect(area.x + (i % cols) * (w + gap), area.y + (i / cols) * (cellH + gap), w, cellH);
            return cells;
        }
    }

    public partial class ArenaApp
    {
        /// The shell of a tool window: the dimmed screen behind, the CRT box, a title with a small line under it, an X key (calls
        /// `onClose`) and a rule. Returns the area inside for the body (below the header, 20 px in from the sides).
        /// `accent` colours the title; `dim` is how dark the screen behind goes.
        Rect DkWindow(Rect panel, string title, string sub, Action onClose, Color? accent = null, float dim = 0.55f)
        {
            Ui.Fill(new Rect(0, 0, W, H), new Color(0.01f, 0.01f, 0.03f, dim));
            DkPanel(panel);
            DkTitle(new Rect(panel.x + 24f, panel.y + 14f, panel.width - 120f, 36f), title, 26, accent);
            if (!string.IsNullOrEmpty(sub))
                Ui.Label(new Rect(panel.x + 26f, panel.y + 48f, panel.width - 120f, 16f), sub, 11, CrtStyle.Desc);
            if (DkKey(new Rect(panel.xMax - 52f, panel.y + 16f, 32f, 30f), "X", Ui.Danger) && onClose != null)
                onClose();
            Ui.Fill(new Rect(panel.x + 20f, panel.y + 74f, panel.width - 40f, 1f), new Color(1f, 1f, 1f, 0.07f));
            return new Rect(panel.x + 20f, panel.y + 90f, panel.width - 40f, panel.height - 110f);
        }
    }
}
