using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Hive Queen's stage of the fight, by her health: Swarm, Honeycomb, Frenzy, then a last stand.
    public enum QueenStage : byte { Swarm, Honeycomb, Frenzy, LastStand }

    /// What she's doing right now. Each stage loops through its turns, one mechanic at a time, with a short
    /// breather (Rest) between them.
    public enum QueenTurn : byte { Rest, Drones, Bombers, Fan, Rings, Eggs, Cells, Spray, Cloud, Dive, Barrage, LastStand }

    /// A hex cell of the Honeycomb's floor that's sealing: it glows for CellWarn, then seals (hurting whoever is
    /// inside), then fades.
    public class QueenCell
    {
        public ushort Index;
        public float T;
        public bool Hit;  // host: has it sealed yet
    }

    /// Everything about the Hive Queen's fight that her body doesn't already carry. The host runs it; clients get it
    /// in the snapshot (WriteQueen / ReadQueen) to draw the cells, the swarm cloud and the dive.
    public class QueenState
    {
        public QueenStage Stage;
        public QueenTurn Turn;
        public float TurnT;              // seconds into the current turn
        public float TransT;             // > 0: changing stage (can't be hurt), counting down
        public QueenStage TransTo;
        public float Aux;                // the nectar spray's sweep, 0..1 across its 140 degrees
        public readonly List<QueenCell> Cells = new List<QueenCell>();
        public bool CloudOn, CloudHome;  // the swarm cloud, and whether it's flying back to her
        public V2 Cloud, CloudDisp;
        public V2 Shadow;                // the royal dive: where she'll land
        public float DiveT;              // seconds into the current dive
        public int Dives;                // dives done this turn

        // Host only.
        public int Step, Wave, Globs;
        public float TurnLen, WaveT, SprayDir = 1f, BombT;
        public V2 DiveFrom;
        public bool Landed;
        public readonly HashSet<int> Royal = new HashSet<int>();    // her shield drones from this Drones turn
        public readonly HashSet<int> Homing = new HashSet<int>();   // ...flying back into her to heal her
        public readonly Dictionary<int, V2> LastPos = new Dictionary<int, V2>(), Vel = new Dictionary<int, V2>();
    }

    public partial class Game
    {
        // Health at which each stage starts (the same as the Warden's).
        const float HoneycombAt = 0.65f, FrenzyAt = 0.3f, QueenLastStandAt = 0.1f;
        const float QueenTransTime = 2f;
        // Swarm: drones (shield), bombers, fan, rings.
        const float QueenSwarmHp = 3f;      // her drones have 3x normal shard health
        const int QueenSwarmCap = 8;
        const float DroneHeal = 0.02f;      // a drone that makes it back into her heals her this much
        const float BomberBlast = 60f, BomberDamage = 22f;
        // Honeycomb: eggs, sealing cells, nectar spray.
        const int EggCount = 4, DronesPerEgg = 2;
        const float EggFlight = 0.6f, EggHatch = 6f;
        public const float HiveCell = 70f;  // the hex cells' size (centre to corner)
        public const float CellWarn = 0.9f, CellLife = 1.4f;
        const float CellEvery = 1.5f, CellDamage = 18f, CellShare = 0.26f;
        const float SprayEvery = 0.1f, SpraySpeed = 230f, SpraySweep = 1.1f;
        public const float HoneyRadius = 40f, HoneySlow = 0.55f;
        const float HoneyLife = 3f;
        // Frenzy: the swarm cloud, the royal dive, the bomber barrage.
        public const int BeeCount = 36;
        public const float BeeRadius = 5f;
        const float CloudSpeed = 170f, CloudHomeSpeed = 320f, CloudTime = 6f, BeeDamage = 8f;
        public const float DiveRadius = 110f;
        const float DiveTime = 2f, DiveLand = 1.3f, DiveTrack = 0.5f, DiveDamage = 25f;
        const int Dives = 3;

        static readonly QueenTurn[][] QueenTurns =
        {
            new[] { QueenTurn.Drones, QueenTurn.Rest, QueenTurn.Bombers, QueenTurn.Rest, QueenTurn.Fan, QueenTurn.Rest, QueenTurn.Rings, QueenTurn.Rest },
            new[] { QueenTurn.Eggs, QueenTurn.Rest, QueenTurn.Cells, QueenTurn.Rest, QueenTurn.Spray, QueenTurn.Rest },
            new[] { QueenTurn.Cloud, QueenTurn.Rest, QueenTurn.Dive, QueenTurn.Rest, QueenTurn.Barrage, QueenTurn.Rest },
            new[] { QueenTurn.LastStand },
        };
        static readonly float[][] QueenTimes =
        {
            new[] { 10f, 1f, 4.5f, 1f, 5f, 1f, 5f, 1f },          // drones end early once they're all dead
            new[] { EggFlight + EggHatch, 1f, 6f, 1f, 5f, 2f },   // eggs hatch as their turn ends
            new[] { CloudTime, 1f, Dives * DiveTime, 1f, 4.5f, 1f },  // the cloud's turn waits for it to fly home
            new[] { 999f },
        };

        /// The Honeycomb's floor: flat-topped hex cells covering the arena (the same on every game).
        public static readonly V2[] HiveCells = MakeHiveCells();

        static V2[] MakeHiveCells()
        {
            var list = new List<V2>();
            float h = HiveCell * (float)Math.Sqrt(3.0);
            for (int col = 0; col * HiveCell * 1.5f <= Width; col++)
                for (int row = 0; row * h <= Height + h / 2f; row++)
                {
                    var c = new V2(col * HiveCell * 1.5f, row * h + (col % 2 == 1 ? h / 2f : 0f));
                    if (c.Y <= Height)
                        list.Add(c);
                }
            return list.ToArray();
        }

        /// Is `p` inside the hex cell centred on `c`?
        public static bool InHexCell(V2 c, V2 p, float r = HiveCell)
        {
            float dx = Math.Abs(p.X - c.X), dy = Math.Abs(p.Y - c.Y), h = r * (float)Math.Sqrt(3.0) / 2f;
            return dy <= h && dx * h + dy * r / 2f <= r * h;
        }

        static int HiveCellAt(V2 p)
        {
            int best = 0;
            float bd = float.MaxValue;
            for (int i = 0; i < HiveCells.Length; i++)
            {
                float d = HiveCells[i].DistanceSq(p);
                if (d < bd)
                {
                    bd = d;
                    best = i;
                }
            }
            return best;
        }

        QueenState QueenOf(Enemy e) => e.Queen ?? (e.Queen = new QueenState { Turn = QueenTurn.Rest, Step = -1 });  // QueenSpecial starts her first turn

        /// The Hive Queen in play, if any.
        public Enemy TheQueen()
        {
            foreach (var e in Enemies.Values)
                if (e.Type == EnemyType.HiveQueen)
                    return e;
            return null;
        }

        /// How high she is in a dive: 0 on the ground, 1 at the top.
        public static float QueenAir(QueenState q)
        {
            if (q == null || q.Turn != QueenTurn.Dive || q.Dives >= Dives)
                return 0f;
            float t = q.DiveT;
            return t < 0.3f ? t / 0.3f : t < 1.1f ? 1f : Math.Max(0f, 1f - (t - 1.1f) / 0.2f);
        }

        bool QueenImmune(Enemy e) => e.Type == EnemyType.HiveQueen && e.Queen != null && (e.Queen.TransT > 0f || QueenAir(e.Queen) > 0.5f);

        /// In the air in a dive: she doesn't touch anyone.
        public static bool QueenAirborne(Enemy e) => e.Type == EnemyType.HiveQueen && QueenAir(e.Queen) > 0.2f;

        /// A bee of the swarm cloud: where it is, from the cloud's centre and the turn's clock (the same on every game).
        public static V2 BeePos(int i, V2 center, float t, bool home)
        {
            float a = Hash01(i) * MathX.Tau, r = 12f + 58f * Hash01(i + 100), w = (1.5f + 2f * Hash01(i + 200)) * (i % 2 == 0 ? 1f : -1f);
            float k = home ? 0.5f : 1f, ang = a + w * t;
            return center + new V2((float)Math.Cos(ang) * r, (float)Math.Sin(ang * 1.3f) * r * 0.8f) * k;
        }

        static float Hash01(int n)
        {
            double s = Math.Sin(n * 12.9898 + 78.233) * 43758.5453;
            return (float)(s - Math.Floor(s));
        }

        /// Standing in a honey puddle slows you (your own game moves you).
        float HoneySlowAt(V2 pos)
        {
            foreach (var pt in Patches.Values)
                if (pt.Kind == PatchKind.Honey && pt.Active && pt.Pos.DistanceTo(pos) < pt.Radius)
                    return HoneySlow;
            return 1f;
        }

        // --- Moving ---------------------------------------------------------------------------------

        /// Where she flies. She holds still for the sealing cells and the nectar spray (a stream fired on the
        /// move smears), and the dive moves her itself.
        V2 QueenMove(Enemy e, V2 tpos, ref float speed)
        {
            var q = QueenOf(e);
            if (q.TransT > 0f || q.Turn == QueenTurn.LastStand)
            {
                V2 to = q.TransT > 0f ? new V2(Width / 2f, q.TransTo >= QueenStage.Frenzy ? 400f : 150f) : new V2(Width / 2f, Height / 2f);
                speed = q.TransT > 0f ? 380f : 300f;
                return e.Pos.DistanceTo(to) > 6f ? (to - e.Pos).Normalized() : V2.Zero;
            }
            if (q.Turn == QueenTurn.Cells || q.Turn == QueenTurn.Spray || q.Turn == QueenTurn.Dive)
                return V2.Zero;
            V2 goal;
            if (q.Stage == QueenStage.Swarm)  // sways along the top of the arena
                goal = new V2(Width / 2f + (float)Math.Sin(e.T * 0.5f) * Width * 0.3f, 150f + (float)Math.Sin(e.T * 0.8f) * 40f);
            else if (q.Stage == QueenStage.Honeycomb)  // a narrower sway near the top middle
                goal = new V2(Width / 2f + (float)Math.Sin(e.T * 0.5f) * Width * 0.18f, 150f + (float)Math.Sin(e.T * 0.8f) * 30f);
            else  // Frenzy: down in the arena, drifting toward her target
                goal = new V2(Width / 2f + (tpos.X - Width / 2f) * 0.4f, 360f + (float)Math.Sin(e.T * 0.7f) * 60f);
            return (goal - e.Pos) / 60f;
        }

        /// Her drones fly back into her when their turn runs out.
        bool DroneGoingHome(Enemy e)
        {
            if (e.Type != EnemyType.Shard)
                return false;
            var queen = TheQueen();
            return queen != null && queen.Queen != null && queen.Queen.Homing.Contains(e.Id);
        }

        V2 DroneHomeMove(Enemy e, ref float speed)
        {
            var queen = TheQueen();
            speed = 260f;
            return queen == null ? V2.Zero : (queen.Pos - e.Pos).Normalized();
        }

        // --- The fight (host) -----------------------------------------------------------------------

        void QueenSpecial(Enemy e, V2 tpos, float dt)
        {
            var q = QueenOf(e);
            if (q.Step < 0)
                NextQueenTurn(e, q);  // her first turn: the drones
            float frac = e.MaxHp > 0f ? e.Hp / e.MaxHp : 1f;
            TrackPlayerSpeeds(q, dt);
            TickQueenCells(q, dt);
            TickHomingDrones(e, q);

            if (q.TransT > 0f)
            {
                q.TransT -= dt;
                if (q.TransT <= 0f)
                {
                    q.TransT = 0f;
                    EnterQueenStage(e, q.TransTo);
                }
                e.Phase = 0;
                return;
            }
            if (q.Stage == QueenStage.Swarm && frac <= HoneycombAt)
            {
                StartQueenTransition(e, QueenStage.Honeycomb);
                return;
            }
            if (q.Stage == QueenStage.Honeycomb && frac <= FrenzyAt)
            {
                StartQueenTransition(e, QueenStage.Frenzy);
                return;
            }
            if (q.Stage == QueenStage.Frenzy && frac <= QueenLastStandAt && q.Turn != QueenTurn.Dive)
                EnterQueenStage(e, QueenStage.LastStand);  // straight away (a dive finishes first)

            q.TurnT += dt;
            switch (q.Turn)
            {
                case QueenTurn.Drones:
                    q.Royal.RemoveWhere(id => !Enemies.ContainsKey(id));
                    if (q.TurnT > 1f && q.Royal.Count == 0)
                        q.TurnT = q.TurnLen;  // all dead: move on
                    break;
                case QueenTurn.Cells:
                    q.WaveT -= dt;
                    if (q.WaveT <= 0f && q.Wave < 4)
                    {
                        q.WaveT = CellEvery;
                        q.Wave++;
                        MarkHiveCells(q);
                    }
                    break;
                case QueenTurn.Spray:
                    // One stream sweeping 140 degrees back and forth below her.
                    q.Aux += q.SprayDir * SpraySweep * dt;
                    if (q.Aux > 1f || q.Aux < 0f)
                    {
                        q.SprayDir = -q.SprayDir;
                        q.Aux = MathX.Clamp01(q.Aux);
                    }
                    break;
                case QueenTurn.Cloud:
                    TickCloud(e, q, dt);
                    break;
                case QueenTurn.Dive:
                    TickDive(e, q, dt);
                    break;
                case QueenTurn.LastStand:
                    q.BombT -= dt;
                    break;
            }
            e.Phase = (byte)(q.Turn == QueenTurn.Drones && q.Royal.Count > 0 ? 1 : 0);  // shielded while her drones live

            bool over = q.TurnT >= q.TurnLen;
            if (q.Turn == QueenTurn.Cloud)
                over = !q.CloudOn;
            if (q.Turn == QueenTurn.Dive)
                over = q.Dives >= Dives;
            if (q.Stage != QueenStage.LastStand && over)
                NextQueenTurn(e, q);
        }

        void EnterQueenStage(Enemy e, QueenStage stage)
        {
            var q = QueenOf(e);
            q.Stage = stage;
            q.Step = -1;
            if (stage == QueenStage.LastStand)
                q.CloudOn = false;
            NextQueenTurn(e, q);
            DoLog("the Hive Queen: " + stage);
        }

        void StartQueenTransition(Enemy e, QueenStage to)
        {
            var q = QueenOf(e);
            q.TransT = QueenTransTime;
            q.TransTo = to;
            q.Cells.Clear();
            q.CloudOn = false;
            q.Royal.Clear();
            q.Homing.Clear();
            ClearQueenAdds(e);
            ClearEnemyBullets();  // (and the honey)
            session.Broadcast(Proto.End(Proto.Begin(Msg.ClearEnemyBullets)));
            BroadcastFx(FxKind.Ring, e.Pos, to == QueenStage.Frenzy ? new Rgba(1f, 0.4f, 0.25f) : new Rgba(1f, 0.7f, 0.2f), 900f, 0.8f, Sfx.BossChime);
            DoLog("the Hive Queen: changing to " + to);
        }

        /// Her drones, eggs and bombers go when the stage changes (or she dies).
        void ClearQueenAdds(Enemy queen)
        {
            var ids = new List<int>();
            foreach (var o in Enemies.Values)
                if (o.Type == EnemyType.HiveEgg || o.Type == EnemyType.HiveBomber || (o.Type == EnemyType.Shard && o.ParentId == queen.Id))
                    ids.Add(o.Id);
            foreach (int id in ids)
                Enemies.Remove(id);
        }

        void NextQueenTurn(Enemy e, QueenState q)
        {
            var turns = QueenTurns[(int)q.Stage];
            if (q.Turn == QueenTurn.Drones)
            {
                // Whatever's left of her shield flies back into her.
                foreach (int id in q.Royal)
                    if (Enemies.ContainsKey(id))
                        q.Homing.Add(id);
                q.Royal.Clear();
            }
            if (q.Turn == QueenTurn.Eggs)
                HatchEggs(e);
            q.Step = (q.Step + 1) % turns.Length;
            q.Turn = turns[q.Step];
            q.TurnLen = QueenTimes[(int)q.Stage][q.Step];
            q.TurnT = 0f;
            q.Wave = 0;
            q.WaveT = 0f;
            q.Dives = 0;
            q.DiveT = 0f;
            e.FireT = 0.6f;  // a beat before each turn starts firing
            switch (q.Turn)
            {
                case QueenTurn.Drones:
                {
                    int swarm = 0;
                    foreach (var o in Enemies.Values)
                        if (o.ParentId == e.Id && o.Type == EnemyType.Shard)
                            swarm++;
                    int n = Math.Max(0, Math.Min(QueenSwarmCap - swarm, 3 + AlivePlayers()));
                    for (int i = 0; i < n; i++)
                        q.Royal.Add(SpawnDrone(e, e.Pos + V2.FromAngle(e.T + MathX.Tau * i / n) * 70f).Id);
                    BroadcastFx(FxKind.Ring, e.Pos, e.Def.Color, 90f, 0.5f, Sfx.Ability);
                    break;
                }
                case QueenTurn.Eggs:
                    LayEggs(e);
                    break;
                case QueenTurn.Spray:
                    q.Aux = 0f;
                    q.SprayDir = 1f;
                    break;
                case QueenTurn.Cloud:
                    q.CloudOn = true;
                    q.CloudHome = false;
                    q.Cloud = q.CloudDisp = e.Pos + new V2(0f, 60f);
                    break;
                case QueenTurn.Dive:
                    StartDive(e, q);
                    break;
                case QueenTurn.LastStand:
                    q.BombT = 1f;
                    break;
            }
        }

        Enemy SpawnDrone(Enemy queen, V2 at)
        {
            var drone = SpawnEnemyAt(EnemyType.Shard, ClampToArena(at, 20f), 0.5f, queen.Id);
            drone.MaxHp *= QueenSwarmHp;
            drone.Hp = drone.MaxHp;
            return drone;
        }

        /// Her guns, per turn (EnemyFire calls this when its timer runs out).
        void QueenFire(Enemy e, V2 tpos)
        {
            var q = QueenOf(e);
            float aim = (tpos - e.Pos).Angle;
            if (q.TransT > 0f)
            {
                e.FireT = 0.2f;
                return;
            }
            switch (q.Turn)
            {
                case QueenTurn.Bombers:  // one explosive drone per player, at where they are right now
                    e.FireT = 0.9f;
                    foreach (var p in Players.Values)
                        if (!p.Downed)
                            LaunchBomber(e, p.Pos);
                    break;
                case QueenTurn.Fan:  // aimed fan, with gaps wide enough to slip through even up close
                    e.FireT = 0.9f;
                    for (int i = 0; i < 5; i++)
                        EnemyShot(e, aim + (i - 2) * 0.22f, 200f, Defs.PierceShot);
                    break;
                case QueenTurn.Rings:
                    e.FireT = 1.6f;
                    e.Ang += 0.15f;
                    for (int i = 0; i < 20; i++)
                        EnemyShot(e, e.Ang + MathX.Tau * i / 20f, 100f, Defs.BigShot);
                    break;
                case QueenTurn.Spray:  // honey globs; every third leaves a sticky puddle where it lands
                {
                    e.FireT = SprayEvery;
                    if (q.TurnT < 0.5f)
                        break;
                    float a = (20f + 140f * q.Aux) * MathX.Pi / 180f, land = Rand(250f, 600f);
                    V2 dir = V2.FromAngle(a), from = e.Pos + dir * e.Radius;
                    float life = land / SpraySpeed;
                    SpawnBullet(Defs.HoneyShot, from, dir * SpraySpeed, 0f, life, Defs.BulletDamage(Defs.HoneyShot), 0, true).Source = e.Type;
                    if (++q.Globs % 3 == 0)
                    {
                        V2 at = from + dir * land;
                        if (at.X > 0f && at.X < Width && at.Y > 0f && at.Y < Height)
                            SpawnPatchAt(PatchKind.Honey, at, life, life + HoneyLife, HoneyRadius, HoneyRadius);
                    }
                    break;
                }
                case QueenTurn.Barrage:  // two per player: where you are, and where you're heading
                    e.FireT = 0.9f;
                    foreach (var p in Players.Values)
                    {
                        if (p.Downed)
                            continue;
                        V2 vel;
                        q.Vel.TryGetValue(p.Id, out vel);
                        LaunchBomber(e, p.Pos);
                        LaunchBomber(e, ClampToArena(p.Pos + vel * 0.84f, 30f));
                    }
                    break;
                case QueenTurn.LastStand:  // in the middle: a three-armed spiral, and a bomber at everyone every 2 s
                    e.FireT = 0.09f;
                    if (e.Pos.DistanceTo(new V2(Width / 2f, Height / 2f)) > 8f)
                        break;
                    e.Ang += 0.21f;
                    for (int k = 0; k < 3; k++)
                        EnemyShot(e, e.Ang + MathX.Tau * k / 3f, 150f, Defs.SmallShot);
                    if (q.BombT <= 0f)
                    {
                        q.BombT = 2f;
                        foreach (var p in Players.Values)
                            if (!p.Downed)
                                LaunchBomber(e, p.Pos);
                    }
                    break;
                default:  // drones, eggs, cells, the cloud, the dive and breathers: no guns
                    e.FireT = 0.1f;
                    break;
            }
        }

        void TrackPlayerSpeeds(QueenState q, float dt)
        {
            if (dt <= 0f)
                return;
            foreach (var p in Players.Values)
            {
                V2 last, vel;
                if (q.LastPos.TryGetValue(p.Id, out last))
                {
                    q.Vel.TryGetValue(p.Id, out vel);
                    V2 now = (p.Pos - last) / dt;
                    if (now.Length > 600f)
                        now = V2.Zero;  // a teleport or wrap, not running
                    q.Vel[p.Id] = V2.Lerp(vel, now, Math.Min(1f, dt * 6f));
                }
                q.LastPos[p.Id] = p.Pos;
            }
        }

        /// Drones that make it back into her heal her.
        void TickHomingDrones(Enemy e, QueenState q)
        {
            if (q.Homing.Count == 0)
                return;
            var done = new List<int>();
            foreach (int id in q.Homing)
            {
                Enemy d;
                if (!Enemies.TryGetValue(id, out d))
                {
                    done.Add(id);
                    continue;
                }
                if (d.Pos.DistanceTo(e.Pos) < e.Radius + d.Radius)
                {
                    e.Hp = Math.Min(e.MaxHp, e.Hp + e.MaxHp * DroneHeal);
                    BroadcastFx(FxKind.Ring, e.Pos, new Rgba(0.55f, 1f, 0.55f), e.Radius + 30f, 0.5f, Sfx.Pick);
                    Enemies.Remove(id);
                    done.Add(id);
                }
            }
            foreach (int id in done)
                q.Homing.Remove(id);
        }

        // --- Honeycomb: eggs and sealing cells ------------------------------------------------------

        void LayEggs(Enemy e)
        {
            var spots = new List<V2>();
            for (int k = 0; k < 60 && spots.Count < EggCount; k++)
            {
                var s = new V2(Rand(180f, Width - 180f), Rand(300f, Height - 120f));
                bool clear = true;
                foreach (var o in spots)
                    clear &= o.DistanceTo(s) > 330f;
                if (clear)
                    spots.Add(s);
            }
            foreach (var s in spots)
            {
                SpawnEnemyAt(EnemyType.HiveEgg, s, EggFlight, e.Id);
                BroadcastFx(FxKind.Target, s, Defs.Get(EnemyType.HiveEgg).Color, 24f, EggFlight, Sfx.Throw);
            }
        }

        /// The eggs nobody broke hatch into drones as their turn ends.
        void HatchEggs(Enemy queen)
        {
            var eggs = new List<Enemy>();
            foreach (var o in Enemies.Values)
                if (o.Type == EnemyType.HiveEgg)
                    eggs.Add(o);
            foreach (var egg in eggs)
            {
                Enemies.Remove(egg.Id);
                for (int i = 0; i < DronesPerEgg; i++)
                    SpawnDrone(queen, egg.Pos + V2.FromAngle(MathX.Tau * i / DronesPerEgg) * 16f);
                BroadcastFx(FxKind.Ring, egg.Pos, new Rgba(0.8f, 1f, 0.55f), 50f, 0.5f, Sfx.Hit);
            }
        }

        /// About a quarter of the cells start glowing; your own cell often does, and one cell next to each player is
        /// always left safe, so there's somewhere to step.
        void MarkHiveCells(QueenState q)
        {
            var keep = new HashSet<int>();
            var mine = new HashSet<int>();
            foreach (var p in Players.Values)
            {
                if (p.Downed)
                    continue;
                int c = HiveCellAt(p.Pos);
                mine.Add(c);
                var near = new List<int>();
                for (int i = 0; i < HiveCells.Length; i++)
                    if (i != c && HiveCells[i].DistanceTo(HiveCells[c]) < HiveCell * 1.9f)
                        near.Add(i);
                if (near.Count > 0)
                    keep.Add(near[rng.Next(near.Count)]);
            }
            for (int i = 0; i < HiveCells.Length; i++)
            {
                if (keep.Contains(i) || HiveCells[i].Y < 40f)
                    continue;
                if (rng.NextDouble() < CellShare || (mine.Contains(i) && rng.NextDouble() < 0.7))
                    q.Cells.Add(new QueenCell { Index = (ushort)i });
            }
            Sounds.Add(Sfx.BossPulse);
        }

        /// Every game moves the cells' clocks; the host seals them and hurts whoever is inside.
        void TickQueenCells(QueenState q, float dt)
        {
            foreach (var c in q.Cells)
            {
                c.T += dt;
                if (!IsHost || c.Hit || c.T < CellWarn)
                    continue;
                c.Hit = true;
                foreach (var p in Players.Values)
                    if (!p.Downed && p.InvulnT <= 0f && InHexCell(HiveCells[c.Index], p.Pos, HiveCell - 4f))
                        Damage(p, CellDamage, "Hive Queen (cells)");
            }
            q.Cells.RemoveAll(c => c.T >= CellLife);
        }

        // --- Frenzy: the swarm cloud and the royal dive ---------------------------------------------

        void TickCloud(Enemy e, QueenState q, float dt)
        {
            if (!q.CloudOn)
                return;
            if (q.TurnT >= CloudTime)
                q.CloudHome = true;
            V2 goal = e.Pos;
            if (!q.CloudHome)
            {
                Player nearest = null;
                foreach (var p in Players.Values)
                    if (!p.Downed && (nearest == null || p.Pos.DistanceSq(q.Cloud) < nearest.Pos.DistanceSq(q.Cloud)))
                        nearest = p;
                if (nearest == null)
                    q.CloudHome = true;
                else
                    goal = nearest.Pos;
            }
            q.Cloud += (goal - q.Cloud).LimitLength((q.CloudHome ? CloudHomeSpeed : CloudSpeed) * dt);
            if (q.CloudHome)
            {
                if (q.Cloud.DistanceTo(e.Pos) < 30f)
                    q.CloudOn = false;
                return;
            }
            for (int i = 0; i < BeeCount; i++)
            {
                V2 b = BeePos(i, q.Cloud, q.TurnT, false);
                foreach (var p in Players.Values)
                    if (!p.Downed && p.InvulnT <= 0f && p.Pos.DistanceTo(b) < BeeRadius + p.Def.HitR)
                        Damage(p, BeeDamage, "Hive Queen (swarm)");
            }
        }

        void StartDive(Enemy e, QueenState q)
        {
            q.DiveT = 0f;
            q.Landed = false;
            q.DiveFrom = e.Pos;
            var t = PlayerOrNull(e.TargetId);
            q.Shadow = t != null && !t.Downed ? t.Pos : e.Pos;
        }

        /// Her shadow follows her target for a moment, then stays put; she flies up, comes down on it, hurts whoever
        /// is under it and throws out a ring of orbs. Three times.
        void TickDive(Enemy e, QueenState q, float dt)
        {
            if (q.Dives >= Dives)
                return;
            q.DiveT += dt;
            var t = PlayerOrNull(e.TargetId);
            if (q.DiveT < DiveTrack && t != null && !t.Downed)
                q.Shadow = ClampToArena(q.Shadow + (t.Pos - q.Shadow) * Math.Min(1f, dt * 8f), e.Radius);
            if (q.DiveT >= 0.3f && q.DiveT < DiveLand)
                e.Pos = V2.Lerp(q.DiveFrom, q.Shadow, MathX.Clamp01((q.DiveT - 0.3f) / 0.9f));
            if (q.DiveT >= DiveLand && !q.Landed)
            {
                q.Landed = true;
                e.Pos = q.Shadow;
                foreach (var p in Players.Values)
                    if (!p.Downed && p.InvulnT <= 0f && p.Pos.DistanceTo(e.Pos) < DiveRadius + p.Def.HitR)
                        Damage(p, DiveDamage, "Hive Queen (dive)");
                e.Ang += 0.17f;
                for (int i = 0; i < 18; i++)
                    EnemyShot(e, e.Ang + MathX.Tau * i / 18f, 110f, Defs.BigShot);
                BroadcastFx(FxKind.Ring, e.Pos, new Rgba(1f, 0.45f, 0.25f), DiveRadius + 60f, 0.45f, Sfx.Explode);
            }
            if (q.DiveT >= DiveTime)
            {
                q.Dives++;
                if (q.Dives < Dives)
                    StartDive(e, q);
            }
        }

        // --- Replication ----------------------------------------------------------------------------

        void SyncQueen(NetSync s, Enemy e)
        {
            bool has = e.Queen != null;
            s.Bool(ref has);
            if (!has)
                return;
            var q = e.Queen ?? (e.Queen = new QueenState());
            int stage = (int)q.Stage, turn = (int)q.Turn, transTo = (int)q.TransTo, dives = q.Dives;
            bool wasOn = q.CloudOn;
            s.Byte(ref stage);
            s.Byte(ref turn);
            s.Float(ref q.TurnT);
            s.Float(ref q.TransT);
            s.Byte(ref transTo);
            s.Float(ref q.Aux);
            s.Bool(ref q.CloudOn);
            s.Bool(ref q.CloudHome);
            s.Vec(ref q.Cloud);
            s.Vec(ref q.Shadow);
            s.Float(ref q.DiveT);
            s.Byte(ref dives);
            if (s.Reading)
            {
                q.Stage = (QueenStage)stage;
                q.Turn = (QueenTurn)turn;
                q.TransTo = (QueenStage)transTo;
                q.Dives = dives;
                if (q.CloudOn && !wasOn)
                    q.CloudDisp = q.Cloud;  // just let loose: don't slide in from where it was
            }
            int n = q.Cells.Count;
            s.Short(ref n);
            if (s.Reading)
            {
                q.Cells.Clear();
                for (int i = 0; i < n; i++)
                    q.Cells.Add(new QueenCell { Hit = true });
            }
            foreach (var c in q.Cells)
            {
                int idx = c.Index;
                s.Short(ref idx);
                s.Float(ref c.T);
                c.Index = (ushort)Math.Max(0, Math.Min(HiveCells.Length - 1, idx));
            }
        }

        /// Every game, each frame: smooth the swarm cloud and (on clients) run the clocks between snapshots.
        void UpdateQueenView(float dt)
        {
            var e = TheQueen();
            if (e == null || e.Queen == null)
                return;
            var q = e.Queen;
            if (!IsHost)
            {
                q.TurnT += dt;
                if (q.Turn == QueenTurn.Dive && q.Dives < Dives)
                    q.DiveT += dt;
                TickQueenCells(q, dt);
            }
            q.CloudDisp = V2.Lerp(q.CloudDisp, q.Cloud, 1f - (float)Math.Exp(-14.0 * dt));
        }
    }
}
