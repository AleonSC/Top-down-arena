using UnityEngine;

namespace Arena
{
    /// Skipping an intro with a key or click (the main menu's pen plot, New run's terminal boot, the profile disk going in or
    /// out) plays a 0.22 s "channel flip" instead of a hard cut (Docs/Mockups/intro-skip-static.html, look B): the finished
    /// screen is grabbed once, then drawn back rolling up into place under a black sync bar, with a colour split,
    /// torn rows and snow that all fade as it settles. Drawn last in OnGUI (under the cursor). Reduced effects or Visual
    /// effects off: the plain cut.
    public partial class ArenaApp
    {
        const float SkipStaticLen = 0.22f;
        const int SkipSnowW = 240, SkipSnowH = 135;
        const float SkipStaticMaxStep = 1f / 60f;  // its clock moves at most this much a frame, so a slow frame (the skip itself, the grab) can't eat it
        float skipStaticT = -1f;           // seconds into the flip; below 0 when none is playing
        bool skipGrabbed;                  // the finished screen has been grabbed for this flip
        static Texture2D skipGrab, skipSnow, skipVignette;
        static Color32[] skipSnowPx;

        /// A skip just happened: start the flip.
        void StartSkipStatic()
        {
            if (fxOn && !fxReduced)
            {
                skipStaticT = 0f;
                skipGrabbed = false;
            }
        }

        /// OnGUI, last thing before the cursor: on the first Repaint grab what was just drawn (once: a grab stalls the frame),
        /// then draw it back mid-flip every frame.
        void DrawSkipStatic()
        {
            if (skipStaticT < 0f || Event.current.type != EventType.Repaint)
                return;
            float s = skipStaticT;
            if (s >= SkipStaticLen)
            {
                skipStaticT = -1f;
                if (skipGrab != null)
                {
                    Object.Destroy(skipGrab);  // a full-screen copy (tens of MB at 4K): free it until the next flip
                    skipGrab = null;
                }
                return;
            }
            skipStaticT += Mathf.Min(Time.unscaledDeltaTime, SkipStaticMaxStep);
            int sw = Screen.width, sh = Screen.height;
            if (sw <= 0 || sh <= 0)
                return;
            if (skipGrab == null || skipGrab.width != sw || skipGrab.height != sh)
            {
                if (skipGrab != null) Object.Destroy(skipGrab);
                skipGrab = new Texture2D(sw, sh, TextureFormat.RGB24, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                skipGrabbed = false;
            }
            if (!skipGrabbed)
            {
                skipGrab.ReadPixels(new Rect(0, 0, sw, sh), 0, 0, false);
                skipGrab.Apply(false);
                skipGrabbed = true;
            }

            var keepM = GUI.matrix;
            var keepC = GUI.color;
            GUI.matrix = Matrix4x4.identity;  // screen pixels from here
            float k = s / SkipStaticLen, e = 1f - Mathf.Pow(1f - k, 3f);  // cubic ease out, as the mockup
            float u = sh / 540f;              // one mockup pixel (the mockup is 960 x 540)
            float roll = (1f - e) * sh * 0.55f, sp = 14f * u * (1f - k), bar = 26f * u;
            var full = new Rect(0, 0, sw, sh);

            GUI.color = Color.black;
            GUI.DrawTexture(full, Texture2D.whiteTexture);
            // The colour split: the screen and a faint copy pushed the other way.
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(-sp, roll, sw, sh), skipGrab);
            GUI.color = new Color(1f, 1f, 1f, 0.3f);
            GUI.DrawTexture(new Rect(sp, roll, sw, sh), skipGrab);
            // The sync bar, and the next frame's top coming up behind it.
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, roll - bar, sw, bar), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(0, roll - bar - sh, sw, sh), skipGrab);
            // Torn rows: thin slices pushed sideways.
            for (int i = 0; i < 7; i++)
            {
                float y = Random.value * sh, h = (4f + Random.value * 14f) * u, off = (Random.value - 0.5f) * 80f * u * (1f - k);
                float src = y - roll;  // where that row is in the rolled screen (or in the copy above it)
                if (src < 0f) src += sh + bar;
                if (src < 0f || src + h > sh)
                    continue;
                // Texture coordinates run from the bottom up.
                GUI.DrawTextureWithTexCoords(new Rect(off, y, sw, h), skipGrab, new Rect(0f, 1f - (src + h) / sh, 1f, h / sh));
            }
            // Snow over it all, thinning out.
            FillSkipSnow();
            GUI.color = new Color(1f, 1f, 1f, 0.85f * (1f - e));
            GUI.DrawTexture(full, skipSnow);
            // Scanlines and dark corners.
            GUI.color = new Color(0f, 0f, 0f, 0.18f);
            for (float y = 0f; y < sh; y += 3f * u)
                GUI.DrawTexture(new Rect(0, y, sw, Mathf.Max(1f, u)), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.DrawTexture(full, SkipVignette());

            GUI.color = keepC;
            GUI.matrix = keepM;
        }

        /// New snow every frame: blue-grey noise with the odd brighter row.
        static void FillSkipSnow()
        {
            if (skipSnow == null)
            {
                skipSnow = new Texture2D(SkipSnowW, SkipSnowH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                skipSnowPx = new Color32[SkipSnowW * SkipSnowH];
            }
            for (int y = 0; y < SkipSnowH; y++)
            {
                float row = Random.value < 0.06f ? 1.35f : 1f;
                for (int x = 0; x < SkipSnowW; x++)
                {
                    float v = Random.value * 255f * row;
                    skipSnowPx[y * SkipSnowW + x] = new Color32((byte)Mathf.Min(255f, v * 0.82f), (byte)Mathf.Min(255f, v * 0.9f), (byte)Mathf.Min(255f, v * 1.05f + 20f), 255);
                }
            }
            skipSnow.SetPixels32(skipSnowPx);
            skipSnow.Apply(false);
        }

        /// The mockup's dark corners: clear out to 35% of the height from the middle, half-black at 95%.
        static Texture2D SkipVignette()
        {
            if (skipVignette != null)
                return skipVignette;
            const int w = 160, h = 90;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - w / 2f) * (x + 0.5f - w / 2f) + (y + 0.5f - h / 2f) * (y + 0.5f - h / 2f)) / h;
                    float a = Mathf.Clamp01((d - 0.35f) / 0.6f) * 0.5f;
                    px[y * w + x] = new Color32(0, 0, 0, (byte)(a * 255f));
                }
            skipVignette = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            skipVignette.SetPixels32(px);
            skipVignette.Apply(false);
            return skipVignette;
        }
    }
}
