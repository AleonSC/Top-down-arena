using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Arena
{
    /// The arena in GL immediate mode: the frame (OnPostRender, DrawWorld, DrawArenaContents), player auras, the
    /// parry shield, orbs and links, the aim line, and the GL primitives everything is built from. The rest is split
    /// by what's drawn: WorldShots.cs, WorldEffects.cs, WorldEnemies.cs (with minions and zones), WorldPlayers.cs.
    public partial class ArenaApp
    {
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
            bool world = game.Running && session.Screen == AppScreen.Game;  // (Practice runs behind the lobby, drawn in its window: HangoutUi.cs)
            drawScale = world ? scale * WorldToUi : scale;
            if (world)
                DrawWorld();
            else if (LobbyCrtLeaving)
                DrawCrtLeaveBackdrop(Time.time);  // squashed with the screen, the lobby's then the menu's (LobbyCrtLeave.cs)
            else
                DrawMenuBackground(Time.time);
            GL.End();
            GL.PopMatrix();
        }

        void DrawWorld()
        {
            UpdateJuice();  // visual effects catch up with this frame (Juice.cs)
            UpdateDownedLooks();  // who went down or stood back up (DownedLooks.cs)
            if (ReplayHeldFrame())
                return;  // hit-pause: last frame again
            BeginJuiceFrame();
            float t = game.AnimT;
            // Behind the arena: the background this player picked in the lobby, dimmed so the fight stays easy to read.
            float worldScale = drawScale;
            drawScale = worldScale / WorldToUi;  // the backgrounds draw in UI canvas coordinates
            float keepSx = shakeX, keepSy = shakeY, keepPunch = punchK;
            shakeX = shakeY = punchK = 0f;  // the backdrop stays put: shakes and a charge camera's zoom out would bare the screen's edges
            DrawMenuBackground(Time.time);
            shakeX = keepSx; shakeY = keepSy; punchK = keepPunch;
            drawScale = worldScale;
            BeginEndingWorld();  // a run ending: the camera eased in on the blow, the colour draining (RunEnding.cs)
            BeginLeanFrame();    // the camera lean: the arena slides, the backdrop doesn't (CameraLean.cs)
            FillArenaBackdrop(new Rgba(0.05f, 0.06f, 0.09f, ArenaBgDim));
            bool infected = ZombieArena;  // Zombie Infection: the arena goes dark green (ZombieLook.cs)
            if (infected)
                FillRect(0, 0, AW, AH, ZombieFloor);
            DrawPathArena(t);  // a divergent path's floor (PathsUi.cs)
            DrawRoomFloor(t);  // a challenge room's ground (ChallengeLook.cs)
            WallInk();  // the coming boss's colour for the floor, walls and crates (LayoutLook.cs)
            var grid = infected ? ZombieGrid : new Rgba(1f, 1f, 1f).Mix(Ink, 0.75f).WithAlpha(0.035f + FloorTintAmt * 0.5f);
            if (game.RoomsOn)
                DrawRoomsFloor(grid);  // the side rooms and hallways: the grid across the whole world (RoomsLook.cs)
            else  // the Pressure grid: bends round the players, ripples from blasts (FloorGridLook.cs)
                DrawFloorGrid(V2.Zero, new V2(AW, AH), V2.Zero, grid);
            var border = infected ? ZombieBorder : new Rgba(0.3f, 0.35f, 0.5f, 0.6f);
            if (!game.RoomsOn)  // (the rooms' walls stand where the border was)
            {
                Line(new V2(2, 2), new V2(AW - 2, 2), 3f, border);
                Line(new V2(2, AH - 2), new V2(AW - 2, AH - 2), 3f, border);
                Line(new V2(2, 2), new V2(2, AH - 2), 3f, border);
                Line(new V2(AW - 2, 2), new V2(AW - 2, AH - 2), 3f, border);
            }
            DrawMapFold();  // the Campaign's map reset: the old rooms fold into the hub (MapResetLook.cs)

            DrawJuiceFloor();  // scorch marks and shot trails (Juice.cs)
            DrawFrostNovaGround();  // Frost Nova's cracks and frosted ground, under everything (FrostNovaLook.cs)
            DrawLighting(t);  // shots, blasts and fire lighting the floor, their shadows cast (LightingLook.cs)
            DrawHeroDummies(t);  // the Dev Arena's stand-in heroes (DevArenaUi.cs)
            DrawArenaContents(t, true);
            DrawJuiceParts();  // shards, rings and sparks
            DrawArenaOver(t);  // what stands or flies over the arena's contents (also in Practice and the loadout preview)
            DrawEngineerMock(t);  // Tools/Preview only: the Engineer mockup (EngineerLook.cs)
            GlowPass();        // the additive glow
            DrawCellDoorsOver(t);  // the Turnkey's Cell Doors, cut into the walls (CellDoorLook.cs)
            DrawEnemyShots(t);  // enemy bullets on top of every effect, spark and glow, so a busy fight never hides what you must dodge
            DrawEndingWorld();  // a run ending: the victory's rays and sparks (RunEnding.cs)
            DrawArenaRules(t);  // Narrow Pass's walls and Fogbound's mist (PathsUi.cs)
            bool zoomed = camZoomOn;
            camZoomOn = false;  // the screen overlays below fill the screen, unzoomed (CameraZoom.cs)
            DrawMood(AW, AH);  // tint, red corners when hurt or low

            EndEndingWorld();  // the camera and colours back; the ending's vignette and flash (RunEnding.cs)
            if (game.State == MatchState.GameOver || game.State == MatchState.Victory)
                FillRect(0, 0, AW, AH, new Rgba(0f, 0f, 0f, 0.45f * EndBg));
            camZoomOn = zoomed;

            DrawPlayerOverlays(t, MouseArena(), aimLine);  // the aim line, and each weapon's pips, meters and charges

            if (aimDebug && game.Me != null)
            {
                // Cyan: from you through the crosshair. Yellow: along your last shot as the host fired it.
                var me = game.Me;
                Line(me.Disp, me.Disp + V2.FromAngle(me.Aim) * 1100f, 1.5f, new Rgba(0.3f, 0.9f, 1f, 0.7f));
                if (game.LastShotId != 0)
                {
                    Line(game.LastShotFrom, game.LastShotFrom + V2.FromAngle(game.LastShotAngle) * 1100f, 1.5f, new Rgba(1f, 0.85f, 0.2f, 0.75f));
                    Bullet shot;
                    if (game.Bullets.TryGetValue(game.LastShotId, out shot))
                        Arc(shot.Pos, shot.Radius + 5f, 1.5f, new Rgba(1f, 0.85f, 0.2f, 0.9f), 12);
                }
            }

            // Crosshair
            V2 m = MouseArena();
            StepCursor(m, t, Keybinds.Held(KeyAction.Fire) && !fireBlocked);
            if (!cursorWantPointer)  // (over a menu or the end screen the pointer is drawn on top of the UI instead: DrawMenuCursor)
                DrawLookMorph(CursorLook(), m, t, cursorFire, cursorMorph, cursorHist);  // CursorLooks.cs: the pointer turning into your crosshair as a run starts
            EndJuiceFrame();
            EndRecording();
        }

        /// Over the arena's contents: your aim line toward `cursor` (when `aim`), and each weapon's pips, meters, charges
        /// and lasers. Shared by the run (DrawWorld), Practice (HangoutUi.cs) and the loadout preview (LobbyUi.cs).
        void DrawPlayerOverlays(float t, V2 cursor, bool aim)
        {
            if (aim && game.Me != null && !game.Me.Downed && !game.Me.ParryActive && !LaunchHides(game.Me) && !(game.Me.Weapon.Id == WeaponId.MendersBow && game.Me.DrawT > 0f))  // (a drawn bow has its own: BowFocusLook.cs)
                DrawAimLine(game.Me, cursor, t);
            if (game.Me != null && !game.Me.Downed && game.Me.Weapon.Id == WeaponId.Revolver && game.Me.StunShots == 0)
                DrawRevolverPips(game.Me, t);
            foreach (var p in game.Players.Values)
                if (p.Weapon.Id == WeaponId.TwinPistols)
                    DrawTwinLasers(p, t);  // Laser Sights' beams (TwinPistolsLook.cs)
            if (game.Me != null && !game.Me.Downed && game.Me.Weapon.Id == WeaponId.TwinPistols)
                DrawTwinLaserReady(game.Me);  // its key over your head
            foreach (var p in game.Players.Values)
                if (p.Weapon.Id == WeaponId.Rapier)
                {
                    DrawRapierPips(p);  // MeleeLooks.cs
                    DrawRapierStance(p);
                }
            foreach (var p in game.Players.Values)
                if (p.Weapon.Id == WeaponId.Quarterstaff)
                    DrawStaffPips(p);  // QuarterstaffLook.cs
            foreach (var p in game.Players.Values)
                if (p.Weapon.Id == WeaponId.TwinRifle)
                    DrawTwinNadePips(p);  // DpsGunLooks.cs
            foreach (var p in game.Players.Values)
                if (p.Weapon.Id == WeaponId.TwinRifleAuto)
                    DrawTwinAutoLock(p, t);  // TwinAutoLook.cs
            foreach (var p in game.Players.Values)
                if (p.Weapon.Id == WeaponId.OutOfAmmo)
                    DrawAmmoSmears(p, t);  // OutOfAmmoLook.cs
            foreach (var p in game.Players.Values)
                if (p.Weapon.Id == WeaponId.Scattergun || p.Weapon.Id == WeaponId.FlakCannon || p.Weapon.Id == WeaponId.RiotRepeater || p.Weapon.Id == WeaponId.BoneGuard || p.Weapon.Id == WeaponId.Nimbus)
                    DrawTankAltPips(p);  // TankAltLook.cs
            foreach (var p in game.Players.Values)
                if (p.Weapon.Id == WeaponId.GrenadeLauncher)
                    DrawGlPips(p);  // GrenadeLauncherLook.cs
            foreach (var p in game.Players.Values)
                if (p.Weapon.Id == WeaponId.WolfWhistle)
                    DrawPackPips(p);  // WolfLook.cs
            foreach (var p in game.Players.Values)
                if (p.Weapon.Id == WeaponId.HoneybeeStaff)
                    DrawHoneyMeter(p);  // HoneybeeLook.cs
            foreach (var p in game.Players.Values)
                if (p.Weapon.Id == WeaponId.WyvernStaff)
                    DrawCinderMeter(p);  // WyvernStaffLook.cs
            foreach (var p in game.Players.Values)
                if (p.Weapon.Id == WeaponId.Railgun)
                {
                    DrawRailAim(p);   // RailBeamLook.cs: your charge's beam and bounces
                    DrawRailHeat(p);  // the heat gauge
                }
            foreach (var p in game.Players.Values)
                if (p.Weapon.Id == WeaponId.RepulsionCannon)
                {
                    DrawRepelPull(p);  // RepulseLook.cs: the Undertow's current and meter
                    DrawRepelMeter(p);
                }
            foreach (var p in game.Players.Values)
                if (p.Weapon.Id == WeaponId.Greatsword)
                {
                    DrawGreatWindup(p);     // each step's own pose
                    DrawGreatWheel(p);      // the Moonwheel
                    DrawGreatPips(p);
                }
            foreach (var p in game.Players.Values)
                if (p.Weapon.Id == WeaponId.Kusarigama)
                    DrawKusarigama(p);  // KusarigamaLook.cs
            DrawFumaWorld(t);  // the Fuma Shuriken's Orbit stance (FumaLook.cs)
            foreach (var p in game.Players.Values)
                if (p.Weapon.Id == WeaponId.MendersBow && p.DrawT > 0f && !p.Downed)
                    DrawBowDraw(p, p.Id == game.MyId, t);
                else if (p.Weapon.Id == WeaponId.Warhammer && p.DrawT > 0f && !p.Downed)
                    DrawHammerCharge(p, p.Id == game.MyId, t);
                else if (p.Weapon.Id == WeaponId.GolemStaff && p.DrawT > 0f && !p.Downed)
                    DrawBoulderCharge(p, p.Id == game.MyId, t);
                else if (p.Weapon.Id == WeaponId.ArcLightning && p.DrawT > 0f && !p.Downed)
                    DrawArcLockFlatCharge(p, p.Id == game.MyId, t);  // (Visual effects off; on, the glow pass has it: ArcLockLook.cs)
                else if (p.Weapon.Id == WeaponId.GoreMortar && p.DrawT > 0f && !p.Downed)
                    DrawClotCharge(p, p.Id == game.MyId, t);  // GoreMortarLook.cs
            DrawBowFocusUi(t);  // your Mender's Bow: the Focus bar and Scatter's cooldown (BowFocusLook.cs)
        }

        /// What stands or flies over the arena's contents, after DrawArenaContents: the run, the Practice window (HangoutUi.cs)
        /// and the lobby's loadout preview all draw it, or these never show there.
        void DrawArenaOver(float t)
        {
            DrawHelis(t);      // the Attack Helicopter, flying over the arena's contents (AttackHelicopterLook.cs)
            DrawDeployTurrets(t);  // the DPS Soldier's Deployable Turret and its rounds (DeployTurretLook.cs)
            DrawLandSharks(t);  // the DPS Summoner's Land Shark mid-leap (LandSharkLook.cs)
            DrawWarhornSky();  // Warhorn Volley's horn and the arrows in the air (WarhornVolleyLook.cs)
            DrawOrbitalSky();  // Orbital Laser's lock-on, beam, shockwave and flash (OrbitalLaserLook.cs)
            DrawDragonsDogma();  // Dragon's Dogma, the Tank Ninja's spirit dragon (DragonsDogmaLook.cs)
        }

        /// Everything on the arena floor: players, bullets, enemies, minions, zones and effects. Also draws the
        /// lobby's loadout preview (a Showcase game swapped in as `game`).
        /// enemyShotsLater: the run leaves enemy bullets out here and draws them over the effects and the glow
        /// (DrawEnemyShots); the lobby preview draws everything here.
        void DrawArenaContents(float t, bool enemyShotsLater = false)
        {
            DrawRankTrails(t);  // a rank reward drawn under everyone (RankTrails.cs)
            foreach (var p in game.Players.Values)
                DrawPlayerAuras(p, t);
            DrawStoneCracks();  // the Stonespear's slams (StonespearLook.cs)
            foreach (var b in game.Bullets.Values)
                if (b.Kind < Defs.SmallShot)
                    DrawBigShot(b, t);
            DrawPathPatches(t);  // a hazard path's fire and pools (PathsUi.cs)
            DrawWardenYard(t);   // the Warden's searchlight, gates and doors (WardenLook.cs)
            DrawHoldingCells(t);  // the Warden's Holding Cells and their padlocks (HoldingCellLook.cs)
            DrawPlayerSearchlights(t);
            DrawQueenFloor(t);   // the Hive Queen's wax cells, dive shadow and spray (QueenLook.cs)
            DrawReaperFloor(t);  // the Reapers' holes, lasers and reap marks (ReaperLook.cs)
            DrawHeraldFloor(t);  // the Storm Herald's rain, wind and dark sky (HeraldLook.cs)
            DrawDuelFloor(t);    // the Duelist's circle, warning lines and hanging blades (DuelistLook.cs)
            DrawColossusFloor(t);  // the Colossus's rings, shadows, fissures, plates and pillars (ColossusLook.cs)
            DrawQuarryFloor(t);    // Stompers' rings (QuarryLook.cs)
            DrawLarderFloor(t);    // the Butcher's chop, the Gulper's cone (LarderLook.cs)
            DrawGuardFloor(t);     // the Guard's swing arc (WardenLook.cs)
            DrawThugFloor(t);      // the Thug's punch wedge (PrisonLook.cs)
            DrawEclipseFloor(t);   // the Eclipse's shadow, sunspots, flares, rings and echoes (EclipseLook.cs)
            DrawVarietyFloor(t);   // the factions' beams and lines, aim lanes (VarietyLook.cs)
            DrawDevourerFloor(t);  // the Devourer's cones, lunge lines, ripple, jaws and teeth (DevourerLook.cs)
            DrawStageShiftFloor(t);  // a boss changing phase: sirens or wax, and the arena pulsing (StageShiftLook.cs)
            DrawLaunchWorld(t);      // a run launching: the ripple's ring, the players' pillars of light (LaunchIntro.cs)
            DrawRecallWorld(t);      // back to the lobby: everyone beamed out (Recall.cs)
            DrawChallengeWorld(t);   // a challenge room's portal, crystal, zones and altar (ChallengeLook.cs)
            DrawBossGate(t);         // the boss gate in the hub before a boss (BossGateLook.cs)
            DrawTombFloor(t);        // the Glacial Tomb's frost lattice and mandala (GlacialTombLook.cs)
            DrawBloodFreezeFloor(t); // Blood Freeze's frost, cracks, rune circle and wave (BloodFreezeLook.cs)
            DrawBambooGroveFloor(t); // Bamboo Grove's wash, cracks, front and shadows (BambooGroveLook.cs)
            DrawFlashFireFloor(t);   // Flash Fire's kindle, rolling ring, scorched band and embers (FlashFireLook.cs)
            DrawShockwaveFloor();    // the Shockwave ability's scorch (ShockwaveLook.cs)
            DrawTitanFloor();        // the Titan golem's slam cracks and rubble (TitanLook.cs)
            DrawSpiritFloor();       // Spirit Avatar's ink strokes and stains (SpiritAvatarLook.cs)
            DrawHammerFloor();       // the Warhammer slam's crater and cracks, under the enemies (MeleeLooks.cs)
            DrawSprayStains(t);      // Arterial Spray's spatter (ArterialSprayLook.cs)
            DrawClotStains(t);       // the Gore Mortar's drops on the floor (GoreMortarLook.cs)
            foreach (var z in game.Zones.Values)
                DrawZone(z, t);
            DrawInk(t);  // Spirit Chalk's lines and loops (ChalkLook.cs)
            foreach (var h in game.Hazards.Values)
                DrawHazard(h, t);
            DrawPits(t);  // a pit variant's voids, gold-and-black rims (PitLook.cs)
            DrawRoomFogVeil(t);  // unseen rooms under a dark veil, under their walls (RoomFogLook.cs)
            DrawLayout(t);  // the floor plan's walls, pillars and edge gates (LayoutLook.cs)
            DrawWallTouch(t);  // a wall crackling where a player touches it (WallTouchLook.cs)
            DrawWallPortals(t);  // the Expanding arena's wall portals, over their walls (RoomsLook.cs)
            DrawGates();     // the Expanding arena's shutter gates: a red laser grid when shut (GateLook.cs)
            DrawMysteryDoors();  // sealed mystery doors (MysteryDoorLook.cs)
            DrawHospitals(t);  // Field Hospitals' laser gates, and the pick's lit doors (FieldHospitalLook.cs)
            DrawBreaches();  // the Expanding arena's breaches: a red hologram barrier in each (BreachLook.cs)
            DrawTourFlash();  // the breach warning's flash on the breach the camera is on (BreachTour.cs)
            DrawProps(t);  // crates, barrels and the chest (PropsLook.cs)
            DrawTurrets();  // the Black Market's turrets (TurretLook.cs)
            DrawRoomTraps(t);  // a room's spike plates, slow fields and turrets (RoomTrapLook.cs)
            DrawRelicGauntlets(t);  // the Relic Gauntlet's lasers (RelicGauntletLook.cs)
            DrawKnightDpsUnder(t);  // the Gauntlet's rings and dust, the Drawbridge's slam (KnightDpsLook.cs)
            DrawAirTrails();  // the air behind fast movers, under every body (AirTrailLooks.cs)
            foreach (var p in game.Players.Values)
                DrawPlayerLinks(p, t);
            foreach (var p in game.Players.Values)
                DrawPactTimer(p, t);  // ForcePactLook.cs
            foreach (var minion in game.Minions.Values)
                DrawMinion(minion, t);
            SweepCloneWalls();  // CloneWallLook.cs
            SweepGolemWalls(t);  // GolemWallLook.cs
            DrawFrenzyMinions(t);  // Alpha Call's frenzied minions (AbilityLooks.cs)
            foreach (var o in game.Orbs.Values)
                if (game.MinePickup(o.Owner) && !game.FogHides(o.Pos))  // (teammates' copies of a drop aren't shown: Core/Coins.cs)
                    DrawOrb(o, t);
            foreach (var c in game.Coins.Values)
            {
                if (!game.MinePickup(c.Owner) || game.FogHidesStill(c.Pos))  // (in an unseen room: hidden, RoomFog.cs)
                    continue;
                if (c.Vec)
                    DrawVectorPickup(c, t);  // VectorPickupLook.cs
                else
                    DrawCoin(c, t);  // CoinLook.cs
            }
            DrawVectorBursts(t);  // a Stash's cache or a vector pickup flying into whoever took it (VectorGainLook.cs)
            DrawGadgets(t);  // the Magnet on the floor or thrown (GadgetLook.cs)
            DrawDevSpawners(t);  // the F1 dock's enemy spawners (DevSpawnerLook.cs)
            DrawHangoutSpawners(t);  // the Hangout's spawners round the edges (HangoutSpawnerLook.cs)
            DrawReaperTether(t);
            DrawSingularityHazards(t);
            DrawHeraldHazards(t);  // rods, arcs, the Conductor and the spokes (HeraldLook.cs)
            DrawEclipseMoon(t);    // EclipseLook.cs
            DrawLichScene(t);  // LichLook.cs
            PruneDarts();  // Poison Dart's stuck darts (PoisonDartLook.cs)
            DrawBambooGroveBack(t);  // Bamboo Grove's far stalks, behind the enemies (BambooGroveLook.cs)
            DrawFlashFireBack(t);  // Flash Fire's far flames, behind the enemies (FlashFireLook.cs)
            segTol = CrowdTol();
            foreach (var e in game.Enemies.Values)
                if (!Game.DevourUnder(e) && !Game.GutwormUnder(e) && !(game.DrillTargetOf(e, out int drillOwner) && drillOwner != game.MyId) && !(e.Dormant ? game.FogHidesStill(e.Pos) : game.FogHides(e.Pos)))  // (room fog: hidden in an unseen room unless a player is close, RoomFog.cs); the Devourer or a Gutworm underground is just its ripple (DevourerLook.cs, LarderLook.cs)
                    DrawEnemy(e, t);
            segTol = SegTol;
            DrawDormantMarks(t);  // sleeping groups in unseen rooms: drifting z's (RoomFogLook.cs)
            DrawPennedMarks(t);  // enemies a chalk loop holds (ChalkLook.cs)
            DrawBambooGroveFront(t);  // Bamboo Grove's near stalks, in front of the enemies (BambooGroveLook.cs)
            DrawFlashFireFront(t);  // Flash Fire's near flames, in front of the enemies (FlashFireLook.cs)
            DrawSprayOver(t);  // Arterial Spray's soaked enemies and drops in flight (ArterialSprayLook.cs)
            DrawBloodbathOver(t);  // enemies wading in a Bloodbath: tinted, bleeding drops (BloodbathLook.cs)
            DrawGlSmokeOver(t);  // the Grenade Launcher's clouds over the enemies, jammed ones greyed (GrenadeLauncherLook.cs)
            DrawWardenOver(t);  // the Warden blinded or stunned (WardenLook.cs)
            DrawWardenIntro(t);  // the Warden's entrance: its Lockdown cell (WardenIntro.cs)
            DrawVeinBeams(t);  // HemomancerLook.cs
            DrawRadiantOrbs(t);
            DrawLeechTendrils(t);
            DrawImplodeMark(t);
            DrawTauntMarks(t);  // enemies a taunt has turned on its caster (AbilityLooks.cs)
            DrawKnightDpsOver(t);  // the Drawbridge falling, the Gauntlet's crests (KnightDpsLook.cs)
            DrawColossusAir(t);  // what the Colossus threw, in flight (ColossusLook.cs)
            DrawClotAir(t);      // the Gore Mortar's drops falling (GoreMortarLook.cs)
            DrawBulletRainAir(t);  // Bullet Rain's rounds falling, landing rings and sparks (BulletRainLook.cs)
            DrawGadgetAim(t);  // GadgetLook.cs: the throw's arc while the Gadget key is held
            DrawLoot(t);  // your chest item, spinning in its beam (PropsLook.cs)
            foreach (var p in game.Players.Values)
            {
                if (p.SwallowT > 0f && !p.Downed)
                    continue;  // inside the Devourer (DevourerLook.cs shows them in its mouth)
                if (LaunchHides(p))
                    continue;  // not landed yet, beamed out or mid Shadow Step: the held weapons and their arms go with the body (LaunchIntro.cs, Recall.cs)
                DrawPlayerWrapped(p, t);
                DrawHeldGadget(p, t);  // GadgetLook.cs
                DrawHalberdIdle(p, t);  // the Halberd held at the ready (HalberdLook.cs)
                DrawRapierIdle(p, t);  // the Rapier held at the ready (MeleeLooks.cs)
                DrawKatanaIdle(p, t);  // the Katana held at the ready (KatanaLook.cs)
                DrawGreatswordIdle(p, t);  // the Greatsword held at the ready (GreatswordLook.cs)
                DrawQuarterstaffIdle(p, t);  // the Quarterstaff held at the ready (QuarterstaffLook.cs)
                DrawBoStaffIdle(p, t);  // the Bo Staff held at the ready (BoStaffLook.cs)
                DrawMeleeHeldIdle(p);  // the Kusarigama and Blood Lash held at the ready (MeleeLooks.cs)
                DrawTwinIdle(p, t);  // the Twin Blades' sabres held at the ready (TwinBladesLook.cs)
                DrawKnuckleIdle(p, t);  // the Arc Knuckles up in a guard (ArcKnucklesLook.cs)
                DrawNunIdle(p, t);  // the Nunchaku tucked, swung or spinning its guard (NunchakuLook.cs)
                DrawNodIdle(p, t);  // the Nodachi at the ready or mid-step (NodachiLook.cs)
                DrawVgIdle(p, t);  // the Vowguard's sword and shield at the ready or mid-step (VowguardLook.cs)
                DrawKnucklePrompt(p, t);  // the Arc Knuckles' dash chain: Fire now (ArcKnucklesLook.cs)
                DrawKnuckleLock(p, t);  // the Arc Knuckles' target reticle (ArcKnucklesLook.cs)
                DrawKnuckleWheel(p, t);  // your own Arc Knuckles' click wheel (ArcKnucklesWheel.cs)
                DrawLightIdle(p, t);  // the Lightbringer held at the ready (LightbringerLook.cs)
                DrawLanceIdle(p, t);  // the Lance and shield held at the ready (LanceLook.cs)
                DrawFrostLanceHeld(p, t);  // the Frost Lance held and swung, its chain pips (FrostLanceLook.cs)
                DrawBulwarkHeld(p, t);  // the Bulwark Barrier round your front half, and its Ram pips (BulwarkBarrierLook.cs)
                DrawMercyWard(p, t);  // a Lightbringer's ward on anyone (LightbringerLook.cs)
                DrawCenser(p, t);  // the Censer held, swung and thrown (CenserLook.cs)
                DrawHammerIdle(p, t);  // the Warhammer held at the ready (MeleeLooks.cs)
                DrawStunned(p, t);  // WardenLook.cs
                DrawRodSparks(p, t);  // Lightning Rod's arcs on the body (LightningRodLook.cs)
                DrawLivingBoltSparks(p, t);  // Living Bolt's arcs on the body (LivingBoltLook.cs)
                DrawHeldStunGun(p, t);
            }
            DrawWardenNet(t);  // the Warden's net, thrown and wrapped round someone, over the bodies (HoldingCellLook.cs)
            DrawFlankArrows(t);  // a flank spawn portal behind you (SpawnPortalLook.cs)
            foreach (var b in game.Bullets.Values)
                if (b.Kind >= Defs.SmallShot && !(enemyShotsLater && Defs.IsEnemyShot(b.Kind)))
                    DrawSmallShot(b, t);
            DrawQueenSwarm(t);  // QueenLook.cs
            DrawKnightDpsTop(t);  // Caught Steel's shards, the Gauntlet lying where it hit (KnightDpsLook.cs)
            DrawKatanaMarks(t);  // the enemies a Momentum Burst marked, and its cracks (KatanaLook.cs)
            DrawStonespearLeftovers(t);  // spent Stonespear spears and their dust (StonespearLook.cs)
            foreach (var fx in game.Effects)
                DrawEffect(fx, t);
        }

        /// Enemy bullets, drawn last in the arena (after effects, sparks and the additive glow; before Fogbound's mist and
        /// the mood tint): whatever the players' weapons are doing, the shots you have to dodge stay on top.
        void DrawEnemyShots(float t)
        {
            foreach (var b in game.Bullets.Values)
                if (b.Kind >= Defs.SmallShot && Defs.IsEnemyShot(b.Kind))
                    DrawSmallShot(b, t);
        }

        /// Under a player: the healer's aura, Barrier, buffs, the parry shield and ultimates' looks.
        void DrawPlayerAuras(Player p, float t)
        {
            if (LaunchHides(p))
                return;  // not landed yet (LaunchIntro.cs)
            if (p.Downed)
                return;
            LookOf(p.Kit).Auras?.Invoke(this, p, t);  // the class's own auras (ClassLooks.cs)
            DrawBuffTimers(p, t);  // a shrinking arc under them for each timed buff (AbilityLooks.cs)
            DrawRodAura(p, t);     // Lightning Rod's charge (LightningRodLook.cs)
            DrawVortex(p, t);      // the Vortex ultimate's storm (VortexLook.cs)
            DrawLivingBoltAura(p, t);  // Living Bolt's channel, halo and time ring (LivingBoltLook.cs)
            DrawStonespearUnder(p, t);  // the hovering spears' shadows (StonespearLook.cs)
            DrawTempest(p, t);  // Wind Clash's Tempest (WindClashLook.cs)
            DrawStaticShockUnder(p, t);  // Overcharge's gold ring; the plain cone (StaticShockLook.cs)
            if (p.CoatT > 0f)
                DrawHealingCoat(p, t);  // a Sanguine Orb's healing blood (HemomancerLook.cs)
            if (p.DoseT > 0f)  // dosed by Medicine Darts: green motes rising off you while it heals
                for (int k = 0; k < 4; k++)
                {
                    float f = MathX.Fmod(t * 1.1f + k * 0.25f + p.Id * 0.3f, 1f);
                    V2 at = p.Disp + new V2((k - 1.5f) * p.Def.BodyR * 0.45f, p.Def.BodyR * 0.6f - f * p.Def.BodyR * 1.8f);
                    Circle(at, 2.2f * (1f - f) + 0.8f, new Rgba(0.5f, 1f, 0.6f, 0.85f * (1f - f)), 6);
                }
            if (p.Weapon.Id == WeaponId.Katana)  // both stances' combo pips over your head (KatanaLook.cs)
                DrawKatanaHud(p, t);
            if (p.Weapon.Id == WeaponId.BoStaff)  // where you are in the slash, jab, spin chain (BoStaffLook.cs)
                DrawBoStaffPips(p, t);
            if (p.Id == game.MyId && Game.InGreatStar(p))
                DrawGreatStarRoute(p, t);  // the Great Star's ink route as you draw it (GreatStarLook.cs)
            if (p.Id == game.MyId && game.OathPickSlot >= 0)
                DrawOathPick(p, t);  // Guardian's Oath waiting for you to click the ally to grab (GuardiansOathLook.cs)
            if (p.Weapon.Id == WeaponId.Kunai && p.KunaiMarkT > 0f)  // the shadow kunai's mark: blink there with a double-tap
            {
                var shade = new Rgba(0.55f, 0.45f, 0.85f);
                V2 m = p.KunaiMark;
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * 8f);
                Arc(m, 12f + 3f * pulse, 1.5f, shade.WithAlpha(0.7f), 16);
                Line(m + new V2(-6f, -6f), m + new V2(5f, 5f), 3f, shade);  // the knife, stuck in the ground
                Line(m + new V2(-8f, -3f), m + new V2(-3f, -8f), 2f, shade.WithAlpha(0.8f));
            }
            if (p.Weapon.Id == WeaponId.IceShards && p.Orbiters > 0)  // Ice Shards waiting in orbit
            {
                for (int i = 0; i < p.Orbiters; i++)  // ice shards circling you, each tumbling a little round its own centre
                {
                    V2 at = Game.OrbiterPos(p.Disp, i, p.Orbiters, game.AnimT), d = (at - p.Disp).Normalized();
                    V2 pt = (d.Orthogonal() * Mathf.Cos(Mathf.Sin(t * 2f + i) * 0.5f) + d * Mathf.Sin(Mathf.Sin(t * 2f + i) * 0.5f)).Normalized();  // the tangent, rocking
                    Circle(at, 6f, IceCol.WithAlpha(0.25f), 8);
                    IceShard(at, pt, 7f, 2.8f, 1f);
                }
            }
            if (p.Weapon.Id == WeaponId.Lightbringer && p.Light > 0)  // the light gauge: pips filling round you, a halo once it's full
            {
                bool full = p.Light >= Game.LightMax;
                float r = p.Def.BodyR + 9f;
                var c = new Rgba(1f, 0.95f, 0.65f, full ? 0.7f + 0.3f * Mathf.Sin(t * 10f) : 0.75f);
                for (int i = 0; i < p.Light; i++)
                    Circle(p.Disp + V2.FromAngle(-MathX.Pi / 2f + MathX.Tau * i / Game.LightMax) * r, full ? 2.6f : 2f, c, 6);
                if (full)
                    Arc(p.Disp, r + 5f, 2f, c.WithAlpha(0.5f), 24);
            }
            if (p.Weapon.Id == WeaponId.TreantRod)  // the root from you to your Treant (RootLook.cs: the same bark as the lash)
                foreach (var m in game.Minions.Values)
                    if (m.Owner == p.Id && m.Kind == MinionKind.Treant && m.Companion)
                    {
                        V2 to = m.Disp - p.Disp;
                        float len = to.Length;
                        if (len < 4f)
                            break;
                        DrawRoot(p.Disp, to / len, len, 5f, 4f, -t * 2f, 0.95f, true, false);
                        Line(p.Disp, m.Disp, Game.VineWidth * 2f, RootBark.WithAlpha(0.07f));  // how wide it mends and stings
                        break;
                    }
            if (p.Weapon.Id == WeaponId.BurstSmg && p.SpinUp > 0.02f)  // the Burst SMG spinning up: a ring filling round you, white-hot when full
            {
                float r = p.Def.BodyR + 9f;
                bool full = p.SpinUp >= 0.99f;
                var hot = new Rgba(1f, 0.6f, 0.2f).Mix(new Rgba(1f, 0.95f, 0.8f), p.SpinUp);
                Arc(p.Disp, r, 2f, new Rgba(1f, 1f, 1f, 0.1f), 28);
                Arc(p.Disp, r, full ? 3.5f : 2.5f, hot.WithAlpha(full ? 0.7f + 0.3f * Mathf.Sin(t * 30f) : 0.8f), 28, t * 9f, MathX.Tau * p.SpinUp);
                if (Juicy)  // the real spray cone, tightening as it spins up (DpsGunLooks.cs)
                {
                    float spray = Game.SmgSpray - (Game.SmgSpray - Game.SmgSprayTight) * p.SpinUp;
                    for (int sg = -1; sg <= 1; sg += 2)
                    {
                        V2 d = V2.FromAngle(p.Aim + sg * spray);
                        Line(p.Disp + d * 24f, p.Disp + d * 300f, 1f, new Rgba(1f, 0.7f, 0.35f, 0.14f + 0.14f * p.SpinUp));
                    }
                }
            }
            if (p.Role == Role.Healer)
                DrawHealerAura(p, t);  // HealerAuraLook.cs
            if (fxOn)
                DrawBulwark(p, t);  // eight armour plates (BarrierLook.cs)
            else if (p.BarrierT > 0f)
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
            if (p.SwiftT > 0f)
                DrawSwiftness(p, t);  // cyan ring and speed streaks (HolyCrossLook.cs)
            if (p.MomentumStacks > 0)
                DrawMomentum(p, t);  // pips on a ring, one per stack (MomentumLook.cs)
            if (p.IronCap > 0f)
                DrawIronhide(p, t);  // five steel plates on a ring (IronhideLook.cs; a Tank, or an ally with Shared Plating's gift)
            if (p.AdrenalineT > 0f)
                DrawAdrenaline(p, t);  // heartbeat rings and a thumping heart (AdrenalineLook.cs)
            if (p.AegisT > 0f)  // Aeroaegis: the gust shell (AeroaegisLook.cs)
                DrawAeroaegis(p, t);
            if (p.WardT > 0f && !p.Downed)  // Martyr's ward: a halo of twelve beads, one a second (MartyrLook.cs)
                DrawMartyrHalo(p, t);
            DrawGustBoost(p, t);  // a Gust Gate boost: speed lines and a swirl (GustGateLook.cs)
            if (p.ShellOn)  // Blood Shell: the bubble (BloodShellLook.cs)
                DrawBloodShell(p, t);
            if (p.ShieldHp > 0f && p.Kit == Kit.Hemomancer)  // Blood Gift alone: plates of blood (HemomancerLook.cs)
                DrawBloodShellOn(p, t);
            else if (p.ShieldHp > 0f && p.MercyT <= 0f)  // a shield, the plain ring (a Lightbringer's ward draws its own plates)
                Arc(p.Disp, p.Def.BodyR + 5f, 3f, new Rgba(0.5f, 0.7f, 1f, 0.4f + 0.5f * Mathf.Min(1f, p.ShieldHp / 60f)), 32);
            if (p.OverHp > 0f && !p.Downed)  // a health shrine's health past max: a gold ring with turning ticks (ShrineLook.cs)
                DrawOverHealth(p, t);
            if (p.LegionT > 0f)  // Legion: the blood moon (LegionLook.cs)
                DrawBloodMoon(p, t);
            if (p.FrenzyT > 0f)  // Alpha Call
                Arc(p.Disp, p.Def.BodyR + 9f, 2f, new Rgba(1f, 0.5f, 0.3f, 0.7f), 16, t * 6f, MathX.Pi);
            if (p.ParryActive)
                DrawParryShield(p, t);
            else if (p.ParryBroken > 0f && !p.Downed)  // broken: cracked pieces until it's back
                for (int k = 0; k < 3; k++)
                    Arc(p.Disp, p.Def.BodyR + 9f, 2f, new Rgba(1f, 0.45f, 0.45f, 0.35f), 4, p.Aim - 0.8f + k * 0.6f, 0.35f);
            else if (p.Id == game.MyId && p.ParryHp < Game.ParryMax(p))  // lowered and refilling: your own gauge, faintly
                DrawParryGauge(p, t, 0.4f);
            if (p.DomeT > 0f)  // Fortress: the dome (DomeLook.cs)
                DrawFortressDome(p, t);
            if (p.UltT > 0f && p.Slots[Upgrades.UltSlot].Id == AbilityId.ArtilleryAssault)  // Artillery Assault (ArtilleryLook.cs)
                DrawArtillery(p, t);
            DrawRiotGear(p, t);  // Riot Gear's plate (RiotGearLook.cs)
            DrawVanishSmoke(p, t);  // Vanish (NinjaLooks.cs)
            DrawCeremonialFloor(p, t);  // Ceremonial Armor's aura, rings and sigil (CeremonialArmorLook.cs)
            DrawSpiritBack(p, t);  // Spirit Avatar's ink body, seal, mask and the cleave's floor arc, under the model (SpiritAvatarLook.cs)
            if (p.DashT > 0f)  // Healing Dash's sprint: speed lines and a mint wake (NinjaLooks.cs)
                DrawDashWake(p, t);
            else if (p.Id == game.MyId ? game.SprintT > 0f : p.Sprint > 0)  // the Dodge's held sprint (NinjaLooks.cs)
                DrawSprintWake(p, t, p.Id == game.MyId ? game.SprintFade : p.Sprint / 255f);
            if (p.Chains.Count > 0 || p.LeashOn)  // the Blood Lash's chains and Martyr's Leash (BloodLashLook.cs)
                DrawLashChains(p, t);
            if (p.BsTarget != 0 || p.SympT > 0f)  // the Blood Brawler's thread, mark and Sympathy (BloodBrawlerLook.cs)
                DrawBsThread(p, t);
            if (p.TitheClock > 0f)  // Vein Tithe: heart, arteries, cuffs (VeinTitheLook.cs)
                DrawTithe(p, t);
            if (p.RainClock > 0f)  // Sanguine Rain: siphon, seeds, rings, tether (SanguineRainLook.cs)
                DrawRain(p, t);
            if (p.GaleT > 0f)  // Gale Wall's lens and the shots hung in it (GaleWallLook.cs)
                DrawGaleWall(p, t);
            if (p.HarvestHold > 0f)  // Crimson Harvest's ball and tendrils (CrimsonHarvestLook.cs)
                DrawHarvest(p, t);
            if (p.OrbitT > 0f)  // Blade Fan's circling kunai (NinjaLooks.cs)
                DrawBladeOrbit(p, t);
            DrawWaterWard(p, t);  // Water Ward's circling drops (WaterWardLook.cs; returns at once without any)
        }

        /// The parry shield: an arc toward the cursor, with its durability gauge around the outside.
        void DrawParryShield(Player p, float t)
        {
            float rr = p.Def.BodyR + Game.ParryReach * 0.55f;
            var sc = new Rgba(0.55f, 0.9f, 1f);
            float from = p.Aim - Game.ParryArc(p), span = Game.ParryArc(p) * 2f;
            Arc(p.Disp, rr, 14f, sc.WithAlpha(0.45f), 20, from, span);                          // the shield's face
            Arc(p.Disp, rr + 7f, 4f, sc.Mix(Rgba.White, 0.6f).WithAlpha(0.95f), 20, from, span);  // bright rim
            for (int k = 0; k <= 1; k++)  // end caps
                Circle(p.Disp + V2.FromAngle(from + span * k) * (rr + 2f), 4f, sc.Mix(Rgba.White, 0.4f), 8);
            DrawParryGauge(p, t, 1f);
        }

        /// The parry's durability: a thin blue bar just outside the shield's rim that shrinks toward your aim
        /// as it wears, blinking red when nearly broken. Faint (alpha below 1) while the shield is lowered.
        void DrawParryGauge(Player p, float t, float alpha)
        {
            float hp = Mathf.Clamp01(p.ParryHp / Game.ParryMax(p));
            float rad = p.Def.BodyR + Game.ParryReach * 0.55f + 12.5f, half = Game.ParryArc(p);
            var gc = new Rgba(0.37f, 0.71f, 1f);
            var warn = new Rgba(1f, 0.42f, 0.42f);
            Arc(p.Disp, rad, 4f, new Rgba(0.02f, 0.03f, 0.07f, 0.55f * alpha), 20, p.Aim - half, half * 2f);  // dark backing
            Arc(p.Disp, rad, 2.5f, gc.WithAlpha(0.18f * alpha), 20, p.Aim - half, half * 2f);                // empty track
            if (hp <= 0f)
                return;
            bool low = hp < 0.35f && alpha >= 1f;
            Rgba c = low ? (MathX.Fmod(t, 0.2f) < 0.1f ? warn : gc.Mix(warn, 0.35f)) : gc;
            Arc(p.Disp, rad, 2.5f, c.WithAlpha(0.95f * alpha), 20, p.Aim - half * hp, half * 2f * hp);
        }

        /// Players' big shots (kinds below Defs.SmallShot), drawn under enemies.
        void DrawBigShot(Bullet b, float t)
        {
            float keep = glAlpha;
            glAlpha = keep * TeammateShotAlpha(b);  // WorldShots.cs
            DrawBigShotLook(b, t);
            glAlpha = keep;
        }

        void DrawBigShotLook(Bullet b, float t)
        {
            DrawShotMods(b, t);
            if (DrawAbilityShot(b, t))
                return;  // Nova's comets, the turret's tracers (SentryNovaLook.cs)
            if (DrawWeaponShot(b, t))
                return;  // its weapon's new look (ShotLooks.cs)
            if (b.Kind == Defs.GrenadeShot)  // spinning grenade with a blinking fuse
            {
                if (fxOn)
                {
                    DrawGrenadeFlight(b, t);  // SoldierLooks.cs
                    return;
                }
                Polygon(b.Pos, b.Radius, 6, t * 12f, Defs.BulletColor(b.Kind));
                if (MathX.Fmod(t, 0.15f) < 0.075f)
                    Circle(b.Pos, 3f, new Rgba(1f, 0.3f, 0.2f), 8);
                return;
            }
            var shotCol = ShotColor(b.Kind, b.Owner);
            if (b.Kind == Defs.FireShot || b.Kind == Defs.HolyShot || b.Kind == Defs.ArcaneShot)
                Circle(b.Pos, b.Radius * (1.8f + 0.3f * Mathf.Sin(t * 30f + b.Id)), shotCol.WithAlpha(0.25f), 12);
            if (b.Kind == Defs.RailShot)
                Line(b.Pos - b.Vel * 0.03f, b.Pos, 3f, Defs.BulletColor(b.Kind).WithAlpha(0.6f));
            Circle(b.Pos, b.Radius, shotCol.WithAlpha(0.8f), 10);
        }

        /// An incoming lightning strike: a filling warning circle (or a Lich's dead hand, LichLook.cs).
        void DrawHazard(Hazard h, float t)
        {
            if (h.Kind == Game.HazardHand)
            {
                DrawDeadHand(h, t);
                return;
            }
            var hc = h.Kind == Game.HazardCaller || h.Kind == Game.HazardDownburst ? SqAmber : new Rgba(0.7f, 0.85f, 1f);  // a Stormcaller's is amber (SquallLook.cs)
            float f = 1f - MathX.Clamp01(h.T / Mathf.Max(0.01f, h.Duration));
            Circle(h.Pos, h.Radius * f, hc.WithAlpha(0.25f), 32);
            Arc(h.Pos, h.Radius, 2.5f, (f > 0.7f && MathX.Fmod(t, 0.1f) < 0.05f ? Rgba.White : hc).WithAlpha(0.85f), 40);
        }

        /// Radiant Orbs held out (RadiantOrb.cs): a faint beam of light back to the caster, and the orb itself, a soft
        /// glow around a bright core with slow-turning rays.
        void DrawRadiantOrbs(float t)
        {
            var c = Defs.BulletColor(Defs.HolyShot);
            foreach (var p in game.Players.Values)
            {
                if (!p.RadiantOn || p.Downed || p.Weapon.Id != WeaponId.RadiantOrb)
                    continue;
                V2 o = p.RadiantDisp;
                float r = Game.RadiantSize(p), pulse = 0.5f + 0.5f * Mathf.Sin(t * 8f + p.Id);
                Line(p.Disp, o, 6f, c.WithAlpha(0.08f));
                Line(p.Disp, o, 1.5f, c.WithAlpha(0.35f));
                Circle(o, r * 1.6f, c.WithAlpha(0.1f + 0.05f * pulse), 28);
                Circle(o, r, c.WithAlpha(0.28f), 28);
                for (int k = 0; k < 6; k++)
                {
                    V2 d = V2.FromAngle(t * 1.5f + k * MathX.Tau / 6f);
                    Line(o + d * (r * 0.55f), o + d * (r * (1.05f + 0.15f * pulse)), 2f, c.WithAlpha(0.6f));
                }
                Circle(o, r * 0.45f + 1.5f * pulse, c, 16);
                Circle(o, r * 0.22f, Rgba.White, 10);
            }
        }

        /// Roar's ring.
        void DrawPlayerLinks(Player p, float t)
        {
            if (LaunchHides(p))
                return;
            if (p.TauntT > 0f && !p.Downed && fxOn)  // Roar: plates, a draining rim and tethers (TankLooks.cs)
                DrawRoarSustain(p, t);
            else if (p.TauntT > 0f && !p.Downed)  // Roar
                Arc(p.Disp, p.Def.BodyR + 12f + 3f * Mathf.Sin(t * 16f), 3f, new Rgba(1f, 0.4f, 0.25f, 0.85f), 32);
        }

        /// A health orb: a green disc with a white cross; big ones get a gold ring.
        void DrawOrb(Orb o, float t)
        {
            if (o.Med)
            {
                DrawMedOrb(o, t);  // HealerKitLooks.cs
                return;
            }
            // Blink during the last 3 seconds before the orb disappears.
            if (o.Life < 3f && MathX.Fmod(t, 0.25f) < 0.1f)
                return;
            DrawPlainOrb(o, t);  // OrbLook.cs
        }

        /// A player, plus, near an edge, the part of them already coming in on the far side.
        void DrawPlayerWrapped(Player p, float t)
        {
            if (LaunchHides(p))
                return;  // still on its way down in a pillar of light (LaunchIntro.cs)
            DrawPlayer(p, t);
            if (!p.Downed)
                DrawStonespears(p, t);  // the hovering spears (StonespearLook.cs)
            DrawEmbolus(p, t);  // the Embolus Spears out of the hands (EmbolusLook.cs)
            DrawHoneySpray(p, t);  // the Honeybee Staff's spray (HoneybeeLook.cs)
            if (game.IsHunted(p) && !p.Downed)
                DrawHuntedMark(p.Disp, p.Def.BodyR, t);  // ModifiersUi.cs
            if (game.Walled)
                return;  // nothing comes in on the far side when the edges are walls
            V2 real = p.Disp;
            float edge = p.Def.BodyR + 12f;
            float dx = real.X < edge ? AW : real.X > AW - edge ? -AW : 0f;
            float dy = real.Y < edge ? AH : real.Y > AH - edge ? -AH : 0f;
            // The copies on the far sides (no list: this runs per player per frame).
            if (dx != 0f) { p.Disp = real + new V2(dx, 0f); DrawPlayer(p, t); }
            if (dy != 0f) { p.Disp = real + new V2(0f, dy); DrawPlayer(p, t); }
            if (dx != 0f && dy != 0f) { p.Disp = real + new V2(dx, dy); DrawPlayer(p, t); }  // corners
            p.Disp = real;
        }

        /// A four-pointed throwing star.
        void DrawStar(V2 c, float r, float rot, Rgba col)
        {
            SetColor(col);
            for (int k = 0; k < 4; k++)
            {
                V2.Axes(rot + MathX.Pi / 2f * k, out V2 d, out V2 s);
                Tri(c + d * r, c + s * (r * 0.3f), c - s * (r * 0.3f));
            }
            Circle(c, r * 0.25f, NinjaCloth.WithAlpha(col.A), 6);
        }

        // --- GL primitives (arena coordinates; everything is triangles) -------

        Vector3 Px(V2 p)
        {
            if (camZoomOn)
                p = ZoomPoint(p);  // the scroll-wheel zoom (CameraZoom.cs)
            var v = CanvasPx(glShift + p * glZoom);
            v.x += shakeX - leanPxX;  // screen shake (Juice.cs) and camera lean (CameraLean.cs) while the world is drawn; 0 otherwise
            v.y += shakeY + leanPxY;  // (GL's y runs up the screen, the arena's runs down)
            if (punchK != 0f)
            {
                float cx = punchX - leanPxX, cy = punchY + leanPxY;  // the Twin Rifle's punch, a charge weapon's draw and release: a hair bigger or smaller round you (TwinRifleFx.cs, ChargeCameraFx.cs)
                v.x = cx + (v.x - cx) * (1f + punchK);
                v.y = cy + (v.y - cy) * (1f + punchK);
            }
            return v;
        }

        /// A point on the UI canvas (or, while the world is drawn, in the arena) in screen pixels.
        Vector3 CanvasPx(V2 u)
        {
            float x = u.X + uiShift.X, y = u.Y + uiShift.Y;  // (uiShift: a menu panel sliding, WindowAnim.cs)
            if (crtSquashOn)  // the lobby's power-cycle leave squashing the screen about its middle (LobbyCrtLeave.cs)
            {
                x = W / 2f + (x - W / 2f) * crtSqX;
                y = H / 2f + (y - H / 2f) * crtSqY;
            }
            return new Vector3(offX + x * drawScale, UnityEngine.Screen.height - (offY + y * drawScale), 0f);
        }

        /// Start drawing game models (GL) on top of the menus, in UI canvas coordinates. IMGUI draws in order
        /// on Repaint, so models land above the panels drawn before and below those drawn after. Returns
        /// false (draw nothing) on other events. Always pair with EndUiGl.
        bool BeginUiGl()
        {
            if (Event.current.type != EventType.Repaint || glMaterial == null || (uiGlBlocked && !pieceGlOn))
                return false;  // (uiGlBlocked: the lobby sliding away, whose live models wait; LaunchIntro.cs)
            glMaterial.SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix();
            GL.Begin(GL.TRIANGLES);
            drawScale = scale;
            glAlpha = Ui.Alpha;  // models in a fading panel fade with it (the pause menu's run card, a window opening)
            if (pieceGlOn)  // a lobby piece arriving (LobbyEnter.cs): only its own models show, cut to its rect
            {
                glClip = pieceGlClip;
                glClipOn = true;
            }
            else if (menuGlClipX >= 0f)  // the main menu's scan line sweeping: models show only on the menu buttons' side of it (Menus.cs)
            {
                glClip = new Rect(menuGlClipX, 0f, Mathf.Max(0f, W - menuGlClipX), H);
                glClipOn = true;
            }
            return true;
        }

        /// While the main menu's box sweeps between the buttons and the picker: the scan line's x, so the buttons' GL models
        /// (your name card's figure) are cut there like the rest of the buttons. -1 = no cut.
        float menuGlClipX = -1f;

        void EndUiGl()
        {
            GL.End();
            GL.PopMatrix();
            glZoom = 1f;
            glAlpha = 1f;
            glShift = V2.Zero;
            glClipOn = false;
        }

        // Clipping for menu previews (the loadout's mini-arena): triangles are cut to this UI canvas rect.
        bool glClipOn;
        Rect glClip;
        readonly V2[] clipIn = new V2[9], clipOut = new V2[9];

        /// Until EndUiGl (or UiNoClip), cut everything drawn to this rect of the UI canvas.
        void UiClip(Rect r)
        {
            if (pieceGlOn)  // a lobby piece being revealed (LobbyEnter.cs, LobbyCrt.cs): cut to both, so the models don't show past the reveal
            {
                float x0 = Mathf.Max(r.x, pieceGlClip.x), y0 = Mathf.Max(r.y, pieceGlClip.y);
                r = new Rect(x0, y0, Mathf.Max(0f, Mathf.Min(r.xMax, pieceGlClip.xMax) - x0), Mathf.Max(0f, Mathf.Min(r.yMax, pieceGlClip.yMax) - y0));
            }
            glClip = r;
            glClipOn = true;
        }

        void UiNoClip()
        {
            glClip = pieceGlClip;  // (back to the revealed piece's cut while one is drawing)
            glClipOn = pieceGlOn;
        }

        /// Models drawn next sit around (0, 0), scaled by zoom and centred on `at` (UI canvas).
        void UiModelAt(V2 at, float zoom)
        {
            glShift = at;
            glZoom = zoom;
        }

        static void SetColor(Rgba c)
        {
            glCur = new Color(c.R, c.G, c.B, c.A);  // remembered for a held frame's replay (Juice.cs)
            if (glShadow)  // a Mirror Match's shadow: its player's look in dark violet (ChallengeLook.cs)
                glCur = ShadowTint(glCur);
            if (glDesat > 0f)  // a run ending in a game over: the colour drains out (RunEnding.cs)
            {
                float l = 0.3f * c.R + 0.59f * c.G + 0.11f * c.B;
                glCur = new Color(c.R + (l - c.R) * glDesat, c.G + (l - c.G) * glDesat, c.B + (l - c.B) * glDesat, c.A);
            }
            if (glAlpha < 1f)
                glCur.a *= glAlpha;
            GL.Color(glCur);
        }

        /// UI GL's opacity (BeginUiGl takes Ui.Alpha, EndUiGl puts it back to 1); the arena's own drawing is always 1.
        static float glAlpha = 1f;

        void Tri(V2 a, V2 b, V2 c)
        {
            if (!glClipOn)
            {
                Vector3 pa = Px(a), pb = Px(b), pc = Px(c);
                if (OffScreen(pa, pb, pc))
                    return;
                Vtx(pa);
                Vtx(pb);
                Vtx(pc);
                return;
            }
            V2 ua = glShift + a * glZoom, ub = glShift + b * glZoom, uc = glShift + c * glZoom;
            float x0 = glClip.x, x1 = glClip.xMax, y0 = glClip.y, y1 = glClip.yMax;
            if ((ua.X < x0 && ub.X < x0 && uc.X < x0) || (ua.X > x1 && ub.X > x1 && uc.X > x1) ||
                (ua.Y < y0 && ub.Y < y0 && uc.Y < y0) || (ua.Y > y1 && ub.Y > y1 && uc.Y > y1))
                return;  // all outside
            if (InClip(ua) && InClip(ub) && InClip(uc))
            {
                Vtx(CanvasPx(ua));
                Vtx(CanvasPx(ub));
                Vtx(CanvasPx(uc));
                return;
            }
            // Cut the triangle against each edge in turn (Sutherland-Hodgman), then fan it back into triangles.
            clipIn[0] = ua; clipIn[1] = ub; clipIn[2] = uc;
            int n = 3;
            n = ClipEdge(clipIn, n, clipOut, 0, x0, false);
            n = ClipEdge(clipOut, n, clipIn, 0, x1, true);
            n = ClipEdge(clipIn, n, clipOut, 1, y0, false);
            n = ClipEdge(clipOut, n, clipIn, 1, y1, true);
            for (int i = 1; i + 1 < n; i++)
            {
                Vtx(CanvasPx(clipIn[0]));
                Vtx(CanvasPx(clipIn[i]));
                Vtx(CanvasPx(clipIn[i + 1]));
            }
        }

        // A colour per vertex, clipped like Tri: the colours are blended along each cut edge.
        readonly V2[] cvIn = new V2[9], cvOut = new V2[9];
        readonly Rgba[] ccIn = new Rgba[9], ccOut = new Rgba[9];

        void ClippedVTri(V2 a, Rgba ca, V2 b, Rgba cb, V2 c, Rgba cc)
        {
            V2 ua = glShift + a * glZoom, ub = glShift + b * glZoom, uc = glShift + c * glZoom;
            float x0 = glClip.x, x1 = glClip.xMax, y0 = glClip.y, y1 = glClip.yMax;
            if ((ua.X < x0 && ub.X < x0 && uc.X < x0) || (ua.X > x1 && ub.X > x1 && uc.X > x1) ||
                (ua.Y < y0 && ub.Y < y0 && uc.Y < y0) || (ua.Y > y1 && ub.Y > y1 && uc.Y > y1))
                return;  // all outside
            cvIn[0] = ua; cvIn[1] = ub; cvIn[2] = uc;
            ccIn[0] = ca; ccIn[1] = cb; ccIn[2] = cc;
            int n = 3;
            n = ClipEdgeC(cvIn, ccIn, n, cvOut, ccOut, 0, x0, false);
            n = ClipEdgeC(cvOut, ccOut, n, cvIn, ccIn, 0, x1, true);
            n = ClipEdgeC(cvIn, ccIn, n, cvOut, ccOut, 1, y0, false);
            n = ClipEdgeC(cvOut, ccOut, n, cvIn, ccIn, 1, y1, true);
            for (int i = 1; i + 1 < n; i++)
            {
                SetColor(ccIn[0]); Vtx(CanvasPx(cvIn[0]));
                SetColor(ccIn[i]); Vtx(CanvasPx(cvIn[i]));
                SetColor(ccIn[i + 1]); Vtx(CanvasPx(cvIn[i + 1]));
            }
        }

        static int ClipEdgeC(V2[] src, Rgba[] srcC, int n, V2[] dst, Rgba[] dstC, int axis, float at, bool max)
        {
            int m = 0;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                V2 p = src[i], q = src[j];
                float pv = axis == 0 ? p.X : p.Y, qv = axis == 0 ? q.X : q.Y;
                bool pin = max ? pv <= at : pv >= at, qin = max ? qv <= at : qv >= at;
                if (pin)
                {
                    dst[m] = p; dstC[m] = srcC[i]; m++;
                }
                if (pin != qin)
                {
                    float k = (at - pv) / (qv - pv);
                    Rgba x = srcC[i], y = srcC[j];
                    dst[m] = V2.Lerp(p, q, k);
                    dstC[m] = new Rgba(x.R + (y.R - x.R) * k, x.G + (y.G - x.G) * k, x.B + (y.B - x.B) * k, x.A + (y.A - x.A) * k);
                    m++;
                }
            }
            return m;
        }

        bool InClip(V2 u) => u.X >= glClip.x && u.X <= glClip.xMax && u.Y >= glClip.y && u.Y <= glClip.yMax;

        /// Keeps the part of polygon src on one side of the line axis = at (max: keep below it), into dst.
        static int ClipEdge(V2[] src, int n, V2[] dst, int axis, float at, bool max)
        {
            int m = 0;
            for (int i = 0; i < n; i++)
            {
                V2 p = src[i], q = src[(i + 1) % n];
                float pv = axis == 0 ? p.X : p.Y, qv = axis == 0 ? q.X : q.Y;
                bool pin = max ? pv <= at : pv >= at, qin = max ? qv <= at : qv >= at;
                if (pin)
                    dst[m++] = p;
                if (pin != qin)
                    dst[m++] = V2.Lerp(p, q, (at - pv) / (qv - pv));
            }
            return m;
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

        // --- Vertex budget: Unity's GL immediate mode costs per vertex, so a crowd (a breach wave's flood) is drawn
        // with no more of them than the screen shows. Neither changes the picture by more than a fraction of a pixel.

        float cullW, cullH;  // the screen in pixels (UpdateLayout and each DrawWorld); 0: no culling yet

        void RefreshCull()
        {
            cullW = UnityEngine.Screen.width;
            cullH = UnityEngine.Screen.height;
        }

        /// A triangle wholly off one side of the screen (screen pixels): GL would clip it to nothing anyway.
        bool OffScreen(Vector3 a, Vector3 b, Vector3 c) =>
            cullW > 0f && ((a.x < 0f && b.x < 0f && c.x < 0f) || (a.y < 0f && b.y < 0f && c.y < 0f) ||
                           (a.x > cullW && b.x > cullW && c.x > cullW) || (a.y > cullH && b.y > cullH && c.y > cullH));

        const float SegTol = 0.35f;  // px: how far a segment's middle may sit inside the true curve
        const float CrowdTolMax = 0.8f;  // px: the same while a big crowd is drawn (CrowdTol)
        const int CrowdFrom = 30, CrowdFull = 90;  // enemies on screen: the tolerance starts to grow, and is at its widest
        float segTol = SegTol;  // the tolerance in use (CrowdTol raises it while the enemies are drawn)

        /// The curve tolerance for drawing the enemies this frame: SegTol with up to CrowdFrom on screen, growing to
        /// CrowdTolMax at CrowdFull. A breach flood of a hundred bodies is a third fewer vertices, each curve still within a
        /// pixel of true; a normal wave looks exactly as it did.
        float CrowdTol()
        {
            int n = 0;
            foreach (var e in game.Enemies.Values)
            {
                Vector3 p = Px(e.Disp);
                if (cullW <= 0f || p.x > -40f && p.y > -40f && p.x < cullW + 40f && p.y < cullH + 40f)
                    n++;
            }
            float k = Mathf.Clamp01((n - CrowdFrom) / (float)(CrowdFull - CrowdFrom));
            return SegTol + (CrowdTolMax - SegTol) * k;
        }

        /// How many of `segs` segments a curve of radius r (arena units) over `sweep` radians needs on screen. Only ever
        /// fewer: counts of 8 or less are left alone (a hexagon drawn as a 6-segment circle stays a hexagon).
        int SegsFor(V2 center, float r, float sweep, int segs)
        {
            if (segs <= 8 || r <= 0f)
                return segs;
            Vector3 p0 = Px(center), p1 = Px(center + new V2(r, 0f));
            float px = Mathf.Sqrt((p1.x - p0.x) * (p1.x - p0.x) + (p1.y - p0.y) * (p1.y - p0.y));
            int least = Mathf.Max(3, Mathf.CeilToInt(8f * Mathf.Abs(sweep) / MathX.Tau - 0.01f));  // (8 a full turn: a half turn 4)
            if (px <= segTol * 2f)
                return least;
            float step = 2f * Mathf.Acos(1f - segTol / px);  // the widest angle a segment may span
            int need = Mathf.CeilToInt(Mathf.Abs(sweep) / Mathf.Max(step, 1e-3f));
            return Mathf.Clamp(need, least, segs);
        }

        void Circle(V2 center, float r, Rgba c, int segs)
        {
            segs = SegsFor(center, r, MathX.Tau, segs);
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
            segs = SegsFor(center, r1, sweep, segs);
            for (int i = 0; i < segs; i++)
            {
                V2 d0 = V2.FromAngle(start + sweep * i / segs), d1 = V2.FromAngle(start + sweep * (i + 1) / segs);
                Quad(center + d0 * r0, center + d0 * r1, center + d1 * r1, center + d1 * r0);
            }
        }

        V2[] polyPts = new V2[32];  // Polygon's corners, reused (no array a call)

        void Polygon(V2 center, float r, int sides, float rot, Rgba c)
        {
            if (polyPts.Length < sides)
                polyPts = new V2[sides];
            var pts = polyPts;
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

        void DrawAimLine(Player me, V2 cursor, float t)
        {
            V2 dir = V2.FromAngle(me.Aim);
            V2 from = me.Disp + dir * (me.Def.BodyR + 8f);
            // Out to the crosshair, but never past where your shots (or swings) stop.
            float range = Mathf.Max(game.WeaponRange(me) - me.Def.BodyR - 8f, 20f);
            float toCursor = cursor.DistanceTo(me.Disp) - me.Def.BodyR - 8f;
            float reach = Mathf.Min(range, toCursor);
            if (reach < 4f)
                return;  // crosshair on top of you
            bool capped = toCursor >= range;  // the crosshair is beyond the weapon's reach: the look ends in a cap
            DrawAimLook(AimLook(), from, dir, reach, t, me.Def.Color, capped);  // AimLineLooks.cs
            var c = me.Def.Color.Mix(Rgba.White, 0.35f);
            if (me.Weapon.Id == WeaponId.TwinRifle)  // an X where the Twin Rifle's streams cross: crossfire there
            {
                V2 x = me.Disp + dir * game.TwinCrossDistance(me), s0 = V2.FromAngle(me.Aim + MathX.Pi / 4f) * 7f, s1 = V2.FromAngle(me.Aim - MathX.Pi / 4f) * 7f;
                Line(x - s0, x + s0, 2f, c.WithAlpha(0.7f));
                Line(x - s1, x + s1, 2f, c.WithAlpha(0.7f));
                Arc(x, Game.CrossZone(me), 1.2f, c.WithAlpha(0.18f), 28);  // (Kill Zone widens it)
            }
        }
    }
}
