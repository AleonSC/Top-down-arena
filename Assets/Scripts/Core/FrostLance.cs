using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Tank Elementalist's Frost Lance, reworked into a chain (Docs/Mockups/frost-lance-rework.html, the owner's pick:
    /// F3 F4 F9 F7 F12, Alt A4).
    ///   Fire: five steps, each a host-side FrostStrike whose hits land at fixed moments of its life (FlHits), so the look
    ///   (Game/FrostLanceLook.cs, drawn from FxKind.FrostStep's age) and the hits agree:
    ///     0 Shiver Crescent: a 158° sweep in front (FlArcR).
    ///     1 Backhand Rime: the sweep back, a little wider; the ground it crosses stays rimed.
    ///     2 Frost Waltz: a full spin, hitting all round you.
    ///     3 Crystal Fan: you stab the floor and five crystals erupt in a fan (each ray hits on its own; one chill a strike).
    ///     4 Avalanche Slam: the finisher, an overhead slam FlSlamAt px ahead: a blast that shatters frozen enemies.
    ///   Each step's end pose is the next one's start (the look blends what's left), so held Fire flows round the chain.
    ///   FlGap[step] (x the fire interval / FlBaseInterval, / the fire rate) until the next step; stop for FlComboReset and it
    ///   starts over.
    ///   Chill: every strike that lands adds chill to the enemy (Enemy.FrostChill, replicated for the pips; FlChillHold s, then it
    ///   wears off) and slows it FlChillSlow. At FlFreezeAt it freezes (Enemy.FrozenT: FlFreezeTime, heavies FlHeavyFreeze,
    ///   Glacier longer; bosses never freeze, they only slow). A shattering hit (the Avalanche Slam, Absolute Zero's second wave)
    ///   on a frozen enemy breaks the ice for FlShatterMult x the damage (FxKind.FrostBurst Radius 1).
    ///   Parry: raising the shield cancels the step in your hands (its hits still to come never land; FxKind.FrostBreak shows
    ///   the lance bursting) but keeps your place in the chain. A perfect parry primes the Rime Counter (Player.FlRime,
    ///   replicated): the next step skips its wind-up (FlWind) and adds one more chill.
    ///   Alt (Msg.WeaponDash WeaponAlt.Zero): Absolute Zero. A ZeroCharge s charge (you move at ZeroSlow and can't attack), then a
    ///   white wave ZeroR round where you cast it: ZeroMult x the damage and ZeroChill chill (freezing all but bosses); at
    ///   ZeroShatterAt a second wave, ZeroShatterMult x and shattering. ZeroCd cooldown. Player.FlZeroT/FlZeroCd replicated.
    ///   Extra projectiles: +20% damage and 10% more size each. Homing Rounds make it all bigger (AreaDesc). Glacier: 20% more
    ///   reach, freezes last 0.6 s longer.
    public partial class Game
    {
        public const float FlBaseInterval = 0.4f, FlComboReset = 1.0f, FlShatterMult = 2.5f;
        public const float FlArcR = 178f, FlArcHalf = 1.38f, FlBackR = 176f, FlBackHalf = 1.48f, FlSpinR = 170f;
        public const float FlRayX0 = 38f, FlRayX1 = 198f, FlRayW = 14f, FlSlamAt = 140f, FlSlamR = 112f;
        public static readonly float[] FlRays = { -0.52f, -0.26f, 0f, 0.26f, 0.52f };
        public static readonly float[] FlDur = { 0.54f, 0.56f, 0.6f, 0.66f, 1.02f };   // how long each step's look plays
        public static readonly float[] FlLife = { 0.6f, 2.4f, 0.66f, 0.72f, 2.8f };    // the effect's life (the rime and the crater linger)
        public static readonly float[] FlGap = { 0.34f, 0.34f, 0.4f, 0.42f, 0.78f };   // until the next step (at the base interval)
        public static readonly float[] FlWind = { 0f, 0f, 0f, 0f, 0.2f };              // the wind-up a Rime Counter skips
        public static readonly float[] FlMult = { 0.8f, 0.8f, 0.7f, 0.5f, 1.35f };     // x the weapon's damage (the fan: each ray)
        public static readonly float[] FlKnock = { 40f, 45f, 42f, 26f, 75f };          // px shoved
        public const int FlFreezeAt = 3;
        public const float FlChillHold = 3f, FlChillSlow = 0.3f, FlFreezeTime = 2.4f, FlHeavyFreeze = 1.2f;
        public const float ZeroCharge = 0.7f, ZeroWaveAt = 0.78f, ZeroShatterAt = 1.5f, ZeroDur = 1.95f, ZeroR = 290f, ZeroCd = 14f, ZeroSlow = 0.5f;
        public const float ZeroMult = 0.67f, ZeroShatterMult = 1f;
        public const int ZeroChill = 3;
        public static readonly Rgba FlColor = new Rgba(0.56f, 0.89f, 1f);
        static readonly Sfx[] FlStepSounds = { Sfx.FrostCrescent, Sfx.FrostBackhand, Sfx.FrostWaltz, Sfx.FrostFan, Sfx.FrostSlamWind };  // FrostSoundBank.cs

        /// When each of a step's hits lands (s into the step): one for the sweeps, spin and slam, one per ray for the fan.
        public static float FlHitAt(int step, int hit) => step == 0 || step == 1 ? 0.1f : step == 2 ? 0.18f : step == 3 ? 0.1f + Math.Abs(hit - 2) * 0.03f : 0.25f;
        public static int FlHits(int step) => step == 3 ? FlRays.Length : 1;

        class FrostStrike
        {
            public int Owner, Step, Bonus;
            public V2 O;
            public float Aim, Age, Scale, Dmg, Knock;
            public bool Counted;
            public readonly bool[] Done = new bool[5];
            public readonly HashSet<int> Chilled = new HashSet<int>();  // one chill a strike, however many rays catch it
        }
        readonly List<FrostStrike> frostStrikes = new List<FrostStrike>();

        float FlScale(Player p) => AreaMult(p) * (1f + 0.1f * ExtraShots(p)) * p.Stats.IceReachMult;

        /// Host, every tick the Frost Lance is out (and you're up): the next step of the chain while Fire is held.
        void TickFrostLance(Player p, float dt)
        {
            p.FlIdle += dt;
            if (p.FlIdle > FlComboReset)
                p.FlStep = 0;
            if (p.FlZeroT > ZeroDur - ZeroCharge || !p.Firing || p.ParryActive || p.FireCd > 0f || State == MatchState.GameOver)
                return;
            int step = p.FlStep % 5, extra = ExtraShots(p);
            var s = new FrostStrike
            {
                Owner = p.Id, Step = step, O = p.Pos, Aim = p.Aim, Scale = FlScale(p),
                Dmg = p.Weapon.Damage * FlMult[step] * p.Stats.DmgMult * (1f + 0.2f * extra) * HomingCost(p),
                Knock = FlKnock[step],
            };
            if (p.FlRime)
            {
                p.FlRime = false;  // the Rime Counter: no wind-up, one more chill
                s.Age = FlWind[step];
                s.Bonus = 1;
            }
            frostStrikes.Add(s);
            // Pos the origin, Pos2 (aim, owner * 8 + step), Radius the size; alpha 0.5 marks a Rime Counter (the look skips the wind-up too).
            BroadcastFx(FxKind.FrostStep, s.O, new V2(s.Aim, p.Id * 8 + step), FlColor.WithAlpha(s.Bonus > 0 ? 0.5f : 1f), s.Scale, FlLife[step],
                FlStepSounds[step]);
            p.FireCd = FlGap[step] * (p.Weapon.FireInterval / FlBaseInterval) / FireRate(p);
            p.FlStep = (step + 1) % 5;
            p.FlIdle = 0f;
            p.Run.ShotsFired++;
        }

        /// Host, every tick: each strike's hits land at their moments; chill wears off; Absolute Zero's waves.
        void TickFrostStrikes(float dt)
        {
            for (int i = frostStrikes.Count - 1; i >= 0; i--)
            {
                var s = frostStrikes[i];
                s.Age += dt;
                Player owner;
                if (!Players.TryGetValue(s.Owner, out owner) || s.Age >= FlDur[s.Step])
                {
                    frostStrikes.RemoveAt(i);
                    continue;
                }
                for (int h = 0; h < FlHits(s.Step); h++)
                {
                    if (s.Done[h] || s.Age < FlHitAt(s.Step, h))
                        continue;
                    s.Done[h] = true;
                    if (s.Step == 4)
                        BroadcastFx(FxKind.FrostBurst, s.O + V2.FromAngle(s.Aim) * (FlSlamAt * s.Scale), FlColor, 4f, 0.9f, Sfx.FrostSlam);
                    LandFrostHit(owner, s, h);
                }
            }
            foreach (var e in Enemies.Values)
                if (e.FrostChill > 0 && (e.FrostChillT -= dt) <= 0f)
                    e.FrostChill = 0;
            foreach (var p in Players.Values)
                TickAbsoluteZero(p, dt);
        }

        /// Is this enemy (rel: from the strike's origin, aim-relative frame) inside the step's hit `h`?
        static bool FrostTouches(int step, int h, float scale, V2 rel, float r)
        {
            float d = rel.Length, a = (float)Math.Atan2(rel.Y, rel.X);
            switch (step)
            {
                case 0: return d <= FlArcR * scale + r && Math.Abs(a) <= FlArcHalf + r / Math.Max(d, 1f);
                case 1: return d <= FlBackR * scale + r && Math.Abs(a) <= FlBackHalf + r / Math.Max(d, 1f);
                case 2: return d <= FlSpinR * scale + r;
                case 3:
                {
                    float c = (float)Math.Cos(-FlRays[h]), sn = (float)Math.Sin(-FlRays[h]);
                    float x = rel.X * c - rel.Y * sn, y = rel.X * sn + rel.Y * c;
                    return x >= FlRayX0 * scale - r && x <= FlRayX1 * scale + r && Math.Abs(y) <= FlRayW * scale + r;
                }
                default: return new V2(rel.X - FlSlamAt * scale, rel.Y).Length <= FlSlamR * scale + r;
            }
        }

        void LandFrostHit(Player owner, FrostStrike s, int h)
        {
            float c = (float)Math.Cos(-s.Aim), sn = (float)Math.Sin(-s.Aim);
            bool any = false;
            V2 from = s.Step == 4 ? s.O + V2.FromAngle(s.Aim) * (FlSlamAt * s.Scale) : s.O;
            foreach (var e in AllFoes(by: owner.Id))
            {
                if (!Targetable(e))
                    continue;
                V2 w = e.Pos - s.O, rel = new V2(w.X * c - w.Y * sn, w.X * sn + w.Y * c);
                if (!FrostTouches(s.Step, h, s.Scale, rel, e.Radius))
                    continue;
                any = true;
                int chill = s.Chilled.Add(e.Id) ? (s.Step == 4 ? 2 : 1) + s.Bonus : 0;
                V2 push = e.Pos - from;
                push = push.Length > 1f ? push / push.Length : V2.FromAngle(s.Aim);
                FrostHit(owner, e, s.Dmg, chill, s.Step == 4, push * s.Knock);
            }
            if (any && !s.Counted)
            {
                s.Counted = true;
                owner.Run.ShotsHit++;
            }
        }

        /// One frost hit: shatter a frozen enemy (shatter hits), else hurt it, chill it, maybe freeze it; then the shove.
        void FrostHit(Player owner, Enemy e, float dmg, int chill, bool shatter, V2 shove)
        {
            bool frozen = IceFrozen(e);
            if (shatter && frozen)
            {
                dmg *= FlShatterMult;
                e.FrozenT = e.StunT;  // the ice breaks (a stun underneath still holds it)
                e.FrostChill = 0;
                BroadcastFx(FxKind.FrostBurst, e.Pos, FlColor, 1f, 0.9f, Sfx.FrostShatter);
                frozen = false;
            }
            WeaponHurt(e, dmg, owner.Id);
            if (e.Hp <= 0f)
                return;
            if (chill > 0 && !frozen)
                Chill(e, chill, FlChillHold, 1f - FlChillSlow, false, owner.Stats.IceFreezeBonus);  // Chill.cs
            if (!Heavy(e) && !IceFrozen(e))
                KnockEnemy(e, shove);
        }

        /// Parry: the step in your hands breaks (its hits still to come never land); your place in the chain stays.
        void ResetFrostChain(Player p)
        {
            bool live = false;
            foreach (var s in frostStrikes)
                if (s.Owner == p.Id && s.Age < FlDur[s.Step] * 0.75f)
                {
                    live = true;
                    s.Age = FlDur[s.Step];  // (removed next tick; nothing more lands)
                }
            if (live)
                BroadcastFx(FxKind.FrostBreak, p.Pos, new V2(p.Aim, p.Id), FlColor, 0f, 0.5f, Sfx.FrostBreak);
            p.FireCd = 0f;
        }

        /// A perfect parry with the Frost Lance out primes the Rime Counter.
        void PrimeRime(Player p)
        {
            if (p.Weapon.Id == WeaponId.FrostLance)
                p.FlRime = true;
        }

        // ---- Absolute Zero (Alt)

        public static float ZeroROf(Player p) => ZeroR * AreaMult(p);

        float zeroLocalCd;  // your own game: until the Alt can go again (the ring under your feet)
        public float ZeroLocalCd => zeroLocalCd;

        /// Your own game: Alt with the Frost Lance. True when it sent something.
        bool LocalZero(Player me, V2 aimAt, bool pressed, float dt)
        {
            zeroLocalCd = Math.Max(0f, zeroLocalCd - dt);
            if (!pressed || me.Weapon.Id != WeaponId.FrostLance || zeroLocalCd > 0f || me.FlZeroT > 0f)
                return false;
            zeroLocalCd = ZeroCd;
            me.FlZeroT = ZeroDur;  // (the charge's slow starts at once on your own screen; the host's snapshot agrees)
            if (IsHost)
                HostWeaponDash(MyId, WeaponAlt.Zero, aimAt);
            else
            {
                var w = Proto.Begin(Msg.WeaponDash);
                w.Write(WeaponAlt.Zero);
                w.Write(aimAt);
                session.SendToHost(Proto.End(w));
            }
            return true;
        }

        /// Host: Alt pressed with the Frost Lance: the charge starts, if the cooldown is done.
        void HostAbsoluteZero(Player p)
        {
            if (p.Weapon.Id != WeaponId.FrostLance || p.Downed || p.StunT > 0f || p.FlZeroCd > 0f || State == MatchState.GameOver)
                return;
            p.FlZeroCd = ZeroCd;
            p.FlZeroT = ZeroDur;
            p.FlZeroAt = p.Pos;
            p.FlZeroStage = 0;
            BroadcastFx(FxKind.AbsoluteZero, p.Pos, new V2(p.Id, p.Aim), FlColor, ZeroROf(p), ZeroDur, Sfx.ZeroCharge);
        }

        /// Host, every tick: the charge, the freezing wave, the shattering wave.
        void TickAbsoluteZero(Player p, float dt)
        {
            p.FlZeroCd = Math.Max(0f, p.FlZeroCd - dt);
            if (p.FlZeroT <= 0f)
                return;
            if (p.Downed || p.Weapon.Id != WeaponId.FrostLance)
            {
                p.FlZeroT = 0f;
                return;
            }
            p.FlZeroT = Math.Max(0f, p.FlZeroT - dt);
            float age = ZeroDur - p.FlZeroT;
            if (p.FlZeroStage == 0 && age >= ZeroWaveAt)
                ZeroWave(p, ZeroMult, ZeroChill, false, 2f, Sfx.ZeroWave);
            else if (p.FlZeroStage == 1 && age >= ZeroShatterAt)
                ZeroWave(p, ZeroShatterMult, 0, true, 3f, Sfx.ZeroShatter);
        }

        void ZeroWave(Player p, float mult, int chill, bool shatter, float mark, Sfx sound)
        {
            p.FlZeroStage++;
            float R = ZeroROf(p), dmg = p.Weapon.Damage * mult * p.Stats.DmgMult * (1f + 0.2f * ExtraShots(p));
            BroadcastFx(FxKind.FrostBurst, p.FlZeroAt, FlColor, mark, 0.6f, sound);
            bool any = false;
            var hit = new List<Enemy>();
            foreach (var e in AllFoes(by: p.Id))
                if (Targetable(e) && e.Pos.WithinReach(p.FlZeroAt, R + e.Radius))
                    hit.Add(e);
            foreach (var e in hit)
            {
                any = true;
                V2 rel = e.Pos - p.FlZeroAt;
                V2 push = rel.Length > 1f ? rel / rel.Length : V2.FromAngle(p.Aim);
                FrostHit(p, e, dmg, chill, shatter, push * (shatter ? 55f : 30f));
            }
            p.Run.ShotsFired++;
            if (any)
                p.Run.ShotsHit++;
        }

        /// Off when you put it away or go down: the chain starts over, the charge stops.
        void ClearFrostLance(Player p)
        {
            p.FlStep = 0;
            p.FlZeroT = 0f;
            p.FlRime = false;
        }

        // --- Network: the player's Frost Lance fields (SyncPlayer) ---

        void SyncFrostLance(NetSync s, Player p)
        {
            s.Float(ref p.FlZeroT);  // the Frost Lance's Absolute Zero and Rime Counter
            s.Float(ref p.FlZeroCd);
            s.Bool(ref p.FlRime);
        }
    }
}
