using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// The DPS Engineer's second weapon, the Bumble Remote (BumbleRemote.cs).
static partial class Program
{
    /// The DPS Engineer's 2nd key is the Bumble Remote; its Bumble Bot follows the cursor (no farther than the leash); you never
    /// shoot; it shoots the enemy nearest it on its own, twice as fast with Fire held, and sprays with Fire held and nothing near;
    /// enemies walk at you, not at it; Alt dives to the cursor: damage, a shove, bees, a 6 s cooldown; the other screen sees it.
    static void BumbleRemoteTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Pilot", Role.Dps, 1, 0, Kit.Engineer);
        var client = JoinAs(port, false, "Friend", Role.Dps, 0, 0, Kit.Soldier);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        g.Bot = client.Game.Bot = false;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        Check(me.Weapon.Id == WeaponId.BumbleRemote && Defs.Weapon(Role.Dps, Kit.Engineer, 1).Id == WeaponId.BumbleRemote && !Defs.WeaponGap(Role.Dps, Kit.Engineer, 1),
            "the DPS Engineer's 2nd key is the Bumble Remote, unlocked (" + me.Weapon.Id + ")");
        var queue = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
        V2 from = new V2(500f, 450f), aim = from + new V2(250f, -60f);
        Func<EnemyType, V2, Enemy> Dummy = (type, at) =>
        {
            var d = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            d.MaxHp = d.Hp = 1e6f; d.Speed = 0f; d.FireT = 99f; d.SpawnT = 0f;
            return d;
        };
        Action<float, bool, bool> Step = (secs, fire, alt) =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                me.Pos = from; queue.Clear();
                foreach (var e in g.Enemies.Values) e.FireT = 99f;
                foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == host ? new PlayerInput { AimAt = aim, Fire = fire, WeaponAction = alt && f < 3 } : new PlayerInput()); }
            }
        };
        Func<Enemy, float> took = e => e.MaxHp - e.Hp;
        Func<int> mine = () => g.Bullets.Values.Count(b => b.Owner == 1);
        queue.Clear(); g.Enemies.Clear(); g.Bullets.Clear();
        Step(1.5f, false, false);
        var bot = g.Minions.Values.FirstOrDefault(m => m.Kind == MinionKind.BumbleBot && m.Owner == 1);
        Check(bot != null && bot.Companion, "the remote comes with a Bumble Bot companion");
        Check(bot != null && bot.Pos.CloserThan(aim, 20f), "which flies to your cursor (" + (bot == null ? "-" : (bot.Pos - aim).Length.ToString("0")) + " px off)");
        Check(client.Game.Minions.Values.Any(m => m.Kind == MinionKind.BumbleBot && m.Pos.CloserThan(aim, 40f)), "the other screen sees it there");
        Check(mine() == 0, "with nothing near and Fire up, nothing is fired");

        // Fire held with nothing near: it sprays from itself, never from you
        Step(0.5f, true, false);
        var shots = g.Bullets.Values.Where(b => b.Owner == 1).ToList();
        Check(shots.Count >= 3 && shots.All(b => b.Pos.FartherThan(from, 120f)), "Fire held sprays stingers from the bot, none from you (" + shots.Count + ")");
        Check(bot.Wind == 1f && client.Game.Minions.Values.Any(m => m.Kind == MinionKind.BumbleBot && m.Wind == 1f), "both screens see Fire held (Wind 1)");
        Step(0.6f, false, false);
        g.Bullets.Clear();

        // the leash
        V2 keep = aim;
        aim = from + new V2(1200f, 0f);
        Step(1.5f, false, false);
        Check(bot.Pos.CloserThan(from, Game.BumbleLeash + 25f) && bot.Pos.FartherThan(from, Game.BumbleLeash - 40f), "a far cursor: it goes no farther than " + Game.BumbleLeash + " px from you (" + (bot.Pos - from).Length.ToString("0") + ")");
        aim = keep;
        Step(1.5f, false, false);

        // on its own: shoots the enemy nearest it; Fire held: twice the rate
        g.Enemies.Clear(); g.Bullets.Clear();
        var target = Dummy(EnemyType.Grunt, bot.Pos + new V2(200f, 0f));
        Step(2f, false, false);
        float auto = took(target);
        float perShot = Defs.WeaponOf(WeaponId.BumbleRemote).Damage * me.Stats.DmgMult;
        Check(auto > perShot * 6f, "with Fire up it shoots the enemy near it on its own (" + auto.ToString("0") + " in 2 s)");
        target.Hp = target.MaxHp;
        Step(2f, true, false);
        float heldDmg = took(target);
        Check(heldDmg > auto * 1.6f, "holding Fire about doubles it (" + heldDmg.ToString("0") + " vs " + auto.ToString("0") + ")");
        g.Enemies.Clear(); g.Bullets.Clear();

        // enemies come for you, not the bot (unlike the robots, it draws nobody in: RobotTarget)
        var walker = Dummy(EnemyType.Grunt, bot.Pos + new V2(30f, 0f));
        var pulled = (V2)typeof(Game).GetMethod("RobotTarget", flags).Invoke(g, new object[] { walker, from });
        Check(pulled.CloserThan(from, 0.1f), "an enemy right next to the bot still goes for you");
        g.Enemies.Clear(); g.Bullets.Clear();
        Step(0.5f, false, false);

        // Alt: the Swarm Burst
        V2 spot = from + new V2(-200f, 150f);
        aim = spot;
        var hit = Dummy(EnemyType.Grunt, spot + new V2(50f, 0f));
        var miss = Dummy(EnemyType.Grunt, spot + new V2(0f, 260f));
        V2 hit0 = hit.Pos;
        int bees0 = g.Minions.Values.Count(m => m.Kind == MinionKind.Bee && m.Owner == 1);
        Step(dt * 4f, false, true);
        Check(bot.Wind == 2f && me.BumbleCd > 5f, "Alt sends the bot diving and starts the 6 s cooldown");
        Step(0.4f, false, false);
        int bees = g.Minions.Values.Count(m => m.Kind == MinionKind.Bee && m.Owner == 1) - bees0;
        float burst = Game.BumbleBurstDmg * me.Stats.DmgMult;
        Check(took(hit) >= burst - 0.5f && took(miss) < burst, "it lands at the cursor: an enemy within " + Game.BumbleBurstR + " px takes the blast, one beyond doesn't (" + took(hit).ToString("0") + ", " + took(miss).ToString("0") + ")");
        Check((hit.Pos - spot).Length > (hit0 - spot).Length + 20f, "and is shoved away (" + (hit0 - spot).Length.ToString("0") + " -> " + (hit.Pos - spot).Length.ToString("0") + ")");
        Check(bees >= Game.BumbleBees - 1, "and " + Game.BumbleBees + " bees burst out (" + bees + ")");
        Check(client.Game.Effects.Any(e => e.Kind == FxKind.BumbleBurst) || g.Effects.Any(e => e.Kind == FxKind.BumbleBurst), "the burst is drawn (FxKind.BumbleBurst)");
        Check(bot.Wind != 2f, "the bot is cruising again");
        Step(dt * 4f, false, true);
        Check(bot.Wind != 2f, "Alt does nothing again while it cools down");
    }
}
