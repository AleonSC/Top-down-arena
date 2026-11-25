using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Healer Ninja's fourth weapon (the lobby's 4th key), the Twin Tessen: an iron war fan in each hand
    /// (mockup: the "Twin Tessen" artifact).
    ///   Fire: the Crane Dance, a five-step combo (step in Player.Combo's low bits, as the Nunchaku's; TtWindow after a step
    ///     ends without the next and it starts over). Each step is one Swing (Swing.Tessen = the step) landing TtHitAt into it:
    ///     1 Left Flick, 2 Right Flick: 110° sweeps that heal the allies they brush (TtBrushHeal).
    ///     3 Scissor Cut: both fans cross in front, a narrow arc that cuts small bullets.
    ///     4 Petal Spin: a full twirl that hits twice and sheds TtPetals petals (+2 an extra projectile) that fly to the most
    ///       hurt player near you (you too) and heal TtPetalHeal each as they land (FxKind.TessenPetals; host: tessenPetals).
    ///     5 Crane Bloom: both fans fly open, a 60° petal wave TtBloomReach long that shoves, bats enemy shots back as yours, and
    ///       heals the allies in it TtBloomHeal with a Tailwind (Player.TtTailT: TtTailSpeed for TtBloomTail s, you too).
    ///   Each enemy hit fills Breeze TtBreezeHit, each heal TtBreezeHeal (Player.TtBreeze, 0-100, replicated).
    ///   Alt, held (Msg.WeaponDash DashTessenOn / DashTessenOff): Kamikaze Gust. Holding pulls both fans back (Player.TtCharging,
    ///     TtCharge: you move at TtChargeSlow and can't attack); letting go sweeps them forward in one gust: a TtGustHalf cone
    ///     TtGustReach long that hurts, throws enemies back up to TtGustKnock (not bosses or heavies), bats every enemy shot in it
    ///     back as yours and heals the allies in it TtGustHeal with a Tailwind of TtGustTail s. A longer hold (TtChargeMin to
    ///     TtChargeFull) throws harder. TtGustCd cooldown from the release (Player.TtGustCd, replicated).
    ///     With a full Breeze bar the gust is a Typhoon (uses it up): TtTyphoonHalf wide, stuns what it hits TtTyphoonStun s,
    ///     heals TtTyphoonHeal.
    ///   Divine Wind (Rare card): the gust reaches 25% farther and heals 50% more.
    /// The look is Game/TwinTessenLook.cs; the fans' poses come from TtPose here.
    public partial class Game
    {
        const byte DashTessenOn = 64, DashTessenOff = 65;
        public const int TtSteps = 5;
        public const float TtWindow = 0.7f, TtHitAt = 0.45f, TtSpinEcho = 0.8f;
        public static readonly float[] TtDur = { 0.2f, 0.2f, 0.24f, 0.36f, 0.4f };
        public static readonly float[] TtMult = { 1f, 1f, 1.6f, 0.8f, 2.8f };       // x the weapon's damage (10)
        public static readonly float[] TtReach = { 105f, 105f, 125f, 115f, 240f };
        public static readonly float[] TtArc = { 110f, 110f, 70f, 360f, 60f };     // degrees, the whole arc
        public static readonly float[] TtKnock = { 0f, 0f, 0f, 0f, 80f };          // 0: the standard melee shove
        public const float TtBrushHeal = 4f, TtBloomHeal = 18f, TtBloomTail = 2f, TtPetalHeal = 5f, TtPetalRange = 420f, TtPetalFlight = 0.5f;
        public const int TtPetals = 6;
        public const float TtExtraDmg = 0.15f;  // each extra projectile: +15% to every step
        public const float TtTailSpeed = 1.2f, TtChargeSlow = 0.6f;
        public const float TtBreezeHit = 6f, TtBreezeHeal = 3f, TtBreezeMax = 100f;
        public const float TtGustHalf = 60f * MathX.Pi / 180f, TtTyphoonHalf = 75f * MathX.Pi / 180f, TtGustReach = 460f;
        public const float TtGustMult = 1.2f, TtGustKnock = 260f, TtGustHeal = 20f, TtTyphoonHeal = 35f, TtGustTail = 3f, TtTyphoonStun = 1f;
        public const float TtChargeMin = 0.4f, TtChargeFull = 1.2f, TtGustCd = 9f, TtGustFx = 0.5f;
        public static readonly Rgba TtPaper = new Rgba(0.95f, 0.9f, 0.81f), TtPetal = new Rgba(1f, 0.76f, 0.82f),
            TtWind = new Rgba(0.35f, 0.85f, 0.91f), TtGold = new Rgba(1f, 0.8f, 0.35f), TtSun = new Rgba(0.85f, 0.26f, 0.31f);
        static readonly Sfx[] TtSounds = { Sfx.SwingLight, Sfx.SwingLight, Sfx.SwingHeavy, Sfx.SwingHeavy, Sfx.SwingHeavy };

        public static float TtEase(float x) { x = Math.Max(0f, Math.Min(1f, x)); return 1f - (1f - x) * (1f - x) * (1f - x); }

        /// Each fan's pose `k` (0-1) into combo step `step` (1-5; 0: at rest), in the hand's frame (+x along the aim, +y your
        /// right): its angle off the aim (radians), how far out the hand is (px) and how open the fan is (0 shut, 1 full, more:
        /// flung wide). The look draws from it; the hits land TtHitAt into each step.
        public static void TtPose(int step, float k, out float aL, out float rL, out float oL, out float aR, out float rR, out float oR)
        {
            const float D = MathX.Pi / 180f;
            float e = TtEase(k);
            aL = -55f * D; rL = 14f; oL = 0.2f;
            aR = 55f * D; rR = 14f; oR = 0.2f;
            switch (step)
            {
                case 1: aL = (-100f + 145f * e) * D; rL = 19f; oL = 1f; break;
                case 2: aR = (100f - 145f * e) * D; rR = 19f; oR = 1f; break;
                case 3: aL = (-80f + 108f * e) * D; aR = (80f - 108f * e) * D; rL = rR = 20f; oL = oR = 1f; break;
                case 4: aL = (-90f + 720f * e) * D; aR = (90f + 720f * e) * D; rL = rR = 19f; oL = oR = 1f; break;
                case 5:
                    aL = (-50f + 36f * e) * D; aR = (50f - 36f * e) * D;
                    rL = rR = 12f + 12f * e; oL = oR = 0.3f + 0.95f * e;
                    break;
            }
        }

        /// The fans pulled back for the gust, `c` (0-1) of the way through the charge.
        public static void TtChargePose(float c, out float aL, out float aR, out float r)
        {
            const float D = MathX.Pi / 180f;
            float e = TtEase(c);
            aL = (-60f - 95f * e) * D;
            aR = (60f + 95f * e) * D;
            r = 14f + 3f * e;
        }

        /// The step a Combo value holds (0: the next press is the Left Flick).
        public static int TtStepOf(int combo) => combo & 7;

        /// How wide and far this player's gust goes, and how much it heals (Homing Rounds' area; extra projectiles: 10% wider;
        /// Divine Wind: farther, more healing).
        public static float TtGustReachOf(Player p) => TtGustReach * AreaMult(p) * p.Stats.TtGustReachMult;
        public float TtGustHalfOf(Player p, bool typhoon) => Math.Min(MathX.Pi * 0.95f, (typhoon ? TtTyphoonHalf : TtGustHalf) * (1f + 0.1f * ExtraShots(p)));

        /// Host: one press of the primary with the Twin Tessen (from FireNinjaWeapon): the next step of the Crane Dance.
        void FireTessen(Player p, float aim, float dmg, int extra)
        {
            if (p.TtCharging)
                return;
            int last = TtStepOf(p.Combo);
            int step = last >= 1 && last < TtSteps && p.ComboT > 0f ? last + 1 : 1;
            int count = ((p.Combo >> 3) + 1) & 31;
            p.Combo = (count << 3) | step;
            float dur = TtDur[step - 1];
            p.ComboT = dur + TtWindow;
            p.FireCd = dur / FireRate(p);
            float hit = p.Weapon.Damage * dmg * TtMult[step - 1] * (1f + TtExtraDmg * extra);
            var s = TessenSwing(p, aim, step, hit, dur * TtHitAt);
            if (step == 4)
                TessenSwing(p, aim, 6, hit, dur * TtSpinEcho);  // the twirl's second turn
            BroadcastFx(FxKind.TessenStep, p.Pos, p.Pos + V2.FromAngle(aim) * s.Reach, TtPaper, p.Id * 10 + step, dur + 0.25f, TtSounds[step - 1]);
        }

        /// One step's hit (`kind` 1-5, or 6 the spin's second turn), landing `delay` after the press.
        Swing TessenSwing(Player p, float aim, int kind, float dmg, float delay)
        {
            int i = (kind == 6 ? 4 : kind) - 1;  // (6, the spin's second turn, is the spin's shape)
            var s = NewSwing(p, aim, TtReach[i], TtArc[i], dmg);
            s.Delay = delay;
            s.NoFx = true;  // the step's own effect draws it (FxKind.TessenStep)
            s.Sound = Sfx.None;
            s.Twin = true;  // (the parry takes back one still winding up: ResetTessenChain)
            s.Tessen = (byte)kind;
            s.Knock = TtKnock[i];
            s.NoCut = kind <= 2;
            s.Deflect = kind == 5;  // the Crane Bloom bats enemy shots back as yours
            s.Heavy = kind == 5;
            return QueueSwingAnd(s);
        }

        Swing QueueSwingAnd(Swing s)
        {
            QueueSwing(s);
            return s;
        }

        /// From the swing's landing (Knight.cs): Breeze for what it hit, and the step's healing, Tailwind and petals.
        void TessenLanded(Player p, Swing s, V2 origin, int hits)
        {
            TtAddBreeze(p, TtBreezeHit * hits);
            int step = s.Tessen;
            if (step == 6)
                return;
            float heal = step == 5 ? TtBloomHeal : step <= 3 ? TtBrushHeal : 0f;
            if (heal > 0f)
                foreach (var q in Players.Values)
                {
                    if (q.Id == p.Id || q.Downed || !TtInCone(origin, s.Aim, s.HalfArc, s.Reach, q))
                        continue;
                    TtHeal(q, heal, p);
                    if (step == 5)
                        q.TtTailT = Math.Max(q.TtTailT, TtBloomTail);
                }
            if (step == 5)
                p.TtTailT = Math.Max(p.TtTailT, TtBloomTail);
            if (step == 4)
                TtShedPetals(p, TtPetals + 2 * ExtraShots(p));
        }

        /// Whether a player stands in this cone (its body counts).
        static bool TtInCone(V2 origin, float aim, float half, float reach, Player q)
        {
            float d = q.Pos.DistanceTo(origin);
            if (d - q.Def.BodyR > reach)
                return false;
            return half >= MathX.Pi - 0.01f || d <= q.Def.BodyR || AngleTo(origin, q.Pos, aim) <= half + (float)Math.Asin(Math.Min(1f, q.Def.BodyR / Math.Max(d, 1f)));
        }

        void TtHeal(Player q, float amount, Player from)
        {
            if (Heal(q, amount, from) > 0f)
                TtAddBreeze(from, TtBreezeHeal);
        }

        static void TtAddBreeze(Player p, float v)
        {
            if (v > 0f)
                p.TtBreezeF = Math.Min(TtBreezeMax, p.TtBreezeF + v);
            p.TtBreeze = (byte)Math.Round(p.TtBreezeF);
        }

        class TtPetalRun { public int Owner, Target, Count; public float T; }
        readonly List<TtPetalRun> tessenPetals = new List<TtPetalRun>();

        /// The Petal Spin's petals fly to the most hurt player near you (you too, alone); none hurt, they scatter and fade.
        void TtShedPetals(Player p, int n)
        {
            Player best = null;
            float worst = 1f;
            foreach (var q in Players.Values)
            {
                if (q.Downed || q.MaxHp <= 0f || q.Pos.FartherThan(p.Pos, TtPetalRange))
                    continue;
                float f = q.Hp / q.MaxHp;
                if (f < worst - 0.001f || f < worst + 0.001f && best != null && best.Id == p.Id && q.Id != p.Id)
                {
                    worst = f;
                    best = q;
                }
            }
            int target = best != null ? best.Id : -1;
            if (best != null)
                tessenPetals.Add(new TtPetalRun { Owner = p.Id, Target = target, Count = n, T = TtPetalFlight });
            BroadcastFx(FxKind.TessenPetals, p.Pos, new V2(target, n), TtPetal, p.Id, TtPetalFlight + 0.15f, Sfx.None);
        }

        /// Host, every tick for every player: the gust's charge and cooldown, Tailwind and the petals landing.
        void TickTessen(Player p, float dt)
        {
            p.TtGustCd = Math.Max(0f, p.TtGustCd - dt);
            p.TtTailT = Math.Max(0f, p.TtTailT - dt);
            if (p.Weapon.Id != WeaponId.TwinTessen || p.Downed)
            {
                if (p.TtCharging)
                    ClearTessen(p);
                return;
            }
            if (p.TtCharging)
            {
                if (p.StunT > 0f || State == MatchState.GameOver)
                {
                    p.TtCharging = false;
                    p.TtCharge = 0f;
                    return;
                }
                p.TtCharge = Math.Min(TtChargeFull + 0.05f, p.TtCharge + dt);
                p.FireCd = Math.Max(p.FireCd, 0.05f);  // no attacks while the fans are pulled back
            }
        }

        /// Host, every tick: petals in flight land on whoever they were flying to.
        void TickTessenPetals(float dt)
        {
            for (int i = tessenPetals.Count - 1; i >= 0; i--)
            {
                var f = tessenPetals[i];
                f.T -= dt;
                if (f.T > 0f)
                    continue;
                tessenPetals.RemoveAt(i);
                Player from, to;
                if (!Players.TryGetValue(f.Owner, out from) || !Players.TryGetValue(f.Target, out to) || to.Downed)
                    continue;
                TtHeal(to, TtPetalHeal * f.Count * from.Stats.TtHealMult, from);
            }
        }

        /// Off when you put it away or go down: the dance starts over and the charge lets go without a gust.
        void ClearTessen(Player p)
        {
            p.TtCharging = false;
            p.TtCharge = 0f;
        }

        /// Raising the parry cancels the dance (ResetAttackChains): back to the Left Flick, a step still winding up never lands.
        void ResetTessenChain(Player p)
        {
            p.Combo &= ~7;
            p.ComboT = 0f;
            p.FireCd = 0f;
            pendingSwings.RemoveAll(s => s.Owner == p.Id && s.Tessen > 0 && s.Delay > 0f);
        }

        // ---- Kamikaze Gust (Alt, held)

        /// Your own game: Alt with the Twin Tessen is held, so both edges go to the host. True when the key is theirs.
        bool LocalTessenGust(Player me, bool pressed, bool released, V2 aimAt)
        {
            if (me.Weapon.Id != WeaponId.TwinTessen)
                return false;
            if (!pressed && !released)
                return true;
            byte kind = pressed ? DashTessenOn : DashTessenOff;
            if (IsHost)
                HostWeaponDash(MyId, kind, aimAt);
            else
            {
                var w = Proto.Begin(Msg.WeaponDash);
                w.Write(kind);
                w.Write(aimAt);
                session.SendToHost(Proto.End(w));
            }
            return true;
        }

        /// From HostWeaponDash: the Twin Tessen's kinds.
        bool HostTessenDash(Player p, int kind)
        {
            if (kind != DashTessenOn && kind != DashTessenOff)
                return false;
            if (kind == DashTessenOn)
            {
                if (p.Weapon.Id == WeaponId.TwinTessen && !p.Downed && p.StunT <= 0f && p.SwallowT <= 0f && p.TtGustCd <= 0f && !p.TtCharging && State != MatchState.GameOver)
                {
                    p.TtCharging = true;
                    p.TtCharge = 0f;
                    p.Combo &= ~7;
                    BroadcastSound(Sfx.SwingLight);
                }
            }
            else if (p.TtCharging)
                ReleaseTessenGust(p);
            return true;
        }

        readonly List<Enemy> tessenGusted = new List<Enemy>();

        /// Host: the fans swept forward: the gust (a Typhoon with a full Breeze bar).
        void ReleaseTessenGust(Player p)
        {
            float held = p.TtCharge;
            p.TtCharging = false;
            p.TtCharge = 0f;
            if (p.Weapon.Id != WeaponId.TwinTessen || p.Downed || State == MatchState.GameOver)
                return;
            float power = 0.5f + 0.5f * MathX.Clamp01((held - TtChargeMin) / (TtChargeFull - TtChargeMin));
            bool typhoon = p.TtBreezeF >= TtBreezeMax - 0.01f;
            if (typhoon)
            {
                p.TtBreezeF = 0f;
                p.TtBreeze = 0;
            }
            float aim = p.Aim, half = TtGustHalfOf(p, typhoon), reach = TtGustReachOf(p);
            p.TtGustCd = TtGustCd;
            p.FireCd = Math.Max(p.FireCd, 0.25f);
            float dmg = p.Weapon.Damage * TtGustMult * p.Stats.DmgMult;
            tessenGusted.Clear();
            foreach (var e in FoesInCone(p.Pos, aim, half, reach, by: p.Id))
                tessenGusted.Add(e);
            foreach (var e in tessenGusted)
            {
                V2 rel = e.Pos - p.Pos;
                float d = rel.Length;
                V2 dir = d > 1f ? rel / d : V2.FromAngle(aim);
                WeaponHurt(e, dmg, p.Id);
                WindHit(e, dir);  // Wind.cs
                if (e.Hp <= 0f)
                    continue;
                if (!Heavy(e))
                    KnockEnemy(e, dir * (TtGustKnock * power * (1f - 0.5f * Math.Min(1f, d / reach))));
                if (typhoon && !e.Def.IsBoss)
                    Stun(e, TtTyphoonStun);
            }
            p.Run.ShotsFired++;
            if (tessenGusted.Count > 0)
            {
                p.Run.ShotsHit++;
                TtAddBreeze(p, typhoon ? 0f : TtBreezeHit * 0.5f * tessenGusted.Count);
            }
            DeflectBullets(p, p.Pos, aim, reach, half);  // every enemy shot in it turns round (Knight.cs)
            float heal = (typhoon ? TtTyphoonHeal : TtGustHeal) * p.Stats.TtHealMult;
            foreach (var q in Players.Values)
            {
                if (q.Id == p.Id || q.Downed || !TtInCone(p.Pos, aim, half, reach, q))
                    continue;
                TtHeal(q, heal, p);
                q.TtTailT = Math.Max(q.TtTailT, TtGustTail);
            }
            p.TtTailT = Math.Max(p.TtTailT, TtGustTail);
            BroadcastFx(FxKind.TessenGust, p.Pos, new V2(aim, half), typhoon ? TtGold : TtWind, reach, TtGustFx, Sfx.BossWhoosh);
        }
    }
}
