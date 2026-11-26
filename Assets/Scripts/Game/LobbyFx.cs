using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The holo boot (a box powering on): a bright scan line wipes down the box, showing it as it goes, then the box
    /// flickers twice with a glow round its frame. Used by the lobby's entrance (LobbyEnter.cs, each piece) and the CRT
    /// cabinet windows: the pause menu, Settings, the lobby's menu, How to play and the profile (CrtWindows.cs).
    /// Look: the Lobby FX Lab mockup's "Holo boot". Reduced effects or Visual effects off: callers skip it.
    public static class HoloBoot
    {
        public const float Wipe = 0.42f, Flicker = 0.18f, Len = Wipe + Flicker;

        /// How much of the box shows, 0..1 from the top, `e` seconds into its boot over `wipe` seconds.
        public static float Reveal(float e, float wipe = Wipe)
        {
            if (e <= 0f)
                return 0f;
            float k = Mathf.Clamp01(e / wipe);
            return 1f - (1f - k) * (1f - k) * (1f - k);
        }

        /// Over the box, after it is drawn clipped to Reveal: the scan line at the reveal edge while it wipes, then the flicker
        /// (two dark frames) and a glow on its frame fading out. `bg` is the dark the flicker dips to.
        public static void Over(Rect r, float e, float wipe = Wipe, bool line = true)
        {
            if (e <= 0f || e >= wipe + Flicker)
                return;
            var ink = CrtStyle.Ink;
            if (e < wipe)
            {
                if (!line)
                    return;
                float y = r.y + r.height * Reveal(e, wipe);
                Ui.Glow(new Rect(r.x, y - 2f, r.width, 4f), ink, 0.25f);
                Ui.Fill(new Rect(r.x, y - 1f, r.width, 2f), new Color(0.92f, 0.96f, 1f, 0.45f));
                return;
            }
            float f = (e - wipe) / Flicker;
            if (Mathf.FloorToInt((e - wipe) * 40f) % 2 == 1)
                Ui.Fill(r, CrtStyle.Bg.WithAlpha(0.2f));
            Ui.Glow(r, ink, 0.25f * (1f - f));
            CrtKit.Frame(r, 1f, ink.WithAlpha(0.4f * (1f - f)));
        }

        // --- The soft sweep (the CRT cabinet windows: pause menu, lobby menu, Settings, How to play, profile) -------------
        // Option D of the "Menu Boot-Up Options" mockup: a wide feathered edge glides down the box (ease in-out), the box
        // fading in under it; no hard line, no flicker. Draw the box clipped to SoftClip, then SoftOver on top.

        public const float SoftLen = 0.55f, Feather = 140f;
        const int FeatherBands = 14;

        /// The leading edge's y (everything above it is fully shown), `e` seconds into the sweep of `r`.
        public static float SoftEdge(Rect r, float e)
        {
            float k = Mathf.Clamp01(e / SoftLen);
            k = k < 0.5f ? 4f * k * k * k : 1f - Mathf.Pow(-2f * k + 2f, 3f) / 2f;
            return r.y - Feather + (r.height + Feather) * k;
        }

        /// Where to cut the box off while it sweeps in (the feather's far end, at most the box's bottom).
        public static float SoftClip(Rect r, float e) => e >= SoftLen ? float.MaxValue : Mathf.Min(r.yMax, SoftEdge(r, e) + Feather);

        /// Over the box, after it is drawn clipped to SoftClip: the feather (bands of the dim backdrop, darker further down)
        /// and a faint glow just under the edge.
        public static void SoftOver(Rect r, float e)
        {
            if (e < 0f || e >= SoftLen)
                return;
            float edge = SoftEdge(r, e), step = Feather / FeatherBands;
            for (int i = 0; i < FeatherBands; i++)
            {
                float y0 = edge + step * i;
                if (y0 >= r.yMax)
                    break;
                float y = Mathf.Max(y0, r.y), h = Mathf.Min(y0 + step, r.yMax) - y;
                if (h > 0f)
                    Ui.Fill(new Rect(r.x, y, r.width, h), new Color(0.01f, 0.012f, 0.03f, (i + 0.5f) / FeatherBands));
            }
            float ly = edge + Feather * 0.15f;
            if (ly > r.y && ly < r.yMax)
                Ui.Fill(new Rect(r.x, ly - 8f, r.width, 16f), CrtStyle.Ink.WithAlpha(0.08f));
        }
    }

    /// The lobby's live effects (the Lobby FX Lab mockup): the proximity glow on box frames near the cursor, the Ready
    /// shockwave into a party card, and the radar sweep over the loadout preview. The holo boot is HoloBoot above; the
    /// launch glitch is LaunchIntro.cs (DrawLobbyLeaving); the CRT veil's moving scanlines are LobbyCrt.cs. All of them
    /// are off with Reduced effects or Visual effects off.
    public partial class ArenaApp
    {
        bool LobbyFxOn => fxOn && !fxReduced;

        // --- Proximity glow -----------------------------------------------------------------------------------------------

        readonly List<Rect> lobbyGlowRects = new List<Rect>();
        const float ProxReach = 140f, ProxSpan = 60f;

        /// A box whose frame lights up when the cursor comes near (collected while the lobby draws, lit at the end).
        void LobbyGlowBox(Rect r)
        {
            if (LobbyFxOn)
                lobbyGlowRects.Add(r);
        }

        /// The frames near the cursor: a soft hot spot on each edge at the point nearest the cursor, brighter the closer it is.
        void DrawLobbyProxGlow()
        {
            if (!LobbyFxOn || Event.current.type != EventType.Repaint || !GUI.enabled)
            {
                lobbyGlowRects.Clear();
                return;
            }
            var m = Event.current.mousePosition;
            foreach (var r in lobbyGlowRects)
            {
                if (m.x < r.x - ProxReach || m.x > r.xMax + ProxReach || m.y < r.y - ProxReach || m.y > r.yMax + ProxReach)
                    continue;
                ProxEdge(m, r.x, r.y, r.width, true);
                ProxEdge(m, r.x, r.yMax - 1f, r.width, true);
                ProxEdge(m, r.x, r.y, r.height, false);
                ProxEdge(m, r.xMax - 1f, r.y, r.height, false);
            }
            lobbyGlowRects.Clear();
        }

        /// One edge from (x, y), `len` long, across (horiz) or down: a crisp 2 px tracer brightest at the point nearest the
        /// cursor, and a faint 1 px echo line 3 px outside it on the cursor's side. No blur.
        static void ProxEdge(Vector2 m, float x, float y, float len, bool horiz)
        {
            float u = horiz ? Mathf.Clamp(m.x - x, 0f, len) : Mathf.Clamp(m.y - y, 0f, len);
            float px = horiz ? x + u : x, py = horiz ? y : y + u;
            float d = Vector2.Distance(m, new Vector2(px, py));
            if (d > ProxReach)
                return;
            float a = Mathf.Pow(1f - d / ProxReach, 1.5f);
            x = Mathf.Round(x);
            y = Mathf.Round(y);
            ProxRun(x, y, len, u, horiz, ProxSpan, 2f, a, 0f);
            ProxRun(x, y, len, u, horiz, ProxSpan * 1.6f, 1f, a * 0.35f, horiz ? (m.y < y ? -3f : 3f) : (m.x < x ? -3f : 3f));
        }

        /// A run of short pieces along an edge, brightest at `u`, fading over `span` either side; `off` moves it off the edge.
        static void ProxRun(float x, float y, float len, float u, bool horiz, float span, float w, float a, float off)
        {
            const float step = 4f;
            var ink = CrtStyle.Ink;
            for (float s = -span; s < span; s += step)
            {
                float p0 = Mathf.Max(0f, u + s), p1 = Mathf.Min(len, u + s + step);
                if (p1 <= p0)
                    continue;
                float k = 1f - Mathf.Abs(s + step / 2f) / span;
                k *= k;
                var c = Color.Lerp(ink, Color.white, 0.25f + 0.75f * k);
                c.a = a * k;
                Ui.Fill(horiz ? new Rect(x + p0, y + off, p1 - p0, w) : new Rect(x + off, y + p0, w, p1 - p0), c);
            }
        }

        // --- Ready shockwave ----------------------------------------------------------------------------------------------

        readonly Dictionary<int, float> readyAt = new Dictionary<int, float>();   // when each seat last readied
        readonly Dictionary<int, bool> readyWas = new Dictionary<int, bool>();
        int readyCheckedFrame = -1;
        const float ReadyPulse = 0.55f, ReadyBurst = 1.05f;

        /// Once a frame: note who has just readied (their card gets the shockwave). Seats ready when the lobby opens don't fire.
        void TickReadyShock()
        {
            if (readyCheckedFrame == Time.frameCount)
                return;
            readyCheckedFrame = Time.frameCount;
            bool opening = LobbyEntering || LobbyLeaving;
            foreach (var e in session.Lobby.Values)
            {
                bool was;
                if (readyWas.TryGetValue(e.Id, out was) && !was && e.Ready && !opening)
                    readyAt[e.Id] = Time.unscaledTime;
                readyWas[e.Id] = e.Ready;
            }
        }

        /// Over a party card: a pulse runs up a thin conduit from the Ready key into the card, a green ring bursts off it with a
        /// wash and a scan across it, then (while ready) its frame keeps a slow green glow.
        void DrawReadyShock(Rect card, LobbyEntry e)
        {
            if (!LobbyFxOn || Event.current.type != EventType.Repaint)
                return;
            float at, el = readyAt.TryGetValue(e.Id, out at) ? Time.unscaledTime - at : 99f;
            var good = Ui.Good;
            if (el < ReadyPulse)
            {
                // The conduit: from the Ready key's right edge, out into the gutter between the party column and the big screen,
                // up beside the card and in (on the inside of the screen, clear of its curved edge).
                float x0 = card.xMax, gx = x0 + 8f, y0 = session.IsHost ? LobbyReadyY + LobbyReadyH / 2f : LobbyStartY + LobbyStartH / 2f,  // (a guest's Ready is in the Start slot)
                      y1 = card.center.y;
                float rise = y0 - y1, total = 8f + rise + 8f, at2 = el / ReadyPulse * total;
                var dim = good.WithAlpha(0.25f * (1f - el / ReadyPulse * 0.5f));
                Ui.Fill(new Rect(x0, y0, 8f, 1f), dim);
                Ui.Fill(new Rect(gx, y1, 1f, rise + 1f), dim);
                Ui.Fill(new Rect(x0, y1, 8f, 1f), dim);
                // A crisp spark with a short tail behind it.
                for (int i = 0; i < 6; i++)
                {
                    float back = i * 5f, tail = 1f - i / 6f;
                    float q = at2 - back;
                    if (q < 0f) break;
                    Vector2 tp = q < 8f ? new Vector2(x0 + q, y0) : q < 8f + rise ? new Vector2(gx, y0 - (q - 8f)) : new Vector2(gx - (q - 8f - rise), y1);
                    float sz = i == 0 ? 4f : 2f;
                    Ui.Fill(new Rect(Mathf.Round(tp.x - sz / 2f + 0.5f), Mathf.Round(tp.y - sz / 2f + 0.5f), sz, sz), new Color(Mathf.Lerp(good.r, 1f, 0.5f * tail), Mathf.Lerp(good.g, 1f, 0.5f * tail), Mathf.Lerp(good.b, 1f, 0.5f * tail), tail));
                }
            }
            const float burstAt = ReadyPulse;
            if (el >= burstAt && el < burstAt + ReadyBurst)
            {
                // The ring bursts off the card (no further than 10 px, clear of the screen's edge), with a green wash and a scan across it.
                float k = (el - burstAt) / ReadyBurst, ease = 1f - (1f - k) * (1f - k) * (1f - k), g = ease * 10f;
                CrtKit.Frame(new Rect(card.x - g, card.y - g, card.width + g * 2f, card.height + g * 2f), 1f, good.WithAlpha(0.7f * (1f - k)));
                Ui.Fill(card, good.WithAlpha(0.12f * (1f - k)));
                Ui.Fill(new Rect(card.x + card.width * ease, card.y, 2f, card.height), new Color(0.92f, 1f, 0.95f, 0.45f * (1f - k)));
            }
            // While ready: a thin green line round the card, breathing slowly (no blur).
            if (e.Ready && el >= burstAt)
                CrtKit.Frame(new Rect(card.x - 2f, card.y - 2f, card.width + 4f, card.height + 4f), 1f,
                    good.WithAlpha(0.18f + 0.1f * Mathf.Sin(Time.unscaledTime * 2f + e.Id)));
        }
    }

    public partial class ArenaApp
    {
        const float RunnerLap = 7f, RunnerTrail = 90f;  // seconds a lap; trail length in px

        /// The neon dot running round the loadout preview's frame (clockwise, a lap every RunnerLap s): a white-hot head (no glow)
        /// and a tail in the role's colour fading out behind it along the edge. Off with Reduced effects.
        void DrawPreviewRunner(Rect view, Color c, float t)
        {
            if (fxReduced || Event.current.type != EventType.Repaint)
                return;
            float per = 2f * (view.width + view.height);
            float head = (t / RunnerLap % 1f) * per;
            const int steps = 60;
            for (int k = steps; k >= 1; k--)  // the tail first (no glow), the head on top
            {
                float f = 1f - k / (float)steps;
                var p = RunnerAt(view, head - RunnerTrail * k / steps);
                float sz = 1f + 3f * f;
                Ui.Fill(new Rect(Mathf.Round(p.x - sz / 2f), Mathf.Round(p.y - sz / 2f), sz, sz), c.WithAlpha(0.9f * f));
            }
            var hp = RunnerAt(view, head);
            Ui.Fill(new Rect(Mathf.Round(hp.x - 2f), Mathf.Round(hp.y - 2f), 4f, 4f), Color.Lerp(c, Color.white, 0.75f));
        }

        /// The line across the loadout between the preview and picks above and the title and detail box below: a hairline in the role's
        /// colour fading out at both ends over a soft glow band, end ticks, an unlit diamond in the middle with two small ones beside it, and
        /// (unless Reduced effects) a bright glint sweeping along it every few seconds.
        void DrawLoadoutSeparator(float x0, float x1, float y, Color c, float t)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            bool calm = fxReduced;
            y = Mathf.Round(y);
            float w = x1 - x0, mid = Mathf.Round((x0 + x1) / 2f);
            const int steps = 48;
            for (int i = 0; i < steps; i++)  // the hairline and its glow, strongest in the middle
            {
                float u = (i + 0.5f) / steps, k = 1f - Mathf.Abs(u * 2f - 1f);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k * 2.2f));
                float sx = x0 + w * i / steps, sw = w / steps + 1f;
                if (!calm)
                    Ui.Fill(new Rect(sx, y - 2f, sw, 5f), c.WithAlpha(0.07f * a));
                Ui.Fill(new Rect(sx, y, sw, 1f), c.WithAlpha(0.65f * a));
            }
            for (int side = -1; side <= 1; side += 2)
            {
                float ex = side < 0 ? x0 : x1 - 2f;  // end ticks
                Ui.Fill(new Rect(ex, y - 3f, 2f, 7f), c.WithAlpha(0.5f));
                for (int n = 1; n <= 2; n++)  // the small diamonds beside the middle one
                    Diamond(new Vector2(mid + side * (14f + n * 10f), y + 0.5f), 2f, c.WithAlpha(0.75f - n * 0.2f));
                Ui.Fill(new Rect(side < 0 ? mid - 13f : mid + 8f, y, 5f, 1f), Color.Lerp(c, Color.white, 0.5f));
            }
            Diamond(new Vector2(mid, y + 0.5f), 6f, c);  // (no glow behind it)
            Diamond(new Vector2(mid, y + 0.5f), 3f, Color.Lerp(c, Color.white, 0.8f));
            if (calm)
                return;
            float g = (t % 5f) / 1.6f;  // the glint: 1.6 s across, then a rest
            if (g < 1f)
            {
                float gx = x0 + w * Mathf.SmoothStep(0f, 1f, g), fade = Mathf.Sin(g * Mathf.PI);
                for (int k = 0; k < 12; k++)
                {
                    float f = 1f - k / 12f;
                    Ui.Fill(new Rect(gx - k * 4f, y, 4f, 1f), Color.Lerp(c, Color.white, 0.7f).WithAlpha(fade * f));
                }
            }
        }

        /// A filled pixel diamond centred on p, `r` px from the centre to each point.
        static void Diamond(Vector2 p, float r, Color col)
        {
            for (int k = -(int)r; k <= (int)r; k++)
            {
                float half = r - Mathf.Abs(k);
                Ui.Fill(new Rect(Mathf.Round(p.x - half), Mathf.Round(p.y + k - 0.5f), half * 2f + 1f, 1f), col);
            }
        }

        /// The point `d` px clockwise round r's edge from its top-left corner.
        static Vector2 RunnerAt(Rect r, float d)
        {
            float per = 2f * (r.width + r.height);
            d = (d % per + per) % per;
            if (d < r.width) return new Vector2(r.x + d, r.y);
            d -= r.width;
            if (d < r.height) return new Vector2(r.xMax, r.y + d);
            d -= r.height;
            if (d < r.width) return new Vector2(r.xMax - d, r.yMax);
            return new Vector2(r.x, r.yMax - (d - r.width));
        }
    }
}
