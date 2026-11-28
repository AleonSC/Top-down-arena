using System;
using System.Reflection;
using Arena;

// CHALICE=<seconds since the cast>: the Healer Knight's Chalice of Life (Core/ChaliceOfLife.cs, Game/ChaliceOfLifeLook.cs)
// played through the real game: you at (700, 480) as a Healer Knight, hurt and wounded, the chalice dropped 120 px to your
// right 0.3 s into the scene; an ally walks up to drink from it at 1 s, you step in at 2.5 s. Use with mode hud.
static partial class Shot {
  static void ChaliceScene() {
    string ts = Environment.GetEnvironmentVariable("CHALICE");
    if (ts == null) return;
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var cast = typeof(Game).GetMethod("HostAbility", flags, null, new[] { typeof(int), typeof(int), typeof(V2), typeof(bool) }, null);
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear(); gm.Enemies.Clear();
    me.Kit = Kit.Knight; me.Role = Role.Healer; me.Def = Defs.Get(Role.Healer); me.Weapon = Defs.Weapon(Role.Healer, Kit.Knight, 0);
    V2 home = new V2(700f, 480f), cup = home + new V2(120f, 0f);
    Player ally = null;
    foreach (var q in gm.Players.Values) if (q != me) { if (ally == null) ally = q; q.Pos = q.Disp = new V2(150f, 820f); }
    V2 allyFrom = cup + new V2(170f, 110f), allyTo = cup + new V2(36f, 22f);
    if (ally != null) { ally.Pos = ally.Disp = allyFrom; ally.Hp = ally.MaxHp * 0.35f; ally.Wound = ally.MaxHp * 0.25f; }
    me.Slots[Upgrades.UltSlot].Id = AbilityId.ChaliceOfLife; me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f;
    me.Hp = me.MaxHp * 0.45f; me.Wound = me.MaxHp * 0.2f;
    int frames = (int)Math.Round((float.Parse(ts, System.Globalization.CultureInfo.InvariantCulture) + 0.3f) * 60f);
    V2 pos = home;
    for (int f = 0; f < frames; f++) {
      float t = f / 60f - 0.3f;
      if (f == 18) cast.Invoke(gm, new object[] { me.Id, Upgrades.UltSlot, cup, false });
      if (t > 2.5f) pos = V2.Lerp(home, cup + new V2(-40f, 4f), Math.Min(1f, (t - 2.5f) / 0.5f));
      me.Pos = me.Disp = pos; me.InvulnT = 1f; me.Aim = 0f;
      if (ally != null) { ally.Pos = V2.Lerp(allyFrom, allyTo, Math.Max(0f, Math.Min(1f, (t - 0.6f) / 0.5f))); ally.InvulnT = 1f; }
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = cup }); gm.Update(1f / 60f);
      foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    }
    Console.Error.WriteLine("chalice: you {0:0}+{1:0}/{2:0} shield {3:0}; ally {4:0}+{5:0}", me.Hp, me.OverHp, me.MaxHp, me.ShieldHp, ally != null ? ally.Hp : 0f, ally != null ? ally.OverHp : 0f);
  }
}
