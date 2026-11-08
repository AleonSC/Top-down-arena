using System; using System.IO; using System.Reflection; using System.Threading; using Arena; using UnityEngine;
// Renders one screen to GL triangles and an IMGUI log (shot.sh turns them into a PNG). Main sets up a match and
// runs it; ShotScenes.cs poses it (the environment flags), ShotModes.cs draws the chosen screen.
static partial class Shot {
  // The preview's "now" for made-up saves and replays: fixed, so their dates (and uishots.sh -r compares) don't change.
  public static readonly DateTime Now = new DateTime(2026, 11, 7, 18, 0, 0, DateTimeKind.Utc);
  static void Step(Session s) { s.Poll(); s.Game.FixedStep(1f/60f, new PlayerInput()); s.Game.Update(1f/60f); }
  static bool heraldOn, eclipseOn;
  static bool devourOn;
  // Two made-up saved runs and a profile in a temp folder, for the menu's Continue list, Records and the lobby.
  static void PreviewSaves() {
    string dir = Path.Combine(Path.GetTempPath(), "arena-preview-saves");
    if (Directory.Exists(dir)) Directory.Delete(dir, true);
    SaveStore.Folder = dir; Profile.Folder = dir; Profile.Reload();
    var a = new RunSave { Id = "a", SavedAt = Shot.Now.AddMinutes(-40), Wave = 23, Score = 18450, RunTime = 2710f, Mods = RunMod.Lucky | RunMod.Frenzy, CurrentPath = PathId.Bazaar, PathFrom = 21, PathTo = 24 };
    a.Seats.Add(new SeatSave { Name = "Anthony", Role = Role.Tank, Kit = Kit.Knight, Weapon = 1, Ability = 2, Level = 17 });
    a.Seats.Add(new SeatSave { Name = "Aleon", Role = Role.Dps, Kit = Kit.Elementalist, Weapon = 0, Ability = 1, Level = 16 });
    var b = new RunSave { Id = "b", SavedAt = Shot.Now.AddDays(-2), Wave = 7, Score = 3120, RunTime = 690f };
    b.Seats.Add(new SeatSave { Name = "Anthony", Role = Role.Healer, Kit = Kit.Ninja, Weapon = 2, Ability = 0, Level = 6 });
    SaveStore.Write(a); SaveStore.Write(b);
    var pr = new Profile { RunsStarted = 14, RunsWon = 1, RunsLost = 9, BestWave = 50, BestScore = 41200, BestEndlessWave = 0, PlayTime = 41000f, Kills = 6812, BossKills = 52, Downs = 37, Revives = 21, DamageDealt = 2.3e6f, HealingDone = 81000f };
    foreach (var t in new[] { EnemyType.Boss, EnemyType.Boss, EnemyType.HiveQueen, EnemyType.TwinA, EnemyType.Singularity, EnemyType.Colossus, EnemyType.Duelist, EnemyType.Lich, EnemyType.StormHerald, EnemyType.Devourer, EnemyType.Eclipse }) pr.BossBeaten(t, 10);
    pr.RunsAs[Profile.ClassKey(Role.Tank, Kit.Knight)] = 6; pr.RunsAs[Profile.ClassKey(Role.Dps, Kit.Elementalist)] = 3; pr.RunsAs[Profile.ClassKey(Role.Healer, Kit.Ninja)] = 1; pr.WinsAs[Profile.ClassKey(Role.Tank, Kit.Knight)] = 1; pr.RoomsPlayed["Defend"] = 5; pr.RoomsWon["Defend"] = 4; pr.RoomsPlayed["MiniBoss"] = 3; pr.RoomsWon["MiniBoss"] = 2; pr.RoomsPlayed["KillRush"] = 3; pr.RoomsWon["KillRush"] = 3; pr.RoomsPlayed["HoldZones"] = 4; pr.RoomsWon["HoldZones"] = 2; pr.RoomsPlayed["Mirror"] = 2; pr.RoomsWon["Mirror"] = 1;
    // Rank (Rank.cs): rank 8 with a few runs logged, as after the run in the "gameover" preview (RANKUP=1: just ranked up).
    pr.Xp = Rank.XpAt(Environment.GetEnvironmentVariable("RANK") != null ? int.Parse(Environment.GetEnvironmentVariable("RANK")) : 8) + 115; pr.Trail = Environment.GetEnvironmentVariable("TRAIL") ?? "trail-ember";  // RANK=40 TRAIL=trail-prism for the cosmetics window
    pr.Recent.Add(new RankLog { Wave = 11, Result = 0, Xp = 810, MultPct = 150, Mods = "Tough Crowd|Nightfall" });
    pr.Recent.Add(new RankLog { Wave = 23, Result = 1, Xp = 3120, MultPct = 175, Mods = "Death Throes|Frenzy|Tough Crowd" });
    pr.Recent.Add(new RankLog { Wave = 14, Result = 0, Xp = 440, MultPct = 100, Mods = "" });
    // Recent runs (RunGraphs.cs): eight made-up runs with curves, newest first, plus the three older log lines above.
    var rng = new System.Random(5);
    string[] modes = { "Endless", "Campaign", "Endless", "Campaign", "Endless", "Campaign", "Endless", "Endless" };
    string[] cls = { "Knight Tank", "Soldier DPS", "Elementalist DPS", "Ninja Healer", "Knight Tank", "Soldier DPS", "Knight Tank", "Ninja Healer" };
    int[] mins = { 21, 14, 15, 9, 18, 12, 7, 16 }, party = { 3, 1, 2, 1, 3, 1, 1, 2 };
    bool[] won = { false, true, false, true, false, false, false, true };
    var older = new System.Collections.Generic.List<RankLog>(pr.Recent);
    pr.Recent.Clear();
    for (int i = 0; i < 8; i++) {
      var tl = new RunTimeline { Id = "2026110" + (8 - i) + "-120000-ab" + i, Mode = modes[i], Class = cls[i], Diff = "Normal", Mods = i % 2 == 0 ? "Tough Crowd" : "", Won = won[i], Wave = 6 + mins[i] / 2 };
      string[] names = { "Anthony", "Mira", "Jonas" };
      for (int pl = 0; pl < party[i]; pl++) tl.PlayerIndex(pl + 1, names[pl], pl == 0);
      var tot = new RunStats[party[i]]; for (int pl = 0; pl < tot.Length; pl++) tot[pl] = new RunStats();
      var hp = new float[party[i]]; for (int pl = 0; pl < hp.Length; pl++) hp[pl] = 100f;
      for (float t = 0f; t <= mins[i] * 60f; t += 10f) {
        float inten = 0.5f + t / (mins[i] * 60f) * 0.9f + 0.25f * (float)Math.Sin(t / 30f);
        var smp = tl.Begin(t, 1 + (int)(t / 70f), t / 70f % 1f > 0.55f && (int)(t / 70f) % 5 == 4);
        for (int pl = 0; pl < tot.Length; pl++) {
          float dmg = (700f + 1200f * inten) * (0.7f + (float)rng.NextDouble() * 0.6f) / (1 + pl * 0.3f);
          float hit = (float)rng.NextDouble() < 0.22f ? 380f * inten : 60f * (float)rng.NextDouble();
          tot[pl].DamageDealt += dmg; tot[pl].DamageTaken += hit; tot[pl].HealingDone += pl == 0 && i == 3 ? 300f : 30f; tot[pl].Kills += (int)(dmg / 150f);
          tot[pl].ShotsFired += 90; tot[pl].ShotsHit += (int)(90 * (0.35f + 0.4f * (float)rng.NextDouble()));
          hp[pl] = Math.Max(4f, Math.Min(100f, hp[pl] - hit / 14f + (float)rng.NextDouble() * 6f + (rng.NextDouble() < 0.08 ? 18f : 0f)));
          if (hp[pl] < 14f && rng.NextDouble() < 0.3) { tot[pl].Downs++; hp[pl] = 60f; }
          tl.Set(smp, pl, tot[pl], hp[pl]);
        }
      }
      tl.Seconds = mins[i] * 60f;
      string fileId = RunTimelineStore.Save(tl, 99);
      pr.Recent.Add(new RankLog { Wave = tl.Wave, Result = won[i] ? 1 : 0, Xp = 300 + (int)(tl.Wave * 38), MultPct = 100, Mods = tl.Mods, Class = cls[i], Mode = modes[i], Seconds = tl.Seconds, Dealt = tot[0].DamageDealt, Taken = tot[0].DamageTaken, Heal = tot[0].HealingDone, Players = party[i], File = i == 6 ? "" : fileId });
    }
    pr.Recent.AddRange(older);
    File.WriteAllText(Path.Combine(dir, "profile.txt"), pr.ToText()); Profile.Reload();
  }
  // A few made-up profiles in a temp folder, for the "Who's playing?" screen (ProfilesUi.cs).
  static void PreviewWho() {
    string dir = Path.Combine(Path.GetTempPath(), "arena-preview-who");
    if (Directory.Exists(dir)) Directory.Delete(dir, true);
    ProfileStore.Init(dir, "");
    if (Environment.GetEnvironmentVariable("EMPTY") != null) return;
    var names = Environment.GetEnvironmentVariable("TWO") != null ? new[] { "Anthony", "Sam" } : new[] { "Anthony", "Sam", "Little Bee" };
    int[] ranks = { 14, 6, 3 };
    for (int i = 0; i < names.Length; i++) {
      string id = ProfileStore.Create(names[i]);
      new Profile { Name = names[i], Xp = Rank.XpAt(ranks[i]) + 40, RunsWon = ranks[i] / 3, RunsLost = ranks[i] * 2 }.SaveTo(Path.Combine(dir, "profiles", id));
    }
    ProfileStore.SetActive(ProfileStore.List()[0].Id);
  }
  // Shared by Main and the scene and mode code (ShotScenes.cs, ShotModes.cs).
  static string[] args;
  static int wave, players;
  static float seconds;
  static string melee, mode;
  static Kit meleeKit;
  static Session host;
  static System.Collections.Generic.List<Session> others;
  static Player me;
  static ArenaApp app;
  static BindingFlags f2;
  static Action<string, object> set;
  static float launchAt, recallAt, endingAt;
  static void Main(string[] args) {
    if (Environment.GetEnvironmentVariable("RANDOM") == null) Game.SeedForTests(1);  // the same rolls every run, so uishots.sh -r can compare pixels (RANDOM=1: as random as the game)
    Shot.args = args; wave = int.Parse(args[1]); seconds = float.Parse(args[2]); players = int.Parse(args[3]);
    UnityEngine.Screen.width = 1280; UnityEngine.Screen.height = 720;
    if (Environment.GetEnvironmentVariable("CRTTHEME") != null) CrtUi.Theme = int.Parse(Environment.GetEnvironmentVariable("CRTTHEME"));  // CRT colour (CrtUi.ThemeNames)
    host = new Session { AutoStart = players, StartWave = wave }; host.Game.Bot = true;
    melee = Environment.GetEnvironmentVariable("KNIGHT") ?? Environment.GetEnvironmentVariable("NINJA") ?? Environment.GetEnvironmentVariable("BLOOD");
    meleeKit = Environment.GetEnvironmentVariable("NINJA") != null ? Kit.Ninja : Environment.GetEnvironmentVariable("BLOOD") != null ? Kit.Hemomancer : Kit.Knight;
    host.Host(7990 + new System.Random().Next(500), "Anthony", Role.Tank, melee != null ? int.Parse(melee) : 0, 0, melee != null ? meleeKit : Kit.Elementalist);
    others = new System.Collections.Generic.List<Session>();
    string[] names = { "Aleon", "Sam" }; Role[] roles = { Role.Dps, Role.Healer }; Kit[] kits = melee != null ? new[] { meleeKit, meleeKit } : new[] { Kit.Soldier, Kit.Summoner };
    for (int i = 1; i < players; i++) { var c = new Session(); c.Game.Bot = true; c.Join("127.0.0.1", host.Port, names[i-1], roles[i-1], 0, 0, kits[i-1]); others.Add(c); }
    if (players == 1) host.StartMatch();
    for (int i = 0; i < 600 && !host.Game.Running; i++) { Step(host); foreach (var c in others) Step(c); Thread.Sleep(2); }
    // Some upgrades so the bars have content.
    for (int f = 0; f < seconds * 60; f++) {
      Step(host); foreach (var c in others) Step(c); if (f % 4 == 0) Thread.Sleep(1);
      foreach (var p in host.Game.Players.Values) { p.InvulnT = 1f; }
      if (Environment.GetEnvironmentVariable("PARRY") != null && f == (int)(seconds * 60) - 50) { host.Game.HoldParry = true; host.Game.Bot = false; }
      if (Environment.GetEnvironmentVariable("PARRY") != null && f > (int)(seconds * 60) - 60) foreach (var e in host.Game.Enemies.Values) if (e.Type == EnemyType.Boss) { host.Game.Players[1].Pos = e.Pos + new V2(-60f, 180f); host.Game.Players[1].Aim = (e.Pos - host.Game.Players[1].Pos).Angle; }
      // SING=<health fraction>: hold the Singularity there (so the shot lands in that stage); SINGTURN=<turn> skips its
      // turns until that one (Pull, Collapse, Lasers, Cage, Slingshot, TwinLasers, Hawking, WhiteHole, Split, Last).
      if (Environment.GetEnvironmentVariable("SING") != null) foreach (var e in host.Game.Enemies.Values) if (e.Type == EnemyType.Singularity)
      {
        e.Hp = e.MaxHp * float.Parse(Environment.GetEnvironmentVariable("SING"));
        var want = Environment.GetEnvironmentVariable("SINGTURN");
        if (want != null && e.Sing != null && e.Sing.TransT <= 0f && e.Sing.Turn.ToString() != want && f < (int)(seconds * 60) - 60) e.Sing.TurnT = 999f;
      }
      // HERALD=<health fraction>: hold the Storm Herald there; HERALDTURN=<turn> starts that turn HERALDT seconds (default
      // 2.5) before the shot (Strikes, Rods, Thunderclap, Crosswind, BallLightning, Downburst, StormFront, StrikeRain, RodCage, Eye).
      if (Environment.GetEnvironmentVariable("HERALD") != null) foreach (var e in host.Game.Enemies.Values) if (e.Type == EnemyType.StormHerald)
      {
        e.Hp = e.MaxHp * float.Parse(Environment.GetEnvironmentVariable("HERALD"));
        var want = Environment.GetEnvironmentVariable("HERALDTURN");
        int from = (int)(seconds * 60) - (int)(60 * float.Parse(Environment.GetEnvironmentVariable("HERALDT") ?? "2.5"));
        if (want != null && e.Herald != null && e.Herald.TransT <= 0f && !heraldOn && f >= from - 10)
        {
          if (e.Herald.Turn.ToString() == want && f >= from) heraldOn = true;
          else e.Herald.TurnT = 999f;
        }
      }
      // ECLIPSE=<health fraction>: hold the Eclipse there; ECLIPSETURN=<turn> starts that turn ECLIPSET seconds (default 2.5)
      // before the shot (Spiral, CoronaRings, Flares, LunarWake, Flash, Gravity, Crescents, ClosingUmbra, DarkSpiral,
      // EchoColossus, EchoLich, EchoHerald, EchoSingularity, FranticSpiral, ShadowNova, SolarStorm, Totality).
      if (Environment.GetEnvironmentVariable("ECLIPSE") != null) foreach (var e in host.Game.Enemies.Values) if (e.Type == EnemyType.Eclipse)
      {
        e.Hp = e.MaxHp * float.Parse(Environment.GetEnvironmentVariable("ECLIPSE"));
        var want = Environment.GetEnvironmentVariable("ECLIPSETURN");
        int from = (int)(seconds * 60) - (int)(60 * float.Parse(Environment.GetEnvironmentVariable("ECLIPSET") ?? "2.5"));
        if (want != null && e.Eclipse != null && e.Eclipse.TransT <= 0f && !eclipseOn && f >= from - 10)
        {
          if (e.Eclipse.Turn.ToString() == want && f >= from) eclipseOn = true;
          else e.Eclipse.TurnT = 999f;
        }
      }
      // DUEL=<health fraction>: hold the Duelist there; DUELTURN=<turn> skips its turns until that one
      // (Volleys, Lunge, Guard, Blink, Masquerade, Crossing, LungeChain, Salute, Flurry, Flourish).
      if (Environment.GetEnvironmentVariable("DUEL") != null) foreach (var e in host.Game.Enemies.Values) if (e.Type == EnemyType.Duelist)
      {
        e.Hp = e.MaxHp * float.Parse(Environment.GetEnvironmentVariable("DUEL"));
        var want = Environment.GetEnvironmentVariable("DUELTURN");
        if (want != null && e.Duel != null && e.Duel.TransT <= 0f && e.Duel.Turn.ToString() != want && e.Duel.Dash == DuelDash.None && e.Duel.SaluteT <= 0f && f < (int)(seconds * 60) - 90) e.Duel.TurnT = 999f;
      }
      // COL=<health fraction>: hold the Colossus there (so the shot lands in that stage); COLTURN=<turn> skips its turns
      // until that one (Stomps, Boulders, Fissures, Charge, Rockslide, Aftershocks, LavaStomps, Meteors, CaveIn, Last).
      if (Environment.GetEnvironmentVariable("COL") != null) foreach (var e in host.Game.Enemies.Values) if (e.Type == EnemyType.Colossus)
      {
        e.Hp = e.MaxHp * float.Parse(Environment.GetEnvironmentVariable("COL"));
        var want = Environment.GetEnvironmentVariable("COLTURN");
        if (want != null && e.Colossus != null && e.Colossus.TransT <= 0f && e.Colossus.Turn.ToString() != want && f < (int)(seconds * 60) - 60) e.Colossus.TurnT = 999f;
      }
      // DEVOUR=<health fraction>: hold the Devourer there; DEVOURTURN=<turn> starts that turn DEVOURT seconds (default
      // 2.5) before the shot (Spiral, Snap, Hungry, Inhale, Bile, Burrow, Frenzy, Jaws, Vomit, LastMeal).
      if (Environment.GetEnvironmentVariable("DEVOUR") != null) foreach (var e in host.Game.Enemies.Values) if (e.Type == EnemyType.Devourer)
      {
        e.Hp = e.MaxHp * float.Parse(Environment.GetEnvironmentVariable("DEVOUR"));
        var want = Environment.GetEnvironmentVariable("DEVOURTURN");
        int from = (int)(seconds * 60) - (int)(60 * float.Parse(Environment.GetEnvironmentVariable("DEVOURT") ?? "2.5"));
        if (want != null && e.Devour != null && e.Devour.TransT <= 0f && !devourOn && f >= from - 10)
        {
          if (e.Devour.Turn.ToString() == want) devourOn = true;
          else { e.Devour.Lunging = false; e.Devour.BurrowT = 0f; e.Devour.InhaleT = 0f; e.Devour.TurnT = 999f; }  // skip it, busy or not
        }
      }
      // WARDEN=<health fraction>: hold the Warden at that health, so the shot lands in that stage.
      if (Environment.GetEnvironmentVariable("WARDEN") != null) foreach (var e in host.Game.Enemies.Values) if (e.Type == EnemyType.Boss) e.Hp = (e.MaxHp * float.Parse(Environment.GetEnvironmentVariable("WARDEN")));
      // REAPERS=<health fraction>: the same for the Twin Reapers (or the Dual Reaper).
      if (Environment.GetEnvironmentVariable("REAPERS") != null) foreach (var e in host.Game.Enemies.Values) if (Game.IsReaper(e.Type) && !host.Game.IsSpawning(e)) e.Hp = e.MaxHp * float.Parse(Environment.GetEnvironmentVariable("REAPERS"));
      // QUEEN=<health fraction>: the same for the Hive Queen.
      if (Environment.GetEnvironmentVariable("QUEEN") != null) foreach (var e in host.Game.Enemies.Values) if (e.Type == EnemyType.HiveQueen) e.Hp = (e.MaxHp * float.Parse(Environment.GetEnvironmentVariable("QUEEN")));
      // LICH=<health fraction> (and LICHTURN=<turn name>): hold the Lich in a stage, skipping to that turn.
      if (Environment.GetEnvironmentVariable("LICH") != null) foreach (var e in host.Game.Enemies.Values) if (e.Type == EnemyType.Lich && !host.Game.IsSpawning(e))
      {
        e.Hp = e.MaxHp * float.Parse(Environment.GetEnvironmentVariable("LICH"));
        var want = Environment.GetEnvironmentVariable("LICHTURN");
        if (want != null && e.Lich != null && e.Lich.TransT <= 0f && e.Lich.Turn.ToString() != want && f < (int)(seconds * 60) - 90) e.Lich.TurnT = 999f;
      }
    }
    if (host.Game.Reapers != null) { var R = host.Game.Reapers; Console.WriteLine("reapers stage {0} turn {1} turnT {2:0.0} trans {3} {4:0.0} suck {5:0.0} trapped {6} holes {7}", R.Stage, R.Turn, R.TurnT, R.Trans, R.TransT, R.Suck, R.Trapped, R.Holes.Count); }
    foreach (var e in host.Game.Enemies.Values) if (e.Type == EnemyType.Lich) Console.WriteLine("lich stage {0} turn {1} turnT {2:0.0} prisons {3} spirits {4} sweep {5}", e.Lich?.Stage, e.Lich?.Turn, e.Lich?.TurnT, e.Lich?.Prisons.Count, e.Lich?.Spirits.Count, e.Lich?.SweepOn);
    foreach (var e in host.Game.Enemies.Values) if (e.Type == EnemyType.HiveQueen) Console.WriteLine("queen stage {0} turn {1} turnT {2:0.0} trans {3:0.0} cells {4} cloud {5}", e.Queen?.Stage, e.Queen?.Turn, e.Queen?.TurnT, e.Queen?.TransT, e.Queen?.Cells.Count, e.Queen?.CloudOn);
    // WARDENSET=exposed|stun|fan: the Warden blinded by its own light (the light on it), stunned, or warning of a fan volley.
    var wset = Environment.GetEnvironmentVariable("WARDENSET");
    if (wset != null) foreach (var e in host.Game.Enemies.Values) if (e.Type == EnemyType.Boss && e.Warden != null)
    {
      var ww = e.Warden;
      if (wset == "exposed") { ww.Turn = WardenTurn.Light; ww.LightMode = WardenLight.Follow; ww.Light = ww.LightDisp = e.Pos + new V2(20f, 10f); ww.ExposedT = 2.2f; }
      if (wset == "stun") ww.StunT = 1f;
      if (wset == "fan") { ww.Turn = WardenTurn.Fan; ww.FanWarn = 0.08f; ww.FanAim = (host.Game.Players[1].Pos - e.Pos).Angle; }
    }
    // GATEHOLD=1: every Warden gate on screen is caught in its stop just inside the arena (doors flashing).
    if (Environment.GetEnvironmentVariable("GATEHOLD") != null) foreach (var e in host.Game.Enemies.Values) if (e.Type == EnemyType.Boss && e.Warden != null)
      foreach (var gt in e.Warden.Gates) { gt.Warn = -(Game.GateHoldIn / Game.GateSpeed + 0.1f); gt.Front = (gt.Dir > 0f ? 6f : Game.Height - 6f) + gt.Dir * Game.GateHoldIn; }
    foreach (var e in host.Game.Enemies.Values) if (e.Type == EnemyType.Boss) Console.WriteLine("warden stage {0} turn {1} turnT {2:0.0} trans {3:0.0} light {4} gates {5} pos {6:0},{7:0}", e.Warden?.Stage, e.Warden?.Turn, e.Warden?.TurnT, e.Warden?.TransT, e.Warden?.LightMode, e.Warden?.Gates.Count, e.Pos.X, e.Pos.Y);
    me = host.Game.Players[1]; if (Environment.GetEnvironmentVariable("PARRY") == null) me.Hp = me.MaxHp * 0.62f; me.ShieldHp = 40f; me.Xp = Defs.XpToNext(me.Level) * 0.4f;
    me.Slots[1].Charges = 0; me.Slots[1].Timer = 4.2f;
    PlayerScenes();
    app = new ArenaApp();
    f2 = BindingFlags.NonPublic | BindingFlags.Instance;
    if (Environment.GetEnvironmentVariable("GOLD") != null) me.Gold = int.Parse(Environment.GetEnvironmentVariable("GOLD"));  // the Bazaar's gold on the HUD
    set = (n, v) => typeof(ArenaApp).GetField(n, f2).SetValue(app, v);
    if (Environment.GetEnvironmentVariable("RANDOM") == null) set("juiceRng", new System.Random(1));  // (Juice.cs rolls its own)
    set("session", host); set("game", host.Game); set("scale", 1f); set("drawScale", 1280f / Game.Width);
    ArenaScenes();
    // LAUNCH=<seconds>: a run launching from the lobby, that far in (LaunchIntro.cs).
    launchAt = Environment.GetEnvironmentVariable("LAUNCH") != null ? float.Parse(Environment.GetEnvironmentVariable("LAUNCH")) : -1f;
    if (launchAt >= 0f) {
      set("launchT", launchAt); set("launchFromLobby", true); set("launchRippled", true);
      host.Game.State = launchAt < 5f ? MatchState.Intermission : MatchState.Fighting; host.Game.Wave = launchAt < 5f ? 0 : 1; host.Game.StateTimer = Math.Max(0.1f, 5f - launchAt);
      host.Game.Enemies.Clear(); host.Game.Bullets.Clear(); foreach (var pl in host.Game.Players.Values) { pl.Offer = null; pl.PendingPicks = 0; }
      float hk = Math.Max(0f, Math.Min(1f, (launchAt - 1.7f) / 0.35f)); set("hudT", hk);
    }
    // RECALL=<seconds>: everyone beaming out back to the lobby, that far in (Recall.cs).
    recallAt = Environment.GetEnvironmentVariable("RECALL") != null ? float.Parse(Environment.GetEnvironmentVariable("RECALL"), System.Globalization.CultureInfo.InvariantCulture) : -1f;
    if (recallAt >= 0f) { host.Screen = AppScreen.Game; set("recallT", recallAt); set("hudT", Math.Max(0f, 1f - recallAt / 0.3f)); }
    // ENDING=<seconds>: a run ending, that far in (RunEnding.cs); with mode gameover or victory.
    endingAt = Environment.GetEnvironmentVariable("ENDING") != null ? float.Parse(Environment.GetEnvironmentVariable("ENDING")) : -1f;
    if (endingAt >= 0f) {
      bool win = args.Length > 4 && args[4] == "victory";
      host.Game.State = win ? MatchState.Victory : MatchState.GameOver;
      V2 focus = me.Disp;
      if (win) { foreach (var en in host.Game.Enemies.Values) if (en.Def.IsBoss) focus = en.Disp; var shots = (System.Collections.Generic.List<V2>)typeof(ArenaApp).GetField("endShots", f2).GetValue(app); foreach (var b in host.Game.Bullets.Values) if (Defs.IsEnemyShot(b.Kind)) shots.Add(b.Pos); }
      else foreach (var pl in host.Game.Players.Values) pl.Downed = true;
      set("endT", endingAt); set("endWin", win); set("endFocus", focus); set("endWasOver", true); set("endStamped", endingAt > 1.3f);
    }
    // DOWNED=<phase>:<value>: you going down or getting back up (DownedLooks.cs). down:<seconds since the hit>, revive:<0-1 revive progress with an ally standing by>, up:<seconds since you stood back up>. Use with mode hud and 2 players.
    string dwn = Environment.GetEnvironmentVariable("DOWNED");
    if (dwn != null) {
      var ci = System.Globalization.CultureInfo.InvariantCulture;
      string[] dp = dwn.Split(':'); float dv = float.Parse(dp[1], ci);
      var trk = typeof(ArenaApp).GetMethod("UpdateDownedLooks", f2);
      host.Game.State = MatchState.Fighting; host.Game.Enemies.Clear(); host.Game.Bullets.Clear();
      Time.unscaledTime = 100f; trk.Invoke(app, null);                       // everyone seen standing
      me.Downed = true; me.Hp = 0f; Time.unscaledTime = 101f; trk.Invoke(app, null);  // the hit: down at 101 s
      if (dp[0] == "down") Time.unscaledTime = 101f + dv;
      else if (dp[0] == "revive") {
        foreach (var o in host.Game.Players.Values) if (o != me) { o.Disp = o.Pos = me.Pos + new V2(-60f, 6f); o.Downed = false; }
        me.Revive = 0.05f; Time.unscaledTime = 104f; trk.Invoke(app, null);
        me.Revive = dv; Time.unscaledTime = 104.1f; trk.Invoke(app, null);
      }
      else if (dp[0] == "up") {
        foreach (var o in host.Game.Players.Values) if (o != me) { o.Disp = o.Pos = me.Pos + new V2(-60f, 6f); o.Downed = false; }
        me.Revive = 0.9f; Time.unscaledTime = 104.9f; trk.Invoke(app, null);
        me.Downed = false; me.Hp = me.MaxHp * 0.35f; me.Revive = 0f; Time.unscaledTime = 105f; trk.Invoke(app, null);
        Time.unscaledTime = 105f + dv;
      }
    }
    if (Environment.GetEnvironmentVariable("NONSHOOT") != null) {  // NONSHOOT=1: the enemies that don't shoot, each caught mid-move (NonShooterLooks.cs)
      var ng = host.Game; ng.Enemies.Clear(); ng.Bullets.Clear();
      var nsp = typeof(Game).GetMethod("SpawnEnemyAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
      Func<EnemyType, V2, byte, float, float, float, Enemy> put = (t, at, ph, pt, t2, face) => { var e = (Enemy)nsp.Invoke(ng, new object[] { t, at, 0f, 0, false }); e.SpawnT = 0f; e.Spawning = false; e.Disp = e.Pos; e.Phase = ph; e.PhaseT = pt; e.T2 = t2; e.Facing = face; return e; };
      me.Pos = me.Disp = new V2(560f, 450f);
      put(EnemyType.FuseRunner, new V2(500f, 400f), 0, 0f, 0f, 0.6f);                       // running
      put(EnemyType.FuseRunner, new V2(330f, 500f), 1, 0.35f, 0f, 0f);                      // fuse half burnt
      put(EnemyType.Hookman, new V2(860f, 300f), 1, 0.2f, 0f, (me.Pos - new V2(860f, 300f)).Angle);   // aiming
      put(EnemyType.Hookman, new V2(900f, 600f), 2, 0f, 210f, (me.Pos - new V2(900f, 600f)).Angle);   // harpoon out
      put(EnemyType.Hookman, new V2(1150f, 450f), 0, 0.5f, 0f, MathX.Pi);                   // spinning
      put(EnemyType.Hookman, new V2(1250f, 250f), 4, 0.6f, 60f, MathX.Pi);                  // open after a miss
      put(EnemyType.Sapper, new V2(400f, 200f), 0, 1f, 0f, 0.3f);
      put(EnemyType.SapperMine, new V2(250f, 180f), 1, 0f, 3f, 0f);
      put(EnemyType.SapperMine, new V2(380f, 120f), 1, 0f, 3f, 0f);
      put(EnemyType.SapperMine, new V2(230f, 310f), 0, 0f, 0.4f, 0f);                       // arming
      put(EnemyType.SapperMine, new V2(700f, 180f), 2, 0.15f, 3f, 0f);                      // beeping
      foreach (var e in ng.Enemies.Values) if (e.Type == EnemyType.SapperMine) { e.Hp = e.MaxHp = 1f; }
    }
    typeof(ArenaApp).GetMethod("DrawWorld", f2).Invoke(app, null);
    typeof(ArenaApp).GetMethod("InitStyles", f2).Invoke(app, null);
    Ui.Init();
    Event.current = new Event { mousePosition = new Vector2 { x = -100, y = -100 } };
    mode = args.Length > 4 ? args[4] : "hud";
    if (Environment.GetEnvironmentVariable("MASTERY") != null) {
      Func<string, int> U0 = n => Array.FindIndex(Upgrades.All, u => u.Name == n);
      host.Game.GrantUpgrade(1, U0("Shockwave")); host.Game.GrantUpgrade(1, U0("Blink"));
      float[] xs = { 900f, 420f, 130f, 0f, 20f };
      for (int k = 0; k < 5; k++) if (me.Slots[k].Id != AbilityId.None) me.MasteryXp[me.Slots[k].Id] = xs[k];
      host.Game.MasteryToasts.Add(new MasteryToast { Ability = me.Slots[1].Id, Level = 3, T = 2f });
      if (Environment.GetEnvironmentVariable("HOVER") != null) Event.current.mousePosition = new Vector2 { x = 500, y = 400 };
    }
    if (Environment.GetEnvironmentVariable("WAVECLEARED") != null) {  // the between-waves box: WAVECLEARED=1 (waiting), =auto (everyone on auto-ready), =boss (next wave is a boss)
      var wg = host.Game; string wc = Environment.GetEnvironmentVariable("WAVECLEARED");
      wg.State = MatchState.Intermission; wg.Wave = wc == "boss" ? 4 : 4; wg.StateTimer = 3.2f; wg.Me.Offer = null;
      var rep = new WaveSummary { Wave = wg.Wave, ClearTime = 42f, ScoreGain = 120 };
      int wi = 0; foreach (var wp in wg.Players.Values) { rep.Lines.Add(new WaveLine { Id = wp.Id, Kills = new[] { 18, 21, 13 }[wi % 3], Damage = new[] { 2140f, 2880f, 1310f }[wi % 3], Taken = new[] { 0f, 96f, 22f }[wi % 3], Awards = new[] { WaveAward.Untouched, WaveAward.MostDamage | WaveAward.MostKills, WaveAward.Reviver }[wi % 3] }); wp.AutoReady = wc == "auto"; wp.Ready = wi == 1; wi++; }
      wg.WaveReport = rep;
      // WCT=<seconds>: that long since the box appeared (its 0.35 s drop-in); settled by default (the preview's clock doesn't move).
      float wct = Environment.GetEnvironmentVariable("WCT") != null ? float.Parse(Environment.GetEnvironmentVariable("WCT"), System.Globalization.CultureInfo.InvariantCulture) : 1f;
      if (Time.unscaledTime < 5f) Time.unscaledTime = 5f;  // (a negative birth time reads as "not up yet")
      set("wcBorn", Time.unscaledTime - wct); set("wcWave", wg.Wave);
    }
    if (Environment.GetEnvironmentVariable("PATHOFFER") != null) { host.Game.PathOffer = Environment.GetEnvironmentVariable("PATHOFFER") == "bazaar" ? new[] { PathId.Bazaar, PathId.EliteGauntlet } : Environment.GetEnvironmentVariable("PATHOFFER") == "4" ? new[] { PathId.Fogbound, PathId.NarrowPass } : Environment.GetEnvironmentVariable("PATHOFFER") == "3" ? new[] { PathId.FrozenLake, PathId.BloodMoon } : Environment.GetEnvironmentVariable("PATHOFFER") == "2" ? new[] { PathId.BurningGrounds, PathId.Sanctuary } : new[] { PathId.SwarmFields, PathId.EliteGauntlet }; host.Game.State = MatchState.Intermission; host.Game.Route.Add(PathId.SniperRidge); }
    if (Environment.GetEnvironmentVariable("PATHOFFER") != null || Environment.GetEnvironmentVariable("PATHON") != null) { Event.current.type = EventType.Repaint; set("glMaterial", new Material(null)); GL.ToUi = true; }
    if (Environment.GetEnvironmentVariable("HUDT") != null) { float ht = float.Parse(Environment.GetEnvironmentVariable("HUDT")); set("hudT", ht); set("hudShown", ht > 0.5f); }
    if (Environment.GetEnvironmentVariable("HUDMODE") != null) { int hm = int.Parse(Environment.GetEnvironmentVariable("HUDMODE")); set("hudMode", hm); set("hudShown", hm == 0); set("hudT", hm == 0 ? 1f : 0f); set("miniT", hm == 1 || hm == 2 ? 1f : 0f); }
    DrawMode();
    Event.current.type = EventType.Repaint; typeof(ArenaApp).GetMethod("DrawCabEdges", f2).Invoke(app, null);  // the cabinet menus' soft edges, as ArenaApp.OnGUI draws them (CrtWindows.cs)
    using (var w = new BinaryWriter(File.Create(args[0] + ".tris"))) foreach (var v in GL.Tris) w.Write(v);
    File.WriteAllLines(args[0] + ".ui", GUI.Log.ToArray());
    Console.WriteLine("state {0} wave {1}, tris {2}, ui items {3}", host.Game.State, host.Game.Wave, GL.Tris.Count / 18, GUI.Log.Count);
    host.Leave(""); foreach (var c in others) c.Leave("");
  }
}
