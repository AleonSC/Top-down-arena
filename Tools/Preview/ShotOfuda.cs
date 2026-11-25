using System;
using System.Reflection;
using Arena;

// OFUDA=<s>: the Healer Ninja's Ofuda (Core/Ofuda.cs, Game/OfudaLook.cs) through the real game, that long in: you at (520, 470)
// holding Fire toward a hurt teammate 300 px out and a little off your aim (the talismans bend onto it at the end of their
// flight), grunts ahead firing small shots, and Alt pressed at 0.1 s at a grunt (the Blast Ofuda sticks) and at 1.35 s at empty
// ground (it lands lit). Use with mode hud, 3 players and ZOOM=1.6.
static partial class Shot {
  static void OfudaScene() {
    string ts = Environment.GetEnvironmentVariable("OFUDA");
    if (ts == null) return;
    float secs = float.Parse(ts, System.Globalization.CultureInfo.InvariantCulture);
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    me.Kit = Kit.Ninja; me.Role = Role.Healer; me.Def = Defs.Get(Role.Healer); me.Weapon = Defs.Weapon(Role.Healer, Kit.Ninja, 2);
    foreach (var sl in me.Slots) sl.Id = AbilityId.None;
    V2 home = new V2(520f, 470f);
    var mates = new System.Collections.Generic.List<Player>();
    foreach (var q in gm.Players.Values) if (q != me) mates.Add(q);
    var mateAt = new[] { new V2(290f, -95f), new V2(-60f, 150f) };
    var spots = new[] { new V2(250f, 60f), new V2(300f, 120f), new V2(210f, 150f), new V2(360f, 30f) };
    var foes = new System.Collections.Generic.List<Enemy>();
    foreach (var at in spots) {
      var en = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Grunt, home + at, 0f, 0, false });
      en.Hp = en.MaxHp = 1e6f; en.SpawnT = 0f; en.FireT = 0.3f + 0.2f * foes.Count; en.Speed = 0f; foes.Add(en);
    }
    int frames = Math.Max(2, (int)Math.Round(secs * 60f));
    for (int f = 0; f < frames; f++) {
      for (int k = 0; k < foes.Count; k++) foes[k].Pos = foes[k].Disp = home + spots[k];
      for (int i = 0; i < mates.Count && i < 2; i++) { mates[i].Pos = mates[i].Disp = home + mateAt[i]; mates[i].Firing = false; if (f == 0) mates[i].Hp = mates[i].MaxHp * 0.45f; }
      me.Pos = me.Disp = home; me.InvulnT = 1f;
      bool alt = f >= 6 && f < 9 || f >= 80 && f < 83;
      V2 aim = f >= 80 && f < 83 ? home + new V2(180f, -180f) : alt ? foes[0].Pos : home + new V2(300f, -40f);
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = aim, Fire = !alt, WeaponAction = alt }); gm.Update(1f / 60f);
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    }
    me.AimAt = home + new V2(300f, -40f);
    Console.Error.WriteLine("ofuda: talismans {0}, blast charms flying {1}, lit {2}", System.Linq.Enumerable.Count(gm.Bullets.Values, b => b.Kind == Defs.OfudaShot),
      System.Linq.Enumerable.Count(gm.Bullets.Values, b => b.Kind == Defs.BlastOfudaShot), System.Linq.Enumerable.Count(gm.Zones.Values, z => z.Kind == ZoneKind.BlastOfuda));
  }
}
