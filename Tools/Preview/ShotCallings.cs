using System;
using System.Reflection;
using Arena;

// CALLINGS=<s>: the Summoner's three Calling minions (Core/Callings.cs, Game/CallingsLook.cs) played through the real
// game, one per player: you at (600, 450) with the Spider (Spider Queen: spiderlings), the ally to the left the Fairy
// (hurt, so it heals), the one to the right the Demon (Infernal Pact: berserk over its first 4 s), grunts round about.
// Mode hud, 3 players; ZOOM=1.6 CAMAT=600,440 frames them.
static partial class Shot {
  static void CallingsScene() {
    string ts = Environment.GetEnvironmentVariable("CALLINGS");
    if (ts == null) return;
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    if (gm.Profile != null) foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) gm.Profile.TagSeen[et.ToString()] = 99;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    V2 home = new V2(600f, 450f);
    var allies = new System.Collections.Generic.List<Player>();
    foreach (var q in gm.Players.Values) if (q != me) allies.Add(q);
    V2[] spots = { home + new V2(-190f, 20f), home + new V2(190f, 10f) };
    Func<string, int> U = name => Array.FindIndex(Upgrades.All, u => u.Name == name);
    string[][] cards = { new[] { "Call the Spider", "Silk Snare", "Thick Carapace", "Spider Queen" }, new[] { "Call the Fairy" }, new[] { "Call the Demon", "Soul Feast", "Hellfire", "Infernal Pact" } };
    var who = new System.Collections.Generic.List<Player> { me };
    who.AddRange(allies);
    for (int i = 0; i < who.Count && i < 3; i++) foreach (var c in cards[i]) gm.GrantUpgrade(who[i].Id, U(c));
    for (int i = 0; i < allies.Count; i++) { allies[i].Pos = allies[i].Disp = i < 2 ? spots[i] : new V2(150f, 820f); allies[i].Hp = allies[i].MaxHp * (i == 0 ? 0.4f : 1f); }
    var mobs = new System.Collections.Generic.List<Enemy>(); var mspots = new System.Collections.Generic.List<V2>();
    V2[] at = { home + new V2(40f, -150f), home + new V2(-120f, -170f), home + new V2(300f, -170f), home + new V2(110f, 190f), home + new V2(-280f, 170f) };
    foreach (var p0 in at) {
      var e = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Grunt, p0, 0f, 0, false });
      e.SpawnT = 0f; e.Hp = e.MaxHp = 50000f; e.Pos = e.Disp = p0; e.FireT = 99f; mobs.Add(e); mspots.Add(p0);
    }
    float secs = float.Parse(ts, System.Globalization.CultureInfo.InvariantCulture);
    int frames = (int)Math.Round(secs * 60f);
    for (int f = 0; f < frames; f++) {
      for (int k = 0; k < mobs.Count; k++) { mobs[k].FireT = 99f; mobs[k].Pos = mobs[k].Disp = mspots[k]; }
      me.Pos = home; me.InvulnT = 1f; me.Aim = -1.2f;
      for (int i = 0; i < allies.Count; i++) { allies[i].InvulnT = 1f; allies[i].Pos = i < 2 ? spots[i] : allies[i].Pos; if (i == 0) allies[i].Hp = Math.Min(allies[i].Hp, allies[i].MaxHp * 0.5f); }
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = home + new V2(40f, -150f) }); gm.Update(1f / 60f);
    }
    foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    foreach (var m in gm.Minions.Values) m.Disp = m.Pos;
  }
}
