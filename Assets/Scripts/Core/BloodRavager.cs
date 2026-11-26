using System;
using System.Collections.Generic;

namespace Arena
{
    /// The eight blood legs' walk (Blood Ravager): plain maths shared by the host (where a foot lands, it stomps) and every
    /// screen's look (Game/BloodRavagerLook.cs keeps its own copy per player, fed the drawn position). From the mockup (claude.ai
    /// artifact "Hemomancer Blood Legs", variant Ravager): each leg rests Reach px out from you at its own angle off your facing
    /// (LegAng), led by your velocity; when you've moved StepDist px past a planted foot it steps (StepTime s, over the ground
    /// to a little past the rest spot), the diagonal halves (Group) taking turns. Your facing follows your movement when you
    /// move fast, else your aim. A leg that's lashing (Busy) is held out of the walk.
    public sealed class RavagerGait
    {
        public const int Legs = 8;
        public const float L1 = 44f, L2 = 54f, Reach = 66f, StepDist = 40f, StepTime = 0.07f, Lead = 0.16f, Overshoot = 0.35f;
        public const float HipOut = 9f, FaceTurn = 6f, MoveFace = 60f;
        public static readonly float[] LegAng = { -0.45f, 0.45f, -1.1f, 1.1f, -1.9f, 1.9f, -2.6f, 2.6f };

        public readonly V2[] Foot = new V2[Legs], From = new V2[Legs], To = new V2[Legs];
        public readonly float[] StepT = new float[Legs];  // 0 to 1 through a step; 1 = planted
        public readonly bool[] Busy = new bool[Legs];     // lashing: out of the walk
        public V2 Pos, Vel;
        public float Face;
        public int Landed;   // the legs (bits) whose step ended in the last Update
        public bool Started;

        /// The leg's walk half: diagonal pairs step together.
        public static int Group(int i) => (i % 2) ^ (i / 2 % 2);

        public static float Ease(float x) => x < 0.5f ? 2f * x * x : 1f - (-2f * x + 2f) * (-2f * x + 2f) / 2f;

        /// Where leg i rests, for a body at `pos` facing `face` moving at `vel`.
        public static V2 Rest(int i, V2 pos, float face, V2 vel, float lead) => pos + V2.FromAngle(face + LegAng[i]) * Reach + vel * lead;

        /// The hip leg i hangs from.
        public V2 Hip(int i) => Pos + V2.FromAngle(Face + LegAng[i] * 0.35f) * HipOut;

        /// The legs burst out of you: every foot at your middle, stepping out to its rest spot.
        public void Reset(V2 pos, float face)
        {
            Pos = pos;
            Vel = V2.Zero;
            Face = face;
            Started = true;
            Landed = 0;
            for (int i = 0; i < Legs; i++)
            {
                Foot[i] = From[i] = pos;
                To[i] = Rest(i, pos, face, V2.Zero, 0f);
                StepT[i] = 0f;
                Busy[i] = false;
            }
        }

        /// One step of the walk, the body now at `pos` aiming at `aim` (radians).
        public void Update(V2 pos, float aim, float dt)
        {
            Landed = 0;
            if (!Started || pos.DistanceTo(Pos) > 300f)
            {
                Reset(pos, aim);
                return;
            }
            if (dt <= 0f)
                return;
            V2 raw = (pos - Pos) / dt;
            Vel = Vel + (raw - Vel) * Math.Min(1f, 20f * dt);
            Pos = pos;
            float want = Vel.Length > MoveFace ? Vel.Angle : aim;
            Face += MathX.WrapPi(want - Face) * Math.Min(1f, FaceTurn * dt);
            bool busy0 = false, busy1 = false;
            for (int i = 0; i < Legs; i++)
                if (StepT[i] < 1f && !Busy[i])
                {
                    if (Group(i) == 0) busy0 = true;
                    else busy1 = true;
                }
            for (int i = 0; i < Legs; i++)
            {
                if (Busy[i])
                    continue;
                if (StepT[i] < 1f)
                {
                    StepT[i] = Math.Min(1f, StepT[i] + dt / StepTime);
                    Foot[i] = V2.Lerp(From[i], To[i], Ease(StepT[i]));
                    if (StepT[i] >= 1f)
                        Landed |= 1 << i;
                }
                else
                {
                    V2 rest = Rest(i, Pos, Face, Vel, Lead);
                    bool otherBusy = Group(i) == 0 ? busy1 : busy0;
                    if (rest.DistanceTo(Foot[i]) > StepDist && !otherBusy)
                    {
                        From[i] = Foot[i];
                        To[i] = rest + (rest - Foot[i]) * Overshoot;
                        StepT[i] = 0f;
                    }
                }
                V2 off = Foot[i] - Pos;
                float max = L1 + L2 - 4f;
                if (off.Length > max)
                    Foot[i] = Pos + off.Normalized() * max;
            }
        }
    }

    /// Blood Ravager: the DPS Hemomancer's second ultimate (key 1 of the lobby's Ultimate row; Crimson Harvest stays the default),
    /// from the mockup (claude.ai artifact "Hemomancer Blood Legs", variant 4 Ravager).
    ///   - Eight thin blood legs burst out of you for RvTime s (RvTimeMastered mastered). You move RvSpeed times as fast and
    ///     walk on them (RavagerGait).
    ///   - Trample: every foot that lands stomps: RvStomp to each enemy within RvStompR px of it, and a small shove.
    ///     Running into an enemy faster than RvRamSpeed hurts it RvRam and shoves it off (once every RvRamGap s each).
    ///   - Lash: your weapon is put away and Fire lashes. A click picks the enemy within RvLashR px of you that no leg is
    ///     already lashing (any in reach if all are), and the free leg whose foot is nearest it lashes out at it: the foot
    ///     follows the enemy, lands RvLashDmg (and the same to anything within RvLashSplash px of it) with a shove, and comes
    ///     back, RvLashTime s all told. Click as fast as you like (RvLashGap s apart), up to RvLashMax legs lashing at once.
    ///     A click with no enemy in reach does nothing.
    ///   - It ends when the time runs out, you go down, the wave ends or the slot changes.
    /// All damage scales by your damage and the ultimate's power.
    /// Like Living Bolt, your own game takes the click (LocalRavager) and sends Msg.WeaponDash (WeaponAlt.Ravager); the host picks
    /// the leg and the enemy (HostRavagerLash) from its own copy of the walk (Player.RvGait).
    /// Replicated: Player.RvT (seconds since the cast; 0 = off) and RvLen (its length). FxKind.RavagerLash is one lash: Pos
    /// the foot where it starts, Pos2 (leg, enemy id) as numbers, Radius the owner's id. Drawn by Game/BloodRavagerLook.cs.
    public partial class Game
    {
        public const float RvTime = 9f, RvTimeMastered = 12f, RvSpeed = 1.8f;
        public const float RvStomp = 7f, RvStompR = 24f, RvStompShove = 6f;
        public const float RvRam = 12f, RvRamSpeed = 200f, RvRamGap = 0.3f, RvRamShove = 16f;
        public const float RvLashR = 140f, RvLashDmg = 20f, RvLashSplash = 16f, RvLashShove = 18f;
        public const float RvLashTime = 0.2f, RvLashHitAt = 0.4f, RvLashGap = 0.07f, RvBotGap = 0.25f;
        public const int RvLashMax = 3;
        public static readonly Rgba RvBlood = new Rgba(0.66f, 0.06f, 0.13f), RvBloodHi = new Rgba(1f, 0.29f, 0.36f);
        public static readonly Rgba RvBloodDk = new Rgba(0.29f, 0.02f, 0.06f);

        /// Blood Ravager's entry: key 1 of the DPS Hemomancer's Ultimate row. Upgrades.Abilities gathers it.
        internal static readonly AbilityDef[] RavagerUltimates =
        {
            new AbilityDef
            {
                Id = AbilityId.BloodRavager, Kit = Kit.Hemomancer, Role = Role.Dps, Choice = AbilityDef.Ult, UltKey = 1,
                Name = "Blood Ravager", Cooldown = 70f,
                Desc = "Eight blood legs burst out of you for 9 s: run fast and trample, and every click lashes a leg at an enemy nearby.",
                Capstone = "Lasts 12 s.",
                Bot = b => b.HaveTarget && b.TargetD < RvLashR + 120f,
            },
        };

        /// One leg lashing (host).
        public sealed class RavagerLash
        {
            public int Leg, Target;
            public float T;
            public bool Hit;
        }

        /// The legs are out.
        public static bool RavagerOn(Player p) => p.RvT > 0f && !p.Downed;

        /// Your speed share (Player.Speed) while the legs are out.
        public static float RavagerSpeed(Player p) => p.RvT > 0f && !p.Downed ? RvSpeed : 1f;

        void CastRavager(Player p, bool mastered, float pw)
        {
            p.RvLen = mastered ? RvTimeMastered : RvTime;
            p.RvT = 0.0001f;
            p.RvPw = pw;
            p.RvLashes.Clear();
            p.RvGait = new RavagerGait();
            p.RvGait.Reset(p.Pos, p.Aim);
            p.UltT = p.RvLen;
            BroadcastFx(FxKind.Ring, p.Pos, RvBloodHi, 60f, 0.35f, Sfx.SwingHeavy);
        }

        void EndRavager(Player p)
        {
            p.RvT = 0f;
            p.RvLashes.Clear();
            p.RvGait = null;
            if (p.Slots[Upgrades.UltSlot].Id == AbilityId.BloodRavager)
                p.UltT = 0f;
        }

        readonly Dictionary<long, float> ravRam = new Dictionary<long, float>();
        readonly List<long> ravRamDone = new List<long>();

        /// Host, every tick: the clock, the walk and its stomps, the ram, the lashes.
        void TickRavager(float dt)
        {
            if (ravRam.Count > 0)
            {
                ravRamDone.Clear();
                foreach (var kv in ravRam)
                    if (kv.Value - dt <= 0f)
                        ravRamDone.Add(kv.Key);
                foreach (var k in ravRamDone)
                    ravRam.Remove(k);
                foreach (var k in new List<long>(ravRam.Keys))
                    ravRam[k] -= dt;
            }
            foreach (var p in Players.Values)
            {
                if (p.RvT <= 0f)
                    continue;
                if (p.Downed || !Battle || p.Slots[Upgrades.UltSlot].Id != AbilityId.BloodRavager)
                {
                    EndRavager(p);
                    continue;
                }
                p.RvT += dt;
                p.RvLashCd -= dt;
                if (p.RvT >= p.RvLen)
                {
                    EndRavager(p);
                    continue;
                }
                var gait = p.RvGait ?? (p.RvGait = new RavagerGait());
                for (int i = 0; i < RavagerGait.Legs; i++)
                    gait.Busy[i] = false;
                foreach (var l in p.RvLashes)
                    gait.Busy[l.Leg] = true;
                gait.Update(p.Pos, p.Aim, dt);
                float scale = p.Stats.DmgMult * p.RvPw;
                for (int i = 0; i < RavagerGait.Legs; i++)
                    if ((gait.Landed & (1 << i)) != 0)
                        RavagerStomp(p, gait.Foot[i], scale);
                if (gait.Vel.Length > RvRamSpeed)
                    foreach (var e in AllFoes(by: p.Id))
                    {
                        long key = p.Id * 1000000L + e.Id;
                        if (e.SpawnT > 0f || e.Pos.FartherOrAt(p.Pos, p.Def.BodyR + e.Radius + 10f) || ravRam.ContainsKey(key))
                            continue;
                        ravRam[key] = RvRamGap;
                        HurtEnemy(e, RvRam * scale, p.Id);
                        if (!e.Def.IsBoss && !e.Def.Heavy)
                            KnockEnemy(e, AwayFrom(e.Pos, p.Pos, V2.FromAngle(p.Aim)) * RvRamShove);
                        p.RvRams++;
                    }
                for (int k = p.RvLashes.Count - 1; k >= 0; k--)
                {
                    var l = p.RvLashes[k];
                    l.T += dt;
                    Enemy e;
                    Enemies.TryGetValue(l.Target, out e);
                    if (!l.Hit && l.T >= RvLashTime * RvLashHitAt)
                    {
                        l.Hit = true;
                        if (e != null && e.Hp > 0f && e.Pos.CloserThan(p.Pos, RvLashR + e.Radius + 60f))
                            RavagerLashHit(p, e, scale);
                    }
                    if (l.T >= RvLashTime)
                        p.RvLashes.RemoveAt(k);
                }
            }
        }

        void RavagerStomp(Player p, V2 at, float scale)
        {
            p.RvStomps++;
            foreach (var e in AllFoes(by: p.Id))
            {
                if (e.SpawnT > 0f || e.Pos.FartherOrAt(at, RvStompR + e.Radius))
                    continue;
                HurtEnemy(e, RvStomp * scale, p.Id);
                if (!e.Def.IsBoss && !e.Def.Heavy)
                    KnockEnemy(e, AwayFrom(e.Pos, at, V2.FromAngle(p.Aim)) * RvStompShove);
                p.RvTrampled++;
            }
        }

        void RavagerLashHit(Player p, Enemy target, float scale)
        {
            V2 at = target.Pos, dir = AwayFrom(at, p.Pos, V2.FromAngle(p.Aim));
            foreach (var e in AllFoes(by: p.Id))
            {
                if (e != target && (e.SpawnT > 0f || e.Pos.FartherOrAt(at, RvLashSplash + e.Radius)))
                    continue;
                HurtEnemy(e, RvLashDmg * scale, p.Id);
                if (!e.Def.IsBoss && !e.Def.Heavy)
                    KnockEnemy(e, dir * RvLashShove);
                p.RvLashHits++;
            }
        }

        /// Host: a click (Msg.WeaponDash WeaponAlt.Ravager, or the host's own game). Picks the enemy and the leg, starts the lash.
        void HostRavagerLash(Player p)
        {
            if (!RavagerOn(p) || p.RvLashCd > 0f || p.RvLashes.Count >= RvLashMax || State == MatchState.GameOver || p.RvGait == null)
                return;
            var gait = p.RvGait;
            Enemy best = null;
            int leg = -1;
            float bestD = float.MaxValue;
            for (int pass = 0; pass < 2 && best == null; pass++)  // first the enemies no leg is lashing, then any
                foreach (var e in AllFoes(by: p.Id))
                {
                    if (e.SpawnT > 0f || e.Hp <= 0f || e.Pos.FartherOrAt(p.Pos, RvLashR + e.Radius))
                        continue;
                    if (pass == 0 && p.RvLashes.Exists(l => l.Target == e.Id))
                        continue;
                    for (int i = 0; i < RavagerGait.Legs; i++)
                    {
                        if (p.RvLashes.Exists(l => l.Leg == i))
                            continue;
                        float d = gait.Foot[i].DistanceTo(e.Pos);
                        if (d < bestD)
                        {
                            bestD = d;
                            best = e;
                            leg = i;
                        }
                    }
                }
            if (best == null)
                return;
            p.RvLashCd = RvLashGap * 0.5f;  // (half: the player's own game keeps the real gap; this only stops a flood)
            p.RvLashes.Add(new RavagerLash { Leg = leg, Target = best.Id });
            p.RvLashCount++;
            BroadcastFx(FxKind.RavagerLash, gait.Foot[leg], new V2(leg, best.Id), RvBloodHi, p.Id, RvLashTime, Sfx.SwingLight);
        }

        float ravLocalCd, ravBuffered;
        bool ravWasFiring;

        /// Your own game, each tick: while the legs are out Fire lashes (true: Fire is taken, nothing else shoots). Each click
        /// asks the host for a lash (one made inside the gap goes as soon as it ends); a bot holding Fire lashes every RvBotGap s.
        bool LocalRavager(Player me, bool firing, float dt)
        {
            ravLocalCd -= dt;
            ravBuffered -= dt;
            if (!RavagerOn(me))
            {
                ravWasFiring = false;
                ravBuffered = 0f;
                return false;
            }
            if (firing && (!ravWasFiring || Bot))
                ravBuffered = RvLashGap + 0.05f;
            ravWasFiring = firing;
            if (ravBuffered <= 0f || ravLocalCd > 0f || me.StunT > 0f || me.ParryActive || State == MatchState.GameOver)
                return true;
            ravBuffered = 0f;
            if (IsHost)
                HostRavagerLash(me);
            else
            {
                var w = Proto.Begin(Msg.WeaponDash);
                w.Write(WeaponAlt.Ravager);
                w.Write(me.Pos);
                session.SendToHost(Proto.End(w));
            }
            ravLocalCd = Bot ? RvBotGap : RvLashGap;
            return true;
        }

        // --- Network: the player's Blood Ravager fields (SyncPlayer) ---

        void SyncRavager(NetSync s, Player p)
        {
            s.Float(ref p.RvT);  // Blood Ravager
            if (p.RvT > 0f)
                s.Float(ref p.RvLen);
        }
    }
}
