using System;
using System.Reflection;
using Arena;

// SENBON=<needles>[,<seconds since the last cast>[,<enemies>]]: the DPS Ninja's Senbon Seekers (Core/SenbonSeekers.cs,
// Game/SenbonSeekersLook.cs) through the real game: you at (800, 470) holding <needles> (3 a cast; 12 with SENBONCAP=1), the
// last cast that long ago (under 0.35 s: the new ones still coming out), then <enemies> grunts step in 170 px off 0.25 s before
// the frame, so needles are leaving the ring and curving in on them. Use with mode hud and ZOOM=2.
static partial class Shot {
  static void SenbonScene() {
    string ts = Environment.GetEnvironmentVariable("SENBON");
    if (ts == null) return;
    var parts = ts.Split(',');
    var inv = System.Globalization.CultureInfo.InvariantCulture;
    bool cap = Environment.GetEnvironmentVariable("SENBONCAP") != null;
    int want = Math.Max(3, Math.Min(cap ? Game.SbMaxMastered : Game.SbMax, int.Parse(parts[0])));
    float since = parts.Length > 1 ? float.Parse(parts[1], inv) : 1.5f;
    int foes = parts.Length > 2 ? int.Parse(parts[2]) : 0;
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var cast = typeof(Game).GetMethod("HostAbility", flags, null, new[] { typeof(int), typeof(int), typeof(V2), typeof(bool) }, null);
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    me.Kit = Kit.Ninja; me.Role = Role.Dps; me.Def = Defs.Get(Role.Dps); me.Weapon = Defs.Weapon(Role.Dps, Kit.Ninja, 0);
    V2 home = new V2(800f, 470f);
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = new V2(150f, 820f);
    me.Slots[0].Id = AbilityId.SenbonSeekers;
    me.SbN = 0;
    if (cap) me.MasteryXp[AbilityId.SenbonSeekers] = 1e6f;
    int casts = (want + Game.SbPer - 1) / Game.SbPer;
    int frames = (int)Math.Round(3f * 60f), lastCast = Math.Max(0, frames - (int)Math.Round(since * 60f)), foesAt = frames - 15;
    var spots = new[] { new V2(170f, -40f), new V2(-120f, -125f), new V2(40f, 175f), new V2(-180f, 60f) };
    var held = new System.Collections.Generic.List<Enemy>();
    for (int f = 0; f <= frames; f++) {
      if (f == 0)
        for (int k = 0; k < casts - 1; k++) { me.Slots[0].Charges = 1; me.Slots[0].Timer = 0f; cast.Invoke(gm, new object[] { me.Id, 0, home, false }); }
      if (f == lastCast) { me.Slots[0].Charges = 1; me.Slots[0].Timer = 0f; cast.Invoke(gm, new object[] { me.Id, 0, home, false }); }
      if (f == foesAt)
        for (int k = 0; k < foes && k < spots.Length; k++) {
          var en = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Grunt, home + spots[k], 0f, 0, false });
          en.Hp = en.MaxHp = 1e6f; en.SpawnT = 0f; en.FireT = 99f; held.Add(en);
        }
      for (int k = 0; k < held.Count; k++) { held[k].Pos = held[k].Disp = home + spots[k]; held[k].FireT = 99f; }
      me.Pos = me.Disp = home; me.InvulnT = 1f; me.Aim = 0f;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = home + new V2(100f, 0f) }); gm.Update(1f / 60f);
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    }
    Console.Error.WriteLine("senbon: held {0} flying {1}", me.SbN, System.Linq.Enumerable.Count(gm.Bullets.Values, b => b.Kind == Defs.Senbon));
  }
}
