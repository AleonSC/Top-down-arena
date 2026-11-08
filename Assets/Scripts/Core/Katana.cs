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
            public bool Emp;   // empowered: the chain was full when it began
            public float T;  // seconds until the cuts land
            public int Cue;  // the sounds already raised along the way: 1 the cut, 2 the sheathe
        }

        readonly List<KatanaStrikeRun> katanaStrikes = new List<KatanaStrikeRun>();
        static readonly Sfx[] KatanaChainSfx = { Sfx.KatanaChain1, Sfx.KatanaChain2, Sfx.KatanaChain3, Sfx.KatanaChain4, Sfx.KatanaChain5 };

        // Local (your own screen): aiming and the strike you are in the middle of. The front end reads these.
        public bool KatanaAiming;      // holding the key with a full meter
        public int KatanaMark;         // the enemy it would cut to (an id), 0 when there is none
        public float KatanaAimT;       // how long you have been aiming
        public float KatanaNoMeterT;   // > 0 for a moment after pressing the key without a full meter (the bar shakes)
        byte katanaLungesSeen;         // the lunge count you last saw; a change means one was just thrown
        float katanaLungeIn = -1f;     // > 0: seconds until you step forward with it
        float katanaMeterSeen;         // the meter last frame (a rise to full plays KatanaReady)
        bool katanaAimSeen;            // aiming last frame
        int katanaMarkSeen;            // the marked enemy last frame
        float katanaStrikeT = -1f;     // >= 0 while you are in the strike, counting up from the release
        V2 katanaTo;
        bool katanaDashed, katanaLanded;
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
            {
                p.KatanaMeter = 0f;
                p.KatanaChain = 0;
            }
            else if (p.KatanaMeter > 0f && p.KatanaChain == 0 && p.KatanaIdle > KatanaDrainDelay)  // (on a streak the meter holds)
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

        /// Raising the parry is an animation cancel for weapons that attack in a fixed order: the chain goes back to its first
        /// step, the wait for the next step is cleared, and an attack that hasn't landed yet never does. Builds you have earned
        /// (the strike meter and its chain, the Rapier's streak) are left alone. Called on the host.
        void ResetAttackChains(Player p)
        {
            if (p.Weapon.Id == WeaponId.BoStaff)
            {
                p.BoStep = 0;  // back to the slash
                p.FireCd = 0f;
                p.TapQueued = false;
                if (boShoves.RemoveAll(sh => sh.Owner == p.Id) > 0)  // a shove still drawing back never lands (and costs no cooldown)
                    p.ShoveCd = 0f;
                return;
            }
            if (p.Weapon.Id == WeaponId.Kusarigama)
            {
                p.KusariStep = 0;  // back to the sickle cut (a throw already away still lands)
                p.FireCd = 0f;
                return;
            }
            if (p.Weapon.Id == WeaponId.Greatsword)
            {
                ResetGreatChain(p);  // Greatsword.cs
                return;
            }
            if (p.Weapon.Id == WeaponId.TwinBlades)
            {
                ResetTwinChain(p);  // TwinBlades.cs
                return;
            }
            if (p.Weapon.Id == WeaponId.Lightbringer)
            {
                ResetLightChain(p);  // Lightbringer.cs
                return;
            }
            if (p.Weapon.Id == WeaponId.Censer)
            {
                ResetCenserChain(p);  // Censer.cs
                return;
            }
            if (p.Weapon.Id != WeaponId.Katana)
                return;
            p.KatanaStep = 0;
            p.KatanaGap = 5f;
            p.FireCd = 0f;
            p.TapQueued = false;
            foreach (var s in pendingSwings)
                if (s.Owner == p.Id && s.DrawCut && s.Delay > 0f)  // a lunge still pulling back
                {
                    pendingSwings.Remove(s);
                    p.KatanaLunges--;  // (your own game cancels the step forward that came with it)
                    break;
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
            KatanaLocalSounds(me);
            // A lunge thrown (the host counts them): as it lands you step forward with it, on your own screen like every move.
            if (me.KatanaLunges != katanaLungesSeen)
            {
                bool cancelled = (byte)(me.KatanaLunges + 1) == katanaLungesSeen;  // the host took a lunge back (a parry raised before it landed)
                katanaLungesSeen = me.KatanaLunges;
                if (cancelled)
                    katanaLungeIn = -1f;
                else if (me.Weapon.Id == WeaponId.Katana)
                    katanaLungeIn = KatanaLungeWind;
            }
            if (me.Parrying)
                katanaLungeIn = -1f;  // raising the parry before the step starts cancels it
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
                if (katanaDashed && !katanaLanded && katanaStrikeT >= KatanaWind + KatanaCut)
                {
                    me.Pos = katanaTo;
                    DashT = 0f;
                    katanaLanded = true;  // (you stand where you landed through the held frame)
                }
                if (katanaStrikeT >= KatanaWind + KatanaCut + KatanaHold)
                    katanaStrikeT = -1f;
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
                {
                    KatanaNoMeterT = 0.4f;  // not charged: the bar shakes
                    Sounds.Add(Sfx.KatanaEmpty);
                }
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

        /// Your own screen only, never sent: the meter reaching full (unless a chain's chime is already saying so), the aim
        /// beginning and the mark moving to another enemy. Reads last frame's state, so it runs first in LocalKatana.
        void KatanaLocalSounds(Player me)
        {
            bool katana = me.Weapon.Id == WeaponId.Katana;
            if (katana && me.KatanaMeter >= KatanaMeterMax && katanaMeterSeen < KatanaMeterMax && me.KatanaChain == 0 && !me.Downed)
                Sounds.Add(Sfx.KatanaReady);
            katanaMeterSeen = katana ? me.KatanaMeter : 0f;
            if (KatanaAiming && !katanaAimSeen)
                Sounds.Add(Sfx.KatanaAim);
            else if (KatanaAiming && KatanaMark != 0 && katanaMarkSeen != 0 && KatanaMark != katanaMarkSeen)
                Sounds.Add(Sfx.KatanaSwitch);
            katanaAimSeen = KatanaAiming;
            katanaMarkSeen = KatanaAiming ? KatanaMark : 0;
        }

        /// What the Alt strike would deal to its target now: 120, or 240 when the chain is full, times your damage. The aim
        /// overlay uses it to show which enemies a strike would kill (a guide: a few enemies' own states change it).
        public float KatanaStrikeDamage(Player p)
        {
            float dmg = (p.KatanaChain >= KatanaChainNeed ? KatanaEmpDmg : KatanaStrikeDmg) * p.Stats.DmgMult;
            if (Has(RunMod.GlassCannon))
                dmg *= Modifiers.GlassCannonDealt;
            return dmg;
        }

        public bool KatanaWouldKill(Player p, Enemy e) => e.Hp <= KatanaStrikeDamage(p);

        /// Host: a swipe or the lunge cut something. Sent as an effect that draws nothing: it carries the cut's sound, and
        /// the striker's own game holds the picture for a moment on a lunge (Game/Juice.cs).
        void KatanaHitCue(Player p, bool lunge, int hits)
        {
            BroadcastFx(FxKind.KatanaHitCue, p.Pos, new V2(p.Id, 0f), KatanaRose.WithAlpha(lunge ? 1f : 0.5f), hits, 0.2f, Sfx.KatanaHit);
        }

        void StartKatanaStrike(Player me, Enemy target)
        {
            katanaTo = KatanaStrikeEnd(me.Pos, target.Pos, me.Def.BodyR);
            katanaStrikeT = 0f;
            katanaDashed = katanaLanded = false;
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
            bool emp = p.KatanaChain >= KatanaChainNeed;
            float total = KatanaWind + KatanaCut + KatanaHold;
            p.InvulnT = Math.Max(p.InvulnT, total + 0.15f);
            V2 to = KatanaStrikeEnd(p.Pos, e.Pos, p.Def.BodyR);
            katanaStrikes.Add(new KatanaStrikeRun { Owner = p.Id, Target = e.Id, From = p.Pos, To = to, Emp = emp, T = total });
            // The whole show (stance, cut, held frame, fading line) is one effect, drawn from its age (Game/KatanaLook.cs).
            BroadcastFx(FxKind.KatanaStrike, p.Pos, to, KatanaRose.WithAlpha(emp ? 1f : 0.5f), p.Id, total + 1.2f, Sfx.KatanaStance);
        }

        /// Host: land the strikes that are due. The target takes the full cut (240 when empowered) and everything the line
        /// crossed half of it (120 when empowered). A kill sets the meter back to full and adds a chain; a target that lives
        /// spends the meter for nothing and ends the streak. An empowered strike that kills starts the chain again.
        void TickKatanaStrikes(float dt)
        {
            for (int i = katanaStrikes.Count - 1; i >= 0; i--)
            {
                var k = katanaStrikes[i];
                k.T -= dt;
                float age = KatanaWind + KatanaCut + KatanaHold - k.T;
                if (k.Cue == 0 && age >= KatanaWind - (k.Emp ? KatanaEmpLead : 0f))
                {
                    k.Cue = 1;
                    BroadcastSound(k.Emp ? Sfx.KatanaEmpStrikeCut : Sfx.KatanaStrikeCut);  // (the empowered one swells in first)
                }
                if (k.Cue == 1 && age >= KatanaWind + KatanaCut + KatanaSheatheAt)
                {
                    k.Cue = 2;
                    BroadcastSound(Sfx.KatanaSheathe);
                }
                if (k.T > 0f)
                    continue;
                katanaStrikes.RemoveAt(i);
                Player p;
                if (!Players.TryGetValue(k.Owner, out p) || p.Downed)
                    continue;
                V2 path = k.To - k.From;
                float len = Math.Max(1f, path.Length);
                V2 dir = path / len;
                bool killedTarget = !Enemies.ContainsKey(k.Target);  // (already gone: it counts)
                foreach (var e in new List<Enemy>(Enemies.Values))
                {
                    if (IsSpawning(e) || e.Hp <= 0f)
                        continue;
                    bool target = e.Id == k.Target;
                    float along = Math.Max(0f, Math.Min(len, Dot(e.Pos - k.From, dir)));
                    if (!target && e.Pos.DistanceTo(k.From + dir * along) > e.Radius + KatanaRouteWidth)
                        continue;
                    float hit = (target ? (k.Emp ? KatanaEmpDmg : KatanaStrikeDmg) : (k.Emp ? KatanaEmpRoute : KatanaRouteDmg)) * p.Stats.DmgMult;
                    hit *= NinjaHitMult(p.Id, Defs.KnightSlash, e);  // Vanish's ambush (Ninja.cs)
                    HurtEnemy(e, hit, p.Id);
                    OnBulletHit(new Bullet { Pos = e.Pos, Vel = dir, Owner = p.Id, Dmg = hit, Kind = Defs.KnightSlash }, e);
                    bool dead = e.Hp <= 0f;
                    if (target)
                        killedTarget = dead;
                    BroadcastFx(FxKind.KatanaCut, e.Pos, dir, KatanaRose.WithAlpha(dead ? 1f : 0.5f), e.Radius, 0.9f, target ? Sfx.KatanaBlow : Sfx.None);
                }
                if (killedTarget)
                {
                    p.KatanaMeter = KatanaMeterMax;  // the meter stays full: strike again at once
                    p.KatanaIdle = 0f;
                    p.KatanaChain = k.Emp ? (byte)0 : (byte)Math.Min(250, p.KatanaChain + 1);  // (only the target's kill counts)
                    if (p.KatanaChain > 0)  // a chime up the scale (each starts a moment after the blow); the fifth also rings in the empowered strike
                        BroadcastSound(KatanaChainSfx[Math.Min(KatanaChainSfx.Length, (int)p.KatanaChain) - 1]);
                    if (p.KatanaChain == KatanaChainNeed)
                        BroadcastSound(Sfx.KatanaEmpReady);
                }
                else
                {
                    p.KatanaChain = 0;  // it lived: the meter stays spent and the streak is over
                    BroadcastSound(Sfx.KatanaLost);
                }
            }
        }
    }
}
