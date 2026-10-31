using UnityEngine;

namespace Arena
{
    /// The fancy buttons' alternative looks, to try out (Settings → Button style; mocked up as styles B-F next to the
    /// current A). Every Ui.FancyButton draws in the picked style. None has an outline.
    public static partial class Ui
    {
        public static readonly string[] ButtonStyleNames = { "A · Sheen", "B · Slab", "C · Capsule", "D · Chamfer", "E · Glow", "F · Split" };
        public static int ButtonStyle;  // index into ButtonStyleNames; saved as "button_style"

        static GUIStyle flat, capsule, chamfer;

        static void InitButtonStyles()
        {
            flat = Style(Rounded(9, Color.white, new Color(0.86f, 0.87f, 0.9f), new Color(1f, 1f, 1f, 0f), 0f), 9);
            capsule = Style(Rounded(24, Color.white, new Color(0.84f, 0.85f, 0.88f), new Color(1f, 1f, 1f, 0f), 0f), 24);
            chamfer = new GUIStyle();
            chamfer.normal.background = Chamfer(12, Color.white, new Color(0.88f, 0.89f, 0.92f));
            chamfer.border = new RectOffset(14, 14, 14, 14);
        }

        /// A box with its top-left and bottom-right corners cut off at 45° (style D).
        static Texture2D Chamfer(int cut, Color top, Color bottom)
        {
            int size = cut * 2 + 8;
            var tex = NewTexture(size);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // Texture rows start at the bottom: the top-left corner is small x, large y.
                    float tl = x + 0.5f + (size - y - 0.5f), br = (size - x - 0.5f) + y + 0.5f;
                    float a = Mathf.Clamp01(Mathf.Min(tl, br) - cut + 0.5f);
                    var c = Color.Lerp(bottom, top, (float)y / (size - 1));
                    c.a = a;
                    px[y * size + x] = c;
                }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        static void Tinted(Rect r, GUIStyle s, Color c)
        {
            var old = GUI.backgroundColor;
            GUI.backgroundColor = c;
            GUI.Box(r, GUIContent.none, s);
            GUI.backgroundColor = old;
        }

        static Color Dark(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);

        static void ShadowText(Rect r, string text, int size, Color c, TextAnchor align = TextAnchor.MiddleCenter)
        {
            Label(new Rect(r.x, r.y + 1.5f, r.width, r.height), text, size, new Color(0f, 0f, 0f, 0.4f), align, true);
            Label(r, text, size, c, align, true);
        }

        /// The font size that fits text (plus extra px for an icon) in width.
        static int FitSize(string text, int size, float width)
        {
            while (size > 10 && TextWidth(text, size, true) - 6f > width)
                size--;
            return size;
        }

        static bool StyledButton(Rect r, string text, Color tint, Texture2D icon, float t, int size)
        {
            bool hover = r.Contains(Event.current.mousePosition) && GUI.enabled;
            int fs0 = size > 0 ? size : r.height >= 44f ? 17 : r.height >= 34f ? 15 : 12;
            switch (ButtonStyle)
            {
                case 1:  // B · Slab: a raised solid block on a darker lip, uppercase
                {
                    float lip = Mathf.Min(5f, r.height * 0.12f), lift = hover ? 1f : 0f;
                    Tinted(new Rect(r.x, r.y + lip, r.width, r.height - lip), flat, Dark(tint, 0.55f));
                    var face = new Rect(r.x, r.y - lift, r.width, r.height - lip);
                    Tinted(face, flat, hover ? Color.Lerp(tint, Color.white, 0.12f) : tint);
                    Fill(new Rect(face.x + 8f, face.y + 2f, face.width - 16f, 2f), new Color(1f, 1f, 1f, 0.22f));
                    string up = text.ToUpperInvariant();
                    float cs = icon != null ? 16f : 0f, gap = icon != null ? 8f : 0f;
                    int fs = FitSize(up, fs0 - 2, face.width - 24f - cs - gap);
                    float tw = TextWidth(up, fs, true) - 6f, x = face.center.x - (cs + gap + tw) / 2f;
                    if (icon != null)
                        Icon(new Rect(x, face.center.y - cs / 2f, cs, cs), icon);
                    ShadowText(new Rect(x + cs + gap, face.y, tw + 12f, face.height), up, fs, Color.white, TextAnchor.MiddleLeft);
                    break;
                }
                case 2:  // C · Capsule: a pill with the icon in a darker circle at its left end
                {
                    Tinted(r, capsule, hover ? Color.Lerp(tint, Color.white, 0.12f) : tint);
                    float d = r.height - 10f, x0 = r.x + 5f;
                    if (icon != null)
                    {
                        float grow = hover ? 2f : 0f;
                        var c = new Rect(x0 - grow / 2f, r.center.y - (d + grow) / 2f, d + grow, d + grow);
                        var oc = GUI.color;
                        GUI.color = Faded(Dark(tint, 0.62f));
                        GUI.DrawTexture(c, chip);
                        GUI.color = oc;
                        float isz = Mathf.Round(d * 0.5f);
                        Icon(new Rect(c.center.x - isz / 2f, c.center.y - isz / 2f, isz, isz), icon);
                    }
                    var area = new Rect(icon != null ? x0 + d + 4f : r.x + 12f, r.y, r.xMax - 12f - (icon != null ? x0 + d + 4f : r.x + 12f), r.height);
                    ShadowText(area, text, FitSize(text, fs0, area.width), Color.white);
                    break;
                }
                case 3:  // D · Chamfer: cut corners, uppercase; on hover the bright part slides across
                {
                    Tinted(r, chamfer, Dark(tint, 0.62f));
                    float bright = hover ? 1f : 0.45f + 0.03f * Mathf.Sin(t * 2f);
                    GUI.BeginGroup(new Rect(r.x, r.y, r.width * bright, r.height));
                    Tinted(new Rect(0f, 0f, r.width, r.height), chamfer, hover ? Color.Lerp(tint, Color.white, 0.08f) : tint);
                    GUI.EndGroup();
                    string up = text.ToUpperInvariant();
                    float cs = icon != null ? Mathf.Min(20f, r.height - 14f) : 0f, gap = icon != null ? 10f : 0f;
                    int fs = FitSize(up, fs0 - 3, r.width - 28f - cs - gap);
                    float tw = TextWidth(up, fs, true) - 6f, x = r.center.x - (cs + gap + tw) / 2f;
                    if (icon != null)
                    {
                        var c = new Rect(x, r.center.y - cs / 2f, cs, cs);
                        Tinted(c, chamfer, new Color(0f, 0f, 0f, 0.25f));
                        float isz = Mathf.Round(cs * 0.62f);
                        Icon(new Rect(c.center.x - isz / 2f, c.center.y - isz / 2f, isz, isz), icon);
                    }
                    ShadowText(new Rect(x + cs + gap, r.y, tw + 12f, r.height), up, fs, Color.white, TextAnchor.MiddleLeft);
                    break;
                }
                case 4:  // E · Glow: a dark tinted face, coloured text and icon, a soft glow underneath
                {
                    var bright = Color.Lerp(tint, Color.white, 0.25f);
                    Tinted(new Rect(r.x + 10f, r.y + 8f, r.width - 20f, r.height), capsule, new Color(bright.r, bright.g, bright.b, hover ? 0.32f : 0.18f));
                    var bg = new Color(0.08f, 0.095f, 0.15f);
                    Tinted(r, flat, Color.Lerp(bg, bright, hover ? 0.42f : 0.26f));
                    var fg = hover ? Color.white : Color.Lerp(bright, Color.white, 0.6f);
                    float cs = icon != null ? 18f : 0f, gap = icon != null ? 10f : 0f;
                    int fs = FitSize(text, fs0 - 1, r.width - 24f - cs - gap);
                    float tw = TextWidth(text, fs, false) - 6f, x = r.center.x - (cs + gap + tw) / 2f;
                    if (icon != null)
                        Icon(new Rect(x, r.center.y - cs / 2f, cs, cs), icon, hover ? Color.white : Color.Lerp(bright, Color.white, 0.3f));
                    Label(new Rect(x + cs + gap, r.y, tw + 12f, r.height), text, fs, fg, TextAnchor.MiddleLeft, false);
                    break;
                }
                default:  // F · Split: the icon in its own darker block on the left, the label beside it
                {
                    Tinted(r, flat, hover ? Color.Lerp(tint, Color.white, 0.12f) : tint);
                    float bw = icon != null ? Mathf.Min(r.height, 44f) + (hover ? 8f : 0f) : 0f;
                    if (icon != null)
                    {
                        GUI.BeginGroup(new Rect(r.x, r.y, bw, r.height));
                        Tinted(new Rect(0f, 0f, r.width, r.height), flat, Dark(tint, 0.62f));
                        GUI.EndGroup();
                        float isz = Mathf.Min(18f, r.height - 12f);
                        Icon(new Rect(r.x + bw / 2f - isz / 2f, r.center.y - isz / 2f, isz, isz), icon);
                    }
                    var area = new Rect(r.x + bw + 8f, r.y, r.width - bw - 16f, r.height);
                    ShadowText(area, text, FitSize(text, fs0, area.width), Color.white);
                    break;
                }
            }
            return Ui.HitArea(r);
        }
    }
}
