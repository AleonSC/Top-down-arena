using UnityEngine;

namespace Arena
{
    /// How the Twin Reapers' legendaries look (ReaperLoot.cs): your Twin Scythes, in Dusk's pink (the close one),
    /// Dawn's blue (the wide one) and the Dual Reaper's gold (a third), and the thrown scythe of Scythe Throw, all
    /// with the Reapers' own pointed blade.
    /// Colours from ReaperLook.cs.
    public partial class ArenaApp
    {
        /// A scythe: the Reapers' pointed blade (ReaperBodyLook.cs) from the head of a short haft, curling back and
        /// tapering to a sharp tip, with a faint trail behind it.
        void DrawScytheShape(V2 at, float aim, float size, Rgba col, float trail)
        {
            V2 dir = V2.FromAngle(aim), head = at + dir * (size * 0.35f);
            float len = size * 1.7f, rb = len * 0.46f;
            if (trail > 0f)  // round the blade's own curve (DrawReaperScythe)
                Arc(head - dir * rb, rb + size * 0.1f, size * 0.4f, col.WithAlpha(0.15f * trail), 14, aim, 1.55f);
            DrawReaperScythe(head - dir * len * 0.65f, aim, len, 1f, col, 0f);
        }

        /// A Twin Scythe: which one it is (Dusk the close one, Dawn the wide one, the gold third) by the order they came.
        void DrawTwinScythe(Minion m, float t)
        {
            int older = 0;
            foreach (var o in game.Minions.Values)
                if (o.Owner == m.Owner && o.Kind == MinionKind.Scythe && o.Id < m.Id)
                    older++;
            var col = older == 0 ? DuskCol : older == 1 ? DawnCol : DualGold;
            DrawScytheShape(m.Disp, m.Aim, 22f, col, 1f);
        }

        /// The thrown scythe: big, spinning, with a soft blur round it.
        void DrawThrownScythe(Minion m, float t)
        {
            float spin = t * 18f + m.Id;
            Circle(m.Disp, 30f, new Rgba(0.85f, 0.75f, 1f, 0.12f), 20);
            for (int k = 0; k < 2; k++)
                DrawScytheShape(m.Disp, spin + MathX.Pi * k, 34f, k == 0 ? DuskCol : DawnCol, 0.8f);
        }
    }
}
