using System; using System.IO; using System.Reflection; using System.Threading; using Arena; using UnityEngine;
class Shot {
  static void Step(Session s) { s.Poll(); s.Game.FixedStep(1f/60f, new PlayerInput()); s.Game.Update(1f/60f); }
  static bool heraldOn, eclipseOn;
  static bool devourOn;
  // Two made-up saved runs and a profile in a temp folder, for the menu's Continue list, Records and the lobby.
  static void PreviewSaves() {
    string dir = Path.Combine(Path.GetTempPath(), "arena-preview-saves");
    if (Directory.Exists(dir)) Directory.Delete(dir, true);
    SaveStore.Folder = dir; Profile.Folder = dir; Profile.Reload();
    var a = new RunSave { Id = "a", SavedAt = DateTime.UtcNow.AddMinutes(-40), Wave = 23, Score = 18450, RunTime = 2710f, Mods = RunMod.Lucky | RunMod.Frenzy, CurrentPath = PathId.Bazaar, PathFrom = 21, PathTo = 24 };
    a.Seats.Add(new SeatSave { Name = "Anthony", Role = Role.Tank, Kit = Kit.Knight, Weapon = 1, Ability = 2, Level = 17 });
    a.Seats.Add(new SeatSave { Name = "Aleon", Role = Role.Dps, Kit = Kit.Mage, Weapon = 0, Ability = 1, Level = 16 });
    var b = new RunSave { Id = "b", SavedAt = DateTime.UtcNow.AddDays(-2), Wave = 7, Score = 3120, RunTime = 690f };
    b.Seats.Add(new SeatSave { Name = "Anthony", Role = Role.Healer, Kit = Kit.Ninja, Weapon = 2, Ability = 0, Level = 6 });
    SaveStore.Write(a); SaveStore.Write(b);
    var pr = new Profile { RunsStarted = 14, RunsWon = 1, RunsLost = 9, BestWave = 50, BestScore = 41200, BestEndlessWave = 0, PlayTime = 41000f, Kills = 6812, BossKills = 52, Downs = 37, Revives = 21, DamageDealt = 2.3e6f, HealingDone = 81000f };
    foreach (var t in new[] { EnemyType.Boss, EnemyType.Boss, EnemyType.HiveQueen, EnemyType.TwinA, EnemyType.Singularity, EnemyType.Colossus, EnemyType.Duelist, EnemyType.Lich, EnemyType.StormHerald, EnemyType.Devourer, EnemyType.Eclipse }) pr.BossBeaten(t, 10);
    pr.RunsAs[Profile.ClassKey(Role.Tank, Kit.Knight)] = 6; pr.RunsAs[Profile.ClassKey(Role.Dps, Kit.Mage)] = 3; pr.RunsAs[Profile.ClassKey(Role.Healer, Kit.Ninja)] = 1; pr.WinsAs[Profile.ClassKey(Role.Tank, Kit.Knight)] = 1;
    File.WriteAllText(Path.Combine(dir, "profile.txt"), pr.ToText()); Profile.Reload();
  }
  static void Main(string[] args) {
    int wave = int.Parse(args[1]); float seconds = float.Parse(args[2]); int players = int.Parse(args[3]);
    UnityEngine.Screen.width = 1280; UnityEngine.Screen.height = 720;
    var host = new Session { AutoStart = players, StartWave = wave }; host.Game.Bot = true;
    var melee = Environment.GetEnvironmentVariable("KNIGHT") ?? Environment.GetEnvironmentVariable("NINJA") ?? Environment.GetEnvironmentVariable("BLOOD");
    Kit meleeKit = Environment.GetEnvironmentVariable("NINJA") != null ? Kit.Ninja : Environment.GetEnvironmentVariable("BLOOD") != null ? Kit.BloodMage : Kit.Knight;
    host.Host(7990 + new System.Random().Next(500), "Anthony", Role.Tank, melee != null ? int.Parse(melee) : 0, 0, melee != null ? meleeKit : Kit.Mage);
    var others = new System.Collections.Generic.List<Session>();
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
    foreach (var e in host.Game.Enemies.Values) if (e.Type == EnemyType.Boss) Console.WriteLine("warden stage {0} turn {1} turnT {2:0.0} trans {3:0.0} light {4} gates {5} pos {6:0},{7:0}", e.Warden?.Stage, e.Warden?.Turn, e.Warden?.TurnT, e.Warden?.TransT, e.Warden?.LightMode, e.Warden?.Gates.Count, e.Pos.X, e.Pos.Y);
    var me = host.Game.Players[1]; if (Environment.GetEnvironmentVariable("PARRY") == null) me.Hp = me.MaxHp * 0.62f; me.ShieldHp = 40f; me.Xp = Defs.XpToNext(me.Level) * 0.4f;
    me.Slots[1].Charges = 0; me.Slots[1].Timer = 4.2f;
    if (Environment.GetEnvironmentVariable("POSE") != null) { host.Game.Bot = false; me.Pos = me.Disp = new V2(800f, 450f); me.Aim = -0.4f; }
    if (Environment.GetEnvironmentVariable("BOW") != null) { me.Weapon = Defs.Weapon(Role.Healer, Kit.Soldier, 2); host.Game.Bot = false; me.Pos = me.Disp = new V2(800f, 450f); me.Aim = -0.4f; me.DrawT = float.Parse(Environment.GetEnvironmentVariable("BOW")); }
    if (Environment.GetEnvironmentVariable("VEIN") != null) {
      // Vein Tether: one beam on an enemy (VEIN=1), or on nothing (VEIN=0).
      me.Weapon = System.Array.Find(Defs.Weapons, w => w.Id == WeaponId.VeinTether); me.Kit = Kit.BloodMage; host.Game.Bot = false;
      me.Pos = me.Disp = new V2(700f, 450f); me.Aim = -0.35f; me.FiringSeen = true; me.Firing = true;
      if (Environment.GetEnvironmentVariable("VEIN") == "1") foreach (var e in host.Game.Enemies.Values) { e.Pos = e.Disp = me.Pos + V2.FromAngle(me.Aim) * 230f; break; }
    }
    if (Environment.GetEnvironmentVariable("LEECH") != null) {
      // Leech Tendrils: LEECH=<n> enemies held in the arc (0 = free tendrils feeling about).
      me.Weapon = System.Array.Find(Defs.Weapons, w => w.Id == WeaponId.LeechBeam); me.Kit = Kit.BloodMage; host.Game.Bot = false;
      me.Pos = me.Disp = new V2(700f, 450f); me.Aim = -0.35f; me.FiringSeen = true; me.Firing = true; me.Grasp.Clear();
      int held = int.Parse(Environment.GetEnvironmentVariable("LEECH")), k = 0;
      foreach (var e in host.Game.Enemies.Values) {
        if (k < held) { e.Pos = e.Disp = me.Pos + V2.FromAngle(me.Aim + (k - (held - 1) / 2f) * 0.42f) * (120f + 25f * (k % 2)); me.Grasp.Add(e.Id); }
        else if (e.Pos.DistanceTo(me.Pos) < 260f) e.Pos = e.Disp = me.Pos + (e.Pos - me.Pos).Normalized() * 260f;
        k++;
      }
    }
    if (Environment.GetEnvironmentVariable("RADIANT") != null) {
      // The Radiant Orb held out on an enemy up and to the right.
      me.Weapon = Defs.WeaponOf(WeaponId.RadiantOrb); host.Game.Bot = false; me.Pos = me.Disp = new V2(700f, 450f);
      V2 at = me.Pos + V2.FromAngle(-0.5f) * 250f;
      foreach (var e in host.Game.Enemies.Values) { e.Pos = e.Disp = at + new V2(10f, 0f); break; }
      for (int f = 0; f < 60; f++) { me.Pos = new V2(700f, 450f); host.Poll(); host.Game.FixedStep(1f / 60f, new PlayerInput { Fire = true, AimAt = at }); host.Game.Update(1f / 60f); }
    }
    if (Environment.GetEnvironmentVariable("SOULGHOST") != null) {
      // Soul Link cast alone: the Soul Ghost beside you, tethered, a little hurt, SOULGHOST=<seconds> in.
      host.Game.Bot = false; me.Pos = me.Disp = new V2(700f, 450f); me.Aim = -0.4f; me.AimAt = me.Pos + V2.FromAngle(-0.4f) * 300f;
      me.Slots[0].Id = AbilityId.SoulLink; me.Slots[0].Charges = 1;
      typeof(Game).GetMethod("HostAbility", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(host.Game, new object[] { 1, 0, me.Pos, false });
      for (int f = 0; f < 60 * float.Parse(Environment.GetEnvironmentVariable("SOULGHOST")); f++) { me.Pos = new V2(700f, 450f); host.Poll(); host.Game.FixedStep(1f / 60f, new PlayerInput { AimAt = me.AimAt }); host.Game.Update(1f / 60f); }
      foreach (var m in host.Game.Minions.Values) if (m.Kind == MinionKind.SoulGhost) { m.Hp = m.MaxHp * 0.7f; m.GuardFrac = 0.7f; }
    }
    if (Environment.GetEnvironmentVariable("FLAME") != null) {
      // The Flamethrower held on a few enemies for FLAME=<seconds>: the stream and the burning.
      me.Weapon = Defs.WeaponOf(WeaponId.Flamethrower); host.Game.Bot = false;
      me.Pos = me.Disp = new V2(700f, 450f);
      int k = 0;
      foreach (var e in host.Game.Enemies.Values) { if (k < 3) { e.Pos = e.Disp = me.Pos + new V2(90f + 45f * k, -40f + 40f * k); e.Hp = e.MaxHp = 1e5f; } else e.Pos = e.Disp = new V2(100f + 20f * k, 120f); k++; }
      V2 at = me.Pos + new V2(200f, 0f);
      for (int f = 0; f < 60 * float.Parse(Environment.GetEnvironmentVariable("FLAME")); f++) {
        int j = 0; foreach (var e in host.Game.Enemies.Values) { if (j < 3) { e.Pos = me.Pos + new V2(90f + 45f * j, -40f + 40f * j); e.FireT = 9f; } j++; }
        me.Pos = new V2(700f, 450f);
        host.Poll(); host.Game.FixedStep(1f / 60f, new PlayerInput { Fire = true, AimAt = at }); host.Game.Update(1f / 60f);
      }
      foreach (var e in host.Game.Enemies.Values) Console.WriteLine("flame: {0} burning {1} hp lost {2:0}", e.Type, e.Burning, e.MaxHp - e.Hp);
    }
    if (Environment.GetEnvironmentVariable("WARDENLOOT") != null) {
      // The Warden's legendaries: two Warden's Guards at your side and the Stun Gun out with 4 of its 6 shots left.
      System.Func<string, int> UL = n => System.Array.FindIndex(Upgrades.All, u => u.Name == n);
      host.Game.GrantUpgrade(1, UL("Warden's Guard")); host.Game.GrantUpgrade(1, UL("Warden's Guard")); host.Game.GrantUpgrade(1, UL("Stun Gun")); host.Game.GrantUpgrade(1, UL("Searchlight"));
      for (int f = 0; f < 90; f++) { Step(host); Thread.Sleep(1); }
      foreach (var m in host.Game.Minions.Values) if (m.Kind == MinionKind.WardenGuard) { m.Hp = m.MaxHp * 0.6f; break; }
      host.Game.Bot = false; me.Pos = me.Disp = new V2(800f, 450f); me.Aim = -0.4f;
      foreach (var m in host.Game.Minions.Values) if (m.Kind == MinionKind.WardenGuard) { m.Pos = m.Disp = me.Pos + V2.FromAngle(m.Id) * 40f; m.Aim = -0.4f; }
      Step(host);
      me.StunShots = 4;
      foreach (var sl in me.Slots) if (sl.Id == AbilityId.StunGun) { sl.Charges = 0; sl.Timer = Upgrades.Ability(AbilityId.StunGun).Cooldown; }
      // The Searchlight on an enemy up and to the right.
      foreach (var e in host.Game.Enemies.Values) { e.Pos = e.Disp = me.Pos + V2.FromAngle(-0.4f) * 260f; me.AimAt = me.LightPos = me.LightDisp = e.Pos + new V2(20f, 10f); break; }
    }
    if (Environment.GetEnvironmentVariable("QUEENLOOT") != null) {
      // The Hive Queen's legendaries: three Hive Drones (one out stinging an enemy), a Bomber Escort bomber on its way
      // to another, and a Royal Dive honey pool where you just landed.
      System.Func<string, int> UL = n => System.Array.FindIndex(Upgrades.All, u => u.Name == n);
      var bf = BindingFlags.NonPublic | BindingFlags.Instance;
      host.Game.GrantUpgrade(1, UL("Hive Drones")); host.Game.GrantUpgrade(1, UL("Hive Drones")); host.Game.GrantUpgrade(1, UL("Royal Dive")); host.Game.GrantUpgrade(1, UL("Bomber Escort"));
      for (int f = 0; f < 90; f++) { Step(host); Thread.Sleep(1); }
      host.Game.Bot = false; me.Pos = me.Disp = new V2(700f, 470f); me.Aim = -0.3f; me.AimAt = me.Pos + V2.FromAngle(me.Aim) * 200f;
      var foes = new System.Collections.Generic.List<Enemy>(host.Game.Enemies.Values);
      if (foes.Count > 0) { foes[0].Pos = foes[0].Disp = me.Pos + new V2(230f, -120f); }
      if (foes.Count > 1) { foes[1].Pos = foes[1].Disp = me.Pos + new V2(-260f, -150f); }
      int k = 0;
      foreach (var m in host.Game.Minions.Values) if (m.Kind == MinionKind.HiveDrone)
      {
        if (k == 0 && foes.Count > 0) { m.Pos = m.Disp = foes[0].Pos + V2.FromAngle(2.4f) * (foes[0].Radius + 11f); m.Aim = (foes[0].Pos - m.Pos).Angle; }
        else { m.Ang = 1.2f + 2.1f * k; m.Pos = m.Disp = me.Pos + V2.FromAngle(m.Ang) * m.Def.OrbitRadius; m.Aim = m.Ang + MathX.Pi / 2f; }
        k++;
      }
      var sm = typeof(Game).GetMethod("SpawnMinion", bf);
      var bomber = (Minion)sm.Invoke(host.Game, new object[] { MinionKind.HiveBomber, me, me.Pos + new V2(-120f, -70f), 2.5f, false });
      bomber.T = 1.4f; bomber.Disp = bomber.Pos; if (foes.Count > 1) bomber.Aim = (foes[1].Pos - bomber.Pos).Angle;
      var az = typeof(Game).GetMethod("AddZone", bf);
      var pool = (Zone)az.Invoke(host.Game, new object[] { ZoneKind.HoneyPool, me, me.Pos + new V2(10f, 30f), Game.DivePool, Game.DivePoolTime });
      pool.T = Game.DivePoolTime * 0.7f;
    }
    if (Environment.GetEnvironmentVariable("REAPERLOOT") != null) {
      // The Twin Reapers' legendaries: three Twin Scythes (one reaping out at an enemy), and a thrown scythe on its way.
      System.Func<string, int> UL = n => System.Array.FindIndex(Upgrades.All, u => u.Name == n);
      var bf = BindingFlags.NonPublic | BindingFlags.Instance;
      host.Game.GrantUpgrade(1, UL("Twin Scythes")); host.Game.GrantUpgrade(1, UL("Twin Scythes")); host.Game.GrantUpgrade(1, UL("Twin Scythes")); host.Game.GrantUpgrade(1, UL("Scythe Throw"));
      for (int f = 0; f < 60; f++) { Step(host); Thread.Sleep(1); }
      host.Game.Bot = false; me.Pos = me.Disp = new V2(700f, 470f); me.Aim = -0.3f; me.AimAt = me.Pos + V2.FromAngle(me.Aim) * 200f;
      var foes = new System.Collections.Generic.List<Enemy>(host.Game.Enemies.Values);
      if (foes.Count > 0) { foes[0].Pos = foes[0].Disp = me.Pos + new V2(-200f, -90f); }
      int k = 0;
      foreach (var m in host.Game.Minions.Values) if (m.Kind == MinionKind.Scythe)
      {
        float ring = k == 0 ? Game.ScytheInner : k == 1 ? Game.ScytheOuter : Game.ScytheMiddle;
        if (k == 1 && foes.Count > 0) { m.Pos = m.Disp = foes[0].Pos + new V2(foes[0].Radius + 14f, 10f); m.Aim = MathX.Pi; }
        else { float a = 0.9f + 2.2f * k; m.Pos = m.Disp = me.Pos + V2.FromAngle(a) * ring; m.Aim = a + (k == 0 ? 1f : -1f) * MathX.Pi / 2f; }
        k++;
      }
      var sm = typeof(Game).GetMethod("SpawnMinion", bf);
      var thrown = (Minion)sm.Invoke(host.Game, new object[] { MinionKind.ThrownScythe, me, me.Pos + new V2(220f, -110f), 2f, false });
      thrown.Disp = thrown.Pos;
    }
    if (Environment.GetEnvironmentVariable("IMPLODE") != null) {
      // Implosion: the cursor on an enemy (its target marker), and implosions going off at IMPLODE=<0..1> of the way through.
      me.Weapon = System.Array.Find(Defs.Weapons, w => w.Id == WeaponId.Implosion); me.Kit = Kit.BloodMage; host.Game.Bot = false;
      me.Pos = me.Disp = new V2(700f, 450f); me.Aim = -0.35f;
      int k = 0;
      foreach (var e in host.Game.Enemies.Values) {
        e.Pos = e.Disp = me.Pos + V2.FromAngle(me.Aim + 0.5f * k) * (230f + 40f * k);
        if (k == 0) me.AimAt = e.Disp;
        else if (k < 3) host.Game.Effects.Add(new Effect { Kind = FxKind.Implode, Pos = e.Disp, Color = Defs.BulletColor(Defs.ImplodeShot), Radius = e.Radius + 55f, Dur = 0.35f, T = 0.35f * float.Parse(Environment.GetEnvironmentVariable("IMPLODE")) });
        if (++k == 3) break;
      }
    }
    if (Environment.GetEnvironmentVariable("REVOLVER") != null) { me.Weapon = Defs.Weapon(Role.Healer, Kit.Soldier, 1); me.AmmoUsed = 3; me.Stats.ExtraShots = 1; host.Game.Bot = false; me.Pos = me.Disp = new V2(800f, 450f); Console.WriteLine("cap " + host.Game.RevolverCapacity(me)); }
    var app = new ArenaApp();
    var f2 = BindingFlags.NonPublic | BindingFlags.Instance;
    if (Environment.GetEnvironmentVariable("GOLD") != null) me.Gold = int.Parse(Environment.GetEnvironmentVariable("GOLD"));  // the Bazaar's gold on the HUD
    Action<string, object> set = (n, v) => typeof(ArenaApp).GetField(n, f2).SetValue(app, v);
    set("session", host); set("game", host.Game); set("scale", 1f); set("drawScale", 1280f / Game.Width);
    if (Environment.GetEnvironmentVariable("AIMLINE") != null)
    {
      // Point the mouse up and to the right of the player: AIMLINE=<distance> (1 = 380 px; screen y is flipped).
      float aimDist = float.Parse(Environment.GetEnvironmentVariable("AIMLINE")); if (aimDist <= 1f) aimDist = 380f;
      V2 target = me.Pos + V2.FromAngle(-0.5f) * aimDist;
      float ws = 1280f / Game.Width;
      Input.mousePosition = new Vector3(target.X * ws, 720f - target.Y * ws, 0f);
      me.Aim = (target - me.Pos).Angle;
    }
    if (Environment.GetEnvironmentVariable("AIMDEBUG") != null)
    {
      typeof(ArenaApp).GetField("aimDebug", f2).SetValue(app, true);
      me.Aim = -0.6f;
      host.Game.LastShotId = 1; host.Game.LastShotAngle = -0.62f; host.Game.LastShotAim = -0.6f; host.Game.LastShotFrom = me.Pos + V2.FromAngle(-0.62f) * 20f;
    }
    if (Environment.GetEnvironmentVariable("FROST") != null) {
      // The Frost Lance's icicle FROST=<0..1> of the way through, with its splinters once they've gone off.
      float x = float.Parse(Environment.GetEnvironmentVariable("FROST")), it = x * Game.IcicleFxTime, st = it - Game.SplinterDelay;
      me.Pos = me.Disp = new V2(600f, 450f); me.Aim = -0.3f; host.Game.Bot = false;
      V2 tipAt = me.Pos + V2.FromAngle(me.Aim) * 230f;
      var frost = Defs.BulletColor(Defs.FrostShot);
      host.Game.Effects.Add(new Effect { Kind = FxKind.Icicle, Pos = me.Pos, Pos2 = tipAt, Color = frost, Radius = Game.IcicleHalfWidth, Dur = Game.IcicleFxTime, T = it });
      if (st > 0f && st < 0.3f)
        host.Game.Effects.Add(new Effect { Kind = FxKind.Splinter, Pos = me.Pos, Pos2 = tipAt, Color = frost, Radius = Game.SplinterReach, Dur = 0.3f, T = st });
    }
    if (Environment.GetEnvironmentVariable("HAMMER") != null) {
      // Storm Hammers in flight, and an electrified enemy arcing to the ones round it.
      var sbh = typeof(Game).GetMethod("SpawnBullet", BindingFlags.NonPublic | BindingFlags.Instance);
      me.Pos = me.Disp = new V2(500f, 450f); host.Game.Bot = false;
      for (int k = 0; k < 3; k++) sbh.Invoke(host.Game, new object[] { Defs.HammerShot, me.Pos + new V2(70f + 70f * k, -20f * k), new V2(460f, 0f), 0f, 1f, 40f, 1, false, 0f });
      var zap = new System.Collections.Generic.List<Enemy>(host.Game.Enemies.Values);
      for (int k = 0; k < zap.Count && k < 4; k++) zap[k].Pos = zap[k].Disp = new V2(900f, 450f) + (k == 0 ? new V2(0f, 0f) : V2.FromAngle(k * 2.1f) * 110f);
      for (int k = 1; k < zap.Count && k < 4; k++) host.Game.Effects.Add(new Effect { Kind = FxKind.Bolt, Pos = zap[0].Pos, Pos2 = zap[k].Pos, Color = Defs.BulletColor(Defs.BoltShot), Dur = 0.15f, T = 0.03f });
      if (zap.Count > 0) host.Game.Effects.Add(new Effect { Kind = FxKind.Spark, Pos = zap[0].Pos, Color = Defs.BulletColor(Defs.HammerShot), Radius = zap[0].Radius + 14f, Dur = 0.3f, T = 0.12f });
    }
    if (Environment.GetEnvironmentVariable("BOULDER") != null) {
      // The Golem Staff: charging a throw (BOULDER=<draw seconds>, with the landing marker), and boulders in flight.
      var sbb = typeof(Game).GetMethod("SpawnBullet", BindingFlags.NonPublic | BindingFlags.Instance);
      me.Weapon = System.Array.Find(Defs.Weapons, w => w.Id == WeaponId.GolemStaff); me.Kit = Kit.Summoner; host.Game.Bot = false;
      me.Pos = me.Disp = new V2(500f, 500f); me.Aim = -0.25f; me.DrawT = float.Parse(Environment.GetEnvironmentVariable("BOULDER"));
      for (int k = 0; k < 3; k++)
      {
        var b = (Bullet)sbb.Invoke(host.Game, new object[] { Defs.BoulderShot, new V2(560f + 110f * k, 300f), new V2(500f, 0f), 0f, 0.7f, 40f, 1, false, 0f });
        b.Life = 0.7f * (0.8f - 0.3f * k);
      }
    }
    if (Environment.GetEnvironmentVariable("CAGE") != null) {
      // The Bone Guard: an enemy caged by its darts, the Skeleton Guards shooting at it.
      me.Weapon = System.Array.Find(Defs.Weapons, w => w.Id == WeaponId.BoneGuard); me.Kit = Kit.Summoner; host.Game.Bot = false;
      me.Pos = me.Disp = new V2(500f, 450f);
      foreach (var e in host.Game.Enemies.Values) { e.Pos = e.Disp = new V2(680f, 430f); e.Caged = true; break; }
      var sbc = typeof(Game).GetMethod("SpawnBullet", BindingFlags.NonPublic | BindingFlags.Instance);
      for (int k = 0; k < 2; k++) sbc.Invoke(host.Game, new object[] { Defs.MinionShot, me.Pos + new V2(60f + 40f * k, -10f - 20f * k), new V2(650f, -40f), 0f, 0.5f, 8f, 1, false, 0f });
    }
    if (Environment.GetEnvironmentVariable("WEAPONS5") != null) {
      // Five reworked weapons at once: a Fuma Shuriken mid-loop, Chain Light's cone, a Sanguine Orb coating an enemy
      // (and an ally coated in healing blood), Fireballs, and Arc Lightning going off.
      var sb5 = typeof(Game).GetMethod("SpawnBullet", BindingFlags.NonPublic | BindingFlags.Instance);
      var gm = host.Game; gm.Bot = false;
      me.Pos = me.Disp = new V2(260f, 300f);
      var fuma = (Bullet)sb5.Invoke(gm, new object[] { Defs.FumaStar, new V2(470f, 230f), new V2(300f, 300f), 1f, 1.1f, 30f, 1, false, 0f });
      var en = new System.Collections.Generic.List<Enemy>(gm.Enemies.Values);
      V2[] at = { new V2(820f, 260f), new V2(880f, 330f), new V2(760f, 640f), new V2(1150f, 600f), new V2(1260f, 640f) };
      for (int k = 0; k < en.Count && k < at.Length; k++) en[k].Pos = en[k].Disp = at[k];
      var light = Defs.BulletColor(Defs.HolyShot);
      V2 caster = new V2(620f, 300f);
      gm.Effects.Add(new Effect { Kind = FxKind.LightCone, Pos = caster, Pos2 = caster + new V2(320f, 0f), Color = light, Radius = Game.LightConeHalf, Dur = 0.3f, T = 0.06f });
      for (int k = 0; k < 2 && k < en.Count; k++) gm.Effects.Add(new Effect { Kind = FxKind.Bolt, Pos = caster + new V2(15f, 0f), Pos2 = at[k], Color = light, Dur = 0.2f, T = 0.03f });
      if (en.Count > 2) gm.Effects.Add(new Effect { Kind = FxKind.BloodCoat, Pos = at[2], Pos2 = new V2(en[2].Id, 0f), Color = Defs.BulletColor(Defs.SanguineShot), Radius = en[2].Radius, Dur = Game.CoatTime, T = Game.CoatTime * 0.45f });
      me.CoatT = 3f;
      for (int k = 0; k < 2; k++) sb5.Invoke(gm, new object[] { Defs.FireballShot, new V2(300f + 120f * k, 560f + 30f * k), new V2(430f, 0f), 0f, 1.4f, 22f, 1, false, 0f });
      V2 arcFrom = new V2(960f, 760f);
      gm.Effects.Add(new Effect { Kind = FxKind.ArcField, Pos = arcFrom, Pos2 = arcFrom + new V2(500f, -160f), Color = Defs.BulletColor(Defs.BoltShot), Radius = Game.ArcHalfWidth, Dur = 0.35f, T = 0.05f });
    }
    if (Environment.GetEnvironmentVariable("HEMO") != null) {
      // Blood Bolts: enemies with 1 to 4 bolts stuck in them (4 is one short of the burst).
      var hm = new System.Collections.Generic.List<Enemy>(host.Game.Enemies.Values);
      for (int k = 0; k < hm.Count && k < 4; k++) { hm[k].Pos = hm[k].Disp = new V2(560f + 130f * k, 450f); hm[k].Hemorrhage = k + 1; }
    }
    if (Environment.GetEnvironmentVariable("HOOK") != null) {
      // The Kusarigama: hooked in an enemy (the chain taut to it), and a throw flying out.
      me.Kit = Kit.Ninja; me.Weapon = Defs.Weapon(Role.Tank, Kit.Ninja, 0); host.Game.Bot = false;
      me.Pos = me.Disp = new V2(500f, 450f); me.Aim = 0f;
      var hk = new System.Collections.Generic.List<Enemy>(host.Game.Enemies.Values);
      if (hk.Count > 0) { hk[0].Pos = hk[0].Disp = new V2(720f, 420f); me.HookId = hk[0].Id; me.HookT = 2f; me.Aim = (hk[0].Pos - me.Pos).Angle; }
      host.Game.Effects.Add(new Effect { Kind = FxKind.ChainThrow, Pos = new V2(500f, 600f), Pos2 = new V2(780f, 600f), Color = new Rgba(0.75f, 0.78f, 0.85f), Radius = 0f, Dur = 0.25f, T = 0.06f });
    }
    if (Environment.GetEnvironmentVariable("ARROWS") != null) {
      var sb = typeof(Game).GetMethod("SpawnBullet", BindingFlags.NonPublic | BindingFlags.Instance);
      for (int k = 0; k < 4; k++) { float a = -0.6f + k * 0.45f; sb.Invoke(host.Game, new object[] { Defs.ArrowShot, me.Pos + V2.FromAngle(a) * (60f + k * 30f), V2.FromAngle(a) * 900f, 0f, 1f, 40f, 1, false, 0f }); }
    }
    if (Environment.GetEnvironmentVariable("REPULSE") != null) {
      // The Repulsion Cannon's blast at REPULSE=<0..1> of the way through, with tiny orbs knocked loose.
      float rf = float.Parse(Environment.GetEnvironmentVariable("REPULSE"));
      me.Weapon = Defs.Weapon(Role.Healer, Kit.Soldier, 0); host.Game.Bot = false; me.Pos = me.Disp = new V2(600f, 450f); me.Aim = 0f;
      host.Game.Effects.Clear();
      host.Game.Effects.Add(new Effect { Kind = FxKind.Repulse, Pos = me.Pos, Pos2 = me.Pos + new V2(258f, 0f), Color = new Rgba(0.55f, 0.95f, 1f), Radius = 0.61f, T = rf * 0.35f, Dur = 0.35f });
      for (int k = 0; k < 3; k++) host.Game.Orbs[900 + k] = new Orb { Id = 900 + k, Tiny = true, Pos = new V2(820f + k * 30f, 420f + k * 25f), Disp = new V2(820f + k * 30f, 420f + k * 25f), Life = 10f };
      host.Game.Orbs[910] = new Orb { Id = 910, Pos = new V2(900f, 520f), Disp = new V2(900f, 520f), Life = 10f };
    }
    if (Environment.GetEnvironmentVariable("THRUST") != null) {
      float tt = float.Parse(Environment.GetEnvironmentVariable("THRUST"));
      host.Game.Effects.Clear();
      host.Game.Effects.Add(new Effect { Kind = FxKind.Jab, Pos = me.Pos, Pos2 = me.Pos + V2.FromAngle(me.Aim) * 150f, Color = Defs.BulletColor(Defs.KnightSlash), Radius = Game.LanceHalfWidth, T = tt * 0.26f, Dur = 0.26f });
    }
    if (Environment.GetEnvironmentVariable("BG") != null) typeof(ArenaApp).GetField("menuBg", f2).SetValue(app, int.Parse(Environment.GetEnvironmentVariable("BG")));
    if (Environment.GetEnvironmentVariable("PATCHES") != null) {
      var gp = host.Game; var mp = gp.Players[1].Pos;
      gp.Patches[1] = new PathPatch { Id = 1, Kind = PatchKind.Fire, Pos = mp + new V2(-260f, -120f), Warn = 1.4f, Life = 9f, R0 = 55f, R1 = 115f, Age = 0.8f };
      gp.Patches[2] = new PathPatch { Id = 2, Kind = PatchKind.Fire, Pos = mp + new V2(220f, 80f), Warn = 1.4f, Life = 9f, R0 = 55f, R1 = 115f, Age = 4f };
      gp.Patches[3] = new PathPatch { Id = 3, Kind = PatchKind.Heal, Pos = mp + new V2(-120f, 200f), Warn = 0.6f, Life = 10f, R0 = 80f, R1 = 80f, Age = 3f };
    }
    // MODS=<RunMod flags>: run modifiers on (Tiny Arena's walls and Nightfall's dark shown settled).
    if (Environment.GetEnvironmentVariable("MODS") != null) {
      var gm = host.Game; gm.Mods = (RunMod)int.Parse(Environment.GetEnvironmentVariable("MODS"));
      if (gm.Has(RunMod.Nightfall)) gm.NightF = 1f;
      if (gm.Has(RunMod.TinyArena)) {
        gm.TinyInset = (V2)typeof(Game).GetMethod("TinyTarget", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(gm, null);
        foreach (var pl in gm.Players.Values) pl.Disp = pl.Pos = pl.Pos.Clamp(gm.ArenaMin + new V2(30f, 30f), gm.ArenaMax - new V2(30f, 30f));
        foreach (var en in gm.Enemies.Values) en.Disp = en.Pos = en.Pos.Clamp(gm.ArenaMin + new V2(30f, 30f), gm.ArenaMax - new V2(30f, 30f));
      }
    }
    if (Environment.GetEnvironmentVariable("PATHON") != null) { host.Game.CurrentPath = (PathId)int.Parse(Environment.GetEnvironmentVariable("PATHON")); host.Game.PathFrom = host.Game.Wave - 1; host.Game.PathTo = host.Game.Wave + 2; host.Game.Mods = RunMod.Frenzy; if (host.Game.CurrentPath == PathId.Fogbound) host.Game.FogF = 1f; if (host.Game.CurrentPath == PathId.NarrowPass) { host.Game.NarrowF = 1f; foreach (var pl in host.Game.Players.Values) pl.Disp = pl.Pos = pl.Pos.Clamp(host.Game.ArenaMin + new V2(30f, 30f), host.Game.ArenaMax - new V2(30f, 30f)); foreach (var en in host.Game.Enemies.Values) en.Disp = en.Pos = en.Pos.Clamp(host.Game.ArenaMin + new V2(30f, 30f), host.Game.ArenaMax - new V2(30f, 30f)); } }
    typeof(ArenaApp).GetMethod("DrawWorld", f2).Invoke(app, null);
    typeof(ArenaApp).GetMethod("InitStyles", f2).Invoke(app, null);
    Ui.Init();
    Event.current = new Event { mousePosition = new Vector2 { x = -100, y = -100 } };
    string mode = args.Length > 4 ? args[4] : "hud";
    if (Environment.GetEnvironmentVariable("MASTERY") != null) {
      Func<string, int> U0 = n => Array.FindIndex(Upgrades.All, u => u.Name == n);
      host.Game.GrantUpgrade(1, U0("Shockwave")); host.Game.GrantUpgrade(1, U0("Blink"));
      float[] xs = { 900f, 420f, 130f, 0f, 20f };
      for (int k = 0; k < 5; k++) if (me.Slots[k].Id != AbilityId.None) me.MasteryXp[me.Slots[k].Id] = xs[k];
      host.Game.MasteryToasts.Add(new MasteryToast { Ability = me.Slots[1].Id, Level = 3, T = 2f });
      if (Environment.GetEnvironmentVariable("HOVER") != null) Event.current.mousePosition = new Vector2 { x = 500, y = 400 };
    }
    if (Environment.GetEnvironmentVariable("PATHOFFER") != null) { host.Game.PathOffer = Environment.GetEnvironmentVariable("PATHOFFER") == "bazaar" ? new[] { PathId.Bazaar, PathId.EliteGauntlet } : Environment.GetEnvironmentVariable("PATHOFFER") == "4" ? new[] { PathId.Fogbound, PathId.NarrowPass } : Environment.GetEnvironmentVariable("PATHOFFER") == "3" ? new[] { PathId.FrozenLake, PathId.BloodMoon } : Environment.GetEnvironmentVariable("PATHOFFER") == "2" ? new[] { PathId.BurningGrounds, PathId.Sanctuary } : new[] { PathId.SwarmFields, PathId.EliteGauntlet }; host.Game.State = MatchState.Intermission; host.Game.Route.Add(PathId.SniperRidge); }
    if (Environment.GetEnvironmentVariable("PATHOFFER") != null || Environment.GetEnvironmentVariable("PATHON") != null) { Event.current.type = EventType.Repaint; set("glMaterial", new Material(null)); GL.ToUi = true; }
    if (Environment.GetEnvironmentVariable("HUDT") != null) { float ht = float.Parse(Environment.GetEnvironmentVariable("HUDT")); set("hudT", ht); set("hudShown", ht > 0.5f); }
    if (mode == "hud")
      typeof(ArenaApp).GetMethod("DrawHud", f2).Invoke(app, null);
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
      host.Lobby[1] = new LobbyEntry { Id = 1, Name = "Anthony", Role = Role.Tank, Kit = Kit.Knight, Weapon = 1, Ability = 2, Ready = true };
      host.Lobby[2] = new LobbyEntry { Id = 2, Name = "Aleon", Role = Role.Dps, Kit = Kit.Mage, Weapon = 0, Ability = 1 };
      if (Environment.GetEnvironmentVariable("SOLO") != null) host.Lobby.Remove(2);
      // RESUME=1: the lobby continuing a saved run (the host in its seat, Aleon's seat still free; SavesUi.cs).
      if (Environment.GetEnvironmentVariable("RESUME") != null) {
        PreviewSaves(); host.Resume = SaveStore.List()[0];
        host.Lobby.Remove(2); host.Lobby[1].Seat = 0; host.Lobby[1].Role = Role.Tank; host.Lobby[1].Kit = Kit.Knight; host.Lobby[1].Weapon = 1; host.Lobby[1].Ability = 2; host.Lobby[1].Ready = false;
      }
      if (Environment.GetEnvironmentVariable("LOBBYTAB") != null) { set("lobbyTab", int.Parse(Environment.GetEnvironmentVariable("LOBBYTAB"))); host.Modifiers = RunMod.GlassCannon | RunMod.Frenzy | RunMod.Lucky; }
      if (Environment.GetEnvironmentVariable("BGMIN") != null) { set("bgPickerMin", true); set("bgTabSeen", 0f); }
      if (Environment.GetEnvironmentVariable("CLIENT") != null) host.MyId = 2;
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
      typeof(ArenaApp).GetMethod("DrawLobby", f2).Invoke(app, null);
      if (te != null) endTe.Invoke(app, null);
      GL.ToUi = false;
    }
    else if (mode == "menu" || mode == "howto" || mode == "continue" || mode == "records")
    {
      // continue / records: the saved-runs list and the Records page, with made-up saves and a profile (SavesUi.cs).
      if (mode == "continue" || mode == "records" || mode == "menu") PreviewSaves();
      if (mode == "continue") { typeof(ArenaApp).GetMethod("OpenContinue", f2).Invoke(app, null); if (Environment.GetEnvironmentVariable("NOSAVES") != null) set("savedRuns", new System.Collections.Generic.List<RunSave>()); }
      GL.Tris.Clear();
      set("drawScale", 1f);
      typeof(ArenaApp).GetMethod("DrawMenuBackground", f2).Invoke(app, new object[] { 7.3f });
      var hv = Environment.GetEnvironmentVariable("MOUSE");
      if (hv != null) { var xy = hv.Split(','); Event.current.mousePosition = new Vector2(float.Parse(xy[0]), float.Parse(xy[1])); }
      Event.current.type = EventType.Repaint;
      set("glMaterial", new Material(null));
      GL.ToUi = true;
      typeof(ArenaApp).GetMethod(mode == "menu" ? "DrawMenu" : mode == "continue" ? "DrawContinue" : mode == "records" ? "DrawRecords" : "DrawHowToPlay", f2).Invoke(app, null);
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
      lab.Wave = 20; lab.PlayerCount = 3; lab.Seats[1].Kit = Kit.Mage; lab.Seats[2].Kit = Kit.Knight; lab.Seats[2].Weapon = 1;
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
    else if (mode == "tab")
    {
      var rng = new System.Random(4);
      Func<string, int> U = n => Array.FindIndex(Upgrades.All, u => u.Name == n);
      foreach (var n in new[] { "Hollow Points", "Hollow Points", "Sharpened Rounds", "Quick Trigger", "Hair Trigger", "Fortitude", "Vitality", "Vitality", "Regeneration", "Focus", "Clarity", "Thick Skin", "Second Charge", "Multishot", "Multishot", "Piercing Rounds", "Overcharge", "Shockwave", "Blink", "Nova", "Warden's Guard", "Stun Gun", "Swarm Rounds", "Twin Fang", "Collapse Rounds", "Homing Rounds", "Homing Rounds", "Incendiary Rounds", "Shock Rounds", "Velocity Rounds", "Light Boots", "Sprinter", "Mending", "Executioner", "Royal Jelly" })
        host.Game.GrantUpgrade(1, U(n));
      if (Environment.GetEnvironmentVariable("MASTERY") != null) { float[] xs = { 900f, 420f, 130f, 0f, 20f }; for (int k = 0; k < 5; k++) if (me.Slots[k].Id != AbilityId.None) me.MasteryXp[me.Slots[k].Id] = xs[k]; }
      me.Level = 31; me.Slots[2].Charges = 0; me.Slots[2].Timer = 6.3f;
      typeof(ArenaApp).GetMethod("DrawTabMenu", f2).Invoke(app, new object[] { me });
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
      if (mode == "picker" && Environment.GetEnvironmentVariable("KEYS") != null) { host.Game.GrantUpgrade(1, U("Overdrive")); me.Offer = new[] { U("Shockwave"), U("Blink"), U("Nova") }; }
      else if (mode == "picker" && Environment.GetEnvironmentVariable("PICKCARDS") != null) me.Offer = Array.ConvertAll(Environment.GetEnvironmentVariable("PICKCARDS").Split(','), n => U(n));
      else if (mode == "picker") me.Offer = Environment.GetEnvironmentVariable("REVOLVER") != null ? new[] { U("Multishot"), U("Hollow Points"), U("Barrage") } : Environment.GetEnvironmentVariable("PARRYCARDS") != null ? new[] { U("Reinforced Guard"), U("Counterstance"), U("Wide Guard") } : new[] { U("Hollow Points"), U("Multishot"), U("Blink") };
      else if (mode == "legendary") { me.Offer = new[] { U("Warden's Guard"), U("Stun Gun"), U("Searchlight") }; me.OfferLegendary = true; }
      else { me.Offer = Upgrades.ModOffer(me.Role, me.RoleAbility, me.Taken); me.OfferMod = true; }
      me.PendingPicks = 2;
      typeof(ArenaApp).GetField("selectedCard", f2).SetValue(app, 1);
      typeof(ArenaApp).GetField("shownOffer", f2).SetValue(app, string.Join(",", Array.ConvertAll(me.Offer, x => x.ToString())) + "/" + me.Taken.Count);
      typeof(ArenaApp).GetMethod("DrawUpgradePicker", f2).Invoke(app, new object[] { me });
    }
    using (var w = new BinaryWriter(File.Create(args[0] + ".tris"))) foreach (var v in GL.Tris) w.Write(v);
    File.WriteAllLines(args[0] + ".ui", GUI.Log.ToArray());
    Console.WriteLine("state {0} wave {1}, tris {2}, ui items {3}", host.Game.State, host.Game.Wave, GL.Tris.Count / 18, GUI.Log.Count);
    host.Leave(""); foreach (var c in others) c.Leave("");
  }
}
