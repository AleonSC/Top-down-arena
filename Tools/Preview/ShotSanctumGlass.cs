using System;
using System.Reflection;
using Arena;

// SANCTUM=<seconds into the scene>: the Healer Knight's Sanctum Glass (Core/SanctumGlass.cs, Game/SanctumGlassLook.cs) played
// through the real game like the mock-up: you at (620, 620) facing up, a hurt friend at (520, 700), five grunts above firing
// small shots (and a big one now and then) at you from 0.4 s; the pane is raised at 0.5 s. SANCTUMCAP=1 casts it mastered.
// Use with mode hud and 2 players; crop about (360, 280)-(880, 760).
static partial class Shot {
  static void SanctumGlassScene() {
    string ts = Environment.GetEnvironmentVariable("SANCTUM");
    if (ts == null) return;
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var cast = typeof(Game).GetMethod("HostAbility", flags);
    var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    if (gm.Profile != null) foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) gm.Profile.TagSeen[et.ToString()] = 99;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    me.Kit = Kit.Knight; me.Role = Role.Healer; me.Def = Defs.Get(Role.Healer); me.Weapon = Defs.Weapon(Role.Healer, Kit.Knight, 0);
    me.MaxHp = me.Def.Hp; me.Hp = me.MaxHp * 0.8f; me.ShieldHp = 0f;
    V2 home = new V2(620f, 620f), cursor = new V2(620f, 300f), friendAt = new V2(520f, 700f);
    Player friend = null;
    foreach (var q in gm.Players.Values) if (q != me) { if (friend == null) friend = q; q.Pos = q.Disp = new V2(150f, 900f); }
    if (friend != null) friend.Hp = friend.MaxHp * 0.35f;
    var spots = new[] { new V2(480f, 360f), new V2(590f, 330f), new V2(690f, 350f), new V2(780f, 385f), new V2(540f, 420f) };
    var mobs = new Enemy[spots.Length];
    for (int i = 0; i < spots.Length; i++) {
      var e = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Grunt, spots[i], 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = 5000f; e.Pos = e.Disp = spots[i]; e.FireT = 99f; mobs[i] = e;
    }
    me.Slots[0].Id = AbilityId.SanctumGlass; me.Slots[0].Charges = 1; me.Slots[0].Timer = 0f;
    if (Environment.GetEnvironmentVariable("SANCTUMCAP") != null) me.MasteryXp[AbilityId.SanctumGlass] = 99999f;
    var inv = System.Globalization.CultureInfo.InvariantCulture;
    int frames = (int)Math.Round(float.Parse(ts, inv) * 60f);
    var rng = new Random(5);
    for (int f = 0; f < frames; f++) {
      if (f == 30) cast.Invoke(gm, new object[] { 1, 0, cursor, false });
      if (f >= 24 && f % 6 == 0) {
        var e = mobs[rng.Next(mobs.Length)];
        V2 to = home + new V2((float)(rng.NextDouble() * 2 - 1) * 80f, -40f);
        bool big = f % 42 == 0;
        V2 v = (to - e.Pos).Normalized() * (big ? 130f : 180f);
        shoot.Invoke(gm, new object[] { big ? Defs.BigShot : Defs.SmallShot, e.Pos, v, 0f, 6f, 5f, 0, false, 0f });
      }
      for (int i = 0; i < mobs.Length; i++) if (mobs[i].Hp > 0f) { mobs[i].Pos = spots[i]; mobs[i].FireT = 99f; }
      me.Pos = home; me.InvulnT = 1f; me.Aim = (cursor - home).Angle;
      if (friend != null) { friend.Pos = friendAt; friend.InvulnT = 1f; friend.Hp = Math.Min(friend.Hp, friend.MaxHp * 0.7f); }
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = cursor }); gm.Update(1f / 60f);
    }
    foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
    foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    Console.WriteLine("sanctum glass: pane {0:0.0} s left, healed {1:0} of {2:0}", me.GlassT, me.GlassHealed, me.GlassCap);
  }
}
