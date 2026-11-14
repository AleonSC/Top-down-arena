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
        public static void Over(Rect r, float e, float wipe = Wipe)
        {
            if (e <= 0f || e >= wipe + Flicker)
                return;
            var ink = CrtUi.Ink;
            if (e < wipe)
            {
                float y = r.y + r.height * Reveal(e, wipe);
                Ui.Glow(new Rect(r.x, y - 2f, r.width, 4f), ink, 0.5f);
                Ui.Fill(new Rect(r.x, y - 1f, r.width, 2f), new Color(0.92f, 0.96f, 1f, 0.9f));
                return;
            }
            float f = (e - wipe) / Flicker;
            if (Mathf.FloorToInt((e - wipe) * 40f) % 2 == 1)
                Ui.Fill(r, new Color(CrtUi.Bg.r, CrtUi.Bg.g, CrtUi.Bg.b, 0.5f));
            Ui.Glow(r, ink, 0.55f * (1f - f));
            CrtKit.Frame(r, 1f, new Color(ink.r, ink.g, ink.b, 0.7f * (1f - f)));
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
        const float ProxReach = 140f, ProxSpan = 90f;

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

        /// One edge from (x, y), `len` long, across (horiz) or down: short 2 px dashes fading out either side of the nearest point.
        static void ProxEdge(Vector2 m, float x, float y, float len, bool horiz)
        {
            float u = horiz ? Mathf.Clamp(m.x - x, 0f, len) : Mathf.Clamp(m.y - y, 0f, len);
            float px = horiz ? x + u : x, py = horiz ? y : y + u;
            float d = Vector2.Distance(m, new Vector2(px, py));
            if (d > ProxReach)
                return;
            float a = Mathf.Pow(1f - d / ProxReach, 1.5f);
            var ink = CrtUi.Ink;
            const float step = 6f;
            for (float s = -ProxSpan; s < ProxSpan; s += step)
            {
                float p0 = Mathf.Max(0f, u + s), p1 = Mathf.Min(len, u + s + step);
                if (p1 <= p0)
                    continue;
                float k = 1f - Mathf.Abs(s + step / 2f) / ProxSpan;
                var c = Color.Lerp(ink, Color.white, 0.3f + 0.7f * k);
                c.a = a * k;
                Ui.Fill(horiz ? new Rect(x + p0, Mathf.Round(y) - 1f, p1 - p0, 3f) : new Rect(Mathf.Round(x) - 1f, y + p0, 3f, p1 - p0), c);
            }
            if (a > 0.15f)
                Ui.Glow(horiz ? new Rect(px - 40f, y - 1f, 80f, 2f) : new Rect(x - 1f, py - 40f, 2f, 80f), ink, a * 0.8f);
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
                // The conduit: from the Ready key's left edge, out into the gutter, up beside the card and in.
                float gx = card.x - 14f, y0 = session.IsHost ? LobbyReadyY + LobbyReadyH / 2f : LobbyStartY + LobbyStartH / 2f,  // (a guest's Ready is in the Start slot)
                      y1 = card.center.y;
                float rise = y0 - y1, total = 14f + rise + 14f, at2 = el / ReadyPulse * total;
                var dim = new Color(good.r, good.g, good.b, 0.18f);
                Ui.Fill(new Rect(gx, y0, 14f, 1f), dim);
                Ui.Fill(new Rect(gx, y1, 1f, rise), dim);
                Ui.Fill(new Rect(gx, y1, 14f, 1f), dim);
                Vector2 p = at2 < 14f ? new Vector2(card.x - at2, y0)
                    : at2 < 14f + rise ? new Vector2(gx, y0 - (at2 - 14f))
                    : new Vector2(gx + (at2 - 14f - rise), y1);
                Ui.Glow(new Rect(p.x - 3f, p.y - 3f, 6f, 6f), good, 0.9f);
                Ui.Fill(new Rect(p.x - 3f, p.y - 3f, 6f, 6f), Color.Lerp(good, Color.white, 0.4f));
            }
            else if (el < ReadyPulse + ReadyBurst)
            {
                float k = (el - ReadyPulse) / ReadyBurst, ease = 1f - (1f - k) * (1f - k) * (1f - k), g = ease * 26f;
                CrtKit.Frame(new Rect(card.x - g, card.y - g, card.width + g * 2f, card.height + g * 2f), 2f, new Color(good.r, good.g, good.b, 0.8f * (1f - k)));
                Ui.Fill(card, new Color(good.r, good.g, good.b, 0.16f * (1f - k)));
                Ui.Fill(new Rect(card.x + card.width * ease, card.y, 2f, card.height), new Color(0.92f, 1f, 0.95f, 0.5f * (1f - k)));
            }
            if (e.Ready && el >= ReadyPulse)
                Ui.Glow(card, good, 0.22f + 0.1f * Mathf.Sin(Time.unscaledTime * 3f + e.Id));
        }

        // --- Radar sweep --------------------------------------------------------------------------------------------------

        /// GL, inside the loadout preview's pass (model at the screen, clipped to `view`): a slow beam sweeping round from low
        /// in the window, with a fading trail and faint range rings. No blips.
        void DrawPreviewRadar(Rect view)
        {
            if (!LobbyFxOn)
                return;
            var c = new V2(view.center.x, view.y + view.height * 0.72f);
            float ang = Time.unscaledTime * 1.3f, r = Mathf.Sqrt(view.width * view.width + view.height * view.height);
            const int trail = 20;
            for (int k = 0; k < trail; k++)
            {
                float a = 0.07f * (1f - k / (float)trail);
                Wedge(c, r, ang - k * 0.03f - 0.015f, 0.015f, new Rgba(CrtUi.Ink.r, CrtUi.Ink.g, CrtUi.Ink.b, a), 2);
            }
            Line(c, c + V2.FromAngle(ang) * r, 1.5f, new Rgba(0.9f, 0.95f, 1f, 0.45f));
            for (int i = 1; i <= 4; i++)
                Arc(c, 70f * i, 1f, new Rgba(CrtUi.Ink.r, CrtUi.Ink.g, CrtUi.Ink.b, 0.07f), 48);
        }
    }
}
