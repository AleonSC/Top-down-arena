using System;
using System.Collections.Generic;

namespace Arena
{
    /// Poison Dart (DPS Soldier, took Dash's place and number; Docs/Mockups/poison-dart.html, look A): a dart at the enemy
    /// nearest your cursor hits it and poisons it (the venom: Enemy.VenomT / VenomDps). An enemy that dies while the venom
    /// is still on it passes it to the nearest enemies around it that don't carry it yet, a little weaker (full strength
    /// once mastered), with the full time again, and they pass it on in turn when they die (SpreadVenom, from the kill loop
    /// in Combat.cs). Medicine Darts' poison (Ninja.cs) is a separate status and never spreads.
    public partial class Game
    {
        public const float VenomHit = 20f;         // the dart's own hit
        public const float VenomDps = 18f;         // the venom, a second
        public const float VenomTime = 5f;         // how long it lasts (a spread starts it over)
        public const float VenomReach = 180f;      // how far it jumps from a corpse (Virulence adds to it)
        public const float VenomFalloff = 0.8f;    // each jump keeps this much of its strength (mastered: all of it)
        public const int VenomJumps = 3, VenomJumpsMastered = 5;  // how many enemies one death passes it to
        public const float VenomDartFly = 0.14f, VenomGlobFly = 0.32f;  // the looks' flight times (FxKind.VenomDart, VenomSpread)
        public const float VenomSplashLife = 2.6f;  // the first glob's effect also carries the burst and the puddle left where it died

        static readonly Rgba VenomGreen = new Rgba(0.56f, 0.94f, 0.35f);

        /// Host: the cast. Nothing near the cursor: the dart is spent on nothing (like Mark for Death).
        void CastPoisonDart(Player p, V2 target, bool mastered, float power)
        {
            Enemy best = null;
            foreach (var e in Enemies.Values)
                if (Targetable(e) && (best == null || e.Pos.DistanceSq(target) < best.Pos.DistanceSq(target)))
                    best = e;
            if (best == null)
                return;
            castHits++;
            HurtEnemy(best, VenomHit * p.Stats.DmgMult, p.Id);
            Envenom(best, VenomDps * p.Stats.DmgMult * p.Stats.VenomMult * power, p.Id, VenomReach + p.Stats.VenomReachBonus,
                mastered ? VenomJumpsMastered : VenomJumps, mastered ? 1f : VenomFalloff);
            BroadcastFx(FxKind.VenomDart, p.Pos, best.Pos, VenomGreen, best.Id, VenomDartFly + VenomTime, Sfx.Throw);  // (it lives as long as the venom: the dart stays stuck in it)
        }

        /// Puts the venom on an enemy (a stronger one already on it is kept, but the time starts over).
        void Envenom(Enemy e, float dps, int by, float reach, int jumps, float falloff)
        {
            e.VenomDps = Math.Max(e.VenomDps, dps);
            e.VenomT = VenomTime;
            e.VenomBy = by;
            e.VenomReach = reach;
            e.VenomJumps = jumps;
            e.VenomFalloff = falloff;
        }

        /// Host, every tick (EnemyAI.cs, next to Medicine Darts' poison).
        void TickVenom(Enemy e, float dt)
        {
            if (e.VenomT <= 0f)
                return;
            if ((e.VenomT -= dt) <= 0f)
                e.VenomDps = 0f;
            else if (e.SpawnT <= 0f)
                DotDamage(e, e.VenomDps * dt, e.VenomBy);
        }

        readonly List<Enemy> venomNear = new List<Enemy>();

        /// Host: an enemy died (the kill loop in Combat.cs). Still poisoned: it passes to the nearest few around it that
        /// aren't poisoned yet and aren't dying in this same tick.
        void SpreadVenom(Enemy dead)
        {
            if (dead.VenomT <= 0f || dead.VenomDps <= 0f)
                return;
            venomNear.Clear();
            float reach = dead.VenomReach;
            foreach (var e in Enemies.Values)
                if (e != dead && Targetable(e) && e.VenomT <= 0f && e.Pos.DistanceTo(dead.Pos) <= reach + e.Radius)
                    venomNear.Add(e);
            venomNear.Sort((a, b) => a.Pos.DistanceSq(dead.Pos).CompareTo(b.Pos.DistanceSq(dead.Pos)));
            int n = Math.Min(dead.VenomJumps, venomNear.Count);
            float dps = dead.VenomDps * dead.VenomFalloff;
            if (n == 0)  // nobody near: it still bursts (Pos2 = Pos: no glob)
                BroadcastFx(FxKind.VenomSpread, dead.Pos, dead.Pos, VenomGreen, 0f, VenomSplashLife, Sfx.Ability);
            for (int i = 0; i < n; i++)  // Radius = the glob's number: the first one also draws the burst and the puddle
            {
                var e = venomNear[i];
                Envenom(e, dps, dead.VenomBy, reach, dead.VenomJumps, dead.VenomFalloff);
                BroadcastFx(FxKind.VenomSpread, dead.Pos, e.Pos, VenomGreen, i, i == 0 ? VenomSplashLife : VenomGlobFly + 0.45f, i == 0 ? Sfx.Ability : Sfx.None);
            }
            dead.VenomT = 0f;
            dead.VenomDps = 0f;
        }
    }
}
