using System;

namespace Arena
{
    /// The DPS Hemomancer's Vein Rush (`AbilityId.HemoNova` kept; was Hemo Nova). Pay 10% of your max health and rush
    /// toward your cursor as a streak of blood, untouchable: every enemy you pass through is cut (25, more with Blood Price),
    /// bleeds, and gives back 2% of your max health, so cutting through five pays for itself. Behind you lies a trail of
    /// blood (`ZoneKind.VeinTrail`, a row of small pools) that slows enemies 30% for 2 s; mastered, it bursts after 1 s.
    /// Like the Knight's Lunge, the player's own game carries them (Game.cs); the host cuts everything along the path.
    /// Drawn in Game/VeinRushLook.cs (look B "Needle" of Docs/Mockups/vein-rush.html).
    public partial class Game
    {
        public const float VeinRushReach = 280f, VeinRushTime = 0.22f;
        public const float VeinRushFxTime = 0.6f;          // the streak and its afterimages fade out after the rush
        public const float VeinCutFxTime = 1f;             // a cut's slash, and the blood flying back to the rusher
        public const float TrailBurstAtPublic = 1f;        // (for drawing: when a mastered trail bursts)
        const float RushWidth = 22f;                       // how near the path an enemy must be (plus its radius) to be cut
        const float RushDamage = 25f, RushBleed = 6f;      // the cut; bleed a second (3 stacks' worth, for 3 s)
        const float RushRefund = 0.02f;                    // each enemy cut gives back this share of your max health
        const float TrailTime = 2f, TrailRadius = 24f, TrailGap = 34f;
        const float TrailBurstAt = TrailBurstAtPublic, TrailBurst = 20f;   // mastered: the trail bursts this long after the rush, for this much

        /// Where a rush toward `aimAt` lands: up to its reach (Crimson Tide adds 25%), along your aim if the cursor is on you.
        public V2 VeinRushLanding(Player p, V2 aimAt)
        {
            float reach = VeinRushReach * p.Stats.RushReach;
            V2 off = aimAt - p.Pos;
            V2 dir = off.Length > 1f ? off.Normalized() : V2.FromAngle(p.Aim);
            return ClampToArena(p.Pos + dir * reach, p.Def.BodyR);
        }

        /// Host: Vein Rush was cast toward `target` (where the player's own game is carrying them).
        void CastVeinRush(Player p, V2 target, float power, bool mastered)
        {
            V2 from = StepFrom.HasValue && StepFrom.Value.DistanceTo(p.Pos) < 120f ? StepFrom.Value : p.Pos, path = target - from;
            float len = Math.Max(1f, path.Length);
            V2 dir = path / len;
            p.InvulnT = Math.Max(p.InvulnT, VeinRushTime + 0.12f);
            var s = p.Stats;
            float dmg = RushDamage * s.DmgMult * power, refund = p.MaxHp * (RushRefund + s.RushRefund);
            var blood = new Rgba(0.85f, 0.12f, 0.2f);
            foreach (var e in Enemies.Values)
            {
                if (!Targetable(e))
                    continue;
                float along = Math.Max(0f, Math.Min(len, (e.Pos.X - from.X) * dir.X + (e.Pos.Y - from.Y) * dir.Y));
                V2 near = from + dir * along;
                if (e.Pos.DistanceTo(near) > e.Radius + RushWidth)
                    continue;
                HurtEnemy(e, dmg, p.Id);
                ApplyBleed(e, RushBleed * power, p.Id);
                Heal(p, refund, p, "Vein Rush");
                castHits++;
                BroadcastFx(FxKind.VeinCut, e.Pos, dir, blood, p.Id * 1000f + e.Radius, VeinCutFxTime, Sfx.None);  // the slash, and its blood flying back to you
            }
            for (float d = TrailGap * 0.5f; d < len; d += TrailGap)  // the trail: a row of small pools along the path
            {
                var z = AddZone(ZoneKind.VeinTrail, p, from + dir * d, TrailRadius, TrailTime);
                z.Bursts = mastered;
                z.Aim = mastered ? 1f : 0f;  // (replicated, so everyone sees it swell before it bursts)
            }
            BroadcastFx(FxKind.VeinRush, from, target, blood, p.Id, VeinRushTime + VeinRushFxTime, Sfx.Blink);
        }

        /// Host, from TickZones: a mastered trail bursts once, a second after the rush.
        void TickVeinTrail(Zone z)
        {
            if (!z.Bursts || z.Duration - z.T < TrailBurstAt)
                return;
            z.Bursts = false;
            foreach (var e in Enemies.Values)
                if (e.SpawnT <= 0f && e.Pos.DistanceTo(z.Pos) < z.Radius + e.Radius + 6f)
                    HurtEnemy(e, TrailBurst * z.DmgMult, z.Owner);
            BroadcastFx(FxKind.Boom, z.Pos, z.Def.Color, z.Radius + 8f, 0.3f, Sfx.None);
        }
    }
}
