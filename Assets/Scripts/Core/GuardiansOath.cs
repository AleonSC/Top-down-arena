using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Healer Knight's Guardian's Oath (Docs/Mockups/guardians-oath.html): pressing it starts a pick (`OathPickSlot`):
    /// click the player to grab (downed or not). The Knight charges to them, grabs them, hauls them both back to the spot
    /// the Knight cast from and plants a Ward of Light (`ZoneKind.OathWard`) there that cuts the damage everyone inside
    /// takes and revives downed allies faster. The pick and the charge are the Knight's own game (`LocalOathPick`,
    /// `LocalOathCharge`, like the Leap Slam); the cast spot rides in Msg.Ability's cast-from (`StepFrom`); the haul is a
    /// knockback sent to each player's own game (a downed ally is carried by the host, because a downed player can't be
    /// knocked); the ward is planted by the host when the haul ends.
    public partial class Game
    {
        public const float OathRange = 600f, OathRangeMastered = 800f, OathSpeed = 1400f;
        public const float OathReach = 130f, OathPickR = 60f;  // how close to the ally the Knight must stop for the grab to take; how near the cursor an ally must be to be picked
        public const float OathWardRadius = 130f, OathWardRadiusMastered = 190f, OathWardTime = 5f;
        public const float OathCutBase = 0.5f, OathCutMax = 0.8f;  // damage taken inside the ward: -50%, up to -80% with Bulwark

        class OathRun
        {
            public int Owner, Ally = -1;
            public float T;        // seconds until the next beat (the grab, then the plant)
            public bool Pulling;
            public V2 Grab, Home, Dest;  // where the charge ends, the spot the Knight cast from, where the hauled ally ends
            public bool Mastered;
        }

        readonly List<OathRun> oaths = new List<OathRun>();

        /// Your own game: the slot of a Guardian's Oath waiting for you to click the ally to grab (-1: not picking).
        public int OathPickSlot = -1;
        bool oathFireWas, oathFireHold;  // Fire last tick; Fire held since the click that cast (no shot until it's let go)

        public float OathRangeFor(Player me) => Mastered(me, AbilityId.GuardiansOath) ? OathRangeMastered : OathRange;

        /// While picking: the ally a click would grab (the one under the cursor, within reach of `me`), or null.
        public Player OathPickTarget(Player me, V2 aimAt)
        {
            var q = OathTarget(me, aimAt, OathRangeFor(me));
            return q != null && q.Pos.WithinReach(aimAt, OathPickR + q.Def.BodyR) ? q : null;
        }

        /// Your own game, each tick before the ability keys: while picking, Fire picks instead of shooting. Returns true on
        /// the tick a click lands on an ally (the cast); cancels the pick when the Knight goes down or the slot changes.
        bool LocalOathPick(Player me, V2 aimAt, bool fireDown, ref bool firing)
        {
            bool click = fireDown && !oathFireWas;
            oathFireWas = fireDown;
            if (oathFireHold && !fireDown)
                oathFireHold = false;
            if (OathPickSlot >= 0 && (me.Downed || HoldControls || State == MatchState.GameOver || me.Slots[OathPickSlot].Id != AbilityId.GuardiansOath))
                OathPickSlot = -1;
            if (OathPickSlot < 0)
            {
                if (oathFireHold)
                    firing = false;
                return false;
            }
            firing = false;
            if (!click || OathPickTarget(me, aimAt) == null)
                return false;
            oathFireHold = true;
            return true;
        }

        /// Whoever the cursor is nearest among the other players, if they are within reach of `me`.
        Player OathTarget(Player me, V2 aimAt, float range)
        {
            Player best = null;
            foreach (var q in Players.Values)
                if (q.Id != me.Id && q.Pos.WithinReach(me.Pos, range + q.Def.BodyR) && (best == null || q.Pos.DistanceSq(aimAt) < best.Pos.DistanceSq(aimAt)))
                    best = q;
            return best;
        }

        /// Your own game, as it's cast: pick where the charge ends (just short of the ally, or toward the cursor when
        /// there's none: a bot's cast with nobody in reach) and start dashing there. Returns the end point.
        V2 LocalOathCharge(Player me, V2 aimAt, Player ally)
        {
            float range = OathRangeFor(me);
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

        /// Host: the cast. `target` is where the Knight's charge ends; they're hauled back to where they cast from.
        bool OathCast(Player p, V2 target, bool mastered)
        {
            V2 home = StepFrom.HasValue && StepFrom.Value.CloserThan(target, OathRangeMastered + 100f) ? StepFrom.Value : p.Pos;  // (a far-off cast spot is ignored)
            var run = new OathRun { Owner = p.Id, Grab = target, Home = ClampToArena(home, p.Def.BodyR), Mastered = mastered };
            float travel = Math.Min(p.Pos.DistanceTo(target), OathRangeMastered + 60f) / OathSpeed;
            run.T = travel + 0.05f;
            Player ally = null;
            foreach (var q in Players.Values)
                if (q.Id != p.Id && q.Pos.WithinReach(target, OathReach) && (ally == null || q.Pos.DistanceSq(target) < ally.Pos.DistanceSq(target)))
                    ally = q;
            if (ally != null)
            {
                run.Ally = ally.Id;
                castHits++;
            }
            p.InvulnT = Math.Max(p.InvulnT, run.T + target.DistanceTo(run.Home) / KnockSpeed + 0.2f);  // nothing touches the Knight on the way in or out
            oaths.Add(run);
            BroadcastFx(FxKind.Link, p.Pos, target, new Rgba(0.6f, 0.78f, 1f), 0f, run.T + 0.1f, Sfx.SpellHoly);
            BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.6f, 0.78f, 1f), 40f, 0.35f, Sfx.None);
            return true;
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
                    if (step > 0.01f)
                        MovePlayer(ally, ally.Pos + (run.Dest - ally.Pos).Normalized() * step);
                    ally.InvulnT = Math.Max(ally.InvulnT, 0.2f);
                }
                run.T -= dt;
                if (run.T > 0f)
                    continue;
                if (!run.Pulling && ally != null)
                {
                    // The grab: both are hauled back to where the Knight cast from, the ally just ahead of them.
                    V2 back = run.Home - p.Pos;
                    float dist = back.Length;
                    V2 dir = dist > 1f ? back / dist : V2.Zero;
                    run.Dest = ClampToArena(run.Home - dir * (p.Def.BodyR + ally.Def.BodyR + 4f), ally.Def.BodyR);
                    float allyDist = ally.Pos.DistanceTo(run.Dest);
                    run.T = Math.Max(dist, allyDist) / KnockSpeed + 0.05f;
                    run.Pulling = true;
                    if (dist > 1f)
                        KnockPlayer(p, dir, dist);
                    if (allyDist > 1f && !ally.Downed)
                        KnockPlayer(ally, (run.Dest - ally.Pos) / allyDist, allyDist);
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
