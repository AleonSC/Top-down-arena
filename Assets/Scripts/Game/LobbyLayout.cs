using UnityEngine;

namespace Arena
{
    /// The lobby's layout in one place: every box the lobby screen draws round its edges (header keys, mode keys, party column,
    /// Ready and Start, tab keys, the big screen), and the bands its entrances and exits cut it into. LobbyEnter.cs (in from the
    /// menu, and Leave), LobbyCrt.cs (New run's boot), Recall.cs (back from a run) and LaunchIntro.cs (a run launching) read the
    /// bands, so moving a box here carries those animations along. Check a change with `Tools/Preview/uishots.sh -r HEAD lobby`.
    ///
    /// Today's numbers are Docs/Mockups/crt-lobby-softer.html (1280 x 720): a 36 px margin all round; the party column is 376 wide at
    /// x=36 (header 30..70, mode keys 82..134, the Party line 142..168, cards from 176, Ready 586..634 over Start 640..684); the big
    /// screen runs 440..1244 by 70..684, the tab row above it at y=32 (30 tall).
    public partial class ArenaApp
    {
        // --- Left column: header, mode keys, the party, Ready and Start -------------------------------------------------------------

        const float PartyX = 36f, PartyW = 376f;  // the party column (everything on the left lines up with it)
        const float LobbyHeadY = 30f, LobbyHeadKey = 40f;  // the header row: square 40 px keys
        /// Back to the menu (top left).
        static Rect LobbyBackKey => new Rect(PartyX, LobbyHeadY, LobbyHeadKey, LobbyHeadKey);
        /// The LOBBY_ title, right of Back.
        static Rect LobbyTitleRect => new Rect(PartyX + 54f, LobbyHeadY, 250f, LobbyHeadKey);
        /// Where the title's text sits in LobbyTitleRect: the menu's entrance flies VECTOR COLOSSEUM here as it turns into LOBBY.
        static V2 LobbyTitleAnchor => new V2(LobbyTitleRect.x + 34f, LobbyTitleRect.center.y);
        /// The "!" connection key, at the column's right end (shown while the mouse is over it).
        static Rect LobbyInfoKey => new Rect(PartyX + PartyW - LobbyHeadKey, LobbyHeadY, LobbyHeadKey, LobbyHeadKey);
        /// Campaign / Boss Hunt / Practice (or the Resume banner).
        static Rect LobbyModeRect => new Rect(PartyX, 82f, PartyW, 52f);
        /// "PARTY (n/3)", and the Hangout key at the right end of the same line.
        static Rect LobbyPartyLabel => new Rect(PartyX, 146f, 220f, 22f);
        static Rect LobbyHangoutKey => new Rect(PartyX + PartyW - 104f, 142f, 104f, 26f);
        const float PartyTop = 176f;      // the top of the first party card
        const float PartyCardH = 84f, PartySlotH = 80f, PartyGap = 10f;  // the first card 84, the others and the open slots 80
        const float LobbyNoteY = 548f;    // who we're waiting for, just above Ready
        const float LobbyReadyY = 586f, LobbyReadyH = 48f, LobbyStartY = 640f, LobbyStartH = 44f;  // Ready over Start (a guest's Ready takes the Start slot)

        // --- Right: the tab keys and the big screen ---------------------------------------------------------------------------------

        const float LobbyBodyTop = 70f;   // the top of the big screen (under the tab keys)
        /// The big screen's rectangle (the loadout, the tabs' bodies and the Hangout); the menu card grows into it (LobbyEnter.cs).
        static readonly Rect LobbyBodyRect = new Rect(440f, LobbyBodyTop, 804f, 614f);
        /// The tab keys, centred over the big screen.
        static Rect LobbyTabsRect => new Rect(LobbyBodyRect.center.x - 370f, 32f, 740f, 30f);

        // --- The bands the entrances and exits cut the lobby into ----------------------------------------------------------------------

        const float LobbySplit = PartyX + PartyW + 14f;  // 426: just right of the party column, where the left and right sides slide apart
        /// The whole left side and the whole right side (Recall.cs's return and LaunchIntro.cs's launch slide them apart).
        static Rect LobbyLeftSide => new Rect(0f, 0f, LobbySplit, H);
        static Rect LobbyRightSide => new Rect(LobbySplit, 0f, W - LobbySplit, H);

        // The pieces that slide in one by one (DrawLobbyPieces, LobbyEnter.cs), each cut a few px outside the boxes it holds.
        static float LobbyHeadBandH => LobbyModeRect.y - 6f;              // the header row's band: down to just above the mode keys
        static float LobbyModeBandY => LobbyModeRect.y - 4f;
        static float LobbyPartyBandY => LobbyHangoutKey.y - 4f;
        const float LobbyCardsBandY = PartyTop - 2f, LobbyCardsBandEnd = PartyTop + PartyCardH + PartyGap;  // your party card's band
        const float LobbyTabsBandEnd = LobbyBodyTop - 6f;                 // the tab keys' band: down to just above the big screen
        static Rect LobbyBackBand => new Rect(0f, 0f, LobbyBackKey.xMax + 12f, LobbyHeadBandH);
        static Rect LobbyTitleBand => new Rect(LobbyBackBand.xMax, 0f, LobbyTitleRect.xMax + 10f - LobbyBackBand.xMax, LobbyHeadBandH);
        static Rect LobbyInfoBand => new Rect(LobbyTitleBand.xMax, 0f, LobbySplit - LobbyTitleBand.xMax, LobbyHeadBandH);
        static Rect LobbyModeBand => new Rect(0f, LobbyModeBandY, LobbySplit, LobbyPartyBandY - LobbyModeBandY);
        static Rect LobbyPartyLineBand => new Rect(0f, LobbyPartyBandY, LobbySplit, LobbyCardsBandY - LobbyPartyBandY);
        static Rect LobbyYourCardBand => new Rect(0f, LobbyCardsBandY, LobbySplit, LobbyCardsBandEnd - LobbyCardsBandY);
        static Rect LobbyOtherSlotsBand => new Rect(0f, LobbyCardsBandEnd, LobbySplit, LobbyNoteY - LobbyCardsBandEnd);
        static Rect LobbyActionsBand => new Rect(0f, LobbyNoteY, LobbySplit, H - LobbyNoteY);
        static Rect LobbyTabsBand => new Rect(LobbySplit, 0f, W - LobbySplit, LobbyTabsBandEnd);
        static Rect LobbyBodyBand => new Rect(LobbySplit, LobbyTabsBandEnd, W - LobbySplit, H - LobbyTabsBandEnd);
    }
}
