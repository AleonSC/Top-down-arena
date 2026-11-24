using System;

namespace Arena
{
    /// Steam (the fire + water combo; enemies only): an enemy that is wet (Tsunami.cs) and burning (the Flamethrower, Fireball,
    /// Spirit Chalk, Warhorn Volley, Incendiary Rounds...) at once flashes the water to steam. Both are used up, and a steam cloud
    /// (ZoneKind.Steam, SteamRadius, SteamTime s) billows up where it stands, credited to whoever lit it. Enemies inside take
    /// SteamDps a second (+SteamDpsWave a wave) and their weapons are jammed (Enemy.SmokeT, as the Grenade Launcher's smoke: no
    /// attacking; bosses and mini-bosses only take the scald). A new steam within SteamMerge px of a cloud refreshes that cloud
    /// instead of stacking another, and an enemy can't steam again for SteamLockout s. Look: Game/SteamLook.cs.
    /// Meltwater: when a freeze ends (real ice: Glacial Tomb, Frost Lance, Blood Freeze...; not a stun), the enemy is wet for
    /// MeltWetTime s. While it's frozen, fire only burns; if it's still burning when the ice melts, it turns to steam.
    public partial class Game
    {
        public const float MeltWetTime = 6f;
        public const float SteamRadius = 90f, SteamTime = 3f, SteamDps = 8f, SteamDpsWave = 0.4f, SteamMerge = 50f, SteamLockout = 1f;
        public static readonly Rgba SteamColor = new Rgba(0.9f, 0.93f, 0.95f);

        /// Host, every tick per enemy (after wet and burning tick): wet and burning at once turns to steam.
        void TickSteam(Enemy e, float dt)
        {
            e.SteamCd = Math.Max(0f, e.SteamCd - dt);
            bool iced = IceFrozen(e);  // real ice (Daze.cs): a stun alone isn't
            if (e.WasIced && !iced && e.Stand == null)
                e.WetT = Math.Max(e.WetT, MeltWetTime);  // the ice melts: wet (Tsunami.cs)
            e.WasIced = iced;
            if (iced || e.Stand != null || e.SteamCd > 0f || e.WetT <= 0f || (e.BurnT <= 0f && e.PoolT <= 0f) || e.SpawnT > 0f)
                return;  // (frozen, fire just burns: the steam waits for the thaw)
            int by = e.BurnBy;
            e.WetT = 0f;
            e.BurnT = e.BurnDps = 0f;
            e.PoolT = e.BurnPool = 0f;
            e.SteamCd = SteamLockout;
            foreach (var z in Zones.Values)
                if (z.Kind == ZoneKind.Steam && z.Pos.CloserThan(e.Pos, SteamMerge))
                {
                    z.T = z.Duration;  // the cloud there billows up again
                    z.Owner = by;
                    return;
                }
            var cloud = new Zone { Id = nextZoneId++, Kind = ZoneKind.Steam, Owner = by, Pos = e.Pos, Radius = SteamRadius, Duration = SteamTime, T = SteamTime };
            Zones[cloud.Id] = cloud;
            BroadcastFx(FxKind.Ring, e.Pos, SteamColor, SteamRadius * 0.7f, 0.35f, Sfx.TankGasHiss);
        }

        /// Host: a steam cloud scalds and jams the enemies in it.
        void TickSteamCloud(Zone z, float dt)
        {
            float dps = SteamDps + SteamDpsWave * Wave;
            foreach (var e in FoesInCircle(z.Pos, z.Radius, by: z.Owner))
            {
                if (e.SpawnT > 0f)
                    continue;
                DotDamage(e, dps * dt, z.Owner);
                if (!e.Def.IsBoss && !e.Def.Mini)
                    e.SmokeT = 0.2f;  // jammed: no attacks while it lasts (EnemyAI.cs)
            }
        }
    }
}
