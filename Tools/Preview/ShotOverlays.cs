using System;
using System.Reflection;
using Arena;

// OVERLAYS=<role 0-2|all>: every class of that role (Soldier .. Arcanist, left to right) standing in a row at 60% health,
// facing up-right, for the bars and markers over the body (WorldPlayers.cs: the health bar and what stacks on it).
// OVERLAYS=all: three rows, Tank, Healer, DPS. OVERBARS=1 also gives each one a wound and an Armor Drop vest.
// e.g. OVERLAYS=all ZOOM=2 CAMAT=800,450 NOHUD=1 Tools/Preview/shot.sh x hud 1 0 1
static partial class Shot {
  static void OverlaysScene() {
    string ov = Environment.GetEnvironmentVariable("OVERLAYS");
    if (ov == null) return;
    var gm = host.Game; gm.Bot = false;
    gm.Enemies.Clear(); gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear(); gm.Props.Clear();
    var clone = typeof(object).GetMethod("MemberwiseClone", BindingFlags.NonPublic | BindingFlags.Instance);
    var template = me;
    foreach (var sl in me.Slots) sl.Id = AbilityId.None;  // (no abilities: only what every player always has)
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = new V2(-500f, -500f);
    int[] roles = ov == "all" ? new[] { 0, 1, 2 } : new[] { int.Parse(ov) };
    bool bars = Environment.GetEnvironmentVariable("OVERBARS") != null;
    int kits = Enum.GetValues(typeof(Kit)).Length, id = 100;
    for (int ri = 0; ri < roles.Length; ri++)
      for (int k = 0; k < kits; k++) {
        Role role = (Role)roles[ri];
        var p = ri == 0 && k == 0 ? me : (Player)clone.Invoke(template, null);
        if (p != me) { p.Id = id++; gm.Players[p.Id] = p; }
        p.Role = role; p.Kit = (Kit)k; p.Def = Defs.Get(role); p.Weapon = Defs.Weapon(role, (Kit)k, 0);
        p.MaxHp = p.Def.Hp; p.Hp = p.MaxHp * 0.6f; p.Flash = 0f; p.InvulnT = 0f; p.Downed = false; p.AegisT = 0f; p.UltT = 0f; p.IronCap = Game.OwnIronCap(p); p.IronLost = 0f;
        p.Name = role + " " + (Kit)k;
        if (bars) { p.Wound = p.MaxHp * 0.15f; p.Armor = Game.ArmorCap * 0.6f; }
        p.Pos = p.Disp = new V2(800f + (k - (kits - 1) / 2f) * 90f, 450f + (ri - (roles.Length - 1) / 2f) * 110f);
        p.Aim = -MathX.Pi / 4f;
      }
  }
}
