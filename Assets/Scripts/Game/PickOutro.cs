using UnityEngine;

namespace Arena
{
    /// What happens on screen when you take an upgrade ("back into you", Docs/Mockups/upgrade-picker-dropin.html close 1):
    /// the picker's opening played backwards (PickIntro.cs). The cards you passed on dim, fold shut and fly back into you
    /// along the arcs they came out on; the one you took flashes, follows them in last and lands with a ring in its rarity
    /// colour, a short gold pillar, and its icon popping up over your head.
    /// It plays over whatever comes next (the next pick waits until it's done; see PickOutroPlaying).
    public partial class ArenaApp
    {
        const float PickOutroTime = 0.95f;
        const float OutroFlash = 0.15f, OutroOthersFly = 0.4f, OutroPickFrom = 0.18f, OutroPickLand = 0.6f;

        float pickOutroAt = -10f;
        Rect[] pickCardRects = new Rect[0];   // where the picker last drew each card (DrawUpgradePicker fills it)
        Rect[] outroRects = new Rect[0];
        Rgba[] outroColors = new Rgba[0];
        int[] outroOffer = new int[0];         // the cards as they were (the offer changes as soon as the pick is sent)
        bool outroGrid;                        // weapon mods: the small cards
        int outroPicked = -1;
        Texture2D outroIcon;
        V2 outroTarget;
        bool outroDim;                         // the picker's dark backdrop fades out with it (no more picks waiting)

        bool PickOutroPlaying => Time.unscaledTime - pickOutroAt < PickOutroTime;

        /// Called by ConfirmPick before the pick is sent: remembers the cards as they were drawn.
        void StartPickOutro(Player me, int picked)
        {
            if (me == null || me.Offer == null || picked < 0 || picked >= me.Offer.Length || pickCardRects.Length < me.Offer.Length)
                return;
            int n = me.Offer.Length;
            outroRects = new Rect[n];
            outroColors = new Rgba[n];
            outroOffer = (int[])me.Offer.Clone();
            outroGrid = n > 3;
            for (int i = 0; i < n; i++)
            {
                outroRects[i] = pickCardRects[i];
                outroColors[i] = Upgrades.RarityColor(Upgrades.All[me.Offer[i]].Rarity);
            }
            outroPicked = picked;
            outroIcon = PixelArt.ForUpgrade(Upgrades.All[me.Offer[picked]].Name);
            outroTarget = PickHome(me);
            outroDim = me.PendingPicks + me.PendingLegendary + me.PendingMods <= 1;
            pickOutroAt = Time.unscaledTime;
        }

        static float EaseIn(float f) => f * f * f;
        static float Lerp(float a, float b, float f) => a + (b - a) * f;

        /// One of the remembered cards, drawn in its slot as the picker draws it.
        void DrawOutroCard(Player me, int i, bool selected, float now)
        {
            var u = Upgrades.All[outroOffer[i]];
            if (outroGrid)
                DrawUpgradeCard(me, i, u, outroRects[i], true, selected, false, now);
            else
                DrawTrophyCard(me, i, u, outroRects[i], selected, !selected, false, now);
        }

        /// Draws the close if one is playing (call after the HUD, so it sits on top).
        void DrawPickOutro()
        {
            float e = Time.unscaledTime - pickOutroAt;
            var me = game.Me;
            if (e < 0f || e >= PickOutroTime || outroRects.Length == 0 || me == null)
                return;
            float now = Time.unscaledTime;
            if (outroDim)  // the picker's backdrop lets go of the arena
                Ui.Fill(new Rect(0, 0, W, H), new Color(0.01f, 0.012f, 0.03f, 0.82f * (1f - Mathf.Clamp01(e / 0.45f))));

            // Each card's place on its way home (1 = in its slot, 0 = in you).
            int n = outroRects.Length;
            var flight = new float[n];
            for (int i = 0, j = 0; i < n; i++)
            {
                if (i == outroPicked)
                    flight[i] = 1f - EaseIn(Seg(e, OutroPickFrom, OutroPickLand));
                else
                {
                    float a = 0.06f * Mathf.Min(j++, 4);
                    flight[i] = 1f - EaseIn(Seg(e, a, a + OutroOthersFly));
                }
            }

            // Streaks behind the cards in flight (GL, under them).
            if (BeginUiGl())
            {
                UiModelAt(V2.Zero, 1f);
                for (int i = 0; i < n; i++)
                    PickStreak(outroTarget, outroRects[i].center, i, flight[i], false, outroColors[i], i == outroPicked ? 0.45f : 0.3f);
                EndUiGl();
            }

            // The ones you passed on, dimmed, then the one you took (it goes in last, so it's drawn on top).
            for (int i = 0; i < n; i++)
            {
                if (i == outroPicked || flight[i] <= 0f)
                    continue;
                int k = i;
                DrawCardInFlight(outroTarget, outroRects[i], i, flight[i], 0.55f * Mathf.Min(1f, flight[i] * 3f), () => DrawOutroCard(me, k, false, now));
            }
            if (flight[outroPicked] > 0f)
            {
                var r = outroRects[outroPicked];
                DrawCardInFlight(outroTarget, r, outroPicked, flight[outroPicked], 1f, () => DrawOutroCard(me, outroPicked, true, now));
                float flash = Seg(e, 0f, OutroFlash);
                if (flash < 1f)
                    Ui.Pill(new Rect(r.x - 6f, r.y - 6f, r.width + 12f, r.height + 12f), new Color(1f, 1f, 1f, 0.6f * (1f - flash)));
            }

            // It lands in you: a ring in its colour, a short gold pillar, its icon popping up over your head.
            float g = Seg(e, OutroPickLand, PickOutroTime);
            if (g > 0f && g < 1f)
            {
                if (outroIcon != null)
                {
                    float sz = 30f, rise = 46f + 16f * g;
                    Ui.Icon(new Rect(outroTarget.X - sz / 2f, outroTarget.Y - rise - sz / 2f, sz, sz), outroIcon, Mathf.Sin(Mathf.PI * g));
                }
                if (BeginUiGl())
                {
                    UiModelAt(V2.Zero, 1f);
                    PickPillar(outroTarget, g, 30f);
                    var col = outroColors[outroPicked];
                    Arc(outroTarget, 20f + 70f * EaseOut(g), 4f, col.WithAlpha(1f - g), 40);
                    Arc(outroTarget, 8f + 40f * g, 2f, new Rgba(1f, 1f, 1f, 0.8f * (1f - g)), 32);
                    EndUiGl();
                }
            }
        }
    }
}
