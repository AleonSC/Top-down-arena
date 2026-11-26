using System;

namespace Arena
{
    /// Brittle (the earth status; enemies only): an earth hit (Stonespear's spears, the Earthball and its rubble, Meteor's
    /// blast, Stone Colossus's rise, stomps, slams and boulders) cracks what it hits for BrittleTime s (refreshed, never
    /// stacked). A brittle enemy takes BrittleMult x damage (bosses BrittleBossMult x). Brittle AND frozen (real ice: a third
    /// chill, Frost Nova, Glacial Tomb, Blood Freeze...), it shatters: the ice and the cracks are both used up, it takes
    /// ShatterDmg (+ShatterDmgWave a wave) and enemies within ShatterReach take ShatterSplash of that. Credited to whoever
    /// cracked it. Enemy.Brittle is replicated for the icon and the cracks on its body (Game/ElementStatusLook.cs).
    public partial class Game
    {
        public const float BrittleTime = 5f, BrittleMult = 1.2f, BrittleBossMult = 1.1f;
        public const float ShatterDmg = 60f, ShatterDmgWave = 3f, ShatterReach = 90f, ShatterSplash = 0.5f;
        public static readonly Rgba BrittleColor = new Rgba(0.85f, 0.62f, 0.35f);

        /// Host: an earth hit cracks e (not a PvP player's stand-in).
        internal void Embrittle(Enemy e, int owner, float t = BrittleTime)
        {
            if (e == null || e.Stand != null || e.Hp <= 0f || e.SpawnT > 0f)
                return;
            e.BrittleT = Math.Max(e.BrittleT, t);
            e.BrittleBy = owner;
        }

        /// The damage multiplier Brittle puts on a hit (HurtEnemy).
        static float BrittleDmgMult(Enemy e) => e.BrittleT <= 0f ? 1f : e.Def.IsBoss ? BrittleBossMult : BrittleMult;

        /// Host, every tick per enemy: the cracks wear off; brittle and frozen at once, it shatters.
        void TickBrittle(Enemy e, float dt)
        {
            if (e.BrittleT <= 0f)
                return;
            e.BrittleT = Math.Max(0f, e.BrittleT - dt);
            if (e.BrittleT > 0f && IceFrozen(e) && e.Stand == null)
                Shatter(e);
        }

        /// A brittle, frozen enemy breaks: the ice goes, a burst of damage on it and a share on its neighbours.
        void Shatter(Enemy e)
        {
            int by = e.BrittleBy;
            e.BrittleT = 0f;
            e.FrozenT = e.StunT;  // the ice breaks (a stun underneath still holds it)
            e.FrostChill = 0;
            float dmg = ShatterDmg + ShatterDmgWave * Wave;
            BroadcastFx(FxKind.FrostBurst, e.Pos, BrittleColor, 1f, 0.9f, Sfx.FrostShatter);  // the Frost Lance's shatter (FrostLanceLook.cs)
            foreach (var o in FoesInCircle(e.Pos, ShatterReach, by: by))
                if (o != e && o.SpawnT <= 0f)
                    HurtEnemy(o, dmg * ShatterSplash, by);
            HurtEnemy(e, dmg, by);
        }
    }
}
