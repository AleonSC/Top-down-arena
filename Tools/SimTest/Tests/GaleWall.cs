using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Gale Wall (GaleWall.cs): the Elementalist DPS's 4th choice (pick 3 reaching the run), the lens catching enemy shots
// (each a charge, up to the cap), the unleash on a second press (cone damage that grows with the charge, shoves) or when it runs
// out, the mastered lens, replication, and going down.
static partial class Program
{
    static void GaleWallTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
        var masteredField = typeof(Game).GetField("castMastered", flags);

        Check(Upgrades.AbilityLimit(Role.Dps, Kit.Elementalist) == 4 && Upgrades.FirstAbility(Role.Dps, Kit.Elementalist, 3) == AbilityId.GaleWall
            && Upgrades.AbilityLimit(Role.Healer, Kit.Ninja) == 3
            && Upgrades.FirstAbility(Role.Healer, Kit.Ninja, 3) == Upgrades.FirstAbility(Role.Healer, Kit.Ninja, 0),  // (the Healer Ninja has no 4th choice; every Knight role now has one)
            "the Elementalist DPS has a 4th choice, Gale Wall; classes without a 4th keep three");

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Elementalist", Role.Dps, 0, 3, Kit.Elementalist);
        var client = JoinAs(port, false, "Friend", Role.Tank, 0, 0, Kit.Knight);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        Check(me.FirstAbility == AbilityId.GaleWall && me.Slots[0].Id == AbilityId.GaleWall, "picking the 4th choice brings Gale Wall into the run as the 1st ability");
        g.DevSetGod(1, true); g.DevSetGod(2, true);

        V2 mePos = new V2(300f, 450f), aim = new V2(900f, 450f);
        var spots = new Dictionary<Enemy, V2>();
        Func<V2, Enemy> pin = at =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, at, 0f, 0, false });
            e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; e.FireT = 99f; spots[e] = at; return e;
        };
        Action<float> step = secs => { for (int f = 0; f < secs / dt; f++) { foreach (var kv in spots) { kv.Key.Pos = kv.Value; kv.Key.FireT = 99f; } me.Pos = mePos; me.Aim = 0f; host.Poll(); client.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = aim }); } };
        Action reset = () => { g.Enemies.Clear(); spots.Clear(); g.Bullets.Clear(); g.Effects.Clear(); me.Pos = mePos; me.Aim = 0f; me.Slots[0].Charges = 1; me.Slots[0].Timer = 0f; };
        Action<int> volley = n => { for (int i = 0; i < n; i++) shoot.Invoke(g, new object[] { Defs.SmallShot, new V2(640f + i * 6f, 450f + (i % 9 - 4) * 15f), new V2(-260f, 0f), 0f, 6f, 5f, 0, false, 0f }); };
        Action press = () => hostAbility.Invoke(g, new object[] { 1, 0, aim, false });

        // The cast and the catch.
        reset();
        masteredField.SetValue(g, false);
        var keep = pin(new V2(1500f, 900f));  // (somewhere out of the way, so the wave doesn't end)
        press();
        Check(me.GaleT > Game.GaleTime - 0.01f && me.GalePos.DistanceTo(mePos) < 1f && !me.GaleWide, "the 1st ability puts the lens up for 5 s where you stand");
        volley(12);
        step(1.6f);
        Check(me.GaleDots.Count == 12 && !g.Bullets.Values.Any(b => b.Owner == 0), "every enemy shot that reaches the lens stops there and charges it");
        Check(client.Game.Players[1].GaleT > 0f && client.Game.Players[1].GaleDots.Count == 12, "other screens see the lens and the shots hung in it");
        Check(g.Effects.Any(e => e.Kind == FxKind.GaleCatch), "each catch is sent as a look");

        // The unleash: a second press. In the cone: hit for 40 + 6 per charge, shoved; outside: nothing.
        var inCone = pin(new V2(560f, 470f));
        var behind = pin(new V2(100f, 450f));
        var wide = pin(new V2(300f, 750f));
        var start = inCone.Pos;
        float D = Game.GaleDamage(12) * me.Stats.DmgMult;
        press();
        Check(me.GaleT == 0f && me.GaleDots.Count == 0 && g.Effects.Any(e => e.Kind == FxKind.GaleBlast && e.Radius == 12f), "pressing again unleashes it (no new cast) and sends the blast with its charge");
        float hit = inCone.MaxHp - inCone.Hp;
        Console.WriteLine("  gale wall: 12 charges, hit {0:0.0} of {1:0.0}", hit, D);
        Check(Math.Abs(hit - D) < 1f, "an enemy in the cone takes 40 + 6 per charge");
        Check(behind.Hp == behind.MaxHp && wide.Hp == wide.MaxHp, "enemies behind you or off to the side aren't hit");
        Check(inCone.KnockLeft.Length > 1f || inCone.Pos.DistanceTo(start) > 1f, "enemies hit are shoved back");
        Check(me.Slots[0].Charges == 0 || me.Slots[0].Timer > 0f, "the cooldown is running");

        // Running out: it unleashes itself. The cap.
        reset();
        keep = pin(new V2(1500f, 900f));
        press();
        volley(55);
        step(2.8f);
        Check(me.GaleDots.Count == Game.GaleCap, "it holds up to 40 charges (the rest are still stopped)");
        Check(!g.Bullets.Values.Any(b => b.Owner == 0), "shots past the cap still stop in it");
        step(Game.GaleTime - 2.8f + 0.2f);
        Check(me.GaleT == 0f && g.Effects.Any(e => e.Kind == FxKind.GaleBlast && e.Radius == Game.GaleCap), "when the time runs out it unleashes by itself");

        // Mastered: wider, holds more.
        reset();
        keep = pin(new V2(1500f, 900f));
        masteredField.SetValue(g, true);
        typeof(Game).GetMethod("CastGaleWall", flags).Invoke(g, new object[] { me, true });
        Check(me.GaleWide && Game.GaleCapOf(me) == Game.GaleCapMastered && Game.GaleSpan(me) > (float)Math.Asin(Game.GaleHalfWidth / Game.GaleRadius) + 0.1f, "mastered: wider, and it holds up to 60");

        // Going down: the lens is gone.
        me.Downed = true;
        step(dt);
        Check(me.GaleT == 0f && me.GaleDots.Count == 0, "going down takes the lens away");
        me.Downed = false;
        masteredField.SetValue(g, false);
        s[0].Leave(""); s[1].Leave("");
    }
}
