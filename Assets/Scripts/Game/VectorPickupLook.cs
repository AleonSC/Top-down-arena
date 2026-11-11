using UnityEngine;

namespace Arena
{
    public partial class ArenaApp
    {
        /// A vector pickup (look D of Docs/Mockups/vector-pickup.html, "Drawn room"): a dark tile in a faint cyan frame whose outline
        /// draws itself corner by corner with a bright dot at the tip, a small plus in the middle. Big drops (elites, bosses) are
        /// larger and ringed in amber. It bobs a little and glows.
        void DrawVectorPickup(Coin c, float t)
        {
            bool big = c.Value >= Coin.VectorDropBigAt;
            float r = c.Radius * 1.1f, ph = c.Id * 0.13f, th = Mathf.Max(1f, r * 0.16f);
            V2 p = c.Disp + new V2(0f, Mathf.Sin(t * 3f + c.Id) * 1.2f);
            var cy = new Rgba(0.3f, 0.88f, 1f);
            Circle(p, r * 2f, new Rgba(0.3f, 0.88f, 1f, 0.07f), 14);  // glow
            Circle(p + new V2(0f, r * 1.1f), r * 0.9f, new Rgba(0f, 0f, 0f, 0.3f), 8);  // shadow
            FillRect(p.X - r, p.Y - r, r * 2f, r * 2f, new Rgba(0.01f, 0.06f, 0.09f, 0.92f));
            var dim = new Rgba(0.3f, 0.88f, 1f, 0.3f);
            FillRect(p.X - r, p.Y - r, r * 2f, th, dim);
            FillRect(p.X - r, p.Y + r - th, r * 2f, th, dim);
            FillRect(p.X - r, p.Y - r, th, r * 2f, dim);
            FillRect(p.X + r - th, p.Y - r, th, r * 2f, dim);
            float L = Mathf.Repeat(t * 0.5f + ph, 1f) * 4f;  // how far round the square the drawn line has got
            V2[] q = { new V2(-r, -r), new V2(r, -r), new V2(r, r), new V2(-r, r), new V2(-r, -r) };
            V2 tip = p + q[0];
            for (int i = 0; i < 4; i++)
            {
                float f = Mathf.Clamp01(L - i);
                if (f <= 0f) break;
                V2 a = p + q[i], b = a + (q[i + 1] - q[i]) * f;
                FillRect(Mathf.Min(a.X, b.X) - th * 0.5f, Mathf.Min(a.Y, b.Y) - th * 0.5f, Mathf.Abs(b.X - a.X) + th, Mathf.Abs(b.Y - a.Y) + th, cy);
                tip = b;
            }
            FillRect(tip.X - th, tip.Y - th, th * 2f, th * 2f, new Rgba(1f, 1f, 1f));
            float u = r * 0.14f;
            FillRect(p.X - u * 0.5f, p.Y - u * 2f, u, u * 4f, new Rgba(0.3f, 0.88f, 1f, 0.55f));
            FillRect(p.X - u * 2f, p.Y - u * 0.5f, u * 4f, u, new Rgba(0.3f, 0.88f, 1f, 0.55f));
            if (big)
            {
                float rr = r * 1.9f, a0 = t * 0.9f;
                for (int i = 0; i < 12; i++)
                {
                    float a = a0 + i * Mathf.PI / 6f;
                    V2 d = p + V2.FromAngle(a) * rr;
                    FillRect(d.X - th * 0.7f, d.Y - th * 0.7f, th * 1.4f, th * 1.4f, new Rgba(1f, 0.84f, 0.4f, 0.85f));
                }
            }
        }
    }
}
