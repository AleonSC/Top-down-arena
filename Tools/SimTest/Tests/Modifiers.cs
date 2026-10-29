using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using Arena;

// The newer run modifiers: Head Start, Practiced, Bloodlust, Gold Rush, Tiny Arena and Nightfall.
static partial class Program
{
    static void MoreModifiersTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var hurt = typeof(Game).GetMethod("HurtEnemy", flags);
        var cleared = typeof(Game).GetMethod("WaveCleared", flags);
        var closeShop = typeof(Game).GetMethod("CloseShop", flags);
        int port = nextPort++;
        var host = new Session();
        host.Host(port, "Host", Role.Tank);
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Dps);
        var s = new[] { host, client };
        Action Sync = () => { for (int i = 0; i < 40; i++) { host.Poll(); client.Poll(); Thread.Sleep(2); } };
        for (int i = 0; i < 300 && client.Screen != AppScreen.Lobby; i++) { host.Poll(); client.Poll(); Thread.Sleep(2); }
        Sync();
        foreach (var m in new[] { RunMod.HeadStart, RunMod.Practiced, RunMod.Bloodlust, RunMod.GoldRush, RunMod.TinyArena, RunMod.Nightfall })
            host.SetModifier(m, true);
        Sync();
        client.RequestReady(true); host.RequestReady(true); Sync();
        host.StartMatch();
        for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
        var g = host.Game;
        var me = g.Players[1];
        Check(client.Game.Has(RunMod.Nightfall) && Modifiers.Count(client.Modifiers) == 6, "the friend's game knows the new modifiers");

        // Head Start: level 3, its two picks waiting, and a legendary first.
        Console.WriteLine("  head start: level {0} (friend's screen {1}), picks {2}, legendary offer {3}", me.Level, client.Game.Players[1].Level, me.PendingPicks, me.OfferLegendary);
        Check(me.Level == 3 && client.Game.Players[1].Level == 3 && me.PendingPicks == 2 && me.OfferLegendary && me.Offer != null,
            "Head Start: everyone starts at level 3 with their picks and a legendary to choose");
        foreach (var p in g.Players.Values) { p.PendingPicks = 0; p.LegendaryQueue.Clear(); p.PendingLegendary = 0; p.Offer = null; p.OfferLegendary = false; }

        // Practiced: mastery comes twice as fast.
        var ability = me.RoleAbility;
        float xp0; me.MasteryXp.TryGetValue(ability, out xp0);
        g.GainMastery(me, ability, 10f);
        Check(Math.Abs(me.MasteryXp[ability] - xp0 - 20f) < 0.01f, "Practiced: abilities gain mastery twice as fast");

        // Bloodlust: kills heal, no orbs, no regeneration.
        Run(s, dt, 6f, () => g.State == MatchState.Fighting);
        var queue = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
        // A frozen dummy in a corner keeps the wave going (clearing it would heal everyone 30%).
        var keep = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, new V2(60f, 60f), 0f, 0, false });
        keep.MaxHp = keep.Hp = 1e6f; keep.Speed = 0f; keep.FireT = 1e6f;
        Action Quiet = () =>
        {
            queue.Clear();
            foreach (var e in g.Enemies.Values.ToList()) if (e != keep && e.Hp > 0f && e.Def.Xp > 0 && e.T > 0f) g.Enemies.Remove(e.Id);
            keep.FrozenT = 1f; keep.FireT = 1e6f;
        };
        me.Hp = me.MaxHp * 0.5f;
        float hp0 = me.Hp;
        Run(s, dt, 2f, null, () => { Quiet(); g.Orbs.Clear(); });
        float afterWait = me.Hp;
        int orbs = g.Orbs.Count, gold0 = me.Gold;
        for (int k = 0; k < 6; k++)
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, new V2(700f + 20f * k, 400f), 0f, 0, false });
            e.SpawnT = 0f;
            hurt.Invoke(g, new object[] { e, e.Hp + 1f, 1 });
        }
        Step(host, dt);
        Console.WriteLine("  bloodlust: hp {0:0} -> {1:0} after 2 s of rest, {2:0} after 6 kills; orbs {3}; gold {4} -> {5}", hp0, afterWait, me.Hp, g.Orbs.Count - orbs, gold0, me.Gold);
        Check(afterWait <= hp0 + 0.01f, "Bloodlust: no regeneration");
        Check(me.Hp > afterWait + 5f && g.Orbs.Count == orbs, "Bloodlust: kills heal the killer, and no health orbs drop");

        // Gold Rush: kills drop gold on any wave, a shop opens before the boss, and the gold left over is kept.
        Check(me.Gold > gold0 && g.Players[2].Gold == me.Gold, "Gold Rush: every kill gives everyone gold");
        g.Wave = 4;
        cleared.Invoke(g, null);
        Check(g.ShopOpen, "Gold Rush: a shop opens before the boss");
        int kept = me.Gold;
        closeShop.Invoke(g, null);
        Check(!g.ShopOpen && me.Gold == kept && kept > 0, "Gold Rush: gold you don't spend is kept for the next shop");

        // Tiny Arena: walls after each boss, open for the bosses. Nightfall: dark on every screen.
        Func<Game, float> Room = x => (x.ArenaMax.X - x.ArenaMin.X) * (x.ArenaMax.Y - x.ArenaMin.Y) / (Game.Width * Game.Height);
        float before = Room(g);
        g.Wave = 6; g.State = MatchState.Fighting;
        Run(s, dt, 2.5f, null, () => { Quiet(); g.Wave = 6; });
        float one = Room(g), oneClient = Room(client.Game);
        g.Wave = 16;
        Run(s, dt, 3f, null, () => { Quiet(); g.Wave = 16; });
        float three = Room(g);
        g.Wave = 41;
        Run(s, dt, 4f, null, () => { Quiet(); g.Wave = 41; });
        float floor = Room(g);
        g.Wave = 45;
        Run(s, dt, 4f, null, () => { Quiet(); g.Wave = 45; });
        float boss = Room(g);
        Console.WriteLine("  tiny arena: room {0:0.00} before a boss, {1:0.00} after 1 (friend {2:0.00}), {3:0.00} after 3, {4:0.00} after 8, {5:0.00} on a boss wave; walled {6}",
            before, one, oneClient, three, floor, boss, g.Walled);
        Check(before > 0.99f && Math.Abs(one - 0.75f) < 0.02f && Math.Abs(oneClient - one) < 0.02f && Math.Abs(three - 0.42f) < 0.02f && Math.Abs(floor - 0.4f) < 0.02f,
            "Tiny Arena: each boss beaten takes a quarter of the room, down to 40%, on every screen");
        Check(boss > 0.99f, "Tiny Arena: bosses get the whole arena");
        Check(g.NightF > 0.99f && client.Game.NightF > 0.99f, "Nightfall: it's dark on every screen");
        host.Leave(""); client.Leave("");
    }
}
