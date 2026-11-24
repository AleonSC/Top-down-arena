using System;
using System.Collections.Generic;

namespace Arena
{
    /// Charged (the electricity status; enemies only): any lightning hit (every WetArcs caller: Arc Lightning, Chain Light,
    /// Storm Hammer pulses, Lightning Rod, the Nimbus) and Static Shock's circuit and Arc Cannon leave what they hit charged for
    /// ChargedTime s (refreshed, never stacked). A charged enemy is a conductor, like a wet one: any lightning hit anywhere arcs
    /// to it for 40% of that hit (Tsunami.cs WetArcs; the arcs don't charge). Wet AND charged, it shorts out: it takes
    /// ChargeDps a second (+ChargeDpsWave a wave) and every ChargeArcEvery s an arc jumps from it to the ChargeArcTargets
    /// nearest enemies within ChargeArcRange for ChargeArcDmg (+ChargeArcDmgWave a wave). Credited to whoever charged it.
    /// Enemy.Charged is replicated for the icon and the arcs on its body (Game/WorldEnemies.cs).
    public partial class Game
    {
        public const float ChargedTime = 4f;
        public const float ChargeDps = 6f, ChargeDpsWave = 0.3f;
        public const float ChargeArcEvery = 2.5f, ChargeArcRange = 220f, ChargeArcDmg = 25f, ChargeArcDmgWave = 1.25f;
        public const int ChargeArcTargets = 3;

        /// Host: a lightning hit charges e (not a PvP player's stand-in: players can't be charged).
        internal void Charge(Enemy e, int owner)
        {
            if (e == null || e.Stand != null || e.Hp <= 0f)
                return;
            if (e.ChargedT <= 0f)
                e.ChargePulseT = ChargeArcEvery;  // (a fresh charge on a wet enemy waits a full beat before its first arc)
            e.ChargedT = Math.Max(e.ChargedT, ChargedTime);
            e.ChargedBy = owner;
        }

        /// Host, every tick per enemy: the charge wears off; wet and charged, it shorts out (constant damage, then arcs).
        void TickCharged(Enemy e, float dt)
        {
            if (e.ChargedT <= 0f)
                return;
            e.ChargedT = Math.Max(0f, e.ChargedT - dt);
            if (e.ChargedT <= 0f || e.WetT <= 0f)
            {
                e.ChargePulseT = ChargeArcEvery;  // the first arc comes a full beat after it's both
                return;
            }
            if (Hungry(e) || e.SpawnT > 0f || SoulBound(e))
                return;
            DotDamage(e, (ChargeDps + ChargeDpsWave * Wave) * dt, e.ChargedBy);
            if (e.Hp <= 0f || (e.ChargePulseT -= dt) > 0f)
                return;
            e.ChargePulseT = ChargeArcEvery;
            ShortArcs(e);
        }

        /// A wet, charged enemy's arc: a bolt from it to each of the nearest few enemies around it.
        void ShortArcs(Enemy from)
        {
            var near = new List<Enemy>();
            float reach = ChargeArcRange * ChargeArcRange;
            foreach (var e in AllFoes(by: from.ChargedBy))
                if (e != from && e.Hp > 0f && e.SpawnT <= 0f && e.Pos.DistanceSq(from.Pos) <= reach)
                    near.Add(e);
            if (near.Count == 0)
                return;
            near.Sort((a, b) => a.Pos.DistanceSq(from.Pos).CompareTo(b.Pos.DistanceSq(from.Pos)));
            var bolt = Defs.BulletColor(Defs.BoltShot);
            float dmg = ChargeArcDmg + ChargeArcDmgWave * Wave;
            for (int i = 0; i < near.Count && i < ChargeArcTargets; i++)
            {
                BroadcastFx(FxKind.Bolt, from.Pos, near[i].Pos, bolt, 0f, 0.2f, i == 0 ? Sfx.SpellChainArc : Sfx.None);
                HurtEnemy(near[i], dmg, from.ChargedBy);
            }
        }
    }
}
