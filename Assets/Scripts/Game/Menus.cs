using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Arena
{
    /// The main menu, How to play, and the pause menu and audio settings.
    public partial class ArenaApp
    {
        // The main menu is the machine (Docs/Mockups/main-menu-machines.html, design B, and disk-boot-sequence.html): the monitor with the
        // title and the floppy drive in its chin (PcMonitor.cs, DriveSlot.cs; the eject key beside the drive goes back to Who's playing?,
        // DiskEject.cs), and under it the case (MenuMachine.cs): Play, Join game, Settings and Quit as keycaps, the bay whose shutter rolls up
        // onto your saved runs (DrawPlayPicker, SavesUi.cs) or the Join address, and your profile on a small screen. Its entrance is the end
        // of the disk boot sequence (MenuIntro.cs, ProfileTransition.cs).
        const float MenuTitleY = 96f, MenuTitleSize = 74f;
        /// The bay's screen behind the shutter (the saved runs, Join): also where New run's CRT grows from and the lobby's panel grows
        /// out of (LobbyCrt.cs, LobbyEnter.cs).
        static Rect MenuCard => new Rect(BayRect.x + 10f, BayRect.y + 10f, BayRect.width - 20f, BayRect.height - 20f);
        bool menuJoinOpen;    // Join game rolled the shutter up onto the host address
        bool menuPlayOpen;    // Play rolled it up onto the saved runs (SavesUi.cs)
        float menuPlayK;      // how far the shutter is up: 0 shut, 1 open (MenuMachine.cs)
        const float MenuPlayLen = 0.9f;  // seconds for the shutter to roll up or down

        void DrawMenu()
        {
            float t = Time.unscaledTime;
            DrawMenuIntroBackdrop();  // the background fading up as the intro starts (MenuIntro.cs)
            float T = MachT;
            bool live = !MenuIntroPlaying && !profTr;  // nothing takes a click until it has all landed
            DrawMenuMachine(T, live, t);
            DrawMenuSecondary(t, live);
            DrawMenuIntroFade();  // the short version of the intro: a fade (MenuIntro.cs)
        }

        /// The machine at boot time T: the dust, the case dropping in and powering on, then the monitor over it (jolting as the case lands).
        void DrawMenuMachine(float T, bool live, float t, bool title = true)
        {
            float drop = CaseDrop(T, out float squash);
            DrawLandingDust(T);
            DrawMachineCase(T, drop, squash, 1f, live, t);
            MonitorShakeAt(T);
            DrawMonitor(title ? MenuTitleAt(T) : new MonTitle { text = CrtTitleText, typed = 0, flat = true }, DriveForMenu(), T >= 2.85f, live);  // (the eject key comes up with the disk read)
            monShakeX = monShakeY = 0f;
        }

        /// The title on the monitor at boot time T: WHO'S PLAYING? erased once the disk is in (from the picker), then VECTOR COLOSSEUM typed.
        MonTitle MenuTitleAt(float T)
        {
            if (T >= MachEnd || !(MenuIntroPlaying && MenuIntroFull) && !profTr)
                return TitleAll(CrtTitleText);
            if (profTr && T < 1.95f)
                return TitleAll(WhoTitleText);
            if (profTr && T < 2.45f)
                return new MonTitle { text = WhoTitleText, typed = Mathf.RoundToInt(WhoTitleText.Length * (1f - Seg(T, 1.95f, 2.45f))), flat = true };
            if (T < 2.6f)
                return new MonTitle { text = CrtTitleText, typed = 0, flat = true };
            float typeK = Seg(menuIntroT, CrtTypeAt, CrtTypeAt + CrtTypeLen);
            int n = menuIntroT >= CrtTypeAt + CrtTypeLen + CrtFlipLen ? -1 : Mathf.FloorToInt(typeK * CrtTitleText.Length);
            return new MonTitle { text = CrtTitleText, typed = n, flat = false };
        }

        /// The bay's insides for the moves into the lobby (LobbyEnter.cs, LobbyCrt.cs; drawn only): the saved runs or Join, if open.
        void DrawMenuCardInside(float t, bool live)
        {
            if (!live && Event.current.type != EventType.Repaint)
                return;
            if (ShutterOpen <= 0f)
                return;
            if (menuSetOpen)
                DrawBaySettings(MenuCard, false);
            else if (menuJoinOpen)
                DrawBayJoin(MenuCard, false);
            else
                DrawPlayPicker(t, false);
        }

        void JoinGame()
        {
            SaveMenuFields();
            session.Join(addressText, Port(), nameText, joinRole, weaponChoice, abilityChoice, kitChoice, Defs.MakeWild(wildSecond), ultChoice);
        }

        /// What's left outside the box: the tooltip (the profile switch is the drive's eject key now, DiskEject.cs). live = false draws it only (Repaint),
        /// for the intro and the move into the lobby.
        void DrawMenuSecondary(float t, bool live)
        {
            if (!live && Event.current.type != EventType.Repaint)
                return;
            float k = MenuIntroK(2.1f, 2.5f);
            if (k <= 0f)
                return;
            GUI.BeginGroup(new Rect(0f, 100f * (1f - EaseOut(k)), W, H));
            if (live)
                TipClear();  // the rank badge's hover card (RankUi.cs)
            if (live)
                DrawTip();
            GUI.EndGroup();
        }

        /// Your name and the port are kept between sessions (unless given on the command line).
        void LoadMenuFields()
        {
            nameText = PlayerPrefs.GetString("player_name", nameText);
            portText = PlayerPrefs.GetString("net_port", portText);
            addressText = PlayerPrefs.GetString("join_address", addressText);
        }

        void SaveMenuFields()
        {
            PlayerPrefs.SetString("player_name", nameText);
            PlayerPrefs.SetString("net_port", portText);
            PlayerPrefs.SetString("join_address", addressText);
            PlayerPrefs.Save();
            SyncProfileName();  // your name is the profile's name (ProfilesUi.cs)
        }

        /// Corner brackets just outside a panel, like the frame of a plot.
        void DrawPlotCorners(Rect r, float gap, float len, Rgba c)
        {
            UiModelAt(V2.Zero, 1f);
            float x0 = r.x - gap, x1 = r.xMax + gap, y0 = r.y - gap, y1 = r.yMax + gap;
            foreach (var corner in new[] { new V2(x0, y0), new V2(x1, y0), new V2(x0, y1), new V2(x1, y1) })
            {
                float sx = corner.X < r.center.x ? 1f : -1f, sy = corner.Y < r.center.y ? 1f : -1f;
                Line(corner, corner + new V2(sx * len, 0f), 2f, c);
                Line(corner, corner + new V2(0f, sy * len), 2f, c);
            }
        }

        static string K(KeyAction a) => Keybinds.KeyName(Keybinds.Get(a));
        static string[][] howToKeys;
        static int howToKeysVersion = -1;

        /// The keys shown beside each How to play action: yours (Settings → Keybinds).
        static string[][] HowToKeys
        {
            get
            {
                if (howToKeys == null || howToKeysVersion != Keybinds.Version)
                {
                    howToKeysVersion = Keybinds.Version;
                    howToKeys = new[]
                    {
                        new[] { K(KeyAction.MoveUp), K(KeyAction.MoveLeft), K(KeyAction.MoveDown), K(KeyAction.MoveRight) }, new[] { K(KeyAction.Fire) }, new[] { K(KeyAction.Parry) },
                        new[] { K(KeyAction.Dodge) }, new[] { K(KeyAction.Ability1) }, new[] { K(KeyAction.WeaponAction) }, new[] { K(KeyAction.Ultimate) },
                        new[] { K(KeyAction.Ability2), K(KeyAction.Ability3), K(KeyAction.Ability4) },
                        new[] { "1", "2", "3" },
                        new[] { K(KeyAction.Interact), K(KeyAction.DiscardItem) }, new[] { K(KeyAction.Ping), K(KeyAction.Emote) }, new[] { K(KeyAction.ReadyUp) }, new[] { K(KeyAction.Reroll) }, new[] { "Tab" }, new[] { K(KeyAction.AimLine), K(KeyAction.ToggleHud) },
                        new[] { "Esc" }, Keybinds.Get(KeyAction.Mute) == KeyCode.None ? new[] { K(KeyAction.Map) } : new[] { K(KeyAction.Map), K(KeyAction.Mute) }, new[] { "R", "L", "C" }, new[] { "F1" },
                    };
                }
                return howToKeys;
            }
        }
        static readonly string[,] HowToActions =
        {
            { "Move", "or the arrow keys · rebind in Settings" },
            { "Aim and shoot", "aim with the mouse, hold to fire" },
            { "Parry", "hold: a shield toward your cursor" },
            { "Dodge", "or double tap a movement key" },
            { "Role ability", "" },
            { "Weapon secondary", "blink, lunge, recall, vault or whirl" },
            { "Ultimate", "starts each run half charged" },
            { "2nd–4th abilities", "from upgrade cards · swap keys in Tab" },
            { "Pick an upgrade", "or click a card, then Confirm" },
            { "Chest item", "standing on it: take / throw away" },
            { "Ping, emotes", "tap: ping · hold: wheel · emotes" },
            { "Ready up", "between waves, once everyone is" },
            { "Reroll upgrades", "" },
            { "Stats & upgrades", "hold · just for you, doesn't pause" },
            { "Aim line, HUD", "aim line on / off · hide the HUD" },
            { "Pause menu", "stats, profile, help, settings" },
            { "Map, mute", "map: mini, full, off · mute: bind it" },
            { "After a run (host)", "R again · L lobby · C keep going" },
            { "Developer tools", "and the Boss Lab" },
        };

        /// How to play, as the side drawer (MenuDrawer.cs).

        /// The small drawing beside each How to play tip.
        void DrawTipGlyph(int i, V2 c, float t)
        {
            Circle(c, 17f, new Rgba(1f, 1f, 1f, 0.04f), 24);
            switch (i)
            {
                case 0:  // hitbox: a character with its white hitbox ring and coloured dot
                    Circle(c, 11f, new Rgba(0.36f, 0.6f, 1f), 24);
                    Circle(c, 7.5f, new Rgba(0.08f, 0.1f, 0.16f), 20);
                    Arc(c, 5.5f, 1.6f, new Rgba(1f, 1f, 1f, 0.95f), 16);
                    Circle(c, 2f + 0.5f * Mathf.Sin(t * 4f), new Rgba(0.36f, 0.6f, 1f), 12);
                    break;
                case 1:  // wrap around: out one side, in the other
                {
                    var col = new Rgba(0.6f, 0.8f, 1f, 0.9f);
                    float s = MathX.Fmod(t * 0.7f, 1f) * 24f - 12f;
                    Line(c - new V2(12f, 0f), c + new V2(12f, 0f), 1.5f, col.WithAlpha(0.35f));
                    Line(c + new V2(-12f, -9f), c + new V2(-12f, 9f), 2f, col);
                    Line(c + new V2(12f, -9f), c + new V2(12f, 9f), 2f, col);
                    Circle(c + new V2(s, 0f), 3.5f, col, 12);
                    break;
                }
                case 2:  // status icons
                    Circle(c + new V2(-7f, -6f), 4f, new Rgba(0.6f, 0.9f, 1f), 12);
                    Circle(c + new V2(7f, -6f), 4f, new Rgba(0.35f, 0.55f, 1f), 12);
                    Circle(c + new V2(-7f, 6f), 4f, new Rgba(1f, 0.55f, 0.2f), 12);
                    Circle(c + new V2(0f, 0f), 3f, new Rgba(0.85f, 0.1f, 0.2f), 10);
                    Arc(c + new V2(7f, 6f), 4f, 1.5f, new Rgba(1f, 0.3f, 0.3f), 12);
                    break;
                case 3:  // downed: a revive cross
                {
                    var g = new Rgba(0.45f, 1f, 0.55f);
                    Line(c - new V2(9f, 0f), c + new V2(9f, 0f), 5f, g);
                    Line(c - new V2(0f, 9f), c + new V2(0f, 9f), 5f, g);
                    break;
                }
                case 4:  // upgrades: a gold gem
                    Polygon(c, 11f, 4, 0f, new Rgba(1f, 0.75f, 0.25f));
                    Polygon(c, 6f, 4, 0f, new Rgba(1f, 0.92f, 0.6f));
                    break;
                case 5:  // boss shots: violet and red, dark cores
                    Circle(c + new V2(-6f, -3f), 7f, new Rgba(0.7f, 0.4f, 1f), 16);
                    Circle(c + new V2(-6f, -3f), 3f, new Rgba(0.1f, 0.05f, 0.15f), 10);
                    Circle(c + new V2(7f, 5f), 6f, new Rgba(1f, 0.3f, 0.3f), 16);
                    Circle(c + new V2(7f, 5f), 2.5f, new Rgba(0.15f, 0.03f, 0.03f), 10);
                    break;
                case 6:  // co-op: three players in their role colours
                    for (int k = 0; k < 3; k++)
                        Circle(c + V2.FromAngle(-MathX.Pi / 2f + k * MathX.Tau / 3f + t * 0.5f) * 8f, 4.5f, Defs.Roles[k].Color, 14);
                    break;
                case 7:  // perfect parry: a shield arc and a shot bouncing back off it
                {
                    var blue = new Rgba(0.55f, 0.8f, 1f);
                    float k = MathX.Fmod(t * 0.8f, 1f), x = k < 0.5f ? 12f - k * 2f * 16f : -4f + (k - 0.5f) * 2f * 16f;
                    Circle(c + new V2(-9f, 0f), 5f, new Rgba(0.36f, 0.6f, 1f), 14);
                    Arc(c + new V2(-9f, 0f), 11f, 2.5f, blue, 12, -0.9f, 1.8f);
                    Circle(c + new V2(x, 0f), 3f, k < 0.5f ? new Rgba(1f, 0.3f, 0.45f) : blue, 10);
                    break;
                }
                case 8:  // roles: the tank drawing enemies' eyes, a healer's aura, a DPS spark
                    Circle(c + new V2(0f, -7f), 5f, Defs.Roles[0].Color, 14);
                    Arc(c + new V2(0f, -7f), 8f + 1.5f * Mathf.Sin(t * 4f), 1.5f, Defs.Roles[0].Color.WithAlpha(0.6f), 16);
                    Circle(c + new V2(-8f, 7f), 4f, Defs.Roles[1].Color, 12);
                    Circle(c + new V2(8f, 7f), 4f, Defs.Roles[2].Color, 12);
                    break;
                case 9:  // health orbs: a green orb drifting in, pulsing
                {
                    var g = new Rgba(0.4f, 1f, 0.5f);
                    float s = 5f + Mathf.Sin(t * 5f);
                    Circle(c, s + 4f, g.WithAlpha(0.25f), 16);
                    Circle(c, s, g, 16);
                    Line(c - new V2(2.5f, 0f), c + new V2(2.5f, 0f), 1.5f, Rgba.White);
                    Line(c - new V2(0f, 2.5f), c + new V2(0f, 2.5f), 1.5f, Rgba.White);
                    break;
                }
                case 10:  // ability mastery: five pips filling up
                {
                    int lit = 1 + (int)MathX.Fmod(t * 1.2f, 5f);
                    for (int k = 0; k < 5; k++)
                        Circle(c + new V2(-12f + k * 6f, 0f), 2.4f, k < lit ? new Rgba(0.5f, 1f, 0.6f) : new Rgba(1f, 1f, 1f, 0.15f), 8);
                    break;
                }
                case 11:  // paths: a fork in the road
                {
                    var col = new Rgba(1f, 0.85f, 0.5f, 0.9f);
                    Line(c + new V2(0f, 11f), c + new V2(0f, 1f), 2f, col);
                    Line(c + new V2(0f, 1f), c + new V2(-9f, -9f), 2f, col);
                    Line(c + new V2(0f, 1f), c + new V2(9f, -9f), 2f, col);
                    Circle(c + new V2(-9f, -9f), 2.5f, col, 8);
                    Circle(c + new V2(9f, -9f), 2.5f, new Rgba(1f, 0.4f, 0.35f), 8);
                    break;
                }
                case 12:  // boss bodies: a big body and you bounced off it
                {
                    float k = Mathf.Abs(Mathf.Sin(t * 2f));
                    Polygon(c + new V2(-5f, 0f), 10f, 8, t * 0.4f, new Rgba(1f, 0.25f, 0.5f));
                    Circle(c + new V2(8f + k * 5f, 0f), 3.5f, new Rgba(0.36f, 0.6f, 1f), 12);
                    break;
                }
                default:  // a target
                    Arc(c, 11f, 2f, new Rgba(1f, 0.45f, 0.4f), 20);
                    Arc(c, 6f, 2f, new Rgba(1f, 0.45f, 0.4f), 16);
                    Circle(c, 2f, Rgba.White, 8);
                    break;
            }
        }

        // --- Pause menu and audio settings --------------------------------------

        void OpenPauseMenu()
        {
            pauseMenuOpen = true;
            pauseSel = 0;
            pauseSettings = pauseHowTo = pauseRecords = pauseConfirmLobby = false;
            if (game.State != MatchState.GameOver)
                game.RequestPause(true);  // pauses the run for both players
        }

        void ResumeGame()
        {
            pauseMenuOpen = false;
            pauseSettings = pauseHowTo = pauseRecords = pauseConfirmLobby = false;
            runStatsOpen = false;
            if (game.Paused)
                game.RequestPause(false);
        }

        // The pause menu is the side drawer (DrawPauseWindow, MenuDrawer.cs): Resume, Run stats, Field guide, Settings,
        // Leave game, the party, and the run's card. DrawPauseMenu (WindowAnim.cs) slides it in and out.

        /// Settings (main menu, lobby; the pause menu's opens from its drawer), as the side drawer (MenuDrawer.cs).
        void DrawSettings() { if (!SkipWhileClosing) DrawSettingsWindow(); }

        /// One volume in Settings > Audio: its name and a small line on the left, a blocky bar slider with - / + keys on the
        /// right (CrtWindows.cs). "Effects  (explosions, abilities, the menus)" splits into the name and the line.
        bool VolumeSlider(float x, ref float y, float w, string label, ref float value, Color tint)
        {
            string title = label, desc = null;
            int p = label.IndexOf("  (");
            if (p > 0 && label.EndsWith(")"))
            {
                title = label.Substring(0, p);
                desc = label.Substring(p + 3, label.Length - p - 4);
                desc = char.ToUpperInvariant(desc[0]) + desc.Substring(1);
            }
            WinLabel(new Rect(x, y + 6f, w - 396f, 48f), title, desc);
            float v = WinSlider(new Rect(x + w - 376f, y + 6f, 376f, 48f), value);
            DashLine(x, y + 60f, w);
            y += 64f;
            if (Mathf.Abs(v - value) < 0.0001f)
                return false;
            value = v;
            return true;
        }

        void LoadAudioSettings()
        {
            sounds.Master = PlayerPrefs.GetFloat("vol_master", 1f);
            sounds.Weapons = PlayerPrefs.GetFloat("vol_weapons", 1f);
            sounds.Enemies = PlayerPrefs.GetFloat("vol_enemies", 1f);
            sounds.Effects = PlayerPrefs.GetFloat("vol_effects", 1f);
            sounds.Music = PlayerPrefs.GetFloat("vol_music", 0.7f);
            sounds.Muted = PlayerPrefs.GetInt("muted", 0) == 1;
            aimLine = PlayerPrefs.GetInt("aim_line", 1) == 1;
        }

        void SaveAudioSettings()
        {
            PlayerPrefs.SetFloat("vol_master", sounds.Master);
            PlayerPrefs.SetFloat("vol_weapons", sounds.Weapons);
            PlayerPrefs.SetFloat("vol_enemies", sounds.Enemies);
            PlayerPrefs.SetFloat("vol_effects", sounds.Effects);
            PlayerPrefs.SetFloat("vol_music", sounds.Music);
            PlayerPrefs.SetInt("muted", sounds.Muted ? 1 : 0);
            PlayerPrefs.Save();
        }

        static string KitBlurb(Kit k) => Classes.Get(k).Blurb;  // KitDef.Blurb
    }
}
