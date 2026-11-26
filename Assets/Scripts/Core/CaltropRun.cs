using System;

namespace Arena
{
    /// Caltrop Run, the DPS Knight's 3rd choice (`AbilityId.CaltropRun`, took Caught Steel's place and number), look "Iron
    /// Scatter" of the claude.ai artifact "Knight Caltrop Run". For CaltropRunTime s you run CaltropSpeed faster, and every
    /// CaltropStep px you move a caltrop bounces out behind your heels (up to CaltropMax, plus Barbed Iron's; none while you
    /// stand still). Each lands CaltropBounce px back along your path, a little to one side, and is armed once it has landed
    /// (CaltropLand s). The first CaltropUses enemies to step on one each take CaltropDamage, bleed CaltropBleed a second (the
    /// shared bleed, KitWeapons.cs) and are slowed CaltropSlow for CaltropSlowT s (Heavy enemies CaltropSlowHeavy, bosses take
    /// only the damage); then it breaks. Unused, it rusts away after CaltropLife s.
    /// Mastered: CaltropLifeMastered s and CaltropUsesMastered enemies each. The cooldown waits while you run (Game.TickSlots).
    ///
    /// Player.CaltropT is replicated (the speed, which the runner's own game applies, and the look); each caltrop is a
    /// `ZoneKind.Caltrop` zone: Pos where it lands, Aim the way it bounced (it leaves Pos - FromAngle(Aim) * CaltropBounce),
    /// GuardFrac the catches it has left (replicated), Leg those catches (host), Trampled who it has caught (host).
    ///   FxKind.CaltropRun: the run starting (Pos = the knight). FxKind.CaltropHit: a caltrop biting (Pos = where, Pos2 = the
    ///   enemy). FxKind.CaltropBreak: one snapping (Pos = where). Drawn in Game/CaltropRunLook.cs.
    public partial class Game
    {
        public const float CaltropCooldown = 12f, CaltropRunTime = 4f, CaltropSpeed = 1.2f;
        public const float CaltropStep = 34f;          // px moved per caltrop
        public const int CaltropMax = 16;
        public const float CaltropRadius = 14f;        // how close an enemy's edge must come to step on one
        public const float CaltropBounce = 16f;        // how far back along your path it lands
        public const float CaltropScatter = 0.6f;      // radians it may land off the line behind you, either way
        public const float CaltropLand = 0.22f;        // its bounce, then it's armed
        public const float CaltropLife = 6f, CaltropLifeMastered = 9f;
        public const int CaltropUses = 2, CaltropUsesMastered = 3;
        public const float CaltropDamage = 12f, CaltropBleed = 4f;
        public const float CaltropSlow = 0.4f, CaltropSlowHeavy = 0.2f, CaltropSlowT = 1.5f;

        /// Seconds since this caltrop left the knight's heels.
        public static float CaltropAge(Zone z) => z.Duration - z.T;

        /// Where it was dropped (it bounces from here to Pos).
        public static V2 CaltropFrom(Zone z) => z.Pos - V2.FromAngle(z.Aim) * CaltropBounce;

        /// Host: Caltrop Run was cast.
        void CastCaltropRun(Player p, float power, bool mastered)
        {
            p.CaltropT = CaltropRunTime;
            p.CaltropLeft = CaltropMax + p.Stats.CaltropMore;
            p.CaltropAcc = CaltropStep * 0.5f;  // the first one comes half a step in
            p.CaltropLast = p.Pos;
            p.CaltropPower = power;
            p.CaltropMastered = mastered;
            BroadcastFx(FxKind.CaltropRun, p.Pos, p.Pos, Kits.Get(ZoneKind.Caltrop).Color, 30f, 0.4f, Sfx.Clang);
        }

        /// Host, every tick (TickKnights): runs counting down and dropping caltrops along the way each runner went.
        void TickCaltropRuns(float dt)
        {
            foreach (var p in Players.Values)
            {
                if (p.CaltropT <= 0f)
                    continue;
                p.CaltropT = Math.Max(0f, p.CaltropT - dt);
                if (p.Downed)
                {
                    p.CaltropT = 0f;
                    continue;
                }
                V2 step = p.Pos - p.CaltropLast;
                float len = step.Length;
                if (len > 0.01f && len < 200f)  // (a teleport isn't a run)
                {
                    V2 dir = step / len;
                    float done = 0f;
                    while (p.CaltropLeft > 0 && p.CaltropAcc + (len - done) >= CaltropStep)
                    {
                        done += CaltropStep - p.CaltropAcc;
                        p.CaltropAcc = 0f;
                        DropCaltrop(p, p.CaltropLast + dir * done, dir);
                    }
                    p.CaltropAcc += len - done;
                }
                p.CaltropLast = p.Pos;
                if (p.CaltropLeft <= 0)
                    p.CaltropT = Math.Min(p.CaltropT, 0.3f);  // the pouch is empty: the sprint eases off
            }
        }

        void DropCaltrop(Player p, V2 at, V2 dir)
        {
            p.CaltropLeft--;
            float aim = (-dir).Angle + ((float)rng.NextDouble() * 2f - 1f) * CaltropScatter;
            V2 land = ClampToArena(at + V2.FromAngle(aim) * CaltropBounce, 8f);
            int uses = p.CaltropMastered ? CaltropUsesMastered : CaltropUses;
            var z = new Zone { Id = nextZoneId++, Kind = ZoneKind.Caltrop, Owner = p.Id, Pos = land, Radius = CaltropRadius,
                Duration = p.CaltropMastered ? CaltropLifeMastered : CaltropLife, Aim = aim, Leg = uses, GuardFrac = 1f,
                DmgMult = p.Stats.DmgMult * p.CaltropPower, Trampled = new System.Collections.Generic.HashSet<int>() };
            z.T = z.Duration;
            Zones[z.Id] = z;
        }

        /// Host, every tick, for each caltrop (TickZones): the first enemies to step on it are cut, bled and slowed.
        void TickCaltrop(Zone z)
        {
            Player owner;
            if (!Battle || !Players.TryGetValue(z.Owner, out owner))
            {
                z.T = Math.Min(z.T, 0.001f);
                return;
            }
            if (CaltropAge(z) < CaltropLand || z.Leg <= 0)
                return;
            int uses = z.Duration > (CaltropLife + CaltropLifeMastered) * 0.5f ? CaltropUsesMastered : CaltropUses;
            foreach (var e in AllFoes(by: z.Owner))
            {
                if (!Targetable(e) || z.Trampled.Contains(e.Id) || !e.Pos.WithinReach(z.Pos, z.Radius + e.Radius))
                    continue;
                z.Trampled.Add(e.Id);
                z.Leg--;
                z.GuardFrac = z.Leg / (float)uses;
                HurtEnemy(e, (CaltropDamage + owner.Stats.CaltropDamage) * z.DmgMult, z.Owner);
                ApplyBleed(e, CaltropBleed * z.DmgMult, z.Owner);
                if (!e.Def.IsBoss)
                    Slow(e, CaltropSlowT, 1f - (e.Def.Heavy ? CaltropSlowHeavy : CaltropSlow));
                BroadcastFx(FxKind.CaltropHit, z.Pos, e.Pos, new Rgba(1f, 0.42f, 0.4f), e.Radius, 0.35f, Sfx.Hit);
                if (z.Leg <= 0)
                {
                    BroadcastFx(FxKind.CaltropBreak, z.Pos, z.Pos, Kits.Get(ZoneKind.Caltrop).Color, 10f, 0.4f, Sfx.None);
                    z.T = Math.Min(z.T, 0.001f);
                    return;
                }
            }
        }
    }
}
