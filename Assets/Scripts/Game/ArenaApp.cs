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
        const float W = Game.Width, H = Game.Height;

        static bool booted;

        Session session;
        Game game;
        string nameText = "Player";
        string addressText = "127.0.0.1";
        string portText = Session.DefaultPort.ToString();
        Role hostRole = Role.Tank;
        Role? joinRole;
        int weaponChoice;
        int selectedCard = -1;  // upgrade card highlighted, waiting for Confirm
        string shownOffer = "";
        readonly bool[] wantAbility = new bool[Upgrades.SlotCount];
        SoundBank sounds;

        Material glMaterial;
        float scale = 1f, offX, offY;  // arena -> screen pixels (top-left origin)
        GUIStyle titleStyle, labelStyle, smallStyle, centerStyle, buttonStyle, fieldStyle;
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
            if (opts.TryGetValue("autostart", out v)) int.TryParse(v, out session.AutoStart);
            if (opts.TryGetValue("start-wave", out v)) int.TryParse(v, out session.StartWave);
            game.Bot = opts.ContainsKey("bot");
            if (opts.ContainsKey("host"))
                HostGame();
            else if (opts.TryGetValue("join", out v))
            {
                addressText = v;
                session.Join(addressText, Port(), nameText, joinRole, weaponChoice);
            }
        }

        int Port()
        {
            int p;
            return int.TryParse(portText, out p) && p > 0 && p < 65536 ? p : Session.DefaultPort;
        }

        void HostGame()
        {
            if (session.Host(Port(), nameText, hostRole, weaponChoice))
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
            bool choosing = me != null && me.Offer != null;
            Cursor.visible = session.Screen != AppScreen.Game || choosing;

            // Remember ability presses between fixed steps so quick taps aren't lost.
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(1))
                wantAbility[0] = true;
            if (Input.GetKeyDown(KeyCode.Q))
                wantAbility[1] = true;
            if (Input.GetKeyDown(KeyCode.E))
                wantAbility[2] = true;
            if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
                wantAbility[3] = true;
            if (Input.GetKeyDown(KeyCode.M))
                sounds.Muted = !sounds.Muted;

            if (choosing)
            {
                SyncSelection(me);
                if (Input.GetKeyDown(KeyCode.Alpha1)) selectedCard = 0;
                else if (Input.GetKeyDown(KeyCode.Alpha2)) selectedCard = 1;
                else if (Input.GetKeyDown(KeyCode.Alpha3)) selectedCard = 2;
                else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) ConfirmPick();
                else if (Input.GetKeyDown(KeyCode.R)) game.RerollOffer();
            }

            if (session.Screen == AppScreen.Game)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                    session.Leave("");
                else if (session.IsHost && game.State == MatchState.GameOver)
                {
                    if (Input.GetKeyDown(KeyCode.R))
                        session.StartMatch();
                    else if (Input.GetKeyDown(KeyCode.L))
                        session.ReturnToLobby();
                }
            }
        }

        void FixedUpdate()
        {
            session.Poll();
            var input = new PlayerInput
            {
                Move = new V2(Axis(KeyCode.D, KeyCode.RightArrow) - Axis(KeyCode.A, KeyCode.LeftArrow),
                              Axis(KeyCode.S, KeyCode.DownArrow) - Axis(KeyCode.W, KeyCode.UpArrow)).Normalized(),
                AimAt = MouseArena(),
                Fire = Input.GetMouseButton(0),
                Ability = wantAbility[0] || Input.GetKey(KeyCode.Space) || Input.GetMouseButton(1),
                AbilityQ = wantAbility[1] || Input.GetKey(KeyCode.Q),
                AbilityE = wantAbility[2] || Input.GetKey(KeyCode.E),
                AbilityShift = wantAbility[3] || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift),
            };
            for (int i = 0; i < wantAbility.Length; i++)
                wantAbility[i] = false;
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
            return new V2((m.x - offX) / scale, (UnityEngine.Screen.height - m.y - offY) / scale);
        }

        // --- World drawing (GL immediate mode, arena coordinates) -------------

        void OnPostRender()
        {
            if (!game.Running)
                return;
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
            DrawWorld();
            GL.End();
            GL.PopMatrix();
        }

        void DrawWorld()
        {
            float t = game.AnimT;
            FillRect(0, 0, W, H, new Rgba(0.05f, 0.06f, 0.09f));
            var grid = new Rgba(1f, 1f, 1f, 0.035f);
            for (float x = 0; x <= W; x += 64f)
                Line(new V2(x, 0), new V2(x, H), 1f, grid);
            for (float y = 0; y <= H; y += 64f)
                Line(new V2(0, y), new V2(W, y), 1f, grid);
            var border = new Rgba(0.3f, 0.35f, 0.5f, 0.6f);
            Line(new V2(2, 2), new V2(W - 2, 2), 3f, border);
            Line(new V2(2, H - 2), new V2(W - 2, H - 2), 3f, border);
            Line(new V2(2, 2), new V2(2, H - 2), 3f, border);
            Line(new V2(W - 2, 2), new V2(W - 2, H - 2), 3f, border);

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
                    float a = p.BarrierT > 1f || MathX.Fmod(t, 0.2f) < 0.1f ? 0.7f : 0.25f;
                    Circle(p.Disp, p.BarrierRadius, new Rgba(0.4f, 0.7f, 1f, 0.12f), 64);
                    Arc(p.Disp, p.BarrierRadius, 3f, new Rgba(0.5f, 0.8f, 1f, a), 64);
                }
                if (p.OverdriveT > 0f)
                    Arc(p.Disp, p.Def.BodyR + 7f + 2f * Mathf.Sin(t * 20f), 2.5f, new Rgba(1f, 0.4f, 0.3f, 0.8f), 32);
            }

            foreach (var b in game.Bullets.Values)
            {
                if (b.Kind >= Defs.SmallShot)
                    continue;
                if (b.Kind == Defs.RailShot)
                    Line(b.Pos - b.Vel * 0.03f, b.Pos, 3f, Defs.BulletColor(b.Kind).WithAlpha(0.6f));
                Circle(b.Pos, b.Radius, Defs.BulletColor(b.Kind).WithAlpha(0.8f), 10);
            }

            foreach (var tur in game.Turrets.Values)
            {
                var c = new Rgba(0.6f, 0.95f, 1f);
                Polygon(tur.Pos, 13f, 6, 0f, c);
                Line(tur.Pos, tur.Pos + V2.FromAngle(tur.Aim) * 20f, 5f, c);
                Arc(tur.Pos, 18f, 2f, c.WithAlpha(0.5f), 32, -MathX.Pi / 2f, MathX.Tau * MathX.Clamp01(tur.T / 10f));
            }
            foreach (var e in game.Enemies.Values)
                DrawEnemy(e, t);
            foreach (var p in game.Players.Values)
                DrawPlayer(p, t);

            foreach (var b in game.Bullets.Values)
            {
                if (b.Kind < Defs.SmallShot)
                    continue;
                Rgba c = Defs.BulletColor(b.Kind);
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
                }
            }

            if (game.State == MatchState.GameOver)
                FillRect(0, 0, W, H, new Rgba(0f, 0f, 0f, 0.45f));

            // Crosshair
            V2 m = MouseArena();
            var ch = new Rgba(1f, 1f, 1f, 0.7f);
            Arc(m, 8f, 1.5f, ch, 16);
            Line(m - new V2(13, 0), m - new V2(5, 0), 1.5f, ch);
            Line(m + new V2(5, 0), m + new V2(13, 0), 1.5f, ch);
            Line(m - new V2(0, 13), m - new V2(0, 5), 1.5f, ch);
            Line(m + new V2(0, 5), m + new V2(0, 13), 1.5f, ch);
        }

        void DrawEnemy(Enemy e, float t)
        {
            Rgba col = e.Def.Color;
            if (game.IsSpawning(e))
            {
                Arc(e.Disp, e.Radius + 6f + 4f * Mathf.Sin(t * 12f), 2f, col.WithAlpha(0.6f), 32);
                col = col.WithAlpha(0.35f);
            }
            if (e.Flash > 0f)
                col = Rgba.White;
            float spin = e.Type == EnemyType.Spinner ? 2.5f : 0.4f;
            Polygon(e.Disp, e.Radius, e.Def.Sides, t * spin + e.Id, col);
            if (e.Hp < e.MaxHp)
            {
                float w = Mathf.Max(30f, e.Radius * 2f);
                Bar(e.Disp + new V2(-w / 2f, -e.Radius - 10f), w, 4f, e.Hp / e.MaxHp, new Rgba(1f, 0.3f, 0.3f));
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
            // The real hitbox, drawn as a bright core.
            Circle(p.Disp, p.Def.HitR, new Rgba(1f, 1f, 1f, 0.85f), 16);
            Circle(p.Disp, p.Def.HitR * 0.5f, col, 12);

            Bar(p.Disp + new V2(-20f, -r - 12f), 40f, 4f, p.Hp / p.MaxHp, new Rgba(0.3f, 1f, 0.4f));
            if (p.Downed)
                Arc(p.Disp, r + 8f, 4f, new Rgba(0.4f, 1f, 0.5f), 32, -MathX.Pi / 2f, MathX.Tau * Mathf.Max(p.Revive, 0.01f));
        }

        // --- GL primitives (arena coordinates; everything is triangles) -------

        Vector3 Px(V2 p) => new Vector3(offX + p.X * scale, UnityEngine.Screen.height - (offY + p.Y * scale), 0f);

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
            if (titleStyle != null)
                return;
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
            labelStyle.normal.textColor = new Color(0.9f, 0.92f, 1f);
            smallStyle = new GUIStyle(labelStyle) { fontSize = 13 };
            titleStyle = new GUIStyle(labelStyle) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            centerStyle = new GUIStyle(labelStyle) { alignment = TextAnchor.MiddleCenter, wordWrap = false };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 16, fixedHeight = 38 };
            fieldStyle = new GUIStyle(GUI.skin.textField) { fontSize = 16, fixedHeight = 30 };
        }

        void OnGUI()
        {
            UpdateLayout();
            InitStyles();
            GUI.matrix = Matrix4x4.TRS(new Vector3(offX, offY, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));
            switch (session.Screen)
            {
                case AppScreen.Menu:
                    DrawMenu();
                    break;
                case AppScreen.Lobby:
                    DrawLobby();
                    break;
                case AppScreen.Game:
                    DrawHud();
                    break;
            }
        }

        void DrawMenu()
        {
            GUILayout.BeginArea(new Rect(W / 2f - 280f, 90f, 560f, 560f), GUI.skin.box);
            GUILayout.Space(10);
            GUILayout.Label("TOP-DOWN ARENA", titleStyle);
            GUILayout.Label("Co-op bullet hell for two. Survive the waves.", centerStyle);
            GUILayout.Space(14);
            GUILayout.Label("Your name", smallStyle);
            nameText = GUILayout.TextField(nameText, 16, fieldStyle);
            GUILayout.Label("Host address (only needed to join)", smallStyle);
            addressText = GUILayout.TextField(addressText, 64, fieldStyle);
            GUILayout.Label("Port", smallStyle);
            portText = GUILayout.TextField(portText, 5, fieldStyle);
            GUILayout.Space(12);
            if (session.Connecting)
            {
                if (GUILayout.Button("Cancel", buttonStyle))
                    session.Leave("");
            }
            else
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Host game", buttonStyle))
                    HostGame();
                if (GUILayout.Button("Join game", buttonStyle))
                    session.Join(addressText, Port(), nameText, joinRole, weaponChoice);
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(8);
            var status = new GUIStyle(smallStyle);
            status.normal.textColor = new Color(1f, 0.75f, 0.4f);
            GUILayout.Label(session.Status, status);
            GUILayout.FlexibleSpace();
            GUILayout.Label("Controls: WASD move · Mouse aim · Left click shoot · Space / Right click ability · Q / E / Shift bonus abilities · 1-3 + Enter pick upgrade · R reroll · M mute · Esc leave", smallStyle);
            GUILayout.EndArea();
        }

        void DrawLobby()
        {
            GUILayout.BeginArea(new Rect(W / 2f - 320f, 30f, 640f, 660f), GUI.skin.box);
            GUILayout.Space(10);
            GUILayout.Label("LOBBY", titleStyle);
            GUILayout.Space(6);
            var info = new GUIStyle(smallStyle);
            info.normal.textColor = new Color(0.7f, 0.8f, 1f);
            if (session.IsHost)
                GUILayout.Label("Hosting on port " + session.Port + ". Your friend should join one of these addresses:\n" + addressList +
                                "\n(Use a 100.x.x.x address if you both run Tailscale.)", info);
            else
                GUILayout.Label("Connected. Waiting for the host to start the game.", info);
            GUILayout.Space(10);

            foreach (var e in session.Lobby.Values)
            {
                var tags = new List<string>();
                if (e.Id == 1) tags.Add("host");
                if (e.Id == session.MyId) tags.Add("you");
                var style = new GUIStyle(labelStyle) { fontSize = 18 };
                style.normal.textColor = ToColor(Defs.Get(e.Role).Color);
                GUILayout.Label("• " + e.Name + " — " + Defs.Get(e.Role).Name + ", " + Defs.Weapon(e.Role, e.Weapon).Name + "  (" + string.Join(", ", tags.ToArray()) + ")", style);
            }
            if (session.Lobby.Count < Session.MaxPlayers)
                GUILayout.Label("• waiting for a friend to join…", labelStyle);

            GUILayout.Space(10);
            GUILayout.Label("Pick your role:", smallStyle);
            LobbyEntry me;
            session.Lobby.TryGetValue(session.MyId, out me);
            GUILayout.BeginHorizontal();
            for (int i = 0; i < Defs.Roles.Length; i++)
            {
                var def = Defs.Roles[i];
                bool selected = me != null && (int)me.Role == i;
                var style = new GUIStyle(buttonStyle) { fontStyle = selected ? FontStyle.Bold : FontStyle.Normal };
                style.normal.textColor = style.hover.textColor = ToColor(def.Color);
                if (GUILayout.Button(selected ? "[ " + def.Name + " ]" : def.Name, style))
                {
                    session.RequestRole((Role)i);
                    hostRole = (Role)i;
                    joinRole = (Role)i;
                    weaponChoice = 0;
                }
            }
            GUILayout.EndHorizontal();
            if (me != null)
            {
                GUILayout.Label(Defs.Get(me.Role).Desc, smallStyle, GUILayout.MinHeight(52));
                GUILayout.Space(6);
                GUILayout.Label("Pick your weapon:", smallStyle);
                GUILayout.BeginHorizontal();
                for (int i = 0; i < Defs.WeaponsPerRole; i++)
                {
                    var wd = Defs.Weapon(me.Role, i);
                    bool selected = me.Weapon == i;
                    var style = new GUIStyle(buttonStyle) { fontStyle = selected ? FontStyle.Bold : FontStyle.Normal };
                    if (GUILayout.Button(selected ? "[ " + wd.Name + " ]" : wd.Name, style))
                    {
                        session.RequestWeapon(i);
                        weaponChoice = i;
                    }
                }
                GUILayout.EndHorizontal();
                GUILayout.Label(Defs.Weapon(me.Role, me.Weapon).Desc, smallStyle);
            }

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (session.IsHost && GUILayout.Button("Start game", buttonStyle))
                session.StartMatch();
            if (GUILayout.Button("Leave", buttonStyle))
                session.Leave("");
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        static readonly string[] SlotKeys = { "SPACE", "Q", "E", "SHIFT" };

        void DrawHud()
        {
            var white = new Color(1f, 1f, 1f, 0.9f);
            var dim = new Color(1f, 1f, 1f, 0.7f);
            var me = game.Me;
            Label(new Rect(20, 10, 400, 34), "WAVE " + game.Wave, 26, white, TextAnchor.UpperLeft, true);
            Label(new Rect(20, 44, 400, 20), "Enemies left: " + game.Remaining + "    Score: " + game.Score, 14, dim, TextAnchor.UpperLeft);

            // Names, level-up popups and downed labels over players.
            foreach (var p in game.Players.Values)
            {
                float r = p.Def.BodyR;
                Label(new Rect(p.Disp.X - 100f, p.Disp.Y - r - 32f, 200f, 18f), p.Name + (p.Id == game.MyId ? " (you)" : "") + "  Lv " + p.Level, 13,
                    ToColor(p.Def.Color.WithAlpha(0.9f)), TextAnchor.MiddleCenter);
                if (p.LevelFlash > 0f)
                {
                    float rise = (1.5f - p.LevelFlash) * 20f;
                    Label(new Rect(p.Disp.X - 100f, p.Disp.Y - r - 56f - rise, 200f, 22f), "LEVEL UP!", 18,
                        new Color(1f, 0.9f, 0.3f, Mathf.Min(1f, p.LevelFlash)), TextAnchor.MiddleCenter, true);
                }
                if (p.Downed)
                    Label(new Rect(p.Disp.X - 150f, p.Disp.Y + r + 12f, 300f, 18f), "DOWNED — stand close to revive", 11,
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
                        if (p.PendingPicks > 0 && p.Id != game.MyId)
                            choosing.Add(p.Name);
                    if (choosing.Count > 0)
                        Label(new Rect(0, H / 2f - 26f, W, 24), "Waiting for " + string.Join(" and ", choosing.ToArray()) + " to choose an upgrade…", 18, dim, TextAnchor.MiddleCenter);
                    else if (!game.AnyonePicking())
                        Label(new Rect(0, H / 2f - 26f, W, 24), "Wave " + (game.Wave + 1) + " starts in " + Mathf.CeilToInt(game.StateTimer), 18, dim, TextAnchor.MiddleCenter);
                    break;
                case MatchState.GameOver:
                    Label(new Rect(0, H / 2f - 80f, W, 60), "GAME OVER", 48, new Color(1f, 0.4f, 0.4f), TextAnchor.MiddleCenter, true);
                    Label(new Rect(0, H / 2f - 20f, W, 28), "You survived to wave " + game.Wave + " with " + game.Score + " points", 20, white, TextAnchor.MiddleCenter);
                    Label(new Rect(0, H / 2f + 14f, W, 24), session.IsHost ? "R: play again    L: back to lobby    Esc: quit" : "Waiting for the host…    Esc: quit",
                        16, dim, TextAnchor.MiddleCenter);
                    break;
            }
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
                Box(r, new Color(0, 0, 0, 0.55f));
                Label(new Rect(r.x + 6, r.y + 2, 60, 14), SlotKeys[i], 10, new Color(1, 1, 1, 0.55f), TextAnchor.UpperLeft, true);
                if (slot.Id == AbilityId.None)
                {
                    Label(new Rect(r.x + 6, r.y + 16, slotW - 12, 26), "empty — find a very rare upgrade", 11, new Color(1, 1, 1, 0.35f), TextAnchor.MiddleLeft);
                    continue;
                }
                bool ready = slot.Charges > 0;
                Label(new Rect(r.x + 6, r.y + 14, slotW - 12, 18), Upgrades.Ability(slot.Id).Name, 14, ready ? Color.white : new Color(1, 1, 1, 0.5f), TextAnchor.UpperLeft, true);
                float fill = slot.Charges >= slot.MaxCharges ? 1f : 1f - slot.Timer / game.Cooldown(me, i);
                GuiBar(new Rect(r.x + 6, r.y + 36, slotW - 12, 5), fill, ready ? new Color(0.5f, 0.8f, 1f) : new Color(0.35f, 0.45f, 0.6f));
                string status = slot.Charges >= slot.MaxCharges ? "READY" : slot.Timer.ToString("0.0") + "s";
                if (slot.MaxCharges > 1)
                    status = slot.Charges + "/" + slot.MaxCharges + "  " + status;
                Label(new Rect(r.x + 6, r.y + 2, slotW - 12, 14), status, 10, new Color(1, 1, 1, 0.7f), TextAnchor.UpperRight);
            }
        }

        void DrawUpgradePicker(Player me)
        {
            SyncSelection(me);
            Box(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.55f));
            Label(new Rect(0, 110, W, 44), "LEVEL UP! Choose an upgrade", 34, new Color(1f, 0.9f, 0.4f), TextAnchor.MiddleCenter, true);
            if (me.PendingPicks > 1)
                Label(new Rect(0, 152, W, 22), (me.PendingPicks - 1) + " more to choose after this", 16, new Color(1, 1, 1, 0.7f), TextAnchor.MiddleCenter);

            const float cardW = 300f, cardH = 230f, gap = 24f;
            float x0 = (W - cardW * 3 - gap * 2) / 2f;
            for (int i = 0; i < me.Offer.Length; i++)
            {
                var u = Upgrades.All[me.Offer[i]];
                var r = new Rect(x0 + i * (cardW + gap), 200f, cardW, cardH);
                Color rc = ToColor(Upgrades.RarityColor(u.Rarity));
                bool hover = r.Contains(Event.current.mousePosition);
                bool selected = selectedCard == i;
                Box(r, selected ? new Color(0.17f, 0.18f, 0.26f, 0.98f) : hover ? new Color(0.13f, 0.14f, 0.2f, 0.97f) : new Color(0.08f, 0.09f, 0.13f, 0.95f));
                float bw = selected ? 6f : hover ? 4f : 2f;
                Box(new Rect(r.x, r.y, r.width, bw), rc);
                Box(new Rect(r.x, r.yMax - bw, r.width, bw), rc);
                Box(new Rect(r.x, r.y, bw, r.height), rc);
                Box(new Rect(r.xMax - bw, r.y, bw, r.height), rc);

                Label(new Rect(r.x, r.y + 14, r.width, 18), Upgrades.RarityName(u.Rarity), 13, rc, TextAnchor.MiddleCenter, true);
                Label(new Rect(r.x, r.y + 44, r.width, 30), u.Name, 22, Color.white, TextAnchor.MiddleCenter, true);
                var desc = new GUIStyle(labelStyle) { fontSize = 15, alignment = TextAnchor.UpperCenter, wordWrap = true };
                GUI.Label(new Rect(r.x + 18, r.y + 90, r.width - 36, 90), u.Desc, desc);
                string note = "";
                if (u.Grants != AbilityId.None)
                {
                    int free = 1;
                    while (free < Upgrades.SlotCount - 1 && me.Slots[free].Id != AbilityId.None)
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
            const float btnW = 220f;
            float by = 200f + cardH + 24f;
            bool wasEnabled = GUI.enabled;
            GUI.enabled = selectedCard >= 0 && !game.RerollPending;
            string confirmText = selectedCard >= 0 ? "Confirm: " + Upgrades.All[me.Offer[selectedCard]].Name + "  [Enter]" : "Select a card to confirm";
            if (GUI.Button(new Rect(W / 2f - btnW - 10f, by, btnW, 40f), confirmText, buttonStyle))
                ConfirmPick();
            GUI.enabled = me.Rerolls > 0 && !game.RerollPending;
            if (GUI.Button(new Rect(W / 2f + 10f, by, btnW, 40f), "Reroll (" + me.Rerolls + " left)  [R]", buttonStyle))
                game.RerollOffer();
            GUI.enabled = wasEnabled;
            Label(new Rect(0, by + 46f, W, 18), "Rerolls: start with " + Game.StartingRerolls + ", +2 for every boss you kill", 12, new Color(1, 1, 1, 0.45f), TextAnchor.MiddleCenter);
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
