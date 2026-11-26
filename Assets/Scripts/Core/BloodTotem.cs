using System;
using System.Collections.Generic;

namespace Arena
{
    /// Blood Totem, the Tank Hemomancer's 4th choice (`AbilityId.BloodTotem`, `AbilityDef.Fourth`: the lobby's 4th ability key;
    /// mockup claude.ai artifact "Blood Totem"). 6% of your max health plants a totem of bone and clot at your cursor (up to
    /// 420 px away). For 14 s a vein reaches out of its beating heart to each enemy within 180 px (up to 5 a totem, nearest
    /// first) and wraps round it: gripped enemies move at 40% speed, bosses at 70%. You keep up to 3 totems; a 4th bursts the
    /// oldest. Each enemy is held by one totem only (the nearest that reaches it), so totems don't stack. Mastered: the veins
    /// also drain 6 health a second from what they hold. Bloodroot (Rare card): 20% more reach and 3 s longer, a stack.
    ///
    /// Each totem is a `ZoneKind.BloodTotem` zone (replicated like any zone: Pos, Radius = its reach, T and Duration), so no
    /// snapshot field is needed. Who is gripped is worked out the same way on every screen from the zones and the enemies
    /// (`TotemGrips`): the host slows what it finds, the look (Game/BloodTotemLook.cs) draws a vein to each.
    public partial class Game
    {
        public const float BtCost = 0.06f, BtCooldown = 2f;
        public const float BtReach = 180f;          // how far its veins reach
        public const float BtLife = 14f;            // how long a totem stands
        public const float BtThrow = 420f;          // the farthest from you a totem can go
        public const int BtMax = 3;                 // totems you can have up at once
        public const int BtVeins = 5;               // enemies one totem can hold
        public const float BtSlow = 0.6f, BtBossSlow = 0.3f;  // how much a vein slows an enemy, a boss
        public const float BtDrain = 6f, BtDrainTick = 0.5f;  // mastered: health a second the veins drain, dealt every half second
        public const float BtRise = 0.25f;          // the totem rises out of the floor in this long (the look; veins wait for it)

        /// One vein: the totem zone and the enemy it holds.
        public struct TotemGrip
        {
            public Zone Totem;
            public Enemy Enemy;
            public float Dist;
        }

        readonly List<TotemGrip> totemGrips = new List<TotemGrip>();
        readonly List<Zone> totemScratch = new List<Zone>();

        /// A totem's reach for this player (Bloodroot: +20% a stack).
        public static float BtReachOf(Player p) => BtReach * p.Stats.BtReachMult;

        /// Has this totem risen far enough to grip? (Its age from the replicated T and Duration.)
        public static bool TotemUp(Zone z) => z.Duration - z.T >= BtRise;

        /// Who each totem holds, the same on every screen: an enemy goes to the nearest risen totem that reaches it, and each
        /// totem keeps its BtVeins nearest. Fills `into` (cleared first).
        public void TotemGrips(List<TotemGrip> into)
        {
            into.Clear();
            totemScratch.Clear();
            foreach (var z in Zones.Values)
                if (z.Kind == ZoneKind.BloodTotem && z.T > 0f && TotemUp(z))
                    totemScratch.Add(z);
            if (totemScratch.Count == 0)
                return;
            foreach (var e in Enemies.Values)
            {
                if (!Targetable(e))
                    continue;
                Zone best = null;
                float bd = float.MaxValue;
                foreach (var z in totemScratch)
                {
                    float d = e.Pos.DistanceTo(z.Pos);
                    if (d <= z.Radius + e.Radius * 0.5f && d < bd)
                    {
                        bd = d;
                        best = z;
                    }
                }
                if (best != null)
                    into.Add(new TotemGrip { Totem = best, Enemy = e, Dist = bd });
            }
            // Each totem keeps its nearest BtVeins (stable: by distance, then id).
            into.Sort((a, b) => a.Totem.Id != b.Totem.Id ? a.Totem.Id.CompareTo(b.Totem.Id)
                : a.Dist != b.Dist ? a.Dist.CompareTo(b.Dist) : a.Enemy.Id.CompareTo(b.Enemy.Id));
            int run = 0, last = -1, w = 0;
            for (int i = 0; i < into.Count; i++)
            {
                if (into[i].Totem.Id != last)
                {
                    last = into[i].Totem.Id;
                    run = 0;
                }
                if (run++ < BtVeins)
                    into[w++] = into[i];
            }
            into.RemoveRange(w, into.Count - w);
        }

        /// Host: Blood Totem cast (the health is paid by the ability's HealthCost).
        void CastBloodTotem(Player p, V2 target, float power, bool mastered)
        {
            V2 d = target - p.Pos;
            if (d.Length > BtThrow)
                d = d * (BtThrow / d.Length);
            V2 at = ClampToArena(p.Pos + d, 24f);
            Zone oldest = null;
            int mine = 0;
            foreach (var z in Zones.Values)
                if (z.Kind == ZoneKind.BloodTotem && z.Owner == p.Id)
                {
                    mine++;
                    if (oldest == null || z.Id < oldest.Id)
                        oldest = z;
                }
            var blood = Kits.Get(ZoneKind.BloodTotem).Color;
            if (mine >= BtMax && oldest != null)  // a 4th bursts the oldest
            {
                Zones.Remove(oldest.Id);
                BroadcastFx(FxKind.Boom, oldest.Pos, blood, 46f, 0.45f, Sfx.None);
            }
            var t = AddZone(ZoneKind.BloodTotem, p, at, BtReachOf(p), BtLife + p.Stats.BtBonusTime);
            t.Cap = mastered;
            t.TickT = BtDrainTick;
            BroadcastFx(FxKind.Ring, at, blood, 40f, 0.35f, Sfx.SpellArcane);
        }

        /// Host, every tick: the veins slow what they hold (mastered: and drain it).
        void TickBloodTotems(float dt)
        {
            TotemGrips(totemGrips);
            foreach (var z in Zones.Values)
                if (z.Kind == ZoneKind.BloodTotem && z.Cap)
                    z.TickT -= dt;
            foreach (var g in totemGrips)
            {
                var e = g.Enemy;
                Slow(e, 0.3f, 1f - (e.Def.IsBoss ? BtBossSlow : BtSlow));
                if (g.Totem.Cap && g.Totem.TickT <= 0f)
                    HurtEnemy(e, BtDrain * BtDrainTick * g.Totem.DmgMult, g.Totem.Owner);
            }
            foreach (var z in Zones.Values)
                if (z.Kind == ZoneKind.BloodTotem && z.Cap && z.TickT <= 0f)
                    z.TickT += BtDrainTick;
        }
    }
}
