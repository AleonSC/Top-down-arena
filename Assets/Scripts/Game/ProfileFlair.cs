using UnityEngine;

namespace Arena
{
    /// Mockup options for extra flair on the profile cabinet (Flair 1-3; 0 = none). Preview only, picked with PROFLAIR in Tools/Preview.
    /// Hooks: FlairBackdrop and FlairFrame (right after BeginDrawer, cabinet coordinates), FlairBadge (behind the rank badge),
    /// FlairTab (an open tab key), FlairRow (an option row, after it is drawn).
    public partial class ArenaApp
    {
        public static int ProfileFlair;

        static float FlairHash(int a) { float s = Mathf.Sin(a * 127.1f) * 43758.5f; return s - Mathf.Floor(s); }

        void FlairBackdrop(Rect cab, Color tc)
        {
            if (ProfileFlair == 0)
                return;
            float t = WinNow;
            if (ProfileFlair == 1)  // a faint tier-tinted perspective grid at the foot
            {
                for (int i = 0; i < 9; i++)
                {
                    float f = (i + (t * 0.25f) % 1f) / 9f, y = cab.y + cab.height * (0.55f + 0.45f * f * f);
                    Ui.Fill(new Rect(cab.x + 4f, y, cab.width - 8f, 1f), tc.WithAlpha(0.05f + 0.10f * f));
                }
                for (int i = -6; i <= 6; i++)
                    Ui.Fill(new Rect(cab.center.x + i * 34f, cab.y + cab.height * 0.7f, 1f, cab.height * 0.3f), tc.WithAlpha(0.06f));
            }
            else if (ProfileFlair == 2)  // scanlines and a slow bright band
            {
                for (float y = cab.y + 4f; y < cab.yMax - 4f; y += 4f)
                    Ui.Fill(new Rect(cab.x + 4f, y, cab.width - 8f, 1f), Color.black.WithAlpha(0.14f));
                float by = cab.y + ((t * 60f) % (cab.height + 60f)) - 30f;
                Ui.Fill(new Rect(cab.x + 4f, by, cab.width - 8f, 22f), tc.WithAlpha(0.05f));
                Ui.Fill(new Rect(cab.x + 4f, by + 10f, cab.width - 8f, 2f), tc.WithAlpha(0.10f));
            }
            else  // rising particles
            {
                for (int i = 0; i < 40; i++)
                {
                    float sp = 12f + 26f * FlairHash(i * 3 + 1), ph = FlairHash(i * 3 + 2) * cab.height;
                    float y = cab.yMax - ((t * sp + ph) % cab.height), x = cab.x + 10f + FlairHash(i * 3) * (cab.width - 20f);
                    float a = Mathf.Sin(Mathf.Clamp01((cab.yMax - y) / cab.height) * Mathf.PI) * 0.8f;
                    float s = FlairHash(i + 99) > 0.8f ? 3f : 2f;
                    Ui.Fill(new Rect(x, y, s, s), tc.WithAlpha(a));
                }
            }
        }

        void FlairFrame(Rect cab, Rect header, Color tc)
        {
            if (ProfileFlair == 0)
                return;
            float L = 22f;
            var edge = ProfileFlair == 1 ? CrtStyle.Gold : tc;
            float pulse = 0.65f + 0.35f * Mathf.Sin(WinNow * 2.2f);
            // Corner brackets
            foreach (var cx in new[] { cab.x + 3f, cab.xMax - 3f })
                foreach (var cy in new[] { cab.y + 3f, cab.yMax - 3f })
                {
                    float sx = cx < cab.center.x ? 1f : -1f, sy = cy < cab.center.y ? 1f : -1f;
                    Ui.Fill(new Rect(sx > 0 ? cx : cx - L, sy > 0 ? cy : cy - 2f, L, 2f), edge.WithAlpha(0.9f * pulse));
                    Ui.Fill(new Rect(sx > 0 ? cx : cx - 2f, sy > 0 ? cy : cy - L, 2f, L), edge.WithAlpha(0.9f * pulse));
                }
            if (ProfileFlair == 2)  // a banner stripe behind the header
            {
                var hb = new Rect(header.x, header.y, header.width, header.height);
                Ui.Fill(hb, tc.WithAlpha(0.08f));
                for (float x = hb.x - hb.height; x < hb.xMax; x += 14f)
                    Ui.Fill(new Rect(Mathf.Max(hb.x, x + (WinNow * 10f) % 14f), hb.y, 4f, 3f), tc.WithAlpha(0.35f));
                Ui.Fill(new Rect(hb.x, hb.yMax - 2f, hb.width, 2f), tc.WithAlpha(0.5f));
            }
            else if (ProfileFlair == 1)  // a gold edge glow along the header's foot
            {
                Ui.Fill(new Rect(header.x, header.yMax + 3f, header.width, 3f), CrtStyle.Gold.WithAlpha(0.18f * pulse));
                Ui.Fill(new Rect(header.x, header.yMax + 4f, header.width * 0.4f, 1f), CrtStyle.Gold.WithAlpha(0.9f));
            }
        }

        void FlairBadge(Rect badge, Color tc)
        {
            if (ProfileFlair == 0 || FxCalm)
                return;
            float t = WinNow, pulse = 0.5f + 0.5f * Mathf.Sin(t * 2f);
            var c = badge.center;
            if (ProfileFlair == 1)  // a layered halo
                for (int i = 0; i < 4; i++)
                {
                    float g = 6f + i * 5f + pulse * 3f;
                    Ui.Pill(new Rect(badge.x - g, badge.y - g * 0.6f, badge.width + g * 2f, badge.height + g * 1.2f), tc.WithAlpha(0.17f - i * 0.035f));
                }
            else if (ProfileFlair == 2)  // a light sweep across the badge
            {
                float sx = badge.x - 20f + ((t * 50f) % (badge.width + 60f));
                Ui.Fill(new Rect(Mathf.Clamp(sx, badge.x, badge.xMax - 6f), badge.y + 4f, 6f, badge.height - 8f), Color.white.WithAlpha(0.18f));
                CrtKit.Frame(new Rect(badge.x - 4f, badge.y - 2f, badge.width + 8f, badge.height + 4f), 1f, tc.WithAlpha(0.3f + 0.3f * pulse));
            }
            else  // sparkles orbiting
                for (int i = 0; i < 6; i++)
                {
                    float a = t * 1.3f + i * Mathf.PI / 3f, r = 46f + 5f * Mathf.Sin(t * 3f + i);
                    float s = 2f + (i % 2);
                    Ui.Fill(new Rect(c.x + Mathf.Cos(a) * r - s / 2f, c.y + Mathf.Sin(a) * r * 0.85f - s / 2f, s, s), Color.Lerp(tc, Color.white, 0.5f).WithAlpha(0.5f + 0.5f * Mathf.Sin(t * 4f + i)));
                }
        }

        void FlairBar(Rect bar, float fraction, Color tc)
        {
            if (ProfileFlair == 0 || FxCalm)
                return;
            float sx = bar.x + ((WinNow * 70f) % (bar.width * fraction + 40f)) - 20f;
            float x0 = Mathf.Clamp(sx, bar.x, bar.x + bar.width * fraction), x1 = Mathf.Clamp(sx + 18f, bar.x, bar.x + bar.width * fraction);
            if (x1 > x0)
                Ui.Fill(new Rect(x0, bar.y, x1 - x0, bar.height), Color.white.WithAlpha(0.55f));
            Ui.Fill(new Rect(bar.x + bar.width * fraction, bar.y - 3f, 2f, bar.height + 6f), Color.white.WithAlpha(0.8f));
        }

        void FlairTab(Rect r, Color tc)
        {
            if (ProfileFlair == 0)
                return;
            float pulse = 0.5f + 0.5f * Mathf.Sin(WinNow * 3f);
            if (ProfileFlair == 1)
                Ui.Fill(new Rect(r.x, r.yMax - 3f, r.width, 3f), CrtStyle.Gold.WithAlpha(0.7f + 0.3f * pulse));
            else if (ProfileFlair == 2)
                Ui.Fill(new Rect(r.x - 3f, r.y + 6f, 2f, r.height - 12f), tc.WithAlpha(0.9f));
            else
                Ui.Pill(new Rect(r.x - 3f, r.y - 3f, r.width + 6f, r.height + 6f), tc.WithAlpha(0.06f + 0.06f * pulse));
        }

        void FlairRow(Rect r, bool selected, Color tc)
        {
            if (ProfileFlair == 0)
                return;
            bool hover = GUI.enabled && r.Contains(Event.current.mousePosition);
            if (!selected && !hover)
                return;
            float pulse = 0.5f + 0.5f * Mathf.Sin(WinNow * 3f);
            if (ProfileFlair == 1)
                Ui.Fill(new Rect(r.x - 4f, r.y + 2f, 3f, r.height - 4f), CrtStyle.Gold.WithAlpha(0.6f + 0.4f * pulse));
            else if (ProfileFlair == 2)
            {
                for (int i = 0; i < 3; i++)
                    Ui.Fill(new Rect(r.x - 9f + i * 3f, r.y + r.height / 2f - 3f + i * 0.0f, 2f, 6f), tc.WithAlpha(0.3f + 0.25f * i));
            }
            else
            {
                float sx = r.x + ((WinNow * 140f) % (r.width + 40f)) - 20f;
                float x0 = Mathf.Clamp(sx, r.x, r.xMax), x1 = Mathf.Clamp(sx + 24f, r.x, r.xMax);
                if (x1 > x0)
                    Ui.Fill(new Rect(x0, r.y + 1f, x1 - x0, r.height - 2f), Color.white.WithAlpha(0.10f));
            }
        }
    }
}
