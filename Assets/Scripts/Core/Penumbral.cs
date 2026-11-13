using System;

namespace Arena
{
    /// The Eclipse's faction, the Penumbral Court (Docs/EclipseFaction.md, mockup Docs/Mockups/eclipse-faction.html; drawn
    /// in Game/PenumbralLook.cs). Each enemy previews one thing the Eclipse's stages test. Step 1 has three:
    ///   Ember (Lunar wake): melee, weaves in at you and drops a spark (Defs.EmberSpark) every EmberSparkEvery s that
    ///     drifts back slowly and hangs for EmberSparkLife s, so crossing its trail hurts. Its age (Enemy.T) times the sparks.
    ///   Corona Priest (Corona rings): keeps PriestRange and strafes; winds up PriestWindUp s (its halo flares), then fires
    ///     a ring of PriestSlots - 1 slow shots: one straight at you, and the gap one slot to the side (Strafe picks it).
    ///   Heliograph (Solar flares): Phase 0 walks in to HelioRange; 1 plants and aims its mirror at you, a line flickers
    ///     for HelioWarn s; 2 the line is a lane of fire HelioWidth wide for HelioBurn s; 3 rests HelioRest s. Phase and
    ///     Facing are in the snapshot, so clients draw the line with no extra fields (timed from Enemy.SeenT).
    public partial class Game
    {
        public const float EmberWeave = 0.55f, EmberSparkEvery = 0.2f, EmberSparkLife = 1.5f, EmberSparkDrift = 25f, EmberSparkRange = 650f;
        public const float PriestRange = 260f, PriestEvery = 2.4f, PriestWindUp = 0.7f, PriestShotSpeed = 130f;
        public const int PriestSlots = 14;
        public const float HelioRange = 340f, HelioWarn = 1f, HelioBurn = 0.5f, HelioRest = 2.5f, HelioLen = 900f, HelioWidth = 40f;
        public const int MaxHeliographs = 3;  // at most this many a wave (CapBeams in Variety.cs)
        float HelioDamage => 20f;  // the difficulty's per-wave damage growth scales it, like the Pulsar's

        public static bool IsPenumbral(EnemyType t) => t >= EnemyType.Ember && t <= EnemyType.Heliograph;

        /// The Heliograph keeps its mirror's line while it flickers and burns.
        public static bool HelioLocked(Enemy e) => e.Type == EnemyType.Heliograph && (e.Phase == 1 || e.Phase == 2);

        V2 PenumbralMove(Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed)
        {
            switch (e.Type)
            {
                case EnemyType.Ember:
                {
                    // Weaves either side of your line, straighter up close.
                    float a = EmberWeave * (float)Math.Sin(e.T * 4f + e.Id) * MathX.Clamp01(dist / 200f);
                    return dir * (float)Math.Cos(a) + dir.Orthogonal() * (float)Math.Sin(a);
                }
                case EnemyType.CoronaPriest:
                    return KeepDistance(dir, dist, PriestRange) + dir.Orthogonal() * e.Strafe * 0.6f;
                case EnemyType.Heliograph:
                    if (HelioLocked(e))
                        return V2.Zero;  // planted while its mirror aims and burns
                    return KeepDistance(dir, dist, HelioRange) + dir.Orthogonal() * e.Strafe * 0.3f;
            }
            return dir;
        }

        void PenumbralSpecial(Enemy e, V2 tpos, float dt)
        {
            if (e.Type == EnemyType.Ember)
                EmberSparks(e, tpos, dt);
            else if (e.Type == EnemyType.Heliograph)
                HelioStep(e, tpos, dt);
        }

        /// An Ember's trail: a spark every EmberSparkEvery s while it's near a player, drifting back the way it came (away from you).
        void EmberSparks(Enemy e, V2 tpos, float dt)
        {
            if ((int)(e.T / EmberSparkEvery) == (int)((e.T - dt) / EmberSparkEvery))
                return;  // one every EmberSparkEvery s of its life
            if (tpos.DistanceTo(e.Pos) > EmberSparkRange || tpos.DistanceTo(e.Pos) < 1f)
                return;
            V2 back = (e.Pos - tpos).Normalized();
            var b = SpawnBullet(Defs.EmberSpark, e.Pos + back * e.Radius, back * EmberSparkDrift, 0f, EmberSparkLife, Defs.BulletDamage(Defs.EmberSpark), 0, false);
            b.Source = e.Type;
            b.Shooter = e.Id;
        }

        void HelioStep(Enemy e, V2 tpos, float dt)
        {
            e.PhaseT -= dt;
            float dist = tpos.DistanceTo(e.Pos);
            switch (e.Phase)
            {
                case 0:
                case 3:
                    if (e.Phase == 3 && e.PhaseT > 0f)
                        break;
                    if (e.T > 1.2f && dist < HelioRange + 90f)
                    {
                        e.Facing = (tpos - e.Pos).Angle;  // the mirror's line, locked on where you stand
                        e.Phase = 1;
                        e.PhaseT = HelioWarn;
                        BroadcastSound(Sfx.BossCharge);
                    }
                    else
                        e.Phase = 0;
                    break;
                case 1:
                    if (e.PhaseT <= 0f)
                    {
                        e.Phase = 2;
                        e.PhaseT = HelioBurn;
                        BroadcastFx(FxKind.Spark, e.Pos + V2.FromAngle(e.Facing) * (e.Radius + 6f), e.Def.Color, 22f, 0.2f, Sfx.Explode);
                    }
                    break;
                case 2:
                    foreach (var p in Players.Values)
                        if (!p.Downed && p.InvulnT <= 0f && HelioLaneDistance(e, p.Pos) < HelioWidth / 2f + p.Def.HitR)
                            Damage(p, HelioDamage, e.Def.Name + " (flare)");
                    if (e.PhaseT <= 0f)
                    {
                        e.Phase = 3;
                        e.PhaseT = HelioRest / TraitFireMult(e);
                    }
                    break;
            }
        }

        /// How far a point is from a Heliograph's lane (from it, along its Facing).
        public static float HelioLaneDistance(Enemy e, V2 p) => SegmentDistance(p, e.Pos, e.Pos + V2.FromAngle(e.Facing) * HelioLen);

        /// Returns true when it handled the shot.
        bool PenumbralFire(Enemy e, V2 tpos, float aim)
        {
            switch (e.Type)
            {
                case EnemyType.Heliograph:
                    e.FireT = 99f;  // its flare is its attack (HelioStep)
                    return true;
                case EnemyType.CoronaPriest:
                    e.FireT = PriestEvery;
                    CoronaRing(e, aim);
                    return true;
            }
            return false;
        }

        /// A ring of PriestSlots slots, one straight at you, with the slot beside it (the Strafe side) left open.
        void CoronaRing(Enemy e, float aim)
        {
            int gap = e.Strafe >= 0f ? 1 : PriestSlots - 1;
            for (int k = 0; k < PriestSlots; k++)
                if (k != gap)
                    EnemyShot(e, aim + k * MathX.Tau / PriestSlots, PriestShotSpeed, Defs.SmallShot);
            BroadcastFx(FxKind.Ring, e.Pos, e.Def.Color, 34f, 0.25f, Sfx.EnemyShot);
        }

        /// How much a bot should avoid this spot near a Penumbral Court enemy (BotBrain.cs): a Heliograph's line.
        float PenumbralBotDanger(Enemy e, V2 p, float hitR) =>
            HelioLocked(e) && HelioLaneDistance(e, p) < HelioWidth / 2f + hitR + 30f ? 2.5f : 0f;
    }
}
