using UnityEngine;

namespace Arena
{
    /// How the Twin Reapers' legendaries look (ReaperLoot.cs): your Twin Scythes, in Dusk's pink (the close one),
    /// Dawn's blue (the wide one) and the Dual Reaper's gold (a third), and the thrown scythe of Scythe Throw.
    /// Colours from ReaperLook.cs.
    public partial class ArenaApp
    {
        /// A scythe: a curved blade swept back from the head of a short haft, with a faint trail behind the blade.
        void DrawScytheShape(V2 at, float aim, float size, Rgba col, float trail)
        {
            V2 dir = V2.FromAngle(aim);
            V2 head = at + dir * (size * 0.35f), foot = at - dir * (size * 0.6f);
            Line(foot, head, Mathf.Max(2f, size * 0.12f), new Rgba(0.55f, 0.45f, 0.4f));  // the haft
            if (trail > 0f)
                Arc(head, size * 0.9f, size * 0.35f, col.WithAlpha(0.15f * trail), 14, aim + MathX.Pi * 0.5f, 1.6f);
            Arc(head, size * 0.75f, Mathf.Max(3f, size * 0.22f), col, 14, aim + MathX.Pi * 0.5f, 1.7f);  // the blade, curving back
            Arc(head, size * 0.75f + size * 0.1f, 1.5f, new Rgba(1f, 1f, 1f, 0.7f), 14, aim + MathX.Pi * 0.55f, 1.5f);  // its edge glint
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
