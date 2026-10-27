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
            Cursor.visible = session.Screen != AppScreen.Game || choosing || menuUp;
            tabHeld = session.Screen == AppScreen.Game && me != null && !menuUp && !choosing
                && Input.GetKey(KeyCode.Tab) && GUIUtility.keyboardControl == 0;

            if (!menuUp)
            {
                // Remember ability presses between fixed steps so quick taps aren't lost.
                if (Input.GetKeyDown(KeyCode.Space))
                    wantAbility[0] = true;
                if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.F))
                    wantAbility[Upgrades.UltSlot] = true;
                if (Input.GetKeyDown(KeyCode.Q))
                    wantAbility[1] = true;
                if (Input.GetKeyDown(KeyCode.E))
                    wantAbility[2] = true;
                if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
                    wantAbility[3] = true;
            }
            // Ignore the mute key while typing in a text box (e.g. a name with an "m" in it).
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

        void FixedUpdate()
        {
            session.Poll();
            var input = new PlayerInput
            {
                Move = new V2(Axis(KeyCode.D, KeyCode.RightArrow) - Axis(KeyCode.A, KeyCode.LeftArrow),
                              Axis(KeyCode.S, KeyCode.DownArrow) - Axis(KeyCode.W, KeyCode.UpArrow)).Normalized(),
                AimAt = MouseArena(),
                Fire = Input.GetMouseButton(0),
                Ability = wantAbility[0] || Input.GetKey(KeyCode.Space),
                AbilityQ = wantAbility[1] || Input.GetKey(KeyCode.Q),
                AbilityE = wantAbility[2] || Input.GetKey(KeyCode.E),
                AbilityShift = wantAbility[3] || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift),
                Ultimate = wantAbility[Upgrades.UltSlot] || Input.GetMouseButton(1) || Input.GetKey(KeyCode.F),
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

            // Effects behind the body.
            switch (e.Type)
            {
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
                case EnemyType.Shard:
                case EnemyType.Mortar:
                case EnemyType.Shielder:
                    rot = e.Facing;  // point where they're aiming
                    break;
                case EnemyType.Spinner:
                    rot = t * 2.5f + e.Id;
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
            var panel = new Rect(W / 2f - 330f, 80f, 660f, 560f);
            Ui.Panel(panel, true);
            Ui.Title(new Rect(panel.x, panel.y + 18f, panel.width, 44f), "HOW TO PLAY", 32, Color.white);
            string[,] rows =
            {
                { "Move", "WASD / arrow keys" },
                { "Aim / shoot", "Mouse / hold left click" },
                { "Role ability", "Space" },
                { "Ultimate", "Right click or F (one per role and class)" },
                { "Bonus abilities", "Q, E and Shift (from very rare upgrades)" },
                { "Pick an upgrade", "Click a card (or 1-3), then Confirm (Enter)" },
                { "Reroll upgrades", "R" },
                { "Stats & upgrades", "Hold Tab (just for you, doesn't pause)" },
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
            var panel = new Rect(W / 2f - 240f, 80f, 480f, 560f);
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
            var panel = new Rect(W / 2f - 240f, 110f, 480f, 470f);
            Ui.Panel(panel, true);
            Ui.Title(new Rect(panel.x, panel.y + 18f, panel.width, 44f), "SETTINGS", 34, Color.white);
            float x = panel.x + 40f, w = panel.width - 80f;
            DrawAudioOptions(x, panel.y + 82f, w);
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
            sounds.Muted = PlayerPrefs.GetInt("muted", 0) == 1;
        }

        void SaveAudioSettings()
        {
            PlayerPrefs.SetFloat("vol_master", sounds.Master);
            PlayerPrefs.SetFloat("vol_weapons", sounds.Weapons);
            PlayerPrefs.SetFloat("vol_enemies", sounds.Enemies);
            PlayerPrefs.SetFloat("vol_effects", sounds.Effects);
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
            Ui.Heading(x, y, w, "Ultimate  (right click / F)");
            Ui.Label(new Rect(x, y + 26f, w, 20f), ult.Name, 15, Ui.Gold, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(x + 150f, y + 26f, w - 150f, 36f), ult.Desc + "  (" + ult.Cooldown.ToString("0") + " s)", 12, Ui.Muted, TextAnchor.UpperLeft, false, true);
        }

        static readonly string[] SlotKeys = { "SPACE", "Q", "E", "SHIFT", "RMB / F" };

        void DrawHud()
        {
            var white = new Color(1f, 1f, 1f, 0.9f);
            var dim = new Color(1f, 1f, 1f, 0.7f);
            var me = game.Me;
            Label(new Rect(20, 10, 400, 34), "WAVE " + game.Wave + (game.Endless ? "  · ENDLESS" : " / " + Game.MaxWave), 26, white, TextAnchor.UpperLeft, true);
            Label(new Rect(20, 44, 400, 20), "Enemies left: " + game.Remaining + "    Score: " + game.Score, 14, dim, TextAnchor.UpperLeft);
            DrawBossBars();

            // Names, level-up popups and downed labels over players.
            foreach (var p in game.Players.Values)
            {
                float r = p.Def.BodyR;
                V2 at = p.Disp * WorldToUi;  // arena -> HUD coordinates
                r *= WorldToUi;
                Label(new Rect(at.X - 100f, at.Y - r - 32f, 200f, 18f), p.Name + (p.Id == game.MyId ? " (you)" : "") + "  Lv " + p.Level, 13,
                    ToColor(p.Def.Color.WithAlpha(0.9f)), TextAnchor.MiddleCenter);
                if (p.LevelFlash > 0f)
                {
                    float rise = (1.5f - p.LevelFlash) * 20f;
                    Label(new Rect(at.X - 100f, at.Y - r - 56f - rise, 200f, 22f), "LEVEL UP!", 18,
                        new Color(1f, 0.9f, 0.3f, Mathf.Min(1f, p.LevelFlash)), TextAnchor.MiddleCenter, true);
                }
                if (p.Downed)
                    Label(new Rect(at.X - 150f, at.Y + r + 12f, 300f, 18f), "DOWNED — stand close to revive", 11,
                        new Color(1f, 0.6f, 0.6f), TextAnchor.MiddleCenter);
            }

            // Player cards in the top-right corner, yours first.
            var ids = new List<int>(game.Players.Keys);
            ids.Sort((a, b) => a == game.MyId ? -1 : b == game.MyId ? 1 : a.CompareTo(b));
            float x = W - 262f * ids.Count - 8f;
            foreach (int id in ids)
            {
                var p = game.Players[id];
                const float y = 12f;
                Box(new Rect(x, y, 250, 50), new Color(0, 0, 0, 0.45f));
                Label(new Rect(x + 8, y + 2, 234, 18), p.Name + " — " + p.Def.Name + "   Lv " + p.Level, 13, ToColor(p.Def.Color), TextAnchor.UpperLeft);
                GuiBar(new Rect(x + 8, y + 22, 234, 8), p.Hp / p.MaxHp, p.Downed ? new Color(0.5f, 0.5f, 0.5f) : new Color(0.3f, 1f, 0.4f));
                GuiBar(new Rect(x + 8, y + 36, 234, 5), p.Xp / Defs.XpToNext(p.Level), new Color(1f, 0.85f, 0.3f));
                x += 262f;
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
                    Label(new Rect(0, H / 2f - 70f, W, 44), game.Wave == 0 ? "Get ready!" : "Wave " + game.Wave + " cleared!", 34, white, TextAnchor.MiddleCenter, true);
                    var choosing = new List<string>();
                    foreach (var p in game.Players.Values)
                        if (p.PendingPicks + p.PendingLegendary + p.PendingMods > 0 && p.Id != game.MyId)
                            choosing.Add(p.Name);
                    if (choosing.Count > 0)
                        Label(new Rect(0, H / 2f - 26f, W, 24), "Waiting for " + string.Join(" and ", choosing.ToArray()) + " to choose an upgrade…", 18, dim, TextAnchor.MiddleCenter);
                    else if (!game.AnyonePicking())
                        Label(new Rect(0, H / 2f - 26f, W, 24), "Wave " + (game.Wave + 1) + " starts in " + Mathf.CeilToInt(game.StateTimer), 18, dim, TextAnchor.MiddleCenter);
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
                Label(new Rect(W - 220f, H - 26f, 200f, 18f), "Hold Tab: stats & upgrades", 11, new Color(1f, 1f, 1f, 0.35f), TextAnchor.MiddleRight);
        }

        // --- Tab: your stats and loadout ---------------------------------------

        /// Held with Tab. Only drawn on this player's screen; nothing is sent to the
        /// other player and the game doesn't pause.
        void DrawTabMenu(Player me)
        {
            var panel = new Rect(40f, 66f, W - 80f, 620f);
            Ui.Panel(panel, true);
            Ui.Title(new Rect(panel.x, panel.y + 12f, panel.width, 36f), "YOUR LOADOUT", 26, Color.white);
            Ui.Label(new Rect(panel.x, panel.y + 46f, panel.width, 20f),
                Defs.KitName(me.Kit) + " " + me.Def.Name + "   ·   Level " + me.Level + "   ·   " + me.Rerolls + " reroll" + (me.Rerolls == 1 ? "" : "s") + " left",
                14, ToColor(me.Def.Color), TextAnchor.MiddleCenter, true);
            float top = panel.y + 80f, bottom = panel.yMax - 18f;
            const float pad = 24f, gap = 24f, statsW = 240f, abilW = 290f;
            float x1 = panel.x + pad, x2 = x1 + statsW + gap, x3 = x2 + abilW + gap;
            float upW = panel.xMax - pad - x3;

            // Column 1: stats (the same numbers as the upgrade screen's panel).
            Ui.Heading(x1, top, statsW, "Stats");
            float y = top + 30f;
            foreach (var line in StatSheet.Describe(me, me.Taken))
            {
                if (line.Header)
                    break;  // abilities and upgrades are listed in the other columns
                Ui.Label(new Rect(x1, y, statsW, 22f), line.Label, 14, Ui.Muted);
                Ui.Label(new Rect(x1, y, statsW, 22f), line.Value, 14, Ui.Text, TextAnchor.MiddleRight, true);
                y += 25f;
            }
            Ui.Label(new Rect(x1, y, statsW, 22f), "Health now", 14, Ui.Muted);
            Ui.Label(new Rect(x1, y, statsW, 22f), (int)me.Hp + " / " + (int)me.MaxHp, 14, Ui.Text, TextAnchor.MiddleRight, true);

            // Column 2: weapon and abilities.
            Ui.Heading(x2, top, abilW, "Weapon");
            Ui.Label(new Rect(x2, top + 30f, abilW, 22f), me.Weapon.Name, 16, Color.white, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(x2, top + 54f, abilW, 40f), me.Weapon.Desc, 12, Ui.Muted, TextAnchor.UpperLeft, false, true);
            y = top + 100f;
            Ui.Heading(x2, y, abilW, "Abilities");
            y += 30f;
            for (int i = 0; i < me.Slots.Length; i++)
            {
                var slot = me.Slots[i];
                if (slot.Id == AbilityId.None)
                    continue;
                var a = Upgrades.Ability(slot.Id);
                Ui.Badge(new Rect(x2, y + 1f, 60f, 20f), SlotKeys[i], i == Upgrades.UltSlot ? new Color(0.7f, 0.52f, 0.15f) : ToColor(me.Def.Color));
                Ui.Label(new Rect(x2 + 68f, y, abilW - 68f, 22f), a.Name, 14, Color.white, TextAnchor.MiddleLeft, true);
                string state = slot.Charges > 0 ? "ready" : Mathf.CeilToInt(slot.Timer) + "s";
                if (slot.MaxCharges > 1)
                    state = slot.Charges + "/" + slot.MaxCharges + (slot.Charges < slot.MaxCharges ? "  (" + Mathf.CeilToInt(slot.Timer) + "s)" : "");
                Ui.Label(new Rect(x2 + 68f, y, abilW - 68f, 22f), state, 12, slot.Charges > 0 ? Ui.Gold : Ui.Muted, TextAnchor.MiddleRight);
                Ui.Label(new Rect(x2 + 68f, y + 21f, abilW - 68f, 34f), a.Desc, 11, Ui.Muted, TextAnchor.UpperLeft, false, true);
                y += 60f;
            }

            // Column 3: every upgrade picked, rarest first, in two compact columns.
            var counts = new Dictionary<int, int>();
            var order = new List<int>();
            foreach (int id in me.Taken)
            {
                if (!counts.ContainsKey(id))
                {
                    counts[id] = 0;
                    order.Add(id);
                }
                counts[id]++;
            }
            order.Sort((a, b) => Upgrades.All[b].Rarity != Upgrades.All[a].Rarity
                ? Upgrades.All[b].Rarity.CompareTo(Upgrades.All[a].Rarity)
                : string.CompareOrdinal(Upgrades.All[a].Name, Upgrades.All[b].Name));
            const float rowH = 20f, subGap = 16f;
            float subW = (upW - subGap) / 2f;
            float listTop = top + 30f;
            int rowsPerCol = Mathf.FloorToInt((bottom - listTop) / rowH);
            int perPage = rowsPerCol * 2;
            // If it ever overflows, the mouse wheel scrolls the list (by rows across both columns).
            int maxScroll = Math.Max(0, order.Count - perPage);
            if (Event.current.type == EventType.ScrollWheel)
            {
                tabScroll += Event.current.delta.y > 0f ? 2 : -2;
                Event.current.Use();
            }
            tabScroll = Mathf.Clamp(tabScroll, 0, maxScroll);
            string heading = "Upgrades (" + me.Taken.Count + " picks, " + order.Count + " different)";
            Ui.Heading(x3, top, upW, heading);
            if (maxScroll > 0)
                Ui.Label(new Rect(x3, top, upW, 20f), "scroll for more", 11, Ui.Muted, TextAnchor.MiddleRight);
            if (order.Count == 0)
                Ui.Label(new Rect(x3, listTop, upW, 22f), "None yet: level up to pick some.", 13, Ui.Muted);
            for (int k = 0; k < perPage && tabScroll + k < order.Count; k++)
            {
                int id = order[tabScroll + k];
                var u = Upgrades.All[id];
                float cx = x3 + (k / rowsPerCol) * (subW + subGap);
                float cy = listTop + (k % rowsPerCol) * rowH;
                Color rc = ToColor(Upgrades.RarityColor(u.Rarity));
                Box(new Rect(cx, cy + 5f, 4f, 10f), rc);
                Ui.Label(new Rect(cx + 10f, cy, subW - 44f, rowH), u.Name, 13, rc, TextAnchor.MiddleLeft, u.Rarity >= Rarity.Rare);
                if (counts[id] > 1)
                    Ui.Label(new Rect(cx, cy, subW, rowH), "x" + counts[id], 12, Ui.Text, TextAnchor.MiddleRight, true);
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
            float y = 76f;
            const float barW = 420f;
            foreach (var e in game.Enemies.Values)
            {
                if (!e.Def.IsBoss)
                    continue;
                var col = ToColor(e.Def.Color);
                string name = e.Def.Name;
                if (e.Type == EnemyType.HiveQueen && e.Phase == 1)
                    name += "  — shielded: kill her swarm!";
                else if (Game.IsTwin(e.Type) && e.Phase == 1)
                    name += "  — ENRAGED";
                else if (e.Type == EnemyType.Singularity && AnyWells())
                    name += "  — its gravity wells swallow your shots: kill them!";
                else if (Game.SoulBound(e))
                    name += "  — SOUL BOUND: hurt the other Reaper!";
                else if (e.Type == EnemyType.Lich && e.Phase == 1)
                    name += "  — immune: destroy the phylactery!";
                else if (Game.Hungry(e))
                    name += "  — HUNGRY: stop shooting, your hits heal it!";
                else if (e.Type == EnemyType.Eclipse)
                    name += "  — phase " + (e.Phase + 1) + " of 3";
                Label(new Rect(W / 2f - barW / 2f, y - 18f, barW, 18f), name, 14, col, TextAnchor.MiddleCenter, true);
                Box(new Rect(W / 2f - barW / 2f, y, barW, 10f), new Color(0, 0, 0, 0.6f));
                Box(new Rect(W / 2f - barW / 2f, y, barW * Mathf.Clamp01(e.Hp / e.MaxHp), 10f), col);
                y += 36f;
            }
            if (game.TwinReviveT > 0f)
                Label(new Rect(0, y - 8f, W, 22f), "Kill the other Reaper in " + game.TwinReviveT.ToString("0.0") + "s or its twin revives!", 16,
                    new Color(1f, 0.5f, 0.5f), TextAnchor.MiddleCenter, true);
            else if (game.PullStrength > 0f)
                Label(new Rect(0, y - 8f, W, 22f), "GRAVITY WELL — run!", 16, new Color(0.8f, 0.6f, 1f), TextAnchor.MiddleCenter, true);
        }

        /// XP bar plus the ability slots (Space / Q / E / Shift) along the bottom.
        void DrawAbilityBar(Player me)
        {
            const float slotW = 160f, gap = 8f;
            float total = slotW * Upgrades.SlotCount + gap * (Upgrades.SlotCount - 1);
            float x0 = (W - total) / 2f, y = H - 58f;

            Box(new Rect(x0, y - 22f, total, 14f), new Color(0, 0, 0, 0.5f));
            GuiBar(new Rect(x0 + 52f, y - 18f, total - 56f, 6f), me.Xp / Defs.XpToNext(me.Level), new Color(1f, 0.85f, 0.3f));
            Label(new Rect(x0 + 4f, y - 24f, 48f, 16f), "Lv " + me.Level, 12, new Color(1f, 0.9f, 0.5f), TextAnchor.MiddleLeft, true);

            for (int i = 0; i < Upgrades.SlotCount; i++)
            {
                var slot = me.Slots[i];
                var r = new Rect(x0 + i * (slotW + gap), y, slotW, 46f);
                bool ultimate = i == Upgrades.UltSlot;
                Box(r, new Color(0, 0, 0, 0.55f));
                if (ultimate)  // gold frame, glowing when it's ready
                {
                    Color frame = slot.Charges > 0 ? new Color(1f, 0.8f, 0.35f, 0.6f + 0.3f * Mathf.Sin(Time.unscaledTime * 5f)) : new Color(1f, 0.8f, 0.35f, 0.3f);
                    Box(new Rect(r.x, r.y, r.width, 2f), frame);
                    Box(new Rect(r.x, r.yMax - 2f, r.width, 2f), frame);
                    Box(new Rect(r.x, r.y, 2f, r.height), frame);
                    Box(new Rect(r.xMax - 2f, r.y, 2f, r.height), frame);
                }
                Label(new Rect(r.x + 6, r.y + 2, 60, 14), SlotKeys[i], 10, new Color(1, 1, 1, 0.55f), TextAnchor.UpperLeft, true);
                if (slot.Id == AbilityId.None)
                {
                    Label(new Rect(r.x + 6, r.y + 16, slotW - 12, 26), "empty — find a very rare upgrade", 11, new Color(1, 1, 1, 0.35f), TextAnchor.MiddleLeft);
                    continue;
                }
                bool ready = slot.Charges > 0;
                Label(new Rect(r.x + 6, r.y + 14, slotW - 12, 18), Upgrades.Ability(slot.Id).Name, 14, ready ? Color.white : new Color(1, 1, 1, 0.5f), TextAnchor.UpperLeft, true);
                float fill = slot.Charges >= slot.MaxCharges ? 1f : 1f - slot.Timer / game.Cooldown(me, i);
                GuiBar(new Rect(r.x + 6, r.y + 36, slotW - 12, 5), fill, ultimate ? (ready ? Ui.Gold : new Color(0.6f, 0.5f, 0.25f)) : ready ? new Color(0.5f, 0.8f, 1f) : new Color(0.35f, 0.45f, 0.6f));
                string status = slot.Charges >= slot.MaxCharges ? "READY" : ultimate ? (int)(fill * 100f) + "%" : slot.Timer.ToString("0.0") + "s";
                if (slot.MaxCharges > 1)
                    status = slot.Charges + "/" + slot.MaxCharges + "  " + status;
                Label(new Rect(r.x + 6, r.y + 2, slotW - 12, 14), status, 10, new Color(1, 1, 1, 0.7f), TextAnchor.UpperRight);
            }
        }

        void DrawUpgradePicker(Player me)
        {
            SyncSelection(me);
            Box(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.55f));
            // Stats on the left; the cards and buttons fill the rest.
            const float statsX = 16f, statsW = 250f;
            const float cardW = 300f, cardH = 230f, gap = 20f;
            float x0 = statsX + statsW + 22f;
            float areaW = cardW * 3 + gap * 2, mid = x0 + areaW / 2f;
            DrawStatPanel(me, new Rect(statsX, 110f, statsW, 560f));

            if (me.OfferLegendary)
                Label(new Rect(x0, 110, areaW, 44), "BOSS REWARD! Choose a legendary weapon upgrade", 28, ToColor(Upgrades.RarityColor(Rarity.Legendary)), TextAnchor.MiddleCenter, true);
            else if (me.OfferMod)
                Label(new Rect(x0, 110, areaW, 44), "WEAPON MOD! Choose how your shots behave", 28, ToColor(Upgrades.RarityColor(Rarity.Mod)), TextAnchor.MiddleCenter, true);
            else
                Label(new Rect(x0, 110, areaW, 44), "LEVEL UP! Choose an upgrade", 34, new Color(1f, 0.9f, 0.4f), TextAnchor.MiddleCenter, true);
            int more = me.PendingPicks + me.PendingLegendary + me.PendingMods - 1;
            if (more > 0)
                Label(new Rect(x0, 152, areaW, 22), more + " more to choose after this", 16, new Color(1, 1, 1, 0.7f), TextAnchor.MiddleCenter);

            // Weapon mods show the whole list in a grid of smaller cards.
            bool grid = me.Offer.Length > 3;
            const int perRow = 4;
            float cw = grid ? (areaW - gap * (perRow - 1)) / perRow : cardW, ch = grid ? 150f : cardH;
            int rows = grid ? (me.Offer.Length + perRow - 1) / perRow : 1;
            for (int i = 0; i < me.Offer.Length; i++)
            {
                var u = Upgrades.All[me.Offer[i]];
                int row = grid ? i / perRow : 0, col = grid ? i % perRow : i;
                int inRow = grid ? Math.Min(perRow, me.Offer.Length - row * perRow) : me.Offer.Length;
                float rowX = mid - (inRow * cw + (inRow - 1) * gap) / 2f;  // center short rows
                var r = new Rect(rowX + col * (cw + gap), 200f + row * (ch + gap), cw, ch);
                Color rc = ToColor(Upgrades.RarityColor(u.Rarity));
                bool hover = r.Contains(Event.current.mousePosition);
                bool selected = selectedCard == i;
                Box(r, selected ? new Color(0.17f, 0.18f, 0.26f, 0.98f) : hover ? new Color(0.13f, 0.14f, 0.2f, 0.97f) : new Color(0.08f, 0.09f, 0.13f, 0.95f));
                float bw = selected ? 6f : hover ? 4f : 2f;
                Box(new Rect(r.x, r.y, r.width, bw), rc);
                Box(new Rect(r.x, r.yMax - bw, r.width, bw), rc);
                Box(new Rect(r.x, r.y, bw, r.height), rc);
                Box(new Rect(r.xMax - bw, r.y, bw, r.height), rc);

                if (grid)
                {
                    int stacks = Upgrades.Count(me.Taken, me.Offer[i]);
                    Label(new Rect(r.x, r.y + 10, r.width, 16), stacks > 0 ? "YOU HAVE " + stacks + " / " + u.MaxStacks : "NEW", 11, rc, TextAnchor.MiddleCenter, true);
                    Label(new Rect(r.x, r.y + 28, r.width, 26), u.Name, 18, Color.white, TextAnchor.MiddleCenter, true);
                    var small = new GUIStyle(labelStyle) { fontSize = 12, alignment = TextAnchor.UpperCenter, wordWrap = true };
                    GUI.Label(new Rect(r.x + 12, r.y + 58, r.width - 24, 64), u.Desc, small);
                    Label(new Rect(r.x, r.yMax - 24, r.width, 16), selected ? "SELECTED" : "[" + (i + 1) + "]", 12,
                        selected ? rc : new Color(1, 1, 1, 0.6f), TextAnchor.MiddleCenter, true);
                    if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                        selectedCard = i;
                    continue;
                }
                Label(new Rect(r.x, r.y + 14, r.width, 18), Upgrades.RarityName(u.Rarity), 13, rc, TextAnchor.MiddleCenter, true);
                Label(new Rect(r.x, r.y + 44, r.width, 30), u.Name, 22, Color.white, TextAnchor.MiddleCenter, true);
                var desc = new GUIStyle(labelStyle) { fontSize = 15, alignment = TextAnchor.UpperCenter, wordWrap = true };
                GUI.Label(new Rect(r.x + 18, r.y + 90, r.width - 36, 90), u.Desc, desc);
                string note = "";
                if (u.Grants != AbilityId.None)
                {
                    int free = 1;
                    while (free < Upgrades.BonusSlots && me.Slots[free].Id != AbilityId.None)
                        free++;
                    note = "Goes in your " + SlotKeys[free] + " slot";
                }
                else if (u.Role.HasValue)
                    note = Defs.Get(u.Role.Value).Name + " only";
                else
                {
                    int have = Upgrades.Count(me.Taken, me.Offer[i]);
                    if (have > 0)
                        note = "You have " + have;
                }
                Label(new Rect(r.x, r.yMax - 52, r.width, 18), note, 12, new Color(1, 1, 1, 0.5f), TextAnchor.MiddleCenter);
                Label(new Rect(r.x, r.yMax - 28, r.width, 18), selected ? "SELECTED" : "[" + (i + 1) + "]", 14,
                    selected ? rc : new Color(1, 1, 1, 0.6f), TextAnchor.MiddleCenter, true);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                    selectedCard = i;
            }

            // Confirm and reroll buttons under the cards.
            const float btnW = 260f;
            float by = 200f + rows * ch + (rows - 1) * gap + 24f;
            bool wasEnabled = GUI.enabled;
            GUI.enabled = selectedCard >= 0 && !game.RerollPending;
            string confirmText = selectedCard >= 0 ? "Confirm: " + Upgrades.All[me.Offer[selectedCard]].Name + "  [Enter]" : "Select a card to confirm";
            if (Ui.Button(new Rect(mid - btnW - 10f, by, btnW, 46f), confirmText, Ui.Primary))
                ConfirmPick();
            GUI.enabled = me.Rerolls > 0 && !game.RerollPending && !me.OfferLegendary && !me.OfferMod;
            if (Ui.Button(new Rect(mid + 10f, by, btnW, 46f), "Reroll (" + me.Rerolls + " left)  [R]"))
                game.RerollOffer();
            GUI.enabled = wasEnabled;
            string hint = me.OfferLegendary || me.OfferMod ? "Boss rewards can't be rerolled" : "Rerolls: start with " + Game.StartingRerolls + ", +2 for every boss you kill";
            if (me.OfferMod)
                hint = "Every weapon mod you can still take is shown. Each stacks up to 3 times.";
            Label(new Rect(x0, by + 46f, areaW, 18), hint, 12, new Color(1, 1, 1, 0.45f), TextAnchor.MiddleCenter);
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

        static void GuiBar(Rect r, float frac, Color c)
        {
            Box(r, new Color(0, 0, 0, 0.6f));
            Box(new Rect(r.x, r.y, r.width * Mathf.Clamp01(frac), r.height), c);
        }

        static Color ToColor(Rgba c) => new Color(c.R, c.G, c.B, c.A);
    }
}
