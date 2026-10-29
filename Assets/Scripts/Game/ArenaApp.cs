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
        int tabScroll; // Tab screen: rows scrolled in the upgrade list
        bool clickOnMenu, mouseBlocked;  // a mouse press that belongs to a menu, not the game
        bool recordSessions;             // dev: record each match for balancing (Settings or --record)
        bool aimDebug;                   // F3: lines for where you're aiming and where your last shot went
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
            LoadBackgroundSettings();
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
            game.Update(Time.deltaTime);
            LabUpdate(Time.deltaTime);  // Boss Lab bots (BossLabUi.cs)
            foreach (var sfx in game.Sounds)
                sounds.Play(sfx.Sfx, sfx.Ally ? 0.45f : 1f, Time.unscaledTime);
            game.Sounds.Clear();
            UpdateMusic();
            UpdateLayout();
            var me = game.Me;
            // If someone else resumes while our menu is open, close it.
            if (wasPaused && !game.Paused)
                pauseMenuOpen = false;
            wasPaused = game.Paused;
            if (session.Screen != AppScreen.Game)
                pauseMenuOpen = false;
            if (Input.GetKeyDown(KeyCode.F1))
            {
                devMenuOpen = !devMenuOpen;
                GUIUtility.keyboardControl = 0;
            }
            bool menuUp = pauseMenuOpen || game.Paused || devMenuOpen || labSetupOpen;
            bool choosing = me != null && me.Offer != null && !menuUp;
            // Clicks on menus (upgrade cards, pause menu, end-of-run screen) shouldn't also shoot or
            // parry: a press that starts while one is up is ignored until the button is released.
            // Holding Tab opens the loadout, which has buttons (moving abilities between keys). It works
            // while choosing an upgrade too: the cards hide until you let go.
            tabHeld = session.Screen == AppScreen.Game && me != null && !menuUp
                && Input.GetKey(KeyCode.Tab) && GUIUtility.keyboardControl == 0;
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
                    wantAbility[0] = true;
                if (Input.GetKeyDown(KeyCode.F))
                    wantAbility[Upgrades.UltSlot] = true;
                if (Input.GetKeyDown(KeyCode.Q))
                    wantAbility[1] = true;
                if (Input.GetKeyDown(KeyCode.E))
                    wantAbility[2] = true;
                if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
                    wantAbility[3] = true;
            }
            // Ignore the mute key while typing in a text box (e.g. a name with an "m" in it).
            if (Input.GetKeyDown(KeyCode.V) && GUIUtility.keyboardControl == 0 && session.Screen == AppScreen.Game)
                SetAimLine(!aimLine);
            if (Input.GetKeyDown(KeyCode.F3))
                aimDebug = !aimDebug;  // aim debug overlay
            TextEditKeys();  // F4: the dev text editor
            if (Input.GetKeyDown(KeyCode.H) && GUIUtility.keyboardControl == 0 && session.Screen == AppScreen.Game)
                hudShown = !hudShown;  // hide or show the HUD (Hud.cs)
            hudT = MathX.MoveTowards(hudT, hudShown ? 1f : 0f, Time.unscaledDeltaTime / HudFadeTime);
            if (Input.GetKeyDown(KeyCode.M) && GUIUtility.keyboardControl == 0)
            {
                sounds.Muted = !sounds.Muted;
                SaveAudioSettings();
            }

            if (choosing && !tabHeld)
            {
                SyncSelection(me);
                for (int k = 0; k < CardKeys.Length; k++)
                    if (Input.GetKeyDown(CardKeys[k]) && k < me.Offer.Length && selectedCard != k)
                    {
                        selectedCard = k;
                        pickKey = 0;
                    }
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) ConfirmPick();
                else if (Input.GetKeyDown(KeyCode.R)) game.RerollOffer();
            }

            if (session.Screen == AppScreen.Game)
            {
                if (Input.GetKeyDown(KeyCode.Escape) && labSetupOpen)
                    labSetupOpen = false;
                else if (Input.GetKeyDown(KeyCode.Escape) && devMenuOpen)
                    devMenuOpen = false;
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
            game.FixedStep(Time.fixedDeltaTime, input);
            LabFixedStep(Time.fixedDeltaTime);
            ShowcaseFixedStep(Time.fixedDeltaTime);  // the lobby's loadout preview (LobbyUi.cs)
        }

        /// Forget the highlighted card whenever a new set of cards is shown.
        void SyncSelection(Player me)
        {
            string key = me.Offer == null ? "" : string.Join(",", Array.ConvertAll(me.Offer, x => x.ToString())) + "/" + me.Taken.Count;
            if (key != shownOffer)
            {
                shownOffer = key;
                selectedCard = -1;
            }
        }

        void ConfirmPick()
        {
            if (selectedCard < 0 || game.RerollPending)
                return;
            var me = game.Me;
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
            bool modal = labSetupOpen || devMenuOpen || textEditOpen;
            if (!modal || Event.current.type == EventType.Repaint)
            switch (session.Screen)
            {
                case AppScreen.Menu:
                    if (settingsOpen)
                        DrawSettings();
                    else if (howToOpen)
                        DrawHowToPlay();
                    else
                        DrawMenu();
                    break;
                case AppScreen.Lobby:
                    DrawLobby();
                    break;
                case AppScreen.Game:
                    DrawHud();
                    if (LabRunning)
                        DrawLabPanel();
                    if ((pauseMenuOpen || game.Paused) && !devMenuOpen && !labSetupOpen)
                        DrawPauseMenu();
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
