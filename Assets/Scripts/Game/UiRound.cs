using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Rounded boxes of any corner radius, flat or with a vertical gradient, and a soft round blur: the machine look of
    /// the Who's playing? screen and the main menu (Docs/Mockups/disk-boot-sequence.html, MachineLook.cs).
    public static partial class Ui
    {
        static readonly Dictionary<int, GUIStyle> roundStyles = new Dictionary<int, GUIStyle>();

        /// A flat rounded box with corners of the given radius (9-sliced, so any size).
        public static void RoundBox(Rect r, Color c, int radius)
        {
            if (r.width <= 0f || r.height <= 0f)
                return;
            radius = Mathf.Clamp(radius, 1, 24);
            if (!roundStyles.TryGetValue(radius, out var st) || st.normal.background == null)
                roundStyles[radius] = st = Style(Rounded(radius, Color.white, Color.white, new Color(1f, 1f, 1f, 0f), 0f), radius);
            var old = GUI.backgroundColor;
            GUI.backgroundColor = Faded(c);
            GUI.Box(r, GUIContent.none, st);
            GUI.backgroundColor = old;
        }

        static readonly Dictionary<int, GUIStyle> rimStyles = new Dictionary<int, GUIStyle>();

        /// The outline of a rounded box: a line `width` px (1 to 6) along its inside edge, following the corners.
        public static void RoundRim(Rect r, Color c, int radius, int width)
        {
            if (r.width <= 0f || r.height <= 0f || c.a <= 0.003f)
                return;
            radius = Mathf.Clamp(radius, 1, 32);
            width = Mathf.Clamp(width, 1, 6);
            int key = radius * 8 + width;
            if (!rimStyles.TryGetValue(key, out var st) || st.normal.background == null)
                rimStyles[key] = st = Style(Rounded(radius, new Color(1f, 1f, 1f, 0f), new Color(1f, 1f, 1f, 0f), Color.white, width), radius);
            var old = GUI.backgroundColor;
            GUI.backgroundColor = Faded(c);
            GUI.Box(r, GUIContent.none, st);
            GUI.backgroundColor = old;
        }

        /// A rounded box filled with a vertical gradient through the given stops (positions 0 to 1, colours), drawn one
        /// row at a time with each row cut in at the corners, so the curve stays round.
        public static void RoundGrad(Rect r, float radius, float[] at, Color[] cols)
        {
            if (r.width <= 0f || r.height <= 0f)
                return;
            radius = Mathf.Min(radius, Mathf.Min(r.width, r.height) / 2f);
            int rows = Mathf.Max(2, Mathf.CeilToInt(r.height / 2f));
            float h = r.height / rows;
            for (int i = 0; i < rows; i++)
            {
                float y = i * h, mid = y + h / 2f, k = mid / r.height;
                float inset = 0f;
                float dy = mid < radius ? radius - mid : mid > r.height - radius ? mid - (r.height - radius) : 0f;
                if (dy > 0f)
                    inset = radius - Mathf.Sqrt(Mathf.Max(0f, radius * radius - dy * dy));
                Fill(new Rect(r.x + inset, r.y + y, r.width - 2f * inset, h + 0.5f), GradAt(k, at, cols));
            }
        }

        static Color GradAt(float k, float[] at, Color[] cols)
        {
            if (k <= at[0])
                return cols[0];
            for (int i = 1; i < at.Length; i++)
                if (k <= at[i])
                    return Color.Lerp(cols[i - 1], cols[i], (k - at[i - 1]) / Mathf.Max(0.0001f, at[i] - at[i - 1]));
            return cols[cols.Length - 1];
        }
    }
}
