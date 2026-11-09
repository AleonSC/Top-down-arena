using UnityEngine;

namespace Arena
{
    /// Eight more frames (ranks 56 to 70, the even ones): Neon Tube, Hazard Tape, Frostbite, Barbed Wire, Lightning Cage,
    /// Pixel March, Blood Drip and Chain Link. Called from DrawFrameFx (FrameLooks.cs), so they share its rules: drawing only,
    /// animated from the clock, nothing at all with Visual effects off (the lit rim in the base colour stays). Built from
    /// IMGUI fills only (no rotation). Mockup: Docs/Mockups/frames-more.html.
    public partial class ArenaApp
    {
        /// A point on the card's outline, d pixels clockwise from the top left, with the outward normal there.
        static Vector2 RimAt(Rect r, float d, out Vector2 n)
        {
            float w = r.width, h = r.height, total = 2f * (w + h);
            d = ((d % total) + total) % total;
            if (d < w) { n = new Vector2(0f, -1f); return new Vector2(r.x + d, r.y); }
            d -= w;
            if (d < h) { n = new Vector2(1f, 0f); return new Vector2(r.xMax, r.y + d); }
            d -= h;
            if (d < w) { n = new Vector2(0f, 1f); return new Vector2(r.xMax - d, r.yMax); }
            d -= w;
            n = new Vector2(-1f, 0f);
            return new Vector2(r.x, r.yMax - d);
        }

        /// A line as a run of small discs (a rotated draw is not worth the trouble for a few pixels).
        static void FrameLine(Vector2 a, Vector2 b, float rad, Color c)
        {
            float len = Vector2.Distance(a, b);
            int n = Mathf.Max(1, (int)Mathf.Ceil(len / 2.5f));
            for (int i = 0; i <= n; i++)
                FrameDot(Mathf.Lerp(a.x, b.x, i / (float)n), Mathf.Lerp(a.y, b.y, i / (float)n), rad, c);
        }

        static void FrameOutlineRect(Rect q, float th, Color c)
        {
            Ui.Fill(new Rect(q.x, q.y, q.width, th), c);
            Ui.Fill(new Rect(q.x, q.yMax - th, q.width, th), c);
            Ui.Fill(new Rect(q.x, q.y + th, th, q.height - 2f * th), c);
            Ui.Fill(new Rect(q.xMax - th, q.y + th, th, q.height - 2f * th), c);
        }

        /// A little snowflake: a plus and a cross of dots, `len` long.
        static void FrameFlake(float x, float y, float len, Color c)
        {
            Ui.Fill(new Rect(x - len, y - 0.6f, len * 2f, 1.2f), c);
            Ui.Fill(new Rect(x - 0.6f, y - len, 1.2f, len * 2f), c);
            float d = len * 0.7f;
            FrameDot(x - d, y - d, 0.8f, c); FrameDot(x + d, y - d, 0.8f, c); FrameDot(x - d, y + d, 0.8f, c); FrameDot(x + d, y + d, 0.8f, c);
        }

        void DrawFrameFxMore(Rect r, string id, float t)
        {
            float w = r.width, h = r.height, total = 2f * (w + h);
            Vector2 n;
            switch (id)
            {
                case "frame-neon":  // a cyan tube inside the magenta one (the rim); the rim's dropout is in FrameTint
                {
                    var cy = new Color(0.43f, 0.94f, 1f, 0.9f);
                    FrameOutlineRect(new Rect(r.x + 4f, r.y + 4f, w - 8f, h - 8f), 1.4f, cy);
                    FrameOutlineRect(new Rect(r.x + 3f, r.y + 3f, w - 6f, h - 6f), 1f, new Color(0.43f, 0.94f, 1f, 0.18f));
                    break;
                }
                case "frame-hazard":  // warning tape: blocks of yellow and black marching clockwise
                {
                    var yel = new Color(0.95f, 0.76f, 0f, 1f);
                    var blk = new Color(0.08f, 0.075f, 0.04f, 1f);
                    float off = t * 14f;
                    for (float d = 8f; d < total - 8f; d += 4f)
                    {
                        var p = RimAt(r, d, out n);
                        bool dark = Mathf.FloorToInt((d + off) / 8f) % 2 == 0;
                        Vector2 q = p + n * 0.5f;
                        bool horizontal = n.y != 0f;
                        var rc = horizontal ? new Rect(q.x - 2f, q.y - (n.y < 0f ? 3f : 1f), 4f, 4f) : new Rect(q.x - (n.x < 0f ? 3f : 1f), q.y - 2f, 4f, 4f);
                        Ui.Fill(rc, dark ? blk : yel);
                    }
                    break;
                }
                case "frame-frost":  // snowflakes that grow and shrink, and a travelling glint
                {
                    for (int i = 0; i < 12; i++)
                    {
                        var p = RimAt(r, FrameHash(i + 3) * total, out n);
                        float grow = 0.5f + 0.5f * Mathf.Sin(t * 0.9f + i * 1.7f);
                        var q = p + n * 2f;
                        FrameFlake(q.x, q.y, 3f + 6f * grow, new Color(0.84f, 0.96f, 1f, 0.55f + 0.4f * grow));
                    }
                    var g = RimAt(r, t * 55f, out n);
                    FrameDot(g.x, g.y, 6f, new Color(1f, 1f, 1f, 0.25f));
                    FrameDot(g.x, g.y, 2.4f, new Color(1f, 1f, 1f, 0.95f));
                    break;
                }
                case "frame-barbed":  // two twisted strands, rusty barbs, one glint
                {
                    for (float d = 0f; d < total; d += 3f)
                    {
                        var p = RimAt(r, d, out n);
                        float a = Mathf.Sin(d / 7f) * 1.9f + 2.2f, b = Mathf.Sin(d / 7f + Mathf.PI) * 1.9f + 2.2f;
                        FrameDot(p.x + n.x * a, p.y + n.y * a, 0.8f, new Color(0.6f, 0.65f, 0.7f, 1f));
                        FrameDot(p.x + n.x * b, p.y + n.y * b, 0.8f, new Color(0.47f, 0.51f, 0.56f, 1f));
                    }
                    var rust = new Color(0.71f, 0.35f, 0.23f, 1f);
                    for (float d = 14f; d < total; d += 31f)
                    {
                        var p = RimAt(r, d, out n) + n * 2.2f;
                        float k = 3f;
                        FrameLine(p + new Vector2(-k, -k), p + new Vector2(k, k), 0.7f, rust);
                        FrameLine(p + new Vector2(k, -k), p + new Vector2(-k, k), 0.7f, rust);
                    }
                    var gl = RimAt(r, t * 70f, out n) + n * 2.2f;
                    FrameDot(gl.x, gl.y, 1.8f, new Color(1f, 1f, 1f, 0.9f));
                    break;
                }
                case "frame-storm":  // corner nodes, and arcs that jump between them along the edge
                {
                    var corners = new[] { new Vector2(r.x, r.y), new Vector2(r.xMax, r.y), new Vector2(r.xMax, r.yMax), new Vector2(r.x, r.yMax) };
                    int seed = Mathf.FloorToInt(t * 12f);
                    for (int i = 0; i < 4; i++)
                    {
                        if (FrameHash(Mathf.FloorToInt(seed * 37 + i * 51)) < 0.42f)
                            continue;
                        Vector2 a = corners[i], b = corners[(i + 1) % 4], dir = (b - a) * (1f / Vector2.Distance(a, b)), perp = new Vector2(-dir.y, dir.x);
                        float len = Vector2.Distance(a, b);
                        int cnt = Mathf.Max(2, (int)(len / 14f));
                        var pts = new Vector2[cnt + 1];
                        pts[0] = a; pts[cnt] = b;
                        for (int k = 1; k < cnt; k++)
                            pts[k] = a + dir * (len * k / cnt) + perp * ((FrameHash(seed * 31 + i * 17 + k) - 0.5f) * 9f);
                        for (int k = 0; k < cnt; k++)
                            FrameLine(pts[k], pts[k + 1], 2.2f, new Color(0.35f, 0.55f, 1f, 0.22f));
                        for (int k = 0; k < cnt; k++)
                            FrameLine(pts[k], pts[k + 1], 0.7f, new Color(0.92f, 0.96f, 1f, 0.95f));
                    }
                    foreach (var c in corners)
                    {
                        FrameDot(c.x, c.y, 7f, new Color(0.47f, 0.67f, 1f, 0.3f));
                        FrameDot(c.x, c.y, 3.4f, new Color(0.82f, 0.9f, 1f, 0.95f));
                    }
                    break;
                }
                case "frame-pixel":  // chunky squares in four colours stepping round the card
                {
                    var pal = new[] { new Color(1f, 0.3f, 0.43f), new Color(1f, 0.82f, 0.4f), new Color(0.02f, 0.84f, 0.63f), new Color(0.3f, 0.79f, 0.94f) };
                    int step = Mathf.FloorToInt(t * 9f), i = 0;
                    for (float d = 0f; d < total; d += 8f, i++)
                    {
                        var p = RimAt(r, d, out n) + n * 2f;
                        Ui.Fill(new Rect(Mathf.Round(p.x / 4f) * 4f - 2f, Mathf.Round(p.y / 4f) * 4f - 2f, 4f, 4f), pal[(((i - step) % 4) + 4) % 4]);
                    }
                    break;
                }
                case "frame-blood":  // beads form along the bottom edge, hang, then drop and fade
                {
                    for (int i = 0; i < 9; i++)
                    {
                        float x = r.x + 30f + FrameHash(i * 7 + 2) * (w - 60f), life = (t * 0.16f + FrameHash(i + 8)) % 1f;
                        float grow = Mathf.Min(1f, life / 0.6f), len = 12f * (1f - (1f - grow) * (1f - grow));
                        Ui.Fill(new Rect(x - 1.3f, r.yMax, 2.6f, len), new Color(0.75f, 0.06f, 0.18f, 0.95f));
                        FrameDot(x, r.yMax + len + 0.5f, 2.5f, new Color(0.84f, 0.12f, 0.24f, 0.95f));
                        if (life > 0.6f)
                        {
                            float f = (life - 0.6f) / 0.4f;
                            FrameDot(x, r.yMax + 12f + f * 14f, 2.2f * (1f - f * 0.3f), new Color(0.84f, 0.12f, 0.24f, 1f - f));
                        }
                    }
                    break;
                }
                case "frame-chain":  // steel links, face-on and end-on, running slowly round
                {
                    float start = (t * 7f) % 11f;
                    int i = 0;
                    for (float d = start; d < total + 11f; d += 11f, i++)
                    {
                        var p = RimAt(r, d, out n) + n * 2.4f;
                        float shine = 0.55f + 0.45f * Mathf.Sin(t * 2f - d / 60f);
                        var steel = new Color(0.78f, 0.83f, 0.9f, 0.7f + 0.3f * shine);
                        bool horizontal = n.y != 0f;
                        if (i % 2 == 0)
                            FrameOutlineRect(horizontal ? new Rect(p.x - 6f, p.y - 3f, 12f, 6f) : new Rect(p.x - 3f, p.y - 6f, 6f, 12f), 1.3f, steel);
                        else
                        {
                            var q = horizontal ? new Rect(p.x - 3f, p.y - 2f, 6f, 4f) : new Rect(p.x - 2f, p.y - 3f, 4f, 6f);
                            Ui.Fill(q, new Color(0.13f, 0.16f, 0.21f, 1f));
                            FrameOutlineRect(q, 1.2f, new Color(0.74f, 0.79f, 0.86f, 0.6f + 0.3f * shine));
                        }
                    }
                    break;
                }
            }
        }
    }
}
