using UnityEngine;

namespace Arena
{
    /// The Dev Arena's front end (Core/DevArena.cs): starting it from the F1 dock (LABS > DEV ARENA, from the menu, the lobby or a
    /// match), routing your controls to the enemy you play as, the camera on it, and the marker and controls strip over it.
    public partial class ArenaApp
    {
        bool devArenaPending;   // asked for from outside a match: switched on once the match is running
        bool devPlayAs = true;  // the Spawn-an-enemy sheet in the Dev Arena: a click plays as the enemy (else just spawns it)
        bool possessAbilityWas, possessParryWas, possessQWas, possessEWas;

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
                possessAbilityWas = possessParryWas = possessQWas = possessEWas = false;
                return input;
            }
            if (input.AbilityQ && !possessQWas)
                game.DevAddHeroDummy(input.AimAt);  // a hero dummy at the cursor
            if (input.AbilityE && !possessEWas)
                game.DevRemoveHeroDummy(input.AimAt);
            possessQWas = input.AbilityQ;
            possessEWas = input.AbilityE;
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
            DrawHeroDummyTags();
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
            string q = Keybinds.KeyName(Keybinds.Get(KeyAction.AbilityQ)), ek = Keybinds.KeyName(Keybinds.Get(KeyAction.AbilityE));
            string move = Keybinds.KeyName(Keybinds.Get(KeyAction.MoveUp)) + Keybinds.KeyName(Keybinds.Get(KeyAction.MoveLeft))
                + Keybinds.KeyName(Keybinds.Get(KeyAction.MoveDown)) + Keybinds.KeyName(Keybinds.Get(KeyAction.MoveRight));
            var strip = new Rect(W / 2f - 470f, 72f, 940f, 34f);
            DkSub(strip);
            Ui.Label(new Rect(strip.x + 14f, strip.y, 200f, strip.height), "PLAYING AS " + e.Def.Name.ToUpperInvariant(), 13, CrtStyle.Gold, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(strip.x + 214f, strip.y, strip.width - 228f, strip.height),
                move + "  move  ·  MOUSE  its target  ·  " + fire + "  attack  ·  " + parry + "  attack now  ·  " + q + " / " + ek + "  dummy here / away  ·  " + ability + "  let go"
                + (game.PossessUnkillable ? "  ·  CAN'T DIE" : ""),
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

        /// The sheet's second Dev Arena row: hero dummies and the enemy you play not dying.
        void DrawDevArenaDummyRow(Rect row)
        {
            bool was = GUI.enabled;
            GUI.enabled = was && session.IsHost && game.HeroDummies.Count < Game.MaxHeroDummies;
            float sw = (row.width - 170f - 140f - 18f) / 2f;
            var cells = new[]
            {
                new Rect(row.x, row.y, 170f, row.height), new Rect(row.x + 176f, row.y, 140f, row.height),
                new Rect(row.x + 328f, row.y, sw, row.height), new Rect(row.x + 334f + sw, row.y, sw, row.height),
            };
            var me = game.Me;
            if (DkKey(cells[0], "ADD HERO DUMMY (" + game.HeroDummies.Count + "/" + Game.MaxHeroDummies + ")", CrtStyle.Gold, false, 11) && me != null)
                game.DevAddHeroDummy(me.Pos + V2.FromAngle(me.Aim) * 220f);
            GUI.enabled = was && session.IsHost && game.HeroDummies.Count > 0;
            if (DkKey(cells[1], "CLEAR DUMMIES", Ui.Danger, false, 11))
                game.DevRemoveHeroDummy(V2.Zero, true);
            GUI.enabled = was && session.IsHost;
            game.DummiesDrawAim = DkSwitch(cells[2], game.DummiesDrawAim, "Enemies aim at dummies");
            game.PossessUnkillable = DkSwitch(cells[3], game.PossessUnkillable, "Played enemy can't die");
            GUI.enabled = was;
        }

        static readonly Rgba DummyGold = new Rgba(1f, 0.84f, 0.42f);

        /// In the world (WorldRenderer.cs): each hero dummy as a hollow hero on a stand, flashing white when hit.
        void DrawHeroDummies(float t)
        {
            if (!session.IsHost || !game.DevArena)
                return;
            foreach (var d in game.HeroDummies)
            {
                float f = Mathf.Clamp01(d.Flash / 0.18f);
                V2 at = d.Pos;
                V2 jolt = f > 0f ? new V2(Mathf.Sin(t * 90f) * 2.5f * f, 0f) : V2.Zero;
                Arc(at, Game.HeroDummyR + 10f, 2f, new Rgba(DummyGold.R, DummyGold.G, DummyGold.B, 0.35f), 32);  // the stand
                for (int k = 0; k < 4; k++)  // tick marks round the stand
                {
                    V2 dir = V2.FromAngle(MathX.Tau * k / 4f + t * 0.6f);
                    Line(at + dir * (Game.HeroDummyR + 6f), at + dir * (Game.HeroDummyR + 15f), 2f, new Rgba(DummyGold.R, DummyGold.G, DummyGold.B, 0.6f));
                }
                Circle(at + jolt, Game.HeroDummyR, new Rgba(0.12f + 0.8f * f, 0.11f + 0.8f * f, 0.09f + 0.8f * f, 0.92f), 28);
                Arc(at + jolt, Game.HeroDummyR - 1.5f, 3f, DummyGold.Mix(new Rgba(1f, 1f, 1f), f), 28);
                Circle(at + jolt, 5f, new Rgba(1f, 0.45f, 0.35f).Mix(new Rgba(1f, 1f, 1f), f), 14);  // the bullseye
                Arc(at + jolt, 10f, 1.5f, new Rgba(1f, 0.45f, 0.35f, 0.8f), 20);
            }
        }

        /// Over each hero dummy (HUD): the last hit popping up, then its DPS, hits and total.
        void DrawHeroDummyTags()
        {
            foreach (var d in game.HeroDummies)
            {
                V2 at = ToUi(d.Pos);
                float r = (Game.HeroDummyR + 14f) * UiWorldScale;
                float f = Mathf.Clamp01(d.Flash / 0.18f);
                if (d.Hits > 0)
                {
                    var pop = Color.Lerp(new Color(1f, 0.55f, 0.45f), Color.white, f);
                    Ui.Shadowed(new Rect(at.X - 80f, at.Y - r - 26f - 6f * f, 160f, 22f), Mathf.RoundToInt(d.Last).ToString(), 16 + Mathf.RoundToInt(4f * f), pop);
                }
                string line = d.Hits == 0 ? "HERO DUMMY" : Mathf.RoundToInt(game.HeroDummyDps(d)) + " DPS  ·  " + d.Hits + " HITS  ·  " + Mathf.RoundToInt(d.Taken) + " TOTAL";
                Ui.Shadowed(new Rect(at.X - 140f, at.Y + r + 2f, 280f, 18f), line, 11, new Color(DummyGold.R, DummyGold.G, DummyGold.B, 0.85f));
            }
        }
    }
}
