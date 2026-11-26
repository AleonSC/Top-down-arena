using System;
using System.Collections.Generic;

namespace Arena
{
    /// Cannon Call, the DPS Knight's 4th choice (the lobby's 4th choice; Docs/Mockups/cannon-call.html, the Iron Round look).
    /// Press it: the knight fires a flare at the cursor (up to CcReach px) and a battery behind the lines drops an iron ball on the
    /// mark CcLandT s after the cast. The slam hits every enemy within CcRadius: CcDamage inside CcCore, CcEdgeDamage out to the
    /// edge; it shoves them outward (CcShoveMax px at the middle down to CcShoveMin at the edge; Heavy enemies and bosses aren't
    /// shoved) and stuns them CcStun s (not bosses), and knocks the ordinary enemy shots in the ring out of the air (red aimed shots fly on).
    /// Kill CcRefundKills or more with one call and CcRefund of the cooldown comes back. Mastered: two smaller balls
    /// (CcSmallScale of the radius and damage) land CcSideGap px behind and beyond the mark along the aim, CcStagger s apart.
    /// The rare card Siege Powder (Stats.CannonDamage, CannonWide) adds 25% damage and 15% radius a stack.
    ///   FxKind.CannonCall: one per ball, sent at the cast and drawn from its age (Game/CannonCallLook.cs): Pos = where the knight
    ///     stood, Pos2 = where the ball lands, Radius = the knight's id * 1000 + the blast radius, Color.A = the landing time / 4 s,
    ///     Dur = the landing + CcCraterT (the crater and smoke). The slam also sends a Boom in CcBoomColor for the shake, scorch,
    ///     ripple and sound; the look draws the impact itself (IsLeapBoom).
    public partial class Game
    {
        public const float CcReach = 420f, CcFlareT = 0.3f, CcFallT = 0.9f, CcLandT = 1.25f, CcCraterT = 3f;
        public const float CcRadius = 130f, CcCore = 70f, CcDamage = 70f, CcEdgeDamage = 35f, CcStun = 0.6f;
        public const float CcShoveMax = 140f, CcShoveMin = 40f, CcRefund = 0.5f;
        public const int CcRefundKills = 3;
        public const float CcSmallScale = 0.7f, CcSideGap = 115f, CcStagger = 0.22f;
        public static readonly Rgba CcBoomColor = new Rgba(0.9f, 0.86f, 0.78f);  // the slam's Boom (the look draws its own impact)

        class CannonBall
        {
            public int Owner, Call;
            public V2 At;
            public float T, Radius, Core, Dmg, Edge;
        }

        class CannonTally
        {
            public int Owner, Kills, Left;
            public bool Refunded;
        }

        readonly List<CannonBall> cannonBalls = new List<CannonBall>();
        readonly Dictionary<int, CannonTally> cannonCalls = new Dictionary<int, CannonTally>();
        readonly List<Enemy> cannonHit = new List<Enemy>();
        readonly List<int> cannonShots = new List<int>();
        int nextCannonCall;

        /// The blast radius of a player's main ball (Siege Powder widens it).
        public static float CcRadiusOf(Player p) => CcRadius * (1f + p.Stats.CannonWide);

        /// Where the mark lands for a cursor at `cursor`: along the aim, no farther than CcReach, inside the arena.
        V2 CannonMark(Player p, V2 cursor)
        {
            V2 off = cursor - p.Pos;
            if (off.Length > CcReach)
                off = off.Normalized() * CcReach;
            return ClampToArena(p.Pos + off, 10f);
        }

        /// Host: Cannon Call was cast with the cursor at `cursor`.
        void CastCannonCall(Player p, V2 cursor, bool mastered, float power)
        {
            V2 mark = CannonMark(p, cursor);
            V2 dir = mark - p.Pos;
            dir = dir.Length < 1f ? V2.FromAngle(p.Aim) : dir.Normalized();
            float mult = p.Stats.DmgMult * power * (1f + p.Stats.CannonDamage), r = CcRadiusOf(p);
            int call = ++nextCannonCall;
            var tally = new CannonTally { Owner = p.Id };
            cannonCalls[call] = tally;
            AddCannonBall(p, call, tally, mark, CcLandT, r, CcCore * (r / CcRadius), mult);
            if (mastered)
            {
                float sr = r * CcSmallScale;
                AddCannonBall(p, call, tally, ClampToArena(mark - dir * CcSideGap, 10f), CcLandT + CcStagger, sr, CcCore * CcSmallScale * (r / CcRadius), mult * CcSmallScale);
                AddCannonBall(p, call, tally, ClampToArena(mark + dir * CcSideGap, 10f), CcLandT + 2f * CcStagger, sr, CcCore * CcSmallScale * (r / CcRadius), mult * CcSmallScale);
            }
            Sounds.Add(Sfx.Ability);
        }

        void AddCannonBall(Player p, int call, CannonTally tally, V2 at, float land, float radius, float core, float mult)
        {
            cannonBalls.Add(new CannonBall { Owner = p.Id, Call = call, At = at, T = land, Radius = radius, Core = core, Dmg = CcDamage * mult, Edge = CcEdgeDamage * mult });
            tally.Left++;
            BroadcastFx(FxKind.CannonCall, p.Pos, at, new Rgba(0.9f, 0.3f, 0.24f, land / 4f), p.Id * 1000f + radius, land + CcCraterT, Sfx.None);
        }

        /// Host, every tick: the balls counting down and slamming in.
        void TickCannons(float dt)
        {
            for (int i = cannonBalls.Count - 1; i >= 0; i--)
            {
                var b = cannonBalls[i];
                b.T -= dt;
                if (b.T > 0f)
                    continue;
                cannonBalls.RemoveAt(i);
                CannonTally tally;
                cannonCalls.TryGetValue(b.Call, out tally);
                Player p;
                if (Players.TryGetValue(b.Owner, out p))
                    CannonSlam(p, b, tally);
                if (tally != null && --tally.Left <= 0)
                    cannonCalls.Remove(b.Call);
            }
        }

        void CannonSlam(Player p, CannonBall b, CannonTally tally)
        {
            cannonHit.Clear();
            foreach (var e in FoesInCircle(b.At, b.Radius, by: p.Id))
                cannonHit.Add(e);
            foreach (var e in cannonHit)
            {
                float d = e.Pos.DistanceTo(b.At);
                HurtEnemy(e, d <= b.Core + e.Radius * 0.5f ? b.Dmg : b.Edge, p.Id);
                if (e.Hp <= 0f)
                {
                    if (tally != null)
                        tally.Kills++;
                    continue;
                }
                if (e.Def.IsBoss)
                    continue;
                Stun(e, CcStun);  // (daze stars, not the ice block: Daze.cs)
                if (!e.Def.Heavy)
                {
                    float u = Math.Min(1f, d / b.Radius);
                    ShoveFrom(e, b.At, CcShoveMax + (CcShoveMin - CcShoveMax) * u);
                }
            }
            // The ordinary enemy shots inside the ring are knocked down; red aimed shots fly on.
            cannonShots.Clear();
            foreach (var s in Bullets.Values)
                if (Defs.IsEnemyShot(s.Kind) && !Defs.Unblockable(s.Kind) && s.Pos.CloserThan(b.At, b.Radius))
                    cannonShots.Add(s.Id);
            foreach (int id in cannonShots)
                KillBullet(id);
            BroadcastFx(FxKind.Boom, b.At, CcBoomColor, b.Radius, 0.4f, Sfx.BigExplode);
            if (tally != null && !tally.Refunded && tally.Kills >= CcRefundKills)
            {
                tally.Refunded = true;
                CutCooldown(p, AbilityId.CannonCall, CcRefund);  // ReaperLoot.cs: that player's own game takes it off too
                BroadcastFx(FxKind.DiveReset, b.At, new V2(p.Id, 0f), new Rgba(1f, 0.84f, 0.55f), 20f, BladedResetFx, Sfx.Clang);  // the gold reset flash (KnightDpsLook.cs)
            }
        }
    }
}
