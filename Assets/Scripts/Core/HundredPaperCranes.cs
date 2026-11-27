using System;

namespace Arena
{
    /// Hundred Paper Cranes: the DPS Ninja's third ultimate (key 2 of the lobby's Ultimate row; Death Lotus stays the default),
    /// from its mockup (claude.ai artifact "Hundred Paper Cranes", v2), on the cast's clock:
    ///   - Gather (0 to HpcGather s): HpcCount folded paper cranes (HpcCountMastered mastered, + Stats.HpcMore) fly out of you,
    ///     crane i at HpcBorn(i), and spiral out into a swirling ring 60 to 130 px round you (HpcSlot) that follows you.
    ///   - Release (HpcGather to HpcEnd): one crane every HpcRelease / count s peels off the ring along its orbit and homes in
    ///     (a Defs.PaperCrane bullet the host steers: SteerCrane). Each picks the enemy within HpcSeek of you with the most health
    ///     left after the cranes already flying at it (HpcPick), so the first ones go for the healthiest, then they spread out.
    ///     With no enemy in reach the cranes wait in the ring. One whose enemy dies picks again. A crane is used up when it hits:
    ///     HpcDmg times your damage (Stats.HpcDmgMult).
    ///   - At HpcEnd the cranes still in the ring scatter (no damage). It stops early if you go down, the wave ends or the slot
    ///     changes; cranes already flying fly on.
    /// The Rare card Thousand Folds: HpcFoldsMore more cranes and HpcFoldsDmg harder hits, a stack.
    /// Replicated: Player.HpcT (seconds since the cast; 0 = off) and Player.HpcN (cranes in the ring); the ring follows from them
    /// (HpcSlot), the cranes in flight are ordinary bullets. Moments are FxKind.PaperCrane (Radius = HpcFx*). Drawn by
    /// Game/HundredPaperCranesLook.cs.
    public partial class Game
    {
        public const int HpcCount = 100, HpcCountMastered = 130, HpcFoldsMore = 20;
        public const float HpcGather = 1f, HpcEnd = 4f, HpcRelease = HpcEnd - HpcGather, HpcRise = 0.8f, HpcGrow = 0.45f;
        public const float HpcDmg = 10f, HpcSeek = 700f, HpcSpeed = 540f, HpcLife = 3f, HpcFoldsDmg = 0.15f;
        public const float HpcRingMin = 60f, HpcRingSpan = 70f;
        // FxKind.PaperCrane's Radius: which moment
        public const int HpcFxCast = 0, HpcFxHit = 1, HpcFxScatter = 2;
        public const float HpcFxCastT = 0.5f, HpcFxHitT = 0.55f, HpcFxScatterT = 0.8f;

        public static readonly Rgba HpcPaper = new Rgba(0.95f, 0.93f, 0.86f), HpcSeal = new Rgba(0.78f, 0.2f, 0.17f), HpcInk = new Rgba(0.72f, 0.61f, 0.94f);

        /// Hundred Paper Cranes' entry: key 2 of the DPS Ninja's Ultimate row. Upgrades.Abilities gathers it.
        internal static readonly AbilityDef[] CraneUltimates =
        {
            new AbilityDef
            {
                Id = AbilityId.HundredPaperCranes, Kit = Kit.Ninja, Role = Role.Dps, Choice = AbilityDef.Ult, UltKey = 2,
                Name = "Hundred Paper Cranes", Cooldown = 75f,
                Desc = "100 paper cranes swirl round you, then peel off one by one over 3 s and home in on enemies, the healthiest first: 10 each.",
                Capstone = "130 cranes.",
                Bot = b => b.NearEnemies >= 3,
            },
        };

        /// The cranes are out (gathering or being released).
        public static bool HpcOn(Player p) => p.HpcT > 0f && !p.Downed;

        /// How many cranes a cast folds.
        public static int HpcTotal(Player p, bool mastered) => Math.Min(255, (mastered ? HpcCountMastered : HpcCount) + p.Stats.HpcMore);

        /// When crane i leaves your body, in seconds since the cast (all of them within HpcRise).
        public static float HpcBorn(int i) => HpcRise * (i % HpcCount) / HpcCount;

        static float HpcHash(int i, float k) { float v = (float)Math.Sin(i * k) * 43758.547f; return v - (float)Math.Floor(v); }

        /// Crane i's place in the ring at `t` seconds since the cast, from you, and its heading (along its orbit). Every game
        /// works it out the same from the replicated clock.
        public static V2 HpcSlot(int i, float t, out float heading)
        {
            float ring = HpcRingMin + HpcRingSpan * HpcHash(i, 12.9898f);
            float spin = (2.2f + 1.2f * HpcHash(i, 78.233f)) * (140f / ring);
            float ang = i * 2.39996f + spin * t;  // (the golden angle: neighbours in the count sit far apart in the ring)
            float u = Math.Min(1f, Math.Max(0f, (t - HpcBorn(i)) / HpcGrow)), e = 1f - (1f - u) * (1f - u) * (1f - u);
            float r = ring * e + 6f * (float)Math.Sin(t * 3f + i);
            heading = ang + MathX.Tau / 4f;
            return V2.FromAngle(ang) * r;
        }

        void CastCranes(Player p, bool mastered)
        {
            p.HpcT = 0.0001f;
            p.HpcN = (byte)HpcTotal(p, mastered);
            p.HpcGone = 0;
            p.UltT = HpcEnd;
            BroadcastFx(FxKind.PaperCrane, p.Pos, p.Pos, HpcPaper, HpcFxCast, HpcFxCastT, Sfx.NinjaFanOpen);
        }

        void EndCranes(Player p, bool scatter)
        {
            if (p.HpcT <= 0f)
                return;
            if (scatter && p.HpcN > 0)
                BroadcastFx(FxKind.PaperCrane, p.Pos, p.Pos, HpcPaper, HpcFxScatter, HpcFxScatterT, Sfx.None);
            p.HpcT = 0f;
            p.HpcN = 0;
            if (p.Slots[Upgrades.UltSlot].Id == AbilityId.HundredPaperCranes)
                p.UltT = 0f;
        }

        /// Host, every tick: the clock, and the cranes peeling off the ring on time.
        void TickCranes(float dt)
        {
            foreach (var p in Players.Values)
            {
                if (p.HpcT <= 0f)
                    continue;
                if (p.Downed || !Battle || p.Slots[Upgrades.UltSlot].Id != AbilityId.HundredPaperCranes)
                {
                    EndCranes(p, true);
                    continue;
                }
                p.HpcT += dt;
                int total = p.HpcN + p.HpcGone;
                if (p.HpcT >= HpcGather && total > 0)
                {
                    int due = Math.Min(total, (int)((p.HpcT - HpcGather) / (HpcRelease / total)) + 1);
                    while (p.HpcGone < due && p.HpcN > 0)
                    {
                        var e = HpcPick(p, -1);
                        if (e == null)
                            break;  // (nothing in reach: they wait in the ring)
                        LaunchCrane(p, e);
                    }
                }
                if (p.HpcT >= HpcEnd)
                    EndCranes(p, true);
            }
        }

        /// The newest crane in the ring leaves along its orbit for `e`.
        void LaunchCrane(Player p, Enemy e)
        {
            int i = p.HpcN - 1;
            float heading;
            V2 from = p.Pos + HpcSlot(i, p.HpcT, out heading);
            var b = SpawnBullet(Defs.PaperCrane, from, V2.FromAngle(heading) * HpcSpeed, 0f, HpcLife, HpcDmg * p.Stats.DmgMult * p.Stats.HpcDmgMult, p.Id);
            b.Crane = true;
            b.HomeOn = e.Id;
            b.Pierce = 0;
            b.Pulled = true;  // the host sends its course
            p.HpcN--;
            p.HpcGone++;
        }

        /// The enemy in reach of the player with the most health left after the cranes already flying at it (`skip`: a crane
        /// re-picking, so it doesn't count itself).
        Enemy HpcPick(Player p, int skip)
        {
            Enemy best = null;
            float bestLeft = float.MinValue;
            foreach (var e in AllFoes(by: p.Id))
            {
                if (!Targetable(e) || e.Pos.FartherThan(p.Pos, HpcSeek + e.Radius))
                    continue;
                float left = e.Hp;
                foreach (var b in Bullets.Values)
                    if (b.Crane && b.Id != skip && b.Owner == p.Id && b.HomeOn == e.Id)
                        left -= b.Dmg;
                if (left > bestLeft)
                {
                    bestLeft = left;
                    best = e;
                }
            }
            return best;
        }

        /// Host, from MoveBullets: a crane curves in on its enemy, turning harder the longer it flies; with its enemy gone it
        /// picks again, or flies on straight and runs out.
        void SteerCrane(Bullet b, float dt)
        {
            Player p;
            if (!Players.TryGetValue(b.Owner, out p))
                return;
            b.SbAge += dt;
            Enemy e;
            if (b.HomeOn == 0 || !TryFoe(b.HomeOn, out e) || !Targetable(e))
            {
                e = HpcPick(p, b.Id);
                b.HomeOn = e != null ? e.Id : 0;
                if (e == null)
                    return;
            }
            float want = (e.Pos - b.Pos).Angle, have = b.Vel.Angle;
            float turn = (5f + 30f * b.SbAge) * dt;
            float diff = Math.Max(-turn, Math.Min(turn, MathX.WrapPi(want - have)));
            b.Vel = V2.FromAngle(have + diff) * HpcSpeed;
        }

        /// A crane in an enemy: it unfolds into paper scraps (its damage is the bullet's own).
        void CraneHit(Bullet b, Enemy e)
        {
            if (!b.Crane)
                return;
            b.Crane = false;
            Player p;
            if (Players.TryGetValue(b.Owner, out p))
            {
                p.HpcHits++;
                p.HpcDealt += b.Dmg;
            }
            BroadcastFx(FxKind.PaperCrane, b.Pos, b.Pos + b.Vel * 0.02f, HpcPaper, HpcFxHit, HpcFxHitT, Sfx.None);
        }

        // --- Network: the player's Hundred Paper Cranes fields (SyncPlayer) ---

        void SyncCranes(NetSync s, Player p)
        {
            s.Float(ref p.HpcT);
            if (p.HpcT > 0f)
                s.Byte(ref p.HpcN);
        }
    }
}
