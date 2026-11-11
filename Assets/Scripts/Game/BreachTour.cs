using UnityEngine;

namespace Arena
{
    /// The breach warning (Core/Breaches.cs WarnTime): at a breach wave's start, before anything spawns, the camera flies to
    /// each breach in turn, holds while it flashes (a strobing barrier and a red ring), then flies back to you. Letterbox bars
    /// slide in and the scan band at the top reads BREACH 1/3. Purely local: every machine plays it from its own BreachAge.
    public partial class ArenaApp
    {
        int tourSeen = -1;  // the last breach the tour flashed (for its siren, once each)

        static float TourEase(float x) => x < 0.5f ? 2f * x * x : 1f - (-2f * x + 2f) * (-2f * x + 2f) / 2f;

        /// Where the camera looks on a breach (a little inside it, so the room's floor and the void beyond both show).
        static V2 TourSpot(Breach b) => b.Pos + b.In * 80f;

        /// Which breach the tour is flashing now (-1: none), and how far into that flash (0-1).
        int TourFlash(out float k)
        {
            k = 0f;
            if (!game.BreachWarnOn)
                return -1;
            float t = game.BreachAge - Breaches.Lead, step = Breaches.Pan + Breaches.Hold;
            int n = game.ActiveBreaches.Length;
            if (t < 0f)
                return -1;
            int i = (int)(t / step);
            if (i >= n)
                return -1;
            float s = t - i * step;
            if (s < Breaches.Pan)
                return -1;
            k = (s - Breaches.Pan) / Breaches.Hold;
            return i;
        }

        /// The tour's camera focus this frame (false: not touring). `home` is where the camera would look at you.
        bool BreachTourFocus(V2 home, out V2 focus)
        {
            focus = home;
            if (!game.BreachWarnOn)
                return false;
            var bs = game.ActiveBreaches;
            float t = game.BreachAge - Breaches.Lead, step = Breaches.Pan + Breaches.Hold;
            if (bs.Length == 0 || t < 0f)
                return true;
            int i = (int)(t / step);
            if (i >= bs.Length)
            {
                float s = t - bs.Length * step;
                focus = V2.Lerp(TourSpot(bs[bs.Length - 1]), home, TourEase(Mathf.Clamp01(s / Breaches.Back)));
                return true;
            }
            float u = t - i * step;
            V2 from = i == 0 ? home : TourSpot(bs[i - 1]);
            focus = V2.Lerp(from, TourSpot(bs[i]), TourEase(Mathf.Clamp01(u / Breaches.Pan)));
            return true;
        }

        /// Over the breaches (after DrawBreaches): the one the tour is on strobes and throws out red rings.
        void DrawTourFlash()
        {
            float k;
            int i = TourFlash(out k);
            if (i < 0)
                return;
            var b = game.ActiveBreaches[i];
            float t = game.AnimT;
            bool on = Mathf.Repeat(t * 14f, 1f) < 0.5f;
            V2 al = b.Along, inw = b.In, c = b.Pos;
            float half = Breaches.Len / 2f + 12f, d = 26f;
            SetColor((on ? BreachHot : BreachRed).WithAlpha(on ? 0.55f : 0.25f));
            Quad(c - al * half - inw * d, c + al * half - inw * d, c + al * half + inw * d, c - al * half + inw * d);
            for (int r = 0; r < 2; r++)
            {
                float p = Mathf.Repeat(k * 1.8f + r * 0.5f, 1f);
                Arc(c, 90f + 220f * p, 5f * (1f - p) + 1f, BreachRed.WithAlpha(0.85f * (1f - p)), 48);
            }
        }

        /// The HUD half: letterbox bars while the camera is away and the BREACH n/N band at the top.
        void DrawBreachWarning()
        {
            if (!game.BreachWarnOn)
            {
                tourSeen = -1;
                return;
            }
            int n = game.ActiveBreaches.Length;
            float total = Breaches.WarnTime(n), age = game.BreachAge;
            float inOut = Mathf.Clamp01(Mathf.Min(age / 0.3f, (total - age) / 0.4f));
            float bar = 30f * TourEase(inOut);
            Ui.Fill(new Rect(0f, 0f, W, bar), Color.black);
            Ui.Fill(new Rect(0f, H - bar, W, bar), Color.black);

            float k;
            int at = TourFlash(out k);
            if (at >= 0 && at != tourSeen)
            {
                tourSeen = at;
                sounds?.Play(Sfx.BossSiren, 0.55f, Time.unscaledTime);  // (none in the preview tool)
            }
            int shown = Mathf.Clamp((int)((age - Breaches.Lead) / (Breaches.Pan + Breaches.Hold)), 0, Mathf.Max(0, n - 1));  // (the breach the camera is on or flying to)
            string title = n == 1 ? "BREACH INCOMING" : "BREACH " + (shown + 1) + "/" + n;
            var red = new Color(1f, 0.3f, 0.32f);
            DrawScanBand(title, "WALL BREACHED  ·  HOSTILES INBOUND  ·  BRACE", "SYS> HULL BREACH\nSYS> CAM LINK " + (shown + 1).ToString("00"),
                "WAVE " + game.ShownWave.ToString("00") + "\nx" + Breaches.Flood + " HOSTILES", age / total, red, inOut, at >= 0 && k < 0.22f, 54f, null, new Color(1f, 0.55f, 0.52f));  // (a solid red hit as each breach lights up, then the red-tinted strip)
        }
    }
}
