using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Healer Knight's Guardian's Oath (Docs/Mockups/guardians-oath.html): the Knight charges to the player nearest
    /// the cursor (downed or not), drags them clear of the fire and plants a Ward of Light (`ZoneKind.OathWard`) that cuts the
    /// damage everyone inside takes and revives downed allies faster. The charge is the Knight's own game moving them (like
    /// the Leap Slam, `LocalOathCharge`); the pull is a knockback sent to each player's own game (a downed ally is carried
    /// by the host, because a downed player can't be knocked); the ward is planted by the host when the pull ends.
    public partial class Game
    {
        public const float OathRange = 600f, OathRangeMastered = 800f, OathSpeed = 1400f;
        public const float OathPullDist = 320f, OathReach = 130f;  // how far the pull goes; how close to the ally the Knight must stop for the grab to take
        public const float OathWardRadius = 130f, OathWardRadiusMastered = 190f, OathWardTime = 5f;
        public const float OathCutBase = 0.5f, OathCutMax = 0.8f;  // damage taken inside the ward: -50%, up to -80% with Bulwark

        class OathRun
        {
            public int Owner, Ally = -1;
            public float T;        // seconds until the next beat (the grab, then the plant)
            public bool Pulling;
            public V2 Grab, Dest;
            public bool Mastered;
        }

        readonly List<OathRun> oaths = new List<OathRun>();

        /// Whoever the cursor is nearest among the other players, if they are within reach of `me`.
        Player OathTarget(Player me, V2 aimAt, float range)
        {
            Player best = null;
            foreach (var q in Players.Values)
                if (q.Id != me.Id && q.Pos.DistanceTo(me.Pos) <= range + q.Def.BodyR && (best == null || q.Pos.DistanceSq(aimAt) < best.Pos.DistanceSq(aimAt)))
                    best = q;
            return best;
        }

        /// Your own game, as you press it: pick where the charge ends (just short of the player nearest the cursor, or
        /// toward the cursor when nobody else is in reach) and start dashing there. Returns the end point.
        V2 LocalOathCharge(Player me, V2 aimAt)
        {
            float range = Mastered(me, AbilityId.GuardiansOath) ? OathRangeMastered : OathRange;
            var ally = OathTarget(me, aimAt, range);
            V2 target;
            if (ally != null)
            {
                V2 from = (me.Pos - ally.Pos).IsZero ? V2.FromAngle(me.Aim + (float)Math.PI) : (me.Pos - ally.Pos).Normalized();
                target = ClampToArena(ally.Pos + from * (ally.Def.BodyR + me.Def.BodyR + 4f), me.Def.BodyR);
            }
            else
                target = ClampToArena(me.Pos + (aimAt - me.Pos).LimitLength(range), me.Def.BodyR);
            float dist = target.DistanceTo(me.Pos);
            DashDir = dist > 1f ? (target - me.Pos) / dist : V2.Zero;
            DashT = dist / OathSpeed;
            dashSpeed = OathSpeed;
            return target;
        }

        /// Host: the cast. `target` is where the Knight's charge ends.
        bool OathCast(Player p, V2 target, bool mastered)
        {
            var run = new OathRun { Owner = p.Id, Grab = target, Mastered = mastered };
            float travel = Math.Min(p.Pos.DistanceTo(target), OathRangeMastered + 60f) / OathSpeed;
            run.T = travel + 0.05f;
            Player ally = null;
            foreach (var q in Players.Values)
                if (q.Id != p.Id && q.Pos.DistanceTo(target) <= OathReach && (ally == null || q.Pos.DistanceSq(target) < ally.Pos.DistanceSq(target)))
                    ally = q;
            if (ally != null)
            {
                run.Ally = ally.Id;
                castHits++;
            }
            p.InvulnT = Math.Max(p.InvulnT, run.T + OathPullDist / KnockSpeed + 0.2f);  // nothing touches the Knight on the way in or out
            oaths.Add(run);
            BroadcastFx(FxKind.Link, p.Pos, target, new Rgba(0.6f, 0.78f, 1f), 0f, run.T + 0.1f, Sfx.SpellHoly);
            BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.6f, 0.78f, 1f), 40f, 0.35f, Sfx.None);
            return true;
        }

        /// Away from the enemies around the grab point (toward the middle of the arena if none are near).
        V2 OathPullDir(V2 grab)
        {
            V2 sum = V2.Zero;
            foreach (var e in Enemies.Values)
            {
                if (e.SpawnT > 0f)
                    continue;
                V2 d = grab - e.Pos;
                float len = d.Length;
                if (len < 1f || len > 500f)
                    continue;
                sum += d / len * (1f - len / 500f);
            }
            if (sum.Length < 0.05f)
                sum = new V2(Width / 2f, Height / 2f) - grab;
            return sum.IsZero ? V2.FromAngle(0f) : sum.Normalized();
        }

        /// Host, every tick (from TickKnights): grab the ally, carry them, plant the ward.
        void TickOath(float dt)
        {
            for (int i = oaths.Count - 1; i >= 0; i--)
            {
                var run = oaths[i];
                Player p;
                if (!Players.TryGetValue(run.Owner, out p) || p.Downed)
                {
                    oaths.RemoveAt(i);
                    continue;
                }
                Player ally;
                if (run.Ally < 0 || !Players.TryGetValue(run.Ally, out ally))
                    ally = null;
                if (run.Pulling && ally != null && ally.Downed)
                {
                    float step = Math.Min(KnockSpeed * dt, ally.Pos.DistanceTo(run.Dest));  // a downed ally can't be knocked: carry them
                    MovePlayer(ally, ally.Pos + (run.Dest - ally.Pos).Normalized() * step);
                    ally.InvulnT = Math.Max(ally.InvulnT, 0.2f);
                }
                run.T -= dt;
                if (run.T > 0f)
                    continue;
                if (!run.Pulling && ally != null)
                {
                    // The grab: both slide away from the fire.
                    V2 dir = OathPullDir(p.Pos);
                    run.Dest = ClampToArena(p.Pos + dir * OathPullDist, p.Def.BodyR);
                    float dist = p.Pos.DistanceTo(run.Dest);
                    run.T = dist / KnockSpeed + 0.05f;
                    run.Pulling = true;
                    if (dist > 1f)
                    {
                        KnockPlayer(p, dir, dist);
                        if (!ally.Downed)
                            KnockPlayer(ally, dir, dist);
                    }
                    ally.InvulnT = Math.Max(ally.InvulnT, run.T + 0.3f);
                    BroadcastFx(FxKind.Link, p.Pos, ally.Pos, new Rgba(1f, 0.9f, 0.5f), 0f, run.T + 0.1f, Sfx.None);
                    BroadcastFx(FxKind.Ring, ally.Pos, new Rgba(1f, 0.9f, 0.5f), 60f, 0.4f, Sfx.None);
                    continue;
                }
                // The plant (or no ally to pull: plant where the charge ended).
                oaths.RemoveAt(i);
                var z = AddZone(ZoneKind.OathWard, p, p.Pos, run.Mastered ? OathWardRadiusMastered : OathWardRadius, OathWardTime);
                z.DamageCut = Math.Min(OathCutMax, OathCutBase + p.Stats.OathCut);
                z.ExtraRevive = 1f;  // downed allies inside are revived twice as fast
                z.Follow = run.Mastered;
                BroadcastFx(FxKind.Ring, z.Pos, z.Def.Color, z.Radius, 0.5f, Sfx.SpellHoly);
                if (run.Mastered && ally != null && ally.Downed)
                {
                    RevivePlayer(ally, 0.25f, p);
                    BroadcastFx(FxKind.Ring, ally.Pos, new Rgba(1f, 0.9f, 0.5f), 70f, 0.6f, Sfx.Revive);
                }
            }
        }
    }
}
