using UnityEngine;

namespace Arena
{
    /// The Practice Range's DOWNED screen (look A of Docs/Mockups/crt-practice-redesign.html): a CRT window in the middle of the arena over a
    /// light dark layer, the glitching DOWNED title, RESPAWN HERE (R) as the big key, then RESTART WAVE (the host's; Enter) and BACK TO LOBBY
    /// (the host's). The wave keeps running behind it. Respawning is Game.RequestRespawn (PracticeRespawn.cs). Outside the Practice Range the
    /// usual DOWNED band is shown instead (DownedLooks.cs).
    public partial class ArenaApp
    {
        const float PdW = 600f, PdH = 310f;
        float pdBorn = -1f;

        /// Your own DOWNED screen is up: in the Practice Range, while you're down (the pointer is free and Fire is held off, as with the wave box).
        bool PracticeDownedUp(Player me) => me != null && me.Downed && game.Practice != null && game.State == MatchState.Fighting;

        void DrawPracticeDowned(Player me)
        {
            if (!PracticeDownedUp(me))
            {
                pdBorn = -1f;
                return;
            }
            float now = Time.unscaledTime;
            if (pdBorn < 0f)
                pdBorn = now;
            bool calm = CabCalm;
            float k = calm ? 1f : Mathf.Clamp01((now - pdBorn) / 0.3f), ease = 1f - (1f - k) * (1f - k);
            RoundBackdrop(0.35f * ease);
            var r = new Rect(W / 2f - PdW / 2f, H / 2f - PdH / 2f - 24f, PdW, PdH);
            RoundTube(r, ease, false, () => DrawPracticeDownedBox(me, r, now, calm));
        }

        void DrawPracticeDownedBox(Player me, Rect r, float now, bool calm)
        {
            Color red = DownRedUi, ink = CrtUi.Ink;
            var ev = Event.current;
            bool host = session.IsHost;

            // The title, glitching for a moment every few seconds.
            var tr = new Rect(r.x, r.y + 16f, r.width, 84f);
            bool glitch = !calm && (now - pdBorn) % 3.4f < 0.22f;
            if (glitch)
            {
                float j = (DHash(Mathf.Floor(now * 30f)) - 0.5f) * 16f;
                CrtUi.Title(new Rect(tr.x - 6f + j, tr.y, tr.width, tr.height), "DOWNED", 78, new Color(red.r, red.g, red.b, 0.45f), TextAnchor.MiddleCenter, true);
                CrtUi.Title(new Rect(tr.x + 6f + j, tr.y, tr.width, tr.height), "DOWNED", 78, new Color(DownCyanUi.r, DownCyanUi.g, DownCyanUi.b, 0.4f), TextAnchor.MiddleCenter, true);
            }
            CrtUi.Title(tr, "DOWNED", 78, red, TextAnchor.MiddleCenter, calm);
            LobbyKit.Spaced(new Rect(r.x, r.y + 100f, r.width, 20f), "NO PENALTY IN PRACTICE  ·  THE WAVE KEEPS RUNNING", 11, new Color(red.r, red.g, red.b, 0.7f), TextAnchor.MiddleCenter, 1.5f);
            Ui.Fill(new Rect(r.x + 30f, r.y + 128f, r.width - 60f, 1f), new Color(red.r, red.g, red.b, 0.4f));

            // The keys.
            float x = r.x + 30f, w = r.width - 60f;
            bool respawn = LobbyKit.Key(new Rect(x, r.y + 144f, w, 60f), "[R]  RESPAWN HERE", false, calm, size: 28, minSize: 18, accent: ReviveMintUi);
            if (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.R && GUIUtility.keyboardControl == 0)
            {
                respawn = true;
                ev.Use();
            }
            if (respawn)
                game.RequestRespawn(Game.RespawnHere);
            float hw = (w - 10f) / 2f;
            var restartR = new Rect(x, r.y + 214f, hw, 46f);
            if (host)
            {
                bool restart = LobbyKit.Key(restartR, "RESTART WAVE", false, calm, size: 20, minSize: 13, accent: CrtStyle.Head);
                if (ev.type == EventType.KeyDown && (ev.keyCode == KeyCode.Return || ev.keyCode == KeyCode.KeypadEnter) && GUIUtility.keyboardControl == 0)
                {
                    restart = true;
                    ev.Use();
                }
                if (restart)
                    game.RequestRespawn(Game.RespawnRestart);
            }
            else
                LobbyKit.Key(restartR, "HOST RESTARTS THE WAVE", false, calm, size: 15, minSize: 11, locked: true);
            var lobbyR = new Rect(x + hw + 10f, r.y + 214f, hw, 46f);
            if (CanQuitToLobby)
            {
                if (LobbyKit.Key(lobbyR, "BACK TO LOBBY", false, calm, size: 20, minSize: 13))
                    RecallToLobby();
            }
            else
                LobbyKit.Key(lobbyR, "HOST LEADS BACK", false, calm, size: 15, minSize: 11, locked: true);

            Ui.Label(new Rect(r.x + 30f, r.y + 270f, w, 40f), "Respawn: full health, 1.5 s of shield, enemies stay where they are." + (game.Players.Count > 1 ? " Each player respawns on their own." : ""), 12,
                CrtStyle.Desc, TextAnchor.UpperCenter, false, true);
        }
    }
}
