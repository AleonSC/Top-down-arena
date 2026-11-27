using System;
using System.Collections.Generic;

namespace Arena
{
    /// Clot Crystals, the Healer Hemomancer's 4th choice (`AbilityId.ClotCrystals`, `AbilityDef.Fourth`: the lobby's 4th ability
    /// key; mockup claude.ai artifact "Clot Crystals", look A Garnet). 15% of your max health grows three crystals of hardened
    /// blood round you (mastered: four). For 12 s they circle you 46 px out; each one picks the nearest enemy shot within 150 px of
    /// you that no other crystal is after, shudders at it for 0.07 s, then jerks at where it will be (0.12 s, overshooting a
    /// little) and eats it, then settles for 0.2 s. A crystal also eats any enemy shot that flies into it. Each has 150 health and
    /// a shot takes its real damage off it, so about a dozen shots break one (it shatters). At the end a crystal melts back into
    /// you and gives back 10% of the health it has left. Hard Clot (Rare card): 30% more health and 3 s longer, a stack.
    ///
    /// Each crystal is a `MinionKind.ClotCrystal` minion moved by the host (TickClotCrystal), replicated like any minion: Pos,
    /// Aim (the way its point faces), GuardFrac (its health left), Stacks (its state: CryRising .. CryMelting) and Wind (0..1
    /// through that state), so no snapshot field is needed. The look is Game/ClotCrystalsLook.cs.
    public partial class Game
    {
        public const float CryCost = 0.15f, CryCooldown = 18f;
        public const int CryCount = 3, CryCountMastered = 4;
        public const float CryHp = 150f;             // each crystal's health
        public const float CryLife = 12f;            // how long they last
        public const float CryReach = 150f;          // they go for shots this close to you
        public const float CryRing = 46f;            // how far out they circle
        public const float CrySpin = 0.6f;           // how fast they circle (rad/s)
        public const float CryCatch = 16f;           // a crystal eats a shot this close (its centre to the shot's edge)
        public const float CryRise = 0.45f, CryTell = 0.07f, CryJerk = 0.12f, CrySettle = 0.2f, CryRest = 0.05f, CryMelt = 0.6f;  // each state's time
        public const float CryRefund = 0.1f;         // melting back: this share of its health left heals you

        /// A crystal's state (replicated as Minion.Stacks; Minion.Wind runs 0..1 through it).
        public const int CryRising = 0, CryIdle = 1, CryTelling = 2, CryJerking = 3, CrySettling = 4, CryMelting = 5;

        /// Where crystal `slot` of `n` sits round `owner` at this age (they turn slowly together).
        public static V2 CrySlotPos(V2 owner, float baseAng, int slot, int n, float age) =>
            owner + V2.FromAngle(baseAng + MathX.Tau * slot / Math.Max(1, n) + age * CrySpin) * CryRing;

        /// The jerk's ease: out fast, a little past, and back (the mockup's easeOutBack).
        public static float CryEase(float t)
        {
            const float c1 = 1.9f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        static float CryTurn(float from, float to, float k)
        {
            float d = to - from;
            while (d > MathX.Pi) d -= MathX.Tau;
            while (d < -MathX.Pi) d += MathX.Tau;
            return from + d * MathX.Clamp01(k);
        }

        /// Host: Clot Crystals cast (the health is paid by the ability's HealthCost). A recast replaces any still up.
        void CastClotCrystals(Player p, bool mastered)
        {
            var old = new List<int>();
            foreach (var o in Minions.Values)
                if (o.Kind == MinionKind.ClotCrystal && o.Owner == p.Id)
                    old.Add(o.Id);
            foreach (int id in old)
                Minions.Remove(id);
            int n = mastered ? CryCountMastered : CryCount;
            float life = CryLife + p.Stats.CryBonusTime;
            for (int i = 0; i < n; i++)
            {
                var m = SpawnMinion(MinionKind.ClotCrystal, p, CrySlotPos(p.Pos, p.Aim, i, n, 0f), life);  // (BlockHp: CryHp times the cast's power)
                m.BlockHp *= p.Stats.CryHpMult;
                m.MaxHp = m.BlockHp;
                m.Ang = p.Aim;
                m.CrySlot = i;
                m.CryN = n;
                m.Pos = CrySlotPos(p.Pos, p.Aim, i, n, 0f);  // (not clamped: it sits round you)
                m.Disp = m.Pos;
                m.Aim = (m.Pos - p.Pos).Angle;
                m.Stacks = CryRising;
            }
            BroadcastFx(FxKind.Ring, p.Pos, Kits.Get(MinionKind.ClotCrystal).Color, CryRing, 0.4f, Sfx.SpellArcane);
        }

        static void CryEnter(Minion m, int state)
        {
            m.Stacks = state;
            m.CryT = 0f;
            m.Wind = 0f;
        }

        /// Does this crystal eat this shot? (Any enemy shot touching it, once risen and until it melts.)
        bool ClotCatches(Minion m, Bullet b) =>
            m.BlockHp > 0f && m.Stacks != CryRising && m.Stacks != CryMelting && b.Pos.CloserThan(m.Pos, CryCatch + b.Radius);

        /// A shot a crystal can go for: an enemy's, that can be blocked and flies low (not a lobbed bomb).
        bool CryPreyShot(Bullet b) =>
            Defs.IsEnemyShot(b.Kind) && b.Kind != Defs.HollowShot && !Defs.Unblockable(b.Kind) && !Lobbed(b.Kind) && b.Kind != Defs.MineShot;

        /// The nearest shot within CryReach of the owner that no crystal is after, or null.
        Bullet CryPrey(Minion m, Player owner)
        {
            Bullet best = null;
            float bd = float.MaxValue;
            foreach (var b in Bullets.Values)
            {
                if (!CryPreyShot(b) || !b.Pos.CloserThan(owner.Pos, CryReach))
                    continue;
                float d = b.Pos.DistanceSq(m.Pos);
                if (d >= bd || CryClaimed(b.Id))
                    continue;
                bd = d;
                best = b;
            }
            return best;
        }

        bool CryClaimed(int bulletId)
        {
            foreach (var o in Minions.Values)
                if (o.Kind == MinionKind.ClotCrystal && o.CryBullet == bulletId)
                    return true;
            return false;
        }

        /// Host, each tick for a crystal (TickMinions, instead of its usual moves).
        void TickClotCrystal(Minion m, Player owner, float dt)
        {
            m.CryT += dt;
            float age = m.Duration - m.T;
            V2 slot = CrySlotPos(owner.Pos, m.Ang, m.CrySlot, m.CryN, age);
            if (m.Stacks != CryMelting && m.T <= CryMelt)  // time's nearly up: it melts back into you
            {
                m.CryBullet = 0;
                CryEnter(m, CryMelting);
                m.CryFrom = m.Pos;
            }
            Bullet b;
            switch (m.Stacks)
            {
                case CryRising:
                    m.Pos = slot;
                    m.Aim = (slot - owner.Pos).Angle;
                    m.Wind = MathX.Clamp01(m.CryT / CryRise);
                    if (m.CryT >= CryRise)
                        CryEnter(m, CryIdle);
                    break;
                case CryIdle:
                    m.Pos = V2.Lerp(m.Pos, slot, 1f - (float)Math.Exp(-7.0 * dt));
                    m.Aim = CryTurn(m.Aim, (m.Pos - owner.Pos).Angle, dt * 8f);
                    if (m.CryT >= CryRest && (b = CryPrey(m, owner)) != null)
                    {
                        m.CryBullet = b.Id;
                        CryEnter(m, CryTelling);
                        m.Aim = (b.Pos - m.Pos).Angle;
                    }
                    break;
                case CryTelling:  // the shudder: it turns to the shot
                    if (!Bullets.TryGetValue(m.CryBullet, out b))
                    {
                        m.CryBullet = 0;
                        CryEnter(m, CryIdle);
                        m.CryT = CryRest;
                        break;
                    }
                    m.Aim = (b.Pos - m.Pos).Angle;
                    m.Wind = MathX.Clamp01(m.CryT / CryTell);
                    if (m.CryT >= CryTell)
                    {
                        V2 to = b.Pos + b.Vel * CryJerk, off = to - owner.Pos;  // where it will be when the jerk lands
                        if (off.Length > CryReach)
                            to = owner.Pos + off * (CryReach / off.Length);
                        m.CryFrom = m.Pos;
                        m.CryTo = to;
                        m.Aim = (to - m.Pos).Angle;
                        CryEnter(m, CryJerking);
                    }
                    break;
                case CryJerking:
                {
                    float k = MathX.Clamp01(m.CryT / CryJerk);
                    m.Pos = V2.Lerp(m.CryFrom, m.CryTo, CryEase(k));
                    m.Wind = k;
                    if (k >= 1f)
                    {
                        m.CryBullet = 0;
                        CryEnter(m, CrySettling);
                    }
                    break;
                }
                case CrySettling:
                    m.Wind = MathX.Clamp01(m.CryT / CrySettle);
                    if (m.CryT >= CrySettle)
                        CryEnter(m, CryIdle);
                    break;
                case CryMelting:
                {
                    float k = MathX.Clamp01(m.CryT / CryMelt);
                    m.Pos = V2.Lerp(m.CryFrom, owner.Pos, k * k);
                    m.Aim = (owner.Pos - m.CryFrom).Angle;
                    m.Wind = k;
                    break;
                }
            }
        }

        /// Host: a crystal shot to nothing (BlockedBySummons): it shatters.
        void ClotShatter(Minion m)
        {
            m.CryBullet = 0;
            BroadcastFx(FxKind.ClotShatter, m.Pos, new V2(m.Owner, 0f), m.Def.Color, m.Def.Radius, 0.7f, Sfx.Hit);
        }

        /// Host: a crystal's time is up (TickMinions): what health it has left melts back into its owner.
        void ClotMelted(Minion m, Player owner)
        {
            if (m.BlockHp <= 0f || owner.Downed)
                return;
            Heal(owner, m.BlockHp * CryRefund, owner, "Clot Crystals");
        }
    }
}
