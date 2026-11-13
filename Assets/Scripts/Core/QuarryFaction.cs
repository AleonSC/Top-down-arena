using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Colossus's faction, The Quarry (Docs/Approaches.md "The Quarry", mockup Docs/Mockups/colossus-faction.html).
    /// Step 1 is three enemies that share their Warden counterpart's behaviour through HiveKin's table: Rubble plays like a
    /// Rioter, the Quarryman like a Guard and the Stomper like a Thug (a Brute). The Stomper's own trick is a small
    /// Colossus stomp (Colossus.cs StompRing): within StomperRange of its target it plants its feet and lifts a foot for
    /// StomperWind (the ring's Warn, while its green gap already shows), then stomps out a ring of push blocks with one
    /// gap a short step beside you, which fades at StomperReach. Each Stomper has at most one ring (Enemy.Stomp,
    /// replicated by SyncStomper); every game spreads it, the host hurts whoever it passes over, and each game pushes its
    /// own player out of it (StomperPush), as the Colossus's rings do.
    ///
    /// Step 2: the Slab Bearer plays like a Riot Shield; when its slab breaks it drops it in front as cover (a PatchKind.Slab,
    /// SlabCoverLife s) that stops shots from both sides. The Rock Hurler plays like a Gas Grenadier, but locks its ring on
    /// where you're heading (HurlerLead) and lobs a boulder: it lands for HurlerDamage, bursts into HurlerShards shards and
    /// lies as a rubble pile (a PatchKind.Rubble, RubbleLife s) that stops shots too (QuarryCoverStops, from Collide).
    public static class QuarryKin
    {
        public static bool Is(EnemyType t) => t >= EnemyType.Rubble && t <= EnemyType.Stomper || t == EnemyType.SlabBearer || t == EnemyType.RockHurler;
    }

    public partial class Enemy
    {
        public StompRing Stomp;  // a Stomper's ring: winding up (Warn > 0, it stands still) or spreading (replicated)
        public float StompT;     // host: until it may stomp again
    }

    public partial class Game
    {
        public const float StomperEvery = 4f, StomperWind = 0.8f, StomperRange = 260f, StomperReach = 220f, StomperDamage = 12f;
        public const float HurlerLandR = 34f, HurlerDamage = 18f, HurlerLeadT = 0.9f, HurlerLeadMax = 200f, RubbleR = 28f, RubbleLife = 2f;
        public const int HurlerShards = 6;
        public const float SlabCoverLife = 3f, SlabCoverHalfL = 26f, SlabCoverHalfW = 8f;
        public static readonly Rgba HurlerColor = new Rgba(1f, 0.48f, 0.23f);

        /// Lifting its foot: it stands still until the stomp lands.
        public static bool StomperPlanted(Enemy e) => e.Stomp != null && e.Stomp.Warn > 0f;

        /// How high its foot is, 0 to 1 (drawing).
        public static float StomperLift(Enemy e) => StomperPlanted(e) ? MathX.Clamp01(1f - e.Stomp.Warn / StomperWind) : 0f;

        /// Host, in Special: starts a stomp once it's close enough and rested. The gap is placed now, so it shows through the wind-up.
        void StomperSpecial(Enemy e, V2 tpos, float dt)
        {
            e.StompT -= dt;
            if (e.Stomp != null || e.StompT > 0f || e.SpawnT > 0f || e.Pos.DistanceTo(tpos) > StomperRange)
                return;
            e.StompT = StomperEvery;
            float gap = GapBeside(e, tpos, Rand(0f, 1f) < 0.5f ? -1f : 1f);
            e.Stomp = new StompRing { Center = e.Pos, Radius = e.Radius + 6f, Warn = StomperWind, Gap1 = MathX.Fmod(gap, MathX.Tau) };
        }

        // --- Rock Hurler and Slab Bearer -----------------------------------------------------------------------------

        readonly Dictionary<int, V2> quarryLast = new Dictionary<int, V2>(), quarryVel = new Dictionary<int, V2>();
        readonly HashSet<int> rubbleLanded = new HashSet<int>();
        bool quarryCover;  // set for each Collide: a pile or slab is down

        /// Where a Rock Hurler aims: the player at `tpos`, a little ahead of where they're heading.
        V2 HurlerLead(V2 tpos)
        {
            Player best = null;
            foreach (var p in Players.Values)
                if (!p.Downed && (best == null || p.Pos.DistanceTo(tpos) < best.Pos.DistanceTo(tpos)))
                    best = p;
            V2 v = V2.Zero;
            if (best != null)
                quarryVel.TryGetValue(best.Id, out v);
            return ClampToArena(tpos + v.LimitLength(HurlerLeadMax / HurlerLeadT) * HurlerLeadT, 60f);
        }

        /// Host: a Slab Bearer's broken slab drops in front of it, standing across its facing.
        void DropSlab(Enemy e)
        {
            V2 at = ClampToArena(e.Pos + V2.FromAngle(e.Facing) * (e.Radius + SlabCoverHalfW + 4f), 20f);
            SpawnPatchAt(PatchKind.Slab, at, 0f, SlabCoverLife, SlabCoverHalfL, e.Facing);
        }

        /// A slab lies across the direction its bearer faced (R1): a box SlabCoverHalfW deep, SlabCoverHalfL wide.
        public static bool InSlabCover(PathPatch pt, V2 p, float pad)
        {
            V2 f = V2.FromAngle(pt.R1), rel = p - pt.Pos;
            float along = rel.X * f.X + rel.Y * f.Y, across = rel.X * -f.Y + rel.Y * f.X;
            return Math.Abs(along) < SlabCoverHalfW + pad && Math.Abs(across) < SlabCoverHalfL + pad;
        }

        void FindQuarryCover()
        {
            quarryCover = false;
            foreach (var pt in Patches.Values)
                if ((pt.Kind == PatchKind.Rubble || pt.Kind == PatchKind.Slab) && pt.Active)
                {
                    quarryCover = true;
                    return;
                }
        }

        /// Host, in Collide: a landed rubble pile or a dropped slab stops shots from both sides.
        bool QuarryCoverStops(Bullet b)
        {
            foreach (var pt in Patches.Values)
            {
                if (!pt.Active)
                    continue;
                if (pt.Kind == PatchKind.Rubble && b.Pos.DistanceTo(pt.Pos) < pt.R0 + b.Radius)
                    return true;
                if (pt.Kind == PatchKind.Slab && InSlabCover(pt, b.Pos, b.Radius))
                    return true;
            }
            return false;
        }

        /// Host, each frame: players' movement (for the Rock Hurler's lead), and boulders landing.
        void TickQuarryHost(float dt)
        {
            foreach (var p in Players.Values)
            {
                V2 last;
                if (dt > 0f && quarryLast.TryGetValue(p.Id, out last))
                {
                    V2 v = (p.Pos - last) / dt, old;
                    quarryVel.TryGetValue(p.Id, out old);
                    quarryVel[p.Id] = old + (v.LimitLength(600f) - old) * Math.Min(1f, dt * 6f);
                }
                quarryLast[p.Id] = p.Pos;
            }
            foreach (var pt in Patches.Values)
            {
                if (pt.Kind != PatchKind.Rubble || !pt.Active || !rubbleLanded.Add(pt.Id))
                    continue;
                foreach (var p in Players.Values)
                    if (!p.Downed && p.InvulnT <= 0f && p.Pos.DistanceTo(pt.Pos) < HurlerLandR + p.Def.HitR)
                        Damage(p, HurlerDamage, "Rock Hurler (boulder)");
                for (int k = 0; k < HurlerShards; k++)
                {
                    float a = MathX.Tau * k / HurlerShards + pt.Id * 0.7f;
                    SpawnBullet(Defs.SmallShot, pt.Pos + V2.FromAngle(a) * (pt.R0 + 8f), V2.FromAngle(a) * 140f, 0f, 3f, Defs.BulletDamage(Defs.SmallShot), 0, false).Source = EnemyType.RockHurler;
                }
                BroadcastFx(FxKind.Boom, pt.Pos, new Rgba(0.78f, 0.62f, 0.45f), HurlerLandR + 16f, 0.35f, Sfx.BossSlam);  // it lands
            }
            if (rubbleLanded.Count > 0)
                rubbleLanded.RemoveWhere(id => !Patches.ContainsKey(id));
        }

        /// Every game, each frame: the wind-up runs down, then the ring spreads until StomperReach; the host hurts once
        /// whoever it passes over outside the gap.
        void UpdateStompRings(float dt)
        {
            if (IsHost)
                TickQuarryHost(dt);
            foreach (var e in Enemies.Values)
            {
                var r = e.Stomp;
                if (r == null || e.Type != EnemyType.Stomper)
                    continue;
                if (r.Warn > 0f)
                {
                    r.Warn -= dt;
                    if (r.Warn <= 0f && IsHost)
                        BroadcastFx(FxKind.Ring, e.Pos, Defs.Get(EnemyType.Stomper).Color, e.Radius + 40f, 0.35f, Sfx.BossSlam);  // the stomp
                    continue;
                }
                r.Radius += RingSpeed * dt;
                if (IsHost)
                    HurtByRing(r, StomperDamage, "Stomper (stomp)");
                if (RingBack(r) > StomperReach)
                    e.Stomp = null;
            }
        }

        /// Your own game: a Stomper's ring passes over you or shoves you back, like the Colossus's.
        void StomperPush(Player me)
        {
            if (me.Downed)
                return;
            foreach (var e in Enemies.Values)
                if (e.Stomp != null && e.Type == EnemyType.Stomper)
                    PushOutOfRing(e.Stomp, me);
        }

        /// The ring a Stomper has going, if any (the snapshot; the rest of its state is the Thug's).
        void SyncStomper(NetSync s, Enemy e)
        {
            bool has = !s.Reading && e.Stomp != null;
            s.Bool(ref has);
            if (!has)
            {
                if (s.Reading)
                    e.Stomp = null;
                return;
            }
            if (s.Reading && e.Stomp == null)
                e.Stomp = new StompRing();
            var r = e.Stomp;
            s.Vec(ref r.Center);
            s.Float(ref r.Radius);
            s.Float(ref r.Warn);
            s.Float(ref r.Gap1);
        }
    }
}
