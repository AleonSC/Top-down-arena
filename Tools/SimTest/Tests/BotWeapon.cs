using System;
using System.Collections.Generic;
using Arena;

// The smart bot's parry (BotParry.cs) and weapon Alts (BotWeaponAlt.cs).
static partial class Program
{
    /// A host with a smart bot at `level` and one still friend far off; the bot stands mid-arena, the friend in a corner.
    static (Session[] s, Game g, Player bot) BotWeaponSetup(float dt, BotSkill level, Role role, int weapon, Kit kit = Kit.Soldier)
    {
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2 }, port, false, "Bot", role, weapon, 0, kit);
        var client = JoinAs(port, false, "Ally", Role.Tank, 0);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        g.Bot = true; g.BotLevel = level; g.BotRerolls = false;
        client.Game.Bot = false;
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        return (s, g, g.Players[1]);
    }

    static void BotParryTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawnShot = typeof(Game).GetMethod("SpawnBullet", flags);
        V2 center = new V2(Game.Width / 2f, Game.Height / 2f);
        foreach (var level in new[] { BotSkill.Good, BotSkill.Expert })
        {
            var (s, g, bot) = BotWeaponSetup(dt, level, Role.Dps, 0);
            int needles = 0, volleys = 0;
            var seen = new HashSet<int>();
            Action step = () =>
            {
                foreach (var e in g.Enemies.Values) e.Hp = 0f;
                g.Players[2].Pos = new V2(60f, 60f);
                foreach (var b in g.Bullets.Values)
                    if (b.Owner == bot.Id && b.Kind == Defs.ParryNeedle && seen.Add(b.Id)) needles++;
            };
            // Calm first: nothing coming, so the shield stays down.
            bot.Pos = center;
            Run(s, dt, 1.5f, null, step);
            int calmRaises = g.BotParries;
            // A wall of shots from the right every 1.2 s, too wide to walk out of in time: block it.
            for (float t = 0f; t < 9f; t += dt)
            {
                if (MathX.Fmod(t, 1.2f) < dt)
                {
                    volleys++;
                    for (int k = -4; k <= 4; k++)
                    {
                        V2 from = bot.Pos + new V2(230f, k * 22f);
                        spawnShot.Invoke(g, new object[] { Defs.SmallShot, from, new V2(-520f, 0f), 0f, 2f, 10f, -1, false, 0f });
                    }
                }
                Run(s, dt, dt, null, step);
            }
            Console.WriteLine("  {0}: {1} raises in the calm, {2} raises and {3} perfect-parry needles over {4} volleys", level, calmRaises, g.BotParries - calmRaises, needles, volleys);
            Check(calmRaises == 0, level + " bot keeps the shield down with nothing coming");
            Check(g.BotParries - calmRaises >= volleys / 2, level + " bot raises the parry against shots it can't dodge");
            Check(needles >= volleys, level + " bot's parries are timed perfect (shots go back as needles)");
        }
    }

    /// Each weapon with an Alt rule: a bot in a small fight (tough enemies close and far, a volley of shots now and then,
    /// a hurt friend nearby) presses its Alt at least once in 10 s.
    static void BotAltTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var spawnShot = typeof(Game).GetMethod("SpawnBullet", flags);
        V2 center = new V2(Game.Width / 2f, Game.Height / 2f);
        var weapons = new[]
        {
            WeaponId.Scattergun, WeaponId.FlakCannon, WeaponId.RiotRepeater, WeaponId.TwinRifle, WeaponId.Revolver, WeaponId.RepulsionCannon,
            WeaponId.FrostLance, WeaponId.StormHammer, WeaponId.Nimbus, WeaponId.ArcLightning, WeaponId.BoneGuard,
            WeaponId.Lance, WeaponId.Quarterstaff, WeaponId.Lightbringer, WeaponId.Censer, WeaponId.TwinBlades, WeaponId.Greatsword, WeaponId.Rapier,
            WeaponId.Kusarigama, WeaponId.BoStaff, WeaponId.FumaShuriken, WeaponId.Katana, WeaponId.Kunai,
            WeaponId.BloodLash, WeaponId.BloodBrawler,
            WeaponId.ArcKnuckles, WeaponId.TwinRifleAuto, WeaponId.OutOfAmmo, WeaponId.DoubleBarrel, WeaponId.Stonespear, WeaponId.GrenadeLauncher,
            WeaponId.HoneybeeStaff, WeaponId.Nunchaku, WeaponId.WindClash, WeaponId.EmbolusSpears, WeaponId.BulwarkBarrier, WeaponId.WyvernStaff, WeaponId.TwinPistols, WeaponId.StaticShock, WeaponId.HiveCenser,
        };
        var missed = new System.Collections.Generic.List<string>();
        foreach (var wid in weapons)
        {
            var def = Defs.WeaponOf(wid);
            var (s, g, bot) = BotWeaponSetup(dt, BotSkill.Good, def.Role, def.Choice, def.Kit);
            var friend = g.Players[2];
            foreach (var e in g.Enemies.Values) e.Hp = 0f;
            Run(s, dt, 0.2f);
            bot.Pos = center;
            var mine = new System.Collections.Generic.List<Enemy>();
            for (int k = 0; k < 6; k++)
            {
                float r = k < 4 ? 140f : 300f;
                var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, center + V2.FromAngle(k * 1.05f) * r, 0f, -7, false });
                e.SpawnT = 0f; e.MaxHp = e.Hp = 99999f;
                mine.Add(e);
            }
            int presses0 = g.BotAltPresses;
            for (float t = 0f; t < 10f; t += dt)
            {
                foreach (var e in g.Enemies.Values) if (!mine.Contains(e)) e.Hp = 0f;
                foreach (var e in mine) e.Hp = Math.Max(e.Hp, 50000f);
                friend.Pos = bot.Pos + new V2(-120f, 60f);
                friend.Hp = Math.Min(friend.Hp, friend.MaxHp * 0.4f);
                if (bot.Pos.DistanceTo(center) > 300f) bot.Pos = center;
                if (MathX.Fmod(t, 2f) < dt)
                    for (int k = -3; k <= 3; k++)
                        spawnShot.Invoke(g, new object[] { Defs.SmallShot, bot.Pos + new V2(220f, k * 20f), new V2(-480f, 0f), 0f, 2f, 5f, -1, false, 0f });
                Run(s, dt, dt);
            }
            int presses = g.BotAltPresses - presses0;
            Console.WriteLine("  {0}: Alt pressed {1} times in 10 s", wid, presses);
            if (presses == 0) missed.Add(wid.ToString());
        }
        Check(missed.Count == 0, "every weapon with an Alt rule uses it in a fight" + (missed.Count > 0 ? " (missed: " + string.Join(", ", missed) + ")" : ""));
    }

    /// The Katana bot (BotKatana.cs) in a fight: it stores a finisher by switching away, plays both finishers back to back,
    /// and now and then leaves a combo halfway for the other stance's opening moves.
    static void BotKatanaTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var spawnShot = typeof(Game).GetMethod("SpawnBullet", flags);
        V2 center = new V2(Game.Width / 2f, Game.Height / 2f);
        var def = Defs.WeaponOf(WeaponId.Katana);
        var (s, g, bot) = BotWeaponSetup(dt, BotSkill.Good, def.Role, def.Choice, def.Kit);
        foreach (var e in g.Enemies.Values) e.Hp = 0f;
        Run(s, dt, 0.2f);
        bot.Pos = center;
        var mine = new System.Collections.Generic.List<Enemy>();
        for (int k = 0; k < 6; k++)
        {
            float r = k < 4 ? 140f : 300f;
            var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, center + V2.FromAngle(k * 1.05f) * r, 0f, -7, false });
            e.SpawnT = 0f; e.MaxHp = e.Hp = 99999f;
            mine.Add(e);
        }
        for (float t = 0f; t < 30f; t += dt)
        {
            foreach (var e in g.Enemies.Values) if (!mine.Contains(e)) e.Hp = 0f;
            foreach (var e in mine) e.Hp = Math.Max(e.Hp, 50000f);
            if (MathX.Fmod(t, 6f) < dt)  // the crowd scatters now and then: far targets to close on
                for (int k = 0; k < mine.Count; k++)
                    mine[k].Pos = bot.Pos + V2.FromAngle(k * 1.05f + t) * (MathX.Fmod(t, 12f) < 6f ? 260f : 120f);
            if (bot.Pos.DistanceTo(center) > 300f) bot.Pos = center;
            if (MathX.Fmod(t, 2.5f) < dt)
                for (int k = -3; k <= 3; k++)
                    spawnShot.Invoke(g, new object[] { Defs.SmallShot, bot.Pos + new V2(220f, k * 20f), new V2(-480f, 0f), 0f, 2f, 5f, -1, false, 0f });
            Run(s, dt, dt);
        }
        Console.WriteLine("  Katana bot in 30 s: {0} finishers, {1} pairs, {2} stored, {3} cancels",
            g.BotKatanaFinishers, g.BotKatanaPairs, g.BotKatanaStores, g.BotKatanaCancels);
        Check(g.BotKatanaStores >= 1, "the Katana bot stores a finisher by switching away");
        Check(g.BotKatanaPairs >= 1, "the Katana bot plays both finishers back to back");
        Check(g.BotKatanaCancels >= 1, "the Katana bot leaves a combo for the other stance's opening moves");
    }

    /// The bots' level-up picks (BotPicks.cs): a card's worth from what it does, its rarity and who it was made for.
    static void BotPicksTest(float dt)
    {
        Func<string, int> idOf = name => Array.FindIndex(Upgrades.All, u => u.Name == name);
        foreach (var role in new[] { Role.Dps, Role.Tank, Role.Healer })
        {
            var me = new Player { Role = role };
            var scored = new List<(float, string)>();
            for (int i = 0; i < Upgrades.All.Length; i++)
                if (!Upgrades.All[i].Unoffered && Upgrades.All[i].Rarity <= Rarity.Rare)
                    scored.Add((Game.BotCardScore(me, i), Upgrades.All[i].Rarity + " " + Upgrades.All[i].Name));
            scored.Sort((a, b) => b.Item1.CompareTo(a.Item1));
            Console.WriteLine("  {0}: best {1}; worst {2}", role,
                string.Join(", ", scored.GetRange(0, 4).ConvertAll(x => x.Item2 + " " + x.Item1.ToString("0"))),
                string.Join(", ", scored.GetRange(scored.Count - 3, 3).ConvertAll(x => x.Item2 + " " + x.Item1.ToString("0"))));
        }
        Func<Role, string, float> score = (role, name) => Game.BotCardScore(new Player { Role = role }, idOf(name));
        Check(score(Role.Dps, "Hollow Points") > score(Role.Dps, "Fortitude"), "a DPS bot takes +20% damage over +40 health");
        Check(score(Role.Tank, "Fortitude") > score(Role.Tank, "Sprinter"), "a tank bot takes +40 health over +12% speed");
        Check(score(Role.Dps, "Hollow Points") > score(Role.Dps, "Light Boots"), "an uncommon damage card beats a common speed card");
        var stacked = new Player { Role = Role.Dps };
        for (int k = 0; k < 4; k++) stacked.Taken.Add(idOf("Hollow Points"));
        Check(Game.BotCardScore(stacked, idOf("Hollow Points")) < score(Role.Dps, "Hollow Points"), "a card already taken a few times counts for less (its copies diminish)");
    }

    /// The bot at the shop (Shop.cs BotShop): heals first when badly hurt, buys the card worth the most for its gold,
    /// leaves cards that are worth little to it, then it's done.
    static void BotShopTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var openShop = typeof(Game).GetMethod("OpenShop", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = -5 }, port, false, "Bot", Role.Dps);
        var client = JoinAs(port, false, "Friend", Role.Tank);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        Action noPicks = () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = p.PendingCards = 0; p.LegendaryQueue.Clear(); p.Offer = null; } };
        Run(s, dt, 20f, () => g.State == MatchState.Fighting && g.Enemies.Count > 0);
        Run(s, dt, 30f, () => g.State == MatchState.Intermission, () => { foreach (var e in g.Enemies.Values) e.Hp = 0f; });
        noPicks();
        g.PathOffer = new[] { PathId.Bazaar, PathId.SniperRidge };
        g.ChoosePath(0);
        Run(s, dt, 0.5f, null, noPicks);
        openShop.Invoke(g, null);
        var me = g.Players[1];
        Func<string, int> idOf = name => Array.FindIndex(Upgrades.All, u => u.Name == name);
        int boots = idOf("Light Boots"), points = idOf("Hollow Points"), vitality = idOf("Vitality"), trigger = idOf("Hair Trigger");
        me.Shelf[0] = vitality; me.Shelf[1] = boots; me.Shelf[2] = points;
        for (int i = 3; i < me.Shelf.Length; i++) me.Shelf[i] = -1;
        me.ShelfLen = 3; me.ShelfSold = 0; me.Restocks = 9;  // (no adding stock in this test)
        me.Gold = 170; me.ReviveToken = true;
        me.DevGod = false; me.Hp = me.MaxHp * 0.3f;
        int had = me.Taken.Count;
        g.Bot = true; g.BotLevel = BotSkill.Good; g.BotRerolls = false;
        Run(s, dt, 8f, () => me.ShopDone, noPicks);
        Console.WriteLine("  bot shop: done {0}, health {1:0}/{2:0}, gold left {3}, bought {4}", me.ShopDone, me.Hp, me.MaxHp, me.Gold,
            string.Join(", ", me.Taken.GetRange(had, me.Taken.Count - had).ConvertAll(id => Upgrades.All[id].Name)));
        Check(me.ShopDone, "the bot finishes shopping");
        Check(me.Hp >= me.MaxHp - 0.5f, "a badly hurt bot heals at the shop");
        Check(me.Taken.Contains(points), "the bot buys the card worth the most to it (+20% damage)");
        Check(!me.Taken.GetRange(had, me.Taken.Count - had).Contains(vitality), "it leaves a card worth little to it (+15 health on a DPS)");
    }
}
