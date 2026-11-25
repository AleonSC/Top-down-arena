using System;
using System.Collections.Generic;

namespace Arena
{
    /// One swarm of a Hive Censer's insects. The host runs it; clients rebuild the list from each snapshot (SyncHive) and
    /// ease Disp toward Pos.
    public class HiveSwarm
    {
        public int Id, Owner;
        public V2 Pos, Vel, Disp;
        public float N;              // insects in it (replicated, rounded)
        public float WrapR = 34f;    // how far out its insects circle (replicated): wide when roaming, tight round what it wraps
        public byte Kind;            // what it is going for: HiveIdle, HiveFoe, HiveAlly or HiveCalled (replicated)
        public int TargetId;         // the enemy or player it is going for (replicated)
        public bool Wrapping;        // close enough to wrap it (replicated)
        public float DmgAcc;         // host: damage gathered for the next HiveTick
        // host: its insects in batches by when they were born (each lives HiveLife), oldest first
        internal readonly List<float> BornAt = new List<float>(), Count = new List<float>();
    }

    /// The Healer Summoner's Hive Censer (the healer weapon on the lobby's 4th key; mockup: the "Hive Censer" artifact).
    ///   Fire (held): insects pour out of your cursor like powder (HivePour a second, HiveReach from you at most). Each tick's
    ///     pour is one puff that drifts on with the flick of your cursor (HiveFling of its speed, slowed by HiveDrag), settles
    ///     for HiveSettle s, then joins your nearest swarm within HiveJoinR, or starts a new one (up to HiveSwarmCap).
    ///     Every insect lives HiveLife s; you keep at most HiveMaxInsects (the oldest go first).
    ///   A swarm goes for the nearest enemy within HiveHuntR (enemies first), else the nearest teammate (you too) whose
    ///     chitin is under HiveTopUp of full, else hovers HiveIdleR round you; it flies at HiveSpeed. Within its target's
    ///     edge + HiveWrapGap it wraps it: an enemy takes the weapon's Damage a second for a full swarm (HiveFull insects;
    ///     scaled by its size) and is slowed HiveSlow (HiveBossSlow on bosses).
    ///   Chitin (Player.ChitinHp, replicated): a barrier, up to ChitinMax, that wears ChitinWear a second and takes hits
    ///     before any shield (Combat.Hurt). Anyone within ChitinR of a swarm (anyone's) gains ChitinFill a second,
    ///     scaled by the swarm's size.
    ///   Alt (Msg.WeaponDash WeaponAlt.HiveCall): Hive Call. Every swarm of yours rushes to your cursor at HiveCallSpeed for
    ///     HiveCallTime; HiveCallCd cooldown. FxKind.HiveCall marks the spot.
    ///   Extra projectiles: one more swarm and HiveExtraPour more pour (and insects kept) each.
    ///   The host sends the swarms (SyncHive) and each pourer's Player.HiveOn / HivePour; every screen flies its own insects
    ///   from those (Game/HiveCenserLook.cs).
    public partial class Game
    {
        public const float HivePour = 150f, HiveLife = 9f, HiveSettle = 0.5f, HiveSettleJitter = 0.25f, HiveReach = 420f;
        public const float HiveFling = 0.42f, HiveDrag = 3.2f;
        public const int HiveSwarmCap = 4, HiveMaxInsects = 900;
        public const float HiveExtraPour = 0.2f;
        public const float HiveJoinR = 140f, HiveHuntR = 260f, HiveSpeed = 110f, HiveFull = 150f, HiveWrapGap = 26f, HiveWrapPad = 9f, HiveIdleR = 80f;
        public const float HiveTick = 0.1f, HiveSlow = 0.4f, HiveBossSlow = 0.2f, HiveTopUp = 0.8f;
        public const float ChitinMax = 40f, ChitinWear = 5f, ChitinFill = 22f, ChitinR = 100f;
        public const float HiveCallCd = 6f, HiveCallTime = 1.2f, HiveCallSpeed = 320f;
        public const byte HiveIdle = 0, HiveFoe = 1, HiveAlly = 2, HiveCalled = 3;
        public static readonly Rgba HiveGold = new Rgba(1f, 0.8f, 0.35f), ChitinGold = new Rgba(1f, 0.84f, 0.47f);

        /// Everyone's swarms, oldest first.
        public readonly List<HiveSwarm> Swarms = new List<HiveSwarm>();
        int nextSwarm = 1;
        float hiveClock, hiveTickT;

        class HivePuff
        {
            public int Owner;
            public V2 Pos, Vel;
            public float Count, Age, Settle;
        }
        readonly List<HivePuff> hivePuffs = new List<HivePuff>();  // host: poured insects not in a swarm yet
        readonly Dictionary<int, V2> hiveLastPour = new Dictionary<int, V2>();  // host: each pourer's last pour point (for the flick)

        /// How many swarms a player keeps, how fast they pour and how many insects they keep.
        public int HiveCapOf(Player p) => HiveSwarmCap + ExtraShots(p);
        public float HivePourOf(Player p) => HivePour * (1f + HiveExtraPour * ExtraShots(p));
        public float HiveKeepOf(Player p) => HiveMaxInsects * (1f + HiveExtraPour * ExtraShots(p));

        /// Where the insects come out: your cursor, no farther than HiveReach from you.
        public static V2 HivePourPoint(V2 from, V2 cursor)
        {
            V2 d = cursor - from;
            float l = d.Length;
            return l > HiveReach ? from + d * (HiveReach / l) : cursor;
        }

        /// Total insects of one player's swarms, and the ones still settling.
        public float HiveInsects(int owner)
        {
            float n = 0f;
            foreach (var s in Swarms)
                if (s.Owner == owner)
                    n += s.N;
            foreach (var pf in hivePuffs)
                if (pf.Owner == owner)
                    n += pf.Count;
            return n;
        }

        /// Host, from TickPlayers: pouring while Fire is held.
        void TickHivePour(Player p, float dt)
        {
            bool hold = p.Firing && !p.Downed && !p.ParryActive && p.SwallowT <= 0f && State != MatchState.GameOver;
            if (hold && !p.HiveOn)
                Sounds.Add(new SoundCue { Sfx = Sfx.SpellZap, Ally = p.Id != MyId });
            V2 at = HivePourPoint(p.Pos, p.AimAt);
            V2 last;
            if (!hiveLastPour.TryGetValue(p.Id, out last) || !p.HiveOn)
                last = at;
            hiveLastPour[p.Id] = at;
            p.HiveOn = hold;
            p.HivePour = at;
            if (!hold || dt <= 0f)
                return;
            float n = HivePourOf(p) * dt;
            // Over the keep: the oldest insects go first.
            float over = HiveInsects(p.Id) + n - HiveKeepOf(p);
            if (over > 0f)
                HiveCull(p.Id, over);
            V2 flick = (at - last) / dt;
            if (flick.Length > 1500f)
                flick = flick * (1500f / flick.Length);  // (a cursor jump, not a flick)
            hivePuffs.Add(new HivePuff { Owner = p.Id, Pos = at, Vel = flick * HiveFling, Count = n, Settle = HiveSettle + HiveSettleJitter * 0.5f });
            p.HivePourT += dt;
            if (p.HivePourT >= HiveTick)
            {
                p.HivePourT -= HiveTick;
                p.Run.ShotsFired++;
                foreach (var s in Swarms)
                    if (s.Owner == p.Id && s.Wrapping && s.Kind == HiveFoe)
                    {
                        p.Run.ShotsHit++;
                        break;
                    }
            }
        }

        /// Host: take `n` of a player's oldest insects away.
        void HiveCull(int owner, float n)
        {
            while (n > 0.001f)
            {
                HiveSwarm oldest = null;
                foreach (var s in Swarms)
                    if (s.Owner == owner && s.BornAt.Count > 0 && (oldest == null || s.BornAt[0] < oldest.BornAt[0]))
                        oldest = s;
                if (oldest == null)
                    return;
                float take = Math.Min(n, oldest.Count[0]);
                oldest.Count[0] -= take;
                n -= take;
                if (oldest.Count[0] <= 0.001f)
                {
                    oldest.BornAt.RemoveAt(0);
                    oldest.Count.RemoveAt(0);
                }
            }
        }

        /// Host, once a step: puffs settling into swarms, insects dying of age, swarms hunting and wrapping, chitin.
        void TickHive(float dt)
        {
            hiveClock += dt;
            if (hivePuffs.Count == 0 && Swarms.Count == 0)
            {
                TickChitin(dt);
                return;
            }
            string was = CreditTo("weapon");
            // Puffs drift with their flick, then settle into a swarm.
            float drag = (float)Math.Exp(-dt * HiveDrag);
            for (int i = 0; i < hivePuffs.Count; i++)
            {
                var pf = hivePuffs[i];
                pf.Vel = pf.Vel * drag;
                pf.Pos = KeepInArena(pf.Pos + pf.Vel * dt, 4f);
                pf.Age += dt;
                Player owner;
                if (!Players.TryGetValue(pf.Owner, out owner))
                {
                    hivePuffs.RemoveAt(i--);
                    continue;
                }
                if (pf.Age < pf.Settle)
                    continue;
                HiveJoin(owner, pf);
                hivePuffs.RemoveAt(i--);
            }
            // Insects die of age; empty swarms (and those of players who left) go.
            for (int i = 0; i < Swarms.Count; i++)
            {
                var s = Swarms[i];
                while (s.BornAt.Count > 0 && hiveClock - s.BornAt[0] >= HiveLife)
                {
                    s.BornAt.RemoveAt(0);
                    s.Count.RemoveAt(0);
                }
                float n = 0f;
                foreach (float c in s.Count)
                    n += c;
                s.N = n;
                if (n < 0.5f || !Players.ContainsKey(s.Owner))
                    Swarms.RemoveAt(i--);
            }
            // Hunting, flying and wrapping.
            bool tick = (hiveTickT += dt) >= HiveTick;
            if (tick)
                hiveTickT -= HiveTick;
            for (int i = 0; i < Swarms.Count; i++)
            {
                var s = Swarms[i];
                var p = Players[s.Owner];
                V2 goal;
                float goalR = 0f;
                Enemy foe = null;
                Player mate = null;
                if (p.HiveCallT > 0f)
                {
                    s.Kind = HiveCalled;
                    s.TargetId = 0;
                    goal = p.HiveCallAt;
                }
                else
                {
                    float bd = HiveHuntR;
                    foreach (var e in Enemies.Values)
                    {
                        if (!Targetable(e))
                            continue;
                        float d = e.Pos.DistanceTo(s.Pos);
                        if (d < bd) { bd = d; foe = e; }
                    }
                    if (foe == null)
                    {
                        bd = HiveHuntR;
                        foreach (var q in Players.Values)
                        {
                            if (q.Downed || q.ChitinHp >= ChitinMax * HiveTopUp)
                                continue;
                            float d = q.Pos.DistanceTo(s.Pos);
                            if (d < bd) { bd = d; mate = q; }
                        }
                    }
                    if (foe != null)
                    {
                        s.Kind = HiveFoe; s.TargetId = foe.Id; goal = foe.Pos; goalR = foe.Radius;
                    }
                    else if (mate != null)
                    {
                        s.Kind = HiveAlly; s.TargetId = mate.Id; goal = mate.Pos; goalR = mate.Def.BodyR;
                    }
                    else
                    {
                        s.Kind = HiveIdle; s.TargetId = 0;
                        goal = p.Pos + V2.FromAngle(i * 1.57f + hiveClock * 0.3f) * HiveIdleR;  // hover round you
                    }
                }
                V2 to = goal - s.Pos;
                float dist = Math.Max(0.001f, to.Length);
                float speed = (s.Kind == HiveCalled ? HiveCallSpeed : HiveSpeed) * Math.Min(1f, dist / 30f);
                float k = Math.Min(1f, dt * 3f);
                s.Vel = s.Vel + (to * (speed / dist) - s.Vel) * k;
                s.Pos = KeepInArena(s.Pos + s.Vel * dt, 4f);
                s.Wrapping = (s.Kind == HiveFoe || s.Kind == HiveAlly) && dist < goalR + HiveWrapGap;
                float wantR = s.Wrapping ? goalR + HiveWrapPad : 30f + (float)Math.Sqrt(s.N) * 1.6f;
                s.WrapR += (wantR - s.WrapR) * k;
                if (s.Wrapping && foe != null)
                {
                    s.DmgAcc += p.Weapon.Damage * p.Stats.DmgMult * (s.N / HiveFull) * dt;
                    Slow(foe, HiveTick * 2.5f, 1f - (foe.Def.IsBoss ? HiveBossSlow : HiveSlow));
                    if (tick && s.DmgAcc > 0f)
                    {
                        WeaponHurt(foe, s.DmgAcc, p.Id);
                        s.DmgAcc = 0f;
                    }
                }
                else
                    s.DmgAcc = 0f;
            }
            dmgSrc = was;
            TickChitin(dt);
        }

        /// Host: a settled puff joins the owner's nearest swarm within HiveJoinR, or starts one, or joins the nearest anyway.
        void HiveJoin(Player owner, HivePuff pf)
        {
            HiveSwarm best = null;
            float bd = HiveJoinR;
            int mine = 0;
            foreach (var s in Swarms)
            {
                if (s.Owner != owner.Id)
                    continue;
                mine++;
                float d = s.Pos.DistanceTo(pf.Pos);
                if (d < bd) { bd = d; best = s; }
            }
            if (best == null && mine < HiveCapOf(owner))
            {
                best = new HiveSwarm { Id = nextSwarm++, Owner = owner.Id, Pos = pf.Pos, Disp = pf.Pos, Vel = pf.Vel };
                if (nextSwarm > 30000)
                    nextSwarm = 1;
                Swarms.Add(best);
            }
            if (best == null)
            {
                bd = float.MaxValue;
                foreach (var s in Swarms)
                    if (s.Owner == owner.Id && s.Pos.DistanceTo(pf.Pos) < bd) { bd = s.Pos.DistanceTo(pf.Pos); best = s; }
            }
            if (best == null)
                return;
            float born = hiveClock - pf.Age;  // insects count their life from the pour
            int last = best.BornAt.Count - 1;
            if (last >= 0 && born - best.BornAt[last] < 0.25f)
                best.Count[last] += pf.Count;
            else
            {
                best.BornAt.Add(born);
                best.Count.Add(pf.Count);
            }
            best.N += pf.Count;
        }

        /// Host: chitin wears off, and swarms near anyone build it back up.
        void TickChitin(float dt)
        {
            foreach (var q in Players.Values)
            {
                if (q.ChitinHp > 0f)
                    q.ChitinHp = Math.Max(0f, q.ChitinHp - ChitinWear * dt);
                if (q.Downed || Swarms.Count == 0)
                    continue;
                float gain = 0f;
                foreach (var s in Swarms)
                    if (s.Pos.DistanceSq(q.Pos) < ChitinR * ChitinR)
                        gain += ChitinFill * Math.Min(1f, s.N / HiveFull) * dt;
                if (gain > 0f)
                {
                    float was = q.ChitinHp;
                    q.ChitinHp = Math.Min(ChitinMax, q.ChitinHp + gain);
                    float given = q.ChitinHp - was;
                    if (given > 0f)
                        foreach (var s in Swarms)
                        {
                            Player by;
                            if (s.Pos.DistanceSq(q.Pos) < ChitinR * ChitinR && Players.TryGetValue(s.Owner, out by))
                            {
                                by.Run.HealingDone += given;  // (chitin counts as healing in the run stats)
                                break;
                            }
                        }
                }
            }
        }

        /// Combat.Hurt: chitin takes a hit before anything else. What's left gets through.
        static float ChitinSoak(Player p, float amount)
        {
            if (p.ChitinHp <= 0f || amount <= 0f)
                return amount;
            float soak = Math.Min(p.ChitinHp, amount);
            p.ChitinHp -= soak;
            return amount - soak;
        }

        /// Players changing weapon or leaving stop pouring.
        void ClearHivePour(Player p)
        {
            p.HiveOn = false;
            hiveLastPour.Remove(p.Id);
        }

        // ---- Hive Call (Alt)

        float hiveLocalCd;  // your own game: until the Alt can go again (the cooldown ring under your feet)
        public float HiveLocalCd => hiveLocalCd;

        /// Your own game: Alt with the Hive Censer. True when the weapon is out (the Alt is its own).
        bool LocalHiveCall(Player me, bool pressed, V2 aimAt, float dt)
        {
            hiveLocalCd = Math.Max(0f, hiveLocalCd - dt);
            if (me.Weapon.Id != WeaponId.HiveCenser)
                return false;
            if (pressed && hiveLocalCd <= 0f)
            {
                hiveLocalCd = HiveCallCd;
                if (IsHost)
                    HostWeaponDash(MyId, WeaponAlt.HiveCall, aimAt);
                else
                {
                    var w = Proto.Begin(Msg.WeaponDash);
                    w.Write(WeaponAlt.HiveCall);
                    w.Write(aimAt);
                    session.SendToHost(Proto.End(w));
                }
            }
            return true;
        }

        /// Host: Alt pressed with the Hive Censer: every swarm of theirs rushes to the cursor, if the cooldown is done.
        void HostHiveCall(Player p, V2 target)
        {
            if (p.Weapon.Id != WeaponId.HiveCenser || p.Downed || p.HiveCallCd > 0f)
                return;
            p.HiveCallCd = HiveCallCd - 0.1f;  // (a little slack for the trip)
            p.HiveCallT = HiveCallTime;
            p.HiveCallAt = ClampToArena(target, 4f);
            BroadcastFx(FxKind.HiveCall, p.HiveCallAt, new V2(p.Id, 0f), HiveGold, 60f, HiveCallTime, Sfx.SpellChainArc);
        }

        /// Host, from TickPlayers: the Alt's clocks.
        void TickHiveCall(Player p, float dt)
        {
            p.HiveCallCd = Math.Max(0f, p.HiveCallCd - dt);
            p.HiveCallT = Math.Max(0f, p.HiveCallT - dt);
        }

        // ---- the network: the swarms ride the snapshot

        void SyncHive(NetSync s)
        {
            int count = Math.Min(255, Swarms.Count);
            s.Byte(ref count);
            if (s.Reading)
            {
                hiveSeen.Clear();
                for (int i = 0; i < count; i++)
                {
                    int id = 0, owner = 0, n = 0, wrapR = 0, kind = 0, target = 0;
                    V2 pos = default(V2);
                    bool wrapping = false;
                    s.Short(ref id); s.Byte(ref owner); s.Vec(ref pos); s.Short(ref n); s.Byte(ref wrapR); s.Byte(ref kind); s.Int(ref target); s.Bool(ref wrapping);
                    HiveSwarm sw = null;
                    foreach (var x in Swarms)
                        if (x.Id == id) { sw = x; break; }
                    if (sw == null)
                    {
                        sw = new HiveSwarm { Id = id, Disp = pos };
                        Swarms.Add(sw);
                    }
                    sw.Owner = owner; sw.Pos = pos; sw.N = n; sw.WrapR = wrapR; sw.Kind = (byte)kind; sw.TargetId = target; sw.Wrapping = wrapping;
                    hiveSeen.Add(id);
                }
                Swarms.RemoveAll(x => !hiveSeen.Contains(x.Id));
            }
            else
                for (int i = 0; i < count; i++)
                {
                    var sw = Swarms[i];
                    int id = sw.Id, owner = sw.Owner & 0xff, n = (int)Math.Round(Math.Min(30000f, sw.N)), wrapR = (int)Math.Round(Math.Min(255f, sw.WrapR)), kind = sw.Kind, target = sw.TargetId;
                    V2 pos = sw.Pos;
                    bool wrapping = sw.Wrapping;
                    s.Short(ref id); s.Byte(ref owner); s.Vec(ref pos); s.Short(ref n); s.Byte(ref wrapR); s.Byte(ref kind); s.Int(ref target); s.Bool(ref wrapping);
                }
        }
        readonly HashSet<int> hiveSeen = new HashSet<int>();

        /// Every frame (Update): the swarms drawn ease toward where the host has them.
        void UpdateHiveView(float dt)
        {
            float k = Math.Min(1f, dt * 12f);
            foreach (var s in Swarms)
                s.Disp = IsHost ? s.Pos : s.Disp + (s.Pos - s.Disp) * k;
            foreach (var p in Players.Values)
                p.HivePourDisp = p.HiveOn && p.HivePourDisp.DistanceSq(p.HivePour) < 300f * 300f ? p.HivePourDisp + (p.HivePour - p.HivePourDisp) * (IsHost ? 1f : Math.Min(1f, dt * 20f)) : p.HivePour;
        }

        /// RunReset: no swarms carry into a new run.
        void ResetHive()
        {
            Swarms.Clear();
            hivePuffs.Clear();
            hiveLastPour.Clear();
        }
    }
}
