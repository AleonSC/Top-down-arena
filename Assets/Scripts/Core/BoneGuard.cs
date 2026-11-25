using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Bone Guard's Marrow Tether and its Alt, the Ossuary Wall (Docs/Mockups/bone-guard-rework.html, concept B).
    ///   Marrow Tether: a dart that hits a different enemy within MarrowWindow of your last dart links the two with a
    ///     marrow thread for MarrowLife (FxKind.MarrowLink, Radius 0). When either is caged (KitWeapons.cs ApplyBoneDart),
    ///     every enemy linked to it is reeled MarrowYank px toward the cage (not bosses or heavies) and slowed MarrowSlow
    ///     while the cage holds; the thread snaps taut (FxKind.MarrowLink, Radius 1) and is spent.
    ///   Ossuary Wall: Alt (Msg.WeaponDash, WeaponAlt.BoneWall) lobs a bone to your cursor (BoneWallReach at most); after
    ///     BoneWallFlight a palisade of femurs rises across your aim (ZoneKind.OssuaryWall: Pos its middle, Aim the way it
    ///     was thrown, Radius half its length) for BoneWallLife. Enemy shots stop on it, enemies can't walk through it
    ///     (bosses can), and an enemy pressed against it takes a dart (BoneWallDart damage) every BoneWallTick, so it
    ///     cages them. Cooldown BoneWallCd.
    public partial class Game
    {
        public const float MarrowWindow = 1f, MarrowLife = 3f, MarrowYank = 40f, MarrowSlow = 0.3f;
        public const int MarrowCap = 24;
        public const float BoneWallCd = 8f, BoneWallFlight = 0.35f, BoneWallLife = 3f, BoneWallHalf = 55f, BoneWallThick = 7f;
        public const float BoneWallTick = 0.6f, BoneWallReach = 320f, BoneWallDart = 7f;
        public static readonly Rgba MarrowRed = new Rgba(0.82f, 0.28f, 0.37f);

        class MarrowLink { public int A, B; public float T; }
        readonly List<MarrowLink> marrowLinks = new List<MarrowLink>();
        readonly List<Zone> ossuaryWalls = new List<Zone>();

        float boneWallLocalCd;  // your own game: until the Alt fires again (drawn as a ring under your feet)
        public float BoneWallLocalCd => boneWallLocalCd;

        /// The wall's two ends: across the way it was thrown.
        public static void OssuaryEnds(Zone z, out V2 a, out V2 b)
        {
            V2 s = V2.FromAngle(z.Aim).Orthogonal() * z.Radius;
            a = z.Pos - s;
            b = z.Pos + s;
        }

        /// Is the bone still in the air?
        public static bool OssuaryInFlight(Zone z) => z.Duration - z.T < BoneWallFlight;

        /// Host: a Bone Guard dart hit `e`. Links it to the enemy your last dart hit, if that was within MarrowWindow.
        void LinkBoneDart(Player p, Enemy e)
        {
            Enemy prev;
            if (p.BoneLinkT > 0f && p.BoneLastHit != e.Id && TryFoe(p.BoneLastHit, out prev) && Targetable(prev)
                && !marrowLinks.Exists(l => l.A == prev.Id && l.B == e.Id || l.A == e.Id && l.B == prev.Id))
            {
                if (marrowLinks.Count >= MarrowCap)
                    marrowLinks.RemoveAt(0);
                marrowLinks.Add(new MarrowLink { A = prev.Id, B = e.Id, T = MarrowLife });
                BroadcastFx(FxKind.MarrowLink, prev.Pos, new V2(prev.Id, e.Id), MarrowRed, 0f, MarrowLife, Sfx.None);
            }
            p.BoneLastHit = e.Id;
            p.BoneLinkT = MarrowWindow;
        }

        /// Host: `caged` was just caged. Everything linked to it is reeled in and slowed; those threads are spent.
        void YankMarrow(Enemy caged, float hold)
        {
            for (int i = marrowLinks.Count - 1; i >= 0; i--)
            {
                var l = marrowLinks[i];
                int other = l.A == caged.Id ? l.B : l.B == caged.Id ? l.A : -1;
                if (other < 0)
                    continue;
                marrowLinks.RemoveAt(i);
                Enemy o;
                if (!TryFoe(other, out o) || !Targetable(o))
                    continue;
                BroadcastFx(FxKind.MarrowLink, caged.Pos, new V2(caged.Id, o.Id), MarrowRed, 1f, Math.Max(MarrowLife, hold), Sfx.None);  // (lives on, unseen after its flash, to hide the spent loose thread)
                if (o.Def.IsBoss || o.Def.Heavy)
                    continue;
                V2 d = caged.Pos - o.Pos;
                float len = d.Length, pull = Math.Min(MarrowYank, len - caged.Radius - o.Radius - 4f);
                if (pull > 0f && len > 0.01f)
                    KnockEnemy(o, d * (pull / len));
                Slow(o, hold, 1f - MarrowSlow);
            }
        }

        /// Host: the Ossuary Wall. The bone flies for BoneWallFlight, then the wall is up.
        void HostOssuaryWall(Player p, V2 aimAt)
        {
            if (p.Weapon.Id != WeaponId.BoneGuard || p.StunT > 0f || p.BoneWallCd > 0f || State == MatchState.GameOver)
                return;
            V2 at = TankAltLanding(p, aimAt, BoneWallReach);
            var z = AddZone(ZoneKind.OssuaryWall, p, at, BoneWallHalf * AreaMult(p), BoneWallFlight + BoneWallLife);
            z.Aim = (at - p.Pos).Angle;
            p.BoneWallCd = BoneWallCd;
            BroadcastFx(FxKind.Ring, p.Pos + V2.FromAngle(z.Aim) * p.Def.BodyR, Defs.BulletColor(Defs.BoneShot), 14f, 0.2f, Sfx.Throw);
        }

        /// Host, every tick: cooldowns, threads running out, walls landing.
        void TickBoneGuard(float dt)
        {
            foreach (var p in Players.Values)
            {
                p.BoneWallCd = Math.Max(0f, p.BoneWallCd - dt);
                p.BoneLinkT = Math.Max(0f, p.BoneLinkT - dt);
            }
            for (int i = marrowLinks.Count - 1; i >= 0; i--)
            {
                var l = marrowLinks[i];
                if ((l.T -= dt) <= 0f || !FoeExists(l.A) || !FoeExists(l.B))
                    marrowLinks.RemoveAt(i);
            }
            ossuaryWalls.Clear();
            foreach (var z in Zones.Values)
            {
                if (z.Kind != ZoneKind.OssuaryWall || OssuaryInFlight(z))
                    continue;
                if (!z.Landed)
                {
                    z.Landed = true;
                    BroadcastFx(FxKind.Spark, z.Pos, z.Def.Color, 10f, 0.15f, Sfx.Summon);  // the femurs punch up out of the floor
                }
                ossuaryWalls.Add(z);
            }
        }

        /// Host, after the enemies move: nobody walks through an Ossuary Wall (pushed out the side they came from; bosses walk
        /// through), and one pressed against it takes a dart every BoneWallTick.
        void KeepOffOssuaryWalls(float dt)
        {
            if (ossuaryWalls.Count == 0)
                return;
            foreach (var e in AllFoes())
            {
                if (e.Def.IsBoss)
                    continue;
                e.WallDartT -= dt;
                foreach (var z in ossuaryWalls)
                {
                    if (!Zones.ContainsKey(z.Id))
                        continue;
                    V2 a, b;
                    OssuaryEnds(z, out a, out b);
                    float reach = BoneWallThick + e.Radius;
                    V2 c = ClosestOn(a, b, e.Pos), off = e.Pos - c;
                    float d = off.Length;
                    if (d >= reach + 2f)
                        continue;
                    if (d < reach)
                    {
                        V2 n = d > 0.01f ? off / d : V2.FromAngle(z.Aim);
                        e.Pos = ClampToArena(c + n * (reach + 0.5f), e.Radius);
                        e.KnockLeft = V2.Zero;
                    }
                    if (e.WallDartT > 0f)
                        break;
                    e.WallDartT = BoneWallTick;
                    HurtEnemy(e, BoneWallDart * z.DmgMult, z.Owner);
                    Player owner;
                    if (Players.TryGetValue(z.Owner, out owner))
                        ApplyBoneDart(owner, e);
                    BroadcastFx(FxKind.Spark, c, z.Def.Color, 8f, 0.2f, Sfx.None);
                    break;
                }
            }
            if (!PvpOn)
                return;
            foreach (var q in Players.Values)  // PvP: another player's Ossuary Wall keeps you off it and darts you too (Pvp.cs)
            {
                if (q.Downed || q.WarpT > 0f)
                    continue;
                var s = StandOf(q);
                s.WallDartT -= dt;
                V2 push = V2.Zero;
                Zone dart = null;
                V2 at = V2.Zero;
                foreach (var z in ossuaryWalls)
                {
                    if (z.Owner == q.Id || !Zones.ContainsKey(z.Id))
                        continue;
                    V2 a, b;
                    OssuaryEnds(z, out a, out b);
                    float reach = BoneWallThick + s.Radius;
                    V2 c = ClosestOn(a, b, s.Pos), off = s.Pos - c;
                    float d = off.Length;
                    if (d >= reach + 2f)
                        continue;
                    if (d < reach)
                        push += (d > 0.01f ? off / d : V2.FromAngle(z.Aim)) * (reach + 0.5f - d);
                    if (dart == null)
                    {
                        dart = z;
                        at = c;
                    }
                }
                PvpWallPush(q, push);
                if (dart == null || s.WallDartT > 0f)
                    continue;
                s.WallDartT = BoneWallTick;
                HurtEnemy(s, BoneWallDart * dart.DmgMult, dart.Owner);
                BroadcastFx(FxKind.Spark, at, dart.Def.Color, 8f, 0.2f, Sfx.None);
            }
        }

        /// Host, from the enemy-bullet loop (Combat.cs): an enemy shot reaching an Ossuary Wall stops there.
        bool OssuaryStops(Bullet b)
        {
            foreach (var z in ossuaryWalls)
            {
                if (b.Pos.FartherThan(z.Pos, z.Radius + BoneWallThick + b.Radius) || !Zones.ContainsKey(z.Id))
                    continue;
                V2 a, e;
                OssuaryEnds(z, out a, out e);
                float reach = BoneWallThick + b.Radius;
                if (ClosestOn(a, e, b.Pos).DistanceSq(b.Pos) > reach * reach)
                    continue;
                BroadcastFx(FxKind.Spark, b.Pos, z.Def.Color, 6f, 0.2f, Sfx.None);
                return true;
            }
            return false;
        }
    }
}
