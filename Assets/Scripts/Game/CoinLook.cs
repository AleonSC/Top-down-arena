using UnityEngine;

namespace Arena
{
    public partial class ArenaApp
    {
        /// A gold coin: a soft ground shadow, a gold neon outline, a dark rim, a gold face, a bright inner ring and a glint that sweeps across it. 5-gold coins are bigger.
        void DrawCoin(Coin c, float t)
        {
            float r = c.Radius, ph = c.Id * 0.7f;
            float squash = 0.55f + 0.45f * Mathf.Abs(Mathf.Cos(t * 2.2f + ph));  // spins about its vertical axis
            V2 p = c.Disp;
            PickupShadow(p, r, 0f);
            NeonFrame(p.X - r * squash, p.Y - r, r * 2f * squash, r * 2f, CoinNeon);
            FillRect(p.X - r * squash, p.Y - r, r * 2f * squash, r * 2f, new Rgba(0.62f, 0.42f, 0.08f));
            float i = r * 0.78f;
            FillRect(p.X - i * squash, p.Y - i, i * 2f * squash, i * 2f, Rgba.Gold);
            float j = r * 0.4f;
            FillRect(p.X - j * squash, p.Y - j, j * 2f * squash, j * 2f, new Rgba(1f, 0.93f, 0.6f));
            if (Mathf.Repeat(t * 0.8f + ph, 1f) < 0.12f)
                FillRect(p.X - r * 0.2f, p.Y - r, r * 0.4f, r * 2f, new Rgba(1f, 1f, 1f, 0.55f));
        }

        static readonly Rgba CoinNeon = new Rgba(1f, 0.8f, 0.25f), OrbNeon = new Rgba(0.35f, 1f, 0.55f), VectorNeon = new Rgba(0.3f, 0.88f, 1f);

        /// The floor pickups' shadow (coins, vector pickups, health orbs): a soft dark ellipse on the ground under the pickup's spot
        /// (`ground`, its size `r`); `lift` is how high it bobs (px), so the shadow shrinks and fades a little as it rises.
        void PickupShadow(V2 ground, float r, float lift)
        {
            float k = 1f - Mathf.Clamp01(lift / (r * 3f)) * 0.3f;
            V2 at = ground + new V2(0f, r * 1.35f);
            Ellipse(at, r * 1.5f * k, r * 0.55f * k, new Rgba(0f, 0f, 0f, 0.35f * k));
            Ellipse(at, r * 1.1f * k, r * 0.38f * k, new Rgba(0f, 0f, 0f, 0.5f * k));
            Ellipse(at, r * 0.7f * k, r * 0.24f * k, new Rgba(0f, 0f, 0f, 0.6f * k));
        }

        /// A neon outline round a box: a wide faint glow, a mid halo and a thin bright core line.
        void NeonFrame(float x, float y, float w, float h, Rgba col)
        {
            FrameRect(x - 3f, y - 3f, w + 6f, h + 6f, 4f, col.WithAlpha(0.14f));
            FrameRect(x - 1.75f, y - 1.75f, w + 3.5f, h + 3.5f, 2.5f, col.WithAlpha(0.35f));
            FrameRect(x - 1f, y - 1f, w + 2f, h + 2f, 1.25f, col.Mix(Rgba.White, 0.35f));
        }

        /// A neon outline round a disc of radius `r`, the same three layers as `NeonFrame`.
        void NeonRing(V2 c, float r, Rgba col)
        {
            Arc(c, r + 3f, 4f, col.WithAlpha(0.14f), 24);
            Arc(c, r + 1.75f, 2.5f, col.WithAlpha(0.35f), 24);
            Arc(c, r + 1f, 1.25f, col.Mix(Rgba.White, 0.35f), 24);
        }

        /// A box outline of width `th` drawn inward from (x, y, w, h).
        void FrameRect(float x, float y, float w, float h, float th, Rgba col)
        {
            FillRect(x, y, w, th, col);
            FillRect(x, y + h - th, w, th, col);
            FillRect(x, y + th, th, h - th * 2f, col);
            FillRect(x + w - th, y + th, th, h - th * 2f, col);
        }
    }
}
