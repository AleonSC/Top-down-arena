using UnityEngine;

namespace Arena
{
    /// The Game Mode screen: the mode carousel and the mode's own page (Quick Play, Campaign, Boss Hunt,
    /// Survival, Training) on a full screen of their own, so they never share the big screen with the loadout. It's a view inside the lobby
    /// (the party, readiness and connection stay as they are): the host lands on it when a lobby opens, CHANGE on the lobby's mode
    /// card comes back to it (VIEW for guests), and Back, Esc or TO THE LOBBY go back to the lobby. Open while `lobbyTab` is one of
    /// the mode tabs (IsModeTab). Preview: `LOBBYTAB=8 shot.sh x lobby` (Quick Play; 4 Boss Hunt with HUNT=1).
    public partial class ArenaApp
    {
        /// The mode screen's pages (the old lobby tabs of each mode).
        static bool IsModeTab(int tab) => tab == 4 || tab == QuickTab || tab == SurvivalTab || tab == CampaignTab || tab == TrainingTab;

        /// The page the session's mode opens on.
        int ModeTabNow()
        {
            if (session.HuntMode) return 4;
            if (session.SurvivalMode) return SurvivalTab;
            if (session.QuickMode) return QuickTab;
            if (session.TrainingMode) return TrainingTab;
            return CampaignTab;
        }

        /// True while the lobby shows the mode screen.
        bool OnModeScreen => session.Resume == null && IsModeTab(lobbyTab);

        /// The lobby's mode card (left column, where the carousel was): the mode's name in its colour, its line, and CHANGE (the host)
        /// or VIEW (a guest), which opens the mode screen.
        void DrawModeCard(Rect r, float t)
        {
            bool calm = fxReduced;
            int m = (int)session.Mode;
            Color acc = ModeAccent(m) ?? CrtStyle.Ink;
            DkSub(r, acc.WithAlpha(0.07f));
            CrtKit.Frame(r, 1f, acc.WithAlpha(0.55f));
            Ui.Fill(new Rect(r.x, r.y, 3f, r.height), acc);
            const float keyW = 104f;
            float tw = r.width - keyW - 30f;
            LobbyKit.Spaced(new Rect(r.x + 14f, r.y + 5f, 80f, 12f), "MODE", 9, CrtStyle.Dim, TextAnchor.MiddleLeft, 1.3f);
            LobbyKit.Fit(new Rect(r.x + 14f, r.y + 15f, tw, 22f), ModeTitle(m), 18, 12, acc, TextAnchor.MiddleLeft, calm);
            Ui.Label(new Rect(r.x + 14f, r.y + 35f, tw, 14f), LobbyKit.Truncate(ModeSub(m).ToUpperInvariant(), 9, tw), 9, CrtStyle.Ink.WithAlpha(0.6f), TextAnchor.MiddleLeft, true);
            var key = new Rect(r.xMax - keyW - 10f, r.y + 12f, keyW, 28f);
            LobbyGlowBox(key);
            if (DkKey(key, session.IsHost ? "CHANGE" : "VIEW", false, calm, size: 13, minSize: 10))
            {
                lobbyTab = ModeTabNow();
                CloseFavorites();
            }
        }

        /// GAME MODE, where the lobby's title sits (the menu's entrance flies VECTOR SECTOR: ARENA into it, LobbyEnter.cs). `a` fades it.
        void DrawModeScreenTitle(Rect r, float a) =>
            LobbyKit.Spaced(new Rect(r.x, r.y, 300f, r.height), "GAME MODE", 16, new Color(0.56f, 0.71f, 1f, a), TextAnchor.MiddleLeft, 4.8f);

        /// The mode screen itself (drawn by DrawLobbyContent in place of the lobby).
        void DrawModeScreen(float t)
        {
            bool calm = fxReduced;
            int m = (int)session.Mode;
            Color acc = ModeAccent(m) ?? CrtStyle.Ink;
            // Header: Back (to the lobby, never out of it), the title, the carousel in the middle.
            var back = new Rect(PartyX, LobbyHeadY, LobbyHeadKey, LobbyHeadKey);
            LobbyGlowBox(back);
            if (DkKey(back, "", false, calm, PixelArt.Get("ui_back"), size: 22))
                LeaveModeScreen();  // (the CRT blink, ModeBlink.cs)
            DrawModeScreenTitle(LobbyTitleRect, 1f);
            DrawModeSwitch(new Rect(W / 2f - 260f, 24f, 520f, 52f), t);
            // Back to the lobby at the right end, with who's in it (the lobby is still open behind this).
            int ready = 0;
            foreach (var e in session.Lobby.Values)
                if (e.Ready)
                    ready++;
            var go = new Rect(W - PartyX - 280f, LobbyHeadY - 4f, 280f, 48f);
            if (session.TrainingMode)
                DrawTrainingStartKey(go);  // Training starts a drill here instead (TrainingUi.cs)
            else
            {
                LobbyGlowBox(go);
                if (DkKey(go, "TO THE LOBBY  >", true, calm, size: 18, minSize: 13, accent: acc,
                        sub: "PARTY " + session.Lobby.Count + "/" + Session.MaxPlayers + "  ·  " + ready + " READY"))
                    LeaveModeScreen();
            }

            // The mode's page.
            var page = new Rect(PartyX, ModePageTop, W - 2f * PartyX, ModePageBottom - ModePageTop);
            if (lobbyTab == TrainingTab && session.TrainingMode)
                DrawTrainingTab(page, t);  // TrainingUi.cs
            else if (lobbyTab == 4 && session.HuntMode)
                DrawHuntPicker(page, t);  // HuntUi.cs
            else if (lobbyTab == QuickTab && session.QuickMode)
                DrawQuickPlayTab(page, t);  // QuickPlayUi.cs
            else if (lobbyTab == CampaignTab)
                DrawCampaignTab(page, t);  // CampaignUi.cs
            else if (lobbyTab == SurvivalTab && session.SurvivalMode)
                DrawSurvivalTab(page, t);  // SurvivalUi.cs
            LobbyGlowBox(page);

        }
    }
}
