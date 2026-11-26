using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;

// OCTO=<seconds since the cast>: the Healer Summoner's Octopus Familiar (Core/OctopusFamiliar.cs, Game/OctopusFamiliarLook.cs)
// played through the real game: you at (560, 470), a teammate at (820, 420) with grunts round them inside its reach and a
// few outside; the cast on them 0.3 s into the scene. It pops out by 0.45 s and its arms start slapping. OCTOHP=<0-1> sets its
// health, OCTOPICK=1 shows the pick instead (cursor on the teammate). Use with `ZOOM=2 shot.sh x hud 1 6 2`; try 0.2, 0.5, 1.4.
static partial class Shot {
  static void OctopusScene() {
    string ts = Environment.GetEnvironmentVariable("OCTO");
    bool picking = Environment.GetEnvironmentVariable("OCTOPICK") != null;
    if (ts == null && !picking) return;
    var inv = System.Globalization.CultureInfo.InvariantCulture;
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var cast = typeof(Game).GetMethod("HostAbility", flags, null, new[] { typeof(int), typeof(int), typeof(V2), typeof(bool) }, null);
    typeof(ArenaApp).GetField("juiceGame", flags).SetValue(app, gm);
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    if (gm.Profile != null) foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) gm.Profile.TagSeen[et.ToString()] = 99;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    me.Kit = Kit.Summoner; me.Role = Role.Healer; me.Def = Defs.Get(Role.Healer); me.Weapon = Defs.Weapon(Role.Healer, Kit.Summoner, 0);
    V2 home = new V2(560f, 470f), allyAt = new V2(820f, 420f);
    Player ally = null;
    foreach (var q in gm.Players.Values) if (q != me) { if (ally == null) ally = q; else q.Pos = q.Disp = new V2(150f, 820f); }
    var mobs = new List<Enemy>();
    var spots = new Dictionary<Enemy, V2>();
    V2[] at = { new V2(900f, 360f), new V2(930f, 450f), new V2(870f, 520f), new V2(760f, 510f), new V2(740f, 330f), new V2(830f, 300f),
                new V2(1060f, 420f), new V2(1010f, 250f) };
    foreach (var p in at) {
      var e = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Grunt, p, 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = 9999f; e.Pos = e.Disp = p; e.FireT = 99f; mobs.Add(e); spots[e] = p;
    }
    me.Slots[0].Id = AbilityId.OctopusFamiliar; me.Slots[0].Charges = 1; me.Slots[0].Timer = 0f;
    me.Aim = (allyAt - home).Angle;
    if (picking) {
      gm.OctoPickSlot = 0;
      me.Pos = me.Disp = home; me.AimAt = allyAt + new V2(6f, -4f);
      if (ally != null) ally.Pos = ally.Disp = allyAt;
      return;
    }
    int frames = (int)Math.Round((float.Parse(ts, inv) + 0.3f) * 60f);
    string hp = Environment.GetEnvironmentVariable("OCTOHP");
    for (int f = 0; f < frames; f++) {
      if (f == 18) cast.Invoke(gm, new object[] { me.Id, 0, allyAt, false });
      foreach (var e in mobs) { e.FireT = 99f; e.Pos = e.Disp = spots[e]; e.KnockLeft = V2.Zero; }
      me.Pos = me.Disp = home;
      if (ally != null) ally.Pos = ally.Disp = allyAt;
      if (hp != null) foreach (var m in gm.Minions.Values) if (m.Kind == MinionKind.Octopus) m.Hp = m.MaxHp * float.Parse(hp, inv);
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = allyAt }); gm.Update(1f / 60f);
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
      foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
      foreach (var m in gm.Minions.Values) m.Disp = m.Pos;
    }
    foreach (var m in gm.Minions.Values) if (m.Kind == MinionKind.Octopus) m.GuardFrac = m.Hp / m.MaxHp;
    int slaps = 0; foreach (var fx in gm.Effects) if (fx.Kind == FxKind.OctoSlap) slaps++;
    Console.Error.WriteLine("octopus: minions {0} arms out {1}", gm.Minions.Count, slaps);
  }
}
