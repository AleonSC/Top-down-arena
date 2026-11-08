using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

// The Lightbringer (Lightbringer.cs): Dawn, Mercy, Noon, the light gauge, a Radiant Noon and the Sunlance.
static partial class Program
{
    static void LightbringerTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var volley = typeof(Game).GetMethod("FireVolley", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2 }, port, false, "Healer", Role.Healer, 1, 0, Kit.Knight);
        var client = JoinAs(port, false, "Tank", Role.Tank, 0);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        var ally = g.Players[2];
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        g.Bot = client.Game.Bot = false;
        Check(me.Weapon.Id == WeaponId.Lightbringer, "the Healer Knight's second weapon is the Lightbringer");
        V2 from = new V2(400f, 450f), allyAt = new V2(400f, 330f);
        var keep = new List<Enemy>();
        var pins = new Dictionary<Enemy, V2>();
        var heard = new HashSet<Sfx>();
        var marks = new HashSet<int>();  // LightMark kinds seen on the client (1 heal burst, 2 ward snap, 3 a shot on the ward)
        var looks = new HashSet<ShotLook>();  // the Look of the Lightbringer's crescents, as the client got them
        Func<V2, Enemy> dummy = at => { var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false }); e.Hp = e.MaxHp = 1e5f; e.SpawnT = 0f; e.FireT = 99f; keep.Add(e); pins[e] = at; return e; };
        Action<float, PlayerInput> play = (secs, input) =>
        {
            for (int f = 0; f < secs * 60; f++)
            {
                foreach (var e in g.Enemies.Values) if (!keep.Contains(e)) e.Hp = 0f;
                foreach (var e in keep) { if (pins.ContainsKey(e)) e.Pos = pins[e]; e.FireT = 99f; }
                me.Pos = from;
                ally.Pos = client.Game.Me.Pos = allyAt;
                host.Poll(); g.FixedStep(dt, input);
                foreach (var c in g.Sounds) heard.Add(c.Sfx);
                g.Update(dt);
                client.Poll(); client.Game.FixedStep(dt, new PlayerInput()); client.Game.Update(dt);
                foreach (var e in client.Game.Effects) if (e.Kind == FxKind.LightMark) marks.Add((int)Math.Round(e.Pos2.X));
                foreach (var b in client.Game.Bullets.Values) if (b.Kind == Defs.KnightWave && b.Owner == 1) looks.Add(b.Look);
            }
        };
        Action reset = () => { foreach (var e in keep) e.Hp = 0f; keep.Clear(); pins.Clear(); g.Bullets.Clear(); me.Light = 0; me.Combo = 0; me.ComboT = 0f; ally.ShieldHp = 0f; ally.ShieldT = 0f; ally.MercyT = 0f; };
        play(0.3f, new PlayerInput());

        // The chain: held fire runs Dawn, Mercy, Noon and starts over; each enemy a cut hits adds a pip, the ring two.
        var foe = dummy(from + new V2(45f, 0f));
        var steps = new List<int>();
        var fx = new HashSet<int>();
        for (int f = 0; f < 300 && steps.Count < 4; f++)
        {
            play(1f / 60f, new PlayerInput { Fire = true, AimAt = from + new V2(300f, 0f) });
            foreach (var e in g.Effects) if (e.Kind == FxKind.LightStep) fx.Add((int)Math.Round(e.Radius) % 10);
            int st = TwinBladesMotion.StepOf(me.Combo);
            if (st != 0 && (steps.Count == 0 || steps[steps.Count - 1] != st))
                steps.Add(st);
        }
        int gauge = me.Light;
        play(1.6f, new PlayerInput { AimAt = from + new V2(300f, 0f) });
        Console.WriteLine("  chain: steps {0}, effects {1}; gauge after a chain and a bit {2}; after a pause the step is {3}", string.Join(",", steps), string.Join(",", fx.OrderBy(x => x)), gauge, TwinBladesMotion.StepOf(me.Combo));
        Check(string.Join(",", steps) == "1,2,3,1" && fx.SetEquals(new[] { 1, 2, 3 }), "the Lightbringer chains Dawn Cut, Mercy Cut and Noon Sunburst, then starts over");
        Check(gauge >= 4, "each enemy a cut hits adds a light pip and the Sunburst adds two");
        Check(TwinBladesMotion.StepOf(me.Combo) == 0, "the chain starts over after a pause");
        reset();

        // Dawn's crescent heals the ally it passes; Mercy's wards them (on every screen).
        float aim = (allyAt - from).Angle;
        Func<int, float> heal = step =>
        {
            ally.Hp = ally.MaxHp * 0.3f;
            me.Combo = step == 1 ? 0 : (1 << 3) | (step - 1); me.ComboT = 1f;
            float a0 = ally.Hp;
            if (step > 0) volley.Invoke(g, new object[] { me, aim, 0 });
            play(0.6f, new PlayerInput());
            return ally.Hp - a0;
        };
        float aura = heal(0);
        float dawn = heal(1) - aura;
        reset();
        heal(2);
        float shield = ally.ShieldHp;
        bool wardSeen = client.Game.Players[2].MercyT > 0f;
        Console.WriteLine("  crescents: Dawn healed {0:0.0} (aura alone {1:0.0}); Mercy left a {2:0} HP ward (seen on the client {3})", dawn, aura, shield, wardSeen);
        Check(dawn >= Game.CrescentHeal * 0.95f && dawn < Game.CrescentHeal * 2f, "Dawn Cut's crescent heals the ally it passes");
        Check(shield >= Game.MercyWard - 0.01f && wardSeen, "Mercy Cut's crescent wards the ally it passes, and every screen sees the ward");
        reset();

        // Noon heals everyone in its ring; a Radiant Noon (a full gauge) heals three times as much, wards them and spends the gauge.
        allyAt = from + new V2(30f, 100f);
        Func<int, float> noon = light =>
        {
            ally.Hp = ally.MaxHp * 0.3f;
            me.Light = light;
            me.Combo = (1 << 3) | 2; me.ComboT = 1f;
            float a0 = ally.Hp;
            volley.Invoke(g, new object[] { me, 0f, 0 });
            play(0.6f, new PlayerInput());
            return ally.Hp - a0;
        };
        float plain = noon(0) - aura;
        reset();
        float radiant = noon(Game.LightMax) - aura;
        float radiantWard = ally.ShieldHp;
        int afterRadiant = me.Light;
        Console.WriteLine("  noon: healed {0:0.0}; radiant {1:0.0}, ward {2:0}, gauge after {3}", plain, radiant, radiantWard, afterRadiant);
        Check(plain >= Game.NoonHeal * 0.95f && Math.Abs(radiant - plain * Game.LightRadiant) < 2f, "Noon Sunburst heals allies in its ring; a Radiant one heals three times as much");
        Check(radiantWard >= Game.MercyWard - 0.01f && afterRadiant == 0, "a Radiant Noon wards everyone in its ring and spends the gauge");
        reset();

        // The Sunlance: the ally nearest the cursor, 4 a pip, and every enemy on the line; it needs 4 pips and spends them all.
        allyAt = from + new V2(320f, 0f);
        var onLine = dummy(from + new V2(160f, 0f));
        Func<int, float[]> lance = light =>
        {
            ally.Hp = ally.MaxHp * 0.3f;
            me.Light = light; me.LanceCd = 0f; me.FireCd = 0f;
            float a0 = ally.Hp, e0 = onLine.Hp;
            play(2f / 60f, new PlayerInput { WeaponAction = true, AimAt = allyAt + new V2(10f, 10f) });
            play(0.7f, new PlayerInput { AimAt = allyAt });
            return new[] { ally.Hp - a0, e0 - onLine.Hp };
        };
        float[] base0 = lance(0);
        play(1.6f, new PlayerInput());
        float[] low = lance(Game.LanceNeed - 1);
        play(1.6f, new PlayerInput());
        float[] full = lance(9);
        float cd = me.LanceCd;
        bool seen = client.Game.Effects.Any(e => e.Kind == FxKind.Sunlance) || g.Effects.Any(e => e.Kind == FxKind.Sunlance);
        Console.WriteLine("  sunlance: 9 pips healed {0:0.0} (aura {1:0.0}) and hit the enemy on the line for {2:0}; with {3} pips nothing ({4:0.0}, {5:0}); gauge after {6}, cooldown {7:0.0} s",
            full[0], base0[0], full[1], Game.LanceNeed - 1, low[0] - base0[0], low[1], me.Light, cd);
        Check(Math.Abs(full[0] - base0[0] - Game.LancePerPip * 9) < 3f && full[1] >= (Game.LanceBase + Game.LanceDmgPip * 9) * 0.99f && me.Light == 0,
            "the Sunlance heals the ally nearest your cursor 4 a pip, hits every enemy on the line and spends the gauge");
        Check(Math.Abs(low[0] - base0[0]) < 1f && low[1] < 1f, "the Sunlance needs 4 light");
        Check(cd > 0f && cd < Game.LanceCooldown, "the Sunlance has a short cooldown");
        Check(seen, "the Sunlance sends its effect");

        // A shot on a warded ally: the ward soaks it and the plate facing it flashes on every screen.
        reset();
        ally.ShieldHp = 0f;
        allyAt = from + new V2(0f, -120f);
        var warder = typeof(Game).GetMethod("Ward", flags);
        warder.Invoke(g, new object[] { ally, Game.MercyWard });
        var hurt = typeof(Game).GetMethod("Damage", flags);
        float hp0 = ally.Hp;
        g.DevSetGod(2, false); ally.InvulnT = 0f;
        hurt.Invoke(g, new object[] { ally, 5f, "test" });
        g.DevSetGod(2, true);
        play(0.1f, new PlayerInput());
        Console.WriteLine("  ward struck: health {0:0} -> {1:0}; marks seen {2}; crescent looks {3}", hp0, ally.Hp, string.Join(",", marks.OrderBy(x => x)), string.Join(",", looks));
        Check(ally.Hp >= hp0 - 0.01f && marks.SetEquals(new[] { 1, 2, 3 }), "the ward soaks a shot; heal bursts, the ward snapping shut and a struck ward all reach the other screens");
        Check(looks.Contains(ShotLook.Dart) && looks.Contains(ShotLook.Crescent), "Dawn's and Mercy's crescents reach the other screens marked as such (their Sunrise look)");

        // Its own sound bank (LightSoundBank.cs), and none of the generic swing whooshes.
        var want = new[] { Sfx.LightCut1, Sfx.LightCut2, Sfx.LightNoon, Sfx.LightRadiant, Sfx.LightHit, Sfx.LightHeal, Sfx.LightWard, Sfx.LanceGather, Sfx.LanceRelease, Sfx.LanceFizzle };
        var missing = want.Where(x => !heard.Contains(x)).ToList();
        Console.WriteLine("  lightbringer sounds missing: {0}", missing.Count == 0 ? "none" : string.Join(", ", missing));
        Check(missing.Count == 0, "every Lightbringer sound is raised: both cuts, Noon and a Radiant Noon, a cut landing, a crescent healing and warding, the Sunlance gathering, released and refused");
        Check(!heard.Contains(Sfx.SwingLight) && !heard.Contains(Sfx.SwingHeavy) && !heard.Contains(Sfx.SpellHoly), "the Lightbringer no longer uses the generic swing and spell sounds");
        host.Leave(""); client.Leave("");
    }
}
