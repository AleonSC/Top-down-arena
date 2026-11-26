using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Healer Elementalist's Static Shock (the healer weapon on the lobby's 4th key; mockup: the "Static Shock" artifact).
    ///   Fire (held): a short cone of constant crackle out of your hand (StaticRange, StaticHalf each side of your aim). Up to
    ///     StaticLinks enemies inside are wired into one circuit: each wires to the nearest point already in it (your hand
    ///     first), and every StaticTick each takes a shock that grows from StaticLow x to the full weapon Damage a second over
    ///     StaticRamp seconds in the cone (6 a second up to 24; it starts over StaticForget after it leaves). Room turrets in
    ///     the cone take the same shock (gathered, StaticTurretFlush at a time). Teammates it touches get Overcharge.
    ///     The host only sends Player.StaticOn: every screen works the circuit out itself (StaticCircuit), so the links you
    ///     see are the ones the host shocks.
    ///   Alt (Msg.WeaponDash WeaponAlt.ArcCannon): the Arc Cannon. A giant ball of lightning (CannonBallR) rolls at CannonSpeed to your
    ///     cursor (CannonMin to CannonReach out) through everyone: each enemy it passes takes CannonPassDmg x the damage once,
    ///     each teammate gets Overcharge; at the end it bursts (CannonBurstR): CannonBurstDmg x the damage, a CannonStun stun (not on
    ///     bosses or heavies) and Overcharge for teammates in it. CannonCd cooldown. Drawn from FxKind.ArcBall (its flight) and
    ///     FxKind.ArcBurst.
    ///   Overcharge: OverchargeTime seconds of OverchargeMove speed (Player.Speed), OverchargeFire fire rate (FireRate) and
    ///     OverchargeShot faster shots (SpawnBullet: same reach, they get there sooner). Touching again refreshes it; it
    ///     doesn't stack. Player.OverchargeT is replicated; FxKind.Overcharge is its pop.
    ///   Extra projectiles: one more link and 15% more shock each. Homing Rounds make the cone and the burst bigger (AreaDesc).
    /// Look: Game/StaticShockLook.cs.
    public partial class Game
    {
        public const float StaticRange = 200f, StaticHalf = 0.56f;  // the cone: 200 px, 64 degrees across
        public const float StaticTick = 0.1f, StaticLow = 0.25f, StaticRamp = 4f, StaticForget = 0.3f, StaticTurretFlush = 5f;
        public const int StaticLinks = 6;
        public const float CannonBallR = 24f, CannonSpeed = 330f, CannonReach = 560f, CannonMin = 120f;
        public const float CannonBurstR = 120f, CannonPassDmg = 0.5f, CannonBurstDmg = 1.25f, CannonStun = 0.8f, CannonCd = 5f, CannonBurstFx = 0.9f;
        public const float OverchargeTime = 10f, OverchargeMove = 1.25f, OverchargeFire = 1.3f, OverchargeShot = 1.35f;
        public static readonly Rgba StaticBlue = new Rgba(0.62f, 0.89f, 1f), CannonViolet = new Rgba(0.79f, 0.7f, 1f), OverchargeGold = new Rgba(1f, 0.8f, 0.35f);

        /// The cone's reach (Homing Rounds' area grows it).
        public static float StaticRangeOf(Player p) => StaticRange * AreaMult(p);
        public static float ArcBurstOf(Player p) => CannonBurstR * AreaMult(p);

        /// Where the crackle comes out: your hand, just ahead of your body.
        public static V2 StaticHand(Player p, V2 at) => at + V2.FromAngle(p.Aim) * (p.Def.BodyR + 6f);

        /// Whether a body (centre c, radius r) is in the cone from `hand` along `aim`.
        public static bool InStaticCone(V2 hand, float aim, float range, V2 c, float r)
            => MathX.InCone(hand, aim, StaticHalf, range, c, r, inner: r, padBody: true, strict: true);

        /// The circuit: up to `max` enemies in the cone, nearest the hand first, each wired (from[i]: -1 the hand, else the
        /// index of the enemy it wires to) to the nearest point already in it. pos: where each enemy is (the host's Pos,
        /// a screen's Disp). Shared by the host's shocks and every screen's drawing.
        public static void StaticCircuit(V2 hand, float aim, float range, IEnumerable<Enemy> enemies, Func<Enemy, V2> pos, Func<Enemy, bool> ok,
            int max, List<Enemy> into, List<int> from)
        {
            into.Clear();
            from.Clear();
            var inside = circuitScratch;
            inside.Clear();
            foreach (var e in enemies)
                if (ok(e) && InStaticCone(hand, aim, range, pos(e), e.Radius))
                    inside.Add(e);
            inside.Sort((a, b) => pos(a).DistanceSq(hand).CompareTo(pos(b).DistanceSq(hand)));
            if (inside.Count > max)
                inside.RemoveRange(max, inside.Count - max);
            while (inside.Count > 0)
            {
                int bi = 0, bf = -1;
                float bd = float.MaxValue;
                for (int i = 0; i < inside.Count; i++)
                {
                    V2 c = pos(inside[i]);
                    float d = c.DistanceSq(hand);
                    if (d < bd) { bd = d; bi = i; bf = -1; }
                    for (int j = 0; j < into.Count; j++)
                    {
                        d = c.DistanceSq(pos(into[j]));
                        if (d < bd) { bd = d; bi = i; bf = j; }
                    }
                }
                into.Add(inside[bi]);
                from.Add(bf);
                inside.RemoveAt(bi);
            }
        }
        static readonly List<Enemy> circuitScratch = new List<Enemy>();

        readonly List<Enemy> staticIn = new List<Enemy>();
        readonly List<int> staticFrom = new List<int>();
        readonly Dictionary<int, float> staticShockT = new Dictionary<int, float>(), staticSeen = new Dictionary<int, float>();  // host: each enemy's time in a cone, and when it was last in one
        readonly List<int> staticGone = new List<int>();
        float staticClock;  // host: seconds of play (TickArcBalls), for staticSeen

        /// Host, from TickPlayers: the held cone, its circuit, its shocks and the Overcharge it gives.
        void TickStaticShock(Player p, float dt)
        {
            bool hold = p.Firing && !p.Downed && !p.ParryActive && p.SwallowT <= 0f && State != MatchState.GameOver;
            if (hold && !p.StaticOn)
            {
                p.FireCd = 0f;
                Sounds.Add(new SoundCue { Sfx = Sfx.SpellZap, Ally = p.Id != MyId });
            }
            p.StaticOn = hold;
            if (!hold || p.FireCd > 0f)
                return;
            // Enemies out of every cone for a while start over.
            staticGone.Clear();
            foreach (var kv in staticSeen)
                if (staticClock - kv.Value > StaticForget || !FoeExists(kv.Key))
                    staticGone.Add(kv.Key);
            foreach (int id in staticGone)
            {
                staticSeen.Remove(id);
                staticShockT.Remove(id);
            }
            ForgetTurretShocks();
            float gap = StaticTick / FireRate(p);
            p.FireCd = Math.Max(-gap, p.FireCd) + gap;  // (the overshoot carries, so ticks keep time with 60 fps steps)
            int extra = ExtraShots(p);
            V2 hand = StaticHand(p, p.Pos);
            StaticCircuit(hand, p.Aim, StaticRangeOf(p), Enemies.Values, e => e.Pos, Targetable, StaticLinks + extra, staticIn, staticFrom);
            float full = p.Weapon.Damage * p.Stats.DmgMult * (1f + 0.15f * extra);
            foreach (var e in staticIn)
            {
                float t;
                staticShockT.TryGetValue(e.Id, out t);
                t += gap;
                staticShockT[e.Id] = t;
                staticSeen[e.Id] = staticClock;
                float rate = full * (StaticLow + (1f - StaticLow) * Math.Min(1f, t / StaticRamp));
                WeaponHurt(e, rate * StaticTick, p.Id);
                Charge(e, p.Id);  // Charged.cs
            }
            bool turret = ShockRoomTurrets(hand, p.Aim, StaticRangeOf(p), full, gap);
            foreach (var q in Players.Values)
                if (q != p && !q.Downed && InStaticCone(hand, p.Aim, StaticRangeOf(p), q.Pos, q.Def.BodyR))
                    Overcharge(q, p);
            p.Run.ShotsFired++;
            if (staticIn.Count > 0 || turret)
                p.Run.ShotsHit++;
        }

        // Room turrets (RoomTraps.cs) in a cone take the same growing shock. A hit on one sends a message and a ring, so the
        // shock gathers (turretPend) and lands StaticTurretFlush at a time, or what's left when it leaves the cone.
        readonly Dictionary<long, float> turretShockT = new Dictionary<long, float>(), turretSeen = new Dictionary<long, float>(), turretPend = new Dictionary<long, float>();
        readonly List<long> turretGone = new List<long>();

        /// Host: shock every standing room turret in the cone. True when one was in it.
        bool ShockRoomTurrets(V2 hand, float aim, float range, float full, float gap)
        {
            if (!ExpandingOn && devTraps.Count == 0)
                return false;
            bool any = false;
            var traps = RoomTrapList;
            for (int i = traps.Count - 1; i >= 0; i--)
            {
                var tr = traps[i];
                if (tr.Kind != TrapKind.Turret || !InStaticCone(hand, aim, range, tr.Pos, RoomTraps.TurretHitR))
                    continue;
                any = true;
                long key = TrapKey(tr.Pos);
                float t, pend;
                turretShockT.TryGetValue(key, out t);
                t += gap;
                turretShockT[key] = t;
                turretSeen[key] = staticClock;
                turretPend.TryGetValue(key, out pend);
                pend += full * (StaticLow + (1f - StaticLow) * Math.Min(1f, t / StaticRamp)) * StaticTick;
                if (pend >= StaticTurretFlush)
                {
                    HurtRoomTurret(tr, pend);
                    pend = 0f;
                }
                turretPend[key] = pend;
            }
            return any;
        }

        /// Host: turrets out of every cone for a while start over; what they had gathered lands.
        void ForgetTurretShocks()
        {
            if (turretSeen.Count == 0)
                return;
            turretGone.Clear();
            foreach (var kv in turretSeen)
                if (staticClock - kv.Value > StaticForget)
                    turretGone.Add(kv.Key);
            foreach (long key in turretGone)
            {
                float pend;
                if (turretPend.TryGetValue(key, out pend) && pend > 0f)
                {
                    var traps = RoomTrapList;
                    for (int i = traps.Count - 1; i >= 0; i--)
                        if (traps[i].Kind == TrapKind.Turret && TrapKey(traps[i].Pos) == key)
                        {
                            HurtRoomTurret(traps[i], pend);
                            break;
                        }
                }
                turretSeen.Remove(key);
                turretShockT.Remove(key);
                turretPend.Remove(key);
            }
        }

        /// Host: Overcharge for a teammate (refreshed, never stacked). The pop and its sound only when it was nearly out.
        void Overcharge(Player q, Player by)
        {
            if (q.OverchargeT < OverchargeTime - 0.25f)
                BroadcastFx(FxKind.Overcharge, q.Pos, new V2(q.Id, by.Id), OverchargeGold, q.Def.BodyR, 1.1f, q.OverchargeT <= 0f ? Sfx.SpellChainArc : Sfx.None);
            q.OverchargeT = OverchargeTime;
        }

        // ---- Arc Cannon (Alt)

        float arcLocalCd;  // your own game: until the Alt can go again (the cooldown ring under your feet)
        public float ArcLocalCd => arcLocalCd;

        /// Your own game: Alt with Static Shock. True when the weapon is out (the Alt is its own).
        bool LocalArcCannon(Player me, bool pressed, V2 aimAt, float dt)
        {
            arcLocalCd = Math.Max(0f, arcLocalCd - dt);
            if (me.Weapon.Id != WeaponId.StaticShock)
                return false;
            if (pressed && arcLocalCd <= 0f)
            {
                arcLocalCd = CannonCd;
                if (IsHost)
                    HostWeaponDash(MyId, WeaponAlt.ArcCannon, aimAt);
                else
                {
                    var w = Proto.Begin(Msg.WeaponDash);
                    w.Write(WeaponAlt.ArcCannon);
                    w.Write(aimAt);
                    session.SendToHost(Proto.End(w));
                }
            }
            return true;
        }

        class ArcBall
        {
            public int Owner;
            public V2 From, To;
            public float Age, Dur, Pass, Burst, BurstR;
            public readonly HashSet<int> HitE = new HashSet<int>(), HitP = new HashSet<int>();
            public readonly HashSet<long> HitT = new HashSet<long>();  // room turrets it rolled through
        }
        readonly List<ArcBall> arcBalls = new List<ArcBall>();

        /// Where a ball `age` into a flight of `dur` is.
        public static V2 ArcBallAt(V2 from, V2 to, float age, float dur) => from + (to - from) * (dur > 0f ? Math.Min(1f, age / dur) : 1f);

        /// Host: Alt pressed with Static Shock: the ball rolls out, if the cooldown is done.
        void HostArcCannon(Player p, V2 target)
        {
            if (p.Weapon.Id != WeaponId.StaticShock || p.Downed || p.StunT > 0f || p.SwallowT > 0f || p.ArcCd > 0f || State == MatchState.GameOver)
                return;
            p.ArcCd = CannonCd;
            V2 hand = StaticHand(p, p.Pos), to = target - hand;
            float len = to.Length;
            V2 dir = len > 1f ? to / len : V2.FromAngle(p.Aim);
            len = Math.Max(CannonMin, Math.Min(CannonReach * p.Stats.ShotSpeedMult, len));
            V2 end = ClampToArena(hand + dir * len, CannonBallR);
            float dmg = p.Weapon.Damage * p.Stats.DmgMult * (1f + 0.15f * ExtraShots(p));
            var b = new ArcBall { Owner = p.Id, From = hand, To = end, Dur = Math.Max(0.05f, end.DistanceTo(hand) / CannonSpeed), Pass = dmg * CannonPassDmg, Burst = dmg * CannonBurstDmg, BurstR = ArcBurstOf(p) };
            arcBalls.Add(b);
            BroadcastFx(FxKind.ArcBall, hand, end, CannonViolet, p.Id, b.Dur, Sfx.SpellZap);
            p.Run.ShotsFired++;
        }

        /// Host, every tick: the balls roll, touch and burst; and each player's Arc Cannon cooldown.
        void TickArcBalls(float dt)
        {
            staticClock += dt;
            foreach (var p in Players.Values)
                p.ArcCd = Math.Max(0f, p.ArcCd - dt);
            for (int i = arcBalls.Count - 1; i >= 0; i--)
            {
                var b = arcBalls[i];
                b.Age += dt;
                V2 at = ArcBallAt(b.From, b.To, b.Age, b.Dur);
                Player owner;
                Players.TryGetValue(b.Owner, out owner);
                foreach (var e in AllFoes(by: b.Owner))
                    if (!b.HitE.Contains(e.Id) && e.Pos.WithinReach(at, CannonBallR + e.Radius))
                    {
                        b.HitE.Add(e.Id);
                        WeaponHurt(e, b.Pass, b.Owner);
                        Charge(e, b.Owner);  // Charged.cs
                    }
                if (ExpandingOn || devTraps.Count > 0)
                {
                    var traps = RoomTrapList;
                    for (int k = traps.Count - 1; k >= 0; k--)
                    {
                        var tr = traps[k];
                        if (tr.Kind == TrapKind.Turret && tr.Pos.WithinReach(at, CannonBallR + RoomTraps.TurretHitR) && b.HitT.Add(TrapKey(tr.Pos)))
                            HurtRoomTurret(tr, b.Pass);
                    }
                }
                if (owner != null)
                    foreach (var q in Players.Values)
                        if (q != owner && !q.Downed && !b.HitP.Contains(q.Id) && q.Pos.WithinReach(at, CannonBallR + q.Def.BodyR))
                        {
                            b.HitP.Add(q.Id);
                            Overcharge(q, owner);
                        }
                if (b.Age < b.Dur)
                    continue;
                arcBalls.RemoveAt(i);
                bool any = b.HitE.Count > 0;
                BroadcastFx(FxKind.ArcBurst, b.To, b.To, CannonViolet, b.BurstR, CannonBurstFx, Sfx.Explode);
                foreach (var e in AllFoes(by: b.Owner))
                    if (e.Pos.WithinReach(b.To, b.BurstR + e.Radius))
                    {
                        any = true;
                        WeaponHurt(e, b.Burst, b.Owner);
                        Charge(e, b.Owner);  // Charged.cs
                        if (e.Hp > 0f && !Heavy(e))
                            Stun(e, CannonStun);
                    }
                var burstAt = b.To;
                float burstR = b.BurstR;
                MeleeHitRoomTurrets(b.Burst, (q, lr) => q.WithinReach(burstAt, burstR + lr));  // room turrets in the burst (RoomTraps.cs)
                if (owner != null)
                {
                    foreach (var q in Players.Values)
                        if (q != owner && !q.Downed && q.Pos.WithinReach(b.To, b.BurstR + q.Def.BodyR))
                            Overcharge(q, owner);
                    if (any)
                        owner.Run.ShotsHit++;
                }
            }
        }

        /// Off when you put it away or go down: the cone stops.
        void ClearStaticShock(Player p) => p.StaticOn = false;

        // --- Network: the player's Static Shock fields (SyncPlayer) ---

        void SyncStaticShock(NetSync s, Player p)
        {
            s.Bool(ref p.StaticOn);  // Static Shock
            s.Float(ref p.OverchargeT);
        }

        /// WeaponDef.BotAlt (BotWeaponAlt.cs): the Arc Cannon into a crowd or the boss
        static bool StaticShockBotAlt(Game g, AltView v, float dt, bool late, ref bool firing, ref V2 aimAt)
        {
            var e = v.ShootAt;
            return !late && e != null && v.ShootD < CannonReach && g.Tap(g.ArcLocalCd <= 0f && (e.Def.IsBoss || g.EnemiesNear(e.Pos, CannonBurstR) >= 3), 1f);
        }
    }
}
