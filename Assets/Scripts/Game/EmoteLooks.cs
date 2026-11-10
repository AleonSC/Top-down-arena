using UnityEngine;

namespace Arena
{
    /// How the emotes look (Rank.cs lists them; Docs/Mockups/emotes.html): a bubble over a player's head with a small effect, for
    /// Session.EmoteLife seconds. Drawn in HUD coordinates, from fills and discs like the frames (FrameLooks.cs). With Visual
    /// effects off the movement stops (the bubble's pop-in and the drifting bits) and it shows still.
    public partial class ArenaApp
    {
        /// Draw emote number `index` (Rank.EmoteIndex), `t` seconds in. `head` is where the bubble's tail points (just above the
        /// player's head); `s` scales it (1 in the lobby, a little less in a match).
        void DrawEmote(int index, Vector2 head, float t, float s)
        {
            string id = Rank.EmoteId(index);
            if (id == "")
                return;
            if (!fxOn)
                t = Mathf.Min(t, 0.9f);
            float fadeOut = t > Session.EmoteLife - 0.35f ? Mathf.Max(0f, (Session.EmoteLife - t) / 0.35f) : 1f;
            float oldAlpha = Ui.Alpha;
            Ui.Alpha = oldAlpha * fadeOut;
            switch (id)
            {
                case "emote-wave": EmoteWave(head, t, s); break;
                case "emote-thumbs": EmoteThumbs(head, t, s); break;
                case "emote-gg": EmoteBubbleText(head, t, s, 56f, 40f, new Color(1f, 0.8f, 0.35f), "GG", 22, 1f + Mathf.Max(0f, 0.3f - t) * 1.5f); break;
                case "emote-cheer": EmoteCheer(head, t, s); break;
                case "emote-heart": EmoteHeart(head, t, s); break;
                case "emote-laugh": EmoteBubbleText(head, t, s, 74f, 40f, new Color(1f, 0.71f, 0.28f), "HA HA", 16, 1f, Mathf.Sin(t * 34f) * 1.2f); break;
                case "emote-question": EmoteBubbleText(head, t, s, 42f, 44f, new Color(0.44f, 0.89f, 0.77f), "?", 26, 1f, Mathf.Sin(t * 5f) * 2f); break;
                case "emote-sleep": EmoteSleep(head, t, s); break;
                case "emote-dance": EmoteDance(head, t, s); break;
                case "emote-spin": EmoteSpin(head, t, s); break;
                case "emote-salute": EmoteSalute(head, t, s); break;
                case "emote-fireworks": EmoteFireworks(head, t, s); break;
                case "emote-steam": EmoteSteam(head, t, s); break;
            }
            Ui.Alpha = oldAlpha;
        }

        // --- The body ---------------------------------------------------------------------------------------------

        int emoteBodyFor = -1;  // the player id whose model is being drawn now (-1: none; the lobby sets it for its characters)

        /// How far an emote moves the player's model (in world units: a hop, a sway, a shake) and how far it turns it, for the
        /// player being drawn. The footprint, hitbox ring and name stay put; only the model moves. Zero with Visual effects off.
        bool EmoteBody(int playerId, out V2 off, out float spin)
        {
            off = V2.Zero;
            spin = 0f;
            int index;
            float t;
            if (!fxOn || !session.EmoteOf(playerId, out index, out t))
                return false;
            float fade = t > Session.EmoteLife - 0.35f ? Mathf.Max(0f, (Session.EmoteLife - t) / 0.35f) : 1f;
            switch (Rank.EmoteId(index))
            {
                case "emote-wave": off = new V2(Mathf.Sin(t * 3f) * 2f, 0f); break;
                case "emote-thumbs": off = new V2(0f, -Mathf.Sin(Mathf.Min(t, 0.4f) / 0.4f * 3.14159f) * 4f); break;
                case "emote-cheer": off = new V2(0f, -Mathf.Abs(Mathf.Sin(t * 6f)) * 7f * fade); break;
                case "emote-laugh": off = new V2(Mathf.Sin(t * 34f) * 1.6f * fade, -Mathf.Abs(Mathf.Sin(t * 12f)) * 2f); break;
                case "emote-sleep": off = new V2(0f, Mathf.Min(t, 0.6f) / 0.6f * 4f * fade); break;
                case "emote-dance": off = new V2(Mathf.Sin(t * 7f) * 6f * fade, -Mathf.Abs(Mathf.Sin(t * 7f)) * 5f * fade); break;
                case "emote-spin":
                {
                    float q = Mathf.Min(1f, t / 1.1f);
                    spin = (1f - (1f - q) * (1f - q) * (1f - q)) * 12.566f;  // two full turns, easing out
                    break;
                }
                case "emote-salute": off = new V2(0f, -Mathf.Min(t, 0.2f) * 10f * fade); break;  // snaps up straight
                case "emote-steam": off = new V2(Mathf.Sin(t * 40f) * 1.2f * fade, 0f); break;
                default: return false;
            }
            return true;
        }

        // --- Pieces ---------------------------------------------------------------------------------------------

        /// A point in the emote's own units (x across, y up from the player's centre; the bubble sits about 54 up), in HUD pixels.
        static Vector2 EP(Vector2 head, float s, float dx, float dy) => new Vector2(head.x + dx * s, head.y + (dy + 26f) * s);

        static float EmotePop(float t)
        {
            const float d = 0.25f;
            if (t >= d)
                return 1f;
            float x = t / d, e = 1f - (1f - x) * (1f - x) * (1f - x);
            return e * 1.12f - (x > 0.7f ? 0.12f * (x - 0.7f) / 0.3f : 0f);
        }

        /// The bubble (a card with a little tail); returns its centre.
        static Vector2 EmoteBubble(Vector2 head, float t, float s, float w, float h, Color tint)
        {
            float pop = Mathf.Max(0.01f, EmotePop(t)), ww = w * s * pop, hh = h * s * pop;
            var r = new Rect(head.x - ww / 2f, head.y - 8f * s * pop - hh, ww, hh);
            DkBox(r, tint, 0.95f);
            for (int i = 0; i < 4; i++)
                Ui.Fill(new Rect(head.x - (5f - 1.3f * i) * s * pop, r.yMax - 1f + i * 2f * s * pop, (10f - 2.6f * i) * s * pop, 2f * s * pop), new Color(0.04f, 0.055f, 0.095f, 0.94f));
            return r.center;
        }

        void EmoteBubbleText(Vector2 head, float t, float s, float w, float h, Color tint, string text, int size, float k = 1f, float shake = 0f)
        {
            var c = EmoteBubble(head, t, s, w, h, tint);
            float pop = Mathf.Min(1f, EmotePop(t));
            Ui.Label(new Rect(c.x - w * s / 2f + shake * s, c.y - 14f * s, w * s, 28f * s), text, Mathf.Max(8, Mathf.RoundToInt(size * s * k * pop)), tint, TextAnchor.MiddleCenter, true);
        }

        static void EDot(Vector2 p, float r, Color c) => Ui.Fill(new Rect(p.x - r, p.y - r, r * 2f, r * 2f), c);

        /// A little picture from rows of # and spaces, centred on p, each cell `px` big.
        static void ESprite(string[] rows, Vector2 p, float px, Color c)
        {
            float x0 = p.x - rows[0].Length * px / 2f, y0 = p.y - rows.Length * px / 2f;
            for (int y = 0; y < rows.Length; y++)
                for (int x = 0; x < rows[y].Length; x++)
                    if (rows[y][x] == '#')
                        Ui.Fill(new Rect(x0 + x * px, y0 + y * px, px + 0.5f, px + 0.5f), c);
        }

        static readonly string[] HeartPix = { " ## ## ", "#######", "#######", " ##### ", "  ###  ", "   #   " };
        static readonly string[] StarPix = { "   #   ", "   #   ", "#######", " ##### ", "  ###  ", " ## ## ", "##   ##" };

        // --- The thirteen -----------------------------------------------------------------------------------------

        void EmoteWave(Vector2 head, float t, float s)
        {
            var c = EmoteBubble(head, t, s, 50f, 44f, new Color(0.5f, 0.7f, 1f));
            var skin = new Color(1f, 0.85f, 0.63f);
            float sway = Mathf.Sin(t * 9f) * 3f * s;  // (no rotation in IMGUI: the fingers lean instead)
            Ui.Fill(new Rect(c.x - 7f * s + sway * 0.3f, c.y - 2f * s, 14f * s, 13f * s), skin);
            for (int i = 0; i < 4; i++)
                Ui.Fill(new Rect(c.x - 7f * s + i * 3.6f * s + sway, c.y - (11f - Mathf.Abs(i - 1.5f)) * s, 3f * s, 11f * s), skin);
            Ui.Fill(new Rect(c.x - 11f * s + sway * 0.3f, c.y + 2f * s, 5f * s, 4f * s), skin);
        }

        void EmoteThumbs(Vector2 head, float t, float s)
        {
            var c = EmoteBubble(head, t, s, 50f, 44f, new Color(0.44f, 0.89f, 0.66f));
            var skin = new Color(1f, 0.85f, 0.63f);
            float rise = (1f - Mathf.Min(1f, EmotePop(t))) * 8f * s;
            Ui.Fill(new Rect(c.x - 8f * s, c.y - 1f * s + rise, 16f * s, 13f * s), skin);
            Ui.Fill(new Rect(c.x - 3f * s, c.y - 12f * s + rise, 6f * s, 12f * s), skin);
            Ui.Fill(new Rect(c.x - 13f * s, c.y - 1f * s + rise, 4f * s, 13f * s), new Color(0.16f, 0.23f, 0.33f));
            if (t > 0.3f && t < 0.9f)
                ESprite(StarPix, EP(head, s, 15f, -70f), Mathf.Max(0.5f, 1.2f * s * (1f - (t - 0.3f) / 0.6f)), new Color(1f, 1f, 1f, 0.9f));
        }

        void EmoteCheer(Vector2 head, float t, float s)
        {
            if (t > 0.2f)
                for (int i = 0; i < 22; i++)
                {
                    float a = FrameHash(i) * 6.2832f, sp = 40f + FrameHash(i + 4) * 60f, q = Mathf.Min(1f, (t - 0.1f) / 1.6f);
                    if (q >= 1f)
                        continue;
                    var col = Color.HSVToRGB(FrameHash(i + 9), 0.85f, 1f);
                    Ui.Fill(new Rect(head.x + (Mathf.Cos(a) * sp * q - 2.5f) * s, head.y + (-8f + Mathf.Sin(a) * sp * q + q * q * 60f) * s, 5f * s, 3f * s), new Color(col.r, col.g, col.b, 1f - q));
                }
            EmoteBubbleText(head, Mathf.Min(t, 0.4f), s, 44f, 36f, new Color(1f, 0.56f, 0.82f), "!", 24);
        }

        void EmoteHeart(Vector2 head, float t, float s)
        {
            var c = EmoteBubble(head, t, s, 50f, 44f, new Color(1f, 0.42f, 0.54f));
            ESprite(HeartPix, c, 2.2f * s * (1f + 0.1f * Mathf.Sin(t * 10f)), new Color(1f, 0.31f, 0.48f));
            if (t > 0.3f)
                for (int i = 0; i < 5; i++)
                {
                    float q = (t * 0.6f + FrameHash(i)) % 1f;
                    ESprite(HeartPix, EP(head, s, (FrameHash(i + 3) - 0.5f) * 50f + Mathf.Sin(q * 6f + i) * 5f, -70f - q * 40f), s * (0.5f + FrameHash(i) * 0.3f), new Color(1f, 0.43f, 0.59f, 1f - q));
                }
        }

        void EmoteSleep(Vector2 head, float t, float s)
        {
            for (int i = 0; i < 3; i++)
            {
                float q = (t * 0.5f + i / 3f) % 1f, a = Mathf.Min(1f, q * 3f) * (1f - q);
                var p = EP(head, s, 12f + i * 8f + Mathf.Sin(q * 6f) * 3f, -24f - q * 44f);
                Ui.Label(new Rect(p.x - 20f, p.y - 12f, 40f, 24f), "z", Mathf.RoundToInt((11f + i * 4f) * s), new Color(0.6f, 0.71f, 1f, a), TextAnchor.MiddleCenter, true);
            }
        }

        void EmoteDance(Vector2 head, float t, float s)
        {
            var cols = new[] { new Color(1f, 0.56f, 0.82f), new Color(0.5f, 0.89f, 1f), new Color(1f, 0.83f, 0.42f), new Color(0.65f, 1f, 0.56f) };
            for (int i = 0; i < 4; i++)
            {
                float q = (t * 0.7f + i * 0.25f) % 1f;
                var c = cols[i];
                c.a = 1f - q;
                var p = EP(head, s, (i % 2 == 1 ? 1f : -1f) * (14f + q * 20f), -24f - q * 40f);
                EDot(p, 3.5f * s, c);
                Ui.Fill(new Rect(p.x + 2.4f * s, p.y - 12f * s, 1.6f * s, 12f * s), c);
            }
        }

        void EmoteSpin(Vector2 head, float t, float s)
        {
            float q = Mathf.Min(1f, t / 1.1f);
            if (q >= 1f)
                return;
            float a0 = (1f - (1f - q) * (1f - q) * (1f - q)) * 12.566f;
            for (int ring = 0; ring < 2; ring++)
                for (int i = 0; i < 14; i++)
                {
                    float a = a0 + ring * 3.14159f + i * 0.08f, rad = (ring == 0 ? 22f + q * 20f : 16f + q * 14f);
                    EDot(EP(head, s, Mathf.Cos(a) * rad, Mathf.Sin(a) * rad), 1.8f * s, new Color(0.6f, 0.71f, 1f, (1f - q) * (1f - i / 16f)));
                }
        }

        void EmoteSalute(Vector2 head, float t, float s)
        {
            var c = EmoteBubble(head, t, s, 46f, 44f, new Color(1f, 0.8f, 0.35f));
            ESprite(StarPix, c, 2.6f * s * (1f + 0.1f * Mathf.Sin(t * 6f)), new Color(1f, 0.8f, 0.35f));
        }

        void EmoteFireworks(Vector2 head, float t, float s)
        {
            var cols = new[] { new Color(1f, 0.42f, 0.54f), new Color(1f, 0.8f, 0.35f), new Color(0.5f, 0.89f, 1f) };
            for (int k = 0; k < 3; k++)
            {
                float q = (t - (0.15f + k * 0.45f)) / 1.1f;
                if (q < 0f || q > 1f)
                    continue;
                var c = cols[k];
                c.a = 1f - q;
                for (int i = 0; i < 14; i++)
                {
                    float a = i / 14f * 6.2832f, r = q * 26f;
                    EDot(EP(head, s, (k - 1) * 34f + Mathf.Cos(a) * r, -64f - (k % 2) * 12f + Mathf.Sin(a) * r + q * q * 8f), 2.1f * s * (1f - q * 0.5f), c);
                }
            }
        }

        void EmoteSteam(Vector2 head, float t, float s)
        {
            for (int i = 0; i < 6; i++)
            {
                float q = (t * 1.1f + i / 6f) % 1f;
                EDot(EP(head, s, (i % 2 == 1 ? 1f : -1f) * (14f + q * 12f), -14f - q * 30f), (3f + q * 7f) * s, new Color(0.91f, 0.93f, 0.96f, (1f - q) * 0.55f));
            }
            EmoteBubbleText(head, t, s, 42f, 40f, new Color(1f, 0.35f, 0.35f), "#!", 17, 1f, Mathf.Sin(t * 40f) * 1.2f);
        }
    }
}
