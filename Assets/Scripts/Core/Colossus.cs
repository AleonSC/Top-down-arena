using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Colossus's phase of the fight, by its health: three phases, then a last stand.
    public enum ColStage : byte { Awakening, Crumbling, MoltenCore, LastStand }

    /// What it's doing now. Each phase loops its turns, one attack at a time, with breathers (Rest) between.
    public enum ColTurn : byte { Rest, Stomps, Boulders, Fissures, Charge, Rockslide, Aftershocks, LavaStomps, Meteors, CaveIn, Last }

    /// A stomp ring spreading out from where it stomped. A Stomper's (QuarryFaction.cs) is push blocks (two rows, like
    /// the Warden's gates) with one or two gaps, each a short step to the side of a player: step into a gap and it passes
    /// you by; caught, it passes over you (one hit, and you're behind it); walk into it from inside and it shoves you back
    /// (ColossusPush). The Colossus's (Orbs) is a ring of ColRingOrbs indestructible orbs like the Singularity's disk,
    /// with a few small openings (Gaps of them, at Gap1-Gap4): touch an orb and it hurts (once a ring); nothing shoves.
    public class StompRing
    {
        public V2 Center;
        public float Radius, Warn, Gap1, Gap2, Gap3, Gap4;  // replicated; only the first Gaps are used
        public int Gaps = 1;
        public bool Orbs;  // replicated: the Colossus's ring of orbs
        public float Rot;  // replicated: where its first orb sits (an Orbs ring)
        public readonly HashSet<int> Hit = new HashSet<int>();      // host: players it has hurt (once each)
        public readonly HashSet<int> Outside = new HashSet<int>();  // host: players seen in front of it (so a crossing can be told)
    }

    /// Something thrown or falling: a boulder, a meteor or a cave-in pillar. Its shadow grows until it lands.
    public class ColDrop
    {
        public const byte Boulder = 0, Meteor = 1, Pillar = 2, LastMeteor = 3;
        public V2 Pos, From;
        public float T, Dur, Radius;
        public byte Kind;
        public bool Hot => Kind == Meteor || Kind == LastMeteor;
    }

    /// A fissure racing out from the Colossus's feet; once it stops it waits, then erupts along its length. The
    /// Colossus's stay on the floor as scars (Dormant) until the phase ends, and wake to erupt again (Fissures with five
    /// up); a Driller's or Geomancer's (QuarryFaction.cs) only race and erupt once.
    public class ColCrack
    {
        public const byte Racing = 0, Waiting = 1, Erupting = 2, Dormant = 3;
        public V2 From;
        public float Ang, Len, Max, Wait;
        public int Seed;
        public byte State;  // replicated (the Colossus's): Racing, Waiting, Erupting or Dormant
        public readonly HashSet<int> Hit = new HashSet<int>();  // host: players this eruption has hurt (once each)
    }

    /// A fallen armour plate (Crumbling): cover that stops bullets both ways and crumbles after enough enemy hits.
    public class ColSlab
    {
        public V2 Pos;
        public float W, H;
        public int Hp;
    }

    /// A cave-in pillar: cover for a few seconds, then it shatters into shots.
    public class ColPillar
    {
        public V2 Pos;
        public float T;
    }

    /// Everything about the Colossus's fight its body doesn't carry. The host runs it; clients get it in the snapshot
    /// (SyncColossus) to draw the rings, landings, fissures, plates, pillars, the charge line and the phase change.
    public class ColossusState : BossFight<ColStage, ColTurn>
    {
        public float DazeT, ChargeWarn, ChargeLeft, ChargeDir;
        public bool Stagger;  // replicated: kneeling, stunned, at the end of a phase; hits fill Break instead of hurting it
        public float Break;   // replicated: 0-1, how broken (at 1 its shell blasts off and TransTo starts)
        public float HoldT;   // replicated: lifting a boulder over its head (seconds left before the throw)
        public int HoldSide = 1;  // replicated: the hand it's in (-1 or 1, the side in Facing's local y)
        public V2 HoldAim;    // replicated: where the held boulder will land (it follows its target while it winds up)
        public readonly List<StompRing> Rings = new List<StompRing>();
        public readonly List<ColDrop> Drops = new List<ColDrop>();
        public readonly List<ColCrack> Cracks = new List<ColCrack>();
        public readonly List<ColSlab> Slabs = new List<ColSlab>();
        public readonly List<ColPillar> Pillars = new List<ColPillar>();

        // Host only.
        public int SlideEdge, SlideGap, NextTarget, StompSide = 1, HoldTarget;
        public bool Cycling;  // a Fissures turn waking the five scars on the floor in turn
        public float GapA, ShockA, MeteorT, LavaTickT;
        public readonly Dictionary<int, V2> LastPos = new Dictionary<int, V2>(), Vel = new Dictionary<int, V2>();
    }

    /// The Colossus (wave 25): a fight in three phases by its health, like the Warden's, then a last stand. It breaks
    /// down as it goes: armour, then shell, then a molten core, and shrinks each time (ColSizes: three times its size
    /// in Awakening, twice in Crumbling, its size in Molten Core, 0.7 in the Last Stand). At the end of each phase it
    /// kneels, staggered (Stagger): it can't act, and hits fill its Break meter instead of hurting it; broken, its shell
    /// blasts off, it drops to the next size and the next phase starts (TransT).
    ///   Awakening (100-65%): it walks at you 200 px off, its rock volley between turns; Stomps (rings of orbs with
    ///     a few small openings), Boulders (lifted over its head, then lobbed where you're heading), Fissures (cracks
    ///     that erupt and stay as scars; with five up they all wake and erupt again in turn).
    ///   Crumbling (65-30%): four armour plates fall as cover that blocks shots both ways, and it walks faster;
    ///     Charge (a plate in the way breaks and dazes it: +50% damage), Rockslide (rows of rocks from an edge),
    ///     Aftershocks (rings of shots whose gaps spiral).
    ///   Molten Core (30-10%): everything leaves lava; Lava stomps, Meteors, Cave-in (pillars that shatter).
    ///   Last stand (below 10%): it kneels in the middle; stomps with two gaps, its volley, a meteor every 3 s.
    public partial class Game
    {
        const float ColCrumbleAt = 0.65f, ColMoltenAt = 0.3f, ColLastAt = 0.1f;
        public const float ColTransTime = 2f;
        public static readonly float[] ColSizes = { 3f, 2f, 1f, 0.7f };  // its radius by phase, times its Def's
        public const float ColBreakShare = 0.05f;  // the damage that breaks a stagger: this share of its full health
        // Its stomp rings: ColRingOrbs orbs like the Singularity's disk, spreading at ColOrbSpeed, with small openings
        // ColOpenHalf radians to each side (never under ColOpenMinArc across).
        public const int ColRingOrbs = 120;
        public const float ColOrbR = 9f, ColOrbSpeed = 125f, ColOpenHalf = 0.12f, ColOpenMinArc = 70f;
        public const float ColHoldTime = 1.2f, ColBoulderFlight = 1.35f, ColBoulderR = 70f;  // a boulder: held over its head, then lobbed
        public const int ColMaxCracks = 5;
        public const float CrackErupt = 1.2f, CrackHurtR = 24f, ColCrackDamage = 22f;
        // Stomp rings: push blocks like the Warden's gates (GateRows, GateRowGap, GateBulletR), spreading out.
        public const float RingSpeed = 150f, RingWarn = 0.45f, RingGapHalf = 0.33f;  // the gap is 0.66 rad (about 38 degrees) wide...
        public const float RingGapMinArc = 100f;  // ...but never less than 100 px across, however close to its middle
        public const float StompStep = 130f;      // a gap is placed about this far to one side of the player it's for
        public const float StompDamage = 20f;     // a ring's hit (the difficulty's damage growth scales it: about 34 at wave 25 on Normal)
        public const float ColKeep = 200f, ColDazeTime = 1.5f, ColDazeMult = 1.5f;
        public const float ColChargeWarn = 1f, ColChargeSpeed = 700f, ColChargeLength = 1100f;
        public const float SlabW = 110f, SlabH = 34f;
        const int SlabHp = 40;
        public const float PillarR = 28f, PillarLife = 3f;
        public const float CrackSpeed = 600f, CrackMax = 1800f, CrackWait = 0.8f, CrackStep = 45f;
        const float LavaDps = 20f, LavaLife = 6f, LavaWarn = 0.5f;
        public static readonly V2[] SlabSpots = { new V2(520f, 290f), new V2(1080f, 290f), new V2(430f, 600f), new V2(1170f, 600f) };

        static readonly ColTurn[][] ColTurns =
        {
            new[] { ColTurn.Stomps, ColTurn.Rest, ColTurn.Boulders, ColTurn.Rest, ColTurn.Fissures, ColTurn.Rest },
            new[] { ColTurn.Charge, ColTurn.Rest, ColTurn.Rockslide, ColTurn.Rest, ColTurn.Aftershocks, ColTurn.Rest },
            new[] { ColTurn.LavaStomps, ColTurn.Rest, ColTurn.Meteors, ColTurn.Rest, ColTurn.CaveIn, ColTurn.Rest },
            new[] { ColTurn.Last },
        };
        static readonly float[][] ColTimes =
        {
            new[] { 6f, 1f, 7f, 1f, 7f, 1.5f },
            new[] { 6f, 1f, 7f, 1f, 6f, 1.5f },
            new[] { 6f, 1f, 6f, 1f, 6f, 1.5f },
            new[] { 999f },
        };

        ColossusState ColOf(Enemy e) => BossOf<ColossusState>(e);

        public static bool ColossusImmune(Enemy e) => e.Type == EnemyType.Colossus && e.Colossus != null && e.Colossus.TransT > 0f;

        /// Its radius: ColSizes by phase (the next phase's once its shell has blasted off).
        public static float ColRadius(Enemy e)
        {
            var c = e.Colossus;
            var st = c == null ? ColStage.Awakening : c.TransT > 0f ? c.TransTo : c.Stage;
            return Defs.Get(EnemyType.Colossus).Radius * ColSizes[(int)st];
        }

        /// A hit while it's staggered: it fills the Break meter instead of hurting it (HurtEnemy, Combat.cs).
        bool ColossusBreakHit(Enemy e, float dmg)
        {
            if (e.Type != EnemyType.Colossus || e.Colossus == null || !e.Colossus.Stagger)
                return false;
            e.Colossus.Break = Math.Min(1f, e.Colossus.Break + dmg / Math.Max(1f, e.MaxHp * ColBreakShare));
            e.Flash = 0.06f;
            return true;
        }
        public static bool ColossusDazed(Enemy e) => e.Type == EnemyType.Colossus && e.Colossus != null && e.Colossus.DazeT > 0f;

        /// The Colossus on the field, if any (for the pushes on each player's own game).
        Enemy TheColossus()
        {
            foreach (var e in EnemiesOf(EnemyType.Colossus))  // (EnemyIndex.cs)
                if (e.Colossus != null && !IsSpawning(e) && StillIn(e))
                    return e;
            return null;
        }

        // Phase (replicated, for cues and drawing): the turn, or ColShift while changing phase.
        public const byte ColShift = 50;

        // --- Moving ---------------------------------------------------------------------------------

        /// Walks at its target, 200 px off, faster once its armour is gone. Holds still while it aims a charge or
        /// is dazed, runs along the charge, and kneels in the middle for the last stand.
        V2 ColossusMove(Enemy e, V2 dir, float dist, ref float speed)
        {
            var c = ColOf(e);
            V2 middle = ArenaMid;
            if (c.TransT > 0f || c.DazeT > 0f || c.ChargeWarn > 0f || c.Stagger || c.HoldT > 0f)
                return V2.Zero;
            if (c.ChargeLeft > 0f)
            {
                speed = ColChargeSpeed;
                return V2.FromAngle(c.ChargeDir);
            }
            if (c.Stage == ColStage.LastStand)
            {
                speed = 420f;
                return e.Pos.FartherThan(middle, 6f) ? (middle - e.Pos).Normalized() : V2.Zero;
            }
            speed = c.Stage == ColStage.Crumbling ? 55f : c.Stage == ColStage.MoltenCore ? 40f : e.Def.Speed;
            return KeepDistance(dir, dist, ColKeep + e.Radius - e.Def.Radius);  // (its edge stays as far off at every size)
        }

        // --- The fight (host) -----------------------------------------------------------------------

        void ColossusSpecial(Enemy e, V2 tpos, float dt)
        {
            var c = ColOf(e);
            if (c.Step < 0)
                EnterColStage(e, ColStage.Awakening);
            float frac = e.MaxHp > 0f ? e.Hp / e.MaxHp : 1f;
            e.Radius = ColRadius(e);
            TrackPlayers(c, dt);
            TickColHazards(e, c, dt);

            if (c.Stagger)
            {
                e.Phase = ColShift;
                if (c.Break >= 1f)
                    BreakColShell(e, c);
                return;
            }
            if (c.TransT > 0f)
            {
                c.TransT -= dt;
                if (c.TransT <= 0f)
                {
                    c.TransT = 0f;
                    EnterColStage(e, c.TransTo);
                    StageStartBurst(e, ColStageColor(c.Stage));
                }
                e.Phase = ColShift;
                return;
            }
            BossStage to;  // its stage table (BossStages.cs): each change is a stagger you break (into the Molten Core, a charge finishes first)
            if (NextStage(e.Type, (byte)c.Stage, frac, out to) && (to.Stage != (byte)ColStage.MoltenCore || c.ChargeLeft <= 0f))
            {
                StartColStagger(e, (ColStage)to.Stage, to.At);
                return;
            }

            c.TurnT += dt;
            c.DazeT = Math.Max(0f, c.DazeT - dt);
            ColCharge(e, c, dt);
            ColHold(e, c, dt);
            c.ActT -= dt;
            if (c.ActT <= 0f)
                ColAct(e, c, tpos);
            if (c.Stage == ColStage.LastStand)
            {
                c.MeteorT -= dt;
                if (c.MeteorT <= 0f)
                {
                    c.MeteorT = 3f;
                    var p = ColTarget(c);
                    if (p != null)
                        ThrowDrop(e, c, p.Pos, ColDrop.LastMeteor);
                }
            }
            e.Phase = (byte)c.Turn;
            if (c.Stage != ColStage.LastStand && c.TurnT >= c.TurnLen && c.ChargeLeft <= 0f && c.ChargeWarn <= 0f)
                NextColTurn(e, c);
        }

        static Rgba ColStageColor(ColStage s) => s == ColStage.Crumbling ? new Rgba(0.88f, 0.64f, 0.35f) : s == ColStage.MoltenCore ? new Rgba(1f, 0.48f, 0.23f)
            : s == ColStage.LastStand ? new Rgba(1f, 0.31f, 0.23f) : new Rgba(0.78f, 0.62f, 0.45f);

        void EnterColStage(Enemy e, ColStage stage)
        {
            var c = ColOf(e);
            c.Stage = stage;
            c.Step = -1;
            c.ChargeWarn = c.ChargeLeft = 0f;
            c.MeteorT = 1.5f;
            if (stage == ColStage.Crumbling)
            {
                // Its armour falls off as four plates of cover.
                c.Slabs.Clear();
                for (int i = 0; i < SlabSpots.Length; i++)
                {
                    bool upright = i >= 2;
                    c.Slabs.Add(new ColSlab { Pos = SlabSpots[i], W = upright ? SlabH : SlabW, H = upright ? SlabW : SlabH, Hp = SlabHp });
                    BroadcastFx(FxKind.Boom, SlabSpots[i], ColStageColor(ColStage.Awakening), 80f, 0.5f, Sfx.None);
                }
                BroadcastSound(Sfx.BossCrack);  // its armour breaks off
            }
            else
                c.Slabs.Clear();
            if (stage == ColStage.LastStand)
                BroadcastSound(Sfx.BossGong);
            NextColTurn(e, c);
            DoLog("the Colossus: " + stage);
        }

        /// The end of a phase: it drops to its knees, staggered, its health held at the threshold. The screen clears
        /// (its rings, landings, fissures and pillars go, and its shots); hits fill Break until its shell blasts off.
        void StartColStagger(Enemy e, ColStage to, float at)
        {
            var c = ColOf(e);
            e.Hp = Math.Max(e.Hp, e.MaxHp * at);
            c.Stagger = true;
            c.Break = 0f;
            c.TransTo = to;
            ClearColField(c);
            HostClearEnemyBullets();
            BroadcastFx(FxKind.Boom, e.Pos, ColStageColor(c.Stage), e.Radius * 1.4f, 0.5f, Sfx.BossCrack);  // it buckles
            BroadcastFx(FxKind.Ring, e.Pos, new Rgba(1f, 0.85f, 0.35f), e.Radius * 3f, 0.6f, Sfx.BossRumble);
            e.Phase = ColShift;
            DoLog("the Colossus: staggered");
        }

        static void ClearColField(ColossusState c)
        {
            c.Rings.Clear();
            c.Drops.Clear();
            c.Cracks.Clear();
            c.Pillars.Clear();
            c.ChargeWarn = c.ChargeLeft = c.DazeT = c.HoldT = 0f;
        }

        /// Broken: a layer of its shell blasts off (the shards: ColossusShellLook.cs), it drops to the next phase's
        /// size, and the phase change starts (TransT: it can't be hurt while it stands back up).
        void BreakColShell(Enemy e, ColossusState c)
        {
            c.Stagger = false;
            c.Break = 0f;
            BroadcastFx(FxKind.Boom, e.Pos, Rgba.White, e.Radius * 2.2f, 0.5f, Sfx.BigExplode);
            BroadcastFx(FxKind.Ring, e.Pos, new Rgba(1f, 0.9f, 0.7f), 1200f, 0.7f, Sfx.Shockwave);
            StartColTransition(e, c.TransTo);
        }

        void StartColTransition(Enemy e, ColStage to)
        {
            var c = ColOf(e);
            c.TransT = ColTransTime;
            c.TransTo = to;
            ClearColField(c);
            HostClearEnemyBullets();
            BroadcastFx(FxKind.Ring, e.Pos, ColStageColor(to), 900f, 0.8f, to == ColStage.MoltenCore ? Sfx.BossWarp : Sfx.BossRumble);
            e.Phase = ColShift;
            e.Radius = ColRadius(e);
            DoLog("the Colossus: changing to " + to);
        }

        void NextColTurn(Enemy e, ColossusState c)
        {
            c.NextTurn(this, ColTurns, ColTimes);
            c.ActT = 0.3f;
            c.Fired = 0;
            if (c.Turn == ColTurn.Stomps || c.Turn == ColTurn.Last)
                c.GapA = RandAngle();
            c.Cycling = c.Turn == ColTurn.Fissures && c.Cracks.Count >= ColMaxCracks;
            if (c.Turn == ColTurn.Rockslide)
            {
                c.SlideEdge = (c.SlideEdge + 1) % 2;
                c.SlideGap = rng.Next(10);
            }
            e.Phase = (byte)c.Turn;
        }

        /// Where its current target stands (the middle if there's nobody).
        V2 ColTargetPos(Enemy e)
        {
            Player p;
            return Players.TryGetValue(e.TargetId, out p) && !p.Downed ? p.Pos : ArenaMid;
        }

        /// The players it goes for, in turn (one each for meteors and boulders, so a team shares them).
        Player ColTarget(ColossusState c)
        {
            Player pick = null;
            int alive = 0;
            foreach (var p in Players.Values)
                if (!p.Downed)
                    alive++;
            if (alive == 0)
                return null;
            int k = c.NextTarget++ % alive;
            foreach (var p in Players.Values)
                if (!p.Downed && k-- == 0)
                    pick = p;
            return pick;
        }

        /// Players' velocities (the host only sees positions), so boulders can lead them.
        void TrackPlayers(ColossusState c, float dt)
        {
            foreach (var p in Players.Values)
            {
                V2 last;
                if (c.LastPos.TryGetValue(p.Id, out last) && dt > 0f)
                {
                    V2 v = (p.Pos - last) / dt, old;
                    c.Vel.TryGetValue(p.Id, out old);
                    c.Vel[p.Id] = old + (v - old) * Math.Min(1f, dt * 6f);
                }
                c.LastPos[p.Id] = p.Pos;
            }
        }

        /// The current turn's next move.
        void ColAct(Enemy e, ColossusState c, V2 tpos)
        {
            float aim = (tpos - e.Pos).Angle;
            switch (c.Turn)
            {
                case ColTurn.Stomps:  // three stomps 2 s apart: rings of orbs with three small openings, one a short step beside its target
                    c.ActT = 2f;
                    AddOrbRing(e, c, 3);
                    break;
                case ColTurn.Boulders:  // three boulders, each lifted over its head (ColHoldTime), then lobbed where its target is heading
                {
                    if (c.Fired >= 3 || c.HoldT > 0f)
                    {
                        c.ActT = c.Fired >= 3 ? 99f : 0.2f;
                        break;
                    }
                    var p = ColTarget(c);
                    if (p == null)
                    {
                        c.ActT = 0.5f;
                        break;
                    }
                    c.Fired++;
                    c.HoldT = ColHoldTime;
                    c.HoldSide = -c.HoldSide;
                    c.HoldTarget = p.Id;
                    c.HoldAim = p.Pos;
                    c.ActT = ColHoldTime + 0.6f;
                    BroadcastSound(Sfx.BossRumble);  // it tears a boulder out of the ground
                    break;
                }
                case ColTurn.Fissures:
                    if (c.Cycling)  // five scars on the floor: they wake and erupt again, one after another
                    {
                        if (c.Fired < c.Cracks.Count)
                        {
                            var k = c.Cracks[c.Fired++];
                            k.State = ColCrack.Waiting;
                            k.Wait = 0f;
                            c.ActT = 0.45f;
                            if (c.Fired == 1)
                                BroadcastFx(FxKind.Boom, e.Pos, ColStageColor(ColStage.Awakening), e.Radius * 1.4f, 0.4f, Sfx.BossRumble);  // the scars wake
                        }
                        else
                            c.ActT = 99f;
                    }
                    else if (c.Fired < 2 && c.Cracks.Count < ColMaxCracks)
                    {
                        c.ActT = 2.5f;
                        c.Fired++;
                        c.Cracks.Add(new ColCrack { From = e.Pos, Ang = CrackAim(e, c, aim), Max = CrackMax, Seed = rng.Next(1000) });
                        BroadcastFx(FxKind.Boom, e.Pos, ColStageColor(ColStage.Awakening), e.Radius * 1.2f, 0.4f, Sfx.BossRumble);  // a fissure opens
                    }
                    else
                        c.ActT = 99f;
                    break;
                case ColTurn.Charge:
                    if (c.Fired < 2 && c.ChargeLeft <= 0f && c.ChargeWarn <= 0f && c.DazeT <= 0f)
                    {
                        c.ActT = 3f;
                        c.Fired++;
                        c.ChargeWarn = ColChargeWarn;
                        c.ChargeDir = aim;
                        BroadcastSound(Sfx.Roar);  // aiming the charge
                    }
                    else
                        c.ActT = 0.2f;
                    break;
                case ColTurn.Rockslide:  // rows of 13 rocks from one edge, a 3-rock gap that shifts along
                {
                    c.ActT = 1.4f;
                    const int rows = 13;
                    int gap = c.SlideGap;
                    c.SlideGap = (c.SlideGap + 2) % (rows - 2);
                    for (int i = 0; i < rows; i++)
                    {
                        if (i >= gap && i < gap + 3)
                            continue;
                        if (c.SlideEdge == 0)
                            ColShot(e, new V2((i + 0.5f) * Width / rows, -10f), MathX.Pi / 2f, 180f, Defs.BigShot, 6f);
                        else
                            ColShot(e, new V2(-10f, (i + 0.5f) * Height / rows), 0f, 180f, Defs.BigShot, 10f);
                    }
                    BroadcastSound(Sfx.BossRumble);  // a row of rocks comes down
                    break;
                }
                case ColTurn.Aftershocks:  // rings of shots with two gaps that turn 20 degrees a ring
                    c.ActT = 0.7f;
                    c.ShockA += 20f * MathX.Pi / 180f;
                    for (int i = 0; i < 30; i++)
                    {
                        float a = MathX.Tau * i / 30f;
                        if (AngleGap(a, c.ShockA) < 0.32f || AngleGap(a, c.ShockA + MathX.Pi) < 0.32f)
                            continue;
                        EnemyShot(e, a, 170f, Defs.SmallShot);
                    }
                    BroadcastFx(FxKind.Ring, e.Pos, ColStageColor(ColStage.Crumbling), 120f, 0.35f, c.TurnT < 0.5f ? Sfx.BossRumble : Sfx.BossPulse);  // the first one quakes
                    break;
                case ColTurn.LavaStomps:  // a stomp every 1.5 s, three openings, and a pool where it stood
                    c.ActT = 1.5f;
                    AddOrbRing(e, c, 3);
                    SpawnPatchAt(PatchKind.Lava, e.Pos, LavaWarn, LavaWarn + LavaLife, 90f, 90f);
                    BroadcastSound(Sfx.BossSplat);
                    break;
                case ColTurn.Meteors:  // two volleys of three, 0.5 s apart
                {
                    if (c.Fired >= 6)
                    {
                        c.ActT = 99f;
                        break;
                    }
                    c.ActT = c.Fired % 3 == 2 ? 2.4f : 0.5f;
                    c.Fired++;
                    var p = ColTarget(c);
                    if (p != null)
                        ThrowDrop(e, c, p.Pos, ColDrop.Meteor);
                    break;
                }
                case ColTurn.CaveIn:  // ten pillars anywhere, two by each player
                    c.ActT = 99f;
                    for (int k = 0; k < 10; k++)
                        DropPillar(c, RandomInArena(80f));
                    foreach (var p in Players.Values)
                        if (!p.Downed)
                            for (int k = 0; k < 2; k++)
                                DropPillar(c, ClampToArena(p.Pos + RandDir() * 45f, 60f));
                    BroadcastSound(Sfx.BossRumble);  // the roof starts coming down
                    break;
                case ColTurn.Last:  // four openings: one a step beside its target (alternating sides), one by each other player
                    c.ActT = 1.6f;
                    AddOrbRing(e, c, 4);
                    break;
                default:
                    c.ActT = 0.2f;
                    break;
            }
        }

        static float AngleGap(float a, float b) => Math.Abs(MathX.WrapPi(a - b));

        /// Where to put a gap for someone at `at`: StompStep to their left (side -1) or right (+1) round the Colossus,
        /// so a short sidestep reaches it in time wherever they stand (it used to be up to 90 degrees round, too far to run).
        float GapBeside(Enemy e, V2 at, float side)
        {
            V2 rel = at - e.Pos;
            float d = Math.Max(60f, rel.Length);
            return rel.Angle + side * Math.Max(0.25f, Math.Min(0.8f, StompStep / d));
        }

        /// A stomp: a ring of orbs from its feet with `opens` small openings: one a short step beside its target
        /// (alternating sides), one beside each other player standing, the rest anywhere (spread round).
        void AddOrbRing(Enemy e, ColossusState c, int opens)
        {
            var r = new StompRing { Center = e.Pos, Radius = e.Radius + 6f, Warn = RingWarn, Orbs = true, Rot = RandAngle(), Gaps = 0 };
            V2 tpos = ColTargetPos(e);
            c.StompSide = -c.StompSide;
            AddOpening(r, GapBeside(e, tpos, c.StompSide), 0f);
            foreach (var p in Players.Values)
                if (r.Gaps < opens && !p.Downed && p.Pos.FartherThan(tpos, 60f))
                    AddOpening(r, GapBeside(e, p.Pos, c.StompSide), 0.5f);
            for (int tries = 0; r.Gaps < opens && tries < 60; tries++)
                AddOpening(r, RandAngle(), 0.75f);
            c.Rings.Add(r);
            BroadcastSound(Sfx.BossSlam);  // the stomp
        }

        /// Adds an opening at `a` unless it's within `apart` radians of one already there.
        static void AddOpening(StompRing r, float a, float apart)
        {
            for (int k = 0; k < r.Gaps; k++)
                if (AngleGap(a, RingGapAt(r, k)) < apart)
                    return;
            a = MathX.Fmod(a, MathX.Tau);
            switch (r.Gaps++)
            {
                case 0: r.Gap1 = a; break;
                case 1: r.Gap2 = a; break;
                case 2: r.Gap3 = a; break;
                default: r.Gap4 = a; r.Gaps = 4; break;
            }
        }

        public static float RingGapAt(StompRing r, int k) => k == 0 ? r.Gap1 : k == 1 ? r.Gap2 : k == 2 ? r.Gap3 : r.Gap4;

        /// A new fissure's aim: at its target, turned aside if a scar from about here already runs that way.
        float CrackAim(Enemy e, ColossusState c, float aim)
        {
            for (int k = 1; k < 12; k++)
            {
                bool clear = true;
                foreach (var old in c.Cracks)
                    if (old.From.CloserThan(e.Pos, 150f) && AngleGap(old.Ang, aim) < 0.3f)
                        clear = false;
                if (clear)
                    break;
                aim += (k % 2 == 1 ? 1f : -1f) * k * 0.34f;
            }
            return aim;
        }

        /// The hand holding a boulder (side -1 or 1 in Facing's local y), where the body look draws it.
        public static V2 ColHand(Enemy e, int side)
        {
            V2.Axes(e.Facing, out V2 f, out V2 y);
            return e.Pos + (f * 14f + y * (side * 56f)) * (e.Radius / 60f);
        }

        /// A boulder over its head: its aim follows its target's lead, then it's lobbed from that hand.
        void ColHold(Enemy e, ColossusState c, float dt)
        {
            if (c.HoldT <= 0f)
                return;
            Player p;
            if (Players.TryGetValue(c.HoldTarget, out p) && !p.Downed)
            {
                V2 v;
                c.Vel.TryGetValue(p.Id, out v);
                c.HoldAim = ClampToArena(p.Pos + v.LimitLength(240f) * 0.9f, 40f);
                e.Facing = (p.Pos - e.Pos).Angle;
            }
            c.HoldT -= dt;
            if (c.HoldT > 0f)
                return;
            c.HoldT = 0f;
            c.Drops.Add(new ColDrop { Pos = c.HoldAim, From = ColHand(e, c.HoldSide), Dur = ColBoulderFlight, Radius = ColBoulderR, Kind = ColDrop.Boulder });
            BroadcastSound(Sfx.Throw);
        }

        void ColShot(Enemy e, V2 pos, float ang, float speed, byte kind, float life = 8f)
        {
            SpawnBullet(kind, pos, V2.FromAngle(ang) * speed, 0f, life, Defs.BulletDamage(kind), 0, true).Source = e.Type;
        }

        void ThrowDrop(Enemy e, ColossusState c, V2 at, byte kind)
        {
            at = ClampToArena(at, 40f);
            c.Drops.Add(new ColDrop { Pos = at, From = e.Pos, Dur = kind == ColDrop.Boulder ? 1.1f : 1.2f, Radius = kind == ColDrop.Boulder ? 60f : 70f, Kind = kind });
            BroadcastSound(kind == ColDrop.Boulder ? Sfx.Throw : Sfx.BossWarp);  // a meteor swells in
        }

        void DropPillar(ColossusState c, V2 at) => c.Drops.Add(new ColDrop { Pos = at, From = at, Dur = 1.2f, Radius = 40f, Kind = ColDrop.Pillar });

        /// The charge: aim (a red line) for a second, then run until a wall, a plate (which breaks and dazes it) or
        /// its full length; it stomps where it stops.
        void ColCharge(Enemy e, ColossusState c, float dt)
        {
            if (c.ChargeWarn > 0f)
            {
                c.ChargeWarn -= dt;
                if (c.ChargeWarn <= 0f)
                {
                    c.ChargeLeft = ColChargeLength;
                    BroadcastSound(Sfx.BossWhoosh);
                }
                return;
            }
            if (c.ChargeLeft <= 0f)
                return;
            c.ChargeLeft -= ColChargeSpeed * dt;
            V2 next = e.Pos + V2.FromAngle(c.ChargeDir) * ColChargeSpeed * dt;
            bool stop = c.ChargeLeft <= 0f || ClampToArena(next, e.Radius).DistanceTo(next) > 1f;
            for (int i = c.Slabs.Count - 1; i >= 0; i--)
                if (InSlab(c.Slabs[i], next, e.Radius - 6f))
                {
                    BroadcastFx(FxKind.Boom, c.Slabs[i].Pos, Rgba.Gold, 90f, 0.5f, Sfx.BossCrack);  // the plate breaks, it's dazed
                    c.Slabs.RemoveAt(i);
                    c.DazeT = ColDazeTime;
                    stop = true;
                }
            if (stop)
            {
                c.ChargeLeft = 0f;
                AddOrbRing(e, c, 3);
            }
        }

        public static bool InSlab(ColSlab s, V2 p, float pad) =>
            p.X > s.Pos.X - s.W / 2f - pad && p.X < s.Pos.X + s.W / 2f + pad && p.Y > s.Pos.Y - s.H / 2f - pad && p.Y < s.Pos.Y + s.H / 2f + pad;

        /// Host: rings spread and hurt, things land, fissures race and erupt, pillars shatter, lava burns.
        void TickColHazards(Enemy e, ColossusState c, float dt)
        {
            AdvanceRings(c, dt);
            foreach (var r in c.Rings)
                HurtByRing(r, StompDamage, "The Colossus (stomp)");
            // Landings
            for (int i = c.Drops.Count - 1; i >= 0; i--)
            {
                var d = c.Drops[i];
                d.T += dt;
                if (d.T < d.Dur)
                    continue;
                c.Drops.RemoveAt(i);
                float dmg = d.Kind == ColDrop.Boulder || d.Kind == ColDrop.Pillar ? 30f : 40f;
                foreach (var p in Players.Values)
                    if (Hittable(p) && p.Pos.CloserThan(d.Pos, d.Radius + p.Def.HitR))
                        Damage(p, dmg, d.Kind == ColDrop.Pillar ? "The Colossus (cave-in)" : d.Hot ? "The Colossus (meteor)" : "The Colossus (boulder)");
                switch (d.Kind)
                {
                    case ColDrop.Boulder:
                        for (int k = 0; k < 10; k++)
                            ColShot(e, d.Pos, MathX.Tau * k / 10f, 140f, Defs.SmallShot);
                        break;
                    case ColDrop.Meteor:
                    case ColDrop.LastMeteor:
                        for (int k = 0; k < 14; k++)
                            ColShot(e, d.Pos, MathX.Tau * k / 14f, 130f, Defs.BigShot);
                        if (d.Kind == ColDrop.Meteor)
                            SpawnPatchAt(PatchKind.Lava, d.Pos, LavaWarn, LavaWarn + LavaLife, 70f, 70f);
                        break;
                    case ColDrop.Pillar:
                        c.Pillars.Add(new ColPillar { Pos = d.Pos, T = PillarLife });
                        break;
                }
                BroadcastFx(FxKind.Boom, d.Pos, d.Hot ? new Rgba(1f, 0.48f, 0.23f) : ColStageColor(ColStage.Awakening), d.Radius + 20f, 0.4f, d.Hot ? Sfx.BigExplode : Sfx.BossSlam);
            }
            // Fissures: race out, wait, erupt (shots out of both sides every 45 px, and for CrackErupt it burns along its
            // length), then lie as a scar until the phase ends (a Fissures turn with five up wakes them again)
            foreach (var k in c.Cracks)
                TickColCrack(e, k, dt);
            // Pillars shatter
            for (int i = c.Pillars.Count - 1; i >= 0; i--)
            {
                var pl = c.Pillars[i];
                pl.T -= dt;
                if (pl.T > 0f)
                    continue;
                c.Pillars.RemoveAt(i);
                for (int k = 0; k < 6; k++)
                {
                    float a = MathX.Tau * k / 6f + pl.Pos.X * 0.01f;
                    ColShot(e, pl.Pos + V2.FromAngle(a) * PillarR, a, 150f, Defs.SmallShot);
                }
                BroadcastFx(FxKind.Boom, pl.Pos, ColStageColor(ColStage.Awakening), 50f, 0.3f, Sfx.BossCrack);  // a pillar shatters
            }
            // Lava
            c.LavaTickT -= dt;
            if (c.LavaTickT <= 0f)
            {
                c.LavaTickT = 0.25f;
                foreach (var p in Players.Values)  // once a tick, however many pools overlap under you
                {
                    if (!Hittable(p))
                        continue;
                    foreach (var pt in Patches.Values)
                        if (pt.Kind == PatchKind.Lava && pt.Active && p.Pos.CloserThan(pt.Pos, pt.Radius + p.Def.HitR))
                        {
                            DamageOverTime(p, LavaDps * 0.25f, "The Colossus (lava)");
                            break;
                        }
                }
            }
        }

        void TickColCrack(Enemy e, ColCrack k, float dt)
        {
            switch (k.State)
            {
                case ColCrack.Racing:
                    if (!GrowCrack(k, dt))
                    {
                        k.State = ColCrack.Waiting;
                        k.Wait = 0f;
                    }
                    break;
                case ColCrack.Waiting:
                    k.Wait += dt;
                    if (k.Wait < CrackWait)
                        break;
                    k.State = ColCrack.Erupting;
                    k.Wait = 0f;
                    k.Hit.Clear();
                    V2 dir = V2.FromAngle(k.Ang);
                    for (float s = CrackStep; s < k.Len; s += CrackStep)
                    {
                        V2 at = k.From + dir * s;
                        ColShot(e, at, k.Ang + MathX.Pi / 2f, 120f, Defs.SmallShot);
                        ColShot(e, at, k.Ang - MathX.Pi / 2f, 120f, Defs.SmallShot);
                    }
                    BroadcastFx(FxKind.Bolt, k.From, k.From + dir * k.Len, new Rgba(1f, 0.6f, 0.3f), 0f, 0.3f, Sfx.BossCrack);  // the fissure erupts
                    break;
                case ColCrack.Erupting:
                    k.Wait += dt;
                    foreach (var p in Players.Values)
                        if (Hittable(p) && OnCrack(k, p.Pos, CrackHurtR + p.Def.HitR) && k.Hit.Add(p.Id))
                            Damage(p, ColCrackDamage, "The Colossus (fissure)");
                    if (k.Wait >= CrackErupt)
                        k.State = ColCrack.Dormant;
                    break;
            }
        }

        /// Within `reach` of the fissure's line (its jags aside).
        public static bool OnCrack(ColCrack k, V2 p, float reach)
        {
            V2 dir = V2.FromAngle(k.Ang), rel = p - k.From;
            float along = rel.X * dir.X + rel.Y * dir.Y;
            if (along < 0f || along > k.Len)
                return false;
            return Math.Abs(rel.X * dir.Y - rel.Y * dir.X) < reach;
        }

        /// Host: a spreading ring hurts each player once as it passes over them: seen in front of it, now at it or just
        /// behind it (your own game puts you behind it, so the host may only ever see you there), and not in a gap.
        /// The Colossus's rings and a Stomper's (QuarryFaction.cs).
        void HurtByRing(StompRing r, float damage, string source)
        {
            if (r.Warn > 0f)
                return;
            if (r.Orbs)
            {
                foreach (var p in Players.Values)  // the Colossus's orbs: touch one (outside an opening) and it hurts, once a ring
                    if (Hittable(p) && !r.Hit.Contains(p.Id) && TouchesRingOrb(r, p.Pos, ColOrbR + p.Def.HitR))
                    {
                        r.Hit.Add(p.Id);
                        Damage(p, damage, source);
                    }
                return;
            }
            foreach (var p in Players.Values)
            {
                if (p.Downed)
                    continue;
                float pad = GateBulletR + p.Def.HitR, d = p.Pos.DistanceTo(r.Center);
                if (d > r.Radius + pad)
                    r.Outside.Add(p.Id);
                else if (r.Outside.Contains(p.Id) && d > RingBack(r) - pad - 40f)
                {
                    r.Outside.Remove(p.Id);
                    if (!InRingGap(r, p.Pos, pad) && r.Hit.Add(p.Id) && p.InvulnT <= 0f)  // invulnerable (revive, Lifeline, Leap Slam): it passes over you
                        Damage(p, damage, source);
                }
            }
        }

        /// Where an Orbs ring's orb i is.
        public static V2 RingOrbAt(StompRing r, int i) => r.Center + V2.FromAngle(r.Rot + MathX.Tau * i / ColRingOrbs) * r.Radius;

        /// An opening's half-width (radians) at this radius: ColOpenHalf, or wider near the middle so it's never less
        /// than ColOpenMinArc across.
        public static float OrbOpenHalfAt(float radius) => Math.Max(ColOpenHalf, ColOpenMinArc / 2f / Math.Max(1f, radius));

        /// An Orbs ring has no orb at this angle (it's in an opening).
        public static bool RingOpenAt(StompRing r, float a)
        {
            float half = OrbOpenHalfAt(r.Radius);
            for (int k = 0; k < r.Gaps; k++)
                if (AngleGap(a, RingGapAt(r, k)) < half)
                    return true;
            return false;
        }

        /// Something at `p` touches one of an Orbs ring's orbs (within `reach` of its middle).
        public static bool TouchesRingOrb(StompRing r, V2 p, float reach)
        {
            V2 rel = p - r.Center;
            float d = rel.Length;
            if (r.Warn > 0f || Math.Abs(d - r.Radius) > reach)
                return false;
            int i0 = (int)Math.Round((rel.Angle - r.Rot) / MathX.Tau * ColRingOrbs);
            for (int i = i0 - 1; i <= i0 + 1; i++)
            {
                int k = ((i % ColRingOrbs) + ColRingOrbs) % ColRingOrbs;
                if (RingOpenAt(r, r.Rot + MathX.Tau * k / ColRingOrbs))
                    continue;
                if (RingOrbAt(r, k).CloserThan(p, reach))
                    return true;
            }
            return false;
        }

        /// Moves the rings out (every game: the host, and clients between snapshots); gone once off the arena.
        static void AdvanceRings(ColossusState c, float dt)
        {
            foreach (var r in c.Rings)
            {
                if (r.Warn > 0f)
                    r.Warn -= dt;
                else
                    r.Radius += (r.Orbs ? ColOrbSpeed : RingSpeed) * dt;
            }
            c.Rings.RemoveAll(r => RingBack(r) > RingReach(r) + 20f);
        }

        /// How far a ring has to spread to clear the whole arena: to the farthest corner from where it stomped.
        public static float RingReach(StompRing r)
        {
            float x = Math.Max(r.Center.X, Width - r.Center.X), y = Math.Max(r.Center.Y, Height - r.Center.Y);
            return (float)Math.Sqrt(x * x + y * y);
        }

        /// A fissure racing out; false once it has stopped (its full length, or the arena's edge).
        static bool GrowCrack(ColCrack k, float dt)
        {
            if (k.Len >= k.Max)
                return false;
            k.Len = Math.Min(k.Max, k.Len + CrackSpeed * dt);
            V2 tip = k.From + V2.FromAngle(k.Ang) * k.Len;
            if (tip.X < 0f || tip.Y < 0f || tip.X > Width || tip.Y > Height)
                k.Max = k.Len;
            return true;
        }

        /// The ring's band of push blocks (both rows) at this distance from its centre, with `pad` on either side.
        public static float RingBack(StompRing r) => r.Orbs ? r.Radius - ColOrbR : r.Radius - (GateRows - 1) * GateRowGap;

        /// A gap's half-width (radians) at this distance from the ring's middle: RingGapHalf, or wider near the middle so
        /// it's never less than RingGapMinArc across.
        public static float RingGapHalfAt(float d) => Math.Max(RingGapHalf, RingGapMinArc / 2f / Math.Max(1f, d));

        public static bool InRingGap(StompRing r, V2 p, float pad)
        {
            V2 rel = p - r.Center;
            float d = rel.Length;
            if (d < 1f)
                return false;
            float a = rel.Angle, half = RingGapHalfAt(d) - pad / d;
            return AngleGap(a, r.Gap1) < half || (r.Gaps == 2 && AngleGap(a, r.Gap2) < half);
        }

        public static bool InRing(StompRing r, V2 p, float pad)
        {
            float d = p.DistanceTo(r.Center);
            return r.Warn <= 0f && d > RingBack(r) - pad && d < r.Radius + pad && !InRingGap(r, p, pad);
        }

        /// A ring that has reached you puts you just behind it (inside it), never carried along.
        static void PushOutOfRing(StompRing r, Player me)
        {
            float pad = GateBulletR + me.Def.HitR;
            if (!InRing(r, me.Pos, pad))
                return;
            V2 dir = (me.Pos - r.Center).Normalized();
            me.Pos = r.Center + dir * Math.Max(0f, RingBack(r) - pad - 2f);
        }

        /// Your own game: a ring that reaches you passes over you (you end up just behind it; the host hurts you once),
        /// and one you walk into from inside shoves you back. Either way you're behind it, never carried along.
        /// Plates and pillars are solid.
        void ColossusPush(Player me, V2 before)
        {
            var e = TheColossus();
            if (e == null || me.Downed)
                return;
            var c = e.Colossus;
            foreach (var r in c.Rings)
                if (!r.Orbs)  // (orbs don't shove: they hurt)
                    PushOutOfRing(r, me);
            foreach (var s in c.Slabs)
            {
                float pad = me.Def.HitR;
                if (!InSlab(s, me.Pos, pad))
                    continue;
                // Out the nearest side.
                float l = me.Pos.X - (s.Pos.X - s.W / 2f - pad), rr = s.Pos.X + s.W / 2f + pad - me.Pos.X;
                float t = me.Pos.Y - (s.Pos.Y - s.H / 2f - pad), b = s.Pos.Y + s.H / 2f + pad - me.Pos.Y;
                float m = Math.Min(Math.Min(l, rr), Math.Min(t, b));
                me.Pos = m == l ? new V2(me.Pos.X - l, me.Pos.Y) : m == rr ? new V2(me.Pos.X + rr, me.Pos.Y) : m == t ? new V2(me.Pos.X, me.Pos.Y - t) : new V2(me.Pos.X, me.Pos.Y + b);
            }
            foreach (var pl in c.Pillars)
            {
                float reach = PillarR + me.Def.HitR, d = me.Pos.DistanceTo(pl.Pos);
                if (d < reach)
                    me.Pos = pl.Pos + (d > 0.5f ? (me.Pos - pl.Pos) / d : V2.FromAngle(me.Aim + MathX.Pi)) * reach;
            }
        }

        /// Host, in Collide: plates and pillars stop bullets from both sides; enemy hits wear a plate down.
        bool ColossusCoverStops(Bullet b)
        {
            if (colCover == null)
                return false;
            var c = colCover;
            for (int i = 0; i < c.Slabs.Count; i++)
            {
                var s = c.Slabs[i];
                if (!InSlab(s, b.Pos, b.Radius))
                    continue;
                if (Defs.IsEnemyShot(b.Kind) && --s.Hp <= 0)
                {
                    BroadcastFx(FxKind.Boom, s.Pos, ColStageColor(ColStage.Awakening), 70f, 0.4f, Sfx.BossCrack);  // a plate crumbles
                    c.Slabs.RemoveAt(i);
                }
                return true;
            }
            foreach (var pl in c.Pillars)
                if (b.Pos.CloserThan(pl.Pos, PillarR + b.Radius))
                    return true;
            return false;
        }
        ColossusState colCover;  // set for each Collide: the Colossus's cover, if it has any up

        void FindColCover()
        {
            colCover = null;
            var e = TheColossus();
            if (e != null && (e.Colossus.Slabs.Count > 0 || e.Colossus.Pillars.Count > 0))
                colCover = e.Colossus;
        }

        /// Its rock volley: 3 aimed piercing shots, between turns in the first two phases and all through the last.
        void ColossusFire(Enemy e, float aim)
        {
            var c = ColOf(e);
            bool volley = c.TransT <= 0f && c.ChargeLeft <= 0f && !c.Stagger && c.HoldT <= 0f && (c.Stage == ColStage.LastStand
                || (c.Stage == ColStage.Awakening && (c.Turn == ColTurn.Rest || c.Turn == ColTurn.Boulders))
                || (c.Stage == ColStage.Crumbling && c.Turn == ColTurn.Rest));
            if (!volley)
            {
                e.FireT = 0.2f;
                return;
            }
            e.FireT = 1.2f;
            for (int i = -1; i <= 1; i++)
                EnemyShot(e, aim + i * 0.18f, 170f, Defs.PierceShot);
        }

        // --- Sync -------------------------------------------------------------------------------------

        void SyncColossus(NetSync s, Enemy e)
        {
            bool has = e.Colossus != null;
            s.Bool(ref has);
            if (!has)
                return;
            var c = e.Colossus ?? (e.Colossus = new ColossusState { Step = 0 });
            int stage = (int)c.Stage, turn = (int)c.Turn, transTo = (int)c.TransTo;
            s.Byte(ref stage);
            s.Byte(ref turn);
            s.Byte(ref transTo);
            s.Float(ref c.TurnT);
            s.Float(ref c.TransT);
            s.Float(ref c.DazeT);
            s.Float(ref c.ChargeWarn);
            s.Float(ref c.ChargeLeft);
            s.Float(ref c.ChargeDir);
            s.Bool(ref c.Stagger);
            s.Float(ref c.Break);
            s.Float(ref c.HoldT);
            s.Vec(ref c.HoldAim);
            int side = c.HoldSide > 0 ? 1 : 0;
            s.Byte(ref side);
            if (s.Reading)
            {
                c.Stage = (ColStage)stage;
                c.Turn = (ColTurn)turn;
                c.TransTo = (ColStage)transTo;
                c.HoldSide = side == 1 ? 1 : -1;
            }
            SyncList(s, c.Rings, (q, r) =>
            {
                q.Vec(ref r.Center); q.Float(ref r.Radius); q.Float(ref r.Warn); q.Byte(ref r.Gaps); q.Bool(ref r.Orbs);
                q.Float(ref r.Gap1); q.Float(ref r.Gap2); q.Float(ref r.Gap3); q.Float(ref r.Gap4); q.Float(ref r.Rot);
            });
            SyncList(s, c.Drops, (q, d) => { q.Vec(ref d.Pos); q.Vec(ref d.From); q.Float(ref d.T); q.Float(ref d.Dur); q.Float(ref d.Radius); q.Byte(ref d.Kind); });
            SyncList(s, c.Cracks, (q, k) => { q.Vec(ref k.From); q.Float(ref k.Ang); q.Float(ref k.Len); q.Float(ref k.Max); q.Short(ref k.Seed); q.Byte(ref k.State); q.Float(ref k.Wait); });
            SyncList(s, c.Slabs, (q, b) => { q.Vec(ref b.Pos); q.Float(ref b.W); q.Float(ref b.H); q.Byte(ref b.Hp); });
            SyncList(s, c.Pillars, (q, p) => { q.Vec(ref p.Pos); q.Float(ref p.T); });
        }

        static void SyncList<T>(NetSync s, List<T> list, Action<NetSync, T> each) where T : new()
        {
            int n = list.Count;
            s.Byte(ref n);
            if (s.Reading)
            {
                list.Clear();
                for (int i = 0; i < n; i++)
                {
                    var x = new T();
                    each(s, x);
                    list.Add(x);
                }
            }
            else
                foreach (var x in list)
                    each(s, x);
        }

        /// Clients, each frame: rings spread, fissures race, shadows grow and the clocks run between snapshots.
        void UpdateColossusView(float dt)
        {
            if (IsHost)
                return;
            foreach (var e in Enemies.Values)
            {
                var c = e.Colossus;
                if (e.Type != EnemyType.Colossus || c == null)
                    continue;
                AdvanceRings(c, dt);
                foreach (var k in c.Cracks)
                {
                    if (k.State == ColCrack.Racing)
                        GrowCrack(k, dt);
                    else if (k.State != ColCrack.Dormant)
                        k.Wait += dt;
                }
                foreach (var d in c.Drops)
                    d.T = Math.Min(d.Dur, d.T + dt);
                foreach (var p in c.Pillars)
                    p.T = Math.Max(0f, p.T - dt);
                c.TurnT += dt;
                c.TransT = Math.Max(0f, c.TransT - dt);
                c.DazeT = Math.Max(0f, c.DazeT - dt);
                c.ChargeWarn = Math.Max(0f, c.ChargeWarn - dt);
                c.HoldT = Math.Max(0f, c.HoldT - dt);
                e.Radius = ColRadius(e);
            }
        }
    }
}
