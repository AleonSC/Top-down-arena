using System;
using System.Linq;
using System.Reflection;
using Arena;

// GUSTGATE=<seconds into the scene>: the Healer Elementalist's Gust Gate (Core/GustGate.cs, Game/GustGateLook.cs) played through the
// real game: you at (560, 600) cast the ring at (690, 540) at 0.2 s; an ally runs right through it from 0.5 s (boosted from the edge);
// a grunt standing inside is pushed out. Use with mode hud, 2 players; ZOOM=2 crops round the ring.
static partial class Shot {
  static void GustGateScene() {
    string ts = Environment.GetEnvironmentVariable("GUSTGATE");
    if (ts == null) return;
    var inv = System.Globalization.CultureInfo.InvariantCulture;
    var gm = host.Game; gm.Bot = false;
    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
    var cast = typeof(Game).GetMethod("HostAbility", flags);
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Props.Clear(); gm.Layout = LayoutId.Open;
    if (gm.Profile != null) foreach (EnemyType et in Enum.GetValues(typeof(EnemyType))) gm.Profile.TagSeen[et.ToString()] = 99;
    for (int i = 0; i < 3; i++) Step(host);
    gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    me.Kit = Kit.Elementalist; me.Role = Role.Healer; me.Def = Defs.Get(Role.Healer); me.Weapon = Defs.Weapon(Role.Healer, Kit.Elementalist, 0);
    me.MaxHp = me.Def.Hp; me.Hp = me.MaxHp;
    V2 home = new V2(560f, 600f), ring = new V2(690f, 540f);
    var ally = gm.Players.Values.FirstOrDefault(q => q != me);
    me.Slots[0].Id = AbilityId.GustGate; me.Slots[0].Charges = 1; me.Slots[0].Timer = 0f;
    int frames = (int)Math.Round(float.Parse(ts, inv) * 60f);
    var mob = (Enemy)spawn.Invoke(gm, new object[] { EnemyType.Grunt, ring + new V2(18f, -22f), 0f, 0, false });
    mob.SpawnT = 0f; mob.Hp = mob.MaxHp = 5000f;
    V2 allyPos = ring + new V2(-200f, 0f);
    for (int f = 0; f < frames; f++) {
      float t = f / 60f;
      if (f == 12) cast.Invoke(gm, new object[] { me.Id, 0, ring, false });
      if (ally != null) {
        if (t >= 0.5f) allyPos += new V2(ally.Speed / 60f, 0f);
        ally.Pos = ally.Disp = allyPos; ally.InvulnT = 1f; ally.Aim = 0f; ally.Firing = false; ally.FireCd = 1f;
      }
      mob.FireT = 99f;
      me.Pos = home; me.InvulnT = 1f; me.Aim = (ring - home).Angle;
      host.Poll(); gm.FixedStep(1f / 60f, new PlayerInput { AimAt = ring }); gm.Update(1f / 60f);
    }
    foreach (var e in gm.Enemies.Values) e.Disp = e.Pos;
    foreach (var q in gm.Players.Values) q.Disp = q.Pos;
    Console.WriteLine("gust gate: ally boost {0:0.00} s, speed {1:0}; grunt {2:0} px from the middle",
      ally?.GustT ?? 0f, ally?.Speed ?? 0f, mob.Pos.DistanceTo(ring));
  }
}
