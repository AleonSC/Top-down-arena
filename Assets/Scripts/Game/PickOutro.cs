using UnityEngine;

namespace Arena
{
    /// What happens on screen when you take an upgrade: the cards you passed on shatter into tumbling shards, and
    /// the one you took flashes, shrinks and flies into your character, ending in a ring in its rarity colour.
    /// It plays over whatever comes next (the next pick waits until it's done; see PickOutroPlaying).
    public partial class ArenaApp
    {
        const float PickOutroTime = 0.8f;
        const float PickFlashEnd = 0.12f, PickFlyEnd = 0.55f;

        float pickOutroAt = -10f;
        Rect[] pickCardRects = new Rect[0];   // where the picker last drew each card (DrawUpgradePicker fills it)
        Rect[] outroRects = new Rect[0];
        Rgba[] outroColors = new Rgba[0];
        int outroPicked = -1;
        string outroName = "";
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
            for (int i = 0; i < n; i++)
            {
                outroRects[i] = pickCardRects[i];
                outroColors[i] = Upgrades.RarityColor(Upgrades.All[me.Offer[i]].Rarity);
            }
            var u = Upgrades.All[me.Offer[picked]];
            outroPicked = picked;
            outroName = u.Name;
            outroIcon = PixelArt.ForUpgrade(u.Name);
            outroTarget = me.Downed ? new V2(W / 2f, H - 140f) : me.Disp * WorldToUi;
            outroDim = me.PendingPicks + me.PendingLegendary + me.PendingMods <= 1;
            pickOutroAt = Time.unscaledTime;
        }

        // Each unpicked card breaks along these lines (corners in 0..1 of the card), into six pieces.
        static readonly float[][] ShardCuts =
        {
            new[] { 0f, 0f, 0.6f, 0f, 0.4f, 0.45f, 0f, 0.35f },
            new[] { 0.6f, 0f, 1f, 0f, 1f, 0.3f, 0.4f, 0.45f },
            new[] { 0f, 0.35f, 0.4f, 0.45f, 0.3f, 0.75f, 0f, 0.7f },
            new[] { 0.4f, 0.45f, 1f, 0.3f, 1f, 0.65f, 0.3f, 0.75f },
            new[] { 0f, 0.7f, 0.3f, 0.75f, 0.55f, 1f, 0f, 1f },
            new[] { 0.3f, 0.75f, 1f, 0.65f, 1f, 1f, 0.55f, 1f },
        };

        static float EaseIn(float f) => f * f;
        static float Lerp(float a, float b, float f) => a + (b - a) * f;

        /// Draws the outro if one is playing (call after the HUD, so it sits on top).
        void DrawPickOutro()
        {
            float e = Time.unscaledTime - pickOutroAt;
            if (e < 0f || e >= PickOutroTime || outroRects.Length == 0)
                return;
            float f = e / PickOutroTime;

            if (outroDim)  // the picker's backdrop lets go of the arena
                Ui.Fill(new Rect(0, 0, W, H), new Color(0.01f, 0.012f, 0.03f, 0.82f * (1f - Mathf.Clamp01(e / 0.45f))));

            // The card you took: a flash, then it shrinks and flies into you.
            var from = outroRects[outroPicked];
            var col = outroColors[outroPicked];
            var c = new Color(col.R, col.G, col.B);
            Rect card = from;
            float flyF = 0f;
            if (e < PickFlyEnd)
            {
                if (e > PickFlashEnd)
                {
                    flyF = EaseIn((e - PickFlashEnd) / (PickFlyEnd - PickFlashEnd));
                    float s = Lerp(1f, 0.08f, flyF);
                    var ctr = new Vector2(Lerp(from.center.x, outroTarget.X, flyF), Lerp(from.center.y, outroTarget.Y, flyF));
                    card = new Rect(ctr.x - from.width * s / 2f, ctr.y - from.height * s / 2f, from.width * s, from.height * s);
                }
                Ui.Card(card, c, 0.9f);
                Ui.Pill(new Rect(card.x + 4f, card.y + 4f, card.width - 8f, card.height * 0.36f), new Color(c.r * 0.25f, c.g * 0.25f, c.b * 0.25f, 0.9f));
                float isz = Mathf.Min(card.width * 0.42f, card.height * 0.28f);
                if (outroIcon != null && isz > 3f)
                    Ui.Icon(new Rect(card.center.x - isz / 2f, card.y + card.height * 0.18f - isz / 2f + 6f * (card.height / from.height), isz, isz), outroIcon);
                if (card.width > 90f)
                    Ui.Fancy(new Rect(card.x, card.y + card.height * 0.42f, card.width, 30f), outroName, Mathf.RoundToInt(22f * card.width / from.width), Color.white, Color.Lerp(c, Color.white, 0.5f), TextAnchor.MiddleCenter);
                float flash = e < PickFlashEnd ? Mathf.Sin(e / PickFlashEnd * Mathf.PI) : 0f;
                if (flash > 0f)
                    Ui.Pill(card, new Color(1f, 1f, 1f, 0.55f * flash));
            }

            if (!BeginUiGl())
                return;
            UiModelAt(V2.Zero, 1f);

            // The ones you passed on: six shards each, flung apart and falling, fading as they go.
            for (int i = 0; i < outroRects.Length; i++)
            {
                if (i == outroPicked)
                    continue;
                var r = outroRects[i];
                float a = 1f - Mathf.Clamp01(e / 0.7f);
                if (a <= 0f)
                    continue;
                var rim = outroColors[i];
                for (int k = 0; k < ShardCuts.Length; k++)
                {
                    var cut = ShardCuts[k];
                    var pts = new V2[4];
                    V2 mid = V2.Zero;
                    for (int q = 0; q < 4; q++)
                    {
                        pts[q] = new V2(r.x + cut[q * 2] * r.width, r.y + cut[q * 2 + 1] * r.height);
                        mid = mid + pts[q] * 0.25f;
                    }
                    // Each piece flies out from the card's middle a little, then gravity takes it.
                    float side = (mid.X - r.center.x) / (r.width / 2f), seed = Hash(i * 17 + k);
                    var vel = new V2(side * (60f + seed * 80f) + (i < outroPicked ? -40f : 40f), -60f - seed * 90f);
                    var move = vel * e + new V2(0f, 900f * e * e);
                    float spin = (seed - 0.5f) * 5f * e;
                    float cs = Mathf.Cos(spin), sn = Mathf.Sin(spin);
                    for (int q = 0; q < 4; q++)
                    {
                        var d = pts[q] - mid;
                        pts[q] = mid + move + new V2(d.X * cs - d.Y * sn, d.X * sn + d.Y * cs);
                    }
                    // The top pieces carry the card's coloured art band, the rest its dark body.
                    var fill = k < 2 ? new Rgba(rim.R * 0.3f + 0.05f, rim.G * 0.3f + 0.05f, rim.B * 0.3f + 0.08f, 0.95f * a)
                                     : new Rgba(rim.R * 0.1f + 0.07f, rim.G * 0.1f + 0.08f, rim.B * 0.1f + 0.13f, 0.95f * a);
                    SetColor(fill);
                    Tri(pts[0], pts[1], pts[2]);
                    Tri(pts[0], pts[2], pts[3]);
                    var edge = rim.WithAlpha(0.7f * a);
                    for (int q = 0; q < 4; q++)
                        Line(pts[q], pts[(q + 1) % 4], 1.5f, edge);
                }
                // A few sparks off the break.
                for (int k = 0; k < 10; k++)
                {
                    float seed = Hash(i * 31 + k + 400);
                    var p0 = new V2(r.x + Hash(i * 31 + k + 500) * r.width, r.y + Hash(i * 31 + k + 600) * r.height);
                    var p = p0 + new V2((seed - 0.5f) * 260f * e, -120f * e + 700f * e * e);
                    Circle(p, 2f, rim.WithAlpha(a), 6);
                }
            }

            // The pick lands: a ring and a few sparks at your feet.
            if (e > PickFlyEnd - 0.03f)
            {
                float g = Mathf.Clamp01((e - PickFlyEnd + 0.03f) / (PickOutroTime - PickFlyEnd + 0.03f));
                Arc(outroTarget, 10f + 70f * g, 4f * (1f - g) + 1f, col.WithAlpha(1f - g), 40);
                Arc(outroTarget, 6f + 40f * g, 2f, new Rgba(1f, 1f, 1f, 0.8f * (1f - g)), 32);
                for (int k = 0; k < 12; k++)
                {
                    var dir = V2.FromAngle(k * MathX.Tau / 12f + 0.3f);
                    Circle(outroTarget + dir * (14f + 60f * g), 2.5f * (1f - g) + 0.5f, col.WithAlpha(1f - g), 6);
                }
            }
            else if (e > PickFlashEnd)
            {
                // A short trail behind the flying card.
                var head = new V2(card.center.x, card.center.y);
                for (int k = 1; k <= 5; k++)
                {
                    float tf = EaseIn(Mathf.Clamp01((e - PickFlashEnd - k * 0.02f) / (PickFlyEnd - PickFlashEnd)));
                    var tp = new V2(Lerp(from.center.x, outroTarget.X, tf), Lerp(from.center.y, outroTarget.Y, tf));
                    Circle(tp, 6f - k, col.WithAlpha(0.5f - k * 0.08f), 10);
                }
                Circle(head, 8f, col.WithAlpha(0.35f * flyF), 12);
            }
            EndUiGl();
        }
    }
}
