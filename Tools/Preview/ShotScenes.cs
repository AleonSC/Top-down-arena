using System; using System.IO; using System.Reflection; using System.Threading; using Arena; using UnityEngine;
// Shot.cs scene setup from environment flags: PlayerScenes poses the player and their weapon (POSE, BOW, VEIN,
// FLAME, the boss legendaries, CAST, ...) before the ArenaApp exists; ArenaScenes poses the arena with it (AIMLINE,
// ROOM, APPROACH, PATCHES, MODS, PATHON, ...).
static partial class Shot {
  // BREACHROOMS=1: a small free Stash room behind every breach (mockup, Docs/BreachRooms.md)
  static void MockBreachRooms(Game xg) {
    string brs = Environment.GetEnvironmentVariable("BREACHROOMS");
    if (brs == null) return;
    var shp = SurpriseShape.Stash;
    var bfl2 = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
    var bs2 = xg.ActiveBreaches; var made = new System.Collections.Generic.List<ExpansionRoom>(); int ok = 0;
    foreach (var b in bs2) {
      var opts = Expansion.GridSurpriseOptions(xg.Expanded, shp, b.Pos - b.In * BreachEvent.Behind);
      if (opts.Count == 0) continue;
      xg.Expanded.AddRange(opts[0].Rooms); made.AddRange(opts[0].Rooms); ok++;
    }
    typeof(Game).GetMethod("PinBreaches", bfl2).Invoke(xg, new object[] { made });
    typeof(Game).GetField("expWorld", bfl2).SetValue(xg, null);
    if (made.Count > 0) {  // STASHAGE=<s>: the build-in that old; you stand in the first Stash
      xg.SurpriseKind = made[0].Surprise; xg.SurpriseWave = xg.Wave;
      xg.SurpriseAge = float.Parse(Environment.GetEnvironmentVariable("STASHAGE") ?? "0.5", System.Globalization.CultureInfo.InvariantCulture);
      if (Environment.GetEnvironmentVariable("STASHAT") != null) { int pi = 0; foreach (var pl in xg.Players.Values) { pl.Pos = pl.Disp = new V2(made[0].X + 90f + 50f * pi, (made[0].Y + made[0].Y1) / 2f); pi++; } }
    }
    Console.Error.WriteLine("breach rooms: " + ok + " of " + bs2.Length + " on=" + xg.BreachesOn + " exp=" + xg.ExpandingOn + " rooms=" + xg.RoomsOn + " boss=" + xg.BossNow + " st=" + xg.State + " w=" + xg.Wave);
  }

  // VECGAIN=<s since>: vectors just came in (Game/VectorGainLook.cs): "+n VECTORS" over you, shards into the chip, the chip lit and
  // counting up; VECGAINN=<n> (default 10). With BREACHROOMS=1 STASHAT=1 the first Stash's cache is taken (its burst), else a pickup's.
  static void VectorGainScene() {
    string vg = Environment.GetEnvironmentVariable("VECGAIN");
    if (vg == null) return;
    var ci = System.Globalization.CultureInfo.InvariantCulture;
    float age = float.Parse(vg, ci);
    int n = Environment.GetEnvironmentVariable("VECGAINN") != null ? int.Parse(Environment.GetEnvironmentVariable("VECGAINN")) : 10;
    var bf = BindingFlags.NonPublic | BindingFlags.Instance;
    Action<string, object> put = (f, v) => typeof(ArenaApp).GetField(f, bf).SetValue(app, v);
    var xg = host.Game;
    if (me.Vectors < n) me.Vectors = n + 30;
    put("vecGainAge", age); put("vecGainN", n); put("vecGainFrom", me.Disp); put("vecSeen", me.Vectors);
    put("vecShown", Math.Min((float)me.Vectors, me.Vectors - n + Math.Max(0f, age - 0.35f) * 30f));
    V2 at = me.Disp + new V2(110f, -40f); bool stash = false;
    byte sk = (byte)(1 + (int)SurpriseShape.Stash);
    int si = xg.Expanded.FindIndex(r => r.Surprise == sk);
    if (si >= 0) { var r = xg.Expanded[si]; r.Claimed = true; xg.Expanded[si] = r; at = new V2((r.X + r.X1) / 2f, (r.Y + r.Y1) / 2f); stash = true; }
    var bt = typeof(ArenaApp).GetNestedType("VecBurst", BindingFlags.NonPublic);
    var b = Activator.CreateInstance(bt);
    bt.GetField("At").SetValue(b, at); bt.GetField("To").SetValue(b, me.Id); bt.GetField("Age").SetValue(b, age);
    bt.GetField("N").SetValue(b, n); bt.GetField("Stash").SetValue(b, stash);
    var list = typeof(ArenaApp).GetField("vecBursts", bf).GetValue(app);
    list.GetType().GetMethod("Add").Invoke(list, new[] { b });
  }

  static void PlayerScenes() {
    BloodWallScene();  // BLOODWALL=<s> BWCLOCK=<s> (ShotBloodWall.cs)
    CoverScene();  // COVER=<s into the scene> [COVERWEAR=1] [COVERBREAK=<s>] (ShotCover.cs)
    ClaymoreScene();  // CLAYMORE=<s into the scene> [CLAYTRIP=<s>] [CLAYCAP=1] (ShotClaymore.cs)
    DecoyScene();  // DECOY=<s into the scene> [DECOYWEAR=1] [DECOYPOP=<s>] (ShotDecoy.cs)
    GrazeScene();  // GRAZE=<s into the scene> (ShotGraze.cs)
    SanctumGlassScene();  // SANCTUM=<s into the scene> [SANCTUMCAP=1] (ShotSanctumGlass.cs)
    GaleWallScene();  // GALEWALL=<s into the scene> [GALEBLAST=<s>] [GALECAP=1] (ShotGaleWall.cs)
    WaterWardScene();  // WWARD=<s into the scene> [WWENEMY=<s>] [WWSHOOT=1] (ShotWaterWard.cs)
    GustGateScene();  // GUSTGATE=<s into the scene> (ShotGustGate.cs)
    CauterizeScene();  // CAUTER=1 (ShotCauterize.cs)
    ChestLooksScene();  // CHESTLOOKS=1 (ShotChests.cs)
    GadgetsScene();  // GADGETS=wheel|floor|recall (ShotGadgets.cs)
    PoisonDartScene();  // DART=<s into the scene> [DARTCAP=1] [FXOFF=1] (ShotPoisonDart.cs)
    PhalanxScene();  // PHALANX=<s since the cast> [PHALANXCAP=1] [FXOFF=1] (ShotPhalanx.cs)
    WolfPackScene();  // WOLFPACK=pack|alpha|merge [WOLFT=<s>] (ShotWolfPack.cs)
    BoneGuardScene();  // BONEGUARD=<s into the scene> [FXOFF=1] (ShotBoneGuard.cs)
    CarrierScene();  // CARRIER=1 (ShotCarrier.cs)
    KnightDpsScene();  // KNIGHTDPS=steel|gauntlet|dive|bridge KDT=<s> (ShotKnightDps.cs)
    if (Environment.GetEnvironmentVariable("POSE") != null) { host.Game.Bot = false; me.Pos = me.Disp = new V2(800f, 450f); me.Aim = -0.4f; }
    if (Environment.GetEnvironmentVariable("BOW") != null) { me.Weapon = Defs.Weapon(Role.Healer, Kit.Soldier, 2); host.Game.Bot = false; me.Pos = me.Disp = new V2(800f, 450f); me.Aim = -0.4f; me.DrawT = float.Parse(Environment.GetEnvironmentVariable("BOW")); }
    if (Environment.GetEnvironmentVariable("TREANT") != null) {
      // The Treant Rod: its Treant rooted with the root to you, and the root lash TREANT=<seconds> into its life (0.15 = fully out).
      me.Weapon = System.Array.Find(Defs.Weapons, w => w.Id == WeaponId.TreantRod); me.Kit = Kit.Summoner; host.Game.Bot = false;
      me.Pos = me.Disp = new V2(450f, 450f); me.Aim = -0.15f;
      for (int i = 0; i < 150; i++) { me.Pos = me.Disp = new V2(450f, 450f); Step(host); }
      float age = float.Parse(Environment.GetEnvironmentVariable("TREANT"), System.Globalization.CultureInfo.InvariantCulture);
      host.Game.Effects.Add(new Effect { Kind = FxKind.RootLash, Pos = me.Pos, Pos2 = me.Pos + V2.FromAngle(me.Aim) * 250f, Color = new Rgba(0.55f, 0.36f, 0.17f), Radius = Game.RootHalfWidth, Dur = Game.RootFxTime, T = age });
    }
    if (Environment.GetEnvironmentVariable("NIMBUS") != null) {
      // The Nimbus: its cloud over your cursor (200 px out), rain under it, and a strike NIMBUS=<seconds> ago (-1: none).
      me.Weapon = Defs.Weapon(Role.Healer, Kit.Elementalist, 2); me.Kit = Kit.Elementalist; host.Game.Bot = false;
      me.Pos = me.Disp = new V2(450f, 450f);
      V2 cursor = new V2(700f, 360f);
      for (int i = 0; i < 200; i++) { me.Pos = me.Disp = new V2(450f, 450f); host.Poll(); host.Game.FixedStep(1f/60f, new PlayerInput { AimAt = cursor }); host.Game.Update(1f/60f); }
      float age = float.Parse(Environment.GetEnvironmentVariable("NIMBUS"), System.Globalization.CultureInfo.InvariantCulture);
      if (age >= 0f) host.Game.Effects.Add(new Effect { Kind = FxKind.NimbusStrike, Pos = cursor, Pos2 = cursor, Color = Game.NimbusBoltColor, Radius = Game.NimbusBlast, Dur = Game.NimbusStrikeFxTime, T = age });
      // NIMBUSSTORM=<s>: the Eye of the Storm that long in (a few enemies caught in it, one just bolted).
      if (Environment.GetEnvironmentVariable("NIMBUSSTORM") != null) {
        float st = float.Parse(Environment.GetEnvironmentVariable("NIMBUSSTORM"), System.Globalization.CultureInfo.InvariantCulture);
        Minion cloud = null; foreach (var mm in host.Game.Minions.Values) if (mm.Kind == MinionKind.Nimbus) cloud = mm;
        host.Game.Effects.Add(new Effect { Kind = FxKind.NimbusStorm, Pos = cloud.Pos, Pos2 = new V2(cloud.Id, me.Id), Color = Game.NimbusBoltColor, Radius = Game.NimbusStormRadius, Dur = Game.NimbusStormTime, T = st });
        if (Environment.GetEnvironmentVariable("NIMBUSBOLT") != null)
          host.Game.Effects.Add(new Effect { Kind = FxKind.Bolt, Pos = cloud.Pos + new V2(-30f, -40f), Pos2 = cloud.Pos + new V2(70f, 10f), Color = Game.NimbusBoltColor, Dur = 0.2f, T = 0.02f });
      }
    }
    if (Environment.GetEnvironmentVariable("CHALK") != null) {
      // Spirit Chalk: a loop drawn round some enemies (they're held where they stand), and a line trailing off it.
      me.Weapon = Defs.Weapon(Role.Healer, Kit.Summoner, 2); me.Kit = Kit.Summoner; host.Game.Bot = false;
      V2 ctr = new V2(700f, 450f);
      host.Game.DevSetGod(1, true);
      var spawn = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      var held = new System.Collections.Generic.List<Enemy>();
      foreach (var at in new[] { ctr + new V2(-20f, -15f), ctr + new V2(28f, 20f), ctr + new V2(-5f, 38f) })
        held.Add((Enemy)spawn.Invoke(host.Game, new object[] { EnemyType.Grunt, at, 0f, 0, false }));
      for (int i = 0; i < 200; i++)
      {
        me.Pos = me.Disp = new V2(450f, 450f); me.InvulnT = 1f; me.Hp = me.MaxHp;
        foreach (var e in held) { e.Hp = e.MaxHp = 1e5f; e.SpawnT = 0f; e.FireT = 99f; if (!host.Game.Ink.Exists(d => d.Pen != 0)) e.Pos = e.Disp = e.Pos; }
        float a = Math.Min(i * 2.9f / 60f, MathX.Tau + 0.5f);
        V2 aim = i < 160 ? ctr + V2.FromAngle(a) * 84f : ctr + new V2(84f + (i - 160) * 2.2f, 84f);
        host.Poll(); host.Game.FixedStep(1f/60f, new PlayerInput { Fire = true, AimAt = aim }); host.Game.Update(1f/60f);
      }
    }
    if (Environment.GetEnvironmentVariable("FIREFLY") != null) {
      // The Sprite Staff's fireflies in flight: one just left your staff, one is hopping between enemies.
      me.Weapon = System.Array.Find(Defs.Weapons, w => w.Id == WeaponId.SpriteStaff); me.Kit = Kit.Summoner; host.Game.Bot = false;
      me.Pos = me.Disp = new V2(450f, 450f); me.Aim = -0.15f;
      var shoot = typeof(Game).GetMethod("SpawnBullet", BindingFlags.NonPublic | BindingFlags.Instance);
      shoot.Invoke(host.Game, new object[] { Defs.FireflyShot, me.Pos + V2.FromAngle(me.Aim) * 20f, V2.FromAngle(me.Aim) * 540f, 0f, 1f, 7f, 1, false, 0f });
      shoot.Invoke(host.Game, new object[] { Defs.FireflyShot, me.Pos + new V2(260f, -150f), V2.FromAngle(0.6f) * 860f, 0f, 1f, 7f, 1, false, 0f });
      for (int i = 0; i < 6; i++) Step(host);
    }
    if (Environment.GetEnvironmentVariable("VEIN") != null) {
      // Vein Tether: one beam on an enemy (VEIN=1), or on nothing (VEIN=0).
      me.Weapon = System.Array.Find(Defs.Weapons, w => w.Id == WeaponId.VeinTether); me.Kit = Kit.Hemomancer; host.Game.Bot = false;
      me.Pos = me.Disp = new V2(700f, 450f); me.Aim = -0.35f; me.FiringSeen = true; me.Firing = true;
      if (Environment.GetEnvironmentVariable("VEIN") == "1") foreach (var e in host.Game.Enemies.Values) { e.Pos = e.Disp = me.Pos + V2.FromAngle(me.Aim) * 230f; break; }
    }
    if (Environment.GetEnvironmentVariable("LEECH") != null) {
      // Leech Tendrils: LEECH=<n> enemies held in the arc (0 = free tendrils feeling about).
      me.Weapon = System.Array.Find(Defs.Weapons, w => w.Id == WeaponId.LeechTendrils); me.Kit = Kit.Hemomancer; host.Game.Bot = false;
      me.Pos = me.Disp = new V2(700f, 450f); me.Aim = -0.35f; me.FiringSeen = true; me.Firing = true; me.Grasp.Clear();
      int held = int.Parse(Environment.GetEnvironmentVariable("LEECH")), k = 0;
      foreach (var e in host.Game.Enemies.Values) {
        if (k < held) { e.Pos = e.Disp = me.Pos + V2.FromAngle(me.Aim + (k - (held - 1) / 2f) * 0.42f) * (120f + 25f * (k % 2)); me.Grasp.Add(e.Id); me.GraspCharge.Add(float.Parse(Environment.GetEnvironmentVariable("CHARGE") ?? "0.3") * (k + 1) / held); }
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
      // Soul Lantern cast alone: the lantern beside you, linked, SOULGHOST=<seconds> in.
      host.Game.Bot = false; me.Pos = me.Disp = new V2(700f, 450f); me.Aim = -0.4f; me.AimAt = me.Pos + V2.FromAngle(-0.4f) * 300f;
      me.Slots[0].Id = AbilityId.SoulLantern; me.Slots[0].Charges = 1;
      typeof(Game).GetMethod("HostAbility", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(host.Game, new object[] { 1, 0, me.Pos, false });
      for (int f = 0; f < 60 * float.Parse(Environment.GetEnvironmentVariable("SOULGHOST")); f++) { me.Pos = new V2(700f, 450f); host.Poll(); host.Game.FixedStep(1f / 60f, new PlayerInput { AimAt = me.AimAt }); host.Game.Update(1f / 60f); }
    }
    if (Environment.GetEnvironmentVariable("FLAME") != null) {
      // The Flamethrower held on a few enemies for FLAME=<seconds>: the stream and the burning. FLAMEGUN=water|gale: its variants
      // instead, the Water Gun or the Gale Gun (ElementGunLook.cs).
      var fg = Environment.GetEnvironmentVariable("FLAMEGUN");
      me.Weapon = Defs.WeaponOf(fg == "water" ? WeaponId.WaterGun : fg == "gale" ? WeaponId.GaleGun : WeaponId.Flamethrower); host.Game.Bot = false;
      me.Pos = me.Disp = new V2(700f, 450f);
      if (fg != null) {  // a clean stage: three grunts in the stream, nothing else
        host.Game.Enemies.Clear(); host.Game.Zones.Clear(); me.AegisT = 0f;
        var fsp = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
        for (int q = 0; q < 3; q++) { var fe = (Enemy)fsp.Invoke(host.Game, new object[] { EnemyType.Grunt, me.Pos + new V2(90f + 45f * q, -40f + 40f * q), 0f, 0, false }); fe.SpawnT = 0f; fe.Spawning = false; }
      }
      int k = 0;
      foreach (var e in host.Game.Enemies.Values) { if (k < 3) { e.Pos = e.Disp = me.Pos + new V2(90f + 45f * k, -40f + 40f * k); e.Hp = e.MaxHp = 1e5f; } else e.Pos = e.Disp = new V2(100f + 20f * k, 120f); k++; }
      V2 at = me.Pos + new V2(200f, 0f);
      for (int f = 0; f < 60 * float.Parse(Environment.GetEnvironmentVariable("FLAME")); f++) {
        int j = 0; foreach (var e in host.Game.Enemies.Values) { if (j < 3) { e.Pos = me.Pos + new V2(90f + 45f * j, -40f + 40f * j); e.FireT = 9f; } j++; }
        me.Pos = new V2(700f, 450f);
        host.Poll(); host.Game.FixedStep(1f / 60f, new PlayerInput { Fire = true, AimAt = at }); host.Game.Update(1f / 60f);
      }
      foreach (var e in host.Game.Enemies.Values) Console.WriteLine("flame: {0} burning {1} wet {2} bleeding {3} slowed {4} hp lost {5:0}", e.Type, e.Burning, e.WetT > 0f, e.Bleeding, e.SlowMult < 1f, e.MaxHp - e.Hp);
    }
    if (Environment.GetEnvironmentVariable("PROPS") != null) {
      // The arena's props: a crate wall (one worn), an iron-bound crate, pots, a lit barrel, a closed chest, an opened one,
      // and your chest item (PROPS=2: a legendary) with you standing on it, so its card shows.
      System.Func<string, int> UL = n => System.Array.FindIndex(Upgrades.All, u => u.Name == n);
      var g = host.Game;
      g.Props.Clear(); g.Loots.Clear();
      foreach (var e in g.Enemies.Values) { e.Pos = e.Disp = new V2(1450f, 150f + 60f * (e.Id % 10)); e.FireT = 9f; }
      g.AddProp(PropKind.Crate, new V2(520f, 300f)); g.AddProp(PropKind.Crate, new V2(566f, 300f)).Hp = 3f;
      g.AddProp(PropKind.HeavyCrate, new V2(520f, 560f));
      g.AddProp(PropKind.Barrel, new V2(1100f, 260f));
      var lit = g.AddProp(PropKind.Barrel, new V2(1160f, 330f)); lit.Fuse = 0.2f;
      g.AddProp(PropKind.Chest, new V2(1150f, 600f));
      g.AddProp(PropKind.HealthBarrel, new V2(1000f, 430f));
      var open = g.AddProp(PropKind.Chest, new V2(820f, 560f)); open.Open = true; open.OpenT = 0.8f;
      foreach (var p in g.Props.Values) p.Age = 3f + p.Id;
      int item = Environment.GetEnvironmentVariable("PROPS") == "2" ? UL("Warden's Guard") : UL("Extra Use");
      if (item < 0) item = 0;
      g.Loots[99] = new Loot { Id = 99, Owner = 1, Upgrade = item, Pos = new V2(820f, 690f), Age = 2.95f };
      string lootKind = Environment.GetEnvironmentVariable("LOOTKIND");  // LOOTKIND=build|card: a Buildables chest's build or a piece chest's card instead of the upgrade
      if (lootKind == "build") { g.Loots[99].Kind = LootKind.Build; g.Loots[99].Item = (int)BuildKind.Turret; }
      else if (lootKind == "card") { g.Loots[99].Kind = LootKind.Card; g.Loots[99].Item = (int)Expansion.MakePieceCard(PieceShape.TriL); }
      g.Bot = false; me.Pos = me.Disp = new V2(822f, 695f); me.Aim = -1.2f;
      for (int i = 0; i < 6; i++) g.Bullets.Remove(i);
      if (Environment.GetEnvironmentVariable("PROPSBREAK") != null) { g.Enemies.Clear(); g.State = MatchState.Intermission; g.StateTimer = 20f; me.Offer = null; }  // PROPSBREAK=1: between waves (the card shows then too)
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
      me.Weapon = System.Array.Find(Defs.Weapons, w => w.Id == WeaponId.Implosion); me.Kit = Kit.Hemomancer; host.Game.Bot = false;
      me.Pos = me.Disp = new V2(700f, 450f); me.Aim = -0.35f;
      int k = 0;
      foreach (var e in host.Game.Enemies.Values) {
        e.Pos = e.Disp = me.Pos + V2.FromAngle(me.Aim + 0.5f * k) * (230f + 40f * k);
        if (k == 0) me.AimAt = e.Disp;
        else if (k < 3) host.Game.Effects.Add(new Effect { Kind = FxKind.Implode, Pos = e.Disp, Color = Defs.BulletColor(Defs.ImplodeShot), Radius = e.Radius + 55f, Dur = 0.35f, T = 0.35f * float.Parse(Environment.GetEnvironmentVariable("IMPLODE")) });
        if (++k == 3) break;
      }
    }
    if (Environment.GetEnvironmentVariable("RIOTERS") != null) {
      // RIOTERS=1: a pack of Rioters (two Ball and Chains) closing on you, a Guard and a Thug behind (mode hud; ZOOM=3 for a close look).
      var gm = host.Game; gm.Bot = false; gm.Enemies.Clear(); gm.Bullets.Clear();
      me.Pos = me.Disp = new V2(1010f, 580f); me.InvulnT = 1f; me.Aim = MathX.Pi;
      var spawn = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      var spots = new[] { new V2(935f, 540f), new V2(950f, 600f), new V2(900f, 575f), new V2(915f, 635f), new V2(880f, 520f), new V2(960f, 505f), new V2(855f, 600f), new V2(860f, 660f) };
      var types = new[] { EnemyType.Rioter, EnemyType.Rioter, EnemyType.Rioter, EnemyType.Rioter, EnemyType.Rioter, EnemyType.Rioter, EnemyType.Guard, EnemyType.Thug };
      foreach (var pl in gm.Players.Values) if (pl != me) pl.Pos = pl.Disp = new V2(300f, 200f);
      for (int i = 0; i < spots.Length; i++) {
        var en = (Enemy)spawn.Invoke(gm, new object[] { types[i], spots[i], 0f, 0, false });
        en.SpawnT = 0f; en.Pos = en.Disp = spots[i]; en.Facing = (me.Pos - spots[i]).Angle + (i % 3 - 1) * 0.25f;
        if (types[i] == EnemyType.Rioter) en.Variant = i == 1 || i == 4 ? EnemyVariant.BallAndChain : EnemyVariant.None;  // two Ball and Chains (EnemyVariants.cs)
      }
    }
    if (Environment.GetEnvironmentVariable("PRISONERS") != null) {
      // PRISONERS=1: Thugs and Shivs (Hooded and Twin Shanks: walking, winding up, lunging) in a row facing you (mode hud; ZOOM=3).
      var gm = host.Game; gm.Bot = false; gm.Enemies.Clear(); gm.Bullets.Clear();
      me.Pos = me.Disp = new V2(1170f, 600f); me.InvulnT = 1f; me.Aim = MathX.Pi;
      foreach (var pl in gm.Players.Values) if (pl != me) pl.Pos = pl.Disp = new V2(300f, 200f);
      var spawn = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      var types = new[] { EnemyType.Thug, EnemyType.Shiv, EnemyType.Shiv, EnemyType.Shiv, EnemyType.Shiv, EnemyType.Shiv, EnemyType.Shiv, EnemyType.Thug };
      var phases = new byte[] { 0, 0, 0, 1, 1, 2, 2, 0 };
      for (int i = 0; i < types.Length; i++) {
        var at = new V2(975f + (i % 4) * 42f, i < 4 ? 565f : 630f);
        var en = (Enemy)spawn.Invoke(gm, new object[] { types[i], at, 0f, 0, false });
        en.SpawnT = 0f; en.Pos = en.Disp = at; en.Facing = (me.Pos - at).Angle; en.Phase = phases[i];
      }
    }
    if (Environment.GetEnvironmentVariable("THUGRAGE") != null) {
      // THUGRAGE=1: four Thugs at full, 2/3, 1/3 and 5% health, left to right: the red halo growing as they enrage
      // (OfficerGlowLook.cs); mode hud, NINJA=0 ZOOM=3.
      var gm = host.Game; gm.Bot = false; gm.Enemies.Clear(); gm.Bullets.Clear();
      me.Pos = me.Disp = new V2(1010f, 690f); me.InvulnT = 1f; me.Aim = -MathX.Pi / 2f;
      foreach (var pl in gm.Players.Values) if (pl != me) pl.Pos = pl.Disp = new V2(300f, 200f);
      var spawn = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      var hp = new[] { 1f, 0.66f, 0.33f, 0.05f };
      for (int i = 0; i < hp.Length; i++) {
        var at = new V2(1010f + (i - 1.5f) * 80f, 600f);
        var en = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Thug, at, 0f, 0, false });
        en.SpawnT = 0f; en.Pos = en.Disp = at; en.Facing = (me.Pos - at).Angle; en.Hp = en.MaxHp * hp[i];
      }
    }
    if (Environment.GetEnvironmentVariable("GUARD") != null) {
      // GUARD=1: the riot Guard (WardenLook.cs DrawGuardBody, GuardSwing.cs) walking, drawing back a forehand, mid-forehand,
      // drawing back a backhand, mid-backhand, left to right, each facing you below; mode hud with KNIGHT=1 ZOOM=2.6.
      var gm = host.Game; gm.Bot = false; gm.Enemies.Clear(); gm.Bullets.Clear();
      me.Pos = me.Disp = new V2(800f, 525f); me.InvulnT = 1f; me.Aim = MathX.Pi / 2f;
      foreach (var pl in gm.Players.Values) if (pl != me) pl.Pos = pl.Disp = new V2(300f, 200f);
      var spawn = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      if (Environment.GetEnvironmentVariable("GUARD") == "2") {
        // GUARD=2: four Guards walking in at 200, 150, 110 and 70 px from you (left to right): the ready tell fading in.
        var dists = new[] { 200f, 150f, 110f, 70f };
        for (int i = 0; i < 4; i++) {
          float ang = MathX.Pi * (0.97f + 0.29f * i);
          var at = me.Pos + V2.FromAngle(ang) * dists[i];
          var en = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Guard, at, 0f, 0, false });
          en.SpawnT = 0f; en.Pos = en.Disp = at; en.Facing = (me.Pos - at).Angle; en.MawSide = (byte)(i % 2);
        }
      } else {
      var stages = new[] { (byte)0, Game.BatonWinding, Game.BatonActing, Game.BatonWindingBack, Game.BatonActingBack };
      var left = new[] { 0f, Game.BatonWind * 0.3f, Game.BatonFlash * 0.45f, Game.BatonWind * 0.3f, Game.BatonFlash * 0.45f };
      for (int i = 0; i < 5; i++) {
        var at = new V2(800f + (i - 2) * 105f, 410f);
        var en = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Guard, at, 0f, 0, false });
        en.SpawnT = 0f; en.Pos = en.Disp = at; en.Facing = MathX.Pi / 2f;
        en.LarderStage = stages[i]; en.LarderT = left[i];
      }
      }
    }
    if (Environment.GetEnvironmentVariable("THUG") != null) {
      // THUG=1: the Thug's charged punch (ThugPunch.cs, PrisonLook.cs DrawThugFloor): walking, early in the wind-up, late in
      // the wind-up, mid-jab, left to right, each facing you above; mode hud with KNIGHT=1 ZOOM=2.6.
      var tgm = host.Game; tgm.Bot = false; tgm.Enemies.Clear(); tgm.Bullets.Clear();
      me.Pos = me.Disp = new V2(800f, 300f); me.InvulnT = 1f; me.Aim = -MathX.Pi / 2f;
      foreach (var pl in tgm.Players.Values) if (pl != me) pl.Pos = pl.Disp = new V2(300f, 200f);
      var tspawn = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      var tstages = new[] { Game.LarderIdle, Game.LarderWinding, Game.LarderWinding, Game.LarderActing };
      var tleft = new[] { 0f, Game.PunchWind * 0.8f, Game.PunchWind * 0.15f, Game.PunchFlash * 0.6f };
      for (int i = 0; i < 4; i++) {
        var at = new V2(800f + (i - 1.5f) * 110f, 420f);
        var en = (Enemy)tspawn.Invoke(tgm, new object[] { EnemyType.Thug, at, 0f, 0, false });
        en.SpawnT = 0f; en.Pos = en.Disp = at; en.Facing = -MathX.Pi / 2f;
        en.LarderStage = tstages[i]; en.LarderT = tleft[i]; en.Traits = 0;
      }
    }
    if (Environment.GetEnvironmentVariable("RALLY") != null) {
      // RALLY=1: a Guard with Rioters round it (amber rally ring), a lone Rioter at each end; bottle Rioters carry a bottle,
      // one rcocked to throw, one bottle in flight. KNIGHT=1 ZOOM=2.2.
      var rgm = host.Game; rgm.Bot = false; rgm.Enemies.Clear(); rgm.Bullets.Clear(); rgm.Wave = Math.Max(rgm.Wave, Game.BottleFromWave);
      me.Pos = me.Disp = new V2(1010f, 640f); me.InvulnT = 1f; me.Aim = -MathX.Pi / 2f;
      foreach (var pl in rgm.Players.Values) if (pl != me) pl.Pos = pl.Disp = new V2(300f, 200f);
      var rspawn = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      var rgd = (Enemy)rspawn.Invoke(rgm, new object[] { EnemyType.Guard, new V2(1010f, 520f), 0f, 0, false });
      rgd.SpawnT = 0f; rgd.Pos = rgd.Disp = new V2(1010f, 520f); rgd.Facing = MathX.Pi / 2f;
      var rspots = new[] { new V2(950f, 540f), new V2(1070f, 540f), new V2(980f, 470f), new V2(1050f, 470f), new V2(1010f, 585f), new V2(720f, 520f), new V2(1300f, 520f), new V2(860f, 600f) };
      bool rcocked = false;
      for (int ri = 0; ri < rspots.Length; ri++) {
        bool rwant = !(ri == 0 || ri == 3 || ri == 7);
        Enemy rr = null;
        for (int rtries = 0; rtries < 400; rtries++) {
          rr = (Enemy)rspawn.Invoke(rgm, new object[] { EnemyType.Rioter, rspots[ri], 0f, 0, false });
          if (Game.BottleRioter(rr) == rwant) break;
          rgm.Enemies.Remove(rr.Id);
        }
        rr.SpawnT = 0f; rr.Pos = rr.Disp = rspots[ri]; rr.Facing = MathX.Pi / 2f;
        if (Game.BottleRioter(rr) && !rcocked) { rr.Phase = 1; rcocked = true; }
      }
      rgm.Bullets[9001] = new Bullet { Id = 9001, Kind = Defs.BottleShot, Pos = new V2(1010f, 600f), Vel = new V2(0f, 330f), Life = 0.6f, Life0 = 1f, Radius = 6f };
    }
    if (Environment.GetEnvironmentVariable("WARDENCREW") != null) {
      // WARDENCREW=1: the Warden's officers in a row facing you below: Guard, Riot Shield, Gas Grenadier, Taser Guard,
      // Security Guard; WARDENCREW=2: the same caught mid-move (swinging, winding a bash, charging, just fired). KNIGHT=1 ZOOM=2.6.
      var gm = host.Game; gm.Bot = false; gm.Enemies.Clear(); gm.Bullets.Clear();
      bool act = Environment.GetEnvironmentVariable("WARDENCREW") == "2";
      me.Pos = me.Disp = new V2(1010f, 640f); me.InvulnT = 1f; me.Aim = -MathX.Pi / 2f;
      foreach (var pl in gm.Players.Values) if (pl != me) pl.Pos = pl.Disp = new V2(300f, 200f);
      var spawn = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      var types = new[] { EnemyType.Guard, EnemyType.RiotShield, EnemyType.GasGrenadier, EnemyType.TaserGuard, EnemyType.SecurityGuard };
      for (int i = 0; i < types.Length; i++) {
        var at = new V2(1010f + (i - 2) * 66f, 555f);
        var en = (Enemy)spawn.Invoke(gm, new object[] { types[i], at, 0f, 0, false });
        en.SpawnT = 0f; en.Pos = en.Disp = at; en.Facing = (me.Pos - at).Angle;
        if (types[i] == EnemyType.RiotShield) en.Phase = (byte)((act ? Game.RiotWinding : Game.RiotWalk) << 3);
        if (types[i] == EnemyType.TaserGuard) en.Winding = act;
        if (types[i] == EnemyType.SecurityGuard) { en.Phase = en.SeenPhase = (byte)(act ? 5 : 6); en.SeenT = act ? 0.06f : 2f; en.Winding = act; }
        if (types[i] == EnemyType.Guard && act) { en.LarderStage = Game.BatonActing; en.LarderT = Game.BatonFlash * 0.45f; }
      }
    }
    if (Environment.GetEnvironmentVariable("CROWD") != null) {
      // CROWD=<n>: a Warden breach mob of n round you (Rioters, Guards, Security Guards, Riot Shields, Thugs), all on screen,
      // for the crowd's draw cost (WorldRenderer.cs CrowdTol); prints the GL vertices of the frame. NINJA=0.
      var gm = host.Game; gm.Bot = false; gm.Enemies.Clear(); gm.Bullets.Clear();
      int cn = int.Parse(Environment.GetEnvironmentVariable("CROWD"));
      me.Pos = me.Disp = new V2(800f, 450f); me.InvulnT = 1f; me.Aim = -MathX.Pi / 2f;
      foreach (var pl in gm.Players.Values) if (pl != me) pl.Pos = pl.Disp = new V2(760f, 470f);
      var spawn = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      var mix = new[] { EnemyType.Rioter, EnemyType.Rioter, EnemyType.Rioter, EnemyType.Guard, EnemyType.SecurityGuard, EnemyType.Rioter, EnemyType.RiotShield, EnemyType.Thug };
      var crng = new System.Random(3);
      for (int i = 0; i < cn; i++) {
        float ca = i * 2.39996f, cr = 110f + 330f * (float)Math.Sqrt((i + 0.5f) / cn);
        var at = me.Pos + new V2((float)Math.Cos(ca) * cr * 1.6f, (float)Math.Sin(ca) * cr * 0.95f);
        var en = (Enemy)spawn.Invoke(gm, new object[] { mix[i % mix.Length], at, 0f, 0, false });
        en.SpawnT = 0f; en.Spawning = false; en.Pos = en.Disp = at; en.Facing = (me.Pos - at).Angle + (float)(crng.NextDouble() - 0.5);
      }
    }
    if (Environment.GetEnvironmentVariable("TURNKEY") != null) {
      // TURNKEY=1: the Turnkey (Turnkey.cs, TurnkeyLook.cs) running, planting and building (its Cell Doors in the wall: TKDOORS=1,
      // ShotCellDoor.cs); Floor Mines arming, armed and beeping; Shackle Traps open and snapped; a Mop Slick. NINJA=0 ZOOM=2.2.
      var gm = host.Game; gm.Bot = false; gm.Enemies.Clear(); gm.Bullets.Clear(); gm.Patches.Clear();
      me.Pos = me.Disp = new V2(1010f, 690f); me.InvulnT = 1f; me.Aim = -MathX.Pi / 2f;
      foreach (var pl in gm.Players.Values) if (pl != me) pl.Pos = pl.Disp = new V2(300f, 200f);
      var spawn = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      Func<EnemyType, V2, Enemy> put = (ty, at) => { var en = (Enemy)spawn.Invoke(gm, new object[] { ty, at, 0f, 0, false }); en.SpawnT = 0f; en.Pos = en.Disp = at; en.Facing = (me.Pos - at).Angle; return en; };
      gm.Props.Clear();
      var k1 = put(EnemyType.Turnkey, new V2(910f, 600f)); k1.Phase = 1;
      var k2 = put(EnemyType.Turnkey, new V2(1010f, 600f)); k2.Phase = 2; k2.PhaseT = Game.TurnkeyPlant * 0.4f;
      var k3 = put(EnemyType.Turnkey, new V2(1110f, 600f)); k3.Phase = 3; k3.PhaseT = Game.TurnkeyBuild * 0.3f;
      float[] mineAge = { 0.4f, 3f, 3f };
      for (int i = 0; i < 3; i++) { var m = put(EnemyType.FloorMine, new V2(850f + i * 34f, 650f)); m.T2 = mineAge[i]; m.Phase = (byte)(i == 0 ? 0 : i == 1 ? 1 : 2); m.PhaseT = 0.2f; }
      for (int i = 0; i < 2; i++) { var sn = put(EnemyType.ShackleTrap, new V2(1140f + i * 40f, 650f)); sn.T2 = 3f; sn.Phase = (byte)(i == 0 ? 1 : 2); sn.PhaseT = 0.4f; }
      gm.Patches[81] = new PathPatch { Id = 81, Kind = PatchKind.Slick, Pos = new V2(1010f, 760f), Warn = Game.SlickWarn, Life = Game.SlickWarn + Game.SlickLife, R0 = Game.SlickRadius, R1 = Game.SlickRadius, Age = 3f };
    }
    if (Environment.GetEnvironmentVariable("SECSHIRT") != null) {
      // SECSHIRT=1: the Security Guard's officer look (SecurityLook.cs) beside a riot Guard:
      // the Guard, then Security Guards walking, locking on, just fired and reloading, each facing you below; KNIGHT=1 ZOOM=2.6.
      var gm = host.Game; gm.Bot = false; gm.Enemies.Clear(); gm.Bullets.Clear();
      me.Pos = me.Disp = new V2(800f, 525f); me.InvulnT = 1f; me.Aim = MathX.Pi / 2f;
      foreach (var pl in gm.Players.Values) if (pl != me) pl.Pos = pl.Disp = new V2(300f, 200f);
      var spawn = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      for (int i = 0; i < 5; i++) {
        var at = new V2(800f + (i - 2) * 105f, 410f);
        bool tz = Environment.GetEnvironmentVariable("SECSHIRT") == "2" && i > 0;  // SECSHIRT=2: Taser Guards instead (idle, charging)
        if (Environment.GetEnvironmentVariable("SECSHIRT") == "4" && i > 0) {  // SECSHIRT=4: Gas Grenadiers walking, then winding up and throwing a lob
          var gg = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.GasGrenadier, at, 0f, 0, false });
          gg.SpawnT = 0f; gg.Pos = gg.Disp = at; gg.Facing = (me.Pos - at).Angle + (i <= 2 ? (i - 1.5f) * 0.6f : 0f);
          if (i >= 3)  // winding up its lob, then mid-throw
            gm.Effects.Add(new Effect { Kind = FxKind.GasLob, Pos = me.Pos + new V2((i - 3.5f) * 120f, 0f), Pos2 = new V2(gg.Id, 1f), Color = new Rgba(0.6f, 0.95f, 0.4f), Radius = 70f,
              Dur = Game.GrenadierWindUp + Game.GrenadierSwing + 1f, T = i == 3 ? 0.6f : Game.GrenadierWindUp + Game.GrenadierSwing * 0.6f });
          continue;
        }
        if (Environment.GetEnvironmentVariable("SECSHIRT") == "3" && i > 0) {  // SECSHIRT=3: Riot Shields instead (walking, winding up, cracked, broken)
          var rs = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.RiotShield, at, 0f, 0, false });
          rs.SpawnT = 0f; rs.Pos = rs.Disp = at; rs.Facing = (me.Pos - at).Angle;
          var stc = new[] { 0, Game.RiotWalk << 3, Game.RiotWinding << 3, Game.RiotWalk << 3 | 3, Game.RiotFlail << 3 | Game.RiotCracked };
          rs.Phase = (byte)stc[i];
          continue;
        }
        var en = (Enemy)spawn.Invoke(gm, new object[] { i == 0 ? EnemyType.Guard : tz ? EnemyType.TaserGuard : EnemyType.SecurityGuard, at, 0f, 0, false });
        en.SpawnT = 0f; en.Pos = en.Disp = at; en.Facing = (me.Pos - at).Angle;
        if (tz) en.Winding = i >= 3;
        else if (i > 0) { en.Phase = en.SeenPhase = (byte)(i == 4 ? 0 : 7 - i); en.SeenT = i == 3 ? 0.06f : i == 4 ? 1.1f : 2f; en.Winding = i == 2; }
      }
    }
    if (Environment.GetEnvironmentVariable("RIOTLINE") != null) {
      // The Riot Line (the last wave before the Warden), RIOTLINE=<seconds> after it arrives, with you standing in the middle.
      var gm = host.Game; gm.Bot = false;
      foreach (var e in gm.Enemies.Values) e.Hp = 0f;
      gm.Bullets.Clear();
      me.Pos = me.Disp = new V2(800f, 450f);
      for (int f = 0; f < 3; f++) { gm.FixedStep(1f / 60f, new PlayerInput { AimAt = me.Pos + new V2(100f, 0f) }); gm.Update(1f / 60f); }
      typeof(Game).GetMethod("SpawnSquad", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(gm, new object[] { Approaches.For(EnemyType.Warden).Finale });
      float rt = float.Parse(Environment.GetEnvironmentVariable("RIOTLINE"));
      for (int f = 0; f < 60 * rt; f++) { me.Pos = new V2(800f, 450f); me.InvulnT = 1f; gm.FixedStep(1f / 60f, new PlayerInput { AimAt = me.Pos + new V2(100f, 0f) }); gm.Update(1f / 60f); }
      foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
      me.Disp = me.Pos;
      gm.Bullets.Clear();
    }
    if (Environment.GetEnvironmentVariable("SURVIVAL") != null) {
      // Survival (SurvivalGame.cs): SURVIVAL=<clock seconds>[,pick] (pick: held for a level-up pick). Modes hud, gameover.
      var sp = Environment.GetEnvironmentVariable("SURVIVAL").Split(',');
      var sg = host.Game;
      sg.SurvivalOn = true; sg.SurvStarted = true;
      sg.SurvTime = float.Parse(sp[0], System.Globalization.CultureInfo.InvariantCulture); sg.Wave = Survival.WaveAt(sg.SurvTime);
      sg.Hunt = null; sg.Quick = false;
      // The big room (SurvivalWorld.cs): up, with its props; SURVPOS=x,y puts you somewhere in it (default: the start).
      sg.RoomsUp = true; sg.Layout = LayoutId.Open; sg.LayoutAge = 99f; sg.PropsOn = true;
      var spos = Environment.GetEnvironmentVariable("SURVPOS") != null ? Array.ConvertAll(Environment.GetEnvironmentVariable("SURVPOS").Split(','), float.Parse) : new[] { 1300f, 700f };
      int sk = 0; foreach (var pl in sg.Players.Values) { pl.Pos = pl.Disp = new V2(spos[0] + 90f * sk, spos[1]); sk++; }
      typeof(Game).GetMethod("SurvivalProps", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(sg, new object[] { true });
      if (Environment.GetEnvironmentVariable("SURVMARKS") != null) {  // SURVMARKS=<n>: n more enemies scattered over the room, off your view (the merged arrows with their counts)
        var mr = new System.Random(5); var spawn = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
        var types = new[] { EnemyType.Swarmer, EnemyType.Grunt, EnemyType.Brute, EnemyType.Sniper };
        for (int i = 0, n = int.Parse(Environment.GetEnvironmentVariable("SURVMARKS")); i < n; i++) {
          var at = new V2(100f + (float)mr.NextDouble() * 3000f, 100f + (float)mr.NextDouble() * 1600f);
          if (Math.Abs(at.X - spos[0]) < 900f && Math.Abs(at.Y - spos[1]) < 520f) { i--; continue; }
          spawn.Invoke(sg, new object[] { types[mr.Next(types.Length)], at, 0f, 0, mr.NextDouble() < 0.12 });
        }
        foreach (var en in sg.Enemies.Values) en.Disp = en.Pos;
      }
      if (sp.Length > 1 && sp[1] == "pick") { sg.State = MatchState.Intermission; sg.StateTimer = 1f; me.PendingPicks = 1; }
    }
    if (Environment.GetEnvironmentVariable("TUTORIAL") != null) {
      // The first-run tutorial's prompt (TutorialUi.cs): TUTORIAL=<lesson 0-4>,<steps of it done>[,open] (open: between waves with a build round open).
      var tp = Environment.GetEnvironmentVariable("TUTORIAL").Split(',');
      host.Game.Tutorial.ShowAt(int.Parse(tp[0]), tp.Length > 1 ? int.Parse(tp[1]) : 0);
      if (tp.Length > 2 && tp[2] == "open") { host.Game.State = MatchState.Intermission; host.Game.ExpandOpen = true; }  // a build round open (the BUILD A ROOM lesson)
    }
    if (Environment.GetEnvironmentVariable("DRILL") != null) {
      // A Training drill (DrillGame.cs, DrillUi.cs): DRILL=<steps done>,<seconds>[,done[,hits]] (Move and Aim: you a little short of the lit beacon);
      // DRILLID=1: the Dodge drill instead (the wall a little short of you; DRILLHIT=1: just caught by one); DRILLID=2: Wall Kick; DRILLID=3: Shoot; DRILLID=4: Parry (PARRYOFF=1: shield down); DRILLID=5: Read the Tells (TELLT, TELLIN=1).
      var dp = Environment.GetEnvironmentVariable("DRILL").Split(',');
      var gm = host.Game;
      gm.Enemies.Clear(); gm.Bullets.Clear(); gm.Hunt = null; gm.Quick = false; gm.Wave = 1;
      gm.Drill = Environment.GetEnvironmentVariable("DRILLID") != null ? int.Parse(Environment.GetEnvironmentVariable("DRILLID")) : 0; gm.DrillRunners.Clear();
      var dk = Training.Drills[gm.Drill];
      foreach (var q in gm.Players.Values) {  // everyone a recruit, as a drill's start does, with the scene's ability looks cleared
        typeof(Game).GetMethod("MakeRecruit", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(gm, new object[] { q });
        q.AegisT = 0f; q.UltT = 0f; q.BarrierT = 0f; q.ShieldHp = 0f; q.ShieldT = 0f; q.OverHp = 0f; q.FrenzyT = 0f; q.InvulnT = 0f;
      }
      gm.Props.Clear();
      foreach (var q in gm.Players.Values) gm.DrillRunners[q.Id] = new DrillRunner { PlayerId = q.Id, Step = 1, Time = 9f };
      var mine = gm.DrillRunners[me.Id];
      mine.Step = int.Parse(dp[0]); mine.Time = float.Parse(dp[1], System.Globalization.CultureInfo.InvariantCulture); mine.Flash = 9f;
      if (dp.Length > 3) mine.Hits = int.Parse(dp[3]);
      if (dp.Length > 2 && dp[2] == "done") { mine.Step = dk.Steps; mine.Done = true; if (gm.Profile != null) gm.Profile.DrillDone(dk.Key, mine.Time); }
      var at = dk.Key == "dodge" ? new V2(900f, 470f) : Training.MoveBeacon(Math.Min(mine.Step, Training.MoveSteps - 1), Game.Width, Game.Height);
      if (dk.Key == "dodge" && !mine.Done) { mine.Wall = 900f - Training.WallDir(mine.Step) * 110f; if (Environment.GetEnvironmentVariable("DRILLHIT") != null) { mine.Hits = Math.Max(1, mine.Hits); mine.HitT = 0.1f; mine.Wall = 900f + Training.WallDir(mine.Step) * 40f; } }
      me.Pos = me.Disp = at + new V2(-170f, 60f); me.Aim = 0f; me.AimAt = at;
      int k2 = 0; foreach (var q in gm.Players.Values) if (q != me) { q.Pos = q.Disp = new V2(300f + 140f * k2++, 820f); }
      if (dk.Key == "dodge") { me.Pos = me.Disp = at; me.AimAt = at + new V2(200f, 0f); }
      if (dk.Key == "wallkick") {  // you short of the lit panel, or on the way to the flag
        at = mine.Step < Training.KickWalls ? Training.KickPanel(mine.Step, Game.Width, Game.Height).Pos + (mine.Step == 1 ? new V2(-40f, 150f) : new V2(-150f, 40f)) : Training.KickFlag(Game.Width, Game.Height) + new V2(-420f, 200f);
        me.Pos = me.Disp = at; me.AimAt = at + new V2(200f, 0f);
      }
      if (dk.Key == "tells") {  // a wind-up part-way (TELLT=<s>, default 0.6) aimed where you stood; you a step out of it (TELLIN=1: still in it)
        me.Pos = me.Disp = new V2(800f, 470f);
        if (!mine.Done) {
          mine.TellUp = true; mine.TellKind = Training.TellKind(mine.Step); mine.TellAt = me.Pos; mine.TellFrom = Training.TellStart(mine.Step, me.Pos);
          mine.TellT = Environment.GetEnvironmentVariable("TELLT") != null ? float.Parse(Environment.GetEnvironmentVariable("TELLT"), System.Globalization.CultureInfo.InvariantCulture) : 0.6f;
          if (Environment.GetEnvironmentVariable("TELLIN") == null) { V2 dir = mine.TellKind == Training.TellCircle ? new V2(1f, 0f) : (mine.TellAt - mine.TellFrom).Normalized(); me.Pos = me.Disp = mine.TellAt + new V2(-dir.Y, dir.X) * (mine.TellKind == Training.TellCircle ? 140f : 80f); }
          me.AimAt = mine.TellFrom; me.Aim = (mine.TellFrom - me.Pos).Angle;
        }
      }
      if (dk.Key == "parry") {  // the shot on its way in from its side, you with the shield up toward it (PARRYOFF=1: down)
        me.Pos = me.Disp = new V2(800f, 470f);
        if (!mine.Done) {
          V2 from = Training.ParryStart(mine.Step, me.Pos);
          mine.ShotUp = true; mine.Shot = me.Pos + (from - me.Pos).Normalized() * 170f;
          me.Aim = (mine.Shot - me.Pos).Angle; me.AimAt = mine.Shot;
          if (Environment.GetEnvironmentVariable("PARRYOFF") == null) me.Parrying = true;
        }
      }
      if (dk.Key == "shoot" && !mine.Done) {  // the target up, part-way along its lane, you a way below it aiming at it
        var tp = Training.ShootAt(mine.Step, 1.1f, Game.Width, Game.Height);
        me.Pos = me.Disp = Training.ShootSpot(mine.Step, Game.Width, Game.Height) + new V2(-120f, Training.ShootSpot(mine.Step, Game.Width, Game.Height).Y > Game.Height / 2f ? -260f : 260f); me.AimAt = tp;
        var te = (Enemy)typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(gm, new object[] { EnemyType.Grunt, tp, 0f, 0, false });
        te.Disp = te.Pos; mine.TargetId = te.Id;
      }
      if (dk.Key == "move" && !mine.Done && Training.MoveShootStep(mine.Step)) {
        var e = (Enemy)typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(gm, new object[] { EnemyType.Grunt, at, 0f, 0, false });
        e.Disp = e.Pos; mine.TargetId = e.Id;
      }
      gm.State = MatchState.Fighting;
    }
    if (Environment.GetEnvironmentVariable("RIOT") != null) {
      // Riot Gear (RiotGearLook.cs): RIOT=ready | wait,<seconds left> | block,<seconds after the block>. You and your friend have it; an enemy shot comes in from the right.
      var gm = host.Game; gm.Bot = false;
      foreach (var e in gm.Enemies.Values) e.Hp = 0f;
      gm.Bullets.Clear();
      me.Role = Role.Dps; me.Kit = Kit.Soldier; me.Def = Defs.Get(Role.Dps); me.Weapon = Defs.Weapon(Role.Dps, Kit.Soldier, 0); me.MaxHp = me.Def.Hp; foreach (var sl in me.Slots) sl.Id = AbilityId.None; foreach (var q0 in gm.Players.Values) { q0.AegisT = 0f; q0.UltT = 0f; }  // a plain Soldier with no ability, so nothing else draws round you
      me.Pos = me.Disp = new V2(800f, 450f); me.Aim = -0.4f; me.AimAt = me.Pos + V2.FromAngle(me.Aim) * 200f; me.Hp = me.MaxHp;
      foreach (var q in gm.Players.Values) { q.Perks = new[] { PerkId.RiotGear }; q.RiotT = 0f; if (q != me) { q.Pos = q.Disp = me.Pos + new V2(-240f, 90f); q.RiotT = 14f; } }
      var rp = Environment.GetEnvironmentVariable("RIOT").Split(',');
      if (rp[0] == "wait") me.RiotT = float.Parse(rp[1]);
      if (rp[0] == "ready") gm.Bullets[900001] = new Bullet { Id = 900001, Kind = 10, Pos = me.Pos + new V2(95f, 22f), Vel = new V2(-30f, -8f), Life = 3f, Radius = 4f, Dmg = 5f };
      if (rp[0] == "block") {
        gm.Bullets[900001] = new Bullet { Id = 900001, Kind = 10, Pos = me.Pos + new V2(34f, 12f), Vel = new V2(-30f, -8f), Life = 3f, Radius = 4f, Dmg = 5f };
        typeof(Game).GetMethod("Damage", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(gm, new object[] { me, 10f, "Test", });
        gm.Bullets.Clear();
        float bt = rp.Length > 1 ? float.Parse(rp[1]) : 0.15f;
        for (int f = 0; f < 60 * bt; f++) { me.Pos = new V2(800f, 450f); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = me.AimAt }); gm.Update(1f / 60f); }
        me.Disp = me.Pos;
      }
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    }
    if (Environment.GetEnvironmentVariable("IRONHIDE") != null) {
      // The Tank's Ironhide (IronhideLook.cs): IRONHIDE=<shield left>[,<wait s>], a Tank Soldier in the middle. Mode hud, 1 player.
      var gm = host.Game; gm.Bot = false;
      foreach (var e in gm.Enemies.Values) e.Hp = 0f;
      gm.Bullets.Clear();
      var ip = Environment.GetEnvironmentVariable("IRONHIDE").Split(',');
      me.Role = Role.Tank; me.Kit = Kit.Soldier; me.Def = Defs.Get(Role.Tank); me.Weapon = Defs.Weapon(Role.Tank, Kit.Soldier, 0); me.MaxHp = me.Def.Hp; me.Hp = me.MaxHp;
      foreach (var sl in me.Slots) sl.Id = AbilityId.None;
      foreach (var q0 in gm.Players.Values) { q0.AegisT = 0f; q0.UltT = 0f; }
      me.Pos = me.Disp = new V2(800f, 450f); me.Aim = -0.4f; me.AimAt = me.Pos + V2.FromAngle(me.Aim) * 200f;
      me.IronLost = Game.IronhideMax - float.Parse(ip[0]); me.IronWait = ip.Length > 1 ? float.Parse(ip[1]) : 0f;
    }
    if (Environment.GetEnvironmentVariable("HEALAURA") != null) {
      // The Healer's aura (HealerAuraLook.cs): HEALAURA=1, a Healer Soldier in the middle, a Tank inside the aura and a DPS outside; HEALAURA=2 puts the Tank at the rim. Mode hud, 3 players.
      var gm = host.Game; gm.Bot = false;
      foreach (var e in gm.Enemies.Values) e.Hp = 0f;
      gm.Bullets.Clear();
      me.Role = Role.Healer; me.Kit = Kit.Soldier; me.Def = Defs.Get(Role.Healer); me.Weapon = Defs.Weapon(Role.Healer, Kit.Soldier, 0); me.MaxHp = me.Def.Hp; me.Hp = me.MaxHp;
      foreach (var sl in me.Slots) sl.Id = AbilityId.None;
      foreach (var q0 in gm.Players.Values) { q0.AegisT = 0f; q0.UltT = 0f; }
      me.Pos = me.Disp = new V2(800f, 450f); me.Aim = -0.4f; me.AimAt = me.Pos + V2.FromAngle(me.Aim) * 200f;
      int n = 0;
      foreach (var q in gm.Players.Values) if (q != me) { var r = n == 0 ? Role.Tank : Role.Dps; q.Role = r; q.Kit = Kit.Soldier; q.Def = Defs.Get(r); q.Weapon = Defs.Weapon(r, Kit.Soldier, 0); q.MaxHp = q.Hp = q.Def.Hp; q.Pos = q.Disp = me.Pos + (n++ == 0 ? (Environment.GetEnvironmentVariable("HEALAURA") == "2" ? new V2(-130f, 95f) : new V2(-60f, 45f)) : new V2(230f, 40f)); }
    }
    if (Environment.GetEnvironmentVariable("MOMENTUM") != null) {
      // Momentum (MomentumLook.cs): MOMENTUM=<stacks 0-5>, a DPS Soldier standing in the middle with that many stacks.
      var gm = host.Game; gm.Bot = false;
      foreach (var e in gm.Enemies.Values) e.Hp = 0f;
      gm.Bullets.Clear();
      me.Role = Role.Dps; me.Kit = Kit.Soldier; me.Def = Defs.Get(Role.Dps); me.Weapon = Defs.Weapon(Role.Dps, Kit.Soldier, 0); me.MaxHp = me.Def.Hp; me.Hp = me.MaxHp;
      foreach (var sl in me.Slots) sl.Id = AbilityId.None;
      foreach (var q0 in gm.Players.Values) { q0.AegisT = 0f; q0.UltT = 0f; }
      me.Pos = me.Disp = new V2(800f, 450f); me.Aim = -0.4f; me.AimAt = me.Pos + V2.FromAngle(me.Aim) * 200f;
      me.MomentumStacks = int.Parse(Environment.GetEnvironmentVariable("MOMENTUM")); me.MomentumT = 3f;
      foreach (var q in gm.Players.Values) if (q != me) { q.Pos = q.Disp = me.Pos + new V2(-240f, 90f); }
    }
    if (Environment.GetEnvironmentVariable("SHADOWSTEP") != null) {
      // A Ninja's Shadow Step look (ShadowStepLook.cs): SHADOWSTEP=<seconds after the press>, SHADOWDIST=<px from where you stood to the enemy, default 400>, SHADOWNONE=1 for a short step with no enemy. Use with mode hud.
      var gm = host.Game; gm.Bot = false;
      foreach (var e in gm.Enemies.Values) e.Hp = 0f;
      gm.Bullets.Clear(); gm.Effects.Clear();
      me.Kit = Kit.Ninja; me.Role = Role.Dps; me.Def = Defs.Get(Role.Dps);
      float sd = Environment.GetEnvironmentVariable("SHADOWDIST") != null ? float.Parse(Environment.GetEnvironmentVariable("SHADOWDIST")) : 400f;
      V2 from = new V2(300f, 450f), foe = from + new V2(sd, 0f);
      float er = 0f;
      if (Environment.GetEnvironmentVariable("SHADOWNONE") == null) {
        var en = (Enemy)typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(gm, new object[] { EnemyType.Brute, foe, 0f, 0, false });
        en.Hp = en.MaxHp = 1e6f; en.FrozenT = 99f; en.Pos = en.Disp = foe; er = en.Radius;
      }
      V2 land = er > 0f ? foe + new V2(er + me.Def.BodyR + 8f, 0f) : from + new V2(300f, 0f);
      me.Pos = me.Disp = land; me.Aim = 0f; me.AimAt = foe;
      gm.Effects.Add(new Effect { Kind = FxKind.ShadowStep, Pos = from, Pos2 = land, Color = new Rgba(0.72f, 0.58f, 1f), Radius = me.Id * 1000f + er, Dur = Game.ShadowStepFxTime, T = float.Parse(Environment.GetEnvironmentVariable("SHADOWSTEP")) });
    }
    if (Environment.GetEnvironmentVariable("BEAMIN") != null) {
      // The boss wave beam-in (BossBeamLook.cs): BEAMIN=<seconds since it began, 0 to 1.9>. Two players; use with mode hud, 2 players (WAVECLEARED=boss: with the between-waves box, which steps aside). The beam is from the left to the middle of the arena.
      var gm = host.Game; gm.Bot = false;
      foreach (var e in gm.Enemies.Values) e.Hp = 0f;
      gm.Bullets.Clear(); gm.Effects.Clear();
      float bt = float.Parse(Environment.GetEnvironmentVariable("BEAMIN"));
      int bi = 0;
      foreach (var q in gm.Players.Values) {
        V2 from = new V2(380f, 330f + bi * 230f), to = new V2(820f, 300f + bi * 260f);
        float lag = bi * 0.18f;
        q.Pos = q.Disp = bt - lag >= Game.BeamMoveAt ? to : from;
        q.WarpT = System.Math.Max(0f, Game.BeamTotal - System.Math.Max(0f, bt - lag));  // (locked while it runs, as in the game: the between-waves box steps aside)
        gm.Effects.Add(new Effect { Kind = FxKind.BossBeam, Pos = from, Pos2 = to, Color = q.Def.Color, Radius = q.Id, Dur = Game.BeamTotal, T = System.Math.Max(0f, bt - lag) });
        bi++;
      }
    }
    if (Environment.GetEnvironmentVariable("LEAPSLAM") != null) {
      // A Tank Knight's Leap Slam look (LeapSlamLook.cs): LEAPSLAM=<seconds after the press> (the leap is 0.45 s, the stun 1 s after it), LEAPMASTERED=1 for the capstone ring. Use with mode hud.
      var gm = host.Game; gm.Bot = false;
      foreach (var e in gm.Enemies.Values) e.Hp = 0f;
      gm.Bullets.Clear(); gm.Effects.Clear();
      me.Kit = Kit.Knight; me.Role = Role.Tank; me.Def = Defs.Get(Role.Tank);
      float lt = float.Parse(Environment.GetEnvironmentVariable("LEAPSLAM"));
      V2 from = new V2(520f, 620f), land = new V2(860f, 400f);
      float f = System.Math.Min(1f, lt / Game.LeapTime);
      me.Pos = me.Disp = from + (land - from) * f; me.Aim = -0.57f; me.AimAt = land;
      gm.Effects.Add(new Effect { Kind = FxKind.LeapSlam, Pos = from, Pos2 = land, Color = Game.LeapBoomColor.WithAlpha(Environment.GetEnvironmentVariable("LEAPMASTERED") != null ? 1f : 0.5f), Radius = me.Id * 1000f + 130f, Dur = Game.LeapTime + Game.LeapFxTail + 1f, T = lt });
      float[,] spots = { { 0.3f, 60f }, { 1.3f, 90f }, { 2.2f, 112f }, { 3.2f, 75f }, { 4.2f, 108f }, { 5.3f, 50f }, { 0.9f, 190f }, { 3.8f, 215f } };
      for (int i = 0; i < 8; i++) {
        V2 at = land + V2.FromAngle(spots[i, 0]) * spots[i, 1];
        var en = (Enemy)typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(gm, new object[] { i == 2 ? EnemyType.Brute : EnemyType.Grunt, at, 0f, 0, false });
        en.Hp = en.MaxHp = 1e6f; en.Pos = en.Disp = at;
        bool stunned = lt >= Game.LeapTime && spots[i, 1] < 150f;
        en.FrozenT = stunned ? 99f : 0f; en.Frozen = stunned;
      }
    }
    if (Environment.GetEnvironmentVariable("CAVALRY") != null) {
      // A Tank Knight's Cavalry Call look (CavalryCallLook.cs): CAVALRY=<seconds after the cast> (lancers appear 0.45 s, ride 0.85 s to 2.75 s), CAVMASTERED=1 for four lancers, CAVAIM=<radians>. Use with mode hud.
      var gm = host.Game; gm.Bot = false;
      foreach (var e in gm.Enemies.Values) e.Hp = 0f;
      gm.Bullets.Clear(); gm.Effects.Clear();
      me.Kit = Kit.Knight; me.Role = Role.Tank; me.Def = Defs.Get(Role.Tank);
      float ct = float.Parse(Environment.GetEnvironmentVariable("CAVALRY"));
      float aimA = float.Parse(Environment.GetEnvironmentVariable("CAVAIM") ?? "0.14");
      V2 from = new V2(300f, 450f), dirC = V2.FromAngle(aimA);
      me.Pos = me.Disp = from; me.Aim = aimA; me.AimAt = from + dirC * 300f;
      bool capC = Environment.GetEnvironmentVariable("CAVMASTERED") != null;
      gm.Effects.Add(new Effect { Kind = FxKind.CavalryCall, Pos = from, Pos2 = from + dirC * Game.CavRange, Color = new Rgba(1f, 0.82f, 0.36f, capC ? 1f : 0.5f), Radius = capC ? 4f : 2f, Dur = Game.CavEndT + Game.CavFadeT + 0.6f, T = ct });
      float[,] spotsC = { { 380f, 10f }, { 520f, -40f }, { 560f, 60f }, { 700f, 0f }, { 860f, -90f }, { 900f, 110f }, { 1050f, 20f }, { 640f, 200f }, { 780f, -230f } };
      for (int i = 0; i < 9; i++) {
        V2 at = from + dirC * spotsC[i, 0] + dirC.Orthogonal() * spotsC[i, 1];
        var en = (Enemy)typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(gm, new object[] { i % 4 == 3 ? EnemyType.Brute : EnemyType.Grunt, at, 0f, 0, false });
        en.Hp = en.MaxHp = 1e6f; en.Pos = en.Disp = at; en.FrozenT = 99f; en.Frozen = true;
      }
    }
    if (Environment.GetEnvironmentVariable("VOLLEY") != null) {
      // A DPS Knight's Warhorn Volley (WarhornVolleyLook.cs): VOLLEY=<seconds after the cast> (horn 0.18-0.9 s, arrows land 0.6 s to Game.VolEndT),
      // VOLMASTERED=1 for the wider lane, VOLAIM=<radians>. Enemies the front has passed burn. Use with mode hud.
      var gm = host.Game; gm.Bot = false;
      foreach (var e in gm.Enemies.Values) e.Hp = 0f;
      gm.Bullets.Clear(); gm.Effects.Clear();
      me.Kit = Kit.Knight; me.Role = Role.Dps; me.Def = Defs.Get(Role.Dps);
      float vt = float.Parse(Environment.GetEnvironmentVariable("VOLLEY"));
      float aimV = float.Parse(Environment.GetEnvironmentVariable("VOLAIM") ?? "0.08");
      V2 fromV = new V2(300f, 460f), dirV = V2.FromAngle(aimV);
      me.Pos = me.Disp = fromV; me.Aim = aimV; me.AimAt = fromV + dirV * 300f;
      bool capV = Environment.GetEnvironmentVariable("VOLMASTERED") != null;
      gm.Effects.Add(new Effect { Kind = FxKind.WarhornVolley, Pos = fromV, Pos2 = fromV + dirV * Game.VolRange, Color = new Rgba(1f, 0.55f, 0.18f, Game.VolHalfOf(capV) / 1000f), Radius = me.Id, Dur = Game.VolFxDur, T = vt });
      float[,] spotsV = { { 260f, 30f }, { 360f, -120f }, { 420f, 150f }, { 520f, -20f }, { 640f, 220f }, { 700f, -200f }, { 820f, 60f }, { 900f, -90f }, { 1000f, 170f }, { 560f, 300f } };
      for (int i = 0; i < 10; i++) {
        V2 at = fromV + dirV * spotsV[i, 0] + dirV.Orthogonal() * spotsV[i, 1];
        var en = (Enemy)typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(gm, new object[] { i % 4 == 3 ? EnemyType.Brute : EnemyType.Grunt, at, 0f, 0, false });
        en.Hp = en.MaxHp = 1e6f; en.Pos = en.Disp = at; en.FrozenT = 99f; en.Frozen = true;
        en.Burning = spotsV[i, 0] < Game.VolFront(vt);
      }
    }
    if (Environment.GetEnvironmentVariable("HARVEST") != null || Environment.GetEnvironmentVariable("HARVESTTHROW") != null) {
      // A Hemomancer's Crimson Harvest (CrimsonHarvestLook.cs): HARVEST=<seconds since the cast> (rip to 2.2 s, then the hold; HARVESTBLOOD=<blood> sets the ball's size, default 26),
      // or HARVESTTHROW=<seconds since the throw> for the flight (to 0.65 s), blast and stain. Use with mode hud.
      var gm = host.Game; gm.Bot = false;
      foreach (var e in gm.Enemies.Values) e.Hp = 0f;
      gm.Bullets.Clear(); gm.Effects.Clear();
      me.Kit = Kit.Hemomancer; me.Role = Role.Dps; me.Def = Defs.Get(Role.Dps);
      V2 from = new V2(420f, 520f), aimH = new V2(1020f, 400f);
      me.Pos = me.Disp = from; me.Aim = -0.2f; me.AimAt = aimH;
      float blood = float.Parse(Environment.GetEnvironmentVariable("HARVESTBLOOD") ?? "26");
      string ht = Environment.GetEnvironmentVariable("HARVESTTHROW");
      if (ht != null)
        gm.Effects.Add(new Effect { Kind = FxKind.HarvestThrow, Pos = from, Pos2 = aimH, Color = new Rgba(0.85f, 0.12f, 0.2f), Radius = blood, Dur = Game.HarvestThrowT + Game.HarvestTail, T = float.Parse(ht) });
      else {
        float hv = float.Parse(Environment.GetEnvironmentVariable("HARVEST"));
        me.HarvestClock = hv; me.HarvestHold = Game.HarvestHoldTime;
        me.HarvestBlood = hv >= Game.HarvestRip ? blood : blood * hv / Game.HarvestRip;
      }
      var rnd = new System.Random(5);
      for (int i = 0; i < 24; i++) {
        V2 at = new V2(80f + (float)rnd.NextDouble() * 1400f, 110f + (float)rnd.NextDouble() * 780f);
        if (at.DistanceTo(from) < 90f) continue;
        var en = (Enemy)typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(gm, new object[] { i % 6 == 5 ? EnemyType.Brute : EnemyType.Grunt, at, 0f, 0, false });
        en.Hp = en.MaxHp = 1e6f; en.Pos = en.Disp = at; en.FrozenT = 99f; en.Frozen = true;
      }
    }
    if (Environment.GetEnvironmentVariable("HEMODPS") != null) {
      // The DPS Hemomancer's 1st abilities. HEMODPS=spray: Arterial Spray gushing into a pack (ArterialSprayLook.cs).
      // HEMODPS=debt: Blood Debt's ledger, coins flying in from a kill (BloodDebtLook.cs); DEBTSETTLE=paid or forfeit shows
      // the stamp instead (DEBTSETTLE=borrowed: the cast). HEMODPS=rush: Vein Rush, RUSHT=<seconds since the cast> (the rush is 0.22 s; default 0.15) (VeinRushLook.cs).
      // Use with mode hud.
      var gm = host.Game; gm.Bot = false;
      foreach (var e in gm.Enemies.Values) e.Hp = 0f;
      gm.Bullets.Clear(); gm.Effects.Clear();
      me.Kit = Kit.Hemomancer; me.Role = Role.Dps; me.Def = Defs.Get(Role.Dps);
      string which = Environment.GetEnvironmentVariable("HEMODPS");
      V2 from = new V2(520f, 480f);
      me.Pos = me.Disp = from; me.Aim = 0f; me.AimAt = from + new V2(300f, 0f); me.Hp = me.MaxHp * 0.7f;
      var blood = new Rgba(0.85f, 0.12f, 0.2f);
      float[,] spots = { { 130f, -10f }, { 175f, 45f }, { 200f, -55f }, { 300f, 20f }, { 160f, 160f }, { -120f, 90f }, { 420f, -140f } };
      var mobs = new System.Collections.Generic.List<Enemy>();
      for (int i = 0; i < 7; i++) {
        V2 at = from + new V2(spots[i, 0], spots[i, 1]);
        var en = (Enemy)typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(gm, new object[] { i == 3 ? EnemyType.Brute : EnemyType.Grunt, at, 0f, 0, false });
        en.Hp = en.MaxHp = 1e6f; en.SpawnT = 0f; en.Pos = en.Disp = at; en.FrozenT = 99f; en.Frozen = true;
        mobs.Add(en);
      }
      if (which == "spray") {
        me.SprayOn = true; me.Hp = me.MaxHp * 0.45f;
      } else if (which == "debt") {
        string settle = Environment.GetEnvironmentVariable("DEBTSETTLE");
        me.Hp = me.MaxHp * 0.42f; me.DebtOwed = me.MaxHp * 0.75f; me.Debt = me.MaxHp * 0.33f; me.DebtT = 3.1f;
        if (settle == "paid") {
          me.Debt = 0f; me.Hp = me.MaxHp; me.DebtT = 1.5f;
          gm.Effects.Add(new Effect { Kind = FxKind.DebtSettle, Pos = from, Pos2 = new V2(me.Id, 1f), Color = new Rgba(1f, 0.8f, 0.35f), Radius = me.DebtOwed, Dur = Game.DebtPaidFx, T = 0.25f });
        } else if (settle == "borrowed") {
          me.Hp = me.MaxHp * 0.25f; me.Debt = me.DebtOwed; me.DebtT = 7.7f;
          gm.Effects.Add(new Effect { Kind = FxKind.DebtSettle, Pos = from, Pos2 = new V2(me.Id, 2f), Color = blood, Radius = me.DebtOwed, Dur = Game.DebtBorrowFx, T = 0.3f });
        } else if (settle == "forfeit") {
          gm.Effects.Add(new Effect { Kind = FxKind.DebtSettle, Pos = from, Pos2 = new V2(me.Id, 0f), Color = blood, Radius = me.Debt, Dur = Game.DebtForfeitFx, T = 0.35f });
          me.Debt = 0f; me.DebtT = 0f;
        } else {
          gm.Effects.Add(new Effect { Kind = FxKind.DebtCoin, Pos = mobs[0].Pos, Pos2 = new V2(me.Id, 0f), Color = blood, Radius = 10f, Dur = Game.DebtCoinFx, T = 0.25f });
          gm.Effects.Add(new Effect { Kind = FxKind.DebtCoin, Pos = mobs[3].Pos, Pos2 = new V2(me.Id, 0f), Color = blood, Radius = 24f, Dur = Game.DebtCoinFx, T = 0.12f });
        }
      } else {
        float rt = float.Parse(Environment.GetEnvironmentVariable("RUSHT") ?? "0.15");
        V2 start = new V2(420f, 475f), end = start + new V2(280f, 0f);
        float k = Math.Min(1f, rt / Game.VeinRushTime);
        me.Pos = me.Disp = start + (end - start) * k;
        gm.Effects.Add(new Effect { Kind = FxKind.VeinRush, Pos = start, Pos2 = end, Color = blood, Radius = me.Id, Dur = Game.VeinRushTime + Game.VeinRushFxTime, T = rt });
        foreach (var en in mobs)
          if (Math.Abs(en.Pos.Y - start.Y) < 40f && en.Pos.X > start.X && en.Pos.X < end.X)
            gm.Effects.Add(new Effect { Kind = FxKind.VeinCut, Pos = en.Pos, Pos2 = new V2(1f, 0f), Color = blood, Radius = me.Id * 1000f + en.Radius, Dur = Game.VeinCutFxTime, T = Math.Max(0f, rt - (en.Pos.X - start.X) / 280f * Game.VeinRushTime) });
        int zi = 0;
        for (float d = 17f; d < 280f && d < 280f * k; d += 34f, zi++)
          gm.Zones[9000 + zi] = new Zone { Id = 9000 + zi, Kind = ZoneKind.VeinTrail, Owner = me.Id, Pos = start + new V2(d, 0f), Radius = 24f, Duration = 2f, T = 2f - Math.Max(0.2f, rt), Aim = Environment.GetEnvironmentVariable("RUSHMASTERED") != null ? 1f : 0f };
      }
    }
    if (Environment.GetEnvironmentVariable("JUGG") != null) {
      // A Tank Soldier's Juggernaut Suit (JuggernautLook.cs): JUGG=<seconds since the cast> (the plates bolt on to 0.6 s; use 0.3 or 3),
      // JUGGARMOR=<armour left, 0-1> (plates come off a sixth at a time), with six enemies around firing at the suit and flak shards
      // flying back. Use with mode hud.
      var gm = host.Game; gm.Bot = false;
      foreach (var e in gm.Enemies.Values) e.Hp = 0f;
      gm.Bullets.Clear(); gm.Effects.Clear();
      me.Kit = Kit.Soldier; me.Role = Role.Tank; me.Def = Defs.Get(Role.Tank); me.Slots[Upgrades.UltSlot].Id = AbilityId.JuggernautSuit;
      V2 from = new V2(640f, 480f);
      me.Pos = me.Disp = from; me.Aim = -0.2f; me.AimAt = from + new V2(300f, -60f); me.Hp = me.MaxHp * 0.9f;
      float jt = float.Parse(Environment.GetEnvironmentVariable("JUGG"));
      me.JuggT = Math.Max(0.1f, Game.JuggTime - jt); me.UltT = me.JuggT;
      me.BarrierT = 0f; me.AegisT = 0f; me.OverdriveT = 0f; me.ShieldHp = 0f;
      me.JuggMax = Game.JuggArmor; me.JuggArmor = Game.JuggArmor * float.Parse(Environment.GetEnvironmentVariable("JUGGARMOR") ?? "0.72");
      var rnd = new System.Random(9);
      for (int i = 0; i < 6; i++) {
        float ang = -1.1f + i * 0.5f + (float)rnd.NextDouble() * 0.2f;
        V2 at = from + V2.FromAngle(ang) * (230f + (float)rnd.NextDouble() * 180f);
        var en = (Enemy)typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(gm, new object[] { i == 2 ? EnemyType.Brute : EnemyType.Grunt, at, 0f, 0, false });
        en.Hp = en.MaxHp = 1e6f; en.SpawnT = 0f; en.Pos = en.Disp = at; en.FrozenT = 99f; en.Frozen = true;
        if (jt < Game.JuggClamp) continue;
        V2 hit = from + (at - from).Normalized() * (me.Def.BodyR * 1.3f + 4f);
        gm.Effects.Add(new Effect { Kind = FxKind.JuggShard, Pos = hit, Pos2 = at, Color = new Rgba(1f, 0.82f, 0.45f), Radius = me.Id, Dur = Game.JuggShardFx, T = 0.03f + 0.045f * i });
      }
    }
    if (Environment.GetEnvironmentVariable("BOLT") != null) {
      // A DPS Elementalist's Living Bolt (LivingBoltLook.cs): BOLT=<seconds since the cast> (the channel runs to 0.5 s; use 0.3 or 4,
      // 9.5 for the flicker); past the channel: three dashes zigzagging in, their residue lanes, the last bolt still lit and two
      // grunts it shocked. Use with mode hud.
      var gm = host.Game; gm.Bot = false;
      foreach (var e in gm.Enemies.Values) e.Hp = 0f;
      gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear();
      me.Kit = Kit.Elementalist; me.Role = Role.Dps; me.Def = Defs.Get(Role.Dps); me.Slots[Upgrades.UltSlot].Id = AbilityId.LivingBolt;
      V2 at0 = new V2(330f, 520f), at1 = new V2(520f, 360f), at2 = new V2(600f, 600f), from = new V2(800f, 440f);
      float bt = float.Parse(Environment.GetEnvironmentVariable("BOLT"));
      bool charged = bt >= Game.BoltChannel;
      if (!charged) from = new V2(640f, 440f);
      me.Pos = me.Disp = from; me.Aim = -0.4f; me.AimAt = from + new V2(240f, -100f); me.Hp = me.MaxHp * 0.9f;
      me.BarrierT = 0f; me.AegisT = 0f; me.OverdriveT = 0f; me.ShieldHp = 0f;
      me.BoltAge = bt; me.BoltT = Math.Max(0.05f, Game.BoltChannel + Game.BoltTime - bt); me.UltT = me.BoltT;
      var spawnAt = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      var rnd = new System.Random(11);
      V2[] spots = { new V2(430f, 455f), new V2(700f, 520f), new V2(560f, 470f), new V2(900f, 320f), new V2(380f, 300f), new V2(980f, 560f), new V2(760f, 250f), new V2(260f, 380f) };
      for (int i = 0; i < spots.Length; i++) {
        var en = (Enemy)spawnAt.Invoke(gm, new object[] { i == 3 ? EnemyType.Brute : EnemyType.Grunt, spots[i], 0f, 0, false });
        en.SpawnT = 0f; en.Pos = en.Disp = spots[i]; en.MaxHp = 1000f; en.Hp = 1000f * (0.4f + 0.55f * (float)rnd.NextDouble());
        if (charged && i < 3) { en.StunT = 0.3f; en.FrozenT = 0.3f; }
      }
      if (charged) {
        V2[] path = { at0, at1, at2, from };
        float[] left = { 1.1f, 2f, 2.9f };
        for (int i = 0; i < 3; i++) {
          V2 a = path[i], b = path[i + 1], d = b - a;
          gm.Zones[9000 + i] = new Zone { Id = 9000 + i, Kind = ZoneKind.StaticResidue, Owner = me.Id, Pos = a + d * 0.5f, Radius = d.Length * 0.5f, Aim = d.Angle, T = left[i], Duration = Game.ResidueTime };
        }
        gm.Effects.Add(new Effect { Kind = FxKind.LivingBolt, Pos = at2, Pos2 = from, Color = Game.BoltCyan, Radius = me.Id, Dur = Game.BoltDashFx, T = 0.05f });
        gm.Effects.Add(new Effect { Kind = FxKind.RodArc, Pos = new V2(690f, 510f), Pos2 = spots[1], Color = Game.BoltCyan, Radius = 40f, Dur = 0.22f, T = 0.04f });
      }
    }
    if (Environment.GetEnvironmentVariable("VORTEX") != null) {
      // A Tank Elementalist's Vortex (VortexLook.cs): VORTEX=<seconds since the cast> (the wind-up runs to 0.4 s; use 0.2, 2 or 5.2),
      // VXMASTERED=1 for 300 px; grunts swept round inside, one drawn in from past the edge, a few enemy shots swept away. Use with mode hud.
      var gm = host.Game; gm.Bot = false;
      foreach (var e in gm.Enemies.Values) e.Hp = 0f;
      gm.Bullets.Clear(); gm.Effects.Clear();
      me.Kit = Kit.Elementalist; me.Role = Role.Tank; me.Def = Defs.Get(Role.Tank); me.Slots[Upgrades.UltSlot].Id = AbilityId.Vortex;
      V2 from = new V2(640f, 440f);
      me.Pos = me.Disp = from; me.Aim = -0.3f; me.AimAt = from + new V2(300f, -90f); me.Hp = me.MaxHp * 0.85f;
      me.BarrierT = 0f; me.AegisT = 0f; me.OverdriveT = 0f; me.ShieldHp = 0f;
      float vt = float.Parse(Environment.GetEnvironmentVariable("VORTEX"));
      me.VortexAge = vt; me.VortexT = Math.Max(0.05f, Game.VortexWindup + Game.VortexTime - vt); me.UltT = me.VortexT;
      me.VortexR = Environment.GetEnvironmentVariable("VXMASTERED") == "1" ? Game.VortexRadiusMastered : Game.VortexRadius;
      var rnd = new System.Random(7);
      for (int i = 0; i < 11; i++) {
        bool outside = i >= 9;
        float ang = i * 0.61f + (float)rnd.NextDouble() * 0.3f;
        float rr = outside ? me.VortexR + 30f + 60f * (i - 9) : 95f + (float)rnd.NextDouble() * (me.VortexR - 110f);
        V2 at = from + V2.FromAngle(ang) * rr;
        var type = i == 3 ? EnemyType.Spinner : i == 6 ? EnemyType.Shard : EnemyType.Grunt;
        var en = (Enemy)typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(gm, new object[] { type, at, 0f, 0, false });
        en.SpawnT = 0f; en.Pos = en.Disp = at;
        en.MaxHp = 1000f; en.Hp = outside ? 1000f : 1000f * (0.35f + 0.5f * (float)rnd.NextDouble());
      }
      if (vt >= Game.VortexWindup)
        for (int i = 0; i < 4; i++) {
          V2 at = from + V2.FromAngle(2.2f + i * 1.3f) * (me.VortexR - 30f - 25f * i);
          gm.Effects.Add(new Effect { Kind = FxKind.VortexWisp, Pos = at, Pos2 = from, Color = Defs.BulletColor(Defs.SmallShot), Radius = 5f, Dur = Game.VortexWispTime, T = 0.08f + 0.1f * i });
        }
    }
    if (Environment.GetEnvironmentVariable("GOLEM") != null) {
      // A Tank Hemomancer's Blood Golem (BloodGolemLook.cs): GOLEM=<seconds since the cast> (the gather runs to 2 s; use 1.2 or 5),
      // GOLCOATS=<coats, default 12 formed / 7 gathering>, GOLPUNCH=<punch number + swing> (2.15: a fist out, 3.2: a Slam),
      // GOLFX=shed|collapse|clot (with GOLFXT=<s into it>). Enemies round it. Use with mode hud.
      var gm = host.Game; gm.Bot = false;
      foreach (var e in gm.Enemies.Values) e.Hp = 0f;
      gm.Bullets.Clear(); gm.Effects.Clear();
      me.Kit = Kit.Hemomancer; me.Role = Role.Tank; me.Def = Defs.Get(Role.Tank); me.Slots[Upgrades.UltSlot].Id = AbilityId.BloodGolem;
      V2 from = new V2(620f, 470f);
      me.Pos = me.Disp = from; me.Aim = -0.25f; me.AimAt = from + new V2(300f, -75f); me.Hp = me.MaxHp * 0.8f;
      me.BarrierT = 0f; me.AegisT = 0f; me.OverdriveT = 0f; me.ShieldHp = 0f;
      float gt = float.Parse(Environment.GetEnvironmentVariable("GOLEM"));
      string fx = Environment.GetEnvironmentVariable("GOLFX");
      if (fx != "collapse") {
        me.GolAge = gt; me.GolT = Math.Max(0.1f, Game.GolGather + Game.GolTime - gt); me.UltT = me.GolT;
        me.GolCoats = float.Parse(Environment.GetEnvironmentVariable("GOLCOATS") ?? (gt < Game.GolGather ? "7" : "12"));
        me.GolPunch = float.Parse(Environment.GetEnvironmentVariable("GOLPUNCH") ?? "0");
      }
      var rnd = new System.Random(4);
      for (int i = 0; i < 9; i++) {
        float ang = -1.4f + i * 0.42f + (float)rnd.NextDouble() * 0.2f;
        V2 at = from + V2.FromAngle(ang) * (130f + (float)rnd.NextDouble() * 170f);
        var en = (Enemy)typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(gm, new object[] { i == 4 ? EnemyType.Brute : EnemyType.Grunt, at, 0f, 0, false });
        en.Hp = en.MaxHp = 1e6f; en.SpawnT = 0f; en.Pos = en.Disp = at; en.FrozenT = 99f; en.Frozen = true;
        if (gt < Game.GolGather && i < 3) {
          en.BlastStacks = 0;
          gm.Effects.Add(new Effect { Kind = FxKind.Drain, Pos = at, Pos2 = from, Color = Game.GolRed, Radius = 3f, Dur = 0.45f, T = 0.12f + 0.1f * i });
        } else if (i % 2 == 0) en.BlastStacks = (byte)(1 + i % 3);
      }
      float ft = float.Parse(Environment.GetEnvironmentVariable("GOLFXT") ?? "0.25");
      if (fx == "shed")
        for (int i = 0; i < 2; i++)
          gm.Effects.Add(new Effect { Kind = FxKind.GolemFx, Pos = from, Pos2 = from + V2.FromAngle(0.6f + i * 2.2f), Color = Game.GolRed, Radius = Game.GolFxShed, Dur = Game.GolShedDur, T = ft });
      else if (fx == "collapse")
        gm.Effects.Add(new Effect { Kind = FxKind.GolemFx, Pos = from, Pos2 = new V2(float.Parse(Environment.GetEnvironmentVariable("GOLCOATS") ?? "6"), me.Id), Color = Game.GolRed, Radius = Game.GolFxCollapse, Dur = Game.GolCollapseDur, T = ft });
      else if (fx == "clot")
        gm.Effects.Add(new Effect { Kind = FxKind.GolemFx, Pos = from + new V2(30f, -8f), Pos2 = from + new V2(290f, -70f), Color = Game.GolRed, Radius = Game.GolFxClot, Dur = Game.GolClotFlight + Game.GolClotTail, T = ft });
    }
    if (Environment.GetEnvironmentVariable("COLOSSUS") != null) {
      // A Tank Elementalist's Stone Colossus (StoneColossusLook.cs): COLOSSUS=<seconds since the cast> (the rise runs to 1 s; use 0.5 or 4),
      // COLWIND=<the slam, 0-1> (0.3: fists up, 0.7: down), COLEND=1: the crumble (rubble and boulders COLEND s in); with enemies round
      // it, a slam's cracks ahead and footfalls behind. Use with mode hud.
      var gm = host.Game; gm.Bot = false;
      foreach (var e in gm.Enemies.Values) e.Hp = 0f;
      gm.Bullets.Clear(); gm.Effects.Clear();
      me.Kit = Kit.Elementalist; me.Role = Role.Tank; me.Def = Defs.Get(Role.Tank); me.Slots[Upgrades.UltSlot].Id = AbilityId.StoneColossus;
      V2 from = new V2(620f, 470f);
      me.Pos = me.Disp = from; me.Aim = -0.25f; me.AimAt = from + new V2(300f, -75f); me.Hp = me.MaxHp * 0.9f;
      me.BarrierT = 0f; me.AegisT = 0f; me.OverdriveT = 0f; me.ShieldHp = 0f;
      float ct = float.Parse(Environment.GetEnvironmentVariable("COLOSSUS"));
      string end = Environment.GetEnvironmentVariable("COLEND");
      var earth = Game.ColEarth;
      if (end == null) {
        me.ColAge = ct; me.ColT = Math.Max(0.1f, Game.ColRise + Game.ColTime - ct); me.UltT = me.ColT;
        me.ColWind = float.Parse(Environment.GetEnvironmentVariable("COLWIND") ?? "0");
      }
      var rnd = new System.Random(4);
      for (int i = 0; i < 9; i++) {
        float ang = -1.4f + i * 0.42f + (float)rnd.NextDouble() * 0.2f;
        V2 at = from + V2.FromAngle(ang) * (150f + (float)rnd.NextDouble() * 170f);
        var en = (Enemy)typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(gm, new object[] { i == 4 ? EnemyType.Brute : EnemyType.Grunt, at, 0f, 0, false });
        en.Hp = en.MaxHp = 1e6f; en.SpawnT = 0f; en.Pos = en.Disp = at; en.FrozenT = 99f; en.Frozen = true;
      }
      if (end != null) {
        float et = float.Parse(end);
        gm.Effects.Add(new Effect { Kind = FxKind.TitanSlam, Pos = from, Pos2 = new V2(Game.TitanEarth + 2, 0f), Color = earth, Radius = 50f, Dur = Game.TitanRubbleTime, T = et });
        gm.Effects.Add(new Effect { Kind = FxKind.TitanSlam, Pos = from, Pos2 = new V2(Game.ColFxBoulders, 0.3f), Color = earth, Radius = Game.ColRoll, Dur = Game.ColRollTime + 0.35f, T = et });
      } else if (ct < Game.ColRise) {
        gm.Effects.Add(new Effect { Kind = FxKind.TitanSlam, Pos = from, Pos2 = new V2(Game.TitanEarth + 1, 0f), Color = earth, Radius = Game.ColRiseRadius, Dur = Game.TitanCrackTime, T = ct });
      } else {
        V2 slam = from + V2.FromAngle(me.Aim) * Game.ColSlamAhead;
        if (me.ColWind == 0f)
          gm.Effects.Add(new Effect { Kind = FxKind.TitanSlam, Pos = slam, Pos2 = new V2(Game.TitanEarth, 0f), Color = earth, Radius = Game.ColSlamRadius, Dur = Game.TitanCrackTime, T = 0.25f });
        for (int i = 0; i < 4; i++)
          gm.Effects.Add(new Effect { Kind = FxKind.TitanSlam, Pos = from + new V2(-60f - 42f * i, 18f * (i % 2 == 0 ? -1f : 1f) + 10f * i), Pos2 = new V2(Game.ColFxStomp, 0f), Color = earth, Radius = Game.ColStompRadius * 1.3f, Dur = 2.2f, T = 0.12f + 0.42f * i });
      }
    }
    if (Environment.GetEnvironmentVariable("TITHE") != null) {
      // A tank Hemomancer's Vein Tithe (VeinTitheLook.cs): TITHE=<seconds since the cast> (the arteries grow to 1.2 s, then beat once a second; use 0.6 or 4.3),
      // with nine enemies held around. TITHESNAP=<seconds since it ended> shows the arteries letting go instead. Use with mode hud.
      var gm = host.Game; gm.Bot = false;
      foreach (var e in gm.Enemies.Values) e.Hp = 0f;
      gm.Bullets.Clear(); gm.Effects.Clear();
      me.Kit = Kit.Hemomancer; me.Role = Role.Tank; me.Def = Defs.Get(Role.Tank); me.Slots[Upgrades.UltSlot].Id = AbilityId.VeinTithe;
      V2 from = new V2(700f, 480f);
      me.Pos = me.Disp = from; me.Aim = -0.2f; me.AimAt = from + new V2(300f, -60f); me.Hp = me.MaxHp * 0.8f;
      float tt = float.Parse(Environment.GetEnvironmentVariable("TITHE"));
      string snap = Environment.GetEnvironmentVariable("TITHESNAP");
      var rnd = new System.Random(5);
      for (int i = 0; i < 9; i++) {
        V2 at = new V2(120f + (float)rnd.NextDouble() * 1300f, 130f + (float)rnd.NextDouble() * 740f);
        if (at.DistanceTo(from) < 120f) { i--; continue; }
        var en = (Enemy)typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(gm, new object[] { i == 4 ? EnemyType.Brute : EnemyType.Grunt, at, 0f, 0, false });
        en.Hp = en.MaxHp = 1e6f; en.SpawnT = 0f; en.Pos = en.Disp = at; en.FrozenT = 99f; en.Frozen = true;
        if (snap != null) gm.Effects.Add(new Effect { Kind = FxKind.TitheSnap, Pos = from, Pos2 = at, Color = new Rgba(0.85f, 0.12f, 0.2f, 0.5f), Radius = en.Radius, Dur = Game.TitheReachTime + 0.2f, T = float.Parse(snap) });
        else me.Tithed.Add(en.Id);
      }
      if (snap == null) { me.TitheClock = Math.Max(0.001f, tt); me.UltT = Math.Max(0.1f, Game.TitheLen - tt); }
    }
    if (Environment.GetEnvironmentVariable("BLOODBATH") != null) {
      // A tank Hemomancer's Bloodbath (BloodbathLook.cs): BLOODBATH=<seconds since the cast> (it floods out to 0.9 s, the channel runs up to 10 s, then it dries over 1.2;
      // use 0.4, 3 or 10.6), with enemies wading in it and around it, an ally inside and a few heal numbers. BATHMASTERED=1: 220 px. Use with mode hud.
      var gm = host.Game; gm.Bot = false;
      foreach (var e in gm.Enemies.Values) e.Hp = 0f;
      gm.Bullets.Clear(); gm.Effects.Clear();
      me.Kit = Kit.Hemomancer; me.Role = Role.Tank; me.Def = Defs.Get(Role.Tank); me.Slots[Upgrades.UltSlot].Id = AbilityId.Bloodbath;
      V2 from = new V2(700f, 480f);
      me.Pos = me.Disp = from + new V2(-30f, 20f); me.Aim = -0.2f; me.AimAt = from + new V2(300f, -60f); me.Hp = me.MaxHp * 0.7f;
      float bt = float.Parse(Environment.GetEnvironmentVariable("BLOODBATH"));
      float R = Environment.GetEnvironmentVariable("BATHMASTERED") != null ? Game.BathRadiusMastered : Game.BathRadius;
      bool drying = bt >= Game.BathTime;  // past the channel's 10 s: the pool drying up
      gm.Zones[9100] = new Zone { Id = 9100, Kind = ZoneKind.Bloodbath, Owner = me.Id, Pos = from, Radius = R, Duration = Game.BathTime + Game.BathDry,
        T = Math.Max(0.01f, Game.BathTime + Game.BathDry - bt), Aim = drying ? 1f : 0f };
      me.UltT = drying ? 0f : Game.BathTime - bt;
      V2[] spots = { new V2(90f, -60f), new V2(-110f, -40f), new V2(40f, 110f), new V2(130f, 70f), new V2(-60f, 120f), new V2(330f, -40f), new V2(-320f, 90f), new V2(260f, 200f) };
      for (int i = 0; i < spots.Length; i++) {
        V2 at = from + spots[i];
        var en = (Enemy)typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(gm, new object[] { i == 3 ? EnemyType.Thunderhead : i == 6 ? EnemyType.Brute : EnemyType.Grunt, at, 0f, 0, false });
        en.Hp = en.MaxHp = 1e6f; en.SpawnT = 0f; en.Pos = en.Disp = at;
      }
      foreach (var q in gm.Players.Values) if (q != me) { q.Pos = q.Disp = from + new V2(-40f, -110f); break; }
      if (bt > 0.6f)
        for (int k = 0; k < 3; k++)
          gm.Effects.Add(new Effect { Kind = FxKind.BathHeal, Pos = me.Pos + new V2(k * 7f, 0f), Pos2 = new V2(me.Id, 0f), Color = new Rgba(1f, 0.35f, 0.4f), Radius = 6f + k * 3f, Dur = Game.BathHealFxTime, T = 0.1f + k * 0.28f });
    }
    if (Environment.GetEnvironmentVariable("STAMPEDE") != null) {
      // A tank Summoner's Stampede (StampedeLook.cs): STAMPEDE=<seconds since the cast> (the gate opens to 0.35 s, the herd charges to 0.92 s, then fades by 1.52;
      // use 0.2, 0.7 or 1.2), with grunts carried along ahead of the line. STAMPEDEMASTERED=1: nine beasts. Use with mode hud.
      var gm = host.Game; gm.Bot = false;
      foreach (var e in gm.Enemies.Values) e.Hp = 0f;
      gm.Bullets.Clear(); gm.Effects.Clear(); gm.Props.Clear(); gm.Layout = LayoutId.Open;
      me.Kit = Kit.Summoner; me.Role = Role.Tank; me.Def = Defs.Get(Role.Tank); me.Slots[0].Id = AbilityId.Stampede;
      V2 from = new V2(420f, 480f);
      me.Pos = me.Disp = from; me.Aim = 0f; me.AimAt = from + new V2(300f, 0f);
      float st = float.Parse(Environment.GetEnvironmentVariable("STAMPEDE"));
      bool cap = Environment.GetEnvironmentVariable("STAMPEDEMASTERED") != null;
      int beasts = cap ? Game.StampedeCapBeasts : Game.StampedeBeasts;
      float life = Game.StampedeLife(cap), dist;
      V2 gate = from + new V2(Game.StampedeAhead, 0f);
      var sz = new Zone { Id = 9102, Kind = ZoneKind.Stampede, Owner = me.Id, Pos = gate, Radius = Game.StampedeSpacing * (beasts - 1) / 2f, Duration = life, Aim = 0f };
      sz.T = Math.Max(0.01f, life - st);
      int leg = Game.StampedeLeg(st, cap, out dist);
      sz.Marched = dist; sz.Pos = gate + new V2(dist, 0f); sz.Aim = leg == 1 ? MathX.Tau / 2f : 0f;
      gm.Zones[sz.Id] = sz;
      V2[] spots = { new V2(-30f, -120f), new V2(60f, -60f), new V2(10f, 0f), new V2(80f, 70f), new V2(-20f, 130f), new V2(220f, -90f), new V2(250f, 40f), new V2(300f, 150f), new V2(160f, -170f), new V2(140f, 190f) };
      for (int i = 0; i < spots.Length; i++) {
        V2 at = gate + new V2(Game.StampedeReach * 0.45f, 0f) + spots[i];
        float front = sz.Pos.X + Game.StampedeFace + 16f;
        if (st > Game.StampedeWind && Math.Abs(at.Y - sz.Pos.Y) < sz.Radius + 20f && at.X < front) at.X = front;  // carried ahead of the herd
        var en = (Enemy)typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(gm, new object[] { i == 7 ? EnemyType.Thunderhead : EnemyType.Grunt, at, 0f, 0, false });
        en.Hp = en.MaxHp = 1e6f; en.SpawnT = 0f; en.Pos = en.Disp = at;
      }
    }
    if (Environment.GetEnvironmentVariable("LANDSHARK") != null) {
      // A DPS Summoner's Land Shark (LandSharkLook.cs): LANDSHARK=<seconds since the cast> (the puddle spreads to 0.45 s, the fin circles
      // from 0.3, the shark leaps 0.85 to 1.35 and bites at 1.1, the puddle drains 1.6 to 2.2; use 0.6, 1.0, 1.1, 1.25), with grunts in
      // and round the puddle. LANDSHARKMASTERED=1: 130 px. Use with mode hud.
      var gm = host.Game; gm.Bot = false;
      foreach (var e in gm.Enemies.Values) e.Hp = 0f;
      gm.Bullets.Clear(); gm.Effects.Clear(); gm.Props.Clear(); gm.Layout = LayoutId.Open;
      me.Kit = Kit.Summoner; me.Role = Role.Dps; me.Def = Defs.Get(Role.Dps); me.Slots[0].Id = AbilityId.LandShark;
      V2 from = new V2(420f, 560f), at0 = new V2(720f, 420f);
      float aim = (at0 - from).Angle;
      me.Pos = me.Disp = from; me.Aim = aim; me.AimAt = at0;
      float lt = float.Parse(Environment.GetEnvironmentVariable("LANDSHARK"));
      bool cap = Environment.GetEnvironmentVariable("LANDSHARKMASTERED") != null;
      var lz = new Zone { Id = 9103, Kind = ZoneKind.LandShark, Owner = me.Id, Pos = at0, Radius = cap ? Game.LandSharkRadiusMastered : Game.LandSharkRadius, Duration = Game.LandSharkLife, Aim = aim };
      lz.T = Math.Max(0.01f, Game.LandSharkLife - lt);
      gm.Zones[lz.Id] = lz;
      V2[] spots = { new V2(-40f, -30f), new V2(35f, 25f), new V2(10f, -60f), new V2(-55f, 45f), new V2(150f, -40f), new V2(-140f, -110f), new V2(120f, 120f) };
      for (int i = 0; i < spots.Length; i++) {
        V2 at = at0 + spots[i];
        var en = (Enemy)typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(gm, new object[] { EnemyType.Grunt, at, 0f, 0, false });
        en.Hp = en.MaxHp = 1e6f; en.SpawnT = 0f; en.Pos = en.Disp = at;
      }
    }
    if (Environment.GetEnvironmentVariable("FLOOD") != null) {
      // A tank Elementalist's Flood (FloodLook.cs): FLOOD=<seconds since the cast> (the crest spreads to 0.5 s, it floods to 6 s, then drains over 0.8;
      // use 0.3, 3 or 6.4), with enemies wading in it and around it, the wading ones wet. FLOODMASTERED=1: 247 px. Use with mode hud.
      var gm = host.Game; gm.Bot = false;
      foreach (var e in gm.Enemies.Values) e.Hp = 0f;
      gm.Bullets.Clear(); gm.Effects.Clear();
      me.Kit = Kit.Elementalist; me.Role = Role.Tank; me.Def = Defs.Get(Role.Tank); me.Slots[0].Id = AbilityId.Flood;
      V2 from = new V2(700f, 480f);
      me.Pos = me.Disp = from + new V2(20f, 10f); me.Aim = -0.2f; me.AimAt = from + new V2(300f, -60f);
      float ft = float.Parse(Environment.GetEnvironmentVariable("FLOOD"));
      float R = Environment.GetEnvironmentVariable("FLOODMASTERED") != null ? Game.FloodRadiusMastered : Game.FloodRadius;
      var fz = new Zone { Id = 9101, Kind = ZoneKind.Flood, Owner = me.Id, Pos = from, Radius = R, Duration = Game.FloodTime + Game.FloodDrain };
      fz.T = Math.Max(0.01f, fz.Duration - ft);
      gm.Zones[fz.Id] = fz;
      V2[] spots = { new V2(110f, -70f), new V2(-130f, -40f), new V2(30f, 130f), new V2(150f, 80f), new V2(-80f, 120f), new V2(330f, -40f), new V2(-330f, 90f), new V2(270f, 210f) };
      for (int i = 0; i < spots.Length; i++) {
        V2 at = from + spots[i];
        var en = (Enemy)typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(gm, new object[] { i == 3 ? EnemyType.Thunderhead : i == 6 ? EnemyType.Brute : EnemyType.Grunt, at, 0f, 0, false });
        en.Hp = en.MaxHp = 1e6f; en.SpawnT = 0f; en.Pos = en.Disp = at;
        bool wading = Game.InFlood(fz, at, en.Radius);
        en.Wet = wading || i == 5; en.WetT = en.Wet ? Game.FloodWetAfter : 0f; en.WetLeft = en.WetT; en.WetPeak = Game.FloodWetAfter;
        if (i == 5) en.WetLeft = 1.4f;  // stepped out a moment ago: its bar drains
      }
    }
    if (Environment.GetEnvironmentVariable("RAIN") != null) {
      // A Hemomancer's Sanguine Rain (SanguineRainLook.cs): RAIN=<seconds since the cast> (siphon to 1.8 s, seeds to 2.4 s, then rain; use 4 or 6), with the allies
      // standing near. RAINEND=<seconds since it ended> shows the rings splashing away instead. Use with mode hud.
      var gm = host.Game; gm.Bot = false;
      foreach (var e in gm.Enemies.Values) e.Hp = 0f;
      gm.Bullets.Clear(); gm.Effects.Clear();
      me.Kit = Kit.Hemomancer; me.Role = Role.Healer; me.Def = Defs.Get(Role.Healer); me.Slots[Upgrades.UltSlot].Id = AbilityId.SanguineRain;
      V2 from = new V2(700f, 480f);
      me.Pos = me.Disp = from; me.Aim = -0.2f; me.AimAt = from + new V2(300f, -60f); me.Hp = me.MaxHp * 0.8f;
      float rt = float.Parse(Environment.GetEnvironmentVariable("RAIN"));
      me.RainClock = Math.Max(0.001f, rt); me.UltT = Math.Max(0.1f, Game.RainCast + Game.RainLen - rt);
      int ai = 0;
      foreach (var q in gm.Players.Values) if (q != me) { q.Pos = q.Disp = from + new V2(ai == 0 ? -300f : 280f, ai == 0 ? -120f : 90f); q.Hp = q.MaxHp * 0.55f; ai++; }
      if (Environment.GetEnvironmentVariable("RAINEND") != null)
        foreach (var q in gm.Players.Values) if (q != me) { me.RainClock = 0f; gm.Effects.Add(new Effect { Kind = FxKind.RainRelease, Pos = q.Pos, Pos2 = q.Pos, Color = new Rgba(0.88f, 0.13f, 0.25f), Radius = Game.RainRingR, Dur = 0.9f, T = float.Parse(Environment.GetEnvironmentVariable("RAINEND")) }); }
    }
    if (Environment.GetEnvironmentVariable("CAST") != null) {
      // An ability going off: CAST=<ability name>, CASTT=<seconds after the cast> (default 0.2). The caster stands in the middle with the enemies brought in around them; its name pops up over them.
      var gm = host.Game; gm.Bot = false;
      var aid = System.Array.Find(Upgrades.Abilities, a => a != null && a.Name == Environment.GetEnvironmentVariable("CAST")).Id;
      var adef = Upgrades.Ability(aid);
      int slot = adef.Choice == AbilityDef.Ult ? Upgrades.UltSlot : 0;
      if (adef.Choice != AbilityDef.CardOnly) {  // play the class and role it belongs to (CASTW=<n> picks the weapon)
        me.Kit = adef.Kit; me.Role = adef.Role; me.Def = Defs.Get(adef.Role);
        me.Weapon = Defs.Weapon(adef.Role, adef.Kit, int.Parse(Environment.GetEnvironmentVariable("CASTW") ?? "0"));
        me.MaxHp = me.Def.Hp * Defs.KitHpMult(adef.Kit);
      }
      me.Pos = me.Disp = new V2(800f, 450f); me.Aim = -0.4f; me.AimAt = me.Pos + V2.FromAngle(me.Aim) * 200f; me.ShieldHp = 0f; me.Hp = me.MaxHp;
      gm.Bullets.Clear();
      if (Environment.GetEnvironmentVariable("CASTCLEAN") != null) { gm.Enemies.Clear(); gm.Props.Clear(); }  // CASTCLEAN=1: an empty arena (the caster, the allies, the ability)
      int k = 0;
      foreach (var e in gm.Enemies.Values) { e.Pos = e.Disp = me.Pos + V2.FromAngle(k * 1.3f + 0.4f) * (130f + 45f * (k % 3)); k++; }
      foreach (var q in gm.Players.Values) if (q != me) { q.Pos = q.Disp = me.Pos + new V2(-260f, 120f); q.Hp = q.MaxHp * 0.5f; if (aid == AbilityId.BloodShell) q.Pos = q.Disp = me.Pos + new V2(-42f, 34f); }
      if (Environment.GetEnvironmentVariable("CASTHP") != null) me.Hp = me.MaxHp * float.Parse(Environment.GetEnvironmentVariable("CASTHP"));  // CASTHP=<share of max health left>
      if (aid == AbilityId.Bulldoze) {  // three enemies in the lane (a light one off-centre), the rest well clear of it
        V2 ld = V2.FromAngle(me.Aim), ln = ld.Orthogonal(); int li = 0;
        foreach (var e in gm.Enemies.Values) { if (li < 3) e.Pos = e.Disp = me.Pos + ld * (80f + 55f * li) + ln * (li == 1 ? 26f : -10f * li); else e.Pos = e.Disp = me.Pos + ln * (140f + 40f * (li % 3)) * (li % 2 == 0 ? 1f : -1f) + ld * (40f * li); li++; }
      }
      if (aid == AbilityId.LastStand) {  // it only stands while fighting; a volley from the front (CASTHP shows the wear, CASTT how long it has stood)
        gm.State = MatchState.Fighting; me.Aim = 0f; me.AimAt = me.Pos + new V2(200f, 0f);
        var shootM = typeof(Game).GetMethod("SpawnBullet", BindingFlags.NonPublic | BindingFlags.Instance);
        for (int b = 0; b < 14; b++) shootM.Invoke(gm, new object[] { Defs.BigShot, me.Pos + new V2(330f + 28f * b, -150f + 24f * b), new V2(-240f, 0f), 0f, 5f, 8f, 0, false, 0f });
      }
      me.Slots[slot].Id = aid; me.Slots[slot].Charges = 1;
      typeof(Game).GetMethod("HostAbility", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(gm, new object[] { 1, slot, me.AimAt, false });
      float ct = float.Parse(Environment.GetEnvironmentVariable("CASTT") ?? "0.2");
      int dartKill = Environment.GetEnvironmentVariable("DARTKILL") != null ? (int)(60f * float.Parse(Environment.GetEnvironmentVariable("DARTKILL"))) : -1;  // DARTKILL=<s>: Poison Dart's target dies then (the poison spreads)
      for (int f = 0; f < 60 * ct; f++) {
        if (f == dartKill) foreach (var e in gm.Enemies.Values) if (e.VenomT > 0f) e.Hp = 0f;
        me.Pos = new V2(800f, 450f); me.InvulnT = 1f; gm.FixedStep(1f / 60f, new PlayerInput { AimAt = me.AimAt }); gm.Update(1f / 60f);
      }
      me.Disp = me.Pos;
      if (aid == AbilityId.Bulldoze) me.Disp = me.Pos + V2.FromAngle(me.Aim) * System.Math.Min(200f, 750f * System.Math.Min(ct, 0.35f));  // the charge itself is the player's own game; show how far it has got
      foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    }
    if (Environment.GetEnvironmentVariable("TWINNADES") != null) {  // TWINNADES=<loaded>,<next tube 0/1>,<charge>: the Twin Rifle's grenade tubes (TwinGrenades.cs)
      var tn = Environment.GetEnvironmentVariable("TWINNADES").Split(',');
      me.Weapon = Defs.Weapon(Role.Dps, Kit.Soldier, 0); me.TwinNades = byte.Parse(tn[0]); me.TwinNext = byte.Parse(tn[1]); me.TwinCharge = byte.Parse(tn[2]);
    }
    if (Environment.GetEnvironmentVariable("REVOLVER") != null) { me.Weapon = Defs.Weapon(Role.Healer, Kit.Soldier, 1); me.RevCocked = Environment.GetEnvironmentVariable("REVOLVER") != "pull"; me.RevCockT = me.RevCocked ? 0f : 0.07f; me.RevStance = (byte)(Environment.GetEnvironmentVariable("REVOLVER") == "fan" ? 1 : 0); host.Game.Bot = false; me.Pos = me.Disp = new V2(800f, 450f); }  // REVOLVER=1 cocked, pull: the hammer coming back, fan: the Fan stance
  }
  // KNWHEEL=r|l|u|d|c: your Arc Knuckles' click wheel open up and to the right of you, the cursor on that side of it (c: its centre).
  static void KnuckleWheelScene() {
    string kw = Environment.GetEnvironmentVariable("KNWHEEL");
    if (kw == null) return;
    var me = host.Game.Me;
    float ws = 1280f / Game.Width;
    V2 at = me.Pos + new V2(150f, -60f), c = new V2(at.X * ws, 720f - at.Y * ws);
    set("knWheelOpen", true);
    set("knWheelAt", c);
    V2 off = kw == "r" ? new V2(40f, 0f) : kw == "l" ? new V2(-40f, 0f) : kw == "u" ? new V2(0f, 40f) : kw == "d" ? new V2(0f, -40f) : V2.Zero;
    Input.mousePosition = new Vector3(c.X + off.X, c.Y + off.Y, 0f);
  }
  static void ArenaScenes() {
    KnuckleWheelScene();  // KNWHEEL=r|l|u|d|c: the Arc Knuckles' click wheel open, the cursor on that side (with SWINGW=ArcKnuckles MOVE=<1-7>)
    OverlaysScene();  // OVERLAYS=<role>|all [OVERBARS=1] (ShotOverlays.cs)
    DuelistSheetScene();  // DUELSHEET=<1-8>|all (ShotDuelist.cs)
    HoldingCellScene();  // HOLDCELL=<s> [HOLDCELLS=2], HOLDLOCK=1, HOLDNET=<s> (ShotHoldingCell.cs)
    ShockwaveScene();  // SHOCK=<s since the cast> [SHOCKCAP=1] [FXOFF=1] (ShotShockwave.cs)
    CereScene();  // CERE=<plates>,<s since the last cast>[,1 hurt] (ShotCeremonial.cs)
    SenbonScene();  // SENBON=<needles>[,<s since the last cast>[,<enemies>]] [SENBONCAP=1] (ShotSenbon.cs)
    HeliScene();  // HELI=<s since the cast> [HELICAP=1] [FXOFF=1] (ShotHeli.cs)
    OrbitalScene();  // ORBITAL=<s since the cast> [ORBCAP=1] [FXOFF=1] (ShotOrbital.cs)
    TankLookScene();  // TANKLOOK=<-1|0-2> (ShotTankLook.cs)
    LanceRainScene();  // LANCERAIN=<s since the cast> [LRCAP=1] [FXOFF=1] (ShotLanceRain.cs)
    TankScene();  // TANK=<s since the cast> [TANKCAP=1] [FXOFF=1] (ShotTank.cs)
    HorseScene();  // HORSE=<s since the cast> [HORSEALL=1] [HORSEWEAR=<0-1>] [HORSEEND=broke|sally] (ShotTrojanHorse.cs)
    ArmadilloScene();  // ARMADILLO=<s since the cast> [ARMREV=<s>] [FXOFF=1] (ShotArmadillo.cs)
    SupplyScene();  // SUPPLY=<s since the cast> [SUPPLYCAP=1] [FXOFF=1] (ShotSupply.cs)
    ArmorDropScene();  // ARMORDROP=<s since the cast> [ARMORCAP=1] [ARMORHIT=<armor>] [FXOFF=1] (ShotArmorDrop.cs)
    HospitalScene();  // HOSP=pick|<s since the cast> [HOSPTENT=1] (ShotHospital.cs)
    BallistaScene();  // BALLISTA=<s since the cast> [BALLISTACAP=1] [BALLISTARIDE=<s>|-1] [FXOFF=1] (ShotBallista.cs)
    DeployTurretScene();  // DEPLOYTURRET=<s since the cast> [FXOFF=1] (ShotDeployTurret.cs)
    NeedleSwarmScene();  // NEEDLESWARM=<s held> [NSALT=<s>] (ShotNeedleSwarm.cs)
    OfudaScene();  // OFUDA=<s> (ShotOfuda.cs)
    NeedlesScene();  // NEEDLES=<s> (ShotNeedles.cs)
    ThornBloomScene();  // THORNBLOOM=<s since the cast> [THORNCAP=1] (ShotThornBloom.cs)
    CannonCallScene();  // CANNON=<s since the cast> [CANNONCAP=1] (ShotCannonCall.cs)
    BambooGroveScene();  // BAMBOO=<s since the cast> [BAMBOOCAP=1] (ShotBambooGrove.cs)
    SiegeBellScene();  // BELL=<s since the cast> [BELLCAP=1] (ShotSiegeBell.cs)
    BloodTotemScene();  // TOTEM=<s since the last plant> [TOTEMCAP=1] (ShotBloodTotem.cs)
    ChakraScene();  // CHAKRA=<s since the cast> [CHAKRACAP=1] (ShotChakra.cs)
    OctopusScene();  // OCTO=<s since the cast> [OCTOHP=<0-1>] or OCTOPICK=1 (ShotOctopus.cs)
    FlashFireScene();  // FLASHFIRE=<s since the cast> [FLASHCAP=1] (ShotFlashFire.cs)
    RockGlovesScene();  // GLOVES=<s since the cast> [GLOVESIDLE=1] (ShotRockGloves.cs)
    BloodRavagerScene();  // RAVAGER=<s since the cast> [RAVAGERCLICK=0] [RAVAGERSTILL=1] (ShotBloodRavager.cs)
    BloodBoilScene();  // BOIL=<s since the cast> [BOILHP=<0-1>] [BOILEND=1] (ShotBloodBoil.cs)
    RodentMinigunScene();  // RODENT=<s since the cast> [RODENTAIM=<deg>] (ShotRodentMinigun.cs)
    MammothFallScene();  // MAMMOTH=<s since the cast> (ShotMammothFall.cs)
    RocketDropScene();  // ROCKETDROP=<s since the cast> [ROCKETCAP=1]: the DPS Soldier's Rocket Drop (ShotRocketDrop.cs)
    HazardFoamScene();  // FOAM=<s since the cast> [FOAMCAP=1] [FOAMFIZZ=1]: the Healer Soldier's Hazard Foam (ShotHazardFoam.cs)
    BloodRitualScene();  // RITUAL=<s since the cast> [RITUALCAP=1]: the DPS Hemomancer's Blood Ritual (ShotBloodRitual.cs)
    KnightBannersScene();  // BANNER=aegis|pilgrim|war [BANNERT=<s>] [BANNERCAP=1]: the Knight banner ultimates (ShotKnightBanners.cs)
    TuskerScene();  // TUSKER=<s since the cast> [TUSKERCAP=1]: the Tank Summoner's Tidal Tusker (ShotTusker.cs)
    ForbiddenPathScene();  // FORBIDDEN=<s since the cast> (ShotForbiddenPath.cs)
    HoneyRaidScene();  // HONEY=<s since the cast> [HONEYCAP=1]: the Healer Summoner's Honey Raid (ShotHoneyRaid.cs)
    TortoiseOracleScene();  // ORACLE=<s since the cast> [ORACLECAP=1]: the Healer Summoner's Tortoise Oracle (ShotTortoiseOracle.cs)
    HeartbeatScene();  // HEARTBEAT=<s since the cast>: the Healer Hemomancer's Heartbeat (ShotHeartbeat.cs)
    SwarmHeartScene();  // SWARMHEART=<s since the cast>: the Healer Hemomancer's Heart of the Swarm (ShotSwarmHeart.cs)
    ConduitWebScene();  // WEB=<s since the cast> [WEBMASTERED=1]: the Healer Elementalist's Conduit Web (ShotConduitWeb.cs)
    EyeOfCalmScene();  // EYE=<s since the cast> [EYEMASTERED=1] [EYEGUST=<s>]: the Healer Elementalist's Eye of Calm (ShotEyeOfCalm.cs)
    StaticBubbleScene();  // BUBBLE=<s since the cast> [BUBFILL=<0-1>] [BUBBURST=<s>]: the Healer Elementalist's Static Bubble (ShotStaticBubble.cs)
    ShadowGongScene();  // GONG=<s since the cast>: the Tank Ninja's Five Shadow Gong (ShotShadowGong.cs)
    PressurePointScene();  // PRESS=<s since the cast>: the Healer Ninja's Pressure Point Press (ShotPressurePoint.cs)
    AstralScene();  // ASTRAL=<s since the cast>: the Healer Ninja's Astral Projection, Fire held (ShotAstral.cs)
    SpiritAvatarScene();  // SPIRIT=<s since the cast>: the Tank Ninja's Spirit Avatar, Fire held (ShotSpiritAvatar.cs)
    DragonsDogmaScene();  // DOGMA=<s since the cast>: MOCKUP of the Tank Ninja's Dragon's Dogma (ShotDragonsDogma.cs)
    RedShotScene();  // REDSHOT=1: the red boss shots, barbed spears (ShotRedShot.cs)
    ChalkFireScene();  // CHALKFIRE=A|B|C (ShotChalkFire.cs)
    FlameWallScene();  // FLAMEWALL=<s since the cast> [FXOFF=1] (ShotFlameWall.cs)
    LightingScene();  // LIGHTING=1|2|3 [LIGHTS=0] [LIGHTSOFT=0|1] [FLOORDIM=0-2] (ShotLighting.cs)
    LightPerf();  // LIGHTPERF=<enemies>,<shots>[,<blasts>] (ShotLighting.cs)
    PhoenixScene();  // PHOENIX=<s since the cast> (ShotPhoenix.cs)
    CallingsScene();  // CALLINGS=<s>: the Summoner's Calling minions (ShotCallings.cs)
    SentryNovaScene();  // TURRET=<s> or NOVA=<s> since the cast [ABILCAP=1] [TURRETEND=1] [FXOFF=1] (ShotSentryNova.cs)
    JuiceMomentScene();  // MOMENT=multi|clear|level|lowhp|marks [MOMENTN] [MOMENTT] [MULTISTYLE=1] (ShotJuice.cs)
    BowFocusScene();  // BOWFOCUS=rest|draw|bar|focus|miss|swing|scatter,<s> (ShotBowFocus.cs)
    LagScene();  // LAG=pill|chart|guest [LAGFROZEN=1] (ShotLag.cs)
    BossGateScene();  // GATE=<fill> [GATEIN=<players in>] [GATEAGE=<s>] (ShotBossGate.cs)
    GL.Additive = false;  // a scene that drew the world ahead (its glow pass) leaves the stub's blend additive; the shot itself starts plain, as Unity does
    // NADET=<s>: the Twin Rifle's Rocket assist (TwinNadeLook.cs), a grenade fired from the left tube that long ago (aiming
    // right, the arena otherwise empty). NADELOAD=<s>: instead, the left tube loading that long ago (its breech and shell).
    // RIFLEFIRE=1 holds fire as well (NADET=0.4 RIFLEFIRE=1 shows the rifles mid-burst); CROSSAT=<px> sets the cursor distance.
    // UNDERTOW=<meter>,<s held>: the Repulsion Cannon's Undertow (Repulsion.cs, RepulseLook.cs): the meter at that much, then
    // Alt held that long (0 = not pulling, just the meter) on a pack of grunts in the cone.
    if (Environment.GetEnvironmentVariable("UNDERTOW") != null) {
      var ci = System.Globalization.CultureInfo.InvariantCulture;
      var ut = Environment.GetEnvironmentVariable("UNDERTOW").Split(',');
      var gm = host.Game; gm.Bot = false; gm.Bullets.Clear(); gm.Effects.Clear(); gm.Enemies.Clear();
      me.Role = Role.Healer; me.Kit = Kit.Soldier; me.Def = Defs.Get(Role.Healer); me.Weapon = Defs.Weapon(Role.Healer, Kit.Soldier, 0); me.MaxHp = me.Hp = me.Def.Hp; foreach (var sl in me.Slots) sl.Id = AbilityId.None; foreach (var q0 in gm.Players.Values) { q0.AegisT = 0f; q0.UltT = 0f; }
      V2 home = new V2(450f, 450f); me.Pos = me.Disp = home; me.Aim = 0f;
      var spawnAt = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      foreach (var at0 in new[] { new V2(700f, 360f), new V2(680f, 430f), new V2(730f, 470f), new V2(660f, 520f), new V2(740f, 550f), new V2(620f, 330f) }) {
        var e = (Enemy)spawnAt.Invoke(gm, new object[] { EnemyType.Grunt, at0, 0f, 0, false }); e.Speed = 0f; e.FireT = 99f; e.SpawnT = 0f; e.MaxHp = e.Hp = 1e5f; }
      typeof(Game).GetMethod("SetRepelMeter", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { me, float.Parse(ut[0], ci) });
      float held = ut.Length > 1 ? float.Parse(ut[1], ci) : 0f;
      V2 aimAt = home + new V2(300f, 0f);
      var draw = typeof(ArenaApp).GetMethod("DrawWorld", f2);
      int frames = Math.Max(6, (int)Math.Round(held * 60f));
      for (int f = 0; f < frames; f++) {
        me.Pos = me.Disp = home; me.Aim = 0f; Time.unscaledDeltaTime = 1f / 60f; Time.unscaledTime += 1f / 60f;
        gm.FixedStep(1f / 60f, new PlayerInput { AimAt = aimAt, WeaponAction = held > 0f });
        me.Disp = me.Pos; me.Aim = 0f; foreach (var e in gm.Enemies.Values) e.Disp = e.Pos; draw.Invoke(app, null);
      }
      GL.Tris.Clear(); GUI.Log.Clear();
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
      foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
    }
    if (Environment.GetEnvironmentVariable("NADET") != null || Environment.GetEnvironmentVariable("NADELOAD") != null) {
      var ci = System.Globalization.CultureInfo.InvariantCulture;
      var gm = host.Game; gm.Bot = false; gm.Bullets.Clear(); gm.Effects.Clear(); gm.Enemies.Clear();
      me.Role = Role.Dps; me.Kit = Kit.Soldier; me.Def = Defs.Get(Role.Dps); me.Weapon = Defs.Weapon(Role.Dps, Kit.Soldier, 0); me.MaxHp = me.Hp = me.Def.Hp; foreach (var sl in me.Slots) sl.Id = AbilityId.None; foreach (var q0 in gm.Players.Values) { q0.AegisT = 0f; q0.UltT = 0f; }
      V2 home = new V2(450f, 330f); me.Pos = me.Disp = home; me.Aim = 0f;
      bool load = Environment.GetEnvironmentVariable("NADELOAD") != null;
      me.TwinNades = (byte)(load ? 0 : 2); me.TwinNext = 0;
      bool fire = Environment.GetEnvironmentVariable("RIFLEFIRE") != null;  // RIFLEFIRE=1: holding fire too (Spread arms, DpsGunLooks.cs)
      V2 aimAt = home + new V2(float.Parse(Environment.GetEnvironmentVariable("CROSSAT") ?? "320", ci), 0f);
      var draw = typeof(ArenaApp).GetMethod("DrawWorld", f2);
      Action frame = () => { me.Pos = me.Disp = home; me.Aim = 0f; Time.unscaledDeltaTime = 1f / 60f; Time.unscaledTime += 1f / 60f; gm.FixedStep(1f / 60f, new PlayerInput { AimAt = aimAt, Fire = fire }); me.Disp = me.Pos; me.Aim = 0f; draw.Invoke(app, null); };
      for (int f = 0; f < 6; f++) frame();
      if (load) me.TwinNades = 1;
      else typeof(Game).GetMethod("HostTwinGrenade", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(gm, new object[] { me, aimAt });
      float at = float.Parse(Environment.GetEnvironmentVariable(load ? "NADELOAD" : "NADET"), ci);
      for (int f = 0; f < Math.Round(at * 60f); f++) { if (load) me.TwinNades = 1; frame(); }
      GL.Tris.Clear(); GUI.Log.Clear();  // (only the mode's own draw goes in the picture)
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    }
    // TWINAUTO=<s>: the Soldier DPS's Twin Rifle Auto (Core/TwinAuto.cs, TwinAutoLook.cs) that long into a fight: two grunts either side of
    // you and a third further out, aiming right, nobody pulling the trigger. TWINLOCK=1: Alt pressed 0.3 s in, locking the one lower down
    // (the cursor sits by it). Needs a few seconds for the guns to get going.
    if (Environment.GetEnvironmentVariable("TWINAUTO") != null) {
      var ci = System.Globalization.CultureInfo.InvariantCulture;
      var gm = host.Game; gm.Bot = false; gm.Bullets.Clear(); gm.Effects.Clear(); gm.Enemies.Clear();
      me.Role = Role.Dps; me.Kit = Kit.Soldier; me.Def = Defs.Get(Role.Dps); me.Weapon = Defs.Weapon(Role.Dps, Kit.Soldier, 4); me.MaxHp = me.Hp = me.Def.Hp; foreach (var sl in me.Slots) sl.Id = AbilityId.None; foreach (var q0 in gm.Players.Values) { q0.AegisT = 0f; q0.UltT = 0f; }
      V2 home = new V2(450f, 330f); me.Pos = me.Disp = home; me.Aim = 0f;
      var spawnAt = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      var foes = new System.Collections.Generic.List<Enemy>();
      foreach (var at0 in new[] { new V2(620f, 200f), new V2(600f, 470f), new V2(760f, 390f) }) {
        var e = (Enemy)spawnAt.Invoke(gm, new object[] { EnemyType.Grunt, at0, 0f, 0, false }); e.Speed = 0f; e.FireT = 99f; e.SpawnT = 0f; e.MaxHp = e.Hp = 1e6f; e.Disp = e.Pos; foes.Add(e); }
      bool lockIt = Environment.GetEnvironmentVariable("TWINLOCK") != null;
      var draw = typeof(ArenaApp).GetMethod("DrawWorld", f2);
      int frames = Math.Max(6, (int)Math.Round(float.Parse(Environment.GetEnvironmentVariable("TWINAUTO"), ci) * 60f));
      for (int f = 0; f < frames; f++) {
        me.Pos = me.Disp = home; Time.unscaledDeltaTime = 1f / 60f; Time.unscaledTime += 1f / 60f;
        V2 aimAt = lockIt && f >= 18 ? foes[1].Pos : home + new V2(300f, 0f);
        me.Aim = (aimAt - home).Angle;
        gm.FixedStep(1f / 60f, new PlayerInput { AimAt = aimAt, WeaponAction = lockIt && f >= 18 && f < 21 });
        me.Disp = me.Pos = home; me.Aim = (aimAt - home).Angle; foreach (var e in foes) e.Disp = e.Pos; draw.Invoke(app, null);
      }
      GL.Tris.Clear(); GUI.Log.Clear();
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    }
    // WHIP=<s>: the Revolver's Fan stance (Core/PistolWhip.cs, Game/PistolWhipLook.cs) that long into a fight: grunts in the arc and a
    // spray of small and big enemy shots flying in, a click 0.1 s in (the Pistol Whip; WHIPIDLE=1: no click; WHIPFAN=1: a second
    // click 0.3 s in, Fan the Hammer).
    if (Environment.GetEnvironmentVariable("WHIP") != null) {
      var ci = System.Globalization.CultureInfo.InvariantCulture;
      var gm = host.Game; gm.Bot = false; gm.Bullets.Clear(); gm.Effects.Clear(); gm.Enemies.Clear();
      me.Role = Role.Healer; me.Kit = Kit.Soldier; me.Def = Defs.Get(Role.Healer); me.Weapon = Defs.WeaponOf(WeaponId.Revolver); me.MaxHp = me.Hp = me.Def.Hp; foreach (var sl in me.Slots) sl.Id = AbilityId.None; foreach (var q0 in gm.Players.Values) { q0.AegisT = 0f; q0.UltT = 0f; }
      V2 home = new V2(420f, 330f); me.Pos = me.Disp = home; me.Aim = 0f;
      var spawnAt = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      var shoot = typeof(Game).GetMethod("SpawnBullet", BindingFlags.NonPublic | BindingFlags.Instance);
      foreach (var at in new[] { new V2(95f, -55f), new V2(110f, 20f), new V2(60f, 85f) }) {
        var e = (Enemy)spawnAt.Invoke(gm, new object[] { EnemyType.Grunt, home + at, 0f, 0, false }); e.Speed = 0f; e.FireT = 99f; e.SpawnT = 0f; e.MaxHp = e.Hp = 1e6f; e.Disp = e.Pos; }
      for (int i = 0; i < 9; i++) {
        float a = -1.3f + 2.6f * i / 8f; V2 from = home + V2.FromAngle(a) * (175f + 20f * (i % 3));
        shoot.Invoke(gm, new object[] { i % 4 == 0 ? Defs.BigShot : Defs.SmallShot, from, (home - from).Normalized() * 160f, 0f, 5f, 10f, -1, false, 0f }); }
      bool idle = Environment.GetEnvironmentVariable("WHIPIDLE") != null, fanToo = Environment.GetEnvironmentVariable("WHIPFAN") != null;
      me.RevStance = 1;
      var draw = typeof(ArenaApp).GetMethod("DrawWorld", f2);
      int frames = Math.Max(1, (int)Math.Round(float.Parse(Environment.GetEnvironmentVariable("WHIP"), ci) * 60f));
      for (int f = 0; f < frames; f++) {
        me.Pos = me.Disp = home; me.Aim = 0f; Time.unscaledDeltaTime = 1f / 60f; Time.unscaledTime += 1f / 60f;
        gm.FixedStep(1f / 60f, new PlayerInput { AimAt = home + new V2(300f, 0f), Fire = !idle && (f >= 6 && f < 9 || fanToo && f >= 18 && f < 21) }); gm.Update(1f / 60f);
        me.Disp = me.Pos = home; me.Aim = 0f; me.Hp = me.MaxHp; foreach (var e in gm.Enemies.Values) e.Disp = e.Pos; draw.Invoke(app, null);
      }
      GL.Tris.Clear(); GUI.Log.Clear();
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    }
    // DOUBLEBARREL=<s>: the Soldier DPS's Double Barrel (Core/DoubleBarrel.cs, Game/DoubleBarrelLook.cs) that long into a fight: three dummies in a line,
    // fire held (DBBOTH=1: Alt pressed 0.3 s in, both barrels at once; DBIDLE=1: not firing, the model at rest).
    if (Environment.GetEnvironmentVariable("DOUBLEBARREL") != null) {
      var ci = System.Globalization.CultureInfo.InvariantCulture;
      var gm = host.Game; gm.Bot = false; gm.Bullets.Clear(); gm.Effects.Clear(); gm.Enemies.Clear();
      me.Role = Role.Dps; me.Kit = Kit.Soldier; me.Def = Defs.Get(Role.Dps); me.Weapon = Defs.Weapon(Role.Dps, Kit.Soldier, int.Parse(Environment.GetEnvironmentVariable("DBWEAPON") ?? "3")); me.MaxHp = me.Hp = me.Def.Hp; foreach (var sl in me.Slots) sl.Id = AbilityId.None; foreach (var q0 in gm.Players.Values) { q0.AegisT = 0f; q0.UltT = 0f; }
      V2 home = new V2(450f, 330f); me.Pos = me.Disp = home; me.Aim = 0f;
      var spawnAt = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      var foes = new System.Collections.Generic.List<Enemy>();
      foreach (var dx in new[] { 90f, 150f, 240f }) {
        var e = (Enemy)spawnAt.Invoke(gm, new object[] { EnemyType.Grunt, home + new V2(dx, 0f), 0f, 0, false }); e.Speed = 0f; e.FireT = 99f; e.SpawnT = 0f; e.MaxHp = e.Hp = 1e6f; e.Disp = e.Pos; foes.Add(e); }
      bool both = Environment.GetEnvironmentVariable("DBBOTH") != null, idle = Environment.GetEnvironmentVariable("DBIDLE") != null;
      var draw = typeof(ArenaApp).GetMethod("DrawWorld", f2);
      int frames = Math.Max(6, (int)Math.Round(float.Parse(Environment.GetEnvironmentVariable("DOUBLEBARREL"), ci) * 60f));
      for (int f = 0; f < frames; f++) {
        me.Pos = me.Disp = home; me.Aim = 0f; foreach (var e in foes) e.Pos = e.Disp = new V2(e.Pos.X, home.Y); Time.unscaledDeltaTime = 1f / 60f; Time.unscaledTime += 1f / 60f;
        gm.FixedStep(1f / 60f, new PlayerInput { AimAt = home + new V2(300f, 0f), Fire = !idle && !both, WeaponAction = both && f >= 18 && f < 21 });
        me.Disp = me.Pos = home; me.Aim = 0f; foreach (var e in foes) e.Disp = e.Pos; draw.Invoke(app, null);
      }
      GL.Tris.Clear(); GUI.Log.Clear();
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    }
    // STATICSHOCK=<s>: the Healer Elementalist's Static Shock (Core/StaticShock.cs, Game/StaticShockLook.cs) that long into a fight: fire
    // held at a pack of grunts ahead, the other players beside the cone (one inside it gets Overcharge). SSALT=1: Alt (Arc Cannon) pressed at
    // 0.1 s through a teammate toward the pack, no Fire. Run with 3 players (shot.sh x hud 3 1 3).
    if (Environment.GetEnvironmentVariable("STATICSHOCK") != null) {
      var ci = System.Globalization.CultureInfo.InvariantCulture;
      var gm = host.Game; gm.Bot = false; gm.Bullets.Clear(); gm.Effects.Clear(); gm.Enemies.Clear(); gm.Orbs.Clear();
      me.Role = Role.Healer; me.Kit = Kit.Elementalist; me.Def = Defs.Get(Role.Healer); me.Weapon = Defs.Weapon(Role.Healer, Kit.Elementalist, 3); me.MaxHp = me.Hp = me.Def.Hp; foreach (var sl in me.Slots) sl.Id = AbilityId.None; foreach (var q0 in gm.Players.Values) { q0.AegisT = 0f; q0.UltT = 0f; q0.OverchargeT = 0f; }
      bool alt = Environment.GetEnvironmentVariable("SSALT") != null;
      V2 home = new V2(380f, 330f); me.Pos = me.Disp = home; me.Aim = 0f;
      var mates = new System.Collections.Generic.List<Player>();
      foreach (var q in gm.Players.Values) if (q != me) mates.Add(q);
      var mateAt = alt ? new[] { new V2(170f, 10f), new V2(120f, 130f) } : new[] { new V2(120f, 60f), new V2(60f, -150f) };
      for (int i = 0; i < mates.Count && i < 2; i++) { mates[i].Pos = mates[i].Disp = home + mateAt[i]; mates[i].Firing = false; }
      var spawnAt = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      var foes = new System.Collections.Generic.List<Enemy>();
      var spots = alt ? new[] { new V2(330f, -40f), new V2(360f, 30f), new V2(300f, 60f), new V2(410f, -10f), new V2(220f, -160f) }
                      : new[] { new V2(90f, -30f), new V2(150f, 10f), new V2(175f, -55f), new V2(205f, 40f), new V2(320f, -20f), new V2(260f, 150f) };
      foreach (var at in spots) {
        var e = (Enemy)spawnAt.Invoke(gm, new object[] { EnemyType.Grunt, home + at, 0f, 0, false }); e.Speed = 0f; e.FireT = 99f; e.SpawnT = 0f; e.MaxHp = e.Hp = 1e6f; e.Disp = e.Pos; foes.Add(e); }
      var keep = new V2[foes.Count];
      for (int i = 0; i < foes.Count; i++) keep[i] = foes[i].Pos;
      var draw = typeof(ArenaApp).GetMethod("DrawWorld", f2);
      int frames = Math.Max(2, (int)Math.Round(float.Parse(Environment.GetEnvironmentVariable("STATICSHOCK"), ci) * 60f));
      V2 cursor = alt ? home + new V2(360f, 10f) : home + new V2(260f, 0f);
      if (Environment.GetEnvironmentVariable("SSHEAT") != null) {  // SSHEAT=<s>: every enemy has already been in the cone that long (the shock's ramp)
        float heat0 = float.Parse(Environment.GetEnvironmentVariable("SSHEAT"), ci);
        var hostT = (System.Collections.Generic.Dictionary<int, float>)typeof(Game).GetField("staticShockT", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(gm);
        var lookT = (System.Collections.Generic.Dictionary<long, float>)typeof(ArenaApp).GetField("shkHeat", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(app);
        foreach (var e in foes) { hostT[e.Id] = heat0; lookT[e.Id] = heat0; }
      }
      for (int f = 0; f < frames; f++) {
        me.Pos = me.Disp = home; me.Aim = 0f; for (int i = 0; i < foes.Count; i++) { foes[i].Pos = foes[i].Disp = keep[i]; foes[i].FireT = 99f; }
        for (int i = 0; i < mates.Count && i < 2; i++) { mates[i].Pos = mates[i].Disp = home + mateAt[i]; mates[i].Hp = mates[i].MaxHp; }
        Time.unscaledDeltaTime = 1f / 60f; Time.unscaledTime += 1f / 60f;
        gm.FixedStep(1f / 60f, new PlayerInput { AimAt = cursor, Fire = !alt, WeaponAction = alt && f >= 6 && f < 9 });
        gm.Update(1f / 60f);
        me.Disp = me.Pos = home; me.Aim = 0f; me.AimAt = cursor; me.Hp = me.MaxHp; for (int i = 0; i < foes.Count; i++) foes[i].Disp = foes[i].Pos = keep[i];
        for (int i = 0; i < mates.Count && i < 2; i++) mates[i].Pos = mates[i].Disp = home + mateAt[i];
        draw.Invoke(app, null);
      }
      GL.Tris.Clear(); GUI.Log.Clear();
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    }
    // HIVECENSER=<s>: the Healer Summoner's Hive Censer (Core/HiveCenser.cs, Game/HiveCenserLook.cs) that long into a scripted pour: Fire
    // held on a pack of grunts ahead (the cursor sweeping across it) until 1.2 s, then beside a teammate below until 2.0 s (a swarm shells
    // them in chitin), then back on the pack. HCALT=1: Hive Call (Alt) at 2.4 s toward the second teammate. Run with 3 players
    // (shot.sh x hud 3 1 3).
    if (Environment.GetEnvironmentVariable("HIVECENSER") != null) {
      var ci = System.Globalization.CultureInfo.InvariantCulture;
      var gm = host.Game; gm.Bot = false; gm.Bullets.Clear(); gm.Effects.Clear(); gm.Enemies.Clear(); gm.Orbs.Clear();
      me.Role = Role.Healer; me.Kit = Kit.Summoner; me.Def = Defs.Get(Role.Healer); me.Weapon = Defs.Weapon(Role.Healer, Kit.Summoner, 3); me.MaxHp = me.Hp = me.Def.Hp; foreach (var sl in me.Slots) sl.Id = AbilityId.None; foreach (var q0 in gm.Players.Values) { q0.AegisT = 0f; q0.UltT = 0f; q0.ChitinHp = 0f; }
      bool call = Environment.GetEnvironmentVariable("HCALT") != null;
      V2 home = new V2(420f, 420f); me.Pos = me.Disp = home; me.Aim = 0f;
      var mates = new System.Collections.Generic.List<Player>();
      foreach (var q in gm.Players.Values) if (q != me) mates.Add(q);
      var mateAt = new[] { new V2(-40f, 230f), new V2(200f, 300f) };
      for (int i = 0; i < mates.Count && i < 2; i++) { mates[i].Pos = mates[i].Disp = home + mateAt[i]; mates[i].Firing = false; }
      var spawnAt = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      var foes = new System.Collections.Generic.List<Enemy>();
      foreach (var at in new[] { new V2(300f, -70f), new V2(340f, 10f), new V2(290f, 70f), new V2(400f, -30f), new V2(260f, -160f) }) {
        var e = (Enemy)spawnAt.Invoke(gm, new object[] { EnemyType.Grunt, home + at, 0f, 0, false }); e.Speed = 0f; e.FireT = 99f; e.SpawnT = 0f; e.MaxHp = e.Hp = 1e6f; e.Disp = e.Pos; foes.Add(e); }
      var keep = new V2[foes.Count];
      for (int i = 0; i < foes.Count; i++) keep[i] = foes[i].Pos;
      var draw = typeof(ArenaApp).GetMethod("DrawWorld", f2);
      int frames = Math.Max(2, (int)Math.Round(float.Parse(Environment.GetEnvironmentVariable("HIVECENSER"), ci) * 60f));
      for (int f = 0; f < frames; f++) {
        float tt = f / 60f;
        V2 cursor = tt < 1.2f ? home + new V2(250f + 90f * (float)Math.Sin(tt * 4f), -90f + 120f * tt)
                  : tt < 2.0f ? home + mateAt[0] + new V2(70f, -20f)
                  : home + new V2(230f + 40f * (float)Math.Sin(tt * 5f), 40f * (float)Math.Cos(tt * 4f));
        if (call && tt >= 2.4f) cursor = home + mateAt[1];
        bool fire = !(call && tt >= 2.4f);
        me.Pos = me.Disp = home; me.Aim = (cursor - home).Angle; for (int i = 0; i < foes.Count; i++) { foes[i].Pos = foes[i].Disp = keep[i]; foes[i].FireT = 99f; }
        for (int i = 0; i < mates.Count && i < 2; i++) { mates[i].Pos = mates[i].Disp = home + mateAt[i]; mates[i].Hp = mates[i].MaxHp; }
        Time.unscaledDeltaTime = 1f / 60f; Time.unscaledTime += 1f / 60f;
        gm.FixedStep(1f / 60f, new PlayerInput { AimAt = cursor, Fire = fire, WeaponAction = call && f >= 144 && f < 147 });
        gm.Update(1f / 60f);
        me.Disp = me.Pos = home; me.AimAt = cursor; me.Hp = me.MaxHp; for (int i = 0; i < foes.Count; i++) foes[i].Disp = foes[i].Pos = keep[i];
        for (int i = 0; i < mates.Count && i < 2; i++) mates[i].Pos = mates[i].Disp = home + mateAt[i];
        draw.Invoke(app, null);
      }
      GL.Tris.Clear(); GUI.Log.Clear();
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    }
    // TWINPISTOLS=<s>: the Healer Soldier's Twin Pistols (Core/TwinPistols.cs, Game/TwinPistolsLook.cs) that long into a fight: fire held at a
    // pack of grunts 260 px ahead (TPLASER=1: Alt pressed 0.1 s in, Laser Sights on; TPIDLE=1: not firing).
    if (Environment.GetEnvironmentVariable("TWINPISTOLS") != null) {
      var ci = System.Globalization.CultureInfo.InvariantCulture;
      var gm = host.Game; gm.Bot = false; gm.Bullets.Clear(); gm.Effects.Clear(); gm.Enemies.Clear(); gm.Orbs.Clear();
      me.Role = Role.Healer; me.Kit = Kit.Soldier; me.Def = Defs.Get(Role.Healer); me.Weapon = Defs.Weapon(Role.Healer, Kit.Soldier, 3); me.MaxHp = me.Hp = me.Def.Hp; foreach (var sl in me.Slots) sl.Id = AbilityId.None; foreach (var q0 in gm.Players.Values) { q0.AegisT = 0f; q0.UltT = 0f; }
      V2 home = new V2(420f, 330f); me.Pos = me.Disp = home; me.Aim = 0f; me.TpLaserT = me.TpCd = 0f;
      var spawnAt = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      var foes = new System.Collections.Generic.List<Enemy>();
      foreach (var at in new[] { new V2(250f, -45f), new V2(275f, 10f), new V2(240f, 55f), new V2(330f, -5f) }) {
        var e = (Enemy)spawnAt.Invoke(gm, new object[] { EnemyType.Grunt, home + at, 0f, 0, false }); e.Speed = 0f; e.FireT = 99f; e.SpawnT = 0f; e.MaxHp = e.Hp = 1e6f; e.Disp = e.Pos; foes.Add(e); }
      bool laser = Environment.GetEnvironmentVariable("TPLASER") != null, idle = Environment.GetEnvironmentVariable("TPIDLE") != null;
      var keep = new V2[foes.Count];
      for (int i = 0; i < foes.Count; i++) keep[i] = foes[i].Pos;
      var draw = typeof(ArenaApp).GetMethod("DrawWorld", f2);
      int frames = Math.Max(2, (int)Math.Round(float.Parse(Environment.GetEnvironmentVariable("TWINPISTOLS"), ci) * 60f));
      for (int f = 0; f < frames; f++) {
        me.Pos = me.Disp = home; me.Aim = 0f; for (int i = 0; i < foes.Count; i++) foes[i].Pos = foes[i].Disp = keep[i]; Time.unscaledDeltaTime = 1f / 60f; Time.unscaledTime += 1f / 60f;
        gm.FixedStep(1f / 60f, new PlayerInput { AimAt = home + new V2(265f, 0f), Fire = !idle, WeaponAction = laser && f >= 6 && f < 9 });
        me.Disp = me.Pos = home; me.Aim = 0f; me.Hp = me.MaxHp; for (int i = 0; i < foes.Count; i++) foes[i].Disp = foes[i].Pos = keep[i]; draw.Invoke(app, null);
      }
      GL.Tris.Clear(); GUI.Log.Clear();
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    }
    // RAILGUN=<s>: the Soldier DPS's Railgun (Core/RailBeam.cs, Game/RailBeamLook.cs) that long into a scripted shot: fire held from 0 for
    // RAILHOLD s (default 1.6, a full charge; 1.15 is a perfect one), then let go, aimed RAILAIM degrees (default 25: down into the
    // bottom wall, so it bounces) at a pack of grunts. RAILHEAT=<0-100>: the heat meter starts there. RAILDUMP=1: no shot; Alt at 0.05 s (Heat Dump).
    if (Environment.GetEnvironmentVariable("RAILGUN") != null) {
      var ci = System.Globalization.CultureInfo.InvariantCulture;
      var gm = host.Game; gm.Bot = false; gm.Bullets.Clear(); gm.Effects.Clear(); gm.Enemies.Clear(); gm.Zones.Clear();
      me.Role = Role.Dps; me.Kit = Kit.Soldier; me.Def = Defs.Get(Role.Dps); me.Weapon = Defs.Weapon(Role.Dps, Kit.Soldier, 1); me.MaxHp = me.Hp = me.Def.Hp; foreach (var sl in me.Slots) sl.Id = AbilityId.None; foreach (var q0 in gm.Players.Values) { q0.AegisT = 0f; q0.UltT = 0f; }
      V2 home = new V2(420f, 330f); me.Pos = me.Disp = home;
      bool dump = Environment.GetEnvironmentVariable("RAILDUMP") != null;
      float aimDeg = float.Parse(Environment.GetEnvironmentVariable("RAILAIM") ?? (dump ? "0" : "25"), ci), hold = float.Parse(Environment.GetEnvironmentVariable("RAILHOLD") ?? "1.6", ci);
      float heat = float.Parse(Environment.GetEnvironmentVariable("RAILHEAT") ?? "0", ci);
      me.RailHeatF = heat; me.RailHeat = (byte)Math.Ceiling(heat);
      float aim = aimDeg * MathX.Pi / 180f; me.Aim = aim;
      var spawnAt = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      var foes = new System.Collections.Generic.List<Enemy>();
      var spots = dump ? new[] { new V2(150f, -40f), new V2(190f, 20f), new V2(120f, 50f), new V2(240f, -10f), new V2(260f, 60f) }
                       : new[] { new V2(160f, 40f), new V2(260f, 150f), new V2(560f, 200f), new V2(700f, 20f), new V2(860f, -90f) };
      for (int i = 0; i < spots.Length; i++) {
        var e = (Enemy)spawnAt.Invoke(gm, new object[] { i == 3 ? EnemyType.Brute : EnemyType.Grunt, home + spots[i], 0f, 0, false }); e.Speed = 0f; e.FireT = 99f; e.SpawnT = 0f; e.MaxHp = e.Hp = 1e6f; e.Disp = e.Pos; foes.Add(e); }
      var draw = typeof(ArenaApp).GetMethod("DrawWorld", f2);
      int frames = Math.Max(2, (int)Math.Round(float.Parse(Environment.GetEnvironmentVariable("RAILGUN"), ci) * 60f));
      var keep = new V2[foes.Count];
      for (int i = 0; i < foes.Count; i++) keep[i] = foes[i].Pos;
      V2 aimAt = home + V2.FromAngle(aim) * 300f;
      for (int f = 0; f < frames; f++) {
        me.Pos = me.Disp = home; me.Aim = aim; for (int i = 0; i < foes.Count; i++) foes[i].Pos = foes[i].Disp = keep[i]; Time.unscaledDeltaTime = 1f / 60f; Time.unscaledTime += 1f / 60f;
        gm.FixedStep(1f / 60f, new PlayerInput { AimAt = aimAt, Fire = !dump && f < hold * 60f, WeaponAction = dump && f >= 3 && f < 6 }); gm.Update(1f / 60f);
        me.Disp = me.Pos = home; me.Aim = aim; me.Flash = 0f; for (int i = 0; i < foes.Count; i++) foes[i].Disp = foes[i].Pos = keep[i]; draw.Invoke(app, null);
      }
      me.Flash = 0f; me.Hp = me.MaxHp;
      GL.Tris.Clear(); GUI.Log.Clear();
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    }
    // GRENADE=<s>: the Tank Soldier's Grenade Launcher (Core/GrenadeLauncher.cs, Game/GrenadeLauncherLook.cs) that long into a fight: a pack of
    // grunts and a brute GLAT px ahead (default 250), fire held (HE; the drum empties at 2.5 s and reloads). GLSMOKE=1: Alt at 0.1 s (the smoke drum),
    // then fire held from 0.75 s (three canisters); GLSWAP=1: Alt at 0.1 s and nothing else; GLIDLE=1: not firing; GLONE=1: one shot only.
    if (Environment.GetEnvironmentVariable("GRENADE") != null) {
      var ci = System.Globalization.CultureInfo.InvariantCulture;
      var gm = host.Game; gm.Bot = false; gm.Bullets.Clear(); gm.Effects.Clear(); gm.Enemies.Clear(); gm.Zones.Clear();
      me.Role = Role.Tank; me.Kit = Kit.Soldier; me.Def = Defs.Get(Role.Tank); me.Weapon = Defs.Weapon(Role.Tank, Kit.Soldier, 3); me.MaxHp = me.Hp = me.Def.Hp; foreach (var sl in me.Slots) sl.Id = AbilityId.None; foreach (var q0 in gm.Players.Values) { q0.AegisT = 0f; q0.UltT = 0f; }
      V2 home = new V2(420f, 330f); me.Pos = me.Disp = home; me.Aim = 0f;
      float far = float.Parse(Environment.GetEnvironmentVariable("GLAT") ?? "250", ci);
      var spawnAt = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      var foes = new System.Collections.Generic.List<Enemy>();
      var spots = new[] { new V2(far - 30f, -40f), new V2(far + 25f, -30f), new V2(far - 10f, 30f), new V2(far + 45f, 40f), new V2(far + 130f, -10f) };
      for (int i = 0; i < spots.Length; i++) {
        var e = (Enemy)spawnAt.Invoke(gm, new object[] { i == 4 ? EnemyType.Brute : EnemyType.Grunt, home + spots[i], 0f, 0, false }); e.Speed = 0f; e.FireT = 99f; e.SpawnT = 0f; e.MaxHp = e.Hp = 1e6f; e.Disp = e.Pos; foes.Add(e); }
      bool smoke = Environment.GetEnvironmentVariable("GLSMOKE") != null, swap = Environment.GetEnvironmentVariable("GLSWAP") != null, idle = Environment.GetEnvironmentVariable("GLIDLE") != null;
      var draw = typeof(ArenaApp).GetMethod("DrawWorld", f2);
      int frames = Math.Max(2, (int)Math.Round(float.Parse(Environment.GetEnvironmentVariable("GRENADE"), ci) * 60f));
      var keep = new V2[foes.Count];
      for (int i = 0; i < foes.Count; i++) keep[i] = foes[i].Pos;
      for (int f = 0; f < frames; f++) {
        me.Pos = me.Disp = home; me.Aim = 0f; for (int i = 0; i < foes.Count; i++) foes[i].Pos = foes[i].Disp = keep[i]; Time.unscaledDeltaTime = 1f / 60f; Time.unscaledTime += 1f / 60f;
        bool fire = !idle && !swap && (!smoke || f >= 54) && (Environment.GetEnvironmentVariable("GLONE") == null || f < (smoke ? 57 : 3));
        gm.FixedStep(1f / 60f, new PlayerInput { AimAt = home + new V2(far, 0f), Fire = fire, WeaponAction = (smoke || swap) && f >= 18 && f < 21 });
        me.Disp = me.Pos = home; me.Aim = 0f; me.Flash = 0f; for (int i = 0; i < foes.Count; i++) foes[i].Disp = foes[i].Pos = keep[i]; draw.Invoke(app, null);
      }
      me.Flash = 0f; me.Hp = me.MaxHp;
      GL.Tris.Clear(); GUI.Log.Clear();
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    }
    // HONEYBEE=<s>: the Summoner DPS's Honeybee Staff (Core/HoneybeeStaff.cs, Game/HoneybeeLook.cs) that long into a scripted loop: clicks
    // by the left dummy at 0, 0.4 and 0.8 s (they swarm it), Alt held 1.0-1.9 s at the right pack (honey puddles), then a click at 2.0 s on
    // the weak dummy standing in the honey (it dies there at about 2.5 s: the harvest's two bees fly at the others).
    if (Environment.GetEnvironmentVariable("HONEYBEE") != null) {
      var ci = System.Globalization.CultureInfo.InvariantCulture;
      var gm = host.Game; gm.Bot = false; gm.Bullets.Clear(); gm.Effects.Clear(); gm.Enemies.Clear(); gm.Zones.Clear();
      me.Role = Role.Dps; me.Kit = Kit.Summoner; me.Def = Defs.Get(Role.Dps); me.Weapon = Defs.Weapon(Role.Dps, Kit.Summoner, 3); me.MaxHp = me.Hp = me.Def.Hp; foreach (var sl in me.Slots) sl.Id = AbilityId.None; foreach (var q0 in gm.Players.Values) { q0.AegisT = 0f; q0.UltT = 0f; }
      me.HoneyFill = Game.HoneyMax;
      V2 home = new V2(380f, 360f); me.Pos = me.Disp = home; me.Aim = 0f;
      var spawnAt = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      var foes = new System.Collections.Generic.List<Enemy>();
      foreach (var at0 in new[] { new V2(-150f, -170f), new V2(170f, -140f), new V2(215f, -95f), new V2(130f, -80f), new V2(250f, -170f) }) {
        var e = (Enemy)spawnAt.Invoke(gm, new object[] { EnemyType.Grunt, home + at0, 0f, 0, false }); e.Speed = 0f; e.FireT = 99f; e.SpawnT = 0f; e.MaxHp = e.Hp = 1e6f; e.Disp = e.Pos; foes.Add(e); }
      foes[3].MaxHp = foes[3].Hp = 12f;  // the weak one, in the honey
      var draw = typeof(ArenaApp).GetMethod("DrawWorld", f2);
      int frames = Math.Max(1, (int)Math.Round(float.Parse(Environment.GetEnvironmentVariable("HONEYBEE"), ci) * 60f));
      for (int f = 0; f < frames; f++) {
        float tt = f / 60f;
        V2 cursor = tt < 0.95f ? foes[0].Pos + new V2(20f, 50f) : tt < 1.95f ? home + new V2(180f, -120f) : foes[3].Pos + new V2(-30f, 10f);
        bool fire = (tt < 2f / 60f) || (tt >= 0.4f && tt < 0.4f + 2f / 60f) || (tt >= 0.8f && tt < 0.8f + 2f / 60f) || (tt >= 2.0f && tt < 2.0f + 2f / 60f);
        bool alt = tt >= 1.0f && tt < 1.9f;
        foreach (var e in foes) { e.Disp = e.Pos; e.FireT = 99f; }
        Time.unscaledDeltaTime = 1f / 60f; Time.unscaledTime += 1f / 60f;
        if (fire) me.FireCd = 0f;
        gm.FixedStep(1f / 60f, new PlayerInput { AimAt = cursor, Fire = fire, WeaponAction = alt });
        gm.Update(1f / 60f);
        me.Pos = home;
        me.Disp = me.Pos; me.Aim = (cursor - me.Pos).Angle; me.AimAt = cursor; foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
        foreach (var m in gm.Minions.Values) m.Disp = m.Pos;
        draw.Invoke(app, null);
      }
      GL.Tris.Clear(); GUI.Log.Clear();
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    }
    // STONESPEAR=<s>: the Elementalist DPS's Stonespear (Core/Stonespear.cs, Game/StonespearLook.cs) that long into the mockup's loop: Fire at 0 (the
    // slam), single throws at 1.3 s and 1.85 s, Alt at 2.6 s (the volley at the dummy nearest the cursor). SSMOVE=1: you walk down the whole time
    // (the hovering spears trail behind you); SSTECT=1: with Tectonic (four a side).
    if (Environment.GetEnvironmentVariable("STONESPEAR") != null) {
      var ci = System.Globalization.CultureInfo.InvariantCulture;
      var gm = host.Game; gm.Bot = false; gm.Bullets.Clear(); gm.Effects.Clear(); gm.Enemies.Clear();
      me.Role = Role.Dps; me.Kit = Kit.Elementalist; me.Def = Defs.Get(Role.Dps); me.Weapon = Defs.Weapon(Role.Dps, Kit.Elementalist, 3); me.MaxHp = me.Hp = me.Def.Hp; foreach (var sl in me.Slots) sl.Id = AbilityId.None; foreach (var q0 in gm.Players.Values) { q0.AegisT = 0f; q0.UltT = 0f; }
      if (Environment.GetEnvironmentVariable("SSTECT") != null) { me.Stats.SsSideBonus = 1; me.Stats.SsSlamMult = 1.2f; }
      V2 home = new V2(380f, 300f); me.Pos = me.Disp = home; me.Aim = 0f;
      var spawnAt = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      var foes = new System.Collections.Generic.List<Enemy>();
      foreach (var at0 in new[] { new V2(190f, -70f), new V2(230f, 30f), new V2(290f, -20f), new V2(330f, 90f), new V2(300f, -130f) }) {
        var e = (Enemy)spawnAt.Invoke(gm, new object[] { EnemyType.Grunt, home + at0, 0f, 0, false }); e.Speed = 0f; e.FireT = 99f; e.SpawnT = 0f; e.MaxHp = e.Hp = 1e6f; e.Disp = e.Pos; foes.Add(e); }
      bool move = Environment.GetEnvironmentVariable("SSMOVE") != null;
      var draw = typeof(ArenaApp).GetMethod("DrawWorld", f2);
      int frames = Math.Max(1, (int)Math.Round(float.Parse(Environment.GetEnvironmentVariable("STONESPEAR"), ci) * 60f));
      for (int f = 0; f < frames; f++) {
        float tt = f / 60f;
        V2 cursor = tt < 1.6f ? foes[1].Pos : tt < 2.3f ? foes[2].Pos : foes[4].Pos;
        bool fire = f < 2 || (tt >= 1.3f && tt < 1.3f + 2f / 60f) || (tt >= 1.85f && tt < 1.85f + 2f / 60f);
        bool alt = tt >= 2.6f && tt < 2.6f + 3f / 60f;
        foreach (var e in foes) { e.Disp = e.Pos; e.FireT = 99f; }
        Time.unscaledDeltaTime = 1f / 60f; Time.unscaledTime += 1f / 60f;
        gm.FixedStep(1f / 60f, new PlayerInput { AimAt = cursor, Fire = fire, WeaponAction = alt, Move = move ? new V2(0f, 1f) : new V2(0f, 0f) });
        gm.Update(1f / 60f);  // the effects' clocks and AnimT (the spears ease by it)
        if (!move) me.Pos = home;
        me.Disp = me.Pos; me.Aim = (cursor - me.Pos).Angle; me.AimAt = cursor; foreach (var e in foes) e.Disp = e.Pos; draw.Invoke(app, null);
      }
      GL.Tris.Clear(); GUI.Log.Clear();
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    }
    // EMBOLUS=<s>: the Hemomancer DPS's Embolus Spears (Core/EmbolusSpears.cs, Game/EmbolusLook.cs) that long into the mockup's loop:
    // clicks at 0 and 0.3 s (left spear, right spear; they stick in the right edge), 1.7 s (recall), 2.6 and 2.9 s (both again),
    // Alt at 4.1 s (they burst), hardening back by 4.6 s. ESAIM=<deg>: aim that far down from straight right.
    if (Environment.GetEnvironmentVariable("EMBOLUS") != null) {
      var ci = System.Globalization.CultureInfo.InvariantCulture;
      var gm = host.Game; gm.Bot = false; gm.Bullets.Clear(); gm.Effects.Clear(); gm.Enemies.Clear();
      me.Role = Role.Dps; me.Kit = Kit.Hemomancer; me.Def = Defs.Get(Role.Dps); me.Weapon = Defs.Weapon(Role.Dps, Kit.Hemomancer, 3); me.MaxHp = me.Hp = me.Def.Hp; foreach (var sl in me.Slots) sl.Id = AbilityId.None; foreach (var q0 in gm.Players.Values) { q0.AegisT = 0f; q0.UltT = 0f; }
      V2 home = new V2(330f, 300f); me.Pos = me.Disp = home; me.Aim = 0f;
      float zoom = Environment.GetEnvironmentVariable("ZOOM") != null ? float.Parse(Environment.GetEnvironmentVariable("ZOOM"), ci) : 1f;
      float tilt = Environment.GetEnvironmentVariable("ESAIM") != null ? float.Parse(Environment.GetEnvironmentVariable("ESAIM"), ci) * MathX.Pi / 180f : 0f;
      V2 cursor = home + V2.FromAngle(tilt) * 600f;
      var spawnAt = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      var foes = new System.Collections.Generic.List<Enemy>();
      foreach (var at0 in new[] { new V2(150f, -14f), new V2(260f, 16f), new V2(370f, -36f), new V2(450f, 60f), new V2(520f, 8f), new V2(300f, -110f) }) {
        var e = (Enemy)spawnAt.Invoke(gm, new object[] { EnemyType.Grunt, home + at0.Rotated(tilt), 0f, 0, false }); e.Speed = 0f; e.FireT = 99f; e.SpawnT = 0f; e.MaxHp = e.Hp = 1e6f; e.Disp = e.Pos; foes.Add(e); }
      var draw = typeof(ArenaApp).GetMethod("DrawWorld", f2);
      int frames = Math.Max(1, (int)Math.Round(float.Parse(Environment.GetEnvironmentVariable("EMBOLUS"), ci) * 60f));
      Func<float, float, bool> At = (tt, c) => tt >= c && tt < c + 2f / 60f;
      for (int f = 0; f < frames; f++) {
        float tt = f / 60f;
        bool fire = At(tt, 0f) || At(tt, 0.3f) || At(tt, 1.7f) || At(tt, 2.6f) || At(tt, 2.9f);
        bool alt = tt >= 4.1f && tt < 4.1f + 3f / 60f;
        foreach (var e in foes) { e.Disp = e.Pos; e.FireT = 99f; }
        Time.unscaledDeltaTime = 1f / 60f; Time.unscaledTime += 1f / 60f;
        gm.FixedStep(1f / 60f, new PlayerInput { AimAt = cursor, Fire = fire, WeaponAction = alt });
        gm.Update(1f / 60f);
        me.Pos = me.Disp = home; me.Aim = (cursor - me.Pos).Angle; me.AimAt = cursor; foreach (var e in foes) e.Disp = e.Pos;
        if (zoom > 1f) { typeof(ArenaApp).GetField("camZoom", f2).SetValue(app, zoom); typeof(ArenaApp).GetField("camFocus", f2).SetValue(app, home + new V2(260f, 0f)); }
        draw.Invoke(app, null);
      }
      GL.Tris.Clear(); GUI.Log.Clear();
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    }
    // BONESPEAR=<s>: the Hemomancer DPS's Blood Brawler, the Marionette (Core/BloodBrawler.cs, Game/BloodBrawlerLook.cs) that long into a loop on
    // one marked Grunt with five around it: Pulse at 0 s, Reel at 0.45 s, Heart Lift at 0.95 s, Marrow Burst at 1.5 s, Alt (Sympathy)
    // at 2.4 s, then Pulse at 2.7 s and Reel at 3.1 s echoing onto the bound ones. BSAIM=<deg> tilts the line; BSHAND=1 centres on the
    // Hemomancer's bleeding hand (zoom in with ZOOM=4).
    if (Environment.GetEnvironmentVariable("BONESPEAR") != null) {
      var ci = System.Globalization.CultureInfo.InvariantCulture;
      var gm = host.Game; gm.Bot = false; gm.Bullets.Clear(); gm.Effects.Clear(); gm.Enemies.Clear();
      me.Role = Role.Dps; me.Kit = Kit.Hemomancer; me.Def = Defs.Get(Role.Dps); me.Weapon = Defs.Weapon(Role.Dps, Kit.Hemomancer, 2); me.MaxHp = me.Hp = me.Def.Hp; foreach (var sl in me.Slots) sl.Id = AbilityId.None; foreach (var q0 in gm.Players.Values) { q0.AegisT = 0f; q0.UltT = 0f; }
      V2 home = new V2(330f, 300f); me.Pos = me.Disp = home; me.Aim = 0f; me.Combo = 0;
      float zoom = Environment.GetEnvironmentVariable("ZOOM") != null ? float.Parse(Environment.GetEnvironmentVariable("ZOOM"), ci) : 1f;
      float tilt = Environment.GetEnvironmentVariable("BSAIM") != null ? float.Parse(Environment.GetEnvironmentVariable("BSAIM"), ci) * MathX.Pi / 180f : 0f;
      var spawnAt = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      var foes = new System.Collections.Generic.List<Enemy>();
      foreach (var at0 in new[] { new V2(300f, 0f), new V2(340f, -110f), new V2(400f, 80f), new V2(250f, 120f), new V2(450f, -30f), new V2(560f, 150f) }) {
        var e = (Enemy)spawnAt.Invoke(gm, new object[] { EnemyType.Grunt, home + at0.Rotated(tilt), 0f, 0, false }); e.Speed = 0f; e.FireT = 99f; e.SpawnT = 0f; e.MaxHp = e.Hp = 1e6f; e.Disp = e.Pos; foes.Add(e); }
      var target = foes[0];
      var draw = typeof(ArenaApp).GetMethod("DrawWorld", f2);
      int frames = Math.Max(1, (int)Math.Round(float.Parse(Environment.GetEnvironmentVariable("BONESPEAR"), ci) * 60f));
      Func<float, float, bool> At = (tt, c) => tt >= c && tt < c + 2f / 60f;
      for (int f = 0; f < frames; f++) {
        float tt = f / 60f;
        bool fire = At(tt, 0f) || At(tt, 0.45f) || At(tt, 0.95f) || At(tt, 1.5f) || At(tt, 2.7f) || At(tt, 3.1f);
        bool alt = tt >= 2.4f && tt < 2.4f + 3f / 60f;
        V2 cursor = target.Pos;
        foreach (var e in foes) { e.Disp = e.Pos; e.FireT = 99f; }
        Time.unscaledDeltaTime = 1f / 60f; Time.unscaledTime += 1f / 60f;
        gm.FixedStep(1f / 60f, new PlayerInput { AimAt = cursor, Fire = fire, WeaponAction = alt });
        gm.Update(1f / 60f);
        me.Pos = me.Disp = home; me.Aim = (target.Pos - me.Pos).Angle; me.AimAt = target.Pos; foreach (var e in foes) e.Disp = e.Pos;
        if (Environment.GetEnvironmentVariable("BSHAND") != null) { me.Flash = 0f; me.Hp = me.MaxHp; }  // (the health each move costs flashes the model white: show its colours)
        if (zoom > 1f) { typeof(ArenaApp).GetField("camZoom", f2).SetValue(app, zoom); typeof(ArenaApp).GetField("camFocus", f2).SetValue(app, home + new V2(Environment.GetEnvironmentVariable("BSHAND") != null ? 30f : 220f, 0f).Rotated(tilt)); }
        draw.Invoke(app, null);
      }
      GL.Tris.Clear(); GUI.Log.Clear();
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    }
    // NUNCHAKU=<s>: the Ninja DPS's Nunchaku, Twin Storm (Core/Nunchaku.cs, Game/NunchakuLook.cs) that long into the mockup's loop: Fire held
    // from 0 to 1.85 s (Snap, Backhand, Double Figure-8, Twin Crush), Alt at 2.1 s (the Twin Windmill guard; small shots fly in from
    // the right from 2.2 s and are swatted), Fire at 3.7 s (the Twin Counter), Alt again at 4.5 s.
    if (Environment.GetEnvironmentVariable("NUNCHAKU") != null) {
      var ci = System.Globalization.CultureInfo.InvariantCulture;
      var gm = host.Game; gm.Bot = false; gm.Bullets.Clear(); gm.Effects.Clear(); gm.Enemies.Clear();
      me.Role = Role.Dps; me.Kit = Kit.Ninja; me.Def = Defs.Get(Role.Dps); me.Weapon = Defs.Weapon(Role.Dps, Kit.Ninja, 3); me.MaxHp = me.Hp = me.Def.Hp; foreach (var sl in me.Slots) sl.Id = AbilityId.None; foreach (var q0 in gm.Players.Values) { q0.AegisT = 0f; q0.UltT = 0f; }
      V2 home = new V2(380f, 300f); me.Pos = me.Disp = home; me.Aim = 0f; me.NunGuard = false; me.NunSwats = 0; me.Combo = 0;
      var spawnAt = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      var shoot = typeof(Game).GetMethod("SpawnBullet", BindingFlags.NonPublic | BindingFlags.Instance);
      var foes = new System.Collections.Generic.List<Enemy>();
      foreach (var at0 in new[] { new V2(78f, -34f), new V2(92f, 6f), new V2(74f, 42f), new V2(170f, -10f) }) {
        var e = (Enemy)spawnAt.Invoke(gm, new object[] { EnemyType.Grunt, home + at0, 0f, 0, false }); e.Speed = 0f; e.FireT = 99f; e.SpawnT = 0f; e.MaxHp = e.Hp = 1e6f; e.Disp = e.Pos; foes.Add(e); }
      var draw = typeof(ArenaApp).GetMethod("DrawWorld", f2);
      float[] ys = { -8f, 14f, -22f, 4f, 24f, -14f, 10f, -28f, 18f, -4f };
      int shot = 0;
      int frames = Math.Max(1, (int)Math.Round(float.Parse(Environment.GetEnvironmentVariable("NUNCHAKU"), ci) * 60f));
      for (int f = 0; f < frames; f++) {
        float tt = f / 60f;
        V2 cursor = home + new V2(200f, 0f);
        bool fire = tt < 1.85f || (tt >= 3.7f && tt < 3.7f + 2f / 60f);
        bool alt = (tt >= 2.1f && tt < 2.1f + 3f / 60f) || (tt >= 4.5f && tt < 4.5f + 3f / 60f);
        if (tt >= 2.2f && tt < 3.5f && f % 9 == 0) {
          V2 from = home + new V2(420f, ys[shot++ % ys.Length] * 1.3f);
          shoot.Invoke(gm, new object[] { Defs.SmallShot, from, (home - from).Normalized() * 320f, 0f, 3f, 5f, 0, false, 0f });
        }
        foreach (var e in foes) { e.Disp = e.Pos; e.FireT = 99f; }
        Time.unscaledDeltaTime = 1f / 60f; Time.unscaledTime += 1f / 60f; Time.deltaTime = 1f / 60f; Time.time += 1f / 60f;
        gm.FixedStep(1f / 60f, new PlayerInput { AimAt = cursor, Fire = fire, WeaponAction = alt });
        gm.Update(1f / 60f);
        me.Pos = home; me.Hp = me.MaxHp;
        me.Disp = me.Pos; me.Aim = (cursor - me.Pos).Angle; me.AimAt = cursor; foreach (var e in foes) e.Disp = e.Pos; draw.Invoke(app, null);
      }
      GL.Tris.Clear(); GUI.Log.Clear();
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    }
    // NODACHI=<s>: the Tank Ninja's Nodachi (Core/Nodachi.cs, Game/NodachiLook.cs) that long after Fire is first held (held throughout,
    // so the stance's combo plays on). NODGUARD=1 starts in Mamori (the Guard: shots fly in from the right); NODALT=<s> presses Alt then.
    if (Environment.GetEnvironmentVariable("NODACHI") != null) {
      var ci = System.Globalization.CultureInfo.InvariantCulture;
      var gm = host.Game; gm.Bot = false; gm.Bullets.Clear(); gm.Effects.Clear(); gm.Enemies.Clear();
      me.Role = Role.Tank; me.Kit = Kit.Ninja; me.Def = Defs.Get(Role.Tank); me.Weapon = Defs.Weapon(Role.Tank, Kit.Ninja, 3); me.MaxHp = me.Hp = me.Def.Hp; foreach (var sl in me.Slots) sl.Id = AbilityId.None; foreach (var q0 in gm.Players.Values) { q0.AegisT = 0f; q0.UltT = 0f; }
      bool guard = Environment.GetEnvironmentVariable("NODGUARD") != null;
      float altAt = Environment.GetEnvironmentVariable("NODALT") != null ? float.Parse(Environment.GetEnvironmentVariable("NODALT"), ci) : -1f;
      V2 home = new V2(480f, 360f); me.Pos = me.Disp = home; me.Aim = 0f; me.NodStance = (byte)(guard ? 1 : 0); me.NodStorm = me.NodGuard = 0;
      var spawnAt = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      var shoot = typeof(Game).GetMethod("SpawnBullet", BindingFlags.NonPublic | BindingFlags.Instance);
      var foes = new System.Collections.Generic.List<Enemy>();
      foreach (var at0 in new[] { new V2(120f, -40f), new V2(150f, 50f), new V2(60f, -130f), new V2(-40f, 140f), new V2(-140f, -60f), new V2(200f, -5f) }) {
        var e = (Enemy)spawnAt.Invoke(gm, new object[] { EnemyType.Grunt, home + at0, 0f, 0, false }); e.Speed = 0f; e.FireT = 99f; e.SpawnT = 0f; e.MaxHp = e.Hp = 1e6f; e.Disp = e.Pos; foes.Add(e); }
      var draw = typeof(ArenaApp).GetMethod("DrawWorld", f2);
      float[] ys = { -8f, 14f, -22f, 4f, 24f, -14f, 10f, -28f, 18f, -4f };
      int shot = 0;
      int frames = Math.Max(1, (int)Math.Round(float.Parse(Environment.GetEnvironmentVariable("NODACHI"), ci) * 60f));
      for (int f = 0; f < frames; f++) {
        float tt = f / 60f;
        V2 cursor = home + new V2(200f, 0f);
        bool alt = altAt >= 0f && tt >= altAt && tt < altAt + 3f / 60f;
        if (me.NodStance == 1 && f % 8 == 0) {
          V2 from = home + new V2(360f, ys[shot++ % ys.Length] * 2f);
          shoot.Invoke(gm, new object[] { Defs.SmallShot, from, (home - from).Normalized() * 340f, 0f, 3f, 5f, 0, false, 0f });
        }
        foreach (var e in foes) { e.Disp = e.Pos; e.FireT = 99f; }
        Time.unscaledDeltaTime = 1f / 60f; Time.unscaledTime += 1f / 60f; Time.deltaTime = 1f / 60f; Time.time += 1f / 60f;
        gm.FixedStep(1f / 60f, new PlayerInput { AimAt = cursor, Fire = true, WeaponAction = alt });
        gm.Update(1f / 60f);
        me.Pos = home; me.Hp = me.MaxHp;
        me.Disp = me.Pos; me.Aim = (cursor - me.Pos).Angle; me.AimAt = cursor; foreach (var e in foes) e.Disp = e.Pos; draw.Invoke(app, null);
      }
      GL.Tris.Clear(); GUI.Log.Clear();
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    }
    // VOWGUARD=<s>: the Healer Knight's Vowguard (Core/Vowguard.cs, Game/VowguardLook.cs) that long after Fire is first held (held
    // throughout, so the stance's chain plays on). VGSANCT=1 starts in Sanctuary (shots fly in from the right); VGALT=<s> presses Alt
    // then; VGCHAIN=<line> plays that chain pair ("EdgeCut,LungeThrust;RallyTap,AegisDome", VowguardMotion.FromLine); VGFREEZE=1 stops
    // Fire on the last frame's step (so a still catches a move mid-swing).
    if (Environment.GetEnvironmentVariable("VOWGUARD") != null) {
      var ci = System.Globalization.CultureInfo.InvariantCulture;
      var gm = host.Game; gm.Bot = false; gm.Bullets.Clear(); gm.Effects.Clear(); gm.Enemies.Clear();
      me.Role = Role.Healer; me.Kit = Kit.Knight; me.Def = Defs.Get(Role.Healer); me.Weapon = Defs.Weapon(Role.Healer, Kit.Knight, 3); me.MaxHp = me.Hp = me.Def.Hp; foreach (var sl in me.Slots) sl.Id = AbilityId.None; foreach (var q0 in gm.Players.Values) { q0.AegisT = 0f; q0.UltT = 0f; }
      bool sanct = Environment.GetEnvironmentVariable("VGSANCT") != null;
      float altAt = Environment.GetEnvironmentVariable("VGALT") != null ? float.Parse(Environment.GetEnvironmentVariable("VGALT"), ci) : -1f;
      if (Environment.GetEnvironmentVariable("VGCHAIN") != null) me.VgChain = VowguardMotion.FromLine(Environment.GetEnvironmentVariable("VGCHAIN"));
      V2 home = new V2(480f, 360f); me.Pos = me.Disp = home; me.Aim = 0f; me.VgStance = (byte)(sanct ? 1 : 0); me.VgAtV = me.VgAtS = 0; me.VgFaith = 3;
      var spawnAt = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      var shoot = typeof(Game).GetMethod("SpawnBullet", BindingFlags.NonPublic | BindingFlags.Instance);
      var foes = new System.Collections.Generic.List<Enemy>();
      foreach (var at0 in new[] { new V2(95f, -25f), new V2(125f, 40f), new V2(80f, 95f), new V2(190f, -70f) }) {
        var e = (Enemy)spawnAt.Invoke(gm, new object[] { EnemyType.Grunt, home + at0, 0f, 0, false }); e.Speed = 0f; e.FireT = 99f; e.SpawnT = 0f; e.MaxHp = e.Hp = 1e6f; e.Disp = e.Pos; foes.Add(e); }
      foreach (var q0 in gm.Players.Values) if (q0 != me) { q0.Pos = q0.Disp = home + new V2(-120f, 50f); q0.Hp = q0.MaxHp * 0.6f; }
      var draw = typeof(ArenaApp).GetMethod("DrawWorld", f2);
      float[] ys = { -8f, 14f, -22f, 4f, 24f, -14f, 10f, -28f, 18f, -4f };
      int shot = 0;
      int frames = Math.Max(1, (int)Math.Round(float.Parse(Environment.GetEnvironmentVariable("VOWGUARD"), ci) * 60f));
      for (int f = 0; f < frames; f++) {
        float tt = f / 60f;
        V2 cursor = home + new V2(200f, 0f);
        bool alt = altAt >= 0f && tt >= altAt && tt < altAt + 3f / 60f;
        if (me.VgStance == 1 && f % 10 == 0) {
          V2 from = home + new V2(360f, ys[shot++ % ys.Length] * 2f);
          shoot.Invoke(gm, new object[] { Defs.SmallShot, from, (home - from).Normalized() * 340f, 0f, 3f, 5f, 0, false, 0f });
        }
        foreach (var e in foes) { e.Disp = e.Pos; e.FireT = 99f; }
        foreach (var q0 in gm.Players.Values) if (q0 != me) { q0.Pos = q0.Disp = home + new V2(-120f, 50f); }
        Time.unscaledDeltaTime = 1f / 60f; Time.unscaledTime += 1f / 60f; Time.deltaTime = 1f / 60f; Time.time += 1f / 60f;
        gm.FixedStep(1f / 60f, new PlayerInput { AimAt = cursor, Fire = true, WeaponAction = alt });
        gm.Update(1f / 60f);
        me.Pos = home; me.Hp = me.MaxHp;
        me.Disp = me.Pos; me.Aim = (cursor - me.Pos).Angle; me.AimAt = cursor; foreach (var e in foes) e.Disp = e.Pos; draw.Invoke(app, null);
      }
      GL.Tris.Clear(); GUI.Log.Clear();
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    }
    // TESSEN=<s>: the Healer Ninja's Twin Tessen (Core/TwinTessen.cs, Game/TwinTessenLook.cs) that long into the Crane Dance, Fire held
    // from 0: Left Flick 0, then each step as the last ends (the Crane Bloom about 1.25 s, its wave out by 1.45 s); a hurt teammate in front.
    // TTALT=<s>: Alt held from 0 for that long instead, then let go (the Kamikaze Gust), enemies shooting; TTBREEZE=100: a Tempest.
    if (Environment.GetEnvironmentVariable("TESSEN") != null) {
      var ci = System.Globalization.CultureInfo.InvariantCulture;
      var gm = host.Game; gm.Bot = false; gm.Bullets.Clear(); gm.Effects.Clear(); gm.Enemies.Clear();
      me.Role = Role.Healer; me.Kit = Kit.Ninja; me.Def = Defs.Get(Role.Healer); me.Weapon = Defs.Weapon(Role.Healer, Kit.Ninja, 3); me.MaxHp = me.Hp = me.Def.Hp; foreach (var sl in me.Slots) sl.Id = AbilityId.None; foreach (var q0 in gm.Players.Values) { q0.AegisT = 0f; q0.UltT = 0f; }
      string altS = Environment.GetEnvironmentVariable("TTALT");
      bool alt = altS != null;
      float hold = alt ? float.Parse(altS, ci) : 0f;
      string brS = Environment.GetEnvironmentVariable("TTBREEZE");
      if (brS != null) { me.TtBreezeF = float.Parse(brS, ci); me.TtBreeze = (byte)me.TtBreezeF; }
      V2 home = new V2(500f, 420f); me.Pos = me.Disp = home; me.Aim = 0f;
      var mates = new System.Collections.Generic.List<Player>();
      foreach (var q in gm.Players.Values) if (q != me) mates.Add(q);
      var mateAt = new[] { new V2(95f, 55f), new V2(-60f, -140f) };
      for (int i = 0; i < mates.Count && i < 2; i++) { mates[i].Pos = mates[i].Disp = home + mateAt[i]; mates[i].Firing = false; mates[i].Hp = mates[i].MaxHp * 0.4f; }
      var spawnAt = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      var foes = new System.Collections.Generic.List<Enemy>();
      var spots = alt ? new[] { new V2(200f, -60f), new V2(260f, 90f), new V2(150f, 20f), new V2(330f, -10f), new V2(230f, 150f) }
                      : new[] { new V2(80f, -50f), new V2(100f, 30f), new V2(190f, 0f), new V2(60f, -85f), new V2(-70f, 60f) };
      foreach (var at0 in spots) {
        var e = (Enemy)spawnAt.Invoke(gm, new object[] { EnemyType.Grunt, home + at0, 0f, 0, false }); e.Speed = 0f; e.FireT = alt ? 0.1f : 99f; e.SpawnT = 0f; e.MaxHp = e.Hp = 1e6f; e.Disp = e.Pos; foes.Add(e); }
      var draw = typeof(ArenaApp).GetMethod("DrawWorld", f2);
      int frames = Math.Max(1, (int)Math.Round(float.Parse(Environment.GetEnvironmentVariable("TESSEN"), ci) * 60f));
      V2 cursor = home + new V2(300f, 0f);
      for (int f = 0; f < frames; f++) {
        foreach (var e in foes) { e.Disp = e.Pos; if (!alt) e.FireT = 99f; }
        Time.unscaledDeltaTime = 1f / 60f; Time.unscaledTime += 1f / 60f;
        gm.FixedStep(1f / 60f, new PlayerInput { AimAt = cursor, Fire = !alt, WeaponAction = alt && f < hold * 60f });
        gm.Update(1f / 60f);
        me.Pos = home; me.Disp = me.Pos; me.Aim = 0f; me.AimAt = cursor; me.Hp = me.MaxHp;
        for (int i = 0; i < mates.Count && i < 2; i++) { mates[i].Pos = mates[i].Disp = home + mateAt[i]; }
        foreach (var e in foes) e.Disp = e.Pos; draw.Invoke(app, null);
      }
      GL.Tris.Clear(); GUI.Log.Clear();
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    }
    // HEARTFLAIL=<s>: the Healer Hemomancer's Heart Flail (Core/HeartFlail.cs, Game/HeartFlailLook.cs) that long in. HFMODE: swing (a click at 0:
    // the swing lands about 0.15 s), spin (Fire held from 0; spinning from 0.22 s), throw (Alt at 0: rooted from 0.25 s, veins to the hurt
    // teammate by the cursor), burst (the beat at 180: the Cardiac Burst on the next beat). Hurt teammates, dummy enemies round you.
    if (Environment.GetEnvironmentVariable("HEARTFLAIL") != null) {
      var ci = System.Globalization.CultureInfo.InvariantCulture;
      var gm = host.Game; gm.Bot = false; gm.Bullets.Clear(); gm.Effects.Clear(); gm.Enemies.Clear();
      me.Role = Role.Healer; me.Kit = Kit.Hemomancer; me.Def = Defs.Get(Role.Healer); me.Weapon = Defs.Weapon(Role.Healer, Kit.Hemomancer, 3); me.MaxHp = me.Hp = me.Def.Hp; foreach (var sl in me.Slots) sl.Id = AbilityId.None; foreach (var q0 in gm.Players.Values) { q0.AegisT = 0f; q0.UltT = 0f; }
      string mode = Environment.GetEnvironmentVariable("HFMODE") ?? "swing";
      V2 home = new V2(500f, 420f); me.Pos = me.Disp = home; me.Aim = 0f;
      var mates = new System.Collections.Generic.List<Player>();
      foreach (var q in gm.Players.Values) if (q != me) mates.Add(q);
      var mateAt = mode == "throw" ? new[] { new V2(250f, 110f), new V2(-90f, -120f) } : new[] { new V2(-40f, 110f), new V2(-110f, -90f) };
      for (int i = 0; i < mates.Count && i < 2; i++) { mates[i].Pos = mates[i].Disp = home + mateAt[i]; mates[i].Firing = false; mates[i].Hp = mates[i].MaxHp * 0.4f; }
      var spawnAt = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      var foes = new System.Collections.Generic.List<Enemy>();
      var spots = mode == "throw" ? new[] { new V2(290f, -20f), new V2(330f, 40f), new V2(150f, -120f) }
                : new[] { new V2(100f, -30f), new V2(95f, 45f), new V2(-110f, 20f), new V2(20f, -118f), new V2(210f, 0f) };
      foreach (var at0 in spots) {
        var e = (Enemy)spawnAt.Invoke(gm, new object[] { EnemyType.Grunt, home + at0, 0f, 0, false }); e.Speed = 0f; e.FireT = 99f; e.SpawnT = 0f; e.MaxHp = e.Hp = 1e6f; e.Disp = e.Pos; foes.Add(e); }
      if (mode == "burst") {
        gm.FixedStep(1f / 60f, new PlayerInput { AimAt = home + new V2(300f, 0f) });
        var hf = (System.Collections.IDictionary)typeof(Game).GetField("hfHost", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(gm);
        var h = hf[me.Id]; h.GetType().GetField("BpmF").SetValue(h, Game.HfBpmMax); h.GetType().GetField("Beat").SetValue(h, 0.55f);
      }
      var draw = typeof(ArenaApp).GetMethod("DrawWorld", f2);
      int frames = Math.Max(1, (int)Math.Round(float.Parse(Environment.GetEnvironmentVariable("HEARTFLAIL"), ci) * 60f));
      V2 cursor = mode == "throw" ? home + new V2(280f, 30f) : home + new V2(300f, 0f);
      for (int f = 0; f < frames; f++) {
        foreach (var e in foes) { e.Disp = e.Pos; e.FireT = 99f; }
        Time.unscaledDeltaTime = 1f / 60f; Time.unscaledTime += 1f / 60f; Time.deltaTime = 1f / 60f;
        bool fire = mode == "spin" || mode == "burst" ? true : mode == "swing" && f < 2;
        gm.FixedStep(1f / 60f, new PlayerInput { AimAt = cursor, Fire = fire, WeaponAction = mode == "throw" && f < 2 });
        gm.Update(1f / 60f);
        me.Pos = home; me.Disp = me.Pos; me.Aim = 0f; me.AimAt = cursor; me.Hp = me.MaxHp;
        for (int i = 0; i < mates.Count && i < 2; i++) { mates[i].Pos = mates[i].Disp = home + mateAt[i]; }
        foreach (var e in foes) { e.Pos = e.Disp = home + spots[foes.IndexOf(e)]; }
        draw.Invoke(app, null);
      }
      GL.Tris.Clear(); GUI.Log.Clear();
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    }
    // WINDCLASH=<s>: the Tank Elementalist's Wind Clash (Core/WindClash.cs, Game/WindClashLook.cs) that long into the combo, Fire held from 0:
    // crescent at 0, back crescent at 0.34 s, gale lance at 0.68 s, clash ring at 1.12 s. WCALT=1: Alt (Tempest) at 0 instead, the
    // enemies shooting at you (their shots are caught and spin round you).
    if (Environment.GetEnvironmentVariable("WINDCLASH") != null) {
      var ci = System.Globalization.CultureInfo.InvariantCulture;
      var gm = host.Game; gm.Bot = false; gm.Bullets.Clear(); gm.Effects.Clear(); gm.Enemies.Clear();
      me.Role = Role.Tank; me.Kit = Kit.Elementalist; me.Def = Defs.Get(Role.Tank); me.Weapon = Defs.Weapon(Role.Tank, Kit.Elementalist, 3); me.MaxHp = me.Hp = me.Def.Hp; foreach (var sl in me.Slots) sl.Id = AbilityId.None; foreach (var q0 in gm.Players.Values) { q0.AegisT = 0f; q0.UltT = 0f; }
      bool alt = Environment.GetEnvironmentVariable("WCALT") != null;
      V2 home = new V2(500f, 420f); me.Pos = me.Disp = home; me.Aim = 0f;
      var spawnAt = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      var foes = new System.Collections.Generic.List<Enemy>();
      var spots = alt ? new[] { new V2(200f, -60f), new V2(-180f, 90f), new V2(60f, 210f), new V2(-90f, -200f), new V2(230f, 120f) }
                      : new[] { new V2(120f, -50f), new V2(150f, 40f), new V2(240f, 10f), new V2(70f, 75f), new V2(-60f, -70f) };
      foreach (var at0 in spots) {
        var e = (Enemy)spawnAt.Invoke(gm, new object[] { EnemyType.Grunt, home + at0, 0f, 0, false }); e.Speed = 0f; e.FireT = alt ? 0.1f : 99f; e.SpawnT = 0f; e.MaxHp = e.Hp = 1e6f; e.Disp = e.Pos; foes.Add(e); }
      var draw = typeof(ArenaApp).GetMethod("DrawWorld", f2);
      int frames = Math.Max(1, (int)Math.Round(float.Parse(Environment.GetEnvironmentVariable("WINDCLASH"), ci) * 60f));
      V2 cursor = home + new V2(300f, 0f);
      for (int f = 0; f < frames; f++) {
        foreach (var e in foes) { e.Disp = e.Pos; if (!alt) e.FireT = 99f; }
        Time.unscaledDeltaTime = 1f / 60f; Time.unscaledTime += 1f / 60f;
        gm.FixedStep(1f / 60f, new PlayerInput { AimAt = cursor, Fire = !alt, WeaponAction = alt && f < 3 });
        gm.Update(1f / 60f);
        me.Pos = home; me.Disp = me.Pos; me.Aim = 0f; me.AimAt = cursor; me.Hp = me.MaxHp; foreach (var e in foes) e.Disp = e.Pos; draw.Invoke(app, null);
      }
      GL.Tris.Clear(); GUI.Log.Clear();
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    }
    // BUMBLE=<s>: the DPS Engineer's Bumble Remote (Core/BumbleRemote.cs, Game/BumbleRemoteLook.cs) that long into a fight: the Bumble Bot
    // flies to the cursor by a pack ahead and stings it, Fire held from BUMFIRE s (default 0.6); BUMALT=<s>: the Swarm Burst pressed then.
    if (Environment.GetEnvironmentVariable("BUMBLE") != null) {
      var ci = System.Globalization.CultureInfo.InvariantCulture;
      var gm = host.Game; gm.Bot = false; gm.Bullets.Clear(); gm.Effects.Clear(); gm.Enemies.Clear();
      me.Role = Role.Dps; me.Kit = Kit.Engineer; me.Def = Defs.Get(Role.Dps); me.Weapon = Defs.Weapon(Role.Dps, Kit.Engineer, 1); me.MaxHp = me.Hp = me.Def.Hp; foreach (var sl in me.Slots) sl.Id = AbilityId.None; foreach (var q0 in gm.Players.Values) { q0.AegisT = 0f; q0.UltT = 0f; }
      string altS = Environment.GetEnvironmentVariable("BUMALT"), fireS = Environment.GetEnvironmentVariable("BUMFIRE");
      float altAt = altS != null ? float.Parse(altS, ci) : -1f, fireAt = fireS != null ? float.Parse(fireS, ci) : 0.6f;
      V2 home = new V2(420f, 470f); me.Pos = me.Disp = home; me.Aim = 0f;
      foreach (var q in gm.Players.Values) if (q != me) { q.Pos = q.Disp = home + new V2(-90f, -170f); q.Firing = false; }
      var spawnAt = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      var foes = new System.Collections.Generic.List<Enemy>();
      foreach (var at0 in new[] { new V2(300f, -150f), new V2(360f, -90f), new V2(250f, -60f), new V2(390f, -190f), new V2(330f, 0f), new V2(-120f, 140f) }) {
        var e = (Enemy)spawnAt.Invoke(gm, new object[] { EnemyType.Grunt, home + at0, 0f, 0, false }); e.Speed = 0f; e.FireT = 0.3f; e.SpawnT = 0f; e.MaxHp = e.Hp = 1e6f; e.Disp = e.Pos; foes.Add(e); }
      var draw = typeof(ArenaApp).GetMethod("DrawWorld", f2);
      int frames = Math.Max(1, (int)Math.Round(float.Parse(Environment.GetEnvironmentVariable("BUMBLE"), ci) * 60f));
      V2 cursor = home + new V2(230f, -120f);
      for (int f = 0; f < frames; f++) {
        float tt = f / 60f;
        foreach (var e in foes) e.Disp = e.Pos;
        Time.unscaledDeltaTime = 1f / 60f; Time.unscaledTime += 1f / 60f;
        V2 aimAt = altAt >= 0f && tt >= altAt ? home + new V2(320f, -90f) : cursor;
        gm.FixedStep(1f / 60f, new PlayerInput { AimAt = aimAt, Fire = tt >= fireAt, WeaponAction = altAt >= 0f && tt >= altAt && tt < altAt + 0.05f });
        gm.Update(1f / 60f);
        me.Pos = home; me.Disp = me.Pos; me.Aim = (aimAt - home).Angle; me.AimAt = aimAt; me.Hp = me.MaxHp;
        foreach (var m in gm.Minions.Values) m.Disp = m.Pos;
        foreach (var e in foes) e.Disp = e.Pos; draw.Invoke(app, null);
      }
      GL.Tris.Clear(); GUI.Log.Clear();
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    }
    // WYVERN=<s>: the Tank Summoner's Wyvern Staff (Core/WyvernStaff.cs, Game/WyvernStaffLook.cs) that long into a scripted loop: the
    // Wyvern hovers over you with its gale on the grunts until 1.0 s, then Fire is held (it flies out and breathes at the cursor).
    // WYALT=1: Alt (Wyrm Slam) at 1.0 s instead.
    if (Environment.GetEnvironmentVariable("WYVERN") != null) {
      var ci = System.Globalization.CultureInfo.InvariantCulture;
      var gm = host.Game; gm.Bot = false; gm.Bullets.Clear(); gm.Effects.Clear(); gm.Enemies.Clear();
      me.Role = Role.Tank; me.Kit = Kit.Summoner; me.Def = Defs.Get(Role.Tank); me.Weapon = Defs.Weapon(Role.Tank, Kit.Summoner, 3); me.MaxHp = me.Hp = me.Def.Hp; foreach (var sl in me.Slots) sl.Id = AbilityId.None; foreach (var q0 in gm.Players.Values) { q0.AegisT = 0f; q0.UltT = 0f; }
      bool alt = Environment.GetEnvironmentVariable("WYALT") != null;
      V2 home = new V2(500f, 420f); me.Pos = me.Disp = home; me.Aim = 0f;
      var spawnAt = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      var foes = new System.Collections.Generic.List<Enemy>();
      var spots = new[] { new V2(-70f, -130f), new V2(40f, -110f), new V2(100f, -60f), new V2(-120f, -50f), new V2(0f, -175f), new V2(290f, -20f), new V2(320f, 35f), new V2(255f, 20f) };
      foreach (var at0 in spots) {
        var e = (Enemy)spawnAt.Invoke(gm, new object[] { EnemyType.Grunt, home + at0, 0f, 0, false }); e.Speed = 0f; e.FireT = 99f; e.SpawnT = 0f; e.MaxHp = e.Hp = 1e6f; e.Disp = e.Pos; foes.Add(e); }
      var draw = typeof(ArenaApp).GetMethod("DrawWorld", f2);
      int frames = Math.Max(1, (int)Math.Round(float.Parse(Environment.GetEnvironmentVariable("WYVERN"), ci) * 60f));
      V2 cursor = home + new V2(300f, 0f);
      for (int f = 0; f < frames; f++) {
        foreach (var e in foes) { e.Disp = e.Pos; e.FireT = 99f; }
        Time.unscaledDeltaTime = 1f / 60f; Time.unscaledTime += 1f / 60f;
        gm.FixedStep(1f / 60f, new PlayerInput { AimAt = cursor, Fire = !alt && f >= 60, WeaponAction = alt && f >= 60 && f < 63 });
        gm.Update(1f / 60f);
        me.Pos = home; me.Disp = me.Pos; me.Aim = 0f; me.AimAt = cursor; me.Hp = me.MaxHp; foreach (var e in foes) e.Disp = e.Pos; draw.Invoke(app, null);
      }
      GL.Tris.Clear(); GUI.Log.Clear();
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    }
    // BLOODBLAST=<s>: the Tank Hemomancer's Blood Blast (Core/BloodBlast.cs, Game/BloodBlastLook.cs) that long into its chain, Fire held
    // from 0: Crimson Slash at 0, Backcut 0.42 s, Thorn Flurry 0.82 s, Vein Whirl 1.32 s, Sanguine Cleave 1.9 s, Crescent Flight 2.68 s.
    // BBSTEP=<1-6>: start the chain at that step instead. BBALT=1: Alt (Crimson Tide) at 0 instead. BBSTACKS=<n>: the grunts start with
    // n stacks; BBKILL=<s>: they all die at that time (the Blood Vortex).
    if (Environment.GetEnvironmentVariable("BLOODBLAST") != null) {
      var ci = System.Globalization.CultureInfo.InvariantCulture;
      var gm = host.Game; gm.Bot = false; gm.Bullets.Clear(); gm.Effects.Clear(); gm.Enemies.Clear();
      me.Role = Role.Tank; me.Kit = Kit.Hemomancer; me.Def = Defs.Get(Role.Tank); me.Weapon = Defs.Weapon(Role.Tank, Kit.Hemomancer, 3); me.MaxHp = me.Hp = me.Def.Hp; foreach (var sl in me.Slots) sl.Id = AbilityId.None; foreach (var q0 in gm.Players.Values) { q0.AegisT = 0f; q0.UltT = 0f; }
      gm.Props.Clear(); gm.Layout = LayoutId.Open;
      bool alt = Environment.GetEnvironmentVariable("BBALT") != null;
      int step0 = int.Parse(Environment.GetEnvironmentVariable("BBSTEP") ?? "1"), stacks0 = int.Parse(Environment.GetEnvironmentVariable("BBSTACKS") ?? "0");
      float killAt = float.Parse(Environment.GetEnvironmentVariable("BBKILL") ?? "-1", ci);
      if (step0 > 1) { me.BlastStep = (byte)(step0 - 1); me.Combo = step0 - 1; }
      V2 home = new V2(500f, 420f); me.Pos = me.Disp = home; me.Aim = 0f;
      var spawnAt = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      var foes = new System.Collections.Generic.List<Enemy>();
      var spots = alt ? new[] { new V2(120f, -40f), new V2(210f, 50f), new V2(300f, -30f), new V2(-90f, 80f) }
                      : new[] { new V2(80f, -40f), new V2(100f, 45f), new V2(170f, 0f), new V2(-70f, -40f) };
      foreach (var at0 in spots) {
        var e = (Enemy)spawnAt.Invoke(gm, new object[] { EnemyType.Grunt, home + at0, 0f, 0, false }); e.Speed = 0f; e.FireT = 99f; e.SpawnT = 0f; e.MaxHp = e.Hp = 1e6f; e.Disp = e.Pos;
        e.BlastStacks = (byte)stacks0; e.BlastOwner = me.Id; foes.Add(e); }
      var draw = typeof(ArenaApp).GetMethod("DrawWorld", f2);
      int frames = Math.Max(1, (int)Math.Round(float.Parse(Environment.GetEnvironmentVariable("BLOODBLAST"), ci) * 60f));
      V2 cursor = home + new V2(300f, 0f);
      for (int f = 0; f < frames; f++) {
        if (killAt >= 0f && Math.Abs(f / 60f - killAt) < 0.5f / 60f) foreach (var e in foes) { e.Hp = 0f; }
        foreach (var e in foes) { e.Disp = e.Pos; e.FireT = 99f; }
        Time.unscaledDeltaTime = 1f / 60f; Time.unscaledTime += 1f / 60f;
        gm.FixedStep(1f / 60f, new PlayerInput { AimAt = cursor, Fire = !alt && killAt < 0f, WeaponAction = alt && f < 3 });
        gm.Update(1f / 60f);
        me.Pos = home; me.Disp = me.Pos; me.Aim = 0f; me.AimAt = cursor; me.Hp = me.MaxHp; foreach (var e in foes) e.Disp = e.Pos; draw.Invoke(app, null);
      }
      GL.Tris.Clear(); GUI.Log.Clear();
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    }
    // FROSTLANCE=<s>: the Tank Elementalist's Frost Lance (Core/FrostLance.cs, Game/FrostLanceLook.cs) that long into its chain, Fire
    // held from 0: Shiver Crescent at 0, Backhand Rime at 0.34 s, Frost Waltz at 0.68 s, Crystal Fan at 1.08 s, Avalanche Slam at 1.5 s.
    // FLALT=1: Alt (Absolute Zero) at 0 instead. FLCHILL=<n>: the grunts start with n chill (2: the next hit freezes them).
    if (Environment.GetEnvironmentVariable("FROSTLANCE") != null) {
      var ci = System.Globalization.CultureInfo.InvariantCulture;
      var gm = host.Game; gm.Bot = false; gm.Bullets.Clear(); gm.Effects.Clear(); gm.Enemies.Clear();
      me.Role = Role.Tank; me.Kit = Kit.Elementalist; me.Def = Defs.Get(Role.Tank); me.Weapon = Defs.Weapon(Role.Tank, Kit.Elementalist, 1); me.MaxHp = me.Hp = me.Def.Hp; foreach (var sl in me.Slots) sl.Id = AbilityId.None; foreach (var q0 in gm.Players.Values) { q0.AegisT = 0f; q0.UltT = 0f; }
      gm.Props.Clear(); gm.Layout = LayoutId.Open;
      bool alt = Environment.GetEnvironmentVariable("FLALT") != null;
      int chill0 = int.Parse(Environment.GetEnvironmentVariable("FLCHILL") ?? "0");
      V2 home = new V2(500f, 420f); me.Pos = me.Disp = home; me.Aim = 0f;
      var spawnAt = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      var foes = new System.Collections.Generic.List<Enemy>();
      var spots = alt ? new[] { new V2(200f, -60f), new V2(-180f, 90f), new V2(60f, 210f), new V2(-90f, -200f), new V2(230f, 120f), new V2(120f, -150f) }
                      : new[] { new V2(120f, -60f), new V2(150f, 40f), new V2(200f, -10f), new V2(90f, 90f), new V2(-80f, -70f), new V2(170f, 110f) };
      foreach (var at0 in spots) {
        var e = (Enemy)spawnAt.Invoke(gm, new object[] { EnemyType.Grunt, home + at0, 0f, 0, false }); e.Speed = 0f; e.FireT = 99f; e.SpawnT = 0f; e.MaxHp = e.Hp = 1e6f; e.Disp = e.Pos; e.FrostChill = (byte)chill0; e.FrostChillT = 99f; foes.Add(e); }
      var draw = typeof(ArenaApp).GetMethod("DrawWorld", f2);
      int frames = Math.Max(1, (int)Math.Round(float.Parse(Environment.GetEnvironmentVariable("FROSTLANCE"), ci) * 60f));
      V2 cursor = home + new V2(300f, 0f);
      for (int f = 0; f < frames; f++) {
        foreach (var e in foes) { e.Disp = e.Pos; e.FireT = 99f; }
        Time.unscaledDeltaTime = 1f / 60f; Time.unscaledTime += 1f / 60f; Time.time += 1f / 60f; Time.frameCount++;
        gm.FixedStep(1f / 60f, new PlayerInput { AimAt = cursor, Fire = !alt, WeaponAction = alt && f < 3 });
        gm.Update(1f / 60f);
        me.Pos = home; me.Disp = me.Pos; me.Aim = 0f; me.AimAt = cursor; me.Hp = me.MaxHp; foreach (var e in foes) e.Disp = e.Pos; draw.Invoke(app, null);
      }
      GL.Tris.Clear(); GUI.Log.Clear();
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    }
    // BULWARK=<s>: the Tank Knight's Bulwark Barrier (Core/BulwarkBarrier.cs, Game/BulwarkBarrierLook.cs) that long into a scene.
    // BBMODE=chain (default): Fire held from 0 (Rim Cut, Backhand, Boss Slam) at a row of grunts; guard: grunts shooting at you,
    // no input (the plate stops them); brace: the weapon-action key held, grunts shooting (shots soak into Ram); bash: braced at
    // full Ram, Fire at 0.3 s (the Shield Bash and its shards); melee: braced, Brutes walking into the plate (blocked melee
    // fills Ram). The chain runs on into the Shield Throw after the Boss Slam (about 1 s in).
    if (Environment.GetEnvironmentVariable("BULWARK") != null) {
      var ci = System.Globalization.CultureInfo.InvariantCulture;
      var gm = host.Game; gm.Bot = false; gm.Bullets.Clear(); gm.Effects.Clear(); gm.Enemies.Clear();
      gm.Props.Clear(); gm.Layout = LayoutId.Open;
      me.Role = Role.Tank; me.Kit = Kit.Knight; me.Def = Defs.Get(Role.Tank); me.Weapon = Defs.Weapon(Role.Tank, Kit.Knight, 3); me.MaxHp = me.Hp = me.Def.Hp; foreach (var sl in me.Slots) sl.Id = AbilityId.None; foreach (var q0 in gm.Players.Values) { q0.AegisT = 0f; q0.UltT = 0f; q0.BarrierT = 0f; q0.ShieldHp = 0f; for (int wi = 0; wi < Player.WallPlates; wi++) { q0.WallPlate[wi] = 0; q0.WallPlateHp[wi] = 0f; } }
      string mode = Environment.GetEnvironmentVariable("BBMODE") ?? "chain";
      bool shoot = mode == "guard" || mode == "brace", bash = mode == "bash", melee = mode == "melee";
      V2 home = new V2(bash ? 380f : 480f, 420f); me.Pos = me.Disp = home; me.Aim = 0f;
      var spawnAt = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      var foes = new System.Collections.Generic.List<Enemy>();
      var spots = mode == "chain" ? new[] { new V2(70f, -30f), new V2(80f, 35f), new V2(150f, 0f) }
                : melee ? new[] { new V2(90f, -40f), new V2(110f, 30f), new V2(140f, -5f) }
                : bash ? new[] { new V2(170f, -40f), new V2(190f, 30f), new V2(240f, -5f), new V2(300f, 50f) }
                : new[] { new V2(230f, -120f), new V2(260f, 0f), new V2(220f, 120f), new V2(160f, -200f), new V2(-200f, 60f) };
      foreach (var at0 in spots) {
        var e = (Enemy)spawnAt.Invoke(gm, new object[] { melee ? EnemyType.Brute : EnemyType.Grunt, home + at0, 0f, 0, false }); e.Speed = melee ? e.Speed : 0f; e.FireT = shoot ? 0.05f + 0.1f * foes.Count : 99f; e.SpawnT = 0f; e.MaxHp = e.Hp = 1e6f; e.Disp = e.Pos; foes.Add(e); }
      if (bash) { me.BbRam = Game.BbRamMax; me.BbBrace = true; typeof(Player).GetField("BbStored").SetValue(me, 12); typeof(Player).GetField("BbHeld").SetValue(me, true); }
      var draw = typeof(ArenaApp).GetMethod("DrawWorld", f2);
      int frames = Math.Max(1, (int)Math.Round(float.Parse(Environment.GetEnvironmentVariable("BULWARK"), ci) * 60f));
      V2 cursor = home + new V2(300f, 0f);
      for (int f = 0; f < frames; f++) {
        float tt = f / 60f;
        foreach (var e in foes) { e.Disp = e.Pos; if (!shoot) e.FireT = 99f; }
        Time.unscaledDeltaTime = 1f / 60f; Time.unscaledTime += 1f / 60f;
        gm.FixedStep(1f / 60f, new PlayerInput { AimAt = me.Pos + new V2(300f, 0f), Fire = mode == "chain" || bash && tt >= 0.3f && tt < 0.35f, WeaponAction = mode == "brace" || melee || bash && tt < 0.35f });
        gm.Update(1f / 60f);
        if (!bash) me.Pos = home;
        me.Disp = me.Pos; me.Aim = 0f; me.AimAt = me.Pos + new V2(300f, 0f); me.Hp = me.MaxHp; foreach (var e in foes) e.Disp = e.Pos; draw.Invoke(app, null);
      }
      GL.Tris.Clear(); GUI.Log.Clear();
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    }
    // ARCLOCK=<s>: Arc Lightning's lock-on chain (Core/ArcLock.cs, Game/ArcLockLook.cs) that long into the mockup's loop: fire held from 0,
    // the cursor sweeping down the cone (one enemy walks out of it, still tagged), let go at 1.5 s; the chain hops from there.
    // ARCFORK=1: Alt instead at 0 (Forked Volley at the enemies nearest the cursor), no hold.
    if (Environment.GetEnvironmentVariable("ARCLOCK") != null) {
      var ci = System.Globalization.CultureInfo.InvariantCulture;
      var gm = host.Game; gm.Bot = false; gm.Bullets.Clear(); gm.Effects.Clear(); gm.Enemies.Clear();
      me.Role = Role.Dps; me.Kit = Kit.Elementalist; me.Def = Defs.Get(Role.Dps); me.Weapon = Defs.Weapon(Role.Dps, Kit.Elementalist, 1); me.MaxHp = me.Hp = me.Def.Hp; foreach (var sl in me.Slots) sl.Id = AbilityId.None; foreach (var q0 in gm.Players.Values) { q0.AegisT = 0f; q0.UltT = 0f; }
      V2 home = new V2(380f, 450f); me.Pos = me.Disp = home; me.Aim = 0f;
      var spawnAt = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      var foes = new System.Collections.Generic.List<Enemy>();
      foreach (var at0 in new[] { new V2(150f, -60f), new V2(190f, 40f), new V2(240f, -130f), new V2(270f, 95f), new V2(300f, -20f), new V2(330f, 150f), new V2(360f, -190f), new V2(220f, 210f), new V2(410f, 60f), new V2(-140f, 120f), new V2(480f, -40f) }) {
        var e = (Enemy)spawnAt.Invoke(gm, new object[] { EnemyType.Grunt, home + at0, 0f, 0, false }); e.Speed = 0f; e.FireT = 99f; e.SpawnT = 0f; e.MaxHp = e.Hp = 1e6f; e.Disp = e.Pos; foes.Add(e); }
      bool fork = Environment.GetEnvironmentVariable("ARCFORK") != null;
      var draw = typeof(ArenaApp).GetMethod("DrawWorld", f2);
      int frames = Math.Max(1, (int)Math.Round(float.Parse(Environment.GetEnvironmentVariable("ARCLOCK"), ci) * 60f));
      for (int f = 0; f < frames; f++) {
        float tt = f / 60f;
        V2 cursor = fork ? home + new V2(290f, 20f) : home + V2.FromAngle(-0.55f + 0.75f * Math.Min(1f, tt / 1.4f)) * 300f;
        bool fire = !fork && tt < 1.5f;
        bool alt = fork && f < 3;
        foes[1].Pos = home + new V2(190f, 40f) + new V2(-120f, 150f) * Math.Min(1f, tt / 1.4f);  // walks out of the cone, still tagged
        foreach (var e in foes) { e.Disp = e.Pos; e.FireT = 99f; }
        Time.unscaledDeltaTime = 1f / 60f; Time.unscaledTime += 1f / 60f;
        gm.FixedStep(1f / 60f, new PlayerInput { AimAt = cursor, Fire = fire, WeaponAction = alt });
        gm.Update(1f / 60f);
        me.Pos = home; me.Disp = me.Pos; me.Aim = (cursor - me.Pos).Angle; me.AimAt = cursor; foreach (var e in foes) e.Disp = e.Pos; draw.Invoke(app, null);
      }
      GL.Tris.Clear(); GUI.Log.Clear();
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    }
    // OUTAMMO=<s>: the Soldier DPS's Out of Ammo (Core/OutOfAmmo.cs, Game/OutOfAmmoLook.cs) that long into a fight, a dummy close in front of you
    // (the rifle arms bash by themselves); KNIFE=1: fire held (the knife chain); OUTSPIN=1: Alt pressed 0.3 s in (the spin).
    if (Environment.GetEnvironmentVariable("OUTAMMO") != null) {
      var ci = System.Globalization.CultureInfo.InvariantCulture;
      var gm = host.Game; gm.Bot = false; gm.Bullets.Clear(); gm.Effects.Clear(); gm.Enemies.Clear();
      me.Role = Role.Dps; me.Kit = Kit.Soldier; me.Def = Defs.Get(Role.Dps); me.Weapon = Defs.Weapon(Role.Dps, Kit.Soldier, 5); me.MaxHp = me.Hp = me.Def.Hp; foreach (var sl in me.Slots) sl.Id = AbilityId.None; foreach (var q0 in gm.Players.Values) { q0.AegisT = 0f; q0.UltT = 0f; }
      V2 home = new V2(450f, 330f); me.Pos = me.Disp = home; me.Aim = 0f;
      var spawnAt = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      var dummy = (Enemy)spawnAt.Invoke(gm, new object[] { EnemyType.Brute, home + new V2(62f, 0f), 0f, 0, false }); dummy.Speed = 0f; dummy.FireT = 99f; dummy.SpawnT = 0f; dummy.MaxHp = dummy.Hp = 1e6f; dummy.Disp = dummy.Pos;
      bool spin = Environment.GetEnvironmentVariable("OUTSPIN") != null;
      var draw = typeof(ArenaApp).GetMethod("DrawWorld", f2);
      int frames = Math.Max(6, (int)Math.Round(float.Parse(Environment.GetEnvironmentVariable("OUTAMMO"), ci) * 60f));
      for (int f = 0; f < frames; f++) {
        me.Pos = me.Disp = home; me.Aim = 0f; dummy.Pos = dummy.Disp = home + new V2(62f, 0f); Time.unscaledDeltaTime = 1f / 60f; Time.unscaledTime += 1f / 60f;
        gm.FixedStep(1f / 60f, new PlayerInput { AimAt = home + new V2(300f, 0f), Fire = Environment.GetEnvironmentVariable("KNIFE") != null, WeaponAction = spin && f >= 18 && f < 21 });
        me.Disp = me.Pos = home; me.Aim = 0f; dummy.Disp = dummy.Pos; draw.Invoke(app, null);
      }
      GL.Tris.Clear(); GUI.Log.Clear();
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    }
    // MOVEFEEL=walk|stop|dash (MoveFeel.cs): running right for 0.6 s and then: walk keeps running (the stretch, footstep dust),
    // stop lets go 0.07 s before the picture (the squash), dash dodges 0.09 s before it (the ghosts, the burst, the stretch).
    // MFOFF=1: the same moment with the movement feel cleared each frame (the before). A teammate does the same below you. Mode hud.
    if (Environment.GetEnvironmentVariable("MOVEFEEL") != null) {
      string mf = Environment.GetEnvironmentVariable("MOVEFEEL");
      bool mfOff = Environment.GetEnvironmentVariable("MFOFF") != null;
      var gm = host.Game; gm.Bot = false; gm.Bullets.Clear(); gm.Effects.Clear(); gm.Enemies.Clear();
      var draw = typeof(ArenaApp).GetMethod("DrawWorld", f2);
      var clearFeel = typeof(ArenaApp).GetMethod("ClearMoveFeel", f2);
      var partN = typeof(ArenaApp).GetField("partN", f2);
      me.Role = Role.Dps; me.Kit = Kit.Soldier; me.Def = Defs.Get(Role.Dps); me.Weapon = Defs.Weapon(Role.Dps, Kit.Soldier, 0); me.MaxHp = me.Hp = me.Def.Hp; foreach (var sl in me.Slots) sl.Id = AbilityId.None; foreach (var q0 in gm.Players.Values) { q0.AegisT = 0f; q0.UltT = 0f; }
      me.Pos = me.Disp = new V2(260f, 380f); me.Aim = 0f;
      int run = 36, extra = mf == "stop" ? 4 : mf == "dash" ? 5 : 0;
      for (int f = 0; f < run + extra; f++) {
        Time.unscaledDeltaTime = 1f / 60f; Time.unscaledTime += 1f / 60f;
        bool going = mf != "stop" || f < run;
        gm.FixedStep(1f / 60f, new PlayerInput { Move = going ? new V2(1f, 0f) : V2.Zero, Dodge = mf == "dash" && f == run, AimAt = me.Pos + new V2(200f, -40f) });
        me.Disp = me.Pos;
        foreach (var q in gm.Players.Values)
          if (q != me) { q.Pos = q.Disp = me.Pos + new V2(-40f, 90f); q.Aim = 0f; }
        if (mfOff) { clearFeel.Invoke(app, null); partN.SetValue(app, 0); }
        draw.Invoke(app, null);
      }
      Console.Error.WriteLine("MOVEFEEL at " + me.Pos.X.ToString("0") + "," + me.Pos.Y.ToString("0"));
      GL.Tris.Clear(); GUI.Log.Clear();
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    }
    // SPRINT=<s since it began>: the Dodge's held sprint (Dodge.cs): you dodged right and kept the key down, running right
    // that long into the sprint, with its fading pale-blue wake (NinjaLooks.cs DrawSprintWake); with 2 players a teammate
    // runs alongside, drawn from the replicated Player.Sprint. SPRINT=0 fresh; mode hud.
    if (Environment.GetEnvironmentVariable("SPRINT") != null) {
      var ci = System.Globalization.CultureInfo.InvariantCulture;
      var gm = host.Game; gm.Bot = false; gm.Bullets.Clear(); gm.Effects.Clear(); gm.Enemies.Clear();
      var draw = typeof(ArenaApp).GetMethod("DrawWorld", f2);
      me.Role = Role.Dps; me.Kit = Kit.Soldier; me.Def = Defs.Get(Role.Dps); me.Weapon = Defs.Weapon(Role.Dps, Kit.Soldier, 0); me.MaxHp = me.Hp = me.Def.Hp; foreach (var sl in me.Slots) sl.Id = AbilityId.None; foreach (var q0 in gm.Players.Values) { q0.AegisT = 0f; q0.UltT = 0f; }
      me.Pos = me.Disp = new V2(300f, 450f); me.Aim = 0f;
      float at = float.Parse(Environment.GetEnvironmentVariable("SPRINT"), ci);
      int frames = (int)Math.Round((Game.DodgeTime + Game.SprintHold + at) * 60f);
      for (int f = 0; f < frames; f++) {
        Time.unscaledDeltaTime = 1f / 60f; Time.unscaledTime += 1f / 60f;
        gm.FixedStep(1f / 60f, new PlayerInput { Move = new V2(1f, 0f), Dodge = f == 0, DodgeHeld = true, AimAt = me.Pos + new V2(200f, -60f) });
        me.Disp = me.Pos;
        foreach (var q in gm.Players.Values)
          if (q != me) { q.Pos = q.Disp = me.Pos + new V2(-60f, 140f); q.Sprint = me.Sprint; }  // a teammate running alongside, its wake from Player.Sprint
        draw.Invoke(app, null);
      }
      GL.Tris.Clear(); GUI.Log.Clear();
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    }
    // TANKALT=<s>: the Tank Soldier's Alt fires (TankAltLook.cs): you with the Scattergun and one Shock Grenade loaded (the next filling),
    // a Shock Grenade lobbed that long ago (flight 0.5 s, then the burst), an armed Proximity Shell, one still flying in, and a Tear Gas cloud. FXOFF=1 for plain looks.
    if (Environment.GetEnvironmentVariable("TANKALT") != null) {
      var ci = System.Globalization.CultureInfo.InvariantCulture;
      var gm = host.Game; gm.Bot = false; gm.Bullets.Clear(); gm.Effects.Clear(); gm.Enemies.Clear(); gm.Zones.Clear();
      me.Role = Role.Tank; me.Kit = Kit.Soldier; me.Def = Defs.Get(Role.Tank); me.Weapon = Defs.Weapon(Role.Tank, Kit.Soldier, 0); me.MaxHp = me.Hp = me.Def.Hp; foreach (var sl in me.Slots) sl.Id = AbilityId.None; foreach (var q0 in gm.Players.Values) { q0.AegisT = 0f; q0.UltT = 0f; }
      V2 home = new V2(420f, 360f); me.Pos = me.Disp = home; me.Aim = 0f;
      me.ScatShells = 1; me.ScatCharge = 9;
      V2 aimAt = home + new V2(200f, 0f);
      var draw = typeof(ArenaApp).GetMethod("DrawWorld", f2);
      var add = typeof(Game).GetMethod("AddZone", BindingFlags.NonPublic | BindingFlags.Instance);
      Func<ZoneKind, V2, float, float, float, Zone> Z = (k, zp, r, life, age) => { var z = (Zone)add.Invoke(gm, new object[] { k, me, zp, r, life }); z.T = life - age; z.Aim = (zp - home).Angle; return z; };
      Z(ZoneKind.FlakMine, new V2(760f, 220f), Game.MineBurst, Game.MineLife, 3f);
      Z(ZoneKind.FlakMine, new V2(600f, 600f), Game.MineBurst, Game.MineLife, Game.MineArm * 0.55f);
      Z(ZoneKind.TearGas, new V2(800f, 520f), Game.TearGasRadius, Game.TearGasFlight + Game.TearGasLife, 2f);
      Action frame = () => { me.Pos = me.Disp = home; me.Aim = 0f; Time.unscaledDeltaTime = 1f / 60f; Time.unscaledTime += 1f / 60f; foreach (var z in gm.Zones.Values) if (z.Kind != ZoneKind.ShockGrenade) z.T += 1f / 60f; gm.FixedStep(1f / 60f, new PlayerInput { AimAt = aimAt }); me.Disp = me.Pos = home; me.Aim = 0f; draw.Invoke(app, null); };
      for (int f = 0; f < 6; f++) frame();
      me.ScatShells = 2;
      typeof(Game).GetMethod("HostDoubleBarrel", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(gm, new object[] { me, aimAt });
      me.ScatShells = 1; me.ScatCharge = 9;
      float at = float.Parse(Environment.GetEnvironmentVariable("TANKALT"), ci);
      for (int f = 0; f < Math.Round(at * 60f); f++) frame();
      GL.Tris.Clear(); GUI.Log.Clear();
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    }
    // SHOTW=<WeaponId>: that weapon in your hand (Arc Lightning: use ARCLOCK=), three volleys fired by the game's own code and moved on 0.06 / 0.18 /
    // 0.3 s, aiming right, the arena otherwise empty (for comparing shot looks with Docs/Mockups/projectiles.html).
    if (Environment.GetEnvironmentVariable("SHOTW") != null) {
      var gm = host.Game; gm.Bot = false; gm.Bullets.Clear(); gm.Effects.Clear(); gm.Enemies.Clear();
      var wd = System.Array.Find(Defs.Weapons, x => x.Id.ToString() == Environment.GetEnvironmentVariable("SHOTW"));
      me.Weapon = wd; me.Kit = wd.Kit; me.Pos = me.Disp = new V2(420f, 450f); me.Aim = 0f; me.DrawT = 0f;
      var fireV = typeof(Game).GetMethod("FireVolley", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
      if (wd.Id == WeaponId.ChainLight) {
        // Chain Light: three enemies where the mockup has them (chain-light.html), one cast, the frame SHOTT s into it.
        var spawnE = typeof(Game).GetMethod("SpawnEnemyAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        foreach (var sp in new[] { new V2(-0.25f, 140f), new V2(0.3f, 205f), new V2(-0.45f, 270f) }) {
          var en = (Enemy)spawnE.Invoke(gm, new object[] { EnemyType.Grunt, me.Pos + V2.FromAngle(sp.X) * sp.Y, 0f, 0, false });
          en.SpawnT = 0f; en.Disp = en.Pos; en.MaxHp = en.Hp = 1e5f;
        }
        fireV.Invoke(gm, new object[] { me, 0f, 0 });
        float st2 = float.Parse(Environment.GetEnvironmentVariable("SHOTT") ?? "0.1", System.Globalization.CultureInfo.InvariantCulture);
        foreach (var fx in gm.Effects) fx.T = st2;
      }
      else foreach (float age in new[] { 0.3f, 0.18f, 0.06f }) {
        var before = new System.Collections.Generic.HashSet<int>(gm.Bullets.Keys);
        fireV.Invoke(gm, new object[] { me, 0f, 0 });
        foreach (var bl in gm.Bullets.Values) if (!before.Contains(bl.Id)) { bl.Pos += bl.Vel * age; bl.Life -= age; }
      }
    }
    // SWING=<seconds>: your melee weapon's attack that far in (with KNIGHT=, NINJA= or BLOOD=; MeleeLooks.cs draws it
    // with Visual effects on). Built like the game's own effects: Slash with the weapon's half arc (negative: a
    // thrust), a Halberd step (SWING = seconds after the click, HALBERDSTEP=1..4, PIKE=1 the pike set), Jab boxes for the Lance and the Warhammer.
    // PERFECTPARRY=<s since the catch>: your shield up and a perfect parry's flash, with its needle flying back (ParryLook.cs).
    if (Environment.GetEnvironmentVariable("PERFECTPARRY") != null) {
      float pt = float.Parse(Environment.GetEnvironmentVariable("PERFECTPARRY"), System.Globalization.CultureInfo.InvariantCulture);
      var gm = host.Game; gm.Bot = false; gm.Effects.Clear(); gm.Bullets.Clear();
      me.Pos = me.Disp = new V2(700f, 470f); me.Aim = -0.4f; me.Parrying = true; me.ParryHp = Game.ParryMax(me);
      V2 hitAt = me.Pos + V2.FromAngle(me.Aim) * (me.Def.BodyR + Game.ParryReachOf(me) * 0.55f);
      gm.Effects.Add(new Effect { Kind = FxKind.PerfectParry, Pos = hitAt, Pos2 = new V2(me.Id, me.Aim), Color = me.Def.Color, Radius = Game.ParryArc(me), T = pt, Dur = Game.PerfectFxTime });
      var needleShot = new Bullet { Id = 999999, Kind = Defs.ParryNeedle, Owner = me.Id, Pos = hitAt + V2.FromAngle(me.Aim) * (Game.NeedleSpeed * pt), Vel = V2.FromAngle(me.Aim) * Game.NeedleSpeed, Radius = 4f, Life0 = 1f, Life = 1f - pt };
      gm.Bullets[needleShot.Id] = needleShot;
    }
    // FUMA=orbit|lash|cyclone|steal FUMAT=<clock s> (with NINJA=2: the Fuma Shuriken's Orbit stance at that clock; FumaLook.cs).
    if (Environment.GetEnvironmentVariable("FUMA") != null) {
      string fm = Environment.GetEnvironmentVariable("FUMA");
      float ft = float.Parse(Environment.GetEnvironmentVariable("FUMAT") ?? "1", System.Globalization.CultureInfo.InvariantCulture);
      var gm = host.Game; gm.Bot = false; gm.Effects.Clear(); gm.Bullets.Clear();
      me.Pos = me.Disp = new V2(640f, 420f); me.Aim = 0f; me.Flash = 0f;
      me.FumaStance = 1; me.FumaClock = ft; me.FumaClk0 = 0f; me.FumaAng0 = 0f; me.FumaSpin = Game.FumaOmega; me.FumaR0 = 0f; me.FumaStars = 1;
      me.FumaMove = 0; me.FumaLashAim = me.Aim;
      if (fm == "lash") { me.FumaMove = 1; me.FumaMoveAt = ft - Game.FumaLashTime * 0.5f; }
      if (fm == "cyclone") { me.FumaMove = 3; me.FumaMoveAt = ft - Game.FumaCycloneTime * 0.5f; }
      if (fm == "steal") { me.FumaMinis = 3; me.FumaMiniAt = ft - 0.5f; me.Parrying = true; }
      int k = 0;
      foreach (var en in gm.Enemies.Values) { if (en.Def.IsBoss) continue; en.Pos = en.Disp = me.Pos + V2.FromAngle(0.9f + k * 2.1f) * (110f + 40f * k); if (++k >= 3) break; }
      foreach (var en in gm.Enemies.Values) if (!en.Def.IsBoss && k-- <= 0) { en.Pos = en.Disp = new V2(150f, 120f); }
    }
    // OATHPICK=1|0 (Guardian's Oath waiting for the click: 1 with the cursor on an ally, 0 off them; GuardiansOathLook.cs; 3 players).
    if (Environment.GetEnvironmentVariable("OATHPICK") != null) {
      var gm = host.Game; gm.Bot = false; gm.Effects.Clear(); gm.Bullets.Clear();
      me.Kit = Kit.Knight; me.Role = Role.Healer; me.Def = Defs.Get(Role.Healer); me.Weapon = Defs.Weapon(Role.Healer, Kit.Knight, 0);
      me.Slots[1].Id = AbilityId.GuardiansOath; gm.OathPickSlot = 1;
      me.Pos = me.Disp = new V2(520f, 480f); me.Flash = 0f;
      int k = 0;
      foreach (var q in gm.Players.Values) if (q.Id != me.Id) { q.Pos = q.Disp = me.Pos + (k == 0 ? new V2(330f, -170f) : new V2(-120f, -300f)); k++; }
      Player tgt = null; foreach (var q in gm.Players.Values) if (q.Id != me.Id) { tgt = q; break; }
      me.AimAt = Environment.GetEnvironmentVariable("OATHPICK") == "1" && tgt != null ? tgt.Pos + new V2(6f, 4f) : me.Pos + new V2(200f, 120f);
      me.Aim = (me.AimAt - me.Pos).Angle;
      foreach (var en in gm.Enemies.Values) { if (en.Def.IsBoss) continue; en.Pos = en.Disp = me.Pos + new V2(380f, -40f + 30f * (k++ % 3)); }
    }
    // GREATSTAR=held|draw|fly GSAT=<px along the route> (the DPS Shuriken's Great Star stance: in your hand, a route being drawn,
    // or the star thrown along one, that far along; GreatStarLook.cs). Any NINJA= works: it puts the Shuriken in your hand.
    if (Environment.GetEnvironmentVariable("GREATSTAR") != null) {
      string gs = Environment.GetEnvironmentVariable("GREATSTAR");
      float gat = float.Parse(Environment.GetEnvironmentVariable("GSAT") ?? "520", System.Globalization.CultureInfo.InvariantCulture);
      var gm = host.Game; gm.Bot = false; gm.Effects.Clear(); gm.Bullets.Clear();
      me.Weapon = System.Array.Find(Defs.Weapons, x => x.Id == WeaponId.Shuriken); me.Kit = Kit.Ninja;
      me.Pos = me.Disp = new V2(420f, 470f); me.Aim = -0.5f; me.Flash = 0f; me.StarStance = 1;
      var corners = new[] { new V2(160f, -170f), new V2(380f, -200f), new V2(540f, -90f), new V2(580f, 50f), new V2(720f, 110f) };
      var route = new System.Collections.Generic.List<V2>();
      V2 last = me.Pos;
      foreach (var c in corners) { V2 to = me.Pos + c; for (float u = 16f; u < last.DistanceTo(to); u += 16f) route.Add(last + (to - last).Normalized() * u); route.Add(to); last = to; }
      int k = 0;
      foreach (var en in gm.Enemies.Values) { if (en.Def.IsBoss) continue; V2 at = me.Pos + corners[Math.Min(4, k + 1)] + new V2(-30f, 10f); en.Pos = en.Disp = at; if (++k >= 3) break; }
      foreach (var en in gm.Enemies.Values) if (!en.Def.IsBoss && k-- <= 0) { en.Pos = en.Disp = new V2(150f, 120f); }
      if (gs == "draw") {
        gm.GreatStarRoute.Clear();
        foreach (var q in route) if (Game.InkLength(me.Pos, gm.GreatStarRoute) < gat) gm.GreatStarRoute.Add(q);
        me.Aim = (gm.GreatStarRoute[gm.GreatStarRoute.Count - 1] - me.Pos).Angle;
      }
      if (gs == "fly") {
        V2 at = me.Pos, dir = new V2(1f, 0f); float left = gat; last = me.Pos;
        foreach (var q in route) { float d = last.DistanceTo(q); if (d >= left) { dir = (q - last).Normalized(); at = last + dir * left; left = 0f; break; } left -= d; last = q; at = q; }
        gm.Bullets[999998] = new Bullet { Id = 999998, Kind = Defs.GreatStar, Owner = me.Id, Pos = at, Vel = dir * Game.GreatStarSpeed, Radius = Defs.BulletRadius(Defs.GreatStar), Life0 = 6f, Life = 6f };
      }
    }
    if (Environment.GetEnvironmentVariable("SWING") != null) {
      float st = float.Parse(Environment.GetEnvironmentVariable("SWING"), System.Globalization.CultureInfo.InvariantCulture);
      var gm = host.Game; gm.Bot = false; gm.Effects.Clear(); gm.Bullets.Clear();
      // SWINGW=<WeaponId>: any melee weapon in your hand (e.g. Greatsword, Rapier, Halberd, Warhammer, Katana, BoStaff).
      var wn = Environment.GetEnvironmentVariable("SWINGW");
      if (wn != null) { var wd = System.Array.Find(Defs.Weapons, x => x.Id.ToString() == wn); if (wd != null) { me.Weapon = wd; me.Kit = wd.Kit; } }
      // SKIN=<skin id>: your weapon skin, as your lobby entry carries it (Expertise.cs), e.g. SKIN=twin-chicken with SWINGW=TwinBlades.
      var skinId = Environment.GetEnvironmentVariable("SKIN");
      if (skinId != null && Expertise.Skin(skinId) != null) { LobbyEntry le; if (!host.Lobby.TryGetValue(me.Id, out le)) host.Lobby[me.Id] = le = new LobbyEntry { Id = me.Id, Name = me.Name }; le.Skins = new byte[] { (byte)Expertise.Skin(skinId).Weapon, Expertise.IndexOf(skinId) }; }
      me.Pos = me.Disp = new V2(700f, 470f); me.Aim = -0.15f; me.DrawT = 0f; me.Flash = 0f;  // (no hit flash: the model shows its own colours)
      int k = 0;
      foreach (var en in gm.Enemies.Values) { if (en.Def.IsBoss) continue; float a = me.Aim + (k - 1) * 0.55f; en.Pos = en.Disp = me.Pos + V2.FromAngle(a) * (70f + 25f * (k % 2)); if (++k >= 3) break; }
      foreach (var en in gm.Enemies.Values) if (!en.Def.IsBoss && k-- <= 0) { en.Pos = en.Disp = new V2(150f, 120f); }
      var col = Defs.BulletColor(Defs.KnightSlash);
      float reach = Game.MeleeReach(me, me.Weapon.Reach);
      V2 to = me.Pos + V2.FromAngle(me.Aim) * reach;
      Action<FxKind, V2, float, float, float> fx = (kind, p2, rad, t0, dur) => gm.Effects.Add(new Effect { Kind = kind, Pos = me.Pos, Pos2 = p2, Color = col, Radius = rad, T = st - t0, Dur = dur });
      switch (me.Weapon.Id) {
        case WeaponId.Warhammer: gm.Effects.Add(new Effect { Kind = FxKind.Jab, Pos = me.Pos, Pos2 = me.Pos + V2.FromAngle(me.Aim) * Game.HammerCentre(reach), Color = Game.HammerBoomColor.WithAlpha(float.Parse(Environment.GetEnvironmentVariable("HAMMERPOW") ?? "1", System.Globalization.CultureInfo.InvariantCulture)), Radius = Game.HammerRadius, T = st, Dur = 0.26f }); break;
        case WeaponId.Lance:  // LANCESTEP=1..4 Jab, Second Jab, Wide Sweep, Breaching Thrust, hook Hook & Haul (at the nearest enemy), 0 held at the ready; PRIMED=1 a hook primed the thrust (SWING = seconds after the click)
        {
          string lsn = Environment.GetEnvironmentVariable("LANCESTEP") ?? "1";
          me.LancePrimed = Environment.GetEnvironmentVariable("PRIMED") == "1";
          if (lsn == "hook")
          {
            Enemy near = null; foreach (var en in gm.Enemies.Values) if (!en.Def.IsBoss && (near == null || en.Pos.DistanceTo(me.Pos) > near.Pos.DistanceTo(me.Pos))) near = en;
            V2 at = near != null ? near.Pos : me.Pos + V2.FromAngle(me.Aim) * Game.HookMiss;
            if (near != null) { near.Pos = near.Disp = me.Pos + V2.FromAngle(me.Aim) * 230f; at = near.Pos; }
            gm.Effects.Add(new Effect { Kind = FxKind.LanceHook, Pos = me.Pos, Pos2 = at, Color = Game.LanceGlow, Radius = me.Id * 10 + (near != null ? 1 : 0), T = st, Dur = LanceMotion.Hook.Dur + LanceMotion.Recover });
          }
          else if (lsn != "0")
          {
            int ls = int.Parse(lsn);
            float lr = ls == 4 ? Game.MeleeReach(me, me.Weapon.Reach + Game.BreachExtraReach) : ls == 3 ? LanceMotion.Len + 17f : reach;
            me.Combo = (1 << 3) | ls;
            gm.Effects.Add(new Effect { Kind = FxKind.LanceStep, Pos = me.Pos, Pos2 = me.Pos + V2.FromAngle(me.Aim) * lr, Color = Game.LanceGlow.WithAlpha(ls == 4 ? Game.BreachHalfWidth / 100f : 0f), Radius = me.Id * 10 + ls, T = st, Dur = LanceMotion.Steps[ls - 1].Dur + LanceMotion.Recover });
          }
          break;
        }
        case WeaponId.Rapier:  // TEMPO=<0-20> its Tempo (GRACE=<0-1> of the grace period left; 0 = draining); FLOURISH=1 a Flourish sweep instead of a thrust
          me.RapierTempo = int.Parse(Environment.GetEnvironmentVariable("TEMPO") ?? "0");
          me.RapierGraceLeft = (byte)(255f * float.Parse(Environment.GetEnvironmentVariable("GRACE") ?? "1", System.Globalization.CultureInfo.InvariantCulture));
          if (Environment.GetEnvironmentVariable("FLOURISH") == "1") fx(FxKind.Slash, me.Pos + V2.FromAngle(me.Aim) * reach * 0.75f, 0.95f, 0f, 0.2f);
          else fx(FxKind.Slash, to, -0.12f, 0f, 0.16f);
          break;
        case WeaponId.BloodLash:  // the lash (110 degrees; SNAP=1 the Snap, 36 degrees out to 190 px); CHAINS=1 chains on every enemy within 340 px (1-4 links); LEASH=<s> Martyr's Leash held that long
        {
          if (Environment.GetEnvironmentVariable("SNAP") == "1") fx(FxKind.Slash, me.Pos + V2.FromAngle(me.Aim) * Game.LashSnapReach, Game.LashSnapArc * MathX.Pi / 360f, 0f, 0.16f);
          else fx(FxKind.Slash, to, 110f * MathX.Pi / 360f, 0f, 0.16f);
          string leash = Environment.GetEnvironmentVariable("LEASH");
          if (Environment.GetEnvironmentVariable("CHAINS") == "1" || leash != null)
          {
            int n = 0;
            var near = new System.Collections.Generic.List<Enemy>();
            foreach (var en in gm.Enemies.Values) if ((en.Pos - me.Pos).Length < 340f) near.Add(en);
            foreach (var en in near) gm.Enemies.Remove(en.Id);  // a fresh crowd spread round the aim, so each chain reads
            float[] offs = { -0.9f, -0.45f, 0f, 0.4f, 0.85f };
            float[] dists = { 120f, 175f, 230f, 150f, 205f };
            for (int i = 0; i < offs.Length; i++)
            {
              var t2 = i == 2 ? EnemyType.Brute : EnemyType.Grunt; var d2 = Defs.Get(t2);
              V2 at = me.Pos + V2.FromAngle(me.Aim + offs[i]) * dists[i];
              var ce = new Enemy { Id = 960 + i, Type = t2, Def = d2, Radius = d2.Radius, Pos = at, Disp = at, Hp = d2.Hp, MaxHp = d2.Hp, Facing = (me.Pos - at).Angle };
              gm.Enemies[ce.Id] = ce;
              me.Chains.Add(ce.Id); me.ChainLinks.Add((byte)(1 + n++ % 4));
            }
          }
          if (leash != null) { me.LeashOn = true; me.LeashT = float.Parse(leash, System.Globalization.CultureInfo.InvariantCulture); }
          break;
        }
        case WeaponId.Kusarigama: gm.Effects.Add(new Effect { Kind = FxKind.Slash, Pos = me.Pos, Pos2 = me.Pos + V2.FromAngle(me.Aim) * Game.MeleeReach(me, Game.KusariCutReach), Color = col, Radius = Game.KusariCutArc * MathX.Pi / 360f, T = st, Dur = 0.16f }); break;  // the sickle cut
        case WeaponId.Halberd:  // HALBERDSTEP=1..5 Sweep, Return Sweep, Overhead Chop, Reaping Wheel, Pike Lunge (SWING = seconds after the click); PIKE=1: Set the Pike, turned to the nearest enemy (POKE=<s since a poke>, its jab)
        {
          int hs = int.Parse(Environment.GetEnvironmentVariable("HALBERDSTEP") ?? "1");
          bool pike = Environment.GetEnvironmentVariable("PIKE") == "1";
          int j = 0;
          foreach (var en in gm.Enemies.Values) {
            if (en.Def.IsBoss) continue;
            float a = pike ? me.Aim + 0.5f + j * 0.7f : hs == 3 || hs == 5 ? me.Aim + (j - 1) * 0.06f : me.Aim + (j - 1) * 0.55f + (hs == 4 && j == 2 ? 2.4f : 0f);
            float dd = pike ? 205f + 60f * j : hs == 3 ? 95f + 65f * j : hs == 5 ? 150f + 55f * j : 140f + 20f * (j % 2);
            en.Pos = en.Disp = me.Pos + V2.FromAngle(a) * dd;
            if (++j >= 3) break;
          }
          if (pike) {
            me.HbSet = true; me.HbPikeA = me.Aim + 0.5f;
            string pk = Environment.GetEnvironmentVariable("POKE");
            if (pk != null) gm.Effects.Add(new Effect { Kind = FxKind.HalberdPoke, Pos = me.Pos, Pos2 = me.Pos + V2.FromAngle(me.HbPikeA) * Game.PikeReach(me, 0), Color = Game.HbGlow, Radius = me.Id, T = float.Parse(pk, System.Globalization.CultureInfo.InvariantCulture), Dur = Game.PikeFxTime });
          }
          else fx(FxKind.HalberdStep, to, me.Id * 10 + hs, 0f, HalberdMotion.StepTotal(hs));
          break;
        }
        case WeaponId.BearTotem:  // two claw swipes (left hand across, right hand back), a Bear beside you and the three enemies bleeding (BLEEDHITS=<stacks>, default 3)
        {
          foreach (int sd in new[] { 1, -1 })
            gm.Effects.Add(new Effect { Kind = FxKind.Claw, Pos = me.Pos, Pos2 = me.Pos + V2.FromAngle(me.Aim + sd * 0.12f) * reach, Color = Game.BearClawColor, Radius = sd * 55f * MathX.Pi / 180f, T = st - (sd > 0 ? 0f : 0.12f), Dur = 0.2f });
          int hits = int.Parse(Environment.GetEnvironmentVariable("BLEEDHITS") ?? "3");
          foreach (var en in gm.Enemies.Values) if (!en.Def.IsBoss && (en.Pos - me.Pos).Length < 130f) { en.Bleeding = hits > 0; en.BleedHits = hits; }
          var bearAt = me.Pos + new V2(-60f, 70f);
          int bid = 9001; gm.Minions[bid] = new Minion { Id = bid, Owner = me.Id, Kind = MinionKind.Bear, Pos = bearAt, Disp = bearAt, Aim = me.Aim, T = -1f, Companion = true };
          break;
        }
        case WeaponId.Quarterstaff:  // STAFFJAB=1 the jab's cone (else the swipe), STACKS=<n> Jab stacks held
          me.StaffStacks = (byte)int.Parse(Environment.GetEnvironmentVariable("STACKS") ?? "2");
          if (Environment.GetEnvironmentVariable("STAFFJAB") == "1") fx(FxKind.Slash, me.Pos + V2.FromAngle(me.Aim) * Game.MeleeReach(me, Game.StaffJabReach), -Game.StaffJabArc * MathX.Pi / 360f, 0f, 0.24f);
          else fx(FxKind.Slash, to, Game.StaffSwipeArc * MathX.Pi / 360f, 0f, 0.16f);
          break;
        case WeaponId.Greatsword:  // GREATSTEP=1..10 a step of the chain (SWING = seconds after the press: its own pose, then its Slash, ring or lance once it lands), GREATSTEP=wheel the Moonwheel 180 px out
        {
          string gsn = Environment.GetEnvironmentVariable("GREATSTEP") ?? "1";
          if (gsn == "wheel") { me.FangOut = 2; me.FangAt = me.Pos + V2.FromAngle(me.Aim) * 180f; break; }
          int gi = int.Parse(gsn);
          var gm2 = Game.GreatMoveOf(gi);
          var gd = Game.GreatMoves[(int)gm2];
          me.GreatStep = (byte)gi;
          me.Combo = 1 << 3;
          ArenaApp.GreatPreviewAge = st;
          float since = st - gd.Wind;  // the hit landed this long ago
          if (since < 0f) break;
          float half = 0f, rch = 0f;
          switch (gm2)
          {
            case Game.GreatMove.Undertow: half = Game.UndertowArc; rch = Game.UndertowReach; break;
            case Game.GreatMove.Cross: half = Game.CrossArc; rch = Game.CrossReach; break;
            case Game.GreatMove.Backstep: half = Game.BackstepArc; rch = Game.BackstepReach; break;
            case Game.GreatMove.FullMoon: half = 360f; rch = Game.MoonReach; break;
          }
          if (rch > 0f)
          {
            float aimOff = gm2 == Game.GreatMove.Cross ? -0.35f : 0f;
            gm.Effects.Add(new Effect { Kind = FxKind.Slash, Pos = me.Pos, Pos2 = me.Pos + V2.FromAngle(me.Aim + aimOff) * Game.MeleeReach(me, rch), Color = col, Radius = half * MathX.Pi / 360f, T = since, Dur = 0.24f });
          }
          else if (gm2 == Game.GreatMove.Leap)
            gm.Effects.Add(new Effect { Kind = FxKind.Ring, Pos = me.Pos, Color = col, Radius = Game.MeleeReach(me, Game.LeapRadius), T = since, Dur = 0.4f });
          else if (gm2 == Game.GreatMove.Eclipse)
          {
            gm.Effects.Add(new Effect { Kind = FxKind.GreatRend, Pos = me.Pos, Pos2 = me.Pos + V2.FromAngle(me.Aim) * Game.MeleeReach(me, Game.EclipseReach), Color = col.WithAlpha(1f), Radius = Game.EclipseHalfWidth, T = since, Dur = Game.GreatRendFxTime });
            gm.Effects.Add(new Effect { Kind = FxKind.Ring, Pos = me.Pos + V2.FromAngle(me.Aim) * Game.EclipseRingAt, Color = new Rgba(1f, 0.84f, 0.51f), Radius = Game.EclipseRing, T = since, Dur = 0.4f });
          }
          break;
        }
        case WeaponId.Censer:  // CENSERSTEP=1..3 a step of the Thurible Rite (default 1), CENSERSTEP=cast Censer Cast (SWING = seconds after the click), EMBERS=<n> held
        {
          string csn = Environment.GetEnvironmentVariable("CENSERSTEP") ?? "1";
          me.Embers = (byte)int.Parse(Environment.GetEnvironmentVariable("EMBERS") ?? "3");
          if (csn == "cast") gm.Effects.Add(new Effect { Kind = FxKind.CenserCast, Pos = me.Pos, Pos2 = me.Pos + V2.FromAngle(me.Aim) * 240f, Color = Game.CenserGold, Radius = me.Id * 10 + 5, T = st, Dur = CenserMotion.CastTotal });
          else { int ci = int.Parse(csn); me.Combo = (1 << 3) | ci; float cr = Game.MeleeReach(me, me.Weapon.Reach + (ci == 3 ? Game.BenedictionReach : 0f));
            gm.Effects.Add(new Effect { Kind = FxKind.CenserStep, Pos = me.Pos, Pos2 = me.Pos + V2.FromAngle(me.Aim) * cr, Color = Game.CenserGold, Radius = me.Id * 10 + ci, T = st, Dur = CenserMotion.Steps[ci - 1].Dur + CenserMotion.Recover }); }
          break;
        }
        case WeaponId.ArcKnuckles:  // KNUCKLESTEP=1..5 a punch's look (1 jab, 2 cross, 3 Pin Punch, 4 Quake Punch, 5 Counter Hammer; default 1), 0 the guard; dash a Slip Dash / Rocket Dash; flurry|grab|punch the Rocket chain (flurry is the Barrage too), prompt its wait for a click; MOVE=<0-7> the Brawl move next (its pips; ROCKET=1 the Rocket stance); CHARGE=<0-1> the Quake Punch's charge ring; ARMED=1 the Counter Hammer armed; SWING = seconds after the press
        {
          string kns = Environment.GetEnvironmentVariable("KNUCKLESTEP") ?? "1";
          me.KnuckleArmed = Environment.GetEnvironmentVariable("ARMED") == "1";
          me.KnuckleStance = (byte)(Environment.GetEnvironmentVariable("ROCKET") == "1" ? 1 : 0);
          me.KnuckleMove = byte.Parse(Environment.GetEnvironmentVariable("MOVE") ?? "0");
          me.KnuckleCharge = (byte)Math.Round(255f * float.Parse(Environment.GetEnvironmentVariable("CHARGE") ?? "0", System.Globalization.CultureInfo.InvariantCulture));
          if (kns == "dash")  // a dash, SWING = seconds after the press: you part-way along it
          {
            V2 d0 = me.Pos - V2.FromAngle(me.Aim) * Game.KnuckleDashLen * Math.Min(1f, st / Game.KnuckleDashTime), d1 = d0 + V2.FromAngle(me.Aim) * Game.KnuckleDashLen;
            gm.Effects.Add(new Effect { Kind = FxKind.KnuckleDash, Pos = d0, Pos2 = d1, Color = Game.ArcBlue, Radius = me.Id, T = st, Dur = Game.KnuckleDashTime + Game.KnuckleDashFxAfter });
            break;
          }
          if (kns == "flurry" || kns == "grab" || kns == "punch" || kns == "prompt")  // the Rocket chain (SWING = seconds into the move); prompt: waiting for the click
          {
            if (kns == "prompt") { me.KnuckleChain = 1; break; }
            int mv = kns == "flurry" ? 1 : kns == "grab" ? 2 : 3;
            me.KnuckleChain = (byte)(mv * 2);
            gm.Effects.Add(new Effect { Kind = FxKind.KnuckleChain, Pos = me.Pos, Pos2 = me.Pos + V2.FromAngle(me.Aim) * (mv == 3 ? Game.RocketPunchReach : 60f), Color = Game.ArcBlue, Radius = me.Id * 10 + mv, T = st, Dur = mv == 1 ? Game.FlurryDur : mv == 2 ? Game.GrabDur : Game.RocketPunchDur });
            if (mv == 2 && st >= Game.GrabLatch)  // the grabbed enemy beside the fist (thrown along the aim after the release)
            {
              Enemy held = null; foreach (var en in gm.Enemies.Values) if (!en.Def.IsBoss) { held = en; break; }
              if (held != null) { V2 hp = st < Game.GrabRelease ? Game.KnuckleHoldPoint(me.Pos, me.Aim, st) : Game.KnuckleHoldPoint(me.Pos, me.Aim, Game.GrabRelease) + V2.FromAngle(me.Aim) * Game.KnuckleThrowSpeed * (st - Game.GrabRelease); held.Pos = held.Disp = hp; }
            }
            break;
          }
          int kn = int.Parse(kns);
          if (kn > 0)
          {
            float far = kn == 4 ? Game.KnuckleQuakeR1 : kn == 5 ? Game.KnuckleHammerAt : Math.Min(reach, 110f);
            bool full = kn == 4 && Environment.GetEnvironmentVariable("FULL") == "1";
            gm.Effects.Add(new Effect { Kind = FxKind.KnuckleStep, Pos = me.Pos, Pos2 = me.Pos + V2.FromAngle(me.Aim) * far, Color = kn == 5 || full ? Game.ArcGold : Game.ArcBlue, Radius = me.Id * 10 + kn, T = st, Dur = kn >= 4 ? Game.KnuckleLand[kn - 1] + 0.7f + Game.KnuckleAftershock * int.Parse(Environment.GetEnvironmentVariable("EXTRA") ?? "0") : Game.KnuckleDur[kn - 1] });  // EXTRA=<n>: the slam's aftershocks
          }
          break;
        }
        case WeaponId.TwinBlades:  // TWINSTEP=1..5 a step of the Crescent Waltz (default 1), TWINSTEP=fang Twin Fang's wind-up (SWING = seconds after the click; past the release the pair spins 180 px out along the aim)
        {
          string tsn = Environment.GetEnvironmentVariable("TWINSTEP") ?? "1";
          if (tsn == "fang")
          {
            gm.Effects.Add(new Effect { Kind = FxKind.TwinFang, Pos = me.Pos, Pos2 = me.Pos + V2.FromAngle(me.Aim) * 180f, Color = Game.TwinMoon, Radius = me.Id, T = st, Dur = TwinBladesMotion.FangRelease + 0.3f });
            if (st >= TwinBladesMotion.FangRelease) { me.FangOut = 2; me.FangAt = me.Pos + V2.FromAngle(me.Aim) * 180f; }
          }
          else { int tsi = int.Parse(tsn); me.Combo = (1 << 3) | tsi; gm.Effects.Add(new Effect { Kind = FxKind.TwinStep, Pos = me.Pos, Pos2 = to, Color = Game.TwinMoon, Radius = me.Id * 10 + tsi, T = st, Dur = TwinBladesMotion.Steps[tsi - 1].Dur + TwinBladesMotion.Recover }); }
          break;
        }
        case WeaponId.Lightbringer:  // LIGHTSTEP=1..3 Dawn, Mercy, Noon, 4 a Radiant Noon, lance the Sunlance (to the other player, 2 players); LIGHT=<n> pips; WARD=1 a Mercy ward on everyone (SWING = seconds after the click)
        {
          string lsn = Environment.GetEnvironmentVariable("LIGHTSTEP") ?? "1";
          me.Light = int.Parse(Environment.GetEnvironmentVariable("LIGHT") ?? "5");
          if (Environment.GetEnvironmentVariable("WARD") == "1") foreach (var q in gm.Players.Values) q.MercyT = Game.MercyTime - 0.7f;
          Player ally = null; foreach (var q in gm.Players.Values) if (q.Id != me.Id) { ally = q; q.Pos = q.Disp = me.Pos + V2.FromAngle(me.Aim - 0.5f) * (lsn == "3" || lsn == "4" ? 110f : 260f); }
          // LIGHTSTEP=cut: a Dawn crescent (and a Mercy one, CRES=mercy) SWING seconds into its flight; WARD=1 WARDHIT=<s>: a shot struck the ward that long ago
          if (lsn == "cut")
          {
            bool mer = Environment.GetEnvironmentVariable("CRES") == "mercy";
            V2 cd = V2.FromAngle(me.Aim);
            gm.Bullets[99001] = new Bullet { Id = 99001, Kind = Defs.KnightWave, Owner = me.Id, Pos = me.Pos + cd * (me.Def.BodyR + 600f * st), Vel = cd * 600f, Life = 0.45f - st, Life0 = 0.45f, Radius = Defs.BulletRadius(Defs.KnightWave), Look = mer ? ShotLook.Crescent : ShotLook.Dart };
            if (ally != null) { ally.Pos = ally.Disp = me.Pos + cd * 330f; gm.Effects.Add(new Effect { Kind = FxKind.LightMark, Pos = ally.Pos, Pos2 = new V2(mer ? 2 : 1, 0f), Color = Game.LightGold.WithAlpha(0.5f), Radius = ally.Id, T = st - 0.4f, Dur = 0.85f }); }
            break;
          }
          if (Environment.GetEnvironmentVariable("WARDHIT") != null && ally != null)
            gm.Effects.Add(new Effect { Kind = FxKind.LightMark, Pos = ally.Pos, Pos2 = new V2(3, 0f), Color = Game.LightGold, Radius = ally.Id, T = float.Parse(Environment.GetEnvironmentVariable("WARDHIT"), System.Globalization.CultureInfo.InvariantCulture), Dur = 0.4f });
          if (lsn == "lance") gm.Effects.Add(new Effect { Kind = FxKind.Sunlance, Pos = me.Pos, Pos2 = ally != null ? ally.Pos : me.Pos + V2.FromAngle(me.Aim) * 300f, Color = Game.LightGold, Radius = me.Id * 1000 + (ally != null ? ally.Id : 0), T = st, Dur = LightbringerMotion.LanceRelease + LightbringerMotion.LanceLightTail });
          else
          {
            int ls = int.Parse(lsn), step = ls == 4 ? 3 : ls;
            float lr = step == 3 ? (ls == 4 ? Game.NoonRadiantRadius : Game.NoonRadius) : reach;
            me.Combo = (1 << 3) | step;
            if (step == 3 && ally != null)  // the ally's heal burst as the ring reaches them
            {
              V2 ringAt = me.Pos + V2.FromAngle(me.Aim) * Game.NoonAt;
              float lag = Game.NoonMarkLag * Math.Min(1f, ally.Pos.DistanceTo(ringAt) / lr), since = st - LightbringerMotion.Steps[2].Dur * LightbringerMotion.NoonHit;
              if (since >= 0f) gm.Effects.Add(new Effect { Kind = FxKind.LightMark, Pos = ally.Pos, Pos2 = new V2(1, lag), Color = Game.LightGold.WithAlpha(ls == 4 ? 1f : 0.5f), Radius = ally.Id, T = since, Dur = lag + 0.85f });
            }
            gm.Effects.Add(new Effect { Kind = FxKind.LightStep, Pos = me.Pos, Pos2 = me.Pos + V2.FromAngle(me.Aim) * lr, Color = Game.LightGold, Radius = me.Id * 10 + ls, T = st, Dur = step < 3 ? LightbringerMotion.Steps[step - 1].Dur + LightbringerMotion.Recover : LightbringerMotion.Steps[2].Dur * LightbringerMotion.NoonHit + LightbringerMotion.NoonLightTail });
          }
          break;
        }
        case WeaponId.Katana:  // KATANA=hayate|senbon[,hay,sen] the stance and where each combo waits; KATANAMOVE=dash|sweep|spin|slash|burst|caltrops|kill|idle
        {
          var kp = (Environment.GetEnvironmentVariable("KATANA") ?? "hayate").Split(',');
          me.KatanaStance = (byte)(kp[0] == "senbon" ? 1 : 0);
          if (kp.Length > 2) { me.KatanaHay = byte.Parse(kp[1]); me.KatanaSen = byte.Parse(kp[2]); }
          string mv = Environment.GetEnvironmentVariable("KATANAMOVE") ?? "sweep";
          var kcol = me.KatanaStance == 0 ? Game.KatanaSteel : Game.KatanaRose;
          Enemy first = null; foreach (var en in gm.Enemies.Values) if (!en.Def.IsBoss) { first = en; break; }
          if (mv == "dash") gm.Effects.Add(new Effect { Kind = FxKind.KatanaDash, Pos = me.Pos - V2.FromAngle(me.Aim) * Game.KatanaDashLen * Math.Min(1f, st / Game.KatanaDashTime), Pos2 = me.Pos + V2.FromAngle(me.Aim) * Game.KatanaDashLen * (1f - Math.Min(1f, st / Game.KatanaDashTime)), Color = kcol, Radius = me.Id, T = st, Dur = 0.42f });
          else if (mv == "sweep") gm.Effects.Add(new Effect { Kind = FxKind.KatanaSweep, Pos = me.Pos, Pos2 = new V2(me.Aim - 1.13f, 2.27f), Color = kcol, Radius = me.Id * 1000 + Game.MeleeReach(me, me.Weapon.Reach), T = st, Dur = Game.KatanaSweepTime });
          else if (mv == "spin") gm.Effects.Add(new Effect { Kind = FxKind.KatanaSweep, Pos = me.Pos, Pos2 = new V2(me.Aim, MathX.Tau), Color = kcol, Radius = me.Id * 1000 + Game.KatanaMoonReach, T = st, Dur = Game.KatanaMoonTime });
          else if (mv == "slash") for (int sk = 0; sk < 6; sk++) { float a = me.Aim + (sk - 2.5f) * 0.22f, la = a + 1.2f + sk * 0.4f; V2 c = me.Pos + V2.FromAngle(a) * (70f + 10f * sk), h = V2.FromAngle(la) * 47f; gm.Effects.Add(new Effect { Kind = FxKind.KatanaSlash, Pos = c - h, Pos2 = c + h, Color = Game.KatanaRose, Radius = me.Id, T = st - 0.05f * sk * 0.3f, Dur = 0.3f }); }
          else if (mv == "burst") { gm.Effects.Add(new Effect { Kind = FxKind.KatanaBurst, Pos = me.Pos, Pos2 = new V2(me.Id, me.Aim), Color = Game.KatanaSteel, Radius = Game.KatanaBurstRadius, T = st, Dur = 0.9f }); foreach (var en in gm.Enemies.Values) if (!en.Def.IsBoss) en.KatanaShocked = true; }
          else if (mv == "caltrops") for (int ck = 0; ck < 16; ck++) { V2 at = me.Pos + V2.FromAngle(ck * MathX.Tau / 16f + 0.2f) * (60f + 90f * ((ck * 7) % 5) / 4f); var cb = new Bullet { Id = 990000 + ck, Kind = Defs.Caltrop, Owner = me.Id, Pos = at, Vel = V2.Zero, Radius = 5f, Life0 = 8.25f, Life = 6f }; gm.Bullets[cb.Id] = cb; }
          else if (mv == "kill" && first != null) gm.Effects.Add(new Effect { Kind = FxKind.KatanaCut, Pos = first.Pos, Pos2 = V2.FromAngle(me.Aim + 1.3f) * first.Def.Sides, Color = first.Def.Color.WithAlpha(1f), Radius = first.Radius, T = st, Dur = 0.9f });
          break;
        }
        case WeaponId.BoStaff:  // BOSTEP=0 slash, 1 jab, 2 spin, 3 shove (the Bo Staff's chain; default the slash)
        {
          int bs = int.Parse(Environment.GetEnvironmentVariable("BOSTEP") ?? "0");
          V2 bdir = V2.FromAngle(me.Aim);
          if (bs == 0) fx(FxKind.Slash, to, Game.BoSlashArc * MathX.Pi / 360f, 0f, 0.16f);
          else if (bs == 1) fx(FxKind.Slash, me.Pos + bdir * Game.MeleeReach(me, Game.BoJabReach), -0.3f, 0f, 0.24f);
          else if (bs == 2) fx(FxKind.Slash, me.Pos + bdir * Game.MeleeReach(me, Game.BoSpinReach), MathX.Pi, 0f, 0.24f);
          else fx(FxKind.StaffShove, me.Pos + bdir * Game.BoShoveReach, Game.BoShoveHalf, 0f, Game.BoShoveTime + 0.45f);
          break;
        }
        default: fx(FxKind.Slash, to, 1.31f, 0f, 0.24f); break;
      }
      gm.Effects.RemoveAll(e => e.T < 0f);
    }
    // KNOCK=<seconds>: the first few regular enemies that far into being knocked back 120 px (Knock.cs), with their
    // dust streak and impact puff.
    if (Environment.GetEnvironmentVariable("KNOCK") != null) {
      float kt = float.Parse(Environment.GetEnvironmentVariable("KNOCK"), System.Globalization.CultureInfo.InvariantCulture);
      host.Game.Bot = false;
      int n = 0;
      foreach (var en in host.Game.Enemies.Values) {
        if (en.Def.IsBoss || n >= 4) continue;
        V2 dir = (en.Pos - me.Pos).Length > 1f ? (en.Pos - me.Pos).Normalized() : new V2(1f, 0f);
        V2 from = en.Pos, to = en.Pos + dir * 120f;
        en.Disp = en.Pos = from + (to - from) * Math.Min(1f, kt / Game.KnockTime);
        host.Game.Effects.Add(new Effect { Kind = FxKind.Knock, Pos = from, Pos2 = to, Color = en.Def.Color, Radius = en.Radius, T = kt, Dur = Game.KnockFxDur });
        n++;
      }
    }
    if (Environment.GetEnvironmentVariable("AIMLINE") != null)
    {
      // Point the mouse up and to the right of the player: AIMLINE=<distance> (1 = 380 px; screen y is flipped).
      float aimDist = float.Parse(Environment.GetEnvironmentVariable("AIMLINE")); if (aimDist <= 1f) aimDist = 380f;
      V2 target = me.Pos + V2.FromAngle(-0.5f) * aimDist;
      float ws = 1280f / Game.Width;
      Input.mousePosition = new Vector3(target.X * ws, 720f - target.Y * ws, 0f);
      me.Aim = (target - me.Pos).Angle;
    }
    if (Environment.GetEnvironmentVariable("REPULSE") != null) {
      // The Repulsion Cannon's blast REPULSE=<seconds> after the shot.
      float rt = float.Parse(Environment.GetEnvironmentVariable("REPULSE"));
      me.Pos = me.Disp = new V2(600f, 450f); me.Aim = -0.2f; host.Game.Bot = false;
      host.Game.Effects.Add(new Effect { Kind = FxKind.Repulse, Pos = me.Pos, Pos2 = me.Pos + V2.FromAngle(me.Aim) * 253f, Color = new Rgba(0.55f, 0.95f, 1f), Radius = 0.61f, Dur = Game.RepulseFxTime, T = rt });
    }
    if (Environment.GetEnvironmentVariable("HAMMERFX") != null) {
      // The Storm Hammer's throw and slam (StormHammerLook.cs). HAMMERFX=throw:<s since the click> | slam:<s since the click> | wave:<s since the impact>
      var hp = Environment.GetEnvironmentVariable("HAMMERFX").Split(':');
      float ha = float.Parse(hp[1], System.Globalization.CultureInfo.InvariantCulture);
      var gm = host.Game; gm.Bot = false; gm.Effects.Clear(); gm.Bullets.Clear();
      var hw = System.Array.Find(Defs.Weapons, x => x.Id == WeaponId.StormHammer); me.Weapon = hw; me.Kit = hw.Kit;
      me.Pos = me.Disp = new V2(330f, 450f); me.Aim = 0f; me.DrawT = 0f;
      V2 crater = me.Pos + new V2(Game.HammerSlamReach, 0f);
      var foes = new System.Collections.Generic.List<Enemy>(gm.Enemies.Values); foes.RemoveAll(x => x.Def.IsBoss);
      float[][] spots = { new[] { 120f, 0f }, new[] { 170f, -1.1f }, new[] { 175f, 1.0f }, new[] { 230f, 0.3f }, new[] { 250f, -0.5f }, new[] { 90f, 2.6f } };
      for (int k = 0; k < foes.Count && k < spots.Length; k++) foes[k].Pos = foes[k].Disp = crater + V2.FromAngle(spots[k][1]) * spots[k][0];
      for (int k = spots.Length; k < foes.Count; k++) foes[k].Pos = foes[k].Disp = new V2(1500f, 120f + 30f * k);
      if (hp[0] == "throw") {
        gm.Effects.Add(new Effect { Kind = FxKind.HammerThrow, Pos = me.Pos, Pos2 = new V2(0f, 0f), Color = Game.HammerStormColor, Radius = me.Id, T = ha, Dur = Game.HammerThrowDur });
        var sbt = typeof(Game).GetMethod("SpawnBullet", BindingFlags.NonPublic | BindingFlags.Instance);
        if (ha >= Game.HammerThrowRelease) { var hb = sbt.Invoke(gm, new object[] { Defs.HammerShot, me.Pos + new V2(32f, 0f), new V2(460f, 0f), 0f, 1.1f, 40f, me.Id, false, 0f }) as Bullet; if (hb != null) hb.Pos += new V2(460f * (ha - Game.HammerThrowRelease), 0f); }
      } else if (hp[0] == "slam") {
        gm.Effects.Add(new Effect { Kind = FxKind.HammerThrow, Pos = me.Pos, Pos2 = new V2(1f, 0f), Color = Game.HammerStormColor, Radius = me.Id, T = ha, Dur = Game.HammerSlamBack });
      } else {
        gm.Effects.Add(new Effect { Kind = FxKind.HammerThrow, Pos = me.Pos, Pos2 = new V2(1f, 0f), Color = Game.HammerStormColor, Radius = me.Id, T = ha + Game.HammerSlamWind, Dur = Game.HammerSlamBack });
        gm.Effects.Add(new Effect { Kind = FxKind.HammerSlam, Pos = crater, Pos2 = me.Pos, Color = Game.HammerStormColor, Radius = Game.HammerSlamRadius, T = ha, Dur = 2.4f });
      }
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
    if (Environment.GetEnvironmentVariable("SHOTLOOKS") != null) {
      // Each weapon's new shot look (ShotLooks.cs), one row per weapon, the player's weapon switched for each.
      var sbs = typeof(Game).GetMethod("SpawnBullet", BindingFlags.NonPublic | BindingFlags.Instance);
      host.Game.Enemies.Clear(); host.Game.Bullets.Clear(); host.Game.Bot = false;
      var rows = new[] { WeaponId.TwinRifle, WeaponId.Railgun, WeaponId.Revolver, WeaponId.Scattergun, WeaponId.IceShards };
      var kinds = new[] { Defs.DpsShot, Defs.RailShot, Defs.HealerShot, Defs.TankShot, Defs.ArcaneShot, Defs.ArcaneShot };
      int pid = 100;
      for (int i = 0; i < rows.Length; i++) {
        // A stand-in player per row so each shot knows its weapon.
        var stand = new Player { Id = pid + i, Name = "", Role = Role.Dps, Def = Defs.Get(Role.Dps), Weapon = Defs.WeaponOf(rows[i]), Pos = new V2(-500f, -500f), Disp = new V2(-500f, -500f) };
        host.Game.Players[stand.Id] = stand;
        for (int k = 0; k < 4; k++)
          sbs.Invoke(host.Game, new object[] { kinds[i], new V2(380f + k * 230f, 110f + i * 120f), new V2(900f, k % 2 == 0 ? 0f : -150f), 0f, 1f, 10f, stand.Id, false, 0f });
      }
    }
    if (Environment.GetEnvironmentVariable("ROOM") != null) {
      // A challenge room (Challenges.cs): ROOM=portal|defend|boss|rush|zones|mirror|won|lost (won/lost: KIND=defend...),
      // ROOMAGE=<seconds into the phase> (default 1).
      var sep = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      var gg = host.Game; string room = Environment.GetEnvironmentVariable("ROOM");
      float age = Environment.GetEnvironmentVariable("ROOMAGE") != null ? float.Parse(Environment.GetEnvironmentVariable("ROOMAGE")) : 1f;
      gg.Enemies.Clear(); gg.Bullets.Clear(); gg.Props.Clear(); gg.Patches.Clear(); gg.Bot = false; gg.CurrentPath = PathId.None;
      foreach (var pl in gg.Players.Values) { pl.Offer = null; pl.PendingPicks = 0; }
      Func<EnemyType, V2, Enemy> put = (ty, at) => { var en = (Enemy)sep.Invoke(gg, new object[] { ty, at, 0f, 0, false }); en.SpawnT = 0f; en.Disp = en.Pos; en.Facing = (me.Pos - at).Angle; return en; };
      Func<string, ChallengeKind> kindOf = n => n == "defend" ? ChallengeKind.Defend : n == "boss" ? ChallengeKind.MiniBoss : n == "rush" ? ChallengeKind.KillRush : n == "zones" ? ChallengeKind.HoldZones : ChallengeKind.Mirror;
      var rps = new System.Collections.Generic.List<Player>(gg.Players.Values);
      for (int i = 0; i < rps.Count; i++) rps[i].Pos = rps[i].Disp = new V2(700f + i * 160f, 640f);
      if (room == "portal") {
        gg.State = MatchState.Intermission; gg.ChallengePhase = RoomPhase.Portal; gg.ChallengeNow = ChallengeKind.HoldZones; gg.RoomT = 11.4f; gg.PortalPos = new V2(1150f, 200f); gg.PortalFill = 0.55f;
        rps[0].Pos = rps[0].Disp = new V2(1030f, 380f);
      } else {
        gg.State = MatchState.Fighting;
        gg.ChallengeNow = kindOf(room == "won" || room == "lost" ? (Environment.GetEnvironmentVariable("KIND") ?? "defend") : room);
        gg.ChallengePhase = room == "won" ? RoomPhase.Won : room == "lost" ? RoomPhase.Lost : Environment.GetEnvironmentVariable("ROOMINTRO") != null ? RoomPhase.Intro : RoomPhase.Active;
        if (gg.ChallengePhase == RoomPhase.Intro) gg.RoomT = 2.2f;
        gg.RoomClock = Challenges.Clock(gg.ChallengeNow) * 0.55f; gg.CrystalPos = gg.AltarPos = new V2(800f, 450f); gg.CrystalMax = 700f; gg.CrystalHp = 420f;
        gg.RoomGoal = 44; gg.RoomKills = 27; gg.ZoneIndex = 1; gg.ZoneFill = 0.6f;
        gg.RoomZones[0] = Challenges.ZoneSpots[0]; gg.RoomZones[1] = Challenges.ZoneSpots[1]; gg.RoomZones[2] = Challenges.ZoneSpots[2];
        if (gg.ChallengePhase == RoomPhase.Active) {
          if (gg.ChallengeNow == ChallengeKind.Defend) { put(EnemyType.Grunt, new V2(560f, 300f)); put(EnemyType.Swarmer, new V2(690f, 380f)); put(EnemyType.Brute, new V2(1000f, 520f)); rps[0].Pos = rps[0].Disp = new V2(760f, 560f); }
          if (gg.ChallengeNow == ChallengeKind.KillRush) for (int i = 0; i < 9; i++) put(i % 3 == 0 ? EnemyType.Grunt : EnemyType.Swarmer, new V2(300f + i * 120f, 250f + (i % 3) * 90f));
          if (gg.ChallengeNow == ChallengeKind.HoldZones) { rps[0].Pos = rps[0].Disp = gg.RoomZones[1] + new V2(-30f, 20f); put(EnemyType.Grunt, gg.RoomZones[1] + new V2(140f, -90f)); put(EnemyType.Sniper, new V2(800f, 160f)); }
          if (gg.ChallengeNow == ChallengeKind.MiniBoss) { var gk = put(EnemyType.Gatekeeper, new V2(800f, 330f)); gk.Hp = gk.MaxHp * 0.42f; gk.Phase = 1; gk.Facing = (rps[0].Pos - gk.Pos).Angle; }
          if (gg.ChallengeNow == ChallengeKind.Mirror) foreach (var pl in rps) { var sh = put(EnemyType.Shadow, new V2(1600f - pl.Pos.X, 900f - pl.Pos.Y)); gg.ShadowOf[sh.Id] = pl.Id; sh.Facing = (pl.Pos - sh.Pos).Angle; }
        }
      }
      set("lookPhase", gg.ChallengePhase); set("lookPhaseAt", Time.unscaledTime - age); set("wasInRoom", gg.InRoom); set("warpAt", -10f);
    }
    if (Environment.GetEnvironmentVariable("APPROACH") != null) {
      // The Warden's faction (APPROACH=1): Riot Shields walking, winding up, lunging, held low, cracked and broken,
      // Gas Grenadiers and a gas cloud, Rioters and a Guard; the approach chip shows above the wave panel.
      var sep = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      var spp = typeof(Game).GetMethod("SpawnPatchAt", BindingFlags.NonPublic | BindingFlags.Instance);
      host.Game.Enemies.Clear(); host.Game.Bullets.Clear(); host.Game.Patches.Clear(); host.Game.Bot = false;
      me.Pos = me.Disp = new V2(800f, 470f);
      Func<EnemyType, V2, Enemy> put = (ty, at) => { var en = (Enemy)sep.Invoke(host.Game, new object[] { ty, at, 0f, 0, false }); en.SpawnT = 0f; en.Disp = en.Pos; en.Facing = (me.Pos - at).Angle; return en; };
      Action<Enemy, byte, byte> riot = (en, st, crack) => en.Phase = (byte)(st << 3 | crack);
      riot(put(EnemyType.RiotShield, new V2(690f, 470f)), Game.RiotWinding, 0);
      riot(put(EnemyType.RiotShield, new V2(560f, 300f)), Game.RiotWalk, 0);
      riot(put(EnemyType.RiotShield, new V2(980f, 330f)), Game.RiotLunging, 1);
      riot(put(EnemyType.RiotShield, new V2(1040f, 600f)), Game.RiotLow, 2);
      riot(put(EnemyType.RiotShield, new V2(560f, 640f)), Game.RiotWalk, 3);
      riot(put(EnemyType.RiotShield, new V2(1230f, 470f)), Game.RiotFlail, Game.RiotCracked);
      put(EnemyType.GasGrenadier, new V2(300f, 450f)); put(EnemyType.GasGrenadier, new V2(1350f, 220f));
      spp.Invoke(host.Game, new object[] { PatchKind.Gas, new V2(880f, 560f), 0f, 4f, 70f, 70f });
      foreach (var pt in host.Game.Patches.Values) pt.Age = 1.2f;
      for (int k = 0; k < 4; k++) put(EnemyType.Rioter, new V2(260f + k * 34f, 160f + (k % 2) * 30f));
      put(EnemyType.Guard, new V2(220f, 230f));
    }
    if (Environment.GetEnvironmentVariable("VECPICK") != null) {
      // VECPICK=1: vector pickups (Game/VectorPickupLook.cs, Docs/Mockups/vector-pickup.html look D) beside gold coins: three small, one big. Mode hud.
      host.Game.Enemies.Clear(); host.Game.Bullets.Clear(); host.Game.Bot = false;
      me.Pos = me.Disp = new V2(900f, 470f);
      int cid = 9000;
      foreach (var q in new[] { new V2(980f, 400f), new V2(1050f, 500f), new V2(960f, 540f), new V2(1100f, 420f) }) {
        bool big = cid == 9003;
        host.Game.Coins[++cid] = new Coin { Id = cid, Vec = true, Value = big ? Game.VectorDropBig : Game.VectorDropSmall, Pos = q, Disp = q };
      }
      host.Game.Coins[9100] = new Coin { Id = 9100, Value = 5, Pos = new V2(1000f, 470f), Disp = new V2(1000f, 470f) };
      host.Game.Coins[9101] = new Coin { Id = 9101, Value = 1, Pos = new V2(1030f, 450f), Disp = new V2(1030f, 450f) };
      host.Game.Coins[9102] = new Coin { Id = 9102, Vec = true, Value = 1, Pos = new V2(860f, 560f), Disp = new V2(860f, 560f) };  // a kill's 1-vector pickup
      if (Environment.GetEnvironmentVariable("PICKOWN") != null)  // PICKOWN=1: per-player pickups (Docs/RoundFlow.md step 3): every other one is a teammate's copy (hidden); use 2 players
        foreach (var co in host.Game.Coins.Values) co.Owner = co.Id % 2 == 0 ? me.Id + 1 : me.Id;
      if (Environment.GetEnvironmentVariable("VECFULL") != null) {  // VECFULL=1: your stock is full, so they wait ("FULL" over them, the chip pulsing amber)
        me.Vectors = me.VectorCap;
        typeof(ArenaApp).GetField("vecFullNear", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(app, true);
      }
    }
    if (Environment.GetEnvironmentVariable("BLOODFLOOR") != null) {
      // BLOODFLOOR=<0-1>: you as a DPS Hemomancer at that share of health, for the blood floor's tick on the health bars
      // (Game/HealthBars.cs DrawBloodFloor; 0.2: at the floor). Mode hud, 2 players (the teammate a Hemomancer too).
      float bf = float.Parse(Environment.GetEnvironmentVariable("BLOODFLOOR"), System.Globalization.CultureInfo.InvariantCulture);
      foreach (var q in host.Game.Players.Values) {
        q.Kit = Kit.Hemomancer; q.Role = Role.Dps; q.Def = Defs.Get(Role.Dps); q.Weapon = Defs.Weapon(Role.Dps, Kit.Hemomancer, 1);
        q.MaxHp = q.Def.Hp * Defs.KitHpMult(Kit.Hemomancer); q.ShieldHp = 0f; q.Hp = q == me ? q.MaxHp * bf : q.MaxHp * 0.6f;
      }
    }
    if (Environment.GetEnvironmentVariable("BLOODORB") != null) {
      // BLOODORB=1: a Hemomancer's blood orbs (Game/OrbLook.cs DrawBloodOrb; each only its Hemomancer's) beside a small and a big health orb. Mode hud.
      host.Game.Enemies.Clear(); host.Game.Bullets.Clear(); host.Game.Bot = false; host.Game.Orbs.Clear();
      me.Pos = me.Disp = new V2(900f, 470f);
      int oid = 9200;
      foreach (var q in new[] { new V2(960f, 410f), new V2(1000f, 520f) })
        { ++oid; host.Game.Orbs[oid] = new Orb { Id = oid, Blood = true, Pos = q, Disp = q, Life = Game.BloodOrbLife, Owner = me.Id }; }
      host.Game.Orbs[9210] = new Orb { Id = 9210, Pos = new V2(840f, 540f), Disp = new V2(840f, 540f), Life = 10f };
      host.Game.Orbs[9211] = new Orb { Id = 9211, Big = true, Pos = new V2(820f, 400f), Disp = new V2(820f, 400f), Life = 10f };
    }
    if (Environment.GetEnvironmentVariable("SWEEP") != null) {
      // SWEEP=<seconds>: the floor-loot sweep as a wave starts (Game/LootSweepLook.cs, Docs/RoundFlow.md step 4): your coins streaming in
      // from round the arena and "+86 GOLD COLLECTED" over you, that long in (0.3: mid-flight; 0.8: the label). Mode hud.
      host.Game.Enemies.Clear(); host.Game.Bullets.Clear(); host.Game.Bot = false;
      me.Pos = me.Disp = new V2(760f, 470f);
      float sweepT = float.Parse(Environment.GetEnvironmentVariable("SWEEP"), System.Globalization.CultureInfo.InvariantCulture);
      var sweepFrom = new[] { new V2(1100f, 300f), new V2(1150f, 340f), new V2(1080f, 380f), new V2(420f, 640f), new V2(460f, 600f), new V2(380f, 300f), new V2(1000f, 700f), new V2(1040f, 660f), new V2(560f, 220f), new V2(900f, 200f) };
      typeof(ArenaApp).GetMethod("PreviewLootSweep", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(app, new object[] { sweepT, 86, sweepFrom });
    }
    if (Environment.GetEnvironmentVariable("GADGET") != null) {
      // GADGET=1: the Magnet (Game/GadgetLook.cs): you holding one switched on with coins and a vector pickup flying in, one on the
      // floor (part used), one thrown mid-air. Mode hud.
      host.Game.Enemies.Clear(); host.Game.Bullets.Clear(); host.Game.Bot = false;
      me.Pos = me.Disp = new V2(760f, 470f); me.Aim = 0.3f;
      me.Gadget = GadgetId.Magnet; me.GadgetOn = true; me.GadgetLeft = 6.4f;
      int cid = 9200;
      foreach (var q in new[] { new V2(900f, 420f), new V2(960f, 520f), new V2(1050f, 380f), new V2(640f, 600f), new V2(560f, 360f), new V2(1150f, 560f) }) {
        ++cid; host.Game.Coins[cid] = new Coin { Id = cid, Value = cid % 2 == 0 ? 5 : 1, Pos = q, Disp = q };
      }
      host.Game.Coins[9300] = new Coin { Id = 9300, Vec = true, Value = Game.VectorDropSmall, Pos = new V2(870f, 600f), Disp = new V2(870f, 600f) };
      var fl = host.Game.SpawnGadget(GadgetId.Magnet, new V2(500f, 640f), 3.5f);
      var th = host.Game.SpawnGadget(GadgetId.Magnet, new V2(1200f, 300f), Game.MagnetTime);
      th.From = new V2(1100f, 380f); th.To = new V2(1340f, 260f); th.FlyT = Game.GadgetFlyTime * 0.45f; th.Pos = th.Disp = V2.Lerp(th.From, th.To, 0.45f);
    }
    if (Environment.GetEnvironmentVariable("GASLOB") != null) {
      // The Gas Grenadier's overhand lob (GASLOB=<s into the wind-up>; 0.85 is wound up, 1.07 the throw, 2.07 the landing): the ring and its dotted arc,
      // the arm reaching back, the canister in the air. Docs/Mockups/gas-grenadier-throw.html option A ("Overhand Lob"). Mode hud.
      var sg2 = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      host.Game.Enemies.Clear(); host.Game.Bullets.Clear(); host.Game.Patches.Clear(); host.Game.Bot = false;
      me.Pos = me.Disp = new V2(1100f, 560f);
      var gl = (Enemy)sg2.Invoke(host.Game, new object[] { EnemyType.GasGrenadier, new V2(520f, 470f), 0f, 0, false });
      gl.SpawnT = 0f; gl.Disp = gl.Pos; gl.Facing = 0.12f;
      float glAge = float.Parse(Environment.GetEnvironmentVariable("GASLOB"));
      float glDur = Game.GrenadierWindUp + Game.GrenadierSwing + 1f;
      host.Game.Effects.Add(new Effect { Kind = FxKind.GasLob, Pos = new V2(1060f, 540f), Pos2 = new V2(gl.Id, 1f), Color = new Rgba(0.6f, 0.95f, 0.4f), Radius = 70f, Dur = glDur, T = glAge });
    }
    if (Environment.GetEnvironmentVariable("TASER") != null) {
      // The Taser Guard (TASER=<s since a Fork Bolt hit you>): one charging at you (visor, taser, crackling prongs, aim line), another's
      // Fork Bolt flying in across a field of bullets, and the hit on you. Set beside Docs/Mockups/taser-guard-stun.html, option B. Mode hud.
      var sbt = typeof(Game).GetMethod("SpawnBullet", BindingFlags.NonPublic | BindingFlags.Instance);
      var set = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      host.Game.Enemies.Clear(); host.Game.Bullets.Clear(); host.Game.Bot = false;
      me.Pos = me.Disp = new V2(1000f, 470f);
      Func<V2, float, Enemy> tg = (at, ang) => { var en = (Enemy)set.Invoke(host.Game, new object[] { EnemyType.TaserGuard, at, 0f, 0, false }); en.SpawnT = 0f; en.Disp = en.Pos; en.Facing = ang; return en; };
      var g1 = tg(new V2(560f, 470f), (me.Pos - new V2(560f, 470f)).Angle); g1.Winding = true;
      var g2 = tg(new V2(640f, 200f), (me.Pos - new V2(640f, 200f)).Angle);
      V2 bd = (me.Pos - new V2(760f, 300f)).Normalized();
      sbt.Invoke(host.Game, new object[] { Defs.TaserBolt, new V2(760f, 300f), bd * Game.TaserBoltSpeed, 0f, 8f, 12f, 0, false, 0f });
      var rng = new System.Random(7);
      for (int k = 0; k < 90; k++) {
        float a = (float)(rng.NextDouble() * Math.PI * 2), d = 60f + (float)rng.NextDouble() * 420f;
        V2 at = new V2(800f, 440f) + V2.FromAngle(a) * d;
        sbt.Invoke(host.Game, new object[] { Defs.SmallShot, at, V2.FromAngle(a) * 60f, 0f, 8f, 12f, 0, false, 0f });
      }
      float since = float.Parse(Environment.GetEnvironmentVariable("TASER"));
      if (since >= 0f) {
        host.Game.Effects.Add(new Effect { Kind = FxKind.TaserHit, Pos = me.Pos, Pos2 = me.Pos + new V2(1f, 0f), Color = Defs.BulletColor(Defs.TaserBolt), Radius = 1f, Dur = 0.7f, T = since });
        me.StunT = Math.Max(0.1f, Game.StunTime - since);
      }
    }
    if (Environment.GetEnvironmentVariable("SECURITY") != null) {
      // The Security Guard (SECURITY=<s since the lock-on began>, 0.25 fills the lane): one locking on (red lane, reticle), one that just fired
      // (kick, flash, a brass casing, a tracer in flight over a field of bullets), one reloading half way, one ready, one empty. Set beside
      // Docs/Mockups/security-guard.html. Draws once to start the lane and the casing, then moves the clock on. Mode hud.
      var sbt = typeof(Game).GetMethod("SpawnBullet", BindingFlags.NonPublic | BindingFlags.Instance);
      var set = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      var sg = host.Game; sg.Enemies.Clear(); sg.Bullets.Clear(); sg.Bot = false;
      me.Pos = me.Disp = new V2(780f, 470f);
      Func<V2, byte, float, Enemy> sec = (at, ammo, seen) => { var en = (Enemy)set.Invoke(sg, new object[] { EnemyType.SecurityGuard, at, 0f, 0, false }); en.SpawnT = 0f; en.Disp = en.Pos; en.Facing = (me.Pos - at).Angle; en.Phase = en.SeenPhase = ammo; en.SeenT = seen; return en; };
      var sa = sec(new V2(520f, 470f), 4, 2f); sa.Winding = true;
      var sb = sec(new V2(620f, 330f), 6, 2f);
      var sc = sec(new V2(620f, 620f), 0, 1.2f);
      sec(new V2(450f, 340f), 7, 2f);
      sec(new V2(450f, 610f), 0, 0.3f);
      V2 bd = (me.Pos - new V2(700f, 360f)).Normalized();
      sbt.Invoke(sg, new object[] { Defs.TracerRound, new V2(700f, 360f), bd * Game.SecurityBulletSpeed, 0f, 8f, 12f, 0, false, 0f });
      var rng = new System.Random(7);
      for (int k = 0; k < 90; k++) {
        float a = (float)(rng.NextDouble() * Math.PI * 2), d = 60f + (float)rng.NextDouble() * 420f;
        V2 at = new V2(800f, 440f) + V2.FromAngle(a) * d;
        sbt.Invoke(sg, new object[] { Defs.SmallShot, at, V2.FromAngle(a) * 60f, 0f, 8f, 12f, 0, false, 0f });
      }
      typeof(ArenaApp).GetMethod("DrawWorld", f2).Invoke(app, null);   // sees the lock-on start and the full magazine
      sb.Phase = 5; sb.SeenPhase = 5; sb.SeenT = 0.05f;               // ...then a shot: the casing starts
      typeof(ArenaApp).GetMethod("DrawWorld", f2).Invoke(app, null);
      GL.Tris.Clear(); GUI.Log.Clear();  // (only the mode's own draw goes in the picture)
      sb.SeenT = 0.05f + float.Parse(Environment.GetEnvironmentVariable("SECURITY"), System.Globalization.CultureInfo.InvariantCulture);
      sc.SeenT = 1.2f;
      sg.AnimT += float.Parse(Environment.GetEnvironmentVariable("SECURITY"), System.Globalization.CultureInfo.InvariantCulture);
    }
    if (Environment.GetEnvironmentVariable("CAPTAIN") != null) {
      // The Riot Captain (RiotCaptain.cs, RiotCaptainLook.cs): CAPTAIN=<n> picks the set: 0 = walking, winding up a charge (its lane), charging,
      // dazed; 1 = shield held low, tear gas wind-up, buckshot, whistling (enraged). Mode hud.
      var cse = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      var cg = host.Game; cg.Enemies.Clear(); cg.Bullets.Clear(); cg.Bot = false;
      me.Pos = me.Disp = new V2(800f, 400f);
      int set = int.Parse(Environment.GetEnvironmentVariable("CAPTAIN"));
      Func<V2, byte, Enemy> cap = (at, ph) => { var en = (Enemy)cse.Invoke(cg, new object[] { EnemyType.RiotCaptain, at, 0f, 0, false }); en.SpawnT = 0f; en.Disp = en.Pos; en.Facing = (me.Pos - at).Angle; en.Phase = ph; en.PhaseT = 0.5f; return en; };
      byte[] phases = set == 0 ? new[] { Game.CapWalk, Game.CapWinding, Game.CapCharging, Game.CapDazed } : new[] { Game.CapLow, Game.CapGas, Game.CapFans, Game.CapWhistle };
      V2[] spots = { new V2(560f, 420f), new V2(720f, 420f), new V2(880f, 420f), new V2(1040f, 420f) };
      var solo = Environment.GetEnvironmentVariable("CAPPHASE");  // CAPPHASE=<k 0-3>: only that one, 200 px left of you (close-ups)
      if (solo != null) { int k = int.Parse(solo); var c = cap(me.Pos + new V2(-200f, 0f), phases[k]); if (set == 1 && k == 3) c.Hp = c.MaxHp * 0.3f; }
      else for (int k = 0; k < 4; k++) { var c = cap(spots[k], phases[k]); if (set == 1 && k == 3) c.Hp = c.MaxHp * 0.3f; }
    }
    if (Environment.GetEnvironmentVariable("FLANKERS") != null) {
      // The Hive Queen's Swarm bombers: two swooping out to your sides (gold) and two diving in (red-hot).
      var sef = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      host.Game.Enemies.Clear(); host.Game.Bullets.Clear(); host.Game.Bot = false;
      me.Pos = me.Disp = new V2(800f, 560f);
      Func<V2, float, byte, Enemy> fl = (at, face, ph) => { var en = (Enemy)sef.Invoke(host.Game, new object[] { EnemyType.HiveBomber, at, 0f, 0, false }); en.SpawnT = 0f; en.Disp = en.Pos; en.Facing = face; en.Phase = ph; return en; };
      fl(new V2(560f, 330f), 2.6f, 1); fl(new V2(1060f, 360f), 0.6f, 1);
      fl(new V2(470f, 560f), 0f, 2); fl(new V2(1150f, 520f), MathX.Pi, 2);
      var qn = (Enemy)sef.Invoke(host.Game, new object[] { EnemyType.HiveQueen, new V2(800f, 160f), 0f, 0, false }); qn.SpawnT = 0f; qn.Disp = qn.Pos;
      // QUEENHOLD=swell,<0-1> | lay,<s into the Eggs turn> | dive,<s into a dive>: the Brood Mother's egg swell, laying, royal dive pose.
      if (Environment.GetEnvironmentVariable("QUEENHOLD") != null)
      {
        var e = qn;
        var qh = Environment.GetEnvironmentVariable("QUEENHOLD").Split(','); float qv = float.Parse(qh[1], System.Globalization.CultureInfo.InvariantCulture);
        var q = e.Queen = new QueenState();
        if (qh[0] == "swell") { q.Stage = QueenStage.Honeycomb; q.Turn = QueenTurn.Rest; q.Step = 5; q.TurnT = qv * 2f; }
        else if (qh[0] == "lay") { q.Stage = QueenStage.Honeycomb; q.Turn = QueenTurn.Eggs; q.Step = 0; q.TurnT = qv; foreach (var at in new[] { new V2(420f, 420f), new V2(1180f, 380f), new V2(620f, 620f), new V2(1020f, 640f) }) { var eg = (Enemy)sef.Invoke(host.Game, new object[] { EnemyType.HiveEgg, at, Game.EggFlight - qv, qn.Id, false }); eg.Disp = eg.Pos; } }
        else { q.Stage = QueenStage.Frenzy; q.Turn = QueenTurn.Dive; q.Step = 2; q.DiveT = qv; q.Shadow = new V2(800f, 400f); e.Pos = e.Disp = V2.Lerp(new V2(800f, 160f), q.Shadow, MathX.Clamp01((qv - 0.3f) / 0.9f)); }
      }
      // QUEENSTAGE=<0-3>: her phase (Swarm, Honeycomb, Frenzy, Last Stand), for the Brood Mother's looks (QueenBodyLook.cs).
      if (Environment.GetEnvironmentVariable("QUEENSTAGE") != null) { int qs = int.Parse(Environment.GetEnvironmentVariable("QUEENSTAGE")); qn.Queen = new QueenState { Turn = QueenTurn.Rest, Step = -1, Stage = (QueenStage)qs }; qn.Hp = qn.MaxHp * new[] { 0.9f, 0.5f, 0.2f, 0.05f }[qs]; }
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
    if (Environment.GetEnvironmentVariable("CLOT") != null) {
      // The Gore Mortar: charging (CLOT=<draw seconds>, with the arc and the landing outline), two clots in flight,
      // a pool still wet and one congealed with an enemy stuck in it.
      var sbc2 = typeof(Game).GetMethod("SpawnBullet", BindingFlags.NonPublic | BindingFlags.Instance);
      var addZone = typeof(Game).GetMethod("AddZone", BindingFlags.NonPublic | BindingFlags.Instance);
      var gm = host.Game; gm.Bot = false;
      me.Weapon = System.Array.Find(Defs.Weapons, w => w.Id == WeaponId.GoreMortar); me.Kit = Kit.Hemomancer;
      me.Pos = me.Disp = new V2(380f, 520f); me.Aim = -0.2f; me.DrawT = float.Parse(Environment.GetEnvironmentVariable("CLOT"), System.Globalization.CultureInfo.InvariantCulture);
      for (int k = 0; k < 2; k++)
      {
        float c = k == 0 ? 1f : 0.4f, fl = Game.ClotFlight(c);
        var b = (Bullet)sbc2.Invoke(gm, new object[] { Defs.ClotShot, new V2(520f + 260f * k, 300f - 60f * k), new V2(500f, -60f), 0f, fl, 34f, 1, false, 0f });
        b.Life = fl * (k == 0 ? 0.55f : 0.3f);
      }
      var en = new System.Collections.Generic.List<Enemy>(gm.Enemies.Values);
      V2[] at = { new V2(1020f, 470f), new V2(1080f, 540f), new V2(700f, 700f), new V2(760f, 650f) };
      for (int k = 0; k < en.Count && k < at.Length; k++) { en[k].Pos = en[k].Disp = at[k]; en[k].FrozenT = k < 2 ? 0.6f : 0f; }
      V2[] poolAt = { new V2(1040f, 500f), new V2(720f, 680f) };
      float[] poolAge = { 1.6f, 0.5f };
      for (int k = 0; k < 2; k++)
      {
        float age = poolAge[k];
        var z = (Zone)addZone.Invoke(gm, new object[] { ZoneKind.BloodPool, me, poolAt[k], Game.ClotPoolRadius, Game.ClotPoolTime });
        z.T = z.Duration - age; z.Congealed = age >= Game.PoolCongeal;
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
    if (Environment.GetEnvironmentVariable("WETBAR") != null) {
      // The effect bar (EffectBar.cs): three wet enemies round you, one just soaked, one half dry, one nearly dry; the middle one hurt.
      me.Pos = me.Disp = new V2(640f, 420f); host.Game.Bot = false;
      int wk = 0;
      foreach (var e in host.Game.Enemies.Values) { if (wk >= 3) break; e.Pos = e.Disp = me.Pos + V2.FromAngle(-0.5f + wk * 1.2f) * 110f; e.SpawnT = 0f; e.Spawning = false; e.WetT = 8f; e.Wet = true; e.WetPeak = 8f; e.WetLeft = new[] { 8f, 4f, 1f }[wk]; if (wk == 1) e.Hp = e.MaxHp * 0.6f; wk++; }
    }
    if (Environment.GetEnvironmentVariable("ELEMSTATUS") != null) {
      // The Elementalist's earth and light statuses (ElementStatusLook.cs): brittle, dazzled, both, and brittle with wet and charged round you.
      me.Pos = me.Disp = new V2(640f, 420f); host.Game.Bot = false;
      int ek = 0;
      foreach (var e in host.Game.Enemies.Values) {
        if (ek >= 4) { e.Pos = e.Disp = new V2(2000f, 2000f); continue; }
        e.Pos = e.Disp = me.Pos + V2.FromAngle(-0.9f + ek * 0.95f) * 130f; e.SpawnT = 0f; e.Spawning = false;
        e.Brittle = ek != 1; e.BrittleT = e.Brittle ? 5f : 0f; e.Dazzled = ek == 1 || ek == 2; e.DazzleT = e.Dazzled ? 3f : 0f;
        if (ek == 3) { e.Wet = true; e.WetT = 4f; e.WetPeak = 4f; e.WetLeft = 3f; e.Charged = true; e.ChargedT = 4f; }
        ek++;
      }
    }
    if (Environment.GetEnvironmentVariable("DAZE") != null) {
      // Dazed by a close-in hit (Daze.cs): three enemies round you with stars over their heads.
      me.Pos = me.Disp = new V2(640f, 420f); host.Game.Bot = false;
      int dk = 0;
      foreach (var e in host.Game.Enemies.Values) { if (dk >= 3) break; e.Pos = e.Disp = me.Pos + V2.FromAngle(-0.5f + dk * 1.2f) * 110f; e.DazeT = 10f; e.Dazed = true; dk++; }
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
      gm.Effects.Add(new Effect { Kind = FxKind.ArcLink, Pos = arcFrom, Pos2 = arcFrom + new V2(260f, -90f), Color = Defs.BulletColor(Defs.BoltShot), Radius = 0f, Dur = 0.75f, T = 0.05f });  // Arc Lightning's chain (ArcLockLook.cs)
      gm.Effects.Add(new Effect { Kind = FxKind.ArcLink, Pos = arcFrom + new V2(260f, -90f), Pos2 = arcFrom + new V2(480f, -30f), Color = Defs.BulletColor(Defs.BoltShot), Radius = 1f, Dur = 0.75f, T = 0.02f });
    }
    if (Environment.GetEnvironmentVariable("ELEMBALLS") != null) {
      // The Fireball and its variants side by side (ElementBallLook.cs): a fireball, a waterball and an earthball in flight,
      // two wet enemies the waterball rolled through, one stunned by an earthball. You hold ELEMBALLS=water|earth|fire.
      var sbE = typeof(Game).GetMethod("SpawnBullet", BindingFlags.NonPublic | BindingFlags.Instance);
      var gm = host.Game; gm.Bot = false;
      string held = Environment.GetEnvironmentVariable("ELEMBALLS");
      me.Kit = Kit.Elementalist; me.Weapon = Defs.WeaponOf(held == "earth" ? WeaponId.Earthball : held == "fire" ? WeaponId.Fireball : WeaponId.Waterball);
      me.Pos = me.Disp = new V2(360f, 470f); me.Aim = 0f;
      byte[] kinds = { Defs.FireballShot, Defs.WaterballShot, Defs.EarthballShot };
      for (int k = 0; k < 3; k++) sbE.Invoke(gm, new object[] { kinds[k], new V2(k == 2 ? 640f : 580f, 300f + 160f * k), new V2(k == 2 ? 360f : 430f, 0f), 0f, 1.4f, 22f, 1, false, 0f });
      var spE = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      for (int k = gm.Enemies.Count; k < 3; k++) spE.Invoke(gm, new object[] { EnemyType.Grunt, new V2(1400f, 200f + 100f * k), 0f, 0, false });
      var ee = new System.Collections.Generic.List<Enemy>(gm.Enemies.Values);
      V2[] eAt = { new V2(500f, 470f), new V2(430f, 430f), new V2(700f, 640f), new V2(800f, 300f) };
      for (int k = 0; k < ee.Count; k++) { ee[k].Pos = ee[k].Disp = k < eAt.Length ? eAt[k] : new V2(1400f, 120f + 60f * k); ee[k].SpawnT = 0f; ee[k].Spawning = false; }
      for (int k = 0; k < 2 && k < ee.Count; k++) { ee[k].WetT = 4f; ee[k].Wet = true; ee[k].WetPeak = 4f; ee[k].WetLeft = 3f - k; }
      if (ee.Count > 2) { ee[2].StunT = ee[2].FrozenT = 0.6f; ee[2].Dazed = true; gm.Effects.Add(new Effect { Kind = FxKind.Debris, Pos = ee[2].Pos, Pos2 = ee[2].Pos, Color = Defs.BulletColor(Defs.EarthballShot), Radius = Game.EarthRubble, Dur = 0.45f, T = 0.08f }); }
    }
    if (Environment.GetEnvironmentVariable("HEMO") != null) {
      // Bleed drops: enemies with 1 to 4 bleed stacks (4 is one short of the Needle Swarm's burst).
      var hm = new System.Collections.Generic.List<Enemy>(host.Game.Enemies.Values);
      for (int k = 0; k < hm.Count && k < 4; k++) { hm[k].Pos = hm[k].Disp = new V2(560f + 130f * k, 450f); hm[k].BleedStacks = k + 1; }
    }
    if (Environment.GetEnvironmentVariable("HOOK") != null) {
      // The Kusarigama: HOOK=whirl (the weight circling you, enemies drawn in) or HOOK=<seconds into the throw, 0 to 0.65> (a throw biting an enemy).
      string hookMode = Environment.GetEnvironmentVariable("HOOK");
      me.Kit = Kit.Ninja; me.Weapon = Defs.Weapon(Role.Tank, Kit.Ninja, 0); host.Game.Bot = false;
      me.Pos = me.Disp = new V2(500f, 450f); me.Aim = 0f;
      var hk = new System.Collections.Generic.List<Enemy>(host.Game.Enemies.Values);
      if (hookMode == "whirl") {
        me.KusariWhirl = true; me.KusariHeld = 1.4f; me.KusariAng = 2.2f; me.Aim = 0f;
        for (int k = 0; k < hk.Count && k < 6; k++) { V2 at = me.Pos + V2.FromAngle(k * 1.05f) * (70f + 22f * (k % 3)); hk[k].Pos = hk[k].Disp = at; }
      } else {
        float tk = float.Parse(hookMode);
        if (hk.Count > 0) { hk[0].Pos = hk[0].Disp = new V2(720f, 450f); me.HookId = hk[0].Id; me.HookT = Game.KusariThrowTime - tk; me.KusariAim = 0f; me.KusariStep = 2; }
      }
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
    if (Environment.GetEnvironmentVariable("ORBS") != null) {
      // ORBS=1: every health orb side by side to the right of you: tiny, small, big, then a Medkit's (OrbLook.cs).
      host.Game.Bot = false; me.Pos = me.Disp = new V2(600f, 420f);
      host.Game.Orbs[920] = new Orb { Id = 920, Tiny = true, Pos = new V2(640f, 470f), Disp = new V2(640f, 470f), Life = 10f };
      host.Game.Orbs[921] = new Orb { Id = 921, Pos = new V2(670f, 470f), Disp = new V2(670f, 470f), Life = 10f };
      host.Game.Orbs[922] = new Orb { Id = 922, Big = true, Pos = new V2(705f, 470f), Disp = new V2(705f, 470f), Life = 10f };
      host.Game.Orbs[923] = new Orb { Id = 923, Med = true, MedHeal = 20f, Pos = new V2(745f, 470f), Disp = new V2(745f, 470f), Life = 10f };
    }
    if (Environment.GetEnvironmentVariable("THRUST") != null) {
      float tt = float.Parse(Environment.GetEnvironmentVariable("THRUST"));
      host.Game.Effects.Clear();
      host.Game.Effects.Add(new Effect { Kind = FxKind.Jab, Pos = me.Pos, Pos2 = me.Pos + V2.FromAngle(me.Aim) * 150f, Color = Defs.BulletColor(Defs.KnightSlash), Radius = Game.BreachHalfWidth, T = tt * 0.26f, Dur = 0.26f });
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
      if (gm.Has(RunMod.Hunted)) gm.HuntedId = gm.MyId;  // you're the quarry
      if (gm.Has(RunMod.SecondWind)) foreach (var pl in gm.Players.Values) pl.SecondWind = true;
      if (gm.Has(RunMod.ZombieInfection)) {
        // The horde (ZombieLook.cs): every kind round you, a Crawler holding you, a few frenzied, acid and spores.
        gm.Enemies.Clear();
        gm.Players[1].Pos = gm.Players[1].Disp = new V2(Game.Width / 2f, Game.Height / 2f + 20f);
        var zp = gm.Players[1].Pos;
        var kinds = new[] { EnemyType.Shambler, EnemyType.Shambler, EnemyType.Runner, EnemyType.Runner, EnemyType.Runner, EnemyType.Spitter, EnemyType.Bloater, EnemyType.Shrieker, EnemyType.Mender, EnemyType.Shambler, EnemyType.Runner, EnemyType.Shambler };
        for (int i = 0; i < kinds.Length; i++) {
          var d = Defs.Get(kinds[i]); float a = i * 0.53f + 0.3f, dd = kinds[i] == EnemyType.Spitter || kinds[i] == EnemyType.Shrieker || kinds[i] == EnemyType.Mender ? 330f : 150f + 25f * (i % 4);
          var at = (zp + V2.FromAngle(a) * dd).Clamp(gm.ArenaMin + new V2(40f, 40f), gm.ArenaMax - new V2(40f, 40f));
          var ze = new Enemy { Id = 900 + i, Type = kinds[i], Def = d, Radius = d.Radius, Pos = at, Disp = at, Hp = d.Hp * (i % 3 == 0 ? 0.6f : 1f), MaxHp = d.Hp };
          ze.Facing = (zp - at).Angle; if (i < 5) ze.Phase = Zombies.PhFrenzy;
          gm.Enemies[ze.Id] = ze;
        }
        var cd = Defs.Get(EnemyType.Crawler); var cat = zp + new V2(-36f, 14f);
        gm.Enemies[950] = new Enemy { Id = 950, Type = EnemyType.Crawler, Def = cd, Radius = cd.Radius, Pos = cat, Disp = cat, Hp = cd.Hp, MaxHp = cd.Hp, Facing = (zp - cat).Angle, Phase = Zombies.PhGrab };
        gm.Players[1].StunT = 1f;
        gm.Patches[71] = new PathPatch { Id = 71, Kind = PatchKind.Acid, Pos = zp + new V2(170f, -150f), Warn = 0.9f, Life = 4.9f, R0 = 70f, R1 = 70f, Age = 2f };
        gm.Patches[72] = new PathPatch { Id = 72, Kind = PatchKind.Spores, Pos = zp + new V2(-260f, -170f), Warn = 0f, Life = 4f, R0 = 110f, R1 = 110f, Age = 1.5f };
      }
      if (gm.Has(RunMod.TinyArena)) {
        gm.TinyInset = (V2)typeof(Game).GetMethod("TinyTarget", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(gm, null);
        foreach (var pl in gm.Players.Values) pl.Disp = pl.Pos = pl.Pos.Clamp(gm.ArenaMin + new V2(30f, 30f), gm.ArenaMax - new V2(30f, 30f));
        foreach (var en in gm.Enemies.Values) en.Disp = en.Pos = en.Pos.Clamp(gm.ArenaMin + new V2(30f, 30f), gm.ArenaMax - new V2(30f, 30f));
      }
    }
    // GRAVES=<s>: the Lich's Grave Caller (ZombieLook.cs) calling, its three graves round you <s> into the call (1.6 s: they open),
    // two of its Shamblers already up with their soul threads, and some of the horde.
    if (Environment.GetEnvironmentVariable("GRAVES") != null) {
      var gg = host.Game; float ga = float.Parse(Environment.GetEnvironmentVariable("GRAVES"), System.Globalization.CultureInfo.InvariantCulture);
      gg.Enemies.Clear();
      gg.Players[1].Pos = gg.Players[1].Disp = new V2(Game.Width / 2f - 60f, Game.Height / 2f + 20f);
      var gp0 = gg.Players[1].Pos;
      System.Func<int, EnemyType, V2, byte, Enemy> addE = (id, t, at, ph) => { var d = Defs.Get(t); var e = new Enemy { Id = id, Type = t, Def = d, Radius = d.Radius, Pos = at, Disp = at, Hp = d.Hp, MaxHp = d.Hp, Phase = ph }; e.Facing = (gp0 - at).Angle; gg.Enemies[id] = e; return e; };
      var callerAt = gp0 + new V2(330f, -60f);
      addE(960, EnemyType.GraveCaller, callerAt, ga < 0.8f ? Zombies.PhAct : (byte)0);
      addE(961, EnemyType.GraveCaller, gp0 + new V2(-360f, 170f), 0);
      var r1 = addE(962, EnemyType.Shambler, gp0 + new V2(190f, 120f), Zombies.PhCalled); r1.Hp = r1.MaxHp * 0.5f; r1.Risen = true;
      var r2 = addE(963, EnemyType.Shambler, gp0 + new V2(-210f, 40f), Zombies.PhCalled); r2.Hp = r2.MaxHp * 0.5f; r2.Risen = true;
      addE(964, EnemyType.Shambler, gp0 + new V2(-120f, -200f), 0); addE(965, EnemyType.Runner, gp0 + new V2(60f, -230f), 0); addE(966, EnemyType.Spitter, gp0 + new V2(-320f, -160f), 0);
      for (int i = 0; i < 3; i++)
        gg.Patches[80 + i] = new PathPatch { Id = 80 + i, Kind = PatchKind.Grave, Pos = gp0 + V2.FromAngle(0.4f + MathX.Tau * i / 3f) * Zombies.GraveRing, Warn = Zombies.GraveRise, Life = Zombies.GraveLife, R0 = Zombies.GraveRadius, R1 = Zombies.GraveRadius, Age = ga };
    }
    if (Environment.GetEnvironmentVariable("PATHON") != null) { host.Game.CurrentPath = (PathId)int.Parse(Environment.GetEnvironmentVariable("PATHON")); host.Game.PathFrom = host.Game.Wave - 1; host.Game.PathTo = host.Game.Wave + 2; host.Game.Mods = RunMod.Frenzy; if (host.Game.CurrentPath == PathId.Fogbound) host.Game.FogF = 1f; if (host.Game.CurrentPath == PathId.NarrowPass) { host.Game.NarrowF = 1f; foreach (var pl in host.Game.Players.Values) pl.Disp = pl.Pos = pl.Pos.Clamp(host.Game.ArenaMin + new V2(30f, 30f), host.Game.ArenaMax - new V2(30f, 30f)); foreach (var en in host.Game.Enemies.Values) en.Disp = en.Pos = en.Pos.Clamp(host.Game.ArenaMin + new V2(30f, 30f), host.Game.ArenaMax - new V2(30f, 30f)); } }
    if (Environment.GetEnvironmentVariable("WOUND") != null) {  // WOUND=0.2: everyone wounded that share of max health and down to 55% (Combat.cs, Docs/PowerStacking.md)
      float wf = float.Parse(Environment.GetEnvironmentVariable("WOUND"), System.Globalization.CultureInfo.InvariantCulture);
      foreach (var wp in host.Game.Players.Values) { wp.Wound = wp.MaxHp * wf; wp.Hp = wp.MaxHp * 0.55f; }
    }
    if (Environment.GetEnvironmentVariable("EXPANDMAP") != null) {  // EXPANDMAP=<rooms bought>: the Expanding arena with that many rooms up, you in the hub, enemies spread over the rooms
      var xg = host.Game; xg.Diff.Set(DiffKnob.ArenaExpanding, 1f);
      if (Environment.GetEnvironmentVariable("FINSTYLE") != null) Expansion.FinStyle = int.Parse(Environment.GetEnvironmentVariable("FINSTYLE"));  // FINSTYLE=<n>: a drawn room's side walls (Expansion.AddFins)
      int nb = int.Parse(Environment.GetEnvironmentVariable("EXPANDMAP"));
      if (nb > 0) { xg.Expanded.AddRange(Expansion.GridCore()); xg.Openings.AddRange(Expansion.GridCoreCuts()); }  // EXPANDMAP=<n>: the core and n pieces round it (0: the bare hub)
      for (int k = 0; k < nb; k++) { var mo = Expansion.PlacementsFor(xg.Expanded, k % 3 == 0 ? PieceShape.TetL : k % 3 == 1 ? PieceShape.Domino : PieceShape.Mono); if (mo.Count == 0) break; foreach (var mr in mo[(k * 7) % mo.Count].Rooms) { var m2 = mr; m2.Owner = 1 + k % 2; xg.Expanded.Add(m2); } }
      string xr = Environment.GetEnvironmentVariable("EXPANDROOMS");  // EXPANDROOMS=x,y,w,h;...: plain boxes added in order (no checks)
      if (xr != null) foreach (var part in xr.Split(';')) { var q = Array.ConvertAll(part.Split(','), int.Parse); string why = null; var cr = Box(q[0], q[1], q[2], q[3]); if (cr.HasValue) xg.Expanded.Add(cr.Value); else Console.Error.WriteLine("EXPANDROOMS " + part + ": " + why); }
      if (Environment.GetEnvironmentVariable("GRIDCORE") != null) { xg.Expanded.InsertRange(0, Expansion.GridCore()); xg.Openings.AddRange(Expansion.GridCoreCuts()); typeof(Game).GetField("expWorld", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(xg, null); }  // GRIDCORE=1 (with EXPANDMAP=0): a Grid run's core (GridRooms.cs); EXPANDAT=x,y ZOOM=z to look at it
      if (Environment.GetEnvironmentVariable("CORELAYOUT") != null) {  // CORELAYOUT=<0-9> (with GRIDCORE=1 or EXPANDMAP=n): that core floor plan (CoreLayouts.cs), its walls and crates
        xg.CoreLayout = (CoreLayoutId)int.Parse(Environment.GetEnvironmentVariable("CORELAYOUT")); typeof(Game).GetField("expWorld", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(xg, null);
        xg.Props.Clear(); foreach (var cc in CoreLayouts.Crates(xg.CoreLayout)) xg.AddProp(cc.Heavy ? PropKind.HeavyCrate : PropKind.Crate, cc.Pos).Age = 5f;
      }
      string xsur = Environment.GetEnvironmentVariable("EXPANDSURPRISE");  // EXPANDSURPRISE=ring|cross|l|t|chamfer: that surprise room built, SURPRISEAGE=<s since it appeared> (default 0.6)
      if (xsur != null) {
        var xsh = (SurpriseShape)Array.IndexOf(new[] { "ring", "cross", "l", "t", "chamfer" }, xsur); string xwhy;
        if (!xg.Expanded.Exists(Expansion.IsGridCore)) { xg.Expanded.InsertRange(0, Expansion.GridCore()); xg.Openings.AddRange(Expansion.GridCoreCuts()); }
        var xsp = Expansion.SurpriseFor(5, 10, xg.Expanded, xsh, out xwhy);
        if (xsp.HasValue) { xg.Expanded.AddRange(xsp.Value.Rooms); xg.SurpriseKind = xsp.Value.Rooms[0].Surprise; xg.SurpriseWave = xg.Wave; xg.SurpriseAge = float.Parse(Environment.GetEnvironmentVariable("SURPRISEAGE") ?? "0.6"); float sx0 = 1e9f, sy0 = 1e9f, sx1 = -1e9f, sy1 = -1e9f; foreach (var sr in xsp.Value.Rooms) { sx0 = Math.Min(sx0, sr.X); sy0 = Math.Min(sy0, sr.Y); sx1 = Math.Max(sx1, sr.X1); sy1 = Math.Max(sy1, sr.Y1); } int spi = 0; foreach (var pl in xg.Players.Values) { pl.Pos = pl.Disp = new V2((sx0 + sx1) / 2f + 60f * spi, (sy0 + sy1) / 2f + 220f); spi++; } }
        else Console.Error.WriteLine("surprise room: " + xwhy);
      }
      if (Environment.GetEnvironmentVariable("GAUNTLET") != null) {  // GAUNTLET=<s> (with GRIDCORE=1 EXPANDMAP=0): the Relic Gauntlet (RelicGauntlet.cs) built on the lattice at laser clock <s>, you in its middle cell (GAUNTLETAT=<0-1800> along it); ZOOM=0.4 sees it whole, GAUNTLETSPOT=<n>
        var gpl = Expansion.PlacementsFor(xg.Expanded, PieceShape.TriI);
        gpl.RemoveAll(q => !Mystery.DoorAtEnd(q.Rooms[0], new MysteryDoor { X = (short)q.SX, Y = (short)q.SY }));
        int gi = Math.Min(gpl.Count - 1, int.Parse(Environment.GetEnvironmentVariable("GAUNTLETSPOT") ?? "0"));
        if (gi >= 0) {
          var gr = gpl[gi].Rooms[0]; gr.Owner = 1; gr.Surprise = RelicGauntlet.Mark; xg.Expanded.Add(gr);
          var rs = RelicGauntlet.RelicSpot(gr); xg.Nodes.Add(new MapNode { X = (short)rs.X, Y = (short)rs.Y, Kind = NodeKind.Relic, Rule = (byte)((int)RelicGauntlet.RelicFor(1, gr) + 1) });
          typeof(Game).GetField("expWorld", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(xg, null);
          typeof(Game).GetField("hostClock", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(xg, float.Parse(Environment.GetEnvironmentVariable("GAUNTLET"), System.Globalization.CultureInfo.InvariantCulture));
          float gu = float.Parse(Environment.GetEnvironmentVariable("GAUNTLETAT") ?? "900", System.Globalization.CultureInfo.InvariantCulture);
          foreach (var pl in xg.Players.Values) pl.Pos = pl.Disp = RelicGauntlet.ToWorld(gr, gu, 300f);
        } else Console.Error.WriteLine("GAUNTLET: no spot fits");
      }
      string xcard = Environment.GetEnvironmentVariable("CARDROOM");  // CARDROOM=buildershop|shop|market|arms|medic: that card's one-cell room built on the lattice, everyone in it; CARDSPOT=<n> picks its nth door spot
      if (xcard != null) {
        var xc = (PieceCard)Enum.Parse(typeof(PieceCard), xcard, true);
        var xco = Expansion.PlacementsFor(xg.Expanded, PieceShape.Mono);
        int xci = Math.Min(xco.Count - 1, int.Parse(Environment.GetEnvironmentVariable("CARDSPOT") ?? "0"));
        if (xci >= 0) { var r2 = xco[xci].Rooms[0]; r2.Owner = 1; r2.Kind = Expansion.CardRoomKind(xc); xg.Expanded.Add(r2); foreach (var pl in xg.Players.Values) { if (Environment.GetEnvironmentVariable("SHOPGOLD") != null) pl.Gold = int.Parse(Environment.GetEnvironmentVariable("SHOPGOLD")); if (Environment.GetEnvironmentVariable("SHOPGOLD") != null && !xg.ShopOpen) typeof(Game).GetMethod("OpenShop", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(xg, null); pl.Pos = pl.Disp = xco[xci].Mid + new V2(float.Parse(Environment.GetEnvironmentVariable("CARDDX") ?? "0"), float.Parse(Environment.GetEnvironmentVariable("CARDDY") ?? "0")); } }  // (CARDDX/CARDDY=<px>: stand that far right of / below the room's middle; SHOPGOLD=<n>: your gold, the shop open with a shelf)
        else Console.Error.WriteLine("CARDROOM: no spot fits");
        if (xci >= 0 && Environment.GetEnvironmentVariable("CARDABOVE") != null) {  // CARDABOVE=1: a plain room built on the card room's far side (a second door)
          var mid = xco[xci].Mid; var ups = Expansion.PlacementsFor(xg.Expanded, PieceShape.Mono).FindAll(o => Math.Abs(o.Mid.X - mid.X) < 1f && o.Mid.Y < mid.Y);
          if (ups.Count > 0) { var up = ups[0]; var r3 = up.Rooms[0]; r3.Owner = 1; xg.Expanded.Add(r3); } else Console.Error.WriteLine("CARDABOVE: no spot");
        }
        if (Environment.GetEnvironmentVariable("SHOPDONE") != null) { xg.ShopOpen = true; xg.State = MatchState.Intermission; foreach (var pl in xg.Players.Values) pl.ShopDone = true; }  // SHOPDONE=1 (with CARDROOM=shop|market|arms|medic): the room shop open, you done with it: the F tag to open it again (ExpansionUi.cs DrawShopReopenHint)
        if (Environment.GetEnvironmentVariable("BUILDEROPEN") != null) {  // BUILDEROPEN=1 (with CARDROOM=buildershop): a build round, so the Builder Shop's shelf shows (BuilderShopUi.cs)
          xg.ExpandOpen = true; xg.State = MatchState.Intermission;
          foreach (var pl in xg.Players.Values) { pl.Vectors = int.Parse(Environment.GetEnvironmentVariable("BUILDERVEC") ?? "40"); pl.VectorCap = 90; pl.Hand[0] = (int)Expansion.MakePieceCard(PieceShape.Mono); pl.Hand[1] = (int)Expansion.MakePieceCard(PieceShape.TetL); }
        }
      }
      string xow = Environment.GetEnvironmentVariable("ONEWAY");  // ONEWAY=domino|tetl|...: a One-way piece on the lattice (the spot with the most one-way doors; OneWayDoors.cs), everyone by its first one-way door
      if (xow != null) {
        var owShape = (PieceShape)Enum.Parse(typeof(PieceShape), xow, true); System.Collections.Generic.List<ExpansionRoom> owBest = null; int owMost = 0;
        foreach (var oo in Expansion.PlacementsFor(xg.Expanded, owShape)) { var ol = new System.Collections.Generic.List<ExpansionRoom>(xg.Expanded); foreach (var orr in oo.Rooms) { var o2 = orr; o2.OneWay = true; o2.Owner = 1; ol.Add(o2); } int on = System.Linq.Enumerable.Count(Expansion.Build(ol, xg.Openings).Doors, dd => dd.Pass != 0); if (on > owMost) { owMost = on; owBest = ol; } }
        if (owBest != null) { xg.Expanded.Clear(); xg.Expanded.AddRange(owBest); typeof(Game).GetField("expWorld", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(xg, null);
          var ow = xg.ExpansionWorld; var od = ow.Doors[ow.OneWays[0]]; int pi = 0; foreach (var pl in xg.Players.Values) { pl.Pos = pl.Disp = od.Pos + od.Along * (-60f + 120f * pi) - od.Across * 260f; pi++; } }
        else Console.Error.WriteLine("ONEWAY: no spot gives one-way doors");
      }
      string xnode = Environment.GetEnvironmentVariable("EXPANDNODE");  // EXPANDNODE=kind:x,y[:spent];...: nodes at world points (kind chest|relic|shrine; a third part "spent" or "hidden" (fogged)), e.g. inside an EXPANDROOMS room
      if (xnode != null) foreach (var part in xnode.Split(';')) { var np = part.Split(':'); var nc = Array.ConvertAll(np[1].Split(','), int.Parse); { var nk = np[0].Split('.'); xg.Nodes.Add(new MapNode { Kind = (NodeKind)Enum.Parse(typeof(NodeKind), nk[0], true), X = (short)nc[0], Y = (short)nc[1], Spent = np.Length > 2 && np[2] == "spent", Hidden = np.Length > 2 && np[2] == "hidden" }); } }
      string pitCell = Environment.GetEnvironmentVariable("PITCELL");  // PITCELL=<variant name, e.g. Four Wells>: a one-cell Combat room with that pit variant (CellVariants.cs), everyone 150 px left of its middle; PITFALL=<s>: a player's and an enemy's fall that long ago, player 1 down in the pit or out on the rim (PitLook.cs)
      if (pitCell != null) {
        CellLayouts.Forced = pitCell; ExpansionRoom? pcRoom = null;
        foreach (var po in Expansion.PlacementsFor(xg.Expanded, PieceShape.Mono)) {
          var pl0 = new System.Collections.Generic.List<ExpansionRoom>(xg.Expanded) { po.Rooms[0] };
          CellLayouts.CellOf(po.Rooms[0].X + 1, po.Rooms[0].Y + 1, out int pcx, out int pcy);
          if (CellLayouts.TryPlan(CellLayouts.Plans(pl0), pcx, pcy, out var pp) && CellLayouts.NameOf(pp) == pitCell) { pcRoom = po.Rooms[0]; break; }
        }
        if (pcRoom.HasValue) { xg.Expanded.Add(pcRoom.Value); typeof(Game).GetField("expWorld", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(xg, null);
          V2 pm = new V2(pcRoom.Value.X + 300, pcRoom.Value.Y + 300); foreach (var pl in xg.Players.Values) pl.Pos = pl.Disp = pm + new V2(-150f, 0f);
          string pf = Environment.GetEnvironmentVariable("PITFALL");
          if (pf != null && xg.ActivePits.Length > 0) { float age = float.Parse(pf, System.Globalization.CultureInfo.InvariantCulture); var pit = xg.ActivePits[0]; Player me1 = null; foreach (var pl in xg.Players.Values) { me1 = pl; break; }
            var rim = xg.PitRim(pit.Pos, me1.Def.HitR, pit.Pos);
            xg.Effects.Add(new Effect { Kind = FxKind.PitFall, Pos = pit.Pos + new V2(-8f, 0f), Pos2 = rim, Color = new Rgba(me1.Def.Color.R, me1.Def.Color.G, me1.Def.Color.B, 1f), Radius = me1.Def.HitR, Dur = Game.PitFxDur, T = age });
            me1.Pos = me1.Disp = age < Game.PitDrop ? pit.Pos + new V2(-8f, 0f) : rim; if (age < Game.PitDrop) xg.PitFallT = Game.PitDrop - age;  // (down in the pit, then out on the rim)
            if (xg.ActivePits.Length > 1) { var p2 = xg.ActivePits[1]; xg.Effects.Add(new Effect { Kind = FxKind.PitFall, Pos = p2.Pos, Pos2 = p2.Pos, Color = new Rgba(1f, 0.3f, 0.34f, 0.5f), Radius = 16f, Dur = Game.PitFxDur, T = age }); } }
        }
        else Console.Error.WriteLine("PITCELL: no one-cell spot rolls " + pitCell);
      }
      string xat = Environment.GetEnvironmentVariable("EXPANDAT");  // EXPANDAT=x,y: every player stands there
      if (xat != null) { var xa = Array.ConvertAll(xat.Split(','), int.Parse); foreach (var pl in xg.Players.Values) pl.Pos = pl.Disp = new V2(xa[0], xa[1]); }
      if (Environment.GetEnvironmentVariable("TURRETS") != null) {  // TURRETS=1: three Black Market turrets round player 1 (5, 3 and 1 waves left; TurretLook.cs)
        var tp = xg.Players[1].Pos; int ti = 0;
        foreach (var to in new[] { new V2(-170f, -90f), new V2(170f, -90f), new V2(0f, 150f) }) { xg.Placed.Add(new PlacedBuild { Kind = BuildKind.Turret, X = (short)(tp.X + to.X), Y = (short)(tp.Y + to.Y), Owner = 1, Waves = (byte)(5 - 2 * ti) }); ti++; }
      }
      if (Environment.GetEnvironmentVariable("DORMANT") != null) {  // DORMANT=1: a sleeping group in every cell of every room but the core (DormantGroups.cs)
        DormantGroups.CellChance = 1f;
        var plant = typeof(Game).GetMethod("PlantDormantGroups", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        foreach (var dr in xg.Expanded.ToArray()) if (RoomFog.StartsUnseen(dr)) plant.Invoke(xg, new object[] { new System.Collections.Generic.List<ExpansionRoom> { dr }, false, null, 0, false });
      }
      if (Environment.GetEnvironmentVariable("FOG") != null)  // FOG=1: every room but the core unseen (room fog, RoomFogLook.cs)
        for (int fi = 0; fi < xg.Expanded.Count; fi++) { var fr = xg.Expanded[fi]; fr.Unseen = RoomFog.StartsUnseen(fr); xg.Expanded[fi] = fr; }
      string fogRoom = Environment.GetEnvironmentVariable("FOGROOM");  // FOGROOM=<index>: every player stands in that room (its Expanded index), 120 px in from its left edge
      if (fogRoom != null) { var fro = xg.Expanded[int.Parse(fogRoom)]; foreach (var pl in xg.Players.Values) pl.Pos = pl.Disp = new V2(fro.X + 120, fro.Y + fro.H / 2); }
      string trapStand = Environment.GetEnvironmentVariable("TRAPSTAND");  // TRAPSTAND=slow|spike[,dx,dy]: you stand on the trap of that kind nearest you, offset by dx,dy (RoomTrapLook.cs)
      if (trapStand != null) {
        var ts = trapStand.Split(','); var kind = ts[0] == "spike" ? TrapKind.Spike : TrapKind.Slow; Player me0 = null; foreach (var pl in xg.Players.Values) { me0 = pl; break; }
        V2 near = me0.Pos; float tsBest = float.MaxValue;
        foreach (var r in xg.RoomTrapList) if (r.Kind == kind && (r.Pos - me0.Pos).LengthSq < tsBest) { tsBest = (r.Pos - me0.Pos).LengthSq; near = r.Pos; }
        var off = ts.Length > 2 ? new V2(float.Parse(ts[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(ts[2], System.Globalization.CultureInfo.InvariantCulture)) : new V2(0f, 0f);
        me0.Pos = me0.Disp = near + off;
      }
      string fogAim = Environment.GetEnvironmentVariable("FOGAIM");  // FOGAIM=<degrees>[,trail]: every player faces that way (0 east, 90 south); "trail" first sweeps the cone round to there, exploring what it passes over (the explored memory)
      if (fogAim != null) {
        var fa = fogAim.Split(','); float fdeg = float.Parse(fa[0], System.Globalization.CultureInfo.InvariantCulture);
        var scan = typeof(Game).GetMethod("ScanFog", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (fa.Length > 1 && fa[1] == "trail") for (float d = fdeg - 170f; d < fdeg; d += 8f) { foreach (var pl in xg.Players.Values) pl.Aim = d * (float)Math.PI / 180f; scan.Invoke(xg, null); }
        foreach (var pl in xg.Players.Values) pl.Aim = fdeg * (float)Math.PI / 180f;
      }
      if (Environment.GetEnvironmentVariable("SHRINEUSED") != null) foreach (var pl in xg.Players.Values) pl.ShrinesUsed = ulong.MaxValue;  // SHRINEUSED=1: everyone has used every health shrine (ShrineLook.cs)
      string overHp = Environment.GetEnvironmentVariable("OVERHP");  // OVERHP=<n>: everyone carries n health past max from a shrine (the HUD's gold, the gold ring)
      if (overHp != null) foreach (var pl in xg.Players.Values) pl.OverHp = float.Parse(overHp, System.Globalization.CultureInfo.InvariantCulture);
      if (Environment.GetEnvironmentVariable("RELICSHEET") != null) { ArenaApp.PreviewRelicSheet = true; ArenaApp.PreviewRelicPage = int.Parse(Environment.GetEnvironmentVariable("RELICSHEET")); }  // RELICSHEET=0|1 (page): the relic artefact sheet beside the map (RelicLook.cs; mode hud ZOOM=1 CAMAT=640,470)
      if (Environment.GetEnvironmentVariable("RELIC") != null) foreach (var pl in xg.Players.Values) pl.Relic = RelicId.WallBounce;  // RELIC=1: everyone holds Wall Bounce (the HUD chip)
      if (Environment.GetEnvironmentVariable("RELICOPEN") != null || Environment.GetEnvironmentVariable("VECOPEN") != null || Environment.GetEnvironmentVariable("RELICPED") != null) {  // RELICPED=<RelicId number>: the same room with that faction relic waiting on its pedestal (unspent)  // RELICOPEN=<s> / VECOPEN=<s>: a 6 x 6 room built beside the map with a spent relic (or vector chest) node in it, opened that long ago, you standing beside it (NodeLook.cs)
        bool vecOpen = Environment.GetEnvironmentVariable("VECOPEN") != null;
        string ped = Environment.GetEnvironmentVariable("RELICPED"); ArenaApp.PreviewOpenAge = ped != null ? 0f : float.Parse(Environment.GetEnvironmentVariable(vecOpen ? "VECOPEN" : "RELICOPEN"), System.Globalization.CultureInfo.InvariantCulture);
        ExpansionRoom? rr = null;
        for (int ry = -800; ry <= 1200 && !rr.HasValue; ry += 100) for (int rx = -800; rx <= 2400 && !rr.HasValue; rx += 100) { string wn; var cand = Box(rx, ry, 600, 600); if (cand.HasValue) rr = cand; }
        if (rr.HasValue) {
          xg.Expanded.Add(rr.Value); typeof(Game).GetField("expWorld", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(xg, null);
          var rn = new MapNode { Kind = vecOpen ? NodeKind.Chest : NodeKind.Relic, X = (short)(rr.Value.X + 350), Y = (short)(rr.Value.Y + 250), Spent = ped == null, Rule = ped != null ? (byte)(int.Parse(ped) + 1) : (byte)0 }; xg.Nodes.Add(rn);
          foreach (var pl in xg.Players.Values) pl.Pos = pl.Disp = new V2(rr.Value.X + 560, rr.Value.Y + 300);
        }
      }
      xg.RoomsUp = true; xg.LayoutAge = 5f;
      string trapHp = Environment.GetEnvironmentVariable("TRAPHP");  // TRAPHP=<0-1>: every room turret is damaged to that health (its health bar, RoomTrapLook.cs)
      if (trapHp != null) { var nth = typeof(Game).GetMethod("NoteTrapHit", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance); foreach (var tr in xg.RoomTrapList.ToArray()) if (tr.Kind == TrapKind.Turret) nth.Invoke(xg, new object[] { tr.Pos, float.Parse(trapHp, System.Globalization.CultureInfo.InvariantCulture) }); }
      string wtStand = Environment.GetEnvironmentVariable("TURRETSTAND");  // TURRETSTAND=dx,dy: you stand that far from the room turret nearest you (RoomTrapLook.cs)
      if (wtStand != null) { var ws = wtStand.Split(','); foreach (var pl in xg.Players.Values) { V2 nr = pl.Pos; float wb = float.MaxValue; foreach (var r in xg.RoomTrapList) if (r.Kind == TrapKind.Turret && (r.Pos - pl.Pos).LengthSq < wb) { wb = (r.Pos - pl.Pos).LengthSq; nr = r.Pos; } pl.Pos = pl.Disp = nr + new V2(float.Parse(ws[0]), float.Parse(ws[1])); } }
      string mreset = Environment.GetEnvironmentVariable("MAPRESET");  // MAPRESET=<s since the reset>: the map just reset (MapResetLook.cs): the old rooms folding in, the biggest one kept, map 2, 27 vectors back
      if (mreset != null) {
        xg.FoldRooms.Clear(); xg.FoldRooms.AddRange(xg.Expanded);
        var mcore = Expansion.GridCore();
        var kept = xg.KeepsakeChoices.Count > 0 ? Expansion.PlaceKeepsake(xg.Expanded, Expansion.BiggestRoom(xg.Expanded, xg.KeepsakeChoices), mcore) : new System.Collections.Generic.List<ExpansionRoom>();
        xg.Expanded.Clear(); xg.Expanded.AddRange(mcore); xg.Openings.Clear(); xg.Openings.AddRange(Expansion.GridCoreCuts()); xg.KeptHead = kept.Count > 0 ? xg.Expanded.Count : -1; xg.Expanded.AddRange(kept);
        typeof(Game).GetField("expWorld", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(xg, null);
        xg.MapNumber = 2; xg.MapResetAge = float.Parse(mreset, System.Globalization.CultureInfo.InvariantCulture);
        xg.MapRefunds.Clear(); xg.MapRefunds[xg.MyId] = 27;
        foreach (var pl in xg.Players.Values) pl.Pos = pl.Disp = new V2(Game.Width / 2f + 60f * (pl.Id - 1), Game.Height / 2f);
        foreach (var en in xg.Enemies.Values) en.Pos = en.Disp = new V2(-9000f, -9000f);
      }
      if (Environment.GetEnvironmentVariable("KEEPSAKE") != null) {  // KEEPSAKE=1: the keepsake vote open between waves (KeepsakeUi.cs), the other player's vote on the biggest room, 9 s left
        xg.State = MatchState.Intermission; xg.KeepsakeOpen = true; xg.KeepsakeLeft = 9f; xg.KeepsakeVotes.Clear();
        foreach (var pl in xg.Players.Values) if (pl.Id != xg.MyId) xg.KeepsakeVotes[pl.Id] = Expansion.BiggestRoom(xg.Expanded, xg.KeepsakeChoices);
      }

      var xrng = new System.Random(7); int xi = 0;
      foreach (var en in xg.Enemies.Values) {
        if (en.Def.IsBoss || en.Dormant) continue;  // (a dormant group stays where it was planted)
        var ar = xg.CurrentWorld.Areas[xi++ % xg.CurrentWorld.Areas.Length];
        en.Pos = en.Disp = xg.PushOutOfProps(new V2(ar.Min.X + 60f + (float)xrng.NextDouble() * Math.Max(1f, ar.Size.X - 120f), ar.Min.Y + 50f + (float)xrng.NextDouble() * Math.Max(1f, ar.Size.Y - 100f)), en.Radius);
      }
      string xlink = Environment.GetEnvironmentVariable("EXPANDLINK");  // EXPANDLINK=ax,ay,bx,by: that portal pair built (no checks), you standing by its first end
      if (xlink != null) {
        var lq = Array.ConvertAll(xlink.Split(','), int.Parse);
        var ln = new PortalLink { AX = (short)lq[0], AY = (short)lq[1], BX = (short)lq[2], BY = (short)lq[3] };
        xg.Links.Add(ln); typeof(Game).GetField("expWorld", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(xg, null);
        V2 lin = xg.ExpansionWorld.Portals[xg.ExpansionWorld.Portals.Length - 1].InA;
        int pi3 = 0; foreach (var pl in xg.Players.Values) { pl.Pos = pl.Disp = ln.A + lin * (160f + 40f * pi3) + lin.Orthogonal() * (60f * pi3); pi3++; }
      }
      if (Environment.GetEnvironmentVariable("EXPANDPORTAL") != null) {  // EXPANDPORTAL=1: the best portal pair built, you standing by one end
        var bl = Expansion.BestLink(xg.ExpansionWorld, 0);
        if (bl.HasValue) {
          xg.Links.Add(bl.Value); typeof(Game).GetField("expWorld", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(xg, null);
          V2 pin = (Expansion.LinkEnd(xg.ExpansionWorld, bl.Value.A) - bl.Value.A).Normalized();  // (into the room from the wall end)
          int pi2 = 0; foreach (var pl in xg.Players.Values) { pl.Pos = pl.Disp = bl.Value.A + pin * (110f + 40f * pi2) + pin.Orthogonal() * (60f * pi2); pi2++; }
        }
      }
      string gshut = Environment.GetEnvironmentVariable("EXPANDGATE");  // EXPANDGATE=<s left shut>: a shutter gate on the hub's first door (0: open, ready), you beside it; GATECOOL=<s> while open
      if (gshut != null) {
        var gw = xg.ExpansionWorld; var gd = gw.Doors[0];
        xg.GateFits.Add(Gates.Of(gd));
        var bfl = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        typeof(Game).GetMethod("SyncGateClocks", bfl).Invoke(xg, null);
        ((System.Collections.Generic.List<float>)typeof(Game).GetField("gateShut", bfl).GetValue(xg))[0] = float.Parse(gshut, System.Globalization.CultureInfo.InvariantCulture);
        string gc = Environment.GetEnvironmentVariable("GATECOOL");
        ((System.Collections.Generic.List<float>)typeof(Game).GetField("gateCool", bfl).GetValue(xg))[0] = gc != null ? float.Parse(gc, System.Globalization.CultureInfo.InvariantCulture) : (float.Parse(gshut, System.Globalization.CultureInfo.InvariantCulture) > 0f ? 25f : 0f);
        V2 side = gd.Across * -1f; if (gw.AreaAt(gd.Pos + side * 60f) != gd.A) side = gd.Across;
        int gi = 0; foreach (var pl in xg.Players.Values) { pl.Pos = pl.Disp = gd.Pos + side * (90f + 60f * gi) + gd.Along * 40f; gi++; }
        int ei = 0; foreach (var en in xg.Enemies.Values) { if (en.Def.IsBoss || ei >= 6) continue; en.Pos = en.Disp = gd.Pos - side * (40f + 26f * (ei % 3)) + gd.Along * (-50f + 34f * (ei / 3) + 10f * (ei % 2)); ei++; }
      }
      if (Environment.GetEnvironmentVariable("MYSTERY") != null) {  // MYSTERY=1: mystery doors rolled on the map (MysteryDoorLook.cs); MYSTERYAT=1 stands you by the first, MYSTERYSAVED=1 the first saved
        var mfl = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        xg.Wave = Math.Max(xg.Wave, 3);
        typeof(Game).GetMethod("RollMysteryDoors", mfl).Invoke(xg, null);
        if (xg.MysteryDoors.Count < 2) typeof(Game).GetMethod("RollMysteryDoors", mfl).Invoke(xg, null);
        Console.Error.WriteLine("mystery doors: " + xg.MysteryDoors.Count);
        if (Environment.GetEnvironmentVariable("MYSTERYHALL") != null) {  // MYSTERYHALL=1: a hallway door opened, its chest at the far end, you at the door
          typeof(Game).GetMethod("ResetMysteryDoors", mfl).Invoke(xg, null);
          foreach (var o in Expansion.GridMysteryOptions(xg.Expanded, PieceShape.TetI)) {
            var args = new object[] { o, null, null };
            if (!(bool)typeof(Game).GetMethod("MysteryDoorOf", mfl).Invoke(xg, args)) continue;
            var hd = (MysteryDoor)args[1];
            xg.MysteryDoors.Add(hd);
            ((System.Collections.Generic.List<SurpriseOption>)typeof(Game).GetField("mysteryPlans", mfl).GetValue(xg)).Add(o);
            ((System.Collections.Generic.List<V2>)typeof(Game).GetField("mysteryBehind", mfl).GetValue(xg)).Add((V2)args[2]);
            ((System.Collections.Generic.List<MysteryKind>)typeof(Game).GetField("mysteryKinds", mfl).GetValue(xg)).Add(MysteryKind.Chest);
            Player op = null; foreach (var pq in xg.Players.Values) { op = pq; break; }
            op.Vectors = op.VectorCap = 999; op.Pos = op.Disp = hd.Pos;
            typeof(Game).GetMethod("OpenMysteryDoor", mfl).Invoke(xg, new object[] { op, 0 });
            var hr = o.Rooms[0]; V2 hm = new V2((hr.X + hr.X1) / 2f, (hr.Y + hr.Y1) / 2f);
            xg.MysteryOpened.Clear(); int hi = 0; foreach (var pl in xg.Players.Values) { pl.Pos = pl.Disp = hm + new V2(30f * hi, 0f); hi++; }
            break;
          }
        }
        string mop = Environment.GetEnvironmentVariable("MYSTERYOPEN");  // MYSTERYOPEN=<s>: the first door opened that long ago (the reveal)
        if (mop != null && xg.MysteryDoors.Count > 0) {
          var md0 = xg.MysteryDoors[0]; Player opener = null; foreach (var pq in xg.Players.Values) { opener = pq; break; } opener.Pos = opener.Disp = md0.Pos; opener.Vectors = opener.VectorCap = 999;
          typeof(Game).GetMethod("OpenMysteryDoor", mfl).Invoke(xg, new object[] { opener, 0 });
          xg.MysteryOpened.Clear(); xg.MysteryOpened.Add(new System.Collections.Generic.KeyValuePair<MysteryDoor, float>(md0, xg.AnimT - float.Parse(mop, System.Globalization.CultureInfo.InvariantCulture)));
          V2 into0 = md0.Vertical ? new V2(1f, 0f) : new V2(0f, 1f); if (xg.CurrentWorld.AreaAt(md0.Pos - into0 * 60f) < 0) into0 = into0 * -1f;
          int oi = 0; foreach (var pl in xg.Players.Values) { pl.Pos = pl.Disp = md0.Pos - into0 * (110f + 60f * oi); oi++; }
        }
        if (xg.MysteryDoors.Count > 0 && Environment.GetEnvironmentVariable("MYSTERYSAVED") != null) { var md = xg.MysteryDoors[0]; md.Saver = xg.MyId; xg.MysteryDoors[0] = md; }
        if (xg.MysteryDoors.Count > 0 && Environment.GetEnvironmentVariable("MYSTERYAT") != null) {
          var md = xg.MysteryDoors[0]; var mw = xg.CurrentWorld;
          V2 into = md.Vertical ? new V2(1f, 0f) : new V2(0f, 1f); if (mw.AreaAt(md.Pos + into * 60f) < 0) into = into * -1f;
          int mi = 0; foreach (var pl in xg.Players.Values) { pl.Pos = pl.Disp = md.Pos + into * (80f + 60f * mi); pl.Vectors = 40; mi++; }
        }
      }
      string bage = Environment.GetEnvironmentVariable("BREACHAGE");  // BREACHAGE=<s>: this wave's breaches opened that long ago (BreachLook.cs); BREACHAT=1 stands you by the first
      if (bage != null) {
        xg.State = MatchState.Fighting;
        xg.BreachAge = float.Parse(bage, System.Globalization.CultureInfo.InvariantCulture);
        var bb = xg.ActiveBreaches;
        Console.Error.WriteLine("breaches: " + bb.Length + (bb.Length > 0 ? " first at " + bb[0].Pos.X + "," + bb[0].Pos.Y : "") + " state " + xg.State + " wave " + xg.Wave);
        if (bb.Length > 0 && Environment.GetEnvironmentVariable("BREACHEVENT") != null) xg.EventMark = bb[0].Pos;  // BREACHEVENT=1: the first breach is the breach event's (gold)
        if (bb.Length > 0 && Environment.GetEnvironmentVariable("BREACHAT") != null) { int bi = 0; foreach (var pl in xg.Players.Values) { pl.Pos = pl.Disp = bb[0].Pos + bb[0].In * (90f + 40f * bi) + bb[0].Along * (bi * 60f - 30f); bi++; } }
      }
      MockBreachRooms(xg);
      string fp = Environment.GetEnvironmentVariable("EXPANDAT");  // EXPANDAT=x,y: where you stand (default the hub)
      if (fp != null) { var q = fp.Split(','); foreach (var pl in xg.Players.Values) pl.Pos = pl.Disp = new V2(float.Parse(q[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(q[1], System.Globalization.CultureInfo.InvariantCulture)); }
    }
    if (Environment.GetEnvironmentVariable("WALLGLOW") != null) ArenaApp.WallBrightness = float.Parse(Environment.GetEnvironmentVariable("WALLGLOW"), System.Globalization.CultureInfo.InvariantCulture);  // WALLGLOW=<0-1>: walls and pillars brightness (LayoutLook.cs)
    if (Environment.GetEnvironmentVariable("FLOORTINT") != null) ArenaApp.FloorTintAmt = float.Parse(Environment.GetEnvironmentVariable("FLOORTINT"), System.Globalization.CultureInfo.InvariantCulture);  // FLOORTINT=<0-1>: the boss floor wash (LayoutLook.cs)
    if (Environment.GetEnvironmentVariable("FLOORBEND") == "0") ArenaApp.PreviewFloorStatic = true;
    if (Environment.GetEnvironmentVariable("FLOORSTYLE") != null) ArenaApp.PreviewFloorStyle = int.Parse(Environment.GetEnvironmentVariable("FLOORSTYLE"));  // FLOORSTYLE=<n>: the arena floor pattern, 1 Flux 2 Seismo 3 Plates 4 Circuit (FloorStylesLook.cs)
    if (Environment.GetEnvironmentVariable("DASHBURST") != null) ArenaApp.PreviewDashBurst = float.Parse(Environment.GetEnvironmentVariable("DASHBURST"), System.Globalization.CultureInfo.InvariantCulture);  // DASHBURST=<s>: a dash's floor burst that old, by the topmost player (FloorGridLook.cs)  // FLOORBEND=0: the static arena floor (FloorGridLook.cs)
    if (Environment.GetEnvironmentVariable("WALLBOSS") != null) ArenaApp.WallBossOverride = (EnemyType)Enum.Parse(typeof(EnemyType), Environment.GetEnvironmentVariable("WALLBOSS"));  // WALLBOSS=<EnemyType>: walls themed for that boss
    if (Environment.GetEnvironmentVariable("LAYOUT") != null) {  // LAYOUT=<LayoutId number, 1-6>: that floor plan with its cover, everyone out of its walls
      var lg = host.Game; lg.PropsOn = true; lg.DevSetLayout((LayoutId)int.Parse(Environment.GetEnvironmentVariable("LAYOUT"))); lg.LayoutAge = 5f;
      typeof(Game).GetMethod("WaveProps", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(lg, null);
      foreach (var pr in lg.Props.Values) pr.Age = 5f;
      foreach (var pl in lg.Players.Values) pl.Disp = pl.Pos = lg.PushOutOfProps(pl.Pos, 14f);
      foreach (var en in lg.Enemies.Values) en.Disp = en.Pos = lg.PushOutOfProps(en.Pos, en.Radius);
    }
    if (Environment.GetEnvironmentVariable("WALLTOUCH") != null) {  // WALLTOUCH=1|end [WALLT=<s>]: a wall touched (end: against its end cap) (WallTouchLook.cs): you pressed against the nearest inner wall's face, the arena empty; use with LAYOUT
      var wg = host.Game; var wq = Environment.GetEnvironmentVariable("WALLTOUCH").Split(',');
      ArenaApp.WallTouchPreview = wq[0] != "0";
      if (Environment.GetEnvironmentVariable("WALLT") != null) ArenaApp.WallTouchT = float.Parse(Environment.GetEnvironmentVariable("WALLT"), System.Globalization.CultureInfo.InvariantCulture);  // WALLT=<s>: the effect's clock
      wg.Enemies.Clear(); wg.Bullets.Clear(); wg.Effects.Clear(); wg.Props.Clear();
      wg.Minions.Clear(); wg.Zones.Clear();
      foreach (var pl in wg.Players.Values) {
        float best = 1e9f; V2 bp = pl.Pos;
        foreach (var s in wg.ActiveLayout.Solids) {  // the nearest long wall: you against the middle of its long face, on your side
          if (s.Round || s.Edge || Math.Max(s.HW, s.HH) < 60f) continue;
          float dl = pl.Pos.DistanceTo(s.Pos);
          if (dl >= best) continue;
          best = dl;
          bool tall = s.HH > s.HW; float r = pl.Def.BodyR + 0.5f;
          bp = wq[0] == "end" ? (tall ? new V2(s.Pos.X + 6f, s.Pos.Y + s.HH + r) : new V2(s.Pos.X + s.HW + r, s.Pos.Y + 6f)) : tall ? new V2(s.Pos.X + Math.Sign(pl.Pos.X - s.Pos.X + 0.01f) * (s.HW + r), s.Pos.Y + s.HH * 0.35f) : new V2(s.Pos.X + s.HW * 0.35f, s.Pos.Y + Math.Sign(pl.Pos.Y - s.Pos.Y + 0.01f) * (s.HH + r));
        }
        pl.Pos = pl.Disp = bp;
      }
    }
  }
}
