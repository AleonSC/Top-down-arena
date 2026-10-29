using System;

namespace Arena
{
    /// The Repulsion Cannon (the Soldier Healer's first weapon, WeaponId.LightBolt): a cone blast that shoves enemies
    /// away, does a little damage, and knocks tiny health orbs loose from what it hits.
    public partial class Game
    {
        const float RepelHalfArc = 0.61f;      // 70 degrees across...
        const float RepelArcPerExtra = 0.175f; // ...20 degrees wider per extra projectile
        const float RepelPush = 90f, RepelPushPerExtra = 30f, RepelDmgPerExtra = 2f;
        const float ShedChance = 0.3f, ShedEvery = 1.5f;  // a hit knocks a tiny orb loose 30% of the time, at most every 1.5 s per enemy

        void FireRepulsion(Player p, float aim, float dmg, int extra)
        {
            var w = p.Weapon;
            float reach = w.Reach * AreaMult(p), half = RepelHalfArc + RepelArcPerExtra * extra;
            float hit = (w.Damage + RepelDmgPerExtra * extra) * dmg, push = RepelPush + RepelPushPerExtra * extra;
            V2 origin = p.Pos;
            BroadcastFx(FxKind.Repulse, origin, origin + V2.FromAngle(aim) * (reach + p.Def.BodyR), new Rgba(0.55f, 0.95f, 1f), half, 0.35f, Sfx.Shockwave);
            bool any = false;
            foreach (var e in new System.Collections.Generic.List<Enemy>(Enemies.Values))
            {
                if (e.SpawnT > 0f || e.Hp <= 0f)
                    continue;
                V2 rel = e.Pos - origin;
                float d = rel.Length;
                if (d - e.Radius > reach + p.Def.BodyR)
                    continue;
                float slack = d > 1f ? (float)Math.Asin(Math.Min(1f, e.Radius / d)) : MathX.Pi;
                float off = Math.Abs(MathX.Fmod(rel.Angle - aim + MathX.Pi, MathX.Tau) - MathX.Pi);
                if (off > half + slack)
                    continue;
                if (BlocksShot(e, origin))
                {
                    WearShield(e, hit);  // a shield takes the blast (and wears)
                    continue;
                }
                any = true;
                HurtEnemy(e, hit, p.Id);
                if (!Heavy(e))
                {
                    V2 away = d > 1f ? rel / d : V2.FromAngle(aim);
                    e.Pos = ClampToArena(e.Pos + away * push, e.Radius);
                }
                // Knock a tiny health orb loose now and then.
                if (e.T >= e.ShedT && rng.NextDouble() < ShedChance)
                {
                    e.ShedT = e.T + ShedEvery;
                    SpawnOrb(e.Pos, false, true);
                }
            }
            if (any)
                p.Run.ShotsHit++;
        }
    }
}
