using System;
using System.Collections.Generic;

namespace Arena
{
    /// The DPS Ninja's Dragon Fang Kunai (WeaponId.DragonFangKunai, took the Kunai's place; mockup claude.ai artifact
    /// "Dragon Fang Kunai", look A Fire Serpent):
    ///   - Fire throws one huge kunai (bullet Defs.DragonFang) at the cursor. Only one is out at a time: Fire does nothing
    ///     until it's gone (DragonFangOut), then the next is ready after DragonFangReady.
    ///   - It bites one enemy (DragonFangBaseHits); each Dragon's Hunger card adds DragonFangHungerBites and each extra projectile
    ///     DragonFangExtraHits, up to DragonFangHits (DragonFangMax). Each bite sends it on to the nearest enemy it hasn't bitten
    ///     within DragonFangReach, DragonFangGrow (10%) harder each bite. With nobody new in reach it
    ///     flies on: off an arena edge or a wall it bounces (FangWalled) and may bite anyone again, so a lone boss still takes the
    ///     chain. It fades after DragonFangSeek with nothing to bite, or after DragonFangWalls bounces in a row.
    ///   - The 12th bite bursts: DragonFangBurstShare of that bite to everything within DragonFangBurstR.
    ///   - Every throw is a shadow kunai: it leaves the Kunai's mark where it bites last (or stops), and the weapon-action key
    ///     blinks you there for a double hit (Ninja.cs, WeaponAlt.Blink).
    ///   - Host only: the chain (FangFlight) lives here and the host steers the bullet (Pulled: Msg.Steer keeps every copy on
    ///     course). Each bite is FxKind.DragonFangBite with Pos2 = (bullet id, bites so far), so every screen knows how fierce
    ///     the dragon is (Game/DragonFangLook.cs); Pos2.Y 0 is its end, where it fades.
    public partial class Game
    {
        public const float DragonFangSpeed = 950f, DragonFangReach = 300f, DragonFangWallReach = 2000f, DragonFangGrow = 0.1f;
        public const int DragonFangHits = 12, DragonFangBaseHits = 1, DragonFangHungerBites = 4, DragonFangExtraHits = 2, DragonFangWalls = 2;
        public const float DragonFangSeek = 2.2f, DragonFangReady = 0.5f, DragonFangBurstR = 120f, DragonFangBurstShare = 0.5f;
        public static readonly Rgba DragonFire = new Rgba(1f, 0.55f, 0.15f);

        class FangFlight
        {
            public int Owner, Max, Hits, Target, Walls;
            public V2 Last;
            public readonly HashSet<int> Bitten = new HashSet<int>();
        }

        readonly Dictionary<int, FangFlight> fangs = new Dictionary<int, FangFlight>();  // host: bullet id -> its chain
        readonly List<int> fangGone = new List<int>();

        /// Host: is this player's Dragon Fang still out?
        public bool DragonFangOut(int owner)
        {
            foreach (var f in fangs.Values)
                if (f.Owner == owner)
                    return true;
            return false;
        }

        /// The most bites a throw of this player's gets: one, +DragonFangHungerBites a Dragon's Hunger card, +DragonFangExtraHits an
        /// extra projectile, never more than DragonFangHits.
        public int DragonFangMax(Player p) => Math.Min(DragonFangHits, DragonFangBaseHits + p.Stats.FangBites + DragonFangExtraHits * ExtraShots(p));

        /// The n-th bite's damage against the first (n from 1): 1, 1.1, 1.21 ...
        public static float DragonFangBiteMult(int n) => (float)Math.Pow(1f + DragonFangGrow, Math.Max(0, n - 1));

        /// Fire with the Dragon Fang Kunai (FireNinjaWeapon): one throw, unless one is already out.
        void ThrowDragonFang(Player p, V2 dir, V2 muzzle, float dmg)
        {
            if (DragonFangOut(p.Id))
                return;
            var w = p.Weapon;
            var b = SpawnBullet(Defs.DragonFang, muzzle, dir * w.Speed, 0f, w.Life, w.Damage * dmg, p.Id);
            b.Pierce = 99;
            b.ShadowKunai = true;  // the Kunai's mark, where it bites last
            b.Pulled = true;       // (the host steers it: SendSteer)
            b.Homing = 0f;         // (it picks its own way, Homing Rounds or not)
            if (b.Sent != null)
                b.Sent.Homing = 0f;
            fangs[b.Id] = new FangFlight { Owner = p.Id, Max = DragonFangMax(p), Last = b.Pos };
        }

        /// The nearest enemy this chain hasn't bitten within reach of `from`, or null.
        Enemy FangNext(V2 from, FangFlight f, float reach, int owner)
        {
            Enemy best = null;
            float bestD = reach * reach;
            foreach (var e in FoesInCircle(from, reach, by: owner))
            {
                if (f.Bitten.Contains(e.Id) || !Targetable(e))
                    continue;
                float d = from.DistanceSq(e.Pos);
                if (d < bestD)
                {
                    bestD = d;
                    best = e;
                }
            }
            return best;
        }

        /// Host, every tick before bullets move (PrepareNinjaBullets): each Dragon Fang flies at its next enemy, bounces off
        /// the arena's edges, and when it's gone its dragon fades where it was.
        void TickDragonFangs(float dt)
        {
            if (!IsHost || fangs.Count == 0)
                return;
            fangGone.Clear();
            foreach (var kv in fangs)
            {
                var f = kv.Value;
                Bullet b;
                Player p;
                bool owner = Players.TryGetValue(f.Owner, out p) && !p.Downed;
                if (!Bullets.TryGetValue(kv.Key, out b) || !owner)
                {
                    if (b != null)
                        KillBullet(b.Id);
                    fangGone.Add(kv.Key);
                    if (p != null)
                        p.FireCd = Math.Max(p.FireCd, DragonFangReady);
                    BroadcastFx(FxKind.DragonFangBite, f.Last, new V2(kv.Key, 0f), DragonFire, 30f + 4f * f.Hits, 0.5f, Sfx.None);  // the dragon fades here
                    continue;
                }
                b.Pierce = 99;
                f.Last = b.Pos;
                float speed = DragonFangSpeed * p.Stats.ShotSpeedMult;
                Enemy e;
                if (f.Target != 0 && (!TryFoe(f.Target, out e) || !Targetable(e) || f.Bitten.Contains(f.Target)))
                {
                    f.Target = 0;  // it died or went away: the next one in reach of here
                    if (f.Hits > 0)
                        f.Target = FangNext(b.Pos, f, DragonFangReach, f.Owner)?.Id ?? 0;
                }
                if (f.Target != 0 && TryFoe(f.Target, out e))
                {
                    V2 d = e.Pos - b.Pos;
                    if (!d.IsZero)
                        b.Vel = d.Normalized() * speed;
                }
                if (f.Hits == 0)
                    continue;  // (the first flight goes straight and ends at the edge)
                V2 lo = ArenaMin + new V2(b.Radius, b.Radius), hi = ArenaMax - new V2(b.Radius, b.Radius);
                V2 n = V2.Zero;
                if (b.Pos.X < lo.X && b.Vel.X < 0f) n = new V2(1f, 0f);
                else if (b.Pos.X > hi.X && b.Vel.X > 0f) n = new V2(-1f, 0f);
                else if (b.Pos.Y < lo.Y && b.Vel.Y < 0f) n = new V2(0f, 1f);
                else if (b.Pos.Y > hi.Y && b.Vel.Y > 0f) n = new V2(0f, -1f);
                if (n.IsZero)
                    continue;
                float into = b.Vel.X * n.X + b.Vel.Y * n.Y;
                b.Vel -= n * (2f * into);
                b.Pos = b.Pos.Clamp(lo, hi);
                FangWalled(b);
            }
            foreach (int id in fangGone)
                fangs.Remove(id);
        }

        /// Host: a Dragon Fang bounced off an edge or a wall (TickDragonFangs, Relics.WallBounce). Anyone may be bitten again;
        /// it heads for the nearest within DragonFangWallReach. Too many bounces in a row and it fades.
        void FangWalled(Bullet b)
        {
            FangFlight f;
            if (!fangs.TryGetValue(b.Id, out f))
                return;
            if (++f.Walls > DragonFangWalls)
            {
                b.Life = 0f;
                return;
            }
            f.Bitten.Clear();
            b.LastHit = b.PrevHit = 0;
            b.Life = DragonFangSeek;
            var next = FangNext(b.Pos, f, DragonFangWallReach, f.Owner);
            f.Target = next != null ? next.Id : 0;
            if (next != null && !(next.Pos - b.Pos).IsZero)
                b.Vel = (next.Pos - b.Pos).Normalized() * b.Vel.Length;
            AddFx(FxKind.Ring, b.Pos, DragonFire, 12f, 0.2f);
        }

        /// Host, from Relics.WallBounce: a Dragon Fang on its chain always bounces off walls and pillars.
        bool FangBouncesOffWalls(Bullet b)
        {
            FangFlight f;
            return b.Kind == Defs.DragonFang && fangs.TryGetValue(b.Id, out f) && f.Hits > 0;
        }

        /// Host, from PlayerShotHits: this chain has already bitten that enemy (it passes over it).
        bool FangBit(Bullet b, Enemy e)
        {
            FangFlight f;
            return b.Kind == Defs.DragonFang && fangs.TryGetValue(b.Id, out f) && f.Bitten.Contains(e.Id);
        }

        /// Host, from OnBulletHit: a bite. The next one hits 10% harder; it goes on to the nearest new enemy, or bursts on the last.
        void DragonFangBite(Bullet b, Enemy e)
        {
            FangFlight f;
            if (b.Kind != Defs.DragonFang || !fangs.TryGetValue(b.Id, out f))
                return;
            float bite = b.Dmg;
            f.Hits++;
            f.Walls = 0;
            f.Bitten.Add(e.Id);
            f.Last = e.Pos;
            b.Dmg *= 1f + DragonFangGrow;
            b.Life = DragonFangSeek;  // it flies on this long looking for the next
            b.Pierce = 99;
            bool last = f.Hits >= f.Max, burst = f.Hits >= DragonFangHits;  // (only the 12th bite bursts)
            BroadcastFx(FxKind.DragonFangBite, e.Pos, new V2(b.Id, f.Hits), DragonFire, 20f + 2.5f * f.Hits, 0.35f, burst ? Sfx.Explode : Sfx.ShotFireballBurn);
            if (last && !burst)
            {
                b.Life = 0f;  // the chain is done: gone at the next move, its mark where it bit
                return;
            }
            if (last)
            {
                Player p;
                float r = DragonFangBurstR * (Players.TryGetValue(b.Owner, out p) ? AreaMult(p) : 1f);
                foreach (var o in FoesInCircle(e.Pos, r, by: b.Owner))
                    if (o != e && Targetable(o))
                        HurtEnemy(o, bite * DragonFangBurstShare, b.Owner);
                BroadcastFx(FxKind.Boom, e.Pos, DragonFire, r, 0.35f, Sfx.None);
                b.Life = 0f;  // (gone at the next move; its mark is where it bit)
                return;
            }
            var next = FangNext(e.Pos, f, DragonFangReach, b.Owner);
            f.Target = next != null ? next.Id : 0;
            if (next != null && !(next.Pos - b.Pos).IsZero)
                b.Vel = (next.Pos - b.Pos).Normalized() * b.Vel.Length;
        }
    }
}
