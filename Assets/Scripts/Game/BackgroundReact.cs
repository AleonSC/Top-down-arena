using UnityEngine;

namespace Arena
{
    /// The groundwork for reactive menu backgrounds: backgrounds that answer the pointer and clicks. A reactive background is a
    /// rank-reward Background cosmetic whose id starts with "bg-react-" (Rank.IsReactiveBackground; the table is
    /// RewardBackgrounds in MenuBackgrounds.cs). It draws like any other background, and reads its input from here:
    ///   ReactPointer(out V2 at)  the pointer in the background's own (W x H) coordinates; false if it isn't over this view
    ///                            (a thumbnail the pointer is outside, or the background is frozen to STILL)
    ///   ReactEnergy              0..1, how fast the pointer has been moving lately (eased)
    ///   bgRipples                clicks, in screen UI coordinates; use ReactRipple(i, t, out at, out age) to read them in background coordinates
    ///   ReactKick(at, ...)       add a ripple from code (e.g. a future "something happened" hook), at in background coordinates
    ///   ReactStepOnce / ReactClick / ReactRand   for ones that keep state: step a simulation once a frame, read this frame's click
    /// BgRipples below is the reference one: copy its shape for the next. Nothing here goes on the wire or into a save.
    public partial class ArenaApp
    {
        const int BgRippleSlots = 8;
        const float BgRippleLife = 2.6f;

        struct BgRipple { public V2 At; public float Born; }  // At in screen UI coordinates, Born in Time.time

        readonly BgRipple[] bgRipples = new BgRipple[BgRippleSlots];
        int bgRippleNext;
        int bgReactFrame = -1;
        V2 bgPointerUi, bgPointerPrev;
        float bgEnergy;
        int bgClickFrame = -1;  // the frame a click happened, and where (screen UI coordinates)
        V2 bgClickUi;
        uint bgRng = 2463534242u;

        // What the thumbnail being drawn maps to (BackgroundThumb sets these; the full screen is the identity).
        V2 bgViewOrigin = V2.Zero;
        float bgViewScale = 1f;
        Rect bgViewRect = new Rect(0f, 0f, W, H);

        /// Once per frame, however many backgrounds are drawn (thumbnails too): read the pointer and a click.
        void ReactTick(float time)
        {
            if (bgReactFrame == Time.frameCount)
                return;
            bgReactFrame = Time.frameCount;
            var m = Input.mousePosition;
            bgPointerUi = new V2((m.x - offX) / scale, (UnityEngine.Screen.height - m.y - offY) / scale);
            float speed = (bgPointerUi - bgPointerPrev).Length / Mathf.Max(0.001f, Time.unscaledDeltaTime) / 900f;
            bgPointerPrev = bgPointerUi;
            bgEnergy = Mathf.Lerp(bgEnergy, Mathf.Clamp01(speed), 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime));
            if (menuBgAnimate && Input.GetMouseButtonDown(0))
            {
                AddRipple(bgPointerUi, time);
                bgClickFrame = Time.frameCount;
                bgClickUi = bgPointerUi;
            }
        }

        void AddRipple(V2 uiAt, float time)
        {
            bgRipples[bgRippleNext] = new BgRipple { At = uiAt, Born = time };
            bgRippleNext = (bgRippleNext + 1) % BgRippleSlots;
        }

        /// Add a ripple from code; `at` is in the background's own coordinates.
        void ReactKick(V2 at, float time) => AddRipple(bgViewOrigin + at * bgViewScale, time);

        /// The pointer in the background's own coordinates, or false if it isn't over what is being drawn (or the background is STILL).
        bool ReactPointer(out V2 at)
        {
            at = V2.Zero;
            if (!menuBgAnimate || !bgViewRect.Contains(new Vector2(bgPointerUi.X, bgPointerUi.Y)))
                return false;
            at = (bgPointerUi - bgViewOrigin) / bgViewScale;
            return true;
        }

        /// For backgrounds that keep state (a simulation): true once per frame while animating, however many views draw it, with the
        /// frame's dt. Keep `lastFrame` as a field of your own. Not while frozen to STILL, so the picture holds.
        bool ReactStepOnce(ref int lastFrame, out float dt)
        {
            dt = Mathf.Min(0.05f, Time.unscaledDeltaTime);
            if (!menuBgAnimate || lastFrame == Time.frameCount)
                return false;
            lastFrame = Time.frameCount;
            return true;
        }

        /// True on the frame a click happened, with where, in the background's own coordinates (kept on the screen).
        bool ReactClick(out V2 at)
        {
            at = V2.Zero;
            if (!menuBgAnimate || bgClickFrame != Time.frameCount)
                return false;
            at = (bgClickUi - bgViewOrigin) / bgViewScale;
            at = new V2(Mathf.Clamp(at.X, 0f, W), Mathf.Clamp(at.Y, 0f, H));
            return true;
        }

        /// A quick random number for backgrounds' own use (0..1), so they don't touch the game's random state.
        float ReactRand()
        {
            bgRng ^= bgRng << 13;
            bgRng ^= bgRng >> 17;
            bgRng ^= bgRng << 5;
            return (bgRng & 0xFFFFFF) / (float)0x1000000;
        }

        float ReactRange(float a, float b) => a + (b - a) * ReactRand();

        float ReactEnergy => menuBgAnimate ? bgEnergy : 0f;

        /// Ripple i in background coordinates, and its age in seconds; false for an empty or finished slot.
        bool ReactRipple(int i, float time, out V2 at, out float age)
        {
            var r = bgRipples[i];
            age = time - r.Born;
            at = (r.At - bgViewOrigin) / bgViewScale;
            return r.Born > 0f && age >= 0f && age < BgRippleLife && menuBgAnimate;
        }

        /// Ripples (a rank reward, reactive): a dark teal pond; rings spread from where you click and from drops that fall by
        /// themselves, and a soft light follows the pointer, brighter the faster it moves.
        void BgRipples(float t)
        {
            GradQuad(new V2(0, 0), new V2(W, 0), new V2(W, H), new V2(0, H), new Rgba(0.02f, 0.08f, 0.12f), new Rgba(0.03f, 0.16f, 0.2f));
            var ink = new Rgba(0.45f, 0.92f, 0.95f);
            // Drops that fall by themselves, so it is alive with nobody touching it.
            for (int k = 0; k < 3; k++)
            {
                float period = 3.4f + k * 0.7f, ph = MathX.Fmod(t + k * 1.9f, period), n = Mathf.FloorToInt((t + k * 1.9f) / period);
                var at = new V2(W * (0.15f + 0.7f * Hash((int)n * 3 + k)), H * (0.2f + 0.6f * Hash((int)n * 3 + k + 50)));
                Ring(at, ph, 0.55f, ink);
            }
            for (int i = 0; i < BgRippleSlots; i++)
                if (ReactRipple(i, t, out var at, out float age))
                    Ring(at, age, 1f, ink);
            if (ReactPointer(out var p))
            {
                SoftGlow(p, 150f + 160f * ReactEnergy, ink.WithAlpha(0.1f + 0.18f * ReactEnergy));
                Circle(p, 3f, ink.WithAlpha(0.7f), 10);
            }
        }

        /// Three rings spreading from `at`, each fainter and wider than the one before.
        void Ring(V2 at, float age, float strength, Rgba col)
        {
            for (int k = 0; k < 3; k++)
            {
                float a = age - k * 0.22f;
                if (a <= 0f || a >= BgRippleLife)
                    continue;
                float f = a / BgRippleLife, fade = (1f - f) * (1f - f) * strength / (1f + k * 0.6f);
                Arc(at, 14f + a * 190f, 2f, col.WithAlpha(0.6f * fade), 64);
            }
        }
    }
}
