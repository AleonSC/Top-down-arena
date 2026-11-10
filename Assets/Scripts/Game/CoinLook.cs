using UnityEngine;

namespace Arena
{
    public partial class ArenaApp
    {
        /// A gold coin: a dark rim, a gold face, a bright inner ring and a glint that sweeps across it. 5-gold coins are bigger.
        void DrawCoin(Coin c, float t)
        {
            float r = c.Radius, ph = c.Id * 0.7f;
            float squash = 0.55f + 0.45f * Mathf.Abs(Mathf.Cos(t * 2.2f + ph));  // spins about its vertical axis
            V2 p = c.Disp;
            Circle(p + new V2(0f, r * 0.9f), r * 0.9f, new Rgba(0f, 0f, 0f, 0.3f), 8);  // shadow
            FillRect(p.X - r * squash, p.Y - r, r * 2f * squash, r * 2f, new Rgba(0.62f, 0.42f, 0.08f));
            float i = r * 0.78f;
            FillRect(p.X - i * squash, p.Y - i, i * 2f * squash, i * 2f, new Rgba(1f, 0.82f, 0.3f));
            float j = r * 0.4f;
            FillRect(p.X - j * squash, p.Y - j, j * 2f * squash, j * 2f, new Rgba(1f, 0.93f, 0.6f));
            if (Mathf.Repeat(t * 0.8f + ph, 1f) < 0.12f)
                FillRect(p.X - r * 0.2f, p.Y - r, r * 0.4f, r * 2f, new Rgba(1f, 1f, 1f, 0.55f));
        }
    }
}
