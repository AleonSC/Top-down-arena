using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Arena;
using UnityEngine;

// MOMENT=multi|clear|level|lowhp|marks: the juice moments (Game/JuiceMoments.cs) played through the real game and Juice.cs.
//   multi   MOMENTN=<kills> grunts round you die one every 4 frames (KillMoments credits them to you); MULTISTYLE=1 the call off you
//   clear   the wave's last enemy dies and the fight ends (the last kill's ring and ripple)
//   level   you level up (MOMENTN=1: a legendary) among a ring of grunts (the level shock)
//   lowhp   you at MOMENTN% health, on the heartbeat's first beat (MOMENTBEAT=<s> that far into its cycle)
//   marks   the floor after a fight: scorch marks from 1 to 40 s old
// MOMENTT=<s> after the moment (default 0.15). Use with mode hud.
static partial class Shot {
  static void JuiceMomentScene() {
    string which = Environment.GetEnvironmentVariable("MOMENT");
    if (which == null) return;
    var ci = System.Globalization.CultureInfo.InvariantCulture;
    float after = Environment.GetEnvironmentVariable("MOMENTT") != null ? float.Parse(Environment.GetEnvironmentVariable("MOMENTT"), ci) : 0.15f;
    int n = Environment.GetEnvironmentVariable("MOMENTN") != null ? int.Parse(Environment.GetEnvironmentVariable("MOMENTN")) : 0;
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var juice = typeof(ArenaApp).GetMethod("UpdateJuice", flags);
    if (Environment.GetEnvironmentVariable("MULTISTYLE") != null) typeof(ArenaApp).GetField("multiStyle", flags).SetValue(app, int.Parse(Environment.GetEnvironmentVariable("MULTISTYLE")));
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    if (gm.Profile != null) foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) gm.Profile.TagSeen[et.ToString()] = 99;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    gm.State = MatchState.Fighting; gm.StateTimer = 999f;
    V2 home = new V2(Game.Width / 2f, Game.Height / 2f);
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = home + new V2(-420f, 260f);
    me.Pos = me.Disp = home; me.Aim = 0f;
    var rng = new System.Random(3);
    var mobs = new List<Enemy>(); var spots = new List<V2>();
    int ring = which == "multi" ? Math.Max(n, 2) + 4 : which == "level" ? 10 : which == "clear" ? 1 : 0;
    for (int i = 0; i < ring; i++) {
      float a = i / (float)ring * MathX.Tau + (float)rng.NextDouble() * 0.3f, d = which == "clear" ? 240f : (i < n ? 60f : 170f) + (float)rng.NextDouble() * 50f;
      if (which == "level") d = 120f + (float)rng.NextDouble() * 220f;
      var e = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Grunt, home + V2.FromAngle(a) * d, 0f, 0, false });
      e.SpawnT = 0f; e.Spawning = false; e.Hp = e.MaxHp = 5000f; e.Pos = e.Disp = home + V2.FromAngle(a) * d; e.FireT = 99f; mobs.Add(e); spots.Add(e.Pos);
    }
    Action frame = () => {
      for (int i = 0; i < mobs.Count; i++) { mobs[i].FireT = 99f; if (mobs[i].Hp > 0f) { mobs[i].Pos = mobs[i].Disp = spots[i]; mobs[i].Vel = new V2(0f, 0f); } }
      me.Pos = home; me.InvulnT = 1f;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = home + new V2(100f, 0f) }); gm.Update(1f / 60f);
      Time.unscaledTime += 1f / 60f;
      juice.Invoke(app, null);
    };
    Time.unscaledDeltaTime = 1f / 60f;
    for (int f = 0; f < 20; f++) frame();  // the juice sees everyone first
    if (which == "multi")
      for (int k = 0; k < n; k++) { mobs[k].Hp = 0f; for (int f = 0; f < 4; f++) frame(); }
    else if (which == "clear") { mobs[0].Hp = 0f; frame(); gm.State = MatchState.Intermission; gm.StateTimer = 20f; }
    else if (which == "level") me.Level++;
    else if (which == "lowhp") {
      me.Hp = me.MaxHp * Math.Max(1, n) / 100f;
      typeof(ArenaApp).GetField("myHp", flags).SetValue(app, me.Hp);
    }
    else if (which == "marks") {
      var list = (IList)typeof(ArenaApp).GetField("scorches", flags).GetValue(app);
      var decal = typeof(ArenaApp).GetNestedType("Decal", BindingFlags.NonPublic);
      for (int i = 0; i < 70; i++) {
        object dc = Activator.CreateInstance(decal);
        decal.GetField("Pos").SetValue(dc, new V2(80f + (float)rng.NextDouble() * (Game.Width - 160f), 80f + (float)rng.NextDouble() * (Game.Height - 160f)));
        decal.GetField("R").SetValue(dc, 30f + (float)rng.NextDouble() * (i % 9 == 0 ? 120f : 40f));
        decal.GetField("Age").SetValue(dc, 1f + 39f * i / 70f);
        list.Add(dc);
      }
    }
    for (int f = 0, end = (int)Math.Round(after * 60f); f < end; f++) frame();
    if (which == "lowhp") {  // stop on the first beat
      float low = 1f - Mathf.Clamp01(me.Hp / (me.MaxHp * 0.3f)), period = Mathf.Lerp(1.1f, 0.6f, low);
      Time.unscaledTime = (float)Math.Ceiling(Time.unscaledTime / period) * period + 0.06f * period;
      if (Environment.GetEnvironmentVariable("MOMENTBEAT") != null) Time.unscaledTime += float.Parse(Environment.GetEnvironmentVariable("MOMENTBEAT"), ci) - 0.06f * period;  // MOMENTBEAT=<s>: that far into the beat's cycle instead
    }
    Time.unscaledDeltaTime = 0f;
    foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
    foreach (var q in gm.Players.Values) q.Disp = q.Pos;
  }
}
