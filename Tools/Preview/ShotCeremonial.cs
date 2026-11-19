using System;
using System.Reflection;
using Arena;

// CERE=<plates>,<seconds since the last cast>[,<hurt>]: the tank Ninja's Ceremonial Armor (Core/CeremonialArmor.cs,
// Game/CeremonialArmorLook.cs) through the real game: you at (800, 470), <plates> - 1 plates already circling, the last one
// summoned that long ago (under 0.55 s: still dropping in, with the sigil), enemy shots coming in from five sides.
// hurt=1 cracks a few plates and breaks one just before the frame. Use with mode hud and ZOOM=1.7.
static partial class Shot {
  static void CereScene() {
    string ts = Environment.GetEnvironmentVariable("CERE");
    if (ts == null) return;
    var parts = ts.Split(',');
    int plates = Math.Max(1, Math.Min(Game.CerePlateMax, int.Parse(parts[0])));
    float since = parts.Length > 1 ? float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture) : 1f;
    bool hurt = parts.Length > 2 && parts[2] == "1";
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var cast = typeof(Game).GetMethod("HostAbility", flags, null, new[] { typeof(int), typeof(int), typeof(V2), typeof(bool) }, null);
    var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
    var brk = typeof(Game).GetMethod("BreakCerePlate", flags);
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    me.Kit = Kit.Ninja; me.Role = Role.Tank; me.Def = Defs.Get(Role.Tank); me.Weapon = Defs.Weapon(Role.Tank, Kit.Ninja, 0);
    V2 home = new V2(800f, 470f);
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = new V2(150f, 820f);
    me.Slots[0].Id = AbilityId.CeremonialArmor;
    me.PlateN = 0;
    var src = new[] { new V2(-330f, -240f), new V2(360f, -200f), new V2(330f, 250f), new V2(-300f, 230f), new V2(0f, -300f) };
    int lastCast = Math.Max(0, (int)Math.Round((3.5f - since) * 60f));
    int frames = Math.Max(lastCast, (int)Math.Round(3.5f * 60f));
    for (int f = 0; f <= frames; f++) {
      if (f == 0) {
        for (int k = 0; k < plates - 1; k++) { me.Slots[0].Charges = 1; me.Slots[0].Timer = 0f; cast.Invoke(gm, new object[] { me.Id, 0, home, false }); me.PlateNewT = 0f; }
        if (hurt) { me.PlateHp[0] = me.PlateMax * 0.5f; me.PlateHp[2] = me.PlateMax * 0.2f; }
      }
      if (f == lastCast) { me.Slots[0].Charges = 1; me.Slots[0].Timer = 0f; cast.Invoke(gm, new object[] { me.Id, 0, home, false }); }
      if (hurt && f == frames - 9 && me.PlateN > 3) brk.Invoke(gm, new object[] { me, 1, Game.CerePlate(me, 1) });
      if (f % 20 == 0 && f > frames - 80)
        for (int k = 0; k < src.Length; k++) {
          V2 from = home + src[k], d = (home - from).Normalized();
          shoot.Invoke(gm, new object[] { Defs.SmallShot, from + d * (f % 40 == 0 ? 0f : 60f), d * 260f, 0f, 3f, 4f, 0, false, 0f });
        }
      me.Pos = me.Disp = home; me.InvulnT = 1f; me.Aim = 0f;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = home + new V2(100f, 0f) }); gm.Update(1f / 60f);
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    }
    Console.Error.WriteLine("cere: plates {0} newT {1:0.00} clock {2:0.0}", me.PlateN, me.PlateNewT, me.PlateClock);
  }
}
