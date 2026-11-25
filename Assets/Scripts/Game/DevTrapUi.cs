using UnityEngine;

namespace Arena
{
    /// Dev Kit traps (F1 > VIEW): place spike plates, slow fields and turrets by clicking the arena (right click takes one out).
    public partial class ArenaApp
    {
        int devTrapKind = -1;  // -1 off, else a TrapKind
        int devTrapSize = 1;   // index into Game.DevTrapSizes
        static readonly string[] DevTrapNames = { "Turret", "Spike", "Slow" };

        bool DevTrapPlacing => devTrapKind >= 0 && !devMenuOpen && session.Screen == AppScreen.Game && session.IsHost;

        void DrawDevTrapControls(ref DkStack col)
        {
            Ui.Label(col.Next(18f), "Place traps (click the arena, right click removes)", 10, CrtStyle.Desc, TextAnchor.MiddleLeft, true);
            var row = DkStack.Cells(col.Next(26f), 4, 4f);
            if (DkChoice(row[0], "OFF", devTrapKind < 0, CrtStyle.Gold))
                devTrapKind = -1;
            for (int i = 0; i < 3; i++)
                if (DkChoice(row[i + 1], DevTrapNames[i], devTrapKind == i, CrtStyle.Gold))
                {
                    devTrapKind = i;
                    devMenuOpen = false;  // out of the way, so you can click the arena
                }
            var sz = DkStack.Cells(col.Next(26f), 4, 4f);
            string[] sn = { "SMALL", "MEDIUM", "BIG" };
            for (int i = 0; i < 3; i++)
                if (DkChoice(sz[i], sn[i], devTrapSize == i, CrtStyle.Gold))
                    devTrapSize = i;
            bool was = GUI.enabled;
            GUI.enabled = was && game.DevTrapCount > 0 && session.IsHost;
            if (DkKey(sz[3], "CLEAR " + game.DevTrapCount, CrtStyle.Danger, false, 10))
                game.DevClearTraps();
            GUI.enabled = was;
        }

        void TickDevTrapClicks()
        {
            if (!DevTrapPlacing || GUIUtility.hotControl != 0)
                return;
            if (Input.GetMouseButtonDown(0))
                game.DevPlaceTrap((TrapKind)devTrapKind, MouseArena(), Game.DevTrapSizes[devTrapSize]);
            else if (Input.GetMouseButtonDown(1))
                game.DevRemoveTrapAt(MouseArena());
        }
    }
}
