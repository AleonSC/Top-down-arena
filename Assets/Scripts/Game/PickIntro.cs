using UnityEngine;

namespace Arena
{
    /// The upgrade picker opening ("out of you", Docs/Mockups/upgrade-picker-dropin.html): a gold pillar shoots up out
    /// of your character with a ring, LEVEL UP! pops in the middle of the screen and slides up to its place, then the
    /// cards fly out of you along arcs, small and turning, opening out as they land with a ring in their rarity colour.
    /// The Reroll and Take buttons rise from the bottom. Cards can't be chosen until they've landed (PickIntroPlaying).
    /// A reroll, or the next pick straight after one, skips the pillar and the title: just the cards fly out.
    /// Picking plays the same flight backwards (PickOutro.cs).
    public partial class ArenaApp
    {
        const float IntroPillarEnd = 0.55f, IntroTitlePop = 0.3f, IntroTitleUp = 0.6f;
        const float IntroCardFly = 0.45f, IntroCardGap = 0.1f, IntroRingTime = 0.4f;
        bool introFull;   // this opening has the pillar and the title (not a reroll, nor the next of several picks)

        /// Seconds since the current cards appeared (offerShownAt is set by SyncSelection).
        float IntroElapsed => Time.unscaledTime - offerShownAt;

        /// When the cards start flying out, after the title has moved up (a full opening) or at once.
        float IntroCardsAt => introFull ? IntroTitleUp - 0.1f : 0f;

        /// How long until card i has landed.
        float IntroLanded(int i) => IntroCardsAt + Mathf.Min(i, 5) * IntroCardGap + IntroCardFly;

        /// Still opening: the cards can't be picked yet (keys and clicks wait for the last card to land).
        bool PickIntroPlaying(Player me) => me != null && me.Offer != null && IntroElapsed < IntroLanded(me.Offer.Length - 1);

        /// Called when new cards appear (SyncSelection): a full opening unless the cards were rerolled, or the last
        /// pick's close has only just ended (several picks in a row open one after another without the fanfare).
        void StartPickIntro(bool reroll)
        {
            introFull = !reroll && Time.unscaledTime - pickOutroAt > PickOutroTime + 0.25f;
        }

        static float EaseOut(float f) => 1f - (1f - f) * (1f - f) * (1f - f);
        static float EaseBack(float f) { const float c = 1.70158f; float q = f - 1f; return 1f + (c + 1f) * q * q * q + c * q * q; }

        /// Where the picker's cards fly from and back to: your character, on the HUD canvas (the bottom middle if you're down).
        V2 PickHome(Player me) => me == null || me.Downed ? new V2(W / 2f, H - 140f) : ToUi(me.Disp);

        /// A point on card i's flight between home (f = 0) and its slot's middle (f = 1): an arc, higher for later cards.
        static V2 PickArc(V2 home, Vector2 slot, int i, float f)
        {
            float lift = (160f + 60f * (i % 3)) * Mathf.Sin(Mathf.PI * f);
            return new V2(Lerp(home.X, slot.x, f), Lerp(home.Y, slot.y, f) - lift);
        }

        /// Draws `draw` (a card, in its slot) at flight point f of its arc: small and turned edge-on near home, full size
        /// and flat in its slot. Uses GUI.matrix, so the card's own drawing is unchanged.
        void DrawCardInFlight(V2 home, Rect slot, int i, float f, float alpha, System.Action draw)
        {
            if (f >= 1f && alpha >= 1f)
            {
                draw();
                return;
            }
            float k = Lerp(0.12f, 1f, f), sx = k * Mathf.Abs(Mathf.Cos((1f - f) * Mathf.PI * 1.5f)) + 0.02f;
            V2 at = PickArc(home, slot.center, i, f);
            var oldMatrix = GUI.matrix;
            var oldColor = GUI.color;
            GUI.matrix = Matrix4x4.TRS(new Vector3(offX + scale * (at.X - slot.center.x * sx), offY + scale * (at.Y - slot.center.y * k), 0f), Quaternion.identity, new Vector3(scale * sx, scale * k, 1f));
            GUI.color = new Color(oldColor.r, oldColor.g, oldColor.b, oldColor.a * alpha);
            Ui.Alpha = alpha;
            draw();
            Ui.Alpha = 1f;
            GUI.color = oldColor;
            GUI.matrix = oldMatrix;
        }

        /// A streak of light trailing a card along its arc, behind flight point f (the 30% of the path before it).
        void PickStreak(V2 home, Vector2 slot, int i, float f, bool outward, Rgba col, float alpha)
        {
            if (f <= 0f || f >= 1f)
                return;
            V2 prev = PickArc(home, slot, i, f);
            for (int s = 1; s <= 12; s++)
            {
                float g = Mathf.Clamp01(outward ? f - s * 0.025f : f + s * 0.025f);
                V2 pt = PickArc(home, slot, i, g);
                Line(prev, pt, 6f - s * 0.35f, col.WithAlpha(alpha * (1f - s / 13f)));
                prev = pt;
            }
        }

        /// Your character's body radius on the HUD canvas, for the pillar's foot and halo.
        float PickHomeR(Player me) => (me != null ? me.Def.BodyR : 16f) * WorldToUi;

        /// A pillar of gold light rising out of a character (k: 0..1, it shoots up, then thins and fades). Its foot
        /// ("soft fade", Docs/Mockups/pillar-foot.html option 2) stops just above their middle and fades out over the
        /// last two body radii, so it has no edge, and a gold halo rings them while it's up. bodyR: their radius.
        void PickPillar(V2 foot, float k, float wide, float bodyR)
        {
            if (k <= 0f || k >= 1f)
                return;
            float a = Mathf.Sin(Mathf.PI * k), w = wide * (1f - k * 0.6f), footY = foot.Y - bodyR * 0.2f, fadeLen = bodyR * 2.1f;
            float top = Lerp(footY, -20f, EaseOut(Mathf.Min(1f, k * 2.5f)));
            var gold = new Rgba(1f, 0.8f, 0.35f);
            const int bands = 10, sub = 4;  // each band split, so the foot's fade is smooth
            for (int b = 0; b < bands; b++)  // brightest near the foot, fading toward the top
            {
                float fade = 1f - b / (float)bands;
                for (int s = 0; s < sub; s++)
                {
                    float y0 = Lerp(footY, top, (b + s / (float)sub) / bands), y1 = Lerp(footY, top, (b + (s + 1f) / sub) / bands);
                    float soft = Mathf.Clamp01((footY - (y0 + y1) / 2f) / fadeLen);
                    if (soft <= 0f)
                        continue;
                    SetColor(gold.WithAlpha(0.6f * a * fade * soft));
                    Quad(new V2(foot.X - w / 2f, y0), new V2(foot.X + w / 2f, y0), new V2(foot.X + w / 2f, y1), new V2(foot.X - w / 2f, y1));
                    SetColor(new Rgba(1f, 1f, 1f, a * fade * soft));
                    Quad(new V2(foot.X - w * 0.12f, y0), new V2(foot.X + w * 0.12f, y0), new V2(foot.X + w * 0.12f, y1), new V2(foot.X - w * 0.12f, y1));
                }
            }
            Arc(foot, bodyR + 5.5f, 1.8f, gold.WithAlpha(0.85f * a), 32);  // the halo
            Arc(foot, bodyR + 9.5f, 1f, gold.WithAlpha(0.3f * a), 32);
        }

        /// GL under the opening cards (call before them, on the HUD canvas): the pillar and ring at your feet, and a
        /// streak behind each card in flight.
        void DrawPickIntroUnder(Player me, Rect[] slots, int n)
        {
            float e = IntroElapsed;
            if (e > IntroLanded(n - 1) + IntroRingTime || !BeginUiGl())
                return;
            UiModelAt(V2.Zero, 1f);
            V2 home = PickHome(me);
            if (introFull)
            {
                PickPillar(home, Seg(e, 0f, IntroPillarEnd), 44f, PickHomeR(me));
                float g = Seg(e, 0.05f, IntroPillarEnd);
                if (g > 0f && g < 1f)
                    Arc(home, 20f + 110f * EaseOut(g), 4f, new Rgba(1f, 0.8f, 0.35f, 1f - g), 40);
            }
            for (int i = 0; i < n; i++)
            {
                float a = IntroCardsAt + Mathf.Min(i, 5) * IntroCardGap, f = EaseOut(Seg(e, a, a + IntroCardFly));
                PickStreak(home, slots[i].center, i, f, true, PickColor(me, i), 0.45f);
            }
            EndUiGl();
        }

        /// GL over the landed cards: a ring in each card's rarity colour as it lands.
        void DrawPickIntroOver(Player me, Rect[] slots, int n)
        {
            float e = IntroElapsed;
            if (e > IntroLanded(n - 1) + IntroRingTime || !BeginUiGl())
                return;
            UiModelAt(V2.Zero, 1f);
            for (int i = 0; i < n; i++)
            {
                float g = Seg(e, IntroLanded(i), IntroLanded(i) + IntroRingTime);
                if (g > 0f && g < 1f)
                {
                    var c = slots[i];
                    float rr = Mathf.Max(c.width, c.height) * 0.3f;
                    Arc(new V2(c.center.x, c.center.y), rr + rr * 0.8f * g, 4f, PickColor(me, i).WithAlpha(1f - g), 48);
                }
            }
            EndUiGl();
        }

        static Rgba PickColor(Player me, int i) => me.Offer != null && i < me.Offer.Length ? Upgrades.RarityColor(Upgrades.All[me.Offer[i]].Rarity) : new Rgba(1f, 0.8f, 0.35f);

        /// The title during a full opening: popping in large in the middle on a dark band (like the run launch's GET
        /// READY), then sliding up to its place. Returns its y and font size, and how much of the band to show.
        void PickTitleMotion(float restY, int restSize, out float y, out int size, out float band)
        {
            if (!introFull)
            {
                y = restY;
                size = restSize;
                band = 0f;
                return;
            }
            float e = IntroElapsed, pop = Seg(e, 0f, IntroTitlePop), up = EaseOut(Seg(e, IntroTitlePop + 0.05f, IntroTitleUp));
            y = Lerp(H / 2f - 60f, restY, up);
            size = Mathf.RoundToInt(restSize * Lerp(1.7f, 1f, EaseBack(pop)) * Lerp(1.15f, 1f, up));
            band = Mathf.Min(1f, pop * 1.5f) * (1f - up);
        }

        /// How far below its place the bottom row (what the pick changes, Reroll, Take, the hint) still is, and its alpha.
        float PickButtonsRise(Player me, out float alpha)
        {
            float last = IntroLanded(me.Offer.Length - 1), k = EaseOut(Seg(IntroElapsed, last - 0.35f, last + 0.05f));
            alpha = k;
            return (1f - k) * 140f;
        }
    }
}
