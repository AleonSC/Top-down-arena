using UnityEngine;

namespace Arena
{
    /// The in-match HUD's layout in one place (the CRT console, Docs/Mockups/crt-hud-a-variants.html A1): the console along the bottom with
    /// the wave plate on its left, the five ability keys and the XP strip in the middle and your card on its right; what stacks above
    /// them (chips over the wave plate; the Riot Gear chip and teammates' rows over your card; the chat over those; the emote bar, mastery
    /// toasts and the chest hint over the keys); and the top row (boss bars, the objective line, the hint pill). The pieces that slide when
    /// the HUD hides (HudGroup, Hud.cs) pivot on these rects too, so moving a box here carries its slide along.
    /// Check a change with `Tools/Preview/uishots.sh -r HEAD hud`.
    public partial class ArenaApp
    {
        // --- The console along the bottom -------------------------------------------------------------------------------------------

        // The wave plate and your card: 229 x 72, 12 px in from the bottom corners.
        const float SideW = 229f, SideL = 12f, SideR = W - 12f - SideW, SideH = 72f, SideY = H - 12f - SideH;
        static readonly Rect HudWavePlate = new Rect(SideL, SideY, SideW, SideH);
        static readonly Rect HudMyCard = new Rect(SideR, SideY, SideW, SideH);
        /// The CRT screen under the wave plate, the keys and your card (a few px round them).
        static readonly Rect ConsoleR = new Rect(10f, SideY - 4f, W - 20f, SideH + 8f);

        // The ability keys: five 150 x 58 tiles, 8 px apart, centred, in the middle of the console's height.
        const float HudTileW = 150f, HudTileH = 58f, HudTileGap = 8f;
        const float HudBarW = HudTileW * Upgrades.SlotCount + HudTileGap * (Upgrades.SlotCount - 1);
        const float HudBarX = (W - HudBarW) / 2f, HudTileY = SideY + (SideH - HudTileH) / 2f;
        static Rect HudTile(int i) => new Rect(HudBarX + i * (HudTileW + HudTileGap), HudTileY, HudTileW, HudTileH);
        /// The XP strip over the keys (the segmented gold bar with "LV 3 · 48%").
        static readonly Rect HudXpStrip = new Rect(HudBarX, HudTileY - 30f, HudBarW, 14f);
        /// What the keys' group (the bar and the XP strip) scales round as the HUD hides and shows.
        static readonly Rect HudBarPivot = new Rect(HudBarX, SideY - 8f, HudBarW, 80f);

        // --- Above the console ------------------------------------------------------------------------------------------------------

        /// Over the keys: the mastery toasts' bottom line (they stack up), the emote bar's bottom (EmoteBar.cs), the chest hint (PropsLook.cs).
        static readonly V2 HudMasteryToastAt = new V2(HudBarX + HudBarW / 2f, HudTileY - 74f);
        static readonly V2 HudEmoteBarAt = new V2(W / 2f, HudXpStrip.y - 5f);
        static readonly Rect HudChestHint = new Rect(W / 2f - 280f, HudXpStrip.y - 25f, 560f, 26f);

        /// Over the wave plate: the path, gold, difficulty and modifier chips stack up from here, one 24 px line each.
        const float HudChipsY = SideY - 28f, HudChipLine = 24f;

        // Over your card: the Riot Gear chip (if you have it), then a slim row per teammate.
        const float HudStackY = SideY - 6f;  // the stack's bottom edge
        const float HudRiotChipW = 112f, HudRiotChipH = 38f, HudRiotChipGap = 4f, HudRiotChipAfter = 6f;  // (42 = the chip + 4 under it)
        const float HudTeammateH = 30f, HudTeammateGap = 5f;
        /// The top of that stack (where the chat box sits on): `teammates` rows, and the Riot Gear chip if `riot`.
        static float HudStackTop(int teammates, bool riot) =>
            HudStackY - (riot ? HudRiotChipH + HudRiotChipGap + HudRiotChipAfter : 0f) - teammates * (HudTeammateH + HudTeammateGap);

        // --- The top row --------------------------------------------------------------------------------------------------------------

        /// The boss bars' column (HudBossBars.cs), and what the top row's group scales round as the HUD hides.
        static readonly Rect HudTopRow = new Rect(W / 2f - 320f, 0f, 640f, 60f);
        const float HudBossBarsY = 12f;
        /// The objective line (ApproachUi.cs): at the top, or under the boss bars while a boss is up.
        const float HudObjectiveY = 14f, HudObjectiveUnderBossY = 92f;
        /// The "Hold TAB · H" hint pill (top right), and the ● REC tag under it.
        static readonly Rect HudHintPill = new Rect(W - 290f, 10f, 274f, 22f);
        static readonly Rect HudRecTag = new Rect(W - 70f, 34f, 62f, 16f);

        // --- In the middle ------------------------------------------------------------------------------------------------------------

        /// The between-waves banner (GET READY, WAVE n CLEARED when the Wave cleared box isn't up); 70 tall without a sub-line.
        static readonly Rect BannerRect = new Rect(W / 2f - 280f, H / 2f - 92f, 560f, 96f);
        /// Unlock toasts (SavesUi.cs) are centred on this line.
        const float HudUnlockToastY = 150f;
        /// "Press H to show the HUD" while it's hidden.
        const float HudHiddenHintY = H - 30f;
    }
}
