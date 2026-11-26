using System;
using System.Collections.Generic;

namespace Arena
{
    /// Stampede, the Tank Summoner's 4th choice (`AbilityId.Stampede`, `AbilityDef.Fourth`: the lobby's 4th choice), from
    /// Docs/Mockups/stampede.html. A gate of light opens StampedeAhead px ahead of you across your aim (StampedeWind s), and a
    /// line of StampedeBeasts spirit beasts (a mixed herd of bison, boars and stags) charges out of it StampedeReach px at
    /// StampedeSpeed px/s, then fades (StampedeFade s):
    ///   - every enemy the line meets takes StampedeDamage (once per charge), bosses and Heavy enemies too;
    ///   - enemies are shoved along ahead of the line, held against the beasts' heads (not Heavy enemies or bosses: they are
    ///     only hurt, and the herd runs over them);
    ///   - an enemy driven into a wall or a prop takes StampedeSlam more and is stunned StampedeStun s (once per charge).
    /// It doesn't block bullets (that is Golem Wall's job). Mastered: StampedeCapBeasts beasts (a 272 px front), and after
    /// StampedeTurn s the herd turns and charges back (a second trample and slam for everything it meets).
    /// Its Rare card Thundering Herd adds Stats.StampedeDmgMult.
    /// It is one `ZoneKind.Stampede` zone: Pos is the middle of the line (it moves), Aim the heading (it flips on the way back),
    /// Radius half the line's length (the beast count is read from it), Marched the px walked from the gate on this charge;
    /// replicated, so every screen draws the same herd from Pos, Aim, Marched and its age (Game/StampedeLook.cs).
    public partial class Game
    {
        public const float StampedeCooldown = 14f;
        public const int StampedeBeasts = 7, StampedeCapBeasts = 9;
        public const float StampedeSpacing = 34f;       // between beasts: 7 make a 204 px front
        public const float StampedeAhead = 60f;         // the gate opens this far ahead of you
        public const float StampedeWind = 0.35f;       // the gate opening
        public const float StampedeReach = 320f, StampedeSpeed = 560f, StampedeTurn = 0.25f, StampedeFade = 0.6f;
        public const float StampedeDamage = 20f, StampedeSlam = 15f, StampedeStun = 0.5f;
        public const float StampedeFace = 30f;          // the beasts' heads, ahead of Pos (an enemy is held this far plus its radius out)
        public const float StampedeBack = 22f;          // and their tails, behind it (an enemy this far behind is already run over)
        public const float StampedeHalfWidth = 17f;     // a beast's half width, beyond the end of the line
        const float StampedePinned = 6f;                // an enemy the wall or a prop kept this far from its place is pinned

        /// Seconds since this herd was called.
        public static float StampedeAge(Zone z) => z.Duration - z.T;

        /// How many beasts the line has (its Radius is half the length).
        public static int StampedeCount(Zone z) => (int)Math.Round(z.Radius * 2f / StampedeSpacing) + 1;

        /// Does it turn and charge back (the mastered herd)? Read from its size.
        public static bool StampedeBack2(Zone z) => StampedeCount(z) > StampedeBeasts;

        /// Seconds from the gate opening to the last beast stopping.
        public static float StampedeRun(bool cap) => StampedeReach / StampedeSpeed;
        public static float StampedeCharges(bool cap) => cap ? 2f * StampedeRun(cap) + StampedeTurn : StampedeRun(cap);
        public static float StampedeLife(bool cap) => StampedeWind + StampedeCharges(cap) + StampedeFade;

        /// Where the herd is at this age: charge 0 outbound, 1 the way back, -1 not running (the gate opening, the turn, the fade);
        /// dist = px from the gate along the outbound heading.
        public static int StampedeLeg(float age, bool cap, out float dist)
        {
            float t = age - StampedeWind, run = StampedeRun(cap);
            dist = 0f;
            if (t < 0f)
                return -1;
            if (t < run)
            {
                dist = t * StampedeSpeed;
                return 0;
            }
            dist = StampedeReach;
            if (!cap || t < run + StampedeTurn)
                return -1;
            if (t < 2f * run + StampedeTurn)
            {
                dist = StampedeReach - (t - run - StampedeTurn) * StampedeSpeed;
                return 1;
            }
            dist = 0f;
            return -1;
        }

        /// How far the line has fully faded: 1 while it runs, easing to 0 over StampedeFade s after the last beast stops.
        public static float StampedeAlpha(Zone z)
        {
            bool cap = StampedeBack2(z);
            float after = StampedeAge(z) - StampedeWind - StampedeCharges(cap);
            return after <= 0f ? 1f : Math.Max(0f, 1f - after / StampedeFade);
        }

        void CastStampede(Player p, V2 target, bool cap)
        {
            V2 off = target - p.Pos;
            float aim = off.LengthSq > 1f ? off.Angle : p.Aim;
            int n = cap ? StampedeCapBeasts : StampedeBeasts;
            foreach (var old in Zones.Values)
                if (old.Kind == ZoneKind.Stampede && old.Owner == p.Id)
                    old.T = Math.Min(old.T, 0.001f);  // (a new herd replaces the last)
            V2 gate = ClampToArena(p.Pos + V2.FromAngle(aim) * StampedeAhead, 20f);
            var z = AddZone(ZoneKind.Stampede, p, gate, StampedeSpacing * (n - 1) / 2f, StampedeLife(cap));
            z.Aim = aim;
            z.Heading = aim;
            z.Origin = gate;
            z.Trampled = new HashSet<int>();
            z.Slammed = new HashSet<int>();
            BroadcastFx(FxKind.Ring, gate, new Rgba(0.5f, 0.88f, 0.67f), StampedeSpacing * (n - 1) / 2f, 0.45f, Sfx.Summon);
        }

        /// Host, every tick, for each Stampede (TickZones): walk the line, trample and shove what it meets.
        void TickStampede(Zone z)
        {
            if (z.Trampled == null)
                return;
            Player owner;
            if (!Battle || !Players.TryGetValue(z.Owner, out owner))
            {
                z.T = Math.Min(z.T, 0.001f);  // the wave is over
                return;
            }
            bool cap = StampedeBack2(z);
            float dist;
            int leg = StampedeLeg(StampedeAge(z), cap, out dist);
            if (leg != z.Leg && leg >= 0)
            {
                z.Leg = leg;
                z.Trampled.Clear();  // the way back tramples again
                z.Slammed.Clear();
            }
            float heading = leg == 1 ? z.Heading + MathX.Tau / 2f : z.Heading;
            z.Aim = heading;
            z.Marched = dist;
            z.Pos = z.Origin + V2.FromAngle(z.Heading) * dist;
            if (leg < 0)
                return;  // the gate opening, turning or fading: nothing is hit
            V2 dir = V2.FromAngle(heading), perp = new V2(-dir.Y, dir.X);
            float half = z.Radius + StampedeHalfWidth, dmg = StampedeDamage * z.DmgMult * owner.Stats.StampedeDmgMult;
            foreach (var e in AllFoes(by: z.Owner))
            {
                V2 rel = e.Pos - z.Pos;
                float along = rel.Dot(dir), side = rel.Dot(perp);
                if (Math.Abs(side) > half + e.Radius * 0.5f || along > StampedeFace + e.Radius + 1f || along < -StampedeBack - e.Radius)
                    continue;
                if (z.Trampled.Add(e.Id))
                {
                    HurtEnemy(e, dmg, z.Owner);
                    BroadcastFx(FxKind.Spark, e.Pos, e.Pos, new Rgba(0.5f, 0.88f, 0.67f), e.Radius, 0.25f, Sfx.Hit);
                }
                if (Heavy(e))
                    continue;  // run over, not carried
                V2 want = z.Pos + dir * (StampedeFace + e.Radius) + perp * side;
                V2 put = PushOutOfProps(ClampToArena(want, e.Radius), e.Radius);
                e.Pos = put;
                e.KnockLeft = V2.Zero;
                if (put.FartherThan(want, StampedePinned) && z.Slammed.Add(e.Id))  // pinned against a wall or a prop
                {
                    HurtEnemy(e, StampedeSlam * z.DmgMult * owner.Stats.StampedeDmgMult, z.Owner);
                    Stun(e, StampedeStun);
                    BroadcastFx(FxKind.Spark, put, put, Rgba.Gold, e.Radius + 6f, 0.35f, Sfx.Hit);
                }
            }
        }
    }
}
