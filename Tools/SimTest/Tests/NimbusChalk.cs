using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// The Nimbus (a cloud that follows your cursor, rains healing, strikes on click; Nimbus.cs) and Spirit Chalk (a line of ink that
// burns, heals and eats bullets, and a loop that pens enemies in; Chalk.cs).
static partial class Program
{
    static void NimbusChalkTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var volley = typeof(Game).GetMethod("FireVolley", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2 }, port, false, "Healer", Role.Healer, 2, 0, Kit.Elementalist);  // weapon 2: the Nimbus
        var client = JoinAs(port, false, "Tank", Role.Tank, 0);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        var ally = g.Players[2];
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        g.Bot = client.Game.Bot = false;
        V2 from = new V2(300f, 450f), allyAt = new V2(300f, 100f);
        var keep = new List<Enemy>();
        var pins = new Dictionary<Enemy, V2>();
        Func<V2, EnemyType, bool, Enemy> make = (at, type, pin) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            e.Hp = e.MaxHp = 1e5f; e.SpawnT = 0f; e.FireT = 99f; keep.Add(e);
            if (pin) pins[e] = at;
            return e;
        };
        Func<V2, Enemy> dummy = at => make(at, EnemyType.Brute, true);
        Func<int, PlayerInput> idle = f => new PlayerInput();
        Action<float, Func<int, PlayerInput>> play = (secs, input) =>
        {
            for (int f = 0; f < (int)Math.Round(secs * 60); f++)
            {
                foreach (var e in g.Enemies.Values) if (!keep.Contains(e)) e.Hp = 0f;
                foreach (var e in keep) { if (pins.ContainsKey(e)) e.Pos = pins[e]; e.FireT = 99f; }
                me.Pos = from;
                ally.Pos = client.Game.Me.Pos = allyAt;
                host.Poll(); g.FixedStep(dt, input(f)); g.Update(dt);
                client.Poll(); client.Game.FixedStep(dt, new PlayerInput()); client.Game.Update(dt);
            }
        };
        Action reset = () => { foreach (var e in keep) e.Hp = 0f; keep.Clear(); pins.Clear(); g.Bullets.Clear(); g.Ink.Clear(); };
        Func<Minion> cloud = () => g.Minions.Values.FirstOrDefault(m => m.Owner == 1 && m.Kind == MinionKind.Nimbus && m.Companion);
        play(0.3f, idle);
        me.Weapon = Defs.Weapon(Role.Healer, Kit.Elementalist, 2);

        // --- Nimbus.
        Check(me.Weapon.Id == WeaponId.Nimbus, "the Healer Elementalist's third weapon is the Nimbus (it replaced the Prism Beam)");
        V2 cursor = from + new V2(200f, 0f);
        play(2.5f, f => new PlayerInput { AimAt = cursor });
        var c = cloud();
        Check(c != null && c.Pos.DistanceTo(cursor) < 20f, "the Nimbus's cloud floats to your cursor");
        play(0.3f, f => new PlayerInput { AimAt = from + new V2(1000f, 0f) });
        play(3f, f => new PlayerInput { AimAt = from + new V2(1000f, 0f) });
        c = cloud();
        Check(c != null && Math.Abs(c.Pos.DistanceTo(from) - Game.NimbusReach(me)) < 6f, "it stays within its leash of you when the cursor is farther (" + (int)Game.NimbusLeash + " px)");
        Check(client.Game.Minions.Values.Any(m => m.Kind == MinionKind.Nimbus), "the other player sees the cloud");

        // Rain: an ally under the cloud mends, one beside it doesn't.
        play(0.2f, f => new PlayerInput { AimAt = cursor });
        play(2.5f, f => new PlayerInput { AimAt = cursor });  // back over the cursor
        c = cloud();
        allyAt = c.Pos;
        ally.Hp = ally.MaxHp * 0.3f;
        float a0 = ally.Hp;
        play(1f, f => new PlayerInput { AimAt = cursor });
        float rained = ally.Hp - a0;
        allyAt = c.Pos + new V2(0f, Game.NimbusRain + 60f);
        ally.Hp = ally.MaxHp * 0.3f;
        a0 = ally.Hp;
        play(1f, f => new PlayerInput { AimAt = cursor });
        float dry = ally.Hp - a0;
        Console.WriteLine("  nimbus: rain healed an ally under it {0:0.0} in 1 s ({1} a second), one outside it {2:0.0}", rained, (int)Game.NimbusRainHeal, dry);
        Check(rained >= Game.NimbusRainHeal * 0.9f && rained < Game.NimbusRainHeal * 1.6f && dry < 1f, "rain heals allies standing under the cloud, 8 a second, and nobody else");
        allyAt = new V2(300f, 100f);

        // The strike: on the spot under the cloud, not elsewhere.
        c = cloud();
        var under = dummy(c.Pos); var beside = dummy(c.Pos + new V2(110f, 0f));
        play(0.1f, f => new PlayerInput { AimAt = cursor });
        float u0 = under.Hp, b0 = beside.Hp;
        volley.Invoke(g, new object[] { me, 0f, 0 });
        play(1f / 60f, f => new PlayerInput { AimAt = cursor });
        float bolt = me.Weapon.Damage * me.Stats.DmgMult;
        Console.WriteLine("  nimbus: strike hit the enemy under the cloud for {0:0.0} (a strike is {1:0.0}) and the one 110 px off for {2:0.0}", u0 - under.Hp, bolt, b0 - beside.Hp);
        Check(Math.Abs(u0 - under.Hp - bolt) < 0.5f && b0 - beside.Hp < 0.01f, "a click strikes the spot under the cloud for " + (int)me.Weapon.Damage + ", in a small blast");
        play(0.1f, f => new PlayerInput { AimAt = cursor });
        Check(client.Game.Effects.Any(e => e.Kind == FxKind.Strike), "the other player sees the lightning");
        // Extra projectiles: more strikes, in a ring round the first.
        me.Stats.ExtraShots = 2;
        u0 = under.Hp;
        volley.Invoke(g, new object[] { me, 0f, 2 });
        play(1f / 60f, f => new PlayerInput { AimAt = cursor });
        Check(u0 - under.Hp > bolt * 2.5f, "each extra projectile adds a strike landing round the cloud (" + (u0 - under.Hp).ToString("0") + " on the middle of it)");
        me.Stats.ExtraShots = 0;
        reset();

        // --- Spirit Chalk.
        me.Weapon = Defs.Weapon(Role.Healer, Kit.Summoner, 2);
        me.Kit = Kit.Summoner;
        Check(me.Weapon.Id == WeaponId.SpiritChalk, "the Healer Summoner's third weapon is the Spirit Chalk (it replaced the Totem Staff)");
        g.Minions.Clear();
        play(0.3f, idle);

        // Drawing: hold and drag, ~250 px a second; the line is laid as dots and only so much ink fits.
        V2 start = from + new V2(120f, -60f);
        play(1.2f, f => new PlayerInput { Fire = true, AimAt = start + new V2(Math.Min(f * 3.5f, 260f), 0f) });
        int dots = g.Ink.Count(d => d.Owner == 1);
        float lineLen = g.Ink.Where(d => d.Owner == 1).Max(d => d.Pos.X) - g.Ink.Where(d => d.Owner == 1).Min(d => d.Pos.X);
        Console.WriteLine("  chalk: dragged 1.2 s: {0} dots, a line {1:0} px long; the client sees {2}", dots, lineLen, client.Game.Ink.Count);
        Check(dots >= 18 && lineLen > 200f && lineLen < 290f, "holding and dragging draws a line of ink along the cursor's path (up to " + (int)Game.ChalkSpeed + " px a second)");
        Check(client.Game.Ink.Count == g.Ink.Count, "the other player sees the same ink");
        play(1f, idle);  // let go
        Check(g.Ink.Count(d => d.Owner == 1) >= dots - 3, "letting go stops drawing");
        play(4.5f, idle);
        Check(g.Ink.Count == 0, "ink fades after " + (int)Game.ChalkLife + " s");

        // Ink cap: scribbling all over uses up the ink.
        V2 mid = from + new V2(160f, 0f);
        int capped = 0;
        play(5f, f =>
        {
            capped = Math.Max(capped, g.Ink.Count(d => d.Owner == 1));  // the most at once (a line fades after 4 s)
            return new PlayerInput { Fire = true, AimAt = mid + V2.FromAngle(f * 0.07f) * 180f * (0.5f + 0.5f * (float)Math.Sin(f * 0.02f)) };
        });
        Check(capped <= (int)(Game.ChalkInk / Game.ChalkStep) && capped >= 40, "ink is limited to " + (int)Game.ChalkInk + " px of line at a time (" + capped + " dots)");
        play(0.1f, idle);
        reset();
        play(0.5f, idle);

        // Smoothness: the chalk keeps up with a fast cursor, its tip is sent for drawing, and running out of ink doesn't leave a chord.
        V2 far1 = from + new V2(100f, 0f), far2 = from + new V2(340f, 0f);
        play(0.2f, f => new PlayerInput { Fire = true, AimAt = far1 });
        play(0.45f, f => new PlayerInput { Fire = true, AimAt = far2 });  // a 240 px flick
        float tipAt = client.Game.InkTip.ContainsKey(1) ? client.Game.InkTip[1].X : -1f;
        Check(client.Game.InkTip.ContainsKey(1) && Math.Abs(tipAt - far2.X) < 8f, "the chalk keeps up with a fast cursor, and the other player gets its tip (" + tipAt.ToString("0") + " vs " + far2.X.ToString("0") + ")");
        play(0.3f, idle);
        Check(!client.Game.InkTip.ContainsKey(1), "the tip goes away when you let go");
        reset();
        play(0.3f, idle);
        V2 into = from + new V2(160f, 0f), away = from + new V2(330f, 130f);
        play(1.6f, f => new PlayerInput { Fire = true, AimAt = into + V2.FromAngle(f * 0.13f) * 120f });  // fills the ink
        var before = g.Ink.Where(d => d.Owner == 1).ToList();
        int strokeBefore = before.Count > 0 ? before.Last().Stroke : -1;
        play(5f, f => new PlayerInput { Fire = true, AimAt = away });  // the chalk goes off with no ink left; the line fades and the ink comes back
        var later = g.Ink.Where(d => d.Owner == 1 && d.Stroke != strokeBefore).ToList();
        Console.WriteLine("  chalk: out of ink with {0} dots; {1} new dots began again at the cursor ({2:0} px from it at most)", before.Count, later.Count, later.Count > 0 ? later.Max(d => d.Pos.DistanceTo(away)) : -1f);
        Check(later.Count > 0 && later.All(d => d.Pos.DistanceTo(away) < 40f), "when the ink comes back, the line starts fresh where the chalk is, not as a straight chord from where it ran out");
        reset();
        play(0.3f, idle);

        // What a line does (ink put down by hand, so this isn't about drawing): burns and slows enemies on it, heals allies on it.
        Func<V2, V2, InkDot[]> lay = (a, b) =>
        {
            var l = new List<InkDot>();
            for (float x = 0f; x <= (b - a).Length; x += Game.ChalkStep) { var d = new InkDot { Pos = a + (b - a).Normalized() * x, T = 3f, Owner = 1, Stroke = 900 }; g.Ink.Add(d); l.Add(d); }
            return l.ToArray();
        };
        allyAt = from + new V2(100f, 60f);  // first beside where the line will be: your own aura mends them a little, so that's the baseline
        ally.Hp = ally.MaxHp * 0.3f;
        a0 = ally.Hp;
        play(1f, idle);
        float aura = ally.Hp - a0;
        lay(from + new V2(150f, -100f), from + new V2(150f, 100f));
        var onLine = dummy(from + new V2(150f, 0f)); var offLine = dummy(from + new V2(250f, 0f));
        allyAt = from + new V2(150f, 60f);
        ally.Hp = ally.MaxHp * 0.3f;
        a0 = ally.Hp;
        float hp0 = onLine.Hp, off0 = offLine.Hp;
        play(1f, idle);
        float burnt = hp0 - onLine.Hp, mended = ally.Hp - a0 - aura;
        Console.WriteLine("  chalk: an enemy on the line took {0:0.0} in 1 s (slow {1:0.00}), one off it {2:0.0}; an ally on it healed {3:0.0}", burnt, onLine.SlowMult, off0 - offLine.Hp, mended);
        float expect = me.Weapon.Damage * me.Stats.DmgMult;
        Check(Math.Abs(burnt - expect) < expect * 0.15f && off0 - offLine.Hp < 0.01f, "ink burns an enemy touching it (" + (int)expect + " a second) and nothing else");
        Check(onLine.SlowMult <= 1f - Game.ChalkSlow + 0.01f, "and slows it by " + (int)(Game.ChalkSlow * 100) + "%");
        Check(mended >= Game.ChalkHeal * 0.9f && mended < Game.ChalkHeal * 1.7f, "an ally standing on ink heals " + (int)Game.ChalkHeal + " a second");
        reset();
        allyAt = new V2(300f, 100f);
        play(0.2f, idle);

        // Bullets: one that crosses ink is destroyed, and wears it down.
        var line = lay(from + new V2(200f, -100f), from + new V2(200f, 100f));
        float life0 = line.Sum(d => d.T);
        var shot = (Bullet)shoot.Invoke(g, new object[] { Defs.SmallShot, from + new V2(120f, 0f), new V2(300f, 0f), 0f, 2f, 5f, 0, false, 0f });
        int shotId = shot.Id;
        play(0.6f, idle);
        float worn = life0 - 0.6f * line.Length - line.Sum(d => d.T);
        Check(!g.Bullets.ContainsKey(shotId), "an enemy bullet crossing ink is destroyed");
        Check(worn > Game.ChalkWear * 0.7f && worn < Game.ChalkWear * 2f, "and wears the ink down (" + worn.ToString("0.00") + " s)");
        reset();
        play(0.3f, idle);

        // A loop traps non-boss enemies inside it. Draw a real circle round a Brute that is chasing you, and a Warden-sized boss with it.
        V2 ctr = from + new V2(190f, 0f);
        var brute = make(ctr + new V2(10f, 10f), EnemyType.Brute, true);  // held still while you draw round them
        var grunt = make(ctr + new V2(-20f, -25f), EnemyType.Grunt, true);
        var big = make(ctr + new V2(25f, -15f), EnemyType.Boss, true);
        float R = 78f;
        for (int f = 0; f < 240 && !g.Ink.Any(d => d.Pen != 0); f++)
        {
            int ff = f;
            play(1f / 60f, _ => new PlayerInput { Fire = true, AimAt = ctr + V2.FromAngle(Math.Min(ff * 3.05f / 60f, MathX.Tau + 0.7f)) * R });  // about 240 px a second round the circle
        }
        foreach (var e in new[] { brute, grunt, big }) { pins.Remove(e); e.Speed = 150f; }  // now they're free to chase you
        play(0.2f, idle);
        int penId = g.Ink.Where(d => d.Pen != 0).Select(d => d.Pen).FirstOrDefault();
        var ring = new List<V2>();
        g.PenRing(penId, ring);
        Console.WriteLine("  chalk: loop of {0} dots (pen {1}); brute penned {2}, grunt {3}, boss {4}; the client sees the brute penned: {5}", ring.Count, penId, brute.PenId != 0, grunt.PenId != 0, big.PenId != 0,
            client.Game.Enemies.ContainsKey(brute.Id) && client.Game.Enemies[brute.Id].Penned);
        Check(penId != 0 && ring.Count >= 20, "drawing a circle closes a loop");
        Check(brute.PenId == penId && grunt.PenId == penId, "enemies inside it when it closes are penned");
        Check(big.PenId == 0, "a boss isn't held");
        Check(client.Game.Enemies.ContainsKey(brute.Id) && client.Game.Enemies[brute.Id].Penned, "the other player sees who is penned");
        // They stay inside while it holds, however they push (they're chasing you, on the far side of the ring).
        bool stayed = true;
        for (int k = 0; k < 20 && g.Ink.Any(d => d.Pen == penId); k++)
        {
            play(0.1f, idle);
            if (brute.PenId == penId && !Game.InsidePoly(ring, brute.Pos)) stayed = false;
            if (grunt.PenId == penId && !Game.InsidePoly(ring, grunt.Pos)) stayed = false;
        }
        Check(stayed, "a penned enemy can't leave the loop while it holds");
        // And it lets go after ChalkSeal seconds.
        play(Game.ChalkSeal + 1f, idle);
        Check(brute.PenId == 0 && grunt.PenId == 0 && !g.Ink.Any(d => d.Pen != 0), "the loop fades and lets them go");
        reset();

        // A broken ring frees them early: shoot a gap in it.
        play(0.3f, idle);
        var pen2 = new List<InkDot>();
        for (int i = 0; i < 24; i++)
        {
            var d = new InkDot { Pos = ctr + V2.FromAngle(MathX.Tau * i / 24f) * 60f, T = Game.ChalkSeal, Owner = 1, Stroke = 901, Pen = 77 };
            g.Ink.Add(d); pen2.Add(d);
        }
        var held = make(ctr, EnemyType.Brute, false);
        held.Speed = 150f;
        var penField = typeof(Game).GetField("pens", flags);
        var pens = (Dictionary<int, InkPen>)penField.GetValue(g);
        var inkPen = new InkPen { Id = 77, Owner = 1, T = Game.ChalkSeal, Dots = 24 };
        foreach (var d in pen2) inkPen.Poly.Add(d.Pos);
        pens[77] = inkPen;
        held.PenId = 77; held.PenPos = held.Pos;
        play(0.3f, idle);
        pen2[5].T = 0.01f;  // one dot gone (as if worn through)
        play(0.3f, idle);
        Check(held.PenId == 0, "a gap in the ring frees what it held");
        reset();
        host.Leave(""); client.Leave("");
    }
}
