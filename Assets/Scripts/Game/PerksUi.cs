using UnityEngine;

namespace Arena
{
    /// The lobby's Perks tab (design: Docs/BossHuntsAndPerks.md §2, mockup: Docs/Mockups/lobby-perks.html, option A).
    /// Perks aren't built yet, so every slot and perk is a locked placeholder; hovering one shows "Coming soon".
    public partial class ArenaApp
    {
        const int PerkTab = 5;
        const int PerkSlots = 3;       // the most a player can unlock
        const int PerkPlaceholders = 14;  // the planned perks (two per early boss, one per later one)

        void DrawPerks(Rect r, float t)
        {
            Ui.BodyPanel(r);
            float lx = r.x + 18f, lw = 236f;
            Ui.Heading(lx, r.y + 14f, lw, "Equipped");
            for (int i = 0; i < PerkSlots; i++)
                LockedSlot(new Rect(lx, r.y + 42f + i * 86f, lw, 78f));
            Ui.Label(new Rect(lx, r.y + 42f + PerkSlots * 86f + 4f, lw, 80f),
                "Perks bend the rules for a run: one per boss, a few slots. Win a Boss Hunt to unlock that boss's perks.", 12, Ui.Muted, TextAnchor.UpperLeft, false, true);

            float x = r.x + 274f, w = r.xMax - 18f - x;
            Ui.Heading(x, r.y + 14f, w, "Perks");
            const int cols = 2, rows = PerkPlaceholders / cols;
            const float gap = 8f;
            float top = r.y + 42f, cw = (w - gap) / cols, ch = (r.yMax - 18f - top - gap * (rows - 1)) / rows;
            for (int i = 0; i < PerkPlaceholders; i++)
                LockedSlot(new Rect(x + (i % cols) * (cw + gap), top + (i / cols) * (ch + gap), cw, ch));
        }
    }
}
