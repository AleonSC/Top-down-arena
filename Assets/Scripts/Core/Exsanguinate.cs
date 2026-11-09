using System;
using System.Collections.Generic;

namespace Arena
{
    /// Exsanguinate (Tank Hemomancer, Choice 2), the "Barbed Lash" rework from Docs/Mockups/exsanguinate-rework.html.
    /// Pay 10% of your max health: barbed blood whips crack out of you to the edge of the reach (DrainRadius) and hook every enemy in
    /// it. The hook rips the enemy's own veins out toward you, the two braid together, then one hard yank throws it in to touching you.
    /// It is stunned (`Enemy.GraspT`) from the cast until it lands. The drain lands on arrival, not at the cast: 20 into you from each
    /// (Deep Veins: more, and farther). Heavy enemies and bosses aren't moved (the tendril strains against them) but still get drained.
    /// Mastered: what it drains is slowed 50% for 2 s, as before.
    ///
    /// Host: the reel lives on the enemy (`ReelBy`, `ReelT`, `ReelFrom`, ...), ticked by TickReels from TickBloodMages. The pull is
    /// ordinary replicated movement. Screens draw it from FxKind.Exsanguinate (Game/ExsanguinateLook.cs):
    ///   cast:   Pos = the caster, Pos2 = (-1, caster's id), Radius = the reach, Dur = ExsCastFx;
    ///   tendril, one per enemy hooked: Pos = where it stood, Pos2 = (its id, caster's id), Radius = its size,
    ///           Color.A = 1 when it's too heavy to move, Dur = ExsShow + its stagger (so the stagger is Dur - ExsShow);
    ///   impact: Pos = the caster, Pos2 = (-2, caster's id), Radius = enemies that landed (the burst, the shake and the hit-pause).
    public partial class Game
    {
        // The show, in seconds from the cast (before each enemy's stagger, up to ExsStagger for one at the edge of the reach).
        public const float ExsHook = 0.34f, ExsBraid = 0.46f, ExsYank = 0.62f, ExsLand = 0.88f, ExsFade = 1.25f, ExsShow = 1.6f;
        public const float ExsWhipStart = 0.12f, ExsStagger = 0.06f, ExsCastFx = 0.6f;
        const float ReelHold = 0.25f;  // the stun each tick tops up while it's reeled

        /// How far along its yank an enemy is, 0 to 1, `el` seconds after its own start (an ease-in: slow, then hard).
        public static float ExsPull(float el)
        {
            float k = Math.Max(0f, Math.Min(1f, (el - ExsYank) / (ExsLand - ExsYank)));
            return k * k * k * k;
        }

        /// Where a reeled enemy ends up: touching the caster, on the side it came from.
        V2 ReelEnd(Player p, Enemy e)
        {
            V2 away = (e.ReelFrom - p.Pos).Normalized();
            if (away.IsZero)
                away = V2.FromAngle(p.Aim);
            return ClampToArena(p.Pos + away * (e.Radius + p.Def.BodyR + 5f), e.Radius);
        }

        void CastExsanguinate(Player p, bool cap, Rgba blood)
        {
            var s = p.Stats;
            float r = DrainRadius * s.DrainRadiusMult, drain = DrainAmount * s.DrainMult;
            foreach (var e in Enemies.Values)
            {
                float d = e.Pos.DistanceTo(p.Pos);
                if (!Targetable(e) || d > r + e.Radius)
                    continue;
                bool heavy = Heavy(e);
                float stagger = ExsStagger * Math.Min(1f, d / Math.Max(1f, r));
                e.ReelBy = p.Id;
                e.ReelT = -stagger;
                e.ReelFrom = e.Pos;
                e.ReelHeal = drain;
                e.ReelDmg = drain * s.DmgMult;
                e.ReelCap = cap;
                if (!heavy)
                    e.GraspT = Math.Max(e.GraspT, ReelHold);
                BroadcastFx(FxKind.Exsanguinate, e.Pos, new V2(e.Id, p.Id), blood.WithAlpha(heavy ? 1f : 0.5f), e.Radius, ExsShow + stagger, Sfx.None);
            }
            BroadcastFx(FxKind.Exsanguinate, p.Pos, new V2(-1f, p.Id), blood, r, ExsCastFx, Sfx.Shockwave);  // the whips crack out
        }

        readonly Dictionary<int, int> reelLanded = new Dictionary<int, int>();

        /// Host, every tick: hold reeled enemies, yank them in, and drain them when they land.
        void TickReels(float dt)
        {
            reelLanded.Clear();
            foreach (var e in Enemies.Values)
            {
                if (e.ReelBy == 0)
                    continue;
                Player p;
                if (e.Hp <= 0f || !Players.TryGetValue(e.ReelBy, out p) || p.Downed)
                {
                    e.ReelBy = 0;  // it died on the way, or you went down: the tendril lets go
                    continue;
                }
                e.ReelT += dt;
                bool heavy = Heavy(e);
                if (!heavy)
                {
                    e.GraspT = Math.Max(e.GraspT, ReelHold);
                    float k = ExsPull(e.ReelT);
                    if (k > 0f)
                        e.Pos = V2.Lerp(e.ReelFrom, ReelEnd(p, e), k);
                }
                if (e.ReelT < ExsLand)
                    continue;
                e.ReelBy = 0;
                HurtEnemy(e, e.ReelDmg, p.Id);
                Heal(p, e.ReelHeal, p);
                if (e.ReelCap && !e.Def.IsBoss)
                {
                    e.SlowT = Math.Max(e.SlowT, 2f);
                    e.SlowMult = Math.Min(e.SlowMult, 0.5f);
                }
                int n;
                reelLanded.TryGetValue(p.Id, out n);
                reelLanded[p.Id] = n + 1;
            }
            foreach (var kv in reelLanded)
            {
                Player p;
                if (Players.TryGetValue(kv.Key, out p))
                    BroadcastFx(FxKind.Exsanguinate, p.Pos, new V2(-2f, p.Id), new Rgba(0.85f, 0.12f, 0.2f), kv.Value, 0.5f, Sfx.Shockwave);
            }
        }
    }
}
