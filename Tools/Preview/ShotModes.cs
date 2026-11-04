using System; using System.IO; using System.Reflection; using System.Threading; using Arena; using UnityEngine;
// Shot.cs screens: DrawMode draws the screen named by the mode argument (hud, gameover, dev, pause, guide, bg,
// lobby, kitgrid, practicehud, labsetup, labpanel, tab, shop, picker, ...).
static partial class Shot {
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
      typeof(ArenaApp).GetMethod("DrawHud", f2).Invoke(app, null);
      if (recallAt >= 0f) typeof(ArenaApp).GetMethod("DrawRecallFade", f2).Invoke(app, null);
      if (launchAt >= 0f) { typeof(ArenaApp).GetMethod("DrawLaunchWave", f2).Invoke(app, null); typeof(ArenaApp).GetMethod("DrawLobbyLeaving", f2).Invoke(app, null); }
    }
    else if (mode == "gameover" || mode == "victory")
    {
      // The end of a run (VICTORY=1 or mode victory: a win): sample numbers for each player.
      var gg = host.Game; gg.State = mode == "victory" ? MatchState.Victory : MatchState.GameOver; gg.RunStatsReady = true; gg.RunTime = 286f; gg.Score = 546;
      int k = 0;
      foreach (var pl in gg.Players.Values)
      {
        pl.Run.DamageDealt = k == 0 ? 11900f : 8400f; pl.Run.Kills = k == 0 ? 100 : 64; pl.Run.EliteKills = 3; pl.Run.BiggestHit = k == 0 ? 15f : 42f;
        pl.Run.ShotsFired = 500; pl.Run.ShotsHit = k == 0 ? 205 : 260; pl.Run.HealingDone = k == 0 ? 0f : 640f; pl.Run.Revives = k; pl.Run.DamageTaken = k == 0 ? 246f : 180f;
        pl.Run.Downs = 1 - k; pl.Run.AbilitiesUsed = 9 + 4 * k; pl.Level = 5 + k; k++;
      }
      // The rank strip: the profile that PreviewSaves makes is rank 8; the run's gain is worked out from where it started.
      PreviewSaves();
      var before = new Profile { Xp = Rank.XpAt(Environment.GetEnvironmentVariable("RANKUP") != null ? 7 : 8) + (Environment.GetEnvironmentVariable("RANKUP") != null ? 900 : 300) };
      gg.LastGain = before.AwardXp(Rank.Award(mode == "victory", 10, 2, 1, RunMod.ToughCrowd | RunMod.Nightfall, DifficultyLevel.Normal), 11, mode == "victory" ? 1 : 0);
      Event.current.type = EventType.Repaint; set("glMaterial", new Material(null)); GL.ToUi = true;  // (the rank report draws a reward preview with GL)
      if (Environment.GetEnvironmentVariable("RANKPAGE") != null) { set("rankGainSeen", gg.LastGain); set("resultsRankPage", true); set("rankPageAt", float.Parse(Environment.GetEnvironmentVariable("RANKPAGE"), System.Globalization.CultureInfo.InvariantCulture)); }  // RANKPAGE=<start time>: the rank report, that many seconds in (-10: all of it)
      typeof(ArenaApp).GetMethod("DrawRunSummary", f2).Invoke(app, null);
    }
    else if (mode == "dev")
    {
      typeof(ArenaApp).GetMethod("DrawHud", f2).Invoke(app, null);
      host.Game.DevSetStat(1, Array.FindIndex(Game.StatFields, x => x.Name == "DmgMult"), 2.5f);
      host.Game.DevSetGod(1, true);
      set("devWave", 10);
      if (Environment.GetEnvironmentVariable("DEVTAB") != null) set("devTab", int.Parse(Environment.GetEnvironmentVariable("DEVTAB")));
      if (Environment.GetEnvironmentVariable("FILTER") != null) set("devFilter", Environment.GetEnvironmentVariable("FILTER"));
      Event.current.type = EventType.Repaint; set("glMaterial", new Material(null)); GL.ToUi = true;
      typeof(ArenaApp).GetMethod("DrawDevMenu", f2).Invoke(app, null);
      GL.ToUi = false;
    }
    else if (mode == "pause")
    {
      set("sounds", System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(SoundBank)));
      typeof(ArenaApp).GetMethod("DrawHud", f2).Invoke(app, null);
      // PAUSEANIM=0..1: how far the menu has opened (PauseGlitch.cs; default fully open). The torn slices need the
      // grabbed frame, so the preview shows the rest: the dim, scanlines, the flickering panel and the typed title.
      set("pauseMenuOpen", true);
      if (Environment.GetEnvironmentVariable("PAUSECONFIRM") != null) set("pauseConfirmLobby", true);  // Leave to lobby's "are you sure" (MenuDrawer.cs)
      if (Environment.GetEnvironmentVariable("PAUSESET") != null) { set("pauseSettings", true); set("settingsGroup", int.Parse(Environment.GetEnvironmentVariable("PAUSESET"))); set("drawerKey", "pause-settings"); }
      set("pauseAnim", float.Parse(Environment.GetEnvironmentVariable("PAUSEANIM") ?? "1"));
      host.Screen = Environment.GetEnvironmentVariable("PAUSECLOSE") != null ? AppScreen.Menu : AppScreen.Game;  // in a match (PAUSECLOSE: closing, at PAUSEANIM)
      Event.current.type = EventType.Repaint; set("glMaterial", new Material(null)); GL.ToUi = true;
      typeof(ArenaApp).GetMethod("DrawPauseMenu", f2).Invoke(app, null);
      GL.ToUi = false;
    }
    else if (mode == "guide")
    {
      // The pause menu's Field guide (FieldGuide.cs). GUIDE=page (0 Bestiary, 1 Classes, 2 Weapons, 3 Abilities, 4 Upgrades),
      // GUIDESEL=key (an EnemyType, "role/kit", a WeaponId, an AbilityId or an upgrade index), GUIDESEARCH=text, GUIDESCROLL=px.
      set("sounds", System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(SoundBank)));
      typeof(ArenaApp).GetMethod("DrawHud", f2).Invoke(app, null);
      int page = int.Parse(Environment.GetEnvironmentVariable("GUIDE") ?? "0");
      set("fieldGuideOpen", true);
      set("guidePage", page); set("guideScrollToSel", true);
      if (Environment.GetEnvironmentVariable("GUIDESEL") != null) ((string[])typeof(ArenaApp).GetField("guideSel", f2).GetValue(app))[page] = Environment.GetEnvironmentVariable("GUIDESEL");
      if (Environment.GetEnvironmentVariable("GUIDESEARCH") != null) set("guideSearch", Environment.GetEnvironmentVariable("GUIDESEARCH"));
      // GUIDEOPEN=<seconds>: the guide that far into its opening (it rises in over 0.4 s).
      if (Environment.GetEnvironmentVariable("GUIDEOPEN") != null) set("guideOpenAt", Time.unscaledTime - float.Parse(Environment.GetEnvironmentVariable("GUIDEOPEN"), System.Globalization.CultureInfo.InvariantCulture));
      Event.current.type = EventType.Repaint; set("glMaterial", new Material(null)); GL.ToUi = true;
      int mark = GUI.Log.Count;
      typeof(ArenaApp).GetMethod("DrawFieldGuideScreen", f2).Invoke(app, null);  // a first pass measures the entry's height
      if (Environment.GetEnvironmentVariable("GUIDESCROLL") != null) set("guideDetailScroll", float.Parse(Environment.GetEnvironmentVariable("GUIDESCROLL")));
      GUI.Log.RemoveRange(mark, GUI.Log.Count - mark);
      typeof(ArenaApp).GetMethod("DrawFieldGuideScreen", f2).Invoke(app, null);
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
      host.Lobby[2] = new LobbyEntry { Id = 2, Name = "Aleon", Role = Role.Dps, Kit = Kit.Elementalist, Weapon = 0, Ability = 1, Rank = 27, Frame = 2 };
      if (Environment.GetEnvironmentVariable("SOLO") != null) host.Lobby.Remove(2);
      // RESUME=1: the lobby continuing a saved run (the host in its seat, Aleon's seat still free; SavesUi.cs).
      if (Environment.GetEnvironmentVariable("RESUME") != null) {
        PreviewSaves(); host.Resume = SaveStore.List()[0];
        host.Lobby.Remove(2); host.Lobby[1].Seat = 0; host.Lobby[1].Role = Role.Tank; host.Lobby[1].Kit = Kit.Knight; host.Lobby[1].Weapon = 1; host.Lobby[1].Ability = 2; host.Lobby[1].Ready = false;
      }
      if (Environment.GetEnvironmentVariable("LOBBYTAB") != null) { set("lobbyTab", int.Parse(Environment.GetEnvironmentVariable("LOBBYTAB"))); host.Modifiers = RunMod.GlassCannon | RunMod.Frenzy | RunMod.Lucky | RunMod.Hunted | RunMod.SecondWind; }
      // HANGOUT=<trail,trail,trail>: the lobby's Hangout tab (LobbyRoom.cs) with three characters, each wearing that trail id and
      // mid-stride (so the trail shows), e.g. HANGOUT=trail-void,trail-prism,trail-frost.
      if (Environment.GetEnvironmentVariable("HANGOUT") != null) {
        var tr = Environment.GetEnvironmentVariable("HANGOUT").Split(',');
        set("lobbyTab", 6);
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
        host.MyId = 1;
      }
      // MODSFOLD=n: the Modifiers tab with those tiers folded (a bit per ModLean: 1 Harder, 2 Easier, 4 Risky).
      set("modsFolded", int.Parse(Environment.GetEnvironmentVariable("MODSFOLD") ?? "0"));
      // DIFF=Hard (a preset) or DIFF=Custom (Hard with a few numbers changed): the lobby's Difficulty tab (LOBBYTAB=3).
      if (Environment.GetEnvironmentVariable("DIFF") != null) { var dl = (DifficultyLevel)Enum.Parse(typeof(DifficultyLevel), Environment.GetEnvironmentVariable("DIFF")); host.Diff = Difficulty.Of(dl == DifficultyLevel.Custom ? DifficultyLevel.Hard : dl); if (dl == DifficultyLevel.Custom) { host.Diff.Set(DiffKnob.EnemyDamage, 1.8f); host.Diff.Set(DiffKnob.Regen, 0.5f); host.Diff.Set(DiffKnob.ReviveTime, 2.5f); } }
      // HUNT=1: Boss Hunt mode, its tab (the Warden's hunt open, won once).
      if (Environment.GetEnvironmentVariable("HUNT") != null) { host.HuntMode = true; var hpr = host.Game.ProfileOverride = new Profile(); hpr.BossBeaten(EnemyType.Boss, 5); hpr.HuntEnded(EnemyType.Boss, true, 312f); hpr.HuntEnded(EnemyType.Boss, false, 200f); }
      if (Environment.GetEnvironmentVariable("CLIENT") != null) host.MyId = 2;
      if (Environment.GetEnvironmentVariable("LOBBYSETTINGS") != null) { set("settingsOpen", true); set("sounds", System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(SoundBank))); }
      if (mode == "lobbypr") {
        System.Collections.Generic.List<string> pr;
        host.PracticeMode = true;
        host.Plan = PracticePlan.Parse("loop: no\nwave 3: Grunt x4, Sniper x2, Swarmer x6\nwave 6: Spinner x2, Medic x1, Shielder x2, Charger x1\nwave 10: The Warden x1, Grunt x3\nwave 15: Twin Reapers x1\nwave 20: The Singularity x1", out pr);
        host.OthersCanEdit = Environment.GetEnvironmentVariable("EDIT") != null;
        host.PlanEditor = "Aleon"; host.PlanEditorSel = 1;
        set("practiceSel", 1);
      }
      var hv = Environment.GetEnvironmentVariable("MOUSE");
      if (hv != null) { var xy = hv.Split(','); Event.current.mousePosition = new Vector2(float.Parse(xy[0]), float.Parse(xy[1])); }
      Event.current.type = EventType.Repaint;
      set("glMaterial", new Material(null));
      set("addressList", "100.123.48.122, 192.168.1.163");
      if (Environment.GetEnvironmentVariable("INFO") != null) set("connInfoOpen", true);
      // The hero card's live mini-arena: run it for a moment first. LOADOUT=role,kit,weapon,ability; ABILITY=n hovers that ability.
      var lo = Environment.GetEnvironmentVariable("LOADOUT");
      if (lo != null) { var q = lo.Split(','); var le = host.Lobby[host.MyId]; le.Role = (Role)int.Parse(q[0]); le.Kit = (Kit)int.Parse(q[1]); le.Weapon = byte.Parse(q[2]); le.Ability = byte.Parse(q[3]); }
      var ab = Environment.GetEnvironmentVariable("ABILITY");
      if (ab != null) set("abilityHover", int.Parse(ab));
      {
        var le = host.Lobby[host.MyId];
        int shown = ab != null ? int.Parse(ab) : le.Ability;
        var sc = new Showcase();
        bool ultDemo = Environment.GetEnvironmentVariable("ULTDEMO") != null;  // ULTDEMO=1: the ultimate is being previewed
        if (ultDemo) set("ultDemoUntil", 1e9f);
        sc.Show(le.Role, le.Kit, le.Weapon, Upgrades.RoleAbility(le.Role, le.Kit, shown), ab != null || ultDemo, ultDemo);
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
      if (lobbyBack != null) { set("returnT", float.Parse(lobbyBack, System.Globalization.CultureInfo.InvariantCulture)); Event.current.type = EventType.Repaint; set("glMaterial", new Material(null)); typeof(ArenaApp).GetMethod("DrawLobbyReturning", f2).Invoke(app, null); }
      else if (lobbyOut != null) { set("leaveT", float.Parse(lobbyOut, System.Globalization.CultureInfo.InvariantCulture)); Event.current.type = EventType.Repaint; set("glMaterial", new Material(null)); typeof(ArenaApp).GetMethod("DrawLobbyLeavingToMenu", f2).Invoke(app, null); }
      else if (lobbyIn != null) { set("enterT", float.Parse(lobbyIn, System.Globalization.CultureInfo.InvariantCulture)); Event.current.type = EventType.Repaint; set("glMaterial", new Material(null)); typeof(ArenaApp).GetMethod("DrawLobbyEntering", f2, null, Type.EmptyTypes, null).Invoke(app, null); }
      else typeof(ArenaApp).GetMethod("DrawLobby", f2).Invoke(app, null);
      if (Environment.GetEnvironmentVariable("LOBBYMENU") != null) { set("lobbyMenuOpen", true); set("winOpenAt", -10f); set("sounds", System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(SoundBank))); if (Environment.GetEnvironmentVariable("LOBBYMENU") == "settings") set("pauseSettings", true); if (Environment.GetEnvironmentVariable("LOBBYMENU") == "howto") set("pauseHowTo", true); Event.current.type = EventType.Repaint; typeof(ArenaApp).GetMethod("DrawLobbyMenu", f2).Invoke(app, null); }  // Esc's menu (MenuDrawer.cs)
      if (Environment.GetEnvironmentVariable("LOBBYSETTINGS") != null) { Ui.Fill(new Rect(0, 0, 1280, 720), new Color(0.01f, 0.01f, 0.03f, 0.75f)); typeof(ArenaApp).GetMethod("DrawSettings", f2).Invoke(app, null); }
      if (te != null) endTe.Invoke(app, null);
      GL.ToUi = false;
    }
    else if (mode == "menu" || mode == "howto" || mode == "continue" || mode == "records" || mode == "cosmetics" || mode == "settings")
    {
      // continue / records: the saved-runs list and the Records page, with made-up saves and a profile (SavesUi.cs).
      if (mode == "continue" || mode == "records" || mode == "cosmetics" || mode == "menu") PreviewSaves();
      if (mode == "continue") { typeof(ArenaApp).GetMethod("OpenContinue", f2).Invoke(app, null); if (Environment.GetEnvironmentVariable("NOSAVES") != null) set("savedRuns", new System.Collections.Generic.List<RunSave>()); }
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
      if (Environment.GetEnvironmentVariable("SECTION") != null) { int sec = int.Parse(Environment.GetEnvironmentVariable("SECTION")); set("settingsGroup", sec); set("recordsSection", sec); set("cosmeticsSection", sec); set("howToTopic", sec); set("panelKey", (mode == "howto" ? "howto" : mode) + sec); set("panelAt", -10f); }
      set("drawerKey", mode == "howto" ? "howto" : mode); set("drawerAt", -10f);
      // WINT=<s>: that far into the window's opening; WINCLOSE=0..1: that far through closing (WindowAnim.cs).
      if (Environment.GetEnvironmentVariable("WINT") != null) set("winOpenAt", Time.unscaledTime - float.Parse(Environment.GetEnvironmentVariable("WINT"), System.Globalization.CultureInfo.InvariantCulture));
      if (Environment.GetEnvironmentVariable("WINCLOSE") != null) { set("winClosingAt", 0f); set("winCloseK", float.Parse(Environment.GetEnvironmentVariable("WINCLOSE"), System.Globalization.CultureInfo.InvariantCulture)); }
      // MENUINTRO=<seconds>: the main menu that far into its intro (MenuIntro.cs); JOINOPEN=1: Join's address row open.
      if (Environment.GetEnvironmentVariable("MENUINTRO") != null) { set("menuIntroDone", true); set("menuIntroT", float.Parse(Environment.GetEnvironmentVariable("MENUINTRO"), System.Globalization.CultureInfo.InvariantCulture)); }
      if (Environment.GetEnvironmentVariable("JOINOPEN") != null) set("menuJoinOpen", true);
      if (Environment.GetEnvironmentVariable("MENUTIP") != null) set("menuTipSeed", int.Parse(Environment.GetEnvironmentVariable("MENUTIP")));  // which line the chip shows
      typeof(ArenaApp).GetMethod(mode == "menu" ? "DrawMenu" : mode == "continue" ? "DrawContinue" : mode == "records" ? "DrawRecords" : mode == "cosmetics" ? "DrawCosmetics" : mode == "settings" ? "DrawSettings" : "DrawHowToPlay", f2).Invoke(app, null);
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
      typeof(ArenaApp).GetMethod("DrawHud", f2).Invoke(app, null);
    }
    else if (mode == "labsetup")
    {
      var lab = (BossLab)typeof(ArenaApp).GetField("lab", f2).GetValue(app);
      lab.Wave = 20; lab.PlayerCount = 3; lab.Seats[1].Kit = Kit.Elementalist; lab.Seats[2].Kit = Kit.Knight; lab.Seats[2].Weapon = 1;
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
      set("waveEdOpen", true);
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
        // STATSFROM=n: caught mid switch from tab n, STATSAT seconds in (the zoom-through; the stub ignores GUI.matrix, so it shows the fade only).
        if (Environment.GetEnvironmentVariable("STATSFROM") != null)
        {
          set("statsShown", int.Parse(Environment.GetEnvironmentVariable("STATSTAB")));
          set("statsFrom", int.Parse(Environment.GetEnvironmentVariable("STATSFROM")));
          set("statsSwitchAt", Time.unscaledTime - float.Parse(Environment.GetEnvironmentVariable("STATSAT") ?? "0.12"));
        }
        Event.current.type = EventType.Repaint; set("glMaterial", new Material(null)); GL.ToUi = true;
        typeof(ArenaApp).GetMethod("DrawRunStatsScreen", f2).Invoke(app, new object[] { me, Environment.GetEnvironmentVariable("PAUSE") != null });
        GL.ToUi = false;
      }
    }
    else if (mode == "shop")
    {
      // The Bazaar's shop (ShopUi.cs): SHOPSEL=n selects a card, REMOVE=1 opens the remove list, GIVE=1 the Give popup.
      Func<string, int> U = n => Array.FindIndex(Upgrades.All, u => u.Name == n);
      foreach (var n in new[] { "Hollow Points", "Fortitude", "Multishot", "Thick Skin", "Riposte" })
        host.Game.GrantUpgrade(1, U(n));
      var g = host.Game;
      g.CurrentPath = PathId.Bazaar; g.PathFrom = 11; g.PathTo = 14; g.Wave = 14; g.State = MatchState.Intermission;
      typeof(Game).GetMethod("OpenShop", f2).Invoke(g, null);
      g.CurrentPath = PathId.None;
      int k = 0;
      foreach (var p in g.Players.Values) { p.Gold = k == 0 ? 252 : k == 1 ? 112 : 162; p.ShopDone = k == 2; k++; }
      me.ShelfSold = 1 << 2; me.Hp = me.MaxHp * 0.6f;
      g.ShopNotices.Add(new ShopNotice { Text = "Mira gave you 40 gold", Life = 2f });
      int sel = Environment.GetEnvironmentVariable("SHOPSEL") != null ? int.Parse(Environment.GetEnvironmentVariable("SHOPSEL")) : 0;
      typeof(ArenaApp).GetField("shopSel", f2).SetValue(app, sel);
      if (Environment.GetEnvironmentVariable("REMOVE") != null) typeof(ArenaApp).GetField("shopRemoveOpen", f2).SetValue(app, true);
      if (Environment.GetEnvironmentVariable("GIVE") != null) foreach (var p in g.Players.Values) if (p.Id != me.Id) typeof(ArenaApp).GetField("shopGiveTo", f2).SetValue(app, p.Id);
      typeof(ArenaApp).GetMethod("DrawShop", f2).Invoke(app, new object[] { me, 1f });
    }
    else
    {
      Func<string, int> U = n => Array.FindIndex(Upgrades.All, u => u.Name == n);
      if (mode == "picker" && Environment.GetEnvironmentVariable("PRIZE") != null) { me.Offer = Upgrades.PrizeOffer(new System.Random(3), me.Role, me.RoleAbility, me.Taken); me.OfferPrize = true; }  // a challenge room's prize
      else if (mode == "picker" && Environment.GetEnvironmentVariable("KEYS") != null) { host.Game.GrantUpgrade(1, U("Overdrive")); me.Offer = new[] { U("Shockwave"), U("Blink"), U("Nova") }; }
      else if (mode == "picker" && Environment.GetEnvironmentVariable("PICKCARDS") != null) me.Offer = Array.ConvertAll(Environment.GetEnvironmentVariable("PICKCARDS").Split(','), n => U(n));
      else if (mode == "picker") me.Offer = Environment.GetEnvironmentVariable("REVOLVER") != null ? new[] { U("Multishot"), U("Hollow Points"), U("Barrage") } : Environment.GetEnvironmentVariable("PARRYCARDS") != null ? new[] { U("Reinforced Guard"), U("Counterstance"), U("Wide Guard") } : new[] { U("Hollow Points"), U("Multishot"), U("Blink") };
      else if (mode == "legendary") { me.Offer = new[] { U("Warden's Guard"), U("Stun Gun"), U("Searchlight") }; me.OfferLegendary = true; }
      else { me.Offer = Upgrades.ModOffer(me.Role, me.RoleAbility, me.Taken); me.OfferMod = true; }
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
