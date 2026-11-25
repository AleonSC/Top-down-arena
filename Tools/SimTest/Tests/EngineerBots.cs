using System;
using System.Linq;
using Arena;

// The Engineer's robot weapons (EngineerBots.cs): a click summons the role's robot at the cursor (an enemy under it is its target),
// the robot builds in, shoots, draws enemies near it and is rebuilt-able 8 s after it falls; with it out, a click is the basic attack
// (the Vector Blade's swipe, the Iron Fist's long shove, the Stasis Gun's held field that holds enemies in place) and a fresh click on
// an enemy sends the robot at it. The Medic Bot heals allies near it.
static partial class Program
{
    static void EngineerBotsTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var hurtGuard = typeof(Game).GetMethod("HurtGuard", flags);
        var robotTarget = typeof(Game).GetMethod("RobotTarget", flags);

        Check(Defs.Weapon(Role.Dps, Kit.Engineer, 0).Id == WeaponId.VectorBlade && !Defs.WeaponGap(Role.Dps, Kit.Engineer, 0)
            && Defs.Weapon(Role.Tank, Kit.Engineer, 0).Id == WeaponId.IronFist && !Defs.WeaponGap(Role.Tank, Kit.Engineer, 0)
            && Defs.Weapon(Role.Healer, Kit.Engineer, 0).Id == WeaponId.StasisGun && !Defs.WeaponGap(Role.Healer, Kit.Engineer, 0)
            && Defs.Weapon(Role.Dps, Kit.Engineer, 1).Id == WeaponId.BumbleRemote && !Defs.WeaponGap(Role.Dps, Kit.Engineer, 1)
            && Defs.WeaponGap(Role.Dps, Kit.Engineer, 2), "each Engineer role's first weapon is its robot weapon, the DPS's second the Bumble Remote (the other places stay locked)");

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Eng", Role.Dps, 0, 0, Kit.Engineer);
        var client = JoinAs(port, false, "Friend", Role.Healer, 0, 0, Kit.Knight);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1]; var friend = g.Players[2];
        Check(me.Weapon.Id == WeaponId.VectorBlade, "the lobby's first DPS Engineer weapon is the Vector Blade");
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        V2 mePos = new V2(500f, 450f), friendPos = new V2(300f, 700f);
        V2 aim = new V2(800f, 450f);
        bool fire = false;
        Action<float> step = secs =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                me.Pos = mePos; friend.Pos = friendPos;
                foreach (var e in g.Enemies.Values) e.FireT = 99f;
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = aim, Fire = fire });
            }
        };
        Func<EnemyType, V2, Enemy> make = (type, at) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            e.Hp = e.MaxHp = 5000f; e.SpawnT = 0f; e.FireT = 99f;
            return e;
        };
        Func<Minion> bot = () => g.Minions.Values.FirstOrDefault(m => Game.IsBot(m.Kind) && m.Owner == 1);
        g.Enemies.Clear();
        step(0.3f);
        Check(bot() == null, "no robot until you click");

        // The summon: a click on an enemy builds the robot there and sends it at that enemy; this click doesn't swipe.
        var grunt = make(EnemyType.Grunt, new V2(800f, 450f));
        fire = true;
        step(0.3f);
        var b = bot();
        Check(b != null && b.Kind == MinionKind.AssaultBot && Math.Abs(b.MaxHp - Game.BotMaxHp(g.Wave, MinionKind.AssaultBot)) < 1f,
            "a click summons the Assault Bot with " + Game.BotMaxHp(g.Wave, MinionKind.AssaultBot).ToString("0") + " health");
        Check(b != null && b.Pos.DistanceTo(mePos) <= Game.BotReach + 1f && b.GoalEnemy == grunt.Id, "it lands at the cursor, sent at the enemy clicked on");
        Check(!g.Effects.Any(x => x.Kind == FxKind.Slash), "the summoning click doesn't swipe");
        fire = false;
        float hp0 = grunt.Hp;
        step(2f);
        Check(grunt.Hp < hp0 - 30f, "the Assault Bot builds in and shoots its target (" + (hp0 - grunt.Hp).ToString("0") + " damage)");
        Check(client.Game.Minions.Values.Any(m => m.Kind == MinionKind.AssaultBot && m.Stacks == 1 && m.LinkTo == grunt.Id), "the client sees the robot and its target");

        // It draws enemies near it.
        b = bot();
        var near = make(EnemyType.Grunt, b.Pos + new V2(0f, 100f));
        Check(((V2)robotTarget.Invoke(g, new object[] { near, new V2(1500f, 900f) })).DistanceTo(b.Pos) < 1f, "an enemy within 150 px of the robot goes for it instead of you");
        g.Enemies.Remove(near.Id);

        // With it out, a click is the Vector Blade's swipe, and a click on an enemy sends the robot there.
        var close = make(EnemyType.Grunt, mePos + new V2(60f, 0f));
        aim = close.Pos;
        float c0 = close.Hp;
        fire = true;
        step(dt * 2f);
        fire = false;
        step(0.1f);
        Check(c0 - close.Hp >= 20f && g.Effects.Any(x => x.Kind == FxKind.Slash), "with the robot out, a click swipes the blade (" + (c0 - close.Hp).ToString("0") + " damage)");
        Check(bot().GoalEnemy == close.Id, "a click on an enemy sends the robot at it");

        // It falls: a new one only after 8 s, and only on a click.
        hurtGuard.Invoke(g, new object[] { bot(), 999999f });
        Check(bot() == null && Math.Abs(me.BotCd - Game.BotRebuild) < 0.05f, "a broken robot is gone; the next in 8 s");
        aim = new V2(700f, 300f);
        fire = true;
        step(0.5f);
        Check(bot() == null, "no summon while it is being rebuilt (the click swipes instead)");
        fire = false;
        step(Game.BotRebuild);
        Check(bot() == null, "nor once it is ready, until you click");
        fire = true;
        step(0.1f);
        fire = false;
        Check(bot() != null, "a click summons it again");

        // Iron Fist: the Tank Bot, and the fist's long shove.
        me.Weapon = Defs.Weapon(Role.Tank, Kit.Engineer, 0);
        step(0.1f);
        Check(bot() == null, "a new weapon takes the old robot away");
        g.Enemies.Clear();
        aim = new V2(700f, 600f);
        fire = true;
        step(0.1f);
        fire = false;
        step(1f);
        Check(bot() != null && bot().Kind == MinionKind.TankBot && Math.Abs(bot().MaxHp - Game.BotMaxHp(g.Wave, MinionKind.TankBot)) < 1f, "the Iron Fist summons a Tank Bot");
        var shoved = make(EnemyType.Grunt, mePos + new V2(70f, 0f));
        aim = shoved.Pos;
        float s0 = shoved.Hp;
        fire = true;
        step(dt * 2f);
        fire = false;
        step(0.3f);
        Check(s0 - shoved.Hp >= 8f && s0 - shoved.Hp < 40f && shoved.Pos.X - mePos.X > 170f,
            "the fist hits for a little (" + (s0 - shoved.Hp).ToString("0") + ") and shoves far (to " + (shoved.Pos.X - mePos.X).ToString("0") + " px)");
        Check(((V2)robotTarget.Invoke(g, new object[] { shoved, new V2(1500f, 900f) })).DistanceTo(bot().Pos) < 1f || shoved.Pos.DistanceTo(bot().Pos) > Game.TankBotTaunt,
            "(the Tank Bot draws enemies from 240 px)");

        // Stasis Gun: the Medic Bot (it heals allies near it), and the field held at the cursor.
        me.Weapon = Defs.Weapon(Role.Healer, Kit.Engineer, 0);
        g.Enemies.Clear();
        aim = friendPos + new V2(40f, 0f);
        fire = true;
        step(0.1f);
        fire = false;
        step(1f);
        Check(bot() != null && bot().Kind == MinionKind.MedicBot, "the Stasis Gun summons a Medic Bot");
        friend.Hp = 30f;
        step(1f);
        Check(friend.Hp > 35f, "the Medic Bot heals an ally near it (" + (friend.Hp - 30f).ToString("0.0") + " in 1 s)");
        var held = make(EnemyType.Grunt, new V2(750f, 450f));
        V2 heldAt = held.Pos;
        aim = held.Pos;
        float h0 = held.Hp;
        fire = true;
        step(1.5f);
        var zone = g.Zones.Values.FirstOrDefault(z => z.Kind == ZoneKind.Stasis && z.Owner == 1);
        Check(zone != null && zone.Pos.DistanceTo(aim) < 1f && client.Game.Zones.Values.Any(z => z.Kind == ZoneKind.Stasis), "holding Fire keeps a stasis field at the cursor (seen by the client)");
        Check(held.Pos.DistanceTo(heldAt) < 2f && h0 - held.Hp > 15f, "an enemy in it is held in place and hurt (" + (h0 - held.Hp).ToString("0") + " in 1.5 s)");
        aim = new V2(1400f, 450f);
        step(0.1f);
        zone = g.Zones.Values.FirstOrDefault(z => z.Kind == ZoneKind.Stasis && z.Owner == 1);
        Check(zone != null && zone.Pos.DistanceTo(mePos) <= Game.StasisReach + 1f, "the field stays within 300 px of you");
        fire = false;
        step(0.1f);
        Check(!g.Zones.Values.Any(z => z.Kind == ZoneKind.Stasis), "letting go ends the field");
        V2 freed = held.Pos;
        step(1f);
        Check(held.Pos.DistanceTo(freed) > 10f, "and the enemy moves again");

        host.Leave(""); client.Leave("");
    }
}
