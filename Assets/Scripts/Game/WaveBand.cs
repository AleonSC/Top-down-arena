using UnityEngine;

namespace Arena
{
    /// The scan band (option B of Docs/Mockups/crt-wave-banners.html): a full-width CRT strip with a log line each side, a
    /// big glowing title, a small line under it and an optional countdown bar. GET READY and the between-waves banners
    /// (Hud.DrawBanner) and the WAVE N STARTED call as a fight begins (LaunchIntro.DrawLaunchWave) both draw it.
    public partial class ArenaApp
    {
        static readonly Rect BandRect = new Rect(0f, H / 2f - 130f, W, 170f);

        // DrawBanner's state: when the banner appeared (it opens like a CRT line) and the longest countdown seen (for the bar)
        float bandShownAt = -10f, bandLastDraw = -10f, bandMax = 1f;
        int bandWave = -1;

        /// The strip. open 0-1 grows it from a line; inverse flips it to a solid block with dark text (the start of a wave);
        /// bar 0-1 fills the countdown bar along the foot, below 0 hides it.
        void DrawScanBand(string title, string sub, string left, string right, float bar, Color ink, float open, bool inverse)
        {
            if (open <= 0f)
                return;
            bool calm = CabCalm;
            float oh = Mathf.Ceil(open * 8f) / 8f;  // opens in eight steps, like a tube warming up
            float h = BandRect.height * oh;
            var r = new Rect(BandRect.x, BandRect.center.y - h / 2f, BandRect.width, h);
            var dark = CrtUi.Bg;

            // the strip: solid in the middle, fading out toward both screen edges
            const int fade = 10;
            float edge = W * 0.12f, sw = edge / fade;
            Color body = inverse ? ink : new Color(dark.r, dark.g, dark.b, 0.94f);
            Ui.Fill(new Rect(edge, r.y, W - 2f * edge, r.height), body);
            for (int i = 0; i < fade; i++)
            {
                var c = new Color(body.r, body.g, body.b, body.a * (i + 1f) / (fade + 1f));
                Ui.Fill(new Rect(i * sw, r.y, sw + 1f, r.height), c);
                Ui.Fill(new Rect(W - (i + 1) * sw - 1f, r.y, sw + 1f, r.height), c);
            }
            var rim = new Color(ink.r, ink.g, ink.b, 0.55f);
            Ui.Fill(new Rect(edge * 0.4f, r.y, W - edge * 0.8f, 1f), rim);
            Ui.Fill(new Rect(edge * 0.4f, r.yMax - 1f, W - edge * 0.8f, 1f), rim);
            if (oh < 1f)
                return;  // (text waits until it's fully open)

            // the sweep: a faint bright line falling through the strip
            if (!calm && !inverse)
            {
                float y = r.y + (Time.unscaledTime / 1.6f % 1f) * r.height;
                Ui.Fill(new Rect(edge, y - 3f, W - 2f * edge, 6f), new Color(ink.r, ink.g, ink.b, 0.10f));
                Ui.Fill(new Rect(edge, y - 1f, W - 2f * edge, 2f), new Color(ink.r, ink.g, ink.b, 0.22f));
            }

            bool was = Ui.CrtFont;
            Ui.CrtFont = true;
            Color tc = inverse ? dark : CrtUi.T(new Color(0.84f, 0.9f, 1f)), dim = inverse ? dark : CrtUi.Dim;
            if (ink.r > ink.b)  // a gold (boss) strip keeps a warm white rather than the blue one
                tc = inverse ? dark : new Color(1f, 0.94f, 0.78f);
            float side = 90f;
            if (left.Length > 0)
                Ui.Label(new Rect(side, r.y + 14f, 380f, 44f), left, 15, dim, TextAnchor.UpperLeft, false);
            if (right.Length > 0)
                Ui.Label(new Rect(W - side - 380f, r.y + 14f, 380f, 44f), right, 15, dim, TextAnchor.UpperRight, false);
            var tr = new Rect(0f, r.y + 22f, W, 100f);
            if (inverse)
                Ui.Label(tr, title, 88, tc, TextAnchor.MiddleCenter, false);
            else
            {
                if (!calm)  // a soft halo: the title twice more, a few pixels out, faint
                {
                    var g = new Color(ink.r, ink.g, ink.b, 0.12f);
                    Ui.Label(new Rect(tr.x - 3f, tr.y, tr.width, tr.height), title, 88, g, TextAnchor.MiddleCenter, false);
                    Ui.Label(new Rect(tr.x + 3f, tr.y, tr.width, tr.height), title, 88, g, TextAnchor.MiddleCenter, false);
                }
                Ui.Label(tr, title, 88, tc, TextAnchor.MiddleCenter, false);
            }
            Ui.CrtFont = was;
            if (sub.Length > 0)
                Ui.Label(new Rect(0f, r.y + 118f, W, 20f), sub, 15, inverse ? dark : CrtUi.T(new Color(0.35f, 0.85f, 0.91f)), TextAnchor.MiddleCenter, true);
            if (bar >= 0f)
            {
                var tk = new Rect(side, r.yMax - 16f, W - 2f * side, 8f);
                Ui.Fill(tk, new Color(dim.r, dim.g, dim.b, 0.9f));
                Ui.Fill(new Rect(tk.x + 1f, tk.y + 1f, tk.width - 2f, tk.height - 2f), dark);
                Ui.Fill(new Rect(tk.x + 1f, tk.y + 1f, (tk.width - 2f) * Mathf.Clamp01(bar), tk.height - 2f), ink);
            }
            CrtUi.End(new Rect(edge, r.y, W - 2f * edge, r.height), Time.unscaledTime, calm);
        }
    }
}
