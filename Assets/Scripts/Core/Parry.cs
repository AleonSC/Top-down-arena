using System;

namespace Arena
{
    /// The parry (hold the parry button: right click by default): a small shield in front of you, facing your cursor. It stops
    /// enemy bullets and melee hits from that side, but you can't shoot while it's up and you move
    /// slower. It takes a limited amount of damage, then breaks and has to recharge. Raising it
    /// just as something hits (a perfect parry) sends bullets back and costs nothing.
    public partial class Game
    {
        public const float ParryCapacity = 150f;    // damage it soaks before it breaks
        public const float ParryHalfArc = 0.95f;    // radians either side of your aim (about 110 degrees in all)
        public const float ParryReach = 20f;        // how far past your body it sticks out
        const float ParrySlow = 0.7f;               // move speed while it's up
        public const float PerfectParry = 0.30f;    // seconds after raising that count as a perfect parry (0.22 before Nov 2026)
        const float PerfectRearm = 0.5f;            // it must have been down this long first (no spamming)
        const float ParryBreakTime = 3f;            // locked out after it breaks
        const float ParryRegenDelay = 0.8f, ParryRegen = 40f;
        const float CounterTime = 2.5f;             // Counterstance: faster fire after a perfect parry
        const float ShatterTime = 0.65f;            // how long the breaking shield's pieces fly
        // A perfect parry's feel (Docs/Mockups/parry-perfect-rework.html): the shot goes back as a fast white needle
        // (Defs.ParryNeedle), the shield flashes on every screen (FxKind.PerfectParry), the parrier's own picture holds
        // (Game/Juice.cs), and the world slows (WorldSlowT): enemies and every shot move at PerfectSlow for PerfectSlowTime,
        // at most once every PerfectSlowGap, so a burst slows the game once. Players keep their own speed.
        public const float NeedleSpeed = 900f, PerfectSlow = 0.3f, PerfectSlowTime = 0.3f, PerfectSlowGap = 0.6f, PerfectSlowEase = 0.08f;
        public const float PerfectHold = 0.05f, PerfectFxTime = 0.35f;
        public float WorldSlowT;   // > 0: the world is slowed by a perfect parry (every game: the host's sets it, guests' from the effect)
        float lastPerfectSlow = -99f;

        /// How fast the world (enemies and every shot) runs this tick: slowed after a perfect parry, easing back at the end.
        public float WorldRate => SurvFrozen ? 0f : WorldSlowT <= 0f ? 1f : WorldSlowT >= PerfectSlowEase ? PerfectSlow : PerfectSlow + (1f - PerfectSlow) * (1f - WorldSlowT / PerfectSlowEase);

        void StartWorldSlow() => WorldSlowT = PerfectSlowTime;

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
                {
                    p.ParryPerfect = p.ParryDown >= PerfectRearm;  // just raised
                    ResetAttackChains(p);  // raising it cancels an attack chain mid-way (Katana.cs)
                }
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
            return Facing(p, d);
        }

        /// Does the shield face this way (any distance)?
        static bool Facing(Player p, V2 d) => Math.Abs(MathX.WrapPi(d.Angle - p.Aim)) < HalfArc(p);

        /// The shield takes a hit: free during a perfect parry, otherwise it wears and may break.
        /// Returns whether the blocked bullet should be sent back (perfect parry or Mirror Guard).
        bool ParryHit(Player p, float dmg, V2 at)
        {
            if (PerfectNow(p))
            {
                if (p.Stats.CounterFireRate > 0f)
                    p.CounterT = CounterTime;  // Counterstance
                ArmVgFaith(p);  // the Vowguard's Faith fills (Vowguard.cs)
                ArmKnuckleCounter(p);  // the Arc Knuckles' next press is the Counter Hammer (ArcKnuckles.cs)
                PrimeRime(p);  // the Frost Lance's next step is a Rime Counter (FrostLance.cs)
                // The flash (every screen): Pos is where the shot met the shield, Pos2 the parrier's id and aim, Radius half the
                // shield's arc, the colour the parrier's; alpha 1 means this one slows the world (the first in PerfectSlowGap).
                bool slow = RunTime - lastPerfectSlow >= PerfectSlowGap || RunTime < lastPerfectSlow;
                if (slow)
                {
                    lastPerfectSlow = RunTime;
                    StartWorldSlow();
                }
                BroadcastFx(FxKind.PerfectParry, at, new V2(p.Id, p.Aim), p.Def.Color.WithAlpha(slow ? 1f : 0.5f), HalfArc(p), PerfectFxTime, Sfx.PerfectParry);
                return true;
            }
            p.ParryHp -= dmg;
            BroadcastFx(FxKind.Spark, at, new Rgba(0.55f, 0.9f, 1f), 9f, 0.15f, Sfx.Hit);
            if (p.ParryHp <= 0f)
            {
                p.ParryHp = 0f;
                p.ParryBroken = ParryBreakTime;
                BroadcastFx(FxKind.Shatter, p.Pos, p.Pos + V2.FromAngle(p.Aim) * (p.Def.BodyR + ParryReach * 0.55f), new Rgba(0.55f, 0.9f, 1f), HalfArc(p), ShatterTime, Sfx.Clang);
            }
            return p.Stats.MirrorChance > 0f && rng.NextDouble() < p.Stats.MirrorChance;  // Mirror Guard
        }

        public const float ParryShove = 45f;        // a melee enemy bumping the shield slides back this far
        public const float ParryStrikeShove = 120f; // a parried weapon strike (sword cut, rake, reap) throws its enemy back this far

        /// Melee enemies running into the shield hit it instead of you, and slide back (Knock.cs) ParryShove px.
        bool ParryContact(Player p, Enemy e, float dmg) => p.ParryActive && InParryArc(p, e.Pos, e.Radius) && ParryTake(p, e, dmg, ParryShove) || BulwarkMelee(p, e, dmg, false);  // (or a braced Bulwark Barrier: BulwarkBarrier.cs)

        /// A weapon strike (a sword cut, a rake, a reap) reaches past the shield's edge, so only the side counts: a
        /// shield facing the striker takes it, and the striker is thrown back ParryStrikeShove px.
        bool ParryStrike(Player p, Enemy e, float dmg) => p.ParryActive && Facing(p, e.Pos - p.Pos) && ParryTake(p, e, dmg, ParryStrikeShove) || BulwarkMelee(p, e, dmg, true);

        bool ParryTake(Player p, Enemy e, float dmg, float shove)
        {
            if (shove < ParryStrikeShove && p.ParryBumpT > 0f)
                return true;  // already took this bump (a strike lands once a swing, so it always counts)
            p.ParryBumpT = 0.25f;
            ParryHit(p, dmg, p.Pos + (e.Pos - p.Pos).Normalized() * (p.Def.BodyR + 10f));
            if (!Heavy(e))
                KnockEnemy(e, (e.Pos - p.Pos).Normalized() * shove);
            return true;
        }
    }
}
