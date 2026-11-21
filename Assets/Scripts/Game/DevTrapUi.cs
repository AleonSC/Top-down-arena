using UnityEngine;

namespace Arena
{
    /// Dev Kit extras (F1 > VIEW): place spike plates, slow fields and turrets by clicking the arena (right click takes one out),
    /// and the name labels overview (N): a name over every player, you included, and over every enemy.
    public partial class ArenaApp
    {
        bool devNames;
        int devTrapKind = -1;  // -1 off, else a TrapKind
        int devTrapSize = 1;   // index into Game.DevTrapSizes
        static readonly string[] DevTrapNames = { "Turret", "Spike", "Slow" };

        bool DevTrapPlacing => devTrapKind >= 0 && !devMenuOpen && session.Screen == AppScreen.Game && session.IsHost;

        void DrawDevTrapControls(DkStack col, bool inMatch)
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

        void DrawDevNames()
        {
            if (!devNames)
                return;
            foreach (var e in game.Enemies.Values)
            {
                V2 at = ToUi(e.Disp);
                Ui.Label(new Rect(at.X - 70f, at.Y - e.Radius * UiWorldScale - 20f, 140f, 14f), PracticePlan.NameOf(e.Type), 10, CrtStyle.Danger, TextAnchor.MiddleCenter, true);
            }
            var me = game.Me;
            if (me != null)
            {
                V2 at = ToUi(me.Disp);
                Ui.Label(new Rect(at.X - 100f, at.Y - me.Def.BodyR * UiWorldScale - 30f, 200f, 16f), me.Name, 12, CrtStyle.Gold, TextAnchor.MiddleCenter, true);
            }
        }
    }
}
