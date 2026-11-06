using UnityEngine;

namespace Arena
{
    /// The lobby as a cyan CRT screen, and New run opening into it (Docs/Mockups/crt-new-run-lobby.html: opening D "Terminal boot",
    /// cyan phosphor, curved glass, scanlines behind panels).
    ///   Behind the panels (DrawLobbyCrtBack): a dark cyan screen (a little see-through, so the chosen menu background still shows
    ///   faintly), a glow in the middle, scanlines and, unless Reduced effects, a slow rolling bar.
    ///   Over everything (DrawLobbyCrtGlass): the dark corners, a glass highlight, a rounded bezel and now and then a flicker.
    /// New run (StartNewRun) plays about 7.1 s, and any key or click after half a second skips to the end:
    ///   0.0 s   the saved-runs screen flares; the title, box and player card fade away
    ///   0.4 s   that screen grows from its own edges to the whole window, going from blue to cyan. It is a CRT the whole way
    ///           (glow, scanlines, dark corners, glass highlight and rounded bezel: DrawCrtGrowScreen), never a flat fill, and the
    ///           bezel and corners at the end are the lobby's own, so the hand-over to DrawLobbyCrtBack / Glass shows no change
    ///   0.7 s   a link log types itself in the middle (link, player, cat, opening lobby), parts typed with a one-second pause and blinking cursor between them, fading out between 5.6 and 5.9 s
    ///   5.5 s   a scan line sweeps down; the lobby's pieces light up one by one (the same LobbyPiece slide-ins as LobbyEnter.cs)
    /// Other ways into the lobby (Join, Continue, Back from a run) keep their own entrances and just fade the CRT in.
    /// Reduced effects and Visual effects off skip the boot and use the normal entrance.
    public partial class ArenaApp
    {
        public static readonly Color LobbyInk = new Color(0.36f, 0.88f, 1f);
        static readonly Color LobbyBg = new Color(0.008f, 0.063f, 0.10f);
        const float EnterCrtLen = 7.1f, CrtGrowFrom = 0.4f, CrtGrowTo = 1.2f, CrtPiecesAt = 5.5f, CrtLogAt = 0.7f, CrtLogCharS = 0.02f, CrtLogPause = 1.0f, CrtLogFadeAt = 5.6f, LobbyCrtBgAlpha = 1f;
        const float LobbyGlassR = 26f, LobbyGlassBezel = 3f;

        bool crtEntry;  // the lobby is opening from New run: DrawLobbyEnteringCrt instead of DrawLobbyEntering
        static Texture2D lobbyScanTex, lobbyPhosTex, lobbyGlassTex;
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
            // Phosphor lines: a faint cyan row every four (stretched like the scanlines).
            lobbyPhosTex = new Texture2D(1, 720, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var phos = new Color[720];
            for (int y = 0; y < phos.Length; y++)
                phos[y] = y % 4 == 0 ? new Color(0.3f, 0.85f, 1f, 1f) : new Color(0.3f, 0.85f, 1f, 0f);
            lobbyPhosTex.SetPixels(phos);
            lobbyPhosTex.Apply();
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
                return crtEntry ? (enterT >= CrtGrowTo ? 1f : 0f) : Seg(enterT, 0.5f, 1.3f);  // (the growing screen already wears its own glass)
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
            for (int i = 0; i < 16; i++)  // a glow in the middle: stacked, shrinking rects, many and faint so it reads as smooth
            {
                float m = 0.02f + i * 0.028f;
                Ui.Fill(new Rect(W * m, H * m, W * (1f - 2f * m), H * (1f - 2f * m)), new Color(0.05f, 0.24f, 0.34f, 0.022f * a));
            }
            var oldC = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.05f * a);
            GUI.DrawTexture(full, lobbyPhosTex);
            GUI.color = oldC;
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
                    Ui.Fill(new Rect(r.x, y0, r.width, y1 - y0), new Color(0.6f, 0.95f, 1f, Mathf.Sin((i + 0.5f) / 8f * Mathf.PI) * 0.03f * a));
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
        }

        /// New run: start the host, and (with effects on) play the CRT boot into the lobby.
        void StartNewRunCrt()
        {
            crtEntry = fxOn && !fxReduced;
            HostGame();
            if (session.Screen != AppScreen.Lobby)
                crtEntry = false;
        }

        /// The link log for the middle of the screen, from your own seat. Each line is typed in parts: the first part, a pause
        /// with the cursor blinking (CrtLogPause), then the rest ("> LINK" ... " ... ESTABLISHED.").
        string[][] CrtLogLines()
        {
            string who = string.IsNullOrEmpty(nameText) ? "PLAYER" : nameText.ToUpperInvariant();
            if (who.Length > 16)
                who = who.Substring(0, 16);
            return new[]
            {
                new[] { "> LINK", " ... ESTABLISHED." },
                new[] { "> PLAYER", " ... " + who + "." },
                new[] { "> CAT", " ... PET." },
                new[] { "> OPENING LOBBY" },
            };
        }

        /// What the log shows e seconds into the boot: the lines typed so far, the last one carrying the cursor.
        string[] CrtLogShown(float e)
        {
            var lines = CrtLogLines();
            var shown = new System.Collections.Generic.List<string>();
            float at = CrtLogAt;
            for (int i = 0; i < lines.Length; i++)
            {
                string text = "";
                for (int j = 0; j < lines[i].Length; j++)
                {
                    float dur = lines[i][j].Length * CrtLogCharS;
                    if (e < at + dur)
                    {
                        text += lines[i][j].Substring(0, Mathf.Max(0, Mathf.FloorToInt((e - at) / CrtLogCharS)));
                        shown.Add(text);
                        return shown.ToArray();
                    }
                    text += lines[i][j];
                    at += dur;
                    if (j < lines[i].Length - 1)
                    {
                        if (e < at + CrtLogPause)  // waiting, cursor blinking
                        {
                            shown.Add(text);
                            return shown.ToArray();
                        }
                        at += CrtLogPause;
                    }
                }
                shown.Add(text);
            }
            return shown.ToArray();
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
                Ui.Fill(scr, new Color(0.7f, 0.85f, 1f, 0.4f * flare * flare));
            else if (grow < 1f)
                DrawCrtGrowScreen(LerpRect(scr, full, grow), grow, e);

            // The link log, typed in the middle of the screen and fading out.
            float logA = Seg(e, CrtLogAt, CrtLogAt + 0.15f) * (1f - Seg(e, CrtLogFadeAt, CrtLogFadeAt + 0.3f));
            if (logA > 0f)
            {
                var shown = CrtLogShown(e);
                float y = H / 2f - CrtLogLines().Length * 24f;  // (placed for the whole log, so lines don't shift as it types)
                for (int i = 0; i < shown.Length; i++)
                {
                    bool current = i == shown.Length - 1;  // the last line carries the cursor
                    string line = shown[i] + (current && !fxReduced && Mathf.FloorToInt(t * 2f) % 2 == 0 ? "_" : "");
                    CrtUi.Text(new Rect(W / 2f - 330f, y + i * 48f, 700f, 48f), line, 36, new Color(LobbyInk.r, LobbyInk.g, LobbyInk.b, logA), TextAnchor.MiddleLeft, true, false);
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

        /// The saved-runs screen while it grows: the lobby's CRT scaled to the rectangle (glow, scanlines, dark corners, glass highlight,
        /// rounded bezel), with ink and background going blue to cyan. At grow = 1 it is the lobby's screen and glass exactly.
        void DrawCrtGrowScreen(Rect r, float grow, float e)
        {
            float k = EaseInOut(Seg(e, 0.3f, CrtGrowTo));  // the colour change, a little ahead of the size
            var bg = Color.Lerp(CrtUi.Bg, LobbyBg, k);
            Ui.Fill(r, bg);
            for (int i = 0; i < 16; i++)  // the glow in the middle, as in DrawLobbyCrtBack
            {
                float m = 0.02f + i * 0.028f;
                Ui.Fill(new Rect(r.x + r.width * m, r.y + r.height * m, r.width * (1f - 2f * m), r.height * (1f - 2f * m)), Color.Lerp(new Color(0.08f, 0.16f, 0.36f, 0.022f), new Color(0.05f, 0.24f, 0.34f, 0.022f), k));
            }
            GUI.BeginGroup(r);
            CrtScanlines(new Rect(0f, 0f, W, H), 1f);
            float s = Mathf.Lerp(3f, 9f, grow);
            for (int i = 0; i < 10; i++)  // dark edges: strips fading inward
            {
                float f = 1f - i / 10f, d = i * s;
                var v = new Color(0f, 0f, 0f, 0.32f * f * f);
                Ui.Fill(new Rect(0f, d, r.width, s), v);
                Ui.Fill(new Rect(0f, r.height - d - s, r.width, s), v);
                Ui.Fill(new Rect(d, 0f, s, r.height), v);
                Ui.Fill(new Rect(r.width - d - s, 0f, s, r.height), v);
            }
            for (int i = 0; i < 14; i++)  // the glass catching light, top left
            {
                float f = 1f - i / 14f;
                Ui.Fill(new Rect(0f, i * s, r.width * 0.55f * f, s), new Color(1f, 1f, 1f, 0.045f * f));
            }
            GUI.EndGroup();
            float flare = (1f - grow) * (1f - grow);  // its brightness settling
            Ui.Fill(r, new Color(0.5f, 0.95f, 1f, 0.16f * flare));
            GUI.Box(r, GUIContent.none, lobbyGlassStyle);  // the rounded bezel
        }

        /// Any key or click, after half a second, jumps the boot to its end.
        void SkipCrtEntry()
        {
            if (crtEntry && enterT > 0.5f && enterT < EnterCrtLen && (Input.anyKeyDown))
                enterT = EnterCrtLen;
        }
    }
}
