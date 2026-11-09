using System;

namespace Arena
{
    /// The Tank Knight's Last Stand: you plant yourself and brace a crescent of nine steel plates (178 degrees wide, 120 px
    /// out) that stops every enemy bullet from the front. Your teammates' shots pass out through it. You take a tenth of
    /// the damage of every shot it stops (a mastered one takes a twentieth). You can't move while it stands, and it has no
    /// timer: it stands until you go down or press the ability again (the cooldown starts when it comes down). The barrier
    /// faces your cursor but turns slowly (`StandTurn`), so you commit to a side.
    /// Player.StandOn and StandAim are replicated (the barrier's wear is just your health); the cancel press goes to the host as
    /// a normal Msg.Ability. Drawn in Game/LastStandLook.cs (option B "Aegis" of Docs/Mockups/last-stand-aegis.html).
    public partial class Game
    {
        public const float StandRadius = 120f;     // how far out the plates stand
        public const float StandHalfArc = 1.55f;   // half the crescent's width (about 178 degrees in all)
        public const int StandPlates = 9;          // plates across the crescent (drawing only)
        public const float StandPlant = 0.5f;      // it takes this long to plant; the barrier only blocks once it is down
        public const float StandTurn = 1.7f;       // radians a second the barrier follows your aim
        public const float StandShare = 0.10f, StandShareMastered = 0.05f;  // the share of a stopped shot's damage you take

        /// Host: Last Stand was cast. You are rooted until it ends.
        void PlantStand(Player p, bool mastered)
        {
            p.StandOn = true;
            p.StandT = 0f;
            p.StandMastered = mastered;
            p.StandAim = p.Aim;
            BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.55f, 0.78f, 1f), StandRadius + 30f, 0.5f, Sfx.Clang);
        }

        /// Pressing Last Stand again while it stands takes it down (no cooldown is spent for the press itself).
        public bool StandRelease(Player p, int slot) => p.StandOn && p.Slots[slot].Id == AbilityId.LastStand;

        /// Abilities that would carry you somewhere: planted, you can't use them.
        static bool MovesYou(AbilityId id)
        {
            switch (id)
            {
                case AbilityId.Dash:
                case AbilityId.LeapSlam:
                case AbilityId.Lunge:      // the Gauntlet's charge
                case AbilityId.Whirlwind:  // Misericorde's leaps
                case AbilityId.Bulldoze:
                case AbilityId.ShadowStep:
                case AbilityId.Blink:
                case AbilityId.RoyalDive:
                case AbilityId.HemoNova:  // Vein Rush
                    return true;
            }
            return false;
        }

        /// Host, every tick: the barrier turns toward your aim, and comes down when you go down or the fight is over.
        void TickStand(Player p, float dt)
        {
            if (!p.StandOn)
                return;
            p.StandT += dt;
            if (p.Downed || State != MatchState.Fighting)
            {
                EndStand(p, false);
                return;
            }
            float d = MathX.Fmod(p.Aim - p.StandAim + MathX.Pi, MathX.Tau) - MathX.Pi;
            p.StandAim += Math.Max(-StandTurn * dt, Math.Min(StandTurn * dt, d));
        }

        /// The barrier comes down (you cancelled, went down, or the wave ended). Its cooldown starts now.
        void EndStand(Player p, bool cancelled)
        {
            if (!p.StandOn)
                return;
            p.StandOn = false;
            p.StandT = 0f;
            int slot = Upgrades.UltSlot;
            if (p.Slots[slot].Id == AbilityId.LastStand && p.Slots[slot].Charges <= 0)
                p.Slots[slot].Timer = Cooldown(p, slot);
            BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.55f, 0.78f, 1f), StandRadius, 0.35f, cancelled ? Sfx.Clang : Sfx.None);
        }

        /// Does the planted barrier stop this enemy bullet? It must be down, cross the plates' circle and be inside the crescent.
        bool StandBlocks(Player p, Bullet b)
        {
            if (!p.StandOn || p.StandT < StandPlant || !Enters(b, p.Pos, StandRadius))
                return false;
            float off = MathX.Fmod((b.Pos - p.Pos).Angle - p.StandAim + MathX.Pi, MathX.Tau) - MathX.Pi;
            return Math.Abs(off) < StandHalfArc;
        }

        /// A bullet reached the plates: it stops, and you take a tenth of what it would have done.
        void StandHit(Player p, Bullet b)
        {
            float share = p.StandMastered ? StandShareMastered : StandShare;
            float grace = p.InvulnT;
            Damage(p, b.Dmg * share, ShotSource(b));
            if (!p.Downed)
                p.InvulnT = grace;  // a steady stream must not turn into a run of hit grace for the shots that get through
            BroadcastFx(FxKind.StandHit, b.Pos, p.Pos, new Rgba(0.55f, 0.78f, 1f), b.Dmg, 0.5f, b.Dmg >= 30f ? Sfx.Clang : Sfx.None);
        }
    }
}
