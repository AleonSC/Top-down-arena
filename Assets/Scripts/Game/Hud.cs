using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Arena
{
    /// The in-match HUD (IMGUI): DrawHud, the wave plate, player cards, ability bar and mastery, banners. Split off:
    /// HudTab.cs (Tab menu), HudBossBars.cs (boss bars), HudPicker.cs (upgrade picker), HudSummary.cs (end of run).
    public partial class ArenaApp
    {
        /// The ability bar's key labels: your Keybinds (Settings), upper case.
        static string[] SlotKeys => Keybinds.SlotLabels;

        void DrawHud()
        {
            var me = game.Me;
            DrawJuiceHud();  // damage numbers and a boss's entrance card, under the rest of the HUD (Juice.cs)
            DrawAbilityCallouts();  // what each player just cast, over their head (AbilityLooks.cs)
            DrawHoneyCallouts();  // the Honeybee Staff's SWARM and HARVEST pops (HoneybeeLook.cs)
            DrawDebtLedgers();  // Blood Debt's pops (BloodDebtLook.cs)
            DrawBloodWallHud();  // Blood Wall's drain over each tank laying one (BloodWallLook.cs)
            DrawVeinRushPops();  // Vein Rush's "+2%" and "-10%", and your cuts' flash (VeinRushLook.cs)
            DrawBathPops();  // Bloodbath's "+n" heals off the Hemomancer (BloodbathLook.cs)
            DrawKnightDpsPops();  // READY, RESET, Caught Steel's pips (KnightDpsLook.cs)
            DrawKnuckleWheelLabel();  // the Arc Knuckles' click wheel: the next move's name (ArcKnucklesWheel.cs)
            DrawRodPops();  // Lightning Rod's grey numbers off the tank (LightningRodLook.cs)
            DrawMatchEmotes();      // their emotes, above that (EmoteBar.cs)
            DrawMatchPings();       // the party's pings (PingLooks.cs)
            if (me != null)
                HudGroup(0, new Vector2(0f, 110f), ConsoleR, DrawConsoleBack);  // the CRT screen under the wave plate, keys and card (below)
            HudGroup(1, new Vector2(-150f, 30f), HudWavePlate, DrawWaveCorner);  // (the layout: HudLayout.cs)
            HudGroup(3, new Vector2(0f, -80f), HudTopRow, () =>
            {
                DrawBossBars();
                DrawObjectiveLine();  // the hunt/approach objective and layout tag (ApproachUi.cs), under the boss bars (bossBarsBottom)
            });
            DrawStageBanner();  // a boss changing phase (StageShiftLook.cs)
            DrawFreezeBanner();  // Blood Freeze's count and timer (BloodFreezeLook.cs)
            DrawChallengeHud(me);  // a challenge room's portal, title, objective and result (ChallengeLook.cs)
            DrawMiniBossBars(introHidHud ? 0f : 1f - hudT);  // (not while a boss's entrance card has the HUD off: they'd sit on the card's bars)  // the HUD hidden (H): a slim bar per boss stays at the top (HealthBars.cs)
            DrawWardenTags();   // "EXPOSED" / "STUNNED" over the Warden (WardenLook.cs)
            DrawHoldingCellHud(me);  // the net's fade, the Holding Cell panel, a teammate's padlock (HoldingCellLook.cs)
            DrawUnlockToasts(W / 2f, HudUnlockToastY);  // SavesUi.cs

            // Names, level-up popups and downed labels over players (hidden while they're under
            // the between-waves banner, so they don't show on top of its text).
            bool banner = game.State == MatchState.Intermission && (me == null || me.Offer == null);
            DrawNameLabels();  // Settings > Gameplay > Name labels, or N (EnemyTagLook.cs)
            foreach (var p in game.Players.Values)
            {
                float r = p.Def.BodyR;
                V2 at = ToUi(p.Disp);  // arena -> HUD coordinates (with the camera lean)
                r *= UiWorldScale;
                if (banner && !LaunchHoldsBanner && at.X > BannerRect.x - 100f && at.X < BannerRect.xMax + 100f && at.Y > BannerRect.y - 20f && at.Y < BannerRect.yMax + r + 60f)
                    continue;
                if (LaunchHides(p))
                    continue;  // not landed yet (LaunchIntro.cs)
                DrawLaunchIcon(p, at, r);
                if (p.Id != game.MyId)  // (no text over your own player; the name rises over a Juggernaut Suit's armour bar)
                    DrawName(new Rect(at.X - 100f, at.Y - r - 30f - (Game.JuggOn(p) ? 8f * UiWorldScale : 0f) - NameLift(p), 200f, 18f), p.Name, 12, NameFxOf(p.Id), Time.unscaledTime,
                    p.Def.Color.WithAlpha(0.95f).ToColor(), TextAnchor.MiddleCenter, true);  // their name effect, if any (NameEffects.cs)
                if (p.LevelFlash > 0f)
                {
                    float rise = (1.5f - p.LevelFlash) * 22f;
                    Ui.Glitch(new Rect(at.X - 100f, at.Y - r - 58f - rise * (Juicy ? 2f : 1f), 200f, 30f), "LEVEL UP!", Juicy ? 25 : 19,
                        new Color(1f, 0.88f, 0.35f, Mathf.Min(1f, p.LevelFlash)), TextAnchor.MiddleCenter);
                }
                if (p.Downed)
                {
                    var tag = new Rect(at.X - 92f, at.Y + r + 10f, 184f, 34f);
                    DkHud(tag, new Color(1f, 0.35f, 0.35f));
                    Ui.Label(new Rect(tag.x, tag.y + 3f, tag.width, 14f), "DOWNED — stand close to revive", 10, new Color(1f, 0.7f, 0.7f), TextAnchor.MiddleCenter, true);
                    Ui.Bar(new Rect(tag.x + 10f, tag.y + 21f, tag.width - 20f, 6f), p.Revive, Ui.Good);
                }
            }

            DrawOffscreenMarkers();  // zoomed in: arrows to the enemies and teammates off the edge (OffscreenMarkers.cs)
            DrawRoomsMinimap();  // the Rooms arena's minimap, top right (RoomsLook.cs)
            DrawGateHint();      // by a shutter gate: the key to shut it (GateLook.cs)
            DrawBallistaHint();  // by a Ballista or on one: the key to mount or leave it (BallistaLook.cs)
            DrawMysteryHint();   // mystery doors' price tags and keys (MysteryDoorLook.cs)
            DrawHospitalHint();  // the Field Hospital's pick hint, time left and eject countdown (FieldHospitalLook.cs)
            DrawZoomHint();      // the open Rooms map: "scroll to zoom out" (CameraZoom.cs)
            DrawGateToast(me);   // the boss gate just opened (BossGateLook.cs)
            DrawDevSpawnerTags();  // DevSpawnerLook.cs
            DrawHangoutSpawnerTags();  // the Hangout spawner you stand on: its enemy and the keys (HangoutSpawnerLook.cs)
            DrawEnemyTags();  // an enemy type's name and job for its first few sightings (EnemyTagLook.cs)
            DrawHoverName();  // the name of the enemy under the cursor, if Settings has it on (EnemyTagLook.cs)

            HudGroup(2, new Vector2(150f, 30f), HudMyCard, DrawPlayerCorner);
            if (me != null)
            {
                HudGroup(0, new Vector2(0f, 110f), HudBarPivot, () => DrawAbilityBar(me));
                HudGroup(0, new Vector2(0f, 110f), ConsoleR, DrawConsoleGlass);  // scanlines and glass over all of it
            }
            DrawMiniHud(me);  // corner text / on the player (HudModes.cs)
            DrawMyLedger();  // Blood Debt's ledger card beside you, over the enemy tags (BloodDebtLook.cs)
            DrawDownedBand(me);  // the DOWNED and REVIVED scan band (DownedLooks.cs)
            if (me != null && !me.Downed)
                DrawEmoteBar(HudEmoteBarAt.X, HudEmoteBarAt.Y);  // hold the Emote key: the bar above the ability bar (EmoteBar.cs)
            DrawPingWheel();  // hold the Ping key: the wheel at the cursor (PingLooks.cs)
            DrawGadgetWheel();  // hold the Gadget key with gadgets in the bag: the wheel at the cursor (GadgetWheel.cs)
            DrawGadgetChip(me);  // the gadget in hand and the bag beside the keys (GadgetWheel.cs)
            DrawChat();  // the chat box, bottom right above the player cards (ChatBox.cs)
            DrawTreasureHud(me);  // your chest item's card, or what the wave is waiting for (PropsLook.cs)

            switch (game.State)
            {
                case MatchState.Intermission:
                    // While the pause menu is up these full-screen choices stay out of its way: they're drawn first, so their click
                    // areas would take the clicks that belong to the menu (Leave to lobby did nothing while a pick was on screen).
                    bool pauseUp = PauseMenuWanted;
                    if (me != null && game.PickHeld(me))
                    {
                        if (!tabHeld && !pauseUp)
                            DrawPickHeld(me);  // a level-up won mid-fight: its cards wait until no enemy is near (Game.PickHeld)
                        break;
                    }
                    if (me != null && me.Offer != null)
                    {
                        if (!tabHeld && !PickOutroPlaying && !pauseUp)
                            DrawUpgradePicker(me);  // holding Tab swaps the cards for the Tab menu; the next pick waits for the last one's outro (PickOutro.cs)
                        break;
                    }
                    if (game.PathOffer != null && !tabHeld)
                    {
                        if (!pauseUp)
                            DrawPathChoice(Time.unscaledTime);  // PathsUi.cs
                        break;
                    }
                    if (game.HuntMapVoting && game.HuntRoute != null && !tabHeld)
                    {
                        if (!pauseUp)
                            DrawHuntMap(game.HuntRoute, game.Hunt, Time.unscaledTime);  // HuntMapUi.cs
                        break;
                    }
                    if (KeepsakeWindowUp && !tabHeld)
                    {
                        if (!pauseUp)
                            DrawKeepsakeVote(Time.unscaledTime);  // the room to keep through the map reset (KeepsakeUi.cs)
                        break;
                    }
                    if (ExpandChipUp && !pauseUp && !WaveBoxUp(me))
                        DrawExpandChip(me);  // an optional room offer when the wave box isn't up (its tabs / minimized strip carry Build otherwise; ExpansionUi.cs)
                    if (ExpandWindowUp && !tabHeld)
                    {
                        if (!pauseUp)
                            DrawExpansionChoice(Time.unscaledTime);  // ExpansionUi.cs
                        break;
                    }
                    if (ShopScreenUp(me))
                    {
                        if (!tabHeld && !pauseUp)
                            DrawShop(me, Time.unscaledTime);  // ShopUi.cs
                        break;
                    }
                    if (!pauseUp && !tabHeld)
                        DrawBuilderShop(me, Time.unscaledTime);  // standing in a Builder room in a build round: its card shelf (BuilderShopUi.cs)
                    if (game.ShopOpen && me != null && !pauseUp)
                        DrawShopRoomHint(me);  // the Expanding arena: walk to the shop room (ExpansionUi.cs)
                    if (game.HangoutOn)
                        break;  // (Practice plays in the lobby's window, HangoutUi.cs: no wave band)
                    var choosing = new List<string>();
                    foreach (var p in game.Players.Values)
                        if (p.PendingPicks + p.PendingLegendary + p.PendingMods + p.PendingCards + p.PendingMilestones + p.PrizePicks > 0 && p.Id != game.MyId)
                            choosing.Add(p.Name);
                    string sub = choosing.Count > 0 ? "Waiting for " + string.Join(" and ", choosing.ToArray()) + " to choose an upgrade…"
                        : game.AnyonePicking() ? ""
                        : ApproachLine(game.NextTrackWave) + "Wave " + (game.ShownWave + 1) + " starts in " + Mathf.CeilToInt(game.StateTimer);  // ApproachUi.cs
                    if (game.SurvivalOn && game.SurvStarted)
                    {
                        DrawSurvivalPick(choosing);  // the clock is held for a level-up pick (SurvivalUi.cs)
                        break;
                    }
                    if (LaunchHoldsBanner)
                        break;  // a run launching: GET READY waits until the players have landed (LaunchIntro.cs)
                    if (game.ChallengePhase == RoomPhase.Portal)
                        break;  // a portal is open: its own call instead (ChallengeLook.cs)
                    if (DrawWaveCleared(me, sub))  // the squad tally with Ready and Auto-ready (WaveClearedUi.cs)
                    {
                        if (!wcMin)
                            DrawNewEnemyChips(WcY + WcH + 6f, FxCalm ? 1f : Mathf.Clamp01((Time.unscaledTime - wcBorn - 0.2f) / 0.3f));  // what the next wave brings that you haven't met (NewEnemyChips.cs)
                        break;
                    }
                    DrawBanner(game.Wave == 0 ? "GET READY" : game.BossNow ? "BOSS CLEARED" : "WAVE " + game.ShownWave + " CLEARED", sub, game.BossNow ? CrtStyle.Gold : Ui.Accent);
                    DrawNewEnemyChips(BandRect.yMax + 14f, Mathf.Clamp01((Time.unscaledTime - bandShownAt) / 0.4f));
                    break;
                case MatchState.Victory:
                case MatchState.GameOver:
                    if (!pauseMenuOpen && !tabHeld && !RecallActive)
                    {
                        if (game.State == MatchState.GameOver && game.RetryReady)
                            DrawLifePrompt();  // a life left: Respawn or Game over first (LivesUi.cs)
                        else
                            DrawRunSummary();
                    }
                    break;
            }
            if (game.SurvivalOn && game.SurvStarted && !tabHeld && !PauseMenuWanted)
                DrawSurvivalPhase();  // a new faction joins (SurvivalUi.cs)
            if (game.State == MatchState.Fighting && !PauseMenuWanted)
                DrawBreachWarning();  // a breach wave's start: letterbox and BREACH n/N while the camera tours (BreachTour.cs)
            if (me != null && !PauseMenuWanted && game.State == MatchState.Fighting && ExpandWindowUp && !tabHeld)  // (between waves the Intermission case above draws the window)
                DrawExpansionChoice(Time.unscaledTime);
            if (!tabHeld)
                DrawSurpriseBanner();  // SurpriseLook.cs
            if (!tabHeld)
                DrawMapResetBanner();  // MAP n/10 after a map reset (MapResetLook.cs)
            if (!tabHeld && game.Running)
                DrawStashLabels();  // the +10 over a Stash's cache (SurpriseLook.cs)
            if (!tabHeld && game.Running)
                DrawChestRewardTags();  // "+56 GOLD" / "+24 VECTORS" over Gold and Vector chests (ChestsTabUi.cs)
            if (!tabHeld && game.Running)
                DrawVectorGainHud();  // "+10 VECTORS" over you and the shards into the chip (VectorGainLook.cs)
            if (!tabHeld && game.Running)
                DrawOverchargeHud();
            if (!tabHeld && game.Running)
                DrawHiveHud();  // "SLOWED" under enemies a Hive Censer swarm wraps (HiveCenserLook.cs)  // Overcharge's seconds and its pop (StaticShockLook.cs)
            if (!tabHeld && game.Running)
                DrawLootSweepHud();  // "+86 GOLD" and your coins streaming in as the wave starts (LootSweepLook.cs)
            if (tabHeld)
                DrawRunStatsScreen(me, false);  // RunStatsUi.cs
            else if (me != null && game.State != MatchState.GameOver && game.State != MatchState.Victory)
            {
                // The TAB / H hint: for the first waves, until you've opened the Tab screen a couple of times; then it fades out.
                bool hintWanted = game.ShownWave <= TabHintWaves && tabOpens < TabHintOpens;
                if (tabHintFrame != Time.frameCount)  // (once a frame: OnGUI runs per event)
                {
                    tabHintFrame = Time.frameCount;
                    tabHintA = Mathf.MoveTowards(tabHintA, hintWanted ? 1f : 0f, Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime / 0.8f : 1f);
                }
                if (!pauseMenuOpen && tabHintA > 0.01f)  // (the pause menu carries this hint at the foot of its drawer)
                HudGroup(3, new Vector2(0f, -60f), HudHintPill, () =>
                {
                    var hint = HudHintPill;
                    Ui.Pill(hint, new Color(0f, 0f, 0f, 0.35f * tabHintA));
                    Ui.Label(hint, "Hold TAB  ·  run stats & build      H  ·  HUD mode", 11, new Color(1f, 1f, 1f, 0.55f * tabHintA), TextAnchor.MiddleCenter, true);
                });
                // With the HUD hidden, a quiet reminder of how to get it back, so nobody's locked out of it.
                float back = hudMode == HudHidden ? Mathf.Clamp01((0.35f - hudT) / 0.35f) : 0f;  // (only when fully hidden: the partial HUDs are their own reminder)
                if (back > 0f && !LaunchActive && !RecallActive)  // (not while a run launches, the HUD on its way in, or beams out: Recall.cs)
                {
                    float breathe = 0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * 2f);
                    Ui.Shadowed(new Rect(0f, HudHiddenHintY + 8f * (1f - back), W, 18f), "Press " + Keybinds.KeyName(Keybinds.Get(KeyAction.ToggleHud)) + " to show the HUD", 12, new Color(1f, 1f, 1f, 0.38f * back * breathe));
                }
            }
            if (!tabHeld)
                DrawLagChart();  // F5, over the name tags (LagChartUi.cs)
            DrawSnapshotToast();  // F6 / F7 (SnapshotUi.cs)
            if (!tabHeld && !pauseMenuOpen)
                DrawDrill();  // a Training drill's beacons, card and result (DrillUi.cs)
            if (!tabHeld && !pauseMenuOpen)
                DrawTutorial();  // a first Quick Play's prompts, over the build and upgrade screens they teach (TutorialUi.cs)
        }

        // --- Hiding the HUD (H) ---------------------------------------------------------

        bool hudShown;  // the full console is up: retired, always false (HudModes.cs)
        float hudT = 0f;               // 0 hidden .. 1 shown, eased in Update
        const float HudFadeTime = 0.55f, HudStagger = 0.14f;
        const int HudGroups = 4;       // 0 ability bar, 1 wave panel, 2 player cards, 3 top row (boss bars, hints)

        /// One piece of the HUD, played in its turn: showing, the ability bar rises first, then the side panels slide
        /// in, then the top row drops; hiding runs the other way. Each piece slides, scales up from 90% and fades,
        /// overshooting a little as it lands (and pulling back before it leaves).
        void HudGroup(int order, Vector2 away, Rect pivot, Action draw)
        {
            float p = Mathf.Clamp01(hudT * (1f + HudStagger * (HudGroups - 1)) - HudStagger * order);
            if (p <= 0f)
                return;
            if (p >= 1f)
            {
                draw();
                return;
            }
            const float back = 1.70158f;  // ease-out-back
            float q = p - 1f, e = 1f + (back + 1f) * q * q * q + back * q * q;
            float k = 0.9f + 0.1f * e, a = p * p * (3f - 2f * p);
            Vector2 c = pivot.center;
            float ox = away.x * (1f - e) + c.x * (1f - k), oy = away.y * (1f - e) + c.y * (1f - k);
            var oldMatrix = GUI.matrix;
            var oldColor = GUI.color;
            GUI.matrix = Matrix4x4.TRS(new Vector3(offX + ox * scale, offY + oy * scale, 0f), Quaternion.identity, new Vector3(scale * k, scale * k, 1f));
            GUI.color = oldColor.ScaleAlpha(a);
            Ui.Alpha = a;
            draw();
            Ui.Alpha = 1f;
            GUI.color = oldColor;
            GUI.matrix = oldMatrix;
        }

        // --- Beside the hotbar ----------------------------------------------------------

        // The wave panel sits left of the ability bar and your card right of it; the path and modifier chips stack up from the wave
        // panel, teammates from your card. Where everything goes: HudLayout.cs.

        void DrawWaveCorner()
        {
            DrawWavePlate(HudWavePlate);
            float y = HudChipsY;
            if (game.CurrentPath != PathId.None)
            {
                DrawPathChip(SideL, y);  // PathsUi.cs
                y -= HudChipLine;
            }
            if (DrawHuntNodeChip(SideL, y))  // HuntMapUi.cs
                y -= HudChipLine;
            if (DrawVectorChip(SideL, y))  // ExpansionUi.cs
                y -= HudChipLine;
            if (DrawRelicChip(SideL, y))  // NodeLook.cs
                y -= HudChipLine;
            if (DrawServiceChip(SideL, y))  // ExpansionUi.cs
                y -= HudChipLine;
            if (DrawGoldChip(SideL, y))  // ShopUi.cs
                y -= HudChipLine;
            if (DrawLivesChip(SideL, y))  // LivesUi.cs
                y -= HudChipLine;
            DrawActiveModifiers(DrawActiveDifficulty(SideL, y + 2f), y + 2f);  // DifficultyUi.cs, ModifiersUi.cs
        }

        readonly List<int> cornerIds = new List<int>();  // (scratch: DrawPlayerCorner's order, every frame)
        Comparison<int> cornerOrder;

        void DrawPlayerCorner()
        {
            var ids = cornerIds;
            ids.Clear();
            ids.AddRange(game.Players.Keys);
            ids.Sort(cornerOrder ?? (cornerOrder = (a, b) => a == game.MyId ? -1 : b == game.MyId ? 1 : a.CompareTo(b)));
            if (ids.Count == 0)
                return;
            // Yours in full; teammates as slim rows above it. (A spectator-less game always has you first.)
            DrawPlayerHudCard(game.Players[ids[0]], HudMyCard, ids[0] == game.MyId);
            float y = HudStackY;  // (the stack's sizes are HudLayout.cs's; HudStackTop gives the chat box its top)
            var mine = game.Me;
            if (mine != null && Game.HasPerk(mine, PerkId.RiotGear))  // status chips sit over your card (RiotGearLook.cs)
            {
                y -= HudRiotChipH + HudRiotChipGap;
                DrawRiotChip(new Rect(SideR, y, HudRiotChipW, HudRiotChipH), mine);
                y -= HudRiotChipAfter;
            }
            for (int i = 1; i < ids.Count; i++)
            {
                y -= HudTeammateH;
                DrawTeammateRow(game.Players[ids[i]], new Rect(SideR, y, SideW, HudTeammateH));
                y -= HudTeammateGap;
            }
        }

        /// A teammate as one slim row: name and class, level, and health (or how far their revive has got).
        void DrawTeammateRow(Player p, Rect r)
        {
            var role = p.Def.Color.ToColor();
            DkHud(r, p.Downed ? new Color(1f, 0.3f, 0.3f) : (Color?)null);
            Ui.Pill(new Rect(r.x + 5f, r.y + 6f, 3f, r.height - 12f), p.Downed ? new Color(0.5f, 0.5f, 0.55f) : role);
            Ui.Label(new Rect(r.x + 14f, r.y + 2f, 82f, 14f), p.Name, 11, Color.white, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(r.x + 14f, r.y + 15f, 82f, 12f), Defs.KitName(p.Kit) + " " + p.Def.Name, 8, role, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(r.xMax - 40f, r.y + 2f, 34f, 14f), "LV " + p.Level, 9, CrtStyle.Gold, TextAnchor.MiddleRight, true);
            var hp = new Rect(r.x + 98f, r.y + 17f, r.width - 106f, 7f);
            float frac = Mathf.Clamp01(p.Hp / Mathf.Max(1f, p.MaxHp));
            if (p.Downed)
            {
                Ui.Bar(hp, p.Revive, new Color(0.6f, 0.6f, 0.65f));
                Ui.Label(new Rect(hp.x, r.y + 2f, hp.width - 36f, 14f), "DOWNED", 9, new Color(1f, 0.6f, 0.6f), TextAnchor.MiddleLeft, true);
                return;
            }
            GhostBar(hp, frac, HealthColor(frac), PlayerKey(p.Id, true));
            DrawWound(hp, p);  // the part no heal can bring back (carried across waves) (HealthBars.cs)
            if (Game.Ironhide(p) > 0f)  // the Tank's Ironhide: a steel stripe along the bottom (Ironhide.cs)
                Ui.Bar(new Rect(hp.x + 1f, hp.yMax - 2f, (hp.width - 2f) * Mathf.Clamp01(Game.Ironhide(p) / Mathf.Max(1f, p.MaxHp)), 2f), 1f, CrtStyle.Steel, new Color(0f, 0f, 0f, 0f));
            if (p.ShieldHp > 0f)
                Ui.Bar(new Rect(hp.x + 1f, hp.yMax - 2f, (hp.width - 2f) * Mathf.Clamp01(p.ShieldHp / Mathf.Max(1f, p.MaxHp)), 2f), 1f, new Color(0.55f, 0.8f, 1f), new Color(0f, 0f, 0f, 0f));
            if (Game.Armored(p))  // an Armor Drop vest's armor: a steel bar just over the health bar (ArmorDrop.cs)
                Ui.Bar(new Rect(hp.x, hp.y - 3f, hp.width * Mathf.Clamp01(p.Armor / Game.ArmorCap), 2f), 1f, CrtStyle.Steel, new Color(0f, 0f, 0f, 0f));
            if (p.OverHp > 0f)  // a health shrine's health past max: a gold stripe along the top (Shrines.cs)
                Ui.Bar(new Rect(hp.x + 1f, hp.y, (hp.width - 2f) * Mathf.Clamp01(p.OverHp / Mathf.Max(1f, p.MaxHp)), 2f), 1f, CrtStyle.Gold, new Color(0f, 0f, 0f, 0f));
            Ui.Label(new Rect(hp.x, r.y + 2f, hp.width - 36f, 14f), Mathf.CeilToInt(p.Hp) + " / " + Mathf.CeilToInt(p.MaxHp), 9, Ui.Muted, TextAnchor.MiddleLeft, true);
        }

        /// Green when healthy, through yellow, to red when low.
        static Color HealthColor(float frac) =>
            frac > 0.5f ? Color.Lerp(new Color(1f, 0.85f, 0.3f), new Color(0.35f, 0.95f, 0.45f), (frac - 0.5f) * 2f)
                : Color.Lerp(new Color(1f, 0.3f, 0.3f), new Color(1f, 0.85f, 0.3f), frac * 2f);

        // --- HUD pieces -----------------------------------------------------------

        // --- The CRT console (Docs/Mockups/crt-hud-a-variants.html, A1): one blue phosphor screen under the wave plate, the five keys and your card.

        static readonly Color KeyGreen = new Color(0.42f, 1f, 0.6f);

        void DrawConsoleBack()
        {
            CrtUi.Begin(ConsoleR);
        }

        void DrawConsoleGlass()
        {
            CrtUi.End(ConsoleR, Time.unscaledTime, FxCalm);
        }

        /// Glowing text in the CRT font (the HUD's screen text).
        void HudCrt(Rect r, string text, int size, Color c, TextAnchor align = TextAnchor.MiddleLeft)
        {
            bool was = Ui.CrtFont;
            Ui.CrtFont = true;
            CrtUi.Text(r, text, size, c, align, false, FxCalm);
            Ui.CrtFont = was;
        }

        /// A digital key: a faint fill and a 2 px outline in the key's colour, or a dashed dim outline when it's empty.
        static void CrtKeyBox(Rect r, Color c, bool dashed)
        {
            if (dashed)
            {
                Ui.Fill(r, c.WithAlpha(CrtStyle.HudKeyEmptyTint));
                CrtKit.Dashed(r, 1f, c.WithAlpha(CrtStyle.HudKeyEmptyDash));
                return;
            }
            Ui.Fill(r, c.WithAlpha(CrtStyle.HudKeyTint));  // (CrtStyle.cs)
            CrtKit.Frame(r, CrtStyle.HudKeyRimW, c);
        }

        int plateWave = -1, plateMax = 1;

        /// Left of the ability keys, on the console: the wave number, how many enemies are left (with a segmented progress bar) and the score.
        void DrawWavePlate(Rect r)
        {
            if (game.Wave != plateWave)
            {
                plateWave = game.Wave;
                plateMax = 1;
            }
            plateMax = Math.Max(plateMax, game.Remaining);
            if (game.SurvivalOn && game.SurvStarted)
            {
                DrawSurvivalPlate(r);  // SurvivalUi.cs
                return;
            }
            bool bossWave = game.BossNow;
            Color ink = bossWave ? CrtStyle.Gold : CrtStyle.Ink, dim = bossWave ? new Color(CrtStyle.Gold.r * 0.7f, CrtStyle.Gold.g * 0.7f, CrtStyle.Gold.b * 0.5f) : CrtStyle.Dim;
            string head = game.Hunt != null ? (bossWave ? "HUNT  ·  BOSS" : "HUNT  ·  WAVE") : bossWave ? "BOSS FIGHT" : "WAVE";
            HudCrt(new Rect(r.x + 14f, r.y + 5f, 150f, 16f), head, 17, dim);
            string num = game.Hunt != null ? game.HuntWave.ToString() : game.ShownWave.ToString();
            HudCrt(new Rect(r.x + 12f, r.y + 14f, 120f, 46f), num, 54, ink);
            float nw = CrtUi.TitleWidth(num, 54);
            string of = game.Endless ? "ENDLESS" : game.Hunt != null ? "/ " + game.HuntLength : "/ " + game.ShownLastWave;
            HudCrt(new Rect(r.x + 18f + nw, r.y + 28f, 100f, 24f), of, 22, game.Endless ? CrtStyle.Gold : dim);
            HudCrt(new Rect(r.xMax - 110f, r.y + 5f, 100f, 16f), "LEFT", 17, dim, TextAnchor.MiddleRight);
            HudCrt(new Rect(r.xMax - 110f, r.y + 16f, 100f, 30f), game.Remaining.ToString(), 32, ink, TextAnchor.MiddleRight);
            HudCrt(new Rect(r.xMax - 130f, r.y + 44f, 120f, 14f), "SCORE " + game.Score.ToString("N0"), 17, CrtStyle.Gold, TextAnchor.MiddleRight);
            float progress = game.State == MatchState.Fighting ? 1f - (float)game.Remaining / Math.Max(1, plateMax) : 1f;
            var bar = new Rect(r.x + 14f, r.yMax - 9f, r.width - 28f, 5f);
            Ui.Fill(bar, new Color(1f, 1f, 1f, 0.1f));
            for (float sx = 0f; sx < bar.width * progress; sx += 8f)
                Ui.Fill(new Rect(bar.x + sx, bar.y, Mathf.Min(6f, bar.width * progress - sx), bar.height), ink);
        }

        bool BossUp()
        {
            foreach (var e in game.Enemies.Values)
                if (e.Def.IsBoss)
                    return true;
            return false;
        }

        /// One player's card: role-colored accent, name and class, health (with shield) and XP.
        void DrawPlayerHudCard(Player p, Rect r, bool mine)
        {
            var role = p.Def.Color.ToColor();
            if (mine)  // on the console: a full-colour island, like the lobby's party cards
            {
                Color edge = p.Downed ? new Color(1f, 0.3f, 0.3f) : role;
                Ui.Fill(r, new Color(0.03f + edge.r * 0.1f, 0.05f + edge.g * 0.1f, 0.07f + edge.b * 0.1f, 1f));
                CrtKit.Frame(r, 1f, edge.WithAlpha(0.85f));
                Ui.Fill(new Rect(r.x, r.y, 6f, r.height), p.Downed ? new Color(0.5f, 0.5f, 0.55f) : role);
            }
            else
            {
                DkHud(r, p.Downed ? new Color(1f, 0.3f, 0.3f) : (Color?)null);
                Ui.Pill(new Rect(r.x + 6f, r.y + 9f, 4f, r.height - 18f), p.Downed ? new Color(0.5f, 0.5f, 0.55f) : role);
            }
            Ui.Label(new Rect(r.x + 18f, r.y + 6f, r.width - 70f, 18f), p.Name, 13, Color.white, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(r.x + 18f, r.y + 22f, r.width - 70f, 14f), Defs.KitName(p.Kit) + " " + p.Def.Name, 10, role, TextAnchor.MiddleLeft, true);
            var lv = new Rect(r.xMax - 52f, r.y + 8f, 42f, 18f);
            Ui.Pill(lv, new Color(1f, 0.8f, 0.35f, 0.22f));
            Ui.Label(lv, "LV " + p.Level, 10, CrtStyle.Gold, TextAnchor.MiddleCenter, true);

            var hpBar = new Rect(r.x + 18f, r.y + 39f, r.width - 28f, 12f);
            float frac = Mathf.Clamp01(p.Hp / Mathf.Max(1f, p.MaxHp));
            Color hpCol = HealthColor(frac);
            if (p.Downed)
            {
                Ui.Bar(hpBar, p.Revive, new Color(0.6f, 0.6f, 0.65f));
                Ui.Label(hpBar, "DOWNED", 9, new Color(1f, 0.6f, 0.6f), TextAnchor.MiddleCenter, true);
            }
            else
            {
                GhostBar(hpBar, frac, hpCol, PlayerKey(p.Id, true));
                DrawWound(hpBar, p);  // the part no heal can bring back (carried across waves) (HealthBars.cs)
                if (Game.Ironhide(p) > 0f)  // the Tank's Ironhide: a steel stripe along the bottom (Ironhide.cs)
                    Ui.Bar(new Rect(hpBar.x + 2f, hpBar.yMax - 4f, (hpBar.width - 4f) * Mathf.Clamp01(Game.Ironhide(p) / Mathf.Max(1f, p.MaxHp)), 3f), 1f, CrtStyle.Steel, new Color(0f, 0f, 0f, 0f));
                if (p.ShieldHp > 0f)  // a shield: a blue stripe along the bottom of the health bar
                    Ui.Bar(new Rect(hpBar.x + 2f, hpBar.yMax - 4f, (hpBar.width - 4f) * Mathf.Clamp01(p.ShieldHp / Mathf.Max(1f, p.MaxHp)), 3f), 1f, new Color(0.55f, 0.8f, 1f), new Color(0f, 0f, 0f, 0f));
                if (Game.Armored(p))  // an Armor Drop vest's armor: a steel bar just over the health bar, notched every 10 (ArmorDrop.cs)
                {
                    var ar = new Rect(hpBar.x, hpBar.y - 5f, hpBar.width, 3f);
                    Ui.Bar(ar, Mathf.Clamp01(p.Armor / Game.ArmorCap), CrtStyle.Steel, new Color(0f, 0f, 0f, 0.5f));
                    for (int i = 1; i < 5; i++)
                        Ui.Bar(new Rect(ar.x + ar.width * i / 5f - 0.5f, ar.y, 1f, ar.height), 1f, new Color(0f, 0f, 0f, 0.7f), new Color(0f, 0f, 0f, 0f));
                }
                if (p.OverHp > 0f)  // a health shrine's health past max: a gold stripe along the top (Shrines.cs)
                    Ui.Bar(new Rect(hpBar.x + 2f, hpBar.y + 1f, (hpBar.width - 4f) * Mathf.Clamp01(p.OverHp / Mathf.Max(1f, p.MaxHp)), 3f), 1f, CrtStyle.Gold, new Color(0f, 0f, 0f, 0f));
                Ui.Shadowed(new Rect(hpBar.x, hpBar.y - 1f, hpBar.width, hpBar.height + 2f), Mathf.CeilToInt(p.Hp) + " / " + Mathf.CeilToInt(p.MaxHp), 10, Color.white);
            }
            Ui.Bar(new Rect(r.x + 18f, r.y + 55f, r.width - 28f, 4f), p.Xp / Defs.XpToNext(p.Level), new Color(1f, 0.82f, 0.3f), new Color(0f, 0f, 0f, 0.4f));
        }

        /// GET READY / WAVE N CLEARED as the scan band (WaveBand.cs; at BannerRect, HudLayout.cs): log lines each side, the title, the sub line and a bar that fills as the countdown runs out.
        void DrawBanner(string title, string sub, Color accent)
        {
            float now = Time.unscaledTime;
            if (now - bandLastDraw > 0.25f)
                bandShownAt = now;  // not shown last frame: opening again
            bandLastDraw = now;
            float tm = game.StateTimer;
            if (tm > bandMax || game.Wave != bandWave)
            {
                bandMax = Mathf.Max(0.01f, tm);
                bandWave = game.Wave;
            }
            bool boss = accent.r > accent.b;  // (gold, for a boss wave)
            int n = Mathf.Max(0, Mathf.CeilToInt(tm));
            string left = "SYS> ARENA LINK OK\nSYS> SQUAD ALIVE";
            string right = (game.Wave == 0 ? "T-MINUS " + n.ToString("00") + "\n" : "") + "WAVE " + (game.Wave + 1).ToString("00") + (boss ? " / BOSS" : "");
            DrawScanBand(title, sub, left, right, 1f - tm / bandMax, boss ? CrtStyle.Gold : CrtStyle.Ink, (now - bandShownAt) / 0.25f, false);
        }

        /// The corner's TAB / H hint shows through this many waves, and until the Tab screen has been opened this many times.
        const int TabHintWaves = 3, TabHintOpens = 2;
        float tabHintA = 1f;  // its fade
        int tabOpens;         // Tab screen opens this session
        int tabHintFrame = -1;

        readonly float[] tileEmptyK = new float[Upgrades.SlotCount];  // each key's ease toward its stub (HudLayout.cs HudTile)
        int tileEmptyFrame = -1;

        /// XP bar plus the ability tiles (1st, 2nd, 3rd, 4th, Ultimate) along the bottom.
        void DrawAbilityBar(Player me)
        {
            // XP strip (the CRT console look): "LV 3 · 48%" in gold on its own dark plate at the left end, then the segmented gold bar.
            var xp = HudXpStrip;  // (the strip and the keys: HudLayout.cs)
            float xpFrac = Mathf.Clamp01(me.Xp / Defs.XpToNext(me.Level));
            Color gold = new Color(1f, 0.82f, 0.3f, 0.95f);
            Ui.Fill(new Rect(xp.x - 1f, xp.y - 1f, xp.width + 2f, xp.height + 2f), CrtStyle.Ink.WithAlpha(0.3f));
            Ui.Fill(xp, new Color(0.01f, 0.03f, 0.08f, 0.88f));
            string lvText = "LV " + me.Level + "  ·  " + Mathf.RoundToInt(xpFrac * 100f) + "%";
            float plateW = Ui.TextWidth(lvText, 10, true) + 12f;
            var plate = new Rect(xp.x, xp.y, plateW, xp.height);
            Ui.Fill(plate, new Color(0.02f, 0.03f, 0.07f, 1f));
            Ui.Fill(new Rect(plate.xMax, xp.y, 1f, xp.height), gold.WithAlpha(0.45f));
            Ui.Label(plate, lvText, 10, gold, TextAnchor.MiddleCenter, true);
            float barX = plate.xMax + 3f, barW = xp.xMax - barX;
            for (float sx = 0f; sx < barW * xpFrac; sx += 8f)
                Ui.Fill(new Rect(barX + sx, xp.y, Mathf.Min(6f, barW * xpFrac - sx), xp.height), gold);

            if (tileEmptyFrame != Time.frameCount)  // (once a frame: OnGUI runs per event)
            {
                float step = Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime / 0.35f : 1f;  // (no frame time, the preview: straight there)
                for (int i = 0; i < Upgrades.SlotCount; i++)
                    tileEmptyK[i] = Mathf.MoveTowards(tileEmptyK[i], me.Slots[i].Id == AbilityId.None ? 1f : 0f, tileEmptyFrame < 0 ? 1f : step);
                tileEmptyFrame = Time.frameCount;
            }
            for (int i = 0; i < Upgrades.SlotCount; i++)
            {
                var r = HudTile(i, tileEmptyK);
                var look = TileLookFor(i, i == Upgrades.UltSlot);  // how the tile reacts this frame (HudTiles.cs)
                int slotIndex = i;
                TileTransformed(r.center, look.Scale, look.Dx, () => DrawAbilityTile(me, slotIndex, r, look));
            }
            DrawMasteryToasts(HudMasteryToastAt.X, HudMasteryToastAt.Y);
        }

        /// One ability tile: key, name, READY or the timer, mastery and recharge lines, charge pips; `look` adds the
        /// tile's reactions (rim flashes, READY sliding in, a pip popping).
        void DrawAbilityTile(Player me, int i, Rect r, TileLook look)
        {
            bool was = Ui.CrtFont;
            Ui.CrtFont = true;  // the CRT screen font for the whole key
            DrawAbilityTileBody(me, i, r, look);
            Ui.CrtFont = was;
        }

        /// An inverse-video key cap: the key's name, dark on a block of the key's colour.
        void DrawCrtCap(Rect at, string key, Color c)
        {
            float w = 12f + key.Length * 7f;
            var cap = new Rect(at.x, at.y, w, 16f);
            Ui.Fill(cap, c);
            bool was = Ui.CrtFont;
            Ui.CrtFont = true;
            Ui.Label(cap, key, 15, new Color(0.01f, 0.03f, 0.08f), TextAnchor.MiddleCenter, false);
            Ui.CrtFont = was;
        }

        void DrawAbilityTileBody(Player me, int i, Rect r, TileLook look)
        {
            var slot = me.Slots[i];
            bool ultimate = i == Upgrades.UltSlot;
            if (slot.Id == AbilityId.None)
            {
                // A stub: the key's cap centred in a dashed box, a dim + under it (very rare upgrades fill these keys).
                CrtKeyBox(r, CrtStyle.Dim, true);
                float cw = 12f + SlotKeys[i].Length * 7f;
                DrawCrtCap(new Rect(r.center.x - cw / 2f, r.y + 10f, 0f, 16f), SlotKeys[i], CrtStyle.Dim);
                Ui.Label(new Rect(r.x, r.y + 28f, r.width, 22f), "+", 22, CrtStyle.Dim.WithAlpha(0.7f), TextAnchor.MiddleCenter, false);
                return;
            }
            bool ready = slot.Charges > 0;
            float cd = game.Cooldown(me, i);
            float fill = slot.Charges >= slot.MaxCharges ? 1f : 1f - slot.Timer / Mathf.Max(0.01f, cd);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
            Color keyCol = ultimate ? CrtStyle.Gold : ready ? KeyGreen : CrtStyle.Ink;  // gold ultimate, green when ready, blue while it recharges
            CrtKeyBox(r, keyCol.WithAlpha(ready ? (ultimate ? 0.65f + 0.35f * pulse : 1f) : 0.7f), false);
            if (look.White > 0f)
                CrtKit.Frame(r, 2f, new Color(1f, 1f, 1f, 0.9f * look.White));
            if (look.Graze > 0f)  // an enemy shot skimmed past: the ultimate's rim flashes pale blue (Core/Graze.cs)
                CrtKit.Frame(r, 2f, new Color(0.6f, 0.92f, 1f, 0.85f * look.Graze));

            // Cooldown: a dark shade over the tile that drains downward as it recharges.
            if (!ready)
            {
                GUI.BeginGroup(new Rect(r.x, r.y, r.width, r.height * (1f - fill)));
                Ui.Fill(new Rect(0f, 0f, r.width, r.height), new Color(0f, 0f, 0f, 0.35f));
                GUI.EndGroup();
            }
            DrawCrtCap(new Rect(r.x + 8f, r.y + 7f, 0f, 16f), ultimate ? "ULT · " + SlotKeys[4] : SlotKeys[i], keyCol);
            // Name: bright when ready, dimmer while recharging.
            Ui.Label(new Rect(r.x + 8f, r.y + 22f, r.width - 16f, 22f), Upgrades.Ability(slot.Id).Name, Upgrades.Ability(slot.Id).Name.Length > 12 ? 18 : 22, ready ? keyCol : new Color(0.62f, 0.7f, 0.9f), TextAnchor.MiddleLeft, false);
            if (slot.Id == AbilityId.WaterWard)  // the drops circling you, out of 10 (WaterWard.cs)
            {
                bool full = Game.WwFull(me);
                DkTitle(new Rect(r.x + 64f, r.y + 4f, r.width - 120f, 20f), full ? "FULL" : me.WwOff.Count + "/" + Game.WwMax, 15,
                    full ? new Color(0.95f, 0.78f, 0.42f) : new Color(0.45f, 0.86f, 0.95f), TextAnchor.MiddleLeft);
            }
            if (slot.Id == AbilityId.SenbonSeekers)  // the needles held, out of 9 (12 mastered; SenbonSeekers.cs)
            {
                bool full = Game.SbFull(me);
                DkTitle(new Rect(r.x + 64f, r.y + 4f, r.width - 120f, 20f), full ? "FULL" : me.SbN + "/" + Game.SbMaxOf(me), 15,
                    full ? new Color(0.95f, 0.78f, 0.42f) : new Color(0.56f, 0.82f, 0.31f), TextAnchor.MiddleLeft);
            }
            if (ready)
            {
                Color lo = ultimate ? CrtStyle.Gold : Ui.Good;
                float rin = look.ReadyIn;  // sliding in from the right as the tile pops
                if (rin > 0f)
                    DkTitle(new Rect(r.x + 8f + 14f * (1f - rin), r.y + 5f, r.width - 16f, 20f), ultimate ? "READY!" : "READY", 15, lo.ScaleAlpha(rin), TextAnchor.MiddleRight);
            }
            else if (slot.Id == AbilityId.LastStand && me.StandOn)  // planted: press it again to come down (its cooldown starts then)
                DkTitle(new Rect(r.x + 8f, r.y + 4f, r.width - 16f, 20f), "PRESS TO CANCEL", 15, new Color(0.55f, 0.78f, 1f), TextAnchor.MiddleRight);
            else if (slot.Id == AbilityId.BloodWall && me.BloodWallOn)  // the wall is up (press it again to stop; its cooldown starts then; BloodWall.cs)
                DkTitle(new Rect(r.x + 8f, r.y + 4f, r.width - 16f, 20f), "ON", 19, new Color(1f, 0.33f, 0.41f), TextAnchor.MiddleRight);
            else if (slot.Id == AbilityId.StunGun && me.StunShots > 0)  // the gun is out: its cooldown waits for the last shot
                DkTitle(new Rect(r.x + 8f, r.y + 4f, r.width - 16f, 20f), me.StunShots + (me.StunShots == 1 ? " SHOT" : " SHOTS"), 16, new Color(0.45f, 0.88f, 1f), TextAnchor.MiddleRight);
            else
            {
                // Pressed too early: the timer flashes red.
                float red = look.Red;
                DkTitle(new Rect(r.x + 8f, r.y + 4f, r.width - 16f, 20f), ultimate ? Mathf.FloorToInt(fill * 100f) + "%" : slot.Timer.ToString(slot.Timer < 10f ? "0.0" : "0") + "s", 19, Color.Lerp(new Color(0.7f, 0.8f, 1f), new Color(1f, 0.3f, 0.3f), red), TextAnchor.MiddleRight);
            }
            // Along the bottom: the mastery XP line (green, full once mastered) sitting right on top of the
            // recharge line, the pair one strip.
            float mxp;
            me.MasteryXp.TryGetValue(slot.Id, out mxp);
            var strip = new Rect(r.x + 8f, r.yMax - 13f, r.width - 16f, 7f);
            const float xpH = 3f;
            Ui.Fill(strip, new Color(1f, 1f, 1f, 0.1f));
            Ui.Fill(new Rect(strip.x, strip.y, strip.width * Game.MasteryProgress(mxp), xpH), new Color(0.35f, 0.9f, 0.45f));
            Ui.Fill(new Rect(strip.x, strip.y + xpH, strip.width * Mathf.Clamp01(fill), strip.height - xpH), keyCol);
            if (slot.MaxCharges > 1)
                for (int k = 0; k < slot.MaxCharges; k++)
                {
                    var pip = new Rect(r.xMax - 12f - k * 9f, r.y + 30f, 6f, 6f);
                    if (k == look.Pip)
                    {
                        // The charge that just came back: a fading halo spreading round it, and the pip popping.
                        float h = 3f + 6f * look.PipHalo;
                        Ui.Pill(new Rect(pip.x - h, pip.y - h, pip.width + 2f * h, pip.height + 2f * h), new Color(1f, 1f, 1f, 0.35f * (1f - look.PipHalo)));
                        float e = 3f * look.PipPop;
                        pip = new Rect(pip.x - e, pip.y - e, pip.width + 2f * e, pip.height + 2f * e);
                    }
                    Ui.Fill(pip, k < slot.Charges ? keyCol : new Color(1f, 1f, 1f, 0.15f));
                }
            if (look.Red > 0f)
                Ui.HudRim(r, new Color(1f, 0.35f, 0.35f, 0.85f * look.Red));
        }


        /// Five small bars for an ability's mastery level; the next one fills as it earns XP.
        void DrawMasteryPips(Rect r, int level, float progress)
        {
            bool max = level >= Game.MasteryMax;
            Color on = max ? CrtStyle.Gold : new Color(0.6f, 0.85f, 1f);
            for (int k = 0; k < Game.MasteryMax; k++)
            {
                var pip = new Rect(r.x + k * 13f, r.y, 10f, r.height);
                Ui.Fill(pip, new Color(1f, 1f, 1f, 0.22f));
                if (k < level)
                    Ui.Fill(pip, on);
                else if (k == level && progress > 0f)
                    Ui.Fill(new Rect(pip.x, pip.y, pip.width * progress, pip.height), on.WithAlpha(0.55f));
            }
        }

        /// "Barrier · Mastery 3" when one of your abilities levels up.
        void DrawMasteryToasts(float cx, float y)
        {
            var toasts = game.MasteryToasts;
            if (Event.current.type == EventType.Repaint)  // (OnGUI runs once per event: tick once a frame, or toasts ran out early)
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                toasts[i].T -= Time.unscaledDeltaTime;
                if (toasts[i].T <= 0f)
                    toasts.RemoveAt(i);
            }
            for (int i = 0; i < toasts.Count && i < 3; i++)
            {
                var t = toasts[toasts.Count - 1 - i];
                bool max = t.Level >= Game.MasteryMax;
                float a = Mathf.Clamp01(t.T / 0.5f);
                string text = Upgrades.Ability(t.Ability).Name + (max ? "  MASTERED: " + Game.Capstone(t.Ability) : "  ·  Mastery " + t.Level);
                var c = max ? CrtStyle.Gold : new Color(0.6f, 0.85f, 1f);
                Ui.Shadowed(new Rect(cx - 400f, y - i * 22f, 800f, 20f), text, max ? 15 : 14, c.WithAlpha(a), TextAnchor.MiddleCenter);
            }
        }

        /// A small key label like [SPACE]; r.width is ignored (it's sized to the text).
        void DrawKeyCap(Rect r, string key, Color color)
        {
            float w = 12f + key.Length * 6.5f;
            var cap = new Rect(r.x, r.y, w, r.height);
            Ui.Pill(cap, color.WithAlpha(0.2f * color.a + 0.05f));
            DkTitle(cap, key, 9, new Color((color.r + 1f) / 2f, (color.g + 1f) / 2f, (color.b + 1f) / 2f, 0.5f + 0.5f * color.a), TextAnchor.MiddleCenter);
        }
    }
}
