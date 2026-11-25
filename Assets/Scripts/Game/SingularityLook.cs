using UnityEngine;

namespace Arena
{
    /// How the Singularity looks (Singularity.cs): its core (a lensed black hole), the accretion disk, the event horizon,
    /// the white hole's flash and push, and the shards of its Split.
    public partial class ArenaApp
    {
        static readonly Rgba RimCol = new Rgba(0.5f, 0.84f, 1f), HorizonGlow = new Rgba(1f, 0.47f, 0.86f);
        static readonly Rgba SingHot = new Rgba(1f, 0.93f, 0.78f), SingGold = new Rgba(1f, 0.72f, 0.35f), SingRed = new Rgba(1f, 0.3f, 0.38f);

        /// The core: a black shadow with a white photon ring, and a tilted accretion band crossing in front of it (its far
        /// half lensed up over the top), brighter on the side spinning toward you, with hot clumps riding it. It heats up
        /// phase by phase: pink in Event Horizon, gold in Accretion, red in Spaghettify, where a streamer tears off it.
        /// Spins faster during the Pull; `col`'s alpha fades it in as it spawns, and a hit whitens the band.
        void DrawSingularityCore(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            int heat = SingHeat(e);
            float spin = t * (e.Phase == Game.SingPull ? 2.2f : 0.7f);
            GlowDisc(pos, r * 3.4f, SingHeatCol(heat), (0.22f + 0.05f * heat) * col.A);
            DrawLensedHole(pos, r, heat, col.A, e.Flash > 0f || JuiceFlash(e) ? HitFlashMix(e) : 0f, spin, 0.35f * Mathf.Sin(t * 0.25f) - 0.25f, 7);
            if (heat == 2)  // torn: a streamer pulled off the band
                for (int k = 0; k < 10; k++)
                    Circle(pos + V2.FromAngle(t * 1.3f + k * 0.18f) * (r * 2.15f + k * 4f), 2.6f - k * 0.2f, SingRed.WithAlpha((0.6f - k * 0.05f) * col.A), 6);
        }

        /// A shard of the Split: a small lensed black hole in the core's heat, its band tilted its own way and spinning
        /// fast, a hot pip trailing it (drawn over DrawSingularityShardUnderlay's tether and glow).
        void DrawSingularityShard(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            Enemy core;
            int heat = game.Enemies.TryGetValue(e.ParentId, out core) ? SingHeat(core) : 2;
            float flash = e.Flash > 0f || JuiceFlash(e) ? HitFlashMix(e) : 0f;
            DrawLensedHole(pos, r * 0.8f, heat, col.A, flash, t * 1.8f + e.Id, e.Ang * 1.1f + 0.4f * Mathf.Sin(t * 0.6f + e.Id), 3);
        }

        /// A Gravity Well or a Cage Well: a small lensed black hole in the Singularity's phase heat, each turned its own
        /// way (Cage Wells smaller and spinning faster), over the well's pull rings (DrawWellPull).
        void DrawSingularityWell(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            bool cage = e.Type == EnemyType.CageWell;
            float flash = e.Flash > 0f || JuiceFlash(e) ? HitFlashMix(e) : 0f;
            int heat = SingHeatNow();
            GlowDisc(pos, r * 2.4f, SingHeatCol(heat), 0.22f * col.A);
            DrawLensedHole(pos, r * 0.85f, heat, col.A, flash, t * (cage ? 2.6f : 1.4f) + e.Id, e.Id * 1.3f + 0.3f * Mathf.Sin(t * 0.5f + e.Id), cage ? 2 : 3);
        }

        /// The living Singularity's heat (Event Horizon if there's none, e.g. a well outside the fight).
        int SingHeatNow()
        {
            foreach (var o in game.Enemies.Values)
                if (o.Type == EnemyType.Singularity)
                    return SingHeat(o);
            return 0;
        }

        /// The phase's heat for the band: 0 Event Horizon, 1 Accretion, 2 Spaghettify and the last stand.
        static int SingHeat(Enemy e) => e.Sing == null || e.Sing.Stage == SingStage.EventHorizon ? 0 : e.Sing.Stage == SingStage.Accretion ? 1 : 2;

        static Rgba SingHeatCol(int heat) => heat == 0 ? HorizonGlow : heat == 1 ? SingGold : SingRed;

        /// A lensed black hole of shadow radius r: the far half of its band, lensed over the top, the shadow and photon
        /// ring, then the near half in front and `clumps` hot clumps riding it. `fade` scales every alpha; `flash` whitens.
        void DrawLensedHole(V2 pos, float r, int heat, float fade, float flash, float spin, float tilt, int clumps)
        {
            Rgba mid = SingHeatCol(heat), hot = SingHot.Mix(Rgba.White, flash);
            const float squash = 0.3f;
            float outR = r * (heat == 0 ? 2f : heat == 1 ? 2.35f : 2.15f);
            SingBand(pos, r * 1.05f, outR, squash, tilt, MathX.Pi, MathX.Tau, hot.WithAlpha(0.85f * fade), mid.WithAlpha(0f), 28, spin);  // the far half
            SingBand(pos, r * 0.98f, r * 1.32f, 1f, tilt, MathX.Pi + 0.15f, MathX.Tau - 0.15f, hot.WithAlpha(0.7f * fade), mid.WithAlpha(0f), 20, spin);  // ...lensed up over the top
            Circle(pos, r * 0.95f, new Rgba(0f, 0f, 0f, fade), 36);  // the shadow
            Arc(pos, r * 0.97f, Mathf.Min(2.2f, r * 0.12f), hot.WithAlpha(0.9f * fade), 40);  // the photon ring
            Arc(pos, r * 1.06f, 1.2f, mid.WithAlpha(0.5f * fade), 40);
            SingBand(pos, r * 1.05f, outR, squash, tilt, 0f, MathX.Pi, hot.WithAlpha(0.95f * fade), mid.WithAlpha(0f), 28, spin);  // the near half, in front
            V2.Axes(tilt, out V2 ax, out V2 ay);
            for (int k = 0; k < clumps; k++)  // hot clumps riding the band (hidden behind the shadow on the far side)
            {
                float a = spin * 1.6f + k * MathX.Tau / clumps * 0.9f, rr = r * (1.2f + 0.12f * (k % 3));
                V2 at = pos + ax * (Mathf.Cos(a) * rr) + ay * (Mathf.Sin(a) * rr * squash);
                if (Mathf.Sin(a) > 0f || (at - pos).Length > r)
                    Circle(at, Mathf.Min(2.2f, r * 0.13f), hot.WithAlpha(0.8f * fade), 6);
            }
        }

        /// Part of the core's band from r0 to r1, squashed to an ellipse across `tilt`, between angles a0 and a1, fading
        /// from `inner` to `outer`; the side spinning toward you (at `spin`) is brightest.
        void SingBand(V2 c, float r0, float r1, float squash, float tilt, float a0, float a1, Rgba inner, Rgba outer, int segs, float spin)
        {
            V2.Axes(tilt, out V2 ax, out V2 ay);
            for (int i = 0; i < segs; i++)
            {
                float u0 = a0 + (a1 - a0) * i / segs, u1 = a0 + (a1 - a0) * (i + 1) / segs;
                float l0 = 1f - 0.375f * (1f + Mathf.Cos(u0 - spin)), l1 = 1f - 0.375f * (1f + Mathf.Cos(u1 - spin));
                V2 d0 = ax * Mathf.Cos(u0) + ay * (Mathf.Sin(u0) * squash), d1 = ax * Mathf.Cos(u1) + ay * (Mathf.Sin(u1) * squash);
                Rgba i0 = inner.WithAlpha(inner.A * l0), i1 = inner.WithAlpha(inner.A * l1), o0 = outer.WithAlpha(outer.A * l0), o1 = outer.WithAlpha(outer.A * l1);
                VTri(c + d0 * r0, i0, c + d0 * r1, o0, c + d1 * r1, o1);
                VTri(c + d0 * r0, i0, c + d1 * r1, o1, c + d1 * r0, i1);
            }
        }

        /// The accretion disk: two rings of orbs round the core, turning opposite ways (drawn with the hazards). Each orb is
        /// a gold ember in the core's Accretion heat, a fading trail of light behind it along its ring, a hot rim and the
        /// dark core every boss orb has; each ring's track glows faintly gold.
        void DrawSingularityDisk(Enemy sing)
        {
            if (!Game.DiskUp(sing))
                return;
            for (int ring = 0; ring < 2; ring++)
            {
                int n = ring == 0 ? Game.DiskInnerN : Game.DiskOuterN;
                float rad = ring == 0 ? Game.DiskInnerR : Game.DiskOuterR, dir = Mathf.Sign(ring == 0 ? Game.DiskInnerSpin : Game.DiskOuterSpin);
                Arc(sing.Disp, rad, 6f, SingGold.WithAlpha(0.06f), 64);  // its track
                Arc(sing.Disp, rad, 1.2f, SingGold.WithAlpha(0.18f), 64);
                for (int i = 0; i < n; i++)
                {
                    V2 at;
                    if (!Game.DiskOrb(sing.Sing, sing.Disp, ring, i, out at))
                        continue;
                    float r = Game.DiskOrbR, a = (at - sing.Disp).Angle;
                    for (int k = 4; k >= 1; k--)  // the trail, behind it along the ring
                        Circle(sing.Disp + V2.FromAngle(a - dir * k * (r * 0.55f) / rad) * rad, r * (1f - 0.17f * k), SingGold.WithAlpha(0.4f - 0.08f * k), 10);
                    GlowDisc(at, r * 2.6f, SingGold, 0.35f);
                    Circle(at, r, SingGold, 14);
                    Arc(at, r * 0.72f, r * 0.32f, SingHot, 14);  // the hot rim
                    Circle(at, r * 0.4f, new Rgba(0.12f, 0.04f, 0f, 0.9f), 10);  // a boss orb's dark core
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
                DrawStageShiftBoss(e, pos, core, t);  // changing phase (StageShiftLook.cs)
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
            if (e.Phase == Game.SingSplit)
                for (int k = 0; k < 16; k++)  // a dashed shell: it can't be hurt, the shards can
                    Arc(pos, edge + 14f, 2f, Rgba.White.WithAlpha(0.5f), 6, k * MathX.Tau / 16f + t * 0.5f, MathX.Tau / 32f);
        }

        /// Under a shard of the Split: a faint tether to the core and a pulsing glow in the core's heat.
        void DrawSingularityShardUnderlay(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            Enemy core;
            bool has = game.Enemies.TryGetValue(e.ParentId, out core);
            Rgba heat = SingHeatCol(has ? SingHeat(core) : 2);
            if (has)
                Line(core.Disp, pos, 1.5f, heat.WithAlpha(0.25f * col.A));
            GlowDisc(pos, r * 2.6f, heat, (0.2f + 0.08f * Mathf.Sin(t * 6f + e.Id)) * col.A);
        }
    }
}
