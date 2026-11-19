using UnityEngine;

namespace Arena
{
    /// Small helpers on Unity colours (Core's Rgba has its own: Rgba.WithAlpha, Mix, Darkened).
    public static class ColorX
    {
        /// The same colour with alpha `a`.
        public static Color WithAlpha(this Color c, float a) => new Color(c.r, c.g, c.b, a);

        /// A Core colour as a Unity one.
        public static Color ToColor(this Rgba c) => new Color(c.R, c.G, c.B, c.A);

        /// A Unity colour as a Core one, with alpha `a`.
        public static Rgba ToRgba(this Color c, float a) => new Rgba(c.r, c.g, c.b, a);

        /// The same colour, its alpha multiplied by `k` (a fade that keeps a see-through colour see-through).
        public static Color ScaleAlpha(this Color c, float k) => new Color(c.r, c.g, c.b, c.a * k);
    }
}
