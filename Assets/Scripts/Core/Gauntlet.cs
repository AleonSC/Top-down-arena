using System;
using System.Collections.Generic;

namespace Arena
{
    /// The DPS Knight's Gauntlet (`AbilityId.Lunge` kept; was Lunge), the Heraldic look of
    /// Docs/Mockups/knight-dps-abilities.html. Throw down the gauntlet at the enemy nearest your cursor in a lane along
    /// your aim (up to GauntletRange; it flies over anything in between): it hits it for GauntletHitDamage and Challenges it for ChallengeTime (its crest
    /// burns over it). After a brace of GauntletBrace you shoulder-charge to it at GauntletChargeSpeed, untouchable,
    /// bowling every enemy in the way aside (GauntletBowlDamage, shoved GauntletBowlShove px), and hit it on arrival for
    /// GauntletImpactDamage. Kill the Challenged enemy (anyone's kill counts) before the Challenge runs out and the
    /// gauntlet flies back to your hand and the cooldown resets; a miss or a Challenge that runs out costs the full cooldown.
    /// The charge is carried out by the player's own game (Msg.Carry, like the Riot Shield's Msg.Knock).
    ///   FxKind.GauntletThrow: the throw (Pos = the hand, Pos2 = the enemy hit or the far end of a miss,
    ///     Radius = thrower id * 10 + 1 when it hit, Dur = the flight + its tail).
    ///   FxKind.GauntletCharge: the charge (Pos = from, Pos2 = to, Radius = the knight's id, Dur = the charge + its dust).
    ///   FxKind.GauntletBowl: an enemy bowled aside (Pos = the enemy, Pos2 = the shove's direction).
    ///   FxKind.GauntletImpact: the charge landing (Pos = where, Pos2 = the charge's direction).
    ///   FxKind.GauntletBack: the gauntlet flying back (Pos = the Challenged enemy, Pos2 = where the gauntlet lay,
    ///     Radius = the knight's id, Color.A = 1 when the kill reset the cooldown, 0.5 when the Challenge ran out).
    /// Drawn in Game/GauntletLook.cs.
    public partial class Game
    {
        public const float GauntletRange = 380f, GauntletSpeed = 850f, GauntletBrace = 0.2f, GauntletChargeSpeed = 1000f;
        public const float GauntletHitDamage = 12f, GauntletBowlDamage = 18f, GauntletImpactDamage = 30f, GauntletBowlShove = 55f;
        public const float ChallengeTime = 5f, ChallengeTimeMastered = 8f;
        public const float GauntletLane = 24f;        // how far off the aim line (plus its radius) an enemy can be and still be the target
        public const float GauntletStopShort = 32f;   // the charge stops this far before the enemy's centre
        public const float GauntletBackTime = 0.28f;   // the flight back to your hand
        public const float GauntletReturnFx = GauntletBackTime + 0.7f;
        public static readonly V2 GauntletHand = new V2(6f, -11f);  // the off hand, in the knight's own frame (x along the aim)

        class GauntletRun
        {
            public int Owner, Target;
            public int Phase;        // 0 flying, 1 bracing, 2 charging
            public float T, Elapsed, Dur;
            public V2 From, To, Dir;
            public float Power;
            public bool Mastered;
            public readonly List<int> Bowled = new List<int>();
        }

        readonly List<GauntletRun> gauntlets = new List<GauntletRun>();

        public static V2 GauntletHandAt(Player p) => p.Pos + GauntletHand.Rotated(p.Aim);

        /// Host: the gauntlet is thrown toward `target`.
        void CastGauntlet(Player p, V2 target, float power, bool mastered)
        {
            V2 dir = (target - p.Pos).IsZero ? V2.FromAngle(p.Aim) : (target - p.Pos).Normalized();
            float range = GauntletRange * p.Stats.GauntletRange;
            V2 aimPt = p.Pos + (target - p.Pos).LimitLength(range);
            Enemy hit = null;
            float best = float.MaxValue;
            foreach (var e in Enemies.Values)
            {
                if (e.SpawnT > 0f || e.Hp <= 0f)
                    continue;
                V2 off = e.Pos - p.Pos;
                float along = off.Dot(dir), side = Math.Abs(off.X * dir.Y - off.Y * dir.X);
                if (along < 0f || along > range + e.Radius || side > e.Radius + GauntletLane)
                    continue;
                float d = e.Pos.DistanceTo(aimPt);  // the one nearest the cursor: thrown past a pack at the gunner behind it
                if (d < best)
                {
                    best = d;
                    hit = e;
                }
            }
            V2 hand = GauntletHandAt(p);
            if (hit == null)
            {
                V2 end = ClampToArena(p.Pos + dir * range, 10f);
                float fly = hand.DistanceTo(end) / GauntletSpeed;
                BroadcastFx(FxKind.GauntletThrow, hand, end, new Rgba(0.84f, 0.67f, 0.29f), p.Id * 10f, fly + GauntletBackTime + 0.05f, Sfx.SwingHeavy);
                return;
            }
            float flight = Math.Max(0.05f, (hand.DistanceTo(hit.Pos) - 14f) / GauntletSpeed);
            BroadcastFx(FxKind.GauntletThrow, hand, hit.Pos, new Rgba(0.84f, 0.67f, 0.29f), p.Id * 10f + 1f, flight + GauntletBrace + 0.05f, Sfx.SwingHeavy);
            gauntlets.Add(new GauntletRun { Owner = p.Id, Target = hit.Id, T = flight, Dir = dir, Power = power, Mastered = mastered });
        }

        /// Host, every tick: throws landing, braces, charges bowling enemies aside, Challenges running out.
        void TickGauntlets(float dt)
        {
            for (int i = gauntlets.Count - 1; i >= 0; i--)
            {
                var g = gauntlets[i];
                Player p;
                Enemy e;
                if (!Players.TryGetValue(g.Owner, out p) || p.Downed)
                {
                    gauntlets.RemoveAt(i);
                    continue;
                }
                bool alive = Enemies.TryGetValue(g.Target, out e) && e.Hp > 0f;
                if (g.Phase == 0)
                {
                    g.T -= dt;
                    if (g.T > 0f)
                        continue;
                    if (!alive)
                    {
                        gauntlets.RemoveAt(i);
                        continue;
                    }
                    HurtEnemy(e, GauntletHitDamage * p.Stats.DmgMult * g.Power, p.Id);
                    e.ChallengeT = g.Mastered ? ChallengeTimeMastered : ChallengeTime;
                    e.ChallengeBy = p.Id;
                    p.GauntletDown = true;
                    p.GauntletRest = e.Pos - g.Dir * 16f + new V2(-g.Dir.Y, g.Dir.X) * 9f;
                    Sounds.Add(Sfx.Clang);
                    g.Phase = 1;
                    g.T = GauntletBrace;
                }
                else if (g.Phase == 1)
                {
                    g.T -= dt;
                    if (g.T > 0f)
                        continue;
                    if (!alive)
                    {
                        gauntlets.RemoveAt(i);
                        continue;
                    }
                    g.From = p.Pos;
                    V2 off = e.Pos - p.Pos;
                    g.Dir = off.IsZero ? V2.FromAngle(p.Aim) : off.Normalized();
                    g.To = off.Length > GauntletStopShort + 4f ? ClampToArena(e.Pos - g.Dir * GauntletStopShort, p.Def.BodyR) : p.Pos;
                    g.Dur = Math.Max(0.02f, g.From.DistanceTo(g.To) / GauntletChargeSpeed);
                    g.Elapsed = 0f;
                    g.Phase = 2;
                    p.InvulnT = Math.Max(p.InvulnT, g.Dur + 0.12f);
                    CarryPlayer(p, g.To, g.Dur);
                    BroadcastFx(FxKind.GauntletCharge, g.From, g.To, new Rgba(0.84f, 0.67f, 0.29f), p.Id, g.Dur + 0.8f, Sfx.Blink);
                }
                else
                {
                    g.Elapsed += dt;
                    float u = Math.Min(1f, g.Elapsed / g.Dur);
                    V2 at = g.From + (g.To - g.From) * u;
                    V2 path = at - g.From;
                    float len = path.Length;
                    foreach (var o in Enemies.Values)
                    {
                        if (o.Id == g.Target || o.SpawnT > 0f || o.Hp <= 0f || g.Bowled.Contains(o.Id))
                            continue;
                        V2 off = o.Pos - g.From;
                        float along = Math.Max(0f, Math.Min(len, off.Dot(g.Dir)));
                        if (o.Pos.DistanceTo(g.From + g.Dir * along) > o.Radius + 16f)
                            continue;
                        g.Bowled.Add(o.Id);
                        HurtEnemy(o, GauntletBowlDamage * p.Stats.DmgMult * g.Power, p.Id);
                        float sideSign = off.X * g.Dir.Y - off.Y * g.Dir.X > 0f ? -1f : 1f;  // the side of the lane it stands on
                        V2 shove = new V2(-g.Dir.Y, g.Dir.X) * sideSign;
                        if (!o.Def.IsBoss && !o.Def.Heavy)
                            KnockEnemy(o, shove * GauntletBowlShove);
                        BroadcastFx(FxKind.GauntletBowl, o.Pos, shove, new Rgba(0.59f, 0.52f, 0.42f), o.Radius, 0.7f, Sfx.Hit);
                    }
                    if (u < 1f)
                        continue;
                    gauntlets.RemoveAt(i);
                    if (alive)
                    {
                        HurtEnemy(e, GauntletImpactDamage * p.Stats.DmgMult * g.Power * p.Stats.GauntletDamage, p.Id);
                        if (g.Mastered && !e.Def.IsBoss)
                        {
                            e.FrozenT = Math.Max(e.FrozenT, 1f);  // stunned, as a Lance hook does
                            e.SlowT = Math.Max(e.SlowT, 1f);
                        }
                    }
                    BroadcastFx(FxKind.GauntletImpact, g.To + g.Dir * 16f, g.Dir, new Rgba(1f, 0.84f, 0.55f), p.Id, 0.6f, Sfx.Clang);
                }
            }
            foreach (var e in Enemies.Values)
            {
                if (e.ChallengeT <= 0f)
                    continue;
                e.ChallengeT -= dt;
                if (e.ChallengeT > 0f)
                    continue;
                e.ChallengeT = 0f;  // it ran out: the gauntlet comes back, but the cooldown stands
                Player p;
                if (Players.TryGetValue(e.ChallengeBy, out p) && p.GauntletDown)
                {
                    p.GauntletDown = false;
                    BroadcastFx(FxKind.GauntletBack, e.Pos, p.GauntletRest, new Rgba(0.84f, 0.67f, 0.29f, 0.5f), p.Id, GauntletReturnFx, Sfx.None);
                }
            }
        }

        /// Host, an enemy died (KnightKit.OnKill): a Challenged one sends the gauntlet back and resets the cooldown.
        void GauntletKill(Enemy e)
        {
            if (e.ChallengeT <= 0f)
                return;
            e.ChallengeT = 0f;
            Player p;
            if (!Players.TryGetValue(e.ChallengeBy, out p))
                return;
            p.GauntletDown = false;
            CutCooldown(p, AbilityId.Lunge, 1f);  // ReaperLoot.cs: that player's own game takes it off too
            BroadcastFx(FxKind.GauntletBack, e.Pos, p.GauntletRest, new Rgba(0.84f, 0.67f, 0.29f, 1f), p.Id, GauntletReturnFx, Sfx.Clang);
        }

        // --- Carried by an ability (the Gauntlet's charge, Misericorde's leaps) ---------------------------------------

        /// Host: carry a player to `to` over `time` seconds. Players move on their own game, so it's sent to theirs (or
        /// done here for the host's own), like the Riot Shield's knockback.
        void CarryPlayer(Player p, V2 to, float time)
        {
            if (p.Id == MyId)
                StartCarry(to, time);
            var w = Proto.Begin(Msg.Carry);
            w.Write(p.Id);
            w.Write(to);
            w.Write(time);
            session.Broadcast(Proto.End(w));
        }

        /// Your own game: a dash to `to` over `time` seconds.
        void StartCarry(V2 to, float time)
        {
            var me = Me;
            if (me == null || me.Downed)
                return;
            V2 off = to - me.Pos;
            float d = off.Length;
            DashDir = d > 1f ? off / d : V2.Zero;
            DashT = Math.Max(0.01f, time);
            dashSpeed = d / DashT;
        }
    }
}
