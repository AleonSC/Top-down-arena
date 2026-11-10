using System;

namespace Arena
{
    /// The Arc Knuckles (DPS Knight, the fourth weapon: Choice 3; Docs/ArcKnuckles.md, Docs/Mockups/gauntlets.html, the Arc Coil look).
    ///   - Chain: Jab, Cross, Hook, Uppercut, Twin Hammer, then it starts over. Each press throws the next punch (the host counts
    ///     the steps in Player.Combo's low bits, as the Twin Blades do); KnuckleChainWindow without a punch and it starts over.
    ///     The Uppercut stuns what it hits (it's launched); the Twin Hammer slams a shockwave round a point ahead of you.
    ///   - Parry: raising it cancels a punch still winding up and sends the chain back to the Jab (ResetAttackChains). A
    ///     perfect parry also arms the Counter Hammer (Player.KnuckleArmed): the next press is a bigger, gold Twin Hammer, from
    ///     any step. It fades after KnuckleArmTime unused.
    /// Each punch is one effect (FxKind.KnuckleStep), drawn from its age (Game/ArcKnucklesLook.cs).
    public partial class Game
    {
        public const float KnuckleChainWindow = 1.2f, KnuckleArmTime = 3f;
        /// Each step (1 Jab, 2 Cross, 3 Hook, 4 Uppercut, 5 Twin Hammer, 6 Counter Hammer): damage against the weapon's Damage
        /// (12, 14, 16, 18, 30, 38), when the punch lands after the press, and when the next press counts.
        public static readonly float[] KnuckleMult = { 1f, 14f / 12f, 16f / 12f, 18f / 12f, 30f / 12f, 38f / 12f };
        public static readonly float[] KnuckleLand = { 0.09f, 0.11f, 0.16f, 0.14f, 0.26f, 0.26f };
        public static readonly float[] KnuckleNext = { 0.24f, 0.28f, 0.32f, 0.38f, 0.62f, 0.62f };
        /// How long each step's effect plays (the punch and the fist coming back to the guard).
        public static readonly float[] KnuckleDur = { 0.28f, 0.32f, 0.36f, 0.42f, 0.62f, 0.62f };
        public const float KnuckleJabReach = 70f, KnuckleCrossReach = 80f, KnuckleJabWidth = 14f, KnuckleCrossWidth = 15f;
        public const float KnuckleHookReach = 72f, KnuckleHookArc = 120f, KnuckleHookKnock = 8f;
        public const float KnuckleUpperReach = 70f, KnuckleUpperWidth = 18f, KnuckleUpperStun = 0.55f;
        public const float KnuckleHammerAt = 44f, KnuckleHammerR = 110f, KnuckleHammerKnock = 38f, KnuckleHammerStun = 0.6f;
        public const float KnuckleCounterR = 135f, KnuckleCounterKnock = 48f, KnuckleCounterStun = 0.8f;
        public const float KnuckleAftershock = 0.2f, KnuckleAftershockOut = 40f, KnuckleAftershockMult = 0.6f;
        public static readonly Rgba ArcBlue = new Rgba(0.48f, 0.83f, 1f), ArcGold = new Rgba(1f, 0.82f, 0.29f);

        /// The step a Combo value holds (0: the next press is the Jab).
        public static int KnuckleStepOf(int combo) => combo & 7;

        /// Host: one press of the primary with the Arc Knuckles (from FireKnightWeapon): the next punch, or the Counter Hammer.
        void FireArcKnuckles(Player p, float aim, float dmg, int extra)
        {
            var w = p.Weapon;
            int last = KnuckleStepOf(p.Combo);
            int step = p.KnuckleArmed ? 6 : last >= 1 && last < 5 && p.ComboT > 0f ? last + 1 : 1;
            p.KnuckleArmed = false;
            p.KnuckleArmedT = 0f;
            int count = ((p.Combo >> 3) + 1) & 31;
            p.Combo = (count << 3) | step;
            p.ComboT = KnuckleNext[step - 1] + KnuckleChainWindow;
            p.FireCd = KnuckleNext[step - 1] / FireRate(p);
            float hit = w.Damage * dmg * KnuckleMult[step - 1], land = KnuckleLand[step - 1];
            Sfx sound = Sfx.KnuckleJab;
            switch (step)
            {
                case 1:
                case 2:
                {
                    var s = KnuckleSwing(p, aim, step == 1 ? KnuckleJabReach : KnuckleCrossReach, 0f, hit, land, step);
                    s.HalfWidth = step == 1 ? KnuckleJabWidth : KnuckleCrossWidth;
                    s.Thrust = true;
                    break;
                }
                case 3:
                {
                    var s = KnuckleSwing(p, aim, KnuckleHookReach, KnuckleHookArc, hit, land, step);
                    s.Knock = KnuckleHookKnock;
                    sound = Sfx.KnuckleHook;
                    break;
                }
                case 4:
                {
                    var s = KnuckleSwing(p, aim, KnuckleUpperReach, 0f, hit, land, step);
                    s.HalfWidth = KnuckleUpperWidth;
                    s.Thrust = true;
                    s.Stun = KnuckleUpperStun;  // launched
                    sound = Sfx.KnuckleUpper;
                    break;
                }
                default:
                {
                    bool counter = step == 6;
                    float reachMult = MeleeReach(p, KnuckleHookReach) / KnuckleHookReach;  // Reach upgrades widen the shockwave too
                    for (int k = 0; k <= extra; k++)  // each extra projectile: an aftershock, further out
                    {
                        var s = KnuckleSwing(p, aim, KnuckleHammerAt + KnuckleAftershockOut * k, 0f, hit * (k == 0 ? 1f : KnuckleAftershockMult), land + KnuckleAftershock * k, step);
                        s.Circle = (counter ? KnuckleCounterR : KnuckleHammerR) * reachMult;
                        s.CircleAt = KnuckleHammerAt + KnuckleAftershockOut * k;
                        s.Knock = counter ? KnuckleCounterKnock : KnuckleHammerKnock;
                        s.Stun = counter ? KnuckleCounterStun : KnuckleHammerStun;
                        s.Power = counter ? 1f : 0.6f;
                        s.Heavy = true;
                        s.Sound = k == 0 ? (counter ? Sfx.KnuckleCounter : Sfx.KnuckleSlam) : Sfx.KnuckleSlam;
                    }
                    sound = Sfx.None;  // (the slam's sound plays as it lands)
                    break;
                }
            }
            // The whole punch, drawn from its age: Radius carries who and which step.
            BroadcastFx(FxKind.KnuckleStep, p.Pos, p.Pos + V2.FromAngle(aim) * MeleeReach(p, w.Reach), step == 6 ? ArcGold : ArcBlue, p.Id * 10 + step,
                step >= 5 ? KnuckleLand[step - 1] + 0.7f + KnuckleAftershock * extra : KnuckleDur[step - 1], sound);  // (a hammer's lasts while its shockwave fades)
        }

        /// One punch landing `delay` after the press.
        Swing KnuckleSwing(Player p, float aim, float reach, float arcDeg, float dmg, float delay, int step)
        {
            var s = NewSwing(p, aim, reach, arcDeg, dmg);
            s.Delay = delay;
            s.NoFx = true;  // the step's own effect draws it (and the press played its sound; a slam has its own as it lands)
            s.Knuckle = (byte)step;
            pendingSwings.Add(s);
            return s;
        }

        /// Host: a perfect parry with the Arc Knuckles out arms the Counter Hammer (from ParryHit, Parry.cs).
        void ArmKnuckleCounter(Player p)
        {
            if (p.Weapon.Id != WeaponId.ArcKnuckles || p.Downed)
                return;
            if (!p.KnuckleArmed)
                BroadcastSound(Sfx.KnuckleArm);
            p.KnuckleArmed = true;
            p.KnuckleArmedT = KnuckleArmTime;
        }

        /// Host, every tick: the chain forgets its place (TickTwinBlades), an unused Counter Hammer fades.
        void TickArcKnuckles(float dt)
        {
            foreach (var p in Players.Values)
            {
                if (!p.KnuckleArmed)
                    continue;
                if (p.Weapon.Id != WeaponId.ArcKnuckles || p.Downed || (p.KnuckleArmedT -= dt) <= 0f)
                {
                    p.KnuckleArmed = false;
                    p.KnuckleArmedT = 0f;
                }
            }
        }

        /// Raising the parry cancels the chain (ResetAttackChains): back to the Jab, a punch still winding up never lands. An
        /// armed Counter Hammer stays armed.
        void ResetKnuckleChain(Player p)
        {
            p.Combo &= ~7;
            p.ComboT = 0f;
            p.FireCd = 0f;
            pendingSwings.RemoveAll(s => s.Owner == p.Id && s.Knuckle != 0 && s.Delay > 0f);
        }
    }
}
