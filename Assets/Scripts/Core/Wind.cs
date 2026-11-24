using System;
using System.Collections.Generic;

namespace Arena
{
    /// Wind reactions (enemies only): wind has no status of its own; it reacts with the others. Every wind source calls WindHit on
    /// the enemies it pushes: Wind Clash's waves and Tempest, Gale Wall's blast, Gust Gate (the cast and its steady push), the Spirit
    /// Fan's gust, the Wyvern's gale and the Nimbus's hurricane burst. An enemy reacts at most once every WindEvery s, so steady
    /// winds don't turn into a laser.
    ///   Wind Chill (wet): +1 chill and the Frost Lance's 30% slow for 3 s; at 3 chill it freezes (FrostLance.cs numbers). Bosses
    ///     only get the slow. After a wind freeze, wind can't chill that enemy for WindChillImmune s (or the meltwater, Steam.cs,
    ///     would let a steady wind keep it frozen).
    ///   Fan the Flames (burning): its burn spreads at FanShare strength to the FanTargets nearest enemies within FanReach
    ///     downwind of it (the half circle the wind blows toward), credited to whoever lit it.
    public partial class Game
    {
        public const float WindEvery = 1f, WindChillImmune = 6f, FanShare = 0.6f, FanReach = 120f;
        public const int FanTargets = 3;
        static readonly Rgba FanColor = new Rgba(1f, 0.55f, 0.2f);

        /// Host: wind blowing `dir` (any length; zero for no direction) hits e.
        internal void WindHit(Enemy e, V2 dir)
        {
            if (e == null || e.Stand != null || e.Hp <= 0f || e.WindCd > 0f || e.SpawnT > 0f)
                return;
            bool wet = e.WetT > 0f, burning = e.BurnT > 0f || e.PoolT > 0f;
            if (!wet && !burning)
                return;
            e.WindCd = WindEvery;
            if (wet && !IceFrozen(e) && e.WindChillImmuneT <= 0f)
                WindChill(e);
            if (burning)
                FanFlames(e, dir);
        }

        void TickWind(Enemy e, float dt)
        {
            e.WindCd = Math.Max(0f, e.WindCd - dt);
            e.WindChillImmuneT = Math.Max(0f, e.WindChillImmuneT - dt);
        }

        void WindChill(Enemy e)
        {
            Slow(e, FlChillHold, 1f - FlChillSlow);
            e.FrostChillT = FlChillHold;
            if (e.Def.IsBoss)
                return;
            e.FrostChill = (byte)Math.Min(FlFreezeAt, e.FrostChill + 1);
            if (e.FrostChill < FlFreezeAt)
                return;
            e.FrostChill = 0;
            e.FrozenT = Math.Max(e.FrozenT, e.Def.Heavy ? FlHeavyFreeze : FlFreezeTime);
            e.WindChillImmuneT = WindChillImmune;
            BroadcastFx(FxKind.FrostBurst, e.Pos, FlColor, 0f, 2.4f, Sfx.FrostFreeze);
        }

        void FanFlames(Enemy e, V2 dir)
        {
            float dps = (e.BurnT > 0f ? e.BurnDps : 0f) + (e.PoolT > 0f ? e.BurnPool / e.PoolT : 0f);
            float time = Math.Max(e.BurnT, e.PoolT);
            if (dps <= 0f || time <= 0f)
                return;
            V2 d = dir.IsZero ? dir : dir.Normalized();
            var near = new List<Enemy>();
            foreach (var o in AllFoes(by: e.BurnBy))
            {
                if (o == e || o.Stand != null || o.Hp <= 0f || o.SpawnT > 0f || o.Pos.FartherThan(e.Pos, FanReach + o.Radius))
                    continue;
                if (!d.IsZero && Dot(o.Pos - e.Pos, d) < 0f)
                    continue;  // upwind: the fire blows the other way
                near.Add(o);
            }
            near.Sort((a, b) => a.Pos.DistanceSq(e.Pos).CompareTo(b.Pos.DistanceSq(e.Pos)));
            for (int i = 0; i < near.Count && i < FanTargets; i++)
            {
                var o = near[i];
                o.BurnT = Math.Max(o.BurnT, time);
                o.BurnDps = Math.Max(o.BurnDps, dps * FanShare);
                o.BurnBy = e.BurnBy;
                BroadcastFx(FxKind.Spark, o.Pos, FanColor, 10f, 0.3f, i == 0 ? Sfx.ShotFireballBurn : Sfx.None);
            }
        }
    }
}
