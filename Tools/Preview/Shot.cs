using System; using System.IO; using System.Reflection; using System.Threading; using Arena; using UnityEngine;
class Shot {
  static void Step(Session s) { s.Poll(); s.Game.FixedStep(1f/60f, new PlayerInput()); s.Game.Update(1f/60f); }
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
      if (Environment.GetEnvironmentVariable("SINGT") != null && f == (int)(seconds * 60) - 120) foreach (var e in host.Game.Enemies.Values) if (e.Type == EnemyType.Singularity) e.T = float.Parse(Environment.GetEnvironmentVariable("SINGT"));
      // WARDEN=<health fraction>: hold the Warden at that health, so the shot lands in that stage.
      if (Environment.GetEnvironmentVariable("WARDEN") != null) foreach (var e in host.Game.Enemies.Values) if (e.Type == EnemyType.Boss) e.Hp = (e.MaxHp * float.Parse(Environment.GetEnvironmentVariable("WARDEN")));
      // REAPERS=<health fraction>: the same for the Twin Reapers (or the Dual Reaper).
      if (Environment.GetEnvironmentVariable("REAPERS") != null) foreach (var e in host.Game.Enemies.Values) if (Game.IsReaper(e.Type) && !host.Game.IsSpawning(e)) e.Hp = e.MaxHp * float.Parse(Environment.GetEnvironmentVariable("REAPERS"));
      // QUEEN=<health fraction>: the same for the Hive Queen.
      if (Environment.GetEnvironmentVariable("QUEEN") != null) foreach (var e in host.Game.Enemies.Values) if (e.Type == EnemyType.HiveQueen) e.Hp = (e.MaxHp * float.Parse(Environment.GetEnvironmentVariable("QUEEN")));
    }
    if (host.Game.Reapers != null) { var R = host.Game.Reapers; Console.WriteLine("reapers stage {0} turn {1} turnT {2:0.0} trans {3} {4:0.0} suck {5:0.0} trapped {6} holes {7}", R.Stage, R.Turn, R.TurnT, R.Trans, R.TransT, R.Suck, R.Trapped, R.Holes.Count); }
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
      var sb2 = typeof(Game).GetMethod("SpawnBullet", BindingFlags.NonPublic | BindingFlags.Instance);
      for (int k = 0; k < 3; k++) { float a = -0.5f + k * 0.5f; sb2.Invoke(host.Game, new object[] { Defs.FrostLance, me.Pos + V2.FromAngle(a) * (90f + k * 60f), V2.FromAngle(a) * 700f, 0f, 1f, 14f, 1, false, 0f }); }
      sb2.Invoke(host.Game, new object[] { Defs.FrostShot, me.Pos + new V2(-120f, 60f), new V2(-500f, 100f), 0f, 1f, 5f, 1, false, 0f });
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
    else if (mode == "menu" || mode == "howto")
    {
      GL.Tris.Clear();
      set("drawScale", 1f);
      typeof(ArenaApp).GetMethod("DrawMenuBackground", f2).Invoke(app, new object[] { 7.3f });
      var hv = Environment.GetEnvironmentVariable("MOUSE");
      if (hv != null) { var xy = hv.Split(','); Event.current.mousePosition = new Vector2(float.Parse(xy[0]), float.Parse(xy[1])); }
      Event.current.type = EventType.Repaint;
      set("glMaterial", new Material(null));
      GL.ToUi = true;
      typeof(ArenaApp).GetMethod(mode == "menu" ? "DrawMenu" : "DrawHowToPlay", f2).Invoke(app, null);
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
