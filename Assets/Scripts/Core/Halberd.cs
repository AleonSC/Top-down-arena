using System;

namespace Arena
{
    /// The Tank Knight's Halberd (mockup Docs/Mockups/halberd-rework.html: Combo A and Alt 1).
    ///   Fire: Reaper's Wheel, four steps sharing Player.Combo (step in the low 3 bits, a count above them, as the Bulwark Barrier):
    ///     1 Sweep (an 18° blade swept across 80°, HalberdSteps steps, each enemy hit once), 2 Return Sweep (the same back, HbMult[1]),
    ///     3 Overhead Chop (a box HbChopHalfW either side of the aim out to HalberdMotion.ChopK of the reach; stuns HbChopStun),
    ///     4 Reaping Wheel (a full turn round you at HalberdMotion.WheelK of the reach, HbWheelSteps steps, shoves HbWheelKnock).
    ///     Every step cuts small shots in its arc (the usual melee cut). The next press counts once HbNext of a step has played;
    ///     HbChainWindow after a step without one, the chain starts over (TwinBlades.cs runs Combo's clock). Raising the parry
    ///     starts it over and takes back a step still winding up (ResetHalberdChain).
    ///     Each step's hits are timed from its motion (HalberdMotion.StepAt / StepCross), so the blade is where the hit says it is.
    ///   Alt (hold the weapon-action key; Msg.WeaponDash WeaponAlt.PikeOn / WeaponAlt.PikeOff): Set the Pike. You move at PikeSlow and can't
    ///     swing; the pike turns on its own toward the nearest enemy within PikeScan (Player.HbPikeA, PikeTurn rad/s, your aim
    ///     doesn't matter) and pokes it whenever it is lined up and within the point's reach (PikeReachK of the weapon's): a thin
    ///     box out to the point, PikeMult of the weapon's damage, a PikeKnock shove straight away from you and a PikeStun stun
    ///     (heavies and bosses aren't moved; bosses aren't stunned), at most once every PikeGap (faster with fire rate).
    ///     Let go of the key: Pike Lunge. The pike drives out along HbPikeA (where it points, not your aim) to
    ///     HalberdMotion.LungeK of the reach: a thin box, LungeMult of the weapon's damage, a LungeKnock shove and a LungeStun
    ///     stun; at most once every LungeCd (a tap of the key lunges at the nearest enemy).
    ///   Extra projectiles: +30 px reach (the weapon's Grow) and +10% damage on every step and poke. Long Haft (Rare card):
    ///     15% more reach on everything and a wider sweep on steps 1 and 2.
    ///   The look (Game/HalberdLook.cs) draws each step from its FxKind.HalberdStep, each poke from FxKind.HalberdPoke and the
    ///   set pike from the replicated HbSet and HbPikeA.
    public partial class Game
    {
        public const float HbChainWindow = 0.9f;
        public static readonly float[] HbNext = { 0.36f, 0.34f, 0.42f, 0.6f };      // s of each step before the next press counts (at the base fire rate)
        public static readonly float[] HbMult = { 1f, 0.8f, 44f / 30f, 28f / 30f };  // each step against the weapon's Damage (30, 24, 44, 28)
        public const float HbChopHalfW = 20f, HbChopStun = 0.6f;
        public const int HbWheelSteps = 8;
        public const float HbWheelKnock = 60f;

        public const float PikeSlow = 0.35f, PikeScan = 290f, PikeReachK = 1.15f, PikeTurn = 8f, PikeAlign = 0.16f;
        public const float PikeGap = 0.45f, PikeMult = 0.75f, PikeKnock = 110f, PikeStun = 0.3f, PikeHalfW = 16f, PikeJab = 0.07f, PikeFxTime = 0.32f;
        public static readonly Rgba HbGlow = new Rgba(0.56f, 0.94f, 0.78f), HbSpark = new Rgba(1f, 0.67f, 0.36f);
        public const float LungeMult = 40f / 30f, LungeKnock = 80f, LungeStun = 0.4f, LungeHalfW = 15f, LungeCd = 1.2f;

        /// How far the pike's point reaches for `p` (Long Haft and extra projectiles lengthen it).
        public static float PikeReach(Player p, int extra) => MeleeReach(p, (p.Weapon.Reach + p.Weapon.Grow * extra) * p.Stats.HalberdReachMult) * PikeReachK;

        /// Host: one press of Fire with the Halberd: the next step of Reaper's Wheel.
        void FireHalberd(Player p, float aim, float dmg, int extra)
        {
            var w = p.Weapon;
            int last = TwinBladesMotion.StepOf(p.Combo);
            int step = last >= 1 && last < 4 && p.ComboT > 0f ? last + 1 : 1;
            int count = ((p.Combo >> 3) + 1) & 31;
            p.Combo = (count << 3) | step;
            p.ComboT = HalberdMotion.StepTotal(step) + HbChainWindow;
            p.FireCd = HbNext[step - 1] / FireRate(p);
            float reach = (w.Reach + w.Grow * extra) * p.Stats.HalberdReachMult;  // Long Haft: farther, and wider below
            float hit = w.Damage * dmg * HbMult[step - 1] * (1f + 0.1f * extra);
            int group = ++sweepCount;
            if (step <= 2)
            {
                // A long, thin cone swept across in front of you; each enemy is hit once a sweep. Each step lands when the edge reaches it.
                float sweep = HalberdSweep + p.Stats.HalberdSweepBonus;
                for (int i = 0; i < HalberdSteps; i++)
                {
                    float f = (float)i / (HalberdSteps - 1) * 2f - 1f;
                    var h = NewSwing(p, aim + (step == 1 ? f : -f) * sweep, reach, HalberdConeDeg, hit);
                    h.Sweep = group;
                    h.Delay = HalberdMotion.StepCross(step, (step == 1 ? f : -f) * HalberdMotion.Sweep);
                    h.Silent = i > 0;  // one whoosh a sweep, as the first step lands
                    h.NoFx = true;     // the step's effect draws it all
                    h.Halberd = (byte)step;
                    QueueSwing(h);
                }
            }
            else if (step == 3)
            {
                // The Overhead Chop: the head comes down along the aim, a long box, and stuns what it lands on.
                var h = NewSwing(p, aim, reach * HalberdMotion.ChopK, 0f, hit);
                h.HalfWidth = HbChopHalfW;
                h.Thrust = true;
                h.Heavy = true;
                h.Stun = HbChopStun;
                h.StarStun = true;
                h.Delay = HalberdMotion.ChopHit;
                h.NoFx = true;
                h.Halberd = 3;
                h.Sound = Sfx.KnuckleSlam;
                QueueSwing(h);
            }
            else
            {
                // The Reaping Wheel: a full turn round you, cut into HbWheelSteps arcs timed to the blade going round.
                float arc = 360f / HbWheelSteps + 8f;
                for (int i = 0; i < HbWheelSteps; i++)
                {
                    float rel = HalberdMotion.WindA + (i + 0.5f) * MathX.Tau / HbWheelSteps;
                    var h = NewSwing(p, aim + rel, reach * HalberdMotion.WheelK, arc, hit);
                    h.Sweep = group;
                    h.Delay = HalberdMotion.StepCross(4, rel);
                    h.Knock = HbWheelKnock;
                    h.Silent = i > 0;
                    h.NoFx = true;
                    h.Halberd = 4;
                    QueueSwing(h);
                }
            }
            // The whole step, drawn from its age: Radius carries who and which step, Pos2 the reach along the aim.
            BroadcastFx(FxKind.HalberdStep, p.Pos, p.Pos + V2.FromAngle(aim) * MeleeReach(p, reach), HbGlow, p.Id * 10 + step,
                HalberdMotion.StepTotal(step), step == 4 ? Sfx.GreatRaise : Sfx.None);
        }

        /// Raising the parry starts the chain over at the Sweep, and a step still winding up never lands.
        void ResetHalberdChain(Player p)
        {
            p.Combo &= ~7;
            p.ComboT = 0f;
            p.FireCd = 0f;
            pendingSwings.RemoveAll(s => s.Owner == p.Id && s.Halberd > 0 && s.Delay > 0f);
        }

        /// Host: the weapon-action key went down (on) or up with the Halberd.
        void HostPike(Player p, bool on)
        {
            if (p.Weapon.Id != WeaponId.Halberd)
                return;
            p.HbHeld = on;
            if (!on)
            {
                if (p.HbSet && CanSetPike(p) && p.HbLungeCd <= 0f)
                    PikeLunge(p);
                p.HbSet = false;
            }
            else
                TrySetPike(p);
        }

        bool CanSetPike(Player p) => !p.Downed && p.StunT <= 0f && p.SwallowT <= 0f && !p.ParryActive && State != MatchState.GameOver;

        void TrySetPike(Player p)
        {
            if (p.HbSet || !CanSetPike(p))
                return;
            p.HbSet = true;
            p.HbPikeA = p.Aim;
            p.HbPokeCd = Math.Max(p.HbPokeCd, 0.12f);  // (planting it takes a moment)
            p.Combo &= ~7;  // the chain starts over
            p.ComboT = 0f;
            BroadcastSound(Sfx.GreatRaise);
        }

        /// The enemy the set pike turns toward: the nearest within PikeScan (its edge), or null.
        Enemy PikeTarget(Player p)
        {
            Enemy best = null;
            float bd = float.MaxValue;
            foreach (var e in AllFoes(by: p.Id))
            {
                if (e.Hp <= 0f || IsSpawning(e))
                    continue;
                float d = e.Pos.DistanceTo(p.Pos) - e.Radius;
                if (d < PikeScan && d < bd)
                {
                    bd = d;
                    best = e;
                }
            }
            return best;
        }

        /// Host, every tick: the pike held or dropped, turning to the nearest enemy, and poking it away.
        void TickHalberd(float dt)
        {
            foreach (var p in Players.Values)
            {
                p.HbPokeCd = Math.Max(0f, p.HbPokeCd - dt);
                p.HbLungeCd = Math.Max(0f, p.HbLungeCd - dt);
                if (p.Weapon.Id != WeaponId.Halberd || p.Downed)
                {
                    p.HbSet = p.HbHeld = false;
                    continue;
                }
                if (p.HbSet && !CanSetPike(p))
                    p.HbSet = false;  // (stunned, swallowed or parrying: the pike comes up; the key still held sets it again)
                if (!p.HbSet && p.HbHeld)
                    TrySetPike(p);
                if (!p.HbSet)
                {
                    p.HbPikeA = p.Aim;
                    continue;
                }
                var e = PikeTarget(p);
                float want = e != null ? (e.Pos - p.Pos).Angle : p.Aim;
                float diff = MathX.WrapPi(want - p.HbPikeA), turn = PikeTurn * dt;
                p.HbPikeA = MathX.WrapPi(p.HbPikeA + Math.Max(-turn, Math.Min(turn, diff)));
                if (e == null || p.HbPokeCd > 0f)
                    continue;
                int extra = ExtraShots(p);
                float reach = PikeReach(p, extra);
                if (Math.Abs(MathX.WrapPi(want - p.HbPikeA)) > PikeAlign || e.Pos.DistanceTo(p.Pos) - e.Radius > reach)
                    continue;
                Poke(p, reach, extra);
            }
        }

        /// The set pike jabs out along HbPikeA: everything on the line to the point is hit and shoved away from you.
        void Poke(Player p, float reach, int extra)
        {
            p.HbPokeCd = PikeGap / FireRate(p);
            p.HbPokes++;
            var w = p.Weapon;
            var s = new Swing { Owner = p.Id, Aim = p.HbPikeA, Reach = reach, Dmg = w.Damage * p.Stats.DmgMult * PikeMult * (1f + 0.1f * extra) };
            s.HalfWidth = PikeHalfW;
            s.Thrust = true;
            s.Knock = PikeKnock;
            s.Stun = PikeStun;
            s.StarStun = true;
            s.Delay = PikeJab;
            s.NoFx = true;
            s.Sound = Sfx.LanceJab;
            QueueSwing(s);
            // The jab, drawn from its age: Pos2 the point at full reach, Radius the poker.
            BroadcastFx(FxKind.HalberdPoke, p.Pos, p.Pos + V2.FromAngle(p.HbPikeA) * reach, HbGlow, p.Id, PikeFxTime, Sfx.None);
        }

        /// Set the Pike let go: the pike drives out along where it points, a long thin box that hits everything on the line.
        void PikeLunge(Player p)
        {
            int extra = ExtraShots(p);
            var w = p.Weapon;
            float baseReach = MeleeReach(p, (w.Reach + w.Grow * extra) * p.Stats.HalberdReachMult);
            p.HbLungeCd = LungeCd / FireRate(p);
            p.HbLunges++;
            p.FireCd = Math.Max(p.FireCd, HalberdMotion.LungeOut + HalberdMotion.LungeHold);
            var s = new Swing { Owner = p.Id, Aim = p.HbPikeA, Reach = baseReach * HalberdMotion.LungeK, Dmg = w.Damage * p.Stats.DmgMult * LungeMult * (1f + 0.1f * extra) };
            s.HalfWidth = LungeHalfW;
            s.Thrust = true;
            s.Heavy = true;
            s.Knock = LungeKnock;
            s.Stun = LungeStun;
            s.StarStun = true;
            s.Delay = HalberdMotion.LungeOut * 0.8f;
            s.NoFx = true;
            s.Halberd = 5;
            s.Sound = Sfx.LanceBreach;
            QueueSwing(s);
            // Drawn as step 5 from its age (HalberdLook.cs): Pos2 the weapon's reach along the pike.
            BroadcastFx(FxKind.HalberdStep, p.Pos, p.Pos + V2.FromAngle(p.HbPikeA) * baseReach, HbGlow, p.Id * 10 + 5, HalberdMotion.StepTotal(5), Sfx.None);
        }

        // --- Your own game ------------------------------------------------------------------------------------------

        /// Your own game, every tick: the weapon-action key's edges go to the host (the pike is held). True while the Halberd is out.
        bool LocalHalberd(Player me, V2 aimAt, bool pressed, bool released)
        {
            if (me.Weapon.Id != WeaponId.Halberd)
                return false;
            if (pressed || released)
            {
                byte kind = pressed ? WeaponAlt.PikeOn : WeaponAlt.PikeOff;
                if (!IsHost && pressed && !me.Downed && me.StunT <= 0f)
                    me.HbSet = true;  // (the slow starts at once on your own screen; the host's snapshot agrees)
                else if (!IsHost && released)
                    me.HbSet = false;
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

        // --- Network: the player's Halberd fields (SyncPlayer) ---

        void SyncHalberd(NetSync s, Player p)
        {
            s.Bool(ref p.HbSet);  // the Halberd's set pike
            s.Float(ref p.HbPikeA);
        }

        /// WeaponDef.BotAlt (BotWeaponAlt.cs): set the pike while a pack closes in; lift it once they've been kept off or it has held a while (held)
        static bool HalberdBotAlt(Game g, AltView v, float dt, bool late, ref bool firing, ref V2 aimAt)
        {
            var me = v.Me;
            if (g.botAltWas)
                return g.botAltHoldT < 3.5f && g.EnemiesNear(me.Pos, PikeScan) >= 1 && (v.Near >= 2 || g.botAltHoldT < 1f);
            return g.Tap(v.Near >= 3 || g.EnemiesNear(me.Pos, 150f) >= 2, 1.2f);
        }
    }
}
