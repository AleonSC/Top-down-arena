using UnityEngine;

namespace Arena
{
    /// How the Lich's phases look (Lich.cs): its six graves, the soul beams between phase 2's phylacteries, the soul
    /// sweep, bone prisons, wailing spirits, dead hands, and the spectre fading between graves.
    public partial class ArenaApp
    {
        static readonly Rgba SoulCol = new Rgba(0.43f, 1f, 0.69f), SpiritCol = new Rgba(0.37f, 0.85f, 1f),
            BoneCol = new Rgba(0.91f, 0.89f, 0.8f), SpectreCol = new Rgba(0.7f, 0.55f, 1f);
        float sweepAngle;  // the soul sweep, smoothed between snapshots

        /// Drawn with the hazards, under the enemies: graves, beams, the sweep, prisons and spirits.
        void DrawLichScene(float t)
        {
            Enemy lich = null;
            foreach (var e in EnemiesOf(EnemyType.Lich))
                if (e.Type == EnemyType.Lich && e.Lich != null && !game.IsSpawning(e))
                    lich = e;
            if (lich == null)
                return;
            var s = lich.Lich;
            var gc = LichStageCol(lich);
            for (int i = 0; i < Game.GraveCount; i++)
            {
                V2 g = game.GravePos(i);
                // how stirred it is: the spectre's grave in Undeath, every grave in the Final Rite
                float heat = s.Stage == LichStage.LastStand ? 0.75f : s.Stage == LichStage.Undeath && lich.Disp.CloserThan(g, 170f) ? 1f : 0.3f;
                DrawGrave(g, gc, heat, t, i);  // LichBodyLook.cs
            }
            // Phase 2: soul beams between the phylacteries, in the order they were raised.
            if (s.Stage == LichStage.Harvest && s.TransT <= 0f)
            {
                var ph = new System.Collections.Generic.List<Enemy>();
                foreach (var o in EnemiesOf(EnemyType.Phylactery))
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
                float diff = MathX.WrapPi(s.SweepA - sweepAngle);
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
            // Bone prisons and wailing spirits (LichBodyLook.cs).
            foreach (var q in s.Prisons)
                DrawBonePrison(q, t);
            for (int i = 0; i < s.Spirits.Count; i++)
                DrawWailingSpirit(s.Spirits[i], i, lich.Disp, t);
            // Where the spectre is about to appear.
            if (s.FadeT > 0f)
                Arc(s.BlinkTo, 40f, 3f, SpectreCol.WithAlpha(0.5f + 0.5f * Mathf.Sin(t * 25f)), 32);
        }

        /// Under the Lich: the phase change, its guard (a tether to each phylactery and a shell), the open window, and
        /// the spectre's ghostly colour (fading out while it blinks). Returns the colour to draw its body in.
        Rgba DrawLichUnderlay(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            var s = e.Lich;
            if (s == null)
                return col;
            if (s.TransT > 0f)
            {
                DrawStageShiftBoss(e, pos, r, t);  // changing phase (StageShiftLook.cs)
                return col;
            }
            var aura = s.Stage == LichStage.Harvest ? SpiritCol : s.Stage == LichStage.Undeath ? SpectreCol : s.Stage == LichStage.LastStand ? new Rgba(1f, 0.42f, 0.6f) : SoulCol;
            Circle(pos, r + 30f, aura.WithAlpha(0.1f + 0.05f * Mathf.Sin(t * 5f)), 32);
            if (e.Phase == Game.LichGuarded)
            {
                foreach (var o in EnemiesOf(EnemyType.Phylactery))
                    if (o.Type == EnemyType.Phylactery)
                        Line(pos, o.Disp, 2f, aura.WithAlpha(0.4f + 0.3f * Mathf.Sin(t * 8f)));
                Circle(pos, r + 14f, aura.WithAlpha(0.15f), 32);
                Arc(pos, r + 14f, 3f, aura.WithAlpha(0.8f), 32);
            }
            else if (s.Window > 0f)
                Arc(pos, r + 12f, 4f, Rgba.Gold.WithAlpha(0.6f + 0.4f * Mathf.Sin(t * 14f)), 32, -MathX.Pi / 2f, MathX.Tau * MathX.Clamp01(s.Window / Game.LichWindow));
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
