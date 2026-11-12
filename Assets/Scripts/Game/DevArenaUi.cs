using UnityEngine;

namespace Arena
{
    /// The Dev Arena's front end (Core/DevArena.cs): starting it from the F1 dock (LABS > DEV ARENA, from the menu, the lobby or a
    /// match), routing your controls to the enemy you play as, the camera on it, and the marker and controls strip over it.
    public partial class ArenaApp
    {
        bool devArenaPending;   // asked for from outside a match: switched on once the match is running
        bool devPlayAs = true;  // the Spawn-an-enemy sheet in the Dev Arena: a click plays as the enemy (else just spawns it)
        bool possessAbilityWas, possessParryWas;

        /// LABS > DEV ARENA: on or off in a match; from the menu or the lobby, host a Practice Range match and turn it on there.
        void ToggleDevArena()
        {
            if (session.Screen == AppScreen.Game)
            {
                if (!session.IsHost)
                    return;
                bool on = !game.DevArena;
                game.DevSetArena(on);
                if (on)
                    OpenDevArenaSheet();
                return;
            }
            if (session.Screen == AppScreen.Menu)
                HostGame();
            if (!session.IsHost)
                return;
            session.SetMode(true, false);
            int auto = session.AutoStart;
            session.AutoStart = 1;  // start now, without waiting for Ready
            session.StartMatch();
            session.AutoStart = auto;
            devArenaPending = true;
        }

        void OpenDevArenaSheet()
        {
            devMenuOpen = true;
            devSheet = 5;
            devDrawer = 0;
            devPlayAs = true;
        }

        /// FixedUpdate: while you play an enemy, your keys drive it and your own hero stands still. Returns your hero's input.
        PlayerInput PossessStep(PlayerInput input)
        {
            if (devArenaPending && session.IsHost && game.Running && game.State != MatchState.Idle)
            {
                devArenaPending = false;
                game.DevSetArena(true);
                OpenDevArenaSheet();
            }
            if (!session.IsHost || !game.DevArena || game.Possessed == null)
            {
                possessAbilityWas = possessParryWas = false;
                return input;
            }
            bool letGo = input.Ability && !possessAbilityWas;
            bool now = input.Parry && !possessParryWas;
            possessAbilityWas = input.Ability;
            possessParryWas = input.Parry;
            if (letGo)
            {
                game.DevRelease();
                return new PlayerInput { AimAt = input.AimAt };
            }
            game.SetPossessInput(input.Move, input.AimAt, input.Fire, now);
            return new PlayerInput { AimAt = input.AimAt };  // your hero: no moving, shooting or casting
        }

        /// The camera follows the enemy you play (CameraZoom.cs).
        V2 CamSubject(Player me)
        {
            var e = session.IsHost && game.DevArena ? game.Possessed : null;
            return e != null ? e.Disp : me.Disp;
        }

        /// Over the world (Hud.cs): brackets round the enemy you play, its name, and the controls strip.
        void DrawPossessHud()
        {
            if (!session.IsHost || !game.DevArena)
                return;
            var e = game.Possessed;
            if (e == null)
            {
                if (!devMenuOpen)
                    Ui.Shadowed(new Rect(W / 2f - 300f, 74f, 600f, 22f), "DEV ARENA  ·  F1 > SPAWN AN ENEMY to play as one", 13, new Color(CrtStyle.Gold.r, CrtStyle.Gold.g, CrtStyle.Gold.b, 0.75f));
                return;
            }
            V2 at = ToUi(e.Disp);
            float r = e.Radius * UiWorldScale + 8f;
            float pulse = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 6f);
            var c = new Color(CrtStyle.Gold.r, CrtStyle.Gold.g, CrtStyle.Gold.b, pulse);
            float arm = Mathf.Max(6f, r * 0.45f);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    float cx = at.X + sx * r, cy = at.Y + sy * r;
                    Ui.Fill(new Rect(sx < 0 ? cx : cx - arm, cy - 1f, arm, 2f), c);
                    Ui.Fill(new Rect(cx - 1f, sy < 0 ? cy : cy - arm, 2f, arm), c);
                }
            Ui.Shadowed(new Rect(at.X - 160f, at.Y - r - 26f, 320f, 20f), "YOU  ·  " + e.Def.Name.ToUpperInvariant(), 13, c);

            if (devMenuOpen)
                return;  // (the dock is over the top of the screen)
            string fire = Keybinds.KeyName(Keybinds.Get(KeyAction.Fire)), parry = Keybinds.KeyName(Keybinds.Get(KeyAction.Parry));
            string ability = Keybinds.KeyName(Keybinds.Get(KeyAction.Ability));
            string move = Keybinds.KeyName(Keybinds.Get(KeyAction.MoveUp)) + Keybinds.KeyName(Keybinds.Get(KeyAction.MoveLeft))
                + Keybinds.KeyName(Keybinds.Get(KeyAction.MoveDown)) + Keybinds.KeyName(Keybinds.Get(KeyAction.MoveRight));
            var strip = new Rect(W / 2f - 380f, 72f, 760f, 34f);
            DkSub(strip);
            Ui.Label(new Rect(strip.x + 14f, strip.y, 200f, strip.height), "PLAYING AS " + e.Def.Name.ToUpperInvariant(), 13, CrtStyle.Gold, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(strip.x + 214f, strip.y, strip.width - 228f, strip.height),
                move + "  move   ·   MOUSE  its target   ·   " + fire + "  attack   ·   " + parry + "  attack now   ·   " + ability + "  let go",
                11, CrtStyle.Body, TextAnchor.MiddleRight, true);
        }

        /// The Spawn-an-enemy sheet in the Dev Arena: Spawn / Play as, and the arena's own keys. Returns the height used.
        float DrawDevArenaRow(Rect row)
        {
            bool was = GUI.enabled;
            GUI.enabled = was && session.IsHost;
            var cells = DkStack.Cells(row, 5, 6f);
            if (DkChoice(cells[0], "CLICK: SPAWN", !devPlayAs, CrtStyle.Head))
                devPlayAs = false;
            if (DkChoice(cells[1], "CLICK: PLAY AS", devPlayAs, CrtStyle.Gold))
                devPlayAs = true;
            GUI.enabled = was && session.IsHost && game.Possessed != null;
            if (DkKey(cells[2], "LET GO", Ui.Neutral, false, 12))
                game.DevRelease();
            GUI.enabled = was && session.IsHost;
            if (DkKey(cells[3], "CLEAR ARENA", Ui.Danger, false, 12))
                game.DevClearArena();
            if (DkKey(cells[4], "LEAVE DEV ARENA", Ui.Neutral, false, 12))
            {
                game.DevSetArena(false);
                devSheet = 0;
            }
            GUI.enabled = was;
            return row.height;
        }
    }
}
