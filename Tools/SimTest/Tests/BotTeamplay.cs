using System.Linq;
using System;
using Arena;

// The smart bot's team play: Roar and Last Stand for a teammate in trouble, Mark for Death on the team's target, walking to
// healing orbs and zones when hurt (BotBrain.cs, and each AbilityDef.Bot).
static partial class Program
{
    static void BotTeamplayTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);

        // The ability rules.
        Func<AbilityId, Func<BotSense, bool>> rule = id => Upgrades.Ability(id).Bot;
        var me = new Player { Hp = 100f, MaxHp = 100f };
        var calm = new BotSense { Me = me, HaveTarget = true, TargetD = 300f, Players = 3, DownedD = 9999f };
        Check(!rule(AbilityId.Roar)(calm), "Roar waits while nobody is in trouble");
        Check(rule(AbilityId.Roar)(new BotSense { Me = me, HaveTarget = true, TargetD = 300f, Players = 3, AllyThreatened = true, DownedD = 9999f }), "Roar goes off for a teammate with enemies on top of them");
        Check(rule(AbilityId.Mark)(calm) && !rule(AbilityId.Mark)(new BotSense { Me = me, HaveTarget = true, TargetD = 300f, TargetMarked = true }), "Mark for Death goes on an unmarked target, not twice");
        Check(!rule(AbilityId.LastStand)(calm) && rule(AbilityId.LastStand)(new BotSense { Me = me, AllyDown = true, DownedD = 40f }), "Last Stand plants over a downed ally, only once the tank is at them");
        Check(!rule(AbilityId.LastStand)(new BotSense { Me = me, AllyDown = true, DownedD = 400f }), "Last Stand doesn't plant far from the downed ally");

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2 }, port, false, "Bot", Role.Dps);
        var client = JoinAs(port, false, "Ally", Role.Tank, 0);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var bot = g.Players[1];
        var ally = g.Players[2];
        g.Bot = true; g.BotLevel = BotSkill.Good; g.BotRerolls = false;
        client.Game.Bot = false;
        V2 center = new V2(Game.Width / 2f, Game.Height / 2f);
        Action<float, Action> play = (secs, each) =>
        {
            for (int f = 0; f < secs * 60; f++)
            {
                foreach (var e in g.Enemies.Values) if (!e.Def.IsBoss) e.Hp = 0f;
                ally.Pos = client.Game.Me.Pos = new V2(100f, 100f);
                each?.Invoke();
                host.Poll(); g.FixedStep(dt, new PlayerInput()); g.Update(dt);
                client.Poll(); client.Game.FixedStep(dt, new PlayerInput()); client.Game.Update(dt);
            }
        };
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        play(0.3f, null);
        g.Zones.Clear(); g.Orbs.Clear();

        // Hurt, the bot walks to a healing orb that lies a little way off.
        bot.Pos = center; bot.Hp = bot.MaxHp * 0.3f;
        var orb = new Orb { Id = 9001, Pos = center + new V2(280f, 0f), Life = 6f, Big = true };
        g.Orbs[orb.Id] = orb;
        float d0 = bot.Pos.DistanceTo(orb.Pos);
        play(2.5f, () => { bot.Hp = Math.Min(bot.Hp, bot.MaxHp * 0.3f); });
        bool took = !g.Orbs.ContainsKey(orb.Id) || bot.Pos.DistanceTo(orb.Pos) < d0 - 150f;
        Console.WriteLine("  bot hurt: orb {0:0} px away at the start, {1:0} after 2.5 s (taken {2})", d0, bot.Pos.DistanceTo(orb.Pos), !g.Orbs.ContainsKey(orb.Id));
        Check(took, "a hurt bot walks to a healing orb");

        // ...and into a healing zone.
        g.Orbs.Clear(); g.Zones.Clear();
        bot.Pos = center; bot.Hp = bot.MaxHp * 0.3f;
        var zone = new Zone { Id = 9002, Kind = ZoneKind.HealingRain, Pos = center + new V2(-300f, 0f), Radius = 130f, Duration = 8f, T = 8f };
        g.Zones[zone.Id] = zone;
        play(3f, () => { bot.Hp = Math.Min(bot.Hp, bot.MaxHp * 0.3f); zone.T = 8f; });
        Console.WriteLine("  bot hurt: {0:0} px from a healing zone's centre after 3 s (radius 130)", bot.Pos.DistanceTo(zone.Pos));
        Check(bot.Pos.DistanceTo(zone.Pos) < 130f, "a hurt bot walks into the team's healing zone");

        // The bot aims at the marked enemy, not the nearer one.
        g.Orbs.Clear(); g.Zones.Clear();
        bot.Hp = bot.MaxHp; bot.Pos = center;
        var near = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, center + new V2(0f, -300f), 0f, -7, false });
        var far = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, center + new V2(330f, 0f), 0f, -7, false });
        near.SpawnT = far.SpawnT = 0f; near.Hp = far.Hp = 9999f; near.MaxHp = far.MaxHp = 9999f;
        far.MarkT = 6f;
        float aimErr = 9f;
        for (int f = 0; f < 90; f++)
        {
            bot.Pos = center; near.Pos = center + new V2(0f, -300f); far.Pos = center + new V2(330f, 0f); far.MarkT = 6f;
            ally.Pos = client.Game.Me.Pos = new V2(100f, 100f);
            host.Poll(); g.FixedStep(dt, new PlayerInput()); g.Update(dt);
            client.Poll(); client.Game.FixedStep(dt, new PlayerInput()); client.Game.Update(dt);
            if (f > 60) aimErr = Math.Min(aimErr, Math.Abs(MathX.Fmod(bot.Aim - 0f + MathX.Pi, MathX.Tau) - MathX.Pi));
        }
        Console.WriteLine("  bot aim is {0:0.00} rad off the marked enemy (the unmarked one is 1.57 off)", aimErr);
        Check(aimErr < 0.4f, "the bot focuses the enemy carrying a Mark for Death");
        host.Leave(""); client.Leave("");
    }
}
