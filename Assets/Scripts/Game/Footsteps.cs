using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Footsteps (FootstepSoundBank.cs): every player's on-screen speed, from how far their drawn position moved this frame,
    /// sets the step rate (a stride of 40 + 0.08 × speed px, so about 4 steps a second at 240 px/s) and how loud and bright
    /// each step is. Your own steps play at full level, allies' quieter and fading out with distance. Local only, never sent.
    public partial class ArenaApp
    {
        const float StepMinSpeed = 15f, StepMaxSpeed = 600f, AllyStepGain = 0.45f, AllyStepFar = 700f;

        class StepState { public V2 Last; public bool Seen; public float Speed, Phase; public int Foot = 1; }
        readonly Dictionary<int, StepState> steps = new Dictionary<int, StepState>();

        /// Each frame (ArenaApp.Update), after the game moved everyone.
        void TickFootsteps(float dt)
        {
            var me = game.Me;
            if (me == null || dt <= 0f)
                return;
            foreach (var p in game.Players.Values)
            {
                StepState s;
                if (!steps.TryGetValue(p.Id, out s))
                    steps[p.Id] = s = new StepState();
                float raw = s.Seen ? p.Disp.DistanceTo(s.Last) / dt : 0f;
                s.Last = p.Disp;
                s.Seen = true;
                if (raw > StepMaxSpeed || p.Downed)
                    raw = 0f;  // a blink, a dodge's burst or a respawn jump isn't walking
                s.Speed += (raw - s.Speed) * (1f - Mathf.Exp(-dt * 20f));  // smooths the snapshots' uneven steps
                if (s.Speed < StepMinSpeed)
                {
                    s.Phase = 0.75f;  // the first step comes quickly when you set off
                    continue;
                }
                s.Phase += dt * s.Speed / (40f + s.Speed * 0.08f);
                if (s.Phase < 1f)
                    continue;
                s.Phase -= 1f;
                s.Foot = -s.Foot;
                float gain = p == me ? 1f : AllyStepGain * Mathf.Clamp01(1f - p.Disp.DistanceTo(me.Disp) / AllyStepFar);
                if (gain > 0f)
                    sounds.PlayFootstep(Mathf.Min(s.Speed, 360f), gain, s.Foot);
            }
        }
    }
}
