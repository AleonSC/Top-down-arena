using System;
using System.Reflection;
using Arena;

// BLOODWALL=<seconds walking> (default 4): the tank Hemomancer's Blood Wall laid through the real game. The tank walks an
// S-curve across the arena with the wall up, a healer trailing; Grunts on the far side walk into it and their shots stop
// on it. BWCLOCK=<s> sets how long it has been up at the end (the drain ring's stage: 0 Seep, 5 Flow, 9 Gush, 13
// Hemorrhage). Use with mode hud, 2 players.
static partial class Shot {
  static void BloodWallScene() {
    string val = Environment.GetEnvironmentVariable("BLOODWALL");
    if (val == null) return;
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
    var cast = typeof(Game).GetMethod("HostAbility", flags);
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    if (gm.Profile != null) foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) gm.Profile.TagSeen[et.ToString()] = 99;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    me.Kit = Kit.Hemomancer; me.Role = Role.Tank; me.Def = Defs.Get(Role.Tank); me.Weapon = Defs.Weapon(Role.Tank, Kit.Hemomancer, 0);
    me.MaxHp = me.Def.Hp * Defs.KitHpMult(Kit.Hemomancer); me.Hp = me.MaxHp; me.ShieldHp = 0f;
    me.Slots[0].Id = AbilityId.CrimsonPact; me.Slots[0].Charges = 1;
    Player healer = null;
    foreach (var q in gm.Players.Values) if (q != me) { healer = q; q.Kit = Kit.Hemomancer; q.Role = Role.Healer; q.Def = Defs.Get(Role.Healer); q.Weapon = Defs.Weapon(Role.Healer, Kit.Hemomancer, 0); }
    float secs = float.Parse(val, System.Globalization.CultureInfo.InvariantCulture);
    Func<float, V2> path = u => new V2(330f + 620f * u, 560f - 120f * (float)Math.Sin(u * Math.PI * 1.3f));
    me.Pos = me.Disp = path(0f);
    cast.Invoke(gm, new object[] { 1, 0, me.Pos + new V2(200f, 0f), false });
    var mobs = new System.Collections.Generic.List<Enemy>();
    for (int i = 0; i < 6; i++) {
      V2 at = new V2(420f + 100f * i, 230f + 30f * (i % 2));
      var e = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Grunt, at, 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = 9999f; e.Pos = e.Disp = at; e.FireT = 99f; mobs.Add(e);
    }
    int frames = (int)Math.Round(secs * 60f);
    var rng = new Random(3);
    for (int f = 0; f < frames; f++) {
      float u = Math.Min(1f, f / (4f * 60f));
      me.Pos = path(u); me.Aim = -1.2f;
      if (healer != null) healer.Pos = path(Math.Max(0f, u - 0.07f)) + new V2(-10f, 34f);
      foreach (var e in mobs) {
        e.FireT = 99f;
        V2 to = me.Pos - e.Pos; float l = to.Length;
        if (f > 60 && l > 40f) e.Pos = e.Pos + to / l * 1.1f;  // walking in on the tank (the wall pushes them back)
      }
      if (f % 9 == 0 && f > 30) {  // the grunts' shots, aimed at the tank
        var src = mobs[rng.Next(mobs.Count)];
        V2 dv = (me.Pos - src.Pos).Normalized();
        shoot.Invoke(gm, new object[] { Defs.SmallShot, src.Pos + dv * 14f, dv * 260f, 0f, 5f, 8f, 0, false, 0f });
      }
      me.InvulnT = 1f;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = me.Pos + new V2(0f, -200f) }); gm.Update(1f / 60f);
    }
    string clk = Environment.GetEnvironmentVariable("BWCLOCK");
    if (clk != null) me.BloodWallClock = float.Parse(clk, System.Globalization.CultureInfo.InvariantCulture);
    me.Disp = me.Pos;
    foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
  }
}
