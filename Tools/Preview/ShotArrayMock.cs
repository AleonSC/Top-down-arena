using System;
using System.Reflection;
using Arena;
using UnityEngine;

// MOCKARRAY=A|B|C MOCKT=<s since the cast>: mockup of a reworked Runic Array (a rune portal pouring homing swords at the
// enemies under your cursor). Drawn with the game's own primitives; not wired into Core. To render it, add
// `if (p.ArrayT > 0f && DrawArrayMock(p, t)) { } else` before the Runic Array block in Game/ArcanistLook.cs (not committed).
//   A Rune Gate: a round gate behind you.  B Rift: an upright rune-edged rift over your shoulder.  C Crown: a ring round you,
//   swords waiting on its rim, then loosed.
static partial class Shot {
  static void ArrayMockScene() {
    string v = Environment.GetEnvironmentVariable("MOCKARRAY");
    if (v == null) return;
    var gm = host.Game; gm.Bot = false;
    var bf = BindingFlags.NonPublic | BindingFlags.Instance;
    var spawn = typeof(Game).GetMethod("SpawnEnemyAt", bf);
    foreach (var e in gm.Enemies.Values) e.Hp = 0f;
    gm.Enemies.Clear(); gm.Bullets.Clear(); gm.Effects.Clear(); gm.Zones.Clear(); gm.Minions.Clear(); gm.Orbs.Clear();
    if (gm.Profile != null) foreach (EnemyType et2 in Enum.GetValues(typeof(EnemyType))) gm.Profile.TagSeen[et2.ToString()] = 99;
    foreach (var bp in gm.Players.Values) { bp.AegisT = 0f; bp.BarrierT = 0f; }
    foreach (var q in gm.Players.Values) if (q != me) q.Pos = q.Disp = new V2(-400f, -400f);
    me.Role = Role.Dps; me.Def = Defs.Get(me.Role);
    me.Kit = Kit.Arcanist; me.Weapon = Defs.Weapon(me.Role, Kit.Arcanist, 0);
    me.MaxHp = me.Def.Hp * Defs.KitHpMult(Kit.Arcanist); me.Hp = me.MaxHp;
    me.Slots[0].Id = Upgrades.FirstAbility(me.Role, Kit.Arcanist, 0); me.Slots[Upgrades.UltSlot].Id = Upgrades.Ultimate(me.Role, Kit.Arcanist);
    me.Pos = me.Disp = new V2(560f, 470f);
    me.AimAt = new V2(930f, 380f);
    me.Aim = (me.AimAt - me.Pos).Angle;
    Func<EnemyType, V2, Enemy> put = (t, p) => {
      var e = (Enemy)spawn.Invoke(gm, new object[] { t, p, 0f, 0, false });
      e.SpawnT = 0f; e.Pos = e.Disp = p; e.Facing = (me.Pos - p).Angle; e.FireT = 99f; return e;
    };
    put(EnemyType.Brute, new V2(950f, 370f)).Hp *= 0.7f;
    put(EnemyType.Grunt, new V2(880f, 320f));
    put(EnemyType.Grunt, new V2(1000f, 450f)).Hp *= 0.4f;
    put(EnemyType.Grunt, new V2(870f, 440f));
    put(EnemyType.Sniper, new V2(980f, 270f));
    put(EnemyType.Grunt, new V2(700f, 650f));  // off the cursor: left alone
    put(EnemyType.Sniper, new V2(380f, 220f));
    me.ArrayT = 5f;
    float mt;
    float.TryParse(Environment.GetEnvironmentVariable("MOCKT") ?? "1.5", System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out mt);
    ArenaApp.ArrayMockV = v[0]; ArenaApp.ArrayMockT = mt;
    Console.Error.WriteLine("array mock: " + v + " t=" + mt);
  }
}

namespace Arena {
  public partial class ArenaApp {
    public static char ArrayMockV;
    public static float ArrayMockT;
    const float MockLock = 130f;
    const float MockEnd = 2.8f;  // the portal starts closing (no swords born after MockEnd - 0.2)  // enemies this near the cursor are the swarm's targets

    static float MEase(float x) { x = Mathf.Clamp01(x); return 1f - (1f - x) * (1f - x) * (1f - x); }
    static V2 MBez(V2 a, V2 c, V2 b, float u) => a * ((1 - u) * (1 - u)) + c * (2 * u * (1 - u)) + b * (u * u);

    /// The mock's draw, in place of the four sigils. True when it drew.
    bool DrawArrayMock(Player p, float t)
    {
      if (ArrayMockV == 0) return false;
      float T = ArrayMockT; t = T;  // the clip's own clock, so the spin moves frame to frame
      float open = MEase(T / 0.45f) * (1f - MEase((T - MockEnd) / 0.4f));
      V2 f = V2.FromAngle(p.Aim), s = new V2(-f.Y, f.X);
      // the targets: enemies near the cursor, nearest first
      var marks = new System.Collections.Generic.List<Enemy>();
      foreach (var e in game.Enemies.Values) if (e.Pos.CloserThan(p.AimAt, MockLock + e.Radius)) marks.Add(e);
      marks.Sort((a, b) => (a.Pos - p.AimAt).LengthSq.CompareTo((b.Pos - p.AimAt).LengthSq));
      DrawMockAim(p, marks, t, open);
      if (ArrayMockV == 'A') MockGate(p, f, s, t, T, open, marks);
      else if (ArrayMockV == 'B') MockRift(p, f, s, t, T, open, marks);
      else MockCrown(p, f, s, t, T, open, marks);
      return true;
    }

    /// The cursor: a slow gold rune ring, and a lock bracket on each target.
    void DrawMockAim(Player p, System.Collections.Generic.List<Enemy> marks, float t, float open)
    {
      V2 c = p.AimAt;
      Circle(c, MockLock, ArcViolet.WithAlpha(0.05f * open), 48);
      for (int k = 0; k < 24; k++)
        Arc(c, MockLock, 1.4f, ArcGold.WithAlpha(0.45f * open), 3, t * 0.3f + MathX.Tau * k / 24f, MathX.Tau / 60f);
      for (int k = 0; k < 4; k++)
        DrawRune(c + V2.FromAngle(-t * 0.5f + MathX.Tau * k / 4f) * MockLock, 4.5f, t, ArcGold.WithAlpha(0.8f * open));
      foreach (var e in marks)
      {
        float r = e.Radius + 9f;
        for (int k = 0; k < 4; k++)
        {
          float a = t * 1.5f + MathX.Tau * k / 4f;
          Arc(e.Disp, r, 2f, ArcGold.WithAlpha(0.85f * open), 6, a - 0.3f, 0.6f);
        }
        DrawRune(e.Disp + new V2(0f, -(e.Radius + 18f)), 4f, 0f, ArcPale.WithAlpha(0.9f * open));
      }
    }

    /// One spectral sword: pale blade, gold guard, violet ghost round it, pointing along `d`.
    void MockSword(V2 at, V2 d, float len, float a)
    {
      V2 n = new V2(-d.Y, d.X);
      SetColor(ArcViolet.WithAlpha(0.28f * a));
      Tri(at + d * (len * 0.62f), at - d * (len * 0.5f) + n * 5f, at - d * (len * 0.5f) - n * 5f);
      SetColor(ArcPale.WithAlpha(0.95f * a));
      Tri(at + d * (len * 0.55f), at - d * (len * 0.12f) + n * 2.4f, at - d * (len * 0.12f) - n * 2.4f);
      Line(at, at + d * (len * 0.4f), 0.8f, ArcViolet.WithAlpha(0.6f * a));  // the fuller
      Line(at - d * (len * 0.14f) + n * 5.5f, at - d * (len * 0.14f) - n * 5.5f, 2f, ArcGold.WithAlpha(0.95f * a));
      Line(at - d * (len * 0.14f), at - d * (len * 0.38f), 2f, ArcGold.WithAlpha(0.7f * a));
      Circle(at - d * (len * 0.4f), 1.8f, ArcGold.WithAlpha(0.9f * a), 6);
    }

    /// A sword on its homing curve from `from` (launched along `go`) to `to`, `u` (0..1) of the way, with its trail.
    void MockFlight(V2 from, V2 go, V2 to, float u, float bend)
    {
      if (u <= 0f || u >= 1.02f) return;
      if (u < 0.18f)  // the launch flash at the mouth
      {
        float k = 1f - u / 0.18f;
        Circle(from, 4f + 10f * (1f - k), ArcPale.WithAlpha(0.5f * k), 12);
        Arc(from, 6f + 14f * (1f - k), 1.4f, ArcGold.WithAlpha(0.8f * k), 14);
      }
      u = Mathf.Min(1f, u * (0.55f + 0.45f * u));  // eases out of the portal, then accelerates in
      V2 ctl = from + go * bend;
      const int n = 7;
      for (int i = n; i >= 1; i--)
      {
        float a = Mathf.Max(0f, u - i * 0.035f), b = Mathf.Max(0f, u - (i - 1) * 0.035f);
        Line(MBez(from, ctl, to, a), MBez(from, ctl, to, b), 3.2f * (1f - i / (float)(n + 1)), ArcViolet.WithAlpha(0.45f * (1f - i / (float)(n + 1))));
      }
      V2 at = MBez(from, ctl, to, u), ahead = MBez(from, ctl, to, Mathf.Min(1f, u + 0.02f)) - at;
      MockSword(at, ahead.LengthSq > 0.01f ? ahead.Normalized() : go, 30f, 1f);
    }

    /// The swords in flight: a new one every 0.12 s from `mouth`, each to the next target in turn.
    void MockSwarm(System.Collections.Generic.List<Enemy> marks, Func<int, V2> mouth, Func<int, V2> launch, float T, float start, float bend)
    {
      if (marks.Count == 0) return;
      for (int i = 0; i < 80; i++)
      {
        float born = start + i * 0.06f, u = (T - born) / 0.7f;
        if (u <= 0f || born > MockEnd - 0.2f) break;
        var e = marks[i % marks.Count];
        V2 jit = V2.FromAngle(i * 2.4f) * (e.Radius * 0.5f);
        if (u > 1f)  // the hit: a ring, four sparks and a gold cut, 0.25 s
        {
          float h = (u - 1f) / 0.36f;
          if (h > 1f) continue;
          V2 at = e.Disp + jit * 0.5f, d = (at - mouth(i)).Normalized();
          float k = 1f - h;
          Arc(at, 6f + 16f * MEase(h), 2f * k, ArcGold.WithAlpha(0.9f * k), 16);
          for (int q = 0; q < 4; q++)
          {
            V2 sd = V2.FromAngle(d.Angle + (q - 1.5f) * 0.45f);
            Line(at + sd * (6f + 18f * h), at + sd * (10f + 26f * h), 1.6f * k, ArcPale.WithAlpha(0.85f * k));
          }
          V2 n = new V2(-d.Y, d.X);
          Line(at - n * (e.Radius + 5f) * k, at + n * (e.Radius + 5f) * k, 2.4f * k, ArcGold.WithAlpha(k));
          continue;
        }
        MockFlight(mouth(i), launch(i), e.Disp + jit, u, bend);
      }
    }

    // A: Rune Gate, a round gate behind you, its rim a ring of turning runes; swords come out of its dark middle.
    void MockGate(Player p, V2 f, V2 s, float t, float T, float open, System.Collections.Generic.List<Enemy> marks)
    {
      V2 c = p.Disp - f * 62f;
      float R = 34f * open;
      Circle(c, R + 16f, ArcViolet.WithAlpha(0.08f), 32);
      Circle(c, R, ArcInk.WithAlpha(0.92f), 32);
      for (int k = 0; k < 3; k++)  // the swirl inside
        Arc(c, R * (0.35f + 0.22f * k), 1.6f, ArcViolet.WithAlpha(0.55f - 0.12f * k), 24, t * (2.2f - k * 0.5f) + k * 2f, MathX.Tau * 0.55f);
      Circle(c, R * 0.22f, ArcPale.WithAlpha(0.7f), 12);
      Arc(c, R, 2.4f, ArcGold.WithAlpha(0.95f), 32, -MathX.Pi / 2f, MathX.Tau * open);
      Arc(c, R + 11f, 1f, ArcGold.WithAlpha(0.45f), 32, t * 0.4f, MathX.Tau * open);
      int runes = 10;
      for (int k = 0; k < runes; k++)
      {
        float show = Mathf.Clamp01((T * 1f - k * 0.03f) / 0.15f);
        if (show <= 0f) continue;
        V2 at = c + V2.FromAngle(t * 0.6f + MathX.Tau * k / runes) * (R + 11f);
        DrawRune(at, 4.2f, t * -1.2f + k, ArcPale.WithAlpha(0.95f * show));
      }
      if (T < 0.5f) return;
      MockSwarm(marks, i => c + V2.FromAngle(i * 1.9f) * (R * 0.35f), i => (f + s * ((i % 5) - 2f) * 0.55f).Normalized(), T, 0.5f, 200f);
    }

    // B: Rift, an upright tear over your shoulder, edged with runes; swords pour out of it sideways and curve in.
    void MockRift(Player p, V2 f, V2 s, float t, float T, float open, System.Collections.Generic.List<Enemy> marks)
    {
      V2 c = p.Disp - f * 30f + new V2(0f, -58f);
      float rx = 15f * open, ry = 46f * MEase(T / 0.3f);
      const int segs = 28;
      Func<int, float, float, V2> Pt = (i, sx, sy) => { float a = MathX.Tau * i / segs; float pinch = 1f - 0.35f * Mathf.Abs(Mathf.Cos(a)); return c + new V2(Mathf.Sin(a) * sx * pinch, -Mathf.Cos(a) * sy); };
      SetColor(ArcViolet.WithAlpha(0.10f));
      for (int i = 0; i < segs; i++) Tri(c, Pt(i, rx + 14f, ry + 14f), Pt(i + 1, rx + 14f, ry + 14f));
      SetColor(ArcInk.WithAlpha(0.95f));
      for (int i = 0; i < segs; i++) Tri(c, Pt(i, rx, ry), Pt(i + 1, rx, ry));
      for (int k = 0; k < 4; k++)  // light rippling up the middle
      {
        float y = ((t * 0.8f + k * 0.25f) % 1f) * 2f - 1f;
        Line(c + new V2(-rx * 0.5f, y * ry * 0.8f), c + new V2(rx * 0.5f, y * ry * 0.8f - 4f), 1.2f, ArcViolet.WithAlpha(0.6f));
      }
      for (int i = 0; i < segs; i++) Line(Pt(i, rx, ry), Pt(i + 1, rx, ry), 2.2f, ArcGold.WithAlpha(0.95f));
      for (int k = 0; k < 8; k++)
      {
        int i = (k * segs / 8 + 1);
        float show = Mathf.Clamp01((T - k * 0.03f) / 0.15f);
        V2 at = Pt(i, rx + 11f, ry + 9f);
        DrawRune(at, 3.8f, t * 1.4f + k, ArcPale.WithAlpha(0.95f * show));
      }
      Line(p.Disp, c + new V2(0f, ry), 1f, ArcViolet.WithAlpha(0.35f));  // tethered to you
      if (T < 0.5f) return;
      MockSwarm(marks, i => c + new V2(rx * 0.3f * ((i % 2) * 2f - 1f), (((i * 7) % 9) / 4f - 1f) * ry * 0.7f), i => (f * 0.4f + s * ((i % 2) * 2f - 1f)).Normalized(), T, 0.5f, 170f);
    }

    // C: Crown, a rune ring round you; swords appear on its rim pointing at the cursor, then are loosed one after another.
    void MockCrown(Player p, V2 f, V2 s, float t, float T, float open, System.Collections.Generic.List<Enemy> marks)
    {
      V2 c = p.Disp;
      float R = 58f;
      Circle(c, R * open, ArcViolet.WithAlpha(0.06f), 40);
      Arc(c, R * open, 1.8f, ArcGold.WithAlpha(0.8f), 40, t * 0.5f, MathX.Tau * open);
      Arc(c, R * open - 7f, 1f, ArcViolet.WithAlpha(0.6f), 40, -t * 0.8f, MathX.Tau * 0.7f * open);
      for (int k = 0; k < 8; k++)
        DrawRune(c + V2.FromAngle(-t * 0.5f + MathX.Tau * k / 8f) * (R * open - 7f), 3.4f, t, ArcPale.WithAlpha(0.8f * open));
      const int slots = 8;
      float rate = 0.06f;
      for (int k = 0; k < slots; k++)  // the waiting swords on the rim, each refilled after it flies
      {
        float a = p.Aim + MathX.Pi + (k - (slots - 1) / 2f) * 0.34f;
        V2 at = c + V2.FromAngle(a) * (R + 8f);
        float since = T - 0.5f - k * rate;
        float cyc = since < 0f ? -1f : since % (slots * rate);
        float grow = since < 0f ? MEase((T - k * 0.04f) / 0.4f) : Mathf.Clamp01((cyc - rate * 1.5f) / (rate * 3f));
        if (grow <= 0f) continue;
        Circle(at, 7f, ArcViolet.WithAlpha(0.15f * grow), 10);
        MockSword(at, (p.AimAt - at).Normalized(), 24f * grow, grow);
      }
      if (T < 0.5f) return;
      MockSwarm(marks, i => { float a = p.Aim + MathX.Pi + ((i % slots) - (slots - 1) / 2f) * 0.34f; return c + V2.FromAngle(a) * (R + 8f); },
        i => { V2 o = V2.FromAngle(p.Aim + MathX.Pi + ((i % slots) - (slots - 1) / 2f) * 0.34f); return (o * 0.5f + V2.FromAngle(p.Aim)).Normalized(); }, T, 0.5f, 140f);
    }
  }
}
