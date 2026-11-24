using UnityEngine;

namespace Arena
{
    /// The Storm Herald's body, the Tempest Herald: a hooded caller facing its target, a torn storm cloak behind it
    /// (streaming downwind in the Gale), two lightning-rod spires on its shoulders with an arc snapping between them,
    /// a staff held out front with a crackling head, and two eyes glowing out of the cowl. Blue in the Gathering Storm
    /// and the Gale, dark violet in the Supercell, blazing in the last stand. Its storm glow and wind sock are
    /// DrawHeraldUnderlay (HeraldLook.cs).
    public partial class ArenaApp
    {
        static Rgba HeraldInk(int stage) => stage >= 2 ? new Rgba(0.13f, 0.09f, 0.22f) : new Rgba(0.16f, 0.2f, 0.3f);
        static Rgba HeraldLit(int stage) => stage >= 2 ? new Rgba(0.55f, 0.42f, 0.85f) : new Rgba(0.55f, 0.68f, 0.9f);
        static Rgba HeraldCore(int stage) => stage >= 3 ? new Rgba(0.95f, 0.75f, 1f) : stage == 2 ? new Rgba(0.82f, 0.7f, 1f) : StormWhite;
        static readonly Rgba HeraldIron = new Rgba(0.3f, 0.36f, 0.48f);

        void DrawHeraldBody(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            var s = e.Herald;
            int stage = s == null ? 0 : (int)s.Stage;
            float wind = s == null || s.TransT > 0f ? 0f : s.Wind;
            float flash = e.Flash > 0f || JuiceFlash(e) ? HitFlashMix(e) : 0f;  // the hit flash, as DrawEnemy mixes it
            Rgba ink = HeraldInk(stage).Mix(Rgba.White, flash), lit = HeraldLit(stage).Mix(Rgba.White, flash), core = HeraldCore(stage);
            int seed = (int)(t * 14f);
            V2 fwd = V2.FromAngle(e.Facing), side = fwd.Orthogonal();
            V2 back = (-fwd + new V2(wind * 0.9f, 0f)).Normalized(), bside = back.Orthogonal();

            // The cloak: a full fan from the shoulders back, its hem torn into points that flutter; longer each phase.
            const int pts = 7;
            V2 prevTip = V2.Zero;
            for (int i = 0; i < pts; i++)
            {
                float u0 = -1f + 2f * i / pts, u1 = -1f + 2f * (i + 1) / pts, um = (u0 + u1) / 2f;
                float len = r * (1.2f + 0.18f * stage) * (1f - 0.22f * um * um) + 5f * Mathf.Sin(t * 7f + i * 1.3f) + Mathf.Abs(wind) * 14f;
                V2 h0 = pos + bside * u0 * r * 1.08f + back * len * 0.8f;
                V2 h1 = pos + bside * u1 * r * 1.08f + back * len * 0.8f;
                V2 tip = pos + bside * um * r * 1.12f + back * len;
                V2 r0 = pos + bside * u0 * r * 0.7f, r1 = pos + bside * u1 * r * 0.7f;
                Rgba fold = ink.Mix(lit, i % 2 == 0 ? 0.12f : 0.22f);  // alternate folds catch the light
                VTri(r0, ink, h0, fold, h1, fold);
                VTri(r0, ink, h1, fold, r1, ink);
                VTri(h0, fold, tip, fold.WithAlpha(0.55f), h1, fold);
                if (i > 0)
                    Line(prevTip, h0, 1.5f, lit.WithAlpha(0.3f));  // the hem's lit edge
                Line(h0, tip, 1.5f, lit.WithAlpha(0.3f));
                prevTip = tip;
                if (stage >= 2 && HHash(seed + i * 5) > 0.75f)  // Supercell: charge crawling off the hem
                    DrawBolt(tip, tip + back * (10f + 8f * stage) + bside * (HHash(seed + i) - 0.5f) * 20f, 1.2f, core, seed + i, 2);
            }

            // Mantle and pauldrons, lit on the side facing its target.
            V2 shoulderL = pos + side * r * 0.62f, shoulderR = pos - side * r * 0.62f;
            Circle(pos + new V2(0f, 5f), r * 0.85f, new Rgba(0f, 0f, 0f, 0.35f), 22);
            Circle(pos, r * 0.8f, ink.Mix(lit, 0.2f), 24);
            Circle(shoulderL, r * 0.32f, ink.Mix(lit, 0.35f), 14);
            Circle(shoulderR, r * 0.32f, ink.Mix(lit, 0.35f), 14);
            Arc(shoulderL, r * 0.32f, 1.5f, lit.WithAlpha(0.7f), 10, e.Facing - 1.2f, 2.4f);
            Arc(shoulderR, r * 0.32f, 1.5f, lit.WithAlpha(0.7f), 10, e.Facing - 1.2f, 2.4f);
            Arc(pos, r * 0.8f, 2f, lit.WithAlpha(0.8f), 24, e.Facing - 1.6f, 3.2f);

            // Two rod spires rising off the shoulders, an arc snapping between their tips (always on from the Supercell).
            V2 tipL = shoulderL + back * r * 0.15f + side * r * 0.12f, tipR = shoulderR + back * r * 0.15f - side * r * 0.12f;
            DrawHeraldSpire(tipL, back, r, t, core);
            DrawHeraldSpire(tipR, back, r, t, core);
            if (Mathf.Sin(t * 5f) > -0.3f || stage >= 2)
                DrawBolt(tipL, tipR, 2f, core, seed, 5);

            // The hood: a dark cowl, two eyes glowing out of it (blazing in the last stand).
            V2 hood = pos + fwd * r * 0.15f;
            Circle(hood, r * 0.5f, ink.Mix(lit, 0.45f), 20);
            Arc(hood, r * 0.5f, 1.5f, lit.WithAlpha(0.6f), 16, e.Facing - 1.3f, 2.6f);
            Circle(hood + fwd * r * 0.06f, r * 0.38f, new Rgba(0.04f, 0.04f, 0.08f), 18);
            for (int k = -1; k <= 1; k += 2)
            {
                V2 ep = hood + fwd * r * 0.2f + side * k * r * 0.13f;
                GlowDisc(ep, 10f + 3f * stage, core, 0.7f);
                Circle(ep, 2.6f + (stage >= 3 ? 0.8f : 0f), Rgba.White, 8);
            }

            // The staff in its right hand, held out front, a crackling head; more sparks each phase.
            V2 hand = pos - side * r * 0.7f + fwd * r * 0.35f;
            V2 head = hand + fwd * r * 0.95f;
            Line(hand - fwd * r * 0.5f, head, 4f, new Rgba(0.42f, 0.46f, 0.55f).Mix(Rgba.White, flash));
            Circle(hand, 5f, ink.Mix(lit, 0.4f), 10);  // the gauntlet round it
            GlowDisc(head, 22f + 6f * stage, core, 0.5f + 0.25f * Mathf.Sin(t * 9f));
            Polygon(head, 7f, 4, t * 2f, lit);
            Circle(head, 3f, Rgba.White, 8);
            for (int k = 0; k < 2 + stage; k++)
                DrawBolt(head, head + V2.FromAngle(t * 4f + k * 2.3f) * (14f + 4f * stage), 1.5f, core, seed + k * 5, 3);
        }

        /// A lightning-rod spire on the Herald's shoulder: an iron rod with a glowing tip.
        void DrawHeraldSpire(V2 tip, V2 back, float r, float t, Rgba core)
        {
            Line(tip - back * r * 0.25f, tip, 5f, HeraldIron);
            GlowDisc(tip, 16f, core, 0.6f);
            Circle(tip, 4f + 1f * Mathf.Sin(t * 14f + tip.X), core, 10);
        }

        static float HHash(int a) => Mathf.Abs(Mathf.Sin(a * 12.9898f + 4.1f) * 43758.5453f) % 1f;
    }
}
