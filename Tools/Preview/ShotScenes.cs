using System; using System.IO; using System.Reflection; using System.Threading; using Arena; using UnityEngine;
// Shot.cs scene setup from environment flags: PlayerScenes poses the player and their weapon (POSE, BOW, VEIN,
// FLAME, the boss legendaries, CAST, ...) before the ArenaApp exists; ArenaScenes poses the arena with it (AIMLINE,
// ROOM, APPROACH, PATTERNS, PATCHES, MODS, PATHON, ...).
static partial class Shot {
  static void PlayerScenes() {
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
      // The Nimbus: its cloud over your cursor (200 px out), rain under it, and a strike NIMBUS=<seconds> ago.
      me.Weapon = Defs.Weapon(Role.Healer, Kit.Elementalist, 2); me.Kit = Kit.Elementalist; host.Game.Bot = false;
      me.Pos = me.Disp = new V2(450f, 450f);
      V2 cursor = new V2(700f, 360f);
      for (int i = 0; i < 200; i++) { me.Pos = me.Disp = new V2(450f, 450f); host.Poll(); host.Game.FixedStep(1f/60f, new PlayerInput { AimAt = cursor }); host.Game.Update(1f/60f); }
      float age = float.Parse(Environment.GetEnvironmentVariable("NIMBUS"), System.Globalization.CultureInfo.InvariantCulture);
      host.Game.Effects.Add(new Effect { Kind = FxKind.Strike, Pos = cursor, Pos2 = cursor, Color = new Rgba(0.78f, 0.86f, 1f), Radius = Game.NimbusBlast, Dur = Game.StrikeFxTime, T = age });
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
      me.Weapon = System.Array.Find(Defs.Weapons, w => w.Id == WeaponId.LeechBeam); me.Kit = Kit.Hemomancer; host.Game.Bot = false;
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
      // Soul Lantern cast alone: the lantern beside you, linked, SOULGHOST=<seconds> in.
      host.Game.Bot = false; me.Pos = me.Disp = new V2(700f, 450f); me.Aim = -0.4f; me.AimAt = me.Pos + V2.FromAngle(-0.4f) * 300f;
      me.Slots[0].Id = AbilityId.SoulLantern; me.Slots[0].Charges = 1;
      typeof(Game).GetMethod("HostAbility", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(host.Game, new object[] { 1, 0, me.Pos, false });
      for (int f = 0; f < 60 * float.Parse(Environment.GetEnvironmentVariable("SOULGHOST")); f++) { me.Pos = new V2(700f, 450f); host.Poll(); host.Game.FixedStep(1f / 60f, new PlayerInput { AimAt = me.AimAt }); host.Game.Update(1f / 60f); }
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
    if (Environment.GetEnvironmentVariable("PROPS") != null) {
      // The arena's props: a crate wall (one worn), an iron-bound crate, pots, a lit barrel, a closed chest, an opened one,
      // and your chest item (PROPS=2: a legendary) with you standing on it, so its card shows.
      System.Func<string, int> UL = n => System.Array.FindIndex(Upgrades.All, u => u.Name == n);
      var g = host.Game;
      g.Props.Clear(); g.Loots.Clear();
      foreach (var e in g.Enemies.Values) { e.Pos = e.Disp = new V2(1450f, 150f + 60f * (e.Id % 10)); e.FireT = 9f; }
      g.AddProp(PropKind.Crate, new V2(520f, 300f)); g.AddProp(PropKind.Crate, new V2(566f, 300f)).Hp = 3f;
      g.AddProp(PropKind.HeavyCrate, new V2(520f, 560f));
      g.AddProp(PropKind.Pot, new V2(700f, 230f)); g.AddProp(PropKind.Pot, new V2(735f, 255f));
      g.AddProp(PropKind.Barrel, new V2(1100f, 260f));
      var lit = g.AddProp(PropKind.Barrel, new V2(1160f, 330f)); lit.Fuse = 0.2f;
      g.AddProp(PropKind.Chest, new V2(1150f, 600f));
      g.AddProp(PropKind.HealthBarrel, new V2(1000f, 430f));
      var open = g.AddProp(PropKind.Chest, new V2(820f, 560f)); open.Open = true; open.OpenT = 0.8f;
      foreach (var p in g.Props.Values) p.Age = 3f + p.Id;
      int item = Environment.GetEnvironmentVariable("PROPS") == "2" ? UL("Warden's Guard") : UL("Second Charge");
      if (item < 0) item = 0;
      g.Loots[99] = new Loot { Id = 99, Owner = 1, Upgrade = item, Pos = new V2(820f, 690f), Age = 2.95f };
      g.Bot = false; me.Pos = me.Disp = new V2(822f, 695f); me.Aim = -1.2f;
      for (int i = 0; i < 6; i++) g.Bullets.Remove(i);
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
    if (Environment.GetEnvironmentVariable("RIOTLINE") != null) {
      // The Riot Line (the last wave before the Warden), RIOTLINE=<seconds> after it arrives, with you standing in the middle.
      var gm = host.Game; gm.Bot = false;
      foreach (var e in gm.Enemies.Values) e.Hp = 0f;
      gm.Bullets.Clear();
      me.Pos = me.Disp = new V2(800f, 450f);
      for (int f = 0; f < 3; f++) { gm.FixedStep(1f / 60f, new PlayerInput { AimAt = me.Pos + new V2(100f, 0f) }); gm.Update(1f / 60f); }
      typeof(Game).GetMethod("SpawnSquad", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(gm, new object[] { Approaches.For(EnemyType.Boss).Finale });
      float rt = float.Parse(Environment.GetEnvironmentVariable("RIOTLINE"));
      for (int f = 0; f < 60 * rt; f++) { me.Pos = new V2(800f, 450f); me.InvulnT = 1f; gm.FixedStep(1f / 60f, new PlayerInput { AimAt = me.Pos + new V2(100f, 0f) }); gm.Update(1f / 60f); }
      foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
      me.Disp = me.Pos;
      gm.Bullets.Clear();
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
    if (Environment.GetEnvironmentVariable("TITHE") != null) {
      // A tank Hemomancer's Vein Tithe (VeinTitheLook.cs): TITHE=<seconds since the cast> (the arteries grow to 1.2 s, then beat once a second; use 0.6 or 4.3),
      // with nine enemies held around. TITHESNAP=<seconds since it ended> shows the arteries letting go instead. Use with mode hud.
      var gm = host.Game; gm.Bot = false;
      foreach (var e in gm.Enemies.Values) e.Hp = 0f;
      gm.Bullets.Clear(); gm.Effects.Clear();
      me.Kit = Kit.Hemomancer; me.Role = Role.Tank; me.Def = Defs.Get(Role.Tank); me.Slots[Upgrades.UltSlot].Id = AbilityId.BloodRite;
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
      // An ability going off: CAST=<ability name>, CASTT=<seconds after the cast> (default 0.2), CASTSTORE=<Crimson Pact's
      // stored damage>. The caster stands in the middle with the enemies brought in around them; its name pops up over them.
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
      if (Environment.GetEnvironmentVariable("CASTSTORE") != null) me.PactStore = float.Parse(Environment.GetEnvironmentVariable("CASTSTORE"));
      float ct = float.Parse(Environment.GetEnvironmentVariable("CASTT") ?? "0.2");
      for (int f = 0; f < 60 * ct; f++) { me.Pos = new V2(800f, 450f); me.InvulnT = 1f; gm.FixedStep(1f / 60f, new PlayerInput { AimAt = me.AimAt }); gm.Update(1f / 60f); }
      me.Disp = me.Pos;
      if (aid == AbilityId.Bulldoze) me.Disp = me.Pos + V2.FromAngle(me.Aim) * System.Math.Min(200f, 750f * System.Math.Min(ct, 0.35f));  // the charge itself is the player's own game; show how far it has got
      foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    }
    if (Environment.GetEnvironmentVariable("REVOLVER") != null) { me.Weapon = Defs.Weapon(Role.Healer, Kit.Soldier, 1); me.AmmoUsed = 3; me.Stats.ExtraShots = 1; host.Game.Bot = false; me.Pos = me.Disp = new V2(800f, 450f); Console.WriteLine("cap " + host.Game.RevolverCapacity(me)); }
  }
  static void ArenaScenes() {
    // SHOTW=<WeaponId>: that weapon in your hand, three volleys fired by the game's own code and moved on 0.06 / 0.18 /
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
      else if (wd.Id == WeaponId.ArcLightning) {
        // Arc Lightning: enemies where the mockup has them (arc-lightning.html). SHOTC=<0..1>: that far through the hold (the
        // charge). Otherwise one full-charge release, the frame SHOTT s into it.
        var spawnE = typeof(Game).GetMethod("SpawnEnemyAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        foreach (var sp in new[] { new V2(170f, -10f), new V2(300f, 30f), new V2(420f, -35f), new V2(540f, 5f), new V2(650f, 38f), new V2(760f, -20f) }) {
          var en = (Enemy)spawnE.Invoke(gm, new object[] { EnemyType.Grunt, me.Pos + new V2(sp.X, sp.Y), 0f, 0, false });
          en.SpawnT = 0f; en.Disp = en.Pos; en.MaxHp = en.Hp = 1e5f;
        }
        string sc = Environment.GetEnvironmentVariable("SHOTC");
        if (sc != null) me.DrawT = Game.BowMinDraw + (Game.BowFullDraw - Game.BowMinDraw) * float.Parse(sc, System.Globalization.CultureInfo.InvariantCulture);
        else {
          me.BowShotDraw = Game.BowFullDraw;
          fireV.Invoke(gm, new object[] { me, 0f, 0 });
          float st2 = float.Parse(Environment.GetEnvironmentVariable("SHOTT") ?? "0.06", System.Globalization.CultureInfo.InvariantCulture);
          foreach (var fx in gm.Effects) fx.T = st2;
        }
      }
      else foreach (float age in new[] { 0.3f, 0.18f, 0.06f }) {
        var before = new System.Collections.Generic.HashSet<int>(gm.Bullets.Keys);
        fireV.Invoke(gm, new object[] { me, 0f, 0 });
        foreach (var bl in gm.Bullets.Values) if (!before.Contains(bl.Id)) { bl.Pos += bl.Vel * age; bl.Life -= age; }
      }
    }
    // SWING=<seconds>: your melee weapon's attack that far in (with KNIGHT=, NINJA= or BLOOD=; MeleeLooks.cs draws it
    // with Visual effects on). Built like the game's own effects: Slash with the weapon's half arc (negative: a
    // thrust), the Halberd's whole swing (SWING = seconds after the click, HALBERDUP=1 for the return sweep), Jab boxes for the Lance and the Warhammer.
    if (Environment.GetEnvironmentVariable("SWING") != null) {
      float st = float.Parse(Environment.GetEnvironmentVariable("SWING"), System.Globalization.CultureInfo.InvariantCulture);
      var gm = host.Game; gm.Bot = false; gm.Effects.Clear(); gm.Bullets.Clear();
      // SWINGW=<WeaponId>: any melee weapon in your hand (e.g. Greatsword, Rapier, Halberd, Warhammer, Katana, BoStaff).
      var wn = Environment.GetEnvironmentVariable("SWINGW");
      if (wn != null) { var wd = System.Array.Find(Defs.Weapons, x => x.Id.ToString() == wn); if (wd != null) { me.Weapon = wd; me.Kit = wd.Kit; } }
      me.Pos = me.Disp = new V2(700f, 470f); me.Aim = -0.15f; me.DrawT = 0f;
      int k = 0;
      foreach (var en in gm.Enemies.Values) { if (en.Def.IsBoss) continue; float a = me.Aim + (k - 1) * 0.55f; en.Pos = en.Disp = me.Pos + V2.FromAngle(a) * (70f + 25f * (k % 2)); if (++k >= 3) break; }
      foreach (var en in gm.Enemies.Values) if (!en.Def.IsBoss && k-- <= 0) { en.Pos = en.Disp = new V2(150f, 120f); }
      var col = Defs.BulletColor(Defs.KnightSlash);
      float reach = Game.MeleeReach(me, me.Weapon.Reach);
      V2 to = me.Pos + V2.FromAngle(me.Aim) * reach;
      Action<FxKind, V2, float, float, float> fx = (kind, p2, rad, t0, dur) => gm.Effects.Add(new Effect { Kind = kind, Pos = me.Pos, Pos2 = p2, Color = col, Radius = rad, T = st - t0, Dur = dur });
      switch (me.Weapon.Id) {
        case WeaponId.Warhammer: gm.Effects.Add(new Effect { Kind = FxKind.Jab, Pos = me.Pos, Pos2 = me.Pos + V2.FromAngle(me.Aim) * Game.HammerCentre(reach), Color = Game.HammerBoomColor.WithAlpha(float.Parse(Environment.GetEnvironmentVariable("HAMMERPOW") ?? "1", System.Globalization.CultureInfo.InvariantCulture)), Radius = Game.HammerRadius, T = st, Dur = 0.26f }); break;
        case WeaponId.Lance: fx(FxKind.Jab, to, 44f, 0f, 0.26f); break;
        case WeaponId.Rapier: case WeaponId.Kusarigama: fx(FxKind.Slash, to, -0.12f, 0f, 0.16f); break;
        case WeaponId.Halberd: { bool hup = Environment.GetEnvironmentVariable("HALBERDUP") == "1"; fx(FxKind.Slash, to, 0.7f + 18f * MathX.Pi / 360f, 0f, HalberdMotion.Total(hup)); break; }  // one effect from the click (HalberdMotion.cs); HALBERDUP=1: with the return sweep
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
        case WeaponId.HolyMace:  // STAFFJAB=1 the jab's cone (else the swipe), STACKS=<n> Jab stacks held
          me.StaffStacks = (byte)int.Parse(Environment.GetEnvironmentVariable("STACKS") ?? "2");
          if (Environment.GetEnvironmentVariable("STAFFJAB") == "1") fx(FxKind.Slash, me.Pos + V2.FromAngle(me.Aim) * Game.MeleeReach(me, Game.StaffJabReach), -Game.StaffJabArc * MathX.Pi / 360f, 0f, 0.24f);
          else fx(FxKind.Slash, to, Game.StaffSwipeArc * MathX.Pi / 360f, 0f, 0.16f);
          break;
        case WeaponId.Greatsword:  // GREAT=guard (TEMPER=<n> points held), GREAT=wind (the overhead rend's wind-up, first frame), GREAT=rend (the Rend, TEMPER=<n>), else the cut
        {
          string gmode = Environment.GetEnvironmentVariable("GREAT") ?? "cut";
          int gn = int.Parse(Environment.GetEnvironmentVariable("TEMPER") ?? "3");
          if (gmode == "guard") { me.GreatGuard = true; me.GreatTemper = (byte)gn; }
          else if (gmode == "wind") me.GreatStep = 3;
          else if (gmode == "rend") gm.Effects.Add(new Effect { Kind = FxKind.GreatRend, Pos = me.Pos, Pos2 = me.Pos + V2.FromAngle(me.Aim) * Game.GreatRendReach, Color = col.WithAlpha(0.2f + 0.8f * gn / Game.GreatTemperMax), Radius = Game.GreatRendWidth + Game.GreatRendWidthPer * gn, T = st, Dur = Game.GreatRendFxTime });
          else { me.GreatStep = 1; fx(FxKind.Slash, to, Game.GreatCutArc * MathX.Pi / 360f, 0f, 0.16f); }
          break;
        }
        case WeaponId.Censer: fx(FxKind.Slash, to, MathX.Pi, 0f, 0.24f); break;
        case WeaponId.TwinBlades: case WeaponId.Katana: fx(FxKind.Slash, to, 1.05f, 0f, 0.16f); break;
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
    if (Environment.GetEnvironmentVariable("AIMDEBUG") != null)
    {
      typeof(ArenaApp).GetField("aimDebug", f2).SetValue(app, true);
      me.Aim = -0.6f;
      host.Game.LastShotId = 1; host.Game.LastShotAngle = -0.62f; host.Game.LastShotAim = -0.6f; host.Game.LastShotFrom = me.Pos + V2.FromAngle(-0.62f) * 20f;
    }
    if (Environment.GetEnvironmentVariable("DPSMETER") != null)
    {
      // The DPS meter (DpsMeterUi.cs) with a dummy and a few seconds of fake damage; add DEVTAB=3 mode=dev for the F1 tab.
      typeof(ArenaApp).GetField("dpsMeter", f2).SetValue(app, true);
      host.Game.DevSpawnDummy(1);
      var track = new DpsTrack();
      for (int i = 0; i < 70; i++) { track.Add(i % 3 == 0 ? 9f : 22f, i % 4 == 0 ? 3f : 0f); track.Advance(0.1f); }
      host.Game.Dps[1] = track;
    }
    if (Environment.GetEnvironmentVariable("FROST") != null) {
      // The Frost Lance's icicle FROST=<0..1> of the way through, with its splinters once they've gone off.
      float x = float.Parse(Environment.GetEnvironmentVariable("FROST")), it = x * Game.IcicleFxTime, st = it - Game.SplinterDelay;
      me.Pos = me.Disp = new V2(600f, 450f); me.Aim = -0.3f; host.Game.Bot = false;
      V2 tipAt = me.Pos + V2.FromAngle(me.Aim) * 230f;
      var frost = Defs.BulletColor(Defs.FrostShot);
      host.Game.Effects.Add(new Effect { Kind = FxKind.Icicle, Pos = me.Pos, Pos2 = tipAt, Color = frost, Radius = Game.IcicleHalfWidth, Dur = Game.IcicleFxTime, T = it });
      if (st > 0f && st < Game.SplinterFxTime)
        host.Game.Effects.Add(new Effect { Kind = FxKind.Splinter, Pos = me.Pos, Pos2 = tipAt, Color = frost, Radius = Game.SplinterReach, Dur = Game.SplinterFxTime, T = st });
    }
    if (Environment.GetEnvironmentVariable("REPULSE") != null) {
      // The Repulsion Cannon's blast REPULSE=<seconds> after the shot.
      float rt = float.Parse(Environment.GetEnvironmentVariable("REPULSE"));
      me.Pos = me.Disp = new V2(600f, 450f); me.Aim = -0.2f; host.Game.Bot = false;
      host.Game.Effects.Add(new Effect { Kind = FxKind.Repulse, Pos = me.Pos, Pos2 = me.Pos + V2.FromAngle(me.Aim) * 253f, Color = new Rgba(0.55f, 0.95f, 1f), Radius = 0.61f, Dur = Game.RepulseFxTime, T = rt });
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
      var rows = new[] { WeaponId.TwinRifle, WeaponId.Railgun, WeaponId.Pistol, WeaponId.Scattergun, WeaponId.ArcaneMissiles };
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
        gg.State = MatchState.Intermission; gg.Room = RoomPhase.Portal; gg.RoomKind = ChallengeKind.HoldZones; gg.RoomT = 11.4f; gg.PortalPos = new V2(1150f, 200f); gg.PortalFill = 0.55f;
        rps[0].Pos = rps[0].Disp = new V2(1030f, 380f);
      } else {
        gg.State = MatchState.Fighting;
        gg.RoomKind = kindOf(room == "won" || room == "lost" ? (Environment.GetEnvironmentVariable("KIND") ?? "defend") : room);
        gg.Room = room == "won" ? RoomPhase.Won : room == "lost" ? RoomPhase.Lost : Environment.GetEnvironmentVariable("ROOMINTRO") != null ? RoomPhase.Intro : RoomPhase.Active;
        if (gg.Room == RoomPhase.Intro) gg.RoomT = 2.2f;
        gg.RoomClock = Challenges.Clock(gg.RoomKind) * 0.55f; gg.CrystalPos = gg.AltarPos = new V2(800f, 450f); gg.CrystalMax = 700f; gg.CrystalHp = 420f;
        gg.RoomGoal = 44; gg.RoomKills = 27; gg.ZoneIndex = 1; gg.ZoneFill = 0.6f;
        gg.RoomZones[0] = Challenges.ZoneSpots[0]; gg.RoomZones[1] = Challenges.ZoneSpots[1]; gg.RoomZones[2] = Challenges.ZoneSpots[2];
        if (gg.Room == RoomPhase.Active) {
          if (gg.RoomKind == ChallengeKind.Defend) { put(EnemyType.Grunt, new V2(560f, 300f)); put(EnemyType.Spiker, new V2(690f, 380f)); put(EnemyType.Brute, new V2(1000f, 520f)); rps[0].Pos = rps[0].Disp = new V2(760f, 560f); }
          if (gg.RoomKind == ChallengeKind.KillRush) for (int i = 0; i < 9; i++) put(i % 3 == 0 ? EnemyType.Grunt : EnemyType.Spiker, new V2(300f + i * 120f, 250f + (i % 3) * 90f));
          if (gg.RoomKind == ChallengeKind.HoldZones) { rps[0].Pos = rps[0].Disp = gg.RoomZones[1] + new V2(-30f, 20f); put(EnemyType.Grunt, gg.RoomZones[1] + new V2(140f, -90f)); put(EnemyType.Sniper, new V2(800f, 160f)); }
          if (gg.RoomKind == ChallengeKind.MiniBoss) { var gk = put(EnemyType.Gatekeeper, new V2(800f, 330f)); gk.Hp = gk.MaxHp * 0.42f; gk.Phase = 1; gk.Facing = (rps[0].Pos - gk.Pos).Angle; }
          if (gg.RoomKind == ChallengeKind.Mirror) foreach (var pl in rps) { var sh = put(EnemyType.Shadow, new V2(1600f - pl.Pos.X, 900f - pl.Pos.Y)); gg.ShadowOf[sh.Id] = pl.Id; sh.Facing = (pl.Pos - sh.Pos).Angle; }
        }
      }
      set("lookPhase", gg.Room); set("lookPhaseAt", Time.unscaledTime - age); set("wasInRoom", gg.InRoom); set("warpAt", -10f);
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
    if (Environment.GetEnvironmentVariable("FLANKERS") != null) {
      // The Hive Queen's Swarm bombers: two swooping out to your sides (gold) and two diving in (red-hot).
      var sef = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      host.Game.Enemies.Clear(); host.Game.Bullets.Clear(); host.Game.Bot = false;
      me.Pos = me.Disp = new V2(800f, 560f);
      Func<V2, float, byte, Enemy> fl = (at, face, ph) => { var en = (Enemy)sef.Invoke(host.Game, new object[] { EnemyType.HiveBomber, at, 0f, 0, false }); en.SpawnT = 0f; en.Disp = en.Pos; en.Facing = face; en.Phase = ph; return en; };
      fl(new V2(560f, 330f), 2.6f, 1); fl(new V2(1060f, 360f), 0.6f, 1);
      fl(new V2(470f, 560f), 0f, 2); fl(new V2(1150f, 520f), MathX.Pi, 2);
      var qn = (Enemy)sef.Invoke(host.Game, new object[] { EnemyType.HiveQueen, new V2(800f, 160f), 0f, 0, false }); qn.SpawnT = 0f; qn.Disp = qn.Pos;
    }
    if (Environment.GetEnvironmentVariable("PATTERNS") != null) {
      // The new shooting patterns: a Pauser with its shots hanging, a Leader aiming ahead, a Stitcher's orbs and threads.
      var sbp = typeof(Game).GetMethod("SpawnBullet", BindingFlags.NonPublic | BindingFlags.Instance);
      var sep = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
      host.Game.Enemies.Clear(); host.Game.Bullets.Clear(); host.Game.Bot = false;
      me.Pos = me.Disp = new V2(800f, 470f);
      Func<EnemyType, V2, Enemy> put = (ty, at) => { var en = (Enemy)sep.Invoke(host.Game, new object[] { ty, at, 0f, 0, false }); en.SpawnT = 0f; en.Disp = en.Pos; return en; };
      var pa = put(EnemyType.Pauser, new V2(420f, 250f));
      float aimP = (me.Pos - pa.Pos).Angle;
      for (int k = 0; k < Game.PauseShots; k++) {
        V2 d = V2.FromAngle(aimP + Game.PauseFan * (k / (Game.PauseShots - 1f) - 0.5f));
        var b = (Bullet)sbp.Invoke(host.Game, new object[] { Defs.PauseShot, pa.Pos + d * (pa.Radius + 102f), d * Game.PauseSpeed, 0f, Game.PauseFly + Game.PauseHold, 12f, 0, false, 0f });
        b.Life = Game.PauseHold * 0.4f;
      }
      var le = put(EnemyType.Leader, new V2(1250f, 260f)); le.Phase = 1; le.Facing = (me.Pos + new V2(160f, 40f) - le.Pos).Angle;
      var s1 = put(EnemyType.Stitcher, new V2(1150f, 760f)); var s2 = put(EnemyType.Stitcher, new V2(1380f, 560f));
      // The pair's volley closing in, and an older one further along; a lone Stitcher's two orbs.
      foreach (float f in new[] { 0.35f, 0.75f }) {
        sbp.Invoke(host.Game, new object[] { Defs.StitchShot, s1.Pos + (me.Pos - s1.Pos) * f, (me.Pos - s1.Pos).Normalized() * 105f, 0f, 7f, 12f, 0, false, 0f });
        sbp.Invoke(host.Game, new object[] { Defs.StitchShot, s2.Pos + (me.Pos - s2.Pos) * f, (me.Pos - s2.Pos).Normalized() * 105f, 0f, 7f, 12f, 0, false, 0f });
      }
      var s3 = put(EnemyType.Stitcher, new V2(300f, 780f));
      for (int k = -1; k <= 1; k += 2) {
        V2 d = V2.FromAngle((me.Pos - s3.Pos).Angle + k * Game.StitchSpread);
        sbp.Invoke(host.Game, new object[] { Defs.StitchShot, s3.Pos + d * 260f, d * 105f, 0f, 7f, 12f, 0, false, 0f });
      }
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
    if (Environment.GetEnvironmentVariable("PATHON") != null) { host.Game.CurrentPath = (PathId)int.Parse(Environment.GetEnvironmentVariable("PATHON")); host.Game.PathFrom = host.Game.Wave - 1; host.Game.PathTo = host.Game.Wave + 2; host.Game.Mods = RunMod.Frenzy; if (host.Game.CurrentPath == PathId.Fogbound) host.Game.FogF = 1f; if (host.Game.CurrentPath == PathId.NarrowPass) { host.Game.NarrowF = 1f; foreach (var pl in host.Game.Players.Values) pl.Disp = pl.Pos = pl.Pos.Clamp(host.Game.ArenaMin + new V2(30f, 30f), host.Game.ArenaMax - new V2(30f, 30f)); foreach (var en in host.Game.Enemies.Values) en.Disp = en.Pos = en.Pos.Clamp(host.Game.ArenaMin + new V2(30f, 30f), host.Game.ArenaMax - new V2(30f, 30f)); } }
    if (Environment.GetEnvironmentVariable("LAYOUT") != null) {  // LAYOUT=<LayoutId number, 1-6>: that floor plan with its cover, everyone out of its walls
      var lg = host.Game; lg.PropsOn = true; lg.DevSetLayout((LayoutId)int.Parse(Environment.GetEnvironmentVariable("LAYOUT"))); lg.LayoutAge = 5f;
      typeof(Game).GetMethod("WaveProps", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(lg, null);
      foreach (var pr in lg.Props.Values) pr.Age = 5f;
      foreach (var pl in lg.Players.Values) pl.Disp = pl.Pos = lg.PushOutOfProps(pl.Pos, 14f);
      foreach (var en in lg.Enemies.Values) en.Disp = en.Pos = lg.PushOutOfProps(en.Pos, en.Radius);
    }
  }
}
