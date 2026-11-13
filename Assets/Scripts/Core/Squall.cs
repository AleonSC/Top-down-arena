using System;

namespace Arena
{
    /// The Storm Herald's faction, the Squall (Docs/StormFaction.md, mockup Docs/Mockups/storm-herald-faction.html; drawn in
    /// Game/SquallLook.cs). Each enemy previews one of the Herald's attacks. Step 1 has three:
    ///   Dust Devil (the Gale's wind): a melee whirlwind that weaves in (a sway across your line, Enemy.Strafe its side).
    ///     A touch hurts and shoves you DevilShove px the way it was going; then it veers off for DevilVeer s (Phase 1).
    ///   Squall Gunner (Crosswind): keeps GunnerRange and strafes; every GunnerEvery s it fires three shots GunnerLead rad
    ///     upwind of you (its own wind's side, Enemy.Strafe, kept for its life) that curve back onto where you stood.
    ///   Stormcaller (Strikes): keeps CallerRange; every CallerEvery s it calls a strike on you, the Herald's warning circle
    ///     (a Hazard of kind HazardCaller: CallerRadius, CallerWarn) that lands for CallerDamage. Hazards are in the
    ///     snapshot and drawn like the Herald's, so it needs no new fields.
    public partial class Game
    {
        public const float DevilShove = 120f, DevilVeer = 0.7f, DevilSway = 0.55f;
        public const float GunnerRange = 260f, GunnerEvery = 2.2f, GunnerLead = 0.45f, GunnerShotSpeed = 220f, GunnerFan = 0.06f;
        public const float CallerRange = 300f, CallerEvery = 2.8f, CallerRadius = 40f, CallerWarn = 1.1f;
        public const byte HazardCaller = 2;  // a Stormcaller's strike (Hazard.Kind; HazardLightning and HazardHand are in Lich.cs)
        float CallerDamage => 20f;  // the difficulty's per-wave damage growth scales it, like the Herald's 25

        public static bool IsSquall(EnemyType t) => t >= EnemyType.DustDevil && t <= EnemyType.Stormcaller;

        V2 SquallMove(Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed)
        {
            switch (e.Type)
            {
                case EnemyType.DustDevil:
                {
                    if (e.Phase == 1)
                    {
                        // Veering off after a shove, then back in.
                        e.PhaseT -= dt;
                        if (e.PhaseT <= 0f)
                            e.Phase = 0;
                        return (-dir + dir.Orthogonal() * e.Strafe * 1.2f).Normalized();
                    }
                    // A weave: a sway across your line that dies away up close.
                    float k = MathX.Clamp01((dist - 60f) / 200f);
                    float a = DevilSway * k * (float)Math.Sin(e.T * 4.2f + e.Id);
                    return dir * (float)Math.Cos(a) + dir.Orthogonal() * (float)Math.Sin(a);
                }
                case EnemyType.SquallGunner:
                    return KeepDistance(dir, dist, GunnerRange) + dir.Orthogonal() * e.Strafe * 0.5f;
                case EnemyType.Stormcaller:
                    return KeepDistance(dir, dist, CallerRange) + dir.Orthogonal() * e.Strafe * 0.35f;
            }
            return dir;
        }

        /// The Dust Devil's touch (from the contact loop, after its damage): shove the player the way it was going, and veer off.
        void DevilTouched(Player p, Enemy e)
        {
            if (e.Type != EnemyType.DustDevil || p.Downed)
                return;
            KnockPlayer(p, p.Pos - e.Pos, DevilShove);  // (it was coming straight at you)
            e.Phase = 1;
            e.PhaseT = DevilVeer;
            BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.62f, 0.91f, 0.85f), 30f, 0.25f, Sfx.None);
        }

        /// Returns true when it handled the shot.
        bool SquallFire(Enemy e, V2 tpos, float aim)
        {
            switch (e.Type)
            {
                case EnemyType.SquallGunner:
                    e.FireT = GunnerEvery;
                    GunnerVolley(e, tpos, aim);
                    return true;
                case EnemyType.Stormcaller:
                    e.FireT = CallerEvery;
                    AddHazard(tpos, CallerRadius, CallerWarn).Kind = HazardCaller;
                    BroadcastFx(FxKind.Spark, e.Pos + new V2(0f, -e.Radius - 8f), new Rgba(1f, 0.82f, 0.4f), 16f, 0.2f, Sfx.SpellZap);
                    return true;
            }
            return false;
        }

        /// Three shots that leave GunnerLead upwind of you (its Strafe side) and curve back to cross where you stood: a shot
        /// turning at a steady rate runs along a circle, so to cross the chord d after setting off at angle s to it, it turns
        /// 2s over an arc of d·s/sin(s) (as the Acolyte's orbs, Infall.cs). The two outer shots fan GunnerFan either side.
        void GunnerVolley(Enemy e, V2 tpos, float aim)
        {
            float d = Math.Max(120f, Math.Min(600f, tpos.DistanceTo(e.Pos) - e.Radius));
            float side = e.Strafe >= 0f ? 1f : -1f, s = GunnerLead;
            float arc = d * s / (float)Math.Sin(s), time = arc / GunnerShotSpeed, turn = -side * 2f * s / time;
            for (int k = -1; k <= 1; k++)
            {
                float ang = aim + side * s + k * GunnerFan;
                V2 dir = V2.FromAngle(ang);
                var b = SpawnBullet(Defs.SmallShot, e.Pos + dir * e.Radius, dir * GunnerShotSpeed, turn, time * 1.6f + 0.4f, Defs.BulletDamage(Defs.SmallShot), 0, false);
                b.Source = e.Type;
                b.Shooter = e.Id;
                b.Look = b.Sent.Look = LookFor(e, ang, turn);
                NoteVolley(e, b);
            }
            BroadcastFx(FxKind.Spark, e.Pos + V2.FromAngle(aim + side * s) * (e.Radius + 8f), new Rgba(0.56f, 0.82f, 1f), 14f, 0.15f, Sfx.EnemyShot);
        }

        /// A Stormcaller's strike landing (TickHazards): the Herald's bolt, its own damage and name.
        void CallerStrike(Hazard h)
        {
            foreach (var p in Players.Values)
                if (!p.Downed && p.InvulnT <= 0f && p.Pos.DistanceTo(h.Pos) < h.Radius)
                    Damage(p, CallerDamage, "Stormcaller (lightning)");
            var color = new Rgba(1f, 0.85f, 0.5f);
            BroadcastFx(FxKind.Bolt, new V2(h.Pos.X + Rand(-40f, 40f), h.Pos.Y - 260f), h.Pos, color, 0f, 0.22f, Sfx.SpellZap);
            BroadcastFx(FxKind.Boom, h.Pos, color, h.Radius, 0.3f, Sfx.None);
        }
    }
}
