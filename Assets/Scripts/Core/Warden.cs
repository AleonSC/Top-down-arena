using System;
using System.Collections.Generic;
using System.IO;

namespace Arena
{
    /// The Warden's stage of the fight, by its health: a prison riot in three phases, then a last stand.
    public enum WardenStage : byte { Count, Lockdown, Riot, LastStand }

    /// What the Warden is doing right now. Each stage loops through its turns, one mechanic at a time,
    /// with a short breather (Rest) between them.
    public enum WardenTurn : byte { Rest, Rings, Spiral, Fan, Light, Cross, Rioters, Charge, LastStand }

    /// The searchlight: off, hunting a player, locked on, or dark while its shackle ring closes.
    public enum WardenLight : byte { Off, Follow, Lock, Ring }

    /// A gate in the Lockdown: five rows of push bullets sweeping across the arena with two door gaps.
    /// You can't get through it: it shoves you back out the side you came from (each player's own game moves
    /// them; see WardenGatePush), and carries you along if it keeps coming.
    public class WardenGate
    {
        public float Front, Dir, Gap1, Gap2, Warn;            // replicated; the front row moves Dir (+1 down, -1 up)
        public readonly HashSet<int> Hit = new HashSet<int>();     // host: players it has hurt (once each)...
        public readonly HashSet<int> Rolled = new HashSet<int>();  // ...and rolled over against the edge (once more)
    }

    /// Everything about the Warden's fight that its body doesn't already carry. The host runs it; clients get it
    /// in the snapshot (WriteWarden / ReadWarden) to draw the searchlight, gates, doors, shield and charge.
    public class WardenState
    {
        public WardenStage Stage;
        public WardenTurn Turn;
        public float TurnT;              // seconds into the current turn
        public float TransT;             // > 0: changing stage (can't be hurt), counting down
        public WardenStage TransTo;
        public V2 Light, LightDisp;      // the searchlight's pool (LightDisp is smoothed for drawing)
        public WardenLight LightMode;
        public float LightT;             // seconds in the current light mode
        public float Aux;                // the riot shield's facing, or the baton charge's direction
        public bool Dashing;             // the cross: moving to its next plant (not firing)
        public readonly List<WardenGate> Gates = new List<WardenGate>();

        // Host only.
        public int Step, Plant = -1, GatesSent, DoorsOpened;
        public float TurnLen, Caught, RingT, GasTickT, ChargeLeft;
        public readonly List<int> Shackle = new List<int>();   // the bullets of the closing ring
    }

    public partial class Game
    {
        // Health at which each stage starts.
        const float LockdownAt = 0.65f, RiotAt = 0.3f, LastStandAt = 0.1f;
        const float WardenTransTime = 2f;
        // The searchlight (Count): follows its target at 150 px/s (slower than every class), the Warden fires a stun
        // shot every 0.75 s; standing in the bright middle for 0.25 s gets you caught: 0.6 s lock-on, then a shackle
        // ring of 28 big orbs 420 px out closes in at 110 px/s, with a 4-orb gap facing away from the Warden.
        public const float LightRadius = 105f;
        const float LightSpeed = 150f, LightCatch = 0.25f, LightLock = 0.6f, StunEvery = 0.75f, StunShotSpeed = 280f;
        public const float StunTime = 1f;
        const int ShackleOrbs = 28, ShackleGap = 4;
        const float ShackleRadius = 420f, ShackleSpeed = 110f;
        // The gates (Lockdown): three per cross turn, 4 s apart, alternating top and bottom.
        public const int GateRows = 5;
        public const float GateRowGap = 16f, GateBulletR = 7f, GateSpeed = 180f, GateDoor = 90f, GateWarn = 0.8f;
        const float GateDamage = 12f, GateEvery = 4f;
        // Gas canisters: with the 1st and 3rd gate, near the target; a cloud 90 px wide for 5 s, 8 damage a second.
        const float GasRadius = 90f, GasFlight = 1f, GasLife = 5f, GasDps = 8f;
        // The riot: doors burst open, rioters pour out; the baton charge.
        const int RiotersPerDoor = 3;
        const float RioterHit = 0.006f;  // a rioter that reaches the Warden takes this much of its health
        const float ShieldHalfArc = 1.05f, ShieldTurn = 1.05f;
        const float ChargeWarn = 0.9f, ChargeSpeed = 700f, ChargeLength = 620f;

        // Each stage's loop of turns (seconds). A Rest is a breather between mechanics.
        static readonly WardenTurn[][] StageTurns =
        {
            new[] { WardenTurn.Rings, WardenTurn.Rest, WardenTurn.Spiral, WardenTurn.Rest, WardenTurn.Fan, WardenTurn.Rest, WardenTurn.Light, WardenTurn.Rest },
            new[] { WardenTurn.Cross, WardenTurn.Rest },
            new[] { WardenTurn.Rioters, WardenTurn.Rest, WardenTurn.Charge, WardenTurn.Rest },
            new[] { WardenTurn.LastStand },
        };
        static readonly float[][] StageTimes =
        {
            new[] { 5f, 1f, 6f, 1f, 4f, 2f, 6f, 1f },  // the fast fan last, so the arena is clear when the light comes on
            new[] { 12f, 2f },
            new[] { 10f, 1f, ChargeWarn + ChargeLength / ChargeSpeed + 0.3f, 1f },
            new[] { 999f },
        };

        /// The doors along the arena's walls that burst open in the riot (left, right and bottom).
        public static readonly V2[] RiotDoors =
        {
            new V2(0f, 260f), new V2(0f, 640f), new V2(Width, 260f), new V2(Width, 640f), new V2(500f, Height), new V2(1100f, Height),
        };

        WardenState WardenOf(Enemy e) => e.Warden ?? (e.Warden = new WardenState { TurnLen = StageTimes[0][0], Turn = StageTurns[0][0] });

        /// The Warden in play, if any (there's only ever one).
        public Enemy TheWarden()
        {
            foreach (var e in Enemies.Values)
                if (e.Type == EnemyType.Boss)
                    return e;
            return null;
        }

        bool WardenImmune(Enemy e) => e.Type == EnemyType.Boss && e.Warden != null && e.Warden.TransT > 0f;

        /// The riot shield stops shots from the front (120 degrees).
        bool WardenShieldBlocks(Enemy e, V2 from)
        {
            var w = e.Warden;
            if (w == null || w.Turn != WardenTurn.Rioters || w.TransT > 0f)
                return false;
            float diff = Math.Abs(MathX.Fmod((from - e.Pos).Angle - w.Aux + MathX.Pi, MathX.Tau) - MathX.Pi);
            return diff < ShieldHalfArc;
        }

        // --- Moving ---------------------------------------------------------------------------------

        /// Where the Warden walks. It holds still while firing a stream (the spiral, the cross's plants, the
        /// last stand), because a stream fired on the move smears; for everything else it keeps moving.
        V2 WardenMove(Enemy e, V2 tpos, ref float speed)
        {
            var w = WardenOf(e);
            V2 middle = new V2(Width / 2f, Height / 2f);
            if (w.TransT > 0f || w.Turn == WardenTurn.LastStand)
            {
                speed = 420f;
                return e.Pos.DistanceTo(middle) > 6f ? (middle - e.Pos).Normalized() : V2.Zero;
            }
            switch (w.Turn)
            {
                case WardenTurn.Spiral:
                    return V2.Zero;
                case WardenTurn.Cross:
                    speed = 340f;
                    return w.Dashing && e.Pos.DistanceTo(e.Waypoint) > 8f ? (e.Waypoint - e.Pos).Normalized() : V2.Zero;
                case WardenTurn.Charge:
                    if (w.TurnT < ChargeWarn || w.ChargeLeft <= 0f)
                        return V2.Zero;
                    speed = ChargeSpeed;
                    return V2.FromAngle(w.Aux);
            }
            V2 goal;
            if (w.Stage == WardenStage.Count)  // sways back and forth through the middle of the arena
                goal = new V2(Width / 2f + (float)Math.Sin(e.T * 0.5f) * Width * 0.3f, Height / 2f + (float)Math.Sin(e.T * 0.8f) * 40f);
            else if (w.Stage == WardenStage.Lockdown)  // between crosses, near the middle
                goal = new V2(Width / 2f + (float)Math.Cos(e.T * 0.4f) * 120f, Height / 2f + (float)Math.Sin(e.T * 0.4f) * 80f);
            else  // the riot: works its way round its target, about 320 px out
                goal = tpos + V2.FromAngle(e.T * 0.25f) * 320f;
            return (goal - e.Pos) / 60f;
        }

        // --- The fight (host) -----------------------------------------------------------------------

        void WardenSpecial(Enemy e, V2 tpos, float dt)
        {
            var w = WardenOf(e);
            Player target;
            Players.TryGetValue(e.TargetId, out target);
            float frac = e.MaxHp > 0f ? e.Hp / e.MaxHp : 1f;

            TickWardenGates(w, dt);
            TickWardenGas(w, dt);

            if (w.TransT > 0f)
            {
                w.TransT -= dt;
                if (w.TransT <= 0f)
                {
                    w.TransT = 0f;
                    EnterWardenStage(e, w.TransTo);
                }
                e.Phase = (byte)w.Turn;
                return;
            }
            if (w.Stage == WardenStage.Count && frac <= LockdownAt)
            {
                StartWardenTransition(e, WardenStage.Lockdown);
                return;
            }
            if (w.Stage == WardenStage.Lockdown && frac <= RiotAt)
            {
                StartWardenTransition(e, WardenStage.Riot);
                return;
            }
            if (w.Stage == WardenStage.Riot && frac <= LastStandAt && w.Turn != WardenTurn.Charge)
                EnterWardenStage(e, WardenStage.LastStand);  // straight away (a charge finishes first); the rioters scatter

            w.TurnT += dt;
            switch (w.Turn)
            {
                case WardenTurn.Light:
                    TickSearchlight(e, w, target, dt);
                    break;
                case WardenTurn.Cross:
                {
                    // Each 3 s: dash 1 s to a new spot in the middle of the arena, then plant and fire for 2 s.
                    int plant = (int)(w.TurnT / CrossPlant);
                    if (plant != w.Plant)
                    {
                        w.Plant = plant;
                        for (int tries = 0; tries < 8; tries++)
                        {
                            e.Waypoint = new V2(Rand(Width * 0.28f, Width * 0.72f), Rand(Height * 0.3f, Height * 0.7f));
                            float d = e.Waypoint.DistanceTo(e.Pos);
                            if (d > 160f && d < 330f) break;
                        }
                    }
                    w.Dashing = MathX.Fmod(w.TurnT, CrossPlant) < CrossDash;
                    // At the same time, the gates: three, 4 s apart, top then bottom then top; gas with the 1st and 3rd.
                    if (w.GatesSent < 3 && w.TurnT >= 0.2f + w.GatesSent * GateEvery)
                    {
                        AddWardenGate(w, w.GatesSent % 2 == 0 ? 1f : -1f);
                        if (w.GatesSent != 1)
                            LobGas(e, target);
                        w.GatesSent++;
                    }
                    break;
                }
                case WardenTurn.Rioters:
                {
                    // Two doors burst open at once, two more 3 s later; the Warden holds its shield toward its target.
                    if (w.DoorsOpened == 0 || (w.DoorsOpened == 2 && w.TurnT >= 3f))
                        OpenRiotDoors(w, 2);
                    if (target != null)
                    {
                        float want = (target.Pos - e.Pos).Angle, diff = MathX.Fmod(want - w.Aux + MathX.Pi, MathX.Tau) - MathX.Pi;
                        w.Aux += Math.Max(-ShieldTurn * dt, Math.Min(ShieldTurn * dt, diff));
                    }
                    if (w.TurnT > 4f && CountRioters() == 0)
                        w.TurnT = w.TurnLen;  // all dealt with: on to the charge
                    break;
                }
                case WardenTurn.Charge:
                    if (w.TurnT >= ChargeWarn && w.ChargeLeft > 0f)
                    {
                        // MoveEnemy moves it; count the distance, and stop at a wall.
                        w.ChargeLeft -= ChargeSpeed * dt;
                        V2 next = e.Pos + V2.FromAngle(w.Aux) * ChargeSpeed * dt;
                        if (ClampToArena(next, e.Radius).DistanceTo(next) > 1f)
                            w.ChargeLeft = 0f;
                    }
                    break;
                case WardenTurn.LastStand:
                    w.RingT -= dt;
                    break;
            }
            e.Phase = (byte)w.Turn;
            if (w.Turn == WardenTurn.Rioters || w.Turn == WardenTurn.Charge)
                e.Facing = w.Aux;
            RiotersReachWarden(e);

            bool waitForRing = w.Turn == WardenTurn.Light && (w.LightMode == WardenLight.Lock || w.LightMode == WardenLight.Ring);
            if (w.Stage != WardenStage.LastStand && w.TurnT >= w.TurnLen && !waitForRing)
                NextWardenTurn(e, w, target);
        }

        void EnterWardenStage(Enemy e, WardenStage stage)
        {
            var w = WardenOf(e);
            w.Stage = stage;
            w.Step = -1;
            if (stage == WardenStage.LastStand)
                ClearRioters();
            NextWardenTurn(e, w, null);
            DoLog("the Warden: " + stage);
        }

        void StartWardenTransition(Enemy e, WardenStage to)
        {
            var w = WardenOf(e);
            w.TransT = WardenTransTime;
            w.TransTo = to;
            w.LightMode = WardenLight.Off;
            w.Gates.Clear();
            w.Shackle.Clear();
            ClearRioters();
            ClearEnemyBullets();  // (and the gas)
            session.Broadcast(Proto.End(Proto.Begin(Msg.ClearEnemyBullets)));
            BroadcastFx(FxKind.Ring, e.Pos, to == WardenStage.Riot ? new Rgba(1f, 0.5f, 0.2f) : new Rgba(0.35f, 0.65f, 1f), 900f, 0.8f, Sfx.BossChime);
            if (to == WardenStage.Riot)
                foreach (var d in RiotDoors)
                    BroadcastFx(FxKind.Boom, d, new Rgba(1f, 0.55f, 0.2f), 90f, 0.6f, Sfx.None);
            DoLog("the Warden: changing to " + to);
        }

        void NextWardenTurn(Enemy e, WardenState w, Player target)
        {
            var turns = StageTurns[(int)w.Stage];
            w.Step = (w.Step + 1) % turns.Length;
            if (w.Turn == WardenTurn.Rioters)
                ClearRioters();  // whoever's left scatters
            w.Turn = turns[w.Step];
            w.TurnLen = StageTimes[(int)w.Stage][w.Step];
            w.TurnT = 0f;
            w.Dashing = false;
            w.Plant = -1;
            w.GatesSent = 0;
            w.DoorsOpened = 0;
            w.LightMode = WardenLight.Off;
            e.FireT = 0.3f;
            switch (w.Turn)
            {
                case WardenTurn.Light:
                    w.LightMode = WardenLight.Follow;
                    w.LightT = 0f;
                    w.Caught = 0f;
                    w.Light = w.LightDisp = new V2(Width / 2f, 110f);  // comes out of the watchtower at the top
                    break;
                case WardenTurn.Cross:
                    w.Dashing = true;
                    break;
                case WardenTurn.Charge:
                {
                    var t = target ?? PlayerOrNull(e.TargetId);
                    w.Aux = t != null ? (t.Pos - e.Pos).Angle : (new V2(Width / 2f, Height / 2f) - e.Pos).Angle;
                    w.ChargeLeft = ChargeLength;
                    break;
                }
                case WardenTurn.Rioters:
                {
                    var t = target ?? PlayerOrNull(e.TargetId);
                    w.Aux = t != null ? (t.Pos - e.Pos).Angle : 0f;
                    break;
                }
            }
        }

        /// The Warden's guns, per turn (EnemyFire calls this when its timer runs out).
        void WardenFire(Enemy e, V2 tpos)
        {
            var w = WardenOf(e);
            float aim = (tpos - e.Pos).Angle;
            if (w.TransT > 0f)
            {
                e.FireT = 0.2f;
                return;
            }
            switch (w.Turn)
            {
                case WardenTurn.Rings:  // slow rings of big orbs
                    e.FireT = 1f;
                    e.Ang += 0.12f;
                    for (int i = 0; i < 26; i++)
                        EnemyShot(e, e.Ang + MathX.Tau * i / 26f, 110f, Defs.BigShot);
                    break;
                case WardenTurn.Spiral:  // four-armed spiral, standing still
                    e.FireT = 0.07f;
                    e.Ang += 0.19f;
                    for (int k = 0; k < 4; k++)
                        EnemyShot(e, e.Ang + MathX.Tau * k / 4f, 150f, Defs.SmallShot);
                    break;
                case WardenTurn.Fan:  // aimed fan
                    e.FireT = 0.45f;
                    for (int i = 0; i < 9; i++)
                        EnemyShot(e, aim + (i - 4) * 0.12f, 210f, Defs.PierceShot);
                    break;
                case WardenTurn.Light:  // stun shots, one at a time, while the light is still hunting
                    e.FireT = StunEvery;
                    if (w.LightMode == WardenLight.Follow && w.TurnT > 0.6f)
                        EnemyShot(e, aim, StunShotSpeed, Defs.StunShot);
                    break;
                case WardenTurn.Cross:  // planted: straight streams up, down, left and right
                    e.FireT = 0.075f;
                    if (!w.Dashing)
                        for (int k = 0; k < 4; k++)
                            EnemyShot(e, MathX.Pi / 2f * k, 240f, Defs.SmallShot);
                    break;
                case WardenTurn.Rioters:  // a slow shotgun from behind the shield
                    e.FireT = 1.4f;
                    if (w.TurnT > 0.6f)
                        for (int i = 0; i < 7; i++)
                            EnemyShot(e, w.Aux + (i - 3) * 0.11f, 230f, Defs.SmallShot);
                    break;
                case WardenTurn.Charge:  // a trail of shots to both sides as it charges
                    e.FireT = 0.035f;
                    if (w.TurnT >= ChargeWarn && w.ChargeLeft > 0f)
                    {
                        SpawnBullet(Defs.SmallShot, e.Pos, V2.FromAngle(w.Aux + MathX.Pi / 2f) * 90f, 0f, 8f, Defs.BulletDamage(Defs.SmallShot), 0, true).Source = e.Type;
                        SpawnBullet(Defs.SmallShot, e.Pos, V2.FromAngle(w.Aux - MathX.Pi / 2f) * 90f, 0f, 8f, Defs.BulletDamage(Defs.SmallShot), 0, true).Source = e.Type;
                    }
                    break;
                case WardenTurn.LastStand:  // in the middle: the spiral, plus an orb ring every 1.2 s
                    e.FireT = 0.08f;
                    if (e.Pos.DistanceTo(new V2(Width / 2f, Height / 2f)) > 8f)
                        break;
                    e.Ang += 0.19f;
                    for (int k = 0; k < 4; k++)
                        EnemyShot(e, e.Ang + MathX.Tau * k / 4f, 150f, Defs.SmallShot);
                    if (w.RingT <= 0f)
                    {
                        w.RingT = 1.2f;
                        for (int i = 0; i < 26; i++)
                            EnemyShot(e, e.Ang * 0.5f + MathX.Tau * i / 26f, 110f, Defs.BigShot);
                    }
                    break;
                default:  // a breather
                    e.FireT = 0.1f;
                    break;
            }
        }

        // --- The searchlight ------------------------------------------------------------------------

        void TickSearchlight(Enemy e, WardenState w, Player target, float dt)
        {
            w.LightT += dt;
            switch (w.LightMode)
            {
                case WardenLight.Follow:
                {
                    if (target != null && !target.Downed)
                        w.Light += (target.Pos - w.Light).LimitLength(LightSpeed * dt);
                    // Anyone standing in its bright middle gets caught.
                    bool inside = false;
                    foreach (var p in Players.Values)
                        if (!p.Downed && p.Pos.DistanceTo(w.Light) < LightRadius * 0.6f)
                            inside = true;
                    w.Caught = inside ? w.Caught + dt : Math.Max(0f, w.Caught - dt * 1.5f);
                    if (w.Caught >= LightCatch)
                    {
                        w.LightMode = WardenLight.Lock;
                        w.LightT = 0f;
                        w.Caught = 0f;
                        Sounds.Add(Sfx.BossCharge);
                        DoLog("the searchlight caught someone");
                    }
                    break;
                }
                case WardenLight.Lock:
                    if (target != null && !target.Downed)
                        w.Light += (target.Pos - w.Light).LimitLength(LightSpeed * dt);
                    if (w.LightT >= LightLock)
                    {
                        SpawnShackle(e, w, w.Light);
                        w.LightMode = WardenLight.Ring;
                        w.LightT = 0f;
                    }
                    break;
                case WardenLight.Ring:
                    // Dark, and no stun shots, until the ring has closed.
                    w.Shackle.RemoveAll(id => !Bullets.ContainsKey(id));
                    if (w.Shackle.Count == 0)
                    {
                        w.LightMode = WardenLight.Follow;
                        w.LightT = 0f;
                    }
                    break;
            }
        }

        /// A ring of big orbs round where someone was caught, closing in, with a gap facing as far from the
        /// Warden as it can while still opening into the arena.
        void SpawnShackle(Enemy e, WardenState w, V2 at)
        {
            float toWarden = (e.Pos - at).Angle, away = toWarden + MathX.Pi, best = -9f;
            for (int k = 0; k < 24; k++)
            {
                float a = MathX.Tau * k / 24f;
                V2 g = at + V2.FromAngle(a) * 240f;
                if (g.X < 70f || g.X > Width - 70f || g.Y < 70f || g.Y > Height - 70f)
                    continue;
                float score = -(float)Math.Cos(a - toWarden);
                if (score > best)
                {
                    best = score;
                    away = a;
                }
            }
            w.Shackle.Clear();
            float life = (ShackleRadius - 18f) / ShackleSpeed;
            for (int i = 0; i < ShackleOrbs; i++)
            {
                float a = away + MathX.Tau * i / ShackleOrbs;
                float off = Math.Abs(MathX.Fmod(a - away + MathX.Pi, MathX.Tau) - MathX.Pi);
                if (off < MathX.Tau * (ShackleGap / 2f + 0.1f) / ShackleOrbs)
                    continue;  // the gap
                V2 d = V2.FromAngle(a);
                var b = SpawnBullet(Defs.BigShot, at + d * ShackleRadius, -d * ShackleSpeed, 0f, life, Defs.BulletDamage(Defs.BigShot), 0, true);
                b.Source = e.Type;
                w.Shackle.Add(b.Id);
            }
            BroadcastFx(FxKind.Ring, at, new Rgba(0.85f, 0.35f, 1f), ShackleRadius, 0.5f, Sfx.BossPulse);
        }

        // --- The gates and the gas ------------------------------------------------------------------

        void AddWardenGate(WardenState w, float dir)
        {
            var g = new WardenGate { Dir = dir, Front = dir > 0f ? 6f : Height - 6f, Warn = GateWarn };
            // The door gaps sit near the side walls, outside where the cross ever plants (28-72% of the width),
            // so its vertical stream can never block one: every quadrant the cross makes has a way through.
            g.Gap1 = Rand(70f, Width * 0.28f - GateDoor - 40f);
            g.Gap2 = Rand(Width * 0.72f + 40f, Width - GateDoor - 70f);
            w.Gates.Add(g);
        }

        public static float GateBack(WardenGate g) => g.Front - g.Dir * (GateRows - 1) * GateRowGap;

        public static bool InGateGap(WardenGate g, float x) => (x > g.Gap1 + 4f && x < g.Gap1 + GateDoor - 4f) || (x > g.Gap2 + 4f && x < g.Gap2 + GateDoor - 4f);

        /// Moves the gates along (every game: the host, and clients between snapshots).
        static void AdvanceGates(WardenState w, float dt)
        {
            foreach (var g in w.Gates)
            {
                if (g.Warn > 0f)
                    g.Warn -= dt;
                else
                    g.Front += g.Dir * GateSpeed * dt;
            }
            w.Gates.RemoveAll(g => g.Dir > 0f ? GateBack(g) > Height + 20f : GateBack(g) < -20f);
        }

        /// Host: move the gates and hurt whoever they touch: once per gate, and once more if it rolls over you
        /// against the edge. (The shove itself happens on each player's own game.)
        void TickWardenGates(WardenState w, float dt)
        {
            AdvanceGates(w, dt);
            foreach (var g in w.Gates)
            {
                if (g.Warn > 0f)
                    continue;
                float back = GateBack(g), lo = Math.Min(g.Front, back), hi = Math.Max(g.Front, back);
                foreach (var p in Players.Values)
                {
                    if (p.Downed)
                        continue;
                    float pad = GateBulletR + p.Def.HitR;
                    if (p.Pos.Y < lo - pad || p.Pos.Y > hi + pad || InGateGap(g, p.Pos.X))
                        continue;
                    if (g.Hit.Add(p.Id))
                        Damage(p, GateDamage, "The Warden (gate)");
                    float ahead = g.Front + g.Dir * (pad + 2f);
                    if ((ahead < p.Def.HitR || ahead > Height - p.Def.HitR) && g.Rolled.Add(p.Id))
                    {
                        p.InvulnT = 0f;
                        Damage(p, GateDamage, "The Warden (gate)");
                    }
                }
            }
        }

        /// Your own game: a gate shoves you back out the side you came from, or carries you in front of it;
        /// pinned against the edge, it rolls over you and you end up behind it.
        void WardenGatePush(Player me, V2 before)
        {
            var e = TheWarden();
            if (e == null || e.Warden == null || me.Downed)
                return;
            foreach (var g in e.Warden.Gates)
            {
                if (g.Warn > 0f)
                    continue;
                float back = GateBack(g), pad = GateBulletR + me.Def.HitR;
                if (me.Pos.Y < Math.Min(g.Front, back) - pad || me.Pos.Y > Math.Max(g.Front, back) + pad || InGateGap(g, me.Pos.X))
                    continue;
                bool ahead = (before.Y - (g.Front + back) / 2f) * g.Dir > 0f;
                float y = ahead ? g.Front + g.Dir * (pad + 2f) : back - g.Dir * (pad + 2f);
                if (y < me.Def.HitR || y > Height - me.Def.HitR)
                    y = back - g.Dir * (pad + 2f);  // pinned: it rolls over you
                me.Pos = new V2(me.Pos.X, y);
            }
        }

        /// A gas canister lobbed near the Warden's target: it lands after 1 s (marked), then a cloud for 5 s.
        void LobGas(Enemy e, Player target)
        {
            V2 near = target != null ? target.Pos : RandomInArena(120f);
            V2 at = ClampToArena(near + V2.FromAngle(Rand(0f, MathX.Tau)) * Rand(60f, 220f), 100f);
            if (at.DistanceTo(e.Pos) < GasRadius + e.Radius)
                at = ClampToArena(at + (at - e.Pos).Normalized() * (GasRadius + e.Radius), 100f);  // not on the Warden
            SpawnPatchAt(PatchKind.Gas, at, GasFlight, GasFlight + GasLife, GasRadius, GasRadius);
            BroadcastFx(FxKind.Target, at, new Rgba(0.6f, 0.95f, 0.4f), GasRadius, GasFlight, Sfx.Throw);
        }

        void TickWardenGas(WardenState w, float dt)
        {
            w.GasTickT -= dt;
            if (w.GasTickT > 0f)
                return;
            w.GasTickT = 0.25f;
            foreach (var pt in Patches.Values)
            {
                if (pt.Kind != PatchKind.Gas || !pt.Active)
                    continue;
                foreach (var p in Players.Values)
                    if (!p.Downed && p.InvulnT <= 0f && p.Pos.DistanceTo(pt.Pos) < pt.Radius + p.Def.HitR)
                        Damage(p, GasDps * 0.25f, "The Warden (gas)");
            }
        }

        // --- The riot -------------------------------------------------------------------------------

        void OpenRiotDoors(WardenState w, int count)
        {
            var doors = new List<int> { 0, 1, 2, 3, 4, 5 };
            for (int k = 0; k < count && doors.Count > 0; k++)
            {
                int pick = doors[rng.Next(doors.Count)];
                doors.Remove(pick);
                V2 d = RiotDoors[pick];
                V2 inward = d.X <= 0f ? new V2(1f, 0f) : d.X >= Width ? new V2(-1f, 0f) : new V2(0f, -1f);
                for (int i = 0; i < RiotersPerDoor; i++)
                    SpawnEnemyAt(EnemyType.Rioter, d + inward * 40f + inward.Orthogonal() * ((i - 1) * 30f), 0.3f, 0);
                BroadcastFx(FxKind.Boom, d + inward * 20f, new Rgba(1f, 0.55f, 0.2f), 70f, 0.5f, Sfx.Explode);
                w.DoorsOpened++;
            }
        }

        int CountRioters()
        {
            int n = 0;
            foreach (var e in Enemies.Values)
                if (e.Type == EnemyType.Rioter)
                    n++;
            return n;
        }

        void ClearRioters()
        {
            var ids = new List<int>();
            foreach (var e in Enemies.Values)
                if (e.Type == EnemyType.Rioter)
                    ids.Add(e.Id);
            foreach (int id in ids)
                Enemies.Remove(id);
        }

        /// A rioter that reaches the Warden lands a blow on it (unless it's changing stage) and is gone.
        void RiotersReachWarden(Enemy warden)
        {
            var ids = new List<int>();
            foreach (var r in Enemies.Values)
                if (r.Type == EnemyType.Rioter && r.SpawnT <= 0f && r.Pos.DistanceTo(warden.Pos) < warden.Radius + r.Radius)
                    ids.Add(r.Id);
            foreach (int id in ids)
            {
                var r = Enemies[id];
                if (!WardenImmune(warden))
                    warden.Hp -= warden.MaxHp * RioterHit;
                BroadcastFx(FxKind.Spark, r.Pos, new Rgba(1f, 0.7f, 0.3f), 18f, 0.3f, Sfx.Hit);
                Enemies.Remove(id);
            }
        }

        /// Rioters run at whoever's nearest: a player, or the Warden.
        V2 RioterMove(Enemy e, V2 tpos)
        {
            V2 goal = tpos;
            var w = TheWarden();
            if (w != null && w.Pos.DistanceSq(e.Pos) < tpos.DistanceSq(e.Pos))
                goal = w.Pos;
            V2 dir = (goal - e.Pos).Normalized();
            return dir + dir.Orthogonal() * (float)Math.Sin(e.T * 3f + e.Id) * 0.25f;
        }

        // --- Replication ----------------------------------------------------------------------------

        void SyncWarden(NetSync s, Enemy e)
        {
            bool has = e.Warden != null;
            s.Bool(ref has);
            if (!has)
                return;
            var w = e.Warden ?? (e.Warden = new WardenState());
            int stage = (int)w.Stage, turn = (int)w.Turn, transTo = (int)w.TransTo, mode = (int)w.LightMode;
            V2 light = w.Light;
            s.Byte(ref stage);
            s.Byte(ref turn);
            s.Float(ref w.TurnT);
            s.Float(ref w.TransT);
            s.Byte(ref transTo);
            s.Vec(ref light);
            s.Byte(ref mode);
            if (s.Reading)
            {
                w.Stage = (WardenStage)stage;
                w.Turn = (WardenTurn)turn;
                w.TransTo = (WardenStage)transTo;
                if ((WardenLight)mode == WardenLight.Follow && w.LightMode != WardenLight.Follow && w.LightMode != WardenLight.Lock)
                    w.LightDisp = light;  // just switched on: don't slide in from where it was
                w.Light = light;
                w.LightMode = (WardenLight)mode;
            }
            s.Float(ref w.LightT);
            s.Float(ref w.Aux);
            s.Bool(ref w.Dashing);
            int n = w.Gates.Count;
            s.Byte(ref n);
            if (s.Reading)
            {
                w.Gates.Clear();
                for (int i = 0; i < n; i++)
                    w.Gates.Add(new WardenGate());
            }
            foreach (var g in w.Gates)
            {
                int dir = (int)g.Dir;
                s.Float(ref g.Front);
                s.SByte(ref dir);
                g.Dir = dir;
                s.Float(ref g.Gap1);
                s.Float(ref g.Gap2);
                s.Float(ref g.Warn);
            }
        }

        /// Every game, each frame: smooth the searchlight and (on clients) move the gates between snapshots.
        void UpdateWardenView(float dt)
        {
            var e = TheWarden();
            if (e == null || e.Warden == null)
                return;
            var w = e.Warden;
            if (!IsHost)
            {
                AdvanceGates(w, dt);
                w.TurnT += dt;
                w.LightT += dt;
            }
            w.LightDisp = V2.Lerp(w.LightDisp, w.Light, 1f - (float)Math.Exp(-14.0 * dt));
        }
    }
}
