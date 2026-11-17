using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Tank Knight's Bulwark Barrier (the lobby's 4th key; mockup Docs/Mockups/bastion-shield.html): a kite shield.
    ///   Guard (always on): the shield covers your front half. Every enemy shot coming at it from the front stops there, red
    ///     aimed shots included (BulwarkBlocks, from the bullet loop in Combat.cs); 180° across, 150° while a chain step plays.
    ///   Fire: Shield Chain, three steps sharing Player.Combo with the Lance (step in the low 3 bits, a count above them):
    ///     1 Rim Cut (a 110° cut with the rim), 2 Backhand (a 160° return sweep), 3 Boss Slam (a 60° thrust of the boss that
    ///     shoves BbKnock[2] and stuns BbSlamStun). The next press counts once BbNext of a step has played; BbChainWindow after
    ///     a step ends without one, the chain starts over. Each step moves you in a little on your own game (LocalBulwark).
    ///   Alt (hold the weapon-action key; Msg.WeaponDash DashBraceOn / DashBraceOff): Brace. You move at BraceSlow, the guard
    ///     widens to 220° and every shot it stops is soaked into Ram (Player.BbRam, 0-100: BbRamShot a shot, BbRamBig a red or
    ///     big one). Ram drains BbRamDrain a second once you have been out of the Brace for BbRamHold.
    ///   Fire while braced: Shield Bash. A dash of BbBashBase + BbBashPer x Ram px along your aim (untouchable while it runs);
    ///     everything in its lane takes BbBashDmg + BbBashDmgPer x Ram (at the weapon's 12 damage), is shoved and stunned, more
    ///     with more Ram. A bash with no Ram is allowed: a short shove. At full Ram the soaked shots burst out ahead of you as up
    ///     to BbShardsMax steel shards. Your own game moves you when Player.BbBashes changes (LocalBulwark), as the Arc Knuckles'
    ///     Rocket Dash does; the host lands the hit as one box Swing along the lane.
    ///   Extra projectiles: +15 px chain reach, +10% chain damage and +20% bash damage each. Iron Boss (Rare card): Ram fills 30% faster, the bash
    ///     hits 30% harder.
    ///   The look (Game/BulwarkBarrierLook.cs) draws the shield round you from the replicated aim, BbBrace and BbRam, each chain
    ///   step from its FxKind.BulwarkStep, the bash from its FxKind.BulwarkBash, and each stopped shot from FxKind.BulwarkBlock.
    public partial class Game
    {
        public const float BbChainWindow = 0.9f;
        public static readonly float[] BbDur = { 0.34f, 0.38f, 0.56f };      // each step's length (at the base fire rate)
        public static readonly float[] BbNext = { 0.7f, 0.7f, 0.9f };       // how much of a step plays before the next press counts
        public static readonly float[] BbHitAt = { 0.4f, 0.4f, 0.38f };      // where in the step its hit lands
        public static readonly float[] BbMult = { 1f, 14f / 12f, 26f / 12f };  // each step against the weapon's Damage (12, 14, 26)
        public static readonly float[] BbArc = { 110f, 160f, 60f };          // degrees across
        public static readonly float[] BbExtraReach = { 0f, 6f, 16f };       // past the weapon's Reach
        public static readonly float[] BbKnock = { 18f, 40f, 200f };         // px shoved
        public static readonly float[] BbStepIn = { 10f, 10f, 26f };         // px you move in (your own game)
        public const float BbSlamStun = 0.6f;

        public const float BbGuardGap = 12f;                                   // the plate's distance outside your body
        public const float BbGuardHalf = MathX.Pi / 2f, BbSwingHalf = 75f * MathX.Pi / 180f, BbBraceHalf = 110f * MathX.Pi / 180f;
        public const float BraceSlow = 0.38f, BbLockout = 0.35f, BbBlockSoundGap = 0.08f;

        public const float BbRamMax = 100f, BbRamShot = 7f, BbRamBig = 14f, BbRamHold = 4f, BbRamDrain = 12f;
        public const int BbShardsMax = 16, BbShardsMin = 8;

        public const float BbBashBase = 70f, BbBashPer = 2f, BbBashTime = 0.2f, BbBashTimePer = 0.0013f, BbBashWidth = 30f, BbBashFront = 34f;
        public const float BbBashDmg = 20f, BbBashDmgPer = 0.9f, BbBashKnock = 120f, BbBashKnockPer = 2f, BbBashStun = 0.4f, BbBashStunPer = 0.01f;
        public const float BbBashLandAt = 0.7f, BbBashFxTail = 0.9f, BbBashGrace = 0.1f;
        public const float BbShardDmg = 12f, BbShardSpeed = 560f, BbShardLife = 0.45f, BbShardFan = 1.24f;

        public static readonly Rgba BbGlow = new Rgba(0.56f, 0.82f, 1f), BbGold = new Rgba(1f, 0.82f, 0.48f), BbSpark = new Rgba(1f, 0.85f, 0.55f);
        const byte DashBraceOn = 40, DashBraceOff = 41;

        /// How far from your centre the shield stands (where shots stop).
        public static float BulwarkRadius(Player p) => p.Def.BodyR + BbGuardGap;

        /// Half the guard's width for `p` now: wider braced, narrower while a chain step plays.
        public static float BulwarkHalf(Player p) => p.BbBrace ? BbBraceHalf : p.BbSwingT < p.BbSwingDur ? BbSwingHalf : BbGuardHalf;

        /// How far a Shield Bash with `ram` goes, and how long it takes.
        public static float BashLength(float ram) => BbBashBase + BbBashPer * ram;
        public static float BashTime(float ram) => BbBashTime + BbBashTimePer * ram;

        class BashShards
        {
            public int Owner, N;
            public float T, Aim;
        }

        readonly List<BashShards> bashShards = new List<BashShards>();

        /// Host: one press of the primary with the Bulwark Barrier (from FireKnightWeapon): the next chain step, or the Shield
        /// Bash while braced.
        void FireBulwark(Player p, float aim, float dmg, int extra)
        {
            if (p.BbBrace)
            {
                HostBulwarkBash(p, aim, dmg, extra);
                return;
            }
            var w = p.Weapon;
            int last = TwinBladesMotion.StepOf(p.Combo);
            int step = last >= 1 && last < 3 && p.ComboT > 0f ? last + 1 : 1;
            int count = ((p.Combo >> 3) + 1) & 31;
            p.Combo = (count << 3) | step;
            float dur = BbDur[step - 1];
            p.ComboT = dur + BbChainWindow;
            p.FireCd = dur * BbNext[step - 1] / FireRate(p);
            p.BbSwingT = 0f;
            p.BbSwingDur = dur;
            float reach = w.Reach + BbExtraReach[step - 1] + w.Grow * extra;
            var s = NewSwing(p, aim, reach, BbArc[step - 1], w.Damage * dmg * BbMult[step - 1] * (1f + 0.1f * extra));
            s.Delay = dur * BbHitAt[step - 1];
            s.Knock = BbKnock[step - 1];
            s.NoFx = true;  // the step's own effect draws it
            s.Bulwark = (byte)step;
            s.Sound = step == 3 ? Sfx.KnuckleSlam : Sfx.LanceSweep;
            if (step == 3)
            {
                s.Heavy = true;
                s.Stun = BbSlamStun;
                s.StarStun = true;
            }
            QueueSwing(s);
            // The whole step, drawn from its age: Radius carries who and which step.
            BroadcastFx(FxKind.BulwarkStep, p.Pos, p.Pos + V2.FromAngle(aim) * MeleeReach(p, reach), BbGlow, p.Id * 10 + step, dur + 0.2f, Sfx.None);
        }

        /// Host: the Shield Bash, `p` braced and pressing Fire along `aim`. Ram sets how far, how hard and how long it stuns; at
        /// full Ram the soaked shots burst out as shards when it lands.
        void HostBulwarkBash(Player p, float aim, float dmg, int extra)
        {
            if (p.Downed || p.StunT > 0f || p.ParryActive || p.SwallowT > 0f || State == MatchState.GameOver)
                return;
            float ram = Math.Max(0f, Math.Min(BbRamMax, p.BbRam));
            V2 from = p.Pos, dir = V2.FromAngle(aim);
            V2 to = ClampToArena(from + dir * BashLength(ram), p.Def.BodyR);
            float len = to.DistanceTo(from), time = BashTime(ram);
            bool full = ram >= BbRamMax - 0.01f;
            p.BbBashes++;
            p.BbBashTo = to;
            p.InvulnT = Math.Max(p.InvulnT, time + BbBashGrace);
            p.FireCd = Math.Max(p.FireCd, time + 0.2f);
            p.BbBrace = false;
            p.BbLock = time + BbLockout;
            p.Combo &= ~7;
            p.ComboT = 0f;
            var w = p.Weapon;
            var s = NewSwing(p, aim, 0f, 0f, w.Damage / 12f * dmg * (BbBashDmg + BbBashDmgPer * ram) * (1f + 0.2f * extra) * p.Stats.BbBashMult);
            s.Reach = len + BbBashFront;  // the lane, out to the shield where you stop
            s.HalfWidth = BbBashWidth;
            s.FromSet = true;
            s.From = from;
            s.Knock = BbBashKnock + BbBashKnockPer * ram;
            s.Stun = BbBashStun + BbBashStunPer * ram;
            s.StarStun = true;
            s.Heavy = true;
            s.Thrust = true;
            s.NoFx = true;
            s.Bulwark = 4;
            s.Sound = Sfx.KnuckleRocketHit;
            s.Delay = time * BbBashLandAt;
            QueueSwing(s);
            if (full)
                bashShards.Add(new BashShards { Owner = p.Id, T = time, Aim = aim, N = Math.Max(BbShardsMin, Math.Min(BbShardsMax, p.BbStored)) });
            p.BbRam = 0f;
            p.BbStored = 0;
            // The dash, its impact and (at full Ram) the shards, drawn from its age: Radius carries who and the Ram spent.
            BroadcastFx(FxKind.BulwarkBash, from, to, full ? BbGold : BbGlow, p.Id * 1000 + (int)Math.Round(ram), time + BbBashFxTail, Sfx.KnuckleRocket);
        }

        /// Host: the weapon-action key went down (on) or up with the Bulwark Barrier.
        void HostBrace(Player p, bool on)
        {
            if (p.Weapon.Id != WeaponId.BulwarkBarrier)
                return;
            p.BbHeld = on;
            if (!on)
            {
                p.BbBrace = false;
                return;
            }
            TryRaiseBrace(p);
        }

        bool CanBrace(Player p) => !p.Downed && p.StunT <= 0f && p.SwallowT <= 0f && !p.ParryActive && p.BbLock <= 0f && State != MatchState.GameOver;

        void TryRaiseBrace(Player p)
        {
            if (p.BbBrace || !CanBrace(p))
                return;
            p.BbBrace = true;
            p.BbIdle = 0f;
            BroadcastSound(Sfx.GreatRaise);
        }

        /// Host, every tick: the Brace held or dropped, Ram draining, shards leaving a full bash.
        void TickBulwark(float dt)
        {
            foreach (var p in Players.Values)
            {
                p.BbLock = Math.Max(0f, p.BbLock - dt);
                p.BbSwingT = Math.Min(p.BbSwingT + dt, 5f);
                p.BbBlockT = Math.Max(0f, p.BbBlockT - dt);
                if (p.Weapon.Id != WeaponId.BulwarkBarrier || p.Downed)
                {
                    if (p.BbBrace || p.BbRam > 0f || p.BbStored > 0 || p.BbHeld)
                        ClearBulwark(p);
                    continue;
                }
                if (p.BbBrace && !CanBrace(p) && p.BbLock <= 0f)
                    p.BbBrace = false;  // (stunned, swallowed or parrying: the Brace drops; the key still held raises it again)
                if (!p.BbBrace && p.BbHeld)
                    TryRaiseBrace(p);
                if (p.BbBrace)
                    p.BbIdle = 0f;
                else if ((p.BbIdle += dt) > BbRamHold && p.BbRam > 0f)
                {
                    p.BbRam = Math.Max(0f, p.BbRam - BbRamDrain * dt);
                    if (p.BbRam <= 0f)
                        p.BbStored = 0;
                }
            }
            for (int i = bashShards.Count - 1; i >= 0; i--)
            {
                var sh = bashShards[i];
                sh.T -= dt;
                if (sh.T > 0f)
                    continue;
                bashShards.RemoveAt(i);
                Player p;
                if (!Players.TryGetValue(sh.Owner, out p) || p.Downed)
                    continue;
                V2 at = p.Pos + V2.FromAngle(sh.Aim) * (BulwarkRadius(p) + 8f);
                for (int k = 0; k < sh.N; k++)
                {
                    float a = sh.Aim + BbShardFan * ((sh.N > 1 ? k / (float)(sh.N - 1) : 0.5f) - 0.5f);
                    var b = SpawnBullet(Defs.SteelShard, at, V2.FromAngle(a) * BbShardSpeed, 0f, BbShardLife, BbShardDmg * p.Stats.DmgMult * p.Stats.BbBashMult, p.Id);
                    b.Pierce = 0;
                }
            }
        }

        /// Everything the shield holds goes (put away, or its owner down).
        void ClearBulwark(Player p)
        {
            p.BbBrace = p.BbHeld = false;
            p.BbRam = 0f;
            p.BbStored = 0;
        }

        /// Host, from the bullet loop: an enemy shot at `p`'s shield from the front stops there (red aimed shots too). True when
        /// it stopped (BulwarkHit then soaks or blocks it).
        bool BulwarkBlocks(Player p, Bullet b)
        {
            if (p.Weapon.Id != WeaponId.BulwarkBarrier || p.Downed || p.SwallowT > 0f)
                return false;
            float r = BulwarkRadius(p) + b.Radius;
            V2 rel = b.Pos - p.Pos;
            if (rel.LengthSq >= r * r || b.Vel.X * rel.X + b.Vel.Y * rel.Y >= 0f)
                return false;  // (not at the shield yet, or already going away)
            return AngleTo(p.Pos, b.Pos, p.Aim) < BulwarkHalf(p);
        }

        /// A shot stopped on the shield: braced, it is soaked into Ram; otherwise it just stops.
        void BulwarkHit(Player p, Bullet b)
        {
            V2 rel = b.Pos - p.Pos;
            V2 at = p.Pos + (rel.IsZero ? V2.FromAngle(p.Aim) : rel.Normalized()) * BulwarkRadius(p);
            bool sound = p.BbBlockT <= 0f;
            if (sound)
                p.BbBlockT = BbBlockSoundGap;
            if (p.BbBrace)
            {
                bool wasFull = p.BbRam >= BbRamMax - 0.01f;
                bool big = b.Kind == Defs.PierceShot || b.Kind == Defs.BigShot;
                p.BbRam = Math.Min(BbRamMax, p.BbRam + (big ? BbRamBig : BbRamShot) * p.Stats.BbRamMult);
                p.BbStored = Math.Min(BbShardsMax, p.BbStored + 1);
                bool nowFull = !wasFull && p.BbRam >= BbRamMax - 0.01f;
                BroadcastFx(FxKind.BulwarkBlock, at, new V2(p.Id, 1), BbGlow, b.Radius, 0.3f, nowFull ? Sfx.GreatFull : sound ? Sfx.GreatBlock : Sfx.None);
            }
            else
                BroadcastFx(FxKind.BulwarkBlock, at, new V2(p.Id, 0), BbSpark, b.Radius, 0.25f, sound ? Sfx.Clang : Sfx.None);
        }

        /// Raising the parry cancels the chain: back to the Rim Cut, a step still winding up never lands.
        void ResetBulwarkChain(Player p)
        {
            p.Combo &= ~7;
            p.ComboT = 0f;
            p.FireCd = 0f;
            p.BbSwingT = 5f;
            pendingSwings.RemoveAll(s => s.Owner == p.Id && s.Bulwark >= 1 && s.Bulwark <= 3 && s.Delay > 0f);
        }

        // --- Your own game ------------------------------------------------------------------------------------------

        int bulwarkCountSeen = -1, bulwarkBashSeen = -1;
        float bulwarkStepWait = -1f, bulwarkStepFwd, bulwarkStepTime;
        V2 bulwarkStepDir;

        /// Your own game, every tick: a bash the host started moves you, each chain step moves you in a little, and the
        /// weapon-action key's edges go to the host (the Brace is held). True while the Bulwark Barrier is out.
        bool LocalBulwark(Player me, V2 aimAt, bool pressed, bool released, float dt)
        {
            bool mine = me.Weapon.Id == WeaponId.BulwarkBarrier;
            if (!mine || bulwarkBashSeen < 0)
                bulwarkBashSeen = me.BbBashes;  // (picking the weapon up never replays an old count)
            if (me.BbBashes != bulwarkBashSeen)
            {
                bulwarkBashSeen = me.BbBashes;
                bulwarkStepWait = -1f;
                V2 d = me.BbBashTo - me.Pos;
                float len = d.Length;
                if (len > 1f && !me.Downed && me.StunT <= 0f)
                {
                    float time = BashTime(Math.Max(0f, (len - BbBashBase) / BbBashPer));
                    dashSpeed = len / Math.Max(0.05f, time);
                    DashDir = d / len;
                    DashT = time;
                }
                if (!IsHost)
                    me.BbBrace = false;
            }
            if (bulwarkStepWait >= 0f && (bulwarkStepWait -= dt) < 0f)
                StartBulwarkStep(me);
            int count = me.Combo >> 3;
            if (bulwarkCountSeen < 0 || !mine)
            {
                bulwarkCountSeen = count;
                bulwarkStepWait = -1f;
            }
            else if (count != bulwarkCountSeen)
            {
                bulwarkCountSeen = count;
                int step = TwinBladesMotion.StepOf(me.Combo);
                if (step >= 1 && step <= 3 && !me.Downed && me.StunT <= 0f && !me.Parrying)
                {
                    float dur = BbDur[step - 1];
                    bulwarkStepDir = V2.FromAngle(me.Aim);
                    bulwarkStepFwd = BbStepIn[step - 1];
                    bulwarkStepTime = dur * 0.3f;
                    bulwarkStepWait = dur * (step == 3 ? 0.3f : 0.12f);
                }
            }
            if (!mine)
                return false;
            if (pressed || released)
            {
                byte kind = pressed ? DashBraceOn : DashBraceOff;
                if (!IsHost && pressed && !me.Downed && me.StunT <= 0f)
                    me.BbBrace = true;  // (the slow starts at once on your own screen; the host's snapshot agrees)
                else if (!IsHost && released)
                    me.BbBrace = false;
                if (IsHost)
                    HostWeaponDash(MyId, kind, aimAt);
                else
                {
                    var w = Proto.Begin(Msg.WeaponDash);
                    w.Write(kind);
                    w.Write(aimAt);
                    session.SendToHost(Proto.End(w));
                }
            }
            return true;
        }

        void StartBulwarkStep(Player me)
        {
            bulwarkStepWait = -1f;
            if (me.Downed || me.StunT > 0f || DashT > 0f || me.Parrying || me.Weapon.Id != WeaponId.BulwarkBarrier)
                return;
            V2 to = ClampToArena(me.Pos + bulwarkStepDir * bulwarkStepFwd, me.Def.BodyR);
            DashDir = bulwarkStepDir;
            dashSpeed = bulwarkStepFwd / bulwarkStepTime;
            DashT = to.DistanceTo(me.Pos) / dashSpeed;
        }
    }
}
