using System;
using System.Reflection;
using Arena;

// GRAZE=<seconds into the scene>: Graze (Core/Graze.cs, Game/GrazeLook.cs) played through the real game: you at (560, 420) with
// shots flying in from the right that skim past you above and below, one every 0.07 s from 0.3 s (the 0.2 s gap is lifted here so
// several streaks show at once), your ultimate recharging so its tile flashes. Use with mode hud and ZOOM=2 for a close look.
static partial class Shot {
  static void GrazeScene() {
    string ts = Environment.GetEnvironmentVariable("GRAZE");
    if (ts == null) return;
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
    foreach (var e in gm.Enemies.Values) { e.Hp = 0f; }
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    foreach (var e in gm.Enemies.Values) e.Pos = e.Disp = new V2(1500f, 950f);
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = new V2(150f, 900f);
    me.Kit = Kit.Soldier; me.Role = Role.Dps; me.Def = Defs.Get(Role.Dps); me.Weapon = Defs.Weapon(Role.Dps, Kit.Soldier, 0);
    me.MaxHp = me.Def.Hp; me.Hp = me.MaxHp; me.ShieldHp = 0f;
    V2 home = new V2(560f, 420f);
    var ult = me.Slots[Upgrades.UltSlot]; ult.Charges = 0; ult.Timer = 30f;
    gm.DevSetGod(me.Id, true);
    var inv = System.Globalization.CultureInfo.InvariantCulture;
    int frames = (int)Math.Round(float.Parse(ts, inv) * 60f);
    float[] miss = { 1f, -1f, 0.6f, -0.4f, 0.9f, -0.8f, 0.3f };
    int shot = 0;
    for (int f = 0; f < frames; f++) {
      if (f >= 18 && (f - 18) % 4 == 0) {
        float r = me.Def.HitR + 5f + 9f * Math.Abs(miss[shot % miss.Length]);
        float y = home.Y + Math.Sign(miss[shot % miss.Length]) * r;
        shoot.Invoke(gm, new object[] { Defs.SmallShot, new V2(home.X + 90f, y), new V2(-520f, (shot % 3 - 1) * 40f), 0f, 2f, 5f, 0, false, 0f });
        shot++;
      }
      foreach (var e in gm.Enemies.Values) { e.FireT = 99f; e.Pos = new V2(1500f, 950f); }
      me.Pos = home; me.InvulnT = 0f; me.AegisT = 0f; me.BarrierT = 0f; me.DomeT = 0f; me.LevelFlash = 0f; me.GrazeGap = 0f; me.Aim = 0f;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = home + new V2(200f, 0f) }); gm.Update(1f / 60f);
    }
    foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
    foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    Console.WriteLine("graze: " + me.Grazes + " grazes, ultimate " + ult.Timer.ToString("0.0") + " s");
  }
}
