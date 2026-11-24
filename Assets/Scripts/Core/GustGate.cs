using System;

namespace Arena
{
    /// Gust Gate, the Healer Elementalist's 2nd choice (`AbilityId.GustGate`, `ZoneKind.GustGate`), from the Gust Gate mockup
    /// (variant A, Vortex): a ring of wind GgRadius px round at your cursor (up to GgReach away) for GgLife s.
    ///   boost: a player (you included) whose step crosses the ring's edge, either way, gets Player.GustT = GgBoostT s of up to
    ///     +GgBoost move speed (full until the last GgFade s, then fading out: GustMult). Each player once per GgRetrigger s; a new
    ///     pass refreshes it. FxKind.Ring at the player (the whoosh) with Sfx.DodgeSwish.
    ///   push: enemies inside are pushed gently out from its middle, GgPush px/s (heavies half, bosses not at all); the cast
    ///     shoves the ones already inside GgCastPush px.
    /// Mastered: the ring is GgMasteredSize times as wide and pushes twice as hard. Its Rare card Jet Stream adds
    /// Stats.GustLifeBonus and GustBoostBonus. GustT is replicated (the speed and the look); GustCd is host only.
    /// The look: Game/GustGateLook.cs.
    public partial class Game
    {
        public const float GgCooldown = 14f, GgReach = 340f, GgRadius = 70f, GgLife = 8f, GgBoost = 0.8f, GgBoostT = 3f, GgFade = 1f;
        public const float GgRetrigger = 1.2f, GgPush = 55f, GgCastPush = 30f, GgMasteredSize = 1.4f;
        public static readonly Rgba GgColor = new Rgba(0.78f, 0.96f, 1f);  // pale wind cyan

        /// The speed multiplier for `t` s of boost left: 1 + GgBoost, fading to 1 over the last GgFade s.
        public static float GustMult(float t) => t <= 0f ? 1f : 1f + GgBoost * Math.Min(1f, t / GgFade);

        void CastGustGate(Player p, V2 target, bool cap)
        {
            V2 to = target - p.Pos;
            if (to.Length > GgReach)
                target = p.Pos + to.Normalized() * GgReach;
            target = ClampToArena(target, 10f);
            var z = AddZone(ZoneKind.GustGate, p, target, GgRadius * (cap ? GgMasteredSize : 1f), GgLife + p.Stats.GustLifeBonus);
            z.Cap = cap;
            foreach (var e in Enemies.Values)
                if (!e.Def.IsBoss && e.SpawnT <= 0f && e.Pos.CloserThan(z.Pos, z.Radius + e.Radius))
                {
                    WindHit(e, PushDir(z, e));  // Wind.cs
                    KnockEnemy(e, PushDir(z, e) * GgCastPush * (e.Def.Heavy ? 0.5f : 1f) * (cap ? 2f : 1f));
                }
            BroadcastFx(FxKind.Ring, target, GgColor, z.Radius, 0.45f, Sfx.GaleCast);
        }

        static V2 PushDir(Zone z, Enemy e)
        {
            V2 d = e.Pos - z.Pos;
            return d.Length > 0.5f ? d.Normalized() : V2.FromAngle(e.Id * 2.39996f);  // dead centre: a fixed, per-enemy way out
        }

        /// Host, every tick (before players move): boost times run down.
        void TickGustTimers(Player p, float dt)
        {
            p.GustT = Math.Max(0f, p.GustT - dt);
            p.GustCd = Math.Max(0f, p.GustCd - dt);
        }

        /// Host, every tick, for each Gust Gate: players crossing its edge are boosted, enemies inside pushed out.
        void TickGustGate(Zone z, float dt)
        {
            Player owner = PlayerOrNull(z.Owner);
            float boostT = GgBoostT + (owner != null ? owner.Stats.GustBoostBonus : 0f);
            foreach (var q in Players.Values)
            {
                if (q.Downed)
                    continue;
                bool inNow = q.Pos.CloserThan(z.Pos, z.Radius), inBefore = q.GustLast.CloserThan(z.Pos, z.Radius);
                if (inNow != inBefore && q.GustCd <= 0f && q.GustSeen)
                {
                    q.GustT = boostT;
                    q.GustCd = GgRetrigger;
                    BroadcastFx(FxKind.Ring, q.Pos, GgColor, 46f, 0.4f, Sfx.DodgeSwish);
                }
            }
            float push = GgPush * (z.Cap ? 2f : 1f) * dt;
            foreach (var e in Enemies.Values)
                if (!e.Def.IsBoss && e.SpawnT <= 0f && e.Pos.CloserThan(z.Pos, z.Radius + e.Radius))
                {
                    WindHit(e, PushDir(z, e));  // Wind.cs
                    KnockEnemy(e, PushDir(z, e) * push * (e.Def.Heavy ? 0.5f : 1f));
                }
        }

        /// Host, every tick after the zones: remember where each player stood, for next tick's crossing test.
        void RememberGustSteps()
        {
            foreach (var q in Players.Values)
            {
                q.GustLast = q.Pos;
                q.GustSeen = true;
            }
        }
    }
}
