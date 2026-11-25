using System;
using System.Reflection;
using Arena;

// NEEDLES=<s>: the Healer Ninja's Acupuncture Needles (Core/AcupunctureNeedles.cs, Game/AcupunctureLook.cs) through the real
// game, that long in: you at (520, 470) holding Fire for 0.6 s at a hurt teammate 220 px out with a grunt in the way (the
// needles fly through it and stick in the teammate), and Alt (Bloodletting) at 0.1 s at one grunt and at 1.07 s at another.
// Use with mode hud, 3 players and ZOOM=2.2; NEEDLES=1.3 shows needles in the teammate and both grunts.
static partial class Shot {
  static void NeedlesScene() {
    string ts = Environment.GetEnvironmentVariable("NEEDLES");
    if (ts == null) return;
    float secs = float.Parse(ts, System.Globalization.CultureInfo.InvariantCulture);
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    me.Kit = Kit.Ninja; me.Role = Role.Healer; me.Def = Defs.Get(Role.Healer); me.Weapon = Defs.Weapon(Role.Healer, Kit.Ninja, 0);
    foreach (var sl in me.Slots) sl.Id = AbilityId.None;
    V2 home = new V2(520f, 470f);
    var mates = new System.Collections.Generic.List<Player>();
    foreach (var q in gm.Players.Values) if (q != me) mates.Add(q);
    var mateAt = new[] { new V2(220f, -50f), new V2(-60f, 150f) };
    var spots = new[] { new V2(110f, -25f), new V2(170f, 90f), new V2(250f, 40f) };
    var foes = new System.Collections.Generic.List<Enemy>();
    foreach (var at in spots) {
      var en = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Grunt, home + at, 0f, 0, false });
      en.Hp = en.MaxHp = 1e6f; en.SpawnT = 0f; en.FireT = 99f; en.Speed = 0f; foes.Add(en);
    }
    int frames = Math.Max(2, (int)Math.Round(secs * 60f));
    for (int f = 0; f < frames; f++) {
      for (int k = 0; k < foes.Count; k++) foes[k].Pos = foes[k].Disp = home + spots[k];
      for (int i = 0; i < mates.Count && i < 2; i++) { mates[i].Pos = mates[i].Disp = home + mateAt[i]; mates[i].Firing = false; if (f == 0) mates[i].Hp = mates[i].MaxHp * 0.4f; }
      me.Pos = me.Disp = home; me.InvulnT = 1f;
      bool alt1 = f >= 6 && f < 9, alt2 = f >= 64 && f < 67, alt = alt1 || alt2;
      V2 aim = alt1 ? foes[1].Pos : alt2 ? foes[2].Pos : home + mateAt[0];
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = aim, Fire = !alt && f < 36, WeaponAction = alt }); gm.Update(1f / 60f);
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    }
    me.AimAt = home + mateAt[0];
    Console.Error.WriteLine("needles: in the teammate {0}, in the grunts {1}/{2}/{3}, flying {4}", mates.Count > 0 ? mates[0].NeedleN : -1,
      foes[0].NeedlesIn, foes[1].NeedlesIn, foes[2].NeedlesIn, gm.Bullets.Count);
  }
}
