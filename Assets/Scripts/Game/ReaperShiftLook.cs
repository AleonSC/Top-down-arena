using UnityEngine;

namespace Arena
{
    /// The Twin Reapers' two phase changes (Reapers.cs, TickReaperChange), drawn from the replicated R.TransT:
    /// the fusion (Game.FuseTime): Dusk and Dawn spiral in trailing afterimages, scythes raised toward the middle,
    /// over a moon-and-sun sigil drawing itself in and closing; they merge in a column of gold light and the Dual
    /// Reaper unfurls out of it (ReaperBodyLook.cs scales it up). The split (Game.SplitTime): it shakes, its seam
    /// burns white and the floor cracks under it; then it bursts, the chain snaps into links flying out, and the
    /// two fly apart trailing afterimages. The banner and the floor streaks stay StageShiftLook.cs's.
    public partial class ArenaApp
    {
        /// How far the fusion is between the merge and the Dual Reaper standing full size (0-1), or 1 otherwise.
        float DualBirth()
        {
            var R = game.Reapers;
            return R != null && R.Trans == 1 ? Mathf.Clamp01((R.TransT - Game.FuseMerge) / 0.7f) : 1f;
        }

        /// On the floor, under everything: the sigil, the column of light, the cracks, the snapped chain.
        void DrawReaperShiftFloor(float t)
        {
            var R = game.Reapers;
            if (R == null || R.Trans == 0)
                return;
            float u = R.TransT;
            V2 mid = Game.ArenaMid;
            if (R.Trans == 1)
            {
                if (u < Game.FuseMerge)
                {
                    // The sigil: a ring drawing itself in, Dusk's half and Dawn's, closing in as they spiral.
                    float f = u / Game.FuseMerge, rr = 70f + 300f * (1f - Ease(f)), spin = t * 0.8f, a = Mathf.Clamp01(f * 3f);
                    Circle(mid, rr, DualGold.WithAlpha(0.05f * a), 48);
                    Arc(mid, rr, 4f, DuskCol.WithAlpha(0.8f * a), 32, spin, MathX.Pi * Ease(f));
                    Arc(mid, rr, 4f, DawnCol.WithAlpha(0.8f * a), 32, spin + MathX.Pi, MathX.Pi * Ease(f));
                    Arc(mid, rr * 0.82f, 1.5f, DualGold.WithAlpha(0.5f * a), 48);
                    for (int k = 0; k < 12; k++)  // runes round it
                    {
                        V2 d = V2.FromAngle(-spin * 1.5f + MathX.Tau * k / 12f);
                        Line(mid + d * rr * 0.86f, mid + d * rr * 0.96f, k % 3 == 0 ? 3f : 1.5f, DualGold.WithAlpha(0.7f * a));
                    }
                    DrawReaperSigil(mid + new V2(-rr * 0.22f, 0f), 10f + 10f * f, true, DuskCol.WithAlpha(0.6f * a), t);
                    DrawReaperSigil(mid + new V2(rr * 0.22f, 0f), 10f + 10f * f, false, DawnCol.WithAlpha(0.6f * a), t);
                    return;
                }
                float m = u - Game.FuseMerge;
                if (m < 0.9f)
                {
                    // The column of gold light they merge in, narrowing as it fades.
                    float k = m / 0.9f, w = 90f * (1f - k) + 20f;
                    FillRect(mid.X - w * 0.5f, mid.Y - 700f, w, 700f, DualGold.WithAlpha(0.35f * (1f - k)));
                    FillRect(mid.X - w * 0.18f, mid.Y - 700f, w * 0.36f, 700f, new Rgba(1f, 0.97f, 0.85f, 0.6f * (1f - k)));
                    Circle(mid, 80f + 120f * k, Rgba.White.WithAlpha(0.3f * (1f - k)), 32);
                }
                // A burnt gold ring where it formed, fading through the power-up.
                float fade = 1f - Mathf.Clamp01(m / (Game.FuseTime - Game.FuseMerge));
                Arc(mid, 95f, 3f, DualGold.WithAlpha(0.45f * fade), 48);
                return;
            }
            // The split.
            if (u < Game.SplitBurst)
            {
                Enemy d = null;
                foreach (var e in game.Enemies.Values)
                    if (e.Type == EnemyType.DualReaper)
                        d = e;
                if (d == null)
                    return;
                float f = u / Game.SplitBurst;
                for (int k = 0; k < 7; k++)  // cracks running out under it, glowing red
                {
                    float a = k * 0.9f + 0.4f;
                    V2 prev = d.Disp;
                    for (int s = 1; s <= 4; s++)
                    {
                        V2 pt = d.Disp + V2.FromAngle(a + 0.25f * Mathf.Sin(k * 3.1f + s * 1.7f)) * (d.Radius + 160f * f * s / 4f);
                        Line(prev, pt, 3f - 0.5f * s, new Rgba(1f, 0.35f, 0.35f, 0.75f * f));
                        prev = pt;
                    }
                }
                return;
            }
            Enemy da = null, db = null;
            foreach (var e in game.Enemies.Values)
                if (e.Type == EnemyType.ReaperOfDusk) da = e; else if (e.Type == EnemyType.ReaperOfDawn) db = e;
            if (da == null || db == null)
                return;
            // The chain snaps: its links fly out from where it broke, tumbling and fading.
            float b = u - Game.SplitBurst, fly = Ease(Mathf.Clamp01(b / 1f)), alpha = 1f - Mathf.Clamp01(b / 1.3f);
            V2 at = V2.Lerp(da.Disp, db.Disp, 0.5f);
            for (int k = 0; k < 14; k++)
            {
                float a = k * 2.399f;
                V2 c = at + V2.FromAngle(a) * (30f + 360f * fly * (0.6f + 0.4f * ((k * 7) % 5) / 4f));
                V2 axis = V2.FromAngle(a * 3f + t * 9f * ((k & 1) == 0 ? 1f : -1f));
                float w = (k & 1) == 0 ? 15f : 9f;
                var lc = DuskCol.Mix(DawnCol, k / 13f);
                Line(c - axis * w, c + axis * w, 12f, lc.WithAlpha(0.3f * alpha));
                Line(c - axis * w, c + axis * w, 5f, lc.Mix(Rgba.White, 0.3f).WithAlpha(alpha));
            }
            Circle(at, 40f + 120f * fly, Rgba.White.WithAlpha(0.1f * alpha), 32);
        }

        /// Under a Reaper changing phase: afterimages as the twins spiral in or fly apart, light pouring in.
        void DrawReaperShiftOn(Enemy e, V2 pos, float r, float t)
        {
            var R = game.Reapers;
            if (R == null || R.Trans == 0)
                return;
            float u = R.TransT;
            V2 mid = Game.ArenaMid;
            var col = e.Type == EnemyType.ReaperOfDusk ? DuskCol : e.Type == EnemyType.ReaperOfDawn ? DawnCol : DualGold;
            if (e.Type == EnemyType.DualReaper)
            {
                float glow = R.Trans == 1 ? 1f - DualBirth() : Mathf.Clamp01(u / Game.SplitBurst);
                Circle(pos, r + 30f + 8f * Mathf.Sin(t * 18f), (R.Trans == 1 ? Rgba.White : new Rgba(1f, 0.35f, 0.35f)).WithAlpha(0.3f * glow), 32);
                return;
            }
            V2 behind;
            float a;
            if (R.Trans == 1 && u < Game.FuseMerge)
            {
                // Spiralling in: behind it along its path, and a thread of light to the middle.
                V2 rad = (pos - mid).Normalized();
                behind = -new V2(-rad.Y, rad.X) + rad * 0.3f;
                a = Mathf.Clamp01(u / 0.4f);
                Line(pos, mid, 2f + 4f * u / Game.FuseMerge, col.WithAlpha(0.25f + 0.4f * u / Game.FuseMerge));
            }
            else if (R.Trans == 2 && u >= Game.SplitBurst)
            {
                behind = (mid - pos).Normalized();  // flying apart: behind it is back toward the middle
                a = 1f - Mathf.Clamp01((u - Game.SplitBurst) / 1.1f);
            }
            else
                return;
            for (int k = 4; k >= 1; k--)
            {
                V2 g = pos + behind * (22f * k);
                Circle(g, r * (1f - 0.12f * k), col.WithAlpha(a * 0.28f * (1f - k / 5f)), 18);
            }
        }
    }
}
