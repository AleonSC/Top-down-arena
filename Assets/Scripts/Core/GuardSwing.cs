using System;

namespace Arena
{
    /// The Warden's riot Guard's baton: it never stops walking, and while its target is within BatonTrigger it swings fast,
    /// forehand then backhand: a short BatonWind draw-back (its arc flashes on the floor), a BatonFlash sweep across BatonHalf
    /// either side of where it faces out to BatonReach (BatonDamage to every player in the arc), then BatonEvery before the
    /// next, the other way. It reuses the Butcher's state (Enemy.LarderStage / LarderT / LarderCd, replicated by SyncLarder)
    /// with its own stages: BatonWinding/BatonActing sweep from its left, BatonWindingBack/BatonActingBack from its right.
    /// Unlike the Butcher it isn't planted and keeps turning (it isn't in LarderAttacks). Look: Game/WardenLook.cs.
    public partial class Game
    {
        public const float BatonTrigger = 46f, BatonReach = 80f, BatonHalf = MathX.Pi / 3f, BatonWind = 0.16f, BatonFlash = 0.12f, BatonEvery = 0.32f, BatonDamage = 7f;
        public const byte BatonWinding = 1, BatonActing = 2, BatonWindingBack = 3, BatonActingBack = 4;

        sealed class GuardSwingSpecial : SpecialPart
        {
            public override void Tick(Game g, Enemy e, V2 tpos, float dt) => g.GuardBaton(e, tpos, dt);
        }

        /// A Guard drawing its baton back: how far, 0 to 1, or 0 when it isn't (drawing).
        public static float BatonWindT(Enemy e) => e.LarderStage == BatonWinding || e.LarderStage == BatonWindingBack ? 1f - MathX.Clamp01(e.LarderT / BatonWind) : 0f;

        /// A Guard mid-swing: how far through it, 0 to 1, or -1 when it isn't swinging (drawing).
        public static float BatonSwingT(Enemy e) => e.LarderStage == BatonActing || e.LarderStage == BatonActingBack ? 1f - MathX.Clamp01(e.LarderT / BatonFlash) : -1f;

        /// Which way the swing goes: +1 from its left across to its right (forehand), -1 back (backhand).
        public static float BatonSide(Enemy e) => e.LarderStage >= BatonWindingBack ? -1f : 1f;

        /// Host, in Special: starts, runs and ends a Guard's swings, alternating sides.
        void GuardBaton(Enemy e, V2 tpos, float dt)
        {
            e.LarderCd -= dt;
            byte st = e.LarderStage;
            if (st == LarderIdle)
            {
                if (e.LarderCd > 0f || e.SpawnT > 0f || e.Pos.FartherThan(tpos, BatonTrigger + e.Radius))
                    return;
                e.LarderStage = e.MawSide == 1 ? BatonWindingBack : BatonWinding;  // (MawSide: which way the last swing went)
                e.LarderT = BatonWind;
                return;
            }
            e.LarderT -= dt;
            if (e.LarderT > 0f)
                return;
            if (st == BatonWinding || st == BatonWindingBack)
            {
                e.LarderStage = (byte)(st + 1);
                e.LarderT = BatonFlash;
                foreach (var p in Players.Values)
                    if (Hittable(p) && p.SwallowT <= 0f && InCone(e.Pos, e.Facing, BatonHalf, BatonReach, p.Pos, p.Def.HitR))
                        Damage(p, BatonDamage, "Guard");
                BroadcastFx(FxKind.Spark, e.Pos + V2.FromAngle(e.Facing) * (e.Radius + 14f), new Rgba(1f, 0.9f, 0.7f), 16f, 0.14f, Sfx.SwingLight);
                return;
            }
            e.MawSide = (byte)(st == BatonActing ? 1 : 0);
            e.LarderStage = LarderIdle;
            e.LarderCd = BatonEvery;
        }
    }
}
