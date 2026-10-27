using System;

namespace Arena
{
    /// The parry (hold right click): a small shield in front of you, facing your cursor. It stops
    /// enemy bullets and melee hits from that side, but you can't shoot while it's up and you move
    /// slower. It takes a limited amount of damage, then breaks and has to recharge. Raising it
    /// just as something hits (a perfect parry) sends bullets back and costs nothing.
    public partial class Game
    {
        public const float ParryCapacity = 150f;    // damage it soaks before it breaks
        public const float ParryHalfArc = 0.95f;    // radians either side of your aim (about 110 degrees in all)
        public const float ParryReach = 20f;        // how far past your body it sticks out
        const float ParrySlow = 0.7f;               // move speed while it's up
        const float PerfectParry = 0.22f;           // seconds after raising that count as a perfect parry
        const float PerfectRearm = 0.5f;            // it must have been down this long first (no spamming)
        const float ParryBreakTime = 3f;            // locked out after it breaks
        const float ParryRegenDelay = 0.8f, ParryRegen = 40f;
        const float CounterTime = 2.5f;             // Counterstance: faster fire after a perfect parry

        /// How much the shield soaks for this player (Reinforced Guard raises it).
        public static float ParryMax(Player p) => ParryCapacity * p.Stats.ParryCapacityMult;

        static float HalfArc(Player p) => Math.Min(MathX.Pi * 0.8f, ParryHalfArc * p.Stats.ParryArcMult);

        public static float ParryArc(Player p) => HalfArc(p);

        bool PerfectNow(Player p) => p.ParryPerfect && p.ParryHeld < PerfectParry + p.Stats.PerfectBonus;

        void TickParry(Player p, float dt)
        {
            p.CounterT = Math.Max(0f, p.CounterT - dt);
            p.ParryBroken = Math.Max(0f, p.ParryBroken - dt);
            p.ParryBumpT = Math.Max(0f, p.ParryBumpT - dt);
            if (p.ParryActive)
            {
                if (p.ParryHeld <= 0f)
                    p.ParryPerfect = p.ParryDown >= PerfectRearm;  // just raised
                p.ParryHeld += dt;
                p.ParryDown = 0f;
            }
            else
            {
                p.ParryHeld = 0f;
                p.ParryDown += dt;
                if (p.ParryBroken <= 0f && p.ParryDown > ParryRegenDelay)
                    p.ParryHp = Math.Min(ParryMax(p), p.ParryHp + ParryRegen * p.Stats.ParryCapacityMult * dt);
            }
        }

        /// Is this point (a bullet or an enemy's edge) inside the shield's arc?
        public static bool InParryArc(Player p, V2 pos, float radius)
        {
            V2 d = pos - p.Pos;
            float reach = p.Def.BodyR + ParryReach + radius;
            if (d.LengthSq > reach * reach)
                return false;
            float diff = Math.Abs(MathX.Fmod(d.Angle - p.Aim + MathX.Pi, MathX.Tau) - MathX.Pi);
            return diff < HalfArc(p);
        }

        /// The shield takes a hit: free during a perfect parry, otherwise it wears and may break.
        /// Returns whether the blocked bullet should be sent back (perfect parry or Mirror Guard).
        bool ParryHit(Player p, float dmg, V2 at)
        {
            if (PerfectNow(p))
            {
                if (p.Stats.CounterFireRate > 0f)
                    p.CounterT = CounterTime;  // Counterstance
                BroadcastFx(FxKind.Spark, at, new Rgba(1f, 1f, 1f), 14f, 0.2f, Sfx.Blink);
                Recorder?.Parry(p, dmg, true, false);
                return true;
            }
            Recorder?.Parry(p, Math.Min(dmg, p.ParryHp), false, p.ParryHp - dmg <= 0f);
            p.ParryHp -= dmg;
            BroadcastFx(FxKind.Spark, at, new Rgba(0.55f, 0.9f, 1f), 9f, 0.15f, Sfx.Hit);
            if (p.ParryHp <= 0f)
            {
                p.ParryHp = 0f;
                p.ParryBroken = ParryBreakTime;
                BroadcastFx(FxKind.Boom, p.Pos + V2.FromAngle(p.Aim) * (p.Def.BodyR + 10f), new Rgba(0.55f, 0.9f, 1f), 30f, 0.35f, Sfx.Hit);
            }
            return p.Stats.MirrorChance > 0f && rng.NextDouble() < p.Stats.MirrorChance;  // Mirror Guard
        }

        /// Melee enemies running into the shield hit it instead of you, and get shoved back.
        bool ParryContact(Player p, Enemy e, float dmg)
        {
            if (!p.ParryActive || !InParryArc(p, e.Pos, e.Radius))
                return false;
            if (p.ParryBumpT > 0f)
                return true;  // already took this bump
            p.ParryBumpT = 0.25f;
            ParryHit(p, dmg, p.Pos + (e.Pos - p.Pos).Normalized() * (p.Def.BodyR + 10f));
            if (!Heavy(e))
                e.Pos = ClampToArena(e.Pos + (e.Pos - p.Pos).Normalized() * 45f, e.Radius);
            return true;
        }
    }
}
