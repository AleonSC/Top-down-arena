using UnityEngine;

namespace Arena
{
    /// Mending Web's look (MendingWeb.cs), from its mockup (claude.ai artifact "Weaver Web Ability"): a violet spirit spider
    /// with a green cross on its back runs from the first anchor to the second trailing one silk line, then zigzags back
    /// weaving four sagging strands and cross-threads; the built web has a faint green glow along it, dew beads drifting down
    /// the strands, white pads where it sticks to the walls, and a thin green line along its side draining with its time;
    /// the spider rests at the middle, bobbing. It fades out over its last 0.8 s. With Visual effects off: the strands, the
    /// spider and the timer. Drawing only, from the replicated zone (Pos, Aim, Radius, T, Duration).
    /// The pick (your own game, `Game.WebPickSlot`): the reach round you, a gold diamond on the wall under the cursor; with the
    /// first anchor down, a dashed line to the cursor, green with the band's edges when the web would fit, red when not.
    public partial class ArenaApp
    {
        static readonly Rgba WebBody = new Rgba(0.29f, 0.18f, 0.53f), WebHead = new Rgba(0.48f, 0.32f, 0.79f), WebLeg = new Rgba(0.72f, 0.55f, 1f);
        static readonly V2[,] webStrand = new V2[4, 17];
        static readonly float[] WebOffsets = { -1f, -0.4f, 0.4f, 1f };

        static V2 WebQuad(V2 s, V2 m, V2 e, float t)
        {
            float u = 1f - t;
            return s * (u * u) + m * (2f * u * t) + e * (t * t);
        }

        void DrawMendingWeb(Zone z, float t)
        {
            Game.WebEnds(z, out V2 a, out V2 b);
            V2 ab = b - a;
            float len = Mathf.Max(1f, ab.Length), age = z.Duration - z.T;
            V2 u = ab / len, n = new V2(-u.Y, u.X), mid = (a + b) * 0.5f;
            float layT = len / Game.WebLaySpeed;
            float lay = Mathf.Clamp01(age / Mathf.Max(0.01f, layT)), weave = Mathf.Clamp01((age - layT) / Game.WebWeaveT);
            float alpha = Mathf.Clamp01(z.T / 0.8f), from = 1f - weave;
            const int steps = 16;
            for (int k = 0; k < 4; k++)
            {
                float o = WebOffsets[k];
                V2 s = a + n * (o * 12f), e = b + n * (o * 12f), m = mid + n * (o * Game.WebBand * 1.2f);
                for (int i = 0; i <= steps; i++)
                    webStrand[k, i] = WebQuad(s, m, e, i / (float)steps);
            }

            // the first line, out to the spider (and under the weave once it's done)
            Line(a, a + ab * lay, 1.4f, Game.WebColor.WithAlpha(0.8f * alpha));
            if (weave > 0f)
            {
                if (fxOn)
                    Line(a + ab * from, b, Game.WebBand * 2f, Game.WebGreen.WithAlpha(0.07f * alpha));  // the glow
                for (int k = 0; k < 4; k++)  // the strands, woven from the far end back
                {
                    Rgba c = (k == 1 || k == 2 ? Game.WebGreen : Game.WebColor).WithAlpha((k == 1 || k == 2 ? 0.5f : 0.6f) * alpha);
                    for (int i = 0; i < steps; i++)
                    {
                        float t0 = i / (float)steps, t1 = (i + 1f) / steps;
                        if (t1 <= from)
                            continue;
                        V2 p0 = t0 < from ? V2.Lerp(webStrand[k, i], webStrand[k, i + 1], (from - t0) * steps) : webStrand[k, i];
                        Line(p0, webStrand[k, i + 1], 1.1f, c);
                    }
                }
                int cross = Mathf.Max(6, Mathf.RoundToInt(len / 24f));
                Rgba cc = Game.WebColor.WithAlpha(0.38f * alpha);
                for (int i = 1; i < cross; i++)  // the cross-threads, zigzagging strand to strand
                {
                    float tt = i / (float)cross;
                    if (tt < from)
                        continue;
                    V2 prev = WebAt(0, tt);
                    for (int k = 1; k < 4; k++)
                    {
                        V2 next = WebAt(k, Mathf.Min(1f, tt + (k % 2) * 0.5f / cross));
                        Line(prev, next, 0.9f, cc);
                        prev = next;
                    }
                }
                if (fxOn && weave >= 1f)
                    for (int i = 0; i < 12; i++)  // dew beads drifting down the strands
                    {
                        float tt = Mathf.Repeat(i * 0.137f + t * 0.06f + z.Id * 0.31f, 1f);
                        Circle(WebAt(i % 4, tt), 1.6f, Game.WebGreen.WithAlpha(0.85f * alpha), 6);
                    }
            }

            // where it sticks to the walls
            Circle(a, 3f, Game.WebColor.WithAlpha(0.85f * alpha), 8);
            if (lay >= 1f)
                Circle(b, 3f, Game.WebColor.WithAlpha(0.85f * alpha), 8);
            if (weave > 0f)
                for (int k = 0; k < 4; k++)
                {
                    if (from <= 0f)
                        Circle(webStrand[k, 0], 1.8f, Game.WebColor.WithAlpha(0.7f * alpha), 6);
                    Circle(webStrand[k, steps], 1.8f, Game.WebColor.WithAlpha(0.7f * alpha), 6);
                }

            // its time: a thin line along its side
            if (weave >= 1f)
            {
                float left = Mathf.Clamp01(z.T / Mathf.Max(0.01f, z.Duration));
                V2 side = n * -(Game.WebBand + 7f);
                Line(a + side, a + side + ab * left, 2f, Game.WebGreen.WithAlpha(0.55f * alpha));
            }

            // the spider: running out, weaving back, then resting at the middle
            V2 at, face;
            bool busy = true;
            if (lay < 1f)
            {
                at = a + ab * lay;
                face = u;
            }
            else if (weave < 1f)
            {
                int cross = Mathf.Max(6, Mathf.RoundToInt(len / 24f));
                float zz = Mathf.Abs(Mathf.Repeat(from * cross * 1.5f, 2f) - 1f);
                at = V2.Lerp(WebAt(0, from), WebAt(3, from), zz);
                face = -u;
            }
            else
            {
                at = mid + n * (fxOn ? Mathf.Sin(t * 2.2f + z.Id) * 1.5f : 0f);
                face = n;
                busy = false;
            }
            DrawWebSpider(at, face, busy, t, alpha);
        }

        static V2 WebAt(int k, float tt)
        {
            float f = Mathf.Clamp01(tt) * 16f;
            int i = Mathf.Min(15, (int)f);
            return V2.Lerp(webStrand[k, i], webStrand[k, i + 1], f - i);
        }

        void DrawWebSpider(V2 at, V2 face, bool busy, float t, float alpha)
        {
            V2 side = new V2(-face.Y, face.X);
            float wig = fxOn ? Mathf.Sin(t * (busy ? 26f : 4f)) * 0.28f : 0f;
            Rgba leg = WebLeg.WithAlpha(alpha);
            for (int sd = -1; sd <= 1; sd += 2)
                for (int i = 0; i < 4; i++)
                {
                    float ang = -0.95f + i * 0.62f + wig * (i % 2 == 0 ? 1f : -1f);
                    V2 knee = at + side * (sd * Mathf.Cos(ang) * 9f) + face * (Mathf.Sin(ang) * 9f + 2f);
                    V2 foot = at + side * (sd * Mathf.Cos(ang) * 15f) + face * (Mathf.Sin(ang) * 15f - 3f);
                    Line(at, knee, 1.6f, leg);
                    Line(knee, foot, 1.4f, leg);
                }
            V2 back = at - face * 5f;
            Circle(back, 7f, WebBody.WithAlpha(alpha), 14);  // the abdomen
            Circle(at + face * 5f, 4.3f, WebHead.WithAlpha(alpha), 10);  // the head
            Line(back - face * 3.5f, back + face * 3.5f, 2f, Game.WebGreen.WithAlpha(alpha));  // the healer's cross
            Line(back - side * 3f, back + side * 3f, 2f, Game.WebGreen.WithAlpha(alpha));
            Circle(at + face * 7f + side * 1.6f, 0.9f, Rgba.Gold.WithAlpha(alpha), 4);  // eyes
            Circle(at + face * 7f - side * 1.6f, 0.9f, Rgba.Gold.WithAlpha(alpha), 4);
        }

        void WebDiamond(V2 p, float r, Rgba c)
        {
            V2 up = new V2(0f, -r), rt = new V2(r, 0f);
            Line(p + up, p + rt, 2f, c);
            Line(p + rt, p - up, 2f, c);
            Line(p - up, p - rt, 2f, c);
            Line(p - rt, p + up, 2f, c);
        }

        /// Your own game, while Mending Web waits for its anchors.
        void DrawWebPick(Player me, float t)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 8f);
            Arc(me.Disp, Game.WebReach, 1.5f, Game.WebColor.WithAlpha(0.12f + 0.05f * Mathf.Sin(t * 3f)), 96);
            if (!game.WebFirst.HasValue)
            {
                if (game.WebPickAnchor(me, me.AimAt, out WebAnchor hover))
                    WebDiamond(hover.At, 5f + pulse, Rgba.Gold.WithAlpha(0.9f));
                return;
            }
            V2 a = game.WebFirst.Value.At;
            string why = game.WebPickWhy(me, me.AimAt, out WebAnchor b);
            bool ok = why == null;
            V2 end = why == "" ? me.AimAt : b.At;
            Rgba c = ok ? Game.WebGreen : new Rgba(0.78f, 0.3f, 0.34f);  // (CrtStyle.Danger)
            V2 d = end - a;
            float len = d.Length;
            if (len > 1f)
            {
                d /= len;
                for (float s = -Mathf.Repeat(t * 40f, 14f); s < len; s += 14f)  // the dashes creep toward the cursor
                {
                    float s0 = Mathf.Max(0f, s), s1 = Mathf.Min(len, s + 7f);
                    if (s1 > s0)
                        Line(a + d * s0, a + d * s1, 2f, c.WithAlpha(0.85f));
                }
                if (ok)
                {
                    V2 n = new V2(-d.Y, d.X) * Game.WebBand;
                    Line(a + n, end + n, 1f, c.WithAlpha(0.35f));
                    Line(a - n, end - n, 1f, c.WithAlpha(0.35f));
                    Line(a, end, Game.WebBand * 2f, c.WithAlpha(0.06f));
                }
            }
            WebDiamond(a, 6f, Rgba.Gold);
            if (why != "")
                WebDiamond(b.At, 5f + pulse, c.WithAlpha(0.95f));
        }
    }
}
