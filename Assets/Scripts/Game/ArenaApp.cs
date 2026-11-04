using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Arena
{
    /// Unity front end: reads input, steps the Session/Game, draws the arena
    /// with GL and the menus/HUD with IMGUI. Creates itself when any scene
    /// starts, so there is nothing to set up in the editor.
    public partial class ArenaApp : MonoBehaviour
    {
        // Menus and the HUD are laid out on a fixed 1280x720 canvas; the arena itself is bigger
        // and is scaled down to fill the same area.
        const float W = 1280f, H = 720f;
        const float AW = Game.Width, AH = Game.Height;
        const float WorldToUi = W / AW;
        const float ArenaBgDim = 0.72f;  // how much of the lobby background the arena's dark floor hides

        static bool booted;

        Session session;
        Game game;
        string nameText = "Player";
        string addressText = "127.0.0.1";
        string portText = Session.DefaultPort.ToString();
        Role hostRole = Role.Tank;
        Role? joinRole;
        int weaponChoice, abilityChoice;
        Kit kitChoice = Kit.Soldier;
        bool pauseMenuOpen, settingsOpen, howToOpen, wasPaused;
        bool tabPaused;  // solo: holding Tab paused the match (released, it resumes)
        int tabScroll; // Tab screen: rows scrolled in the upgrade list
        bool fireOnMenu, parryOnMenu, fireBlocked, parryBlocked, mouseBlocked;  // a mouse press that belongs to a menu, not the game
        bool recordSessions;             // dev: record each match for balancing (Settings or --record)
        bool aimDebug;                   // F3: lines for where you're aiming and where your last shot went
        bool devKillAllKey;              // F1 dev tool: X kills every enemy on the field (host only; saved)
        bool devPauseKey;                // F1 dev tool: P freezes the match with no menu, for screenshots (saved)
        /// The frame time for things the drawing moves by itself (sweeping lasers, spark rates): zero while P has frozen the match.
        float ViewDt => devFrozen ? 0f : Time.deltaTime;
        bool devFrozen, devFrozenAck;    // P has frozen the match (Ack: the pause has come back from the host, so a later resume is someone else's)
        bool aimLine = true;             // V: a soft line from your gun to the crosshair, at most your weapon's range (saved per player)

        void SetAimLine(bool on)
        {
            aimLine = on;
            PlayerPrefs.SetInt("aim_line", on ? 1 : 0);
            PlayerPrefs.Save();
        }

        static string SessionsFolder => System.IO.Path.Combine(Application.persistentDataPath, "sessions");

        /// Recording happens on the host (which decides every hit), so this only matters when you host.
        void ApplyRecording() => game.RecordFolder = recordSessions ? SessionsFolder : null;
        bool tabHeld;  // Tab: your stats and loadout, shown only on this screen (the game keeps running)
        int selectedCard = -1;  // upgrade card highlighted, waiting for Confirm
        int pickKey;            // for a card that grants an ability: the key to put it on (1 = Q, 2 = E, 3 = Shift)
        string shownOffer = "";
        float offerShownAt = -99f;     // when the current cards appeared (unscaled time)
        const float RerollLock = 0.6f;  // the reroll stays locked this long after new cards appear, so a spam-click can't burn rerolls

        /// The reroll can't be used for a moment after the window opens or new cards come in.
        bool RerollLocked => Time.unscaledTime - offerShownAt < RerollLock;
        readonly bool[] wantAbility = new bool[Upgrades.SlotCount];
        SoundBank sounds;

        Material glMaterial;
        float scale = 1f, offX, offY;  // UI canvas -> screen pixels (top-left origin)
        float drawScale = 1f;          // GL coordinates -> screen pixels: the arena in a match, the UI canvas on menus
        float glZoom = 1f;             // menu previews (LobbyUi.cs): a model drawn around (0, 0), scaled by glZoom
        V2 glShift;                    //   and moved to glShift on the UI canvas
        bool menuPreview;              // drawing an enemy for a menu tile (keeps it easy to see)
        GUIStyle labelStyle;  // base for the HUD's labels; menus use Ui
        string addressList;

        // Reset statics in case "Enter Play Mode Options" skips domain reloads.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            booted = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (booted)
                return;
            booted = true;
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.03f);
            cam.orthographic = true;
            if (FindFirstObjectByType<AudioListener>() == null)
                cam.gameObject.AddComponent<AudioListener>();
            cam.gameObject.AddComponent<ArenaApp>();
            DontDestroyOnLoad(cam.gameObject);
        }

        void Awake()
        {
            Application.runInBackground = true;  // keep hosting while alt-tabbed
            Application.targetFrameRate = 144;
            if (QualitySettings.antiAliasing < 4)
                QualitySettings.antiAliasing = 4;  // smooth edges on everything drawn with GL (shapes, arcs, glows)
            if (QualitySettings.activeColorSpace != ColorSpace.Gamma)
                Debug.Log("Arena: the colour space is " + QualitySettings.activeColorSpace + "; colours and glows are designed for Gamma " +
                    "(Edit > Project Settings > Player > Other Settings > Color Space; the Arena editor script sets it when the project opens).");
            Time.fixedDeltaTime = 1f / 60f;

            session = new Session();
            game = session.Game;
            game.Log = msg => Debug.Log(msg);
            sounds = new SoundBank(gameObject);
            MenuVhs.OnHover = calm => sounds.Play(Sfx.MenuHover, calm ? 0.35f : 0.7f, Time.unscaledTime);
            LoadMusicPack();  // clips you assigned in Unity replace / join the generated music (MusicPack.cs)
            sounds.StartMusic(gameObject);
            LoadAudioSettings();
            ScanCustomMusic();
            LoadJukebox();
            Keybinds.Load();  // Settings → Keybinds (Keybinds.cs)
            LoadFxSettings();  // Juice.cs
            LoadCamLean();     // CameraLean.cs
            LoadBackgroundSettings();
            InitSaveFolders();  // saved runs and the profile (SavesUi.cs)
            LoadTextOverrides();  // text edited with the dev text editor (DevTextEdit.cs)
            ParseCommandLine();
        }

        // Command-line options for quick testing, e.g.
        //   TopDownArena.exe --host --role=tank --autostart=2
        //   TopDownArena.exe --join=127.0.0.1 --role=dps
        void ParseCommandLine()
        {
            var opts = new Dictionary<string, string>();
            foreach (string arg in Environment.GetCommandLineArgs())
            {
                if (!arg.StartsWith("--"))
                    continue;
                int eq = arg.IndexOf('=');
                if (eq < 0)
                    opts[arg.Substring(2)] = "";
                else
                    opts[arg.Substring(2, eq - 2)] = arg.Substring(eq + 1);
            }
            string v;
            LoadMenuFields();  // your name, the port and the last address (Menus.cs); the command line wins
            SyncNameFromProfile();  // the active profile's name is yours (ProfilesUi.cs)
            whoOpen = !(opts.ContainsKey("host") || opts.ContainsKey("join") || opts.ContainsKey("autostart"));  // "Who's playing?" first, unless started from the command line
            if (whoOpen)
                WhoRefresh();
            if (opts.TryGetValue("name", out v)) nameText = v;
            if (opts.TryGetValue("port", out v)) portText = v;
            if (opts.TryGetValue("role", out v))
            {
                for (int i = 0; i < Defs.Roles.Length; i++)
                    if (string.Equals(Defs.Roles[i].Name, v, StringComparison.OrdinalIgnoreCase))
                        hostRole = (Role)i;
                joinRole = hostRole;
            }
            int weapon;
            if (opts.TryGetValue("weapon", out v) && int.TryParse(v, out weapon))
                weaponChoice = Mathf.Clamp(weapon - 1, 0, Defs.WeaponsPerRole - 1);
            if (opts.TryGetValue("ability", out v) && int.TryParse(v, out weapon))
                abilityChoice = Mathf.Clamp(weapon - 1, 0, Upgrades.RoleAbilityChoices - 1);
            if (opts.TryGetValue("class", out v))
                for (int i = 0; i < Defs.KitCount; i++)
                    if (string.Equals(((Kit)i).ToString(), v, StringComparison.OrdinalIgnoreCase))
                        kitChoice = (Kit)i;
            if (opts.TryGetValue("autostart", out v)) int.TryParse(v, out session.AutoStart);
            if (opts.TryGetValue("start-wave", out v)) int.TryParse(v, out session.StartWave);
            game.Bot = opts.ContainsKey("bot");
            recordSessions = PlayerPrefs.GetInt("record_sessions", 1) == 1 || opts.ContainsKey("record");
            devKillAllKey = PlayerPrefs.GetInt("dev_killall", 0) == 1;
            devPauseKey = PlayerPrefs.GetInt("dev_pausekey", 0) == 1;
            ApplyRecording();
            recordReplays = PlayerPrefs.GetInt("record_replays", 0) == 1 || opts.ContainsKey("record-replays");
            ApplyReplayRecording();
            if (opts.ContainsKey("host"))
                HostGame();
            else if (opts.TryGetValue("join", out v))
            {
                addressText = v;
                session.Join(addressText, Port(), nameText, joinRole, weaponChoice, abilityChoice, kitChoice);
            }
        }

        int Port()
        {
            int p;
            return int.TryParse(portText, out p) && p > 0 && p < 65536 ? p : Session.DefaultPort;
        }

        void HostGame()
        {
            SaveMenuFields();
            if (session.Host(Port(), nameText, hostRole, weaponChoice, abilityChoice, kitChoice))
                addressList = string.Join(", ", NetPeer.LocalAddresses().ToArray());
        }

        // Closing the game ends a match in progress, so its recording is saved as "quit".
        void OnApplicationQuit()
        {
            game.Stop("quit");
            session.Leave("");
        }

        void OnDestroy()
        {
            session.Leave("");
        }

        // --- Loop --------------------------------------------------------------

        void Update()
        {
            if (replay != null)
            {
                ReplayUpdate();  // watching a replay: nobody is at the controls (ReplayUi.cs)
                return;
            }
            Keybinds.Tick();  // Settings → Keybinds waiting for a key (Keybinds.cs)
            session.Poll();
            game.Update(devFrozen ? 0f : Time.deltaTime * EndSpeed);  // slowed as a run ends (RunEnding.cs); nothing ages while P has frozen it
            LabUpdate(Time.deltaTime);  // Boss Lab bots (BossLabUi.cs)
            WaveEditorUpdate(Time.unscaledDeltaTime);  // the wave editor's playhead and keys (WaveEditorUi.cs)
            foreach (var sfx in game.Sounds)
                sounds.Play(sfx.Sfx, sfx.Ally ? 0.45f : 1f, Time.unscaledTime);
            game.Sounds.Clear();
            UpdateMusic();
            UpdateLayout();
            TickEnding(Time.unscaledDeltaTime);  // the run ending: slow motion, then the results come in (RunEnding.cs)
            UpdateCameraLean(Time.unscaledDeltaTime);  // the picture eases toward your aim (CameraLean.cs)
            StepPauseAnim(Time.unscaledDeltaTime);  // the pause menu opening or closing (WindowAnim.cs)
            var me = game.Me;
            // If someone else resumes while our menu is open, close it.
            if (wasPaused && !game.Paused)
                pauseMenuOpen = false;
            wasPaused = game.Paused;
            if (session.Screen != AppScreen.Game)
                pauseMenuOpen = false;
            // Settings opens from the main menu and the lobby (its cog by Leave); the Field guide from the pause menu in a
            // match, or from Settings.
            if (session.Screen == AppScreen.Game)
                settingsOpen = false;
            if (session.Screen == AppScreen.Game ? !pauseMenuOpen && !game.Paused : session.Screen == AppScreen.Lobby && !lobbyMenuOpen)
                fieldGuideOpen = false;  // (the Field guide opens from the pause menu, the lobby's menu, or the main menu)
            if (session.Screen != AppScreen.Lobby)
                lobbyMenuOpen = false;
            if (session.Screen != AppScreen.Game || !pauseMenuOpen && !game.Paused)
                runStatsOpen = false;  // the pause menu's Run stats closes with it (RunStatsUi.cs)
            else if (fieldGuideOpen && !devMenuOpen && !labSetupOpen)
                FieldGuideKeys();
            if (Input.GetKeyDown(KeyCode.F1))
            {
                devMenuOpen = !devMenuOpen;
                GUIUtility.keyboardControl = 0;
            }
            // Solo, holding Tab pauses the match while the Run stats screen is up (RunStatsUi.cs); in multiplayer
            // it doesn't (the pause menu's Run stats button is for looking together).
            bool tabKey = session.Screen == AppScreen.Game && me != null && Input.GetKey(KeyCode.Tab) && GUIUtility.keyboardControl == 0;
            bool soloTab = tabKey && game.Players.Count == 1 && !LabRunning && !pauseMenuOpen && !devMenuOpen && !labSetupOpen && !waveEdOpen
                && (game.State == MatchState.Fighting || game.State == MatchState.Intermission);
            if (soloTab && !game.Paused)
            {
                game.RequestPause(true);
                tabPaused = true;
            }
            if (tabPaused && (!soloTab || !game.Paused))
            {
                if (game.Paused && !pauseMenuOpen)
                    game.RequestPause(false);
                tabPaused = false;
            }
            // The F1 pause key (P): freezes the match without opening the pause menu, so a screenshot shows only the game.
            // P again resumes. Opening the pause menu, or anyone resuming, ends it.
            if (devFrozen && (pauseMenuOpen || session.Screen != AppScreen.Game || game.State == MatchState.GameOver || game.State == MatchState.Victory))
                devFrozen = false;  // the menu or the end of the run takes over
            if (devFrozen && game.Paused)
                devFrozenAck = true;
            else if (devFrozen && devFrozenAck)
                devFrozen = false;  // someone else resumed
            if (!devFrozen)
                devFrozenAck = false;
            if (devPauseKey && Input.GetKeyDown(KeyCode.P) && GUIUtility.keyboardControl == 0 && session.Screen == AppScreen.Game && me != null && !LabRunning
                && !pauseMenuOpen && !devMenuOpen && !labSetupOpen && !waveEdOpen && !RecallActive && !tabPaused
                && (game.State == MatchState.Fighting || game.State == MatchState.Intermission))
            {
                if (devFrozen)
                {
                    devFrozen = false;
                    game.RequestPause(false);
                }
                else if (!game.Paused)
                {
                    devFrozen = true;
                    game.RequestPause(true);
                }
            }
            bool menuUp = pauseMenuOpen || (game.Paused && !tabPaused && !devFrozen) || devMenuOpen || labSetupOpen || waveEdOpen;
            bool choosing = me != null && me.Offer != null && !menuUp;
            // Clicks on menus (upgrade cards, pause menu, end-of-run screen) shouldn't also shoot or
            // parry: a press that starts while one is up is ignored until the button is released.
            // Holding Tab opens the loadout, which has buttons (moving abilities between keys). It works
            // while choosing an upgrade too: the cards hide until you let go.
            tabHeld = tabKey && !menuUp;
            bool watching = LabRunning && !lab.Seats[0].Human;  // Boss Lab with only bots: the mouse is just for the panel
            bool pathChoice = session.Screen == AppScreen.Game && game.PathOffer != null;
            bool shopping = session.Screen == AppScreen.Game && game.ShopOpen && me != null;  // the Bazaar's shop covers the arena (ShopUi.cs)
            bool overlay = menuUp || choosing || tabHeld || pathChoice || shopping || game.State == MatchState.GameOver || game.State == MatchState.Victory || watching || MouseOnLab();
            if (!(session.Screen == AppScreen.Lobby && lobbyTab == HangoutTab))  // (the Hangout ticks its own, in OnGUI)
                TickEmoteBar(session.Screen == AppScreen.Game && me != null && !me.Downed && !overlay);  // EmoteBar.cs: while it's open a click picks an emote
            TickPing(session.Screen == AppScreen.Game && me != null && !me.Downed && !overlay && !emoteBarOpen);  // PingLooks.cs: tap to ping, hold for the wheel
            TickChat(session.Screen == AppScreen.Game && me != null && !menuUp && !choosing && !tabHeld && !pathChoice && !shopping);  // ChatBox.cs: Enter opens the chat line
            // Fire and Parry (a key or a mouse button, Keybinds) pressed while a menu was up stay off until let go.
            if ((overlay || emoteBarOpen || pingWheelOpen) && Keybinds.Down(KeyAction.Fire))
                fireOnMenu = true;
            if (overlay && Keybinds.Down(KeyAction.Parry))
                parryOnMenu = true;
            if (!Keybinds.Held(KeyAction.Fire))
                fireOnMenu = false;
            if (!Keybinds.Held(KeyAction.Parry))
                parryOnMenu = false;
            fireBlocked = overlay || fireOnMenu || emoteBarOpen || pingWheelOpen;
            parryBlocked = overlay || parryOnMenu;
            mouseBlocked = fireBlocked || parryBlocked;
            Cursor.visible = session.Screen != AppScreen.Game || choosing || menuUp || tabHeld || pathChoice || shopping || watching || MouseOnLab() || WaveBoxUp(me);

            if (!menuUp)
            {
                // Remember ability presses between fixed steps so quick taps aren't lost.
                if (Keybinds.Down(KeyAction.Ability))
                {
                    wantAbility[0] = true;
                    TileKeyPressed(0);  // still recharging: the tile shakes (HudTiles.cs)
                }
                if (Keybinds.Down(KeyAction.Ultimate))
                {
                    wantAbility[Upgrades.UltSlot] = true;
                    TileKeyPressed(Upgrades.UltSlot);  // still recharging: the tile shakes (HudTiles.cs)
                }
                if (Keybinds.Down(KeyAction.AbilityQ))
                {
                    wantAbility[1] = true;
                    TileKeyPressed(1);  // still recharging: the tile shakes (HudTiles.cs)
                }
                if (Keybinds.Down(KeyAction.AbilityE))
                {
                    wantAbility[2] = true;
                    TileKeyPressed(2);  // still recharging: the tile shakes (HudTiles.cs)
                }
                if (Keybinds.Down(KeyAction.AbilityShift))
                {
                    wantAbility[3] = true;
                    TileKeyPressed(3);  // still recharging: the tile shakes (HudTiles.cs)
                }
            }
            // Ignore the mute key while typing in a text box (e.g. a name with an "m" in it).
            if (Keybinds.Down(KeyAction.AimLine) && GUIUtility.keyboardControl == 0 && session.Screen == AppScreen.Game)
                SetAimLine(!aimLine);
            if (Input.GetKeyDown(KeyCode.F3))
                aimDebug = !aimDebug;  // aim debug overlay
            DpsMeterKeys();  // F2: the DPS meter (DpsMeterUi.cs)
            TextEditKeys();  // F4: the dev text editor
            if (Keybinds.Down(KeyAction.ToggleHud) && GUIUtility.keyboardControl == 0 && session.Screen == AppScreen.Game)
                hudShown = !hudShown;  // hide or show the HUD (Hud.cs)
            TickLaunch(Time.unscaledDeltaTime);  // a run starting: the drop-in (LaunchIntro.cs)
            TickMenuIntro(Time.unscaledDeltaTime);  // the main menu drawing itself in, the first time (MenuIntro.cs)
            TickLobbyEnter(Time.unscaledDeltaTime);  // the menu turning into the lobby (LobbyEnter.cs)
            TickRecall(Time.unscaledDeltaTime);      // back to the lobby from a run: the beam-out (Recall.cs)
            TickWindowClose();  // a menu window sliding out, then closing (WindowAnim.cs)
            TickTiles(Time.unscaledDeltaTime);   // the ability bar's tiles react (HudTiles.cs)
            hudT = MathX.MoveTowards(hudT, hudShown && !LaunchHoldsHud && !BossIntroHoldsHud && !RecallActive ? 1f : 0f, Time.unscaledDeltaTime / HudFadeTime);  // (a boss's entrance card hides it: Juice.cs)
            if (BossIntroHoldsHud)
                introHidHud = true;
            else if (hudT >= 1f || !hudShown)
                introHidHud = false;
            if (Keybinds.Down(KeyAction.Mute) && GUIUtility.keyboardControl == 0)
            {
                sounds.Muted = !sounds.Muted;
                SaveAudioSettings();
            }

            if (choosing && !tabHeld && !PickOutroPlaying)
            {
                SyncSelection(me);
            }
            TickWaveReady(me);  // the saved Auto-ready sent to the host, the Ready key (WaveClearedUi.cs)
            TickPickSounds(me, choosing && session.Screen == AppScreen.Game);  // the picker opening and closing (PickSounds.cs)
            if (choosing && !tabHeld && !PickOutroPlaying && !PickIntroPlaying(me))  // the cards are still flying out (PickIntro.cs)
            {
                for (int k = 0; k < CardKeys.Length; k++)
                    if (Input.GetKeyDown(CardKeys[k]) && k < me.Offer.Length && selectedCard != k)
                    {
                        selectedCard = k;
                        pickKey = 0;
                    }
                if (!chatOpen && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))) ConfirmPick();
                else if (Keybinds.Down(KeyAction.Reroll)) RerollPress();  // (a second press within a moment rerolls)
            }
            // Standing on your chest item: G takes it, X throws it away (Props.cs).
            if (session.Screen == AppScreen.Game && !menuUp && !tabHeld && GUIUtility.keyboardControl == 0 && game.MyLootInReach != null)
            {
                if (Keybinds.Down(KeyAction.TakeItem))
                    game.ResolveLoot(true);
                else if (Keybinds.Down(KeyAction.DiscardItem))
                    game.ResolveLoot(false);
            }
            // The F1 kill-all key (X), when it's on: every enemy on the field dies as your kill. Not while you're standing
            // on a chest item (X throws that away), and only the host can (DevTools.cs).
            else if (devKillAllKey && session.IsHost && session.Screen == AppScreen.Game && !menuUp && !tabHeld && GUIUtility.keyboardControl == 0
                     && Keybinds.Down(KeyAction.DiscardItem))
                game.DevKillAll(game.MyId);

            bool edWasOpen = waveEdOpen;  // Esc closes the wave editor first, and nothing else that press (WaveEditorUi.cs)
            if (edWasOpen && EscPressed && GUIUtility.keyboardControl == 0)
                waveEdOpen = false;
            if (session.Screen != AppScreen.Game && EscPressed && !devMenuOpen && !edWasOpen)
            {
                if (fieldGuideOpen)
                    CloseFieldGuide();  // back to the lobby's menu
                else if (lobbyMenuOpen && (pauseSettings || pauseHowTo || pauseRecords))
                    pauseSettings = pauseHowTo = pauseRecords = false;  // the lobby menu's Settings: back to its list
                else if (lobbyMenuOpen)
                    CloseLobbyMenu();
                else if (session.Screen == AppScreen.Lobby && !settingsOpen && !LobbyEntering && !LobbyLeaving && !LobbyReturning && GUIUtility.keyboardControl == 0)
                    OpenLobbyMenu();  // Esc in the lobby: its menu (MenuDrawer.cs)
                else if (settingsOpen)
                    CloseWindow(() => settingsOpen = false);  // back to the lobby or the main menu, sliding out (WindowAnim.cs)
                else if (howToOpen)
                    CloseWindow(() => howToOpen = false);
                else if (recordsOpen)
                    CloseWindow(() => recordsOpen = false);
                else if (cosmeticsOpen)
                    CloseWindow(() => cosmeticsOpen = false);  // CosmeticsUi.cs
                else if (menuPlayOpen && session.Screen == AppScreen.Menu && GUIUtility.keyboardControl == 0)
                    PlayPickerBack();  // the saved-runs picker: back to the older runs' first page, or to the menu (SavesUi.cs)
            }
            if (session.Screen == AppScreen.Game && !RecallActive && !edWasOpen)  // (nothing to open while everyone's beaming out: Recall.cs)
            {
                if (EscPressed && labSetupOpen)
                    labSetupOpen = false;
                else if (EscPressed && runStatsOpen && !devMenuOpen)
                    runStatsOpen = false;  // back to the pause menu (RunStatsUi.cs)
                else if (EscPressed && fieldGuideOpen && !devMenuOpen)
                    CloseFieldGuide();  // back to the pause menu (FieldGuide.cs)
                else if (EscPressed && devMenuOpen)
                    devMenuOpen = false;
                else if (EscPressed && pauseConfirmLobby && menuUp)
                    pauseConfirmLobby = false;  // "back to the lobby?": no (MenuDrawer.cs)
                else if (EscPressed && (pauseSettings || pauseHowTo || pauseRecords) && menuUp)
                    pauseSettings = pauseHowTo = pauseRecords = false;  // the pause menu's Settings: back to its choices (MenuDrawer.cs)
                else if (EscPressed)
                {
                    if (menuUp)
                        ResumeGame();
                    else
                        OpenPauseMenu();
                }
                else if (session.IsHost && (game.State == MatchState.GameOver || game.State == MatchState.Victory) && !menuUp)
                {
                    if (Input.GetKeyDown(KeyCode.R))
                        session.StartMatch();
                    else if (Input.GetKeyDown(KeyCode.L))
                        RecallToLobby();  // everyone beams out first (Recall.cs)
                    else if (Input.GetKeyDown(KeyCode.C) && game.Hunt == null)
                        game.ContinueEndless();
                }
            }
        }

        static readonly KeyCode[] CardKeys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8 };

        /// Menu theme on the menus; the soft battle track in a match; each boss's own track while
        /// it's on the field. The next boss's track is rendered ahead of time.
        void UpdateMusic()
        {
            TickDecodes();
            string wanted = "menu", keep = null;
            if (session.Screen == AppScreen.Game && game.State != MatchState.GameOver && game.State != MatchState.Victory)
            {
                wanted = "battle";
                foreach (var e in game.Enemies.Values)
                    if (e.Def.IsBoss)
                    {
                        var key = Game.IsReaper(e.Type) ? EnemyType.TwinA : e.Type;
                        sounds.RequestBossTrack(key);
                        wanted = SoundBank.BossTrackId(key);
                        break;
                    }
                int next = game.NextBossWave(game.Wave, game.State == MatchState.Fighting);
                var upcoming = game.BossAt(next);
                sounds.RequestBossTrack(upcoming);
                keep = SoundBank.BossTrackId(upcoming);
            }
            // The music player's choice replaces the automatic music (a boss still gets its own theme if that's on).
            string keep2 = null;
            if (juke.Selected != null)
            {
                if (juke.BossThemes && wanted.StartsWith("boss:"))
                    keep2 = juke.Selected;
                else
                {
                    wanted = juke.Selected;
                    RequestMusic(wanted);
                    keep2 = juke.Upcoming;
                    RequestMusic(keep2);
                }
                AdvanceJukebox();
            }
            sounds.UpdateMusic(wanted, keep, Time.unscaledDeltaTime, keep2);
        }

        void FixedUpdate()
        {
            if (replay != null)
            {
                ReplayFixedStep(Time.fixedDeltaTime);  // (ReplayUi.cs)
                return;
            }
            session.Poll();
            var input = new PlayerInput
            {
                Move = new V2(Axis(KeyAction.MoveRight) - Axis(KeyAction.MoveLeft), Axis(KeyAction.MoveDown) - Axis(KeyAction.MoveUp)).Normalized(),
                AimAt = MouseArena(),
                Fire = Keybinds.Held(KeyAction.Fire) && !fireBlocked,
                Ability = wantAbility[0] || Keybinds.Held(KeyAction.Ability),
                AbilityQ = wantAbility[1] || Keybinds.Held(KeyAction.AbilityQ),
                AbilityE = wantAbility[2] || Keybinds.Held(KeyAction.AbilityE),
                AbilityShift = wantAbility[3] || Keybinds.Held(KeyAction.AbilityShift),
                Ultimate = wantAbility[Upgrades.UltSlot] || Keybinds.Held(KeyAction.Ultimate),
                Parry = Keybinds.Held(KeyAction.Parry) && !parryBlocked,
                WeaponAction = Keybinds.Held(KeyAction.WeaponAction),
            };
            for (int i = 0; i < wantAbility.Length; i++)
                wantAbility[i] = false;
            if (pauseMenuOpen || game.Paused)
                input = new PlayerInput { AimAt = input.AimAt };  // no moving or shooting behind the menu
            game.FixedStep(Time.fixedDeltaTime * EndSpeed, input);
            LabFixedStep(Time.fixedDeltaTime);
            ShowcaseFixedStep(Time.fixedDeltaTime);  // the lobby's loadout preview (LobbyUi.cs)
        }

        /// Forget the highlighted card whenever a new set of cards is shown.
        void SyncSelection(Player me)
        {
            string key = me.Offer == null ? "" : string.Join(",", Array.ConvertAll(me.Offer, x => x.ToString())) + "/" + me.Taken.Count;
            if (key != shownOffer)
            {
                // The same picks taken and different cards: a reroll (the opening skips its fanfare; PickIntro.cs).
                bool reroll = shownOffer.Length > 0 && key.Length > 0 && shownOffer.Substring(shownOffer.LastIndexOf('/')) == key.Substring(key.LastIndexOf('/'));
                shownOffer = key;
                StartPickIntro(reroll);
                selectedCard = -1;
                offerShownAt = Time.unscaledTime;
                rerollArmedAt = -99f;
            }
        }

        void ConfirmPick()
        {
            var me = game.Me;
            if (selectedCard < 0 || game.RerollPending || PickOutroPlaying || PickIntroPlaying(me))
                return;
            rerollArmedAt = -99f;
            StartPickOutro(me, selectedCard);  // the cards fly back into you, yours last (PickOutro.cs)
            int key = 0;
            if (me != null && me.Offer != null && selectedCard < me.Offer.Length && Upgrades.All[me.Offer[selectedCard]].Grants != AbilityId.None)
                key = pickKey;
            game.PickUpgrade(selectedCard, key);
            selectedCard = -1;
        }

        static float Axis(KeyAction a) => Keybinds.MoveHeld(a) ? 1f : 0f;

        /// Esc, unless it just cancelled a rebind (Keybinds.cs).
        static bool EscPressed => Input.GetKeyDown(KeyCode.Escape) && !Keybinds.Busy && !bgBig;  // (the bigger background view takes its own Esc, MenuBackgrounds.cs)

        void UpdateLayout()
        {
            scale = Mathf.Min(UnityEngine.Screen.width / W, UnityEngine.Screen.height / H);
            offX = (UnityEngine.Screen.width - W * scale) / 2f;
            offY = (UnityEngine.Screen.height - H * scale) / 2f;
        }

        V2 MouseArena()
        {
            Vector3 m = Input.mousePosition;
            float ws = scale * WorldToUi;
            return new V2((m.x - offX) / ws, (UnityEngine.Screen.height - m.y - offY) / ws) + lean;  // (+ the camera lean, CameraLean.cs)
        }

        // --- Menus and HUD (IMGUI, arena coordinates) --------------------------

        void InitStyles()
        {
            if (labelStyle != null)
                return;
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
            labelStyle.normal.textColor = new Color(0.9f, 0.92f, 1f);
        }

        /// The main menu under a side drawer (Settings, Records, How to play): the box fades and drifts out as the drawer
        /// slides in, and back in as it slides out. Draw-only (Repaint), so it takes no clicks. `hidden` skips it.
        void DrawMenuUnderDrawer(bool hidden)
        {
            float vis = hidden ? 1f : WindowVis;
            if (vis >= 1f || Event.current.type != EventType.Repaint)
                return;
            PushShift(70f * EaseOut(vis), 0f, 1f - vis);
            DrawMenu();
            PopShift();
        }

        void OnGUI()
        {
            UpdateLayout();
            InitStyles();
            Ui.Init();
            GUI.matrix = Matrix4x4.TRS(new Vector3(offX, offY, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));
            // With the F1 menu or the Boss Lab setup open, the screen underneath is only drawn, not used:
            // IMGUI hands a click to the first control drawn under it, so buttons hidden behind the panel
            // (the main menu's fields and buttons) would otherwise take clicks meant for the panel.
            TextEditBeginGui();
            Ui.BeginHoverFrame();
            bool modal = labSetupOpen || devMenuOpen || textEditOpen || waveEdOpen;
            if (!modal || Event.current.type == EventType.Repaint)
            switch (session.Screen)
            {
                case AppScreen.Menu:
                    if (profTr)
                        DrawProfTr();  // the picker and the menu trading places (ProfileTransition.cs)
                    else if (whoOpen)
                        DrawWho();  // ProfilesUi.cs
                    else if (fieldGuideOpen)
                        DrawFieldGuideScreen();  // FieldGuide.cs (the main menu's button)
                    else if (settingsOpen)
                    {
                        DrawMenuUnderDrawer(bgBig);
                        DrawSettings();
                    }
                    else if (howToOpen)
                    {
                        DrawMenuUnderDrawer(false);
                        DrawHowToPlay();
                    }
                    else if (continueOpen)
                        DrawContinue();  // SavesUi.cs
                    else if (recordsOpen)
                    {
                        DrawMenuUnderDrawer(false);
                        DrawRecords();
                    }
                    else if (cosmeticsOpen)
                        DrawCosmetics();  // CosmeticsUi.cs
                    else
                        DrawMenu();
                    break;
                case AppScreen.Lobby:
                    // Settings (and the Field guide from it) over the lobby: the lobby is only drawn, not used, underneath.
                    if (LobbyReturning)
                        DrawLobbyReturning();  // back from a run: the dark lifting, the sides sliding in (Recall.cs)
                    else if (LobbyLeaving)
                        DrawLobbyLeavingToMenu();  // Leave: the way in, backwards (LobbyEnter.cs)
                    else if (LobbyEntering)
                        DrawLobbyEntering();  // just in from the menu: the card becomes your loadout (LobbyEnter.cs)
                    else if (!settingsOpen && !lobbyMenuOpen || Event.current.type == EventType.Repaint)
                        DrawLobby();
                    if (lobbyMenuOpen)
                    {
                        if (fieldGuideOpen)
                            DrawFieldGuideScreen();  // FieldGuide.cs
                        else
                            DrawLobbyMenu();  // Esc: back, Settings, Field guide, leave, quit (MenuDrawer.cs)
                    }
                    else if (settingsOpen)
                    {
                        Ui.Fill(new Rect(0, 0, W, H), new Color(0.01f, 0.01f, 0.03f, 0.75f * (fieldGuideOpen ? 1f : WindowVis)));  // (fading with the drawer, WindowAnim.cs)
                        if (fieldGuideOpen)
                            DrawFieldGuideScreen();  // FieldGuide.cs
                        else
                            DrawSettings();
                    }
                    break;
                case AppScreen.Game:
                    DrawHud();
                    DrawLaunchWave();    // WAVE 1 popping in as a launched run's fight starts (LaunchIntro.cs)
                    DrawLobbyLeaving();  // the lobby sliding away as the run launches (LaunchIntro.cs)
                    DrawPickOutro();  // an upgrade just taken: the cards flying back into you (PickOutro.cs)
                    if (LabRunning)
                        DrawLabPanel();
                    if ((PauseMenuWanted || pauseAnim > 0f) && !devMenuOpen && !labSetupOpen && !waveEdOpen)
                    {
                        if (fieldGuideOpen && PauseMenuWanted)
                            DrawFieldGuideScreen();  // FieldGuide.cs
                        else if (runStatsOpen && PauseMenuWanted)
                            DrawRunStatsScreen(game.Me, true);  // RunStatsUi.cs
                        else
                            DrawPauseMenu();  // with its opening and closing (WindowAnim.cs)
                    }
                    DrawRecallFade();  // back to the lobby: the screen going dark (Recall.cs)
                    break;
            }
            if (replay != null && (!modal || Event.current.type == EventType.Repaint))
                DrawReplayBar();  // the timeline (ReplayUi.cs)
            if (!textEditOpen || Event.current.type == EventType.Repaint)
            {
                if (waveEdOpen)
                    DrawWaveEditor();  // WaveEditorUi.cs
                else if (labSetupOpen)
                    DrawLabSetup();
                else if (devMenuOpen)
                    DrawDevMenu();
            }
            TextEditEndGui();
            bool clicked = Ui.TakeClick();
            if (Ui.EndHoverFrame() && sounds != null && !clicked)
                sounds.PlayUiHover(Time.unscaledTime);  // a soft tick when the mouse moves onto a button
            if (clicked && sounds != null)
                sounds.PlayUiClick(Time.unscaledTime);  // and a soft click when one is pressed
        }

        void Label(Rect r, string text, int size, Color color, TextAnchor align, bool bold = false)
        {
            var style = new GUIStyle(labelStyle) { fontSize = size, alignment = align, wordWrap = false, fontStyle = bold ? FontStyle.Bold : FontStyle.Normal };
            style.normal.textColor = color;
            GUI.Label(r, Ui.Shown(r, text), style);
        }

        static void Box(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = Ui.Faded(c);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        static Color ToColor(Rgba c) => new Color(c.R, c.G, c.B, c.A);
    }
}
