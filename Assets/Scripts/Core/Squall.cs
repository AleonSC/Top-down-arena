using System;

namespace Arena
{
    /// The Storm Herald's faction, the Squall (Docs/StormFaction.md, mockup Docs/Mockups/storm-herald-faction.html; drawn in
    /// Game/SquallLook.cs). Each enemy previews one of the Herald's attacks. Steps 1-3 have all eight:
    ///   Dust Devil (the Gale's wind): a melee whirlwind that weaves in (a sway across your line, Enemy.Strafe its side).
    ///     A touch hurts and shoves you DevilShove px the way it was going; then it veers off for DevilVeer s (Phase 1).
    ///   Squall Gunner (Crosswind): keeps GunnerRange and strafes; every GunnerEvery s it fires three shots GunnerLead rad
    ///     upwind of you (its own wind's side, Enemy.Strafe, kept for its life) that curve back onto where you stood.
    ///   Stormcaller (Strikes): keeps CallerRange; every CallerEvery s it calls a strike on you, the Herald's warning circle
    ///     (a Hazard of kind HazardCaller: CallerRadius, CallerWarn) that lands for CallerDamage. Hazards are in the
    ///     snapshot and drawn like the Herald's, so it needs no new fields.
    ///   Grounder (Thunderclap): walks in behind a copper shield that turns slowly (GrounderTurn). Shots on its face
    ///     (GrounderArc either side of Facing) are grounded: blocked, and each adds one to its charge (Enemy.Phase). At
    ///     GrounderFull it lets go a Thunderclap: a ring of ClapShots with two ClapGap-shot gaps centred 70° either side of Facing
    ///     (ClapGapFrom), so the shooter in front steps sideways into one.
    ///   Pylon (Lightning rods): always a pair (SpawnEnemyAt brings the partner, Enemy.LinkId, like a Bound Wraith). The
    ///     leader (the lower Id) runs the cycle and its partner copies its Phase: 0 they walk to either side of you,
    ///     PylonSpread px out along the line between them; 1 planted and quiet (PylonIdle s), 2 the arc flickers (PylonWarn s), 3 it fires
    ///     along the line once (PylonDamage), shown for PylonLive s; PylonShots arcs, then they walk again. Kill either and
    ///     the line is gone. Its fire is the arc, so it never shoots.
    ///   Lineman (the Conductor): keeps LineRange; when it fires it hooks the player it aims at (Enemy.LinkId is that
    ///     player's id, Phase 1 for LineMark s). When the mark runs out the charge jumps to every other player within
    ///     LineRing px: each of them and the marked player take LineDamage and burst LineBurst shots. Alone, it only bursts.
    ///   Thunderhead (Downburst): a slow, heavy storm cloud that walks at you. Within HeadReach px, every HeadEvery s it
    ///     drops a row of HeadRow strikes (Hazards of kind HazardDownburst, HeadRadius) ahead of it toward you, HeadGap px
    ///     apart, landing one after another (HeadWarn, then HeadStep apart). Phase 1 while its row is coming (it flashes).
    ///   Ball Lightning (Ball lightning): the Herald's own orb (Herald.cs: it drifts at you, arcs to another within
    ///     BallArcRange, pops into 8 after BallLife s), made to work as a wave enemy too: slows and freezes hold it, and the
    ///     shots and arcs of one that didn't come from the Herald are its own, not the boss's. A wave's pay BallWaveXp.
    /// Step 4, the set piece: the Storm front (SquadForm.StormFront), six Stormcallers in a line with two Grounders in
    ///   front. While any of its callers lives, they stop calling strikes of their own and sweep a wall of strikes across
    ///   the players instead (TickStormFronts): a column of FrontSlots strikes, FrontSlotGap apart, every FrontEvery s, each
    ///   column FrontStep further along the line from the callers to the players' middle (taken when the sweep starts); two slots side by side are left open, and that gap
    ///   drifts a slot one way or the other as it goes, so you walk with it. A sweep starts FrontBack behind the players'
    ///   middle and runs FrontColumns columns; FrontRest s later the next one comes. Kill the callers to end it. The
    ///   strikes are Hazards (kind HazardCaller), so the wall needs nothing new on the wire.
    public partial class Game
    {
        public const float DevilShove = 120f, DevilVeer = 0.7f, DevilSway = 0.55f;
        public const float GunnerRange = 260f, GunnerEvery = 2.2f, GunnerLead = 0.45f, GunnerShotSpeed = 220f, GunnerFan = 0.06f;
        public const float CallerRange = 300f, CallerEvery = 2.8f, CallerRadius = 40f, CallerWarn = 1.1f;
        public const byte HazardCaller = 2;  // a Stormcaller's strike (Hazard.Kind; HazardLightning and HazardHand are in Lich.cs)
        public const byte HazardDownburst = 3;  // a Thunderhead's row
        public const float HeadReach = 380f, HeadEvery = 2.6f, HeadRadius = 30f, HeadGap = 55f, HeadAhead = 80f, HeadWarn = 1f, HeadStep = 0.2f;
        public const int HeadRow = 3, MaxBalls = 4;  // at most MaxBalls Ball Lightning a wave (CapBeams in Variety.cs)
        public const float BallWaveXp = 9f;  // a wave's Ball Lightning pays like a Shiv (the Herald's own pay their Def's 2)
        float CallerDamage => 20f;  // the difficulty's per-wave damage growth scales it, like the Herald's 25

        public const float GrounderTurn = 1.4f, GrounderArc = 1.15f, GrounderRange = 150f, ClapSpeed = 170f;
        public const int GrounderFull = 12, ClapShots = 18, ClapGap = 2, ClapGapFrom = 3;  // gaps: shots 3-4 and 14-15, centred 70° either side of its front
        public const float PylonSpread = 150f, PylonIdle = 0.9f, PylonWarn = 0.6f, PylonLive = 0.35f, PylonWalk = 1.6f, PylonMaxLine = 620f, PylonWidth = 14f;
        public const int PylonShots = 3, MaxPylonPairs = 2;  // at most this many pairs a wave (CapBeams in Variety.cs)
        public const float LineRange = 260f, LineEvery = 5.5f, LineMark = 2.6f, LineRing = 120f;
        public const int LineBurst = 8;
        float PylonDamage => 18f;  // like the Herald's rod arcs; the difficulty's per-wave damage growth scales these
        float LineDamage => 16f;
        public const byte PylonWalking = 0, PylonQuiet = 1, PylonFlicker = 2, PylonFiring = 3;

        public const float StormFrontGuard = 90f, FrontEvery = 0.42f, FrontStep = 60f, FrontSlotGap = 60f, FrontBack = 480f, FrontRest = 3.5f,
            FrontFirst = 2.5f, FrontRadius = 32f;
        public const int FrontSlots = 13, FrontColumns = 17, FrontGap = 2;

        /// Host: a Storm front's sweep, by squad id.
        public class StormFrontState
        {
            public V2 Fwd, Across, Origin;
            public int Column = -1, Gap;  // the next column (-1: resting), the first open slot
            public float T;                // until the next column, or the next sweep while resting
        }
        readonly System.Collections.Generic.Dictionary<int, StormFrontState> stormFronts = new System.Collections.Generic.Dictionary<int, StormFrontState>();

        void StartStormFront(int squad, V2 fwd, V2 across) =>
            stormFronts[squad] = new StormFrontState { Fwd = fwd.IsZero ? new V2(1f, 0f) : fwd.Normalized(), Across = across.IsZero ? new V2(0f, 1f) : across.Normalized(), T = FrontFirst };

        /// A Stormcaller sweeping a Storm front doesn't call strikes of its own.
        bool InStormFront(Enemy e) => e.Type == EnemyType.Stormcaller && e.SquadId != 0 && stormFronts.ContainsKey(e.SquadId);

        /// Host, every tick: each Storm front lays its next column, rests between sweeps, and ends with its last caller.
        void TickStormFronts(float dt)
        {
            if (stormFronts.Count == 0)
                return;
            var done = new System.Collections.Generic.List<int>();
            foreach (var kv in stormFronts)
            {
                int callers = 0;
                V2 from = V2.Zero;
                foreach (var e in Enemies.Values)
                    if (e.SquadId == kv.Key && e.Type == EnemyType.Stormcaller && e.Hp > 0f)
                    {
                        callers++;
                        from += e.Pos;
                    }
                if (callers == 0)
                {
                    done.Add(kv.Key);
                    continue;
                }
                var f = kv.Value;
                f.T -= dt;
                if (f.T > 0f)
                    continue;
                if (f.Column < 0)
                {
                    // A new sweep: from behind the players' middle, the gap somewhere near the middle.
                    var alive = AlivePlayersList();
                    if (alive.Count == 0)
                    {
                        f.T = 0.5f;
                        continue;
                    }
                    V2 mid = V2.Zero;
                    foreach (var p in alive)
                        mid += p.Pos;
                    mid = mid / alive.Count;
                    V2 at = (mid - from / callers).Normalized();  // from the callers, through you
                    if (!at.IsZero)
                    {
                        f.Fwd = at;
                        f.Across = at.Orthogonal();
                    }
                    f.Origin = mid - f.Fwd * FrontBack;
                    f.Column = 0;
                    f.Gap = rng.Next(FrontSlots / 2 - 2, FrontSlots / 2 + 1);
                    BroadcastSound(Sfx.BossCharge);
                }
                V2 col = f.Origin + f.Fwd * (f.Column * FrontStep);
                for (int j = 0; j < FrontSlots; j++)
                    if (j < f.Gap || j >= f.Gap + FrontGap)
                        AddHazard(col + f.Across * ((j - (FrontSlots - 1) / 2f) * FrontSlotGap), FrontRadius, CallerWarn).Kind = HazardCaller;
                // The gap drifts a slot one way or the other (or stays), never off the edge.
                f.Gap = Math.Max(1, Math.Min(FrontSlots - FrontGap - 1, f.Gap + rng.Next(-1, 2)));
                if (++f.Column >= FrontColumns)
                {
                    f.Column = -1;
                    f.T = FrontRest;
                }
                else
                    f.T = FrontEvery;
            }
            foreach (int id in done)
                stormFronts.Remove(id);
        }

        public static bool IsSquall(EnemyType t) => t >= EnemyType.DustDevil && t <= EnemyType.Thunderhead;
        readonly System.Collections.Generic.Dictionary<int, int> pylonArcs = new System.Collections.Generic.Dictionary<int, int>();  // host: arcs fired this plant, by leader
        bool pylonPairing;  // host: SpawnEnemyAt is making a partner

        /// SpawnEnemyAt: a Pylon brings its partner beside it.
        void PairPylon(Enemy e, float spawnT, int parent, bool elite)
        {
            if (e.Type != EnemyType.Pylon || pylonPairing || e.LinkId != 0)
                return;
            pylonPairing = true;
            var b = SpawnEnemyAt(EnemyType.Pylon, e.Pos + V2.FromAngle(e.Ang + MathX.Pi / 2f) * 90f, spawnT, parent, elite);
            pylonPairing = false;
            e.LinkId = b.Id;
            b.LinkId = e.Id;
            b.SquadId = e.SquadId;
        }

        /// A Pylon's partner, alive (null: none).
        public Enemy PylonPartner(Enemy e)
        {
            Enemy o;
            return e.Type == EnemyType.Pylon && e.LinkId != 0 && Enemies.TryGetValue(e.LinkId, out o) && o.Hp > 0f && o.Type == EnemyType.Pylon ? o : null;
        }

        /// The leader of a pair draws and fires the line (the other copies its Phase).
        public static bool PylonLeads(Enemy e, Enemy partner) => partner != null && e.Id < partner.Id;

        /// How far a point is from a pair's line (a huge number when there's no line).
        public static float PylonLineDistance(Enemy e, Enemy partner, V2 p) =>
            partner == null || e.Pos.DistanceTo(partner.Pos) > PylonMaxLine ? 1e9f : SegmentDistance(p, e.Pos, partner.Pos);


        /// Where a Pylon walks to: either side of you, along the line the pair already makes (so the spots hold still as they walk).
        V2 PylonSpot(Enemy e, Enemy partner, V2 tpos)
        {
            V2 axis = (e.Pos - partner.Pos).Normalized();
            if (axis.IsZero)
                axis = PylonLeads(e, partner) ? new V2(0f, -1f) : new V2(0f, 1f);
            return ClampToArena(tpos + axis * PylonSpread, 40f);
        }


        /// The Grounder's shield turns slowly toward you (EnemyAI's facing), so you can get round it.
        void GrounderFace(Enemy e, float want, float dt)
        {
            float diff = MathX.Fmod(want - e.Facing + MathX.Pi, MathX.Tau) - MathX.Pi;
            float step = GrounderTurn * dt;
            e.Facing += Math.Abs(diff) < step ? diff : Math.Sign(diff) * step;
        }

        public static bool GrounderFaces(Enemy e, V2 from) =>
            e.Type == EnemyType.Grounder && Math.Abs(MathX.Fmod((from - e.Pos).Angle - e.Facing + MathX.Pi, MathX.Tau) - MathX.Pi) < GrounderArc;

        /// A player's shot grounded on a Grounder's face (Combat.cs, after BlocksShot): it charges, and full, it claps.
        void GroundShot(Enemy e)
        {
            if (e.Type != EnemyType.Grounder)
                return;
            BroadcastFx(FxKind.Spark, e.Pos + V2.FromAngle(e.Facing) * (e.Radius + 8f), new Rgba(0.82f, 0.54f, 0.29f), 12f, 0.15f, Sfx.None);
            if (++e.Phase < GrounderFull)
                return;
            e.Phase = 0;
            Thunderclap(e);
        }

        /// A ring of ClapShots with two gaps of ClapGap shots either side of its front: the shooter in front steps sideways into one.
        void Thunderclap(Enemy e)
        {
            float step = MathX.Tau / ClapShots;
            for (int i = 0; i < ClapShots; i++)
            {
                bool gap = i >= ClapGapFrom && i < ClapGapFrom + ClapGap || i > ClapShots - ClapGapFrom - ClapGap && i <= ClapShots - ClapGapFrom;
                if (!gap)
                    EnemyShot(e, e.Facing + i * step, ClapSpeed, Defs.SmallShot);
            }
            BroadcastFx(FxKind.Ring, e.Pos, new Rgba(0.56f, 0.82f, 1f), 70f, 0.35f, Sfx.BossPulse);
        }

        void TickPylon(Enemy e, V2 tpos, float dt)
        {
            var o = PylonPartner(e);
            if (o == null)
            {
                e.Phase = PylonWalking;
                return;
            }
            if (!PylonLeads(e, o))
            {
                e.Phase = o.Phase;  // the leader runs the cycle
                return;
            }
            e.PhaseT -= dt;
            switch (e.Phase)
            {
                case PylonWalking:
                    bool there = e.Pos.DistanceTo(PylonSpot(e, o, tpos)) < 30f && o.Pos.DistanceTo(PylonSpot(o, e, tpos)) < 30f;
                    if (e.T > 1.2f && (there || e.PhaseT < -5f) && e.PhaseT <= 0f)
                    {
                        e.Phase = PylonQuiet;
                        e.PhaseT = PylonIdle;
                        pylonArcs[e.Id] = 0;
                        BroadcastFx(FxKind.Ring, e.Pos, new Rgba(0.56f, 0.82f, 1f), 30f, 0.25f, Sfx.Clang);
                    }
                    break;
                case PylonQuiet:
                    if (e.PhaseT <= 0f)
                    {
                        e.Phase = PylonFlicker;
                        e.PhaseT = PylonWarn;
                        BroadcastSound(Sfx.BossCharge);
                    }
                    break;
                case PylonFlicker:
                    if (e.PhaseT <= 0f)
                    {
                        e.Phase = PylonFiring;
                        e.PhaseT = PylonLive;
                        foreach (var p in Players.Values)
                            if (!p.Downed && p.InvulnT <= 0f && PylonLineDistance(e, o, p.Pos) < PylonWidth / 2f + p.Def.HitR)
                                Damage(p, PylonDamage, "Pylon (arc)");
                        BroadcastFx(FxKind.Bolt, e.Pos, o.Pos, new Rgba(0.56f, 0.82f, 1f), 0f, PylonLive, Sfx.SpellZap);
                    }
                    break;
                case PylonFiring:
                    if (e.PhaseT <= 0f)
                    {
                        int n;
                        pylonArcs.TryGetValue(e.Id, out n);
                        pylonArcs[e.Id] = ++n;
                        if (n >= PylonShots)
                        {
                            e.Phase = PylonWalking;
                            e.PhaseT = PylonWalk;  // walk at least this long before planting again
                        }
                        else
                        {
                            e.Phase = PylonQuiet;
                            e.PhaseT = PylonIdle;
                        }
                    }
                    break;
            }
            o.Phase = e.Phase;
        }

        /// A Lineman's mark runs out: the charge jumps to every ally in the ring; all of them are hurt and burst shots. Alone,
        /// the marked player only bursts.
        void Conduct(Enemy e, Player marked)
        {
            e.Phase = 0;
            e.LinkId = 0;
            var hit = new System.Collections.Generic.List<Player> { marked };
            foreach (var p in Players.Values)
                if (p != marked && !p.Downed && p.Pos.DistanceTo(marked.Pos) < LineRing)
                    hit.Add(p);
            var col = new Rgba(1f, 0.82f, 0.4f);
            foreach (var p in hit)
            {
                if (hit.Count > 1)
                {
                    if (p.InvulnT <= 0f)
                        Damage(p, LineDamage, "Lineman (chain)");
                    if (p != marked)
                        BroadcastFx(FxKind.Bolt, marked.Pos, p.Pos, col, 0f, 0.3f, Sfx.SpellZap);
                }
                for (int i = 0; i < LineBurst; i++)
                {
                    float ang = i * MathX.Tau / LineBurst + 0.2f;
                    V2 d = V2.FromAngle(ang);
                    var b = SpawnBullet(Defs.SmallShot, p.Pos + d * 18f, d * 200f, 0f, 3f, Defs.BulletDamage(Defs.SmallShot), 0, false);
                    b.Source = e.Type;
                    b.Shooter = e.Id;
                    b.Look = b.Sent.Look = LookFor(e, ang, 0f);
                    NoteVolley(e, b);
                }
            }
            BroadcastFx(FxKind.Ring, marked.Pos, col, LineRing, 0.3f, Sfx.SpellZap);
        }

        /// The Dust Devil's touch (from the contact loop, after its damage): shove the player the way it was going, and veer off.
        void DevilTouched(Player p, Enemy e)
        {
            if (e.Type != EnemyType.DustDevil || p.Downed)
                return;
            KnockPlayer(p, p.Pos - e.Pos, DevilShove);  // (it was coming straight at you)
            e.Phase = 1;
            e.PhaseT = DevilVeer;
            BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.62f, 0.91f, 0.85f), 30f, 0.25f, Sfx.None);
        }


        /// Three shots that leave GunnerLead upwind of you (its Strafe side) and curve back to cross where you stood: a shot
        /// turning at a steady rate runs along a circle, so to cross the chord d after setting off at angle s to it, it turns
        /// 2s over an arc of d·s/sin(s) (as the Acolyte's orbs, Infall.cs). The two outer shots fan GunnerFan either side.
        void GunnerVolley(Enemy e, V2 tpos, float aim)
        {
            float d = Math.Max(120f, Math.Min(600f, tpos.DistanceTo(e.Pos) - e.Radius));
            float side = e.Strafe >= 0f ? 1f : -1f, s = GunnerLead;
            float arc = d * s / (float)Math.Sin(s), time = arc / GunnerShotSpeed, turn = -side * 2f * s / time;
            for (int k = -1; k <= 1; k++)
            {
                float ang = aim + side * s + k * GunnerFan;
                V2 dir = V2.FromAngle(ang);
                var b = SpawnBullet(Defs.SmallShot, e.Pos + dir * e.Radius, dir * GunnerShotSpeed, turn, time * 1.6f + 0.4f, Defs.BulletDamage(Defs.SmallShot), 0, false);
                b.Source = e.Type;
                b.Shooter = e.Id;
                b.Look = b.Sent.Look = LookFor(e, ang, turn);
                NoteVolley(e, b);
            }
            BroadcastFx(FxKind.Spark, e.Pos + V2.FromAngle(aim + side * s) * (e.Radius + 8f), new Rgba(0.56f, 0.82f, 1f), 14f, 0.15f, Sfx.EnemyShot);
        }

        /// A Thunderhead: every HeadEvery s while you're within HeadReach, a row of strikes ahead of it toward you.
        void TickThunderhead(Enemy e, V2 tpos, float dt)
        {
            e.PhaseT -= dt;
            if (e.Phase == 1 && e.PhaseT <= 0f)
            {
                e.Phase = 0;
                e.PhaseT = HeadEvery - HeadWarn - HeadStep * (HeadRow - 1);
            }
            if (e.Phase != 0 || e.PhaseT > 0f || e.T < 1f || tpos.DistanceTo(e.Pos) > HeadReach || e.FrozenT > 0f || e.DazeT > 0f)
                return;
            V2 dir = (tpos - e.Pos).Normalized();
            for (int k = 0; k < HeadRow; k++)
                AddHazard(e.Pos + dir * (e.Radius + HeadAhead + k * HeadGap), HeadRadius, HeadWarn + k * HeadStep).Kind = HazardDownburst;
            e.Phase = 1;
            e.PhaseT = HeadWarn + HeadStep * (HeadRow - 1);
            BroadcastSound(Sfx.BossCharge);
        }

        /// A Stormcaller's or a Thunderhead's strike landing (TickHazards): the Herald's bolt, its own damage and name.
        void CallerStrike(Hazard h)
        {
            string who = h.Kind == HazardDownburst ? "Thunderhead (downburst)" : "Stormcaller (lightning)";
            foreach (var p in Players.Values)
                if (!p.Downed && p.InvulnT <= 0f && p.Pos.DistanceTo(h.Pos) < h.Radius)
                    Damage(p, CallerDamage, who);
            var color = new Rgba(1f, 0.85f, 0.5f);
            BroadcastFx(FxKind.Bolt, new V2(h.Pos.X + Rand(-40f, 40f), h.Pos.Y - 260f), h.Pos, color, 0f, 0.22f, Sfx.SpellZap);
            BroadcastFx(FxKind.Boom, h.Pos, color, h.Radius, 0.3f, Sfx.None);
        }
        /// (moved from SquallMove, DustDevil)
        V2 SquallMove_DustDevil(Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed)
        {
            if (e.Phase == 1)
            {
                // Veering off after a shove, then back in.
                e.PhaseT -= dt;
                if (e.PhaseT <= 0f)
                    e.Phase = 0;
                return (-dir + dir.Orthogonal() * e.Strafe * 1.2f).Normalized();
            }
            // A weave: a sway across your line that dies away up close.
            float k = MathX.Clamp01((dist - 60f) / 200f);
            float a = DevilSway * k * (float)Math.Sin(e.T * 4.2f + e.Id);
            return dir * (float)Math.Cos(a) + dir.Orthogonal() * (float)Math.Sin(a);
        }

        /// (moved from SquallMove, SquallGunner)
        V2 SquallMove_SquallGunner(Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed)
        {
            return KeepDistance(dir, dist, GunnerRange) + dir.Orthogonal() * e.Strafe * 0.5f;
        }

        /// (moved from SquallMove, Stormcaller)
        V2 SquallMove_Stormcaller(Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed)
        {
            return KeepDistance(dir, dist, CallerRange) + dir.Orthogonal() * e.Strafe * 0.35f;
        }

        /// (moved from SquallMove, Grounder)
        V2 SquallMove_Grounder(Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed)
        {
            return KeepDistance(dir, dist, GrounderRange);
        }

        /// (moved from SquallMove, Lineman)
        V2 SquallMove_Lineman(Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed)
        {
            return KeepDistance(dir, dist, LineRange) + dir.Orthogonal() * e.Strafe * 0.5f;
        }

        /// (moved from SquallMove, Thunderhead)
        V2 SquallMove_Thunderhead(Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed)
        {
            return dir;  // straight at you, slowly
        }

        /// (moved from SquallMove, Pylon)
        V2 SquallMove_Pylon(Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed)
        {
            var o = PylonPartner(e);
            if (o == null)
                return KeepDistance(dir, dist, 200f);  // alone: no line, it just follows
            if (e.Phase != PylonWalking)
                return V2.Zero;  // planted
            V2 spot = PylonSpot(e, o, tpos);
            float left = spot.DistanceTo(e.Pos);
            return left < 8f ? V2.Zero : (spot - e.Pos) / left;
        }

        /// (moved from SquallSpecial, Pylon)
        void SquallSpecial_Pylon(Enemy e, V2 tpos, float dt)
        {
            TickPylon(e, tpos, dt);
        }

        /// (moved from SquallSpecial, Thunderhead)
        void SquallSpecial_Thunderhead(Enemy e, V2 tpos, float dt)
        {
            TickThunderhead(e, tpos, dt);
        }

        /// (moved from SquallSpecial, Lineman)
        void SquallSpecial_Lineman(Enemy e, V2 tpos, float dt)
        {
            do
            {
                if (e.Phase != 1)
                    break;
                e.PhaseT -= dt;
                Player marked;
                if (!Players.TryGetValue(e.LinkId, out marked) || marked.Downed)
                {
                    e.Phase = 0;  // its mark is gone
                    e.LinkId = 0;
                }
                else if (e.PhaseT <= 0f)
                    Conduct(e, marked);
            } while (false);  // (a break leaves the case, as it left the switch)
        }

        /// (moved from SquallFire, SquallGunner)
        bool SquallFire_SquallGunner(Enemy e, V2 tpos, float aim)
        {
            e.FireT = GunnerEvery;
            GunnerVolley(e, tpos, aim);
            return true;
        }

        /// (moved from SquallFire, Lineman)
        bool SquallFire_Lineman(Enemy e, V2 tpos, float aim)
        {
            e.FireT = LineEvery;
            Player best = null;
            foreach (var p in Players.Values)
                if (!p.Downed && (best == null || p.Pos.DistanceTo(tpos) < best.Pos.DistanceTo(tpos)))
                    best = p;
            if (best == null || e.Phase == 1)
                return true;
            e.Phase = 1;
            e.PhaseT = LineMark;
            e.LinkId = best.Id;
            BroadcastFx(FxKind.Bolt, e.Pos + V2.FromAngle(aim) * (e.Radius + 10f), best.Pos, new Rgba(0.56f, 0.82f, 1f), 0f, 0.25f, Sfx.SpellZap);
            return true;
        }

        /// (moved from SquallFire, Stormcaller)
        bool SquallFire_Stormcaller(Enemy e, V2 tpos, float aim)
        {
            e.FireT = CallerEvery;
            if (InStormFront(e))
                return true;  // it's sweeping the front with the others
            AddHazard(tpos, CallerRadius, CallerWarn).Kind = HazardCaller;
            BroadcastFx(FxKind.Spark, e.Pos + new V2(0f, -e.Radius - 8f), new Rgba(1f, 0.82f, 0.4f), 16f, 0.2f, Sfx.SpellZap);
            return true;
        }

        // --- Brains (Docs/EnemyBehaviours.md step 8d) ------------------------------------------------------------------

        /// A Grounder keeps its range and turns its shield slowly (GrounderFace).
        sealed class GrounderMove : MovePart
        {
            public override V2 Move(Game g, Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed) => g.SquallMove_Grounder(e, tpos, dir, dist, dt, ref speed);
            public override bool Turn(Game g, Enemy e, float want, V2 tpos, float dt)
            {
                g.GrounderFace(e, want, dt);
                return true;
            }
        }

        /// A Grounder's shield: blocks from the front (GrounderFaces); a player's shot on it charges it (GroundShot).
        sealed class GrounderGuard : GuardPart
        {
            public override bool Blocks(Game g, Enemy e, V2 from) => GrounderFaces(e, from);
            public override void Shot(Game g, Enemy e, Bullet b) => g.GroundShot(e);
        }

        static void SquallBrains(Brain[] t)
        {
            t[(int)EnemyType.DustDevil] = new Brain { Move = new CodeMove((Game g, Enemy e, V2 p, V2 d, float di, float dt, ref float sp) => g.SquallMove_DustDevil(e, p, d, di, dt, ref sp)) };
            t[(int)EnemyType.SquallGunner] = new Brain { Move = new CodeMove((Game g, Enemy e, V2 p, V2 d, float di, float dt, ref float sp) => g.SquallMove_SquallGunner(e, p, d, di, dt, ref sp)), Fire = new CodeFire((g, e, p, a) => g.SquallFire_SquallGunner(e, p, a)) };
            t[(int)EnemyType.Stormcaller] = new Brain { Move = new CodeMove((Game g, Enemy e, V2 p, V2 d, float di, float dt, ref float sp) => g.SquallMove_Stormcaller(e, p, d, di, dt, ref sp)), Fire = new CodeFire((g, e, p, a) => g.SquallFire_Stormcaller(e, p, a)) };
            t[(int)EnemyType.Lineman] = new Brain { Move = new CodeMove((Game g, Enemy e, V2 p, V2 d, float di, float dt, ref float sp) => g.SquallMove_Lineman(e, p, d, di, dt, ref sp)), Special = new CodeSpecial((g, e, p, dt) => g.SquallSpecial_Lineman(e, p, dt)), Fire = new CodeFire((g, e, p, a) => g.SquallFire_Lineman(e, p, a)) };
            t[(int)EnemyType.Thunderhead] = new Brain { Move = new CodeMove((Game g, Enemy e, V2 p, V2 d, float di, float dt, ref float sp) => g.SquallMove_Thunderhead(e, p, d, di, dt, ref sp)), Special = new CodeSpecial((g, e, p, dt) => g.SquallSpecial_Thunderhead(e, p, dt)) };
            t[(int)EnemyType.Grounder] = new Brain { Move = new GrounderMove(), Guard = new GrounderGuard(), Fire = new HoldFire()  /* the arc, the clap: not shots of its own */ };
            t[(int)EnemyType.Pylon] = new Brain { Move = new CodeMove((Game g, Enemy e, V2 p, V2 d, float di, float dt, ref float sp) => g.SquallMove_Pylon(e, p, d, di, dt, ref sp), e => true), Special = new CodeSpecial((g, e, p, dt) => g.SquallSpecial_Pylon(e, p, dt)), Fire = new HoldFire()  /* the arc, the clap: not shots of its own */ };
        }
    }
}
