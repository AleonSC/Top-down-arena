using UnityEngine;

namespace Arena
{
    /// The lobby as a cyan CRT screen, and New run opening into it (Docs/Mockups/crt-new-run-lobby.html: opening D "Terminal boot",
    /// cyan phosphor, curved glass, scanlines behind panels).
    ///   Behind the panels (DrawLobbyCrtBack): a dark cyan screen (a little see-through, so the chosen menu background still shows
    ///   faintly), a glow in the middle, scanlines and, unless Reduced effects, a slow rolling bar.
    ///   Over everything (DrawLobbyCrtGlass): the dark corners, a glass highlight, a rounded bezel and now and then a flicker.
    /// New run (StartNewRun) plays about 3.6 s, and any key or click after half a second skips to the end:
    ///   0.0 s   the saved-runs screen flares; the title, box and player card fade away
    ///   0.4 s   that screen grows from its own edges to the whole window, going from green to cyan
    ///   0.6 s   a link log types itself in the middle (player, loadout, opening lobby), fading out by 2.3 s
    ///   2.0 s   a scan line sweeps down; the lobby's pieces light up one by one (the same LobbyPiece slide-ins as LobbyEnter.cs)
    /// Other ways into the lobby (Join, Continue, Back from a run) keep their own entrances and just fade the CRT in.
    /// Reduced effects and Visual effects off skip the boot and use the normal entrance.
    public partial class ArenaApp
    {
        public static readonly Color LobbyInk = new Color(0.36f, 0.88f, 1f);
        static readonly Color LobbyBg = new Color(0.008f, 0.063f, 0.10f);
        const float EnterCrtLen = 3.6f, CrtGrowFrom = 0.4f, CrtGrowTo = 1.2f, CrtPiecesAt = 2.0f, LobbyCrtBgAlpha = 0.88f;
        const float LobbyGlassR = 26f, LobbyGlassBezel = 3f;

        bool crtEntry;  // the lobby is opening from New run: DrawLobbyEnteringCrt instead of DrawLobbyEntering
        static Texture2D lobbyScanTex, lobbyGlassTex;
        static GUIStyle lobbyGlassStyle;

        static void InitLobbyCrt()
        {
            if (lobbyScanTex != null)
                return;
            // Scanlines: one dark row in three, stretched over the window (the same 1 px lines as CrtUi.End).
            lobbyScanTex = new Texture2D(1, 720, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var rows = new Color[720];
            for (int y = 0; y < rows.Length; y++)
                rows[y] = y % 3 == 0 ? new Color(0f, 0f, 0f, 1f) : new Color(0f, 0f, 0f, 0f);
            lobbyScanTex.SetPixels(rows);
            lobbyScanTex.Apply();
            // The glass frame, meant to be 9-sliced: clear inside a rounded rectangle, dark bezel outside it, a thin rim on the edge.
            const int size = 64;
            lobbyGlassTex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color[size * size];
            float half = size / 2f, inner = half - LobbyGlassBezel;
            var bezel = new Color(0.03f, 0.045f, 0.09f, 1f);
            var rim = new Color(0.15f, 0.23f, 0.40f, 1f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float qx = Mathf.Abs(x + 0.5f - half) - (inner - LobbyGlassR);
                    float qy = Mathf.Abs(y + 0.5f - half) - (inner - LobbyGlassR);
                    float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
                    float d = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - LobbyGlassR;  // negative inside the screen
                    float solid = Mathf.Clamp01(d + 0.5f);
                    float rimK = Mathf.Clamp01(1f - Mathf.Abs(d + 0.6f) / 0.9f);
                    Color c = Color.Lerp(bezel, rim, rimK);
                    c.a = Mathf.Max(solid, rimK * 0.9f);
                    px[y * size + x] = c;
                }
            lobbyGlassTex.SetPixels(px);
            lobbyGlassTex.Apply();
            lobbyGlassStyle = new GUIStyle();
            lobbyGlassStyle.normal.background = lobbyGlassTex;
            lobbyGlassStyle.border = new RectOffset(32, 32, 32, 32);
        }

        /// How much of the CRT shows (back, then glass) right now: all of it in the lobby, fading in or out around the entrance and leave.
        float LobbyCrtBackA()
        {
            if (LobbyLeaving)
                return 1f - Seg(leaveT, 0f, LeaveLen * 0.8f);
            if (LobbyEntering)
                return crtEntry ? (enterT >= CrtGrowTo ? 1f : 0f) : Seg(enterT, 0.3f, 1.2f);  // (the boot draws its own growing screen until then)
            return 1f;
        }

        float LobbyCrtGlassA()
        {
            if (LobbyLeaving)
                return 1f - Seg(leaveT, 0f, LeaveLen * 0.8f);
            if (LobbyEntering)
                return crtEntry ? Seg(enterT, CrtGrowTo, CrtGrowTo + 0.4f) : Seg(enterT, 0.5f, 1.3f);
            return 1f;
        }

        /// Behind the lobby's panels: the screen, its glow, scanlines and the rolling bar.
        void DrawLobbyCrtBack(float t)
        {
            float a = LobbyCrtBackA();
            if (a <= 0f || Event.current.type != EventType.Repaint)
                return;
            InitLobbyCrt();
            var full = new Rect(0f, 0f, W, H);
            Ui.Fill(full, new Color(LobbyBg.r, LobbyBg.g, LobbyBg.b, LobbyCrtBgAlpha * a));
            for (int i = 0; i < 6; i++)  // a glow in the middle: stacked, shrinking rects
            {
                float m = 0.05f + i * 0.07f;
                Ui.Fill(new Rect(W * m, H * m, W * (1f - 2f * m), H * (1f - 2f * m)), new Color(0.04f, 0.2f, 0.3f, 0.035f * a));
            }
            CrtScanlines(full, a);
            CrtRoll(full, t, a);
        }

        void CrtScanlines(Rect r, float a)
        {
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, (fxReduced ? 0.14f : 0.24f) * a);
            GUI.DrawTexture(r, lobbyScanTex);
            GUI.color = old;
        }

        /// The slow bar: a third of the screen tall, falls from above to below every 7 s (not with Reduced effects).
        void CrtRoll(Rect r, float t, float a)
        {
            if (fxReduced)
                return;
            float bh = r.height * 0.34f, by = Mathf.Lerp(r.y - bh, r.yMax, (t / 7f) % 1f);
            for (int i = 0; i < 8; i++)
            {
                float y0 = Mathf.Max(by + bh * i / 8f, r.y), y1 = Mathf.Min(by + bh * (i + 1) / 8f + 1f, r.yMax);
                if (y1 > y0)
                    Ui.Fill(new Rect(r.x, y0, r.width, y1 - y0), new Color(0.6f, 0.95f, 1f, Mathf.Sin((i + 0.5f) / 8f * Mathf.PI) * 0.05f * a));
            }
        }

        /// Over the lobby: dark corners, a glass highlight, the rounded bezel and a quick dip in brightness now and then.
        void DrawLobbyCrtGlass(float t)
        {
            float a = LobbyCrtGlassA();
            if (a <= 0f || Event.current.type != EventType.Repaint)
                return;
            InitLobbyCrt();
            var full = new Rect(0f, 0f, W, H);
            for (int i = 0; i < 10; i++)  // dark edges: strips fading inward
            {
                float k = 1f - i / 10f, d = i * 9f, s = 9f;
                var v = new Color(0f, 0f, 0f, 0.32f * k * k * a);
                Ui.Fill(new Rect(0f, d, W, s), v);
                Ui.Fill(new Rect(0f, H - d - s, W, s), v);
                Ui.Fill(new Rect(d, 0f, s, H), v);
                Ui.Fill(new Rect(W - d - s, 0f, s, H), v);
            }
            for (int i = 0; i < 14; i++)  // the glass catching light, top left
            {
                float k = 1f - i / 14f;
                Ui.Fill(new Rect(0f, i * 9f, W * 0.55f * k, 9f), new Color(1f, 1f, 1f, 0.045f * k * a));
            }
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, a);
            GUI.Box(full, GUIContent.none, lobbyGlassStyle);
            GUI.color = old;
            if (!fxReduced && Mathf.FloorToInt(t * 12f) % 41 == 0)
                Ui.Fill(full, new Color(0f, 0f, 0f, 0.1f * a));
        }

        /// New run: start the host, and (with effects on) play the CRT boot into the lobby.
        void StartNewRunCrt()
        {
            crtEntry = fxOn && !fxReduced;
            HostGame();
            if (session.Screen != AppScreen.Lobby)
                crtEntry = false;
        }

        /// The link log for the middle of the screen, from your own seat.
        string[] CrtLogLines()
        {
            string who = string.IsNullOrEmpty(nameText) ? "PLAYER" : nameText.ToUpperInvariant(), load = "LOADOUT";
            LobbyEntry me;
            if (session.Lobby.TryGetValue(session.MyId, out me))
                load = (me.Kit + " " + me.Role).ToUpperInvariant();
            return new[] { "> LINK ESTABLISHED", "> PLAYER   " + who, "> LOADOUT  " + load + " .... OK", "> OPENING LOBBY" };
        }

        /// Instead of DrawLobby while New run is opening it (see the timeline at the top).
        void DrawLobbyEnteringCrt()
        {
            if (Event.current.type != EventType.Repaint)
                return;
            InitLobbyCrt();
            float t = Time.unscaledTime, e = Mathf.Max(0f, enterT);
            var full = new Rect(0f, 0f, W, H);
            var scr = RunsScreenRect(MenuCard);

            // The menu steps aside: title, box and player card fade (the title lifts a little).
            float away = Seg(e, 0.25f, 0.75f);
            if (away < 1f)
            {
                float k = 1f - away;
                Ui.BodyGlow(MenuCard, k);
                WithAlpha(k, () =>
                {
                    Ui.BodyBox(MenuCard);
                    DrawMenuCardInside(t, false);
                });
                WithAlpha(k, () =>
                {
                    DrawMenuSecondary(t, false);
                    DrawMenuPlayerCard(t, 1f, 1f, false);
                    float lift = away * away * 30f;
                    Ui.Glitch(new Rect(0f, MenuTitleY - MenuTitleSize - lift, W, MenuTitleSize * 2f), "CARTESIAN COLOSSEUM", Mathf.RoundToInt(MenuTitleSize), Color.white, TextAnchor.MiddleCenter, Ui.Accent);
                });
                if (BeginUiGl())
                {
                    DrawAxis(new V2(W / 2f, MenuAxisY), MenuAxisHalf, t, k);
                    DrawMenuArc(t, k);
                    DrawPlotCorners(MenuCard, 7f, 22f, new Rgba(0.45f, 0.65f, 1f, 0.7f * k));
                    EndUiGl();
                }
            }

            // The saved-runs screen flares, then grows to the whole window and turns from green to cyan.
            float flare = Seg(e, 0f, CrtGrowFrom);
            float grow = EaseInOut(Seg(e, CrtGrowFrom, CrtGrowTo));
            if (e < CrtGrowFrom)
                Ui.Fill(scr, new Color(0.7f, 1f, 0.9f, 0.4f * flare * flare));
            else if (grow < 1f)
            {
                var r = LerpRect(scr, full, grow);
                var fill = Color.Lerp(CrtUi.Bg, LobbyBg, grow);
                Ui.Fill(r, new Color(fill.r, fill.g, fill.b, Mathf.Lerp(1f, LobbyCrtBgAlpha, grow)));
                var rimC = Color.Lerp(CrtUi.Ink, LobbyInk, grow);
                var rim = new Color(rimC.r, rimC.g, rimC.b, 0.5f * (1f - grow));
                Ui.Fill(new Rect(r.x, r.y, r.width, 2f), rim);
                Ui.Fill(new Rect(r.x, r.yMax - 2f, r.width, 2f), rim);
                Ui.Fill(new Rect(r.x, r.y, 2f, r.height), rim);
                Ui.Fill(new Rect(r.xMax - 2f, r.y, 2f, r.height), rim);
                GUI.BeginGroup(r);
                CrtScanlines(new Rect(0f, 0f, W, H), 1f);
                GUI.EndGroup();
                Ui.Fill(r, new Color(0.5f, 0.95f, 1f, 0.16f * (1f - grow) * (1f - grow)));  // its brightness settling
            }

            // The link log, typed in the middle of the screen and fading out.
            float logA = Seg(e, 0.6f, 0.75f) * (1f - Seg(e, 2.0f, 2.3f));
            if (logA > 0f)
            {
                var lines = CrtLogLines();
                int left = Mathf.FloorToInt((e - 0.6f) / 0.028f);
                float y = H / 2f - lines.Length * 24f;
                for (int i = 0; i < lines.Length && left > 0; i++)
                {
                    int n = Mathf.Min(left, lines[i].Length);
                    left -= lines[i].Length;
                    bool current = left <= 0;  // the line being typed carries the cursor
                    string s = lines[i].Substring(0, n) + (current && !fxReduced && Mathf.FloorToInt(t * 2f) % 2 == 0 ? "_" : "");
                    CrtUi.Text(new Rect(W / 2f - 330f, y + i * 48f, 700f, 48f), s, 36, new Color(LobbyInk.r, LobbyInk.g, LobbyInk.b, logA), TextAnchor.MiddleLeft, true, false);
                }
            }

            // A scan line sweeps down; the lobby lights up in pieces behind and under it.
            float sw = Seg(e, CrtPiecesAt, CrtPiecesAt + 1.0f);
            if (sw > 0f && sw < 1f)
            {
                float by = Mathf.Lerp(-130f, H + 20f, sw);
                for (int i = 0; i < 8; i++)
                    Ui.Fill(new Rect(0f, by + 130f * i / 8f, W, 130f / 8f + 1f), new Color(0.6f, 0.95f, 1f, 0.07f + 0.5f * Mathf.Pow((i + 1f) / 8f, 3f)));
                Ui.Fill(new Rect(0f, by + 128f, W, 3f), new Color(0.9f, 1f, 1f, 0.9f));
            }
            DrawLobbyPieces(e - (CrtPiecesAt - 0.45f), true);
        }

        /// Any key or click, after half a second, jumps the boot to its end.
        void SkipCrtEntry()
        {
            if (crtEntry && enterT > 0.5f && enterT < EnterCrtLen && (Input.anyKeyDown))
                enterT = EnterCrtLen;
        }
    }
}
