using System;
using UnityEngine;

namespace Arena
{
    /// Practice in the lobby (Core/HangoutGame.cs): the Practice key swaps the loadout in the big screen for the shared Practice room, the
    /// real match drawn into that window (the way the loadout preview draws its own), so the party column with everyone's class and Ready
    /// mark stays in view. Each player toggles it for themselves: the Loadout key swaps back. The swap glitches: the old screen splits into
    /// red and blue copies, tears into bands and collapses to a bright line; the new one opens out of the line through fading static.
    /// Your keys and mouse drive your character while the mouse is over the window (so the party column and the Ready key still work).
    public partial class ArenaApp
    {
        const int PracticeTab = 12;  // lobbyTab: the Practice room (LobbyUi.cs)
        const float PracticeOutT = 0.26f, PracticeInT = 0.34f;  // the swap: the old screen glitching out, then the new one in
        const float PracticeViewH = 760f;  // world height the window shows

        float practiceFxAt = -9f;   // when the last swap started
        int practiceFxFrom, practiceFxTo, practiceLastTab;
        float practiceWaitAt = -1f;  // the Practice key pressed, the room not up yet
        Vector2 practiceMouse;
        bool practiceMouseOver;
        V2 practiceFocus;
        float practiceZoom;
        Rect practiceView;

        float PracticeFxAge => Time.unscaledTime - practiceFxAt;
        bool PracticeFxOn => PracticeFxAge < PracticeOutT + PracticeInT;

        /// The Practice room is what this screen shows (the lobby, its window on Practice, the room up).
        bool PracticeShown => session.Screen == AppScreen.Lobby && lobbyTab == PracticeTab && game.Practicing;

        /// Your keys play it: shown, the swap done, nothing over it, and the mouse over the window.
        bool PracticeLive => PracticeShown && !PracticeFxOn && practiceMouseOver && !lobbyMenuOpen && !settingsOpen && inspectId < 0
            && GUIUtility.keyboardControl == 0;

        /// The lobby's Practice key: open the room if it isn't up (the host starts it for the party), and look at it.
        void OpenPractice()
        {
            CloseFavorites();
            lobbyTab = PracticeTab;
            practiceWaitAt = Time.unscaledTime;
            session.StartHangout();
        }

        /// Each lobby frame, before the big screen: notice a swap to or from Practice (whatever changed the tab) and start the glitch.
        void TickPracticeSwap()
        {
            if (session.Screen != AppScreen.Lobby)
            {
                if (lobbyTab == PracticeTab)
                    lobbyTab = 0;  // back from a run: the loadout
                practiceLastTab = lobbyTab;
                return;
            }
            if (lobbyTab == PracticeTab && !game.Practicing && Time.unscaledTime - practiceWaitAt > 4f)
                lobbyTab = 0;  // the room never came (or ended): back to the loadout
            if (lobbyTab != practiceLastTab && (lobbyTab == PracticeTab || practiceLastTab == PracticeTab))
            {
                practiceFxFrom = practiceLastTab;
                practiceFxTo = lobbyTab;
                practiceFxAt = fxReduced ? -9f : Time.unscaledTime;
                if (sounds != null && !fxReduced)
                    sounds.Play(Sfx.PauseClose, 0.5f, Time.unscaledTime);
            }
            practiceLastTab = lobbyTab;
        }

        /// The big screen during a swap: the old screen glitching out, then the new one glitching in.
        void DrawPracticeSwap(LobbyEntry me, Rect body, float t, Action<int> drawTab)
        {
            float age = PracticeFxAge;
            bool outPart = age < PracticeOutT;
            float k = outPart ? age / PracticeOutT : (age - PracticeOutT) / PracticeInT;
            int tab = outPart ? practiceFxFrom : practiceFxTo;
            bool en = GUI.enabled;
            GUI.enabled = false;  // nothing in it takes a click until it has settled
            var oc = GUI.color;
            drawTab(tab);
            bool repaint = Event.current.type == EventType.Repaint;
            if (repaint && outPart && tab != PracticeTab)
            {
                // The colour split: red and blue copies of the screen sliding apart (its live models wait).
                bool blocked = uiGlBlocked;
                uiGlBlocked = true;
                float shake = 2f + 9f * k;
                for (int side = -1; side <= 1; side += 2)
                {
                    var tint = side > 0 ? new Color(1f, 0.25f, 0.35f) : new Color(0.3f, 0.55f, 1f);
                    Ui.Alpha = 0.18f;
                    GUI.color = tint.WithAlpha(0.18f);
                    GUI.BeginGroup(new Rect(body.x + side * shake, body.y, body.width, body.height));
                    GUI.BeginGroup(new Rect(-body.x, -body.y, W, H));
                    drawTab(tab);
                    GUI.EndGroup();
                    GUI.EndGroup();
                }
                Ui.Alpha = 1f;
                GUI.color = oc;
                // Torn bands: strips of the screen jumping sideways, new ones every 1/16 s.
                int seed = Mathf.FloorToInt(age * 16f);
                for (int i = 0; i < 4; i++)
                {
                    float y = body.y + Hash01(i * 7.1f + seed * 3.7f) * body.height, h = 5f + Hash01(i + seed * 1.3f) * 22f * (0.4f + k);
                    float dx = (Hash01(seed * 5.3f + i * 2.9f) - 0.5f) * 60f * (0.3f + k);
                    GUI.BeginGroup(new Rect(body.x + dx, y, body.width, Mathf.Min(h, body.yMax - y)));
                    GUI.BeginGroup(new Rect(-body.x, -y, W, H));
                    drawTab(tab);
                    GUI.EndGroup();
                    GUI.EndGroup();
                }
                uiGlBlocked = blocked;
            }
            if (repaint)
                DrawSwapStatic(body, age, outPart ? k : 1f - k, outPart);
            GUI.color = oc;
            Ui.Alpha = 1f;
            GUI.enabled = en;
        }

        /// Over the screen: static blocks and colour-fringed tears (strong at k = 1), and the CRT line: going out, black closing from top and
        /// bottom onto a bright line in the last stretch; coming in, the black opening from that line.
        void DrawSwapStatic(Rect body, float age, float k, bool outPart)
        {
            int seed = Mathf.FloorToInt(age * 24f);
            int blocks = Mathf.RoundToInt(10f + 34f * k);
            for (int i = 0; i < blocks; i++)
            {
                float bx = body.x + Hash01(i * 3.3f + seed * 1.7f) * body.width, by = body.y + Hash01(i * 5.9f + seed * 2.3f) * body.height;
                float bw = 8f + Hash01(i * 1.1f + seed) * 90f * k, bh = 1f + Hash01(i * 4.4f + seed * 0.7f) * 5f;
                float pick = Hash01(i * 9.2f + seed * 0.3f);
                var c = pick < 0.33f ? new Color(1f, 0.3f, 0.4f) : pick < 0.66f ? new Color(0.3f, 0.6f, 1f) : CrtStyle.Head;
                Ui.Fill(new Rect(bx, by, Mathf.Min(bw, body.xMax - bx), Mathf.Min(bh, body.yMax - by)), c.WithAlpha(0.25f + 0.45f * k));
            }
            for (int i = 0; i < 3; i++)  // tears: a dark strip with a cyan and a red edge, sliding
            {
                float y = body.y + Hash01(i * 11.3f + seed * 0.9f) * (body.height - 12f), h = 3f + 9f * k * Hash01(i + seed * 0.5f);
                float dx = (Hash01(i * 2.2f + seed * 1.9f) - 0.5f) * 40f * k;
                Ui.Fill(new Rect(body.x, y, body.width, h), new Color(0.01f, 0.02f, 0.04f, 0.55f * k));
                Ui.Fill(new Rect(body.x + Mathf.Max(0f, dx), y, body.width - Mathf.Abs(dx), 1f), new Color(0.3f, 0.95f, 1f, 0.6f * k));
                Ui.Fill(new Rect(body.x + Mathf.Max(0f, -dx), y + h, body.width - Mathf.Abs(dx), 1f), new Color(1f, 0.3f, 0.4f, 0.6f * k));
            }
            // The CRT line: out, closing over its last 45%; in, opening over its first 55%.
            float shut = outPart ? Mathf.Clamp01((age / PracticeOutT - 0.55f) / 0.45f) : 1f - Mathf.Clamp01(((age - PracticeOutT) / PracticeInT) / 0.55f);
            if (shut > 0f)
            {
                float e = shut * shut * (3f - 2f * shut), half = body.height / 2f * e, mid = body.center.y;
                var dark = new Color(0.01f, 0.015f, 0.03f, 1f);
                Ui.Fill(new Rect(body.x, body.y, body.width, Mathf.Max(0f, half - 1f)), dark);
                Ui.Fill(new Rect(body.x, body.yMax - half + 1f, body.width, Mathf.Max(0f, half - 1f)), dark);
                float lineH = Mathf.Max(2f, (1f - e) * 6f + 2f);
                var line = new Rect(body.x + body.width * 0.04f * e, mid - lineH / 2f, body.width * (1f - 0.08f * e), lineH);
                Ui.Glow(line, CrtStyle.Head, e);
                Ui.Fill(line, new Color(0.9f, 0.98f, 1f, 0.35f + 0.65f * e));
            }
        }

        /// The Practice window: the real match, following you (its own health bars and names), with a small header.
        void DrawPractice(Rect view, float t)
        {
            var e = Event.current;
            practiceMouse = e.mousePosition;
            practiceMouseOver = view.Contains(practiceMouse) && GUI.enabled;
            Ui.Fill(view, new Color(0.02f, 0.025f, 0.04f, 0.92f));
            if (!game.Practicing)
            {
                if (!game.Running)  // (a run starting: the lobby leaves with an empty window)
                    Ui.Label(new Rect(view.x, view.center.y - 14f, view.width, 28f), "STARTING PRACTICE...", 16, CrtStyle.Head, TextAnchor.MiddleCenter, true);
                return;
            }
            var me = game.Me;
            // The wheel over the window steps the enemy of the spawner you stand on (HangoutSpawners.cs).
            if (e.type == EventType.ScrollWheel && practiceMouseOver && me != null)
            {
                int pad = game.SpawnerNear(me.Pos);
                if (pad >= 0)
                {
                    game.UseSpawner(pad, e.delta.y > 0f ? 1 : -1);
                    e.Use();
                }
            }
            float zoom = view.height / PracticeViewH;
            V2 focus = me != null ? me.Disp : Game.ArenaMid;
            float hw = view.width / 2f / zoom, hh = view.height / 2f / zoom;  // the camera stops at the arena's edge (plus a margin)
            focus = new V2(Mathf.Clamp(focus.X, Mathf.Min(hw - 60f, Game.Width / 2f), Mathf.Max(Game.Width - hw + 60f, Game.Width / 2f)),
                Mathf.Clamp(focus.Y, Mathf.Min(hh - 60f, Game.Height / 2f), Mathf.Max(Game.Height - hh + 60f, Game.Height / 2f)));
            practiceFocus = focus;
            practiceZoom = zoom;
            practiceView = view;
            if (BeginUiGl())
            {
                UiModelAt(new V2(view.center.x, view.center.y) - focus * zoom, zoom);
                UiClip(view);
                float gw = hw + 64f, gh = hh + 64f;
                FillRect(focus.X - gw, focus.Y - gh, gw * 2f, gh * 2f, new Rgba(0.02f, 0.025f, 0.04f, 1f));
                FillRect(0f, 0f, Game.Width, Game.Height, new Rgba(0.05f, 0.06f, 0.09f, 1f));
                var grid = new Rgba(1f, 1f, 1f, 0.04f);
                for (float gx = 0f; gx <= Game.Width; gx += 64f)
                    Line(new V2(gx, 0f), new V2(gx, Game.Height), 1f / zoom, grid);
                for (float gy = 0f; gy <= Game.Height; gy += 64f)
                    Line(new V2(0f, gy), new V2(Game.Width, gy), 1f / zoom, grid);
                var border = new Rgba(0.3f, 0.35f, 0.5f, 0.8f);
                Line(new V2(0f, 0f), new V2(Game.Width, 0f), 3f, border);
                Line(new V2(0f, Game.Height), new V2(Game.Width, Game.Height), 3f, border);
                Line(new V2(0f, 0f), new V2(0f, Game.Height), 3f, border);
                Line(new V2(Game.Width, 0f), new V2(Game.Width, Game.Height), 3f, border);
                bool fx = fxOn;
                if (fx)
                {
                    UpdateJuice();
                    DrawJuiceFloor();
                }
                DrawArenaContents(game.AnimT);
                if (fx)
                    DrawJuiceParts();
                DrawArenaOver(game.AnimT);  // the Deployable Turret, the Attack Helicopter... (WorldRenderer.cs)
                if (fx)
                {
                    SwitchToGlow(true);
                    DrawGlowPass();
                    SwitchToGlow(false);
                }
                DrawEnemyShots(game.AnimT);
                DrawPlayerOverlays(game.AnimT, PracticeMouseWorld(), aimLine && PracticeLive);  // your aim line, and each weapon's pips, meters and charges (WorldRenderer.cs)
                UiModelAt(V2.Zero, 1f);
                UiNoClip();
                var edge = CrtStyle.Head.WithAlpha(0.5f);
                var ed = new Rgba(edge.r, edge.g, edge.b, edge.a);
                Line(new V2(view.x, view.y), new V2(view.xMax, view.y), 1.5f, ed);
                Line(new V2(view.x, view.yMax), new V2(view.xMax, view.yMax), 1.5f, ed);
                Line(new V2(view.x, view.y), new V2(view.x, view.yMax), 1.5f, ed);
                Line(new V2(view.xMax, view.y), new V2(view.xMax, view.yMax), 1.5f, ed);
                EndUiGl();
            }
            DrawHangoutSpawnerTags(PracticeToUi);  // the spawner you stand on: its enemy and the keys (HangoutSpawnerLook.cs)
            // The header: PRACTICE, and what works here.
            var chip = new Rect(view.x + 10f, view.y + 10f, 300f, 38f);
            Ui.Pill(chip, new Color(0.02f, 0.03f, 0.05f, 0.75f));
            Ui.Label(new Rect(chip.x + 14f, chip.y + 3f, chip.width - 20f, 18f), "PRACTICE", 15, CrtStyle.Gold, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(chip.x + 14f, chip.y + 20f, chip.width - 20f, 15f), "PVP ON  ·  NO COOLDOWNS  ·  PADS: SPAWN ENEMIES", 10, CrtStyle.Desc, TextAnchor.MiddleLeft, true);
            if (!practiceMouseOver && !PracticeFxOn)
                Ui.Label(new Rect(view.x, view.yMax - 30f, view.width, 18f), "MOUSE OVER THE WINDOW TO PLAY", 11, CrtStyle.Dim, TextAnchor.MiddleCenter, true);
        }

        /// The mouse in the Practice arena (the window's point under it).
        V2 PracticeMouseWorld() => practiceFocus + new V2(practiceMouse.x - practiceView.center.x, practiceMouse.y - practiceView.center.y) / practiceZoom;

        /// A world point in the Practice window (UI canvas).
        V2 PracticeToUi(V2 world) => new V2(practiceView.center.x, practiceView.center.y) + (world - practiceFocus) * practiceZoom;

        /// FixedUpdate, while the room runs behind the lobby: your keys and mouse (mapped through the window) when it's live, otherwise
        /// your character stands, facing where it faced.
        PlayerInput PracticeInput(PlayerInput keys)
        {
            var me = game.Me;
            V2 aim = me != null ? me.Pos + V2.FromAngle(me.Aim) * 100f : Game.ArenaMid;
            if (!PracticeLive || practiceZoom <= 0f)
                return new PlayerInput { AimAt = aim };
            keys.AimAt = PracticeMouseWorld();
            return keys;
        }
    }
}
