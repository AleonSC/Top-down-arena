using UnityEngine;

namespace Arena
{
    /// The DPS passive's stacks (Momentum.cs): one amber pip per stack on a ring round the player, and a faint ring
    /// that thins out as the stacks are about to age. Full stacks pulse.
    public partial class ArenaApp
    {
        static readonly Rgba MomentumAmber = new Rgba(1f, 0.74f, 0.2f);

        void DrawMomentum(Player p, float t)
        {
            if (p.Downed)
                return;
            float r = p.Def.BodyR + 9f;
            bool full = p.MomentumStacks >= Game.MomentumMax;
            float fade = p.MomentumT < 1f ? (MathX.Fmod(t, 0.2f) < 0.1f ? 1f : 0.35f) : 1f;  // flickers for the last second
            Arc(p.Disp, r, 1.5f, MomentumAmber.WithAlpha(0.22f * fade), 32);
            for (int i = 0; i < Game.MomentumMax; i++)
            {
                float a = Mathf.PI / 2f + (i - (Game.MomentumMax - 1) / 2f) * 0.42f;
                V2 c = p.Disp + new V2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                if (i < p.MomentumStacks)
                    Circle(c, 2.6f + (full ? 0.7f * Mathf.Sin(t * 14f) : 0f), MomentumAmber.WithAlpha(fade), 8);
                else
                    Circle(c, 1.5f, MomentumAmber.WithAlpha(0.18f), 6);
            }
        }
    }
}
