using System;

namespace Arena
{
    /// The melee grunts (built from Docs/Mockups/melee-grunts.html):
    ///   Buzzsaw (from wave 5): a ring of spinning spikes. It steers with momentum, so it swoops and overshoots, and its touches
    ///     come about eight times a second for 4 each (a Buzzsaw cuts your hit grace short, `SawGrace`, so they can).
    ///   Swarmer: now zig-zags toward you, 40 to 65 degrees off line, flipping side every 0.2 to 0.45 s; the swing fades as it
    ///     gets close. It has a long thin spike on each face.
    ///   Spike Block (from wave 5): rocks from side to side as it closes in, then at about 140 px stops and winds up for 0.55 s
    ///     (a red line shows where, locked when the wind-up starts), lunges 0.26 s for 35 damage and rests for 0.8 s.
    /// Phase: Swarmer 1 / 2 is the side it is swinging to; Spike Block 0 approach, 1 wind up, 2 lunge, 3 rest.
    public partial class Game
    {
        public const float SawTurn = 3.2f;           // how quickly a Buzzsaw's heading follows you (bigger: tighter turns)
        public const float SawGrace = 0.12f;         // after a Buzzsaw touch you can be touched again this soon
        public const float SpikeLungeDamage = 35f;
        public const float SpikeLungeSpeed = 650f;
        public const float SpikeWindUp = 0.55f, SpikeLunge = 0.26f, SpikeRest = 0.8f, SpikeTrigger = 140f;

        /// A Swarmer's heading: toward you, swung to one side and then the other. Facing follows its heading (it points where it goes).
        V2 SwarmerMove(Enemy e, V2 dir, float dist, float dt)
        {
            e.PhaseT -= dt;
            if (e.PhaseT <= 0f)
            {
                e.Phase = (byte)(e.Phase == 1 ? 2 : 1);
                e.PhaseT = Rand(0.2f, 0.45f);
                e.T2 = Rand(0.7f, 1.13f);  // the swing, in radians
            }
            if (dist < 0.01f)
                return V2.Zero;
            float fade = Math.Max(0.2f, Math.Min(1f, dist / 150f));
            float ang = dir.Angle + (e.Phase == 1 ? 1f : -1f) * e.T2 * fade;
            e.Facing = ang;
            return V2.FromAngle(ang);
        }

        /// A Spike Block's lunge: approach, wind up (line locked), leap, rest.
        void SpikeBlockSpecial(Enemy e, V2 tpos, float dt)
        {
            float dist = tpos.DistanceTo(e.Pos);
            e.PhaseT -= dt;
            if (e.Phase == 0 && dist < SpikeTrigger && e.PhaseT <= 0f)
            {
                e.Phase = 1;
                e.PhaseT = SpikeWindUp;
                e.Facing = (tpos - e.Pos).Angle;  // Face() already pointed it at you; it keeps this line from now on
            }
            else if (e.Phase == 1 && e.PhaseT <= 0f)
            {
                e.Phase = 2;
                e.PhaseT = SpikeLunge;
            }
            else if (e.Phase == 2 && e.PhaseT <= 0f)
            {
                e.Phase = 3;
                e.PhaseT = SpikeRest;
            }
            else if (e.Phase == 3 && e.PhaseT <= 0f)
            {
                e.Phase = 0;
                e.PhaseT = 0.5f;
            }
        }
    }
}
