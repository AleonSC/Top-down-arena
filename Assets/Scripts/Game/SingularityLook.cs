using UnityEngine;

namespace Arena
{
    /// How the Singularity's stages look (Singularity.cs): the accretion disk, the event horizon, the white hole's
    /// flash and push, and the shards of its Split.
    public partial class ArenaApp
    {
        static readonly Rgba DiskCol = new Rgba(0.85f, 0.35f, 1f), RimCol = new Rgba(0.5f, 0.84f, 1f), HorizonGlow = new Rgba(1f, 0.47f, 0.86f);

        /// The accretion disk: two rings of orbs round the core, turning opposite ways (drawn with the hazards).
        void DrawSingularityDisk(Enemy sing)
        {
            if (!Game.DiskUp(sing))
                return;
            for (int ring = 0; ring < 2; ring++)
            {
                int n = ring == 0 ? Game.DiskInnerN : Game.DiskOuterN;
                Arc(sing.Disp, ring == 0 ? Game.DiskInnerR : Game.DiskOuterR, 1.5f, DiskCol.WithAlpha(0.15f), 64);  // its track
                for (int i = 0; i < n; i++)
                {
                    V2 at;
                    if (!Game.DiskOrb(sing.Sing, sing.Disp, ring, i, out at))
                        continue;
                    float r = Game.DiskOrbR;
                    Circle(at, r * 1.7f, DiskCol.WithAlpha(0.22f), 14);
                    Circle(at, r, DiskCol, 14);
                    Circle(at, r * 0.45f, new Rgba(0.08f, 0f, 0.08f, 0.8f), 10);  // a boss orb's dark core
                }
            }
        }

        /// Under the core: the pull's rings (in during a pull, out during the white hole's push), the event horizon
        /// and its glowing rim, and the dashed shell while it's split.
        void DrawSingularityUnderlay(Enemy e, V2 pos, float core, Rgba col, float t)
        {
            var s = e.Sing;
            if (s != null && s.TransT > 0f)
            {
                DrawStageShiftBoss(e, pos, core, t);  // changing stage (StageShiftLook.cs)
                Circle(pos, core * 1.6f, new Rgba(0.05f, 0f, 0.1f, 0.9f), 40);
                return;
            }
            float horizon = s != null && s.TransT <= 0f ? s.Horizon : 0f;
            bool pulling = e.Phase == Game.SingPull || (s != null && s.TransT <= 0f && (s.Stage == SingStage.Spaghettify || s.Stage == SingStage.LastStand) && e.Phase != Game.SingWhiteHole);
            bool pushing = e.Phase == Game.SingWhiteHole && s != null && s.TurnT >= 1f && s.TurnT < 2.5f;
            float edge = Mathf.Max(core, horizon);
            if (pulling)
                for (int k = 0; k < 4; k++)
                {
                    float f = 1f - MathX.Fmod(t * 0.8f + k * 0.25f, 1f);
                    Arc(pos, edge + 40f + f * 360f, 2f, col.WithAlpha(0.35f * (1f - f)), 64);
                }
            if (pushing)
                for (int k = 0; k < 4; k++)
                {
                    float f = MathX.Fmod(t * 1.6f + k * 0.25f, 1f);
                    Arc(pos, edge + 30f + f * 420f, 3f, Rgba.White.WithAlpha(0.45f * (1f - f)), 64);
                }
            if (horizon > 1f)
            {
                Circle(pos, horizon + 40f, HorizonGlow.WithAlpha(0.06f), 48);
                Circle(pos, horizon, new Rgba(0f, 0f, 0f, 1f), 48);
                bool flash = e.Phase == Game.SingWhiteHole && s.TurnT < 1f;  // the white hole's warning
                Arc(pos, horizon, flash ? 6f : 3f, flash ? Rgba.White.WithAlpha(0.6f + 0.4f * Mathf.Sin(t * 30f)) : RimCol.WithAlpha(0.85f), 64);
                for (int k = 0; k < 6; k++)  // matter spiralling in
                {
                    float a = t * 0.8f + k;
                    Arc(pos, horizon + 8f + k * 5f, 2f, HorizonGlow.WithAlpha(0.35f), 24, a, 1.2f);
                }
            }
            else
                Circle(pos, core * 1.6f, new Rgba(0.05f, 0f, 0.1f, 0.9f), 40);  // dark core halo
            if (e.Phase == Game.SingSplit)
                for (int k = 0; k < 16; k++)  // a dashed shell: it can't be hurt, the shards can
                    Arc(pos, edge + 14f, 2f, Rgba.White.WithAlpha(0.5f), 6, k * MathX.Tau / 16f + t * 0.5f, MathX.Tau / 32f);
        }

        /// A shard of the Split: a small black hole with the core's color, tied to it by a faint line.
        void DrawSingShardUnderlay(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            Enemy core;
            if (game.Enemies.TryGetValue(e.ParentId, out core))
                Line(core.Disp, pos, 1.5f, col.WithAlpha(0.25f));
            Circle(pos, r + 14f, col.WithAlpha(0.15f + 0.08f * Mathf.Sin(t * 6f + e.Id)), 24);
        }
    }
}
