using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The overhead stack: what sits over a player's head, one row above another and clear of the body. From the bottom:
    /// the health bar (with the wound and Blood Debt on it), then whichever extra bars are up (an Armor Drop vest, the
    /// Juggernaut Suit, A Literal Tank, Iron Armadillo), then the name (Hud.cs). The stack starts above the outermost
    /// ring round the body (`OverheadClear`: Ironhide, Blood Debt, Cavalry Call, a Juggernaut's swell, a tank's hull,
    /// a rolling Armadillo), so nothing in it covers the player. Each bar asks for its row with `OverheadRow`.
    ///
    /// Settings > Gameplay > Health bars: which bodies show a health bar (you, teammates, enemies); all on by default.
    public partial class ArenaApp
    {
        const float OverheadGap = 5f;     // between the body's outer ring and the first row
        const float OverheadRowGap = 3f;  // between rows
        float ohCursor;                   // the current player's next free row (a y offset from its centre, going up)
        readonly Dictionary<int, float> ohTop = new Dictionary<int, float>();  // player id -> the stack's top last frame (for the name)

        /// How far over a player's centre its body and the rings round it reach.
        float OverheadClear(Player p)
        {
            float r = p.Def.BodyR;
            float clear = r * (Game.JuggOn(p) ? 1f + JuggSwellBy : 1f) + 1f;
            if (p.Downed)
                return r + 10f;  // the revive ring
            if (Game.Ironhide(p) > 0.01f)
                clear = Mathf.Max(clear, r + 15f);  // IronhideLook.cs: its plates at r + 13
            if (p.DebtT > 0f)
                clear = Mathf.Max(clear, r + 11f);  // BloodDebtLook.cs: the window ring at r + 9
            if (p.UnbreakT > 0f)
                clear = Mathf.Max(clear, r + 13f);  // Cavalry Call's octagon (WorldPlayers.cs)
            if (p.SallyT > 0f)
                clear = Mathf.Max(clear, r + 7f);   // Sally Forth's ring (TrojanHorseLook.cs)
            if (Game.TankOn(p))
                clear = Mathf.Max(clear, r + 40f);  // the tank's hull (LiteralTankLook.cs)
            if (Game.ArmRolling(p))
                clear = Mathf.Max(clear, Game.ArmR + 8f);  // the Armadillo's ball (IronArmadilloLook.cs)
            return clear;
        }

        /// Start a player's stack (WorldPlayers.cs, before its bars).
        void BeginOverhead(Player p) => ohCursor = -OverheadClear(p) - OverheadGap;

        /// The next row up, `h` tall: its top's y offset from the player's centre.
        float OverheadRow(float h)
        {
            float top = ohCursor - h;
            ohCursor = top - OverheadRowGap;
            return top;
        }

        /// End a player's stack: keep its top for the name over it.
        void EndOverhead(Player p) => ohTop[p.Id] = ohCursor + OverheadRowGap;

        /// The stack's top over a player (a negative y offset), or just over the body if it hasn't been drawn yet.
        float OverheadTop(Player p) => ohTop.TryGetValue(p.Id, out float y) ? y : -OverheadClear(p) - OverheadGap;

        // --- Settings > Gameplay > Health bars ----------------------------------------------------------------------

        const int HpBarsYou = 1, HpBarsMates = 2, HpBarsEnemies = 4;
        int hpBars = HpBarsYou | HpBarsMates | HpBarsEnemies;
        void LoadHpBars() => hpBars = PlayerPrefs.GetInt("hp_bars", HpBarsYou | HpBarsMates | HpBarsEnemies);

        void SetHpBars(int mask)
        {
            hpBars = mask;
            PlayerPrefs.SetInt("hp_bars", mask);
            PlayerPrefs.Save();
        }

        bool HpBarOn(Player p) => (hpBars & (p.Id == game.MyId ? HpBarsYou : HpBarsMates)) != 0;
        bool EnemyHpBarsOn => (hpBars & HpBarsEnemies) != 0;

        static readonly string[] HpBarNames = { "You", "Teammates", "Enemies" };
    }
}
