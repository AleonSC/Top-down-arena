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
    ///
    /// Step 3: the Driller plays like a Shiv; its leap drills a crack along its path (Enemy.Crack) that erupts DrillerWait s
    /// after it stops. The Geomancer moves and charges like a Taser Guard, but instead of a bolt it sends a crack racing at
    /// you (CrackSpeed, the Colossus's) that erupts GeoWait s after it stops. An erupting crack hurts whoever stands on it
    /// (CrackDamage). The Cinder Priest is a Taser Guard whose bolt is a magma bolt (Defs.MagmaBolt): wherever it stops
    /// (a hit, the end of its range, cover), it leaves a lava pool, which hurts like the Colossus's (TickQuarryHost).
    public static class QuarryKin
    {
        public static bool Is(EnemyType t) => t >= EnemyType.Rubble && t <= EnemyType.Stomper || t >= EnemyType.SlabBearer && t <= EnemyType.CinderPriest;
    }

    public partial class Enemy
    {
        public StompRing Stomp;  // a Stomper's ring: winding up (Warn > 0, it stands still) or spreading (replicated)
        public ColCrack Crack;   // a Driller's or Geomancer's crack: racing, then waiting to erupt (replicated)
        public float StompT;     // host: until it may stomp again
    }

    public partial class Game
    {
        public const float StomperEvery = 4f, StomperWind = 0.8f, StomperRange = 260f, StomperReach = 220f, StomperDamage = 12f;
        public const float HurlerLandR = 34f, HurlerDamage = 18f, HurlerLeadT = 0.9f, HurlerLeadMax = 200f, RubbleR = 28f, RubbleLife = 2f;
        public const int HurlerShards = 6;
        public const float SlabCoverLife = 3f, SlabCoverHalfL = 26f, SlabCoverHalfW = 8f;
        public static readonly Rgba HurlerColor = new Rgba(1f, 0.48f, 0.23f);
        public const float DrillerReach = 320f, DrillerWait = 0.4f, GeoWait = 0.5f, GeoReach = 460f, CrackDamage = 12f, CrackHalfW = 16f;
        public const float MagmaSpeed = 240f, MagmaRange = 300f, MagmaPoolR = 45f, MagmaPoolLife = 2.5f, QuarryLavaDps = 20f;

        static bool Cracks(EnemyType t) => t == EnemyType.Driller || t == EnemyType.Geomancer;

        /// Host, in EnemyFire: the Geomancer's crack and the Cinder Priest's bolt (both charge like a Taser Guard). False for anyone else.
        bool QuarryFire(Enemy e, V2 tpos, float aim)
        {
            if (e.Type == EnemyType.Geomancer)
            {
                e.FireT = TaserEvery;
                V2 dir = V2.FromAngle(aim);
                e.Crack = new ColCrack { From = e.Pos + dir * (e.Radius + 6f), Ang = aim, Max = Math.Min(GeoReach, e.Pos.DistanceTo(tpos) + 60f), Seed = e.Id * 7 + (int)(AnimT * 10f) };
                BroadcastFx(FxKind.Spark, e.Pos + dir * (e.Radius + 10f), HurlerColor, 22f, 0.25f, Sfx.BossCrack);  // the staff strikes the ground
                return true;
            }
            if (e.Type == EnemyType.CinderPriest)
            {
                e.FireT = TaserEvery;
                var b = SpawnBullet(Defs.MagmaBolt, e.Pos + V2.FromAngle(aim) * (e.Radius + 8f), V2.FromAngle(aim) * MagmaSpeed, 0f, MagmaRange / MagmaSpeed, Defs.BulletDamage(Defs.MagmaBolt), 0, false);
                b.Source = e.Type;
                magmaBolts[b.Id] = b.Pos;
                BroadcastFx(FxKind.Spark, e.Pos + V2.FromAngle(aim) * (e.Radius + 12f), HurlerColor, 22f, 0.2f, Sfx.Throw);
                return true;
            }
            return false;
        }

        readonly Dictionary<int, V2> magmaBolts = new Dictionary<int, V2>();  // host: magma bolts in flight and where each last was
        readonly HashSet<int> magmaParried = new HashSet<int>();              // host: parried (sent back, so no lava at your feet)
        readonly List<int> magmaGone = new List<int>();
        float quarryLavaT;

        /// Host, in the parry: a parried magma bolt leaves no lava.
        void MagmaParried(Bullet b)
        {
            if (b.Kind == Defs.MagmaBolt)
                magmaParried.Add(b.Id);
        }

        /// Every game: a crack grows (a Geomancer's on its own; a Driller's with its leap, on the host), then waits;
        /// the host erupts it. False once it has erupted.
        bool TickCrack(Enemy e, float dt)
        {
            var k = e.Crack;
            if (e.Type == EnemyType.Driller)
            {
                if (IsHost)
                {
                    if (e.Phase == 2 && k.Max > k.Len + 0.5f)
                        k.Len = Math.Min(k.Max, k.From.DistanceTo(e.Pos));
                    else if (k.Max > k.Len)
                        k.Max = k.Len;  // the leap is over: it stops here
                }
            }
            else if (GrowCrack(k, dt))
                return true;
            if (k.Len < k.Max)
                return true;
            k.Wait += dt;
            if (!IsHost || k.Wait < (e.Type == EnemyType.Driller ? DrillerWait : GeoWait))
                return true;
            V2 a = k.From, b = k.From + V2.FromAngle(k.Ang) * k.Len;
            foreach (var p in Players.Values)
                if (!p.Downed && p.InvulnT <= 0f && SegmentDistance(p.Pos, a, b) < CrackHalfW + p.Def.HitR)
                    Damage(p, CrackDamage, Defs.Get(e.Type).Name + " (crack)");
            BroadcastFx(FxKind.Bolt, a, b, HurlerColor, 0f, 0.3f, Sfx.BossCrack);  // it erupts
            return false;
        }

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
            // Magma bolts: wherever one stops (not parried), a lava pool.
            magmaGone.Clear();
            foreach (var kv in magmaBolts)
            {
                if (Bullets.TryGetValue(kv.Key, out Bullet b) && b.Kind == Defs.MagmaBolt)
                    continue;
                magmaGone.Add(kv.Key);
                if (!magmaParried.Contains(kv.Key))
                    SpawnPatchAt(PatchKind.Lava, ClampToArena(kv.Value, 20f), 0.2f, 0.2f + MagmaPoolLife, MagmaPoolR, MagmaPoolR);
            }
            foreach (var id in magmaGone)
            {
                magmaBolts.Remove(id);
                magmaParried.Remove(id);
            }
            foreach (var b in Bullets.Values)
                if (b.Kind == Defs.MagmaBolt && magmaBolts.ContainsKey(b.Id))
                    magmaBolts[b.Id] = b.Pos;
            // Lava with no Colossus about (its fight ticks its own): it hurts the same.
            quarryLavaT -= dt;
            if (quarryLavaT <= 0f)
            {
                quarryLavaT = 0.25f;
                if (TheColossus() == null)
                    foreach (var p in Players.Values)
                    {
                        if (p.Downed || p.InvulnT > 0f)
                            continue;
                        foreach (var pt in Patches.Values)
                            if (pt.Kind == PatchKind.Lava && pt.Active && p.Pos.DistanceTo(pt.Pos) < pt.Radius + p.Def.HitR)
                            {
                                DamageOverTime(p, QuarryLavaDps * 0.25f, "Lava");
                                break;
                            }
                    }
            }
        }

        /// Every game, each frame: the wind-up runs down, then the ring spreads until StomperReach; the host hurts once
        /// whoever it passes over outside the gap.
        void UpdateStompRings(float dt)
        {
            if (IsHost)
                TickQuarryHost(dt);
            foreach (var e in Enemies.Values)
            {
                if (IsHost && e.Type == EnemyType.Driller && e.Phase == 2 && e.Crack == null)
                    e.Crack = new ColCrack { From = e.Pos, Ang = e.Facing, Max = DrillerReach, Seed = e.Id * 13 + (int)(AnimT * 10f) };  // the drill bites in
                if (e.Crack != null && Cracks(e.Type) && !TickCrack(e, dt))
                    e.Crack = null;
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
        /// A Driller's or Geomancer's crack, if it has one.
        void SyncCrack(NetSync s, Enemy e)
        {
            bool has = !s.Reading && e.Crack != null;
            s.Bool(ref has);
            if (!has)
            {
                if (s.Reading)
                    e.Crack = null;
                return;
            }
            if (s.Reading && e.Crack == null)
                e.Crack = new ColCrack();
            var k = e.Crack;
            s.Vec(ref k.From);
            s.Float(ref k.Ang);
            s.Float(ref k.Len);
            s.Float(ref k.Max);
            s.Float(ref k.Wait);
            s.Int(ref k.Seed);
        }

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
