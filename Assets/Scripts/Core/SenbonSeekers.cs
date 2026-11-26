using System;

namespace Arena
{
    /// Senbon Seekers, the DPS Ninja's 4th choice (Docs/Mockups/senbon-seekers.html, take C "Koi"). Each cast adds SbPer poisoned
    /// needles that school round you in a loose, breathing ring (SbSlot), up to SbMax (SbMaxMastered mastered); the key does
    /// nothing at the cap (Game.cs keeps the cooldown). They have no timer. When an enemy comes within SbSeek of you, the
    /// newest needle leaves the ring along its orbit and curves in on it (a Defs.Senbon bullet the host steers: SteerSenbon),
    /// one launch every SbGap, at most SbPerFoe on one enemy. A needle is used up when it hits: SbHit and a stack of poison
    /// (SbPoisonDps for SbPoisonTime, renewed by each hit, stacking to SbStacks). One whose enemy dies picks another in range,
    /// or flies back into the ring. Going down or re-arming loses them. Rare card Hidden Needles (Stats.SbDmgMult, SbStackBonus).
    /// Replicated: Player.SbN, SbClock, SbNewT, SbCap; the needles in flight are ordinary bullets. Drawn by Game/SenbonSeekersLook.cs.
    public partial class Game
    {
        public const int SbPer = 3, SbMax = 9, SbMaxMastered = 12, SbPerFoe = 2;
        public const float SbCooldown = 6f, SbSeek = 200f, SbSpeed = 820f, SbBackSpeed = 650f, SbGap = 0.12f, SbLife = 1.5f;
        public const float SbHit = 20f, SbPoisonDps = 5f, SbPoisonTime = 4f, SbStacks = 5f, SbNewTime = 0.35f;
        public const float SbOrbitR = 44f, SbWobble = 8f, SbSpin = 1.25f, SbCatchR = 16f;
        static readonly Rgba SbSteel = new Rgba(0.86f, 0.89f, 0.93f);

        /// The i-th of n needles' angle and distance from you at this clock (the host, and every screen from the replicated clock).
        public static void SbSlot(int i, int n, float clock, out float ang, out float rad)
        {
            ang = clock * SbSpin + i * MathX.Tau / Math.Max(1, n) + 0.25f * (float)Math.Sin(clock * 1.3f + i);
            rad = SbOrbitR + SbWobble * (float)Math.Sin(clock * 2.1f + i * 1.7f);
        }

        public static V2 SbNeedle(Player p, int i)
        {
            float a, r;
            SbSlot(i, p.SbN, p.SbClock, out a, out r);
            return p.Pos + V2.FromAngle(a) * r;
        }

        /// How many needles this Ninja can hold (12 mastered).
        public static int SbMaxOf(Player p) => p.SbCap ? SbMaxMastered : SbMax;

        public static bool SbFull(Player p) => p.SbN >= SbMaxOf(p);

        /// The cast: three more needles, up to the cap.
        void CastSenbon(Player p, float pw, bool cap)
        {
            p.SbCap = cap;
            if (SbFull(p))
                return;
            p.SbPw = pw;
            if (p.SbN <= 0)
                p.SbGapT = SbNewTime;  // (a fresh ring settles before its first launch)
            p.SbN = Math.Min(SbMaxOf(p), p.SbN + SbPer);
            p.SbNewT = SbNewTime;
            BroadcastFx(FxKind.Ring, p.Pos, SbSteel, SbOrbitR + 14f, 0.35f, Sfx.NinjaFanOpen);
        }

        /// Host, every tick: the ring turns, and a needle leaves it for an enemy in range.
        void TickSenbon(Player p, float dt)
        {
            if (p.SbN <= 0)
                return;
            if (p.Downed || !HasSlot(p, AbilityId.SenbonSeekers))
            {
                p.SbN = 0;
                p.SbNewT = 0f;
                return;
            }
            p.SbClock += dt;
            p.SbNewT = Math.Max(0f, p.SbNewT - dt);
            if ((p.SbGapT -= dt) > 0f)
                return;
            Enemy best = null;
            float bestScore = float.MaxValue;
            foreach (var e in Enemies.Values)
            {
                if (!Targetable(e) || e.Pos.FartherThan(p.Pos, SbSeek))
                    continue;
                int coming = SbComing(p.Id, e.Id);
                if (coming >= SbPerFoe)
                    continue;
                float score = e.Pos.DistanceTo(p.Pos) + coming * 40f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = e;
                }
            }
            if (best == null)
                return;
            int i = p.SbN - 1;  // the newest needle goes
            float a, r;
            SbSlot(i, p.SbN, p.SbClock, out a, out r);
            V2 from = p.Pos + V2.FromAngle(a) * r;
            var b = SpawnBullet(Defs.Senbon, from, V2.FromAngle(a + MathX.Tau / 4f) * SbSpeed, 0f, SbLife, SbHit * p.SbPw * p.Stats.DmgMult * p.Stats.SbDmgMult, p.Id);
            b.Senbon = true;
            b.HomeOn = best.Id;
            b.Pierce = 0;
            b.Pulled = true;  // the host sends its course
            p.SbN--;
            p.SbGapT = SbGap;
        }

        int SbComing(int owner, int enemy)
        {
            int n = 0;
            foreach (var b in Bullets.Values)
                if (b.Senbon && !b.SbHome && b.Owner == owner && b.HomeOn == enemy)
                    n++;
            return n;
        }

        /// Host, from MoveBullets: a needle curves in on its enemy, turning harder the longer it flies; with its enemy gone it takes
        /// another in range, or flies back into the ring (and is held again once it reaches you).
        void SteerSenbon(Bullet b, float dt)
        {
            Player p;
            if (!Players.TryGetValue(b.Owner, out p) || p.Downed)
                return;  // (it flies on and runs out)
            b.Life = SbLife;
            b.SbAge += dt;
            Enemy e = null;
            if (!b.SbHome && (!TryFoe(b.HomeOn, out e) || !Targetable(e)))
            {
                e = null;
                float bestD = SbSeek;
                foreach (var o in Enemies.Values)
                {
                    float d = o.Pos.DistanceTo(p.Pos);
                    if (Targetable(o) && d < bestD)
                    {
                        bestD = d;
                        e = o;
                    }
                }
                b.HomeOn = e != null ? e.Id : 0;
                if (e == null)
                {
                    b.SbHome = true;
                    b.SbAge = 0f;
                }
            }
            V2 to = b.SbHome ? p.Pos : e.Pos;
            if (b.SbHome && b.Pos.DistanceTo(p.Pos) < SbCatchR)
            {
                if (HasSlot(p, AbilityId.SenbonSeekers) && !SbFull(p))
                    p.SbN++;  // back in the ring
                b.Senbon = false;
                b.Life = -1f;  // (MoveBullets takes it off)
                return;
            }
            float want = (to - b.Pos).Angle, have = b.Vel.Angle;
            float turn = (12f + 60f * b.SbAge) * dt;
            float diff = Math.Max(-turn, Math.Min(turn, MathX.WrapPi(want - have)));
            b.Vel = V2.FromAngle(have + diff) * (b.SbHome ? SbBackSpeed : SbSpeed);
        }

        /// A needle in an enemy: another stack of poison (it is used up: Pierce 0).
        void SenbonHit(Bullet b, Enemy e)
        {
            if (!b.Senbon)
                return;
            b.Senbon = false;
            Player p;
            float stacks = SbStacks + (Players.TryGetValue(b.Owner, out p) ? p.Stats.SbStackBonus : 0f);
            float each = SbPoisonDps * b.Dmg / SbHit;
            float cap = Math.Max(e.PoisonDps, each * stacks);
            e.PoisonDps = Math.Min(e.PoisonDps + each, cap);
            e.PoisonT = Math.Max(e.PoisonT, SbPoisonTime);
            e.PoisonBy = b.Owner;
            BroadcastFx(FxKind.Ring, e.Pos, new Rgba(0.56f, 0.82f, 0.31f), 12f + e.Radius * 0.5f, 0.25f, Sfx.None);
        }

        static bool HasSlot(Player p, AbilityId id)
        {
            foreach (var s in p.Slots)
                if (s.Id == id)
                    return true;
            return false;
        }

        // --- Network: the player's Senbon Seekers fields (SyncPlayer) ---

        void SyncSenbon(NetSync s, Player p)
        {
            s.Byte(ref p.SbN);  // Senbon Seekers
            if (p.SbN > 0)
            {
                s.Float(ref p.SbClock);
                s.Float(ref p.SbNewT);
                s.Bool(ref p.SbCap);
            }
        }
    }
}
