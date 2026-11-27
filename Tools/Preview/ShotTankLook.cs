using System;
using System.Reflection;
using Arena;

// TANKLOOK=<-1|0-2>: the Tank Soldier model mockups (Game/TankSoldierMock.cs): you as a Tank Soldier with the Scattergun, standing
// still at (800, 450) facing up-right, nothing else on the field; -1 the current look. Use with mode hud, ZOOM=5 for a close-up.
static partial class Shot {
  static void TankLookScene() {
    string v = Environment.GetEnvironmentVariable("TANKLOOK");
    if (v == null) return;
    var gm = host.Game; gm.Bot = false;
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Enemies.Clear(); gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear(); gm.Props.Clear();
    me.Kit = Kit.Soldier; me.Role = Role.Tank; me.Def = Defs.Get(Role.Tank);
    me.Weapon = Defs.Weapon(Role.Tank, Kit.Soldier, int.Parse(Environment.GetEnvironmentVariable("TANKGUN") ?? "0"));
    me.MaxHp = me.Def.Hp; me.Hp = me.MaxHp; me.BarrierT = 0f; me.AegisT = 0f; me.ShieldHp = 0f; me.InvulnT = 0f; me.Flash = 0f;
    me.Pos = me.Disp = new V2(800f, 450f); me.Aim = float.Parse(Environment.GetEnvironmentVariable("TANKAIM") ?? "-0.5", System.Globalization.CultureInfo.InvariantCulture);
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = new V2(-400f, -400f);
    foreach (var sl in me.Slots) sl.Id = AbilityId.None;
    foreach (var q0 in gm.Players.Values) { q0.AegisT = 0f; q0.UltT = 0f; }
    me.IronLost = Game.IronhideMax; me.AimAt = me.Pos + V2.FromAngle(me.Aim) * 600f;
    ArenaApp.tankLookMock = int.Parse(v);
  }
}

// TANKSHEET=1 (mode hud): the sheet: the current look and the three concepts side by side, each big (the lobby's preview
// model, hit ring and all) and at play size below it, aimed three ways.
static partial class Shot {
  static bool TankSheet() {
    if (Environment.GetEnvironmentVariable("TANKSHEET") == null) return false;
    var F = BindingFlags.NonPublic | BindingFlags.Instance;
    UnityEngine.Event.current.type = UnityEngine.EventType.Repaint; set("glMaterial", new UnityEngine.Material(null)); UnityEngine.GL.ToUi = true;
    Ui.Fill(new UnityEngine.Rect(0f, 0f, 1280f, 720f), CrtStyle.Bg);
    string[] names = { "CURRENT", "A  BULWARK", "B  EXO FRAME", "C  BREACHER" };
    var p = (Player)typeof(ArenaApp).GetField("previewPlayer", F).GetValue(app);
    var begin = typeof(ArenaApp).GetMethod("BeginUiGl", F); var end = typeof(ArenaApp).GetMethod("EndUiGl", F);
    var at = typeof(ArenaApp).GetMethod("UiModelAt", F); var draw = typeof(ArenaApp).GetMethod("DrawPlayer", F);
    if (Environment.GetEnvironmentVariable("HITMARK") != null) ArenaApp.hitMarkMock = int.Parse(Environment.GetEnvironmentVariable("HITMARK"));  // HITMARK=<0-3>: a hitbox mark option
    float big = float.Parse(Environment.GetEnvironmentVariable("TANKZOOM") ?? "5.2", System.Globalization.CultureInfo.InvariantCulture);
    int gun = int.Parse(Environment.GetEnvironmentVariable("TANKGUN") ?? "0");
    for (int i = 0; i < 4; i++) {
      float cx = 160f + i * 320f;
      ArenaApp.tankLookMock = i - 1;
      p.Role = Role.Tank; p.Def = Defs.Get(Role.Tank); p.Kit = Kit.Soldier; p.Weapon = Defs.Weapon(Role.Tank, Kit.Soldier, gun);
      p.Hp = p.MaxHp = 1f; p.Disp = p.Pos = V2.Zero;
      p.Aim = -0.55f;
      if ((bool)begin.Invoke(app, null)) { at.Invoke(app, new object[] { new V2(cx, 300f), big }); draw.Invoke(app, new object[] { p, 1.3f, true }); end.Invoke(app, null); }
      float[] aims = { -1.57f, 0.4f, 2.6f };
      for (int k = 0; k < 3; k++) {
        p.Aim = aims[k];
        if ((bool)begin.Invoke(app, null)) { at.Invoke(app, new object[] { new V2(cx - 100f + k * 100f, 625f), 1.25f }); draw.Invoke(app, new object[] { p, 1.3f + k, true }); end.Invoke(app, null); }
      }
      Ui.Label(new UnityEngine.Rect(cx - 150f, 40f, 300f, 30f), names[i], 22, i == 0 ? CrtStyle.Dim : CrtStyle.Ink, UnityEngine.TextAnchor.MiddleCenter, true);
      Ui.Label(new UnityEngine.Rect(cx - 150f, 555f, 300f, 20f), "PLAY SIZE", 12, CrtStyle.Dim, UnityEngine.TextAnchor.MiddleCenter, true);
    }
    UnityEngine.GL.ToUi = false;
    ArenaApp.tankLookMock = -1;
    return true;
  }
}
