using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Katana's combo and Alt strike (Docs/KatanaCombo.md). The chain is swipe, swipe, lunge, always in that order; the
    /// swings themselves are queued in FireNinjaWeapon (Ninja.cs) and land through the usual Swing pipeline (Knight.cs).
    /// Every enemy a step hits adds to the strike meter, which drains once you stop hitting. At a full meter, holding your
    /// weapon-action key marks the enemy nearest your cursor; letting go cuts across the arena and lands behind it.
    /// As with Lunge and the other weapon moves, your own game moves you (LocalKatana) and tells the host (Msg.WeaponDash,
    /// DashKatana); the host spends the meter, makes you untouchable and lands the cuts (HostKatanaStrike, TickKatanaStrikes).
    public partial class Game
    {
        public static readonly Rgba KatanaRose = new Rgba(1f, 0.56f, 0.66f);

        class KatanaStrikeRun
        {
            public int Owner, Target;
            public V2 From, To;
            public float T;  // seconds until the cuts land
        }

        readonly List<KatanaStrikeRun> katanaStrikes = new List<KatanaStrikeRun>();

        // Local (your own screen): aiming and the strike you are in the middle of. The front end reads these.
        public bool KatanaAiming;      // holding the key with a full meter
        public int KatanaMark;         // the enemy it would cut to (an id), 0 when there is none
        public float KatanaAimT;       // how long you have been aiming
        public float KatanaNoMeterT;   // > 0 for a moment after pressing the key without a full meter (the bar shakes)
        byte katanaLungesSeen;         // the lunge count you last saw; a change means one was just thrown
        float katanaLungeIn = -1f;     // > 0: seconds until you step forward with it
        float katanaStrikeT = -1f;     // >= 0 while you are in the strike, counting up from the release
        V2 katanaTo;
        bool katanaDashed;
        public bool KatanaStriking => katanaStrikeT >= 0f;
        public float KatanaStrikeTime => katanaStrikeT;

        // --- The chain and the meter (host; replicated on the Player) ---------------------------------------------

        /// Every tick, for every player: the chain forgetting its place and the meter draining.
        void TickKatana(Player p, float dt)
        {
            p.KatanaGap = Math.Min(p.KatanaGap + dt, 5f);
            if (p.KatanaGap > KatanaChainWindow)
                p.KatanaStep = 0;  // too long since the last step: back to the first swipe
            p.KatanaIdle = Math.Min(p.KatanaIdle + dt, 5f);
            if (p.Downed)
                p.KatanaMeter = 0f;
            else if (p.KatanaMeter > 0f && p.KatanaIdle > KatanaDrainDelay)
                p.KatanaMeter = Math.Max(0f, p.KatanaMeter - KatanaDrain * dt);
        }

        /// The Katana fires on its own timing: a press during a step is remembered and starts the next step the moment this one ends.
        void TickKatanaFire(Player p)
        {
            bool pressed = p.Firing && !p.WasFiring;
            p.WasFiring = p.Firing;
            if (pressed)
                p.TapQueued = true;
            if (p.ParryActive || State == MatchState.GameOver)
            {
                p.TapQueued = false;
                return;
            }
            if ((p.Firing || p.TapQueued) && p.FireCd <= 0f)
            {
                p.TapQueued = false;
                p.FireCd = p.Weapon.FireInterval / FireRate(p);  // (the step sets its own, in FireNinjaWeapon)
                FirePlayer(p);
            }
        }

        void KatanaGain(Player p, float amount)
        {
            p.KatanaMeter = Math.Min(KatanaMeterMax, p.KatanaMeter + amount);
            p.KatanaIdle = 0f;
        }

        // --- The Alt strike ----------------------------------------------------------------------------------------

        /// Where the strike leaves you: KatanaBehind past the enemy, on the far side from where you started.
        public V2 KatanaStrikeEnd(V2 from, V2 enemyPos, float bodyR)
        {
            V2 d = enemyPos - from;
            V2 dir = d.IsZero ? new V2(1f, 0f) : d.Normalized();
            return ClampToArena(enemyPos + dir * KatanaBehind, bodyR);
        }

        /// The enemy nearest `at` (anywhere in the arena), or null.
        Enemy KatanaTargetNear(V2 at)
        {
            Enemy best = null;
            foreach (var e in Enemies.Values)
                if (!IsSpawning(e) && e.Hp > 0f && (best == null || e.Pos.DistanceSq(at) < best.Pos.DistanceSq(at)))
                    best = e;
            return best;
        }

        /// Your own screen: hold the weapon-action key with a full meter to aim, let go to strike. Returns true while the
        /// Katana is out (nothing else uses the key then).
        bool LocalKatana(Player me, V2 aimAt, bool pressed, bool held, float dt)
        {
            KatanaNoMeterT = Math.Max(0f, KatanaNoMeterT - dt);
            // A lunge thrown (the host counts them): as it lands you step forward with it, on your own screen like every move.
            if (me.KatanaLunges != katanaLungesSeen)
            {
                katanaLungesSeen = me.KatanaLunges;
                if (me.Weapon.Id == WeaponId.Katana)
                    katanaLungeIn = KatanaLungeWind;
            }
            if (katanaLungeIn >= 0f && (katanaLungeIn -= dt) < 0f && !me.Downed && me.StunT <= 0f && DashT <= 0f && !KatanaStriking)
            {
                V2 dir = V2.FromAngle(me.Aim);
                V2 to = ClampToArena(me.Pos + dir * KatanaLungeStep, me.Def.BodyR);
                DashDir = dir;
                DashT = to.DistanceTo(me.Pos) / (KatanaLungeStep / KatanaLungeStepTime);
                dashSpeed = KatanaLungeStep / KatanaLungeStepTime;
            }
            if (me.Downed || me.StunT > 0f)
            {
                KatanaAiming = false;
                if (KatanaStriking)
                {
                    katanaStrikeT = -1f;
                    DashT = 0f;
                }
                return me.Weapon.Id == WeaponId.Katana;
            }
            if (KatanaStriking)
            {
                katanaStrikeT += dt;
                if (!katanaDashed && katanaStrikeT >= KatanaWind)
                {
                    katanaDashed = true;
                    V2 d = katanaTo - me.Pos;
                    float len = d.Length;
                    DashDir = len > 1f ? d / len : V2.Zero;
                    DashT = KatanaCut;
                    dashSpeed = len / KatanaCut;
                }
                if (katanaStrikeT >= KatanaWind + KatanaCut)
                {
                    me.Pos = katanaTo;
                    DashT = 0f;
                    katanaStrikeT = -1f;
                }
                return true;
            }
            if (me.Weapon.Id != WeaponId.Katana)
            {
                KatanaAiming = false;
                return false;
            }
            if (DashT > 0f)
            {
                KatanaAiming = false;
                return true;
            }
            if (pressed && !KatanaAiming)
            {
                if (me.KatanaMeter >= KatanaMeterMax)
                {
                    KatanaAiming = true;
                    KatanaAimT = 0f;
                }
                else
                    KatanaNoMeterT = 0.4f;  // not charged: the bar shakes
            }
            if (KatanaAiming)
            {
                KatanaAimT += dt;
                var mark = KatanaTargetNear(aimAt);
                KatanaMark = mark != null ? mark.Id : 0;
                if (!held || KatanaAimT > KatanaAimMax)
                {
                    KatanaAiming = false;
                    if (!held && mark != null)
                        StartKatanaStrike(me, mark);  // (a hold that runs out cancels instead)
                }
            }
            else
                KatanaMark = 0;
            return true;
        }

        void StartKatanaStrike(Player me, Enemy target)
        {
            katanaTo = KatanaStrikeEnd(me.Pos, target.Pos, me.Def.BodyR);
            katanaStrikeT = 0f;
            katanaDashed = false;
            if (IsHost)
                HostWeaponDash(MyId, DashKatana, target.Pos);
            else
            {
                var w = Proto.Begin(Msg.WeaponDash);
                w.Write(DashKatana);
                w.Write(target.Pos);
                session.SendToHost(Proto.End(w));
            }
        }

        /// Host: a Katana strike was asked for, aimed at the enemy nearest `at`. Spends the meter, makes the striker
        /// untouchable until the cut ends, and queues the cuts to land then.
        void HostKatanaStrike(Player p, V2 at)
        {
            if (p.Weapon.Id != WeaponId.Katana || p.Downed || p.StunT > 0f || p.KatanaMeter < KatanaMeterMax)
                return;
            Enemy e = null;
            foreach (var q in Enemies.Values)
                if (!IsSpawning(q) && q.Hp > 0f && q.Pos.DistanceTo(at) <= KatanaTargetSlack && (e == null || q.Pos.DistanceSq(at) < e.Pos.DistanceSq(at)))
                    e = q;
            if (e == null)
                return;
            p.KatanaMeter = 0f;
            p.KatanaIdle = 0f;
            p.KatanaStep = 0;
            p.InvulnT = Math.Max(p.InvulnT, KatanaWind + KatanaCut + 0.15f);
            katanaStrikes.Add(new KatanaStrikeRun { Owner = p.Id, Target = e.Id, From = p.Pos, To = KatanaStrikeEnd(p.Pos, e.Pos, p.Def.BodyR), T = KatanaWind + KatanaCut });
            BroadcastFx(FxKind.Ring, p.Pos, KatanaRose, 30f, 0.3f, Sfx.None);  // the stance
        }

        /// Host: land the strikes that are due: the target takes the full cut, and everything the line crossed takes half.
        void TickKatanaStrikes(float dt)
        {
            for (int i = katanaStrikes.Count - 1; i >= 0; i--)
            {
                var k = katanaStrikes[i];
                if ((k.T -= dt) > 0f)
                    continue;
                katanaStrikes.RemoveAt(i);
                Player p;
                if (!Players.TryGetValue(k.Owner, out p) || p.Downed)
                    continue;
                V2 path = k.To - k.From;
                float len = Math.Max(1f, path.Length);
                V2 dir = path / len;
                BroadcastFx(FxKind.KatanaStrike, k.From, k.To, KatanaRose, 0f, 1.2f, Sfx.SwingHeavy);
                foreach (var e in new List<Enemy>(Enemies.Values))
                {
                    if (IsSpawning(e) || e.Hp <= 0f)
                        continue;
                    bool target = e.Id == k.Target;
                    float along = Math.Max(0f, Math.Min(len, Dot(e.Pos - k.From, dir)));
                    if (!target && e.Pos.DistanceTo(k.From + dir * along) > e.Radius + KatanaRouteWidth)
                        continue;
                    float hit = (target ? KatanaStrikeDmg : KatanaRouteDmg) * p.Stats.DmgMult;
                    hit *= NinjaHitMult(p.Id, Defs.KnightSlash, e);  // Vanish's ambush (Ninja.cs)
                    HurtEnemy(e, hit, p.Id);
                    OnBulletHit(new Bullet { Pos = e.Pos, Vel = dir, Owner = p.Id, Dmg = hit, Kind = Defs.KnightSlash }, e);
                }
            }
        }
    }
}
