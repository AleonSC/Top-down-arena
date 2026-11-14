using System;

namespace Arena
{
    /// The Eclipse's faction, the Penumbral Court (Docs/EclipseFaction.md, mockup Docs/Mockups/eclipse-faction.html; drawn
    /// in Game/PenumbralLook.cs). Each enemy previews one thing the Eclipse's stages test. Step 1 has three:
    ///   Ember (Lunar wake): melee, weaves in at you and drops a spark (Defs.EmberSpark) every EmberSparkEvery s that
    ///     drifts back slowly and hangs for EmberSparkLife s, so crossing its trail hurts. Its age (Enemy.T) times the sparks.
    ///   Corona Priest (Corona rings): keeps PriestRange and strafes; winds up PriestWindUp s (its halo flares), then fires
    ///     a ring of PriestSlots - 1 slow shots: one straight at you, and the gap one slot to the side (Strafe picks it).
    ///   Heliograph (Solar flares): Phase 0 walks in to HelioRange; 1 plants and aims its mirror at you, a line flickers
    ///     for HelioWarn s; 2 the line is a lane of fire HelioWidth wide for HelioBurn s; 3 rests HelioRest s. Phase and
    ///     Facing are in the snapshot, so clients draw the line with no extra fields (timed from Enemy.SeenT).
    /// Step 2 adds three:
    ///   Moonward (the Penumbra's moon): melee; a moon circles it MoonwardOrbit px out, one turn every MoonwardTurn s, and
    ///     soaks every player shot it touches (MoonwardStops, from Collide). Its Facing is the moon's angle (in the snapshot).
    ///   Gnomon (Eclipse flash): a heavy walking sundial. Within GnomonRange, every GnomonEvery s it plants (Phase 1) with its
    ///     slab facing GnomonOffset rad to one side of you while its lantern swells for GnomonSwell s, then flashes (Phase 2,
    ///     GnomonRecover s): GnomonDamage to every player within GnomonReach who isn't in the slab's shadow (InGnomonShadow,
    ///     a widening wedge in front of it). The shadow is a short step to the side: walk into it, toward the Gnomon.
    ///   Dusk Lobber (the Umbra): plays like a Gas Grenadier (HiveKin.cs), but its lob lands as a pool of dark (PatchKind.Dusk,
    ///     DuskPoolR, DuskLife s) that drains DuskDps (TickPenumbral) and hides what's in it beyond DuskSight of each player
    ///     (Game/PenumbralLook.cs).
    /// Step 3 adds two:
    ///   Sickle (Crescents): keeps SickleRange; winds up SickleWindUp s, then throws a crescent blade (Defs.CrescentShot,
    ///     the Eclipse's) on a loop that leaves to one side, passes through where you stand and comes back round to it
    ///     (SickleThrow: the boss's ThrowCrescent, smaller and slower).
    ///   Nova (Totality): melee, darts in weaving; within NovaLight it lights a NovaFuse s fuse (Phase 1: it hovers
    ///     NovaHover px off you, slowed, and takes NovaLitMult damage), then goes nova: NovaBlastDamage to everyone within
    ///     NovaBlastR, and it's gone (no kill credit).
    /// Step 4, the set piece: Last Light (SquadForm.LastLight), four Heliographs at the corners and eight Embers along the
    ///   sides of a square round a player, like the Eclipse's burning edge. It turns at LastLightSpin rad/s and closes from
    ///   LastLightStart to LastLightEnd px (half its side) at LastLightClose px/s; the Embers' sparks hang where they pass,
    ///   so the edge burns, and the Heliographs flare in from the corners. Kill a side to break out. It breaks up after
    ///   LastLightLife s, or once half of it is down.
    public partial class Game
    {
        public const float EmberWeave = 0.55f, EmberSparkEvery = 0.2f, EmberSparkLife = 1.5f, EmberSparkDrift = 25f, EmberSparkRange = 650f;
        public const float PriestRange = 260f, PriestEvery = 2.4f, PriestWindUp = 0.7f, PriestShotSpeed = 130f;
        public const int PriestSlots = 14;
        public const float HelioRange = 340f, HelioWarn = 1f, HelioBurn = 0.5f, HelioRest = 2.5f, HelioLen = 900f, HelioWidth = 40f;
        public const int MaxHeliographs = 3;  // at most this many a wave (CapBeams in Variety.cs)
        float HelioDamage => 20f;  // the difficulty's per-wave damage growth scales it, like the Pulsar's

        public const float MoonwardOrbit = 30f, MoonwardTurn = 2.4f, MoonwardMoonR = 9f;
        public const float GnomonRange = 360f, GnomonEvery = 6f, GnomonSwell = 1.5f, GnomonRecover = 0.5f, GnomonOffset = 0.6f,
            GnomonReach = 520f, GnomonShadowLen = 380f, GnomonShadowBase = 24f, GnomonShadowFlare = 0.32f;
        float GnomonDamage => 30f;
        public const float DuskPoolR = 90f, DuskLife = 4f, DuskDps = 6f, DuskSight = 40f;
        static readonly Rgba DuskColor = new Rgba(0.61f, 0.48f, 1f);

        public const float SickleRange = 240f, SickleEvery = 2.4f, SickleWindUp = 0.6f, SickleSpeed = 300f, SickleLoopMin = 150f;
        public const float NovaLight = 140f, NovaFuse = 3f, NovaBlastR = 110f, NovaHover = 60f, NovaLitSpeed = 0.35f, NovaLitMult = 1.5f;
        float NovaBlastDamage => 30f;

        public const float LastLightStart = 330f, LastLightEnd = 140f, LastLightClose = 16f, LastLightSpin = 0.18f, LastLightLife = 14f;

        sealed class LastLightSquare { public V2 Center; public float Spin0; public int Size; }
        readonly System.Collections.Generic.Dictionary<int, LastLightSquare> lastLights = new System.Collections.Generic.Dictionary<int, LastLightSquare>();  // host: by squad id

        /// The point at `u` round a square (0-4: a corner at each whole number), `half` px from its middle to each side,
        /// turned by `spin`.
        public static V2 LastLightPoint(V2 center, float spin, float u, float half)
        {
            int side = (int)Math.Floor(u) & 3;
            float k = u - (float)Math.Floor(u);
            V2 a = SquareCorner(side, half), b = SquareCorner(side + 1, half);
            return center + (a + (b - a) * k).Rotated(spin);
        }

        static V2 SquareCorner(int c, float half)
        {
            switch (c & 3)
            {
                case 0: return new V2(-half, -half);
                case 1: return new V2(half, -half);
                case 2: return new V2(half, half);
                default: return new V2(-half, half);
            }
        }

        /// Its place on the square: every third member a corner (the Heliographs), the others a third and two thirds
        /// along each side.
        public static float LastLightU(int i) => i / 3f;

        V2 LastLightSpawn(int squad, int i, Player target)
        {
            if (!lastLights.TryGetValue(squad, out var sq))
            {
                V2 lo = ArenaMin + new V2(150f, 150f), hi = ArenaMax - new V2(150f, 150f);
                V2 c = new V2(Math.Max(lo.X, Math.Min(hi.X, target.Pos.X)), Math.Max(lo.Y, Math.Min(hi.Y, target.Pos.Y)));
                lastLights[squad] = sq = new LastLightSquare { Center = c, Spin0 = Rand(0f, MathX.Tau) };
            }
            sq.Size++;
            return ClampToArena(LastLightPoint(sq.Center, sq.Spin0, LastLightU(i), LastLightStart), 30f);
        }

        /// Its place on the turning, closing square; a Heliograph stands still while it flares; once the square breaks
        /// up, its own way.
        V2 LastLightMove(Enemy e, System.Collections.Generic.List<Enemy> mates, V2 v)
        {
            var sq = lastLights[e.SquadId];
            if (e.T > LastLightLife || mates.Count * 2 < sq.Size)
                return v;
            if (HelioLocked(e))
                return V2.Zero;
            float t = Math.Max(0f, e.T - 0.9f);  // (from when it has spawned in)
            float half = Math.Max(LastLightEnd, LastLightStart - LastLightClose * t);
            return GoalVel(e, ClampToArena(LastLightPoint(sq.Center, sq.Spin0 + LastLightSpin * t, LastLightU(e.SquadSlot), half), 30f));
        }

        public static bool IsPenumbral(EnemyType t) => t >= EnemyType.Ember && t <= EnemyType.Heliograph || t >= EnemyType.Moonward && t <= EnemyType.Nova;

        /// A Nova with its fuse lit: it takes NovaLitMult damage (Combat.cs).
        public static bool NovaLit(Enemy e) => e.Type == EnemyType.Nova && e.Phase == 1;

        /// The Gnomon keeps its slab's line while its lantern swells and flashes.
        public static bool GnomonLocked(Enemy e) => e.Type == EnemyType.Gnomon && (e.Phase == 1 || e.Phase == 2);

        /// Where a Moonward's moon is (its Facing is the moon's angle).
        public static V2 MoonwardMoon(Enemy e, V2 pos) => pos + V2.FromAngle(e.Facing) * MoonwardOrbit;

        /// Is this point in a Gnomon's shadow: a wedge in front of its slab, widening as it goes?
        public static bool InGnomonShadow(Enemy e, V2 p, float pad = 0f)
        {
            V2 f = V2.FromAngle(e.Facing), rel = p - e.Pos;
            float along = rel.Dot(f), across = Math.Abs(rel.Dot(f.Orthogonal()));
            return along > e.Radius * 0.4f && along < GnomonShadowLen && across + pad < GnomonShadowBase + along * GnomonShadowFlare;
        }

        /// The Heliograph keeps its mirror's line while it flickers and burns.
        public static bool HelioLocked(Enemy e) => e.Type == EnemyType.Heliograph && (e.Phase == 1 || e.Phase == 2);

        V2 PenumbralMove(Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed)
        {
            switch (e.Type)
            {
                case EnemyType.Ember:
                {
                    // Weaves either side of your line, straighter up close.
                    float a = EmberWeave * (float)Math.Sin(e.T * 4f + e.Id) * MathX.Clamp01(dist / 200f);
                    return dir * (float)Math.Cos(a) + dir.Orthogonal() * (float)Math.Sin(a);
                }
                case EnemyType.CoronaPriest:
                    return KeepDistance(dir, dist, PriestRange) + dir.Orthogonal() * e.Strafe * 0.6f;
                case EnemyType.Heliograph:
                    if (HelioLocked(e))
                        return V2.Zero;  // planted while its mirror aims and burns
                    return KeepDistance(dir, dist, HelioRange) + dir.Orthogonal() * e.Strafe * 0.3f;
                case EnemyType.Gnomon:
                    if (GnomonLocked(e))
                        return V2.Zero;  // planted while its lantern swells and flashes
                    break;
                case EnemyType.Sickle:
                    return KeepDistance(dir, dist, SickleRange) + dir.Orthogonal() * e.Strafe * 0.6f;
                case EnemyType.Nova:
                    if (e.Phase == 1)
                    {
                        speed *= NovaLitSpeed;  // lit: it hangs about you, slowly, so you can back off
                        return KeepDistance(dir, dist, NovaHover) + dir.Orthogonal() * e.Strafe * 0.5f;
                    }
                    float w = 0.6f * (float)Math.Sin(e.T * 7f + e.Id) * MathX.Clamp01(dist / 160f);
                    return dir * (float)Math.Cos(w) + dir.Orthogonal() * (float)Math.Sin(w);  // a darting zigzag
            }
            return dir;
        }

        void PenumbralSpecial(Enemy e, V2 tpos, float dt)
        {
            if (e.Type == EnemyType.Ember)
                EmberSparks(e, tpos, dt);
            else if (e.Type == EnemyType.Heliograph)
                HelioStep(e, tpos, dt);
            else if (e.Type == EnemyType.Moonward)
                e.Facing = MathX.Fmod(e.Facing + MathX.Tau / MoonwardTurn * dt, MathX.Tau);  // the moon goes round
            else if (e.Type == EnemyType.Gnomon)
                GnomonStep(e, tpos, dt);
            else if (e.Type == EnemyType.Nova)
                NovaStep(e, tpos, dt);
        }

        void NovaStep(Enemy e, V2 tpos, float dt)
        {
            if (e.Phase == 0)
            {
                if (tpos.DistanceTo(e.Pos) < NovaLight && e.SpawnT <= 0f && e.T > 0.5f)
                {
                    e.Phase = 1;
                    e.PhaseT = NovaFuse;
                    BroadcastFx(FxKind.Target, e.Pos, e.Def.Color, NovaBlastR, NovaFuse, Sfx.BossCharge);
                }
                return;
            }
            e.PhaseT -= dt;
            if (e.PhaseT > 0f)
                return;
            foreach (var p in Players.Values)
            {
                if (p.Downed || p.InvulnT > 0f || p.Pos.DistanceTo(e.Pos) > NovaBlastR + p.Def.HitR)
                    continue;
                if (p.ParryActive && ParryContact(p, e, NovaBlastDamage))
                    continue;  // a raised guard facing it takes it
                Damage(p, NovaBlastDamage, e.Def.Name + " (nova)");
            }
            BroadcastFx(FxKind.Boom, e.Pos, e.Def.Color, NovaBlastR, 0.4f, Sfx.Explode);
            e.LastHitBy = 0;  // no kill credit for going nova
            e.Hp = 0f;
        }

        void GnomonStep(Enemy e, V2 tpos, float dt)
        {
            e.PhaseT -= dt;
            switch (e.Phase)
            {
                case 0:
                    if (e.PhaseT <= 0f && e.T > 1.5f && tpos.DistanceTo(e.Pos) < GnomonRange)
                    {
                        // Plant: the slab turns a little to one side of you, so its shadow is a step away.
                        e.Strafe = e.Strafe >= 0f ? 1f : -1f;
                        e.Facing = (tpos - e.Pos).Angle + e.Strafe * GnomonOffset;
                        e.Phase = 1;
                        e.PhaseT = GnomonSwell;
                        BroadcastSound(Sfx.BossCharge);
                    }
                    break;
                case 1:
                    if (e.PhaseT <= 0f)
                    {
                        foreach (var p in Players.Values)
                            if (!p.Downed && p.InvulnT <= 0f && p.Pos.DistanceTo(e.Pos) < GnomonReach && !InGnomonShadow(e, p.Pos))
                                Damage(p, GnomonDamage, e.Def.Name + " (flash)");
                        BroadcastFx(FxKind.Ring, e.Pos - V2.FromAngle(e.Facing) * e.Radius, new Rgba(1f, 0.95f, 0.8f), GnomonReach, 0.35f, Sfx.Shockwave);
                        e.Phase = 2;
                        e.PhaseT = GnomonRecover;
                    }
                    break;
                case 2:
                    if (e.PhaseT <= 0f)
                    {
                        e.Phase = 0;
                        e.PhaseT = GnomonEvery / TraitFireMult(e);
                    }
                    break;
            }
        }

        bool moonwards;

        void FindMoonwards()
        {
            moonwards = false;
            foreach (var e in Enemies.Values)
                if (e.Type == EnemyType.Moonward && e.Hp > 0f && !IsSpawning(e))
                {
                    moonwards = true;
                    return;
                }
        }

        /// Host, in Collide: a Moonward's moon soaks any player shot that touches it.
        bool MoonwardStops(Bullet b)
        {
            if (Defs.IsEnemyShot(b.Kind))
                return false;
            foreach (var e in Enemies.Values)
                if (e.Type == EnemyType.Moonward && e.Hp > 0f && !IsSpawning(e) && b.Pos.DistanceTo(MoonwardMoon(e, e.Pos)) < MoonwardMoonR + b.Radius)
                {
                    BroadcastFx(FxKind.Spark, b.Pos, e.Def.Color, 10f, 0.15f, Sfx.None);
                    return true;
                }
            return false;
        }

        float duskT;

        /// Host, every tick: a Dusk Lobber's pool drains anyone in it.
        void TickPenumbral(float dt)
        {
            duskT -= dt;
            if (duskT > 0f)
                return;
            duskT = 0.25f;
            foreach (var pt in Patches.Values)
            {
                if (pt.Kind != PatchKind.Dusk || !pt.Active)
                    continue;
                foreach (var p in Players.Values)
                    if (!p.Downed && p.InvulnT <= 0f && p.Pos.DistanceTo(pt.Pos) < pt.Radius + p.Def.HitR)
                        DamageOverTime(p, DuskDps * 0.25f, "Dusk");
            }
        }

        /// An Ember's trail: a spark every EmberSparkEvery s while it's near a player, drifting back the way it came (away from you).
        void EmberSparks(Enemy e, V2 tpos, float dt)
        {
            if ((int)(e.T / EmberSparkEvery) == (int)((e.T - dt) / EmberSparkEvery))
                return;  // one every EmberSparkEvery s of its life
            if (tpos.DistanceTo(e.Pos) > EmberSparkRange || tpos.DistanceTo(e.Pos) < 1f)
                return;
            V2 back = (e.Pos - tpos).Normalized();
            var b = SpawnBullet(Defs.EmberSpark, e.Pos + back * e.Radius, back * EmberSparkDrift, 0f, EmberSparkLife, Defs.BulletDamage(Defs.EmberSpark), 0, false);
            b.Source = e.Type;
            b.Shooter = e.Id;
        }

        void HelioStep(Enemy e, V2 tpos, float dt)
        {
            e.PhaseT -= dt;
            float dist = tpos.DistanceTo(e.Pos);
            switch (e.Phase)
            {
                case 0:
                case 3:
                    if (e.Phase == 3 && e.PhaseT > 0f)
                        break;
                    if (e.T > 1.2f && dist < HelioRange + 90f)
                    {
                        e.Facing = (tpos - e.Pos).Angle;  // the mirror's line, locked on where you stand
                        e.Phase = 1;
                        e.PhaseT = HelioWarn;
                        BroadcastSound(Sfx.BossCharge);
                    }
                    else
                        e.Phase = 0;
                    break;
                case 1:
                    if (e.PhaseT <= 0f)
                    {
                        e.Phase = 2;
                        e.PhaseT = HelioBurn;
                        BroadcastFx(FxKind.Spark, e.Pos + V2.FromAngle(e.Facing) * (e.Radius + 6f), e.Def.Color, 22f, 0.2f, Sfx.Explode);
                    }
                    break;
                case 2:
                    foreach (var p in Players.Values)
                        if (!p.Downed && p.InvulnT <= 0f && HelioLaneDistance(e, p.Pos) < HelioWidth / 2f + p.Def.HitR)
                            Damage(p, HelioDamage, e.Def.Name + " (flare)");
                    if (e.PhaseT <= 0f)
                    {
                        e.Phase = 3;
                        e.PhaseT = HelioRest / TraitFireMult(e);
                    }
                    break;
            }
        }

        /// How far a point is from a Heliograph's lane (from it, along its Facing).
        public static float HelioLaneDistance(Enemy e, V2 p) => SegmentDistance(p, e.Pos, e.Pos + V2.FromAngle(e.Facing) * HelioLen);

        /// Returns true when it handled the shot.
        bool PenumbralFire(Enemy e, V2 tpos, float aim)
        {
            switch (e.Type)
            {
                case EnemyType.Heliograph:
                    e.FireT = 99f;  // its flare is its attack (HelioStep)
                    return true;
                case EnemyType.CoronaPriest:
                    e.FireT = PriestEvery;
                    CoronaRing(e, aim);
                    return true;
                case EnemyType.Sickle:
                    e.FireT = SickleEvery;
                    SickleThrow(e, tpos, e.Strafe >= 0f ? 1 : -1);
                    return true;
            }
            return false;
        }

        /// A crescent blade on a loop that leaves `side` of your line, passes through `at` and comes back round to it
        /// (the Eclipse's ThrowCrescent, smaller and slower).
        void SickleThrow(Enemy e, V2 at, int side)
        {
            float d = Math.Max(40f, at.DistanceTo(e.Pos)), r = Math.Max(SickleLoopMin, d / 2f + 30f);
            float th = (float)Math.Asin(Math.Min(1f, d / (2f * r)));
            float ang = (at - e.Pos).Angle - side * th, spin = side * SickleSpeed / r;
            V2 dir = V2.FromAngle(ang);
            var b = SpawnBullet(Defs.CrescentShot, e.Pos + dir * e.Radius, dir * SickleSpeed, spin, MathX.Tau * r / SickleSpeed, Defs.BulletDamage(Defs.CrescentShot), 0, false);
            b.Source = e.Type;
            b.Shooter = e.Id;
            BroadcastFx(FxKind.Spark, e.Pos + dir * (e.Radius + 6f), e.Def.Color, 14f, 0.15f, Sfx.Throw);
        }

        /// A ring of PriestSlots slots, one straight at you, with the slot beside it (the Strafe side) left open.
        void CoronaRing(Enemy e, float aim)
        {
            int gap = e.Strafe >= 0f ? 1 : PriestSlots - 1;
            for (int k = 0; k < PriestSlots; k++)
                if (k != gap)
                    EnemyShot(e, aim + k * MathX.Tau / PriestSlots, PriestShotSpeed, Defs.SmallShot);
            BroadcastFx(FxKind.Ring, e.Pos, e.Def.Color, 34f, 0.25f, Sfx.EnemyShot);
        }

        /// How much a bot should avoid this spot near a Penumbral Court enemy (BotBrain.cs): a Heliograph's line.
        float PenumbralBotDanger(Enemy e, V2 p, float hitR)
        {
            if (HelioLocked(e))
                return HelioLaneDistance(e, p) < HelioWidth / 2f + hitR + 30f ? 2.5f : 0f;
            if (e.Phase == 1 && e.Type == EnemyType.Gnomon)
                return p.DistanceTo(e.Pos) < GnomonReach && !InGnomonShadow(e, p, hitR) ? 2f : 0f;  // get into its shadow
            if (NovaLit(e))
                return p.DistanceTo(e.Pos) < NovaBlastR + hitR + 30f ? 2.5f : 0f;  // back off before it goes
            return 0f;
        }
    }
}
