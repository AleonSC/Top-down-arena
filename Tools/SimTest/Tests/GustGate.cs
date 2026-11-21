using System.Linq;
using System;
using Arena;

// Gust Gate (the Healer Elementalist's 2nd choice, GustGate.cs): a ring of wind at the cursor; crossing its edge gives +80% move
// speed for 3 s (once per 1.2 s a player), fading over the last second; enemies inside are pushed gently out (bosses not);
// mastered it's 40% wider; it ends on time; the boost and the ring reach the client.
static partial class Program
{
    static void GustGateTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var cast = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
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
        V2 center = new V2(600f, 450f), meAt = new V2(300f, 450f);
        V2 allyAt = center + new V2(0f, 150f);
        Action<float> play = secs =>
        {
            for (int f = 0; f < secs * 60; f++)
            {
                foreach (var e in g.Enemies.Values) if (e.Id > 0 && e.Pos.DistanceTo(center) > 200f) e.Hp = 0f;
                me.Pos = meAt;
                ally.Pos = client.Game.Me.Pos = allyAt;
                host.Poll(); g.FixedStep(dt, new PlayerInput()); g.Update(dt);
                client.Poll(); client.Game.FixedStep(dt, new PlayerInput()); client.Game.Update(dt);
            }
        };
        Action<V2> use = at => { g.ShowcaseSetAbility(AbilityId.GustGate); me.Slots[0].Charges = 1; cast.Invoke(g, new object[] { 1, 0, at, true }); };
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        play(0.3f);
        g.Zones.Clear();

        // The ring: 70 px round, 8 s, where the cursor is; the client sees it.
        use(center);
        play(0.2f);
        var zone = g.Zones.Values.FirstOrDefault(z => z.Kind == ZoneKind.GustGate);
        bool seen = client.Game.Zones.Values.Any(z => z.Kind == ZoneKind.GustGate && Math.Abs(z.Radius - Game.GgRadius) < 0.5f);
        Check(zone != null && Math.Abs(zone.Duration - Game.GgLife) < 0.01f && zone.Pos.DistanceTo(center) < 1f && seen,
            "Gust Gate makes an 8 s, 70 px ring at the cursor that the client sees");

        // Standing outside: no boost. Walking in across the edge: +80% for 3 s, and the client has it.
        Check(ally.GustT <= 0f, "a player who hasn't crossed the ring has no boost");
        float slow = ally.Speed;
        allyAt = center + new V2(0f, 30f);
        play(0.1f);
        float boosted = ally.Speed;
        bool clientBoost = client.Game.Players[2].GustT > 2f;
        Console.WriteLine("  gust gate: speed {0:0} -> {1:0} (x{2:0.00}), boost left {3:0.00} s, client sees it {4}", slow, boosted, boosted / slow, ally.GustT, clientBoost);
        Check(ally.GustT > Game.GgBoostT - 0.2f && Math.Abs(boosted / slow - (1f + Game.GgBoost)) < 0.01f, "walking into the ring gives +80% move speed for 3 s");
        Check(clientBoost, "the boost reaches the client (its speed and its look)");

        // Out again at once: too soon to refresh. Back in after 1.2 s: refreshed.
        float t0 = ally.GustT;
        allyAt = center + new V2(0f, 150f);
        play(0.1f);
        Check(ally.GustT < t0, "crossing back out within 1.2 s doesn't refresh the boost");
        play(1.3f);
        allyAt = center;
        play(0.05f);
        Check(ally.GustT > Game.GgBoostT - 0.1f, "a pass after 1.2 s refreshes it");

        // It fades over the last second.
        Check(Math.Abs(Game.GustMult(0.5f) - 1.4f) < 0.001f && Game.GustMult(2f) == 1f + Game.GgBoost && Game.GustMult(0f) == 1f, "the boost fades out over its last second");

        // An enemy inside (held still) is pushed out from the middle, unhurt; you aren't pushed.
        var enemy = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, center + new V2(20f, 0f), 0f, -7, false });
        enemy.SpawnT = 0f;
        float full = enemy.Hp, d0 = enemy.Pos.DistanceTo(zone.Pos);
        for (int f = 0; f < 60; f++)
        {
            enemy.FrozenT = 1f; enemy.StunT = 1f;
            g.FixedStep(dt, new PlayerInput());
        }
        float d1 = enemy.Pos.DistanceTo(zone.Pos);
        Console.WriteLine("  gust gate: an enemy inside went from {0:0} to {1:0} px out in 1 s, {2:0}/{3:0} HP", d0, d1, enemy.Hp, full);
        Check(d1 - d0 > Game.GgPush * 0.6f && d1 <= zone.Radius + enemy.Radius + 4f && enemy.Hp >= full - 0.01f, "enemies inside are pushed gently out (about 55 px/s) and not hurt");
        enemy.Hp = 0f;

        // It ends on time.
        play(Game.GgLife);
        Check(!g.Zones.Values.Any(z => z.Kind == ZoneKind.GustGate), "Gust Gate ends after 8 s");

        // Mastered: 40% wider.
        me.MasteryXp[AbilityId.GustGate] = Game.MasteryXpFor[Game.MasteryMax - 1];
        use(center);
        var cap = g.Zones.Values.FirstOrDefault(z => z.Kind == ZoneKind.GustGate);
        Check(cap != null && Math.Abs(cap.Radius - Game.GgRadius * Game.GgMasteredSize) < 0.5f, "a mastered Gust Gate is 40% wider");
        me.MasteryXp.Remove(AbilityId.GustGate);
        g.Zones.Clear();
        host.Leave(""); client.Leave("");
    }
}
