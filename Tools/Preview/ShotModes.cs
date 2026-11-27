using System; using System.IO; using System.Reflection; using System.Threading; using Arena; using UnityEngine;
// Shot.cs screens: DrawMode draws the screen named by the mode argument (hud, gameover, dev, pause, guide, bg,
// lobby, kitgrid, tab, shop, picker, ...).
static partial class Shot {
  /// BEAT=<pulse 0-1>: the Holo Floor's music reaction frozen mid-song (MenuBeat.cs): a made-up spectrum (strong bass, a
  /// mid bump, falling highs) and beat lines 0.15, 0.5 and 0.85 s old.
  static void FakeBeat(ArenaApp app) {
    var e = Environment.GetEnvironmentVariable("BEAT");
    if (e == null) return;
    var f = BindingFlags.NonPublic | BindingFlags.Instance;
    float pulse = float.Parse(e, System.Globalization.CultureInfo.InvariantCulture);
    typeof(ArenaApp).GetField("beatFrozen", f).SetValue(app, true);
    typeof(ArenaApp).GetField("beatPulse", f).SetValue(app, pulse);
    var bands = (float[])typeof(ArenaApp).GetField("beatBands", f).GetValue(app);
    for (int i = 0; i < bands.Length; i++)
      bands[i] = Math.Min(1f, (0.55f + 0.45f * pulse) * (float)(0.95 * Math.Exp(-i / 6.0) + 0.45 * Math.Exp(-Math.Pow((i - 11) / 3.5, 2)) + 0.18 + 0.12 * Math.Sin(i * 2.3)));
    var waves = (float[])typeof(ArenaApp).GetField("beatWaves", f).GetValue(app);
    float now = UnityEngine.Time.unscaledTime;
    waves[0] = now - 0.15f; waves[1] = now - 0.5f; waves[2] = now - 0.85f;
  }

  static void DrawMode() {
    if (TankSheet()) return;  // TANKSHEET=1 (ShotTankLook.cs)
    if (mode == "hud") {
      // TILEFX=moment,seconds: every ability tile that long into a reaction (HudTiles.cs): ready, charge, use or deny
      // (deny also puts the 1st ability on cooldown, so its timer shows). The stub ignores GUI.matrix: no pop or shake, the rest shows.
      var tilefx = Environment.GetEnvironmentVariable("TILEFX");
      if (tilefx != null)
      {
        var tp = tilefx.Split(',');
        if (tp[0] == "deny") { var sl = host.Game.Me.Slots[0]; sl.Charges = 0; sl.Timer = 2.4f; }
        typeof(ArenaApp).GetMethod("PreviewTileMoment", f2).Invoke(app, new object[] { tp[0], tp.Length > 1 ? float.Parse(tp[1]) : 0.05f });
      }
      // MXFX=gain|level,<s>,<xp>[,slot]: the slot (default 1, the 2nd ability) that far into an XP gain or a level-up (HudMastery.cs; the floor cleared)
      var mxfx = Environment.GetEnvironmentVariable("MXFX");
      if (mxfx != null) { var mp = mxfx.Split(','); typeof(ArenaApp).GetMethod("PreviewMasteryMoment", f2).Invoke(app, new object[] { mp[0], mp.Length > 1 ? float.Parse(mp[1]) : 0.1f, mp.Length > 2 ? float.Parse(mp[2]) : 30f, mp.Length > 3 ? int.Parse(mp[3]) : 1 }); }
      // EMOTEBAR=1: the emote bar open (rank 60); EMOTE=n[,age]: emote number n showing over player 1, that many seconds in (EmoteBar.cs)
      if (Environment.GetEnvironmentVariable("EMOTEBAR") != null) { Profile.Current.Xp = Rank.XpAt(60); set("emoteBarOpen", true); set("emoteBarK", 1f); }
      if (Environment.GetEnvironmentVariable("EMOTE") != null) { var ep = Environment.GetEnvironmentVariable("EMOTE").Split(','); host.Emotes[1] = new Session.EmoteShow { Index = byte.Parse(ep[0]), At = Session.Clock - (ep.Length > 1 ? float.Parse(ep[1]) : 0.8f) }; }
      bool zoomGl = Environment.GetEnvironmentVariable("ZOOM") != null || Environment.GetEnvironmentVariable("LAG") != null || Environment.GetEnvironmentVariable("SURVIVAL") != null;  // (the off-screen markers and the lag chart's lines draw with GL on the HUD; OffscreenMarkers.cs, LagChartUi.cs)
      // MAPVIEW=<from>,<to>,<t>: the Map key's switch (0 minimap, 1 full map, 2 off) that far through (MapView.cs); use with EXPANDMAP
      // MAPCYCLE=<s>: that far through a scripted run of the Map key (minimap, M, full map, M, off, M, minimap), for clip.sh MAPCYCLE=0:3.6:0.05
      if (Environment.GetEnvironmentVariable("MAPCYCLE") != null) {
        float mc = float.Parse(Environment.GetEnvironmentVariable("MAPCYCLE"), System.Globalization.CultureInfo.InvariantCulture);
        float[] at = { 0.5f, 1.9f, 2.8f };  // when each press lands
        int seg = mc < at[0] ? -1 : mc < at[1] ? 0 : mc < at[2] ? 1 : 2;
        int[] fr = { 0, 1, 2 }, to = { 1, 2, 0 };
        if (seg < 0) typeof(ArenaApp).GetMethod("PreviewMapView", f2).Invoke(app, new object[] { 2, 0, 1f });
        else typeof(ArenaApp).GetMethod("PreviewMapView", f2).Invoke(app, new object[] { fr[seg], to[seg], Math.Min(1f, (mc - at[seg]) / 0.42f) });
        if (seg == 1) set("mapOffAt", Time.unscaledTime - (mc - at[1]));  // (the "Map off" note's own clock)
      }
      if (Environment.GetEnvironmentVariable("MAPSTYLE") != null) ArenaApp.MapStyle = int.Parse(Environment.GetEnvironmentVariable("MAPSTYLE"));  // map style mockups (MapStyleLook.cs)
      if (Environment.GetEnvironmentVariable("MAPVIEW") != null) { var mv = Environment.GetEnvironmentVariable("MAPVIEW").Split(','); typeof(ArenaApp).GetMethod("PreviewMapView", f2).Invoke(app, new object[] { int.Parse(mv[0]), int.Parse(mv[1]), float.Parse(mv[2], System.Globalization.CultureInfo.InvariantCulture) }); }
      if (zoomGl) { Event.current.type = EventType.Repaint; set("glMaterial", new Material(null)); GL.ToUi = true; }
      typeof(ArenaApp).GetMethod("DrawHud", f2).Invoke(app, null);
      if (zoomGl) GL.ToUi = false;
      if (recallAt >= 0f) typeof(ArenaApp).GetMethod("DrawRecallFade", f2).Invoke(app, null);
      if (launchAt >= 0f) { typeof(ArenaApp).GetMethod("DrawLaunchWave", f2).Invoke(app, null); typeof(ArenaApp).GetMethod("DrawLobbyLeaving", f2).Invoke(app, null); }
    }
    else if (mode == "gameover" || mode == "victory")
    {
      // The end of a run (VICTORY=1 or mode victory: a win): sample numbers for each player.
      var gg = host.Game; gg.State = mode == "victory" ? MatchState.Victory : MatchState.GameOver; gg.RunStatsReady = true; gg.RunTime = 286f; gg.Score = 546;
      // RETRY=1: a life left: the Respawn / Game over choice instead of the results (Checkpoints.cs, LivesUi.cs).
      if (Environment.GetEnvironmentVariable("RETRY") != null) { gg.RetryReady = true; gg.RetryWave = 11; gg.LivesUsed = 0; gg.Hunt = null; gg.Quick = false; gg.Unrecorded = false; }
      int k = 0;
      foreach (var pl in gg.Players.Values)
      {
        pl.Run.DamageDealt = k == 0 ? 11900f : 8400f; pl.Run.Kills = k == 0 ? 100 : 64; pl.Run.EliteKills = 3; pl.Run.BiggestHit = k == 0 ? 15f : 42f;
        pl.Run.ShotsFired = 500; pl.Run.ShotsHit = k == 0 ? 205 : 260; pl.Run.HealingDone = k == 0 ? 0f : 640f; pl.Run.Revives = k; pl.Run.DamageTaken = k == 0 ? 246f : 180f;
        pl.Run.Downs = 1 - k; pl.Run.AbilitiesUsed = 9 + 4 * k; pl.Level = 5 + k; k++;
      }
      // The rank strip: the profile that PreviewSaves makes is rank 8; the run's gain is worked out from where it started.
      PreviewSaves();
      var before = new Profile { Xp = Rank.XpAt(Environment.GetEnvironmentVariable("RANKUP") != null ? 8 : 9) + (Environment.GetEnvironmentVariable("RANKUP") != null ? 900 : 300) };
      gg.LastGain = before.AwardXp(Rank.Award(mode == "victory", 10, 2, 1, RunMod.ToughCrowd | RunMod.Nightfall, DifficultyLevel.Normal), 11, mode == "victory" ? 1 : 0);
      Event.current.type = EventType.Repaint; set("glMaterial", new Material(null)); GL.ToUi = true;  // (the rank report draws a reward preview with GL)
      if (Environment.GetEnvironmentVariable("RANKPAGE") != null) { set("rankGainSeen", gg.LastGain); set("resultsRankPage", true); set("rankPageAt", float.Parse(Environment.GetEnvironmentVariable("RANKPAGE"), System.Globalization.CultureInfo.InvariantCulture)); }  // RANKPAGE=<start time>: the rank report, that many seconds in (-10: all of it)
      typeof(ArenaApp).GetMethod(gg.RetryReady ? "DrawLifePrompt" : "DrawRunSummary", f2).Invoke(app, null);
    }
    else if (mode == "huntmap")
    {
      // The hunt map screen (HuntMapUi.cs): HMSEED=<n> the map (default 5), HMSTEP=<n> rows cleared (default 4), HMVOTE=1 you have voted for
      // the first choice (P2 voted for the second, P3 for the first), HMLOCK=1 you have locked in. Use with 3 players: huntmap huntmap 6 12.2 3.
      var hmSeed = int.Parse(Environment.GetEnvironmentVariable("HMSEED") ?? "5");
      int hmLen = Environment.GetEnvironmentVariable("HMLONG") != null ? Hunts.LongLength : Hunts.Length;   // HMLONG=1: the 20-step map
      var hm = HuntMap.Generate(hmSeed, hmLen);
      int hmSteps = int.Parse(Environment.GetEnvironmentVariable("HMSTEP") ?? "4");
      for (int k = 0; k < hmSteps && !hm.Complete; k++) { var ch = hm.Choices(); hm.Pick(ch[(hmSeed + k) % ch.Length]); hm.Clear(); }
      var hg = host.Game; hg.HuntLength = hmLen; hg.Hunt = Hunts.All[0]; hg.HuntMapOn = true; hg.HuntRoute = hm; hg.HuntMapVoting = true; hg.HuntMapSeconds = 17f;
      int hmSeat = 0;
      foreach (var hp in hg.Players.Values)
      {
        if (hmSeat == 0 && Environment.GetEnvironmentVariable("HMVOTE") != null) hg.MapVotes[hp.Id] = 0;
        if (hmSeat == 1) { hg.MapVotes[hp.Id] = 1; hg.MapLocked.Add(hp.Id); }
        if (hmSeat == 2) { hg.MapVotes[hp.Id] = 0; hg.MapLocked.Add(hp.Id); }
        if (hmSeat == 0 && Environment.GetEnvironmentVariable("HMLOCK") != null) hg.MapLocked.Add(hp.Id);
        hmSeat++;
      }
      Event.current.type = EventType.Repaint; set("glMaterial", new Material(null)); GL.ToUi = true;
      typeof(ArenaApp).GetMethod("DrawHuntMap", f2).Invoke(app, new object[] { hm, Hunts.All[0], 0f });
      GL.ToUi = false;
    }
    else if (mode == "dev")
    {
      if (Environment.GetEnvironmentVariable("SNAPTOAST") != null) { set("snapToast", Environment.GetEnvironmentVariable("SNAPTOAST")); set("snapToastT", 2f); }  // the F6 / F7 line
      typeof(ArenaApp).GetMethod("DrawHud", f2).Invoke(app, null);
      host.Game.DevSetStat(1, Array.FindIndex(Game.StatFields, x => x.Name == "DmgMult"), 2.5f);
      host.Game.DevSetGod(1, true);
      if (Environment.GetEnvironmentVariable("DEVMENU") != null) host.Screen = AppScreen.Menu;  // the dock as opened from the main menu
      // DEVDRAWER=1..4 (wave, party, view, labs), DEVSIDE=1 (stat editor), DEVSHEET=1 (DPS), 3 (balance), 4 (upgrades), 5 (enemies), 6 (snapshots), 7 (gadgets and relics), DEVMENU=1 (outside a run)
      if (Environment.GetEnvironmentVariable("DEVSHEET") == "6")  // run snapshots (SnapshotUi.cs): three in the profile's folder; SNAPDEL=1 asks to delete the second, SNAPREN=1 renames the first
      {
        if (Profile.Folder == null) Profile.Folder = Path.Combine(Path.GetTempPath(), "arena-preview-snaps");
        if (Directory.Exists(RunSnapshot.Folder)) Directory.Delete(RunSnapshot.Folder, true);
        foreach (var n in new[] { "Wave 3, mid-wave", "Before the Warden", "Wave 9, between waves" })
        {
          RunSnapshot.SaveFile(host.Game, n);
          System.Threading.Thread.Sleep(5);
        }
        if (Environment.GetEnvironmentVariable("SNAPDEL") != null) set("snapDeletePath", RunSnapshot.List()[1].Path);
        if (Environment.GetEnvironmentVariable("SNAPREN") != null) { set("snapRenamePath", RunSnapshot.List()[0].Path); set("snapRenameText", "Wave 9, between waves"); }
      }
      if (Environment.GetEnvironmentVariable("DEVDRAWER") != null) set("devDrawer", int.Parse(Environment.GetEnvironmentVariable("DEVDRAWER")));
      if (Environment.GetEnvironmentVariable("DEVSIDE") != null) set("devSide", true);
      if (Environment.GetEnvironmentVariable("DEVSHEET") != null) set("devSheet", int.Parse(Environment.GetEnvironmentVariable("DEVSHEET")));
      if (Environment.GetEnvironmentVariable("SPAWNERMODE") != null) set("devSpawnerMode", true);  // the Spawn-an-enemy sheet's Spawner row on (DEVSHEET=5)
      if (Environment.GetEnvironmentVariable("FILTER") != null) set("devFilter", Environment.GetEnvironmentVariable("FILTER"));
      if (Environment.GetEnvironmentVariable("UPRARITY") != null) set("devUpRarity", int.Parse(Environment.GetEnvironmentVariable("UPRARITY")));  // the Spawn-an-upgrade sheet (DEVSHEET=4): 0 common ... 6 milestone, UPSCROLL=px
      if (Environment.GetEnvironmentVariable("UPSCROLL") != null) set("devUpScroll", float.Parse(Environment.GetEnvironmentVariable("UPSCROLL")));
      Event.current.type = EventType.Repaint; set("glMaterial", new Material(null)); GL.ToUi = true;
      typeof(ArenaApp).GetMethod("DrawDevMenu", f2).Invoke(app, null);
      GL.ToUi = false;
    }
    else if (mode == "pause")
    {
      set("sounds", System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(SoundBank)));
      typeof(ArenaApp).GetMethod("DrawHud", f2).Invoke(app, null);
      // The CRT cabinet (CrtWindows.cs). PAUSEAGE=<s>: that far into the opening (typing, power-on, scan bar; default settled);
      // PAUSEANIM=0..1: how far the cabinet has slid in (0.3 s) or, with PAUSECLOSE, out. PAUSESET=<0-3>: Settings with that section's
      // window out (PAUSESET=list: the four sections, no window); PAUSEHOW=<topic>: How to play's window beside the list;
      // PAUSERECORDS=1: the profile's cabinet; PAUSECONFIRM=1: Quit's question; PAUSEWIN=<s>: that long since its window began to slide out.
      set("pauseMenuOpen", true); if (Environment.GetEnvironmentVariable("REDUCED") != null) set("fxReduced", true);
      set("cabInstant", true);
      var inv = System.Globalization.CultureInfo.InvariantCulture;
      float pAnim = float.Parse(Environment.GetEnvironmentVariable("PAUSEANIM") ?? "1", inv);
      float pAge = Environment.GetEnvironmentVariable("PAUSEAGE") != null ? float.Parse(Environment.GetEnvironmentVariable("PAUSEAGE"), inv) : Environment.GetEnvironmentVariable("PAUSEANIM") != null ? pAnim * 0.3f : 10f;
      set("pauseOpenedAt", Time.unscaledTime - pAge);
      set("drawerKey", "pause");
      float pWin = Environment.GetEnvironmentVariable("PAUSEWIN") != null ? float.Parse(Environment.GetEnvironmentVariable("PAUSEWIN"), inv) : 10f;
      if (Environment.GetEnvironmentVariable("PAUSECONFIRM") != null) set("pauseConfirmLobby", true);  // Leave to lobby's "are you sure" (MenuDrawer.cs)
      if (Environment.GetEnvironmentVariable("PAUSERECORDS") != null) { set("pauseRecords", true); set("drawerKey", "pause-profile"); set("profileTab", Environment.GetEnvironmentVariable("TAB") != null ? int.Parse(Environment.GetEnvironmentVariable("TAB")) : 2); PreviewSaves(); if (Environment.GetEnvironmentVariable("SECTION") != null) { int sc = int.Parse(Environment.GetEnvironmentVariable("SECTION")); set("rankSection", sc); set("recordsSection", sc); set("unlocksSection", sc); } if (Environment.GetEnvironmentVariable("NOWIN") == null) { typeof(ArenaApp).GetMethod("OpenCabWindow", f2).Invoke(app, new object[] { 3 }); set("cabWinInAt", Time.unscaledTime - pWin); } }
      if (Environment.GetEnvironmentVariable("PAUSESET") != null) {
        set("pauseSettings", true); string ps = Environment.GetEnvironmentVariable("PAUSESET");
        if (ps != "list") { set("settingsGroup", int.Parse(ps)); typeof(ArenaApp).GetMethod("OpenCabWindow", f2).Invoke(app, new object[] { 1 }); set("cabWinInAt", Time.unscaledTime - pWin); }
      }
      if (Environment.GetEnvironmentVariable("PAUSEHOW") != null) { set("pauseHowTo", true); set("howToTopic", int.Parse(Environment.GetEnvironmentVariable("PAUSEHOW"))); typeof(ArenaApp).GetMethod("OpenCabWindow", f2).Invoke(app, new object[] { 2 }); set("cabWinInAt", Time.unscaledTime - pWin); }
      set("pauseAnim", Environment.GetEnvironmentVariable("PAUSECLOSE") != null || pAge < 0.3f ? pAnim : 1f);
      host.Screen = Environment.GetEnvironmentVariable("PAUSECLOSE") != null ? AppScreen.Menu : AppScreen.Game;  // in a run (PAUSECLOSE: closing, at PAUSEANIM)
      Event.current.type = EventType.Repaint; set("glMaterial", new Material(null)); GL.ToUi = true;
      // PAUSEMORPH=<s>: that long after the list switched from the pause menu's rows to Settings' (the rows rewrite, CrtKit.Morph)
      if (Environment.GetEnvironmentVariable("PAUSEMORPH") != null) {
        set("pauseSettings", false);
        typeof(ArenaApp).GetMethod("DrawPauseMenu", f2).Invoke(app, null); Time.unscaledTime += 2f;  // (its rows type in, then settle)
        typeof(ArenaApp).GetMethod("DrawPauseMenu", f2).Invoke(app, null);
        set("pauseSettings", true); typeof(ArenaApp).GetMethod("DrawPauseMenu", f2).Invoke(app, null);  // (the switch: the rows start rewriting)
        Time.unscaledTime += float.Parse(Environment.GetEnvironmentVariable("PAUSEMORPH"), inv); GUI.Log.Clear(); GL.Tris.Clear();
      }
      typeof(ArenaApp).GetMethod("DrawPauseMenu", f2).Invoke(app, null);
      GL.ToUi = false;
    }
    else if (mode == "bg")
    {
      GL.Tris.Clear();
      set("drawScale", 1f);
      set("bgProfile", Profile.Current);
      set("menuBg", int.Parse(Environment.GetEnvironmentVariable("BG")));
      set("menuBgOverride", true);  // (the main menu would show the Holo Floor otherwise)
      if (Environment.GetEnvironmentVariable("MENUHUE") != null) { set("menuBgHueLoaded", true); set("menuBgHue", float.Parse(Environment.GetEnvironmentVariable("MENUHUE"))); }  // MENUHUE=0-1: the Holo Floor turned to that hue (MenuBgHue.cs)
      FakeBeat(app);  // BEAT=<pulse 0-1>: the Holo Floor mid-song (MenuBeat.cs)
      // POINTER=x,y (the pointer, in 1280x720 canvas coordinates), PCLICK=x,y,age (a click that long ago), PENERGY=0-1: for the reactive ones.
      var ptr = Environment.GetEnvironmentVariable("POINTER");
      if (ptr != null)
      {
        var pp = ptr.Split(',');
        set("bgReactFrame", UnityEngine.Time.frameCount);
        set("bgPointerUi", new V2(float.Parse(pp[0]), float.Parse(pp[1])));
        ((Game)typeof(ArenaApp).GetField("game", f2).GetValue(app)).Running = false;  // the menus: the reactions are on
        set("bgEnergy", Environment.GetEnvironmentVariable("PENERGY") != null ? float.Parse(Environment.GetEnvironmentVariable("PENERGY")) : 0.5f);
        var pc = Environment.GetEnvironmentVariable("PCLICK");
        if (pc != null)
        {
          var cc = pc.Split(',');
          var rip = (Array)typeof(ArenaApp).GetField("bgRipples", f2).GetValue(app);
          var rt = rip.GetType().GetElementType();
          var one = Activator.CreateInstance(rt);
          rt.GetField("At").SetValue(one, new V2(float.Parse(cc[0]), float.Parse(cc[1])));
          rt.GetField("Born").SetValue(one, 7.3f - float.Parse(cc[2]));
          rt.GetField("Power").SetValue(one, 1f);
          rip.SetValue(one, 0);
        }
      }
      typeof(ArenaApp).GetMethod("DrawMenuBackground", f2).Invoke(app, new object[] { 7.3f });
    }
    else if (mode == "lobby")
    {
      GL.Tris.Clear();
      set("drawScale", 1f);
      typeof(ArenaApp).GetMethod("DrawMenuBackground", f2).Invoke(app, new object[] { 3.7f });
      host.Lobby.Clear();
      host.Lobby[1] = new LobbyEntry { Id = 1, Name = "Anthony", Role = Role.Tank, Kit = Kit.Knight, Weapon = 1, Ability = 2, Ready = true, Rank = 8, Frame = 1 };
      host.Lobby[2] = new LobbyEntry { Id = 2, Name = "Aleon", Role = Role.Dps, Kit = Kit.Elementalist, Weapon = 0, Ability = 1, Rank = 27, Frame = Rank.FrameIndex("frame-gold") };
      // FRAME=frame-prism: your party card wears that frame (and AFRAME for Aleon's), e.g. frame-circuit, frame-ember, frame-void, frame-foil.
      if (Environment.GetEnvironmentVariable("FRAME") != null) host.Lobby[1].Frame = Rank.FrameIndex(Environment.GetEnvironmentVariable("FRAME"));
      if (Environment.GetEnvironmentVariable("CARD") != null) { var ids = Environment.GetEnvironmentVariable("CARD").Split(','); host.Lobby[1].CardBg = Rank.CardBgIndex(ids[0]); if (ids.Length > 1) host.Lobby[2].CardBg = Rank.CardBgIndex(ids[1]); }  // CARD=card-aurora,card-foil
      if (Environment.GetEnvironmentVariable("NAMEFX") != null) { var ids = Environment.GetEnvironmentVariable("NAMEFX").Split(','); host.Lobby[1].NameFx = Rank.NameFxIndex(ids[0]); if (ids.Length > 1) host.Lobby[2].NameFx = Rank.NameFxIndex(ids[1]); }  // NAMEFX=name-glitchy,name-gold
      if (Environment.GetEnvironmentVariable("AFRAME") != null) host.Lobby[2].Frame = Rank.FrameIndex(Environment.GetEnvironmentVariable("AFRAME"));
      // TANKGUN=0|1|2: your card is a Soldier Tank with that weapon (0 Scattergun, 1 Flak Cannon, 2 Riot Repeater).
      if (Environment.GetEnvironmentVariable("TANKGUN") != null) { host.Lobby[1].Role = Role.Tank; host.Lobby[1].Kit = Kit.Soldier; host.Lobby[1].Weapon = byte.Parse(Environment.GetEnvironmentVariable("TANKGUN")); host.Lobby[1].Ability = 0; }
      // DPSGUN=0..5: your card is a Soldier DPS with that weapon pick (3 the Double Barrel on the 4th key, 4 the Twin Rifle Auto, 5 Out of Ammo).
      if (Environment.GetEnvironmentVariable("DPSGUN") != null) { host.Lobby[1].Role = Role.Dps; host.Lobby[1].Kit = Kit.Soldier; host.Lobby[1].Weapon = byte.Parse(Environment.GetEnvironmentVariable("DPSGUN")); host.Lobby[1].Ability = 0; }
      // ELEMGUN=0..5: your card is an Elementalist Tank with that weapon pick (0 the Flamethrower, 4 the Water Gun, 5 the Gale Gun).
      if (Environment.GetEnvironmentVariable("ELEMGUN") != null) { host.Lobby[1].Role = Role.Tank; host.Lobby[1].Kit = Kit.Elementalist; host.Lobby[1].Weapon = byte.Parse(Environment.GetEnvironmentVariable("ELEMGUN")); host.Lobby[1].Ability = 0; }
      if (Environment.GetEnvironmentVariable("BLOODGUN") != null) { host.Lobby[1].Role = Role.Dps; host.Lobby[1].Kit = Kit.Hemomancer; host.Lobby[1].Weapon = byte.Parse(Environment.GetEnvironmentVariable("BLOODGUN")); host.Lobby[1].Ability = 0; }  // BLOODGUN=<n>: the Hemomancer DPS with weapon n (2 the Blood Brawler's bleeding hand)
      // LOBBYULT=0|1: your card is an Elementalist Tank with that ultimate picked (1 Lightning Rod), its detail box open (DETAIL=2 is implied).
      if (Environment.GetEnvironmentVariable("LOBBYULT") != null) { host.Lobby[1].Role = Role.Tank; host.Lobby[1].Kit = Kit.Elementalist; host.Lobby[1].Weapon = 0; host.Lobby[1].Ability = 0; host.Lobby[1].Ult = byte.Parse(Environment.GetEnvironmentVariable("LOBBYULT")); set("detailSel", 2); }
      if (Environment.GetEnvironmentVariable("WILDCARD") != null) { host.Lobby[1].Role = Role.Wildcard; host.Lobby[1].Kit = Kit.Soldier; host.Lobby[1].Weapon = (byte)int.Parse(Environment.GetEnvironmentVariable("WILDWEAPON") ?? "9");  // WILDWEAPON=<pick>: 18 the Arc Knuckles
       host.Lobby[1].Ability = 12; host.Lobby[1].Wild = Defs.MakeWild(14); host.Lobby[1].Ult = (byte)Upgrades.WildUlt(Kit.Ninja, 0); }  // WILDCARD=1: Twin Blades, Shadow Step, Blade Fan as the Wildcard ability on the 2nd slot, Death Lotus
      if (Environment.GetEnvironmentVariable("SOLO") != null) host.Lobby.Remove(2);
      // VARIANTS=1: your weapon's variants window open with 25 Cores and tier I bought (VARIANTS=0: none bought, 5 Cores).
      if (Environment.GetEnvironmentVariable("VARIANTS") != null) {
        bool one = Environment.GetEnvironmentVariable("VARIANTS") == "1";
        Profile.Current.Cores = one ? 25 : 5; Profile.Current.VariantTiers.Clear();
        if (one) Profile.Current.VariantTiers[Defs.Weapon(Role.Tank, Kit.Knight, 1).Name] = 1;
        set("ascOpen", int.Parse(Environment.GetEnvironmentVariable("VARSLOT") ?? "1"));  // VARSLOT=<n>: which weapon key's window
      }
      // BOTS=1: Aleon's seat is a bot (the host's skill, MIX and remove keys on its card); BOTS=edit: the host is also choosing that bot's loadout (its name box over the preview).
      if (Environment.GetEnvironmentVariable("BOTS") != null) {
        host.Lobby[2] = new LobbyEntry { Id = 2, Name = "Bot 1", Role = Role.Healer, Kit = Kit.Hemomancer, Weapon = 0, Ability = 0, Ready = true, Rank = 3 };
        var pbs = (PartyBots)typeof(ArenaApp).GetField("partyBots", f2).GetValue(app);
        var bs = new Session(); bs.MyId = 2; bs.Lobby[2] = host.Lobby[2];
        pbs.Bots.Add(new PartyBots.PartyBot { S = bs, Skill = BotSkill.Good, Name = "Bot 1" });
        if (Environment.GetEnvironmentVariable("BOTS") == "edit") set("editBotId", 2);
      }
      // HOLOT=<s>: the lobby hover tips (the hologram look, TipHolo.cs) that far into their animation; HOLO=0 shows the older soft glass. Use with MOUSE=x,y.
      if (Environment.GetEnvironmentVariable("HOLO") == "0") set("holoTips", false);
      set("holoClock", float.Parse(Environment.GetEnvironmentVariable("HOLOT") ?? "1.3"));
      // RESUME=1: the lobby continuing a saved run (the host in its seat, Aleon's seat still free; SavesUi.cs).
      if (Environment.GetEnvironmentVariable("RESUME") != null) {
        PreviewSaves(); host.Resume = SaveStore.List()[0];
        host.Lobby.Remove(2); host.Lobby[1].Seat = 0; host.Lobby[1].Role = Role.Tank; host.Lobby[1].Kit = Kit.Knight; host.Lobby[1].Weapon = 1; host.Lobby[1].Ability = 2; host.Lobby[1].Ready = false;
      }
      // PERKS=1: the Warden Hunt won with Riot Gear equipped (PERKS=2 also has a first run win, for the second slot), and the seats show their perks; LOBBYTAB=5 for the Perks tab.
      if (Environment.GetEnvironmentVariable("PERKS") != null) {
        var pp = host.Game.ProfileOverride = new Profile(); pp.BossBeaten(EnemyType.Warden, 5); pp.HuntEnded(EnemyType.Warden, true, 300f);
        if (Environment.GetEnvironmentVariable("PERKS") == "2" || Environment.GetEnvironmentVariable("PERKS") == "3") pp.RunsWon = 1;
        if (Environment.GetEnvironmentVariable("PERKS") == "3") pp.HuntEnded(EnemyType.Warden, true, 280f, true);  // PERKS=3: also won on Hard, so Contraband is unlocked
        Perks.Equip(pp, PerkId.RiotGear);
        host.Lobby[1].PerkIds = new[] { PerkId.RiotGear }; if (host.Lobby.ContainsKey(2)) host.Lobby[2].PerkIds = new[] { PerkId.RiotGear };
      }
      if (Environment.GetEnvironmentVariable("QUICKMET") != null) {
        // The Quick Play tab with a played profile: QUICKMET=<bosses beaten, in the Campaign's order> (QuickPlayUi.cs).
        var qp = new Profile { QuickPlays = 4, QuickWins = 3, QuickBestWave = 23, QuickBosses = 5, TutorialDone = true };
        int qn = int.Parse(Environment.GetEnvironmentVariable("QUICKMET"));
        for (int i = 0; i < qn; i++) qp.BossBeaten(BossSchedule.Campaign.BossAt((i + 1) * 5), 0);
        host.Game.ProfileOverride = qp;
      }
      // CAMPAIGN=<bosses beaten>: Normal mode's Campaign tab with a played profile (CampaignUi.cs).
      if (Environment.GetEnvironmentVariable("CAMPAIGN") != null) {
        host.Mode = GameMode.Campaign; set("lobbyTab", 10);
        int cn = int.Parse(Environment.GetEnvironmentVariable("CAMPAIGN"));
        var cp = new Profile { TutorialDone = true, RunsStarted = 14, RunsWon = 2, RunsLost = 11, BestWave = cn * 10 + 4, BestEndlessWave = cn >= 10 ? 112 : 0, BestScore = 482310, Kills = 18420, BossKills = 31, Downs = 87, Revives = 52 };
        for (int i = 0; i < cn; i++) cp.BossBeaten(BossSchedule.Campaign.BossAt((i + 1) * 5), (i + 1) * 10);
        host.Game.ProfileOverride = cp;
      }
      if (Environment.GetEnvironmentVariable("SURVLOBBY") != null) { host.Mode = GameMode.Survival; set("lobbyTab", 9); if (Environment.GetEnvironmentVariable("SURVLOBBY") == "2") { var sp2 = new Profile { TutorialDone = true }; sp2.SurvivalEnded("Knight Tank", 512f, 3); sp2.SurvivalEnded("Elementalist DPS", 845f, 4); sp2.SurvivalEnded("Knight Tank", 301f, 2); host.Game.ProfileOverride = sp2; } }  // SURVLOBBY=1: the lobby's Survival mode and tab (SurvivalUi.cs); 2: with some records
      if (Environment.GetEnvironmentVariable("LOBBYTAB") != null) { set("lobbyTab", int.Parse(Environment.GetEnvironmentVariable("LOBBYTAB"))); host.Modifiers = RunMod.GlassCannon | RunMod.Frenzy | RunMod.Lucky | RunMod.Hunted | RunMod.SecondWind; }
      // TRAINING=1: the lobby's Training mode and its page (TrainingUi.cs); TRAINING=2: with drill 1 built and a gold on it.
      if (Environment.GetEnvironmentVariable("TRAINING") != null) { host.Mode = GameMode.Training; set("lobbyTab", 13); if (Environment.GetEnvironmentVariable("TRAINING") == "2") { Training.Drills[0].Built = true; var tp = new Profile { TutorialDone = true }; tp.DrillDone("move", 13.4f); host.Game.ProfileOverride = tp; } }
      // MODEBLINK=<s>: that long into the CRT blink back to the lobby from the Game Mode screen (LOBBYTAB=8 for its page; ModeBlink.cs).
      if (Environment.GetEnvironmentVariable("MODEBLINK") != null) { set("modeBlinkFrom", Environment.GetEnvironmentVariable("LOBBYTAB") != null ? int.Parse(Environment.GetEnvironmentVariable("LOBBYTAB")) : 8); set("lobbyTab", 0); set("modeBlinkT", float.Parse(Environment.GetEnvironmentVariable("MODEBLINK"), System.Globalization.CultureInfo.InvariantCulture)); }
      // PRACTICE=1: the lobby's Practice window (HangoutUi.cs): the room up behind the lobby, everyone in it, two spawners on.
      // PRACTICEFX=<s>: that far into the Loadout -> Practice glitch (0..0.26 the loadout out, 0.26..0.6 Practice in); PRACTICEBACK=1 the other way.
      if (Environment.GetEnvironmentVariable("PRACTICE") != null || Environment.GetEnvironmentVariable("PRACTICEFX") != null) {
        host.Lobby[3] = new LobbyEntry { Id = 3, Name = "Sam", Role = Role.Healer, Kit = Kit.Summoner, Weapon = 0, Ability = 0, Rank = 14, Frame = 1 };
        host.MyId = 1;
        host.Game.HangoutStart = true; host.Game.HostStart(); host.Game.HangoutStart = false;
        host.Screen = AppScreen.Lobby;
        var pg = host.Game;
        foreach (int k in new[] { 0, 4 }) pg.Spawners[k].On = true;
        var at = new[] { new V2(760f, 470f), new V2(900f, 400f), new V2(640f, 360f) };
        int pi = 0;
        foreach (var pp in pg.Players.Values) { pp.Pos = pp.Disp = at[pi % 3]; pp.Aim = pi == 0 ? -0.6f : 2.6f; pi++; }
        pg.Players[2].Hp = pg.Players[2].MaxHp * 0.55f;
        set("lobbyTab", 12); set("practiceLastTab", 12);
        if (Environment.GetEnvironmentVariable("PRACTICEFX") != null) {
          Time.unscaledTime = 20f;
          bool back = Environment.GetEnvironmentVariable("PRACTICEBACK") != null;
          set("practiceFxFrom", back ? 12 : 0); set("practiceFxTo", back ? 0 : 12); set("lobbyTab", back ? 0 : 12); set("practiceLastTab", back ? 0 : 12);
          set("practiceFxAt", 20f - float.Parse(Environment.GetEnvironmentVariable("PRACTICEFX"), System.Globalization.CultureInfo.InvariantCulture));
        }
      }
      // HANGOUT=<trail,trail,trail>: the lobby's Hangout tab (LobbyRoom.cs) with three characters, each wearing that trail id and
      // mid-stride (so the trail shows), e.g. HANGOUT=trail-void,trail-prism,trail-frost.
      if (Environment.GetEnvironmentVariable("HANGOUT") != null) {
        var tr = Environment.GetEnvironmentVariable("HANGOUT").Split(',');
        set("lobbyTab", 6);
        // HANGOUTANIM=<0..1>: freeze the Loadout -> Hangout transition part-way (0.5 = the window half grown); the Loadout tab is the start.
        if (Environment.GetEnvironmentVariable("HANGOUTANIM") != null) { set("lobbyTab", 6); set("hangoutLastTab", 6); set("hangoutAnim", float.Parse(Environment.GetEnvironmentVariable("HANGOUTANIM"))); }
        host.Lobby[3] = new LobbyEntry { Id = 3, Name = "Sam", Role = Role.Healer, Kit = Kit.Summoner, Weapon = 0, Ability = 0, Rank = 14, Frame = 1 };
        Time.unscaledTime = 10f;
        var tpType = typeof(ArenaApp).GetNestedType("TrailPoint", BindingFlags.NonPublic);
        var rt = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<object>>();
        var rtField = typeof(ArenaApp).GetField("roomTrails", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(app);
        var spots = new[] { new V2(250f, 300f), new V2(520f, 210f), new V2(380f, 400f) };
        int n = 0;
        foreach (var le in host.Lobby.Values) {
          if (n < tr.Length) le.Trail = Rank.TrailIndex(tr[n]);
          var pos = spots[n]; var av = new Avatar { Pos = pos, Disp = pos, Aim = n == 1 ? 3.14f : 0.2f - n * 0.4f }; host.Avatars[le.Id] = av;
          var listType = rtField.GetType(); var list = Activator.CreateInstance(listType.GetGenericArguments()[1]);
          for (int i = 24; i >= 0; i--) {
            var tp = Activator.CreateInstance(tpType);
            float dir = n == 1 ? 1f : -1f;
            tpType.GetField("At").SetValue(tp, new V2(pos.X + dir * i * 7f, pos.Y + (float)Math.Sin(i * 0.35f + n) * 9f));
            tpType.GetField("T").SetValue(tp, 10f - i * 0.036f);
            list.GetType().GetMethod("Add").Invoke(list, new object[] { tp });
          }
          if (le.Trail != 0) listType.GetMethod("set_Item").Invoke(rtField, new object[] { le.Id, list });
          n++;
        }
        // A walk for the Light Cycle (it keeps corners, not the points above): right, then down, then right again, ending at the spots.
        if (Array.IndexOf(tr, "trail-lightcycle") >= 0) {
          var tick = typeof(ArenaApp).GetMethod("TickRoomTrails", BindingFlags.NonPublic | BindingFlags.Instance);
          for (int f = 0; f <= 150; f++) {
            float tt = 10f - (150 - f) * 0.016f, u = f / 150f;
            int ni = 0;
            foreach (var le in host.Lobby.Values) {
              var spot = spots[ni++]; var av2 = host.Avatars[le.Id];
              V2 pp = u < 0.45f ? new V2(spot.X - 220f + 220f * (u / 0.45f) / 1f, spot.Y - 90f) : u < 0.75f ? new V2(spot.X, spot.Y - 90f + 90f * ((u - 0.45f) / 0.3f)) : new V2(spot.X + 150f * ((u - 0.75f) / 0.25f), spot.Y);
              av2.Pos = av2.Disp = pp;
            }
            tick.Invoke(app, new object[] { tt, 0.016f });
          }
        }
        host.MyId = 1;
      }
      // DIFF=Hard (a preset) or DIFF=Custom (Hard with a few numbers changed): the lobby's Difficulty tab (LOBBYTAB=3).
      if (Environment.GetEnvironmentVariable("DIFF") != null) { var dl = (DifficultyLevel)Enum.Parse(typeof(DifficultyLevel), Environment.GetEnvironmentVariable("DIFF")); host.Diff = Difficulty.Of(dl == DifficultyLevel.Custom ? DifficultyLevel.Hard : dl); if (dl == DifficultyLevel.Custom) { host.Diff.Set(DiffKnob.EnemyDamage, 1.8f); host.Diff.Set(DiffKnob.Regen, 0.5f); host.Diff.Set(DiffKnob.ReviveTime, 2.5f); } }
      // HUNT=<n>: Boss Hunt mode, its tab (the Warden's hunt open, won once), the nth boss on top (n > 1: a never-met "???" boss).
      if (Environment.GetEnvironmentVariable("HUNT") != null) { host.Mode = GameMode.Hunt; var hpr = host.Game.ProfileOverride = new Profile(); hpr.BossBeaten(EnemyType.Warden, 5); hpr.HuntEnded(EnemyType.Warden, true, 312f); hpr.HuntEnded(EnemyType.Warden, false, 200f);
        int hn; if (int.TryParse(Environment.GetEnvironmentVariable("HUNT"), out hn) && hn > 1) host.HuntBoss = BossSchedule.Campaign.BossAt(Math.Min(hn, 10) * 5);
        if (Environment.GetEnvironmentVariable("HUNTMAP") != null) host.HuntMapMode = true; }  // HUNTMAP=1: the Boss Hunt tab with the hunt map switch on
      if (Environment.GetEnvironmentVariable("CLIENT") != null) host.MyId = 2;
      if (Environment.GetEnvironmentVariable("LOBBYSETTINGS") != null) { set("settingsOpen", true); set("sounds", System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(SoundBank))); }
      // FAVS=1: the Saved loadouts drawer open with four favourites (FavoritesUi.cs); FAVS=name: naming a fifth; FAVS=sure: deleting the second; FAVS=rename: renaming the second; FAVS=key: closed, the gold Saved star showing.
      var favs = Environment.GetEnvironmentVariable("FAVS");
      if (favs != null) {
        var fp = host.Game.ProfileOverride ?? (host.Game.ProfileOverride = new Profile());
        var seats = new[] { new LobbyEntry { Role = Role.Tank, Kit = Kit.Knight, Weapon = 1, Ability = 2 }, new LobbyEntry { Role = Role.Dps, Kit = Kit.Elementalist, Weapon = 0, Ability = 1 },
          new LobbyEntry { Role = Role.Healer, Kit = Kit.Hemomancer, Weapon = 0, Ability = 0 }, new LobbyEntry { Role = Role.Dps, Kit = Kit.Soldier, Weapon = 3, Ability = 0 } };
        var names = new[] { "Lance Wall", "Storm Caller", "Blood Medic", "Double Tap" };
        for (int i = 0; i < seats.Length; i++) Favorites.Save(fp, names[i], seats[i]);
        if (favs != "key") set("favOpen", true);
        if (favs == "name") { set("favNaming", true); set("favNameText", "Knight Tank"); }
        if (favs == "sure") set("favSure", 1);
        if (favs == "rename") { set("favRenaming", 1); set("favRenameText", "Storm Caller"); }
      }
      var hv = Environment.GetEnvironmentVariable("MOUSE");
      if (hv != null) { var xy = hv.Split(','); Event.current.mousePosition = new Vector2(float.Parse(xy[0]), float.Parse(xy[1])); }
      Event.current.type = EventType.Repaint;
      set("glMaterial", new Material(null));
      set("addressList", "100.123.48.122, 192.168.1.163");
      if (Environment.GetEnvironmentVariable("INFO") != null) set("connInfoOpen", true);
      if (Environment.GetEnvironmentVariable("VGEDIT") != null) { set("vgEditOpen", true); if (Environment.GetEnvironmentVariable("VGEDIT") != "1") { set("vgSelSt", 0); set("vgSelIdx", int.Parse(Environment.GetEnvironmentVariable("VGEDIT"))); } }  // VGEDIT=1: the Vowguard's chain editor open (LOADOUT=1,3,3,0); VGEDIT=<n>: with Valor step n selected
      // The hero card's live mini-arena: run it for a moment first. LOADOUT=role,kit,weapon,ability[,ultimate pick]; ABILITY=n clicks that ability key: it's picked, its detail box opens and the preview casts it (the lobby has no hover state since the declutter).
      var lo = Environment.GetEnvironmentVariable("LOADOUT");
      if (lo != null) { var q = lo.Split(','); var le = host.Lobby[host.MyId]; le.Role = (Role)int.Parse(q[0]); le.Kit = (Kit)int.Parse(q[1]); le.Weapon = byte.Parse(q[2]); le.Ability = byte.Parse(q[3]); if (q.Length > 4) le.Ult = byte.Parse(q[4]); }  // (a fifth number: the Ultimate row's key)
      var dsel = Environment.GetEnvironmentVariable("DETAIL");  // DETAIL=role|class: the detail box's role or class page (as after clicking that key)
      if (dsel != null) set("detailSel", dsel == "role" ? ArenaApp.DetailRole : ArenaApp.DetailClass);
      var car = Environment.GetEnvironmentVariable("CAROUSEL");  // CAROUSEL=row,first: a loadout carousel row (0 class, 1 weapon, 2 ability, 3 ultimate; 4-6 the Modifiers tab's Easier, Risky, Harder) scrolled to that key
      if (car != null) { var cq = car.Split(','); var cf = typeof(ArenaApp).GetField("carFirst", f2); var cp = typeof(ArenaApp).GetField("carPos", f2); var cs = typeof(ArenaApp).GetField("carSeen", f2);
        int crow = int.Parse(cq[0]), cfirst = int.Parse(cq[1]); ((int[])cf.GetValue(app))[crow] = cfirst; ((float[])cp.GetValue(app))[crow] = cfirst; ((int[])cs.GetValue(app))[crow] = -1; }
      // SHOWCASEKEYS=1: the mouse over the preview (its zoom bar and MAIN FIRE | ALT FIRE keys up); SHOWCASEALT=1: ALT FIRE picked.
      if (Environment.GetEnvironmentVariable("SHOWCASEKEYS") != null) set("showcaseZoomA", 1f);
      if (Environment.GetEnvironmentVariable("SHOWCASEALT") != null) set("showcaseAlt", true);
      // SHOWCASEHUE=<0-1>: the preview light's hue picked (-1 the role's colour); HUEOPEN=1: the wheel key's hue panel open (with SHOWCASEKEYS=1).
      if (Environment.GetEnvironmentVariable("SHOWCASEHUE") != null) { set("showcaseHue", float.Parse(Environment.GetEnvironmentVariable("SHOWCASEHUE"))); set("showcaseHueLoaded", true); }
      if (Environment.GetEnvironmentVariable("HUEOPEN") != null) set("showcaseHueOpen", true);
      var ab = Environment.GetEnvironmentVariable("ABILITY");
      if (ab != null) { host.Lobby[host.MyId].Ability = byte.Parse(ab); set("detailSel", 1); }
      {
        var le = host.Lobby[host.MyId];
        int shown = ab != null ? int.Parse(ab) : le.Ability;
        var sc = new Showcase { AltFire = Environment.GetEnvironmentVariable("SHOWCASEALT") != null && ab == null && Environment.GetEnvironmentVariable("ULTDEMO") == null };
        bool ultDemo = Environment.GetEnvironmentVariable("ULTDEMO") != null;  // ULTDEMO=1: the ultimate is being previewed
        if (ultDemo) set("detailSel", 2);  // (the ultimate's detail box, and the preview casting it)
        sc.Show(le.Role, le.Kit, le.Weapon, Upgrades.FirstAbility(le.Role, le.Kit, shown), ab != null || ultDemo, ultDemo, le.Ult);
        float st = float.Parse(Environment.GetEnvironmentVariable("SHOWT") ?? "2.5");
        // A Greatsword's cuts are timed on the clock from when the look first saw each step (GreatswordLook.cs WatchGreatMove):
        // run the clock with the preview and let the look see every step, as the game's frames would.
        var watchGreat = typeof(ArenaApp).GetMethod("WatchGreatMove", f2);
        bool great = false;
        foreach (var gp in sc.Game.Players.Values) great |= gp.Weapon.Id == WeaponId.Greatsword;
        // The Deployable Turret's look eases its drop and guns frame by frame (DeployTurretLook.cs): let it see each step too.
        var dtLook = typeof(ArenaApp).GetMethod("UpdateDeployTurrets", f2); var gameF = typeof(ArenaApp).GetField("game", f2);
        for (int f = 0; f < (int)(st * 60f); f++) {
          sc.Step(1f / 60f);
          if (System.Linq.Enumerable.Any(sc.Game.Zones.Values, z => z.Kind == ZoneKind.DeployTurret)) { var real = gameF.GetValue(app); gameF.SetValue(app, sc.Game); dtLook.Invoke(app, new object[] { sc.Game.AnimT }); gameF.SetValue(app, real); }
          if (!great) continue;
          Time.unscaledTime += 1f / 60f;
          foreach (var gp in sc.Game.Players.Values) if (gp.Weapon.Id == WeaponId.Greatsword) watchGreat.Invoke(app, new object[] { gp });
        }
        set("showcase", sc);
      }
      GL.ToUi = true;
      // LOBBYIN=<seconds>: that far into coming in from the main menu (LobbyEnter.cs), instead of the settled lobby.
      var lobbyIn = Environment.GetEnvironmentVariable("LOBBYIN");
      // LOBBYOUT=<seconds>: that far into leaving for the menu (the entrance backwards).
      var lobbyOut = Environment.GetEnvironmentVariable("LOBBYOUT");
      // RETURN=<seconds>: that far into coming back from a run (Recall.cs).
      var lobbyBack = Environment.GetEnvironmentVariable("RETURN");
      // CRT=<seconds>: that far into New run's CRT boot (LobbyCrt.cs). Every lobby shot has the CRT screen and glass round it.
      var crtIn = Environment.GetEnvironmentVariable("CRT");
      if (crtIn != null) set("crtEntry", true);
      if (Environment.GetEnvironmentVariable("CRTSTYLE") != null) set("crtStyle", int.Parse(Environment.GetEnvironmentVariable("CRTSTYLE")));
      if (crtIn != null) set("enterT", float.Parse(crtIn, System.Globalization.CultureInfo.InvariantCulture));
      Action<string> crtDraw = n => { Event.current.type = EventType.Repaint; typeof(ArenaApp).GetMethod(n, f2, null, new[] { typeof(float) }, null).Invoke(app, new object[] { 7.3f }); };
      // LOBBYCRTOUT=<seconds>: that far into leaving for the menu through the CRT power-cycle (LobbyCrtLeave.cs; 0 to 1.05).
      var lobbyScan = Environment.GetEnvironmentVariable("LOBBYCRTOUT");
      if (lobbyScan == null) crtDraw("DrawLobbyCrtBack");
      if (lobbyScan != null) {
        set("leaveT", float.Parse(lobbyScan, System.Globalization.CultureInfo.InvariantCulture)); set("crtLeave", true);
        GL.Tris.Clear(); GL.ToUi = false; set("drawScale", 1f);
        typeof(ArenaApp).GetMethod("DrawCrtLeaveBackdrop", f2).Invoke(app, new object[] { 3.7f });
        GL.ToUi = true; Event.current.type = EventType.Repaint; set("glMaterial", new Material(null));
        typeof(ArenaApp).GetMethod("DrawLobbyCrtLeave", f2).Invoke(app, null);
      }
      else if (crtIn != null) { Event.current.type = EventType.Repaint; set("glMaterial", new Material(null)); typeof(ArenaApp).GetMethod("DrawLobbyEnteringCrt", f2, null, Type.EmptyTypes, null).Invoke(app, null); }
      else if (lobbyBack != null) { set("returnT", float.Parse(lobbyBack, System.Globalization.CultureInfo.InvariantCulture)); Event.current.type = EventType.Repaint; set("glMaterial", new Material(null)); typeof(ArenaApp).GetMethod("DrawLobbyReturning", f2).Invoke(app, null); }
      else if (lobbyOut != null) { set("leaveT", float.Parse(lobbyOut, System.Globalization.CultureInfo.InvariantCulture)); Event.current.type = EventType.Repaint; set("glMaterial", new Material(null)); typeof(ArenaApp).GetMethod("DrawLobbyLeavingToMenu", f2).Invoke(app, null); }
      else if (lobbyIn != null) { set("enterT", float.Parse(lobbyIn, System.Globalization.CultureInfo.InvariantCulture)); Event.current.type = EventType.Repaint; set("glMaterial", new Material(null)); typeof(ArenaApp).GetMethod("DrawLobbyEntering", f2, null, Type.EmptyTypes, null).Invoke(app, null); }
      // LAUNCHFX=<seconds>: that far into the launch glitch (LaunchIntro.cs, DrawLobbyLaunchGlitch; 0 to 0.8), over a dark arena stand-in.
      else if (Environment.GetEnvironmentVariable("LAUNCHFX") != null) { set("launchT", float.Parse(Environment.GetEnvironmentVariable("LAUNCHFX"), System.Globalization.CultureInfo.InvariantCulture)); Event.current.type = EventType.Repaint; Ui.Fill(new Rect(0, 0, 1280, 720), new Color(0.04f, 0.05f, 0.08f)); typeof(ArenaApp).GetMethod("DrawLobbyLaunchGlitch", f2).Invoke(app, null); }
      else
      {
        // READYFX=<seconds>: your seat readied that long ago (the Ready shockwave, LobbyFx.cs).
        if (Environment.GetEnvironmentVariable("READYFX") != null) { var ra = (System.Collections.Generic.Dictionary<int, float>)typeof(ArenaApp).GetField("readyAt", f2).GetValue(app); ra[host.MyId] = Time.unscaledTime - float.Parse(Environment.GetEnvironmentVariable("READYFX"), System.Globalization.CultureInfo.InvariantCulture); }
        typeof(ArenaApp).GetMethod("DrawLobby", f2).Invoke(app, null);
      }
      if (lobbyScan == null) crtDraw("DrawLobbyCrtGlass");
      // INSPECT=2: the holo card of lobby player 2 (PlayerInspect.cs), wearing FRAME / CARD / NAMEFX / TRAIL (ids, as above) with made-up records.
      if (Environment.GetEnvironmentVariable("INSPECT") != null) {
        int iid = int.Parse(Environment.GetEnvironmentVariable("INSPECT")); var ie = host.Lobby[iid];
        ie.Rank = 34; ie.XpPct = 62; ie.Kills = 18420; ie.BestWave = 31; ie.BossKills = 94;
        if (Environment.GetEnvironmentVariable("TRAIL") != null) ie.Trail = Rank.TrailIndex(Environment.GetEnvironmentVariable("TRAIL"));
        set("inspectId", iid); set("inspectAt", Time.unscaledTime - 5f);
        typeof(ArenaApp).GetMethod("DrawInspect", f2).Invoke(app, null);
      }
      if (Environment.GetEnvironmentVariable("LOBBYMENU") != null) { set("lobbyMenuOpen", true); set("cabInstant", true); set("drawerKey", "pause"); set("winOpenAt", Time.unscaledTime - (Environment.GetEnvironmentVariable("PAUSEAGE") != null ? float.Parse(Environment.GetEnvironmentVariable("PAUSEAGE"), System.Globalization.CultureInfo.InvariantCulture) : 10f)); set("sounds", System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(SoundBank))); if (Environment.GetEnvironmentVariable("LOBBYMENU") == "settings") set("pauseSettings", true); if (Environment.GetEnvironmentVariable("LOBBYMENU") == "howto") { set("pauseHowTo", true); typeof(ArenaApp).GetMethod("OpenCabWindow", f2).Invoke(app, new object[] { 2 }); set("cabWinInAt", Time.unscaledTime - 10f); } Event.current.type = EventType.Repaint; typeof(ArenaApp).GetMethod("DrawLobbyMenu", f2).Invoke(app, null); }  // Esc's menu (MenuDrawer.cs)
      if (Environment.GetEnvironmentVariable("LOBBYSETTINGS") != null) { Ui.Fill(new Rect(0, 0, 1280, 720), new Color(0.01f, 0.01f, 0.03f, 0.75f)); typeof(ArenaApp).GetMethod("DrawSettings", f2).Invoke(app, null); }
      GL.ToUi = false;
    }
    else if (mode == "menu" || mode == "records" || mode == "cosmetics" || mode == "profile" || mode == "settings" || mode == "who" || mode == "whotr" || mode == "pacman")
    {
      // who: the "Who's playing?" disk carousel with made-up profiles (TWO=1: two of them; MANY=<n>: n of them, up to 50; MANAGE=1: Rename and Delete
      // under the middle disk; NEWDISK=<name>: naming a new profile, the blank disk in the middle; CARPOS=<disks>: the carousel that far round, e.g. 0.5 mid-scroll;
      // EMPTY=1: none yet).
      // whotr: the disk boot sequence to the menu (ProfileTransition.cs), PROFTR=<clock 0..4.8>,<profile index>.
      if (mode == "whotr") {
        PreviewWho();
        var pt = (Environment.GetEnvironmentVariable("PROFTR") ?? "0,0").Split(',');
        float ptT = float.Parse(pt[0], System.Globalization.CultureInfo.InvariantCulture);
        int slot = pt.Length > 1 ? int.Parse(pt[1]) : 0;
        set("profTr", true); set("profTrT", ptT); set("profTrSlot", slot);
        set("whoCarPos", (float)slot); set("whoCarTarget", (float)slot);
        set("menuIntroDone", true); set("menuIntroT", Mathf.Clamp(ptT - 2.6f, 0f, 2.199f));
        // PROFEJ=<0..2.7>,<profile index>: the eject's way back instead (DiskEject.cs).
        if (Environment.GetEnvironmentVariable("PROFEJ") != null) { var pe = Environment.GetEnvironmentVariable("PROFEJ").Split(','); int es = pe.Length > 1 ? int.Parse(pe[1]) : 0; set("profEj", true); set("profEjT", float.Parse(pe[0], System.Globalization.CultureInfo.InvariantCulture)); set("profTrSlot", es); set("whoCarPos", (float)es); set("whoCarTarget", (float)es); set("menuIntroT", 2.199f); }
        typeof(ArenaApp).GetMethod("WhoRefresh", f2).Invoke(app, null);
      }
      // pacman: the Pac-Man game's screen (PacmanUi.cs). PACKEYS=l1.7,u0.6: steer left for 1.7 s, then up for 0.6 s (letters l r u d), after READY; PACPAUSE=1: the pause screen.
      if (mode == "pacman") {
        typeof(ArenaApp).GetMethod("OpenPacman", f2).Invoke(app, null);
        var pg = (PacmanGame)typeof(ArenaApp).GetField("pac", f2).GetValue(app);
        pg.Step(PacmanGame.ReadyTime + 0.05f);
        foreach (var tok in (Environment.GetEnvironmentVariable("PACKEYS") ?? "l0.8").Split(',')) {
          if (tok.Length < 2) continue;
          char dch = tok[0]; float secs = float.Parse(tok.Substring(1), System.Globalization.CultureInfo.InvariantCulture);
          pg.SetInput(0, dch == 'l' ? -1 : dch == 'r' ? 1 : 0, dch == 'u' ? -1 : dch == 'd' ? 1 : 0);
          pg.Step(secs);
        }
        // PACFRIGHT=<s left>: the ghosts blue; PACDIE=<s left>: the chomper dying; PACOVER=1: game over.
        if (Environment.GetEnvironmentVariable("PACFRIGHT") != null) { pg.FrightT = float.Parse(Environment.GetEnvironmentVariable("PACFRIGHT"), System.Globalization.CultureInfo.InvariantCulture); foreach (var gh in pg.Ghosts) gh.Mode = GhostMode.Frightened; pg.Ghosts[1].Mode = GhostMode.Eaten; }
        if (Environment.GetEnvironmentVariable("PACDIE") != null) pg.Chompers[0].DeadT = float.Parse(Environment.GetEnvironmentVariable("PACDIE"), System.Globalization.CultureInfo.InvariantCulture);
        if (Environment.GetEnvironmentVariable("PACOVER") != null) pg.State = PacState.Over;
        if (Environment.GetEnvironmentVariable("PACPAUSE") != null) set("pacPaused", true);
      }
      if (mode == "who") {
        PreviewWho(); set("whoOpen", true); if (Environment.GetEnvironmentVariable("MANAGE") != null) set("whoManage", true); typeof(ArenaApp).GetMethod("WhoRefresh", f2).Invoke(app, null);
        typeof(ArenaApp).GetMethod("CarCenterOn", f2).Invoke(app, new object[] { ProfileStore.ActiveId });
        if (Environment.GetEnvironmentVariable("NEWDISK") != null) { typeof(ArenaApp).GetMethod("StartNewProfile", f2).Invoke(app, null); set("whoName", Environment.GetEnvironmentVariable("NEWDISK")); set("whoCarPos", typeof(ArenaApp).GetField("whoCarTarget", f2).GetValue(app)); set("whoFocus", false); }
        if (Environment.GetEnvironmentVariable("CARPOS") != null) { float cp = float.Parse(Environment.GetEnvironmentVariable("CARPOS"), System.Globalization.CultureInfo.InvariantCulture); set("whoCarPos", cp); set("whoCarTarget", cp); }
      }
      // records: the Records page, with made-up saves and a profile (SavesUi.cs).
      if (mode == "records" || mode == "cosmetics" || mode == "profile" || mode == "menu") PreviewSaves();
      // menu: PLAY=1 shows the saved-runs picker in the box after Play (NOSAVES=1: with no saves; MORE=1: extra saves; SCROLL=n: scroll the list n px).
      if (mode == "menu" && Environment.GetEnvironmentVariable("PLAY") != null) {
        if (Environment.GetEnvironmentVariable("MORE") != null) { for (int i = 0; i < 6; i++) { var x = new RunSave { Id = "m" + i, SavedAt = Shot.Now.AddDays(-3 - i), Wave = 3 + i, Score = 900 * (i + 1), RunTime = 300f * (i + 1) }; x.Seats.Add(new SeatSave { Name = "Anthony", Role = Role.Dps, Kit = Kit.Ninja, Weapon = 0, Ability = 0, Level = 3 + i }); SaveStore.Write(x); } }
        typeof(ArenaApp).GetMethod("OpenPlayPicker", f2).Invoke(app, null); set("menuPlayK", 1f);
        // PLAYK=<0..1>: the shutter that far up (MenuMachine.cs); PLAYBACK=1: rolling back down instead.
        if (Environment.GetEnvironmentVariable("PLAYK") != null) { set("menuPlayK", float.Parse(Environment.GetEnvironmentVariable("PLAYK"), System.Globalization.CultureInfo.InvariantCulture)); if (Environment.GetEnvironmentVariable("PLAYBACK") != null) set("menuPlayOpen", false); }
        if (Environment.GetEnvironmentVariable("NOSAVES") != null) set("savedRuns", new System.Collections.Generic.List<RunSave>());
        if (Environment.GetEnvironmentVariable("SCROLL") != null) { float sc = float.Parse(Environment.GetEnvironmentVariable("SCROLL"), System.Globalization.CultureInfo.InvariantCulture); set("menuListScroll", sc); set("menuListShown", sc); }
      }
      GL.Tris.Clear();
      set("drawScale", 1f);
      if ((mode == "menu" || mode == "who" || mode == "whotr") && Environment.GetEnvironmentVariable("BG") != null) { set("menuBg", int.Parse(Environment.GetEnvironmentVariable("BG"))); set("menuBgOverride", true); set("bgProfile", Profile.Current); }  // (before the background is drawn)
      if (mode == "menu") FakeBeat(app);
      typeof(ArenaApp).GetMethod("DrawMenuBackground", f2).Invoke(app, new object[] { 7.3f });
      var hv = Environment.GetEnvironmentVariable("MOUSE");
      if (hv != null) { var xy = hv.Split(','); Event.current.mousePosition = new Vector2(float.Parse(xy[0]), float.Parse(xy[1])); }
      Event.current.type = EventType.Repaint;
      set("glMaterial", new Material(null));
      GL.ToUi = true;
      if (mode == "settings") set("sounds", System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(SoundBank)));
      if (Environment.GetEnvironmentVariable("KBSCROLL") != null) set("keybindScroll", float.Parse(Environment.GetEnvironmentVariable("KBSCROLL")));  // KBSCROLL=<px>: Settings → Keybinds scrolled down (e.g. 400: the Toggles)
      // The drawer windows (MenuDrawer.cs): SECTION=n opens that group / section / topic; the drawer's slide is skipped.
      // SKINKIT=<Kit>,<Role>,<weapon key> (the lobby pick the Weapon skin section shows), SKINXP=<n> (that class's expertise XP),
      // SKINEQ=<skin id> (equipped on its weapon): Profile > Look > Weapon skin, SECTION=7 (WeaponSkinUi.cs).
      var skinKit = Environment.GetEnvironmentVariable("SKINKIT");
      if (skinKit != null)
      {
        var sk = skinKit.Split(',');
        var kit = (Kit)Enum.Parse(typeof(Kit), sk[0]);
        set("kitChoice", kit); set("hostRole", (Role)Enum.Parse(typeof(Role), sk[1])); set("weaponChoice", int.Parse(sk[2]));
        var pf = Profile.Current;
        if (Environment.GetEnvironmentVariable("SKINXP") != null) pf.ClassXp[Expertise.Key(kit)] = long.Parse(Environment.GetEnvironmentVariable("SKINXP"));
        var eq = Environment.GetEnvironmentVariable("SKINEQ");
        if (eq != null && Expertise.Skin(eq) != null) pf.EquipSkin(Expertise.Skin(eq).Weapon, eq);
      }
      var secEnv = Environment.GetEnvironmentVariable("SECTION");
      if (secEnv != null) { int sec = int.Parse(secEnv); set("settingsGroup", sec); set("settingsShown", sec); set("recordsSection", sec); set("cosmeticsSection", sec); set("rankSection", sec); set("unlocksSection", sec); set("howToTopic", sec); }
      if (Environment.GetEnvironmentVariable("REDUCED") != null) set("fxReduced", true);  // Reduced effects (calm CRT)
      // The window beside the cabinet (CrtWindows.cs): settings opens one (NOWIN=1: just the cabinet's list); WINWIN=<s>: that long since it began to slide out.
      float winWin = Environment.GetEnvironmentVariable("WINWIN") != null ? float.Parse(Environment.GetEnvironmentVariable("WINWIN"), System.Globalization.CultureInfo.InvariantCulture) : 10f;
      set("cabInstant", true);
      if (mode == "settings") set("settingsOpen", true);
      if (Environment.GetEnvironmentVariable("NOWIN") == null) {
        if (mode == "settings") { if (secEnv == null) set("settingsGroup", 1); typeof(ArenaApp).GetMethod("OpenCabWindow", f2).Invoke(app, new object[] { 1 }); set("cabWinInAt", Time.unscaledTime - winWin); }
        else if (mode == "records" || mode == "cosmetics" || mode == "profile") { set("profileTab", mode == "records" ? 2 : mode == "cosmetics" ? 0 : (Environment.GetEnvironmentVariable("TAB") != null ? int.Parse(Environment.GetEnvironmentVariable("TAB")) : 0)); typeof(ArenaApp).GetMethod("OpenCabWindow", f2).Invoke(app, new object[] { 3 }); set("cabWinInAt", Time.unscaledTime - winWin); }
      }
      if (Environment.GetEnvironmentVariable("RTAB") != null) set("recentTab", int.Parse(Environment.GetEnvironmentVariable("RTAB")));  // Recent runs: 0 Overview, 1 Trends (SECTION=4 opens the page)
      if (Environment.GetEnvironmentVariable("RSEL") != null) set("recentSel", int.Parse(Environment.GetEnvironmentVariable("RSEL")));
      set("drawerKey", (mode == "records" || mode == "cosmetics" || mode == "profile") ? "profile" : mode);
      // The profile (ProfileUi.cs): records opens its Records tab, cosmetics the Look tab, profile the tab in TAB (0 Look, 1 Rank, 2 Records, 3 Unlocks; default Look).
      if (mode == "records") set("profileTab", 2); else if (mode == "cosmetics") set("profileTab", 0); else if (mode == "profile") set("profileTab", Environment.GetEnvironmentVariable("TAB") != null ? int.Parse(Environment.GetEnvironmentVariable("TAB")) : 0); set("drawerAt", -10f);
      // WINT=<s>: that far into the window's opening; WINCLOSE=0..1: that far through closing (WindowAnim.cs).
      if (Environment.GetEnvironmentVariable("WINT") != null) set("winOpenAt", Time.unscaledTime - float.Parse(Environment.GetEnvironmentVariable("WINT"), System.Globalization.CultureInfo.InvariantCulture));
      if (Environment.GetEnvironmentVariable("WINCLOSE") != null) { set("winClosingAt", 0f); set("winCloseK", float.Parse(Environment.GetEnvironmentVariable("WINCLOSE"), System.Globalization.CultureInfo.InvariantCulture)); }
      // MENUINTRO=<seconds>: the main menu that far into its intro (MenuIntro.cs); JOINOPEN=1: Join's address row open.
      if (Environment.GetEnvironmentVariable("MENUINTRO") != null) { set("menuIntroDone", true); set("menuIntroT", float.Parse(Environment.GetEnvironmentVariable("MENUINTRO"), System.Globalization.CultureInfo.InvariantCulture)); }
      // BG=<n>: the menu background behind the menu, Who's playing? and the boot sequence (0: the Holo Floor, the game's own).
      if ((mode == "menu" || mode == "who" || mode == "whotr") && Environment.GetEnvironmentVariable("BG") != null) { set("menuBg", int.Parse(Environment.GetEnvironmentVariable("BG"))); set("menuBgOverride", true); set("bgProfile", Profile.Current); }
      // MENUSET=<-1..3>: Settings on the bay's screen (BaySettings.cs): -1 the tiles, 0 Audio, 1 Visual effects, 2 Gameplay, 3 Keybinds; SETSCROLL=<px>.
      if (mode == "menu" && Environment.GetEnvironmentVariable("MENUHUE") != null) { var mh = Environment.GetEnvironmentVariable("MENUHUE").Split(','); set("menuBgHueLoaded", true); set("menuBgHue", float.Parse(mh[0])); if (mh.Length > 1 && mh[1] == "open") { set("menuBgHueOpen", true); set("menuBgHueShown", 1f); } }
      if (mode == "menu" && Environment.GetEnvironmentVariable("MENUSET") != null) { int sg = int.Parse(Environment.GetEnvironmentVariable("MENUSET")); set("menuSetOpen", true); set("menuSetGroup", sg); set("sounds", System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(SoundBank))); set("menuSetShown", sg); set("menuSetShownAt", -100f); set("menuPlayK", 1f); if (Environment.GetEnvironmentVariable("SETSCROLL") != null) set("menuSetScroll", float.Parse(Environment.GetEnvironmentVariable("SETSCROLL"))); }
      // WIKI=<query>: the Wiki on the bay's screen (BayWiki.cs) searching for it ("" for everything); WIKIPAGE=<name>: that entry's page.
      if (mode == "menu" && Environment.GetEnvironmentVariable("WIKI") != null) { set("menuWikiOpen", true); set("wikiQuery", Environment.GetEnvironmentVariable("WIKI")); set("wikiOpenedAt", -100f); set("menuPlayK", 1f); set("sounds", System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(SoundBank))); if (Environment.GetEnvironmentVariable("WIKIPAGE") != null) set("wikiPage", Wiki.Find(Environment.GetEnvironmentVariable("WIKIPAGE"))); }
      if (Environment.GetEnvironmentVariable("JOINOPEN") != null) { set("menuJoinOpen", true); set("menuPlayK", 1f); }
      // MENUT=<s>: the menu's clock (Time.unscaledTime).
      if (Environment.GetEnvironmentVariable("MENUT") != null) Time.unscaledTime = float.Parse(Environment.GetEnvironmentVariable("MENUT"), System.Globalization.CultureInfo.InvariantCulture);
      // WHEEL=<x>,<y>,<dy>: a mouse-wheel event there first (one draw on it), then the shot: to check scrolling.
      if (Environment.GetEnvironmentVariable("WHEEL") != null) {
        var wv = Environment.GetEnvironmentVariable("WHEEL").Split(',');
        var keepPos = Event.current.mousePosition;
        Event.current.type = EventType.ScrollWheel; Event.current.mousePosition = new Vector2(float.Parse(wv[0]), float.Parse(wv[1])); Event.current.delta = new Vector2(0f, float.Parse(wv[2]));
        typeof(ArenaApp).GetMethod(mode == "menu" ? "DrawMenu" : mode == "who" ? "DrawWho" : "DrawProfTr", f2).Invoke(app, null);
        Console.Error.WriteLine("after wheel: menuSetScroll=" + typeof(ArenaApp).GetField("menuSetScroll", f2).GetValue(app) + " menuListScroll=" + typeof(ArenaApp).GetField("menuListScroll", f2).GetValue(app) + " whoCarTarget=" + typeof(ArenaApp).GetField("whoCarTarget", f2).GetValue(app));
        Event.current.type = EventType.Repaint; Event.current.mousePosition = keepPos;
      }
      typeof(ArenaApp).GetMethod(mode == "menu" ? "DrawMenu" : mode == "records" || mode == "cosmetics" || mode == "profile" ? "DrawProfile" : mode == "settings" ? "DrawSettings" : mode == "who" ? "DrawWho" : mode == "pacman" ? "DrawPacman" : "DrawProfTr", f2).Invoke(app, null);
      GL.ToUi = false;
    }
    else if (mode == "keys")
    {
      // The one CRT key (DkKey, DevKit.cs) in each of its states, plus a lamp, an icon and a sub-line. WIDE=1: a longer label.
      Event.current.type = EventType.Repaint;
      Ui.Fill(new Rect(0f, 0f, UnityEngine.Screen.width, UnityEngine.Screen.height), CrtStyle.Bg);
      string text = Environment.GetEnvironmentVariable("WIDE") != null ? "Start the run" : "Play";
      string[] names = { "plain", "on", "danger", "gold", "hover", "locked", "view only", "static", "quiet", "lamp", "sub-line", "tint (old DkKey)" };
      float kw = 170f, kh = 50f;
      for (int i = 0; i < names.Length; i++)
      {
        var k = new Rect(60f + (i % 4) * 300f, 110f + (i / 4) * 170f, kw, kh);
        Ui.Label(new Rect(k.x, k.y - 34f, kw, 20f), names[i].ToUpperInvariant(), 13, CrtStyle.Desc, TextAnchor.MiddleCenter, true);
        Event.current.mousePosition = i == 4 ? k.center : new Vector2 { x = -100, y = -100 };
        switch (i)
        {
          case 0: ArenaApp.DkKey(k, text, false, false); break;
          case 1: ArenaApp.DkKey(k, text, true, false); break;
          case 2: ArenaApp.DkKey(k, text, false, false, danger: true); break;
          case 3: ArenaApp.DkKey(k, text, false, false, accent: CrtStyle.Gold); break;
          case 4: ArenaApp.DkKey(k, text, false, false); break;
          case 5: ArenaApp.DkKey(k, text, false, false, locked: true); break;
          case 6: ArenaApp.DkKey(k, text, true, false, enabled: false); break;
          case 7: ArenaApp.DkKey(k, text, false, false, isStatic: true); break;
          case 8: ArenaApp.DkKey(k, text, false, false, quiet: true); break;
          case 9: ArenaApp.DkKey(k, text, false, false, lamp: true); break;
          case 10: ArenaApp.DkKey(k, text, false, false, sub: "15 gold"); break;
          default: typeof(ArenaApp).GetMethod("DkKey", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(Rect), typeof(string), typeof(Color), typeof(bool), typeof(int) }, null).Invoke(app, new object[] { k, text, Ui.GoldButton, false, 13 }); break;
        }
      }
    }
    else if (mode == "kitgrid")
    {
      // Every role and weapon of one class (KIT=0..5, default the Ninja), drawn big: a check on how each one looks.
      GL.Tris.Clear();
      set("drawScale", 1f);
      typeof(ArenaApp).GetMethod("DrawMenuBackground", f2).Invoke(app, new object[] { 3.7f });
      Event.current.type = EventType.Repaint;
      set("glMaterial", new Material(null));
      var kitArg = (Kit)int.Parse(Environment.GetEnvironmentVariable("KIT") ?? "4");
      var begin = typeof(ArenaApp).GetMethod("BeginUiGl", f2); var at = typeof(ArenaApp).GetMethod("UiModelAt", f2);
      var draw = typeof(ArenaApp).GetMethod("DrawPreviewPlayer", f2); var end = typeof(ArenaApp).GetMethod("EndUiGl", f2);
      GL.ToUi = true;
      begin.Invoke(app, null);
      for (int r = 0; r < 3; r++) for (int w = 0; w < 3; w++) {
        at.Invoke(app, new object[] { new V2(240f + w * 400f, 130f + r * 230f), 3.2f });
        draw.Invoke(app, new object[] { (Role)r, kitArg, w, 2.3f + w * 0.4f + r });
      }
      end.Invoke(app, null);
      for (int r = 0; r < 3; r++) for (int w = 0; w < 3; w++)
        Ui.Label(new Rect(140f + w * 400f, 215f + r * 230f, 200f, 20f), Defs.Weapon((Role)r, kitArg, w).Name, 14, Color.white, TextAnchor.MiddleCenter, true);
      GL.ToUi = false;
    }
    else if (mode == "englook")
    {
      // ENGSHEET=0 the Engineer's three looks, 1 the kit (firing + rivets, Drag Pylon, Breach hammer).
      GL.Tris.Clear();
      set("drawScale", 1f);
      typeof(ArenaApp).GetMethod("DrawMenuBackground", f2).Invoke(app, new object[] { 3.7f });
      Event.current.type = EventType.Repaint;
      set("glMaterial", new Material(null));
      int sheet = int.Parse(Environment.GetEnvironmentVariable("ENGSHEET") ?? "0");
      var begin = typeof(ArenaApp).GetMethod("BeginUiGl", f2); var at = typeof(ArenaApp).GetMethod("UiModelAt", f2);
      var cell = typeof(ArenaApp).GetMethod("DrawEngineerSheetCell", f2); var end = typeof(ArenaApp).GetMethod("EndUiGl", f2);
      sheet = Math.Min(sheet, 1);
      string[][] names = { new[] { "A  Hi-vis", "B  Drafter", "C  Welder" }, new[] { "Rivet gun: rivets", "Drag Pylon", "Breach: the hammer" } };
      float[] zoom = { 8.5f, 2.2f };
      GL.ToUi = true;
      begin.Invoke(app, null);
      for (int i = 0; i < 3; i++) {
        at.Invoke(app, new object[] { new V2(213f + i * 427f, 420f), sheet != 1 ? zoom[sheet] : i == 0 ? 5f : i == 1 ? 1.5f : 1.6f });
        cell.Invoke(app, new object[] { sheet, i, 2.3f });
      }
      end.Invoke(app, null);
      for (int i = 0; i < 3; i++)
        Ui.Label(new Rect(63f + i * 427f, 640f, 300f, 24f), names[sheet][i], 18, Color.white, TextAnchor.MiddleCenter, true);
      GL.ToUi = false;
    }
    else if (mode == "celled")
    {
      int ci = int.Parse(Environment.GetEnvironmentVariable("CELLED") ?? "0");
      typeof(ArenaApp).GetField("cedIndex", f2).SetValue(app, ci);
      foreach (var kv in new[] { "CEDTAB:cedTab", "CEDSHAPE:cedShape", "CEDORIENT:cedOrient", "CEDX:cedPosX", "CEDY:cedPosY" })
      { var pr = kv.Split(':'); var v = Environment.GetEnvironmentVariable(pr[0]); if (v != null) typeof(ArenaApp).GetField(pr[1], f2).SetValue(app, int.Parse(v)); }
      if (Environment.GetEnvironmentVariable("COREID") != null) typeof(ArenaApp).GetField("coreIndex", f2).SetValue(app, int.Parse(Environment.GetEnvironmentVariable("COREID")));
      if (Environment.GetEnvironmentVariable("HUBID") != null) typeof(ArenaApp).GetField("hubId", f2).SetValue(app, (LayoutId)int.Parse(Environment.GetEnvironmentVariable("HUBID")));
      Event.current.type = EventType.Repaint; set("glMaterial", new Material(null));
      typeof(ArenaApp).GetMethod("DrawCellEditor", f2).Invoke(app, null);
    }
    else if (mode == "tab")
    {
      var rng = new System.Random(4);
      Func<string, int> U = n => Array.FindIndex(Upgrades.All, u => u.Name == n);
      foreach (var n in new[] { "Hollow Points", "Hollow Points", "Sharpened Rounds", "Quick Trigger", "Hair Trigger", "Fortitude", "Vitality", "Vitality", "Regeneration", "Focus", "Clarity", "Thick Skin", "Extra Use", "Multishot", "Multishot", "Piercing Rounds", "Extra Use", "Shockwave", "Blink", "Starburst", "Warden's Guard", "Stun Gun", "Swarm Rounds", "Twin Fang", "Collapse Rounds", "Homing Rounds", "Homing Rounds", "Incendiary Rounds", "Shock Rounds", "Velocity Rounds", "Light Boots", "Sprinter", "Mending", "Executioner", "Royal Jelly" })
        host.Game.GrantUpgrade(1, U(n));
      if (Environment.GetEnvironmentVariable("MASTERY") != null) { float[] xs = { 900f, 420f, 130f, 0f, 20f }; for (int k = 0; k < 5; k++) if (me.Slots[k].Id != AbilityId.None) me.MasteryXp[me.Slots[k].Id] = xs[k]; }
      me.Level = 31; me.Slots[2].Charges = 0; me.Slots[2].Timer = 6.3f;
      var tm = Environment.GetEnvironmentVariable("MOUSE");
      if (tm != null) { var xy = tm.Split(','); Event.current.mousePosition = new Vector2(float.Parse(xy[0]), float.Parse(xy[1])); }
      if (Environment.GetEnvironmentVariable("STATSTAB") == null)
        typeof(ArenaApp).GetMethod("DrawTabMenu", f2).Invoke(app, new object[] { me, true });
      else
      {
        // The Run stats screen (RunStatsUi.cs): STATSTAB=0 Run so far, 1 Your build, 2 Team; PAUSE=1 as opened from the pause menu.
        var g = host.Game;
        g.Wave = 23; g.State = MatchState.Fighting; g.RunTime = 1902f; g.Score = 18450;
        g.Route.Clear(); g.Route.AddRange(new[] { PathId.SniperRidge, PathId.Bazaar, PathId.SwarmFields, PathId.FrozenLake });
        g.CurrentPath = PathId.FrozenLake; g.PathFrom = 21; g.PathTo = 24;
        // HUNT=1: during the Warden Hunt, on its sixth wave.
        if (Environment.GetEnvironmentVariable("HUNT") != null) { g.Hunt = Hunts.All[0]; g.Wave = 6; g.Route.Clear(); g.CurrentPath = PathId.None; g.RunTime = 260f; }
        int k = 0;
        foreach (var pl in g.Players.Values)
        {
          var r = pl.Run; float f = k == 0 ? 1f : k == 1 ? 0.82f : 0.35f;
          r.DamageDealt = 306307f * f; r.Kills = (int)(781 * f); r.HealingDone = k == 2 ? 9420f : 2227f * f; r.BiggestHit = 1240f + 900f * k;
          r.Downs = 2 - k; r.Revives = k == 2 ? 3 : 1 - k; r.ShotsFired = 9000; r.ShotsHit = 5800 + 500 * k; r.DamageTaken = 4210f - 800f * k;
          r.EliteKills = 31 - 5 * k; r.BossKills = 4; r.AbilitiesUsed = 212 - 30 * k; k++;
          if (pl.Id != 1) foreach (var n in new[] { "Hollow Points", "Multishot", "Fortitude", "Vitality", "Vitality", "Executioner", "Piercing Rounds", "Sprinter", "Quick Trigger" }) g.GrantUpgrade(pl.Id, U(n));
        }
        set("statsTab", int.Parse(Environment.GetEnvironmentVariable("STATSTAB")));
        if (Environment.GetEnvironmentVariable("STATSTAB") == "3" && Environment.GetEnvironmentVariable("INVEMPTY") == null) {  // STATSTAB=3: the Inventory page, stocked (INVEMPTY=1: nothing held, Classic arena)
          g.Diff.Set(DiffKnob.ArenaExpanding, 1f);
          me.Gold = 245; me.Vectors = 52; me.VectorCap = 90; me.ReviveToken = true; me.Gadget = GadgetId.Magnet; me.GadgetLeft = 6.4f; me.Relic = RelicId.WallBounce;
          me.Hand[0] = (int)PieceCard.Ring; me.Hand[1] = (int)PieceCard.Hall; me.Hand[2] = (int)PieceCard.Cross; me.Hand[3] = (int)PieceCard.MiniBoss;
          me.Stash[(int)BuildKind.Teleporter] = 1; me.Stash[(int)BuildKind.Turret] = 3;
          g.RoomTokens.Add(new RoomToken { Kind = RoomKind.Floor, Floor = PathId.FrozenLake }); g.RoomTokens.Add(new RoomToken { Kind = RoomKind.Shop });
          g.ExpandOpen = Environment.GetEnvironmentVariable("INVOPEN") != null;  // INVOPEN=1: a build round is open (cards and buildables are usable; MOUSE=x,y hovers one)
          foreach (var tm2 in g.Players.Values) if (tm2.Id != me.Id) { tm2.Gold = 90; tm2.Vectors = 18; tm2.VectorCap = 70; tm2.Hand[0] = (int)PieceCard.Box; tm2.Hand[1] = (int)PieceCard.THall; tm2.Stash[(int)BuildKind.Landmine] = 2; tm2.Relic = RelicId.WallBounce; }
          if (Environment.GetEnvironmentVariable("INVVIEW") != null) set("invView", int.Parse(Environment.GetEnvironmentVariable("INVVIEW")));  // INVVIEW=<player id>: a teammate's inventory
          var im = Environment.GetEnvironmentVariable("MOUSE"); if (im != null) { var xy = im.Split(','); Event.current.mousePosition = new Vector2(float.Parse(xy[0]), float.Parse(xy[1])); }
          if (Environment.GetEnvironmentVariable("INVFULL") != null) { foreach (var fl in new[] { PathId.SniperRidge, PathId.SwarmFields, PathId.Sanctuary, PathId.BurningGrounds }) g.RoomTokens.Add(new RoomToken { Kind = RoomKind.Floor, Floor = fl }); me.Hand[4] = (int)PieceCard.Box; me.Hand[5] = (int)PieceCard.LHall; }  // INVFULL=1: six kits (the list scrolls) and a full hand
        }
        if (Environment.GetEnvironmentVariable("SOLOTAB") != null) set("tabPaused", true);
        // The screen as if already open (its panels settled); HOLOAT=<s>: that far into the page's panels switching on (TabHolo.cs).
        set("statsLastFrame", Time.frameCount); set("statsShown", int.Parse(Environment.GetEnvironmentVariable("STATSTAB")));
        set("holoOpenAt", -10f);
        set("holoPageAt", Environment.GetEnvironmentVariable("HOLOAT") != null ? Time.unscaledTime - float.Parse(Environment.GetEnvironmentVariable("HOLOAT"), System.Globalization.CultureInfo.InvariantCulture) : -10f);
        Event.current.type = EventType.Repaint; set("glMaterial", new Material(null)); GL.ToUi = true;
        typeof(ArenaApp).GetMethod("DrawRunStatsScreen", f2).Invoke(app, new object[] { me, Environment.GetEnvironmentVariable("PAUSE") != null });
        GL.ToUi = false;
      }
    }
    else if (mode == "shop")
    {
      // The Bazaar's shop (ShopUi.cs): SHOPSEL=n puts item n (a card, then the services) in the middle, SELL=1 opens the sell list, GIVE=1 the Give popup.
      Func<string, int> U = n => Array.FindIndex(Upgrades.All, u => u.Name == n);
      foreach (var n in new[] { "Hollow Points", "Fortitude", "Multishot", "Thick Skin", "Riposte" })
        host.Game.GrantUpgrade(1, U(n));
      var g = host.Game;
      if (Environment.GetEnvironmentVariable("SHOPEXPAND") != null) g.Diff.Set(DiffKnob.ArenaExpanding, 1f);  // SHOPEXPAND=1: the Expanding arena's services (Floor kit, Vector pack)
      if (Environment.GetEnvironmentVariable("MARKET") != null)  // MARKET=1: you stand in the opened Black Market (the vendor room) of an Expanding arena; MARKET=shop|arms|medic: that room instead; SHOPSEL picks the card, BUILDS=1 puts some in your stash
      {
        string mv = Environment.GetEnvironmentVariable("MARKET");
        var mkind = mv == "shop" ? RoomKind.Shop : mv == "arms" ? RoomKind.Arms : mv == "medic" ? RoomKind.Medic : RoomKind.BlackMarket;
        g.Diff.Set(DiffKnob.ArenaExpanding, 1f);
        g.Expanded.Clear(); g.Expanded.AddRange(Expansion.GridCore()); g.Openings.AddRange(Expansion.GridCoreCuts());
        var vo = Expansion.PlacementsFor(g.Expanded, PieceShape.Mono); if (vo.Count > 0) { var v2 = vo[0].Rooms[0]; v2.Owner = 1; v2.Kind = mkind; g.Expanded.Add(v2); }  // (a one-cell Black Market off the core)
        typeof(Game).GetField("expWorld", f2).SetValue(g, null);
        g.RoomsUp = true;
        var va = g.ExpansionWorld.Areas[g.ExpansionWorld.Areas.Length - 1];
        foreach (var ar in g.ExpansionWorld.Areas) if (ar.Kind == mkind) va = ar;
        me.Pos = va.Mid; me.Disp = va.Mid;
        if (Environment.GetEnvironmentVariable("BUILDS") != null) { me.Stash[0] = 1; me.Stash[1] = 2; me.Stash[2] = 5; }
        if (mkind == RoomKind.Medic) me.Hp = me.MaxHp * 0.45f;  // (hurt, so the heal shows what it gives)
      }
      g.CurrentPath = PathId.Bazaar; g.PathFrom = 11; g.PathTo = 14; g.Wave = 14; g.State = MatchState.Intermission;
      typeof(Game).GetMethod("OpenShop", f2).Invoke(g, null);
      g.CurrentPath = PathId.None;
      if (Environment.GetEnvironmentVariable("MARKET") != null) g.ShopOpen = true;
      int k = 0;
      foreach (var p in g.Players.Values) { p.Gold = k == 0 ? 252 : k == 1 ? 112 : 162; p.ShopDone = k == 2; k++; }
      me.ShelfSold = 1 << 2; me.Hp = me.MaxHp * 0.6f;
      g.ShopNotices.Add(new ShopNotice { Text = "Mira gave you 40 gold", Life = 2f });
      int sel = Environment.GetEnvironmentVariable("SHOPSEL") != null ? int.Parse(Environment.GetEnvironmentVariable("SHOPSEL")) : 0;
      typeof(ArenaApp).GetField("shopSel", f2).SetValue(app, sel);
      typeof(ArenaApp).GetField("shopTurn", f2).SetValue(app, (float)sel);
      // SHOPCAT=-1 the two category panels (the default without SHOPSEL), 0 the upgrades' carousel, 1 the services'; with only SHOPSEL, the category it's in
      var catEnv = Environment.GetEnvironmentVariable("SHOPCAT");
      int cat = catEnv != null ? int.Parse(catEnv) : Environment.GetEnvironmentVariable("SHOPSEL") == null ? -1 : Environment.GetEnvironmentVariable("MARKET") != null ? (sel < Market.Count ? 0 : 1) : sel < Game.ShelfCount(me) ? 0 : 1;
      typeof(ArenaApp).GetField("shopCat", f2).SetValue(app, cat);
      if (Environment.GetEnvironmentVariable("SELL") != null) typeof(ArenaApp).GetField("shopSellOpen", f2).SetValue(app, true);
      if (Environment.GetEnvironmentVariable("GIVE") != null) foreach (var p in g.Players.Values) if (p.Id != me.Id) typeof(ArenaApp).GetField("shopGiveTo", f2).SetValue(app, p.Id);
      typeof(ArenaApp).GetMethod("DrawShop", f2).Invoke(app, new object[] { me, 1f });
    }
    else
    {
      Func<string, int> U = n => Array.FindIndex(Upgrades.All, u => u.Name == n);
      if (Environment.GetEnvironmentVariable("GOLDINSTEAD") != null) { var gg = host.Game; gg.CurrentPath = PathId.Bazaar; gg.PathFrom = 11; gg.PathTo = 14; gg.Wave = 12; }  // GOLDINSTEAD=1: on the Bazaar, so the picker shows its gold-instead key
      if (mode == "picker" && Environment.GetEnvironmentVariable("PRIZE") != null) { me.Offer = Upgrades.PrizeOffer(new System.Random(3), OfferCtx.Of(me, false)); me.OfferPrize = true; }  // a challenge room's prize
      else if (mode == "picker" && Environment.GetEnvironmentVariable("USES") != null) { host.Game.GrantUpgrade(1, U("Overdrive")); me.Offer = new[] { U("Fortitude"), U("Extra Use"), U("Clarity") }; }  // USES=1: the Extra Use key chooser
      else if (mode == "picker" && Environment.GetEnvironmentVariable("KEYS") != null) { host.Game.GrantUpgrade(1, U("Overdrive")); me.Offer = new[] { U("Shockwave"), U("Blink"), U("Starburst") }; }
      else if (mode == "picker" && Environment.GetEnvironmentVariable("PICKCARDS") != null) me.Offer = Array.ConvertAll(Environment.GetEnvironmentVariable("PICKCARDS").Split(','), n => U(n));
      else if (mode == "picker" && Environment.GetEnvironmentVariable("MILESTONE") != null) { host.Game.GrantUpgrade(1, U("Scholar")); host.Game.GrantUpgrade(1, U("Scholar")); host.Game.GrantUpgrade(1, U("Second Helping")); me.Offer = new[] { U("Scholar"), U("Second Helping"), U("Wide Choice") }; me.OfferMilestone = true; }  // MILESTONE=1: a milestone pick
      else if (mode == "picker") me.Offer = Environment.GetEnvironmentVariable("REVOLVER") != null ? new[] { U("Multishot"), U("Hollow Points"), U("Barrage") } : Environment.GetEnvironmentVariable("PARRYCARDS") != null ? new[] { U("Reinforced Guard"), U("Counterstance"), U("Wide Guard") } : new[] { U("Hollow Points"), U("Multishot"), U("Blink") };
      else if (mode == "bosscards") {  // the boss card pick (BossCards.cs): BOSSFULL=1 fills your hand so the cards grey out
        host.Game.Diff.Set(DiffKnob.ArenaExpanding, 1f);
        me.Hand[0] = (int)PieceCard.Box; me.Hand[1] = (int)PieceCard.Hall;
        if (Environment.GetEnvironmentVariable("BOSSFULL") != null) for (int hi = 0; hi < me.Hand.Length; hi++) me.Hand[hi] = (int)PieceCard.Box;
        me.Offer = new[] { (int)PieceCard.Ring, (int)PieceCard.Arms, BossCards.KitCode(PathId.SniperRidge), (int)PieceCard.MiniBoss, (int)PieceCard.LHall }; me.OfferCards = true; me.PendingCards = 1;
        if (Environment.GetEnvironmentVariable("WAVECARDS") != null) { me.Offer = new[] { (int)Expansion.GridRoundCard(new DetRandom(3), 3), (int)Expansion.MakePieceCard(PieceShape.TetT) }; me.OfferWave = true; me.WavePicks = 1; }  // WAVECARDS=1: the wave card pick
        if (Environment.GetEnvironmentVariable("STARTCARDS") != null) { me.Hand[0] = me.Hand[1] = 0; me.Offer = Array.ConvertAll(Expansion.GridStartOffer(int.Parse(Environment.GetEnvironmentVariable("STARTCARDS"))), c => (int)c); me.OfferWave = me.OfferStart = true; me.WavePicks = 1; me.PendingCards = 2; } }  // STARTCARDS=<seed>: the starting piece pick (INTRO=<s> for its roll)
      else if (mode == "legendary") { me.Offer = new[] { U("Warden's Guard"), U("Stun Gun"), U("Searchlight") }; me.OfferLegendary = true; }
      else {  // the weapon mod pick; MODWEAPON=<WeaponId> picks your weapon, so the cards say what each mod does for it (WeaponMods.cs)
        var mw = Environment.GetEnvironmentVariable("MODWEAPON");
        if (mw != null) foreach (var w in Defs.Weapons) if (w.Id.ToString() == mw) { me.Weapon = w; me.Kit = w.Kit; me.Role = w.Role; me.Def = Defs.Get(w.Role); }
        me.Offer = Upgrades.ModOffer(OfferCtx.Of(me, false)); me.OfferMod = true; }
      me.PendingPicks = 2;
      typeof(ArenaApp).GetField("selectedCard", f2).SetValue(app, 1);
      typeof(ArenaApp).GetField("shownOffer", f2).SetValue(app, string.Join(",", Array.ConvertAll(me.Offer, x => x.ToString())) + "/" + me.Taken.Count + "," + Game.HandCount(me) + "," + host.Game.RoomTokens.Count);
      int pickMark = GUI.Log.Count;
      // INTRO=<seconds>: the picker that long into its opening (PickIntro.cs); INTROREROLL=1 for a reroll's (cards only).
      var intro = Environment.GetEnvironmentVariable("INTRO");
      if (intro != null) {
        set("offerShownAt", Time.unscaledTime - float.Parse(intro, System.Globalization.CultureInfo.InvariantCulture));
        set("introFull", Environment.GetEnvironmentVariable("INTROREROLL") == null);
        GL.ToUi = true; Event.current.type = EventType.Repaint; set("glMaterial", new Material(null));
      }
      typeof(ArenaApp).GetMethod("DrawUpgradePicker", f2).Invoke(app, new object[] { me });
      GL.ToUi = false;
      // OUTRO=<seconds>: the moment that long after taking the middle card (PickOutro.cs), instead of the picker.
      var outro = Environment.GetEnvironmentVariable("OUTRO");
      if (outro != null) {
        GUI.Log.RemoveRange(pickMark, GUI.Log.Count - pickMark);
        me.PendingPicks = 1;
        typeof(ArenaApp).GetMethod("StartPickOutro", f2).Invoke(app, new object[] { me, 1 });
        set("pickOutroAt", Time.unscaledTime - float.Parse(outro, System.Globalization.CultureInfo.InvariantCulture));
        GL.ToUi = true;
        Event.current.type = EventType.Repaint;
        set("glMaterial", new Material(null));
        typeof(ArenaApp).GetMethod("DrawPickOutro", f2).Invoke(app, null);
        GL.ToUi = false;
      }
    }
  }
}
