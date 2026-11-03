using System; using System.Linq; using System.Reflection; using System.Collections.Generic; using Arena; using UnityEngine;
// Drives the visual effects (Game/Juice.cs) through real frames without Unity: hits, kills, a boss's death, a hit-pause,
// a level up and being hurt, and checks each one starts what it should. Run by Tools/RenderCheck/juice.sh.
class JuiceCheck {
  static int fails;
  static void Check(bool ok, string what) { Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
  static void Main() {
    UnityEngine.Screen.width = 1280; UnityEngine.Screen.height = 720;
    UnityEngine.Time.unscaledDeltaTime = 1f / 60f;
    var f2 = BindingFlags.NonPublic | BindingFlags.Instance;
    var s = new Session { StartWave = 4 };
    s.HostOffline(Role.Tank, Kit.Soldier, 0, 0);
    var g = s.Game;
    typeof(Game).GetField("rng", f2).SetValue(g, new System.Random(4));
    g.Bot = true; g.HostStart(); g.DevSetGod(1, true);
    for (int i = 0; i < 60 * 6; i++) { g.FixedStep(1f / 60f, new PlayerInput()); g.Update(1f / 60f); g.Sounds.Clear(); }
    var app = new ArenaApp();
    Action<string, object> set = (n, v) => typeof(ArenaApp).GetField(n, f2).SetValue(app, v);
    Func<string, object> get = n => typeof(ArenaApp).GetField(n, f2).GetValue(app);
    set("session", s); set("game", g); set("scale", 1f); set("drawScale", 1280f / Game.Width);
    var draw = typeof(ArenaApp).GetMethod("DrawWorld", f2);
    Func<int> frame = () => { GL.Tris.Clear(); draw.Invoke(app, null); return GL.Tris.Count / 18; };
    // Particles of one kind (and, if given, one colour) alive now.
    Func<byte, float, int> countParts = (kind, green) => {
      var arr = (Array)get("parts"); int n = (int)get("partN"), cnt = 0;
      for (int i = 0; i < n; i++) {
        var p = arr.GetValue(i); var t = p.GetType();
        if ((byte)t.GetField("Kind").GetValue(p) != kind) continue;
        var col = (Rgba)t.GetField("Col").GetValue(p);
        if (green < 0f || Math.Abs(col.G - green) < 1e-3f && col.R < 0.4f) cnt++;
      }
      return cnt; };
    Action tick = () => { g.FixedStep(1f / 60f, new PlayerInput()); g.Update(1f / 60f); g.Sounds.Clear(); };

    set("fxOn", false);
    int plain = frame();
    set("fxOn", true);
    frame(); tick();
    int juicy = frame();
    Console.WriteLine("  triangles: {0} plain, {1} with effects", plain, juicy);
    Check(juicy > plain, "with effects on, the arena draws more (glow, tint)");

    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", f2);
    var a = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, new V2(500f, 300f), 0f, 0, false });
    a.SpawnT = 0f;
    frame(); tick();
    a.Hp -= 20f;
    frame();
    Check((bool)typeof(ArenaApp).GetMethod("JuiceFlash", f2).Invoke(app, new object[] { a }) && (float)typeof(ArenaApp).GetMethod("JuiceScale", f2).Invoke(app, new object[] { a }) >= 1f,
      "a hit enemy flashes white (and bulges)");
    for (int i = 0; i < 12; i++) { tick(); frame(); }
    var nums = (System.Collections.IList)get("numbers");
    Check(nums.Count >= 1, "damage to an enemy rises off it as a number");

    a.Hp = 0f; g.Enemies.Remove(a.Id);
    frame();
    int parts = (int)get("partN");
    var scorches = (System.Collections.IList)get("scorches");
    Console.WriteLine("  a grunt died: {0} particles, {1} scorch marks", parts, scorches.Count);
    Check(parts >= 3 + 10 && scorches.Count >= 1, "a killed enemy shatters into shards, sparks and a scorch mark");

    // One shot from full health: the killing blow still shows (the game plays a death where it was).
    var c = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Sniper, new V2(300f, 600f), 0f, 0, false });
    c.SpawnT = 0f;
    tick(); frame();
    nums.Clear();
    float full = c.Hp;
    g.Enemies.Remove(c.Id);
    g.Effects.Add(new Effect { Kind = FxKind.Boom, Pos = c.Pos, Color = c.Def.Color, Radius = c.Radius * 2.2f, Dur = 0.4f });
    frame();
    var last = nums.Count > 0 ? nums[nums.Count - 1] : null;
    int shown = last == null ? -1 : (int)last.GetType().GetField("Value").GetValue(last);
    Check(shown == (int)Math.Round(full), "a one-shot kill shows its killing blow as a number (" + shown + ")");

    // A big enemy's death holds the picture: the next frame replays the last one exactly.
    var j = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Juggernaut, new V2(900f, 500f), 0f, 0, false });
    j.SpawnT = 0f;
    tick(); frame(); tick(); frame();
    set("holdGap", 0f);
    j.Hp = 0f; g.Enemies.Remove(j.Id);
    frame();
    var before = new List<float>(GL.Tris);
    tick();
    frame();
    float hold = (float)get("holdT");
    Check(before.Count > 0 && GL.Tris.SequenceEqual(before), "a heavy enemy's death holds the picture: the next frame is the same one again");
    for (int i = 0; i < 10; i++) { tick(); frame(); }
    Check((float)get("holdT") <= 0f, "and the picture moves on after a moment");

    // Shake: a blast near you offsets the arena.
    float t0 = (float)get("trauma");
    g.Effects.Add(new Effect { Kind = FxKind.Boom, Pos = g.Me.Pos + new V2(60f, 0f), Color = new Rgba(1f, 0.6f, 0.3f), Radius = 140f, Dur = 0.4f });
    frame();
    Check((float)get("trauma") > t0 && ((System.Collections.IList)get("ripples")).Count >= 1, "a big blast near you shakes the screen and sends a ripple through the grid");
    set("fxShake", 0); set("trauma", 0f);
    g.Effects.Add(new Effect { Kind = FxKind.Boom, Pos = g.Me.Pos, Color = new Rgba(1f, 0.6f, 0.3f), Radius = 140f, Dur = 0.4f });
    frame();
    Check((float)get("trauma") == 0f, "with Screen shake off, nothing shakes");
    set("fxShake", 2);

    // Hurt: the red corners; a level up: the gold burst.
    g.Me.Hp -= 30f; frame();
    Check((float)get("hurtTint") > 0.9f, "taking damage tints the corners red");
    int p0 = (int)get("partN");
    g.Me.Level++; frame();
    Check((int)get("partN") >= p0 + 30, "a level up bursts with gold sparks");

    // A boss: the entrance card, then its death in waves.
    var boss = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Boss, new V2(800f, 300f), 0f, 0, false });
    boss.SpawnT = 0f;
    frame();
    Check((float)get("introT") > 0f && (string)get("introName") == "THE WARDEN", "a boss's arrival brings its entrance card");
    set("holdGap", 0f);
    boss.Hp = 0f; g.Enemies.Remove(boss.Id); frame();
    Check(((System.Collections.IList)get("bursts")).Count == 4 && (float)get("trauma") > 0.5f, "a boss's death shakes hard and bursts in waves");

    // The damage ghost: a bar that drops keeps a pale chunk for a moment, then drains to it; healing pulls it up.
    var ghost = typeof(ArenaApp).GetMethod("Ghost", f2);
    Func<float, float, float> G = (now, frac) => { UnityEngine.Time.unscaledTime = now; return (float)ghost.Invoke(app, new object[] { 77L, frac }); };
    G(10f, 1f);
    float held = G(10.05f, 0.6f), still = G(10.3f, 0.6f), draining = G(10.7f, 0.6f), done = G(12f, 0.6f), healed = G(12.1f, 0.9f);
    Console.WriteLine("  ghost: {0:0.00} held, {1:0.00} still, {2:0.00} draining, {3:0.00} done, {4:0.00} healed", held, still, draining, done, healed);
    Check(held == 1f && still == 1f && draining < 1f && draining > 0.6f && Math.Abs(done - 0.6f) < 1e-4f && Math.Abs(healed - 0.9f) < 1e-4f,
      "a health bar's damage ghost holds where health was, then drains to it; healing pulls it up");

    // A health orb picked up: a green pulse and sparks on the player (after the boss's hold has passed).
    for (int i = 0; i < 30; i++) frame();
    var me = g.Me;
    var orb = new Orb { Id = 9001, Pos = me.Pos + new V2(15f, 0f), Disp = me.Pos + new V2(15f, 0f), Life = 9f };
    g.Orbs[orb.Id] = orb;
    frame();
    int beforeOrb = countParts(0, 1f);
    g.Orbs.Remove(orb.Id);
    frame();
    int greenSparks = countParts(0, 1f);
    Console.WriteLine("  orb picked up: {0} green sparks (before: {1})", greenSparks, beforeOrb);
    Check(greenSparks >= beforeOrb + 10, "picking up a health orb pulses green on you");

    // Dust: setting off kicks up a puff at your feet.
    int beforeDust = countParts(4, -1f);
    for (int i = 0; i < 4; i++) { me.Disp = me.Disp + new V2(6f, 0f); frame(); }
    Check(countParts(4, -1f) > beforeDust, "setting off kicks up a puff of dust");

    // Weather: a path brings its own (Frozen Lake: snow).
    g.CurrentPath = PathId.FrozenLake; g.PathFrom = 1; g.PathTo = 99;
    for (int i = 0; i < 90; i++) frame();
    Check((PathId)get("weatherPath") == PathId.FrozenLake && (float)get("weatherFade") > 0.5f, "a path brings its weather (Frozen Lake: snow), fading in");
    g.CurrentPath = PathId.None;

    // Bullet endings: why a shot went, and what it does as it goes.
    for (int i = 0; i < 30; i++) frame();
    foreach (var en in g.Enemies.Values.ToList()) g.Enemies.Remove(en.Id);
    var why = typeof(ArenaApp).GetMethod("WhyGone");
    Func<V2, V2, float, int, string> W = (pos, vel, life, owner) => why.Invoke(app, new object[] { pos, vel, life, 4f, owner }).ToString();
    var shielder = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Shielder, new V2(1200f, 300f), 0f, 0, false });
    shielder.SpawnT = 0f; shielder.Facing = 0f; shielder.Disp = shielder.Pos;   // its shield faces +x
    var grunt = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, new V2(1200f, 700f), 0f, 0, false });
    grunt.SpawnT = 0f; grunt.Disp = grunt.Pos;
    me.Pos = me.Disp = new V2(400f, 450f);
    string ranOut = W(new V2(800f, 200f), new V2(500f, 0f), 0.01f, 1);
    string hit = W(grunt.Pos + new V2(-12f, 0f), new V2(600f, 0f), 0.4f, 1);
    string shieldFront = W(shielder.Pos + new V2(20f, 0f), new V2(-600f, 0f), 0.4f, 1);
    string shieldBack = W(shielder.Pos + new V2(-20f, 0f), new V2(600f, 0f), 0.4f, 1);
    string hitMe = W(me.Pos + new V2(6f, 0f), new V2(-200f, 0f), 3f, 0);
    me.Parrying = true;
    string parried = W(me.Pos + new V2(30f, 0f), new V2(-200f, 0f), 3f, 0);
    me.Parrying = false; me.BarrierT = 3f;
    string barrier = W(me.Pos + new V2(me.BarrierRadius - 4f, 0f), new V2(-200f, 0f), 3f, 0);
    me.BarrierT = 0f;
    string offArena = W(new V2(-30f, 450f), new V2(-500f, 0f), 0.5f, 1);
    Console.WriteLine("  endings: ran out {0}, hit {1}, shield front {2}, shield back {3}, hit you {4}, parried {5}, barrier {6}, off the arena {7}", ranOut, hit, shieldFront, shieldBack, hitMe, parried, barrier, offArena);
    Check(ranOut == "RanOut" && hit == "HitEnemy" && hitMe == "HitPlayer" && offArena == "None", "a shot that's gone ran out, hit an enemy or hit you (or just left the arena)");
    Check(shieldFront == "Blocked" && shieldBack == "HitEnemy" && parried == "Blocked" && barrier == "Blocked", "a shot stopped by a shield's front, a parry or a Barrier was blocked (a Shielder hit from behind wasn't)");
    // End to end: an enemy shot parried tumbles off as a ricochet.
    var sb = typeof(Game).GetMethod("SpawnBullet", f2);
    me.Parrying = true;
    var shot = (Bullet)sb.Invoke(g, new object[] { Defs.SmallShot, me.Pos + new V2(30f, 0f), new V2(-200f, 0f), 0f, 3f, 10f, 0, false, 0f });
    frame();
    g.Bullets.Remove(shot.Id);
    frame();
    me.Parrying = false;
    Check(countParts(7, -1f) >= 1, "a parried shot ricochets off, tumbling");

    // New shot looks: each weapon's own shot draws its new way with effects on; an enemy's shot of the same kind doesn't.
    var dws = typeof(ArenaApp).GetMethod("DrawWeaponShot", f2);
    var lookWeapons = new[] { WeaponId.TwinRifle, WeaponId.BurstSmg, WeaponId.Railgun, WeaponId.Pistol, WeaponId.Scattergun, WeaponId.PrismBeam, WeaponId.ArcaneMissiles };
    var lookKinds = new[] { Defs.DpsShot, Defs.DpsShot, Defs.RailShot, Defs.HealerShot, Defs.TankShot, Defs.ArcaneShot, Defs.ArcaneShot };
    bool allLooks = true, offPlain = true;
    var keepWeapon = me.Weapon;
    for (int li = 0; li < lookWeapons.Length; li++) {
      var kind = lookKinds[li];
      me.Weapon = Defs.WeaponOf(lookWeapons[li]);
      var bl = new Bullet { Id = 7000, Kind = kind, Owner = me.Id, Pos = new V2(600f, 300f), Vel = new V2(900f, 0f), Radius = 4f, Life = 1f };
      allLooks &= (bool)dws.Invoke(app, new object[] { bl, 1f });
      set("fxOn", false); offPlain &= !(bool)dws.Invoke(app, new object[] { bl, 1f }); set("fxOn", true);
    }
    me.Weapon = Defs.WeaponOf(WeaponId.Railgun);
    var enemyShot = new Bullet { Id = 7001, Kind = Defs.RailShot, Owner = 0, Pos = new V2(600f, 300f), Vel = new V2(900f, 0f), Radius = 4f, Life = 1f };
    bool enemyPlain = !(bool)dws.Invoke(app, new object[] { enemyShot, 1f });
    me.Weapon = Defs.WeaponOf(WeaponId.Flamethrower);
    var other = new Bullet { Id = 7002, Kind = Defs.DpsShot, Owner = me.Id, Pos = new V2(600f, 300f), Vel = new V2(900f, 0f), Radius = 4f, Life = 1f };
    bool otherPlain = !(bool)dws.Invoke(app, new object[] { other, 1f });
    me.Weapon = keepWeapon;
    Check(allLooks && offPlain && enemyPlain && otherPlain, "seven weapons' shots have their new look with effects on (and their old one off); an enemy's shot, or another source's, keeps the old one");

    // The run ending (RunEnding.cs): slow motion from the blow, focused on the last player down, then the results in.
    {
      var tickEnd = typeof(ArenaApp).GetMethod("TickEnding", f2);
      Func<string, float> prop = n => (float)typeof(ArenaApp).GetProperty(n, f2).GetValue(app);
      Action endStep = () => tickEnd.Invoke(app, new object[] { 1f / 60f });
      endStep();
      bool fullSpeed = prop("EndSpeed") == 1f && prop("EndBg") == 1f;
      foreach (var pl in g.Players.Values) { g.DevSetGod(pl.Id, false); pl.Downed = true; pl.ReviveToken = false; pl.SecondWind = false; }
      g.Minions.Clear();
      endStep(); tick(); endStep();
      bool over = g.State == MatchState.GameOver;
      for (int i = 0; i < 30; i++) endStep();
      float slow = prop("EndSpeed"), bgEarly = prop("EndBg");
      int zoomed = frame();
      for (int i = 0; i < 60 * 4; i++) endStep();
      float late = prop("EndSpeed"), bg = prop("EndBg"), buttons = prop("EndButtonsIn");
      bool stamped = (bool)get("endStamped");
      g.State = MatchState.Intermission;  // Play again
      endStep();
      Check(fullSpeed && over && slow < 0.1f && bgEarly == 0f && zoomed > 0 && late > 0f && late < 0.05f && bg == 1f && buttons == 1f && stamped && prop("EndSpeed") == 1f,
        "a run ending slows to a crawl, stamps the title and brings the results in, and a new run plays at full speed (" + slow + ", " + late + ")");
    }

    // The ability bar's tiles (HudTiles.cs): a used ability presses in, pressing it while it recharges flashes red,
    // pressing a ready one doesn't, and its cooldown finishing pops it with a white rim.
    {
      set("fxOn", true); set("fxReduced", false);
      s.Screen = AppScreen.Game;
      g.Me.Downed = false; g.State = MatchState.Fighting;
      var sl = g.Me.Slots[0];
      if (sl.Id == AbilityId.None) sl.Id = AbilityId.Barrier;
      Action tt = () => typeof(ArenaApp).GetMethod("TickTiles", f2).Invoke(app, new object[] { 1f / 60f });
      Func<string, float> lk = n => { var l = typeof(ArenaApp).GetMethod("TileLookFor", f2).Invoke(app, new object[] { 0, false }); return Convert.ToSingle(l.GetType().GetField(n).GetValue(l)); };
      Action<int> press = i => typeof(ArenaApp).GetMethod("TileKeyPressed", f2).Invoke(app, new object[] { i });
      sl.Charges = sl.MaxCharges; tt(); for (int i = 0; i < 40; i++) tt();
      bool tStill = lk("Scale") == 1f && lk("Red") == 0f && lk("White") == 0f;
      press(0); tt();
      bool readyPress = lk("Red") == 0f;
      sl.Charges = 0; sl.Timer = 3f; tt(); tt();
      bool pressedIn = lk("Scale") < 1f;
      for (int i = 0; i < 30; i++) tt();
      press(0); tt();
      bool denied = lk("Red") > 0.5f;
      for (int i = 0; i < 30; i++) tt();
      sl.Charges = 1; tt(); tt();
      bool popped = lk("Scale") > 1f && lk("White") > 0.5f && lk("ReadyIn") < 1f;
      sl.Charges = 0; tt(); press(0); tt();
      set("fxOn", false);
      bool tOff = lk("Scale") == 1f && lk("Red") == 0f;
      Check(tStill && readyPress && pressedIn && denied && popped && tOff,
        "ability tiles press in when used, flash red when pressed too early, pop when ready, and stay still with effects off");
    }

    // Melee style E (MeleeLooks.cs): real swings (fired by the game's own code, played out over real ticks) draw the
    // weapon + energy look on every frame, and a Halberd sweep is one effect, not one per step.
    {
      set("fxOn", true);
      var seenMap = (System.Collections.IDictionary)get("meleeSeen");
      var fire = typeof(Game).GetMethod("FireKnightWeapon", f2);
      var pl = g.Players.Values.First();
      var keep = pl.Weapon;
      bool botWas = g.Bot;
      g.Bot = false;  // (only the one swing fired below)
      Func<WeaponId, int[]> swing = id => {
        pl.Weapon = Defs.Weapons.First(x => x.Id == id);
        g.Effects.Clear();
        fire.Invoke(g, new object[] { pl, 0f, 1f, 0 });
        int frames = 0, missed = 0, slashes = 0;
        var all = new HashSet<Effect>();
        for (int i = 0; i < 30; i++) {
          tick(); frame(); frames++;
          foreach (var fx in g.Effects) if (fx.Kind == FxKind.Slash) { all.Add(fx); if (!seenMap.Contains(fx)) missed++; }
        }
        slashes = all.Count;
        return new[] { frames, missed, slashes };
      };
      var halberd = swing(WeaponId.Halberd);
      var sword = swing(WeaponId.Greatsword);
      // The Warhammer: its slam (a Jab) draws the new look, and its blast (a Boom beside it) isn't drawn twice.
      pl.Weapon = Defs.Weapons.First(x => x.Id == WeaponId.Warhammer);
      g.Effects.Clear();
      fire.Invoke(g, new object[] { pl, 0f, 1f, 0 });
      var boomOld = typeof(ArenaApp).GetMethod("HammerBoomDrawnByMelee", f2);
      int hammerJabs = 0, hammerJabsSeen = 0, booms = 0, boomsLeftOut = 0;
      var hs = new HashSet<Effect>();
      for (int i = 0; i < 20; i++) {
        tick(); frame();
        foreach (var fx in g.Effects) {
          if (!hs.Add(fx)) continue;
          if (fx.Kind == FxKind.Jab) { hammerJabs++; if (seenMap.Contains(fx)) hammerJabsSeen++; }
          if (fx.Kind == FxKind.Boom) { booms++; if ((bool)boomOld.Invoke(app, new object[] { fx })) boomsLeftOut++; }
        }
      }
      Check(hammerJabs >= 1 && hammerJabsSeen == hammerJabs && booms >= 1 && boomsLeftOut == booms,
        "a Warhammer slam draws only the new look (its blast's old ring is left out: " + boomsLeftOut + "/" + booms + ")");
      pl.Weapon = keep;
      g.Bot = botWas;
      Console.WriteLine("  melee: Halberd {0} effect(s), {1} frames missed; Greatsword {2} effect(s), {3} missed", halberd[2], halberd[1], sword[2], sword[1]);
      Check(halberd[1] == 0 && sword[1] == 0 && halberd[2] >= 1 && sword[2] >= 1,
        "real melee swings (the Greatsword, the Halberd) draw the weapon + energy look on every frame");
      Check(halberd[2] == 1, "a Halberd sweep is one effect (its six steps share it)");
    }

    // Off again: exactly the plain drawing.
    set("fxOn", false);
    tick(); set("fxOn", false);
    int off1 = frame();
    Check(off1 > 0 && (float)typeof(ArenaApp).GetMethod("JuiceScale", f2).Invoke(app, new object[] { g.Enemies.Values.First() }) == 1f, "turned off, nothing extra is drawn");
    Console.WriteLine(fails == 0 ? "ALL PASSED" : fails + " FAILED");
    Environment.Exit(fails == 0 ? 0 : 1);
  }
}
