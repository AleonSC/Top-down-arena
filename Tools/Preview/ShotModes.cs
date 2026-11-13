using System; using System.IO; using System.Reflection; using System.Threading; using Arena; using UnityEngine;
// Shot.cs screens: DrawMode draws the screen named by the mode argument (hud, gameover, dev, pause, guide, bg,
// lobby, kitgrid, practicehud, labsetup, labpanel, tab, shop, picker, ...).
static partial class Shot {
  // A made-up replay for the Replays tab and the timeline bar: n waves over `frames` frames, a boss every fifth, a couple of downs.
  static ReplayFile SampleReplay(string name, int frames, int waves, string mode, string party)
  {
    var f = new ReplayFile { Created = Shot.Now.AddHours(-3), Mode = mode, Party = party, MaxWave = waves, Players = 1 };
    for (int i = 0; i < frames; i++) f.Dts.Add(1f / 60f);
    for (int w = 1; w <= waves; w++) f.Marks.Add(new ReplayMark { Frame = (w - 1) * frames / waves, Kind = w % 5 == 0 ? ReplayMark.Boss : ReplayMark.Wave, Value = w });
    f.Marks.Add(new ReplayMark { Frame = frames * 3 / 10, Kind = ReplayMark.Down, Value = 1 });
    f.Marks.Add(new ReplayMark { Frame = frames * 6 / 10, Kind = ReplayMark.Down, Value = 1 });
    f.Seconds = frames / 60f;
    f.Path = Path.Combine("/tmp/replays", name + ReplayFile.Ext);
    return f;
  }
  static void DrawMode() {
    if (mode == "hud") {
      // TILEFX=moment,seconds: every ability tile that long into a reaction (HudTiles.cs): ready, charge, use or deny
      // (deny also puts Space on cooldown, so its timer shows). The stub ignores GUI.matrix: no pop or shake, the rest shows.
      var tilefx = Environment.GetEnvironmentVariable("TILEFX");
      if (tilefx != null)
      {
        var tp = tilefx.Split(',');
        if (tp[0] == "deny") { var sl = host.Game.Me.Slots[0]; sl.Charges = 0; sl.Timer = 2.4f; }
        typeof(ArenaApp).GetMethod("PreviewTileMoment", f2).Invoke(app, new object[] { tp[0], tp.Length > 1 ? float.Parse(tp[1]) : 0.05f });
      }
      // EMOTEBAR=1: the emote bar open (rank 60); EMOTE=n[,age]: emote number n showing over player 1, that many seconds in (EmoteBar.cs)
      if (Environment.GetEnvironmentVariable("EMOTEBAR") != null) { Profile.Current.Xp = Rank.XpAt(60); set("emoteBarOpen", true); set("emoteBarK", 1f); }
      if (Environment.GetEnvironmentVariable("EMOTE") != null) { var ep = Environment.GetEnvironmentVariable("EMOTE").Split(','); host.Emotes[1] = new Session.EmoteShow { Index = byte.Parse(ep[0]), At = Session.Clock - (ep.Length > 1 ? float.Parse(ep[1]) : 0.8f) }; }
      bool zoomGl = Environment.GetEnvironmentVariable("ZOOM") != null || Environment.GetEnvironmentVariable("ROOMS") != null || Environment.GetEnvironmentVariable("LAG") != null;  // (the off-screen markers and the lag chart's lines draw with GL on the HUD; OffscreenMarkers.cs, LagChartUi.cs)
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
      // RETRY=1: a life left, the host can retry from the checkpoint (Checkpoints.cs).
      if (Environment.GetEnvironmentVariable("RETRY") != null) { gg.RetryReady = true; gg.RetryWave = 11; gg.LivesUsed = 0; }
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
      typeof(ArenaApp).GetMethod("DrawRunSummary", f2).Invoke(app, null);
    }
    else if (mode == "replay")
    {
      // The timeline over a replay (ReplayUi.cs). RSPEED=-4 (a speed), RPAUSE=1, RAT=0..1 (how far along).
      typeof(ArenaApp).GetMethod("DrawHud", f2).Invoke(app, null);
      var rf = SampleReplay("Boss practice, Warden", 60 * 1100, 12, "Standard", "Mia (Knight)");
      var rp = new ReplayPlayer(); rp.Load(rf);
      rp.Seek((int)(rf.Frames * float.Parse(Environment.GetEnvironmentVariable("RAT") ?? "0.42")));
      rp.Paused = Environment.GetEnvironmentVariable("RPAUSE") != null;
      rp.Rate = float.Parse(Environment.GetEnvironmentVariable("RSPEED") ?? "1");
      set("replay", rp);
      Event.current.type = EventType.Repaint; set("glMaterial", new Material(null)); GL.ToUi = true;
      typeof(ArenaApp).GetMethod("DrawReplayBar", f2).Invoke(app, null);
      GL.ToUi = false;
    }
    else if (mode == "dev")
    {
      typeof(ArenaApp).GetMethod("DrawHud", f2).Invoke(app, null);
      host.Game.DevSetStat(1, Array.FindIndex(Game.StatFields, x => x.Name == "DmgMult"), 2.5f);
      host.Game.DevSetGod(1, true);
      set("devWave", 10);
      // DEVDRAWER=1..4 (wave, party, view, labs), DEVSIDE=1 (stat editor), DEVSHEET=1..3 (DPS, replays, balance)
      if (Environment.GetEnvironmentVariable("DEVSHEET") == "2")
      {
        // Sample replays in the replays folder (the stub's persistent data path is /tmp).
        if (Directory.Exists("/tmp/replays")) Directory.Delete("/tmp/replays", true);
        SampleReplay("Boss practice, Warden", 60 * 292, 5, "Boss Hunt", "Mia (Knight)").Save("/tmp/replays/Boss practice, Warden.replay");
        SampleReplay("Co-op test, 3 players", 60 * 668, 12, "Standard", "Mia (Ninja), Bo (Healer), Cy (Tank)").Save("/tmp/replays/Co-op test, 3 players.replay");
        SampleReplay("Run 2026-11-02 16-05", 60 * 1110, 21, "Endless", "Mia (Hemomancer)").Save("/tmp/replays/Run 2026-11-02 16-05.replay");
        if (Environment.GetEnvironmentVariable("RENAME") != null) { set("replayRenamePath", "/tmp/replays/Co-op test, 3 players.replay"); set("replayRenameText", "Co-op test, 3 players"); }
        if (Environment.GetEnvironmentVariable("DELETE") != null) set("replayDeletePath", "/tmp/replays/Boss practice, Warden.replay");
      }
      if (Environment.GetEnvironmentVariable("DEVDRAWER") != null) set("devDrawer", int.Parse(Environment.GetEnvironmentVariable("DEVDRAWER")));
      if (Environment.GetEnvironmentVariable("DEVSIDE") != null) set("devSide", true);
      if (Environment.GetEnvironmentVariable("DEVSHEET") != null) set("devSheet", int.Parse(Environment.GetEnvironmentVariable("DEVSHEET")));
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
      host.Screen = Environment.GetEnvironmentVariable("PAUSECLOSE") != null ? AppScreen.Menu : AppScreen.Game;  // in a match (PAUSECLOSE: closing, at PAUSEANIM)
      Event.current.type = EventType.Repaint; set("glMaterial", new Material(null)); GL.ToUi = true;
      typeof(ArenaApp).GetMethod("DrawPauseMenu", f2).Invoke(app, null);
      GL.ToUi = false;
    }
    else if (mode == "bg")
    {
      GL.Tris.Clear();
      set("drawScale", 1f);
      set("menuBg", int.Parse(Environment.GetEnvironmentVariable("BG")));
      typeof(ArenaApp).GetMethod("DrawMenuBackground", f2).Invoke(app, new object[] { 7.3f });
    }
    else if (mode == "lobby" || mode == "lobbypr")
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
      // LOBBYULT=0|1: your card is an Elementalist Tank with that ultimate picked (1 Lightning Rod), its detail box open (DETAIL=2 is implied).
      if (Environment.GetEnvironmentVariable("LOBBYULT") != null) { host.Lobby[1].Role = Role.Tank; host.Lobby[1].Kit = Kit.Elementalist; host.Lobby[1].Weapon = 0; host.Lobby[1].Ability = 0; host.Lobby[1].Ult = byte.Parse(Environment.GetEnvironmentVariable("LOBBYULT")); set("detailSel", 2); }
      if (Environment.GetEnvironmentVariable("WILDCARD") != null) { host.Lobby[1].Role = Role.Wildcard; host.Lobby[1].Kit = Kit.Soldier; host.Lobby[1].Weapon = (byte)int.Parse(Environment.GetEnvironmentVariable("WILDWEAPON") ?? "9");  // WILDWEAPON=<pick>: 18 the Arc Knuckles
       host.Lobby[1].Ability = 12; host.Lobby[1].Wild = Defs.MakeWild(14, 1); host.Lobby[1].Ult = (byte)Upgrades.WildUlt(Kit.Ninja, 0); }  // WILDCARD=1: Twin Blades, Shadow Step, Blade Fan on Q, Death Lotus
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
        var pp = host.Game.ProfileOverride = new Profile(); pp.BossBeaten(EnemyType.Boss, 5); pp.HuntEnded(EnemyType.Boss, true, 300f);
        if (Environment.GetEnvironmentVariable("PERKS") == "2" || Environment.GetEnvironmentVariable("PERKS") == "3") pp.RunsWon = 1;
        if (Environment.GetEnvironmentVariable("PERKS") == "3") pp.HuntEnded(EnemyType.Boss, true, 280f, true);  // PERKS=3: also won on Hard, so Contraband is unlocked
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
      if (Environment.GetEnvironmentVariable("LOBBYTAB") != null) { set("lobbyTab", int.Parse(Environment.GetEnvironmentVariable("LOBBYTAB"))); set("lobbyWasQuick", true); host.Modifiers = RunMod.GlassCannon | RunMod.Frenzy | RunMod.Lucky | RunMod.Hunted | RunMod.SecondWind; }
      // MODSCROLL=<px>: the Modifiers tab's list scrolled down (e.g. 420 shows the Risky tier, Simple Arena among it).
      if (Environment.GetEnvironmentVariable("MODSCROLL") != null) set("modsScroll", float.Parse(Environment.GetEnvironmentVariable("MODSCROLL")));
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
      // MODSFOLD=n: the Modifiers tab with those tiers folded (a bit per ModLean: 1 Harder, 2 Easier, 4 Risky).
      set("modsFolded", int.Parse(Environment.GetEnvironmentVariable("MODSFOLD") ?? "0"));
      // DIFF=Hard (a preset) or DIFF=Custom (Hard with a few numbers changed): the lobby's Difficulty tab (LOBBYTAB=3).
      if (Environment.GetEnvironmentVariable("DIFF") != null) { var dl = (DifficultyLevel)Enum.Parse(typeof(DifficultyLevel), Environment.GetEnvironmentVariable("DIFF")); host.Diff = Difficulty.Of(dl == DifficultyLevel.Custom ? DifficultyLevel.Hard : dl); if (dl == DifficultyLevel.Custom) { host.Diff.Set(DiffKnob.EnemyDamage, 1.8f); host.Diff.Set(DiffKnob.Regen, 0.5f); host.Diff.Set(DiffKnob.ReviveTime, 2.5f); } }
      // HUNT=<n>: Boss Hunt mode, its tab (the Warden's hunt open, won once), the nth boss on top (n > 1: a never-met "???" boss).
      if (Environment.GetEnvironmentVariable("HUNT") != null) { host.HuntMode = true; var hpr = host.Game.ProfileOverride = new Profile(); hpr.BossBeaten(EnemyType.Boss, 5); hpr.HuntEnded(EnemyType.Boss, true, 312f); hpr.HuntEnded(EnemyType.Boss, false, 200f);
        int hn; if (int.TryParse(Environment.GetEnvironmentVariable("HUNT"), out hn) && hn > 1) host.HuntBoss = BossSchedule.Campaign.BossAt(Math.Min(hn, 10) * 5); }
      if (Environment.GetEnvironmentVariable("CLIENT") != null) host.MyId = 2;
      if (Environment.GetEnvironmentVariable("LOBBYSETTINGS") != null) { set("settingsOpen", true); set("sounds", System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(SoundBank))); }
      if (mode == "lobbypr") {
        System.Collections.Generic.List<string> pr;
        host.PracticeMode = true;
        host.Plan = PracticePlan.Parse("loop: no\nwave 3: Grunt x4, Sniper x2, Swarmer x6\nwave 6: Spinner x2, Medic x1, Shielder x2, Charger x1\nwave 10: The Warden x1, Grunt x3\nwave 15: Twin Reapers x1\nwave 20: The Singularity x1", out pr);
        host.OthersCanEdit = Environment.GetEnvironmentVariable("EDIT") != null;
        host.PlanEditor = "Aleon"; host.PlanEditorSel = 1;
        set("practiceSel", 1);
        if (Environment.GetEnvironmentVariable("LOBBYTAB") != null) set("lobbyWasPractice", true);  // (so LOBBYTAB=7 stays on the Training tab)
      }
      // FAVS=1: the Saved loadouts drawer open with four favourites (FavoritesUi.cs); FAVS=name: naming a fifth; FAVS=sure: deleting the second; FAVS=rename: renaming the second; FAVS=key: closed, the SAVED key showing.
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
      // The hero card's live mini-arena: run it for a moment first. LOADOUT=role,kit,weapon,ability[,ultimate pick]; ABILITY=n hovers that ability.
      var lo = Environment.GetEnvironmentVariable("LOADOUT");
      if (lo != null) { var q = lo.Split(','); var le = host.Lobby[host.MyId]; le.Role = (Role)int.Parse(q[0]); le.Kit = (Kit)int.Parse(q[1]); le.Weapon = byte.Parse(q[2]); le.Ability = byte.Parse(q[3]); if (q.Length > 4) le.Ult = byte.Parse(q[4]); }  // (a fifth number: the Ultimate row's key)
      var ab = Environment.GetEnvironmentVariable("ABILITY");
      if (ab != null) set("abilityHover", int.Parse(ab));
      {
        var le = host.Lobby[host.MyId];
        int shown = ab != null ? int.Parse(ab) : le.Ability;
        var sc = new Showcase();
        bool ultDemo = Environment.GetEnvironmentVariable("ULTDEMO") != null;  // ULTDEMO=1: the ultimate is being previewed
        if (ultDemo) set("detailSel", 2);  // (the ultimate's detail box, and the preview casting it)
        sc.Show(le.Role, le.Kit, le.Weapon, Upgrades.RoleAbility(le.Role, le.Kit, shown), ab != null || ultDemo, ultDemo, le.Ult);
        float st = float.Parse(Environment.GetEnvironmentVariable("SHOWT") ?? "2.5");
        for (int f = 0; f < (int)(st * 60f); f++) sc.Step(1f / 60f);
        set("showcase", sc);
      }
      // TEXTEDIT=1: the dev text editor's mode (outlines the text under MOUSE); TEXTEDIT=<text>: its edit box for that text
      // (TEXTBUF = what's typed). Drawn twice, as in the game the outline is the text picked the frame before.
      var te = Environment.GetEnvironmentVariable("TEXTEDIT");
      var beginTe = typeof(ArenaApp).GetMethod("TextEditBeginGui", f2); var endTe = typeof(ArenaApp).GetMethod("TextEditEndGui", f2);
      if (te != null) {
        set("textEditMode", true);
        GL.ToUi = true; beginTe.Invoke(app, null); typeof(ArenaApp).GetMethod("DrawLobby", f2).Invoke(app, null); endTe.Invoke(app, null); GL.ToUi = false;
        GUI.Log.Clear(); GL.Tris.Clear();
        typeof(ArenaApp).GetMethod("DrawMenuBackground", f2).Invoke(app, new object[] { 3.7f });
        if (te != "1") { set("textEditOriginal", te); set("textEditBuffer", Environment.GetEnvironmentVariable("TEXTBUF") ?? te); set("textEditNote", ""); set("textEditOpen", true); }
        GL.ToUi = true; beginTe.Invoke(app, null);
      }
      else GL.ToUi = true;
      // LOBBYIN=<seconds>: that far into coming in from the main menu (LobbyEnter.cs), instead of the settled lobby.
      var lobbyIn = Environment.GetEnvironmentVariable("LOBBYIN");
      // LOBBYOUT=<seconds>: that far into leaving for the menu (the entrance backwards).
      var lobbyOut = Environment.GetEnvironmentVariable("LOBBYOUT");
      // RETURN=<seconds>: that far into coming back from a run (Recall.cs).
      var lobbyBack = Environment.GetEnvironmentVariable("RETURN");
      // CRT=<seconds>: that far into New run's CRT boot (LobbyCrt.cs). Every lobby shot has the CRT screen and glass round it.
      var crtIn = Environment.GetEnvironmentVariable("CRT");
      if (crtIn != null) set("crtEntry", true);
      if (crtIn != null) set("enterT", float.Parse(crtIn, System.Globalization.CultureInfo.InvariantCulture));
      Action<string> crtDraw = n => { Event.current.type = EventType.Repaint; typeof(ArenaApp).GetMethod(n, f2, null, new[] { typeof(float) }, null).Invoke(app, new object[] { 7.3f }); };
      crtDraw("DrawLobbyCrtBack");
      if (crtIn != null) { Event.current.type = EventType.Repaint; set("glMaterial", new Material(null)); typeof(ArenaApp).GetMethod("DrawLobbyEnteringCrt", f2, null, Type.EmptyTypes, null).Invoke(app, null); }
      else if (lobbyBack != null) { set("returnT", float.Parse(lobbyBack, System.Globalization.CultureInfo.InvariantCulture)); Event.current.type = EventType.Repaint; set("glMaterial", new Material(null)); typeof(ArenaApp).GetMethod("DrawLobbyReturning", f2).Invoke(app, null); }
      else if (lobbyOut != null) { set("leaveT", float.Parse(lobbyOut, System.Globalization.CultureInfo.InvariantCulture)); Event.current.type = EventType.Repaint; set("glMaterial", new Material(null)); typeof(ArenaApp).GetMethod("DrawLobbyLeavingToMenu", f2).Invoke(app, null); }
      else if (lobbyIn != null) { set("enterT", float.Parse(lobbyIn, System.Globalization.CultureInfo.InvariantCulture)); Event.current.type = EventType.Repaint; set("glMaterial", new Material(null)); typeof(ArenaApp).GetMethod("DrawLobbyEntering", f2, null, Type.EmptyTypes, null).Invoke(app, null); }
      else typeof(ArenaApp).GetMethod("DrawLobby", f2).Invoke(app, null);
      crtDraw("DrawLobbyCrtGlass");
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
      if (te != null) endTe.Invoke(app, null);
      GL.ToUi = false;
    }
    else if (mode == "menu" || mode == "records" || mode == "cosmetics" || mode == "profile" || mode == "settings" || mode == "who" || mode == "whotr" || mode == "pacman")
    {
      // who: the "Who's playing?" screen with made-up profiles (TWO=1: two of them, so one slot says Empty; MANAGE=1: Rename and Delete showing; EMPTY=1: none yet).
      // whotr: the move between the picker and the menu (ProfileTransition.cs), PROFTR=<clock 0..5.4>,<slot 0..2>[,back].
      if (mode == "whotr") {
        PreviewWho();
        var pt = (Environment.GetEnvironmentVariable("PROFTR") ?? "0,0,back").Split(',');
        float ptT = float.Parse(pt[0], System.Globalization.CultureInfo.InvariantCulture);
        set("profTr", true); set("profTrT", ptT); set("profTrSlot", int.Parse(pt[1])); set("profTrFwd", pt.Length < 3);
        set("menuIntroDone", true); set("menuIntroT", Mathf.Min(2.749f, 0.35f + Mathf.Max(0f, ptT - 3.0f)));
        // PROFEJ=<0..2.9>,<slot>: the eject key's move instead (DiskEject.cs); the menu stays fully drawn.
        if (Environment.GetEnvironmentVariable("PROFEJ") != null) { var pe = Environment.GetEnvironmentVariable("PROFEJ").Split(','); set("profEj", true); set("profEjT", float.Parse(pe[0], System.Globalization.CultureInfo.InvariantCulture)); set("profTrSlot", pe.Length > 1 ? int.Parse(pe[1]) : 0); set("profTrFwd", false); set("menuIntroT", 2.749f); }
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
      if (mode == "who") { PreviewWho(); set("whoOpen", true); if (Environment.GetEnvironmentVariable("MANAGE") != null) set("whoManage", true); typeof(ArenaApp).GetMethod("WhoRefresh", f2).Invoke(app, null); }
      // records: the Records page, with made-up saves and a profile (SavesUi.cs).
      if (mode == "records" || mode == "cosmetics" || mode == "profile" || mode == "menu") PreviewSaves();
      // menu: PLAY=1 shows the saved-runs picker in the box after Play (NOSAVES=1: with no saves; MORE=1: extra saves; SCROLL=n: scroll the list n px).
      if (mode == "menu" && Environment.GetEnvironmentVariable("PLAY") != null) {
        if (Environment.GetEnvironmentVariable("MORE") != null) { for (int i = 0; i < 6; i++) { var x = new RunSave { Id = "m" + i, SavedAt = Shot.Now.AddDays(-3 - i), Wave = 3 + i, Score = 900 * (i + 1), RunTime = 300f * (i + 1) }; x.Seats.Add(new SeatSave { Name = "Anthony", Role = Role.Dps, Kit = Kit.Ninja, Weapon = 0, Ability = 0, Level = 3 + i }); SaveStore.Write(x); } }
        typeof(ArenaApp).GetMethod("OpenPlayPicker", f2).Invoke(app, null); set("menuPlayK", 1f);
        // PLAYK=<0..1>: that far through the Play move (MenuServo.cs); PLAYBACK=1: going back to the menu instead.
        if (Environment.GetEnvironmentVariable("PLAYK") != null) { set("menuPlayK", float.Parse(Environment.GetEnvironmentVariable("PLAYK"), System.Globalization.CultureInfo.InvariantCulture)); if (Environment.GetEnvironmentVariable("PLAYBACK") != null) set("menuPlayOpen", false); }
        if (Environment.GetEnvironmentVariable("NOSAVES") != null) set("savedRuns", new System.Collections.Generic.List<RunSave>());
        if (Environment.GetEnvironmentVariable("SCROLL") != null) { float sc = float.Parse(Environment.GetEnvironmentVariable("SCROLL"), System.Globalization.CultureInfo.InvariantCulture); set("menuListScroll", sc); set("menuListShown", sc); }
      }
      GL.Tris.Clear();
      set("drawScale", 1f);
      typeof(ArenaApp).GetMethod("DrawMenuBackground", f2).Invoke(app, new object[] { 7.3f });
      var hv = Environment.GetEnvironmentVariable("MOUSE");
      if (hv != null) { var xy = hv.Split(','); Event.current.mousePosition = new Vector2(float.Parse(xy[0]), float.Parse(xy[1])); }
      Event.current.type = EventType.Repaint;
      set("glMaterial", new Material(null));
      GL.ToUi = true;
      if (mode == "settings") set("sounds", System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(SoundBank)));
      // The drawer windows (MenuDrawer.cs): SECTION=n opens that group / section / topic; the drawer's slide is skipped.
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
      if (Environment.GetEnvironmentVariable("JOINOPEN") != null) { set("menuJoinOpen", true); set("menuBottomHide", 1f); }
      typeof(ArenaApp).GetMethod(mode == "menu" ? "DrawMenu" : mode == "records" || mode == "cosmetics" || mode == "profile" ? "DrawProfile" : mode == "settings" ? "DrawSettings" : mode == "who" ? "DrawWho" : mode == "pacman" ? "DrawPacman" : "DrawProfTr", f2).Invoke(app, null);
      GL.ToUi = false;
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
    else if (mode == "practicehud")
    {
      System.Collections.Generic.List<string> pr;
      host.Game.Practice = PracticePlan.Parse("wave 3: Grunt x4\nwave 6: Swarmer x8\nwave 10: The Warden x1", out pr);
      host.Game.PracticeIndex = 1;
      if (Environment.GetEnvironmentVariable("PDT") != null) typeof(ArenaApp).GetField("pdBorn", f2).SetValue(app, Time.unscaledTime - float.Parse(Environment.GetEnvironmentVariable("PDT"), System.Globalization.CultureInfo.InvariantCulture));  // PDT=<s>: the practice DOWNED window that long since it opened (with DOWNED=down:1; PracticeDownedUi.cs)
      typeof(ArenaApp).GetMethod("DrawHud", f2).Invoke(app, null);
    }
    else if (mode == "labsetup")
    {
      var lab = (BossLab)typeof(ArenaApp).GetField("lab", f2).GetValue(app);
      lab.Wave = 20; lab.PlayerCount = 3; lab.Seats[1].Kit = Kit.Elementalist; lab.Seats[2].Kit = Kit.Knight; lab.Seats[2].Weapon = 1;
      Event.current.type = EventType.Repaint; set("glMaterial", new Material(null));
      typeof(ArenaApp).GetMethod("DrawLabSetup", f2).Invoke(app, null);
    }
    else if (mode == "labpanel")
    {
      var lab = (BossLab)typeof(ArenaApp).GetField("lab", f2).GetValue(app);
      lab.Host = host; lab.FightTime = 23.4f; lab.BossHpFrac = 0.41f; lab.BossName = "The Singularity"; lab.PeakBullets = 171;
      foreach (var p in host.Game.Players.Values) { var st = new LabPlayerStats { Name = p.Name, BossDamage = 2140f, DamageTaken = 96f, Downs = 0 }; st.TakenFrom["The Singularity"] = 70f; st.TakenFrom["The Singularity (aimed)"] = 22f; st.TakenFrom["Grunt"] = 4f; lab.Stats[p.Id] = st; }
      typeof(ArenaApp).GetMethod("DrawHud", f2).Invoke(app, null);
      typeof(ArenaApp).GetMethod("DrawLabPanel", f2).Invoke(app, null);
      if (Environment.GetEnvironmentVariable("RESULT") != null) { lab.Outcome = LabOutcome.Won; typeof(ArenaApp).GetMethod("DrawLabResult", f2).Invoke(app, null); }
    }
    else if (mode == "waveeditor")
    {
      // The F1 wave editor (WaveEditorUi.cs) on a Warden drill: EDT=<seconds> puts the playhead there; EDPROPS=1 the
      // map in Props mode; EDGROUP=n selects a group; EDLOAD=1 shows the saved files.
      GL.Tris.Clear();
      set("drawScale", 1f);
      set("glMaterial", new Material(null));
      Event.current.type = EventType.Repaint;
      var ws = new WaveSet { Name = "warden drills" };
      var dw = new DesignWave { Level = 6 };
      var r1 = new DesignGroup { Name = "Breakout rush", From = "B", At = 0f, Every = 0.4f };
      for (int i = 0; i < 6; i++) r1.Members.Add(EnemyType.Rioter);
      r1.Members.Add(EnemyType.Guard);
      dw.Groups.Add(r1);
      var r2 = new DesignGroup { Name = "Gas pair", From = "CD", At = 5f, Arrive = ArriveMode.Together };
      r2.Members.Add(EnemyType.GasGrenadier); r2.Members.Add(EnemyType.GasGrenadier);
      dw.Groups.Add(r2);
      var r3 = new DesignGroup { Name = "Guards", From = "E", At = 8f, Every = 0.6f };
      for (int i = 0; i < 3; i++) r3.Members.Add(EnemyType.Guard);
      dw.Groups.Add(r3);
      dw.Groups.Add(new DesignGroup { Name = "Riot line", Squad = "Riot line", From = "A", At = 11f });
      foreach (var pp in new[] { new V2(610f, 330f), new V2(990f, 330f) }) dw.Props.Add(new DesignProp { Kind = PropKind.Crate, Pos = pp });
      dw.Props.Add(new DesignProp { Kind = PropKind.Barrel, Pos = new V2(800f, 640f) });
      dw.Props.Add(new DesignProp { Kind = PropKind.Pot, Pos = new V2(300f, 200f) });
      ws.Waves.Add(new DesignWave { Level = 5 });
      ws.Waves.Add(dw);
      set("edSet", ws);
      set("edWave", 1);
      set("edGroup", int.Parse(Environment.GetEnvironmentVariable("EDGROUP") ?? "1"));
      set("edT", float.Parse(Environment.GetEnvironmentVariable("EDT") ?? "6.7"));
      typeof(ArenaApp).GetProperty("waveEdOpen", f2).SetValue(app, true, null);
      if (Environment.GetEnvironmentVariable("EDPROPS") != null) set("edPropsMode", true);
      if (Environment.GetEnvironmentVariable("EDLOAD") != null) set("edLoadOpen", true);
      var hv2 = Environment.GetEnvironmentVariable("MOUSE");
      if (hv2 != null) { var xy = hv2.Split(','); Event.current.mousePosition = new Vector2(float.Parse(xy[0]), float.Parse(xy[1])); }
      typeof(ArenaApp).GetMethod("DrawMenuBackground", f2).Invoke(app, new object[] { 3.7f });
      GL.ToUi = true;  // its models (the palette) over the panels
      typeof(ArenaApp).GetMethod("DrawWaveEditor", f2).Invoke(app, null);
      GL.ToUi = false;
    }
    else if (mode == "tab")
    {
      var rng = new System.Random(4);
      Func<string, int> U = n => Array.FindIndex(Upgrades.All, u => u.Name == n);
      foreach (var n in new[] { "Hollow Points", "Hollow Points", "Sharpened Rounds", "Quick Trigger", "Hair Trigger", "Fortitude", "Vitality", "Vitality", "Regeneration", "Focus", "Clarity", "Thick Skin", "Second Charge", "Multishot", "Multishot", "Piercing Rounds", "Overcharge", "Shockwave", "Blink", "Nova", "Warden's Guard", "Stun Gun", "Swarm Rounds", "Twin Fang", "Collapse Rounds", "Homing Rounds", "Homing Rounds", "Incendiary Rounds", "Shock Rounds", "Velocity Rounds", "Light Boots", "Sprinter", "Mending", "Executioner", "Royal Jelly" })
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
      if (Environment.GetEnvironmentVariable("MARKET") != null)  // MARKET=1: you stand in the opened Black Market (the vendor room) of an Expanding arena; SHOPSEL picks the card, BUILDS=1 puts some in your stash
      {
        g.Diff.Set(DiffKnob.ArenaExpanding, 1f);
        var vk = Expansion.FloatingFor(3, 2, g.Expanded, RoomKind.Vendor); if (vk.HasValue) g.Expanded.Add(vk.Value);
        int vi = g.Expanded.FindIndex(r => r.Floating && r.Kind == RoomKind.Vendor);
        var vc = vi >= 0 ? Expansion.ConnectorFor(g.Expanded, vi) : null; if (vc.HasValue) { var v2 = vc.Value; v2.Owner = 1; g.Expanded.Add(v2); }
        typeof(Game).GetField("expWorld", f2).SetValue(g, null);
        g.RoomsUp = true;
        var va = g.ExpansionWorld.Areas[g.ExpansionWorld.Areas.Length - 1];
        foreach (var ar in g.ExpansionWorld.Areas) if (ar.Kind == RoomKind.Vendor) va = ar;
        me.Pos = va.Mid; me.Disp = va.Mid;
        if (Environment.GetEnvironmentVariable("BUILDS") != null) { me.Stash[0] = 1; me.Stash[1] = 2; me.Stash[2] = 5; }
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
      if (Environment.GetEnvironmentVariable("SELL") != null) typeof(ArenaApp).GetField("shopSellOpen", f2).SetValue(app, true);
      if (Environment.GetEnvironmentVariable("GIVE") != null) foreach (var p in g.Players.Values) if (p.Id != me.Id) typeof(ArenaApp).GetField("shopGiveTo", f2).SetValue(app, p.Id);
      typeof(ArenaApp).GetMethod("DrawShop", f2).Invoke(app, new object[] { me, 1f });
    }
    else
    {
      Func<string, int> U = n => Array.FindIndex(Upgrades.All, u => u.Name == n);
      if (Environment.GetEnvironmentVariable("GOLDINSTEAD") != null) { var gg = host.Game; gg.CurrentPath = PathId.Bazaar; gg.PathFrom = 11; gg.PathTo = 14; gg.Wave = 12; }  // GOLDINSTEAD=1: on the Bazaar, so the picker shows its gold-instead key
      if (mode == "picker" && Environment.GetEnvironmentVariable("PRIZE") != null) { me.Offer = Upgrades.PrizeOffer(new System.Random(3), OfferCtx.Of(me, false)); me.OfferPrize = true; }  // a challenge room's prize
      else if (mode == "picker" && Environment.GetEnvironmentVariable("KEYS") != null) { host.Game.GrantUpgrade(1, U("Overdrive")); me.Offer = new[] { U("Shockwave"), U("Blink"), U("Nova") }; }
      else if (mode == "picker" && Environment.GetEnvironmentVariable("PICKCARDS") != null) me.Offer = Array.ConvertAll(Environment.GetEnvironmentVariable("PICKCARDS").Split(','), n => U(n));
      else if (mode == "picker" && Environment.GetEnvironmentVariable("MILESTONE") != null) { host.Game.GrantUpgrade(1, U("Scholar")); host.Game.GrantUpgrade(1, U("Scholar")); host.Game.GrantUpgrade(1, U("Second Helping")); me.Offer = new[] { U("Scholar"), U("Second Helping"), U("Wide Choice") }; me.OfferMilestone = true; }  // MILESTONE=1: a milestone pick
      else if (mode == "picker") me.Offer = Environment.GetEnvironmentVariable("REVOLVER") != null ? new[] { U("Multishot"), U("Hollow Points"), U("Barrage") } : Environment.GetEnvironmentVariable("PARRYCARDS") != null ? new[] { U("Reinforced Guard"), U("Counterstance"), U("Wide Guard") } : new[] { U("Hollow Points"), U("Multishot"), U("Blink") };
      else if (mode == "legendary") { me.Offer = new[] { U("Warden's Guard"), U("Stun Gun"), U("Searchlight") }; me.OfferLegendary = true; }
      else { me.Offer = Upgrades.ModOffer(OfferCtx.Of(me, false)); me.OfferMod = true; }
      me.PendingPicks = 2;
      typeof(ArenaApp).GetField("selectedCard", f2).SetValue(app, 1);
      typeof(ArenaApp).GetField("shownOffer", f2).SetValue(app, string.Join(",", Array.ConvertAll(me.Offer, x => x.ToString())) + "/" + me.Taken.Count);
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
