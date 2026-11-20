using System;

namespace Arena
{
    /// The Warden's Thug's charged punch: with its target within PunchTrigger of its front it plants its feet and draws a
    /// fist back for PunchWind (its facing locked, a narrow wedge filling on the floor ahead of it), then jabs: PunchDamage
    /// to every player in the wedge (PunchHalf either side, out to PunchReach), PunchFlash of follow-through, then
    /// PunchEvery before the next. Step sideways out of the wedge to dodge it. It reuses the Butcher's state
    /// (Enemy.LarderStage / LarderT / LarderCd, replicated by SyncLarder; planted and face-locked by LarderPlanted).
    /// Look: Game/PrisonLook.cs (DrawThugFloor, the fist in DrawThugBody).
    public partial class Game
    {
        public const float PunchTrigger = 34f, PunchReach = 64f, PunchHalf = 0.42f, PunchWind = 0.6f, PunchFlash = 0.14f, PunchEvery = 1.4f, PunchDamage = 18f;

        sealed class ThugPunchSpecial : SpecialPart
        {
            public override void Tick(Game g, Enemy e, V2 tpos, float dt) => g.ThugPunch(e, tpos, dt);
        }

        /// A Thug drawing its fist back: how far, 0 to 1, or 0 when it isn't (drawing).
        public static float PunchWindT(Enemy e) => e.Type == EnemyType.Thug && e.LarderStage == LarderWinding ? 1f - MathX.Clamp01(e.LarderT / PunchWind) : 0f;

        /// A Thug mid-punch: how far through it, 0 to 1, or -1 when it isn't punching (drawing).
        public static float PunchJabT(Enemy e) => e.Type == EnemyType.Thug && e.LarderStage == LarderActing ? 1f - MathX.Clamp01(e.LarderT / PunchFlash) : -1f;

        /// Host, in Special: starts, lands and ends a Thug's punches.
        void ThugPunch(Enemy e, V2 tpos, float dt)
        {
            e.LarderCd -= dt;
            byte st = e.LarderStage;
            if (st == LarderIdle)
            {
                if (e.LarderCd > 0f || e.SpawnT > 0f || e.Pos.FartherThan(tpos, PunchTrigger + e.Radius))
                    return;
                e.Facing = (tpos - e.Pos).Angle;  // squares up to you, then holds it (LarderFaceLocked)
                e.LarderStage = LarderWinding;
                e.LarderT = PunchWind;
                return;
            }
            e.LarderT -= dt;
            if (e.LarderT > 0f)
                return;
            if (st == LarderWinding)
            {
                e.LarderStage = LarderActing;
                e.LarderT = PunchFlash;
                foreach (var p in Players.Values)
                    if (!p.Downed && p.InvulnT <= 0f && p.SwallowT <= 0f && InCone(e.Pos, e.Facing, PunchHalf, PunchReach, p.Pos, p.Def.HitR))
                        Damage(p, PunchDamage, "Thug");
                BroadcastFx(FxKind.Spark, e.Pos + V2.FromAngle(e.Facing) * (e.Radius + 22f), new Rgba(1f, 0.85f, 0.6f), 20f, 0.16f, Sfx.SwingHeavy);
                return;
            }
            e.LarderStage = LarderIdle;
            e.LarderCd = PunchEvery;
        }
    }
}
