using System; using System.IO; using System.Reflection; using System.Collections.Generic; using Arena; using UnityEngine;
// Renders fixed, seeded scenes with ArenaApp.DrawWorld and writes every GL triangle to a file (see check.sh).
class Harness {
  static void Main(string[] args) {
    UnityEngine.Screen.width = 1280; UnityEngine.Screen.height = 720;
    var f2 = BindingFlags.NonPublic | BindingFlags.Instance;
    var all = new List<float>();
    var scenes = new[] { new object[] { 1, Role.Tank, Kit.Mage, 0 }, new object[] { 6, Role.Dps, Kit.Knight, 1 }, new object[] { 14, Role.Healer, Kit.Summoner, 2 },
                         new object[] { 20, Role.Tank, Kit.Knight, 1 }, new object[] { 33, Role.Dps, Kit.Soldier, 0 }, new object[] { 50, Role.Healer, Kit.Soldier, 2 },
                         new object[] { 10, Role.Dps, Kit.Summoner, 0 }, new object[] { 25, Role.Healer, Kit.Mage, 1 }, new object[] { 40, Role.Tank, Kit.Soldier, 2 },
                         new object[] { 15, Role.Tank, Kit.Ninja, 2 }, new object[] { 30, Role.Healer, Kit.Ninja, 1 } };
    foreach (var sc in scenes) {
      var s = new Session { StartWave = (int)sc[0] };
      s.HostOffline((Role)sc[1], (Kit)sc[2], (int)sc[3], (int)sc[0] % 3);
      var g = s.Game;
      typeof(Game).GetField("rng", f2).SetValue(g, new System.Random((int)sc[0]));
      g.Bot = true; g.HostStart(); g.DevSetGod(1, true);
      for (int i = 0; i < 60 * 25; i++) { g.FixedStep(1f / 60f, new PlayerInput()); g.Update(1f / 60f); g.Sounds.Clear(); }
      var spawn = typeof(Game).GetMethod("SpawnEnemyAt", f2);
      int k = 0;
      foreach (EnemyType t in Enum.GetValues(typeof(EnemyType))) { spawn.Invoke(g, new object[] { t, new V2(120f + (k % 9) * 170f, 120f + (k / 9) * 220f), 0f, 0, k % 4 == 0 }); k++; }
      // Every minion kind next to the player, and each of the class's abilities and its ultimate cast in turn.
      var me = g.Me;
      var spawnMinion = typeof(Game).GetMethod("SpawnMinion", f2);
      int mk = 0;
      foreach (MinionKind kind in Enum.GetValues(typeof(MinionKind))) { if (kind.ToString() == "None") continue; spawnMinion.Invoke(g, new object[] { kind, me, me.Pos + V2.FromAngle(mk++ * 0.6f) * 90f, 5f, false }); }
      var hostAbility = typeof(Game).GetMethod("HostAbility", f2);
      for (int c = 0; c < 3; c++) {
        g.ShowcaseSetAbility(Upgrades.RoleAbility(me.Role, me.Kit, c));
        hostAbility.Invoke(g, new object[] { 1, 0, me.Pos + new V2(160f, -120f), true });
        for (int i = 0; i < 8; i++) { g.FixedStep(1f / 60f, new PlayerInput()); g.Update(1f / 60f); }
      }
      hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, me.Pos + new V2(160f, -120f), true });
      for (int i = 0; i < 30; i++) { g.FixedStep(1f / 60f, new PlayerInput()); g.Update(1f / 60f); }
      var app = new ArenaApp();
      Action<string, object> set = (n, v) => typeof(ArenaApp).GetField(n, f2).SetValue(app, v);
      set("session", s); set("game", g); set("scale", 1f); set("drawScale", 1280f / Game.Width);
      GL.Tris.Clear();
      typeof(ArenaApp).GetMethod("DrawWorld", f2).Invoke(app, null);
      Console.WriteLine("wave {0}: {1} enemies, {2} bullets, {3} minions, {4} zones, {5} effects, {6} triangles", sc[0], g.Enemies.Count, g.Bullets.Count, g.Minions.Count, g.Zones.Count, g.Effects.Count, GL.Tris.Count / 18);
      all.AddRange(GL.Tris);
    }
    using (var w = new BinaryWriter(File.Create(args[0]))) foreach (var f in all) w.Write(f);
  }
}
