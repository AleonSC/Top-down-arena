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
    pr.Xp = Rank.XpAt(Environment.GetEnvironmentVariable("RANK") != null ? int.Parse(Environment.GetEnvironmentVariable("RANK")) : 9) + 115; pr.Trail = Environment.GetEnvironmentVariable("TRAIL") ?? "trail-ember";  // RANK=40 TRAIL=trail-prism for the cosmetics window
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
    if (Environment.GetEnvironmentVariable("SANDREAS") != null) names[names.Length - 1] = "GTA:SA";  // the San Andreas disk easter egg
    if (Environment.GetEnvironmentVariable("DOOM") != null) names[1] = "Doom";  // the Doom disk easter egg
    if (Environment.GetEnvironmentVariable("PAC") != null) names[1] = "Pacman";  // the Pac-Man disk easter egg
    if (Environment.GetEnvironmentVariable("HOLO") != null) names[0] = "Antaleon";  // the hologram disk easter egg
    if (Environment.GetEnvironmentVariable("WIN95") != null) names[2] = "Windows 95";  // the Windows 95 disk easter egg
    if (Environment.GetEnvironmentVariable("MI") != null) names[0] = "Mission Impossible";  // the Mission: Impossible disk easter egg
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
  static readonly System.Collections.Generic.HashSet<int> rodMine = new System.Collections.Generic.HashSet<int>();  // ROD=: the scene's grunts
  static readonly V2 RodHome = new V2(Game.Width / 2f - 300f, Game.Height / 2f + 190f);
  static void Main(string[] args) {
    if (Environment.GetEnvironmentVariable("RANDOM") == null) Game.SeedForTests(1);  // the same rolls every run, so uishots.sh -r can compare pixels (RANDOM=1: as random as the game)
    Shot.args = args; wave = int.Parse(args[1]); seconds = float.Parse(args[2]); players = int.Parse(args[3]);
    UnityEngine.Screen.width = 1280; UnityEngine.Screen.height = 720;
    if (Environment.GetEnvironmentVariable("CRTTHEME") != null) CrtUi.Theme = int.Parse(Environment.GetEnvironmentVariable("CRTTHEME"));  // CRT colour (CrtUi.ThemeNames)
    host = new Session { AutoStart = players, StartWave = wave }; host.Game.Bot = true;
    melee = Environment.GetEnvironmentVariable("KNIGHT") ?? Environment.GetEnvironmentVariable("NINJA") ?? Environment.GetEnvironmentVariable("BLOOD");
    meleeKit = Environment.GetEnvironmentVariable("NINJA") != null ? Kit.Ninja : Environment.GetEnvironmentVariable("BLOOD") != null ? Kit.Hemomancer : Kit.Knight;
    var rodEnv = Environment.GetEnvironmentVariable("ROD");  // ROD=<seconds since the cast>: you take Lightning Rod (the tank Elementalist's second ultimate) and cast it that long before the shot
    host.Host(7990 + new System.Random().Next(500), "Anthony", Role.Tank, melee != null ? int.Parse(melee) : 0, 0, melee != null ? meleeKit : Kit.Elementalist, 0, rodEnv != null && melee == null ? 1 : 0);
    others = new System.Collections.Generic.List<Session>();
    string[] names = { "Aleon", "Sam" }; Role[] roles = { Role.Dps, Role.Healer }; Kit[] kits = melee != null ? new[] { meleeKit, meleeKit } : new[] { Kit.Soldier, Kit.Summoner };
    for (int i = 1; i < players; i++) { var c = new Session(); c.Game.Bot = true; c.Join("127.0.0.1", host.Port, names[i-1], roles[i-1], 0, 0, kits[i-1]); others.Add(c); }
    if (players == 1) host.StartMatch();
    for (int i = 0; i < 600 && !host.Game.Running; i++) { Step(host); foreach (var c in others) Step(c); Thread.Sleep(2); }
    // Some upgrades so the bars have content.
    for (int f = 0; f < seconds * 60; f++) {
      Step(host); foreach (var c in others) Step(c); if (f % 4 == 0) Thread.Sleep(1);
      foreach (var p in host.Game.Players.Values) { p.InvulnT = 1f; }
      if (rodEnv != null && f > 60) foreach (var p in host.Game.Players.Values) { p.InvulnT = 0f; p.Hp = Math.Max(p.Hp, p.MaxHp * 0.6f); }  // ROD=: the hits land, for the grey numbers
      if (rodEnv != null && f == Math.Max(0, (int)((seconds - float.Parse(rodEnv)) * 60)) && host.Game.Players.ContainsKey(1))
      {
        host.Game.Bot = false;  // (it stands still from here: no Space ability or fire hiding the charge)
        foreach (var bp in host.Game.Players.Values) { bp.AegisT = 0f; bp.BarrierT = 0f; }
        // A clean stage: the tank in the middle, five grunts in an arc ahead of it (caged: they only shoot when this scene says).
        host.Game.Enemies.Clear(); host.Game.Bullets.Clear(); rodMine.Clear();
        var rsp = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
        for (int k = 0; k < 5; k++)
        {
          var re = (Enemy)rsp.Invoke(host.Game, new object[] { k == 2 ? EnemyType.Brute : EnemyType.Grunt, RodHome + V2.FromAngle(-MathX.Pi / 2f + (k - 2) * 0.38f) * (k % 2 == 0 ? 250f : 200f), 0f, 0, false });
          re.SpawnT = 0f; re.Hp = re.MaxHp = 1e5f; re.CageT = 999f; rodMine.Add(re.Id);
        }
        typeof(Game).GetMethod("Ultimate", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(host.Game, new object[] { host.Game.Players[1], host.Game.Players[1].Slots[Upgrades.UltSlot].Id, host.Game.Players[1].Pos });
      }
      if (rodEnv != null && rodMine.Count > 0)
      {
        foreach (var id in new System.Collections.Generic.List<int>(host.Game.Enemies.Keys)) if (!rodMine.Contains(id)) host.Game.Enemies.Remove(id);
        foreach (var bp in host.Game.Players.Values) { bp.Pos = bp.Disp = RodHome; bp.Aim = -MathX.Pi / 2f; }
      }
      // While the charge is up, the nearest enemies take turns landing a shot on you every 0.3 s (the last 0.07 s before the shot), so bolts are falling.
      if (rodEnv != null && host.Game.Players.ContainsKey(1) && host.Game.Players[1].RodT > 0f && ((int)(seconds * 60) - f - 4) % 18 == 0)
      {
        var rme = host.Game.Players[1];
        var near = new System.Collections.Generic.List<Enemy>(host.Game.Enemies.Values);
        near.RemoveAll(e => host.Game.IsSpawning(e) || e.Pos.DistanceTo(rme.Pos) > 700f);
        near.Sort((x, y) => x.Pos.DistanceSq(rme.Pos).CompareTo(y.Pos.DistanceSq(rme.Pos)));
        if (near.Count > 0)
        {
          var shooter = near[(f / 18) % near.Count];
          var gf = BindingFlags.NonPublic | BindingFlags.Instance;
          typeof(Game).GetField("actingEnemy", gf).SetValue(host.Game, shooter);
          V2 dir = (rme.Pos - shooter.Pos).Normalized();
          typeof(Game).GetMethod("SpawnBullet", gf).Invoke(host.Game, new object[] { Defs.SmallShot, rme.Pos - dir * 6f, dir * 300f, 0f, 2f, Defs.BulletDamage(Defs.SmallShot), 0, false, 0f });
          typeof(Game).GetField("actingEnemy", gf).SetValue(host.Game, null);
        }
      }
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
    if (Environment.GetEnvironmentVariable("FXOFF") != null) set("fxOn", false);  // FXOFF=1: Visual effects off (the plain looks)
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
    if (Environment.GetEnvironmentVariable("PORTALS") != null) {  // PORTALS=1: spawn portals opening, letting a burst out, a flank behind you (with its arrow) and one closing (SpawnPortalLook.cs)
      var pg = host.Game; pg.Enemies.Clear(); pg.Bullets.Clear();
      var psp = typeof(Game).GetMethod("SpawnEnemyAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
      var page = typeof(ArenaApp).GetMethod("PortalAge", f2);
      me.Pos = me.Disp = new V2(820f, 470f); me.Aim = 0f;
      foreach (var o in pg.Players.Values) if (o != me) { o.Pos = o.Disp = me.Pos + new V2(-40f, 90f); o.Aim = 0.3f; }
      Func<EnemyType, V2, Enemy> pput = (t, at) => { var e = (Enemy)psp.Invoke(pg, new object[] { t, at, 0f, 0, false }); e.SpawnT = 0f; e.Spawning = false; e.Disp = e.Pos; e.Facing = (me.Pos - at).Angle; return e; };
      Action<Enemy, byte, float> phase = (e, ph, age) => { e.Phase = ph; Time.time = 100f - age; page.Invoke(app, new object[] { e }); };
      var a = pput(EnemyType.Portal, new V2(1300f, 140f)); phase(a, SpawnPortals.Opening, 0.5f);
      var b = pput(EnemyType.Portal, new V2(1420f, 640f)); phase(b, SpawnPortals.Emitting, 1f); b.Hp = b.MaxHp * 0.55f;
      var c = pput(EnemyType.Portal, new V2(170f, 520f)); phase(c, SpawnPortals.FlankOpening, 0.9f);
      var d = pput(EnemyType.Portal, new V2(560f, 820f)); phase(d, SpawnPortals.Closing, 0.2f);
      V2 fwd = V2.FromAngle(b.Facing);
      pput(EnemyType.Grunt, b.Pos + fwd * 55f + fwd.Orthogonal() * 18f).Facing = b.Facing;
      pput(EnemyType.Swarmer, b.Pos + fwd * 110f - fwd.Orthogonal() * 20f).Facing = b.Facing;
      pput(EnemyType.Grunt, b.Pos + fwd * 170f + fwd.Orthogonal() * 6f).Facing = b.Facing;
      Time.time = 100f;
    }
    if (Environment.GetEnvironmentVariable("TAGS") != null) {  // TAGS=1: a set of enemies on first sight, each with its name tag (EnemyTagLook.cs); a fresh profile, so nothing is saved to yours
      var tg = host.Game; tg.Enemies.Clear(); tg.Bullets.Clear();
      tg.ProfileOverride = new Profile();
      var tsp = typeof(Game).GetMethod("SpawnEnemyAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
      me.Pos = me.Disp = new V2(1200f, 900f);
      var tk = new[] { EnemyType.Grunt, EnemyType.Swarmer, EnemyType.Mortar, EnemyType.Hookman, EnemyType.Medic, EnemyType.Sapper };
      for (int k = 0; k < tk.Length; k++) { var e = (Enemy)tsp.Invoke(tg, new object[] { tk[k], new V2(250f + 170f * k, 300f), 0f, 0, false }); e.SpawnT = 0f; e.Spawning = false; e.Disp = e.Pos; }
    }
    if (Environment.GetEnvironmentVariable("AIM") != null) {  // AIM=<0-1>: the aimed shooters winding up (ShooterAimLook.cs); draw once, then move the clock on
      var ag = host.Game; ag.Enemies.Clear(); ag.Bullets.Clear();
      var asp = typeof(Game).GetMethod("SpawnEnemyAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
      Func<EnemyType, V2, Enemy> aput = (t, at) => { var e = (Enemy)asp.Invoke(ag, new object[] { t, at, 0f, 0, false }); e.SpawnT = 0f; e.Spawning = false; e.Disp = e.Pos; e.Winding = true; e.Facing = 0f; return e; };
      me.Pos = me.Disp = new V2(1200f, 900f);
      var kinds = new[] { EnemyType.Sniper, EnemyType.Shard, EnemyType.Medic, EnemyType.Splitter, EnemyType.Phantom, EnemyType.Grunt };
      for (int k = 0; k < kinds.Length; k++) aput(kinds[k], new V2(250f, 130f + 105f * k));
      typeof(ArenaApp).GetMethod("DrawWorld", f2).Invoke(app, null);
      ag.AnimT += float.Parse(Environment.GetEnvironmentVariable("AIM"), System.Globalization.CultureInfo.InvariantCulture) * 0.3f;
    }
    if (Environment.GetEnvironmentVariable("RESKIN") != null) {  // RESKIN=1: the Rioter, Thug, Shiv and the Brute and Lunger they reskin (PrisonLook.cs)
      var rg = host.Game; rg.Enemies.Clear(); rg.Bullets.Clear();
      var rsp = typeof(Game).GetMethod("SpawnEnemyAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
      me.Pos = me.Disp = new V2(1200f, 900f);
      var kinds = new[] { EnemyType.Rioter, EnemyType.Thug, EnemyType.Brute, EnemyType.Shiv, EnemyType.Lunger };
      for (int k = 0; k < kinds.Length; k++) { var e = (Enemy)rsp.Invoke(rg, new object[] { kinds[k], new V2(300f + 120f * k, 300f), 0f, 0, false }); e.SpawnT = 0f; e.Spawning = false; e.Disp = e.Pos; e.Facing = 0f; }
    }
    if (Environment.GetEnvironmentVariable("HIVE") != null) {  // HIVE=<s into the clock>: the eight Hive enemies in two rows (HiveLook.cs): the Stinger charging with a dart in flight, the Needler with a few rounds left, the Hornet winding up; set beside Docs/Mockups/hive-faction.html
      var hg = host.Game; hg.Enemies.Clear(); hg.Bullets.Clear();
      var hsp = typeof(Game).GetMethod("SpawnEnemyAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
      me.Pos = me.Disp = new V2(1200f, 900f);
      var hk = new[] { EnemyType.Worker, EnemyType.Soldier, EnemyType.CombShield, EnemyType.HoneyLobber, EnemyType.Stinger, EnemyType.Needler, EnemyType.Bumble, EnemyType.Hornet };
      for (int k = 0; k < hk.Length; k++) {
        var e = (Enemy)hsp.Invoke(hg, new object[] { hk[k], new V2(300f + 220f * (k % 4), 260f + 240f * (k / 4)), 0f, 0, false });
        e.SpawnT = 0f; e.Spawning = false; e.Disp = e.Pos; e.Facing = 0f;
        if (hk[k] == EnemyType.Soldier && Environment.GetEnvironmentVariable("HIVECHARGE") != null) e.Phase = 1;  // HIVECHARGE=1: the Soldier charging, its armour plates closed
        if (hk[k] == EnemyType.Stinger) e.Winding = true;
        if (hk[k] == EnemyType.Needler) e.Phase = 5;
        if (hk[k] == EnemyType.Hornet) { e.Phase = 1; e.PhaseT = 0.5f; }
      }
      hg.Bullets[9001] = new Bullet { Id = 9001, Kind = Defs.StingBolt, Pos = new V2(640f, 500f), Vel = new V2(260f, 0f), Radius = 7f, Dmg = 8f };
      var lobber = System.Linq.Enumerable.First(hg.Enemies.Values, q => q.Type == EnemyType.HoneyLobber);
      if (Environment.GetEnvironmentVariable("HIVELOB") != null) {  // HIVELOB=<s into the lob>: the Honey Lobber's ring, then the glob in flight (throw at 1.2 s, lands at 2.1 s); a puddle lands there too
        float la = float.Parse(Environment.GetEnvironmentVariable("HIVELOB"), System.Globalization.CultureInfo.InvariantCulture);
        hg.Effects.Add(new Effect { Kind = FxKind.GasLob, Pos = lobber.Pos + new V2(230f, 40f), Pos2 = new V2(lobber.Id, 0.9f), Color = new Rgba(1f, 0.77f, 0.2f), Radius = 55f, Dur = 2.1f, T = la });
        lobber.Winding = la < 1.2f;
      }
      hg.AnimT += float.Parse(Environment.GetEnvironmentVariable("HIVE"), System.Globalization.CultureInfo.InvariantCulture);
    }
    if (Environment.GetEnvironmentVariable("INFALL") != null) {  // INFALL=<s into the clock>: the Singularity's faction, step 1 (InfallLook.cs): four Debris, an Acolyte winding up with a volley in flight, a Pulsar flickering and one sweeping; set beside Docs/Mockups/singularity-faction.html
      var ig = host.Game; ig.Enemies.Clear(); ig.Bullets.Clear();
      var isp = typeof(Game).GetMethod("SpawnEnemyAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
      Func<EnemyType, V2, Enemy> iput = (t, at) => { var e = (Enemy)isp.Invoke(ig, new object[] { t, at, 0f, 0, false }); e.SpawnT = 0f; e.Spawning = false; e.Disp = e.Pos; return e; };
      me.Pos = me.Disp = new V2(1000f, 560f);
      for (int k = 0; k < 4; k++) { float a = 3.6f + k * 0.5f, rr = 300f - 50f * k; iput(EnemyType.Debris, me.Pos + new V2((float)Math.Cos(a) * rr, (float)Math.Sin(a) * rr * 0.8f)); }
      var aco = iput(EnemyType.VoidAcolyte, new V2(700f, 600f)); aco.Facing = 0f;
      typeof(Game).GetMethod("AcolyteVolley", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(ig, new object[] { aco, me.Pos, 0f });
      foreach (var b in ig.Bullets.Values) for (int i = 0; i < 45; i++) { b.Vel = b.Vel.Rotated(b.AngVel * 0.02f); b.Pos += b.Vel * 0.02f; }
      aco.Winding = true;
      var warn = iput(EnemyType.Pulsar, new V2(1250f, 400f)); warn.Phase = 1; warn.SeenT = 0.6f; warn.Facing = 2.0f;
      var on = iput(EnemyType.Pulsar, new V2(1200f, 690f)); on.Phase = 2; on.Facing = 3.75f;
      ig.AnimT += float.Parse(Environment.GetEnvironmentVariable("INFALL"), System.Globalization.CultureInfo.InvariantCulture);
    }
    if (Environment.GetEnvironmentVariable("INFALL2") != null) {  // INFALL2=<s into the clock>: the Singularity's faction, step 2 (InfallLook.cs): a Graviton winding up beside its landed Small Well, an Orbiter flaring with a slung orb in flight, a hurt Collapsar and an Implosion half-way
      var ig = host.Game; ig.Enemies.Clear(); ig.Bullets.Clear();
      var isp = typeof(Game).GetMethod("SpawnEnemyAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
      Func<EnemyType, V2, Enemy> iput = (t, at) => { var e = (Enemy)isp.Invoke(ig, new object[] { t, at, 0f, 0, false }); e.SpawnT = 0f; e.Spawning = false; e.Disp = e.Pos; return e; };
      me.Pos = me.Disp = new V2(1000f, 560f);
      var gv = iput(EnemyType.Graviton, new V2(700f, 380f)); gv.Facing = 0.6f; gv.Winding = true;
      iput(EnemyType.MiniWell, new V2(880f, 470f));
      var ob = iput(EnemyType.Orbiter, new V2(1280f, 380f)); ob.Facing = (me.Pos - ob.Pos).Angle; ob.Winding = true; ob.Phase = 1; ob.SeenT = 0.4f;
      ig.Bullets[9101] = new Bullet { Id = 9101, Kind = Defs.FastShot, Pos = new V2(1140f, 470f), Vel = (me.Pos - new V2(1140f, 470f)).Normalized() * 330f, Radius = 5f, Dmg = 17f, Look = ShotLook.Round };
      var co = iput(EnemyType.Collapsar, new V2(760f, 720f)); co.Hp = co.MaxHp * 0.4f;
      var im = iput(EnemyType.Implosion, new V2(1240f, 720f)); im.SeenT = 0.4f;
      ig.AnimT += float.Parse(Environment.GetEnvironmentVariable("INFALL2"), System.Globalization.CultureInfo.InvariantCulture);
    }
    if (Environment.GetEnvironmentVariable("INFALL3") != null) {  // INFALL3=<s into the clock>: the Singularity's faction, step 3 (InfallLook.cs): a Lens facing you with your shots bending round its face, a White Dwarf flashing and one darting in
      var ig = host.Game; ig.Enemies.Clear(); ig.Bullets.Clear();
      var isp = typeof(Game).GetMethod("SpawnEnemyAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
      Func<EnemyType, V2, Enemy> iput = (t, at) => { var e = (Enemy)isp.Invoke(ig, new object[] { t, at, 0f, 0, false }); e.SpawnT = 0f; e.Spawning = false; e.Disp = e.Pos; return e; };
      me.Pos = me.Disp = new V2(1000f, 560f);
      var ln = iput(EnemyType.Lens, new V2(720f, 560f)); ln.Facing = 0f;
      var bend = typeof(Game).GetMethod("BendRoundLenses", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
      typeof(Game).GetMethod("PrepareLenses", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(ig, null);
      for (int k = 0; k < 4; k++) {  // four of your shots at different stages of passing its face
        var b = new Bullet { Id = 9200 + k, Kind = Defs.TankShot, Owner = me.Id, Pos = me.Pos + new V2(-20f, (k % 2 == 0 ? -4f : 4f)), Vel = new V2(-700f, 0f), Radius = 4f, Dmg = 10f };
        for (int i = 0; i < 30 + k * 9; i++) { bend.Invoke(ig, new object[] { b, 0.01f }); b.Pos += b.Vel * 0.01f; }
        ig.Bullets[b.Id] = b;
      }
      var wd = iput(EnemyType.WhiteDwarf, new V2(1150f, 380f)); wd.Phase = 1; wd.SeenT = 0.35f;
      iput(EnemyType.WhiteDwarf, new V2(1260f, 720f));
      ig.AnimT += float.Parse(Environment.GetEnvironmentVariable("INFALL3"), System.Globalization.CultureInfo.InvariantCulture);
    }
    if (Environment.GetEnvironmentVariable("ACCRETION") != null) {  // ACCRETION=<s into the ring>: the Singularity's set piece (Infall.cs): eight Debris and four Orbiters in a ring round you, turning and closing, two gaps open
      var ig = host.Game; ig.Enemies.Clear(); ig.Bullets.Clear();
      me.Pos = me.Disp = new V2(1000f, 560f);
      float at = float.Parse(Environment.GetEnvironmentVariable("ACCRETION"), System.Globalization.CultureInfo.InvariantCulture);
      float spin = 0.7f, radius = Math.Max(Game.AccretionEnd, Game.AccretionStart - Game.AccretionClose * at);
      var fin = Approaches.For(EnemyType.Singularity).Finale;
      var isp = typeof(Game).GetMethod("SpawnEnemyAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
      for (int i = 0; i < fin.Members.Length; i++) {
        V2 p = me.Pos + V2.FromAngle(spin + Game.AccretionSpin * at + MathX.Tau * Game.AccretionSlot(i) / Game.AccretionSlots) * radius;
        var e = (Enemy)isp.Invoke(ig, new object[] { fin.Members[i], p, 0f, 0, false }); e.SpawnT = 0f; e.Spawning = false; e.Disp = e.Pos; e.Facing = (me.Pos - p).Angle;
        if (i == 3) e.Winding = true;
      }
      ig.AccretionBands.Add(new Game.AccretionBand { Center = me.Pos, Radius = radius, Spin = spin + Game.AccretionSpin * at });
      ig.AnimT += at;
    }
    if (Environment.GetEnvironmentVariable("QUARRY") != null) {  // QUARRY=<s into the clock>: the Colossus's step-1 faction (QuarryLook.cs): two Rubble, a Quarryman swinging at you, a Stomper walking, one lifting its foot (gap showing) and one whose ring is out; set beside Docs/Mockups/colossus-faction.html
      var qg = host.Game; qg.Enemies.Clear(); qg.Bullets.Clear();
      var qsp = typeof(Game).GetMethod("SpawnEnemyAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
      Func<EnemyType, V2, float, Enemy> qput = (t, at, face) => { var e = (Enemy)qsp.Invoke(qg, new object[] { t, at, 0f, 0, false }); e.SpawnT = 0f; e.Spawning = false; e.Disp = e.Pos; e.Facing = face; e.Elite = false; e.Traits = 0; return e; };
      me.Pos = me.Disp = new V2(1450f, 820f);
      qput(EnemyType.Rubble, new V2(260f, 250f), 0f); qput(EnemyType.Rubble, new V2(300f, 330f), 0f);
      qput(EnemyType.Quarryman, new V2(560f, 330f), 0f);
      qput(EnemyType.Stomper, new V2(900f, 300f), MathX.Pi);
      var sw = qput(EnemyType.Stomper, new V2(420f, 640f), 0f);
      sw.Stomp = new StompRing { Center = sw.Pos, Radius = sw.Radius + 6f, Warn = 0.3f, Gap1 = -0.5f };
      var so = qput(EnemyType.Stomper, new V2(980f, 660f), MathX.Pi);
      so.Stomp = new StompRing { Center = so.Pos, Radius = 150f, Warn = 0f, Gap1 = MathX.Pi - 0.4f };
      if (Environment.GetEnvironmentVariable("QUARRY2") != null) {  // QUARRY2=1 with QUARRY: step 2 instead: Slab Bearers (whole, flashing, cracked, broken with its slab down) and Rock Hurlers (winding up, boulder in flight, a landed pile)
        qg.Enemies.Clear(); qg.Patches.Clear(); qg.Effects.Clear();
        var setRiot = typeof(Game).GetMethod("SetRiotState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var sb1 = qput(EnemyType.SlabBearer, new V2(250f, 250f), 0f);
        var sb2 = qput(EnemyType.SlabBearer, new V2(450f, 250f), 0f); setRiot.Invoke(null, new object[] { sb2, Game.RiotWinding, 0.3f });
        var sb3 = qput(EnemyType.SlabBearer, new V2(650f, 250f), 0f); sb3.Phase = (byte)(Game.RiotWalk << 3 | 3);
        var sb4 = qput(EnemyType.SlabBearer, new V2(850f, 250f), 0f); sb4.Phase = (byte)(Game.RiotFlail << 3 | Game.RiotCracked);
        qg.Patches[701] = new PathPatch { Id = 701, Kind = PatchKind.Slab, Pos = new V2(885f, 250f), Warn = 0f, Life = 3f, Age = 1f, R0 = Game.SlabCoverHalfL, R1 = 0f };
        var rh1 = qput(EnemyType.RockHurler, new V2(250f, 600f), 0f);
        qg.Effects.Add(new Effect { Kind = FxKind.GasLob, Pos = new V2(470f, 560f), Pos2 = new V2(rh1.Id, 1f), Color = Game.HurlerColor, Radius = Game.HurlerLandR, Dur = 2.07f, T = 0.7f });
        var rh2 = qput(EnemyType.RockHurler, new V2(700f, 600f), 0f);
        qg.Effects.Add(new Effect { Kind = FxKind.GasLob, Pos = new V2(950f, 600f), Pos2 = new V2(rh2.Id, 1f), Color = Game.HurlerColor, Radius = Game.HurlerLandR, Dur = 2.07f, T = 1.55f });
        qg.Patches[702] = new PathPatch { Id = 702, Kind = PatchKind.Rubble, Pos = new V2(1150f, 600f), Warn = 0f, Life = 2f, Age = 0.6f, R0 = Game.RubbleR, R1 = Game.RubbleR };
      }
      if (Environment.GetEnvironmentVariable("QUARRY3") != null) {  // QUARRY3=1 with QUARRY: step 3 instead: a Driller winding up and one whose crack is about to erupt, a Geomancer charging and one whose crack races, a Cinder Priest charging with a magma bolt out and a lava pool
        qg.Enemies.Clear(); qg.Patches.Clear(); qg.Effects.Clear(); qg.Bullets.Clear();
        var d1 = qput(EnemyType.Driller, new V2(250f, 250f), 0f); d1.Phase = 1; d1.PhaseT = 0.3f;
        var d2 = qput(EnemyType.Driller, new V2(700f, 250f), 0f); d2.Phase = 3;
        d2.Crack = new ColCrack { From = new V2(460f, 250f), Ang = 0f, Len = 220f, Max = 220f, Wait = 0.2f, Seed = 5 };
        var g1 = qput(EnemyType.Geomancer, new V2(250f, 600f), 0f); g1.Winding = true;
        var g2 = qput(EnemyType.Geomancer, new V2(550f, 470f), 0f);
        g2.Crack = new ColCrack { From = new V2(570f, 470f), Ang = 0f, Len = 200f, Max = 380f, Seed = 9 };
        var c1 = qput(EnemyType.CinderPriest, new V2(820f, 640f), MathX.Pi); c1.Winding = true;
        var c2 = qput(EnemyType.CinderPriest, new V2(1150f, 450f), MathX.Pi);
        qg.Bullets[9101] = new Bullet { Id = 9101, Kind = Defs.MagmaBolt, Pos = new V2(1040f, 450f), Vel = new V2(-240f, 0f), Radius = 8f, Dmg = 9f };
        qg.Patches[703] = new PathPatch { Id = 703, Kind = PatchKind.Lava, Pos = new V2(1150f, 680f), Warn = 0.2f, Life = 2.7f, Age = 1f, R0 = Game.MagmaPoolR, R1 = Game.MagmaPoolR };
      }
      qg.AnimT += float.Parse(Environment.GetEnvironmentVariable("QUARRY"), System.Globalization.CultureInfo.InvariantCulture);
    }
    if (Environment.GetEnvironmentVariable("COURT") != null) {  // COURT=<s into the clock>: the Duelist's step-1 faction (CourtLook.cs): Fencers (walking, line showing, hopping), a Blade Dancer showing its line and one with blades hanging, Parriers (whole, flashing, cracked, broken); set beside Docs/Mockups/duelist-faction.html
      var cg = host.Game; cg.Enemies.Clear(); cg.Bullets.Clear(); cg.Patches.Clear(); cg.Effects.Clear();
      var csp = typeof(Game).GetMethod("SpawnEnemyAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
      Func<EnemyType, V2, float, Enemy> cput = (t, at, face) => { var e = (Enemy)csp.Invoke(cg, new object[] { t, at, 0f, 0, false }); e.SpawnT = 0f; e.Spawning = false; e.Disp = e.Pos; e.Facing = face; e.Elite = false; e.Traits = 0; return e; };
      me.Pos = me.Disp = new V2(570f, 520f);  // (between the rows, so ZOOM frames them)
      cput(EnemyType.Fencer, new V2(330f, 330f), 0f);
      var f1 = cput(EnemyType.Fencer, new V2(470f, 330f), 0f); f1.Phase = 1; f1.PhaseT = 0.2f;
      var f2 = cput(EnemyType.Fencer, new V2(610f, 330f), 0f); f2.Phase = 2;
      var bd1 = cput(EnemyType.BladeDancer, new V2(260f, 440f), 0f);
      bd1.Duel = new DuelState { Dash = DuelDash.Dancer, DashWarn = 0.3f, DashFrom = bd1.Pos, DashAng = 0f, DashLen = Game.DancerReach };
      var bd2 = cput(EnemyType.BladeDancer, new V2(900f, 440f), 0f);
      bd2.Duel = new DuelState();
      for (int k = 1; k <= 3; k++) bd2.Duel.Blades.Add(new DuelBlade { Pos = new V2(900f - Game.DancerReach + k * Game.DancerBladeEvery - 20f, 440f), Ang = 0f, T = 0.3f, Speed = Game.DancerBladeSpeed });
      var setRiotC = typeof(Game).GetMethod("SetRiotState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
      cput(EnemyType.Parrier, new V2(300f, 610f), 0f);
      var pa2 = cput(EnemyType.Parrier, new V2(450f, 610f), 0f); setRiotC.Invoke(null, new object[] { pa2, Game.RiotWinding, 0.3f });
      var pa3 = cput(EnemyType.Parrier, new V2(690f, 610f), 0f); pa3.Phase = (byte)(Game.RiotWalk << 3 | 2);
      var pa4 = cput(EnemyType.Parrier, new V2(850f, 610f), 0f); pa4.Phase = (byte)(Game.RiotStaggered << 3 | Game.RiotCracked);
      if (Environment.GetEnvironmentVariable("COURT2") != null) {  // COURT2=1 with COURT: step 2 instead: Seconds (walking, winding up, daggers in flight), Marshals (walking, baton up) and a circle round you (forming beside it, formed round you), Gallants (walking, saluting, reeling after a parry)
        cg.Enemies.Clear(); cg.Patches.Clear(); cg.Bullets.Clear();
        cput(EnemyType.Second, new V2(330f, 330f), 0f);
        var s2 = cput(EnemyType.Second, new V2(470f, 330f), 0.4f); s2.Winding = true;
        var s3 = cput(EnemyType.Second, new V2(330f, 420f), 0.3f);
        var spawnB = typeof(Game).GetMethod("SpawnBullet", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        for (int k = 0; k < Game.SecondFan; k++) { float a = 0.3f + (k - 2) * Game.SecondSpread; spawnB.Invoke(cg, new object[] { Defs.ThrownDagger, s3.Pos + V2.FromAngle(a) * 90f, V2.FromAngle(a) * Game.SecondSpeed, 0f, 3f, 10f, 0, false, 0f }); }
        cput(EnemyType.Marshal, new V2(760f, 330f), MathX.Pi);
        var m2 = cput(EnemyType.Marshal, new V2(820f, 440f), MathX.Pi); m2.Winding = true;
        cg.Patches[801] = new PathPatch { Id = 801, Kind = PatchKind.DuelCircle, Pos = me.Pos, Warn = Game.MarshalWarn, Life = Game.MarshalWarn + Game.MarshalHold, Age = 1.8f, R0 = Game.MarshalR, R1 = Game.MarshalR };
        cg.Patches[802] = new PathPatch { Id = 802, Kind = PatchKind.DuelCircle, Pos = new V2(1000f, 470f), Warn = Game.MarshalWarn, Life = Game.MarshalWarn + Game.MarshalHold, Age = 0.35f, R0 = Game.MarshalR, R1 = Game.MarshalR };
        cput(EnemyType.Gallant, new V2(300f, 640f), 0f);
        var g2 = cput(EnemyType.Gallant, new V2(470f, 640f), 0f); g2.Duel = new DuelState { SaluteT = 0.35f };
        var g3 = cput(EnemyType.Gallant, new V2(690f, 640f), 0f); g3.Duel = new DuelState { StaggerT = 1.5f };
      }
      if (Environment.GetEnvironmentVariable("COURT3") != null) {  // COURT3=1 with COURT: step 3 instead: a Harlequin walking, one showing its first line through you and one dashing the crossing arm with blades hanging on both; a Mummer walking, and one masked with its two copies round you firing (solid and hollow shots)
        cg.Enemies.Clear(); cg.Patches.Clear(); cg.Bullets.Clear();
        var spawnB3 = typeof(Game).GetMethod("SpawnBullet", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        cput(EnemyType.Harlequin, new V2(300f, 330f), 0f);
        var h2 = cput(EnemyType.Harlequin, new V2(330f, 520f), 0f);
        h2.Duel = new DuelState { Dash = DuelDash.Cross, DashWarn = 0.4f, DashFrom = h2.Pos, DashAng = 0f, DashLen = 410f };
        var h3 = cput(EnemyType.Harlequin, new V2(900f, 600f), -MathX.Pi / 2f);
        h3.Duel = new DuelState { Dash = DuelDash.Cross, DashWarn = 0f, DashFrom = new V2(900f, 720f), DashAng = -MathX.Pi / 2f, DashLen = 340f };
        for (int k = 1; k <= 4; k++) { h3.Duel.Blades.Add(new DuelBlade { Pos = new V2(780f + k * 70f, 600f), Ang = 0f, T = 0.2f, Speed = 200f }); }
        h3.Duel.Blades.Add(new DuelBlade { Pos = new V2(900f, 670f), Ang = -MathX.Pi / 2f, T = 0.6f, Speed = 200f });
        cput(EnemyType.Mummer, new V2(470f, 330f), 0f);
        V2 mc = new V2(760f, 360f);
        for (int k = 0; k < 3; k++)
        {
          V2 at = mc + V2.FromAngle(0.6f + MathX.Tau * k / 3f) * 95f;
          var m = cput(k == 0 ? EnemyType.Mummer : EnemyType.MummerCopy, at, (mc - at).Angle);
          V2 dir = (mc - at).Normalized();
          spawnB3.Invoke(cg, new object[] { k == 0 ? Defs.SmallShot : Defs.HollowShot, at + dir * 40f, dir * 260f, 0f, 3f, k == 0 ? 10f : 0f, 0, true, 0f });
        }
      }
      if (Environment.GetEnvironmentVariable("COURT4") != null) {  // COURT4=1 with COURT: step 4 instead: the Ring of Honour round you (ten Fencers, two Gallants), an opposite pair of Fencers showing their lines in, one Gallant saluting
        cg.Enemies.Clear(); cg.Patches.Clear(); cg.Bullets.Clear();
        for (int k = 0; k < Game.HonourSlots; k++)
        {
          V2 at = me.Pos + V2.FromAngle(0.2f + MathX.Tau * k / Game.HonourSlots) * 230f;
          var e = cput(k == 2 || k == 8 ? EnemyType.Gallant : EnemyType.Fencer, at, (me.Pos - at).Angle);
          if (k == 0 || k == 6) { e.Phase = 1; e.PhaseT = 0.2f; }
          if (k == 2) e.Duel = new DuelState { SaluteT = 0.4f };
        }
      }
      cg.AnimT += float.Parse(Environment.GetEnvironmentVariable("COURT"), System.Globalization.CultureInfo.InvariantCulture);
    }
    if (Environment.GetEnvironmentVariable("LUNGE") != null) {  // LUNGE=<0-1>: the four lungers part-way through their wind-up (LungeLaneLook.cs); the lane fills with the client's clock, so draw once, then move it on
      var lg = host.Game; lg.Enemies.Clear(); lg.Bullets.Clear();
      var lsp = typeof(Game).GetMethod("SpawnEnemyAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
      Func<EnemyType, V2, Enemy> lput = (t, at) => { var e = (Enemy)lsp.Invoke(lg, new object[] { t, at, 0f, 0, false }); e.SpawnT = 0f; e.Spawning = false; e.Disp = e.Pos; e.Phase = 1; e.PhaseT = 0.5f; e.Facing = 0f; return e; };
      me.Pos = me.Disp = new V2(1200f, 900f);
      lput(EnemyType.Lunger, new V2(300f, 200f)); lput(EnemyType.SpikeBlock, new V2(300f, 330f)); lput(EnemyType.Charger, new V2(300f, 460f));
      var rs = lput(EnemyType.RiotShield, new V2(300f, 590f)); rs.Phase = 0; typeof(Game).GetMethod("SetRiotState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, new object[] { rs, Game.RiotWinding, 0.4f });
      typeof(ArenaApp).GetMethod("DrawWorld", f2).Invoke(app, null);
      lg.AnimT += float.Parse(Environment.GetEnvironmentVariable("LUNGE"), System.Globalization.CultureInfo.InvariantCulture) * 0.6f;
    }
    if (Environment.GetEnvironmentVariable("SWORD") != null) {  // SWORD=1: the Swordsman (top row) and the Shield Swordsman (bottom row) in each phase: closing in, wind-up, cut, recovery (SwordsmanLook.cs); SWORDBLOCK=1 flashes the plate
      var sg = host.Game; sg.Enemies.Clear(); sg.Bullets.Clear();
      var ssp = typeof(Game).GetMethod("SpawnEnemyAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
      me.Pos = me.Disp = new V2(1500f, 900f);
      var ages = new[] { 0f, 0.3f, 0.07f, 0.35f };
      for (int row = 0; row < 2; row++)
        for (int ph = 0; ph < 4; ph++) {
          var e = (Enemy)ssp.Invoke(sg, new object[] { row == 0 ? EnemyType.Swordsman : EnemyType.ShieldSwordsman, new V2(260f + 260f * ph, 230f + 280f * row), 0f, 0, false });
          e.SpawnT = 0f; e.Spawning = false; e.Disp = e.Pos; e.Facing = 0f; e.Phase = (byte)ph; e.SeenPhase = (byte)ph; e.SeenT = ages[ph];
          if (Environment.GetEnvironmentVariable("SWORDBLOCK") != null) e.T2 = 0.2f;
        }
      typeof(ArenaApp).GetMethod("DrawWorld", f2).Invoke(app, null);
    }
    if (Environment.GetEnvironmentVariable("CULT") != null) {  // CULT=1: the Twin Reapers' cult caught mid-move (ReaperCultLook.cs): Wisps, an Acolyte raising its sickle, a Gravedigger, a Shade; a Bound pair with its tether and one channelling a revive; a Scythe Wraith marking; Lamplighters' lines and cross; a Mourner weeping
      var cg = host.Game; cg.Enemies.Clear(); cg.Bullets.Clear();
      var csp = typeof(Game).GetMethod("SpawnEnemyAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
      Func<EnemyType, V2, float, Enemy> cput = (t, at, face) => { var e = (Enemy)csp.Invoke(cg, new object[] { t, at, 0f, 0, false }); e.SpawnT = 0f; e.Spawning = false; e.Disp = e.Pos = at; e.Facing = face; return e; };
      Func<Enemy, Enemy> partner = e => cg.Enemies[e.LinkId];
      me.Pos = me.Disp = new V2(1480f, 470f);
      foreach (var o in cg.Players.Values) if (o != me) { o.Pos = o.Disp = new V2(1500f, 820f); }
      for (int k = 0; k < 3; k++) cput(EnemyType.Wisp, new V2(170f + 34f * k, 180f + 30f * (k % 2)), 0.2f);
      var aco = cput(EnemyType.Acolyte, new V2(560f, 190f), 0f); aco.Phase = 1; aco.SeenPhase = 1; aco.SeenT = 0.3f;
      cput(EnemyType.Gravedigger, new V2(700f, 200f), 0.3f);
      cput(EnemyType.Shade, new V2(1000f, 300f), 0f);
      var b1 = cput(EnemyType.BoundWraith, new V2(200f, 600f), 0f); var b2 = partner(b1); b2.Pos = b2.Disp = new V2(340f, 540f);  // tethered
      var c1 = cput(EnemyType.BoundWraith, new V2(560f, 600f), 0f); var c2 = partner(c1); c2.Pos = c2.Disp = new V2(-900f, -900f);   // one fell: the other channels it back
      c1.Phase = Game.BoundChanneling; c1.PhaseT = 1.2f; c1.Waypoint = new V2(660f, 540f); c1.LinkId = 0;
      var sw = cput(EnemyType.ScytheWraith, new V2(900f, 560f), 0f); sw.Phase = 1; sw.PhaseT = 0.3f;
      var l1 = cput(EnemyType.Lamplighter, new V2(200f, 380f), 0.15f); l1.Winding = true; l1.Phase = 0;
      var l2 = cput(EnemyType.Lamplighter, new V2(600f, 380f), -0.1f); l2.Winding = true; l2.Phase = 1;
      var mo = cput(EnemyType.Mourner, new V2(1180f, 640f), 0f); mo.Phase = 1;
      var fire = typeof(Game).GetMethod("ReaperCultFire", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
      for (int k = 0; k < 16; k++) { fire.Invoke(cg, new object[] { mo, 0f }); foreach (var bl in cg.Bullets.Values) bl.Pos += bl.Vel * 0.12f; }
      typeof(ArenaApp).GetMethod("DrawWorld", f2).Invoke(app, null);
      cg.AnimT += 0.3f;
    }
    if (Environment.GetEnvironmentVariable("ZOOM") != null) {  // ZOOM=<1-2>: the scroll-wheel zoom, centred on you and held inside the arena (CameraZoom.cs)
      float z = float.Parse(Environment.GetEnvironmentVariable("ZOOM"), System.Globalization.CultureInfo.InvariantCulture);
      float hx = Game.Width / (2f * z), hy = Game.Height / (2f * z);
      typeof(ArenaApp).GetField("camZoom", f2).SetValue(app, z);
      typeof(ArenaApp).GetField("camFocus", f2).SetValue(app, new V2(Math.Max(hx, Math.Min(Game.Width - hx, me.Disp.X)), Math.Max(hy, Math.Min(Game.Height - hy, me.Disp.Y))));
    }
    if (Environment.GetEnvironmentVariable("ROOMS") != null || Environment.GetEnvironmentVariable("EXPANDMAP") != null) {  // the rooms' following camera (CameraZoom.cs), at the ZOOM if any
      float z = (float)typeof(ArenaApp).GetField("camZoom", f2).GetValue(app);
      typeof(ArenaApp).GetField("camFollow", f2).SetValue(app, true);
      if (Environment.GetEnvironmentVariable("ZOOMHINT") != null) typeof(ArenaApp).GetField("zoomHintT", f2).SetValue(app, 6f);  // ZOOMHINT=1: the open Rooms map's "scroll to zoom out" prompt
      typeof(ArenaApp).GetField("camFocus", f2).SetValue(app, typeof(ArenaApp).GetMethod("RoomsFocus", f2).Invoke(app, new object[] { me.Disp, z }));
      if (Environment.GetEnvironmentVariable("BREACHTOUR") != null) {  // BREACHTOUR=1 (with BREACHAGE on a breach wave): the warning's camera tour (BreachTour.cs), no enemies yet
        host.Game.Enemies.Clear();
        var tourArgs = new object[] { typeof(ArenaApp).GetField("camFocus", f2).GetValue(app), null };
        if ((bool)typeof(ArenaApp).GetMethod("BreachTourFocus", f2).Invoke(app, tourArgs)) typeof(ArenaApp).GetField("camFocus", f2).SetValue(app, tourArgs[1]);
      }
    }
    if (Environment.GetEnvironmentVariable("SWING") != null)  // draw the swings once first (then throw that away), so each is already seen (its owner known) as after its first frame in the game
    {
      var slashE = typeof(ArenaApp).GetMethod("DrawSlashE", f2);
      var shoveL = typeof(ArenaApp).GetMethod("DrawStaffShoveLayer", f2);
      foreach (var e in host.Game.Effects)
        if (e.Kind == FxKind.Slash) slashE.Invoke(app, new object[] { e, 0f });
        else if (e.Kind == FxKind.StaffShove) shoveL.Invoke(app, new object[] { e, false });
      GL.Tris.Clear();
      typeof(ArenaApp).GetField("holdT", f2).SetValue(app, 0f);  // (a hit-pause it started would replay a frame)
    }
    if (Environment.GetEnvironmentVariable("PACSKIN") != null) Profile.Current.DiskLook = "pac";  // the Pac-Man skin (PacmanLook.cs)
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
    if (Environment.GetEnvironmentVariable("WAVECLEARED") != null) {  // the between-waves box: WAVECLEARED=1 (waiting), =auto (everyone on auto-ready), =boss (the boss is next), =beaten (the boss was just beaten)
      var wg = host.Game; string wc = Environment.GetEnvironmentVariable("WAVECLEARED");
      wg.State = MatchState.Intermission; wg.Wave = Environment.GetEnvironmentVariable("WCWAVE") != null ? int.Parse(Environment.GetEnvironmentVariable("WCWAVE")) : wc == "boss" || wc == "beaten" ? 5 : 4; wg.BossSlot = wc == "beaten"; wg.Leg = 1;  // (a Campaign: both legs of the wave cleared; boss: the boss comes next; beaten: it was just beaten; WCWAVE=<n>: the wave just cleared)
      wg.ProfileOverride = new Profile(); wg.StateTimer = 3.2f; wg.Me.Offer = null;
      var rep = new WaveSummary { Wave = wg.Wave, ClearTime = 42f, ScoreGain = 120 };
      int wi = 0; foreach (var wp in wg.Players.Values) { rep.Lines.Add(new WaveLine { Id = wp.Id, Kills = new[] { 18, 21, 13 }[wi % 3], Damage = new[] { 2140f, 2880f, 1310f }[wi % 3], Taken = new[] { 0f, 96f, 22f }[wi % 3], Awards = new[] { WaveAward.Untouched, WaveAward.MostDamage | WaveAward.MostKills, WaveAward.Reviver }[wi % 3] }); wp.AutoReady = wc == "auto"; wp.Ready = wi == 1; wi++; }
      wg.WaveReport = rep;
      // WCT=<seconds>: that long since the box appeared (its 0.35 s drop-in); settled by default (the preview's clock doesn't move).
      float wct = Environment.GetEnvironmentVariable("WCT") != null ? float.Parse(Environment.GetEnvironmentVariable("WCT"), System.Globalization.CultureInfo.InvariantCulture) : 1f;
      if (Time.unscaledTime < 5f) Time.unscaledTime = 5f;  // (a negative birth time reads as "not up yet")
      set("wcBorn", Time.unscaledTime - wct); set("wcWave", wg.Wave);
      if (Environment.GetEnvironmentVariable("WCSTEP") != null) set("wcStep", int.Parse(Environment.GetEnvironmentVariable("WCSTEP")));  // WCSTEP=0|1|2: Results, Build (needs EXPANDVOTE=<rooms> EXPANDFREE=1), Ready
      if (Environment.GetEnvironmentVariable("WCMIN") != null) set("wcMin", true);  // WCMIN=1: minimized to the strip at the top
    }
    if (Environment.GetEnvironmentVariable("PATHOFFER") != null) { host.Game.PathOffer = Environment.GetEnvironmentVariable("PATHOFFER") == "bazaar" ? new[] { PathId.Bazaar, PathId.EliteGauntlet } : Environment.GetEnvironmentVariable("PATHOFFER") == "4" ? new[] { PathId.Fogbound, PathId.NarrowPass } : Environment.GetEnvironmentVariable("PATHOFFER") == "3" ? new[] { PathId.FrozenLake, PathId.BloodMoon } : Environment.GetEnvironmentVariable("PATHOFFER") == "2" ? new[] { PathId.BurningGrounds, PathId.Sanctuary } : new[] { PathId.SwarmFields, PathId.EliteGauntlet }; host.Game.State = MatchState.Intermission; host.Game.Route.Add(PathId.SniperRidge); }
    if (Environment.GetEnvironmentVariable("EXPANDVOTE") != null) {  // EXPANDVOTE=<rooms already bought>: the Expanding arena's vote after a boss (ExpansionUi.cs)
      var eg = host.Game; eg.Diff.Set(DiffKnob.ArenaExpanding, 1f); if (Environment.GetEnvironmentVariable("EXPANDFREE") == null) eg.Diff.Set(DiffKnob.BuildVote, 1f); eg.State = MatchState.Intermission;  // EXPANDFREE=1: free building (the default setting; the vote shows by default here)
      int bought = int.Parse(Environment.GetEnvironmentVariable("EXPANDVOTE"));
      for (int k = 0; k < bought; k++) eg.Expanded.Add(Expansion.Offers(5, 5 + 5 * k, eg.Expanded)[0]);
      string ebuilt = Environment.GetEnvironmentVariable("EXPANDBUILT");  // EXPANDBUILT=x,y,w,h;...: drawn boxes on the map first (Expansion.Custom), owner 1
      if (ebuilt != null) foreach (var part in ebuilt.Split(';')) { var q = Array.ConvertAll(part.Split(','), int.Parse); string bw; var br = Expansion.Custom(eg.Expanded, q[0], q[1], q[2], q[3], out bw); if (br.HasValue) { var b2 = br.Value; b2.Owner = 1; eg.Expanded.Add(b2); } else Console.Error.WriteLine("EXPANDBUILT " + part + ": " + bw); }

      if (Environment.GetEnvironmentVariable("EXPANDFLOAT") != null && eg.Nodes.Count == 0) { foreach (var fk in new[] { RoomKind.Gatekeeper, RoomKind.Shop, RoomKind.Vendor }) { var gk = Expansion.FloatingFor(3, 2, eg.Expanded, fk); if (gk.HasValue) eg.Expanded.Add(gk.Value); } foreach (var nk in new[] { NodeKind.Chest }) { var nn = Expansion.NodeFor(3, 0, eg.Expanded, eg.Nodes, nk); if (nn.HasValue) eg.Nodes.Add(nn.Value); } if (Environment.GetEnvironmentVariable("EXPANDFLOAT") == "2" && eg.Nodes.Count > 0) { for (int stp = 0; stp < 3 && !Expansion.NodeClaimed(eg.Expanded, eg.Nodes[0]); stp++) { var hh = Expansion.NodeStep(eg.Expanded, eg.Nodes[0]); if (!hh.HasValue) break; var h2 = hh.Value; h2.Owner = 1; eg.Expanded.Add(h2); } } }  // EXPANDFLOAT=1: floating Gatekeeper, shop and vendor rooms and a chest node off the map; =2 also builds a hallway and a room round the chest node; =3 builds a box up to the shop room (opened)=1: a floating shop, vendor and Gatekeeper room off the map
      if (Environment.GetEnvironmentVariable("EXPANDFLOAT") == "3") { int sfi = eg.Expanded.FindIndex(r => r.Floating && r.Kind == RoomKind.Shop); var sfc = sfi >= 0 ? Expansion.ConnectorFor(eg.Expanded, sfi) : null; if (sfc.HasValue) { var sf2 = sfc.Value; sf2.Owner = 1; eg.Expanded.Add(sf2);  typeof(Game).GetField("expWorld", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(eg, null); } }  // EXPANDFLOAT=3: a box built up to the shop room, so it is open
      eg.StateTimer = 8f; eg.ExpandOpen = true;  // the round is open: each player's proposal, the first one backed
      if (Environment.GetEnvironmentVariable("EXPANDMIDWAVE") != null) { eg.State = MatchState.Fighting; eg.ExpandOpen = false; eg.Wave = 7; eg.RoomsUp = true; eg.BreachAge = 12f; MockBreachRooms(eg); }  // EXPANDMIDWAVE=1: the draw screen mid-wave (Open wall only; a breach wave, so its breaches show)
      var propPool = Expansion.Offers(5, 5 + 5 * bought, eg.Expanded); int pk = 0;
      foreach (var pl in eg.Players.Values) { pl.Vectors = 30 + 8 * pk; if (pk < propPool.Count) { var pr = propPool[pk]; pr.Owner = pl.Id; eg.ExpandProposals.Add(pr); } pk++; }
      if (Environment.GetEnvironmentVariable("EXPANDGATECARD") != null && eg.ExpandProposals.Count > 0) {  // EXPANDGATECARD=1: the first proposal is a shutter gate
        var gp = Gates.Proposal(Gates.Of(eg.ExpansionWorld.Doors[eg.ExpansionWorld.Doors.Length - 1])); gp.Owner = eg.ExpandProposals[0].Owner; eg.ExpandProposals[0] = gp;
      }
      if (Environment.GetEnvironmentVariable("EXPANDLINKCARD") != null && eg.ExpandProposals.Count > 0) {  // EXPANDLINKCARD=1: the first proposal is a portal pair
        string lw; var bl = Expansion.BestLink(eg.ExpansionWorld, 0);
        if (bl.HasValue) { var lp = Expansion.LinkProposal(bl.Value, Expansion.LinkPrice(eg.ExpansionWorld, 0, bl.Value, out lw)); lp.Owner = eg.ExpandProposals[0].Owner; eg.ExpandProposals[0] = lp; }
      }
      if (eg.ExpandProposals.Count > 0) eg.ExpandVotes[eg.MyId] = eg.ExpandProposals[0].Owner;
      // EXPANDCHIP=1: just the chip; otherwise the window is open (=2 too)
      if (Environment.GetEnvironmentVariable("EXPANDCHIP") != "1") typeof(ArenaApp).GetField("expOpen", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(app, true);
      string drawAt = Environment.GetEnvironmentVariable("EXPANDDRAW");  // EXPANDDRAW=x,y,w,h: the drawing screen with that box dragged out
      if (drawAt != null) {
        var dq = drawAt.Split(','); var ci = System.Globalization.CultureInfo.InvariantCulture;
        var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        float dx0 = float.Parse(dq[0], ci), dy0 = float.Parse(dq[1], ci), dw0 = float.Parse(dq[2], ci), dh0 = float.Parse(dq[3], ci);
        typeof(ArenaApp).GetField("expDrawing", bf).SetValue(app, true);
        string themeName = Environment.GetEnvironmentVariable("EXPANDTHEME");  // EXPANDTHEME=FrozenLake: that theme picked on the drawing screen
        if (themeName != null) typeof(ArenaApp).GetField("expTheme", bf).SetValue(app, (PathId)Enum.Parse(typeof(PathId), themeName));
        typeof(ArenaApp).GetField("expA", bf).SetValue(app, new V2(dx0, dy0));
        typeof(ArenaApp).GetField("expB", bf).SetValue(app, new V2(dx0 + dw0, dy0 + dh0));
        string dz = Environment.GetEnvironmentVariable("EXPANDZOOM");  // EXPANDZOOM=z,x,y: the draw map zoomed z times on that world point
        if (dz != null) { var zq = Array.ConvertAll(dz.Split(','), v => float.Parse(v, ci)); typeof(ArenaApp).GetField("expZoom", bf).SetValue(app, zq[0]); typeof(ArenaApp).GetField("expViewC", bf).SetValue(app, new V2(zq[1], zq[2])); }
        if (Environment.GetEnvironmentVariable("EXPANDGATEMODE") != null) {  // EXPANDGATEMODE=1: the draw screen in Gate mode (one gate already fitted); MOUSE over a door to point at it
          typeof(ArenaApp).GetField("expGateMode", bf).SetValue(app, true);
          eg.GateFits.Add(Gates.Of(eg.ExpansionWorld.Doors[0]));
        }
        if (Environment.GetEnvironmentVariable("EXPANDLINK") != null) {  // EXPANDLINK=1: the draw screen in Portal mode with the best pair placed (and one pair already built)
          typeof(ArenaApp).GetField("expLinkMode", bf).SetValue(app, true);
          var first = Expansion.BestLink(eg.ExpansionWorld, 0);
          if (first.HasValue) { eg.Links.Add(first.Value); typeof(Game).GetField("expWorld", bf).SetValue(eg, null); }
          var next = Expansion.BestLink(eg.ExpansionWorld, eg.Links.Count);
          if (next.HasValue) { typeof(ArenaApp).GetField("expA", bf).SetValue(app, next.Value.A); typeof(ArenaApp).GetField("expB", bf).SetValue(app, next.Value.B); }
        }
        string outl = Environment.GetEnvironmentVariable("EXPANDOUTLINE");  // EXPANDOUTLINE=x,y;x,y;...: the draw screen in Room mode with those corners laid (closed back to the first)
        if (outl != null) {
          var pts = (System.Collections.Generic.List<int[]>)typeof(ArenaApp).GetField("expPts", bf).GetValue(app);
          foreach (var part in outl.Split(';')) pts.Add(Array.ConvertAll(part.Split(','), int.Parse));
          eg.Me.Vectors = 60;
        }
        if (Environment.GetEnvironmentVariable("EXPANDCUT") != null) {  // EXPANDCUT=1: the draw screen in Open wall mode with the first shared wall dragged out
          typeof(ArenaApp).GetField("expCutMode", bf).SetValue(app, true);
          string cw; var cr = Expansion.Custom(eg.Expanded, 1600, 100, 800, 600, out cw); if (cr.HasValue) { eg.Expanded.Add(cr.Value); typeof(Game).GetField("expWorld", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(eg, null); }
          var sws = Expansion.SharedWalls(eg.ExpansionWorld);
          if (sws.Count > 0) { var sw = sws[0]; bool vtc = sw[0] == 1; typeof(ArenaApp).GetField("expA", bf).SetValue(app, new V2(vtc ? sw[1] : sw[2], vtc ? sw[2] : sw[1])); typeof(ArenaApp).GetField("expB", bf).SetValue(app, new V2(vtc ? sw[1] : Math.Min(sw[3], sw[2] + 400), vtc ? Math.Min(sw[3], sw[2] + 400) : sw[1])); }
        }
      }
    }
    if (Environment.GetEnvironmentVariable("EXPANDSHOP") != null) {  // EXPANDSHOP=1: the shop is open in the Expanding arena and you're not in the shop room (hint banner); EXPANDSHOP=2: you're in it (the shop)
      var xg = host.Game; xg.Diff.Set(DiffKnob.ArenaExpanding, 1f); xg.State = MatchState.Intermission; xg.RoomsUp = true;
      string xw; var xr = Expansion.Custom(xg.Expanded, 1600, 300, 600, 300, out xw).Value; xr.Kind = RoomKind.Shop; xg.Expanded.Add(xr);
      foreach (var pl in xg.Players.Values) { pl.Gold = 180; pl.Pos = pl.Disp = Environment.GetEnvironmentVariable("EXPANDSHOP") == "2" ? xg.CurrentWorld.Areas[1].Mid : new V2(800f, 450f); }
      typeof(Game).GetMethod("OpenShop", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(xg, null);
    }
    if (Environment.GetEnvironmentVariable("PATHOFFER") != null || Environment.GetEnvironmentVariable("PATHON") != null || Environment.GetEnvironmentVariable("EXPANDVOTE") != null || Environment.GetEnvironmentVariable("EXPANDSHOP") != null) { Event.current.type = EventType.Repaint; set("glMaterial", new Material(null)); GL.ToUi = true; }
    if (Environment.GetEnvironmentVariable("HUDT") != null) { float ht = float.Parse(Environment.GetEnvironmentVariable("HUDT")); set("hudT", ht); set("hudShown", ht > 0.5f); }
    if (Environment.GetEnvironmentVariable("HUDMODE") != null) { int hm = int.Parse(Environment.GetEnvironmentVariable("HUDMODE")); set("hudMode", hm); set("hudShown", hm == 0); set("hudT", hm == 0 ? 1f : 0f); set("miniT", hm == 1 || hm == 2 ? 1f : 0f); }
    DrawMode();
    Event.current.type = EventType.Repaint; typeof(ArenaApp).GetMethod("DrawCabEdges", f2).Invoke(app, null);  // the cabinet menus' soft edges, as ArenaApp.OnGUI draws them (CrtWindows.cs)
    using (var w = new BinaryWriter(File.Create(args[0] + ".tris"))) foreach (var v in GL.Tris) w.Write(v);
    File.WriteAllLines(args[0] + ".ui", GUI.Log.ToArray());
    Console.WriteLine("state {0} wave {1}, tris {2}, ui items {3}", host.Game.State, host.Game.Wave, GL.Tris.Count / 18, GUI.Log.Count);
    ClipFrames();
    host.Leave(""); foreach (var c in others) c.Leave("");
  }
}
