using UnityEngine;

namespace Arena
{
    /// How the Lich's stages look (Lich.cs): its six graves, the soul beams between stage 2's phylacteries, the soul
    /// sweep, bone prisons, wailing spirits, dead hands, and the spectre fading between graves.
    public partial class ArenaApp
    {
        static readonly Rgba SoulCol = new Rgba(0.43f, 1f, 0.69f), SpiritCol = new Rgba(0.37f, 0.85f, 1f), GraveStone = new Rgba(0.29f, 0.32f, 0.3f),
            GraveEdge = new Rgba(0.54f, 0.58f, 0.55f), BoneCol = new Rgba(0.91f, 0.89f, 0.8f), SpectreCol = new Rgba(0.7f, 0.55f, 1f);
        float sweepAngle;  // the soul sweep, smoothed between snapshots

        /// Drawn with the hazards, under the enemies: graves, beams, the sweep, prisons and spirits.
        void DrawLichScene(float t)
        {
            Enemy lich = null;
            foreach (var e in game.Enemies.Values)
                if (e.Type == EnemyType.Lich && e.Lich != null && !game.IsSpawning(e))
                    lich = e;
            if (lich == null)
                return;
            var s = lich.Lich;
            for (int i = 0; i < Game.GraveCount; i++)
                DrawGrave(game.GravePos(i), t);
            // Stage 2: soul beams between the phylacteries, in the order they were raised.
            if (s.Stage == LichStage.Harvest && s.TransT <= 0f)
            {
                var ph = new System.Collections.Generic.List<Enemy>();
                foreach (var o in game.Enemies.Values)
                    if (o.Type == EnemyType.Phylactery && !game.IsSpawning(o))
                        ph.Add(o);
                ph.Sort((a, b) => a.Id.CompareTo(b.Id));
                for (int i = 0; i < ph.Count && ph.Count >= 2; i++)
                {
                    if (ph.Count == 2 && i == 1)
                        break;
                    V2 a = ph[i].Disp, b = ph[(i + 1) % ph.Count].Disp;
                    Line(a, b, Game.SoulBeamWidth + 10f, SpiritCol.WithAlpha(0.18f));
                    Line(a, b, 3f, new Rgba(0.8f, 0.96f, 1f, 0.75f + 0.25f * Mathf.Sin(t * 22f + i)));
                }
            }
            // The soul sweep: a flickering warning line, then the beam.
            if (s.SweepOn > 0)
            {
                float diff = MathX.Fmod(s.SweepA - sweepAngle + MathX.Pi, MathX.Tau) - MathX.Pi;
                sweepAngle += Mathf.Abs(diff) > 0.6f ? diff : diff * Mathf.Min(1f, Time.deltaTime * 12f);
                V2 end = lich.Disp + V2.FromAngle(sweepAngle) * Game.LichSweepLen;
                if (s.SweepOn == 1)
                    Line(lich.Disp, end, 2f, SpiritCol.WithAlpha(MathX.Fmod(t, 0.12f) < 0.06f ? 0.9f : 0.35f));
                else
                {
                    Line(lich.Disp, end, Game.LichSweepWidth * 2f + 4f, SpiritCol.WithAlpha(0.22f));
                    Line(lich.Disp, end, 10f, SpiritCol.WithAlpha(0.85f));
                    Line(lich.Disp, end, 3f, new Rgba(0.91f, 0.98f, 1f, 1f));
                }
            }
            else
                sweepAngle = s.SweepA;
            // Bone prisons: spikes rising round a ring, a gold mark at the gap.
            foreach (var q in s.Prisons)
            {
                float k = MathX.Clamp01(q.T / Game.PrisonRise);
                Arc(q.Center, Game.PrisonR, 1.5f, BoneCol.WithAlpha(0.15f), 48);
                for (int i = 0; i < Game.PrisonSpikes; i++)
                {
                    float a = Game.PrisonSpikeAngle(q, i);
                    V2 dir = V2.FromAngle(a), side = dir.Orthogonal(), at = q.Center + dir * Game.PrisonR;
                    SetColor(BoneCol.WithAlpha(q.T >= Game.PrisonRise ? 0.4f : 1f));
                    Tri(at + side * 6f, at - side * 6f, at - dir * (26f * k));
                }
                Circle(q.Center + V2.FromAngle(q.Gap) * Game.PrisonR, 7f, new Rgba(1f, 0.8f, 0.35f, 0.5f + 0.5f * Mathf.Sin(t * 16f)), 12);
            }
            // Wailing spirits: pale heads with a trailing wisp.
            foreach (var sp in s.Spirits)
            {
                float a = sp.Diving ? 0f : t * Game.SpiritSpin;
                Circle(sp.Pos, Game.SpiritR * 2.2f, SpiritCol.WithAlpha(sp.Diving ? 0.3f : 0.18f), 14);
                Circle(sp.Pos, Game.SpiritR, new Rgba(0.75f, 0.94f, 1f), 14);
                Circle(sp.Pos + V2.FromAngle(a) * 3f, 3f, new Rgba(0.05f, 0.16f, 0.2f), 8);
            }
            // Where the spectre is about to appear.
            if (s.FadeT > 0f)
                Arc(s.BlinkTo, 40f, 3f, SpectreCol.WithAlpha(0.5f + 0.5f * Mathf.Sin(t * 25f)), 32);
        }

        /// A headstone with a cross, its shadow below.
        void DrawGrave(V2 g, float t)
        {
            SetColor(new Rgba(0f, 0f, 0f, 0.35f));
            Quad(g + new V2(-22f, 22f), g + new V2(22f, 22f), g + new V2(22f, 32f), g + new V2(-22f, 32f));
            SetColor(GraveStone);
            Quad(g + new V2(-18f, -10f), g + new V2(18f, -10f), g + new V2(18f, 24f), g + new V2(-18f, 24f));
            Circle(g + new V2(0f, -10f), 18f, GraveStone, 20);
            Line(g + new V2(-18f, 24f), g + new V2(-18f, -10f), 2f, GraveEdge);
            Line(g + new V2(18f, 24f), g + new V2(18f, -10f), 2f, GraveEdge);
            Arc(g + new V2(0f, -10f), 18f, 2f, GraveEdge, 20, MathX.Pi, MathX.Pi);
            var cross = new Rgba(0.08f, 0.1f, 0.09f, 0.9f);
            Line(g + new V2(0f, -18f), g + new V2(0f, 6f), 2.5f, cross);
            Line(g + new V2(-7f, -9f), g + new V2(7f, -9f), 2.5f, cross);
        }

        /// Under the Lich: the stage change, its guard (a tether to each phylactery and a shell), the open window, and
        /// the spectre's ghostly colour (fading out while it blinks). Returns the colour to draw its body in.
        Rgba DrawLichUnderlay(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            var s = e.Lich;
            if (s == null)
                return col;
            if (s.TransT > 0f)
            {
                DrawStageShiftBoss(e, pos, r, t);  // changing stage (StageShiftLook.cs)
                return col;
            }
            var aura = s.Stage == LichStage.Harvest ? SpiritCol : s.Stage == LichStage.Undeath ? SpectreCol : s.Stage == LichStage.LastStand ? new Rgba(1f, 0.42f, 0.6f) : SoulCol;
            Circle(pos, r + 30f, aura.WithAlpha(0.1f + 0.05f * Mathf.Sin(t * 5f)), 32);
            if (e.Phase == Game.LichGuarded)
            {
                foreach (var o in game.Enemies.Values)
                    if (o.Type == EnemyType.Phylactery)
                        Line(pos, o.Disp, 2f, aura.WithAlpha(0.4f + 0.3f * Mathf.Sin(t * 8f)));
                Circle(pos, r + 14f, aura.WithAlpha(0.15f), 32);
                Arc(pos, r + 14f, 3f, aura.WithAlpha(0.8f), 32);
            }
            else if (s.Window > 0f)
                Arc(pos, r + 12f, 4f, new Rgba(1f, 0.8f, 0.35f, 0.6f + 0.4f * Mathf.Sin(t * 14f)), 32, -MathX.Pi / 2f, MathX.Tau * MathX.Clamp01(s.Window / Game.LichWindow));
            if (s.Stage == LichStage.Undeath)
            {
                float a = s.FadeT > 0f ? s.FadeT / 0.5f * 0.6f : 0.75f + 0.15f * Mathf.Sin(t * 4f);
                return SpectreCol.WithAlpha(a);
            }
            return col;
        }

        /// A dead hand about to burst up: a darkening patch with bony fingers rising out of it.
        void DrawDeadHand(Hazard h, float t)
        {
            float f = 1f - MathX.Clamp01(h.T / Mathf.Max(0.01f, h.Duration));
            Circle(h.Pos, h.Radius * (0.3f + 0.7f * f), new Rgba(0f, 0f, 0f, 0.3f + 0.3f * f), 28);
            Arc(h.Pos, h.Radius, 2.5f, (f > 0.7f && MathX.Fmod(t, 0.1f) < 0.05f ? Rgba.White : BoneCol).WithAlpha(0.85f), 36);
            for (int k = -2; k <= 2; k++)
                Line(h.Pos + new V2(k * 7f, 8f), h.Pos + new V2(k * 9f, -10f - (2 - Mathf.Abs(k)) * 6f * f), 4f, BoneCol.WithAlpha(0.4f + 0.6f * f));
        }
    }
}
