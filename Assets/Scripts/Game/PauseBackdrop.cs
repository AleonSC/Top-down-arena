using UnityEngine;

namespace Arena
{
    /// Behind the pause menu: the arena darkens over the first 1.5 s the menu is open (0.5 to 0.8 black) under a faint, moving
    /// static (sparse blue-white grains, four pre-made frames cycled, and a soft band rolling down). Reduced effects or Visual effects
    /// off: the darkening only. Fades with the menu (WindowVis).
    public partial class ArenaApp
    {
        const int PauseSnowW = 320, PauseSnowH = 180, PauseSnowFrames = 4;
        static Texture2D[] pauseSnow;

        void DrawPauseBackdrop()
        {
            if (Event.current.type != EventType.Repaint)
                return;
            float vis = WindowVis;
            float k = EaseOut(Mathf.Clamp01(WinT / 1.5f));
            Ui.Fill(new Rect(0, 0, W, H), new Color(0.005f, 0.008f, 0.02f, Mathf.Lerp(0.5f, 0.8f, k) * vis));
            if (FxCalm)
                return;
            var snow = PauseSnow();
            float now = WinNow;
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, (0.03f + 0.07f * k) * vis);
            GUI.DrawTexture(new Rect(0, 0, W, H), snow[Mathf.FloorToInt(now * 16f) % PauseSnowFrames]);
            GUI.color = old;
            // a soft brighter band rolling down the screen every 4 s
            float by = Mathf.Lerp(-120f, H, MathX.Fmod(now, 4f) / 4f);
            for (int i = 0; i < 12; i++)
            {
                float a = Mathf.Sin((i + 0.5f) / 12f * Mathf.PI) * 0.025f * k * vis;
                Ui.Fill(new Rect(0, by + i * 10f, W, 10f), new Color(0.6f, 0.75f, 1f, a));
            }
        }

        /// The static's frames, made once: about one pixel in six lit, at a random brightness.
        static Texture2D[] PauseSnow()
        {
            if (pauseSnow != null && pauseSnow[0] != null)
                return pauseSnow;
            pauseSnow = new Texture2D[PauseSnowFrames];
            var rng = new System.Random(7);
            var px = new Color32[PauseSnowW * PauseSnowH];
            for (int f = 0; f < PauseSnowFrames; f++)
            {
                for (int i = 0; i < px.Length; i++)
                {
                    if (rng.NextDouble() < 0.16)
                    {
                        byte v = (byte)(150 + rng.Next(106));
                        px[i] = new Color32((byte)(v * 0.85f), (byte)(v * 0.92f), v, (byte)(120 + rng.Next(136)));
                    }
                    else
                        px[i] = new Color32(0, 0, 0, 0);
                }
                var t = new Texture2D(PauseSnowW, PauseSnowH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                t.SetPixels32(px);
                t.Apply(false);
                pauseSnow[f] = t;
            }
            return pauseSnow;
        }
    }
}
