using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Tank Elementalist's Wind Clash (the tank weapon on the lobby's 4th key; mockup Docs/Mockups/wind-clash.html, Look A "Gale").
    ///   Fire: a four-step combo of air waves, each hurting and shoving every enemy it passes once:
    ///     1 Crescent: a 110° arc that sweeps down across your aim as it flies out from you (WcArcR0 to WcArcR1).
    ///     2 Back Crescent: the same, sweeping back up.
    ///     3 Gale Lance: a line of wind that shoots out along your aim (WcLanceLen long, WcLanceW wide), its tail catching up.
    ///     4 Clash Ring: a ring that bursts out round you (to WcRingR1).
    ///   Each step waits WcGap[step] (scaled by the fire rate) before the next; stop for WcComboReset and it starts over at 1.
    ///   Every wave is a WindWave the host moves along its course (TickWindWaves); everyone draws it from one FxKind.WindWave
    ///   (Pos the origin, Pos2.X the aim, Pos2.Y owner * 4 + step, Radius its size scale, Dur its life; Game/WindClashLook.cs),
    ///   whose shape comes from the same functions as the hits (WcArc, WcLance, WcRing), so what you see is what hits.
    ///   Alt (Msg.WeaponDash WeaponAlt.Tempest): Tempest. For TempestTime a storm of wind (TempestRadius) blows round you: it hurts every
    ///     enemy in it TempestDmg x the damage each TempestTick with a light shove out, and every enemy shot that comes into it is
    ///     caught (FxKind.WindCatch: it spins round you and bursts); you move at TempestSlow and can't attack. TempestCd cooldown.
    ///   Extra projectiles: +20% damage and 10% more size each. Homing Rounds make the waves and the storm bigger (AreaDesc).
    ///   Gale Force (Rare card): waves reach 20% farther and shove 30% harder.
    public partial class Game
    {
        public const float WcArcHalf = 0.96f, WcArcR0 = 22f, WcArcR1 = 170f, WcArcDur = 0.4f, WcArcTilt = 0.12f;
        public const float WcLanceLen = 330f, WcLanceW = 28f, WcLanceDur = 0.46f;
        public const float WcRingR0 = 18f, WcRingR1 = 100f, WcRingDur = 0.36f;
        public const float WcComboReset = 0.9f, WcBaseInterval = 0.34f, WcHitBand = 12f;
        public static readonly float[] WcGap = { 0.34f, 0.34f, 0.44f, 0.62f };      // at the base fire interval
        public static readonly float[] WcMult = { 1f, 1f, 1.6f, 1.2f };            // x the weapon's damage
        public static readonly float[] WcKnock = { 45f, 45f, 70f, 85f };           // px shoved
        public const float TempestTime = 3f, TempestCd = 9f, TempestRadius = 120f, TempestTick = 0.2f, TempestDmg = 0.5f, TempestSlow = 0.7f, TempestShove = 10f;
        public const float TempestSpinUp = 0.22f, WindCatchTime = 0.3f;
        public static readonly Rgba WcColor = new Rgba(0.56f, 0.89f, 0.95f);

        public static float WcEase(float x) { x = Math.Max(0f, Math.Min(1f, x)); return 1f - (1f - x) * (1f - x) * (1f - x); }

        public static float WcDur(int step) => step < 2 ? WcArcDur : step == 2 ? WcLanceDur : WcRingDur;

        /// A crescent `age` into its life (scale: its size): its radius, how far it has swept (0-1) and its strength (fades out).
        public static void WcArc(float age, float scale, out float r, out float sweep, out float alpha)
        {
            float p = MathX.Clamp01(age / WcArcDur);
            sweep = WcEase(p / 0.3f);
            r = WcArcR0 + (WcArcR1 * scale - WcArcR0) * WcEase(p);
            alpha = p < 0.6f ? 1f : 1f - (p - 0.6f) / 0.4f;
        }

        /// Where a crescent's sweep starts (its first edge) and which way it goes: step 0 sweeps one way, step 1 back.
        public static void WcArcSpan(float aim, int step, float sweep, out float start, out float span)
        {
            float side = step == 0 ? 1f : -1f, a = aim + side * WcArcTilt;
            start = a + side * WcArcHalf;
            span = -side * 2f * WcArcHalf * sweep;
        }

        /// The Gale Lance `age` into its life: its head and tail along the aim, and its strength.
        public static void WcLance(float age, float scale, out float head, out float tail, out float alpha)
        {
            float p = MathX.Clamp01(age / WcLanceDur), len = WcLanceLen * scale, q = MathX.Clamp01((p - 0.3f) / 0.7f);
            head = len * WcEase(p / 0.5f);
            tail = len * q * q * 0.92f;
            alpha = p < 0.7f ? 1f : 1f - (p - 0.7f) / 0.3f;
        }

        /// The Clash Ring `age` into its life: its radius and strength.
        public static void WcRing(float age, float scale, out float r, out float alpha)
        {
            float p = MathX.Clamp01(age / WcRingDur);
            r = WcRingR0 + (WcRingR1 * scale - WcRingR0) * WcEase(p);
            alpha = p < 0.5f ? 1f : 1f - (p - 0.5f) / 0.5f;
        }

        class WindWave
        {
            public int Owner, Step;
            public V2 O;
            public float Aim, Age, Scale, Dmg, Knock;
            public bool Counted;
            public readonly HashSet<int> Hit = new HashSet<int>();
        }
        readonly List<WindWave> windWaves = new List<WindWave>();

        /// The size of this player's waves: Homing Rounds' area, extra projectiles and Gale Force.
        float WcScale(Player p) => AreaMult(p) * (1f + 0.1f * ExtraShots(p));

        /// Host, every tick Wind Clash is out (and you're up): the next step of the combo while Fire is held.
        void TickWindClash(Player p, float dt)
        {
            p.WcIdle += dt;
            if (p.WcIdle > WcComboReset)
                p.WcStep = 0;
            if (p.WcTempestT > 0f || !p.Firing || p.ParryActive || p.FireCd > 0f || State == MatchState.GameOver)
                return;
            int step = p.WcStep % 4;
            int extra = ExtraShots(p);
            var w = new WindWave
            {
                Owner = p.Id, Step = step, O = p.Pos, Aim = p.Aim,
                Scale = WcScale(p) * (step < 3 ? p.Stats.WcReachMult : 1f),
                Dmg = p.Weapon.Damage * WcMult[step] * p.Stats.DmgMult * (1f + 0.2f * extra),
                Knock = WcKnock[step] * p.Stats.WcShoveMult,
            };
            windWaves.Add(w);
            BroadcastFx(FxKind.WindWave, w.O, new V2(w.Aim, p.Id * 4 + step), WcColor, w.Scale, WcDur(step), step < 2 ? Sfx.SwingLight : Sfx.SwingHeavy);
            p.FireCd = WcGap[step] * (p.Weapon.FireInterval / WcBaseInterval) / FireRate(p);
            p.WcStep = (step + 1) % 4;
            p.WcIdle = 0f;
            p.Run.ShotsFired++;
        }

        /// Host, every tick: each wave moves along its course and hits what its front passes, once each; then the Tempests blow.
        void TickWindWaves(float dt)
        {
            for (int i = windWaves.Count - 1; i >= 0; i--)
            {
                var w = windWaves[i];
                w.Age += dt;
                if (w.Age >= WcDur(w.Step) || !Players.ContainsKey(w.Owner))
                {
                    windWaves.RemoveAt(i);
                    continue;
                }
                bool any = false;
                foreach (var e in AllFoes(by: w.Owner))
                {
                    if (w.Hit.Contains(e.Id))
                        continue;
                    V2 rel = e.Pos - w.O;
                    float d = rel.Length;
                    V2 push;
                    if (!WaveTouches(w, e, rel, d, out push))
                        continue;
                    w.Hit.Add(e.Id);
                    any = true;
                    WeaponHurt(e, w.Dmg, w.Owner);
                    WindHit(e, push);  // Wind.cs
                    if (e.Hp > 0f && !Heavy(e))
                        KnockEnemy(e, push * w.Knock);
                }
                Player owner;
                if (any && !w.Counted && Players.TryGetValue(w.Owner, out owner))
                {
                    w.Counted = true;  // (a wave counts as one hit, however many it lands on)
                    owner.Run.ShotsHit++;
                }
            }
            foreach (var p in Players.Values)
                TickTempest(p, dt);
        }

        /// Whether this wave's front is on the enemy now, and which way it shoves it.
        static bool WaveTouches(WindWave w, Enemy e, V2 rel, float d, out V2 push)
        {
            V2 outward = d > 1f ? rel / d : V2.FromAngle(w.Aim);
            push = outward;
            if (w.Step < 2)
            {
                float r, sweep, alpha, start, span;
                WcArc(w.Age, w.Scale, out r, out sweep, out alpha);
                if (alpha < 0.35f || Math.Abs(d - r) > WcHitBand + e.Radius)
                    return false;
                WcArcSpan(w.Aim, w.Step, sweep, out start, out span);
                float off = MathX.WrapPi(rel.Angle - start);
                float along = span < 0f ? -off : off, slack = d > 1f ? e.Radius / d : 0f;
                return along >= -slack && along <= Math.Abs(span) + slack;
            }
            if (w.Step == 2)
            {
                float head, tail, alpha;
                WcLance(w.Age, w.Scale, out head, out tail, out alpha);
                V2 dir = V2.FromAngle(w.Aim);
                float al = rel.X * dir.X + rel.Y * dir.Y, across = Math.Abs(rel.X * dir.Y - rel.Y * dir.X);
                push = dir;
                return alpha > 0.3f && al > tail - 4f && al < head + 10f && across < WcLanceW * w.Scale / 2f + e.Radius;
            }
            float rr, ra;
            WcRing(w.Age, w.Scale, out rr, out ra);
            return ra > 0.3f && d - e.Radius < rr + WcHitBand;
        }

        // ---- Tempest (Alt)

        public static float TempestRadiusOf(Player p) => TempestRadius * AreaMult(p);

        float tempestLocalCd;  // your own game: until the Alt can go again (the cooldown ring under your feet)
        public float TempestLocalCd => tempestLocalCd;
        readonly List<int> tempestShots = new List<int>();

        /// Your own game: Alt with Wind Clash. True when it sent something (LocalWeaponDash stops there).
        bool LocalTempest(Player me, V2 aimAt, bool pressed, float dt)
        {
            tempestLocalCd = Math.Max(0f, tempestLocalCd - dt);
            if (!pressed || me.Weapon.Id != WeaponId.WindClash || tempestLocalCd > 0f || me.WcTempestT > 0f)
                return false;
            tempestLocalCd = TempestCd;
            me.WcTempestT = TempestTime;  // (the slow starts at once on your own screen; the host's snapshot agrees)
            if (IsHost)
                HostWeaponDash(MyId, WeaponAlt.Tempest, aimAt);
            else
            {
                var w = Proto.Begin(Msg.WeaponDash);
                w.Write(WeaponAlt.Tempest);
                w.Write(aimAt);
                session.SendToHost(Proto.End(w));
            }
            return true;
        }

        /// Host: Alt pressed with Wind Clash: the storm starts, if its cooldown is done.
        void HostTempest(Player p)
        {
            if (p.Weapon.Id != WeaponId.WindClash || p.Downed || p.StunT > 0f || p.WcTempestCd > 0f || State == MatchState.GameOver)
                return;
            p.WcTempestCd = TempestCd;
            p.WcTempestT = TempestTime;
            p.WcTempestTick = TempestSpinUp;
            BroadcastFx(FxKind.Ring, p.Pos, WcColor, TempestRadiusOf(p), 0.3f, Sfx.BossWhoosh);
        }

        /// Host, every tick: the storm round each player who has one up.
        void TickTempest(Player p, float dt)
        {
            p.WcTempestCd = Math.Max(0f, p.WcTempestCd - dt);
            if (p.WcTempestT <= 0f)
                return;
            if (p.Downed || p.Weapon.Id != WeaponId.WindClash)
            {
                p.WcTempestT = 0f;
                return;
            }
            float age = TempestTime - p.WcTempestT, R = TempestRadiusOf(p) * WcEase(age / TempestSpinUp);
            p.WcTempestTick -= dt;
            if (p.WcTempestTick <= 0f)
            {
                p.WcTempestTick += TempestTick;
                float dmg = p.Weapon.Damage * TempestDmg * p.Stats.DmgMult * (1f + 0.2f * ExtraShots(p));
                bool any = false;
                foreach (var e in FoesInCircle(p.Pos, R, by: p.Id))
                {
                    any = true;
                    WeaponHurt(e, dmg, p.Id);
                    V2 rel = e.Pos - p.Pos;
                    WindHit(e, rel);  // Wind.cs
                    if (e.Hp > 0f && !Heavy(e))
                        KnockEnemy(e, (rel.Length > 1f ? rel / rel.Length : RandDir()) * TempestShove);
                }
                p.Run.ShotsFired++;
                if (any)
                    p.Run.ShotsHit++;
            }
            // Enemy shots that come into it are caught: they spin round you and burst (the look draws the spin from the fx).
            if (age > 0.1f)
            {
                tempestShots.Clear();
                foreach (var b in Bullets.Values)
                    if (Defs.IsEnemyShot(b.Kind) && !Defs.Unblockable(b.Kind) && b.Pos.WithinReach(p.Pos, R))
                        tempestShots.Add(b.Id);
                foreach (int id in tempestShots)
                {
                    BroadcastFx(FxKind.WindCatch, Bullets[id].Pos, new V2(p.Id, 0f), WcColor, 0f, WindCatchTime, Sfx.None);
                    KillBullet(id);
                }
            }
            p.WcTempestT = Math.Max(0f, p.WcTempestT - dt);
        }

        /// Off when you put it away or go down: the combo starts over and the storm stops.
        void ClearWindClash(Player p)
        {
            p.WcStep = 0;
            p.WcTempestT = 0f;
        }
    }
}
