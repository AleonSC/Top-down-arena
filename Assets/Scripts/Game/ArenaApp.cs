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
        bool clickOnMenu, mouseBlocked;  // a mouse press that belongs to a menu, not the game
        bool recordSessions;             // dev: record each match for balancing (Settings or --record)
        bool aimDebug;                   // F3: lines for where you're aiming and where your last shot went
        bool devKillAllKey;              // F1 dev tool: X kills every enemy on the field (host only; saved)
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
            Time.fixedDeltaTime = 1f / 60f;

            session = new Session();
            game = session.Game;
            game.Log = msg => Debug.Log(msg);
            sounds = new SoundBank(gameObject);
            sounds.StartMusic(gameObject);
            LoadAudioSettings();
            LoadFxSettings();  // Juice.cs
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
            ApplyRecording();
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
            session.Poll();
            game.Update(Time.deltaTime * EndSpeed);  // slowed as a run ends (RunEnding.cs)
            LabUpdate(Time.deltaTime);  // Boss Lab bots (BossLabUi.cs)
            foreach (var sfx in game.Sounds)
                sounds.Play(sfx.Sfx, sfx.Ally ? 0.45f : 1f, Time.unscaledTime);
            game.Sounds.Clear();
            UpdateMusic();
            UpdateLayout();
            TickEnding(Time.unscaledDeltaTime);  // the run ending: slow motion, then the results come in (RunEnding.cs)
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
            if (session.Screen == AppScreen.Game ? !pauseMenuOpen && !game.Paused : !settingsOpen)
                fieldGuideOpen = false;
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
            bool soloTab = tabKey && game.Players.Count == 1 && !LabRunning && !pauseMenuOpen && !devMenuOpen && !labSetupOpen
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
            bool menuUp = pauseMenuOpen || (game.Paused && !tabPaused) || devMenuOpen || labSetupOpen;
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
            if (overlay && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)))
                clickOnMenu = true;
            if (!Input.GetMouseButton(0) && !Input.GetMouseButton(1))
                clickOnMenu = false;
            mouseBlocked = overlay || clickOnMenu;
            Cursor.visible = session.Screen != AppScreen.Game || choosing || menuUp || tabHeld || pathChoice || shopping || watching || MouseOnLab();

            if (!menuUp)
            {
                // Remember ability presses between fixed steps so quick taps aren't lost.
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    wantAbility[0] = true;
                    TileKeyPressed(0);  // still recharging: the tile shakes (HudTiles.cs)
                }
                if (Input.GetKeyDown(KeyCode.F))
                {
                    wantAbility[Upgrades.UltSlot] = true;
                    TileKeyPressed(Upgrades.UltSlot);  // still recharging: the tile shakes (HudTiles.cs)
                }
                if (Input.GetKeyDown(KeyCode.Q))
                {
                    wantAbility[1] = true;
                    TileKeyPressed(1);  // still recharging: the tile shakes (HudTiles.cs)
                }
                if (Input.GetKeyDown(KeyCode.E))
                {
                    wantAbility[2] = true;
                    TileKeyPressed(2);  // still recharging: the tile shakes (HudTiles.cs)
                }
                if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
                {
                    wantAbility[3] = true;
                    TileKeyPressed(3);  // still recharging: the tile shakes (HudTiles.cs)
                }
            }
            // Ignore the mute key while typing in a text box (e.g. a name with an "m" in it).
            if (Input.GetKeyDown(KeyCode.V) && GUIUtility.keyboardControl == 0 && session.Screen == AppScreen.Game)
                SetAimLine(!aimLine);
            if (Input.GetKeyDown(KeyCode.F3))
                aimDebug = !aimDebug;  // aim debug overlay
            TextEditKeys();  // F4: the dev text editor
            if (Input.GetKeyDown(KeyCode.H) && GUIUtility.keyboardControl == 0 && session.Screen == AppScreen.Game)
                hudShown = !hudShown;  // hide or show the HUD (Hud.cs)
            TickLaunch(Time.unscaledDeltaTime);  // a run starting: the drop-in (LaunchIntro.cs)
            TickLobbyEnter(Time.unscaledDeltaTime);  // the menu turning into the lobby (LobbyEnter.cs)
            TickWindowClose();  // a menu window sliding out, then closing (WindowAnim.cs)
            TickTiles(Time.unscaledDeltaTime);   // the ability bar's tiles react (HudTiles.cs)
            hudT = MathX.MoveTowards(hudT, hudShown && !LaunchHoldsHud && !BossIntroHoldsHud ? 1f : 0f, Time.unscaledDeltaTime / HudFadeTime);  // (a boss's entrance card hides it: Juice.cs)
            if (BossIntroHoldsHud)
                introHidHud = true;
            else if (hudT >= 1f || !hudShown)
                introHidHud = false;
            if (Input.GetKeyDown(KeyCode.M) && GUIUtility.keyboardControl == 0)
            {
                sounds.Muted = !sounds.Muted;
                SaveAudioSettings();
            }

            if (choosing && !tabHeld && !PickOutroPlaying)
            {
                SyncSelection(me);
            }
            TickPickSounds(me, choosing && session.Screen == AppScreen.Game);  // the picker opening and closing (PickSounds.cs)
            if (choosing && !tabHeld && !PickOutroPlaying && !PickIntroPlaying(me))  // the cards are still flying out (PickIntro.cs)
            {
                for (int k = 0; k < CardKeys.Length; k++)
                    if (Input.GetKeyDown(CardKeys[k]) && k < me.Offer.Length && selectedCard != k)
                    {
                        selectedCard = k;
                        pickKey = 0;
                    }
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) ConfirmPick();
                else if (Input.GetKeyDown(KeyCode.R) && !RerollLocked) game.RerollOffer();
            }
            // Standing on your chest item: G takes it, X throws it away (Props.cs).
            if (session.Screen == AppScreen.Game && !menuUp && !tabHeld && GUIUtility.keyboardControl == 0 && game.MyLootInReach != null)
            {
                if (Input.GetKeyDown(KeyCode.G))
                    game.ResolveLoot(true);
                else if (Input.GetKeyDown(KeyCode.X))
                    game.ResolveLoot(false);
            }
            // The F1 kill-all key (X), when it's on: every enemy on the field dies as your kill. Not while you're standing
            // on a chest item (X throws that away), and only the host can (DevTools.cs).
            else if (devKillAllKey && session.IsHost && session.Screen == AppScreen.Game && !menuUp && !tabHeld && GUIUtility.keyboardControl == 0
                     && Input.GetKeyDown(KeyCode.X))
                game.DevKillAll(game.MyId);

            if (session.Screen != AppScreen.Game && Input.GetKeyDown(KeyCode.Escape) && !devMenuOpen)
            {
                if (fieldGuideOpen)
                    CloseFieldGuide();  // back to Settings
                else if (settingsOpen)
                    CloseWindow(() => settingsOpen = false);  // back to the lobby or the main menu, sliding out (WindowAnim.cs)
                else if (howToOpen)
                    CloseWindow(() => howToOpen = false);
                else if (recordsOpen)
                    CloseWindow(() => recordsOpen = false);
            }
            if (session.Screen == AppScreen.Game)
            {
                if (Input.GetKeyDown(KeyCode.Escape) && labSetupOpen)
                    labSetupOpen = false;
                else if (Input.GetKeyDown(KeyCode.Escape) && runStatsOpen && !devMenuOpen)
                    runStatsOpen = false;  // back to the pause menu (RunStatsUi.cs)
                else if (Input.GetKeyDown(KeyCode.Escape) && fieldGuideOpen && !devMenuOpen)
                    CloseFieldGuide();  // back to the pause menu (FieldGuide.cs)
                else if (Input.GetKeyDown(KeyCode.Escape) && devMenuOpen)
                    devMenuOpen = false;
                else if (Input.GetKeyDown(KeyCode.Escape) && pauseSettings && menuUp)
                    pauseSettings = false;  // the pause menu's Settings: back to its choices (MenuDrawer.cs)
                else if (Input.GetKeyDown(KeyCode.Escape))
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
                        session.ReturnToLobby();
                    else if (Input.GetKeyDown(KeyCode.C))
                        game.ContinueEndless();
                }
            }
        }

        static readonly KeyCode[] CardKeys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8 };

        /// Menu theme on the menus; the soft battle track in a match; each boss's own track while
        /// it's on the field. The next boss's track is rendered ahead of time.
        void UpdateMusic()
        {
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
                int next = game.Wave % 5 == 0 && game.State == MatchState.Fighting ? game.Wave : (game.Wave / 5 + 1) * 5;
                var upcoming = Game.BossForWave(next);
                sounds.RequestBossTrack(upcoming);
                keep = SoundBank.BossTrackId(upcoming);
            }
            sounds.UpdateMusic(wanted, keep, Time.unscaledDeltaTime);
        }

        void FixedUpdate()
        {
            session.Poll();
            var input = new PlayerInput
            {
                Move = new V2(Axis(KeyCode.D, KeyCode.RightArrow) - Axis(KeyCode.A, KeyCode.LeftArrow),
                              Axis(KeyCode.S, KeyCode.DownArrow) - Axis(KeyCode.W, KeyCode.UpArrow)).Normalized(),
                AimAt = MouseArena(),
                Fire = Input.GetMouseButton(0) && !mouseBlocked,
                Ability = wantAbility[0] || Input.GetKey(KeyCode.Space),
                AbilityQ = wantAbility[1] || Input.GetKey(KeyCode.Q),
                AbilityE = wantAbility[2] || Input.GetKey(KeyCode.E),
                AbilityShift = wantAbility[3] || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift),
                Ultimate = wantAbility[Upgrades.UltSlot] || Input.GetKey(KeyCode.F),
                Parry = Input.GetMouseButton(1) && !mouseBlocked,
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
            }
        }

        void ConfirmPick()
        {
            var me = game.Me;
            if (selectedCard < 0 || game.RerollPending || PickOutroPlaying || PickIntroPlaying(me))
                return;
            StartPickOutro(me, selectedCard);  // the cards fly back into you, yours last (PickOutro.cs)
            int key = 0;
            if (me != null && me.Offer != null && selectedCard < me.Offer.Length && Upgrades.All[me.Offer[selectedCard]].Grants != AbilityId.None)
                key = pickKey;
            game.PickUpgrade(selectedCard, key);
            selectedCard = -1;
        }

        static float Axis(KeyCode a, KeyCode b) => Input.GetKey(a) || Input.GetKey(b) ? 1f : 0f;

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
            return new V2((m.x - offX) / ws, (UnityEngine.Screen.height - m.y - offY) / ws);
        }

        // --- Menus and HUD (IMGUI, arena coordinates) --------------------------

        void InitStyles()
        {
            if (labelStyle != null)
                return;
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
            labelStyle.normal.textColor = new Color(0.9f, 0.92f, 1f);
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
            bool modal = labSetupOpen || devMenuOpen || textEditOpen;
            if (!modal || Event.current.type == EventType.Repaint)
            switch (session.Screen)
            {
                case AppScreen.Menu:
                    if (settingsOpen && fieldGuideOpen)
                        DrawFieldGuideScreen();  // FieldGuide.cs
                    else if (settingsOpen)
                        DrawSettings();
                    else if (howToOpen)
                        DrawHowToPlay();
                    else if (continueOpen)
                        DrawContinue();  // SavesUi.cs
                    else if (recordsOpen)
                        DrawRecords();
                    else
                        DrawMenu();
                    break;
                case AppScreen.Lobby:
                    // Settings (and the Field guide from it) over the lobby: the lobby is only drawn, not used, underneath.
                    if (LobbyLeaving)
                        DrawLobbyLeavingToMenu();  // Leave: the way in, backwards (LobbyEnter.cs)
                    else if (LobbyEntering)
                        DrawLobbyEntering();  // just in from the menu: the card becomes your loadout (LobbyEnter.cs)
                    else if (!settingsOpen || Event.current.type == EventType.Repaint)
                        DrawLobby();
                    if (settingsOpen)
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
                    if ((PauseMenuWanted || pauseAnim > 0f) && !devMenuOpen && !labSetupOpen)
                    {
                        if (fieldGuideOpen && PauseMenuWanted)
                            DrawFieldGuideScreen();  // FieldGuide.cs
                        else if (runStatsOpen && PauseMenuWanted)
                            DrawRunStatsScreen(game.Me, true);  // RunStatsUi.cs
                        else
                            DrawPauseMenu();  // with its opening and closing (WindowAnim.cs)
                    }
                    break;
            }
            if (!textEditOpen || Event.current.type == EventType.Repaint)
            {
                if (labSetupOpen)
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
