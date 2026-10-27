using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Arena
{
    /// Unity front end: reads input, steps the Session/Game, draws the arena
    /// with GL and the menus/HUD with IMGUI. Creates itself when any scene
    /// starts, so there is nothing to set up in the editor.
    public class ArenaApp : MonoBehaviour
    {
        // Menus and the HUD are laid out on a fixed 1280x720 canvas; the arena itself is bigger
        // and is scaled down to fill the same area.
        const float W = 1280f, H = 720f;
        const float AW = Game.Width, AH = Game.Height;
        const float WorldToUi = W / AW;

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
        bool aimLine = true;             // V: a soft line from your gun toward the crosshair (saved per player)

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
        string shownOffer = "";
        readonly bool[] wantAbility = new bool[Upgrades.SlotCount];
        SoundBank sounds;

        Material glMaterial;
        float scale = 1f, offX, offY;  // UI canvas -> screen pixels (top-left origin)
        float drawScale = 1f;          // GL coordinates -> screen pixels: the arena in a match, the UI canvas on menus
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
                for (int i = 0; i <= (int)Kit.Summoner; i++)
                    if (string.Equals(((Kit)i).ToString(), v, StringComparison.OrdinalIgnoreCase))
                        kitChoice = (Kit)i;
            if (opts.TryGetValue("autostart", out v)) int.TryParse(v, out session.AutoStart);
            if (opts.TryGetValue("start-wave", out v)) int.TryParse(v, out session.StartWave);
            game.Bot = opts.ContainsKey("bot");
            recordSessions = PlayerPrefs.GetInt("record_sessions", 0) == 1 || opts.ContainsKey("record");
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

        void OnDestroy()
        {
            session.Leave("");
        }

        // --- Loop --------------------------------------------------------------

        void Update()
        {
            session.Poll();
            game.Update(Time.deltaTime);
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
            bool menuUp = pauseMenuOpen || game.Paused;
            bool choosing = me != null && me.Offer != null && !menuUp;
            // Clicks on menus (upgrade cards, pause menu, end-of-run screen) shouldn't also shoot or
            // parry: a press that starts while one is up is ignored until the button is released.
            bool overlay = menuUp || choosing || game.State == MatchState.GameOver || game.State == MatchState.Victory;
            if (overlay && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)))
                clickOnMenu = true;
            if (!Input.GetMouseButton(0) && !Input.GetMouseButton(1))
                clickOnMenu = false;
            mouseBlocked = overlay || clickOnMenu;
            Cursor.visible = session.Screen != AppScreen.Game || choosing || menuUp;
            tabHeld = session.Screen == AppScreen.Game && me != null && !menuUp && !choosing
                && Input.GetKey(KeyCode.Tab) && GUIUtility.keyboardControl == 0;

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
            if (Input.GetKeyDown(KeyCode.M) && GUIUtility.keyboardControl == 0)
            {
                sounds.Muted = !sounds.Muted;
                SaveAudioSettings();
            }

            if (choosing)
            {
                SyncSelection(me);
                for (int k = 0; k < CardKeys.Length; k++)
                    if (Input.GetKeyDown(CardKeys[k]) && k < me.Offer.Length)
                        selectedCard = k;
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) ConfirmPick();
                else if (Input.GetKeyDown(KeyCode.R)) game.RerollOffer();
            }

            if (session.Screen == AppScreen.Game)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
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
                        var key = Game.IsTwin(e.Type) ? EnemyType.TwinA : e.Type;
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
            game.PickUpgrade(selectedCard);
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

        // --- World drawing (GL immediate mode, arena coordinates) -------------

        void OnPostRender()
        {
            if (glMaterial == null)
            {
                glMaterial = new Material(Shader.Find("Hidden/Internal-Colored")) { hideFlags = HideFlags.HideAndDontSave };
                glMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                glMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                glMaterial.SetInt("_Cull", (int)CullMode.Off);
                glMaterial.SetInt("_ZWrite", 0);
                glMaterial.SetInt("_ZTest", (int)CompareFunction.Always);
            }
            UpdateLayout();
            glMaterial.SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix();
            GL.Begin(GL.TRIANGLES);
            drawScale = game.Running ? scale * WorldToUi : scale;
            if (game.Running)
                DrawWorld();
            else
                DrawMenuBackground(Time.time);
            GL.End();
            GL.PopMatrix();
        }

        /// Behind the menus: a slow drifting grid and a boss-like spiral of glowing bullets.
        void DrawMenuBackground(float t)
        {
            FillRect(0, 0, W, H, new Rgba(0.035f, 0.04f, 0.065f));
            var grid = new Rgba(0.4f, 0.55f, 1f, 0.05f);
            float scroll = MathX.Fmod(t * 12f, 64f);
            for (float x = -64f + scroll; x <= W; x += 64f)
                Line(new V2(x, 0), new V2(x, H), 1f, grid);
            for (float y = -64f + scroll; y <= H; y += 64f)
                Line(new V2(0, y), new V2(W, y), 1f, grid);
            // Soft colored glows.
            Circle(new V2(W * 0.25f + 60f * Mathf.Sin(t * 0.2f), H * 0.35f), 260f, new Rgba(0.3f, 0.45f, 1f, 0.05f), 48);
            Circle(new V2(W * 0.78f, H * 0.7f + 50f * Mathf.Cos(t * 0.17f)), 300f, new Rgba(0.8f, 0.35f, 0.9f, 0.04f), 48);
            // A rotating five-armed spiral of bullets from the center.
            var center = new V2(W / 2f, H / 2f);
            for (int arm = 0; arm < 5; arm++)
                for (int k = 0; k < 14; k++)
                {
                    float dist = MathX.Fmod(t * 60f + k * 52f, 14f * 52f);
                    float ang = t * 0.25f + arm * MathX.Tau / 5f + dist * 0.004f;
                    V2 pos = center + V2.FromAngle(ang) * dist;
                    float a = 0.35f * MathX.Clamp01(dist / 120f) * (1f - dist / (14f * 52f));
                    var c = arm % 2 == 0 ? new Rgba(1f, 0.35f, 0.5f) : new Rgba(0.45f, 0.65f, 1f);
                    Circle(pos, 7f, c.WithAlpha(a * 0.5f), 10);
                    Circle(pos, 3.5f, c.WithAlpha(a), 8);
                }
        }

        void DrawWorld()
        {
            float t = game.AnimT;
            FillRect(0, 0, AW, AH, new Rgba(0.05f, 0.06f, 0.09f));
            var grid = new Rgba(1f, 1f, 1f, 0.035f);
            for (float x = 0; x <= AW; x += 64f)
                Line(new V2(x, 0), new V2(x, AH), 1f, grid);
            for (float y = 0; y <= AH; y += 64f)
                Line(new V2(0, y), new V2(AW, y), 1f, grid);
            var border = new Rgba(0.3f, 0.35f, 0.5f, 0.6f);
            Line(new V2(2, 2), new V2(AW - 2, 2), 3f, border);
            Line(new V2(2, AH - 2), new V2(AW - 2, AH - 2), 3f, border);
            Line(new V2(2, 2), new V2(2, AH - 2), 3f, border);
            Line(new V2(AW - 2, 2), new V2(AW - 2, AH - 2), 3f, border);

            foreach (var p in game.Players.Values)
            {
                if (p.Downed)
                    continue;
                if (p.Role == Role.Healer)
                {
                    Circle(p.Disp, p.AuraRange, new Rgba(0.3f, 1f, 0.5f, 0.03f), 64);
                    Arc(p.Disp, p.AuraRange, 2f, new Rgba(0.3f, 1f, 0.5f, 0.18f), 64);
                }
                if (p.BarrierT > 0f)
                {
                    // Fades as it soaks damage; flickers when nearly broken or about to expire.
                    float strength = MathX.Clamp01(p.BarrierFrac);
                    bool failing = p.BarrierT < 1f || strength < 0.25f;
                    float a = !failing || MathX.Fmod(t, 0.2f) < 0.1f ? 0.3f + 0.5f * strength : 0.15f;
                    Circle(p.Disp, p.BarrierRadius, new Rgba(0.4f, 0.7f, 1f, 0.04f + 0.1f * strength), 64);
                    Arc(p.Disp, p.BarrierRadius, 1.5f + 2.5f * strength, new Rgba(0.5f, 0.8f, 1f, a), 64);
                }
                if (p.OverdriveT > 0f)
                    Arc(p.Disp, p.Def.BodyR + 7f + 2f * Mathf.Sin(t * 20f), 2.5f, new Rgba(1f, 0.4f, 0.3f, 0.8f), 32);
                if (p.AegisT > 0f)  // Arcane Aegis: a spinning hexagon
                    for (int k = 0; k < 6; k++)
                        Line(p.Disp + V2.FromAngle(t * 3f + MathX.Tau * k / 6f) * (p.Def.BodyR + 22f),
                             p.Disp + V2.FromAngle(t * 3f + MathX.Tau * (k + 1) / 6f) * (p.Def.BodyR + 22f), 3f, new Rgba(0.8f, 0.5f, 1f, 0.85f));
                if (p.ShieldHp > 0f)  // Arcane Ward
                    Arc(p.Disp, p.Def.BodyR + 5f, 3f, new Rgba(0.5f, 0.7f, 1f, 0.4f + 0.5f * Mathf.Min(1f, p.ShieldHp / 60f)), 32);
                if (p.SurgeT > 0f)  // Arcane Surge
                    Circle(p.Disp, p.Def.BodyR + 10f + 3f * Mathf.Sin(t * 12f), new Rgba(0.75f, 0.45f, 1f, 0.18f), 32);
                if (p.FrenzyT > 0f)  // Alpha Call
                    Arc(p.Disp, p.Def.BodyR + 9f, 2f, new Rgba(1f, 0.5f, 0.3f, 0.7f), 16, t * 6f, MathX.Pi);
                if (p.ParryActive)  // the parry shield: an arc toward the cursor that dims as it wears
                {
                    float hp = Mathf.Clamp01(p.ParryHp / Game.ParryMax(p));
                    float rr = p.Def.BodyR + Game.ParryReach * 0.55f;
                    var sc = new Rgba(0.55f, 0.9f, 1f);
                    float from = p.Aim - Game.ParryArc(p), span = Game.ParryArc(p) * 2f;
                    Arc(p.Disp, rr, 14f, sc.WithAlpha(0.3f + 0.25f * hp), 20, from, span);            // the shield's face
                    Arc(p.Disp, rr + 7f, 4f, sc.Mix(Rgba.White, 0.6f).WithAlpha(0.7f + 0.3f * hp), 20, from, span);  // bright rim
                    for (int k = 0; k <= 1; k++)  // end caps
                        Circle(p.Disp + V2.FromAngle(from + span * k) * (rr + 2f), 4f, sc.Mix(Rgba.White, 0.4f), 8);
                    if (hp < 0.35f && MathX.Fmod(t, 0.2f) < 0.1f)  // about to break
                        Arc(p.Disp, rr + 7f, 4f, new Rgba(1f, 0.4f, 0.4f, 0.9f), 20, from, span);
                }
                else if (p.ParryBroken > 0f && !p.Downed)  // broken: cracked pieces until it's back
                    for (int k = 0; k < 3; k++)
                        Arc(p.Disp, p.Def.BodyR + 9f, 2f, new Rgba(1f, 0.45f, 0.45f, 0.35f), 4, p.Aim - 0.8f + k * 0.6f, 0.35f);
                if (p.UltT > 0f && p.Slots[Upgrades.UltSlot].Id == AbilityId.Fortress)  // golden bulwark
                {
                    Circle(p.Disp, p.Def.BodyR + 16f, new Rgba(1f, 0.8f, 0.35f, 0.15f), 32);
                    Polygon(p.Disp, p.Def.BodyR + 14f, 6, t * 0.8f, new Rgba(1f, 0.8f, 0.35f, 0.12f));
                    Arc(p.Disp, p.Def.BodyR + 16f, 3f, new Rgba(1f, 0.85f, 0.4f, 0.9f), 6, t * 0.8f + MathX.Pi / 6f, MathX.Tau);
                }
                if (p.UltT > 0f && p.Slots[Upgrades.UltSlot].Id == AbilityId.BulletStorm)  // spinning muzzle sparks
                    for (int k = 0; k < 3; k++)
                        Arc(p.Disp, p.Def.BodyR + 7f + k * 4f, 2f, new Rgba(1f, 0.6f, 0.25f, 0.8f - k * 0.2f), 12, t * (8f + k * 3f), 1.2f);
            }

            foreach (var b in game.Bullets.Values)
            {
                if (b.Kind >= Defs.SmallShot)
                    continue;
                DrawShotMods(b, t);
                if (b.Kind == Defs.GrenadeShot)  // spinning grenade with a blinking fuse
                {
                    Polygon(b.Pos, b.Radius, 6, t * 12f, Defs.BulletColor(b.Kind));
                    if (MathX.Fmod(t, 0.15f) < 0.075f)
                        Circle(b.Pos, 3f, new Rgba(1f, 0.3f, 0.2f), 8);
                    continue;
                }
                if (b.Kind == Defs.FireShot || b.Kind == Defs.HolyShot || b.Kind == Defs.ArcaneShot || b.Kind == Defs.FrostShot)
                    Circle(b.Pos, b.Radius * (1.8f + 0.3f * Mathf.Sin(t * 30f + b.Id)), Defs.BulletColor(b.Kind).WithAlpha(0.25f), 12);
                if (b.Kind == Defs.RailShot)
                    Line(b.Pos - b.Vel * 0.03f, b.Pos, 3f, Defs.BulletColor(b.Kind).WithAlpha(0.6f));
                Circle(b.Pos, b.Radius, Defs.BulletColor(b.Kind).WithAlpha(0.8f), 10);
            }

            foreach (var z in game.Zones.Values)
                DrawZone(z, t);
            foreach (var h in game.Hazards.Values)  // incoming lightning strike: a filling warning circle
            {
                var hc = new Rgba(0.7f, 0.85f, 1f);
                float f = 1f - MathX.Clamp01(h.T / Mathf.Max(0.01f, h.Duration));
                Circle(h.Pos, h.Radius * f, hc.WithAlpha(0.25f), 32);
                Arc(h.Pos, h.Radius, 2.5f, (f > 0.7f && MathX.Fmod(t, 0.1f) < 0.05f ? Rgba.White : hc).WithAlpha(0.85f), 40);
            }
            foreach (var p in game.Players.Values)
            {
                Player q;
                if (p.LinkT > 0f && p.LinkTo != p.Id && game.Players.TryGetValue(p.LinkTo, out q))
                {
                    // Soul Link: a wavy tether between healer and ally.
                    V2 a = p.Disp, b = q.Disp, n = (b - a).Normalized().Orthogonal();
                    var c = new Rgba(0.6f, 1f, 0.9f, 0.8f);
                    V2 prev = a;
                    for (int k = 1; k <= 16; k++)
                    {
                        float f = k / 16f;
                        V2 pt = V2.Lerp(a, b, f) + n * (Mathf.Sin(f * 12f - t * 10f) * 5f * Mathf.Sin(f * MathX.Pi));
                        Line(prev, pt, 2.5f, c);
                        prev = pt;
                    }
                }
                if (p.TauntT > 0f && !p.Downed)  // Roar
                    Arc(p.Disp, p.Def.BodyR + 12f + 3f * Mathf.Sin(t * 16f), 3f, new Rgba(1f, 0.4f, 0.25f, 0.85f), 32);
            }
            foreach (var minion in game.Minions.Values)
                DrawMinion(minion, t);
            foreach (var o in game.Orbs.Values)
            {
                // Blink during the last 3 seconds before the orb disappears.
                if (o.Life < 3f && MathX.Fmod(t, 0.25f) < 0.1f)
                    continue;
                float pulse = 1f + 0.12f * Mathf.Sin(t * (o.Big ? 6f : 8f) + o.Id);
                float r = o.Radius * pulse;
                var green = new Rgba(0.35f, 1f, 0.5f);
                Circle(o.Disp, r * 2.2f, green.WithAlpha(o.Big ? 0.18f : 0.12f), 20);
                Circle(o.Disp, r, green, 16);
                Line(o.Disp - new V2(r * 0.55f, 0), o.Disp + new V2(r * 0.55f, 0), o.Big ? 3f : 2f, Rgba.White);
                Line(o.Disp - new V2(0, r * 0.55f), o.Disp + new V2(0, r * 0.55f), o.Big ? 3f : 2f, Rgba.White);
                if (o.Big)
                    Arc(o.Disp, r + 5f, 2f, new Rgba(1f, 0.95f, 0.5f, 0.8f), 24);
            }
            DrawReaperTether(t);
            foreach (var e in game.Enemies.Values)
                DrawEnemy(e, t);
            foreach (var p in game.Players.Values)
            {
                DrawPlayer(p, t);
                // Near an edge, also draw the part of you that's already coming in on the far side.
                V2 real = p.Disp;
                float edge = p.Def.BodyR + 12f;
                float dx = real.X < edge ? AW : real.X > AW - edge ? -AW : 0f;
                float dy = real.Y < edge ? AH : real.Y > AH - edge ? -AH : 0f;
                var ghosts = new List<V2>();
                if (dx != 0f) ghosts.Add(new V2(dx, 0f));
                if (dy != 0f) ghosts.Add(new V2(0f, dy));
                if (dx != 0f && dy != 0f) ghosts.Add(new V2(dx, dy));  // corners
                foreach (var off in ghosts)
                {
                    p.Disp = real + off;
                    DrawPlayer(p, t);
                }
                p.Disp = real;
            }

            foreach (var b in game.Bullets.Values)
            {
                if (b.Kind < Defs.SmallShot)
                    continue;
                if (!Defs.IsEnemyShot(b.Kind))
                    DrawShotMods(b, t);
                Rgba c = Defs.BulletColor(b.Kind);
                if (b.Kind == Defs.PierceShot)  // a boss's unblockable shot: red with a dark core
                {
                    Circle(b.Pos, b.Radius + 4f, c.WithAlpha(0.35f), 12);
                    Circle(b.Pos, b.Radius, c, 12);
                    Circle(b.Pos, b.Radius * 0.5f, new Rgba(0.1f, 0f, 0f, 1f), 8);
                    continue;
                }
                if (b.FromBoss)  // other boss shots: violet glow, spinning spikes, dark core (they wear blockers down)
                {
                    Circle(b.Pos, b.Radius + 5f, BossShotGlow.WithAlpha(0.3f), 12);
                    Polygon(b.Pos, b.Radius + 4f, 4, t * 5f + b.Id, BossShotGlow);
                    Circle(b.Pos, b.Radius, c, 12);
                    Circle(b.Pos, b.Radius * 0.5f, new Rgba(0.08f, 0f, 0.12f, 1f), 8);
                    continue;
                }
                if (b.Kind == Defs.MineShot)  // mortar bomb: pulses faster as it's about to burst
                    Arc(b.Pos, b.Radius + 5f + 3f * Mathf.Sin(t * (10f + 20f * (1.3f - b.Life))), 2f, c.WithAlpha(0.8f), 16);
                Circle(b.Pos, b.Radius + 2.5f, c.WithAlpha(0.3f), 12);
                Circle(b.Pos, b.Radius, c, 12);
                Circle(b.Pos, b.Radius * 0.45f, new Rgba(1f, 1f, 1f, 0.9f), 8);
            }

            foreach (var fx in game.Effects)
            {
                float f = fx.T / fx.Dur;
                switch (fx.Kind)
                {
                    case FxKind.Boom:
                        Circle(fx.Pos, fx.Radius * (0.4f + 0.6f * f), fx.Color.WithAlpha(0.35f * (1f - f)), 24);
                        Arc(fx.Pos, fx.Radius * (0.5f + f), 3f, fx.Color.WithAlpha(1f - f), 32);
                        break;
                    case FxKind.Spark:
                        Arc(fx.Pos, Mathf.Max(1f, fx.Radius * f), 2f, fx.Color.WithAlpha(1f - f), 12);
                        break;
                    case FxKind.Ring:
                        Arc(fx.Pos, fx.Radius * (0.2f + 0.8f * f), 4f, fx.Color.WithAlpha(1f - f), 64);
                        break;
                    case FxKind.Bolt:  // jagged lightning between two points
                    {
                        V2 a = fx.Pos, b = fx.Pos2, n = (b - a).Normalized().Orthogonal();
                        V2 prev = a;
                        for (int k = 1; k <= 6; k++)
                        {
                            V2 pt = k == 6 ? b : V2.Lerp(a, b, k / 6f) + n * (((k * 37 + fx.Pos.X) % 17f) - 8.5f);
                            Line(prev, pt, 3f, fx.Color.WithAlpha(1f - f));
                            prev = pt;
                        }
                        break;
                    }
                }
            }

            if (game.State == MatchState.GameOver || game.State == MatchState.Victory)
                FillRect(0, 0, AW, AH, new Rgba(0f, 0f, 0f, 0.45f));

            if (aimLine && game.Me != null && !game.Me.Downed && !game.Me.ParryActive)
                DrawAimLine(game.Me, MouseArena(), t);

            if (aimDebug && game.Me != null)
            {
                // Cyan: from you through the crosshair. Yellow: along your last shot as the host fired it.
                var me = game.Me;
                Line(me.Disp, me.Disp + V2.FromAngle(me.Aim) * 1100f, 1.5f, new Rgba(0.3f, 0.9f, 1f, 0.7f));
                if (game.LastShotId != 0)
                {
                    Line(game.LastShotFrom, game.LastShotFrom + V2.FromAngle(game.LastShotAngle) * 1100f, 1.5f, new Rgba(1f, 0.85f, 0.2f, 0.75f));
                    Bullet shot;
                    if (game.Bullets.TryGetValue(game.LastShotId, out shot))
                        Arc(shot.Pos, shot.Radius + 5f, 1.5f, new Rgba(1f, 0.85f, 0.2f, 0.9f), 12);
                }
            }

            // Crosshair
            V2 m = MouseArena();
            var ch = new Rgba(1f, 1f, 1f, 0.7f);
            Arc(m, 8f, 1.5f, ch, 16);
            Line(m - new V2(13, 0), m - new V2(5, 0), 1.5f, ch);
            Line(m + new V2(5, 0), m + new V2(13, 0), 1.5f, ch);
            Line(m - new V2(0, 13), m - new V2(0, 5), 1.5f, ch);
            Line(m + new V2(0, 5), m + new V2(0, 13), 1.5f, ch);
        }

        bool AnyWells()
        {
            foreach (var o in game.Enemies.Values)
                if (o.Type == EnemyType.GravityWell)
                    return true;
            return false;
        }

        /// The beam between the Twin Reapers: crackling dusk-to-dawn colors. It hurts to touch.
        void DrawReaperTether(float t)
        {
            Enemy a = null, b = null;
            foreach (var e in game.Enemies.Values)
                if (Game.IsTwin(e.Type) && !game.IsSpawning(e))
                {
                    if (a == null) a = e; else b = e;
                }
            if (a == null || b == null)
                return;
            V2 from = a.Disp, to = b.Disp, n = (to - from).Normalized().Orthogonal();
            Line(from, to, Game.TetherWidth + 8f, new Rgba(0.75f, 0.4f, 1f, 0.18f));
            Line(from, to, Game.TetherWidth, new Rgba(0.85f, 0.5f, 1f, 0.85f));
            // A jagged core so it reads as dangerous.
            V2 prev = from;
            for (int k = 1; k <= 16; k++)
            {
                V2 pt = k == 16 ? to : V2.Lerp(from, to, k / 16f) + n * (3f * Mathf.Sin(t * 30f + k * 1.7f));
                Line(prev, pt, 2f, new Rgba(1f, 0.95f, 1f, 0.9f));
                prev = pt;
            }
        }

        void DrawZone(Zone z, float t)
        {
            var d = z.Def;
            var c = d.Color;
            float timeLeft = MathX.Clamp01(z.T / Mathf.Max(0.01f, z.Duration));
            switch (z.Kind)
            {
                case ZoneKind.Meteor:  // warning circle that fills in, then the meteor lands
                    Circle(z.Pos, z.Radius * (1f - timeLeft), c.WithAlpha(0.25f), 40);
                    Arc(z.Pos, z.Radius, 3f, c.WithAlpha(MathX.Fmod(t, 0.2f) < 0.1f ? 0.9f : 0.5f), 48);
                    return;
                case ZoneKind.FlameWall:  // a ring of flickering flames; lower once it's done blocking
                {
                    float wall = z.GuardFrac > 0f ? 1f : 0.45f;
                    Circle(z.Pos, z.Radius, c.WithAlpha(0.12f), 40);
                    for (int k = 0; k < 18; k++)
                        Circle(z.Pos + V2.FromAngle(MathX.Tau * k / 18f) * z.Radius, (7f + 3f * Mathf.Sin(t * 14f + k)) * wall, c.WithAlpha(0.8f * wall), 8);
                    break;
                }
                case ZoneKind.Blizzard:  // swirling snow
                    Circle(z.Pos, z.Radius, c.WithAlpha(0.14f), 40);
                    for (int k = 0; k < 14; k++)
                    {
                        float a = t * 1.5f + k * 2.4f, rr = z.Radius * MathX.Fmod(k * 0.37f + t * 0.2f, 1f);
                        Circle(z.Pos + V2.FromAngle(a) * rr, 2.5f, Rgba.White.WithAlpha(0.8f), 6);
                    }
                    break;
                case ZoneKind.Lantern:
                    Circle(z.Pos, z.Radius, c.WithAlpha(0.1f + 0.05f * Mathf.Sin(t * 5f)), 40);
                    Polygon(z.Pos, 9f, 4, MathX.Pi / 4f, c);
                    break;
                case ZoneKind.Totem:
                    Circle(z.Pos, z.Radius, c.WithAlpha(0.08f), 40);
                    Polygon(z.Pos, 8f, 6, t, c);
                    break;
                case ZoneKind.Sanctum:  // a shining dome
                    Circle(z.Pos, z.Radius, c.WithAlpha(0.12f + 0.04f * Mathf.Sin(t * 3f)), 56);
                    Arc(z.Pos, z.Radius - 6f, 2f, c.WithAlpha(0.5f), 56);
                    for (int k = 0; k < 8; k++)
                        Line(z.Pos, z.Pos + V2.FromAngle(MathX.Tau * k / 8f + t * 0.3f) * 26f, 3f, c.WithAlpha(0.8f));
                    break;
                case ZoneKind.Grove:  // a great tree with roots spreading out
                    Circle(z.Pos, z.Radius, c.WithAlpha(0.1f), 48);
                    for (int k = 0; k < 7; k++)
                    {
                        float a = MathX.Tau * k / 7f + 0.3f * Mathf.Sin(t + k);
                        Line(z.Pos, z.Pos + V2.FromAngle(a) * z.Radius * 0.85f, 3f, new Rgba(0.45f, 0.32f, 0.2f, 0.7f));
                    }
                    Circle(z.Pos, 34f, c.WithAlpha(0.85f), 20);
                    Circle(z.Pos, 12f, new Rgba(0.45f, 0.32f, 0.2f), 12);
                    break;
                default:  // Haven
                    Circle(z.Pos, z.Radius, c.WithAlpha(0.1f + 0.04f * Mathf.Sin(t * 4f)), 48);
                    break;
            }
            Arc(z.Pos, z.Radius, 2.5f, c.WithAlpha(0.7f), 48, -MathX.Pi / 2f, MathX.Tau * timeLeft);
        }

        void DrawMinion(Minion m, float t)
        {
            var d = m.Def;
            var c = d.Color;
            if (m.T >= 0f && m.T < 1.5f && MathX.Fmod(t, 0.2f) < 0.1f)
                c = c.WithAlpha(0.4f);  // about to vanish
            bool staggered = d.BlockRadius > 0f && m.GuardFrac <= 0f;
            if (staggered)  // took too much fire: dazed, not blocking for a moment
            {
                c = c.Darkened(0.4f);
                for (int k = 0; k < 3; k++)
                    Circle(m.Disp + V2.FromAngle(t * 5f + MathX.Tau * k / 3f) * (d.Radius + 6f), 2.5f, new Rgba(1f, 0.9f, 0.4f, 0.9f), 6);
            }
            if (d.BlockRadius > d.Radius)  // the guardian spirit's bullet-eating field, fading as it soaks
            {
                float g = MathX.Clamp01(m.GuardFrac);
                Circle(m.Disp, d.BlockRadius, c.WithAlpha(0.04f + 0.1f * g), 40);
                Arc(m.Disp, d.BlockRadius, 1f + 2f * g, c.WithAlpha(0.25f + 0.45f * g), 40);
            }
            float rot = d.Move == MinionMove.Static ? 0f : m.Aim;
            if (m.Kind == MinionKind.Wisp || m.Kind == MinionKind.Sprite)
                Circle(m.Disp, d.Radius * 2.2f, c.WithAlpha(0.2f), 12);
            if (m.Kind == MinionKind.Phoenix)
                for (int k = 0; k < 2; k++)  // flapping wings
                    Line(m.Disp, m.Disp + V2.FromAngle(m.Aim + MathX.Pi * (0.6f + k * 0.8f) + 0.4f * Mathf.Sin(t * 12f)) * 20f, 4f, c);
            Polygon(m.Disp, d.Radius, d.Sides, rot, c);
            if (d.Attack == MinionAttack.Shoot)
                Line(m.Disp, m.Disp + V2.FromAngle(m.Aim) * (d.Radius + 7f), 4f, c);
            if (m.Kind == MinionKind.Sprite)
                Line(m.Disp - new V2(3, 0), m.Disp + new V2(3, 0), 1.5f, Rgba.White);
            if (m.T >= 0f && m.Duration > 0f)
                Arc(m.Disp, d.Radius + 5f, 2f, c.WithAlpha(0.5f), 24, -MathX.Pi / 2f, MathX.Tau * MathX.Clamp01(m.T / m.Duration));
        }

        void DrawEnemy(Enemy e, float t)
        {
            Rgba col = e.Def.Color;
            V2 pos = e.Disp;
            float r = e.Radius;
            bool spawning = game.IsSpawning(e);
            if (spawning)
            {
                Arc(pos, r + 6f + 4f * Mathf.Sin(t * 12f), 2f, col.WithAlpha(0.6f), 32);
                col = col.WithAlpha(0.35f);
            }

            // Melee enemies are spiky, so you can tell rushers from shooters at a glance.
            if (e.Def.Melee && !spawning)
            {
                int spikes = e.Type == EnemyType.Swarmer ? 6 : e.Type == EnemyType.Brute ? 10 : 8;
                float spin = e.Type == EnemyType.Swarmer ? t * 4f : t * 0.6f;
                for (int k = 0; k < spikes; k++)
                {
                    float a = spin + MathX.Tau * k / spikes;
                    V2 d = V2.FromAngle(a), side = d.Orthogonal();
                    SetColor(col.Mix(new Rgba(1f, 0.95f, 0.85f, col.A), 0.35f));
                    Tri(pos + d * (r + 6f), pos + d * (r - 2f) + side * (r * 0.28f), pos + d * (r - 2f) - side * (r * 0.28f));
                }
            }

            // Effects behind the body.
            switch (e.Type)
            {
                case EnemyType.Spinner:
                    if (e.Phase == 1 && !spawning)  // bursting: a hot glow
                        Circle(pos, r + 8f + 3f * Mathf.Sin(t * 20f), new Rgba(1f, 0.6f, 0.2f, 0.25f), 24);
                    break;
                case EnemyType.Boss:
                    if (e.Phase == 3)  // the Warden's cross streams: faint guides along the four arms
                        for (int k = 0; k < 4; k++)
                        {
                            V2 d = V2.FromAngle(e.Facing + MathX.Pi / 2f * k);
                            Line(pos + d * r, pos + d * (r + 420f), 10f, new Rgba(1f, 0.35f, 0.45f, 0.07f));
                            Line(pos + d * r, pos + d * (r + 420f), 2f, new Rgba(1f, 0.45f, 0.55f, 0.18f));
                        }
                    break;
                case EnemyType.Lunger:
                    if (e.Phase == 1)  // winding up: shows where it's about to leap
                    {
                        V2 dir = V2.FromAngle(e.Facing);
                        float a = MathX.Fmod(t, 0.12f) < 0.06f ? 0.6f : 0.25f;
                        Line(pos + dir * r, pos + dir * 230f, 4f, new Rgba(1f, 0.35f, 0.65f, a));
                        Arc(pos + dir * 230f, 10f, 2f, new Rgba(1f, 0.35f, 0.65f, a), 12);
                    }
                    else if (e.Phase == 2)  // leaping: a smear behind it
                        for (int k = 1; k <= 3; k++)
                            Circle(pos - V2.FromAngle(e.Facing) * (r * 0.9f * k), r * (1f - 0.2f * k), col.WithAlpha(0.35f / k), 12);
                    break;
                case EnemyType.Charger:
                    if (e.Phase == 1)  // wind-up: show where it's about to dash
                    {
                        V2 dir = V2.FromAngle(e.Facing);
                        float a = MathX.Fmod(t, 0.16f) < 0.08f ? 0.55f : 0.25f;
                        Line(pos + dir * r, pos + dir * 260f, 3f, new Rgba(1f, 0.3f, 0.3f, a));
                    }
                    else if (e.Phase == 2)
                        Circle(pos - V2.FromAngle(e.Facing) * r, r * 0.9f, col.WithAlpha(0.3f), 16);
                    break;
                case EnemyType.HiveQueen:
                    if (e.Phase == 1)  // shielded by her swarm
                    {
                        Circle(pos, r + 16f, new Rgba(1f, 0.85f, 0.3f, 0.15f), 40);
                        Arc(pos, r + 16f, 3f, new Rgba(1f, 0.9f, 0.4f, 0.8f), 40);
                    }
                    break;
                case EnemyType.TwinA:
                case EnemyType.TwinB:
                    if (e.Phase == 1)  // enraged
                        Circle(pos, r + 10f + 4f * Mathf.Sin(t * 14f), new Rgba(1f, 0.2f, 0.2f, 0.25f), 32);
                    else if (e.Phase == 2)  // Soul Bond: immune, wrapped in a pale shell with orbiting runes
                    {
                        Circle(pos, r + 14f, new Rgba(0.9f, 0.9f, 1f, 0.18f), 40);
                        Arc(pos, r + 14f, 3f, new Rgba(0.95f, 0.95f, 1f, 0.85f), 40);
                        for (int k = 0; k < 5; k++)
                            Polygon(pos + V2.FromAngle(t * 1.5f + MathX.Tau * k / 5f) * (r + 14f), 4f, 4, t * 3f, new Rgba(1f, 1f, 1f, 0.9f));
                    }
                    break;
                case EnemyType.Singularity:
                    if (e.Phase == 1)  // gravity well: rings shrinking inward
                        for (int k = 0; k < 4; k++)
                        {
                            float f = 1f - MathX.Fmod(t * 0.8f + k * 0.25f, 1f);
                            Arc(pos, r + 40f + f * 360f, 2f, col.WithAlpha(0.35f * (1f - f)), 64);
                        }
                    Circle(pos, r * 1.6f, new Rgba(0.05f, 0f, 0.1f, 0.9f), 40);  // dark core halo
                    break;
                case EnemyType.Phantom:
                    col = col.WithAlpha(0.45f + 0.35f * Mathf.Sin(t * 9f + e.Id));  // flickers in and out
                    break;
                case EnemyType.Juggernaut:
                    Arc(pos, r + 4f, 5f, new Rgba(0.75f, 0.75f, 0.8f, 0.9f), 8);  // armor plating
                    break;
                case EnemyType.Nest:
                    Circle(pos, r + 6f + 3f * Mathf.Sin(t * 3f), col.WithAlpha(0.2f), 24);
                    break;
                case EnemyType.Lich:
                {
                    // A tether to its phylactery while it's protected.
                    if (e.Phase == 1)
                    {
                        foreach (var o in game.Enemies.Values)
                            if (o.Type == EnemyType.Phylactery)
                                Line(pos, o.Disp, 2f, new Rgba(0.4f, 1f, 0.6f, 0.5f + 0.3f * Mathf.Sin(t * 8f)));
                        Circle(pos, r + 14f, new Rgba(0.4f, 1f, 0.6f, 0.15f), 32);
                        Arc(pos, r + 14f, 3f, new Rgba(0.4f, 1f, 0.6f, 0.8f), 32);
                    }
                    break;
                }
                case EnemyType.Phylactery:
                    Circle(pos, r * 2.2f, col.WithAlpha(0.15f + 0.1f * Mathf.Sin(t * 6f)), 24);
                    break;
                case EnemyType.GravityWell:  // its pull range, with rings falling inward
                    if (spawning)
                        break;
                    Circle(pos, Game.WellPullRange, col.WithAlpha(0.05f), 40);
                    for (int k = 0; k < 3; k++)
                    {
                        float f = 1f - MathX.Fmod(t * 0.9f + k / 3f + e.Id * 0.13f, 1f);
                        Arc(pos, r + 6f + f * (Game.WellPullRange - r - 6f), 1.5f, col.WithAlpha(0.4f * (1f - f)), 40);
                    }
                    break;
                case EnemyType.StormHerald:
                    for (int k = 0; k < 3; k++)  // crackling sparks
                    {
                        V2 a = pos + V2.FromAngle(t * 4f + k * 2.1f) * (r + 6f);
                        Line(a, a + V2.FromAngle(t * 17f + k) * 10f, 2f, new Rgba(0.8f, 0.9f, 1f, 0.8f));
                    }
                    break;
                case EnemyType.Devourer:
                    if (e.Phase == 1)  // hungry: a pulsing warning glow
                        Circle(pos, r + 18f + 6f * Mathf.Sin(t * 10f), new Rgba(1f, 0.2f, 0.6f, 0.25f), 40);
                    break;
                case EnemyType.Eclipse:
                    // A dark disk with a corona that grows wilder each phase.
                    for (int k = 0; k < 12; k++)
                    {
                        float a = MathX.Tau * k / 12f + t * (0.3f + 0.3f * e.Phase);
                        Line(pos + V2.FromAngle(a) * (r + 2f), pos + V2.FromAngle(a) * (r + 14f + 6f * e.Phase + 4f * Mathf.Sin(t * 6f + k)), 3f, col.WithAlpha(0.8f));
                    }
                    break;
            }
            if (e.Elite)
                Arc(pos, r + 5f, 2.5f, new Rgba(1f, 0.85f, 0.3f, 0.9f), 32);
            // Status effects tint the body; icons above it say which ones (see DrawStatusIcons).
            if (e.Frozen)
                col = col.Mix(new Rgba(0.75f, 0.92f, 1f), 0.85f);  // frosted almost white-blue
            else if (e.Chilled)
                col = col.Mix(IceColor, 0.35f);
            if (e.Burning)
            {
                col = col.Mix(FireColor, 0.25f + 0.15f * Mathf.Sin(t * 18f + e.Id));
                // Flames licking up off the top of it.
                for (int k = 0; k < 4; k++)
                {
                    float f = MathX.Fmod(t * 1.6f + k * 0.25f + e.Id * 0.17f, 1f);
                    V2 at = pos + new V2((k - 1.5f) * r * 0.45f + 2f * Mathf.Sin(t * 9f + k), -r * 0.4f - f * r * 0.9f);
                    Circle(at, (1f - f) * (4f + r * 0.28f), FireColor.Mix(new Rgba(1f, 0.85f, 0.3f), f).WithAlpha(0.9f * (1f - f)), 8);
                }
            }
            if (e.Marked)  // Mark for Death: a rotating red reticle
            {
                var mc = new Rgba(1f, 0.2f, 0.2f, 0.9f);
                for (int k = 0; k < 4; k++)
                    Arc(pos, r + 12f, 2.5f, mc, 6, t * 2f + k * MathX.Pi / 2f, 0.9f);
            }

            if (e.Flash > 0f)
                col = Rgba.White;
            float rot;
            switch (e.Type)
            {
                case EnemyType.Charger:
                case EnemyType.Lunger:
                case EnemyType.Brute:
                case EnemyType.Shard:
                case EnemyType.Mortar:
                case EnemyType.Shielder:
                    rot = e.Facing;  // point where they're aiming
                    break;
                case EnemyType.Spinner:
                    rot = t * (e.Phase == 1 ? 7f : 1.5f) + e.Id;  // revs up while it's spraying
                    break;
                case EnemyType.Singularity:
                    rot = t * (e.Phase == 1 ? -3f : 1f);
                    break;
                case EnemyType.Medic:
                    rot = MathX.Pi / 4f;
                    break;
                default:
                    rot = t * 0.4f + e.Id;
                    break;
            }
            Polygon(pos, r, e.Def.Sides, rot, col);
            if (e.Frozen)  // encased in an angular block of ice with a glint
            {
                Arc(pos, r + 5f, 3f, IceColor.WithAlpha(0.95f), 6, MathX.Pi / 6f, MathX.Tau);
                Polygon(pos, r + 5f, 6, MathX.Pi / 6f, IceColor.WithAlpha(0.35f));
                float g = MathX.Fmod(t * 0.7f + e.Id * 0.3f, 1f);
                Line(pos + new V2(-r * 0.6f, -r * 0.2f + g * r * 0.5f), pos + new V2(-r * 0.1f, -r * 0.7f + g * r * 0.5f), 2f, Rgba.White.WithAlpha(0.8f));
            }

            // Details on top of the body.
            switch (e.Type)
            {
                case EnemyType.Brute:  // a heavy brow and fists toward its target
                {
                    V2 f = V2.FromAngle(e.Facing), side = f.Orthogonal();
                    Circle(pos + f * r * 0.95f + side * r * 0.6f, r * 0.34f, col.Mix(new Rgba(1f, 0.9f, 0.8f), 0.3f), 10);
                    Circle(pos + f * r * 0.95f - side * r * 0.6f, r * 0.34f, col.Mix(new Rgba(1f, 0.9f, 0.8f), 0.3f), 10);
                    Line(pos + f * r * 0.35f - side * r * 0.4f, pos + f * r * 0.35f + side * r * 0.4f, 3f, new Rgba(0.15f, 0.05f, 0.05f, 0.9f));
                    break;
                }
                case EnemyType.Shielder:
                    Arc(pos, r + 7f, 4f, new Rgba(0.7f, 0.85f, 1f, 0.9f), 20, e.Facing - 1.13f, 2.26f);
                    break;
                case EnemyType.Medic:
                    Line(pos - new V2(r * 0.5f, 0), pos + new V2(r * 0.5f, 0), 3f, Rgba.White);
                    Line(pos - new V2(0, r * 0.5f), pos + new V2(0, r * 0.5f), 3f, Rgba.White);
                    break;
                case EnemyType.Mortar:
                    Line(pos, pos + V2.FromAngle(e.Facing) * (r + 8f), 6f, col);
                    break;
                case EnemyType.Splitter:
                    Line(pos - new V2(r * 0.6f, 0), pos + new V2(r * 0.6f, 0), 2f, col.WithAlpha(0.6f));
                    Line(pos - new V2(0, r * 0.6f), pos + new V2(0, r * 0.6f), 2f, col.WithAlpha(0.6f));
                    break;
                case EnemyType.Singularity:
                    Circle(pos, r * 0.45f, new Rgba(0f, 0f, 0f, 1f), 24);
                    Arc(pos, r * 0.45f, 2f, Rgba.White.WithAlpha(0.6f), 24);
                    break;
                case EnemyType.Devourer:
                    Circle(pos, r * (e.Phase == 1 ? 0.7f : 0.4f), new Rgba(0.1f, 0f, 0.05f, 1f), 24);  // the mouth opens when hungry
                    break;
                case EnemyType.Eclipse:
                    Circle(pos, r * 0.8f, new Rgba(0.02f, 0.02f, 0.04f, 1f), 32);
                    break;
                case EnemyType.Phylactery:
                    Polygon(pos, r * 0.5f, 4, -t * 2f, Rgba.White.WithAlpha(0.8f));
                    break;
                case EnemyType.GravityWell:  // a spinning black hole
                    Circle(pos, r * 0.6f, new Rgba(0f, 0f, 0f, 1f), 16);
                    Arc(pos, r * 0.6f, 1.5f, Rgba.White.WithAlpha(0.7f), 16, t * 6f, MathX.Pi * 1.2f);
                    break;
            }
            // Bosses get a bar at the top of the screen instead.
            bool bar = e.Hp < e.MaxHp && !e.Def.IsBoss && !spawning;
            if (bar)
            {
                float w = Mathf.Max(30f, r * 2f);
                Bar(pos + new V2(-w / 2f, -r - 10f), w, 4f, e.Hp / e.MaxHp, new Rgba(1f, 0.3f, 0.3f));
            }
            DrawStatusIcons(e, pos + new V2(0f, -r - (bar ? 25f : 17f)), t);
        }

        static readonly Rgba IceColor = new Rgba(0.55f, 0.85f, 1f);
        static readonly Rgba FireColor = new Rgba(1f, 0.42f, 0.1f);
        static readonly Rgba SlowColor = new Rgba(0.45f, 0.7f, 1f);
        static readonly Rgba MarkColor = new Rgba(1f, 0.25f, 0.25f);

        /// A row of small icons over an enemy, one per status: snowflake = frozen, blue chevrons =
        /// slowed, flame = burning, red crosshair = marked (takes extra damage).
        void DrawStatusIcons(Enemy e, V2 center, float t)
        {
            int n = (e.Frozen ? 1 : 0) + (e.Chilled ? 1 : 0) + (e.Burning ? 1 : 0) + (e.Marked ? 1 : 0);
            if (n == 0)
                return;
            const float size = 8f, step = 21f;
            float x = center.X - (n - 1) * step / 2f;
            var bg = new Rgba(0f, 0f, 0f, 0.55f);
            if (e.Frozen)
            {
                V2 c = new V2(x, center.Y);
                Circle(c, size + 3f, bg, 12);
                for (int k = 0; k < 3; k++)  // snowflake: three crossing spokes with tips
                {
                    V2 d = V2.FromAngle(MathX.Pi / 2f + k * MathX.Pi / 3f) * size;
                    Line(c - d, c + d, 2f, IceColor);
                }
                Circle(c, 1.6f, Rgba.White, 6);
                x += step;
            }
            if (e.Chilled)
            {
                V2 c = new V2(x, center.Y);
                Circle(c, size + 3f, bg, 12);
                for (int k = 0; k < 2; k++)  // two downward chevrons: slowed
                {
                    float y = c.Y - 3f + k * 4.5f;
                    Line(new V2(c.X - size * 0.8f, y - 2f), new V2(c.X, y + 2f), 2f, SlowColor);
                    Line(new V2(c.X, y + 2f), new V2(c.X + size * 0.8f, y - 2f), 2f, SlowColor);
                }
                x += step;
            }
            if (e.Burning)
            {
                V2 c = new V2(x, center.Y);
                Circle(c, size + 3f, bg, 12);
                float flick = 0.8f * Mathf.Sin(t * 16f + e.Id);
                Circle(c + new V2(0f, 1.5f), size * 0.6f, FireColor, 10);       // flame: round base...
                Polygon(c + new V2(0f, -2f + flick * 0.3f), size * 0.62f, 3, -MathX.Pi / 2f, FireColor);  // ...pointed top
                Circle(c + new V2(0f, 2f), size * 0.3f, new Rgba(1f, 0.9f, 0.4f), 8);
                x += step;
            }
            if (e.Marked)
            {
                V2 c = new V2(x, center.Y);
                Circle(c, size + 3f, bg, 12);
                Arc(c, size * 0.75f, 1.6f, MarkColor, 12);
                Line(c - new V2(size, 0f), c + new V2(size, 0f), 1.5f, MarkColor);
                Line(c - new V2(0f, size), c + new V2(0f, size), 1.5f, MarkColor);
            }
        }

        void DrawPlayer(Player p, float t)
        {
            float r = p.Def.BodyR;
            Rgba col = p.Def.Color;
            if (p.Downed)
                col = new Rgba(0.45f, 0.45f, 0.5f);
            else if (p.InvulnT > 0f && MathX.Fmod(t, 0.12f) < 0.06f)
                col = col.WithAlpha(0.35f);
            if (p.Flash > 0f)
                col = Rgba.White;

            if (p.Id == game.MyId && game.DashT > 0f)
                Circle(p.Disp - game.DashDir * 18f, r * 0.8f, col.WithAlpha(0.25f), 20);
            Circle(p.Disp, r, col.Darkened(0.55f), 28);
            Arc(p.Disp, r, 3f, col, 28);
            V2 dir = V2.FromAngle(p.Aim);
            Line(p.Disp + dir * (r * 0.5f), p.Disp + dir * (r + 10f), 5f, col);
            if (p.Role == Role.Tank)
            {
                float h = r * 0.55f;
                var c = col.WithAlpha(0.5f);
                Line(p.Disp + new V2(-h, -h), p.Disp + new V2(h, -h), 2f, c);
                Line(p.Disp + new V2(h, -h), p.Disp + new V2(h, h), 2f, c);
                Line(p.Disp + new V2(h, h), p.Disp + new V2(-h, h), 2f, c);
                Line(p.Disp + new V2(-h, h), p.Disp + new V2(-h, -h), 2f, c);
            }
            else if (p.Role == Role.Healer)
            {
                Line(p.Disp - new V2(r * 0.6f, 0), p.Disp + new V2(r * 0.6f, 0), 3f, col.WithAlpha(0.6f));
                Line(p.Disp - new V2(0, r * 0.6f), p.Disp + new V2(0, r * 0.6f), 3f, col.WithAlpha(0.6f));
            }
            // Class marker: a mage's four-point star, a summoner's three orbiting motes.
            if (p.Kit == Kit.Mage && !p.Downed)
                for (int k = 0; k < 4; k++)
                    Line(p.Disp + V2.FromAngle(MathX.Pi / 4f + k * MathX.Pi / 2f) * (r * 0.35f),
                         p.Disp + V2.FromAngle(MathX.Pi / 4f + k * MathX.Pi / 2f) * (r + 6f), 2f, new Rgba(0.8f, 0.55f, 1f, 0.8f));
            else if (p.Kit == Kit.Summoner && !p.Downed)
                for (int k = 0; k < 3; k++)
                    Circle(p.Disp + V2.FromAngle(t * 2f + MathX.Tau * k / 3f) * (r + 7f), 2.5f, new Rgba(0.6f, 1f, 0.7f, 0.9f), 8);
            // The real hitbox, drawn as a bright core.
            Circle(p.Disp, p.Def.HitR, new Rgba(1f, 1f, 1f, 0.85f), 16);
            Circle(p.Disp, p.Def.HitR * 0.5f, col, 12);

            Bar(p.Disp + new V2(-20f, -r - 12f), 40f, 4f, p.Hp / p.MaxHp, new Rgba(0.3f, 1f, 0.4f));
            if (p.Downed)
                Arc(p.Disp, r + 8f, 4f, new Rgba(0.4f, 1f, 0.5f), 32, -MathX.Pi / 2f, MathX.Tau * Mathf.Max(p.Revive, 0.01f));
        }

        // --- GL primitives (arena coordinates; everything is triangles) -------

        Vector3 Px(V2 p) => new Vector3(offX + p.X * drawScale, UnityEngine.Screen.height - (offY + p.Y * drawScale), 0f);

        static void SetColor(Rgba c) => GL.Color(new Color(c.R, c.G, c.B, c.A));

        void Tri(V2 a, V2 b, V2 c)
        {
            GL.Vertex(Px(a));
            GL.Vertex(Px(b));
            GL.Vertex(Px(c));
        }

        void Quad(V2 a, V2 b, V2 c, V2 d)
        {
            Tri(a, b, c);
            Tri(a, c, d);
        }

        void FillRect(float x, float y, float w, float h, Rgba c)
        {
            SetColor(c);
            Quad(new V2(x, y), new V2(x + w, y), new V2(x + w, y + h), new V2(x, y + h));
        }

        void Line(V2 a, V2 b, float width, Rgba c)
        {
            V2 n = (b - a).Normalized().Orthogonal() * (width / 2f);
            SetColor(c);
            Quad(a + n, b + n, b - n, a - n);
        }

        /// Weapon mods show on the shots themselves: a trail for homing, embers for fire,
        /// frost rings, sparks for shock and a hot rim for explosive rounds.
        void DrawShotMods(Bullet b, float t)
        {
            Player p;
            if (b.Owner == 0 || b.Kind == Defs.GrenadeShot || b.Kind == Defs.SwarmShot || !game.Players.TryGetValue(b.Owner, out p))
                return;
            var s = p.Stats;
            V2 back = b.Vel.Normalized();
            if (b.Homing > 0f)
                Line(b.Pos - back * (b.Radius * 4f), b.Pos, b.Radius * 0.9f, Defs.BulletColor(b.Kind).WithAlpha(0.3f));
            if (s.BurnFrac > 0f)
                Circle(b.Pos - back * (b.Radius * 1.8f) + V2.FromAngle(t * 20f + b.Id) * 1.5f, b.Radius * 0.7f, new Rgba(1f, 0.45f, 0.1f, 0.7f), 8);
            if (s.CryoSlow > 0f)
                Arc(b.Pos, b.Radius + 3f, 1.5f, new Rgba(0.65f, 0.92f, 1f, 0.8f), 10);
            if (s.ShockChance > 0f && MathX.Fmod(t * 7f + b.Id * 0.37f, 1f) < 0.5f)
            {
                V2 side = back.Orthogonal();
                Line(b.Pos + side * (b.Radius + 2f), b.Pos + side * (b.Radius + 6f) - back * 3f, 1.5f, new Rgba(0.75f, 0.85f, 1f, 0.9f));
                Line(b.Pos - side * (b.Radius + 2f), b.Pos - side * (b.Radius + 6f) + back * 3f, 1.5f, new Rgba(0.75f, 0.85f, 1f, 0.9f));
            }
            if (s.ExplosiveRadius > 0f)
                Arc(b.Pos, b.Radius + 1.5f, 1.5f, new Rgba(1f, 0.3f, 0.15f, 0.9f), 10);
        }

        static readonly Rgba BossShotGlow = new Rgba(0.85f, 0.3f, 1f);

        void Circle(V2 center, float r, Rgba c, int segs)
        {
            SetColor(c);
            V2 prev = center + new V2(r, 0);
            for (int i = 1; i <= segs; i++)
            {
                V2 next = center + V2.FromAngle(MathX.Tau * i / segs) * r;
                Tri(center, prev, next);
                prev = next;
            }
        }

        void Arc(V2 center, float r, float width, Rgba c, int segs, float start = 0f, float sweep = MathX.Tau)
        {
            SetColor(c);
            float r0 = Mathf.Max(0f, r - width / 2f), r1 = r + width / 2f;
            for (int i = 0; i < segs; i++)
            {
                V2 d0 = V2.FromAngle(start + sweep * i / segs), d1 = V2.FromAngle(start + sweep * (i + 1) / segs);
                Quad(center + d0 * r0, center + d0 * r1, center + d1 * r1, center + d1 * r0);
            }
        }

        void Polygon(V2 center, float r, int sides, float rot, Rgba c)
        {
            var pts = new V2[sides];
            for (int i = 0; i < sides; i++)
                pts[i] = center + V2.FromAngle(rot + MathX.Tau * i / sides) * r;
            SetColor(c.Darkened(0.45f));
            for (int i = 0; i < sides; i++)
                Tri(center, pts[i], pts[(i + 1) % sides]);
            for (int i = 0; i < sides; i++)
                Line(pts[i], pts[(i + 1) % sides], 2.5f, c);
        }

        void Bar(V2 pos, float w, float h, float frac, Rgba c)
        {
            FillRect(pos.X, pos.Y, w, h, new Rgba(0f, 0f, 0f, 0.6f));
            FillRect(pos.X, pos.Y, w * MathX.Clamp01(frac), h, c);
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
                    if (pauseMenuOpen || game.Paused)
                        DrawPauseMenu();
                    break;
            }
        }

        void DrawMenu()
        {
            Ui.Title(new Rect(0, 70f, W, 70f), "TOP-DOWN ARENA", 60, Color.white);
            Ui.Label(new Rect(0, 140f, W, 24f), "CO-OP BULLET HELL  ·  SURVIVE 50 WAVES", 15, Ui.Gold, TextAnchor.MiddleCenter, true);

            var card = new Rect(W / 2f - 250f, 190f, 500f, 360f);
            Ui.Panel(card, true);
            float x = card.x + 32f, w = card.width - 64f, y = card.y + 26f;
            Ui.Heading(x, y, w, "Your name");
            nameText = Ui.Field(new Rect(x, y + 28f, w, 38f), nameText, 16);
            y += 82f;
            Ui.Heading(x, y, w - 130f, "Host address (to join)");
            Ui.Heading(x + w - 110f, y, 110f, "Port");
            addressText = Ui.Field(new Rect(x, y + 28f, w - 130f, 38f), addressText, 64);
            portText = Ui.Field(new Rect(x + w - 110f, y + 28f, 110f, 38f), portText, 5);
            y += 94f;
            if (session.Connecting)
            {
                Ui.Label(new Rect(x, y, w, 44f), session.Status, 15, Ui.Muted, TextAnchor.MiddleCenter);
                if (Ui.Button(new Rect(x + w / 2f - 90f, y + 52f, 180f, 44f), "Cancel", Ui.Danger))
                    session.Leave("");
            }
            else
            {
                if (Ui.Button(new Rect(x, y, w / 2f - 8f, 50f), "Host game", Ui.Primary))
                    HostGame();
                if (Ui.Button(new Rect(x + w / 2f + 8f, y, w / 2f - 8f, 50f), "Join game", Ui.Neutral))
                    session.Join(addressText, Port(), nameText, joinRole, weaponChoice, abilityChoice, kitChoice);
                if (session.Status.Length > 0)
                    Ui.Label(new Rect(x - 10f, y + 58f, w + 20f, 50f), session.Status, 13, Ui.Warn, TextAnchor.UpperCenter, false, true);
            }

            float by = card.yMax + 24f;
            if (Ui.Button(new Rect(W / 2f - 250f, by, 160f, 40f), "How to play"))
                howToOpen = true;
            if (Ui.Button(new Rect(W / 2f - 80f, by, 160f, 40f), "Settings"))
                settingsOpen = true;
            if (Ui.Button(new Rect(W / 2f + 90f, by, 160f, 40f), "Quit", Ui.Danger))
                Application.Quit();
            Ui.Label(new Rect(0, H - 30f, W, 20f), Session.VersionText + "  ·  you and your friend need the same version", 12, Ui.Muted, TextAnchor.MiddleCenter);
        }

        void DrawHowToPlay()
        {
            var panel = new Rect(W / 2f - 330f, 26f, 660f, 668f);
            Ui.Panel(panel, true);
            Ui.Title(new Rect(panel.x, panel.y + 18f, panel.width, 44f), "HOW TO PLAY", 32, Color.white);
            string[,] rows =
            {
                { "Move", "WASD / arrow keys" },
                { "Aim / shoot", "Mouse / hold left click" },
                { "Role ability", "Space" },
                { "Parry", "Hold right click: a shield toward your cursor (no shooting)" },
                { "Ultimate", "F (one per role and class)" },
                { "Bonus abilities", "Q, E and Shift (from very rare upgrades)" },
                { "Pick an upgrade", "Click a card (or 1-3), then Confirm (Enter)" },
                { "Reroll upgrades", "R" },
                { "Stats & upgrades", "Hold Tab (just for you, doesn't pause)" },
                { "Aim line on / off", "V" },
                { "Pause menu", "Esc" },
                { "Mute", "M" },
            };
            float x = panel.x + 40f, y = panel.y + 80f, w = panel.width - 80f;
            Ui.Heading(x, y, w, "Controls");
            y += 30f;
            for (int i = 0; i < rows.GetLength(0); i++)
            {
                Ui.Label(new Rect(x, y, 200f, 24f), rows[i, 0], 15, Ui.Muted);
                Ui.Label(new Rect(x + 200f, y, w - 200f, 24f), rows[i, 1], 15, Ui.Text, TextAnchor.MiddleLeft, true);
                y += 28f;
            }
            y += 10f;
            Ui.Heading(x, y, w, "Tips");
            Ui.Label(new Rect(x, y + 28f, w, 180f),
                "Your hitbox is the small white dot in the middle of your character.\n" +
                "Walk off an edge to come in on the other side (enemies and bullets can't follow).\n" +
                "Icons over enemies: snowflake = frozen, blue arrows = slowed, flame = burning, red crosshair = marked.\n" +
                "At 0 HP you're downed: stand next to your ally to revive them.\n" +
                "Levels give upgrade picks at the end of each wave; bosses give legendary ones.\n" +
                "Dark-cored shots are a boss's: violet ones wear blockers down, red ones can't be blocked.\n" +
                "1 to 3 players: one hosts, the others join (Tailscale makes this easy).", 14, Ui.Text, TextAnchor.UpperLeft, false, true);
            if (Ui.Button(new Rect(panel.x + panel.width / 2f - 100f, panel.yMax - 64f, 200f, 44f), "Back", Ui.Primary))
                howToOpen = false;
        }

        // --- Pause menu and audio settings --------------------------------------

        void OpenPauseMenu()
        {
            pauseMenuOpen = true;
            if (game.State != MatchState.GameOver)
                game.RequestPause(true);  // pauses the match for both players
        }

        void ResumeGame()
        {
            pauseMenuOpen = false;
            if (game.Paused)
                game.RequestPause(false);
        }

        void DrawPauseMenu()
        {
            Ui.Fill(new Rect(0, 0, W, H), new Color(0.01f, 0.01f, 0.03f, 0.7f));
            var panel = new Rect(W / 2f - 240f, 45f, 480f, 630f);
            Ui.Panel(panel, true);
            Ui.Title(new Rect(panel.x, panel.y + 18f, panel.width, 44f), "PAUSED", 36, Color.white);
            Player by;
            string sub = "";
            if (game.Paused && game.PausedBy != game.MyId && game.Players.TryGetValue(game.PausedBy, out by))
                sub = "Paused by " + by.Name;
            else if (!game.Paused && game.State != MatchState.GameOver && game.State != MatchState.Victory)
                sub = "Pausing…";
            Ui.Label(new Rect(panel.x, panel.y + 62f, panel.width, 20f), sub, 14, Ui.Muted, TextAnchor.MiddleCenter);

            float x = panel.x + 40f, w = panel.width - 80f;
            if (Ui.Button(new Rect(x, panel.y + 96f, w, 48f), "Resume   [Esc]", Ui.Primary))
                ResumeGame();
            DrawAudioOptions(x, panel.y + 168f, w);
            if (Ui.Button(new Rect(x, panel.yMax - 68f, w, 44f), "Leave game", Ui.Danger))
            {
                pauseMenuOpen = false;
                session.Leave("");
            }
        }

        void DrawSettings()
        {
            var panel = new Rect(W / 2f - 240f, 30f, 480f, 660f);
            Ui.Panel(panel, true);
            Ui.Title(new Rect(panel.x, panel.y + 18f, panel.width, 44f), "SETTINGS", 34, Color.white);
            float x = panel.x + 40f, w = panel.width - 80f;
            DrawAudioOptions(x, panel.y + 82f, w);

            // Developer: session recording for balancing.
            float y = panel.y + 476f;
            Ui.Heading(x, y, w, "Developer");
            bool rec = Ui.Toggle(new Rect(x, y + 30f, w, 26f), recordSessions, "Record sessions for balancing (when hosting)");
            if (rec != recordSessions)
            {
                recordSessions = rec;
                PlayerPrefs.SetInt("record_sessions", rec ? 1 : 0);
                PlayerPrefs.Save();
                ApplyRecording();
            }
            if (Ui.Button(new Rect(x, y + 64f, 190f, 30f), "Open sessions folder"))
            {
                System.IO.Directory.CreateDirectory(SessionsFolder);
                Application.OpenURL("file:///" + SessionsFolder.Replace('\\', '/'));
            }
            Ui.Label(new Rect(x + 200f, y + 64f, w - 200f, 30f), "Summarize with Tools/SessionReport", 11, Ui.Muted, TextAnchor.MiddleLeft);
            if (Ui.Button(new Rect(panel.x + panel.width / 2f - 100f, panel.yMax - 66f, 200f, 44f), "Back", Ui.Primary))
                settingsOpen = false;
        }

        /// Volume sliders and mute; changes are saved right away.
        void DrawAudioOptions(float x, float y, float w)
        {
            Ui.Heading(x, y, w, "Audio");
            y += 34f;
            bool changed = false;
            changed |= VolumeSlider(x, ref y, w, "Master", ref sounds.Master);
            changed |= VolumeSlider(x, ref y, w, "Music", ref sounds.Music);
            changed |= VolumeSlider(x, ref y, w, "Weapons  (your shots and spells)", ref sounds.Weapons);
            changed |= VolumeSlider(x, ref y, w, "Enemies", ref sounds.Enemies);
            changed |= VolumeSlider(x, ref y, w, "Effects  (explosions, abilities, UI)", ref sounds.Effects);
            bool muted = Ui.Toggle(new Rect(x, y + 2f, w, 26f), sounds.Muted, "Mute all sound   [M]");
            if (muted != sounds.Muted)
            {
                sounds.Muted = muted;
                changed = true;
            }
            if (changed)
                SaveAudioSettings();
            bool line = Ui.Toggle(new Rect(x, y + 34f, w, 26f), aimLine, "Aim line   [V]");
            if (line != aimLine)
                SetAimLine(line);
        }

        bool VolumeSlider(float x, ref float y, float w, string label, ref float value)
        {
            Ui.Label(new Rect(x, y, w - 60f, 20f), label, 14, Ui.Text);
            Ui.Label(new Rect(x + w - 60f, y, 60f, 20f), Mathf.RoundToInt(value * 100f) + "%", 14, Ui.Muted, TextAnchor.MiddleRight, true);
            float v = Ui.Slider(new Rect(x, y + 24f, w, 18f), value);
            y += 54f;
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

        static string KitBlurb(Kit k)
        {
            switch (k)
            {
                case Kit.Mage: return "Spells: fire, frost, lightning and arcane magic. 85% health.";
                case Kit.Summoner: return "Fights alongside minions that come with your weapon. 90% health.";
                default: return "Guns and gear. Full health.";
            }
        }

        void DrawLobby()
        {
            Ui.Title(new Rect(40f, 22f, 600f, 50f), "LOBBY", 40, Color.white, TextAnchor.MiddleLeft);
            Ui.Label(new Rect(W - 440f, 30f, 400f, 20f), Session.VersionText, 12, Ui.Muted, TextAnchor.MiddleRight);
            LobbyEntry me;
            session.Lobby.TryGetValue(session.MyId, out me);

            // Left column: who's here and how to connect.
            float lx = 40f, lw = 420f, y = 92f;
            Ui.Heading(lx, y, lw, "Players");
            y += 32f;
            foreach (var e in session.Lobby.Values)
            {
                DrawPlayerCard(new Rect(lx, y, lw, 88f), e);
                y += 96f;
            }
            if (session.Lobby.Count < Session.MaxPlayers)
            {
                var empty = new Rect(lx, y, lw, 56f);
                Ui.Fill(empty, new Color(1f, 1f, 1f, 0.03f));
                Ui.Label(empty, session.Lobby.Count == 1 ? "Waiting for friends to join (up to 2)…" : "Room for one more (optional)", 15, Ui.Muted, TextAnchor.MiddleCenter);
                y += 64f;
            }
            y += 8f;
            Ui.Heading(lx, y, lw, session.IsHost ? "Friends join with" : "Connection");
            y += 30f;
            if (session.IsHost)
            {
                Ui.Label(new Rect(lx, y, lw, 20f), "Port " + session.Port + " and one of these addresses:", 13, Ui.Muted);
                y += 24f;
                float bx = lx;
                foreach (var addr in (addressList ?? "").Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries))
                {
                    float bw = 14f + addr.Length * 8.5f;
                    if (bx + bw > lx + lw)
                    {
                        bx = lx;
                        y += 30f;
                    }
                    Ui.Badge(new Rect(bx, y, bw, 24f), addr, addr.StartsWith("100.") ? Ui.Accent : Ui.Neutral);
                    bx += bw + 8f;
                }
                y += 32f;
                Ui.Label(new Rect(lx, y, lw, 20f), "Blue = Tailscale address", 12, Ui.Muted);
            }
            else
                Ui.Label(new Rect(lx, y, lw, 20f), "Connected. Click Ready, then the host starts.", 14, Ui.Good);
            if (session.Status.Length > 0)
                Ui.Label(new Rect(lx, H - 150f, lw, 60f), session.Status, 13, Ui.Warn, TextAnchor.UpperLeft, false, true);

            // Right column: your loadout.
            if (me != null)
                DrawLoadout(me, new Rect(500f, 92f, W - 540f, 520f));

            // Bottom bar.
            if (Ui.Button(new Rect(40f, H - 76f, 180f, 48f), "Leave", Ui.Danger))
                session.Leave("");
            // Everyone readies up; then the host can start.
            bool ready = me != null && me.Ready;
            float readyX = session.IsHost ? W - 580f : W - 300f;
            if (Ui.Button(new Rect(readyX, H - 76f, 260f, 48f), ready ? "Ready!  (click to cancel)" : "Ready", ready ? Ui.GoldButton : Ui.Primary))
                session.RequestReady(!ready);
            int waiting = 0;
            foreach (var e in session.Lobby.Values)
                if (!e.Ready)
                    waiting++;
            string note = waiting == 0 ? (session.IsHost ? "Everyone's ready" : "Everyone's ready: waiting for the host to start")
                : waiting == 1 && !ready ? "Click Ready when your loadout is set" : "Waiting for " + waiting + " player" + (waiting == 1 ? "" : "s") + " to ready up";
            Ui.Label(new Rect(240f, H - 76f, readyX - 260f, 48f), note, 14, waiting == 0 ? Ui.Good : Ui.Muted, TextAnchor.MiddleRight);
            if (session.IsHost)
            {
                bool wasEnabled = GUI.enabled;
                GUI.enabled = session.AllReady;
                if (Ui.Button(new Rect(W - 300f, H - 76f, 260f, 48f), session.Lobby.Count == 1 ? "Start solo" : "Start game (" + session.Lobby.Count + " players)", Ui.Primary))
                    session.StartMatch();
                GUI.enabled = wasEnabled;
            }
        }

        void DrawPlayerCard(Rect r, LobbyEntry e)
        {
            var color = ToColor(Defs.Get(e.Role).Color);
            Ui.Panel(r, e.Id == session.MyId);
            Ui.Fill(new Rect(r.x + 3f, r.y + 10f, 4f, r.height - 20f), color);
            Ui.Label(new Rect(r.x + 20f, r.y + 10f, r.width - 140f, 28f), e.Name, 20, Color.white, TextAnchor.MiddleLeft, true);
            float bx = r.xMax - 16f;
            if (e.Id == session.MyId)
            {
                bx -= 48f;
                Ui.Badge(new Rect(bx, r.y + 12f, 48f, 22f), "YOU", Ui.Accent);
            }
            if (e.Id == 1)
            {
                bx -= 58f;
                Ui.Badge(new Rect(bx, r.y + 12f, 52f, 22f), "HOST", Ui.GoldButton);
            }
            // Ready state, bottom right of the card.
            Ui.Badge(new Rect(r.xMax - 104f, r.yMax - 32f, 88f, 22f), e.Ready ? "READY" : "NOT READY", e.Ready ? new Color(0.2f, 0.6f, 0.32f) : Ui.Neutral);
            Ui.Label(new Rect(r.x + 20f, r.y + 40f, r.width - 40f, 22f), Defs.KitName(e.Kit) + " " + Defs.Get(e.Role).Name, 15, color, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(r.x + 20f, r.y + 62f, r.width - 130f, 22f),
                Defs.Weapon(e.Role, e.Kit, e.Weapon).Name + "  ·  " + Upgrades.Ability(Upgrades.RoleAbility(e.Role, e.Kit, e.Ability)).Name, 13, Ui.Muted);
        }

        void DrawLoadout(LobbyEntry me, Rect r)
        {
            Ui.Panel(r);
            float x = r.x + 24f, w = r.width - 48f, y = r.y + 18f;
            const float gap = 10f;
            float third = (w - gap * 2f) / 3f;
            var roleColor = ToColor(Defs.Get(me.Role).Color);

            Ui.Heading(x, y, w, "Role");
            for (int i = 0; i < Defs.Roles.Length; i++)
                if (Ui.Choice(new Rect(x + i * (third + gap), y + 28f, third, 38f), Defs.Roles[i].Name, (int)me.Role == i, ToColor(Defs.Roles[i].Color)))
                {
                    session.RequestRole((Role)i);
                    hostRole = (Role)i;
                    joinRole = (Role)i;
                    weaponChoice = 0;
                    abilityChoice = 0;
                }
            Ui.Label(new Rect(x, y + 70f, w, 20f), Defs.Get(me.Role).Desc + "  " + (int)(Defs.Get(me.Role).Hp * Defs.KitHpMult(me.Kit)) + " HP.", 13, Ui.Muted);
            y += 100f;

            Ui.Heading(x, y, w, "Class");
            for (int i = 0; i <= (int)Kit.Summoner; i++)
                if (Ui.Choice(new Rect(x + i * (third + gap), y + 28f, third, 38f), Defs.KitName((Kit)i), me.Kit == (Kit)i, roleColor))
                {
                    session.RequestKit((Kit)i);
                    kitChoice = (Kit)i;
                    weaponChoice = 0;
                    abilityChoice = 0;
                }
            Ui.Label(new Rect(x, y + 70f, w, 20f), KitBlurb(me.Kit), 13, Ui.Muted);
            y += 100f;

            Ui.Heading(x, y, w, "Weapon");
            for (int i = 0; i < Defs.WeaponsPerRole; i++)
                if (Ui.Choice(new Rect(x + i * (third + gap), y + 28f, third, 38f), Defs.Weapon(me.Role, me.Kit, i).Name, me.Weapon == i, roleColor))
                {
                    session.RequestWeapon(i);
                    weaponChoice = i;
                }
            Ui.Label(new Rect(x, y + 70f, w, 36f), Defs.Weapon(me.Role, me.Kit, me.Weapon).Desc, 13, Ui.Muted, TextAnchor.UpperLeft, false, true);
            y += 110f;

            Ui.Heading(x, y, w, "Ability  (Space)");
            for (int i = 0; i < Upgrades.RoleAbilityChoices; i++)
                if (Ui.Choice(new Rect(x + i * (third + gap), y + 28f, third, 38f), Upgrades.Ability(Upgrades.RoleAbility(me.Role, me.Kit, i)).Name, me.Ability == i, roleColor))
                {
                    session.RequestAbility(i);
                    abilityChoice = i;
                }
            var ability = Upgrades.Ability(Upgrades.RoleAbility(me.Role, me.Kit, me.Ability));
            Ui.Label(new Rect(x, y + 70f, w, 40f), ability.Desc + "  (" + ability.Cooldown.ToString("0.#") + " s cooldown)", 13, Ui.Muted, TextAnchor.UpperLeft, false, true);
            y += 108f;

            // The ultimate comes with the role and class; it can't be picked.
            var ult = Upgrades.Ability(Upgrades.Ultimate(me.Role, me.Kit));
            Ui.Heading(x, y, w, "Ultimate  (F)");
            Ui.Label(new Rect(x, y + 26f, w, 20f), ult.Name, 15, Ui.Gold, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(x + 150f, y + 26f, w - 150f, 36f), ult.Desc + "  (" + ult.Cooldown.ToString("0") + " s)", 12, Ui.Muted, TextAnchor.UpperLeft, false, true);
        }

        static readonly string[] SlotKeys = { "SPACE", "Q", "E", "SHIFT", "F" };

        void DrawHud()
        {
            var me = game.Me;
            DrawWavePlate();
            if (aimDebug && me != null)
                DrawAimDebug(me);
            DrawBossBars();

            // Names, level-up popups and downed labels over players (hidden while they're under
            // the between-waves banner, so they don't show on top of its text).
            bool banner = game.State == MatchState.Intermission && (me == null || me.Offer == null);
            foreach (var p in game.Players.Values)
            {
                float r = p.Def.BodyR;
                V2 at = p.Disp * WorldToUi;  // arena -> HUD coordinates
                r *= WorldToUi;
                if (banner && at.X > BannerRect.x - 100f && at.X < BannerRect.xMax + 100f && at.Y > BannerRect.y - 20f && at.Y < BannerRect.yMax + r + 60f)
                    continue;
                Ui.Shadowed(new Rect(at.X - 100f, at.Y - r - 30f, 200f, 18f), p.Name + (p.Id == game.MyId ? " (you)" : ""), 12,
                    ToColor(p.Def.Color.WithAlpha(0.95f)));
                if (p.LevelFlash > 0f)
                {
                    float rise = (1.5f - p.LevelFlash) * 22f;
                    Ui.Shadowed(new Rect(at.X - 100f, at.Y - r - 54f - rise, 200f, 24f), "LEVEL UP!", 19,
                        new Color(1f, 0.88f, 0.35f, Mathf.Min(1f, p.LevelFlash)));
                }
                if (p.Downed)
                {
                    var tag = new Rect(at.X - 92f, at.Y + r + 10f, 184f, 34f);
                    Ui.HudPanel(tag, new Color(1f, 0.35f, 0.35f));
                    Ui.Label(new Rect(tag.x, tag.y + 3f, tag.width, 14f), "DOWNED — stand close to revive", 10, new Color(1f, 0.7f, 0.7f), TextAnchor.MiddleCenter, true);
                    Ui.Bar(new Rect(tag.x + 10f, tag.y + 21f, tag.width - 20f, 6f), p.Revive, Ui.Good);
                }
            }

            // Player cards in the top-right corner, yours first.
            var ids = new List<int>(game.Players.Keys);
            ids.Sort((a, b) => a == game.MyId ? -1 : b == game.MyId ? 1 : a.CompareTo(b));
            const float cardW = 232f, cardH = 66f, cardGap = 8f;
            float x = W - (cardW + cardGap) * ids.Count - 8f;
            foreach (int id in ids)
            {
                DrawPlayerHudCard(game.Players[id], new Rect(x, 12f, cardW, cardH), id == game.MyId);
                x += cardW + cardGap;
            }

            if (me != null)
                DrawAbilityBar(me);

            switch (game.State)
            {
                case MatchState.Intermission:
                    if (me != null && me.Offer != null)
                    {
                        DrawUpgradePicker(me);
                        break;
                    }
                    var choosing = new List<string>();
                    foreach (var p in game.Players.Values)
                        if (p.PendingPicks + p.PendingLegendary + p.PendingMods > 0 && p.Id != game.MyId)
                            choosing.Add(p.Name);
                    string sub = choosing.Count > 0 ? "Waiting for " + string.Join(" and ", choosing.ToArray()) + " to choose an upgrade…"
                        : !game.AnyonePicking() ? "Wave " + (game.Wave + 1) + " starts in " + Mathf.CeilToInt(game.StateTimer) : "";
                    DrawBanner(game.Wave == 0 ? "GET READY" : "WAVE " + game.Wave + " CLEARED", sub, game.Wave > 0 && game.Wave % 5 == 0 ? Ui.Gold : Ui.Accent);
                    break;
                case MatchState.Victory:
                case MatchState.GameOver:
                    if (!pauseMenuOpen && !tabHeld)
                        DrawRunSummary();
                    break;
            }
            if (tabHeld)
                DrawTabMenu(me);
            else if (me != null && me.Offer == null && game.State != MatchState.GameOver && game.State != MatchState.Victory)
            {
                var hint = new Rect(W - 196f, H - 34f, 180f, 22f);
                Ui.Pill(hint, new Color(0f, 0f, 0f, 0.35f));
                Ui.Label(hint, "Hold TAB  ·  stats & upgrades", 11, new Color(1f, 1f, 1f, 0.55f), TextAnchor.MiddleCenter, true);
            }
        }

        // --- Tab: your stats and loadout ---------------------------------------

        /// Held with Tab. Only drawn on this player's screen; nothing is sent to the
        /// other player and the game doesn't pause.
        void DrawTabMenu(Player me)
        {
            Ui.Fill(new Rect(0, 0, W, H), new Color(0.01f, 0.012f, 0.03f, 0.6f));
            var panel = new Rect(36f, 54f, W - 72f, 640f);
            Ui.Panel(panel, true);
            Ui.Title(new Rect(panel.x, panel.y + 12f, panel.width, 34f), "YOUR LOADOUT", 26, Color.white);
            Ui.Label(new Rect(panel.x, panel.y + 46f, panel.width, 18f),
                Defs.KitName(me.Kit) + " " + me.Def.Name + "   ·   Level " + me.Level + "   ·   " + me.Rerolls + " reroll" + (me.Rerolls == 1 ? "" : "s") + " left",
                13, ToColor(me.Def.Color), TextAnchor.MiddleCenter, true);
            float top = panel.y + 76f, bottom = panel.yMax - 16f;
            const float pad = 16f, gap = 14f, statsW = 270f, abilW = 330f;
            var statsBox = new Rect(panel.x + pad, top, statsW, bottom - top);
            var abilBox = new Rect(statsBox.xMax + gap, top, abilW, bottom - top);
            var upBox = new Rect(abilBox.xMax + gap, top, panel.xMax - pad - abilBox.xMax - gap, bottom - top);
            DrawTabStats(me, statsBox);
            DrawTabAbilities(me, abilBox);
            DrawTabUpgrades(me, upBox);
        }

        /// Stats, grouped so they're quick to scan.
        void DrawTabStats(Player me, Rect box)
        {
            Ui.HudPanel(box);
            var lines = new Dictionary<string, string>();
            foreach (var line in StatSheet.Describe(me, me.Taken))
                if (!line.Header && !lines.ContainsKey(line.Label))
                    lines[line.Label] = line.Value;
            Func<string, string> get = k => lines.ContainsKey(k) ? lines[k] : "";
            string charges = "";
            foreach (var kv in lines)
                if (kv.Key.EndsWith(" charges") && kv.Key != "Bonus charges")
                    charges = kv.Value;
            var groups = new[]
            {
                new KeyValuePair<string, string[,]>("OFFENSE", new string[,] { { "Damage", get("Damage") }, { "Fire rate", get("Fire rate") }, { "Extra projectiles", get("Extra projectiles") }, { "Pierce", get("Pierce") } }),
                new KeyValuePair<string, string[,]>("DEFENSE", new string[,] { { "Health", Mathf.CeilToInt(me.Hp) + " / " + Mathf.CeilToInt(me.MaxHp) }, { "Damage taken", get("Damage taken") }, { "Regeneration", get("Regeneration") }, { "Parry durability", get("Parry durability") } }),
                new KeyValuePair<string, string[,]>("UTILITY", new string[,] { { "Move speed", get("Move speed") }, { "Ability cooldowns", get("Ability cooldowns") }, { "Space charges", charges } }),
            };
            float y = box.y + 12f;
            foreach (var g in groups)
            {
                Ui.Label(new Rect(box.x + 16f, y, box.width - 32f, 16f), g.Key, 11, Ui.Gold, TextAnchor.MiddleLeft, true);
                y += 22f;
                for (int i = 0; i < g.Value.GetLength(0); i++)
                {
                    var row = new Rect(box.x + 10f, y, box.width - 20f, 28f);
                    if (i % 2 == 0)
                        Ui.Fill(row, new Color(1f, 1f, 1f, 0.035f));
                    Ui.Label(new Rect(row.x + 8f, row.y, row.width - 16f, row.height), g.Value[i, 0], 14, Ui.Muted);
                    Ui.Label(new Rect(row.x + 8f, row.y, row.width - 16f, row.height), g.Value[i, 1], 15, Color.white, TextAnchor.MiddleRight, true);
                    y += 28f;
                }
                y += 14f;
            }
        }

        /// The weapon and each ability on its own tile.
        void DrawTabAbilities(Player me, Rect box)
        {
            Ui.HudPanel(box);
            float x = box.x + 12f, w = box.width - 24f, y = box.y + 12f;
            Ui.Label(new Rect(x + 4f, y, w, 16f), "WEAPON", 11, Ui.Gold, TextAnchor.MiddleLeft, true);
            y += 20f;
            Ui.Label(new Rect(x + 4f, y, w, 22f), me.Weapon.Name, 17, Color.white, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(x + 4f, y + 22f, w - 8f, 18f), me.Weapon.Desc, 12, Ui.Muted, TextAnchor.UpperLeft, false, true);
            y += 54f;
            Ui.Label(new Rect(x + 4f, y, w, 16f), "ABILITIES", 11, Ui.Gold, TextAnchor.MiddleLeft, true);
            y += 22f;
            for (int i = 0; i < me.Slots.Length; i++)
            {
                var slot = me.Slots[i];
                if (slot.Id == AbilityId.None)
                    continue;
                var a = Upgrades.Ability(slot.Id);
                bool ult = i == Upgrades.UltSlot;
                var tile = new Rect(x, y, w, 80f);
                Ui.Fill(tile, new Color(1f, 1f, 1f, 0.04f));
                DrawKeyCap(new Rect(tile.x + 8f, tile.y + 8f, 0f, 18f), ult ? "ULT · F" : SlotKeys[i], ult ? Ui.Gold : ToColor(me.Def.Color));
                Ui.Label(new Rect(tile.x + 8f, tile.y + 28f, tile.width - 16f, 20f), a.Name, 15, Color.white, TextAnchor.MiddleLeft, true);
                string state = slot.Charges > 0 ? "Ready" : Mathf.CeilToInt(slot.Timer) + "s";
                if (slot.MaxCharges > 1)
                    state = slot.Charges + "/" + slot.MaxCharges + " charges" + (slot.Charges < slot.MaxCharges ? "  ·  " + Mathf.CeilToInt(slot.Timer) + "s" : "");
                Ui.Label(new Rect(tile.x + 8f, tile.y + 8f, tile.width - 16f, 18f), state, 12, slot.Charges > 0 ? Ui.Good : Ui.Muted, TextAnchor.MiddleRight, true);
                Ui.Label(new Rect(tile.x + 8f, tile.y + 47f, tile.width - 16f, 30f), a.Desc, 11, Ui.Muted, TextAnchor.UpperLeft, false, true);
                y += 84f;
            }
        }

        /// Every upgrade picked, grouped by rarity (rarest first) in two columns.
        void DrawTabUpgrades(Player me, Rect box)
        {
            Ui.HudPanel(box);
            var counts = new Dictionary<int, int>();
            foreach (int id in me.Taken)
                counts[id] = counts.ContainsKey(id) ? counts[id] + 1 : 1;
            // A flat list of headers (id < 0, rarity in -id - 1) and upgrades, rarest first.
            var items = new List<int>();
            for (int rr = (int)Rarity.Mod; rr >= 0; rr--)
            {
                var group = new List<int>();
                foreach (var id in counts.Keys)
                    if ((int)Upgrades.All[id].Rarity == rr)
                        group.Add(id);
                if (group.Count == 0)
                    continue;
                group.Sort((a, b) => string.CompareOrdinal(Upgrades.All[a].Name, Upgrades.All[b].Name));
                items.Add(-rr - 1);
                items.AddRange(group);
            }
            Ui.Label(new Rect(box.x + 16f, box.y + 12f, box.width - 32f, 16f), "UPGRADES", 11, Ui.Gold, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(box.x + 16f, box.y + 12f, box.width - 32f, 16f), me.Taken.Count + " picks", 11, Ui.Muted, TextAnchor.MiddleRight, true);
            if (items.Count == 0)
            {
                Ui.Label(new Rect(box.x + 16f, box.y + 40f, box.width - 32f, 22f), "None yet: level up to pick some.", 13, Ui.Muted);
                return;
            }
            const float rowH = 23f, colGap = 14f;
            float listTop = box.y + 36f, colW = (box.width - 24f - colGap) / 2f;
            int perCol = Mathf.FloorToInt((box.yMax - 10f - listTop) / rowH);
            int maxScroll = Math.Max(0, items.Count - perCol * 2);
            if (Event.current.type == EventType.ScrollWheel)
            {
                tabScroll += Event.current.delta.y > 0f ? 2 : -2;
                Event.current.Use();
            }
            tabScroll = Mathf.Clamp(tabScroll, 0, maxScroll);
            if (maxScroll > 0)
                Ui.Label(new Rect(box.x + 16f, box.yMax - 20f, box.width - 32f, 14f), "scroll for more", 10, Ui.Muted, TextAnchor.MiddleRight);
            for (int k = 0; k < perCol * 2 && tabScroll + k < items.Count; k++)
            {
                int item = items[tabScroll + k];
                float cx = box.x + 12f + (k / perCol) * (colW + colGap), cy = listTop + (k % perCol) * rowH;
                if (item < 0)
                {
                    var rarity = (Rarity)(-item - 1);
                    Color rc = ToColor(Upgrades.RarityColor(rarity));
                    Ui.Pill(new Rect(cx, cy + 9f, 3f, 8f), rc);
                    Ui.Label(new Rect(cx + 9f, cy + 3f, colW - 9f, 20f), Upgrades.RarityName(rarity), 11, rc, TextAnchor.MiddleLeft, true);
                    Ui.Fill(new Rect(cx, cy + rowH - 1f, colW, 1f), new Color(rc.r, rc.g, rc.b, 0.25f));
                    continue;
                }
                var u = Upgrades.All[item];
                Ui.Icon(new Rect(cx + 2f, cy + 3f, 18f, 18f), PixelArt.ForUpgrade(u.Name));
                Ui.Label(new Rect(cx + 26f, cy, colW - 60f, rowH), u.Name, 13, Ui.Text, TextAnchor.MiddleLeft);
                if (counts[item] > 1)
                {
                    var badge = new Rect(cx + colW - 30f, cy + 4f, 28f, 16f);
                    Ui.Pill(badge, new Color(1f, 1f, 1f, 0.12f));
                    Ui.Label(badge, "x" + counts[item], 10, Color.white, TextAnchor.MiddleCenter, true);
                }
            }
        }

        // --- End of run ---------------------------------------------------------

        struct SummaryRow
        {
            public string Label, Award;
            public Func<RunStats, float> Value;
            public Func<RunStats, string> Format;
        }

        static readonly SummaryRow[] SummaryRows =
        {
            new SummaryRow { Label = "Damage dealt", Award = "Most damage", Value = s => s.DamageDealt, Format = s => Big(s.DamageDealt) },
            new SummaryRow { Label = "Kills", Award = "Most kills", Value = s => s.Kills, Format = s => s.Kills + (s.EliteKills + s.BossKills > 0 ? "  (" + s.EliteKills + " elite, " + s.BossKills + " boss)" : "") },
            new SummaryRow { Label = "Biggest hit", Award = "Heavy hitter", Value = s => s.BiggestHit, Format = s => Big(s.BiggestHit) },
            new SummaryRow { Label = "Accuracy", Award = "Sharpshooter", Value = s => s.Accuracy, Format = s => Mathf.RoundToInt(s.Accuracy * 100f) + "%" },
            new SummaryRow { Label = "Healing done", Award = "Best healer", Value = s => s.HealingDone, Format = s => Big(s.HealingDone) },
            new SummaryRow { Label = "Allies revived", Award = "Lifesaver", Value = s => s.Revives, Format = s => s.Revives.ToString() },
            new SummaryRow { Label = "Damage taken", Award = "Damage sponge", Value = s => s.DamageTaken, Format = s => Big(s.DamageTaken) },
            new SummaryRow { Label = "Times downed", Award = null, Value = s => s.Downs, Format = s => s.Downs.ToString() },
            new SummaryRow { Label = "Abilities used", Award = null, Value = s => s.AbilitiesUsed, Format = s => s.AbilitiesUsed.ToString() },
        };

        static string Big(float v) => v >= 10000f ? (v / 1000f).ToString("0.#") + "k" : Mathf.RoundToInt(v).ToString();

        void DrawRunSummary()
        {
            bool won = game.State == MatchState.Victory;
            Ui.Fill(new Rect(0, 0, W, H), new Color(0.01f, 0.01f, 0.03f, 0.75f));
            var panel = new Rect(70f, 30f, W - 140f, H - 60f);
            Ui.Panel(panel, true);
            Ui.Title(new Rect(panel.x, panel.y + 14f, panel.width, 56f), won ? "VICTORY!" : "GAME OVER", 46, won ? Ui.Gold : new Color(1f, 0.45f, 0.45f));
            Ui.Label(new Rect(panel.x, panel.y + 66f, panel.width, 22f),
                won ? "You beat all " + Game.MaxWave + " waves and The Eclipse" : "You made it to wave " + game.Wave, 16, Ui.Muted, TextAnchor.MiddleCenter);
            if (game.Recorder != null)
                Ui.Label(new Rect(panel.x + 20f, panel.yMax - 22f, panel.width - 40f, 16f), "Session recorded: " + game.Recorder.Path, 10, Ui.Muted, TextAnchor.MiddleLeft);

            // Run totals.
            int kills = 0;
            foreach (var p in game.Players.Values)
                kills += p.Run.Kills;
            int secs = Mathf.RoundToInt(game.RunTime);
            string[,] chips = { { "WAVE", game.Wave.ToString() }, { "TIME", secs / 60 + ":" + (secs % 60).ToString("00") }, { "SCORE", game.Score.ToString() }, { "KILLS", kills.ToString() } };
            float cw = 170f, cx = panel.x + panel.width / 2f - (cw * 4f + 30f) / 2f;
            for (int i = 0; i < 4; i++)
            {
                var c = new Rect(cx + i * (cw + 10f), panel.y + 98f, cw, 58f);
                Ui.Fill(c, new Color(1f, 1f, 1f, 0.04f));
                Ui.Label(new Rect(c.x, c.y + 6f, c.width, 16f), chips[i, 0], 11, Ui.Muted, TextAnchor.MiddleCenter, true);
                Ui.Label(new Rect(c.x, c.y + 22f, c.width, 30f), chips[i, 1], 24, Color.white, TextAnchor.MiddleCenter, true);
            }

            if (!game.RunStatsReady)
                Ui.Label(new Rect(panel.x, panel.y + 300f, panel.width, 30f), "Collecting results…", 18, Ui.Muted, TextAnchor.MiddleCenter);
            else
            {
                var players = new List<Player>(game.Players.Values);
                float colW = (panel.width - 60f - 20f * (players.Count - 1)) / Mathf.Max(1, players.Count);
                for (int i = 0; i < players.Count; i++)
                    DrawPlayerSummary(players[i], players, new Rect(panel.x + 30f + i * (colW + 20f), panel.y + 168f, colW, 410f));
            }

            // Buttons.
            float by = panel.yMax - 66f;
            if (session.IsHost)
            {
                float bx = panel.x + 30f;
                if (Ui.Button(new Rect(bx, by, 200f, 46f), "Play again  [R]", Ui.Primary))
                    session.StartMatch();
                bx += 212f;
                if (Ui.Button(new Rect(bx, by, 200f, 46f), "Back to lobby  [L]"))
                    session.ReturnToLobby();
                bx += 212f;
                if (won && Ui.Button(new Rect(bx, by, 280f, 46f), "Keep going (endless)  [C]", Ui.GoldButton))
                    game.ContinueEndless();
            }
            else
                Ui.Label(new Rect(panel.x + 30f, by, 400f, 46f), "Waiting for the host…", 16, Ui.Muted);
            if (Ui.Button(new Rect(panel.xMax - 190f, by, 160f, 46f), "Leave", Ui.Danger))
                session.Leave("");
        }

        void DrawPlayerSummary(Player p, List<Player> all, Rect r)
        {
            var color = ToColor(p.Def.Color);
            Ui.Panel(r, p.Id == game.MyId);
            Ui.Fill(new Rect(r.x + 3f, r.y + 12f, 4f, 44f), color);
            Ui.Label(new Rect(r.x + 18f, r.y + 10f, r.width - 120f, 26f), p.Name, 20, Color.white, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(r.xMax - 110f, r.y + 10f, 94f, 26f), "LEVEL " + p.Level, 14, Ui.Gold, TextAnchor.MiddleRight, true);
            Ui.Label(new Rect(r.x + 18f, r.y + 36f, r.width - 36f, 20f), Defs.KitName(p.Kit) + " " + p.Def.Name + "  ·  " + p.Weapon.Name, 13, color);

            // Awards: rows where this player has the best number (and it isn't zero).
            float ax = r.x + 18f;
            if (all.Count > 1)
                foreach (var row in SummaryRows)
                {
                    if (row.Award == null || row.Value(p.Run) <= 0f)
                        continue;
                    bool best = true;
                    foreach (var o in all)
                        if (o != p && row.Value(o.Run) >= row.Value(p.Run))
                            best = false;
                    if (!best || ax > r.xMax - 120f)
                        continue;
                    float bw = 16f + row.Award.Length * 7.2f;
                    Ui.Badge(new Rect(ax, r.y + 62f, bw, 22f), row.Award.ToUpperInvariant(), Ui.GoldButton);
                    ax += bw + 6f;
                }

            float y = r.y + 96f;
            foreach (var row in SummaryRows)
            {
                float mine = row.Value(p.Run), top = 0f;
                foreach (var o in all)
                    top = Mathf.Max(top, row.Value(o.Run));
                bool leading = all.Count > 1 && row.Award != null && mine > 0f && mine >= top;
                Ui.Label(new Rect(r.x + 18f, y, 150f, 20f), row.Label, 14, Ui.Muted);
                Ui.Label(new Rect(r.x + 160f, y, r.width - 178f, 20f), row.Format(p.Run), 14, leading ? Ui.Gold : Color.white, TextAnchor.MiddleRight, true);
                if (top > 0f && row.Award != null)
                    Ui.Meter(new Rect(r.x + 18f, y + 21f, r.width - 36f, 2f), mine / top, leading ? Ui.Gold : color * 0.8f);
                y += 27f;
            }

            // Upgrades picked, by rarity.
            var counts = new int[Enum.GetValues(typeof(Rarity)).Length];
            var legendaries = new List<string>();
            foreach (int id in p.Taken)
            {
                counts[(int)Upgrades.All[id].Rarity]++;
                if (Upgrades.All[id].Rarity == Rarity.Legendary)
                    legendaries.Add(Upgrades.All[id].Name);
            }
            y += 6f;
            Ui.Label(new Rect(r.x + 18f, y, 120f, 20f), "Upgrades", 14, Ui.Muted);
            float px = r.x + 120f;
            for (int i = 0; i < counts.Length; i++)
            {
                if (counts[i] == 0)
                    continue;
                Ui.Badge(new Rect(px, y, 44f, 20f), counts[i].ToString(), ToColor(Upgrades.RarityColor((Rarity)i)));
                px += 50f;
            }
            if (legendaries.Count > 0)
                Ui.Label(new Rect(r.x + 18f, y + 24f, r.width - 36f, 20f), string.Join(", ", legendaries.ToArray()), 12, ToColor(Upgrades.RarityColor(Rarity.Legendary)));
        }

        /// Name and health bar for each boss on the field, plus warnings for their mechanics.
        void DrawBossBars()
        {
            float y = 86f;
            const float panelW = 560f;
            foreach (var e in game.Enemies.Values)
            {
                if (!e.Def.IsBoss)
                    continue;
                var col = ToColor(e.Def.Color);
                string note = null;
                Color noteColor = Ui.Warn;
                if (e.Type == EnemyType.HiveQueen && e.Phase == 1)
                    note = "SHIELDED — kill her swarm!";
                else if (Game.IsTwin(e.Type) && e.Phase == 1)
                {
                    note = "ENRAGED";
                    noteColor = new Color(1f, 0.4f, 0.4f);
                }
                else if (Game.SoulBound(e))
                {
                    note = "SOUL BOUND — hurt the other Reaper!";
                    noteColor = new Color(0.85f, 0.88f, 1f);
                }
                else if (e.Type == EnemyType.Singularity && AnyWells())
                    note = "Its gravity wells swallow your shots — kill them!";
                else if (e.Type == EnemyType.Lich && e.Phase == 1)
                    note = "IMMUNE — destroy the phylactery!";
                else if (Game.Hungry(e))
                {
                    note = "HUNGRY — stop shooting, your hits heal it!";
                    noteColor = new Color(1f, 0.45f, 0.7f);
                }
                else if (e.Type == EnemyType.Eclipse)
                    note = "Phase " + (e.Phase + 1) + " of 3";
                float h = note != null ? 62f : 46f;
                var panel = new Rect(W / 2f - panelW / 2f, y, panelW, h);
                Ui.HudPanel(panel, col);
                float frac = Mathf.Clamp01(e.Hp / e.MaxHp);
                // Name with small diamonds either side, and the health percentage.
                Ui.Pill(new Rect(panel.x + 14f, panel.y + 12f, 6f, 6f), col);
                Ui.Label(new Rect(panel.x + 26f, panel.y + 5f, panelW - 120f, 20f), e.Def.Name.ToUpperInvariant(), 14, Color.white, TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(panel.xMax - 90f, panel.y + 5f, 76f, 20f), Mathf.CeilToInt(frac * 100f) + "%", 13, Ui.Muted, TextAnchor.MiddleRight, true);
                var bar = new Rect(panel.x + 14f, panel.y + 27f, panelW - 28f, 12f);
                Ui.Bar(bar, frac, col);
                for (int k = 1; k < 10; k++)  // tick marks every 10%
                    Ui.Fill(new Rect(bar.x + bar.width * k / 10f, bar.y + 2f, 1f, bar.height - 4f), new Color(0f, 0f, 0f, 0.35f));
                if (note != null)
                    Ui.Label(new Rect(panel.x, panel.y + 41f, panelW, 18f), note, 12, noteColor, TextAnchor.MiddleCenter, true);
                y += h + 6f;
            }
            string warning = null;
            if (game.TwinReviveT > 0f)
                warning = "Kill the other Reaper in " + game.TwinReviveT.ToString("0.0") + "s or its twin revives!";
            else if (game.PullStrength > 0f)
                warning = "GRAVITY WELL — run!";
            if (warning != null)
            {
                var r = new Rect(W / 2f - 230f, y, 460f, 28f);
                float pulse = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 8f);
                Ui.Pill(r, new Color(0.45f, 0.06f, 0.1f, 0.75f));
                Ui.Label(r, warning, 14, new Color(1f, 0.75f, 0.75f, pulse), TextAnchor.MiddleCenter, true);
            }
        }

        // --- HUD pieces -----------------------------------------------------------

        int plateWave = -1, plateMax = 1;

        /// Top-left: the wave number, how many enemies are left (with a progress bar) and the score.
        void DrawWavePlate()
        {
            if (game.Wave != plateWave)
            {
                plateWave = game.Wave;
                plateMax = 1;
            }
            plateMax = Math.Max(plateMax, game.Remaining);
            var r = new Rect(14f, 12f, 270f, 76f);
            bool bossWave = game.Wave > 0 && game.Wave % 5 == 0;
            Ui.HudPanel(r, bossWave ? Ui.Gold : (Color?)null);
            Ui.Label(new Rect(r.x + 16f, r.y + 8f, 120f, 14f), bossWave ? "BOSS WAVE" : "WAVE", 10, bossWave ? Ui.Gold : Ui.Muted, TextAnchor.MiddleLeft, true);
            string num = game.Wave.ToString();
            Ui.Label(new Rect(r.x + 14f, r.y + 20f, 120f, 40f), num, 34, Color.white, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(r.x + 18f + num.Length * 20f, r.y + 31f, 100f, 24f), game.Endless ? "ENDLESS" : "/ " + Game.MaxWave, 13,
                game.Endless ? Ui.Gold : Ui.Muted, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(r.x + 130f, r.y + 8f, 124f, 14f), "ENEMIES LEFT", 10, Ui.Muted, TextAnchor.MiddleRight, true);
            Ui.Label(new Rect(r.x + 130f, r.y + 20f, 124f, 26f), game.Remaining.ToString(), 20, Color.white, TextAnchor.MiddleRight, true);
            Ui.Label(new Rect(r.x + 130f, r.y + 44f, 124f, 14f), "SCORE " + game.Score.ToString("N0"), 10, Ui.Gold, TextAnchor.MiddleRight, true);
            float progress = game.State == MatchState.Fighting ? 1f - (float)game.Remaining / Math.Max(1, plateMax) : 1f;
            Ui.Bar(new Rect(r.x + 14f, r.yMax - 12f, r.width - 28f, 5f), progress, bossWave ? Ui.Gold : Ui.Accent);
        }

        /// One player's card: role-colored accent, name and class, health (with shield) and XP.
        void DrawPlayerHudCard(Player p, Rect r, bool mine)
        {
            var role = ToColor(p.Def.Color);
            Ui.HudPanel(r, p.Downed ? new Color(1f, 0.3f, 0.3f) : mine ? role : (Color?)null);
            Ui.Pill(new Rect(r.x + 6f, r.y + 9f, 4f, r.height - 18f), p.Downed ? new Color(0.5f, 0.5f, 0.55f) : role);
            Ui.Label(new Rect(r.x + 18f, r.y + 6f, r.width - 70f, 18f), p.Name, 13, Color.white, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(r.x + 18f, r.y + 22f, r.width - 70f, 14f), Defs.KitName(p.Kit) + " " + p.Def.Name, 10, role, TextAnchor.MiddleLeft, true);
            var lv = new Rect(r.xMax - 52f, r.y + 8f, 42f, 18f);
            Ui.Pill(lv, new Color(1f, 0.8f, 0.35f, 0.22f));
            Ui.Label(lv, "LV " + p.Level, 10, Ui.Gold, TextAnchor.MiddleCenter, true);

            var hpBar = new Rect(r.x + 18f, r.y + 39f, r.width - 28f, 12f);
            float frac = Mathf.Clamp01(p.Hp / Mathf.Max(1f, p.MaxHp));
            // Green when healthy, through yellow, to red when low.
            Color hpCol = frac > 0.5f ? Color.Lerp(new Color(1f, 0.85f, 0.3f), new Color(0.35f, 0.95f, 0.45f), (frac - 0.5f) * 2f)
                : Color.Lerp(new Color(1f, 0.3f, 0.3f), new Color(1f, 0.85f, 0.3f), frac * 2f);
            if (p.Downed)
            {
                Ui.Bar(hpBar, p.Revive, new Color(0.6f, 0.6f, 0.65f));
                Ui.Label(hpBar, "DOWNED", 9, new Color(1f, 0.6f, 0.6f), TextAnchor.MiddleCenter, true);
            }
            else
            {
                Ui.Bar(hpBar, frac, hpCol);
                if (p.ShieldHp > 0f)  // Arcane Ward: a blue stripe along the bottom of the health bar
                    Ui.Bar(new Rect(hpBar.x + 2f, hpBar.yMax - 4f, (hpBar.width - 4f) * Mathf.Clamp01(p.ShieldHp / Mathf.Max(1f, p.MaxHp)), 3f), 1f, new Color(0.55f, 0.8f, 1f), new Color(0f, 0f, 0f, 0f));
                Ui.Shadowed(new Rect(hpBar.x, hpBar.y - 1f, hpBar.width, hpBar.height + 2f), Mathf.CeilToInt(p.Hp) + " / " + Mathf.CeilToInt(p.MaxHp), 10, Color.white);
            }
            Ui.Bar(new Rect(r.x + 18f, r.y + 55f, r.width - 28f, 4f), p.Xp / Defs.XpToNext(p.Level), new Color(1f, 0.82f, 0.3f), new Color(0f, 0f, 0f, 0.4f));
        }

        /// The aim line: a soft glow from your gun toward the crosshair in your role's color, fading
        /// with distance, with motes drifting outward the way your shots fly.
        void DrawAimLine(Player me, V2 cursor, float t)
        {
            V2 dir = V2.FromAngle(me.Aim);
            V2 from = me.Disp + dir * (me.Def.BodyR + 8f);
            float reach = Mathf.Min(Mathf.Max(cursor.DistanceTo(me.Disp) - me.Def.BodyR - 14f, 40f), 620f);  // stops just short of the crosshair
            var c = me.Def.Color.Mix(Rgba.White, 0.35f);
            const int Steps = 24;
            for (int i = 0; i < Steps; i++)
            {
                float a = (float)i / Steps, b = (float)(i + 1) / Steps;
                float fade = 1f - 0.8f * a;  // strongest at the gun, still visible at the crosshair
                V2 p0 = from + dir * (reach * a), p1 = from + dir * (reach * b);
                Line(p0, p1, 8f, c.WithAlpha(0.07f * fade));   // soft glow
                Line(p0, p1, 1.5f, c.WithAlpha(0.32f * fade));  // thin core
            }
            // Motes drifting outward.
            const float Spacing = 34f;
            float offset = MathX.Fmod(t * 140f, Spacing);
            for (float d = offset; d < reach; d += Spacing)
            {
                float f = 1f - 0.75f * d / reach;
                Circle(from + dir * d, 1.3f + 1.2f * f, c.WithAlpha(0.5f * f), 8);
            }
        }

        /// F3: the numbers behind the aim lines.
        void DrawAimDebug(Player me)
        {
            var r = new Rect(14f, H - 190f, 300f, 104f);
            Ui.HudPanel(r, new Color(0.3f, 0.9f, 1f));
            Func<float, string> deg = a => (MathX.Fmod(a * 180f / MathX.Pi + 360f, 360f)).ToString("0.00") + "°";
            float diff = MathX.Fmod(game.LastShotAngle - game.LastShotAim + MathX.Pi, MathX.Tau) - MathX.Pi;
            Ui.Label(new Rect(r.x + 12f, r.y + 6f, r.width - 24f, 16f), "AIM DEBUG  (F3)", 11, new Color(0.3f, 0.9f, 1f), TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(r.x + 12f, r.y + 24f, r.width - 24f, 18f), "Aim now", 12, Ui.Muted);
            Ui.Label(new Rect(r.x + 12f, r.y + 24f, r.width - 24f, 18f), deg(me.Aim), 12, Color.white, TextAnchor.MiddleRight, true);
            Ui.Label(new Rect(r.x + 12f, r.y + 42f, r.width - 24f, 18f), "Last shot", 12, Ui.Muted);
            Ui.Label(new Rect(r.x + 12f, r.y + 42f, r.width - 24f, 18f), game.LastShotId != 0 ? deg(game.LastShotAngle) : "-", 12, new Color(1f, 0.85f, 0.2f), TextAnchor.MiddleRight, true);
            Ui.Label(new Rect(r.x + 12f, r.y + 60f, r.width - 24f, 18f), "Shot vs aim when it appeared", 12, Ui.Muted);
            Ui.Label(new Rect(r.x + 12f, r.y + 60f, r.width - 24f, 18f), game.LastShotId != 0 ? (diff * 180f / MathX.Pi).ToString("+0.00;-0.00;0.00") + "°" : "-", 12, Color.white, TextAnchor.MiddleRight, true);
            Ui.Label(new Rect(r.x + 12f, r.y + 78f, r.width - 24f, 18f), "Ping to host", 12, Ui.Muted);
            Ui.Label(new Rect(r.x + 12f, r.y + 78f, r.width - 24f, 18f), session.IsHost ? "0 ms (you're the host)" : Mathf.RoundToInt(game.PingMs) + " ms", 12,
                session.IsHost || game.PingMs < 60f ? Ui.Good : game.PingMs < 120f ? Ui.Warn : new Color(1f, 0.45f, 0.45f), TextAnchor.MiddleRight, true);
        }

        /// A centered announcement between waves.
        static readonly Rect BannerRect = new Rect(W / 2f - 280f, H / 2f - 92f, 560f, 96f);

        void DrawBanner(string title, string sub, Color accent)
        {
            var r = new Rect(BannerRect.x, BannerRect.y, BannerRect.width, sub.Length > 0 ? 96f : 70f);
            Ui.Card(r, accent, 0.8f);  // solid, so nothing in the arena shows through
            Ui.Fill(new Rect(r.x + 40f, r.y + 14f, r.width - 80f, 1f), new Color(accent.r, accent.g, accent.b, 0.35f));
            Ui.Title(new Rect(r.x, r.y + 16f, r.width, 40f), title, 30, Color.white);
            if (sub.Length > 0)
                Ui.Label(new Rect(r.x, r.y + 60f, r.width, 22f), sub, 15, Ui.Muted, TextAnchor.MiddleCenter, true);
        }

        /// XP bar plus the ability tiles (Space / Q / E / Shift / ultimate) along the bottom.
        void DrawAbilityBar(Player me)
        {
            const float tileW = 150f, tileH = 58f, gap = 8f;
            float total = tileW * Upgrades.SlotCount + gap * (Upgrades.SlotCount - 1);
            float x0 = (W - total) / 2f, y = H - tileH - 12f;

            // XP strip with the level badge.
            var lv = new Rect(x0, y - 22f, 52f, 16f);
            Ui.Pill(lv, new Color(1f, 0.8f, 0.35f, 0.25f));
            Ui.Label(lv, "LV " + me.Level, 10, Ui.Gold, TextAnchor.MiddleCenter, true);
            Ui.Bar(new Rect(x0 + 58f, y - 17f, total - 58f, 6f), me.Xp / Defs.XpToNext(me.Level), new Color(1f, 0.82f, 0.3f), new Color(0f, 0f, 0f, 0.5f));

            // Parry meter, just left of the abilities.
            {
                var r = new Rect(x0 - 78f, y, 70f, tileH);
                bool broken = me.ParryBroken > 0f;
                float hp = Mathf.Clamp01(me.ParryHp / Game.ParryMax(me));
                var sc = new Color(0.55f, 0.9f, 1f);
                Ui.HudPanel(r, me.ParryActive ? sc : broken ? new Color(1f, 0.4f, 0.4f) : (Color?)null);
                DrawKeyCap(new Rect(r.x + 8f, r.y + 7f, 0f, 16f), "RMB", me.ParryActive ? sc : new Color(1f, 1f, 1f, 0.6f));
                Ui.Label(new Rect(r.x + 8f, r.y + 26f, r.width - 16f, 16f), broken ? "BROKEN" : "PARRY", 11,
                    broken ? new Color(1f, 0.55f, 0.55f) : Color.white, TextAnchor.MiddleLeft, true);
                Ui.Bar(new Rect(r.x + 8f, r.yMax - 12f, r.width - 16f, 5f), hp, broken ? new Color(1f, 0.45f, 0.45f) : sc, new Color(0f, 0f, 0f, 0.5f));
            }

            for (int i = 0; i < Upgrades.SlotCount; i++)
            {
                var slot = me.Slots[i];
                var r = new Rect(x0 + i * (tileW + gap), y, tileW, tileH);
                bool ultimate = i == Upgrades.UltSlot;
                if (slot.Id == AbilityId.None)
                {
                    Ui.HudPanel(r);
                    DrawKeyCap(new Rect(r.x + 8f, r.y + 7f, 0f, 16f), SlotKeys[i], new Color(1f, 1f, 1f, 0.45f));
                    Ui.Label(new Rect(r.x + 8f, r.y + 26f, r.width - 16f, 18f), "Empty", 12, new Color(1f, 1f, 1f, 0.45f), TextAnchor.MiddleLeft, true);
                    Ui.Label(new Rect(r.x + 8f, r.y + 40f, r.width - 16f, 14f), "from a very rare upgrade", 9, new Color(1f, 1f, 1f, 0.35f), TextAnchor.MiddleLeft);
                    continue;
                }
                bool ready = slot.Charges > 0;
                float cd = game.Cooldown(me, i);
                float fill = slot.Charges >= slot.MaxCharges ? 1f : 1f - slot.Timer / Mathf.Max(0.01f, cd);
                Color accent = ultimate ? Ui.Gold : ToColor(me.Def.Color);
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
                Ui.HudPanel(r, ready ? new Color(accent.r, accent.g, accent.b, ultimate ? 0.6f + 0.4f * pulse : 1f) : (Color?)null);

                // Cooldown: a dark shade over the tile that drains downward as it recharges.
                if (!ready)
                {
                    GUI.BeginGroup(new Rect(r.x, r.y, r.width, r.height * (1f - fill)));
                    Ui.Pill(new Rect(0f, 0f, r.width, r.height), new Color(0f, 0f, 0f, 0.35f));
                    GUI.EndGroup();
                }
                DrawKeyCap(new Rect(r.x + 8f, r.y + 7f, 0f, 16f), ultimate ? "ULT · F" : SlotKeys[i], ready ? accent : new Color(1f, 1f, 1f, 0.6f));
                Ui.Label(new Rect(r.x + 8f, r.y + 26f, r.width - 16f, 18f), Upgrades.Ability(slot.Id).Name, 13, ready ? Color.white : new Color(1f, 1f, 1f, 0.78f), TextAnchor.MiddleLeft, true);
                if (ready)
                    Ui.Label(new Rect(r.x + 8f, r.y + 7f, r.width - 16f, 16f), ultimate ? "READY!" : "READY", 10, ultimate ? Ui.Gold : Ui.Good, TextAnchor.MiddleRight, true);
                else
                    Ui.Shadowed(new Rect(r.x + 8f, r.y + 4f, r.width - 16f, 20f), ultimate ? Mathf.FloorToInt(fill * 100f) + "%" : slot.Timer.ToString(slot.Timer < 10f ? "0.0" : "0") + "s",
                        14, Color.white, TextAnchor.MiddleRight);
                // Recharge line along the bottom, and charge pips.
                Ui.Bar(new Rect(r.x + 8f, r.yMax - 9f, r.width - 16f, 3f), fill, ready ? accent : new Color(0.5f, 0.6f, 0.8f), new Color(0f, 0f, 0f, 0.4f));
                if (slot.MaxCharges > 1)
                    for (int k = 0; k < slot.MaxCharges; k++)
                        Ui.Pill(new Rect(r.xMax - 12f - k * 9f, r.y + 30f, 6f, 6f), k < slot.Charges ? accent : new Color(1f, 1f, 1f, 0.15f));
            }
        }

        /// A small key label like [SPACE]; r.width is ignored (it's sized to the text).
        void DrawKeyCap(Rect r, string key, Color color)
        {
            float w = 12f + key.Length * 6.5f;
            var cap = new Rect(r.x, r.y, w, r.height);
            Ui.Pill(cap, new Color(color.r, color.g, color.b, 0.2f * color.a + 0.05f));
            Ui.Label(cap, key, 9, new Color(1f, 1f, 1f, 0.5f + 0.5f * color.a), TextAnchor.MiddleCenter, true);
        }

        void DrawUpgradePicker(Player me)
        {
            SyncSelection(me);
            float now = Time.unscaledTime;
            Ui.Fill(new Rect(0, 0, W, H), new Color(0.01f, 0.012f, 0.03f, 0.8f));
            // Stats on the left; the cards and buttons fill the rest.
            const float statsX = 16f, statsW = 250f;
            const float cardW = 290f, cardH = 300f, gap = 22f;
            float x0 = statsX + statsW + 22f;
            float areaW = cardW * 3 + gap * 2, mid = x0 + areaW / 2f;
            DrawStatPanel(me, new Rect(statsX, 110f, statsW, 560f));

            // Heading: a small tag, a big title, and how many picks are left.
            Color theme = me.OfferLegendary ? ToColor(Upgrades.RarityColor(Rarity.Legendary))
                : me.OfferMod ? ToColor(Upgrades.RarityColor(Rarity.Mod)) : Ui.Gold;
            string tag = me.OfferLegendary ? "BOSS REWARD" : me.OfferMod ? "BOSS REWARD · WEAPON MOD" : "LEVEL " + me.Level;
            string title = me.OfferLegendary ? "Choose a legendary upgrade" : me.OfferMod ? "Choose how your shots behave" : "Choose an upgrade";
            float tagW = 30f + tag.Length * 8f;
            var tagRect = new Rect(mid - tagW / 2f, 104f, tagW, 24f);
            Ui.Pill(tagRect, new Color(theme.r, theme.g, theme.b, 0.25f));
            Ui.Label(tagRect, tag, 12, theme, TextAnchor.MiddleCenter, true);
            Ui.Title(new Rect(x0, 128f, areaW, 40f), title.ToUpperInvariant(), 30, Color.white);
            int more = me.PendingPicks + me.PendingLegendary + me.PendingMods - 1;
            if (more > 0)
                Ui.Label(new Rect(x0, 164f, areaW, 18f), more + " more to choose after this", 13, Ui.Muted, TextAnchor.MiddleCenter, true);

            // Weapon mods show the whole list in a grid of smaller cards.
            bool grid = me.Offer.Length > 3;
            const int perRow = 4;
            float cw = grid ? (areaW - gap * (perRow - 1)) / perRow : cardW, ch = grid ? 150f : cardH;
            int rows = grid ? (me.Offer.Length + perRow - 1) / perRow : 1;
            float top = grid ? 196f : 196f;
            for (int i = 0; i < me.Offer.Length; i++)
            {
                var u = Upgrades.All[me.Offer[i]];
                int row = grid ? i / perRow : 0, col = grid ? i % perRow : i;
                int inRow = grid ? Math.Min(perRow, me.Offer.Length - row * perRow) : me.Offer.Length;
                float rowX = mid - (inRow * cw + (inRow - 1) * gap) / 2f;  // center short rows
                var slot = new Rect(rowX + col * (cw + gap), top + row * (ch + gap), cw, ch);
                bool hover = slot.Contains(Event.current.mousePosition);
                bool selected = selectedCard == i;
                // The card lifts a little when hovered and more when selected.
                var r = new Rect(slot.x, slot.y - (selected ? 10f : hover ? 4f : 0f), slot.width, slot.height);
                DrawUpgradeCard(me, i, u, r, grid, selected, hover, now);
                if (GUI.Button(slot, GUIContent.none, GUIStyle.none))
                    selectedCard = i;
            }

            // Confirm and reroll buttons under the cards.
            const float btnW = 280f;
            float by = top + rows * ch + (rows - 1) * gap + 26f;
            bool wasEnabled = GUI.enabled;
            GUI.enabled = selectedCard >= 0 && !game.RerollPending;
            string confirmText = selectedCard >= 0 ? "Confirm  [Enter]" : "Pick a card first";  // the selected card shows its name
            if (Ui.Button(new Rect(mid - btnW - 10f, by, btnW, 46f), confirmText, selectedCard >= 0 ? Ui.Primary : Ui.Neutral))
                ConfirmPick();
            GUI.enabled = me.Rerolls > 0 && !game.RerollPending && !me.OfferLegendary && !me.OfferMod;
            if (Ui.Button(new Rect(mid + 10f, by, btnW, 46f), "Reroll (" + me.Rerolls + " left)  [R]"))
                game.RerollOffer();
            GUI.enabled = wasEnabled;
            string hint = me.OfferLegendary || me.OfferMod ? "Boss rewards can't be rerolled" : "Rerolls: start with " + Game.StartingRerolls + ", +2 for every boss you kill";
            if (me.OfferMod)
                hint = "Every weapon mod you can still take is shown. Each stacks up to 3 times.";
            Ui.Label(new Rect(x0, by + 50f, areaW, 18), hint, 12, new Color(1, 1, 1, 0.45f), TextAnchor.MiddleCenter);
        }

        /// One upgrade card: rarity ribbon, pixel-art icon in a glowing frame, name, description and a note.
        void DrawUpgradeCard(Player me, int index, UpgradeDef u, Rect r, bool compact, bool selected, bool hover, float now)
        {
            Color rc = ToColor(Upgrades.RarityColor(u.Rarity));
            if (selected)  // a soft glow under the selected card
                Ui.Pill(new Rect(r.x - 6f, r.y - 6f, r.width + 12f, r.height + 12f), new Color(rc.r, rc.g, rc.b, 0.18f + 0.06f * Mathf.Sin(now * 4f)));
            Ui.Card(r, rc, selected ? 1f : hover ? 0.75f : 0.45f);
            // A wash of the rarity color across the top of the card.
            Ui.Pill(new Rect(r.x + 3f, r.y + 3f, r.width - 6f, compact ? 30f : 120f), new Color(rc.r, rc.g, rc.b, 0.08f));
            if (u.Rarity == Rarity.Legendary)
            {
                // A shimmer that sweeps across legendary cards.
                float sweep = MathX.Fmod(now * 0.6f + index * 0.3f, 1.6f) - 0.3f;
                GUI.BeginGroup(r);
                Ui.Fill(new Rect(sweep * r.width, 0f, 18f, r.height), new Color(1f, 0.9f, 0.6f, 0.07f));
                Ui.Fill(new Rect(sweep * r.width + 22f, 0f, 6f, r.height), new Color(1f, 0.9f, 0.6f, 0.05f));
                GUI.EndGroup();
            }
            var icon = PixelArt.ForUpgrade(u.Name);
            float bob = selected ? 3f * Mathf.Sin(now * 3f) : 0f;
            string note = UpgradeNote(me, index, u);
            if (compact)
            {
                // Weapon mod grid: icon on the left, name and stacks on the right, description under.
                var frame = new Rect(r.x + 12f, r.y + 14f, 52f, 52f);
                Ui.Pill(frame, new Color(rc.r * 0.3f, rc.g * 0.3f, rc.b * 0.3f, 0.9f));
                Ui.Icon(new Rect(frame.x + 2f, frame.y + 2f + bob * 0.5f, 48f, 48f), icon);
                Ui.Label(new Rect(r.x + 74f, r.y + 14f, r.width - 82f, 16f), note, 10, rc, TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(r.x + 74f, r.y + 30f, r.width - 82f, 36f), u.Name, 15, Color.white, TextAnchor.UpperLeft, true, true);
                Ui.Label(new Rect(r.x + 12f, r.y + 74f, r.width - 24f, 52f), u.Desc, 11, Ui.Text, TextAnchor.UpperLeft, false, true);
                Ui.Label(new Rect(r.x, r.yMax - 22f, r.width - 12f, 16f), selected ? "SELECTED" : "[" + (index + 1) + "]", 11,
                    selected ? rc : new Color(1, 1, 1, 0.45f), TextAnchor.MiddleRight, true);
                return;
            }
            // Rarity ribbon.
            string rarity = Upgrades.RarityName(u.Rarity);
            float rw = 24f + rarity.Length * 7.5f;
            var ribbon = new Rect(r.x + r.width / 2f - rw / 2f, r.y + 12f, rw, 20f);
            Ui.Pill(ribbon, new Color(rc.r, rc.g, rc.b, 0.85f));
            Ui.Label(ribbon, rarity, 10, new Color(0.06f, 0.06f, 0.1f), TextAnchor.MiddleCenter, true);
            // Icon in a framed, glowing tile.
            var tile = new Rect(r.x + r.width / 2f - 46f, r.y + 42f, 92f, 92f);
            Ui.Pill(new Rect(tile.x - 4f, tile.y - 4f, tile.width + 8f, tile.height + 8f), new Color(rc.r, rc.g, rc.b, selected ? 0.35f : 0.18f));
            Ui.Pill(tile, new Color(rc.r * 0.22f, rc.g * 0.22f, rc.b * 0.22f, 1f));
            Ui.Icon(new Rect(tile.x + 10f, tile.y + 10f + bob, 72f, 72f), icon);
            Ui.Label(new Rect(r.x + 10f, r.y + 144f, r.width - 20f, 28f), u.Name, 21, Color.white, TextAnchor.MiddleCenter, true);
            Ui.Fill(new Rect(r.x + 40f, r.y + 176f, r.width - 80f, 1f), new Color(rc.r, rc.g, rc.b, 0.35f));
            Ui.Label(new Rect(r.x + 18f, r.y + 184f, r.width - 36f, 66f), u.Desc, 14, Ui.Text, TextAnchor.UpperCenter, false, true);
            if (note.Length > 0)
                Ui.Label(new Rect(r.x, r.yMax - 50f, r.width, 16f), note, 11, new Color(1, 1, 1, 0.55f), TextAnchor.MiddleCenter, true);
            var key = new Rect(r.x + r.width / 2f - 50f, r.yMax - 30f, 100f, 20f);
            Ui.Pill(key, selected ? new Color(rc.r, rc.g, rc.b, 0.85f) : new Color(1f, 1f, 1f, 0.08f));
            Ui.Label(key, selected ? "SELECTED" : "PRESS " + (index + 1), 10, selected ? new Color(0.06f, 0.06f, 0.1f) : new Color(1, 1, 1, 0.6f), TextAnchor.MiddleCenter, true);
        }

        /// The small line on a card: where a new ability goes, a role restriction, or how many you have.
        string UpgradeNote(Player me, int index, UpgradeDef u)
        {
            int have = Upgrades.Count(me.Taken, me.Offer[index]);
            if (u.Rarity == Rarity.Mod)
                return have > 0 ? "YOU HAVE " + have + " / " + u.MaxStacks : "NEW";
            if (u.Grants != AbilityId.None)
            {
                int free = 1;
                while (free < Upgrades.BonusSlots && me.Slots[free].Id != AbilityId.None)
                    free++;
                return "Goes in your " + SlotKeys[free] + " slot";
            }
            if (u.Role.HasValue)
                return Defs.Get(u.Role.Value).Name + " only";
            return have > 0 ? "You have " + have : "";
        }

        /// Your current stats; with a card selected, the ones it would change show their new value.
        void DrawStatPanel(Player me, Rect r)
        {
            Ui.Panel(r);
            Ui.Label(new Rect(r.x + 14f, r.y + 10f, r.width - 28f, 22f), "YOUR STATS", 15, Ui.Muted, TextAnchor.MiddleLeft, true);
            Label(new Rect(r.x + 12f, r.y + 30f, r.width - 24f, 18f), me.Weapon.Name + " · " + Upgrades.Ability(me.RoleAbility).Name, 12,
                ToColor(me.Def.Color), TextAnchor.MiddleLeft);
            var now = StatSheet.Describe(me, me.Taken);
            List<StatLine> after = null;
            if (selectedCard >= 0 && me.Offer != null && selectedCard < me.Offer.Length)
            {
                var preview = new List<int>(me.Taken) { me.Offer[selectedCard] };
                after = StatSheet.Describe(me, preview);
            }
            var lines = after ?? now;
            var green = new Color(0.45f, 1f, 0.5f);
            float y = r.y + 56f;
            for (int i = 0; i < lines.Count && y < r.yMax - 20f; i++)
            {
                var line = lines[i];
                if (line.Header)
                {
                    y += 6f;
                    Label(new Rect(r.x + 12f, y, r.width - 24f, 18f), line.Label.ToUpperInvariant(), 12, new Color(1f, 0.8f, 0.45f), TextAnchor.MiddleLeft, true);
                    y += 20f;
                    continue;
                }
                // Compare with the same row in the current stats to spot what the card changes.
                string old = null;
                foreach (var n in now)
                    if (n.Label == line.Label && !n.Header)
                        old = n.Value;
                bool changed = after != null && old != line.Value;
                string value = changed && old != null ? old + "  ->  " + line.Value : line.Value;
                Label(new Rect(r.x + 12f, y, r.width - 24f, 18f), line.Label, 13, changed ? green : new Color(1, 1, 1, 0.75f), TextAnchor.MiddleLeft);
                Label(new Rect(r.x + 12f, y, r.width - 24f, 18f), changed && old == null ? "NEW " + value : value, 13, changed ? green : Color.white, TextAnchor.MiddleRight, changed);
                y += 20f;
            }
        }

        void Label(Rect r, string text, int size, Color color, TextAnchor align, bool bold = false)
        {
            var style = new GUIStyle(labelStyle) { fontSize = size, alignment = align, wordWrap = false, fontStyle = bold ? FontStyle.Bold : FontStyle.Normal };
            style.normal.textColor = color;
            GUI.Label(r, text, style);
        }

        static void Box(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        static Color ToColor(Rgba c) => new Color(c.R, c.G, c.B, c.A);
    }
}
