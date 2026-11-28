using UnityEngine;

namespace Arena
{
    /// The lobby's layout in one place: every box the lobby screen draws round its edges (header keys, mode keys, party column,
    /// Ready and Start, tab keys, the big screen), and the bands its entrances and exits cut it into. LobbyEnter.cs (in from the
    /// menu, and Leave), LobbyCrt.cs (New run's boot), Recall.cs (back from a run) and LaunchIntro.cs (a run launching) read the
    /// bands, so moving a box here carries those animations along. Check a change with `Tools/Preview/uishots.sh -r HEAD lobby`.
    ///
    /// Today's numbers are Docs/Mockups/crt-lobby-softer.html (1280 x 720): a 36 px margin all round; the party column is 376 wide at
    /// x=36 (header 30..70, the mode card 82..134, the Party line 146..168, cards from 176, Ready 586..634 over Start 640..684); the big
    /// screen runs 440..1244 by 70..684; above it at y=34 the Saved star, the Loadout, Difficulty and Modifiers tabs, and the Practice tab at the right end.
    public partial class ArenaApp
    {
        // --- Left column: header, mode keys, the party, Ready and Start -------------------------------------------------------------

        const float PartyX = 36f, PartyW = 376f;  // the party column (everything on the left lines up with it)
        const float LobbyHeadY = 30f, LobbyHeadKey = 40f;  // the header row: square 40 px keys
        /// Back to the menu (top left).
        static Rect LobbyBackKey => new Rect(PartyX, LobbyHeadY, LobbyHeadKey, LobbyHeadKey);
        /// The LOBBY_ title, right of Back.
        static Rect LobbyTitleRect => new Rect(PartyX + 54f, LobbyHeadY, 250f, LobbyHeadKey);
        /// Where the title's text sits in LobbyTitleRect: the menu's entrance flies VECTOR SECTOR: ARENA here as it turns into LOBBY.
        static V2 LobbyTitleAnchor => new V2(LobbyTitleRect.x + 34f, LobbyTitleRect.center.y);
        /// The "!" connection key, at the column's right end (shown while the mouse is over it).
        static Rect LobbyInfoKey => new Rect(PartyX + PartyW - LobbyHeadKey, LobbyHeadY, LobbyHeadKey, LobbyHeadKey);
        /// Campaign / Boss Hunt / Survival (or the Resume banner).
        static Rect LobbyModeRect => new Rect(PartyX, 82f, PartyW, 52f);
        /// "PARTY (n/3)", the line over the party cards.
        static Rect LobbyPartyLabel => new Rect(PartyX, 146f, 220f, 22f);
        const float PartyTop = 176f;      // the top of the first party card
        const float PartyCardH = 84f, PartySlotH = 80f, PartyGap = 10f;  // the first card 84, the others and the open slots 80
        const float LobbySetupY = 512f;   // the top of the actions band (who we're waiting for, Ready and Start)
        const float LobbyReadyY = 586f, LobbyReadyH = 48f, LobbyStartY = 640f, LobbyStartH = 44f;  // Ready over Start (a guest's Ready takes the Start slot)

        // --- Right: the tab keys and the big screen ---------------------------------------------------------------------------------

        const float LobbyBodyTop = 70f;   // the top of the big screen (under the tab keys)
        /// The big screen's rectangle (the loadout, the tabs' bodies and the Hangout); the menu card grows into it (LobbyEnter.cs).
        static readonly Rect LobbyBodyRect = new Rect(440f, LobbyBodyTop, 804f, 614f);
        /// The tab row: folder tabs standing on the big screen's top edge, as tall as the header row's keys: the Saved star, Loadout,
        /// Difficulty and Modifiers from the left, Practice alone at the right end. (The modes' own pages are on the Game Mode
        /// screen, ModeScreenUi.cs.)
        const float LobbyTabY = 34f, LobbyTabH = LobbyBodyTop - LobbyTabY;
        const float LobbyLoadoutKeyW = 150f, LobbySavedW = 40f;
        /// The Saved loadouts star (FavoritesUi.cs): a small tab at the far left, before the Loadout tab.
        static Rect LobbySavedKey => new Rect(LobbyBodyRect.x + 30f, LobbyTabY, LobbySavedW, LobbyTabH);
        /// The Loadout tab, fixed right of the star (lit on the loadout and on the Perks view it opens).
        static Rect LobbyLoadoutKey => new Rect(LobbySavedKey.xMax + 4f, LobbyTabY, LobbyLoadoutKeyW, LobbyTabH);
        const float LobbyTabKeyGap = 4f;
        /// Difficulty and Modifiers, Loadout-sized, right after the Loadout tab.
        static Rect LobbyDiffKey => new Rect(LobbyLoadoutKey.xMax + LobbyTabKeyGap, LobbyTabY, LobbyLoadoutKeyW, LobbyTabH);
        static Rect LobbyModsKey => new Rect(LobbyDiffKey.xMax + LobbyTabKeyGap, LobbyTabY, LobbyLoadoutKeyW, LobbyTabH);
        /// Practice (HangoutUi.cs), alone at the big screen's right end.
        static Rect LobbyPracticeKey => new Rect(LobbyBodyRect.xMax - 30f - LobbyLoadoutKeyW, LobbyTabY, LobbyLoadoutKeyW, LobbyTabH);

        // --- The Game Mode screen (ModeScreenUi.cs): its page under the header row, down to the big screen's bottom ----------------

        const float ModePageTop = 108f, ModePageBottom = 684f;
        static Rect ModePageRect => new Rect(PartyX, ModePageTop, W - 2f * PartyX, ModePageBottom - ModePageTop);  // the menu card grows into it (LobbyEnter.cs)
        // Its pieces for the entrance (with the Back and title bands above): the carousel, TO THE LOBBY, and the page.
        const float ModeHeadBandEnd = 78f, ModeGoBandX = 930f;
        static Rect ModeCarouselBand => new Rect(LobbyTitleBand.xMax, 0f, ModeGoBandX - LobbyTitleBand.xMax, ModeHeadBandEnd);
        static Rect ModeGoBand => new Rect(ModeGoBandX, 0f, W - ModeGoBandX, ModeHeadBandEnd);
        static Rect ModePageBand => new Rect(0f, ModeHeadBandEnd, W, H - ModeHeadBandEnd);

        // --- The bands the entrances and exits cut the lobby into ----------------------------------------------------------------------

        const float LobbySplit = PartyX + PartyW + 14f;  // 426: just right of the party column, where the left and right sides slide apart
        /// The whole left side and the whole right side (Recall.cs's return and LaunchIntro.cs's launch slide them apart).
        static Rect LobbyLeftSide => new Rect(0f, 0f, LobbySplit, H);
        static Rect LobbyRightSide => new Rect(LobbySplit, 0f, W - LobbySplit, H);

        // The pieces that slide in one by one (DrawLobbyPieces, LobbyEnter.cs), each cut a few px outside the boxes it holds.
        static float LobbyHeadBandH => LobbyModeRect.y - 6f;              // the header row's band: down to just above the mode keys
        static float LobbyModeBandY => LobbyModeRect.y - 4f;
        static float LobbyPartyBandY => LobbyPartyLabel.y - 8f;
        const float LobbyCardsBandY = PartyTop - 2f, LobbyCardsBandEnd = PartyTop + PartyCardH + PartyGap;  // your party card's band
        const float LobbyTabsBandEnd = LobbyBodyTop - 6f;                 // the tab keys' band: down to just above the big screen
        static Rect LobbyBackBand => new Rect(0f, 0f, LobbyBackKey.xMax + 12f, LobbyHeadBandH);
        static Rect LobbyTitleBand => new Rect(LobbyBackBand.xMax, 0f, LobbyTitleRect.xMax + 10f - LobbyBackBand.xMax, LobbyHeadBandH);
        static Rect LobbyInfoBand => new Rect(LobbyTitleBand.xMax, 0f, LobbySplit - LobbyTitleBand.xMax, LobbyHeadBandH);
        static Rect LobbyModeBand => new Rect(0f, LobbyModeBandY, LobbySplit, LobbyPartyBandY - LobbyModeBandY);
        static Rect LobbyPartyLineBand => new Rect(0f, LobbyPartyBandY, LobbySplit, LobbyCardsBandY - LobbyPartyBandY);
        static Rect LobbyYourCardBand => new Rect(0f, LobbyCardsBandY, LobbySplit, LobbyCardsBandEnd - LobbyCardsBandY);
        static Rect LobbyOtherSlotsBand => new Rect(0f, LobbyCardsBandEnd, LobbySplit, LobbySetupY - 4f - LobbyCardsBandEnd);
        static Rect LobbyActionsBand => new Rect(0f, LobbySetupY - 4f, LobbySplit, H - (LobbySetupY - 4f));  // Difficulty, Modifiers, Ready and Start
        static Rect LobbyTabsBand => new Rect(LobbySplit, 0f, W - LobbySplit, LobbyTabsBandEnd);
        static Rect LobbyBodyBand => new Rect(LobbySplit, LobbyTabsBandEnd, W - LobbySplit, H - LobbyTabsBandEnd);
    }
}
