using System.Linq;
using System;
using Arena;

// Lumen Ward (the Healer Elementalist's cone of light; AbilityId.Sanctify, Core/LumenWard.cs): heals allies inside it 9 HP/s, burns enemy
// bullets inside, slows enemy bullets outside to a full stop and lets them go when it ends; the capstone freezes enemies outside.
static partial class Program
{
    static void LumenWardTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var cast = typeof(Game).GetMethod("HostAbility", flags);
        var make = typeof(Game).GetMethod("MakeBullet", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2 }, port, false, "Healer", Role.Healer, 0, 2, Kit.Elementalist);
        var client = JoinAs(port, false, "Tank", Role.Tank, 0);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        var ally = g.Players[2];
        g.Bot = client.Game.Bot = false;
        V2 origin = new V2(300f, 450f), inside = new V2(600f, 450f), outside = new V2(300f, 100f);
        V2 at = inside;
        Action<float> play = secs =>
        {
            for (int f = 0; f < secs * 60; f++)
            {
                foreach (var e in g.Enemies.Values) if (e.Type != EnemyType.Grunt || e.Pos.DistanceTo(outside) > 40f) e.Hp = 0f;
                me.Pos = origin;
                ally.Pos = client.Game.Me.Pos = at;
                host.Poll(); g.FixedStep(dt, new PlayerInput()); g.Update(dt);
                client.Poll(); client.Game.FixedStep(dt, new PlayerInput()); client.Game.Update(dt);
            }
        };
        Func<V2, V2, Bullet> shot = (pos, vel) => (Bullet)make.Invoke(g, new object[] { 9000 + g.Bullets.Count, Defs.BigShot, 0, pos, vel, 0f, 60f, false, 0f });
        Action use = () => { g.ShowcaseSetAbility(AbilityId.Sanctify); me.Slots[2 - 2].Charges = 1; cast.Invoke(g, new object[] { 1, 0, new V2(800f, 450f), true }); };
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        play(0.3f);
        g.Zones.Clear(); g.Bullets.Clear();
        g.DevSetGod(2, false);

        Check(Upgrades.Ability(AbilityId.Sanctify).Name == "Lumen Ward", "Sanctify is now called Lumen Ward");

        // The cast makes one cone zone along the aim, which the client sees with its direction.
        ally.Hp = ally.MaxHp * 0.3f;
        use();
        var zone = g.Zones.Values.FirstOrDefault(z => z.Kind == ZoneKind.LumenWard);
        var held = shot(new V2(400f, 150f), new V2(120f, 0f));   // outside the cone
        make.Invoke(client.Game, new object[] { held.Id, Defs.BigShot, 0, held.Pos, held.Vel, 0f, 60f, false, 0f });  // the client's copy, as a spawn message would make it
        var burnt = shot(new V2(650f, 440f), new V2(-60f, 0f));  // inside it
        float h0 = ally.Hp;
        play(2f);
        bool seen = client.Game.Zones.Values.Any(z => z.Kind == ZoneKind.LumenWard && Math.Abs(z.Aim) < 0.01f && Math.Abs(z.Radius - Game.LumenLength) < 0.5f);
        Check(zone != null && Math.Abs(zone.Duration - (Game.LumenTime + Game.LumenClose)) < 0.01f && seen, "Lumen Ward makes a cone zone along the aim that the client sees");
        float gained = ally.Hp - h0;
        Console.WriteLine("  lumen ward: an ally in the light gained {0:0.0} HP in 2 s (about 16 expected)", gained);
        Check(gained > 12f && gained < 19f, "Lumen Ward heals allies inside about 9 HP/s");

        // Enemy bullets inside the light burn away; those outside slow to a stop.
        Check(!g.Bullets.ContainsKey(burnt.Id), "an enemy bullet inside the light burns away");
        Check(g.Bullets.ContainsKey(held.Id), "an enemy bullet outside the light survives");
        V2 p1 = held.Pos;
        play(0.5f);
        float drift = held.Pos.DistanceTo(p1);
        Console.WriteLine("  lumen ward: a bullet outside moved {0:0.0} px in 0.5 s (about 0 expected), stasis {1:0.00}", drift, held.Stasis);
        Check(drift < 3f && held.Stasis > 0.95f, "an enemy bullet outside the light is held still");
        Check(client.Game.Bullets.ContainsKey(held.Id) && client.Game.Bullets[held.Id].Stasis > 0.9f, "the client holds it still too");

        // An ally outside the light is not healed.
        at = outside + new V2(0f, 100f);
        ally.Hp = ally.MaxHp * 0.3f;
        h0 = ally.Hp;
        play(1f);
        Check(ally.Hp - h0 < 1f, "Lumen Ward doesn't heal allies outside the light");

        // When it ends (6.7 s in all) the bullet flies again.
        play(4.5f);
        Check(!g.Zones.Values.Any(z => z.Kind == ZoneKind.LumenWard), "Lumen Ward ends after its 6 s and a short fold");
        V2 p2 = held.Pos;
        play(1f);
        Console.WriteLine("  lumen ward: after it ended the bullet moved {0:0.0} px in 1 s (about 120 expected)", held.Pos.DistanceTo(p2));
        Check(held.Pos.DistanceTo(p2) > 60f && held.Stasis < 0.05f, "a held bullet flies on again when the light ends");

        // The capstone freezes enemies outside the light, not the ones inside.
        g.Zones.Clear(); g.Bullets.Clear();
        me.MasteryXp[AbilityId.Sanctify] = Game.MasteryXpFor[Game.MasteryMax - 1];
        var far = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, outside, 0f, -7, false });
        far.SpawnT = 0f; far.Hp = far.MaxHp = 1e6f;
        var near = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, inside, 0f, -8, false });
        near.SpawnT = 0f; near.Hp = near.MaxHp = 1e6f;
        use();
        g.FixedStep(dt, new PlayerInput());
        for (int i = 0; i < 40; i++) { far.Pos = outside; near.Pos = inside; far.Hp = near.Hp = 1e6f; g.FixedStep(dt, new PlayerInput()); }
        Check(far.FrozenT > 0f && near.FrozenT <= 0f, "a mastered Lumen Ward freezes enemies outside the light but not inside it");
        me.MasteryXp.Remove(AbilityId.Sanctify);
        far.Hp = near.Hp = 0f;
        g.Zones.Clear(); g.Bullets.Clear();
    }
}
