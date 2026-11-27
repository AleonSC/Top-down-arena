using System;
using System.Collections.Generic;

namespace Arena
{
    /// Tortoise Oracle: the Healer Summoner's third ultimate (key 2 of the lobby's Ultimate row; World Tree stays the default), from
    /// the mockup (Docs/Mockups/tortoise-oracle.html). An old tortoise settles at your cursor (kept within ToRange px) and slows time
    /// in a ToRadius px circle (mastered ToRadiusMastered) for ToTime s:
    ///   - enemies inside act at ToEnemyTime of their speed (bosses ToBossTime): moving, turning, their attacks and wind-ups
    ///     (TickEnemies scales the time it gives them: `OracleEnemyTime`);
    ///   - enemy shots over it fly at ToShotTime of their speed (MoveBullets: `OracleShotSpeed`);
    ///   - allies standing in it run at double tempo: heals on them count ToAllyTempo times (RunStats.cs `Heal`), they fire
    ///     ToAllyTempo times as often (Weapons.cs `FireRate`) and their abilities, not the ultimate, recharge ToAllyTempo times as
    ///     fast (Game.cs `TickSlots`).
    /// It is one `ZoneKind.TortoiseOracle` zone (Pos the tortoise, Radius the circle); every screen works the effects out from the
    /// replicated zone. The circle grows in over ToGrow s. Summoned minions are not sped up. Game/TortoiseOracleLook.cs draws it.
    public partial class Game
    {
        public const float ToTime = 10f, ToCooldown = 90f, ToRange = 500f, ToRadius = 240f, ToRadiusMastered = 300f, ToGrow = 0.6f;
        public const float ToEnemyTime = 0.5f, ToBossTime = 0.75f, ToShotTime = 0.5f, ToAllyTempo = 2f;

        /// Tortoise Oracle's entry: key 2 of the Healer Summoner's Ultimate row. Upgrades.Abilities gathers it.
        internal static readonly AbilityDef[] OracleUltimates =
        {
            new AbilityDef
            {
                Id = AbilityId.TortoiseOracle, Kit = Kit.Summoner, Role = Role.Healer, Choice = AbilityDef.Ult, UltKey = 2,
                Name = "Tortoise Oracle", Cooldown = ToCooldown,
                Desc = "An old tortoise slows time in a big circle at your cursor: enemies and their shots at half speed, allies inside heal, shoot and recharge twice as fast.",
                Capstone = "The circle is 25% wider.",
                Bot = b => b.HaveTarget && b.TargetD < ToRange,
            },
        };

        /// Seconds since the cast.
        public static float ToAge(Zone z) => z.Duration - z.T;

        /// The circle's reach now: it grows in over ToGrow s (easing out).
        public static float ToReach(Zone z)
        {
            float x = Math.Min(1f, ToAge(z) / ToGrow);
            return z.Radius * (1f - (1f - x) * (1f - x) * (1f - x));
        }

        /// Is this point inside this Oracle's circle?
        public static bool InOracle(Zone z, V2 pos) => z.Kind == ZoneKind.TortoiseOracle && z.T > 0f && pos.WithinReach(z.Pos, ToReach(z));

        /// Is this point inside any Oracle's circle?
        public bool InAnyOracle(V2 pos)
        {
            foreach (var z in Zones.Values)
                if (InOracle(z, pos))
                    return true;
            return false;
        }

        /// The tempo an ally runs at (ToAllyTempo inside a circle, 1 outside or downed).
        public float OracleTempo(Player p) => !p.Downed && InAnyOracle(p.Pos) ? ToAllyTempo : 1f;

        /// The share of time an enemy gets (1 outside).
        float OracleEnemyTime(Enemy e) => InAnyOracle(e.Pos) ? (e.Def.IsBoss ? ToBossTime : ToEnemyTime) : 1f;

        /// The speed share of an enemy shot (1 outside, and for allied shots).
        float OracleShotSpeed(Bullet b) => oracleUp && Defs.IsEnemyShot(b.Kind) && InAnyOracle(b.Pos) ? ToShotTime : 1f;

        /// MoveBullets, before the bullet loop: is any Oracle up? (Saves the zone walk on every bullet.)
        void PrepareOracles()
        {
            oracleUp = false;
            foreach (var z in Zones.Values)
                if (z.Kind == ZoneKind.TortoiseOracle && z.T > 0f)
                {
                    oracleUp = true;
                    return;
                }
        }

        bool oracleUp;

        void CastOracle(Player p, V2 target, bool cap)
        {
            V2 off = target - p.Pos;
            if (off.Length > ToRange)
                target = p.Pos + off.Normalized() * ToRange;
            target = ClampToArena(target, 30f);
            foreach (var old in Zones.Values)
                if (old.Kind == ZoneKind.TortoiseOracle && old.Owner == p.Id)
                    old.T = Math.Min(old.T, 0.01f);  // one each: a new cast replaces the old circle
            AddZone(ZoneKind.TortoiseOracle, p, target, cap ? ToRadiusMastered : ToRadius, ToTime);
            BroadcastFx(FxKind.Ring, target, Kits.Get(ZoneKind.TortoiseOracle).Color, cap ? ToRadiusMastered : ToRadius, 0.6f, Sfx.Summon);
        }
    }
}
