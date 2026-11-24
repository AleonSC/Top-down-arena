using System;
using System.Collections.Generic;
using System.Reflection;
using Arena;
using UnityEngine;

// BOWFOCUS=<phase>,<s>: the Mender's Bow's Focus shot and Scatter (Core/BowFocus.cs, Game/BowFocusLook.cs) played through
// the real game and Juice.cs: you, a Healer Soldier with the bow, at (520, 450) aiming right at a loose line of grunts.
//   rest     standing with an arrow on the string (s ignored)
//   draw     drawing: s seconds into the draw (the camera easing out and back, ChargeCameraFx.cs)
//   bar      drawing: s seconds after the draw is full (the Focus bar up, the bow glowing)
//   focus    let go in the gold zone: s seconds after (the Focus arrow, its wind, FOCUS!, the bar bursting)
//   miss     let go outside the zone: s seconds after (a plain full arrow, the bar shaking away)
//   swing    a tap: s seconds after it (0 to 0.25: the fresh arrow swung from the quiver to the string)
//   scatter  Alt: s seconds after (the fan of five arrows)
// Use with mode hud, 1 player; ZOOM=2 frames you and the grunts. FXOFF=1: Visual effects off.
static partial class Shot {
  static void BowFocusScene() {
    string env = Environment.GetEnvironmentVariable("BOWFOCUS");
    if (env == null) return;
    var ci = System.Globalization.CultureInfo.InvariantCulture;
    string[] parts = env.Split(',');
    string phase = parts[0];
    float after = parts.Length > 1 ? float.Parse(parts[1], ci) : 0f;
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var juice = typeof(ArenaApp).GetMethod("UpdateJuice", flags);
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    if (gm.Profile != null) foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) gm.Profile.TagSeen[et.ToString()] = 99;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    gm.State = MatchState.Fighting; gm.StateTimer = 999f;
    me.Kit = Kit.Soldier; me.Role = Role.Healer; me.Def = Defs.Get(Role.Healer); me.Weapon = Defs.Weapon(Role.Healer, Kit.Soldier, 2);
    me.MaxHp = me.Def.Hp; me.Hp = me.MaxHp;
    V2 home = new V2(520f, 450f);
    me.Pos = me.Disp = home; me.Aim = 0f;
    var mobs = new List<Enemy>(); var spots = new List<V2>();
    V2[] at = { new V2(700f, 455f), new V2(790f, 410f), new V2(805f, 500f), new V2(900f, 440f), new V2(860f, 600f) };
    foreach (var p in at) {
      var e = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Grunt, p, 0f, 0, false });
      e.SpawnT = 0f; e.Spawning = false; e.Hp = e.MaxHp = 5000f; e.Pos = e.Disp = p; e.FireT = 99f; e.Speed = 0f; mobs.Add(e); spots.Add(p);
    }
    bool fire = false, alt = false;
    int altFrames = 0;
    Action frame = () => {
      for (int i = 0; i < mobs.Count; i++) { mobs[i].FireT = 99f; if (mobs[i].KnockLeft.Length < 0.01f && phase != "focus") mobs[i].Pos = spots[i]; mobs[i].Disp = mobs[i].Pos; }
      me.Pos = home; me.InvulnT = 1f;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = home + new V2(300f, 0f), Fire = fire, WeaponAction = alt && altFrames++ < 3 }); gm.Update(1f / 60f);
      me.Disp = me.Pos;
      Time.unscaledTime += 1f / 60f;
      juice.Invoke(app, null);
    };
    Time.unscaledDeltaTime = 1f / 60f;
    for (int f = 0; f < 20; f++) frame();  // the juice sees everyone first
    int end = (int)Math.Round(after * 60f);
    if (phase == "bar" || phase == "focus" || phase == "miss") {
      fire = true;
      while (me.DrawT < gm.BowFull(me)) frame();
      if (phase == "bar") { for (int f = 0; f < end; f++) frame(); }
      else {
        for (int f = 0; f < 200; f++) {
          float m = Game.BowMarker(me.DrawT - gm.BowFull(me)), mid = gm.BowZone + Game.BowFocusZone / 2f;
          bool go = me.DrawT > gm.BowFull(me) + 0.25f && (phase == "focus" ? Math.Abs(m - mid) < 0.04f : Math.Abs(m - mid) > 0.35f);
          if (go) break;
          frame();
        }
        fire = false;
        for (int f = 0; f <= end; f++) frame();
      }
    }
    else if (phase == "draw") { fire = true; for (int f = 0; f < end; f++) frame(); }
    else if (phase == "swing") { fire = true; frame(); frame(); fire = false; for (int f = 0; f <= end; f++) frame(); }
    else if (phase == "scatter") { alt = true; for (int f = 0; f <= end; f++) frame(); }
    Time.unscaledDeltaTime = 0f;
    foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
    foreach (var q in gm.Players.Values) q.Disp = q.Pos;
  }
}
