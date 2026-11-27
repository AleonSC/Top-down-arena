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
    /// BackgroundArena.cs has the reactive ones. Nothing here goes on the wire or into a save.
    public partial class ArenaApp
    {
        const int BgRippleSlots = 8;
        const float BgRippleLife = 2.6f;

        struct BgRipple { public V2 At; public float Born; public float Power; }  // At in screen UI coordinates, Born in Time.time, Power 1 for a click (less for game events)

        readonly BgRipple[] bgRipples = new BgRipple[BgRippleSlots];
        int bgRippleNext;
        int bgReactFrame = -1;
        V2 bgPointerUi, bgPointerPrev;
        float bgEnergy;
        bool menuBgAnimate = true;

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
            BeatTick();  // MenuBeat.cs: the music's spectrum and beats
            var m = Input.mousePosition;
            bgPointerUi = new V2((m.x - offX) / scale, (UnityEngine.Screen.height - m.y - offY) / scale);
            float speed = (bgPointerUi - bgPointerPrev).Length / Mathf.Max(0.001f, Time.unscaledDeltaTime) / 900f;
            bgPointerPrev = bgPointerUi;
            bgEnergy = Mathf.Lerp(bgEnergy, Mathf.Clamp01(speed), 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime));
            if (menuBgAnimate && Input.GetMouseButtonDown(0) && !BgInRun)
                AddRipple(bgPointerUi, time);
        }

        /// During a run the background sits behind the arena: it ignores the pointer (that is your aim) and clicks (your shots) and
        /// only drifts on its own; the arena floor (FloorGridLook.cs) does the reacting.
        bool BgInRun => game != null && game.Running && session.Screen == AppScreen.Game;  // (not Practice behind the lobby)

        void AddRipple(V2 uiAt, float time, float power = 1f)
        {
            bgRipples[bgRippleNext] = new BgRipple { At = uiAt, Born = time, Power = power };
            bgRippleNext = (bgRippleNext + 1) % BgRippleSlots;
        }

        /// The pointer in the background's own coordinates, or false if it isn't over what is being drawn (or the background is STILL).
        bool ReactPointer(out V2 at)
        {
            at = V2.Zero;
            if (!menuBgAnimate || BgInRun || !bgViewRect.Contains(new Vector2(bgPointerUi.X, bgPointerUi.Y)))
                return false;
            at = (bgPointerUi - bgViewOrigin) / bgViewScale;
            return true;
        }

        float ReactEnergy => menuBgAnimate ? bgEnergy : 0f;

        /// Ripple i in background coordinates, and its age in seconds; false for an empty or finished slot.
        bool ReactRipple(int i, float time, out V2 at, out float age)
        {
            var r = bgRipples[i];
            age = time - r.Born;
            at = (r.At - bgViewOrigin) / bgViewScale;
            return r.Born > 0f && age >= 0f && age < BgRippleLife && menuBgAnimate;
        }
    }
}
