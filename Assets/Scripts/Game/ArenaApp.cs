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
        int ultChoice;  // the key picked on the lobby's Ultimate row (LobbyEntry.Ult; 0 the default ultimate)
        int wildSecond = 1;  // a Wildcard ability (0-17), on the 2nd ability key; see Defs.MakeWild
        int wildTab;                      // the lobby's Wildcard picker: 0 edits the 1st ability, 1 the second
        Kit kitChoice = Kit.Soldier;
        bool pauseMenuOpen, settingsOpen, wasPaused;
        bool tabPaused;  // solo: holding Tab paused the run (released, it resumes)
        int tabScroll; // Tab screen: rows scrolled in the upgrade list
        bool fireOnMenu, parryOnMenu, fireBlocked, parryBlocked;  // a mouse press that belongs to a menu, not the game
        bool devKillAllKey;              // F1 dev tool: the discard key (B) kills every enemy on the field (host only; saved)
        bool aimLine = true;             // V: a soft line from your gun to the crosshair, at most your weapon's range (saved per player)

        void SetAimLine(bool on)
        {
            aimLine = on;
            PlayerPrefs.SetInt("aim_line", on ? 1 : 0);
            PlayerPrefs.Save();
        }

        bool tabHeld;  // Tab: your stats and loadout, shown only on this screen (the game keeps running)
        bool tabSuppress;  // a room card or buildable was used from the Tab screen's Inventory (InventoryUi.cs): the screen stays shut until Tab is let go
        int selectedCard = -1;  // upgrade card highlighted, waiting for Confirm
        int pickKey;            // for a card that grants an ability: the key to put it on (1 = the 2nd ability, 2 = the 3rd, 3 = the 4th)
        string shownOffer = "";
        float offerShownAt = -99f;     // when the current cards appeared (unscaled time)
        const float RerollLock = 0.6f;  // the reroll stays locked this long after new cards appear, so a spam-click can't burn rerolls

        /// The reroll can't be used for a moment after the window opens or new cards come in.
        bool RerollLocked => Time.unscaledTime - offerShownAt < RerollLock;
        readonly bool[] wantAbility = new bool[Upgrades.SlotCount];
        SoundBank sounds;

        Material glMaterial;
        float scale = 1f, offX, offY;  // UI canvas -> screen pixels (top-left origin)
        float drawScale = 1f;          // GL coordinates -> screen pixels: the arena in a run, the UI canvas on menus
        float glZoom = 1f;             // menu previews (LobbyUi.cs): a model drawn around (0, 0), scaled by glZoom
        V2 glShift;                    //   and moved to glShift on the UI canvas
        bool menuPreview;              // drawing an enemy for a menu tile (keeps it easy to see)
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
            if (FindAnyObjectByType<AudioListener>() == null)
                cam.gameObject.AddComponent<AudioListener>();
            cam.gameObject.AddComponent<ArenaApp>();
            DontDestroyOnLoad(cam.gameObject);
        }

        void Awake()
        {
            Game.StartDelay = LaunchFightAt;  // a longer GET READY than the sim default (LaunchIntro.cs)
            useGUILayout = false;  // nothing uses GUILayout or GUI.Window: skip the Layout pass, which ran all of OnGUI a second time per frame
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
            Diag.Sink = msg => Debug.LogWarning("Arena: " + msg);  // (recovered file, save and sound failures, Diag.cs)
            sounds = new SoundBank(gameObject);
            MenuVhs.OnHover = calm => sounds.Play(Sfx.MenuHover, SoundBank.MenuSoundVolume * (calm ? 0.5f : 1f), Time.unscaledTime);
            LoadMusicPack();  // clips you assigned in Unity replace / join the generated music (MusicPack.cs)
            sounds.StartMusic(gameObject);
            LoadAudioSettings();
            ScanCustomMusic();
            LoadJukebox();
            Keybinds.Load();  // Settings → Keybinds (Keybinds.cs)
            LoadFxSettings();  // Juice.cs
            LoadCamLean();     // CameraLean.cs
            LoadCamZoom();     // CameraZoom.cs
            LoadHoverNames();  // EnemyTagLook.cs
            LoadNameLabels();
            LoadHpBars();  // Settings > Gameplay > Health bars (OverheadStack.cs)
            LoadLagChart();  // LagChartUi.cs
            SetHudMode(SavedHudMode(), false);  // the HUD mode, remembered (HudModes.cs)
            LoadMapView();  // minimap, full map or off, remembered (MapView.cs)
            LoadBackgroundSettings();
            InitLobbyCrt();  // build the lobby CRT's textures now, not on the New run boot's first frame (LobbyCrt.cs)
            InitSaveFolders();  // saved runs and the profile (SavesUi.cs)
            LoadCellLayoutEdits();  // cell layouts edited in the Cell Editor (CellEditorUi.cs)
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
            {
                WhoRefresh();
                CarCenterOn(ProfileStore.ActiveId);  // the carousel opens on the profile used last (DiskCarousel.cs)
            }
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
                weaponChoice = Mathf.Clamp(weapon - 1, 0, Defs.MaxWeapons - 1);  // (the host keeps it to what the class has)
            if (opts.TryGetValue("ability", out v) && int.TryParse(v, out weapon))
                abilityChoice = Mathf.Clamp(weapon - 1, 0, Upgrades.FirstAbilityChoices);  // (the host keeps it to what the class has: a 4th only where there is one)
            if (opts.TryGetValue("ultimate", out v) && int.TryParse(v, out weapon))
                ultChoice = Mathf.Clamp(weapon - 1, 0, Upgrades.UltKeys - 1);  // the host clamps it to what the class has
            if (opts.TryGetValue("class", out v))
                for (int i = 0; i < Defs.KitCount; i++)
                    if (string.Equals(((Kit)i).ToString(), v, StringComparison.OrdinalIgnoreCase))
                        kitChoice = (Kit)i;
            if (opts.TryGetValue("autostart", out v)) int.TryParse(v, out session.AutoStart);
            if (opts.TryGetValue("start-wave", out v)) int.TryParse(v, out session.StartWave);
            game.Bot = opts.ContainsKey("bot");
            devKillAllKey = PlayerPrefs.GetInt("dev_killall", 0) == 1;
            if (opts.ContainsKey("host"))
                HostGame();
            else if (opts.TryGetValue("join", out v))
            {
                addressText = v;
                session.Join(addressText, Port(), nameText, joinRole, weaponChoice, abilityChoice, kitChoice, Defs.MakeWild(wildSecond), ultChoice);
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
            if (session.Host(Port(), nameText, hostRole, weaponChoice, abilityChoice, kitChoice, ult: ultChoice))
            {
                addressList = string.Join(", ", NetPeer.LocalAddresses().ToArray());
                lobbyTab = ModeTabNow();  // a new lobby opens on the Game Mode screen (ModeScreenUi.cs)
            }
        }

        // Closing the game ends a run in progress, so its recording is saved as "quit".
        void OnApplicationQuit()
        {
            game.Stop("quit");
            session.Leave("");
            FlushPrefs(true);  // settings a slider or text field changed last (Menus.cs)
        }

        void OnDestroy()
        {
            session.Leave("");
        }

        // --- Loop --------------------------------------------------------------

        void Update()
        {
            TickFrameSystems();
            var me = game.Me;
            bool tabKey = UpdatePauseStates(me);
            UpdateWikiKey();  // the Wiki key: tap for the wiki, hold for the eyedropper (WikiPick.cs)
            bool menuUp = pauseMenuOpen || (game.Paused && !tabPaused) || ModalScreenOpen;
            bool choosing = me != null && me.Offer != null && !menuUp && !game.PickHeld(me) && !LaunchHoldsPicker;  // (cards held back while enemies are near: Game.PickHeld)
            UpdatePointerAndBlocking(me, tabKey, menuUp, choosing);
            if (!menuUp)
                ReadPlayKeys();
            UpdateToggleKeys();
            UpdateCardPicker(me, choosing);
            UpdateWorldKeys(me, menuUp);
            HandleEscAndEndKeys(me, menuUp);
            UpdateFirstPerson();  // F8, the mouse look (FirstPerson.cs)
        }

        /// The game, the network, bots, sounds and every per-frame animation tick.
        void TickFrameSystems()
        {
            Keybinds.Tick();  // Settings → Keybinds waiting for a key (Keybinds.cs)
            FlushPrefs(false);  // settings held back during a slider drag or typing (Menus.cs)
            session.Poll();
            PacmanUpdate();  // the Pac-Man game, when a Pac profile's Play opened it (PacmanUi.cs)
            game.Update(Time.deltaTime * EndSpeed);  // slowed as a run ends (RunEnding.cs)
            partyBots.Update(Time.deltaTime);  // bots in the lobby's open slots (PartyBots.cs)
            bool heard = !(session.Screen == AppScreen.Lobby && game.Practicing && !PracticeShown);  // (Practice is heard only while its window is up: HangoutUi.cs)
            foreach (var sfx in game.Sounds)
                if (heard)
                    sounds.Play(sfx.Sfx, sfx.Volume, Time.unscaledTime);
            game.Sounds.Clear();
            TickLitTankSound();  // A Literal Tank's engine (LiteralTankLook.cs, LitTankSoundBank.cs)
            TickHeliSound();  // the Attack Helicopter's rotor (AttackHelicopterLook.cs, HeliSoundBank.cs)
            if (heard)
                TickGaleWallSounds();  // Gale Wall's cast, catches, chime, unleash and hum (GaleWallLook.cs; before TickArcLockSounds, which marks effects heard)
            if (heard)
                TickArcLockSounds();  // Arc Lightning's tags, hops, forks and charge hum (ArcLockSoundBank.cs)
            TickVectorGain(Time.unscaledDeltaTime);  // vectors coming in: the bursts, the "+10 VECTORS" and the chip (VectorGainLook.cs)
            TickLootSweep(Time.unscaledDeltaTime);  // your coins swept to you as the wave starts (LootSweepLook.cs)
            TickOneWayBump();  // a One-way piece's door holding you back: the bump (OneWayDoorLook.cs)
            if (heard)
                TickWallTouch(Time.deltaTime);  // a wall touched: its arcs ease in and out, and the hum (WallTouchLook.cs)
            if (heard)
                TickFootsteps(Time.deltaTime);  // footsteps from everyone's speed (Footsteps.cs)
            UpdateMusic();
            UpdateLayout();
            TickBossSlow(Time.unscaledDeltaTime);  // a boss's death: a moment of slow motion (JuiceMoments.cs)
            TickEnding(Time.unscaledDeltaTime);  // the run ending: slow motion, then the results come in (RunEnding.cs)
            UpdateCameraLean(Time.unscaledDeltaTime);  // the picture eases toward your aim (CameraLean.cs)
            StepPauseAnim(Time.unscaledDeltaTime);  // the pause menu opening or closing (WindowAnim.cs)
            LagChartUpdate();  // F5: the lag chart, and its frame times (LagChartUi.cs)
        }

        /// Menus that close with their screen, F1, Tab's solo pause and the F1 pause key (P). Returns whether Tab is held for the Run stats.
        bool UpdatePauseStates(Player me)
        {
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
            if (session.Screen != AppScreen.Lobby)
            {
                lobbyMenuOpen = false;
                inspectId = -1;
                if (lobbyTab == PracticeTab)
                    lobbyTab = 0;  // back from a run on the loadout, no glitch (HangoutUi.cs)
                practiceLastTab = lobbyTab;
            }
            if (session.Screen != AppScreen.Game || !pauseMenuOpen && !game.Paused)
                runStatsOpen = false;  // the pause menu's Run stats closes with it (RunStatsUi.cs)
            if (Input.GetKeyDown(KeyCode.F1))
            {
                devMenuOpen = !devMenuOpen;
                GUIUtility.keyboardControl = 0;
            }
            // Solo, holding Tab pauses the run while the Run stats screen is up (RunStatsUi.cs); in multiplayer
            // it doesn't (the pause menu's Run stats button is for looking together).
            bool tabKey = session.Screen == AppScreen.Game && me != null && Input.GetKey(KeyCode.Tab) && GUIUtility.keyboardControl == 0;
            if (!tabKey)
                tabSuppress = false;
            bool soloTab = tabKey && !tabSuppress && game.Players.Count == 1 && !pauseMenuOpen && !ModalScreenOpen
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
            return tabKey;
        }

        /// What the mouse and the Fire / Parry keys are for this frame: a menu, the emote bar, the ping wheel, the chat, or the run; and the pointer.
        void UpdatePointerAndBlocking(Player me, bool tabKey, bool menuUp, bool choosing)
        {
            // The Hangout's ability press is caught here, every frame, and used by its next draw (a key press read only inside OnGUI could be missed).
            if (session.Screen == AppScreen.Lobby && lobbyTab == HangoutTab && (Keybinds.Down(KeyAction.Ability1) || Keybinds.Down(KeyAction.Parry)))
                hangoutCastLatch = true;
            // Clicks on menus (upgrade cards, pause menu, end-of-run screen) shouldn't also shoot or
            // parry: a press that starts while one is up is ignored until the button is released.
            // Holding Tab opens the loadout, which has buttons (moving abilities between keys). It works
            // while choosing an upgrade too: the cards hide until you let go.
            bool wasTab = tabHeld;
            tabHeld = tabKey && !menuUp && !tabSuppress;
            if (tabHeld && !wasTab)
                tabOpens++;  // (the TAB hint in the corner retires once you've used it: Hud.cs)
            Keybinds.SetPickerNumbers(choosing && !tabHeld ? Mathf.Min(me.Offer.Length, CardKeys.Length) : 0);  // its card keys win over a bound number key
            bool pathChoice = session.Screen == AppScreen.Game && (game.PathOffer != null || game.HuntMapVoting || ExpandWindowUp);
            bool shopping = session.Screen == AppScreen.Game && (ShopScreenUp(me) || PedestalSellUp(me));  // the Bazaar's shop covers the arena (ShopUi.cs)
            bool overlay = menuUp || wikiPicking || choosing || tabHeld || pathChoice || shopping || game.State == MatchState.GameOver || game.State == MatchState.Victory || MouseOnDock() || WaveBoxShown(me) || MouseOnWcStrip(me) || MouseOnBuilderShop(me);  // (the Builder Shop's shelf, BuilderShopUi.cs; the Wave cleared box has buttons: a click on Ready must not fire, ping or emote)
            if (!(session.Screen == AppScreen.Lobby && lobbyTab == HangoutTab))  // (the Hangout ticks its own, in OnGUI)
                TickEmoteBar(session.Screen == AppScreen.Game && me != null && !me.Downed && !overlay);  // EmoteBar.cs: while it's open a click picks an emote
            TickPing(session.Screen == AppScreen.Game && me != null && !me.Downed && !overlay && !emoteBarOpen);  // PingLooks.cs: tap to ping, hold for the wheel
            TickChat(session.Screen == AppScreen.Game && me != null && !menuUp && !choosing && !tabHeld && !pathChoice && !shopping);  // ChatBox.cs: Enter opens the chat line
            if (devMenuOpen && GUIUtility.keyboardControl != 0)
                Keybinds.ChatOpen = true;  // typing in a dock field (the stat filter, a value): the keyboard is the field's, no moving or casting
            else if (!chatOpen)
                Keybinds.ChatOpen = false;
            // Right click while the Gadget key is held cancels the throw (GadgetLook.cs); that click doesn't also parry or fire.
            if (CancelGadgetThrow())
            {
                if (Keybinds.Down(KeyAction.Parry))
                    parryOnMenu = true;
                if (Keybinds.Down(KeyAction.Fire))
                    fireOnMenu = true;
            }
            // Fire and Parry (a key or a mouse button, Keybinds) pressed while a menu was up stay off until let go.
            if ((overlay || emoteBarOpen || pingWheelOpen || gadgetWheelOpen) && Keybinds.Down(KeyAction.Fire))
                fireOnMenu = true;
            if (overlay && Keybinds.Down(KeyAction.Parry))
                parryOnMenu = true;
            if (!Keybinds.Held(KeyAction.Fire))
                fireOnMenu = false;
            if (!Keybinds.Held(KeyAction.Parry))
                parryOnMenu = false;
            fireBlocked = overlay || fireOnMenu || emoteBarOpen || pingWheelOpen || gadgetWheelOpen;
            parryBlocked = overlay || parryOnMenu;
            bool inMenus = session.Screen != AppScreen.Game;
            bool overlayPointer = choosing || menuUp || wikiPicking || tabHeld || pathChoice || shopping || MouseOnDock() || WaveBoxShown(me) || MouseOnWcStrip(me) || MouseOnBuilderShop(me)
                || game.State == MatchState.GameOver || game.State == MatchState.Victory;  // the end screen and rank report need the pointer
            // The system pointer is always hidden: wherever a pointer is wanted we draw the Corner pointer of your cursor look, and it
            // morphs into the crosshair when the run takes over (CursorLooks.cs).
            cursorWantPointer = inMenus || overlayPointer;
            Cursor.visible = false;
            StepCursorMorph(cursorWantPointer);
        }

        /// Dodge (its key or a double tap) and the ability keys, remembered until the next fixed step.
        void ReadPlayKeys()
        {
            // Dodge: its key, or a double tap of a movement key (also remembered between fixed steps).
            if (Keybinds.Down(KeyAction.SwapWeapon))
                wantSwap = true;  // a second-slot Calling (Core/SoldierCallings.cs)
            if (Keybinds.Down(KeyAction.Dodge))
            {
                wantDodge = true;
                dodgeKey = -1;
            }
            for (int mi = 0; mi < 4; mi++)
            {
                var ma = (KeyAction)mi;  // MoveUp..MoveRight
                if (!MoveDown(ma))
                    continue;
                if (lastTapAction == mi && Time.unscaledTime - lastTapTime <= DoubleTapWindow)
                {
                    wantDodge = true;
                    dodgeKey = mi;  // double tap and hold the key to sprint
                    lastTapAction = -1;
                }
                else
                {
                    lastTapAction = mi;
                    lastTapTime = Time.unscaledTime;
                }
            }
            // Remember ability presses between fixed steps so quick taps aren't lost.
            if (Keybinds.Down(KeyAction.Ability1))
            {
                wantAbility[0] = true;
                TileKeyPressed(0);  // still recharging: the tile shakes (HudTiles.cs)
            }
            if (Keybinds.Down(KeyAction.Ultimate))
            {
                wantAbility[Upgrades.UltSlot] = true;
                TileKeyPressed(Upgrades.UltSlot);  // still recharging: the tile shakes (HudTiles.cs)
            }
            if (Keybinds.Down(KeyAction.Ability2))
            {
                wantAbility[1] = true;
                TileKeyPressed(1);  // still recharging: the tile shakes (HudTiles.cs)
            }
            if (Keybinds.Down(KeyAction.Ability3))
            {
                wantAbility[2] = true;
                TileKeyPressed(2);  // still recharging: the tile shakes (HudTiles.cs)
            }
            if (Keybinds.Down(KeyAction.Ability4))
            {
                wantAbility[3] = true;
                TileKeyPressed(3);  // still recharging: the tile shakes (HudTiles.cs)
            }
        }

        /// Opens the folder with Player.log (Application.consoleLogPath), for sending it in when something looks wrong on one machine.
        static void OpenLogFolder()
        {
            try
            {
                string log = Application.consoleLogPath, dir = string.IsNullOrEmpty(log) ? null : System.IO.Path.GetDirectoryName(log);
                if (string.IsNullOrEmpty(dir) || !System.IO.Directory.Exists(dir))
                    dir = Application.persistentDataPath;  // no log path (editor, some platforms): the save folder is next to it
                Application.OpenURL("file:///" + dir.Replace('\\', '/'));
            }
            catch (System.Exception ex) { Diag.Warn("couldn't open the log folder", ex); }
        }

        /// Toggle keys (aim line, F2-F7, HUD, mute) and the screen transitions that tick every frame.
        void UpdateToggleKeys()
        {
            // Ignore the toggle keys while typing in a text box (e.g. a name with an "m" in it).
            if (Keybinds.Down(KeyAction.AimLine) && GUIUtility.keyboardControl == 0 && session.Screen == AppScreen.Game)
                SetAimLine(!aimLine);
            if (Input.GetKeyDown(KeyCode.N) && GUIUtility.keyboardControl == 0 && session.Screen == AppScreen.Game && !(game.RetryReady && game.State == MatchState.GameOver))
                SetNameLabels(!nameLabels);  // name labels over you and every enemy (EnemyTagLook.cs)
            TickDevTrapClicks();  // click to place a Dev Kit trap (DevTrapUi.cs)
            if (Input.GetKeyDown(KeyCode.F2) && session.Screen != AppScreen.Game)
                OpenLogFolder();  // outside a run F2 opens the folder holding Player.log
            SnapshotKeys();  // F6 / F7: take / load a run snapshot (SnapshotUi.cs)
            if (Keybinds.Down(KeyAction.ToggleHud) && GUIUtility.keyboardControl == 0 && session.Screen == AppScreen.Game)
                SetHudMode(hudMode + 1);  // H: corner > on the player > hidden (HudModes.cs)
            TickMapView(Time.unscaledDeltaTime);  // M: minimap > full map > off (MapView.cs)
            TickLaunch(Time.unscaledDeltaTime);  // a run starting: the drop-in (LaunchIntro.cs)
            TickMenuIntro(Time.unscaledDeltaTime);  // the main menu drawing itself in, the first time (MenuIntro.cs)
            TickMenuSounds(Time.unscaledDeltaTime);  // the menu's hum, and quitting after Quit's sound (MenuHolo.cs)
            TickLobbyEnter(Time.unscaledDeltaTime);  // the menu turning into the lobby (LobbyEnter.cs)
            TickModeBlink(Time.unscaledDeltaTime);   // the Game Mode screen blinking back to the lobby (ModeBlink.cs)
            TickRecall(Time.unscaledDeltaTime);      // back to the lobby from a run: the beam-out (Recall.cs)
            TickWindowClose();  // a menu window sliding out, then closing (WindowAnim.cs)
            TickTiles(Time.unscaledDeltaTime);   // the ability bar's tiles react (HudTiles.cs)
            TickMiniHud();  // the partial HUD easing in and out (HudModes.cs)
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
        }

        /// The upgrade picker: number keys, Enter and Reroll.
        void UpdateCardPicker(Player me, bool choosing)
        {
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
        }

        /// Keys used on things in the arena: a chest item, a mystery door, a gate, the kill-all key and the gadget.
        void UpdateWorldKeys(Player me, bool menuUp)
        {
            // Standing on your chest item: F (Interact) takes it, B throws it away (Props.cs).
            if (session.Screen == AppScreen.Game && !menuUp && !tabHeld && GUIUtility.keyboardControl == 0 && game.MyLootInReach != null)
            {
                if (Keybinds.Down(KeyAction.Interact))
                    game.ResolveLoot(true);
                else if (Keybinds.Down(KeyAction.DiscardItem))
                    game.ResolveLoot(false);
            }
            // By or in a Trojan Horse: F climbs in or drops out (TrojanHorse.cs).
            else if (session.Screen == AppScreen.Game && !menuUp && !tabHeld && GUIUtility.keyboardControl == 0 && game.HorseInteractable(game.Me)
                     && Keybinds.Down(KeyAction.Interact))
                game.HorseInteract();
            // By a Ballista, or on one: F climbs on or gets off (Ballista.cs).
            else if (session.Screen == AppScreen.Game && !menuUp && !tabHeld && GUIUtility.keyboardControl == 0 && game.Me != null && !game.Me.Downed
                     && (game.BallistaOf(game.Me) != null || game.BallistaNear(game.Me.Pos) != null) && Keybinds.Down(KeyAction.Interact))
                game.UseBallista();
            // By a sealed mystery door (the Expanding arena): F pays to open it, B saves it from expiring (MysteryDoors.cs).
            else if (session.Screen == AppScreen.Game && !menuUp && !tabHeld && GUIUtility.keyboardControl == 0 && game.Me != null && game.MysteryNear(game.Me.Pos) >= 0
                     && (Keybinds.Down(KeyAction.Interact) || Keybinds.Down(KeyAction.DiscardItem)))
                game.UseMysteryDoor(Keybinds.Down(KeyAction.DiscardItem));
            // On a Hangout spawner: F turns it on or off, B changes its enemy (the mouse wheel too: CameraZoom.cs; HangoutSpawners.cs). In
            // Practice (the lobby's window, HangoutUi.cs) while your keys play it.
            else if (PracticeLive && game.Me != null && game.SpawnerNear(game.Me.Pos) >= 0 && (Keybinds.Down(KeyAction.Interact) || Keybinds.Down(KeyAction.DiscardItem)))
                game.UseSpawner(game.SpawnerNear(game.Me.Pos), Keybinds.Down(KeyAction.Interact) ? 0 : 1);
            else if (session.Screen == AppScreen.Game && !menuUp && !tabHeld && GUIUtility.keyboardControl == 0 && game.Me != null && game.SpawnerNear(game.Me.Pos) >= 0
                     && (Keybinds.Down(KeyAction.Interact) || Keybinds.Down(KeyAction.DiscardItem)))
                game.UseSpawner(game.SpawnerNear(game.Me.Pos), Keybinds.Down(KeyAction.Interact) ? 0 : 1);
            // By a shutter gate (the Expanding arena): F shuts it (Gates.cs).
            else if (session.Screen == AppScreen.Game && !menuUp && !tabHeld && GUIUtility.keyboardControl == 0 && game.Me != null && game.GateNear(game.Me.Pos) >= 0
                     && Keybinds.Down(KeyAction.Interact))
                game.UseGate();
            // At a pedestal in the shop room: F buys it (ShopPedestalLook.cs).
            else if (session.Screen == AppScreen.Game && !menuUp && !tabHeld && GUIUtility.keyboardControl == 0 && !PedestalSellUp(game.Me) && TickPedestalKey(game.Me))
            {
            }
            // In a shop room you've left (Done shopping): F opens its screen again, any time between waves (ExpansionUi.cs DrawShopReopenHint).
            else if (session.Screen == AppScreen.Game && !menuUp && !tabHeld && GUIUtility.keyboardControl == 0 && ShopReopenable(game.Me)
                     && Keybinds.Down(KeyAction.Interact))
                game.Shop(ShopAction.Done, 0);
            // The F1 kill-all key (the discard key, B), when it's on: every enemy on the field dies as your kill. Not while you're standing
            // on a chest item (that key throws it away), and only the host can (DevTools.cs).
            else if (devKillAllKey && session.IsHost && session.Screen == AppScreen.Game && !menuUp && !tabHeld && GUIUtility.keyboardControl == 0
                     && Keybinds.Down(KeyAction.DiscardItem))
                game.DevKillAll(game.MyId);

            TickGadgetKey(me, session.Screen == AppScreen.Game && !menuUp && !tabHeld && GUIUtility.keyboardControl == 0);  // GadgetLook.cs
        }

        /// Esc on every screen, and the end-of-run keys (retry, restart, lobby, continue).
        void HandleEscAndEndKeys(Player me, bool menuUp)
        {
            bool escTaken = EscPressed && (pacEscFrame == Time.frameCount || HandleScreenEsc() || WikiPickEsc());  // (WikiPickEsc: Esc cancels the wiki's PICK)  // the top tool screen (F1 menu, Boss Lab setup) takes Esc first (UiScreens.cs)
            if (session.Screen != AppScreen.Game && EscPressed && !escTaken)
            {
                if (inspectId >= 0)
                    CloseInspect();
                else if (lobbyMenuOpen && (pauseSettings || pauseHowTo || pauseRecords))
                    MenuBack();  // a window closes first; then the lobby menu's Settings: back to its list
                else if (lobbyMenuOpen)
                    CloseLobbyMenu();
                else if (session.Screen == AppScreen.Lobby && OnModeScreen && !settingsOpen && GUIUtility.keyboardControl == 0)
                    LeaveModeScreen();  // Esc on the Game Mode screen: back to the lobby (never out of it; ModeBlink.cs)
                else if (session.Screen == AppScreen.Lobby && !settingsOpen && !LobbyEntering && !LobbyLeaving && !LobbyReturning && GUIUtility.keyboardControl == 0)
                    OpenLobbyMenu();  // Esc in the lobby: its menu (MenuDrawer.cs)
                else if (settingsOpen)
                {
                    if (Keybinds.Listening >= 0)
                        Keybinds.Listening = -1;  // a key waiting to be rebound: Esc only cancels that
                    else
                        CloseWindow(() => settingsOpen = false);  // back to the lobby or the main menu, the section's window going with it (WindowAnim.cs)
                }
                else if (profileOpen)
                {
                    if (editingName)
                    {
                        editingName = false;  // Esc ends renaming first
                        GUI.FocusControl(null);
                        SaveMenuFields();
                    }
                    else if (CabWindowOpen)
                        CloseCabWindow();  // the option's window slides back in first
                    else
                        CloseWindow(() => profileOpen = false);  // ProfileUi.cs
                }
                else if (menuSetOpen && session.Screen == AppScreen.Menu && MenuSettingsEsc())
                {
                    // the bay's settings: back to the tiles, or the shutter down (BaySettings.cs)
                }
                else if (menuWikiOpen && session.Screen == AppScreen.Menu && MenuWikiEsc())
                {
                    // the bay's wiki: back a page, or the shutter down (BayWiki.cs)
                }
                else if ((menuPlayOpen || menuJoinOpen) && session.Screen == AppScreen.Menu && GUIUtility.keyboardControl == 0)
                    PlayPickerBack();  // the case's bay: the shutter rolls back down (SavesUi.cs, MenuMachine.cs)
            }
            if (session.Screen == AppScreen.Game && !RecallActive && !escTaken)  // (nothing to open while everyone's beaming out: Recall.cs)
            {
                if (EscPressed && runStatsOpen)
                    runStatsOpen = false;  // back to the pause menu (RunStatsUi.cs)
                else if (EscPressed && pauseConfirmLobby && menuUp)
                    pauseConfirmLobby = false;  // "back to the lobby?": no (MenuDrawer.cs)
                else if (EscPressed && (pauseSettings || pauseHowTo || pauseRecords) && menuUp)
                    MenuBack();  // a window closes first; then the pause menu's Settings: back to its choices (MenuDrawer.cs)
                else if (EscPressed && !menuUp && ShopScreenUp(me))
                    ShopEsc();  // the shop / Black Market: a popup or category closes first, then Esc leaves it (ShopUi.cs)
                else if (EscPressed && !menuUp && PedestalSellUp(me))
                    shopSellOpen = false;  // the pedestal shop's sell list  // the shop / Black Market: a popup or category closes first, then Esc leaves it (ShopUi.cs)
                else if (EscPressed)
                {
                    if (menuUp)
                        ResumeGame();
                    else
                        OpenPauseMenu();
                }
                else if (session.IsHost && (game.State == MatchState.GameOver || game.State == MatchState.Victory) && !menuUp)
                {
                    if (game.RetryReady && game.State == MatchState.GameOver)
                    {
                        if (Input.GetKeyDown(KeyCode.Y))
                            session.RetryRun();  // a life left: respawn at the wave's start (Checkpoints.cs, LivesUi.cs)
                        else if (Input.GetKeyDown(KeyCode.N))
                            game.GiveUpRetry();  // the run ends: the results come up
                    }
                    else if (Input.GetKeyDown(KeyCode.R))
                        session.StartMatch();
                    else if (Input.GetKeyDown(KeyCode.L))
                        RecallToLobby();  // everyone beams out first (Recall.cs)
                    else if (Input.GetKeyDown(KeyCode.C) && game.Hunt == null)
                        game.ContinueEndless();
                }
            }
        }

        static readonly KeyCode[] CardKeys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8 };

        /// Menu theme on the main menu, the lobby theme in the lobby; the soft battle track in a run (or the coming boss's
        /// faction's track from the Music Pack); each boss's own track while
        /// it's on the field. The next boss's track is rendered ahead of time.
        void UpdateMusic()
        {
            TickDecodes();
            string wanted = session.Screen == AppScreen.Lobby ? MusicLibrary.Lobby : MusicLibrary.Menu, keep = null;
            if (session.Screen == AppScreen.Game && game.State != MatchState.GameOver && game.State != MatchState.Victory)
            {
                wanted = "battle";
                bool bossOn = false;
                foreach (var e in game.Enemies.Values)
                    if (e.Def.IsBoss)
                    {
                        var key = Game.IsReaper(e.Type) ? EnemyType.ReaperOfDusk : e.Type;
                        sounds.RequestBossTrack(key);
                        wanted = SoundBank.BossTrackId(key);
                        bossOn = true;
                        break;
                    }
                if (!bossOn)  // a faction mini-boss (the Riot Captain) has its own theme too
                    foreach (var e in game.Enemies.Values)
                        if (FactionMinis.Is(e.Type) && e.Hp > 0f)
                        {
                            sounds.RequestBossTrack(e.Type);
                            wanted = SoundBank.BossTrackId(e.Type);
                            break;
                        }
                int next = game.NextBossWave(game.Wave, game.State == MatchState.Fighting);
                var upcoming = game.BossAt(next);
                if (wanted == "battle")  // regular waves: the coming boss's faction's own track, if the Music Pack has one
                    wanted = sounds.RequestFactionTrack(upcoming) ?? wanted;
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
            session.Poll();
            var input = new PlayerInput
            {
                Move = FpMove(new V2(Axis(KeyAction.MoveRight) - Axis(KeyAction.MoveLeft), Axis(KeyAction.MoveDown) - Axis(KeyAction.MoveUp))).Normalized(),  // (keys move you on the screen: turned back with the camera, CameraRotate.cs; first person: where you look, FirstPerson.cs)
                AimAt = MouseArena(),
                Fire = Keybinds.Held(KeyAction.Fire) && !fireBlocked && !DevTrapPlacing,
                Ability = wantAbility[0] || Keybinds.Held(KeyAction.Ability1),
                AbilityQ = wantAbility[1] || Keybinds.Held(KeyAction.Ability2),
                AbilityE = wantAbility[2] || Keybinds.Held(KeyAction.Ability3),
                AbilityShift = wantAbility[3] || Keybinds.Held(KeyAction.Ability4),
                Ultimate = wantAbility[Upgrades.UltSlot] || Keybinds.Held(KeyAction.Ultimate),
                Parry = Keybinds.Held(KeyAction.Parry) && !parryBlocked,
                WeaponAction = Keybinds.Held(KeyAction.WeaponAction),
                Dodge = wantDodge,
                SwapWeapon = wantSwap,
                DodgeHeld = dodgeKey < 0 ? Keybinds.Held(KeyAction.Dodge) : Keybinds.MoveHeld((KeyAction)dodgeKey),
            };
            input.Steer = KnuckleWheelSteer(input.Fire);  // the Arc Knuckles' click wheel (ArcKnucklesWheel.cs)
            wantDodge = false;
            wantSwap = false;
            for (int i = 0; i < wantAbility.Length; i++)
                wantAbility[i] = false;
            if (pauseMenuOpen || game.Paused)
                input = new PlayerInput { AimAt = input.AimAt };  // no moving or shooting behind the menu
            if (session.Screen == AppScreen.Lobby && game.Practicing)
                input = PracticeInput(input);  // Practice behind the lobby: your keys only while its window is up and the mouse is over it (HangoutUi.cs)
            game.FixedStep(Time.fixedDeltaTime * EndSpeed, input);
            partyBots.Step(session, Time.fixedDeltaTime);  // (PartyBots.cs)
            ShowcaseFixedStep(Time.fixedDeltaTime);  // the lobby's loadout preview (LobbyUi.cs)
        }

        /// Forget the highlighted card whenever a new set of cards is shown.
        void SyncSelection(Player me)
        {
            string key = me.Offer == null || game.PickHeld(me) ? "" : string.Join(",", Array.ConvertAll(me.Offer, x => x.ToString())) + "/" + me.Taken.Count + "," + Game.HandCount(me) + "," + game.RoomTokens.Count;  // (a boss card pick changes the hand or kits, not Taken)
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
            if (me != null && me.Offer != null && !me.OfferCards && selectedCard < me.Offer.Length && (Upgrades.All[me.Offer[selectedCard]].Grants != AbilityId.None || Upgrades.All[me.Offer[selectedCard]].PicksSlot))
                key = pickKey;
            game.PickUpgrade(selectedCard, key);
            selectedCard = -1;
        }

        bool wantDodge, wantSwap;
        int lastTapAction = -1;
        int dodgeKey = -1;  // what started the last dodge: -1 the Dodge key, 0..3 a double-tapped movement key
        float lastTapTime;
        const float DoubleTapWindow = 0.25f;

        /// A movement key (or its arrow) went down this frame.
        static bool MoveDown(KeyAction a)
        {
            KeyCode arrow = a == KeyAction.MoveUp ? KeyCode.UpArrow : a == KeyAction.MoveDown ? KeyCode.DownArrow : a == KeyAction.MoveLeft ? KeyCode.LeftArrow : KeyCode.RightArrow;
            return Keybinds.Down(a) || (!Keybinds.Busy && Input.GetKeyDown(arrow));
        }

        static float Axis(KeyAction a) => Keybinds.MoveHeld(a) ? 1f : 0f;

        /// Esc, unless it just cancelled a rebind (Keybinds.cs).
        static bool EscPressed => Input.GetKeyDown(KeyCode.Escape) && !Keybinds.Busy;

        void UpdateLayout()
        {
            scale = Mathf.Min(UnityEngine.Screen.width / W, UnityEngine.Screen.height / H);
            offX = (UnityEngine.Screen.width - W * scale) / 2f;
            offY = (UnityEngine.Screen.height - H * scale) / 2f;
            RefreshCull();  // WorldRenderer.cs
        }

        V2 MouseArena() => fpView ? FpAimPoint() : ScreenToArena(new V2(Input.mousePosition.x, Input.mousePosition.y));  // (+ the camera lean, CameraLean.cs; the scroll-wheel zoom, CameraZoom.cs; a charge weapon's draw zoom, ChargeCameraFx.cs: ArcKnucklesWheel.cs)

        // --- Menus and HUD (IMGUI, arena coordinates) --------------------------

        /// The main menu under a side drawer (Settings, Profile, How to play): the box fades and drifts out as the drawer
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
            Ui.Init();
            Ui.Alpha = 1f;  // a fade that was left unrestored (an early return or an error mid-draw) must not carry into this frame: it left the round windows' dark layer and the path cards invisible
            GUI.color = Color.white;
            GUI.matrix = Matrix4x4.TRS(new Vector3(offX, offY, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));
            // With the F1 menu or the Cell Editor open, the screen underneath is only drawn, not used:
            // IMGUI hands a click to the first control drawn under it, so buttons hidden behind the panel
            // (the main menu's fields and buttons) would otherwise take clicks meant for the panel.
            Ui.MenuFont = session.Screen != AppScreen.Game;  // the menus use the mockup fonts; the HUD keeps the default (Ui.cs)
            ClickLogBegin();  // ClickLog.cs (F9)
            if (Event.current.type == EventType.MouseDown)
                Ui.ForgetPress();
            PickBeginGui();  // the text under the pointer, for the Wiki key's eyedropper (WikiPick.cs)
            WikiPickBeginGui();  // the Wiki key held: a click opens what's under the pointer (WikiPick.cs)
            Ui.BeginHoverFrame();
            bool modal = ModalScreenOpen;
            bool dockOwns = DockOwnsEvent();  // the F1 dock is drawn last but its clicks come first: the screen under it doesn't see them (DevDock.cs)
            Ui.NoHit = Event.current.type == EventType.Repaint && MouseOverDockRects(Event.current.mousePosition);  // (and nothing under it lights up on hover)
            if ((!modal && !dockOwns) || Event.current.type == EventType.Repaint)
            switch (session.Screen)
            {
                case AppScreen.Menu:
                    if (pacOpen)
                        DrawPacman();  // PacmanUi.cs
                    else if (profTr)
                        DrawProfTr();  // the picker and the menu trading places (ProfileTransition.cs)
                    else if (whoOpen)
                        DrawWho();  // ProfilesUi.cs
                    else if (settingsOpen)
                    {
                        DrawerIn("settings", true);  // clock first, as for the profile below
                        DrawMenuUnderDrawer(false);
                        DrawSettings();
                    }
                    else if (profileOpen)
                    {
                        DrawerIn("profile", true);  // start the window's clock first: the menu's fade-out under it reads WindowVis (a stale clock flashed the menu for a frame)
                        DrawMenuUnderDrawer(false);
                        DrawProfile();  // ProfileUi.cs
                    }
                    else
                        DrawMenu();
                    break;
                case AppScreen.Lobby:
                    if (LobbyCrtLeaving)
                    {
                        DrawLobbyCrtLeave();  // Leave with effects on: the CRT power-cycle, lobby off, menu on (LobbyCrtLeave.cs)
                        break;
                    }
                    // Settings (and the Field guide from it) over the lobby: the lobby is only drawn, not used, underneath.
                    DrawLobbyCrtBack(Time.unscaledTime);  // the cyan CRT screen behind the panels (LobbyCrt.cs)
                    if (LobbyReturning)
                        DrawLobbyReturning();  // back from a run: the dark lifting, the sides sliding in (Recall.cs)
                    else if (LobbyLeaving)
                        DrawLobbyLeavingToMenu();  // Leave: the way in, backwards (LobbyEnter.cs)
                    else if (LobbyEntering)
                    {
                        if (crtEntry)
                            DrawLobbyEnteringCrt();  // New run: the saved-runs screen opens into the lobby (LobbyCrt.cs)
                        else
                            DrawLobbyEntering();  // just in from the menu: the card becomes your loadout (LobbyEnter.cs)
                    }
                    else if (!settingsOpen && !lobbyMenuOpen && inspectId < 0 || Event.current.type == EventType.Repaint)
                        DrawLobby();
                    DrawLobbyCrtGlass(Time.unscaledTime);  // corners, highlight and bezel over it (LobbyCrt.cs)
                    if (inspectId >= 0 && !lobbyMenuOpen && !settingsOpen)
                        DrawInspect();  // a teammate's holo card (PlayerInspect.cs)
                    if (lobbyMenuOpen)
                    {
                        DrawLobbyMenu();  // Esc: back, Settings, How to play, quit (MenuDrawer.cs)
                    }
                    else if (settingsOpen)
                    {
                        Ui.Fill(new Rect(0, 0, W, H), new Color(0.01f, 0.01f, 0.03f, 0.75f * WindowVis));  // (fading with the drawer, WindowAnim.cs)
                        DrawSettings();
                    }
                    break;
                case AppScreen.Game:
                    Ui.MenuFont = false;
                    DrawHud();
                    DrawLaunchWave();    // WAVE 1 popping in as a launched run's fight starts (LaunchIntro.cs)
                    DrawLobbyLeaving();  // the lobby sliding away as the run launches (LaunchIntro.cs)
                    DrawPickOutro();  // an upgrade just taken: the cards flying back into you (PickOutro.cs)
                    if ((PauseMenuWanted || pauseAnim > 0f) && !ModalScreenOpen)
                    {
                        Ui.MenuFont = true;
                        if (runStatsOpen && PauseMenuWanted)
                            DrawRunStatsScreen(game.Me, true);  // RunStatsUi.cs
                        else
                            DrawPauseMenu();  // with its opening and closing (WindowAnim.cs)
                    }
                    Ui.MenuFont = false;
                    DrawRecallFade();  // back to the lobby: the screen going dark (Recall.cs)
                    break;
            }
            Ui.NoHit = false;
            DrawScreens();  // the top tool screen: the F1 menu or the Cell Editor (UiScreens.cs)
            DrawWikiPickTag();  // the eyedropper's tag by the pointer (WikiPick.cs)
            Ui.EndPick();
            DrawCabEdges();  // the cabinet menus' soft edges, over them (CrtWindows.cs)
            DrawSkipStatic();  // an intro just skipped: the channel flip over everything (SkipStatic.cs)
            DrawMenuCursor();  // the equipped crosshair over the menus (CursorLooks.cs)
            bool clicked = Ui.TakeClick();
            ClickLogEnd(clicked);
            if (Ui.EndHoverFrame() && sounds != null && !clicked)
                sounds.PlayUiHover(Time.unscaledTime, fxReduced);  // the glassy tick when the mouse moves onto a button
            if (clicked && sounds != null)
                sounds.PlayUiClick(Time.unscaledTime);  // and the chirp when one is pressed
        }
    }
}
