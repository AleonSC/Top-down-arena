using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The main menu buttons' old-tape look (look C, "Rewind", of Docs/Mockups/menu-vhs-hover.html). Wrap a button's draw call in
    /// MenuVhs.Draw: while the mouse is over it, it gets a blue cast, flicker, scanlines, a tracking band, sideways jitter and a
    /// tape counter. Clicking snaps it into focus (0.3 s: double image pulling together and a white flash) and it stays clean until
    /// the mouse leaves and comes back. calm (Reduced effects) keeps only a tint and no snap.
    public static class MenuVhs
    {
        const float SnapLen = 0.3f;

        /// Called once each time the mouse moves onto a button (the argument is "calm": Reduced effects, so play it quieter). ArenaApp sets it to play the hover tick.
        public static Action<bool> OnHover;

        class State { public bool Hover, Clean; public float SnapAt = -10f, Seed; }

        static readonly Dictionary<int, State> states = new Dictionary<int, State>();

        public static bool Draw(Rect r, string tag, bool live, bool calm, Func<bool> draw)
        {
            int key = Mathf.RoundToInt(r.x * 31f + r.y * 17f + r.width);
            if (!states.TryGetValue(key, out var s))
                states[key] = s = new State { Seed = (key & 255) / 255f };
            float now = Time.unscaledTime;
            bool repaint = Event.current.type == EventType.Repaint;
            if (repaint)
            {
                bool was = s.Hover;
                s.Hover = live && GUI.enabled && r.Contains(Event.current.mousePosition);
                if (s.Hover && !was)
                    OnHover?.Invoke(calm);
                if (!s.Hover)
                    s.Clean = false;
            }
            float snap = (now - s.SnapAt) / SnapLen;  // 0..1 while snapping
            bool snapping = snap >= 0f && snap < 1f && !calm;
            bool vhs = s.Hover && !s.Clean && !snapping;

            float dx = 0f;
            if (vhs && !calm)
            {
                float j = (now * 2f + s.Seed * 3f) % 1f;  // a quick lurch twice a second
                dx = j < 0.06f ? -2f : j > 0.62f && j < 0.66f ? 2f : 0f;
            }
            var old = GUI.color;
            if (vhs)
            {
                float flick = calm ? 1f : (Mathf.FloorToInt(now * 15f) % 2 == 0 ? 1f : 0.88f);
                GUI.color = new Color(0.82f, 0.9f, 1f, old.a * flick);  // the cool cast
            }
            // Double image while snapping: red and cyan copies pull together as the button locks.
            if (snapping && repaint)
            {
                float k = 1f - snap, off = 7f * k * k;
                Ui.NoHit = true;  // copies for show: no control id, no click, or ids drift between Repaint and mouse events
                GUI.color = new Color(1f, 0.3f, 0.35f, old.a * 0.45f * k);
                GUI.BeginGroup(new Rect(-off, 0f, 4000f, 4000f)); draw(); GUI.EndGroup();
                GUI.color = new Color(0.3f, 0.85f, 1f, old.a * 0.45f * k);
                GUI.BeginGroup(new Rect(off, 0f, 4000f, 4000f)); draw(); GUI.EndGroup();
                Ui.NoHit = false;
                GUI.color = old;
            }
            bool clicked;
            Ui.SilentHover = true;
            if (dx != 0f)
            {
                GUI.BeginGroup(new Rect(dx, 0f, 4000f, 4000f));
                clicked = draw();
                GUI.EndGroup();
            }
            else
                clicked = draw();
            Ui.SilentHover = false;
            GUI.color = old;

            if (clicked)
            {
                s.SnapAt = now;
                s.Clean = true;
            }
            if (repaint)
            {
                if (vhs)
                    Overlay(r, tag, now, s.Seed, calm);
                else if (snapping)
                    Ui.Fill(r, new Color(1f, 1f, 1f, 0.5f * (1f - snap) * (1f - snap)));
            }
            return clicked;
        }

        /// Scanlines, the tracking band and the tape counter, clipped to the button.
        static void Overlay(Rect r, string tag, float now, float seed, bool calm)
        {
            GUI.BeginGroup(r);
            var local = new Rect(0f, 0f, r.width, r.height);
            for (float y = 0f; y < r.height; y += 3f)
                Ui.Fill(new Rect(0f, y, r.width, 1f), new Color(0f, 0f, 0f, 0.34f));
            if (!calm)
            {
                float p = (now / 0.9f + seed) % 1f;  // the band: 40% tall, falls from above to below
                float bh = r.height * 0.4f, by = Mathf.Lerp(-bh, r.height, p);
                for (int i = 0; i < 8; i++)
                {
                    float f = (i + 0.5f) / 8f, a = Mathf.Sin(f * Mathf.PI) * 0.3f;
                    Ui.Fill(new Rect(0f, by + bh * i / 8f, r.width, bh / 8f + 1f), new Color(0.75f, 0.88f, 1f, a));
                }
                if (tag != null)
                {
                    int fr = Mathf.FloorToInt(now * 24f) % 24;
                    string txt = tag + " ◂◂ 00:00:" + (Mathf.FloorToInt(now) % 60).ToString("00") + ":" + fr.ToString("00");
                    Ui.Label(new Rect(9f, 4f, r.width - 12f, 14f), txt, 10, new Color(1f, 0.16f, 0.2f, 0.9f), TextAnchor.UpperLeft);
                    Ui.Label(new Rect(8f, 4f, r.width - 12f, 14f), txt, 10, new Color(0.9f, 0.97f, 1f, 1f), TextAnchor.UpperLeft);
                }
            }
            GUI.EndGroup();
        }
    }
}
