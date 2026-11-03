using System.Linq;
using System;
using Arena;

// Healing Rain (the Healer Elementalist's zone ability, Docs/HealingRain.md): it heals allies inside, slows enemies, ends on time,
// and its capstone lasts longer and speeds up revives.
static partial class Program
{
    static void HealingRainTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var cast = typeof(Game).GetMethod("HostAbility", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2 }, port, false, "Healer", Role.Healer, 0, 0, Kit.Elementalist);
        var client = JoinAs(port, false, "Tank", Role.Tank, 0);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        var ally = g.Players[2];
        g.Bot = client.Game.Bot = false;
        V2 center = new V2(500f, 450f), inside = center + new V2(60f, 0f), outside = center + new V2(300f, 0f);
        V2 at = inside;
        Action<float> play = secs =>
        {
            for (int f = 0; f < secs * 60; f++)
            {
                foreach (var e in g.Enemies.Values) if (e.Pos.DistanceTo(center) > 150f) e.Hp = 0f;
                me.Pos = center - new V2(200f, 0f);
                ally.Pos = client.Game.Me.Pos = at;
                host.Poll(); g.FixedStep(dt, new PlayerInput()); g.Update(dt);
                client.Poll(); client.Game.FixedStep(dt, new PlayerInput()); client.Game.Update(dt);
            }
        };
        Action use = () => { g.ShowcaseSetAbility(AbilityId.HealingRain); me.Slots[0].Charges = 1; cast.Invoke(g, new object[] { 1, 0, center, true }); };
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        play(0.3f);
        g.Zones.Clear(); g.Orbs.Clear();
        g.DevSetGod(2, false);

        // An ally inside is healed about 8 HP/s, and the client sees the zone.
        ally.Hp = ally.MaxHp * 0.3f;
        use();
        var zone = g.Zones.Values.FirstOrDefault(z => z.Kind == ZoneKind.HealingRain);
        float h0 = ally.Hp;
        play(2f);
        float gained = ally.Hp - h0;
        bool seen = client.Game.Zones.Values.Any(z => z.Kind == ZoneKind.HealingRain && Math.Abs(z.Radius - 130f) < 0.5f);
        Console.WriteLine("  healing rain: an ally inside gained {0:0.0} HP in 2 s (about 16 expected), zone length {1:0.0} s, client sees it {2}", gained, zone?.Duration ?? 0f, seen);
        Check(zone != null && Math.Abs(zone.Duration - 6f) < 0.01f && seen, "Healing Rain makes a 6 s zone of radius 130 that the client sees");
        Check(gained > 14f && gained < 18f, "Healing Rain heals allies inside about 8 HP/s");

        // An enemy inside is slowed 30%; the rain does it no damage.
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var enemy = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, center, 0f, -7, false });
        enemy.SpawnT = 0f;
        float full = enemy.Hp;
        g.FixedStep(dt, new PlayerInput());
        g.FixedStep(dt, new PlayerInput());
        Console.WriteLine("  healing rain: an enemy inside has slow x{0:0.00} and {1:0}/{2:0} HP", enemy.SlowMult, enemy.Hp, full);
        Check(enemy.SlowMult <= 0.7f + 0.001f && enemy.Hp >= full - 0.01f, "Healing Rain slows an enemy inside by 30% and doesn't hurt it");
        enemy.Hp = 0f;

        // An ally outside is not healed.
        at = outside;
        ally.Hp = ally.MaxHp * 0.3f;
        h0 = ally.Hp;
        play(1f);
        Check(ally.Hp - h0 < 1f, "Healing Rain doesn't heal allies outside it");

        // It ends on time (6 s in all, plus the stretch used above).
        play(4f);
        Check(!g.Zones.Values.Any(z => z.Kind == ZoneKind.HealingRain), "Healing Rain ends after 6 s");

        // The capstone: 10 s, and a downed ally inside revives faster than outside.
        g.Zones.Clear();
        me.MasteryXp[AbilityId.HealingRain] = Game.MasteryXpFor[Game.MasteryMax - 1];
        use();
        var cap = g.Zones.Values.FirstOrDefault(z => z.Kind == ZoneKind.HealingRain);
        Check(cap != null && Math.Abs(cap.Duration - 10f) < 0.01f && cap.ExtraRevive > 0f, "a mastered Healing Rain lasts 10 s and speeds up revives");
        me.MasteryXp.Remove(AbilityId.HealingRain);
        g.Zones.Clear();
    }
}
